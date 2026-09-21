using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;

namespace DemaConsulting.CanvasNet.Tests.Geometry;

/// <summary>
///     Unit tests for the Rect struct.
/// </summary>
public class RectTests
{
    /// <summary>
    ///     Proves that the constructor stores the supplied position and size on the corresponding
    ///     properties, and that the computed edge/corner properties derive correctly from them.
    /// </summary>
    [Fact]
    public void Rect_Constructor_ValidValues_SetsPropertiesAndComputedEdges()
    {
        // Arrange & Act: construct a rectangle with known position and size
        var rect = new Rect(10, 20, 30, 40);

        // Assert: stored fields and every computed edge/corner property
        Assert.Equal(10, rect.X);
        Assert.Equal(20, rect.Y);
        Assert.Equal(30, rect.Width);
        Assert.Equal(40, rect.Height);
        Assert.Equal(10, rect.Left);
        Assert.Equal(20, rect.Top);
        Assert.Equal(40, rect.Right);
        Assert.Equal(60, rect.Bottom);
        Assert.Equal(new Vector2(10, 20), rect.Location);
        Assert.Equal(new Vector2(30, 40), rect.Size);
        Assert.Equal(new Vector2(10, 20), rect.TopLeft);
        Assert.Equal(new Vector2(40, 60), rect.BottomRight);
    }

    /// <summary>
    ///     Proves that a rectangle constructed with a positive width is not IsEmpty, and that
    ///     Rect.Empty is IsEmpty.
    /// </summary>
    [Fact]
    public void Rect_IsEmpty_EmptySentinelVersusRealRectangle_ReturnsExpectedValue()
    {
        // Arrange
        var real = new Rect(0, 0, 1, 1);

        // Act & Assert
        Assert.False(real.IsEmpty);
        Assert.True(Rect.Empty.IsEmpty);
    }

    /// <summary>
    ///     Proves that unioning Rect.Empty with any real rectangle is an identity operation,
    ///     returning exactly the real rectangle - the load-bearing property that makes
    ///     Path.GetBounds() correct for a path with zero or more subpaths.
    /// </summary>
    [Fact]
    public void Rect_Union_WithEmpty_ReturnsOtherRectangleUnchanged()
    {
        // Arrange
        var real = new Rect(3, 4, 5, 6);

        // Act
        var left = Rect.Union(Rect.Empty, real);
        var right = Rect.Union(real, Rect.Empty);

        // Assert: Empty is a true identity element on both sides
        Assert.Equal(real, left);
        Assert.Equal(real, right);
    }

    /// <summary>
    ///     Proves that Contains uses a half-open interval: the left/top edges are included, while
    ///     the right/bottom edges are excluded.
    /// </summary>
    [Fact]
    public void Rect_Contains_BoundaryPoints_ReturnsExpectedValue()
    {
        // Arrange: a 10x10 rectangle at the origin
        var rect = new Rect(0, 0, 10, 10);

        // Act & Assert: top-left corner is included, bottom-right corner is excluded
        Assert.True(rect.Contains(new Vector2(0, 0)));
        Assert.True(rect.Contains(new Vector2(9.999f, 9.999f)));
        Assert.False(rect.Contains(new Vector2(10, 0)));
        Assert.False(rect.Contains(new Vector2(0, 10)));
        Assert.False(rect.Contains(new Vector2(10, 10)));
    }

    /// <summary>
    ///     Proves that Union computes the smallest rectangle enclosing two overlapping
    ///     rectangles, matching a hand-computed expected result.
    /// </summary>
    [Fact]
    public void Rect_Union_TwoOverlappingRectangles_ReturnsSmallestEnclosingRectangle()
    {
        // Arrange: two overlapping rectangles
        var a = new Rect(0, 0, 10, 10);
        var b = new Rect(5, 5, 10, 10);

        // Act
        var union = a.Union(b);

        // Assert: hand-computed enclosing rectangle spans (0,0) to (15,15)
        Assert.Equal(new Rect(0, 0, 15, 15), union);
    }

    /// <summary>
    ///     Proves that Intersect computes the overlapping region of two overlapping rectangles,
    ///     matching a hand-computed expected result.
    /// </summary>
    [Fact]
    public void Rect_Intersect_TwoOverlappingRectangles_ReturnsOverlapRegion()
    {
        // Arrange: two overlapping rectangles
        var a = new Rect(0, 0, 10, 10);
        var b = new Rect(5, 5, 10, 10);

        // Act
        var intersection = a.Intersect(b);

        // Assert: hand-computed overlap spans (5,5) to (10,10)
        Assert.Equal(new Rect(5, 5, 5, 5), intersection);
    }

    /// <summary>
    ///     Proves that Intersect returns Rect.Empty for two disjoint rectangles that do not
    ///     overlap at all.
    /// </summary>
    [Fact]
    public void Rect_Intersect_DisjointRectangles_ReturnsEmpty()
    {
        // Arrange: two rectangles that do not overlap
        var a = new Rect(0, 0, 5, 5);
        var b = new Rect(10, 10, 5, 5);

        // Act
        var intersection = a.Intersect(b);

        // Assert
        Assert.Equal(Rect.Empty, intersection);
    }

    /// <summary>
    ///     Proves that Intersect returns Rect.Empty for two rectangles that merely touch along an
    ///     edge (share a boundary line but no interior area) on either axis, rather than a
    ///     zero-width or zero-height non-Empty rectangle. Regression test for a bug where the
    ///     disjoint comparisons used strict "&lt;" instead of "&lt;=", so edge-touching
    ///     rectangles produced a zero-extent rectangle that was not recognized as Empty.
    /// </summary>
    [Fact]
    public void Rect_Intersect_RectanglesTouchingAlongVerticalEdge_ReturnsEmpty()
    {
        // Arrange: b's left edge exactly equals a's right edge (touching, not overlapping)
        var a = new Rect(0, 0, 10, 10);
        var b = new Rect(10, 0, 10, 10);

        // Act
        var intersection = a.Intersect(b);

        // Assert
        Assert.Equal(Rect.Empty, intersection);
        Assert.True(intersection.IsEmpty);
    }

    /// <summary>
    ///     Proves that Intersect returns Rect.Empty for two rectangles that merely touch along a
    ///     horizontal edge (b's top edge exactly equals a's bottom edge).
    /// </summary>
    [Fact]
    public void Rect_Intersect_RectanglesTouchingAlongHorizontalEdge_ReturnsEmpty()
    {
        // Arrange: b's top edge exactly equals a's bottom edge (touching, not overlapping)
        var a = new Rect(0, 0, 10, 10);
        var b = new Rect(0, 10, 10, 10);

        // Act
        var intersection = a.Intersect(b);

        // Assert
        Assert.Equal(Rect.Empty, intersection);
        Assert.True(intersection.IsEmpty);
    }

    /// <summary>
    ///     Proves that Transform under a pure translation matrix simply offsets the rectangle's
    ///     position, leaving its size unchanged.
    /// </summary>
    [Fact]
    public void Rect_Transform_Translation_OffsetsPosition()
    {
        // Arrange
        var rect = new Rect(0, 0, 10, 10);
        var matrix = Matrix3x2.CreateTranslation(5, 7);

        // Act
        var transformed = rect.Transform(matrix);

        // Assert
        Assert.Equal(new Rect(5, 7, 10, 10), transformed);
    }

    /// <summary>
    ///     Proves that Transform under a 90-degree rotation about the origin correctly
    ///     re-encloses the four transformed corners into an axis-aligned bounding box, rather
    ///     than naively transforming just two opposite corners (which would produce a wrong
    ///     result for a rotated rectangle whose original width and height differ).
    /// </summary>
    [Fact]
    public void Rect_Transform_Rotation90DegreesAboutOrigin_ReturnsHandComputedAabb()
    {
        // Arrange: a wide, short rectangle whose corners are (0,0),(10,0),(0,2),(10,2)
        var rect = new Rect(0, 0, 10, 2);
        var matrix = Matrix3x2.CreateRotation(MathF.PI / 2f);

        // Act: rotate 90 degrees about the origin
        var transformed = rect.Transform(matrix);

        // Assert: rotating 90 degrees maps (x,y) -> (-y,x), so the four corners become
        // (0,0), (0,10), (-2,0), (-2,10); the enclosing AABB is X=-2, Y=0, Width=2, Height=10
        Assert.Equal(-2, transformed.X, 3);
        Assert.Equal(0, transformed.Y, 3);
        Assert.Equal(2, transformed.Width, 3);
        Assert.Equal(10, transformed.Height, 3);
    }

    /// <summary>
    ///     Proves that transforming Rect.Empty returns Empty rather than a NaN-filled rectangle:
    ///     Empty's corners include +/-Infinity, and transforming them directly (e.g. via a matrix
    ///     with a zero off-diagonal element) can produce 0 * Infinity = NaN, which would then
    ///     poison the Min/Max re-enclosure fold.
    /// </summary>
    [Theory]
    [InlineData(0, 0)] // Identity
    [InlineData(1, 0)] // Translation
    public void Rect_Transform_Empty_ReturnsEmpty(float tx, float ty)
    {
        // Arrange
        var matrix = Matrix3x2.CreateTranslation(tx, ty);

        // Act
        var transformed = Rect.Empty.Transform(matrix);

        // Assert
        Assert.Equal(Rect.Empty, transformed);
        Assert.True(transformed.IsEmpty);
    }

    /// <summary>
    ///     Proves that transforming Rect.Empty under a rotation (which introduces zero
    ///     off-diagonal matrix elements) still returns Empty rather than NaN.
    /// </summary>
    [Fact]
    public void Rect_Transform_Empty_UnderRotation_ReturnsEmpty()
    {
        // Arrange
        var matrix = Matrix3x2.CreateRotation(MathF.PI / 4f);

        // Act
        var transformed = Rect.Empty.Transform(matrix);

        // Assert
        Assert.Equal(Rect.Empty, transformed);
        Assert.True(transformed.IsEmpty);
    }

    /// <summary>
    ///     Proves that two rectangles with identical field values compare equal via Equals and
    ///     the == / != operators, and that a differing rectangle compares unequal.
    /// </summary>
    [Fact]
    public void Rect_Equals_SameAndDifferentValues_ReturnsExpectedResult()
    {
        // Arrange
        var a = new Rect(1, 2, 3, 4);
        var b = new Rect(1, 2, 3, 4);
        var c = new Rect(1, 2, 3, 5);

        // Act & Assert
        Assert.True(a.Equals(b));
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.False(a.Equals(c));
        Assert.True(a != c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    /// <summary>
    ///     Proves that ToString produces a non-empty, human-readable representation containing
    ///     each field's value.
    /// </summary>
    [Fact]
    public void Rect_ToString_ReturnsStringContainingFieldValues()
    {
        // Arrange
        var rect = new Rect(1, 2, 3, 4);

        // Act
        var text = rect.ToString();

        // Assert
        Assert.Contains("1", text);
        Assert.Contains("2", text);
        Assert.Contains("3", text);
        Assert.Contains("4", text);
    }
}
