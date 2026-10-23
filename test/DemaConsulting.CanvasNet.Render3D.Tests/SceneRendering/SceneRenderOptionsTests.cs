using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneRendering;

/// <summary>
///     Unit tests for <see cref="SceneRenderOptions"/> (SceneRenderer unit).
/// </summary>
public class SceneRenderOptionsTests
{
    /// <summary>
    ///     Validates the documented defaults: white background, neutral gray, red back faces, 3x3 supersampling, scheduler parallelism.
    /// </summary>
    [Fact]
    public void SceneRenderOptions_Defaults_AreNeutralGrayWhiteBackgroundThreeSamples()
    {
        // Arrange / Act
        var options = new SceneRenderOptions();

        // Assert
        Assert.Equal(new Rgba32(255, 255, 255, 255), options.BackgroundColor);
        Assert.Equal(new Rgba32(180, 180, 180, 255), options.DefaultColor);
        Assert.Equal(new Rgba32(200, 70, 70, 255), options.BackFaceColor);
        Assert.Equal(3, options.Supersampling);
        Assert.Equal(-1, options.MaxDegreeOfParallelism);
        Assert.Equal(0.10f, options.SpecularStrength);
        Assert.Equal(32.0f, options.SpecularPower);
        Assert.Null(options.Lights);
        Assert.Equal(options.DefaultColor, SceneRenderOptions.Default.DefaultColor);
        Assert.Equal(3, SceneRenderOptions.Default.Supersampling);
    }

    /// <summary>
    ///     Validates that null lights select the default rig, while an explicit empty list stays empty.
    /// </summary>
    [Fact]
    public void SceneRenderOptions_Lights_Null_UsesDefaultRig()
    {
        // Arrange / Act
        var defaults = new SceneRenderOptions();
        var empty = new SceneRenderOptions { Lights = [] };

        // Assert
        Assert.Same(Light.DefaultRig, defaults.EffectiveLights);
        Assert.Empty(empty.EffectiveLights);
    }

    /// <summary>
    ///     Validates that the light list is copied (later changes to the caller's list have no effect) and null lights are rejected.
    /// </summary>
    [Fact]
    public void SceneRenderOptions_Lights_CopiedDefensively()
    {
        // Arrange
        var list = new List<Light> { new AmbientLight() };

        // Act
        var options = new SceneRenderOptions { Lights = list };
        list.Add(new AmbientLight());
        list.Clear();

        // Assert
        Assert.Single(options.Lights);
        Assert.Throws<ArgumentException>(() => new SceneRenderOptions { Lights = [new AmbientLight(), null!] });
    }

    /// <summary>
    ///     Validates that zero and values below -1 are rejected for the parallelism limit while -1 and positive values are accepted.
    /// </summary>
    [Fact]
    public void SceneRenderOptions_MaxDegreeOfParallelism_ZeroOrBelowMinusOne_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneRenderOptions { MaxDegreeOfParallelism = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneRenderOptions { MaxDegreeOfParallelism = -2 });
        Assert.Equal(-1, new SceneRenderOptions { MaxDegreeOfParallelism = -1 }.MaxDegreeOfParallelism);
        Assert.Equal(1, new SceneRenderOptions { MaxDegreeOfParallelism = 1 }.MaxDegreeOfParallelism);
        Assert.Equal(8, new SceneRenderOptions { MaxDegreeOfParallelism = 8 }.MaxDegreeOfParallelism);
    }

    /// <summary>
    ///     Validates the supersampling and specular ranges.
    /// </summary>
    [Fact]
    public void SceneRenderOptions_NumericRanges_AreValidated()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneRenderOptions { Supersampling = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneRenderOptions { Supersampling = 5 });
        Assert.Equal(4, new SceneRenderOptions { Supersampling = 4 }.Supersampling);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneRenderOptions { SpecularStrength = -0.1f });
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneRenderOptions { SpecularStrength = 1.5f });
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneRenderOptions { SpecularStrength = float.NaN });
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneRenderOptions { SpecularPower = 0.0f });
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneRenderOptions { SpecularPower = float.PositiveInfinity });
        Assert.Equal(1.0f, new SceneRenderOptions { SpecularStrength = 1.0f }.SpecularStrength);
    }
}
