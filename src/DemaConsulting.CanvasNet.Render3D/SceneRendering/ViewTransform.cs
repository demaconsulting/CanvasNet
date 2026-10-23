using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     The resolved, camera-relative view transform and projection scalars used while rendering.
/// </summary>
/// <remarks>
///     <para>
///         The precision strategy of the renderer lives here: a model-to-view transform is composed
///         as <c>world * translate(-eye) * rotation</c>, so the (possibly huge) world translation is
///         cancelled against the eye position <b>in double precision before</b> any rotation, and
///         only the small camera-relative result is later narrowed to single precision. Float error
///         is thus relative to the view distance, never to the world position of the model.
///     </para>
///     <para>
///         View space is right-handed, the camera looks down -Z, +Y is up. A point at view depth
///         <c>d = -z</c> projects to normalized device coordinates
///         <c>x / (d * tan(fov/2) * aspect)</c> and <c>y / (d * tan(fov/2))</c> (perspective) or
///         <c>x / (h/2 * aspect)</c> and <c>y / (h/2)</c> (orthographic, <c>h</c> = visible height).
///     </para>
/// </remarks>
internal readonly struct ViewTransform
{
    /// <summary>
    ///     Creates the view transform for a validated camera and an output aspect ratio.
    /// </summary>
    /// <param name="camera">The camera; validated by this call.</param>
    /// <param name="aspect">The output width divided by height; finite and greater than zero.</param>
    /// <exception cref="ArgumentException">Thrown when the camera is invalid (see <c>Camera.Validate</c>).</exception>
    public ViewTransform(Camera camera, double aspect)
    {
        camera.Validate();

        Eye = camera.Eye;
        Translation = Matrix4x4D.CreateTranslation(-camera.Eye);
        Rotation = camera.CreateViewRotation();
        IsOrthographic = camera.Projection == CameraProjection.Orthographic;
        Aspect = aspect;
        TanHalfFovY = Math.Tan(camera.FieldOfViewDegrees * Math.PI / 360.0);
        OrthographicHalfHeight = (camera.OrthographicHeight ?? 1.0) * 0.5;
    }

    /// <summary>Gets the eye position in world space.</summary>
    public Vector3D Eye { get; }

    /// <summary>Gets the translation by the negated eye position (the camera-relative step).</summary>
    public Matrix4x4D Translation { get; }

    /// <summary>Gets the pure view rotation (world axes to view axes; also orients world-space light directions).</summary>
    public Matrix4x4D Rotation { get; }

    /// <summary>Gets a value indicating whether the projection is orthographic.</summary>
    public bool IsOrthographic { get; }

    /// <summary>Gets the output aspect ratio (width divided by height).</summary>
    public double Aspect { get; }

    /// <summary>Gets <c>tan(fieldOfView / 2)</c> for perspective projection (vertical).</summary>
    public double TanHalfFovY { get; }

    /// <summary>Gets half the visible world height for orthographic projection.</summary>
    public double OrthographicHalfHeight { get; }

    /// <summary>Gets the full world-to-view matrix (translation followed by rotation).</summary>
    public Matrix4x4D WorldToView => Translation * Rotation;

    /// <summary>
    ///     Composes the model-to-view transform of a mesh instance with the camera-relative
    ///     translation applied before the rotation.
    /// </summary>
    /// <param name="world">The instance's model-to-world transform.</param>
    /// <returns>The model-to-view transform.</returns>
    public Matrix4x4D ComposeModelToView(Matrix4x4D world) => world * Translation * Rotation;
}
