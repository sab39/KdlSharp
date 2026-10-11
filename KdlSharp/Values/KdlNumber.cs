using KdlSharp.Utilities;

namespace KdlSharp.Values;

/// <summary>
/// Represents a KDL number value.
/// </summary>
public sealed class KdlNumber : KdlValue
{
    /// <summary>
    /// Gets the numeric value as a <see cref="decimal"/>.
    /// </summary>
    /// <remarks>
    /// For special values (infinity, NaN), accessing this property will throw.
    /// Use <see cref="IsSpecial"/>, <see cref="IsPositiveInfinity"/>, <see cref="IsNegativeInfinity"/>,
    /// or <see cref="IsNaN"/> to check for special values, or use <see cref="AsDoubleValue"/> which properly
    /// handles all IEEE 754 special values.
    /// For parsed numbers that exceed decimal range, this throws <see cref="OverflowException"/> when accessed.
    /// Precision beyond what <see cref="decimal"/> can hold is rounded.
    /// Use <see cref="AsDoubleValue"/> for the nearest double, or <see cref="RawText"/> to get the original representation.
    /// </remarks>
    public decimal Value
    {
        get
        {
            if (Format == KdlNumberFormat.Infinity || Format == KdlNumberFormat.NaN)
            {
                throw new InvalidOperationException(
                    $"Cannot represent {Format} as decimal. Use AsDouble() for special values.");
            }

            if (value.HasValue)
                return value.Value;

            // Lazy parse from raw text
            value = ParseFromRawText();
            return value.Value;
        }
    }

    private decimal? value;
    private readonly double? specialValue;

    /// <summary>
    /// Gets the original format used when parsing this number.
    /// </summary>
    public KdlNumberFormat Format { get; }

    /// <summary>
    /// Gets the original text representation of this number.
    /// </summary>
    /// <remarks>
    /// For a number parsed with <see cref="Settings.KdlParserSettings.PreserveSourceText"/>, this is the exact
    /// source text. Otherwise it may be null, or differ from the source text.
    /// </remarks>
    public string? RawText => rawText ??= SourceText?.RawText ?? valueText;

    private string? rawText;

    // Text a parsed number's value is computed from: the source text without underscores.
    private readonly string? valueText;

    /// <summary>
    /// Gets whether this number is a special value (infinity or NaN).
    /// </summary>
    public bool IsSpecial => Format == KdlNumberFormat.Infinity || Format == KdlNumberFormat.NaN;

    /// <summary>
    /// Gets whether this number is positive infinity.
    /// </summary>
    public bool IsPositiveInfinity => Format == KdlNumberFormat.Infinity && specialValue > 0;

    /// <summary>
    /// Gets whether this number is negative infinity.
    /// </summary>
    public bool IsNegativeInfinity => Format == KdlNumberFormat.Infinity && specialValue < 0;

    /// <summary>
    /// Gets whether this number is NaN (not a number).
    /// </summary>
    public bool IsNaN => Format == KdlNumberFormat.NaN;

    /// <summary>
    /// Gets the value type (Number).
    /// </summary>
    public override KdlValueType ValueType => KdlValueType.Number;

    /// <summary>
    /// Initializes a new number value.
    /// </summary>
    public KdlNumber(decimal value, KdlNumberFormat format = KdlNumberFormat.Decimal)
    {
        this.value = value;
        Format = format;
        valueText = null;
        specialValue = null;
    }

    /// <summary>
    /// Initializes a new number value from normalized number text, as produced by the lexer.
    /// </summary>
    internal KdlNumber(string rawText, KdlNumberFormat format)
    {
        valueText = rawText ?? throw new ArgumentNullException(nameof(rawText));
        Format = format;
        value = null; // Parse lazily
        specialValue = null;
    }

    /// <summary>
    /// Initializes a new special number value (infinity or NaN).
    /// </summary>
    private KdlNumber(double special, KdlNumberFormat format)
    {
        specialValue = special;
        Format = format;
        valueText = null;
        value = null;
    }

    /// <summary>
    /// Creates a positive infinity value.
    /// </summary>
    public static KdlNumber PositiveInfinity() => new KdlNumber(double.PositiveInfinity, KdlNumberFormat.Infinity);

    /// <summary>
    /// Creates a negative infinity value.
    /// </summary>
    public static KdlNumber NegativeInfinity() => new KdlNumber(double.NegativeInfinity, KdlNumberFormat.Infinity);

    /// <summary>
    /// Creates a NaN (not-a-number) value.
    /// </summary>
    public static KdlNumber NaN() => new KdlNumber(double.NaN, KdlNumberFormat.NaN);

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
    public double? AsDoubleValue()
    {
        if (IsSpecial)
            return specialValue;
        if (valueText != null)
            return NumberParser.ParseDouble(valueText);
        return (double)Value;
    }

    /// <summary>
    /// Converts this value to a KDL string representation.
    /// </summary>
    /// <returns>The KDL string representation.</returns>
    public override string ToKdlString()
    {
        // Handle special values first
        if (Format == KdlNumberFormat.Infinity)
        {
            return specialValue < 0 ? "#-inf" : "#inf";
        }
        if (Format == KdlNumberFormat.NaN)
        {
            return "#nan";
        }

        // Use raw text if available and format-preserving
        if (RawText != null && Format != KdlNumberFormat.Decimal)
        {
            return RawText;
        }

        return Format switch
        {
            KdlNumberFormat.Decimal => FormatDecimal(),
            KdlNumberFormat.Hexadecimal => TryConvertToInt64() is long hexVal ? $"0x{hexVal:X}" : Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            KdlNumberFormat.Octal => TryConvertToInt64() is long octVal ? $"0o{Convert.ToString(octVal, 8)}" : Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            KdlNumberFormat.Binary => TryConvertToInt64() is long binVal ? $"0b{Convert.ToString(binVal, 2)}" : Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    private string FormatDecimal()
    {
        // Beyond decimal's range, the normalized text is still valid KDL
        return TryGetDecimalValue(out var decimalValue)
            ? decimalValue.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : valueText!;
    }

    /// <summary>
    /// Gets the value as a decimal, unless this is a special value or a parsed number beyond decimal range.
    /// </summary>
    internal bool TryGetDecimalValue(out decimal decimalValue)
    {
        decimalValue = 0;
        if (IsSpecial)
            return false;

        try
        {
            decimalValue = Value;
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private long? TryConvertToInt64()
    {
        try
        {
            return Convert.ToInt64(Value);
        }
        catch
        {
            return null;
        }
    }

    private decimal ParseFromRawText()
    {
        if (valueText == null)
            throw new InvalidOperationException("Cannot parse number without raw text");

        return NumberParser.ParseDecimal(valueText);
    }

    /// <summary>
    /// Creates a deep clone of this value.
    /// </summary>
    /// <returns>A new KdlNumber with the same value and format.</returns>
    public override KdlValue Clone()
    {
        if (IsSpecial)
        {
            return new KdlNumber(specialValue!.Value, Format) { TypeAnnotation = TypeAnnotation };
        }
        if (valueText != null)
        {
            var clone = new KdlNumber(valueText, Format) { TypeAnnotation = TypeAnnotation };
            if (value.HasValue)
                clone.value = value;
            return clone;
        }
        return new KdlNumber(value ?? Value, Format) { TypeAnnotation = TypeAnnotation };
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

