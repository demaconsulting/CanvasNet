using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DemaConsulting.CanvasNet.Tool.PandocFilter;

/// <summary>
///     The drawing walk of <see cref="DependencyHasher"/>: an SVG drawing, the files its image elements link, and whether
///     it links a file only the tool draws.
/// </summary>
internal sealed partial class DependencyHasher
{
    /// <summary>
    ///     The namespace of the legacy <c>xlink:href</c> attribute.
    /// </summary>
    private const string XlinkNamespace = "http://www.w3.org/1999/xlink";

    /// <summary>
    ///     The markup that holds no elements, by the text that starts it and the text that ends it.
    /// </summary>
    private static readonly (string Start, string End)[] SkippedMarkup = [("<!--", "-->"), ("<![CDATA[", "]]>"), ("<?", "?>")];

    /// <summary>
    ///     Adds a drawing and the local files its image elements link to the parts (<c>add_svg_sources</c>).
    /// </summary>
    /// <param name="path">The drawing.</param>
    /// <param name="root">The folder labels are relative to.</param>
    /// <param name="depth">The nesting depth of linked drawings.</param>
    /// <returns>Whether the drawing links a file only the tool draws, directly or through a nested drawing.</returns>
    /// <remarks>
    ///     A link is read as the tool's resolver reads it: the query and fragment are cut off, then the rest is
    ///     percent-decoded (a <c>file:</c> URI is decoded as it is read); a remote or <c>data:</c> link is skipped. A
    ///     missing model, chart, diagram, or document it links adds a <c>missing</c> part and makes the drawing composite
    ///     (the tool reports the link). While a drawing is walked it is <see cref="Seen.Taken"/>, afterwards
    ///     <see cref="Seen.Composite"/> or <see cref="Seen.Plain"/>, so a drawing linked twice is read once and a cycle
    ///     ends.
    /// </remarks>
    private bool AddSvg(string path, string root, int depth)
    {
        if (depth > MaxDepth)
        {
            throw Fail($"SVG drawings are linked inside each other more than {MaxDepth} levels deep");
        }

        _seen[path] = Seen.Taken;
        var content = FilterPaths.ReadFile(path);
        if (content == null)
        {
            _seen[path] = Seen.Plain;
            return false;
        }

        if (_parts.Count >= MaxDependencies)
        {
            throw Fail($"the drawing and the files it links are more than {MaxDependencies} files; the limit is {MaxDependencies} (every file must be hashed so that a changed one is drawn again)");
        }

        AddPart(FilterPaths.Label(root, path), content);

        // Every link that names a file: an existing one is walked, a missing one of a kind the tool draws is noted
        var folder = FilterPaths.Directory(path);
        var files = new List<string>();
        var missing = new List<string>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var link in CollectSvgLinks(AsBytes(content)).Select(FromBytes))
        {
            // A file: URI is decoded as it is read; any other link must be local both as written and decoded
            var reference = FilterPaths.CutFragment(link);
            string? resolved;
            if (FilterPaths.AsciiLower(FilterPaths.TrimLuaSpaceStart(reference)).StartsWith("file:", StringComparison.Ordinal))
            {
                resolved = FilterPaths.LocalDataFile(folder, reference);
            }
            else
            {
                resolved = FilterPaths.LocalDataFile(folder, reference) == null ? null : FilterPaths.LocalDataFile(folder, FilterPaths.DecodePercent(reference));
            }

            if (resolved == null || unique.Contains(resolved))
            {
                continue;
            }

            if (DependencyExists(resolved))
            {
                unique.Add(resolved);
                files.Add(resolved);
            }
            else if (PictureAttributes.KindOf(resolved) != null)
            {
                unique.Add(resolved);
                missing.Add(resolved);
            }
        }

        // Sorted, so the order the links are met in cannot change the picture's name
        FilterPaths.SortBytes(files);
        FilterPaths.SortBytes(missing);
        if (files.Count + missing.Count > MaxDependencies)
        {
            throw Fail($"'{FilterPaths.FileName(path)}' links {files.Count + missing.Count} files; the limit is {MaxDependencies} (every file must be hashed so that a changed one is drawn again)");
        }

        // A missing file cannot be hashed, so the key notes it as missing: creating it later changes the key
        foreach (var file in missing)
        {
            _parts.Add(Encoding.UTF8.GetBytes("missing " + FilterPaths.Label(root, file)));
        }

        var composite = missing.Count > 0;
        foreach (var file in files)
        {
            if (FilterPaths.AsciiLower(file).EndsWith(".svg", StringComparison.Ordinal))
            {
                if (!_seen.ContainsKey(file))
                {
                    AddSvg(file, root, depth + 1);
                }

                composite = composite || _seen[file] == Seen.Composite;
            }
            else
            {
                composite = composite || PictureAttributes.KindOf(file) != null;
                AddImageFiles([file], root, depth + 1);
            }
        }

        _seen[path] = composite ? Seen.Composite : Seen.Plain;
        return composite;
    }

    /// <summary>
    ///     Collects the links of the image elements of an SVG text, as the tool finds them (<c>collect_svg_links</c>).
    /// </summary>
    /// <param name="content">The drawing as a text of bytes.</param>
    /// <returns>The links, entities decoded and white space trimmed, as texts of bytes.</returns>
    /// <remarks>
    ///     For each element named <c>image</c> (with or without a prefix): the unprefixed <c>href</c>, else the first
    ///     <c>P:href</c> whose prefix is bound to the XLink namespace on the element or an ancestor. Comments, character
    ///     data, processing instructions, a DOCTYPE (with its internal subset), and end tags hold no links, and a <c>&gt;</c>
    ///     inside a quoted attribute does not end a tag.
    /// </remarks>
    private static HashSet<string> CollectSvgLinks(string content)
    {
        var links = new HashSet<string>(StringComparer.Ordinal);
        var length = content.Length;
        var position = 0;

        // The namespace bindings of the open elements, innermost last
        var scopes = new List<Dictionary<string, string>>();
        while (position <= length)
        {
            var open = content.IndexOf('<', position);
            if (open < 0)
            {
                return links;
            }

            // Comments, character data, and processing instructions are skipped whole
            var markup = SkippedMarkup.FirstOrDefault(m => content.AsSpan(open).StartsWith(m.Start, StringComparison.Ordinal));
            if (markup.Start != null)
            {
                var close = content.IndexOf(markup.End, open + markup.Start.Length, StringComparison.Ordinal);
                if (close < 0)
                {
                    return links;
                }

                position = close + markup.End.Length;
                continue;
            }

            var next = open + 1 < length ? content[open + 1] : '\0';
            if (open + 1 < length && next == '!')
            {
                position = SkipDeclaration(content, open + 2) + 1;
            }
            else if (open + 1 < length && next == '/')
            {
                var end = content.IndexOf('>', Math.Min(open + 2, length));
                position = end < 0 ? length : end + 1;
                if (scopes.Count > 0)
                {
                    scopes.RemoveAt(scopes.Count - 1);
                }
            }
            else
            {
                position = ReadElement(content, open, scopes, links);
            }
        }

        return links;
    }

    /// <summary>
    ///     Finds the end of a DOCTYPE or another declaration: the <c>&gt;</c> outside quotes and outside its internal
    ///     subset.
    /// </summary>
    /// <param name="content">The text.</param>
    /// <param name="start">The position after <c>&lt;!</c>.</param>
    /// <returns>The position of the closing <c>&gt;</c>, or the length of the text when there is none.</returns>
    private static int SkipDeclaration(string content, int start)
    {
        var depth = 0;
        char? quote = null;
        var i = start;
        for (; i < content.Length; i++)
        {
            var c = content[i];
            if (quote != null)
            {
                if (c == quote)
                {
                    quote = null;
                }
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '[')
            {
                depth++;
            }
            else if (c == ']')
            {
                depth--;
            }
            else if (c == '>' && depth <= 0)
            {
                break;
            }
        }

        return i;
    }

    /// <summary>
    ///     Reads a start tag: its namespace bindings, and the link when it is an image element.
    /// </summary>
    /// <param name="content">The text.</param>
    /// <param name="open">The position of the <c>&lt;</c>.</param>
    /// <param name="scopes">The namespace bindings of the open elements; the element's own is pushed unless it closes itself.</param>
    /// <param name="links">The links found.</param>
    /// <returns>The position after the tag.</returns>
    private static int ReadElement(string content, int open, List<Dictionary<string, string>> scopes, HashSet<string> links)
    {
        // The element name: letters, digits, "_", ".", "-", and ":"
        var nameEnd = open + 1;
        while (nameEnd < content.Length && (char.IsAsciiLetterOrDigit(content[nameEnd]) || content[nameEnd] is '_' or '.' or '-' or ':'))
        {
            nameEnd++;
        }

        if (nameEnd == open + 1)
        {
            return open + 1;
        }

        var name = content[(open + 1)..nameEnd];
        var (attributes, position, closes) = ReadAttributes(content, nameEnd);
        var own = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (attribute, value) in attributes.Where(a => a.Name.StartsWith("xmlns:", StringComparison.Ordinal) && a.Name.Length > 6))
        {
            own[attribute[6..]] = DecodeEntities(value);
        }

        if (name == "image" || name.EndsWith(":image", StringComparison.Ordinal))
        {
            string? plain = null;
            string? legacy = null;
            foreach (var (attribute, value) in attributes)
            {
                var prefix = HrefPrefix(attribute);
                if (attribute == "href")
                {
                    plain = value;
                }
                else if (legacy == null && prefix != null && NamespaceOf(prefix, own, scopes) == XlinkNamespace)
                {
                    legacy = value;
                }
            }

            if ((plain ?? legacy) is { } link)
            {
                links.Add(FilterPaths.TrimLuaSpace(DecodeEntities(link)));
            }
        }

        if (!closes)
        {
            scopes.Add(own);
        }

        return position;
    }

    /// <summary>
    ///     Reads the attributes of a start tag up to its closing <c>&gt;</c> (<c>read_attributes</c>).
    /// </summary>
    /// <param name="content">The text.</param>
    /// <param name="position">The position just after the element name.</param>
    /// <returns>
    ///     The attributes in order (an unquoted value, which is not XML, is read up to white space or <c>&gt;</c>), the
    ///     position after the tag, and whether the tag closes itself.
    /// </returns>
    private static (List<(string Name, string Value)> Attributes, int Position, bool Closes) ReadAttributes(string content, int position)
    {
        var attributes = new List<(string Name, string Value)>();
        var length = content.Length;
        var closes = false;
        while (position < length)
        {
            position = SkipSpace(content, position);
            if (position < length && content[position] == '>')
            {
                return (attributes, position + 1, closes);
            }

            if (position < length && content[position] == '/')
            {
                closes = true;
                position++;
                continue;
            }

            closes = false;
            var nameEnd = position;
            while (nameEnd < length && !FilterPaths.IsLuaSpace(content[nameEnd]) && content[nameEnd] is not ('=' or '/' or '>'))
            {
                nameEnd++;
            }

            if (nameEnd == position)
            {
                // A stray "=", which is not XML
                position++;
                continue;
            }

            var name = content[position..nameEnd];
            position = SkipSpace(content, nameEnd);
            var value = string.Empty;
            if (position < length && content[position] == '=')
            {
                position = SkipSpace(content, position + 1);
                var quote = position < length ? content[position] : '\0';
                if (position < length && quote is '"' or '\'')
                {
                    var close = content.IndexOf(quote, position + 1);
                    close = close < 0 ? length : close;
                    value = content[(position + 1)..close];
                    position = close + 1;
                }
                else
                {
                    var end = position;
                    while (end < length && !FilterPaths.IsLuaSpace(content[end]) && content[end] != '>')
                    {
                        end++;
                    }

                    value = content[position..end];
                    position = end;
                }
            }

            attributes.Add((name, value));
        }

        return (attributes, position, closes);
    }

    /// <summary>
    ///     Skips white space.
    /// </summary>
    /// <param name="content">The text.</param>
    /// <param name="position">The position to start at.</param>
    /// <returns>The position of the next character that is not white space, or the length of the text.</returns>
    private static int SkipSpace(string content, int position)
    {
        while (position < content.Length && FilterPaths.IsLuaSpace(content[position]))
        {
            position++;
        }

        return position;
    }

    /// <summary>
    ///     Gets the prefix of a prefixed <c>href</c> attribute (<c>^([^:]+):href$</c>).
    /// </summary>
    /// <param name="attribute">The attribute name.</param>
    /// <returns>The prefix, or <see langword="null"/> when the name is not <c>P:href</c> with a prefix without a colon.</returns>
    private static string? HrefPrefix(string attribute)
    {
        if (!attribute.EndsWith(":href", StringComparison.Ordinal) || attribute.Length <= 5)
        {
            return null;
        }

        var prefix = attribute[..^5];
        return prefix.Contains(':', StringComparison.Ordinal) ? null : prefix;
    }

    /// <summary>
    ///     Gets the namespace a prefix is bound to on an element or its ancestors.
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    /// <param name="own">The bindings of the element.</param>
    /// <param name="scopes">The bindings of the open elements, innermost last.</param>
    /// <returns>The namespace, or <see langword="null"/> when the prefix is not bound.</returns>
    private static string? NamespaceOf(string prefix, Dictionary<string, string> own, List<Dictionary<string, string>> scopes)
    {
        if (own.TryGetValue(prefix, out var bound))
        {
            return bound;
        }

        for (var i = scopes.Count - 1; i >= 0; i--)
        {
            if (scopes[i].TryGetValue(prefix, out var outer))
            {
                return outer;
            }
        }

        return null;
    }

    /// <summary>
    ///     Decodes the five predefined XML entities and the character references (<c>&amp;#N;</c>, <c>&amp;#xN;</c> below
    ///     0x110000) of a text of bytes; any other reference stays.
    /// </summary>
    /// <param name="text">The text of bytes.</param>
    /// <returns>The decoded text of bytes (a character reference becomes its UTF-8 bytes).</returns>
    private static string DecodeEntities(string text)
    {
        return EntityPattern().Replace(text, match =>
        {
            var name = match.Groups[1].Value;
            var named = name switch
            {
                "amp" => "&",
                "lt" => "<",
                "gt" => ">",
                "quot" => "\"",
                "apos" => "'",
                _ => null
            };
            if (named != null)
            {
                return named;
            }

            long code = -1;
            if (name.Length > 2 && name[0] == '#' && name[1] is 'x' or 'X' && name[2..].All(char.IsAsciiHexDigit))
            {
                code = long.TryParse(name[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex) && hex >= 0 ? hex : -1;
            }
            else if (name.Length > 1 && name[0] == '#' && name[1..].All(char.IsAsciiDigit))
            {
                code = long.TryParse(name[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var decimalCode) ? decimalCode : -1;
            }

            return code is >= 0 and < 0x110000 ? Encoding.Latin1.GetString(Utf8Of((int)code)) : match.Value;
        });
    }

    /// <summary>
    ///     Encodes a code point as UTF-8 as Lua's <c>utf8.char</c> does (a surrogate is encoded too).
    /// </summary>
    /// <param name="code">The code point, below 0x110000.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Utf8Of(int code)
    {
        return code switch
        {
            < 0x80 => [(byte)code],
            < 0x800 => [(byte)(0xC0 | (code >> 6)), (byte)(0x80 | (code & 0x3F))],
            < 0x10000 => [(byte)(0xE0 | (code >> 12)), (byte)(0x80 | ((code >> 6) & 0x3F)), (byte)(0x80 | (code & 0x3F))],
            _ => [(byte)(0xF0 | (code >> 18)), (byte)(0x80 | ((code >> 12) & 0x3F)), (byte)(0x80 | ((code >> 6) & 0x3F)), (byte)(0x80 | (code & 0x3F))]
        };
    }

    /// <summary>Matches an entity or a character reference (<c>&amp;(#?[%w]+);</c>).</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("&(#?[A-Za-z0-9]+);", RegexOptions.CultureInvariant)]
    private static partial Regex EntityPattern();
}
