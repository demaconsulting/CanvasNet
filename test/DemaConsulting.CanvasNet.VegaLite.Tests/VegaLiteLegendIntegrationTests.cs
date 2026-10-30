using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of the legends (Phase 7): the legends of the original fixtures are found in the scene of a rendered
///     document, placed next to the data rectangle, stacked, merged and sized into the canvas as the rules of Vega-Lite's layout say. The
///     sizes of the legend boxes come from the unit-tested legend builder (the text is measured with the bundled font); every position and
///     every canvas size is derived by hand from the offset (18), the margin (8) and the padding (5).
/// </summary>
public class VegaLiteLegendIntegrationTests
{
    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Reads a fixture specification.
    /// </summary>
    /// <param name="name">The file name in the <c>Fixtures</c> folder.</param>
    /// <returns>The specification text.</returns>
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    /// <summary>
    ///     Builds the scene of a specification.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="options">The options, or null for the defaults.</param>
    /// <returns>The scene.</returns>
    private static Scene SceneOf(string json, VegaLiteRenderOptions? options = null) => VegaLiteDocument.Parse(json).BuildScene(options ?? new VegaLiteRenderOptions(), Ct);

    /// <summary>
    ///     Gets the legends of a scene with their positions in the scene.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The legend groups with their x and y positions on the canvas.</returns>
    private static List<(SceneGroup Group, double X, double Y)> LegendsOf(Scene scene) =>
        [.. scene.FindAll("legend").Select(e => (Assert.IsType<SceneGroup>(e.Item), e.OffsetX + ((SceneGroup)e.Item).X, e.OffsetY + ((SceneGroup)e.Item).Y))];

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
    ///     Gets the items of a role inside a legend.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="group">The legend group.</param>
    /// <param name="role">The role.</param>
    /// <returns>The items with the offsets of their parents relative to the legend origin.</returns>
    private static List<(T Item, double X, double Y)> Inside<T>(SceneGroup group, string role)
        where T : SceneItem =>
        [.. Scene.Enumerate(group).Where(e => e.Item.Role == role).Select(e => ((T)e.Item, e.OffsetX - group.X, e.OffsetY - group.Y))];

    /// <summary>
    ///     Replaces text in a specification, failing when the text is not there.
    /// </summary>
    /// <param name="json">The specification.</param>
    /// <param name="from">The text to find.</param>
    /// <param name="to">The replacement.</param>
    /// <returns>The changed specification.</returns>
    private static string Change(string json, string from, string to)
    {
        Assert.Contains(from, json, StringComparison.Ordinal);
        return json.Replace(from, to, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Removes the legends of the size and the color of the symbols fixture.
    /// </summary>
    /// <param name="json">The specification.</param>
    /// <returns>The specification without legends.</returns>
    private static string WithoutLegends(string json) =>
        Change(Change(json, "\"legend\": { \"title\": \"Crop\" }", "\"legend\": null"), "\"field\": \"trays\", \"type\": \"quantitative\"", "\"field\": \"trays\", \"type\": \"quantitative\", \"legend\": null");

    /// <summary>
    ///     Validates the symbols fixture: a color legend and a size legend in one column right of the data rectangle, 18 pixels from it,
    ///     8 pixels apart, with the explicit title of the color legend, and a canvas that is exactly as wide as the legends need.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Legend_SymbolLegendsStackRightOfTheDataRectangle()
    {
        // Arrange
        var json = Fixture("legend-symbols.vl.json");
        var color = ChartTestHelpers.Legend(json, 0, 200, 150, cancellationToken: Ct);
        var size = ChartTestHelpers.Legend(json, 1, 200, 150, cancellationToken: Ct);

        // Act
        var scene = SceneOf(json);
        var view = ViewOf(scene);
        var legends = LegendsOf(scene);

        // Assert: two legends at the offset of 18 from the right edge of the data rectangle, the second below the first plus the margin
        Assert.Equal(2, legends.Count);
        Assert.Equal([view.X + 200 + 18, view.X + 200 + 18], legends.Select(l => l.X));
        Assert.Equal([view.Y, view.Y + color.Height + 8], legends.Select(l => l.Y));
        Assert.Equal([color.Width, size.Width], legends.Select(l => l.Group.Width));
        Assert.Equal(["Crop", "trays"], legends.Select(l => Inside<SceneText>(l.Group, "legend-title").Single().Item.Text));

        // Assert: the entries are the three crops (symbols colored by the scheme of the category) and the ticks of the size scale
        var crops = Inside<SceneSymbol>(legends[0].Group, "legend-symbol");
        Assert.Equal(["#4c78a8", "#f58518", "#e45756"], crops.Select(s => s.Item.Fill));
        Assert.Equal(["beet", "kale", "leek"], Inside<SceneText>(legends[0].Group, "legend-label").Select(l => l.Item.Text));

        // Assert: the canvas is the padding, the data rectangle, the 18 pixels and the wider legend, and the padding; the legends together
        // are higher than the data rectangle (size entries up to 20 pixels high), so they reach below the x axis, and the canvas ends 5
        // pixels below whichever reaches further
        Assert.Equal(view.X + 200 + 18 + Math.Max(color.Width, size.Width) + 5, scene.Width);
        var baseline = SceneOf(WithoutLegends(json));
        var axisBottom = baseline.Height - 5 - ViewOf(baseline).Y - 150;
        var legendsBottom = color.Height + 8 + size.Height;
        Assert.True(legendsBottom > 150);
        Assert.Equal(view.Y + Math.Max(150 + axisBottom, legendsBottom) + 5, scene.Height);
        Assert.Equal(ViewOf(baseline).X, view.X);
    }

    /// <summary>
    ///     Validates that a legend that is switched off with <c>legend: null</c> is not drawn and does not change the size of the canvas.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Legend_NullSwitchesTheLegendOff()
    {
        // Arrange
        var json = Fixture("legend-symbols.vl.json");
        var plain = WithoutLegends(json);

        // Act
        var withLegends = SceneOf(json);
        var without = SceneOf(plain);

        // Assert: none is drawn, the canvas is narrower by the 18 pixels and the legend, less the pixel that the end of the axis reaches
        Assert.Empty(without.FindAll("legend"));
        Assert.True(withLegends.Width > without.Width + 18);
        Assert.Equal(without.Width - 5 - ViewOf(without).X, 200 + AxisReach(without));
    }

    /// <summary>
    ///     Gets how far the content of a scene reaches right of a data rectangle of 200 pixels (the half tick at the end of the x axis).
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The reach in pixels beyond 200.</returns>
    private static double AxisReach(Scene scene) => scene.Width - 5 - ViewOf(scene).X - 200;

    /// <summary>
    ///     Validates the gradient fixture: one gradient legend (a quantitative color with the default ramp) of 16 by 160 pixels (the length
    ///     of a vertical gradient is the height of the data rectangle) whose bar is painted with the ramp from the light end at the bottom to the
    ///     dark end at the top, with the title and the labels of the ends of the domain.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Legend_QuantitativeColorDrawsAGradient()
    {
        // Arrange
        var json = Fixture("legend-gradient.vl.json");
        var document = VegaLiteDocument.Parse(json);

        // Act
        var scene = document.BuildScene(new VegaLiteRenderOptions(), Ct);
        using var surface = document.Render(cancellationToken: Ct);
        var legend = Assert.Single(LegendsOf(scene));
        var bar = Inside<SceneRect>(legend.Group, "legend-gradient").Single();
        var gradient = bar.Item.FillGradient!;

        // Assert: the bar is 16 wide and as high as the data rectangle (160), the first stop is the light and the last the dark end of the ramp
        Assert.Equal((16.0, 160.0), (bar.Item.Width, bar.Item.Height));
        Assert.Equal((0.0, 1.0, 0.0, 0.0), (gradient.X1, gradient.Y1, gradient.X2, gradient.Y2));
        Assert.Equal(("#cfe1f2", "#0a4a90"), (gradient.Stops[0].Color, gradient.Stops[^1].Color));
        Assert.Equal((0.0, 1.0), (gradient.Stops[0].Offset, gradient.Stops[^1].Offset));

        // Assert: Vega's scaleGradient samples the ends of the domain (2.5 and 15) and the ticks between them (a step of 1: 3 to 15), so
        // the second stop is at (3 - 2.5) / 12.5
        Assert.Equal(0.04, gradient.Stops[1].Offset, 9);
        Assert.Equal("Rain (mm)", Inside<SceneText>(legend.Group, "legend-title").Single().Item.Text);

        // Assert: the painted bar is light at its bottom row and dark at its top row (the title and the padding are above the bar)
        var x = (int)Math.Floor(legend.X + bar.X + bar.Item.X + 8);
        var top = (int)Math.Floor(legend.Y + bar.Y + bar.Item.Y);
        AssertNear(surface[x, top + 1], (10, 74, 144), 12);
        AssertNear(surface[x, top + 158], (207, 225, 242), 12);
        AssertNear(surface[x, top + 80], (121, 169, 206), 40);
    }

    /// <summary>
    ///     Asserts that a pixel is near a color.
    /// </summary>
    /// <param name="pixel">The pixel.</param>
    /// <param name="expected">The expected color.</param>
    /// <param name="tolerance">The allowed difference of each channel.</param>
    private static void AssertNear(Rgba32 pixel, (int R, int G, int B) expected, int tolerance) =>
        Assert.True(
            Math.Abs(pixel.R - expected.R) <= tolerance && Math.Abs(pixel.G - expected.G) <= tolerance && Math.Abs(pixel.B - expected.B) <= tolerance,
            $"The pixel is {pixel.R},{pixel.G},{pixel.B}; expected about {expected.R},{expected.G},{expected.B}.");

    /// <summary>
    ///     Validates that a quantitative color field without any <c>legend</c> property (the specification of a scatter plot with a default
    ///     color ramp) gets a gradient legend, so the rendered chart explains its colors.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Legend_DefaultColorRampOfAScatterPlotHasAGradientLegend()
    {
        // Arrange
        const string json = """
            { "width": 120, "height": 100,
              "data": { "values": [ { "a": 1, "b": 2, "c": 10 }, { "a": 2, "b": 5, "c": 20 }, { "a": 3, "b": 4, "c": 40 } ] },
              "mark": "circle",
              "encoding": { "x": { "field": "a", "type": "quantitative" }, "y": { "field": "b", "type": "quantitative" },
                            "color": { "field": "c", "type": "quantitative" } } }
            """;

        // Act
        var scene = SceneOf(json);
        var legend = Assert.Single(LegendsOf(scene));

        // Assert: a gradient legend titled with the field, the labels at the ends of the domain (10 to 40, the length 100 has two ticks)
        Assert.Single(Inside<SceneRect>(legend.Group, "legend-gradient"));
        Assert.Equal("c", Inside<SceneText>(legend.Group, "legend-title").Single().Item.Text);
        Assert.Equal(["10", "40"], Inside<SceneText>(legend.Group, "legend-label").Select(l => l.Item.Text));
    }

    /// <summary>
    ///     Validates the merged fixture: the color and the shape that read the same field share one legend (each symbol has the shape and the
    ///     color of its category), the size has its own legend below it, and a shape on another field is not merged.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Legend_SharedFieldsMergeAndOthersStack()
    {
        // Arrange
        var json = Fixture("legend-merged.vl.json");
        var split = Change(json, "\"shape\": { \"field\": \"bed\", \"type\": \"nominal\" }", "\"shape\": { \"field\": \"weeds\", \"type\": \"ordinal\" }");
        var first = ChartTestHelpers.Legend(json, 0, 200, 120, cancellationToken: Ct);

        // Act
        var merged = LegendsOf(SceneOf(json));
        var separate = LegendsOf(SceneOf(split));

        // Assert: two legends, "bed" with both a shape and a color for each of the three beds, then "weeds" (size) 8 pixels below
        Assert.Equal(2, merged.Count);
        Assert.Equal(["bed", "weeds"], merged.Select(l => Inside<SceneText>(l.Group, "legend-title").Single().Item.Text));
        var symbols = Inside<SceneSymbol>(merged[0].Group, "legend-symbol");
        Assert.Equal(["#4c78a8", "#f58518", "#e45756"], symbols.Select(s => s.Item.Stroke));
        Assert.Equal(["circle", "square", "triangle-up"], symbols.Select(s => s.Item.Shape));
        Assert.Equal(["east", "north", "south"], Inside<SceneText>(merged[0].Group, "legend-label").Select(l => l.Item.Text));
        Assert.Equal(merged[0].Y + first.Height + 8, merged[1].Y);
        Assert.Equal(merged[0].X, merged[1].X);

        // Assert: the shape of another field (weeds, a different domain) is a legend of its own: three legends in all
        Assert.Equal(3, separate.Count);
        Assert.Equal(["bed", "weeds", "weeds"], separate.Select(l => Inside<SceneText>(l.Group, "legend-title").Single().Item.Text));
    }

    /// <summary>
    ///     Validates the orients of the layout in a rendered chart: a legend at the bottom is 18 pixels under the x axis and makes the canvas
    ///     higher, one at the top is 18 pixels above the data rectangle, and one at a corner is inside the data rectangle and leaves the canvas
    ///     as it was.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Legend_OrientsPlaceTheLegendAroundTheDataRectangle()
    {
        // Arrange: the size legend is switched off so that the color legend is the only one
        var json = Change(Fixture("legend-symbols.vl.json"), "\"field\": \"trays\", \"type\": \"quantitative\"", "\"field\": \"trays\", \"type\": \"quantitative\", \"legend\": null");
        var baseline = SceneOf(WithoutLegends(Fixture("legend-symbols.vl.json")));
        var withOrient = (string orient) => Change(json, "\"legend\": { \"title\": \"Crop\" }", "\"legend\": { \"title\": \"Crop\", \"orient\": \"" + orient + "\" }");
        var axis = ChartTestHelpers.Axis(json, "x", 200, 150) ?? throw new InvalidOperationException("The x axis is missing.");

        // Act
        var bottom = SceneOf(withOrient("bottom"));
        var top = SceneOf(withOrient("top"));
        var corner = SceneOf(withOrient("top-right"));
        var bottomLegend = Assert.Single(LegendsOf(bottom));
        var topLegend = Assert.Single(LegendsOf(top));
        var cornerLegend = Assert.Single(LegendsOf(corner));
        var box = ChartTestHelpers.Legend(withOrient("bottom"), 0, 200, 150, cancellationToken: Ct);
        var inner = ChartTestHelpers.Legend(withOrient("top-right"), 0, 200, 150, cancellationToken: Ct);

        // Assert: bottom: 18 below the axis (rounded up), at the left edge, and the canvas is as high as the legend ends plus the padding
        var bottomView = ViewOf(bottom);
        Assert.Equal((bottomView.X, bottomView.Y + Math.Ceiling(axis.Bounds.Y2) + 18), (bottomLegend.X, bottomLegend.Y));
        Assert.Equal(bottomLegend.Y + box.Height + 5, bottom.Height);
        Assert.Equal(baseline.Width, bottom.Width);

        // Assert: top: the legend ends 18 pixels above the data rectangle, so the data rectangle moved down by those and the legend height;
        // the width is that of the chart without legends
        var topView = ViewOf(top);
        Assert.Equal((topView.X, topView.Y - 18 - box.Height), (topLegend.X, topLegend.Y));
        Assert.Equal(baseline.Width, top.Width);
        Assert.Equal(baseline.Height - ViewOf(baseline).Y + topView.Y, top.Height);
        Assert.True(topView.Y > ViewOf(baseline).Y);

        // Assert: top-right: inside the data rectangle, 18 pixels from the top and the right edge; the canvas keeps its size
        var cornerView = ViewOf(corner);
        Assert.Equal((cornerView.X + 200 - 18 - inner.Width, cornerView.Y + 18), (cornerLegend.X, cornerLegend.Y));
        Assert.Equal((baseline.Width, baseline.Height), (corner.Width, corner.Height));
    }

    /// <summary>
    ///     Validates the configuration of the legend and of its layout: the orient and the offset of <c>config.legend</c> move the legends, the
    ///     properties of the encoding win over the configuration, and a fixed size does not grow with a legend (<c>autosize: none</c>).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Legend_ConfigurationAndAutosizeNone()
    {
        // Arrange
        var json = Change(Fixture("legend-symbols.vl.json"), "\"mark\": {", "\"config\": { \"legend\": { \"orient\": \"left\" } }, \"mark\": {");
        var pinned = Change(Fixture("legend-symbols.vl.json"), "\"mark\": {", "\"autosize\": \"none\", \"mark\": {");

        // Act
        var left = SceneOf(json);
        var overflowing = SceneOf(pinned);
        var plain = SceneOf(WithoutLegends(pinned));
        var legends = LegendsOf(left);

        // Assert: both legends are one column against the left axis: their left edges are aligned and the widest ends 18 pixels left of the
        // y axis, which reaches left of the data rectangle (the legends start left of the rounded-down edge of the axis)
        Assert.Equal(2, legends.Count);
        var view = ViewOf(left);
        var axisLeft = Math.Floor((ChartTestHelpers.Axis(json, "y", 200, 150) ?? throw new InvalidOperationException("The y axis is missing.")).Bounds.X1);
        Assert.Equal(legends[0].X, legends[1].X);
        Assert.Equal(view.X + axisLeft - 18, legends.Max(l => l.X + l.Group.Width));
        Assert.Equal(5 - (axisLeft - 18 - legends.Max(l => l.Group.Width)), view.X);

        // Assert: with autosize none the canvas is the one without legends and the legends stick out of it on the right
        Assert.Equal((plain.Width, plain.Height), (overflowing.Width, overflowing.Height));
        Assert.All(LegendsOf(overflowing), l => Assert.True(l.X + l.Group.Width > overflowing.Width));
    }

    /// <summary>
    ///     Validates the report and the strict mode of a legend property that is not drawn: it is reported once with its location (the legend
    ///     is still drawn), and strict mode turns it into an exception at the same place.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Legend_UnsupportedPropertiesAreReportedWithLocations()
    {
        // Arrange
        var json = Change(Fixture("legend-symbols.vl.json"), "\"legend\": { \"title\": \"Crop\" }", "\"legend\": { \"title\": \"Crop\", \"titleOrient\": \"left\" }");
        var document = VegaLiteDocument.Parse(json);
        var reported = new List<(string Feature, string Pointer)>();
        var options = new VegaLiteRenderOptions { UnsupportedFeatureHandler = u => reported.Add((u.Feature, u.Location.Pointer)) };

        // Act
        using var drawn = document.Render(options, Ct);
        var strict = Assert.Throws<VegaLiteUnsupportedFeatureException>(() => document.Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct));

        // Assert
        Assert.Equal([("vl-guide-legend-titleorient", "/encoding/color/legend/titleOrient")], reported);
        Assert.Equal("vl-guide-legend-titleorient", strict.Feature);
        Assert.Equal("/encoding/color/legend/titleOrient", strict.Location.Pointer);
        Assert.Equal(2, LegendsOf(SceneOf(json)).Count);
    }

    /// <summary>
    ///     Validates the limits and the cancellation of the legends: the scene count with the legends is exact (a limit of that count renders
    ///     and any smaller limit fails with the name of the limit), a legend that is not drawn does not count, and a canceled token stops the
    ///     render.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Legend_LimitsAndCancellationApply()
    {
        // Arrange
        var json = Fixture("legend-merged.vl.json");
        var document = VegaLiteDocument.Parse(json);
        var count = document.BuildScene(new VegaLiteRenderOptions(), Ct).CountItems();
        var without = VegaLiteDocument.Parse(Change(Change(json, "\"field\": \"weeds\", \"type\": \"quantitative\"", "\"field\": \"weeds\", \"type\": \"quantitative\", \"legend\": null"), "\"shape\": { \"field\": \"bed\", \"type\": \"nominal\" }", "\"shape\": { \"field\": \"bed\", \"type\": \"nominal\", \"legend\": null }"));
        var legendItems = LegendsOf(document.BuildScene(new VegaLiteRenderOptions(), Ct)).Sum(l => Scene.Enumerate(l.Group).Count());
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        // Act
        using var exact = document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = count } }, Ct);
        var tooFew = Assert.Throws<VegaLiteRenderException>(() => document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = count - 1 } }, Ct));
        var onlyLegends = Assert.Throws<VegaLiteRenderException>(() => document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = legendItems } }, Ct));

        // Assert: the legend of a symbol and a color that is switched off does not draw; the others do
        Assert.True(exact.Width > 0);
        Assert.Contains("MaxSceneItems", tooFew.Message, StringComparison.Ordinal);
        Assert.Contains("MaxSceneItems", onlyLegends.Message, StringComparison.Ordinal);
        Assert.True(without.BuildScene(new VegaLiteRenderOptions(), Ct).CountItems() < count);
        Assert.Throws<OperationCanceledException>(() => document.Render(cancellationToken: canceled.Token));
    }

    /// <summary>
    ///     Validates that the legends are deterministic: two renders of a document are byte-identical, and so are renders on several threads.
    /// </summary>
    [Fact]
    public async Task CanvasNetVegaLite_SystemIntegration_Legend_RendersAreDeterministicAcrossThreads()
    {
        // Arrange
        var document = VegaLiteDocument.Parse(Fixture("legend-merged.vl.json"));
        using var first = document.Render(cancellationToken: Ct);
        var expected = VegaLiteTestHelpers.Snapshot(first);

        // Act
        var renders = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(
            () =>
            {
                using var surface = document.Render(cancellationToken: Ct);
                return VegaLiteTestHelpers.Snapshot(surface);
            },
            Ct)));

        // Assert
        Assert.All(renders, r => Assert.Equal(expected, r));
    }
}
