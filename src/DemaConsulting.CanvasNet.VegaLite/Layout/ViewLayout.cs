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
///     How far the guides of a view (axes, later legends) reach beyond the data rectangle on each side.
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

    /// <summary>Gets a value indicating whether <c>autosize.contains</c> is <c>padding</c>: with <see cref="AutosizeType.None"/> the width and height then include the padding.</summary>
    public bool ContainsPadding { get; init; }

    /// <summary>Gets the appearance of the plot area.</summary>
    public ViewStyle View { get; init; } = new(null, null, null, null, null, null, null, null);

    /// <summary>Gets the background of the canvas as a CSS color, or null for a transparent canvas.</summary>
    public string? Background { get; init; }

    /// <summary>Gets a value indicating whether the content of the view is clipped to the data rectangle (which then also decides the bounds of the view).</summary>
    public bool Clip { get; init; }

    /// <summary>
    ///     Gets the width of the container for a <c>width: "container"</c> chart, or null: the data rectangle then fills
    ///     the container less the padding and the guides on the left and right, and <see cref="DataWidth"/> is ignored.
    /// </summary>
    public double? ContainerWidth { get; init; }

    /// <summary>Gets the height of the container for a <c>height: "container"</c> chart, or null; see <see cref="ContainerWidth"/>.</summary>
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
///         <c>{"step": n}</c> size with no hint falls back to the continuous size. <c>"container"</c> fits the container
///         the host gives (less padding and the guides at the sides, without the overflow of a wide title).
///     </para>
///     <para>
///         <b>Canvas.</b> With <c>autosize: pad</c> (the default) the canvas grows: left = the whole pixels that the
///         bounds of the view reach to the left of the data rectangle (a left axis, a wide title), likewise top, right and
///         bottom, so the canvas is the data rectangle plus those reaches plus the padding. With <c>autosize: none</c>
///         the canvas is the width and height as given plus the padding (or exactly the width and height when
///         <c>contains</c> is <c>padding</c>), and anything that does not fit is cut off. The other autosize types
///         (<c>fit</c>, <c>fit-x</c>, <c>fit-y</c>) are reported by the feature catalog and laid out as <c>pad</c>.
///     </para>
///     <para>
///         <b>Numbers.</b> The padding is a number or an object whose missing sides are 0. The view group's border is
///         one pixel wide unless configured, and is not part of the bounds, so it does not enlarge the canvas. The class
///         is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static class ViewLayout
{
    /// <summary>
    ///     Reads the size, padding, autosize and appearance of a single view from the specification and the configuration.
    /// </summary>
    /// <param name="root">The root of the specification, or null for an empty one.</param>
    /// <param name="config">The effective configuration.</param>
    /// <param name="hints">Sizes computed by the scale stage and the host.</param>
    /// <param name="backgroundOverride">A CSS background that replaces the background of the specification and the configuration, or null.</param>
    /// <returns>The request for the layout.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    public static ViewLayoutRequest Resolve(SpecNode? root, ResolvedConfig config, ViewSizeHints hints = default, string? backgroundOverride = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        var viewSpec = root?["view"];
        var continuousWidth = Math.Max(0, ViewNumber(viewSpec, config, "continuousWidth") ?? 300);
        var continuousHeight = Math.Max(0, ViewNumber(viewSpec, config, "continuousHeight") ?? 300);
        var (width, containerWidth) = ResolveDimension(root?["width"], hints.DiscreteWidth, hints.ContainerWidth, continuousWidth);
        var (height, containerHeight) = ResolveDimension(root?["height"], hints.DiscreteHeight, hints.ContainerHeight, continuousHeight);

        var autosize = root?["autosize"] is { } specAutosize ? ReadAutosize(specAutosize) : ReadAutosize(config.Get("autosize"));
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
            View = ReadViewStyle(viewSpec, config),
            Background = background,
        };
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
        var guideLeft = Math.Max(0, guides.Left);
        var guideTop = Math.Max(0, guides.Top);
        var guideRight = Math.Max(0, guides.Right);
        var guideBottom = Math.Max(0, guides.Bottom);

        if (request.ContainerWidth is { } containerWidth)
        {
            width = Math.Max(0, containerWidth - padding.Horizontal - guideLeft - guideRight);
        }

        if (request.ContainerHeight is { } containerHeight)
        {
            height = Math.Max(0, containerHeight - padding.Vertical - guideTop - guideBottom);
        }

        if (request.Autosize == AutosizeType.None && request.ContainsPadding)
        {
            // The width and height then count the padding: the data rectangle is what is left of them
            width = Math.Max(0, width - padding.Horizontal);
            height = Math.Max(0, height - padding.Vertical);
        }

        // The bounds start with the data rectangle and grow with the guides and the title
        var bounds = new SceneBounds(-guideLeft, -guideTop, width + guideRight, height + guideBottom);
        SceneGroup? placedTitle = null;
        var titleBounds = SceneBounds.Empty;
        if (title is not null)
        {
            (placedTitle, titleBounds) = title.Place(width, height, bounds);
            bounds = bounds.Union(titleBounds);
        }

        if (request.Clip)
        {
            bounds = new SceneBounds(0, 0, width, height);
        }

        double left = 0;
        double top = 0;
        double viewWidth = width;
        double viewHeight = height;
        if (request.Autosize == AutosizeType.Pad)
        {
            left = Math.Max(0, Math.Ceiling(-bounds.X1));
            top = Math.Max(0, Math.Ceiling(-bounds.Y1));
            viewWidth = width + left + Math.Max(0, Math.Ceiling(bounds.X2 - width));
            viewHeight = height + top + Math.Max(0, Math.Ceiling(bounds.Y2 - height));
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
            placedTitle,
            titleBounds);
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
        var view = new SceneGroup("view", measure.OriginX, measure.OriginY)
        {
            Width = measure.DataWidth,
            Height = measure.DataHeight,
            Fill = style.Fill,
            Stroke = style.Stroke,
            StrokeWidth = style.StrokeWidth,
            Opacity = style.Opacity ?? 1,
            FillOpacity = style.FillOpacity ?? 1,
            StrokeOpacity = style.StrokeOpacity ?? 1,
            StrokeDash = style.StrokeDash,
            CornerRadius = style.CornerRadius ?? 0,
            Clip = request.Clip ? new SceneClip(0, 0, measure.DataWidth, measure.DataHeight) : null,
            Children = children,
        };
        var root = new SceneGroup("root", 0, 0) { Children = [view] };
        return new Scene(measure.Width, measure.Height, request.Background, root);
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
    ///     Resolves one size of the data rectangle.
    /// </summary>
    /// <param name="node">The <c>width</c> or <c>height</c> of the specification, or null.</param>
    /// <param name="discrete">The size computed from the step of a discrete scale, or null.</param>
    /// <param name="container">The container size, or null.</param>
    /// <param name="continuous">The configured continuous size.</param>
    /// <returns>The size, and the container size when the specification asks for <c>container</c>.</returns>
    private static (double Size, double? Container) ResolveDimension(SpecNode? node, double? discrete, double? container, double continuous)
    {
        if (node is { IsNumber: true })
        {
            return (Math.Max(0, node.NumberValue), null);
        }

        if (node is { IsString: true } && string.Equals(node.StringValue, "container", StringComparison.Ordinal))
        {
            return (continuous, container ?? continuous);
        }

        // A step size, or no size at all: a discrete scale decides, else the continuous size applies
        return (Math.Max(0, discrete ?? continuous), null);
    }

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
    ///     Reads the autosize from the specification.
    /// </summary>
    /// <param name="node">The <c>autosize</c> property: a type name or an object with <c>type</c> and <c>contains</c>.</param>
    /// <returns>The type and the <c>contains</c> flag.</returns>
    private static (AutosizeType Type, bool ContainsPadding) ReadAutosize(SpecNode node)
    {
        var type = node.IsObject ? node["type"]?.AsString() : node.AsString();
        var contains = node.IsObject ? node["contains"]?.AsString() : null;
        return (ToAutosizeType(type), string.Equals(contains, "padding", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Reads the autosize from the configuration.
    /// </summary>
    /// <param name="node">The configuration's <c>autosize</c>, or null.</param>
    /// <returns>The type and the <c>contains</c> flag.</returns>
    private static (AutosizeType Type, bool ContainsPadding) ReadAutosize(ConfigNode? node)
    {
        if (node is null)
        {
            return (AutosizeType.Pad, false);
        }

        var type = node.IsObject ? node["type"]?.AsString() : node.AsString();
        var contains = node.IsObject ? node["contains"]?.AsString() : null;
        return (ToAutosizeType(type), string.Equals(contains, "padding", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Maps an autosize type name to the types that are laid out.
    /// </summary>
    /// <param name="type">The name.</param>
    /// <returns><see cref="AutosizeType.None"/> for <c>none</c>, otherwise <see cref="AutosizeType.Pad"/>.</returns>
    private static AutosizeType ToAutosizeType(string? type) =>
        string.Equals(type, "none", StringComparison.Ordinal) ? AutosizeType.None : AutosizeType.Pad;

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
