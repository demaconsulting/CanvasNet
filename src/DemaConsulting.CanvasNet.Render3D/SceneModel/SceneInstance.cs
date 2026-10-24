namespace DemaConsulting.CanvasNet.Render3D.SceneModel;

/// <summary>
///     One flattened mesh occurrence in a <see cref="Scene"/>: the mesh, its composed world
///     transform, its resolved appearance and its draw priority.
/// </summary>
/// <remarks>
///     Produced by <see cref="Scene.EnumerateInstances"/> and consumed by the renderer so the
///     renderer never walks the node hierarchy itself.
/// </remarks>
/// <param name="Mesh">The mesh.</param>
/// <param name="World">The local-to-world transform (child first, then each ancestor).</param>
/// <param name="Appearance">The innermost appearance on the path from the root, or <see langword="null"/> when none is set.</param>
/// <param name="Order">The zero-based pre-order position of this instance among all instances; lower values have higher priority when coincident faces tie.</param>
/// <param name="Node">The <see cref="MeshNode"/> this instance came from (the same node is reported once per path when shared); used to attribute diagnostics.</param>
internal readonly record struct SceneInstance(Mesh Mesh, Matrix4x4D World, NodeAppearance? Appearance, int Order, MeshNode Node);
