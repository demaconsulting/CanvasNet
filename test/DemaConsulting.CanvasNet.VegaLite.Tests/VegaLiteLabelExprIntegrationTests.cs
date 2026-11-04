using System.Globalization;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of <c>labelExpr</c>: the label expressions of axes, legends and facet headers (and of the configuration for axes and headers) run on the expression engine of the call when a chart is laid
///     out, so the labels have the replaced text before they are measured, strict mode has nothing to complain about, and the limits of the call apply.
/// </summary>
public class VegaLiteLabelExprIntegrationTests
{
    /// <summary>The rows: years with a share and a group.</summary>
    private const string Data = """
        "data": { "values": [ { "year": 2020, "share": 20, "g": "a", "d": "2020-01-01" }, { "year": 2021, "share": 40, "g": "b", "d": "2021-01-01" }, { "year": 2022, "share": 60, "g": "a", "d": "2022-01-01" } ] }
        """;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Lays a specification out without painting.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="options">The render options, or null.</param>
    /// <returns>The scene.</returns>
    private static Scene SceneOf(string json, VegaLiteRenderOptions? options = null) => VegaLiteDocument.Parse(json).BuildScene(options ?? new VegaLiteRenderOptions(), Ct);

    /// <summary>
    ///     Gets the texts of the items of a role.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role.</param>
    /// <returns>The texts in drawing order.</returns>
    private static List<string> Texts(Scene scene, string role) => [.. scene.FindAll(role).Select(e => ((SceneText)e.Item).Text)];

    /// <summary>
    ///     Validates the axis of a line chart with <c>datum.label + '%'</c> on y and a conditional on x, rendered with strict mode (no diagnostics).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_LabelExpr_AxisLabelsAreReplacedAndStrictModeHasNothingToReport()
    {
        // Arrange
        var json = "{ " + Data + ", \"mark\": \"line\", \"encoding\": { "
            + "\"x\": { \"field\": \"year\", \"type\": \"ordinal\", \"axis\": { \"labelAngle\": 0, \"labelExpr\": \"datum.value % 2 == 0 ? 'FY' + datum.label : ''\" } }, "
            + "\"y\": { \"field\": \"share\", \"type\": \"quantitative\", \"axis\": { \"labelExpr\": \"datum.label + '%'\" } } } }";
        var unsupported = new List<string>();
        var options = new VegaLiteRenderOptions { UnsupportedFeatureHandler = u => unsupported.Add(u.Feature), ThrowOnUnsupportedFeature = true };

        // Act
        var scene = SceneOf(json, options);

        // Assert
        var labels = Texts(scene, "axis-label");
        Assert.Contains("FY2020", labels);
        Assert.Contains("FY2022", labels);
        Assert.DoesNotContain("2021", labels);
        Assert.Contains("20%", labels);
        Assert.Contains("60%", labels);
        Assert.Empty(unsupported);
    }

    /// <summary>
    ///     Validates the labels of the axis of a temporal field: <c>datum.value</c> is a date.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_LabelExpr_TemporalAxisUsesTheDateValue()
    {
        // Arrange
        var json = "{ " + Data + ", \"mark\": \"line\", \"encoding\": { "
            + "\"x\": { \"field\": \"d\", \"type\": \"temporal\", \"axis\": { \"values\": [\"2021-01-01\"], \"labelExpr\": \"'Q' + quarter(datum.value) + ' ' + year(datum.value)\" } }, "
            + "\"y\": { \"field\": \"share\", \"type\": \"quantitative\" } } }";

        // Act
        var labels = Texts(SceneOf(json), "axis-label");

        // Assert
        Assert.Contains("Q1 2021", labels);
    }

    /// <summary>
    ///     Validates the labels of a legend, with the index and value.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_LabelExpr_LegendLabelsAreReplaced()
    {
        // Arrange
        var json = "{ " + Data + ", \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"year\", \"type\": \"quantitative\" }, "
            + "\"color\": { \"field\": \"g\", \"type\": \"nominal\", \"legend\": { \"labelExpr\": \"upper(datum.value) + ' (' + datum.index + ')'\" } } } }";

        // Act
        var labels = Texts(SceneOf(json), "legend-label");

        // Assert
        Assert.Equal(["A (0)", "B (1)"], labels);
    }

    /// <summary>
    ///     Validates the headers of a faceted chart: the label of a column and of a row, and the wrapped facet's cell titles, with the precedence of the header over the header configuration of the channel and
    ///     <c>config.header</c>.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_LabelExpr_HeaderLabelsAreReplacedWithPrecedence()
    {
        // Arrange: the column header has its own expression, the row header the one of headerRow, and config.header is the fallback
        var json = "{ \"config\": { \"header\": { \"labelExpr\": \"'H:' + datum.label\" }, \"headerRow\": { \"labelExpr\": \"'R:' + datum.value\" } }, " + Data
            + ", \"facet\": { \"column\": { \"field\": \"g\", \"type\": \"nominal\", \"header\": { \"labelExpr\": \"'C:' + datum.label\" } }, \"row\": { \"field\": \"year\", \"type\": \"ordinal\" } },"
            + " \"spec\": { \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"share\", \"type\": \"quantitative\", \"axis\": null }, \"y\": { \"field\": \"share\", \"type\": \"quantitative\", \"axis\": null } } } }";

        // Act
        var scene = SceneOf(json);
        var columns = scene.FindAll("column-header").SelectMany(e => Scene.Enumerate((SceneGroup)e.Item)).Select(x => x.Item).OfType<SceneText>().Select(t => t.Text).ToList();
        var rows = scene.FindAll("row-header").SelectMany(e => Scene.Enumerate((SceneGroup)e.Item)).Select(x => x.Item).OfType<SceneText>().Select(t => t.Text).ToList();

        // Assert
        Assert.Equal(["C:a", "C:b"], columns);
        Assert.Equal(["R:2020", "R:2021", "R:2022"], rows);
    }

    /// <summary>
    ///     Validates the labels of a wrapped facet (the title of each cell) with <c>config.header</c> as the fallback.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_LabelExpr_WrappedFacetTitlesAreReplaced()
    {
        // Arrange
        var json = "{ \"config\": { \"headerFacet\": { \"labelExpr\": \"datum.label + '!' + datum.value\" } }, " + Data
            + ", \"facet\": { \"field\": \"g\", \"type\": \"nominal\" }, \"spec\": { \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"share\", \"type\": \"quantitative\", \"axis\": null }, \"y\": { \"field\": \"share\", \"type\": \"quantitative\", \"axis\": null } } } }";

        // Act
        var scene = SceneOf(json);
        var titles = scene.Enumerate().Select(e => e.Item).OfType<SceneText>().Select(t => t.Text).ToList();

        // Assert: the title of a cell is its label
        Assert.Contains("a!a", titles);
        Assert.Contains("b!b", titles);
    }

    /// <summary>
    ///     Validates that the expression can read a variable parameter of the specification.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_LabelExpr_ParameterCanBeUsed()
    {
        // Arrange
        var json = "{ \"params\": [ { \"name\": \"unit\", \"value\": \" kg\" } ], " + Data + ", \"mark\": \"point\", \"encoding\": { "
            + "\"x\": { \"field\": \"year\", \"type\": \"ordinal\" }, \"y\": { \"field\": \"share\", \"type\": \"quantitative\", \"axis\": { \"labelExpr\": \"datum.label + unit\" } } } }";

        // Act
        var labels = Texts(SceneOf(json), "axis-label");

        // Assert
        Assert.Contains("20 kg", labels);
    }

    /// <summary>
    ///     Validates that the result does not depend on the current culture: the same text under <c>tr-TR</c>.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_LabelExpr_ResultDoesNotDependOnTheCulture()
    {
        // Arrange: upper() of an i and the number text under the Turkish culture
        var json = "{ " + Data + ", \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"year\", \"type\": \"ordinal\", \"axis\": { \"labelExpr\": \"upper('i') + (datum.value / 4)\" } }, \"y\": { \"field\": \"share\", \"type\": \"quantitative\" } } }";
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            var invariant = Texts(SceneOf(json), "axis-label");
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            var turkish = Texts(SceneOf(json), "axis-label");

            // Assert
            Assert.Equal(invariant, turkish);
            Assert.Contains("I505", turkish);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>
    ///     Validates that the step budget of the call applies to the label expressions: an expression that needs more is a render exception located at it.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_LabelExpr_BudgetExceededIsARenderException()
    {
        // Arrange
        var json = "{ " + Data + ", \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"year\", \"type\": \"ordinal\", \"axis\": { \"labelExpr\": \"datum.label + 1 + 2 + 3 + 4 + 5 + 6 + 7 + 8\" } }, \"y\": { \"field\": \"share\", \"type\": \"quantitative\" } } }";
        var options = new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxEvaluationSteps = 20 } };

        // Act
        var error = Assert.Throws<VegaLiteRenderException>(() => SceneOf(json, options));

        // Assert
        Assert.Contains("MaxEvaluationSteps", error.Message, StringComparison.Ordinal);
        Assert.Equal("/encoding/x/axis/labelExpr", error.Location.Pointer);
    }

    /// <summary>
    ///     Validates that a function outside the table (a color function) is reported as an unsupported feature at the text and the labels are empty, and strict mode fails.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_LabelExpr_UnsupportedFunctionIsReportedAndEvaluatesToEmptyLabels()
    {
        // Arrange
        var json = "{ " + Data + ", \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"year\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"share\", \"type\": \"quantitative\", \"axis\": { \"labelExpr\": \"luminance('#fff') + datum.label\" } } } }";
        var unsupported = new List<string>();
        var options = new VegaLiteRenderOptions { UnsupportedFeatureHandler = u => unsupported.Add(u.Feature) };

        // Act
        using var surface = VegaLiteDocument.Parse(json).Render(options, Ct);
        var scene = SceneOf(json);

        // Assert
        Assert.Equal(["vl-expr-function-luminance"], unsupported);
        Assert.DoesNotContain("40", Texts(scene, "axis-label"));
        Assert.Throws<VegaLiteUnsupportedFeatureException>(() => VegaLiteDocument.Parse(json).Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct));
    }

    /// <summary>
    ///     Validates that <c>format</c> and <c>timeFormat</c> draw without diagnostics in strict mode on an axis, a header and a legend: the y labels have a thousands separator, the header and the legend are
    ///     formatted, and an invalid specifier is a render exception located at the expression.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_LabelExpr_FormatAndTimeFormatDrawWithoutDiagnostics()
    {
        // Arrange
        var json = "{ " + Data + ", \"facet\": { \"column\": { \"field\": \"g\", \"type\": \"nominal\", \"header\": { \"labelExpr\": \"format(datum.value == 'a' ? 1 : 2, '03d')\" } } },"
            + " \"spec\": { \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"d\", \"type\": \"temporal\", \"axis\": { \"values\": [\"2021-01-01\"], \"labelExpr\": \"timeFormat(datum.value, '%b %Y')\" } },"
            + " \"y\": { \"field\": \"share\", \"type\": \"quantitative\", \"axis\": { \"labelExpr\": \"format(datum.value * 1000, ',.0f')\" } },"
            + " \"color\": { \"field\": \"year\", \"type\": \"ordinal\", \"legend\": { \"labelExpr\": \"format(datum.value, '+d')\" } } } } }";
        var unsupported = new List<string>();
        var options = new VegaLiteRenderOptions { UnsupportedFeatureHandler = u => unsupported.Add(u.Feature), ThrowOnUnsupportedFeature = true };

        // Act
        var scene = SceneOf(json, options);
        var invalid = Assert.Throws<VegaLiteRenderException>(() => SceneOf(json.Replace("',.0f'", "'.2.3f'", StringComparison.Ordinal)));

        // Assert
        Assert.Empty(unsupported);
        Assert.Contains("20,000", Texts(scene, "axis-label"));
        Assert.Contains("Jan 2021", Texts(scene, "axis-label"));
        Assert.Equal(["+2020", "+2021", "+2022"], Texts(scene, "legend-label"));
        var headers = scene.FindAll("column-header").SelectMany(e => Scene.Enumerate((SceneGroup)e.Item)).Select(x => x.Item).OfType<SceneText>().Select(t => t.Text).ToList();
        Assert.Equal(["001", "002"], headers);
        Assert.Contains("invalid format: .2.3f", invalid.Message, StringComparison.Ordinal);
        Assert.EndsWith("/axis/labelExpr", invalid.Location.Pointer, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Gets the texts of the items of a header group of a facet, as the scene holds them.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role of the header group.</param>
    /// <returns>The text items.</returns>
    private static List<SceneText> HeaderTexts(Scene scene, string role) =>
        [.. scene.FindAll(role).SelectMany(e => Scene.Enumerate((SceneGroup)e.Item)).Select(x => x.Item).OfType<SceneText>()];

    /// <summary>
    ///     Validates that an array result of a header <c>labelExpr</c> is several lines of the label (not one line joined by a line feed), for a column and a row, and that a string with a line feed stays one line.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_LabelExpr_HeaderArrayResultIsSeveralLines()
    {
        // Arrange
        var json = "{ " + Data
            + ", \"facet\": { \"column\": { \"field\": \"g\", \"type\": \"nominal\", \"header\": { \"labelExpr\": \"[datum.label, 'n=' + datum.value]\" } },"
            + " \"row\": { \"field\": \"year\", \"type\": \"ordinal\", \"header\": { \"labelExpr\": \"[datum.label, 'r']\" } } },"
            + " \"spec\": { \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"share\", \"type\": \"quantitative\", \"axis\": null }, \"y\": { \"field\": \"share\", \"type\": \"quantitative\", \"axis\": null } } } }";
        var oneString = json.Replace("[datum.label, 'n=' + datum.value]", "datum.label + '\\\\n' + datum.value", StringComparison.Ordinal);

        // Act
        var scene = SceneOf(json);
        var columns = HeaderTexts(scene, "column-header");
        var rows = HeaderTexts(scene, "row-header");
        var single = HeaderTexts(SceneOf(oneString), "column-header");

        // Assert
        Assert.Equal([["a", "n=a"], ["b", "n=b"]], columns.Select(t => t.Lines.ToArray()));
        Assert.Equal([["2020", "r"], ["2021", "r"], ["2022", "r"]], rows.Select(t => t.Lines.ToArray()));
        Assert.All(single, t => Assert.Single(t.Lines));
        Assert.Equal(["a\na", "b\nb"], single.Select(t => t.Text));
    }

    /// <summary>
    ///     Validates that an array result of <c>headerFacet.labelExpr</c> is several lines in the title of the cell of a wrapped facet.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_LabelExpr_WrappedFacetTitleArrayResultIsSeveralLines()
    {
        // Arrange
        var json = "{ \"config\": { \"headerFacet\": { \"labelExpr\": \"[datum.label, '#' + datum.value]\" } }, " + Data
            + ", \"facet\": { \"field\": \"g\", \"type\": \"nominal\" }, \"spec\": { \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"share\", \"type\": \"quantitative\", \"axis\": null }, \"y\": { \"field\": \"share\", \"type\": \"quantitative\", \"axis\": null } } } }";

        // Act
        var titles = SceneOf(json).Enumerate().Select(e => e.Item).OfType<SceneText>().Where(t => t.Lines.Count == 2).Select(t => t.Lines.ToArray()).ToList();

        // Assert
        Assert.Equal([["a", "#a"], ["b", "#b"]], titles);
    }

    /// <summary>
    ///     Validates that the layout measures the lines of a header label: the chart with a two-line header is taller than the one with a one-line header.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_LabelExpr_HeaderMultiLineLabelIsTallerThanOneLine()
    {
        // Arrange
        string Chart(string expression) => "{ " + Data + ", \"facet\": { \"column\": { \"field\": \"g\", \"type\": \"nominal\", \"header\": { \"labelExpr\": \"" + expression + "\" } } },"
            + " \"spec\": { \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"share\", \"type\": \"quantitative\", \"axis\": null }, \"y\": { \"field\": \"share\", \"type\": \"quantitative\", \"axis\": null } } } }";

        // Act
        var one = SceneOf(Chart("datum.label"));
        var two = SceneOf(Chart("[datum.label, datum.value]"));

        // Assert: a line is about a dozen pixels
        Assert.True(two.Height > one.Height + 8, $"{two.Height} should be taller than {one.Height}");
    }
}
