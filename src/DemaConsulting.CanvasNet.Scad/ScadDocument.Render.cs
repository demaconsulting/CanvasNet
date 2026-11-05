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
    ///         produces no geometry yields a scene with an empty root, which renders as the background. A <c>color</c>
    ///         alpha becomes the alpha of the appearance of the geometry, and the <c>#</c> and <c>%</c> modifiers on solids
    ///         become Render3D overlays (<see cref="OverlayNode"/>): <c>scene.ComputeBounds()</c> includes them, while
    ///         <c>scene.ComputeBounds(includeOverlays: false)</c> measures the model without ghosts and highlights.
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
    ///         The viewport special variables <c>$vpt</c>, <c>$vpr</c>, <c>$vpd</c> and <c>$vpf</c> are defined while the
    ///         document is evaluated, with the values that follow from <paramref name="options"/>: an explicit
    ///         <see cref="ScadRenderOptions.Camera"/>, or explicit <see cref="ScadRenderOptions.ViewAzimuthDegrees"/> and
    ///         <see cref="ScadRenderOptions.ViewElevationDegrees"/>, set them (they win over an
    ///         <see cref="ScadEvaluationOptions.Variables"/> override and a top-level assignment of the document);
    ///         otherwise they have the defaults of the default view. An explicit camera that the renderer would reject
    ///         (for example eye equal to target) is ignored like an absent camera: it defines none of the four
    ///         variables, and <see cref="BuildScene"/> never throws for it, whereas <see cref="Render"/> does. A model that reads them (for example to size or place
    ///         geometry) therefore builds a different scene for a different camera or view angle, exactly as
    ///         <see cref="Render"/> does for the same options. The build chooses no camera and never calls
    ///         <see cref="ScadRenderOptions.ResolvedViewHandler"/>; <see cref="ScadRenderOptions.Projection"/> and the size of
    ///         a surface do not change the scene. Declared anchors (<c>echo("anchor", "name", [x, y, z])</c>) never
    ///         change the scene either; they are reported by <see cref="Render"/> only (see
    ///         <see cref="ScadRenderOptions.AnchorHandler"/>), a malformed one produces a warning that starts with
    ///         <c>scad-anchor-invalid</c> here as well.
    ///     </para>
    ///     <para>
    ///         <see cref="BuildScene"/> evaluates the program itself and delivers its echo, warnings, and unsupported
    ///         features through the handlers of <see cref="ScadRenderOptions.Evaluation"/>, once. Do not call
    ///         <see cref="Evaluate"/> first to "check" the program: it only runs the program (and its handlers), so
    ///         calling both runs everything twice. Use <see cref="Evaluate"/> alone when you need only the echo and
    ///         the diagnostics without a scene.
    ///     </para>
    ///     <para>
    ///         The document is immutable and holds no state between calls, so it can be built from several threads
    ///         at once; every call returns a new scene.
    ///     </para>
    /// </remarks>
    /// <param name="options">
    ///     Optional render options; <see langword="null"/> uses the defaults. The scene depends on the evaluation options,
    ///     the camera and the view angles (they define the viewport variables, see the remarks; a camera the renderer
    ///     would reject defines none), the flat-shape
    ///     thickness, the highlight and ghost colors, and the font resolution; the projection and the resolved-view
    ///     handler have no effect.
    /// </param>
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
            built = BuildGeometry(effective, evaluation, sink, ScadViewport.Plan(effective, IsUsableCamera(effective.Camera)), cancellationToken).Built;
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
    ///         which equals <see cref="SceneRenderer.RenderFitted"/> for the same scene. A model that is entirely 2D
    ///         (no 3D object at the top level, for example a <c>projection()</c> drawing sheet) with no explicit view
    ///         is drawn as a flat top-down orthographic drawing instead, lit by ambient light only so that a shape has
    ///         exactly its own color. The document can steer the view with the viewport variables <c>$vpt</c>,
    ///         <c>$vpr</c>, <c>$vpd</c> and <c>$vpf</c> (see <see cref="ScadRenderOptions.Camera"/> for the order of
    ///         precedence). Surfaces are neutral gray
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
    ///         Declared anchors (<c>echo("anchor", "name", [x, y, z])</c>, with the enclosing transforms applied) are
    ///         delivered to <see cref="ScadRenderOptions.AnchorHandler"/> after the view is chosen and before the
    ///         drawing, projected with the camera that is rendered with. A malformed anchor produces a warning that
    ///         starts with <c>scad-anchor-invalid</c> and is skipped.
    ///     </para>
    ///     <para>
    ///         <see cref="Render"/> evaluates the program itself (evaluation, geometry, and drawing in one call), so do
    ///         not call <see cref="Evaluate"/> first: it would run the program and its handlers a second time. The echo
    ///         and the warnings of the render arrive once, through <see cref="ScadRenderOptions.Evaluation"/>, and the
    ///         anchors through <see cref="ScadRenderOptions.AnchorHandler"/>. Use <see cref="Evaluate"/> alone when you
    ///         need only the echo and the diagnostics without drawing.
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
    /// <param name="plan">The viewport plan that tells the document what <c>$vpt</c>, <c>$vpr</c>, <c>$vpd</c> and <c>$vpf</c> are.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The scene and its location map, and the viewport variables the document defined.</returns>
    private (ScadBuiltScene Built, ScadDocumentViewport Viewport) BuildGeometry(
        ScadRenderOptions options,
        ScadEvaluationOptions evaluation,
        ScadEvaluationSink sink,
        ScadViewportPlan plan,
        CancellationToken cancellationToken)
    {
        var output = RunEvaluation(evaluation, cancellationToken, sink, plan);
        var settings = new ScadGeometrySettings(
            evaluation.Limits.MaxTriangles,
            options.FlatShapeThickness,
            evaluation.ThrowOnUnsupportedFeature,
            cancellationToken,
            options.HighlightColor,
            options.GhostColor,
            options.FontResolution);
        return (ScadGeometryBuilder.Build(output.Roots, settings, sink), output.Viewport ?? ScadDocumentViewport.None);
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
        var plan = ScadViewport.Plan(options);
        var (built, viewport) = BuildGeometry(options, evaluation, sink, plan, cancellationToken);

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

        // The camera is chosen after the build (it needs the bounds and whether the model is flat); a model that is
        // entirely 2D with no chosen view is drawn flat: ambient light only, so a shape has exactly its own color
        var camera = ScadViewport.ChooseCamera(plan, viewport, built.IsFlatModel, built.Scene, width / (double)height, out var view);
        options.ResolvedViewHandler?.Invoke(view);
        ReportAnchors(options, built, camera, width, height);
        var sceneOptions = ScadSceneOptions.Create(options.SceneOptions, handler, view.Source == ScadViewSource.FlatDefault);
        return SceneRenderer.Render(built.Scene, camera, width, height, sceneOptions, cancellationToken);
    }

    /// <summary>
    ///     Delivers the declared anchors, projected with the camera the render uses, to the anchor handler.
    /// </summary>
    /// <remarks>
    ///     The pixel comes from <see cref="Camera.TryProject"/> with the final camera and the output size, so it is
    ///     exactly the position the rasterizer gives the point. Nothing is computed when no handler is installed.
    /// </remarks>
    /// <param name="options">The render options.</param>
    /// <param name="built">The built scene with its anchors.</param>
    /// <param name="camera">The camera the render draws with.</param>
    /// <param name="width">The output width in pixels.</param>
    /// <param name="height">The output height in pixels.</param>
    private static void ReportAnchors(ScadRenderOptions options, ScadBuiltScene built, Camera camera, int width, int height)
    {
        if (options.AnchorHandler is not { } handler || built.Anchors is null)
        {
            return;
        }

        foreach (var anchor in built.Anchors)
        {
            // A point that cannot be projected (behind a perspective eye) has no pixel and is not inside the image
            var projected = camera.TryProject(anchor.Point, width, height, out var pixel);
            var inside = projected && pixel.X >= 0.0 && pixel.X < width && pixel.Y >= 0.0 && pixel.Y < height;
            handler(new ScadAnchor(anchor.Name, anchor.Point, projected ? pixel : null, inside, anchor.Location));
        }
    }

    /// <summary>
    ///     Fits the default camera to the scene with the view angles and the projection of the options; it never throws
    ///     for a scene the build returned.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="options">The render options.</param>
    /// <param name="aspectRatio">The width divided by the height.</param>
    /// <returns>The camera.</returns>
    internal static Camera FitCamera(Scene scene, ScadRenderOptions options, double aspectRatio) =>
        ScadViewport.FitScene(scene, options.ViewAzimuthDegrees, options.ViewElevationDegrees, aspectRatio, options.Projection);

    /// <summary>
    ///     Tells whether the renderer accepts a camera, by the same probe <see cref="Render"/> validates with, so there
    ///     is one definition of an invalid camera; it never throws.
    /// </summary>
    /// <param name="camera">The camera, or <see langword="null"/> for none.</param>
    /// <returns><see langword="true"/> when there is no camera to reject, or the renderer accepts it.</returns>
    private static bool IsUsableCamera(Camera? camera)
    {
        if (camera is null)
        {
            return true;
        }

        try
        {
            ValidateCamera(camera);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
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
