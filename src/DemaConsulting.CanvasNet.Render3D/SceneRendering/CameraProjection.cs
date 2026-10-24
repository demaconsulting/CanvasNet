namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     The projection used by a <see cref="Camera"/>.
/// </summary>
public enum CameraProjection
{
    /// <summary>Perspective projection: objects shrink with distance (field of view is <see cref="Camera.FieldOfViewDegrees"/>).</summary>
    Perspective,

    /// <summary>Orthographic projection: parallel rays; the visible height is <see cref="Camera.OrthographicHeight"/> world units.</summary>
    Orthographic,
}
