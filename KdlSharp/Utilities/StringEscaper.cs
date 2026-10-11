namespace KdlSharp.Utilities;

/// <summary>
/// Utility class for escaping and unescaping KDL strings.
/// </summary>
internal static class StringEscaper
{
    /// <summary>
    /// Escapes a string for use in KDL quoted string format.
    /// </summary>
    /// <exception cref="ArgumentException">The string contains a lone surrogate, which no KDL string can represent.</exception>
    public static string Escape(string value)
    {
        using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        WriteQuoted(writer, value, 0);
        return writer.ToString();
    }

    /// <summary>
    /// Writes a string in KDL quoted string format. Only the characters that have to be are escaped, and the text
    /// between them is written a run at a time.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The string to write.</param>
    /// <param name="scanFrom">Where to start looking for characters to escape, when it's known that none come before
    /// it. <see cref="FindNonIdentifierIndex"/> gives such a position, since every character that has to be escaped
    /// also can't be in an identifier.</param>
    /// <exception cref="ArgumentException">The string contains a lone surrogate, which no KDL string can represent.</exception>
    public static void WriteQuoted(TextWriter writer, string value, int scanFrom)
    {
        writer.Write('"');
        var runStart = 0;
        for (int i = scanFrom; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch < 128 ? !IsEscapedAscii(ch) : !char.IsSurrogate(ch) && !IsEscapedNonAscii(ch))
            {
                continue;
            }

            // A surrogate pair is written as the character itself, since \u{} escapes must be
            // Unicode scalar values and so can't escape the halves of a pair
            if (char.IsSurrogate(ch))
            {
                if (!char.IsSurrogatePair(value, i))
                {
                    throw new ArgumentException($"Lone surrogate U+{(int)ch:X4} at index {i} can't be written as KDL.", nameof(value));
                }
                i++;
                continue;
            }

            writer.Write(value.AsSpan(runStart, i - runStart));
            WriteEscape(writer, ch);
            runStart = i + 1;
        }
        writer.Write(value.AsSpan(runStart, value.Length - runStart));
        writer.Write('"');
    }

    private static void WriteEscape(TextWriter writer, char ch)
    {
        switch (ch)
        {
            case '\n':
                writer.Write("\\n");
                break;
            case '\r':
                writer.Write("\\r");
                break;
            case '\t':
                writer.Write("\\t");
                break;
            case '\\':
                writer.Write("\\\\");
                break;
            case '"':
                writer.Write("\\\"");
                break;
            case '\b':
                writer.Write("\\b");
                break;
            case '\f':
                writer.Write("\\f");
                break;
            default:
                // Disallowed code points, and newlines that have no escape of their own
                writer.Write("\\u{");
                writer.Write(((int)ch).ToString("X", System.Globalization.CultureInfo.InvariantCulture));
                writer.Write('}');
                break;
        }
    }

    // ASCII characters a quoted string has to escape: the named escapes, and every other control character, which
    // is either disallowed or a newline
    private static bool IsEscapedAscii(char ch)
    {
        return ((ch < 64 ? escapedAsciiLow >> ch : escapedAsciiHigh >> (ch - 64)) & 1) != 0;
    }

    // Characters beyond ASCII a quoted string has to escape: disallowed code points, and newlines, which a
    // single-line string can't contain literally
    private static bool IsEscapedNonAscii(char ch)
    {
        return IsDisallowedChar(ch) || IsNewline(ch);
    }

    /// <summary>
    /// Unescapes a KDL quoted string (without the surrounding quotes).
    /// </summary>
    public static string Unescape(string escaped)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < escaped.Length; i++)
        {
            if (escaped[i] == '\\' && i + 1 < escaped.Length)
            {
                var next = escaped[i + 1];
                switch (next)
                {
                    case 'n':
                        sb.Append('\n');
                        i++;
                        break;
                    case 'r':
                        sb.Append('\r');
                        i++;
                        break;
                    case 't':
                        sb.Append('\t');
                        i++;
                        break;
                    case '\\':
                        sb.Append('\\');
                        i++;
                        break;
                    case '"':
                        sb.Append('"');
                        i++;
                        break;
                    case 'b':
                        sb.Append('\b');
                        i++;
                        break;
                    case 'f':
                        sb.Append('\f');
                        i++;
                        break;
                    case 's':
                        sb.Append(' ');
                        i++;
                        break;
                    case 'u':
                        if (i + 2 < escaped.Length && escaped[i + 2] == '{')
                        {
                            var end = escaped.IndexOf('}', i + 3);
                            if (end > 0)
                            {
                                var hex = escaped.Substring(i + 3, end - i - 3);
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var codePoint))
                                {
                                    sb.Append(char.ConvertFromUtf32(codePoint));
                                    i = end;
                                }
                            }
                        }
                        break;
                    default:
                        // Whitespace escape - skip until newline
                        if (char.IsWhiteSpace(next))
                        {
                            i++;
                            while (i < escaped.Length && escaped[i] != '\n' && escaped[i] != '\r')
                            {
                                i++;
                            }
                            if (i < escaped.Length && escaped[i] == '\r' && i + 1 < escaped.Length && escaped[i + 1] == '\n')
                            {
                                i++;
                            }
                        }
                        else
                        {
                            sb.Append(escaped[i]);
                        }
                        break;
                }
            }
            else
            {
                sb.Append(escaped[i]);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Checks if a character is disallowed in KDL strings.
    /// </summary>
    public static bool IsDisallowedChar(char ch)
    {
        int code = ch;
        // Control characters: U+0000-0008, U+000E-001F, U+007F
        if ((code >= 0x0000 && code <= 0x0008) || (code >= 0x000E && code <= 0x001F) || code == 0x007F)
        {
            return true;
        }
        // Surrogates: U+D800-DFFF
        if (code >= 0xD800 && code <= 0xDFFF)
        {
            return true;
        }
        // Direction control: U+200E-200F, U+202A-202E, U+2066-2069
        if ((code >= 0x200E && code <= 0x200F) || (code >= 0x202A && code <= 0x202E) || (code >= 0x2066 && code <= 0x2069))
        {
            return true;
        }
        // Zero-width non-breaking space, or BOM: U+FEFF. A file's BOM is removed when the file is read, so it
        // isn't allowed anywhere in the text that's parsed or written.
        if (code == 0xFEFF)
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if a string can be represented as an identifier (bare, unquoted).
    /// </summary>
    public static bool IsValidIdentifier(string value, KdlVersion version = KdlVersion.V2)
    {
        return FindNonIdentifierIndex(value, version) < 0;
    }

    /// <summary>
    /// Finds where a string stops being able to be an identifier.
    /// </summary>
    /// <returns>-1 if the string can be an identifier. Otherwise, the index of the first character that can't be in
    /// one, so that every character before it can be, or 0 if the string can't be an identifier as a whole: when
    /// it's empty, a keyword, or starts like a number.</returns>
    public static int FindNonIdentifierIndex(string value, KdlVersion version = KdlVersion.V2)
    {
        if (string.IsNullOrEmpty(value) || IsReservedKeyword(value) || LooksLikeNumber(value, version))
        {
            return 0;
        }

        // One pass over the characters, which can all be any identifier character (IsIdentifierStart and
        // IsIdentifierContinue are the same): ASCII is looked up in a bit table, other characters are checked
        // individually, and surrogates are allowed only as complete pairs
        for (int i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch < 128)
            {
                if (IsNonIdentifierAscii(ch))
                {
                    return i;
                }
            }
            else if (char.IsHighSurrogate(ch))
            {
                if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                {
                    return i;
                }
                i++;
            }
            else if (char.IsLowSurrogate(ch) || !IsIdentifierContinue(ch))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Checks if text made of identifier characters starts like a number, and so can't be an identifier.
    /// </summary>
    /// <remarks>
    /// In KDL v2, after an optional sign and then an optional '.', the next character can't be a digit.
    /// KDL v1 has no rule about '.', so only the sign is skipped. In both, only ASCII digits count.
    /// </remarks>
    public static bool LooksLikeNumber(string value, KdlVersion version)
    {
        var index = 0;
        if (index < value.Length && value[index] is '+' or '-')
        {
            index++;
        }
        if (version != KdlVersion.V1 && index < value.Length && value[index] == '.')
        {
            index++;
        }

        return index < value.Length && value[index] is >= '0' and <= '9';
    }

    // Bit tables of ASCII characters, for characters 0-63 and 64-127: those that can't be in an identifier, built
    // from IsIdentifierContinue so that the two always agree, and those a quoted string has to escape
    private static readonly ulong nonIdentifierAsciiLow = BuildAsciiTable(0, ch => !IsIdentifierContinue(ch));
    private static readonly ulong nonIdentifierAsciiHigh = BuildAsciiTable(64, ch => !IsIdentifierContinue(ch));
    private static readonly ulong escapedAsciiLow = BuildAsciiTable(0, ch => ch < 0x20 || ch is '"' or '\\' or '\u007F');
    private static readonly ulong escapedAsciiHigh = BuildAsciiTable(64, ch => ch < 0x20 || ch is '"' or '\\' or '\u007F');

    private static ulong BuildAsciiTable(int start, Func<char, bool> isIncluded)
    {
        ulong table = 0;
        for (int i = 0; i < 64; i++)
        {
            if (isIncluded((char)(start + i)))
            {
                table |= 1UL << i;
            }
        }
        return table;
    }

    private static bool IsNonIdentifierAscii(char ch)
    {
        return ((ch < 64 ? nonIdentifierAsciiLow >> ch : nonIdentifierAsciiHigh >> (ch - 64)) & 1) != 0;
    }

    private static bool IsReservedKeyword(string value)
    {
        return value is "true" or "false" or "null" or "inf" or "-inf" or "nan";
    }

    /// <summary>
    /// Checks if a character can start an identifier.
    /// </summary>
    public static bool IsIdentifierStart(char ch)
    {
        // Per KDL spec: identifier-char := unicode - unicode-space - newline - [\\/(){};\[\]"#=] - disallowed-literal-code-points

        // Allow surrogates (for emojis and other high Unicode code points)
        if (char.IsHighSurrogate(ch) || char.IsLowSurrogate(ch))
        {
            return true;
        }

        return !char.IsWhiteSpace(ch) && !IsNewline(ch) && ch != '\\' && ch != '/' &&
               ch != '(' && ch != ')' && ch != '{' && ch != '}' && ch != ';' &&
               ch != '[' && ch != ']' && ch != '"' && ch != '#' && ch != '=' &&
               !IsDisallowedChar(ch);
    }

    /// <summary>
    /// Checks if a character can continue an identifier.
    /// </summary>
    public static bool IsIdentifierContinue(char ch)
    {
        // Same as start - KDL allows very permissive identifiers

        // Allow surrogates (for emojis and other high Unicode code points)
        if (char.IsHighSurrogate(ch) || char.IsLowSurrogate(ch))
        {
            return true;
        }

        return !char.IsWhiteSpace(ch) && !IsNewline(ch) && ch != '\\' && ch != '/' &&
               ch != '(' && ch != ')' && ch != '{' && ch != '}' && ch != ';' &&
               ch != '[' && ch != ']' && ch != '"' && ch != '#' && ch != '=' &&
               !IsDisallowedChar(ch);
    }

    private static bool IsNewline(char ch) => ch is '\n' or '\r' or '\u0085' or '\u000B' or '\u000C' or '\u2028' or '\u2029';
}

