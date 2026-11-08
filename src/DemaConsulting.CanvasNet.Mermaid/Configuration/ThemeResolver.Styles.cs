// Portions derived from mermaid (MIT); see third-party/README.md
using System.Collections.Frozen;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Mermaid.Configuration;

/// <summary>
///     The resolved paint of a shape: fill, stroke and stroke width.
/// </summary>
/// <param name="Fill">The fill color, or null for none.</param>
/// <param name="Stroke">The stroke color, or null for none.</param>
/// <param name="StrokeWidth">The stroke width in pixels.</param>
internal sealed record ShapePaint(Rgba32? Fill, Rgba32? Stroke, double StrokeWidth);

/// <summary>
///     The resolved font and color of a text.
/// </summary>
/// <param name="FontFamily">The CSS font family list.</param>
/// <param name="FontSize">The font size in pixels.</param>
/// <param name="Color">The text color.</param>
internal sealed record TextPaint(string FontFamily, double FontSize, Rgba32 Color);

/// <summary>
///     The style sheet Mermaid writes for a flowchart (its <c>styles.ts</c> after the global rules of <c>styles.ts</c>),
///     built from the theme variables of a configuration, with the rules of <c>themeCSS</c> applied after it.
/// </summary>
/// <remarks>
///     <para>
///         Each rule of Mermaid's style text is kept as a rule here with its selector, so a <c>themeCSS</c> rule for one
///         of these selectors overrides it as it does in a browser (the user styles come last). Only the declarations
///         that change pixels are kept (paint, stroke, dash, opacity, font and the label backgrounds); a declaration whose
///         value is not valid CSS (a theme variable that is not a color, say) is left out, as a browser drops it.
///     </para>
///     <para>
///         The typed properties resolve the rules the flowchart draws with: <see cref="Node"/>, <see cref="Cluster"/>,
///         <see cref="EdgeColor"/>, <see cref="MarkerColor"/>, <see cref="NodeLabel"/>, <see cref="ClusterLabel"/> and
///         the others. An instance is immutable.
///     </para>
/// </remarks>
internal sealed class FlowchartStyles
{
    /// <summary>The selector of the root rule (Mermaid's <c>&amp;</c>, the SVG element).</summary>
    public const string Root = "&";

    /// <summary>The rules by selector, cascaded.</summary>
    private readonly Dictionary<string, StyleDeclarationSet> _resolved;

    /// <summary>
    ///     Initializes the styles from their rules.
    /// </summary>
    /// <param name="rules">The rules in cascade order.</param>
    private FlowchartStyles(IReadOnlyList<StyleRule> rules)
    {
        Rules = rules;
        _resolved = new Dictionary<string, StyleDeclarationSet>(StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            _resolved[rule.Selector] = _resolved.TryGetValue(rule.Selector, out var existing)
                ? existing.Cascade(rule.Declarations)
                : rule.Declarations;
        }
    }

    /// <summary>Gets the selectors of the flowchart's style sheet, which <c>themeCSS</c> may use.</summary>
    public static FrozenSet<string> KnownSelectors { get; } = BuildRules(new Dictionary<string, string?>(), [])
        .Select(rule => rule.Selector)
        .ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Gets the rules in cascade order: the diagram's, then the user's.</summary>
    public IReadOnlyList<StyleRule> Rules { get; }

    /// <summary>Gets the paint of node shapes (<c>.node rect</c>).</summary>
    public ShapePaint Node => Shape(".node rect", 1);

    /// <summary>Gets the paint of subgraph rectangles (<c>.cluster rect</c>).</summary>
    public ShapePaint Cluster => Shape(".cluster rect", 1);

    /// <summary>Gets the color of edge lines (<c>.flowchart-link</c>).</summary>
    public Rgba32? EdgeColor => Resolve(".flowchart-link").Paint("stroke");

    /// <summary>Gets the stroke width of normal edges (<c>.edge-thickness-normal</c>).</summary>
    public double EdgeWidth => Resolve(".edge-thickness-normal").Length("stroke-width", 1);

    /// <summary>Gets the stroke width of thick edges (<c>.edge-thickness-thick</c>).</summary>
    public double ThickEdgeWidth => Resolve(".edge-thickness-thick").Length("stroke-width", 3.5);

    /// <summary>Gets the color of arrowheads and other markers (<c>.marker</c>).</summary>
    public Rgba32? MarkerColor => Resolve(".marker").Paint("fill");

    /// <summary>Gets the font and color of node labels (<c>.label</c> on the root font).</summary>
    public TextPaint NodeLabel => Text(".label", "color");

    /// <summary>Gets the font and color of subgraph titles (<c>.cluster-label span</c>).</summary>
    public TextPaint ClusterLabel => Text(".cluster-label span", "color");

    /// <summary>Gets the font and color of the diagram title (<c>.flowchartTitleText</c>).</summary>
    public TextPaint Title => Text(".flowchartTitleText", "fill");

    /// <summary>Gets the background of edge labels (<c>.edgeLabel</c>), or null for none.</summary>
    public Rgba32? EdgeLabelBackground => Resolve(".edgeLabel").Paint("background-color");

    /// <summary>
    ///     Builds the styles of a configuration: the flowchart's rules from its theme variables, then the known rules of
    ///     its <c>themeCSS</c>.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="sink">The report sink for <c>themeCSS</c> problems, or null.</param>
    /// <returns>The styles.</returns>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown in strict mode at the first unsupported <c>themeCSS</c> rule.</exception>
    public static FlowchartStyles Create(MermaidConfig config, MermaidReportSink? sink)
    {
        ArgumentNullException.ThrowIfNull(config);
        var variables = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, _) in config.ThemeVariables)
        {
            variables[key] = config.ThemeVariable(key);
        }

        var user = config.ThemeCss is { } css
            ? StyleDeclarations.ParseSheet(css, KnownSelectors, config.LocationOf("themeCSS"), sink)
            : [];
        return new FlowchartStyles(BuildRules(variables, user));
    }

    /// <summary>
    ///     Gets the cascaded declarations of a selector.
    /// </summary>
    /// <param name="selector">The normalized selector.</param>
    /// <returns>The declarations (empty when the selector has none).</returns>
    public StyleDeclarationSet Resolve(string selector) =>
        _resolved.TryGetValue(StyleDeclarations.NormalizeSelector(selector), out var set) ? set : StyleDeclarationSet.Empty;

    /// <summary>
    ///     Builds the rules: the global rules of Mermaid's <c>getStyles</c>, the flowchart's <c>styles.ts</c> (in their
    ///     order, the diagram rules between the two global groups), then the user's.
    /// </summary>
    /// <param name="v">The theme variables as text.</param>
    /// <param name="user">The user rules.</param>
    /// <returns>The rules.</returns>
    private static List<StyleRule> BuildRules(IReadOnlyDictionary<string, string?> v, IReadOnlyList<StyleRule> user)
    {
        string? Var(string name) => v.TryGetValue(name, out var value) ? value : null;
        var nodeTextColor = Var("nodeTextColor") is { Length: > 0 } nodeText ? nodeText : Var("textColor");
        var strokeWidth = Var("strokeWidth");
        var rules = new List<StyleRule>();
        void Add(string selectors, params (string Property, string? Value)[] declarations)
        {
            var set = new StyleDeclarationSet(declarations
                .Where(d => d.Value is not null && IsValid(d.Property, d.Value))
                .Select(d => ToDeclaration(d.Property, d.Value!)));
            foreach (var selector in selectors.Split(','))
            {
                rules.Add(new StyleRule(StyleDeclarations.NormalizeSelector(selector), set));
            }
        }

        // Global rules (styles.ts getStyles) before the diagram styles
        Add(Root, ("font-family", Var("fontFamily")), ("font-size", Var("fontSize")), ("fill", Var("textColor")));
        Add(".edge-animation-slow", ("stroke-dasharray", "9,5 !important"), ("stroke-dashoffset", "900"));
        Add(".edge-animation-fast", ("stroke-dasharray", "9,5 !important"), ("stroke-dashoffset", "900"));
        Add(".error-icon", ("fill", Var("errorBkgColor")));
        Add(".error-text", ("fill", Var("errorTextColor")), ("stroke", Var("errorTextColor")));
        Add(".edge-thickness-normal", ("stroke-width", $"{strokeWidth ?? "1"}px"));
        Add(".edge-thickness-thick", ("stroke-width", "3.5px"));
        Add(".edge-pattern-solid", ("stroke-dasharray", "0"));
        Add(".edge-thickness-invisible", ("stroke-width", "0"), ("fill", "none"));
        Add(".edge-pattern-dashed", ("stroke-dasharray", "3"));
        Add(".edge-pattern-dotted", ("stroke-dasharray", "2"));
        Add(".marker", ("fill", Var("lineColor")), ("stroke", Var("lineColor")));
        Add(".marker.cross", ("stroke", Var("lineColor")));
        Add("svg", ("font-family", Var("fontFamily")), ("font-size", Var("fontSize")));
        Add("p", ("margin", "0"));

        // The flowchart's styles.ts
        Add(".label", ("font-family", Var("fontFamily")), ("color", nodeTextColor));
        Add(".cluster-label text", ("fill", Var("titleColor")));
        Add(".cluster-label span", ("color", Var("titleColor")));
        Add(".cluster-label span p", ("background-color", "transparent"));
        Add(".label text,span", ("fill", nodeTextColor), ("color", nodeTextColor));
        Add(
            ".node rect,.node circle,.node ellipse,.node polygon,.node path",
            ("fill", Var("mainBkg")),
            ("stroke", Var("nodeBorder")),
            ("stroke-width", $"{strokeWidth ?? "1"}px"));
        Add(".node .katex path", ("fill", "#000"), ("stroke", "#000"), ("stroke-width", "1px"));
        Add(".root .anchor path", ("fill", $"{Var("lineColor")} !important"), ("stroke-width", "0"), ("stroke", Var("lineColor")));
        Add(".arrowheadPath", ("fill", Var("arrowheadColor")));
        Add(".edgePaths .path", ("stroke", Var("lineColor")), ("stroke-width", $"{strokeWidth ?? "2"}px"));
        Add(".flowchart-link", ("stroke", Var("lineColor")), ("fill", "none"));
        Add(".edgeLabel", ("background-color", Var("edgeLabelBackground")));
        Add(".edgeLabel p", ("background-color", Var("edgeLabelBackground")));
        Add(".edgeLabel rect", ("opacity", "0.5"), ("background-color", Var("edgeLabelBackground")), ("fill", Var("edgeLabelBackground")));
        Add(".labelBkg", ("background-color", Fade(Var("edgeLabelBackground"), 0.5)));
        Add(".cluster rect", ("fill", Var("clusterBkg")), ("stroke", Var("clusterBorder")), ("stroke-width", "1px"));
        Add(".cluster text", ("fill", Var("titleColor")));
        Add(".cluster span", ("color", Var("titleColor")));
        Add(".node .collapsed-indicator", ("fill", Var("clusterBorder")), ("stroke", "none"), ("opacity", "0.6"));
        Add(".node .collapsed-separator", ("stroke", Var("clusterBorder")), ("stroke-width", "0.75px"));
        Add(".flowchartTitleText", ("font-size", "18px"), ("fill", Var("textColor")));
        Add("rect.text", ("fill", "none"), ("stroke-width", "0"));
        Add(".icon-shape,.image-shape", ("background-color", Var("edgeLabelBackground")));
        Add(".icon-shape p,.image-shape p", ("background-color", Var("edgeLabelBackground")));
        Add(".icon-shape .label rect,.image-shape .label rect", ("opacity", "0.5"), ("background-color", Var("edgeLabelBackground")), ("fill", Var("edgeLabelBackground")));

        // The user styles come last
        rules.AddRange(user);
        return rules;
    }

    /// <summary>
    ///     The flowchart's <c>fade</c>: a color with the given opacity, through khroma (null when khroma cannot read it).
    /// </summary>
    /// <param name="color">The color.</param>
    /// <param name="opacity">The opacity.</param>
    /// <returns>The faded color, or null.</returns>
    private static string? Fade(string? color, double opacity)
    {
        if (color is null)
        {
            return null;
        }

        try
        {
            return ColorMath.Rgba(ColorMath.Channel(color, 'r'), ColorMath.Channel(color, 'g'), ColorMath.Channel(color, 'b'), opacity);
        }
        catch (ColorFormatException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Converts an internal declaration (with an optional <c>!important</c> suffix).
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="value">The value.</param>
    /// <returns>The declaration.</returns>
    private static StyleDeclaration ToDeclaration(string property, string value)
    {
        const string Important = "!important";
        return value.EndsWith(Important, StringComparison.Ordinal)
            ? new StyleDeclaration(property, value[..^Important.Length].Trim(), true)
            : new StyleDeclaration(property, value.Trim(), false);
    }

    /// <summary>
    ///     Gets whether an internal declaration has a valid value (a browser drops the invalid ones).
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="value">The value.</param>
    /// <returns>Whether it is valid.</returns>
    private static bool IsValid(string property, string value)
    {
        var text = ToDeclaration(property, value).Value;
        return property switch
        {
            "fill" or "stroke" => StyleDeclarations.TryParsePaint(text, out _),
            "color" or "background-color" => CssColor.TryParse(text, out _),
            "stroke-width" or "font-size" => StyleDeclarations.TryParseLength(text, 16, out _),
            _ => text.Length > 0,
        };
    }

    /// <summary>
    ///     Resolves the paint of a shape rule.
    /// </summary>
    /// <param name="selector">The selector.</param>
    /// <param name="defaultWidth">The stroke width when none is declared.</param>
    /// <returns>The paint.</returns>
    private ShapePaint Shape(string selector, double defaultWidth)
    {
        var set = Resolve(selector);
        return new ShapePaint(set.Paint("fill"), set.Paint("stroke"), set.Length("stroke-width", defaultWidth));
    }

    /// <summary>
    ///     Resolves a text rule over the root font.
    /// </summary>
    /// <param name="selector">The selector.</param>
    /// <param name="colorProperty">The property that holds the text color.</param>
    /// <returns>The text paint.</returns>
    private TextPaint Text(string selector, string colorProperty)
    {
        var root = Resolve(Root);
        var set = root.Cascade(Resolve(selector));
        var fontSize = root.Length("font-size", 16);
        return new TextPaint(
            set["font-family"] ?? MermaidConfigResolver.DefaultFontFamily,
            set.Length("font-size", fontSize, fontSize),
            set.Paint(colorProperty) ?? root.Paint("fill") ?? new Rgba32(0, 0, 0, 255));
    }
}
