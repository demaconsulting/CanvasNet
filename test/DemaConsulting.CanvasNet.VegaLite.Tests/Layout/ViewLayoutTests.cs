using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Rendering;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Layout;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Layout;

/// <summary>
///     Tests of the single-view layout: the data rectangle, padding, autosize, title bands and guide extents, with
///     numbers derived by hand from the Vega-Lite 6 defaults.
/// </summary>
public class ViewLayoutTests
{
    /// <summary>The measurer shared by the tests (bundled fonts).</summary>
    private static readonly TextMeasurer Measurer = new();

    /// <summary>
    ///     Resolves the layout request of a specification with the default configuration.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="hints">Sizes from the scale stage and the host.</param>
    /// <param name="background">A background override.</param>
    /// <returns>The request.</returns>
    private static ViewLayoutRequest Request(string json, ViewSizeHints hints = default, string? background = null)
    {
        var tree = SpecParser.Parse(json);
        return ViewLayout.Resolve(tree.Root, ConfigResolver.Resolve(tree, null), hints, background);
    }

    /// <summary>
    ///     Lays out a specification: its request, its title, and the given guide extents.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="guides">The guide extents.</param>
    /// <returns>The layout result.</returns>
    private static ViewLayoutResult Layout(string json, ViewExtents guides = default)
    {
        var tree = SpecParser.Parse(json);
        var config = ConfigResolver.Resolve(tree, null);
        var request = ViewLayout.Resolve(tree.Root, config);
        var title = new TitleBuilder(Measurer).Build(tree.Root["title"], config);
        return ViewLayout.Compute(request, guides, title);
    }

    /// <summary>
    ///     Measures a bold 13 pixel title the way the layout does, independently of the unit under test.
    /// </summary>
    /// <param name="text">The title.</param>
    /// <returns>The width.</returns>
    private static double TitleWidth(string text) =>
        TextRenderer.MeasureText(text, SystemFontCatalog.LoadBundledFallback(false, false, true, false), 13).Width;

    /// <summary>
    ///     Validates the defaults of an empty view, then padding, a title band and autosize against hand-derived numbers.
    /// </summary>
    [Fact]
    public void ViewLayout_Compute_PaddingTitleAndAutosizeProduceExpectedDataRectangle()
    {
        // Act: an empty view: Vega-Lite 6 default 300 by 300 data rectangle and 5 pixels of padding
        var empty = Layout("{}");

        // Assert: the canvas is 310 by 310 and the data rectangle starts at (5, 5)
        Assert.Equal((300.0, 300.0), (empty.Measure.DataWidth, empty.Measure.DataHeight));
        Assert.Equal((310.0, 310.0), (empty.Measure.Width, empty.Measure.Height));
        Assert.Equal(new VegaLiteSize(310, 310), empty.Measure.NaturalSize);
        Assert.Equal((5.0, 5.0), (empty.Measure.OriginX, empty.Measure.OriginY));
        Assert.Equal(new SceneBounds(0, 0, 300, 300), empty.Measure.ViewBounds);
        Assert.Null(empty.Measure.Title);
        Assert.True(empty.Measure.TitleBounds.IsEmpty);
        Assert.Equal((310.0, 310.0, "white"), (empty.Scene.Width, empty.Scene.Height, empty.Scene.Background));

        // Assert: the scene is root > view; the view group has the data rectangle's size at its origin, and the one pixel #ddd border
        var view = empty.Scene.Find("view");
        Assert.NotNull(view);
        var group = Assert.IsType<SceneGroup>(view.Value.Item);
        Assert.Equal((5.0, 5.0, 300.0, 300.0), (group.X, group.Y, group.Width, group.Height));
        Assert.Equal(("transparent", "#ddd"), (group.Fill, group.Stroke));
        Assert.Null(group.StrokeWidth);
        Assert.Null(group.Clip);
        Assert.Empty(group.Children);
        Assert.Equal(2, empty.Scene.CountItems());

        // Act: explicit sizes and a padding of 20
        var sized = Layout("{\"width\":200,\"height\":100,\"padding\":20}");

        // Assert: 200 + 2 * 20 by 100 + 2 * 20
        Assert.Equal((240.0, 140.0), (sized.Measure.Width, sized.Measure.Height));
        Assert.Equal((20.0, 20.0), (sized.Measure.OriginX, sized.Measure.OriginY));

        // Act: a padding object leaves the sides it does not name at 0
        var sides = Layout("{\"padding\":{\"left\":1,\"top\":2}}");

        // Assert
        Assert.Equal((301.0, 302.0), (sides.Measure.Width, sides.Measure.Height));
        Assert.Equal((1.0, 2.0), (sides.Measure.OriginX, sides.Measure.OriginY));

        // Act: a title above the plot: 13 pixels of text and 4 pixels of offset reach 17 pixels above the data rectangle
        var titled = Layout("{\"title\":\"Title\"}");
        var title = titled.Scene.Find("title");

        // Assert: the canvas grows by 17 at the top; the title is a child of the view, centered above the plot
        Assert.Equal((310.0, 327.0), (titled.Measure.Width, titled.Measure.Height));
        Assert.Equal((5.0, 22.0), (titled.Measure.OriginX, titled.Measure.OriginY));
        Assert.NotNull(title);
        var titleGroup = Assert.IsType<SceneGroup>(title.Value.Item);
        Assert.Equal((150.0, -17.0), (titleGroup.X, titleGroup.Y));
        Assert.Equal((5.0, 22.0), (title.Value.OffsetX, title.Value.OffsetY));
        Assert.Equal(new SceneBounds(0, -17, 300, 300).Y1, titled.Measure.ViewBounds.Y1);
        Assert.Equal(-4, titled.Measure.TitleBounds.Y2, 6);
        Assert.Equal(["title-text"], titleGroup.Children.Select(c => c.Role));

        // Act: the other three sides
        var bottom = Layout("{\"title\":{\"text\":\"Title\",\"orient\":\"bottom\"}}");
        var left = Layout("{\"title\":{\"text\":\"Title\",\"orient\":\"left\"}}");
        var right = Layout("{\"title\":{\"text\":\"Title\",\"orient\":\"right\"}}");

        // Assert: bottom grows the canvas below (4 + 13), left and right beside (13 + 4)
        Assert.Equal((310.0, 327.0, 5.0, 5.0), (bottom.Measure.Width, bottom.Measure.Height, bottom.Measure.OriginX, bottom.Measure.OriginY));
        Assert.Equal((327.0, 310.0, 22.0, 5.0), (left.Measure.Width, left.Measure.Height, left.Measure.OriginX, left.Measure.OriginY));
        Assert.Equal((327.0, 310.0, 5.0, 5.0), (right.Measure.Width, right.Measure.Height, right.Measure.OriginX, right.Measure.OriginY));

        // Act: autosize none keeps the given size plus padding whatever sticks out
        var none = Layout("{\"autosize\":\"none\",\"width\":100,\"height\":100,\"title\":\"X\"}");
        var noneObject = Layout("{\"autosize\":{\"type\":\"none\"},\"width\":100,\"height\":100,\"title\":\"X\"}");
        var contained = Layout("{\"autosize\":{\"type\":\"none\",\"contains\":\"padding\"},\"width\":100,\"height\":80}");
        var configured = Layout("{\"config\":{\"autosize\":\"none\"},\"width\":50,\"height\":50,\"title\":\"X\"}");

        // Assert: no growth for the title; with contains padding the width and height are the whole canvas
        Assert.Equal((110.0, 110.0, 5.0, 5.0), (none.Measure.Width, none.Measure.Height, none.Measure.OriginX, none.Measure.OriginY));
        Assert.Equal((110.0, 110.0), (noneObject.Measure.Width, noneObject.Measure.Height));
        Assert.Equal((100.0, 80.0, 90.0, 70.0), (contained.Measure.Width, contained.Measure.Height, contained.Measure.DataWidth, contained.Measure.DataHeight));
        Assert.Equal((60.0, 60.0), (configured.Measure.Width, configured.Measure.Height));

        // Assert: the types the layout does not support are laid out as pad (the feature catalog reports them)
        var fit = Layout("{\"autosize\":\"fit\",\"width\":100,\"height\":100,\"title\":\"X\"}");
        Assert.Equal((110.0, 127.0), (fit.Measure.Width, fit.Measure.Height));
    }

    /// <summary>
    ///     Validates that a title wider than the plot grows the canvas on both sides by whole pixels.
    /// </summary>
    [Fact]
    public void ViewLayout_Compute_WideTitleGrowsCanvasByWholePixelsOnBothSides()
    {
        // Arrange: a 20 pixel plot under a long title, centered on the plot
        const string text = "A very wide title that overflows";
        var expectedGrowth = Math.Ceiling((TitleWidth(text) / 2) - 10);

        // Act
        var result = Layout("{\"width\":20,\"height\":20,\"title\":\"" + text + "\"}");

        // Assert: the title overhangs the plot by (w - 20) / 2 on each side, which rounds up to whole pixels
        Assert.True(expectedGrowth > 20);
        Assert.Equal(20 + (2 * expectedGrowth) + 10, result.Measure.Width, 6);
        Assert.Equal(5 + expectedGrowth, result.Measure.OriginX, 6);
        Assert.Equal(20 + 17 + 10, result.Measure.Height, 6);
        Assert.Equal(-(TitleWidth(text) / 2) + 10, result.Measure.ViewBounds.X1, 4);
    }

    /// <summary>
    ///     Validates how the guide extents grow the canvas and how the title relates to them.
    /// </summary>
    [Fact]
    public void ViewLayout_Compute_GuideExtentsGrowTheCanvas()
    {
        // Act: a left axis 40 wide and a bottom axis 25 high
        var axes = Layout("{}", new ViewExtents(40, 0, 0, 25));

        // Assert: 300 + 40 + 10 by 300 + 25 + 10, the data rectangle starts at (45, 5)
        Assert.Equal((350.0, 335.0), (axes.Measure.Width, axes.Measure.Height));
        Assert.Equal((45.0, 5.0), (axes.Measure.OriginX, axes.Measure.OriginY));
        Assert.Equal(new SceneBounds(-40, 0, 300, 325), axes.Measure.ViewBounds);

        // Act: guides on all sides plus a title that is centered over the plot (frame group), above the top guide
        var all = Layout("{\"title\":\"T\"}", new ViewExtents(40, 20, 10, 30));

        // Assert: the title clears the top guide: y = -20 - 13 - 4 = -37; the canvas grows 37 + 30 at the top and bottom
        var title = Assert.IsType<SceneGroup>(all.Scene.Find("title")!.Value.Item);
        Assert.Equal((150.0, -37.0), (title.X, title.Y));
        Assert.Equal((300.0 + 40 + 10 + 10, 300.0 + 37 + 30 + 10), (all.Measure.Width, all.Measure.Height));
        Assert.Equal((45.0, 42.0), (all.Measure.OriginX, all.Measure.OriginY));

        // Act: the bounds frame centers the title over the guides, not the plot: (-40 + 310) / 2 = 135
        var framed = Layout("{\"title\":{\"text\":\"T\",\"frame\":\"bounds\"}}", new ViewExtents(40, 20, 10, 30));

        // Assert
        Assert.Equal(135, Assert.IsType<SceneGroup>(framed.Scene.Find("title")!.Value.Item).X);

        // Assert: negative extents count as 0
        var negative = Layout("{}", new ViewExtents(-5, -5, -5, -5));
        Assert.Equal((310.0, 310.0), (negative.Measure.Width, negative.Measure.Height));
    }

    /// <summary>
    ///     Validates the sources of the data rectangle: numbers, the configuration, steps and the container.
    /// </summary>
    [Fact]
    public void ViewLayout_Resolve_SizesComeFromSpecificationConfigurationAndHints()
    {
        // Act / Assert: the Vega-Lite 6 default is 300; configuration changes it; the view of the specification wins over it
        Assert.Equal((300.0, 300.0), (Request("{}").DataWidth, Request("{}").DataHeight));
        var configured = Request("{\"config\":{\"view\":{\"continuousWidth\":120,\"continuousHeight\":80}}}");
        Assert.Equal((120.0, 80.0), (configured.DataWidth, configured.DataHeight));
        var overridden = Request("{\"view\":{\"continuousWidth\":50},\"config\":{\"view\":{\"continuousWidth\":120,\"continuousHeight\":80}}}");
        Assert.Equal((50.0, 80.0), (overridden.DataWidth, overridden.DataHeight));

        // Assert: a number wins over everything, negative sizes count as 0
        var hints = new ViewSizeHints(DiscreteWidth: 140, DiscreteHeight: 60);
        var explicitSize = Request("{\"width\":50,\"height\":-4}", hints);
        Assert.Equal((50.0, 0.0), (explicitSize.DataWidth, explicitSize.DataHeight));

        // Assert: a step size (and a missing size on a discrete scale) takes the size that the scale stage computed from the step
        var step = Request("{\"width\":{\"step\":20},\"height\":{\"step\":20}}", hints);
        Assert.Equal((140.0, 60.0), (step.DataWidth, step.DataHeight));
        var discrete = Request("{}", hints);
        Assert.Equal((140.0, 60.0), (discrete.DataWidth, discrete.DataHeight));

        // Assert: without the scale stage's answer a step size falls back to the continuous size
        var unknownStep = Request("{\"width\":{\"step\":20}}");
        Assert.Equal(300.0, unknownStep.DataWidth);

        // Act: a container size fills the container less padding and the room of the guides
        var container = Request("{\"width\":\"container\",\"height\":\"container\"}", new ViewSizeHints(ContainerWidth: 400, ContainerHeight: 250));
        var measure = ViewLayout.Measure(container, new ViewExtents(30, 0, 0, 0));

        // Assert: 400 - 10 - 30 = 360 wide in a canvas of exactly 400, 250 - 10 = 240 high in a canvas of 250
        Assert.Equal((400.0, 250.0), (container.ContainerWidth, container.ContainerHeight));
        Assert.Equal((360.0, 240.0), (measure.DataWidth, measure.DataHeight));
        Assert.Equal((400.0, 250.0), (measure.Width, measure.Height));
        Assert.Equal(new VegaLiteSize(400, 250), measure.NaturalSize);
    }

    /// <summary>
    ///     Validates the appearance of the plot area and the background.
    /// </summary>
    [Fact]
    public void ViewLayout_Resolve_ViewStyleAndBackgroundFollowPrecedence()
    {
        // Act
        var defaults = Request("{}");
        var spec = Request(
            """
            {"background": "#fafafa", "view": {"stroke": null, "fill": "#eee", "strokeWidth": 2, "cornerRadius": 4,
             "strokeDash": [4, 2], "opacity": 0.5, "fillOpacity": 0.25, "strokeOpacity": 0.75}}
            """);
        var config = Request("{\"config\":{\"background\":\"black\",\"view\":{\"stroke\":\"red\",\"fill\":\"#123\"}}}");
        var noStroke = Request("{\"config\":{\"view\":{\"stroke\":null}}}");
        var overridden = Request("{\"background\":\"#fafafa\",\"config\":{\"background\":\"black\"}}", default, "#336699");

        // Assert: Vega's cell style gives a transparent fill and a #ddd stroke, the canvas is white
        Assert.Equal(("transparent", "#ddd", "white"), (defaults.View.Fill, defaults.View.Stroke, defaults.Background));
        Assert.Null(defaults.View.StrokeWidth);
        Assert.Null(defaults.View.StrokeDash);
        Assert.Null(defaults.View.CornerRadius);

        // Assert: the view of the specification, including an explicit null for no stroke, and its background
        Assert.Equal("#eee", spec.View.Fill);
        Assert.Null(spec.View.Stroke);
        Assert.Equal(2.0, spec.View.StrokeWidth);
        Assert.Equal(4.0, spec.View.CornerRadius);
        Assert.Equal([4.0, 2.0], spec.View.StrokeDash);
        Assert.Equal((0.5, 0.25, 0.75), (spec.View.Opacity, spec.View.FillOpacity, spec.View.StrokeOpacity));
        Assert.Equal("#fafafa", spec.Background);

        // Assert: config.view and config.background apply when the specification is silent; null in the configuration removes the stroke
        Assert.Equal(("#123", "red", "black"), (config.View.Fill, config.View.Stroke, config.Background));
        Assert.Null(noStroke.View.Stroke);

        // Assert: the override of the caller wins over the specification and the configuration
        Assert.Equal("#336699", overridden.Background);

        // Assert: the scene carries them: the view group paints the plot area, the scene the canvas
        var tree = SpecParser.Parse("{\"view\":{\"fill\":\"#eee\",\"strokeWidth\":2,\"cornerRadius\":4,\"strokeDash\":[4,2],\"opacity\":0.5}}");
        var request = ViewLayout.Resolve(tree.Root, ConfigResolver.Resolve(tree, null));
        var scene = ViewLayout.Compute(request, default).Scene;
        var group = Assert.IsType<SceneGroup>(scene.Find("view")!.Value.Item);
        Assert.Equal(("#eee", "#ddd"), (group.Fill, group.Stroke));
        Assert.Equal(2.0, group.StrokeWidth);
        Assert.Equal((4.0, 0.5), (group.CornerRadius, group.Opacity));
        Assert.Equal([4.0, 2.0], group.StrokeDash);
    }

    /// <summary>
    ///     Validates content placement, the clip option, the two-phase API and rounding of the natural size.
    /// </summary>
    [Fact]
    public void ViewLayout_Build_ContentTitleClipAndRoundingFollowTheMeasure()
    {
        // Arrange
        var tree = SpecParser.Parse("{\"title\":\"T\",\"width\":100.4,\"height\":50.5}");
        var config = ConfigResolver.Resolve(tree, null);
        var request = ViewLayout.Resolve(tree.Root, config);
        var title = new TitleBuilder(Measurer).Build(tree.Root["title"], config);
        SceneItem[] content = [new SceneRect("mark-bar", 1, 2, 3, 4), new SceneRule("axis-domain", 0, 100, 100, 100)];

        // Act: measure first (guides are measured by their builders in between), then build around the content
        var measure = ViewLayout.Measure(request, new ViewExtents(0, 0, 0, 0), title);
        var scene = ViewLayout.Build(request, measure, content);
        var all = ViewLayout.Compute(request, default, title, content);

        // Assert: the content comes first in the view group, the title last
        var view = Assert.IsType<SceneGroup>(scene.Find("view")!.Value.Item);
        Assert.Equal(["mark-bar", "axis-domain", "title"], view.Children.Select(c => c.Role));
        Assert.Equal(scene.Enumerate().Select(e => e.Item.Role), all.Scene.Enumerate().Select(e => e.Item.Role));
        Assert.Equal((measure.Width, measure.Height, measure.OriginX, measure.OriginY), (all.Measure.Width, all.Measure.Height, all.Measure.OriginX, all.Measure.OriginY));

        // Assert: the canvas is 100.4 + 10 by 50.5 + 17 + 10, rounded half away from zero to whole pixels for the surface
        Assert.Equal(110.4, measure.Width, 6);
        Assert.Equal(77.5, measure.Height, 6);
        Assert.Equal(new VegaLiteSize(110, 78), measure.NaturalSize);
        Assert.Equal((110.4, 77.5), (scene.Width, scene.Height));

        // Act: a clipped view does not grow for its title, and clips its content to the data rectangle
        var clipped = ViewLayout.Compute(request with { Clip = true }, default, title);

        // Assert
        Assert.Equal(new SceneBounds(0, 0, 100.4, 50.5), clipped.Measure.ViewBounds);
        Assert.Equal((110.4, 60.5), (clipped.Measure.Width, clipped.Measure.Height));
        Assert.Equal(new SceneClip(0, 0, 100.4, 50.5), Assert.IsType<SceneGroup>(clipped.Scene.Find("view")!.Value.Item).Clip);

        // Assert: invalid arguments are rejected
        Assert.Throws<ArgumentNullException>(() => ViewLayout.Resolve(tree.Root, null!));
        Assert.Throws<ArgumentNullException>(() => ViewLayout.Measure(null!, default));
        Assert.Throws<ArgumentNullException>(() => ViewLayout.Build(request, null!, content));
        Assert.Throws<ArgumentNullException>(() => ViewLayout.Build(request, measure, null!));

        // Assert: a huge size cannot overflow the pixel size
        var huge = ViewLayout.Measure(request with { DataWidth = 1e12 }, default);
        Assert.Equal(int.MaxValue, huge.NaturalSize.Width);
    }
}
