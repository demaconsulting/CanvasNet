using System.Collections.Frozen;
using System.Globalization;
using System.Text;

namespace DemaConsulting.CanvasNet.Mermaid.Syntax;

/// <summary>
///     The HTML clean-up Mermaid applies to every label it stores (its <c>sanitizeText</c>, which runs DOMPurify): text
///     without a <c>&lt;</c> is kept as written, and text with one is parsed as an HTML fragment, stripped of what
///     DOMPurify removes and written back as HTML.
/// </summary>
/// <remarks>
///     <para>
///         <b>What is reproduced.</b> DOMPurify's fast path (no <c>&lt;</c>, no change); the HTML tokenizer for text,
///         start and end tags, attributes, comments and character references; tags that are not closed before the end
///         of the text, which the HTML parser drops; DOMPurify's default allow lists of HTML elements and attributes
///         (with <c>data-</c> and <c>aria-</c> attributes) and its URI check, with <c>style</c> forbidden as Mermaid
///         configures it; removed elements keep their content unless DOMPurify forbids it (<c>script</c>, <c>style</c>,
///         <c>template</c>, <c>svg</c>, <c>math</c> and the like); Mermaid's hook that keeps the <c>target</c> of a link
///         (and adds <c>rel="noopener"</c> for <c>_blank</c>); and the HTML serialization (lower-case names, double-quoted
///         attributes, <c>&amp;amp;</c>, <c>&amp;lt;</c>, <c>&amp;gt;</c> and <c>&amp;nbsp;</c> escapes, no end tag for
///         void elements).
///     </para>
///     <para>
///         <b>Simplifications</b> (labels use a small inline subset of HTML): the tree is built with a plain open-element
///         stack (an end tag closes up to its open element and is ignored otherwise; there is no implied closing of
///         paragraphs, no table fixing and only the common case of the adoption of formatting elements closed out of order); only common named character
///         references are decoded (an unknown one is kept as text); SVG and MathML content is removed.
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use; it runs in time linear in the length of the text.
///     </para>
/// </remarks>
internal static class HtmlSanitizer
{
    /// <summary>The HTML elements DOMPurify allows by default (its <c>html</c> tag list), less the forbidden <c>style</c>.</summary>
    private static readonly FrozenSet<string> AllowedElements = new[]
    {
        "a", "abbr", "acronym", "address", "area", "article", "aside", "audio", "b", "bdi", "bdo", "big", "blink",
        "blockquote", "body", "br", "button", "canvas", "caption", "center", "cite", "code", "col", "colgroup",
        "content", "data", "datalist", "dd", "decorator", "del", "details", "dfn", "dialog", "dir", "div", "dl", "dt",
        "element", "em", "fieldset", "figcaption", "figure", "font", "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6",
        "head", "header", "hgroup", "hr", "html", "i", "img", "input", "ins", "kbd", "label", "legend", "li", "main",
        "map", "mark", "marquee", "menu", "menuitem", "meter", "nav", "nobr", "ol", "optgroup", "option", "output", "p",
        "picture", "pre", "progress", "q", "rp", "rt", "ruby", "s", "samp", "search", "section", "select", "shadow",
        "slot", "small", "source", "spacer", "span", "strike", "strong", "sub", "summary", "sup", "table", "tbody",
        "td", "template", "textarea", "tfoot", "th", "thead", "time", "tr", "track", "tt", "u", "ul", "var", "video",
        "wbr",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The HTML attributes DOMPurify allows by default (its <c>html</c> attribute list).</summary>
    private static readonly FrozenSet<string> AllowedAttributes = new[]
    {
        "accept", "action", "align", "alt", "autocapitalize", "autocomplete", "autopictureinpicture", "autoplay",
        "background", "bgcolor", "border", "capture", "cellpadding", "cellspacing", "checked", "cite", "class", "clear",
        "color", "cols", "colspan", "command", "commandfor", "controls", "controlslist", "coords", "crossorigin",
        "datetime", "decoding", "default", "dir", "disabled", "disablepictureinpicture", "disableremoteplayback",
        "download", "draggable", "enctype", "enterkeyhint", "exportparts", "face", "for", "headers", "height", "hidden",
        "high", "href", "hreflang", "id", "inert", "inputmode", "integrity", "ismap", "kind", "label", "lang", "list",
        "loading", "loop", "low", "max", "maxlength", "media", "method", "min", "minlength", "multiple", "muted", "name",
        "nonce", "noshade", "novalidate", "nowrap", "open", "optimum", "part", "pattern", "placeholder", "playsinline",
        "popover", "popovertarget", "popovertargetaction", "poster", "preload", "pubdate", "radiogroup", "readonly",
        "rel", "required", "rev", "reversed", "role", "rows", "rowspan", "spellcheck", "scope", "selected", "shape",
        "size", "sizes", "slot", "span", "srclang", "start", "src", "srcset", "step", "style", "summary", "tabindex",
        "title", "translate", "type", "usemap", "valign", "value", "width", "wrap", "xmlns",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The attributes whose value is never checked as a URI (DOMPurify's <c>URI_SAFE_ATTRIBUTES</c>).</summary>
    private static readonly FrozenSet<string> UriSafeAttributes = new[]
    {
        "alt", "class", "for", "id", "label", "name", "pattern", "placeholder", "role", "summary", "title", "value",
        "style", "xmlns",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The elements whose content is removed with them (DOMPurify's <c>FORBID_CONTENTS</c>, plus SVG and MathML).</summary>
    private static readonly FrozenSet<string> RemovedWithContent = new[]
    {
        "annotation-xml", "audio", "colgroup", "desc", "foreignobject", "head", "iframe", "math", "mi", "mn", "mo", "ms",
        "mtext", "noembed", "noframes", "noscript", "plaintext", "script", "selectedcontent", "style", "svg", "template",
        "thead", "title", "video", "xmp",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The elements whose content the tokenizer reads as raw text up to the matching end tag.</summary>
    private static readonly FrozenSet<string> RawTextElements = new[]
    {
        "script", "style", "xmp", "iframe", "noembed", "noframes", "textarea", "title",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The void elements, which have no content and no end tag.</summary>
    private static readonly FrozenSet<string> VoidElements = new[]
    {
        "area", "base", "basefont", "bgsound", "br", "col", "embed", "frame", "hr", "img", "input", "keygen", "link",
        "meta", "param", "source", "track", "wbr",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The Unicode replacement character, the value of an invalid numeric reference.</summary>
    private static readonly string ReplacementCharacter = char.ConvertFromUtf32(0xFFFD);

    /// <summary>The formatting elements, which the HTML parser reopens after an end tag closes them out of order.</summary>
    private static readonly FrozenSet<string> FormattingElements = new[]
    {
        "a", "b", "big", "code", "em", "font", "i", "nobr", "s", "small", "strike", "strong", "tt", "u",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The elements the fragment parser ignores (their content is kept).</summary>
    private static readonly FrozenSet<string> IgnoredElements = new[] { "html", "body", "head" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The named character references that are decoded.</summary>
    private static readonly FrozenDictionary<string, string> NamedReferences = new Dictionary<string, string>
    {
        ["amp"] = "&",
        ["lt"] = "<",
        ["gt"] = ">",
        ["quot"] = "\"",
        ["apos"] = "'",
        ["nbsp"] = "\u00a0",
        ["copy"] = "\u00a9",
        ["reg"] = "\u00ae",
        ["trade"] = "\u2122",
        ["hellip"] = "\u2026",
        ["mdash"] = "\u2014",
        ["ndash"] = "\u2013",
        ["laquo"] = "\u00ab",
        ["raquo"] = "\u00bb",
        ["lsquo"] = "\u2018",
        ["rsquo"] = "\u2019",
        ["ldquo"] = "\u201c",
        ["rdquo"] = "\u201d",
        ["bull"] = "\u2022",
        ["middot"] = "\u00b7",
        ["deg"] = "\u00b0",
        ["plusmn"] = "\u00b1",
        ["times"] = "\u00d7",
        ["divide"] = "\u00f7",
        ["euro"] = "\u20ac",
        ["pound"] = "\u00a3",
        ["yen"] = "\u00a5",
        ["cent"] = "\u00a2",
        ["sect"] = "\u00a7",
        ["para"] = "\u00b6",
        ["larr"] = "\u2190",
        ["rarr"] = "\u2192",
        ["uarr"] = "\u2191",
        ["darr"] = "\u2193",
        ["harr"] = "\u2194",
        ["le"] = "\u2264",
        ["ge"] = "\u2265",
        ["ne"] = "\u2260",
        ["equals"] = "=",
        ["num"] = "#",
        ["semi"] = ";",
        ["colon"] = ":",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    ///     Sanitizes a label as Mermaid's <c>sanitizeText</c> does with its default configuration.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text itself when it has no <c>&lt;</c>; otherwise the cleaned HTML.</returns>
    public static string Sanitize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // DOMPurify's fast path: nothing that could be markup
        if (!text.Contains('<', StringComparison.Ordinal))
        {
            return text;
        }

        var tree = new Tree(new StringBuilder(text.Length + 16));
        var i = 0;
        var textStart = 0;
        while (i < text.Length)
        {
            if (text[i] != '<')
            {
                i++;
                continue;
            }

            // Flush the text before a possible tag
            var tagEnd = ReadMarkup(text, i, out var markup);
            if (tagEnd < 0)
            {
                // Not markup: the '<' is text
                i++;
                continue;
            }

            AppendText(tree, text[textStart..i]);
            i = tagEnd;
            textStart = tagEnd;
            if (markup is null)
            {
                // A comment, a bogus comment, an empty end tag or a tag cut off by the end of the text: removed
                continue;
            }

            if (markup.IsEnd)
            {
                CloseElement(tree, markup.Name);
                continue;
            }

            // A raw-text element takes everything up to its end tag
            if (RawTextElements.Contains(markup.Name))
            {
                var close = IndexOfEndTag(text, i, markup.Name);
                var content = text[i..(close < 0 ? text.Length : close)];
                i = close < 0 ? text.Length : SkipTag(text, close);
                textStart = i;
                if (!RemovedWithContent.Contains(markup.Name))
                {
                    OpenElement(tree, markup);
                    AppendText(tree, content);
                    CloseElement(tree, markup.Name);
                }

                continue;
            }

            OpenElement(tree, markup);
        }

        AppendText(tree, text[textStart..]);

        // Close every element left open at the end, innermost first; formatting elements waiting to reopen are dropped
        for (var k = tree.Open.Count - 1; k >= 0; k--)
        {
            if (tree.Open[k].Kept)
            {
                tree.Output.Append("</").Append(tree.Open[k].Name).Append('>');
            }
        }

        return tree.Output.ToString();
    }

    /// <summary>
    ///     Reads the markup that starts at a <c>&lt;</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The position of the <c>&lt;</c>.</param>
    /// <param name="markup">The start or end tag, or null for markup that is removed (comments, unfinished tags).</param>
    /// <returns>The position after the markup, or -1 when the <c>&lt;</c> is plain text.</returns>
    private static int ReadMarkup(string text, int start, out Tag? markup)
    {
        markup = null;
        var next = start + 1 < text.Length ? text[start + 1] : '\0';

        // A start tag
        if (char.IsAsciiLetter(next))
        {
            return ReadStartTag(text, start + 1, out markup);
        }

        // An end tag, an empty end tag (ignored) or a bogus comment
        if (next == '/')
        {
            var after = start + 2 < text.Length ? text[start + 2] : '\0';
            if (char.IsAsciiLetter(after))
            {
                var nameEnd = start + 2;
                while (nameEnd < text.Length && !IsTagBreak(text[nameEnd]))
                {
                    nameEnd++;
                }

                var close = text.IndexOf('>', nameEnd);
                if (close < 0)
                {
                    return text.Length;
                }

                markup = new Tag(text[(start + 2)..nameEnd].ToLowerInvariant(), true, []);
                return close + 1;
            }

            if (start + 2 >= text.Length)
            {
                return -1;
            }

            var end = text.IndexOf('>', start + 2);
            return end < 0 ? text.Length : end + 1;
        }

        // A comment or a bogus comment
        if (next == '!' || next == '?')
        {
            if (string.CompareOrdinal(text, start, "<!--", 0, 4) == 0)
            {
                var end = text.IndexOf("-->", start + 4, StringComparison.Ordinal);
                return end < 0 ? text.Length : end + 3;
            }

            var close = text.IndexOf('>', start + 2);
            return close < 0 ? text.Length : close + 1;
        }

        return -1;
    }

    /// <summary>
    ///     Reads a start tag after its <c>&lt;</c>: the name and the attributes.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="nameStart">The position of the name.</param>
    /// <param name="markup">The tag, or null when the end of the text cuts it off.</param>
    /// <returns>The position after the tag.</returns>
    private static int ReadStartTag(string text, int nameStart, out Tag? markup)
    {
        markup = null;
        var i = nameStart;
        while (i < text.Length && !IsTagBreak(text[i]))
        {
            i++;
        }

        var name = text[nameStart..i].ToLowerInvariant();
        var attributes = new List<KeyValuePair<string, string>>();
        while (true)
        {
            // Skip white space and stray slashes between attributes
            while (i < text.Length && (IsSpace(text[i]) || text[i] == '/'))
            {
                i++;
            }

            if (i >= text.Length)
            {
                return text.Length;
            }

            if (text[i] == '>')
            {
                markup = new Tag(name, false, attributes);
                return i + 1;
            }

            // The attribute name (an '=' at its start belongs to it)
            var attributeStart = i;
            i++;
            while (i < text.Length && !IsSpace(text[i]) && text[i] is not ('/' or '>' or '='))
            {
                i++;
            }

            var attributeName = text[attributeStart..i].ToLowerInvariant();
            var value = string.Empty;
            var afterName = i;
            while (afterName < text.Length && IsSpace(afterName < text.Length ? text[afterName] : '\0'))
            {
                afterName++;
            }

            if (afterName < text.Length && text[afterName] == '=')
            {
                i = afterName + 1;
                while (i < text.Length && IsSpace(text[i]))
                {
                    i++;
                }

                if (i < text.Length && text[i] is '"' or '\'')
                {
                    var quote = text[i];
                    var close = text.IndexOf(quote, i + 1);
                    if (close < 0)
                    {
                        return text.Length;
                    }

                    value = DecodeReferences(text[(i + 1)..close]);
                    i = close + 1;
                }
                else
                {
                    var valueStart = i;
                    while (i < text.Length && !IsSpace(text[i]) && text[i] != '>')
                    {
                        i++;
                    }

                    value = DecodeReferences(text[valueStart..i]);
                }
            }

            // The first of two attributes of the same name wins
            if (!attributes.Exists(a => a.Key == attributeName))
            {
                attributes.Add(new KeyValuePair<string, string>(attributeName, value));
            }
        }
    }

    /// <summary>
    ///     Opens an element: writes its start tag when DOMPurify keeps it, and pushes it unless it is void.
    /// </summary>
    /// <param name="tree">The tree being built.</param>
    /// <param name="tag">The start tag.</param>
    private static void OpenElement(Tree tree, Tag tag)
    {
        if (IgnoredElements.Contains(tag.Name))
        {
            return;
        }

        tree.Reconstruct();
        Push(tree, tag);
    }

    /// <summary>
    ///     Pushes an element on the open elements and writes its start tag when it is kept.
    /// </summary>
    /// <param name="tree">The tree being built.</param>
    /// <param name="tag">The start tag.</param>
    private static void Push(Tree tree, Tag tag)
    {
        // Content inside a removed-with-content element is dropped, and so is everything below it
        var dropped = (tree.Open.Count > 0 && tree.Open[^1].Dropped) || RemovedWithContent.Contains(tag.Name);
        var kept = !dropped && AllowedElements.Contains(tag.Name);
        if (kept)
        {
            tree.Output.Append('<').Append(tag.Name);
            foreach (var (name, value) in CleanAttributes(tag))
            {
                tree.Output.Append(' ').Append(name).Append("=\"").Append(EscapeAttribute(value)).Append('"');
            }

            tree.Output.Append('>');
        }

        if (!VoidElements.Contains(tag.Name))
        {
            tree.Open.Add(new Element(tag, kept, dropped));
        }
    }

    /// <summary>
    ///     Closes the innermost open element of a name and every element inside it; formatting elements closed this way
    ///     reopen before the next content, as the HTML parser reconstructs them. An end tag without an open element is
    ///     ignored, except <c>&lt;/br&gt;</c>, which the HTML parser reads as <c>&lt;br&gt;</c>.
    /// </summary>
    /// <param name="tree">The tree being built.</param>
    /// <param name="name">The element name.</param>
    private static void CloseElement(Tree tree, string name)
    {
        if (name == "br")
        {
            OpenElement(tree, new Tag("br", false, []));
            return;
        }

        var open = tree.Open;
        var index = open.FindLastIndex(e => e.Name == name);
        if (index < 0)
        {
            // A formatting element waiting to reopen is forgotten
            var pending = tree.Pending.FindLastIndex(t => t.Name == name);
            if (pending >= 0)
            {
                tree.Pending.RemoveAt(pending);
            }

            return;
        }

        for (var k = open.Count - 1; k >= index; k--)
        {
            if (open[k].Kept)
            {
                tree.Output.Append("</").Append(open[k].Name).Append('>');
            }
        }

        // The formatting elements inside the closed one reopen, outermost first
        tree.Pending.InsertRange(0, open.Skip(index + 1).Where(e => FormattingElements.Contains(e.Name)).Select(e => e.Tag));
        open.RemoveRange(index, open.Count - index);
    }
    /// <summary>
    ///     Gets the attributes of a tag that DOMPurify keeps, with Mermaid's link-target hook applied.
    /// </summary>
    /// <remarks>
    ///     Mermaid sanitizes twice with a hook that takes the <c>target</c> of a link out before DOMPurify and puts it back
    ///     after, so a kept <c>target</c> ends up last, after a <c>rel="noopener"</c> that a <c>_blank</c> target adds.
    /// </remarks>
    /// <param name="tag">The tag.</param>
    /// <returns>The kept attributes in order.</returns>
    private static List<KeyValuePair<string, string>> CleanAttributes(Tag tag)
    {
        var kept = new List<KeyValuePair<string, string>>();
        string? target = null;
        foreach (var (name, value) in tag.Attributes)
        {
            if (name == "target" && tag.Name == "a")
            {
                target = value;
                continue;
            }

            var allowed = AllowedAttributes.Contains(name)
                || (name.StartsWith("data-", StringComparison.Ordinal) && name.Length > 5)
                || (name.StartsWith("aria-", StringComparison.Ordinal) && name.Length > 5);
            if (!allowed || (!UriSafeAttributes.Contains(name) && !IsAllowedUri(value, tag.Name)))
            {
                continue;
            }

            kept.Add(new KeyValuePair<string, string>(name, value));
        }

        if (target is null)
        {
            return kept;
        }

        // A link that opens a new window gets rel="noopener"; the target comes back last
        if (target == "_blank")
        {
            var rel = kept.FindIndex(a => a.Key == "rel");
            if (rel >= 0)
            {
                kept[rel] = new KeyValuePair<string, string>("rel", "noopener");
            }
            else
            {
                kept.Add(new KeyValuePair<string, string>("rel", "noopener"));
            }
        }

        kept.Add(new KeyValuePair<string, string>("target", target));
        return kept;
    }

    /// <summary>
    ///     Checks an attribute value as DOMPurify's <c>IS_ALLOWED_URI</c> does (after removing white space and control
    ///     characters): a known safe scheme, no scheme at all, or a <c>data:</c> URI on an image or media element.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="element">The element name.</param>
    /// <returns>Whether the value is kept.</returns>
    private static bool IsAllowedUri(string value, string element)
    {
        var compact = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c > '\u0020' && c != '\u00a0' && c != '\u1680' && c != '\u180e' && c != '\u205f' && c != '\u3000' && (c < '\u2000' || c > '\u2029'))
            {
                compact.Append(c);
            }
        }

        var text = compact.ToString();
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            return true;
        }

        // A scheme is ASCII letters, digits, '+', '.' and '-' before the colon
        var scheme = text[..colon];
        if (scheme.Length == 0 || !scheme.All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '.' or '-') || !char.IsAsciiLetter(scheme[0]))
        {
            return true;
        }

        var lower = scheme.ToLowerInvariant();
        if (lower is "http" or "https" or "ftp" or "ftps" or "mailto" or "tel" or "callto" or "sms" or "cid" or "xmpp" or "matrix")
        {
            return true;
        }

        return lower == "data" && element is "img" or "audio" or "video" or "source" or "track";
    }

    /// <summary>
    ///     Appends text to the output, decoded and escaped as the serializer writes a text node, unless it is inside a
    ///     removed-with-content element.
    /// </summary>
    /// <param name="tree">The tree being built.</param>
    /// <param name="text">The raw text.</param>
    private static void AppendText(Tree tree, string text)
    {
        if (text.Length == 0 || (tree.Open.Count > 0 && tree.Open[^1].Dropped))
        {
            return;
        }

        tree.Reconstruct();
        var output = tree.Output;
        foreach (var c in DecodeReferences(text))
        {
            switch (c)
            {
                case '&':
                    output.Append("&amp;");
                    break;
                case '<':
                    output.Append("&lt;");
                    break;
                case '>':
                    output.Append("&gt;");
                    break;
                case '\u00a0':
                    output.Append("&nbsp;");
                    break;
                default:
                    output.Append(c);
                    break;
            }
        }
    }

    /// <summary>
    ///     Escapes an attribute value as the serializer writes it.
    /// </summary>
    /// <param name="value">The decoded value.</param>
    /// <returns>The escaped value.</returns>
    private static string EscapeAttribute(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("\u00a0", "&nbsp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    /// <summary>
    ///     Decodes the character references of a text: numeric references and the known named ones with their
    ///     semicolon.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The decoded text.</returns>
    private static string DecodeReferences(string text)
    {
        var amp = text.IndexOf('&', StringComparison.Ordinal);
        if (amp < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        builder.Append(text, 0, amp);
        var i = amp;
        while (i < text.Length)
        {
            if (text[i] != '&' || !TryDecodeReference(text, i, out var decoded, out var length))
            {
                builder.Append(text[i]);
                i++;
                continue;
            }

            builder.Append(decoded);
            i += length;
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Decodes one character reference at an ampersand.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The position of the ampersand.</param>
    /// <param name="decoded">The decoded text.</param>
    /// <param name="length">The length of the reference.</param>
    /// <returns>Whether a reference was decoded.</returns>
    private static bool TryDecodeReference(string text, int start, out string decoded, out int length)
    {
        decoded = string.Empty;
        length = 0;
        var semicolon = text.IndexOf(';', start + 1);
        if (semicolon < 0 || semicolon - start > 32)
        {
            return false;
        }

        var body = text[(start + 1)..semicolon];
        length = semicolon + 1 - start;
        if (body.Length > 1 && body[0] == '#')
        {
            var hex = body[1] is 'x' or 'X';
            var digits = hex ? body[2..] : body[1..];
            var style = hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None;
            if (digits.Length == 0 || !int.TryParse(digits, style, CultureInfo.InvariantCulture, out var code))
            {
                return false;
            }

            // Null, surrogates and values beyond Unicode become the replacement character
            decoded = code is 0 or > 0x10FFFF || (code >= 0xD800 && code <= 0xDFFF) ? ReplacementCharacter : char.ConvertFromUtf32(code);
            return true;
        }

        return NamedReferences.TryGetValue(body, out decoded!);
    }

    /// <summary>
    ///     Finds the end tag of a raw-text element (case-insensitive).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="from">The position to search from.</param>
    /// <param name="name">The element name.</param>
    /// <returns>The position of the <c>&lt;/</c>, or -1.</returns>
    private static int IndexOfEndTag(string text, int from, string name)
    {
        var probe = "</" + name;
        for (var i = text.IndexOf(probe, from, StringComparison.OrdinalIgnoreCase); i >= 0; i = text.IndexOf(probe, i + 1, StringComparison.OrdinalIgnoreCase))
        {
            var after = i + probe.Length;
            if (after >= text.Length || IsTagBreak(text[after]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Gets the position after a tag that starts at a position.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The position of the <c>&lt;</c>.</param>
    /// <returns>The position after its <c>&gt;</c>, or the end of the text.</returns>
    private static int SkipTag(string text, int start)
    {
        var close = text.IndexOf('>', start);
        return close < 0 ? text.Length : close + 1;
    }

    /// <summary>
    ///     Gets a value indicating whether a character ends a tag name.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>Whether it does.</returns>
    private static bool IsTagBreak(char c) => IsSpace(c) || c is '/' or '>';

    /// <summary>
    ///     Gets a value indicating whether a character is HTML white space.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsSpace(char c) => c is ' ' or '\t' or '\n' or '\f' or '\r';

    /// <summary>
    ///     A start or end tag read from the text.
    /// </summary>
    /// <param name="Name">The lower-case element name.</param>
    /// <param name="IsEnd">Whether it is an end tag.</param>
    /// <param name="Attributes">The attributes in order, with lower-case names and decoded values.</param>
    private sealed record Tag(string Name, bool IsEnd, List<KeyValuePair<string, string>> Attributes);

    /// <summary>
    ///     An open element.
    /// </summary>
    /// <param name="Tag">The start tag.</param>
    /// <param name="Kept">Whether its tags are written.</param>
    /// <param name="Dropped">Whether its content is removed.</param>
    private sealed record Element(Tag Tag, bool Kept, bool Dropped)
    {
        /// <summary>Gets the element name.</summary>
        public string Name => Tag.Name;
    }

    /// <summary>
    ///     The state of the tree construction: the output, the open elements and the formatting elements that reopen
    ///     before the next content.
    /// </summary>
    /// <param name="Output">The output.</param>
    private sealed record Tree(StringBuilder Output)
    {
        /// <summary>Gets the open elements, outermost first.</summary>
        public List<Element> Open { get; } = [];

        /// <summary>Gets the formatting elements to reopen before the next content, outermost first.</summary>
        public List<Tag> Pending { get; } = [];

        /// <summary>
        ///     Reopens the pending formatting elements (the HTML parser's reconstruction of the active formatting
        ///     elements).
        /// </summary>
        public void Reconstruct()
        {
            if (Pending.Count == 0)
            {
                return;
            }

            var tags = Pending.ToList();
            Pending.Clear();
            foreach (var tag in tags)
            {
                Push(this, tag);
            }
        }
    }
}
