using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using DemaConsulting.CanvasNet.Packaging;

namespace DemaConsulting.CanvasNet.MathMl.Syntax;

/// <summary>
///     Reads MathML text into the equation tree (<see cref="MathElement"/>) with the location of every element,
///     attribute and text in the original text.
/// </summary>
/// <remarks>
///     <para>
///         <b>Pipeline.</b> The named character references are rewritten first (<see cref="CharacterReferences"/>);
///         the rewritten text is read by the hardened reader of the core (<see cref="SafeXml"/>: document types
///         prohibited, no resolver, a character cap), and every position it reports is mapped back to the original text.
///         Every <see cref="XmlException"/> becomes a located <see cref="MathMlSyntaxException"/> with a hint.
///     </para>
///     <para>
///         <b>Rules.</b> The root element must be <c>math</c>, in the MathML namespace or in no namespace. An element of
///         another namespace, and an element inside a token, is reported (<see cref="MathMlDiagnosticCodes.ElementUnsupported"/>)
///         and skipped with its subtree. The content of <c>annotation</c> (its text) and of <c>annotation-xml</c> (its
///         markup) is kept as raw text, never laid out and never reported. The text of a token is trimmed and its runs of
///         XML whitespace are collapsed to one space; whitespace between elements is dropped; other text directly inside a
///         layout element is kept (the layout draws it like an <c>mtext</c>) and reported. Links (<c>href</c> and the
///         <c>xlink</c> attributes) are reported and never followed; other attributes of other namespaces are ignored.
///     </para>
///     <para>
///         <b>Bounds.</b> The reader is iterative (an explicit stack), so a deeply nested text fails with
///         <see cref="MathMlDiagnosticCodes.LimitExceeded"/> (<see cref="MathMlLimits.MaxNestingDepth"/>), never with a
///         stack overflow; it also enforces <see cref="MathMlLimits.MaxElements"/> and
///         <see cref="MathMlLimits.MaxTokenLength"/>. The class is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static partial class MathMlReader
{
    /// <summary>The MathML namespace.</summary>
    public const string MathMlNamespace = "http://www.w3.org/1998/Math/MathML";

    /// <summary>The XLink namespace, whose attributes are links.</summary>
    private const string XLinkNamespace = "http://www.w3.org/1999/xlink";

    /// <summary>The strict UTF-8 encoder of the rewritten text: an unpaired surrogate is an error, never U+FFFD.</summary>
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    ///     Reads a text into the equation tree.
    /// </summary>
    /// <param name="text">The original text (at most <see cref="MathMlLimits.MaxTextSize"/> characters; the caller checks).</param>
    /// <param name="sourceName">The source name placed in every location.</param>
    /// <param name="limits">The limits of reading.</param>
    /// <returns>The root element, the element count, the pending reports and the annotations.</returns>
    /// <exception cref="MathMlSyntaxException">Thrown, located, when the text is not well-formed MathML or a limit is exceeded.</exception>
    public static MathReadResult Read(string text, string? sourceName, MathMlLimits limits)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(limits);

        // Rewrite the named references, then encode strictly (an unpaired surrogate cannot be XML)
        var prePass = CharacterReferences.Rewrite(text, sourceName);
        byte[] bytes;
        try
        {
            bytes = StrictUtf8.GetBytes(prePass.Text);
        }
        catch (EncoderFallbackException ex)
        {
            var (line, column) = CharacterReferences.PositionOf(text, IndexOfUnpairedSurrogate(text));
            throw new MathMlSyntaxException(ex, MathMlDiagnosticCodes.SyntaxError, "the text contains an unpaired surrogate, which is not a character", new MathMlLocation(sourceName, line, column), "remove the broken character");
        }

        using var stream = new MemoryStream(bytes, writable: false);
        var state = new ReadState(prePass.Map, limits);
        try
        {
            // The cap of the reader is the rewritten length (at most 9/4 of the original); the original was checked
            using var reader = SafeXml.CreateReader(stream, Math.Max(1, prePass.Text.Length + 1));
            state.Run(reader);
        }
        catch (XmlException ex)
        {
            // The refusal of a document type has no position of its own: it is located at the declaration
            var location = ex.LineNumber == 0 && prePass.DoctypeLocation is { } doctype ? doctype : prePass.Map.MapBack(ex.LineNumber, ex.LinePosition);
            var hint = prePass.DoctypeLocation is not null
                ? "remove the document type declaration: document types are not allowed, and the named character references of the W3C HTML+MathML set are built in"
                : "check that every element is closed and that a literal '&' or '<' in text is written as &amp; or &lt;";
            throw new MathMlSyntaxException(ex, MathMlDiagnosticCodes.SyntaxError, $"the text is not well-formed XML: {StripPosition(ex.Message)}", location, hint);
        }

        return new MathReadResult(state.Root!, state.ElementCount, [.. state.Reports], [.. state.Annotations]);
    }

    /// <summary>
    ///     Collapses the whitespace of token text: trims it and replaces every run of XML whitespace with one space.
    /// </summary>
    /// <param name="text">The raw text.</param>
    /// <returns>The collapsed text.</returns>
    public static string CollapseWhitespace(string text)
    {
        var result = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var c in text)
        {
            if (c is ' ' or '\t' or '\n' or '\r')
            {
                pendingSpace = result.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                result.Append(' ');
                pendingSpace = false;
            }

            result.Append(c);
        }

        return result.ToString();
    }

    /// <summary>
    ///     Removes the position suffix (" Line 3, position 7.") that the XML reader appends to its messages, because the
    ///     location of the exception is the one in the original text.
    /// </summary>
    /// <param name="message">The message of the XML reader.</param>
    /// <returns>The message without the suffix.</returns>
    private static string StripPosition(string message) => PositionSuffix().Replace(message, string.Empty).TrimEnd();

    /// <summary>
    ///     Finds the first unpaired surrogate of a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>Its index, or the length of the text when there is none.</returns>
    private static int IndexOfUnpairedSurrogate(string text)
    {
        var i = 0;
        while (i < text.Length)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i += 2;
            }
            else if (char.IsSurrogate(text[i]))
            {
                return i;
            }
            else
            {
                i++;
            }
        }

        return text.Length;
    }

    /// <summary>
    ///     The pattern of the position suffix of the messages of the XML reader.
    /// </summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\s*Line \d+, position \d+\.\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex PositionSuffix();

    /// <summary>
    ///     An element being read: its name, attributes and children so far.
    /// </summary>
    /// <param name="Name">The local name.</param>
    /// <param name="Attributes">The attributes.</param>
    /// <param name="Location">The location of the element.</param>
    private sealed record Frame(string Name, Dictionary<string, MathAttribute> Attributes, MathMlLocation Location)
    {
        /// <summary>Gets the children read so far.</summary>
        public List<MathNode> Children { get; } = [];

        /// <summary>Gets the raw text of a token, or of the current run of text inside a layout element.</summary>
        public StringBuilder Text { get; } = new();

        /// <summary>Gets or sets the location of the first character of <see cref="Text"/>.</summary>
        public MathMlLocation TextLocation { get; set; }

        /// <summary>Gets a value indicating whether the element is a token whose content is text.</summary>
        public bool IsToken => MathElement.IsTextTokenName(Name);
    }

    /// <summary>
    ///     The mutable state of one read: the stack of open elements, the counts, the reports and the annotations.
    /// </summary>
    private sealed class ReadState
    {
        /// <summary>The map of the rewritten positions.</summary>
        private readonly LocationMap _map;

        /// <summary>The limits of reading.</summary>
        private readonly MathMlLimits _limits;

        /// <summary>The open elements, the root first.</summary>
        private readonly List<Frame> _stack = [];

        /// <summary>The line information of the reader.</summary>
        private IXmlLineInfo? _lineInfo;

        /// <summary>
        ///     Initializes the state.
        /// </summary>
        /// <param name="map">The map of the rewritten positions.</param>
        /// <param name="limits">The limits.</param>
        public ReadState(LocationMap map, MathMlLimits limits)
        {
            _map = map;
            _limits = limits;
        }

        /// <summary>Gets the root element once read.</summary>
        public MathElement? Root { get; private set; }

        /// <summary>Gets the number of elements read.</summary>
        public int ElementCount { get; private set; }

        /// <summary>Gets the pending reports in text order.</summary>
        public List<MathMlPendingReport> Reports { get; } = [];

        /// <summary>Gets the annotations in text order.</summary>
        public List<MathMlAnnotation> Annotations { get; } = [];

        /// <summary>
        ///     Reads every node of the document.
        /// </summary>
        /// <param name="reader">The hardened reader.</param>
        /// <exception cref="XmlException">Thrown by the reader for text that is not well-formed.</exception>
        /// <exception cref="MathMlSyntaxException">Thrown, located, for a wrong root or a limit.</exception>
        public void Run(XmlReader reader)
        {
            _lineInfo = (IXmlLineInfo)reader;
            var hasNode = reader.Read();
            while (hasNode)
            {
                // A handler that consumed the subtree has already moved the reader to the next node
                var moved = reader.NodeType switch
                {
                    XmlNodeType.Element => StartElement(reader),
                    XmlNodeType.EndElement => EndElement(),
                    XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace => Text(reader.Value),
                    _ => false,
                };
                hasNode = moved ? !reader.EOF : reader.Read();
            }

            if (Root is null)
            {
                throw new MathMlSyntaxException(MathMlDiagnosticCodes.SyntaxError, "the text holds no element", new MathMlLocation(_map.SourceName, 1, 1), "write the equation as <math>...</math>");
            }
        }

        /// <summary>
        ///     Handles the start of an element.
        /// </summary>
        /// <param name="reader">The reader, on the element.</param>
        /// <returns>True when the reader was moved past the element (a skipped or an annotation element).</returns>
        private bool StartElement(XmlReader reader)
        {
            var location = Here();
            var name = reader.LocalName;
            var isMathMl = reader.NamespaceURI.Length == 0 || reader.NamespaceURI == MathMlNamespace;
            if (_stack.Count == 0)
            {
                // The root is the math element of MathML (or of no namespace, as HTML writes it)
                if (!isMathMl || name != "math")
                {
                    throw new MathMlSyntaxException(
                        MathMlDiagnosticCodes.SyntaxError,
                        $"the root element is <{reader.Name}>, not <math>",
                        location,
                        $"write the equation as <math xmlns=\"{MathMlNamespace}\">...</math>");
                }
            }
            else if (!isMathMl || _stack[^1].IsToken)
            {
                // Foreign content and markup inside a token are skipped with their subtree
                var reason = isMathMl
                    ? $"the element <{name}> inside the token <{_stack[^1].Name}> is not drawn; a token holds text only"
                    : $"the element <{reader.Name}> of the namespace '{reader.NamespaceURI}' is not MathML and is not drawn";
                Reports.Add(MathMlPendingReport.Unsupported(MathMlDiagnosticCodes.ElementUnsupported, reason, location, "remove the element or replace it with MathML"));
                reader.Skip();
                return true;
            }

            // The structural limits, before anything is kept
            ElementCount++;
            if (ElementCount > _limits.MaxElements)
            {
                throw Limit($"the equation has more than {_limits.MaxElements} elements (MathMlLimits.MaxElements)", location);
            }

            if (_stack.Count + 1 > _limits.MaxNestingDepth)
            {
                throw Limit($"the elements are nested deeper than {_limits.MaxNestingDepth} (MathMlLimits.MaxNestingDepth)", location);
            }

            FlushText();
            var attributes = ReadAttributes(reader);
            if (name is "annotation" or "annotation-xml")
            {
                ReadAnnotation(reader, name, attributes, location);
                return true;
            }

            var frame = new Frame(name, attributes, location);
            if (reader.IsEmptyElement)
            {
                Complete(frame);
            }
            else
            {
                _stack.Add(frame);
            }

            return false;
        }

        /// <summary>
        ///     Handles the end of an element.
        /// </summary>
        /// <returns>False: the reader is not moved.</returns>
        private bool EndElement()
        {
            FlushText();
            var frame = _stack[^1];
            _stack.RemoveAt(_stack.Count - 1);
            Complete(frame);
            return false;
        }

        /// <summary>
        ///     Handles a text node.
        /// </summary>
        /// <param name="value">The text.</param>
        /// <returns>False: the reader is not moved.</returns>
        private bool Text(string value)
        {
            if (_stack.Count == 0)
            {
                return false;
            }

            var frame = _stack[^1];
            if (!frame.IsToken && frame.Text.Length == 0 && string.IsNullOrWhiteSpace(value))
            {
                // Whitespace between elements has no meaning
                return false;
            }

            if (frame.Text.Length == 0)
            {
                frame.TextLocation = Here();
            }

            frame.Text.Append(value);
            if (frame.Text.Length > _limits.MaxTokenLength)
            {
                throw Limit($"the text of <{frame.Name}> is longer than {_limits.MaxTokenLength} characters (MathMlLimits.MaxTokenLength)", frame.TextLocation);
            }

            return false;
        }

        /// <summary>
        ///     Turns the run of text inside a layout element into a text child, and reports it: text belongs in a token.
        /// </summary>
        private void FlushText()
        {
            if (_stack.Count == 0 || _stack[^1].IsToken || _stack[^1].Text.Length == 0)
            {
                return;
            }

            var frame = _stack[^1];
            var collapsed = CollapseWhitespace(frame.Text.ToString());
            frame.Text.Clear();
            if (collapsed.Length == 0)
            {
                return;
            }

            frame.Children.Add(new MathText(collapsed, frame.TextLocation));
            Reports.Add(MathMlPendingReport.Unsupported(
                MathMlDiagnosticCodes.ElementUnsupported,
                $"text directly inside <{frame.Name}> is not MathML; it is drawn like an <mtext>",
                frame.TextLocation,
                "wrap the text in a token element such as <mtext> or <mi>"));
        }

        /// <summary>
        ///     Completes an element and adds it to its parent, or makes it the root.
        /// </summary>
        /// <param name="frame">The element read.</param>
        private void Complete(Frame frame)
        {
            // A token keeps its collapsed text as its only child
            if (frame.IsToken)
            {
                var text = CollapseWhitespace(frame.Text.ToString());
                if (text.Length > 0)
                {
                    frame.Children.Add(new MathText(text, frame.TextLocation));
                }
            }

            var element = new MathElement(frame.Name, frame.Attributes, [.. frame.Children], frame.Location);
            Add(element);
        }

        /// <summary>
        ///     Adds a completed element to the open parent, or makes it the root.
        /// </summary>
        /// <param name="element">The element.</param>
        private void Add(MathElement element)
        {
            if (_stack.Count == 0)
            {
                Root = element;
            }
            else
            {
                _stack[^1].Children.Add(element);
            }
        }

        /// <summary>
        ///     Reads an annotation element as raw content: the text of <c>annotation</c>, the markup of
        ///     <c>annotation-xml</c>.
        /// </summary>
        /// <param name="reader">The reader, on the element; it is moved past the element.</param>
        /// <param name="name">The element name.</param>
        /// <param name="attributes">The attributes.</param>
        /// <param name="location">The location.</param>
        private void ReadAnnotation(XmlReader reader, string name, Dictionary<string, MathAttribute> attributes, MathMlLocation location)
        {
            string content;
            if (name == "annotation-xml")
            {
                content = reader.ReadInnerXml();
            }
            else if (reader.IsEmptyElement)
            {
                content = string.Empty;
                reader.Read();
            }
            else
            {
                // The text of every text node of the subtree, as written (the annotation is not laid out)
                var text = new StringBuilder();
                using (var subtree = reader.ReadSubtree())
                {
                    while (subtree.Read())
                    {
                        if (subtree.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace)
                        {
                            text.Append(subtree.Value);
                        }
                    }
                }

                content = text.ToString();
                reader.Read();
            }

            var encoding = attributes.TryGetValue("encoding", out var value) ? value.Value : null;
            Annotations.Add(new MathMlAnnotation(encoding, content));
            Add(new MathElement(name, attributes, content.Length > 0 ? [new MathText(content, location)] : [], location));
        }

        /// <summary>
        ///     Reads the attributes of the current element with their locations, reporting links.
        /// </summary>
        /// <param name="reader">The reader, on the element; it is left on the element.</param>
        /// <returns>The attributes of no namespace, by local name.</returns>
        private Dictionary<string, MathAttribute> ReadAttributes(XmlReader reader)
        {
            var attributes = new Dictionary<string, MathAttribute>(StringComparer.Ordinal);
            if (!reader.MoveToFirstAttribute())
            {
                return attributes;
            }

            do
            {
                var location = Here();
                var isLink = reader.NamespaceURI == XLinkNamespace || (reader.NamespaceURI.Length == 0 && reader.LocalName == "href");
                if (isLink)
                {
                    // Links are never followed, so they are reported (the rendering is offline)
                    Reports.Add(MathMlPendingReport.Unsupported(
                        MathMlDiagnosticCodes.AttributeUnsupported,
                        $"the link attribute '{reader.Name}' is not followed (rendering never uses the network or other files)",
                        location,
                        "remove the attribute; a picture cannot hold a link"));
                }
                else if (reader.NamespaceURI.Length == 0)
                {
                    attributes[reader.LocalName] = new MathAttribute(reader.Value, location);
                }

                // Namespace declarations and the attributes of other namespaces (xml:lang, ...) have no effect on a picture
            }
            while (reader.MoveToNextAttribute());

            reader.MoveToElement();
            return attributes;
        }

        /// <summary>
        ///     Gets the location of the current node of the reader in the original text.
        /// </summary>
        /// <returns>The location.</returns>
        private MathMlLocation Here() => _lineInfo is { } info && info.HasLineInfo() ? _map.MapBack(info.LineNumber, info.LinePosition) : new MathMlLocation(_map.SourceName, 0, 0);

        /// <summary>
        ///     Creates the exception of an exceeded limit.
        /// </summary>
        /// <param name="description">The description that names the property.</param>
        /// <param name="location">The location.</param>
        /// <returns>The exception.</returns>
        private static MathMlSyntaxException Limit(string description, MathMlLocation location) =>
            new(MathMlDiagnosticCodes.LimitExceeded, description, location, "simplify the equation, or raise the limit in MathMlOpenOptions.Limits");
    }
}

/// <summary>
///     The result of reading a text: the equation tree and what was found on the way.
/// </summary>
/// <param name="Root">The <c>math</c> element.</param>
/// <param name="ElementCount">The number of MathML elements read (the root included; skipped foreign elements and the content of annotations excluded).</param>
/// <param name="Reports">The pending reports, in text order.</param>
/// <param name="Annotations">The annotations, in text order.</param>
internal sealed record MathReadResult(MathElement Root, int ElementCount, IReadOnlyList<MathMlPendingReport> Reports, IReadOnlyList<MathMlAnnotation> Annotations);
