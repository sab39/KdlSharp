using AwesomeAssertions;
using KdlSharp;
using KdlSharp.Exceptions;
using KdlSharp.Settings;
using Xunit;

namespace KdlSharp.Tests;

// The parser decides which identifier strings look like numbers by the same rule the formatter uses to decide what
// it can write bare: see KdlFormatterTests.IdentifierEdgeCases.
public class IdentifierParsingTests
{
    // Per the spec's identifier-string grammar, after an optional sign and then an optional '.',
    // the next character can't be a digit.
    [Theory]
    [InlineData("1x")]
    [InlineData("-1x")]
    [InlineData("+1x")]
    [InlineData(".5x")]
    [InlineData("-.5x")]
    [InlineData("+.5x")]
    public void Parse_NumberLikeIdentifier_Throws(string text)
    {
        var asNodeName = () => KdlDocument.Parse(text);
        var asArgument = () => KdlDocument.Parse($"node {text}");
        var asPropertyKey = () => KdlDocument.Parse($"node {text}=1");

        asNodeName.Should().Throw<KdlParseException>();
        asArgument.Should().Throw<KdlParseException>();
        asPropertyKey.Should().Throw<KdlParseException>();
    }

    [Theory]
    [InlineData("-x")]
    [InlineData(".x")]
    [InlineData("-.x")]
    [InlineData("+.x")]
    [InlineData("--1")]
    [InlineData(".-1")]
    // The spec's digit is ASCII only, so other Unicode digits don't make the text number-like.
    [InlineData("٣x")]
    [InlineData("+٣x")]
    [InlineData(".٣x")]
    public void Parse_IdentifierThatOnlyStartsLikeANumber_IsAString(string text)
    {
        var doc = KdlDocument.Parse($"{text} {text} {text}=1");

        doc.Nodes[0].Name.Should().Be(text);
        doc.Nodes[0].Arguments[0].AsString().Should().Be(text);
        doc.Nodes[0].HasProperty(text).Should().BeTrue();
    }

    // KDL v1 has no rule about '.', so only a digit straight after the optional sign makes text number-like.
    // v1 only allows identifiers as node names and property keys.
    [Theory]
    [InlineData(".5x")]
    [InlineData("+.5x")]
    [InlineData("٣x")]
    public void Parse_V1_IdentifierThatOnlyStartsLikeANumber_IsAnIdentifier(string text)
    {
        var settings = new KdlParserSettings { TargetVersion = KdlVersion.V1 };

        var doc = KdlDocument.Parse($"{text} {text}=1", settings);

        doc.Nodes[0].Name.Should().Be(text);
        doc.Nodes[0].HasProperty(text).Should().BeTrue();
    }

    [Theory]
    [InlineData("1x")]
    [InlineData("-1x")]
    [InlineData("+1x")]
    public void Parse_V1_NumberLikeIdentifier_Throws(string text)
    {
        var settings = new KdlParserSettings { TargetVersion = KdlVersion.V1 };

        var asNodeName = () => KdlDocument.Parse(text, settings);
        var asPropertyKey = () => KdlDocument.Parse($"node {text}=1", settings);

        asNodeName.Should().Throw<KdlParseException>();
        asPropertyKey.Should().Throw<KdlParseException>();
    }
}
