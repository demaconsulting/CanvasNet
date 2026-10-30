using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of binned fields on the channels other than <c>x</c> and <c>y</c> and of the bin-ordinal scale (Phase 7): charts of the
///     original fixture and of small inline specifications are rendered through the public API and checked by the bins, the colors of the marks, the range
///     labels of the legends and sample pixels, with every number derived by hand from Vega's bin algorithm (the values 3 to 97 with six bins have the step 20
///     over 0 to 100) and the layout of the chart.
/// </summary>
public class VegaLiteBinnedChannelIntegrationTests
{
    /// <summary>The five colors of the fixture, one per bin, as pixels.</summary>
    private static readonly Rgba32[] BinPixels =
    [
        new(0xd7, 0x30, 0x27, 255),
        new(0xfc, 0x8d, 0x59, 255),
        new(0xfe, 0xe0, 0x8b, 255),
        new(0x91, 0xcf, 0x60, 255),
        new(0x1a, 0x98, 0x50, 255),
    ];

    /// <summary>The five colors of the fixture, one per bin, as text.</summary>
    private static readonly string[] BinColors = ["#d73027", "#fc8d59", "#fee08b", "#91cf60", "#1a9850"];

    /// <summary>The range labels of the five bins.</summary>
    private static readonly string[] BinLabels = ["0 \u2013 20", "20 \u2013 40", "40 \u2013 60", "60 \u2013 80", "80 \u2013 100"];

    /// <summary>The bin of each of the eight readings of the fixture (3, 12, 27, 41, 58, 66, 83 and 97).</summary>
    private static readonly int[] BinOfReading = [0, 0, 1, 2, 2, 3, 4, 4];

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Reads a fixture specification.
    /// </summary>
    /// <param name="name">The file name in the <c>Fixtures</c> folder.</param>
    /// <returns>The specification text.</returns>
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    /// <summary>
    ///     Gets the position of the view (the data rectangle) on the canvas.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The x and y position of the data rectangle.</returns>
    private static (double X, double Y) ViewOf(Scene scene)
    {
        var view = scene.Find("view") ?? throw new InvalidOperationException("The scene has no view.");
        var group = Assert.IsType<SceneGroup>(view.Item);
        return (view.OffsetX + group.X, view.OffsetY + group.Y);
    }

    /// <summary>
    ///     Gets the texts of the items of a role in a scene.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role.</param>
    /// <returns>The texts in drawing order.</returns>
    private static List<string> Texts(Scene scene, string role) => [.. scene.FindAll(role).Select(e => ((SceneText)e.Item).Text)];

    /// <summary>
    ///     Validates the fixture: eight circles whose positions follow the linear axes, colored by the bin of their moisture through a bin-ordinal scale (the
    ///     readings 3 and 12 share the first color, 41 and 58 the third, 83 and 97 the last), with a legend of five entries labeled with the range of the bin
    ///     and titled as a binned field, every number derived by hand, in strict mode, and the colors confirmed by pixels of the marks and of the legend.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_BinnedColor_CirclesTakeTheColorOfTheirBinAndTheLegendListsTheRanges()
    {
        // Arrange: strict mode would throw for any unsupported feature; a warning would be collected
        var document = VegaLiteDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "binned-channels.vl.json"));
        var warnings = new List<string>();
        var options = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, WarningHandler = w => warnings.Add(w.Message) };
        var scene = document.BuildScene(options, Ct);
        var view = ViewOf(scene);
        var circles = scene.FindAll("mark-circle").Select(e => (SceneSymbol)e.Item).ToList();

        // Act
        using var surface = document.Render(options, Ct);

        // Assert: nothing was reported; the day 1 to 8 is on the x axis 0 to 8 (25 pixels a day) and the height 1 to 5 on the y axis 0 to 5 (20 pixels a unit)
        Assert.Empty(warnings);
        Assert.Equal(8, circles.Count);
        double[] heights = [1, 3, 2, 4, 2, 5, 1, 3];
        for (var i = 0; i < circles.Count; i++)
        {
            Assert.Equal(25 * (i + 1), circles[i].X, 6);
            Assert.Equal(100 - (20 * heights[i]), circles[i].Y, 6);
            Assert.Equal(BinColors[BinOfReading[i]], circles[i].Fill);
        }

        // Assert: the legend has one symbol and one range label per bin (five, not the six edges) in the colors of the bins, and the title of a binned field
        var legendSymbols = scene.FindAll("legend-symbol").ToList();
        Assert.Equal(BinLabels, Texts(scene, "legend-label"));
        Assert.Equal(BinColors, legendSymbols.Select(e => ((SceneSymbol)e.Item).Fill));
        Assert.Equal(["moisture (binned)"], Texts(scene, "legend-title"));
        Assert.Empty(scene.FindAll("legend-gradient"));

        // Assert: the circles are painted in the colors of their bins at the 0.7 opacity of a filled circle over the white background (sampled 2 pixels right of and
        // below the center, which the grid lines through every center do not reach, well inside the circle), and so are the legend symbols
        for (var i = 0; i < circles.Count; i++)
        {
            var px = (int)Math.Round(view.X + circles[i].X) + 2;
            var py = (int)Math.Round(view.Y + circles[i].Y) + 2;
            Assert.True(IsNear(OverWhite(BinPixels[BinOfReading[i]]), surface[px, py]), $"circle {i} at {view.X + circles[i].X},{view.Y + circles[i].Y}: expected {OverWhite(BinPixels[BinOfReading[i]])} but the pixel ({px},{py}) is {surface[px, py]}");
        }

        for (var bin = 0; bin < legendSymbols.Count; bin++)
        {
            var symbol = (SceneSymbol)legendSymbols[bin].Item;
            AssertNear(OverWhite(BinPixels[bin]), surface[(int)Math.Round(legendSymbols[bin].OffsetX + symbol.X), (int)Math.Round(legendSymbols[bin].OffsetY + symbol.Y)]);
        }
    }

    /// <summary>
    ///     Gets the color a mark of the opacity 0.7 gives over the white background.
    /// </summary>
    /// <param name="color">The opaque color.</param>
    /// <returns>The blended color (as the exact arithmetic gives it; the painter may differ by one in a channel).</returns>
    private static Rgba32 OverWhite(Rgba32 color) =>
        new((byte)Math.Round((0.7 * color.R) + (0.3 * 255)), (byte)Math.Round((0.7 * color.G) + (0.3 * 255)), (byte)Math.Round((0.7 * color.B) + (0.3 * 255)), 255);

    /// <summary>
    ///     Asserts that two colors differ by at most one in every channel (the rounding of the blend).
    /// </summary>
    /// <param name="expected">The expected color.</param>
    /// <param name="actual">The painted color.</param>
    private static void AssertNear(Rgba32 expected, Rgba32 actual) =>
        Assert.True(IsNear(expected, actual), $"expected {expected} but the pixel is {actual}");

    /// <summary>
    ///     Tells whether two colors differ by at most one in every channel and the second is opaque.
    /// </summary>
    /// <param name="expected">The expected color.</param>
    /// <param name="actual">The painted color.</param>
    /// <returns><see langword="true"/> when the colors are as near as the rounding of a blend allows.</returns>
    private static bool IsNear(Rgba32 expected, Rgba32 actual) =>
        Math.Abs(actual.R - expected.R) <= 1 && Math.Abs(actual.G - expected.G) <= 1 && Math.Abs(actual.B - expected.B) <= 1 && actual.A == 255;

    /// <summary>
    ///     Validates a histogram whose bars are colored by the bin they count (a bin of <c>x</c> and a bin of <c>color</c> with the same parameters are the same
    ///     bin): five bars with the counts 2, 1, 2, 1 and 2 on a count axis of 0 to 2, the bin axis from 0 to 100 by 20, the colors in bin order, the range
    ///     legend, and the colors confirmed by pixels at the middle of the bars.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_BinnedColor_HistogramBarsAreColoredByTheirOwnBin()
    {
        // Arrange: the fixture's readings as a histogram (a bar chart of the count per bin)
        const string json = """
            { "width": 200, "height": 100,
              "data": { "values": [ { "m": 3 }, { "m": 12 }, { "m": 27 }, { "m": 41 }, { "m": 58 }, { "m": 66 }, { "m": 83 }, { "m": 97 } ] },
              "mark": "bar",
              "encoding": {
                "x": { "field": "m", "type": "quantitative", "bin": { "maxbins": 5 } },
                "y": { "aggregate": "count", "type": "quantitative" },
                "color": { "field": "m", "type": "quantitative", "bin": { "maxbins": 5 },
                           "scale": { "range": ["#d73027", "#fc8d59", "#fee08b", "#91cf60", "#1a9850"] } } } }
            """;
        var document = VegaLiteDocument.Parse(json);
        var scene = document.BuildScene(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct);
        var view = ViewOf(scene);
        var bars = scene.FindAll("mark-bar").Select(e => (SceneRect)e.Item).ToList();

        // Act
        using var surface = document.Render(cancellationToken: Ct);

        // Assert: five bars of 200 / 5 = 40 pixels less the bin spacing of 1, the counts 2, 1, 2, 1, 2 on the domain 0 to 2 (100 pixels for 2)
        Assert.Equal(5, bars.Count);
        Assert.Equal([2.0, 1, 2, 1, 2], bars.Select(b => Math.Round(b.Height / 50, 9)));
        Assert.Equal(BinColors, bars.Select(b => b.Fill));
        Assert.Equal([1.0, 41, 81, 121, 161], bars.Select(b => Math.Round(b.X, 9)));
        Assert.Equal(Enumerable.Repeat(39.0, 5), bars.Select(b => Math.Round(b.Width, 9)));

        // Assert: the bin axis is 0 to 100 by 20 (ticks of the bin step), the count axis 0 to 2 by 0.5 (d3 ticks(0, 2, 3)), and the titles are those of the binned field and the count
        Assert.Equal(
            new[] { "0", "20", "40", "60", "80", "100", "0.0", "0.5", "1.0", "1.5", "2.0" }.Order(StringComparer.Ordinal),
            Texts(scene, "axis-label").Where(t => t.Length > 0).Order(StringComparer.Ordinal));
        Assert.Equal(["Count of Records", "m (binned)"], Texts(scene, "axis-title").Order(StringComparer.Ordinal));
        Assert.Equal(BinLabels, Texts(scene, "legend-label"));

        // Assert: the middle of each bar is its bin's color
        for (var i = 0; i < bars.Count; i++)
        {
            Assert.Equal(BinPixels[i], surface[(int)(view.X + bars[i].X + (bars[i].Width / 2)), (int)(view.Y + bars[i].Y + (bars[i].Height / 2))]);
        }
    }

    /// <summary>
    ///     Validates the other binned channels: a binned size reads the start of the bin through a linear scale over the bins (0 to 100), so two readings in
    ///     one bin have one size and the legend shows the ticks of the scale; an ordinal binned opacity gives the legend the text of every bin.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_BinnedSizeAndOpacity_ReadTheBinsOfTheirChannel()
    {
        // Arrange: the readings of the fixture, as points sized by the binned moisture and (in a second chart) shaded by its text
        const string Rows = """
            "data": { "values": [
              { "day": 1, "height": 1, "moisture": 3 }, { "day": 2, "height": 3, "moisture": 12 }, { "day": 3, "height": 2, "moisture": 27 }, { "day": 4, "height": 4, "moisture": 41 },
              { "day": 5, "height": 2, "moisture": 58 }, { "day": 6, "height": 5, "moisture": 66 }, { "day": 7, "height": 1, "moisture": 83 }, { "day": 8, "height": 3, "moisture": 97 } ] },
            """;
        const string Position = """
            "x": { "field": "day", "type": "quantitative" }, "y": { "field": "height", "type": "quantitative" },
            """;
        var sized = "{ \"width\": 200, \"height\": 100, " + Rows + " \"mark\": \"point\", \"encoding\": { " + Position + " \"size\": { \"field\": \"moisture\", \"type\": \"quantitative\", \"bin\": true } } }";
        var shaded = "{ \"width\": 200, \"height\": 100, " + Rows + " \"mark\": \"point\", \"encoding\": { " + Position + " \"opacity\": { \"field\": \"moisture\", \"type\": \"ordinal\", \"bin\": true } } }";
        var sizedScene = VegaLiteDocument.Parse(sized).BuildScene(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct);
        var shadedScene = VegaLiteDocument.Parse(shaded).BuildScene(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct);

        // Act
        var sizes = sizedScene.FindAll("mark-point").Select(e => ((SceneSymbol)e.Item).Size).ToList();

        // Assert: size = 4 + 357 * start / 100 for the starts 0, 0, 20, 40, 40, 60, 80, 80
        double[] starts = [0, 0, 20, 40, 40, 60, 80, 80];
        Assert.Equal(starts.Select(start => Math.Round(4 + (357 * start / 100), 9)), sizes.Select(s => Math.Round(s, 9)));

        // Assert: the size legend shows the ticks 0 to 100 by 20 under the title of a binned field; the opacity legend shows the text of the five bins
        Assert.Equal(["0", "20", "40", "60", "80", "100"], Texts(sizedScene, "legend-label"));
        Assert.Equal(["moisture (binned)"], Texts(sizedScene, "legend-title"));
        Assert.Equal(BinLabels, Texts(shadedScene, "legend-label"));
    }

    /// <summary>
    ///     Validates the diagnostics around bins: <c>bin: "binned"</c> on a color channel is reported with its location (and strict mode turns it into an
    ///     exception there) while the chart is drawn as an unbinned color, and an explicit bin-ordinal scale type on a field that is not binned is a located
    ///     warning that leaves the default continuous scale (a gradient legend).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_BinnedColor_DiagnosticsAreLocated()
    {
        // Arrange
        var fixture = Fixture("binned-channels.vl.json");
        var alreadyBinned = fixture.Replace("\"bin\": true", "\"bin\": \"binned\"", StringComparison.Ordinal);
        var ignored = fixture.Replace("\"bin\": true,", string.Empty, StringComparison.Ordinal).Replace("\"scale\": { \"range\"", "\"scale\": { \"type\": \"bin-ordinal\", \"range\"", StringComparison.Ordinal);
        Assert.NotEqual(fixture, alreadyBinned);
        Assert.NotEqual(fixture, ignored);
        var reported = new List<(string Feature, string Pointer)>();
        var warnings = new List<(string Message, string Pointer)>();
        var options = new VegaLiteRenderOptions
        {
            UnsupportedFeatureHandler = u => reported.Add((u.Feature, u.Location.Pointer)),
            WarningHandler = w => warnings.Add((w.Message, w.Location.Pointer)),
        };

        // Act
        var binnedDocument = VegaLiteDocument.Parse(alreadyBinned);
        using var binnedSurface = binnedDocument.Render(options, Ct);
        var binnedScene = binnedDocument.BuildScene(new VegaLiteRenderOptions(), Ct);
        var strict = Assert.Throws<VegaLiteUnsupportedFeatureException>(() => binnedDocument.Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct));
        var ignoredDocument = VegaLiteDocument.Parse(ignored);
        using var ignoredSurface = ignoredDocument.Render(options, Ct);
        var ignoredScene = ignoredDocument.BuildScene(new VegaLiteRenderOptions(), Ct);

        // Assert: "binned" on a color is reported once, with the place of the value, and the chart still draws its eight circles
        Assert.Equal([("vl-encoding-bin", "/encoding/color/bin")], reported);
        Assert.Equal("vl-encoding-bin", strict.Feature);
        Assert.Equal("/encoding/color/bin", strict.Location.Pointer);
        Assert.True(strict.Location.Line > 0 && strict.Location.Column > 0);
        Assert.Equal(8, binnedScene.FindAll("mark-circle").Count());

        // Assert: the unbinned field with a bin-ordinal type warns at the type and gets the gradient of the default continuous scale
        var warning = Assert.Single(warnings);
        Assert.Equal("/encoding/color/scale/type", warning.Pointer);
        Assert.Contains("bin-ordinal", warning.Message, StringComparison.Ordinal);
        Assert.Single(ignoredScene.FindAll("legend-gradient"));
    }

    /// <summary>
    ///     Validates the limits, the cancellation and the determinism of binned charts: more bins than <c>MaxScaleDomainSize</c> is a located error at the
    ///     <c>bin</c> that names the limit, a limit that allows the bins renders, the scene count is exact, a canceled token stops the render, and repeated and
    ///     concurrent renders give the same pixels.
    /// </summary>
    [Fact]
    public async Task CanvasNetVegaLite_SystemIntegration_BinnedColor_LimitsCancellationAndDeterminismApply()
    {
        // Arrange
        var document = VegaLiteDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "binned-channels.vl.json"));
        var count = document.BuildScene(new VegaLiteRenderOptions(), Ct).CountItems();
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();

        // Act: six bins asked for against limits of five and six, and the scene count against itself and one less
        var tooMany = Assert.Throws<VegaLiteRenderException>(() => document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxScaleDomainSize = 5 } }, Ct));
        using var allowed = document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxScaleDomainSize = 6 } }, Ct);
        using var exact = document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = count } }, Ct);
        var tooFew = Assert.Throws<VegaLiteRenderException>(() => document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = count - 1 } }, Ct));
        using var first = document.Render(cancellationToken: Ct);
        using var second = document.Render(cancellationToken: Ct);
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => VegaLiteTestHelpers.Snapshot(document.Render(cancellationToken: Ct)), Ct)));

        // Assert: the limit errors name their limits and the places
        Assert.Equal("/encoding/color/bin", tooMany.Location.Pointer);
        Assert.Contains("MaxScaleDomainSize", tooMany.Message, StringComparison.Ordinal);
        Assert.Contains("MaxSceneItems", tooFew.Message, StringComparison.Ordinal);
        Assert.Equal(first.Width, allowed.Width);
        Assert.Equal(first.Height, exact.Height);
        Assert.Throws<OperationCanceledException>(() => document.Render(cancellationToken: canceled.Token));

        // Assert: the pixels are the same on every render, also when they run at the same time
        var expected = VegaLiteTestHelpers.Snapshot(first);
        Assert.Equal(expected, VegaLiteTestHelpers.Snapshot(second));
        Assert.All(concurrent, snapshot => Assert.Equal(expected, snapshot));
    }
}
