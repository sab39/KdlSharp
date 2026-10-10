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
