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
/// <param name="TrianglesCharged">The triangles charged against the budget, which equals the triangles of all the meshes created.</param>
internal sealed record ScadBuiltScene(Scene Scene, ScadSceneMap Map, long TrianglesCharged);

/// <summary>
///     The services every geometry builder uses: reporting, the triangle budget, cancellation and the node map.
/// </summary>
/// <remarks>
///     <para>
///         One context serves one build on one thread. The triangle budget is charged with the exact triangle
///         count of a mesh, computed from its parameters (fragments, profile contours and holes, slices)
///         <i>before</i> the mesh is allocated, so a hostile <c>$fn</c> or loop fails with a
///         <see cref="ScadEvaluationException"/> that names the offending instance instead of exhausting memory.
///         When the mesh is created, <see cref="SettleTriangles"/> reconciles the charge with the actual triangle
///         count (they differ only when the tessellator drops a triangle that is degenerate in floating point), so
///         the total charged always equals the triangles of the meshes created, and a mesh that is rejected is
///         refunded with <see cref="RefundTriangles"/>.
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
    ///     Gets the number of hull computations (<c>ConvexHull.Hull3D</c> calls) that the hull and Minkowski builders
    ///     started with this context, a diagnostic counter that lets tests prove that a computation was rejected by the
    ///     triangle budget before it was begun.
    /// </summary>
    public long HullsStarted { get; private set; }

    /// <summary>
    ///     Gets the number of pairwise Minkowski sums that the builders started with this context (a diagnostic counter).
    /// </summary>
    public long SumsStarted { get; private set; }

    /// <summary>Counts a hull computation that is about to start.</summary>
    public void NoteHullStarted() => HullsStarted++;

    /// <summary>Counts a pairwise Minkowski sum that is about to start.</summary>
    public void NoteSumStarted() => SumsStarted++;

    /// <summary>
    ///     Gets the number of scans that counted the distinct vertices of a Minkowski member (a diagnostic counter that
    ///     lets tests prove that each member is scanned once, not once per pair).
    /// </summary>
    public long DistinctScans { get; private set; }

    /// <summary>Counts a scan of the distinct vertices of one mesh.</summary>
    public void NoteDistinctScan() => DistinctScans++;

    /// <summary>Gets the triangle budget of the model (<see cref="ScadLimits.MaxTriangles"/>).</summary>
    public long MaxTriangles => _settings.MaxTriangles;

    /// <summary>Gets the token polled while building, for the long-running Render3D operations (hull, Minkowski sum).</summary>
    public CancellationToken Cancellation => _settings.Cancellation;

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
    /// <param name="estimate">The estimate of the triangles (an upper bound or the exact count).</param>
    /// <param name="location">The location of the instance that would create them.</param>
    /// <exception cref="ScadEvaluationException">Thrown when the estimate does not fit.</exception>
    public void EnsureTriangles(long estimate, ScadSourceLocation location)
    {
        if (estimate > _settings.MaxTriangles - _triangles)
        {
            FailTriangleLimit(location);
        }
    }

    /// <summary>
    ///     Fails the build with the triangle-limit error, for an estimate that is over the budget or too large to
    ///     be represented (and so can never be built).
    /// </summary>
    /// <param name="location">The location of the instance that would create the triangles.</param>
    /// <exception cref="ScadEvaluationException">Always thrown.</exception>
    public void FailTriangleLimit(ScadSourceLocation location) =>
        throw new ScadEvaluationException(
            string.Create(CultureInfo.InvariantCulture, $"Triangle limit exceeded (limit {_settings.MaxTriangles})"),
            location);

    /// <summary>
    ///     Charges the predicted number of triangles of a mesh against the budget, failing before anything is allocated.
    /// </summary>
    /// <param name="estimate">The predicted (exact) number of triangles.</param>
    /// <param name="location">The location of the instance that creates them.</param>
    /// <exception cref="ScadEvaluationException">Thrown when the budget would be exceeded.</exception>
    public void ChargeTriangles(long estimate, ScadSourceLocation location)
    {
        EnsureTriangles(estimate, location);
        _triangles += estimate;
    }

    /// <summary>
    ///     Reconciles a charge with the number of triangles the mesh actually has.
    /// </summary>
    /// <param name="charged">The number charged for the mesh before it was created.</param>
    /// <param name="actual">The triangle count of the created mesh.</param>
    /// <param name="location">The location of the instance that created the mesh.</param>
    /// <exception cref="ScadEvaluationException">Thrown when the mesh has more triangles than were charged and the surplus does not fit the budget.</exception>
    public void SettleTriangles(long charged, long actual, ScadSourceLocation location)
    {
        if (actual > charged)
        {
            ChargeTriangles(actual - charged, location);
        }
        else
        {
            RefundTriangles(charged - actual);
        }
    }

    /// <summary>
    ///     Returns the charge of a mesh that was not created (it was rejected or never attempted).
    /// </summary>
    /// <remarks>
    ///     Every caller refunds exactly what it charged, so the counter never goes below zero; the floor only
    ///     guarantees that an accounting mistake could never turn into a negative counter that lets later geometry
    ///     bypass the limit.
    /// </remarks>
    /// <param name="charged">The number charged for the mesh.</param>
    public void RefundTriangles(long charged) => _triangles = Math.Max(0L, _triangles - Math.Max(0L, charged));

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
