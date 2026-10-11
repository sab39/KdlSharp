namespace KdlSharp;

/// <summary>
/// The exact source text a parsed element came from, with its position.
/// </summary>
/// <remarks>
/// Only present for elements parsed with <see cref="Settings.KdlParserSettings.PreserveSourceText"/>, and only ever
/// created by the parser, so properties that give one are <c>null</c> otherwise. When present, it's always exactly
/// the text the element was parsed from. The default value isn't a valid source text.
/// </remarks>
public readonly struct SourceText
{
    private readonly ExtendedSourcePosition position;

    /// <summary>
    /// Gets the position of this text in the source.
    /// </summary>
    public SourcePosition Position => position;

    /// <summary>
    /// Gets the exact source text.
    /// </summary>
    public string RawText => position.Source.Substring(position.Offset, position.Length);

    internal SourceText(ExtendedSourcePosition position)
    {
        this.position = position;
    }

    /// <summary>
    /// Gets the exact source text without allocating a string.
    /// </summary>
    public ReadOnlySpan<char> AsSpan() => position.Source.AsSpan(position.Offset, position.Length);

    /// <summary>
    /// Returns the exact source text, <see cref="RawText"/>.
    /// </summary>
    public override string ToString() => RawText;
}
