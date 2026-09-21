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
///     crossings)</c> rather than <c>O(edges x rows)</c>. For each row, two dense <c>float</c>
///     buffers (<c>cover</c> and <c>area</c>, sized to the clipped row width, cleared per row)
///     accumulate each active edge's exact geometric contribution: <c>cover</c> receives the
///     edge's full signed vertical extent, banked one column to the right of wherever the edge
///     lies (so it propagates rightward to every subsequent pixel via the prefix sum below,
///     modeling "this edge is entirely to the left, so a ray cast further right always crosses
///     it"), while <c>area</c> receives the additional fractional trapezoidal area for the
///     specific cell the edge passes through sub-pixel (the exact analytic antialiasing term). A
///     single left-to-right prefix sum (<c>acc += cover[x]; pixelCoverage[x] = acc + area[x];</c>)
///     then yields the raw signed winding-weighted coverage per pixel, and the fill rule resolves
///     that raw value to an alpha in <c>[0, 1]</c> (see <see cref="ResolveAlpha"/>). The resulting
///     alpha row is composited directly via <see cref="Surface.CompositeOverSpan"/> - no
///     intermediate full-row or full-surface buffer is ever allocated.
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
    ///     no coverage) or a row-clipped segment as vertical (a single-column contribution).
    /// </summary>
    /// <remarks>
    ///     A genuinely horizontal or vertical edge always lands well within this threshold (its
    ///     displacement is exactly zero); the threshold exists so an edge that is horizontal or
    ///     vertical only up to ordinary floating-point rounding is treated identically, rather than
    ///     falling through to the general slanted-edge formula and dividing by a near-zero
    ///     denominator. This is far smaller than any meaningful sub-pixel distance, so it never
    ///     affects the antialiasing accuracy of a genuinely slanted edge.
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

        // Dense per-row scratch buffers, allocated once and cleared per row (see the type-level
        // remarks for why dense buffers - rather than a sparse per-cell structure - were chosen).
        // "cover" is sized width + 1 so an edge lying exactly at, or banked one column past, the
        // rightmost visible column has a harmless slot to accumulate into that is never read by
        // the prefix sum below.
        var cover = new float[width + 1];
        var area = new float[width];
        var coverage = new float[width];

        var activeEdges = new List<Edge>();
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

            Array.Clear(cover);
            Array.Clear(area);

            foreach (var edge in activeEdges)
            {
                AccumulateEdgeRowContribution(edge, rowTop, rowBottom, clipMinX, clipMaxX, cover, area);
            }

            // Left-to-right prefix sum: acc carries the running signed winding-weighted coverage
            // contributed by every edge banked at or before the current column; area supplies the
            // additional sub-pixel antialiasing term for this column alone.
            var acc = 0f;
            for (var i = 0; i < width; i++)
            {
                acc += cover[i];
                coverage[i] = ResolveAlpha(acc + area[i], fillRule);
            }

            surface.CompositeOverSpan(y, clipMinX, coverage, color);
        }
    }

    /// <summary>
    ///     Resolves a raw, signed winding-weighted coverage value to an alpha in <c>[0, 1]</c>
    ///     according to <paramref name="fillRule"/>.
    /// </summary>
    private static float ResolveAlpha(float rawCoverage, FillRule fillRule)
    {
        if (fillRule == FillRule.EvenOdd)
        {
            // Fold into [0, 2) via modulo (handling negative raw coverage), then apply the
            // triangle-wave fold: a raw coverage of 0 or 2 means "outside" (alpha 0), 1 means
            // "inside" (alpha 1), matching even-odd's alternating inside/outside semantics.
            var folded = rawCoverage % 2f;
            if (folded < 0)
            {
                folded += 2f;
            }

            var triangle = folded <= 1f ? folded : 2f - folded;
            return Math.Clamp(triangle, 0f, 1f);
        }

        // NonZero: any nonzero winding count is "inside"; clamp the magnitude to a maximum of
        // fully opaque so overlapping same-direction windings never exceed full coverage.
        return Math.Min(1f, Math.Abs(rawCoverage));
    }

    /// <summary>
    ///     Adds one active edge's contribution, for the portion of the edge overlapping
    ///     <c>[rowTop, rowBottom)</c>, to <paramref name="cover"/> and <paramref name="area"/>.
    /// </summary>
    private static void AccumulateEdgeRowContribution(
        Edge edge, float rowTop, float rowBottom, int clipMinX, int clipMaxX, float[] cover, float[] area)
    {
        var sy0 = Math.Max(edge.TopY, rowTop);
        var sy1 = Math.Min(edge.BottomY, rowBottom);
        if (sy0 >= sy1)
        {
            // This edge does not actually overlap this row (can happen at the boundary rows of
            // its vertical extent due to floating-point comparisons in the active-list sweep).
            return;
        }

        var xa = edge.TopX + edge.Slope * (sy0 - edge.TopY);
        var xb = edge.TopX + edge.Slope * (sy1 - edge.TopY);

        // A near-zero horizontal displacement is treated as a single-column (vertical) segment,
        // both because a genuinely vertical edge (Slope exactly 0) always lands here exactly, and
        // to avoid ever dividing by a near-zero displacement in AccumulateSlantedSpan for an edge
        // that is vertical only up to floating-point rounding.
        if (MathF.Abs(xb - xa) <= NearZeroDisplacement)
        {
            AccumulateSingleColumn(xa, sy1 - sy0, edge.Direction, clipMinX, clipMaxX, cover, area);
            return;
        }

        AccumulateSlantedSpan(xa, sy0, xb, sy1, edge.Direction, clipMinX, clipMaxX, cover, area);
    }

    /// <summary>
    ///     Accumulates the contribution of an edge-row segment that lies within a single pixel
    ///     column (a vertical, or row-clipped-to-vertical, segment at constant x).
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
            // pixel never reaches this edge, so it contributes nothing to any visible column.
            return;
        }

        var fraction = x - column;
        area[column - clipMinX] += direction * deltaY * (1f - fraction);
        var bankIndex = Math.Clamp(column + 1 - clipMinX, 0, clipMaxX - clipMinX);
        cover[bankIndex] += direction * deltaY;
    }

    /// <summary>
    ///     Accumulates the contribution of an edge-row segment that spans a horizontal range of
    ///     x (a slanted edge), splitting it at the visible clip boundaries first, then walking
    ///     each pixel column the visible portion crosses.
    /// </summary>
    private static void AccumulateSlantedSpan(
        float xa, float sy0, float xb, float sy1, int direction, int clipMinX, int clipMaxX, float[] cover, float[] area)
    {
        var xLeft = Math.Min(xa, xb);
        var xRight = Math.Max(xa, xb);

        // Left overflow: the portion of the segment with x < clipMinX contributes its full
        // (unsplit) vertical extent banked at the first visible column, exactly as a fully
        // left-of-range edge would - see AccumulateSingleColumn's analogous case.
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
        // edge's own potentially unbounded extent, because xLeft/xRight were already clipped
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
}
