using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneRendering;

/// <summary>
///     Unit tests for the internal <c>LightingModel</c> (Lighting unit).
/// </summary>
public class LightingModelTests
{
    /// <summary>The neutral CAD gray used as the base colour in these tests.</summary>
    private static readonly Rgba32 Gray = new(180, 180, 180, 255);

    /// <summary>A camera-relative headlight shining straight into the screen at the given intensity.</summary>
    /// <param name="intensity">The light intensity.</param>
    /// <returns>The light.</returns>
    private static DirectionalLight Headlight(float intensity) =>
        new() { Direction = new Vector3D(0, 0, -1), IsCameraRelative = true, Intensity = intensity };

    /// <summary>
    ///     Shades with no specular term and an identity view.
    /// </summary>
    /// <param name="normal">The view-space normal of the visible side.</param>
    /// <param name="lights">The lights.</param>
    /// <returns>The shaded colour.</returns>
    private static Rgba32 Shade(Vector3D normal, params Light[] lights) =>
        LightingModel.Shade(normal, Gray, lights, 0.0f, 32.0f, Matrix4x4D.Identity);

    /// <summary>
    ///     Validates that ambient light alone scales the base colour (0.5 x 180 = 90).
    /// </summary>
    [Fact]
    public void LightingModel_Shade_AmbientOnly_ScalesBaseColor()
    {
        // Arrange / Act
        var color = Shade(new Vector3D(0, 0, 1), new AmbientLight { Intensity = 0.5f });

        // Assert
        Assert.Equal(new Rgba32(90, 90, 90, 255), color);
    }

    /// <summary>
    ///     Validates that a headlight on a face turned toward the viewer applies full intensity.
    /// </summary>
    [Fact]
    public void LightingModel_Shade_HeadlightFacingViewer_AppliesFullIntensity()
    {
        // Arrange / Act
        var color = Shade(new Vector3D(0, 0, 1), Headlight(0.5f));

        // Assert: 180 * 0.5 * cos(0) = 90
        Assert.Equal(new Rgba32(90, 90, 90, 255), color);
    }

    /// <summary>
    ///     Validates the Lambert cosine law at a 60 degree tilt (cos 60 = 0.5).
    /// </summary>
    [Fact]
    public void LightingModel_Shade_SixtyDegreeTilt_AppliesHalfIntensity()
    {
        // Arrange: normal tilted 60 degrees away from the view axis
        var angle = 60.0 * Math.PI / 180.0;
        var normal = new Vector3D(Math.Sin(angle), 0, Math.Cos(angle));

        // Act
        var color = Shade(normal, Headlight(1.0f));

        // Assert: 180 * 1 * 0.5 = 90
        Assert.Equal(new Rgba32(90, 90, 90, 255), color);
    }

    /// <summary>
    ///     Validates that a face turned away from the light receives only ambient light.
    /// </summary>
    [Fact]
    public void LightingModel_Shade_FaceTurnedAwayFromLight_ReceivesOnlyAmbient()
    {
        // Arrange / Act: normal points away from the viewer, so N . toLight < 0
        var color = Shade(new Vector3D(0, 0, -1), new AmbientLight { Intensity = 0.25f }, Headlight(0.5f));

        // Assert: 180 * 0.25 = 45
        Assert.Equal(new Rgba32(45, 45, 45, 255), color);
    }

    /// <summary>
    ///     Validates that a combined ambient and headlight rig adds contributions (0.25 + 0.5 = 0.75 x 180 = 135).
    /// </summary>
    [Fact]
    public void LightingModel_Shade_AmbientPlusHeadlight_AddsContributions()
    {
        // Arrange / Act
        var color = Shade(new Vector3D(0, 0, 1), new AmbientLight { Intensity = 0.25f }, Headlight(0.5f));

        // Assert
        Assert.Equal(new Rgba32(135, 135, 135, 255), color);
    }

    /// <summary>
    ///     Validates that the light colour tints the result per channel.
    /// </summary>
    [Fact]
    public void LightingModel_Shade_ColoredLight_TintsChannels()
    {
        // Arrange: a pure red ambient light on gray
        var red = new AmbientLight { Color = new Rgba32(255, 0, 0, 255), Intensity = 1.0f };

        // Act
        var color = Shade(new Vector3D(0, 0, 1), red);

        // Assert
        Assert.Equal(new Rgba32(180, 0, 0, 255), color);
    }

    /// <summary>
    ///     Validates that enabling specular brightens a face aligned with the half vector.
    /// </summary>
    [Fact]
    public void LightingModel_Shade_SpecularEnabled_BrightensFaceAlignedWithHalfVector()
    {
        // Arrange: headlight and viewer coincide, so the half vector equals the normal
        var lights = new Light[] { Headlight(0.5f) };

        // Act
        var diffuseOnly = LightingModel.Shade(new Vector3D(0, 0, 1), Gray, lights, 0.0f, 32.0f, Matrix4x4D.Identity);
        var withSpecular = LightingModel.Shade(new Vector3D(0, 0, 1), Gray, lights, 0.5f, 32.0f, Matrix4x4D.Identity);

        // Assert: diffuse 90; specular adds 0.5 * 0.5 * 255 = 63.75 -> 153.75 -> 154
        Assert.Equal(90, diffuseOnly.R);
        Assert.Equal(154, withSpecular.R);
    }

    /// <summary>
    ///     Validates that zero specular strength equals the diffuse-only result at any exponent.
    /// </summary>
    [Fact]
    public void LightingModel_Shade_SpecularZero_MatchesDiffuseOnly()
    {
        // Arrange
        var lights = new Light[] { new AmbientLight { Intensity = 0.3f }, Headlight(0.6f) };
        var normal = new Vector3D(0.3, 0.2, 0.9).Normalize();

        // Act
        var a = LightingModel.Shade(normal, Gray, lights, 0.0f, 8.0f, Matrix4x4D.Identity);
        var b = LightingModel.Shade(normal, Gray, lights, 0.0f, 128.0f, Matrix4x4D.Identity);

        // Assert
        Assert.Equal(a, b);
    }

    /// <summary>
    ///     Validates that a world-space light rotates with the view transform.
    /// </summary>
    [Fact]
    public void LightingModel_Shade_WorldLight_RotatesWithViewTransform()
    {
        // Arrange: a world light travelling along -Z; a 90 degree X rotation maps that direction to +Y in view space
        var world = new DirectionalLight { Direction = new Vector3D(0, 0, -1) };
        var lights = new Light[] { world };
        var rotation = Matrix4x4D.CreateRotationX(90.0);

        // Act
        var facingScreen = LightingModel.Shade(new Vector3D(0, 0, 1), Gray, lights, 0.0f, 32.0f, rotation);
        var facingDown = LightingModel.Shade(new Vector3D(0, -1, 0), Gray, lights, 0.0f, 32.0f, rotation);
        var identityFacingScreen = LightingModel.Shade(new Vector3D(0, 0, 1), Gray, lights, 0.0f, 32.0f, Matrix4x4D.Identity);

        // Assert: with the rotation, the face looking along -Y (toward the light) is lit and the screen-facing one is dark
        Assert.Equal(0, facingScreen.R);
        Assert.Equal(180, facingDown.R);
        Assert.Equal(180, identityFacingScreen.R);
    }

    /// <summary>
    ///     Validates that a camera-relative light ignores the view rotation.
    /// </summary>
    [Fact]
    public void LightingModel_Shade_CameraRelativeLight_IgnoresViewRotation()
    {
        // Arrange
        var lights = new Light[] { Headlight(1.0f) };
        var rotation = Matrix4x4D.CreateRotationX(90.0);

        // Act
        var color = LightingModel.Shade(new Vector3D(0, 0, 1), Gray, lights, 0.0f, 32.0f, rotation);

        // Assert
        Assert.Equal(180, color.R);
    }

    /// <summary>
    ///     Validates that back faces use the flipped normal and the back-face colour.
    /// </summary>
    [Fact]
    public void LightingModel_ShadeBackFace_UsesFlippedNormalAndBackColor()
    {
        // Arrange: the outward normal points away from the viewer, so this is a back face
        var backColor = new Rgba32(200, 70, 70, 255);
        var lights = new Light[] { Headlight(0.5f), new AmbientLight { Intensity = 0.25f } };

        // Act
        var color = LightingModel.ShadeBackFace(new Vector3D(0, 0, -1), backColor, lights, 0.0f, 32.0f, Matrix4x4D.Identity);

        // Assert: (0.5 + 0.25) of the back colour (70 * 0.75 = 52.5 may round either way)
        Assert.Equal(150, color.R);
        Assert.InRange(color.G, 52, 53);
        Assert.InRange(color.B, 52, 53);
        Assert.Equal(255, color.A);
    }

    /// <summary>
    ///     Validates that an empty light list produces black.
    /// </summary>
    [Fact]
    public void LightingModel_Shade_EmptyLightList_ReturnsBlack()
    {
        // Arrange / Act
        var color = Shade(new Vector3D(0, 0, 1));

        // Assert
        Assert.Equal(new Rgba32(0, 0, 0, 255), color);
    }

    /// <summary>
    ///     Validates that over-bright light clamps to 255 per channel.
    /// </summary>
    [Fact]
    public void LightingModel_Shade_OverbrightLights_ClampsTo255()
    {
        // Arrange / Act
        var color = Shade(new Vector3D(0, 0, 1), new AmbientLight { Intensity = 5.0f });

        // Assert: 180 * 5 = 900 -> 255
        Assert.Equal(new Rgba32(255, 255, 255, 255), color);
    }
}
