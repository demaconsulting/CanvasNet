using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.Tessellation;

/// <summary>
///     Rotational extrusion (<see cref="Rotate"/>).
/// </summary>
public static partial class Extruder
{
    /// <summary>Smallest segment count of a full revolution (a closed ring of facets needs three).</summary>
    private const int MinimumFullRevolutionSegments = 3;

    /// <summary>
    ///     Revolves a region about the +Z axis: profile X is the radius, profile Y is the height.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The profile is rotated literally from azimuth 0 toward +<paramref name="angleDegrees"/>
    ///         (counter-clockwise seen from +Z). All profile vertices must lie on one side of the axis: every
    ///         <c>x &gt;= 0</c> or every <c>x &lt;= 0</c> (the negative side is supported by flipping the
    ///         winding, and its sweep is the literal rotation). A profile on both sides, or entirely on the axis,
    ///         is rejected; there is no tolerance, so <c>x == 0</c> exactly means a vertex on the axis.
    ///     </para>
    ///     <para>
    ///         Every profile vertex on the axis becomes exactly one mesh vertex, a profile edge with both ends on
    ///         the axis sweeps to nothing and is skipped, and an edge with one end on the axis yields one triangle
    ///         per segment, so the result is closed with no degenerate triangle. A full revolution (exactly 360)
    ///         wraps the last segment to the first ring by index (no duplicated seam and no end caps); a partial
    ///         sweep adds a start cap at azimuth 0 and an end cap at the final azimuth, both triangulated by
    ///         <see cref="PolygonTriangulator"/>. Facets are planar (parallel chords of concentric rings), so the
    ///         volume has the exact closed form <c>segments sin(delta) integral(x dA)</c> with
    ///         <c>delta = angle / segments</c>, which approaches the Pappus volume as the segment count grows.
    ///         A profile with a hole yields a hollow solid (outer shell outward, cavity shell inward). A profile
    ///         that touches the axis at one isolated vertex (not along an edge) would sweep two surfaces that meet
    ///         at that vertex, a pinched (non-manifold) vertex, so it is rejected; a vertex on the axis must be
    ///         an end of an edge lying on the axis (a pole).
    ///     </para>
    /// </remarks>
    /// <param name="region">The profile in the (X = radius, Y = height) plane.</param>
    /// <param name="angleDegrees">The sweep angle in degrees; finite, greater than 0 and at most 360 (exactly 360 is a full revolution); not so small that its radian value rounds to zero.</param>
    /// <param name="segments">
    ///     The number of angular steps over the sweep: at least 3 for a full revolution, at least 1 for a partial sweep.
    /// </param>
    /// <returns>The closed, outward-oriented mesh; empty for an empty region.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="region"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle is not in (0, 360], is so small that it converts to a zero radian sweep, or the segment count is too small.</exception>
    /// <exception cref="ArgumentException">Thrown when the profile crosses or lies entirely on the axis, touches the axis at an isolated vertex, a sweep step is too small to resolve at a profile vertex's radius, or the result would exceed the maximum mesh size.</exception>
    public static Mesh Rotate(PolygonRegion region, double angleDegrees, int segments)
    {
        // Validate everything before doing any work
        ArgumentNullException.ThrowIfNull(region);
        if (!double.IsFinite(angleDegrees) || angleDegrees <= 0.0 || angleDegrees > 360.0)
        {
            throw new ArgumentOutOfRangeException(nameof(angleDegrees), angleDegrees, "The angle must be greater than 0 and at most 360 degrees.");
        }

        // An angle so small that its radian value rounds to zero would weld the end ring onto the start ring
        if (angleDegrees * Math.PI / 180.0 == 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(angleDegrees), angleDegrees, "The angle is too small to represent as a non-zero sweep.");
        }

        var full = angleDegrees >= 360.0;
        if (segments < (full ? MinimumFullRevolutionSegments : 1))
        {
            throw new ArgumentOutOfRangeException(nameof(segments), segments, full ? "A full revolution needs at least 3 segments." : "At least 1 segment is required.");
        }

        if (region.IsEmpty)
        {
            return EmptyMesh();
        }

        var negative = CheckProfileSide(region);
        RejectIsolatedAxisContacts(region);

        // Upper bounds: V * (segments + 1) vertices; two wall triangles per edge and segment plus two caps
        var vertexCount = region.VertexCount;
        RequireMeshSize((long)vertexCount * (segments + 1L), (2L * vertexCount * segments) + (2L * vertexCount));

        var builder = new MeshBuilder();
        var rings = BuildRevolveRings(builder, region, angleDegrees, segments, full);
        AddRevolveWalls(builder, region, rings, full, negative);
        if (!full)
        {
            AddRevolveCaps(builder, region, rings, negative);
        }

        return builder.Build();
    }

    /// <summary>
    ///     Checks that the profile lies on one side of the axis and reports which.
    /// </summary>
    /// <param name="region">The profile.</param>
    /// <returns><see langword="true"/> when every vertex has <c>x &lt;= 0</c> (and at least one has <c>x &lt; 0</c>).</returns>
    /// <exception cref="ArgumentException">Thrown when the profile crosses the axis or lies entirely on it.</exception>
    private static bool CheckProfileSide(PolygonRegion region)
    {
        var right = false;
        var left = false;
        foreach (var point in region.VertexSpan)
        {
            right |= point.X > 0.0;
            left |= point.X < 0.0;
        }

        if (right && left)
        {
            throw new ArgumentException("The profile crosses the rotation axis; all vertices must have x >= 0 or all x <= 0.", nameof(region));
        }

        if (!right && !left)
        {
            throw new ArgumentException("The profile lies entirely on the rotation axis and sweeps no volume.", nameof(region));
        }

        return left;
    }

    /// <summary>
    ///     Rejects a profile vertex that touches the axis while both of its contour neighbors are off the axis.
    ///     Such a vertex is shared by every angular ring, so the two swept surfaces would meet only at that point
    ///     (a pinched, non-manifold vertex); a vertex at the end of an edge lying on the axis (a pole) is fine.
    /// </summary>
    /// <param name="region">The profile.</param>
    /// <exception cref="ArgumentException">Thrown when the profile touches the axis at an isolated vertex.</exception>
    private static void RejectIsolatedAxisContacts(PolygonRegion region)
    {
        var points = region.VertexSpan;
        foreach (var contour in region.ContourTable)
        {
            for (var i = 0; i < contour.Count; i++)
            {
                var current = contour.Start + i;
                var previous = contour.Start + ((i + contour.Count - 1) % contour.Count);
                var next = contour.Start + ((i + 1) % contour.Count);
                if (IsOnAxis(points[current]) && !IsOnAxis(points[previous]) && !IsOnAxis(points[next]))
                {
                    throw new ArgumentException(
                        $"The profile touches the rotation axis at the isolated vertex ({points[current].X}, {points[current].Y}); a vertex on the axis needs an adjacent edge lying on the axis.",
                        nameof(region));
                }
            }
        }
    }

    /// <summary>Tests whether a profile point lies exactly on the rotation axis (no tolerance).</summary>
    /// <param name="point">The profile point.</param>
    /// <returns><see langword="true"/> when <c>x == 0</c>.</returns>
    private static bool IsOnAxis(Vector2D point) => !(point.X > 0.0) && !(point.X < 0.0);

    /// <summary>
    ///     Creates the vertex rings: one vertex per axis profile vertex (shared by all rings) and one vertex
    ///     per off-axis profile vertex and azimuth.
    /// </summary>
    /// <param name="builder">The target builder.</param>
    /// <param name="region">The profile.</param>
    /// <param name="angleDegrees">The sweep angle in degrees.</param>
    /// <param name="segments">The number of angular steps.</param>
    /// <param name="full">Whether the sweep is a full revolution (no duplicated seam ring).</param>
    /// <returns>For each ring, the mesh vertex index of every profile vertex.</returns>
    private static int[][] BuildRevolveRings(MeshBuilder builder, PolygonRegion region, double angleDegrees, int segments, bool full)
    {
        var points = region.VertexSpan.ToArray();
        var ringCount = full ? segments : segments + 1;

        // A full revolution uses exactly 2 pi so its rings coincide with the rings of a cylinder
        var angle = full ? 2.0 * Math.PI : angleDegrees * Math.PI / 180.0;
        var rings = new int[ringCount][];
        for (var j = 0; j < ringCount; j++)
        {
            rings[j] = new int[points.Length];
        }

        for (var i = 0; i < points.Length; i++)
        {
            // An exactly zero radius is an axis vertex: one shared vertex, not one per ring
            if (!(points[i].X > 0.0) && !(points[i].X < 0.0))
            {
                var axis = builder.AddVertex(new Vector3D(0.0, 0.0, points[i].Y));
                for (var j = 0; j < ringCount; j++)
                {
                    rings[j][i] = axis;
                }

                continue;
            }

            for (var j = 0; j < ringCount; j++)
            {
                var (sin, cos) = Math.SinCos(angle * j / segments);
                rings[j][i] = builder.AddVertex(new Vector3D(points[i].X * cos, points[i].X * sin, points[i].Y));
                if (j > 0 && rings[j][i] == rings[j - 1][i])
                {
                    // Floating point cannot resolve this step at this radius; welding would leave holes in the mesh
                    throw new ArgumentException("The sweep step is too small to resolve at the profile's radius.", nameof(region));
                }
            }
        }

        return rings;
    }

    /// <summary>
    ///     Adds the swept side walls of every profile edge; edges lying on the axis collapse and are skipped
    ///     by the repeated-corner rule.
    /// </summary>
    /// <param name="builder">The target builder.</param>
    /// <param name="region">The profile.</param>
    /// <param name="rings">The vertex rings.</param>
    /// <param name="full">Whether the last segment wraps to the first ring.</param>
    /// <param name="negative">Whether the profile is on the negative side (winding is reversed).</param>
    private static void AddRevolveWalls(MeshBuilder builder, PolygonRegion region, int[][] rings, bool full, bool negative)
    {
        var segments = full ? rings.Length : rings.Length - 1;
        foreach (var contour in region.ContourTable)
        {
            for (var i = 0; i < contour.Count; i++)
            {
                // Edge u -> v with the solid on its left; for positive x the sweep tangent is +Y at azimuth 0
                var u = contour.Start + i;
                var v = contour.Start + ((i + 1) % contour.Count);
                for (var j = 0; j < segments; j++)
                {
                    var current = rings[j];
                    var following = rings[(j + 1) % rings.Length];
                    AddTriangle(builder, current[u], following[u], current[v], negative);
                    AddTriangle(builder, following[u], following[v], current[v], negative);
                }
            }
        }
    }

    /// <summary>
    ///     Adds the start cap (azimuth 0) and end cap (final azimuth) of a partial sweep.
    /// </summary>
    /// <param name="builder">The target builder.</param>
    /// <param name="region">The profile.</param>
    /// <param name="rings">The vertex rings (segments + 1 of them).</param>
    /// <param name="negative">Whether the profile is on the negative side (winding is reversed).</param>
    private static void AddRevolveCaps(MeshBuilder builder, PolygonRegion region, int[][] rings, bool negative)
    {
        var start = rings[0];
        var end = rings[^1];
        var triangles = PolygonTriangulator.TriangulateToArray(region);
        for (var t = 0; t < triangles.Length; t += 3)
        {
            var a = triangles[t];
            var b = triangles[t + 1];
            var c = triangles[t + 2];

            // A counter-clockwise profile in the (X, Z) plane faces -Y (away from a positive sweep); the end cap is its mirror
            AddTriangle(builder, start[a], start[b], start[c], negative);
            AddTriangle(builder, end[a], end[c], end[b], negative);
        }
    }
}
