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

    /// <summary>
    ///     Proves that the gradient overload of Fill paints each fully covered pixel with the
    ///     gradient's color at that pixel's own position - a horizontal rectangle filled with a
    ///     horizontal linear gradient produces the first stop's color at its left edge and the
    ///     last stop's color at its right edge.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_Gradient_HorizontalRectangleWithHorizontalLinearGradient_VariesLeftToRight()
    {
        // Arrange: a 10x1 surface, a rectangle covering it entirely, and a red-to-blue linear
        // gradient spanning the same extent
        var surface = new Surface(10, 1);
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0)).LineTo(new Vector2(10, 0)).LineTo(new Vector2(10, 1)).LineTo(new Vector2(0, 1))
            .Close()
            .Build();
        GradientStop[] stops =
        [
            new GradientStop(0f, new Rgba32(255, 0, 0, 255)),
            new GradientStop(1f, new Rgba32(0, 0, 255, 255)),
        ];
        var gradient = new LinearGradient(new Vector2(0, 0), new Vector2(10, 0), stops);

        // Act
        PathFiller.Fill(surface, path, gradient);

        // Assert: left edge is red-dominant, right edge is blue-dominant
        Assert.True(surface[0, 0].R > surface[0, 0].B);
        Assert.True(surface[9, 0].B > surface[9, 0].R);
    }

    /// <summary>
    ///     Proves that the gradient overload of Fill paints each fully covered pixel with a
    ///     RadialGradient's color at that pixel's own position, exercising the full
    ///     PathFiller/ScanlineRasterizer pipeline with a RadialGradient - previously only exercised
    ///     with LinearGradient.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_Gradient_SquareWithRadialGradient_CenterAndCornerMatchExpectedRamp()
    {
        // Arrange: a 10x10 surface, a square covering it entirely, and a red-to-blue radial
        // gradient centered on the surface with a radius that reaches the edges but not the corners.
        var surface = new Surface(10, 10);
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0)).LineTo(new Vector2(10, 0)).LineTo(new Vector2(10, 10)).LineTo(new Vector2(0, 10))
            .Close()
            .Build();
        GradientStop[] stops =
        [
            new GradientStop(0f, new Rgba32(255, 0, 0, 255)),
            new GradientStop(1f, new Rgba32(0, 0, 255, 255)),
        ];
        var gradient = new RadialGradient(new Vector2(5, 5), 0f, new Vector2(5, 5), 5f, stops);

        // Act
        PathFiller.Fill(surface, path, gradient);

        // Assert: the center pixel is red-dominant (near the start circle); a corner pixel (beyond
        // the end circle's radius, Pad-clamped to the last stop) is blue-dominant.
        Assert.True(surface[5, 5].R > surface[5, 5].B);
        Assert.True(surface[0, 0].B > surface[0, 0].R);
    }

    /// <summary>
    ///     Proves that the gradient overload throws ArgumentNullException when the surface
    ///     argument is null.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_Gradient_NullSurface_ThrowsArgumentNullException()
    {
        var gradient = new LinearGradient(Vector2.Zero, Vector2.One, [new GradientStop(0f, new Rgba32(1, 2, 3, 4))]);

        Assert.Throws<ArgumentNullException>(() => PathFiller.Fill(null!, Path.Empty, gradient));
    }

    /// <summary>
    ///     Proves that the gradient overload throws ArgumentNullException when the path argument
    ///     is null.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_Gradient_NullPath_ThrowsArgumentNullException()
    {
        var gradient = new LinearGradient(Vector2.Zero, Vector2.One, [new GradientStop(0f, new Rgba32(1, 2, 3, 4))]);

        Assert.Throws<ArgumentNullException>(() => PathFiller.Fill(new Surface(1, 1), null!, gradient));
    }

    /// <summary>
    ///     Proves that the gradient overload throws ArgumentNullException when the paint (gradient)
    ///     argument is null.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_Gradient_NullPaint_ThrowsArgumentNullException()
    {
        var surface = new Surface(1, 1);

        Assert.Throws<ArgumentNullException>(() => PathFiller.Fill(surface, Path.Empty, (Gradient)null!));
    }

    /// <summary>
    ///     Proves that the gradient overload is a no-op on an empty path, matching the solid-color
    ///     overload's documented behavior.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_Gradient_EmptyPath_NoOpLeavesSurfaceUnchanged()
    {
        var surface = new Surface(2, 2);
        surface[0, 0] = new Rgba32(1, 2, 3, 4);
        var gradient = new LinearGradient(Vector2.Zero, Vector2.One, [new GradientStop(0f, new Rgba32(9, 9, 9, 9))]);

        PathFiller.Fill(surface, Path.Empty, gradient);

        Assert.Equal(new Rgba32(1, 2, 3, 4), surface[0, 0]);
    }

    /// <summary>
    ///     Proves that the gradient overload throws ArgumentOutOfRangeException when
    ///     flattenTolerance is non-positive or non-finite, matching the solid-color overload's
    ///     validation.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(-0.01f)]
    [InlineData(float.NaN)]
    public void PathFiller_Fill_Gradient_NonPositiveFlattenTolerance_ThrowsArgumentOutOfRangeException(float tolerance)
    {
        var surface = new Surface(2, 2);
        var path = new PathBuilder().MoveTo(new Vector2(0, 0)).LineTo(new Vector2(1, 1)).Close().Build();
        var gradient = new LinearGradient(Vector2.Zero, Vector2.One, [new GradientStop(0f, new Rgba32(1, 2, 3, 4))]);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PathFiller.Fill(surface, path, gradient, flattenTolerance: tolerance));
    }

    /// <summary>
    ///     Proves that the gradient overload throws ArgumentOutOfRangeException when fillRule is
    ///     not a defined FillRule value.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_Gradient_UndefinedFillRule_ThrowsArgumentOutOfRangeException()
    {
        var surface = new Surface(2, 2);
        var path = new PathBuilder().MoveTo(new Vector2(0, 0)).LineTo(new Vector2(1, 1)).Close().Build();
        var gradient = new LinearGradient(Vector2.Zero, Vector2.One, [new GradientStop(0f, new Rgba32(1, 2, 3, 4))]);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PathFiller.Fill(surface, path, gradient, fillRule: (FillRule)42));
    }

    /// <summary>
    ///     Proves that filling with a single-stop gradient produces the same result as filling
    ///     with that stop's color via the solid-color overload - a single-stop gradient is a
    ///     solid color.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_Gradient_SingleStop_MatchesSolidColorFill()
    {
        var color = new Rgba32(30, 60, 90, 200);
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0)).LineTo(new Vector2(4, 0)).LineTo(new Vector2(4, 4)).LineTo(new Vector2(0, 4))
            .Close()
            .Build();

        var expected = new Surface(4, 4);
        PathFiller.Fill(expected, path, color);

        var actual = new Surface(4, 4);
        var gradient = new LinearGradient(Vector2.Zero, new Vector2(4, 0), [new GradientStop(0f, color)]);
        PathFiller.Fill(actual, path, gradient);

        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                Assert.Equal(expected[x, y], actual[x, y]);
            }
        }
    }

    /// <summary>
    ///     Proves that the gradient overload honors the fill rule for overlapping subpaths just
    ///     like the solid-color overload does: NonZero fills the fully overlapping region while
    ///     EvenOdd leaves it unfilled.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_Gradient_OverlappingSameWoundRectangles_NonZeroVsEvenOddDiverge()
    {
        var surface1 = new Surface(4, 4);
        var surface2 = new Surface(4, 4);
        var gradient = new LinearGradient(Vector2.Zero, new Vector2(4, 0), [new GradientStop(0f, new Rgba32(1, 2, 3, 255))]);

        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0)).LineTo(new Vector2(3, 0)).LineTo(new Vector2(3, 3)).LineTo(new Vector2(0, 3)).Close()
            .MoveTo(new Vector2(1, 1)).LineTo(new Vector2(4, 1)).LineTo(new Vector2(4, 4)).LineTo(new Vector2(1, 4)).Close()
            .Build();

        PathFiller.Fill(surface1, path, gradient, FillRule.NonZero);
        PathFiller.Fill(surface2, path, gradient, FillRule.EvenOdd);

        // The overlapping region (1,1)-(3,3) is filled under NonZero but left unfilled under
        // EvenOdd (two same-direction windings cancel to zero).
        Assert.NotEqual((byte)0, surface1[2, 2].A);
        Assert.Equal((byte)0, surface2[2, 2].A);
    }

    /// <summary>
    ///     Builds a 2x2 tile surface split into a red/blue 2-color checkerboard (red at (0,0)
    ///     and (1,1), blue at (1,0) and (0,1)), used by the <see cref="TilePaint"/> overload
    ///     tests below.
    /// </summary>
    private static Surface CreateCheckerboardTileSurface()
    {
        var tileSurface = new Surface(2, 2);
        tileSurface[0, 0] = new Rgba32(255, 0, 0, 255);
        tileSurface[1, 0] = new Rgba32(0, 0, 255, 255);
        tileSurface[0, 1] = new Rgba32(0, 0, 255, 255);
        tileSurface[1, 1] = new Rgba32(255, 0, 0, 255);
        return tileSurface;
    }

    /// <summary>
    ///     Proves that the <see cref="TilePaint"/> overload of Fill paints a repeating tile
    ///     pattern: filling a region larger than a single tile with a 2-color checkerboard tile
    ///     paint produces both of the tile's own colors in the filled result.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_TilePaint_CheckerboardTile_FillsWithBothTileColors()
    {
        using var tileSurface = CreateCheckerboardTileSurface();
        var tilePaint = new TilePaint(tileSurface, Matrix3x2.Identity, 2f, 2f);

        using var surface = new Surface(8, 8);
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0)).LineTo(new Vector2(8, 0)).LineTo(new Vector2(8, 8)).LineTo(new Vector2(0, 8))
            .Close()
            .Build();

        PathFiller.Fill(surface, path, tilePaint);

        var distinctColors = new HashSet<Rgba32>();
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                distinctColors.Add(surface[x, y]);
            }
        }

        Assert.Contains(new Rgba32(255, 0, 0, 255), distinctColors);
        Assert.Contains(new Rgba32(0, 0, 255, 255), distinctColors);
    }

    /// <summary>
    ///     Proves that the <see cref="TilePaint"/> overload throws ArgumentNullException when the
    ///     surface argument is <see langword="null"/>.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_TilePaint_NullSurface_ThrowsArgumentNullException()
    {
        using var tileSurface = CreateCheckerboardTileSurface();
        var tilePaint = new TilePaint(tileSurface, Matrix3x2.Identity, 2f, 2f);

        Assert.Throws<ArgumentNullException>(() => PathFiller.Fill(null!, Path.Empty, tilePaint));
    }

    /// <summary>
    ///     Proves that the <see cref="TilePaint"/> overload throws ArgumentNullException when the
    ///     path argument is <see langword="null"/>.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_TilePaint_NullPath_ThrowsArgumentNullException()
    {
        using var tileSurface = CreateCheckerboardTileSurface();
        var tilePaint = new TilePaint(tileSurface, Matrix3x2.Identity, 2f, 2f);
        using var surface = new Surface(1, 1);

        Assert.Throws<ArgumentNullException>(() => PathFiller.Fill(surface, null!, tilePaint));
    }

    /// <summary>
    ///     Proves that the <see cref="TilePaint"/> overload throws ArgumentNullException when the
    ///     paint argument is <see langword="null"/>.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_TilePaint_NullPaint_ThrowsArgumentNullException()
    {
        using var surface = new Surface(1, 1);

        Assert.Throws<ArgumentNullException>(() => PathFiller.Fill(surface, Path.Empty, (TilePaint)null!));
    }

    /// <summary>
    ///     Proves that the <see cref="TilePaint"/> overload is a no-op on an empty path, matching
    ///     the solid-color and gradient overloads' own documented behavior.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_TilePaint_EmptyPath_NoOpLeavesSurfaceUnchanged()
    {
        using var tileSurface = CreateCheckerboardTileSurface();
        var tilePaint = new TilePaint(tileSurface, Matrix3x2.Identity, 2f, 2f);
        using var surface = new Surface(2, 2);

        PathFiller.Fill(surface, Path.Empty, tilePaint);

        Assert.Equal((byte)0, surface[0, 0].A);
    }

    /// <summary>
    ///     Proves that the internal clip-aware <c>Fill(Surface, Path, Rgba32, ClipMask?, FillRule, float)</c>
    ///     overload restricts an otherwise full-surface fill to the active clip mask's own
    ///     covered region, leaving pixels outside the clip mask untouched.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_SolidColorWithClip_RestrictsFillToClipCoverage()
    {
        // Arrange: a full-surface fill path, clipped to a 2x2 square in the top-left corner of a
        // 4x4 surface
        using var surface = new Surface(4, 4);
        var fillPath = new PathBuilder()
            .MoveTo(new Vector2(0, 0)).LineTo(new Vector2(4, 0)).LineTo(new Vector2(4, 4)).LineTo(new Vector2(0, 4))
            .Close().Build();
        var clipPath = new PathBuilder()
            .MoveTo(new Vector2(0, 0)).LineTo(new Vector2(2, 0)).LineTo(new Vector2(2, 2)).LineTo(new Vector2(0, 2))
            .Close().Build();
        var clip = ClipMask.FromPath(clipPath, FillRule.NonZero, 4, 4);
        var color = new Rgba32(255, 0, 0, 255);

        // Act
        PathFiller.Fill(surface, fillPath, color, clip, FillRule.NonZero);

        // Assert: inside the clip, the fill paints
        Assert.Equal((byte)255, surface[0, 0].A);
        Assert.Equal((byte)255, surface[1, 1].A);

        // Assert: outside the clip (but inside the fill path), nothing paints
        Assert.Equal((byte)0, surface[3, 3].A);
        Assert.Equal((byte)0, surface[2, 2].A);
    }

    /// <summary>
    ///     Proves that the internal clip-aware <c>Fill</c> overload with a <see langword="null"/>
    ///     clip argument behaves identically to the public, non-clip-aware overload (no
    ///     restriction is applied).
    /// </summary>
    [Fact]
    public void PathFiller_Fill_SolidColorWithNullClip_BehavesLikePublicOverload()
    {
        // Arrange
        using var surface = new Surface(2, 2);
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0)).LineTo(new Vector2(2, 0)).LineTo(new Vector2(2, 2)).LineTo(new Vector2(0, 2))
            .Close().Build();
        var color = new Rgba32(0, 255, 0, 255);

        // Act
        PathFiller.Fill(surface, path, color, null, FillRule.NonZero);

        // Assert
        Assert.Equal((byte)255, surface[0, 0].A);
        Assert.Equal((byte)255, surface[1, 1].A);
    }

    /// <summary>
    ///     Proves a clip-aware fill through an analytic rectangle clip matches the same fill through
    ///     the equivalent dense clip away from the clip's edge pixels (within one level per
    ///     channel), and that on edge pixels the rectangle result never exceeds the unclipped fill's
    ///     coverage and nothing paints outside the clip's pixel bounds. Covers fractional and
    ///     whole-pixel clip rectangles, including single-column, single-row, and single-pixel clips
    ///     (for which every pixel is an edge pixel, so only the bounded-edge and outside-is-empty
    ///     assertions apply).
    /// </summary>
    [Theory]
    [InlineData(5f, 6f, 25f, 22f)]
    [InlineData(5.3f, 6.6f, 25.4f, 22.2f)]
    [InlineData(7.25f, 3f, 8f, 28f)]
    [InlineData(3f, 9.5f, 29f, 10.25f)]
    [InlineData(7.25f, 9.5f, 7.75f, 10.25f)]
    public void PathFiller_Fill_RectangleClipVersusDenseClip_InteriorMatchesAndEdgesAreBounded(float l, float t, float r, float b)
    {
        // Arrange: a triangle with a (non-crossing) rectangular subpath, filled with both fill rules
        var clipPath = new PathBuilder()
            .MoveTo(new Vector2(l, t)).LineTo(new Vector2(r, t)).LineTo(new Vector2(r, b)).LineTo(new Vector2(l, b))
            .Close().Build();
        var shape = new PathBuilder()
            .MoveTo(new Vector2(2, 3)).LineTo(new Vector2(30, 12.5f)).LineTo(new Vector2(9, 29)).Close()
            .MoveTo(new Vector2(10.3f, 10.2f)).LineTo(new Vector2(18.4f, 10.2f)).LineTo(new Vector2(18.4f, 16.1f)).LineTo(new Vector2(10.3f, 16.1f)).Close()
            .Build();
        var rectClip = ClipMask.FromPath(clipPath, FillRule.NonZero, 32, 32);
        var denseClip = ClipMask.FromPathDense(clipPath, FillRule.NonZero, 32, 32);
        var color = new Rgba32(200, 40, 90, 255);

        foreach (var rule in new[] { FillRule.NonZero, FillRule.EvenOdd })
        {
            using var withRect = new Surface(32, 32);
            using var withDense = new Surface(32, 32);

            // Act
            PathFiller.Fill(withRect, shape, color, rectClip, rule);
            PathFiller.Fill(withDense, shape, color, denseClip, rule);

            // Assert: away from the clip's own edge pixels the two clips are identical within one
            // level per channel. On edge pixels the rectangle clip is now applied geometrically
            // (exact area of fill-intersect-clip), whereas the dense reference multiplies fill
            // coverage by clip coverage (an independence approximation that can be higher or lower
            // than the exact value). The exact value can never exceed the unclipped fill's own
            // coverage, and nothing is painted outside the clip's pixel bounds.
            Assert.True(rectClip.IsRectangle);
            Assert.False(denseClip.IsRectangle);
            using var unclipped = new Surface(32, 32);
            PathFiller.Fill(unclipped, shape, color, null, rule);
            var minX = (int)MathF.Floor(l);
            var maxX = (int)MathF.Ceiling(r) - 1;
            var minY = (int)MathF.Floor(t);
            var maxY = (int)MathF.Ceiling(b) - 1;
            for (var y = 0; y < 32; y++)
            {
                for (var x = 0; x < 32; x++)
                {
                    var p = withRect[x, y];
                    var q = withDense[x, y];
                    var onEdge = x <= minX || x >= maxX || y <= minY || y >= maxY;
                    if (x < minX || x > maxX || y < minY || y > maxY)
                    {
                        Assert.Equal(default, p);
                    }
                    else if (onEdge)
                    {
                        Assert.True(p.A <= unclipped[x, y].A + 1, $"{rule} edge ({x},{y}) rect={p.A} dense={q.A} plain={unclipped[x, y].A}");
                    }
                    else
                    {
                        Assert.InRange(Math.Abs(p.R - q.R), 0, 1);
                        Assert.InRange(Math.Abs(p.G - q.G), 0, 1);
                        Assert.InRange(Math.Abs(p.B - q.B), 0, 1);
                        Assert.True(Math.Abs(p.A - q.A) <= 1, $"{rule} ({x},{y}) rect={p.A} dense={q.A} plain={unclipped[x, y].A}");
                    }
                }
            }
        }
    }

    private static Path RectanglePath(float l, float t, float r, float b) =>
        new PathBuilder()
            .MoveTo(new Vector2(l, t)).LineTo(new Vector2(r, t)).LineTo(new Vector2(r, b)).LineTo(new Vector2(l, b))
            .Close().Build();

    private static double AlphaSum(Surface surface)
    {
        double sum = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                sum += surface[x, y].A / 255.0;
            }
        }

        return sum;
    }

    /// <summary>
    ///     Proves a rectangle clip coincident with the fill's rectangle (fractional edges) does not
    ///     attenuate the edge pixels: edge pixel coverage equals the unclipped fill's, rather than
    ///     its square.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_RectangleClipCoincidentWithFill_DoesNotAttenuateEdges()
    {
        for (var i = 0; i < 50; i++)
        {
            var t = 5 + (i * 0.019f);
            var b = t + 0.4f + (i * 0.021f);
            var path = RectanglePath(3.3f, t, 28.6f, b);
            var clip = ClipMask.FromPath(path, FillRule.NonZero, 32, 32);
            using var plain = new Surface(32, 32);
            using var clipped = new Surface(32, 32);

            PathFiller.Fill(plain, path, new Rgba32(0, 0, 0, 255), null, FillRule.NonZero);
            PathFiller.Fill(clipped, path, new Rgba32(0, 0, 0, 255), clip, FillRule.NonZero);

            Assert.Equal(AlphaSum(plain), AlphaSum(clipped), 1e-9);
            Assert.Equal(plain[3, 5].A, clipped[3, 5].A);
        }
    }

    /// <summary>
    ///     Proves a clip narrower than the fill paints exactly the geometric intersection area
    ///     (within 8-bit quantization), for fractional clip and fill edges.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_NarrowerRectangleClip_PaintsGeometricIntersection()
    {
        var fill = RectanglePath(2.2f, 5.35f, 29.7f, 7.1f);
        var clip = ClipMask.FromPath(RectanglePath(6.4f, 5.9f, 20.3f, 12.5f), FillRule.NonZero, 32, 32);
        using var surface = new Surface(32, 32);

        PathFiller.Fill(surface, fill, new Rgba32(0, 0, 0, 255), clip, FillRule.NonZero);

        var expected = (20.3 - 6.4) * (7.1 - 5.9);
        Assert.Equal(expected, AlphaSum(surface), 0.1);
    }

    /// <summary>
    ///     Proves that for a clip combining a dense mask with a rectangle, the rectangle is not
    ///     counted twice at a coincident fill edge: only one rectangle factor (from the geometry)
    ///     appears, the dense mask contributes its own coverage.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_DenseClipRestrictedByRectangle_AppliesRectangleOnce()
    {
        var circle = new PathBuilder();
        const float Cx = 32;
        const float Cy = 32;
        const float Radius = 30;
        circle.MoveTo(new Vector2(Cx + Radius, Cy));
        for (var i = 1; i <= 96; i++)
        {
            var a = i * MathF.PI * 2 / 96;
            circle.LineTo(new Vector2(Cx + (Radius * MathF.Cos(a)), Cy + (Radius * MathF.Sin(a))));
        }

        var dense = ClipMask.FromPathDense(circle.Close().Build(), FillRule.NonZero, 64, 64);
        var rectPath = RectanglePath(20.5f, 22.5f, 44.25f, 40f);
        var clip = dense.Intersect(ClipMask.FromPath(rectPath, FillRule.NonZero, 64, 64));
        using var surface = new Surface(64, 64);

        PathFiller.Fill(surface, rectPath, new Rgba32(0, 0, 0, 255), clip, FillRule.NonZero);

        // Left edge column 20 has fractional coverage 0.5 (not 0.25), interior is opaque
        Assert.InRange(surface[20, 30].A, 127, 129);
        Assert.Equal(255, surface[30, 30].A);
        Assert.InRange(surface[30, 22].A, 127, 129);
        Assert.Equal(0, surface[19, 30].A);
    }

    private static LinearGradient CreateHorizontalGradient() =>
        new(new Vector2(0, 0), new Vector2(32, 0),
        [
            new GradientStop(0f, new Rgba32(255, 0, 0, 255)),
            new GradientStop(1f, new Rgba32(0, 0, 255, 255)),
        ]);

    private static TilePaint CreateTilePaint()
    {
        var tileSurface = new Surface(2, 2);
        tileSurface[0, 0] = new Rgba32(255, 0, 0, 255);
        tileSurface[1, 0] = new Rgba32(0, 0, 255, 255);
        tileSurface[0, 1] = new Rgba32(0, 0, 255, 255);
        tileSurface[1, 1] = new Rgba32(255, 0, 0, 255);
        return new TilePaint(tileSurface, Matrix3x2.Identity, 2f, 2f);
    }

    /// <summary>
    ///     Proves a gradient fill through a whole-pixel rectangle clip (so the sweep starts at a
    ///     non-zero column) paints exactly the unclipped gradient's pixels inside the clip and
    ///     nothing outside, matching the dense-clip result.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_GradientWithRectangleClip_MatchesUnclippedInsideAndDenseClip()
    {
        var full = RectanglePath(0, 0, 32, 32);
        var clipPath = RectanglePath(8, 4, 20, 12);
        var rectClip = ClipMask.FromPath(clipPath, FillRule.NonZero, 32, 32);
        var denseClip = ClipMask.FromPathDense(clipPath, FillRule.NonZero, 32, 32);
        var gradient = CreateHorizontalGradient();
        using var plain = new Surface(32, 32);
        using var withRect = new Surface(32, 32);
        using var withDense = new Surface(32, 32);

        PathFiller.Fill(plain, full, gradient, null, FillRule.NonZero);
        PathFiller.Fill(withRect, full, gradient, rectClip, FillRule.NonZero);
        PathFiller.Fill(withDense, full, gradient, denseClip, FillRule.NonZero);

        Assert.True(rectClip.IsRectangle);
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                var inside = x >= 8 && x < 20 && y >= 4 && y < 12;
                Assert.Equal(inside ? plain[x, y] : default, withRect[x, y]);
                Assert.Equal(withDense[x, y], withRect[x, y]);
            }
        }
    }

    /// <summary>
    ///     Proves a gradient fill through a fractional rectangle clip paints the geometric
    ///     intersection coverage on the clip's edge pixels (half coverage for a half-pixel edge)
    ///     and the gradient color at the shifted column.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_GradientWithFractionalRectangleClip_PaintsGeometricEdgeCoverage()
    {
        var full = RectanglePath(0, 0, 32, 32);
        var clip = ClipMask.FromPath(RectanglePath(8.5f, 4, 20, 12), FillRule.NonZero, 32, 32);
        using var plain = new Surface(32, 32);
        using var clipped = new Surface(32, 32);

        PathFiller.Fill(plain, full, CreateHorizontalGradient(), null, FillRule.NonZero);
        PathFiller.Fill(clipped, full, CreateHorizontalGradient(), clip, FillRule.NonZero);

        Assert.Equal(0, clipped[7, 6].A);
        Assert.InRange(clipped[8, 6].A, 127, 129);
        Assert.Equal(plain[9, 6], clipped[9, 6]);
        Assert.Equal(plain[19, 6], clipped[19, 6]);
        Assert.Equal(0, clipped[20, 6].A);
    }

    /// <summary>
    ///     Proves a gradient fill whose rectangle is coincident with a fractional rectangle clip
    ///     is not attenuated at the shared edges: total coverage equals the unclipped fill's.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_GradientRectangleClipCoincidentWithFill_DoesNotAttenuateEdges()
    {
        var path = RectanglePath(3.3f, 5.2f, 28.6f, 9.7f);
        var clip = ClipMask.FromPath(path, FillRule.NonZero, 32, 32);
        using var plain = new Surface(32, 32);
        using var clipped = new Surface(32, 32);

        PathFiller.Fill(plain, path, CreateHorizontalGradient(), null, FillRule.NonZero);
        PathFiller.Fill(clipped, path, CreateHorizontalGradient(), clip, FillRule.NonZero);

        Assert.Equal(AlphaSum(plain), AlphaSum(clipped), 1e-9);
        Assert.Equal(plain[3, 6].A, clipped[3, 6].A);
    }

    /// <summary>
    ///     Proves a tile-paint fill through a whole-pixel rectangle clip paints exactly the
    ///     unclipped tile pixels inside the clip and nothing outside, matching the dense-clip
    ///     result.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_TileWithRectangleClip_MatchesUnclippedInsideAndDenseClip()
    {
        var full = RectanglePath(0, 0, 32, 32);
        var clipPath = RectanglePath(7, 5, 19, 13);
        var rectClip = ClipMask.FromPath(clipPath, FillRule.NonZero, 32, 32);
        var denseClip = ClipMask.FromPathDense(clipPath, FillRule.NonZero, 32, 32);
        var paint = CreateTilePaint();
        using var plain = new Surface(32, 32);
        using var withRect = new Surface(32, 32);
        using var withDense = new Surface(32, 32);

        PathFiller.Fill(plain, full, paint, null, FillRule.NonZero);
        PathFiller.Fill(withRect, full, paint, rectClip, FillRule.NonZero);
        PathFiller.Fill(withDense, full, paint, denseClip, FillRule.NonZero);

        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                var inside = x >= 7 && x < 19 && y >= 5 && y < 13;
                Assert.Equal(inside ? plain[x, y] : default, withRect[x, y]);
                Assert.Equal(withDense[x, y], withRect[x, y]);
            }
        }
    }

    /// <summary>
    ///     Proves a tile-paint fill whose rectangle is coincident with a fractional rectangle clip
    ///     is not attenuated at the shared edges: total coverage equals the unclipped fill's.
    /// </summary>
    [Fact]
    public void PathFiller_Fill_TileRectangleClipCoincidentWithFill_DoesNotAttenuateEdges()
    {
        var path = RectanglePath(3.3f, 5.2f, 28.6f, 9.7f);
        var clip = ClipMask.FromPath(path, FillRule.NonZero, 32, 32);
        var paint = CreateTilePaint();
        using var plain = new Surface(32, 32);
        using var clipped = new Surface(32, 32);

        PathFiller.Fill(plain, path, paint, null, FillRule.NonZero);
        PathFiller.Fill(clipped, path, paint, clip, FillRule.NonZero);

        Assert.Equal(AlphaSum(plain), AlphaSum(clipped), 1e-9);
        Assert.Equal(plain[3, 6].A, clipped[3, 6].A);
    }
}



