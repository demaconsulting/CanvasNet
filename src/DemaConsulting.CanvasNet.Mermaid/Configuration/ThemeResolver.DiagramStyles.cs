// Portions derived from mermaid (MIT); see third-party/README.md
using System.Collections.Frozen;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Mermaid.Configuration;

/// <summary>
///     The style sheet Mermaid writes for a class diagram (its <c>class/styles.js</c> after the global rules of
///     <c>styles.ts</c>), built from the theme variables of a configuration, with the rules of <c>themeCSS</c> applied
///     after it.
/// </summary>
/// <remarks>
///     <para>
///         <b>Rules.</b> The rules are those of <c>styles.js</c> in their order, with the declarations that change pixels.
///         The rules for the <c>neo</c> look and the icon rules are left out, because that look is drawn classic and the
///         class diagram has no icons. A marker rule such as <c>[id$="-compositionStart"], .composition</c> is kept as its
///         class selector (<c>.composition</c>), which matches the same marker element.
///     </para>
///     <para>
///         <b>Facts the typed resolvers encode.</b> The class diagram's labels are colored by <c>.nodeLabel</c> and
///         <c>.edgeLabel</c> (<c>classText</c>); the title Mermaid writes has the class <c>classDiagramTitleText</c>, which
///         no rule styles (the sheet's rule is for <c>.classTitleText</c>), so the title takes the root font and fill; and
///         the composition, dependency, extension, aggregation and lollipop markers take <c>lineColor</c> (and
///         <c>transparent</c> or <c>mainBkg</c> fills) with <c>!important</c>.
///     </para>
///     <para>
///         An instance is immutable.
///     </para>
/// </remarks>
internal sealed class ClassStyles : UnifiedStyles
{
    /// <summary>
    ///     Initializes the styles from their rules.
    /// </summary>
    /// <param name="rules">The rules in cascade order.</param>
    private ClassStyles(IReadOnlyList<StyleRule> rules)
        : base(rules)
    {
    }

    /// <summary>Gets the selectors of the class diagram's style sheet, which <c>themeCSS</c> may use.</summary>
    public static FrozenSet<string> KnownSelectors { get; } = BuildRules(new Dictionary<string, string?>(), [])
        .Select(rule => rule.Selector)
        .ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Gets the font and color of node labels (<c>.nodeLabel</c> on the root font: <c>classText</c>).</summary>
    public override TextPaint NodeLabel => Text(".nodeLabel", "color");

    /// <summary>Gets the font and color of edge labels (<c>.edgeLabel</c> on the root font: <c>classText</c>).</summary>
    public override TextPaint EdgeLabel => Text(".edgeLabel", "color");

    /// <summary>
    ///     Gets the selectors of the backgrounds of an HTML edge label: the div's <c>.labelBkg</c>, then the span's
    ///     <c>.edgeLabel .label span</c>.
    /// </summary>
    public override IReadOnlyList<string> HtmlEdgeLabelBackgrounds { get; } = [".labelBkg", ".edgeLabel .label span"];

    /// <summary>Gets the selector of the background rectangle of an SVG edge label (<c>.edgeLabel .label rect</c>).</summary>
    public override string SvgEdgeLabelBackground => ".edgeLabel .label rect";

    /// <summary>Gets the font and color of note labels (<c>.noteLabel .nodeLabel</c> on the root font: <c>noteTextColor</c>).</summary>
    public override TextPaint NoteLabel => Text(".noteLabel .nodeLabel", "color");

    /// <summary>Gets the paint of the divider lines of a class box (<c>.divider</c>).</summary>
    public ShapePaint Divider => Shape(".divider", 1);

    /// <summary>Gets the selector of the diagram title (<c>.classDiagramTitleText</c>, which has no rule).</summary>
    protected override string TitleSelector => ".classDiagramTitleText";

    /// <summary>
    ///     Builds the styles of a configuration: the class diagram's rules from its theme variables, then the known rules
    ///     of its <c>themeCSS</c>.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="sink">The report sink for <c>themeCSS</c> problems, or null.</param>
    /// <returns>The styles.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown in strict mode at the first unsupported <c>themeCSS</c> rule.</exception>
    public static ClassStyles Create(MermaidConfig config, MermaidReportSink? sink)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new ClassStyles(BuildRules(VariablesOf(config), UserRules(config, KnownSelectors, sink)));
    }

    /// <summary>
    ///     Builds the rules: the global rules of Mermaid's <c>getStyles</c>, the class diagram's <c>styles.js</c> in its
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
        var strokeWidth = Var("strokeWidth");
        var lineColor = Var("lineColor");
        var important = lineColor is null ? null : lineColor + " !important";

        // Global rules (styles.ts getStyles) before the diagram styles
        builder.AddGlobalRules();

        // The class diagram's styles.js (JavaScript's || for the legacy group text)
        Add(
            "g.classGroup text",
            ("fill", Var("nodeBorder") is { Length: > 0 } border ? border : Var("classText")),
            ("stroke", "none"),
            ("font-family", Var("fontFamily")),
            ("font-size", "10px"));
        Add("g.classGroup text .title", ("font-weight", "bolder"));
        Add(".cluster-label text", ("fill", Var("titleColor")));
        Add(".cluster-label span", ("color", Var("titleColor")));
        Add(".cluster-label span p", ("background-color", "transparent"));
        Add(".cluster rect", ("fill", Var("clusterBkg")), ("stroke", Var("clusterBorder")), ("stroke-width", "1px"));
        Add(".cluster text", ("fill", Var("titleColor")));
        Add(".cluster span", ("color", Var("titleColor")));
        Add(".nodeLabel,.edgeLabel", ("color", Var("classText")));
        Add(".noteLabel .nodeLabel,.noteLabel .edgeLabel", ("color", Var("noteTextColor")));
        Add(".edgeLabel .label rect", ("fill", Var("mainBkg")));
        Add(".label text", ("fill", Var("classText")));

        // The background shorthand of these two rules sets only the background color
        Add(".labelBkg", ("background-color", Var("mainBkg")));
        Add(".edgeLabel .label span", ("background-color", Var("mainBkg")));
        Add(".classTitle", ("font-weight", "bolder"));
        Add(
            ".node rect,.node circle,.node ellipse,.node polygon,.node path",
            ("fill", Var("mainBkg")),
            ("stroke", Var("nodeBorder")),
            ("stroke-width", strokeWidth));
        Add(".divider", ("stroke", Var("nodeBorder")), ("stroke-width", "1"));
        Add("g.classGroup rect", ("fill", Var("mainBkg")), ("stroke", Var("nodeBorder")));
        Add("g.classGroup line", ("stroke", Var("nodeBorder")), ("stroke-width", "1"));
        Add(".classLabel .box", ("stroke", "none"), ("stroke-width", "0"), ("fill", Var("mainBkg")), ("opacity", "0.5"));
        Add(".classLabel .label", ("fill", Var("nodeBorder")), ("font-size", "10px"));
        Add(".relation", ("stroke", lineColor), ("stroke-width", strokeWidth), ("fill", "none"));
        Add(".dashed-line", ("stroke-dasharray", "3"));
        Add(".dotted-line", ("stroke-dasharray", "1 2"));

        // The markers: filled with the line color, transparent, or the node background
        Add(".composition", ("fill", important), ("stroke", important), ("stroke-width", "1"));
        Add(".dependency", ("fill", important), ("stroke", important), ("stroke-width", "1"));
        Add(".extension", ("fill", "transparent !important"), ("stroke", important), ("stroke-width", "1"));
        Add(".aggregation", ("fill", "transparent !important"), ("stroke", important), ("stroke-width", "1"));
        Add(".lollipop", ("fill", Var("mainBkg") is { } bkg ? bkg + " !important" : null), ("stroke", important), ("stroke-width", "1"));
        Add(".edgeTerminals", ("font-size", "11px"));
        Add(".classTitleText", ("font-size", "18px"), ("fill", Var("textColor")));

        // The user styles come last
        builder.Rules.AddRange(user);
        return builder.Rules;
    }
}

/// <summary>
///     The style sheet Mermaid writes for a state diagram (its <c>state/styles.js</c> after the global rules of
///     <c>styles.ts</c>), built from the theme variables of a configuration, with the rules of <c>themeCSS</c> applied
///     after it.
/// </summary>
/// <remarks>
///     <para>
///         <b>Rules.</b> The rules are those of <c>styles.js</c> in their order, nested rules flattened as Mermaid's
///         style compiler writes them, with the declarations that change pixels (the CSS <c>rx</c> and <c>ry</c> of the
///         rectangles included). The rules for the <c>neo</c> look are left out, because that look is drawn classic.
///         JavaScript's <c>||</c> fallbacks take the second variable when the first is not set or empty.
///     </para>
///     <para>
///         <b>Facts the typed resolvers encode.</b> Node labels are colored by <c>.nodeLabel</c>
///         (<c>stateLabelColor</c>), edge labels by <c>.label div .edgeLabel</c> (<c>transitionLabelColor</c>, else
///         <c>tertiaryTextColor</c>) and note labels by <c>.statediagram-note .nodeLabel</c> (<c>noteTextColor</c>). A
///         composite state's title is colored by <c>.cluster-label</c> as an HTML label and takes the root fill as SVG
///         text. The rule <c>.statediagram .edgeLabel</c> never applies, because the class <c>statediagram</c> is on the
///         root SVG element that every selector is scoped under. The parts of the state shapes (the start circle, the
///         rectangles and their CSS corner radii, the composite and divider clusters, the transition marker) are
///         resolved by the shape library and the edge builder from these rules by their selectors.
///     </para>
///     <para>
///         An instance is immutable.
///     </para>
/// </remarks>
internal sealed class StateStyles : UnifiedStyles
{
    /// <summary>
    ///     Initializes the styles from their rules.
    /// </summary>
    /// <param name="rules">The rules in cascade order.</param>
    private StateStyles(IReadOnlyList<StyleRule> rules)
        : base(rules)
    {
    }

    /// <summary>Gets the selectors of the state diagram's style sheet, which <c>themeCSS</c> may use.</summary>
    public static FrozenSet<string> KnownSelectors { get; } = BuildRules(new Dictionary<string, string?>(), [])
        .Select(rule => rule.Selector)
        .ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Gets the font and color of node labels (<c>.nodeLabel</c> on the root font: <c>stateLabelColor</c>).</summary>
    public override TextPaint NodeLabel => Text(".nodeLabel", "color");

    /// <summary>
    ///     Gets the font and color of edge labels (<c>.label div .edgeLabel</c> on the root font:
    ///     <c>transitionLabelColor</c>, else <c>tertiaryTextColor</c>).
    /// </summary>
    public override TextPaint EdgeLabel => Text(".label div .edgeLabel", "color");

    /// <summary>Gets the font and color of note labels (<c>.statediagram-note .nodeLabel</c>: <c>noteTextColor</c>).</summary>
    public override TextPaint NoteLabel => Text(".statediagram-note .nodeLabel", "color");

    /// <summary>Gets the selector of the background rectangle of an SVG edge label (<c>.edgeLabel .label rect</c>).</summary>
    public override string SvgEdgeLabelBackground => ".edgeLabel .label rect";

    /// <summary>Gets the selector of the diagram title (<c>.statediagramTitleText</c>).</summary>
    protected override string TitleSelector => ".statediagramTitleText";

    /// <summary>
    ///     Builds the styles of a configuration: the state diagram's rules from its theme variables, then the known rules
    ///     of its <c>themeCSS</c>.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="sink">The report sink for <c>themeCSS</c> problems, or null.</param>
    /// <returns>The styles.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown in strict mode at the first unsupported <c>themeCSS</c> rule.</exception>
    public static StateStyles Create(MermaidConfig config, MermaidReportSink? sink)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new StateStyles(BuildRules(VariablesOf(config), UserRules(config, KnownSelectors, sink)));
    }

    /// <summary>
    ///     Gets the font and color of a composite state's title: <c>.cluster-label</c>'s color for an HTML label, the
    ///     root fill for SVG text (no rule of the sheet fills it).
    /// </summary>
    /// <param name="html">Whether the title is an HTML label.</param>
    /// <returns>The text paint.</returns>
    public override TextPaint ClusterTitle(bool html) => html ? Text(".cluster-label", "color") : Text(Root, "fill");

    /// <summary>
    ///     Builds the rules: the global rules of Mermaid's <c>getStyles</c>, the state diagram's <c>styles.js</c> in its
    ///     order, then the user's.
    /// </summary>
    /// <param name="v">The theme variables as text.</param>
    /// <param name="user">The user rules.</param>
    /// <returns>The rules.</returns>
    private static List<StyleRule> BuildRules(IReadOnlyDictionary<string, string?> v, IReadOnlyList<StyleRule> user)
    {
        var builder = new RuleBuilder(v);
        string? Var(string name) => builder.Var(name);
        string? Or(string first, string? second) => Var(first) is { Length: > 0 } value ? value : second;
        void Add(string selectors, params (string Property, string? Value)[] declarations) => builder.Add(selectors, declarations);
        var strokeWidth = Or("strokeWidth", "1");
        var stateBorder = Or("stateBorder", Var("nodeBorder"));
        var transitionLabel = Or("transitionLabelColor", Var("tertiaryTextColor"));
        var altBackground = Or("altBackground", "#efefef");

        // Global rules (styles.ts getStyles) before the diagram styles
        builder.AddGlobalRules();

        // The state diagram's styles.js
        Add("defs [id$=\"-barbEnd\"]", ("fill", Var("transitionColor")), ("stroke", Var("transitionColor")));
        Add("g.stateGroup text", ("fill", Var("nodeBorder")), ("stroke", "none"), ("font-size", "10px"));
        Add("g.stateGroup text", ("fill", Var("textColor")), ("stroke", "none"), ("font-size", "10px"));
        Add("g.stateGroup .state-title", ("font-weight", "bolder"), ("fill", Var("stateLabelColor")));
        Add("g.stateGroup rect", ("fill", Var("mainBkg")), ("stroke", Var("nodeBorder")));
        Add("g.stateGroup line", ("stroke", Var("lineColor")), ("stroke-width", strokeWidth));
        Add(".transition", ("stroke", Var("transitionColor")), ("stroke-width", strokeWidth), ("fill", "none"));
        Add(".stateGroup .composit", ("fill", Var("background")));
        Add(".stateGroup .alt-composit", ("fill", "#e0e0e0"));
        Add(".state-note", ("stroke", Var("noteBorderColor")), ("fill", Var("noteBkgColor")));
        Add(".state-note text", ("fill", Var("noteTextColor")), ("stroke", "none"), ("font-size", "10px"));
        Add(".stateLabel .box", ("stroke", "none"), ("stroke-width", "0"), ("fill", Var("mainBkg")), ("opacity", "0.5"));
        Add(".edgeLabel .label rect", ("fill", Var("labelBackgroundColor")), ("opacity", "0.5"));
        Add(".edgeLabel", ("background-color", Var("edgeLabelBackground")));
        Add(".edgeLabel p", ("background-color", Var("edgeLabelBackground")));
        Add(".edgeLabel rect", ("opacity", "0.5"), ("background-color", Var("edgeLabelBackground")), ("fill", Var("edgeLabelBackground")));
        Add(".edgeLabel .label text", ("fill", transitionLabel));
        Add(".label div .edgeLabel", ("color", transitionLabel));
        Add(".stateLabel text", ("fill", Var("stateLabelColor")), ("font-size", "10px"), ("font-weight", "bold"));
        Add(".node circle.state-start", ("fill", Var("specialStateColor")), ("stroke", Var("specialStateColor")));
        Add(".node .fork-join", ("fill", Var("specialStateColor")), ("stroke", Var("specialStateColor")));
        Add(".node circle.state-end", ("fill", Var("innerEndBackground")), ("stroke", Var("background")), ("stroke-width", "1.5"));
        Add(".end-state-inner", ("fill", Or("compositeBackground", Var("background"))), ("stroke-width", "1.5"));
        Add(".node rect", ("fill", Or("stateBkg", Var("mainBkg"))), ("stroke", stateBorder), ("stroke-width", strokeWidth + "px"));
        Add(".node polygon", ("fill", Var("mainBkg")), ("stroke", stateBorder), ("stroke-width", strokeWidth + "px"));
        Add("[id$=\"-barbEnd\"]", ("fill", Var("lineColor")));
        Add(".statediagram-cluster rect", ("fill", Var("compositeTitleBackground")), ("stroke", stateBorder), ("stroke-width", strokeWidth + "px"));
        Add(".cluster-label,.nodeLabel", ("color", Var("stateLabelColor")));
        Add(".statediagram-cluster rect.outer", ("rx", "5px"), ("ry", "5px"));
        Add(".statediagram-state .divider", ("stroke", stateBorder));
        Add(".statediagram-state .title-state", ("rx", "5px"), ("ry", "5px"));
        Add(".statediagram-cluster.statediagram-cluster .inner", ("fill", Or("compositeBackground", Var("background"))));
        Add(".statediagram-cluster.statediagram-cluster-alt .inner", ("fill", altBackground));
        Add(".statediagram-cluster .inner", ("rx", "0"), ("ry", "0"));
        Add(".statediagram-state rect.basic", ("rx", "5px"), ("ry", "5px"));
        Add(".statediagram-state rect.divider", ("stroke-dasharray", "10,10"), ("fill", altBackground));
        Add(".note-edge", ("stroke-dasharray", "5"));

        // The note rule is written twice
        for (var i = 0; i < 2; i++)
        {
            Add(
                ".statediagram-note rect",
                ("fill", Var("noteBkgColor")),
                ("stroke", Var("noteBorderColor")),
                ("stroke-width", "1px"),
                ("rx", "0"),
                ("ry", "0"));
        }

        Add(".statediagram-note text", ("fill", Var("noteTextColor")));
        Add(".statediagram-note .nodeLabel", ("color", Var("noteTextColor")));
        Add(".statediagram .edgeLabel", ("color", "red"));
        Add("[id$=\"-dependencyStart\"],[id$=\"-dependencyEnd\"]", ("fill", Var("lineColor")), ("stroke", Var("lineColor")), ("stroke-width", strokeWidth));
        Add(".statediagramTitleText", ("font-size", "18px"), ("fill", Var("textColor")));

        // The user styles come last
        builder.Rules.AddRange(user);
        return builder.Rules;
    }
}

/// <summary>
///     The style sheet Mermaid writes for an entity relationship diagram (its <c>er/styles.ts</c> after the global rules
///     of <c>styles.ts</c>), built from the theme variables of a configuration, with the rules of <c>themeCSS</c> applied
///     after it.
/// </summary>
/// <remarks>
///     <para>
///         <b>Rules.</b> The rules are those of <c>styles.ts</c> in their order, nested rules flattened, with the
///         declarations that change pixels and the values of the classic look. The per-color sections of the
///         <c>redux-color</c> themes and the <c>neo</c> look rules are left out (those themes are not built in, and the
///         look is drawn classic). JavaScript's <c>??</c> fallbacks take the second variable when the first is not set.
///     </para>
///     <para>
///         <b>Facts the typed resolvers encode.</b> Node labels are colored by <c>.label</c> (<c>nodeTextColor</c>, else
///         <c>textColor</c>); edge labels too, at the 14 pixels of <c>.edgeLabel .label</c>. An HTML edge label's
///         backgrounds are the div's <c>.labelBkg</c> (the faded <c>tertiaryColor</c>) and the span's
///         <c>.edgeLabel</c>. No rule colors an HTML subgraph title, so it takes the page's initial black; an SVG title
///         is filled by <c>.cluster-label text</c>. The markers take <c>.marker</c>'s <c>none !important</c> fill and
///         <c>lineColor !important</c> stroke; the zero circles keep their own white fill. The diagram title's class
///         <c>erDiagramTitleText</c> has no rule, so the title takes the root font and fill.
///     </para>
///     <para>
///         An instance is immutable.
///     </para>
/// </remarks>
internal sealed class ErStyles : UnifiedStyles
{
    /// <summary>The initial CSS color, which an HTML label inherits when no rule colors it.</summary>
    private static readonly Rgba32 InitialColor = new(0, 0, 0, 255);

    /// <summary>
    ///     Initializes the styles from their rules.
    /// </summary>
    /// <param name="rules">The rules in cascade order.</param>
    private ErStyles(IReadOnlyList<StyleRule> rules)
        : base(rules)
    {
    }

    /// <summary>Gets the selectors of the entity relationship diagram's style sheet, which <c>themeCSS</c> may use.</summary>
    public static FrozenSet<string> KnownSelectors { get; } = BuildRules(new Dictionary<string, string?>(), [])
        .Select(rule => rule.Selector)
        .ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Gets the font and color of node labels (<c>.label</c> on the root font).</summary>
    public override TextPaint NodeLabel => Text(".label", "color");

    /// <summary>Gets the font and color of edge labels (<c>.label</c>'s color at the size of <c>.edgeLabel .label</c>).</summary>
    public override TextPaint EdgeLabel => NodeLabel with { FontSize = Text(".edgeLabel .label", "color").FontSize };

    /// <summary>
    ///     Gets the selectors of the backgrounds of an HTML edge label: the div's <c>.labelBkg</c>, then the span's
    ///     <c>.edgeLabel</c>.
    /// </summary>
    public override IReadOnlyList<string> HtmlEdgeLabelBackgrounds { get; } = [".labelBkg", ".edgeLabel"];

    /// <summary>Gets the selector of the background rectangle of an SVG edge label (<c>.edgeLabel .label rect</c>).</summary>
    public override string SvgEdgeLabelBackground => ".edgeLabel .label rect";

    /// <summary>Gets the selector of the diagram title (<c>.erDiagramTitleText</c>, which has no rule).</summary>
    protected override string TitleSelector => ".erDiagramTitleText";

    /// <summary>
    ///     Builds the styles of a configuration: the entity relationship diagram's rules from its theme variables, then
    ///     the known rules of its <c>themeCSS</c>.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="sink">The report sink for <c>themeCSS</c> problems, or null.</param>
    /// <returns>The styles.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown in strict mode at the first unsupported <c>themeCSS</c> rule.</exception>
    public static ErStyles Create(MermaidConfig config, MermaidReportSink? sink)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new ErStyles(BuildRules(VariablesOf(config), UserRules(config, KnownSelectors, sink)));
    }

    /// <summary>
    ///     Gets the font and color of a subgraph title: the initial black for an HTML label (no rule colors it), the fill
    ///     of <c>.cluster-label text</c> for SVG text.
    /// </summary>
    /// <param name="html">Whether the title is an HTML label.</param>
    /// <returns>The text paint.</returns>
    public override TextPaint ClusterTitle(bool html) =>
        html ? Text(Root, "fill") with { Color = InitialColor } : Text(".cluster-label text", "fill");

    /// <summary>
    ///     Builds the rules: the global rules of Mermaid's <c>getStyles</c>, the entity relationship diagram's
    ///     <c>styles.ts</c> in its order, then the user's.
    /// </summary>
    /// <param name="v">The theme variables as text.</param>
    /// <param name="user">The user rules.</param>
    /// <returns>The rules.</returns>
    private static List<StyleRule> BuildRules(IReadOnlyDictionary<string, string?> v, IReadOnlyList<StyleRule> user)
    {
        var builder = new RuleBuilder(v);
        string? Var(string name) => builder.Var(name);
        void Add(string selectors, params (string Property, string? Value)[] declarations) => builder.Add(selectors, declarations);
        var lineColor = Var("lineColor");
        var titleColor = Var("titleColor") ?? Var("textColor");

        // Global rules (styles.ts getStyles) before the diagram styles
        builder.AddGlobalRules();

        // The entity relationship diagram's styles.ts (the classic look)
        Add(".entityBox", ("fill", Var("mainBkg")), ("stroke", Var("nodeBorder")));
        Add(".relationshipLabelBox", ("fill", Var("tertiaryColor")), ("opacity", "0.7"), ("background-color", Var("tertiaryColor")));
        Add(".relationshipLabelBox rect", ("opacity", "0.5"));
        Add(".labelBkg", ("background-color", Fade(Var("tertiaryColor"), 0.5)));
        Add(".edgeLabel", ("background-color", Var("edgeLabelBackground")));
        Add(".edgeLabel .label rect", ("fill", Var("edgeLabelBackground")));
        Add(".edgeLabel .label text", ("fill", Var("textColor")));
        Add(".edgeLabel .label", ("fill", Var("nodeBorder")), ("font-size", "14px"));
        Add(".label", ("font-family", Var("fontFamily")), ("color", Var("nodeTextColor") is { Length: > 0 } nodeText ? nodeText : Var("textColor")));
        Add(".edge-pattern-dashed", ("stroke-dasharray", "8,8"));
        Add(".node rect,.node circle,.node ellipse,.node polygon", ("fill", Var("mainBkg")), ("stroke", Var("nodeBorder")), ("stroke-width", "1px"));
        Add(".relationshipLine", ("stroke", lineColor), ("stroke-width", "1px"), ("fill", "none"));
        Add(".marker", ("fill", "none !important"), ("stroke", lineColor is null ? null : lineColor + " !important"), ("stroke-width", "1"));
        Add(".cluster rect", ("fill", Var("clusterBkg") ?? Var("mainBkg")), ("stroke", Var("clusterBorder") ?? Var("nodeBorder")), ("stroke-width", "1px"));
        Add(".cluster text", ("fill", titleColor));
        Add(".cluster-label text", ("fill", titleColor));

        // The user styles come last
        builder.Rules.AddRange(user);
        return builder.Rules;
    }
}
