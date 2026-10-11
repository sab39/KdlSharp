namespace KdlSharp;

/// <summary>
/// The exact source text a parsed element came from, with its position.
/// </summary>
/// <remarks>
/// Only present for elements parsed with <see cref="Settings.KdlParserSettings.PreserveSourceText"/>, and only ever
/// created by the parser. When present, it's always exactly the text the element was parsed from.
/// </remarks>
public sealed class SourceText
{
    private readonly string source;
    private string? rawText;

    /// <summary>
    /// Gets the position of this text in the source.
    /// </summary>
    public SourcePosition Position { get; }

    /// <summary>
    /// Gets the exact source text.
    /// </summary>
    public string RawText => rawText ??= source.Substring(Position.Offset, Position.Length);

    internal SourceText(string source, SourcePosition position)
    {
        this.source = source;
        Position = position;
    }

    /// <summary>
    /// Gets the exact source text without allocating a string.
    /// </summary>
    public ReadOnlySpan<char> AsSpan() => source.AsSpan(Position.Offset, Position.Length);

    /// <summary>
    /// Returns the exact source text, <see cref="RawText"/>.
    /// </summary>
    public override string ToString() => RawText;

    /// <summary>
    /// Creates the text from the start of one source text to the end of another, in the same source.
    /// </summary>
    internal static SourceText Span(SourceText start, SourceText end)
    {
        var position = new SourcePosition(
            start.Position.Line,
            start.Position.Column,
            start.Position.Offset,
            end.Position.Offset + end.Position.Length - start.Position.Offset);
        return new SourceText(start.source, position);
    }
}
