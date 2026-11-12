// Portions derived from mermaid (MIT); see third-party/README.md
using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Mermaid.Configuration;

/// <summary>
///     The resolved paint of a pie slice: its fill and stroke with the element opacity folded in, and the scale of a
///     highlighted slice.
/// </summary>
/// <param name="Fill">The fill color, or null for none.</param>
/// <param name="Stroke">The stroke color, or null for none.</param>
/// <param name="StrokeWidth">The stroke width in pixels.</param>
/// <param name="Scale">The scale around the center of the pie (the CSS <c>scale</c> property).</param>
internal sealed record SlicePaint(Rgba32? Fill, Rgba32? Stroke, double StrokeWidth, double Scale);

/// <summary>
///     The style sheet Mermaid writes for a pie chart (its <c>pieStyles.ts</c> after the global rules of
///     <c>styles.ts</c>), built from the theme variables of a configuration, with the rules of <c>themeCSS</c> applied
///     after it.
/// </summary>
/// <remarks>
///     <para>
///         <b>Cascade.</b> A rule beats a presentation attribute, so a slice keeps the fill of its <c>fill</c> attribute
///         (its color of the ordinal scale) unless a rule declares one, and a highlighted slice
///         (<c>.pieCircle.highlighted</c>, more specific) is drawn over the declarations of <c>.pieCircle</c>. Texts
///         inherit the root rule's font and fill where their own rule has none. The rule
///         <c>.pieCircle.highlightedOnHover:hover</c> never applies to a picture.
///     </para>
///     <para>
///         <b>Simplifications.</b> The element <c>opacity</c> of a slice is folded into its fill and stroke colors, and the
///         CSS <c>scale</c> of a highlighted slice is read as one number or percentage (any other value is 1).
///     </para>
///     <para>
///         An instance is immutable.
///     </para>
/// </remarks>
internal sealed class PieStyles : DiagramStyles
{
    /// <summary>
    ///     Initializes the styles from their rules.
    /// </summary>
    /// <param name="rules">The rules in cascade order.</param>
    private PieStyles(IReadOnlyList<StyleRule> rules)
        : base(rules)
    {
    }

    /// <summary>Gets the selectors of the pie chart's style sheet, which <c>themeCSS</c> may use.</summary>
    public static FrozenSet<string> KnownSelectors { get; } = BuildRules(new Dictionary<string, string?>(), [])
        .Select(rule => rule.Selector)
        .ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Gets the paint of the outer circle (<c>.pieOuterCircle</c>; the circle has no paint attributes).</summary>
    public ShapePaint OuterCircle
    {
        get
        {
            var set = Resolve(".pieOuterCircle");
            return new ShapePaint(set.Paint("fill", Black), set.Paint("stroke"), set.Length("stroke-width", 1));
        }
    }

    /// <summary>Gets the font and color of the title (<c>.pieTitleText</c>).</summary>
    public TextPaint TitleText => Text(".pieTitleText", "fill");

    /// <summary>Gets the font and color of the percentage labels (<c>.slice</c>).</summary>
    public TextPaint SliceText => Text(".slice", "fill");

    /// <summary>Gets the font and color of the legend texts (<c>.legend text</c>).</summary>
    public TextPaint LegendText => Text(".legend text", "fill");

    /// <summary>
    ///     Builds the styles of a configuration: the pie chart's rules from its theme variables, then the known rules of
    ///     its <c>themeCSS</c>.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="sink">The report sink for <c>themeCSS</c> problems, or null.</param>
    /// <returns>The styles.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown in strict mode at the first unsupported <c>themeCSS</c> rule.</exception>
    public static PieStyles Create(MermaidConfig config, MermaidReportSink? sink)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new PieStyles(BuildRules(VariablesOf(config), UserRules(config, KnownSelectors, sink)));
    }

    /// <summary>
    ///     Resolves the paint of a slice (<c>path.pieCircle</c>, with <c>.highlighted</c> when it is the highlighted slice):
    ///     the declarations of its rules over its <c>fill</c> attribute, the opacity folded in.
    /// </summary>
    /// <param name="fill">The fill of the slice's attribute (black when the attribute is not a color).</param>
    /// <param name="highlighted">Whether the slice is the highlighted one.</param>
    /// <returns>The paint.</returns>
    public SlicePaint Slice(Rgba32 fill, bool highlighted)
    {
        var set = Resolve(".pieCircle");
        if (highlighted)
        {
            set = set.Cascade(Resolve(".pieCircle.highlighted"));
        }

        var opacity = set["opacity"] is { } value && StyleDeclarations.TryParseOpacity(value, out var parsed) ? parsed : 1;
        var scale = set["scale"] is { } text && TryParseScale(text, out var factor) ? factor : 1;
        return new SlicePaint(
            WithOpacity(set["fill"] is null ? fill : set.Paint("fill"), opacity),
            WithOpacity(set.Paint("stroke"), opacity),
            set.Length("stroke-width", 1),
            scale);
    }

    /// <summary>
    ///     Multiplies the alpha of a color by an opacity.
    /// </summary>
    /// <param name="color">The color, or null.</param>
    /// <param name="opacity">The opacity from 0 to 1.</param>
    /// <returns>The faded color, or null.</returns>
    private static Rgba32? WithOpacity(Rgba32? color, double opacity) =>
        color is { } c ? c with { A = (byte)Math.Round(c.A * opacity, MidpointRounding.AwayFromZero) } : null;

    /// <summary>
    ///     Parses the CSS <c>scale</c> property when it is one number or one percentage.
    /// </summary>
    /// <param name="text">The value.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>Whether the value is one number or percentage.</returns>
    private static bool TryParseScale(string text, out double scale)
    {
        var value = text.Trim();
        var percent = value.EndsWith('%');
        if (!double.TryParse(percent ? value[..^1] : value, NumberStyles.Float, CultureInfo.InvariantCulture, out scale) || !double.IsFinite(scale))
        {
            scale = 1;
            return false;
        }

        if (percent)
        {
            scale /= 100;
        }

        return true;
    }

    /// <summary>
    ///     Builds the rules: the global rules of Mermaid's <c>getStyles</c>, the pie chart's <c>pieStyles.ts</c> in its
    ///     order, then the user's.
    /// </summary>
    /// <param name="v">The theme variables as text.</param>
    /// <param name="user">The user rules.</param>
    /// <returns>The rules.</returns>
    private static List<StyleRule> BuildRules(IReadOnlyDictionary<string, string?> v, IReadOnlyList<StyleRule> user)
    {
        var builder = new RuleBuilder(v);
        string? Var(string name) => builder.Var(name);
        void Add(string selectors, params (string Property, string? Value)[] declarations) => builder.Add(selectors, declarations);

        // Global rules (styles.ts getStyles) before the diagram styles
        builder.AddGlobalRules();

        // pieStyles.ts (the comment "// fill: white;" of .slice is an invalid declaration, which a browser drops)
        Add(".pieCircle", ("stroke", Var("pieStrokeColor")), ("stroke-width", Var("pieStrokeWidth")), ("opacity", Var("pieOpacity")));
        Add(".pieCircle.highlighted", ("scale", "1.05"), ("opacity", "1"));
        Add(".pieCircle.highlightedOnHover:hover", ("scale", "1.05"), ("opacity", "1"));
        Add(".pieOuterCircle", ("stroke", Var("pieOuterStrokeColor")), ("stroke-width", Var("pieOuterStrokeWidth")), ("fill", "none"));
        Add(
            ".pieTitleText",
            ("text-anchor", "middle"),
            ("font-size", Var("pieTitleTextSize")),
            ("fill", Var("pieTitleTextColor")),
            ("font-family", Var("fontFamily")));
        Add(".slice", ("font-family", Var("fontFamily")), ("fill", Var("pieSectionTextColor")), ("font-size", Var("pieSectionTextSize")));
        Add(".legend text", ("fill", Var("pieLegendTextColor")), ("font-family", Var("fontFamily")), ("font-size", Var("pieLegendTextSize")));

        // The user styles come last
        builder.Rules.AddRange(user);
        return builder.Rules;
    }

    /// <summary>Gets black, the default of SVG paint.</summary>
    private static Rgba32 Black => new(0, 0, 0, 255);
}

/// <summary>
///     The style sheet Mermaid writes for a packet diagram (its packet <c>styles.ts</c> after the global rules of
///     <c>styles.ts</c>), built from the theme variables of a configuration, with the rules of <c>themeCSS</c> applied
///     after it.
/// </summary>
/// <remarks>
///     <para>
///         <b>Options.</b> The sheet reads its ten values from <c>cleanAndMerge</c> of its defaults (black texts and
///         strokes, a <c>#efefef</c> fill, 10, 12 and 14 pixel fonts and a stroke width of 1) and the theme variable
///         <c>packet</c> when it is an object, each value written as JavaScript's <c>String</c> writes it. The dark and
///         forest themes set the object; a user's <c>themeVariables.packet</c> replaces the theme's object, so the keys it
///         leaves out take the defaults.
///     </para>
///     <para>
///         <b>Cascade.</b> A declaration whose value is not valid CSS is dropped as a browser drops it: a color that is
///         not a color, and a font size that is a number without a unit (a unitless <c>labelFontSize</c>), so the text
///         inherits the root rule's font size. A bit number matches <c>.packetByte</c> and, more specific,
///         <c>.packetByte.start</c> or <c>.packetByte.end</c>. A block whose rule declares no valid fill inherits the
///         root rule's fill, as SVG paint is inherited.
///     </para>
///     <para>
///         An instance is immutable.
///     </para>
/// </remarks>
internal sealed class PacketStyles : DiagramStyles
{
    /// <summary>The defaults of the sheet's options (Mermaid's <c>defaultPacketStyleOptions</c>), in their order.</summary>
    private static readonly (string Key, string Value)[] DefaultOptions =
    [
        ("byteFontSize", "10px"),
        ("startByteColor", "black"),
        ("endByteColor", "black"),
        ("labelColor", "black"),
        ("labelFontSize", "12px"),
        ("titleColor", "black"),
        ("titleFontSize", "14px"),
        ("blockStrokeColor", "black"),
        ("blockStrokeWidth", "1"),
        ("blockFillColor", "#efefef"),
    ];

    /// <summary>
    ///     Initializes the styles from their rules.
    /// </summary>
    /// <param name="rules">The rules in cascade order.</param>
    private PacketStyles(IReadOnlyList<StyleRule> rules)
        : base(rules)
    {
    }

    /// <summary>Gets the selectors of the packet diagram's style sheet, which <c>themeCSS</c> may use.</summary>
    public static FrozenSet<string> KnownSelectors { get; } = BuildRules(new Dictionary<string, string?>(), Options(null), [])
        .Select(rule => rule.Selector)
        .ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    ///     Gets the paint of a block (<c>rect.packetBlock</c>; the rectangle has no paint attributes, so a fill, stroke or
    ///     stroke width its rule does not declare is inherited from the root rule, else SVG's black fill, no stroke and a
    ///     width of 1).
    /// </summary>
    public ShapePaint Block
    {
        get
        {
            var root = Resolve(Root);
            var set = Resolve(".packetBlock");
            return new ShapePaint(
                set.Paint("fill", root.Paint("fill", Black)),
                set.Paint("stroke", root.Paint("stroke")),
                set.Length("stroke-width", root.Length("stroke-width", 1)));
        }
    }

    /// <summary>Gets the font and color of a block's label (<c>.packetLabel</c>).</summary>
    public TextPaint Label => Text(".packetLabel", "fill");

    /// <summary>Gets the font and color of the title (<c>.packetTitle</c>).</summary>
    public TextPaint Title => Text(".packetTitle", "fill");

    /// <summary>Gets the font and color of a block's first bit number (<c>.packetByte</c> with <c>.packetByte.start</c>).</summary>
    public TextPaint ByteStart => Text(Resolve(".packetByte").Cascade(Resolve(".packetByte.start")), "fill");

    /// <summary>Gets the font and color of a block's last bit number (<c>.packetByte</c> with <c>.packetByte.end</c>).</summary>
    public TextPaint ByteEnd => Text(Resolve(".packetByte").Cascade(Resolve(".packetByte.end")), "fill");

    /// <summary>
    ///     Builds the styles of a configuration: the packet diagram's rules from its theme variables, then the known rules
    ///     of its <c>themeCSS</c>.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="sink">The report sink for <c>themeCSS</c> problems, or null.</param>
    /// <returns>The styles.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown in strict mode at the first unsupported <c>themeCSS</c> rule.</exception>
    public static PacketStyles Create(MermaidConfig config, MermaidReportSink? sink)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new PacketStyles(BuildRules(VariablesOf(config), Options(config.ThemeVariables["packet"]), UserRules(config, KnownSelectors, sink)));
    }

    /// <summary>
    ///     Gets the options of the sheet: Mermaid's <c>cleanAndMerge</c> of the defaults and the theme variable
    ///     <c>packet</c> (when it is an object), each value as JavaScript's <c>String</c> writes it.
    /// </summary>
    /// <param name="packet">The theme variable <c>packet</c>, or null.</param>
    /// <returns>The options by key.</returns>
    internal static Dictionary<string, string> Options(JsonNode? packet)
    {
        var options = DefaultOptions.ToDictionary(option => option.Key, option => option.Value, StringComparer.Ordinal);
        if (packet is JsonObject overrides)
        {
            foreach (var (key, value) in overrides)
            {
                options[key] = JsString(value);
            }
        }

        return options;
    }

    /// <summary>
    ///     Writes a JSON value as JavaScript's <c>String</c> writes the value it parses to: a string as it is, a number as
    ///     JavaScript writes it, <c>true</c>, <c>false</c>, <c>null</c>, an object as <c>[object Object]</c> and an array
    ///     as its elements joined by commas (a null element as nothing).
    /// </summary>
    /// <param name="node">The value.</param>
    /// <returns>The text.</returns>
    internal static string JsString(JsonNode? node) => node switch
    {
        null => "null",
        JsonObject => "[object Object]",
        JsonArray array => string.Join(',', array.Select(item => item is null ? string.Empty : JsString(item))),
        JsonValue value => value.GetValueKind() switch
        {
            JsonValueKind.String => value.GetValue<string>(),
            JsonValueKind.Number => ColorMath.FormatNumber(double.Parse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture)),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => "null",
        },
        _ => "null",
    };

    /// <summary>
    ///     Keeps a font size a browser accepts: a number without a unit is not a CSS length (unless it is zero), so the
    ///     declaration is dropped.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The value, or null when the browser drops it.</returns>
    private static string? FontSize(string value) =>
        double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number != 0 ? null : value;

    /// <summary>
    ///     Builds the rules: the global rules of Mermaid's <c>getStyles</c>, the packet diagram's <c>styles.ts</c> in its
    ///     order, then the user's.
    /// </summary>
    /// <param name="v">The theme variables as text.</param>
    /// <param name="options">The options of the sheet.</param>
    /// <param name="user">The user rules.</param>
    /// <returns>The rules.</returns>
    private static List<StyleRule> BuildRules(IReadOnlyDictionary<string, string?> v, IReadOnlyDictionary<string, string> options, IReadOnlyList<StyleRule> user)
    {
        var builder = new RuleBuilder(v);
        void Add(string selectors, params (string Property, string? Value)[] declarations) => builder.Add(selectors, declarations);

        // Global rules (styles.ts getStyles) before the diagram styles
        builder.AddGlobalRules();

        // The packet styles.ts
        Add(".packetByte", ("font-size", FontSize(options["byteFontSize"])));
        Add(".packetByte.start", ("fill", options["startByteColor"]));
        Add(".packetByte.end", ("fill", options["endByteColor"]));
        Add(".packetLabel", ("fill", options["labelColor"]), ("font-size", FontSize(options["labelFontSize"])));
        Add(".packetTitle", ("fill", options["titleColor"]), ("font-size", FontSize(options["titleFontSize"])));
        Add(".packetBlock", ("stroke", options["blockStrokeColor"]), ("stroke-width", options["blockStrokeWidth"]), ("fill", options["blockFillColor"]));

        // The user styles come last
        builder.Rules.AddRange(user);
        return builder.Rules;
    }

    /// <summary>Gets black, the default of SVG paint.</summary>
    private static Rgba32 Black => new(0, 0, 0, 255);
}

/// <summary>
///     An element of a gantt chart's SVG, as the cascade sees it: its tag, its classes, its presentation attributes and
///     its inline style.
/// </summary>
/// <param name="Tag">The tag (<c>svg</c>, <c>g</c>, <c>rect</c>, <c>line</c>, <c>text</c>, <c>tspan</c>).</param>
/// <param name="Classes">The classes.</param>
internal sealed record GanttElement(string Tag, IReadOnlyList<string> Classes)
{
    /// <summary>Gets the presentation attributes (property and value), which every matching rule overrides.</summary>
    public IReadOnlyList<(string Property, string Value)> Attributes { get; init; } = [];

    /// <summary>Gets the inline style (the <c>style</c> attribute), or null.</summary>
    public StyleDeclarationSet? Inline { get; init; }

    /// <summary>
    ///     Creates an element from a class attribute (classes separated by white space).
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <param name="classAttribute">The class attribute.</param>
    /// <param name="attributes">The presentation attributes.</param>
    /// <returns>The element.</returns>
    public static GanttElement Of(string tag, string classAttribute, params (string Property, string Value)[] attributes) =>
        new(tag, classAttribute.Split(' ', StringSplitOptions.RemoveEmptyEntries)) { Attributes = attributes };
}

/// <summary>
///     The computed style of a gantt chart element: its paint with the opacity of the element and its groups, its font and
///     its text anchor.
/// </summary>
/// <param name="Fill">The fill, or null for none.</param>
/// <param name="Stroke">The stroke, or null for none.</param>
/// <param name="StrokeWidth">The stroke width in pixels.</param>
/// <param name="Dash">The dash lengths of the stroke, empty for a solid stroke.</param>
/// <param name="Opacity">The opacity of the element times the opacities of its groups.</param>
/// <param name="FontFamily">The font family.</param>
/// <param name="FontSize">The font size in pixels.</param>
/// <param name="Bold">Whether the font is bold.</param>
/// <param name="Italic">Whether the font is italic.</param>
/// <param name="TextAnchor">The text anchor: <c>start</c>, <c>middle</c> or <c>end</c>.</param>
/// <param name="Transform">The CSS transform of the element, or null.</param>
internal sealed record GanttComputedStyle(
    Rgba32? Fill,
    Rgba32? Stroke,
    double StrokeWidth,
    IReadOnlyList<double> Dash,
    double Opacity,
    string FontFamily,
    double FontSize,
    bool Bold,
    bool Italic,
    string TextAnchor,
    string? Transform)
{
    /// <summary>
    ///     Gets a color with the opacity folded into its alpha.
    /// </summary>
    /// <param name="color">The color, or null.</param>
    /// <returns>The faded color, or null.</returns>
    public Rgba32? Faded(Rgba32? color) =>
        color is { } c ? c with { A = (byte)Math.Round(c.A * Opacity, MidpointRounding.AwayFromZero) } : null;
}

/// <summary>
///     The style sheet Mermaid writes for a gantt chart (its gantt <c>styles.js</c> after the global rules of
///     <c>styles.ts</c>), built from the theme variables of a configuration, with the rules of <c>themeCSS</c> applied
///     after it, and the computed style of an element through a small CSS cascade.
/// </summary>
/// <remarks>
///     <para>
///         <b>Cascade.</b> Every rule is <c>#id selector</c> in Mermaid, so the rules that match an element are ordered by
///         the number of their classes and tags, then by their place in the sheet; an <c>!important</c> declaration wins
///         over every normal one. A rule beats a presentation attribute of the element (such as the <c>fill="#000"</c> of
///         an axis label), and the inline style beats the normal declarations of the rules. The selectors are classes
///         (one or several on one element: <c>.task.clickable</c>, <c>.doneText0.taskTextOutsideLeft</c>) and the
///         descendant selectors <c>.grid .tick</c>, <c>.grid .tick text</c> and <c>.grid path</c>; the root rule applies to
///         the svg element. Fill, stroke, the stroke width and dashes, the font and the text anchor are inherited;
///         <c>currentColor</c> is the inherited <c>color</c> (black); the element opacities multiply.
///     </para>
///     <para>
///         <b>Simplifications.</b> The opacity is folded into the fill and stroke colors (overlapping parts of one element
///         are not composited as a group), and <c>shape-rendering: crispEdges</c> is not modeled.
///     </para>
///     <para>
///         An instance is used by one render on one thread (it caches the matched rules).
///     </para>
/// </remarks>
internal sealed class GanttStyles : DiagramStyles
{
    /// <summary>The parsed selectors of the rules, in cascade order.</summary>
    private readonly List<(GanttSelector Selector, StyleDeclarationSet Declarations)> _parsed;

    /// <summary>The matched declarations by element chain.</summary>
    private readonly Dictionary<string, StyleDeclarationSet> _matched = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes the styles from their rules.
    /// </summary>
    /// <param name="rules">The rules in cascade order.</param>
    private GanttStyles(IReadOnlyList<StyleRule> rules)
        : base(rules)
    {
        _parsed = [.. rules.Select(rule => (GanttSelector.Parse(rule.Selector), rule.Declarations))];
    }

    /// <summary>Gets the selectors of the gantt chart's style sheet, which <c>themeCSS</c> may use.</summary>
    public static FrozenSet<string> KnownSelectors { get; } = BuildRules(new Dictionary<string, string?>(), [])
        .Select(rule => rule.Selector)
        .ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    ///     Builds the styles of a configuration: the gantt chart's rules from its theme variables, then the known rules of
    ///     its <c>themeCSS</c>.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="sink">The report sink for <c>themeCSS</c> problems, or null.</param>
    /// <returns>The styles.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown in strict mode at the first unsupported <c>themeCSS</c> rule.</exception>
    public static GanttStyles Create(MermaidConfig config, MermaidReportSink? sink)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new GanttStyles(BuildRules(VariablesOf(config), UserRules(config, KnownSelectors, sink)));
    }

    /// <summary>
    ///     Computes the style of an element from the svg element down to it.
    /// </summary>
    /// <param name="chain">The svg element, the groups and the element.</param>
    /// <returns>The computed style.</returns>
    public GanttComputedStyle Compute(IReadOnlyList<GanttElement> chain)
    {
        ArgumentNullException.ThrowIfNull(chain);
        var black = new Rgba32(0, 0, 0, 255);
        var color = black;
        Rgba32? fill = black;
        Rgba32? stroke = null;
        var strokeWidth = 1.0;
        IReadOnlyList<double> dash = [];
        var family = MermaidConfigResolver.DefaultFontFamily;
        var size = 16.0;
        var bold = false;
        var italic = false;
        var anchor = "start";
        var opacity = 1.0;
        string? transform = null;
        for (var depth = 0; depth < chain.Count; depth++)
        {
            var element = chain[depth];
            var set = new StyleDeclarationSet(element.Attributes.Select(a => new StyleDeclaration(a.Property, a.Value, false)))
                .Cascade(Matched(chain, depth));
            if (element.Inline is { } inline)
            {
                set = set.Cascade(inline);
            }

            // color first: currentColor refers to it
            if (set["color"] is { } colorValue && CssColor.TryParse(colorValue, out var parsedColor))
            {
                color = parsedColor;
            }

            fill = Paint(set["fill"], color, fill);
            stroke = Paint(set["stroke"], color, stroke);
            strokeWidth = set.Length("stroke-width", strokeWidth, size);
            if (set["stroke-dasharray"] is { } dashValue && StyleDeclarations.TryParseDashArray(dashValue, out var dashes))
            {
                dash = dashes;
            }

            family = set["font-family"] ?? family;
            size = set["font-size"] is { } sizeValue && StyleDeclarations.TryParseFontSize(sizeValue, size, out var parsedSize) ? parsedSize : size;
            bold = set["font-weight"] is { } weight && StyleDeclarations.TryParseBold(weight, out var isBold) ? isBold : bold;
            italic = set["font-style"] is { } style && StyleDeclarations.TryParseItalic(style, out var isItalic) ? isItalic : italic;
            anchor = set["text-anchor"] is { } anchorValue && anchorValue.Trim() is "start" or "middle" or "end" ? anchorValue.Trim() : anchor;
            opacity *= set["opacity"] is { } opacityValue && StyleDeclarations.TryParseOpacity(opacityValue, out var parsedOpacity) ? parsedOpacity : 1;
            transform = depth == chain.Count - 1 ? set["transform"] : null;
        }

        return new GanttComputedStyle(fill, stroke, strokeWidth, dash, opacity, family, size, bold, italic, anchor, transform);
    }

    /// <summary>
    ///     Gets a paint value: a color, none, <c>currentColor</c>, or the inherited paint when the value is missing or
    ///     invalid.
    /// </summary>
    /// <param name="value">The value, or null.</param>
    /// <param name="color">The current color.</param>
    /// <param name="inherited">The inherited paint.</param>
    /// <returns>The paint.</returns>
    private static Rgba32? Paint(string? value, Rgba32 color, Rgba32? inherited)
    {
        if (value is null)
        {
            return inherited;
        }

        if (value.Trim().Equals("currentColor", StringComparison.OrdinalIgnoreCase))
        {
            return color;
        }

        return StyleDeclarations.TryParsePaint(value, out var paint) ? paint : inherited;
    }

    /// <summary>
    ///     Gets the declarations of the rules that match an element, cascaded by specificity and order.
    /// </summary>
    /// <param name="chain">The chain.</param>
    /// <param name="depth">The element's depth in the chain.</param>
    /// <returns>The declarations.</returns>
    private StyleDeclarationSet Matched(IReadOnlyList<GanttElement> chain, int depth)
    {
        var key = string.Join('/', chain.Take(depth + 1).Select(e => e.Tag + "." + string.Join('.', e.Classes)));
        if (_matched.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var matching = new List<(int Specificity, int Order, StyleDeclarationSet Declarations)>();
        for (var i = 0; i < _parsed.Count; i++)
        {
            var (selector, declarations) = _parsed[i];
            if (selector.Matches(chain, depth))
            {
                matching.Add((selector.Specificity, i, declarations));
            }
        }

        var result = StyleDeclarationSet.Empty;
        foreach (var (_, _, declarations) in matching.OrderBy(m => m.Specificity).ThenBy(m => m.Order))
        {
            result = result.Cascade(declarations);
        }

        _matched[key] = result;
        return result;
    }

    /// <summary>
    ///     Builds the rules: the global rules of Mermaid's <c>getStyles</c>, the gantt chart's <c>styles.js</c> in its
    ///     order, then the user's.
    /// </summary>
    /// <param name="v">The theme variables as text.</param>
    /// <param name="user">The user rules.</param>
    /// <returns>The rules.</returns>
    private static List<StyleRule> BuildRules(IReadOnlyDictionary<string, string?> v, IReadOnlyList<StyleRule> user)
    {
        var builder = new RuleBuilder(v);
        string? Var(string name) => builder.Var(name);
        void Add(string selectors, params (string Property, string? Value)[] declarations) => builder.Add(selectors, declarations);
        string? Important(string name) => Var(name) is { } value ? value + " !important" : null;

        // Global rules (styles.ts getStyles) before the diagram styles
        builder.AddGlobalRules();

        // styles.js of the gantt chart, in its order
        Add(".mermaid-main-font", ("font-family", Var("fontFamily")));
        Add(".exclude-range", ("fill", Var("excludeBkgColor")));
        Add(".section", ("stroke", "none"), ("opacity", "0.2"));
        Add(".section0", ("fill", Var("sectionBkgColor")));
        Add(".section2", ("fill", Var("sectionBkgColor2")));
        Add(".section1, .section3", ("fill", Var("altSectionBkgColor")), ("opacity", "0.2"));
        Add(".sectionTitle0", ("fill", Var("titleColor")));
        Add(".sectionTitle1", ("fill", Var("titleColor")));
        Add(".sectionTitle2", ("fill", Var("titleColor")));
        Add(".sectionTitle3", ("fill", Var("titleColor")));
        Add(".sectionTitle", ("text-anchor", "start"), ("font-family", Var("fontFamily")));
        Add(".grid .tick", ("stroke", Var("gridColor")), ("opacity", "0.8"), ("shape-rendering", "crispEdges"));
        Add(".grid .tick text", ("font-family", Var("fontFamily")), ("fill", Var("textColor")));
        Add(".grid path", ("stroke-width", "0"));
        Add(".today", ("fill", "none"), ("stroke", Var("todayLineColor")), ("stroke-width", "2px"));
        Add(".task", ("stroke-width", "2"));
        Add(".taskText", ("text-anchor", "middle"), ("font-family", Var("fontFamily")));
        Add(".taskTextOutsideRight", ("fill", Var("taskTextDarkColor")), ("text-anchor", "start"), ("font-family", Var("fontFamily")));
        Add(".taskTextOutsideLeft", ("fill", Var("taskTextDarkColor")), ("text-anchor", "end"));
        Add(".task.clickable", ("cursor", "pointer"));
        Add(".taskText.clickable", ("cursor", "pointer"), ("fill", Important("taskTextClickableColor")), ("font-weight", "bold"));
        Add(".taskTextOutsideLeft.clickable", ("cursor", "pointer"), ("fill", Important("taskTextClickableColor")), ("font-weight", "bold"));
        Add(".taskTextOutsideRight.clickable", ("cursor", "pointer"), ("fill", Important("taskTextClickableColor")), ("font-weight", "bold"));
        Add(".taskText0, .taskText1, .taskText2, .taskText3", ("fill", Var("taskTextColor")));
        Add(".task0, .task1, .task2, .task3", ("fill", Var("taskBkgColor")), ("stroke", Var("taskBorderColor")));
        Add(".taskTextOutside0, .taskTextOutside2", ("fill", Var("taskTextOutsideColor")));
        Add(".taskTextOutside1, .taskTextOutside3", ("fill", Var("taskTextOutsideColor")));
        Add(".active0, .active1, .active2, .active3", ("fill", Var("activeTaskBkgColor")), ("stroke", Var("activeTaskBorderColor")));
        Add(".activeText0, .activeText1, .activeText2, .activeText3", ("fill", Important("taskTextDarkColor")));
        Add(".done0, .done1, .done2, .done3", ("stroke", Var("doneTaskBorderColor")), ("fill", Var("doneTaskBkgColor")), ("stroke-width", "2"));
        Add(".doneText0, .doneText1, .doneText2, .doneText3", ("fill", Important("taskTextDarkColor")));
        Add(
            ".doneText0.taskTextOutsideLeft, .doneText0.taskTextOutsideRight, .doneText1.taskTextOutsideLeft, .doneText1.taskTextOutsideRight, " +
            ".doneText2.taskTextOutsideLeft, .doneText2.taskTextOutsideRight, .doneText3.taskTextOutsideLeft, .doneText3.taskTextOutsideRight",
            ("fill", Important("taskTextOutsideColor")));
        Add(".crit0, .crit1, .crit2, .crit3", ("stroke", Var("critBorderColor")), ("fill", Var("critBkgColor")), ("stroke-width", "2"));
        Add(".activeCrit0, .activeCrit1, .activeCrit2, .activeCrit3", ("stroke", Var("critBorderColor")), ("fill", Var("activeTaskBkgColor")), ("stroke-width", "2"));
        Add(
            ".doneCrit0, .doneCrit1, .doneCrit2, .doneCrit3",
            ("stroke", Var("critBorderColor")),
            ("fill", Var("doneTaskBkgColor")),
            ("stroke-width", "2"),
            ("cursor", "pointer"),
            ("shape-rendering", "crispEdges"));
        Add(".milestone", ("transform", "rotate(45deg) scale(0.8,0.8)"));
        Add(".milestoneText", ("font-style", "italic"));
        Add(".doneCritText0, .doneCritText1, .doneCritText2, .doneCritText3", ("fill", Important("taskTextDarkColor")));
        Add(
            ".doneCritText0.taskTextOutsideLeft, .doneCritText0.taskTextOutsideRight, .doneCritText1.taskTextOutsideLeft, .doneCritText1.taskTextOutsideRight, " +
            ".doneCritText2.taskTextOutsideLeft, .doneCritText2.taskTextOutsideRight, .doneCritText3.taskTextOutsideLeft, .doneCritText3.taskTextOutsideRight",
            ("fill", Important("taskTextOutsideColor")));
        Add(".vert", ("stroke", Var("vertLineColor")));
        Add(".vertText", ("font-size", "15px"), ("text-anchor", "middle"), ("fill", Important("vertLineColor")));
        Add(".activeCritText0, .activeCritText1, .activeCritText2, .activeCritText3", ("fill", Important("taskTextDarkColor")));
        Add(".titleText", ("text-anchor", "middle"), ("font-size", "18px"), ("fill", Var("titleColor") is { Length: > 0 } title ? title : Var("textColor")), ("font-family", Var("fontFamily")));

        // The user styles come last
        builder.Rules.AddRange(user);
        return builder.Rules;
    }
}

/// <summary>
///     A selector of the gantt chart's style sheet: compound selectors of a tag and classes, joined by the descendant
///     combinator.
/// </summary>
/// <param name="Compounds">The compound selectors, from the outermost to the subject.</param>
/// <param name="IsRoot">Whether the selector is the root rule (Mermaid's <c>&amp;</c>, the svg element).</param>
internal sealed record GanttSelector(IReadOnlyList<(string? Tag, string[] Classes)> Compounds, bool IsRoot)
{
    /// <summary>Gets the specificity: 100 per class and 1 per tag (every rule has Mermaid's id prefix too).</summary>
    public int Specificity => Compounds.Sum(c => (c.Classes.Length * 100) + (c.Tag is null ? 0 : 1));

    /// <summary>
    ///     Parses a normalized selector; a selector with an attribute, a pseudo-class or a child combinator matches nothing.
    /// </summary>
    /// <param name="selector">The selector.</param>
    /// <returns>The selector.</returns>
    public static GanttSelector Parse(string selector)
    {
        if (selector == DiagramStyles.Root)
        {
            return new GanttSelector([], true);
        }

        var compounds = new List<(string? Tag, string[] Classes)>();
        foreach (var part in selector.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.IndexOfAny(['[', ':', '>', '#', '*']) >= 0)
            {
                return new GanttSelector([(null, ["\u0000"])], false);
            }

            var pieces = part.Split('.');
            compounds.Add((pieces[0].Length > 0 ? pieces[0] : null, pieces[1..]));
        }

        return new GanttSelector(compounds, false);
    }

    /// <summary>
    ///     Gets whether the selector matches an element of a chain (as a descendant of the svg element).
    /// </summary>
    /// <param name="chain">The chain from the svg element.</param>
    /// <param name="depth">The depth of the element.</param>
    /// <returns>Whether it matches.</returns>
    public bool Matches(IReadOnlyList<GanttElement> chain, int depth)
    {
        if (IsRoot)
        {
            return depth == 0;
        }

        if (depth == 0 || Compounds.Count == 0 || !MatchesCompound(Compounds[^1], chain[depth]))
        {
            return false;
        }

        // The other compounds match ancestors in order, below the svg element
        var ancestor = depth - 1;
        for (var c = Compounds.Count - 2; c >= 0; c--)
        {
            while (ancestor >= 1 && !MatchesCompound(Compounds[c], chain[ancestor]))
            {
                ancestor--;
            }

            if (ancestor < 1)
            {
                return false;
            }

            ancestor--;
        }

        return true;
    }

    /// <summary>
    ///     Gets whether a compound selector matches an element.
    /// </summary>
    /// <param name="compound">The compound.</param>
    /// <param name="element">The element.</param>
    /// <returns>Whether it matches.</returns>
    private static bool MatchesCompound((string? Tag, string[] Classes) compound, GanttElement element) =>
        (compound.Tag is null || compound.Tag == element.Tag) && compound.Classes.All(c => element.Classes.Contains(c));
}
