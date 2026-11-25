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
///         <c>mphantom</c>, <c>merror</c>, <c>mpadded</c>, <c>semantics</c> and <c>maction</c> to
///         <see cref="RowLayout"/> (the last two with their first child only, as the user agent stylesheet of MathML Core
///         hides the others). Every other element (an element of MathML Core that a later version draws, such as
///         <c>mfrac</c>, or an element that MathML Core does not define, such as <c>mfenced</c>) is laid out as a row of
///         its children, as MathML Core lays out an unknown element, and reported as
///         <see cref="MathMlDiagnosticCodes.ElementUnsupported"/>. Annotations and elements with <c>display: none</c> get
///         no box. Text written directly inside a layout element is laid out like an <c>mtext</c>.
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
///         to <see cref="MathMlLimits.MaxLayoutWork"/>, and the cancellation token is checked at every element. An
///         instance serves one call on one thread.
///     </para>
/// </remarks>
internal sealed class LayoutEngine
{
    /// <summary>The token elements.</summary>
    private static readonly FrozenSet<string> TokenNames = new[] { "mi", "mn", "mo", "mtext", "ms", "mspace" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The elements laid out as a row of all their children.</summary>
    private static readonly FrozenSet<string> RowNames = new[] { "math", "mrow", "mstyle", "mphantom", "merror", "mpadded" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The elements of MathML Core that this version lays out as rows (a later version draws them).</summary>
    private static readonly FrozenSet<string> LaterCoreNames = new[]
    {
        "mfrac", "msqrt", "mroot", "msub", "msup", "msubsup", "munder", "mover", "munderover", "mmultiscripts", "mprescripts", "none",
        "mtable", "mtr", "mtd",
    }.ToFrozenSet(StringComparer.Ordinal);

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

    /// <summary>The work charged so far.</summary>
    private long _work;

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
        _tokens = new TokenLayout(font, _styles, sink);
        _rows = new RowLayout(_styles);
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
    /// <returns>The box, or null when the element is not displayed.</returns>
    private MathBox? LayoutElement(MathElement element, MathStyle style, int depth)
    {
        // The bounds of every element: cancellation, the work budget, and the depth of the recursion
        _cancellationToken.ThrowIfCancellationRequested();
        Charge(1, element.Location);
        if (depth > _limits.MaxNestingDepth)
        {
            throw Limit($"the elements are nested deeper than {_limits.MaxNestingDepth} (MathMlLimits.MaxNestingDepth)", element.Location);
        }

        if (style.DisplayNone || element.Name is "annotation" or "annotation-xml")
        {
            return null;
        }

        MathBox box;
        if (TokenNames.Contains(element.Name))
        {
            CheckAttributes(element);
            Charge(element.TextContent.Length, element.Location);
            box = _tokens.LayoutToken(element, style);
        }
        else if (RowNames.Contains(element.Name) || element.Name is "semantics" or "maction")
        {
            CheckAttributes(element);
            box = LayoutRowLike(element, style, depth);
        }
        else
        {
            // An element this version does not draw is a row of its children, as MathML Core lays out an unknown element
            ReportUnsupportedElement(element);
            box = _rows.LayoutRow(LayoutChildren(element.Children, style, depth), isMath: false);
        }

        return Decorate(box, element, style);
    }

    /// <summary>
    ///     Lays out an element that is laid out like a row.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="style">Its computed style.</param>
    /// <param name="depth">Its nesting depth.</param>
    /// <returns>The box.</returns>
    private MathBox LayoutRowLike(MathElement element, MathStyle style, int depth)
    {
        // semantics and maction draw their first child only (the user agent stylesheet hides the others)
        IReadOnlyList<MathNode> children = element.Name is "semantics" or "maction"
            ? element.ElementChildren.Take(1).ToList<MathNode>()
            : element.Children;
        var row = _rows.LayoutRow(LayoutChildren(children, style, depth), isMath: element.Name == "math");
        return element.Name switch
        {
            "mpadded" => _rows.LayoutPadded(element, style, row),
            "merror" => RowLayout.LayoutError(row),
            _ => row,
        };
    }

    /// <summary>
    ///     Lays out the children of an element.
    /// </summary>
    /// <param name="children">The children to lay out.</param>
    /// <param name="style">The computed style of the parent.</param>
    /// <param name="depth">The nesting depth of the parent.</param>
    /// <returns>The boxes of the displayed children, in order.</returns>
    private List<MathBox> LayoutChildren(IReadOnlyList<MathNode> children, MathStyle style, int depth)
    {
        var boxes = new List<MathBox>(children.Count);
        foreach (var child in children)
        {
            MathBox? box;
            if (child is MathElement element)
            {
                box = LayoutElement(element, _styles.Resolve(element, style), depth + 1);
            }
            else
            {
                // Text directly inside a layout element (reported while reading) is laid out like an mtext
                var text = (MathText)child;
                Charge(text.Text.Length, text.Location);
                box = _tokens.LayoutText(text.Text, style, autoItalic: false, text.Location);
                box = new MathBox(box.Width, box.Ascent, box.Descent, box.Children, box.Items) { IsSpaceLike = true };
            }

            if (box is not null)
            {
                boxes.Add(box);
            }
        }

        return boxes;
    }

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
