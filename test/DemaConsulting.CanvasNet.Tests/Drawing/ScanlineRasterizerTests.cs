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
    ///     Proves that two exactly coincident, sub-pixel-offset polygons resolve to the correct
    ///     analytic result under both fill rules - not the naive "sum each polygon's own raw
    ///     fractional coverage, then fold the aggregate" approach, which would incorrectly double
    ///     the covered fraction (clamping it to fully opaque under NonZero, instead of leaving it
    ///     at the single shape's true covered fraction) and would incorrectly report a nonzero
    ///     result under EvenOdd (instead of correctly cancelling to fully transparent, since the
    ///     two duplicate shapes cover exactly the same points and even-odd toggles per crossing).
    /// </summary>
    /// <remarks>
    ///     Arrange: a rectangle from <c>x = 0.25</c> to <c>x = 1.75</c> (spanning both columns of
    ///     a 2x2 surface with a fractional edge in each), duplicated twice in the polygon list.
    ///     Hand-computed reference: a single instance of this rectangle covers each column by
    ///     exactly <c>0.75</c> of its unit cell (column 0: from <c>x = 0.25</c> to <c>x = 1</c>;
    ///     column 1: from <c>x = 1</c> to <c>x = 1.75</c>) - since the duplicate is an exact copy
    ///     occupying precisely the same points, the true covered fraction remains <c>0.75</c>
    ///     under NonZero (raw winding 2 throughout the covered region, but the covered region
    ///     itself is unchanged by duplication), while EvenOdd toggles back to uncovered
    ///     everywhere the duplicate's edges coincide with the original's, correctly cancelling to
    ///     <c>0</c> everywhere. Under the old, buggy "sum raw fractional coverage, then fold"
    ///     approach, this scenario instead incorrectly produces a NonZero raw coverage of
    ///     <c>1.5</c> per column (clamped to fully opaque, alpha 255, not 191) and an EvenOdd
    ///     triangle-fold of <c>0.5</c> (alpha 128, not 0).
    /// </remarks>
    [Fact]
    public void ScanlineRasterizer_Fill_SubPixelOffsetDuplicatePolygons_NonZeroMatchesSingleShapeEvenOddCancelsToZero()
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

        // Assert: NonZero matches the single shape's own true covered fraction (0.75 * 255 =
        // 191.25, rounds to 191), not a doubled-and-clamped 255, at every covered pixel
        Assert.Equal((byte)191, nonZeroSurface[0, 0].A);
        Assert.Equal((byte)191, nonZeroSurface[1, 0].A);
        Assert.Equal((byte)191, nonZeroSurface[0, 1].A);
        Assert.Equal((byte)191, nonZeroSurface[1, 1].A);

        // Assert: EvenOdd correctly cancels to fully transparent everywhere, not a spurious 128
        Assert.Equal((byte)0, evenOddSurface[0, 0].A);
        Assert.Equal((byte)0, evenOddSurface[1, 0].A);
        Assert.Equal((byte)0, evenOddSurface[0, 1].A);
        Assert.Equal((byte)0, evenOddSurface[1, 1].A);
    }

    /// <summary>
    ///     Proves that two overlapping (not coincident), sub-pixel-offset polygons resolve to the
    ///     correct per-region analytic result under both fill rules, matching an independently
    ///     hand-computed reference rather than the naive raw-coverage-sum-then-fold approach.
    /// </summary>
    /// <remarks>
    ///     Arrange: on a 2x1 surface, square 1 spans <c>x</c> in <c>[0.25, 1.25)</c> and square 2
    ///     spans <c>x</c> in <c>[0.75, 1.75)</c> (both full-height), so <c>x</c> in
    ///     <c>[0.75, 1.25)</c> is doubly covered (raw winding 2), while <c>[0.25, 0.75)</c> and
    ///     <c>[1.25, 1.75)</c> are singly covered (raw winding 1). Hand-computed reference: under
    ///     NonZero, the entire union <c>[0.25, 1.75)</c> is "inside" regardless of winding
    ///     magnitude, so column 0 (<c>x</c> in <c>[0, 1)</c>) covers exactly <c>[0.25, 1)</c>
    ///     (fraction <c>0.75</c>) and column 1 (<c>x</c> in <c>[1, 2)</c>) covers exactly
    ///     <c>[1, 1.75)</c> (fraction <c>0.75</c>). Under EvenOdd, only the singly-covered
    ///     sub-regions are "inside": column 0 covers <c>[0.25, 0.75)</c> (fraction <c>0.5</c>,
    ///     since <c>[0.75, 1)</c> falls in the doubly-covered, therefore even/uncovered, region)
    ///     and column 1 covers <c>[1.25, 1.75)</c> (fraction <c>0.5</c>, since <c>[1, 1.25)</c>
    ///     falls in the doubly-covered region). Under the old, buggy "sum raw fractional coverage,
    ///     then fold" approach, column 0's raw coverage instead sums to <c>1.0</c> (from both
    ///     squares' own left-edge fractional contributions), which folds to fully opaque (alpha
    ///     255) under both NonZero <i>and</i> EvenOdd - failing to distinguish the two fill rules
    ///     at all for this pixel, unlike the correct <c>191</c>/<c>128</c> divergence asserted
    ///     below.
    /// </remarks>
    [Fact]
    public void ScanlineRasterizer_Fill_OverlappingSubPixelSquares_MatchesHandComputedPerRegionCoverage()
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

        // Assert: NonZero yields the union area's coverage fraction (0.75 * 255 = 191.25 -> 191)
        Assert.Equal((byte)191, nonZeroSurface[0, 0].A);
        Assert.Equal((byte)191, nonZeroSurface[1, 0].A);

        // Assert: EvenOdd yields only the singly-covered symmetric-difference fraction
        // (0.5 * 255 = 127.5 -> 128 under round-half-away-from-zero)
        Assert.Equal((byte)128, evenOddSurface[0, 0].A);
        Assert.Equal((byte)128, evenOddSurface[1, 0].A);
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
