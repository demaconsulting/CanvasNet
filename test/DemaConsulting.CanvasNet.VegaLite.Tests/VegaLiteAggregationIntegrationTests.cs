using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of the implicit encoding pipeline (aggregate, bin and stack from the encoding) through the public API, with the
///     original fixtures <c>aggregate-bars.vl.json</c>, <c>histogram.vl.json</c> and <c>stacked-bars.vl.json</c>. Every number is derived by hand
///     from the data and the scale formulas; the geometry of stacked ranges and binned bars is checked by the tests of the mark builder.
/// </summary>
public class VegaLiteAggregationIntegrationTests
{
    /// <summary>The names of the fixtures of the implicit pipeline.</summary>
    private static readonly string[] Fixtures = ["aggregate-bars.vl.json", "histogram.vl.json", "stacked-bars.vl.json"];

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Reads a fixture specification text.
    /// </summary>
    /// <param name="name">The file name in the <c>Fixtures</c> folder.</param>
    /// <returns>The text.</returns>
    private static string Text(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    /// <summary>
    ///     Opens a fixture from its path.
    /// </summary>
    /// <param name="name">The file name in the <c>Fixtures</c> folder.</param>
    /// <returns>The document.</returns>
    private static VegaLiteDocument Open(string name) => VegaLiteDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    /// <summary>
    ///     Gets the rectangles of the bars of a scene in drawing order.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <returns>The rectangles.</returns>
    private static List<SceneRect> Bars(VegaLiteDocument document) =>
        [.. document.BuildScene(new VegaLiteRenderOptions(), Ct).FindAll("mark-bar").Select(e => (SceneRect)e.Item)];

    /// <summary>
    ///     Gets the texts of a role in a scene.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="role">The role (<c>axis-label</c> or <c>axis-title</c>).</param>
    /// <returns>The texts in drawing order.</returns>
    private static List<string> Texts(VegaLiteDocument document, string role) =>
        [.. document.BuildScene(new VegaLiteRenderOptions(), Ct).FindAll(role).Select(e => ((SceneText)e.Item).Text)];

    /// <summary>
    ///     Validates the aggregate bar chart: the sums per region (north 3 + 4 = 7, south 5 + 1 = 6, east 2) become three bars in first-seen order,
    ///     the categories are sorted (east, north, south), the y domain is 0 to 7, and the axes are titled with Vega-Lite's default titles.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Aggregation_SumBarsHaveHandDerivedGeometryAndTitles()
    {
        // Arrange: three bands of 80 px (padding 0.1 inside, 0.05 outside) are 72 px wide and start at 4, 84 and 164 for east, north and south;
        // the y domain 0 to 7 is 100 px high, so 7, 6 and 2 are 100, 600/7 and 200/7 px high
        var document = Open("aggregate-bars.vl.json");

        // Act
        var bars = Bars(document);

        // Assert: the bars follow the order of the first appearance of the groups: north, south, east
        Assert.Equal(3, bars.Count);
        Assert.Equal([84.0, 164.0, 4.0], bars.Select(b => b.X).ToArray());
        Assert.All(bars, b => Assert.Equal(72.0, b.Width, 9));
        Assert.Equal([100.0, 600.0 / 7, 200.0 / 7], bars.Select(b => b.Height).ToArray(), (a, b) => Math.Abs(a - b) < 1e-9);
        Assert.Equal(["east", "north", "south"], Texts(document, "axis-label").Take(3).ToArray());
        Assert.Equal(["region", "Sum of units"], Texts(document, "axis-title"));
    }

    /// <summary>
    ///     Validates the histogram: twelve weights from 1.5 to 31 are binned with 10 bins asked for: the span 29.5 needs a step of 5 (the first step 10 and the
    ///     divisor 2), so the bins are 0 to 35 by 5, with the counts 3, 2, 3, 1, 1, 1, 1; the bars are as high as the counts (the y domain is 0 to 3).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Binning_HistogramCountsEveryBin()
    {
        // Arrange: [0, 5) has 1.5, 2.5 and 3; [5, 10) 7 and 8.5; [10, 15) 12, 14 and 14.5; [15, 20) 18; [20, 25) 22; [25, 30) 25; [30, 35] 31
        var document = Open("histogram.vl.json");

        // Act
        var parts = ChartTestHelpers.ResolveFull(Text("histogram.vl.json"));
        var bars = Bars(document);

        // Assert
        Assert.Equal(new BinSpec(0, 35, 5), parts.Data.Bins["bin_maxbins_10_weight"]);
        Assert.Equal(
            ["0|5|3", "5|10|2", "10|15|3", "15|20|1", "20|25|1", "25|30|1", "30|35|1"],
            Transforms.AggregateTestSupport.Rows(parts.Data.Main, "bin_maxbins_10_weight", "bin_maxbins_10_weight_end", "__count"));
        Assert.Equal(7, bars.Count);
        Assert.Equal([100.0, 200.0 / 3, 100.0, 100.0 / 3, 100.0 / 3, 100.0 / 3, 100.0 / 3], bars.Select(b => b.Height).ToArray(), (a, b) => Math.Abs(a - b) < 1e-9);
        Assert.Equal(["weight (binned)", "Count of Records"], Texts(document, "axis-title"));
        Assert.Equal((0, 35), (parts.Scales["x"]!.DomainStart, parts.Scales["x"]!.DomainEnd));
    }

    /// <summary>
    ///     Validates the stacked bar chart: five bars (one per region and product), the layers stacked from the bottom in the order washers, nuts, bolts
    ///     (descending by product on y): north is washers (0, 5), nuts (5, 7), bolts (7, 10) and south is nuts (0, 1), bolts (1, 5); the y axis reaches the
    ///     total 10. How the ranges are drawn is the business of the mark builder.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Stacking_LayersAreStackedAndTheAxisReachesTheTotal()
    {
        // Act
        var document = Open("stacked-bars.vl.json");
        var parts = ChartTestHelpers.ResolveFull(Text("stacked-bars.vl.json"));

        // Assert: the rows are the groups in first-seen order with their stack
        Assert.Equal(
            ["north|bolts|7|10", "north|nuts|5|7", "north|washers|0|5", "south|bolts|1|5", "south|nuts|0|1"],
            Transforms.AggregateTestSupport.Rows(parts.Data.Main, "region", "product", "sum_units_start", "sum_units_end"));
        Assert.Equal(5, Bars(document).Count);
        Assert.Equal((0, 10), (parts.Scales["y"]!.DomainStart, parts.Scales["y"]!.DomainEnd));
        Assert.Contains("10", Texts(document, "axis-label"));
        Assert.Equal(["region", "Sum of units"], Texts(document, "axis-title"));
    }

    /// <summary>
    ///     Validates that measuring gives the size of the rendered surface, that rendering twice is byte-identical, and that a scale multiplies the size,
    ///     for every fixture of the implicit pipeline.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Aggregation_MeasureMatchesRenderAndRendersAreByteIdentical()
    {
        foreach (var name in Fixtures)
        {
            // Arrange
            var document = Open(name);

            // Act
            var natural = document.Measure(cancellationToken: Ct);
            using var first = document.Render(cancellationToken: Ct);
            using var second = document.Render(cancellationToken: Ct);
            using var scaled = document.Render(new VegaLiteRenderOptions { Scale = 2 }, Ct);

            // Assert
            Assert.Equal(new VegaLiteSize(first.Width, first.Height), natural);
            Assert.Equal(VegaLiteTestHelpers.Snapshot(first), VegaLiteTestHelpers.Snapshot(second));
            Assert.Equal(new VegaLiteSize(natural.Width * 2, natural.Height * 2), new VegaLiteSize(scaled.Width, scaled.Height));
        }
    }

    /// <summary>
    ///     Validates the limits of the implicit stage through the public API: the groups count in the row budget (the five rows and the three groups of the
    ///     aggregate bars need eight), the failure is located at the aggregated channel and names the limit, and a canceled token stops the render.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Aggregation_LimitsAndCancellationApply()
    {
        // Arrange
        var document = Open("aggregate-bars.vl.json");

        // Act
        using var exact = document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxDataRows = 8 } }, Ct);
        var rows = Assert.Throws<VegaLiteRenderException>(() => document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxDataRows = 7 } }, Ct));
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Assert
        Assert.True(exact.Width > 0);
        Assert.Equal("/encoding/y", rows.Location.Pointer);
        Assert.Contains("MaxDataRows", rows.Message, StringComparison.Ordinal);
        Assert.Throws<OperationCanceledException>(() => document.Render(cancellationToken: source.Token));
    }

    /// <summary>
    ///     Validates that the aggregate, bin and stack of an encoding are drawn without a report (the color legend of the stacked bars is the only report
    ///     of the fixtures: legends are not drawn yet), and that a part that is still unsupported next to them (a time unit) is reported once with its
    ///     location, which strict mode turns into an exception.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Aggregation_OnlyTheUnsupportedPartIsReported()
    {
        // Arrange
        var reported = new List<string>();
        var options = new VegaLiteRenderOptions { UnsupportedFeatureHandler = u => reported.Add(u.Feature + " " + u.Location.Pointer) };
        var unsupported = Text("aggregate-bars.vl.json").Replace(
            "\"type\": \"nominal\"",
            "\"type\": \"nominal\", \"timeUnit\": \"month\"",
            StringComparison.Ordinal);

        // Act
        foreach (var name in Fixtures)
        {
            using var surface = Open(name).Render(options, Ct);
        }

        var fixtureReports = reported.ToArray();
        reported.Clear();
        var document = VegaLiteDocument.Parse(unsupported);
        using var drawn = document.Render(options, Ct);
        var strict = Assert.Throws<VegaLiteUnsupportedFeatureException>(() => document.Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct));

        // Assert
        Assert.Equal(["vl-guide-legend /encoding/color"], fixtureReports);
        Assert.Equal(["vl-encoding-timeunit /encoding/x/timeUnit"], reported);
        Assert.Equal("vl-encoding-timeunit", strict.Feature);
    }
}
