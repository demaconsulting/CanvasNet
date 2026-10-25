using System.Globalization;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     The inputs of one geometry build, already validated and converted from the public options.
/// </summary>
/// <param name="MaxTriangles">The triangle budget of the model (<see cref="ScadLimits.MaxTriangles"/>).</param>
/// <param name="FlatShapeThickness">The slab thickness of a top-level 2D shape.</param>
/// <param name="ThrowOnUnsupportedFeature">Whether the first unsupported feature throws.</param>
/// <param name="Cancellation">The token polled while building.</param>
internal sealed record ScadGeometrySettings(
    long MaxTriangles,
    double FlatShapeThickness,
    bool ThrowOnUnsupportedFeature,
    CancellationToken Cancellation);

/// <summary>
///     The outcome of building a model: the scene and the map from its mesh nodes back to source locations.
/// </summary>
/// <param name="Scene">The scene; an empty group when the model produced no geometry.</param>
/// <param name="Map">The location map of the scene's mesh nodes.</param>
internal sealed record ScadBuiltScene(Scene Scene, ScadSceneMap Map);

/// <summary>
///     The services every geometry builder uses: reporting, the triangle budget, cancellation and the node map.
/// </summary>
/// <remarks>
///     <para>
///         One context serves one build on one thread. The triangle budget is charged with an upper-bound estimate
///         <i>before</i> a mesh is allocated, so a hostile <c>$fn</c> or loop fails with a
///         <see cref="ScadEvaluationException"/> that names the offending instance instead of exhausting memory.
///         Cancellation is polled cheaply on a fixed schedule and explicitly before each heavy tessellation.
///     </para>
/// </remarks>
internal sealed class ScadGeometryContext
{
    /// <summary>The number of instances between two cancellation polls.</summary>
    private const int PollInterval = 256;

    /// <summary>The settings of the build.</summary>
    private readonly ScadGeometrySettings _settings;

    /// <summary>The number of instances visited since the last poll.</summary>
    private int _sincePoll;

    /// <summary>The number of triangles charged so far.</summary>
    private long _triangles;

    /// <summary>
    ///     Initializes a context.
    /// </summary>
    /// <param name="settings">The settings of the build.</param>
    /// <param name="reporter">The reporter that receives warnings and unsupported features.</param>
    public ScadGeometryContext(ScadGeometrySettings settings, ScadReporter reporter)
    {
        _settings = settings;
        Reporter = reporter;
        Map = new ScadSceneMap();
    }

    /// <summary>Gets the reporter.</summary>
    public ScadReporter Reporter { get; }

    /// <summary>Gets the node-to-location map filled by <see cref="CreateMeshNode"/>.</summary>
    public ScadSceneMap Map { get; }

    /// <summary>Gets the slab thickness of a top-level 2D shape.</summary>
    public double FlatShapeThickness => _settings.FlatShapeThickness;

    /// <summary>Gets the number of triangles charged so far.</summary>
    public long TrianglesCharged => _triangles;

    /// <summary>
    ///     Counts one visited instance and polls cancellation once every <see cref="PollInterval"/> instances.
    /// </summary>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled.</exception>
    public void Tick()
    {
        if (++_sincePoll >= PollInterval)
        {
            _sincePoll = 0;
            _settings.Cancellation.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    ///     Polls cancellation immediately (before a heavy tessellation).
    /// </summary>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled.</exception>
    public void Poll() => _settings.Cancellation.ThrowIfCancellationRequested();

    /// <summary>
    ///     Verifies that an estimated number of triangles fits in the remaining budget without charging it.
    /// </summary>
    /// <param name="estimate">The upper-bound estimate of the triangles.</param>
    /// <param name="location">The location of the instance that would create them.</param>
    /// <exception cref="ScadEvaluationException">Thrown when the estimate does not fit.</exception>
    public void EnsureTriangles(long estimate, ScadSourceLocation location)
    {
        if (estimate > _settings.MaxTriangles - _triangles)
        {
            throw new ScadEvaluationException(
                string.Create(CultureInfo.InvariantCulture, $"Triangle limit exceeded (limit {_settings.MaxTriangles})"),
                location);
        }
    }

    /// <summary>
    ///     Charges an estimated number of triangles against the budget, failing before anything is allocated.
    /// </summary>
    /// <param name="estimate">The upper-bound estimate of the triangles.</param>
    /// <param name="location">The location of the instance that creates them.</param>
    /// <exception cref="ScadEvaluationException">Thrown when the budget would be exceeded.</exception>
    public void ChargeTriangles(long estimate, ScadSourceLocation location)
    {
        EnsureTriangles(estimate, location);
        _triangles += estimate;
    }

    /// <summary>
    ///     Creates a mesh node and remembers the location that produced it.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="location">The location of the producing instance.</param>
    /// <returns>The node.</returns>
    public MeshNode CreateMeshNode(Mesh mesh, ScadSourceLocation location)
    {
        var node = new MeshNode(mesh);
        Map.Add(node, location);
        return node;
    }
}
