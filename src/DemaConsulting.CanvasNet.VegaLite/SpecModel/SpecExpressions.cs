using DemaConsulting.CanvasNet.VegaLite.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <summary>
///     The expressions of a specification that the renderer evaluates, compiled once when the specification is parsed.
/// </summary>
/// <remarks>
///     <para>
///         The specification parser compiles every expression that has a supported site: the <c>expr</c> of a variable
///         parameter, the <c>calculate</c> and string <c>filter</c> of a transform of the drawn view, the expression strings
///         inside a filter predicate, and the <c>{"expr": ...}</c> values of the paint properties of the mark and of a
///         <c>value</c> encoding. A syntax error is a <see cref="VegaLiteSpecException"/> at that point, so the document never
///         opens with an expression that cannot run. The <c>{"expr": ...}</c> values of the static sites that Vega-Lite's schema allows (the title, the padding and
///         the background of the view, the mark properties that are not row-bound, the properties of an axis, a legend, a header, a scale and the view, the
///         <c>datum</c> and <c>value</c> of the other channels, and the same properties in the configuration) are compiled too and listed as <see cref="StaticReference"/> entries,
///         which <see cref="ExpressionReferenceFolder"/> evaluates once per call. An expression anywhere else is not compiled and is reported by the feature catalog as not supported. The <c>labelExpr</c> of an axis, a legend and a header, and of the axis and
///         header configurations, is compiled too (it runs on a datum with <c>value</c> and <c>label</c>).
///     </para>
///     <para>
///         The entries are keyed by the string node that holds the expression text (identity), so a stage that has a node
///         finds its compiled expression without parsing anything again. The collection is immutable after parsing and safe
///         for concurrent use.
///     </para>
/// </remarks>
internal sealed class SpecExpressions
{
    /// <summary>The compiled expressions by the string node that holds their text.</summary>
    private readonly Dictionary<SpecNode, CompiledExpression> _byNode;

    /// <summary>
    ///     Initializes the collection.
    /// </summary>
    /// <param name="byNode">The compiled expressions by node; adopted by the collection.</param>
    /// <param name="references">The expression references of the static sites, in document order; none when null.</param>
    internal SpecExpressions(Dictionary<SpecNode, CompiledExpression> byNode, IReadOnlyList<StaticReference>? references = null)
    {
        _byNode = byNode;
        StaticReferences = references ?? [];
    }

    /// <summary>Gets a collection without entries.</summary>
    public static SpecExpressions Empty { get; } = new(new Dictionary<SpecNode, CompiledExpression>(ReferenceEqualityComparer.Instance));

    /// <summary>Gets the number of compiled expressions.</summary>
    public int Count => _byNode.Count;

    /// <summary>
    ///     Gets the <c>{"expr": ...}</c> references of the static sites (the sites that no row reads), in document order. The row-bound sites are not listed: the mark evaluates them for every row.
    /// </summary>
    public IReadOnlyList<StaticReference> StaticReferences { get; }

    /// <summary>
    ///     Gets the compiled expression of a string node.
    /// </summary>
    /// <param name="node">The string node that holds the expression text, or null.</param>
    /// <returns>The compiled expression, or <see langword="null"/> when the node is not a compiled expression site.</returns>
    public CompiledExpression? Get(SpecNode? node) =>
        node is not null && _byNode.TryGetValue(node, out var compiled) ? compiled : null;

    /// <summary>
    ///     Creates a collection that holds these expressions and some more.
    /// </summary>
    /// <remarks>
    ///     This is how the expressions that the renderer writes itself (the <c>calculate</c> and <c>filter</c> of an expanded composite mark) are
    ///     kept next to the expressions of the specification. This collection is not changed; an entry for a node that already has one is replaced
    ///     in the copy.
    /// </remarks>
    /// <param name="additions">The string nodes and the expressions compiled for them.</param>
    /// <returns>A new collection with the entries of this one and the additions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="additions"/> is null.</exception>
    public SpecExpressions With(IEnumerable<(SpecNode Node, CompiledExpression Expression)> additions)
    {
        ArgumentNullException.ThrowIfNull(additions);
        var copy = new Dictionary<SpecNode, CompiledExpression>(_byNode, ReferenceEqualityComparer.Instance);
        foreach (var (node, expression) in additions)
        {
            copy[node] = expression;
        }

        return new SpecExpressions(copy, StaticReferences);
    }

    /// <summary>
    ///     Gets the compiled expression of an <c>{"expr": ...}</c> reference object.
    /// </summary>
    /// <param name="reference">The object with the <c>expr</c> string, or null.</param>
    /// <returns>The compiled expression, or <see langword="null"/> when the object is not a supported expression reference.</returns>
    public CompiledExpression? GetReference(SpecNode? reference) =>
        reference is { IsObject: true } ? Get(reference["expr"]) : null;
}

/// <summary>
///     What the folder does with the value of an expression reference at a static site, which decides how a result that is not a plain value is read.
/// </summary>
internal enum StaticSiteKind
{
    /// <summary>A property that takes a number, a string, a boolean, null or an array of them; a date result cannot be folded.</summary>
    Value,

    /// <summary>A property that takes a text (the <c>text</c> of a title or a mark): a number or a boolean result is its JavaScript text.</summary>
    Text,

    /// <summary>A property of a scale domain: a date result is its number of milliseconds since the epoch (Vega builds the domain of a time scale from numbers).</summary>
    Domain,
}

/// <summary>
///     One <c>{"expr": ...}</c> object at a static site of the specification: a site that Vega-Lite's schema allows an expression reference at and that no row reads.
/// </summary>
/// <param name="Reference">The reference object (<c>{"expr": ...}</c>).</param>
/// <param name="Text">The string node that holds the expression text, which keys the compiled expression.</param>
/// <param name="Expression">The compiled expression.</param>
/// <param name="Kind">How the value is read.</param>
internal sealed record StaticReference(SpecNode Reference, SpecNode Text, CompiledExpression Expression, StaticSiteKind Kind);
