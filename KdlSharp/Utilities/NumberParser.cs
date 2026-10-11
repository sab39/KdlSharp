using System.Globalization;
using System.Numerics;
using System.Text;
using KdlSharp.Values;

namespace KdlSharp.Utilities;

/// <summary>
/// Provides shared number utilities for normalized number text, as produced by the lexer or by
/// <see cref="KdlNumber"/>'s constructors: an optional sign, then either decimal digits (with optional
/// fraction and exponent) or a hex (0x), octal (0o) or binary (0b) prefix and digits, with any
/// underscores already removed.
/// </summary>
internal static class NumberParser
{
    /// <summary>
    /// Gets the format of a normalized number text.
    /// </summary>
    public static KdlNumberFormat GetFormat(string text)
    {
        var start = SignLength(text);
        if (text.Length > start + 1 && text[start] == '0')
        {
            switch (text[start + 1])
            {
                case 'x' or 'X':
                    return KdlNumberFormat.Hexadecimal;
                case 'o' or 'O':
                    return KdlNumberFormat.Octal;
                case 'b' or 'B':
                    return KdlNumberFormat.Binary;
            }
        }

        return KdlNumberFormat.Decimal;
    }

    /// <summary>
    /// Parses a normalized number text as a decimal, rounding precision beyond what it can hold.
    /// </summary>
    /// <exception cref="OverflowException">The number is outside the range of <see cref="decimal"/>.</exception>
    public static decimal ParseDecimal(string text)
    {
        if (TryParseNonDecimal(text, out var bigInt))
        {
            if (bigInt > (BigInteger)decimal.MaxValue)
                throw new OverflowException($"Number is too large to fit in decimal: {text}");
            if (bigInt < (BigInteger)decimal.MinValue)
                throw new OverflowException($"Number is too small to fit in decimal: {text}");
            return (decimal)bigInt;
        }

        return decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses a normalized number text as a double, rounding to the nearest representable value
    /// and giving an infinity beyond the range of <see cref="double"/>.
    /// </summary>
    public static double ParseDouble(string text)
    {
        if (TryParseNonDecimal(text, out var bigInt))
        {
            return (double)bigInt;
        }

        return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses a normalized number text as a float, rounding to the nearest representable value
    /// and giving an infinity beyond the range of <see cref="float"/>.
    /// </summary>
    public static float ParseSingle(string text)
    {
        if (TryParseNonDecimal(text, out var bigInt))
        {
            return (float)bigInt;
        }

        return float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses a normalized number text as a whole number, if it is one.
    /// </summary>
    /// <param name="text">The normalized number text.</param>
    /// <param name="maxDigits">Numbers with more digits than this before the decimal point fail with an
    /// <see cref="OverflowException"/>, without the value being computed.</param>
    /// <param name="result">The whole number, when successful.</param>
    /// <returns>Null on success; otherwise the exception describing why the number isn't a suitable whole
    /// number: <see cref="InvalidOperationException"/> if it has a fractional part, or
    /// <see cref="OverflowException"/> if it has too many digits.</returns>
    public static Exception? ParseWholeNumber(string text, int maxDigits, out BigInteger result)
    {
        result = BigInteger.Zero;
        if (TryParseNonDecimal(text, out var nonDecimal))
        {
            if (DigitCount(nonDecimal) > maxDigits)
            {
                return new OverflowException($"{text} is too large.");
            }
            result = nonDecimal;
            return null;
        }

        // The value is significand * 10^exponent, with trailing zeros moved into the exponent
        var negative = text.Length > 0 && text[0] == '-';
        var body = text.Substring(SignLength(text));
        var exponentIndex = body.IndexOfAny(new[] { 'e', 'E' });
        var mantissa = exponentIndex < 0 ? body : body.Substring(0, exponentIndex);
        var pointIndex = mantissa.IndexOf('.');
        var fractionLength = pointIndex < 0 ? 0 : mantissa.Length - pointIndex - 1;
        var digits = mantissa.Replace(".", "").TrimStart('0');
        if (digits.Length == 0)
        {
            return null;
        }

        var exponent = exponentIndex < 0
            ? BigInteger.Zero
            : BigInteger.Parse(body.Substring(exponentIndex + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        exponent -= fractionLength;

        var trimmed = digits.TrimEnd('0');
        exponent += digits.Length - trimmed.Length;
        if (exponent < 0)
        {
            return new InvalidOperationException($"{text} isn't a whole number.");
        }
        if (trimmed.Length + exponent > maxDigits)
        {
            return new OverflowException($"{text} is too large.");
        }

        result = BigInteger.Parse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture) * BigInteger.Pow(10, (int)exponent);
        if (negative)
        {
            result = -result;
        }
        return null;
    }

    /// <summary>
    /// Gets the canonical form of a normalized number text, as in the official test suite's expected output:
    /// non-decimal formats become decimal integers, a leading '+' and leading zeros are dropped, the fraction
    /// is kept as written, and an exponent is written as 'E' with an explicit sign and no leading zeros.
    /// </summary>
    public static string ToCanonical(string text)
    {
        if (TryParseNonDecimal(text, out var integer))
        {
            return integer.ToString(CultureInfo.InvariantCulture);
        }

        var negative = text.Length > 0 && text[0] == '-';
        var body = text.Substring(SignLength(text));
        var exponentIndex = body.IndexOfAny(new[] { 'e', 'E' });
        var mantissa = exponentIndex < 0 ? body : body.Substring(0, exponentIndex);
        var pointIndex = mantissa.IndexOf('.');
        var integerPart = (pointIndex < 0 ? mantissa : mantissa.Substring(0, pointIndex)).TrimStart('0');

        var builder = new StringBuilder();
        if (negative)
        {
            builder.Append('-');
        }
        builder.Append(integerPart.Length == 0 ? "0" : integerPart);
        if (pointIndex >= 0)
        {
            builder.Append(mantissa, pointIndex, mantissa.Length - pointIndex);
        }
        if (exponentIndex >= 0)
        {
            var exponent = body.Substring(exponentIndex + 1);
            var exponentDigits = exponent.Substring(SignLength(exponent)).TrimStart('0');
            builder.Append('E')
                .Append(exponent.Length > 0 && exponent[0] == '-' ? '-' : '+')
                .Append(exponentDigits.Length == 0 ? "0" : exponentDigits);
        }
        return builder.ToString();
    }

    /// <summary>
    /// Gets the normalized number text for a constructed decimal or floating-point value, from its invariant
    /// text: scientific notation with all its significant digits outside the range 1e-4 to 1e10, as KDL has
    /// always written such numbers, and the text as given within it.
    /// </summary>
    /// <param name="text">The value's invariant text, such as a decimal's <c>ToString()</c> or a double's
    /// round-trip text.</param>
    /// <param name="isFloatingPoint">Whether a whole number within the range gets <c>.0</c>, so that it doesn't
    /// read back as an integer.</param>
    public static string FormatConstructed(string text, bool isFloatingPoint)
    {
        var negative = text.Length > 0 && text[0] == '-';
        var body = text.Substring(SignLength(text));
        var exponentIndex = body.IndexOfAny(new[] { 'e', 'E' });
        var mantissa = exponentIndex < 0 ? body : body.Substring(0, exponentIndex);
        var exponent = exponentIndex < 0 ? 0 : int.Parse(body.Substring(exponentIndex + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var pointIndex = mantissa.IndexOf('.');
        var allDigits = mantissa.Replace(".", "");
        var integerLength = pointIndex < 0 ? mantissa.Length : pointIndex;

        var digits = allDigits.TrimStart('0');
        integerLength -= allDigits.Length - digits.Length;

        // The exponent of the leading significant digit, as in scientific notation
        var magnitude = integerLength + exponent - 1;
        if (digits.Length == 0 || (magnitude >= -4 && magnitude < 10))
        {
            var plain = isFloatingPoint && text.IndexOfAny(new[] { '.', 'E', 'e' }) < 0 ? text + ".0" : text;
            return ToCanonical(plain);
        }

        var significant = digits.TrimEnd('0');
        var builder = new StringBuilder();
        if (negative)
        {
            builder.Append('-');
        }
        builder.Append(significant[0]);
        if (significant.Length > 1)
        {
            builder.Append('.').Append(significant, 1, significant.Length - 1);
        }
        builder.Append('E').Append(magnitude < 0 ? '-' : '+').Append(Math.Abs(magnitude).ToString(CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    /// <summary>
    /// Formats an integer in the given format, with its prefix for hex, octal and binary.
    /// </summary>
    public static string FormatInteger(BigInteger value, KdlNumberFormat format)
    {
        var (radix, prefix) = format switch
        {
            KdlNumberFormat.Decimal => (10, ""),
            KdlNumberFormat.Hexadecimal => (16, "0x"),
            KdlNumberFormat.Octal => (8, "0o"),
            KdlNumberFormat.Binary => (2, "0b"),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Integers can only be formatted as decimal, hexadecimal, octal or binary.")
        };
        if (radix == 10)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        var magnitude = BigInteger.Abs(value);
        var digits = new StringBuilder();
        do
        {
            magnitude = BigInteger.DivRem(magnitude, radix, out var digit);
            digits.Insert(0, "0123456789ABCDEF"[(int)digit]);
        }
        while (!magnitude.IsZero);

        return (value.Sign < 0 ? "-" : "") + prefix + digits;
    }

    /// <summary>
    /// Parses a number text that may be in hex (0x), octal (0o), or binary (0b) format, with an optional sign.
    /// </summary>
    /// <returns>True if the number was in a non-decimal format and was parsed; false if decimal parsing should be used.</returns>
    private static bool TryParseNonDecimal(string text, out BigInteger value)
    {
        value = BigInteger.Zero;

        var format = GetFormat(text);
        var radix = format switch
        {
            KdlNumberFormat.Hexadecimal => 16,
            KdlNumberFormat.Octal => 8,
            KdlNumberFormat.Binary => 2,
            _ => 0
        };
        if (radix == 0)
        {
            return false;
        }

        var signLength = SignLength(text);
        foreach (var c in text.Substring(signLength + 2))
        {
            if (c == '_')
                continue;
            value = value * radix + HexDigitValue(c);
        }

        if (signLength == 1 && text[0] == '-')
        {
            value = -value;
        }

        return true;
    }

    private static int DigitCount(BigInteger value) => BigInteger.Abs(value).ToString(CultureInfo.InvariantCulture).Length;

    private static int SignLength(string text) => text.Length > 0 && (text[0] == '-' || text[0] == '+') ? 1 : 0;

    private static int HexDigitValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => throw new FormatException($"Invalid digit: '{c}'")
    };
}
