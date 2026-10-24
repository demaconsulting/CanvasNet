namespace DemaConsulting.CanvasNet.Render3D.SceneModel;

/// <summary>
///     The topology defects <see cref="MeshTopology.Analyze(ReadOnlySpan{int})"/> can find in a triangle index list;
///     a combination of flags, or <see cref="None"/> for a closed, consistently oriented surface.
/// </summary>
[Flags]
internal enum MeshTopologyDefects
{
    /// <summary>No defect: every edge is shared by exactly two triangles with opposite directions.</summary>
    None = 0,

    /// <summary>At least one edge belongs to a single triangle, so the surface is open.</summary>
    BoundaryEdge = 1,

    /// <summary>At least one edge is shared by three or more triangles.</summary>
    NonManifoldEdge = 2,

    /// <summary>At least one edge is shared by two triangles that traverse it in the same direction (inconsistent winding).</summary>
    InconsistentWinding = 4,

    /// <summary>At least one triangle repeats a vertex index and so has no area.</summary>
    DegenerateTriangle = 8,
}
