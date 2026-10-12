using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Drawing;

/// <summary>
///     A per-pixel antialiased coverage mask, bound to a fixed <c>width</c> x <c>height</c>
///     device pixel extent, representing a PDF content stream's current clipping path
///     (PDF 32000-1 &#xA7;8.5.4).
/// </summary>
/// <remarks>
///     <para>
///     Not a public <see cref="Drawing"/> unit (unlike <see cref="PathFiller"/>,
///     <see cref="Gradient"/>, or <see cref="TilePaint"/>): this is an internal implementation
///     detail of clip-path enforcement, consumed only by <c>DemaConsulting.CanvasNet.Pdf</c> (an
///     <c>InternalsVisibleTo</c> friend assembly) via the internal clip-aware
///     <see cref="PathFiller"/>/<see cref="ScanlineRasterizer"/> <c>Fill</c> overloads - exactly
///     the same "internal helper documented inline alongside <see cref="PathFiller"/>" treatment
///     already given to <see cref="EdgeFlattener"/> and <see cref="ScanlineRasterizer"/>
///     themselves, rather than the "new public unit with its own full companion-artifact set"
///     treatment given to a genuinely new public capability such as <see cref="TilePaint"/>. See
///     <c>path-filler.md</c> for the design-level treatment of clip-path support.
///     </para>
///     <para>
///     <b>Coverage, not a boolean mask.</b> Every pixel has a <c>[0, 1]</c> antialiased
///     coverage fraction - identical in meaning to the per-pixel fill coverage
///     <see cref="ScanlineRasterizer"/> itself computes - rather than a hard inside/outside bit,
///     so a clip path's own edge antialiasing composes smoothly with whatever is painted through
///     it, rather than producing a jagged, aliased clip boundary.
///     </para>
///     <para>
///     <b>Analytic rectangle plus optional dense coverage.</b> Coverage at a pixel is
///     <c>dense(x, y) * columnFactor(x) * rowFactor(y)</c>. The column and row factors come from an
///     analytic device-space rectangle (the exact per-axis fractional overlap of the pixel with the
///     rectangle), and <c>dense</c> is an optional shared coverage buffer. An axis-aligned
///     rectangular clip path (by far the most common PDF clip) has no dense buffer at all, so it
///     costs no allocation and no per-pixel work; a general path keeps a dense buffer over its own
///     bounding box, and a rectangle restriction (from intersecting it with a rectangle) is applied
///     lazily through the factors without copying the buffer. Intersecting two rectangles yields
///     their exact geometric overlap, which at a shared fractional edge pixel differs from the
///     product of the two per-rectangle coverages (by up to 0.25); the geometric overlap is the
///     more accurate result.
///     </para>
///     <para>
///     <b>Immutable and reused, never mutated in place.</b> <see cref="FromPath"/> builds a fresh
///     mask from a path's own geometry; <see cref="Intersect"/> always returns a new instance
///     (possibly sharing the immutable dense buffer), never modifying either operand - this is
///     what lets <c>PdfDocument.GraphicsState.Clone()</c> safely copy a <see cref="ClipMask"/>
///     reference as-is for <c>q</c>/<c>Q</c> save/restore scoping (PDF 32000-1 &#xA7;8.4.2) without
///     any risk of one graphics state's clip later being mutated out from under another.
///     </para>
/// </remarks>
internal sealed class ClipMask
{
    /// <summary>The tolerance, in pixels, used for rectangle detection and full-surface detection.</summary>
    private const float Epsilon = 1e-3f;

    /// <summary>
    ///     The optional dense per-pixel <c>[0, 1]</c> coverage buffer, row-major, sized
    ///     <see cref="_denseWidth"/> * <see cref="_denseHeight"/> (the clip path's own bounding
    ///     box only). <see langword="null"/> for a pure rectangle (or empty) clip.
    /// </summary>
    private readonly float[]? _dense;

    private readonly int _denseOriginX;
    private readonly int _denseOriginY;
    private readonly int _denseWidth;
    private readonly int _denseHeight;

    // The analytic rectangle, in float device pixel units.
    private readonly float _left;
    private readonly float _top;
    private readonly float _right;
    private readonly float _bottom;

    // Whole-pixel bounds outside which coverage is zero.
    private readonly int _originX;
    private readonly int _originY;
    private readonly int _boundsWidth;
    private readonly int _boundsHeight;

    private ClipMask(
        float[]? dense,
        int denseOriginX,
        int denseOriginY,
        int denseWidth,
        int denseHeight,
        float left,
        float top,
        float right,
        float bottom,
        int width,
        int height)
    {
        Width = width;
        Height = height;
        if (!(right > left) || !(bottom > top))
        {
            // Empty: nothing is visible.
            return;
        }

        _dense = dense;
        _denseOriginX = denseOriginX;
        _denseOriginY = denseOriginY;
        _denseWidth = denseWidth;
        _denseHeight = denseHeight;
        _left = left;
        _top = top;
        _right = right;
        _bottom = bottom;
        _originX = (int)MathF.Floor(left);
        _originY = (int)MathF.Floor(top);
        _boundsWidth = (int)MathF.Ceiling(right) - _originX;
        _boundsHeight = (int)MathF.Ceiling(bottom) - _originY;
    }

    /// <summary>The device pixel extent's width, in whole pixel columns, this mask covers.</summary>
    public int Width { get; }

    /// <summary>The device pixel extent's height, in whole pixel rows, this mask covers.</summary>
    public int Height { get; }

    /// <summary>
    ///     Gets the whole-pixel rectangle outside which this mask's coverage is zero (zero-sized
    ///     when the mask covers nothing).
    /// </summary>
    public Rect Bounds => new(_originX, _originY, _boundsWidth, _boundsHeight);

    /// <summary>Gets a value indicating whether this mask covers nothing at all.</summary>
    public bool IsEmpty => _boundsWidth <= 0 || _boundsHeight <= 0;

    /// <summary>
    ///     Gets a value indicating whether this mask is purely an analytic rectangle (no dense
    ///     coverage buffer). An empty mask is also reported as a rectangle.
    /// </summary>
    public bool IsRectangle => _dense is null;

    /// <summary>Gets a value indicating whether this mask has a dense coverage buffer.</summary>
    internal bool HasDense => _dense is not null;

    /// <summary>Gets the analytic rectangle's left edge, in device pixels.</summary>
    internal float RectLeft => _left;

    /// <summary>Gets the analytic rectangle's top edge, in device pixels.</summary>
    internal float RectTop => _top;

    /// <summary>Gets the analytic rectangle's right edge, in device pixels.</summary>
    internal float RectRight => _right;

    /// <summary>Gets the analytic rectangle's bottom edge, in device pixels.</summary>
    internal float RectBottom => _bottom;

    /// <summary>
    ///     Gets a value indicating whether this mask is a rectangle that covers the whole device
    ///     pixel extent (so painting through it is identical to painting with no clip).
    /// </summary>
    internal bool IsFullSurface =>
        _dense is null &&
        !IsEmpty &&
        _left <= Epsilon &&
        _top <= Epsilon &&
        _right >= Width - Epsilon &&
        _bottom >= Height - Epsilon;

    /// <summary>
    ///     Gets the dense buffer instance, for reference-sharing checks in tests (<see langword="null"/>
    ///     for a pure rectangle).
    /// </summary>
    internal float[]? DenseBuffer => _dense;

    /// <summary>
    ///     Builds a new <see cref="ClipMask"/> whose per-pixel coverage is <paramref name="path"/>'s
    ///     own antialiased fill coverage, interpreted under <paramref name="fillRule"/>, across a
    ///     <paramref name="width"/> x <paramref name="height"/> device pixel extent. An
    ///     axis-aligned rectangle path is represented analytically (no dense buffer).
    /// </summary>
    /// <param name="path">The clipping path's geometry. Must not be <see langword="null"/>.</param>
    /// <param name="fillRule">
    ///     The rule used to interpret <paramref name="path"/> for clipping purposes - <c>W</c>
    ///     uses <see cref="FillRule.NonZero"/>, <c>W*</c> uses <see cref="FillRule.EvenOdd"/>
    ///     (PDF 32000-1 &#xA7;8.5.4).
    /// </param>
    /// <param name="width">The device pixel extent's width, in whole pixel columns. Must be greater than zero.</param>
    /// <param name="height">The device pixel extent's height, in whole pixel rows. Must be greater than zero.</param>
    /// <param name="flattenTolerance">
    ///     The maximum allowed perpendicular deviation between each curve in <paramref name="path"/>
    ///     and the polyline used to approximate it. Must be greater than zero. Defaults to
    ///     <c>0.25f</c>, identical to <see cref="PathFiller.Fill(Canvas.Surface, Path, Canvas.Rgba32, FillRule, float)"/>'s
    ///     own default.
    /// </param>
    /// <returns>
    ///     A new <see cref="ClipMask"/> whose coverage is zero everywhere outside
    ///     <paramref name="path"/>'s own filled region (including, trivially, when
    ///     <paramref name="path"/> has an empty bounding box or lies entirely outside the
    ///     <paramref name="width"/> x <paramref name="height"/> extent).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="fillRule"/> is not a defined <see cref="FillRule"/> value,
    ///     when <paramref name="flattenTolerance"/> is not a finite value greater than zero, or
    ///     when <paramref name="width"/> or <paramref name="height"/> is not greater than zero.
    /// </exception>
    public static ClipMask FromPath(Path path, FillRule fillRule, int width, int height, float flattenTolerance = 0.25f)
        => Build(path, fillRule, width, height, flattenTolerance, allowRectangle: true);

    /// <summary>
    ///     Same as <see cref="FromPath"/>, but always builds a dense mask (never the analytic
    ///     rectangle form). Used as the reference implementation by tests.
    /// </summary>
    internal static ClipMask FromPathDense(Path path, FillRule fillRule, int width, int height, float flattenTolerance = 0.25f)
        => Build(path, fillRule, width, height, flattenTolerance, allowRectangle: false);

    /// <summary>
    ///     Same as <see cref="FromPath"/>, but returns <see langword="null"/> (meaning "no clip")
    ///     when the clip is a rectangle covering the whole device pixel extent.
    /// </summary>
    internal static ClipMask? FromPathOrNull(Path path, FillRule fillRule, int width, int height, float flattenTolerance = 0.25f)
    {
        var mask = FromPath(path, fillRule, width, height, flattenTolerance);
        return mask.IsFullSurface ? null : mask;
    }

    /// <summary>
    ///     Intersects two optional clips, treating <see langword="null"/> as "no clip" and
    ///     normalizing a full-surface result to <see langword="null"/>.
    /// </summary>
    internal static ClipMask? Combine(ClipMask? current, ClipMask? next)
    {
        if (next is null)
        {
            return current is { IsFullSurface: true } ? null : current;
        }

        var result = current is null ? next : current.Intersect(next);
        return result.IsFullSurface ? null : result;
    }

    /// <summary>
    ///     Returns a new <see cref="ClipMask"/> that is the geometric intersection of this mask
    ///     and <paramref name="other"/> (PDF 32000-1 &#xA7;8.5.4: "the new clipping path ... shall be the
    ///     intersection of the current clipping path and the newly constructed path"). Rectangle
    ///     with rectangle is a new rectangle; a rectangle with a dense mask shares the dense buffer
    ///     and restricts it lazily; two dense masks are multiplied over their overlap only.
    /// </summary>
    /// <param name="other">
    ///     The other clip mask to intersect with. Must not be <see langword="null"/>, and must
    ///     share this mask's own <see cref="Width"/>/<see cref="Height"/> (every <see cref="ClipMask"/>
    ///     live within a single PDF page render shares the same device pixel extent).
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="other"/>'s <see cref="Width"/>/<see cref="Height"/> differ
    ///     from this mask's own.
    /// </exception>
    public ClipMask Intersect(ClipMask other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other.Width != Width || other.Height != Height)
        {
            throw new ArgumentException(
                "Cannot intersect clip masks covering different device pixel extents.", nameof(other));
        }

        if (IsEmpty || other.IsEmpty)
        {
            return Empty(Width, Height);
        }

        var left = MathF.Max(_left, other._left);
        var top = MathF.Max(_top, other._top);
        var right = MathF.Min(_right, other._right);
        var bottom = MathF.Min(_bottom, other._bottom);
        if (!(right > left) || !(bottom > top))
        {
            return Empty(Width, Height);
        }

        if (_dense is null && other._dense is null)
        {
            return new ClipMask(null, 0, 0, 0, 0, left, top, right, bottom, Width, Height);
        }

        if (_dense is null || other._dense is null)
        {
            // Exactly one dense operand: share its buffer and restrict it by the rectangle.
            var dense = _dense is not null ? this : other;
            var restricted = new ClipMask(
                dense._dense,
                dense._denseOriginX,
                dense._denseOriginY,
                dense._denseWidth,
                dense._denseHeight,
                left,
                top,
                right,
                bottom,
                Width,
                Height);
            if (dense._pieces is null)
            {
                return restricted;
            }

            var restrictedPieces = IntersectPieces(dense._pieces, null, left, top, right, bottom);
            return restrictedPieces is null ? Empty(Width, Height) : restricted.WithPieces(restrictedPieces);
        }

        var product = IntersectDense(other, left, top, right, bottom);
        if (_pieces is null || other._pieces is null)
        {
            return product;
        }

        var both = IntersectPieces(_pieces, other._pieces, left, top, right, bottom);
        if (both is null)
        {
            return Empty(Width, Height);
        }

        return both.Length > MaxPieces * 4 ? product : product.WithPieces(both);
    }

    /// <summary>
    ///     Gets a value indicating whether this mask is a union of disjoint axis-aligned rectangles
    ///     (built from a clip path made only of rectangles, for example a <c>W*</c> band). Such a mask
    ///     is applied to fills geometrically (see <see cref="TryClipPolygonsToRect"/>) so a fill
    ///     edge coincident with a clip edge is covered exactly once; its dense buffer is then
    ///     used only by <see cref="GetCoverage"/>.
    /// </summary>
    internal bool HasPieces => _pieces is not null;

    /// <summary>The maximum number of disjoint rectangles kept for a rectangle-union clip.</summary>
    private const int MaxPieces = 64;

    /// <summary>The maximum number of rectangle subpaths decomposed into pieces.</summary>
    private const int MaxRectSubpaths = 32;

    /// <summary>
    ///     The disjoint rectangles (left, top, right, bottom per entry, flattened) whose union is
    ///     this mask's shape, or <see langword="null"/> when the mask is not a rectangle union.
    /// </summary>
    private float[]? _pieces;

    private ClipMask WithPieces(float[]? pieces)
    {
        _pieces = pieces;
        return this;
    }

    /// <summary>
    ///     Intersects the rectangle set <paramref name="a"/> with <paramref name="b"/> (or, when
    ///     <see langword="null"/>, with the single rectangle given by the bounds). Returns
    ///     <see langword="null"/> when nothing remains; a result longer than the piece cap is
    ///     returned as-is for the caller to reject.
    /// </summary>
    private static float[]? IntersectPieces(
        float[] a, float[]? b, float left, float top, float right, float bottom)
    {
        var result = new List<float>();
        for (var i = 0; i < a.Length; i += 4)
        {
            var bCount = b is null ? 1 : b.Length / 4;
            for (var j = 0; j < bCount; j++)
            {
                var l = MathF.Max(a[i], b is null ? left : MathF.Max(b[j * 4], left));
                var t = MathF.Max(a[i + 1], b is null ? top : MathF.Max(b[(j * 4) + 1], top));
                var r = MathF.Min(a[i + 2], b is null ? right : MathF.Min(b[(j * 4) + 2], right));
                var bt = MathF.Min(a[i + 3], b is null ? bottom : MathF.Min(b[(j * 4) + 3], bottom));
                if (r > l && bt > t)
                {
                    result.Add(l);
                    result.Add(t);
                    result.Add(r);
                    result.Add(bt);
                    if (result.Count > MaxPieces * 4 + 4)
                    {
                        return [.. result];
                    }
                }
            }
        }

        return result.Count == 0 ? null : [.. result];
    }

    /// <summary>
    ///     Decomposes a set of axis-aligned rectangle subpaths into disjoint rectangles covering
    ///     exactly the region the fill rule selects (coordinate compression, then run merging).
    ///     Returns <see langword="null"/> when the result would exceed <see cref="MaxPieces"/>.
    /// </summary>
    private static float[]? DecomposeRects(
        List<(float L, float T, float R, float B, int Sign)> rects, FillRule fillRule)
    {
        var xs = new SortedSet<float>();
        var ys = new SortedSet<float>();
        foreach (var (l, t, r, b, _) in rects)
        {
            xs.Add(l);
            xs.Add(r);
            ys.Add(t);
            ys.Add(b);
        }

        var xv = xs.ToArray();
        var yv = ys.ToArray();
        var rows = new List<(float T, float B, List<(float L, float R)> Runs)>();
        for (var yi = 0; yi + 1 < yv.Length; yi++)
        {
            var cy = (yv[yi] + yv[yi + 1]) * 0.5f;
            var runs = new List<(float L, float R)>();
            for (var xi = 0; xi + 1 < xv.Length; xi++)
            {
                var cx = (xv[xi] + xv[xi + 1]) * 0.5f;
                var winding = 0;
                var count = 0;
                foreach (var (l, t, r, b, sign) in rects)
                {
                    if (cx > l && cx < r && cy > t && cy < b)
                    {
                        winding += sign;
                        count++;
                    }
                }

                var inside = fillRule == FillRule.EvenOdd ? (count & 1) == 1 : winding != 0;
                if (!inside)
                {
                    continue;
                }

                if (runs.Count > 0 && SameCoordinate(runs[^1].R, xv[xi]))
                {
                    runs[^1] = (runs[^1].L, xv[xi + 1]);
                }
                else
                {
                    runs.Add((xv[xi], xv[xi + 1]));
                }
            }

            if (runs.Count > 0)
            {
                rows.Add((yv[yi], yv[yi + 1], runs));
            }
        }

        // Merge vertically adjacent rows with identical runs.
        var pieces = new List<float>();
        var merged = new List<(float T, float B, List<(float L, float R)> Runs)>();
        foreach (var row in rows)
        {
            if (merged.Count > 0 && SameCoordinate(merged[^1].B, row.T) && merged[^1].Runs.SequenceEqual(row.Runs))
            {
                merged[^1] = (merged[^1].T, row.B, merged[^1].Runs);
            }
            else
            {
                merged.Add(row);
            }
        }

        foreach (var (t, b, runs) in merged)
        {
            foreach (var (l, r) in runs)
            {
                pieces.Add(l);
                pieces.Add(t);
                pieces.Add(r);
                pieces.Add(b);
            }
        }

        return pieces.Count == 0 || pieces.Count > MaxPieces * 4 ? null : [.. pieces];
    }

    /// <summary>Collects every polygon as an axis-aligned rectangle with its winding sign, or returns <see langword="false"/>.</summary>
    private static bool TryCollectRects(
        IReadOnlyList<List<Vector2>> polygons,
        int width,
        int height,
        out List<(float L, float T, float R, float B, int Sign)> rects)
    {
        rects = [];
        if (polygons.Count < 2 || polygons.Count > MaxRectSubpaths)
        {
            return false;
        }

        foreach (var poly in polygons)
        {
            var single = new List<List<Vector2>>(1) { poly };
            if (!TryGetAxisAlignedRect(single, out var l, out var t, out var r, out var b))
            {
                return false;
            }

            // Winding sign from the signed area of the first three vertices.
            var cross = ((poly[1].X - poly[0].X) * (poly[2].Y - poly[1].Y)) - ((poly[1].Y - poly[0].Y) * (poly[2].X - poly[1].X));
            l = MathF.Max(l, 0f);
            t = MathF.Max(t, 0f);
            r = MathF.Min(r, width);
            b = MathF.Min(b, height);
            if (r > l && b > t)
            {
                rects.Add((l, t, r, b, cross > 0 ? 1 : -1));
            }
        }

        return rects.Count > 0;
    }

    /// <summary>
    ///     Returns this mask's antialiased coverage, in <c>[0, 1]</c>, at device pixel
    ///     <paramref name="x"/>, <paramref name="y"/>.
    /// </summary>
    /// <param name="x">The zero-based device pixel column.</param>
    /// <param name="y">The zero-based device pixel row.</param>
    /// <returns>
    ///     The coverage at <paramref name="x"/>, <paramref name="y"/>, or <c>0f</c> if the
    ///     coordinate lies outside this mask's own <see cref="Width"/> x <see cref="Height"/>
    ///     extent - matching the "clip restricts to an area, it never grants coverage beyond what
    ///     it was built for" semantics every caller needs, without every caller re-deriving its
    ///     own bounds check.
    /// </returns>
    public float GetCoverage(int x, int y)
    {
        var localX = x - _originX;
        var localY = y - _originY;
        if ((uint)localX >= (uint)_boundsWidth || (uint)localY >= (uint)_boundsHeight)
        {
            return 0f;
        }

        var factor = GetColumnFactor(x) * GetRowFactor(y);
        if (_dense is null)
        {
            return factor;
        }

        return _dense[((y - _denseOriginY) * _denseWidth) + (x - _denseOriginX)] * factor;
    }

    /// <summary>
    ///     Gets the analytic rectangle's fractional coverage of device pixel column <paramref name="x"/>.
    /// </summary>
    internal float GetColumnFactor(int x)
        => Math.Clamp(MathF.Min(x + 1f, _right) - MathF.Max(x, _left), 0f, 1f);

    /// <summary>
    ///     Gets the analytic rectangle's fractional coverage of device pixel row <paramref name="y"/>.
    /// </summary>
    internal float GetRowFactor(int y)
        => Math.Clamp(MathF.Min(y + 1f, _bottom) - MathF.Max(y, _top), 0f, 1f);

    /// <summary>
    ///     Gets the dense buffer's values for <paramref name="count"/> columns starting at
    ///     device column <paramref name="x"/> in device row <paramref name="y"/>, or an empty span
    ///     when this mask has no dense buffer. The range must lie within <see cref="Bounds"/>.
    /// </summary>
    internal ReadOnlySpan<float> GetDenseRow(int y, int x, int count)
    {
        if (_dense is null)
        {
            return default;
        }

        return _dense.AsSpan(((y - _denseOriginY) * _denseWidth) + (x - _denseOriginX), count);
    }

    /// <summary>
    ///     Geometrically clips <paramref name="polygons"/> to this mask's analytic rectangle
    ///     (Sutherland-Hodgman against the four axis-aligned half-planes), so a fill edge that
    ///     coincides with a clip edge is rasterized once (exact area coverage) rather than having
    ///     its edge-pixel coverage multiplied by the clip's own edge-pixel coverage.
    /// </summary>
    /// <param name="polygons">The closed polygons to clip.</param>
    /// <param name="clipped">
    ///     The clipped polygons: the original <paramref name="polygons"/> instance when every
    ///     polygon lies fully inside the rectangle, otherwise a new list.
    /// </param>
    /// <param name="clippedBounds">The bounding box of <paramref name="clipped"/>.</param>
    /// <returns><see langword="true"/> when anything remains visible; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    ///     Clipping a closed polygon chain against a half-plane preserves the winding number of every
    ///     point inside the half-plane, so both fill rules remain correct. Any dense coverage is
    ///     still multiplied separately by the rasterizer.
    /// </remarks>
    internal bool TryClipPolygonsToRect(
        IReadOnlyList<List<Vector2>> polygons, out IReadOnlyList<List<Vector2>> clipped, out Rect clippedBounds)
    {
        if (IsEmpty)
        {
            clipped = polygons;
            clippedBounds = default;
            return false;
        }

        if (_pieces is null)
        {
            return ClipPolygonsToRect(polygons, _left, _top, _right, _bottom, out clipped, out clippedBounds);
        }

        // Rectangle-union clip: clip to each disjoint piece and concatenate. Pieces are disjoint,
        // so each point is covered by at most one piece's output and the rasterizer's signed-area
        // accumulation sums coverage exactly across shared piece edges.
        var all = new List<List<Vector2>>();
        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;
        for (var i = 0; i < _pieces.Length; i += 4)
        {
            if (ClipPolygonsToRect(polygons, _pieces[i], _pieces[i + 1], _pieces[i + 2], _pieces[i + 3], out var part, out var bounds))
            {
                all.AddRange(part);
                minX = Math.Min(minX, bounds.Left);
                minY = Math.Min(minY, bounds.Top);
                maxX = Math.Max(maxX, bounds.Right);
                maxY = Math.Max(maxY, bounds.Bottom);
            }
        }

        clipped = all;
        if (all.Count == 0)
        {
            clippedBounds = default;
            return false;
        }

        clippedBounds = new Rect(minX, minY, maxX - minX, maxY - minY);
        return true;
    }

    private static bool ClipPolygonsToRect(
        IReadOnlyList<List<Vector2>> polygons,
        float rectLeft,
        float rectTop,
        float rectRight,
        float rectBottom,
        out IReadOnlyList<List<Vector2>> clipped,
        out Rect clippedBounds)
    {
        clipped = polygons;
        clippedBounds = default;
        if (!(rectRight > rectLeft) || !(rectBottom > rectTop))
        {
            return false;
        }

        List<List<Vector2>>? result = null;
        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;

        for (var p = 0; p < polygons.Count; p++)
        {
            var polygon = polygons[p];
            if (polygon.Count < 3)
            {
                if (result is null)
                {
                    result = CopyPrefix(polygons, p);
                }

                continue;
            }

            float pMinX = float.PositiveInfinity, pMinY = float.PositiveInfinity;
            float pMaxX = float.NegativeInfinity, pMaxY = float.NegativeInfinity;
            foreach (var v in polygon)
            {
                pMinX = Math.Min(pMinX, v.X);
                pMaxX = Math.Max(pMaxX, v.X);
                pMinY = Math.Min(pMinY, v.Y);
                pMaxY = Math.Max(pMaxY, v.Y);
            }

            List<Vector2>? output;
            if (pMinX >= rectLeft && pMaxX <= rectRight && pMinY >= rectTop && pMaxY <= rectBottom)
            {
                output = polygon;
            }
            else if (pMaxX <= rectLeft || pMinX >= rectRight || pMaxY <= rectTop || pMinY >= rectBottom)
            {
                output = null;
            }
            else
            {
                output = ClipPolygon(polygon, rectLeft, rectTop, rectRight, rectBottom);
                if (output is not null)
                {
                    pMinX = float.PositiveInfinity;
                    pMinY = float.PositiveInfinity;
                    pMaxX = float.NegativeInfinity;
                    pMaxY = float.NegativeInfinity;
                    foreach (var v in output)
                    {
                        pMinX = Math.Min(pMinX, v.X);
                        pMaxX = Math.Max(pMaxX, v.X);
                        pMinY = Math.Min(pMinY, v.Y);
                        pMaxY = Math.Max(pMaxY, v.Y);
                    }

                    if (!(pMaxX > pMinX) || !(pMaxY > pMinY))
                    {
                        output = null;
                    }
                }
            }

            if (output is null || !ReferenceEquals(output, polygon))
            {
                result ??= CopyPrefix(polygons, p);
            }

            if (output is null)
            {
                continue;
            }

            result?.Add(output);
            minX = Math.Min(minX, pMinX);
            minY = Math.Min(minY, pMinY);
            maxX = Math.Max(maxX, pMaxX);
            maxY = Math.Max(maxY, pMaxY);
        }

        if (float.IsPositiveInfinity(minX))
        {
            return false;
        }

        if (result is not null)
        {
            clipped = result;
        }

        clippedBounds = new Rect(minX, minY, maxX - minX, maxY - minY);
        return true;
    }

    /// <summary>Copies the first <paramref name="count"/> polygons of <paramref name="polygons"/> into a new list.</summary>
    private static List<List<Vector2>> CopyPrefix(IReadOnlyList<List<Vector2>> polygons, int count)
    {
        var list = new List<List<Vector2>>(polygons.Count);
        for (var i = 0; i < count; i++)
        {
            list.Add(polygons[i]);
        }

        return list;
    }

    /// <summary>Sutherland-Hodgman clip of one closed polygon against an axis-aligned rectangle.</summary>
    private static List<Vector2>? ClipPolygon(List<Vector2> polygon, float left, float top, float right, float bottom)
    {
        var current = new List<Vector2>(polygon);
        var next = new List<Vector2>(polygon.Count + 4);
        for (var side = 0; side < 4 && current.Count >= 3; side++)
        {
            next.Clear();
            var prev = current[^1];
            var prevIn = Inside(prev, side, left, top, right, bottom);
            foreach (var cur in current)
            {
                var curIn = Inside(cur, side, left, top, right, bottom);
                if (curIn != prevIn)
                {
                    next.Add(Intersection(prev, cur, side, left, top, right, bottom));
                }

                if (curIn)
                {
                    next.Add(cur);
                }

                prev = cur;
                prevIn = curIn;
            }

            (current, next) = (next, current);
        }

        if (current.Count < 3)
        {
            return null;
        }

        // The rasterizer's edge table requires explicitly closed polygons (last vertex == first).
        if (current[0] != current[^1])
        {
            current.Add(current[0]);
        }

        return current;
    }

    private static bool Inside(Vector2 v, int side, float left, float top, float right, float bottom)
        => side switch
        {
            0 => v.X >= left,
            1 => v.X <= right,
            2 => v.Y >= top,
            _ => v.Y <= bottom,
        };

    private static Vector2 Intersection(Vector2 a, Vector2 b, int side, float left, float top, float right, float bottom)
    {
        double ax = a.X, ay = a.Y, bx = b.X, by = b.Y;
        if (side < 2)
        {
            var x = side == 0 ? left : right;
            var t = (x - ax) / (bx - ax);
            return new Vector2(x, (float)(ay + (t * (by - ay))));
        }

        var y = side == 2 ? top : bottom;
        var u = (y - ay) / (by - ay);
        return new Vector2((float)(ax + (u * (bx - ax))), y);
    }

    private static ClipMask Empty(int width, int height)
        => new(null, 0, 0, 0, 0, 0, 0, 0, 0, width, height);

    private static ClipMask Build(
        Path path, FillRule fillRule, int width, int height, float flattenTolerance, bool allowRectangle)
    {
        ArgumentNullException.ThrowIfNull(path);
        PathFiller.ValidateFillArgs(fillRule, flattenTolerance);
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be greater than zero.");
        }

        var polygons = EdgeFlattener.Flatten(path, flattenTolerance);
        if (allowRectangle && TryGetAxisAlignedRect(polygons, out var l, out var t, out var r, out var b))
        {
            // Fill rule is irrelevant for a single simple rectangle.
            return new ClipMask(
                null, 0, 0, 0, 0, MathF.Max(l, 0f), MathF.Max(t, 0f), MathF.Min(r, width), MathF.Min(b, height), width, height);
        }

        var pathBounds = PathFiller.GetPolygonBounds(polygons);
        var extentBounds = new Rect(0, 0, width, height);
        var clipBounds = Rect.Intersect(pathBounds, extentBounds);
        if (clipBounds.IsEmpty)
        {
            return Empty(width, height);
        }

        // Round the (already extent-clipped) float clip bounds outward to the smallest whole
        // pixel rectangle that contains them, and allocate the coverage buffer sized to only that
        // rectangle - never the full width x height device pixel extent.
        var originX = (int)MathF.Floor(clipBounds.Left);
        var originY = (int)MathF.Floor(clipBounds.Top);
        var boundsWidth = (int)MathF.Ceiling(clipBounds.Right) - originX;
        var boundsHeight = (int)MathF.Ceiling(clipBounds.Bottom) - originY;

        var coverage = new float[boundsWidth * boundsHeight];
        ScanlineRasterizer.AccumulateCoverageMask(polygons, fillRule, clipBounds, coverage, boundsWidth, originX, originY);

        float[]? pieces = null;
        if (allowRectangle && TryCollectRects(polygons, width, height, out var rects))
        {
            pieces = DecomposeRects(rects, fillRule);
        }

        return new ClipMask(
            coverage, originX, originY, boundsWidth, boundsHeight, originX, originY, originX + boundsWidth, originY + boundsHeight, width, height)
            .WithPieces(pieces);
    }

    /// <summary>
    ///     Detects a single closed polygon that is an axis-aligned rectangle (four vertices with
    ///     alternating horizontal and vertical edges, either winding and either start corner).
    /// </summary>
    private static bool TryGetAxisAlignedRect(
        IReadOnlyList<List<Vector2>> polygons, out float left, out float top, out float right, out float bottom)
    {
        left = top = right = bottom = 0f;
        if (polygons.Count != 1)
        {
            return false;
        }

        var poly = polygons[0];
        var n = poly.Count;
        if (n == 5 && Near(poly[0], poly[4]))
        {
            n = 4;
        }

        if (n != 4)
        {
            return false;
        }

        var firstHorizontal = IsHorizontal(poly[0], poly[1]);
        if (firstHorizontal)
        {
            if (!IsVertical(poly[1], poly[2]) || !IsHorizontal(poly[2], poly[3]) || !IsVertical(poly[3], poly[0]))
            {
                return false;
            }
        }
        else if (IsVertical(poly[0], poly[1]))
        {
            if (!IsHorizontal(poly[1], poly[2]) || !IsVertical(poly[2], poly[3]) || !IsHorizontal(poly[3], poly[0]))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        left = MathF.Min(MathF.Min(poly[0].X, poly[1].X), MathF.Min(poly[2].X, poly[3].X));
        right = MathF.Max(MathF.Max(poly[0].X, poly[1].X), MathF.Max(poly[2].X, poly[3].X));
        top = MathF.Min(MathF.Min(poly[0].Y, poly[1].Y), MathF.Min(poly[2].Y, poly[3].Y));
        bottom = MathF.Max(MathF.Max(poly[0].Y, poly[1].Y), MathF.Max(poly[2].Y, poly[3].Y));
        return true;
    }

    private static bool SameCoordinate(float a, float b) => MathF.Abs(a - b) <= 1e-6f;

    private static bool Near(Vector2 a, Vector2 b)
        => MathF.Abs(a.X - b.X) <= Epsilon && MathF.Abs(a.Y - b.Y) <= Epsilon;

    private static bool IsHorizontal(Vector2 a, Vector2 b)
        => MathF.Abs(a.Y - b.Y) <= Epsilon;

    private static bool IsVertical(Vector2 a, Vector2 b)
        => MathF.Abs(a.X - b.X) <= Epsilon;

    /// <summary>
    ///     Multiplies two dense masks over the overlap of their bounds only, folding both
    ///     operands' rectangle restrictions in. The result is a pure dense mask.
    /// </summary>
    private ClipMask IntersectDense(ClipMask other, float left, float top, float right, float bottom)
    {
        var minX = (int)MathF.Floor(left);
        var minY = (int)MathF.Floor(top);
        var boundsWidth = (int)MathF.Ceiling(right) - minX;
        var boundsHeight = (int)MathF.Ceiling(bottom) - minY;
        var result = new float[boundsWidth * boundsHeight];
        var lastX = minX + boundsWidth - 1;
        for (var y = 0; y < boundsHeight; y++)
        {
            var deviceY = minY + y;
            var rowFactor = GetRowFactor(deviceY) * other.GetRowFactor(deviceY);
            var a = GetDenseRow(deviceY, minX, boundsWidth);
            var b = other.GetDenseRow(deviceY, minX, boundsWidth);
            var dest = result.AsSpan(y * boundsWidth, boundsWidth);
            for (var x = 0; x < dest.Length; x++)
            {
                dest[x] = a[x] * b[x] * rowFactor;
            }

            // Rectangle column factors differ from 1 only on the outermost columns of the bounds.
            dest[0] *= GetColumnFactor(minX) * other.GetColumnFactor(minX);
            if (lastX != minX)
            {
                dest[^1] *= GetColumnFactor(lastX) * other.GetColumnFactor(lastX);
            }
        }

        return new ClipMask(
            result, minX, minY, boundsWidth, boundsHeight, minX, minY, minX + boundsWidth, minY + boundsHeight, Width, Height);
    }
}
