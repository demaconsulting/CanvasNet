using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Layout;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Layout;

/// <summary>
///     Tests of <c>autosize</c> <c>fit</c>, <c>fit-x</c> and <c>fit-y</c> and of the <c>"container"</c> sizes in the single-view layout (Phase 11): the width and height are the size of the
///     whole view, so the data rectangle is what is left after the guides (each reach rounded up to whole pixels, as Vega's <c>viewSizeLayout</c> does), the title and the padding that
///     <c>contains</c> asks for. Every number is derived by hand from those rules with the Vega-Lite 6 defaults (a padding of 5 pixels, a 13 pixel title and 4 pixels of offset: 17 pixels above the data rectangle).
/// </summary>
public class ViewLayoutFitTests
{
    /// <summary>The measurer shared by the tests (bundled fonts).</summary>
    private static readonly TextMeasurer Measurer = new();

    /// <summary>
    ///     Resolves the layout request of a specification with the default configuration.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="hints">Sizes from the scale stage and the host.</param>
    /// <returns>The request.</returns>
    private static ViewLayoutRequest Request(string json, ViewSizeHints hints = default)
    {
        var tree = SpecParser.Parse(json);
        return ViewLayout.Resolve(tree.Root, ConfigResolver.Resolve(tree, null), hints);
    }

    /// <summary>
    ///     Measures a specification: its request, its title and the given guide extents.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="guides">The guide extents.</param>
    /// <param name="hints">Sizes from the scale stage and the host.</param>
    /// <returns>The measurements.</returns>
    private static ViewMeasure Measure(string json, ViewExtents guides = default, ViewSizeHints hints = default)
    {
        var tree = SpecParser.Parse(json);
        var config = ConfigResolver.Resolve(tree, null);
        var request = ViewLayout.Resolve(tree.Root, config, hints);
        var title = new TitleBuilder(Measurer).Build(tree.Root["title"], config);
        return ViewLayout.Measure(request, guides, title);
    }

    /// <summary>
    ///     Validates that <c>fit</c> makes the width and height the size of the whole view: with <c>contains: padding</c> the canvas is exactly that size.
    /// </summary>
    [Fact]
    public void ViewLayout_Measure_FitFillsWidthAndHeightIncludingGuides()
    {
        // Act: 400 by 300 including the padding of 5; a left axis of 30, a top guide of 4, a right legend of 40 and a bottom axis of 25
        var measure = Measure("{\"autosize\":{\"type\":\"fit\",\"contains\":\"padding\"},\"width\":400,\"height\":300}", new ViewExtents(30, 4, 40, 25));

        // Assert: 400 - 10 - 30 - 40 = 320 wide and 300 - 10 - 4 - 25 = 261 high; the canvas is exactly the given size and the data rectangle starts after the padding and the guides
        Assert.Equal((320.0, 261.0), (measure.DataWidth, measure.DataHeight));
        Assert.Equal((400.0, 300.0), (measure.Width, measure.Height));
        Assert.Equal(new VegaLiteSize(400, 300), measure.NaturalSize);
        Assert.Equal((35.0, 9.0), (measure.OriginX, measure.OriginY));
        Assert.Equal(new SceneBounds(-30, -4, 360, 286), measure.ViewBounds);
    }

    /// <summary>
    ///     Validates that <c>fit-x</c> fits the width only: the height pads around the data rectangle as <c>autosize: pad</c> does.
    /// </summary>
    [Fact]
    public void ViewLayout_Measure_FitXOnlyFitsWidth()
    {
        // Act: contains content: the 400 are the view without the padding; the guides are those of the fit test
        var measure = Measure("{\"autosize\":\"fit-x\",\"width\":400,\"height\":200}", new ViewExtents(30, 4, 40, 25));

        // Assert: 400 - 30 - 40 = 330 wide in a canvas of 400 + 10; the height stays 200 and the canvas grows by the guides: 200 + 4 + 25 + 10 = 239
        Assert.Equal((330.0, 200.0), (measure.DataWidth, measure.DataHeight));
        Assert.Equal((410.0, 239.0), (measure.Width, measure.Height));
        Assert.Equal((35.0, 9.0), (measure.OriginX, measure.OriginY));
    }

    /// <summary>
    ///     Validates that <c>fit-y</c> fits the height only: the width pads around the data rectangle.
    /// </summary>
    [Fact]
    public void ViewLayout_Measure_FitYOnlyFitsHeight()
    {
        // Act
        var measure = Measure("{\"autosize\":\"fit-y\",\"width\":200,\"height\":300}", new ViewExtents(30, 4, 40, 25));

        // Assert: 300 - 4 - 25 = 271 high in a canvas of 300 + 10; the width stays 200 and the canvas grows by the guides: 200 + 30 + 40 + 10 = 280
        Assert.Equal((200.0, 271.0), (measure.DataWidth, measure.DataHeight));
        Assert.Equal((280.0, 310.0), (measure.Width, measure.Height));
    }

    /// <summary>
    ///     Validates that the title is part of what the fit leaves room for, above the plot, and that it does not make the canvas larger than the size.
    /// </summary>
    [Fact]
    public void ViewLayout_Measure_FitLeavesRoomForTheTitle()
    {
        // Act: a title above the plot reaches 17 pixels (13 of text and 4 of offset)
        var measure = Measure("{\"autosize\":\"fit\",\"width\":100,\"height\":100,\"title\":\"X\"}");

        // Assert: 100 - 17 = 83 high, in a canvas of 100 + 10 by 100 + 10
        Assert.Equal((100.0, 83.0), (measure.DataWidth, measure.DataHeight));
        Assert.Equal((110.0, 110.0), (measure.Width, measure.Height));
        Assert.Equal((5.0, 22.0), (measure.OriginX, measure.OriginY));

        // Act: a title wider than the plot overhangs it on both sides and takes room from the width
        var wide = Measure("{\"autosize\":\"fit\",\"width\":100,\"height\":100,\"title\":\"A very wide title that overflows\"}");

        // Assert: the canvas is still the size and the plot is smaller than 100
        Assert.Equal((110.0, 110.0), (wide.Width, wide.Height));
        Assert.True(wide.DataWidth < 100);
        Assert.True(wide.OriginX > 5);
    }

    /// <summary>
    ///     Validates that <c>contains: padding</c> takes the padding out of the size and <c>content</c> adds it to the canvas.
    /// </summary>
    [Fact]
    public void ViewLayout_Measure_ContainsPaddingSubtractsPaddingFromTheFitSize()
    {
        // Act: a padding of 20
        var content = Measure("{\"autosize\":{\"type\":\"fit\"},\"width\":100,\"height\":100,\"padding\":20}");
        var padding = Measure("{\"autosize\":{\"type\":\"fit\",\"contains\":\"padding\"},\"width\":100,\"height\":100,\"padding\":20}");

        // Assert: content: the data rectangle is the 100 and the canvas 140; padding: the data rectangle is 100 - 40 = 60 and the canvas 100
        Assert.Equal((100.0, 100.0, 140.0, 140.0), (content.DataWidth, content.DataHeight, content.Width, content.Height));
        Assert.Equal((60.0, 60.0, 100.0, 100.0), (padding.DataWidth, padding.DataHeight, padding.Width, padding.Height));
    }

    /// <summary>
    ///     Validates that the reach of each guide is rounded up to whole pixels before it is taken from the size, and that a guide of whole pixels is not rounded up by an error of the subtraction.
    /// </summary>
    [Fact]
    public void ViewLayout_Measure_FitUsesCeiledGuideExtents()
    {
        // Act: no padding; reaches of 30.2 and 40.6 pixels
        var rounded = Measure("{\"autosize\":\"fit\",\"padding\":0,\"width\":400,\"height\":300}", new ViewExtents(30.2, 10.5, 40.6, 0.1));

        // Assert: 400 - 31 - 41 = 328 and 300 - 11 - 1 = 288; the data rectangle starts at the rounded reach
        Assert.Equal((328.0, 288.0), (rounded.DataWidth, rounded.DataHeight));
        Assert.Equal((400.0, 300.0), (rounded.Width, rounded.Height));
        Assert.Equal((31.0, 11.0), (rounded.OriginX, rounded.OriginY));

        // Act: reaches of whole pixels with a fractional size
        var whole = Measure("{\"autosize\":\"fit\",\"padding\":0,\"width\":400.3,\"height\":300.7}", new ViewExtents(0, 0, 12, 20));

        // Assert: nothing is rounded up
        Assert.Equal(388.3, whole.DataWidth, 9);
        Assert.Equal(280.7, whole.DataHeight, 9);
    }

    /// <summary>
    ///     Validates that a step size drops the fit for its dimension the way Vega-Lite does, and that the drop is recorded for the report.
    /// </summary>
    [Fact]
    public void ViewLayout_Resolve_StepDimensionDropsFit()
    {
        // Arrange: a discrete scale sized the width from its step (140) and the height from its step (60)
        var width = new ViewSizeHints(DiscreteWidth: 140);
        var both = new ViewSizeHints(DiscreteWidth: 140, DiscreteHeight: 60);

        // Act
        var fitWidthStep = Request("{\"autosize\":\"fit\",\"height\":100}", width);
        var fitXWidthStep = Request("{\"autosize\":\"fit-x\",\"height\":100}", width);
        var fitYWidthStep = Request("{\"autosize\":\"fit-y\",\"height\":100}", width);
        var fitBoth = Request("{\"autosize\":\"fit\"}", both);
        var fitHeightStep = Request("{\"autosize\":\"fit\",\"width\":100}", new ViewSizeHints(DiscreteHeight: 60));
        var explicitStep = Request("{\"autosize\":\"fit\",\"width\":{\"step\":20},\"height\":100}", width);
        var numeric = Request("{\"autosize\":\"fit\",\"width\":100,\"height\":100}", both);

        // Assert: fit with a step width keeps the fit of the height and reports it; the step gives the width
        Assert.Equal((AutosizeType.FitY, "x", 140.0), (fitWidthStep.Autosize, fitWidthStep.DroppedFit, fitWidthStep.DataWidth));

        // Assert: Vega-Lite keeps the fit of the other dimension also when the type was the fit of the step dimension (fit-x with a step width becomes fit-y)
        Assert.Equal((AutosizeType.FitY, "x"), (fitXWidthStep.Autosize, fitXWidthStep.DroppedFit));

        // Assert: fit-y with a step width has nothing to drop
        Assert.Equal((AutosizeType.FitY, null), (fitYWidthStep.Autosize, fitYWidthStep.DroppedFit));

        // Assert: two steps leave pad; a step height leaves fit-x
        Assert.Equal((AutosizeType.Pad, "both", 140.0, 60.0), (fitBoth.Autosize, fitBoth.DroppedFit, fitBoth.DataWidth, fitBoth.DataHeight));
        Assert.Equal((AutosizeType.FitX, "y"), (fitHeightStep.Autosize, fitHeightStep.DroppedFit));
        Assert.Equal((AutosizeType.FitY, "x"), (explicitStep.Autosize, explicitStep.DroppedFit));

        // Assert: a number is not a step, whatever the scale stage says
        Assert.Equal((AutosizeType.Fit, null, 100.0, 100.0), (numeric.Autosize, numeric.DroppedFit, numeric.DataWidth, numeric.DataHeight));

        // Act / Assert: the dropped dimension pads: the step width 140 and the fitted height 100 with a guide of 20 at the bottom
        var measure = ViewLayout.Measure(fitWidthStep, new ViewExtents(0, 0, 30, 20));
        Assert.Equal((140.0, 80.0), (measure.DataWidth, measure.DataHeight));
        Assert.Equal((180.0, 110.0), (measure.Width, measure.Height));
    }

    /// <summary>
    ///     Validates that a <c>"container"</c> width means <c>fit-x</c> with <c>contains: padding</c>, a container height <c>fit-y</c> and both <c>fit</c>, and that the host's size is what is fitted.
    /// </summary>
    [Fact]
    public void ViewLayout_Resolve_ContainerWidthImpliesFitXContainsPadding()
    {
        // Arrange
        var hints = new ViewSizeHints(ContainerWidth: 400, ContainerHeight: 250);

        // Act
        var width = Request("{\"width\":\"container\"}", hints);
        var height = Request("{\"height\":\"container\"}", hints);
        var both = Request("{\"width\":\"container\",\"height\":\"container\"}", hints);

        // Assert
        Assert.Equal((AutosizeType.FitX, true, 400.0, null), (width.Autosize, width.ContainsPadding, width.ContainerWidth, width.ContainerHeight));
        Assert.Equal((AutosizeType.FitY, true, null, 250.0), (height.Autosize, height.ContainsPadding, height.ContainerWidth, height.ContainerHeight));
        Assert.Equal((AutosizeType.Fit, true, 400.0, 250.0), (both.Autosize, both.ContainsPadding, both.ContainerWidth, both.ContainerHeight));

        // Act: a container width with a left axis of 30; the height is 300 by default and pads
        var measure = ViewLayout.Measure(width, new ViewExtents(30, 0, 0, 0));

        // Assert: 400 - 10 - 30 = 360 wide in a canvas of exactly 400; 300 + 10 high
        Assert.Equal((360.0, 300.0), (measure.DataWidth, measure.DataHeight));
        Assert.Equal((400.0, 310.0), (measure.Width, measure.Height));

        // Act: without a size from the host the container falls back to the continuous size
        var fallback = Request("{\"width\":\"container\"}");

        // Assert
        Assert.Equal(300.0, fallback.ContainerWidth);
    }

    /// <summary>
    ///     Validates the precedence of the autosize: the specification over the configuration over the default that a <c>"container"</c> size implies, property by property.
    /// </summary>
    [Fact]
    public void ViewLayout_Resolve_SpecAutosizeOverridesContainerDefault()
    {
        // Arrange
        var hints = new ViewSizeHints(ContainerWidth: 400);

        // Act
        var pad = Request("{\"width\":\"container\",\"autosize\":\"pad\"}", hints);
        var none = Request("{\"width\":\"container\",\"autosize\":{\"type\":\"none\"}}", hints);
        var content = Request("{\"width\":\"container\",\"autosize\":{\"contains\":\"content\"}}", hints);
        var configured = Request("{\"width\":\"container\",\"config\":{\"autosize\":\"pad\"}}", hints);
        var specOverConfig = Request("{\"width\":\"container\",\"autosize\":\"fit-x\",\"config\":{\"autosize\":\"none\"}}", hints);
        var configOnly = Request("{\"width\":100,\"config\":{\"autosize\":{\"type\":\"fit\",\"contains\":\"padding\"}}}");

        // Assert: pad (and none) give the container size to the data rectangle, which is not fitted; the contains of the default stays
        Assert.Equal((AutosizeType.Pad, true, 400.0, (double?)null), (pad.Autosize, pad.ContainsPadding, pad.DataWidth, pad.ContainerWidth));
        Assert.Equal((AutosizeType.None, true, 400.0), (none.Autosize, none.ContainsPadding, none.DataWidth));
        Assert.Equal(390.0, ViewLayout.Measure(none, default).DataWidth);
        Assert.Equal(400.0, ViewLayout.Measure(none, default).Width);

        // Assert: only the contains changes: still fit-x, the 400 are now the view without the padding
        Assert.Equal((AutosizeType.FitX, false, 400.0), (content.Autosize, content.ContainsPadding, content.ContainerWidth));
        Assert.Equal((400.0, 410.0), (ViewLayout.Measure(content, default).DataWidth, ViewLayout.Measure(content, default).Width));

        // Assert: a configuration that sets an autosize wins over the container default, even when it is pad; the specification wins over the configuration
        Assert.Equal((AutosizeType.Pad, true), (configured.Autosize, configured.ContainsPadding));
        Assert.Equal((AutosizeType.FitX, true), (specOverConfig.Autosize, specOverConfig.ContainsPadding));
        Assert.Equal((AutosizeType.Fit, true, 100.0), (configOnly.Autosize, configOnly.ContainsPadding, configOnly.DataWidth));

        // Assert: a composition has no autosize of its own
        var tree = SpecParser.Parse("{\"autosize\":\"fit\",\"width\":\"container\"}");
        var composition = ViewLayout.Resolve(tree.Root, ConfigResolver.Resolve(tree, null), hints, null, composition: true);
        Assert.Equal((AutosizeType.Pad, false, null), (composition.Autosize, composition.ContainsPadding, composition.DroppedFit));
    }

    /// <summary>
    ///     Validates that a clipped view has nothing reaching beyond its data rectangle: the fit leaves no room for guides.
    /// </summary>
    [Fact]
    public void ViewLayout_Measure_FitOfAClippedViewLeavesNoRoomForGuides()
    {
        // Arrange
        var tree = SpecParser.Parse("{\"autosize\":\"fit\",\"width\":200,\"height\":100}");
        var request = ViewLayout.Resolve(tree.Root, ConfigResolver.Resolve(tree, null)) with { Clip = true };

        // Act
        var measure = ViewLayout.Measure(request, new ViewExtents(30, 20, 40, 25));

        // Assert: the data rectangle is the whole size
        Assert.Equal((200.0, 100.0, 210.0, 110.0), (measure.DataWidth, measure.DataHeight, measure.Width, measure.Height));
    }

    /// <summary>
    ///     Validates that a size smaller than the guides leaves an empty data rectangle instead of a negative one, and that the canvas stays the size.
    /// </summary>
    [Fact]
    public void ViewLayout_Measure_FitSmallerThanTheGuidesClampsTheDataRectangleToZero()
    {
        // Act
        var measure = Measure("{\"autosize\":\"fit\",\"padding\":0,\"width\":50,\"height\":50}", new ViewExtents(40, 40, 40, 40));

        // Assert
        Assert.Equal((0.0, 0.0, 50.0, 50.0), (measure.DataWidth, measure.DataHeight, measure.Width, measure.Height));
    }
}
