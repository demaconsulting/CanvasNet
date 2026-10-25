using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <content>
///     The <c>rotate_extrude</c> module.
/// </content>
internal static partial class ScadExtrusions
{
    /// <summary>The largest sweep angle in degrees; wider angles are clamped (a full turn).</summary>
    private const double FullTurn = 360.0;

    /// <summary>The largest angular step in degrees the extruder accepts (the step of the coarsest full revolution).</summary>
    private const double MaximumStep = 120.0;

    /// <summary>The parameter names of <c>rotate_extrude</c> in positional order.</summary>
    private static readonly string[] RotateParameters = ["angle", "convexity"];

    /// <summary>
    ///     Builds <c>rotate_extrude(angle, convexity)</c>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The union of the 2D children is revolved about the Z axis: profile X is the radius and profile Y the
    ///         height. The sweep starts at +X and turns counter-clockwise seen from +Z for a positive <c>angle</c>
    ///         (default 360; a larger magnitude is clamped to a full turn) and clockwise for a negative one; an angle
    ///         of zero warns and omits the instance. The profile is swept where it lies: a profile on the negative
    ///         side of the axis (every vertex has <c>x &lt;= 0</c>) is accepted and sweeps its actual coordinates, so
    ///         a point at (-4, 0) with an angle of 90 starts at the azimuth of 180 degrees and ends at 270 degrees
    ///         (the quadrant x &lt; 0, y &lt; 0), and a full turn equals the full turn of the mirrored profile. The
    ///         number of segments follows the fragment rule for the largest radius, scaled by the share of the
    ///         full turn (<c>ceil(max(fragments * |angle| / 360, 1))</c>), and is raised where needed so that no step
    ///         exceeds 120 degrees, the limit of the renderer's extruder. A partial sweep gets flat end caps.
    ///         <c>convexity</c> is accepted and ignored. The triangle budget is charged with the exact count before
    ///         the mesh is built: per segment two wall triangles for a profile edge off the axis and one for an
    ///         edge with an end on it, plus the two caps of a partial sweep (<c>V + 2H - 2P</c> triangles each for a
    ///         profile with <c>V</c> vertices, <c>H</c> holes and <c>P</c> solid contours).
    ///     </para>
    ///     <para>
    ///         A profile with vertices on both sides of the axis is the unsupported feature
    ///         <c>scad-rotate-extrude-axis-crossing</c> (it also applies to a difference operand that crosses the
    ///         axis even where the difference would not, because every operand is revolved separately), and one that
    ///         touches the axis at a single vertex without an edge along the axis is
    ///         <c>scad-rotate-extrude-axis-contact</c> (the swept surfaces would meet at a pinched point); both omit
    ///         the extrusion.
    ///     </para>
    /// </remarks>
    /// <param name="node">The instance.</param>
    /// <param name="results">The results of the children.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The solid, or nothing when the extrusion is invalid or unsupported.</returns>
    public static ScadGeo RotateExtrude(ScadInstance? node, List<ScadGeo> results, ScadGeometryContext context)
    {
        var instance = node!;
        var args = ScadArguments.Bind(instance, RotateParameters, context);
        var shape = CollectShape(instance, results, context);
        var angle = FullTurn;
        if (args.TryGet("angle", out var angleValue) && !ScadArguments.TryNumber(angleValue, out angle))
        {
            context.Reporter.Warn("rotate_extrude: angle must be a number; the instance is ignored", instance.Location);
            return ScadGeo.Empty;
        }

        if (angle == 0.0)
        {
            context.Reporter.Warn("rotate_extrude: angle must not be zero; the instance is ignored", instance.Location);
            return ScadGeo.Empty;
        }

        if (shape.Kind == ScadGeoKind.Empty)
        {
            return ScadGeo.Empty;
        }

        var job = Prepare(shape.Shape!, context);
        if (job is null || !CheckAxis(instance, context, job))
        {
            return ScadGeo.Empty;
        }

        var sweep = Math.Min(Math.Abs(angle), FullTurn);
        var radius = job.Regions.Values.Max(r => r.Vertices.Max(v => Math.Abs(v.X)));
        var fragments = ScadFragmentRule.Fragments(radius, instance.Fragments);
        var segments = (int)Math.Ceiling(Math.Max(fragments * sweep / FullTurn, 1.0));
        segments = Math.Max(segments, (int)Math.Ceiling(sweep / MaximumStep));
        var full = sweep >= FullTurn;

        // The exact triangle count is charged before anything is built: per segment, two wall triangles for a profile
        // edge off the axis and one for an edge with an end on the axis (none when both ends are on it), plus the
        // start and end caps of a partial sweep
        long Predict(PolygonRegion region) =>
            Saturate((WallTrianglesPerSegment(region) * segments) + (full ? 0.0 : 2.0 * CapTriangles(region)));

        // A negative angle sweeps clockwise: revolve counter-clockwise and mirror the result in the XZ plane
        var mirror = Matrix4x4D.CreateMirror(new Vector3D(0.0, 1.0, 0.0));
        return Extrude(
            job,
            context,
            instance,
            shape.IsApproximate,
            Predict,
            region =>
            {
                var mesh = Extruder.Rotate(region, sweep, segments);
                return angle < 0.0 ? mesh.Transform(mirror) : mesh;
            });
    }

    /// <summary>
    ///     Counts the wall triangles of one angular segment of a revolved profile.
    /// </summary>
    /// <param name="region">The profile.</param>
    /// <returns>Two triangles per edge with both ends off the axis, one per edge with exactly one end on the axis.</returns>
    private static double WallTrianglesPerSegment(PolygonRegion region)
    {
        var triangles = 0.0;
        foreach (var contour in region.Contours)
        {
            for (var i = 0; i < contour.Count; i++)
            {
                var startOnAxis = contour[i].X == 0.0;
                var endOnAxis = contour[(i + 1) % contour.Count].X == 0.0;
                triangles += (startOnAxis ? 0.0 : 1.0) + (endOnAxis ? 0.0 : 1.0);
            }
        }

        return triangles;
    }

    /// <summary>
    ///     Checks the position of the profile relative to the axis.
    /// </summary>
    /// <remarks>
    ///     A profile entirely on the negative side is kept as it is: the extruder sweeps the actual coordinates, so
    ///     a profile at x &lt; 0 sweeps around the axis starting at the azimuth of 180 degrees.
    /// </remarks>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <param name="job">The prepared leaves.</param>
    /// <returns><see langword="true"/> when the profile can be revolved; otherwise the problem was reported.</returns>
    private static bool CheckAxis(ScadInstance node, ScadGeometryContext context, Job job)
    {
        var positive = false;
        var negative = false;
        foreach (var region in job.Regions.Values)
        {
            foreach (var x in region.Vertices.Select(v => v.X))
            {
                positive |= x > 0.0;
                negative |= x < 0.0;
            }
        }

        if (positive && negative)
        {
            context.Reporter.Unsupported(
                ScadFeatureIds.RotateExtrudeAxisCrossing,
                "rotate_extrude: the profile lies on both sides of the Z axis, which is not supported; the extrusion is omitted",
                node.Location);
            return false;
        }

        foreach (var region in job.Regions.Values)
        {
            if (FindAxisContact(region) is { } contact)
            {
                context.Reporter.Unsupported(
                    ScadFeatureIds.RotateExtrudeAxisContact,
                    $"rotate_extrude: the profile touches the Z axis at the single point ({contact.X}, {contact.Y}), which is not supported; the extrusion is omitted",
                    node.Location);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Finds a profile vertex that lies on the axis while both of its neighbors on the contour are off the axis.
    /// </summary>
    /// <remarks>
    ///     This is exactly the extruder's rule: a vertex on the axis must be an end of an edge that lies on the axis
    ///     (a pole); an isolated contact would be a pinched vertex of the swept surface. There is no tolerance:
    ///     <c>x == 0</c> means on the axis.
    /// </remarks>
    /// <param name="region">The profile.</param>
    /// <returns>The first isolated contact, or <see langword="null"/> when there is none.</returns>
    private static Vector2D? FindAxisContact(PolygonRegion region)
    {
        foreach (var contour in region.Contours)
        {
            for (var i = 0; i < contour.Count; i++)
            {
                var previous = contour[(i + contour.Count - 1) % contour.Count];
                var next = contour[(i + 1) % contour.Count];
                if (contour[i].X == 0.0 && previous.X != 0.0 && next.X != 0.0)
                {
                    return contour[i];
                }
            }
        }

        return null;
    }
}
