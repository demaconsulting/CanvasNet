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
    ///     Proves <see cref="ClipMask.Bounds"/> reports the clip path's own whole-pixel bounding
    ///     box, not the full device extent.
    /// </summary>
    [Fact]
    public void ClipMask_Bounds_SmallRectangle_ReportsPathBoundingBox()
    {
        // Arrange: a rectangle from (2.5, 3) to (6, 7) on a 100x100 extent
        var path = new PathBuilder()
            .MoveTo(new Vector2(2.5f, 3))
            .LineTo(new Vector2(6, 3))
            .LineTo(new Vector2(6, 7))
            .LineTo(new Vector2(2.5f, 7))
            .Close()
            .Build();

        // Act
        var bounds = ClipMask.FromPath(path, FillRule.NonZero, 100, 100).Bounds;

        // Assert: outward-rounded to whole pixels
        Assert.Equal(2f, bounds.Left);
        Assert.Equal(3f, bounds.Top);
        Assert.Equal(6f, bounds.Right);
        Assert.Equal(7f, bounds.Bottom);
    }

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

    #region Analytic rectangle representation

    private static Path RectPath(float l, float t, float r, float b)
        => new PathBuilder()
            .MoveTo(new Vector2(l, t)).LineTo(new Vector2(r, t)).LineTo(new Vector2(r, b)).LineTo(new Vector2(l, b))
            .Close().Build();

    private static Path CirclePath(float cx, float cy, float radius)
    {
        var builder = new PathBuilder();
        const int segments = 24;
        for (var i = 0; i < segments; i++)
        {
            var angle = i * 2 * MathF.PI / segments;
            var p = new Vector2(cx + (radius * MathF.Cos(angle)), cy + (radius * MathF.Sin(angle)));
            _ = i == 0 ? builder.MoveTo(p) : builder.LineTo(p);
        }

        return builder.Close().Build();
    }

    private static void AssertSameCoverage(ClipMask expected, ClipMask actual, int width, int height)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                Assert.Equal(expected.GetCoverage(x, y), actual.GetCoverage(x, y), 1e-4f);
            }
        }
    }

    /// <summary>Proves an axis-aligned rectangle is represented analytically and matches the dense reference.</summary>
    [Fact]
    public void ClipMask_FromPath_AxisAlignedRectangle_IsRectangleMatchingDenseReference()
    {
        var path = RectPath(10.25f, 20.5f, 40.75f, 50.25f);

        var mask = ClipMask.FromPath(path, FillRule.NonZero, 64, 64);
        var reference = ClipMask.FromPathDense(path, FillRule.NonZero, 64, 64);

        Assert.True(mask.IsRectangle);
        Assert.False(reference.IsRectangle);
        AssertSameCoverage(reference, mask, 64, 64);
    }

    /// <summary>Proves fractional edges give fractional boundary coverage.</summary>
    [Fact]
    public void ClipMask_FromPath_FractionalEdges_BoundaryCoverageIsFractional()
    {
        var mask = ClipMask.FromPath(RectPath(10.25f, 20.5f, 40.75f, 50.25f), FillRule.NonZero, 64, 64);

        Assert.Equal(0.75f, mask.GetCoverage(10, 30), 1e-4f);
        Assert.Equal(0.75f, mask.GetCoverage(40, 30), 1e-4f);
        Assert.Equal(0.5f, mask.GetCoverage(20, 20), 1e-4f);
        Assert.Equal(0.25f, mask.GetCoverage(20, 50), 1e-4f);
        Assert.Equal(1f, mask.GetCoverage(20, 30), 1e-4f);
    }

    /// <summary>Proves a rotated rectangle is not treated as an analytic rectangle.</summary>
    [Fact]
    public void ClipMask_FromPath_RotatedRectangle_StaysDense()
    {
        var path = new PathBuilder()
            .MoveTo(new Vector2(10, 20)).LineTo(new Vector2(30, 10)).LineTo(new Vector2(40, 30)).LineTo(new Vector2(20, 40))
            .Close().Build();

        var mask = ClipMask.FromPath(path, FillRule.NonZero, 64, 64);

        Assert.False(mask.IsRectangle);
    }

    /// <summary>Proves non-rectangular shapes (circle, L shape, two subpaths) stay dense.</summary>
    [Fact]
    public void ClipMask_FromPath_NonRectangles_StayDense()
    {
        var lShape = new PathBuilder()
            .MoveTo(new Vector2(0, 0)).LineTo(new Vector2(20, 0)).LineTo(new Vector2(20, 10))
            .LineTo(new Vector2(10, 10)).LineTo(new Vector2(10, 20)).LineTo(new Vector2(0, 20))
            .Close().Build();
        var twoRects = new PathBuilder()
            .MoveTo(new Vector2(0, 0)).LineTo(new Vector2(5, 0)).LineTo(new Vector2(5, 5)).LineTo(new Vector2(0, 5)).Close()
            .MoveTo(new Vector2(10, 10)).LineTo(new Vector2(15, 10)).LineTo(new Vector2(15, 15)).LineTo(new Vector2(10, 15)).Close()
            .Build();

        Assert.False(ClipMask.FromPath(CirclePath(20, 20, 10), FillRule.NonZero, 64, 64).IsRectangle);
        Assert.False(ClipMask.FromPath(lShape, FillRule.NonZero, 64, 64).IsRectangle);
        Assert.False(ClipMask.FromPath(twoRects, FillRule.NonZero, 64, 64).IsRectangle);
    }

    /// <summary>Proves an even-odd rectangle and a clockwise/counter-clockwise rectangle are both detected.</summary>
    [Fact]
    public void ClipMask_FromPath_EvenOddAndReverseWinding_AreRectangles()
    {
        var reversed = new PathBuilder()
            .MoveTo(new Vector2(5, 5)).LineTo(new Vector2(5, 15)).LineTo(new Vector2(25, 15)).LineTo(new Vector2(25, 5))
            .Close().Build();

        var evenOdd = ClipMask.FromPath(RectPath(5, 5, 25, 15), FillRule.EvenOdd, 64, 64);
        var reverse = ClipMask.FromPath(reversed, FillRule.NonZero, 64, 64);

        Assert.True(evenOdd.IsRectangle);
        Assert.True(reverse.IsRectangle);
        Assert.Equal(1f, evenOdd.GetCoverage(10, 10));
        Assert.Equal(1f, reverse.GetCoverage(10, 10));
    }

    /// <summary>Proves a rectangle partly off the surface is clamped to the surface.</summary>
    [Fact]
    public void ClipMask_FromPath_RectangleOffSurface_IsClampedOrEmpty()
    {
        var clamped = ClipMask.FromPath(RectPath(-10, -10, 20, 20), FillRule.NonZero, 64, 64);
        var outside = ClipMask.FromPath(RectPath(100, 100, 120, 120), FillRule.NonZero, 64, 64);

        Assert.Equal(1f, clamped.GetCoverage(0, 0));
        Assert.Equal(0f, clamped.GetCoverage(20, 20));
        Assert.True(outside.IsEmpty);
        Assert.Equal(0f, outside.GetCoverage(10, 10));
    }

    /// <summary>Proves rectangle intersect rectangle yields a rectangle equal to the dense reference.</summary>
    [Theory]
    [InlineData(10f, 10f, 40f, 40f, 20f, 25f, 60f, 50f)]
    [InlineData(10.25f, 10.5f, 40.75f, 40.25f, 20.5f, 25.25f, 60.5f, 50.75f)]
    public void ClipMask_Intersect_RectangleWithRectangle_IsRectangleEqualToDenseReference(
        float al, float at, float ar, float ab, float bl, float bt, float br, float bb)
    {
        var a = ClipMask.FromPath(RectPath(al, at, ar, ab), FillRule.NonZero, 64, 64);
        var b = ClipMask.FromPath(RectPath(bl, bt, br, bb), FillRule.NonZero, 64, 64);
        var reference = ClipMask.FromPathDense(RectPath(al, at, ar, ab), FillRule.NonZero, 64, 64)
            .Intersect(ClipMask.FromPathDense(RectPath(bl, bt, br, bb), FillRule.NonZero, 64, 64));

        var result = a.Intersect(b);

        Assert.True(result.IsRectangle);
        Assert.Null(result.DenseBuffer);
        AssertSameCoverage(reference, result, 64, 64);
    }

    /// <summary>Proves disjoint rectangles intersect to an empty mask.</summary>
    [Fact]
    public void ClipMask_Intersect_DisjointRectangles_IsEmpty()
    {
        var a = ClipMask.FromPath(RectPath(0, 0, 10, 10), FillRule.NonZero, 64, 64);
        var b = ClipMask.FromPath(RectPath(20, 20, 30, 30), FillRule.NonZero, 64, 64);

        Assert.True(a.Intersect(b).IsEmpty);
    }

    /// <summary>Proves a full-surface rectangle clip is reported as no clip.</summary>
    [Fact]
    public void ClipMask_FromPathOrNull_FullSurfaceRectangle_ReturnsNull()
    {
        var full = RectPath(0.00000912f, 0, 612, 792);
        var almost = RectPath(0, 0, 611, 792);

        Assert.Null(ClipMask.FromPathOrNull(full, FillRule.NonZero, 612, 792));
        Assert.NotNull(ClipMask.FromPathOrNull(almost, FillRule.NonZero, 612, 792));
        Assert.Null(ClipMask.Combine(null, null));
    }

    /// <summary>Proves combining with a full-surface rectangle or with null leaves the other clip unchanged.</summary>
    [Fact]
    public void ClipMask_Combine_FullSurfaceAndNull_NormalizesToOtherOperandOrNull()
    {
        var partial = ClipMask.FromPath(RectPath(5, 5, 30, 30), FillRule.NonZero, 64, 64);
        var full = ClipMask.FromPath(RectPath(0, 0, 64, 64), FillRule.NonZero, 64, 64);

        Assert.True(full.IsFullSurface);
        Assert.Same(partial, ClipMask.Combine(partial, null));
        Assert.Same(partial, ClipMask.Combine(null, partial));
        Assert.Null(ClipMask.Combine(full, null));
        Assert.Null(ClipMask.Combine(null, full));
        Assert.Equal(partial.GetCoverage(10, 10), ClipMask.Combine(partial, full)!.GetCoverage(10, 10));
        Assert.Equal(0f, ClipMask.Combine(partial, full)!.GetCoverage(40, 40));
    }

    /// <summary>Proves rectangle/dense intersections (either order) equal the dense reference and share the buffer.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClipMask_Intersect_RectangleWithDense_EqualsDenseReferenceAndSharesBuffer(bool rectFirst)
    {
        var circle = CirclePath(30, 30, 18);
        var rect = RectPath(15.3f, 20.6f, 45.7f, 55.2f);
        var dense = ClipMask.FromPath(circle, FillRule.NonZero, 64, 64);
        var rectMask = ClipMask.FromPath(rect, FillRule.NonZero, 64, 64);
        var reference = ClipMask.FromPathDense(circle, FillRule.NonZero, 64, 64)
            .Intersect(ClipMask.FromPathDense(rect, FillRule.NonZero, 64, 64));

        var result = rectFirst ? rectMask.Intersect(dense) : dense.Intersect(rectMask);

        Assert.True(rectMask.IsRectangle);
        Assert.False(dense.IsRectangle);
        Assert.Same(dense.DenseBuffer, result.DenseBuffer);
        AssertSameCoverage(reference, result, 64, 64);
    }

    /// <summary>Proves dense/dense intersection equals the reference and is bounded to the overlap.</summary>
    [Fact]
    public void ClipMask_Intersect_DenseWithDense_EqualsReferenceAndBoundsAreOverlap()
    {
        var a = ClipMask.FromPath(CirclePath(25, 30, 15), FillRule.NonZero, 64, 64);
        var b = ClipMask.FromPath(CirclePath(40, 30, 15), FillRule.NonZero, 64, 64);

        var result = a.Intersect(b);

        var expectedLeft = Math.Max(a.Bounds.Left, b.Bounds.Left);
        var expectedRight = Math.Min(a.Bounds.Right, b.Bounds.Right);
        Assert.Equal(expectedLeft, result.Bounds.Left);
        Assert.Equal(expectedRight, result.Bounds.Right);
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                Assert.Equal(a.GetCoverage(x, y) * b.GetCoverage(x, y), result.GetCoverage(x, y), 1e-4f);
            }
        }
    }

    /// <summary>Proves dense/dense intersection folds in prior rectangle restrictions.</summary>
    [Fact]
    public void ClipMask_Intersect_RestrictedDenseWithDense_EqualsReference()
    {
        var circleA = CirclePath(25, 30, 15);
        var circleB = CirclePath(35, 32, 15);
        var rect = RectPath(18.5f, 20.25f, 44.5f, 50.75f);
        var restricted = ClipMask.FromPath(circleA, FillRule.NonZero, 64, 64)
            .Intersect(ClipMask.FromPath(rect, FillRule.NonZero, 64, 64));
        var reference = ClipMask.FromPathDense(circleA, FillRule.NonZero, 64, 64)
            .Intersect(ClipMask.FromPathDense(rect, FillRule.NonZero, 64, 64))
            .Intersect(ClipMask.FromPathDense(circleB, FillRule.NonZero, 64, 64));

        var result = restricted.Intersect(ClipMask.FromPath(circleB, FillRule.NonZero, 64, 64));

        AssertSameCoverage(reference, result, 64, 64);
    }

    #endregion
}
