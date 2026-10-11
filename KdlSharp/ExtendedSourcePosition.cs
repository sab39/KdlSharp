namespace KdlSharp;

/// <summary>
/// A source position that also keeps what's needed for its element's exact source text. The parser creates these
/// in place of plain <see cref="SourcePosition"/>s only when parsing with
/// <see cref="Settings.KdlParserSettings.PreserveSourceText"/>, so without it, source text costs nothing.
/// </summary>
internal sealed class ExtendedSourcePosition : SourcePosition
{
    /// <summary>
    /// Gets the full source text that this position is in.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Gets the position of the element's type annotation, parentheses included, or <c>null</c> if it has none.
    /// </summary>
    public ExtendedSourcePosition? Annotation { get; }

    public ExtendedSourcePosition(string source, int line, int column, int offset, int length, ExtendedSourcePosition? annotation = null)
        : base(line, column, offset, length)
    {
        Source = source;
        Annotation = annotation;
    }

    /// <summary>
    /// Gets the source text at a position, if it's an extended one.
    /// </summary>
    public static SourceText? TextOf(SourcePosition? position)
    {
        return position is ExtendedSourcePosition extended ? new SourceText(extended) : null;
    }

    /// <summary>
    /// Gets the source text at a position including its element's type annotation, if it's an extended one.
    /// </summary>
    public static SourceText? FullTextOf(SourcePosition? position)
    {
        return position switch
        {
            ExtendedSourcePosition { Annotation: { } annotation } extended => new SourceText(Span(annotation, extended)),
            ExtendedSourcePosition extended => new SourceText(extended),
            _ => null
        };
    }

    /// <summary>
    /// Creates the position from the start of one position to the end of another, in the same source.
    /// </summary>
    public static ExtendedSourcePosition Span(ExtendedSourcePosition start, ExtendedSourcePosition end)
    {
        return new ExtendedSourcePosition(
            start.Source,
            start.Line,
            start.Column,
            start.Offset,
            end.Offset + end.Length - start.Offset);
    }
}
