using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneModel;

/// <summary>
///     Unit tests for <see cref="BoundingBox3D"/> (DoublePrecisionMath unit).
/// </summary>
public class BoundingBox3DTests
{
    /// <summary>
    ///     Validates that the empty box is empty and is the identity of union.
    /// </summary>
    [Fact]
    public void BoundingBox3D_Empty_IsEmptyAndUnionIdentity()
    {
        // Arrange
        var box = new BoundingBox3D(new Vector3D(1, 2, 3), new Vector3D(4, 5, 6));

        // Act / Assert
        Assert.True(BoundingBox3D.Empty.IsEmpty);
        Assert.False(box.IsEmpty);
        Assert.Equal(box, BoundingBox3D.Empty.Union(box));
        Assert.Equal(box, box.Union(BoundingBox3D.Empty));
        Assert.Equal(Vector3D.Zero, BoundingBox3D.Empty.Size);
        Assert.Equal(0.0, BoundingBox3D.Empty.Diagonal);
        Assert.Equal(new BoundingBox3D(new Vector3D(7, 8, 9), new Vector3D(7, 8, 9)), BoundingBox3D.Empty.Union(new Vector3D(7, 8, 9)));
    }

    /// <summary>
    ///     Validates that the union of two boxes covers both.
    /// </summary>
    [Fact]
    public void BoundingBox3D_Union_TwoBoxes_CoversBoth()
    {
        // Arrange
        var a = new BoundingBox3D(new Vector3D(0, 0, 0), new Vector3D(1, 1, 1));
        var b = new BoundingBox3D(new Vector3D(-1, 0.5, 2), new Vector3D(0.5, 3, 4));

        // Act
        var union = a.Union(b);

        // Assert
        Assert.Equal(new Vector3D(-1, 0, 0), union.Min);
        Assert.Equal(new Vector3D(1, 3, 4), union.Max);
        Assert.Equal(new Vector3D(-1, 0, 0), a.Union(new Vector3D(-1, 0, 0)).Min);
    }

    /// <summary>
    ///     Validates that rotating a box by 90 degrees yields the axis-aligned bounds of the
    ///     rotated corners.
    /// </summary>
    [Fact]
    public void BoundingBox3D_Transform_Rotation90_ReturnsAxisAlignedBounds()
    {
        // Arrange: 2 x 4 x 6 box with min at the origin, rotated 90 degrees about Z (x -> y, y -> -x)
        var box = new BoundingBox3D(Vector3D.Zero, new Vector3D(2, 4, 6));

        // Act
        var rotated = box.Transform(Matrix4x4D.CreateRotationZ(90.0));

        // Assert
        Render3DTestHelpers.AssertVector(new Vector3D(-4, 0, 0), rotated.Min);
        Render3DTestHelpers.AssertVector(new Vector3D(0, 2, 6), rotated.Max);
        Assert.True(BoundingBox3D.Empty.Transform(Matrix4x4D.CreateScale(2.0)).IsEmpty);
    }

    /// <summary>
    ///     Validates size, center and diagonal against analytic values.
    /// </summary>
    [Fact]
    public void BoundingBox3D_SizeCenterDiagonal_MatchAnalytic()
    {
        // Arrange: a 3 x 4 x 12 box centered at (1, 2, 3)
        var box = new BoundingBox3D(new Vector3D(-0.5, 0, -3), new Vector3D(2.5, 4, 9));

        // Act / Assert
        Assert.Equal(new Vector3D(3, 4, 12), box.Size);
        Assert.Equal(new Vector3D(1, 2, 3), box.Center);
        Assert.Equal(13.0, box.Diagonal, 1e-12);
    }
}
