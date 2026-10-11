using AwesomeAssertions;
using KdlSharp;
using KdlSharp.Settings;
using KdlSharp.Values;
using Xunit;

namespace KdlSharp.Tests.FormatterTests;

public class StringEscapingTests
{
    [Theory]
    [InlineData("plain text", "node \"plain text\"\n")]
    [InlineData("\"start", "node \"\\\"start\"\n")]
    [InlineData("end\"", "node \"end\\\"\"\n")]
    [InlineData("a\n\tb", "node \"a\\n\\tb\"\n")]
    [InlineData("a b\\c d", "node \"a b\\\\c d\"\n")]
    [InlineData("\n", "node \"\\n\"\n")]
    [InlineData("\r\n\"\\", "node \"\\r\\n\\\"\\\\\"\n")]
    public void Write_QuotedString_EscapesOnlyWhatItMust(string value, string expected)
    {
        var written = Write(value);

        written.Should().Be(expected);
        KdlDocument.Parse(written).Nodes[0].Arguments[0].AsString().Should().Be(value);
    }

    [Theory]
    [InlineData(0x0B)]
    [InlineData(0x85)]
    [InlineData(0x2028)]
    [InlineData(0x2029)]
    public void Write_NewlineWithoutNamedEscape_IsEscaped(int code)
    {
        var value = $"a{(char)code}b";

        var written = Write(value);

        written.Should().Be($"node \"a\\u{{{code:X}}}b\"\n");
        KdlDocument.Parse(written).Nodes[0].Arguments[0].AsString().Should().Be(value);
    }

    [Fact]
    public void Write_SurrogatePairBesideEscapes_IsWrittenLiterally()
    {
        var emoji = char.ConvertFromUtf32(0x1F600);
        var value = $"\"{emoji}\n";

        var written = Write(value);

        written.Should().Be($"node \"\\\"{emoji}\\n\"\n");
        KdlDocument.Parse(written).Nodes[0].Arguments[0].AsString().Should().Be(value);
    }

    [Fact]
    public void Write_LoneSurrogate_Throws()
    {
        var act = () => Write($"a {(char)0xD800} b");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Write_WithoutPreferIdentifierStrings_QuotesEverything()
    {
        var doc = new KdlDocument();
        doc.Nodes.Add(new KdlNode("node").AddArgument(new KdlString("bare")).AddProperty("key", new KdlString("text")));

        var written = doc.ToKdlString(new KdlFormatterSettings { PreferIdentifierStrings = false });

        written.Should().Be("\"node\" \"bare\" \"key\"=\"text\"\n");
    }

    private static string Write(string value)
    {
        var doc = new KdlDocument();
        doc.Nodes.Add(new KdlNode("node").AddArgument(new KdlString(value)));
        return doc.ToKdlString();
    }
}
