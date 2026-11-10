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
///     A style sheet Mermaid writes for a diagram, built from the theme variables of a configuration: the global rules of
///     Mermaid's <c>styles.ts</c>, then the diagram's own rules, then the rules of <c>themeCSS</c>.
/// </summary>
/// <remarks>
///     <para>
///         Each rule of Mermaid's style text is kept as a rule here with its selector, so a <c>themeCSS</c> rule for one
///         of these selectors overrides it as it does in a browser (the user styles come last). Only the declarations
///         that change pixels are kept (paint, stroke, dash, opacity, font, filter and the label backgrounds); a
///         declaration whose value is not valid CSS (a theme variable that is not a color, say) is left out, as a browser
///         drops it.
///     </para>
///     <para>
///         The diagram style sheets (<see cref="FlowchartStyles"/>, <see cref="ClassStyles"/>, <see cref="StateStyles"/>,
///         <see cref="ErStyles"/>, <see cref="SequenceStyles"/>, <see cref="PieStyles"/>, <see cref="GanttStyles"/>) share the rule building, the global rules and the
///         cascade here. An instance is immutable.
///     </para>
/// </remarks>
internal abstract class DiagramStyles
{
    /// <summary>The selector of the root rule (Mermaid's <c>&amp;</c>, the SVG element).</summary>
    public const string Root = "&";

    /// <summary>The rules by selector, cascaded.</summary>
    private readonly Dictionary<string, StyleDeclarationSet> _resolved;

    /// <summary>
    ///     Initializes the styles from their rules.
    /// </summary>
    /// <param name="rules">The rules in cascade order.</param>
    protected DiagramStyles(IReadOnlyList<StyleRule> rules)
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

    /// <summary>Gets the rules in cascade order: the diagram's, then the user's.</summary>
    public IReadOnlyList<StyleRule> Rules { get; }

    /// <summary>
    ///     Gets the cascaded declarations of a selector.
    /// </summary>
    /// <param name="selector">The normalized selector.</param>
    /// <returns>The declarations (empty when the selector has none).</returns>
    public StyleDeclarationSet Resolve(string selector) =>
        _resolved.TryGetValue(StyleDeclarations.NormalizeSelector(selector), out var set) ? set : StyleDeclarationSet.Empty;

    /// <summary>
    ///     Gets the theme variables of a configuration as text.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <returns>The variables by name.</returns>
    protected static Dictionary<string, string?> VariablesOf(MermaidConfig config)
    {
        var variables = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, _) in config.ThemeVariables)
        {
            variables[key] = config.ThemeVariable(key);
        }

        return variables;
    }

    /// <summary>
    ///     Parses the rules of a configuration's <c>themeCSS</c> whose selectors are known; the rest is reported.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="knownSelectors">The selectors of the diagram's style sheet.</param>
    /// <param name="sink">The report sink, or null.</param>
    /// <returns>The user rules.</returns>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown in strict mode at the first unsupported rule.</exception>
    protected static IReadOnlyList<StyleRule> UserRules(MermaidConfig config, IReadOnlySet<string> knownSelectors, MermaidReportSink? sink) =>
        config.ThemeCss is { } css
            ? StyleDeclarations.ParseSheet(css, knownSelectors, config.LocationOf("themeCSS"), sink)
            : [];

    /// <summary>
    ///     Resolves the paint of a shape rule.
    /// </summary>
    /// <param name="selector">The selector.</param>
    /// <param name="defaultWidth">The stroke width when none is declared.</param>
    /// <returns>The paint.</returns>
    protected ShapePaint Shape(string selector, double defaultWidth)
    {
        var set = Resolve(selector);
        return new ShapePaint(set.Paint("fill"), set.Paint("stroke"), set.Length("stroke-width", defaultWidth));
    }

    /// <summary>
    ///     Mermaid's <c>fade</c> (in the flowchart's and the entity relationship diagram's styles): a color with the given
    ///     opacity, through khroma (null when khroma cannot read it).
    /// </summary>
    /// <param name="color">The color.</param>
    /// <param name="opacity">The opacity.</param>
    /// <returns>The faded color, or null.</returns>
    protected static string? Fade(string? color, double opacity)
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
    ///     Resolves a text rule over the root font.
    /// </summary>
    /// <param name="selector">The selector.</param>
    /// <param name="colorProperty">The property that holds the text color.</param>
    /// <returns>The text paint.</returns>
    protected TextPaint Text(string selector, string colorProperty)
    {
        var root = Resolve(Root);
        var set = root.Cascade(Resolve(selector));
        var fontSize = root.Length("font-size", 16);
        var size = set["font-size"] is { } value && StyleDeclarations.TryParseFontSize(value, fontSize, out var length) ? length : fontSize;
        return new TextPaint(
            set["font-family"] ?? MermaidConfigResolver.DefaultFontFamily,
            size,
            set.Paint(colorProperty) ?? root.Paint("fill") ?? new Rgba32(0, 0, 0, 255));
    }

    /// <summary>
    ///     Builds the rules of a style sheet in cascade order, keeping only the declarations a browser would accept.
    /// </summary>
    /// <param name="variables">The theme variables as text.</param>
    protected sealed class RuleBuilder(IReadOnlyDictionary<string, string?> variables)
    {
        /// <summary>Gets the rules added so far, in cascade order.</summary>
        public List<StyleRule> Rules { get; } = [];

        /// <summary>
        ///     Gets a theme variable.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <returns>The value, or null when it is not set.</returns>
        public string? Var(string name) => variables.TryGetValue(name, out var value) ? value : null;

        /// <summary>
        ///     Adds a rule for each selector of a list; declarations with no value or an invalid one are left out.
        /// </summary>
        /// <param name="selectors">The selectors, separated by commas.</param>
        /// <param name="declarations">The declarations; a value may end with <c>!important</c>.</param>
        public void Add(string selectors, params (string Property, string? Value)[] declarations)
        {
            var set = new StyleDeclarationSet(declarations
                .Where(d => d.Value is not null && IsValid(d.Property, d.Value))
                .Select(d => ToDeclaration(d.Property, d.Value!)));
            foreach (var selector in selectors.Split(','))
            {
                Rules.Add(new StyleRule(StyleDeclarations.NormalizeSelector(selector), set));
            }
        }

        /// <summary>
        ///     Adds the global rules that Mermaid's <c>getStyles</c> writes before the diagram's own rules.
        /// </summary>
        public void AddGlobalRules()
        {
            var strokeWidth = Var("strokeWidth");
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
                "stroke-width" => StyleDeclarations.TryParseLength(text, 16, out _),
                "font-size" => StyleDeclarations.TryParseFontSize(text, 16, out _),
                "filter" => SequenceStyles.TryParseFilter(text, out _),
                _ => text.Length > 0,
            };
        }
    }
}

/// <summary>
///     The style sheet of a diagram that Mermaid draws with its unified renderer (<c>rendering-util</c>): the selectors
///     that the shared shapes, clusters, edges, labels and the diagram title read, whatever diagram's <c>styles.js</c>
///     wrote them.
/// </summary>
/// <remarks>
///     <para>
///         The rendering units (the shape library, the edge builder, the cluster layout) are shared by every unified
///         diagram, so they read a diagram's sheet only through these members. Each diagram's sheet (the flowchart's,
///         the class, state and entity relationship diagrams') supplies its own rules, its title selector and the rule its label colors come from.
///     </para>
///     <para>
///         An instance is immutable.
///     </para>
/// </remarks>
internal abstract class UnifiedStyles : DiagramStyles
{
    /// <summary>
    ///     Initializes the styles from their rules.
    /// </summary>
    /// <param name="rules">The rules in cascade order.</param>
    protected UnifiedStyles(IReadOnlyList<StyleRule> rules)
        : base(rules)
    {
    }

    /// <summary>Gets the paint of node shapes (<c>.node rect</c>).</summary>
    public ShapePaint Node => Shape(".node rect", 1);

    /// <summary>Gets the paint of subgraph rectangles (<c>.cluster rect</c>).</summary>
    public ShapePaint Cluster => Shape(".cluster rect", 1);

    /// <summary>Gets the font and color of node labels, from the rule the diagram's sheet colors them with.</summary>
    public abstract TextPaint NodeLabel { get; }

    /// <summary>
    ///     Gets the font and color of edge labels; the node label's unless the diagram's sheet colors edge labels with a
    ///     rule of their own.
    /// </summary>
    public virtual TextPaint EdgeLabel => NodeLabel;

    /// <summary>
    ///     Gets the font and color of the label of a note node; the node label's unless the diagram's sheet colors note
    ///     labels with a rule of their own.
    /// </summary>
    public virtual TextPaint NoteLabel => NodeLabel;

    /// <summary>
    ///     Gets a value indicating whether the sheet centers the SVG text of a node's label on its anchor (a flowchart's
    ///     <c>.node .label text { text-anchor: middle }</c>); without the rule the text starts at its anchor.
    /// </summary>
    public virtual bool CentersNodeSvgText => true;

    /// <summary>Gets the font and color of subgraph titles (<c>.cluster-label span</c>).</summary>
    public TextPaint ClusterLabel => Text(".cluster-label span", "color");

    /// <summary>
    ///     Gets the font and color of a subgraph title as an HTML label or as SVG text; <see cref="ClusterLabel"/> for
    ///     both, unless the diagram's sheet colors the two differently.
    /// </summary>
    /// <param name="html">Whether the title is an HTML label.</param>
    /// <returns>The text paint.</returns>
    public virtual TextPaint ClusterTitle(bool html) => ClusterLabel;

    /// <summary>Gets the root font and fill of the diagram, which a text inherits where no rule of its own applies.</summary>
    public TextPaint RootText => Text(Root, "fill");

    /// <summary>Gets the font and color of the diagram title (the rule of <see cref="TitleSelector"/> on the root font).</summary>
    public TextPaint Title => Text(TitleSelector, "fill");

    /// <summary>Gets the background of edge labels (<c>.edgeLabel</c>), or null for none.</summary>
    public Rgba32? EdgeLabelBackground => Resolve(".edgeLabel").Paint("background-color");

    /// <summary>
    ///     Gets the selectors of the backgrounds of an HTML edge label, painted in order over the label's box: the
    ///     div's <c>.labelBkg</c>, then the paragraph's <c>.edgeLabel p</c> (a flowchart's rules).
    /// </summary>
    public virtual IReadOnlyList<string> HtmlEdgeLabelBackgrounds { get; } = [".labelBkg", ".edgeLabel p"];

    /// <summary>Gets the selector of the background rectangle of an SVG edge label (a flowchart's <c>.edgeLabel rect</c>).</summary>
    public virtual string SvgEdgeLabelBackground => ".edgeLabel rect";

    /// <summary>
    ///     Gets the selector of the diagram title (the CSS class that Mermaid's <c>insertTitle</c> gives the title text,
    ///     which may have no rule of its own, in which case the title takes the root font).
    /// </summary>
    protected abstract string TitleSelector { get; }

    /// <summary>
    ///     Gets the font and color of an edge's terminal label (a class relation's cardinality, Mermaid's
    ///     <c>g.edgeTerminals</c>): the size of the <c>.edgeTerminals</c> rule on the root font, and the color of an edge
    ///     label for an HTML label (its span has the <c>edgeLabel</c> class) or the root's fill for an SVG label.
    /// </summary>
    /// <param name="html">Whether the label is an HTML label.</param>
    /// <returns>The text paint.</returns>
    public TextPaint TerminalLabel(bool html)
    {
        var text = Text(".edgeTerminals", "fill");
        return html ? text with { Color = EdgeLabel.Color } : text;
    }
}

/// <summary>
///     The style sheet Mermaid writes for a flowchart (its <c>styles.ts</c> after the global rules of <c>styles.ts</c>),
///     built from the theme variables of a configuration, with the rules of <c>themeCSS</c> applied after it.
/// </summary>
/// <remarks>
///     The typed properties resolve the rules the flowchart draws with: <see cref="UnifiedStyles.Node"/>,
///     <see cref="UnifiedStyles.Cluster"/>, <see cref="EdgeColor"/>, <see cref="MarkerColor"/>, <see cref="NodeLabel"/>,
///     <see cref="UnifiedStyles.ClusterLabel"/> and the others. An instance is immutable.
/// </remarks>
internal sealed class FlowchartStyles : UnifiedStyles
{
    /// <summary>
    ///     Initializes the styles from their rules.
    /// </summary>
    /// <param name="rules">The rules in cascade order.</param>
    private FlowchartStyles(IReadOnlyList<StyleRule> rules)
        : base(rules)
    {
    }

    /// <summary>Gets the selectors of the flowchart's style sheet, which <c>themeCSS</c> may use.</summary>
    public static FrozenSet<string> KnownSelectors { get; } = BuildRules(new Dictionary<string, string?>(), [])
        .Select(rule => rule.Selector)
        .ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Gets the color of edge lines (<c>.flowchart-link</c>).</summary>
    public Rgba32? EdgeColor => Resolve(".flowchart-link").Paint("stroke");

    /// <summary>Gets the stroke width of normal edges (<c>.edge-thickness-normal</c>).</summary>
    public double EdgeWidth => Resolve(".edge-thickness-normal").Length("stroke-width", 1);

    /// <summary>Gets the stroke width of thick edges (<c>.edge-thickness-thick</c>).</summary>
    public double ThickEdgeWidth => Resolve(".edge-thickness-thick").Length("stroke-width", 3.5);

    /// <summary>Gets the color of arrowheads and other markers (<c>.marker</c>).</summary>
    public Rgba32? MarkerColor => Resolve(".marker").Paint("fill");

    /// <summary>Gets the font and color of node and edge labels (<c>.label</c> on the root font).</summary>
    public override TextPaint NodeLabel => Text(".label", "color");

    /// <summary>Gets the selector of the diagram title (<c>.flowchartTitleText</c>).</summary>
    protected override string TitleSelector => ".flowchartTitleText";

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
        return new FlowchartStyles(BuildRules(VariablesOf(config), UserRules(config, KnownSelectors, sink)));
    }

    /// <summary>
    ///     Builds the rules: the global rules of Mermaid's <c>getStyles</c>, the flowchart's <c>styles.ts</c> (in their
    ///     order, the diagram rules between the two global groups), then the user's.
    /// </summary>
    /// <param name="v">The theme variables as text.</param>
    /// <param name="user">The user rules.</param>
    /// <returns>The rules.</returns>
    private static List<StyleRule> BuildRules(IReadOnlyDictionary<string, string?> v, IReadOnlyList<StyleRule> user)
    {
        var builder = new RuleBuilder(v);
        string? Var(string name) => builder.Var(name);
        void Add(string selectors, params (string Property, string? Value)[] declarations) => builder.Add(selectors, declarations);
        var nodeTextColor = Var("nodeTextColor") is { Length: > 0 } nodeText ? nodeText : Var("textColor");
        var strokeWidth = Var("strokeWidth");

        // Global rules (styles.ts getStyles) before the diagram styles
        builder.AddGlobalRules();

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
        builder.Rules.AddRange(user);
        return builder.Rules;
    }
}
