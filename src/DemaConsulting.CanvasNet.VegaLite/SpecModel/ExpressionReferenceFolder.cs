using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <summary>
///     The result of folding the expression references of a specification: the tree with the references replaced by their values, and the findings.
/// </summary>
/// <param name="Tree">The tree to render from; the tree that was given when it holds nothing to fold.</param>
/// <param name="Warnings">The references that were left in place because their value cannot stand where they are (a date at a property that takes no date).</param>
internal sealed record FoldResult(SpecTree Tree, IReadOnlyList<FeatureReport> Warnings);

/// <summary>
///     Replaces the <c>{"expr": ...}</c> references of the static sites of a specification by the values of their expressions, evaluated once.
/// </summary>
/// <remarks>
///     <para>
///         Vega-Lite compiles an <c>ExprRef</c> to a Vega signal, so a property that holds one has, in a chart that never changes, the value the expression gives with the
///         parameters at their initial state. The folder evaluates each reference that the specification parser listed as a static site (<see cref="SpecExpressions.StaticReferences"/>;
///         <see cref="ExpressionReferenceSites"/> holds the sites) and rewrites the tree so that every later stage reads the value where it read the reference: the title, the padding, the
///         background, the properties of the axes, legends, headers, scales and the view, the mark properties that no row reads, and the configuration. The sites that the marks evaluate for every row
///         (the paint properties of the mark and of a <c>value</c> channel) are not static and keep their path.
///     </para>
///     <para>
///         The rewrite copies the path from the root to each reference (<see cref="SpecNode.Overlay"/> for an object, <see cref="SpecNode.Synthetic"/> for an array and the literals), so
///         every subtree that holds no reference keeps its identity, and with it the compiled expressions and the parameter indexes that are keyed by node. A literal is located at the
///         reference it replaces, so a later diagnostic points at the expression. A reference that reads <c>datum</c> has no row at a static site: it stays in place and the feature catalog
///         reports it. The tree must be the one the specification parser produced, because the ancestors of a reference are found through the parent links of its nodes.
///     </para>
///     <para>
///         The value of an expression is whatever JavaScript gives: a string, a number, a boolean, null, an array or an object. A number that is not finite folds to null (JSON has no
///         such number, and null leaves the property unset). A date folds to its number of milliseconds only in a scale domain, where Vega builds the domain of a time scale from
///         numbers; anywhere else it has no value a property can read, so the reference stays and a warning says so. The <c>text</c> of a title or a mark takes the JavaScript text of a
///         number or a boolean. A text longer than <see cref="VegaLiteLimits.MaxStringLength"/> is a located <see cref="VegaLiteRenderException"/>. The class is stateless; one call
///         belongs to one evaluator (one thread).
///     </para>
/// </remarks>
internal static class ExpressionReferenceFolder
{
    /// <summary>
    ///     Evaluates the static expression references of a specification and returns the tree with their values in place.
    /// </summary>
    /// <param name="tree">The specification as the parser produced it.</param>
    /// <param name="evaluator">The evaluator of the call, whose parameter values are those the expressions read; it charges every evaluation to the step budget of the call.</param>
    /// <returns>The tree to render from and the warnings; <paramref name="tree"/> itself when nothing is folded.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when an expression exceeds a limit of the evaluation, or a value holds a text longer than <see cref="VegaLiteLimits.MaxStringLength"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token of the evaluator is canceled.</exception>
    public static FoldResult Fold(SpecTree tree, ExpressionEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(evaluator);
        var replaced = new Dictionary<SpecNode, SpecNode>(ReferenceEqualityComparer.Instance);
        var warnings = new List<FeatureReport>();
        foreach (var reference in tree.Expressions.StaticReferences)
        {
            // Without a row there is no datum: the reference stays, and the catalog reports it
            if (reference.Expression.ReferencesDatum)
            {
                continue;
            }

            var value = evaluator.Evaluate(reference.Expression);
            if (value.HasTextLongerThan(evaluator.MaxStringLength))
            {
                throw new VegaLiteRenderException(
                    $"The expression has a result with a text longer than the limit of {evaluator.MaxStringLength} characters (VegaLiteLimits.MaxStringLength).",
                    tree.Locate(reference.Reference));
            }

            if (TryConvert(value, reference.Reference, reference.Reference.Name, reference.Kind, out var literal))
            {
                replaced[reference.Reference] = literal!;
            }
            else
            {
                warnings.Add(new FeatureReport(
                    "vl-expr-ref",
                    $"The expression evaluates to {Describe(value)}, which this property cannot take; the expression reference is ignored.",
                    tree.Locate(reference.Reference),
                    FeatureReportKind.Warning));
            }
        }

        if (replaced.Count == 0)
        {
            return new FoldResult(tree, warnings);
        }

        // Every node on the way from a reference to the root is copied; the others are shared
        var ancestors = new HashSet<SpecNode>(ReferenceEqualityComparer.Instance);
        foreach (var start in replaced.Keys.Select(reference => reference.Parent))
        {
            var up = start;
            while (up is not null && ancestors.Add(up))
            {
                up = up.Parent;
            }
        }

        var root = Rebuild(tree.Root, replaced, ancestors);
        return new FoldResult(new SpecTree(root, tree.SourceName, tree.Warnings, tree.Expressions), warnings);
    }

    /// <summary>
    ///     Copies the path from a node to the references below it.
    /// </summary>
    /// <remarks>
    ///     The recursion follows the nesting of the specification, which the parser bounded by <see cref="VegaLiteLimits.MaxJsonDepth"/>.
    /// </remarks>
    /// <param name="node">The node.</param>
    /// <param name="replaced">The values by reference object.</param>
    /// <param name="ancestors">The nodes that have a reference below them.</param>
    /// <returns>The node itself when nothing below it is folded; otherwise a copy of it.</returns>
    private static SpecNode Rebuild(SpecNode node, Dictionary<SpecNode, SpecNode> replaced, HashSet<SpecNode> ancestors)
    {
        if (replaced.TryGetValue(node, out var literal))
        {
            return literal;
        }

        if (!ancestors.Contains(node))
        {
            return node;
        }

        var children = node.IsObject ? node.Properties : node.Items;
        var copies = new SpecNode[children.Count];
        for (var i = 0; i < copies.Length; i++)
        {
            copies[i] = Rebuild(children[i], replaced, ancestors);
        }

        return node.IsObject
            ? SpecNode.Overlay(node, copies)
            : SpecNode.Synthetic(node, SpecNodeKind.Array, node.Name, children: copies);
    }

    /// <summary>
    ///     Converts the value of an expression to a node, located at the reference it replaces.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="origin">The reference that the node stands for.</param>
    /// <param name="name">The property name of the node, or null for an element of an array.</param>
    /// <param name="kind">How the site reads the value.</param>
    /// <param name="node">Receives the node.</param>
    /// <returns><see langword="false"/> when the value has no form that the site can hold (a date at a site that takes none, a regular expression).</returns>
    private static bool TryConvert(DataValue value, SpecNode origin, string? name, StaticSiteKind kind, out SpecNode? node)
    {
        node = null;
        switch (value.Kind)
        {
            case DataValueKind.Null:
                node = SpecNode.Synthetic(origin, SpecNodeKind.Null, name);
                return true;
            case DataValueKind.Boolean:
                node = kind == StaticSiteKind.Text
                    ? SpecNode.Synthetic(origin, SpecNodeKind.String, name, text: value.ToText())
                    : SpecNode.Synthetic(origin, SpecNodeKind.Boolean, name, flag: value.Boolean);
                return true;
            case DataValueKind.Number:
                if (!double.IsFinite(value.Number))
                {
                    node = SpecNode.Synthetic(origin, SpecNodeKind.Null, name);
                }
                else
                {
                    node = kind == StaticSiteKind.Text
                        ? SpecNode.Synthetic(origin, SpecNodeKind.String, name, text: value.ToText())
                        : SpecNode.Synthetic(origin, SpecNodeKind.Number, name, number: value.Number);
                }

                return true;
            case DataValueKind.String:
                node = SpecNode.Synthetic(origin, SpecNodeKind.String, name, text: value.Text);
                return true;
            case DataValueKind.Date when kind == StaticSiteKind.Domain:
                node = SpecNode.Synthetic(origin, SpecNodeKind.Number, name, number: value.Milliseconds);
                return true;
            case DataValueKind.Array:
                return TryConvertArray(value, origin, name, kind, out node);
            case DataValueKind.Object:
                return TryConvertObject(value, origin, name, out node);
            default:
                return false;
        }
    }

    /// <summary>
    ///     Converts an array value to an array node.
    /// </summary>
    /// <param name="value">The array value.</param>
    /// <param name="origin">The reference that the node stands for.</param>
    /// <param name="name">The property name of the node, or null.</param>
    /// <param name="kind">How the site reads the value.</param>
    /// <param name="node">Receives the node.</param>
    /// <returns><see langword="false"/> when an element has no form that the site can hold.</returns>
    private static bool TryConvertArray(DataValue value, SpecNode origin, string? name, StaticSiteKind kind, out SpecNode? node)
    {
        var items = new List<SpecNode>(value.Elements.Length);
        foreach (var element in value.Elements)
        {
            if (!TryConvert(element, origin, null, kind, out var item))
            {
                node = null;
                return false;
            }

            items.Add(item!);
        }

        node = SpecNode.Synthetic(origin, SpecNodeKind.Array, name, children: items);
        return true;
    }

    /// <summary>
    ///     Converts an object value to an object node.
    /// </summary>
    /// <param name="value">The object value.</param>
    /// <param name="origin">The reference that the node stands for.</param>
    /// <param name="name">The property name of the node, or null.</param>
    /// <param name="node">Receives the node.</param>
    /// <returns><see langword="false"/> when a member has no form that the site can hold.</returns>
    private static bool TryConvertObject(DataValue value, SpecNode origin, string? name, out SpecNode? node)
    {
        var members = value.ObjectValue!;
        var properties = new List<SpecNode>(members.Count);
        for (var i = 0; i < members.Count; i++)
        {
            // The text and the domain of a property are read as such only at the top; the members of an object are plain values
            if (!TryConvert(members.ValueAt(i), origin, members.Keys[i], StaticSiteKind.Value, out var member))
            {
                node = null;
                return false;
            }

            properties.Add(member!);
        }

        node = SpecNode.Synthetic(origin, SpecNodeKind.Object, name, children: properties);
        return true;
    }

    /// <summary>
    ///     Describes the kind of a value for a message.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>For example <c>a date</c>.</returns>
    private static string Describe(DataValue value) => value.Kind switch
    {
        DataValueKind.Date => "a date",
        DataValueKind.RegExp => "a regular expression",
        _ => "a value",
    };
}
