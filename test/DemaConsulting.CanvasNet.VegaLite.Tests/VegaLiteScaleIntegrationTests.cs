using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of the log, pow, sqrt and symlog scales (Phase 7): charts of the original fixture and of small inline
///     specifications are rendered through the public API and checked by the positions of marks, the ticks and labels of the axes, the legends
///     and sample pixels, with every number derived by hand from the scale formulas (<c>ln x</c> over the nice domain, the signed square
///     root, the symmetric log) and the layout of the chart.
/// </summary>
public class VegaLiteScaleIntegrationTests
{
    /// <summary>The default color of a mark.</summary>
    private static readonly Rgba32 Blue = new(0x4c, 0x78, 0xa8, 255);

    /// <summary>The names of the categories of the log fixture, which the axis labels of the x axis show.</summary>
    private static readonly string[] Days = ["mon", "tue", "wed", "thu"];

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
    ///     Validates the log fixture: four bars on a log y axis whose domain is rounded out to the powers 10 to 10000 (three decades of 100 pixels), the
    ///     bars start at the bottom and rise by the logarithm of their value, the axis has the multiples of every decade as ticks with only
    ///     the labels whose leading digit is 1, 2 or 3, and the bars are drawn in their color over the grid.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_LogScale_BarsRiseByTheLogarithmOfTheirValue()
    {
        // Arrange: the counts 12, 95, 1200 and 8000 give the domain 12 to 8000; nice floors the log of 12 to 10 and ceils the log of 8000 to
        // 10000, so the three decades have 100 pixels each and a bar of v reaches y = 300 - 100 * (log10(v) - 1)
        var document = VegaLiteDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "log-scales.vl.json"));
        var scene = document.BuildScene(new VegaLiteRenderOptions(), Ct);
        var view = ViewOf(scene);
        var bars = scene.FindAll("mark-bar").Select(e => (SceneRect)e.Item).ToList();

        // Act
        using var surface = document.Render(cancellationToken: Ct);

        // Assert: four bars from the tops of the logarithm to the bottom of the data rectangle (the baseline is the start of the domain)
        Assert.Equal(4, bars.Count);
        double[] values = [12, 95, 1200, 8000];
        for (var i = 0; i < values.Length; i++)
        {
            Assert.Equal(300 - (100 * (Math.Log10(values[i]) - 1)), bars[i].Y, 6);
            Assert.Equal(300, bars[i].Y + bars[i].Height, 6);
        }

        // Assert: the y axis has 28 ticks (10 to 90, 100 to 900, 1000 to 9000 and 10000) and labels the digits 1, 2 and 3 (k = 10 * 10 / 28 = 3.57)
        var yLabels = Texts(scene, "axis-label").Where(t => t.Length > 0 && !Days.Contains(t)).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(
            new[] { "10", "20", "30", "100", "200", "300", "1,000", "2,000", "3,000", "10,000" }.Order(StringComparer.Ordinal),
            yLabels);
        Assert.Equal(["Cells counted", "Day"], Texts(scene, "axis-title").Order(StringComparer.Ordinal));
        Assert.Equal(28, scene.FindAll("axis-grid").Count());

        // Assert: the bars are opaque over the grid at their centers; between the grid lines above a bar the plot is the background
        var ox = view.X;
        var oy = view.Y;
        foreach (var bar in bars)
        {
            Assert.Equal(Blue, surface[(int)(ox + bar.X + (bar.Width / 2)), (int)(oy + bar.Y + (bar.Height / 2))]);
        }

        int[] rowsAbove = [281, 192, 85, 7];
        for (var i = 0; i < bars.Count; i++)
        {
            Assert.Equal(VegaLiteTestHelpers.White, surface[(int)(ox + bars[i].X + (bars[i].Width / 2)), (int)(oy + rowsAbove[i])]);
        }
    }

    /// <summary>
    ///     Validates that data that reaches zero cannot be drawn on a log axis: the render fails with a located error that says what to do,
    ///     where a symlog scale draws the same data, with the zero bar flat on the baseline.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_LogScale_ZeroInTheDataIsALocatedErrorAndSymlogDrawsIt()
    {
        // Arrange: the first count of the fixture becomes zero
        var withZero = Fixture("log-scales.vl.json").Replace("\"cells\": 12 }", "\"cells\": 0 }", StringComparison.Ordinal);
        Assert.NotEqual(Fixture("log-scales.vl.json"), withZero);
        var document = VegaLiteDocument.Parse(withZero);

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => document.Render(cancellationToken: Ct));
        var symlog = VegaLiteDocument.Parse(withZero.Replace("\"type\": \"log\"", "\"type\": \"symlog\"", StringComparison.Ordinal));
        var bars = symlog.BuildScene(new VegaLiteRenderOptions(), Ct).FindAll("mark-bar").Select(e => (SceneRect)e.Item).ToList();

        // Assert: the error is at the y channel, with its place in the text, and names the domain and the remedy
        Assert.Equal("/encoding/y", ex.Location.Pointer);
        Assert.True(ex.Location.Line > 0 && ex.Location.Column > 0);
        Assert.Contains("includes zero or crosses it ([0, 8000])", ex.Message, StringComparison.Ordinal);
        Assert.Contains("symlog", ex.Message, StringComparison.Ordinal);

        // Assert: the symlog scale takes the zero: four bars, the first has no height (it is at the baseline)
        Assert.Equal(4, bars.Count);
        Assert.Equal(0, bars[0].Height, 6);
        Assert.All(bars.Skip(1), bar => Assert.True(bar.Height > 0));
    }

    /// <summary>
    ///     Validates the square root and symmetric log scales on a scatter plot: points sit at the signed square root of x over 0 to 100 on 200
    ///     pixels and at the symmetric log of y over the nice domain -10 to 10 on 100 pixels, as the d3 formulas give.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_SqrtAndSymlogScales_PointsSitAtTheTransformedPositions()
    {
        // Arrange: x is 0, 25, 100 and 49 (sqrt 0, 5, 10 and 7 of 10 over 200 pixels); y is -9, 0, 1 and 9 on a symlog axis whose nice
        // domain is -10 to 10 (ticks of step 2), so T(y) = sign(y) * ln(1 + |y|) over +-ln(11)
        const string json = """
            { "width": 200, "height": 100,
              "data": { "values": [ { "x": 0, "y": -9 }, { "x": 25, "y": 0 }, { "x": 100, "y": 1 }, { "x": 49, "y": 9 } ] },
              "mark": "point",
              "encoding": { "x": { "field": "x", "type": "quantitative", "scale": { "type": "sqrt" } },
                            "y": { "field": "y", "type": "quantitative", "scale": { "type": "symlog" } } } }
            """;
        var scene = VegaLiteDocument.Parse(json).BuildScene(new VegaLiteRenderOptions(), Ct);
        var view = ViewOf(scene);

        // Act
        var points = scene.FindAll("mark-point")
            .Select(e => (X: e.OffsetX + ((SceneSymbol)e.Item).X - view.X, Y: e.OffsetY + ((SceneSymbol)e.Item).Y - view.Y))
            .ToList();

        // Assert: x by the square root, y by the symmetric log (the origin is at the top, so larger values are higher)
        double[] xs = [0, 25, 100, 49];
        double[] ys = [-9, 0, 1, 9];
        Assert.Equal(4, points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            var t = (Math.Sign(ys[i]) * Math.Log(1 + Math.Abs(ys[i])) / (2 * Math.Log(11))) + 0.5;
            Assert.Equal(200 * Math.Sqrt(xs[i]) / 10, points[i].X, 6);
            Assert.Equal(100 * (1 - t), points[i].Y, 6);
        }

        // Assert: the sqrt axis has the ordinary ticks (a count of ceil(200 / 40) = 5 over 0 to 100 gives a step of 20) at the square roots 0, 89, 127,
        // 155, 179 and 200 pixels; the last two labels overlap (the flush "100" starts at 183, the "80" ends at 184), so the parity method removes every
        // other label and 0, 40 and 80 stay; the symlog axis has those of -10 to 10 for a count of ceil(100 / 40) = 3 (20 / 3 = 6.7 rounds to a step
        // of 5), which are far enough apart to stay
        Assert.Equal(
            string.Join("|", new[] { "0", "40", "80", "\u221210", "\u22125", "0", "5", "10" }.Order(StringComparer.Ordinal)),
            string.Join("|", Texts(scene, "axis-label").Order(StringComparer.Ordinal)));
    }

    /// <summary>
    ///     Validates the legend of a log color scale in a rendered chart: a gradient legend whose labels are the powers of ten, placed along the
    ///     logarithm of the bar, and that a log scale reports nothing in strict mode.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_LogScale_ColorLegendLabelsThePowers()
    {
        // Arrange: four readings 1, 10, 100 and 1000 colored on a log scale
        const string json = """
            { "width": 200, "height": 220,
              "data": { "values": [ { "a": 1, "b": 1, "c": 1 }, { "a": 2, "b": 3, "c": 10 }, { "a": 3, "b": 2, "c": 100 }, { "a": 4, "b": 4, "c": 1000 } ] },
              "mark": "circle",
              "encoding": { "x": { "field": "a", "type": "quantitative" }, "y": { "field": "b", "type": "quantitative" },
                            "color": { "field": "c", "type": "quantitative", "scale": { "type": "log" }, "legend": { "title": "Reading" } } } }
            """;
        var reports = new List<string>();
        var options = new VegaLiteRenderOptions
        {
            ThrowOnUnsupportedFeature = true,
            UnsupportedFeatureHandler = u => reports.Add(u.Feature),
            WarningHandler = w => reports.Add(w.Message),
        };

        // Act: strict mode would throw for any report
        var scene = VegaLiteDocument.Parse(json).BuildScene(options, Ct);
        var legend = Assert.IsType<SceneGroup>(Assert.Single(scene.FindAll("legend")).Item);
        var labels = Scene.Enumerate(legend).Where(e => e.Item.Role == "legend-label").Select(e => (SceneText)e.Item).ToList();

        // Assert: nothing was reported, the legend is a gradient with the powers as labels, a third of the bar apart (the bar is 200 pixels long:
        // a data rectangle of 220 is clamped to 200, so a count of 4 asks for the powers, as the unit tests of the legend show)
        Assert.Empty(reports);
        Assert.Single(Scene.Enumerate(legend), e => e.Item.Role == "legend-gradient");
        Assert.Equal(["1", "10", "100", "1,000"], labels.Select(l => l.Text));
        Assert.Equal(
            new[] { 200.0, 200 * (1 - (1.0 / 3)), 200 * (1 - (2.0 / 3)), 0.0 }.Select(v => Math.Round(v, 9)),
            labels.Select(l => Math.Round(l.Y, 9)));
    }

    /// <summary>
    ///     Validates the limits, the cancellation and the determinism of log charts: more ticks than the limit allows is a located error that
    ///     names the limit, a canceled token stops the render, and repeated and concurrent renders give the same pixels.
    /// </summary>
    [Fact]
    public async Task CanvasNetVegaLite_SystemIntegration_LogScale_LimitsCancellationAndDeterminismApply()
    {
        // Arrange
        var document = VegaLiteDocument.Parse(Fixture("log-scales.vl.json"));

        // Act: the 28 ticks of the axis against limits of 27 and 28, and a canceled token
        var tooMany = Assert.Throws<VegaLiteRenderException>(() => document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxTicks = 5 } }, Ct));
        using var exact = document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxTicks = 28 } }, Ct);
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();
        using var first = document.Render(cancellationToken: Ct);
        using var second = document.Render(cancellationToken: Ct);
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => VegaLiteTestHelpers.Snapshot(document.Render(cancellationToken: Ct)), Ct)));

        // Assert: the limit error names the limit and the place of the y channel; exactly 28 ticks are allowed
        Assert.Contains("the limit of 5", tooMany.Message, StringComparison.Ordinal);
        Assert.Equal("/encoding/y", tooMany.Location.Pointer);
        Assert.Equal(first.Width, exact.Width);
        Assert.Throws<OperationCanceledException>(() => document.Render(cancellationToken: canceled.Token));

        // Assert: the pixels are the same on every render, also when they run at the same time
        var expected = VegaLiteTestHelpers.Snapshot(first);
        Assert.Equal(expected, VegaLiteTestHelpers.Snapshot(second));
        Assert.All(concurrent, snapshot => Assert.Equal(expected, snapshot));
    }
}
