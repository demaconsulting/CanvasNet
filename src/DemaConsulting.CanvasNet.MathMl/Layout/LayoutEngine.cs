using System.Collections.Frozen;
using System.Globalization;
using DemaConsulting.CanvasNet.MathMl.Syntax;

namespace DemaConsulting.CanvasNet.MathMl.Layout;

/// <summary>
///     Lays an equation tree out into a tree of boxes: walks the elements, computes their styles, and dispatches each to
///     its layout.
/// </summary>
/// <remarks>
///     <para>
///         <b>Dispatch.</b> The token elements go to <see cref="TokenLayout"/>; <c>math</c>, <c>mrow</c>, <c>mstyle</c>,
///         <c>mphantom</c>, <c>merror</c>, <c>mpadded</c>, <c>mprescripts</c>, <c>none</c>, <c>semantics</c> and
///         <c>maction</c> to <see cref="RowLayout"/> (the last two with their first child only, as the user agent
///         stylesheet of MathML Core hides the others); <c>mfrac</c> to <see cref="FractionLayout"/>; <c>msqrt</c> (the row
///         of its children) and <c>mroot</c> to <see cref="RadicalLayout"/>; <c>msub</c>, <c>msup</c>, <c>msubsup</c> and
///         <c>mmultiscripts</c> to <see cref="ScriptLayout"/>; <c>munder</c>, <c>mover</c> and <c>munderover</c> to
///         <see cref="UnderOverLayout"/>, or to <see cref="ScriptLayout"/> when their base is an embellished operator with
///         <c>movablelimits</c> and their math style is compact (MathML Core 3.4.2.1). Every other element (an element of
///         MathML Core that a later version draws, such as <c>mtable</c>, or an element that MathML Core does not define,
///         such as <c>mfenced</c>) is laid out as a row of its children, as MathML Core lays out an unknown element, and
///         reported as <see cref="MathMlDiagnosticCodes.ElementUnsupported"/>. Annotations and elements with
///         <c>display: none</c> get no box. Text written directly inside a layout element is laid out like an
///         <c>mtext</c>.
///     </para>
///     <para>
///         <b>Children.</b> The children of a fraction, of a root and of a scripted element get the user agent rules of
///         their positions (<see cref="StyleResolver.ResolveChild"/>, <see cref="StyleResolver.ScriptRule"/>). An
///         element without the in-flow children MathML Core requires (two for <c>mfrac</c>, <c>mroot</c>, <c>msub</c>,
///         <c>msup</c>, <c>munder</c> and <c>mover</c>, three for <c>msubsup</c> and <c>munderover</c>, a base and pairs
///         of scripts around at most one <c>mprescripts</c> for <c>mmultiscripts</c>) is laid out as a row of its
///         children, as MathML Core lays out invalid markup, and reported as the
///         <see cref="MathMlDiagnosticCodes.ChildCount"/> warning.
///     </para>
///     <para>
///         <b>Operators.</b> Each embellished operator child of a row gets its form from its position among the children
///         that are not space-like (MathML Core 3.2.4.2: the first of several is prefix, the last postfix, the others
///         infix; the children of <c>msqrt</c> likewise); a script (a child of a scripted element but the first) is
///         postfix. An element that is itself an embellished operator (a row of one operator and space-like elements, or
///         a fraction or a scripted element whose first child is one) passes the form its parent gave it to its operator.
///         After the children of a row are laid out, its stretchy operators are laid out again with a block stretch size
///         constraint (3.3.1.1, <see cref="RowLayout.Stretch"/>); after the children of an under- or overscript element,
///         its operators stretchy along the inline axis with an inline stretch size constraint (3.4.2.2,
///         <see cref="UnderOverLayout.StretchInline"/>); an embellished element with a constraint passes it to its
///         operator. Whether a child is space-like or an embellished operator is predicted from the tree before it is
///         laid out (an element hidden by a <c>display: none</c> declaration below the child is not seen).
///     </para>
///     <para>
///         <b>Attributes.</b> An attribute that has no effect in this version on an element it draws (a legacy MathML 3
///         attribute, <c>dir="rtl"</c>, an attribute the element does not define) is reported as
///         <see cref="MathMlDiagnosticCodes.AttributeUnsupported"/>. The global attributes that do not change a picture
///         (<c>id</c>, <c>class</c>, <c>data-*</c>, event handlers, ...) are accepted silently.
///     </para>
///     <para>
///         <b>Bounds.</b> The walk is recursive; its depth is bounded by <see cref="MathMlLimits.MaxNestingDepth"/>, which
///         the reader already enforced, and checked again here. Every element and every character of a token is charged
///         to <see cref="MathMlLimits.MaxLayoutWork"/>, and the cancellation token is checked at every element; an operator
///         laid out again to stretch is charged again. An instance serves one call on one thread.
///     </para>
/// </remarks>
internal sealed partial class LayoutEngine
{
    /// <summary>The token elements.</summary>
    private static readonly FrozenSet<string> TokenNames = new[] { "mi", "mn", "mo", "mtext", "ms", "mspace" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    ///     The elements laid out as a row of all their children (<c>mprescripts</c> is a grouping element of MathML Core
    ///     and <c>none</c> an empty element; both mark positions in <c>mmultiscripts</c>).
    /// </summary>
    private static readonly FrozenSet<string> RowNames = new[] { "math", "mrow", "mstyle", "mphantom", "merror", "mpadded", "mprescripts", "none" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The scripted elements: their children but the first are scripts (MathML Core 3.4).</summary>
    private static readonly FrozenSet<string> ScriptedNames = new[] { "msub", "msup", "msubsup", "munder", "mover", "munderover", "mmultiscripts" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The elements of MathML Core that this version lays out as rows (a later version draws them).</summary>
    private static readonly FrozenSet<string> LaterCoreNames = new[] { "mtable", "mtr", "mtd" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The attributes every element accepts (MathML Core 2.1.3); those with no effect on a picture are accepted silently.</summary>
    private static readonly FrozenSet<string> GlobalAttributes = new[]
    {
        "id", "class", "style", "dir", "displaystyle", "scriptlevel", "mathbackground", "mathcolor", "mathsize", "mathvariant", "nonce",
        "tabindex", "autofocus", "intent", "arg",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The attributes of the elements this version draws, beyond the global ones.</summary>
    private static readonly FrozenDictionary<string, FrozenSet<string>> ElementAttributes = new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal)
    {
        ["math"] = new[] { "display", "alttext" }.ToFrozenSet(StringComparer.Ordinal),
        ["mfrac"] = new[] { "linethickness" }.ToFrozenSet(StringComparer.Ordinal),
        ["munder"] = new[] { "accentunder" }.ToFrozenSet(StringComparer.Ordinal),
        ["mover"] = new[] { "accent" }.ToFrozenSet(StringComparer.Ordinal),
        ["munderover"] = new[] { "accent", "accentunder" }.ToFrozenSet(StringComparer.Ordinal),
        ["mo"] = new[] { "form", "fence", "separator", "lspace", "rspace", "stretchy", "symmetric", "maxsize", "minsize", "largeop", "movablelimits", "accent" }.ToFrozenSet(StringComparer.Ordinal),
        ["mspace"] = new[] { "width", "height", "depth" }.ToFrozenSet(StringComparer.Ordinal),
        ["mpadded"] = new[] { "width", "height", "depth", "lspace", "voffset" }.ToFrozenSet(StringComparer.Ordinal),
        ["maction"] = new[] { "actiontype", "selection" }.ToFrozenSet(StringComparer.Ordinal),
        ["semantics"] = new[] { "encoding", "definitionURL" }.ToFrozenSet(StringComparer.Ordinal),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The limits of the call.</summary>
    private readonly MathMlLimits _limits;

    /// <summary>The sink of the call.</summary>
    private readonly MathMlReportSink _sink;

    /// <summary>The cancellation token of the call.</summary>
    private readonly CancellationToken _cancellationToken;

    /// <summary>The style resolver of the call.</summary>
    private readonly StyleResolver _styles;

    /// <summary>The token layout of the call.</summary>
    private readonly TokenLayout _tokens;

    /// <summary>The row layout of the call.</summary>
    private readonly RowLayout _rows;

    /// <summary>The fraction layout of the call.</summary>
    private readonly FractionLayout _fractions;

    /// <summary>The radical layout of the call.</summary>
    private readonly RadicalLayout _radicals;

    /// <summary>The script layout of the call.</summary>
    private readonly ScriptLayout _scripts;

    /// <summary>The under- and overscript layout of the call.</summary>
    private readonly UnderOverLayout _underOver;

    /// <summary>The work charged so far.</summary>
    private long _work;

    /// <summary>The cached predictions of <see cref="IsSpaceLike"/>.</summary>
    private readonly Dictionary<MathElement, bool> _spaceLike = new(ReferenceEqualityComparer.Instance);

    /// <summary>The cached predictions of <see cref="IsEmbellishedOperator"/>.</summary>
    private readonly Dictionary<MathElement, bool> _embellished = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    ///     Initializes the engine of one call.
    /// </summary>
    /// <param name="font">The math font.</param>
    /// <param name="limits">The limits of the call.</param>
    /// <param name="sink">The sink of the call.</param>
    /// <param name="cancellationToken">The cancellation token of the call.</param>
    public LayoutEngine(MathFont font, MathMlLimits limits, MathMlReportSink sink, CancellationToken cancellationToken)
    {
        _limits = limits;
        _sink = sink;
        _cancellationToken = cancellationToken;
        _styles = new StyleResolver(font, sink);
        _tokens = new TokenLayout(font, _styles, sink, limits);
        _rows = new RowLayout(_styles);
        _fractions = new FractionLayout(font, _styles);
        _radicals = new RadicalLayout(font, limits);
        _scripts = new ScriptLayout(font);
        _underOver = new UnderOverLayout(font);
    }

    /// <summary>
    ///     Gets the style resolver of the call (the document resolves the style of the <c>math</c> element with it).
    /// </summary>
    public StyleResolver Styles => _styles;

    /// <summary>
    ///     Gets the work charged so far.
    /// </summary>
    public long Work => _work;

    /// <summary>
    ///     Lays out an equation.
    /// </summary>
    /// <param name="root">The <c>math</c> element.</param>
    /// <param name="rootStyle">The computed style of the <c>math</c> element (<see cref="StyleResolver.ResolveRoot"/>).</param>
    /// <returns>The box of the <c>math</c> element; an empty box when it is not displayed.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled.</exception>
    /// <exception cref="MathMlRenderException">Thrown, located, when a limit is exceeded.</exception>
    /// <exception cref="MathMlUnsupportedFeatureException">Thrown in strict mode at the first unsupported feature.</exception>
    public MathBox Layout(MathElement root, MathStyle rootStyle)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(rootStyle);
        return LayoutElement(root, rootStyle, 1) ?? new MathBox(0, 0, 0) { Element = root };
    }

    /// <summary>
    ///     Lays out one element with its computed style.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="style">Its computed style.</param>
    /// <param name="depth">Its nesting depth, the <c>math</c> element being 1.</param>
    /// <param name="context">The form and the stretch constraint its row gives it when it is an embellished operator.</param>
    /// <returns>The box, or null when the element is not displayed.</returns>
    private MathBox? LayoutElement(MathElement element, MathStyle style, int depth, OperatorContext context = default)
    {
        // The bounds of every element: cancellation, the work budget, and the depth of the recursion
        Enter(element, depth);
        if (style.DisplayNone || element.Name is "annotation" or "annotation-xml")
        {
            return null;
        }

        MathBox box;
        if (TokenNames.Contains(element.Name))
        {
            CheckAttributes(element);
            Charge(element.TextContent.Length, element.Location);
            box = _tokens.LayoutToken(element, style, context);
        }
        else if (RowNames.Contains(element.Name) || element.Name is "semantics" or "maction")
        {
            CheckAttributes(element);
            box = LayoutRowLike(element, style, depth, context);
        }
        else if (element.Name is "mfrac")
        {
            CheckAttributes(element);
            box = LayoutFraction(element, style, depth, context);
        }
        else if (element.Name is "msqrt" or "mroot")
        {
            CheckAttributes(element);
            box = LayoutRadical(element, style, depth, context);
        }
        else if (element.Name is "msub" or "msup" or "msubsup")
        {
            CheckAttributes(element);
            box = LayoutScripted(element, style, depth, context);
        }
        else if (element.Name is "munder" or "mover" or "munderover")
        {
            CheckAttributes(element);
            box = LayoutUnderOver(element, style, depth, context);
        }
        else if (element.Name is "mmultiscripts")
        {
            CheckAttributes(element);
            box = LayoutMultiscripts(element, style, depth, context);
        }
        else
        {
            // An element this version does not draw is a row of its children, as MathML Core lays out an unknown element
            ReportUnsupportedElement(element);
            box = LayoutRowLike(element, style, depth, context);
        }

        return Decorate(box, element, style);
    }

    /// <summary>
    ///     Checks the bounds of an element: cancellation, the work budget, and the depth of the recursion.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="depth">Its nesting depth.</param>
    private void Enter(MathElement element, int depth)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        Charge(1, element.Location);
        if (depth > _limits.MaxNestingDepth)
        {
            throw Limit($"the elements are nested deeper than {_limits.MaxNestingDepth} (MathMlLimits.MaxNestingDepth)", element.Location);
        }
    }

    /// <summary>
    ///     Lays out an element that is laid out like a row: its in-flow children, each embellished operator with the form
    ///     of its position (MathML Core 3.2.4.2), then the row, which stretches its stretchy operators (3.3.1.1).
    /// </summary>
    /// <remarks>
    ///     A row that is itself an embellished operator passes the form and the stretch constraint of its parent to its
    ///     embellished child; with a constraint it does not stretch on its own (3.3.1.1). The forms are inferred before the
    ///     children are laid out, so whether a child is space-like or an embellished operator is predicted from the tree
    ///     (<see cref="IsSpaceLike"/>, <see cref="IsEmbellishedOperator"/>); the boxes then decide the spacing.
    /// </remarks>
    /// <param name="element">The element.</param>
    /// <param name="style">Its computed style.</param>
    /// <param name="depth">Its nesting depth.</param>
    /// <param name="context">The form and the stretch constraint its parent gives it.</param>
    /// <returns>The box.</returns>
    private MathBox LayoutRowLike(MathElement element, MathStyle style, int depth, OperatorContext context) =>
        LayoutRowLike(element, style, depth, context, FlowChildren(element, style, depth, null));

    /// <summary>
    ///     Lays out an element like a row from its in-flow children (<see cref="FlowChildren"/>), already styled.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="style">Its computed style.</param>
    /// <param name="depth">Its nesting depth.</param>
    /// <param name="context">The form and the stretch constraint its parent gives it.</param>
    /// <param name="flow">The in-flow children with their computed styles.</param>
    /// <returns>The box.</returns>
    private MathBox LayoutRowLike(MathElement element, MathStyle style, int depth, OperatorContext context, List<(MathNode Node, MathStyle Style)> flow)
    {
        // The form of each embellished operator from its position among the children that are not space-like; the
        // inferred rows of math and msqrt are never embellished operators themselves (they always add operator spacing)
        var opaque = element.Name is "math" or "msqrt";
        var spaceLike = flow.Select(entry => entry.Node is not MathElement child || IsSpaceLike(child)).ToArray();
        var count = spaceLike.Count(value => !value);
        var passThrough = !opaque && (context.Form is not null || context.HasStretch) && IsEmbellishedOperator(element);
        var contexts = new OperatorContext[flow.Count];
        var position = 0;
        for (var i = 0; i < flow.Count; i++)
        {
            if (!spaceLike[i])
            {
                contexts[i] = passThrough ? context : new OperatorContext(OperatorDictionary.InferForm(position++, count), null);
            }
        }

        // Lay the children out; a row with a constraint of its parent passes it on and does not stretch on its own
        var boxes = new List<MathBox>(flow.Count);
        var sources = new List<int>(flow.Count);
        for (var i = 0; i < flow.Count; i++)
        {
            if (LayoutChild(flow[i].Node, flow[i].Style, depth, contexts[i]) is { } box)
            {
                boxes.Add(box);
                sources.Add(i);
            }
        }

        Func<int, BlockStretchConstraint, MathBox>? relayout = passThrough && context.HasStretch
            ? null
            : (index, constraint) =>
            {
                var source = sources[index];
                return LayoutChild(flow[source].Node, flow[source].Style, depth, contexts[source] with { Stretch = constraint })
                    ?? throw new InvalidOperationException("A displayed child has no box.");
            };
        var row = _rows.LayoutRow(boxes, opaque, relayout);
        return element.Name switch
        {
            "mpadded" => _rows.LayoutPadded(element, style, row),
            "merror" => RowLayout.LayoutError(row),
            _ => row,
        };
    }

    /// <summary>
    ///     Lays out a child of a row: an element, or text written directly inside a layout element (laid out like an
    ///     <c>mtext</c>, reported while reading).
    /// </summary>
    /// <param name="node">The child.</param>
    /// <param name="style">Its computed style (the parent's for text).</param>
    /// <param name="depth">The nesting depth of the parent.</param>
    /// <param name="context">The form and the stretch constraint of the child.</param>
    /// <returns>The box, or null when the child is not displayed.</returns>
    private MathBox? LayoutChild(MathNode node, MathStyle style, int depth, OperatorContext context)
    {
        if (node is MathElement element)
        {
            return LayoutElement(element, style, depth + 1, context);
        }

        var text = (MathText)node;
        Charge(text.Text.Length, text.Location);
        var box = _tokens.LayoutText(text.Text, style, autoItalic: false, text.Location);
        return new MathBox(box.Width, box.Ascent, box.Descent, box.Children, box.Items) { IsSpaceLike = true };
    }

    /// <summary>
    ///     Gets the children that a row-like element lays out: the first child only for <c>semantics</c> and
    ///     <c>maction</c> (the user agent stylesheet of MathML Core hides the others), every child otherwise.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>The children.</returns>
    private static IReadOnlyList<MathNode> RowChildren(MathElement element) =>
        element.Name is "semantics" or "maction" ? element.ElementChildren.Take(1).ToList<MathNode>() : element.Children;

    /// <summary>
    ///     Predicts from the tree whether an element is space-like (MathML Core 3.2.5.1): an <c>mtext</c> or an
    ///     <c>mspace</c>, or an element laid out as a row whose in-flow children are all space-like (text written directly
    ///     inside it counts as space-like). A fraction, a radical or a scripted element is never space-like, unless an
    ///     invalid number of children makes it a row. The result is cached per element.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>True when it is space-like.</returns>
    private bool IsSpaceLike(MathElement element)
    {
        if (_spaceLike.TryGetValue(element, out var cached))
        {
            return cached;
        }

        var result = element.Name switch
        {
            "mtext" or "mspace" => true,
            "mi" or "mn" or "mo" or "ms" or "msqrt" => false,
            _ when HasValidChildren(element) => false,
            _ => InFlowChildren(element).All(child => child is not MathElement childElement || IsSpaceLike(childElement)),
        };
        _spaceLike[element] = result;
        return result;
    }

    /// <summary>
    ///     Predicts from the tree whether an element is an embellished operator (MathML Core 3.2.4.1): an <c>mo</c>; an
    ///     <c>mfrac</c> or a scripted element whose first in-flow child is an embellished operator; or an element laid out
    ///     as a row whose in-flow children are one embellished operator and space-like elements. A radical is never one,
    ///     unless an invalid number of children makes an <c>mroot</c> a row. The result is cached per element.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>True when it is an embellished operator.</returns>
    private bool IsEmbellishedOperator(MathElement element)
    {
        if (_embellished.TryGetValue(element, out var cached))
        {
            return cached;
        }

        bool result;
        if (element.Name == "mo")
        {
            result = true;
        }
        else if (TokenNames.Contains(element.Name) || element.Name is "math" or "msqrt" || (element.Name == "mroot" && HasValidChildren(element)))
        {
            result = false;
        }
        else if ((element.Name == "mfrac" || ScriptedNames.Contains(element.Name)) && HasValidChildren(element))
        {
            result = InFlowChildren(element).First() is MathElement first && IsEmbellishedOperator(first);
        }
        else
        {
            var operators = 0;
            result = true;
            foreach (var child in InFlowChildren(element).OfType<MathElement>())
            {
                if (IsEmbellishedOperator(child))
                {
                    operators++;
                }
                else if (!IsSpaceLike(child))
                {
                    result = false;
                    break;
                }
            }

            result = result && operators == 1;
        }

        _embellished[element] = result;
        return result;
    }

    /// <summary>
    ///     Gets the in-flow children of an element as the tree shows them: the children it lays out, annotations excluded.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>The children.</returns>
    private static IEnumerable<MathNode> InFlowChildren(MathElement element) =>
        RowChildren(element).Where(child => child is not MathElement { Name: "annotation" or "annotation-xml" });

    /// <summary>
    ///     Completes the box of an element: its background, its element, and whether it is a phantom.
    /// </summary>
    /// <param name="box">The laid-out box.</param>
    /// <param name="element">The element.</param>
    /// <param name="style">Its computed style.</param>
    /// <returns>The completed box.</returns>
    private static MathBox Decorate(MathBox box, MathElement element, MathStyle style)
    {
        // The background covers the box (border included), behind everything the element paints
        var items = box.Items;
        if (style.Background is { } background)
        {
            items = [new BackgroundItem(box.ContentRect, background), .. box.Items];
        }

        return new MathBox(box.Width, box.Ascent, box.Descent, box.Children, items)
        {
            ItalicCorrection = box.ItalicCorrection,
            TopAccentAttachment = box.TopAccentAttachment,
            Operator = box.Operator,
            IsSpaceLike = box.IsSpaceLike,
            Element = element,
            IsPhantom = element.Name == "mphantom",
        };
    }

    /// <summary>
    ///     Reports the attributes of a drawn element that have no effect in this version.
    /// </summary>
    /// <param name="element">The element.</param>
    private void CheckAttributes(MathElement element)
    {
        foreach (var (name, attribute) in element.Attributes)
        {
            if (name == "dir" && attribute.Value.Trim().Equals("rtl", StringComparison.OrdinalIgnoreCase))
            {
                _sink.Unsupported(MathMlDiagnosticCodes.AttributeUnsupported, "right-to-left math (dir=\"rtl\") is not supported; the content is laid out left to right", attribute.Location, "write the equation left to right");
                continue;
            }

            var known = GlobalAttributes.Contains(name)
                || name.StartsWith("data-", StringComparison.Ordinal)
                || name.StartsWith("on", StringComparison.Ordinal)
                || (ElementAttributes.TryGetValue(element.Name, out var own) && own.Contains(name));
            if (!known)
            {
                _sink.Unsupported(
                    MathMlDiagnosticCodes.AttributeUnsupported,
                    $"the attribute '{name}' of <{element.Name}> has no effect in this version; it is ignored",
                    attribute.Location,
                    "use the attributes of MathML Core (mathvariant, mathsize, mathcolor, mathbackground, displaystyle, scriptlevel) or the supported style declarations");
            }
        }
    }

    /// <summary>
    ///     Reports an element that is laid out as a row of its children.
    /// </summary>
    /// <param name="element">The element.</param>
    private void ReportUnsupportedElement(MathElement element)
    {
        string message;
        string hint;
        if (LaterCoreNames.Contains(element.Name))
        {
            message = $"the element <{element.Name}> is not drawn by this version; it is laid out as a row of its children";
            hint = "the element is part of MathML Core and a later version draws it";
        }
        else
        {
            message = $"the element <{element.Name}> is not part of MathML Core; it is laid out as a row of its children, as MathML Core lays out an unknown element";
            hint = element.Name == "mfenced" ? "write the fences and separators as <mo> elements inside an <mrow>" : "use the elements of MathML Core";
        }

        _sink.Unsupported(MathMlDiagnosticCodes.ElementUnsupported, message, element.Location, hint);
    }

    /// <summary>
    ///     Charges work to the budget of the call.
    /// </summary>
    /// <param name="amount">The amount of work.</param>
    /// <param name="location">The location of the construct, for the error.</param>
    /// <exception cref="MathMlRenderException">Thrown when the budget is exceeded.</exception>
    private void Charge(long amount, MathMlLocation location)
    {
        _work += amount;
        if (_work > _limits.MaxLayoutWork)
        {
            throw Limit(string.Create(CultureInfo.InvariantCulture, $"the layout needs more than {_limits.MaxLayoutWork} steps (MathMlLimits.MaxLayoutWork)"), location);
        }
    }

    /// <summary>
    ///     Creates the exception of an exceeded limit.
    /// </summary>
    /// <param name="description">The description that names the property.</param>
    /// <param name="location">The location.</param>
    /// <returns>The exception.</returns>
    private static MathMlRenderException Limit(string description, MathMlLocation location) =>
        new(MathMlDiagnosticCodes.LimitExceeded, description, location, "simplify the equation, or raise the limit in MathMlRenderOptions.Limits");
}
