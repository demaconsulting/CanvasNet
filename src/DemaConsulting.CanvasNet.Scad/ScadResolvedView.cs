using DemaConsulting.CanvasNet.Render3D.SceneRendering;

namespace DemaConsulting.CanvasNet.Scad;

/// <summary>
///     The view a render actually used, reported to <see cref="ScadRenderOptions.ResolvedViewHandler"/>.
/// </summary>
/// <remarks>
///     The angles describe the direction from the target to the eye in the orbit convention of
///     <see cref="Camera.FitToBounds"/> (azimuth about +Z counter-clockwise seen from above, 0 = looking from -Y;
///     elevation above the XY plane), whichever way the camera was chosen; for a model that is entirely 2D with no
///     explicit view they are 0 and 90 (a top view).
/// </remarks>
/// <param name="AzimuthDegrees">The azimuth of the view direction in degrees.</param>
/// <param name="ElevationDegrees">The elevation of the view direction in degrees.</param>
/// <param name="Projection">The projection that was used.</param>
/// <param name="IsFlatModel">Whether the model is entirely two-dimensional (a 2D result at the top level, not a mix of 2D and 3D).</param>
/// <param name="Source">Where the view came from.</param>
public sealed record ScadResolvedView(
    double AzimuthDegrees,
    double ElevationDegrees,
    CameraProjection Projection,
    bool IsFlatModel,
    ScadViewSource Source);
