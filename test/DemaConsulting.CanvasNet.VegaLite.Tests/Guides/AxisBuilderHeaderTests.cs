using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Unit tests of <see cref="AxisBuilder"/> for the axes that the cells of a facet share and that live in its headers and footers
///     (<see cref="AxisRequest.InHeader"/>): the axis group moves to the origin of the header group, the grid stays where the cell needs it.
/// </summary>
public class AxisBuilderHeaderTests
{
    /// <summary>A scatter specification whose x domain is 0 to 100 and whose y domain is 0 to 50.</summary>
    private const string LinearSpec = """
        { "data": { "values": [ { "v": 0, "w": 0 }, { "v": 100, "w": 50 } ] },
          "mark": "point",
          "encoding": {
            "x": { "field": "v", "type": "quantitative" },
            "y": { "field": "w", "type": "quantitative" }
          } }
        """;

    /// <summary>
    ///     Builds a linear specification that puts an axis object on a channel.
    /// </summary>
    /// <param name="channel">The channel, <c>x</c> or <c>y</c>.</param>
    /// <param name="axis">The JSON text of the <c>axis</c> property value.</param>
    /// <returns>The specification text.</returns>
    private static string WithAxis(string channel, string axis)
    {
        var field = channel == "x" ? "v" : "w";
        return LinearSpec.Replace(
            "\"" + channel + "\": { \"field\": \"" + field + "\", \"type\": \"quantitative\" }",
            "\"" + channel + "\": { \"field\": \"" + field + "\", \"type\": \"quantitative\", \"axis\": " + axis + " }",
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a bottom axis of a header sits at the origin of the header group instead of below the data rectangle, with the same ticks, labels and domain, and
    ///     with bounds that are those of the axis next to its data rectangle moved up by the height of the data rectangle.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_InHeaderPlacesBottomAxisAtOriginAndKeepsExtents()
    {
        // Arrange / Act: a 300 by 200 data rectangle; next to it the bottom axis group is at (0.5, 200 + 0.5), in a footer at (0.5, 0 + 0.5)
        var beside = ChartTestHelpers.Axis(LinearSpec, "x", 300, 200)!;
        var header = ChartTestHelpers.Axis(LinearSpec, "x", 300, 200, inHeader: true)!;

        // Assert: the group moved by the 200 pixels of the data height, nothing else changed
        Assert.Equal((0.5, 200.5), (beside.Group.X, beside.Group.Y));
        Assert.Equal((0.5, 0.5), (header.Group.X, header.Group.Y));
        Assert.False(beside.InHeader);
        Assert.True(header.InHeader);
        Assert.Equal("bottom", header.Orient);
        Assert.Equal(beside.Ticks.Select(t => (t.Position, t.Label)), header.Ticks.Select(t => (t.Position, t.Label)));
        Assert.Equal(beside.ItemCount, header.ItemCount);

        // Assert: the children are laid out relative to the group, so they are the same items; the domain spans the 300 pixels of the width
        var ticks = ChartTestHelpers.Of<SceneRule>(header.Group, "axis-tick");
        Assert.Equal(11, ticks.Count);
        Assert.All(ticks, t => Assert.Equal((0.0, 5.0), (t.Y, t.Y2)));
        var domain = Assert.Single(ChartTestHelpers.Of<SceneRule>(header.Group, "axis-domain"));
        Assert.Equal((0.0, 0.0, 300.0, 0.0), (domain.X, domain.Y, domain.X2, domain.Y2));

        // Assert: the extent is the same, in the coordinates of the header group
        Assert.Equal(beside.Bounds.X1, header.Bounds.X1, 9);
        Assert.Equal(beside.Bounds.X2, header.Bounds.X2, 9);
        Assert.Equal(beside.Bounds.Y1 - 200, header.Bounds.Y1, 9);
        Assert.Equal(beside.Bounds.Y2 - 200, header.Bounds.Y2, 9);
        Assert.Equal(header.Bounds.Height, beside.Bounds.Height, 9);
    }

    /// <summary>
    ///     Validates that the axes on the far sides (bottom, right) move to the origin and the near sides (top, left) stay, whatever the offset of the axis is, so a header
    ///     and a footer both start where the cell's edge would be if the cell had no extent across the axis.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_InHeaderRowAxisKeepsHeight()
    {
        // Arrange / Act: a left axis (the row header of a facet) and a right axis (a row footer) with an offset of 4, next to the 300 by 200 data rectangle and in a header
        var leftBeside = ChartTestHelpers.Axis(WithAxis("y", "{ \"offset\": 4 }"), "y", 300, 200)!;
        var leftHeader = ChartTestHelpers.Axis(WithAxis("y", "{ \"offset\": 4 }"), "y", 300, 200, inHeader: true)!;
        var rightBeside = ChartTestHelpers.Axis(WithAxis("y", "{ \"orient\": \"right\", \"offset\": 4 }"), "y", 300, 200)!;
        var rightHeader = ChartTestHelpers.Axis(WithAxis("y", "{ \"orient\": \"right\", \"offset\": 4 }"), "y", 300, 200, inHeader: true)!;

        // Assert: the left axis is -offset left of the origin both ways (-4 + 0.5); the right axis is at width + offset next to the cell (304.5) and at offset in a footer (4.5)
        Assert.Equal("left", leftHeader.Orient);
        Assert.Equal((-3.5, 0.5), (leftBeside.Group.X, leftBeside.Group.Y));
        Assert.Equal((-3.5, 0.5), (leftHeader.Group.X, leftHeader.Group.Y));
        Assert.Equal("right", rightHeader.Orient);
        Assert.Equal((304.5, 0.5), (rightBeside.Group.X, rightBeside.Group.Y));
        Assert.Equal((4.5, 0.5), (rightHeader.Group.X, rightHeader.Group.Y));

        // Assert: the height the axis reaches along the cell is 200 either way; the right axis bounds moved left by the 300 of the width
        Assert.Equal(leftBeside.Bounds.Height, leftHeader.Bounds.Height, 9);
        Assert.Equal(rightBeside.Bounds.Height, rightHeader.Bounds.Height, 9);
        Assert.Equal(rightBeside.Bounds.X1 - 300, rightHeader.Bounds.X1, 9);
        Assert.Equal(rightBeside.Bounds.X2 - 300, rightHeader.Bounds.X2, 9);
        Assert.Equal(leftBeside.Bounds.X1, leftHeader.Bounds.X1, 9);
        Assert.Equal(leftBeside.Bounds.X2, leftHeader.Bounds.X2, 9);
    }

    /// <summary>
    ///     Validates that the grid of a header axis is still built, in the coordinates of the data rectangle where the cell draws it, with the lines of the axis next to the cell.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_InHeaderStillReturnsTheGrid()
    {
        // Arrange / Act
        var beside = ChartTestHelpers.Axis(LinearSpec, "x", 300, 200)!;
        var header = ChartTestHelpers.Axis(LinearSpec, "x", 300, 200, inHeader: true)!;

        // Assert: the grid group is at the cell's bottom edge (200 + 0.5) and the lines run up the 200 pixels, one per tick (11 ticks at 3 pixels per unit)
        var grid = Assert.IsType<SceneGroup>(header.Grid);
        Assert.Equal(("axis-grid-group", 0.5, 200.5), (grid.Role, grid.X, grid.Y));
        var lines = ChartTestHelpers.Of<SceneRule>(grid, "axis-grid");
        Assert.Equal(11, lines.Count);
        Assert.All(lines, l => Assert.Equal((0.0, -200.0), (Math.Abs(l.Y), l.Y2)));
        Assert.Equal(Enumerable.Range(0, 11).Select(i => 30.0 * i), lines.Select(l => l.X));

        // Assert: the axis group itself has no grid lines, only ticks, labels, domain and title
        Assert.DoesNotContain(header.Group.Children, c => c.Role == "axis-grid");
        Assert.Equal(beside.Grid!.Children.Count, grid.Children.Count);
    }

    /// <summary>
    ///     Validates that a top axis and the offset of a bottom axis in a header: the axis is placed by its offset from the origin of the header group.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_InHeaderKeepsTheOffsetFromTheHeaderOrigin()
    {
        // Arrange / Act: an offset of 4 puts a top axis 4 above the origin (y = -4 + 0.5) and a bottom axis 4 below it (4 + 0.5), where next to a 200 high cell it would be at 204.5
        var top = ChartTestHelpers.Axis(WithAxis("x", "{ \"orient\": \"top\", \"offset\": 4 }"), "x", 300, 200, inHeader: true)!;
        var bottom = ChartTestHelpers.Axis(WithAxis("x", "{ \"offset\": 4 }"), "x", 300, 200, inHeader: true)!;
        var bottomBeside = ChartTestHelpers.Axis(WithAxis("x", "{ \"offset\": 4 }"), "x", 300, 200)!;

        // Assert
        Assert.Equal((0.5, -3.5), (top.Group.X, top.Group.Y));
        Assert.Equal((0.5, 4.5), (bottom.Group.X, bottom.Group.Y));
        Assert.Equal((0.5, 204.5), (bottomBeside.Group.X, bottomBeside.Group.Y));
        Assert.Equal(bottomBeside.Bounds.Y1 - 200, bottom.Bounds.Y1, 9);
    }
}
