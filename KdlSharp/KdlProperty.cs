namespace KdlSharp;

/// <summary>
/// Represents a property (key-value pair) in a KDL node.
/// </summary>
/// <remarks>
/// Properties are ordered and can have duplicate keys.
/// When duplicate keys exist, the rightmost value takes precedence per KDL specification.
/// </remarks>
public class KdlProperty
{
    /// <summary>
    /// Gets the property key.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the property value.
    /// </summary>
    public KdlValue Value { get; }

    /// <summary>
    /// Gets the exact source text of the property key, or <c>null</c> unless parsed with
    /// <see cref="Settings.KdlParserSettings.PreserveSourceText"/>.
    /// </summary>
    public SourceText? KeySourceText => ExtendedSourcePosition.TextOf((this as ParsedKdlProperty)?.KeyPosition);

    /// <summary>
    /// Gets the exact source text of the whole property, or <c>null</c> unless parsed with
    /// <see cref="Settings.KdlParserSettings.PreserveSourceText"/>.
    /// </summary>
    /// <remarks>
    /// Spans from the start of the key to the end of the value, including anything between them.
    /// </remarks>
    public SourceText? SourceText
        => this is ParsedKdlProperty { KeyPosition: var key } && Value.SourcePosition is ExtendedSourcePosition value
            ? new SourceText(ExtendedSourcePosition.Span(key, value))
            : null;

    /// <summary>
    /// Initializes a new property with the specified key and value.
    /// </summary>
    /// <param name="key">The property key.</param>
    /// <param name="value">The property value.</param>
    /// <exception cref="ArgumentNullException">Thrown when key or value is null.</exception>
    public KdlProperty(string key, KdlValue value)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Returns a string representation of this property.
    /// </summary>
    public override string ToString() => $"{Key}={Value}";
}
