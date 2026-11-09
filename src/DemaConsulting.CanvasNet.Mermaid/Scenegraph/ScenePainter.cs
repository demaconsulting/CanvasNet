using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Mermaid.Scenegraph;

/// <summary>
///     Where a scene is drawn on the target surface: device = scene × <see cref="Scale"/> + offset.
/// </summary>
/// <param name="Scale">The factor that maps scene units to surface pixels; positive.</param>
/// <param name="OffsetX">The surface x of the scene origin.</param>
/// <param name="OffsetY">The surface y of the scene origin.</param>
internal readonly record struct ScenePlacement(double Scale, double OffsetX, double OffsetY)
{
    /// <summary>
    ///     Places the view box of a scene at the origin of the surface, enlarged by a factor.
    /// </summary>
    /// <param name="viewBox">The view box of the scene.</param>
    /// <param name="scale">The factor.</param>
    /// <returns>The placement.</returns>
    public static ScenePlacement Uniform(SceneRect viewBox, double scale) => new(scale, -viewBox.X * scale, -viewBox.Y * scale);

    /// <summary>
    ///     Fits the view box of a scene into a target: scaled uniformly by the largest factor that fits, and centered.
    /// </summary>
    /// <param name="viewBox">The view box of the scene.</param>
    /// <param name="targetWidth">The target width in pixels.</param>
    /// <param name="targetHeight">The target height in pixels.</param>
    /// <returns>The placement; a view box without area is not scaled.</returns>
    public static ScenePlacement Fit(SceneRect viewBox, int targetWidth, int targetHeight)
    {
        if (!(viewBox.Width > 0) || !(viewBox.Height > 0))
        {
            return new ScenePlacement(1, -viewBox.X, -viewBox.Y);
        }

        var scale = Math.Min(targetWidth / viewBox.Width, targetHeight / viewBox.Height);
        var offsetX = ((targetWidth - (viewBox.Width * scale)) / 2) - (viewBox.X * scale);
        var offsetY = ((targetHeight - (viewBox.Height * scale)) / 2) - (viewBox.Y * scale);
        return new ScenePlacement(scale, offsetX, offsetY);
    }
}

/// <summary>
///     Loads the picture of a <see cref="SceneImage"/> at the size it is drawn.
/// </summary>
internal interface ISceneImageLoader
{
    /// <summary>
    ///     Loads a picture.
    /// </summary>
    /// <param name="href">The link of the picture.</param>
    /// <param name="pixelWidth">The width of the box in device pixels; at least one.</param>
    /// <param name="pixelHeight">The height of the box in device pixels; at least one.</param>
    /// <returns>The picture, which the painter disposes, or null when it cannot be loaded (nothing is drawn).</returns>
    Surface? Load(string href, int pixelWidth, int pixelHeight);
}

/// <summary>
///     Draws a <see cref="Scene"/> onto a core <see cref="Surface"/>: the only part of the package that produces pixels.
/// </summary>
/// <remarks>
///     <para>
///         <b>Order and paints.</b> The surface is first filled with the background (transparent when none), then the items
///         are drawn in list order, parents before children, translated by their groups. Fills and strokes are
///         anti-aliased by the core path filler; a stroke is built in device space with its width and dashes scaled, so it
///         looks the same at every scale. A path with a drop shadow gets its shadow painted under it first. A fully
///         transparent paint draws nothing.
///     </para>
///     <para>
///         <b>Text.</b> Every run is drawn with the face that <see cref="CreateFontResolver"/> resolves for its family, weight
///         and style (Mermaid's default stack resolves to the bundled Liberation Sans), at the scaled size, placed after the
///         runs before it and anchored as the line says. Characters missing from the face come from the core's bundled
///         fallback fonts.
///     </para>
///     <para>
///         <b>Bounds.</b> Every coordinate is checked before it is drawn: an item with a value that is not finite or whose
///         magnitude exceeds <see cref="MaxMagnitude"/> pixels is not drawn, so the cost of flattening one item stays
///         bounded. Before an item is drawn, the device pixels of its box, clipped to the surface, are charged to
///         <see cref="MermaidLimits.MaxPaintedPixels"/>; the item that would pass the limit fails the call with a located
///         <see cref="MermaidRenderException"/> (<see cref="MermaidDiagnosticCodes.LimitExceeded"/>) before any of its
///         pixels is painted. Cancellation is checked before every item.
///     </para>
///     <para>
///         <b>Determinism.</b> The same scene, size and placement always give the same pixels. The painter keeps the count of
///         painted pixels of one painting, so an instance paints one scene on one thread.
///     </para>
/// </remarks>
internal sealed partial class ScenePainter
{
    /// <summary>
    ///     The largest magnitude, in device pixels, of a coordinate, size or font size that is drawn: four times
    ///     <see cref="Surface.MaxDimension"/>, far outside every surface.
    /// </summary>
    internal const double MaxMagnitude = 32_768;

    /// <summary>The largest distance in device pixels between a curve and the polyline that fills it.</summary>
    private const float FlattenTolerance = 0.1f;

    /// <summary>The smallest dash pattern period in device pixels that is stroked as dashes; a finer pattern is drawn solid.</summary>
    private const double MinDashPeriod = 0.25;

    /// <summary>The largest estimated number of dashes on one path; a pattern that would make more is drawn solid.</summary>
    private const double MaxDashSegments = 50_000;

    /// <summary>The fonts the text is drawn with.</summary>
    private readonly FontResolver _fonts;

    /// <summary>The loader of pictures, or null when no picture is drawn.</summary>
    private readonly ISceneImageLoader? _images;

    /// <summary>The most device pixels one painting may paint.</summary>
    private readonly long _maxPaintedPixels;

    /// <summary>The device pixels painted so far.</summary>
    private long _painted;

    /// <summary>
    ///     Initializes a painter.
    /// </summary>
    /// <param name="fonts">The font resolver (normally <see cref="CreateFontResolver"/>, shared with the label measurement).</param>
    /// <param name="images">The picture loader, or null to draw no pictures.</param>
    /// <param name="maxPaintedPixels">The most device pixels one painting may paint (<see cref="MermaidLimits.MaxPaintedPixels"/>).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fonts"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxPaintedPixels"/> is less than one.</exception>
    public ScenePainter(FontResolver fonts, ISceneImageLoader? images = null, long maxPaintedPixels = long.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPaintedPixels, 1L);
        _fonts = fonts;
        _images = images;
        _maxPaintedPixels = maxPaintedPixels;
    }

    /// <summary>
    ///     Creates the font resolver of a rendering: bundled fonts only, with the common aliases (Mermaid's
    ///     <c>"trebuchet ms", verdana, arial</c>) mapped to the bundled Liberation families, so text never depends on the
    ///     fonts installed on the machine.
    /// </summary>
    /// <returns>A new resolver for one rendering.</returns>
    public static FontResolver CreateFontResolver() =>
        new(new FontResolverOptions { MapCommonAliasesToBundled = true, UseSystemFonts = false });

    /// <summary>
    ///     Draws a scene onto a new surface.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="width">The width of the surface in pixels, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="height">The height of the surface in pixels, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="placement">Where the scene goes on the surface.</param>
    /// <param name="background">The background color, or null for transparent.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>The surface; the caller owns it.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="scene"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled.</exception>
    /// <exception cref="MermaidRenderException">Thrown when the painting would pass <see cref="MermaidLimits.MaxPaintedPixels"/>.</exception>
    public Surface Paint(Scene scene, int width, int height, ScenePlacement placement, Rgba32? background, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        cancellationToken.ThrowIfCancellationRequested();

        var surface = new Surface(width, height);
        try
        {
            // The background first; a new surface is transparent
            if (background is { } color)
            {
                surface.Clear(color);
            }

            PaintItems(surface, scene.Items, placement, cancellationToken);
            return surface;
        }
        catch
        {
            surface.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Draws items in order with a placement.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="items">The items.</param>
    /// <param name="placement">The placement, with the translations of the enclosing groups applied.</param>
    /// <param name="cancellationToken">The token.</param>
    private void PaintItems(Surface surface, IReadOnlyList<SceneItem> items, ScenePlacement placement, CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (item)
            {
                case SceneGroup group:
                    var inner = placement with
                    {
                        OffsetX = placement.OffsetX + (group.OffsetX * placement.Scale),
                        OffsetY = placement.OffsetY + (group.OffsetY * placement.Scale),
                    };
                    if (IsBounded(inner.OffsetX, inner.OffsetY))
                    {
                        PaintItems(surface, group.Children, inner, cancellationToken);
                    }

                    break;
                case ScenePath path:
                    PaintPath(surface, path, placement);
                    break;
                case SceneText text:
                    PaintText(surface, text, placement);
                    break;
                case SceneImage image:
                    PaintImage(surface, image, placement);
                    break;
            }
        }
    }

    /// <summary>
    ///     Fills and strokes a path.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="item">The path item.</param>
    /// <param name="placement">The placement.</param>
    private void PaintPath(Surface surface, ScenePath item, ScenePlacement placement)
    {
        if (item.Segments.Count == 0 || !SegmentsBounded(item.Segments, placement))
        {
            return;
        }

        var path = Scene.ToPath(item.Segments, placement.Scale, placement.OffsetX, placement.OffsetY);
        var bounds = path.GetBounds();
        if (bounds.IsEmpty)
        {
            return;
        }

        // The shadow first, under the shape
        if (item.Shadow is { } shadow)
        {
            PaintShadow(surface, item, shadow, placement, bounds);
        }

        FillAndStroke(surface, item, path, placement, bounds, charge: true);
    }

    /// <summary>
    ///     Fills and strokes the device path of a path item.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="item">The path item.</param>
    /// <param name="path">The item's geometry in device space.</param>
    /// <param name="placement">The placement the geometry was built with.</param>
    /// <param name="bounds">The bounds of the geometry.</param>
    /// <param name="charge">Whether the painted pixels are charged to the budget (not for a shadow's silhouette).</param>
    private void FillAndStroke(Surface surface, ScenePath item, Geometry.Path path, ScenePlacement placement, Geometry.Rect bounds, bool charge)
    {
        // The fill
        if (item.Fill is { A: > 0 } fill)
        {
            if (charge)
            {
                Charge(surface, bounds.X, bounds.Y, bounds.Right, bounds.Bottom, item);
            }

            PathFiller.Fill(surface, path, fill, item.FillRule, FlattenTolerance);
        }

        // The stroke, built in device space with the scaled width and dashes
        if (item.Stroke is { Color.A: > 0 } stroke && stroke.Width > 0 && double.IsFinite(stroke.Width))
        {
            var width = stroke.Width * placement.Scale;
            if (!(width > 0) || width > MaxMagnitude)
            {
                return;
            }

            var half = width / 2;
            if (charge)
            {
                Charge(surface, bounds.X - half, bounds.Y - half, bounds.Right + half, bounds.Bottom + half, item);
            }

            var dash = DashFor(stroke, placement.Scale, item.Segments, out var dashOffset);
            var style = new StrokeStyle(
                (float)width,
                stroke.Cap,
                stroke.Join,
                4f,
                dash,
                dashOffset);
            var outline = PathStroker.Stroke(path, style, FlattenTolerance);
            PathFiller.Fill(surface, outline, stroke.Color, FillRule.NonZero, FlattenTolerance);
        }
    }

    /// <summary>
    ///     Gets the device dash pattern of a stroke, or null for a solid line (no pattern, an invalid one, one finer than a
    ///     pixel fraction, one with an entry longer than <see cref="MaxMagnitude"/> pixels, or one that would make too many
    ///     dashes), and its device dash offset.
    /// </summary>
    /// <param name="stroke">The stroke.</param>
    /// <param name="scale">The scale.</param>
    /// <param name="segments">The geometry, for the length estimate.</param>
    /// <param name="offset">
    ///     The device dash offset: zero for a solid line, and an offset beyond <see cref="MaxMagnitude"/> pixels reduced
    ///     by whole periods (which draws the same dashes) so that it stays finite in single precision.
    /// </param>
    /// <returns>The pattern, or null.</returns>
    private static float[]? DashFor(SceneStroke stroke, double scale, IReadOnlyList<SceneSegment> segments, out float offset)
    {
        offset = 0;
        if (stroke.Dash is not { Count: > 0 } dash || !double.IsFinite(stroke.DashOffset))
        {
            return null;
        }

        // A pattern that is invalid for SVG (a negative or non-finite entry, all zeros) is drawn solid, as browsers do;
        // so is one with a dash longer than any drawing (whose single-precision value could be infinite)
        var period = 0.0;
        foreach (var d in dash)
        {
            if (!double.IsFinite(d) || d < 0 || d * scale > MaxMagnitude)
            {
                return null;
            }

            period += d * scale;
        }

        if (period < MinDashPeriod || EstimateLength(segments) * scale / period > MaxDashSegments)
        {
            return null;
        }

        var deviceOffset = stroke.DashOffset * scale;
        offset = (float)(Math.Abs(deviceOffset) <= MaxMagnitude ? deviceOffset : deviceOffset % period);
        return [.. dash.Select(d => (float)(d * scale))];
    }

    /// <summary>
    ///     Estimates the length of a path from its control polygon (an upper bound of the curve length).
    /// </summary>
    /// <param name="segments">The segments.</param>
    /// <returns>The estimate in scene units.</returns>
    private static double EstimateLength(IReadOnlyList<SceneSegment> segments)
    {
        double length = 0, x = 0, y = 0, startX = 0, startY = 0;
        foreach (var s in segments)
        {
            switch (s.Kind)
            {
                case SceneSegmentKind.MoveTo:
                    startX = s.X;
                    startY = s.Y;
                    break;
                case SceneSegmentKind.QuadraticTo:
                    length += Distance(x, y, s.X1, s.Y1) + Distance(s.X1, s.Y1, s.X, s.Y);
                    break;
                case SceneSegmentKind.CubicTo:
                    length += Distance(x, y, s.X1, s.Y1) + Distance(s.X1, s.Y1, s.X2, s.Y2) + Distance(s.X2, s.Y2, s.X, s.Y);
                    break;
                case SceneSegmentKind.ArcTo:
                    length += Math.PI * Math.Max(Math.Abs(s.X1), Math.Abs(s.Y1)) * 2;
                    break;
                case SceneSegmentKind.Close:
                    length += Distance(x, y, startX, startY);
                    x = startX;
                    y = startY;
                    continue;
                default:
                    length += Distance(x, y, s.X, s.Y);
                    break;
            }

            x = s.X;
            y = s.Y;
        }

        return length;
    }

    /// <summary>
    ///     Gets the distance of two points.
    /// </summary>
    /// <param name="x1">The first x.</param>
    /// <param name="y1">The first y.</param>
    /// <param name="x2">The second x.</param>
    /// <param name="y2">The second y.</param>
    /// <returns>The distance.</returns>
    private static double Distance(double x1, double y1, double x2, double y2) => Math.Sqrt(((x2 - x1) * (x2 - x1)) + ((y2 - y1) * (y2 - y1)));

    /// <summary>
    ///     Checks that every coordinate of a path is finite and within <see cref="MaxMagnitude"/> once placed.
    /// </summary>
    /// <param name="segments">The segments.</param>
    /// <param name="placement">The placement.</param>
    /// <returns>Whether the path can be drawn.</returns>
    private static bool SegmentsBounded(IReadOnlyList<SceneSegment> segments, ScenePlacement placement)
    {
        foreach (var s in segments)
        {
            var radiusOk = s.Kind != SceneSegmentKind.ArcTo || (IsBounded(s.X1 * placement.Scale, s.Y1 * placement.Scale) && double.IsFinite(s.X2));
            if (!radiusOk ||
                !IsBounded((s.X * placement.Scale) + placement.OffsetX, (s.Y * placement.Scale) + placement.OffsetY) ||
                (s.Kind is SceneSegmentKind.QuadraticTo or SceneSegmentKind.CubicTo &&
                 !IsBounded((s.X1 * placement.Scale) + placement.OffsetX, (s.Y1 * placement.Scale) + placement.OffsetY)) ||
                (s.Kind == SceneSegmentKind.CubicTo &&
                 !IsBounded((s.X2 * placement.Scale) + placement.OffsetX, (s.Y2 * placement.Scale) + placement.OffsetY)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Checks that values are finite and within <see cref="MaxMagnitude"/>.
    /// </summary>
    /// <param name="values">The values in device pixels.</param>
    /// <returns>Whether all are.</returns>
    private static bool IsBounded(params double[] values) => values.All(v => double.IsFinite(v) && Math.Abs(v) <= MaxMagnitude);

    /// <summary>
    ///     Charges the pixels of a device box, clipped to the surface, to the painting budget.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="left">The left edge.</param>
    /// <param name="top">The top edge.</param>
    /// <param name="right">The right edge.</param>
    /// <param name="bottom">The bottom edge.</param>
    /// <param name="item">The item, for the location of the failure.</param>
    /// <exception cref="MermaidRenderException">Thrown when the budget would be exceeded.</exception>
    private void Charge(Surface surface, double left, double top, double right, double bottom, SceneItem item)
    {
        var width = Math.Clamp(Math.Ceiling(right), 0, surface.Width) - Math.Clamp(Math.Floor(left), 0, surface.Width);
        var height = Math.Clamp(Math.Ceiling(bottom), 0, surface.Height) - Math.Clamp(Math.Floor(top), 0, surface.Height);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var area = (long)(width * height);
        if (_painted + area > _maxPaintedPixels)
        {
            throw new MermaidRenderException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"painting the diagram would paint more than the limit of {_maxPaintedPixels} pixels (MermaidLimits.MaxPaintedPixels)"),
                item.Location,
                "render at a smaller scale or size, or raise MermaidLimits.MaxPaintedPixels");
        }

        _painted += area;
    }

}
