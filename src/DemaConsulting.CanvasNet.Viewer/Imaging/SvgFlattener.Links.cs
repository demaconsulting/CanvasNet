using System.Text;
using System.Xml;
using DemaConsulting.CanvasNet.Packaging;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     The link scanner and the rewrite of <see cref="SvgFlattener"/>: where the link attributes of the <c>image</c>
///     elements are in the text, and the copy with only their values changed.
/// </summary>
public static partial class SvgFlattener
{
    /// <summary>
    ///     The namespace of the legacy <c>xlink:href</c> attribute.
    /// </summary>
    private const string XlinkNamespace = "http://www.w3.org/1999/xlink";

    /// <summary>
    ///     The prefix of a replaced link.
    /// </summary>
    internal const string DataUriPrefix = "data:image/png;base64,";

    /// <summary>
    ///     The link of one <c>image</c> element: the attribute the SVG library reads, and where its value is in the text.
    /// </summary>
    /// <param name="Start">The index in the text of the first character of the value, after the opening quote.</param>
    /// <param name="Length">The length of the value as written, without the quotes.</param>
    /// <param name="Quote">The quote character that delimits the value.</param>
    /// <param name="Href">The value as the SVG library reads it: entities decoded, white space trimmed.</param>
    /// <param name="Line">The 1-based line of the attribute.</param>
    /// <param name="Column">The 1-based column of the attribute name.</param>
    internal sealed record ImageLink(int Start, int Length, char Quote, string Href, int Line, int Column);

    /// <summary>
    ///     The rewritten text and what changed.
    /// </summary>
    /// <param name="Text">The text of the copy.</param>
    /// <param name="Replaced">The links replaced by pictures, in order of first appearance.</param>
    /// <param name="Rebased">The links rewritten for the folder of the copy, in order of first appearance.</param>
    private sealed record Rewritten(string Text, List<FlattenedImage> Replaced, List<RebasedLink> Rebased);

    /// <summary>
    ///     Finds the link of every <c>image</c> element and where its value is in the text.
    /// </summary>
    /// <param name="bytes">The bytes of the drawing, which the XML reader reads.</param>
    /// <param name="text">The text of the drawing without the byte order mark: the same characters the reader sees.</param>
    /// <returns>
    ///     The links in document order: for each element named <c>image</c> (in any namespace, as the SVG library
    ///     matches it), its <c>href</c>, or its <c>xlink:href</c> when it has no <c>href</c>; an element with neither has
    ///     none. Text in comments and character data is not markup and is never found.
    /// </returns>
    /// <exception cref="XmlException">Thrown when the drawing is not well-formed, declares a DTD, or is too large.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a position the reader reports does not match the text (a defect).</exception>
    /// <remarks>
    ///     The reader that finds the elements is the hardened one of the SVG library (<see cref="SafeXml"/>), so the same
    ///     documents are refused. The reader reports the line and the column of each attribute name; the value is found
    ///     from there in the raw text (white space, <c>=</c>, white space, a quote), so that the copy can change exactly
    ///     the characters of the value and nothing else.
    /// </remarks>
    internal static List<ImageLink> FindImageLinks(byte[] bytes, string text)
    {
        var lineStarts = LineStarts(text);
        var links = new List<ImageLink>();
        using var stream = new MemoryStream(bytes, false);
        using var reader = SafeXml.CreateReader(stream, MaxInputCharacters);
        var position = (IXmlLineInfo)reader;
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "image")
            {
                continue;
            }

            // The unprefixed href wins over xlink:href, as in the SVG library
            ImageLink? plain = null;
            ImageLink? legacy = null;
            for (var more = reader.MoveToFirstAttribute(); more; more = reader.MoveToNextAttribute())
            {
                if (reader.LocalName != "href")
                {
                    continue;
                }

                if (reader.NamespaceURI.Length == 0)
                {
                    plain = Locate(text, lineStarts, reader, position);
                }
                else if (reader.NamespaceURI == XlinkNamespace)
                {
                    legacy = Locate(text, lineStarts, reader, position);
                }
            }

            reader.MoveToElement();
            if ((plain ?? legacy) is { } link)
            {
                links.Add(link);
            }
        }

        return links;
    }

    /// <summary>
    ///     Finds the index in the text where each line starts.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The start of line 1, line 2, and so on; a line break is <c>\r\n</c>, <c>\r</c>, or <c>\n</c>, as XML counts them.</returns>
    private static List<int> LineStarts(string text)
    {
        var starts = new List<int> { 0 };
        var index = 0;
        while (index < text.Length)
        {
            // A carriage return followed by a line feed is one line break
            var character = text[index];
            index += character == '\r' && index + 1 < text.Length && text[index + 1] == '\n' ? 2 : 1;
            if (character is '\r' or '\n')
            {
                starts.Add(index);
            }
        }

        return starts;
    }

    /// <summary>
    ///     Finds the value of the attribute the reader is on in the raw text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="lineStarts">The start of each line.</param>
    /// <param name="reader">The reader, on an attribute.</param>
    /// <param name="position">The line information of the reader.</param>
    /// <returns>The link.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the text at the reported position is not the attribute.</exception>
    private static ImageLink Locate(string text, List<int> lineStarts, XmlReader reader, IXmlLineInfo position)
    {
        // The reader counts columns in UTF-16 characters from 1, so the name starts here
        var name = reader.Name;
        var index = lineStarts[position.LineNumber - 1] + position.LinePosition - 1;
        if (index < 0 || index + name.Length > text.Length || string.CompareOrdinal(text, index, name, 0, name.Length) != 0)
        {
            throw new InvalidOperationException($"The attribute '{name}' was not found at line {position.LineNumber}, column {position.LinePosition}.");
        }

        // name S? '=' S? quote value quote
        index = SkipSpace(text, index + name.Length);
        if (index >= text.Length || text[index] != '=')
        {
            throw new InvalidOperationException($"The attribute '{name}' at line {position.LineNumber} has no '='.");
        }

        index = SkipSpace(text, index + 1);
        var quote = index < text.Length ? text[index] : '\0';
        var end = quote is '"' or '\'' ? text.IndexOf(quote, index + 1) : -1;
        if (end < 0)
        {
            throw new InvalidOperationException($"The value of the attribute '{name}' at line {position.LineNumber} is not quoted.");
        }

        return new ImageLink(index + 1, end - index - 1, quote, reader.Value.Trim(), position.LineNumber, position.LinePosition);
    }

    /// <summary>
    ///     Skips XML white space.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">Where to start.</param>
    /// <returns>The index of the first character that is not white space, or the length of the text.</returns>
    private static int SkipSpace(string text, int index)
    {
        while (index < text.Length && text[index] is ' ' or '\t' or '\r' or '\n')
        {
            index++;
        }

        return index;
    }

    /// <summary>
    ///     Builds the text of the copy: each link the recorder drew becomes a PNG data URI and, when the copy is written
    ///     to another folder, each relative link that is left is rebased; all other text is kept.
    /// </summary>
    /// <param name="document">The drawing.</param>
    /// <param name="links">The links, in document order.</param>
    /// <param name="recorder">The recorder, with the pictures it drew.</param>
    /// <param name="inputPath">The path of the drawing.</param>
    /// <param name="request">The request.</param>
    /// <returns>The text and what changed.</returns>
    /// <exception cref="ViewerException">Thrown with code <c>size-limit</c> as soon as the copy would exceed the limit.</exception>
    private static Rewritten Rewrite(SvgText document, List<ImageLink> links, Recorder recorder, string inputPath, FlattenRequest request)
    {
        var inputFolder = FolderOf(inputPath);
        var outputFolder = FolderOf(request.Output);
        var rebase = Path.GetRelativePath(outputFolder, inputFolder) != ".";
        var text = document.Text;
        var builder = new StringBuilder(text.Length);
        var replaced = new List<FlattenedImage>();
        var rebased = new List<RebasedLink>();
        var size = (long)document.Bytes.Length;
        var copied = 0;
        foreach (var link in links)
        {
            // A picture that was drawn replaces the link; a relative link that is left finds its file from the copy
            string? value = null;
            if (recorder.Captures.TryGetValue(link.Href, out var capture))
            {
                value = capture.DataUri;
                Count(replaced, capture, link.Href);
            }
            else if (rebase && RebaseLink(link.Href, link.Quote, inputFolder, outputFolder) is { } moved)
            {
                value = moved;
                if (!rebased.Exists(r => r.Href == link.Href))
                {
                    rebased.Add(new RebasedLink(link.Href, moved));
                }
            }

            if (value is null)
            {
                continue;
            }

            // The size is checked as the copy grows, so a picture linked many times cannot use up the memory first
            size += value.Length - link.Length;
            CheckSize(size, request.MaxOutputBytes);
            builder.Append(text, copied, link.Start - copied).Append(value);
            copied = link.Start + link.Length;
        }

        builder.Append(text, copied, text.Length - copied);
        return new Rewritten(builder.ToString(), replaced, rebased);
    }

    /// <summary>
    ///     Adds one element to the entry of a replaced link, creating the entry the first time.
    /// </summary>
    /// <param name="replaced">The entries so far.</param>
    /// <param name="capture">The picture of the link.</param>
    /// <param name="href">The link.</param>
    private static void Count(List<FlattenedImage> replaced, Capture capture, string href)
    {
        var index = replaced.FindIndex(r => r.Href == href);
        if (index < 0)
        {
            replaced.Add(new FlattenedImage(href, capture.Format, capture.Width, capture.Height, 1, capture.PngBytes));
        }
        else
        {
            replaced[index] = replaced[index] with { Elements = replaced[index].Elements + 1 };
        }
    }

    /// <summary>
    ///     Gets the full path of the folder of a file.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The full path of its folder.</returns>
    private static string FolderOf(string path)
    {
        var full = Path.GetFullPath(path);
        return Path.GetDirectoryName(full) ?? Path.GetPathRoot(full) ?? full;
    }

    /// <summary>
    ///     Rewrites a relative link so that it finds the same file from another folder.
    /// </summary>
    /// <param name="href">The link as the SVG library reads it.</param>
    /// <param name="quote">The quote of the attribute, which the new value must escape.</param>
    /// <param name="inputFolder">The folder the link is relative to now.</param>
    /// <param name="outputFolder">The folder it must be relative to.</param>
    /// <returns>
    ///     The new attribute value (escaped for XML), or <see langword="null"/> for a link that is not a relative path: one
    ///     with a URI scheme (<c>data:</c>, <c>http:</c>, <c>file:</c>), a same-document <c>#</c> link, an empty link, and a
    ///     rooted path. The query and the fragment are kept as written; the path is percent-encoded per segment, and is a
    ///     <c>file:</c> URI when no relative path exists (another drive).
    /// </returns>
    internal static string? RebaseLink(string href, char quote, string inputFolder, string outputFolder)
    {
        if (href.Length == 0 || href[0] == '#' || HasScheme(href))
        {
            return null;
        }

        // The path ends at the first '?' or '#' (as the resolver reads it); it is decoded before it is resolved
        var cut = href.AsSpan().IndexOfAny('?', '#');
        var path = cut < 0 ? href : href[..cut];
        var suffix = cut < 0 ? string.Empty : href[cut..];
        var decoded = path.Contains('%', StringComparison.Ordinal) ? Uri.UnescapeDataString(path) : path;
        if (decoded.Length == 0 || Path.IsPathRooted(decoded))
        {
            return null;
        }

        var full = Path.GetFullPath(Path.Combine(inputFolder, decoded));
        var relative = Path.GetRelativePath(outputFolder, full);
        var moved = Path.IsPathRooted(relative)
            ? new Uri(full).AbsoluteUri
            : string.Join('/', relative.Replace(Path.DirectorySeparatorChar, '/').Split('/').Select(Uri.EscapeDataString));
        return moved + EscapeAttribute(suffix, quote);
    }

    /// <summary>
    ///     Tells whether a link starts with a URI scheme, as the resolver decides it.
    /// </summary>
    /// <param name="href">The link.</param>
    /// <returns>
    ///     <see langword="true"/> for an ASCII letter followed by letters, digits, <c>+</c>, <c>-</c>, or <c>.</c> and a
    ///     colon at index 2 or later (a single letter and a colon is a drive letter, not a scheme).
    /// </returns>
    private static bool HasScheme(string href)
    {
        var colon = href.IndexOf(':', StringComparison.Ordinal);
        if (colon < 2 || !char.IsAsciiLetter(href[0]))
        {
            return false;
        }

        for (var i = 1; i < colon; i++)
        {
            if (!char.IsAsciiLetterOrDigit(href[i]) && href[i] is not ('+' or '-' or '.'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Escapes text for an attribute value delimited by a quote.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <param name="quote">The quote of the attribute.</param>
    /// <returns>The text with <c>&amp;</c>, <c>&lt;</c>, and the quote written as references.</returns>
    private static string EscapeAttribute(string value, char quote)
    {
        var escaped = value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal);
        return quote == '"'
            ? escaped.Replace("\"", "&quot;", StringComparison.Ordinal)
            : escaped.Replace("'", "&apos;", StringComparison.Ordinal);
    }
}
