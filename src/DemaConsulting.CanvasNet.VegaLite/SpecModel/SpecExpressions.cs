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
///         opens with an expression that cannot run. An expression anywhere else (the title, the size, the padding, the
///         background, <c>axis.labelExpr</c>) is not compiled and is reported by the feature catalog as not supported.
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
    internal SpecExpressions(Dictionary<SpecNode, CompiledExpression> byNode) => _byNode = byNode;

    /// <summary>Gets a collection without entries.</summary>
    public static SpecExpressions Empty { get; } = new(new Dictionary<SpecNode, CompiledExpression>(ReferenceEqualityComparer.Instance));

    /// <summary>Gets the number of compiled expressions.</summary>
    public int Count => _byNode.Count;

    /// <summary>
    ///     Gets the compiled expression of a string node.
    /// </summary>
    /// <param name="node">The string node that holds the expression text, or null.</param>
    /// <returns>The compiled expression, or <see langword="null"/> when the node is not a compiled expression site.</returns>
    public CompiledExpression? Get(SpecNode? node) =>
        node is not null && _byNode.TryGetValue(node, out var compiled) ? compiled : null;

    /// <summary>
    ///     Gets the compiled expression of an <c>{"expr": ...}</c> reference object.
    /// </summary>
    /// <param name="reference">The object with the <c>expr</c> string, or null.</param>
    /// <returns>The compiled expression, or <see langword="null"/> when the object is not a supported expression reference.</returns>
    public CompiledExpression? GetReference(SpecNode? reference) =>
        reference is { IsObject: true } ? Get(reference["expr"]) : null;
}
