using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Tests.Drawing;

/// <summary>
///     Unit tests for <see cref="PathFiller"/>.
/// </summary>
public class PathFillerTests
{
    /// <summary>
    ///     Proves that filling an axis-aligned rectangle with fractional-pixel edges produces
    ///     fully opaque interior pixels and correctly antialiased edge pixels, matching an
    ///     independently hand-computed analytic coverage (see the developer's companion
    ///     computation: a rectangle from x=0.5 to x=3.5 covers pixel columns 1-2 fully, and
    ///     columns 0 and 3 at exactly half coverage).
    /// </summary>
    [Fact]
    public void PathFiller_Fill_AxisAlignedRectangleFractionalEdges_InteriorOpaqueEdgesAntialiased()
    {
        // Arrange: a 4x4 surface and a rectangle from (0.5, 0.5) to (3.5, 2.5)
        var surface = new Surface(4, 4);
        var path = new PathBuilder()
            .MoveTo(new Vector2(0.5f, 0.5f))
            .LineTo(new Vector2(3.5f, 0.5f))
            .LineTo(new Vector2(3.5f, 2.5f))
            .LineTo(new Vector2(0.5f, 2.5f))
            .Close()
            .Build();
        var color = new Rgba32(255, 0, 0, 255);

        // Act
        PathFiller.Fill(surface, path, color);

        // Assert: interior columns (1, 2) at row 1 are fully opaque
        Assert.Equal((byte)255, surface[1, 1].A);
        Assert.Equal((byte)255, surface[2, 1].A);

        // Assert: edge columns (0, 3) at row 1 are antialiased to exactly half coverage
        // (255 * 0.5 = 127.5, rounds to 128 under round-half-away-from-zero)
        Assert.Equal((byte)128, surface[0, 1].A);
        Assert.Equal((byte)128, surface[3, 1].A);

        // Assert: a pixel entirely outside the rectangle's bounding box remains untouched
        Assert.Equal(new Rgba32(0, 0, 0, 0), surface[0, 3]);
    }

    /// <summary>
    ///     Proves that filling a right triangle with a diagonal hypotenuse produces an
    ///     antialiased coverage gradient matching an independently hand-computed analytic result
    ///     (see the developer's companion computation: the triangle (0,0),(2,0),(0,2) covers the
    ///     top-left cell fully, the two cells straddling the hypotenuse at exactly half coverage,
    ///     and the bottom-right cell not at all).
    /// </summary>
    [Fact]
    public void PathFiller_Fill_DiagonalTriangle_MatchesHandComputedCoverageGradient()
    {
        // Arrange: a 2x2 surface and a right triangle with a diagonal hypotenuse from (2,0) to (0,2)
        var surface = new Surface(2, 2);
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0))
            .LineTo(new Vector2(2, 0))
            .LineTo(new Vector2(0, 2))
            .Close()
            .Build();
        var color = new Rgba32(0, 255, 0, 255);

        // Act
        PathFiller.Fill(surface, path, color);

        // Assert: (0,0) is fully inside the triangle
        Assert.Equal((byte)255, surface[0, 0].A);

        // Assert: (1,0) and (0,1) straddle the hypotenuse at exactly half coverage
        Assert.Equal((byte)128, surface[1, 0].A);
        Assert.Equal((byte)128, surface[0, 1].A);

        // Assert: (1,1) is entirely outside the triangle
        Assert.Equal((byte)0, surface[1, 1].A);
    }

    /// <summary>
    ///     Proves that filling two overlapping, identically wound rectangles (whose overlap
    ///     region therefore has a raw winding number of 2) diverges between fill rules exactly as
    ///     expected: NonZero clamps the overlap's magnitude-2 winding to fully opaque, while
    ///     EvenOdd folds a winding of 2 to fully transparent, even though both rules agree the
    ///     non-overlapping region (winding magnitude 1) is fully opaque.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_OverlappingSameWoundRectangles_NonZeroVsEvenOddDiverge()
    {
        // Arrange: two identically wound, integer-aligned, overlapping 4x4 rectangles on an 8x8
        // surface, so every covered pixel has an exact (non-antialiased) winding count
        Path BuildOverlappingRectangles()
        {
            var builder = new PathBuilder();
            builder.MoveTo(new Vector2(0, 0)).LineTo(new Vector2(4, 0)).LineTo(new Vector2(4, 4)).LineTo(new Vector2(0, 4)).Close();
            builder.MoveTo(new Vector2(2, 2)).LineTo(new Vector2(6, 2)).LineTo(new Vector2(6, 6)).LineTo(new Vector2(2, 6)).Close();
            return builder.Build();
        }

        var color = new Rgba32(0, 0, 255, 255);

        var nonZeroSurface = new Surface(8, 8);
        PathFiller.Fill(nonZeroSurface, BuildOverlappingRectangles(), color, FillRule.NonZero);

        var evenOddSurface = new Surface(8, 8);
        PathFiller.Fill(evenOddSurface, BuildOverlappingRectangles(), color, FillRule.EvenOdd);

        // Assert: the single-covered pixel (1,1) is fully opaque under both rules
        Assert.Equal((byte)255, nonZeroSurface[1, 1].A);
        Assert.Equal((byte)255, evenOddSurface[1, 1].A);

        // Assert: the doubly-covered overlap pixel (3,3) diverges: opaque under NonZero, fully
        // transparent under EvenOdd
        Assert.Equal((byte)255, nonZeroSurface[3, 3].A);
        Assert.Equal((byte)0, evenOddSurface[3, 3].A);
    }

    /// <summary>
    ///     Proves that a single Path containing an outer subpath and an oppositely wound inner
    ///     subpath renders a "hole": the ring between the two subpaths is filled, while the
    ///     inner subpath's own interior is left unfilled, matching an independently hand-computed
    ///     analytic result (see the developer's companion computation).
    /// </summary>
    [Fact]
    public void PathFiller_Fill_NestedCounterWoundSubpaths_RendersHole()
    {
        // Arrange: an outer 6x6 square and an oppositely wound inner 2x2 hole (from (2,2) to
        // (4,4)), both integer-aligned so every covered pixel has an exact winding count
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0)).LineTo(new Vector2(6, 0)).LineTo(new Vector2(6, 6)).LineTo(new Vector2(0, 6)).Close()
            .MoveTo(new Vector2(2, 2)).LineTo(new Vector2(2, 4)).LineTo(new Vector2(4, 4)).LineTo(new Vector2(4, 2)).Close()
            .Build();
        var surface = new Surface(6, 6);
        var color = new Rgba32(255, 255, 0, 255);

        // Act
        PathFiller.Fill(surface, path, color);

        // Assert: the ring between the outer square and the hole is filled
        Assert.Equal((byte)255, surface[1, 1].A);
        Assert.Equal((byte)255, surface[1, 3].A);
        Assert.Equal((byte)255, surface[4, 4].A);

        // Assert: the hole's own interior is left unfilled
        Assert.Equal((byte)0, surface[2, 2].A);
        Assert.Equal((byte)0, surface[3, 3].A);
    }

    /// <summary>
    ///     Proves that a subpath left explicitly open (no Close call) fills identically to the
    ///     same subpath explicitly closed, because every subpath is treated as implicitly closed
    ///     for fill purposes regardless of Subpath.IsClosed.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_ExplicitlyOpenSubpath_FillsIdenticallyToClosed()
    {
        // Arrange: the same triangle built once left open, once explicitly closed
        var openPath = new PathBuilder()
            .MoveTo(new Vector2(0, 0))
            .LineTo(new Vector2(4, 0))
            .LineTo(new Vector2(0, 4))
            .Build();
        var closedPath = new PathBuilder()
            .MoveTo(new Vector2(0, 0))
            .LineTo(new Vector2(4, 0))
            .LineTo(new Vector2(0, 4))
            .Close()
            .Build();
        Assert.False(openPath.Subpaths[0].IsClosed);
        Assert.True(closedPath.Subpaths[0].IsClosed);

        var color = new Rgba32(10, 20, 30, 255);
        var openSurface = new Surface(4, 4);
        var closedSurface = new Surface(4, 4);

        // Act
        PathFiller.Fill(openSurface, openPath, color);
        PathFiller.Fill(closedSurface, closedPath, color);

        // Assert: every pixel matches exactly between the open and closed variants
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                Assert.Equal(closedSurface[x, y], openSurface[x, y]);
            }
        }
    }

    /// <summary>
    ///     Proves that a genuinely curved edge is correctly integrated end-to-end through
    ///     <see cref="EdgeFlattener"/> and <see cref="ScanlineRasterizer"/> - not merely that the
    ///     curve is flattened to the expected vertex positions (see
    ///     <c>EdgeFlattenerTests</c> for that), but that the resulting rendered pixel coverage
    ///     matches the curve's true analytic area. A quadratic Bezier curve from <c>(0, 0)</c> to
    ///     <c>(4, 4)</c> with control point <c>(4, 0)</c>, implicitly closed by a straight edge
    ///     back to <c>(0, 0)</c>, encloses an area with a well-known closed form: the area between
    ///     a quadratic Bezier curve and its own chord equals exactly 2/3 of the area of the
    ///     triangle formed by the curve's start, control, and end points (a standard identity
    ///     obtained by applying Green's theorem to the curve's quadratic parametric form), and
    ///     since the implicit closing edge here runs exactly along that chord, the closed shape's
    ///     total area is exactly that bulge area: 2/3 * (0.5 * 4 * 4) = 16/3.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_QuadraticCurveShape_TotalCoverageMatchesAnalyticBezierBulgeArea()
    {
        // Arrange: a small surface comfortably enclosing the curve's bounding box, and a tight
        // flatten tolerance so the flattened polygon's area is negligibly close to the true
        // curve's analytic area
        var surface = new Surface(6, 6);
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0))
            .QuadraticBezierTo(new Vector2(4, 0), new Vector2(4, 4))
            .Build();
        var color = new Rgba32(255, 255, 255, 255);

        // Act
        PathFiller.Fill(surface, path, color, flattenTolerance: 0.001f);

        // Sum every pixel's fractional coverage (alpha / 255) across the whole surface - this
        // exercises the actual rendered pixel output, not just the flattened vertex positions
        double totalCoverage = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                totalCoverage += surface[x, y].A / 255.0;
            }
        }

        // Assert: the rasterized total coverage matches the analytic Bezier bulge area (16/3)
        Assert.Equal(16.0 / 3.0, totalCoverage, 1);
    }

    /// <summary>
    ///     Proves that filling Path.Empty is a no-op: the surface is left completely unmodified,
    ///     and no exception is thrown.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_EmptyPath_NoOpLeavesSurfaceUnchanged()
    {
        // Arrange
        var surface = new Surface(2, 2);
        surface[0, 0] = new Rgba32(1, 2, 3, 4);

        // Act
        PathFiller.Fill(surface, Path.Empty, new Rgba32(9, 9, 9, 9));

        // Assert
        Assert.Equal(new Rgba32(1, 2, 3, 4), surface[0, 0]);
        Assert.Equal(new Rgba32(0, 0, 0, 0), surface[1, 1]);
    }

    /// <summary>
    ///     Proves that filling a path whose bounding box does not overlap the surface's pixel
    ///     extent at all is a no-op: the surface is left completely unmodified, and no exception
    ///     is thrown.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_PathFullyOutsideSurfaceBounds_NoOpLeavesSurfaceUnchanged()
    {
        // Arrange: a 4x4 surface and a rectangle entirely beyond its bounds
        var surface = new Surface(4, 4);
        surface[0, 0] = new Rgba32(1, 2, 3, 4);
        var path = new PathBuilder()
            .MoveTo(new Vector2(10, 10))
            .LineTo(new Vector2(20, 10))
            .LineTo(new Vector2(20, 20))
            .LineTo(new Vector2(10, 20))
            .Close()
            .Build();

        // Act
        PathFiller.Fill(surface, path, new Rgba32(9, 9, 9, 9));

        // Assert
        Assert.Equal(new Rgba32(1, 2, 3, 4), surface[0, 0]);
        Assert.Equal(new Rgba32(0, 0, 0, 0), surface[3, 3]);
    }

    /// <summary>
    ///     Proves that a fully covered interior pixel's composited color and alpha exactly match
    ///     an independently computed Porter-Duff "over" oracle - not merely that the pixel
    ///     changed, but that it changed to the precise expected value.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_FullyCoveredInteriorPixel_ColorAndAlphaMatchOverOracle()
    {
        // Arrange: an opaque background pixel and a semi-transparent fill color
        var surface = new Surface(4, 4);
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                surface[x, y] = new Rgba32(50, 60, 70, 255);
            }
        }

        var color = new Rgba32(200, 100, 10, 180);
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0)).LineTo(new Vector2(4, 0)).LineTo(new Vector2(4, 4)).LineTo(new Vector2(0, 4)).Close()
            .Build();

        var oracle = new Surface(4, 4);
        oracle[1, 1] = new Rgba32(50, 60, 70, 255);
        oracle.CompositeOver(color);

        // Act
        PathFiller.Fill(surface, path, color);

        // Assert
        Assert.Equal(oracle[1, 1], surface[1, 1]);
    }

    /// <summary>
    ///     Proves that Fill throws ArgumentNullException when the surface argument is null.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_NullSurface_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PathFiller.Fill(null!, Path.Empty, new Rgba32(1, 2, 3, 4)));
    }

    /// <summary>
    ///     Proves that Fill throws ArgumentNullException when the path argument is null.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PathFiller.Fill(new Surface(1, 1), null!, new Rgba32(1, 2, 3, 4)));
    }

    /// <summary>
    ///     Proves that Fill throws ArgumentOutOfRangeException when flattenTolerance is zero,
    ///     negative, or a non-finite value (NaN or either infinity).
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(-0.01f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void PathFiller_Fill_NonPositiveFlattenTolerance_ThrowsArgumentOutOfRangeException(float tolerance)
    {
        var surface = new Surface(2, 2);
        var path = new PathBuilder().MoveTo(new Vector2(0, 0)).LineTo(new Vector2(1, 1)).Close().Build();

        Assert.Throws<ArgumentOutOfRangeException>(() => PathFiller.Fill(surface, path, new Rgba32(1, 2, 3, 4), flattenTolerance: tolerance));
    }

    /// <summary>
    ///     Proves that Fill throws ArgumentOutOfRangeException when fillRule is not a defined
    ///     FillRule value, rather than silently being treated as FillRule.EvenOdd.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_UndefinedFillRule_ThrowsArgumentOutOfRangeException()
    {
        var surface = new Surface(2, 2);
        var path = new PathBuilder().MoveTo(new Vector2(0, 0)).LineTo(new Vector2(1, 1)).Close().Build();

        Assert.Throws<ArgumentOutOfRangeException>(() => PathFiller.Fill(surface, path, new Rgba32(1, 2, 3, 4), fillRule: (FillRule)42));
    }
}
