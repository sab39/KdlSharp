using AwesomeAssertions;
using KdlSharp;
using KdlSharp.Settings;
using Xunit;

namespace KdlSharp.Tests;

public class SourceTextTests
{
    [Theory]
    [InlineData("bare")]
    [InlineData("\"quoted\"")]
    [InlineData("#\"raw\"#")]
    [InlineData("\"\"\"\n    multi\n    line\n    \"\"\"")]
    [InlineData("1_000.5")]
    [InlineData("-0x10")]
    [InlineData("#true")]
    [InlineData("#-inf")]
    public void Parse_Value_SourceTextIsExactSourceText(string literal)
    {
        var kdl = $"node {literal} key={literal}";
        var doc = ParseWithSourceText(kdl);
        var node = doc.Nodes[0];

        foreach (var value in new[] { node.Arguments[0], node.Properties[0].Value })
        {
            var text = value.SourceText!.Value;
            text.RawText.Should().Be(literal);
            text.AsSpan().ToString().Should().Be(literal);
            text.ToString().Should().Be(literal);
            text.Position.Offset.Should().Be(value.SourcePosition!.Offset);
            text.Position.Length.Should().Be(literal.Length);
        }
    }

    [Theory]
    [InlineData("node")]
    [InlineData("\"my node\"")]
    [InlineData("#\"raw node\"#")]
    public void Parse_Node_SourceTextIsNodeName(string name)
    {
        var doc = ParseWithSourceText($"(tag){name} 1");

        var text = doc.Nodes[0].SourceText!.Value;
        text.Position.Offset.Should().Be(5);
        text.RawText.Should().Be(name);
    }

    [Theory]
    [InlineData("(u8)")]
    [InlineData("( u8 )")]
    [InlineData("(\"u8\")")]
    [InlineData("(#\"u8\"#)")]
    [InlineData("( /* comment */ u8 )")]
    public void Parse_Annotation_SourceTextCoversParentheses(string annotation)
    {
        var kdl = $"{annotation}node {annotation}1 key={annotation}2";
        var doc = ParseWithSourceText(kdl);
        var node = doc.Nodes[0];

        var nodeAnnotation = node.TypeAnnotation!.SourceText!.Value;
        nodeAnnotation.Position.Offset.Should().Be(0);
        nodeAnnotation.RawText.Should().Be(annotation);

        var argumentAnnotation = node.Arguments[0].TypeAnnotation!.SourceText!.Value;
        argumentAnnotation.Position.Offset.Should().Be(annotation.Length + 5);
        argumentAnnotation.RawText.Should().Be(annotation);

        var propertyAnnotation = node.Properties[0].Value.TypeAnnotation!.SourceText!.Value;
        propertyAnnotation.Position.Offset.Should().Be(kdl.LastIndexOf(annotation, StringComparison.Ordinal));
        propertyAnnotation.RawText.Should().Be(annotation);
    }

    [Theory]
    [InlineData("255")]
    [InlineData("(u8)255")]
    [InlineData("(u8) 255")]
    [InlineData("( u8 )/* comment */255")]
    [InlineData("(tag)\"quoted\"")]
    [InlineData("(tag)#true")]
    public void Parse_Value_FullSourceTextIncludesAnnotation(string literal)
    {
        var kdl = $"node {literal} key={literal}";
        var doc = ParseWithSourceText(kdl);
        var node = doc.Nodes[0];

        var argument = node.Arguments[0].FullSourceText!.Value;
        argument.Position.Offset.Should().Be(5);
        argument.Position.Length.Should().Be(literal.Length);
        argument.RawText.Should().Be(literal);

        var propertyValue = node.Properties[0].Value.FullSourceText!.Value;
        propertyValue.Position.Offset.Should().Be(kdl.LastIndexOf(literal, StringComparison.Ordinal));
        propertyValue.RawText.Should().Be(literal);
    }

    [Theory]
    [InlineData("node")]
    [InlineData("(tag)node")]
    [InlineData("( tag ) /* comment */ \"my node\"")]
    public void Parse_Node_FullSourceTextIncludesAnnotation(string nameWithAnnotation)
    {
        var doc = ParseWithSourceText($"{nameWithAnnotation} 1");

        var text = doc.Nodes[0].FullSourceText!.Value;
        text.Position.Offset.Should().Be(0);
        text.RawText.Should().Be(nameWithAnnotation);
    }

    [Theory]
    [InlineData("key", "key=1")]
    [InlineData("\"my key\"", "\"my key\"=1")]
    [InlineData("#\"raw\"#", "#\"raw\"#=1")]
    [InlineData("key", "key = 1")]
    [InlineData("key", "key=(u8)1")]
    [InlineData("\"k\"", "\"k\" /* comment */ = ( u8 ) \"v\"")]
    public void Parse_Property_SourceTextCoversKeyAndWholeProperty(string key, string property)
    {
        var doc = ParseWithSourceText($"node 0 {property} other=2");
        var parsed = doc.Nodes[0].Properties[0];

        var keyText = parsed.KeySourceText!.Value;
        keyText.Position.Offset.Should().Be(7);
        keyText.RawText.Should().Be(key);

        var propertyText = parsed.SourceText!.Value;
        propertyText.Position.Offset.Should().Be(7);
        propertyText.Position.Length.Should().Be(property.Length);
        propertyText.RawText.Should().Be(property);
    }

    [Fact]
    public void Parse_WithoutPreserveSourceText_NoSourceTextButPositions()
    {
        var doc = KdlDocument.Parse("(tag)node (u8)1 key=(u8)2");
        var node = doc.Nodes[0];
        var argument = node.Arguments[0];
        var property = node.Properties[0];

        node.SourcePosition.Should().NotBeNull();
        node.SourceText.Should().BeNull();
        node.FullSourceText.Should().BeNull();
        node.TypeAnnotation!.SourceText.Should().BeNull();

        argument.SourcePosition.Should().NotBeNull();
        argument.SourceText.Should().BeNull();
        argument.FullSourceText.Should().BeNull();
        argument.TypeAnnotation!.SourceText.Should().BeNull();

        property.KeySourceText.Should().BeNull();
        property.SourceText.Should().BeNull();
        property.Value.SourceText.Should().BeNull();
    }

    [Fact]
    public void Parse_PropertyConstructedFromParsedValue_HasNoSourceText()
    {
        var parsed = ParseWithSourceText("node key=1").Nodes[0].Properties[0];

        var constructed = new KdlProperty("other", parsed.Value);

        constructed.KeySourceText.Should().BeNull();
        constructed.SourceText.Should().BeNull();
        constructed.Value.SourceText.Should().NotBeNull();
    }

    private static KdlDocument ParseWithSourceText(string kdl)
        => KdlDocument.Parse(kdl, new KdlParserSettings { PreserveSourceText = true });
}
