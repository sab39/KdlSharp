using System.Globalization;
using System.Numerics;
using AwesomeAssertions;
using KdlSharp;
using KdlSharp.Formatting;
using KdlSharp.Settings;
using KdlSharp.Values;
using Xunit;

namespace KdlSharp.Tests;

public class KdlNumberConversionTests
{
    private static readonly KdlParserSettings preserveSourceText = new() { PreserveSourceText = true };

    // Canonical form follows the official test suite's expected_kdl files
    [Theory]
    [InlineData("0", "0")]
    [InlineData("1_0", "10")]
    [InlineData("+10", "10")]
    [InlineData("-10", "-10")]
    [InlineData("011", "11")]
    [InlineData("12345678901234", "12345678901234")]
    [InlineData("340282366920938463463374607431768211456", "340282366920938463463374607431768211456")]
    [InlineData("0x10", "16")]
    [InlineData("0x01", "1")]
    [InlineData("-0x10", "-16")]
    [InlineData("+0x10", "16")]
    [InlineData("0xABCDEF0123456789abcdef", "207698809136909011942886895")]
    [InlineData("0o76543210", "16434824")]
    [InlineData("-0o20", "-16")]
    [InlineData("0b10", "2")]
    [InlineData("-0b10000", "-16")]
    [InlineData("0.0", "0.0")]
    [InlineData("1.0", "1.0")]
    [InlineData("-10.0", "-10.0")]
    [InlineData("15.7", "15.7")]
    [InlineData("1_1.0", "11.0")]
    [InlineData("1.0_2", "1.02")]
    [InlineData("1e10", "1E+10")]
    [InlineData("1e29", "1E+29")]
    [InlineData("1e309", "1E+309")]
    [InlineData("1.0e10", "1.0E+10")]
    [InlineData("1.0e+10", "1.0E+10")]
    [InlineData("1.0e-10", "1.0E-10")]
    [InlineData("2.5E10", "2.5E+10")]
    [InlineData("1.2345678901e+15", "1.2345678901E+15")]
    [InlineData("1.23E+1000", "1.23E+1000")]
    [InlineData("1.23E-1000", "1.23E-1000")]
    [InlineData("1.0e-10_0", "1.0E-100")]
    [InlineData("1e05", "1E+5")]
    [InlineData("1.0E-05", "1.0E-5")]
    [InlineData("1e00", "1E+0")]
    [InlineData("#inf", "#inf")]
    [InlineData("#-inf", "#-inf")]
    [InlineData("#nan", "#nan")]
    public void Parse_Number_ToKdlStringIsCanonicalForm(string literal, string canonical)
    {
        foreach (var settings in new[] { new KdlParserSettings(), preserveSourceText })
        {
            var value = (KdlNumber)KdlDocument.Parse($"node {literal}", settings).Nodes[0].Arguments[0];
            value.ToKdlString().Should().Be(canonical);
        }
    }

    [Theory]
    [InlineData("1_000.5", "1000.5")]
    [InlineData("1e300", "1E+300")]
    [InlineData("9223372036854775807", "9223372036854775807")]
    [InlineData("0.000012345", "0.000012345")]
    [InlineData("-0x10", "-16")]
    public void Serialize_ParsedNumber_WritesCanonicalForm(string literal, string canonical)
    {
        var doc = KdlDocument.Parse($"node {literal}");

        var result = new KdlFormatter().Serialize(doc);

        result.Should().Be($"node {canonical}\n");
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(42, "42")]
    [InlineData(-5, "-5")]
    [InlineData(int.MaxValue, "2147483647")]
    [InlineData(int.MinValue, "-2147483648")]
    public void Construct_Int32_CanonicalText(int value, string canonical)
        => AssertCanonical(new KdlNumber(value), canonical);

    [Theory]
    [InlineData(long.MaxValue, "9223372036854775807")]
    [InlineData(long.MinValue, "-9223372036854775808")]
    public void Construct_Int64_CanonicalText(long value, string canonical)
        => AssertCanonical(new KdlNumber(value), canonical);

    [Fact]
    public void Construct_UInt64_CanonicalText()
        => AssertCanonical(new KdlNumber(ulong.MaxValue), "18446744073709551615");

    [Theory]
    [InlineData("340282366920938463463374607431768211456")]
    [InlineData("-340282366920938463463374607431768211456")]
    public void Construct_BigInteger_CanonicalText(string digits)
        => AssertCanonical(new KdlNumber(BigInteger.Parse(digits, CultureInfo.InvariantCulture)), digits);

    // From 1e-4 up to 1e10 a decimal is written as is, keeping its scale; outside that range, in scientific
    // notation with all its significant digits
    [Theory]
    [InlineData("1.50", "1.50")]
    [InlineData("0", "0")]
    [InlineData("0.00", "0.00")]
    [InlineData("42", "42")]
    [InlineData("-2.5", "-2.5")]
    [InlineData("0.0001", "0.0001")]
    [InlineData("9999999999.50", "9999999999.50")]
    [InlineData("0.00009", "9E-5")]
    [InlineData("10000000000", "1E+10")]
    [InlineData("-10000000000", "-1E+10")]
    [InlineData("15000000000.50", "1.50000000005E+10")]
    [InlineData("12345678901234", "1.2345678901234E+13")]
    [InlineData("0.00000000000000000001", "1E-20")]
    [InlineData("79228162514264337593543950335", "7.9228162514264337593543950335E+28")]
    public void Construct_Decimal_CanonicalText(string text, string canonical)
        => AssertCanonical(new KdlNumber(decimal.Parse(text, CultureInfo.InvariantCulture)), canonical);

    [Theory]
    [InlineData(1.0, "1.0")]
    [InlineData(-1.0, "-1.0")]
    [InlineData(0.0, "0.0")]
    [InlineData(1.5, "1.5")]
    [InlineData(0.1, "0.1")]
    [InlineData(0.0001, "0.0001")]
    [InlineData(9e-5, "9E-5")]
    [InlineData(9999999999.5, "9999999999.5")]
    [InlineData(1e10, "1E+10")]
    [InlineData(123456789012345.0, "1.23456789012345E+14")]
    [InlineData(1.2345678901234568E+17, "1.2345678901234568E+17")]
    [InlineData(1e300, "1E+300")]
    [InlineData(-1e300, "-1E+300")]
    [InlineData(1e-5, "1E-5")]
    [InlineData(1.7976931348623157E+308, "1.7976931348623157E+308")]
    [InlineData(5E-324, "5E-324")]
    public void Construct_Double_CanonicalText(double value, string canonical)
        => AssertCanonical(new KdlNumber(value), canonical);

    [Theory]
    [InlineData(1.0f, "1.0")]
    [InlineData(1.5f, "1.5")]
    [InlineData(0.1f, "0.1")]
    [InlineData(3.4028235E+38f, "3.4028235E+38")]
    public void Construct_Single_CanonicalText(float value, string canonical)
        => AssertCanonical(new KdlNumber(value), canonical);

    [Theory]
    [InlineData(double.PositiveInfinity, "#inf")]
    [InlineData(double.NegativeInfinity, "#-inf")]
    [InlineData(double.NaN, "#nan")]
    public void Construct_SpecialDouble_IsSpecialValue(double value, string kdl)
    {
        var number = new KdlNumber(value);

        number.IsSpecial.Should().BeTrue();
        number.ToKdlString().Should().Be(kdl);
        new KdlNumber((float)value).ToKdlString().Should().Be(kdl);
    }

    [Fact]
    public void Construct_IntegerWithFormat_WritesFormatWhenPreserved()
    {
        var doc = new KdlDocument();
        doc.Nodes.Add(new KdlNode("node")
            .AddArgument(new KdlNumber(255, KdlNumberFormat.Hexadecimal))
            .AddArgument(new KdlNumber(493L, KdlNumberFormat.Octal))
            .AddArgument(new KdlNumber(new BigInteger(10), KdlNumberFormat.Binary)));

        new KdlFormatter(new KdlFormatterSettings { PreserveNumberFormats = true }).Serialize(doc)
            .Should().Be("node 0xFF 0o755 0b1010\n");
        new KdlFormatter().Serialize(doc).Should().Be("node 255 493 10\n");
    }

    [Theory]
    [InlineData(1e300, "1E+300")]
    [InlineData(1.0, "1.0")]
    [InlineData(0.000012345, "1.2345E-5")]
    public void Serialize_ConstructedDouble_WritesCanonicalForm(double value, string canonical)
    {
        var doc = new KdlDocument();
        doc.Nodes.Add(new KdlNode("node").AddArgument(new KdlNumber(value)));

        new KdlFormatter().Serialize(doc).Should().Be($"node {canonical}\n");
    }

    [Theory]
    [InlineData("42", 42)]
    [InlineData("-0x10", -16)]
    [InlineData("1.0", 1)]
    [InlineData("1e3", 1000)]
    [InlineData("2147483647", int.MaxValue)]
    public void ToInt32_WholeNumber_ReturnsValue(string literal, int expected)
    {
        var number = Parse(literal);

        number.ToInt32().Should().Be(expected);
        number.TryGetInt32(out var result).Should().BeTrue();
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("1e-3")]
    [InlineData("#inf")]
    [InlineData("#nan")]
    public void ToInt32_NotAWholeNumber_ThrowsInvalidOperation(string literal)
    {
        var number = Parse(literal);

        number.Invoking(n => n.ToInt32()).Should().Throw<InvalidOperationException>();
        number.TryGetInt32(out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("2147483648")]
    [InlineData("-2147483649")]
    [InlineData("1e10")]
    public void ToInt32_OutOfRange_ThrowsOverflow(string literal)
    {
        var number = Parse(literal);

        number.Invoking(n => n.ToInt32()).Should().Throw<OverflowException>();
        number.TryGetInt32(out _).Should().BeFalse();
    }

    [Fact]
    public void ToInt64_ChecksRange()
    {
        Parse("9223372036854775807").ToInt64().Should().Be(long.MaxValue);
        Parse("-9223372036854775808").ToInt64().Should().Be(long.MinValue);
        Parse("9223372036854775808").Invoking(n => n.ToInt64()).Should().Throw<OverflowException>();
        Parse("9223372036854775808").TryGetInt64(out _).Should().BeFalse();
        Parse("1.5").Invoking(n => n.ToInt64()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ToUInt64_ChecksRange()
    {
        Parse("18446744073709551615").ToUInt64().Should().Be(ulong.MaxValue);
        Parse("0xFFFF_FFFF_FFFF_FFFF").ToUInt64().Should().Be(ulong.MaxValue);
        Parse("-1").Invoking(n => n.ToUInt64()).Should().Throw<OverflowException>();
        Parse("18446744073709551616").TryGetUInt64(out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("340282366920938463463374607431768211456", "340282366920938463463374607431768211456")]
    [InlineData("0x1_0000_0000_0000_0000_0000_0000_0000_0000", "340282366920938463463374607431768211456")]
    [InlineData("-0b10000", "-16")]
    [InlineData("1e30", "1000000000000000000000000000000")]
    [InlineData("1.5e1", "15")]
    public void ToBigInteger_WholeNumber_ReturnsValue(string literal, string expected)
    {
        var number = Parse(literal);
        var expectedValue = BigInteger.Parse(expected, CultureInfo.InvariantCulture);

        number.ToBigInteger().Should().Be(expectedValue);
        number.TryGetBigInteger(out var result).Should().BeTrue();
        result.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("1e-30")]
    [InlineData("#-inf")]
    public void ToBigInteger_NotAWholeNumber_ThrowsInvalidOperation(string literal)
    {
        var number = Parse(literal);

        number.Invoking(n => n.ToBigInteger()).Should().Throw<InvalidOperationException>();
        number.TryGetBigInteger(out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("1e300")]
    [InlineData("-1e308")]
    [InlineData("1e-320")]
    [InlineData("0.1")]
    [InlineData("1.00000000000000011102230246251565404236316680908203125")]
    [InlineData("340282366920938463463374607431768211456")]
    public void ToDouble_ReturnsNearestDouble(string literal)
    {
        var expected = double.Parse(literal, NumberStyles.Float, CultureInfo.InvariantCulture);

        Parse(literal).ToDouble().Should().Be(expected);
    }

    [Fact]
    public void ToDouble_NonDecimalFormat_ReturnsValue()
    {
        Parse("0xFF").ToDouble().Should().Be(255);
        Parse("-0o20").ToDouble().Should().Be(-16);
    }

    [Theory]
    [InlineData("1e309", double.PositiveInfinity)]
    [InlineData("-1e309", double.NegativeInfinity)]
    [InlineData("#inf", double.PositiveInfinity)]
    [InlineData("#-inf", double.NegativeInfinity)]
    public void ToDouble_BeyondRangeOrInfinite_ReturnsInfinity(string literal, double expected)
        => Parse(literal).ToDouble().Should().Be(expected);

    [Fact]
    public void ToDouble_NaN_ReturnsNaN()
        => double.IsNaN(Parse("#nan").ToDouble()).Should().BeTrue();

    [Theory]
    [InlineData("1.5", 1.5f)]
    [InlineData("0.1", 0.1f)]
    [InlineData("3.4028235e38", float.MaxValue)]
    [InlineData("1e39", float.PositiveInfinity)]
    [InlineData("#-inf", float.NegativeInfinity)]
    public void ToSingle_ReturnsNearestSingle(string literal, float expected)
        => Parse(literal).ToSingle().Should().Be(expected);

    [Theory]
    [InlineData("1.50", "1.50")]
    [InlineData("-0x10", "-16")]
    [InlineData("1e28", "10000000000000000000000000000")]
    [InlineData("0.1234567890123456789012345678901", "0.1234567890123456789012345679")]
    public void ToDecimal_WithinRange_ReturnsValue(string literal, string expected)
    {
        var number = Parse(literal);
        var expectedValue = decimal.Parse(expected, CultureInfo.InvariantCulture);

        number.ToDecimal().Should().Be(expectedValue);
        number.TryGetDecimal(out var result).Should().BeTrue();
        result.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("1e29")]
    [InlineData("-1e29")]
    [InlineData("340282366920938463463374607431768211456")]
    public void ToDecimal_OutOfRange_ThrowsOverflow(string literal)
    {
        var number = Parse(literal);

        number.Invoking(n => n.ToDecimal()).Should().Throw<OverflowException>();
        number.TryGetDecimal(out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("#inf")]
    [InlineData("#nan")]
    public void ToDecimal_SpecialValue_ThrowsInvalidOperation(string literal)
    {
        var number = Parse(literal);

        number.Invoking(n => n.ToDecimal()).Should().Throw<InvalidOperationException>();
        number.TryGetDecimal(out _).Should().BeFalse();
    }

    [Fact]
    public void Constructed_GettersRoundTrip()
    {
        new KdlNumber(int.MinValue).ToInt32().Should().Be(int.MinValue);
        new KdlNumber(long.MaxValue).ToInt64().Should().Be(long.MaxValue);
        new KdlNumber(ulong.MaxValue).ToUInt64().Should().Be(ulong.MaxValue);

        var big = BigInteger.Pow(2, 200);
        new KdlNumber(big).ToBigInteger().Should().Be(big);
        new KdlNumber(-big).ToBigInteger().Should().Be(-big);

        new KdlNumber(0.1).ToDouble().Should().Be(0.1);
        new KdlNumber(double.MaxValue).ToDouble().Should().Be(double.MaxValue);
        new KdlNumber(double.Epsilon).ToDouble().Should().Be(double.Epsilon);
        new KdlNumber(0.1f).ToSingle().Should().Be(0.1f);

        new KdlNumber(1.50m).ToDecimal().Should().Be(1.50m);
        new KdlNumber(1.0).ToInt32().Should().Be(1);
        new KdlNumber(1e300).Invoking(n => n.ToDecimal()).Should().Throw<OverflowException>();
        double.IsNaN(new KdlNumber(double.NaN).ToDouble()).Should().BeTrue();
    }

    [Fact]
    public void Constructed_WrittenAndParsedBack_HasSameValue()
    {
        var doc = new KdlDocument();
        doc.Nodes.Add(new KdlNode("node")
            .AddArgument(new KdlNumber(long.MaxValue))
            .AddArgument(new KdlNumber(BigInteger.Pow(10, 40)))
            .AddArgument(new KdlNumber(1e300))
            .AddArgument(new KdlNumber(0.1))
            .AddArgument(new KdlNumber(1.0)));

        var arguments = KdlDocument.Parse(new KdlFormatter().Serialize(doc), preserveSourceText).Nodes[0].Arguments.Cast<KdlNumber>().ToList();

        arguments[0].ToInt64().Should().Be(long.MaxValue);
        arguments[1].ToBigInteger().Should().Be(BigInteger.Pow(10, 40));
        arguments[2].ToDouble().Should().Be(1e300);
        arguments[3].ToDouble().Should().Be(0.1);
        arguments[4].RawText.Should().Be("1.0");
    }

    private static KdlNumber Parse(string literal) => Parse(literal, new KdlParserSettings());

    private static KdlNumber Parse(string literal, KdlParserSettings settings)
        => (KdlNumber)KdlDocument.Parse($"node {literal}", settings).Nodes[0].Arguments[0];

    private static void AssertCanonical(KdlNumber number, string canonical)
    {
        number.ToKdlString().Should().Be(canonical);
        number.RawText.Should().Be(canonical);
    }
}
