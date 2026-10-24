using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     Checks that the meshes of a scene rendered through the fragment-list CSG path are valid solids
///     and reports (or, in strict mode, throws) every problem it finds.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why.</b> The CSG resolver counts front-facing and back-facing crossings per leaf, which is
///         only meaningful for closed, consistently wound, outward-facing meshes. An open or inverted
///         mesh does not make the renderer fail (it still produces a deterministic image), but the image
///         is visibly wrong, so the renderer names the problem instead of hiding it.
///     </para>
///     <para>
///         <b>Scope.</b> The validator is called only for scenes that take the CSG path (at least one
///         difference or intersection with two or more operands). <em>Every</em> mesh instance of such a
///         scene is checked, not only the direct operands, because all leaves share the signed-counter
///         semantics. Union-only scenes are rendered by the z-buffer, never need closed meshes and are
///         never validated. Nothing is analyzed when there is neither a handler nor strict mode.
///     </para>
///     <para>
///         <b>Reports.</b> Instances are visited in scene pre-order; each distinct <see cref="MeshNode"/>
///         is reported once (a node shared by several parents or visited per path is analyzed once), and
///         its problems are reported in the fixed order open mesh, non-manifold edge, inconsistent winding,
///         degenerate triangle, inverted mesh. Reports are delivered on the calling thread before any
///         tile work starts. An inverted mesh is a defect-free closed mesh whose signed volume is
///         negative; a mirroring transform does not change that (the renderer reverses the winding of a
///         mirrored instance, which restores the orientation the mesh data describes), so inversion is a
///         property of the mesh itself.
///     </para>
///     <para>
///         <b>Transform-dependent checks.</b> The checks above depend only on the mesh and are done once per
///         node, before any transform work. A valid mesh can still be destroyed by an instance's world
///         transform, so <see cref="ValidateTransformed"/> checks every instance (not every node) after the
///         vertices were transformed: a singular world transform and triangles that are geometrically
///         degenerate in view space (the renderer's own <see cref="MeshTopology.TryGetFaceNormal"/> criterion).
///         These reports follow all mesh-level reports, in scene pre-order.
///     </para>
///     <para>
///         Stateless and thread-safe.
///     </para>
/// </remarks>
internal static class CsgOperandValidator
{
    /// <summary>The feature token of a mesh with a boundary edge (an open surface).</summary>
    public const string OpenMeshFeature = "render3d-csg-open-mesh";

    /// <summary>The feature token of a mesh with an edge shared by three or more triangles.</summary>
    public const string NonManifoldFeature = "render3d-csg-non-manifold-edge";

    /// <summary>The feature token of a mesh whose neighboring triangles are wound in conflicting directions.</summary>
    public const string InconsistentWindingFeature = "render3d-csg-inconsistent-winding";

    /// <summary>The feature token of a mesh containing zero-area (repeated index) triangles.</summary>
    public const string DegenerateTriangleFeature = "render3d-csg-degenerate-triangle";

    /// <summary>The feature token of a closed mesh that is wound inside out.</summary>
    public const string InvertedMeshFeature = "render3d-csg-inverted-mesh";

    /// <summary>The feature token of a mesh instance whose composed world transform is singular (zero or non-finite determinant).</summary>
    public const string SingularTransformFeature = "render3d-csg-singular-transform";

    /// <summary>The feature token reported when a user-set near plane lies behind operand geometry.</summary>
    public const string NearPlaneClipsFeature = "render3d-csg-near-plane-clips-operands";

    /// <summary>
    ///     Gets a value indicating whether the options ask for any validation to be performed.
    /// </summary>
    /// <param name="options">The render options.</param>
    /// <returns><see langword="true"/> when a handler is set or strict mode is on.</returns>
    public static bool IsEnabled(SceneRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.ThrowOnUnsupportedFeature || options.UnsupportedFeatureHandler is not null;
    }

    /// <summary>
    ///     Validates every mesh instance of a CSG-path scene and delivers the reports.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="options">The render options supplying the handler and strict flag.</param>
    /// <param name="cancellationToken">Observed once per instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="SceneRenderUnsupportedFeatureException">Thrown for the first problem when <see cref="SceneRenderOptions.ThrowOnUnsupportedFeature"/> is set.</exception>
    /// <exception cref="OperationCanceledException">Thrown when cancellation was requested.</exception>
    public static void Validate(Scene scene, SceneRenderOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (!IsEnabled(options))
        {
            return;
        }

        var seen = new HashSet<MeshNode>(ReferenceEqualityComparer.Instance);
        foreach (var instance in scene.EnumerateInstances())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!seen.Add(instance.Node))
            {
                continue;
            }

            var mesh = instance.Mesh;
            var defects = mesh.OperandDefects;
            if (mesh.TriangleCount == 0)
            {
                // An empty mesh contributes nothing and has nothing to report
                continue;
            }

            if ((defects & MeshTopologyDefects.BoundaryEdge) != 0)
            {
                Report(options, OpenMeshFeature, "A mesh used in a constructive solid geometry scene is open (it has boundary edges); results near it are undefined.", instance.Node);
            }

            if ((defects & MeshTopologyDefects.NonManifoldEdge) != 0)
            {
                Report(options, NonManifoldFeature, "A mesh used in a constructive solid geometry scene has an edge shared by three or more triangles; results near it are undefined.", instance.Node);
            }

            if ((defects & MeshTopologyDefects.InconsistentWinding) != 0)
            {
                Report(options, InconsistentWindingFeature, "A mesh used in a constructive solid geometry scene has inconsistently wound triangles; results near it are undefined.", instance.Node);
            }

            if ((defects & MeshTopologyDefects.DegenerateTriangle) != 0)
            {
                Report(options, DegenerateTriangleFeature, "A mesh used in a constructive solid geometry scene has degenerate (repeated vertex or zero-area) triangles; they are ignored and may leave gaps.", instance.Node);
            }

            if (mesh.IsInverted)
            {
                Report(options, InvertedMeshFeature, "A mesh used in a constructive solid geometry scene is wound inside out (negative volume); its inside and outside are swapped.", instance.Node);
            }
        }
    }

    /// <summary>
    ///     Validates the transform-dependent properties of one mesh instance of a CSG-path scene, after
    ///     its vertices were transformed into view space, and delivers the reports.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The mesh-level checks of <see cref="Validate"/> are independent of the instance's transform
    ///         and run once per node. A transform can nevertheless destroy a valid closed mesh: a singular
    ///         world transform (determinant zero or non-finite, such as a zero scale) collapses every triangle,
    ///         and an extreme flattening or an overflowing transform collapses some. The renderer silently
    ///         drops such a triangle (<see cref="MeshTopology.TryGetFaceNormal"/> fails), which removes
    ///         crossings from the CSG result, so this check runs <em>per instance</em> (a node shared by a
    ///         valid and a singular instance is reported for the singular one only).
    ///     </para>
    ///     <para>
    ///         A singular transform is reported as <see cref="SingularTransformFeature"/> and nothing else for
    ///         that instance (all of its triangles are gone). Otherwise the transformed triangles are tested
    ///         with exactly the criterion the renderer uses, on the same double-precision view-space
    ///         vertices, and a failing triangle is reported once per instance as
    ///         <see cref="DegenerateTriangleFeature"/> unless the mesh itself already carries that defect
    ///         (already reported for the node). A mirroring transform needs no special handling: swapping
    ///         two indices does not change whether a triangle has a normal.
    ///     </para>
    /// </remarks>
    /// <param name="options">The render options supplying the handler and strict flag.</param>
    /// <param name="instance">The scene instance (mesh, composed world transform, node).</param>
    /// <param name="viewVertices">The instance's mesh vertices in the renderer's view space.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="viewVertices"/> is <see langword="null"/>.</exception>
    /// <exception cref="SceneRenderUnsupportedFeatureException">Thrown for the first problem when <see cref="SceneRenderOptions.ThrowOnUnsupportedFeature"/> is set.</exception>
    public static void ValidateTransformed(SceneRenderOptions options, SceneInstance instance, Vector3D[] viewVertices)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(viewVertices);
        var mesh = instance.Mesh;
        if (!IsEnabled(options) || mesh.TriangleCount == 0)
        {
            return;
        }

        var determinant = instance.World.Determinant;
        if (!double.IsFinite(determinant) || determinant == 0.0)
        {
            Report(options, SingularTransformFeature, "A mesh instance in a constructive solid geometry scene has a singular (zero-determinant or non-finite) world transform; it collapses to nothing and removes its crossings.", instance.Node);
            return;
        }

        if ((mesh.OperandDefects & MeshTopologyDefects.DegenerateTriangle) != 0)
        {
            // Already reported for the node; the transform cannot add a new kind of report
            return;
        }

        var indices = mesh.IndexSpan;
        for (var t = 0; t + 2 < indices.Length; t += 3)
        {
            if (!MeshTopology.TryGetFaceNormal(viewVertices[indices[t]], viewVertices[indices[t + 1]], viewVertices[indices[t + 2]], out _))
            {
                Report(options, DegenerateTriangleFeature, "A mesh instance in a constructive solid geometry scene has triangles that are degenerate after its world transform (extreme flattening or overflow); they are ignored and may leave gaps.", instance.Node);
                return;
            }
        }
    }

    /// <summary>
    ///     Reports that a user-supplied near plane lies in front of some operand geometry, so the
    ///     sample ray state of a CSG scene may be wrong where that geometry is clipped.
    /// </summary>
    /// <param name="options">The render options supplying the handler and strict flag.</param>
    /// <param name="nearPlane">The camera's explicit near distance, or <see langword="null"/> when it is derived.</param>
    /// <param name="hasVertices">Whether the scene has any vertex.</param>
    /// <param name="minDepth">The smallest view depth of any vertex.</param>
    /// <exception cref="SceneRenderUnsupportedFeatureException">Thrown when strict mode is on and the near plane clips geometry.</exception>
    public static void ValidateNearPlane(SceneRenderOptions options, double? nearPlane, bool hasVertices, double minDepth)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!IsEnabled(options) || nearPlane is not { } near || !hasVertices || minDepth >= near)
        {
            return;
        }

        Report(options, NearPlaneClipsFeature, "The camera's near plane lies in front of geometry of a constructive solid geometry scene; clipped operands make the result undefined.", null);
    }

    /// <summary>Delivers one report to the handler or throws it in strict mode.</summary>
    /// <param name="options">The render options.</param>
    /// <param name="feature">The feature token.</param>
    /// <param name="message">The message.</param>
    /// <param name="node">The mesh node the report concerns, or <see langword="null"/>.</param>
    private static void Report(SceneRenderOptions options, string feature, string message, MeshNode? node)
    {
        if (options.ThrowOnUnsupportedFeature)
        {
            throw new SceneRenderUnsupportedFeatureException(feature, message, node);
        }

        options.UnsupportedFeatureHandler?.Invoke(new SceneRenderUnsupportedFeature(feature, message, node));
    }
}
