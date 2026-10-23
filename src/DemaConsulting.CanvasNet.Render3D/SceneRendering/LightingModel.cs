using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     Computes the flat-shaded color of a face from its view-space normal and a light rig.
/// </summary>
/// <remarks>
///     <para>
///         Model: <c>radiance = sum over ambient lights I*C + sum over directional lights
///         I*C*max(0, N . -L)</c>, where <c>L</c> is the unit direction the light travels in view
///         space (camera-relative lights use their direction as given; world lights are rotated by
///         the view rotation). The result is <c>clamp(base * radiance + specular)</c> per channel
///         with sRGB values used as-is (no gamma), consistent with the 2D engine; alpha is always
///         255 (surfaces are opaque in Phase 1).
///     </para>
///     <para>
///         When <c>specularStrength &gt; 0</c> a Blinn-Phong term
///         <c>I*C*strength*max(0, N . H)^power</c> is added per directional light with half vector
///         <c>H = normalize(-L + V)</c> and a constant viewer direction <c>V = (0, 0, 1)</c>. The
///         constant viewer is a documented flat-shading/CAD approximation that keeps the shade of a
///         face constant across its pixels, so shading runs once per triangle.
///     </para>
///     <para>
///         An empty light list produces black (no light, no color); callers wanting an unshaded
///         look supply a single <see cref="AmbientLight"/> of intensity 1. Stateless and thread-safe.
///     </para>
/// </remarks>
internal static class LightingModel
{
    /// <summary>Divisor converting an 8-bit channel to the [0, 1] range.</summary>
    private const double ByteScale = 255.0;

    /// <summary>
    ///     Shades a face whose visible side has the given normal.
    /// </summary>
    /// <param name="normal">The unit view-space normal of the visible side (pointing toward the viewer).</param>
    /// <param name="baseColor">The surface color before lighting (alpha ignored).</param>
    /// <param name="lights">The light rig (non-empty for any color).</param>
    /// <param name="specularStrength">The specular strength in [0, 1]; 0 disables the specular term.</param>
    /// <param name="specularPower">The specular exponent (greater than zero).</param>
    /// <param name="worldToView">The world-to-view matrix whose rotation orients world-space lights.</param>
    /// <returns>The opaque shaded color.</returns>
    public static Rgba32 Shade(
        Vector3D normal,
        Rgba32 baseColor,
        IReadOnlyList<Light> lights,
        float specularStrength,
        float specularPower,
        Matrix4x4D worldToView)
    {
        // Accumulate diffuse radiance (per channel) and the white-ish specular contribution
        var radR = 0.0;
        var radG = 0.0;
        var radB = 0.0;
        var specR = 0.0;
        var specG = 0.0;
        var specB = 0.0;
        var viewer = new Vector3D(0.0, 0.0, 1.0);

        foreach (var light in lights)
        {
            double scale = light.Intensity;
            var cr = light.Color.R / ByteScale;
            var cg = light.Color.G / ByteScale;
            var cb = light.Color.B / ByteScale;

            if (light is DirectionalLight directional)
            {
                // Direction the light travels, in view space
                var travel = directional.IsCameraRelative
                    ? directional.Direction
                    : worldToView.TransformDirection(directional.Direction).Normalize();
                var toLight = -travel;
                var nDotL = Math.Max(0.0, Vector3D.Dot(normal, toLight));
                scale *= nDotL;

                // Blinn-Phong highlight only where the surface is lit
                if (specularStrength > 0.0f && nDotL > 0.0)
                {
                    var half = (toLight + viewer).Normalize();
                    var nDotH = Math.Max(0.0, Vector3D.Dot(normal, half));
                    var spec = light.Intensity * specularStrength * Math.Pow(nDotH, specularPower);
                    specR += spec * cr;
                    specG += spec * cg;
                    specB += spec * cb;
                }
            }

            radR += scale * cr;
            radG += scale * cg;
            radB += scale * cb;
        }

        return new Rgba32(
            ToByte((baseColor.R / ByteScale * radR) + specR),
            ToByte((baseColor.G / ByteScale * radG) + specG),
            ToByte((baseColor.B / ByteScale * radB) + specB),
            255);
    }

    /// <summary>
    ///     Shades a face that is seen from behind: the geometric (outward) normal is flipped toward
    ///     the viewer and the back-face color replaces the base color.
    /// </summary>
    /// <param name="frontNormal">The unit view-space outward normal of the face (pointing away from the viewer).</param>
    /// <param name="backFaceColor">The color used for back faces (alpha ignored).</param>
    /// <param name="lights">The light rig.</param>
    /// <param name="specularStrength">The specular strength in [0, 1].</param>
    /// <param name="specularPower">The specular exponent (greater than zero).</param>
    /// <param name="worldToView">The world-to-view matrix whose rotation orients world-space lights.</param>
    /// <returns>The opaque shaded color.</returns>
    public static Rgba32 ShadeBackFace(
        Vector3D frontNormal,
        Rgba32 backFaceColor,
        IReadOnlyList<Light> lights,
        float specularStrength,
        float specularPower,
        Matrix4x4D worldToView) =>
        Shade(-frontNormal, backFaceColor, lights, specularStrength, specularPower, worldToView);

    /// <summary>Converts a [0, 1] channel value to a clamped, rounded byte.</summary>
    /// <param name="value">The channel value (may be outside [0, 1]).</param>
    /// <returns>The byte value in [0, 255].</returns>
    private static byte ToByte(double value) =>
        (byte)Math.Clamp(Math.Round(value * ByteScale, MidpointRounding.AwayFromZero), 0.0, ByteScale);
}
