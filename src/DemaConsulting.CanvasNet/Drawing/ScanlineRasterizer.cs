using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Geometry;

namespace DemaConsulting.CanvasNet.Drawing;

/// <summary>
///     Rasterizes a set of already-flattened, closed polygons onto a <see cref="Surface"/> using
///     an antialiased, analytic signed-area/coverage-accumulation scanline algorithm - the
///     technique underlying FreeType's "smooth" rasterizer, AGG's <c>scanline_u8</c>, and
///     <c>stb_truetype</c>'s rasterizer - rather than supersampling (multiple samples per pixel).
/// </summary>
/// <remarks>
///     <para>
///     <b>Coordinate convention</b>: pixel <c>(x, y)</c> is treated as covering path-space area
///     <c>[x, x+1) x [y, y+1)</c>, with <c>y</c> increasing downward, consistent with
///     <see cref="Rect"/>'s "top-left corner" terminology and <see cref="Surface"/>'s row-major,
///     row-0-at-top storage. This is the first unit in CanvasNet that turns path coordinates into
///     actual rendered pixel output, so this convention is documented prominently here (and in
///     the <c>path-filler.md</c> design document) rather than left implicit.
///     </para>
///     <para>
///     <b>Algorithm</b>: every polygon edge is bucketed by its topmost scanline row into an edge
///     table, then rows are swept top-to-bottom from <c>clipBounds.Top</c> to
///     <c>clipBounds.Bottom</c>, maintaining an active-edge list of edges whose vertical extent
///     overlaps the current row (added from the edge table when reached, removed once their
///     vertical extent is exhausted) - bounding total work to <c>O(edges + total edge-row
///     crossings)</c> rather than <c>O(edges x rows)</c>.
///     </para>
///     <para>
///     <b>Winding resolution is per-interval, not per-pixel raw-scalar-sum.</b> Within a row, the
///     active edges are first restricted to the portion of their vertical extent overlapping that
///     row (<see cref="RowEdge"/>), then the row is split into sub-intervals at every one of those
///     restricted edges' own start/end y (<see cref="CollectYBoundaries"/>) - so within any single
///     resulting sub-interval, the exact same set of edges spans the whole sub-interval; none
///     starts or stops partway through it. Within each sub-interval, the spanning edges are
///     ordered left-to-right by x (<see cref="CollectSpanningRowEdges"/>), and a running signed
///     winding count is accumulated exactly as a classic polygon scanline fill would: after
///     crossing the <c>i</c>-th edge, the strip between it and the next edge is "inside" if
///     <see cref="FillRule"/> resolves the accumulated winding count to filled (nonzero winding,
///     or odd winding for even-odd - see <see cref="IsInside"/>), and only then is the exact
///     trapezoidal area of that strip (clipped to the visible column range) added to the row's
///     resolved coverage (<see cref="AddIntervalArea"/>).
///     </para>
///     <para>
///     This is deliberately different from summing every edge's raw signed coverage contribution
///     into a single scalar per pixel first, and only afterward folding that aggregate scalar
///     through the fill rule: that naive approach double-counts antialiased overlap between
///     multiple edges that both pass through the same pixel cell in the same row. For example,
///     two exactly coincident polygons each contribute their own fractional edge coverage to the
///     same cell; summing before resolving winding yields twice the correct area under NonZero
///     (instead of clamping or, correctly, just the single shape's own area), and a spurious
///     nonzero result under EvenOdd (instead of the correct fully-transparent cancellation).
///     Resolving winding per interval - asking only "is this specific x-range inside the fill?"
///     via an integer winding count, then adding that interval's exact area once - handles
///     duplicate/overlapping edges within the same cell with the same exactness as a single edge,
///     because the winding decision and the area contribution are never conflated into one
///     fractional value the way the raw-sum approach does.
///     </para>
///     <para>
///     Edges lying entirely to the left of the clipped column range, or spanning across it, are
///     analytically split at the clip boundary rather than walked column-by-column outside the
///     visible range, so an edge (or a whole polygon) that extends far outside the surface's
///     bounds costs work proportional only to the visible row/column range, never to the edge's
///     own (potentially unbounded) extent.
///     </para>
/// </remarks>
internal static class ScanlineRasterizer
{
    /// <summary>
    ///     The maximum horizontal or vertical displacement, in path-space units, still treated as
    ///     exactly zero when classifying an edge as horizontal (zero vertical extent, contributing
    ///     no coverage), a boundary-line segment as vertical (a single-column contribution), or
    ///     two row sub-interval y-boundaries as coincident (merged into a single boundary rather
    ///     than an ultra-thin, floating-point-noise sub-interval).
    /// </summary>
    /// <remarks>
    ///     A genuinely horizontal, vertical, or coincident boundary always lands well within this
    ///     threshold (its displacement is exactly zero); the threshold exists so a value that is
    ///     zero only up to ordinary floating-point rounding is treated identically, rather than
    ///     falling through to the general formula and dividing by (or sub-dividing at) a
    ///     near-zero denominator/interval. This is far smaller than any meaningful sub-pixel
    ///     distance, so it never affects the antialiasing accuracy of genuinely distinct geometry.
    /// </remarks>
    private const float NearZeroDisplacement = 1e-6f;

    /// <summary>
    ///     Rasterizes <paramref name="polygons"/> onto <paramref name="surface"/>, compositing
    ///     <paramref name="color"/> at each pixel scaled by its analytically computed fill
    ///     coverage, restricted to <paramref name="clipBounds"/>.
    /// </summary>
    /// <param name="surface">The surface to composite into. Must not be null.</param>
    /// <param name="polygons">
    ///     The closed polygons to fill (typically produced by <see cref="EdgeFlattener.Flatten"/>).
    ///     Each polygon is an ordered list of vertices whose first and last points coincide (a
    ///     closed loop); a polygon with fewer than three vertices contributes zero coverage.
    /// </param>
    /// <param name="color">The solid color to paint, scaled per pixel by fill coverage.</param>
    /// <param name="fillRule">The rule used to resolve overlapping/self-intersecting geometry.</param>
    /// <param name="clipBounds">
    ///     The region to rasterize, in the same path-space coordinates as <paramref name="polygons"/>.
    ///     Must already be clipped to the surface's own pixel extent by the caller
    ///     (<see cref="PathFiller.Fill"/>); this method rounds it outward to whole pixel rows and
    ///     columns.
    /// </param>
    internal static void Fill(Surface surface, IReadOnlyList<List<Vector2>> polygons, Rgba32 color, FillRule fillRule, Rect clipBounds)
    {
        // Round the (already surface-clipped) float clip bounds outward to whole pixel rows and
        // columns - the rasterizer always operates on whole-pixel scanlines and columns.
        var clipMinX = (int)MathF.Floor(clipBounds.Left);
        var clipMaxX = (int)MathF.Ceiling(clipBounds.Right);
        var clipMinY = (int)MathF.Floor(clipBounds.Top);
        var clipMaxY = (int)MathF.Ceiling(clipBounds.Bottom);
        var width = clipMaxX - clipMinX;
        if (width <= 0 || clipMaxY <= clipMinY)
        {
            return;
        }

        var edges = BuildSortedEdgeTable(polygons);
        if (edges.Count == 0)
        {
            return;
        }

        // Dense per-row scratch buffers. "rowCoverage" accumulates the row's final, already
        // fill-rule-resolved coverage per column (see the type-level remarks); "tempCover"/
        // "tempArea" are reused, per sub-interval, purely as scratch space inside
        // AddIntervalArea's two-boundary trapezoid-area computation - "tempCover" is sized
        // width + 1 for the same "harmless overflow slot" reason the per-edge accumulation below
        // needs.
        var rowCoverage = new float[width];
        var tempCover = new float[width + 1];
        var tempArea = new float[width];

        var activeEdges = new List<Edge>();
        var rowEdges = new List<RowEdge>();
        var boundaries = new List<float>();
        var spanning = new List<SpanningEdge>();
        var nextEdgeIndex = 0;

        for (var y = clipMinY; y < clipMaxY; y++)
        {
            var rowTop = (float)y;
            var rowBottom = rowTop + 1f;

            // Remove edges whose vertical extent is fully exhausted, then add edges from the
            // edge table whose top row has now been reached - each edge is added and removed
            // exactly once across the whole sweep, bounding this bookkeeping to O(edges) overall.
            activeEdges.RemoveAll(edge => edge.BottomY <= rowTop);
            while (nextEdgeIndex < edges.Count && edges[nextEdgeIndex].TopY < rowBottom)
            {
                activeEdges.Add(edges[nextEdgeIndex]);
                nextEdgeIndex++;
            }

            if (activeEdges.Count == 0)
            {
                continue;
            }

            BuildRowEdges(activeEdges, rowTop, rowBottom, rowEdges);
            if (rowEdges.Count == 0)
            {
                continue;
            }

            Array.Clear(rowCoverage);
            CollectYBoundaries(rowEdges, rowTop, rowBottom, boundaries);

            for (var k = 0; k < boundaries.Count - 1; k++)
            {
                var ya = boundaries[k];
                var yb = boundaries[k + 1];
                if (yb - ya <= NearZeroDisplacement)
                {
                    continue;
                }

                CollectSpanningRowEdges(rowEdges, ya, yb, spanning);
                ResolveIntervalCoverage(spanning, ya, yb, fillRule, clipMinX, clipMaxX, tempCover, tempArea, rowCoverage);
            }

            for (var i = 0; i < width; i++)
            {
                rowCoverage[i] = Math.Clamp(rowCoverage[i], 0f, 1f);
            }

            surface.CompositeOverSpan(y, clipMinX, rowCoverage, color);
        }
    }

    /// <summary>
    ///     Walks <paramref name="spanning"/> (already sorted ascending by x by
    ///     <see cref="CollectSpanningRowEdges"/>) left to right, accumulating a running signed
    ///     winding count and adding the exact trapezoidal area of every "inside" strip (per
    ///     <paramref name="fillRule"/>) between consecutive edges to <paramref name="rowCoverage"/>.
    /// </summary>
    private static void ResolveIntervalCoverage(
        List<SpanningEdge> spanning,
        float ya,
        float yb,
        FillRule fillRule,
        int clipMinX,
        int clipMaxX,
        float[] tempCover,
        float[] tempArea,
        float[] rowCoverage)
    {
        var winding = 0;
        for (var i = 0; i < spanning.Count - 1; i++)
        {
            winding += spanning[i].Direction;
            if (!IsInside(winding, fillRule))
            {
                continue;
            }

            AddIntervalArea(
                spanning[i].XAtYa, spanning[i].XAtYb,
                spanning[i + 1].XAtYa, spanning[i + 1].XAtYb,
                ya, yb, clipMinX, clipMaxX, tempCover, tempArea, rowCoverage);
        }
    }

    /// <summary>
    ///     Determines whether <paramref name="fillRule"/> treats an accumulated signed
    ///     <paramref name="winding"/> count as "inside" (filled).
    /// </summary>
    /// <remarks>
    ///     EvenOdd uses a bitwise AND with 1 (rather than a modulo) so the correct odd/even parity
    ///     is obtained directly from the two's-complement bit pattern regardless of the sign of
    ///     <paramref name="winding"/>, without the negative-operand caveats of C#'s <c>%</c>
    ///     operator (which can return a negative result for a negative left-hand operand).
    /// </remarks>
    private static bool IsInside(int winding, FillRule fillRule) =>
        fillRule == FillRule.NonZero ? winding != 0 : (winding & 1) != 0;

    /// <summary>
    ///     Adds the exact area of the strip between two skew boundary lines - a left boundary
    ///     from <c>(xL0, ya)</c> to <c>(xL1, yb)</c> and a right boundary from <c>(xR0, ya)</c> to
    ///     <c>(xR1, yb)</c> - to <paramref name="rowCoverage"/>, per visible column.
    /// </summary>
    /// <remarks>
    ///     Computed as "area to the right of the left boundary" minus "area to the right of the
    ///     right boundary": both terms are exactly the per-edge cover/area/prefix-sum computation
    ///     a single-edge design would use, applied here to a temporary, per-interval buffer pair
    ///     (cleared and reused for every interval) with direction <c>+1</c> for the left boundary
    ///     and <c>-1</c> for the right boundary, regardless of either original edge's own winding
    ///     direction - this is deliberately a request for a raw geometric area between two lines,
    ///     not a further winding accumulation (that already happened in
    ///     <see cref="ResolveIntervalCoverage"/> to decide whether to call this method at all).
    /// </remarks>
    private static void AddIntervalArea(
        float xL0, float xL1, float xR0, float xR1,
        float ya, float yb,
        int clipMinX, int clipMaxX,
        float[] tempCover, float[] tempArea, float[] rowCoverage)
    {
        Array.Clear(tempCover);
        Array.Clear(tempArea);

        AccumulateBoundaryEdge(xL0, ya, xL1, yb, 1, clipMinX, clipMaxX, tempCover, tempArea);
        AccumulateBoundaryEdge(xR0, ya, xR1, yb, -1, clipMinX, clipMaxX, tempCover, tempArea);

        var acc = 0f;
        for (var i = 0; i < rowCoverage.Length; i++)
        {
            acc += tempCover[i];
            rowCoverage[i] += acc + tempArea[i];
        }
    }

    /// <summary>
    ///     Adds one boundary line's contribution - for the portion of the line from
    ///     <c>(xa, ya)</c> to <c>(xb, yb)</c> - to <paramref name="cover"/> and
    ///     <paramref name="area"/>, exactly as a single polygon edge's row contribution would be
    ///     computed (see <see cref="AccumulateSingleColumn"/>/<see cref="AccumulateSlantedSpan"/>).
    /// </summary>
    private static void AccumulateBoundaryEdge(
        float xa, float ya, float xb, float yb, int direction, int clipMinX, int clipMaxX, float[] cover, float[] area)
    {
        if (MathF.Abs(xb - xa) <= NearZeroDisplacement)
        {
            AccumulateSingleColumn(xa, yb - ya, direction, clipMinX, clipMaxX, cover, area);
            return;
        }

        AccumulateSlantedSpan(xa, ya, xb, yb, direction, clipMinX, clipMaxX, cover, area);
    }

    /// <summary>
    ///     Accumulates the contribution of a boundary-line segment that lies within a single
    ///     pixel column (a vertical, or near-vertical, segment at constant x).
    /// </summary>
    private static void AccumulateSingleColumn(
        float x, float deltaY, int direction, int clipMinX, int clipMaxX, float[] cover, float[] area)
    {
        var column = (int)MathF.Floor(x);
        if (column < clipMinX)
        {
            // Entirely left of the visible range: bank the full contribution at the first
            // visible column so every visible pixel's prefix sum includes it.
            cover[0] += direction * deltaY;
            return;
        }

        if (column >= clipMaxX)
        {
            // Entirely right of the visible range: a ray cast further right than any visible
            // pixel never reaches this boundary, so it contributes nothing to any visible column.
            return;
        }

        var fraction = x - column;
        area[column - clipMinX] += direction * deltaY * (1f - fraction);
        var bankIndex = Math.Clamp(column + 1 - clipMinX, 0, clipMaxX - clipMinX);
        cover[bankIndex] += direction * deltaY;
    }

    /// <summary>
    ///     Accumulates the contribution of a boundary-line segment that spans a horizontal range
    ///     of x (a slanted line), splitting it at the visible clip boundaries first, then walking
    ///     each pixel column the visible portion crosses.
    /// </summary>
    private static void AccumulateSlantedSpan(
        float xa, float sy0, float xb, float sy1, int direction, int clipMinX, int clipMaxX, float[] cover, float[] area)
    {
        var xLeft = Math.Min(xa, xb);
        var xRight = Math.Max(xa, xb);

        // Left overflow: the portion of the segment with x < clipMinX contributes its full
        // (unsplit) vertical extent banked at the first visible column, exactly as a fully
        // left-of-range boundary would - see AccumulateSingleColumn's analogous case.
        if (xLeft < clipMinX)
        {
            var xClip = Math.Min(xRight, clipMinX);
            var yAtLeft = InterpolateY(xa, sy0, xb, sy1, xLeft);
            var yAtClip = InterpolateY(xa, sy0, xb, sy1, xClip);
            var overflowDeltaY = MathF.Abs(yAtClip - yAtLeft);
            if (overflowDeltaY > 0)
            {
                cover[0] += direction * overflowDeltaY;
            }

            xLeft = xClip;
        }

        // Right overflow: the portion of the segment with x >= clipMaxX contributes nothing to
        // any visible column (a ray cast further right never reaches it) - simply drop it.
        if (xRight > clipMaxX)
        {
            xRight = clipMaxX;
        }

        if (xLeft >= xRight)
        {
            return;
        }

        // Walk each pixel column the remaining, now fully visible-range-clipped span crosses.
        // This loop is bounded by the visible column count (clipMaxX - clipMinX), never by the
        // boundary's own potentially unbounded extent, because xLeft/xRight were already clipped
        // above.
        var column = (int)MathF.Floor(xLeft);
        var currentX = xLeft;
        while (currentX < xRight)
        {
            var columnRightEdge = Math.Min(column + 1, xRight);
            var yAtCurrentX = InterpolateY(xa, sy0, xb, sy1, currentX);
            var yAtColumnRightEdge = InterpolateY(xa, sy0, xb, sy1, columnRightEdge);
            var deltaY = MathF.Abs(yAtColumnRightEdge - yAtCurrentX);
            var averageFraction = (currentX - column + (columnRightEdge - column)) / 2f;

            area[column - clipMinX] += direction * deltaY * (1f - averageFraction);
            var bankIndex = Math.Clamp(column + 1 - clipMinX, 0, clipMaxX - clipMinX);
            cover[bankIndex] += direction * deltaY;

            currentX = columnRightEdge;
            column++;
        }
    }

    /// <summary>
    ///     Linearly interpolates the y-coordinate at a given x along the line through
    ///     <c>(xa, ya)</c> and <c>(xb, yb)</c>, where <c>xa != xb</c>.
    /// </summary>
    private static float InterpolateY(float xa, float ya, float xb, float yb, float x) =>
        ya + (x - xa) / (xb - xa) * (yb - ya);

    /// <summary>
    ///     Builds the per-row edge list: every <paramref name="activeEdges"/> entry, restricted to
    ///     the portion of its vertical extent overlapping <c>[rowTop, rowBottom)</c>, with its
    ///     x-coordinate at both that restricted range's start and end y precomputed. Edges whose
    ///     vertical extent does not actually overlap this row at all (possible at the boundary
    ///     rows of an edge's extent, due to floating-point comparisons in the active-list sweep)
    ///     are omitted.
    /// </summary>
    private static void BuildRowEdges(List<Edge> activeEdges, float rowTop, float rowBottom, List<RowEdge> rowEdges)
    {
        rowEdges.Clear();

        foreach (var edge in activeEdges)
        {
            var sy0 = Math.Max(edge.TopY, rowTop);
            var sy1 = Math.Min(edge.BottomY, rowBottom);
            if (sy0 >= sy1)
            {
                continue;
            }

            var xAtSy0 = edge.TopX + edge.Slope * (sy0 - edge.TopY);
            var xAtSy1 = edge.TopX + edge.Slope * (sy1 - edge.TopY);
            rowEdges.Add(new RowEdge(sy0, sy1, xAtSy0, xAtSy1, edge.Direction));
        }
    }

    /// <summary>
    ///     Collects the sorted, de-duplicated set of y-values at which any <paramref name="rowEdges"/>
    ///     entry starts or ends within <c>[rowTop, rowBottom]</c>, always including
    ///     <paramref name="rowTop"/> and <paramref name="rowBottom"/> themselves, into
    ///     <paramref name="boundaries"/>.
    /// </summary>
    /// <remarks>
    ///     Splitting the row at every edge's own entry/exit y guarantees that, within any single
    ///     resulting sub-interval, the exact same set of edges spans the whole sub-interval - none
    ///     starts or stops partway through it - which is what lets
    ///     <see cref="CollectSpanningRowEdges"/> and <see cref="ResolveIntervalCoverage"/> treat
    ///     each sub-interval's active edge set, and their relative x-order, as fixed throughout.
    /// </remarks>
    private static void CollectYBoundaries(List<RowEdge> rowEdges, float rowTop, float rowBottom, List<float> boundaries)
    {
        boundaries.Clear();
        boundaries.Add(rowTop);
        boundaries.Add(rowBottom);

        foreach (var edge in rowEdges)
        {
            boundaries.Add(edge.Y0);
            boundaries.Add(edge.Y1);
        }

        boundaries.Sort();

        // De-duplicate near-equal boundary values in place (already sorted ascending), so two
        // edges sharing a common endpoint y (the overwhelmingly common case - most edges meet at
        // shared polygon vertices) do not create a spurious ultra-thin sub-interval between them.
        var writeIndex = 1;
        for (var readIndex = 1; readIndex < boundaries.Count; readIndex++)
        {
            if (boundaries[readIndex] - boundaries[writeIndex - 1] > NearZeroDisplacement)
            {
                boundaries[writeIndex] = boundaries[readIndex];
                writeIndex++;
            }
        }

        boundaries.RemoveRange(writeIndex, boundaries.Count - writeIndex);
    }

    /// <summary>
    ///     Collects every <paramref name="rowEdges"/> entry that spans the entire sub-interval
    ///     <c>[ya, yb]</c> (that is, started at or before <paramref name="ya"/> and ends at or
    ///     after <paramref name="yb"/>) into <paramref name="spanning"/>, as a
    ///     <see cref="SpanningEdge"/> giving its x-coordinate at exactly <paramref name="ya"/> and
    ///     <paramref name="yb"/>, and sorts the result ascending by that x-position.
    /// </summary>
    /// <remarks>
    ///     Every entry in <paramref name="rowEdges"/> either fully spans <c>[ya, yb]</c> or has no
    ///     overlap with it at all, never partially - <see cref="CollectYBoundaries"/> already split
    ///     the row at every edge's own start/end y, so no edge can start or stop strictly inside a
    ///     sub-interval boundary produced from that same set of edges.
    /// </remarks>
    private static void CollectSpanningRowEdges(List<RowEdge> rowEdges, float ya, float yb, List<SpanningEdge> spanning)
    {
        spanning.Clear();

        foreach (var edge in rowEdges)
        {
            if (edge.Y0 <= ya + NearZeroDisplacement && edge.Y1 >= yb - NearZeroDisplacement)
            {
                spanning.Add(edge.AtInterval(ya, yb));
            }
        }

        spanning.Sort((left, right) => (left.XAtYa + left.XAtYb).CompareTo(right.XAtYa + right.XAtYb));
    }

    /// <summary>
    ///     Builds the edge table: every non-horizontal edge of every polygon, normalized so
    ///     <see cref="Edge.TopY"/> is less than <see cref="Edge.BottomY"/>, sorted ascending by
    ///     <see cref="Edge.TopY"/> so the active-list sweep in <see cref="Fill"/> can add edges
    ///     with a single forward-advancing pointer.
    /// </summary>
    private static List<Edge> BuildSortedEdgeTable(IReadOnlyList<List<Vector2>> polygons)
    {
        var edges = new List<Edge>();

        foreach (var polygon in polygons)
        {
            for (var i = 0; i < polygon.Count - 1; i++)
            {
                var a = polygon[i];
                var b = polygon[i + 1];

                // A horizontal (or near-horizontal, within floating-point rounding) edge has
                // effectively zero vertical extent and therefore contributes zero coverage under
                // this algorithm - it is never added to the edge table. Using a near-zero
                // threshold rather than exact equality also avoids ever computing a slope with a
                // near-zero denominator below for an edge that is horizontal only up to rounding.
                if (MathF.Abs(a.Y - b.Y) <= NearZeroDisplacement)
                {
                    continue;
                }

                var direction = a.Y < b.Y ? 1 : -1;
                var top = a.Y < b.Y ? a : b;
                var bottom = a.Y < b.Y ? b : a;
                var slope = (bottom.X - top.X) / (bottom.Y - top.Y);

                edges.Add(new Edge(top.Y, bottom.Y, top.X, slope, direction));
            }
        }

        edges.Sort((left, right) => left.TopY.CompareTo(right.TopY));
        return edges;
    }

    /// <summary>
    ///     A single polygon edge, normalized to a y-monotonic representation (<see cref="TopY"/>
    ///     less than <see cref="BottomY"/>) so its x-coordinate at any y within its vertical
    ///     extent can be computed directly via <see cref="TopX"/> and <see cref="Slope"/>, without
    ///     tracking or incrementally updating a running x position (avoiding incremental
    ///     floating-point drift across many rows).
    /// </summary>
    private readonly struct Edge(float topY, float bottomY, float topX, float slope, int direction)
    {
        /// <summary>
        ///     The smaller of the edge's two endpoint y-coordinates.
        /// </summary>
        public float TopY { get; } = topY;

        /// <summary>
        ///     The larger of the edge's two endpoint y-coordinates.
        /// </summary>
        public float BottomY { get; } = bottomY;

        /// <summary>
        ///     The x-coordinate of the edge's endpoint at <see cref="TopY"/>.
        /// </summary>
        public float TopX { get; } = topX;

        /// <summary>
        ///     The edge's rate of change of x per unit y (<c>dx/dy</c>), used to compute the
        ///     x-coordinate at any y within <c>[TopY, BottomY]</c> as
        ///     <c>TopX + Slope * (y - TopY)</c>.
        /// </summary>
        public float Slope { get; } = slope;

        /// <summary>
        ///     The winding contribution direction: <c>+1</c> if the original polygon vertex order
        ///     ran from a lower y to a higher y (top to bottom) along this edge, <c>-1</c> if it
        ///     ran from bottom to top. This sign, not vertex order alone, is what nonzero/even-odd
        ///     winding accumulation depends on.
        /// </summary>
        public int Direction { get; } = direction;
    }

    /// <summary>
    ///     One <see cref="Edge"/> restricted to the portion of a single row's vertical extent it
    ///     overlaps, with its x-coordinate at both endpoints of that restricted range
    ///     precomputed, so <see cref="CollectYBoundaries"/> and <see cref="CollectSpanningRowEdges"/>
    ///     never need to re-derive <see cref="Edge.Slope"/>-based interpolation themselves.
    /// </summary>
    private readonly struct RowEdge(float y0, float y1, float xAtY0, float xAtY1, int direction)
    {
        /// <summary>
        ///     The smaller y-coordinate of this row-restricted segment.
        /// </summary>
        public float Y0 { get; } = y0;

        /// <summary>
        ///     The larger y-coordinate of this row-restricted segment.
        /// </summary>
        public float Y1 { get; } = y1;

        /// <summary>
        ///     The winding contribution direction; see <see cref="Edge.Direction"/>.
        /// </summary>
        public int Direction { get; } = direction;

        /// <summary>
        ///     Computes this segment's x-coordinate at both bounds of a sub-interval
        ///     <c>[ya, yb]</c> (both within <c>[Y0, Y1]</c>), linearly interpolated between this
        ///     segment's own endpoint x-coordinates at <see cref="Y0"/> and <see cref="Y1"/>.
        /// </summary>
        public SpanningEdge AtInterval(float ya, float yb)
        {
            var span = Y1 - Y0;
            if (span <= NearZeroDisplacement)
            {
                // This row-restricted segment has (up to floating-point rounding) zero vertical
                // extent - both interval bounds resolve to its single x-position.
                return new SpanningEdge(xAtY0, xAtY0, Direction);
            }

            var xAtYa = xAtY0 + (xAtY1 - xAtY0) * (ya - Y0) / span;
            var xAtYb = xAtY0 + (xAtY1 - xAtY0) * (yb - Y0) / span;
            return new SpanningEdge(xAtYa, xAtYb, Direction);
        }
    }

    /// <summary>
    ///     One <see cref="RowEdge"/>'s x-coordinate at both bounds of a specific sub-interval
    ///     within its row-restricted extent, as computed by <see cref="RowEdge.AtInterval"/> and
    ///     consumed by <see cref="ResolveIntervalCoverage"/>/<see cref="AddIntervalArea"/>.
    /// </summary>
    private readonly struct SpanningEdge(float xAtYa, float xAtYb, int direction)
    {
        /// <summary>
        ///     The x-coordinate at the sub-interval's start y.
        /// </summary>
        public float XAtYa { get; } = xAtYa;

        /// <summary>
        ///     The x-coordinate at the sub-interval's end y.
        /// </summary>
        public float XAtYb { get; } = xAtYb;

        /// <summary>
        ///     The winding contribution direction; see <see cref="Edge.Direction"/>.
        /// </summary>
        public int Direction { get; } = direction;
    }
}
