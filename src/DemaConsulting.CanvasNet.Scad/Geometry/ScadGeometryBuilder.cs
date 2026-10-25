// cspell:ignore multmatrix minkowski
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
///         recurses. The build therefore behaves identically on any caller stack, including a 64 KiB one.
///     </para>
///     <para>
///         <b>Dispatch.</b> A group (a user module, <c>for</c>, a modifier wrapper) and the <c>union</c> and
///         <c>render</c> modules combine their children as an implicit union; a recognized primitive builds a
///         leaf; <c>difference</c>, <c>intersection</c>, the transforms (<c>translate</c>, <c>rotate</c>,
///         <c>scale</c>, <c>mirror</c>, <c>multmatrix</c>, <c>resize</c>), <c>color</c> and the extrusions
///         (<c>linear_extrude</c>, <c>rotate_extrude</c>) build from the results of their children (see the
///         container table in <c>ScadGeometryBuilder.Unsupported.cs</c>); every other recognized module
///         (<c>text</c>, <c>offset</c>, <c>projection</c>, <c>surface</c>, <c>import</c>, <c>roof</c>, <c>hull</c>,
///         <c>minkowski</c>) is reported as an unsupported feature (see <see cref="ScadFeatureIds"/>) and yields
///         nothing, so nothing is silently skipped. Modifiers: <c>!</c> replaces the model with the outermost
///         marked subtrees (without the transforms and colors of their ancestors), <c>#</c> renders normally and is
///         reported as degraded, <c>%</c> omits the subtree and is reported.
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
    /// <summary>The services of this build.</summary>
    private readonly ScadGeometryContext _context;

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

        var context = new ScadGeometryContext(settings, new ScadReporter(sink, settings.ThrowOnUnsupportedFeature));
        var builder = new ScadGeometryBuilder(context);

        // A model that contains '!' is just the marked subtrees; otherwise it is the union of the top-level instances
        var showOnly = FindShowOnlyRoots(roots);
        var geo = builder.BuildForest(showOnly.Count > 0 ? showOnly : roots);
        var root = builder.ToSceneRoot(geo);
        return new ScadBuiltScene(new Scene(root), context.Map);
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
