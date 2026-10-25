// cspell:ignore tessellators
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
    /// <summary>The largest triangle estimate the planning arithmetic hands to the budget check (keeps the arithmetic far from overflow).</summary>
    private const double EstimateCeiling = 1e18;

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

        /// <summary>
        ///     Gets the number of triangles charged for each leaf (by leaf node, reference equality): the one value
        ///     that is charged, settled and refunded, so the three always agree.
        /// </summary>
        public Dictionary<ScadShape2D, long> Charges { get; } = new(ReferenceEqualityComparer.Instance);

        /// <summary>Gets or sets a value indicating whether a leaf was rejected, so the whole extrusion is omitted.</summary>
        public bool Failed { get; set; }

        /// <summary>Gets or sets the union of the bounds of the meshes created so far.</summary>
        public BoundingBox3D Bounds { get; set; } = BoundingBox3D.Empty;
    }

    /// <summary>
    ///     Gets the 2D shape under an extrusion from the results of its children, warning about 3D children.
    /// </summary>
    /// <param name="node">The extrusion instance.</param>
    /// <param name="results">The results of its children.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The union of the 2D children, or <see cref="ScadGeo.Empty"/>.</returns>
    private static ScadGeo CollectShape(ScadInstance node, List<ScadGeo> results, ScadGeometryContext context)
    {
        if (results.Any(r => r.Kind == ScadGeoKind.Solid))
        {
            context.Reporter.Warn($"{node.Name}: only 2D children can be extruded; the 3D children are ignored", node.Location);
        }

        return ScadBooleans.Union(node, results.Where(r => r.Kind == ScadGeoKind.Flat).ToList(), context);
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
    ///     Estimates a triangle count in double precision and turns it into a charge that cannot overflow.
    /// </summary>
    /// <param name="estimate">The estimate.</param>
    /// <returns>The estimate as a <see cref="long"/>, saturated far below the maximum.</returns>
    private static long Saturate(double estimate) => (long)Math.Min(estimate, EstimateCeiling);

    /// <summary>
    ///     Counts the triangles of one cap of a region: the triangulation of the region.
    /// </summary>
    /// <remarks>
    ///     The triangulator adds no Steiner point and never drops a vertex, so a region with <c>V</c> vertices,
    ///     <c>H</c> holes (clockwise contours) and <c>P</c> solid contours (counter-clockwise outer boundaries and
    ///     islands) has exactly <c>V + 2H - 2P</c> triangles. Contours are normalized by the region (solid
    ///     counter-clockwise, hole clockwise), so the sign of a contour's area tells them apart; the area is taken
    ///     relative to the first vertex to keep it accurate far from the origin.
    /// </remarks>
    /// <param name="region">The region.</param>
    /// <returns>The number of triangles of one cap.</returns>
    private static double CapTriangles(PolygonRegion region)
    {
        double solids = 0.0;
        double holes = 0.0;
        foreach (var contour in region.Contours)
        {
            var origin = contour[0];
            var twiceArea = 0.0;
            for (var i = 1; i < contour.Count - 1; i++)
            {
                var a = contour[i];
                var b = contour[i + 1];
                twiceArea += ((a.X - origin.X) * (b.Y - origin.Y)) - ((a.Y - origin.Y) * (b.X - origin.X));
            }

            if (twiceArea >= 0.0)
            {
                solids++;
            }
            else
            {
                holes++;
            }
        }

        return region.VertexCount + (2.0 * holes) - (2.0 * solids);
    }

    /// <summary>
    ///     Counts the triangles of the slab of a region (both caps and one wall slice).
    /// </summary>
    /// <param name="region">The region.</param>
    /// <returns>The number of triangles.</returns>
    private static long SlabTriangles(PolygonRegion region) => Saturate((2.0 * region.VertexCount) + (2.0 * CapTriangles(region)));

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
    ///     Extrudes every leaf with one function and assembles the solid.
    /// </summary>
    /// <remarks>
    ///     The exact triangle count of every leaf is charged before the first mesh is built, so an extrusion over the
    ///     budget fails without allocating; each leaf's charge is reconciled with its mesh when it is created and
    ///     refunded when the leaf is not built. The charge of a leaf is computed once and is the very value that is
    ///     settled or refunded, and an estimate too large to represent (a saturated leaf, or a sum reaching the
    ///     ceiling) is rejected as over the limit before anything is charged, so the counter stays exact and never
    ///     goes negative.
    /// </remarks>
    /// <param name="job">The job.</param>
    /// <param name="context">The build context.</param>
    /// <param name="node">The instance that owns the extrusion (for reports).</param>
    /// <param name="approximate">Whether the shape contains a difference or intersection.</param>
    /// <param name="predict">Gives the exact number of triangles the extrusion of a region has.</param>
    /// <param name="extrude">Extrudes one region into a mesh; may throw <see cref="ArgumentException"/>.</param>
    /// <returns>The solid, or <see cref="ScadGeo.Empty"/> when nothing was produced or a leaf was rejected.</returns>
    /// <exception cref="ScadEvaluationException">Thrown when the triangle budget would be exceeded.</exception>
    private static ScadGeo Extrude(
        Job job,
        ScadGeometryContext context,
        ScadInstance node,
        bool approximate,
        Func<PolygonRegion, long> predict,
        Func<PolygonRegion, Mesh> extrude)
    {
        var total = 0L;
        foreach (var leaf in job.Shape.EnumerateLeaves().Select(l => l.Leaf))
        {
            if (!job.Regions.TryGetValue(leaf, out var region))
            {
                continue;
            }

            // A leaf whose count is saturated, or a sum that reaches the ceiling, cannot be built whatever the
            // budget: reject it before anything is allocated, so every charge below is exact and refundable
            var charge = predict(region);
            if (charge >= EstimateCeiling || total > EstimateCeiling - charge)
            {
                context.FailTriangleLimit(node.Location);
            }

            job.Charges[leaf] = charge;
            total += charge;
        }

        context.ChargeTriangles(total, node.Location);
        var root = Distribute(job.Shape, leaf => ExtrudeLeaf(job, context, node, leaf, extrude));
        if (job.Failed || root is null)
        {
            return ScadGeo.Empty;
        }

        return ScadGeo.FromSolid(root, job.Bounds, true, approximate);
    }

    /// <summary>
    ///     Extrudes one leaf, mapping a tessellator rejection to the catch-all report.
    /// </summary>
    /// <param name="job">The job.</param>
    /// <param name="context">The build context.</param>
    /// <param name="node">The instance that owns the extrusion.</param>
    /// <param name="leaf">The leaf.</param>
    /// <param name="extrude">Extrudes one region into a mesh.</param>
    /// <returns>The mesh node, or <see langword="null"/> when the leaf is invalid or was rejected.</returns>
    private static SceneNode? ExtrudeLeaf(
        Job job,
        ScadGeometryContext context,
        ScadInstance node,
        ScadShape2D leaf,
        Func<PolygonRegion, Mesh> extrude)
    {
        if (!job.Regions.TryGetValue(leaf, out var region))
        {
            return null;
        }

        // The charge of a leaf that is not built is returned: exactly the amount that was charged for it
        var charged = job.Charges[leaf];
        if (job.Failed)
        {
            context.RefundTriangles(charged);
            return null;
        }

        context.Poll();
        try
        {
            var mesh = extrude(region);
            context.SettleTriangles(charged, mesh.TriangleCount, node.Location);
            job.Bounds = job.Bounds.Union(mesh.Bounds);
            return mesh.TriangleCount == 0 ? null : context.CreateMeshNode(mesh, node.Location);
        }
        catch (ArgumentException ex)
        {
            job.Failed = true;
            context.RefundTriangles(charged);
            context.Reporter.Unsupported(
                ScadFeatureIds.GeometryRejected,
                $"{node.Name}: the geometry was rejected: {ex.Message}",
                node.Location);
            return null;
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

        // Every slab must fit the budget, with its exact triangle count; the location of a slab is the 2D shape that produced it
        foreach (var leaf in job.Shape.EnumerateLeaves().Select(l => l.Leaf))
        {
            if (job.Regions.TryGetValue(leaf, out var region))
            {
                var charge = SlabTriangles(region);
                if (charge >= EstimateCeiling)
                {
                    context.FailTriangleLimit(leaf.Location);
                }

                job.Charges[leaf] = charge;
                context.ChargeTriangles(charge, leaf.Location);
            }
        }

        var root = Distribute(job.Shape, leaf =>
        {
            if (!job.Regions.ContainsKey(leaf))
            {
                return null;
            }

            var charged = job.Charges[leaf];
            var region = job.Regions[leaf];
            context.Poll();
            try
            {
                var mesh = Extruder.Linear(region, context.FlatShapeThickness);
                context.SettleTriangles(charged, mesh.TriangleCount, leaf.Location);
                return context.CreateMeshNode(mesh, leaf.Location);
            }
            catch (ArgumentException ex)
            {
                context.RefundTriangles(charged);
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
