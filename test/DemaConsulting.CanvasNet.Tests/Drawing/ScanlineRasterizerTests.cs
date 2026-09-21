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
