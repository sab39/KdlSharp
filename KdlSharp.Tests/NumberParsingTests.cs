using System.Globalization;
using AwesomeAssertions;
using KdlSharp;
using KdlSharp.Extensions;
using KdlSharp.Settings;
using KdlSharp.Values;
using Xunit;

namespace KdlSharp.Tests;

public class NumberParsingTests
{
    private static readonly KdlParserSettings preserveSourceText = new() { PreserveSourceText = true };

    [Theory]
    [InlineData("123")]
    [InlineData("-123")]
    [InlineData("+123")]
    [InlineData("1_000_000")]
    [InlineData("1.5")]
    [InlineData("1_000.000_5")]
    [InlineData("1.5e10")]
    [InlineData("1.5E+10")]
    [InlineData("1e-30")]
    [InlineData("1e29")]
    [InlineData("1e309")]
    [InlineData("340282366920938463463374607431768211456")]
    [InlineData("0.1234567890123456789012345678901")]
    [InlineData("0xFF")]
    [InlineData("0xff_ff")]
    [InlineData("-0x10")]
    [InlineData("0o755")]
    [InlineData("-0o20")]
    [InlineData("0b1010")]
    [InlineData("-0b10000")]
    [InlineData("#inf")]
    [InlineData("#-inf")]
    [InlineData("#nan")]
    public void Parse_Number_WithPreserveSourceText_RawTextIsSourceText(string literal)
    {
        var kdl = $"node {literal}";
        var doc = KdlDocument.Parse(kdl, preserveSourceText);

        var value = (KdlNumber)doc.Nodes[0].Arguments[0];
        value.RawText.Should().Be(literal);
    }

    [Theory]
    [InlineData("node 1_000", "1_000")]
    [InlineData("node key=1_000", "1_000")]
    [InlineData("node (u8)0xff", "0xff")]
    [InlineData("node key=(i32)-0x10", "-0x10")]
    [InlineData("node \"a\" 1e29 \"b\"", "1e29")]
    [InlineData("node {\n    child 1.5e10\n}", "1.5e10")]
    [InlineData("node #-inf", "#-inf")]
    public void Parse_Number_WithPreserveSourceText_RawTextIsAtSourcePosition(string kdl, string literal)
    {
        var doc = KdlDocument.Parse(kdl, preserveSourceText);

        var value = doc.AllNodes().SelectMany(n => n.Arguments.Concat(n.Properties.Select(p => p.Value)))
            .OfType<KdlNumber>()
            .Single();
        value.RawText.Should().Be(literal);
        value.SourcePosition.Should().NotBeNull();
        value.SourcePosition!.Length.Should().Be(literal.Length);
        kdl.Substring(value.SourcePosition.Offset, value.SourcePosition.Length).Should().Be(literal);
    }

    [Theory]
    [InlineData("1e29")]
    [InlineData("1e308")]
    [InlineData("-1e308")]
    [InlineData("1e309")]
    [InlineData("-1e400")]
    [InlineData("340282366920938463463374607431768211456")]
    [InlineData("1e-30")]
    [InlineData("1e-400")]
    [InlineData("0.1234567890123456789012345678901")]
    [InlineData("1.00000000000000011102230246251565404236316680908203125")]
    [InlineData("1_000.000_5")]
    public void Parse_DecimalNumber_AsDoubleValueMatchesDoubleParse(string literal)
    {
        var kdl = $"node {literal}";
        var doc = KdlDocument.Parse(kdl);

        var expected = double.Parse(literal.Replace("_", ""), NumberStyles.Float, CultureInfo.InvariantCulture);
        var value = (KdlNumber)doc.Nodes[0].Arguments[0];
        value.AsDoubleValue().Should().Be(expected);
    }

    [Theory]
    [InlineData("-0x10")]
    [InlineData("-0o20")]
    [InlineData("-0b10000")]
    public void Parse_NegativeNonDecimalNumber_HasNegativeValue(string literal)
    {
        var kdl = $"node {literal}";
        var doc = KdlDocument.Parse(kdl);

        var value = (KdlNumber)doc.Nodes[0].Arguments[0];
        value.Value.Should().Be(-16m);
        value.AsNumber().Should().Be(-16m);
        value.AsDoubleValue().Should().Be(-16d);
    }
}
