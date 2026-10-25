using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     Remembers which source location produced each mesh node of a built scene.
/// </summary>
/// <remarks>
///     <para>
///         The renderer reports problems about a mesh operand by naming its <see cref="MeshNode"/>; the map turns
///         that node back into the location of the OpenSCAD instance, so a render-time report points at the line
///         and column of the module that caused it. Nodes are compared by reference. The map is filled while the
///         scene is built and only read afterwards. It is not thread-safe while it is being filled.
///     </para>
/// </remarks>
internal sealed class ScadSceneMap
{
    /// <summary>The recorded locations by node.</summary>
    private readonly Dictionary<MeshNode, ScadSourceLocation> _locations = new(ReferenceEqualityComparer.Instance);

    /// <summary>Gets the number of mesh nodes recorded.</summary>
    public int Count => _locations.Count;

    /// <summary>
    ///     Records the location that produced a mesh node.
    /// </summary>
    /// <param name="node">The mesh node.</param>
    /// <param name="location">The location of the producing instance.</param>
    public void Add(MeshNode node, ScadSourceLocation location) => _locations[node] = location;

    /// <summary>
    ///     Looks up the location that produced a mesh node.
    /// </summary>
    /// <param name="node">The mesh node, or <see langword="null"/>.</param>
    /// <returns>The recorded location, or <c>default</c> (unknown) when the node is <see langword="null"/> or was not recorded.</returns>
    public ScadSourceLocation Find(MeshNode? node) =>
        node is not null && _locations.TryGetValue(node, out var location) ? location : default;
}
