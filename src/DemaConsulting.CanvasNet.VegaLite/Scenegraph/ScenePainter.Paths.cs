// Portions derived from d3-shape (ISC), d3-path (ISC), Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause); see third-party/README.md
using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.VegaLite.Data;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.VegaLite.Scenegraph;

/// <summary>
///     The painting of paths with segments, arcs and trails, and of the miter limit (a part of <see cref="ScenePainter"/>).
/// </summary>
/// <remarks>
///     <para>
///         <b>Segments.</b> A <see cref="ScenePath"/> with <see cref="ScenePath.Segments"/> is drawn from its commands: a
///         line or cubic command before the first move is skipped, a command after a close continues at the start of the
///         closed subpath (as d3-path does), and a command with a coordinate that is not finite, or an end point beyond
///         <see cref="MaxMagnitude"/> surface pixels, makes the whole item draw nothing (as a points path does). A cubic command with a finite control point
///         beyond <see cref="MaxMagnitude"/> is drawn as a line to its end point. The fill is nonzero; the stroke is the item's stroke.
///     </para>
///     <para>
///         <b>Arcs.</b> A <see cref="SceneArc"/> is the outline of <see cref="ArcGeometry"/> translated to the center of the
///         item; an arc with a member that is not finite, or a center, radius or corner radius beyond <see cref="MaxMagnitude"/> surface pixels, draws nothing.
///     </para>
///     <para>
///         <b>Trails.</b> A <see cref="SceneTrail"/> is drawn as Vega's trail: for every pair of consecutive points of a run a
///         capsule of two straight edges and two half circles, all capsules filled and stroked as one path. A point that has
///         a coordinate or a size that is not finite, a magnitude above <see cref="MaxMagnitude"/> surface pixels or a size that is not positive ends its run; a
///         run needs two points to draw anything. Two coincident points draw a circle (Vega starts it with a line from the
///         previous capsule; this painter starts it with a move, which has the same fill and no stray stroke).
///         Cancellation is polled every 1,024 points and before every fill. The filler cannot be interrupted, so the capsules are filled and stroked
///         in chunks of at most <see cref="TrailChunkWeight"/> (the estimate of <see cref="TrailCapsuleWeight"/>): a trail of ordinary size is
///         one chunk, and a trail of huge points costs a bounded amount between two polls. A translucent trail of several chunks is painted through coverage layers (see
///         <c>TrailLayers</c>) and composited once, so that the capsules at the joints of the chunks do not composite twice (Vega paints one path:
///         <c>vega-scenegraph/src/path/trail.js</c>, <c>marks/markMultiItemPath.js</c>).
///     </para>
///     <para>
///         <b>Miter limit.</b> The miter limit of an item is 4 when it has none, 1 when it is not finite or is below 1 and
///         1,000 when it is above 1,000.
///     </para>
/// </remarks>
internal sealed partial class ScenePainter
{
    /// <summary>The miter limit of an item that has none.</summary>
    private const double DefaultMiterLimit = 4;

    /// <summary>The largest miter limit that is passed to the stroker.</summary>
    private const double MaxMiterLimit = 1000;

    /// <summary>The estimated cost, in line pieces of flattened curves, of the capsules that a trail paints in one fill (see <see cref="TrailCapsuleWeight"/>); a larger trail is painted in several chunks with a poll of the cancellation token between them. A chunk of the largest capsules (a radius of 16,384 pixels, weight about 906) is about 110 capsules, which take well under a second.</summary>
    internal const double TrailChunkWeight = 100_000;

    /// <summary>
    ///     Gets an observer that is told the number of capsules of a trail chunk that is followed by another one, just before the chunk is polled for cancellation and painted (so it is told
    ///     once for each chunk but the last); null in production. It makes the chunking of
    ///     a trail testable by counting (and lets a test cancel at a chunk boundary), without measuring time.
    /// </summary>
    internal Action<int>? TrailChunkFlushed { get; init; }

    /// <summary>The default budget of <see cref="TrailLayerPixelBudget"/>: 32 Mi pixels, which is 128 MiB of <see cref="Rgba32"/> (two layers of 4,096 by 4,096 pixels).</summary>
    internal const long DefaultTrailLayerPixels = 33_554_432;

    /// <summary>
    ///     Gets the most pixels, over all the layers of one trail, that the coverage layers of a translucent trail (see <c>TrailLayers</c>) may have; a trail whose layers would be larger is
    ///     painted chunk by chunk, as it was before the layers existed (the capsules at a joint between two chunks then composite twice). Tests lower it; the default is
    ///     <see cref="DefaultTrailLayerPixels"/>.
    /// </summary>
    internal long TrailLayerPixelBudget { get; init; } = DefaultTrailLayerPixels;

    /// <summary>
    ///     Gets an observer that is told the number of pixels (the width times the height times the number of layers: one for a translucent fill, one for a stroke that is translucent or under
    ///     a layered fill) of the layers of a trail when they are created, which is only for a trail of several chunks; null in production. It
    ///     makes the box of the layers, and the fallback beyond <see cref="TrailLayerPixelBudget"/>, testable without allocating the layers.
    /// </summary>
    internal Action<long>? TrailLayersCreated { get; init; }

    /// <summary>The number of trail points between two checks of the cancellation token.</summary>
    private const int TrailPollInterval = 1024;

    /// <summary>
    ///     Resolves the miter limit that the stroker receives.
    /// </summary>
    /// <param name="value">The miter limit of the item, or <see langword="null"/>.</param>
    /// <returns>The limit: 4 for <see langword="null"/>, otherwise the value clamped to 1 to 1,000 (1 when it is not a number).</returns>
    internal static float ResolveMiterLimit(double? value)
    {
        if (value is not { } limit)
        {
            return (float)DefaultMiterLimit;
        }

        if (!double.IsFinite(limit) || limit < 1)
        {
            return 1f;
        }

        return (float)Math.Min(limit, MaxMiterLimit);
    }

    /// <summary>
    ///     Draws a path that is made of drawing commands.
    /// </summary>
    /// <param name="item">The path item.</param>
    /// <param name="local">The context of the enclosing group.</param>
    private static void PaintSegments(ScenePath item, Context local)
    {
        // A coordinate that is not finite, or an end point beyond MaxMagnitude surface pixels, makes the whole item draw nothing
        if (item.Opacity <= 0 || item.Segments.Any(s => !s.IsFinite() || !IsEndPointBounded(s, local.Scale)))
        {
            return;
        }

        var builder = new PathBuilder();
        var hasStart = false;
        var isOpen = false;
        var startX = 0.0;
        var startY = 0.0;
        foreach (var segment in item.Segments)
        {
            if (segment.Kind == ScenePathSegmentKind.MoveTo)
            {
                builder.MoveTo(new Vector2((float)segment.X, (float)segment.Y));
                (hasStart, isOpen, startX, startY) = (true, true, segment.X, segment.Y);
                continue;
            }

            // Everything else needs a subpath; after a close it continues at the start of the closed subpath
            if (!hasStart)
            {
                continue;
            }

            if (!isOpen)
            {
                builder.MoveTo(new Vector2((float)startX, (float)startY));
                isOpen = true;
            }

            switch (segment.Kind)
            {
                case ScenePathSegmentKind.LineTo:
                    builder.LineTo(new Vector2((float)segment.X, (float)segment.Y));
                    break;
                case ScenePathSegmentKind.CubicTo:
                    // A control point beyond MaxMagnitude (a spline with a huge tension, for example) would make the flattening of the curve expensive: a line instead
                    if (IsBoundedAt(local.Scale, segment.X1, segment.Y1, segment.X2, segment.Y2))
                    {
                        builder.CubicBezierTo(
                            new Vector2((float)segment.X1, (float)segment.Y1),
                            new Vector2((float)segment.X2, (float)segment.Y2),
                            new Vector2((float)segment.X, (float)segment.Y));
                    }
                    else
                    {
                        builder.LineTo(new Vector2((float)segment.X, (float)segment.Y));
                    }

                    break;
                default:
                    builder.Close();
                    isOpen = false;
                    break;
            }
        }

        FillAndStroke(builder.Build(), item, local, local.Transform);
    }

    /// <summary>
    ///     Draws a circular or annular sector.
    /// </summary>
    /// <param name="arc">The arc item.</param>
    /// <param name="local">The context of the enclosing group.</param>
    private static void PaintArc(SceneArc arc, Context local)
    {
        // The center, the radii and the corner radius are lengths and must be drawable; the angles only have to be numbers
        // (ArcGeometry bounds them itself, and the sector repeats every full turn)
        if (!IsBoundedAt(local.Scale, arc.X, arc.Y, arc.InnerRadius, arc.OuterRadius, arc.CornerRadius)
            || !IsFinite(arc.StartAngle, arc.EndAngle, arc.PadAngle)
            || arc.Opacity <= 0)
        {
            return;
        }

        var path = ArcGeometry.Build(arc.InnerRadius, arc.OuterRadius, arc.StartAngle, arc.EndAngle, arc.PadAngle, arc.CornerRadius);
        var placed = Matrix3x2.CreateTranslation((float)arc.X, (float)arc.Y) * local.Transform;
        FillAndStroke(path, arc, local, placed);
    }

    /// <summary>
    ///     Draws a trail.
    /// </summary>
    /// <param name="trail">The trail item.</param>
    /// <param name="local">The context of the enclosing group.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    private void PaintTrail(SceneTrail trail, Context local, CancellationToken cancellationToken)
    {
        if (trail.Opacity <= 0)
        {
            return;
        }

        // The layers of a translucent trail that is painted in several chunks (see TrailLayers); created at the first chunk, when it is known that there are several
        TrailLayers? layers = null;
        var layersDecided = false;
        try
        {
            var writer = new ArcPathWriter();
            var weight = 0.0;
            var capsules = 0;
            var visited = 0;
            foreach (var run in trail.Runs)
            {
                var ready = false;
                double x1 = 0;
                double y1 = 0;
                double r1 = 0;
                foreach (var point in run)
                {
                    if (++visited % TrailPollInterval == 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    if (!IsDrawable(point, local.Scale))
                    {
                        ready = false;
                        continue;
                    }

                    var r2 = point.Size / 2;
                    if (ready)
                    {
                        // The filler cannot be interrupted, so the capsules are painted in chunks of a bounded cost, with a poll before each. A complete chunk is flushed
                        // only when another capsule follows it (not right after the capsule that completes it), so a flush means that the trail has a second chunk
                        // and the last chunk is never empty: a trail of one chunk never creates layers
                        if (weight >= TrailChunkWeight)
                        {
                            TrailChunkFlushed?.Invoke(capsules);
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!layersDecided)
                            {
                                layers = TrailLayers.TryCreate(trail, local, TrailLayerPixelBudget, cancellationToken);
                                layersDecided = true;
                                if (layers is not null)
                                {
                                    TrailLayersCreated?.Invoke(layers.Pixels);
                                }
                            }

                            PaintTrailChunk(writer.Build(), trail, local, layers);
                            writer = new ArcPathWriter();
                            (weight, capsules) = (0, 0);
                        }

                        AppendCapsule(writer, x1, y1, r1, point.X, point.Y, r2);
                        weight += TrailCapsuleWeight(Math.Max(r1, r2), local.Scale);
                        capsules++;
                    }

                    (ready, x1, y1, r1) = (true, point.X, point.Y, r2);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            PaintTrailChunk(writer.Build(), trail, local, layers);
            layers?.Composite(local.Target, cancellationToken);
        }
        finally
        {
            layers?.Dispose();
        }
    }

    /// <summary>
    ///     Paints one chunk of the capsules of a trail: straight onto the target, or, for a translucent trail in several chunks, into its layers.
    /// </summary>
    /// <param name="path">The path of the chunk in the units of the trail.</param>
    /// <param name="trail">The trail.</param>
    /// <param name="local">The context of the enclosing group.</param>
    /// <param name="layers">The layers of the trail, or null to paint onto the target.</param>
    private static void PaintTrailChunk(Path path, SceneTrail trail, Context local, TrailLayers? layers)
    {
        if (layers is null)
        {
            FillAndStroke(path, trail, local, local.Transform);
        }
        else
        {
            layers.PaintChunk(path, trail, local);
        }
    }

    /// <summary>
    ///     The coverage layers of a translucent trail that is painted in several chunks, so that it is painted as one path as Vega paints it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Vega paints a trail as one path (<c>vega-scenegraph/src/path/trail.js</c> builds all capsules into one path, <c>marks/markMultiItemPath.js</c> <c>draw</c> begins one
    ///         path and fills and strokes it once with the non-zero rule), so the capsules of a translucent trail overlap without compositing twice. Painted chunk by chunk,
    ///         the first capsule of a chunk repeats the half circle at the joint that the last capsule of the previous chunk already painted, and that circle would be composited
    ///         twice, a visible dot. The layers repair this: every chunk of the fill is painted with the opaque color into a transparent layer (the union of the coverage inside
    ///         the layer is the union of the one path), and once the last chunk is in, the layer is composited at the alpha of the paint, then the stroke layer. Compositing the
    ///         fill and then the stroke is the order of Vega's single path.
    ///     </para>
    ///     <para>
    ///         Only the paints that need it are layered. An opaque paint has no visible seam (painting it again per chunk gives the same pixels except possibly at anti-aliased edge pixels of a joint), so a solid fill is layered only when it is translucent,
    ///         and an opaque fill is painted straight onto the target chunk by chunk. A stroke is layered when it is translucent, and also when it is drawn under a layered fill whatever its
    ///         opacity: the layered fill is composited only after the last chunk, so an opaque stroke that was painted straight onto the target would be covered by it, and the layer keeps
    ///         the order of Vega's path (fill, then stroke). A gradient fill is not layered either. The two layers cannot be shared or reused: both are live until the last chunk, because
    ///         the fill and the stroke are painted into separate layers of every chunk and composited after it; only the buffer of the coverage of a row is shared by the composites.
    ///     </para>
    ///     <para>
    ///         A layer covers the pixel box of the trail (its points grown by the radius, the stroke width of a layered stroke and the antialiasing margin) cut to the target, so it never exceeds the
    ///         target, and the layers of one trail are charged against <see cref="TrailLayerPixelBudget"/> before they are allocated: a trail whose layers would have more pixels is painted chunk
    ///         by chunk without them. A gradient fill is still painted chunk by chunk, with the box of the chunk as its reference (the gradient is not layered).
    ///     </para>
    /// </remarks>
    private sealed class TrailLayers : IDisposable
    {
        /// <summary>The antialiasing margin around the points, in surface pixels.</summary>
        private const float Margin = 2;

        /// <summary>The layer of the fill, or null when the fill is not a translucent solid fill (it is then painted straight onto the target).</summary>
        private readonly Surface? _fill;

        /// <summary>The layer of the stroke, or null when the stroke is not layered (it is not drawn, or it is opaque and not under a layered fill).</summary>
        private readonly Surface? _stroke;

        /// <summary>The first column of the layers on the target.</summary>
        private readonly int _boxX;

        /// <summary>The first row of the layers on the target.</summary>
        private readonly int _boxY;

        /// <summary>The fill color with the alpha of the paint.</summary>
        private readonly Rgba32 _fillColor;

        /// <summary>The stroke color with the alpha of the paint.</summary>
        private readonly Rgba32 _strokeColor;

        /// <summary>The trail with the opaque color and no opacity, for the stroke into the layer.</summary>
        private readonly SceneTrail? _strokeItem;

        /// <summary>
        ///     Initializes the layers.
        /// </summary>
        /// <param name="trail">The trail.</param>
        /// <param name="plan">The paints that are layered.</param>
        /// <param name="boxX">The first column of the box.</param>
        /// <param name="boxY">The first row of the box.</param>
        /// <param name="width">The width of the box.</param>
        /// <param name="height">The height of the box.</param>
        private TrailLayers(SceneTrail trail, LayerPlan plan, int boxX, int boxY, int width, int height)
        {
            _boxX = boxX;
            _boxY = boxY;
            _fillColor = plan.FillColor;
            _strokeColor = plan.StrokeColor;
            if (plan.Fill)
            {
                _fill = new Surface(width, height);
            }

            if (plan.Stroke)
            {
                _stroke = new Surface(width, height);
                _strokeItem = trail with
                {
                    Stroke = $"#{_strokeColor.R:x2}{_strokeColor.G:x2}{_strokeColor.B:x2}",
                    StrokeOpacity = 1,
                    Opacity = 1,
                };
            }

            Pixels = (long)width * height * plan.Count;
        }

        /// <summary>Gets the number of pixels of the layers: the width times the height of the box, for each layer.</summary>
        public long Pixels { get; }

        /// <summary>
        ///     The paints of a trail that are layered.
        /// </summary>
        /// <param name="Fill">Whether the fill is layered: a solid fill that is translucent (the alpha is above 0 and below 255).</param>
        /// <param name="FillColor">The fill color with the alpha of the paint.</param>
        /// <param name="Stroke">Whether the stroke is layered: it is drawn, and it is translucent or under a layered fill (see the remarks of <see cref="TrailLayers"/>).</param>
        /// <param name="StrokeColor">The stroke color with the alpha of the paint.</param>
        private readonly record struct LayerPlan(bool Fill, Rgba32 FillColor, bool Stroke, Rgba32 StrokeColor)
        {
            /// <summary>Gets the number of layers.</summary>
            public int Count => (Fill ? 1 : 0) + (Stroke ? 1 : 0);
        }

        /// <summary>
        ///     Decides which paints of a trail are layered; the single source of the layers for their allocation, the box and the budget.
        /// </summary>
        /// <remarks>
        ///     A paint that resolves to no color (fully transparent, none) is not painted and has no layer. A gradient fill is never layered. A stroke that is not drawn (<see cref="TryStrokeWidth"/>:
        ///     a width that is not finite or is beyond the bound) has no layer and does not grow the box.
        /// </remarks>
        /// <param name="trail">The trail.</param>
        /// <param name="scale">The uniform scale from the units of the trail to surface pixels.</param>
        /// <param name="plan">Receives the plan.</param>
        /// <returns><see langword="true"/> when at least one paint is layered.</returns>
        private static bool TryPlan(SceneTrail trail, float scale, out LayerPlan plan)
        {
            var fill = default(Rgba32);
            var stroke = default(Rgba32);
            var fillLayered = trail.FillGradient is null && CssPaint.TryResolvePaint(trail.Fill, trail.FillOpacity * trail.Opacity, out fill) && fill.A < 255;
            var strokeDrawn = TryStrokeWidth(trail.StrokeWidth, scale, out _) && CssPaint.TryResolvePaint(trail.Stroke, trail.StrokeOpacity * trail.Opacity, out stroke);
            var strokeLayered = strokeDrawn && (stroke.A < 255 || fillLayered);
            plan = new LayerPlan(fillLayered, fill, strokeLayered, stroke);
            return fillLayered || strokeLayered;
        }

        /// <summary>
        ///     Creates the layers of a trail.
        /// </summary>
        /// <param name="trail">The trail.</param>
        /// <param name="local">The context of the enclosing group.</param>
        /// <param name="pixelBudget">The most pixels, over all the layers, that may be created.</param>
        /// <param name="cancellationToken">A token that stops the work.</param>
        /// <returns>
        ///     The layers; null when the trail has no translucent solid paint (an opaque trail has no seam), when no part of it is on the target, or when its layers would have more pixels than
        ///     <paramref name="pixelBudget"/> (the trail is then painted chunk by chunk). Only the paints of the plan are charged: an opaque fill beside a translucent stroke has no layer.
        /// </returns>
        public static TrailLayers? TryCreate(SceneTrail trail, Context local, long pixelBudget, CancellationToken cancellationToken)
        {
            if (!TryPlan(trail, local.Scale, out var plan))
            {
                return null;
            }

            // The pixel box of the points: each point grown by its radius, the stroke width of a layered stroke (the stroke is centered on the outline) and the margin
            var grow = plan.Stroke ? (float)trail.StrokeWidth.GetValueOrDefault(1) * local.Scale : 0f;
            float left = float.MaxValue, top = float.MaxValue, right = float.MinValue, bottom = float.MinValue;
            var visited = 0;
            foreach (var run in trail.Runs)
            {
                foreach (var point in run)
                {
                    if (++visited % TrailPollInterval == 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    if (!IsDrawable(point, local.Scale))
                    {
                        continue;
                    }

                    var center = Vector2.Transform(new Vector2((float)point.X, (float)point.Y), local.Transform);
                    var reach = (float)((point.Size / 2 * local.Scale) + grow) + Margin;
                    left = Math.Min(left, center.X - reach);
                    top = Math.Min(top, center.Y - reach);
                    right = Math.Max(right, center.X + reach);
                    bottom = Math.Max(bottom, center.Y + reach);
                }
            }

            left = Math.Max(0, left);
            top = Math.Max(0, top);
            right = Math.Min(local.Target.Width, right);
            bottom = Math.Min(local.Target.Height, bottom);
            if (!(right > left) || !(bottom > top))
            {
                return null;
            }

            var boxX = NumericConversions.ToInt32Saturating(Math.Floor(left));
            var boxY = NumericConversions.ToInt32Saturating(Math.Floor(top));
            var boxWidth = NumericConversions.ToInt32Saturating(Math.Ceiling(right)) - boxX;
            var boxHeight = NumericConversions.ToInt32Saturating(Math.Ceiling(bottom)) - boxY;

            // The layers are charged against the budget before anything is allocated; a trail over it is painted chunk by chunk
            if ((long)boxWidth * boxHeight * plan.Count > pixelBudget)
            {
                return null;
            }

            // The layers are allocated and composited, which is painting work of their own: it is charged against the paint limit before they exist
            ChargePixels(local, (long)boxWidth * boxHeight * plan.Count, trail.Role);
            return new TrailLayers(trail, plan, boxX, boxY, boxWidth, boxHeight);
        }

        /// <summary>
        ///     Paints a chunk of capsules into the layers.
        /// </summary>
        /// <param name="path">The path of the chunk in the units of the trail.</param>
        /// <param name="trail">The trail.</param>
        /// <param name="local">The context of the enclosing group.</param>
        public void PaintChunk(Path path, SceneTrail trail, Context local)
        {
            if (path.Subpaths.Count == 0)
            {
                return;
            }

            var shifted = local.Transform * Matrix3x2.CreateTranslation(-_boxX, -_boxY);
            if (_fill is not null)
            {
                ChargePath(local, path, local.Transform, 0, trail);
                var opaque = new Rgba32(_fillColor.R, _fillColor.G, _fillColor.B, 255);
                PathFiller.Fill(_fill, path.Transform(shifted), opaque, FillRule.NonZero, FlattenTolerance);
            }
            else if (!TryFillWithGradient(path, trail, local, local.Transform)
                && CssPaint.TryResolvePaint(trail.Fill, trail.FillOpacity * trail.Opacity, out var color))
            {
                // An opaque solid fill (no seam, so no layer) or a gradient fill (or a fallback to its solid fill) is not layered: it is painted chunk by chunk
                // straight onto the target, below the composite of the stroke layer
                ChargePath(local, path, local.Transform, 0, trail);
                PathFiller.Fill(local.Target, path.Transform(local.Transform), color, FillRule.NonZero, FlattenTolerance);
            }

            if (_stroke is not null)
            {
                StrokeOnly(path, _strokeItem!, local with { Target = _stroke, Transform = shifted }, shifted);
            }
        }

        /// <summary>
        ///     Composites the layers onto the target: the fill, then the stroke, each at the alpha of its paint.
        /// </summary>
        /// <param name="target">The target surface.</param>
        /// <param name="cancellationToken">A token that stops the work.</param>
        public void Composite(Surface target, CancellationToken cancellationToken)
        {
            // The layers have the width of the box, so one buffer of the coverage of a row serves both
            var coverage = new float[(_fill ?? _stroke)!.Width];
            CompositeLayer(target, _fill, _fillColor, coverage, cancellationToken);
            CompositeLayer(target, _stroke, _strokeColor, coverage, cancellationToken);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _fill?.Dispose();
            _stroke?.Dispose();
        }

        /// <summary>
        ///     Composites one layer: the alpha of each pixel of the layer is the coverage of the paint.
        /// </summary>
        /// <param name="target">The target surface.</param>
        /// <param name="layer">The layer, or null.</param>
        /// <param name="color">The color of the paint, with its alpha.</param>
        /// <param name="coverage">The buffer of the coverage of a row, as wide as the layer; shared by the layers.</param>
        /// <param name="cancellationToken">A token that stops the work.</param>
        private void CompositeLayer(Surface target, Surface? layer, Rgba32 color, float[] coverage, CancellationToken cancellationToken)
        {
            if (layer is null)
            {
                return;
            }

            for (var y = 0; y < layer.Height; y++)
            {
                if (y % 64 == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var row = layer.GetRowSpan(y);
                var any = false;
                for (var x = 0; x < coverage.Length; x++)
                {
                    coverage[x] = row[x].A / 255f;
                    any |= row[x].A != 0;
                }

                if (any)
                {
                    target.CompositeOverSpan(_boxY + y, _boxX, coverage, color);
                }
            }
        }
    }

    /// <summary>
    ///     Estimates the cost of painting the capsule of a trail in units of one line piece of a flattened curve.
    /// </summary>
    /// <remarks>
    ///     A capsule has two round caps; the filler flattens a circle of a radius <c>r</c> surface pixels into a number of pieces that is proportional to
    ///     <c>sqrt(r / tolerance)</c>, so that is the weight of the caps; the two straight edges and the bookkeeping are one more unit.
    /// </remarks>
    /// <param name="radius">The larger radius of the capsule in scene units.</param>
    /// <param name="scale">The uniform scale of the placement.</param>
    /// <returns>The weight; at least one.</returns>
    internal static double TrailCapsuleWeight(double radius, float scale) => 1 + Math.Sqrt(radius * scale / FlattenTolerance);

    /// <summary>
    ///     Determines whether a trail point can be drawn.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <param name="scale">The uniform scale of the placement.</param>
    /// <returns><see langword="true"/> when the coordinates and the size are bounded (see <see cref="IsBoundedAt"/>) and the size is positive.</returns>
    private static bool IsDrawable(SceneTrailPoint point, float scale) => IsBoundedAt(scale, point.X, point.Y, point.Size) && point.Size > 0;

    /// <summary>
    ///     Determines whether the end point of a path command can be drawn.
    /// </summary>
    /// <param name="segment">The command.</param>
    /// <param name="scale">The uniform scale of the placement.</param>
    /// <returns><see langword="true"/> for a close, and for any other command when its end point is bounded (see <see cref="IsBoundedAt"/>).</returns>
    private static bool IsEndPointBounded(ScenePathSegment segment, float scale) => segment.Kind == ScenePathSegmentKind.Close || IsBoundedAt(scale, segment.X, segment.Y);

    /// <summary>
    ///     Writes the capsule between two trail points (the segment of Vega's trail shape).
    /// </summary>
    /// <param name="writer">The path writer.</param>
    /// <param name="x1">The x coordinate of the previous point.</param>
    /// <param name="y1">The y coordinate of the previous point.</param>
    /// <param name="r1">The radius (half the size) at the previous point.</param>
    /// <param name="x2">The x coordinate of the point.</param>
    /// <param name="y2">The y coordinate of the point.</param>
    /// <param name="r2">The radius (half the size) at the point.</param>
    private static void AppendCapsule(ArcPathWriter writer, double x1, double y1, double r1, double x2, double y2, double r2)
    {
        var ux = y1 - y2;
        var uy = x2 - x1;
        if (ux != 0 || uy != 0)
        {
            // The unit normal of the segment
            var length = Math.Sqrt((ux * ux) + (uy * uy));
            ux /= length;
            uy /= length;
            var rx = ux * r1;
            var ry = uy * r1;
            var t = Math.Atan2(uy, ux);
            writer.MoveTo(x1 - rx, y1 - ry);
            writer.LineTo(x2 - (ux * r2), y2 - (uy * r2));
            writer.Arc(x2, y2, r2, t - Math.PI, t, false);
            writer.LineTo(x1 + rx, y1 + ry);
            writer.Arc(x1, y1, r1, t, t + Math.PI, false);
        }
        else
        {
            // Coincident points: a circle
            writer.MoveTo(x2 + r2, y2);
            writer.Arc(x2, y2, r2, 0, 2 * Math.PI, false);
        }

        writer.Close();
    }
}
