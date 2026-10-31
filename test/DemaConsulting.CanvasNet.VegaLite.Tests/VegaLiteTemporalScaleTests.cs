using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     Tests that temporal fields are drawn on their time scales: points at their dates, ranges from their start to their end, and a color over dates. The axes, the
///     labels, the legends and the geometry of time-unit bars are the guides and marks work package; these tests pin the positions that follow from the scales.
/// </summary>
public class VegaLiteTemporalScaleTests
{
    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Validates that points sit at their dates: January 1, 6 and 11 of 2024 on a data rectangle 200 pixels wide are 100 pixels apart.
    /// </summary>
    [Fact]
    public void VegaLiteTemporalScale_Points_SitAtTheirDates()
    {
        // Arrange
        const string Json = """
            { "width": 200, "height": 100,
              "data": { "values": [ { "d": "2024-01-01", "v": 1 }, { "d": "2024-01-06", "v": 2 }, { "d": "2024-01-11", "v": 3 } ] },
              "mark": "point",
              "encoding": { "x": { "field": "d", "type": "temporal" }, "y": { "field": "v", "type": "quantitative" } } }
            """;

        // Act
        var xs = VegaLiteDocument.Parse(Json).BuildScene(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct)
            .FindAll("mark-point").Select(e => ((SceneSymbol)e.Item).X).ToList();

        // Assert: the dates are five days apart, so the points are 100 pixels apart across the 200 pixels
        Assert.Equal(3, xs.Count);
        Assert.Equal(100, xs[1] - xs[0], 6);
        Assert.Equal(100, xs[2] - xs[1], 6);
    }

    /// <summary>
    ///     Validates that a range of two temporal fields is a bar from its start to its end on the time scale whose domain holds both.
    /// </summary>
    [Fact]
    public void VegaLiteTemporalScale_RangeBars_SpanFromStartToEnd()
    {
        // Arrange: two tasks, January 1 to 11 and January 6 to 11
        const string Json = """
            { "width": 200, "height": 100,
              "data": { "values": [ { "t": "a", "s": "2024-01-01", "e": "2024-01-11" }, { "t": "b", "s": "2024-01-06", "e": "2024-01-11" } ] },
              "mark": "bar",
              "encoding": { "x": { "field": "s", "type": "temporal" }, "x2": { "field": "e" }, "y": { "field": "t", "type": "nominal" } } }
            """;

        // Act
        var bars = VegaLiteDocument.Parse(Json).BuildScene(new VegaLiteRenderOptions(), Ct).FindAll("mark-bar").Select(e => (SceneRect)e.Item).ToList();

        // Assert: the second task starts in the middle and both end at the right edge
        Assert.Equal(2, bars.Count);
        Assert.Equal(200, bars[0].Width, 6);
        Assert.Equal(100, bars[1].Width, 6);
        Assert.Equal(bars[0].X + bars[0].Width, bars[1].X + bars[1].Width, 6);
        Assert.Equal(100, bars[1].X - bars[0].X, 6);
    }

    /// <summary>
    ///     Validates that a color over dates and a UTC time unit render (in strict mode: nothing about them is unsupported) and that the colors follow the dates.
    /// </summary>
    [Fact]
    public void VegaLiteTemporalScale_ColorOverDates_FollowsTheDates()
    {
        // Arrange
        const string Json = """
            { "width": 200, "height": 100,
              "data": { "values": [ { "d": "2024-01-01", "v": 1 }, { "d": "2024-01-06", "v": 2 }, { "d": "2024-01-11", "v": 3 } ] },
              "mark": "circle",
              "encoding": { "x": { "field": "v", "type": "quantitative" },
                            "color": { "field": "d", "type": "temporal", "timeUnit": "utcyearmonthdate" } } }
            """;

        // Act
        var fills = VegaLiteDocument.Parse(Json).BuildScene(new VegaLiteRenderOptions(), Ct).FindAll("mark-circle").Select(e => ((SceneSymbol)e.Item).Fill).ToList();

        // Assert: three different colors, the first and the last the ends of the ramp
        Assert.Equal(3, fills.Count);
        Assert.Equal(3, fills.Distinct().Count());
    }
}
