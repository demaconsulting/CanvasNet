using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <content>
///     The <c>linear_extrude</c> module.
/// </content>
internal static partial class ScadExtrusions
{
    /// <summary>The default <c>height</c> of <c>linear_extrude</c>.</summary>
    private const double DefaultHeight = 100.0;

    /// <summary>The parameter names of <c>linear_extrude</c> in positional order.</summary>
    private static readonly string[] LinearParameters = ["height", "center", "convexity", "twist", "slices", "scale"];

    /// <summary>
    ///     Builds <c>linear_extrude(height, center, convexity, twist, slices, scale)</c>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The union of the 2D children is extruded along +Z from 0 to <c>height</c> (default 100; centered on
    ///         Z = 0 with <c>center</c>). A positive <c>twist</c> turns the top clockwise seen from +Z (the renderer's
    ///         extruder turns the other way, so the angle is negated); <c>slices</c> defaults to 1, or, when there is a
    ///         twist, to enough slices that no slice turns by more than one fragment of the circle through the
    ///         farthest profile point (<c>ceil(fragments(radius) * |twist| / 360)</c>, the fragment rule of
    ///         <see cref="ScadFragmentRule"/>). <c>scale</c> is a number or a vector <c>[sx, sy]</c>, the scale of
    ///         the top about the origin; both components must be positive, or both zero (a cone-like apex, only for a
    ///         profile without holes or islands). <c>convexity</c> is accepted and ignored.
    ///     </para>
    ///     <para>
    ///         A height that is not a positive number, <c>slices</c> below 1 and a malformed scale are warnings and
    ///         omit the instance. A negative or half-zero scale is the unsupported feature
    ///         <c>scad-linear-extrude-scale</c>; an apex over a profile with several contours is
    ///         <c>scad-linear-extrude-apex-multiple-contours</c>; both omit the extrusion.
    ///     </para>
    /// </remarks>
    /// <param name="node">The instance.</param>
    /// <param name="results">The results of the children.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The solid, or nothing when the extrusion is invalid or unsupported.</returns>
    public static ScadGeo LinearExtrude(ScadInstance? node, List<ScadGeo> results, ScadGeometryContext context)
    {
        var instance = node!;
        var args = ScadArguments.Bind(instance, LinearParameters, context);
        var shape = CollectShape(instance, results, context);
        var parameters = ReadLinearParameters(instance, args, context);
        if (parameters is null || shape.Kind == ScadGeoKind.Empty)
        {
            return ScadGeo.Empty;
        }

        var job = Prepare(shape.Shape!, context);
        if (job is null)
        {
            return ScadGeo.Empty;
        }

        // An apex collapses every contour of a leaf onto one point, which the extruder cannot do for several contours
        var scale = parameters.Scale;
        if (scale.X == 0.0 && scale.Y == 0.0 && job.Regions.Values.Any(r => r.Contours.Count > 1))
        {
            context.Reporter.Unsupported(
                ScadFeatureIds.LinearExtrudeApexMultipleContours,
                "linear_extrude: scale = 0 is not supported for a profile with holes or separate islands; the extrusion is omitted",
                instance.Location);
            return ScadGeo.Empty;
        }

        var slices = ChooseSlices(instance, parameters, job);
        var vertices = job.Regions.Values.Sum(r => (double)r.VertexCount);
        context.ChargeTriangles(Saturate((2.0 * vertices * slices) + (2.0 * vertices)), instance.Location);
        var sliceCount = (int)Math.Min(slices, int.MaxValue);
        Vector2D? topScale = ScadNumbers.AreEqual(scale.X, 1.0) && ScadNumbers.AreEqual(scale.Y, 1.0) ? null : scale;
        return Extrude(
            job,
            context,
            instance,
            shape.IsApproximate,
            region => Extruder.Linear(region, parameters.Height, parameters.Center, -parameters.Twist, sliceCount, topScale));
    }

    /// <summary>
    ///     The validated arguments of <c>linear_extrude</c>.
    /// </summary>
    /// <param name="Height">The height.</param>
    /// <param name="Center">Whether the solid is centered on Z = 0.</param>
    /// <param name="Twist">The twist in degrees, positive clockwise seen from +Z.</param>
    /// <param name="Slices">The explicit slice count, or <see langword="null"/> for the default rule.</param>
    /// <param name="Scale">The scale of the top.</param>
    private sealed record LinearArguments(double Height, bool Center, double Twist, double? Slices, Vector2D Scale);

    /// <summary>
    ///     Reads and validates the arguments of <c>linear_extrude</c>.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="args">The bound arguments.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The arguments, or <see langword="null"/> when the instance is invalid or unsupported (already reported).</returns>
    private static LinearArguments? ReadLinearParameters(ScadInstance node, ScadArguments args, ScadGeometryContext context)
    {
        var height = DefaultHeight;
        if (args.TryGet("height", out var heightValue) && (!ScadArguments.TryNumber(heightValue, out height) || height <= 0.0))
        {
            return Reject(node, context, "height must be a positive number");
        }

        var twist = 0.0;
        if (args.TryGet("twist", out var twistValue) && !ScadArguments.TryNumber(twistValue, out twist))
        {
            return Reject(node, context, "twist must be a number");
        }

        double? slices = null;
        if (args.TryGet("slices", out var slicesValue))
        {
            if (!ScadArguments.TryNumber(slicesValue, out var count) || count < 1.0)
            {
                return Reject(node, context, "slices must be a number of at least 1");
            }

            slices = Math.Floor(count);
        }

        var scale = new Vector2D(1.0, 1.0);
        if (args.TryGet("scale", out var scaleValue))
        {
            if (ScadArguments.TryNumber(scaleValue, out var uniform))
            {
                scale = new Vector2D(uniform, uniform);
            }
            else if (ScadArguments.TryNumbers(scaleValue, 2, out var pair))
            {
                scale = new Vector2D(pair![0], pair[1]);
            }
            else
            {
                return Reject(node, context, "scale must be a number or a vector of two numbers");
            }

            // The extruder cannot collapse the top to a line or pass through zero
            if (scale.X < 0.0 || scale.Y < 0.0 || (scale.X == 0.0) != (scale.Y == 0.0))
            {
                context.Reporter.Unsupported(
                    ScadFeatureIds.LinearExtrudeScale,
                    "linear_extrude: a negative scale, or a scale with exactly one zero component, is not supported; the extrusion is omitted",
                    node.Location);
                return null;
            }
        }

        var center = args.TryGet("center", out var centerValue) && centerValue.IsTrue;
        return new LinearArguments(height, center, twist, slices, scale);
    }

    /// <summary>
    ///     Chooses the slice count: the explicit value, or the default rule.
    /// </summary>
    /// <param name="node">The instance (for the fragment settings).</param>
    /// <param name="parameters">The validated arguments.</param>
    /// <param name="job">The prepared leaves.</param>
    /// <returns>The slice count as a double (it may exceed the range of an integer for a hostile twist).</returns>
    private static double ChooseSlices(ScadInstance node, LinearArguments parameters, Job job)
    {
        if (parameters.Slices is { } explicitSlices)
        {
            return explicitSlices;
        }

        if (parameters.Twist == 0.0)
        {
            return 1.0;
        }

        // Twist turns about the origin, so the farthest vertex sweeps the longest arc
        var radius = job.Regions.Values.Max(r => r.Vertices.Max(v => Math.Sqrt((v.X * v.X) + (v.Y * v.Y))));
        var fragments = ScadFragmentRule.Fragments(radius, node.Fragments);
        return Math.Max(1.0, Math.Ceiling(fragments * Math.Abs(parameters.Twist) / 360.0));
    }

    /// <summary>
    ///     Reports an invalid argument as a warning and returns <see langword="null"/>.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <param name="problem">What is wrong, without the module name.</param>
    /// <returns><see langword="null"/>.</returns>
    private static LinearArguments? Reject(ScadInstance node, ScadGeometryContext context, string problem)
    {
        context.Reporter.Warn($"{node.Name}: {problem}; the instance is ignored", node.Location);
        return null;
    }
}
