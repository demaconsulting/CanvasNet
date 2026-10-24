using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneRendering;

/// <summary>
///     Unit tests for the Light types: <see cref="Light"/>, <see cref="AmbientLight"/> and <see cref="DirectionalLight"/>.
/// </summary>
public class LightTests
{
    /// <summary>
    ///     Validates that negative or non-finite intensities are rejected.
    /// </summary>
    [Fact]
    public void Light_Constructors_NegativeIntensity_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new AmbientLight { Intensity = -0.1f });
        Assert.Throws<ArgumentOutOfRangeException>(() => new DirectionalLight { Intensity = float.NaN });
        Assert.Throws<ArgumentOutOfRangeException>(() => new DirectionalLight { Intensity = float.PositiveInfinity });
        Assert.Equal(1.0f, new AmbientLight().Intensity);
        Assert.Equal(new Rgba32(255, 255, 255, 255), new AmbientLight().Color);
    }

    /// <summary>
    ///     Validates that a zero or non-finite light direction is rejected.
    /// </summary>
    [Fact]
    public void DirectionalLight_ZeroDirection_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => new DirectionalLight { Direction = Vector3D.Zero });
        Assert.Throws<ArgumentException>(() => new DirectionalLight { Direction = new Vector3D(double.NaN, 0, 1) });
    }

    /// <summary>
    ///     Validates that a tiny direction whose squared length flushes to zero is accepted and normalized.
    /// </summary>
    [Fact]
    public void DirectionalLight_TinyDirection_NormalizesToUnitVector()
    {
        // Arrange / Act: the squared length of these components flushes to zero
        var axisAligned = new DirectionalLight { Direction = new Vector3D(0.0, 0.0, -1e-200) };
        var diagonal = new DirectionalLight { Direction = new Vector3D(3e-200, 4e-200, 0.0) };

        // Assert
        Assert.Equal(new Vector3D(0, 0, -1), axisAligned.Direction);
        Assert.Equal(0.6, diagonal.Direction.X, 1e-12);
        Assert.Equal(0.8, diagonal.Direction.Y, 1e-12);
    }

    /// <summary>
    ///     Validates that a directional light accepts a very large direction (squared length overflows).
    /// </summary>
    [Fact]
    public void DirectionalLight_LargeDirection_NormalizesToUnitVector()
    {
        // Arrange / Act: the squared length of this direction overflows double
        var light = new DirectionalLight { Direction = new Vector3D(0.0, 0.0, -1e200) };

        // Assert
        Assert.Equal(new Vector3D(0, 0, -1), light.Direction);
    }

    /// <summary>
    ///     Validates that the stored direction is normalized.
    /// </summary>
    [Fact]
    public void DirectionalLight_NormalizesDirection()
    {
        // Arrange / Act
        var light = new DirectionalLight { Direction = new Vector3D(0, 0, -5) };

        // Assert
        Assert.Equal(new Vector3D(0, 0, -1), light.Direction);
        Assert.False(light.IsCameraRelative);
        Assert.Equal(1.0, new DirectionalLight { Direction = new Vector3D(1, 2, 3) }.Direction.Length, 1e-12);
    }

    /// <summary>
    ///     Validates the documented content of the default rig.
    /// </summary>
    [Fact]
    public void Light_DefaultRig_HasAmbientHeadlightAndKey_WithDocumentedIntensities()
    {
        // Arrange / Act
        var rig = Light.DefaultRig;

        // Assert: ambient 0.35, headlight 0.55, key 0.25 (camera-relative)
        Assert.Equal(3, rig.Count);
        var ambient = Assert.IsType<AmbientLight>(rig[0]);
        var headlight = Assert.IsType<DirectionalLight>(rig[1]);
        var key = Assert.IsType<DirectionalLight>(rig[2]);
        Assert.Equal(0.35f, ambient.Intensity);
        Assert.Equal(0.55f, headlight.Intensity);
        Assert.Equal(0.25f, key.Intensity);
        Assert.True(headlight.IsCameraRelative);
        Assert.True(key.IsCameraRelative);
        Assert.Equal(new Vector3D(0, 0, -1), headlight.Direction);
        Assert.True(key.Direction.X > 0.0 && key.Direction.Y < 0.0 && key.Direction.Z < 0.0);
        Assert.Same(Light.DefaultRig, Light.DefaultRig);
    }
}
