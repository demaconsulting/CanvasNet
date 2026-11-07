using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Unit tests of the static axis properties of <see cref="AxisBuilder"/> that Phase 13 draws: <c>translate</c>, <c>position</c>, <c>titleX</c> and <c>titleY</c>, <c>bandPosition</c>,
///     <c>tickBand</c> and <c>tickExtra</c>, and <c>labelBound</c>. The expectations are derived by hand from vega-parser's guide code (<c>axis.js</c>, <c>guide-util.js</c> <c>tickBand</c>,
///     <c>axis-title.js</c>, <c>axis-labels.js</c>), vega-view-transforms' <c>axis.js</c> layout and <c>Overlap.js</c>, and vega-encode's <c>AxisTicks</c>.
/// </summary>
public class AxisBuilderStaticPropertiesTests
{
    /// <summary>A scatter specification whose x domain is 0 to 100 and whose y domain is 0 to 50; the x axis is the one given.</summary>
    private const string LinearSpec = """
        { "data": { "values": [ { "v": 0, "w": 0 }, { "v": 100, "w": 50 } ] },
          "mark": "point",
          "encoding": {
            "x": { "field": "v", "type": "quantitative", "axis": @X },
            "y": { "field": "w", "type": "quantitative", "axis": @Y }
          } }
        """;

    /// <summary>A bar specification with three categories; the band scale of 300 pixels has a step of 100, a bandwidth of 90 and the bands start at 5, 105 and 205.</summary>
    private const string BandSpec = """
        { "data": { "values": [ { "k": "a", "n": 1 }, { "k": "b", "n": 2 }, { "k": "c", "n": 3 } ] },
          "mark": "bar",
          "encoding": {
            "x": { "field": "k", "type": "nominal", "axis": @X },
            "y": { "field": "n", "type": "quantitative" }
          } }
        """;

    /// <summary>
    ///     Builds a linear specification with axis objects on x and y.
    /// </summary>
    /// <param name="x">The JSON text of the x axis.</param>
    /// <param name="y">The JSON text of the y axis.</param>
    /// <returns>The specification.</returns>
    private static string Linear(string x = "{}", string y = "{}") => LinearSpec.Replace("@X", x, StringComparison.Ordinal).Replace("@Y", y, StringComparison.Ordinal);

    /// <summary>
    ///     Builds a band specification with an axis object on x.
    /// </summary>
    /// <param name="x">The JSON text of the x axis.</param>
    /// <returns>The specification.</returns>
    private static string Band(string x) => BandSpec.Replace("@X", x, StringComparison.Ordinal);

    /// <summary>
    ///     Gets the texts of the labels of an axis.
    /// </summary>
    /// <param name="axis">The axis.</param>
    /// <returns>The first line of each label.</returns>
    private static List<string> Labels(AxisResult axis) => [.. ChartTestHelpers.Of<SceneText>(axis.Group, "axis-label").Select(l => l.Lines[0])];

    /// <summary>
    ///     Validates <c>translate</c>: the axis group and its grid group sit at the edge of the data rectangle plus the translate (0.5 by default, Vega's crisp line), for both orientations.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_TranslateReplacesTheHalfPixelOfTheGroup()
    {
        // Arrange / Act
        var plain = ChartTestHelpers.Axis(Linear("{ \"grid\": true }"), "x", 300, 200)!;
        var moved = ChartTestHelpers.Axis(Linear("{ \"grid\": true, \"translate\": 3 }"), "x", 300, 200)!;
        var none = ChartTestHelpers.Axis(Linear("{ \"translate\": 0 }"), "x", 300, 200)!;
        var left = ChartTestHelpers.Axis(Linear(y: "{ \"translate\": 2.5, \"grid\": true }"), "y", 300, 200)!;

        // Assert: the bottom axis is at y = height + translate, x = translate; the grid group follows the axis
        Assert.Equal((0.5, 200.5), (plain.Group.X, plain.Group.Y));
        Assert.Equal((3.0, 203.0), (moved.Group.X, moved.Group.Y));
        Assert.Equal((3.0, 203.0), (moved.Grid!.X, moved.Grid.Y));
        Assert.Equal((0.0, 200.0), (none.Group.X, none.Group.Y));

        // Assert: the left axis is at x = translate (no offset), y = translate
        Assert.Equal((2.5, 2.5), (left.Group.X, left.Group.Y));
        Assert.Equal((2.5, 2.5), (left.Grid!.X, left.Grid.Y));
    }

    /// <summary>
    ///     Validates <c>position</c>: it moves the axis group along the axis (the x of a horizontal axis, the y of a vertical one), the grid with it, and the bounds of the axis by the same distance;
    ///     with the offset it puts the group at the position and the offset.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_PositionMovesTheAxisAlongItself()
    {
        // Arrange / Act
        var plain = ChartTestHelpers.Axis(Linear("{ \"grid\": true }"), "x", 300, 200)!;
        var moved = ChartTestHelpers.Axis(Linear("{ \"grid\": true, \"position\": 10, \"offset\": 4 }"), "x", 300, 200)!;
        var topMoved = ChartTestHelpers.Axis(Linear("{ \"orient\": \"top\", \"position\": -6 }"), "x", 300, 200)!;
        var y = ChartTestHelpers.Axis(Linear(y: "{ \"position\": 15 }"), "y", 300, 200)!;
        var yPlain = ChartTestHelpers.Axis(Linear(), "y", 300, 200)!;
        var right = ChartTestHelpers.Axis(Linear(y: "{ \"orient\": \"right\", \"position\": 8 }"), "y", 300, 200)!;

        // Assert: the bottom axis is at (position + translate, height + offset + translate); the grid group is at the same place
        Assert.Equal((10.5, 204.5), (moved.Group.X, moved.Group.Y));
        Assert.Equal((10.5, 204.5), (moved.Grid!.X, moved.Grid.Y));

        // Assert: the bounds move along the axis by the position (and across it by the offset)
        Assert.Equal(plain.Bounds.X1 + 10, moved.Bounds.X1, 9);
        Assert.Equal(plain.Bounds.X2 + 10, moved.Bounds.X2, 9);
        Assert.Equal(plain.Bounds.Y1 + 4, moved.Bounds.Y1, 9);

        // Assert: the top axis at (position + translate, -offset + translate)
        Assert.Equal((-5.5, 0.5), (topMoved.Group.X, topMoved.Group.Y));

        // Assert: the left axis is moved down by the position; the right axis is at the far edge with the position as its y
        Assert.Equal((0.5, 15.5), (y.Group.X, y.Group.Y));
        Assert.Equal(yPlain.Bounds.Y1 + 15, y.Bounds.Y1, 9);
        Assert.Equal((300.5, 8.5), (right.Group.X, right.Group.Y));
    }

    /// <summary>
    ///     Validates <c>titleX</c> and <c>titleY</c>: each replaces one pixel position of the title in the coordinates of the axis group (the position along the anchor, or the distance from the
    ///     axis), for a horizontal and a vertical axis, and leaves the other position as it was.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_TitleXAndTitleYReplaceThePositionsOfTheTitle()
    {
        // Arrange / Act
        var plain = ChartTestHelpers.Axis(Linear(), "x", 300, 200)!;
        var xMoved = ChartTestHelpers.Axis(Linear("{ \"titleX\": 40 }"), "x", 300, 200)!;
        var yMoved = ChartTestHelpers.Axis(Linear("{ \"titleY\": 30 }"), "x", 300, 200)!;
        var both = ChartTestHelpers.Axis(Linear("{ \"titleX\": -5, \"titleY\": 12 }"), "x", 300, 200)!;
        var verticalPlain = ChartTestHelpers.Axis(Linear(), "y", 300, 200)!;
        var verticalX = ChartTestHelpers.Axis(Linear(y: "{ \"titleX\": -50 }"), "y", 300, 200)!;
        var verticalY = ChartTestHelpers.Axis(Linear(y: "{ \"titleY\": 20 }"), "y", 300, 200)!;
        SceneText Title(AxisResult axis) => Assert.Single(ChartTestHelpers.Of<SceneText>(axis.Group, "axis-title"));

        // Assert: the default title of the bottom axis is centered on the range (150) and below the labels
        var (plainX, plainY) = (Title(plain).X, Title(plain).Y);
        Assert.Equal(150.0, plainX, 9);

        // Assert: titleX moves the title along the axis, titleY moves it away from the axis; the other stays
        Assert.Equal((40.0, plainY), (Title(xMoved).X, Title(xMoved).Y));
        Assert.Equal((plainX, 30.0), (Title(yMoved).X, Title(yMoved).Y));
        Assert.Equal((-5.0, 12.0), (Title(both).X, Title(both).Y));

        // Assert: on the left axis titleX is the distance from the axis and titleY is the position along it (the middle of the range, 100, by default)
        Assert.Equal(100.0, Title(verticalPlain).Y, 9);
        Assert.Equal((-50.0, Title(verticalPlain).Y), (Title(verticalX).X, Title(verticalX).Y));
        Assert.Equal((Title(verticalPlain).X, 20.0), (Title(verticalY).X, Title(verticalY).Y));

        // Assert: the bounds of the axis include the title where it was put
        Assert.True(both.Bounds.Y2 < plain.Bounds.Y2);
    }

    /// <summary>
    ///     Validates <c>bandPosition</c> on a band axis: the ticks and the grid lines are at the start (0) or the end (1) of the bands instead of the middle (0.5), the labels stay in the middle
    ///     of the band (Vega's axis-labels use a band of 0.5), and the default is unchanged. The tick offset of a band axis is -0.5.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_BandPositionMovesTicksAndGridButNotLabels()
    {
        // Arrange / Act: the bands start at 5, 105 and 205 and are 90 wide
        var plain = ChartTestHelpers.Axis(Band("{ \"grid\": true }"), "x", 300, 200)!;
        var start = ChartTestHelpers.Axis(Band("{ \"bandPosition\": 0, \"grid\": true }"), "x", 300, 200)!;
        var end = ChartTestHelpers.Axis(Band("{ \"bandPosition\": 1, \"grid\": true }"), "x", 300, 200)!;

        // Assert: round(start + width * band - 0.5), half up: 5 + 45 - 0.5 = 49.5 -> 50; 4.5 -> 5; 94.5 -> 95
        Assert.Equal([50.0, 150.0, 250.0], plain.Ticks.Select(t => t.Position));
        Assert.Equal([5.0, 105.0, 205.0], start.Ticks.Select(t => t.Position));
        Assert.Equal([95.0, 195.0, 295.0], end.Ticks.Select(t => t.Position));
        Assert.Equal([5.0, 105.0, 205.0], ChartTestHelpers.Of<SceneRule>(start.Grid!, "axis-grid").Select(g => g.X));
        Assert.Equal([5.0, 105.0, 205.0], ChartTestHelpers.Of<SceneRule>(start.Group, "axis-tick").Select(g => g.X));

        // Assert: the labels are at the center of the band, 5 + 45 - 0.5, whatever the band position
        Assert.Equal([49.5, 149.5, 249.5], ChartTestHelpers.Of<SceneText>(start.Group, "axis-label").Select(l => l.X));
        Assert.Equal([49.5, 149.5, 249.5], ChartTestHelpers.Of<SceneText>(end.Group, "axis-label").Select(l => l.X));
    }

    /// <summary>
    ///     Validates <c>tickBand: "extent"</c> (the ticks at the end of the bands, no offset, and an extra tick at the start of the first band, so the ticks mark the extents of the bands) and
    ///     <c>"center"</c> (the middle, no extra tick, which wins over <c>bandPosition</c> and <c>tickExtra</c>), with the grid following the ticks and the extra tick having no label.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_TickBandExtentAddsAnExtraTickAtTheStartOfTheFirstBand()
    {
        // Arrange / Act
        var extent = ChartTestHelpers.Axis(Band("{ \"tickBand\": \"extent\", \"grid\": true }"), "x", 300, 200)!;
        var center = ChartTestHelpers.Axis(Band("{ \"tickBand\": \"center\", \"bandPosition\": 1, \"tickExtra\": true }"), "x", 300, 200)!;

        // Assert: round(start + 90) = 95, 195, 295 and the extra tick at round(5) = 5; the ticks come with the extra one last, without a label
        Assert.Equal([95.0, 195.0, 295.0, 5.0], extent.Ticks.Select(t => t.Position));
        Assert.Equal(["a", "b", "c", string.Empty], extent.Ticks.Select(t => t.Label));
        Assert.Equal([false, false, false, true], extent.Ticks.Select(t => t.IsExtra));
        Assert.Equal(4, ChartTestHelpers.Of<SceneRule>(extent.Group, "axis-tick").Count);
        Assert.Equal([95.0, 195.0, 295.0, 5.0], ChartTestHelpers.Of<SceneRule>(extent.Grid!, "axis-grid").Select(g => g.X));

        // Assert: the labels are only the three categories, at the center of the bands with the offset of tickBand extent (0): 5 + 45
        var labels = ChartTestHelpers.Of<SceneText>(extent.Group, "axis-label");
        Assert.Equal(["a", "b", "c"], labels.Select(l => l.Lines[0]));
        Assert.Equal([50.0, 150.0, 250.0], labels.Select(l => l.X));

        // Assert: "center" is the middle with the offset of the axis and no extra tick, whatever the other properties say
        Assert.Equal([50.0, 150.0, 250.0], center.Ticks.Select(t => t.Position));
        Assert.DoesNotContain(center.Ticks, t => t.IsExtra);
    }

    /// <summary>
    ///     Validates <c>tickExtra</c> without a tick band: the extra tick is at the start of the first band with the tick offset (-0.5) and needs a band position that is not 0 (the position
    ///     of the extra tick is not computed without a band, which Vega does not draw), and it also works for a continuous scale, where it repeats the first tick.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_TickExtraNeedsABandPosition()
    {
        // Arrange / Act
        var withBand = ChartTestHelpers.Axis(Band("{ \"bandPosition\": 1, \"tickExtra\": true }"), "x", 300, 200)!;
        var middle = ChartTestHelpers.Axis(Band("{ \"tickExtra\": true }"), "x", 300, 200)!;
        var zero = ChartTestHelpers.Axis(Band("{ \"bandPosition\": 0, \"tickExtra\": true }"), "x", 300, 200)!;
        var linear = ChartTestHelpers.Axis(Linear("{ \"tickExtra\": true }"), "x", 300, 200)!;
        var linearPlain = ChartTestHelpers.Axis(Linear(), "x", 300, 200)!;

        // Assert: the extra tick at round(5 - 0.5) = 5 (4.5 rounds up)
        Assert.Equal([95.0, 195.0, 295.0, 5.0], withBand.Ticks.Select(t => t.Position));
        Assert.Equal([50.0, 150.0, 250.0, 5.0], middle.Ticks.Select(t => t.Position));
        Assert.Equal([5.0, 105.0, 205.0], zero.Ticks.Select(t => t.Position));

        // Assert: a continuous axis gets one more tick at the position of its first tick (a band of 0.5 times a bandwidth of 0)
        Assert.Equal(linearPlain.Ticks.Count + 1, linear.Ticks.Count);
        Assert.Equal(linear.Ticks[0].Position, linear.Ticks[^1].Position);
        Assert.True(linear.Ticks[^1].IsExtra);
    }

    /// <summary>
    ///     Validates <c>labelBound</c> on a horizontal axis: with <c>true</c> a label whose box passes the range of the axis by more than one pixel is hidden (the centered "0" and "100" of an axis
    ///     without flushing), a number is the tolerance in pixels, and it does nothing without a label overlap method (Vega's Overlap transform returns before the bound test), for a value of
    ///     false, 0 or a negative number.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_LabelBoundHidesLabelsThatPassTheRange()
    {
        // Arrange / Act: the x range is 0 to 300 and the ticks 0, 10, ..., 100 are 30 pixels apart; "0" and "100" are centered on the ends (labelFlush is off)
        const string flat = "\"labelFlush\": false";
        var all = ChartTestHelpers.Axis(Linear("{ " + flat + " }"), "x", 300, 200)!;
        var bound = ChartTestHelpers.Axis(Linear("{ " + flat + ", \"labelBound\": true }"), "x", 300, 200)!;
        var four = ChartTestHelpers.Axis(Linear("{ " + flat + ", \"labelBound\": 4 }"), "x", 300, 200)!;
        var wide = ChartTestHelpers.Axis(Linear("{ " + flat + ", \"labelBound\": 10 }"), "x", 300, 200)!;
        var noOverlap = ChartTestHelpers.Axis(Linear("{ " + flat + ", \"labelBound\": true, \"labelOverlap\": false }"), "x", 300, 200)!;
        var off = ChartTestHelpers.Axis(Linear("{ " + flat + ", \"labelBound\": false }"), "x", 300, 200)!;
        var zero = ChartTestHelpers.Axis(Linear("{ " + flat + ", \"labelBound\": 0 }"), "x", 300, 200)!;
        var negative = ChartTestHelpers.Axis(Linear("{ " + flat + ", \"labelBound\": -1 }"), "x", 300, 200)!;

        // Assert: eleven labels without a bound; with a tolerance of 1 pixel the half width of "0" (about 3 pixels) and of "100" (about 8) pass it
        Assert.Equal(["0", "10", "20", "30", "40", "50", "60", "70", "80", "90", "100"], Labels(all));
        Assert.Equal(["10", "20", "30", "40", "50", "60", "70", "80", "90"], Labels(bound));

        // Assert: with 4 pixels the "0" stays and the "100" goes; with 10 both stay
        Assert.Equal(["0", "10", "20", "30", "40", "50", "60", "70", "80", "90"], Labels(four));
        Assert.Equal(Labels(all), Labels(wide));

        // Assert: nothing is hidden without a label overlap method, or for false, 0 and a negative tolerance
        Assert.Equal(Labels(all), Labels(noOverlap));
        Assert.Equal(Labels(all), Labels(off));
        Assert.Equal(Labels(all), Labels(zero));
        Assert.Equal(Labels(all), Labels(negative));
    }

    /// <summary>
    ///     Validates <c>labelBound</c> on a vertical axis: the boxes of the labels are bound along y, so the "0" at the bottom and the "50" at the top of the range, which pass the range by half
    ///     their height (about 5 pixels), are hidden with a tolerance of 1.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_LabelBoundAppliesAlongYForAVerticalAxis()
    {
        // Arrange / Act
        var plain = ChartTestHelpers.Axis(Linear(), "y", 300, 200)!;
        var bound = ChartTestHelpers.Axis(Linear(y: "{ \"labelBound\": true }"), "y", 300, 200)!;

        // Assert
        Assert.Equal(["0", "10", "20", "30", "40", "50"], Labels(plain));
        Assert.Equal(["10", "20", "30", "40"], Labels(bound));
    }
}
