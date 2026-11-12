// Portions derived from mermaid (MIT); see third-party/README.md
using System.Collections.Frozen;
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Mermaid.Configuration;

/// <summary>
///     An element of a git graph's SVG, as the cascade sees it: its tag, its classes, its presentation attributes and its
///     inline style.
/// </summary>
/// <param name="Tag">The tag (<c>svg</c>, <c>g</c>, <c>line</c>, <c>rect</c>, <c>circle</c>, <c>path</c>, <c>polygon</c>, <c>text</c>).</param>
/// <param name="Classes">The classes.</param>
internal sealed record GitGraphElement(string Tag, IReadOnlyList<string> Classes)
{
    /// <summary>The characters that separate the classes of a class attribute (ASCII white space).</summary>
    private static readonly char[] ClassSeparators = [' ', '\t', '\n', '\f', '\r'];

    /// <summary>Gets the presentation attributes (property and value), which every matching rule overrides.</summary>
    public IReadOnlyList<(string Property, string Value)> Attributes { get; init; } = [];

    /// <summary>Gets the inline style (the <c>style</c> attribute), or null.</summary>
    public StyleDeclarationSet? Inline { get; init; }

    /// <summary>
    ///     Creates an element from a class attribute, whose classes are separated by ASCII white space as a browser splits
    ///     them (so a commit id in the attribute is one class per word).
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <param name="classAttribute">The class attribute.</param>
    /// <param name="attributes">The presentation attributes.</param>
    /// <returns>The element.</returns>
    public static GitGraphElement Of(string tag, string classAttribute, params (string Property, string Value)[] attributes)
    {
        ArgumentNullException.ThrowIfNull(classAttribute);
        return new GitGraphElement(tag, classAttribute.Split(ClassSeparators, StringSplitOptions.RemoveEmptyEntries)) { Attributes = attributes };
    }
}

/// <summary>
///     The computed style of a git graph element: its paint with the opacity of the element and its groups, its line cap,
///     its font, its text anchor and its shadow.
/// </summary>
/// <param name="Fill">The fill, or null for none.</param>
/// <param name="Stroke">The stroke, or null for none.</param>
/// <param name="StrokeWidth">The stroke width in pixels.</param>
/// <param name="Dash">The dash lengths of the stroke, empty for a solid stroke.</param>
/// <param name="LineCap">The line cap: <c>butt</c>, <c>round</c> or <c>square</c>.</param>
/// <param name="Opacity">The opacity of the element times the opacities of its groups.</param>
/// <param name="FontFamily">The font family.</param>
/// <param name="FontSize">The font size in pixels.</param>
/// <param name="TextAnchor">The text anchor: <c>start</c>, <c>middle</c> or <c>end</c>.</param>
/// <param name="Shadow">The drop shadow of the element's <c>filter</c>, or null for none.</param>
internal sealed record GitGraphComputedStyle(
    Rgba32? Fill,
    Rgba32? Stroke,
    double StrokeWidth,
    IReadOnlyList<double> Dash,
    string LineCap,
    double Opacity,
    string FontFamily,
    double FontSize,
    string TextAnchor,
    ShadowPaint? Shadow)
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
///     The style sheet Mermaid writes for a git graph (its git <c>styles.js</c> for the built-in themes, after the global
///     rules of <c>styles.ts</c>), built from the theme variables of a configuration, with the rules of <c>themeCSS</c>
///     applied after it, and the computed style of an element through a small CSS cascade.
/// </summary>
/// <remarks>
///     <para>
///         <b>Rules.</b> The rules are those of <c>styles.js</c> in their order: <c>.commit-id</c>, <c>.commit-msg</c> and
///         <c>.branch-label</c> (light grey; their font family is a <c>var()</c> that no page defines, which a browser
///         treats as inherited, so it is left out); for each of the <c>THEME_COLOR_LIMIT</c> colors <c>i</c> (12 in
///         every theme; JavaScript's array length of the value, capped as the themes cap it), with <c>c = i % 8</c>,
///         <c>.branch-label&lt;i&gt;</c> filled <c>gitBranchLabel&lt;c&gt;</c>, <c>.commit&lt;i&gt;</c> stroked and filled
///         <c>git&lt;c&gt;</c>, <c>.commit-highlight&lt;i&gt;</c> stroked and filled <c>gitInv&lt;c&gt;</c>,
///         <c>.label&lt;i&gt;</c> filled <c>git&lt;c&gt;</c> and <c>.arrow&lt;i&gt;</c> stroked <c>git&lt;c&gt;</c>; then
///         <c>.branch</c> (the theme's <c>strokeWidth</c>, <c>commitLineColor</c> or else <c>lineColor</c>, a dash of
///         2), <c>.commit-label</c>, <c>.commit-label-bkg</c> (half opaque), <c>.tag-label</c>, <c>.tag-label-bkg</c>,
///         <c>.tag-hole</c>, <c>.commit-merge</c>, <c>.commit-reverse</c> (a stroke width of 3),
///         <c>.commit-highlight-inner</c>, <c>.arrow</c> (8 pixels wide with round caps and no fill) and
///         <c>.gitTitleText</c> (centered, 18 pixels). A font size that is a number without a unit is not a CSS length,
///         so it is dropped as a browser drops it.
///     </para>
///     <para>
///         <b>Cascade.</b> Every rule is <c>#id selector</c> in Mermaid, so the rules that match an element are ordered by
///         the number of their classes and tags, then by their place in the sheet; an <c>!important</c> declaration wins
///         over every normal one. A rule beats a presentation attribute (the white <c>fill</c> of a cherry-pick's marks),
///         and the inline style (the <c>filter</c> of a branch label in the <c>neo</c> look) beats the normal
///         declarations of the rules. The root rule applies to the svg element only, and every other rule to the
///         elements below it whose tag and classes it names: the class attribute of a commit's shapes holds the commit
///         id, so an id that is also a class of the sheet (<c>commit-reverse</c>, say) is styled by its rule, as in a
///         browser. Fill, stroke, the stroke width, dashes and line cap, the font and the text anchor are inherited;
///         the element opacities multiply; the filter applies to its element only.
///     </para>
///     <para>
///         <b>Simplifications.</b> The opacity is folded into the fill and stroke colors (overlapping parts of one element
///         are not composited as a group).
///     </para>
///     <para>
///         An instance is used by one render on one thread (it caches the matched rules).
///     </para>
/// </remarks>
internal sealed class GitGraphStyles : DiagramStyles
{
    /// <summary>The number of colors of the sheet when the theme does not say (<c>THEME_COLOR_LIMIT</c> of every theme).</summary>
    internal const int DefaultColorCount = 12;

    /// <summary>The number of named git colors (Mermaid's <c>GIT_NAMED_COLOR_COUNT</c>).</summary>
    private const int NamedColors = 8;

    /// <summary>The parsed selectors of the rules (null for a selector that matches nothing here), in cascade order.</summary>
    private readonly List<(Selector? Selector, StyleDeclarationSet Declarations)> _parsed;

    /// <summary>The matched declarations by element (tag and classes) and depth.</summary>
    private readonly Dictionary<string, StyleDeclarationSet> _matched = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes the styles from their rules.
    /// </summary>
    /// <param name="rules">The rules in cascade order.</param>
    /// <param name="branchLabelStyle">The inline style of a branch label's rectangle, or null.</param>
    private GitGraphStyles(IReadOnlyList<StyleRule> rules, StyleDeclarationSet? branchLabelStyle)
        : base(rules)
    {
        _parsed = [.. rules.Select(rule => (Selector.Parse(rule.Selector), rule.Declarations))];
        BranchLabelStyle = branchLabelStyle;
    }

    /// <summary>Gets the selectors of the git graph's style sheet, which <c>themeCSS</c> may use.</summary>
    public static FrozenSet<string> KnownSelectors { get; } = BuildRules(new Dictionary<string, string?>(), [])
        .Select(rule => rule.Selector)
        .ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    ///     Gets the inline style Mermaid gives the rectangle of a branch label: in the <c>neo</c> look, the theme's
    ///     <c>dropShadow</c> as its <c>filter</c> (left out when it is not a valid filter); otherwise null (an empty style).
    /// </summary>
    public StyleDeclarationSet? BranchLabelStyle { get; }

    /// <summary>
    ///     Builds the styles of a configuration: the git graph's rules from its theme variables, then the known rules of
    ///     its <c>themeCSS</c>.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="sink">The report sink for <c>themeCSS</c> problems, or null.</param>
    /// <returns>The styles.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown in strict mode at the first unsupported <c>themeCSS</c> rule.</exception>
    public static GitGraphStyles Create(MermaidConfig config, MermaidReportSink? sink)
    {
        ArgumentNullException.ThrowIfNull(config);
        var variables = VariablesOf(config);
        StyleDeclarationSet? labelStyle = null;
        if (config.Look == "neo" && variables.TryGetValue("dropShadow", out var shadow) && shadow is not null &&
            SequenceStyles.TryParseFilter(shadow, out _))
        {
            labelStyle = new StyleDeclarationSet([new StyleDeclaration("filter", shadow.Trim(), false)]);
        }

        return new GitGraphStyles(BuildRules(variables, UserRules(config, KnownSelectors, sink)), labelStyle);
    }

    /// <summary>
    ///     Gets the number of colors the sheet styles: JavaScript's <c>Array.from({ length: THEME_COLOR_LIMIT })</c> (the
    ///     whole part of the number, zero for a value that is not a positive number), capped as the themes cap it.
    /// </summary>
    /// <param name="limit">The theme variable, or null when it is not set (the sheet's known selectors).</param>
    /// <returns>The number of colors.</returns>
    internal static int ColorCount(string? limit)
    {
        var number = limit is null ? DefaultColorCount : Js.ToNumber(limit);
        return number >= 1 ? (int)Math.Min(Math.Floor(number), ThemeResolver.MaxColorLimit) : 0;
    }

    /// <summary>
    ///     Computes the style of an element from the svg element down to it.
    /// </summary>
    /// <param name="chain">The svg element, the groups and the element.</param>
    /// <returns>The computed style.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chain"/> is null.</exception>
    public GitGraphComputedStyle Compute(IReadOnlyList<GitGraphElement> chain)
    {
        ArgumentNullException.ThrowIfNull(chain);
        var color = new Rgba32(0, 0, 0, 255);
        Rgba32? fill = color;
        Rgba32? stroke = null;
        var strokeWidth = 1.0;
        IReadOnlyList<double> dash = [];
        var lineCap = "butt";
        var family = MermaidConfigResolver.DefaultFontFamily;
        var size = 16.0;
        var anchor = "start";
        var opacity = 1.0;
        ShadowPaint? shadow = null;
        for (var depth = 0; depth < chain.Count; depth++)
        {
            var element = chain[depth];
            var set = new StyleDeclarationSet(element.Attributes.Select(a => new StyleDeclaration(a.Property, a.Value, false)))
                .Cascade(Matched(element, depth));
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
            size = set["font-size"] is { } sizeValue && IsFontSize(sizeValue) &&
                StyleDeclarations.TryParseFontSize(sizeValue, size, out var parsedSize) ? parsedSize : size;
            strokeWidth = set.Length("stroke-width", strokeWidth, size);
            if (set["stroke-dasharray"] is { } dashValue && StyleDeclarations.TryParseDashArray(dashValue, out var dashes))
            {
                dash = dashes;
            }

            lineCap = set["stroke-linecap"]?.Trim() is "butt" or "round" or "square" ? set["stroke-linecap"]!.Trim() : lineCap;
            family = set["font-family"] ?? family;
            anchor = set["text-anchor"]?.Trim() is "start" or "middle" or "end" ? set["text-anchor"]!.Trim() : anchor;
            opacity *= set["opacity"] is { } opacityValue && StyleDeclarations.TryParseOpacity(opacityValue, out var parsedOpacity) ? parsedOpacity : 1;
            shadow = depth == chain.Count - 1 && set["filter"] is { } filter && SequenceStyles.TryParseFilter(filter, out var parsedShadow)
                ? parsedShadow
                : null;
        }

        return new GitGraphComputedStyle(fill, stroke, strokeWidth, dash, lineCap, opacity, family, size, anchor, shadow);
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
    ///     Gets whether a font size is one a browser accepts: a number without a unit is not a CSS length (unless it is
    ///     zero).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>Whether the browser keeps the declaration.</returns>
    private static bool IsFontSize(string value) =>
        !(double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number != 0);

    /// <summary>
    ///     Gets the declarations of the rules that match an element, cascaded by specificity and order.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="depth">The element's depth in the chain (0 for the svg element).</param>
    /// <returns>The declarations.</returns>
    private StyleDeclarationSet Matched(GitGraphElement element, int depth)
    {
        var key = (depth == 0 ? "0/" : "1/") + element.Tag + "." + string.Join('.', element.Classes);
        if (_matched.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var matching = new List<(int Specificity, int Order, StyleDeclarationSet Declarations)>();
        for (var i = 0; i < _parsed.Count; i++)
        {
            var (selector, declarations) = _parsed[i];
            if (selector is not null && selector.Matches(element, depth))
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
    ///     Builds the rules: the global rules of Mermaid's <c>getStyles</c>, the git graph's <c>styles.js</c> in its order,
    ///     then the user's.
    /// </summary>
    /// <param name="v">The theme variables as text.</param>
    /// <param name="user">The user rules.</param>
    /// <returns>The rules.</returns>
    private static List<StyleRule> BuildRules(IReadOnlyDictionary<string, string?> v, IReadOnlyList<StyleRule> user)
    {
        var builder = new RuleBuilder(v);
        string? Var(string name) => builder.Var(name);
        void Add(string selectors, params (string Property, string? Value)[] declarations) => builder.Add(selectors, declarations);
        string? FontSize(string name) => Var(name) is { } value && IsFontSize(value) ? value : null;

        // Global rules (styles.ts getStyles) before the diagram styles
        builder.AddGlobalRules();

        // The git graph's styles.js (normalTheme for the built-in themes)
        Add(".commit-id, .commit-msg, .branch-label", ("fill", "lightgrey"), ("color", "lightgrey"));
        var colors = ColorCount(Var("THEME_COLOR_LIMIT"));
        for (var i = 0; i < colors; i++)
        {
            var index = i.ToString(CultureInfo.InvariantCulture);
            var named = (i % NamedColors).ToString(CultureInfo.InvariantCulture);
            Add($".branch-label{index}", ("fill", Var("gitBranchLabel" + named)));
            Add($".commit{index}", ("stroke", Var("git" + named)), ("fill", Var("git" + named)));
            Add($".commit-highlight{index}", ("stroke", Var("gitInv" + named)), ("fill", Var("gitInv" + named)));
            Add($".label{index}", ("fill", Var("git" + named)));
            Add($".arrow{index}", ("stroke", Var("git" + named)));
        }

        Add(".branch", ("stroke-width", Var("strokeWidth")), ("stroke", Var("commitLineColor") ?? Var("lineColor")), ("stroke-dasharray", "2"));
        Add(".commit-label", ("font-size", FontSize("commitLabelFontSize")), ("fill", Var("commitLabelColor")));
        Add(".commit-label-bkg", ("font-size", FontSize("commitLabelFontSize")), ("fill", Var("commitLabelBackground")), ("opacity", "0.5"));
        Add(".tag-label", ("font-size", FontSize("tagLabelFontSize")), ("fill", Var("tagLabelColor")));
        Add(".tag-label-bkg", ("fill", Var("tagLabelBackground")), ("stroke", Var("tagLabelBorder")));
        Add(".tag-hole", ("fill", Var("textColor")));
        Add(".commit-merge", ("stroke", Var("primaryColor")), ("fill", Var("primaryColor")));
        Add(".commit-reverse", ("stroke", Var("primaryColor")), ("fill", Var("primaryColor")), ("stroke-width", "3"));
        Add(".commit-highlight-outer");
        Add(".commit-highlight-inner", ("stroke", Var("primaryColor")), ("fill", Var("primaryColor")));
        Add(".arrow", ("stroke-width", "8"), ("stroke-linecap", "round"), ("fill", "none"));
        Add(".gitTitleText", ("text-anchor", "middle"), ("font-size", "18px"), ("fill", Var("textColor")));

        // The user styles come last
        builder.Rules.AddRange(user);
        return builder.Rules;
    }

    /// <summary>
    ///     A selector of the sheet: the root rule, or one compound selector of a tag and classes (a descendant selector, an
    ///     attribute or a pseudo-class matches no element of a git graph and is not parsed).
    /// </summary>
    /// <param name="IsRoot">Whether the selector is the root rule (Mermaid's <c>&amp;</c>, the svg element).</param>
    /// <param name="Tag">The tag, or null for any.</param>
    /// <param name="Classes">The classes.</param>
    private sealed record Selector(bool IsRoot, string? Tag, string[] Classes)
    {
        /// <summary>Gets the specificity: 100 per class and 1 per tag (every rule has Mermaid's id prefix too).</summary>
        public int Specificity => (Classes.Length * 100) + (Tag is null ? 0 : 1);

        /// <summary>
        ///     Parses a normalized selector.
        /// </summary>
        /// <param name="selector">The selector.</param>
        /// <returns>The selector, or null when it matches no element of a git graph.</returns>
        public static Selector? Parse(string selector)
        {
            if (selector == Root)
            {
                return new Selector(true, null, []);
            }

            if (selector.Length == 0 || selector.IndexOfAny([' ', '[', ':', '>', '#', '*', '+', '~']) >= 0)
            {
                return null;
            }

            var pieces = selector.Split('.');
            return new Selector(false, pieces[0].Length > 0 ? pieces[0] : null, pieces[1..]);
        }

        /// <summary>
        ///     Gets whether the selector matches an element at a depth of the chain (the root rule the svg element, every
        ///     other rule an element below it).
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="depth">The element's depth.</param>
        /// <returns>Whether it matches.</returns>
        public bool Matches(GitGraphElement element, int depth) =>
            IsRoot
                ? depth == 0
                : depth > 0 && (Tag is null || Tag == element.Tag) && Classes.All(c => element.Classes.Contains(c));
    }
}
