using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Geometry;

namespace DemaConsulting.CanvasNet.Scad;

/// <content>
///     The geometry and rendering entry points of the document.
/// </content>
public sealed partial class ScadDocument
{
    /// <summary>
    ///     Evaluates the program and builds its geometry as a Render3D <see cref="Scene"/>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The model is the implicit union of the top-level instances (or, when the program uses <c>!</c>, of
    ///         the marked subtrees). Solids become mesh nodes in right-handed Z-up model space; a 2D shape that is
    ///         not extruded becomes a thin slab of <see cref="ScadRenderOptions.FlatShapeThickness"/>. A model that
    ///         produces no geometry yields a scene with an empty root, which renders as the background.
    ///     </para>
    ///     <para>
    ///         Everything that cannot be built is reported, never silently skipped: invalid arguments as warnings,
    ///         constructs not supported yet as unsupported features (see <see cref="ScadUnsupportedFeature"/>), all
    ///         through the handlers and strict mode of <see cref="ScadRenderOptions.Evaluation"/>, with the same
    ///         buffering, ordering, de-duplication and cap as <see cref="Evaluate"/>. Handlers run on the calling
    ///         thread after the build finished (and before an exception is thrown). The build is bounded by
    ///         <see cref="ScadLimits"/>, including <see cref="ScadLimits.MaxTriangles"/>, and never recurses, so it
    ///         cannot overflow the stack of the calling thread whatever its size.
    ///     </para>
    ///     <para>
    ///         The document is immutable and holds no state between calls, so it can be built from several threads
    ///         at once; every call returns a new scene.
    ///     </para>
    /// </remarks>
    /// <param name="options">Optional render options; <see langword="null"/> uses the defaults. Only the evaluation options and the flat-shape thickness matter here.</param>
    /// <param name="cancellationToken">A token that stops the evaluation and the build.</param>
    /// <returns>The scene.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled before or during the build.</exception>
    /// <exception cref="ScadEvaluationException">
    ///     Thrown, with the source location, when a limit (including the triangle limit) is exceeded, an
    ///     <c>assert</c> fails, or the program is otherwise invalid beyond recovery.
    /// </exception>
    /// <exception cref="ScadUnsupportedFeatureException">
    ///     Thrown when <see cref="ScadEvaluationOptions.ThrowOnUnsupportedFeature"/> is set and the program uses an
    ///     unsupported feature.
    /// </exception>
    /// <exception cref="ScadSyntaxException">Thrown when an included or used file contains a syntax error.</exception>
    /// <exception cref="InvalidDataException">Thrown when an included or used file is longer than 4,194,304 characters.</exception>
    /// <exception cref="IOException">Thrown when an included or used file cannot be read.</exception>
    public Scene BuildScene(ScadRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        var effective = options ?? new ScadRenderOptions();
        var evaluation = effective.Evaluation ?? new ScadEvaluationOptions();
        cancellationToken.ThrowIfCancellationRequested();

        var sink = new ScadEvaluationSink(evaluation.Limits);
        ScadBuiltScene built;
        try
        {
            built = BuildGeometry(effective, evaluation, sink, cancellationToken);
        }
        catch
        {
            // The events recorded before the failure are still delivered; a failing handler must not hide the failure
            DeliverIgnoringHandlerFailures(sink, evaluation);
            throw;
        }

        // On success a failing handler propagates, as documented
        sink.Deliver(evaluation.EchoHandler, evaluation.WarningHandler, evaluation.UnsupportedFeatureHandler);
        return built.Scene;
    }

    /// <summary>
    ///     Evaluates the program, builds its geometry and draws it onto a new <see cref="Surface"/>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Without a <see cref="ScadRenderOptions.Camera"/> the view is fitted to the model: an isometric view
    ///         by default (<see cref="ScadRenderOptions.ViewAzimuthDegrees"/>,
    ///         <see cref="ScadRenderOptions.ViewElevationDegrees"/>, <see cref="ScadRenderOptions.Projection"/>),
    ///         which equals <see cref="SceneRenderer.RenderFitted"/> for the same scene. Surfaces are neutral gray
    ///         on a white background unless the model uses <c>color()</c> or
    ///         <see cref="ScadRenderOptions.SceneOptions"/> says otherwise. Rendering is deterministic: two renders
    ///         of the same document and options are byte-identical, for every degree of parallelism.
    ///     </para>
    ///     <para>
    ///         Warnings, unsupported features and echo output of all three stages (evaluation, geometry, renderer
    ///         operand checks) go through <see cref="ScadRenderOptions.Evaluation"/>, in the order they occurred,
    ///         on the calling thread, once, before the method returns or throws; a renderer report is mapped back to
    ///         the source location of the module that produced the mesh. The size and the camera are validated
    ///         before anything is evaluated. The caller owns and must dispose the returned surface.
    ///     </para>
    ///     <para>
    ///         The document is immutable and holds no state between calls, so it can be rendered from several
    ///         threads at once.
    ///     </para>
    /// </remarks>
    /// <param name="width">The width in pixels, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="height">The height in pixels, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="options">Optional render options; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">A token that stops the evaluation, the build and the rendering.</param>
    /// <returns>A new <paramref name="width"/> by <paramref name="height"/> surface.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the size is outside 1 to <see cref="Surface.MaxDimension"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <see cref="ScadRenderOptions.Camera"/> is invalid (for example eye equal to target).</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled before or during the work.</exception>
    /// <exception cref="ScadEvaluationException">
    ///     Thrown when a limit is exceeded (the message names the location when there is one), an <c>assert</c>
    ///     fails, or the renderer's fragment budget (<see cref="SceneRenderOptions.MaxFragmentsPerTile"/>) is exceeded.
    /// </exception>
    /// <exception cref="ScadUnsupportedFeatureException">
    ///     Thrown when <see cref="ScadEvaluationOptions.ThrowOnUnsupportedFeature"/> is set and any stage reports an
    ///     unsupported feature.
    /// </exception>
    /// <exception cref="ScadSyntaxException">Thrown when an included or used file contains a syntax error.</exception>
    /// <exception cref="InvalidDataException">Thrown when an included or used file is longer than 4,194,304 characters.</exception>
    /// <exception cref="IOException">Thrown when an included or used file cannot be read.</exception>
    public Surface Render(int width, int height, ScadRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        // Validate the cheap arguments before any work: the size, then the camera
        if (width < 1 || width > Surface.MaxDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, $"The width must be between 1 and {Surface.MaxDimension}.");
        }

        if (height < 1 || height > Surface.MaxDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, $"The height must be between 1 and {Surface.MaxDimension}.");
        }

        var effective = options ?? new ScadRenderOptions();
        var evaluation = effective.Evaluation ?? new ScadEvaluationOptions();
        cancellationToken.ThrowIfCancellationRequested();
        if (effective.Camera is not null)
        {
            ValidateCamera(effective.Camera);
        }

        var sink = new ScadEvaluationSink(evaluation.Limits);
        Surface surface;
        try
        {
            surface = BuildAndDraw(effective, evaluation, sink, width, height, cancellationToken);
        }
        catch (RenderBudgetExceededException ex)
        {
            // A resource failure of the renderer is an error of this document too, with advice on what to change
            DeliverIgnoringHandlerFailures(sink, evaluation);
            throw new ScadEvaluationException(
                "The model needs more fragments per render tile than SceneOptions.MaxFragmentsPerTile allows; raise that limit or simplify the model (fewer or smaller CSG operands).",
                ex);
        }
        catch
        {
            DeliverIgnoringHandlerFailures(sink, evaluation);
            throw;
        }

        // On success a failing handler propagates, as documented, but must not leak the surface
        try
        {
            sink.Deliver(evaluation.EchoHandler, evaluation.WarningHandler, evaluation.UnsupportedFeatureHandler);
        }
        catch
        {
            surface.Dispose();
            throw;
        }

        return surface;
    }

    /// <summary>
    ///     Evaluates the program and builds the geometry, recording every event in the sink.
    /// </summary>
    /// <param name="options">The render options.</param>
    /// <param name="evaluation">The effective evaluation options.</param>
    /// <param name="sink">The sink that records the events of both stages.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The scene and its location map.</returns>
    private ScadBuiltScene BuildGeometry(ScadRenderOptions options, ScadEvaluationOptions evaluation, ScadEvaluationSink sink, CancellationToken cancellationToken)
    {
        var output = RunEvaluation(evaluation, cancellationToken, sink);
        var settings = new ScadGeometrySettings(
            evaluation.Limits.MaxTriangles,
            options.FlatShapeThickness,
            evaluation.ThrowOnUnsupportedFeature,
            cancellationToken);
        return ScadGeometryBuilder.Build(output.Roots, settings, sink);
    }

    /// <summary>
    ///     Builds the geometry and draws it, mapping the renderer's reports into the sink.
    /// </summary>
    /// <param name="options">The render options.</param>
    /// <param name="evaluation">The effective evaluation options.</param>
    /// <param name="sink">The sink that records the events of all stages.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The rendered surface.</returns>
    private Surface BuildAndDraw(ScadRenderOptions options, ScadEvaluationOptions evaluation, ScadEvaluationSink sink, int width, int height, CancellationToken cancellationToken)
    {
        var built = BuildGeometry(options, evaluation, sink, cancellationToken);

        // The renderer only checks CSG operands when a handler is present, so install ours only when someone listens
        Action<SceneRenderUnsupportedFeature>? handler = null;
        if (evaluation.UnsupportedFeatureHandler is not null || evaluation.ThrowOnUnsupportedFeature)
        {
            var reporter = new ScadReporter(sink, evaluation.ThrowOnUnsupportedFeature);
            handler = report => reporter.Unsupported(
                ScadSceneOptions.MapFeature(report.Feature),
                report.Message,
                built.Map.Find(report.Node));
        }

        var sceneOptions = ScadSceneOptions.Create(options.SceneOptions, handler);
        var camera = options.Camera ?? FitCamera(built.Scene, options, width / (double)height);
        return SceneRenderer.Render(built.Scene, camera, width, height, sceneOptions, cancellationToken);
    }

    /// <summary>
    ///     Fits the default camera to the scene; it never throws for a scene the build returned.
    /// </summary>
    /// <remarks>
    ///     The build keeps every coordinate finite and within <see cref="ScadGeo.MaxCoordinate"/>, so the fit
    ///     normally succeeds. Should the bounds still be unusable (not finite or out of range) or the fit be
    ///     rejected, the camera frames the unit cube around the origin instead of failing the render.
    /// </remarks>
    /// <param name="scene">The scene.</param>
    /// <param name="options">The render options.</param>
    /// <param name="aspectRatio">The width divided by the height.</param>
    /// <returns>The camera.</returns>
    internal static Camera FitCamera(Scene scene, ScadRenderOptions options, double aspectRatio)
    {
        var bounds = scene.ComputeBounds();
        if (!ScadGeo.IsSafe(bounds))
        {
            bounds = BoundingBox3D.Empty;
        }

        try
        {
            return Camera.FitToBounds(bounds, options.ViewAzimuthDegrees, options.ViewElevationDegrees, aspectRatio, options.Projection);
        }
        catch (ArgumentException)
        {
            return Camera.FitToBounds(BoundingBox3D.Empty, options.ViewAzimuthDegrees, options.ViewElevationDegrees, aspectRatio, options.Projection);
        }
    }

    /// <summary>
    ///     Validates a camera by rendering an empty scene with it, so the renderer's own rules decide.
    /// </summary>
    /// <param name="camera">The camera.</param>
    /// <exception cref="ArgumentException">Thrown when the renderer rejects the camera.</exception>
    private static void ValidateCamera(Camera camera)
    {
        using var probe = SceneRenderer.Render(
            new Scene(new GroupNode([])),
            camera,
            1,
            1,
            new SceneRenderOptions { Supersampling = 1, MaxDegreeOfParallelism = 1 });
    }
}
