// Portions derived from mermaid (MIT); see third-party/README.md
using System.Collections.Frozen;

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
