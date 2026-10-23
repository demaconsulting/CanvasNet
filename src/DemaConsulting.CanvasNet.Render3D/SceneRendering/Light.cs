using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     The base class of all light sources used by <see cref="SceneRenderOptions.Lights"/>.
/// </summary>
/// <remarks>
///     <para>
///         The hierarchy is closed (private-protected constructor): <see cref="AmbientLight"/> and
///         <see cref="DirectionalLight"/> are the two supported kinds. Lights are immutable after
///         construction and therefore thread-safe and shareable between renders. The alpha channel of
///         <see cref="Color"/> is ignored.
///     </para>
///     <para>
///         Shading is flat per face using Lambert diffuse (and optional Blinn-Phong specular); see
///         <see cref="DefaultRig"/> for the neutral CAD look used when no lights are specified.
///     </para>
/// </remarks>
public abstract class Light
{
    /// <summary>Backing field for <see cref="Intensity"/>.</summary>
    private readonly float _intensity = 1.0f;

    /// <summary>Initializes a new instance; restricted to this assembly to keep the hierarchy closed.</summary>
    private protected Light()
    {
    }

    /// <summary>Gets the default rig: a neutral CAD-style ambient + headlight + key-light combination.</summary>
    /// <remarks>
    ///     Contents: an <see cref="AmbientLight"/> at intensity 0.35; a camera-relative
    ///     <see cref="DirectionalLight"/> headlight along <c>(0, 0, -1)</c> at 0.55; and a
    ///     camera-relative key light along <c>(0.5, -0.6, -0.62)</c> (from the upper left) at 0.25.
    ///     The list is immutable and shared.
    /// </remarks>
    public static IReadOnlyList<Light> DefaultRig { get; } = Array.AsReadOnly(new Light[]
    {
        new AmbientLight { Intensity = 0.35f },
        new DirectionalLight { Direction = new Vector3D(0.0, 0.0, -1.0), IsCameraRelative = true, Intensity = 0.55f },
        new DirectionalLight { Direction = new Vector3D(0.5, -0.6, -0.62), IsCameraRelative = true, Intensity = 0.25f },
    });

    /// <summary>Gets the light color (alpha ignored). Defaults to white.</summary>
    public Rgba32 Color { get; init; } = new(255, 255, 255, 255);

    /// <summary>
    ///     Gets the light intensity (a non-negative, finite multiplier; values above 1 can
    ///     over-brighten and are clamped after shading). Defaults to 1.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown (from the <see langword="init"/> accessor) when the value is negative or not finite.</exception>
    public float Intensity
    {
        get => _intensity;
        init
        {
            if (!float.IsFinite(value) || value < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The intensity must be finite and not negative.");
            }

            _intensity = value;
        }
    }
}
