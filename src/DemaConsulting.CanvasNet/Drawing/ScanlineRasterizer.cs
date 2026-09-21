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
///     overlaps the current row (added from the edge table when reached, removed - via a second,
///     symmetric per-row expiration bucket keyed by the row each edge stops overlapping, rather
///     than re-scanning the whole active list every row - once their vertical extent is exhausted)
///     - bounding total work to <c>O(edges + total edge-row crossings)</c> rather than
///     <c>O(edges x rows)</c>.
///     </para>
///     <para>
///     <b>Cell-based signed area/cover accumulation, not per-interval edge sorting.</b> Every row
///     maintains two dense per-column accumulators, <c>cover[x]</c> (the net signed vertical
///     extent of every edge slice passing through column <c>x</c>) and <c>area[x]</c> (the exact,
///     signed sub-cell area of column <c>x</c> lying to the right of every edge slice passing
///     through it) - this is exactly the technique used by AGG's <c>scanline_u8</c>, FreeType's
///     "smooth" rasterizer, and <c>stb_truetype</c>. Every active edge restricted to the row
///     (<see cref="RowEdge"/>) is accumulated independently into these two shared arrays via
///     <see cref="CoverageSweep.AccumulateRowEdge"/> - <b>no sorting and no reasoning about edges' relative
///     x-order whatsoever</b>. Accumulating a single near-vertical edge
///     (<see cref="CoverageSweep.AccumulateSingleColumn"/>) is <c>O(1)</c>, but accumulating a slanted edge
///     (<see cref="CoverageSweep.AccumulateSlantedSpan"/>) walks every pixel column the edge's row-restricted
///     segment crosses, so it costs <c>O(1 + columns crossed)</c> - up to <c>O(width)</c> for a
///     single edge that is nearly horizontal within the row and spans the whole visible width.
///     This entire accumulation phase therefore costs <c>O(edges + total columns crossed by every
///     edge's row-restricted segment)</c>, not a flat <c>O(edges)</c>: it only approaches
///     <c>O(edges)</c> for the common case of edges that are steep relative to the row height (few
///     columns crossed per edge), and degrades toward <c>O(edges x width)</c> in the pathological
///     case of many edges that are each nearly horizontal within a single row. This is the same
///     trade-off AGG's own <c>rasterizer_cells_aa::line</c>/FreeType's <c>gray_render_line</c>
///     accept: producing exact per-pixel coverage fundamentally requires visiting every pixel a
///     slanted edge's row-restricted segment actually crosses, so this cost cannot be avoided
///     without abandoning per-pixel coverage output altogether. The row is then swept left to
///     right exactly once (<c>O(width)</c>): a running <c>accumulatedCover</c> total starts at
///     zero, and
///     at each column <c>x</c>, <c>accumulatedCover</c> is first advanced by <c>cover[x]</c>
///     (folding this column's own vertical edge crossings into the running winding total), and
///     only then is the resolved raw signed value - <c>accumulatedCover + area[x]</c> (the
///     updated running total, including this column, plus this column's own partial-edge
///     geometry) - converted to a <c>[0, 1]</c> coverage fraction by
///     <see cref="CoverageSweep.ResolveCoverage"/> per <see cref="FillRule"/>.
///     </para>
///     <para>
///     <b>Why this fixes crossing/self-intersecting edges by construction.</b> A prior revision
///     of this algorithm split each row into sub-intervals at every edge's own start/end y, sorted
///     the edges spanning each sub-interval by x once, and relied on that order staying fixed for
///     the sub-interval's whole vertical extent. That assumption fails whenever two edges actually
///     cross each other's x-order strictly inside a sub-interval (not at a shared vertex or row
///     boundary) - which happens for ordinary self-intersecting polygons such as a bowtie or star,
///     which <see cref="EdgeFlattener"/> does not detect or split - producing gross over-filling
///     (observed: ~100% fill where a dense-supersampling ground truth reference is ~67%, under
///     both fill rules, for a simple two-edge bowtie crossing mid-row). Cell-based accumulation
///     sidesteps this entirely: each edge only ever contributes to the specific column(s) it
///     geometrically passes through, independently of every other edge, and the sweep's running
///     total
///     reconstructs the correct winding number at every x purely via summation - which is
///     associative/commutative regardless of the order in which edges cross one another. No
///     comparison between edges' positions is ever needed, so crossing/self-intersecting edges are
///     handled correctly, not merely assumed absent.
///     </para>
///     <para>
///     <b>Trade-off: exactly coincident/duplicate edges within the same cell.</b> Because each
///     edge's contribution is accumulated independently rather than resolved against a per-cell
///     winding decision first, two edges that occupy the exact same sub-pixel position within one
///     cell (for example, an identical polygon submitted twice) have their raw signed
///     cover/area contributions sum linearly, which can exceed the single-shape value before
///     <see cref="CoverageSweep.ResolveCoverage"/> folds it back into <c>[0, 1]</c> - this is the same
///     documented, accepted behavior of AGG/FreeType/<c>stb_truetype</c> for coincident contours,
///     and is a materially rarer case in practice than ordinary self-intersecting geometry, which
///     is why this trade-off is accepted in exchange for fixing the crossing-edge bug.
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
    ///     no coverage) or a row-restricted edge segment as vertical (a single-column
    ///     contribution).
    ///     </summary>
    /// <remarks>
    ///     A genuinely horizontal or vertical edge always lands well within this threshold (its
    ///     displacement is exactly zero); the threshold exists so a value that is zero only up to
    ///     ordinary floating-point rounding is treated identically, rather than falling through to
    ///     the general formula and dividing by a near-zero denominator. This is far smaller than
    ///     any meaningful sub-pixel distance, so it never affects the antialiasing accuracy of
    ///     genuinely distinct geometry.
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
    ///     (<see cref="PathFiller.Fill(Canvas.Surface, Geometry.Path, Canvas.Rgba32, FillRule, float)"/>); this method rounds it outward to whole pixel rows and
    ///     columns.
    /// </param>
    internal static void Fill(Surface surface, IReadOnlyList<List<Vector2>> polygons, Rgba32 color, FillRule fillRule, Rect clipBounds)
    {
        var sweep = new CoverageSweep(polygons, fillRule, clipBounds);
        if (sweep.IsEmpty)
        {
            return;
        }

        // Every row in this loop composites the same fixed-width "rowCoverage" span
        // (clipMinX..clipMaxX), so a single workspace sized to that width can serve every row's
        // CompositeOverSpan call - amortizing the per-row ArrayPool rent/return of
        // Surface.CompositeOverSpan's scratch buffers across the whole fill instead of paying it
        // once per rasterized row.
        using var compositeWorkspace = new Surface.CompositeSpanWorkspace(sweep.Width);

        while (sweep.MoveNext(out var y, out var rowCoverage))
        {
            surface.CompositeOverSpan(y, sweep.ClipMinX, rowCoverage, color, compositeWorkspace);
        }
    }

    /// <summary>
    ///     Rasterizes <paramref name="polygons"/> onto <paramref name="surface"/>, evaluating
    ///     <paramref name="paint"/> once per pixel via <see cref="GradientEvaluator.EvaluateRow"/>
    ///     and compositing the resulting per-pixel colors scaled by each pixel's analytically
    ///     computed fill coverage, restricted to <paramref name="clipBounds"/>.
    /// </summary>
    /// <param name="surface">The surface to composite into. Must not be null.</param>
    /// <param name="polygons">
    ///     The closed polygons to fill (typically produced by <see cref="EdgeFlattener.Flatten"/>).
    /// </param>
    /// <param name="paint">The gradient paint to evaluate per pixel. Must not be null.</param>
    /// <param name="fillRule">The rule used to resolve overlapping/self-intersecting geometry.</param>
    /// <param name="clipBounds">
    ///     The region to rasterize, in the same path-space coordinates as <paramref name="polygons"/>.
    /// </param>
    /// <remarks>
    ///     Shares its whole row-coverage computation (edge table build, active-edge tracking, and
    ///     per-row <c>cover</c>/<c>area</c> accumulation) with the constant-color
    ///     <see cref="Fill(Surface, IReadOnlyList{List{Vector2}}, Rgba32, FillRule, Rect)"/>
    ///     overload via the shared <see cref="CoverageSweep"/> helper - the only difference between
    ///     the two overloads is the final per-row compositing call, which here first evaluates a
    ///     per-pixel color row via <see cref="GradientEvaluator.EvaluateRow"/>.
    /// </remarks>
    internal static void Fill(Surface surface, IReadOnlyList<List<Vector2>> polygons, Gradient paint, FillRule fillRule, Rect clipBounds)
    {
        ArgumentNullException.ThrowIfNull(paint);

        var sweep = new CoverageSweep(polygons, fillRule, clipBounds);
        if (sweep.IsEmpty)
        {
            return;
        }

        using var compositeWorkspace = new Surface.CompositeSpanWorkspace(sweep.Width);
        var rowColors = new Rgba32[sweep.Width];

        while (sweep.MoveNext(out var y, out var rowCoverage))
        {
            GradientEvaluator.EvaluateRow(paint, y, sweep.ClipMinX, sweep.Width, rowColors);
            surface.CompositeOverSpan(y, sweep.ClipMinX, rowCoverage, rowColors, compositeWorkspace);
        }
    }

    /// <summary>
    ///     Encapsulates the row-coverage computation shared by every <c>Fill</c> overload:
    ///     the edge table build, active-edge tracking, and per-row <c>cover</c>/<c>area</c>
    ///     accumulation/resolution described in this class's type-level remarks. A caller
    ///     constructs one instance per fill operation, checks <see cref="IsEmpty"/>, then repeatedly
    ///     calls <see cref="MoveNext"/> until it returns <see langword="false"/>, compositing each
    ///     yielded row's coverage span however that particular <c>Fill</c> overload needs to
    ///     (constant color vs. per-pixel gradient color) - the coverage math itself is computed
    ///     exactly once, in exactly one place, regardless of how many <c>Fill</c> overloads exist.
    /// </summary>
    private sealed class CoverageSweep
    {
        private readonly List<Edge> _edges;
        private readonly FillRule _fillRule;
        private readonly int _clipMinX;
        private readonly int _clipMaxX;
        private readonly int _clipMinY;
        private readonly int _clipMaxY;
        private readonly float[] _cover;
        private readonly float[] _area;
        private readonly float[] _rowCoverage;
        private readonly List<Edge> _activeEdges = [];
        private readonly List<int> _activeEdgeIds = [];
        private readonly Dictionary<int, int> _activeEdgePositions = [];
        private readonly List<int>?[] _expiringEdgeIds;
        private readonly List<RowEdge> _rowEdges = [];
        private int _nextEdgeIndex;
        private int _currentY;

        public CoverageSweep(IReadOnlyList<List<Vector2>> polygons, FillRule fillRule, Rect clipBounds)
        {
            _fillRule = fillRule;

            // Round the (already surface-clipped) float clip bounds outward to whole pixel rows
            // and columns - the rasterizer always operates on whole-pixel scanlines and columns.
            _clipMinX = (int)MathF.Floor(clipBounds.Left);
            _clipMaxX = (int)MathF.Ceiling(clipBounds.Right);
            _clipMinY = (int)MathF.Floor(clipBounds.Top);
            _clipMaxY = (int)MathF.Ceiling(clipBounds.Bottom);
            Width = _clipMaxX - _clipMinX;
            _currentY = _clipMinY;

            if (Width <= 0 || _clipMaxY <= _clipMinY)
            {
                _edges = [];
                _expiringEdgeIds = [];
                IsEmpty = true;
                _cover = [];
                _area = [];
                _rowCoverage = [];
                return;
            }

            _edges = BuildSortedEdgeTable(polygons);
            if (_edges.Count == 0)
            {
                _expiringEdgeIds = [];
                IsEmpty = true;
                _cover = [];
                _area = [];
                _rowCoverage = [];
                return;
            }

            // Dense per-row accumulators. "cover"/"area" are the shared cell accumulators every
            // active edge's row-restricted slice contributes into independently (see the
            // type-level remarks) - "cover" is sized Width + 1 for the same "harmless overflow
            // slot" reason AccumulateSingleColumn/AccumulateSlantedSpan's own bank-index clamping
            // needs. "rowCoverage" holds the final, already fill-rule-resolved coverage per
            // column, produced by the single left-to-right sweep over "cover"/"area".
            _cover = new float[Width + 1];
            _area = new float[Width];
            _rowCoverage = new float[Width];
            _expiringEdgeIds = new List<int>?[_clipMaxY - _clipMinY];
        }

        /// <summary>
        ///     The clipped row width, in pixel columns; also the length of every
        ///     <c>rowCoverage</c> span yielded by <see cref="MoveNext"/>.
        /// </summary>
        public int Width { get; }

        /// <summary>
        ///     The clipped leftmost pixel column; the <c>x</c> argument every caller should pass
        ///     to <see cref="Surface.CompositeOverSpan(int, int, ReadOnlySpan{float}, Rgba32, Surface.CompositeSpanWorkspace)"/>
        ///     alongside a yielded row.
        /// </summary>
        public int ClipMinX => _clipMinX;

        /// <summary>
        ///     <see langword="true"/> when this fill operation has no visible rows or edges at
        ///     all (an empty clip region, or geometry with no non-horizontal edges), meaning
        ///     <see cref="MoveNext"/> would never yield a row - callers should skip the whole
        ///     compositing loop (and any workspace allocation) entirely in this case.
        /// </summary>
        public bool IsEmpty { get; }

        /// <summary>
        ///     Advances to the next visible row, if any, computing its fully resolved
        ///     <c>[0, 1]</c> coverage span.
        /// </summary>
        /// <param name="y">The zero-based row just computed.</param>
        /// <param name="rowCoverage">
        ///     The row's coverage span, one value per column starting at <see cref="ClipMinX"/>.
        ///     This span is reused (overwritten) by every call - a caller must fully consume it
        ///     (for example, by compositing it) before calling <see cref="MoveNext"/> again.
        /// </param>
        /// <returns>
        ///     <see langword="true"/> if a row was produced; <see langword="false"/> once every
        ///     row in the clip range has been swept.
        /// </returns>
        public bool MoveNext(out int y, out ReadOnlySpan<float> rowCoverage)
        {
            while (_currentY < _clipMaxY)
            {
                y = _currentY;
                _currentY++;

                var rowTop = (float)y;
                var rowBottom = rowTop + 1f;

                // Remove every edge bucketed to expire at this row (see the bucketing below):
                // this touches exactly the edges that actually expire on this row, never the
                // whole active list, so - unlike a full
                // "activeEdges.RemoveAll(edge => edge.BottomY <= rowTop)" scan of every still-
                // active edge on every row - this bookkeeping never re-examines an edge that still
                // has rows left to contribute to.
                var expiringHere = _expiringEdgeIds[y - _clipMinY];
                if (expiringHere != null)
                {
                    foreach (var expiredId in expiringHere)
                    {
                        RemoveActiveEdge(expiredId, _activeEdges, _activeEdgeIds, _activeEdgePositions);
                    }
                }

                while (_nextEdgeIndex < _edges.Count && _edges[_nextEdgeIndex].TopY < rowBottom)
                {
                    // Each edge is identified by its own fixed position in the sorted edge table
                    // ("_edges"), which never changes and is never reused, so it is a stable id to
                    // bucket by even though its position within the unordered "_activeEdges" list
                    // itself can move (see RemoveActiveEdge's swap-remove).
                    var edgeId = _nextEdgeIndex;
                    var edge = _edges[edgeId];
                    _activeEdgePositions[edgeId] = _activeEdges.Count;
                    _activeEdges.Add(edge);
                    _activeEdgeIds.Add(edgeId);

                    // Bucket this edge's removal at the earliest row it can actually be observed
                    // as expired. Removal always happens at the *start* of a row, before this
                    // row's own additions, so an edge just added this row cannot be examined for
                    // expiry until at least the next row - hence the "y + 1" floor alongside the
                    // edge's own BottomY.
                    var expireRow = Math.Max((int)MathF.Ceiling(edge.BottomY), y + 1);
                    if (expireRow < _clipMaxY)
                    {
                        var bucket = _expiringEdgeIds[expireRow - _clipMinY] ??= [];
                        bucket.Add(edgeId);
                    }

                    _nextEdgeIndex++;
                }

                if (_activeEdges.Count == 0)
                {
                    continue;
                }

                BuildRowEdges(_activeEdges, rowTop, rowBottom, _rowEdges);
                if (_rowEdges.Count == 0)
                {
                    continue;
                }

                // Accumulate every active edge's row-restricted slice into the shared cell
                // arrays - a single O(edges) pass, with no sorting and no pairing of edges into
                // "inside gaps".
                Array.Clear(_cover);
                Array.Clear(_area);
                foreach (var edge in _rowEdges)
                {
                    AccumulateRowEdge(edge, _clipMinX, _clipMaxX, _cover, _area);
                }

                // Single left-to-right sweep: "accumulatedCover" is the running winding total. At
                // each column, "accumulatedCover" is first advanced by this column's own
                // "cover[x]", then the updated running total plus this column's own partial-edge
                // geometry ("area[x]") is resolved to a [0, 1] coverage fraction per fill rule.
                var accumulatedCover = 0f;
                for (var i = 0; i < Width; i++)
                {
                    accumulatedCover += _cover[i];
                    var total = accumulatedCover + _area[i];
                    _rowCoverage[i] = ResolveCoverage(total, _fillRule);
                }

                rowCoverage = _rowCoverage;
                return true;
            }

            y = 0;
            rowCoverage = default;
            return false;
        }

        /// <summary>
        ///     Removes the active-list entry identified by <paramref name="edgeId"/> (its fixed
        ///     index in the sorted edge table) from <paramref name="activeEdges"/>/<paramref
        ///     name="activeEdgeIds"/> in <c>O(1)</c>, via swap-remove with the last entry rather
        ///     than a linear shift of every subsequent element.
        /// </summary>
        /// <remarks>
        ///     Swap-remove is safe here specifically because active-edge order never matters to
        ///     any downstream consumer (<see cref="BuildRowEdges"/> and the cell accumulation it
        ///     feeds are associative/commutative regardless of edge order - see the type-level
        ///     remarks) - unlike a naive "scan every active edge every row" removal, this keeps
        ///     the whole sweep's bookkeeping bounded to <c>O(edges)</c> total (one add, one
        ///     lookup, and one removal per edge), never <c>O(edges x rows)</c>, no matter how many
        ///     rows an edge remains active for.
        /// </remarks>
        private static void RemoveActiveEdge(
            int edgeId, List<Edge> activeEdges, List<int> activeEdgeIds, Dictionary<int, int> activeEdgePositions)
        {
            if (!activeEdgePositions.Remove(edgeId, out var position))
            {
                return;
            }

            var lastIndex = activeEdges.Count - 1;
            if (position != lastIndex)
            {
                var movedEdgeId = activeEdgeIds[lastIndex];
                activeEdges[position] = activeEdges[lastIndex];
                activeEdgeIds[position] = movedEdgeId;
                activeEdgePositions[movedEdgeId] = position;
            }

            activeEdges.RemoveAt(lastIndex);
            activeEdgeIds.RemoveAt(lastIndex);
        }

        /// <summary>
        ///     Converts a column's raw signed accumulated cell value (<c>accumulatedCover +
        ///     area[x]</c>, see <see cref="MoveNext"/>) into a <c>[0, 1]</c> coverage fraction per
        ///     <paramref name="fillRule"/>.
        /// </summary>
        /// <remarks>
        ///     <c>NonZero</c> is <c>min(1, abs(total))</c>: any nonzero magnitude is fully
        ///     "inside", clamped to a whole pixel's worth of coverage. <c>EvenOdd</c> folds
        ///     <paramref name="total"/> into a <c>[0, 2)</c> triangle wave and reflects it
        ///     (<c>folded > 1 ? 2 - folded : folded</c>), matching the classic even-odd "every
        ///     crossing toggles inside/outside" semantics applied to a continuous, analytically
        ///     accumulated value rather than an integer winding count.
        /// </remarks>
        private static float ResolveCoverage(float total, FillRule fillRule)
        {
            var magnitude = MathF.Abs(total);
            if (fillRule == FillRule.NonZero)
            {
                return Math.Clamp(magnitude, 0f, 1f);
            }

            var folded = magnitude % 2f;
            return folded > 1f ? 2f - folded : folded;
        }

        /// <summary>
        ///     Accumulates one row-restricted edge slice's contribution into the row's shared
        ///     <paramref name="cover"/>/<paramref name="area"/> cell arrays (see
        ///     <see cref="MoveNext"/>), reusing the same single-edge geometry as
        ///     <see cref="AccumulateSingleColumn"/>/<see cref="AccumulateSlantedSpan"/> - the only
        ///     change from a single-edge design is that every <see cref="RowEdge"/> for the row is
        ///     accumulated into the same shared arrays rather than each edge (or interval
        ///     boundary) getting its own scratch buffer.
        /// </summary>
        private static void AccumulateRowEdge(RowEdge edge, int clipMinX, int clipMaxX, float[] cover, float[] area)
        {
            if (MathF.Abs(edge.XAtY1 - edge.XAtY0) <= NearZeroDisplacement)
            {
                AccumulateSingleColumn(edge.XAtY0, edge.Y1 - edge.Y0, edge.Direction, clipMinX, clipMaxX, cover, area);
                return;
            }

            AccumulateSlantedSpan(edge.XAtY0, edge.Y0, edge.XAtY1, edge.Y1, edge.Direction, clipMinX, clipMaxX, cover, area);
        }

        /// <summary>
        ///     Builds the per-row edge list: every <paramref name="activeEdges"/> entry,
        ///     restricted to the portion of its vertical extent overlapping
        ///     <c>[rowTop, rowBottom)</c>, with its x-coordinate at both that restricted range's
        ///     start and end y precomputed. Edges whose vertical extent does not actually overlap
        ///     this row at all (possible at the boundary rows of an edge's extent, due to
        ///     floating-point comparisons in the active-list sweep) are omitted.
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
        ///     Builds the edge table: every non-horizontal edge of every polygon, normalized so
        ///     <see cref="Edge.TopY"/> is less than <see cref="Edge.BottomY"/>, sorted ascending
        ///     by <see cref="Edge.TopY"/> so the active-list sweep in <see cref="MoveNext"/> can
        ///     add edges with a single forward-advancing pointer.
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
                    // effectively zero vertical extent and therefore contributes zero coverage
                    // under this algorithm - it is never added to the edge table. Using a
                    // near-zero threshold rather than exact equality also avoids ever computing a
                    // slope with a near-zero denominator below for an edge that is horizontal
                    // only up to rounding.
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
                // pixel never reaches this boundary, so it contributes nothing to any visible
                // column.
                return;
            }

            var fraction = x - column;
            area[column - clipMinX] += direction * deltaY * (1f - fraction);
            var bankIndex = Math.Clamp(column + 1 - clipMinX, 0, clipMaxX - clipMinX);
            cover[bankIndex] += direction * deltaY;
        }

        /// <summary>
        ///     Accumulates the contribution of a boundary-line segment that spans a horizontal
        ///     range of x (a slanted line), splitting it at the visible clip boundaries first,
        ///     then walking each pixel column the visible portion crosses.
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

            // Right overflow: the portion of the segment with x >= clipMaxX contributes nothing
            // to any visible column (a ray cast further right never reaches it) - simply drop it.
            if (xRight > clipMaxX)
            {
                xRight = clipMaxX;
            }

            if (xLeft >= xRight)
            {
                return;
            }

            // Walk each pixel column the remaining, now fully visible-range-clipped span
            // crosses. This loop is bounded by the visible column count
            // (clipMaxX - clipMinX), never by the boundary's own potentially unbounded extent,
            // because xLeft/xRight were already clipped above.
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
    ///     precomputed, so <see cref="CoverageSweep.AccumulateRowEdge"/> never needs to re-derive
    ///     <see cref="Edge.Slope"/>-based interpolation itself.
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
        ///     This segment's x-coordinate at <see cref="Y0"/>.
        /// </summary>
        public float XAtY0 { get; } = xAtY0;

        /// <summary>
        ///     This segment's x-coordinate at <see cref="Y1"/>.
        /// </summary>
        public float XAtY1 { get; } = xAtY1;

        /// <summary>
        ///     The winding contribution direction; see <see cref="Edge.Direction"/>.
        /// </summary>
        public int Direction { get; } = direction;
    }
}
