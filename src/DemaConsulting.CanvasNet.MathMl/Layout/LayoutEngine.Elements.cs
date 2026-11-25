using System.Globalization;
using DemaConsulting.CanvasNet.MathMl.Syntax;

namespace DemaConsulting.CanvasNet.MathMl.Layout;

/// <summary>
///     The element walk of the layout engine for the layout elements beyond rows: fractions and radicals, their in-flow
///     children with the user agent rules of their positions, and the child-count rule of MathML Core.
/// </summary>
internal sealed partial class LayoutEngine
{
    /// <summary>
    ///     Gets the in-flow children of an element with their computed styles: its element children (the first only for
    ///     <c>semantics</c> and <c>maction</c>) without annotations and elements with <c>display: none</c> (which are still
    ///     charged), and the text written directly inside it with the element's style.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="style">Its computed style.</param>
    /// <param name="depth">Its nesting depth.</param>
    /// <param name="rules">
    ///     The user agent rule of each element child by its index among the element children (as the selectors of MathML
    ///     Core count them, hidden elements included), or null for none.
    /// </param>
    /// <returns>The in-flow children in order.</returns>
    private List<(MathNode Node, MathStyle Style)> FlowChildren(MathElement element, MathStyle style, int depth, Func<int, ChildStyleRule>? rules)
    {
        var flow = new List<(MathNode Node, MathStyle Style)>();
        var index = 0;
        foreach (var node in RowChildren(element))
        {
            if (node is not MathElement child)
            {
                flow.Add((node, style));
                continue;
            }

            var childStyle = rules is null ? _styles.Resolve(child, style) : _styles.ResolveChild(child, style, rules(index));
            index++;
            if (childStyle.DisplayNone || child.Name is "annotation" or "annotation-xml")
            {
                Enter(child, depth + 1);
                continue;
            }

            flow.Add((child, childStyle));
        }

        return flow;
    }

    /// <summary>
    ///     Lays out an <c>mfrac</c> (MathML Core 3.3.2): its numerator and denominator under the user agent rules of their
    ///     positions; a row of its children with a <see cref="MathMlDiagnosticCodes.ChildCount"/> warning when it does not
    ///     have exactly two in-flow children.
    /// </summary>
    /// <param name="element">The <c>mfrac</c> element.</param>
    /// <param name="style">Its computed style.</param>
    /// <param name="depth">Its nesting depth.</param>
    /// <param name="context">The form and the stretch constraint its parent gives it, passed to its numerator when the fraction is an embellished operator.</param>
    /// <returns>The box.</returns>
    private MathBox LayoutFraction(MathElement element, MathStyle style, int depth, OperatorContext context)
    {
        var flow = FlowChildren(element, style, depth, FractionRule);
        if (flow.Count != 2)
        {
            ReportChildCount(element, flow.Count, "a numerator and a denominator");
            return LayoutRowLike(element, style, depth, context, flow);
        }

        // An embellished fraction passes the context of its parent to its numerator and carries its operator
        var embellished = IsEmbellishedOperator(element);
        var numerator = LayoutFlowChild(flow[0], depth, embellished ? context : default);
        var denominator = LayoutFlowChild(flow[1], depth, default);
        var thickness = _fractions.ResolveThickness(element, style);
        return _fractions.Layout(numerator, denominator, style, thickness, embellished ? numerator.Operator : null);
    }

    /// <summary>
    ///     Lays out an <c>msqrt</c> (the row of its children, whose operators infer their forms) or an <c>mroot</c> (its
    ///     base and its index, under the user agent rule of the index) (MathML Core 3.3.3); an <c>mroot</c> without exactly
    ///     two in-flow children is a row of its children with a <see cref="MathMlDiagnosticCodes.ChildCount"/> warning.
    /// </summary>
    /// <param name="element">The <c>msqrt</c> or <c>mroot</c> element.</param>
    /// <param name="style">Its computed style.</param>
    /// <param name="depth">Its nesting depth.</param>
    /// <param name="context">The form and the stretch constraint its parent gives it (a radical is not an embellished operator; used only by an invalid <c>mroot</c>).</param>
    /// <returns>The box.</returns>
    private MathBox LayoutRadical(MathElement element, MathStyle style, int depth, OperatorContext context)
    {
        if (element.Name == "msqrt")
        {
            var radicand = LayoutRowLike(element, style, depth, default);
            return _radicals.LayoutSquareRoot(radicand, style, element.Location);
        }

        var flow = FlowChildren(element, style, depth, RootRule);
        if (flow.Count != 2)
        {
            ReportChildCount(element, flow.Count, "a base and an index");
            return LayoutRowLike(element, style, depth, context, flow);
        }

        var root = LayoutFlowChild(flow[0], depth, default);
        var index = LayoutFlowChild(flow[1], depth, default);
        return _radicals.LayoutRoot(root, index, style, element.Location);
    }

    /// <summary>
    ///     Lays out an in-flow child that must have a box (it was not filtered out as hidden).
    /// </summary>
    /// <param name="child">The child with its computed style.</param>
    /// <param name="depth">The nesting depth of the parent.</param>
    /// <param name="context">The form and the stretch constraint of the child.</param>
    /// <returns>The box.</returns>
    private MathBox LayoutFlowChild((MathNode Node, MathStyle Style) child, int depth, OperatorContext context) =>
        LayoutChild(child.Node, child.Style, depth, context) ?? throw new InvalidOperationException("A displayed child has no box.");

    /// <summary>
    ///     Predicts from the tree whether an <c>mfrac</c> or an <c>mroot</c> has the two in-flow children that MathML Core
    ///     requires (an element hidden by <c>display: none</c> is counted).
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>True when it has exactly two.</returns>
    private static bool HasValidChildCount(MathElement element) => InFlowChildren(element).Take(3).Count() == 2;

    /// <summary>
    ///     Reports an element whose number of in-flow children is not the one MathML Core requires; it is laid out as a
    ///     row of its children.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="count">Its number of in-flow children.</param>
    /// <param name="expected">What it requires, in words.</param>
    private void ReportChildCount(MathElement element, int count, string expected) =>
        _sink.Warn(
            MathMlDiagnosticCodes.ChildCount,
            string.Create(CultureInfo.InvariantCulture, $"the element <{element.Name}> has {count} in-flow children instead of two ({expected}); it is laid out as a row of its children, as MathML Core lays out invalid markup"),
            element.Location,
            $"give <{element.Name}> exactly two children, {expected}; wrap the elements of one part in an <mrow>");

    /// <summary>
    ///     Gets the user agent rule of a child of <c>mfrac</c> (MathML Core Appendix A): every child gets
    ///     <c>math-depth: auto-add; math-style: compact</c>, the second <c>math-shift: compact</c>.
    /// </summary>
    /// <param name="index">The index of the child among the element children.</param>
    /// <returns>The rule.</returns>
    private static ChildStyleRule FractionRule(int index) => index == 1 ? ChildStyleRule.FractionDenominator : ChildStyleRule.FractionNumerator;

    /// <summary>
    ///     Gets the user agent rule of a child of <c>mroot</c> (MathML Core Appendix A): every child but the first gets
    ///     <c>math-depth: add(2); math-style: compact</c>.
    /// </summary>
    /// <param name="index">The index of the child among the element children.</param>
    /// <returns>The rule.</returns>
    private static ChildStyleRule RootRule(int index) => index == 0 ? default : ChildStyleRule.RootIndex;
}
