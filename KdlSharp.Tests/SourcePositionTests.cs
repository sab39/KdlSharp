using AwesomeAssertions;
using KdlSharp;
using Xunit;

namespace KdlSharp.Tests;

public class SourcePositionTests
{
    [Theory]
    [InlineData("#true")]
    [InlineData("#false")]
    [InlineData("#null")]
    public void Parse_KeywordArgument_HasSourcePosition(string keyword)
    {
        var kdl = $"node 1 {keyword} 2";
        var doc = KdlDocument.Parse(kdl);

        var position = doc.Nodes[0].Arguments[1].SourcePosition;
        position.Should().NotBeNull();
        position!.Line.Should().Be(1);
        position.Column.Should().Be(8);
        position.Offset.Should().Be(7);
        kdl.Substring(position.Offset, keyword.Length).Should().Be(keyword);
    }

    [Theory]
    [InlineData("#true")]
    [InlineData("#false")]
    [InlineData("#null")]
    public void Parse_KeywordPropertyValue_HasSourcePosition(string keyword)
    {
        var kdl = $"node key={keyword}";
        var doc = KdlDocument.Parse(kdl);

        var position = doc.Nodes[0].GetProperty("key")!.SourcePosition;
        position.Should().NotBeNull();
        position!.Offset.Should().Be(9);
        kdl.Substring(position.Offset, keyword.Length).Should().Be(keyword);
    }

    [Theory]
    [InlineData("#true")]
    [InlineData("#false")]
    [InlineData("#null")]
    public void Parse_KeywordInChildNode_HasSourcePosition(string keyword)
    {
        var kdl = $"node {{\n    child {keyword}\n}}";
        var doc = KdlDocument.Parse(kdl);

        var position = doc.Nodes[0].Children[0].Arguments[0].SourcePosition;
        position.Should().NotBeNull();
        position!.Line.Should().Be(2);
        position.Column.Should().Be(11);
        position.Offset.Should().Be(kdl.IndexOf(keyword, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("bare")]
    [InlineData("\"quoted\"")]
    [InlineData("\"with\\tescape\"")]
    [InlineData("#\"raw\"#")]
    [InlineData("##\"raw with \"# inside\"##")]
    [InlineData("\"\"\"\n    multi\n    line\n    \"\"\"")]
    [InlineData("\"\"\"\r\n    crlf\r\n    \"\"\"")]
    [InlineData("1_000.5")]
    [InlineData("-0x10")]
    [InlineData("#true")]
    [InlineData("#null")]
    [InlineData("#-inf")]
    public void Parse_Value_SourcePositionLengthCoversValue(string literal)
    {
        var kdl = $"node {literal} key={literal}";
        var doc = KdlDocument.Parse(kdl);

        var argumentPosition = doc.Nodes[0].Arguments[0].SourcePosition!;
        argumentPosition.Length.Should().Be(literal.Length);
        kdl.Substring(argumentPosition.Offset, argumentPosition.Length).Should().Be(literal);

        var propertyPosition = doc.Nodes[0].GetProperty("key")!.SourcePosition!;
        propertyPosition.Length.Should().Be(literal.Length);
        kdl.Substring(propertyPosition.Offset, propertyPosition.Length).Should().Be(literal);
    }

    [Theory]
    [InlineData("node 1", 0, 4)]
    [InlineData("\"my node\" 1", 0, 9)]
    [InlineData("(tag)node 1", 5, 4)]
    public void Parse_Node_SourcePositionCoversNodeName(string kdl, int offset, int length)
    {
        var doc = KdlDocument.Parse(kdl);

        var position = doc.Nodes[0].SourcePosition!;
        position.Offset.Should().Be(offset);
        position.Length.Should().Be(length);
    }

    [Fact]
    public void Parse_AnnotatedValue_SourcePositionLengthExcludesAnnotation()
    {
        var kdl = "node (u8)255";
        var doc = KdlDocument.Parse(kdl);

        var position = doc.Nodes[0].Arguments[0].SourcePosition!;
        position.Offset.Should().Be(9);
        position.Length.Should().Be(3);
    }

    [Theory]
    [InlineData("#true")]
    [InlineData("#false")]
    [InlineData("#null")]
    public void Parse_AnnotatedKeyword_SourcePositionIsAfterAnnotation(string keyword)
    {
        var kdl = $"node (tag){keyword}";
        var doc = KdlDocument.Parse(kdl);

        var position = doc.Nodes[0].Arguments[0].SourcePosition;
        position.Should().NotBeNull();
        position!.Offset.Should().Be(10);
        kdl.Substring(position.Offset, keyword.Length).Should().Be(keyword);
    }
}
