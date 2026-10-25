// cspell:ignore polyhedron Newell

using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <content>
///     The <c>polyhedron</c> builder: argument parsing, winding reversal and face triangulation.
/// </content>
internal static partial class ScadPrimitives
{
    /// <summary>The parameter names of <c>polyhedron</c> in positional order.</summary>
    private static readonly string[] PolyhedronParameters = ["points", "faces", "convexity", "triangles"];

    /// <summary>The relative tolerance below which a turn of a projected face is treated as collinear.</summary>
    private const double CollinearTolerance = 1e-12;

    /// <summary>
    ///     Builds a <c>polyhedron(points, faces, convexity, triangles)</c>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The language lists each face clockwise when seen from outside; Render3D wants counter-clockwise, so
    ///         every face is reversed. A face with more than three vertices is triangulated here: a strictly convex
    ///         face is fanned from its first vertex, any other planar-ish face is projected onto its best-fit
    ///         (Newell) plane and ear-clipped, and a face that cannot be triangulated reports
    ///         <see cref="ScadFeatureIds.PolyhedronFaceInvalid"/> and omits the whole polyhedron. The legacy
    ///         <c>triangles</c> parameter is used only when <c>faces</c> is absent.
    ///     </para>
    ///     <para>
    ///         The assembled mesh is checked: when it is closed but has a negative volume the faces were evidently
    ///         listed counter-clockwise, so the mesh is reversed and a warning says so; an open mesh is kept as
    ///         given (the renderer shows it correctly, and a CSG operand check reports it later).
    ///     </para>
    /// </remarks>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <returns>A solid, or <see cref="ScadGeo.Empty"/> when the arguments are invalid.</returns>
    public static ScadGeo Polyhedron(ScadInstance node, ScadGeometryContext context)
    {
        var args = Begin(node, PolyhedronParameters, context);
        if (!args.TryGet("points", out var pointsValue))
        {
            return Invalid(node, context, "points is required");
        }

        var points = ReadPoints3D(pointsValue);
        if (points is null)
        {
            return Invalid(node, context, "points must be a list of [x, y, z] triples of finite numbers");
        }

        // The faces come from 'faces', or from the legacy 'triangles' when 'faces' is absent
        if (!args.TryGet("faces", out var facesValue) && !args.TryGet("triangles", out facesValue))
        {
            return Invalid(node, context, "faces is required");
        }

        var faces = ReadFaces(facesValue, points.Length);
        if (faces is null)
        {
            return Invalid(node, context, "faces must be a non-empty list of faces, each at least three distinct point indices in range");
        }

        // Charge the triangles up front: a face of k vertices gives k - 2 triangles
        long estimate = 0;
        foreach (var face in faces)
        {
            estimate += face.Length - 2;
        }

        context.ChargeTriangles(estimate, node.Location);
        context.Poll();
        var triangles = TriangulateFaces(points, faces);
        if (triangles is null)
        {
            context.Reporter.Unsupported(
                ScadFeatureIds.PolyhedronFaceInvalid,
                "polyhedron: a face cannot be triangulated (degenerate or self-intersecting); the polyhedron is omitted",
                node.Location);
            return ScadGeo.Empty;
        }

        Mesh mesh;
        try
        {
            mesh = PrimitiveTessellator.Polyhedron(points, triangles);
        }
        catch (ArgumentException ex)
        {
            Rejected(node, context, ex);
            return ScadGeo.Empty;
        }

        // A closed mesh with negative volume was listed counter-clockwise from outside: turn it right way round
        if (mesh.IsClosed && mesh.SignedVolume < 0.0)
        {
            mesh = mesh.Reverse();
            context.Reporter.Warn("polyhedron: the faces appear to be listed counter-clockwise when seen from outside; the winding was reversed", node.Location);
        }

        return ScadGeo.FromSolid(context.CreateMeshNode(mesh, node.Location));
    }

    /// <summary>
    ///     Reads a list of 3D points.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The points, or <see langword="null"/> when the value is not a list of [x, y, z] triples of finite numbers.</returns>
    private static Vector3D[]? ReadPoints3D(ScadValue value)
    {
        if (value.Kind != ScadValueKind.Vector)
        {
            return null;
        }

        var list = value.AsVector;
        var points = new Vector3D[list.Count];
        for (var i = 0; i < points.Length; i++)
        {
            if (!ScadArguments.TryNumbers(list[i], 3, out var triple))
            {
                return null;
            }

            points[i] = new Vector3D(triple![0], triple[1], triple[2]);
        }

        return points;
    }

    /// <summary>
    ///     Reads the face list, reversing every face (clockwise from outside becomes counter-clockwise).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="pointCount">The number of points.</param>
    /// <returns>The reversed faces, or <see langword="null"/> when the list is empty or a face is invalid.</returns>
    private static int[][]? ReadFaces(ScadValue value, int pointCount)
    {
        if (value.Kind != ScadValueKind.Vector || value.AsVector.Count == 0)
        {
            return null;
        }

        var list = value.AsVector;
        var faces = new int[list.Count][];
        for (var i = 0; i < faces.Length; i++)
        {
            var face = ReadIndices(list[i], pointCount);
            if (face is null || face.Length < 3 || face.Distinct().Count() != face.Length)
            {
                return null;
            }

            Array.Reverse(face);
            faces[i] = face;
        }

        return faces;
    }

    /// <summary>
    ///     Triangulates every face.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <param name="faces">The counter-clockwise faces.</param>
    /// <returns>The triangles as three-index faces, or <see langword="null"/> when a face cannot be triangulated.</returns>
    private static List<IReadOnlyList<int>>? TriangulateFaces(Vector3D[] points, int[][] faces)
    {
        var triangles = new List<IReadOnlyList<int>>(faces.Length);
        foreach (var face in faces)
        {
            if (face.Length == 3)
            {
                triangles.Add(face);
            }
            else if (!TriangulateFace(points, face, triangles))
            {
                return null;
            }
        }

        return triangles;
    }

    /// <summary>
    ///     Triangulates one face of four or more vertices.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <param name="face">The counter-clockwise face.</param>
    /// <param name="triangles">The list that receives the triangles.</param>
    /// <returns><see langword="false"/> when the face is degenerate or cannot be triangulated.</returns>
    private static bool TriangulateFace(Vector3D[] points, int[] face, List<IReadOnlyList<int>> triangles)
    {
        // Project onto the best-fit plane (Newell normal) with a basis whose (u, v) turns counter-clockwise about it
        var normal = NewellNormal(points, face);
        var length = normal.Length;
        if (!double.IsFinite(length) || length <= 0.0)
        {
            return false;
        }

        var unit = normal / length;
        var axis = SmallestAxis(unit);
        var u = Vector3D.Cross(axis, unit).Normalize();
        var v = Vector3D.Cross(unit, u);
        var origin = points[face[0]];
        var projected = new Vector2D[face.Length];
        for (var i = 0; i < projected.Length; i++)
        {
            var d = points[face[i]] - origin;
            projected[i] = new Vector2D(Vector3D.Dot(d, u), Vector3D.Dot(d, v));
        }

        if (IsStrictlyConvex(projected))
        {
            // The fast path: a fan from the first vertex
            for (var i = 1; i < face.Length - 1; i++)
            {
                triangles.Add([face[0], face[i], face[i + 1]]);
            }

            return true;
        }

        return EarClipFace(face, projected, triangles);
    }

    /// <summary>
    ///     Picks the coordinate axis that is least aligned with a direction (the best axis to cross it with).
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <returns>The unit vector of the axis along which <paramref name="direction"/> has its smallest absolute component.</returns>
    private static Vector3D SmallestAxis(Vector3D direction)
    {
        var x = Math.Abs(direction.X);
        var y = Math.Abs(direction.Y);
        var z = Math.Abs(direction.Z);
        if (x <= y && x <= z)
        {
            return Vector3D.UnitX;
        }

        return y <= z ? Vector3D.UnitY : Vector3D.UnitZ;
    }

    /// <summary>
    ///     Computes the (unnormalized) Newell normal of a face, whose direction is the face's outward direction
    ///     when the face is counter-clockwise and whose length is twice the face's area.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <param name="face">The face.</param>
    /// <returns>The normal.</returns>
    private static Vector3D NewellNormal(Vector3D[] points, int[] face)
    {
        double x = 0.0, y = 0.0, z = 0.0;
        for (var i = 0; i < face.Length; i++)
        {
            var current = points[face[i]];
            var next = points[face[(i + 1) % face.Length]];
            x += (current.Y - next.Y) * (current.Z + next.Z);
            y += (current.Z - next.Z) * (current.X + next.X);
            z += (current.X - next.X) * (current.Y + next.Y);
        }

        return new Vector3D(x, y, z);
    }

    /// <summary>
    ///     Determines whether a counter-clockwise polygon turns left at every vertex by a clear margin.
    /// </summary>
    /// <param name="polygon">The projected vertices.</param>
    /// <returns><see langword="true"/> when every turn is strictly counter-clockwise (so a fan is valid).</returns>
    private static bool IsStrictlyConvex(Vector2D[] polygon)
    {
        var scale = 0.0;
        foreach (var p in polygon)
        {
            scale = Math.Max(scale, Math.Max(Math.Abs(p.X), Math.Abs(p.Y)));
        }

        var threshold = CollinearTolerance * scale * scale;
        for (var i = 0; i < polygon.Length; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Length];
            var c = polygon[(i + 2) % polygon.Length];
            var turn = ((b.X - a.X) * (c.Y - b.Y)) - ((b.Y - a.Y) * (c.X - b.X));
            if (!(turn > threshold))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Triangulates a non-convex projected face with the Render3D polygon triangulator and maps the corners
    ///     back to point indices.
    /// </summary>
    /// <param name="face">The face's point indices.</param>
    /// <param name="projected">The projected vertices, parallel to <paramref name="face"/>.</param>
    /// <param name="triangles">The list that receives the triangles.</param>
    /// <returns><see langword="false"/> when the projected polygon is invalid (self-touching, crossing or without area).</returns>
    private static bool EarClipFace(int[] face, Vector2D[] projected, List<IReadOnlyList<int>> triangles)
    {
        try
        {
            var region = new PolygonRegion([projected]);

            // The region keeps the exact coordinates, so each region vertex maps back to its face vertex by value
            var lookup = new Dictionary<Vector2D, int>(face.Length);
            for (var i = 0; i < face.Length; i++)
            {
                lookup[projected[i]] = face[i];
            }

            var order = PolygonTriangulator.Triangulate(region);
            for (var t = 0; t < order.Count; t += 3)
            {
                triangles.Add([
                    lookup[region.Vertices[order[t]]],
                    lookup[region.Vertices[order[t + 1]]],
                    lookup[region.Vertices[order[t + 2]]],
                ]);
            }

            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
