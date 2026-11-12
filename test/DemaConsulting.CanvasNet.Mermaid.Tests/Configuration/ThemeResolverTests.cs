using System.Globalization;
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Configuration;

/// <summary>
///     Unit tests of <see cref="ThemeResolver"/>: the five themes against the variables Mermaid computes, the JavaScript
///     object model, the bounds, and the flowchart and sequence style rules built from the variables.
/// </summary>
public class ThemeResolverTests
{
    /// <summary>The number of global rules Mermaid's <c>getStyles</c> writes before a diagram's rules.</summary>
    private const int GlobalRuleCount = 15;

    /// <summary>A sequence diagram whose configuration the style tests resolve.</summary>
    private const string SequenceText = "sequenceDiagram\n  Alice->>Bob: Hi\n";

    /// <summary>A mindmap whose configuration the style tests resolve.</summary>
    private const string MindmapText = "mindmap\n  root\n    child\n";

    /// <summary>A git graph whose configuration the style tests resolve.</summary>
    private const string GitGraphText = "gitGraph\n  commit\n";

    /// <summary>Gets the svg element of a git graph's element chains.</summary>
    private static GitGraphElement Svg { get; } = GitGraphElement.Of("svg", string.Empty);

    /// <summary>
    ///     Gets the git graph goldens, by file name.
    /// </summary>
    public static TheoryData<string> GitGraphGoldens
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference", "gitgraph"), "*.layout.json").Order(StringComparer.Ordinal))
            {
                data.Add(Path.GetFileName(file));
            }

            return data;
        }
    }

    /// <summary>
    ///     Gets the theme cases: every theme without overrides, with <c>themeVariables</c>, and in dark mode.
    /// </summary>
    public static TheoryData<string, string> SiteCases
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var theme in (string[])["default", "base", "dark", "forest", "neutral"])
            {
                data.Add(theme, $"site-{theme}");
                data.Add(theme, $"site-{theme}-themeVariables");
                data.Add(theme, $"site-{theme}-darkMode");
            }

            return data;
        }
    }

    /// <summary>
    ///     Validates that every variable of a theme equals Mermaid's (string-equal colors), without overrides, with
    ///     overrides of base and derived variables, and with <c>darkMode</c>.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <param name="name">The vector case.</param>
    [Theory]
    [MemberData(nameof(SiteCases))]
    public void ThemeResolver_GetThemeVariables_MatchesMermaid(string theme, string name)
    {
        // Arrange
        var vector = UpstreamVectors.Config(name);
        var overrides = vector["site"]!["themeVariables"]?.DeepClone();

        // Act
        var variables = ThemeResolver.GetThemeVariables(theme, overrides);

        // Assert
        var difference = UpstreamVectors.Difference(vector["expected"]!["themeVariables"], variables);
        Assert.True(difference is null, $"{name}: {difference}");
    }

    /// <summary>
    ///     Validates the well-known flowchart colors of the default theme.
    /// </summary>
    [Fact]
    public void ThemeResolver_GetThemeVariables_DefaultTheme_FlowchartColors()
    {
        // Act
        var variables = ThemeResolver.GetThemeVariables("default", null);

        // Assert
        Assert.Equal("#ECECFF", (string?)variables["mainBkg"]);
        Assert.Equal("#9370DB", (string?)variables["nodeBorder"]);
        Assert.Equal("#ffffde", (string?)variables["clusterBkg"]);
        Assert.Equal("hsl(240, 60%, 86.2745098039%)", (string?)variables["primaryBorderColor"]);
    }

    /// <summary>
    ///     Validates that an override of a base color is derived into the other variables, and that an override of a
    ///     derived variable wins.
    /// </summary>
    [Fact]
    public void ThemeResolver_GetThemeVariables_BaseOverride_DerivesAndKeepsOverrides()
    {
        // Arrange
        var overrides = new JsonObject { ["primaryColor"] = "#ff0000", ["nodeBorder"] = "#000000" };

        // Act
        var variables = ThemeResolver.GetThemeVariables("base", overrides);

        // Assert
        Assert.Equal("#ff0000", (string?)variables["mainBkg"]);
        Assert.Equal("#000000", (string?)variables["nodeBorder"]);
        Assert.Equal("hsl(0, 60%, 40%)", (string?)variables["primaryBorderColor"]);
    }

    /// <summary>
    ///     Validates that overrides that are not an object are ignored, as <c>typeof overrides !== 'object'</c>.
    /// </summary>
    [Fact]
    public void ThemeResolver_GetThemeVariables_NonObjectOverrides_Ignored()
    {
        // Act
        var plain = ThemeResolver.GetThemeVariables("forest", null);
        var text = ThemeResolver.GetThemeVariables("forest", JsonValue.Create("red"));

        // Assert
        Assert.True(JsonNode.DeepEquals(plain, text));
    }

    /// <summary>
    ///     Validates that a hostile <c>THEME_COLOR_LIMIT</c> keeps the loops bounded (khroma then fails on the missing
    ///     scale colors, as in Mermaid).
    /// </summary>
    [Fact]
    public void ThemeResolver_GetThemeVariables_HugeColorLimit_IsBounded()
    {
        // Arrange
        var overrides = new JsonObject { ["THEME_COLOR_LIMIT"] = 1e12 };

        // Act
        var ex = Assert.Throws<ColorFormatException>(() => ThemeResolver.GetThemeVariables("default", overrides));

        // Assert
        Assert.Same(ThemeObject.Undefined, ex.Value);
    }

    /// <summary>
    ///     Validates that a theme variable khroma cannot read makes the computation fail with the value.
    /// </summary>
    [Fact]
    public void ThemeResolver_GetThemeVariables_NotAColor_ThrowsWithValue()
    {
        // Arrange
        var overrides = new JsonObject { ["primaryColor"] = "nonsense" };

        // Act
        var ex = Assert.Throws<ColorFormatException>(() => ThemeResolver.GetThemeVariables("base", overrides));

        // Assert
        Assert.Equal("nonsense", ex.Value);
    }

    /// <summary>
    ///     Validates the theme names: the five supported ones, the newer Mermaid themes that are not, and an unknown one.
    /// </summary>
    [Fact]
    public void ThemeResolver_IsTheme_KnowsFiveThemes()
    {
        // Act and Assert
        Assert.All(MermaidRenderOptions.ThemeNames, name => Assert.True(ThemeResolver.IsTheme(name)));
        Assert.All(ThemeResolver.UnsupportedMermaidThemes, name => Assert.False(ThemeResolver.IsTheme(name)));
        Assert.False(ThemeResolver.IsTheme("Dark"));
        Assert.False(ThemeResolver.IsTheme(5.0));
        Assert.Throws<ArgumentException>(() => ThemeResolver.GetThemeVariables("neo", null));
    }

    /// <summary>
    ///     Validates the JavaScript object model: undefined, <c>||</c>, <c>??</c>, member access and the JSON form.
    /// </summary>
    [Fact]
    public void ThemeObject_JavaScriptSemantics()
    {
        // Arrange
        var t = new ThemeObject();
        t["a"] = 0.0;
        t["b"] = ThemeObject.Undefined;
        t["c"] = string.Empty;

        // Act
        t.Or("a", () => "or");
        t.Nullish("b", () => "nullish");
        t.Nullish("c", () => "kept");
        var json = t.ToJson();

        // Assert
        Assert.Equal("or", t["a"]);
        Assert.Equal("nullish", t["b"]);
        Assert.Equal(string.Empty, t["c"]);
        Assert.Same(ThemeObject.Undefined, t["missing"]);
        Assert.Same(ThemeObject.Undefined, Js.Member("text", "length"));
        Assert.Equal(["a", "b", "c"], t.Keys);
        Assert.Equal("{\"a\":\"or\",\"b\":\"nullish\",\"c\":\"\"}", json.ToJsonString());
    }

    /// <summary>
    ///     Validates the flowchart style rules of the default theme: Mermaid's node, cluster, edge, marker and label paint.
    /// </summary>
    [Fact]
    public void FlowchartStyles_Create_DefaultTheme_ResolvesPaint()
    {
        // Arrange
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), MermaidTestHelpers.FlowchartText);

        // Act
        var styles = FlowchartStyles.Create(config, null);

        // Assert
        Assert.Equal(new ShapePaint(Color("#ECECFF"), Color("#9370DB"), 1), styles.Node);
        Assert.Equal(new ShapePaint(Color("#ffffde"), Color("#aaaa33"), 1), styles.Cluster);
        Assert.Equal(Color("#333333"), styles.EdgeColor);
        Assert.Equal(Color("#333333"), styles.MarkerColor);
        Assert.Equal(1, styles.EdgeWidth);
        Assert.Equal(3.5, styles.ThickEdgeWidth);
        Assert.Equal(new TextPaint("\"trebuchet ms\", verdana, arial, sans-serif", 16, Color("#333")), styles.NodeLabel);
        Assert.Equal(new TextPaint("\"trebuchet ms\", verdana, arial, sans-serif", 18, Color("#333")), styles.Title);
        Assert.Equal(CssColor.Parse("rgba(232,232,232, 0.8)"), styles.EdgeLabelBackground);
        Assert.Equal("rgba(232, 232, 232, 0.5)", styles.Resolve(".labelBkg")["background-color"]);
    }

    /// <summary>
    ///     Validates that a <c>themeCSS</c> rule of a known selector overrides the theme, that an important theme rule is
    ///     kept, and that an unknown selector is reported and not applied.
    /// </summary>
    [Fact]
    public void FlowchartStyles_Create_ThemeCss_OverridesKnownSelectors()
    {
        // Arrange
        const string Text = "%%{init: {\"themeCSS\": \".node rect { fill: #ff0000; stroke-width: 3px } .root .anchor path { fill: blue } .mine { fill: red }\"}}%%\nflowchart LR\n  A --> B\n";
        var source = Mermaid.Syntax.Preprocessor.Run(Text, "t.mmd", MermaidLimits.Default);
        var sink = new MermaidReportSink(100, throwOnUnsupported: false);
        var config = MermaidConfigResolver.Resolve(source, "flowchart-v2", null, sink);
        var reports = new List<MermaidUnsupportedFeature>();

        // Act
        var styles = FlowchartStyles.Create(config, sink);
        sink.Deliver(null, reports.Add);

        // Assert
        Assert.Equal(new ShapePaint(Color("#ff0000"), Color("#9370DB"), 3), styles.Node);
        Assert.Equal("#333333", styles.Resolve(".root .anchor path")["fill"]);
        var report = Assert.Single(reports);
        Assert.Equal(MermaidDiagnosticCodes.ThemeCssUnsupported, report.Feature);
        Assert.Equal(new MermaidLocation("t.mmd", 1, 1), report.Location);
    }

    /// <summary>
    ///     Validates that a theme variable that is not a color drops the declarations that use it, so the rule below it
    ///     stays in effect, as a browser drops an invalid declaration.
    /// </summary>
    [Fact]
    public void FlowchartStyles_Create_InvalidVariable_DropsDeclaration()
    {
        // Arrange
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(
            new JsonObject { ["themeVariables"] = new JsonObject { ["nodeBorder"] = "nonsense" } },
            MermaidTestHelpers.FlowchartText);

        // Act
        var styles = FlowchartStyles.Create(config, null);

        // Assert
        Assert.Equal("nonsense", config.ThemeVariable("nodeBorder"));
        Assert.Equal(Color("#ECECFF"), styles.Node.Fill);
        Assert.Null(styles.Node.Stroke);
    }

    /// <summary>
    ///     Validates the class diagram's style rules of the default theme: the global rules first, the node and cluster
    ///     paint, the <c>classText</c> labels, the title in the root font (its class has no rule), the dividers, the 11
    ///     pixel terminal labels and the marker rules.
    /// </summary>
    [Fact]
    public void ClassStyles_Create_DefaultTheme_ResolvesClassRules()
    {
        // Arrange
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), "classDiagram\n  class A\n");
        const string Family = "\"trebuchet ms\", verdana, arial, sans-serif";

        // Act
        var styles = ClassStyles.Create(config, null);

        // Assert
        Assert.Equal(
            FlowchartStyles.Create(config, null).Rules.Take(GlobalRuleCount).Select(rule => rule.Selector),
            styles.Rules.Take(GlobalRuleCount).Select(rule => rule.Selector));
        Assert.Equal(new ShapePaint(Color("#ECECFF"), Color("#9370DB"), 1), styles.Node);
        Assert.Equal(new ShapePaint(Color("#ffffde"), Color("#aaaa33"), 1), styles.Cluster);
        Assert.Equal(new TextPaint(Family, 16, Color("#131300")), styles.NodeLabel);
        Assert.Equal(new TextPaint(Family, 16, Color("#131300")), styles.EdgeLabel);
        Assert.Equal(new TextPaint(Family, 16, Color("#333")), styles.Title);
        Assert.Equal(new ShapePaint(null, Color("#9370DB"), 1), styles.Divider);
        Assert.Equal(new TextPaint(Family, 11, Color("#131300")), styles.TerminalLabel(true));
        Assert.Equal(new TextPaint(Family, 11, Color("#333")), styles.TerminalLabel(false));
        Assert.Equal(("#333333", "#333333", "transparent", "#ECECFF"), (
            styles.Resolve(".composition")["fill"],
            styles.Resolve(".dependency")["stroke"],
            styles.Resolve(".extension")["fill"],
            styles.Resolve(".lollipop")["fill"]));
    }

    /// <summary>
    ///     Validates the class sheet's note labels (<c>noteTextColor</c>) and edge label backgrounds (the span's and the
    ///     SVG rectangle's selectors), where a flowchart keeps its own.
    /// </summary>
    [Fact]
    public void ClassStyles_Create_DefaultTheme_ResolvesNoteLabelsAndLabelBackgrounds()
    {
        // Arrange
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), "classDiagram\n  class A\n");

        // Act
        var styles = ClassStyles.Create(config, null);
        var flowchart = FlowchartStyles.Create(config, null);

        // Assert
        Assert.Equal(Color(config.ThemeVariable("noteTextColor")!), styles.NoteLabel.Color);
        Assert.Equal([".labelBkg", ".edgeLabel .label span"], styles.HtmlEdgeLabelBackgrounds);
        Assert.Equal(".edgeLabel .label rect", styles.SvgEdgeLabelBackground);
        Assert.Equal("#ECECFF", styles.Resolve(styles.SvgEdgeLabelBackground)["fill"]);
        Assert.Equal(flowchart.NodeLabel, flowchart.NoteLabel);
        Assert.Equal([".labelBkg", ".edgeLabel p"], flowchart.HtmlEdgeLabelBackgrounds);
        Assert.Equal(".edgeLabel rect", flowchart.SvgEdgeLabelBackground);
    }

    /// <summary>
    ///     Validates that a <c>themeCSS</c> rule of a class selector overrides the theme, and that the important marker
    ///     fills are kept against it.
    /// </summary>
    [Fact]
    public void ClassStyles_Create_ThemeCss_OverridesKnownSelectors()
    {
        // Arrange
        const string Text = "%%{init: {\"themeCSS\": \".divider { stroke: #ff0000 } .composition { fill: blue } .edgeTerminals { font-size: 14px }\"}}%%\nclassDiagram\n  class A\n";
        var source = Mermaid.Syntax.Preprocessor.Run(Text, "t.mmd", MermaidLimits.Default);
        var sink = new MermaidReportSink(100, throwOnUnsupported: false);
        var config = MermaidConfigResolver.Resolve(source, "classDiagram", null, sink);
        var reports = new List<MermaidUnsupportedFeature>();

        // Act
        var styles = ClassStyles.Create(config, sink);
        sink.Deliver(null, reports.Add);

        // Assert
        Assert.Empty(reports);
        Assert.Equal(Color("#ff0000"), styles.Divider.Stroke);
        Assert.Equal("#333333", styles.Resolve(".composition")["fill"]);
        Assert.Equal(14, styles.TerminalLabel(true).FontSize);
    }

    /// <summary>
    ///     Validates the state diagram's style rules of the default theme: the global rules first, the node paint, the
    ///     label colors of nodes, transitions, notes and composite titles, the 18 pixel title, and the CSS corner radii and
    ///     cluster fills the state shapes read by selector.
    /// </summary>
    [Fact]
    public void StateStyles_Create_DefaultTheme_ResolvesStateRules()
    {
        // Arrange
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), "stateDiagram-v2\n  A --> B\n");
        const string Family = "\"trebuchet ms\", verdana, arial, sans-serif";
        var stateLabel = Color(config.ThemeVariable("stateLabelColor")!);
        var transitionLabel = Color((config.ThemeVariable("transitionLabelColor") ?? config.ThemeVariable("tertiaryTextColor"))!);

        // Act
        var styles = StateStyles.Create(config, null);

        // Assert
        Assert.Equal(
            FlowchartStyles.Create(config, null).Rules.Take(GlobalRuleCount).Select(rule => rule.Selector),
            styles.Rules.Take(GlobalRuleCount).Select(rule => rule.Selector));
        Assert.Equal(new ShapePaint(Color("#ECECFF"), Color("#9370DB"), 1), styles.Node);
        Assert.Equal(new TextPaint(Family, 16, stateLabel), styles.NodeLabel);
        Assert.Equal(new TextPaint(Family, 16, transitionLabel), styles.EdgeLabel);
        Assert.Equal(Color(config.ThemeVariable("noteTextColor")!), styles.NoteLabel.Color);
        Assert.Equal(new TextPaint(Family, 18, Color("#333")), styles.Title);
        Assert.Equal(stateLabel, styles.ClusterTitle(true).Color);
        Assert.Equal(Color("#333"), styles.ClusterTitle(false).Color);
        Assert.Equal(("5px", "0"), (styles.Resolve(".statediagram-state rect.basic")["rx"], styles.Resolve(".statediagram-cluster .inner")["rx"]));
        Assert.Equal(
            (Color("#ffffff"), Color("#f0f0f0")),
            (styles.Resolve(".statediagram-cluster.statediagram-cluster .inner").Paint("fill"), styles.Resolve(".statediagram-cluster.statediagram-cluster-alt .inner").Paint("fill")));
        Assert.Equal(config.ThemeVariable("transitionColor"), styles.Resolve("defs [id$=\"-barbEnd\"]")["fill"]);
        Assert.Equal("5", styles.Resolve(".note-edge")["stroke-dasharray"]);
    }

    /// <summary>
    ///     Validates that a <c>themeCSS</c> rule of a state selector overrides the theme without reports.
    /// </summary>
    [Fact]
    public void StateStyles_Create_ThemeCss_OverridesKnownSelectors()
    {
        // Arrange
        const string Text = "%%{init: {\"themeCSS\": \".statediagram-cluster rect { fill: #00ff00 } .transition { stroke: #ff0000 }\"}}%%\nstateDiagram-v2\n  A --> B\n";
        var source = Mermaid.Syntax.Preprocessor.Run(Text, "t.mmd", MermaidLimits.Default);
        var sink = new MermaidReportSink(100, throwOnUnsupported: false);
        var config = MermaidConfigResolver.Resolve(source, "stateDiagram", null, sink);
        var reports = new List<MermaidUnsupportedFeature>();

        // Act
        var styles = StateStyles.Create(config, sink);
        sink.Deliver(null, reports.Add);

        // Assert
        Assert.Empty(reports);
        Assert.Equal(Color("#00ff00"), styles.Resolve(".statediagram-cluster rect").Paint("fill"));
        Assert.Equal(Color("#ff0000"), styles.Resolve(".transition").Paint("stroke"));
    }

    /// <summary>
    ///     Validates the entity relationship diagram's style rules of the default theme: the global rules first, the node
    ///     paint, the 14 pixel edge labels and their one visible background, the title in the root font (its class has no rule),
    ///     the subgraph titles, and the important marker paint.
    /// </summary>
    [Fact]
    public void ErStyles_Create_DefaultTheme_ResolvesErRules()
    {
        // Arrange
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), "erDiagram\n  A ||--o{ B : has\n");
        const string Family = "\"trebuchet ms\", verdana, arial, sans-serif";

        // Act
        var styles = ErStyles.Create(config, null);

        // Assert
        Assert.Equal(
            FlowchartStyles.Create(config, null).Rules.Take(GlobalRuleCount).Select(rule => rule.Selector),
            styles.Rules.Take(GlobalRuleCount).Select(rule => rule.Selector));
        Assert.Equal(new ShapePaint(Color("#ECECFF"), Color("#9370DB"), 1), styles.Node);
        Assert.Equal(16, styles.NodeLabel.FontSize);
        Assert.Equal(new TextPaint(Family, 14, styles.NodeLabel.Color), styles.EdgeLabel);
        Assert.Equal([".labelBkg"], styles.HtmlEdgeLabelBackgrounds);
        Assert.Equal(new TextPaint(Family, 16, Color("#333")), styles.Title);
        Assert.Equal(new Rgba32(0, 0, 0, 255), styles.ClusterTitle(true).Color);
        Assert.Equal(Color(config.ThemeVariable("titleColor")!), styles.ClusterTitle(false).Color);
        var marker = styles.Resolve(".marker");
        Assert.Equal(("none", true, "#333333", true), (marker["fill"], marker.IsImportant("fill"), marker["stroke"], marker.IsImportant("stroke")));
        Assert.Equal("8,8", styles.Resolve(".edge-pattern-dashed")["stroke-dasharray"]);
    }

    /// <summary>
    ///     Validates that a <c>themeCSS</c> rule cannot replace the important marker fill of an entity relationship
    ///     diagram, while it overrides an ordinary rule.
    /// </summary>
    [Fact]
    public void ErStyles_Create_ThemeCss_KeepsTheImportantMarkerFill()
    {
        // Arrange
        const string Text = "%%{init: {\"themeCSS\": \".marker { fill: #ff0000 } .relationshipLine { stroke: #0000ff }\"}}%%\nerDiagram\n  A ||--o{ B : has\n";
        var source = Mermaid.Syntax.Preprocessor.Run(Text, "t.mmd", MermaidLimits.Default);
        var sink = new MermaidReportSink(100, throwOnUnsupported: false);
        var config = MermaidConfigResolver.Resolve(source, "er", null, sink);
        var reports = new List<MermaidUnsupportedFeature>();

        // Act
        var styles = ErStyles.Create(config, sink);
        sink.Deliver(null, reports.Add);

        // Assert
        Assert.Empty(reports);
        Assert.Equal("none", styles.Resolve(".marker")["fill"]);
        Assert.Equal(Color("#0000ff"), styles.Resolve(".relationshipLine").Paint("stroke"));
    }

    /// <summary>
    ///     Validates the pie chart's style rules of the default theme: the global rules first, the slice paint over its
    ///     color with the opacity of 0.7 folded in, the highlighted slice opaque and 1.05 times larger, the outer circle
    ///     without fill, and the fonts and colors of the title, the percentage labels and the legend.
    /// </summary>
    [Fact]
    public void PieStyles_Create_DefaultTheme_ResolvesPieRules()
    {
        // Arrange
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), "pie\n  \"a\" : 1\n");
        const string Family = "\"trebuchet ms\", verdana, arial, sans-serif";
        var color = Color("#ECECFF");

        // Act
        var styles = PieStyles.Create(config, null);

        // Assert
        Assert.Equal(
            FlowchartStyles.Create(config, null).Rules.Take(GlobalRuleCount).Select(rule => rule.Selector),
            styles.Rules.Take(GlobalRuleCount).Select(rule => rule.Selector));
        Assert.Equal(new SlicePaint(color with { A = 179 }, new Rgba32(0, 0, 0, 179), 2, 1), styles.Slice(color, highlighted: false));
        Assert.Equal(new SlicePaint(color, new Rgba32(0, 0, 0, 255), 2, 1.05), styles.Slice(color, highlighted: true));
        Assert.Equal(new ShapePaint(null, new Rgba32(0, 0, 0, 255), 2), styles.OuterCircle);
        Assert.Equal(new TextPaint(Family, 25, new Rgba32(0, 0, 0, 255)), styles.TitleText);
        Assert.Equal(new TextPaint(Family, 17, Color("#333")), styles.SliceText);
        Assert.Equal(new TextPaint(Family, 17, new Rgba32(0, 0, 0, 255)), styles.LegendText);
    }

    /// <summary>
    ///     Validates that a <c>themeCSS</c> rule for a pie selector overrides the theme's: a slice fill beats the slice's
    ///     own color, and a legend text color and size apply.
    /// </summary>
    [Fact]
    public void PieStyles_Create_ThemeCss_OverridesKnownSelectors()
    {
        // Arrange
        const string Text = "%%{init: {\"themeCSS\": \".pieCircle { fill: #ff0000; opacity: 1 } .legend text { fill: #0000ff; font-size: 20px }\"}}%%\npie\n\"a\" : 1\n";
        var source = Mermaid.Syntax.Preprocessor.Run(Text, "t.mmd", MermaidLimits.Default);
        var sink = new MermaidReportSink(100, throwOnUnsupported: false);
        var config = MermaidConfigResolver.Resolve(source, "pie", null, sink);
        var reports = new List<MermaidUnsupportedFeature>();

        // Act
        var styles = PieStyles.Create(config, sink);
        sink.Deliver(null, reports.Add);

        // Assert
        Assert.Empty(reports);
        Assert.Equal(Color("#ff0000"), styles.Slice(Color("#ECECFF"), highlighted: false).Fill);
        Assert.Equal((20.0, Color("#0000ff")), (styles.LegendText.FontSize, styles.LegendText.Color));
    }

    /// <summary>
    ///     Validates the packet diagram's style rules of each theme: the global rules first; the default, base and neutral
    ///     themes have no <c>packet</c> object (black texts and strokes on <c>#efefef</c>), the dark and forest themes set
    ///     theirs; the font sizes and the stroke width always come from the sheet's defaults.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <param name="text">The color of the texts and strokes.</param>
    /// <param name="fill">The fill of the blocks.</param>
    [Theory]
    [InlineData("default", "#000000", "#efefef")]
    [InlineData("base", "#000000", "#efefef")]
    [InlineData("neutral", "#000000", "#efefef")]
    [InlineData("dark", "#e0dfdf", "#333333")]
    [InlineData("forest", "#321b67", "#cde498")]
    public void PacketStyles_Create_EachTheme_UsesThemePacketObject(string theme, string text, string fill)
    {
        // Arrange
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject { ["theme"] = theme }, "packet\n0: \"a\"\n");
        const string Family = "\"trebuchet ms\", verdana, arial, sans-serif";

        // Act
        var styles = PacketStyles.Create(config, null);

        // Assert
        Assert.Equal(
            FlowchartStyles.Create(config, null).Rules.Take(GlobalRuleCount).Select(rule => rule.Selector),
            styles.Rules.Take(GlobalRuleCount).Select(rule => rule.Selector));
        Assert.Equal(new ShapePaint(Color(fill), Color(text), 1), styles.Block);
        Assert.Equal(new TextPaint(Family, 12, Color(text)), styles.Label);
        Assert.Equal(new TextPaint(Family, 14, Color(text)), styles.Title);
        Assert.Equal(new TextPaint(Family, 10, Color(text)), styles.ByteStart);
        Assert.Equal(new TextPaint(Family, 10, Color(text)), styles.ByteEnd);
    }

    /// <summary>
    ///     Validates that a user's <c>themeVariables.packet</c> replaces the theme's object: the keys it gives apply, the
    ///     keys it leaves out take the sheet's defaults (black bytes and strokes on <c>#efefef</c> in the dark theme), and
    ///     a unitless font size is dropped as a browser drops it, so the label takes the root's 16 pixels.
    /// </summary>
    [Fact]
    public void PacketStyles_Create_PacketOverride_ReplacesThemeObject()
    {
        // Arrange
        var site = new JsonObject
        {
            ["theme"] = "dark",
            ["themeVariables"] = new JsonObject
            {
                ["packet"] = new JsonObject { ["labelColor"] = "red", ["titleFontSize"] = "20px", ["labelFontSize"] = 20, ["blockStrokeWidth"] = 3 },
            },
        };
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(site, "packet\n0: \"a\"\n");

        // Act
        var styles = PacketStyles.Create(config, null);

        // Assert
        Assert.Equal((16.0, Color("#ff0000")), (styles.Label.FontSize, styles.Label.Color));
        Assert.Equal((20.0, Color("#000000")), (styles.Title.FontSize, styles.Title.Color));
        Assert.Equal(Color("#000000"), styles.ByteStart.Color);
        Assert.Equal(new ShapePaint(Color("#efefef"), Color("#000000"), 3), styles.Block);
    }

    /// <summary>
    ///     Validates the options of the packet sheet: the defaults, a replacing object whose values are written as
    ///     JavaScript's <c>String</c> writes them, and a theme variable that is not an object, which changes nothing.
    /// </summary>
    [Fact]
    public void PacketStyles_Options_ValuesAsJavaScriptStrings_MergedOverDefaults()
    {
        // Arrange
        var packet = new JsonObject
        {
            ["labelColor"] = 12.5,
            ["titleColor"] = null,
            ["endByteColor"] = true,
            ["blockFillColor"] = new JsonObject { ["a"] = 1 },
            ["blockStrokeColor"] = new JsonArray("a", null, 2),
        };

        // Act
        var defaults = PacketStyles.Options(null);
        var merged = PacketStyles.Options(packet);
        var text = PacketStyles.Options(JsonValue.Create("red"));

        // Assert
        Assert.Equal(10, defaults.Count);
        Assert.Equal(("10px", "#efefef", "1"), (defaults["byteFontSize"], defaults["blockFillColor"], defaults["blockStrokeWidth"]));
        Assert.Equal(("12.5", "null", "true"), (merged["labelColor"], merged["titleColor"], merged["endByteColor"]));
        Assert.Equal(("[object Object]", "a,,2", "black"), (merged["blockFillColor"], merged["blockStrokeColor"], merged["startByteColor"]));
        Assert.Equal(defaults, text);
    }

    /// <summary>
    ///     Validates that a <c>themeCSS</c> rule for a packet selector overrides the theme's: a block's fill, stroke and
    ///     width, and the color of the last bit numbers only (<c>.packetByte.end</c>), without a report.
    /// </summary>
    [Fact]
    public void PacketStyles_Create_ThemeCss_OverridesKnownSelectors()
    {
        // Arrange
        const string Text = "%%{init: {\"themeCSS\": \".packetBlock { fill: #ffe0b2; stroke: #e65100; stroke-width: 2px } .packetByte.end { fill: blue }\"}}%%\npacket\n0: \"a\"\n";
        var source = Mermaid.Syntax.Preprocessor.Run(Text, "t.mmd", MermaidLimits.Default);
        var sink = new MermaidReportSink(100, throwOnUnsupported: false);
        var config = MermaidConfigResolver.Resolve(source, "packet", null, sink);
        var reports = new List<MermaidUnsupportedFeature>();

        // Act
        var styles = PacketStyles.Create(config, sink);
        sink.Deliver(null, reports.Add);

        // Assert
        Assert.Empty(reports);
        Assert.Equal(new ShapePaint(Color("#ffe0b2"), Color("#e65100"), 2), styles.Block);
        Assert.Equal((Color("#0000ff"), Color("#000000")), (styles.ByteEnd.Color, styles.ByteStart.Color));
    }

    /// <summary>
    ///     Validates that a block whose rule has no valid fill inherits the root rule's fill (the theme's text color), as
    ///     SVG paint is inherited, and that an invalid stroke falls back to none.
    /// </summary>
    [Fact]
    public void PacketStyles_Create_InvalidBlockPaint_InheritsRootFill()
    {
        // Arrange
        var site = new JsonObject
        {
            ["themeVariables"] = new JsonObject { ["packet"] = new JsonObject { ["blockFillColor"] = "not a color", ["blockStrokeColor"] = "nope" } },
        };
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(site, "packet\n0: \"a\"\n");

        // Act
        var styles = PacketStyles.Create(config, null);

        // Assert
        Assert.Equal(new ShapePaint(Color("#333"), null, 1), styles.Block);
    }

    /// <summary>
    ///     Validates the git graph's style rules of each theme: the global rules first, five rules for each of the twelve
    ///     colors of <c>THEME_COLOR_LIMIT</c>, and the paint of each of the eight named colors (a commit's bullet, a
    ///     highlight's outer square, a branch label's rectangle and text, an arrow), which repeats for the colors past the
    ///     eighth.
    /// </summary>
    /// <param name="theme">The theme.</param>
    [Theory]
    [InlineData("default")]
    [InlineData("base")]
    [InlineData("dark")]
    [InlineData("forest")]
    [InlineData("neutral")]
    public void GitGraphStyles_Create_EachTheme_PaintsTheNamedGitColors(string theme)
    {
        // Arrange
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject { ["theme"] = theme }, GitGraphText);
        Rgba32 Var(string name) => Color(config.ThemeVariable(name)!);

        // Act
        var styles = GitGraphStyles.Create(config, null);

        // Assert
        Assert.Equal(
            FlowchartStyles.Create(config, null).Rules.Take(GlobalRuleCount).Select(rule => rule.Selector),
            styles.Rules.Take(GlobalRuleCount).Select(rule => rule.Selector));
        Assert.Equal(12, styles.Rules.Count(rule => rule.Selector.StartsWith(".commit-highlight", StringComparison.Ordinal) && char.IsAsciiDigit(rule.Selector[^1])));
        for (var i = 0; i < 12; i++)
        {
            var c = (i % 8).ToString(CultureInfo.InvariantCulture);
            var n = i.ToString(CultureInfo.InvariantCulture);
            var bullet = styles.Compute([Svg, GitGraphElement.Of("g", "commit-bullets"), GitGraphElement.Of("circle", $"commit 0-eb28011 commit{n}")]);
            var outer = styles.Compute([Svg, GitGraphElement.Of("g", "commit-bullets"), GitGraphElement.Of("rect", $"commit A commit-highlight{n} commit-highlight-outer")]);
            var rect = styles.Compute([Svg, GitGraphElement.Of("g", string.Empty), GitGraphElement.Of("rect", $"branchLabelBkg label{n}")]);
            var label = styles.Compute([Svg, GitGraphElement.Of("g", string.Empty), GitGraphElement.Of("g", "branchLabel"), GitGraphElement.Of("g", $"label branch-label{n}"), GitGraphElement.Of("text", string.Empty)]);
            var arrow = styles.Compute([Svg, GitGraphElement.Of("g", "commit-arrows"), GitGraphElement.Of("path", $"arrow arrow{n}")]);
            Assert.Equal((Var("git" + c), Var("git" + c)), (bullet.Fill, bullet.Stroke));
            Assert.Equal((Var("gitInv" + c), Var("gitInv" + c)), (outer.Fill, outer.Stroke));
            Assert.Equal(Var("git" + c), rect.Fill);
            Assert.Equal(Var("gitBranchLabel" + c), label.Fill);
            Assert.Equal((Var("git" + c), (Rgba32?)null, 8.0, "round"), (arrow.Stroke, arrow.Fill, arrow.StrokeWidth, arrow.LineCap));
        }
    }

    /// <summary>
    ///     Validates the cascade against the browser: every branch line, branch label, arrow, bullet, commit label, tag
    ///     and title of each git graph golden gets, from the style sheet of its fixture's configuration (its theme, theme
    ///     variables, <c>themeCSS</c> and look), the fill, stroke, stroke width, dashes, line cap, opacity, shadow, font
    ///     size and text anchor that Chrome computed for it.
    /// </summary>
    /// <param name="golden">The golden file name.</param>
    [Theory]
    [MemberData(nameof(GitGraphGoldens))]
    public void GitGraphStyles_Compute_FixtureElements_MatchTheBrowsersComputedStyle(string golden)
    {
        // Arrange
        var reference = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference", "gitgraph", golden)))!;
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gitgraph", (string)reference["source"]!));
        var sink = new MermaidReportSink(100, throwOnUnsupported: false);
        var config = MermaidConfigResolver.Resolve(Mermaid.Syntax.Preprocessor.Run(text, "t.mmd", MermaidLimits.Default), "gitGraph", null, sink);
        var checks = 0;

        // Act
        var styles = GitGraphStyles.Create(config, sink);

        // Assert
        void Shape(JsonNode shape, params GitGraphElement[] parents)
        {
            var attributes = new List<(string, string)>();
            foreach (var name in (string[])["fill", "stroke"])
            {
                if (shape[name] is { } value)
                {
                    attributes.Add((name, value.GetValue<string>()));
                }
            }

            var styleText = shape["style"]?.GetValue<string>() ?? string.Empty;
            var inline = styleText.Length > 0 ? styles.BranchLabelStyle : null;
            Assert.Equal(styleText.Length > 0, inline is not null);
            var element = GitGraphElement.Of((string)shape["tag"]!, (string?)shape["class"] ?? string.Empty, [.. attributes]) with { Inline = inline };
            var computed = styles.Compute([Svg, .. parents, element]);
            var paint = shape["paint"]!;
            AssertChromeColor(paint["fill"], computed.Fill);
            AssertChromeColor(paint["stroke"], computed.Stroke);
            Assert.Equal((double)paint["strokeWidth"]!, computed.StrokeWidth, 3);
            Assert.Equal(ChromeDash(paint["dasharray"]), computed.Dash);
            Assert.Equal((string)paint["lineCap"]!, computed.LineCap);
            Assert.Equal((double)paint["opacity"]!, computed.Opacity, 3);
            Assert.Equal(ChromeShadow(paint["filter"]), computed.Shadow);
            checks++;
        }

        void Text(JsonNode label, params GitGraphElement[] parents)
        {
            var computed = styles.Compute([Svg, .. parents, GitGraphElement.Of("text", (string?)label["class"] ?? string.Empty)]);
            AssertChromeColor(label["fill"], computed.Fill);
            Assert.Equal((double)label["fontSize"]!, computed.FontSize, 3);
            Assert.Equal((string)label["anchor"]!, computed.TextAnchor);
            Assert.Equal((double)label["opacity"]!, computed.Opacity, 3);
            checks++;
        }

        var group = GitGraphElement.Of("g", string.Empty);
        foreach (var branch in reference["branches"]!.AsArray())
        {
            Shape(branch!["line"]!, group);
            Shape(branch["rect"]!, group);
            Text(branch["label"]!["text"]!, group, GitGraphElement.Of("g", "branchLabel"), GitGraphElement.Of("g", (string)branch["label"]!["class"]!));
        }

        foreach (var arrow in reference["arrows"]!.AsArray())
        {
            var path = new JsonObject { ["tag"] = "path", ["class"] = (string)arrow!["class"]!, ["paint"] = arrow["paint"]!.DeepClone() };
            Shape(path, GitGraphElement.Of("g", "commit-arrows"));
        }

        foreach (var bullet in reference["bullets"]!.AsArray())
        {
            Shape(bullet!, GitGraphElement.Of("g", "commit-bullets"));
        }

        var labels = GitGraphElement.Of("g", "commit-labels");
        foreach (var label in reference["labels"]!.AsArray())
        {
            if ((string)label!["kind"]! == "commit")
            {
                Shape(label["rect"]!, labels, group);
                Text(label["text"]!, labels, group);
            }
            else
            {
                Shape(label["polygon"]!, labels);
                Shape(label["hole"]!, labels);
                Text(label["text"]!, labels);
            }
        }

        if (reference["title"] is { } title)
        {
            Text(title);
        }

        Assert.True(checks > 0);
    }

    /// <summary>
    ///     Validates that the commit id in a bullet's class attribute is classes of its own, split on ASCII white space as a
    ///     browser splits them: an id that is also a class of the sheet (<c>commit-reverse</c>, <c>arrow</c>) is styled by
    ///     its rule, and a white fill attribute of a cherry-pick's mark loses to no rule.
    /// </summary>
    [Fact]
    public void GitGraphStyles_Compute_CommitIdClasses_AreStyledByTheirRules()
    {
        // Arrange
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), GitGraphText);
        var styles = GitGraphStyles.Create(config, null);
        var bullets = GitGraphElement.Of("g", "commit-bullets");

        // Act
        var plain = styles.Compute([Svg, bullets, GitGraphElement.Of("circle", "commit A commit0")]);
        var reverse = styles.Compute([Svg, bullets, GitGraphElement.Of("circle", "commit x\tcommit-reverse commit0")]);
        var arrow = styles.Compute([Svg, bullets, GitGraphElement.Of("circle", "commit arrow commit0")]);
        var mark = styles.Compute([Svg, bullets, GitGraphElement.Of("circle", "commit A commit-cherry-pick", ("fill", "#fff"))]);

        // Assert
        Assert.Equal(1.0, plain.StrokeWidth);
        Assert.Equal((Color(config.ThemeVariable("primaryColor")!), 3.0), (reverse.Fill!.Value, reverse.StrokeWidth));
        Assert.Equal((Color(config.ThemeVariable("git0")!), 8.0, "round"), (arrow.Stroke!.Value, arrow.StrokeWidth, arrow.LineCap));
        Assert.Equal(Color("#fff"), mark.Fill);
    }

    /// <summary>
    ///     Validates the number of colors of the sheet: JavaScript's array length of <c>THEME_COLOR_LIMIT</c> (its whole
    ///     part, zero for a value that is not a positive number), capped; with fewer than eight colors, a commit of a later
    ///     branch has no color rule and inherits the root's fill and no stroke.
    /// </summary>
    [Fact]
    public void GitGraphStyles_ColorCount_FollowsTheArrayLengthOfTheLimit()
    {
        // Arrange
        var site = new JsonObject { ["themeVariables"] = new JsonObject { ["THEME_COLOR_LIMIT"] = 4 } };
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(site, GitGraphText);

        // Act
        var counts = new[] { null, "12", "4.9", "0.5", "-3", "many", "1000" }.Select(GitGraphStyles.ColorCount).ToArray();
        var styles = GitGraphStyles.Create(config, null);
        var colored = styles.Compute([Svg, GitGraphElement.Of("g", "commit-bullets"), GitGraphElement.Of("circle", "commit A commit3")]);
        var uncolored = styles.Compute([Svg, GitGraphElement.Of("g", "commit-bullets"), GitGraphElement.Of("circle", "commit A commit5")]);

        // Assert
        Assert.Equal([12, 12, 4, 0, 0, 0, ThemeResolver.MaxColorLimit], counts);
        Assert.Equal(Color(config.ThemeVariable("git3")!), colored.Fill);
        Assert.Equal((Color("#333"), (Rgba32?)null), (uncolored.Fill!.Value, uncolored.Stroke));
    }

    /// <summary>
    ///     Validates the <c>neo</c> look's shadow: the inline style of a branch label's rectangle is the theme's
    ///     <c>dropShadow</c> filter, which the cascade turns into a shadow of that element only; the classic look has no
    ///     inline style.
    /// </summary>
    [Fact]
    public void GitGraphStyles_Create_NeoLook_ShadowsTheBranchLabels()
    {
        // Arrange
        var neo = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject { ["look"] = "neo" }, GitGraphText);
        var classic = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), GitGraphText);

        // Act
        var styles = GitGraphStyles.Create(neo, null);
        var rect = styles.Compute([Svg, GitGraphElement.Of("g", string.Empty), GitGraphElement.Of("rect", "branchLabelBkg label0") with { Inline = styles.BranchLabelStyle }]);
        var group = styles.Compute([Svg, GitGraphElement.Of("g", string.Empty) with { Inline = styles.BranchLabelStyle }, GitGraphElement.Of("rect", "branchLabelBkg label0")]);

        // Assert
        Assert.Equal(new ShadowPaint(1, 2, 2, Color("rgba(185, 185, 185, 1)")), rect.Shadow);
        Assert.Null(group.Shadow);
        Assert.Null(GitGraphStyles.Create(classic, null).BranchLabelStyle);
    }

    /// <summary>
    ///     Validates the theme variables and <c>themeCSS</c> of a git graph: a unitless commit label font size is dropped
    ///     as a browser drops it (the label takes the root's size), a <c>themeCSS</c> rule of a known selector overrides
    ///     the theme's, and a rule of an unknown selector is reported.
    /// </summary>
    [Fact]
    public void GitGraphStyles_Create_ThemeVariablesAndThemeCss_AreAppliedAsTheBrowserDoes()
    {
        // Arrange
        const string Text = "---\nconfig:\n  themeVariables:\n    commitLabelFontSize: 12\n    tagLabelFontSize: 13px\n" +
                            "  themeCSS: \".tag-label { fill: #0000ff; } .arrow1 { stroke-width: 3px; } .bullet { fill: red; }\"\n---\n" + GitGraphText;
        var sink = new MermaidReportSink(100, throwOnUnsupported: false);
        var config = MermaidConfigResolver.Resolve(Mermaid.Syntax.Preprocessor.Run(Text, "t.mmd", MermaidLimits.Default), "gitGraph", null, sink);
        var reports = new List<MermaidUnsupportedFeature>();

        // Act
        var styles = GitGraphStyles.Create(config, sink);
        sink.Deliver(null, reports.Add);
        var label = styles.Compute([Svg, GitGraphElement.Of("g", "commit-labels"), GitGraphElement.Of("g", string.Empty), GitGraphElement.Of("text", "commit-label")]);
        var tag = styles.Compute([Svg, GitGraphElement.Of("g", "commit-labels"), GitGraphElement.Of("text", "tag-label")]);
        var arrow = styles.Compute([Svg, GitGraphElement.Of("g", "commit-arrows"), GitGraphElement.Of("path", "arrow arrow1")]);

        // Assert
        Assert.Equal(16.0, label.FontSize);
        Assert.Equal((13.0, Color("#0000ff")), (tag.FontSize, tag.Fill!.Value));
        Assert.Equal(3.0, arrow.StrokeWidth);
        Assert.Single(reports);
    }

    /// <summary>
    ///     Validates the gantt chart's cascade of the default theme: the global rules first; a bar's class rules over the
    ///     section style; an <c>!important</c> clickable text color over a later normal rule; a compound selector
    ///     (<c>.doneText0.taskTextOutsideRight</c>) over its single classes; the descendant rule of the axis labels over
    ///     their <c>fill</c> attribute; <c>currentColor</c> and the tick group's opacity for the grid lines; the section rows'
    ///     opacity; and the milestone's transform.
    /// </summary>
    [Fact]
    public void GanttStyles_Create_DefaultTheme_CascadesClassSets()
    {
        // Arrange
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), "gantt\nA :1d\n");
        var svg = new GanttElement("svg", []);
        var group = new GanttElement("g", []);
        var grid = GanttElement.Of("g", "grid", ("fill", "none"), ("font-size", "10"));
        var tick = GanttElement.Of("g", "tick", ("opacity", "1"));

        // Act
        var styles = GanttStyles.Create(config, null);
        var bar = styles.Compute([svg, group, GanttElement.Of("rect", "task crit1 ")]);
        var clickable = styles.Compute([svg, group, GanttElement.Of("text", "clickable taskText taskText0  activeText0", ("font-size", "11"))]);
        var doneOutside = styles.Compute([svg, group, GanttElement.Of("text", " taskTextOutsideRight taskTextOutside0  doneText0")]);
        var label = styles.Compute([svg, grid, tick, GanttElement.Of("text", string.Empty, ("fill", "#000"), ("font-size", "10"))]);
        var line = styles.Compute([svg, grid, tick, GanttElement.Of("line", string.Empty, ("stroke", "currentColor"))]);
        var row = styles.Compute([svg, group, GanttElement.Of("rect", "section section0")]);
        var milestone = styles.Compute([svg, group, GanttElement.Of("rect", "task milestone  task0 ")]);

        // Assert
        Assert.Equal(
            FlowchartStyles.Create(config, null).Rules.Take(GlobalRuleCount).Select(rule => rule.Selector),
            styles.Rules.Take(GlobalRuleCount).Select(rule => rule.Selector));
        Assert.Equal((Color("red"), Color("#ff8888"), 2.0), (bar.Fill, bar.Stroke, bar.StrokeWidth));
        Assert.Equal((Color("#003163"), true, 11.0, "middle"), (clickable.Fill, clickable.Bold, clickable.FontSize, clickable.TextAnchor));
        Assert.Equal((Color("black"), "start"), (doneOutside.Fill, doneOutside.TextAnchor));
        Assert.Equal((Color("#333"), 10.0, 0.8), (label.Fill, label.FontSize, label.Opacity));
        Assert.Equal((new Rgba32(0, 0, 0, 255), 0.8), (line.Stroke, line.Opacity));
        Assert.Equal(0.2, row.Opacity, 6);
        Assert.Equal("rotate(45deg) scale(0.8,0.8)", milestone.Transform);
    }

    /// <summary>
    ///     Validates that a <c>themeCSS</c> rule for a gantt selector overrides the theme's: a bar fill and a section title
    ///     color apply, and an unknown selector is reported.
    /// </summary>
    [Fact]
    public void GanttStyles_Create_ThemeCss_OverridesKnownSelectors()
    {
        // Arrange
        const string Text = "%%{init: {\"themeCSS\": \".task0 { fill: #ff0000 } .sectionTitle0 { fill: #0000ff } .bogus { fill: red }\"}}%%\ngantt\nA :1d\n";
        var source = Mermaid.Syntax.Preprocessor.Run(Text, "t.mmd", MermaidLimits.Default);
        var sink = new MermaidReportSink(100, throwOnUnsupported: false);
        var config = MermaidConfigResolver.Resolve(source, "gantt", null, sink);
        var reports = new List<MermaidUnsupportedFeature>();

        // Act
        var styles = GanttStyles.Create(config, sink);
        sink.Deliver(null, reports.Add);
        var bar = styles.Compute([new GanttElement("svg", []), new GanttElement("g", []), GanttElement.Of("rect", "task task0")]);
        var title = styles.Compute([new GanttElement("svg", []), new GanttElement("g", []), GanttElement.Of("text", "sectionTitle sectionTitle0")]);

        // Assert
        Assert.Single(reports);
        Assert.Equal(Color("#ff0000"), bar.Fill);
        Assert.Equal(Color("#0000ff"), title.Fill);
    }
    /// <summary>
    ///     Validates the facts every unified sheet gives the rendering: only the flowchart and class sheets center the
    ///     SVG text of a node's label on its anchor (the state and entity relationship sheets have no such rule), and the
    ///     root text is the diagram's root font and its text color.
    /// </summary>
    [Fact]
    public void UnifiedStyles_Create_EachSheet_CentersSvgNodeTextAndGivesTheRootText()
    {
        // Arrange
        var flowchart = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), "flowchart TD\n  A\n");
        var classes = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), "classDiagram\n  class A\n");
        var state = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), "stateDiagram-v2\n  A --> B\n");
        var er = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), "erDiagram\n  A ||--o{ B : has\n");
        const string Family = "\"trebuchet ms\", verdana, arial, sans-serif";

        // Act
        var sheets = new UnifiedStyles[]
        {
            FlowchartStyles.Create(flowchart, null), ClassStyles.Create(classes, null), StateStyles.Create(state, null), ErStyles.Create(er, null),
        };

        // Assert
        Assert.Equal([true, true, false, false], sheets.Select(sheet => sheet.CentersNodeSvgText));
        Assert.Equal(new TextPaint(Family, 16, Color(er.ThemeVariable("textColor")!)), sheets[3].RootText);
        Assert.All(sheets, sheet => Assert.Equal(16, sheet.RootText.FontSize));
    }

    /// <summary>
    ///     Validates that the flowchart and sequence style sheets start with the same global rules of Mermaid's
    ///     <c>getStyles</c>.
    /// </summary>
    [Fact]
    public void DiagramStyles_Create_GlobalRules_SharedByFlowchartAndSequence()
    {
        // Arrange
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), SequenceText);

        // Act
        var flowchart = FlowchartStyles.Create(config, null).Rules.Take(GlobalRuleCount).ToList();
        var sequence = SequenceStyles.Create(config, null).Rules.Take(GlobalRuleCount).ToList();

        // Assert
        Assert.Equal(DiagramStyles.Root, flowchart[0].Selector);
        Assert.Equal("p", flowchart[^1].Selector);
        Assert.Equal(flowchart.Select(rule => rule.Selector), sequence.Select(rule => rule.Selector));
        Assert.Equal(
            flowchart.Select(rule => string.Join(";", rule.Declarations.Declarations)),
            sequence.Select(rule => string.Join(";", rule.Declarations.Declarations)));
    }

    /// <summary>
    ///     Validates the sequence style rules of the default theme and the cascade facts they make: the <c>line</c> rule
    ///     widens lifelines and block lines to 2 pixels, a message rule beats it, CSS beats the attributes, and a box gets
    ///     the theme's drop shadow and node border.
    /// </summary>
    [Fact]
    public void SequenceStyles_Create_DefaultTheme_ResolvesCascade()
    {
        // Arrange
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), SequenceText);

        // Act
        var styles = SequenceStyles.Create(config, null);

        // Assert: actors and notes take the theme's paint over their attributes
        Assert.Equal(new ShapePaint(Color("#ECECFF"), Color("#9370DB"), 1), styles.Shape(new ShapePaint(Color("#eaeaea"), Color("#666"), 1), ".actor"));
        Assert.Equal(new ShapePaint(Color("#fff5ad"), Color("#aaaa33"), 1), styles.Shape(new ShapePaint(Color("#EDF2AE"), Color("#666"), 1), ".note"));
        Assert.Equal(new ShapePaint(Color("#EDF2AE"), Color("#666"), 1), styles.Shape(new ShapePaint(Color("#EDF2AE"), Color("#666"), 1), ".custom"));

        // Lines: the line rule's 2 pixels, the message rules' 1.5 and their dashes
        var lifeline = styles.Lifeline;
        Assert.Equal((Color("#9370DB"), 2.0), (lifeline.Color, lifeline.Width));
        Assert.Empty(lifeline.Dash);
        Assert.Equal((Color("#9370DB"), 2.0), (styles.LoopLine.Color, styles.LoopLine.Width));
        Assert.Equal([2.0, 2.0], styles.LoopLine.Dash);
        var solid = styles.MessageLine(dotted: false, isLineElement: true);
        var dotted = styles.MessageLine(dotted: true, isLineElement: false);
        Assert.Equal((Color("#333"), 1.5, 0), (solid.Color, solid.Width, solid.Dash.Count));
        Assert.Equal((Color("#333"), 1.5), (dotted.Color, dotted.Width));
        Assert.Equal([2.0, 2.0], dotted.Dash);

        // Boxes: the drop shadow and the node border; markers and texts
        Assert.Equal(new ShadowPaint(1, 2, 2, Color("rgb(185, 185, 185)")), styles.BoxShadow);
        Assert.Equal(new ShapePaint(Color("blue"), Color("#9370DB"), 1), styles.Box(new ShapePaint(Color("blue"), Color("black"), 1)));
        Assert.Equal(new ShadowPaint(3, 5, 2, Color("rgb(0 0 0 / 0.4)")), styles.MenuPanelShadow);
        Assert.Null(styles.Shadow(".labelBox"));
        Assert.Equal((Color("#333"), Color("#333"), Color("#333")), (styles.ArrowheadColor, styles.CrossheadColor, styles.SequenceNumberMarkerColor));
        Assert.Equal(Color("white"), styles.TextColor(null, ".sequenceNumber"));
        Assert.Equal(Color("#333"), styles.TextColor(Color("red"), ".messageText"));
        Assert.Equal(Color("red"), styles.TextColor(Color("red"), ".unknown"));
        Assert.Equal(Color("#333"), styles.TextColor(null, ".unknown"));
        Assert.Equal(new ShapePaint(Color("#ECECFF"), Color("#9370DB"), 1), styles.LabelBox);
        Assert.Equal("normal", styles.Resolve(".noteText")["font-weight"]);
    }

    /// <summary>
    ///     Validates for every theme that the sequence styles take the theme's variables: actor paint, lifeline color, note
    ///     paint, activation paint and the box shadow.
    /// </summary>
    /// <param name="theme">The theme.</param>
    [Theory]
    [InlineData("default")]
    [InlineData("base")]
    [InlineData("dark")]
    [InlineData("forest")]
    [InlineData("neutral")]
    public void SequenceStyles_Create_EachTheme_UsesThemeVariables(string theme)
    {
        // Arrange
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject { ["theme"] = theme }, SequenceText);
        Rgba32? Variable(string name) => CssColor.TryParse(config.ThemeVariable(name), out var color) ? color : null;
        var attributes = new ShapePaint(Color("#eaeaea"), Color("#666"), 1);

        // Act
        var styles = SequenceStyles.Create(config, null);

        // Assert
        Assert.Equal(new ShapePaint(Variable("actorBkg"), Variable("actorBorder"), 1), styles.Shape(attributes, ".actor"));
        Assert.Equal(Variable("actorLineColor"), styles.Lifeline.Color);
        Assert.Equal(new ShapePaint(Variable("noteBkgColor"), Variable("noteBorderColor"), 1), styles.Shape(attributes, ".note"));
        Assert.Equal(new ShapePaint(Variable("activationBkgColor"), Variable("activationBorderColor"), 1), styles.Shape(attributes, ".activation1"));
        Assert.True(SequenceStyles.TryParseFilter(config.ThemeVariable("dropShadow")!, out var expected));
        Assert.NotNull(expected);
        Assert.Equal(expected, styles.BoxShadow);
    }

    /// <summary>
    ///     Validates that a <c>themeCSS</c> rule of a sequence selector overrides the theme and that a selector of
    ///     another diagram is reported and not applied.
    /// </summary>
    [Fact]
    public void SequenceStyles_Create_ThemeCss_OverridesKnownSelectors()
    {
        // Arrange
        const string Text = "%%{init: {\"themeCSS\": \".actor { fill: #ff0000 } .actor-line { stroke-width: 3px } .node rect { fill: red }\"}}%%\nsequenceDiagram\n  A->>B: hi\n";
        var source = Mermaid.Syntax.Preprocessor.Run(Text, "t.mmd", MermaidLimits.Default);
        var sink = new MermaidReportSink(100, throwOnUnsupported: false);
        var config = MermaidConfigResolver.Resolve(source, "sequence", null, sink);
        var reports = new List<MermaidUnsupportedFeature>();

        // Act
        var styles = SequenceStyles.Create(config, sink);
        sink.Deliver(null, reports.Add);

        // Assert
        Assert.Equal(Color("#ff0000"), styles.Shape(new ShapePaint(null, null, 1), ".actor").Fill);
        Assert.Equal(3, styles.Lifeline.Width);
        var report = Assert.Single(reports);
        Assert.Equal(MermaidDiagnosticCodes.ThemeCssUnsupported, report.Feature);
        Assert.Contains(".node rect", report.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the parsing of the CSS filters that are drawn: <c>none</c> and one <c>drop-shadow()</c>.
    /// </summary>
    /// <param name="value">The filter.</param>
    /// <param name="expected">The shadow as "dx dy blur #rrggbbaa", "none", or null when the filter is not drawn.</param>
    [Theory]
    [InlineData("none", "none")]
    [InlineData("drop-shadow( 1px 2px 2px rgba(185,185,185,0.5))", "1 2 2 #B9B9B980")]
    [InlineData("drop-shadow(red 3px -4px)", "3 -4 0 #FF0000FF")]
    [InlineData("drop-shadow(0 0 1px)", "0 0 1 #000000FF")]
    [InlineData("drop-shadow(1px 2px 3px 4px)", null)]
    [InlineData("drop-shadow(1px)", null)]
    [InlineData("drop-shadow(1 2)", null)]
    [InlineData("drop-shadow(1px 2px -1px)", null)]
    [InlineData("drop-shadow(1px red 2px)", null)]
    [InlineData("drop-shadow(1px 2px) blur(1px)", null)]
    [InlineData("url(#drop-shadow)", null)]
    [InlineData("blur(2px)", null)]
    public void SequenceStyles_TryParseFilter_ParsesDropShadow(string value, string? expected)
    {
        // Act
        var parsed = SequenceStyles.TryParseFilter(value, out var shadow);

        // Assert
        Assert.Equal(expected is not null, parsed);
        if (expected == "none" || expected is null)
        {
            Assert.Null(shadow);
            return;
        }

        var parts = expected.Split(' ');
        Assert.Equal(
            new ShadowPaint(double.Parse(parts[0], CultureInfo.InvariantCulture), double.Parse(parts[1], CultureInfo.InvariantCulture), double.Parse(parts[2], CultureInfo.InvariantCulture), Color(parts[3])),
            shadow);
    }

    /// <summary>
    ///     Validates the mindmap's style rules of the default theme, as Chrome computes them for Mermaid's mindmap: the
    ///     global rules first; a section's shapes filled with its <c>cScale</c> color and not stroked, whatever the
    ///     element; the root's <c>git0</c> fill and <c>gitBranchLabel0</c> label over <c>.section--1</c>; the default
    ///     node's line in the section's inverse color, 3 pixels wide; labels colored as a span or as SVG text; and SVG text
    ///     not centered on its anchor.
    /// </summary>
    [Fact]
    public void MindmapStyles_Create_DefaultTheme_ResolvesSectionRules()
    {
        // Arrange
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), MindmapText);
        const string Root = "mindmap-node section-root section--1";
        const string Section0 = "mindmap-node section-0";

        // Act
        var styles = MindmapStyles.Create(config, null);

        // Assert: the colors of Chrome's computed styles of Mermaid's mindmap in the default theme
        Assert.Equal(
            FlowchartStyles.Create(config, null).Rules.Take(GlobalRuleCount).Select(rule => rule.Selector),
            styles.Rules.Take(GlobalRuleCount).Select(rule => rule.Selector));
        Assert.Equal(new ShapePaint(Color("#0000ec"), null, 1), styles.NodePaintFor(Root, "path"));
        Assert.Equal(new ShapePaint(Color("#ffff78"), null, 1), styles.NodePaintFor(Section0, "path"));
        Assert.All(
            (string[])["rect", "circle", "polygon"],
            element => Assert.Equal(Color("#ffff78"), styles.NodePaintFor(Section0, element).Fill));
        Assert.Equal(new ShapePaint(null, Color("#ffffb9"), 3), styles.NodePaintFor(Root, "line"));
        Assert.Equal(new ShapePaint(null, Color("#ababff"), 3), styles.NodePaintFor(Section0, "line"));
        Assert.Equal(Color("#ffffff"), styles.NodeLabelFor(Root, html: true).Color);
        Assert.Equal(Color("#000000"), styles.NodeLabelFor(Section0, html: true).Color);
        Assert.Equal(Color(config.ThemeVariable("cScaleLabel1")!), styles.NodeLabelFor(Section0, html: false).Color);
        Assert.Equal(16, styles.NodeLabelFor(Section0, html: true).FontSize);
        Assert.False(styles.CentersNodeSvgText);
    }

    /// <summary>
    ///     Validates the mindmap's edge rules: <c>.edge</c> gives a width of 3 and no fill, a section's edge its
    ///     <c>cScale</c> stroke, and <c>.edge-depth-k</c> the widths <c>17 - 3(k + 1)</c> while they are not negative
    ///     (CSS drops a negative width, so from depth 5 the edge keeps <c>.edge</c>'s 3, as Chrome computes it).
    /// </summary>
    [Fact]
    public void MindmapStyles_Create_EdgeRules_DropNegativeWidths()
    {
        // Arrange
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), MindmapText);

        // Act
        var styles = MindmapStyles.Create(config, null);
        var widths = Enumerable.Range(-1, 12)
            .Select(k => styles.Resolve($".edge-depth-{k}")["stroke-width"])
            .ToList();

        // Assert
        Assert.Equal(new string?[] { "17", "14", "11", "8", "5", "2", null, null, null, null, null, null }, widths);
        Assert.Equal(("3", "none"), (styles.Resolve(".edge")["stroke-width"], styles.Resolve(".edge")["fill"]));
        Assert.Equal(Color("#ffff78"), styles.Resolve(".section-edge-0").Paint("stroke"));
    }

    /// <summary>
    ///     Validates that every theme's mindmap sheet takes its colors from the theme's variables: a section's fill
    ///     <c>cScale</c>, its label <c>cScaleLabel</c>, its line <c>cScaleInv</c>, and the root's <c>git0</c> and
    ///     <c>gitBranchLabel0</c>.
    /// </summary>
    /// <param name="theme">The theme.</param>
    [Theory]
    [InlineData("default")]
    [InlineData("base")]
    [InlineData("dark")]
    [InlineData("forest")]
    [InlineData("neutral")]
    public void MindmapStyles_Create_EachTheme_UsesThemeVariables(string theme)
    {
        // Arrange
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject { ["theme"] = theme }, MindmapText);
        Rgba32 Var(string name) => Color(config.ThemeVariable(name)!);

        // Act
        var styles = MindmapStyles.Create(config, null);

        // Assert
        Assert.Equal(Var("cScale4"), styles.NodePaintFor("mindmap-node section-3", "rect").Fill);
        Assert.Equal(Var("cScaleLabel4"), styles.NodeLabelFor("mindmap-node section-3", html: true).Color);
        Assert.Equal(Var("cScaleInv4"), styles.NodePaintFor("mindmap-node section-3", "line").Stroke);
        Assert.Equal(Var("cScale11"), styles.NodePaintFor("mindmap-node section-10", "path").Fill);
        Assert.Equal(Var("git0"), styles.NodePaintFor("mindmap-node section-root section--1", "circle").Fill);
        Assert.Equal(Var("gitBranchLabel0"), styles.NodeLabelFor("mindmap-node section-root section--1", html: false).Color);
    }

    /// <summary>
    ///     Validates the gradient rules of the mindmap sheet, as Chrome computes them: the themes whose
    ///     <c>useGradient</c> is true (base, dark, forest and neutral) give the line under a default node a stroke width
    ///     of zero, the default theme keeps 3, and a theme variable <c>useGradient: false</c> keeps 3 in any theme.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <param name="useGradient">The <c>useGradient</c> theme variable, or null to keep the theme's.</param>
    /// <param name="expected">The expected stroke width of the line.</param>
    [Theory]
    [InlineData("default", null, 3)]
    [InlineData("base", null, 0)]
    [InlineData("dark", null, 0)]
    [InlineData("forest", null, 0)]
    [InlineData("neutral", null, 0)]
    [InlineData("forest", false, 3)]
    public void MindmapStyles_Create_GradientThemes_HideTheSectionLines(string theme, bool? useGradient, double expected)
    {
        // Arrange
        var site = new JsonObject { ["theme"] = theme };
        if (useGradient is { } flag)
        {
            site["themeVariables"] = new JsonObject { ["useGradient"] = flag };
        }

        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(site, MindmapText);

        // Act
        var styles = MindmapStyles.Create(config, null);

        // Assert
        Assert.Equal(expected, styles.NodePaintFor("mindmap-node section-2", "line").StrokeWidth);
        Assert.Equal(expected, styles.NodePaintFor("mindmap-node section-root section--1", "line").StrokeWidth);
    }

    /// <summary>
    ///     Validates that a <c>themeCSS</c> rule for a mindmap selector overrides the theme's, after the root rules (a
    ///     later <c>.section--1</c> rule wins over <c>.section-root</c>), and that an unknown selector is reported.
    /// </summary>
    [Fact]
    public void MindmapStyles_Create_ThemeCss_OverridesKnownSelectors()
    {
        // Arrange
        const string Text = "%%{init: {\"themeCSS\": \".section-0 rect { fill: #ff0000 } .section--1 span { color: #0000ff } .bogus { fill: red }\"}}%%\nmindmap\n  root\n";
        var source = Mermaid.Syntax.Preprocessor.Run(Text, "t.mmd", MermaidLimits.Default);
        var sink = new MermaidReportSink(100, throwOnUnsupported: false);
        var config = MermaidConfigResolver.Resolve(source, "mindmap", null, sink);
        var reports = new List<MermaidUnsupportedFeature>();

        // Act
        var styles = MindmapStyles.Create(config, sink);
        sink.Deliver(null, reports.Add);

        // Assert
        Assert.Single(reports);
        Assert.Equal(Color("#ff0000"), styles.NodePaintFor("mindmap-node section-0", "rect").Fill);
        Assert.NotEqual(Color("#ff0000"), styles.NodePaintFor("mindmap-node section-0", "path").Fill);
        Assert.Equal(Color("#0000ff"), styles.NodeLabelFor("mindmap-node section-root section--1", html: true).Color);
    }

    /// <summary>
    ///     Validates that a <c>themeCSS</c> rule for a class of the diagram's nodes and an element a node draws is
    ///     applied in sheet order with the section rules (as mmdc draws it, a later <c>.section-0 rect</c> wins over an
    ///     earlier <c>.urgent rect</c>), while a selector of another element is reported.
    /// </summary>
    [Fact]
    public void MindmapStyles_Create_ThemeCssNodeClass_CascadesInSheetOrder()
    {
        // Arrange: the classes of a root, a classed node of section 0, and a node of section 1
        const string Text = "%%{init: {\"themeCSS\": \".urgent rect { fill: #ff0000; stroke: #00ff00; stroke-width: 4px } .section-0 rect { fill: #0000ff } .urgent circle { fill: #ffa500 } .urgent span { color: #ffffff } .urgent p { color: #000000 }\"}}%%\nmindmap\n  root\n";
        var source = Mermaid.Syntax.Preprocessor.Run(Text, "t.mmd", MermaidLimits.Default);
        var sink = new MermaidReportSink(100, throwOnUnsupported: false);
        var config = MermaidConfigResolver.Resolve(source, "mindmap", null, sink);
        var reports = new List<MermaidUnsupportedFeature>();
        string[] nodeClasses = ["mindmap-node section-root section--1", "mindmap-node section-0 urgent", "mindmap-node section-1"];

        // Act
        var styles = MindmapStyles.Create(config, sink, nodeClasses);
        sink.Deliver(null, reports.Add);

        // Assert
        var report = Assert.Single(reports);
        Assert.Contains("'.urgent p'", report.Message, StringComparison.Ordinal);
        var rect = styles.NodePaintFor(nodeClasses[1], "rect");
        Assert.Equal(Color("#0000ff"), rect.Fill);
        Assert.Equal(Color("#00ff00"), rect.Stroke);
        Assert.Equal(4.0, rect.StrokeWidth);
        Assert.Equal(Color("#ffa500"), styles.NodePaintFor(nodeClasses[1], "circle").Fill);
        Assert.Equal(Color("#ffffff"), styles.NodeLabelFor(nodeClasses[1], html: true).Color);
        Assert.NotEqual(Color("#ffa500"), styles.NodePaintFor(nodeClasses[2], "circle").Fill);
        Assert.NotEqual(Color("#ffffff"), styles.NodeLabelFor(nodeClasses[2], html: true).Color);
    }

    /// <summary>
    ///     Validates that a <c>themeCSS</c> rule for a class no node has, or for a class that is not a plain CSS
    ///     identifier (it would read as another selector), is reported and not applied.
    /// </summary>
    [Fact]
    public void MindmapStyles_Create_ThemeCssOtherClass_IsReported()
    {
        // Arrange: the node class a.b, which CSS would read as the two classes a and b
        const string Text = "%%{init: {\"themeCSS\": \".urgent rect { fill: #ff0000 } .a.b rect { fill: #ff0000 }\"}}%%\nmindmap\n  root\n";
        var source = Mermaid.Syntax.Preprocessor.Run(Text, "t.mmd", MermaidLimits.Default);
        var sink = new MermaidReportSink(100, throwOnUnsupported: false);
        var config = MermaidConfigResolver.Resolve(source, "mindmap", null, sink);
        var reports = new List<MermaidUnsupportedFeature>();

        // Act
        var styles = MindmapStyles.Create(config, sink, ["mindmap-node section-0 a.b"]);
        sink.Deliver(null, reports.Add);

        // Assert
        Assert.Equal(2, reports.Count);
        Assert.NotEqual(Color("#ff0000"), styles.NodePaintFor("mindmap-node section-0 urgent", "rect").Fill);
        Assert.NotEqual(Color("#ff0000"), styles.NodePaintFor("mindmap-node section-0 a.b", "rect").Fill);
    }

    /// <summary>
    ///     Validates that the number of styled sections follows the theme's <c>THEME_COLOR_LIMIT</c>, as Mermaid's loop
    ///     does.
    /// </summary>
    [Fact]
    public void MindmapStyles_Create_ColorLimit_BoundsTheSections()
    {
        // Arrange
        var site = new JsonObject { ["themeVariables"] = new JsonObject { ["THEME_COLOR_LIMIT"] = 3 } };
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(site, MindmapText);

        // Act
        var styles = MindmapStyles.Create(config, null);

        // Assert
        Assert.NotNull(styles.NodePaintFor("mindmap-node section-1", "rect").Fill);
        Assert.Null(styles.NodePaintFor("mindmap-node section-2", "rect").Fill);
    }

    /// <summary>
    ///     Validates that the sheets of the other unified diagrams paint every element of a node's body with the node
    ///     rule and leave a line unpainted, whatever the node's classes, and color labels with the node label rule.
    /// </summary>
    [Fact]
    public void UnifiedStyles_NodePaintFor_OtherSheets_UseTheNodeRule()
    {
        // Arrange
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), "flowchart TD\n  A\n");

        // Act
        var styles = FlowchartStyles.Create(config, null);

        // Assert
        Assert.All(
            (string[])["rect", "circle", "polygon", "path"],
            element => Assert.Equal(styles.Node, styles.NodePaintFor("default section-0", element)));
        Assert.Equal(new ShapePaint(null, null, 1), styles.NodePaintFor("default", "line"));
        Assert.Equal(styles.NodeLabel, styles.NodeLabelFor("default section-0", html: false));
    }

    /// <summary>
    ///     Parses a color for an assertion.
    /// </summary>
    /// <param name="text">The color text.</param>
    /// <returns>The color.</returns>
    private static Rgba32 Color(string text) => CssColor.Parse(text);

    /// <summary>
    ///     Reads a paint Chrome computed (<c>none</c>, or a color as <c>rgb(...)</c> or <c>rgba(...)</c>).
    /// </summary>
    /// <param name="node">The computed value.</param>
    /// <returns>The paint, or null for none.</returns>
    private static Rgba32? ChromePaint(JsonNode? node) => (string)node! is "none" ? null : Color((string)node!);

    /// <summary>
    ///     Asserts that a paint is the one Chrome computed, each channel within one: Chrome converts an <c>hsl()</c> theme
    ///     color (the forest theme's <c>git1</c>, say) in double precision and rounds each channel's single-precision
    ///     value, while the core color parser converts in single precision, so a channel that falls on a half can round
    ///     the other way.
    /// </summary>
    /// <param name="expected">The computed value Chrome reported.</param>
    /// <param name="actual">The computed paint.</param>
    private static void AssertChromeColor(JsonNode? expected, Rgba32? actual)
    {
        var paint = ChromePaint(expected);
        Assert.Equal(paint is null, actual is null);
        if (paint is { } e && actual is { } a)
        {
            Assert.True(
                Math.Abs(e.R - a.R) <= 1 && Math.Abs(e.G - a.G) <= 1 && Math.Abs(e.B - a.B) <= 1 && e.A == a.A,
                $"Expected {e}, got {a}");
        }
    }

    /// <summary>
    ///     Reads a dash array Chrome computed (<c>none</c>, or lengths in pixels), as the cascade's parser reads one.
    /// </summary>
    /// <param name="node">The computed value.</param>
    /// <returns>The dash lengths, empty for none.</returns>
    private static IReadOnlyList<double> ChromeDash(JsonNode? node)
    {
        var text = (string)node!;
        if (text == "none")
        {
            return [];
        }

        return StyleDeclarations.TryParseDashArray(text, out var dashes) ? dashes : throw new FormatException(text);
    }

    /// <summary>
    ///     Reads a filter Chrome computed (<c>none</c>, or a <c>drop-shadow(color x y blur)</c>).
    /// </summary>
    /// <param name="node">The computed value.</param>
    /// <returns>The shadow, or null for none.</returns>
    private static ShadowPaint? ChromeShadow(JsonNode? node) =>
        SequenceStyles.TryParseFilter((string)node!, out var shadow) ? shadow : throw new FormatException((string)node!);
}
