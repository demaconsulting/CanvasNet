using System.Numerics;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Tests.Drawing;

/// <summary>
///     Unit tests for <see cref="ClipMask"/>.
/// </summary>
public class ClipMaskTests
{
    /// <summary>
    ///     Proves that <see cref="ClipMask.FromPath"/> rasterizes an axis-aligned rectangle's
    ///     coverage identically to the way <see cref="PathFiller"/> itself would fill it: fully
    ///     opaque (coverage <c>1f</c>) strictly inside, and zero outside.
    /// </summary>
    [Fact]
    public void ClipMask_FromPath_AxisAlignedRectangle_InteriorFullCoverageExteriorZero()
    {
        // Arrange: a rectangle from (2, 2) to (6, 6) on a 10x10 extent
        var path = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .LineTo(new Vector2(6, 2))
            .LineTo(new Vector2(6, 6))
            .LineTo(new Vector2(2, 6))
            .Close()
            .Build();

        // Act
        var mask = ClipMask.FromPath(path, FillRule.NonZero, 10, 10);

        // Assert: interior pixel fully covered
        Assert.Equal(1f, mask.GetCoverage(3, 3));

        // Assert: exterior pixel not covered
        Assert.Equal(0f, mask.GetCoverage(8, 8));

        // Assert: dimensions match the requested extent
        Assert.Equal(10, mask.Width);
        Assert.Equal(10, mask.Height);
    }

    /// <summary>
    ///     Proves that <see cref="ClipMask.FromPath"/> interprets two identically wound,
    ///     overlapping rectangles differently under <see cref="FillRule.NonZero"/> (overlap
    ///     remains covered) versus <see cref="FillRule.EvenOdd"/> (overlap becomes a hole), the
    ///     same divergence <see cref="PathFiller"/> itself exhibits for ordinary fills.
    /// </summary>
    [Fact]
    public void ClipMask_FromPath_OverlappingSameWoundRectangles_NonZeroVsEvenOddDiverge()
    {
        // Arrange: two identically wound, integer-aligned, overlapping 4x4 rectangles on a 8x8
        // extent, so the overlap pixel has an exact (non-antialiased) winding count of 2
        Path BuildOverlappingRectangles()
        {
            var builder = new PathBuilder();
            builder.MoveTo(new Vector2(0, 0)).LineTo(new Vector2(4, 0)).LineTo(new Vector2(4, 4)).LineTo(new Vector2(0, 4)).Close();
            builder.MoveTo(new Vector2(2, 2)).LineTo(new Vector2(6, 2)).LineTo(new Vector2(6, 6)).LineTo(new Vector2(2, 6)).Close();
            return builder.Build();
        }

        var path = BuildOverlappingRectangles();

        // Act
        var nonZeroMask = ClipMask.FromPath(path, FillRule.NonZero, 8, 8);
        var evenOddMask = ClipMask.FromPath(path, FillRule.EvenOdd, 8, 8);

        // Assert: NonZero keeps the overlap (winding 2) fully covered
        Assert.Equal(1f, nonZeroMask.GetCoverage(3, 3));

        // Assert: EvenOdd folds the overlap (winding 2) to a hole
        Assert.Equal(0f, evenOddMask.GetCoverage(3, 3));

        // Assert: both rules agree the non-overlapping region (winding 1) is covered
        Assert.Equal(1f, nonZeroMask.GetCoverage(1, 1));
        Assert.Equal(1f, evenOddMask.GetCoverage(1, 1));
    }

    /// <summary>
    ///     Proves that <see cref="ClipMask.FromPath"/> throws <see cref="ArgumentNullException"/>
    ///     when the path argument is <see langword="null"/>.
    /// </summary>
    [Fact]
    public void ClipMask_FromPath_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ClipMask.FromPath(null!, FillRule.NonZero, 4, 4));
    }

    /// <summary>
    ///     Proves that <see cref="ClipMask.FromPath"/> throws <see cref="ArgumentOutOfRangeException"/>
    ///     when <c>width</c> is not greater than zero.
    /// </summary>
    [Fact]
    public void ClipMask_FromPath_NonPositiveWidth_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ClipMask.FromPath(Path.Empty, FillRule.NonZero, 0, 4));
    }

    /// <summary>
    ///     Proves that <see cref="ClipMask.FromPath"/> throws <see cref="ArgumentOutOfRangeException"/>
    ///     when <c>height</c> is not greater than zero.
    /// </summary>
    [Fact]
    public void ClipMask_FromPath_NonPositiveHeight_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ClipMask.FromPath(Path.Empty, FillRule.NonZero, 4, 0));
    }

    /// <summary>
    ///     Proves that <see cref="ClipMask.FromPath"/> with an empty path produces an all-zero
    ///     coverage mask spanning the requested extent, rather than throwing.
    /// </summary>
    [Fact]
    public void ClipMask_FromPath_EmptyPath_AllZeroCoverage()
    {
        var mask = ClipMask.FromPath(Path.Empty, FillRule.NonZero, 4, 4);

        Assert.Equal(0f, mask.GetCoverage(0, 0));
        Assert.Equal(0f, mask.GetCoverage(3, 3));
    }

    /// <summary>
    ///     Proves that <see cref="ClipMask.Intersect"/> returns a new mask whose coverage at
    ///     every pixel is the elementwise product of the two operands' own coverage, computing
    ///     the geometric intersection of two overlapping (but not identical) rectangles.
    /// </summary>
    [Fact]
    public void ClipMask_Intersect_OverlappingRectangles_ProducesGeometricIntersection()
    {
        // Arrange: rect A = (0,0)-(5,5), rect B = (3,3)-(8,8), both on a 8x8 extent
        var pathA = new PathBuilder()
            .MoveTo(new Vector2(0, 0)).LineTo(new Vector2(5, 0)).LineTo(new Vector2(5, 5)).LineTo(new Vector2(0, 5))
            .Close().Build();
        var pathB = new PathBuilder()
            .MoveTo(new Vector2(3, 3)).LineTo(new Vector2(8, 3)).LineTo(new Vector2(8, 8)).LineTo(new Vector2(3, 8))
            .Close().Build();
        var maskA = ClipMask.FromPath(pathA, FillRule.NonZero, 8, 8);
        var maskB = ClipMask.FromPath(pathB, FillRule.NonZero, 8, 8);

        // Act
        var intersected = maskA.Intersect(maskB);

        // Assert: pixel in the geometric overlap (A ∩ B) is covered
        Assert.Equal(1f, intersected.GetCoverage(4, 4));

        // Assert: pixel only in A (not B) is not covered by the intersection
        Assert.Equal(0f, intersected.GetCoverage(1, 1));

        // Assert: pixel only in B (not A) is not covered by the intersection
        Assert.Equal(0f, intersected.GetCoverage(6, 6));

        // Assert: pixel in neither remains uncovered
        Assert.Equal(0f, intersected.GetCoverage(7, 1));
    }

    /// <summary>
    ///     Proves that <see cref="ClipMask.Intersect"/> never mutates either operand - both
    ///     masks retain their own original, pre-intersection coverage after the call.
    /// </summary>
    [Fact]
    public void ClipMask_Intersect_DoesNotMutateEitherOperand()
    {
        var pathA = new PathBuilder()
            .MoveTo(new Vector2(0, 0)).LineTo(new Vector2(5, 0)).LineTo(new Vector2(5, 5)).LineTo(new Vector2(0, 5))
            .Close().Build();
        var pathB = new PathBuilder()
            .MoveTo(new Vector2(3, 3)).LineTo(new Vector2(8, 3)).LineTo(new Vector2(8, 8)).LineTo(new Vector2(3, 8))
            .Close().Build();
        var maskA = ClipMask.FromPath(pathA, FillRule.NonZero, 8, 8);
        var maskB = ClipMask.FromPath(pathB, FillRule.NonZero, 8, 8);

        _ = maskA.Intersect(maskB);

        // Assert: maskA still reports its own, unmodified full coverage at (1, 1)
        Assert.Equal(1f, maskA.GetCoverage(1, 1));

        // Assert: maskB still reports its own, unmodified full coverage at (6, 6)
        Assert.Equal(1f, maskB.GetCoverage(6, 6));
    }

    /// <summary>
    ///     Proves that <see cref="ClipMask.Intersect"/> throws <see cref="ArgumentNullException"/>
    ///     when the other mask argument is <see langword="null"/>.
    /// </summary>
    [Fact]
    public void ClipMask_Intersect_NullOther_ThrowsArgumentNullException()
    {
        var mask = ClipMask.FromPath(Path.Empty, FillRule.NonZero, 4, 4);

        Assert.Throws<ArgumentNullException>(() => mask.Intersect(null!));
    }

    /// <summary>
    ///     Proves that <see cref="ClipMask.Intersect"/> throws <see cref="ArgumentException"/>
    ///     when the two masks cover different device pixel extents.
    /// </summary>
    [Fact]
    public void ClipMask_Intersect_MismatchedExtents_ThrowsArgumentException()
    {
        var maskA = ClipMask.FromPath(Path.Empty, FillRule.NonZero, 4, 4);
        var maskB = ClipMask.FromPath(Path.Empty, FillRule.NonZero, 8, 8);

        Assert.Throws<ArgumentException>(() => maskA.Intersect(maskB));
    }

    /// <summary>
    ///     Proves that <see cref="ClipMask.GetCoverage"/> returns <c>0f</c> for coordinates
    ///     outside the mask's own <see cref="ClipMask.Width"/> x <see cref="ClipMask.Height"/>
    ///     extent, including negative coordinates, rather than throwing.
    /// </summary>
    [Fact]
    public void ClipMask_GetCoverage_OutOfBoundsCoordinates_ReturnsZero()
    {
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0)).LineTo(new Vector2(4, 0)).LineTo(new Vector2(4, 4)).LineTo(new Vector2(0, 4))
            .Close().Build();
        var mask = ClipMask.FromPath(path, FillRule.NonZero, 4, 4);

        Assert.Equal(0f, mask.GetCoverage(-1, 0));
        Assert.Equal(0f, mask.GetCoverage(0, -1));
        Assert.Equal(0f, mask.GetCoverage(4, 0));
        Assert.Equal(0f, mask.GetCoverage(0, 4));
    }
}
