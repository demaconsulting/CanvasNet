using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Positions;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     Tests of the <see cref="VegaLiteRenderOptions.PositionHandler"/> contract of <see cref="VegaLiteDocument"/>: the
///     guides and marks a chart reports, their view paths, ids and values, the mapping by scale and fit, the paint at bar
///     centers, and that positions never change the pixels or come from <c>Measure</c>.
/// </summary>
public class VegaLiteDocumentPositionTests
{
    /// <summary>A bar chart with a title.</summary>
    private const string Bars =
        "{\"title\":\"Crates\",\"data\":{\"values\":[{\"stall\":\"A\",\"crates\":28},{\"stall\":\"B\",\"crates\":55},{\"stall\":\"C\",\"crates\":43}]}," +
        "\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"stall\",\"type\":\"nominal\"},\"y\":{\"field\":\"crates\",\"type\":\"quantitative\"}}}";

    /// <summary>The default color of a mark.</summary>
    private static readonly Rgba32 MarkBlue = new(0x4C, 0x78, 0xA8, 255);

    /// <summary>
    ///     Renders a chart and collects its positions.
    /// </summary>
    /// <param name="json">The specification.</param>
    /// <param name="kindFilter">The kind filter, or null.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>The surface and the positions.</returns>
    private static (Surface Surface, List<ImagePosition> Positions) Render(string json, Func<string, bool>? kindFilter = null, double scale = 1)
    {
        var positions = new List<ImagePosition>();
        var surface = VegaLiteDocument.Parse(json).Render(
            new VegaLiteRenderOptions { PositionHandler = positions.Add, PositionKindFilter = kindFilter, Scale = scale },
            TestContext.Current.CancellationToken);
        return (surface, positions);
    }

    /// <summary>
    ///     Validates the guides and bars of a single view: the order, the ids, the values and the paint at each bar center.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_BarChart_ReportsGuidesAndBars()
    {
        // Act
        var (surface, positions) = Render(Bars);
        using var _ = surface;

        // Assert: titles, views, axes, then marks
        Assert.Equal(["title", "view", "axis", "axis", "mark", "mark", "mark"], positions.Select(p => p.Kind));
        Assert.Equal(["title", "", "x", "y", "bar-0", "bar-1", "bar-2"], positions.Select(p => p.Id));
        Assert.All(positions, p => Assert.Equal(string.Empty, p.View));
        Assert.Equal("Crates", positions[0].Text);
        Assert.Equal("stall", positions[2].Text);
        var bar = positions[6];
        Assert.True(bar.TryGetValue("x", out var x));
        Assert.True(bar.TryGetValue("y", out var y));
        Assert.Equal((PositionValue.FromString("C"), PositionValue.FromNumber(43)), (x, y));
        foreach (var mark in positions.Where(p => p.Kind == "mark"))
        {
            Assert.Equal(MarkBlue, surface[(int)mark.Pixel!.Value.X, (int)mark.Pixel.Value.Y]);
            Assert.Equal(mark.Box!.Value.Center, mark.Pixel);
        }
    }

    /// <summary>
    ///     Validates that an aggregate is keyed by its channel and a temporal value is UTC ISO text, and that a line is
    ///     one position per vertex.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_AggregateAndTemporalLine_ValuesAndVertices()
    {
        // Arrange
        const string Line =
            "{\"data\":{\"values\":[{\"d\":\"2025-01-01\",\"v\":3},{\"d\":\"2025-01-01\",\"v\":5},{\"d\":\"2025-01-02\",\"v\":4}]}," +
            "\"mark\":\"line\",\"encoding\":{\"x\":{\"field\":\"d\",\"type\":\"temporal\"},\"y\":{\"aggregate\":\"sum\",\"field\":\"v\",\"type\":\"quantitative\"}}}";

        // Act
        var (surface, positions) = Render(Line, kind => kind == "mark");
        using var _ = surface;

        // Assert
        Assert.Equal(["line-0", "line-1"], positions.Select(p => p.Id));
        Assert.True(positions[0].TryGetValue("x", out var x));
        Assert.True(positions[0].TryGetValue("y", out var y));
        Assert.Equal(PositionValue.FromString("2025-01-01T00:00:00.000Z"), x);
        Assert.Equal(PositionValue.FromNumber(8), y);
        Assert.Equal(0, positions[0].Box!.Value.Width);
    }

    /// <summary>
    ///     Validates the view paths of a concatenation with layers and names, and the facet values of facet cells.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_CompositionPaths_AreCanonical()
    {
        // Arrange
        const string Concat =
            "{\"data\":{\"values\":[{\"a\":\"A\",\"b\":3},{\"a\":\"B\",\"b\":5}]},\"vconcat\":[" +
            "{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"nominal\"},\"y\":{\"field\":\"b\",\"type\":\"quantitative\"}}}," +
            "{\"name\":\"lower\",\"layer\":[{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"nominal\"},\"y\":{\"field\":\"b\",\"type\":\"quantitative\"}}}," +
            "{\"mark\":\"point\",\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"nominal\"},\"y\":{\"field\":\"b\",\"type\":\"quantitative\"}}}]}]}";
        const string Facet =
            "{\"data\":{\"values\":[{\"a\":\"A\",\"b\":3,\"c\":\"x\"},{\"a\":\"B\",\"b\":5,\"c\":\"y\"}]},\"mark\":\"bar\"," +
            "\"encoding\":{\"column\":{\"field\":\"c\"},\"x\":{\"field\":\"a\",\"type\":\"nominal\"},\"y\":{\"field\":\"b\",\"type\":\"quantitative\"}}}";

        // Act
        var (concatSurface, concat) = Render(Concat);
        var (facetSurface, facet) = Render(Facet);
        using var _ = concatSurface;
        using var __ = facetSurface;

        // Assert
        Assert.Equal(["vconcat[0]", "vconcat[1]"], concat.Where(p => p.Kind == "view").Select(p => p.Id));
        Assert.Equal("lower", concat.Single(p => p is { Kind: "view", Id: "vconcat[1]" }).ViewName);
        Assert.Contains(concat, p => p is { Kind: "axis", Id: "y", View: "vconcat[1]" });
        Assert.Contains(concat, p => p is { Kind: "mark", Id: "point-1", View: "vconcat[1].layer[1]" });
        var cell = facet.Single(p => p is { Kind: "mark", View: "facet[1]" });
        Assert.True(cell.TryGetValue("column", out var column));
        Assert.Equal(PositionValue.FromString("y"), column);
    }

    /// <summary>
    ///     Validates that the marks a composite mark expands to belong to its view, and that a stacked bar reports the
    ///     value of its row and a bin its start and end.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_BoxplotStackAndBin_ValuesAndViews()
    {
        // Arrange
        const string Boxplot =
            "{\"data\":{\"values\":[{\"a\":\"A\",\"b\":3},{\"a\":\"A\",\"b\":5},{\"a\":\"A\",\"b\":9}]},\"mark\":\"boxplot\"," +
            "\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"nominal\"},\"y\":{\"field\":\"b\",\"type\":\"quantitative\"}}}";
        const string Stack =
            "{\"data\":{\"values\":[{\"a\":\"A\",\"b\":3,\"c\":\"x\"},{\"a\":\"A\",\"b\":5,\"c\":\"y\"}]},\"mark\":\"bar\"," +
            "\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"nominal\"},\"y\":{\"field\":\"b\",\"type\":\"quantitative\"},\"color\":{\"field\":\"c\"}}}";
        const string Bin =
            "{\"data\":{\"values\":[{\"b\":3},{\"b\":15}]},\"mark\":\"bar\",\"encoding\":{\"x\":{\"bin\":true,\"field\":\"b\"},\"y\":{\"aggregate\":\"count\"}}}";

        // Act
        var (boxSurface, box) = Render(Boxplot, kind => kind == "mark");
        var (stackSurface, stack) = Render(Stack, kind => kind == "mark");
        var (binSurface, bin) = Render(Bin, kind => kind == "mark");
        using var _ = boxSurface;
        using var __ = stackSurface;
        using var ___ = binSurface;

        // Assert
        Assert.All(box, p => Assert.Equal(string.Empty, p.View));
        Assert.Contains(box, p => p.Mark == "rule");
        Assert.Contains(box, p => p.Mark == "tick");
        Assert.Equal([3.0, 5.0], stack.Select(p => p.TryGetValue("y", out var y) ? y.NumberValue : double.NaN));
        Assert.True(bin[0].TryGetValue("x", out var start));
        Assert.True(bin[0].TryGetValue("x2", out var end));
        Assert.True(end.NumberValue > start.NumberValue);
    }

    /// <summary>
    ///     Validates the user's month time-unit line chart: <c>x</c> is the start of the month in Vega-Lite's 2012 base
    ///     year, <c>data</c> keeps the source row (the date as the data wrote it, no time-unit helper fields), and a
    ///     selector on data fields picks the November Americas point while one on the channels with the source date
    ///     matches nothing.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_MonthTimeUnitLine_DataKeepsTheSourceRow()
    {
        // Arrange
        const string Line =
            "{\"data\":{\"values\":[" +
            "{\"month\":\"2025-10-01\",\"region\":\"Americas\",\"revenue\":6.1},{\"month\":\"2025-11-01\",\"region\":\"Americas\",\"revenue\":6.8}," +
            "{\"month\":\"2025-10-01\",\"region\":\"Europe\",\"revenue\":4.2},{\"month\":\"2025-11-01\",\"region\":\"Europe\",\"revenue\":4.9}]}," +
            "\"mark\":{\"type\":\"line\",\"point\":true},\"encoding\":{" +
            "\"x\":{\"field\":\"month\",\"timeUnit\":\"month\",\"type\":\"temporal\"}," +
            "\"y\":{\"field\":\"revenue\",\"type\":\"quantitative\"},\"color\":{\"field\":\"region\",\"type\":\"nominal\"}}}";

        // Act
        var (surface, positions) = Render(Line, kind => kind == "mark");
        using var _ = surface;
        var selected = positions.Where(PositionSelector.Parse("mark:month=2025-11-01+region=Americas").Matches).ToList();
        var byChannel = PositionSelector.Parse("mark:x=2025-11-01+color=Americas");

        // Assert: the line vertex and its point, both with the source row as data
        Assert.Equal(["line", "point"], selected.Select(p => p.Mark));
        Assert.Equal(selected[0].Pixel, selected[1].Pixel);
        foreach (var position in selected)
        {
            Assert.Equal(["month", "region", "revenue"], position.Data.Select(pair => pair.Key));
            Assert.True(position.TryGetData("month", out var month));
            Assert.Equal(PositionValue.FromString("2025-11-01"), month);
            Assert.True(position.TryGetData("revenue", out var revenue));
            Assert.Equal(PositionValue.FromNumber(6.8), revenue);
            Assert.True(position.TryGetValue("x", out var x));
            Assert.Equal(PositionValue.FromString("2012-11-01T00:00:00.000Z"), x);
            Assert.Contains("x", (IReadOnlySet<string>)position.ChannelNames!);
        }

        Assert.DoesNotContain(positions, byChannel.Matches);
    }

    /// <summary>
    ///     Validates the documented content of <c>data</c>: a stacked aggregate has its group fields and measures (no stack
    ///     fields), a chart with a <c>calculate</c> keeps the computed field and leaves nested objects, arrays and bin
    ///     fields out, and a wide row gives the first fields by name, up to the cap.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_AggregatedNestedAndWideRows_DataAsDocumented()
    {
        // Arrange
        const string Stacked =
            "{\"data\":{\"values\":[{\"m\":\"2025-10-01\",\"r\":\"A\",\"v\":1},{\"m\":\"2025-10-15\",\"r\":\"A\",\"v\":2},{\"m\":\"2025-10-01\",\"r\":\"B\",\"v\":4}]}," +
            "\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"m\",\"timeUnit\":\"month\",\"type\":\"ordinal\"}," +
            "\"y\":{\"aggregate\":\"sum\",\"field\":\"v\",\"type\":\"quantitative\"},\"color\":{\"field\":\"r\"}}}";
        const string Nested =
            "{\"data\":{\"values\":[{\"a\":\"A\",\"b\":3,\"meta\":{\"k\":1},\"tags\":[1,2],\"ok\":true,\"none\":null}]}," +
            "\"transform\":[{\"calculate\":\"datum.b * 2\",\"as\":\"twice\"}],\"mark\":\"point\"," +
            "\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"nominal\"},\"y\":{\"field\":\"b\",\"bin\":true}}}";
        var wideRow = string.Join(",", Enumerable.Range(0, 40).Select(i => $"\"f{i:D2}\":{i}"));
        var wide = "{\"data\":{\"values\":[{" + wideRow + "}]},\"mark\":\"point\",\"encoding\":{\"x\":{\"field\":\"f39\",\"type\":\"quantitative\"}}}";

        // Act
        var (stackedSurface, stacked) = Render(Stacked, kind => kind == "mark");
        var (nestedSurface, nested) = Render(Nested, kind => kind == "mark");
        var (wideSurface, wideMarks) = Render(wide, kind => kind == "mark");
        using var _ = stackedSurface;
        using var __ = nestedSurface;
        using var ___ = wideSurface;

        // Assert: the aggregated row, with the time unit's group field by its derived name
        Assert.Equal(2, stacked.Count);
        Assert.All(stacked, p => Assert.Equal(["month_m", "r", "sum_v"], p.Data.Select(pair => pair.Key)));
        Assert.True(stacked.Single(PositionSelector.Parse("mark:r=A").Matches).TryGetData("sum_v", out var sum));
        Assert.Equal(PositionValue.FromNumber(3), sum);

        // Assert: the row after the transforms, scalars only, without the bin fields
        Assert.Equal(["a", "b", "none", "ok", "twice"], nested.Single().Data.Select(pair => pair.Key));
        Assert.True(nested.Single().TryGetData("twice", out var twice));
        Assert.Equal(PositionValue.FromNumber(6), twice);
        Assert.True(nested.Single().TryGetData("none", out var none));
        Assert.Equal(PositionValue.Null, none);

        // Assert: the first fields by name, up to the cap
        Assert.Equal(
            Enumerable.Range(0, ImagePosition.MaxDataFields).Select(i => $"f{i:D2}"),
            wideMarks.Single().Data.Select(pair => pair.Key));
    }

    /// <summary>
    ///     Validates that an arc's pixel is painted with its slice color, and that legends report their channel.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_ArcAndLegend_AimInsideTheSlice()
    {
        // Arrange
        const string Arc =
            "{\"data\":{\"values\":[{\"a\":\"A\",\"b\":3},{\"a\":\"B\",\"b\":5}]},\"mark\":\"arc\"," +
            "\"encoding\":{\"theta\":{\"field\":\"b\",\"type\":\"quantitative\"},\"color\":{\"field\":\"a\",\"type\":\"nominal\"}}}";

        // Act
        var (surface, positions) = Render(Arc);
        using var _ = surface;

        // Assert
        Assert.Equal("color", positions.Single(p => p.Kind == "legend").Id);
        var arcs = positions.Where(p => p.Kind == "mark").ToList();
        var first = surface[(int)arcs[0].Pixel!.Value.X, (int)arcs[0].Pixel!.Value.Y];
        var second = surface[(int)arcs[1].Pixel!.Value.X, (int)arcs[1].Pixel!.Value.Y];
        Assert.Equal(MarkBlue, first);
        Assert.NotEqual(first, second);
    }

    /// <summary>
    ///     Validates that scale 2 doubles every coordinate, that the exact size maps by the fit, that the handler never
    ///     changes the pixels, and that <c>Measure</c> never calls it.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_ScaleFitAndPixels_MapWithoutChangingPixels()
    {
        // Arrange
        var document = VegaLiteDocument.Parse(Bars);
        var measured = new List<ImagePosition>();
        var fitted = new List<ImagePosition>();

        // Act
        var (one, natural) = Render(Bars);
        var (two, doubled) = Render(Bars, scale: 2);
        using var plain = document.Render(cancellationToken: TestContext.Current.CancellationToken);
        using var exact = document.Render(one.Width * 2, one.Height * 2, new VegaLiteRenderOptions { PositionHandler = fitted.Add }, TestContext.Current.CancellationToken);
        document.Measure(new VegaLiteRenderOptions { PositionHandler = measured.Add }, TestContext.Current.CancellationToken);
        using var _ = one;
        using var __ = two;

        // Assert
        Assert.Equal(VegaLiteTestHelpers.Snapshot(plain), VegaLiteTestHelpers.Snapshot(one));
        Assert.Empty(measured);
        for (var index = 0; index < natural.Count; index++)
        {
            Assert.Equal(natural[index].Pixel!.Value.X * 2, doubled[index].Pixel!.Value.X, 6);
            Assert.Equal(natural[index].Box!.Value.Height * 2, doubled[index].Box!.Value.Height, 6);
            Assert.Equal(doubled[index].Pixel!.Value.X, fitted[index].Pixel!.Value.X, 6);
        }
    }

    /// <summary>
    ///     Validates that the mark items record their rows only when a handler wants marks.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_BuildScene_RecordsRowsOnlyForAHandlerThatWantsMarks()
    {
        // Arrange
        var document = VegaLiteDocument.Parse(Bars);

        // Act
        var plain = document.BuildScene(new VegaLiteRenderOptions(), TestContext.Current.CancellationToken);
        var guidesOnly = document.BuildScene(new VegaLiteRenderOptions { PositionHandler = _ => { }, PositionKindFilter = kind => kind != "mark" }, TestContext.Current.CancellationToken);
        var recorded = document.BuildScene(new VegaLiteRenderOptions { PositionHandler = _ => { } }, TestContext.Current.CancellationToken);

        // Assert
        Assert.All(plain.FindAll("mark-bar"), entry => Assert.Equal(-1, entry.Item.Datum));
        Assert.All(guidesOnly.FindAll("mark-bar"), entry => Assert.Equal(-1, entry.Item.Datum));
        Assert.Equal([0, 1, 2], recorded.FindAll("mark-bar").Select(entry => entry.Item.Datum));
        Assert.Null(((SceneGroup)plain.FindAll("marks").Single().Item).Marks);
        Assert.NotNull(((SceneGroup)recorded.FindAll("marks").Single().Item).Marks);
    }

    /// <summary>
    ///     Validates that an exception thrown by the handler propagates.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_HandlerThrows_Propagates()
    {
        // Arrange
        var options = new VegaLiteRenderOptions { PositionHandler = _ => throw new InvalidOperationException("stop") };

        // Act / Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            VegaLiteDocument.Parse(Bars).Render(options, TestContext.Current.CancellationToken));
        Assert.Equal("stop", exception.Message);
    }

    /// <summary>
    ///     Validates that the title of a single view and of a layered view reports the <c>name</c> of the view, as its
    ///     data rectangle does.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_NamedViewTitle_ReportsTheViewName()
    {
        // Arrange
        const string Single =
            "{\"name\":\"stalls\",\"title\":\"Crates\",\"data\":{\"values\":[{\"stall\":\"A\",\"crates\":28}]}," +
            "\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"stall\",\"type\":\"nominal\"},\"y\":{\"field\":\"crates\",\"type\":\"quantitative\"}}}";
        const string Layered =
            "{\"name\":\"overlay\",\"title\":\"Crates\",\"data\":{\"values\":[{\"stall\":\"A\",\"crates\":28}]}," +
            "\"encoding\":{\"x\":{\"field\":\"stall\",\"type\":\"nominal\"},\"y\":{\"field\":\"crates\",\"type\":\"quantitative\"}}," +
            "\"layer\":[{\"mark\":\"bar\"},{\"mark\":\"point\"}]}";

        // Act
        var (singleSurface, single) = Render(Single, kind => kind is "title" or "view");
        var (layeredSurface, layered) = Render(Layered, kind => kind is "title" or "view");
        using var _ = singleSurface;
        using var __ = layeredSurface;

        // Assert
        foreach (var (positions, name) in new[] { (single, "stalls"), (layered, "overlay") })
        {
            var title = Assert.Single(positions, p => p.Kind == "title");
            Assert.Equal("Crates", title.Text);
            Assert.Equal(string.Empty, title.View);
            Assert.Equal(name, title.ViewName);
            Assert.Equal(name, Assert.Single(positions, p => p.Kind == "view").ViewName);
        }
    }

    /// <summary>
    ///     Validates that an image mark that omits its width, or both its width and its height, reports the rectangle the
    ///     picture is drawn in (sized from the loaded picture) as its box, with the pixel at its center, and that the
    ///     picture is loaded once.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_ImageMarkWithoutSize_ReportsTheDrawnBox()
    {
        // Arrange: one picture centered at (100, 50); the resolver returns a 40 by 10 raster whatever size it is asked for
        const string Template =
            "{\"width\":200,\"height\":100,\"padding\":0,\"data\":{\"values\":[{\"x\":5,\"y\":5,\"u\":\"red.png\"}]}," +
            "\"mark\":{\"type\":\"image\"SIZE}," +
            "\"encoding\":{\"x\":{\"field\":\"x\",\"type\":\"quantitative\",\"scale\":{\"domain\":[0,10]},\"axis\":null}," +
            "\"y\":{\"field\":\"y\",\"type\":\"quantitative\",\"scale\":{\"domain\":[0,10]},\"axis\":null}," +
            "\"url\":{\"field\":\"u\",\"type\":\"nominal\"}}}";

        // Act / Assert: a height of 20 keeps the aspect ratio of 4 (80 by 20); no size is the natural 40 by 10
        AssertDrawnBox(Template.Replace("SIZE", ",\"height\":20", StringComparison.Ordinal), new PositionBox(60, 40, 80, 20));
        AssertDrawnBox(Template.Replace("SIZE", string.Empty, StringComparison.Ordinal), new PositionBox(80, 45, 40, 10));

        static void AssertDrawnBox(string json, PositionBox expected)
        {
            var red = new Rgba32(255, 0, 0, 255);
            var white = new Rgba32(255, 255, 255, 255);
            var resolver = new RasterResolver(40, 10, red);
            var positions = new List<ImagePosition>();
            using var surface = VegaLiteDocument.Parse(json).Render(
                new VegaLiteRenderOptions { ImageResolver = resolver, PositionHandler = positions.Add, PositionKindFilter = kind => kind == "mark" },
                TestContext.Current.CancellationToken);

            var image = Assert.Single(positions);
            Assert.Equal("image-0", image.Id);
            Assert.Equal(expected, image.Box);
            Assert.Equal(new PositionPoint(100, 50), image.Pixel);
            Assert.Equal(1, resolver.Calls);

            // The box is the drawn rectangle: the picture covers its corners and stops at its edges
            var (left, top) = ((int)expected.X, (int)expected.Y);
            var (right, bottom) = ((int)(expected.X + expected.Width), (int)(expected.Y + expected.Height));
            Assert.Equal(red, surface[left, top]);
            Assert.Equal(red, surface[right - 1, bottom - 1]);
            Assert.Equal(white, surface[left - 1, top]);
            Assert.Equal(white, surface[right, bottom - 1]);
            Assert.Equal(white, surface[left, top - 1]);
            Assert.Equal(white, surface[right - 1, bottom]);
        }
    }

    /// <summary>
    ///     A resolver that returns a raster of one color and a fixed size, whatever size it is asked for, and counts its calls.
    /// </summary>
    /// <param name="width">The width of the raster.</param>
    /// <param name="height">The height of the raster.</param>
    /// <param name="color">The color of the raster.</param>
    private sealed class RasterResolver(int width, int height, Rgba32 color) : IImageResolver
    {
        /// <summary>Gets the number of calls.</summary>
        public int Calls { get; private set; }

        /// <inheritdoc />
        public Surface? Resolve(string href, int pixelWidth, int pixelHeight)
        {
            Calls++;
            var surface = new Surface(width, height);
            surface.Clear(color);
            return surface;
        }
    }
}
