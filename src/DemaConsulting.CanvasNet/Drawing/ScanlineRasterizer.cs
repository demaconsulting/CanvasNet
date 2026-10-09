// cspell:ignore precomputation
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
///     <b>Why a single fold is insufficient when multiple edges toggle inside one row under
///     <see cref="FillRule.EvenOdd"/>.</b> <see cref="CoverageSweep.ResolveCoverage"/>'s even-odd branch
///     folds a single scalar - the row-height-weighted integral of winding number,
///     <c>&#8747; w(y) dy</c> over the row, normalized to <c>[0, 1]</c> - into a parity value via
///     <c>magnitude % 2</c> (reflected above <c>1</c>). Folding and integrating commute
///     (<c>fold(&#8747; w dy) = &#8747; fold(w(y)) dy</c>) only when <c>w(y)</c> does not change
///     sign/parity-class more than once within the row for a given column - i.e. at most one
///     edge's <c>Y0</c>/<c>Y1</c> lands strictly inside the row. When two or more edges' start/end
///     y-boundaries land inside the SAME row for the same column (for example several nested
///     shapes whose boundaries are all closer together than one device pixel, such as this
///     project's own "double border" regression - four nested rectangles whose boundaries all
///     land in one row), <c>fold(&#8747; w dy) &#8800; &#8747; fold(w(y)) dy</c> in general: the
///     single scalar fold sees only the row's total accumulated winding, not how many times parity
///     actually toggled across the row's height, and silently produces the wrong coverage (for
///     example, a fully opaque fold where the true height-weighted parity average is a fraction
///     such as ~0.33). <see cref="FillRule.NonZero"/> never needs this reasoning: its fold
///     (<c>magnitude != 0</c>) is a single yes/no threshold on the same running total regardless of
///     how many times winding crosses zero within the row, so <c>fold</c> and <c>&#8747;</c>
///     trivially commute for it - which is exactly why the fix below touches only the
///     <see cref="FillRule.EvenOdd"/> path and leaves <see cref="FillRule.NonZero"/>'s code path
///     completely untouched, bit-for-bit.
///     </para>
///     <para>
///     <b>Fix: exact sub-row splitting for the rare even-odd multi-toggle-per-row case.</b> Each
///     row first runs <see cref="CoverageSweep.CollectMidRowBreakpoints"/>, which scans every active edge's
///     <c>Y0</c>/<c>Y1</c> for values landing strictly inside the row (not merely at its top/bottom
///     boundary, which is the common case and needs no special handling). When fewer than two
///     distinct breakpoints are found, or the fill rule is <see cref="FillRule.NonZero"/>, the row
///     is accumulated exactly as before via <see cref="CoverageSweep.AccumulateRowCoverageSinglePass"/> - the
///     original, unmodified single-pass method, same code, same output, bit-for-bit, for the
///     overwhelming common case. Only when <see cref="FillRule.EvenOdd"/> and two or more
///     breakpoints land inside the row does
///     <see cref="CoverageSweep.AccumulateRowCoverageWithSubRowSplitting"/> run instead: the row is split into
///     sub-intervals at each breakpoint, and <see cref="CoverageSweep.AccumulateSubInterval"/> re-clips every
///     <see cref="RowEdge"/> to each sub-interval's <c>[top, bottom)</c> span by linearly
///     re-interpolating its already-affine <c>x(y)</c> mapping at the sub-interval's boundaries -
///     exact for slanted edges as well as axis-aligned ones, since a <see cref="RowEdge"/>'s x as a
///     function of y is already an affine (straight-line) relationship by construction, and
///     restricting an affine function to a sub-interval of its domain and re-evaluating its
///     endpoints is exact, not an approximation. By construction each sub-interval contains no
///     breakpoint strictly inside it (breakpoints are exactly the sub-interval boundaries), so
///     within any single sub-interval no edge newly starts or stops partway through for any
///     column - restoring the single-toggle-per-row precondition the existing, unmodified
///     <see cref="CoverageSweep.ResolveCoverage"/> fold already relies on, so each sub-interval's coverage is
///     resolved by that same fold, unmodified, scaled to that sub-interval's own height. The
///     sub-intervals' resolved coverages are then combined by weighting each by
///     <c>sub-interval height / row height</c> and summing - exactly the height-weighted parity
///     average the single-fold approach was supposed to approximate, but now computed exactly
///     because each term is independently resolved over a range where the fold is valid. This
///     raises the rare multi-toggle row's cost from <c>O(edges + columns)</c> to
///     <c>O(edges + columns x breakpoints)</c> (re-accumulating cell contributions once per
///     sub-interval) - strictly worse than the common case, but only in the rare row where several
///     edges' boundaries coincide within one device pixel, and still bounded by the number of
///     edges actually active in that row, so it cannot degrade overall complexity beyond a small,
///     rare, local constant-factor multiplier.
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
    /// <param name="clip">
    ///     An optional PDF clipping path coverage mask (see <see cref="ClipMask"/>) whose per-pixel
    ///     coverage is multiplied into this fill's own antialiased coverage before compositing, or
    ///     <see langword="null"/> when no clip is active. Defaults to <see langword="null"/> so
    ///     every pre-existing call site (none of which know about clipping) is unaffected.
    /// </param>
    internal static void Fill(Surface surface, IReadOnlyList<List<Vector2>> polygons, Rgba32 color, FillRule fillRule, Rect clipBounds, ClipMask? clip = null)
    {
        var sweep = new CoverageSweep(polygons, fillRule, clipBounds, clip);
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
    /// <param name="clip">
    ///     An optional PDF clipping path coverage mask (see <see cref="ClipMask"/>) whose per-pixel
    ///     coverage is multiplied into this fill's own antialiased coverage before compositing, or
    ///     <see langword="null"/> when no clip is active. Defaults to <see langword="null"/>.
    /// </param>
    /// <remarks>
    ///     Shares its whole row-coverage computation (edge table build, active-edge tracking, and
    ///     per-row <c>cover</c>/<c>area</c> accumulation) with the constant-color
    ///     <see cref="Fill(Surface, IReadOnlyList{List{Vector2}}, Rgba32, FillRule, Rect, ClipMask?)"/>
    ///     overload via the shared <see cref="CoverageSweep"/> helper - the only difference between
    ///     the two overloads is the final per-row compositing call, which here first evaluates a
    ///     per-pixel color row via <see cref="GradientEvaluator.EvaluateRow"/>, against a
    ///     <see cref="Gradient"/> plan built exactly once for the whole fill operation (via
    ///     <see cref="GradientEvaluator.CreatePlan"/>), not rebuilt on every row.
    /// </remarks>
    internal static void Fill(Surface surface, IReadOnlyList<List<Vector2>> polygons, Gradient paint, FillRule fillRule, Rect clipBounds, ClipMask? clip = null)
    {
        ArgumentNullException.ThrowIfNull(paint);

        var sweep = new CoverageSweep(polygons, fillRule, clipBounds, clip);
        if (sweep.IsEmpty)
        {
            return;
        }

        using var compositeWorkspace = new Surface.CompositeSpanWorkspace(sweep.Width);
        var rowColors = new Rgba32[sweep.Width];

        // Built once per fill operation, not once per row - the transform inverse and radial
        // quadratic coefficients it holds are invariant across every row of this fill (see
        // GradientEvaluator's "Per-fill precomputation" remarks).
        var plan = GradientEvaluator.CreatePlan(paint);

        while (sweep.MoveNext(out var y, out var rowCoverage))
        {
            GradientEvaluator.EvaluateRow(in plan, y, sweep.ClipMinX, sweep.Width, rowColors);
            surface.CompositeOverSpan(y, sweep.ClipMinX, rowCoverage, rowColors, compositeWorkspace);
        }
    }

    /// <summary>
    ///     Rasterizes <paramref name="polygons"/> onto <paramref name="surface"/>, evaluating
    ///     <paramref name="paint"/> once per pixel via <see cref="TilePaintEvaluator.EvaluateRow"/>
    ///     and compositing the resulting per-pixel colors scaled by each pixel's analytically
    ///     computed fill coverage, restricted to <paramref name="clipBounds"/>.
    /// </summary>
    /// <param name="surface">The surface to composite into. Must not be null.</param>
    /// <param name="polygons">
    ///     The closed polygons to fill (typically produced by <see cref="EdgeFlattener.Flatten"/>).
    /// </param>
    /// <param name="paint">The tile paint to evaluate per pixel. Must not be null.</param>
    /// <param name="fillRule">The rule used to resolve overlapping/self-intersecting geometry.</param>
    /// <param name="clipBounds">
    ///     The region to rasterize, in the same path-space coordinates as <paramref name="polygons"/>.
    /// </param>
    /// <param name="clip">
    ///     An optional PDF clipping path coverage mask (see <see cref="ClipMask"/>) whose per-pixel
    ///     coverage is multiplied into this fill's own antialiased coverage before compositing, or
    ///     <see langword="null"/> when no clip is active. Defaults to <see langword="null"/>.
    /// </param>
    /// <remarks>
    ///     Shares its whole row-coverage computation with the constant-color and
    ///     <see cref="Fill(Surface, IReadOnlyList{List{Vector2}}, Gradient, FillRule, Rect, ClipMask?)"/>
    ///     overloads via the shared <see cref="CoverageSweep"/> helper - the only difference here
    ///     is that the final per-row compositing call first evaluates a per-pixel color row via
    ///     <see cref="TilePaintEvaluator.EvaluateRow"/>, against a <see cref="TilePaint"/> plan
    ///     built exactly once for the whole fill operation (via
    ///     <see cref="TilePaintEvaluator.CreatePlan"/>), not rebuilt on every row.
    /// </remarks>
    internal static void Fill(Surface surface, IReadOnlyList<List<Vector2>> polygons, TilePaint paint, FillRule fillRule, Rect clipBounds, ClipMask? clip = null)
    {
        ArgumentNullException.ThrowIfNull(paint);

        var sweep = new CoverageSweep(polygons, fillRule, clipBounds, clip);
        if (sweep.IsEmpty)
        {
            return;
        }

        using var compositeWorkspace = new Surface.CompositeSpanWorkspace(sweep.Width);
        var rowColors = new Rgba32[sweep.Width];

        // Built once per fill operation, not once per row - see TilePaintEvaluator's "Per-fill
        // precomputation" remarks.
        var plan = TilePaintEvaluator.CreatePlan(paint);

        while (sweep.MoveNext(out var y, out var rowCoverage))
        {
            TilePaintEvaluator.EvaluateRow(in plan, y, sweep.ClipMinX, sweep.Width, rowColors);
            surface.CompositeOverSpan(y, sweep.ClipMinX, rowCoverage, rowColors, compositeWorkspace);
        }
    }

    /// <summary>
    ///     Rasterizes <paramref name="polygons"/>'s antialiased fill coverage directly into
    ///     <paramref name="mask"/>, a dense <paramref name="maskWidth"/> x height buffer covering
    ///     an entire <see cref="Surface"/> extent - the entry point <see cref="ClipMask.FromPath"/>
    ///     uses to build a PDF clipping path's own coverage mask (PDF 32000-1 &#xA7;8.5.4), reusing
    ///     the exact same <see cref="CoverageSweep"/> row-coverage computation as every <c>Fill</c>
    ///     overload above rather than duplicating it.
    /// </summary>
    /// <param name="polygons">
    ///     The closed polygons to rasterize (typically produced by <see cref="EdgeFlattener.Flatten"/>).
    /// </param>
    /// <param name="fillRule">The rule used to resolve overlapping/self-intersecting geometry.</param>
    /// <param name="clipBounds">
    ///     The region to rasterize, in the same path-space coordinates as <paramref name="polygons"/>
    ///     - already intersected with the full <paramref name="maskWidth"/> x height mask extent by
    ///     the caller (<see cref="ClipMask.FromPath"/>), exactly as every <c>Fill</c> overload's own
    ///     <paramref name="clipBounds"/> is.
    /// </param>
    /// <param name="mask">
    ///     The dense coverage buffer to write into, row-major with the same layout as
    ///     <see cref="Surface"/>'s own pixel storage (row <c>y</c>, column <c>x</c>, at index
    ///     <c>y * maskWidth + x</c>) and already zero-initialized by the caller for every pixel
    ///     <paramref name="polygons"/> does not cover.
    /// </param>
    /// <param name="maskWidth">The full width, in pixel columns, of <paramref name="mask"/>'s own extent.</param>
    internal static void AccumulateCoverageMask(
        IReadOnlyList<List<Vector2>> polygons, FillRule fillRule, Rect clipBounds, float[] mask, int maskWidth)
    {
        var sweep = new CoverageSweep(polygons, fillRule, clipBounds);
        if (sweep.IsEmpty)
        {
            return;
        }

        while (sweep.MoveNext(out var y, out var rowCoverage))
        {
            var rowOffset = (y * maskWidth) + sweep.ClipMinX;
            rowCoverage.CopyTo(mask.AsSpan(rowOffset, rowCoverage.Length));
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
    /// <remarks>
    ///     When constructed with a non-<see langword="null"/> <c>clip</c> (see <see cref="ClipMask"/>),
    ///     every resolved row-coverage value is additionally multiplied by that clip's own
    ///     per-pixel coverage at the same device pixel, in <see cref="AccumulateRowCoverage"/> -
    ///     this is the single place a PDF clipping path (PDF 32000-1 &#xA7;8.5.4) is enforced
    ///     against painted fill coverage, regardless of which <c>Fill</c> overload or paint source
    ///     (solid color, gradient, tile) is in use.
    /// </remarks>
    private sealed class CoverageSweep
    {
        private readonly List<Edge> _edges;
        private readonly FillRule _fillRule;
        private readonly ClipMask? _clip;
        private readonly int _clipMinX;
        private readonly int _clipMaxX;
        private readonly int _clipMinY;
        private readonly int _clipMaxY;
        private readonly float[] _cover;
        private readonly float[] _area;
        private readonly float[] _rowCoverage;
        private readonly float[] _subRowCoverage;
        private readonly List<Edge> _activeEdges = [];
        private readonly List<int> _activeEdgeIds = [];
        private readonly Dictionary<int, int> _activeEdgePositions = [];
        private readonly List<int>?[] _expiringEdgeIds;
        private readonly List<RowEdge> _rowEdges = [];
        private readonly List<float> _rowBreakpoints = [];
        private readonly List<RowEdge> _subRowEdges = [];
        private int _nextEdgeIndex;
        private int _currentY;

        public CoverageSweep(IReadOnlyList<List<Vector2>> polygons, FillRule fillRule, Rect clipBounds, ClipMask? clip = null)
        {
            _fillRule = fillRule;
            _clip = clip;

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
                _subRowCoverage = [];
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
                _subRowCoverage = [];
                return;
            }

            // Dense per-row accumulators. "cover"/"area" are the shared cell accumulators every
            // active edge's row-restricted slice contributes into independently (see the
            // type-level remarks) - "cover" is sized Width + 1 for the same "harmless overflow
            // slot" reason AccumulateSingleColumn/AccumulateSlantedSpan's own bank-index clamping
            // needs. "rowCoverage" holds the final, already fill-rule-resolved coverage per
            // column, produced by the single left-to-right sweep over "cover"/"area".
            // "subRowCoverage" is used only by the rare EvenOdd multi-mid-row-breakpoint path
            // (see AccumulateRowCoverageWithSubRowSplitting) to accumulate each sub-interval's
            // own height-weighted coverage contribution before it is merged into "rowCoverage" -
            // it is never touched by the common fast path.
            _cover = new float[Width + 1];
            _area = new float[Width];
            _rowCoverage = new float[Width];
            _subRowCoverage = new float[Width];
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

                ExpireEdgesForRow(y);
                ActivateEdgesThroughRow(y, rowBottom);

                if (_activeEdges.Count == 0)
                {
                    continue;
                }

                BuildRowEdges(_activeEdges, rowTop, rowBottom, _rowEdges);
                if (_rowEdges.Count == 0)
                {
                    continue;
                }

                AccumulateRowCoverage(y, rowTop, rowBottom);

                rowCoverage = _rowCoverage;
                return true;
            }

            y = 0;
            rowCoverage = default;
            return false;
        }

        /// <summary>
        ///     Removes every active edge bucketed to expire at row <paramref name="y"/> (see the
        ///     bucketing performed by <see cref="ActivateEdgesThroughRow"/>): this touches exactly
        ///     the edges that actually expire on this row, never the whole active list, so -
        ///     unlike a full <c>activeEdges.RemoveAll(edge =&gt; edge.BottomY &lt;= rowTop)</c>
        ///     scan of every still-active edge on every row - this bookkeeping never re-examines
        ///     an edge that still has rows left to contribute to.
        /// </summary>
        /// <remarks>
        ///     Isolated from <see cref="MoveNext"/> as its own self-contained row-sweep phase,
        ///     independently nameable from edge activation and coverage accumulation.
        /// </remarks>
        private void ExpireEdgesForRow(int y)
        {
            var expiringHere = _expiringEdgeIds[y - _clipMinY];
            if (expiringHere == null)
            {
                return;
            }

            foreach (var expiredId in expiringHere)
            {
                RemoveActiveEdge(expiredId, _activeEdges, _activeEdgeIds, _activeEdgePositions);
            }
        }

        /// <summary>
        ///     Activates every edge in the sorted edge table whose top y falls before
        ///     <paramref name="rowBottom"/>, bucketing each newly-activated edge's future removal
        ///     at the earliest row it can actually be observed as expired.
        /// </summary>
        /// <remarks>
        ///     Isolated from <see cref="MoveNext"/> as its own self-contained row-sweep phase,
        ///     independently nameable from edge expiration and coverage accumulation. Removal
        ///     always happens at the <em>start</em> of a row, before this row's own additions, so
        ///     an edge just added this row cannot be examined for expiry until at least the next
        ///     row - hence the <c>y + 1</c> floor alongside the edge's own <c>BottomY</c>.
        /// </remarks>
        private void ActivateEdgesThroughRow(int y, float rowBottom)
        {
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

                var expireRow = Math.Max((int)MathF.Ceiling(edge.BottomY), y + 1);
                if (expireRow < _clipMaxY)
                {
                    var bucket = _expiringEdgeIds[expireRow - _clipMinY] ??= [];
                    bucket.Add(edgeId);
                }

                _nextEdgeIndex++;
            }
        }

        /// <summary>
        ///     Resolves this row's final <see cref="_rowCoverage"/>, dispatching to whichever of
        ///     the two coverage-resolution strategies this row and <see cref="_fillRule"/>
        ///     actually require.
        /// </summary>
        /// <param name="y">
        ///     The zero-based device row being resolved - used only to look up this row's own
        ///     <see cref="_clip"/> coverage per column, when a clip is active.
        /// </param>
        /// <param name="rowTop">This device row's top y-coordinate, in path-space units.</param>
        /// <param name="rowBottom">This device row's bottom y-coordinate, in path-space units.</param>
        /// <remarks>
        ///     <para>
        ///     <b>Why a dispatcher exists at all.</b> <see cref="ResolveCoverage"/>'s
        ///     <c>EvenOdd</c> fold (<c>magnitude % 2</c>, reflected into <c>[0, 1]</c>) is exact
        ///     only when the row's accumulated <c>total</c> reflects at most one winding-number
        ///     transition for the column it describes - true whenever at most one active edge's
        ///     <see cref="RowEdge.Y0"/>/<see cref="RowEdge.Y1"/> falls strictly inside this row
        ///     (the overwhelmingly common case: an ordinary edge either spans the row's full
        ///     height, having already started/ended in an earlier/later row, or is the one edge
        ///     that legitimately starts or stops here). When two or more edges' boundaries land
        ///     strictly inside the <em>same</em> row - for example, several nested rectangles
        ///     whose close-together top/bottom edges all collapse into one device-pixel row under
        ///     downscaling, the real-world <c>border: double</c> rendering defect this dispatch
        ///     fixes - the row's true winding number visits three or more distinct values across
        ///     its height for a shared column, and folding the row's single combined <c>total</c>
        ///     no longer equals the parity-weighted average of those values:
        ///     <c>fold(integral of w dy) != integral of parity(w(y)) dy</c> in general, though the
        ///     two sides are provably equal whenever <c>w</c> only ever takes two distinct values
        ///     across the row (the single-toggle case). <c>NonZero</c>'s resolution
        ///     (<c>min(1, abs(total))</c>) has no such restriction - it depends only on the
        ///     row-integrated magnitude, not on how many times winding number crosses parity
        ///     boundaries within the row - so it is exact for every row regardless of how many
        ///     mid-row breakpoints exist, and is therefore never routed through the sub-row
        ///     splitting path below.
        ///     </para>
        ///     <para>
        ///     <b>Dispatch condition.</b> <see cref="CollectMidRowBreakpoints"/> counts the
        ///     distinct, strictly-mid-row <see cref="RowEdge.Y0"/>/<see cref="RowEdge.Y1"/> values
        ///     this row's edges contribute. <c>NonZero</c> always takes the fast,
        ///     unmodified single-pass path (<see cref="AccumulateRowCoverageSinglePass"/>) -
        ///     unconditionally, so its output is bit-for-bit identical to before this dispatcher
        ///     existed. <c>EvenOdd</c> also takes that same fast path whenever fewer than two such
        ///     breakpoints exist, which keeps the - already proven exact, see above - common case
        ///     at its original <c>O(edges + columns)</c> cost with no behavioral change. Only
        ///     <c>EvenOdd</c> rows with two or more mid-row breakpoints fall through to
        ///     <see cref="AccumulateRowCoverageWithSubRowSplitting"/>, whose cost is
        ///     <c>O(edges + columns * breakpoints)</c> for that row alone (each of the
        ///     <c>breakpoints + 1</c> sub-intervals re-scans this row's edges and re-sweeps every
        ///     column) - acceptable because multi-toggle rows are rare by construction (most edges
        ///     are long relative to a single row's height), so this cost is paid only on the rare
        ///     rows that actually need it, never across the whole fill.
        ///     </para>
        /// </remarks>
        private void AccumulateRowCoverage(int y, float rowTop, float rowBottom)
        {
            if (_fillRule == FillRule.EvenOdd && CollectMidRowBreakpoints(rowTop, rowBottom) >= 2)
            {
                AccumulateRowCoverageWithSubRowSplitting(y, rowTop, rowBottom);
                return;
            }

            AccumulateRowCoverageSinglePass(y);
        }

        /// <summary>
        ///     Accumulates every active, row-restricted edge into the shared <c>cover</c>/<c>area</c>
        ///     cell arrays, then sweeps left to right exactly once to resolve <see cref="_rowCoverage"/>.
        /// </summary>
        /// <param name="y">
        ///     The zero-based device row being resolved - used only to look up this row's own
        ///     <see cref="_clip"/> coverage per column, when a clip is active.
        /// </param>
        /// <remarks>
        ///     The common-case fast path (see <see cref="AccumulateRowCoverage"/>'s remarks for
        ///     when it is and is not taken) - unmodified since before the sub-row-splitting fix
        ///     existed, so every row/fill-rule combination that took this path before still
        ///     produces bit-for-bit identical output.
        /// </remarks>
        private void AccumulateRowCoverageSinglePass(int y)
        {
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
            // geometry ("area[x]") is resolved to a [0, 1] coverage fraction per fill rule. When
            // a PDF clipping path (see this class's remarks and ClipMask) is active, that same
            // column's own clip coverage is then multiplied in - intersecting the two coverage
            // fractions, exactly as PDF 32000-1 8.5.4 requires painting to be restricted to the
            // current clipping path "in addition to" (not instead of) the geometry being painted.
            var accumulatedCover = 0f;
            for (var i = 0; i < Width; i++)
            {
                accumulatedCover += _cover[i];
                var total = accumulatedCover + _area[i];
                var coverage = ResolveCoverage(total, _fillRule);
                if (_clip != null)
                {
                    coverage *= _clip.GetCoverage(_clipMinX + i, y);
                }

                _rowCoverage[i] = coverage;
            }
        }

        /// <summary>
        ///     Collects, into <see cref="_rowBreakpoints"/> (sorted ascending, with near-equal
        ///     values deduplicated), every distinct y-coordinate at which some
        ///     <see cref="_rowEdges"/> entry's <see cref="RowEdge.Y0"/> or <see cref="RowEdge.Y1"/>
        ///     falls strictly inside <c>(rowTop, rowBottom)</c> - that is, every point mid-row
        ///     where some edge actually starts or stops, as opposed to merely spanning the row's
        ///     full height.
        /// </summary>
        /// <param name="rowTop">This device row's top y-coordinate, in path-space units.</param>
        /// <param name="rowBottom">This device row's bottom y-coordinate, in path-space units.</param>
        /// <returns>The number of distinct mid-row breakpoints found (0, 1, or more).</returns>
        /// <remarks>
        ///     Uses the same <see cref="NearZeroDisplacement"/> tolerance the rest of this class
        ///     uses for "is this y effectively at a given boundary" comparisons, both to decide
        ///     whether a boundary is strictly interior (excluding one that lands within rounding
        ///     distance of <paramref name="rowTop"/>/<paramref name="rowBottom"/> themselves, which
        ///     is the ordinary single-toggle case already handled correctly by the fast path) and
        ///     to merge near-duplicate breakpoints (for example, two edges both starting at
        ///     exactly the same y) into one, so a coincidence of floating-point rounding never
        ///     spuriously inflates the count past the two-breakpoint dispatch threshold or
        ///     produces a near-zero-height sub-interval below.
        /// </remarks>
        private int CollectMidRowBreakpoints(float rowTop, float rowBottom)
        {
            _rowBreakpoints.Clear();

            foreach (var edge in _rowEdges)
            {
                if (edge.Y0 > rowTop + NearZeroDisplacement && edge.Y0 < rowBottom - NearZeroDisplacement)
                {
                    _rowBreakpoints.Add(edge.Y0);
                }

                if (edge.Y1 > rowTop + NearZeroDisplacement && edge.Y1 < rowBottom - NearZeroDisplacement)
                {
                    _rowBreakpoints.Add(edge.Y1);
                }
            }

            if (_rowBreakpoints.Count < 2)
            {
                return _rowBreakpoints.Count;
            }

            _rowBreakpoints.Sort();

            var writeIndex = 1;
            for (var readIndex = 1; readIndex < _rowBreakpoints.Count; readIndex++)
            {
                if (_rowBreakpoints[readIndex] - _rowBreakpoints[writeIndex - 1] > NearZeroDisplacement)
                {
                    _rowBreakpoints[writeIndex] = _rowBreakpoints[readIndex];
                    writeIndex++;
                }
            }

            _rowBreakpoints.RemoveRange(writeIndex, _rowBreakpoints.Count - writeIndex);
            return _rowBreakpoints.Count;
        }

        /// <summary>
        ///     The rare <c>EvenOdd</c>, two-or-more-mid-row-breakpoint path (see
        ///     <see cref="AccumulateRowCoverage"/>'s remarks for why this is needed and when it is
        ///     taken): splits the row into exact sub-intervals at <see cref="_rowBreakpoints"/>,
        ///     resolves each sub-interval's own coverage independently via the existing,
        ///     unmodified <see cref="ResolveCoverage"/> fold, and sums each sub-interval's
        ///     contribution weighted by its own height (a fraction of the full row height) to
        ///     produce the row's final, exact parity-weighted average coverage per column.
        /// </summary>
        /// <param name="y">
        ///     The zero-based device row being resolved - used only to look up this row's own
        ///     <see cref="_clip"/> coverage per column, when a clip is active.
        /// </param>
        /// <param name="rowTop">This device row's top y-coordinate, in path-space units.</param>
        /// <param name="rowBottom">This device row's bottom y-coordinate, in path-space units.</param>
        /// <remarks>
        ///     <para>
        ///     <b>Why splitting at every mid-row breakpoint restores exactness.</b> By
        ///     construction, no edge starts or stops strictly inside any one of the resulting
        ///     sub-intervals - every such point was already extracted as a sub-interval boundary -
        ///     so within a single sub-interval the true winding number for any column changes at
        ///     most by the edges that continuously cross it (a slanted edge's own partial-column
        ///     crossing), never by a boundary appearing or disappearing mid-sub-interval. That is
        ///     exactly the condition <see cref="ResolveCoverage"/>'s fold is already proven exact
        ///     for (see <see cref="AccumulateRowCoverage"/>'s remarks), so applying it per
        ///     sub-interval - rather than once for the whole multi-toggle row - is correct. A
        ///     column where a slanted edge happens to cross partially within a sub-interval that
        ///     also contains an unrelated edge's own crossing is the one case this does not fully
        ///     resolve exactly; it falls back to the same already-documented, accepted
        ///     coincident/overlapping-within-one-cell approximation this class's type-level
        ///     remarks describe for the ordinary fast path, not a new trade-off introduced here,
        ///     and only applies within the already-rare compound case of "slanted edge crossing
        ///     and an unrelated boundary toggling in the very same sub-interval".
        ///     </para>
        ///     <para>
        ///     <b>Why re-clipping a <see cref="RowEdge"/> to a sub-interval is exact for slanted
        ///     edges, not just vertical ones.</b> <see cref="BuildRowEdges"/> already derives each
        ///     <see cref="RowEdge"/>'s <see cref="RowEdge.XAtY0"/>/<see cref="RowEdge.XAtY1"/> from
        ///     the same affine <c>x = TopX + Slope * (y - TopY)</c> relationship the parent
        ///     <see cref="Edge"/> holds, so <c>x</c> is linear in <c>y</c> across the whole
        ///     <c>[Y0, Y1]</c> range. <see cref="AccumulateSubInterval"/> below re-derives each
        ///     sub-interval's endpoint <c>x</c> values by linearly interpolating along that exact
        ///     same affine mapping, restricted to the sub-interval's own <c>[subTop, subBottom]</c>
        ///     - not an approximation, because a sub-range of a linear function is still exactly
        ///     that same linear function.
        ///     </para>
        ///     <para>
        ///     <b>Why dividing by sub-interval height before folding, then multiplying back, is
        ///     correct.</b> The fast path's <c>ResolveCoverage(total, fillRule)</c> implicitly
        ///     assumes <c>total</c> is the winding integral over a full unit-height row - a column
        ///     fully inside the shape across the whole row accumulates <c>deltaY</c> contributions
        ///     summing to exactly <c>1</c>. A sub-interval's own height is some fraction
        ///     <c>h &lt; 1</c> of the full row, so a column fully inside the shape across only that
        ///     sub-interval accumulates a <c>total</c> of only <c>h</c>, not <c>1</c> - dividing by
        ///     <c>h</c> first (the "density") restores the same per-unit-height scale the fold
        ///     already assumes, and multiplying the resolved <c>[0, 1]</c> fraction back by
        ///     <c>h</c> afterward converts it back into this sub-interval's own
        ///     height-proportional contribution to the full row's average - exactly the
        ///     area-weighted average the task requires.
        ///     </para>
        /// </remarks>
        private void AccumulateRowCoverageWithSubRowSplitting(int y, float rowTop, float rowBottom)
        {
            Array.Clear(_subRowCoverage);

            var previousBoundary = rowTop;
            for (var i = 0; i <= _rowBreakpoints.Count; i++)
            {
                var boundary = i < _rowBreakpoints.Count ? _rowBreakpoints[i] : rowBottom;
                AccumulateSubInterval(previousBoundary, boundary);
                previousBoundary = boundary;
            }

            for (var i = 0; i < Width; i++)
            {
                var coverage = _subRowCoverage[i];
                if (_clip != null)
                {
                    coverage *= _clip.GetCoverage(_clipMinX + i, y);
                }

                _rowCoverage[i] = coverage;
            }
        }

        /// <summary>
        ///     Accumulates one sub-row interval's own <c>EvenOdd</c> coverage - re-clipping every
        ///     <see cref="_rowEdges"/> entry to <c>[subTop, subBottom)</c>, accumulating the
        ///     restricted edges via the same unmodified <see cref="AccumulateRowEdge"/> cell
        ///     accumulation the fast path uses, resolving via the same unmodified
        ///     <see cref="ResolveCoverage"/> fold (scaled by this sub-interval's own height, see
        ///     <see cref="AccumulateRowCoverageWithSubRowSplitting"/>'s remarks), and adding the
        ///     height-weighted result into <see cref="_subRowCoverage"/>.
        /// </summary>
        /// <param name="subTop">This sub-interval's top y-coordinate, in path-space units.</param>
        /// <param name="subBottom">This sub-interval's bottom y-coordinate, in path-space units.</param>
        private void AccumulateSubInterval(float subTop, float subBottom)
        {
            var height = subBottom - subTop;
            if (height <= NearZeroDisplacement)
            {
                // A near-zero-height sub-interval (possible only from floating-point rounding
                // between adjacent breakpoints that CollectMidRowBreakpoints' deduplication did
                // not fully merge) contributes a negligible area-weighted share - skipping it also
                // avoids dividing by a near-zero height below.
                return;
            }

            _subRowEdges.Clear();
            foreach (var edge in _rowEdges)
            {
                var oy0 = Math.Max(edge.Y0, subTop);
                var oy1 = Math.Min(edge.Y1, subBottom);
                if (oy0 >= oy1)
                {
                    continue;
                }

                // Linearly interpolate this edge's x at the sub-interval's own restricted y
                // range, along the exact same affine x(y) mapping BuildRowEdges already derived
                // this RowEdge's own XAtY0/XAtY1 from - exact for slanted edges, not merely
                // vertical ones (see this method's remarks).
                var edgeHeight = edge.Y1 - edge.Y0;
                var xAtOy0 = edge.XAtY0 + ((oy0 - edge.Y0) / edgeHeight * (edge.XAtY1 - edge.XAtY0));
                var xAtOy1 = edge.XAtY0 + ((oy1 - edge.Y0) / edgeHeight * (edge.XAtY1 - edge.XAtY0));
                _subRowEdges.Add(new RowEdge(oy0, oy1, xAtOy0, xAtOy1, edge.Direction));
            }

            if (_subRowEdges.Count == 0)
            {
                return;
            }

            Array.Clear(_cover);
            Array.Clear(_area);
            foreach (var edge in _subRowEdges)
            {
                AccumulateRowEdge(edge, _clipMinX, _clipMaxX, _cover, _area);
            }

            var accumulatedCover = 0f;
            for (var i = 0; i < Width; i++)
            {
                accumulatedCover += _cover[i];
                var total = accumulatedCover + _area[i];
                var density = total / height;
                var coverage = ResolveCoverage(density, FillRule.EvenOdd);
                _subRowCoverage[i] += coverage * height;
            }
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
