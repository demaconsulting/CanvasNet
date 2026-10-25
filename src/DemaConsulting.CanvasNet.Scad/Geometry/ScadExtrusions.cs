using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     Builds <c>linear_extrude</c> and <c>rotate_extrude</c>, and the slabs of top-level 2D shapes, by distributing
///     the extrusion over a 2D shape tree.
/// </summary>
/// <remarks>
///     <para>
///         <b>Distribution.</b> A 2D result is a <see cref="ScadShape2D"/> tree, not one region (the package has no 2D
///         boolean kernel). To extrude it, the planar transforms are first applied to the points of every leaf
///         region (a leaf whose transformed points the polygon validator rejects is reported as
///         <c>scad-polygon-invalid</c> and left out), then each leaf is extruded on its own, and the tree is rebuilt
///         from the extruded leaves: the extrusion of a union is the union of the extrusions, of a difference the
///         difference, of an intersection the intersection, and a color becomes an appearance wrapper. This is exact
///         because an extrusion is a bijection of the profile domain, so it commutes with set operations; the
///         renderer's constructive solid geometry resolves the booleans per pixel.
///     </para>
///     <para>
///         <b>Shared parameters.</b> All leaves of one extrusion use the same slice count (linear) or segment count
///         (rotate), derived from the largest radius over all leaves, so the faceting of the operands of a boolean
///         agrees.
///     </para>
///     <para>
///         <b>Failure.</b> Argument problems are warnings (the extrusion is omitted). Constructs the geometry kernel
///         cannot represent are reported as unsupported features at the instance and omit the whole extrusion (a
///         partial extrusion would silently change the meaning of a boolean). A rejection by the Render3D
///         tessellators that the checks did not foresee is reported as <c>scad-geometry-rejected</c> with the
///         tessellator's message; nothing ever throws out of the build for geometry reasons. Only a resource
///         overrun, which is the user's budget, throws a <see cref="ScadEvaluationException"/>.
///     </para>
///     <para>
///         Every walk over a tree is iterative. The methods are stateless and thread-safe.
///     </para>
/// </remarks>
internal static partial class ScadExtrusions
{
    /// <summary>
    ///     The state of one extrusion while its leaves are being extruded.
    /// </summary>
    private sealed class Job
    {
        /// <summary>
        ///     Initializes a job.
        /// </summary>
        /// <param name="shape">The 2D shape to extrude.</param>
        /// <param name="regions">The transformed regions of the valid leaves.</param>
        public Job(ScadShape2D shape, Dictionary<ScadShape2D, PolygonRegion> regions)
        {
            Shape = shape;
            Regions = regions;
        }

        /// <summary>Gets the 2D shape to extrude.</summary>
        public ScadShape2D Shape { get; }

        /// <summary>Gets the transformed regions of the valid leaves, by leaf node (reference equality).</summary>
        public Dictionary<ScadShape2D, PolygonRegion> Regions { get; }
    }

    /// <summary>
    ///     Applies the planar transforms of a shape tree to its leaves.
    /// </summary>
    /// <param name="shape">The shape.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The job, or <see langword="null"/> when no leaf is valid.</returns>
    private static Job? Prepare(ScadShape2D shape, ScadGeometryContext context)
    {
        var regions = new Dictionary<ScadShape2D, PolygonRegion>(ReferenceEqualityComparer.Instance);
        foreach (var (leaf, matrix) in shape.EnumerateLeaves())
        {
            context.Tick();
            try
            {
                regions[leaf] = matrix == Matrix4x4D.Identity ? leaf.Region! : TransformRegion(leaf.Region!, matrix);
            }
            catch (ArgumentException ex)
            {
                context.Reporter.Unsupported(
                    ScadFeatureIds.PolygonInvalid,
                    $"A 2D shape was rejected after its transformations: {ex.Message}",
                    leaf.Location);
            }
        }

        return regions.Count == 0 ? null : new Job(shape, regions);
    }

    /// <summary>
    ///     Applies a planar matrix to the points of a region.
    /// </summary>
    /// <param name="region">The region.</param>
    /// <param name="matrix">The planar matrix (row-vector convention).</param>
    /// <returns>The transformed region (winding is normalized by the region).</returns>
    /// <exception cref="ArgumentException">Thrown when the transformed points do not form a valid region.</exception>
    private static PolygonRegion TransformRegion(PolygonRegion region, Matrix4x4D matrix)
    {
        var contours = new List<IReadOnlyList<Vector2D>>(region.Contours.Count);
        foreach (var contour in region.Contours)
        {
            var points = new Vector2D[contour.Count];
            for (var i = 0; i < points.Length; i++)
            {
                var p = contour[i];
                points[i] = new Vector2D(
                    (p.X * matrix.M11) + (p.Y * matrix.M21) + matrix.M41,
                    (p.X * matrix.M12) + (p.Y * matrix.M22) + matrix.M42);
            }

            contours.Add(points);
        }

        return new PolygonRegion(contours);
    }

    /// <summary>
    ///     Rebuilds the shape tree as a scene tree from the extruded leaves.
    /// </summary>
    /// <remarks>
    ///     An iterative post-order walk. A leaf that yields no node (an invalid leaf or one that failed) is an
    ///     empty operand: it vanishes from a union, empties an intersection, and empties a difference when it is the
    ///     first operand and vanishes otherwise.
    /// </remarks>
    /// <param name="shape">The shape.</param>
    /// <param name="leaf">Extrudes one leaf; returns <see langword="null"/> when it yields nothing.</param>
    /// <returns>The scene node, or <see langword="null"/> when the whole shape is empty.</returns>
    private static SceneNode? Distribute(ScadShape2D shape, Func<ScadShape2D, SceneNode?> leaf)
    {
        var stack = new List<(ScadShape2D Node, int Next, List<SceneNode?> Results)> { (shape, 0, new List<SceneNode?>()) };
        SceneNode? last = null;
        while (stack.Count > 0)
        {
            var (node, next, results) = stack[^1];
            if (node.Kind == ScadShape2DKind.Leaf)
            {
                last = leaf(node);
                stack.RemoveAt(stack.Count - 1);
            }
            else if (next < node.Children.Count)
            {
                stack[^1] = (node, next + 1, results);
                stack.Add((node.Children[next], 0, new List<SceneNode?>()));
                continue;
            }
            else
            {
                last = CombineOperands(node, results);
                stack.RemoveAt(stack.Count - 1);
            }

            if (stack.Count > 0)
            {
                stack[^1].Results.Add(last);
            }
        }

        return last;
    }

    /// <summary>
    ///     Combines the extruded operands of one interior node of the shape tree.
    /// </summary>
    /// <param name="node">The interior node.</param>
    /// <param name="results">The extruded children; <see langword="null"/> for an empty child.</param>
    /// <returns>The combined node, or <see langword="null"/> when it is empty.</returns>
    private static SceneNode? CombineOperands(ScadShape2D node, List<SceneNode?> results)
    {
        switch (node.Kind)
        {
            case ScadShape2DKind.Transform:
                // The transform was applied to the leaf points
                return results[0];
            case ScadShape2DKind.Color:
                return results[0] is null ? null : new GroupNode([results[0]!]) { Appearance = node.Appearance };
        }

        var present = results.Where(r => r is not null).Select(r => r!).ToList();
        switch (node.Operation)
        {
            case CsgOperation.Union:
                return present.Count switch
                {
                    0 => null,
                    1 => present[0],
                    _ => new GroupNode(present),
                };
            case CsgOperation.Difference:
                // Position matters: nothing minus anything is nothing
                if (results[0] is null)
                {
                    return null;
                }

                return present.Count == 1 ? present[0] : new CsgNode(CsgOperation.Difference, present);
            default:
                if (present.Count != results.Count)
                {
                    return null;
                }

                return present.Count == 1 ? present[0] : new CsgNode(CsgOperation.Intersection, present);
        }
    }

    /// <summary>
    ///     Turns the 2D result at the top of the model into slabs of the configured thickness.
    /// </summary>
    /// <param name="geo">The flat result of the model.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The scene root of the slabs (an empty group when nothing could be built).</returns>
    public static SceneNode MakeSlabs(ScadGeo geo, ScadGeometryContext context)
    {
        var job = Prepare(geo.Shape!, context);
        if (job is null)
        {
            return new GroupNode([]);
        }

        // Every slab must fit the budget; the location of a slab is the 2D shape that produced it
        foreach (var leaf in job.Shape.EnumerateLeaves().Select(l => l.Leaf))
        {
            if (job.Regions.TryGetValue(leaf, out var region))
            {
                context.ChargeTriangles(ScadPrimitives.SlabTrianglesPerVertex * region.VertexCount, leaf.Location);
            }
        }

        var root = Distribute(job.Shape, leaf =>
        {
            if (!job.Regions.TryGetValue(leaf, out var region))
            {
                return null;
            }

            context.Poll();
            try
            {
                return context.CreateMeshNode(Extruder.Linear(region, context.FlatShapeThickness), leaf.Location);
            }
            catch (ArgumentException ex)
            {
                context.Reporter.Unsupported(
                    ScadFeatureIds.GeometryRejected,
                    $"The 2D shape could not be made into a slab: {ex.Message}",
                    leaf.Location);
                return null;
            }
        });
        return root ?? new GroupNode([]);
    }
}
