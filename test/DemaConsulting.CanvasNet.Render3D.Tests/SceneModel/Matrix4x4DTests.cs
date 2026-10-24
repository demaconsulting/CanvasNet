using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneModel;

/// <summary>
///     Unit tests for <see cref="Matrix4x4D"/> (DoublePrecisionMath unit).
/// </summary>
public class Matrix4x4DTests
{
    /// <summary>
    ///     Validates that the identity leaves points unchanged.
    /// </summary>
    [Fact]
    public void Matrix4x4D_Identity_TransformPoint_ReturnsSamePoint()
    {
        // Arrange
        var p = new Vector3D(1.5, -2.5, 3.5);

        // Act
        var result = Matrix4x4D.Identity.TransformPoint(p);

        // Assert
        Assert.Equal(p, result);
        Assert.Equal(1.0, Matrix4x4D.Identity.Determinant);
        Assert.NotEqual(Matrix4x4D.Identity, default(Matrix4x4D));
    }

    /// <summary>
    ///     Validates that a translation moves a point by exactly the offset.
    /// </summary>
    [Fact]
    public void Matrix4x4D_CreateTranslation_TransformPoint_Translates()
    {
        // Arrange
        var m = Matrix4x4D.CreateTranslation(new Vector3D(1.0, 2.0, 3.0));

        // Act
        var result = m.TransformPoint(new Vector3D(10.0, 20.0, 30.0));

        // Assert
        Assert.Equal(new Vector3D(11.0, 22.0, 33.0), result);
        Assert.Equal(1.0, m.M11);
        Assert.Equal(3.0, m.M43);
    }

    /// <summary>
    ///     Validates the row-vector composition order: the left operand applies first.
    /// </summary>
    [Fact]
    public void Matrix4x4D_Multiply_TranslateThenScale_AppliesLeftOperandFirst()
    {
        // Arrange: translate by (1,2,3) first, scale by 2 second
        var translate = Matrix4x4D.CreateTranslation(new Vector3D(1.0, 2.0, 3.0));
        var scale = Matrix4x4D.CreateScale(2.0);

        // Act
        var combined = translate * scale;
        var reversed = scale * translate;

        // Assert: origin -> (1,2,3) -> (2,4,6); the reverse order gives (1,2,3)
        Assert.Equal(new Vector3D(2.0, 4.0, 6.0), combined.TransformPoint(Vector3D.Zero));
        Assert.Equal(new Vector3D(1.0, 2.0, 3.0), reversed.TransformPoint(Vector3D.Zero));
    }

    /// <summary>
    ///     Validates that a 90-degree Z rotation maps +X exactly onto +Y (no residual error).
    /// </summary>
    [Fact]
    public void Matrix4x4D_CreateRotationZ_Ninety_MapsUnitXToUnitYExactly()
    {
        // Arrange / Act
        var result = Matrix4x4D.CreateRotationZ(90.0).TransformPoint(Vector3D.UnitX);

        // Assert: exact, not merely approximate
        Assert.Equal(0.0, result.X);
        Assert.Equal(1.0, result.Y);
        Assert.Equal(0.0, result.Z);
    }

    /// <summary>
    ///     Validates that rotations by multiples of 90 degrees (including negative and over-360
    ///     angles) are exact for all three principal axes.
    /// </summary>
    /// <param name="degrees">The rotation angle.</param>
    /// <param name="x">The expected X of the rotated +X vector.</param>
    /// <param name="y">The expected Y of the rotated +X vector.</param>
    [Theory]
    [InlineData(0.0, 1.0, 0.0)]
    [InlineData(90.0, 0.0, 1.0)]
    [InlineData(180.0, -1.0, 0.0)]
    [InlineData(270.0, 0.0, -1.0)]
    [InlineData(-90.0, 0.0, -1.0)]
    [InlineData(360.0, 1.0, 0.0)]
    [InlineData(450.0, 0.0, 1.0)]
    public void Matrix4x4D_CreateRotation_MultiplesOfNinety_AreExact(double degrees, double x, double y)
    {
        // Arrange / Act
        var z = Matrix4x4D.CreateRotationZ(degrees).TransformPoint(Vector3D.UnitX);
        var yAxis = Matrix4x4D.CreateRotationY(degrees).TransformPoint(Vector3D.UnitZ);
        var xAxis = Matrix4x4D.CreateRotationX(degrees).TransformPoint(Vector3D.UnitY);

        // Assert: Z rotation of +X is exact; the Y rotation of +Z and X rotation of +Y are the cyclic equivalents
        Assert.Equal(x, z.X);
        Assert.Equal(y, z.Y);
        Assert.Equal(x, yAxis.Z);
        Assert.Equal(y, yAxis.X);
        Assert.Equal(x, xAxis.Y);
        Assert.Equal(y, xAxis.Z);
    }

    /// <summary>
    ///     Validates that the axis-angle factory about +Z equals the dedicated Z rotation, at a
    ///     non-trivial angle.
    /// </summary>
    [Fact]
    public void Matrix4x4D_CreateFromAxisAngle_ZAxis_MatchesRotationZ()
    {
        // Arrange / Act
        var axisAngle = Matrix4x4D.CreateFromAxisAngle(new Vector3D(0.0, 0.0, 5.0), 37.0);
        var rotation = Matrix4x4D.CreateRotationZ(37.0);

        // Assert
        Render3DTestHelpers.AssertMatrix(rotation, axisAngle);
    }

    /// <summary>
    ///     Validates that an axis whose squared length overflows is still normalized, so the
    ///     rotation matches the equivalent unit-axis rotation.
    /// </summary>
    [Fact]
    public void Matrix4x4D_CreateFromAxisAngle_LargeAxis_MatchesRotationZ()
    {
        // Arrange / Act
        var axisAngle = Matrix4x4D.CreateFromAxisAngle(new Vector3D(0.0, 0.0, 1e200), 37.0);
        var rotation = Matrix4x4D.CreateRotationZ(37.0);

        // Assert
        Render3DTestHelpers.AssertMatrix(rotation, axisAngle);
    }

    /// <summary>
    ///     Validates that rotating about the (1,1,1) diagonal by 120 degrees cycles the axes.
    /// </summary>
    [Fact]
    public void Matrix4x4D_CreateFromAxisAngle_DiagonalAxis_CyclesAxes()
    {
        // Arrange / Act
        var m = Matrix4x4D.CreateFromAxisAngle(Vector3D.One, 120.0);

        // Assert: X -> Y -> Z -> X (right-handed)
        Render3DTestHelpers.AssertVector(Vector3D.UnitY, m.TransformPoint(Vector3D.UnitX));
        Render3DTestHelpers.AssertVector(Vector3D.UnitZ, m.TransformPoint(Vector3D.UnitY));
        Render3DTestHelpers.AssertVector(Vector3D.UnitX, m.TransformPoint(Vector3D.UnitZ));
    }

    /// <summary>
    ///     Validates the per-axis scale factory.
    /// </summary>
    [Fact]
    public void Matrix4x4D_CreateScale_Vector_ScalesComponents()
    {
        // Arrange / Act
        var result = Matrix4x4D.CreateScale(new Vector3D(2.0, 3.0, -4.0)).TransformPoint(new Vector3D(1.0, 1.0, 1.0));

        // Assert
        Assert.Equal(new Vector3D(2.0, 3.0, -4.0), result);
    }

    /// <summary>
    ///     Validates that the determinant of a scale matrix is the product of its factors.
    /// </summary>
    [Fact]
    public void Matrix4x4D_Determinant_Scale_ReturnsProduct()
    {
        // Arrange / Act
        var det = Matrix4x4D.CreateScale(new Vector3D(2.0, 3.0, 4.0)).Determinant;

        // Assert
        Assert.Equal(24.0, det, 1e-12);
    }

    /// <summary>
    ///     Validates that an invertible matrix times its inverse is the identity.
    /// </summary>
    [Fact]
    public void Matrix4x4D_TryInvert_Invertible_MultipliesToIdentity()
    {
        // Arrange: a composite rotation + scale + translation
        var m = Matrix4x4D.CreateScale(new Vector3D(2.0, 3.0, 0.5))
            * Matrix4x4D.CreateFromAxisAngle(new Vector3D(1.0, 2.0, 3.0), 33.0)
            * Matrix4x4D.CreateTranslation(new Vector3D(10.0, -20.0, 5.0));

        // Act
        var ok = m.TryInvert(out var inverse);

        // Assert
        Assert.True(ok);
        Render3DTestHelpers.AssertMatrix(Matrix4x4D.Identity, m * inverse, 1e-12);
        Render3DTestHelpers.AssertMatrix(Matrix4x4D.Identity, inverse * m, 1e-12);
    }

    /// <summary>
    ///     Validates that a singular matrix reports failure and yields the default matrix.
    /// </summary>
    [Fact]
    public void Matrix4x4D_TryInvert_Singular_ReturnsFalse()
    {
        // Arrange: flattening scale (determinant 0)
        var singular = Matrix4x4D.CreateScale(new Vector3D(1.0, 1.0, 0.0));

        // Act
        var ok = singular.TryInvert(out var inverse);

        // Assert
        Assert.False(ok);
        Assert.Equal(default(Matrix4x4D), inverse);
    }

    /// <summary>
    ///     Validates that mirroring through the YZ plane negates X and has determinant -1.
    /// </summary>
    [Fact]
    public void Matrix4x4D_CreateMirror_PlaneNormalX_NegatesXAndHasNegativeDeterminant()
    {
        // Arrange / Act
        var mirror = Matrix4x4D.CreateMirror(new Vector3D(3.0, 0.0, 0.0));

        // Assert
        Assert.Equal(new Vector3D(-1.0, 2.0, 3.0), mirror.TransformPoint(new Vector3D(1.0, 2.0, 3.0)));
        Assert.Equal(-1.0, mirror.Determinant, 1e-12);
    }

    /// <summary>
    ///     Validates that a tiny non-zero axis whose squared length flushes to zero is accepted and
    ///     rotates like the equivalent unit axis.
    /// </summary>
    [Fact]
    public void Matrix4x4D_CreateFromAxisAngle_TinyAxis_MatchesRotationZ()
    {
        // Arrange / Act
        var axisAngle = Matrix4x4D.CreateFromAxisAngle(new Vector3D(0.0, 0.0, 1e-200), 37.0);
        var rotation = Matrix4x4D.CreateRotationZ(37.0);

        // Assert
        Render3DTestHelpers.AssertMatrix(rotation, axisAngle);
    }

    /// <summary>
    ///     Validates that a tiny non-zero mirror normal is accepted and mirrors like a unit normal.
    /// </summary>
    [Fact]
    public void Matrix4x4D_CreateMirror_TinyNormal_MatchesUnitNormalMirror()
    {
        // Arrange / Act
        var mirror = Matrix4x4D.CreateMirror(new Vector3D(3e-200, 0.0, 0.0));

        // Assert
        Assert.Equal(new Vector3D(-1.0, 2.0, 3.0), mirror.TransformPoint(new Vector3D(1.0, 2.0, 3.0)));
        Assert.Equal(-1.0, mirror.Determinant, 1e-12);
    }

    /// <summary>
    ///     Validates that a mirror normal whose squared length overflows is still accepted.
    /// </summary>
    [Fact]
    public void Matrix4x4D_CreateMirror_LargeNormal_MatchesUnitNormalMirror()
    {
        // Arrange / Act
        var mirror = Matrix4x4D.CreateMirror(new Vector3D(3e200, 0.0, 0.0));

        // Assert
        Assert.Equal(new Vector3D(-1.0, 2.0, 3.0), mirror.TransformPoint(new Vector3D(1.0, 2.0, 3.0)));
        Assert.Equal(-1.0, mirror.Determinant, 1e-12);
    }

    /// <summary>
    ///     Validates that a zero mirror normal is rejected.
    /// </summary>
    [Fact]
    public void Matrix4x4D_CreateMirror_ZeroNormal_ThrowsArgumentException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => Matrix4x4D.CreateMirror(Vector3D.Zero));
    }

    /// <summary>
    ///     Validates that directions ignore the translation part of the matrix.
    /// </summary>
    [Fact]
    public void Matrix4x4D_TransformDirection_Translation_IgnoresTranslation()
    {
        // Arrange
        var m = Matrix4x4D.CreateRotationZ(90.0) * Matrix4x4D.CreateTranslation(new Vector3D(100.0, 200.0, 300.0));

        // Act
        var direction = m.TransformDirection(Vector3D.UnitX);
        var point = m.TransformPoint(Vector3D.UnitX);

        // Assert
        Assert.Equal(new Vector3D(0.0, 1.0, 0.0), direction);
        Assert.Equal(new Vector3D(100.0, 201.0, 300.0), point);
    }

    /// <summary>
    ///     Validates that a 1e9 offset retains a 0.001 feature, which single precision cannot.
    /// </summary>
    [Fact]
    public void Matrix4x4D_TransformPoint_LargeOffset_RetainsDoublePrecision()
    {
        // Arrange
        var m = Matrix4x4D.CreateTranslation(new Vector3D(1e9, 0.0, 0.0));

        // Act
        var result = m.TransformPoint(new Vector3D(0.001, 0.0, 0.0));

        // Assert: the 0.001 offset survives (a float would round to a multiple of 64)
        Assert.Equal(0.001, result.X - 1e9, 1e-6);
    }

    /// <summary>
    ///     Validates that a row-major span of the wrong length is rejected.
    /// </summary>
    [Fact]
    public void Matrix4x4D_FromRowMajor_WrongLength_ThrowsArgumentException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => Matrix4x4D.FromRowMajor(new double[15]));
        Assert.Throws<ArgumentException>(() => Matrix4x4D.FromRowMajor(new double[17]));
    }

    /// <summary>
    ///     Validates that a row-major span of 16 values round-trips through the element properties.
    /// </summary>
    [Fact]
    public void Matrix4x4D_FromRowMajor_SixteenValues_MapsToElements()
    {
        // Arrange
        var values = new double[16];
        for (var i = 0; i < 16; i++)
        {
            values[i] = i + 1;
        }

        // Act
        var m = Matrix4x4D.FromRowMajor(values);

        // Assert: row-major order => M11..M14 are 1..4, M41..M44 are 13..16
        Assert.Equal(1.0, m.M11);
        Assert.Equal(4.0, m.M14);
        Assert.Equal(6.0, m.M22);
        Assert.Equal(13.0, m.M41);
        Assert.Equal(16.0, m.M44);
    }

    /// <summary>
    ///     Validates that a zero rotation axis is rejected.
    /// </summary>
    [Fact]
    public void Matrix4x4D_CreateFromAxisAngle_ZeroAxis_ThrowsArgumentException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => Matrix4x4D.CreateFromAxisAngle(Vector3D.Zero, 45.0));
        Assert.Throws<ArgumentException>(() => Matrix4x4D.CreateFromAxisAngle(new Vector3D(double.NaN, 0, 1), 45.0));
    }

    /// <summary>
    ///     Validates that a NaN element makes the matrix non-finite.
    /// </summary>
    [Fact]
    public void Matrix4x4D_IsFinite_NaNElement_ReturnsFalse()
    {
        // Arrange
        var elements = new double[16];
        elements[0] = 1.0;
        elements[5] = 1.0;
        elements[10] = 1.0;
        elements[15] = 1.0;
        elements[7] = double.NaN;

        // Act / Assert
        Assert.False(Matrix4x4D.FromRowMajor(elements).IsFinite);
        Assert.True(Matrix4x4D.Identity.IsFinite);
    }

    /// <summary>
    ///     Validates value equality, hash codes and the equality operators.
    /// </summary>
    [Fact]
    public void Matrix4x4D_Equality_SameElements_EqualWithSameHashCode()
    {
        // Arrange
        var a = Matrix4x4D.CreateRotationZ(30.0);
        var b = Matrix4x4D.CreateRotationZ(30.0);
        var c = Matrix4x4D.CreateRotationZ(31.0);

        // Act / Assert
        Assert.True(a == b);
        Assert.True(a != c);
        Assert.True(a.Equals((object)b));
        Assert.False(a.Equals("not a matrix"));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.False(string.IsNullOrEmpty(a.ToString()));
    }
}
