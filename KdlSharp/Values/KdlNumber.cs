using System.Globalization;
using System.Numerics;
using KdlSharp.Utilities;

namespace KdlSharp.Values;

/// <summary>
/// Represents a KDL number value.
/// </summary>
/// <remarks>
/// A number is held as text, either as parsed or as generated from the value it was constructed from, so it
/// keeps its full range and precision. The conversion methods, such as <see cref="ToInt64"/>,
/// <see cref="ToBigInteger"/> and <see cref="ToDouble"/>, convert from that text.
/// </remarks>
public sealed class KdlNumber : KdlValue
{
    /// <summary>
    /// Gets the numeric value as a <see cref="decimal"/>.
    /// </summary>
    /// <remarks>
    /// The same as <see cref="ToDecimal"/>.
    /// For special values (infinity, NaN), accessing this property will throw.
    /// Use <see cref="IsSpecial"/>, <see cref="IsPositiveInfinity"/>, <see cref="IsNegativeInfinity"/>,
    /// or <see cref="IsNaN"/> to check for special values, or use <see cref="ToDouble"/> which properly
    /// handles all IEEE 754 special values.
    /// For numbers that exceed decimal range, this throws <see cref="OverflowException"/> when accessed.
    /// Precision beyond what <see cref="decimal"/> can hold is rounded.
    /// </remarks>
    public decimal Value => ToDecimal();

    private decimal? value;

    // The number as text in its format, without underscores; null for special values, and for a number
    // constructed from a decimal until ValueText first formats it
    private string? valueText;

    // The format and the flags are bytes, so that they take up no more room together than one int would
    private readonly StoredFormat format;
    private NumberFlags flags;

    private string ValueText => valueText ??= NumberParser.FormatConstructed(value!.Value.ToString(CultureInfo.InvariantCulture), isFloatingPoint: false);

    /// <summary>
    /// Gets the format of this number: as parsed, or as given when it was constructed.
    /// </summary>
    public KdlNumberFormat Format => (KdlNumberFormat)format;

    /// <summary>
    /// Gets the text representation of this number, in its own format.
    /// </summary>
    /// <remarks>
    /// For a number parsed with <see cref="Settings.KdlParserSettings.PreserveSourceText"/>, this is the exact
    /// source text. Otherwise, it may be missing or synthesized: for a parsed number, the source text without
    /// underscores, and for a constructed one, text generated from its value. Use <see cref="ToKdlString"/>
    /// for the canonical form.
    /// </remarks>
    public string? RawText => SourceText?.RawText ?? (IsSpecial ? null : ValueText);

    /// <summary>
    /// Gets whether this number is a special value (infinity or NaN).
    /// </summary>
    public bool IsSpecial => format is StoredFormat.Infinity or StoredFormat.NaN;

    /// <summary>
    /// Gets whether this number is positive infinity.
    /// </summary>
    public bool IsPositiveInfinity => format == StoredFormat.Infinity && (flags & NumberFlags.Negative) == 0;

    /// <summary>
    /// Gets whether this number is negative infinity.
    /// </summary>
    public bool IsNegativeInfinity => format == StoredFormat.Infinity && (flags & NumberFlags.Negative) != 0;

    /// <summary>
    /// Gets whether this number is NaN (not a number).
    /// </summary>
    public bool IsNaN => format == StoredFormat.NaN;

    /// <summary>
    /// Gets the value type (Number).
    /// </summary>
    public override KdlValueType ValueType => KdlValueType.Number;

    /// <summary>
    /// Initializes a new number value.
    /// </summary>
    /// <param name="value">The value. From 1e-4 up to 1e10, it's written as is, keeping its scale, so <c>1.50m</c>
    /// is written as <c>1.50</c>. Outside that range, it's written in scientific notation with all its significant
    /// digits, such as <c>1.2345E+15</c>.</param>
    /// <param name="format">The format to write the number in when number formats are preserved. Only a whole
    /// number can be hexadecimal, octal or binary.</param>
    public KdlNumber(decimal value, KdlNumberFormat format = KdlNumberFormat.Decimal)
    {
        // In decimal format, the text is formatted by ValueText when first needed
        if (format != KdlNumberFormat.Decimal)
        {
            if (decimal.Truncate(value) != value)
            {
                throw new ArgumentException("Only a whole number can be written in hexadecimal, octal or binary.", nameof(value));
            }
            valueText = NumberParser.FormatInteger(new BigInteger(value), format);
        }

        this.value = value;
        this.format = (StoredFormat)format;
        flags = CanonicalKnown(format == KdlNumberFormat.Decimal);
    }

    /// <summary>
    /// Initializes a new integer value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="format">The format to write the number in when number formats are preserved.</param>
    public KdlNumber(int value, KdlNumberFormat format = KdlNumberFormat.Decimal)
        : this(new BigInteger(value), format)
    {
    }

    /// <summary>
    /// Initializes a new integer value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="format">The format to write the number in when number formats are preserved.</param>
    public KdlNumber(long value, KdlNumberFormat format = KdlNumberFormat.Decimal)
        : this(new BigInteger(value), format)
    {
    }

    /// <summary>
    /// Initializes a new integer value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="format">The format to write the number in when number formats are preserved.</param>
    public KdlNumber(ulong value, KdlNumberFormat format = KdlNumberFormat.Decimal)
        : this(new BigInteger(value), format)
    {
    }

    /// <summary>
    /// Initializes a new integer value of any size.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="format">The format to write the number in when number formats are preserved.</param>
    public KdlNumber(BigInteger value, KdlNumberFormat format = KdlNumberFormat.Decimal)
    {
        valueText = NumberParser.FormatInteger(value, format);
        this.format = (StoredFormat)format;
        flags = CanonicalKnown(format == KdlNumberFormat.Decimal);
    }

    /// <summary>
    /// Initializes a new floating-point value. Infinities and NaN become <c>#inf</c>, <c>#-inf</c> and <c>#nan</c>.
    /// </summary>
    /// <param name="value">The value. From 1e-4 up to 1e10, it's written as is, with a whole number written with a
    /// fraction, such as <c>1.0</c>, so that it doesn't read back as an integer. Outside that range, it's written in
    /// scientific notation with all its significant digits, such as <c>1.2345E+15</c>.</param>
    public KdlNumber(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            (format, flags) = Special(float.IsNaN(value), float.IsNegativeInfinity(value));
        }
        else
        {
            valueText = NumberParser.FormatConstructed(value.ToString("R", CultureInfo.InvariantCulture), isFloatingPoint: true);
            format = StoredFormat.Decimal;
            flags = CanonicalKnown(true);
        }
    }

    /// <summary>
    /// Initializes a new floating-point value. Infinities and NaN become <c>#inf</c>, <c>#-inf</c> and <c>#nan</c>.
    /// </summary>
    /// <param name="value">The value. From 1e-4 up to 1e10, it's written as is, with a whole number written with a
    /// fraction, such as <c>1.0</c>, so that it doesn't read back as an integer. Outside that range, it's written in
    /// scientific notation with all its significant digits, such as <c>1.2345E+15</c>.</param>
    public KdlNumber(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            (format, flags) = Special(double.IsNaN(value), double.IsNegativeInfinity(value));
        }
        else
        {
            valueText = NumberParser.FormatConstructed(value.ToString("R", CultureInfo.InvariantCulture), isFloatingPoint: true);
            format = StoredFormat.Decimal;
            flags = CanonicalKnown(true);
        }
    }

    /// <summary>
    /// Initializes a new number value from normalized number text, as produced by the lexer. Whether the text is
    /// canonical is worked out when it's first written.
    /// </summary>
    internal KdlNumber(string rawText, KdlNumberFormat format)
    {
        valueText = rawText ?? throw new ArgumentNullException(nameof(rawText));
        this.format = (StoredFormat)format;
    }

    /// <summary>
    /// Creates a positive infinity value.
    /// </summary>
    public static KdlNumber PositiveInfinity() => new KdlNumber(double.PositiveInfinity);

    /// <summary>
    /// Creates a negative infinity value.
    /// </summary>
    public static KdlNumber NegativeInfinity() => new KdlNumber(double.NegativeInfinity);

    /// <summary>
    /// Creates a NaN (not-a-number) value.
    /// </summary>
    public static KdlNumber NaN() => new KdlNumber(double.NaN);

    /// <summary>
    /// Returns this value as a decimal number.
    /// </summary>
    /// <returns>The numeric value, or null for special values (infinity, NaN).</returns>
    public override decimal? AsNumber()
    {
        if (IsSpecial)
            return null;
        return Value;
    }

    /// <summary>
    /// Attempts to get this value as a double-precision floating point number.
    /// </summary>
    /// <returns>The double value, including proper IEEE 754 special values.</returns>
    public double? AsDoubleValue() => ToDouble();

    /// <summary>
    /// Gets this value as an <see cref="int"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The value isn't a whole number, or is a special value.</exception>
    /// <exception cref="OverflowException">The value is outside the range of <see cref="int"/>.</exception>
    public int ToInt32() => (int)ToWholeNumber(10, int.MinValue, int.MaxValue);

    /// <summary>
    /// Gets this value as an <see cref="int"/>, if it's a whole number within its range.
    /// </summary>
    public bool TryGetInt32(out int result)
    {
        var success = GetWholeNumber(10, int.MinValue, int.MaxValue, out var wholeNumber) == null;
        result = success ? (int)wholeNumber : 0;
        return success;
    }

    /// <summary>
    /// Gets this value as a <see cref="long"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The value isn't a whole number, or is a special value.</exception>
    /// <exception cref="OverflowException">The value is outside the range of <see cref="long"/>.</exception>
    public long ToInt64() => (long)ToWholeNumber(19, long.MinValue, long.MaxValue);

    /// <summary>
    /// Gets this value as a <see cref="long"/>, if it's a whole number within its range.
    /// </summary>
    public bool TryGetInt64(out long result)
    {
        var success = GetWholeNumber(19, long.MinValue, long.MaxValue, out var wholeNumber) == null;
        result = success ? (long)wholeNumber : 0;
        return success;
    }

    /// <summary>
    /// Gets this value as a <see cref="ulong"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The value isn't a whole number, or is a special value.</exception>
    /// <exception cref="OverflowException">The value is outside the range of <see cref="ulong"/>.</exception>
    public ulong ToUInt64() => (ulong)ToWholeNumber(20, ulong.MinValue, ulong.MaxValue);

    /// <summary>
    /// Gets this value as a <see cref="ulong"/>, if it's a whole number within its range.
    /// </summary>
    public bool TryGetUInt64(out ulong result)
    {
        var success = GetWholeNumber(20, ulong.MinValue, ulong.MaxValue, out var wholeNumber) == null;
        result = success ? (ulong)wholeNumber : 0;
        return success;
    }

    /// <summary>
    /// Gets this value as a <see cref="BigInteger"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The value isn't a whole number, or is a special value.</exception>
    /// <exception cref="OverflowException">The value has too many digits to compute.</exception>
    public BigInteger ToBigInteger() => ToWholeNumber(int.MaxValue, null, null);

    /// <summary>
    /// Gets this value as a <see cref="BigInteger"/>, if it's a whole number.
    /// </summary>
    public bool TryGetBigInteger(out BigInteger result) => GetWholeNumber(int.MaxValue, null, null, out result) == null;

    /// <summary>
    /// Gets this value as the nearest <see cref="float"/>, including infinities and NaN.
    /// </summary>
    public float ToSingle() => IsSpecial ? (float)SpecialValue : NumberParser.ParseSingle(ValueText);

    /// <summary>
    /// Gets this value as the nearest <see cref="double"/>, including infinities and NaN.
    /// </summary>
    public double ToDouble() => IsSpecial ? SpecialValue : NumberParser.ParseDouble(ValueText);

    // The value of #inf, #-inf or #nan
    private double SpecialValue => IsNaN ? double.NaN : IsNegativeInfinity ? double.NegativeInfinity : double.PositiveInfinity;

    /// <summary>
    /// Gets this value as a <see cref="decimal"/>. Precision beyond what <see cref="decimal"/> can hold is rounded.
    /// </summary>
    /// <exception cref="InvalidOperationException">The value is a special value.</exception>
    /// <exception cref="OverflowException">The value is outside the range of <see cref="decimal"/>.</exception>
    public decimal ToDecimal()
    {
        if (IsSpecial)
        {
            throw new InvalidOperationException($"Cannot represent {ToKdlString()} as decimal. Use ToDouble() for special values.");
        }

        return value ??= NumberParser.ParseDecimal(ValueText);
    }

    /// <summary>
    /// Gets this value as a <see cref="decimal"/>, if it's within its range and isn't a special value.
    /// </summary>
    public bool TryGetDecimal(out decimal result)
    {
        result = 0;
        if (IsSpecial)
        {
            return false;
        }

        try
        {
            result = ToDecimal();
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    /// <summary>
    /// Converts this value to its canonical KDL representation.
    /// </summary>
    /// <remarks>
    /// The canonical form is the one the official KDL test suite's expected output uses: hex, octal and binary
    /// become decimal integers, a leading <c>+</c>, leading zeros and underscores are dropped, a fraction is kept
    /// as written, and an exponent is written as <c>E</c> with an explicit sign, as in <c>1E+10</c>.
    /// </remarks>
    /// <returns>The KDL string representation.</returns>
    public override string ToKdlString()
    {
        if (IsSpecial)
        {
            return IsNaN ? "#nan" : IsNegativeInfinity ? "#-inf" : "#inf";
        }

        var text = ValueText;
        if ((flags & NumberFlags.CanonicalKnown) == 0)
        {
            flags |= CanonicalKnown(NumberParser.IsCanonical(text));
        }
        return (flags & NumberFlags.CanonicalText) != 0 ? text : NumberParser.ToCanonical(text);
    }

    private BigInteger ToWholeNumber(int maxDigits, BigInteger? min, BigInteger? max)
    {
        var failure = GetWholeNumber(maxDigits, min, max, out var result);
        if (failure != null)
        {
            throw failure;
        }
        return result;
    }

    /// <summary>
    /// Gets the value as a whole number within a range, or the exception explaining why it isn't one.
    /// </summary>
    private Exception? GetWholeNumber(int maxDigits, BigInteger? min, BigInteger? max, out BigInteger result)
    {
        result = BigInteger.Zero;
        if (IsSpecial)
        {
            return new InvalidOperationException($"{ToKdlString()} isn't a whole number.");
        }

        var failure = NumberParser.ParseWholeNumber(ValueText, maxDigits, out result);
        if (failure == null && (result < min || result > max))
        {
            failure = new OverflowException($"{ToKdlString()} is outside the range {min} to {max}.");
        }
        return failure;
    }

    /// <summary>
    /// Creates a deep clone of this value.
    /// </summary>
    /// <returns>A new KdlNumber with the same value and format.</returns>
    public override KdlValue Clone()
    {
        if (IsSpecial)
        {
            return new KdlNumber(SpecialValue) { TypeAnnotation = TypeAnnotation };
        }

        return new KdlNumber(ValueText, Format) { TypeAnnotation = TypeAnnotation, value = this.value, flags = this.flags };
    }

    private static NumberFlags CanonicalKnown(bool isCanonical)
    {
        return isCanonical ? NumberFlags.CanonicalKnown | NumberFlags.CanonicalText : NumberFlags.CanonicalKnown;
    }

    private static (StoredFormat Format, NumberFlags Flags) Special(bool isNaN, bool isNegativeInfinity)
    {
        return isNaN ? (StoredFormat.NaN, NumberFlags.None)
            : (StoredFormat.Infinity, isNegativeInfinity ? NumberFlags.Negative : NumberFlags.None);
    }

    // KdlNumberFormat's values, held in a byte
    private enum StoredFormat : byte
    {
        Decimal = (byte)KdlNumberFormat.Decimal,
        Hexadecimal = (byte)KdlNumberFormat.Hexadecimal,
        Octal = (byte)KdlNumberFormat.Octal,
        Binary = (byte)KdlNumberFormat.Binary,
        Infinity = (byte)KdlNumberFormat.Infinity,
        NaN = (byte)KdlNumberFormat.NaN
    }

    [Flags]
    private enum NumberFlags : byte
    {
        None = 0,

        // For infinity: it's negative infinity
        Negative = 1,

        // Whether CanonicalText has been worked out yet
        CanonicalKnown = 2,

        // The number's text is already canonical, so ToKdlString can return it as is
        CanonicalText = 4
    }
}

/// <summary>
/// Enumeration of KDL number formats.
/// </summary>
public enum KdlNumberFormat
{
    /// <summary>Decimal format (e.g., <c>123</c>, <c>1.5</c>, <c>1.5e10</c>).</summary>
    Decimal,

    /// <summary>Hexadecimal format (e.g., <c>0xFF</c>).</summary>
    Hexadecimal,

    /// <summary>Octal format (e.g., <c>0o755</c>).</summary>
    Octal,

    /// <summary>Binary format (e.g., <c>0b1010</c>).</summary>
    Binary,

    /// <summary>Infinity (<c>#inf</c> or <c>#-inf</c>).</summary>
    Infinity,

    /// <summary>Not-a-number (<c>#nan</c>).</summary>
    NaN
}
