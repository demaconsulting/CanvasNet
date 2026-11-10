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
    ///     paint, the 14 pixel edge labels and their two backgrounds, the title in the root font (its class has no rule),
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
        Assert.Equal([".labelBkg", ".edgeLabel"], styles.HtmlEdgeLabelBackgrounds);
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
    ///     Parses a color for an assertion.
    /// </summary>
    /// <param name="text">The color text.</param>
    /// <returns>The color.</returns>
    private static Rgba32 Color(string text) => CssColor.Parse(text);
}
