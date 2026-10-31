using DemaConsulting.CanvasNet.VegaLite.Marks;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Marks;

/// <summary>
///     Unit tests of the polar position of the text mark of <see cref="MarkBuilder"/> that goes with the arc mark: the label of a stacked <c>theta</c> or <c>radius</c> sits in the middle of its
///     slice, the <c>bandPosition</c> of the channel moves it, and the anchor is the center moved by <c>radius</c> along <c>theta</c> (clockwise from twelve o'clock) and then by <c>dx</c> and
///     <c>dy</c>. The expectations are derived by hand from <c>x = cx + r sin(theta)</c> and <c>y = cy - r cos(theta)</c>.
/// </summary>
public class MarkBuilderPolarTextTests
{
    /// <summary>The tolerance of the comparisons of positions that go through trigonometry.</summary>
    private const double Tolerance = 1e-9;

    /// <summary>The data of a pie of three slices with the values 1, 1 and 2 (a quarter, a quarter and a half).</summary>
    private const string PieRows = """[ { "k": "a", "v": 1 }, { "k": "b", "v": 1 }, { "k": "c", "v": 2 } ]""";

    /// <summary>
    ///     Builds the labels of a text mark in a 200 by 200 view.
    /// </summary>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="encoding">The JSON text of the encoding object.</param>
    /// <param name="values">The JSON text of the data values array.</param>
    /// <returns>The labels in drawing order.</returns>
    private static List<SceneText> Labels(string mark, string encoding, string values) =>
        ChartTestHelpers.Of<SceneText>(
            ChartTestHelpers.Marks(
                "{ \"width\": 200, \"height\": 200, \"data\": { \"values\": " + values + " }, \"mark\": " + mark + ", \"encoding\": " + encoding + " }",
                200,
                200),
            "mark-text");

    /// <summary>
    ///     Builds the labels of the text mark of a pie: the value stacked on <c>theta</c> (with a <c>bandPosition</c> when given), the name as the color and the text, and a radius of 60 pixels.
    /// </summary>
    /// <param name="bandPosition">The <c>bandPosition</c> member of the <c>theta</c> channel (with its leading comma), or an empty text.</param>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <returns>The labels in drawing order.</returns>
    private static List<SceneText> PieLabels(string bandPosition = "", string mark = "\"text\"") =>
        Labels(
            mark,
            "{ \"theta\": { \"field\": \"v\", \"type\": \"quantitative\", \"stack\": true" + bandPosition + " }, \"radius\": { \"value\": 60 }, \"color\": { \"field\": \"k\", \"type\": \"nominal\" }, "
            + "\"text\": { \"field\": \"k\", \"type\": \"nominal\" } }",
            PieRows);

    /// <summary>
    ///     Asserts the position of a text.
    /// </summary>
    /// <param name="item">The text.</param>
    /// <param name="x">The expected x coordinate.</param>
    /// <param name="y">The expected y coordinate.</param>
    private static void AssertAt(SceneText item, double x, double y)
    {
        Assert.Equal(x, item.X, Tolerance);
        Assert.Equal(y, item.Y, Tolerance);
    }

    /// <summary>
    ///     Validates the labels of a pie: the stacked <c>theta</c> puts each label at the middle of its slice (the stack <c>[0, 1]</c>, <c>[1, 2]</c>, <c>[2, 4]</c> of the domain <c>[0, 4]</c> gives the
    ///     angles PI/4, 3 PI/4 and 3 PI/2), and the <c>bandPosition</c> of the channel moves a label from the start of its slice (0) to its end (1).
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_PieLabelsSitAtTheMiddleOfTheirSlice()
    {
        // Act: the centre is (100, 100) and the radius 60; the anchor is (100 + 60 sin(theta), 100 - 60 cos(theta))
        var labels = PieLabels();

        // Assert: a at PI/4 (upper right), b at 3 PI/4 (lower right), c at 3 PI/2 (left)
        var diagonal = 60 * Math.Sqrt(0.5);
        Assert.Equal(["a", "b", "c"], labels.Select(l => l.Text));
        AssertAt(labels[0], 100 + diagonal, 100 - diagonal);
        AssertAt(labels[1], 100 + diagonal, 100 + diagonal);
        AssertAt(labels[2], 40, 100);

        // Act: band position 0 is the start of each slice (0, PI/2, PI), 1 is its end (PI/2, PI, 2 PI)
        var starts = PieLabels(", \"bandPosition\": 0");
        var ends = PieLabels(", \"bandPosition\": 1");

        // Assert: start of a: straight up; start of b: right; start of c: straight down; the ends are the starts of the next slices
        AssertAt(starts[0], 100, 40);
        AssertAt(starts[1], 160, 100);
        AssertAt(starts[2], 100, 160);
        AssertAt(ends[0], 160, 100);
        AssertAt(ends[1], 100, 160);
        AssertAt(ends[2], 100, 40);

        // Act: a quarter of the way through the slice, interpolated in the data: a is 0.25 of [0, 1], b 1.25 of [1, 2], c 2.5 of [2, 4]: angles PI/8, 5 PI/8, 5 PI/4
        var quarter = PieLabels(", \"bandPosition\": 0.25");

        // Assert
        AssertAt(quarter[0], 100 + (60 * Math.Sin(Math.PI / 8)), 100 - (60 * Math.Cos(Math.PI / 8)));
        AssertAt(quarter[1], 100 + (60 * Math.Sin(5 * Math.PI / 8)), 100 - (60 * Math.Cos(5 * Math.PI / 8)));
        AssertAt(quarter[2], 100 + (60 * Math.Sin(5 * Math.PI / 4)), 100 - (60 * Math.Cos(5 * Math.PI / 4)));

        // Assert: the item keeps dx and dy apart from the anchor, which they move afterwards
        var moved = PieLabels(mark: """{ "type": "text", "dx": 5, "dy": -3 }""");
        AssertAt(moved[0], 100 + diagonal, 100 - diagonal);
        Assert.All(moved, l => Assert.Equal((5.0, -3.0), (l.Dx, l.Dy)));
    }

    /// <summary>
    ///     Validates the polar offset of a label: an unstacked <c>theta</c> is its scaled value, a stacked <c>radius</c> is the middle of its ring, constants of the channels and of the mark
    ///     properties are used as they are, and a row without a valid value is not drawn.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_PolarTextOffsetsFollowThetaAndRadius()
    {
        // Act: an unstacked theta on the domain [0, 4] is 2 PI v / 4: the value 1 is the angle PI/2, so a radius of 50 puts the label 50 pixels right of the center (100, 100); the value 4 is the full turn, straight up
        var turns = Labels(
            "\"text\"",
            """{ "theta": { "field": "v", "type": "quantitative", "stack": false }, "radius": { "value": 50 }, "text": { "value": "r" } }""",
            """[ { "v": 1 }, { "v": 4 } ]""");
        Assert.Equal(2, turns.Count);
        AssertAt(turns[0], 150, 100);
        AssertAt(turns[1], 100, 50);

        // Act: a stacked radius is the middle of its ring: the domain [0, 4] runs over [0, 100] (half of the side), so a is 0.5 -> 12.5, b is 1.5 -> 37.5, c is 3 -> 75; the angle is the value PI/2
        var rings = Labels(
            "\"text\"",
            """
            { "radius": { "field": "v", "type": "quantitative", "stack": true }, "theta": { "value": 1.5707963267948966 }, "color": { "field": "k", "type": "nominal" },
              "text": { "field": "k", "type": "nominal" } }
            """,
            PieRows);

        // Assert
        AssertAt(rings[0], 112.5, 100);
        AssertAt(rings[1], 137.5, 100);
        AssertAt(rings[2], 175, 100);

        // Act: the mark properties are the constants of absent channels: theta PI (straight down) and radius 30
        var down = Assert.Single(Labels("""{ "type": "text", "theta": 3.141592653589793, "radius": 30, "text": "d" }""", "{ }", """[ { "v": 1 } ]"""));

        // Assert
        AssertAt(down, 100, 130);

        // Act: a zero radius leaves the anchor at the center whatever the angle; the row without a number is not drawn
        var center = Labels(
            "\"text\"",
            """{ "theta": { "field": "v", "type": "quantitative", "stack": false }, "radius": { "value": 0 }, "text": { "value": "z" } }""",
            """[ { "v": 1 }, { "v": null } ]""");

        // Assert
        AssertAt(Assert.Single(center), 100, 100);
    }
}
