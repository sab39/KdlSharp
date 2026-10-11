using System.Globalization;
using KdlSharp.Settings;
using KdlSharp.Utilities;
using KdlSharp.Values;

namespace KdlSharp.Formatting;

/// <summary>
/// Low-level writer for streaming KDL output.
/// </summary>
public sealed class KdlWriter : IDisposable
{
    private readonly TextWriter writer;
    private readonly KdlFormatterSettings settings;
    private int indentLevel;

    /// <summary>
    /// Initializes a new writer with the specified output.
    /// </summary>
    public KdlWriter(TextWriter writer, KdlFormatterSettings? settings = null)
    {
        this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
        this.settings = settings ?? new KdlFormatterSettings();
        indentLevel = 0;
    }

    /// <summary>
    /// Writes a KDL version marker comment at the start of a document.
    /// </summary>
    /// <remarks>
    /// Per the KDL specification, a version marker takes the form <c>/- kdl-version N</c>
    /// where N is 1 or 2. This is a slashdashed (commented-out) node.
    /// </remarks>
    public void WriteVersionMarker()
    {
        var version = settings.TargetVersion == KdlVersion.V1 ? "1" : "2";
        writer.Write($"/- kdl-version {version}");
        writer.Write(settings.Newline);
    }

    /// <summary>
    /// Writes a complete node (name, type annotation, arguments, properties, children).
    /// </summary>
    public void WriteNode(KdlNode node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        WriteIndent();

        // Write type annotation
        if (node.TypeAnnotation != null)
        {
            WriteTypeAnnotation(node.TypeAnnotation);
        }

        // Write node name
        WriteNodeName(node.Name);

        // Write arguments
        foreach (var arg in node.Arguments)
        {
            writer.Write(" ");
            WriteValue(arg);
        }

        // Write properties in insertion order (preserving order from parsing)
        foreach (var prop in node.Properties)
        {
            writer.Write(" ");
            WritePropertyName(prop.Key);
            writer.Write("=");
            WriteValue(prop.Value);
        }

        // Write children
        if (node.HasChildren)
        {
            writer.Write(" {");
            writer.Write(settings.Newline);
            indentLevel++;

            foreach (var child in node.Children)
            {
                WriteNode(child);
            }

            indentLevel--;
            WriteIndent();
            writer.Write("}");
        }

        writer.Write(settings.Newline);
    }

    /// <summary>
    /// Writes a value (for use in arguments or properties).
    /// </summary>
    public void WriteValue(KdlValue value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        // Write type annotation
        if (value.TypeAnnotation != null)
        {
            WriteTypeAnnotation(value.TypeAnnotation);
        }

        switch (value)
        {
            case KdlString str:
                WriteString(str);
                break;
            case KdlNumber num:
                WriteNumber(num);
                break;
            case KdlBoolean boolean:
                if (settings.TargetVersion == KdlVersion.V1)
                {
                    writer.Write(boolean.Value ? "true" : "false");
                }
                else
                {
                    writer.Write(boolean.Value ? "#true" : "#false");
                }
                break;
            case KdlNull:
                writer.Write(settings.TargetVersion == KdlVersion.V1 ? "null" : "#null");
                break;
            default:
                throw new InvalidOperationException($"Unknown value type: {value.GetType()}");
        }
    }

    /// <summary>
    /// Writes the start of a node (name and optional type annotation).
    /// </summary>
    public void WriteStartNode(string name, KdlAnnotation? annotation = null)
    {
        WriteIndent();
        if (annotation != null)
        {
            WriteTypeAnnotation(annotation);
        }
        WriteNodeName(name);
    }

    /// <summary>
    /// Writes the end of a node (terminates the current node).
    /// </summary>
    public void WriteEndNode()
    {
        writer.Write(settings.Newline);
    }

    /// <summary>
    /// Writes an argument value for the current node.
    /// </summary>
    public void WriteArgument(KdlValue value)
    {
        writer.Write(" ");
        WriteValue(value);
    }

    /// <summary>
    /// Writes a property key-value pair for the current node.
    /// </summary>
    public void WriteProperty(string key, KdlValue value)
    {
        writer.Write(" ");
        WritePropertyName(key);
        writer.Write("=");
        WriteValue(value);
    }

    /// <summary>
    /// Writes the start of a children block.
    /// </summary>
    public void WriteStartChildren()
    {
        writer.Write(" {");
        writer.Write(settings.Newline);
        indentLevel++;
    }

    /// <summary>
    /// Writes the end of a children block.
    /// </summary>
    public void WriteEndChildren()
    {
        indentLevel--;
        WriteIndent();
        writer.Write("}");
        writer.Write(settings.Newline);
    }

    /// <summary>
    /// Flushes the underlying writer.
    /// </summary>
    public void Flush()
    {
        writer.Flush();
    }

    /// <summary>
    /// Disposes the writer and flushes any buffered output.
    /// </summary>
    public void Dispose()
    {
        Flush();
    }

    private void WriteIndent()
    {
        if (!settings.Compact)
        {
            for (int i = 0; i < indentLevel; i++)
            {
                writer.Write(settings.Indentation);
            }
        }
    }

    private void WriteNodeName(string name) => WriteIdentifierOrString(name);

    private void WritePropertyName(string key) => WriteIdentifierOrString(key);

    private void WriteTypeAnnotation(KdlAnnotation annotation)
    {
        writer.Write('(');
        WriteIdentifierOrString(annotation.TypeName);
        writer.Write(')');
    }

    // Node names, property keys and type annotations can be any string, written bare where that's valid.
    private void WriteIdentifierOrString(string text)
    {
        if (settings.PreferIdentifierStrings && StringEscaper.IsValidIdentifier(text, settings.TargetVersion))
        {
            writer.Write(text);
        }
        else
        {
            writer.Write(StringEscaper.Escape(text));
        }
    }

    // KDL v1 only allows identifiers as node names and property keys, never as values.
    private bool CanWriteBareStringValue(string value)
        => settings.TargetVersion != KdlVersion.V1 && StringEscaper.IsValidIdentifier(value, settings.TargetVersion);

    private void WriteString(KdlString str)
    {
        if (!settings.PreserveStringTypes)
        {
            if (settings.PreferIdentifierStrings && CanWriteBareStringValue(str.Value))
            {
                writer.Write(str.Value);
            }
            else
            {
                writer.Write(StringEscaper.Escape(str.Value));
            }
            return;
        }

        switch (str.StringType)
        {
            case KdlStringType.Identifier:
                if (CanWriteBareStringValue(str.Value))
                {
                    writer.Write(str.Value);
                }
                else
                {
                    writer.Write(StringEscaper.Escape(str.Value));
                }
                break;
            case KdlStringType.Quoted:
                writer.Write(StringEscaper.Escape(str.Value));
                break;
            case KdlStringType.MultiLine:
                writer.Write($"\"\"\"{str.Value}\"\"\"");
                break;
            case KdlStringType.Raw:
                WriteRawString(str);
                break;
            default:
                writer.Write(StringEscaper.Escape(str.Value));
                break;
        }
    }

    private void WriteNumber(KdlNumber num)
    {
        if (settings.PreserveNumberFormats && num.Format is KdlNumberFormat.Hexadecimal or KdlNumberFormat.Octal or KdlNumberFormat.Binary)
        {
            // The number's own text is in its format
            writer.Write(num.RawText);
        }
        else
        {
            writer.Write(num.ToKdlString());
        }
    }

    private void WriteRawString(KdlString str)
    {
        // Determine the hash count to use
        var hashCount = str.RawHashCount > 0 ? str.RawHashCount : ComputeMinimalHashCount(str.Value);
        var hashes = new string('#', hashCount);

        if (str.IsRawMultiLine)
        {
            // Multi-line raw string: #"""..."""#
            // Use the preserved indent from parsing, or fall back to computed indent
            var indent = str.RawMultiLineIndent ?? GetIndentString(indentLevel + 1);

            writer.Write(hashes);
            writer.Write("\"\"\"");
            writer.Write(settings.Newline);
            // Write the content with the preserved indentation
            var lines = str.Value.Split('\n');
            foreach (var line in lines)
            {
                writer.Write(indent);
                writer.Write(line);
                writer.Write(settings.Newline);
            }
            writer.Write(indent);
            writer.Write("\"\"\"");
            writer.Write(hashes);
            // Note: We do NOT add a newline after the closing delimiter.
            // In KDL, subsequent arguments/properties must appear on the same line
            // as the closing delimiter, otherwise they become separate nodes.
            // The formatter continues with " next-arg" which produces valid KDL.
        }
        else
        {
            // Single-line raw string: #"..."#
            writer.Write(hashes);
            writer.Write('"');
            writer.Write(str.Value);
            writer.Write('"');
            writer.Write(hashes);
        }
    }

    private string GetIndentString(int level)
    {
        if (settings.Compact)
        {
            return "";
        }
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < level; i++)
        {
            sb.Append(settings.Indentation);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Computes the minimal number of hash characters needed to safely delimit a raw string.
    /// </summary>
    private static int ComputeMinimalHashCount(string value)
    {
        // We need to ensure the closing delimiter ("# pattern) doesn't appear in the value
        // Start with 1 hash and increment if the pattern appears in the value
        var hashCount = 1;
        while (true)
        {
            var closingPattern = '"' + new string('#', hashCount);
            if (!value.Contains(closingPattern))
            {
                break;
            }
            hashCount++;
        }
        return hashCount;
    }
}

