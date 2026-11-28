using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Viewer;

namespace DemaConsulting.CanvasNet.Tool.PandocFilter;

/// <summary>
///     The attributes of a Pandoc element: <c>[id, [class], [[key, value]]]</c>.
/// </summary>
/// <param name="Id">The identifier; empty when there is none.</param>
/// <param name="Classes">The classes, in order.</param>
/// <param name="Attributes">The key-value attributes, in order (a repeated key appears each time).</param>
internal sealed record PandocAttr(string Id, IReadOnlyList<string> Classes, IReadOnlyList<KeyValuePair<string, string>> Attributes);

/// <summary>
///     The parts of an <c>Image</c> element the filter reads.
/// </summary>
/// <param name="Attr">The attributes.</param>
/// <param name="Src">The picture's source as written.</param>
internal sealed record PandocImage(PandocAttr Attr, string Src);

/// <summary>
///     The parts of a <c>CodeBlock</c> element the filter reads.
/// </summary>
/// <param name="Attr">The attributes.</param>
/// <param name="Text">The block's text.</param>
internal sealed record PandocCodeBlock(PandocAttr Attr, string Text);

/// <summary>
///     Reads, walks, and writes a Pandoc document in its JSON form, touching only the elements the filter handles.
/// </summary>
/// <remarks>
///     <para>
///         The document is a <see cref="JsonNode"/> tree: an element the filter does not handle is never interpreted and is
///         written back as it was read (an unmodified number keeps its text). Only <c>Image</c> and <c>CodeBlock</c> are
///         read (and the types of an image's ancestors, to tell running text from a picture on its own), and only
///         <c>Image</c>, <c>Para</c>, <c>Plain</c>, <c>Figure</c>, <c>Span</c>, and the metadata
///         <c>MetaList</c> of <c>MetaString</c> are built; a touched element of an unexpected shape is
///         <see cref="ErrorCodes.CorruptInput"/>.
///     </para>
///     <para>
///         The reader and the writer allow <see cref="MaxDepth"/> levels (the default of 64 is exceeded by a document with
///         about ten levels of nested lists, quotes, or tables), and the walk uses an explicit stack. The output is UTF-8
///         without a byte order mark, non-ASCII text unescaped. Stateless and thread-safe; a document tree itself is not
///         safe for concurrent use.
///     </para>
/// </remarks>
internal static class PandocAst
{
    /// <summary>
    ///     The deepest nesting the reader and the writer accept.
    /// </summary>
    internal const int MaxDepth = 4096;

    /// <summary>
    ///     The metadata key the warnings are returned in (reserved; an incoming value is replaced).
    /// </summary>
    internal const string WarningsKey = "canvasnet-warnings";

    /// <summary>
    ///     The class of the placeholder <c>Span</c> the shim replaces with a caption parsed as Markdown (reserved).
    /// </summary>
    internal const string MarkdownClass = "canvasnet-markdown";

    /// <summary>
    ///     The attribute of the placeholder <c>Span</c> that holds the caption's Markdown.
    /// </summary>
    internal const string MarkdownAttribute = "markdown";

    /// <summary>
    ///     The oldest Pandoc API the filter reads (1.23, Pandoc 3, added <c>Figure</c>).
    /// </summary>
    private const int MinApiMinor = 23;

    /// <summary>
    ///     The options of the JSON reader.
    /// </summary>
    private static readonly JsonDocumentOptions ReaderOptions = new() { MaxDepth = MaxDepth };

    /// <summary>
    ///     The inline elements that hold a line of inlines an element can stand alone in, which <see cref="IsInRunningText"/>
    ///     moves up through.
    /// </summary>
    private static readonly HashSet<string> InlineContainers = new(StringComparer.Ordinal)
    {
        "Emph", "Underline", "Strong", "Strikeout", "Superscript", "Subscript", "SmallCaps", "Quoted", "Cite", "Link", "Span"
    };

    /// <summary>
    ///     The inline elements that are white space, which do not make a line running text.
    /// </summary>
    private static readonly HashSet<string> WhiteSpaceInlines = new(StringComparer.Ordinal) { "Space", "SoftBreak", "LineBreak" };

    /// <summary>
    ///     The options of the JSON writer: compact, non-ASCII text unescaped (Pandoc reads it).
    /// </summary>
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false,
        MaxDepth = MaxDepth,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    ///     Reads a Pandoc document.
    /// </summary>
    /// <param name="utf8">The document as UTF-8 JSON; a leading byte order mark is skipped.</param>
    /// <param name="pandocVersion">The version of Pandoc, for messages, or <see langword="null"/> when unknown.</param>
    /// <returns>The document's root object.</returns>
    /// <exception cref="ToolException">
    ///     Thrown with <see cref="ErrorCodes.CorruptInput"/> when the input is not a Pandoc JSON document, or its API version
    ///     is older than 1.23 (Pandoc 3) or has another major version.
    /// </exception>
    internal static JsonObject Parse(ReadOnlySpan<byte> utf8, string? pandocVersion)
    {
        if (utf8.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            utf8 = utf8[3..];
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(utf8, null, ReaderOptions);
        }
        catch (JsonException ex)
        {
            throw Corrupt($"The input is not a Pandoc JSON document: {ex.Message}");
        }

        if (root is not JsonObject document || document["blocks"] is not JsonArray || document["meta"] is not JsonObject)
        {
            throw Corrupt("The input is not a Pandoc JSON document: it has no blocks and meta.");
        }

        // The API version: major 1, minor 23 or later (a newer minor is fine, as the touched shapes are checked)
        var version = document["pandoc-api-version"] as JsonArray;
        var numbers = version?.Select(v => v is JsonValue value && value.TryGetValue<int>(out var n) ? n : -1).ToList() ?? [];
        if (numbers.Count < 2 || numbers.Contains(-1))
        {
            throw Corrupt("The input is not a Pandoc JSON document: it has no pandoc-api-version.");
        }

        if (numbers[0] != 1 || numbers[1] < MinApiMinor)
        {
            var pandoc = pandocVersion == null ? string.Empty : $" (Pandoc {pandocVersion})";
            throw new ToolException(
                ErrorCodes.CorruptInput,
                $"The document has Pandoc API version {string.Join('.', numbers)}{pandoc}; the pandoc-filter command reads version 1.{MinApiMinor} or a later 1.x.",
                "This filter needs Pandoc 3 or later.");
        }

        return document;
    }

    /// <summary>
    ///     Writes a Pandoc document.
    /// </summary>
    /// <param name="document">The document's root object.</param>
    /// <returns>The document as UTF-8 JSON without a byte order mark.</returns>
    internal static byte[] Write(JsonObject document)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            document.WriteTo(writer);
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    ///     Finds every element of a type, in the order Pandoc's Lua filter visits it: the metadata first, then the blocks,
    ///     each in document order, an element's children before the element itself.
    /// </summary>
    /// <param name="document">The document's root object.</param>
    /// <param name="type">The element type, for example <c>Image</c>.</param>
    /// <returns>The elements: every JSON object whose <c>t</c> is the type.</returns>
    /// <remarks>
    ///     Pandoc runs a filter's functions typewise: every <c>Image</c> (in the metadata, then in the blocks) before any
    ///     <c>CodeBlock</c>, bottom-up, so an image inside the description of another is visited first; a replacement is
    ///     never visited again. The walk uses an explicit stack, so a deep document cannot overflow the call stack.
    /// </remarks>
    internal static IReadOnlyList<JsonObject> FindAll(JsonObject document, string type)
    {
        var found = new List<JsonObject>();
        foreach (var root in new[] { document["meta"], document["blocks"] }.OfType<JsonNode>())
        {
            // Post-order: a node is pushed again as visited after its children
            var stack = new Stack<(JsonNode Node, bool Visited)>();
            stack.Push((root, false));
            while (stack.Count > 0)
            {
                var (node, visited) = stack.Pop();
                if (visited)
                {
                    if (node is JsonObject element && TypeOf(element) == type)
                    {
                        found.Add(element);
                    }

                    continue;
                }

                stack.Push((node, true));
                IEnumerable<JsonNode?> children = node switch
                {
                    JsonObject obj => obj.Select(p => p.Value),
                    JsonArray array => array,
                    _ => []
                };
                foreach (var child in children.OfType<JsonNode>().Reverse())
                {
                    stack.Push((child, false));
                }
            }
        }

        return found;
    }

    /// <summary>
    ///     Reads an <c>Image</c> element: <c>{"t":"Image","c":[Attr, [Inline], [src, title]]}</c>.
    /// </summary>
    /// <param name="node">The element.</param>
    /// <returns>Its attributes and source.</returns>
    /// <exception cref="ToolException">Thrown with <see cref="ErrorCodes.CorruptInput"/> when the element has another shape.</exception>
    internal static PandocImage ReadImage(JsonObject node)
    {
        if (node["c"] is JsonArray { Count: 3 } content && content[1] is JsonArray && content[2] is JsonArray { Count: 2 } target
            && StringOf(target[0]) is { } src && StringOf(target[1]) != null && ReadAttr(content[0]) is { } attr)
        {
            return new PandocImage(attr, src);
        }

        throw Malformed("Image");
    }

    /// <summary>
    ///     Determines whether an inline element (an <c>Image</c>) stands in running text: in a line with other text or
    ///     pictures, rather than alone in its paragraph or figure.
    /// </summary>
    /// <param name="element">The element, in a document read by <see cref="Parse"/> (its ancestors are read).</param>
    /// <returns>
    ///     <see langword="true"/> when the inlines that hold the element hold anything but the element and white space
    ///     (<c>Space</c>, <c>SoftBreak</c>, <c>LineBreak</c>). While they hold only the element, the check moves up
    ///     through an inline container (<c>Emph</c>, <c>Underline</c>, <c>Strong</c>, <c>Strikeout</c>,
    ///     <c>Superscript</c>, <c>Subscript</c>, <c>SmallCaps</c>, <c>Cite</c>, <c>Link</c>, <c>Span</c>) and checks that
    ///     container's line; inside <c>Quoted</c> it is running text (the quotation marks are text). Any other owner
    ///     (<c>Para</c>, <c>Plain</c> of a figure or a table cell, <c>Header</c>, the description of an <c>Image</c>, the
    ///     metadata) ends the check: <see langword="false"/>, the element is shown on its own (display).
    /// </returns>
    /// <remarks>
    ///     Pandoc makes an image alone in its paragraph a <c>Figure</c> (or a <c>Para</c> of the image alone when its
    ///     description is empty), and keeps an image in a sentence in the sentence's <c>Para</c>; this check tells the two
    ///     apart. A trailing non-breaking space, Pandoc's way to keep a lone image out of a figure, is text, so such an
    ///     image is in running text too.
    /// </remarks>
    internal static bool IsInRunningText(JsonObject element)
    {
        JsonNode current = element;
        while (current.Parent is JsonArray inlines)
        {
            var self = current;
            if (inlines.Any(n => !ReferenceEquals(n, self) && !(n is JsonObject o && TypeOf(o) is { } t && WhiteSpaceInlines.Contains(t))))
            {
                return true;
            }

            // The line holds only the element: move up through an inline container, stop at any other owner
            if (OwnerOf(inlines) is not { } owner || TypeOf(owner) is not { } type || !InlineContainers.Contains(type))
            {
                return false;
            }

            if (type == "Quoted")
            {
                return true;
            }

            current = owner;
        }

        return false;
    }

    /// <summary>
    ///     Reads a <c>CodeBlock</c> element: <c>{"t":"CodeBlock","c":[Attr, text]}</c>.
    /// </summary>
    /// <param name="node">The element.</param>
    /// <returns>Its attributes and text.</returns>
    /// <exception cref="ToolException">Thrown with <see cref="ErrorCodes.CorruptInput"/> when the element has another shape.</exception>
    internal static PandocCodeBlock ReadCodeBlock(JsonObject node)
    {
        if (node["c"] is JsonArray { Count: 2 } content && StringOf(content[1]) is { } text && ReadAttr(content[0]) is { } attr)
        {
            return new PandocCodeBlock(attr, text);
        }

        throw Malformed("CodeBlock");
    }

    /// <summary>
    ///     Points an <c>Image</c> element (read by <see cref="ReadImage"/>) at a picture: only the source and the key-value
    ///     attributes change; the description, the title, the identifier, and the classes stay.
    /// </summary>
    /// <param name="node">The element.</param>
    /// <param name="src">The new source.</param>
    /// <param name="attributes">The key-value attributes that stay.</param>
    internal static void RewriteImage(JsonObject node, string src, IReadOnlyList<KeyValuePair<string, string>> attributes)
    {
        var content = (JsonArray)node["c"]!;
        ((JsonArray)content[0]!)[2] = KeyValues(attributes);
        ((JsonArray)content[2]!)[0] = src;
    }

    /// <summary>
    ///     Determines whether a <c>Code</c> element is math the shim made: its class is <c>canvasnet-math</c>.
    /// </summary>
    /// <param name="node">The <c>Code</c> element.</param>
    /// <returns><see langword="true"/> for the shim's math element.</returns>
    internal static bool IsMath(JsonObject node)
    {
        return node["c"] is JsonArray { Count: 2 } content && ReadAttr(content[0]) is { } attr && attr.Classes.Contains("canvasnet-math");
    }

    /// <summary>
    ///     Builds a <c>Str</c> element.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The element.</returns>
    internal static JsonObject Str(string text)
    {
        return Element("Str", JsonValue.Create(text));
    }

    /// <summary>
    ///     Builds an <c>Image</c> element.
    /// </summary>
    /// <param name="attr">The attributes.</param>
    /// <param name="description">The description (alternative text) inlines.</param>
    /// <param name="src">The source.</param>
    /// <returns>The element.</returns>
    internal static JsonObject Image(PandocAttr attr, IEnumerable<JsonNode> description, string src)
    {
        return Element("Image", new JsonArray(Attr(attr), new JsonArray([.. description]), new JsonArray(src, string.Empty)));
    }

    /// <summary>
    ///     Builds a <c>Para</c> element.
    /// </summary>
    /// <param name="inlines">The inlines.</param>
    /// <returns>The element.</returns>
    internal static JsonObject Para(params JsonNode[] inlines)
    {
        return Element("Para", new JsonArray(inlines));
    }

    /// <summary>
    ///     Builds a <c>Plain</c> element.
    /// </summary>
    /// <param name="inlines">The inlines.</param>
    /// <returns>The element.</returns>
    internal static JsonObject Plain(params JsonNode[] inlines)
    {
        return Element("Plain", new JsonArray(inlines));
    }

    /// <summary>
    ///     Builds a <c>Figure</c> element: <c>{"t":"Figure","c":[Attr, [null, caption], body]}</c>, with no short caption.
    /// </summary>
    /// <param name="identifier">The figure's identifier; empty for none.</param>
    /// <param name="caption">The caption blocks.</param>
    /// <param name="body">The body blocks.</param>
    /// <returns>The element.</returns>
    internal static JsonObject Figure(string identifier, IEnumerable<JsonNode> caption, IEnumerable<JsonNode> body)
    {
        var attr = Attr(new PandocAttr(identifier, [], []));
        return Element("Figure", new JsonArray(attr, new JsonArray((JsonNode?)null, new JsonArray([.. caption])), new JsonArray([.. body])));
    }

    /// <summary>
    ///     Builds the placeholder <c>Span</c> of a caption, which the shim replaces with the caption parsed as Markdown
    ///     (Pandoc's own reader, so emphasis, math, and links work as before).
    /// </summary>
    /// <param name="markdown">The caption as written.</param>
    /// <returns>The element: an empty <c>Span</c> of class <see cref="MarkdownClass"/> whose <see cref="MarkdownAttribute"/> holds the caption.</returns>
    internal static JsonObject MarkdownPlaceholder(string markdown)
    {
        var attr = Attr(new PandocAttr(string.Empty, [MarkdownClass], [new(MarkdownAttribute, markdown)]));
        return Element("Span", new JsonArray(attr, new JsonArray()));
    }

    /// <summary>
    ///     Sets the warnings of the run in the metadata: <see cref="WarningsKey"/> as a <c>MetaList</c> of
    ///     <c>MetaString</c>, in order; the key is removed when there are none.
    /// </summary>
    /// <param name="document">The document's root object.</param>
    /// <param name="warnings">The warnings.</param>
    internal static void SetWarnings(JsonObject document, IReadOnlyList<string> warnings)
    {
        var meta = (JsonObject)document["meta"]!;
        meta.Remove(WarningsKey);
        if (warnings.Count > 0)
        {
            meta[WarningsKey] = Element("MetaList", new JsonArray([.. warnings.Select(w => (JsonNode)Element("MetaString", JsonValue.Create(w)))]));
        }
    }

    /// <summary>
    ///     Gets the type of an element: its <c>t</c> when that is a string.
    /// </summary>
    /// <param name="element">The JSON object.</param>
    /// <returns>The type, or <see langword="null"/> when the object is not an element.</returns>
    private static string? TypeOf(JsonObject element)
    {
        return element.TryGetPropertyValue("t", out var type) ? StringOf(type) : null;
    }

    /// <summary>
    ///     Gets the element that owns a node: the nearest ancestor (or the node itself) that is an element.
    /// </summary>
    /// <param name="node">The node, for example the inlines of a <c>Link</c>.</param>
    /// <returns>The element, or <see langword="null"/> when the node is not inside one.</returns>
    private static JsonObject? OwnerOf(JsonNode node)
    {
        for (JsonNode? current = node; current != null; current = current.Parent)
        {
            if (current is JsonObject element && TypeOf(element) != null)
            {
                return element;
            }
        }

        return null;
    }

    /// <summary>
    ///     Gets a JSON string.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The string, or <see langword="null"/> when the node is not a string.</returns>
    private static string? StringOf(JsonNode? node)
    {
        return node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
    }

    /// <summary>
    ///     Reads attributes: <c>[id, [class], [[key, value]]]</c>.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The attributes, or <see langword="null"/> when the node has another shape.</returns>
    private static PandocAttr? ReadAttr(JsonNode? node)
    {
        if (node is not JsonArray { Count: 3 } attr || StringOf(attr[0]) is not { } id || attr[1] is not JsonArray classes || attr[2] is not JsonArray pairs)
        {
            return null;
        }

        var classList = classes.Select(StringOf).ToList();
        var pairList = pairs.Select(p => p is JsonArray { Count: 2 } pair && StringOf(pair[0]) is { } key && StringOf(pair[1]) is { } value
            ? new KeyValuePair<string, string>(key, value)
            : (KeyValuePair<string, string>?)null).ToList();
        if (classList.Contains(null) || pairList.Contains(null))
        {
            return null;
        }

        return new PandocAttr(id, classList.OfType<string>().ToList(), pairList.Select(p => p!.Value).ToList());
    }

    /// <summary>
    ///     Builds attributes.
    /// </summary>
    /// <param name="attr">The attributes.</param>
    /// <returns>The JSON array.</returns>
    private static JsonArray Attr(PandocAttr attr)
    {
        return new JsonArray(attr.Id, new JsonArray([.. attr.Classes.Select(c => (JsonNode)JsonValue.Create(c))]), KeyValues(attr.Attributes));
    }

    /// <summary>
    ///     Builds key-value attributes.
    /// </summary>
    /// <param name="attributes">The attributes.</param>
    /// <returns>The JSON array of pairs.</returns>
    private static JsonArray KeyValues(IReadOnlyList<KeyValuePair<string, string>> attributes)
    {
        return new JsonArray([.. attributes.Select(a => (JsonNode)new JsonArray(a.Key, a.Value))]);
    }

    /// <summary>
    ///     Builds an element: <c>{"t": type, "c": content}</c>.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="content">The content.</param>
    /// <returns>The element.</returns>
    private static JsonObject Element(string type, JsonNode content)
    {
        return new JsonObject { ["t"] = type, ["c"] = content };
    }

    /// <summary>
    ///     Creates the error of an element of an unexpected shape.
    /// </summary>
    /// <param name="type">The element's type.</param>
    /// <returns>The exception to throw.</returns>
    private static ToolException Malformed(string type)
    {
        return Corrupt($"The document has a malformed {type} element.");
    }

    /// <summary>
    ///     Creates the error of an input that is not a Pandoc document.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The exception to throw.</returns>
    private static ToolException Corrupt(string message)
    {
        return new ToolException(ErrorCodes.CorruptInput, message, "The pandoc-filter command reads the document Pandoc writes with 'pandoc -t json' (Pandoc 3 or later).");
    }
}
