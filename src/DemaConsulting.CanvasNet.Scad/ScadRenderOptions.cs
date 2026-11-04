using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Scad.Geometry;

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

    /// <summary>Whether <see cref="ViewAzimuthDegrees"/> was set explicitly.</summary>
    private readonly bool _azimuthExplicit;

    /// <summary>Whether <see cref="ViewElevationDegrees"/> was set explicitly.</summary>
    private readonly bool _elevationExplicit;

    /// <summary>Whether <see cref="Projection"/> was set explicitly.</summary>
    private readonly bool _projectionExplicit;

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
    ///     or <see langword="null"/> (default) to fit the view to the model (see the remarks).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         An explicit camera is the highest-precedence source of the view: it is used as given (no fitting) and
    ///         it also defines the viewport variables <c>$vpt</c>, <c>$vpr</c>, <c>$vpd</c> and <c>$vpf</c> that the
    ///         document sees. Without a camera the view is fitted to the model from, in order of precedence: the
    ///         explicitly set <see cref="ViewAzimuthDegrees"/> and <see cref="ViewElevationDegrees"/>; the document
    ///         (a top-level assignment of the viewport variables, or a <see cref="ScadEvaluationOptions.Variables"/>
    ///         override of them, which wins over the assignment); a flat top view for a model that is entirely 2D; the
    ///         isometric default.
    ///     </para>
    ///     <para>
    ///         An invalid camera is rejected with an <see cref="ArgumentException"/> from the renderer before the model
    ///         is evaluated. <see cref="ScadDocument.BuildScene(ScadRenderOptions?, CancellationToken)"/> ignores the
    ///         camera, but the document still sees the viewport variables.
    ///     </para>
    /// </remarks>
    public Camera? Camera { get; init; }

    /// <summary>
    ///     Gets the azimuth in degrees of the fitted view (rotation about +Z); 45 by default, which together with the
    ///     default elevation gives the isometric view of the renderer. Used only when <see cref="Camera"/> is
    ///     <see langword="null"/>.
    /// </summary>
    /// <remarks>
    ///     Setting the azimuth or the elevation (even to the default value) makes the view explicit: it is then used
    ///     for every model, including one that is entirely 2D (which is drawn from the top when no view is chosen),
    ///     and it defines <c>$vpr</c> for the document (<c>[90 - elevation, 0, azimuth]</c>, wrapped to 0 to 360).
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is not finite.</exception>
    public double ViewAzimuthDegrees
    {
        get => _viewAzimuthDegrees;
        init
        {
            _viewAzimuthDegrees = RequireFinite(value);
            _azimuthExplicit = true;
        }
    }

    /// <summary>
    ///     Gets the elevation in degrees of the fitted view (angle above the XY plane); about 35.26 by default
    ///     (isometric). Used only when <see cref="Camera"/> is <see langword="null"/>.
    /// </summary>
    /// <remarks>See <see cref="ViewAzimuthDegrees"/> for how an explicit value is treated.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is not finite.</exception>
    public double ViewElevationDegrees
    {
        get => _viewElevationDegrees;
        init
        {
            _viewElevationDegrees = RequireFinite(value);
            _elevationExplicit = true;
        }
    }

    /// <summary>
    ///     Gets the projection of the fitted view; <see cref="CameraProjection.Perspective"/> by default, except for a
    ///     model that is entirely 2D with no explicit view, which defaults to <see cref="CameraProjection.Orthographic"/>.
    ///     Used only when <see cref="Camera"/> is <see langword="null"/>.
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
            _projectionExplicit = true;
        }
    }

    /// <summary>
    ///     Gets the handler that is told which view a render used, or <see langword="null"/> (default) for none.
    /// </summary>
    /// <remarks>
    ///     <see cref="ScadDocument.Render(int, int, ScadRenderOptions?, CancellationToken)"/> calls it once, on the
    ///     calling thread, after the model was built and before it draws, with the resolved azimuth, elevation and
    ///     projection (a model that is entirely 2D reports <see cref="ScadViewSource.FlatDefault"/> with a top view).
    ///     An exception thrown by the handler propagates to the caller. <see cref="ScadDocument.BuildScene(ScadRenderOptions?, CancellationToken)"/>
    ///     does not choose a camera and never calls it.
    /// </remarks>
    public Action<ScadResolvedView>? ResolvedViewHandler { get; init; }

    /// <summary>Gets a value indicating whether the azimuth or the elevation was set explicitly.</summary>
    internal bool IsViewExplicit => _azimuthExplicit || _elevationExplicit;

    /// <summary>Gets a value indicating whether the projection was set explicitly.</summary>
    internal bool IsProjectionExplicit => _projectionExplicit;

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
    ///     Gets the color of the translucent copy that the <c>#</c> (highlight) modifier draws over the highlighted
    ///     subtree; translucent pink-red (255, 64, 128, alpha 110) by default. Any alpha is accepted (255 draws an
    ///     opaque copy, 0 an invisible one).
    /// </summary>
    /// <remarks>
    ///     The highlighted subtree is still rendered normally, with its own colors, and still takes part in the
    ///     boolean operations around it; the highlight is an additional translucent body in this color, drawn where
    ///     the subtree is (including where it was subtracted). It replaces every <c>color()</c> inside the subtree.
    ///     A <c>#</c> on a 2D shape that is not extruded is reported as unsupported instead.
    /// </remarks>
    public Rgba32 HighlightColor { get; init; } = ScadGeometryContext.DefaultHighlightColor;

    /// <summary>
    ///     Gets the color of the translucent ghost that the <c>%</c> (background) modifier draws instead of the
    ///     subtree; translucent neutral gray (128, 128, 128, alpha 80) by default. Any alpha is accepted.
    /// </summary>
    /// <remarks>
    ///     The ghost does not take part in the boolean operations around it and contributes no propagated bounds
    ///     (<c>resize</c>, <c>hull</c>, and <c>minkowski</c> ignore it), but it is part of <c>Scene.ComputeBounds()</c>, the
    ///     bounds that fit the default camera. It replaces every <c>color()</c> inside the subtree.
    ///     A <c>%</c> on a 2D shape that is not extruded is reported as unsupported and omitted instead.
    /// </remarks>
    public Rgba32 GhostColor { get; init; } = ScadGeometryContext.DefaultGhostColor;

    /// <summary>
    ///     Gets where the <c>text()</c> module looks for the font named by its <c>font</c> argument, or
    ///     <see langword="null"/> (default) for a default <see cref="ScadFontResolutionOptions"/> (the installed fonts of
    ///     the host are searched).
    /// </summary>
    /// <remarks>
    ///     Only an explicit <c>font</c> argument is affected: text without one always uses the bundled Liberation Sans
    ///     Regular font. See <see cref="ScadFontResolutionOptions"/> for the search order and how to make an explicit
    ///     font independent of the host.
    /// </remarks>
    public ScadFontResolutionOptions? FontResolution { get; init; }

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
