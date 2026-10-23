using System.Numerics;
using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneModel;

/// <summary>
///     Unit tests for <see cref="Vector3D"/> (DoublePrecisionMath unit).
/// </summary>
public class Vector3DTests
{
    /// <summary>
    ///     Validates that addition, subtraction, negation, scalar multiplication and division
    ///     act component-wise.
    /// </summary>
    [Fact]
    public void Vector3D_Operators_AddSubtractScale_ComponentWise()
    {
        // Arrange
        var a = new Vector3D(1.0, 2.0, 3.0);
        var b = new Vector3D(4.0, -5.0, 0.5);

        // Act / Assert
        Assert.Equal(new Vector3D(5.0, -3.0, 3.5), a + b);
        Assert.Equal(new Vector3D(-3.0, 7.0, 2.5), a - b);
        Assert.Equal(new Vector3D(-1.0, -2.0, -3.0), -a);
        Assert.Equal(new Vector3D(2.0, 4.0, 6.0), a * 2.0);
        Assert.Equal(new Vector3D(2.0, 4.0, 6.0), 2.0 * a);
        Assert.Equal(new Vector3D(0.5, 1.0, 1.5), a / 2.0);
        Assert.Equal(new Vector3D(1.0, -5.0, 0.5), Vector3D.Min(a, b));
        Assert.Equal(new Vector3D(4.0, 2.0, 3.0), Vector3D.Max(a, b));
    }

    /// <summary>
    ///     Validates that the dot product of orthogonal vectors is exactly zero.
    /// </summary>
    [Fact]
    public void Vector3D_Dot_OrthogonalVectors_ReturnsZero()
    {
        // Arrange / Act
        var dot = Vector3D.Dot(Vector3D.UnitX, Vector3D.UnitY);

        // Assert
        Assert.Equal(0.0, dot);
        Assert.Equal(14.0, Vector3D.Dot(new Vector3D(1, 2, 3), new Vector3D(1, 2, 3)));
    }

    /// <summary>
    ///     Validates the right-hand rule: X cross Y is Z.
    /// </summary>
    [Fact]
    public void Vector3D_Cross_UnitXUnitY_ReturnsUnitZ()
    {
        // Arrange / Act
        var cross = Vector3D.Cross(Vector3D.UnitX, Vector3D.UnitY);

        // Assert: right-handed, and anti-commutative
        Assert.Equal(Vector3D.UnitZ, cross);
        Assert.Equal(-Vector3D.UnitZ, Vector3D.Cross(Vector3D.UnitY, Vector3D.UnitX));
    }

    /// <summary>
    ///     Validates the length of (3, 4, 12), which is exactly 13.
    /// </summary>
    [Fact]
    public void Vector3D_Length_ThreeFourTwelve_ReturnsThirteen()
    {
        // Arrange
        var v = new Vector3D(3.0, 4.0, 12.0);

        // Act / Assert
        Assert.Equal(13.0, v.Length, 1e-15);
        Assert.Equal(169.0, v.LengthSquared, 1e-15);
        Assert.Equal(1.0, v.Normalize().Length, 1e-15);
    }

    /// <summary>
    ///     Validates that normalizing the zero vector returns the zero vector instead of NaN.
    /// </summary>
    [Fact]
    public void Vector3D_Normalize_ZeroVector_ReturnsZero()
    {
        // Arrange / Act
        var normalized = Vector3D.Zero.Normalize();

        // Assert
        Assert.Equal(Vector3D.Zero, normalized);
        Assert.True(normalized.IsFinite);
    }

    /// <summary>
    ///     Validates that a finite vector whose squared length would overflow still normalizes to
    ///     a true unit vector instead of collapsing to zero.
    /// </summary>
    [Fact]
    public void Vector3D_Normalize_LargeVector_ReturnsUnitVector()
    {
        // Arrange: the squared length of these components exceeds double.MaxValue
        var axisAligned = new Vector3D(1e200, 0.0, 0.0);
        var diagonal = new Vector3D(3e200, 4e200, 0.0);

        // Act
        var normalizedAxis = axisAligned.Normalize();
        var normalizedDiagonal = diagonal.Normalize();

        // Assert
        Assert.Equal(Vector3D.UnitX, normalizedAxis);
        Assert.Equal(0.6, normalizedDiagonal.X, 1e-12);
        Assert.Equal(0.8, normalizedDiagonal.Y, 1e-12);
        Assert.Equal(0.0, normalizedDiagonal.Z);
    }

    /// <summary>
    ///     Validates that a tiny non-zero vector whose squared length would underflow is not
    ///     mistaken for the zero vector when normalized.
    /// </summary>
    [Fact]
    public void Vector3D_Normalize_TinyVector_ReturnsUnitVector()
    {
        // Arrange: the squared length of these components flushes to zero
        var axisAligned = new Vector3D(0.0, -1e-200, 0.0);
        var diagonal = new Vector3D(3e-200, 4e-200, 0.0);

        // Act
        var normalizedAxis = axisAligned.Normalize();
        var normalizedDiagonal = diagonal.Normalize();

        // Assert
        Assert.Equal(-Vector3D.UnitY, normalizedAxis);
        Assert.Equal(0.6, normalizedDiagonal.X, 1e-12);
        Assert.Equal(0.8, normalizedDiagonal.Y, 1e-12);
    }

    /// <summary>
    ///     Validates that a vector with a NaN component normalizes to zero (it has no direction)
    ///     rather than propagating NaN.
    /// </summary>
    [Fact]
    public void Vector3D_Normalize_NaNComponent_ReturnsZero()
    {
        // Arrange
        var vector = new Vector3D(double.NaN, 1.0, 0.0);

        // Act
        var normalized = vector.Normalize();

        // Assert
        Assert.Equal(Vector3D.Zero, normalized);
    }

    /// <summary>
    ///     Validates that the length of a finite vector with very large components is finite and
    ///     exact rather than overflowing to infinity.
    /// </summary>
    [Fact]
    public void Vector3D_Length_LargeVector_ReturnsFiniteLength()
    {
        // Arrange
        var vector = new Vector3D(3e200, 4e200, 0.0);

        // Act
        var length = vector.Length;

        // Assert
        Assert.True(double.IsFinite(length));
        Assert.Equal(5e200, length, 1e188);
    }

    /// <summary>
    ///     Validates that the length of a tiny non-zero vector is not flushed to zero by underflow.
    /// </summary>
    [Fact]
    public void Vector3D_Length_TinyVector_ReturnsNonZeroLength()
    {
        // Arrange
        var vector = new Vector3D(3e-200, 4e-200, 0.0);

        // Act
        var length = vector.Length;

        // Assert
        Assert.True(length > 0.0);
        Assert.Equal(5e-200, length, 1e-212);
    }

    /// <summary>
    ///     Validates that the zero vector has exactly zero length.
    /// </summary>
    [Fact]
    public void Vector3D_Length_ZeroVector_ReturnsZero()
    {
        // Arrange / Act
        var length = Vector3D.Zero.Length;

        // Assert
        Assert.Equal(0.0, length);
    }

    /// <summary>
    ///     Validates that narrowing to a single-precision vector rounds each component to float.
    /// </summary>
    [Fact]
    public void Vector3D_ToVector3_NarrowsToFloat()
    {
        // Arrange: components that are not exactly representable as float
        var v = new Vector3D(0.1, -2.5, 1e6 + 0.3);

        // Act
        var narrowed = v.ToVector3();

        // Assert
        Assert.Equal(new Vector3((float)0.1, -2.5f, (float)(1e6 + 0.3)), narrowed);
    }

    /// <summary>
    ///     Validates that an infinite component makes the vector non-finite.
    /// </summary>
    [Fact]
    public void Vector3D_IsFinite_Infinity_ReturnsFalse()
    {
        // Arrange / Act / Assert
        Assert.False(new Vector3D(0.0, double.PositiveInfinity, 0.0).IsFinite);
        Assert.False(new Vector3D(double.NaN, 0.0, 0.0).IsFinite);
        Assert.True(new Vector3D(1.0, 2.0, 3.0).IsFinite);
    }
}
