namespace KdlSharp;

/// <summary>
/// Represents a position in KDL source text.
/// </summary>
/// <remarks>
/// Used for error reporting and source mapping. Only present for parsed nodes, not programmatically constructed ones.
/// </remarks>
public sealed class SourcePosition
{
    /// <summary>
    /// Gets the line number (1-indexed).
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets the column number (1-indexed).
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Gets the character offset in the source text.
    /// </summary>
    public int Offset { get; }

    /// <summary>
    /// Gets the length of the source text at this position, in characters.
    /// </summary>
    /// <remarks>
    /// For a value, this covers the value itself, excluding any type annotation. For a node, it covers the node name.
    /// </remarks>
    public int Length { get; }

    /// <summary>
    /// Initializes a new source position.
    /// </summary>
    public SourcePosition(int line, int column, int offset)
        : this(line, column, offset, 0)
    {
    }

    /// <summary>
    /// Initializes a new source position with a length.
    /// </summary>
    public SourcePosition(int line, int column, int offset, int length)
    {
        Line = line;
        Column = column;
        Offset = offset;
        Length = length;
    }

    /// <summary>
    /// Returns a string representation like "line 10, column 5".
    /// </summary>
    public override string ToString() => $"line {Line}, column {Column}";
}
