using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Layout;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Layout;

/// <summary>
///     Tests of the helpers of <see cref="ViewLayout"/> for compositions: the size of a chart that sizes itself, the bounds of a cell, and the
///     request of the root of a concatenation or a facet. Numbers are derived by hand from the Vega-Lite 6 defaults (continuous size 300, padding 5).
/// </summary>
public class ViewLayoutCompositionTests
{
    /// <summary>
    ///     Parses a specification and resolves its configuration.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The root of the specification and the configuration.</returns>
    private static (SpecNode Root, ResolvedConfig Config) Parse(string json)
    {
        var tree = SpecParser.Parse(json);
        return (tree.Root, ConfigResolver.Resolve(tree, null));
    }

    /// <summary>
    ///     A number is the size, a step size or no size takes the discrete size of the scale stage, and the continuous size is the last resort.
    /// </summary>
    [Fact]
    public void ViewLayout_ResolveSize_NumberStepDefaultAndContinuousFallback()
    {
        // Arrange: a size given, a step size, no size, and the discrete hints of the scale stage
        var hints = new ViewSizeHints(DiscreteWidth: 80, DiscreteHeight: 60);
        var (number, config) = Parse("{ \"width\": 120, \"height\": 90 }");
        var (step, _) = Parse("{ \"width\": { \"step\": 10 }, \"height\": { \"step\": 20 } }");
        var (none, _) = Parse("{}");

        // Act and assert: the number wins over the hints
        Assert.Equal(new ViewSize(120, 90), ViewLayout.ResolveSize(number, config, hints));

        // Assert: a step size and no size use the discrete sizes; without hints the continuous size of 300 by 300
        Assert.Equal(new ViewSize(80, 60), ViewLayout.ResolveSize(step, config, hints));
        Assert.Equal(new ViewSize(80, 60), ViewLayout.ResolveSize(none, config, hints));
        Assert.Equal(new ViewSize(300, 300), ViewLayout.ResolveSize(none, config));
        Assert.Equal(new ViewSize(300, 300), ViewLayout.ResolveSize(null, config));
    }

    /// <summary>
    ///     A <c>"container"</c> size has no meaning in a composition and is read as if it were missing, even when the host gives a container size.
    /// </summary>
    [Fact]
    public void ViewLayout_ResolveSize_ContainerIsIgnoredInAComposition()
    {
        // Arrange: container sizes, a container hint, and a discrete hint for the height only
        var (root, config) = Parse("{ \"width\": \"container\", \"height\": \"container\" }");
        var hints = new ViewSizeHints(DiscreteHeight: 40, ContainerWidth: 900, ContainerHeight: 700);

        // Act
        var size = ViewLayout.ResolveSize(root, config, hints);

        // Assert: the width is the continuous 300, the height the discrete 40; the container is not used
        Assert.Equal(new ViewSize(300, 40), size);
    }

    /// <summary>
    ///     The chart's own <c>view</c> sets the continuous size, over the configuration.
    /// </summary>
    [Fact]
    public void ViewLayout_ResolveSize_ChartViewAndConfigSetTheContinuousSize()
    {
        // Arrange: a configured size, and a chart whose view overrides the height
        var (root, config) = Parse("{ \"config\": { \"view\": { \"continuousWidth\": 200, \"continuousHeight\": 150 } }, \"view\": { \"continuousHeight\": 90 } }");

        // Act and assert
        Assert.Equal(new ViewSize(200, 90), ViewLayout.ResolveSize(root, config));
    }

    /// <summary>
    ///     The bounds of a cell are the data rectangle united with the guides and the placed title.
    /// </summary>
    [Fact]
    public void ViewLayout_CellBounds_UnionsDataRectGuidesAndTitle()
    {
        // Arrange: a 100 by 80 data rectangle, an axis 30 wide on the left and one 20 high below, and a title 50 by 15 with the bounds frame, 4 above
        var title = new TitleBlock("top", "start", "bounds", 4, [], new SceneBounds(0, 0, 50, 15));

        // Act
        var (bounds, placed) = ViewLayout.CellBounds(100, 80, new ViewExtents(30, 0, 0, 20), title);

        // Assert: the bounds of the data rectangle and guides are (-30, 0, 100, 100); the title starts at their left edge -30, 15 + 4 above the top: (-30, -19, 20, -4)
        Assert.NotNull(placed);
        Assert.Equal((-30.0, -19.0), (placed.X, placed.Y));
        Assert.Equal(new SceneBounds(-30, -19, 100, 100), bounds);
    }

    /// <summary>
    ///     The stroke of the border of a cell reaches half its width outside everything in the cell (Vega expands the bounds of a stroked group), which is how a cell is spaced from its neighbors.
    /// </summary>
    [Fact]
    public void ViewLayout_CellBounds_StrokeReachesHalfItsWidthOutside()
    {
        // Act: a bare cell with a one pixel border, and one with a four pixel border, an axis and a title that reach further than the stroke
        var (thin, _) = ViewLayout.CellBounds(100, 80, default, null, 1);
        var (axis, _) = ViewLayout.CellBounds(100, 80, new ViewExtents(30, 0, 0, 0), null, 4);
        var (titled, _) = ViewLayout.CellBounds(100, 80, default, new TitleBlock("top", "start", "bounds", 10, [], new SceneBounds(0, 0, 20, 10)), 1);
        var (none, noTitle) = ViewLayout.CellBounds(100, 80, default);

        // Assert: the half stroke (2) is added to the axis (30) and (0.5) to the title, which lies 10 + 10 above the data rectangle
        Assert.Equal(new SceneBounds(-0.5, -0.5, 100.5, 80.5), thin);
        Assert.Equal(new SceneBounds(-32, -2, 102, 82), axis);
        Assert.Equal(new SceneBounds(-0.5, -20.5, 100.5, 80.5), titled);
        Assert.Equal(new SceneBounds(0, 0, 100, 80), none);
        Assert.Null(noTitle);
    }

    /// <summary>
    ///     A negative size and negative reaches count as zero.
    /// </summary>
    [Fact]
    public void ViewLayout_CellBounds_NegativeSizesAndReachesCountAsZero()
    {
        // Act
        var (bounds, _) = ViewLayout.CellBounds(-5, -6, new ViewExtents(-1, -2, -3, -4));

        // Assert: a point at the origin
        Assert.Equal(new SceneBounds(0, 0, 0, 0), bounds);
    }

    /// <summary>
    ///     The root of a composition has a data rectangle of size zero, no plot style and no container, and is padded: the canvas is the content plus the padding.
    /// </summary>
    [Fact]
    public void ViewLayout_Resolve_CompositionRootHasZeroSizeAndNoViewStyle()
    {
        // Arrange: a root that would otherwise be sized 400 by 300 with a container width, a plot stroke and autosize none; its padding and background are read
        var (root, config) = Parse(
            "{ \"width\": \"container\", \"height\": 300, \"padding\": 12, \"background\": \"#eee\", \"autosize\": { \"type\": \"none\", \"contains\": \"padding\" }, \"view\": { \"stroke\": \"red\", \"fill\": \"blue\" } }");

        // Act
        var request = ViewLayout.Resolve(root, config, new ViewSizeHints(ContainerWidth: 500), null, composition: true);

        // Assert: no data rectangle, no container, always padded
        Assert.Equal((0.0, 0.0), (request.DataWidth, request.DataHeight));
        Assert.Null(request.ContainerWidth);
        Assert.Null(request.ContainerHeight);
        Assert.Equal((AutosizeType.Pad, false), (request.Autosize, request.ContainsPadding));

        // Assert: no plot style; the padding and the background are those of the root
        Assert.Equal(new ViewStyle(null, null, null, null, null, null, null, null), request.View);
        Assert.Equal(new ViewPadding(12, 12, 12, 12), request.Padding);
        Assert.Equal("#eee", request.Background);
    }

    /// <summary>
    ///     A measured composition root is padded around the bounds of its content, which the guide extents of a root of size zero carry.
    /// </summary>
    [Fact]
    public void ViewLayout_Resolve_CompositionRootCanvasIsContentPlusPadding()
    {
        // Arrange: the content of a composition spans (-30.5, -0.5) to (252.5, 110.5) and the padding is the default 5
        var (root, config) = Parse("{}");
        var request = ViewLayout.Resolve(root, config, default, null, composition: true);
        var content = new SceneBounds(-30.5, -0.5, 252.5, 110.5);
        var guides = LegendLayout.Combine(default, content, 0, 0);

        // Act
        var measure = ViewLayout.Measure(request, guides);

        // Assert: the canvas holds ceil(30.5) = 31 on the left, ceil(252.5) = 253 on the right, ceil(0.5) = 1 above and 111 below, plus 5 around
        Assert.Equal((36.0, 6.0), (measure.OriginX, measure.OriginY));
        Assert.Equal(31.0 + 253.0 + 10.0, measure.Width);
        Assert.Equal(1.0 + 111.0 + 10.0, measure.Height);
    }

    /// <summary>
    ///     The arguments are validated.
    /// </summary>
    [Fact]
    public void ViewLayout_ResolveSize_NullConfigThrows()
    {
        // Act and assert
        Assert.Throws<ArgumentNullException>(() => ViewLayout.ResolveSize(null, null!));
    }
}
