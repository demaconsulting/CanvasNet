using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneModel;

/// <summary>
///     Unit tests for <see cref="Vector2D"/> (DoublePrecisionMath unit).
/// </summary>
public class Vector2DTests
{
    /// <summary>
    ///     Validates dot product, scalar cross product, length and normalization against
    ///     analytic values (3-4-5 triangle).
    /// </summary>
    [Fact]
    public void Vector2D_Dot_Cross_Normalize_MatchAnalytic()
    {
        // Arrange: a 3-4 vector and the unit X vector
        var a = new Vector2D(3.0, 4.0);
        var b = Vector2D.UnitX;

        // Act
        var dot = Vector2D.Dot(a, b);
        var cross = Vector2D.Cross(b, a);
        var length = a.Length;
        var unit = a.Normalize();

        // Assert: dot = 3, cross(UnitX, a) = +4 (a is counter-clockwise from X), length = 5, unit = (0.6, 0.8)
        Assert.Equal(3.0, dot, 1e-15);
        Assert.Equal(4.0, cross, 1e-15);
        Assert.Equal(5.0, length, 1e-15);
        Assert.Equal(25.0, a.LengthSquared, 1e-15);
        Assert.Equal(0.6, unit.X, 1e-15);
        Assert.Equal(0.8, unit.Y, 1e-15);
    }

    /// <summary>
    ///     Validates that the length of a tiny non-zero vector is not flushed to zero by underflow.
    /// </summary>
    [Fact]
    public void Vector2D_Length_TinyVector_ReturnsNonZeroLength()
    {
        // Arrange: the squared components flush to zero
        var vector = new Vector2D(3e-200, 4e-200);

        // Act
        var length = vector.Length;

        // Assert
        Assert.True(length > 0.0);
        Assert.Equal(5e-200, length, 1e-212);
    }

    /// <summary>
    ///     Validates that the length of a finite vector with very large components is finite and
    ///     exact rather than overflowing to infinity.
    /// </summary>
    [Fact]
    public void Vector2D_Length_LargeVector_ReturnsFiniteLength()
    {
        // Arrange
        var vector = new Vector2D(3e200, 4e200);

        // Act
        var length = vector.Length;

        // Assert
        Assert.True(double.IsFinite(length));
        Assert.Equal(5e200, length, 1e188);
    }

    /// <summary>
    ///     Validates that the zero vector has exactly zero length.
    /// </summary>
    [Fact]
    public void Vector2D_Length_ZeroVector_ReturnsZero()
    {
        // Arrange / Act
        var length = Vector2D.Zero.Length;

        // Assert
        Assert.Equal(0.0, length);
    }

    /// <summary>
    ///     Validates that a tiny non-zero vector normalizes to a unit vector instead of zero.
    /// </summary>
    [Fact]
    public void Vector2D_Normalize_TinyVector_ReturnsUnitVector()
    {
        // Arrange
        var axisAligned = new Vector2D(0.0, -1e-200);
        var diagonal = new Vector2D(3e-200, 4e-200);

        // Act
        var normalizedAxis = axisAligned.Normalize();
        var normalizedDiagonal = diagonal.Normalize();

        // Assert
        Assert.Equal(-Vector2D.UnitY, normalizedAxis);
        Assert.Equal(0.6, normalizedDiagonal.X, 1e-12);
        Assert.Equal(0.8, normalizedDiagonal.Y, 1e-12);
    }

    /// <summary>
    ///     Validates that a vector with very large components normalizes to a unit vector instead
    ///     of overflowing.
    /// </summary>
    [Fact]
    public void Vector2D_Normalize_LargeVector_ReturnsUnitVector()
    {
        // Arrange
        var axisAligned = new Vector2D(1e200, 0.0);
        var diagonal = new Vector2D(3e200, 4e200);

        // Act
        var normalizedAxis = axisAligned.Normalize();
        var normalizedDiagonal = diagonal.Normalize();

        // Assert
        Assert.Equal(Vector2D.UnitX, normalizedAxis);
        Assert.Equal(0.6, normalizedDiagonal.X, 1e-12);
        Assert.Equal(0.8, normalizedDiagonal.Y, 1e-12);
    }

    /// <summary>
    ///     Validates that the zero vector and a NaN-component vector normalize to zero.
    /// </summary>
    [Fact]
    public void Vector2D_Normalize_ZeroOrNaNVector_ReturnsZero()
    {
        // Arrange / Act
        var zero = Vector2D.Zero.Normalize();
        var nan = new Vector2D(double.NaN, 1.0).Normalize();

        // Assert
        Assert.Equal(Vector2D.Zero, zero);
        Assert.Equal(Vector2D.Zero, nan);
    }

    /// <summary>
    ///     Validates the arithmetic operators and the zero-vector normalization rule.
    /// </summary>
    [Fact]
    public void Vector2D_Operators_AddSubtractScaleNegate_ComponentWise()
    {
        // Arrange
        var a = new Vector2D(1.0, 2.0);
        var b = new Vector2D(0.5, -1.0);

        // Act / Assert: each operator acts component-wise
        Assert.Equal(new Vector2D(1.5, 1.0), a + b);
        Assert.Equal(new Vector2D(0.5, 3.0), a - b);
        Assert.Equal(new Vector2D(2.0, 4.0), a * 2.0);
        Assert.Equal(new Vector2D(2.0, 4.0), 2.0 * a);
        Assert.Equal(new Vector2D(0.5, 1.0), a / 2.0);
        Assert.Equal(new Vector2D(-1.0, -2.0), -a);
        Assert.Equal(Vector2D.Zero, Vector2D.Zero.Normalize());
        Assert.True(a.IsFinite);
        Assert.False(new Vector2D(double.NaN, 0.0).IsFinite);
    }
}
