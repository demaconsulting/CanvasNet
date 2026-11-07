using System.Globalization;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of the statistical transforms (Phase 13): <c>regression</c>, <c>loess</c>, <c>density</c>, <c>quantile</c> and <c>extent</c> (and the aggregate
///     operations of <c>pivot</c> that the quantile comparison uses) are rendered through the public API in a layered view, and checked by the pixel positions of the marks, with every
///     number derived by hand from the scale formulas (a 300 pixel data rectangle whose domains are fixed, so that a data value <c>v</c> in the domain <c>[a, b]</c> is at
///     <c>300 (v - a) / (b - a)</c> pixels from the left, and from the top at <c>300 - 300 (v - a) / (b - a)</c>).
/// </summary>
public class VegaLiteStatTransformsIntegrationTests
{
    /// <summary>The cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The rows <c>y = 2x + 1</c> for x = 0 to 5.</summary>
    private const string LineRows = "[{\"x\":0,\"y\":1},{\"x\":1,\"y\":3},{\"x\":2,\"y\":5},{\"x\":3,\"y\":7},{\"x\":4,\"y\":9},{\"x\":5,\"y\":11}]";

    /// <summary>The encoding of the x and y positions on fixed domains: x in [0, 10] and y in [0, 30].</summary>
    private const string Position =
        "\"x\":{\"field\":\"x\",\"type\":\"quantitative\",\"axis\":null,\"scale\":{\"domain\":[0,10],\"nice\":false,\"zero\":false}},\"y\":{\"field\":\"y\",\"type\":\"quantitative\",\"axis\":null,\"scale\":{\"domain\":[0,30],\"nice\":false,\"zero\":false}}";

    /// <summary>
    ///     Lays a specification out without painting.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The scene.</returns>
    private static Scene SceneOf(string json) => VegaLiteDocument.Parse(json).BuildScene(new VegaLiteRenderOptions(), Ct);

    /// <summary>
    ///     Gets the items of a role in a scene as a given kind.
    /// </summary>
    /// <typeparam name="T">The kind of item.</typeparam>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role.</param>
    /// <returns>The items in drawing order.</returns>
    private static List<T> Items<T>(Scene scene, string role)
        where T : SceneItem => [.. scene.FindAll(role).Select(e => (T)e.Item)];

    /// <summary>
    ///     Writes the coordinates of a path as text: <c>x,y</c> pairs rounded to six decimals, joined with spaces.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The text.</returns>
    private static string Describe(ScenePath path) =>
        string.Join(" ", path.Segments.Select(s => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(s.X, 6)},{Math.Round(s.Y, 6)}")));

    /// <summary>
    ///     Builds a layered chart: the points of the rows and a line mark with the transform.
    /// </summary>
    /// <param name="rows">The rows.</param>
    /// <param name="transform">The transform entry of the line layer.</param>
    /// <param name="encoding">The encoding of both layers.</param>
    /// <param name="extra">More members of the line layer (each preceded by a comma).</param>
    /// <returns>The specification text.</returns>
    private static string Chart(string rows, string transform, string encoding = Position, string extra = "") =>
        "{\"width\":300,\"height\":300,\"data\":{\"values\":" + rows + "},\"layer\":[{\"mark\":\"point\",\"encoding\":{" + encoding + "}},"
        + "{\"mark\":\"line\",\"transform\":[" + transform + "],\"encoding\":{" + encoding + "}" + extra + "}]}";

    /// <summary>
    ///     Validates the regression line of a layered chart: the line is the two end points of <c>y = 2x + 1</c> at the extent of x, at the pixels of the scales (x = 0 and 5 are at 0 and 150 pixels,
    ///     y = 1 and 11 are at 290 and 190 pixels from the top), and the points layer is unchanged (six points).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_StatTransforms_RegressionLineIsDrawnAtTheScaledEndPoints()
    {
        // Arrange
        var json = Chart(LineRows, "{\"regression\":\"y\",\"on\":\"x\"}");

        // Act
        var scene = SceneOf(json);

        // Assert
        Assert.Equal("0,290 150,190", Describe(Assert.Single(Items<ScenePath>(scene, "mark-line"))));
        Assert.Equal(6, Items<SceneSymbol>(scene, "mark-point").Count);
    }

    /// <summary>
    ///     Validates the <c>extent</c> of a regression: the line is drawn over [0, 10], so its end is at 300 pixels and 21 from the top at 300 - 210 = 90 pixels.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_StatTransforms_RegressionExtentExtendsTheLine()
    {
        // Arrange
        var json = Chart(LineRows, "{\"regression\":\"y\",\"on\":\"x\",\"extent\":[0,10]}");

        // Act
        var scene = SceneOf(json);

        // Assert
        Assert.Equal("0,290 300,90", Describe(Assert.Single(Items<ScenePath>(scene, "mark-line"))));
    }

    /// <summary>
    ///     Validates the groups of a regression: one line per group of <c>detail</c>, each through its own end points (a: 1 + 2x, b: 5 - 2x over x in [0, 2], on the domains [0, 2] and [0, 6]).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_StatTransforms_GroupedRegressionDrawsOneLineEach()
    {
        // Arrange
        const string rows = "[{\"g\":\"a\",\"x\":0,\"y\":1},{\"g\":\"a\",\"x\":1,\"y\":3},{\"g\":\"a\",\"x\":2,\"y\":5},{\"g\":\"b\",\"x\":0,\"y\":5},{\"g\":\"b\",\"x\":1,\"y\":3},{\"g\":\"b\",\"x\":2,\"y\":1}]";
        const string encoding =
            "\"x\":{\"field\":\"x\",\"type\":\"quantitative\",\"axis\":null,\"scale\":{\"domain\":[0,2],\"nice\":false,\"zero\":false}},"
            + "\"y\":{\"field\":\"y\",\"type\":\"quantitative\",\"axis\":null,\"scale\":{\"domain\":[0,6],\"nice\":false,\"zero\":false}},"
            + "\"detail\":{\"field\":\"g\",\"type\":\"nominal\"}";
        var json = Chart(rows, "{\"regression\":\"y\",\"on\":\"x\",\"groupby\":[\"g\"]}", encoding);

        // Act
        var lines = Items<ScenePath>(SceneOf(json), "mark-line");

        // Assert: a runs from (0, 1) to (2, 5), b from (0, 5) to (2, 1)
        Assert.Equal(["0,250 300,50", "0,50 300,250"], lines.Select(Describe));
    }

    /// <summary>
    ///     Validates the loess of exactly linear data: the smoothed line is the data line, so every vertex of the drawn line is at the pixel of its own data point.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_StatTransforms_LoessOfALineIsTheLine()
    {
        // Arrange
        var json = Chart(LineRows, "{\"loess\":\"y\",\"on\":\"x\",\"bandwidth\":0.8}");

        // Act
        var line = Assert.Single(Items<ScenePath>(SceneOf(json), "mark-line"));

        // Assert: x = 0..5 at 30 pixels each and y = 2x + 1 at 300 - 10y pixels from the top
        Assert.Equal(6, line.Segments.Count);
        for (var i = 0; i < 6; i++)
        {
            Assert.Equal(30.0 * i, line.Segments[i].X, 6);
            Assert.Equal(300 - (10.0 * ((2 * i) + 1)), line.Segments[i].Y, 6);
        }
    }

    /// <summary>
    ///     Validates a density line: for the support {4, 6} with the bandwidth 2 over [-5, 15] in 40 steps the line has 41 vertices at 15 pixels per unit and the highest one is at x = 5
    ///     (the center of the support), at the height that the Gaussian formula gives.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_StatTransforms_DensityLinePeaksAtTheCenterOfTheSupport()
    {
        // Arrange: the fields of the output are value and density
        const string rows = "[{\"v\":4},{\"v\":6}]";
        const string encoding =
            "\"x\":{\"field\":\"value\",\"type\":\"quantitative\",\"axis\":null,\"scale\":{\"domain\":[-5,15],\"nice\":false,\"zero\":false}},"
            + "\"y\":{\"field\":\"density\",\"type\":\"quantitative\",\"axis\":null,\"scale\":{\"domain\":[0,0.25],\"nice\":false,\"zero\":false}}";
        var json = "{\"width\":300,\"height\":300,\"data\":{\"values\":" + rows + "},\"transform\":[{\"density\":\"v\",\"bandwidth\":2,\"steps\":40,\"extent\":[-5,15]}],\"mark\":\"line\",\"encoding\":{" + encoding + "}}";

        // Act
        var line = Assert.Single(Items<ScenePath>(SceneOf(json), "mark-line"));

        // Assert
        Assert.Equal(41, line.Segments.Count);
        var peak = line.Segments.MinBy(s => s.Y);
        Assert.Equal(15.0 * 10, peak.X, 6);
        var density = ((Math.Exp(-0.5 * 0.25) / Math.Sqrt(2 * Math.PI)) * 2) / 2 / 2;
        Assert.Equal(300 - (density / 0.25 * 300), peak.Y, 6);
    }

    /// <summary>
    ///     Validates a quantile-quantile plot of two identical samples: the quantiles of each group are pivoted into the columns a and b (with the aggregate operation <c>max</c>), so every point
    ///     is on the diagonal, which in the pixels of equal domains is <c>x + y = 300</c>.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_StatTransforms_QuantilesOfTwoIdenticalSamplesLieOnTheDiagonal()
    {
        // Arrange: two groups of 1..10
        var rows = StatTestSupport.GroupRows(["a", "b"], 10);
        const string encoding =
            "\"x\":{\"field\":\"a\",\"type\":\"quantitative\",\"axis\":null,\"scale\":{\"domain\":[0,11],\"nice\":false,\"zero\":false}},"
            + "\"y\":{\"field\":\"b\",\"type\":\"quantitative\",\"axis\":null,\"scale\":{\"domain\":[0,11],\"nice\":false,\"zero\":false}}";
        var json = "{\"width\":300,\"height\":300,\"data\":{\"values\":" + rows + "},\"transform\":["
            + "{\"quantile\":\"v\",\"groupby\":[\"g\"],\"step\":0.1},{\"pivot\":\"g\",\"value\":\"value\",\"groupby\":[\"prob\"],\"op\":\"max\"}],\"mark\":\"point\",\"encoding\":{" + encoding + "}}";

        // Act
        var points = Items<SceneSymbol>(SceneOf(json), "mark-point");

        // Assert: ten quantiles (0.05, 0.15, ... 0.95) of 1..10 are 1 + 9p = 1.45, 2.35, ... 9.55
        Assert.Equal(10, points.Count);
        Assert.All(points, p => Assert.Equal(300, p.X + p.Y, 6));
        Assert.Equal(300 * 1.45 / 11, points[0].X, 6);
        Assert.Equal(300 * 9.55 / 11, points[9].X, 6);
    }

    /// <summary>
    ///     Validates that the extent of a field is a parameter that the following transforms read: the values 2, 5 and 10 normalized by their extent are at 0, 0.375 and 1, so the points are at 0, 112.5 and
    ///     300 pixels on the domain [0, 1].
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_StatTransforms_ExtentParameterFeedsTheFollowingTransforms()
    {
        // Arrange
        const string json = """
            {
              "width": 300, "height": 300,
              "data": { "values": [ { "v": 2 }, { "v": 5 }, { "v": 10 } ] },
              "transform": [ { "extent": "v", "param": "e" }, { "calculate": "(datum.v - e[0]) / (e[1] - e[0])", "as": "u" } ],
              "mark": "point",
              "encoding": { "x": { "field": "u", "type": "quantitative", "axis": null, "scale": { "domain": [0, 1], "nice": false, "zero": false } } }
            }
            """;

        // Act
        var points = Items<SceneSymbol>(SceneOf(json), "mark-point");

        // Assert
        Assert.Equal([0.0, 112.5, 300.0], points.Select(p => p.X));
    }

    /// <summary>
    ///     Validates that a scale domain that is the extent parameter (<c>{"param": "e"}</c>) is read after the data stage: the domain is [2, 10], so the values 2, 5 and 10 are at 0, 112.5 and 300 pixels.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_StatTransforms_ExtentParameterSetsAScaleDomain()
    {
        // Arrange
        const string json = """
            {
              "width": 300, "height": 300,
              "data": { "values": [ { "v": 2 }, { "v": 5 }, { "v": 10 } ] },
              "transform": [ { "extent": "v", "param": "e" } ],
              "mark": "point",
              "encoding": { "x": { "field": "v", "type": "quantitative", "axis": null, "scale": { "domain": { "param": "e" }, "nice": false, "zero": false } } }
            }
            """;

        // Act
        var points = Items<SceneSymbol>(SceneOf(json), "mark-point");

        // Assert
        Assert.Equal([0.0, 112.5, 300.0], points.Select(p => p.X));
    }

    /// <summary>
    ///     Validates the documented limit of the extent parameter: a static site (a title that is an expression reference) is evaluated before the data is read, so it sees null, and the renderer
    ///     reports the warning <c>vl-param-extent-static</c> (a warning, so strict mode does not stop).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_StatTransforms_StaticSitesSeeNoExtentAndAreReported()
    {
        // Arrange
        const string json = """
            {
              "width": 300, "height": 300,
              "title": { "text": { "expr": "'max ' + e[1]" } },
              "data": { "values": [ { "v": 2 }, { "v": 10 } ] },
              "transform": [ { "extent": "v", "param": "e" } ],
              "mark": "point",
              "encoding": { "x": { "field": "v", "type": "quantitative" } }
            }
            """;
        var warnings = new List<VegaLiteDiagnostic>();
        var options = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, WarningHandler = warnings.Add };

        // Act
        using var surface = VegaLiteDocument.Parse(json).Render(options, Ct);

        // Assert
        Assert.True(surface.Width > 0);
        var warning = Assert.Single(warnings, w => w.Message.StartsWith("vl-param-extent-static:", StringComparison.Ordinal));
        Assert.Equal("/title/text", warning.Location.Pointer);
        Assert.Contains("'e'", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a chart with statistical transforms renders deterministically and in strict mode: two renders give the same pixels, and nothing is reported as unsupported.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_StatTransforms_RenderDeterministicallyInStrictMode()
    {
        // Arrange
        var json = Chart(LineRows, "{\"regression\":\"y\",\"on\":\"x\",\"method\":\"quad\"}");
        var options = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true };
        var document = VegaLiteDocument.Parse(json);

        // Act
        using var first = document.Render(options, Ct);
        using var second = document.Render(options, Ct);

        // Assert
        Assert.Equal(VegaLiteTestHelpers.Snapshot(first), VegaLiteTestHelpers.Snapshot(second));
    }

    /// <summary>
    ///     Validates that a regression is computed in every cell of a facet: each cell has the rows of its value and its own line, so the two cells draw two lines.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_StatTransforms_FacetCellsGetTheirOwnRegression()
    {
        // Arrange
        const string json = """
            {
              "data": { "values": [
                { "g": "a", "x": 0, "y": 1 }, { "g": "a", "x": 1, "y": 3 }, { "g": "a", "x": 2, "y": 5 },
                { "g": "b", "x": 0, "y": 5 }, { "g": "b", "x": 1, "y": 3 }, { "g": "b", "x": 2, "y": 1 } ] },
              "facet": { "column": { "field": "g", "type": "nominal" } },
              "spec": {
                "width": 100, "height": 100,
                "transform": [ { "regression": "y", "on": "x" } ],
                "mark": "line",
                "encoding": {
                  "x": { "field": "x", "type": "quantitative", "scale": { "domain": [0, 2], "nice": false } },
                  "y": { "field": "y", "type": "quantitative", "scale": { "domain": [0, 6], "nice": false } }
                }
              }
            }
            """;

        // Act
        var lines = Items<ScenePath>(SceneOf(json), "mark-line");

        // Assert: a runs from (0, 1) to (2, 5), b from (0, 5) to (2, 1) in cells of 100 pixels with the domains [0, 2] and [0, 6]
        Assert.Equal(2, lines.Count);
        Assert.Equal(["0,83.333333 100,16.666667", "0,16.666667 100,83.333333"], lines.Select(Describe));
    }
}
