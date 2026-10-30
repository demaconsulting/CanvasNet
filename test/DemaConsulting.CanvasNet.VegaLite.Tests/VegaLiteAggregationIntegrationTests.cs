using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of the implicit encoding pipeline (aggregate, bin and stack from the encoding) and of the marks and axes that draw its result
///     through the public API, with the original fixtures <c>aggregate-bars.vl.json</c>, <c>histogram.vl.json</c>, <c>stacked-bars.vl.json</c>,
///     <c>signed-stacks.vl.json</c>,
///     <c>binned-ranges.vl.json</c> and <c>window-cumulative.vl.json</c>. Every number is derived by hand from the data and the scale formulas.
/// </summary>
public class VegaLiteAggregationIntegrationTests
{
    /// <summary>The names of the fixtures of the implicit pipeline.</summary>
    private static readonly string[] Fixtures = ["aggregate-bars.vl.json", "histogram.vl.json", "stacked-bars.vl.json", "binned-ranges.vl.json", "window-cumulative.vl.json"];

    /// <summary>The default color of a mark.</summary>
    private static readonly Rgba32 Blue = new(0x4c, 0x78, 0xa8, 255);

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
    ///     Validates that the layers of the stacked bar chart are drawn from the start to the end of their stack, not from zero, and that a pixel inside
    ///     a lower layer has its color: the bands of 108 px start at 6 and 126, the y domain 0 to 10 is 100 px high (y(v) = 100 - 10 v), and the layers are bolts (blue),
    ///     nuts (orange) and washers (red) in the order of the sorted colors.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Stacking_LayersAreDrawnBetweenTheirStackBounds()
    {
        // Arrange: north is washers 0 to 5, nuts 5 to 7, bolts 7 to 10; south is nuts 0 to 1, bolts 1 to 5
        var document = Open("stacked-bars.vl.json");

        // Act
        var scene = document.BuildScene(new VegaLiteRenderOptions(), Ct);
        var bars = scene.FindAll("mark-bar").ToList();
        using var surface = document.Render(cancellationToken: Ct);

        // Assert: the rows are in first-seen order: north/bolts, north/nuts, north/washers, south/bolts, south/nuts
        var rects = bars.Select(b => (SceneRect)b.Item).ToList();
        Assert.Equal([6.0, 6.0, 6.0, 126.0, 126.0], rects.Select(r => r.X).ToArray());
        Assert.All(rects, r => Assert.Equal(108.0, r.Width, 9));
        Assert.Equal([0.0, 30.0, 50.0, 50.0, 90.0], rects.Select(r => r.Y).ToArray(), (a, b) => Math.Abs(a - b) < 1e-9);
        Assert.Equal([30.0, 20.0, 50.0, 40.0, 10.0], rects.Select(r => r.Height).ToArray(), (a, b) => Math.Abs(a - b) < 1e-9);
        Assert.Equal(["#4c78a8", "#f58518", "#e45756", "#4c78a8", "#f58518"], rects.Select(r => r.Fill).ToArray());

        // Assert: the layers do not overlap: the pixels at the middle of the nuts layer of north (y 40) and of the washers layer (y 75) have their own colors,
        // and the pixel above the south stack (y 20) is the background
        var (ox, oy) = ((int)Math.Floor(bars[0].OffsetX), (int)Math.Floor(bars[0].OffsetY));
        Assert.Equal(new Rgba32(0xf5, 0x85, 0x18, 255), surface[ox + 60, oy + 40]);
        Assert.Equal(new Rgba32(0xe4, 0x57, 0x56, 255), surface[ox + 60, oy + 75]);
        Assert.Equal(Blue, surface[ox + 180, oy + 70]);
        Assert.Equal(VegaLiteTestHelpers.White, surface[ox + 180, oy + 20]);
    }

    /// <summary>
    ///     Validates the binned histogram bars: the bins 0 to 35 by 5 over 280 px (8 px per unit) are 40 px wide; a bar fills its bin less a pixel of
    ///     spacing (half a pixel on each side) and the half pixel by which Vega translates the axis, so the bars start at 1, 41 and so on and are 39 wide.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Binning_HistogramBarsFillTheirBinsLessOnePixel()
    {
        // Act
        var document = Open("histogram.vl.json");
        var bars = Bars(document);

        // Assert
        Assert.Equal([1.0, 41.0, 81.0, 121.0, 161.0, 201.0, 241.0], bars.Select(b => b.X).ToArray());
        Assert.All(bars, b => Assert.Equal(39.0, b.Width, 9));
        // Assert: the x axis asks for a tick per ten pixels (28 for 280 px): d3 makes it a step of 1 over 0 to 35 (36 ticks), and the y axis has the four ticks 0 to 3
        Assert.Equal(36 + 4, document.BuildScene(new VegaLiteRenderOptions(), Ct).FindAll("axis-tick").Count());
    }

    /// <summary>
    ///     Validates the data that is binned already: three ranges (0 to 10, 10 to 25 and 25 to 30) over 150 px (5 px per unit, no zero and no nice) are
    ///     bars with a pixel of spacing (1 to 50, 51 to 125 and 126 to 150), the merged x title names both fields, the x axis has no grid and ticks every
    ///     ten units, and the pixels show the bars and the gap between two of them.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_PreBinned_RangesAreBarsOverTheirRanges()
    {
        // Arrange: the tickets 4, 7 and 2 have the domain 0 to 7, so y(v) = 100 - 100 v / 7
        var document = Open("binned-ranges.vl.json");

        // Act
        var scene = document.BuildScene(new VegaLiteRenderOptions(), Ct);
        var bars = scene.FindAll("mark-bar").ToList();
        var rects = bars.Select(b => (SceneRect)b.Item).ToList();
        using var surface = document.Render(cancellationToken: Ct);

        // Assert
        Assert.Equal([1.0, 51.0, 126.0], rects.Select(r => r.X).ToArray());
        Assert.Equal([49.0, 74.0, 24.0], rects.Select(r => r.Width).ToArray());
        Assert.Equal([400.0 / 7, 100.0, 200.0 / 7], rects.Select(r => r.Height).ToArray(), (a, b) => Math.Abs(a - b) < 1e-9);
        Assert.Equal(["from, to", "tickets"], Texts(document, "axis-title"));
        Assert.Equal(["0", "10", "20", "30", "0", "2", "4", "6"], Texts(document, "axis-label"));

        // Assert: the only grid lines are those of the four y ticks
        Assert.Equal(4, scene.FindAll("axis-grid").Count());

        // Assert: inside the first bar (x 25, y 90) is blue; in the pixel column between the first two bars (x 50, y 90) it is the background
        var (ox, oy) = ((int)Math.Floor(bars[0].OffsetX), (int)Math.Floor(bars[0].OffsetY));
        Assert.Equal(Blue, surface[ox + 25, oy + 90]);
        Assert.Equal(VegaLiteTestHelpers.White, surface[ox + 50, oy + 90]);
        Assert.Equal(Blue, surface[ox + 100, oy + 90]);
    }

    /// <summary>
    ///     Validates the running total of a <c>window</c> transform drawn as bars: the sums 4, 7, 12 and 14 on the domain 0 to 14 over 140 px (10 px per unit),
    ///     the four bands of 36 px that start at 2, 42, 82 and 122, and the title of the y axis that the specification gives.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Window_CumulativeTotalsAreBars()
    {
        // Act
        var document = Open("window-cumulative.vl.json");
        var parts = ChartTestHelpers.ResolveFull(Text("window-cumulative.vl.json"));
        var bars = Bars(document);

        // Assert
        Assert.Equal(["q1|4", "q2|7", "q3|12", "q4|14"], Transforms.AggregateTestSupport.Rows(parts.Data.Main, "quarter", "running"));
        Assert.Equal([2.0, 42.0, 82.0, 122.0], bars.Select(b => b.X).ToArray());
        Assert.All(bars, b => Assert.Equal(36.0, b.Width, 9));
        Assert.Equal([40.0, 70.0, 120.0, 140.0], bars.Select(b => b.Height).ToArray(), (a, b) => Math.Abs(a - b) < 1e-9);
        Assert.Equal([100.0, 70.0, 20.0, 0.0], bars.Select(b => b.Y).ToArray(), (a, b) => Math.Abs(a - b) < 1e-9);
        Assert.Equal(["quarter", "Orders so far"], Texts(document, "axis-title"));
    }

    /// <summary>
    ///     Validates the encoding sort by the aggregate of another channel through the public API, with the shorthands <c>y</c> and <c>-y</c> on the x channel: the
    ///     sums of w are c 1, b 10 and a 5 (in that first-seen order), so <c>y</c> orders the bands c, a, b and <c>-y</c> orders them b, a, c. The bands of a 150 px
    ///     wide view are 45 px wide and start at 2.5, 52.5 and 102.5 (step 50, inner padding 0.1, outer 0.05); the y domain 0 to 10 is 100 px high, so the sums
    ///     are 10, 100 and 50 px high.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_SortByAggregate_VerticalBarsFollowTheYShorthand()
    {
        // Arrange
        const string Template = """
            { "width": 150, "height": 100,
              "data": { "values": [ { "k": "c", "w": 1 }, { "k": "b", "w": 10 }, { "k": "a", "w": 2 }, { "k": "a", "w": 3 } ] },
              "mark": "bar",
              "encoding": { "x": { "field": "k", "type": "nominal", "sort": "SORT", "axis": { "labelAngle": 0 } },
                            "y": { "field": "w", "type": "quantitative", "aggregate": "sum" } } }
            """;

        foreach (var (sort, order, positions) in new[]
                 {
                     ("y", new[] { "c", "a", "b" }, new Dictionary<string, double> { ["c"] = 2.5, ["a"] = 52.5, ["b"] = 102.5 }),
                     ("-y", new[] { "b", "a", "c" }, new Dictionary<string, double> { ["b"] = 2.5, ["a"] = 52.5, ["c"] = 102.5 }),
                 })
        {
            var document = VegaLiteDocument.Parse(Template.Replace("SORT", sort, StringComparison.Ordinal));

            // Act
            var bars = Bars(document);
            var labels = Texts(document, "axis-label").Where(t => t is "a" or "b" or "c").ToArray();

            // Assert: the bars are in the first-seen order of the groups (c, b, a) and sit on the band of their category
            Assert.Equal(order, labels);
            Assert.Equal([positions["c"], positions["b"], positions["a"]], bars.Select(b => b.X).ToArray());
            Assert.Equal([10.0, 100.0, 50.0], bars.Select(b => b.Height).ToArray(), (a, b) => Math.Abs(a - b) < 1e-9);
            Assert.All(bars, b => Assert.Equal(45.0, b.Width, 9));
        }
    }

    /// <summary>
    ///     Validates the encoding sort by the aggregate of another channel on a horizontal bar chart: the shorthands <c>x</c> and <c>-x</c> on the y channel. The sums
    ///     are c 1, b 10 and a 5, so <c>x</c> orders the bands top to bottom c, a, b and <c>-x</c> orders them b, a, c; the bands of a 150 px high view are 45 px
    ///     high and start at 2.5, 52.5 and 102.5, and the x domain 0 to 10 over 200 px makes the bars 20, 200 and 100 px wide.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_SortByAggregate_HorizontalBarsFollowTheXShorthand()
    {
        // Arrange
        const string Template = """
            { "width": 200, "height": 150,
              "data": { "values": [ { "k": "c", "w": 1 }, { "k": "b", "w": 10 }, { "k": "a", "w": 2 }, { "k": "a", "w": 3 } ] },
              "mark": "bar",
              "encoding": { "y": { "field": "k", "type": "nominal", "sort": "SORT" },
                            "x": { "field": "w", "type": "quantitative", "aggregate": "sum" } } }
            """;

        foreach (var (sort, order, positions) in new[]
                 {
                     ("x", new[] { "c", "a", "b" }, new Dictionary<string, double> { ["c"] = 2.5, ["a"] = 52.5, ["b"] = 102.5 }),
                     ("-x", new[] { "b", "a", "c" }, new Dictionary<string, double> { ["b"] = 2.5, ["a"] = 52.5, ["c"] = 102.5 }),
                 })
        {
            var document = VegaLiteDocument.Parse(Template.Replace("SORT", sort, StringComparison.Ordinal));

            // Act
            var bars = Bars(document);
            var labels = Texts(document, "axis-label").Where(t => t is "a" or "b" or "c").ToArray();

            // Assert
            Assert.Equal(order, labels);
            Assert.Equal([positions["c"], positions["b"], positions["a"]], bars.Select(b => b.Y).ToArray());
            Assert.Equal([20.0, 200.0, 100.0], bars.Select(b => b.Width).ToArray(), (a, b) => Math.Abs(a - b) < 1e-9);
            Assert.All(bars, b => Assert.Equal(45.0, b.Height, 9));
        }
    }

    /// <summary>
    ///     Validates the sort object with an operation through the public API: the sums of w are c 1, a 5 and b 10, so the order is c, a, b ascending and b, a, c
    ///     descending.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_SortByAggregate_ASortObjectOrdersBySumInBothDirections()
    {
        // Arrange: the same chart sorted by the sum of w ascending and descending through the sort object
        const string Template = """
            { "width": 150, "height": 100,
              "data": { "values": [ { "k": "c", "w": 1 }, { "k": "b", "w": 10 }, { "k": "a", "w": 2 }, { "k": "a", "w": 3 } ] },
              "mark": "bar",
              "encoding": { "x": { "field": "k", "type": "nominal", "sort": { "op": "sum", "field": "w", "order": "ORDER" } },
                            "y": { "field": "w", "type": "quantitative", "aggregate": "sum" } } }
            """;

        // Act
        var ascending = Texts(VegaLiteDocument.Parse(Template.Replace("ORDER", "ascending", StringComparison.Ordinal)), "axis-label").Where(t => t is "a" or "b" or "c").ToArray();
        var descending = Texts(VegaLiteDocument.Parse(Template.Replace("ORDER", "descending", StringComparison.Ordinal)), "axis-label").Where(t => t is "a" or "b" or "c").ToArray();

        // Assert
        Assert.Equal(["c", "a", "b"], ascending);
        Assert.Equal(["b", "a", "c"], descending);
    }

    /// <summary>
    ///     Validates that the layers of a signed stack hang on both sides of the zero line, in both orientations, through the public API and the pixels. The
    ///     fixture has the layers tips 8 and sales 12 above zero in q1 (0 to 8, 8 to 20) and refunds -4 and costs -6 below it (0 to -4, -4 to -10); q2 is tips 3 and
    ///     sales 15 (0 to 3, 3 to 18) and refunds -8 and costs -12 (0 to -8, -8 to -20). The domain -20 to 20 over 100 px gives y(v) = 50 - 2.5 v, and the bands
    ///     are 108 px wide and start at 6 and 126. The negative layers (costs blue, refunds orange) are drawn only below the zero line, the positive layers (sales red,
    ///     tips teal) only above it. The transposed chart (the quarter on y, the stack along x over 100 px: x(v) = 50 + 2.5 v) stacks ascending: costs (0, -6),
    ///     refunds (-6, -10), sales (0, 12) and tips (12, 20) in q1.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Stacking_SignedLayersAreDrawnOnBothSidesOfZero()
    {
        // Arrange: tableau10 gives the sorted kinds costs, refunds, sales and tips these colors
        var costs = new Rgba32(0x4c, 0x78, 0xa8, 255);
        var refunds = new Rgba32(0xf5, 0x85, 0x18, 255);
        var sales = new Rgba32(0xe4, 0x57, 0x56, 255);
        var tips = new Rgba32(0x72, 0xb7, 0xb2, 255);
        var document = Open("signed-stacks.vl.json");

        // Act
        var scene = document.BuildScene(new VegaLiteRenderOptions(), Ct);
        var bars = scene.FindAll("mark-bar").ToList();
        var rects = bars.Select(b => (SceneRect)b.Item).ToList();
        using var surface = document.Render(cancellationToken: Ct);
        var (ox, oy) = ((int)Math.Floor(bars[0].OffsetX), (int)Math.Floor(bars[0].OffsetY));

        // Assert: the rows are in data order (q1: sales, tips, refunds, costs; q2 the same)
        Assert.Equal([6.0, 6.0, 6.0, 6.0, 126.0, 126.0, 126.0, 126.0], rects.Select(r => r.X).ToArray());
        Assert.Equal([0.0, 30.0, 50.0, 60.0, 5.0, 42.5, 50.0, 70.0], rects.Select(r => r.Y).ToArray(), (a, b) => Math.Abs(a - b) < 1e-9);
        Assert.Equal([30.0, 20.0, 10.0, 15.0, 37.5, 7.5, 20.0, 30.0], rects.Select(r => r.Height).ToArray(), (a, b) => Math.Abs(a - b) < 1e-9);

        // Assert: above the zero line of q1 there are only positive layers, below it only negative ones, and under the stack it is the background
        for (var y = 2; y <= 47; y++)
        {
            var pixel = surface[ox + 60, oy + y];
            Assert.True(pixel.Equals(sales) || pixel.Equals(tips), $"y {y} above zero is {pixel}");
        }

        for (var y = 52; y <= 73; y++)
        {
            var pixel = surface[ox + 60, oy + y];
            Assert.True(pixel.Equals(refunds) || pixel.Equals(costs), $"y {y} below zero is {pixel}");
        }

        Assert.Equal(refunds, surface[ox + 60, oy + 55]);
        Assert.Equal(costs, surface[ox + 60, oy + 67]);
        Assert.Equal(VegaLiteTestHelpers.White, surface[ox + 60, oy + 80]);
        Assert.Equal(costs, surface[ox + 180, oy + 90]);

        // Arrange: the same data on the other orientation
        var transposed = VegaLiteDocument.Parse(Text("signed-stacks.vl.json")
            .Replace("\"width\": 240", "\"width\": 100", StringComparison.Ordinal)
            .Replace("\"height\": 100", "\"height\": 240", StringComparison.Ordinal)
            .Replace("\"x\": { \"field\": \"quarter\", \"type\": \"nominal\", \"axis\": { \"labelAngle\": 0 } }", "\"y\": { \"field\": \"quarter\", \"type\": \"nominal\" }", StringComparison.Ordinal)
            .Replace("\"y\": { \"field\": \"amount\", \"type\": \"quantitative\" }", "\"x\": { \"field\": \"amount\", \"type\": \"quantitative\" }", StringComparison.Ordinal));

        // Act
        var acrossBars = transposed.BuildScene(new VegaLiteRenderOptions(), Ct).FindAll("mark-bar").ToList();
        var across = acrossBars.Select(b => (SceneRect)b.Item).ToList();
        using var acrossSurface = transposed.Render(cancellationToken: Ct);
        var (ax, ay) = ((int)Math.Floor(acrossBars[0].OffsetX), (int)Math.Floor(acrossBars[0].OffsetY));

        // Assert: q1 is sales (0, 12), tips (12, 20), refunds (-6, -10) and costs (0, -6); q2 is sales (0, 15), tips (15, 18), refunds (-12, -20) and costs (0, -12)
        Assert.Equal([6.0, 6.0, 6.0, 6.0, 126.0, 126.0, 126.0, 126.0], across.Select(r => r.Y).ToArray());
        Assert.Equal([50.0, 80.0, 25.0, 35.0, 50.0, 87.5, 0.0, 20.0], across.Select(r => r.X).ToArray(), (a, b) => Math.Abs(a - b) < 1e-9);
        Assert.Equal([30.0, 20.0, 10.0, 15.0, 37.5, 7.5, 20.0, 30.0], across.Select(r => r.Width).ToArray(), (a, b) => Math.Abs(a - b) < 1e-9);

        // Assert: along the row through the middle of q1 the negative colors are left of the zero line and the positive ones right of it
        for (var x = 2; x <= 47; x++)
        {
            var pixel = acrossSurface[ax + x, ay + 60];
            Assert.True(pixel.Equals(refunds) || pixel.Equals(costs) || pixel.Equals(VegaLiteTestHelpers.White), $"x {x} left of zero is {pixel}");
        }

        for (var x = 52; x <= 98; x++)
        {
            var pixel = acrossSurface[ax + x, ay + 60];
            Assert.True(pixel.Equals(sales) || pixel.Equals(tips), $"x {x} right of zero is {pixel}");
        }

        Assert.Equal(costs, acrossSurface[ax + 42, ay + 60]);
        Assert.Equal(refunds, acrossSurface[ax + 30, ay + 60]);

        // Assert: the rendering is deterministic
        using var again = document.Render(cancellationToken: Ct);
        Assert.Equal(VegaLiteTestHelpers.Snapshot(surface), VegaLiteTestHelpers.Snapshot(again));
    }

    /// <summary>
    ///     Validates that the marks of stacked and binned charts are counted by the scene budget (the exact number of items passes and one fewer fails with a
    ///     located failure) and that every chart is rendered twice to the same bytes.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_BinnedAndStacked_SceneBudgetAndDeterminismApply()
    {
        foreach (var name in new[] { "histogram.vl.json", "stacked-bars.vl.json", "binned-ranges.vl.json" })
        {
            // Arrange
            var document = Open(name);
            var count = document.BuildScene(new VegaLiteRenderOptions(), Ct).CountItems();

            // Act
            using var exact = document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = count } }, Ct);
            var tooFew = Assert.Throws<VegaLiteRenderException>(() => document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = count - 1 } }, Ct));
            using var first = document.Render(cancellationToken: Ct);
            using var second = document.Render(cancellationToken: Ct);

            // Assert
            Assert.True(exact.Width > 0);
            Assert.Contains("MaxSceneItems", tooFew.Message, StringComparison.Ordinal);
            Assert.Equal(VegaLiteTestHelpers.Snapshot(first), VegaLiteTestHelpers.Snapshot(second));
        }
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
