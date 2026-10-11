namespace KdlSharp;

/// <summary>
/// A property parsed with <see cref="Settings.KdlParserSettings.PreserveSourceText"/>, which keeps its key's
/// position. Being a separate type means a plain <see cref="KdlProperty"/> costs nothing for it, and a property
/// constructed from a parsed value never reports a key position that isn't its own.
/// </summary>
internal sealed class ParsedKdlProperty : KdlProperty
{
    /// <summary>
    /// Gets the position of the property key.
    /// </summary>
    public ExtendedSourcePosition KeyPosition { get; }

    public ParsedKdlProperty(string key, KdlValue value, ExtendedSourcePosition keyPosition)
        : base(key, value)
    {
        KeyPosition = keyPosition;
    }
}
