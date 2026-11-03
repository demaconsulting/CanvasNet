// Portions derived from Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Layout;

/// <summary>
///     The padding around the content of a chart, in pixels.
/// </summary>
/// <param name="Left">The left padding.</param>
/// <param name="Top">The top padding.</param>
/// <param name="Right">The right padding.</param>
/// <param name="Bottom">The bottom padding.</param>
internal readonly record struct ViewPadding(double Left, double Top, double Right, double Bottom)
{
    /// <summary>Gets the horizontal padding: left plus right.</summary>
    public double Horizontal => Left + Right;

    /// <summary>Gets the vertical padding: top plus bottom.</summary>
    public double Vertical => Top + Bottom;
}

/// <summary>
///     How far the guides of a view (axes and legends) reach beyond the data rectangle on each side.
/// </summary>
/// <param name="Left">The reach to the left of the data rectangle (an axis at the left), 0 or more.</param>
/// <param name="Top">The reach above the data rectangle, 0 or more.</param>
/// <param name="Right">The reach to the right of the data rectangle, 0 or more.</param>
/// <param name="Bottom">The reach below the data rectangle, 0 or more.</param>
internal readonly record struct ViewExtents(double Left, double Top, double Right, double Bottom);

/// <summary>
///     How the canvas adapts to the content of the view (the type of the <c>autosize</c> property).
/// </summary>
internal enum AutosizeType
{
    /// <summary>The canvas grows around the data rectangle to hold the guides and the title.</summary>
    Pad,

    /// <summary>The canvas is the given size and the guides may overflow it.</summary>
    None,

    /// <summary>The width and the height are the size of the whole view: the data rectangle shrinks to leave room for the guides and the title.</summary>
    Fit,

    /// <summary>The width is the size of the whole view, as for <see cref="Fit"/>; the height pads like <see cref="Pad"/>.</summary>
    FitX,

    /// <summary>The height is the size of the whole view, as for <see cref="Fit"/>; the width pads like <see cref="Pad"/>.</summary>
    FitY,
}

/// <summary>
///     The appearance of the plot area of a view: the fill and border of the data rectangle.
/// </summary>
/// <param name="Fill">The fill as a CSS color, or null for none.</param>
/// <param name="Stroke">The stroke as a CSS color, or null for none.</param>
/// <param name="StrokeWidth">The stroke width, or null for 1.</param>
/// <param name="Opacity">The opacity of the whole plot background, or null for 1.</param>
/// <param name="FillOpacity">The fill opacity, or null for 1.</param>
/// <param name="StrokeOpacity">The stroke opacity, or null for 1.</param>
/// <param name="StrokeDash">The dash pattern of the stroke, or null for a solid stroke.</param>
/// <param name="CornerRadius">The corner radius, or null for square corners.</param>
internal sealed record ViewStyle(
    string? Fill,
    string? Stroke,
    double? StrokeWidth,
    double? Opacity,
    double? FillOpacity,
    double? StrokeOpacity,
    IReadOnlyList<double>? StrokeDash,
    double? CornerRadius);

/// <summary>
///     Sizes that the layout cannot know from the specification and the configuration alone.
/// </summary>
/// <param name="DiscreteWidth">The width of the data rectangle computed from the step of a discrete x scale, or null when the x scale is not discrete.</param>
/// <param name="DiscreteHeight">The height of the data rectangle computed from the step of a discrete y scale, or null when the y scale is not discrete.</param>
/// <param name="ContainerWidth">The width of the container, used for <c>width: "container"</c>.</param>
/// <param name="ContainerHeight">The height of the container, used for <c>height: "container"</c>.</param>
internal readonly record struct ViewSizeHints(double? DiscreteWidth = null, double? DiscreteHeight = null, double? ContainerWidth = null, double? ContainerHeight = null);

/// <summary>
///     The size of the data rectangle of a view that sizes itself (a chart in a concatenation or a facet).
/// </summary>
/// <param name="Width">The width of the data rectangle in pixels.</param>
/// <param name="Height">The height of the data rectangle in pixels.</param>
internal readonly record struct ViewSize(double Width, double Height);

/// <summary>
///     Everything that decides the size and look of a view before its guides are measured.
/// </summary>
/// <remarks>
///     Produced by <see cref="ViewLayout.Resolve"/> from the specification and the configuration, or built directly (as
///     the tests do) to run the layout on hand-picked numbers.
/// </remarks>
internal sealed record ViewLayoutRequest
{
    /// <summary>Gets the width of the data rectangle in pixels.</summary>
    public double DataWidth { get; init; }

    /// <summary>Gets the height of the data rectangle in pixels.</summary>
    public double DataHeight { get; init; }

    /// <summary>Gets the padding around the content.</summary>
    public ViewPadding Padding { get; init; }

    /// <summary>Gets the autosize type; <see cref="AutosizeType.Pad"/> by default.</summary>
    public AutosizeType Autosize { get; init; }

    /// <summary>Gets a value indicating whether <c>autosize.contains</c> is <c>padding</c>: with <see cref="AutosizeType.None"/> or a fit type the width and height then include the padding.</summary>
    public bool ContainsPadding { get; init; }

    /// <summary>Gets a value indicating whether the autosize is one of the fit types, which change how the legends count (see <see cref="LegendLayout.Combine(ViewExtents, LegendPlacement, double, double, bool)"/>).</summary>
    public bool IsFit => Autosize is AutosizeType.Fit or AutosizeType.FitX or AutosizeType.FitY;

    /// <summary>
    ///     Gets what <c>fit</c> was dropped for because the size is a step: <c>x</c> (the width), <c>y</c> (the height) or <c>both</c>; null when nothing was dropped. The caller reports it
    ///     (<c>vl-layout-fit-step</c>).
    /// </summary>
    public string? DroppedFit { get; init; }

    /// <summary>Gets the appearance of the plot area.</summary>
    public ViewStyle View { get; init; } = new(null, null, null, null, null, null, null, null);

    /// <summary>Gets the background of the canvas as a CSS color, or null for a transparent canvas.</summary>
    public string? Background { get; init; }

    /// <summary>Gets a value indicating whether the content of the view is clipped to the data rectangle (which then also decides the bounds of the view).</summary>
    public bool Clip { get; init; }

    /// <summary>
    ///     Gets the width of the container for a <c>width: "container"</c> chart that is fitted, or null. With <see cref="AutosizeType.Fit"/> or <see cref="AutosizeType.FitX"/> it is the width
    ///     that the whole view fills (the data rectangle, the guides and the title, and the padding when <see cref="ContainsPadding"/>); without it that size is <see cref="DataWidth"/>.
    ///     It is not used by the other autosize types.
    /// </summary>
    public double? ContainerWidth { get; init; }

    /// <summary>Gets the height of the container for a <c>height: "container"</c> chart that is fitted, or null; see <see cref="ContainerWidth"/>.</summary>
    public double? ContainerHeight { get; init; }
}

/// <summary>
///     The result of measuring a view: the final data rectangle, the bounds of everything, the size of the canvas and the
///     position of the data rectangle on it.
/// </summary>
/// <param name="DataWidth">The width of the data rectangle (it differs from the request for <c>autosize: none</c> that contains the padding, and for container sizes).</param>
/// <param name="DataHeight">The height of the data rectangle.</param>
/// <param name="ViewBounds">The bounding box of the data rectangle, the guides and the title in the coordinates of the view.</param>
/// <param name="Padding">The padding.</param>
/// <param name="OriginX">The x position of the data rectangle's top-left corner on the canvas: the padding plus the room that guides and title need on the left.</param>
/// <param name="OriginY">The y position of the data rectangle's top-left corner on the canvas.</param>
/// <param name="Width">The canvas width at a scale of one; can be fractional.</param>
/// <param name="Height">The canvas height at a scale of one; can be fractional.</param>
/// <param name="Title">The title group placed in the coordinates of the view, or null when there is no title.</param>
/// <param name="TitleBounds">The bounding box of the placed title in the coordinates of the view; empty when there is no title.</param>
internal sealed record ViewMeasure(
    double DataWidth,
    double DataHeight,
    SceneBounds ViewBounds,
    ViewPadding Padding,
    double OriginX,
    double OriginY,
    double Width,
    double Height,
    SceneGroup? Title,
    SceneBounds TitleBounds)
{
    /// <summary>
    ///     Gets the canvas size in whole pixels: the width and height rounded half away from zero.
    /// </summary>
    public VegaLiteSize NaturalSize => new(ToPixels(Width), ToPixels(Height));

    /// <summary>
    ///     Rounds a canvas dimension to whole pixels without overflowing.
    /// </summary>
    /// <param name="value">The dimension.</param>
    /// <returns>The rounded value, from 0 to <see cref="int.MaxValue"/>.</returns>
    private static int ToPixels(double value) => (int)Math.Min(int.MaxValue, Math.Max(0, Math.Round(value, MidpointRounding.AwayFromZero)));
}

/// <summary>
///     The scene of a laid-out view together with its measurements.
/// </summary>
/// <param name="Measure">The measurements.</param>
/// <param name="Scene">The scene: the canvas, its background, the view group and the title.</param>
internal sealed record ViewLayoutResult(ViewMeasure Measure, Scene Scene);

/// <summary>
///     Lays out a single view: the size of the data rectangle, the padding, the room the guides and the title need, and
///     the size of the canvas that holds them.
/// </summary>
/// <remarks>
///     <para>
///         <b>Two phases.</b> Like Vega, layout runs in two steps. <see cref="Resolve"/> reads the settings that do not
///         depend on the content (the data rectangle from <c>width</c>, <c>height</c> and the configuration, the padding,
///         <c>autosize</c>, the plot background and the canvas background). The guides are then measured by their
///         builders, which report how far they reach beyond the data rectangle (<see cref="ViewExtents"/>) and build the
///         title (<see cref="TitleBlock"/>); <see cref="Measure"/> places the title, computes the bounds of everything and
///         from them the canvas size, and <see cref="Build"/> assembles the scene around caller-supplied content.
///         <see cref="Compute"/> runs both.
///     </para>
///     <para>
///         <b>Sizes.</b> The data rectangle is the number in <c>width</c>/<c>height</c>; without one it is
///         <c>config.view.continuousWidth/continuousHeight</c> (300 by 300 in Vega-Lite 6), or, when the scale stage
///         reports a discrete scale, the size it computed from the step (<see cref="ViewSizeHints"/>); a
///         <c>{"step": n}</c> size with no hint falls back to the continuous size. <c>"container"</c> is Vega-Lite's <c>fit</c> to the container
///         the host gives (see Autosize below).
///     </para>
///     <para>
///         <b>Canvas.</b> With <c>autosize: pad</c> (the default) the canvas grows: left = the whole pixels that the
///         bounds of the view reach to the left of the data rectangle (a left axis, a wide title), likewise top, right and
///         bottom, so the canvas is the data rectangle plus those reaches plus the padding. With <c>autosize: none</c>
///         the canvas is the width and height as given plus the padding (or exactly the width and height when
///         <c>contains</c> is <c>padding</c>), and anything that does not fit is cut off.
///     </para>
///     <para>
///         <b>Autosize.</b> The type is Vega-Lite's merge (<c>normalizeAutoSize</c>): <c>pad</c>, then the default of a <c>"container"</c> size (<c>fit</c> for both,
///         <c>fit-x</c> for the width, <c>fit-y</c> for the height, each with <c>contains: padding</c>), then <c>config.autosize</c> if the configuration sets one,
///         then the <c>autosize</c> of the specification, property by property. With <c>fit</c> (<c>fit-x</c>, <c>fit-y</c> for one dimension) the width and height
///         (the number, or the container size) are the size of the whole view, Vega's <c>viewSizeLayout</c>: the data rectangle is what is left after the
///         guides and the title, each reach rounded up to whole pixels, and after the padding when <c>contains</c> is <c>padding</c>; the canvas is then that size, plus
///         the padding when <c>contains</c> is <c>content</c>. A dimension that the type does not fit still grows like <c>pad</c>. A fit is dropped for a dimension
///         that is sized by a step (Vega-Lite's <c>droppingFit</c>; <see cref="ViewLayoutRequest.DroppedFit"/>). A <c>"container"</c> size under <c>pad</c> or <c>none</c> is the container
///         size as the data rectangle.
///     </para>
///     <para>
///         <b>Compositions.</b> A concatenation or a facet has no data rectangle: <see cref="Resolve"/> with <c>composition</c> reads only the
///         padding and the background, each chart in it sizes itself with <see cref="ResolveSize"/> and is given its bounds with <see cref="CellBounds"/>,
///         <c>CompositionLayout</c> places the cells, and <see cref="Measure"/> then pads the content of the whole (a root of size 0 by 0, whose
///         guides reach as far as the content does).
///     </para>
///     <para>
///         <b>Numbers.</b> The padding is a number or an object whose missing sides are 0. The view group's border is
///         one pixel wide unless configured, and is not part of the bounds, so it does not enlarge the canvas. The class
///         is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static class ViewLayout
{
    /// <summary>The size of a root that has no data rectangle of its own: nothing, and no container.</summary>
    private static readonly (double Size, double? Container) NoContainer = (0, null);

    /// <summary>
    ///     Reads the size, padding, autosize and appearance of a single view from the specification and the configuration.
    /// </summary>
    /// <param name="root">The root of the specification, or null for an empty one.</param>
    /// <param name="config">The effective configuration.</param>
    /// <param name="hints">Sizes computed by the scale stage and the host.</param>
    /// <param name="backgroundOverride">A CSS background that replaces the background of the specification and the configuration, or null.</param>
    /// <param name="composition">
    ///     <see langword="true"/> for the root of a concatenation or a facet: the root then has no data rectangle of its own (its size is 0 by 0, and
    ///     <c>width</c>, <c>height</c>, <c>view</c> and <c>autosize</c> are not read; the charts inside size themselves, see <see cref="ResolveSize"/>)
    ///     and no plot background, so the canvas is the content plus the padding; the padding and the background are read as usual.
    /// </param>
    /// <returns>The request for the layout.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    public static ViewLayoutRequest Resolve(SpecNode? root, ResolvedConfig config, ViewSizeHints hints = default, string? backgroundOverride = null, bool composition = false)
    {
        ArgumentNullException.ThrowIfNull(config);
        var viewSpec = root?["view"];
        var (continuousWidth, continuousHeight) = ContinuousSize(viewSpec, config);

        // The autosize decides whether a "container" size is fitted, so it is resolved first, from what the sizes are
        var (widthContainer, heightContainer) = (IsContainer(root?["width"]), IsContainer(root?["height"]));
        var widthStep = IsStep(root?["width"], hints.DiscreteWidth);
        var heightStep = IsStep(root?["height"], hints.DiscreteHeight);
        var autosize = composition ? new AutosizeSetting(AutosizeType.Pad, false, null) : ResolveAutosize(root, config, widthContainer, heightContainer, widthStep, heightStep);
        var fitX = autosize.Type is AutosizeType.Fit or AutosizeType.FitX;
        var fitY = autosize.Type is AutosizeType.Fit or AutosizeType.FitY;
        var (width, containerWidth) = composition ? NoContainer : ResolveDimension(root?["width"], hints.DiscreteWidth, hints.ContainerWidth, continuousWidth, fitX);
        var (height, containerHeight) = composition ? NoContainer : ResolveDimension(root?["height"], hints.DiscreteHeight, hints.ContainerHeight, continuousHeight, fitY);

        string? background;
        if (backgroundOverride is not null)
        {
            background = backgroundOverride;
        }
        else if (root?["background"] is { IsString: true } specBackground)
        {
            background = specBackground.StringValue;
        }
        else
        {
            background = config.GetString("background") ?? "white";
        }

        return new ViewLayoutRequest
        {
            DataWidth = width,
            DataHeight = height,
            ContainerWidth = containerWidth,
            ContainerHeight = containerHeight,
            Padding = ReadPadding(root?["padding"], config.Get("padding")),
            Autosize = autosize.Type,
            ContainsPadding = autosize.ContainsPadding,
            DroppedFit = autosize.DroppedFit,
            View = composition ? new ViewStyle(null, null, null, null, null, null, null, null) : ReadViewStyle(viewSpec, config),
            Background = background,
        };
    }

    /// <summary>
    ///     Resolves the size of the data rectangle of a chart that sizes itself, the way a chart inside a concatenation or a facet does.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The size is the number in <c>width</c> or <c>height</c> of the chart; without one (or with a <c>{"step": n}</c>, whose size the scale
    ///         stage has computed) it is the size that the scale stage gives for a discrete scale (<see cref="ViewSizeHints.DiscreteWidth"/>), else
    ///         <c>view.continuousWidth</c> and <c>view.continuousHeight</c> (300 by 300 in Vega-Lite 6) of the chart's own <c>view</c> over the configuration.
    ///     </para>
    ///     <para>
    ///         <c>"container"</c> has no meaning inside a composition (Vega-Lite drops it with a warning that the feature catalog reports), so it is read as
    ///         if the property were missing; the container hints are not used.
    ///     </para>
    /// </remarks>
    /// <param name="chartRoot">The specification of the chart (its <c>width</c>, <c>height</c> and <c>view</c>), or null for the defaults.</param>
    /// <param name="config">The effective configuration.</param>
    /// <param name="hints">Sizes computed by the scale stage; only the discrete ones are used.</param>
    /// <returns>The size of the data rectangle.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    public static ViewSize ResolveSize(SpecNode? chartRoot, ResolvedConfig config, ViewSizeHints hints = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        var (continuousWidth, continuousHeight) = ContinuousSize(chartRoot?["view"], config);
        var width = ResolveDimension(IgnoreContainer(chartRoot?["width"]), hints.DiscreteWidth, null, continuousWidth, false).Size;
        var height = ResolveDimension(IgnoreContainer(chartRoot?["height"]), hints.DiscreteHeight, null, continuousHeight, false).Size;
        return new ViewSize(width, height);
    }

    /// <summary>
    ///     Gets the bounds of one cell of a composition: its data rectangle, the guides and the title, in the coordinates of the cell.
    /// </summary>
    /// <remarks>
    ///     Like <see cref="Measure"/> for the content of a view, without the padding and the autosize that only the whole canvas has: the bounds
    ///     start with the data rectangle at the origin and grow with the reach of the guides and with the placed title (which is positioned against the
    ///     bounds of the data rectangle and the guides). The stroke of the border of the cell is not part of the bounds of a single view, but it is
    ///     part of those of a cell: Vega expands the bounds of a stroked group, with everything in it (the guides and the title too), by half the stroke width on each side, so the
    ///     composition layout spaces the cells from it.
    /// </remarks>
    /// <param name="width">The width of the data rectangle.</param>
    /// <param name="height">The height of the data rectangle.</param>
    /// <param name="guides">How far the guides reach beyond the data rectangle.</param>
    /// <param name="title">The built title of the cell, or null.</param>
    /// <param name="strokeWidth">The width of the stroke of the border of the cell, or 0 when it has none; half of it is added on each side of the bounds of everything in the cell.</param>
    /// <returns>The bounds of the cell, and the title group placed in the coordinates of the cell (null without a title).</returns>
    public static (SceneBounds Bounds, SceneGroup? Title) CellBounds(double width, double height, ViewExtents guides, TitleBlock? title = null, double strokeWidth = 0)
    {
        var w = Math.Max(0, width);
        var h = Math.Max(0, height);
        var bounds = new SceneBounds(-Math.Max(0, guides.Left), -Math.Max(0, guides.Top), w + Math.Max(0, guides.Right), h + Math.Max(0, guides.Bottom));
        SceneGroup? placedTitle = null;
        if (title is not null)
        {
            SceneBounds titleBounds;
            (placedTitle, titleBounds) = title.Place(w, h, bounds);
            bounds = bounds.Union(titleBounds);
        }

        if (strokeWidth > 0)
        {
            // Vega expands the bounds of a stroked group, with everything in it, by half the stroke width on each side
            var half = strokeWidth / 2;
            bounds = new SceneBounds(bounds.X1 - half, bounds.Y1 - half, bounds.X2 + half, bounds.Y2 + half);
        }

        return (bounds, placedTitle);
    }

    /// <summary>
    ///     Measures a view: places the title, computes the bounds of everything and the size of the canvas.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="guides">How far the guides reach beyond the data rectangle.</param>
    /// <param name="title">The built title, or null.</param>
    /// <returns>The measurements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    public static ViewMeasure Measure(ViewLayoutRequest request, ViewExtents guides, TitleBlock? title = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var padding = request.Padding;
        var width = Math.Max(0, request.DataWidth);
        var height = Math.Max(0, request.DataHeight);

        if (request.Autosize == AutosizeType.None && request.ContainsPadding)
        {
            // The width and height then count the padding: the data rectangle is what is left of them
            width = Math.Max(0, width - padding.Horizontal);
            height = Math.Max(0, height - padding.Vertical);
        }

        // A fitted dimension is the size of the whole view: the container, or the width and height, less the padding that they contain
        var fitX = request.Autosize is AutosizeType.Fit or AutosizeType.FitX;
        var fitY = request.Autosize is AutosizeType.Fit or AutosizeType.FitY;
        var fitWidth = Math.Max(0, (request.ContainerWidth ?? width) - (request.ContainsPadding ? padding.Horizontal : 0));
        var fitHeight = Math.Max(0, (request.ContainerHeight ?? height) - (request.ContainsPadding ? padding.Vertical : 0));

        // The bounds start with the data rectangle and grow with the guides and the title
        var arrangement = Arrange(request.Clip, width, height, guides, title);
        if (fitX || fitY)
        {
            // The title is placed against the data rectangle, which in turn leaves room for the title: iterate (two passes settle a title above or below, and a title that fits the width)
            for (var pass = 0; pass < 3; pass++)
            {
                var (fittedWidth, fittedHeight) = FitSize(request.Clip, arrangement, (width, height), (fitX ? fitWidth : null, fitY ? fitHeight : null), guides);
                if (Math.Abs(fittedWidth - width) < 1e-9 && Math.Abs(fittedHeight - height) < 1e-9)
                {
                    break;
                }

                (width, height) = (fittedWidth, fittedHeight);
                arrangement = Arrange(request.Clip, width, height, guides, title);
            }
        }

        var bounds = arrangement.Bounds;
        double left = 0;
        double top = 0;
        double viewWidth = width;
        double viewHeight = height;
        if (request.Autosize != AutosizeType.None)
        {
            left = Math.Max(0, Math.Ceiling(-bounds.X1));
            top = Math.Max(0, Math.Ceiling(-bounds.Y1));
            viewWidth = fitX ? fitWidth : width + left + Math.Max(0, Math.Ceiling(bounds.X2 - width));
            viewHeight = fitY ? fitHeight : height + top + Math.Max(0, Math.Ceiling(bounds.Y2 - height));
        }

        return new ViewMeasure(
            width,
            height,
            bounds,
            padding,
            padding.Left + left,
            padding.Top + top,
            viewWidth + padding.Horizontal,
            viewHeight + padding.Vertical,
            arrangement.Title,
            arrangement.TitleBounds);
    }

    /// <summary>
    ///     Places the title against a data rectangle and gives the bounds of everything.
    /// </summary>
    /// <param name="clip">Whether the content is clipped to the data rectangle, which then is the bounds.</param>
    /// <param name="width">The width of the data rectangle.</param>
    /// <param name="height">The height of the data rectangle.</param>
    /// <param name="guides">How far the guides reach beyond the data rectangle.</param>
    /// <param name="title">The built title, or null.</param>
    /// <returns>The bounds, the placed title and its bounds.</returns>
    private static Arrangement Arrange(bool clip, double width, double height, ViewExtents guides, TitleBlock? title)
    {
        var bounds = new SceneBounds(-Math.Max(0, guides.Left), -Math.Max(0, guides.Top), width + Math.Max(0, guides.Right), height + Math.Max(0, guides.Bottom));
        SceneGroup? placedTitle = null;
        var titleBounds = SceneBounds.Empty;
        if (title is not null)
        {
            (placedTitle, titleBounds) = title.Place(width, height, bounds);
            bounds = bounds.Union(titleBounds);
        }

        return new Arrangement(clip ? new SceneBounds(0, 0, width, height) : bounds, placedTitle, titleBounds);
    }

    /// <summary>
    ///     Computes the data rectangle of a fitted view: Vega's <c>viewSizeLayout</c>, the size of the view less the reach of the guides and the title on each side, in whole pixels.
    /// </summary>
    /// <remarks>
    ///     The reach to the right and below is taken from the guides and the placed title rather than by subtracting the data rectangle from the bounds, so that a guide of a
    ///     whole number of pixels is not rounded up by the error of the subtraction. The far edge of an empty title is negative infinity, which never exceeds a guide.
    /// </remarks>
    /// <param name="clip">Whether the content is clipped to the data rectangle: nothing then reaches beyond it.</param>
    /// <param name="arrangement">The arrangement for the current data rectangle.</param>
    /// <param name="current">The current width and height of the data rectangle.</param>
    /// <param name="fit">The size of the view to fill in each dimension, or null for a dimension that is not fitted.</param>
    /// <param name="guides">How far the guides reach beyond the data rectangle.</param>
    /// <returns>The width and height of the data rectangle.</returns>
    private static (double Width, double Height) FitSize(bool clip, Arrangement arrangement, (double Width, double Height) current, (double? Width, double? Height) fit, ViewExtents guides)
    {
        var width = current.Width;
        var height = current.Height;
        var title = arrangement.TitleBounds;
        if (fit.Width is { } viewWidth)
        {
            var left = clip ? 0 : Math.Max(0, -arrangement.Bounds.X1);
            var right = clip ? 0 : Math.Max(Math.Max(0, guides.Right), title.X2 - current.Width);
            width = Math.Max(0, viewWidth - Math.Ceiling(left) - Math.Ceiling(right));
        }

        if (fit.Height is { } viewHeight)
        {
            var top = clip ? 0 : Math.Max(0, -arrangement.Bounds.Y1);
            var bottom = clip ? 0 : Math.Max(Math.Max(0, guides.Bottom), title.Y2 - current.Height);
            height = Math.Max(0, viewHeight - Math.Ceiling(top) - Math.Ceiling(bottom));
        }

        return (width, height);
    }

    /// <summary>
    ///     Assembles the scene of a measured view around its content.
    /// </summary>
    /// <remarks>
    ///     The root group holds the view group, translated to the data rectangle's origin and given the plot background and
    ///     border; the content (axes, marks, legends) is its children in the order given, followed by the title.
    /// </remarks>
    /// <param name="request">The request.</param>
    /// <param name="measure">The measurements of <see cref="Measure"/>.</param>
    /// <param name="content">The items inside the view group, in drawing order (they are positioned relative to the data rectangle).</param>
    /// <returns>The scene.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static Scene Build(ViewLayoutRequest request, ViewMeasure measure, IEnumerable<SceneItem> content)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentNullException.ThrowIfNull(content);
        var children = new List<SceneItem>(content);
        if (measure.Title is not null)
        {
            children.Add(measure.Title);
        }

        var style = request.View;
        var view = CreateViewGroup(style, measure.OriginX, measure.OriginY, measure.DataWidth, measure.DataHeight, request.Clip ? new SceneClip(0, 0, measure.DataWidth, measure.DataHeight) : null, children);
        var root = new SceneGroup("root", 0, 0) { Children = [view] };
        return new Scene(measure.Width, measure.Height, request.Background, root);
    }

    /// <summary>
    ///     Creates the group of a view: the plot area with its fill and border, which holds the guides, the marks and the title of the view.
    /// </summary>
    /// <remarks>
    ///     A single or layered view has one (<see cref="Build"/> places it on the canvas); every chart of a composition has one at its cell (the composition layout moves it).
    /// </remarks>
    /// <param name="style">The appearance of the plot area.</param>
    /// <param name="x">The x position of the data rectangle's top-left corner in the parent.</param>
    /// <param name="y">The y position of the data rectangle's top-left corner in the parent.</param>
    /// <param name="width">The width of the data rectangle.</param>
    /// <param name="height">The height of the data rectangle.</param>
    /// <param name="clip">The clip rectangle in the group's coordinates, or null for none.</param>
    /// <param name="children">The items inside the group, in drawing order.</param>
    /// <returns>The group, with the role <c>view</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="style"/> or <paramref name="children"/> is null.</exception>
    public static SceneGroup CreateViewGroup(ViewStyle style, double x, double y, double width, double height, SceneClip? clip, IReadOnlyList<SceneItem> children)
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(children);
        return new SceneGroup("view", x, y)
        {
            Width = width,
            Height = height,
            Fill = style.Fill,
            Stroke = style.Stroke,
            StrokeWidth = style.StrokeWidth,
            Opacity = style.Opacity ?? 1,
            FillOpacity = style.FillOpacity ?? 1,
            StrokeOpacity = style.StrokeOpacity ?? 1,
            StrokeDash = style.StrokeDash,
            CornerRadius = style.CornerRadius ?? 0,
            Clip = clip,
            Children = children,
        };
    }

    /// <summary>
    ///     Measures a view and builds its scene.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="guides">How far the guides reach beyond the data rectangle.</param>
    /// <param name="title">The built title, or null.</param>
    /// <param name="content">The items inside the view group, or null for none.</param>
    /// <returns>The measurements and the scene.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    public static ViewLayoutResult Compute(ViewLayoutRequest request, ViewExtents guides, TitleBlock? title = null, IEnumerable<SceneItem>? content = null)
    {
        var measure = Measure(request, guides, title);
        return new ViewLayoutResult(measure, Build(request, measure, content ?? []));
    }

    /// <summary>
    ///     Reads the continuous size of the data rectangle: <c>view.continuousWidth</c> and <c>view.continuousHeight</c>.
    /// </summary>
    /// <param name="viewSpec">The <c>view</c> object of the specification, or null.</param>
    /// <param name="config">The configuration.</param>
    /// <returns>The width and height; 300 by 300 when neither defines them.</returns>
    private static (double Width, double Height) ContinuousSize(SpecNode? viewSpec, ResolvedConfig config) =>
        (Math.Max(0, ViewNumber(viewSpec, config, "continuousWidth") ?? 300), Math.Max(0, ViewNumber(viewSpec, config, "continuousHeight") ?? 300));

    /// <summary>
    ///     Treats <c>"container"</c> as if the size were not given.
    /// </summary>
    /// <param name="node">The <c>width</c> or <c>height</c> of the specification, or null.</param>
    /// <returns>Null for <c>"container"</c>, otherwise <paramref name="node"/>.</returns>
    private static SpecNode? IgnoreContainer(SpecNode? node) =>
        node is { IsString: true } && string.Equals(node.StringValue, "container", StringComparison.Ordinal) ? null : node;

    /// <summary>
    ///     Resolves one size of the data rectangle.
    /// </summary>
    /// <param name="node">The <c>width</c> or <c>height</c> of the specification, or null.</param>
    /// <param name="discrete">The size computed from the step of a discrete scale, or null.</param>
    /// <param name="container">The container size, or null.</param>
    /// <param name="continuous">The configured continuous size.</param>
    /// <param name="fitted">Whether the dimension is fitted (<c>fit</c> in it): a container size is then the size to fit, else it is the size of the data rectangle.</param>
    /// <returns>The size, and the container size when the specification asks for <c>container</c> and the dimension is fitted.</returns>
    private static (double Size, double? Container) ResolveDimension(SpecNode? node, double? discrete, double? container, double continuous, bool fitted)
    {
        if (node is { IsNumber: true })
        {
            return (Math.Max(0, node.NumberValue), null);
        }

        if (IsContainer(node))
        {
            // A fitted dimension fills the container with the whole view; otherwise the container is the data rectangle (Vega-Lite's width signal)
            return fitted ? (continuous, container ?? continuous) : (Math.Max(0, container ?? continuous), null);
        }

        // A step size, or no size at all: a discrete scale decides, else the continuous size applies
        return (Math.Max(0, discrete ?? continuous), null);
    }

    /// <summary>
    ///     Determines whether a size is <c>"container"</c>.
    /// </summary>
    /// <param name="node">The <c>width</c> or <c>height</c> of the specification, or null.</param>
    /// <returns><see langword="true"/> for <c>"container"</c>.</returns>
    private static bool IsContainer(SpecNode? node) =>
        node is { IsString: true } && string.Equals(node.StringValue, "container", StringComparison.Ordinal);

    /// <summary>
    ///     Determines whether a size is decided by the step of a discrete scale: a <c>{"step": n}</c>, or no size at all, on a discrete scale (the scale stage then gave a size).
    /// </summary>
    /// <param name="node">The <c>width</c> or <c>height</c> of the specification, or null.</param>
    /// <param name="discrete">The size computed from the step of a discrete scale, or null.</param>
    /// <returns><see langword="true"/> for a step size.</returns>
    private static bool IsStep(SpecNode? node, double? discrete) =>
        discrete is not null && node is not { IsNumber: true } && !IsContainer(node);

    /// <summary>
    ///     Resolves the autosize of a view the way Vega-Lite normalizes it (<c>normalizeAutoSize</c> and the dropping of <c>fit</c> for a step size in <c>compile.ts</c>).
    /// </summary>
    /// <remarks>
    ///     The merge, from the lowest precedence: <c>pad</c> and <c>content</c>, the fit that a <c>"container"</c> size implies (with <c>contains: padding</c>),
    ///     <c>config.autosize</c> when the configuration sets one (the built-in <c>pad</c> does not count), the <c>autosize</c> of the specification; the type and <c>contains</c> merge
    ///     separately. A fit type is then dropped for a step dimension: for both it becomes <c>pad</c>, for one it becomes the fit of the <em>other</em> dimension (also when the type was
    ///     the fit of the step dimension, as Vega-Lite does).
    /// </remarks>
    /// <param name="root">The root of the specification, or null.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="widthContainer">Whether the width is <c>"container"</c>.</param>
    /// <param name="heightContainer">Whether the height is <c>"container"</c>.</param>
    /// <param name="widthStep">Whether the width is a step.</param>
    /// <param name="heightStep">Whether the height is a step.</param>
    /// <returns>The autosize.</returns>
    private static AutosizeSetting ResolveAutosize(SpecNode? root, ResolvedConfig config, bool widthContainer, bool heightContainer, bool widthStep, bool heightStep)
    {
        var (type, contains) = ImpliedAutosize(widthContainer, heightContainer);

        // The configuration only counts when it sets an autosize: the built-in default is the very node that the merge leaves in place
        if (config.Get("autosize") is { } configured && !ReferenceEquals(configured, ConfigResolver.Defaults.Get("autosize")))
        {
            var node = configured.IsObject ? configured["type"] : configured;
            type = node?.AsString() ?? type;
            contains = configured.IsObject ? configured["contains"]?.AsString() ?? contains : contains;
        }

        if (root?["autosize"] is { } specified)
        {
            var node = specified.IsObject ? specified["type"] : specified;
            type = node?.AsString() ?? type;
            contains = specified.IsObject ? specified["contains"]?.AsString() ?? contains : contains;
        }

        var resolved = type switch
        {
            "none" => AutosizeType.None,
            "fit" => AutosizeType.Fit,
            "fit-x" => AutosizeType.FitX,
            "fit-y" => AutosizeType.FitY,
            _ => AutosizeType.Pad,
        };
        var containsPadding = string.Equals(contains, "padding", StringComparison.Ordinal);
        if (resolved is not (AutosizeType.Fit or AutosizeType.FitX or AutosizeType.FitY) || !(widthStep || heightStep))
        {
            return new AutosizeSetting(resolved, containsPadding, null);
        }

        // Vega-Lite keeps the fit of the other dimension: a step width leaves fit-y, a step height fit-x, both leave pad
        var kept = KeptFit(widthStep, heightStep);
        return new AutosizeSetting(kept, containsPadding, kept == resolved ? null : DroppedDimension(widthStep, heightStep));
    }

    /// <summary>
    ///     Gets the autosize that a <c>"container"</c> size implies.
    /// </summary>
    /// <param name="widthContainer">Whether the width is <c>"container"</c>.</param>
    /// <param name="heightContainer">Whether the height is <c>"container"</c>.</param>
    /// <returns>The type and <c>contains</c> (<c>fit</c>, <c>fit-x</c> or <c>fit-y</c> with <c>padding</c>), or nulls when no size is a container.</returns>
    private static (string? Type, string? Contains) ImpliedAutosize(bool widthContainer, bool heightContainer)
    {
        if (widthContainer && heightContainer)
        {
            return ("fit", "padding");
        }

        if (widthContainer)
        {
            return ("fit-x", "padding");
        }

        return heightContainer ? ("fit-y", "padding") : (null, null);
    }

    /// <summary>
    ///     Gets the autosize that remains when a fit is dropped for the step dimensions.
    /// </summary>
    /// <param name="widthStep">Whether the width is a step.</param>
    /// <param name="heightStep">Whether the height is a step.</param>
    /// <returns><see cref="AutosizeType.Pad"/> when both are steps, <see cref="AutosizeType.FitY"/> for a step width, else <see cref="AutosizeType.FitX"/>.</returns>
    private static AutosizeType KeptFit(bool widthStep, bool heightStep)
    {
        if (widthStep && heightStep)
        {
            return AutosizeType.Pad;
        }

        return widthStep ? AutosizeType.FitY : AutosizeType.FitX;
    }

    /// <summary>
    ///     Names the dimension that a fit is dropped for.
    /// </summary>
    /// <param name="widthStep">Whether the width is a step.</param>
    /// <param name="heightStep">Whether the height is a step.</param>
    /// <returns><c>both</c>, <c>x</c> or <c>y</c>.</returns>
    private static string DroppedDimension(bool widthStep, bool heightStep)
    {
        if (widthStep && heightStep)
        {
            return "both";
        }

        return widthStep ? "x" : "y";
    }

    /// <summary>
    ///     The autosize of a view after normalization.
    /// </summary>
    /// <param name="Type">The type.</param>
    /// <param name="ContainsPadding">Whether the sizes contain the padding.</param>
    /// <param name="DroppedFit">The dimension that a fit was dropped for (<c>x</c>, <c>y</c>, <c>both</c>), or null.</param>
    private readonly record struct AutosizeSetting(AutosizeType Type, bool ContainsPadding, string? DroppedFit);

    /// <summary>
    ///     The title and the bounds of everything for one data rectangle.
    /// </summary>
    /// <param name="Bounds">The bounds of the data rectangle, the guides and the title (the data rectangle for a clipped view).</param>
    /// <param name="Title">The placed title, or null.</param>
    /// <param name="TitleBounds">The bounds of the placed title; empty without a title.</param>
    private readonly record struct Arrangement(SceneBounds Bounds, SceneGroup? Title, SceneBounds TitleBounds);

    /// <summary>
    ///     Reads a number of the view configuration, where the <c>view</c> property of the specification wins.
    /// </summary>
    /// <param name="viewSpec">The <c>view</c> object of the specification, or null.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="property">The property.</param>
    /// <returns>The value, or null.</returns>
    private static double? ViewNumber(SpecNode? viewSpec, ResolvedConfig config, string property) =>
        viewSpec?[property]?.AsNumber() ?? config.GetNumber("view." + property);

    /// <summary>
    ///     Reads the padding: a number for all sides, or an object whose missing sides are 0.
    /// </summary>
    /// <param name="spec">The <c>padding</c> of the specification, or null.</param>
    /// <param name="config">The configuration's <c>padding</c>, or null.</param>
    /// <returns>The padding; 5 on all sides when neither defines it.</returns>
    private static ViewPadding ReadPadding(SpecNode? spec, ConfigNode? config)
    {
        if (spec is not null)
        {
            if (spec.IsNumber)
            {
                var all = Math.Max(0, spec.NumberValue);
                return new ViewPadding(all, all, all, all);
            }

            if (spec.IsObject)
            {
                return new ViewPadding(Side(spec, "left"), Side(spec, "top"), Side(spec, "right"), Side(spec, "bottom"));
            }
        }

        if (config is { Kind: ConfigNodeKind.Number })
        {
            var all = Math.Max(0, config.NumberValue);
            return new ViewPadding(all, all, all, all);
        }

        if (config is { IsObject: true })
        {
            double ConfigSide(string name) => Math.Max(0, config[name]?.AsNumber() ?? 0);
            return new ViewPadding(ConfigSide("left"), ConfigSide("top"), ConfigSide("right"), ConfigSide("bottom"));
        }

        return new ViewPadding(5, 5, 5, 5);
    }

    /// <summary>
    ///     Reads one side of a padding object.
    /// </summary>
    /// <param name="padding">The padding object.</param>
    /// <param name="name">The side name.</param>
    /// <returns>The padding of the side, 0 when it is missing.</returns>
    private static double Side(SpecNode padding, string name) => Math.Max(0, padding[name]?.AsNumber() ?? 0);

    /// <summary>
    ///     Reads the appearance of the plot area: the specification's <c>view</c> over <c>style.cell</c> over <c>config.view</c>
    ///     over Vega's <c>cell</c> style.
    /// </summary>
    /// <param name="viewSpec">The <c>view</c> object of the specification, or null.</param>
    /// <param name="config">The configuration.</param>
    /// <returns>The style.</returns>
    private static ViewStyle ReadViewStyle(SpecNode? viewSpec, ResolvedConfig config)
    {
        string? Text(string property)
        {
            if (viewSpec?[property] is { } own)
            {
                return own.AsString();
            }

            var node = config.ResolveViewProperty(property);
            return node?.AsString();
        }

        double? Number(string property)
        {
            if (viewSpec?[property] is { } own)
            {
                return own.AsNumber();
            }

            return config.ResolveViewProperty(property)?.AsNumber();
        }

        IReadOnlyList<double>? Dash()
        {
            if (viewSpec?["strokeDash"] is { } own)
            {
                return own.IsArray ? own.Items.Where(i => i.IsNumber).Select(i => i.NumberValue).ToList() : null;
            }

            var node = config.ResolveViewProperty("strokeDash");
            return node is { Kind: ConfigNodeKind.Array } ? node.Items.Select(i => i.AsNumber() ?? 0).ToList() : null;
        }

        return new ViewStyle(
            Text("fill"),
            Text("stroke"),
            Number("strokeWidth"),
            Number("opacity"),
            Number("fillOpacity"),
            Number("strokeOpacity"),
            Dash(),
            Number("cornerRadius"));
    }
}
