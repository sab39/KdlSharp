using System.Text;
using AwesomeAssertions;
using KdlSharp;
using KdlSharp.Exceptions;
using KdlSharp.Values;
using Xunit;

namespace KdlSharp.Tests;

public class DisallowedCodePointTests
{
    private const char bom = '\uFEFF';

    [Theory]
    [InlineData("\uFEFFnode arg")]
    [InlineData("node ar\uFEFFg")]
    [InlineData("node \"ar\uFEFFg\"")]
    [InlineData("node #\"ar\uFEFFg\"#")]
    [InlineData("node \"\"\"\n    ar\uFEFFg\n    \"\"\"")]
    [InlineData("// comment \uFEFF\nnode arg")]
    [InlineData("node /* comment \uFEFF */ arg")]
    public void Parse_Bom_AnywhereInText_Throws(string kdl)
    {
        var act = () => KdlDocument.Parse(kdl);

        act.Should().Throw<KdlParseException>();
    }

    [Theory]
    [InlineData("node \"a\u200Eb\"")]
    [InlineData("node #\"a\u2066b\"#")]
    [InlineData("node \"a\u007Fb\"")]
    [InlineData("// a\u202A\nnode")]
    public void Parse_DisallowedCodePoint_InStringOrComment_Throws(string kdl)
    {
        var act = () => KdlDocument.Parse(kdl);

        act.Should().Throw<KdlParseException>();
    }

    [Fact]
    public void Parse_SurrogatePairInString_IsAllowed()
    {
        var doc = KdlDocument.Parse("node \"\uD83D\uDE00\" // \uD83D\uDE00");

        doc.Nodes[0].Arguments[0].AsString().Should().Be("\uD83D\uDE00");
    }

    [Fact]
    public void ParseStream_WithBom_Parses()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("node arg")).ToArray());

        var doc = KdlDocument.ParseStream(stream);

        doc.Nodes[0].Name.Should().Be("node");
    }

    [Fact]
    public void ParseFile_WithBom_Parses()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "node arg", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            var doc = KdlDocument.ParseFile(path);

            doc.Nodes[0].Name.Should().Be("node");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseFile_WithTwoBoms_Throws()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, $"{bom}node arg", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            var act = () => KdlDocument.ParseFile(path);

            act.Should().Throw<KdlParseException>();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Write_BomInStringValue_IsEscaped()
    {
        var doc = new KdlDocument();
        doc.Nodes.Add(new KdlNode("node").AddArgument(new KdlString($"ar{bom}g")));

        var written = doc.ToKdlString();

        written.Should().NotContain(bom.ToString());
        written.Should().Contain("\\u{FEFF}");
        KdlDocument.Parse(written).Nodes[0].Arguments[0].AsString().Should().Be($"ar{bom}g");
    }

    [Fact]
    public void Write_BomInNodeName_IsQuotedAndEscaped()
    {
        var doc = new KdlDocument();
        doc.Nodes.Add(new KdlNode($"no{bom}de"));

        var written = doc.ToKdlString();

        written.Should().NotContain(bom.ToString());
        KdlDocument.Parse(written).Nodes[0].Name.Should().Be($"no{bom}de");
    }
}
