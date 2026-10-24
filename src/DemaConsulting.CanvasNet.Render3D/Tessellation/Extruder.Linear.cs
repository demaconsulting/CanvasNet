using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.Tessellation;

/// <summary>
///     Linear extrusion (<see cref="Linear(PolygonRegion, double, bool, double, int, Vector2D?)"/>).
/// </summary>
public static partial class Extruder
{
    /// <summary>
    ///     Extrudes a region along +Z, optionally twisting and scaling the top relative to the bottom.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Ring <c>k</c> (<c>k = 0..slices</c>) sits at <c>t = k / slices</c>: height <c>z0 + height * t</c>,
    ///         per-axis scale <c>1 + (scale - 1) t</c> applied first, then a counter-clockwise rotation of
    ///         <c>twistDegrees * t</c> about +Z (right-hand rule). Each ring vertex is created once and shared by
    ///         the wall quads of both neighboring slices and by the cap at the end, so the solid is watertight.
    ///         Positive twist is counter-clockwise seen from +Z; language front ends with a different
    ///         convention negate it.
    ///     </para>
    ///     <para>
    ///         A scale of <c>(0, 0)</c> collapses the top to a single apex vertex (no top cap, one triangle per
    ///         wall edge in the last slice); exactly one zero component or a negative component is rejected
    ///         because it would flatten the top to a line or pass through zero and self-intersect. Contours
    ///         are normalized (solid on the left), so one wall formula gives outward normals for outer walls
    ///         and hole walls alike. Twisted or non-uniformly scaled wall quads are not planar; the facet
    ///         volume then only approaches the smooth solid as <paramref name="slices"/> grows, and a large
    ///         twist per slice can make neighboring slices intersect, so choose the slice count accordingly.
    ///     </para>
    /// </remarks>
    /// <param name="region">The region to extrude (profile in the XY plane).</param>
    /// <param name="height">The extrusion height; finite and greater than zero.</param>
    /// <param name="center"><see langword="true"/> to center the solid on Z = 0; otherwise it spans Z = 0 to <paramref name="height"/>.</param>
    /// <param name="twistDegrees">The counter-clockwise rotation of the top relative to the bottom, in degrees; finite.</param>
    /// <param name="slices">The number of slices along the height (at least one).</param>
    /// <param name="scale">
    ///     The per-axis scale of the top ring about the origin relative to the bottom; each component finite and not
    ///     negative, and either both zero (apex) or both positive. <see langword="null"/> means <c>(1, 1)</c>.
    /// </param>
    /// <returns>The closed, outward-oriented mesh; empty for an empty region.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="region"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the height is not finite and positive, <paramref name="slices"/> is below 1, or the scale is invalid.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="twistDegrees"/> is not finite or the result would exceed the maximum mesh size.</exception>
    public static Mesh Linear(PolygonRegion region, double height, bool center = false, double twistDegrees = 0.0, int slices = 1, Vector2D? scale = null)
    {
        // Validate everything before doing any work
        ArgumentNullException.ThrowIfNull(region);
        RequirePositiveFinite(height, nameof(height));
        if (slices < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(slices), slices, "At least one slice is required.");
        }

        if (!double.IsFinite(twistDegrees))
        {
            throw new ArgumentException("The twist must be finite.", nameof(twistDegrees));
        }

        var topScale = scale ?? new Vector2D(1.0, 1.0);
        var apex = ValidateScale(topScale);
        if (region.IsEmpty)
        {
            return EmptyMesh();
        }

        // Upper bounds: V(S+1) vertices; 2VS wall triangles plus two caps of at most V triangles each
        var vertexCount = region.VertexCount;
        RequireMeshSize(((long)vertexCount * (slices + 1L)) + 1L, (2L * vertexCount * slices) + (2L * vertexCount));

        var builder = new MeshBuilder();
        var spec = new LinearSpec(center ? -height / 2.0 : 0.0, height, twistDegrees * Math.PI / 180.0, slices, topScale, apex);
        var rings = BuildRings(builder, region, spec);
        AddWalls(builder, region, rings);
        AddCaps(builder, region, rings[0], rings[slices], apex);
        return builder.Build();
    }

    /// <summary>The numeric parameters of a linear extrusion, grouped to keep the helpers' signatures small.</summary>
    /// <param name="Bottom">The Z of the bottom ring.</param>
    /// <param name="Height">The extrusion height.</param>
    /// <param name="TwistRadians">The total counter-clockwise twist in radians.</param>
    /// <param name="Slices">The slice count.</param>
    /// <param name="TopScale">The scale of the last ring.</param>
    /// <param name="Apex">Whether the last ring collapses to a single vertex.</param>
    private readonly record struct LinearSpec(double Bottom, double Height, double TwistRadians, int Slices, Vector2D TopScale, bool Apex);

    /// <summary>Validates the top scale and reports whether it collapses the top to an apex.</summary>
    /// <param name="scale">The requested scale.</param>
    /// <returns><see langword="true"/> when both components are zero.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for non-finite, negative or half-zero scales.</exception>
    private static bool ValidateScale(Vector2D scale)
    {
        if (!scale.IsFinite || scale.X < 0.0 || scale.Y < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "The scale must be finite and not negative.");
        }

        // A single zero component flattens the top to a line (degenerate triangles); only a full collapse is legal
        var apex = !(scale.X > 0.0) && !(scale.Y > 0.0);
        if (!apex && (!(scale.X > 0.0) || !(scale.Y > 0.0)))
        {
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "The scale must be zero in both axes (apex) or positive in both.");
        }

        return apex;
    }

    /// <summary>
    ///     Creates the vertex rings of all slice boundaries, one vertex per region vertex per ring (a single
    ///     apex vertex shared by every entry of the last ring when the top collapses).
    /// </summary>
    /// <param name="builder">The target builder.</param>
    /// <param name="region">The extruded region.</param>
    /// <param name="spec">The numeric extrusion parameters.</param>
    /// <returns>For each ring, the mesh vertex index of every region vertex.</returns>
    private static int[][] BuildRings(MeshBuilder builder, PolygonRegion region, LinearSpec spec)
    {
        var points = region.VertexSpan.ToArray();
        var rings = new int[spec.Slices + 1][];
        for (var k = 0; k <= spec.Slices; k++)
        {
            // The last ring uses the exact requested height and scale instead of an accumulated fraction
            var last = k == spec.Slices;
            var t = (double)k / spec.Slices;
            var z = last ? spec.Bottom + spec.Height : spec.Bottom + (spec.Height * t);
            var ring = new int[points.Length];
            if (last && spec.Apex)
            {
                Array.Fill(ring, builder.AddVertex(new Vector3D(0.0, 0.0, z)));
                rings[k] = ring;
                continue;
            }

            var scaleX = last ? spec.TopScale.X : 1.0 + ((spec.TopScale.X - 1.0) * t);
            var scaleY = last ? spec.TopScale.Y : 1.0 + ((spec.TopScale.Y - 1.0) * t);
            var (sin, cos) = Math.SinCos(spec.TwistRadians * t);
            for (var i = 0; i < points.Length; i++)
            {
                var x = scaleX * points[i].X;
                var y = scaleY * points[i].Y;
                ring[i] = builder.AddVertex(new Vector3D((cos * x) - (sin * y), (sin * x) + (cos * y), z));
            }

            rings[k] = ring;
        }

        return rings;
    }

    /// <summary>
    ///     Adds the side walls of every contour edge in every slice.
    /// </summary>
    /// <param name="builder">The target builder.</param>
    /// <param name="region">The extruded region.</param>
    /// <param name="rings">The vertex rings.</param>
    private static void AddWalls(MeshBuilder builder, PolygonRegion region, int[][] rings)
    {
        var table = region.ContourTable.ToArray();
        for (var k = 0; k < rings.Length - 1; k++)
        {
            var below = rings[k];
            var above = rings[k + 1];
            foreach (var contour in table)
            {
                for (var i = 0; i < contour.Count; i++)
                {
                    // Edge u -> v with the solid on its left; the quad's outward normal then points away from the solid
                    var u = contour.Start + i;
                    var v = contour.Start + ((i + 1) % contour.Count);
                    AddTriangle(builder, below[u], below[v], above[v], reverse: false);
                    AddTriangle(builder, below[u], above[v], above[u], reverse: false);
                }
            }
        }
    }

    /// <summary>
    ///     Adds the bottom cap (facing -Z) and, unless the top collapsed, the top cap (facing +Z).
    /// </summary>
    /// <param name="builder">The target builder.</param>
    /// <param name="region">The extruded region.</param>
    /// <param name="bottomRing">The bottom ring.</param>
    /// <param name="topRing">The top ring.</param>
    /// <param name="apex">Whether the top collapsed to an apex.</param>
    private static void AddCaps(MeshBuilder builder, PolygonRegion region, int[] bottomRing, int[] topRing, bool apex)
    {
        var triangles = PolygonTriangulator.TriangulateToArray(region);
        for (var t = 0; t < triangles.Length; t += 3)
        {
            var a = triangles[t];
            var b = triangles[t + 1];
            var c = triangles[t + 2];

            // The triangulation is counter-clockwise seen from +Z: as is on top, reversed on the bottom
            AddTriangle(builder, bottomRing[a], bottomRing[b], bottomRing[c], reverse: true);
            if (!apex)
            {
                AddTriangle(builder, topRing[a], topRing[b], topRing[c], reverse: false);
            }
        }
    }
}
