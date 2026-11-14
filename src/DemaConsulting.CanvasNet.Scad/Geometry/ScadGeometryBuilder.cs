// cspell:ignore multmatrix minkowski
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     Converts the instantiation tree of an evaluation into a Render3D scene.
/// </summary>
/// <remarks>
///     <para>
///         <b>Iterative by construction.</b> The tree can be thousands of levels deep (a recursive user module is
///         bounded only by <see cref="ScadLimits.MaxRecursionDepth"/>), so the engine walks it with an explicit stack
///         in post-order, exactly like the renderer's CSG compiler, and no code in the <c>Geometry</c> namespace
///         recurses, so the stack a build needs does not depend on the depth of the tree. One level still runs a fixed
///         chain of calls (down to the exact predicates of the convex hull, whose arbitrary-precision arithmetic is
///         large), and the runtime may compile code on the calling thread while it runs; on .NET 11 with the framework
///         compiled just in time, that chain and the compilation together exceeded a 64 KiB stack. A build therefore
///         runs inline only when <see cref="RuntimeHelpers.TryEnsureSufficientExecutionStack"/> reports the runtime's
///         minimum execution stack, and otherwise on a dedicated thread (<see cref="BuildThreadStackSize"/>), which
///         gives the same scene, so the build behaves identically on any caller stack, including a 64 KiB one.
///     </para>
///     <para>
///         <b>Dispatch.</b> A group (a user module, <c>for</c>, a modifier wrapper) and the <c>union</c> and
///         <c>render</c> modules combine their children as an implicit union; a recognized primitive (including
///         <c>text</c>, a flat glyph-region leaf built by <see cref="ScadTextGeometry"/>) builds a
///         leaf; <c>difference</c>, <c>intersection</c>, the transforms (<c>translate</c>, <c>rotate</c>,
///         <c>scale</c>, <c>mirror</c>, <c>multmatrix</c>, <c>resize</c>), <c>color</c> and the extrusions
///         (<c>linear_extrude</c>, <c>rotate_extrude</c>) and the convex combinations <c>hull</c> and
///         <c>minkowski</c> build from the results of their children (see the
///         container table in <c>ScadGeometryBuilder.Unsupported.cs</c>), as does <c>projection</c>, which turns
///         its 3D children into a 2D shape (<see cref="ScadProjections"/>); every other recognized module
///         (<c>offset</c>, <c>surface</c>, <c>import</c>, <c>roof</c>)
///         is reported as an unsupported feature (see <see cref="ScadFeatureIds"/>) and yields
///         nothing, so nothing is silently skipped. Modifiers: <c>!</c> replaces the model with the outermost
///         marked subtrees (without the transforms and colors of their ancestors), <c>#</c> renders normally and
///         additionally as a translucent highlight overlay that shares the subtree's meshes (it still takes part in
///         the booleans around it), <c>%</c> draws a translucent ghost overlay instead of the subtree (the ghost
///         does not take part in the booleans and contributes no propagated bounds, so <c>resize</c> and the bounds of
///         enclosing geometry ignore it, yet it is part of <c>Scene.ComputeBounds()</c>, which fits the default
///         camera); on a 2D shape that is not extruded both are
///         reported (see <c>ScadGeometryBuilder.Modifiers.cs</c>). An anchor instance (a declared
///         <c>echo("anchor", name, [x, y, z])</c>) is never geometry: the build skips it, so it cannot become the
///         first operand of a <c>difference</c>, and a separate pass (<c>ScadGeometryBuilder.Anchors.cs</c>) turns
///         it into a world point with the enclosing matrix transforms applied.
///     </para>
///     <para>
///         <b>Dimensions.</b> A 2D result is a symbolic <see cref="ScadShape2D"/> tree (booleans, planar transforms and
///         colors over 2D leaves); an extrusion distributes over it. Mixing 2D and 3D children in one union keeps the
///         3D ones and warns about the dropped 2D ones. A 2D result at the top of the model becomes thin slabs of
///         <see cref="ScadGeometrySettings.FlatShapeThickness"/> so that it is visible.
///     </para>
///     <para>
///         The builder holds the per-build state, so it is single-use and not thread-safe; independent builds can
///         run concurrently.
///     </para>
/// </remarks>
internal sealed partial class ScadGeometryBuilder
{
    /// <summary>The largest effective triangle count that is tracked (far above any budget; keeps sums from overflowing).</summary>
    private const long EffectiveCap = long.MaxValue / 4;

    /// <summary>
    ///     The stack size in bytes of the dedicated build thread (16 MiB), used only when the caller's stack is below
    ///     the runtime's minimum execution stack.
    /// </summary>
    /// <remarks>
    ///     The walk is iterative, so the build needs only the fixed chain of one level (measured at under 32 KiB,
    ///     including the code the runtime compiles on the thread); 16 MiB leaves a wide margin for other runtimes and
    ///     architectures. It is reserved address space that is committed only as it is touched.
    /// </remarks>
    internal const int BuildThreadStackSize = 16 * 1024 * 1024;

    /// <summary>The name of the dedicated build thread.</summary>
    private const string BuildThreadName = "CanvasNet.Scad geometry build";

    /// <summary>
    ///     A per-thread test seam that forces <see cref="Build"/> onto the dedicated build thread, so that path is
    ///     exercised whatever stack size the platform really grants.
    /// </summary>
    [ThreadStatic]
    internal static bool ForceBuildThreadForCurrentThread;
    /// <summary>The services of this build.</summary>
    private readonly ScadGeometryContext _context;

    /// <summary>The effective triangle count of the nodes visited by <see cref="EffectiveTriangles"/>, by reference.</summary>
    private readonly Dictionary<SceneNode, long> _effectiveTriangles = new(ReferenceEqualityComparer.Instance);

    /// <summary>The number of <c>hull</c> and <c>minkowski</c> instances whose children are being built (overlays are not created inside them).</summary>
    private int _convexDepth;

    /// <summary>
    ///     Initializes a builder.
    /// </summary>
    /// <param name="context">The services of this build.</param>
    private ScadGeometryBuilder(ScadGeometryContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Builds the scene of an evaluated model.
    /// </summary>
    /// <param name="roots">The top-level instances of the evaluation.</param>
    /// <param name="settings">The settings of the build.</param>
    /// <param name="sink">The sink that receives warnings and unsupported features (shared with the evaluation).</param>
    /// <returns>The scene (an empty group when nothing was built) and the location map of its mesh nodes.</returns>
    /// <exception cref="ScadEvaluationException">Thrown, with the location, when the triangle budget is exceeded.</exception>
    /// <exception cref="ScadUnsupportedFeatureException">Thrown in strict mode at the first unsupported feature.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token in <paramref name="settings"/> is canceled.</exception>
    public static ScadBuiltScene Build(IReadOnlyList<ScadInstance> roots, ScadGeometrySettings settings, ScadEvaluationSink sink)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(sink);

        // The walk is iterative, but one level still runs a fixed chain of calls (down to the exact predicates of the
        // convex hull), and the runtime may compile code on this thread while it runs; below the runtime's minimum
        // execution stack the build moves to a dedicated thread so that chain can never overflow the caller's stack
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack() || ForceBuildThreadForCurrentThread)
        {
            return RunOnBuildThread(() => BuildInline(roots, settings, sink), settings.Cancellation);
        }

        return BuildInline(roots, settings, sink);
    }

    /// <summary>
    ///     Builds the scene on the current thread.
    /// </summary>
    /// <param name="roots">The top-level instances of the evaluation.</param>
    /// <param name="settings">The settings of the build.</param>
    /// <param name="sink">The sink that receives warnings and unsupported features.</param>
    /// <returns>The scene and the location map of its mesh nodes.</returns>
    private static ScadBuiltScene BuildInline(IReadOnlyList<ScadInstance> roots, ScadGeometrySettings settings, ScadEvaluationSink sink)
    {
        var context = new ScadGeometryContext(settings, new ScadReporter(sink, settings.ThrowOnUnsupportedFeature));
        var builder = new ScadGeometryBuilder(context);

        // A model that contains '!' is just the marked subtrees; otherwise it is the union of the top-level instances
        var showOnly = FindShowOnlyRoots(roots, context);
        var forest = showOnly.Count > 0 ? showOnly : roots;
        var geo = builder.BuildForest(forest);
        var root = builder.ToSceneRoot(geo);

        // Anchors are resolved over the same forest, so a '!' model reports only the anchors of the marked subtrees;
        // a model that declared none (the evaluation says so) skips the extra walk
        var anchors = new List<ScadWorldAnchor>();
        if (sink.AnchorsDeclared)
        {
            sink.AnchorPasses++;
            anchors = builder.CollectAnchors(forest, settings);
        }

        return new ScadBuiltScene(new Scene(root), context.Map, context.TrianglesCharged, geo.Kind == ScadGeoKind.Flat, anchors);
    }

    /// <summary>
    ///     Runs a build on a dedicated thread with a <see cref="BuildThreadStackSize"/> stack and the caller's cultures,
    ///     and returns its result, rethrowing any exception unchanged.
    /// </summary>
    /// <remarks>
    ///     The number of dedicated threads is bounded by the same fallback slots the evaluator uses
    ///     (<see cref="ScadEvaluator.FallbackSlotsForCurrentThread"/>, at most
    ///     <see cref="ScadEvaluator.MaxConcurrentFallbackThreads"/> at once), so a burst of concurrent small-stack
    ///     callers waits for a slot instead of reserving one stack each. As in the evaluator, the wait honors
    ///     <paramref name="cancellation"/> (a canceled token ends it with <see cref="OperationCanceledException"/>
    ///     without taking a slot), the slot is released after the thread has been joined, and a thread that cannot be
    ///     created or started (<see cref="OutOfMemoryException"/> or <see cref="ThreadStartException"/>) is reported as a
    ///     <see cref="ScadEvaluationException"/> with the slot released. The build and the evaluation of one document
    ///     run one after the other, so they never hold two slots at once.
    /// </remarks>
    /// <param name="build">The build to run.</param>
    /// <param name="cancellation">The token that ends the wait for a slot.</param>
    /// <returns>The result of the build.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled before a slot was acquired.</exception>
    /// <exception cref="ScadEvaluationException">Thrown when the dedicated thread cannot be created or started.</exception>
    private static ScadBuiltScene RunOnBuildThread(Func<ScadBuiltScene> build, CancellationToken cancellation)
    {
        ScadBuiltScene? result = null;
        ExceptionDispatchInfo? failure = null;
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;

        var slots = ScadEvaluator.FallbackSlotsForCurrentThread;
        slots.Acquire(cancellation);
        try
        {
            var thread = StartBuildThread(
                () =>
                {
                    CultureInfo.CurrentCulture = culture;
                    CultureInfo.CurrentUICulture = uiCulture;
                    try
                    {
                        result = build();
                    }
                    catch (Exception ex)
                    {
                        // Every exception is handed back to the caller unchanged, so none may be lost here
                        failure = ExceptionDispatchInfo.Capture(ex);
                    }
                });
            thread.Join();
        }
        finally
        {
            slots.Release();
        }

        failure?.Throw();
        return result!;
    }

    /// <summary>
    ///     Creates and starts the dedicated build thread, converting a failure to obtain it into the documented
    ///     controlled failure.
    /// </summary>
    /// <param name="body">The thread body.</param>
    /// <returns>The started thread.</returns>
    /// <exception cref="ScadEvaluationException">Thrown when the thread cannot be created or started.</exception>
    private static Thread StartBuildThread(ThreadStart body)
    {
        try
        {
            var thread = new Thread(body, BuildThreadStackSize)
            {
                IsBackground = true,
                Name = BuildThreadName,
            };
            thread.Start();
            return thread;
        }
        catch (Exception ex) when (ex is OutOfMemoryException or ThreadStartException)
        {
            throw new ScadEvaluationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The geometry build needs a dedicated thread with a {BuildThreadStackSize / (1024 * 1024)} MiB stack on this small caller stack and it could not be started (not enough memory or address space)"),
                ex);
        }
    }

    /// <summary>
    ///     Converts the result of the whole model into the root node of the scene.
    /// </summary>
    /// <param name="geo">The result of the model.</param>
    /// <returns>The root node: the solid, a group of slabs for a flat model, or an empty group.</returns>
    private SceneNode ToSceneRoot(ScadGeo geo)
    {
        switch (geo.Kind)
        {
            case ScadGeoKind.Solid:
                return geo.Node!;
            case ScadGeoKind.Flat:
                return ScadExtrusions.MakeSlabs(geo, _context);
            default:
                return new GroupNode([]);
        }
    }
}
