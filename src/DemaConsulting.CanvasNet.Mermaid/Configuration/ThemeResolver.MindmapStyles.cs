// Portions derived from mermaid (MIT); see third-party/README.md
using System.Collections.Frozen;
using System.Globalization;

namespace DemaConsulting.CanvasNet.Mermaid.Configuration;

/// <summary>
///     The style sheet Mermaid writes for a mindmap (its <c>mindmap/styles.ts</c> after the global rules of
///     <c>styles.ts</c>), built from the theme variables of a configuration, with the rules of <c>themeCSS</c> applied
///     after it.
/// </summary>
/// <remarks>
///     <para>
///         <b>Rules.</b> The rules are those of <c>styles.ts</c> in their order, with the declarations that change pixels
///         and the values of the classic look: <c>.edge</c>'s stroke width of 3; then, for each of the
///         <c>THEME_COLOR_LIMIT</c> section colors <c>i</c> (12 in every theme), with <c>k = i - 1</c>, the fill
///         <c>cScale&lt;i&gt;</c> of the <c>rect</c>, <c>path</c>, <c>circle</c> and <c>polygon</c> elements under
///         <c>.section-k</c>, the label color <c>cScaleLabel&lt;i&gt;</c> of its <c>text</c> and <c>span</c>, the stroke
///         <c>cScale&lt;i&gt;</c> of <c>.section-edge-k</c>, the stroke width <c>17 - 3i</c> of <c>.edge-depth-k</c> and the
///         stroke <c>cScaleInv&lt;i&gt;</c> 3 pixels wide of the <c>line</c> under <c>.section-k</c>; then the root's fill
///         <c>git0</c> and label color <c>gitBranchLabel0</c> under <c>.section-root</c>, and <c>.edge</c>'s fill of none.
///         A stroke width below zero (<c>.edge-depth-5</c> onward) is not valid CSS, so it is left out as a browser drops
///         it, and such an edge keeps <c>.edge</c>'s 3. No rule strokes a node's shape. When the theme variable
///         <c>useGradient</c> and <c>mainBkg</c> are truthy (the base, dark, forest and neutral themes), the gradient
///         rules come last, and of them only the stroke width of zero of the <c>line</c> under each <c>.section-k</c>
///         applies to the classic look: those themes draw no line under a default node. The <c>neo</c> look's rules,
///         the <c>redux</c> themes' values and the <c>disabled</c> rules are left out (the look is drawn classic, those
///         themes are not built in, and no node is disabled).
///     </para>
///     <para>
///         <b>Cascade.</b> A node's group has the classes <c>mindmap-node</c> and <c>section-k</c> (the root
///         <c>section-root section--1</c>) and any class of its own, so the rules for an element of its shape or label
///         are the <c>.class element</c> rules of its classes, which all weigh the same and apply in sheet order: the
///         root's <c>.section-root</c> rules come after <c>.section--1</c>'s, so the root takes <c>git0</c>. An edge's
///         paint comes from the single-class rules of its classes (<c>edge</c>, <c>section-edge-k</c> and
///         <c>edge-depth-n</c>), which the edge builder cascades. Node labels are not centered by any rule, so SVG text
///         starts at its anchor.
///     </para>
///     <para>
///         An instance is immutable.
///     </para>
/// </remarks>
internal sealed class MindmapStyles : UnifiedStyles
{
    /// <summary>The number of section colors when the theme does not say (<c>THEME_COLOR_LIMIT</c> of every theme).</summary>
    internal const int DefaultSectionColors = 12;

    /// <summary>The elements of a node's shape that the section rules fill.</summary>
    private static readonly string[] FilledElements = ["rect", "path", "circle", "polygon"];

    /// <summary>
    ///     Initializes the styles from their rules.
    /// </summary>
    /// <param name="rules">The rules in cascade order.</param>
    private MindmapStyles(IReadOnlyList<StyleRule> rules)
        : base(rules)
    {
    }

    /// <summary>Gets the selectors of the mindmap's style sheet, which <c>themeCSS</c> may use.</summary>
    public static FrozenSet<string> KnownSelectors { get; } = BuildRules(new Dictionary<string, string?>(), [], gradient: false)
        .Select(rule => rule.Selector)
        .ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Gets the font and color of a label no section rule colors: the root font and fill.</summary>
    public override TextPaint NodeLabel => Text(Root, "fill");

    /// <summary>
    ///     Gets a value indicating whether the SVG text of a node's label is centered on its anchor: no, because the
    ///     mindmap sheet has no <c>text-anchor</c> rule for the labels of its nodes.
    /// </summary>
    public override bool CentersNodeSvgText => false;

    /// <summary>Gets the selector of the diagram title: the root, because a mindmap draws no title.</summary>
    protected override string TitleSelector => Root;

    /// <summary>
    ///     Builds the styles of a configuration: the mindmap's rules from its theme variables, then the known rules of
    ///     its <c>themeCSS</c>.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="sink">The report sink for <c>themeCSS</c> problems, or null.</param>
    /// <returns>The styles.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown in strict mode at the first unsupported <c>themeCSS</c> rule.</exception>
    public static MindmapStyles Create(MermaidConfig config, MermaidReportSink? sink)
    {
        ArgumentNullException.ThrowIfNull(config);
        var gradient = MermaidConfigResolver.IsTruthy(config.ThemeVariables["useGradient"]) &&
            MermaidConfigResolver.IsTruthy(config.ThemeVariables["mainBkg"]);
        return new MindmapStyles(BuildRules(VariablesOf(config), UserRules(config, KnownSelectors, sink), gradient));
    }

    /// <summary>
    ///     Gets the paint of an element of a node's shape: the <c>.class element</c> rules of the node's classes in sheet
    ///     order (the section fill of the body, the section line of a default node); no rule strokes the body.
    /// </summary>
    /// <param name="cssClasses">The node's classes, separated by spaces.</param>
    /// <param name="element">The SVG element the shape draws: <c>rect</c>, <c>circle</c>, <c>polygon</c>, <c>path</c> or <c>line</c>.</param>
    /// <returns>The paint.</returns>
    public override ShapePaint NodePaintFor(string cssClasses, string element)
    {
        ArgumentNullException.ThrowIfNull(cssClasses);
        ArgumentNullException.ThrowIfNull(element);
        var set = ClassRules(cssClasses, element);
        return new ShapePaint(set.Paint("fill"), set.Paint("stroke"), set.Length("stroke-width", 1));
    }

    /// <summary>
    ///     Gets the font and color of a node's label: the root font with the <c>.class span</c> rules (an HTML label's
    ///     color) or the <c>.class text</c> rules (SVG text's fill) of the node's classes in sheet order.
    /// </summary>
    /// <param name="cssClasses">The node's classes, separated by spaces.</param>
    /// <param name="html">Whether the label is an HTML label rather than SVG text.</param>
    /// <returns>The text paint.</returns>
    public override TextPaint NodeLabelFor(string cssClasses, bool html)
    {
        ArgumentNullException.ThrowIfNull(cssClasses);
        return html ? Text(ClassRules(cssClasses, "span"), "color") : Text(ClassRules(cssClasses, "text"), "fill");
    }

    /// <summary>
    ///     Cascades, in sheet order, the rules of the form <c>.class element</c> whose class is one of a node's classes.
    /// </summary>
    /// <param name="cssClasses">The node's classes, separated by spaces.</param>
    /// <param name="element">The element.</param>
    /// <returns>The cascaded declarations.</returns>
    private StyleDeclarationSet ClassRules(string cssClasses, string element)
    {
        var classes = cssClasses.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var set = StyleDeclarationSet.Empty;
        foreach (var rule in Rules)
        {
            var selector = rule.Selector;
            var space = selector.IndexOf(' ', StringComparison.Ordinal);
            if (selector.Length > 0 && selector[0] == '.' && space > 1 &&
                selector.AsSpan(space + 1).SequenceEqual(element) &&
                classes.Contains(selector[1..space]))
            {
                set = set.Cascade(rule.Declarations);
            }
        }

        return set;
    }

    /// <summary>
    ///     Builds the rules: the global rules of Mermaid's <c>getStyles</c>, the mindmap's <c>styles.ts</c> in its order,
    ///     then the user's.
    /// </summary>
    /// <param name="v">The theme variables as text.</param>
    /// <param name="user">The user rules.</param>
    /// <param name="gradient">Whether the gradient rules are written (<c>useGradient</c> and <c>mainBkg</c> are truthy).</param>
    /// <returns>The rules.</returns>
    private static List<StyleRule> BuildRules(IReadOnlyDictionary<string, string?> v, IReadOnlyList<StyleRule> user, bool gradient)
    {
        var builder = new RuleBuilder(v);
        string? Var(string name) => builder.Var(name);
        void Add(string selectors, params (string Property, string? Value)[] declarations) => builder.Add(selectors, declarations);

        // Global rules (styles.ts getStyles) before the diagram styles
        builder.AddGlobalRules();

        // The mindmap's styles.ts
        Add(".edge", ("stroke-width", "3"));
        var sections = SectionColors(Var("THEME_COLOR_LIMIT"));
        for (var i = 0; i < sections; i++)
        {
            var k = (i - 1).ToString(CultureInfo.InvariantCulture);
            var index = i.ToString(CultureInfo.InvariantCulture);
            Add(string.Join(",", FilledElements.Select(element => $".section-{k} {element}")), ("fill", Var("cScale" + index)));
            Add($".section-{k} text", ("fill", Var("cScaleLabel" + index)));
            Add($".section-{k} span", ("color", Var("cScaleLabel" + index)));
            Add($".section-edge-{k}", ("stroke", Var("cScale" + index)));
            Add($".edge-depth-{k}", ("stroke-width", (17 - (3 * i)).ToString(CultureInfo.InvariantCulture)));
            Add($".section-{k} line", ("stroke", Var("cScaleInv" + index)), ("stroke-width", "3"));
        }

        Add(string.Join(",", FilledElements.Select(element => $".section-root {element}")), ("fill", Var("git0")));
        Add(".section-root text", ("fill", Var("gitBranchLabel0")));
        Add(".section-root span", ("color", Var("gitBranchLabel0")));
        Add(".edge", ("fill", "none"));

        // The gradient rules (genGradient): in the classic look only the lines' stroke width of zero applies
        for (var i = 0; gradient && i < sections; i++)
        {
            Add($".section-{(i - 1).ToString(CultureInfo.InvariantCulture)} line", ("stroke-width", "0"));
        }

        // The user styles come last
        builder.Rules.AddRange(user);
        return builder.Rules;
    }

    /// <summary>
    ///     Gets the number of section colors the sheet styles: <c>THEME_COLOR_LIMIT</c> as a JavaScript loop bound, capped
    ///     as the themes cap it.
    /// </summary>
    /// <param name="limit">The theme variable, or null when it is not set (the sheet's known selectors).</param>
    /// <returns>The number of section colors.</returns>
    private static int SectionColors(string? limit)
    {
        var number = limit is null ? DefaultSectionColors : Js.ToNumber(limit);
        return number > 0 ? (int)Math.Min(Math.Ceiling(number), ThemeResolver.MaxColorLimit) : 0;
    }
}
