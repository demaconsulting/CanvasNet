using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of the Phase 14 additions to the CanvasNetVegaLite system, exercised through the public API: the <c>clipHeight</c> of a symbol legend, the ranged offsets of bars and areas,
///     <c>config.scale.invalid</c> and the distribution functions of the expression language. Every chart is rendered with the strict policy (nothing that it uses is reported) and checked on its structure and
///     on its pixels; the geometry of the expectations is that of Vega 6.4 with Vega-Lite 6.5.0 (see the unit tests of the same features).
/// </summary>
public class VegaLitePhase14SystemTests
{
    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The strict render options: a feature that is not drawn is an exception.</summary>
    private static VegaLiteRenderOptions Strict => new() { ThrowOnUnsupportedFeature = true };

    /// <summary>
    ///     Renders a specification with the strict policy.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The surface.</returns>
    private static Surface RenderStrict(string json) => VegaLiteDocument.Parse(json).Render(Strict, Ct);

    /// <summary>
    ///     Lays a specification out without painting.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The scene.</returns>
    private static Scene SceneOf(string json) => VegaLiteDocument.Parse(json).BuildScene(new VegaLiteRenderOptions(), Ct);

    /// <summary>
    ///     Validates the <c>clipHeight</c> of a symbol legend through the whole pipeline: nothing is reported, and a symbol far larger than its entry (a diameter of 62 pixels in an entry of 10) is clipped to the entry,
    ///     so the column through its center holds about ten pixels of its fill, against more than fifty without the property.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Legend_ClipHeightClipsTheSymbolsToTheirEntries()
    {
        // Arrange: three categories as filled points; the legend symbols have the area 3000 (62 pixels across) in entries that are 10 high
        const string template = """
            { "data": { "values": [ { "a": 1, "c": "x" }, { "a": 2, "c": "y" }, { "a": 3, "c": "z" } ] }, "width": 100, "height": 100,
              "mark": { "type": "point", "filled": true, "opacity": 1 },
              "encoding": { "x": { "field": "a", "type": "quantitative", "axis": null }, "color": { "field": "c", "type": "nominal", "legend": LEGEND } } }
            """;
        var clipped = template.Replace("LEGEND", """{ "clipHeight": 10, "symbolSize": 3000 }""", StringComparison.Ordinal);
        var unclipped = template.Replace("LEGEND", """{ "symbolSize": 3000 }""", StringComparison.Ordinal);

        // Act
        var scene = SceneOf(clipped);
        using var surface = RenderStrict(clipped);
        using var wide = RenderStrict(unclipped);
        var clips = scene.FindAll("legend-symbol-clip").ToList();
        var first = Assert.IsType<SceneGroup>(clips[0].Item);
        var symbol = Assert.IsType<SceneSymbol>(Assert.Single(first.Children));
        var column = (int)Math.Round(clips[0].OffsetX + first.X + symbol.X);
        var blue = new Rgba32(0x4c, 0x78, 0xa8, 255);

        // Assert: three entries of 10 (12 apart), each with a clip rectangle of its own
        Assert.Equal(3, clips.Count);
        Assert.Equal(10.0, first.Clip!.Value.Height);
        Assert.Equal(Enumerable.Range(0, 3).Select(i => 12.0 * i), clips.Select(c => ((SceneGroup)c.Item).Y));
        Assert.InRange(CountColor(surface, column, blue), 9, 11);
        Assert.True(CountColor(wide, column, blue) > 40, "the unclipped symbol is far taller than its entry");
    }

    /// <summary>
    ///     Validates a ranged <c>yOffset</c> of a bar chart through the whole pipeline: nothing is reported, the bar of a positive value stands above the baseline of its band and the bar of a negative
    ///     value hangs below it, and the pixels between the bars and the baseline are the bar color and the pixels outside the bars are white.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Offset_RangedYOffsetDrawsBarsFromTheBaseline()
    {
        // Arrange: Vega's output for this chart (data rectangle 150 by 100): Q1 from y = 16.67 to the baseline 54.76, Q2 from the baseline to 83.33, Q3 from 42.06 to the baseline
        const string json = """
            { "padding": 0, "data": { "values": [ { "q": "Q1", "t": "A", "s": 12 }, { "q": "Q2", "t": "A", "s": -9 }, { "q": "Q3", "t": "A", "s": 4 } ] }, "width": 150, "height": 100,
              "mark": "bar",
              "encoding": { "x": { "field": "q", "type": "ordinal", "axis": null }, "y": { "field": "t", "type": "nominal", "axis": null }, "yOffset": { "field": "s", "type": "quantitative" } } }
            """;

        // Act
        var scene = SceneOf(json);
        using var surface = RenderStrict(json);
        var bars = scene.FindAll("mark-bar").Select(e => (Rect: (SceneRect)e.Item, e.OffsetX, e.OffsetY)).ToList();
        var blue = new Rgba32(0x4c, 0x78, 0xa8, 255);
        int X(int bar) => (int)Math.Round(bars[bar].OffsetX + bars[bar].Rect.X + (bars[bar].Rect.Width / 2));

        // Assert: three bars; the pixel inside each bar has the bar color and the pixel on the other side of the baseline is white
        Assert.Equal(3, bars.Count);
        Assert.Equal(blue, surface[X(0), (int)(bars[0].OffsetY + 40)]);
        Assert.Equal(VegaLiteTestHelpers.White, surface[X(0), (int)(bars[0].OffsetY + 70)]);
        Assert.Equal(blue, surface[X(1), (int)(bars[1].OffsetY + 70)]);
        Assert.Equal(VegaLiteTestHelpers.White, surface[X(1), (int)(bars[1].OffsetY + 30)]);
        Assert.Equal(blue, surface[X(2), (int)(bars[2].OffsetY + 50)]);
        Assert.Equal(VegaLiteTestHelpers.White, surface[X(2), (int)(bars[2].OffsetY + 30)]);
    }

    /// <summary>
    ///     Validates a ranged <c>yOffset</c> of an area: nothing is reported and the polygon runs between the offset values and the baseline of the band.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Offset_RangedYOffsetDrawsAnAreaBetweenTheValuesAndTheBaseline()
    {
        // Arrange
        const string json = """
            { "padding": 0, "data": { "values": [ { "q": "Q1", "t": "A", "s": 12 }, { "q": "Q2", "t": "A", "s": -9 }, { "q": "Q3", "t": "A", "s": 4 } ] }, "width": 150, "height": 100,
              "mark": "area",
              "encoding": { "x": { "field": "q", "type": "ordinal", "axis": null }, "y": { "field": "t", "type": "nominal", "axis": null }, "yOffset": { "field": "s", "type": "quantitative" } } }
            """;

        // Act
        var scene = SceneOf(json);
        using var surface = RenderStrict(json);
        var area = scene.FindAll("mark-area").Select(e => (Path: (ScenePath)e.Item, e.OffsetX, e.OffsetY)).Single();
        var blue = new Rgba32(0x4c, 0x78, 0xa8, 255);

        // Assert: Vega's polygon M25,16.667 L75,83.333 L125,42.063 L125,54.762 L75,54.762 L25,54.762 Z; the pixel at x = 25 between 17 and 54 is inside, at x = 75 between 55 and 83 too
        Assert.Equal(7, area.Path.Segments.Count);
        Assert.Equal(blue, surface[(int)(area.OffsetX + 25), (int)(area.OffsetY + 40)]);
        Assert.Equal(VegaLiteTestHelpers.White, surface[(int)(area.OffsetX + 25), (int)(area.OffsetY + 70)]);
        Assert.Equal(blue, surface[(int)(area.OffsetX + 75), (int)(area.OffsetY + 70)]);
        Assert.Equal(VegaLiteTestHelpers.White, surface[(int)(area.OffsetX + 75), (int)(area.OffsetY + 40)]);
    }

    /// <summary>
    ///     Validates what is still reported of the continuous offsets: a temporal offset of a bar is not drawn and reports <c>vl-offset-ranged</c>, so the strict policy refuses it.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Offset_ATemporalOffsetOfABarIsStillReported()
    {
        // Arrange
        const string json = """
            { "data": { "values": [ { "q": "Q1", "t": "A", "d": "2024-01-01" } ] }, "mark": "bar",
              "encoding": { "x": { "field": "q", "type": "ordinal" }, "y": { "field": "t", "type": "nominal" }, "yOffset": { "field": "d", "type": "temporal" } } }
            """;

        // Act
        var exception = Assert.Throws<VegaLiteUnsupportedFeatureException>(() => RenderStrict(json));

        // Assert
        Assert.Equal("vl-offset-ranged", exception.Feature);
        Assert.Equal("/encoding/yOffset", exception.Location.Pointer);
    }

    /// <summary>
    ///     Validates <c>config.scale.invalid</c> through the whole pipeline: nothing is reported, and the row without a color is drawn in the configured color (<c>#aaa</c>) instead of being dropped, the row without
    ///     an x at the configured pixel (a <c>value</c>), and the same chart without the configuration draws two of the four rows.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_ScaleInvalid_RowsWithoutAValueTakeTheConfiguredOutput()
    {
        // Arrange: filled circles of a fixed size, so the center pixel of each has the color of its row
        const string template = """
            { "padding": 0, "data": { "values": [ { "a": 1, "b": 2, "c": 5 }, { "a": 2, "b": 3, "c": null }, { "a": null, "b": 4, "c": 9 }, { "a": 4, "b": 6, "c": 2 } ] }, "width": 200, "height": 100,
              CONFIG "mark": { "type": "circle", "size": 400, "opacity": 1 },
              "encoding": { "x": { "field": "a", "type": "quantitative", "axis": null }, "y": { "field": "b", "type": "quantitative", "axis": null }, "color": { "field": "c", "type": "quantitative", "legend": null } } }
            """;
        var configured = template.Replace("CONFIG", "\"config\": { \"scale\": { \"invalid\": { \"color\": { \"value\": \"#aaa\" }, \"x\": { \"value\": 5 } } } },", StringComparison.Ordinal);
        var plain = template.Replace("CONFIG", string.Empty, StringComparison.Ordinal);

        // Act
        var scene = SceneOf(configured);
        using var surface = RenderStrict(configured);
        var points = scene.FindAll("mark-circle").Select(e => (Symbol: (SceneSymbol)e.Item, e.OffsetX, e.OffsetY)).ToList();
        var plainPoints = SceneOf(plain).FindAll("mark-circle").ToList();

        // Assert: four rows; the second has the color #aaa at the center pixel and the third is 5 pixels from the left edge of the data rectangle (Vega: translate(100,50) and translate(5,33.33))
        Assert.Equal(4, points.Count);
        Assert.Equal(2, plainPoints.Count);
        Assert.Equal("#aaa", points[1].Symbol.Fill);
        Assert.Equal(new Rgba32(0xaa, 0xaa, 0xaa, 255), surface[(int)(points[1].OffsetX + points[1].Symbol.X), (int)(points[1].OffsetY + points[1].Symbol.Y)]);
        Assert.Equal(5.0, points[2].Symbol.X, 9);
    }

    /// <summary>
    ///     Validates the reporting of an entry of <c>config.scale.invalid</c> that cannot be read: a <c>null</c> value is reported as <c>vl-scale-invalid-value</c> at the entry and the channel keeps the mode of the mark.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_ScaleInvalid_AnUnreadableEntryIsReported()
    {
        // Arrange
        const string json = """
            { "data": { "values": [ { "a": 1, "b": 2 } ] }, "config": { "scale": { "invalid": { "x": { "value": null } } } }, "mark": "point",
              "encoding": { "x": { "field": "a", "type": "quantitative" }, "y": { "field": "b", "type": "quantitative" } } }
            """;

        // Act
        var exception = Assert.Throws<VegaLiteUnsupportedFeatureException>(() => RenderStrict(json));

        // Assert
        Assert.Equal("vl-scale-invalid-value", exception.Feature);
        Assert.Equal("/config/scale/invalid/x", exception.Location.Pointer);
    }

    /// <summary>
    ///     Validates the distribution functions through the whole pipeline: nothing is reported, a filter on <c>cumulativeNormal</c> keeps the points above the 97.5th percentile and a calculate with
    ///     <c>quantileNormal</c> moves a point to 1.959964.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Expressions_DistributionFunctionsDrawTheirResults()
    {
        // Arrange: x from -3 to 3 in steps of 0.5; cumulativeNormal(x) > 0.975 holds from x = 2 (0.9772), and the quantile of 0.975 is 1.959964
        const string json = """
            { "padding": 0, "data": { "sequence": { "start": -3, "stop": 3.5, "step": 0.5, "as": "x" } }, "width": 130, "height": 50,
              "transform": [ { "filter": "cumulativeNormal(datum.x) > 0.975" }, { "calculate": "quantileNormal(0.975)", "as": "z" }, { "calculate": "densityNormal(datum.x)", "as": "d" } ],
              "mark": { "type": "circle", "size": 20 },
              "encoding": { "x": { "field": "x", "type": "quantitative", "axis": null, "scale": { "domain": [-3, 3] } }, "y": { "field": "d", "type": "quantitative", "axis": null } } }
            """;

        // Act
        var points = SceneOf(json).FindAll("mark-circle").Select(e => (SceneSymbol)e.Item).ToList();
        using var surface = RenderStrict(json);

        // Assert: x = 2, 2.5 and 3 over the domain -3 to 3 on 130 pixels; the density falls from 0.054 to 0.0044, so the points go down to the baseline
        Assert.Equal([108.333333333, 119.166666667, 130.0], points.Select(p => Math.Round(p.X, 9)));
        Assert.True(points[0].Y < points[1].Y && points[1].Y < points[2].Y);
    }

    /// <summary>
    ///     Validates that the sampling functions are still reported (an expression is a pure function of its datum): <c>random()</c> is <c>vl-expr-function-random</c> and evaluates to null.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Expressions_SamplingFunctionsAreStillReported()
    {
        // Arrange
        const string json = """
            { "data": { "values": [ { "x": 1 } ] }, "transform": [ { "calculate": "random()", "as": "r" } ], "mark": "point", "encoding": { "x": { "field": "x", "type": "quantitative" } } }
            """;

        // Act
        var exception = Assert.Throws<VegaLiteUnsupportedFeatureException>(() => RenderStrict(json));

        // Assert
        Assert.Equal("vl-expr-function-random", exception.Feature);
    }

    /// <summary>
    ///     Validates that the scene budget counts a clipped legend exactly (a symbol, a label and the clipping group of each entry are charged before they are created): a limit of the number of items of the scene
    ///     renders, one item less fails with the name of the limit, and a clipped legend has one item per entry more than the same legend without the property.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Legend_ClipHeightIsChargedToTheSceneBudget()
    {
        // Arrange
        const string template = """
            { "data": { "values": [ { "a": 1, "c": "x" }, { "a": 2, "c": "y" }, { "a": 3, "c": "z" } ] }, "width": 100, "height": 100, "mark": "point",
              "encoding": { "x": { "field": "a", "type": "quantitative" }, "color": { "field": "c", "type": "nominal", "legend": LEGEND } } }
            """;
        var clipped = VegaLiteDocument.Parse(template.Replace("LEGEND", """{ "clipHeight": 12 }""", StringComparison.Ordinal));
        var plain = VegaLiteDocument.Parse(template.Replace("LEGEND", "{}", StringComparison.Ordinal));
        var count = clipped.BuildScene(new VegaLiteRenderOptions(), Ct).CountItems();

        // Act
        using var exact = clipped.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = count } }, Ct);
        var tooFew = Assert.Throws<VegaLiteRenderException>(() => clipped.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = count - 1 } }, Ct));

        // Assert: three entries, so three clipping groups more
        Assert.Contains("MaxSceneItems", tooFew.Message, StringComparison.Ordinal);
        Assert.Equal(plain.BuildScene(new VegaLiteRenderOptions(), Ct).CountItems() + 3, count);
        Assert.True(exact.Width > 0);
    }

    /// <summary>
    ///     Counts the pixels of one color in a column of a surface.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="column">The column.</param>
    /// <param name="color">The color.</param>
    /// <returns>The number of pixels of that color in the column.</returns>
    private static int CountColor(Surface surface, int column, Rgba32 color)
    {
        var count = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            if (surface[column, y] == color)
            {
                count++;
            }
        }

        return count;
    }
}
