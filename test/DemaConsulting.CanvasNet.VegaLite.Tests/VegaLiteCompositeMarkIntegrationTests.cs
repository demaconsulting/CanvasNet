using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of the composite marks (Phase 10), here the <c>boxplot</c>: the chart is rendered through the public API and checked by the positions
///     of the marks, with the quartiles derived by hand. Two groups are used: group <c>A</c> holds 1 to 10 and 30 (eleven values: first quartile 3.5, median 6, third quartile
///     8.5 by the linear interpolation of Vega's quartiles, fences at -4 and 16 for the default extent of 1.5, so the whiskers reach 1 and 10 and 30 is an outlier) and group
///     <c>B</c> holds 11 to 15 (quartiles 12, 13 and 14, whiskers 11 and 15). The measure runs over a domain of 0 to 30, 10 pixels for each unit of a 300 pixel data rectangle,
///     and each group is a band of 20 pixels.
/// </summary>
public class VegaLiteCompositeMarkIntegrationTests
{
    /// <summary>The data of the two groups.</summary>
    private const string BoxData = """
        "data": { "values": [
            { "g": "A", "v": 1 }, { "g": "A", "v": 2 }, { "g": "A", "v": 3 }, { "g": "A", "v": 4 }, { "g": "A", "v": 5 }, { "g": "A", "v": 6 },
            { "g": "A", "v": 7 }, { "g": "A", "v": 8 }, { "g": "A", "v": 9 }, { "g": "A", "v": 10 }, { "g": "A", "v": 30 },
            { "g": "B", "v": 11 }, { "g": "B", "v": 12 }, { "g": "B", "v": 13 }, { "g": "B", "v": 14 }, { "g": "B", "v": 15 }
          ] }
        """;

    /// <summary>The encoding of a vertical box plot.</summary>
    private const string VerticalEncoding = """
        "encoding": { "x": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } }
        """;

    /// <summary>The encoding of a horizontal box plot.</summary>
    private const string HorizontalEncoding = """
        "encoding": { "y": { "field": "g", "type": "nominal" }, "x": { "field": "v", "type": "quantitative" } }
        """;

    /// <summary>The encoding of a vertical box plot whose groups are also colored.</summary>
    private const string ColorEncoding = """
        "encoding": { "x": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" }, "color": { "field": "g", "type": "nominal" } }
        """;

    /// <summary>The encoding of a box plot whose groups are a binned field.</summary>
    private const string BinnedEncoding = """
        "encoding": { "x": { "field": "v", "type": "quantitative", "bin": true }, "y": { "field": "v", "type": "quantitative" } }
        """;

    /// <summary>The encoding of a box plot that has no continuous channel.</summary>
    private const string NominalEncoding = """
        "encoding": { "x": { "field": "g", "type": "nominal" }, "y": { "field": "g", "type": "ordinal" } }
        """;

    /// <summary>A configuration of the box plot.</summary>
    private const string BoxConfig = """
        "config": { "boxplot": { "size": 8, "median": { "color": "red" } } },
        """;

    /// <summary>A transform that leaves out the outlier.</summary>
    private const string FilterTransform = """
        "transform": [ { "filter": "datum.v < 20" } ],
        """;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Lays a specification out without painting.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="limits">The limits, or null.</param>
    /// <returns>The scene.</returns>
    private static Scene SceneOf(string json, VegaLiteLimits? limits = null) =>
        VegaLiteDocument.Parse(json).BuildScene(new VegaLiteRenderOptions { Limits = limits ?? VegaLiteLimits.Default }, Ct);

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
    ///     Gets the texts of the items of a role in a scene.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role.</param>
    /// <returns>The texts in drawing order.</returns>
    private static List<string> Texts(Scene scene, string role) => [.. Items<SceneText>(scene, role).Select(t => t.Text)];

    /// <summary>
    ///     Gets the number of items that each marks group of a scene holds, one entry for each layer.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The item counts of the marks groups in drawing order.</returns>
    private static List<int> MarkCounts(Scene scene) => [.. Items<SceneGroup>(scene, "marks").Select(g => g.Children.Count)];

    /// <summary>
    ///     Gets the items of the marks group of one layer.
    /// </summary>
    /// <typeparam name="T">The kind of item.</typeparam>
    /// <param name="scene">The scene.</param>
    /// <param name="layer">The index of the layer in drawing order.</param>
    /// <returns>The items of the layer.</returns>
    private static List<T> Layer<T>(Scene scene, int layer)
        where T : SceneItem => [.. Items<SceneGroup>(scene, "marks")[layer].Children.Cast<T>()];

    /// <summary>
    ///     Builds a specification.
    /// </summary>
    /// <param name="mark">The mark.</param>
    /// <param name="encoding">The encoding property.</param>
    /// <param name="extra">More top-level properties (each ending with a comma), or empty.</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string mark, string encoding, string extra = "") => $$"""{ {{extra}} {{BoxData}}, "mark": {{mark}}, {{encoding}} }""";

    /// <summary>
    ///     Validates a default vertical box plot with every position derived from the quartiles: a layer for the outliers, the two whisker rules, the boxes and the medians;
    ///     white medians; and the axes of the group and of the measure.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_BoxPlot_VerticalTukeyIsDrawnFromTheQuartiles()
    {
        // Arrange / Act
        var scene = SceneOf(Spec("\"boxplot\"", VerticalEncoding));

        // Assert: the layers in drawing order and their items (one per group, the outliers only for A)
        Assert.Equal([1, 2, 2, 2, 2], MarkCounts(scene));
        var outlier = Assert.Single(Layer<SceneSymbol>(scene, 0));
        Assert.Equal((10.0, 0.0), (outlier.X, outlier.Y));

        // Assert: the lower whiskers run from the minimum 1 to the first quartile (A 3.5: 290 to 265; B 11 to 12: 190 to 180)
        Assert.Equal([(10.0, 290.0, 265.0), (30.0, 190.0, 180.0)], Layer<SceneRule>(scene, 1).Select(r => (r.X, r.Y, r.Y2)));

        // Assert: the upper whiskers from the third quartile to 10 (A: 215 to 200; B 14 to 15: 160 to 150)
        var upper = Layer<SceneRule>(scene, 2);
        Assert.Equal([10.0, 30.0], upper.Select(r => r.X));
        Assert.Equal([215.0, 160.0], upper.Select(r => r.Y));
        Assert.Equal([200.0, 150.0], upper.Select(r => r.Y2).Select(y => Math.Round(y, 6)));

        // Assert: the boxes are 14 wide, centered in the band of 20, from the third to the first quartile
        var boxes = Layer<SceneRect>(scene, 3);
        Assert.Equal([3.0, 23.0], boxes.Select(b => b.X));
        Assert.Equal([14.0, 14.0], boxes.Select(b => b.Width));
        Assert.Equal([215.0, 160.0], boxes.Select(b => b.Y));
        Assert.Equal([50.0, 20.0], boxes.Select(b => b.Height));
        Assert.All(boxes, b => Assert.Equal("#4c78a8", b.Fill));

        // Assert: the medians (6 and 13) are white ticks, a pixel thick, across the box
        var medians = Layer<SceneRect>(scene, 4);
        Assert.Equal([239.5, 169.5], medians.Select(m => m.Y));
        Assert.Equal([1.0, 1.0], medians.Select(m => m.Height));
        Assert.Equal([14.0, 14.0], medians.Select(m => m.Width));
        Assert.All(medians, m => Assert.Equal("white", m.Fill));

        // Assert: the guides name the group and the measure
        Assert.Equal(["g", "v"], Texts(scene, "axis-title"));
        Assert.Equal(["A", "B"], Texts(scene, "axis-label").Where(t => t is "A" or "B"));
        Assert.Equal(["0", "5", "10", "15", "20", "25", "30"], Texts(scene, "axis-label").Where(t => t.Length <= 2 && char.IsDigit(t[0])));
    }

    /// <summary>
    ///     Validates a horizontal box plot (the measure on <c>x</c>): the same quartiles run along the width, the ticks across the box are vertical.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_BoxPlot_HorizontalTukeyRunsAlongX()
    {
        // Arrange / Act
        var scene = SceneOf(Spec("\"boxplot\"", HorizontalEncoding));

        // Assert: the outlier 30 is at x = 300 on the row of A
        Assert.Equal([1, 2, 2, 2, 2], MarkCounts(scene));
        var outlier = Assert.Single(Layer<SceneSymbol>(scene, 0));
        Assert.Equal((300.0, 10.0), (outlier.X, outlier.Y));

        // Assert: whiskers 10 to 35 and 85 to 100 for A, 110 to 120 and 140 to 150 for B
        Assert.Equal([(10.0, 35.0, 10.0), (110.0, 120.0, 30.0)], Layer<SceneRule>(scene, 1).Select(r => (Math.Round(r.X, 6), Math.Round(r.X2, 6), r.Y)));
        Assert.Equal([(85.0, 100.0), (140.0, 150.0)], Layer<SceneRule>(scene, 2).Select(r => (Math.Round(r.X, 6), Math.Round(r.X2, 6))));

        // Assert: the boxes are 14 tall from the first to the third quartile
        var boxes = Layer<SceneRect>(scene, 3);
        Assert.Equal([35.0, 120.0], boxes.Select(b => b.X));
        Assert.Equal([50.0, 20.0], boxes.Select(b => b.Width));
        Assert.Equal([3.0, 23.0], boxes.Select(b => b.Y));
        Assert.Equal([14.0, 14.0], boxes.Select(b => b.Height));

        // Assert: the medians (6 and 13) are vertical ticks
        var medians = Layer<SceneRect>(scene, 4);
        Assert.Equal([59.5, 129.5], medians.Select(m => m.X));
        Assert.Equal([1.0, 1.0], medians.Select(m => m.Width));
        Assert.Equal([14.0, 14.0], medians.Select(m => m.Height));
        Assert.Equal(["v", "g"], Texts(scene, "axis-title").Where(t => t is "v" or "g"));
    }

    /// <summary>
    ///     Validates the min-max extent: the whiskers reach the minimum and the maximum (30 is no outlier, there is no layer of outliers).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_BoxPlot_MinMaxWhiskersReachTheExtremes()
    {
        // Arrange / Act
        var scene = SceneOf(Spec("""{ "type": "boxplot", "extent": "min-max" }""", VerticalEncoding));

        // Assert: four layers (lower whisker, upper whisker, box, median) with the extremes 1 and 30 and 11 and 15
        Assert.Equal([2, 2, 2, 2], MarkCounts(scene));
        Assert.Equal([(290.0, 265.0), (190.0, 180.0)], Layer<SceneRule>(scene, 0).Select(r => (r.Y, r.Y2)));
        Assert.Equal([(215.0, 0.0), (160.0, 150.0)], Layer<SceneRule>(scene, 1).Select(r => (r.Y, Math.Round(r.Y2, 6))));
        Assert.Equal([215.0, 160.0], Layer<SceneRect>(scene, 2).Select(b => b.Y));
    }

    /// <summary>
    ///     Validates a numeric extent that is wide enough to take 30 inside the fences (8.5 + 6 * 5 = 38.5): the outlier layer is empty and the whisker reaches 30.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_BoxPlot_WideExtentLeavesNoOutliers()
    {
        // Arrange / Act
        var scene = SceneOf(Spec("""{ "type": "boxplot", "extent": 6 }""", VerticalEncoding));

        // Assert
        Assert.Equal([0, 2, 2, 2, 2], MarkCounts(scene));
        Assert.Equal([(215.0, 0.0), (160.0, 150.0)], Layer<SceneRule>(scene, 2).Select(r => (r.Y, Math.Round(r.Y2, 6))));
    }

    /// <summary>
    ///     Validates a narrow extent: with 0.5 interquartile ranges the fences of A are 3.5 - 2.5 = 1 and 8.5 + 2.5 = 11, so 1 to 10 are inside and 30 is an outlier, and the
    ///     fences of B (interquartile range 2) are 11 and 15, so nothing is outside.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_BoxPlot_NarrowExtentKeepsTheValuesInsideTheFences()
    {
        // Arrange / Act
        var scene = SceneOf(Spec("""{ "type": "boxplot", "extent": 0.5 }""", VerticalEncoding));

        // Assert: one outlier (30), whiskers of A at 1 and 10, of B at 11 and 15
        Assert.Equal([1, 2, 2, 2, 2], MarkCounts(scene));
        Assert.Equal([290.0, 190.0], Layer<SceneRule>(scene, 1).Select(r => r.Y));
        Assert.Equal([200.0, 150.0], Layer<SceneRule>(scene, 2).Select(r => Math.Round(r.Y2, 6)));
    }

    /// <summary>
    ///     Validates the end ticks that are switched on: black, opaque, a pixel thick, at each end of the whiskers, drawn between the rules and the box; the outliers
    ///     switched off leave no layer.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_BoxPlot_EndTicksAreBlackAndOutliersCanBeSwitchedOff()
    {
        // Arrange / Act
        var scene = SceneOf(Spec("""{ "type": "boxplot", "ticks": true, "outliers": false }""", VerticalEncoding));

        // Assert: rules, ticks at the lower and upper whiskers, box, median; without the outlier the domain is 0 to 16 (18.75 pixels for each unit), so the lower whiskers
        // (1 and 11) are at 281.25 and 93.75 and the upper ones (10 and 15) at 112.5 and 18.75, each tick a pixel thick around its position
        Assert.Equal([2, 2, 2, 2, 2, 2], MarkCounts(scene));
        var lower = Layer<SceneRect>(scene, 2);
        var upper = Layer<SceneRect>(scene, 3);
        Assert.Equal([280.75, 93.25], lower.Select(t => Math.Round(t.Y, 6)));
        Assert.Equal([112.0, 18.25], upper.Select(t => Math.Round(t.Y, 6)));
        Assert.All(lower.Concat(upper), t =>
        {
            Assert.Equal("black", t.Fill);
            Assert.Equal(1.0, t.Opacity);
            Assert.Equal(1.0, t.Height);
        });
    }

    /// <summary>
    ///     Validates the color channel: the boxes and the outliers take the group's color, the median stays white, the whiskers stay black, and one legend describes the
    ///     groups.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_BoxPlot_ColorChannelColorsBoxesAndOutliersAndMakesALegend()
    {
        // Arrange / Act
        var scene = SceneOf(Spec("\"boxplot\"", ColorEncoding));

        // Assert: boxes colored by group, hollow outlier of the first color, white medians, black whiskers
        Assert.Equal(["#4c78a8", "#f58518"], Layer<SceneRect>(scene, 3).Select(b => b.Fill));
        Assert.Equal("#4c78a8", Assert.Single(Layer<SceneSymbol>(scene, 0)).Stroke);
        Assert.All(Layer<SceneRect>(scene, 4), m => Assert.Equal("white", m.Fill));
        Assert.All(Layer<SceneRule>(scene, 1).Concat(Layer<SceneRule>(scene, 2)), r => Assert.Equal("black", r.Stroke));

        // Assert: one legend with the two groups
        Assert.Equal(["A", "B"], Texts(scene, "legend-label"));
        Assert.Equal(["g"], Texts(scene, "legend-title"));
    }

    /// <summary>
    ///     Validates that a box without height (all values equal) gives its color to the median, which would be invisible in white.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_BoxPlot_FlatBoxMedianTakesTheBoxColor()
    {
        // Arrange: one group whose quartiles are all 5, and a group with a real box
        const string json = """
            { "data": { "values": [ { "g": "C", "v": 5 }, { "g": "C", "v": 5 }, { "g": "C", "v": 5 }, { "g": "D", "v": 1 }, { "g": "D", "v": 2 }, { "g": "D", "v": 3 } ] },
              "mark": "boxplot", "encoding": { "x": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } }
            """;

        // Act
        var scene = SceneOf(json);
        var boxes = Layer<SceneRect>(scene, 3);
        var medians = Layer<SceneRect>(scene, 4);

        // Assert: the flat box is as high as nothing, its median has the box color; the other median is white
        Assert.Equal(0.0, boxes[0].Height);
        Assert.Equal(["#4c78a8", "white"], medians.Select(m => m.Fill));
    }

    /// <summary>
    ///     Validates that the <c>color</c> of the mark colors every part (the boxes, the whiskers and the outliers, but not the median), and that the properties of a part
    ///     (<c>median</c>) override it.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_BoxPlot_MarkColorAndPartPropertiesApply()
    {
        // Arrange / Act
        var scene = SceneOf(Spec("""{ "type": "boxplot", "color": "firebrick", "median": { "color": "yellow" } }""", VerticalEncoding));

        // Assert
        Assert.All(Layer<SceneRect>(scene, 3), b => Assert.Equal("firebrick", b.Fill));
        Assert.All(Layer<SceneRule>(scene, 1), r => Assert.Equal("firebrick", r.Stroke));
        Assert.Equal("firebrick", Assert.Single(Layer<SceneSymbol>(scene, 0)).Stroke);
        Assert.All(Layer<SceneRect>(scene, 4), m => Assert.Equal("yellow", m.Fill));
    }

    /// <summary>
    ///     Validates the configuration of the box plot (<c>config.boxplot</c>): the size of the box and the color of the median.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_BoxPlot_ConfigurationSetsSizeAndMedianColor()
    {
        // Arrange / Act
        var scene = SceneOf(Spec("\"boxplot\"", VerticalEncoding, BoxConfig));

        // Assert: 8 wide, centered in the band of 20
        var boxes = Layer<SceneRect>(scene, 3);
        Assert.Equal([6.0, 26.0], boxes.Select(b => b.X));
        Assert.Equal([8.0, 8.0], boxes.Select(b => b.Width));
        Assert.All(Layer<SceneRect>(scene, 4), m => Assert.Equal("red", m.Fill));
    }

    /// <summary>
    ///     Validates that the transforms of the view apply before the box plot: with the outlier filtered out the domain is 0 to 16 (18.75 pixels for each unit) and A has the
    ///     quartiles 3.25 and 7.75.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_BoxPlot_TopLevelTransformAppliesFirst()
    {
        // Arrange / Act
        var scene = SceneOf(Spec("\"boxplot\"", VerticalEncoding, FilterTransform));

        // Assert: no outlier; box of A from 7.75 (y = 154.6875) to 3.25, 84.375 high; box of B from 14 (y = 37.5) to 12, 37.5 high
        Assert.Equal([0, 2, 2, 2, 2], MarkCounts(scene));
        var boxes = Layer<SceneRect>(scene, 3);
        Assert.Equal([154.6875, 37.5], boxes.Select(b => Math.Round(b.Y, 6)));
        Assert.Equal([84.375, 37.5], boxes.Select(b => Math.Round(b.Height, 6)));
    }

    /// <summary>
    ///     Validates a box plot as one layer of a layered view: the mean rule layer shares the measure scale with it, and the axis title is the title the box plot writes for its
    ///     field (a written title wins over the one derived for the aggregate, as in Vega-Lite).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_BoxPlot_LayersShareTheMeasureScale()
    {
        // Arrange: the mean of the sixteen values is 150 / 16 = 9.375, which is at 300 - 93.75 = 206.25
        var json = $$"""
            { {{BoxData}},
              "layer": [
                { "mark": "boxplot", {{VerticalEncoding}} },
                { "mark": "rule", "encoding": { "y": { "aggregate": "mean", "field": "v", "type": "quantitative" }, "color": { "value": "red" } } } ] }
            """;

        // Act
        var scene = SceneOf(json);

        // Assert: five layers for the box plot, one for the rule on the same scale
        Assert.Equal([1, 2, 2, 2, 2, 1], MarkCounts(scene));
        var rule = Layer<SceneRule>(scene, 5).Single();
        Assert.Equal(206.25, rule.Y, 6);
        Assert.Equal(["g", "v"], Texts(scene, "axis-title"));
        Assert.Equal([215.0, 160.0], Layer<SceneRect>(scene, 3).Select(b => b.Y));
    }

    /// <summary>
    ///     Validates that the box plot reports nothing for the features it supports, even in strict mode, and that a bin, a time unit or an aggregate on a grouping channel is
    ///     reported as <c>vl-composite-grouping</c> while the chart is still drawn (the retired <c>vl-mark-boxplot</c> is never produced).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_BoxPlot_ReportsOnlyUnsupportedGrouping()
    {
        // Arrange
        var plain = VegaLiteDocument.Parse(Spec("\"boxplot\"", VerticalEncoding));
        var binned = VegaLiteDocument.Parse(Spec("\"boxplot\"", BinnedEncoding));
        var unsupported = new List<string>();
        var strict = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true };

        // Act: strict mode does not throw for the plain box plot
        using var surface = plain.Render(strict, Ct);
        using var drawn = binned.Render(new VegaLiteRenderOptions { UnsupportedFeatureHandler = u => unsupported.Add(u.Feature) }, Ct);

        // Assert
        Assert.NotNull(surface);
        Assert.Equal(["vl-composite-grouping"], unsupported);
        Assert.NotNull(drawn);
    }

    /// <summary>
    ///     Validates the limits: the views of the expansion count against <see cref="VegaLiteLimits.MaxViews"/> (nine for a box plot), at the mark.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_BoxPlot_ExpandedViewsAreLimitedByMaxViews()
    {
        // Arrange
        var document = VegaLiteDocument.Parse(Spec("\"boxplot\"", VerticalEncoding));

        // Act / Assert: nine views are allowed, eight are not
        Assert.NotNull(document.BuildScene(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxViews = 9 } }, Ct));
        var error = Assert.Throws<VegaLiteRenderException>(() => document.BuildScene(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxViews = 8 } }, Ct));
        Assert.Contains("MaxViews", error.Message, StringComparison.Ordinal);
        Assert.Equal("/mark", error.Location.Pointer);
    }

    /// <summary>
    ///     Validates that a box plot whose orientation cannot be decided (no continuous channel) is a specification error at the mark.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_BoxPlot_NoContinuousChannelIsASpecificationError()
    {
        // Arrange
        var document = VegaLiteDocument.Parse(Spec("\"boxplot\"", NominalEncoding));

        // Act / Assert
        var error = Assert.Throws<VegaLiteSpecException>(() => document.BuildScene(new VegaLiteRenderOptions(), Ct));
        Assert.Equal("/mark", error.Location.Pointer);
    }

    /// <summary>
    ///     Validates that measuring and rendering agree for a box plot and that the result is the same every time.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_BoxPlot_MeasureMatchesRenderAndIsDeterministic()
    {
        // Arrange
        var document = VegaLiteDocument.Parse(Spec("\"boxplot\"", VerticalEncoding));

        // Act
        var size = document.Measure(new VegaLiteRenderOptions(), Ct);
        using var first = document.Render(new VegaLiteRenderOptions(), Ct);
        using var second = document.Render(new VegaLiteRenderOptions(), Ct);

        // Assert
        Assert.Equal((size.Width, size.Height), (first.Width, first.Height));
        Assert.Equal(first.Width, second.Width);
        Assert.Equal(first.GetRowSpanBytes(first.Height / 2).ToArray(), second.GetRowSpanBytes(second.Height / 2).ToArray());
    }

    /// <summary>The encoding of a one-dimensional error band: the measure only, over a fixed scale of 0 to 30.</summary>
    private const string MeasureOnly = """
        "encoding": { "y": { "field": "v", "type": "quantitative", "scale": { "domain": [0, 30] } } }
        """;

    /// <summary>The encoding of a vertical error bar over a fixed measure scale of 0 to 30 (10 pixels for each unit).</summary>
    private const string ScaledVertical = """
        "encoding": { "x": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative", "scale": { "domain": [0, 30] } } }
        """;

    /// <summary>The encoding of a horizontal error bar over a fixed measure scale of 0 to 30.</summary>
    private const string ScaledHorizontal = """
        "encoding": { "y": { "field": "g", "type": "nominal" }, "x": { "field": "v", "type": "quantitative", "scale": { "domain": [0, 30] } } }
        """;

    /// <summary>The rows of an error bar whose range is computed: a value, its ends and its errors.</summary>
    private const string RangeData = """
        "data": { "values": [
            { "g": "A", "m": 10, "lo": 7, "hi": 14, "e": 3, "n": -2 },
            { "g": "B", "m": 20, "lo": 18, "hi": 25, "e": 5, "n": -4 }
          ] },
        """;

    /// <summary>
    ///     Computes the summary of the values the way an error bar does: the mean, the standard error and the sample standard deviation.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>The mean, standard error and standard deviation.</returns>
    private static (double Mean, double StdErr, double StdDev) Summary(params double[] values)
    {
        var mean = values.Average();
        var deviation = Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / (values.Length - 1));
        return (mean, deviation / Math.Sqrt(values.Length), deviation);
    }

    /// <summary>
    ///     Gets the pixel position of a value on the fixed measure scale of 0 to 30 over 300 pixels.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The vertical pixel position.</returns>
    private static double Pixel(double value) => 300 - (10 * value);

    /// <summary>
    ///     Validates a default error bar: only the rule is drawn (the ticks are off), from the mean minus the standard error to the mean plus it, for each group.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_ErrorBar_DefaultIsARuleOfTheMeanAndStandardError()
    {
        // Arrange
        var a = Summary(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 30);
        var b = Summary(11, 12, 13, 14, 15);

        // Act
        var scene = SceneOf(Spec("\"errorbar\"", ScaledVertical));

        // Assert: one layer of two rules, on the center of the bands
        Assert.Equal([2], MarkCounts(scene));
        var rules = Layer<SceneRule>(scene, 0);
        Assert.Equal([10.0, 30.0], rules.Select(r => r.X));
        Assert.Equal([Pixel(a.Mean - a.StdErr), Pixel(b.Mean - b.StdErr)], rules.Select(r => r.Y).ToList(), new PixelComparer());
        Assert.Equal([Pixel(a.Mean + a.StdErr), Pixel(b.Mean + b.StdErr)], rules.Select(r => r.Y2).ToList(), new PixelComparer());

        // Assert: the axis is titled with the field
        Assert.Equal(["g", "v"], Texts(scene, "axis-title"));
    }

    /// <summary>
    ///     Validates the <c>stdev</c> extent: the rule reaches the sample standard deviation from the mean.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_ErrorBar_StandardDeviationExtent()
    {
        // Arrange
        var a = Summary(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 30);
        var b = Summary(11, 12, 13, 14, 15);

        // Act
        var scene = SceneOf(Spec("""{ "type": "errorbar", "extent": "stdev" }""", ScaledVertical));

        // Assert
        var rules = Layer<SceneRule>(scene, 0);
        Assert.Equal([Pixel(a.Mean - a.StdDev), Pixel(b.Mean - b.StdDev)], rules.Select(r => r.Y).ToList(), new PixelComparer());
        Assert.Equal([Pixel(a.Mean + a.StdDev), Pixel(b.Mean + b.StdDev)], rules.Select(r => r.Y2).ToList(), new PixelComparer());
    }

    /// <summary>
    ///     Validates the <c>iqr</c> extent (and the median center that goes with it when only the center is given): the rule runs from the first to the third quartile.
    /// </summary>
    /// <param name="mark">The mark definition.</param>
    [Theory]
    [InlineData("""{ "type": "errorbar", "extent": "iqr" }""")]
    [InlineData("""{ "type": "errorbar", "center": "median" }""")]
    public void CanvasNetVegaLite_CompositeMark_ErrorBar_InterquartileRange(string mark)
    {
        // Act: the quartiles are 3.5 and 8.5 for A and 12 and 14 for B
        var scene = SceneOf(Spec(mark, ScaledVertical));

        // Assert
        var rules = Layer<SceneRule>(scene, 0);
        Assert.Equal([Pixel(3.5), Pixel(12)], rules.Select(r => r.Y).ToList(), new PixelComparer());
        Assert.Equal([Pixel(8.5), Pixel(14)], rules.Select(r => r.Y2).ToList(), new PixelComparer());
    }

    /// <summary>
    ///     Validates the <c>ci</c> extent: a confidence interval around the mean that is the same on every render.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_ErrorBar_ConfidenceIntervalSurroundsTheMeanAndIsDeterministic()
    {
        // Arrange
        var a = Summary(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 30);
        var spec = Spec("""{ "type": "errorbar", "extent": "ci" }""", ScaledVertical);

        // Act
        var first = Layer<SceneRule>(SceneOf(spec), 0);
        var second = Layer<SceneRule>(SceneOf(spec), 0);

        // Assert: the interval (Y2 is the upper end, the smaller pixel) contains the mean and is the same every time
        Assert.True(first[0].Y > Pixel(a.Mean) && first[0].Y2 < Pixel(a.Mean));
        Assert.Equal(first.Select(r => (r.Y, r.Y2)), second.Select(r => (r.Y, r.Y2)));
    }

    /// <summary>
    ///     Validates the horizontal error bar (the measure on <c>x</c>): the rule runs along <c>x</c> and the ticks are vertical.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_ErrorBar_HorizontalRunsAlongX()
    {
        // Arrange
        var b = Summary(11, 12, 13, 14, 15);

        // Act
        var scene = SceneOf(Spec("""{ "type": "errorbar", "ticks": true, "size": 6 }""", ScaledHorizontal));

        // Assert: ticks at the lower and upper end, then the rule; the rule of B is on the second row center (30)
        Assert.Equal([2, 2, 2], MarkCounts(scene));
        var rule = Layer<SceneRule>(scene, 2)[1];
        Assert.Equal((10 * (b.Mean - b.StdErr), 10 * (b.Mean + b.StdErr), 30.0), (rule.X, rule.X2, rule.Y), new PixelComparer());
        var lowerTick = Layer<SceneRect>(scene, 0)[1];
        Assert.Equal(6.0, lowerTick.Height);
        Assert.Equal(1.0, lowerTick.Width);
    }

    /// <summary>
    ///     Validates the ticks that are switched on: black-less default marks of the given size and thickness at both ends, drawn before the rule.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_ErrorBar_TicksHaveTheGivenSizeAndThickness()
    {
        // Arrange
        var a = Summary(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 30);

        // Act
        var scene = SceneOf(Spec("""{ "type": "errorbar", "ticks": true, "size": 8, "thickness": 3 }""", ScaledVertical));

        // Assert: ticks are 8 wide and 3 thick, centered on the ends of the rule; the rule is 3 wide
        Assert.Equal([2, 2, 2], MarkCounts(scene));
        var lower = Layer<SceneRect>(scene, 0)[0];
        var upper = Layer<SceneRect>(scene, 1)[0];
        Assert.Equal((8.0, 3.0), (lower.Width, lower.Height));
        Assert.Equal(Pixel(a.Mean - a.StdErr) - 1.5, lower.Y, 6);
        Assert.Equal(Pixel(a.Mean + a.StdErr) - 1.5, upper.Y, 6);
        Assert.All(Layer<SceneRule>(scene, 2), r => Assert.Equal(3.0, r.StrokeWidth));
    }

    /// <summary>
    ///     Validates that the parts can be switched off, so a mark with no part draws nothing, and that the configuration of the mark gives the defaults.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_ErrorBar_PartsFollowMarkAndConfiguration()
    {
        // Act
        var none = SceneOf(Spec("""{ "type": "errorbar", "rule": false }""", ScaledVertical));
        var configured = SceneOf(Spec("\"errorbar\"", ScaledVertical, """ "config": { "errorbar": { "ticks": true, "rule": false } }, """));
        var overridden = SceneOf(Spec("""{ "type": "errorbar", "rule": true }""", ScaledVertical, """ "config": { "errorbar": { "ticks": true, "rule": false } }, """));

        // Assert
        Assert.Empty(MarkCounts(none));
        Assert.Equal([2, 2], MarkCounts(configured));
        Assert.Equal([2, 2, 2], MarkCounts(overridden));
    }

    /// <summary>
    ///     Validates that the color of the mark and of a part, and a color channel, reach the rule; the size channel is not drawn.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_ErrorBar_ColorAndPartProperties()
    {
        // Act
        var marked = SceneOf(Spec("""{ "type": "errorbar", "color": "red", "rule": { "strokeDash": [4, 2] } }""", ScaledVertical));
        var colored = SceneOf(Spec("\"errorbar\"", """
            "encoding": { "x": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" }, "color": { "field": "g", "type": "nominal" } }
            """));

        // Assert
        Assert.All(Layer<SceneRule>(marked, 0), r =>
        {
            Assert.Equal("red", r.Stroke);
            Assert.Equal([4.0, 2.0], r.StrokeDash);
        });
        Assert.Equal(["#4c78a8", "#f58518"], Layer<SceneRule>(colored, 0).Select(r => r.Stroke));
        Assert.Equal(["A", "B"], Texts(colored, "legend-label"));
    }

    /// <summary>
    ///     Validates the pre-aggregated forms of an error bar: the range given by a second end (<c>y2</c>), by an error (<c>yError</c>) and by two errors (<c>yError2</c>);
    ///     the rows are not aggregated.
    /// </summary>
    /// <param name="encoding">The encoding.</param>
    /// <param name="lowerA">The lower end of A.</param>
    /// <param name="upperA">The upper end of A.</param>
    /// <param name="lowerB">The lower end of B.</param>
    /// <param name="upperB">The upper end of B.</param>
    [Theory]
    [InlineData("""{ "x": { "field": "g", "type": "nominal" }, "y": { "field": "lo", "type": "quantitative", "scale": { "domain": [0, 30] } }, "y2": { "field": "hi" } }""", 7, 14, 18, 25)]
    [InlineData("""{ "x": { "field": "g", "type": "nominal" }, "y": { "field": "m", "type": "quantitative", "scale": { "domain": [0, 30] } }, "yError": { "field": "e" } }""", 7, 13, 15, 25)]
    [InlineData("""{ "x": { "field": "g", "type": "nominal" }, "y": { "field": "m", "type": "quantitative", "scale": { "domain": [0, 30] } }, "yError": { "field": "e" }, "yError2": { "field": "n" } }""", 8, 13, 16, 25)]
    public void CanvasNetVegaLite_CompositeMark_ErrorBar_RangeAndErrorChannelsAreNotAggregated(string encoding, double lowerA, double upperA, double lowerB, double upperB)
    {
        // Act
        var scene = SceneOf($$"""{ {{RangeData}} "mark": "errorbar", "encoding": {{encoding}} }""");

        // Assert
        var rules = Layer<SceneRule>(scene, 0);
        Assert.Equal([Pixel(lowerA), Pixel(lowerB)], rules.Select(r => r.Y).ToList(), new PixelComparer());
        Assert.Equal([Pixel(upperA), Pixel(upperB)], rules.Select(r => r.Y2).ToList(), new PixelComparer());
    }

    /// <summary>
    ///     Validates the horizontal pre-aggregated forms: <c>x2</c> and <c>xError</c>.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_ErrorBar_HorizontalRangeAndErrorChannels()
    {
        // Act
        var range = SceneOf($$"""{ {{RangeData}} "mark": "errorbar", "encoding": { "y": { "field": "g", "type": "nominal" }, "x": { "field": "lo", "type": "quantitative", "scale": { "domain": [0, 30] } }, "x2": { "field": "hi" } } }""");
        var error = SceneOf($$"""{ {{RangeData}} "mark": "errorbar", "encoding": { "y": { "field": "g", "type": "nominal" }, "x": { "field": "m", "type": "quantitative", "scale": { "domain": [0, 30] } }, "xError": { "field": "e" } } }""");

        // Assert: 10 pixels for each unit of a 300 pixel wide range
        Assert.Equal([(70.0, 140.0), (180.0, 250.0)], Layer<SceneRule>(range, 0).Select(r => (r.X, r.X2)));
        Assert.Equal([(70.0, 130.0), (150.0, 250.0)], Layer<SceneRule>(error, 0).Select(r => (r.X, r.X2)));
    }

    /// <summary>
    ///     Validates the specification errors of an error bar: both ends and errors, <c>x2</c> and <c>y2</c> together, an error without its value, a range whose measure is
    ///     not quantitative and a range channel that is not a field, all at the mark.
    /// </summary>
    /// <param name="encoding">The encoding.</param>
    [Theory]
    [InlineData("""{ "x": { "field": "g", "type": "nominal" }, "y": { "field": "lo", "type": "quantitative" }, "y2": { "field": "hi" }, "yError": { "field": "e" } }""")]
    [InlineData("""{ "x": { "field": "lo", "type": "quantitative" }, "y": { "field": "lo", "type": "quantitative" }, "y2": { "field": "hi" }, "x2": { "field": "hi" } }""")]
    [InlineData("""{ "x": { "field": "g", "type": "nominal" }, "y": { "field": "m", "type": "quantitative" }, "yError2": { "field": "n" } }""")]
    [InlineData("""{ "x": { "field": "g", "type": "nominal" }, "y": { "field": "g", "type": "nominal" }, "y2": { "field": "hi" } }""")]
    [InlineData("""{ "x": { "field": "g", "type": "nominal" }, "yError": { "field": "e" } }""")]
    public void CanvasNetVegaLite_CompositeMark_ErrorBar_InvalidEncodingIsASpecificationError(string encoding)
    {
        // Arrange
        var document = VegaLiteDocument.Parse($$"""{ {{RangeData}} "mark": "errorbar", "encoding": {{encoding}} }""");

        // Act / Assert
        var error = Assert.Throws<VegaLiteSpecException>(() => document.BuildScene(new VegaLiteRenderOptions(), Ct));
        Assert.Equal("/mark", error.Location.Pointer);
    }

    /// <summary>
    ///     Validates the one-dimensional error band (only the measure is encoded): one band (a rect) over the whole width from the lower to the upper end of all the rows,
    ///     30% opaque by default and without borders; an <c>x</c> channel as well would make the band two-dimensional.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_ErrorBand_OneDimensionalBandIsAThirtyPercentRect()
    {
        // Arrange: all sixteen rows form the group
        var all = Summary(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 30, 11, 12, 13, 14, 15);

        // Act
        var scene = SceneOf(Spec("\"errorband\"", MeasureOnly));

        // Assert: one rect, from the left edge, over the width of the view
        Assert.Equal([1], MarkCounts(scene));
        var band = Layer<SceneRect>(scene, 0)[0];
        Assert.Equal(0.0, band.X);
        Assert.Equal(300.0, band.Width);
        Assert.Equal(0.3, band.Opacity, 6);
        Assert.Equal(Pixel(all.Mean + all.StdErr), band.Y, 6);
        Assert.Equal(20 * all.StdErr, band.Height, 6);
    }

    /// <summary>
    ///     Validates the borders of a band that are switched on: a rule at the lower and at the upper end, drawn after the band.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_ErrorBand_BordersAreRulesAtBothEnds()
    {
        // Arrange
        var all = Summary(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 30, 11, 12, 13, 14, 15);

        // Act
        var scene = SceneOf(Spec("""{ "type": "errorband", "borders": true, "opacity": 0.5, "extent": "stdev" }""", MeasureOnly));

        // Assert: band, lower border, upper border; the mark opacity replaces the 30% of the band and applies to the borders
        Assert.Equal([1, 1, 1], MarkCounts(scene));
        Assert.Equal(0.5, Layer<SceneRect>(scene, 0)[0].Opacity, 6);
        var lower = Layer<SceneRule>(scene, 1)[0];
        var upper = Layer<SceneRule>(scene, 2)[0];
        Assert.Equal(Pixel(all.Mean - all.StdDev), lower.Y, 6);
        Assert.Equal(Pixel(all.Mean + all.StdDev), upper.Y, 6);
        Assert.Equal([0.0, 300.0], new[] { lower.X, lower.X2 }.Order());
        Assert.Equal(0.5, lower.Opacity, 6);
    }

    /// <summary>
    ///     Validates the two-dimensional error band (both <c>x</c> and <c>y</c>): an area between the lower and upper ends along <c>x</c>, interpolated as asked, and
    ///     lines for the borders.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_ErrorBand_TwoDimensionalBandIsAnAreaWithLineBorders()
    {
        // Arrange: three x positions, two observations each (mean 5 +- 1 stderr at every x, as the two values are 4 and 6)
        const string data = """
            "data": { "values": [
                { "t": 1, "v": 4 }, { "t": 1, "v": 6 }, { "t": 2, "v": 14 }, { "t": 2, "v": 16 }, { "t": 3, "v": 4 }, { "t": 3, "v": 6 }
              ] },
            """;
        const string encoding = """
            "encoding": { "x": { "field": "t", "type": "quantitative" }, "y": { "field": "v", "type": "quantitative", "scale": { "domain": [0, 30] } } }
            """;

        // Act
        var plain = SceneOf($$"""{ {{data}} "mark": "errorband", {{encoding}} }""");
        var bordered = SceneOf($$"""{ {{data}} "mark": { "type": "errorband", "borders": true, "interpolate": "monotone", "tension": 0.5 }, {{encoding}} }""");

        // Assert: one area of three points up and three back (a move, five lines and a close); with borders also two lines of three points
        Assert.Equal([1], MarkCounts(plain));
        var area = Layer<ScenePath>(plain, 0)[0];
        Assert.Equal(0.3, area.FillOpacity * area.Opacity, 6);
        Assert.Equal(7, area.Segments.Count);
        Assert.Equal([1, 1, 1], MarkCounts(bordered));
        Assert.All(Layer<ScenePath>(bordered, 1).Concat(Layer<ScenePath>(bordered, 2)), p => Assert.True(p.Segments.Count > 0 || p.Points.Count == 3));
    }

    /// <summary>
    ///     Validates the configuration of the error band: the opacity of the band and the borders.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_ErrorBand_ConfigurationSetsOpacityAndBorders()
    {
        // Act
        var scene = SceneOf(Spec("\"errorband\"", MeasureOnly, """ "config": { "errorband": { "band": { "opacity": 0.6 }, "borders": true } }, """));

        // Assert
        Assert.Equal([1, 1, 1], MarkCounts(scene));
        Assert.Equal(0.6, Layer<SceneRect>(scene, 0)[0].Opacity, 6);
    }

    /// <summary>
    ///     Validates that an error bar layered over a point mark shares the measure scale, and that an error bar with the errors and the unsupported grouping is reported
    ///     correctly: no report for the channels the mark uses, <c>vl-channel-xerror</c> for a mark that does not use them, and no retired mark ids.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_ErrorBar_ReportsOnlyWhatItDoesNotUse()
    {
        // Arrange
        var supported = VegaLiteDocument.Parse($$"""{ {{RangeData}} "mark": "errorbar", "encoding": { "x": { "field": "g", "type": "nominal" }, "y": { "field": "m", "type": "quantitative" }, "yError": { "field": "e" } } }""");
        var unsupported = VegaLiteDocument.Parse($$"""{ {{RangeData}} "mark": "point", "encoding": { "x": { "field": "g", "type": "nominal" }, "y": { "field": "m", "type": "quantitative" }, "yError": { "field": "e" } } }""");
        var features = new List<string>();

        // Act: strict mode does not throw for the error bar
        using var drawn = supported.Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct);
        using var other = unsupported.Render(new VegaLiteRenderOptions { UnsupportedFeatureHandler = u => features.Add(u.Feature) }, Ct);

        // Assert
        Assert.NotNull(drawn);
        Assert.Equal(["vl-channel-yerror"], features);
    }

    /// <summary>
    ///     Validates the layering with another mark: a bar and its error bar share the measure scale, so the rule is at the top of the bar's range.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_ErrorBar_LayersOverABar()
    {
        // Act
        var scene = SceneOf($$"""
            { {{RangeData}}
              "layer": [
                { "mark": "bar", "encoding": { "x": { "field": "g", "type": "nominal" }, "y": { "field": "m", "type": "quantitative", "scale": { "domain": [0, 30] } } } },
                { "mark": "errorbar", "encoding": { "x": { "field": "g", "type": "nominal" }, "y": { "field": "m", "type": "quantitative" }, "yError": { "field": "e" } } }
              ] }
            """);

        // Assert: the bars end at 10 and 20; the rules reach 13 and 25 above them
        Assert.Equal([2, 2], MarkCounts(scene));
        Assert.Equal([Pixel(10), Pixel(20)], Layer<SceneRect>(scene, 0).Select(r => r.Y).ToList(), new PixelComparer());
        Assert.Equal([Pixel(13), Pixel(25)], Layer<SceneRule>(scene, 1).Select(r => r.Y2).ToList(), new PixelComparer());
    }

    /// <summary>
    ///     Validates the limits: the views of an error bar (the composite, the view of its transforms and the rule) count against <see cref="VegaLiteLimits.MaxViews"/>.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_CompositeMark_ErrorBar_ExpandedViewsAreLimitedByMaxViews()
    {
        // Arrange
        var document = VegaLiteDocument.Parse(Spec("\"errorbar\"", VerticalEncoding));

        // Act / Assert: three views are allowed, two are not
        Assert.NotNull(document.BuildScene(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxViews = 3 } }, Ct));
        var error = Assert.Throws<VegaLiteRenderException>(() => document.BuildScene(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxViews = 2 } }, Ct));
        Assert.Contains("MaxViews", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Compares pixel positions with a tolerance of a millionth, so that the arithmetic of the scale and the test may differ in the last digits.
    /// </summary>
    private sealed class PixelComparer : IEqualityComparer<double>, IEqualityComparer<(double, double, double)>
    {
        /// <inheritdoc/>
        public bool Equals(double x, double y) => Math.Abs(x - y) < 1e-6;

        /// <inheritdoc/>
        public int GetHashCode(double obj) => 0;

        /// <inheritdoc/>
        public bool Equals((double, double, double) x, (double, double, double) y) => Equals(x.Item1, y.Item1) && Equals(x.Item2, y.Item2) && Equals(x.Item3, y.Item3);

        /// <inheritdoc/>
        public int GetHashCode((double, double, double) obj) => 0;
    }
}
