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
        var sb = new System.Text.StringBuilder("\"");
        for (int i = 0; i < value.Length; i++)
        {
            var ch = value[i];

            // A surrogate pair is written as the character itself, since \u{} escapes must be
            // Unicode scalar values and so can't escape the halves of a pair
            if (char.IsSurrogate(ch))
            {
                if (!char.IsSurrogatePair(value, i))
                {
                    throw new ArgumentException($"Lone surrogate U+{(int)ch:X4} at index {i} can't be written as KDL.", nameof(value));
                }
                sb.Append(ch).Append(value[i + 1]);
                i++;
                continue;
            }

            switch (ch)
            {
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                default:
                    if (IsDisallowedChar(ch))
                    {
                        sb.Append($"\\u{{{(int)ch:X}}}");
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
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
        return false;
    }

    /// <summary>
    /// Checks if a string can be represented as an identifier (bare, unquoted).
    /// </summary>
    public static bool IsValidIdentifier(string value, KdlVersion version = KdlVersion.V2)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        // Check reserved keywords
        if (IsReservedKeyword(value))
        {
            return false;
        }

        if (LooksLikeNumber(value, version))
        {
            return false;
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
                    return false;
                }
            }
            else if (char.IsHighSurrogate(ch))
            {
                if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                {
                    return false;
                }
                i++;
            }
            else if (char.IsLowSurrogate(ch) || !IsIdentifierContinue(ch))
            {
                return false;
            }
        }

        return true;
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

    // Bit tables of the ASCII characters that can't be in an identifier, for characters 0-63 and 64-127,
    // built from IsIdentifierContinue so that the two always agree
    private static readonly ulong nonIdentifierAsciiLow = BuildNonIdentifierAsciiTable(0);
    private static readonly ulong nonIdentifierAsciiHigh = BuildNonIdentifierAsciiTable(64);

    private static ulong BuildNonIdentifierAsciiTable(int start)
    {
        ulong table = 0;
        for (int i = 0; i < 64; i++)
        {
            if (!IsIdentifierContinue((char)(start + i)))
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

