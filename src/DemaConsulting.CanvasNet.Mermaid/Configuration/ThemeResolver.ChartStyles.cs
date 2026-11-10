// Portions derived from mermaid (MIT); see third-party/README.md
using System.Collections.Frozen;
using System.Globalization;
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
