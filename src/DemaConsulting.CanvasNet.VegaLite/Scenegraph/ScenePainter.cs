// Portions derived from Vega (BSD-3-Clause); see third-party/README.md
using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.Rendering;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using Path = DemaConsulting.CanvasNet.Geometry.Path;
using RenderCanvas = DemaConsulting.CanvasNet.Rendering.Canvas;

namespace DemaConsulting.CanvasNet.VegaLite.Scenegraph;

/// <summary>
///     Where a scene is drawn on the target surface: a uniform scale and a translation.
/// </summary>
/// <param name="Scale">The factor that maps scene pixels to surface pixels; positive.</param>
/// <param name="OffsetX">The surface x of the scene's origin.</param>
/// <param name="OffsetY">The surface y of the scene's origin.</param>
internal readonly record struct ScenePlacement(double Scale, double OffsetX, double OffsetY)
{
    /// <summary>
    ///     Places the scene at the origin of the surface, enlarged by a factor.
    /// </summary>
    /// <param name="scale">The factor.</param>
    /// <returns>The placement.</returns>
    public static ScenePlacement Uniform(double scale) => new(scale, 0, 0);

    /// <summary>
    ///     Fits the scene into a target: scaled uniformly by the largest factor that fits, and centered.
    /// </summary>
    /// <param name="sceneWidth">The scene width.</param>
    /// <param name="sceneHeight">The scene height.</param>
    /// <param name="targetWidth">The target width in pixels.</param>
    /// <param name="targetHeight">The target height in pixels.</param>
    /// <returns>The placement; a scene without area is not scaled.</returns>
    public static ScenePlacement Fit(double sceneWidth, double sceneHeight, int targetWidth, int targetHeight)
    {
        if (!(sceneWidth > 0) || !(sceneHeight > 0))
        {
            return new ScenePlacement(1, 0, 0);
        }

        var scale = Math.Min(targetWidth / sceneWidth, targetHeight / sceneHeight);
        return new ScenePlacement(scale, (targetWidth - (sceneWidth * scale)) / 2, (targetHeight - (sceneHeight * scale)) / 2);
    }
}

/// <summary>
///     Draws a <see cref="Scene"/> onto a core <see cref="Surface"/>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Order and paints.</b> The surface is first filled with the scene's background (transparent when none), then
///         the items are drawn in list order, parents before children. Fills and strokes are anti-aliased by the core
///         path filler; the stroke of an item is built in the item's own units (so widths and dashes scale with the chart)
///         and filled; opacity is folded into the alpha of the color. A paint that is none, unparseable or fully
///         transparent draws nothing.
///     </para>
///     <para>
///         <b>Gradient fills.</b> An item with a <see cref="SceneGradient"/> is filled with the core
///         <see cref="LinearGradient"/> (pad spread) instead of its solid fill. The start and end of the gradient are
///         fractions of the bounding box of the item's path, mapped to surface pixels with the same matrix as the path;
///         the fill opacity scales the alpha of every stop. A gradient with no usable stop, or with geometry that is not
///         finite, is ignored and the solid fill is drawn. Group backgrounds, rectangles, paths and symbols can use a
///         gradient; text and strokes cannot. The core evaluates the gradient per pixel, so the cost is that of a solid
///         fill of the same area plus a constant per pixel and the number of stops is bounded by
///         <see cref="SceneGradient.MaxStops"/>.
///     </para>
///     <para>
///         <b>Transform.</b> The core library has no scale and no clip, so this unit does both itself: every path is
///         transformed by the placement (uniform scale, then translation, plus the translations of the enclosing groups)
///         before it is filled, and text is drawn at the scaled font size. Rotation applies to text and symbols. A group
///         with a clip draws its children onto a transparent layer the size of the clip's pixel box and composites it back
///         with the coverage of the clip rectangle, so a clip edge on a fractional coordinate is anti-aliased and a clip
///         edge on an integer coordinate is exact. The corner radius of a clip is not applied (clips are rectangles).
///     </para>
///     <para>
///         <b>Group background.</b> A stroked group is drawn offset as Vega does (half a pixel for a one-pixel stroke, so
///         the line covers exactly one pixel column and row). Text is filled only; a stroke on text is ignored. An unknown
///         symbol shape is drawn as a circle.
///     </para>
///     <para>
///         <b>Fonts.</b> Text uses the font that the <see cref="TextMeasurer"/> resolves (bundled Liberation fonts unless
///         the font options say otherwise), draws the lines the measurer produced (trimmed and truncated to the limit) at
///         the baseline offsets of Vega's text mark. The result is deterministic: the same scene, size and placement
///         always give the same bytes. The painter holds no state and is safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class ScenePainter
{
    /// <summary>Half of the square root of three, the height factor of Vega's triangle symbols.</summary>
    private static readonly double HalfSqrt3 = Math.Sqrt(3) / 2;

    /// <summary>The tangent of 30 degrees.</summary>
    private const double Tan30 = 0.5773502691896257;

    /// <summary>The largest distance in surface pixels between a curve and the polyline that approximates it when filling; small enough for symbols of a few pixels to stay round.</summary>
    private const float FlattenTolerance = 0.02f;

    /// <summary>The smallest dash pattern period, in surface pixels, that is stroked as dashes; a finer pattern is drawn solid.</summary>
    private const double MinDashPeriod = 0.25;

    /// <summary>The largest estimated number of dashes on one stroked path; a pattern that would make more is drawn solid.</summary>
    private const double MaxDashSegments = 50_000;

    /// <summary>The text measurer.</summary>
    private readonly TextMeasurer _measurer;

    /// <summary>
    ///     Initializes a painter.
    /// </summary>
    /// <param name="measurer">The measurer that resolves the fonts and measures the text.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="measurer"/> is null.</exception>
    public ScenePainter(TextMeasurer measurer)
    {
        ArgumentNullException.ThrowIfNull(measurer);
        _measurer = measurer;
    }

    /// <summary>
    ///     Draws a scene onto a new surface.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="width">The width of the surface in pixels, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="height">The height of the surface in pixels, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="placement">Where the scene goes on the surface.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>The surface; the caller owns it.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="scene"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled.</exception>
    public Surface Paint(Scene scene, int width, int height, ScenePlacement placement, CancellationToken cancellationToken = default)
    {
        var surface = new Surface(width, height);
        try
        {
            Paint(scene, surface, placement, cancellationToken);
            return surface;
        }
        catch
        {
            surface.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Draws a scene onto an existing surface, replacing its content with the background first.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="surface">The surface.</param>
    /// <param name="placement">Where the scene goes on the surface.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="scene"/> or <paramref name="surface"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale is not a positive finite number.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled.</exception>
    public void Paint(Scene scene, Surface surface, ScenePlacement placement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(surface);
        if (!double.IsFinite(placement.Scale) || placement.Scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(placement), placement.Scale, "The scale must be a finite number greater than zero.");
        }

        surface.Clear(CssColor.TryParse(scene.Background, out var background) ? background : new Rgba32(0, 0, 0, 0));
        var transform = Matrix3x2.CreateScale((float)placement.Scale) * Matrix3x2.CreateTranslation((float)placement.OffsetX, (float)placement.OffsetY);
        PaintGroup(scene.Root, new Context(surface, transform, (float)placement.Scale), cancellationToken);
    }

    /// <summary>The names of the symbol shapes that <see cref="SymbolPath"/> draws; any other name is drawn as a circle.</summary>
    private static readonly HashSet<string> SymbolShapes =
    [
        "circle", "square", "cross", "diamond", "arrow", "wedge", "triangle", "triangle-up", "triangle-down", "triangle-left", "triangle-right", "stroke",
    ];

    /// <summary>
    ///     Finds out whether a name is one of the symbol shapes that the painter draws.
    /// </summary>
    /// <param name="shape">The shape name.</param>
    /// <returns><see langword="true"/> for a known shape name; <see langword="false"/> for any other text (a custom SVG path, for example), which is drawn as a circle.</returns>
    internal static bool IsSymbolShape(string shape) => SymbolShapes.Contains(shape);

    /// <summary>
    ///     Gets the path of a symbol shape centered at the origin.
    /// </summary>
    /// <param name="shape">The shape name; an unknown name is a circle.</param>
    /// <param name="size">The area of the symbol's bounding box in square pixels.</param>
    /// <returns>The path; empty for a size that is not positive.</returns>
    internal static Path SymbolPath(string shape, double size)
    {
        if (!(size > 0) || !double.IsFinite(size))
        {
            return Path.Empty;
        }

        var r = Math.Sqrt(size) / 2;
        var builder = new PathBuilder();
        switch (shape)
        {
            case "square":
                var w = Math.Sqrt(size);
                return Path.Rectangle((float)(-w / 2), (float)(-w / 2), (float)w, (float)w);
            case "cross":
                var s = r / 2.5;
                return Polygon(builder, (-r, -s), (-r, s), (-s, s), (-s, r), (s, r), (s, s), (r, s), (r, -s), (s, -s), (s, -r), (-s, -r), (-s, -s));
            case "diamond":
                return Polygon(builder, (-r, 0), (0, -r), (r, 0), (0, r));
            case "arrow":
                var stem = r / 7;
                var head = r / 2.5;
                var notch = r / 8;
                return Polygon(builder, (-stem, r), (stem, r), (stem, -notch), (head, -notch), (0, -r), (-head, -notch), (-stem, -notch));
            case "wedge":
                var wedgeHeight = HalfSqrt3 * r;
                var wedgeOffset = wedgeHeight - (r * Tan30);
                var b = r / 4;
                return Polygon(builder, (0, -wedgeHeight - wedgeOffset), (-b, wedgeHeight - wedgeOffset), (b, wedgeHeight - wedgeOffset));
            case "triangle":
                var h = HalfSqrt3 * r;
                var o = h - (r * Tan30);
                return Polygon(builder, (0, -h - o), (-r, h - o), (r, h - o));
            case "triangle-up":
                return Polygon(builder, (0, -HalfSqrt3 * r), (-r, HalfSqrt3 * r), (r, HalfSqrt3 * r));
            case "triangle-down":
                return Polygon(builder, (0, HalfSqrt3 * r), (-r, -HalfSqrt3 * r), (r, -HalfSqrt3 * r));
            case "triangle-right":
                return Polygon(builder, (HalfSqrt3 * r, 0), (-HalfSqrt3 * r, -r), (-HalfSqrt3 * r, r));
            case "triangle-left":
                return Polygon(builder, (-HalfSqrt3 * r, 0), (HalfSqrt3 * r, -r), (HalfSqrt3 * r, r));
            case "stroke":
                return builder.MoveTo(new Vector2((float)-r, 0)).LineTo(new Vector2((float)r, 0)).Build();
            default:
                return Path.Circle(0, 0, (float)r);
        }
    }

    /// <summary>
    ///     Builds a closed polygon.
    /// </summary>
    /// <param name="builder">A fresh path builder.</param>
    /// <param name="points">The corners.</param>
    /// <returns>The path.</returns>
    private static Path Polygon(PathBuilder builder, params (double X, double Y)[] points)
    {
        for (var i = 0; i < points.Length; i++)
        {
            var point = new Vector2((float)points[i].X, (float)points[i].Y);
            if (i == 0)
            {
                builder.MoveTo(point);
            }
            else
            {
                builder.LineTo(point);
            }
        }

        return builder.Close().Build();
    }

    /// <summary>
    ///     Draws a group: its background, then its children (through a clip layer when it has a clip).
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="parent">The context of the group's parent.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    private void PaintGroup(SceneGroup group, Context parent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsFinite(group.X, group.Y))
        {
            return;
        }

        var local = parent with { Transform = Matrix3x2.CreateTranslation((float)group.X, (float)group.Y) * parent.Transform };
        PaintGroupBackground(group, local);

        if (group.Clip is { } clip)
        {
            PaintClipped(group, clip, local, cancellationToken);
            return;
        }

        PaintChildren(group, local, cancellationToken);
    }

    /// <summary>
    ///     Draws the children of a group.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="local">The context of the group's own coordinate system.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    private void PaintChildren(SceneGroup group, Context local, CancellationToken cancellationToken)
    {
        foreach (var child in group.Children)
        {
            switch (child)
            {
                case SceneGroup inner:
                    PaintGroup(inner, local, cancellationToken);
                    break;
                case SceneRect rect:
                    cancellationToken.ThrowIfCancellationRequested();
                    PaintRect(rect, local);
                    break;
                case SceneRule rule:
                    cancellationToken.ThrowIfCancellationRequested();
                    PaintRule(rule, local);
                    break;
                case ScenePath path:
                    cancellationToken.ThrowIfCancellationRequested();
                    PaintPath(path, local);
                    break;
                case SceneSymbol symbol:
                    cancellationToken.ThrowIfCancellationRequested();
                    PaintSymbol(symbol, local);
                    break;
                case SceneText text:
                    cancellationToken.ThrowIfCancellationRequested();
                    PaintText(text, local);
                    break;
            }
        }
    }

    /// <summary>
    ///     Draws the children of a group onto a transparent layer and composites the layer inside the clip rectangle.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="clip">The clip rectangle in the group's coordinates.</param>
    /// <param name="local">The context of the group's own coordinate system.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    private void PaintClipped(SceneGroup group, SceneClip clip, Context local, CancellationToken cancellationToken)
    {
        if (!IsFinite(clip.X, clip.Y, clip.Width, clip.Height) || clip.Width <= 0 || clip.Height <= 0)
        {
            return;
        }

        // The clip rectangle in surface pixels (the transform is a uniform scale and a translation), cut to the surface
        var corner1 = Vector2.Transform(new Vector2((float)clip.X, (float)clip.Y), local.Transform);
        var corner2 = Vector2.Transform(new Vector2((float)(clip.X + clip.Width), (float)(clip.Y + clip.Height)), local.Transform);
        var left = Math.Max(0, Math.Min(corner1.X, corner2.X));
        var top = Math.Max(0, Math.Min(corner1.Y, corner2.Y));
        var right = Math.Min(local.Target.Width, Math.Max(corner1.X, corner2.X));
        var bottom = Math.Min(local.Target.Height, Math.Max(corner1.Y, corner2.Y));
        if (right <= left || bottom <= top)
        {
            return;
        }

        var boxX = (int)Math.Floor(left);
        var boxY = (int)Math.Floor(top);
        var boxWidth = (int)Math.Ceiling(right) - boxX;
        var boxHeight = (int)Math.Ceiling(bottom) - boxY;

        using var layer = new Surface(boxWidth, boxHeight);
        var layerContext = new Context(layer, local.Transform * Matrix3x2.CreateTranslation(-boxX, -boxY), local.Scale);
        PaintChildren(group, layerContext, cancellationToken);

        // The coverage of each pixel column and row by the clip rectangle: 1 inside, the overlap at a fractional edge
        var columns = new float[boxWidth];
        for (var x = 0; x < boxWidth; x++)
        {
            columns[x] = (float)Overlap(boxX + x, left, right);
        }

        var coverage = new float[boxWidth];
        for (var y = 0; y < boxHeight; y++)
        {
            var rowCoverage = (float)Overlap(boxY + y, top, bottom);
            for (var x = 0; x < boxWidth; x++)
            {
                coverage[x] = columns[x] * rowCoverage;
            }

            local.Target.CompositeOverSpan(boxY + y, boxX, coverage, layer.GetRowSpan(y));
        }
    }

    /// <summary>
    ///     Computes how much of the pixel cell [index, index + 1] lies inside [low, high].
    /// </summary>
    /// <param name="index">The pixel index.</param>
    /// <param name="low">The lower edge.</param>
    /// <param name="high">The upper edge.</param>
    /// <returns>The overlap, from 0 to 1.</returns>
    private static double Overlap(int index, double low, double high) =>
        Math.Clamp(Math.Min(index + 1, high) - Math.Max(index, low), 0, 1);

    /// <summary>
    ///     Draws the fill and border of a group.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="local">The context of the group's own coordinate system.</param>
    private static void PaintGroupBackground(SceneGroup group, Context local)
    {
        var hasStroke = !CssColor.IsNone(group.Stroke);
        if ((CssColor.IsNone(group.Fill) && group.FillGradient is null && !hasStroke) || group.Opacity <= 0)
        {
            return;
        }

        // Vega offsets a stroked group so that a one-pixel stroke covers whole pixels
        var width = group.StrokeWidth ?? 1;
        var offset = group.StrokeOffset ?? (hasStroke && width > 0.5 && width < 1.5 ? 0.5 - Math.Abs(width - 1) : 0);
        var path = RectanglePath(offset, offset, group.Width, group.Height, group.CornerRadius);
        FillAndStroke(path, group, local, local.Transform);
    }

    /// <summary>
    ///     Draws a rectangle.
    /// </summary>
    /// <param name="rect">The rectangle.</param>
    /// <param name="local">The context of the enclosing group.</param>
    private static void PaintRect(SceneRect rect, Context local)
    {
        if (!IsFinite(rect.X, rect.Y, rect.Width, rect.Height) || rect.Opacity <= 0)
        {
            return;
        }

        FillAndStroke(RectanglePath(rect.X, rect.Y, rect.Width, rect.Height, rect.CornerRadius), rect, local, local.Transform);
    }

    /// <summary>
    ///     Draws a rule: a stroked line.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="local">The context of the enclosing group.</param>
    private static void PaintRule(SceneRule rule, Context local)
    {
        if (!IsFinite(rule.X, rule.Y, rule.X2, rule.Y2) || rule.Opacity <= 0 || (rule.X.CompareTo(rule.X2) == 0 && rule.Y.CompareTo(rule.Y2) == 0))
        {
            return;
        }

        var path = new PathBuilder()
            .MoveTo(new Vector2((float)rule.X, (float)rule.Y))
            .LineTo(new Vector2((float)rule.X2, (float)rule.Y2))
            .Build();
        StrokeOnly(path, rule, local, local.Transform);
    }

    /// <summary>
    ///     Draws a polyline or polygon.
    /// </summary>
    /// <param name="item">The path item.</param>
    /// <param name="local">The context of the enclosing group.</param>
    private static void PaintPath(ScenePath item, Context local)
    {
        if (item.Points.Count < 2 || item.Opacity <= 0 || item.Points.Any(p => !IsFinite(p.X, p.Y)))
        {
            return;
        }

        var builder = new PathBuilder();
        for (var i = 0; i < item.Points.Count; i++)
        {
            var point = new Vector2((float)item.Points[i].X, (float)item.Points[i].Y);
            if (i == 0)
            {
                builder.MoveTo(point);
            }
            else
            {
                builder.LineTo(point);
            }
        }

        if (item.Closed)
        {
            builder.Close();
        }

        FillAndStroke(builder.Build(), item, local, local.Transform);
    }

    /// <summary>
    ///     Draws a symbol.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="local">The context of the enclosing group.</param>
    private static void PaintSymbol(SceneSymbol symbol, Context local)
    {
        if (!IsFinite(symbol.X, symbol.Y, symbol.Size, symbol.Angle) || symbol.Opacity <= 0)
        {
            return;
        }

        // Rotate about the center first, then move the center to its position
        var placed = Matrix3x2.CreateRotation((float)(symbol.Angle * Math.PI / 180))
            * Matrix3x2.CreateTranslation((float)symbol.X, (float)symbol.Y)
            * local.Transform;
        FillAndStroke(SymbolPath(symbol.Shape, symbol.Size), symbol, local, placed);
    }

    /// <summary>
    ///     Draws text: each line at its baseline, filled, at the scaled font size.
    /// </summary>
    /// <param name="text">The text item.</param>
    /// <param name="local">The context of the enclosing group.</param>
    private void PaintText(SceneText text, Context local)
    {
        if (!IsFinite(text.X, text.Y, text.Dx, text.Dy, text.FontSize, text.Angle, text.Limit)
            || text.FontSize <= 0
            || !CssColor.TryResolvePaint(text.Fill, text.FillOpacity * text.Opacity, out var color))
        {
            return;
        }

        var size = (float)(text.FontSize * local.Scale);
        if (!(size > 0))
        {
            return;
        }

        var font = new TextFont(text.FontFamily, text.FontSize, text.FontWeight, text.FontStyle);
        var face = _measurer.Resolve(font).Font;
        var align = text.Align switch
        {
            "center" => TextAlign.Center,
            "right" => TextAlign.Right,
            _ => TextAlign.Left,
        };
        var step = TextMeasurer.LineHeight(text.FontSize, text.LineHeight);
        var baseline = text.Dy + TextMeasurer.BaselineOffset(text.Baseline, text.FontSize, text.LineHeight);

        // The anchor in surface pixels; rotation is about it, and offsets inside the rotated frame scale with the chart
        var anchor = Vector2.Transform(new Vector2((float)text.X, (float)text.Y), local.Transform);
        var canvas = new RenderCanvas(local.Target);
        canvas.Translate(anchor.X, anchor.Y);
        if (text.Angle != 0)
        {
            canvas.RotateDegrees((float)text.Angle);
        }

        for (var i = 0; i < text.Lines.Count; i++)
        {
            var line = _measurer.TextValue(text.Lines[i], font, text.Limit, text.Ellipsis);
            if (line.Length == 0)
            {
                continue;
            }

            var y = (baseline + (i * step)) * local.Scale;
            canvas.DrawText(line, (float)(text.Dx * local.Scale), (float)y, align, face, size, color);
        }
    }

    /// <summary>
    ///     Builds the path of a rectangle, rounded when a radius is given.
    /// </summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="radius">The corner radius.</param>
    /// <returns>The path; empty when the rectangle has no area.</returns>
    private static Path RectanglePath(double x, double y, double width, double height, double radius) =>
        radius > 0
            ? Path.RoundRectangle((float)x, (float)y, (float)width, (float)height, (float)radius)
            : Path.Rectangle((float)x, (float)y, (float)width, (float)height);

    /// <summary>
    ///     Fills a path and then strokes it, as the item's paints say.
    /// </summary>
    /// <param name="path">The path in the item's own units.</param>
    /// <param name="item">The item whose paints apply.</param>
    /// <param name="context">The context of the enclosing group.</param>
    /// <param name="transform">The matrix from the path's units to surface pixels.</param>
    private static void FillAndStroke(Path path, SceneItem item, Context context, Matrix3x2 transform)
    {
        if (path.Subpaths.Count == 0)
        {
            return;
        }

        if (!TryFillWithGradient(path, item, context, transform)
            && CssColor.TryResolvePaint(item.Fill, item.FillOpacity * item.Opacity, out var fill))
        {
            PathFiller.Fill(context.Target, path.Transform(transform), fill, FillRule.NonZero, FlattenTolerance);
        }

        StrokeOnly(path, item, context, transform);
    }

    /// <summary>
    ///     Fills a path with the gradient of an item, when it has a usable one.
    /// </summary>
    /// <remarks>
    ///     The start and end of the gradient are fractions of the bounding box of the path in the item's own units, mapped
    ///     to surface pixels with the same matrix as the path. The opacity of the fill (<c>FillOpacity * Opacity</c>)
    ///     multiplies the alpha of every stop. A stop whose color cannot be parsed or whose offset is not a number is
    ///     skipped, an offset outside 0 to 1 is clamped, and the spread is pad. A gradient with no usable stop or with
    ///     geometry that is not finite is not drawn and the solid fill is used instead; a fully transparent fill draws
    ///     nothing.
    /// </remarks>
    /// <param name="path">The path in the item's own units.</param>
    /// <param name="item">The item whose gradient applies.</param>
    /// <param name="context">The context of the enclosing group.</param>
    /// <param name="transform">The matrix from the path's units to surface pixels.</param>
    /// <returns><see langword="true"/> when the gradient took the place of the solid fill (even if nothing was visible);
    /// <see langword="false"/> when the item has no usable gradient.</returns>
    private static bool TryFillWithGradient(Path path, SceneItem item, Context context, Matrix3x2 transform)
    {
        var gradient = item.FillGradient;
        if (gradient is null || !IsFinite(gradient.X1, gradient.Y1, gradient.X2, gradient.Y2))
        {
            return false;
        }

        // The stops: the alpha of each color is scaled by the fill opacity, and unusable stops are dropped
        var opacity = item.FillOpacity * item.Opacity;
        var stops = new List<GradientStop>(gradient.Stops.Count);
        foreach (var stop in gradient.Stops)
        {
            if (double.IsNaN(stop.Offset) || !CssColor.TryParse(stop.Color, out var color))
            {
                continue;
            }

            stops.Add(new GradientStop((float)Math.Clamp(stop.Offset, 0, 1), CssColor.WithOpacity(color, opacity)));
        }

        var bounds = path.GetBounds(FlattenTolerance);
        if (stops.Count == 0 || bounds.IsEmpty)
        {
            return false;
        }

        // The gradient vector in the item's units (a fraction of its bounding box), then in surface pixels
        var start = Vector2.Transform(
            new Vector2((float)(bounds.X + (gradient.X1 * bounds.Width)), (float)(bounds.Y + (gradient.Y1 * bounds.Height))),
            transform);
        var end = Vector2.Transform(
            new Vector2((float)(bounds.X + (gradient.X2 * bounds.Width)), (float)(bounds.Y + (gradient.Y2 * bounds.Height))),
            transform);
        if (!float.IsFinite(start.X) || !float.IsFinite(start.Y) || !float.IsFinite(end.X) || !float.IsFinite(end.Y))
        {
            return false;
        }

        if (!(opacity > 0))
        {
            return true;
        }

        PathFiller.Fill(context.Target, path.Transform(transform), new LinearGradient(start, end, stops), FillRule.NonZero, FlattenTolerance);
        return true;
    }

    /// <summary>
    ///     Strokes a path as the item's stroke paint, width, cap, join and dashes say.
    /// </summary>
    /// <param name="path">The path in the item's own units.</param>
    /// <param name="item">The item whose paints apply.</param>
    /// <param name="context">The context of the enclosing group.</param>
    /// <param name="transform">The matrix from the path's units to surface pixels.</param>
    private static void StrokeOnly(Path path, SceneItem item, Context context, Matrix3x2 transform)
    {
        var requestedWidth = item.StrokeWidth ?? 1;
        if (path.Subpaths.Count == 0 || !(requestedWidth > 0) || !double.IsFinite(requestedWidth))
        {
            return;
        }

        // A width that is finite as a double can still overflow to infinity or underflow to zero as a float; such a
        // stroke cannot be drawn, so it is skipped rather than handed to the stroker
        var width = (float)requestedWidth;
        if (!float.IsFinite(width) || !(width > 0)
            || !CssColor.TryResolvePaint(item.Stroke, item.StrokeOpacity * item.Opacity, out var color))
        {
            return;
        }

        var cap = item.StrokeCap switch
        {
            "round" => LineCap.Round,
            "square" => LineCap.Square,
            _ => LineCap.Butt,
        };
        var join = item.StrokeJoin switch
        {
            "round" => LineJoin.Round,
            "bevel" => LineJoin.Bevel,
            _ => LineJoin.Miter,
        };
        var dashOffset = (float)item.StrokeDashOffset;
        var style = new StrokeStyle(
            width,
            cap,
            join,
            4f,
            DashArray(item.StrokeDash, path, context.Scale),
            float.IsFinite(dashOffset) ? dashOffset : 0f);

        // The outline is made in the item's units and then transformed, so widths and dashes scale with the chart
        var tolerance = FlattenTolerance / Math.Max(context.Scale, 1e-3f);
        var outline = PathStroker.Stroke(path, style, tolerance);
        PathFiller.Fill(context.Target, outline.Transform(transform), color, FillRule.NonZero, FlattenTolerance);
    }

    /// <summary>
    ///     Converts a dash pattern into the form the stroker accepts: odd patterns are repeated (as a canvas does) and
    ///     patterns that cannot draw anything, or that would take unbounded work, are solid.
    /// </summary>
    /// <remarks>
    ///     The entries are validated after the conversion to <see cref="float"/>, which is the type the stroker takes:
    ///     an entry that overflows to infinity, or a pattern whose entries all underflow to zero, is solid. The work of
    ///     the stroker grows with the number of dashes, which is the path length divided by the pattern period, so a
    ///     pattern is also solid when its period is below <see cref="MinDashPeriod"/> surface pixels (such a pattern
    ///     cannot be told from a solid line) or when the estimated dash count exceeds <see cref="MaxDashSegments"/>.
    /// </remarks>
    /// <param name="dash">The pattern, or null.</param>
    /// <param name="path">The path in the item's own units, whose length bounds the number of dashes.</param>
    /// <param name="scale">The uniform scale from the item's units to surface pixels.</param>
    /// <returns>The pattern, or null for a solid stroke.</returns>
    private static float[]? DashArray(IReadOnlyList<double>? dash, Path path, float scale)
    {
        if (dash is null || dash.Count == 0 || dash.Any(d => !double.IsFinite(d) || d < 0))
        {
            return null;
        }

        var values = dash.Select(d => (float)d).ToList();
        if (values.Any(v => !float.IsFinite(v)) || values.All(v => v == 0))
        {
            return null;
        }

        if (values.Count % 2 == 1)
        {
            values.AddRange(values.ToArray());
        }

        var period = values.Sum(v => (double)v);
        if (!(period * scale >= MinDashPeriod) || EstimateLength(path) / period * (values.Count / 2) > MaxDashSegments)
        {
            return null;
        }

        return values.ToArray();
    }

    /// <summary>
    ///     Estimates the length of a path from its commands: the chords and control polygons, and for an arc its chord
    ///     plus half the circumference of its radius.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The estimated length, which is at least the length of the chords.</returns>
    private static double EstimateLength(Path path)
    {
        double length = 0;
        foreach (var subpath in path.Subpaths)
        {
            var current = subpath.Start;
            foreach (var command in subpath.Commands)
            {
                var end = command.Type == PathCommandType.Close ? subpath.Start : command.EndPoint;
                length += command.Type switch
                {
                    PathCommandType.QuadraticBezierTo => Distance(current, command.Control1) + Distance(command.Control1, end),
                    PathCommandType.CubicBezierTo => Distance(current, command.Control1) + Distance(command.Control1, command.Control2) + Distance(command.Control2, end),
                    PathCommandType.ArcTo => Distance(current, end) + (Math.PI * Math.Max(Math.Abs(command.Radius.X), Math.Abs(command.Radius.Y))),
                    _ => Distance(current, end),
                };
                current = end;
            }
        }

        return length;
    }

    /// <summary>
    ///     Measures the distance between two points in double precision.
    /// </summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns>The distance.</returns>
    private static double Distance(Vector2 a, Vector2 b)
    {
        double dx = (double)a.X - b.X;
        double dy = (double)a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>
    ///     Determines whether numbers are all finite.
    /// </summary>
    /// <param name="values">The numbers.</param>
    /// <returns><see langword="true"/> when none is NaN or infinite.</returns>
    private static bool IsFinite(params double[] values) => Array.TrueForAll(values, double.IsFinite);

    /// <summary>
    ///     The drawing state: the target, the matrix from the current group's units to target pixels and the uniform scale.
    /// </summary>
    /// <param name="Target">The surface drawn on.</param>
    /// <param name="Transform">The matrix from the current coordinate system to target pixels.</param>
    /// <param name="Scale">The uniform scale of the matrix.</param>
    private readonly record struct Context(Surface Target, Matrix3x2 Transform, float Scale);
}
