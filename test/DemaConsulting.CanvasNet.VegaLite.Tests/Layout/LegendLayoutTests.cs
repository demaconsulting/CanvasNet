using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Layout;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Layout;

/// <summary>
///     Unit tests of <see cref="LegendLayout"/>: where legends of each orient are placed around a data rectangle of 200 by 100 pixels, how
///     several legends of one orient are stacked, how the configuration changes it, and how far the legends reach. Every position is
///     derived by hand from the rules of Vega's legend layout (offset 18, margin 8, anchor start, direction vertical for the left and right
///     side), with synthetic legend boxes, so the tests do not depend on the text measurements.
/// </summary>
public class LegendLayoutTests
{
    /// <summary>The width of the data rectangle in these tests.</summary>
    private const double DataWidth = 200;

    /// <summary>The height of the data rectangle in these tests.</summary>
    private const double DataHeight = 100;

    /// <summary>The data rectangle without axes.</summary>
    private static readonly SceneBounds Data = new(0, 0, DataWidth, DataHeight);

    /// <summary>
    ///     Creates the effective configuration of a specification with a configuration text.
    /// </summary>
    /// <param name="config">The <c>config</c> object as JSON text, or null for the defaults.</param>
    /// <returns>The configuration.</returns>
    private static ResolvedConfig Config(string? config = null) =>
        ConfigResolver.Resolve(SpecParser.Parse(config is null ? "{ \"mark\": \"point\" }" : "{ \"mark\": \"point\", \"config\": " + config + " }", "test.vl.json"), null);

    /// <summary>
    ///     Creates a legend of a given size with no content.
    /// </summary>
    /// <param name="orient">The orient.</param>
    /// <param name="width">The width of the box.</param>
    /// <param name="height">The height of the box.</param>
    /// <param name="offset">The offset the legend asks for, or null.</param>
    /// <param name="x">The <c>legendX</c>.</param>
    /// <param name="y">The <c>legendY</c>.</param>
    /// <param name="bounds">The bounds, or null for the box.</param>
    /// <returns>The legend.</returns>
    private static LegendResult Box(string orient, double width, double height, double? offset = null, double x = 0, double y = 0, SceneBounds? bounds = null) =>
        new(LegendKind.Symbol, orient, offset, x, y, width, height, bounds ?? new SceneBounds(0, 0, width, height), [], new LegendFrame(null, null, null, null, 0), 1);

    /// <summary>
    ///     Places legends against the bare data rectangle.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="legends">The legends.</param>
    /// <returns>The placement.</returns>
    private static LegendPlacement Place(ResolvedConfig config, params LegendResult[] legends) =>
        LegendLayout.Place(legends, config, Data, Data, DataWidth, DataHeight);

    /// <summary>
    ///     Gets the positions of the groups of a placement.
    /// </summary>
    /// <param name="placement">The placement.</param>
    /// <returns>The x and y position of each group.</returns>
    private static List<(double X, double Y)> Positions(LegendPlacement placement) => [.. placement.Groups.Select(g => (g.X, g.Y))];

    /// <summary>
    ///     Validates that a right legend starts 18 pixels right of the data rectangle at its top and that the groups are legend groups.
    /// </summary>
    [Fact]
    public void LegendLayout_Place_RightLegendIsOffsetFromTheDataRectangle()
    {
        // Act
        var placement = Place(Config(), Box("right", 50, 40));

        // Assert
        Assert.Equal([(218.0, 0.0)], Positions(placement));
        Assert.Equal("legend", Assert.Single(placement.Groups).Role);
        Assert.Equal(new SceneBounds(218, 0, 268, 40), placement.Bounds);
    }

    /// <summary>
    ///     Validates that two right legends form one column with 8 pixels between the boxes, that the second keeps the left edge of the
    ///     first, and that the group reaches the widest legend.
    /// </summary>
    [Fact]
    public void LegendLayout_Place_RightLegendsStackInAColumn()
    {
        // Act
        var placement = Place(Config(), Box("right", 50, 40), Box("right", 60, 30));

        // Assert: the second box starts at 40 + 8
        Assert.Equal([(218.0, 0.0), (218.0, 48.0)], Positions(placement));
        Assert.Equal(new SceneBounds(218, 0, 278, 78), placement.Bounds);
    }

    /// <summary>
    ///     Validates that the margin and the direction come from the configuration of the layout, and that the object of an orient wins
    ///     over the layout object.
    /// </summary>
    [Fact]
    public void LegendLayout_Place_LayoutConfigurationOfTheOrientWins()
    {
        // Arrange
        var margin = Config("{ \"legend\": { \"layout\": { \"margin\": 20 } } }");
        var orient = Config("{ \"legend\": { \"layout\": { \"right\": { \"direction\": \"horizontal\", \"offset\": 4 } } } }");

        // Act
        var wide = Place(margin, Box("right", 50, 40), Box("right", 60, 30));
        var row = Place(orient, Box("right", 50, 40), Box("right", 60, 30));

        // Assert: a margin of 20 under the first box; one row with the offset of the orient: 204, then 204 + 50 + 8
        Assert.Equal([(218.0, 0.0), (218.0, 60.0)], Positions(wide));
        Assert.Equal([(204.0, 0.0), (262.0, 0.0)], Positions(row));
    }

    /// <summary>
    ///     Validates that a left legend ends 18 pixels left of the data rectangle (its right edge is anchored), and that the reach shows on
    ///     the left of the view.
    /// </summary>
    [Fact]
    public void LegendLayout_Place_LeftLegendEndsLeftOfTheDataRectangle()
    {
        // Act
        var placement = Place(Config(), Box("left", 50, 40));
        var extents = LegendLayout.Combine(new ViewExtents(0, 0, 0, 0), placement.Bounds, DataWidth, DataHeight);

        // Assert: x = 0 - 18 - 50
        Assert.Equal([(-68.0, 0.0)], Positions(placement));
        Assert.Equal(new ViewExtents(68, 0, 0, 0), extents);
    }

    /// <summary>
    ///     Validates that top legends are one row above the data rectangle and that bottom legends are one row below it, the first at the
    ///     start and the next 8 pixels after it.
    /// </summary>
    [Fact]
    public void LegendLayout_Place_TopAndBottomLegendsFormRows()
    {
        // Act
        var top = Place(Config(), Box("top", 50, 40));
        var bottom = Place(Config(), Box("bottom", 50, 40), Box("bottom", 60, 30));

        // Assert: the top row ends 18 above (y = -18 - 40); the bottom row starts 18 below the bottom edge (100 + 18)
        Assert.Equal([(0.0, -58.0)], Positions(top));
        Assert.Equal([(0.0, 118.0), (58.0, 118.0)], Positions(bottom));
        Assert.Equal(new SceneBounds(0, 118, 118, 158), bottom.Bounds);
    }

    /// <summary>
    ///     Validates the anchor of the configuration: the middle centers the column on the data rectangle and the end puts its bottom there.
    /// </summary>
    [Fact]
    public void LegendLayout_Place_AnchorMovesTheLegendAlongTheSide()
    {
        // Arrange
        var middle = Config("{ \"legend\": { \"layout\": { \"anchor\": \"middle\" } } }");
        var end = Config("{ \"legend\": { \"layout\": { \"anchor\": \"end\" } } }");

        // Act
        var centered = Place(middle, Box("right", 50, 40));
        var bottomed = Place(end, Box("right", 50, 40));
        var horizontal = Place(middle, Box("top", 60, 30));

        // Assert: the column of 40 around y = 50, then the bottom at y = 100; a row of 60 centered at x = 100
        Assert.Equal([(218.0, 30.0)], Positions(centered));
        Assert.Equal([(218.0, 60.0)], Positions(bottomed));
        Assert.Equal([(70.0, -48.0)], Positions(horizontal));
    }

    /// <summary>
    ///     Validates the inner orients: they sit 18 pixels inside the corners of the data rectangle.
    /// </summary>
    [Fact]
    public void LegendLayout_Place_InnerOrientsSitInTheCorners()
    {
        // Act
        var topLeft = Place(Config(), Box("top-left", 50, 40));
        var topRight = Place(Config(), Box("top-right", 50, 40));
        var bottomLeft = Place(Config(), Box("bottom-left", 50, 40));
        var bottomRight = Place(Config(), Box("bottom-right", 50, 40));

        // Assert
        Assert.Equal([(18.0, 18.0)], Positions(topLeft));
        Assert.Equal([(132.0, 18.0)], Positions(topRight));
        Assert.Equal([(18.0, 42.0)], Positions(bottomLeft));
        Assert.Equal([(132.0, 42.0)], Positions(bottomRight));
        Assert.Equal(new SceneBounds(132, 42, 182, 82), bottomRight.Bounds);
    }

    /// <summary>
    ///     Validates that a legend without an orient is placed at its own position and does not join a grid.
    /// </summary>
    [Fact]
    public void LegendLayout_Place_NoneUsesLegendXAndLegendY()
    {
        // Act
        var placement = Place(Config(), Box("none", 50, 40, x: 10, y: 20), Box("right", 50, 40));

        // Assert: the group of the free legend is at (10, 20) and the right legend is placed as if alone
        Assert.Equal([(10.0, 20.0), (218.0, 0.0)], Positions(placement));
        Assert.Equal(new SceneBounds(10, 0, 268, 60), placement.Bounds);
    }

    /// <summary>
    ///     Validates that the largest offset that a legend asks for is used for all legends of the orient.
    /// </summary>
    [Fact]
    public void LegendLayout_Place_LargestRequestedOffsetWins()
    {
        // Act
        var placement = Place(Config(), Box("right", 50, 40, offset: 5), Box("right", 50, 40, offset: 30));

        // Assert
        Assert.Equal([(230.0, 0.0), (230.0, 48.0)], Positions(placement));
    }

    /// <summary>
    ///     Validates that the legends of different orients are placed independently, in the order of their first appearance, and that the
    ///     groups keep the order of the given legends.
    /// </summary>
    [Fact]
    public void LegendLayout_Place_OrientsArePlacedIndependentlyAndKeepTheOrder()
    {
        // Act
        var placement = Place(Config(), Box("bottom", 40, 20), Box("right", 50, 40), Box("bottom", 30, 20));

        // Assert
        Assert.Equal([(0.0, 118.0), (218.0, 0.0), (48.0, 118.0)], Positions(placement));
    }

    /// <summary>
    ///     Validates that the legends are measured from the axes: a right axis that reaches 230.4 pushes a right legend to its rounded
    ///     edge plus the offset, and a bottom axis pushes a bottom legend down, while the other sides are unchanged.
    /// </summary>
    [Fact]
    public void LegendLayout_Place_LegendsStartAfterTheAxes()
    {
        // Arrange
        var right = new AxisResult(new SceneGroup("axis", 0, 0), null, new SceneBounds(200, -3, 230.4, 100), "right", [], 1, false);
        var bottom = new AxisResult(new SceneGroup("axis", 0, 0), null, new SceneBounds(-5, 100, 200, 124.2), "bottom", [], 1, false);
        var (horizontal, vertical) = LegendLayout.AxisBounds([right, bottom], DataWidth, DataHeight);

        // Act
        var placement = LegendLayout.Place([Box("right", 50, 40), Box("bottom", 50, 40)], Config(), horizontal, vertical, DataWidth, DataHeight);

        // Assert: the left and right axes widen the horizontal bounds (the right axis also reaches 3 above) and the top and bottom axes
        // the vertical ones; ceil(230.4) + 18 = 249 and ceil(124.2) + 18 = 143
        Assert.Equal(new SceneBounds(0, -3, 230.4, 100), horizontal);
        Assert.Equal(new SceneBounds(-5, 0, 200, 124.2), vertical);
        Assert.Equal([(249.0, 0.0), (0.0, 143.0)], Positions(placement));
    }

    /// <summary>
    ///     Validates that the full bounds of a legend (which include its border) are used for the box when the configuration asks for them,
    ///     and the box otherwise.
    /// </summary>
    [Fact]
    public void LegendLayout_Place_FullBoundsPlaceTheBorderInsideTheGrid()
    {
        // Arrange: a legend whose bounds are one pixel larger on every side than its box
        var grown = new SceneBounds(-1, -1, 51, 41);
        var full = Config("{ \"legend\": { \"layout\": { \"bounds\": \"full\" } } }");

        // Act
        var flush = Place(Config(), Box("right", 50, 40, bounds: grown), Box("right", 50, 40, bounds: grown));
        var outer = Place(full, Box("right", 50, 40, bounds: grown), Box("right", 50, 40, bounds: grown));

        // Assert: flush boxes are 40 + 8 apart; with full bounds (as in Vega's grid layout) the second box is placed after the extent of the
        // first (ceil 41), the margin (8) and the room the second needs before its origin (1): 50 apart; the origin of the first stays at 218
        Assert.Equal([(218.0, 0.0), (218.0, 48.0)], Positions(flush));
        Assert.Equal([(218.0, 0.0), (218.0, 50.0)], Positions(outer));
    }

    /// <summary>
    ///     Validates that the reach of the legends is joined with that of the axes by the larger value on each side, and that no legend
    ///     leaves the axes unchanged.
    /// </summary>
    [Fact]
    public void LegendLayout_Combine_TakesTheLargerReachOfEachSide()
    {
        // Arrange
        var axes = new ViewExtents(10, 5, 3, 20);

        // Act
        var joined = LegendLayout.Combine(axes, new SceneBounds(218, -30, 268, 40), DataWidth, DataHeight);
        var none = LegendLayout.Combine(axes, SceneBounds.Empty, DataWidth, DataHeight);

        // Assert: right 268 - 200, top 30, left and bottom keep the axes
        Assert.Equal(new ViewExtents(10, 30, 68, 20), joined);
        Assert.Equal(axes, none);
    }

    /// <summary>
    ///     Validates that the placement of no legend is empty.
    /// </summary>
    [Fact]
    public void LegendLayout_Place_NoLegendsGivesAnEmptyPlacement()
    {
        // Act
        var placement = Place(Config());

        // Assert
        Assert.Empty(placement.Groups);
        Assert.True(placement.Bounds.IsEmpty);
    }
}
