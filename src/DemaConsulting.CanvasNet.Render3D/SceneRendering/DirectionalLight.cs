using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     A light with parallel rays (like the sun) traveling in a fixed direction.
/// </summary>
/// <remarks>
///     A surface is lit in proportion to the cosine between its normal and the direction toward the
///     light. The direction can be expressed in world space (the light stays fixed relative to the
///     model while the camera orbits) or in view space (<see cref="IsCameraRelative"/>, the light
///     stays fixed relative to the camera, like a headlight).
/// </remarks>
public sealed class DirectionalLight : Light
{
    /// <summary>Backing field for <see cref="Direction"/>; always unit length.</summary>
    private readonly Vector3D _direction = new(0.0, 0.0, -1.0);

    /// <summary>
    ///     Gets the direction the light <b>travels</b> (from the light toward the surface); finite and
    ///     non-zero. The stored value is normalized, so the getter returns a unit vector. Defaults to
    ///     <c>(0, 0, -1)</c>.
    /// </summary>
    /// <remarks>
    ///     In view space (see <see cref="IsCameraRelative"/>) +X is right, +Y is up and -Z points away
    ///     from the viewer, so <c>(0, 0, -1)</c> is a headlight.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown (from the <see langword="init"/> accessor) when the vector is zero-length or not finite.</exception>
    public Vector3D Direction
    {
        get => _direction;
        init
        {
            // A zero or non-finite direction cannot be normalized into a ray direction
            if (!value.IsFinite || value.Length <= 0.0)
            {
                throw new ArgumentException("The light direction must be finite and non-zero.", nameof(value));
            }

            _direction = value.Normalize();
        }
    }

    /// <summary>
    ///     Gets a value indicating whether <see cref="Direction"/> is expressed in view space (fixed
    ///     relative to the camera: +X right, +Y up, -Z forward) rather than world space. Defaults to
    ///     <see langword="false"/> (world space).
    /// </summary>
    public bool IsCameraRelative { get; init; }
}
