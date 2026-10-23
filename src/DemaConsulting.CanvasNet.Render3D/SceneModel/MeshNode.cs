namespace DemaConsulting.CanvasNet.Render3D.SceneModel;

/// <summary>
///     A leaf scene node that places a <see cref="SceneModel.Mesh"/> in the scene.
/// </summary>
/// <remarks>
///     The mesh is immutable and may be shared by several <see cref="MeshNode"/> instances (each
///     with its own <see cref="SceneNode.Transform"/>), which makes repeated parts cheap.
/// </remarks>
public sealed class MeshNode : SceneNode
{
    /// <summary>Creates a node for <paramref name="mesh"/>.</summary>
    /// <param name="mesh">The mesh to place; must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    public MeshNode(Mesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        Mesh = mesh;
    }

    /// <summary>Gets the mesh placed by this node.</summary>
    public Mesh Mesh { get; }
}
