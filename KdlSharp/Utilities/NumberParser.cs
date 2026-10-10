using System.Globalization;
using System.Numerics;
using KdlSharp.Values;

namespace KdlSharp.Utilities;

/// <summary>
/// Provides shared number parsing utilities for the normalized number text produced by the lexer:
/// an optional sign, then either decimal digits (with optional fraction and exponent) or a hex (0x),
/// octal (0o) or binary (0b) prefix and digits, with any underscores already removed.
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
    /// Parses a normalized number text as a decimal.
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

    private static int SignLength(string text) => text.Length > 0 && (text[0] == '-' || text[0] == '+') ? 1 : 0;

    private static int HexDigitValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => throw new FormatException($"Invalid digit: '{c}'")
    };
}
