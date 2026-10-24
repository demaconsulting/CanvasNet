using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.Tessellation;

/// <summary>
///     Turns a <see cref="PolygonRegion"/> into a solid <see cref="Mesh"/> by linear extrusion (optionally
///     twisted and scaled) or by rotation about the Z axis.
/// </summary>
/// <remarks>
///     <para>
///         <b>Closed, outward, valid operands.</b> Every returned mesh is welded, closed (each edge shared by
///         exactly two triangles in opposite directions), wound counter-clockwise when seen from outside
///         (positive <see cref="Mesh.SignedVolume"/>) and free of degenerate triangles, so it is a valid
///         constructive-solid-geometry operand. A region with holes yields one connected shell with a
///         through-hole (the hole walls face into the hole and the caps are triangulated around the hole), so
///         the hole is also visible to the z-buffer path, which never evaluates solids.
///     </para>
///     <para>
///         <b>Shared vertices.</b> Rings of the side walls are created once and shared by index between the
///         wall quads of consecutive slices and the caps, so twisted or scaled extrusions are watertight
///         by construction and never depend on recomputing identical coordinates. The caps are produced by
///         <see cref="PolygonTriangulator"/>, which adds no vertices, so no T-junctions appear. An empty region
///         extrudes to an empty mesh (nothing is rendered).
///     </para>
///     <para>
///         <b>Finite precision.</b> The guarantees above hold in exact arithmetic for ordinary magnitudes. With
///         extreme values (a tiny non-zero scale such as 1e-17, a height near <see cref="double.MaxValue"/>, or
///         coordinates of about 1e200) the rounded vertices can make a triangle degenerate (zero area or
///         overflow), which <c>CsgOperandValidator</c> reports as a <c>DegenerateTriangle</c> defect; such a mesh is
///         not a valid operand. Use scales, heights and coordinates of moderate magnitude.
///     </para>
///     <para>
///         <b>Shapes that can self-overlap.</b> Scale and twist act about the origin. Scaling to an apex, a large
///         scale change or a per-axis scale combined with twist keeps the solid valid only when the profile is
///         star-shaped about the origin; otherwise the result may self-intersect (it can even be inverted) and is
///         not a valid operand. An apex over a region with a hole, or a rotational profile that touches the
///         axis at a single vertex, produces a pinched (non-manifold) vertex although every edge stays
///         manifold.
///     </para>
///     <para>
///         Invalid polygons cannot reach this type: <see cref="PolygonRegion"/> rejects them at construction.
///         All methods are pure and thread-safe.
///     </para>
/// </remarks>
public static partial class Extruder
{
    /// <summary>
    ///     Extrudes a region along +Z with a uniform scale of the top relative to the bottom.
    /// </summary>
    /// <param name="region">The region to extrude (profile in the XY plane).</param>
    /// <param name="height">The extrusion height; finite and greater than zero.</param>
    /// <param name="center"><see langword="true"/> to center the solid on Z = 0; otherwise it spans Z = 0 to <paramref name="height"/>.</param>
    /// <param name="twistDegrees">The counter-clockwise (right-hand rule about +Z) rotation of the top relative to the bottom, in degrees; finite.</param>
    /// <param name="slices">The number of slices (at least one); twisted or scaled solids are refined only by this count.</param>
    /// <param name="uniformScale">The scale of the top ring about the origin; finite and not negative; zero collapses the top to an apex.</param>
    /// <returns>The closed, outward-oriented mesh.</returns>
    /// <remarks>
    ///     Equivalent to the overload with a <see cref="Vector2D"/> scale of <c>(uniformScale, uniformScale)</c>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="region"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the height, slice count or scale is invalid.</exception>
    /// <exception cref="ArgumentException">Thrown when the twist is not finite or the result would exceed the maximum mesh size.</exception>
    public static Mesh Linear(PolygonRegion region, double height, bool center, double twistDegrees, int slices, double uniformScale)
    {
        // Reject the scale here so the exception names this overload's parameter
        if (!double.IsFinite(uniformScale) || uniformScale < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(uniformScale), uniformScale, "The scale must be finite and not negative.");
        }

        return Linear(region, height, center, twistDegrees, slices, (Vector2D?)new Vector2D(uniformScale, uniformScale));
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

    /// <summary>
    ///     Rejects results too large for a mesh before any allocation, so hostile counts fail with a clear
    ///     exception instead of exhausting memory or overflowing an index.
    /// </summary>
    /// <param name="vertices">An upper bound of the vertex count.</param>
    /// <param name="triangles">An upper bound of the triangle count.</param>
    /// <exception cref="ArgumentException">Thrown when the mesh would exceed the maximum array length.</exception>
    private static void RequireMeshSize(long vertices, long triangles)
    {
        if (vertices > Array.MaxLength || (triangles * 3L) > Array.MaxLength)
        {
            throw new ArgumentException("The extrusion would exceed the maximum mesh size; reduce the segment or slice count.");
        }
    }

    /// <summary>
    ///     Adds a triangle unless it has a repeated corner (which happens where a ring collapses to an apex
    ///     or axis vertex), optionally with reversed winding.
    /// </summary>
    /// <param name="builder">The target builder.</param>
    /// <param name="first">The first corner index.</param>
    /// <param name="second">The second corner index.</param>
    /// <param name="third">The third corner index.</param>
    /// <param name="reverse"><see langword="true"/> to swap the second and third corner.</param>
    private static void AddTriangle(MeshBuilder builder, int first, int second, int third, bool reverse)
    {
        if (first == second || second == third || first == third)
        {
            return;
        }

        if (reverse)
        {
            builder.AddTriangle(first, third, second);
        }
        else
        {
            builder.AddTriangle(first, second, third);
        }
    }

    /// <summary>Creates the mesh of an empty region.</summary>
    /// <returns>A mesh without vertices or triangles.</returns>
    private static Mesh EmptyMesh() => new(Array.Empty<Vector3D>(), Array.Empty<int>());
}
