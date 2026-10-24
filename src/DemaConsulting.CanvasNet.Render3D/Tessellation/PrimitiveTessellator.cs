// cspell:ignore tessellator

using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.Tessellation;

/// <summary>
///     Converts analytic primitives (box, sphere, cylinder/cone, polyhedron) into
///     <see cref="Mesh"/> instances.
/// </summary>
/// <remarks>
///     <para>
///         Every primitive is built through <see cref="MeshBuilder"/> so that each vertex is
///         computed once and shared by index; the result is therefore welded, closed (every edge
///         is shared by exactly two triangles in opposite directions) and wound
///         counter-clockwise when seen from outside, giving a positive
///         <see cref="Mesh.SignedVolume"/>. The primitives are oriented in right-handed Z-up model
///         space. The only exception is <see cref="Polyhedron"/>, whose orientation and closed-surface property
///         are those of the caller's faces.
///     </para>
///     <para>
///         Curved primitives are faceted: a sphere or cylinder with <c>n</c> segments is an
///         inscribed polyhedron, so its volume is slightly below the analytic value. Choose
///         <c>segments</c> to trade smoothness against triangle count. All methods are pure and
///         thread-safe.
///     </para>
/// </remarks>
public static class PrimitiveTessellator
{
    /// <summary>Smallest accepted number of azimuthal segments for curved primitives.</summary>
    private const int MinimumSegments = 3;

    /// <summary>
    ///     Creates an axis-aligned box with 8 vertices and 12 triangles.
    /// </summary>
    /// <param name="size">The edge lengths along X, Y and Z; every component must be finite and greater than zero.</param>
    /// <param name="center">
    ///     <see langword="true"/> to center the box on the origin; <see langword="false"/> (default)
    ///     to span from the origin to <paramref name="size"/>.
    /// </param>
    /// <returns>The closed, outward-oriented box mesh.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any size component is not finite or not greater than zero.</exception>
    public static Mesh Box(Vector3D size, bool center = false)
    {
        // A box needs a positive, finite extent on every axis
        RequirePositiveFinite(size.X, nameof(size));
        RequirePositiveFinite(size.Y, nameof(size));
        RequirePositiveFinite(size.Z, nameof(size));

        var min = center ? size * -0.5 : Vector3D.Zero;
        var builder = new MeshBuilder();

        // Vertex i has x at bit 0, y at bit 1 and z at bit 2 of i
        var v = new int[8];
        for (var i = 0; i < 8; i++)
        {
            v[i] = builder.AddVertex(new Vector3D(
                min.X + (((i & 1) != 0) ? size.X : 0.0),
                min.Y + (((i & 2) != 0) ? size.Y : 0.0),
                min.Z + (((i & 4) != 0) ? size.Z : 0.0)));
        }

        // Two counter-clockwise (seen from outside) triangles per face
        AddQuad(builder, v[0], v[2], v[3], v[1]); // -Z
        AddQuad(builder, v[4], v[5], v[7], v[6]); // +Z
        AddQuad(builder, v[0], v[1], v[5], v[4]); // -Y
        AddQuad(builder, v[2], v[6], v[7], v[3]); // +Y
        AddQuad(builder, v[0], v[4], v[6], v[2]); // -X
        AddQuad(builder, v[1], v[3], v[7], v[5]); // +X
        return builder.Build();
    }

    /// <summary>
    ///     Creates a UV sphere centered on the origin with true pole vertices on the +Z and -Z axis.
    /// </summary>
    /// <param name="radius">The sphere radius; finite and greater than zero.</param>
    /// <param name="segments">
    ///     The number of azimuthal divisions (at least 3). The number of latitude rings is
    ///     <c>max(2, (segments + 1) / 2)</c>, giving roughly square facets.
    /// </param>
    /// <returns>The closed, outward-oriented sphere mesh (an inscribed polyhedron).</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="radius"/> is not finite and positive or <paramref name="segments"/> is below 3.</exception>
    public static Mesh Sphere(double radius, int segments)
    {
        RequirePositiveFinite(radius, nameof(radius));
        RequireSegments(segments);

        var rings = Math.Max(2, (segments + 1) / 2);
        var builder = new MeshBuilder();
        var north = builder.AddVertex(new Vector3D(0.0, 0.0, radius));
        var south = builder.AddVertex(new Vector3D(0.0, 0.0, -radius));

        // Ring r (1..rings-1) sits at polar angle pi*r/rings; vertices are created once and shared by index
        var ringVertices = new int[rings - 1][];
        for (var r = 1; r < rings; r++)
        {
            var (sin, cos) = Math.SinCos(Math.PI * r / rings);
            ringVertices[r - 1] = AddRing(builder, radius * sin, radius * cos, segments);
        }

        // North pole fan
        var first = ringVertices[0];
        for (var j = 0; j < segments; j++)
        {
            builder.AddTriangle(north, first[j], first[(j + 1) % segments]);
        }

        // Quads between consecutive rings, split into two triangles each
        for (var r = 0; r < ringVertices.Length - 1; r++)
        {
            var upper = ringVertices[r];
            var lower = ringVertices[r + 1];
            for (var j = 0; j < segments; j++)
            {
                var jn = (j + 1) % segments;
                builder.AddTriangle(upper[j], lower[j], upper[jn]);
                builder.AddTriangle(upper[jn], lower[j], lower[jn]);
            }
        }

        // South pole fan (reversed so it faces outward)
        var last = ringVertices[^1];
        for (var j = 0; j < segments; j++)
        {
            builder.AddTriangle(south, last[(j + 1) % segments], last[j]);
        }

        return builder.Build();
    }

    /// <summary>
    ///     Creates a cylinder, frustum or cone along the +Z axis.
    /// </summary>
    /// <remarks>
    ///     Giving exactly one radius as zero produces a cone with a single apex vertex and no
    ///     degenerate triangles. Caps are triangle fans around a dedicated centre vertex.
    /// </remarks>
    /// <param name="height">The height along +Z; finite and greater than zero.</param>
    /// <param name="radius1">The radius at the bottom (z = 0, or -height/2 when centered); finite and not negative.</param>
    /// <param name="radius2">The radius at the top; finite and not negative; at least one of the radii must be greater than zero.</param>
    /// <param name="segments">The number of azimuthal divisions (at least 3).</param>
    /// <param name="center"><see langword="true"/> to center the solid on the origin along Z.</param>
    /// <returns>The closed, outward-oriented mesh.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the height, a radius or the segment count is invalid, or both radii are zero.</exception>
    public static Mesh Cylinder(double height, double radius1, double radius2, int segments, bool center = false)
    {
        RequirePositiveFinite(height, nameof(height));
        RequireNonNegativeFinite(radius1, nameof(radius1));
        RequireNonNegativeFinite(radius2, nameof(radius2));
        RequireSegments(segments);

        // With both radii zero there is no solid left
        if (!(radius1 > 0.0) && !(radius2 > 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(radius1), "At least one radius must be greater than zero.");
        }

        var z0 = center ? -height / 2.0 : 0.0;
        var z1 = z0 + height;
        var builder = new MeshBuilder();

        // A zero radius collapses its end to a single apex vertex
        var bottom = radius1 > 0.0 ? AddRing(builder, radius1, z0, segments) : [builder.AddVertex(new Vector3D(0.0, 0.0, z0))];
        var top = radius2 > 0.0 ? AddRing(builder, radius2, z1, segments) : [builder.AddVertex(new Vector3D(0.0, 0.0, z1))];

        // Side wall
        for (var j = 0; j < segments; j++)
        {
            var jn = (j + 1) % segments;
            if (radius1 > 0.0 && radius2 > 0.0)
            {
                builder.AddTriangle(bottom[j], bottom[jn], top[j]);
                builder.AddTriangle(bottom[jn], top[jn], top[j]);
            }
            else if (radius1 > 0.0)
            {
                // Apex on top
                builder.AddTriangle(bottom[j], bottom[jn], top[0]);
            }
            else
            {
                // Apex at the bottom
                builder.AddTriangle(bottom[0], top[jn], top[j]);
            }
        }

        // Caps are fans around a dedicated centre vertex (facing -Z at the bottom, +Z at the top)
        if (radius1 > 0.0)
        {
            var centre = builder.AddVertex(new Vector3D(0.0, 0.0, z0));
            for (var j = 0; j < segments; j++)
            {
                builder.AddTriangle(centre, bottom[(j + 1) % segments], bottom[j]);
            }
        }

        if (radius2 > 0.0)
        {
            var centre = builder.AddVertex(new Vector3D(0.0, 0.0, z1));
            for (var j = 0; j < segments; j++)
            {
                builder.AddTriangle(centre, top[j], top[(j + 1) % segments]);
            }
        }

        return builder.Build();
    }

    /// <summary>
    ///     Creates a mesh from an explicit point list and polygonal faces.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Each face is fan-triangulated from its first index, so faces must be <b>convex</b>
    ///         (ear clipping for concave faces arrives with a later phase). Faces must be listed
    ///         counter-clockwise when seen from outside. This method validates structure only: it
    ///         neither repairs the orientation nor requires the result to be closed. Callers who
    ///         need an outward-oriented closed mesh should check <see cref="Mesh.IsClosed"/> and
    ///         <see cref="Mesh.SignedVolume"/> and apply <see cref="Mesh.Reverse"/> when the volume
    ///         is negative.
    ///     </para>
    ///     <para>
    ///         Identical points are welded, so a face that repeats an index, or that lists two
    ///         distinct indices addressing identical points, is rejected (every vertex of a face
    ///         must be distinct after welding).
    ///     </para>
    /// </remarks>
    /// <param name="points">The vertex positions; each must be finite.</param>
    /// <param name="facesCcw">The faces as lists of at least three distinct, in-range point indices in counter-clockwise order.</param>
    /// <returns>The mesh.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument or a face is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when a face has fewer than three vertices, repeats an index, lists indices of identical (welded) points, or a point is not finite.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a face index does not address a point.</exception>
    public static Mesh Polyhedron(IReadOnlyList<Vector3D> points, IReadOnlyList<IReadOnlyList<int>> facesCcw)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(facesCcw);

        // Map each input point to its welded vertex index
        var builder = new MeshBuilder();
        var map = new int[points.Count];
        for (var i = 0; i < map.Length; i++)
        {
            map[i] = builder.AddVertex(points[i]);
        }

        foreach (var face in facesCcw)
        {
            if (face is null)
            {
                throw new ArgumentNullException(nameof(facesCcw), "A face must not be null.");
            }

            if (face.Count < 3)
            {
                throw new ArgumentException("A face requires at least three vertices.", nameof(facesCcw));
            }

            // Translate and range-check face indices
            var polygon = new int[face.Count];
            for (var k = 0; k < polygon.Length; k++)
            {
                if ((uint)face[k] >= (uint)map.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(facesCcw), face[k], "A face index does not address a point.");
                }

                polygon[k] = map[face[k]];
            }

            // Reject any repeated vertex (the fan would otherwise accept non-adjacent repeats)
            for (var k = 1; k < polygon.Length; k++)
            {
                for (var m = 0; m < k; m++)
                {
                    if (polygon[k] != polygon[m])
                    {
                        continue;
                    }

                    throw new ArgumentException(
                        face[k] == face[m]
                            ? $"A face must not repeat an index (index {face[k]})."
                            : $"A face collapses after welding identical points (indices {face[m]} and {face[k]}).",
                        nameof(facesCcw));
                }
            }

            builder.AddPolygon(polygon);
        }

        return builder.Build();
    }

    /// <summary>Adds the two counter-clockwise triangles of a convex quad.</summary>
    /// <param name="builder">The target builder.</param>
    /// <param name="a">First corner.</param>
    /// <param name="b">Second corner.</param>
    /// <param name="c">Third corner.</param>
    /// <param name="d">Fourth corner.</param>
    private static void AddQuad(MeshBuilder builder, int a, int b, int c, int d)
    {
        builder.AddTriangle(a, b, c);
        builder.AddTriangle(a, c, d);
    }

    /// <summary>
    ///     Adds a horizontal ring of <paramref name="segments"/> vertices counter-clockwise seen
    ///     from +Z.
    /// </summary>
    /// <param name="builder">The target builder.</param>
    /// <param name="radius">The ring radius (greater than zero).</param>
    /// <param name="z">The ring height.</param>
    /// <param name="segments">The vertex count.</param>
    /// <returns>The vertex indices in azimuth order.</returns>
    private static int[] AddRing(MeshBuilder builder, double radius, double z, int segments)
    {
        var ring = new int[segments];
        for (var j = 0; j < segments; j++)
        {
            var (sin, cos) = Math.SinCos(2.0 * Math.PI * j / segments);
            ring[j] = builder.AddVertex(new Vector3D(radius * cos, radius * sin, z));
        }

        return ring;
    }

    /// <summary>Rejects values that are not finite and strictly positive.</summary>
    /// <param name="value">The value to check.</param>
    /// <param name="paramName">The parameter name for the exception.</param>
    private static void RequirePositiveFinite(double value, string paramName)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "The value must be finite and greater than zero.");
        }
    }

    /// <summary>Rejects values that are not finite or are negative.</summary>
    /// <param name="value">The value to check.</param>
    /// <param name="paramName">The parameter name for the exception.</param>
    private static void RequireNonNegativeFinite(double value, string paramName)
    {
        if (!double.IsFinite(value) || value < 0.0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "The value must be finite and not negative.");
        }
    }

    /// <summary>Rejects segment counts below <see cref="MinimumSegments"/>.</summary>
    /// <param name="segments">The segment count.</param>
    private static void RequireSegments(int segments)
    {
        if (segments < MinimumSegments)
        {
            throw new ArgumentOutOfRangeException(nameof(segments), segments, "At least 3 segments are required.");
        }
    }
}
