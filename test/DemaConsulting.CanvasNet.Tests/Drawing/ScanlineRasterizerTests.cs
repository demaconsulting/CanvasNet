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
    ///     Proves the fix for the medium-severity active-edge-list bookkeeping bug: sweeping a
    ///     tall region containing many edges that genuinely expire at staggered rows throughout
    ///     the sweep (rather than all abandoned together at the very last row) completes within a
    ///     bound that is calibrated, at test run time, against this machine's own measured
    ///     throughput on an independent workload - rather than any fixed millisecond constant -
    ///     so the test remains meaningful on hardware faster or slower than the machine it was
    ///     authored on.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Edge construction actually exercises <c>RemoveActiveEdge</c>.</b> Every rectangle
    ///         shares the same top row (0), but each rectangle's bottom row is staggered evenly
    ///         across the *entire* clip height (rectangle <c>i</c> of <c>N</c> ends at row
    ///         <c>(i + 1) * height / N</c>), so on essentially every row of the sweep at least one
    ///         edge's bucketed expiration row is strictly less than <c>clipMaxY</c> and is
    ///         genuinely removed via the expiration bucket/<c>RemoveActiveEdge</c> mid-sweep -
    ///         unlike an earlier version of this test where every edge's <c>BottomY</c> exactly
    ///         equalled <c>clipMaxY</c>, making the bucketed-removal condition
    ///         (<c>expireRow &lt; clipMaxY</c>) always false and <c>RemoveActiveEdge</c> never
    ///         actually invoked (edges were only ever abandoned unremoved at the end of the sweep).
    ///         Despite every edge eventually expiring, the active-edge list still grows to, and
    ///         stays near, its full size for most of the sweep (rectangle <c>i</c> remains active
    ///         until row <c>(i + 1) * height / N</c>, so on average half of all rectangles are
    ///         still active at any given row) - this is what makes both the genuine
    ///         <c>RemoveActiveEdge</c> traffic *and* the unavoidable per-row rendering cost
    ///         (<c>BuildRowEdges</c>/cell accumulation, which must visit every still-active edge
    ///         regardless of removal mechanism) substantial on every row.
    ///     </para>
    ///     <para>
    ///         <b>Why the measured separation is a modest, bounded multiplier, not an order of
    ///         magnitude, and why that is expected.</b> Because the per-row rendering cost
    ///         (<c>BuildRowEdges</c> plus cell accumulation) already visits every currently-active
    ///         edge regardless of which removal mechanism is used, that unavoidable cost is
    ///         identical for the fixed implementation and the regression; the regression's
    ///         <c>RemoveAll</c> only adds *one more* <c>O(active-list size)</c> pass on top of the
    ///         (already <c>O(active-list size)</c>-per-row) rendering passes - so the maximum
    ///         possible overhead is bounded by roughly "one extra pass out of the several the
    ///         render step already performs", not a difference in complexity *class*. This was
    ///         verified empirically: across many edge-construction strategies tried while
    ///         developing this test (no genuine expiry at all; genuine expiry staggered evenly
    ///         across the whole sweep as used here; a bounded sliding window of concurrently-active
    ///         edges; wildly different edge-to-row ratios from 300:1 down to 1:400) the regression
    ///         consistently measured only around 30-50% slower than the fixed implementation in
    ///         every configuration - genuine mid-sweep expiration exercises the correct code path
    ///         but does not, and structurally cannot, unlock a dramatically larger separation for
    ///         *this specific* bug, because the bug is a constant-factor overhead rather than an
    ///         asymptotic complexity regression.
    ///     </para>
    ///     <para>
    ///         <b>Why a same-run size-scaling ratio cannot detect this bug (confirmed
    ///         empirically).</b> A scaling-ratio test - such as the two sibling tests below, or
    ///         <see cref="ScanlineRasterizer_Fill_ManyOverlappingRectangles_ScalesRoughlyLinearlyWithEdgeCount"/>
    ///         - asserts that doubling the input size does not more than roughly double (or
    ///         quadruple, etc.) the elapsed time under *whatever implementation is currently
    ///         present*; it is designed to catch a change in complexity *class* (e.g. O(n)
    ///         becoming O(n^2)), not a change in constant factor. Because this regression preserves
    ///         the same complexity class as the fix (both are O(edges x rows) for this edge shape
    ///         - see above) and only changes the constant multiplier, scaling the input size scales
    ///         the time by the same factor under *both* the fixed implementation and the regression
    ///         alike, so no same-run ratio between two problem sizes - regardless of which sizes
    ///         are chosen - can distinguish them. This was directly reconfirmed while redesigning
    ///         this test: measuring the fastest-of-many time for a half-scale run (16,000
    ///         rectangles / 4,000 rows) and this test's full-scale run (32,000 rectangles / 8,000
    ///         rows) in the same process and comparing their ratio gave effectively the same result
    ///         (full/half-scaled-by-4 ratio ~0.92-0.97) whether or not the historical
    ///         <c>RemoveAll</c> regression was present - the regression's ~30-50% overhead applies
    ///         equally to both problem sizes and cancels out of the ratio.
    ///     </para>
    ///     <para>
    ///         <b>The self-calibrating mechanism actually used: an independent, shape-matched
    ///         throughput reference, not a same-run size ratio.</b> Because a same-run *ratio
    ///         between two sizes of the regression's own code path* cannot detect a uniform
    ///         constant-factor regression (see above), detecting it at all - on hardware whose
    ///         absolute speed is unknown ahead of time - requires comparing the measured time
    ///         against an expectation derived from something that varies with this machine's raw
    ///         speed but is *not itself subject to the regression*.
    ///         <see cref="MeasureIndependentThroughputCalibrationMilliseconds"/> provides that: a
    ///         fixed-size reference workload that reproduces the *same* triangular active-entry-
    ///         count shape as the full sweep above (entries remaining "active" for a staggered
    ///         number of rows, so on average half remain active at any point, and every
    ///         still-active entry is visited once per row - mirroring the unavoidable
    ///         <c>BuildRowEdges</c>/cell-accumulation cost every implementation pays), sized and
    ///         timed to take roughly as long, at rest, as the full sweep itself - but implemented
    ///         with its own independent <c>List&lt;int&gt;</c> and never calling
    ///         <see cref="ScanlineRasterizer"/> at all. Its measured time therefore reflects only
    ///         this machine's raw throughput on this memory-access shape, and is completely
    ///         unaffected by whether the active-edge removal mechanism under test is regressed.
    ///         The ratio of the full sweep's fastest time to this reference workload's fastest time
    ///         is then compared against a fixed threshold - the threshold itself is a dimensionless
    ///         ratio, not a millisecond value, so (to the extent the reference workload and the
    ///         rasterizer scale proportionally with this machine's speed - see the empirical
    ///         validation below for the residual risk where that assumption is imperfect) it is
    ///         expected to remain meaningful across machines of different absolute speed, unlike
    ///         the fixed-millisecond threshold this test previously used.
    ///     </para>
    ///     <para>
    ///         <b>Why the reference workload is duration-matched, not just shape-matched.</b> An
    ///         earlier attempt at this calibration used a reference workload with the same
    ///         triangular shape but scaled to run roughly 10x faster than the full sweep (tens of
    ///         milliseconds rather several hundred). That shorter measurement was found,
    ///         empirically, to be *disproportionately* likely to land in a contention-free window
    ///         even while the much longer full sweep it was compared against kept overlapping with
    ///         at least some concurrent load - inflating the full/reference ratio for *both* the
    ///         fixed implementation and the regression under this repository's own concurrent
    ///         multi-target-framework test execution, to the point their ranges were observed to
    ///         overlap in some trials. Scaling the reference workload up so its unhindered duration
    ///         is comparable to the full sweep's (both take roughly a second at rest on the
    ///         development machine) removed that asymmetry and restored a clean separation - see
    ///         the empirical validation below.
    ///     </para>
    ///     <para>
    ///         <b>Empirical validation.</b> On the development machine used to design this test,
    ///         across multiple repeated trials of the calibration-then-full-sweep sequence used
    ///         below, both at rest and under this repository's own <c>build.ps1</c> (which runs
    ///         this project's three target frameworks' test binaries concurrently, so each trial
    ///         under "contention" below ran all three simultaneously): the fixed implementation
    ///         measured a full/reference ratio of roughly 0.887-0.891 at rest, widening to roughly
    ///         0.888-0.975 under that concurrent execution. Temporarily reintroducing the
    ///         historical full-active-list <c>activeEdges.RemoveAll(edge => edge.BottomY &lt;=
    ///         rowTop)</c> rescan (replacing the bucketed removal) measured a ratio of roughly
    ///         1.070-1.074 at rest, and roughly 1.055-1.458 under the same concurrent execution -
    ///         reliably and comfortably above the fixed implementation's range in every trial, at
    ///         rest and under contention alike, with a clear gap (0.975 to 1.055) separating the
    ///         two implementations' worst-case observed ratios. The threshold below (1.0) is chosen
    ///         inside that gap. Because both measurements are taken back-to-back in the same
    ///         process on the same machine, this approach - unlike the fixed-millisecond threshold
    ///         it replaces - does not depend on the absolute speed of whatever machine runs the
    ///         test; the residual risk it does carry is that the reference workload's simplified
    ///         <c>int</c>-based bookkeeping does not correlate perfectly with the rasterizer's own
    ///         <c>Edge</c>-struct-based bookkeeping and cell accumulation on every possible CPU
    ///         microarchitecture, and that contention patterns more extreme than three concurrent
    ///         target-framework test runs could still widen either range - this is an accepted,
    ///         inherent limitation of any throughput-proxy calibration, and is why the threshold is
    ///         placed with margin on both sides of the empirically observed gap rather than exactly
    ///         at either boundary.
    ///     </para>
    /// </remarks>
    [Fact]
    public void ScanlineRasterizer_Fill_TallRegionWithManyLongLivedEdges_ScalesRoughlyLinearlyNotQuadratically()
    {
        // Arrange: 32,000 rectangles, all starting at row 0 but each ending at a different,
        // evenly-staggered row spread across the full 8,000-row clip height, swept on a
        // 1-pixel-wide surface. On average half of all rectangles are concurrently active at any
        // given row (so the active-edge list - and therefore both the unavoidable per-row
        // rendering cost and, under the regression, the full-list re-scan cost - stays large for
        // most of the sweep), while genuinely staggered BottomY values mean at least one edge's
        // bucketed expiration is actually exercised via RemoveActiveEdge on essentially every row,
        // rather than every edge being abandoned, unremoved, only at the very last row.
        const int width = 1;
        const int height = 8000;
        const int rectangleCount = 32_000;
        const float topY = 0f;
        var color = new Rgba32(1, 2, 3, 255);
        var clipBounds = new Rect(0, 0, width, height);
        var polygons = new List<List<Vector2>>(rectangleCount);
        for (var i = 0; i < rectangleCount; i++)
        {
            var offset = i * 0.0001f;
            var bottomY = (i + 1) * (float)height / rectangleCount;
            polygons.Add(
            [
                new Vector2(0.1f + offset, topY),
                new Vector2(width - 0.1f + offset, topY),
                new Vector2(width - 0.1f + offset, bottomY),
                new Vector2(0.1f + offset, bottomY),
                new Vector2(0.1f + offset, topY)
            ]);
        }

        // Unmeasured warmup: absorbs JIT tiering-up cost so the measured runs reflect steady-state
        // throughput rather than first-call compilation overhead.
        ScanlineRasterizer.Fill(new Surface(width, height), polygons, color, FillRule.NonZero, clipBounds);

        // Act: minimum-of-eleven wall-clock measurement of the full sweep, and a separate
        // minimum-of-eleven measurement of the independent, shape-matched reference workload (see
        // remarks). Unlike a median, the minimum is immune to *any number* of samples being slowed
        // down by transient contention (GC pauses, scheduler noise, or concurrent test execution):
        // contention can only ever make a sample slower than the implementation's true unhindered
        // cost, never faster, so the fastest of many repeated samples is a robust, noise-resistant
        // estimate of that true cost, whereas a median can still land on a contention-inflated
        // value once enough of the samples happen to be affected.
        var samples = new long[11];
        for (var i = 0; i < samples.Length; i++)
        {
            var surface = new Surface(width, height);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            ScanlineRasterizer.Fill(surface, polygons, color, FillRule.NonZero, clipBounds);
            stopwatch.Stop();
            samples[i] = stopwatch.ElapsedMilliseconds;
        }

        Array.Sort(samples);
        var fastestFullSweepMilliseconds = samples[0];
        var fastestReferenceMilliseconds = MeasureIndependentThroughputCalibrationMilliseconds();
        var ratio = fastestFullSweepMilliseconds / fastestReferenceMilliseconds;

        // Assert: the full sweep's fastest time, expressed as a multiple of this machine's own
        // measured throughput on an independent, shape-matched reference workload, stays within a
        // threshold chosen inside the empirically observed gap between the fixed implementation's
        // worst-case ratio (~0.975, under concurrent multi-target-framework test execution) and
        // the historical regression's best-case ratio (~1.055, at rest) - see remarks above for
        // the full empirical validation.
        const double maxRatio = 1.0;
        Assert.True(
            ratio <= maxRatio,
            $"Expected the full sweep's fastest-of-{samples.Length} time to be at most " +
            $"{maxRatio}x this machine's own fastest-of-11 reference-workload time (observed " +
            $"~0.887-0.975x for the fixed implementation vs. ~1.055-1.458x for the historical " +
            $"full-active-list-rescan regression), but the full sweep took " +
            $"{fastestFullSweepMilliseconds}ms, the reference workload took " +
            $"{fastestReferenceMilliseconds:F2}ms, giving a ratio of {ratio:F3}x. Full-sweep " +
            $"samples: [{string.Join(", ", samples)}].");
    }

    /// <summary>
    ///     Measures this machine's own achieved throughput, at test run time, on a fixed-size
    ///     reference workload that reproduces the *same* triangular active-entry-count shape as
    ///     the full sweep this test measures (entries remaining "active" for a staggered number of
    ///     rows, so on average half remain active at any point), scaled up (120,000 entries over
    ///     30,000 rows, rather than the full sweep's 32,000 edges over 8,000 rows) so its
    ///     unhindered duration is comparable to the full sweep's own - see the caller's remarks for
    ///     why that duration match, not just the shape match, is essential for the comparison to
    ///     remain valid under concurrent test execution. Every still-active entry is visited once
    ///     per row (mirroring the unavoidable per-row <c>BuildRowEdges</c>/cell-accumulation cost
    ///     every implementation pays) and expired entries are removed via the same swap-remove
    ///     pattern <c>RemoveActiveEdge</c> uses - but using its own independent
    ///     <c>List&lt;int&gt;</c>, entirely separate from <see cref="ScanlineRasterizer"/>. It
    ///     never calls <see cref="ScanlineRasterizer.Fill"/> at all, so its measured time reflects
    ///     only this machine's raw throughput on this memory-access shape and can never be
    ///     influenced by a regression in the rasterizer's own active-edge removal mechanism.
    /// </summary>
    /// <returns>
    ///     The fastest of eleven repeated measurements, in milliseconds, of the reference workload
    ///     described above.
    /// </returns>
    private static double MeasureIndependentThroughputCalibrationMilliseconds()
    {
        const int entryCount = 120_000;
        const int rowCount = 30_000;

        double RunOnce()
        {
            var active = new List<int>(entryCount);
            for (var i = 0; i < entryCount; i++)
            {
                active.Add(i);
            }

            var accumulatorA = 0f;
            var accumulatorB = 0f;
            var expirationsPerRow = entryCount / (double)rowCount;
            var carry = 0.0;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (var row = 0; row < rowCount; row++)
            {
                // Mirrors BuildRowEdges + AccumulateRowEdge: visit every currently-active entry
                // exactly once per row, the unavoidable per-row cost paid regardless of removal
                // mechanism.
                for (var k = 0; k < active.Count; k++)
                {
                    accumulatorA += active[k] * 0.0001f;
                    accumulatorB += active[k] * 0.0002f;
                }

                // Mirrors the staggered per-row expirations: remove entries at the same average
                // rate the full sweep's rectangles expire, via the same swap-remove
                // RemoveActiveEdge itself uses.
                carry += expirationsPerRow;
                while (carry >= 1.0 && active.Count > 0)
                {
                    var last = active.Count - 1;
                    active[0] = active[last];
                    active.RemoveAt(last);
                    carry -= 1.0;
                }
            }

            stopwatch.Stop();

            // Read the accumulators so the JIT cannot treat the loop as dead code and elide it.
            return stopwatch.Elapsed.TotalMilliseconds + (accumulatorA + accumulatorB) * 0.0;
        }

        // Unmeasured warmup, then minimum-of-eleven - see the caller's "Act" comment for why the
        // minimum, not the median, is used.
        RunOnce();
        var samples = new double[11];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = RunOnce();
        }

        Array.Sort(samples);
        return samples[0];
    }

    /// <summary>
    ///     Holds the visible row count fixed (large) and quadruples only the number of long-lived
    ///     edges. Kept as a basic linear-scaling sanity check alongside
    ///     <see cref="ScanlineRasterizer_Fill_TallRegionWithManyLongLivedEdges_ScalesRoughlyLinearlyNotQuadratically"/>;
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
    ///     <see cref="ScanlineRasterizer_Fill_TallRegionWithManyLongLivedEdges_ScalesRoughlyLinearlyNotQuadratically"/>;
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
