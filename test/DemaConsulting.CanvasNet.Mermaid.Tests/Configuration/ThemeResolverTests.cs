using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Configuration;

/// <summary>
///     Unit tests of <see cref="ThemeResolver"/>: the five themes against the variables Mermaid computes, the JavaScript
///     object model, the bounds, and the flowchart style rules built from the variables.
/// </summary>
public class ThemeResolverTests
{
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
    ///     Parses a color for an assertion.
    /// </summary>
    /// <param name="text">The color text.</param>
    /// <returns>The color.</returns>
    private static Rgba32 Color(string text) => CssColor.Parse(text);
}
