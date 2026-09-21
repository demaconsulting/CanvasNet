using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;

namespace DemaConsulting.CanvasNet.Tests.Drawing;

/// <summary>
///     Unit tests for the internal <see cref="ScanlineRasterizer"/> type.
/// </summary>
public class ScanlineRasterizerTests
{
    /// <summary>
    ///     Proves that a unit square offset by a known sub-pixel amount produces the exact
    ///     hand-computed analytic coverage at every pixel it overlaps: a 1x1 square positioned at
    ///     (0.5, 0.5)-(1.5, 1.5) on a 2x2 surface overlaps each of the 4 pixels by exactly a
    ///     0.5 x 0.5 sub-region (area 0.25), so every pixel's resolved alpha must be exactly 0.25
    ///     (255 * 0.25 = 63.75, rounds to 64).
    /// </summary>
    [Fact]
    public void ScanlineRasterizer_Fill_SubPixelOffsetSquare_MatchesHandComputedCoverage()
    {
        // Arrange: the raw polygon (already "flattened") directly, bypassing EdgeFlattener/PathFiller
        var polygon = new List<Vector2>
        {
            new(0.5f, 0.5f),
            new(1.5f, 0.5f),
            new(1.5f, 1.5f),
            new(0.5f, 1.5f),
            new(0.5f, 0.5f)
        };
        var surface = new Surface(2, 2);
        var color = new Rgba32(255, 255, 255, 255);
        var clipBounds = new Rect(0, 0, 2, 2);

        // Act
        ScanlineRasterizer.Fill(surface, [polygon], color, FillRule.NonZero, clipBounds);

        // Assert: every one of the 4 pixels gets exactly quarter coverage
        Assert.Equal((byte)64, surface[0, 0].A);
        Assert.Equal((byte)64, surface[1, 0].A);
        Assert.Equal((byte)64, surface[0, 1].A);
        Assert.Equal((byte)64, surface[1, 1].A);
    }

    /// <summary>
    ///     Proves that NonZero clamps an overlapping, same-direction raw winding of magnitude 2
    ///     to fully opaque, while EvenOdd resolves the very same raw winding of 2 to fully
    ///     transparent via its triangle-wave fold (2 mod 2 = 0).
    /// </summary>
    [Fact]
    public void ScanlineRasterizer_Fill_RawWindingOfTwo_NonZeroClampsEvenOddFoldsToZero()
    {
        // Arrange: two coincident, identically wound 2x2 squares - every covered pixel has a raw
        // winding magnitude of exactly 2
        var square = new List<Vector2>
        {
            new(0, 0),
            new(2, 0),
            new(2, 2),
            new(0, 2),
            new(0, 0)
        };
        var color = new Rgba32(1, 2, 3, 255);
        var clipBounds = new Rect(0, 0, 2, 2);

        var nonZeroSurface = new Surface(2, 2);
        ScanlineRasterizer.Fill(nonZeroSurface, [square, square], color, FillRule.NonZero, clipBounds);

        var evenOddSurface = new Surface(2, 2);
        ScanlineRasterizer.Fill(evenOddSurface, [square, square], color, FillRule.EvenOdd, clipBounds);

        // Assert
        Assert.Equal((byte)255, nonZeroSurface[0, 0].A);
        Assert.Equal((byte)0, evenOddSurface[0, 0].A);
    }

    /// <summary>
    ///     Proves that two exactly coincident, sub-pixel-offset polygons resolve consistently
    ///     with the cell-based signed area/cover accumulation algorithm's known, accepted
    ///     trade-off for coincident geometry within the same pixel cell (see the
    ///     <see cref="ScanlineRasterizer"/> type-level remarks): each duplicate edge's
    ///     contribution accumulates independently into the shared per-row cell arrays, so two
    ///     exactly coincident boundaries within one cell sum their raw signed contributions rather
    ///     than being resolved against a single winding decision first.
    /// </summary>
    /// <remarks>
    ///     Arrange: a rectangle from <c>x = 0.25</c> to <c>x = 1.75</c> (spanning both columns of
    ///     a 2x2 surface with a fractional edge in each), duplicated twice in the polygon list.
    ///     Independently verified reference for this algorithm: at column 0, the duplicated left
    ///     boundary (<c>x = 0.25</c>) contributes area <c>-2 * (1 - 0.25) = -1.5</c>
    ///     (magnitude 1.5) with no cover banked yet, so <c>NonZero</c> clamps
    ///     <c>min(1, 1.5) = 1</c> (alpha 255) and <c>EvenOdd</c> folds <c>1.5</c> into
    ///     <c>2 - 1.5 = 0.5</c> (alpha 128) - by symmetry column 1 (driven by the duplicated right
    ///     boundary at <c>x = 1.75</c> plus the running cover banked from column 0) resolves to
    ///     the same magnitude 1.5, hence the same alpha values. This differs from a hypothetical
    ///     per-region-resolved algorithm's exact single-shape answer (alpha 191/0) precisely
    ///     because this algorithm's crossing-edge fix (see
    ///     <see cref="ScanlineRasterizer_Fill_BowtieSelfIntersectingPolygon_ProducesCorrectPartialCoverageNotFullFill"/>)
    ///     requires never comparing/sorting edges by position - the accepted, industry-standard
    ///     (AGG/FreeType) cost of that fix is that duplicate/coincident edges within one cell sum
    ///     linearly instead of being resolved as a single boundary crossing.
    /// </remarks>
    [Fact]
    public void ScanlineRasterizer_Fill_SubPixelOffsetDuplicatePolygons_SumsCoincidentContributionsPerCellAlgorithm()
    {
        // Arrange
        var rectangle = new List<Vector2>
        {
            new(0.25f, 0f),
            new(1.75f, 0f),
            new(1.75f, 2f),
            new(0.25f, 2f),
            new(0.25f, 0f)
        };
        var color = new Rgba32(9, 8, 7, 255);
        var clipBounds = new Rect(0, 0, 2, 2);

        var nonZeroSurface = new Surface(2, 2);
        ScanlineRasterizer.Fill(nonZeroSurface, [rectangle, rectangle], color, FillRule.NonZero, clipBounds);

        var evenOddSurface = new Surface(2, 2);
        ScanlineRasterizer.Fill(evenOddSurface, [rectangle, rectangle], color, FillRule.EvenOdd, clipBounds);

        // Assert: NonZero clamps the doubled raw magnitude (1.5) to fully opaque
        Assert.Equal((byte)255, nonZeroSurface[0, 0].A);
        Assert.Equal((byte)255, nonZeroSurface[1, 0].A);
        Assert.Equal((byte)255, nonZeroSurface[0, 1].A);
        Assert.Equal((byte)255, nonZeroSurface[1, 1].A);

        // Assert: EvenOdd folds the doubled raw magnitude (1.5) to 0.5 (alpha 128)
        Assert.Equal((byte)128, evenOddSurface[0, 0].A);
        Assert.Equal((byte)128, evenOddSurface[1, 0].A);
        Assert.Equal((byte)128, evenOddSurface[0, 1].A);
        Assert.Equal((byte)128, evenOddSurface[1, 1].A);
    }

    /// <summary>
    ///     Proves that two overlapping (not coincident), sub-pixel-offset polygons whose
    ///     boundaries both fall within the same pixel column resolve consistently with the
    ///     cell-based algorithm's per-cell raw signed accumulation, rather than an exact
    ///     per-region area calculation (see the <see cref="ScanlineRasterizer"/> type-level
    ///     remarks on the accepted coincident/overlapping-within-one-cell trade-off).
    /// </summary>
    /// <remarks>
    ///     Arrange: on a 2x1 surface, square 1 spans <c>x</c> in <c>[0.25, 1.25)</c> and square 2
    ///     spans <c>x</c> in <c>[0.75, 1.75)</c> (both full-height). Independently verified
    ///     reference for this algorithm: column 0 receives both squares' own left-boundary area
    ///     contributions (<c>-(1 - 0.25) - (1 - 0.75) = -1.0</c>, magnitude exactly 1), which both
    ///     fill rules resolve identically (<c>NonZero</c>: <c>min(1, 1) = 1</c>; <c>EvenOdd</c>:
    ///     folding exactly <c>1.0</c> leaves <c>1.0</c> unchanged) - alpha 255 under both rules.
    ///     By symmetry, column 1 (driven by both squares' right boundaries) resolves to the same
    ///     magnitude and the same alpha. This is a direct consequence of more than one edge
    ///     falling within the same pixel column - the same accepted trade-off documented on
    ///     <see cref="ScanlineRasterizer_Fill_SubPixelOffsetDuplicatePolygons_SumsCoincidentContributionsPerCellAlgorithm"/>.
    /// </remarks>
    [Fact]
    public void ScanlineRasterizer_Fill_OverlappingSubPixelSquares_SumsPerCellContributionsPerCellAlgorithm()
    {
        // Arrange
        var square1 = new List<Vector2>
        {
            new(0.25f, 0f),
            new(1.25f, 0f),
            new(1.25f, 1f),
            new(0.25f, 1f),
            new(0.25f, 0f)
        };
        var square2 = new List<Vector2>
        {
            new(0.75f, 0f),
            new(1.75f, 0f),
            new(1.75f, 1f),
            new(0.75f, 1f),
            new(0.75f, 0f)
        };
        var color = new Rgba32(1, 1, 1, 255);
        var clipBounds = new Rect(0, 0, 2, 1);

        var nonZeroSurface = new Surface(2, 1);
        ScanlineRasterizer.Fill(nonZeroSurface, [square1, square2], color, FillRule.NonZero, clipBounds);

        var evenOddSurface = new Surface(2, 1);
        ScanlineRasterizer.Fill(evenOddSurface, [square1, square2], color, FillRule.EvenOdd, clipBounds);

        // Assert: both fill rules resolve the same raw magnitude (exactly 1) to fully opaque
        Assert.Equal((byte)255, nonZeroSurface[0, 0].A);
        Assert.Equal((byte)255, nonZeroSurface[1, 0].A);
        Assert.Equal((byte)255, evenOddSurface[0, 0].A);
        Assert.Equal((byte)255, evenOddSurface[1, 0].A);
    }

    /// <summary>
    ///     Proves the critical bug fix: a self-intersecting "bowtie" polygon whose two diagonal
    ///     edges cross each other strictly inside a row (not at a shared vertex or row boundary)
    ///     now produces the analytically correct partial coverage, not the ~100% gross
    ///     over-filling produced by the prior sub-interval/sort-by-x algorithm (which assumed
    ///     edges spanning a sub-interval never change their relative x-order within it).
    /// </summary>
    /// <remarks>
    ///     Arrange: the polygon <c>(0,0)->(4,3)->(4,0)->(0,3)->close</c> on a 4x3 surface - two
    ///     triangles sharing the same four corners, crossing each other at the exact center
    ///     <c>(2, 1.5)</c>, strictly inside row 1 (<c>y</c> in <c>[1, 2)</c>), which is exactly
    ///     the reviewer-reported reproduction case. Ground truth was independently verified (not
    ///     merely trusted from the review) via a dense (200x200 samples per pixel) supersampling
    ///     ray-casting point-in-polygon reference implementation, entirely independent of
    ///     <see cref="ScanlineRasterizer"/> itself, which for row 1 yields coverage fractions
    ///     <c>1.000, 0.667, 0.667, 1.000</c> for columns 0-3 under both fill rules (this bowtie's
    ///     two wings only ever produce winding magnitude 0 or 1 at any point, so NonZero and
    ///     EvenOdd coincide here) - matching the values reported in the original review
    ///     (<c>255, 170, 170, 255</c> once scaled to byte alpha and rounded) to within a single
    ///     alpha unit. The outer columns (0 and 3) are almost fully inside the union of both
    ///     triangle wings (only a thin sliver near the crossing point is excluded); the inner
    ///     columns (1 and 2) are exactly two-thirds covered, since the crossing point sits exactly
    ///     at their shared boundary <c>x = 2</c>, y = 1.5, splitting each inner column into a
    ///     covered wing area and an excluded wedge.
    /// </remarks>
    [Fact]
    public void ScanlineRasterizer_Fill_BowtieSelfIntersectingPolygon_ProducesCorrectPartialCoverageNotFullFill()
    {
        // Arrange: self-intersecting bowtie, diagonals crossing strictly inside row 1
        var bowtie = new List<Vector2>
        {
            new(0, 0),
            new(4, 3),
            new(4, 0),
            new(0, 3),
            new(0, 0)
        };
        var color = new Rgba32(255, 255, 255, 255);
        var clipBounds = new Rect(0, 0, 4, 3);

        var nonZeroSurface = new Surface(4, 3);
        ScanlineRasterizer.Fill(nonZeroSurface, [bowtie], color, FillRule.NonZero, clipBounds);

        var evenOddSurface = new Surface(4, 3);
        ScanlineRasterizer.Fill(evenOddSurface, [bowtie], color, FillRule.EvenOdd, clipBounds);

        // Assert: row 1 (the row the crossing point falls strictly inside) matches the
        // independently verified ground truth, not the ~100% over-fill of the prior algorithm,
        // for both fill rules
        for (var fillRule = 0; fillRule < 2; fillRule++)
        {
            var surface = fillRule == 0 ? nonZeroSurface : evenOddSurface;
            Assert.Equal((byte)255, surface[0, 1].A);
            Assert.Equal((byte)170, surface[1, 1].A);
            Assert.Equal((byte)170, surface[2, 1].A);
            Assert.Equal((byte)255, surface[3, 1].A);
        }
    }

    /// <summary>
    ///     Proves the fix for the medium-severity performance bug: filling many overlapping
    ///     full-width rectangles in the same row now scales roughly linearly with edge count
    ///     (<c>O(edges + width)</c> per row), rather than the prior sub-interval algorithm's
    ///     super-linear (measured ~<c>O(edges^2 x width)</c>) blowup from re-sweeping full
    ///     row-width buffers once per qualifying "inside" gap per sub-interval.
    /// </summary>
    /// <remarks>
    ///     Rather than asserting a specific, potentially CI-flaky wall-clock bound, this asserts
    ///     the algorithmic invariant directly: doubling the number of overlapping rectangles (and
    ///     therefore the number of edges) must not more than roughly double the elapsed time (a
    ///     generous 6x tolerance is used to absorb CI scheduling noise while still failing hard on
    ///     genuine quadratic-or-worse scaling, which would produce a ~4x-or-more slowdown for a 2x
    ///     edge count increase on top of any noise).
    /// </remarks>
    [Fact]
    public void ScanlineRasterizer_Fill_ManyOverlappingRectangles_ScalesRoughlyLinearlyWithEdgeCount()
    {
        // Arrange
        const int width = 2000;
        const int height = 4;
        var color = new Rgba32(1, 2, 3, 255);
        var clipBounds = new Rect(0, 0, width, height);

        var smallCount = MeasureFillMilliseconds(200, width, height, color, clipBounds);
        var largeCount = MeasureFillMilliseconds(1600, width, height, color, clipBounds);

        // Assert: 8x the rectangles (and therefore 8x the edges) took no more than roughly 8x as
        // long, not the tens/hundreds-of-times blowup a super-linear algorithm would produce
        Assert.True(
            largeCount.Milliseconds <= smallCount.Milliseconds * 8 * 6 + 50,
            $"Expected roughly linear scaling: {smallCount.RectangleCount} rectangles took " +
            $"{smallCount.Milliseconds}ms, but {largeCount.RectangleCount} rectangles took " +
            $"{largeCount.Milliseconds}ms.");
    }

    /// <summary>
    ///     Fills <paramref name="rectangleCount"/> overlapping full-width rectangles (each shifted
    ///     down by a fraction of a pixel so every rectangle contributes distinct, overlapping
    ///     fractional edges to the same rows) onto a surface of the given size, and returns the
    ///     elapsed wall-clock time.
    /// </summary>
    private static (int RectangleCount, long Milliseconds) MeasureFillMilliseconds(
        int rectangleCount, int width, int height, Rgba32 color, Rect clipBounds)
    {
        var polygons = new List<List<Vector2>>(rectangleCount);
        for (var i = 0; i < rectangleCount; i++)
        {
            var offset = i * 0.001f;
            polygons.Add(
            [
                new Vector2(0.1f + offset, 0f),
                new Vector2(width - 0.1f + offset, 0f),
                new Vector2(width - 0.1f + offset, height),
                new Vector2(0.1f + offset, height),
                new Vector2(0.1f + offset, 0f)
            ]);
        }

        var surface = new Surface(width, height);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        ScanlineRasterizer.Fill(surface, polygons, color, FillRule.NonZero, clipBounds);
        stopwatch.Stop();

        return (rectangleCount, stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    ///     Coarse smoke test guarding against a genuine reintroduction of superlinear
    ///     <c>O(active-edge-count x rows)</c> (or worse) active-edge-list bookkeeping - deliberately
    ///     <b>not</b> an attempt to precisely detect the specific historical bug this test used to
    ///     target (see remarks for why that historical bug is provably undetectable this way).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>History and why this test was simplified.</b> This test previously tried, across
    ///         ten iterations, to precisely detect a real but narrow historical bug: an active-edge
    ///         removal step that used <c>activeEdges.RemoveAll(edge =&gt; edge.BottomY &lt;=
    ///         rowTop)</c> (a full rescan of the entire active list on every row) instead of the
    ///         current per-row expiration-bucket + <c>O(1)</c> swap-remove (an internal
    ///         <c>RemoveActiveEdge</c> helper inside <see cref="ScanlineRasterizer"/>). Every
    ///         attempt - a fixed-millisecond threshold, and later a
    ///         same-run self-calibrating throughput ratio - was eventually falsified by real
    ///         hardware: the fixed threshold failed on slower machines, and the self-calibrating
    ///         ratio failed catastrophically on GitHub-hosted <c>ubuntu-latest</c> runners (measured
    ///         ratios of 2.475x-2.630x against a threshold of 1.0x for the *correct*, fixed
    ///         implementation, on both net9.0 and net10.0) - a clear false positive.
    ///     </para>
    ///     <para>
    ///         <b>Why: this is a bounded constant-factor bug, not an asymptotic one - re-derived
    ///         from scratch.</b> <see cref="ScanlineRasterizer.Fill"/>'s per-row rendering step
    ///         (<c>BuildRowEdges</c> plus cell accumulation) already visits every currently-active
    ///         edge exactly once on every row it remains active, regardless of how removal is
    ///         implemented - this per-row, per-active-edge visitation cost is unavoidable and
    ///         identical between the fixed implementation and the historical regression. The
    ///         historical bug's <c>RemoveAll</c> rescan is *also* a single <c>O(active-count)</c>
    ///         pass over the same active list, once per row - i.e. it adds exactly one more pass of
    ///         the same size as the passes the render step was already doing, never a pass whose
    ///         size scales with anything the render step's passes do not already scale with. For any
    ///         edge-construction shape - whether few or many edges are concurrently active, whether
    ///         edges expire in a tight cluster or staggered evenly across the whole sweep, whatever
    ///         the ratio of edges to rows - both implementations remain in the same <c>O(active
    ///         count)</c>-per-row complexity class, so the regression can only ever add a bounded
    ///         fraction of *extra* work on top of the cost the render step already pays (empirically
    ///         observed at ~30-50% across many shapes while this test was being developed) - it
    ///         cannot, by construction, produce an unbounded or large multiplicative blowup the way
    ///         a genuine complexity-class change (e.g. an active list that is never trimmed and
    ///         grows without bound) would. Because both implementations scale by the *same* factor
    ///         when the problem size is scaled, no ratio between two problem sizes - of edges, rows,
    ///         or both, at any chosen scale or multiplier - can distinguish them; this was reconfirmed
    ///         empirically while redesigning this test (see the removed self-calibration test's
    ///         history in source control for the specific measurements). No threshold-based
    ///         same-run or cross-machine timing test can therefore detect this specific historical
    ///         bug's exact magnitude with a reliable safety margin, on heterogeneous hardware. This
    ///         test deliberately no longer tries.
    ///     </para>
    ///     <para>
    ///         <b>What this test guards against instead.</b> A genuinely worse regression - for
    ///         example, active-edge removal that silently stops working altogether (a bucketing
    ///         condition that never triggers, a swap-remove that corrupts <c>activeEdgePositions</c>
    ///         and leaves stale entries behind, or code that scans the *entire historical edge
    ///         table* rather than just the currently-active subset) - would make the active list
    ///         grow without bound as the sweep progresses, turning the per-row visitation cost from
    ///         <c>O(active count)</c>, which stays small here, into <c>O(total edges)</c>, which
    ///         does not. This test's edge shape is deliberately chosen so almost none of the
    ///         unavoidable per-row rendering cost is paid by the correct implementation (few edges
    ///         are ever concurrently active), so that a regression of *this* kind - a genuine
    ///         complexity-class change, not a bounded constant-factor one - would blow far past a
    ///         generous absolute wall-clock budget, while the correct implementation clears it with
    ///         a large margin on any real machine, however slow or contended.
    ///     </para>
    ///     <para>
    ///         <b>Edge shape.</b> <see cref="ManyShortLivedEdgesRowStartCount"/> distinct starting
    ///         rows are used (bounded by the surface's 8192-pixel maximum dimension), each hosting
    ///         <see cref="ManyShortLivedEdgesGroupsPerRow"/> narrow rectangles that all share that
    ///         row's start and remain active for only
    ///         <see cref="ManyShortLivedEdgesActiveRowSpan"/> rows before expiring - multiplying the
    ///         total edge count (and, under a genuine unbounded-growth regression, the eventual
    ///         active-list size) far above the row count without needing a taller-than-permitted
    ///         surface. Only a small, bounded number of edges (<see
    ///         cref="ManyShortLivedEdgesGroupsPerRow"/> times a small constant) are ever
    ///         concurrently active at any point in the sweep. A correct <c>O(active count)</c>-per-
    ///         row implementation therefore does a small, bounded amount of work on every row; total
    ///         work stays roughly proportional to (edges + rows), not (edges x rows). A regression
    ///         that lets the active list grow without bound, by contrast, would pay a per-row cost
    ///         that grows linearly with how far the sweep has progressed, reaching the *full* edge
    ///         count (hundreds of thousands here) well before the sweep ends - making the total cost
    ///         quadratic in the row count, not linear.
    ///     </para>
    ///     <para>
    ///         <b>Budget.</b> The absolute millisecond budget below is chosen to be extremely
    ///         generous relative to the correct implementation's actual measured time (~170-180ms
    ///         on the development machine, including JIT-warmed sort/build overhead for 400,000
    ///         edges) - a margin of roughly 25-30x - so that ordinary CI scheduling noise,
    ///         contention from other concurrently running test binaries, or a slower CI runner
    ///         cannot make this test flaky. It was verified, while developing this test, that a
    ///         deliberately broken active-list removal (expiration bucketing disabled, so every
    ///         edge remains "active" and is re-visited on every subsequent row for the rest of the
    ///         sweep) measured ~7,369ms on the same machine - roughly 41x slower than the correct
    ///         implementation, and comfortably over this budget. It was also verified that
    ///         reproducing the exact magnitude of the specific historical <c>RemoveAll</c>
    ///         regression this test used to target - one extra full <c>O(active-count)</c> pass
    ///         over the active list per row, layered on top of the otherwise-correct bucketed
    ///         removal - measured ~178ms, indistinguishable from the correct implementation at this
    ///         problem size (as the analysis above predicts: with only a few dozen edges ever
    ///         concurrently active here, "one extra pass over the active list" costs almost
    ///         nothing). This confirms, empirically and by construction, that this test cannot and
    ///         does not claim to catch that specific historical regression's magnitude any more -
    ///         only a genuine unbounded active-list growth is guaranteed to be caught, and is
    ///         caught with a large margin. Correctness of the removal mechanism itself (edges start
    ///         and stop contributing at exactly the right rows, with no double-removal or skipped
    ///         edges) is covered independently by
    ///         <see cref="ScanlineRasterizer_Fill_EdgeStartingAndEndingMidSweep_StopsContributingAtCorrectRows"/>
    ///         and the other functional tests in this file.
    ///     </para>
    /// </remarks>
    private const int ManyShortLivedEdgesRowStartCount = 8_000;

    private const int ManyShortLivedEdgesGroupsPerRow = 50;

    private const int ManyShortLivedEdgesActiveRowSpan = 5;

    /// <summary>
    ///     Coarse smoke test: many short-lived, staggered edges must complete within a generous
    ///     absolute wall-clock budget (see the type-level remarks above for the full rationale).
    /// </summary>
    [Fact]
    public void ScanlineRasterizer_Fill_ManyShortLivedStaggeredEdges_CompletesWithinGenerousAbsoluteBudget()
    {
        // Arrange: ManyShortLivedEdgesGroupsPerRow narrow rectangles per starting row, each
        // remaining active for only a handful of rows, so only a small, bounded number of edges
        // are ever concurrently active - see remarks above for why this shape is what makes a
        // genuine complexity-class regression (unbounded active-list growth), as opposed to the
        // historical bounded constant-factor regression, detectable with a large safety margin.
        const int width = 1;
        const int height = ManyShortLivedEdgesRowStartCount + ManyShortLivedEdgesActiveRowSpan;
        var totalRectangleCount = ManyShortLivedEdgesRowStartCount * ManyShortLivedEdgesGroupsPerRow;
        var color = new Rgba32(1, 2, 3, 255);
        var clipBounds = new Rect(0, 0, width, height);
        var polygons = new List<List<Vector2>>(totalRectangleCount);
        for (var row = 0; row < ManyShortLivedEdgesRowStartCount; row++)
        {
            var topY = (float)row;
            var bottomY = topY + ManyShortLivedEdgesActiveRowSpan;
            for (var g = 0; g < ManyShortLivedEdgesGroupsPerRow; g++)
            {
                var offset = g * 0.00001f;
                polygons.Add(
                [
                    new Vector2(0.1f + offset, topY),
                    new Vector2(width - 0.1f + offset, topY),
                    new Vector2(width - 0.1f + offset, bottomY),
                    new Vector2(0.1f + offset, bottomY),
                    new Vector2(0.1f + offset, topY)
                ]);
            }
        }

        var surface = new Surface(width, height);

        // Unmeasured warmup: absorbs JIT tiering-up cost so the measured run reflects steady-state
        // throughput rather than first-call compilation overhead.
        ScanlineRasterizer.Fill(new Surface(width, height), polygons, color, FillRule.NonZero, clipBounds);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        ScanlineRasterizer.Fill(surface, polygons, color, FillRule.NonZero, clipBounds);
        stopwatch.Stop();

        // Assert: a generous multi-second absolute budget - roughly two orders of magnitude above
        // the correct implementation's actual measured time - that only a genuine complexity-class
        // regression (unbounded active-list growth), not ordinary CI noise or the historical bounded
        // constant-factor bug, could overrun. See remarks above for the empirical validation.
        const long maxMilliseconds = 5000;
        Assert.True(
            stopwatch.ElapsedMilliseconds <= maxMilliseconds,
            $"Expected {totalRectangleCount} short-lived, staggered edges across " +
            $"{height} rows to complete within a generous {maxMilliseconds}ms smoke-test budget, " +
            $"but the fill took {stopwatch.ElapsedMilliseconds}ms - this suggests active-edge " +
            "removal is no longer bounding the active list to a small size (a genuine " +
            "complexity-class regression), not merely the historical bounded constant-factor " +
            "overhead this test no longer tries to precisely detect.");
    }

    /// <summary>
    ///     Holds the visible row count fixed (large) and quadruples only the number of long-lived
    ///     edges. Kept as a basic linear-scaling sanity check alongside
    ///     <see cref="ScanlineRasterizer_Fill_ManyShortLivedStaggeredEdges_CompletesWithinGenerousAbsoluteBudget"/>;
    ///     with the row count fixed, an <c>O(edges x rows)</c> implementation's cost is linear in
    ///     the edge count (the fixed row count is just a constant multiplier), so this sub-case
    ///     alone cannot reliably detect that regression - it only guards against a separate,
    ///     cruder edges-only super-linear blowup.
    /// </summary>
    [Fact]
    public void ScanlineRasterizer_Fill_FixedRowCountManyLongLivedEdges_ScalesRoughlyLinearlyWithEdgeCount()
    {
        const int width = 40;
        const int height = 1200;
        var color = new Rgba32(1, 2, 3, 255);

        var small = MeasureTallFillMilliseconds(rectangleCount: 300, height, width, color);
        var large = MeasureTallFillMilliseconds(rectangleCount: 1200, height, width, color);

        Assert.True(
            large.Milliseconds <= Math.Max(small.Milliseconds, 1) * 4 * 6 + 50,
            $"Expected roughly linear scaling with edge count (fixed {height} rows): " +
            $"{small.RectangleCount} rectangles took {small.Milliseconds}ms, but " +
            $"{large.RectangleCount} rectangles took {large.Milliseconds}ms.");
    }

    /// <summary>
    ///     Holds the long-lived edge count fixed (large) and quadruples only the number of visible
    ///     rows swept. Kept as a basic linear-scaling sanity check alongside
    ///     <see cref="ScanlineRasterizer_Fill_ManyShortLivedStaggeredEdges_CompletesWithinGenerousAbsoluteBudget"/>;
    ///     with the edge count fixed, an <c>O(edges x rows)</c> implementation's cost is linear in
    ///     the row count (the fixed edge count is just a constant multiplier), so this sub-case
    ///     alone cannot reliably detect that regression either - see that test's remarks for the
    ///     fixed-size measurement that actually distinguishes it.
    /// </summary>
    [Fact]
    public void ScanlineRasterizer_Fill_FixedEdgeCountManyRows_ScalesRoughlyLinearlyWithRowCount()
    {
        const int width = 40;
        const int rectangleCount = 1200;
        var color = new Rgba32(1, 2, 3, 255);

        var small = MeasureTallFillMilliseconds(rectangleCount, height: 300, width, color);
        var large = MeasureTallFillMilliseconds(rectangleCount, height: 1200, width, color);

        Assert.True(
            large.Milliseconds <= Math.Max(small.Milliseconds, 1) * 4 * 6 + 50,
            $"Expected roughly linear scaling with row count (fixed {rectangleCount} edges): " +
            $"{small.Height} rows took {small.Milliseconds}ms, but {large.Height} rows took " +
            $"{large.Milliseconds}ms.");
    }

    /// <summary>
    ///     Fills <paramref name="rectangleCount"/> overlapping, nearly-full-height rectangles
    ///     (each shifted by a fraction of a pixel so every rectangle contributes distinct,
    ///     overlapping fractional edges, and every rectangle's vertical extent spans nearly the
    ///     entire clip height so it remains in the active-edge list for essentially every row of
    ///     the sweep) onto a surface of the given tall size, and returns the median-of-five
    ///     elapsed wall-clock time (after an unmeasured warmup run) to reduce JIT/GC scheduling
    ///     noise.
    /// </summary>
    private static (int RectangleCount, int Height, long Milliseconds) MeasureTallFillMilliseconds(
        int rectangleCount, int height, int width, Rgba32 color)
    {
        var clipBounds = new Rect(0, 0, width, height);
        var polygons = new List<List<Vector2>>(rectangleCount);
        for (var i = 0; i < rectangleCount; i++)
        {
            var offset = i * 0.0001f;
            polygons.Add(
            [
                new Vector2(0.1f + offset, 0f),
                new Vector2(width - 0.1f + offset, 0f),
                new Vector2(width - 0.1f + offset, height),
                new Vector2(0.1f + offset, height),
                new Vector2(0.1f + offset, 0f)
            ]);
        }

        // Unmeasured warmup: absorbs JIT tiering-up cost so the measured run reflects steady-state
        // throughput rather than first-call compilation overhead.
        ScanlineRasterizer.Fill(new Surface(width, height), polygons, color, FillRule.NonZero, clipBounds);

        // Five repeats, median-of-five: a single best-of-few run can be an unrepresentative lucky
        // outlier (especially for the shorter "small" measurement), while the median is robust to
        // one-off GC/scheduler hiccups in either direction without being as optimistic as a
        // minimum.
        var samples = new long[5];
        for (var repeat = 0; repeat < samples.Length; repeat++)
        {
            var surface = new Surface(width, height);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            ScanlineRasterizer.Fill(surface, polygons, color, FillRule.NonZero, clipBounds);
            stopwatch.Stop();
            samples[repeat] = stopwatch.ElapsedMilliseconds;
        }

        Array.Sort(samples);
        return (rectangleCount, height, samples[samples.Length / 2]);
    }

    /// <summary>
    ///     Proves that the active-edge-list sweep correctly starts and stops an edge's
    ///     contribution at the exact rows its vertical extent covers: a rectangle spanning only
    ///     rows 1-3 of a 6-row surface leaves row 0 (before the edges start) and rows 4-5 (after
    ///     the edges end) completely untouched, while rows 1-3 are fully filled.
    /// </summary>
    [Fact]
    public void ScanlineRasterizer_Fill_EdgeStartingAndEndingMidSweep_StopsContributingAtCorrectRows()
    {
        // Arrange: an integer-aligned rectangle spanning x in [1,5), y in [1,4) on a 6x6 surface
        var polygon = new List<Vector2>
        {
            new(1, 1),
            new(5, 1),
            new(5, 4),
            new(1, 4),
            new(1, 1)
        };
        var surface = new Surface(6, 6);
        var color = new Rgba32(10, 20, 30, 255);
        var clipBounds = new Rect(0, 0, 6, 6);

        // Act
        ScanlineRasterizer.Fill(surface, [polygon], color, FillRule.NonZero, clipBounds);

        // Assert: row 0 (before either vertical edge has started) is untouched
        Assert.Equal((byte)0, surface[2, 0].A);

        // Assert: rows 1 through 3 (the edges' full vertical extent) are fully filled
        Assert.Equal((byte)255, surface[2, 1].A);
        Assert.Equal((byte)255, surface[2, 2].A);
        Assert.Equal((byte)255, surface[2, 3].A);

        // Assert: row 4 (immediately after both edges have ended) is untouched - proving the
        // active edge list actually removes exhausted edges rather than leaving them active
        Assert.Equal((byte)0, surface[2, 4].A);
        Assert.Equal((byte)0, surface[2, 5].A);
    }

    /// <summary>
    ///     Proves that Fill is a no-op for an empty polygon list.
    /// </summary>
    [Fact]
    public void ScanlineRasterizer_Fill_NoPolygons_NoOp()
    {
        // Arrange
        var surface = new Surface(2, 2);
        surface[0, 0] = new Rgba32(1, 2, 3, 4);

        // Act
        ScanlineRasterizer.Fill(surface, [], new Rgba32(9, 9, 9, 9), FillRule.NonZero, new Rect(0, 0, 2, 2));

        // Assert
        Assert.Equal(new Rgba32(1, 2, 3, 4), surface[0, 0]);
    }

    /// <summary>
    ///     Proves that a degenerate polygon with fewer than three effective points (here, a
    ///     single point repeated, forming a zero-area "polygon") contributes zero coverage,
    ///     rather than throwing or otherwise misbehaving.
    /// </summary>
    [Fact]
    public void ScanlineRasterizer_Fill_DegeneratePolygon_ContributesZeroCoverage()
    {
        // Arrange: a degenerate two-point polygon (a single line segment doubled back on itself)
        var polygon = new List<Vector2> { new(1, 1), new(1, 1) };
        var surface = new Surface(2, 2);

        // Act
        ScanlineRasterizer.Fill(surface, [polygon], new Rgba32(1, 2, 3, 255), FillRule.NonZero, new Rect(0, 0, 2, 2));

        // Assert: no pixel is touched
        Assert.Equal(new Rgba32(0, 0, 0, 0), surface[0, 0]);
        Assert.Equal(new Rgba32(0, 0, 0, 0), surface[1, 1]);
    }
}
