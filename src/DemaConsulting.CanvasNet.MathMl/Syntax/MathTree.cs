namespace DemaConsulting.CanvasNet.MathMl.Syntax;

/// <summary>
///     A node of the equation tree: an element or a run of text, with its location in the original text.
/// </summary>
/// <remarks>
///     <para>
///         The equation tree is the single internal model of an equation: the reader builds it from MathML, the layout
///         reads it, and a later front end (for example LaTeX math) can produce it without any XML. It therefore holds
///         no <c>System.Xml</c> type, only names, attribute values, children, text and locations.
///     </para>
///     <para>
///         The nodes are immutable and safe for concurrent use, so a document can be measured and rendered from several
///         threads at once.
///     </para>
/// </remarks>
/// <param name="Location">The location of the node in the original text.</param>
internal abstract record MathNode(MathMlLocation Location);

/// <summary>
///     An element of the equation tree, such as <c>mrow</c> or <c>mi</c>.
/// </summary>
/// <remarks>
///     The name is the local name of a MathML element (lowercase as written; MathML names are case-sensitive).
///     Attributes are keyed by their local name (ordinal, case-sensitive); attributes of other namespaces are not kept.
///     The children are in document order: elements, and for a token element its text (whitespace already collapsed). The
///     record is immutable.
/// </remarks>
/// <param name="Name">The local name of the element.</param>
/// <param name="Attributes">The attributes by local name.</param>
/// <param name="Children">The child nodes in document order.</param>
/// <param name="Location">The location of the element name in the original text.</param>
internal sealed record MathElement(
    string Name,
    IReadOnlyDictionary<string, MathAttribute> Attributes,
    IReadOnlyList<MathNode> Children,
    MathMlLocation Location) : MathNode(Location)
{
    /// <summary>The names of the token elements, whose content is text.</summary>
    private static readonly HashSet<string> TokenNames = new(StringComparer.Ordinal) { "mi", "mn", "mo", "mtext", "ms" };

    /// <summary>
    ///     Gets the child elements in document order (the text children skipped).
    /// </summary>
    public IEnumerable<MathElement> ElementChildren => Children.OfType<MathElement>();

    /// <summary>
    ///     Gets the text content of the element: its text children concatenated (for a token, its collapsed text); empty
    ///     when it has none.
    /// </summary>
    public string TextContent => string.Concat(Children.OfType<MathText>().Select(text => text.Text));

    /// <summary>
    ///     Decides whether a name is the name of a token element whose content is text.
    /// </summary>
    /// <param name="name">The local name.</param>
    /// <returns>True for <c>mi</c>, <c>mn</c>, <c>mo</c>, <c>mtext</c> and <c>ms</c>.</returns>
    public static bool IsTextTokenName(string name) => TokenNames.Contains(name);

    /// <summary>
    ///     Gets the value of an attribute.
    /// </summary>
    /// <param name="name">The local name of the attribute.</param>
    /// <returns>The value as written (character references decoded), or null when the element has no such attribute.</returns>
    public string? GetAttribute(string name) => Attributes.TryGetValue(name, out var attribute) ? attribute.Value : null;
}

/// <summary>
///     A run of text of the equation tree: the content of a token element, or text written directly inside a layout
///     element (which the layout draws like an <c>mtext</c>).
/// </summary>
/// <param name="Text">The text, character references decoded.</param>
/// <param name="Location">The location of the first character of the text in the original text.</param>
internal sealed record MathText(string Text, MathMlLocation Location) : MathNode(Location);

/// <summary>
///     The value of an attribute with the location of the attribute, so that a report about an invalid value points at
///     it.
/// </summary>
/// <param name="Value">The value, character references decoded.</param>
/// <param name="Location">The location of the attribute name in the original text.</param>
internal readonly record struct MathAttribute(string Value, MathMlLocation Location);
