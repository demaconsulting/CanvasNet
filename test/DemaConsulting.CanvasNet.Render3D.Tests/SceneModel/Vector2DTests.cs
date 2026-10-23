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
