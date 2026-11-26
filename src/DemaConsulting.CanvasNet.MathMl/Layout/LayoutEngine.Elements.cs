using System.Globalization;
using DemaConsulting.CanvasNet.MathMl.Syntax;

namespace DemaConsulting.CanvasNet.MathMl.Layout;

/// <summary>
///     The element walk of the layout engine for the layout elements beyond rows: fractions, radicals, scripts, under- and
///     overscripts and tables, their in-flow children with the user agent rules of their positions, and the child-count
///     rules of MathML Core.
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
            ReportChildCount(element, flow.Count, 2, "a numerator and a denominator");
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
            ReportChildCount(element, flow.Count, 2, "a base and an index");
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
    ///     Predicts from the tree whether an element with a fixed structure (<c>mfrac</c>, <c>mroot</c>, a scripted element)
    ///     has the in-flow children that MathML Core requires (an element hidden by <c>display: none</c> is not counted,
    ///     as the layout and its <see cref="MathMlDiagnosticCodes.ChildCount"/> report do not count it).
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>True when its children are valid; false for every other element.</returns>
    private static bool HasValidChildren(MathElement element) => element.Name switch
    {
        "mfrac" or "mroot" or "msub" or "msup" or "munder" or "mover" => InFlowChildren(element).Take(3).Count() == 2,
        "msubsup" or "munderover" => InFlowChildren(element).Take(4).Count() == 3,
        "mmultiscripts" => MultiscriptsProblem(InFlowChildren(element).ToList()) is null,
        _ => false,
    };

    /// <summary>
    ///     Reports an element whose number of in-flow children is not the one MathML Core requires; it is laid out as a
    ///     row of its children.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="count">Its number of in-flow children.</param>
    /// <param name="required">The number it requires (two or three).</param>
    /// <param name="expected">What it requires, in words.</param>
    private void ReportChildCount(MathElement element, int count, int required, string expected)
    {
        var number = required == 2 ? "two" : "three";
        _sink.Warn(
            MathMlDiagnosticCodes.ChildCount,
            string.Create(CultureInfo.InvariantCulture, $"the element <{element.Name}> has {count} in-flow children instead of {number} ({expected}); it is laid out as a row of its children, as MathML Core lays out invalid markup"),
            element.Location,
            $"give <{element.Name}> exactly {number} children, {expected}; wrap the elements of one part in an <mrow>");
    }

    /// <summary>
    ///     Lays out an <c>msub</c>, <c>msup</c> or <c>msubsup</c> (MathML Core 3.4.1): its base, then its scripts as postfix
    ///     operators under the user agent rules of their positions; a row of its children with a
    ///     <see cref="MathMlDiagnosticCodes.ChildCount"/> warning when it does not have the two (three for <c>msubsup</c>)
    ///     in-flow children it requires.
    /// </summary>
    /// <param name="element">The scripted element.</param>
    /// <param name="style">Its computed style.</param>
    /// <param name="depth">Its nesting depth.</param>
    /// <param name="context">The form and the stretch constraint its parent gives it, passed to its base when the element is an embellished operator.</param>
    /// <returns>The box.</returns>
    private MathBox LayoutScripted(MathElement element, MathStyle style, int depth, OperatorContext context)
    {
        var flow = FlowChildren(element, style, depth, index => StyleResolver.ScriptRule(element, index));
        var required = element.Name == "msubsup" ? 3 : 2;
        if (flow.Count != required)
        {
            ReportChildCount(element, flow.Count, required, element.Name switch
            {
                "msub" => "a base and a subscript",
                "msup" => "a base and a superscript",
                _ => "a base, a subscript and a superscript",
            });
            return LayoutRowLike(element, style, depth, context, flow);
        }

        // An embellished scripted element passes the context of its parent to its base and carries its operator
        var embellished = IsEmbellishedOperator(element);
        var @base = LayoutFlowChild(flow[0], depth, embellished ? context : default);
        var first = LayoutFlowChild(flow[1], depth, ScriptContext);
        var second = required == 3 ? LayoutFlowChild(flow[2], depth, ScriptContext) : null;
        var op = embellished ? @base.Operator : null;
        return element.Name switch
        {
            "msub" => _scripts.Layout(@base, first, null, style, op),
            "msup" => _scripts.Layout(@base, null, first, style, op),
            _ => _scripts.Layout(@base, first, second, style, op),
        };
    }

    /// <summary>
    ///     Lays out an <c>munder</c>, <c>mover</c> or <c>munderover</c> (MathML Core 3.4.2): its base, then its scripts as
    ///     postfix operators under the user agent rules of their positions; as scripts when its math style is compact and
    ///     its base is an embellished operator with <c>movablelimits</c>, else under and over the base after stretching its
    ///     operators along the inline axis; a row of its children with a <see cref="MathMlDiagnosticCodes.ChildCount"/>
    ///     warning when it does not have the two (three for <c>munderover</c>) in-flow children it requires.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="style">Its computed style.</param>
    /// <param name="depth">Its nesting depth.</param>
    /// <param name="context">The form and the stretch constraint its parent gives it, passed to its base when the element is an embellished operator.</param>
    /// <returns>The box.</returns>
    private MathBox LayoutUnderOver(MathElement element, MathStyle style, int depth, OperatorContext context)
    {
        var flow = FlowChildren(element, style, depth, index => StyleResolver.ScriptRule(element, index));
        var required = element.Name == "munderover" ? 3 : 2;
        if (flow.Count != required)
        {
            ReportChildCount(element, flow.Count, required, element.Name switch
            {
                "munder" => "a base and an underscript",
                "mover" => "a base and an overscript",
                _ => "a base, an underscript and an overscript",
            });
            return LayoutRowLike(element, style, depth, context, flow);
        }

        // The accents (an invalid value is false, reported)
        var accentUnder = element.Name != "mover" && Accent(element, "accentunder");
        var accent = element.Name != "munder" && Accent(element, "accent");

        // The base first: an embellished element passes the context of its parent to it and carries its operator
        var embellished = IsEmbellishedOperator(element);
        var baseContext = embellished ? context : default;
        var contexts = new OperatorContext[flow.Count];
        contexts[0] = baseContext;
        for (var i = 1; i < flow.Count; i++)
        {
            contexts[i] = ScriptContext;
        }

        var boxes = new MathBox[flow.Count];
        boxes[0] = LayoutFlowChild(flow[0], depth, baseContext);
        for (var i = 1; i < flow.Count; i++)
        {
            boxes[i] = LayoutFlowChild(flow[i], depth, contexts[i]);
        }

        // Limits of a movable operator outside display style are scripts (MathML Core 3.4.2.1)
        var op = embellished ? boxes[0].Operator : null;
        var (under, over) = element.Name switch
        {
            "munder" => (1, -1),
            "mover" => (-1, 1),
            _ => (1, 2),
        };
        if (!style.DisplayStyle && boxes[0].Operator is { MovableLimits: true })
        {
            return _scripts.Layout(boxes[0], under < 0 ? null : boxes[under], over < 0 ? null : boxes[over], style, op);
        }

        // The operators stretchy along the inline axis stretch to the widest other child (3.4.2.2)
        var stretched = UnderOverLayout.StretchInline(
            boxes,
            embellished && context.HasStretch,
            (index, width) => LayoutFlowChild(flow[index], depth, contexts[index] with { InlineStretch = width }));
        return _underOver.Layout(
            stretched[0],
            under < 0 ? null : stretched[under],
            over < 0 ? null : stretched[over],
            style,
            accentUnder,
            accent,
            embellished ? stretched[0].Operator : null);
    }

    /// <summary>
    ///     Lays out an <c>mmultiscripts</c> (MathML Core 3.4.3): its base, its postscript pairs, its <c>mprescripts</c> and
    ///     its prescript pairs (the scripts as postfix operators under the user agent rules of their positions); a row of
    ///     its children with a <see cref="MathMlDiagnosticCodes.ChildCount"/> warning when it is not valid.
    /// </summary>
    /// <param name="element">The <c>mmultiscripts</c> element.</param>
    /// <param name="style">Its computed style.</param>
    /// <param name="depth">Its nesting depth.</param>
    /// <param name="context">The form and the stretch constraint its parent gives it, passed to its base when the element is an embellished operator.</param>
    /// <returns>The box.</returns>
    private MathBox LayoutMultiscripts(MathElement element, MathStyle style, int depth, OperatorContext context)
    {
        var flow = FlowChildren(element, style, depth, index => StyleResolver.ScriptRule(element, index));
        if (MultiscriptsProblem(flow.Select(entry => entry.Node).ToList()) is { } problem)
        {
            _sink.Warn(
                MathMlDiagnosticCodes.ChildCount,
                $"the element <mmultiscripts> is not valid: {problem}; it is laid out as a row of its children, as MathML Core lays out invalid markup",
                element.Location,
                "write a base, then pairs of a subscript and a superscript, then optionally <mprescripts/> and more pairs; use <none/> for a missing script");
            return LayoutRowLike(element, style, depth, context, flow);
        }

        // The base with the context of an embellished element, then the scripts in pairs around mprescripts
        var embellished = IsEmbellishedOperator(element);
        var @base = LayoutFlowChild(flow[0], depth, embellished ? context : default);
        var postscripts = new List<ScriptPair>();
        var prescripts = new List<ScriptPair>();
        MathBox? separator = null;
        var target = postscripts;
        var i = 1;
        while (i < flow.Count)
        {
            if (flow[i].Node is MathElement { Name: "mprescripts" })
            {
                separator = LayoutFlowChild(flow[i], depth, default);
                target = prescripts;
                i++;
                continue;
            }

            var sub = LayoutFlowChild(flow[i], depth, ScriptContext);
            var sup = LayoutFlowChild(flow[i + 1], depth, ScriptContext);
            target.Add(new ScriptPair(sub, sup));
            i += 2;
        }

        return _scripts.LayoutMultiscripts(@base, postscripts, prescripts, separator, style, embellished ? @base.Operator : null);
    }

    /// <summary>
    ///     Finds why the in-flow children of an <c>mmultiscripts</c> do not form a valid element (MathML Core 3.4.3): a
    ///     base that is not <c>mprescripts</c>, an even number of postscripts, then optionally one <c>mprescripts</c> and
    ///     an even number of prescripts.
    /// </summary>
    /// <param name="children">The in-flow children.</param>
    /// <returns>The problem in words, or null when the element is valid.</returns>
    private static string? MultiscriptsProblem(IReadOnlyList<MathNode> children)
    {
        if (children.Count == 0)
        {
            return "it has no base";
        }

        if (children[0] is MathElement { Name: "mprescripts" })
        {
            return "its base is <mprescripts>";
        }

        var separators = children.Select((child, index) => (child, index)).Where(entry => entry.child is MathElement { Name: "mprescripts" }).Select(entry => entry.index).ToList();
        if (separators.Count > 1)
        {
            return "it has more than one <mprescripts>";
        }

        var postscripts = (separators.Count == 1 ? separators[0] : children.Count) - 1;
        var prescripts = separators.Count == 1 ? children.Count - separators[0] - 1 : 0;
        if (postscripts % 2 != 0)
        {
            return string.Create(CultureInfo.InvariantCulture, $"it has an odd number of postscripts ({postscripts})");
        }

        return prescripts % 2 != 0 ? string.Create(CultureInfo.InvariantCulture, $"it has an odd number of prescripts ({prescripts})") : null;
    }

    /// <summary>
    ///     Reads the <c>accent</c> or <c>accentunder</c> attribute of an under- or overscript element: true when it is an
    ///     ASCII case-insensitive match to <c>true</c> (MathML Core 3.4.2); a value other than <c>true</c> or <c>false</c> is
    ///     false and reported as <see cref="MathMlDiagnosticCodes.AttributeInvalid"/>.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="name">The attribute name.</param>
    /// <returns>Whether the script is an accent.</returns>
    private bool Accent(MathElement element, string name)
    {
        if (!element.Attributes.TryGetValue(name, out var attribute))
        {
            return false;
        }

        if (!attribute.Value.Equals("true", StringComparison.OrdinalIgnoreCase) && !attribute.Value.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            _sink.Warn(
                MathMlDiagnosticCodes.AttributeInvalid,
                $"'{attribute.Value}' is not a valid value of the attribute '{name}'; it is treated as false",
                attribute.Location,
                "use true or false");
        }

        return StyleResolver.IsTrue(element, name);
    }

    /// <summary>
    ///     Lays out an <c>mtable</c> (MathML Core 3.5): its <c>mtr</c> children as rows and the other in-flow children,
    ///     consecutive ones together, in anonymous rows (reported as <see cref="MathMlDiagnosticCodes.ChildCount"/>); then
    ///     the grid with <see cref="TableLayout"/>, which charges its slots.
    /// </summary>
    /// <param name="element">The <c>mtable</c> element.</param>
    /// <param name="style">Its computed style.</param>
    /// <param name="depth">Its nesting depth.</param>
    /// <returns>The box.</returns>
    private MathBox LayoutTable(MathElement element, MathStyle style, int depth)
    {
        var rows = new List<TableRowInput>();
        var stray = new List<(MathNode Node, MathStyle Style)>();
        foreach (var entry in FlowChildren(element, style, depth, null))
        {
            if (entry.Node is MathElement { Name: "mtr" } row)
            {
                AddStrayRow(element, style, depth, stray, rows);
                rows.Add(LayoutTableRow(row, entry.Style, depth + 1));
            }
            else
            {
                stray.Add(entry);
            }
        }

        AddStrayRow(element, style, depth, stray, rows);
        return _tables.Layout(new TableInput(element, style, rows), (amount, location) =>
        {
            _cancellationToken.ThrowIfCancellationRequested();
            Charge(amount, location);
        });
    }

    /// <summary>
    ///     Lays out consecutive in-flow children of a table that are not rows in one anonymous row (a CSS anonymous table
    ///     row), with a <see cref="MathMlDiagnosticCodes.ChildCount"/> warning, and empties the list.
    /// </summary>
    /// <param name="table">The <c>mtable</c> element.</param>
    /// <param name="style">Its computed style (the style of the anonymous row).</param>
    /// <param name="depth">Its nesting depth.</param>
    /// <param name="stray">The children that are not rows.</param>
    /// <param name="rows">The rows, which the anonymous row is added to.</param>
    private void AddStrayRow(MathElement table, MathStyle style, int depth, List<(MathNode Node, MathStyle Style)> stray, List<TableRowInput> rows)
    {
        if (stray.Count == 0)
        {
            return;
        }

        _sink.Warn(
            MathMlDiagnosticCodes.ChildCount,
            $"the {Describe(stray[0].Node)} inside <mtable> is not an <mtr>; it is laid out in an anonymous row, as CSS lays out a table",
            stray[0].Node.Location,
            "put every row of a table in an <mtr> and every cell in an <mtd>");
        rows.Add(new TableRowInput(null, style, LayoutTableCells(table, style, depth, stray, reportStray: false)));
        stray.Clear();
    }

    /// <summary>
    ///     Lays out an <c>mtr</c> of a table: checks its bounds and attributes, then its cells.
    /// </summary>
    /// <param name="row">The <c>mtr</c> element.</param>
    /// <param name="style">Its computed style.</param>
    /// <param name="depth">Its nesting depth.</param>
    /// <returns>The row to lay out.</returns>
    private TableRowInput LayoutTableRow(MathElement row, MathStyle style, int depth)
    {
        Enter(row, depth);
        CheckAttributes(row);
        return new TableRowInput(row, style, LayoutTableCells(row, style, depth, FlowChildren(row, style, depth, null), reportStray: true));
    }

    /// <summary>
    ///     Lays out the cells of a row: each <c>mtd</c> as the anonymous row of its children, the other children,
    ///     consecutive ones together, in an anonymous cell (a CSS anonymous table cell).
    /// </summary>
    /// <param name="parent">The <c>mtr</c>, or the <c>mtable</c> of an anonymous row.</param>
    /// <param name="style">The computed style of the row.</param>
    /// <param name="depth">The nesting depth of the parent.</param>
    /// <param name="flow">The in-flow children of the row.</param>
    /// <param name="reportStray">Whether a child that is not an <c>mtd</c> is reported (the anonymous row of a table is reported already).</param>
    /// <returns>The cells.</returns>
    private List<TableCellInput> LayoutTableCells(MathElement parent, MathStyle style, int depth, List<(MathNode Node, MathStyle Style)> flow, bool reportStray)
    {
        var cells = new List<TableCellInput>();
        var stray = new List<(MathNode Node, MathStyle Style)>();
        void AddStrayCell()
        {
            if (stray.Count == 0)
            {
                return;
            }

            if (reportStray)
            {
                _sink.Warn(
                    MathMlDiagnosticCodes.ChildCount,
                    $"the {Describe(stray[0].Node)} inside <mtr> is not an <mtd>; it is laid out in an anonymous cell, as CSS lays out a table",
                    stray[0].Node.Location,
                    "put every cell of a table in an <mtd>");
            }

            cells.Add(new TableCellInput(null, style, LayoutRowLike(parent, style, depth, default, [.. stray])));
            stray.Clear();
        }

        foreach (var entry in flow)
        {
            if (entry.Node is MathElement { Name: "mtd" } cell)
            {
                AddStrayCell();
                Enter(cell, depth + 1);
                CheckAttributes(cell);
                cells.Add(new TableCellInput(cell, entry.Style, LayoutRowLike(cell, entry.Style, depth + 1, default)));
            }
            else
            {
                stray.Add(entry);
            }
        }

        AddStrayCell();
        return cells;
    }

    /// <summary>
    ///     Reports an <c>mtr</c> or <c>mtd</c> outside a table; it is laid out as a row of its children.
    /// </summary>
    /// <param name="element">The element.</param>
    private void ReportOutsideTable(MathElement element) =>
        _sink.Warn(
            MathMlDiagnosticCodes.ChildCount,
            $"the element <{element.Name}> is not inside {(element.Name == "mtr" ? "an <mtable>" : "an <mtr>")}; it is laid out as a row of its children",
            element.Location,
            "put every <mtd> in an <mtr> and every <mtr> in an <mtable>");

    /// <summary>
    ///     Describes a child for a report: its element name, or text.
    /// </summary>
    /// <param name="node">The child.</param>
    /// <returns>The description.</returns>
    private static string Describe(MathNode node) => node is MathElement element ? $"element <{element.Name}>" : "text";

    /// <summary>The context of a script: the postfix form (MathML Core 3.2.4.2), no stretch constraint.</summary>
    private static OperatorContext ScriptContext => new(OperatorForm.Postfix, null);

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
