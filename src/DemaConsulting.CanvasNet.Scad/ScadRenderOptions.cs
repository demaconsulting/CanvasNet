using DemaConsulting.CanvasNet.Render3D.SceneRendering;

namespace DemaConsulting.CanvasNet.Scad;

/// <summary>
///     Options that control one call of <see cref="ScadDocument.BuildScene(ScadRenderOptions?, CancellationToken)"/>
///     or <see cref="ScadDocument.Render(int, int, ScadRenderOptions?, CancellationToken)"/>.
/// </summary>
/// <remarks>
///     <para>
///         The options compose rather than copy: everything that steers evaluation (<c>-D</c> style variable
///         overrides, <c>$t</c>, resource limits, the echo, warning and unsupported-feature handlers and strict mode)
///         lives in the one <see cref="Evaluation"/> object, so there is a single definition of how events are
///         reported and a single place that decides whether an unsupported feature aborts. Geometry-building and
///         rendering problems are reported through the same handlers, in the order they occur: evaluation first,
///         then geometry, then the renderer.
///     </para>
///     <para>
///         Every numeric property is validated when it is set (fail-closed) and the instance is immutable after
///         initialization, so an options object can be shared between threads and calls. A
///         <see cref="SceneRenderOptions"/> supplied in <see cref="SceneOptions"/> is used
///         for the look of the image (background, colors, lights, supersampling, parallelism and budget) but its
///         <see cref="SceneRenderOptions.UnsupportedFeatureHandler"/> and
///         <see cref="SceneRenderOptions.ThrowOnUnsupportedFeature"/> are ignored: reporting always goes through
///         <see cref="Evaluation"/>.
///     </para>
/// </remarks>
public sealed class ScadRenderOptions
{
    /// <summary>The default thickness of a flat (2D) shape rendered without an extrusion.</summary>
    private const double DefaultFlatShapeThickness = 0.1;

    /// <summary>The backing field of <see cref="ViewAzimuthDegrees"/>.</summary>
    private readonly double _viewAzimuthDegrees = Camera.DefaultAzimuthDegrees;

    /// <summary>The backing field of <see cref="ViewElevationDegrees"/>.</summary>
    private readonly double _viewElevationDegrees = Camera.DefaultElevationDegrees;

    /// <summary>The backing field of <see cref="Projection"/>.</summary>
    private readonly CameraProjection _projection = CameraProjection.Perspective;

    /// <summary>The backing field of <see cref="FlatShapeThickness"/>.</summary>
    private readonly double _flatShapeThickness = DefaultFlatShapeThickness;

    /// <summary>
    ///     Gets the options of the evaluation that precedes geometry building (variable overrides, <c>$t</c>, limits,
    ///     handlers and strict mode), or <see langword="null"/> for a default <see cref="ScadEvaluationOptions"/>.
    /// </summary>
    /// <remarks>
    ///     <see cref="ScadEvaluationOptions.ThrowOnUnsupportedFeature"/> applies to every stage: the first unsupported
    ///     feature reported by the evaluator, the geometry builder or the renderer is thrown as a
    ///     <see cref="ScadUnsupportedFeatureException"/>. <see cref="ScadLimits.MaxTriangles"/> in
    ///     <see cref="ScadEvaluationOptions.Limits"/> bounds the size of the geometry.
    /// </remarks>
    public ScadEvaluationOptions? Evaluation { get; init; }

    /// <summary>
    ///     Gets the camera used by <see cref="ScadDocument.Render(int, int, ScadRenderOptions?, CancellationToken)"/>,
    ///     or <see langword="null"/> (default) to fit the view to the model with <see cref="ViewAzimuthDegrees"/>,
    ///     <see cref="ViewElevationDegrees"/> and <see cref="Projection"/>.
    /// </summary>
    /// <remarks>
    ///     An invalid camera is rejected with an <see cref="ArgumentException"/> from the renderer before the model is
    ///     evaluated. <see cref="ScadDocument.BuildScene(ScadRenderOptions?, CancellationToken)"/> ignores the camera.
    /// </remarks>
    public Camera? Camera { get; init; }

    /// <summary>
    ///     Gets the azimuth in degrees of the fitted view (rotation about +Z); 45 by default, which together with the
    ///     default elevation gives the isometric view of the renderer. Used only when <see cref="Camera"/> is
    ///     <see langword="null"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is not finite.</exception>
    public double ViewAzimuthDegrees
    {
        get => _viewAzimuthDegrees;
        init => _viewAzimuthDegrees = RequireFinite(value);
    }

    /// <summary>
    ///     Gets the elevation in degrees of the fitted view (angle above the XY plane); about 35.26 by default
    ///     (isometric). Used only when <see cref="Camera"/> is <see langword="null"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is not finite.</exception>
    public double ViewElevationDegrees
    {
        get => _viewElevationDegrees;
        init => _viewElevationDegrees = RequireFinite(value);
    }

    /// <summary>
    ///     Gets the projection of the fitted view; <see cref="CameraProjection.Perspective"/> by default. Used only
    ///     when <see cref="Camera"/> is <see langword="null"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is not a defined <see cref="CameraProjection"/>.</exception>
    public CameraProjection Projection
    {
        get => _projection;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(Projection), value, "The projection must be a defined CameraProjection value.");
            }

            _projection = value;
        }
    }

    /// <summary>
    ///     Gets the look-and-feel and performance settings of the renderer (background, default color, lights,
    ///     supersampling, parallelism, fragment budget), or <see langword="null"/> for
    ///     <see cref="SceneRenderOptions.Default"/> (white background, neutral gray surfaces).
    /// </summary>
    /// <remarks>
    ///     Its unsupported-feature handler and strict flag are ignored (see the remarks of this type). A model
    ///     <c>color()</c> overrides <see cref="SceneRenderOptions.DefaultColor"/>.
    /// </remarks>
    public SceneRenderOptions? SceneOptions { get; init; }

    /// <summary>
    ///     Gets the thickness, in model units, of the slab that represents a 2D shape (<c>square</c>, <c>circle</c>,
    ///     <c>polygon</c>) that is rendered without a <c>linear_extrude</c>; 0.1 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is not finite and greater than zero.</exception>
    public double FlatShapeThickness
    {
        get => _flatShapeThickness;
        init
        {
            if (!double.IsFinite(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(FlatShapeThickness), value, "The thickness must be finite and greater than zero.");
            }

            _flatShapeThickness = value;
        }
    }

    /// <summary>
    ///     Validates a view angle.
    /// </summary>
    /// <param name="value">The angle in degrees.</param>
    /// <param name="propertyName">The property name for the exception.</param>
    /// <returns>The value, unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not finite.</exception>
    private static double RequireFinite(double value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(propertyName, value, "The angle must be finite.");
        }

        return value;
    }
}
