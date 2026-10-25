// cspell:ignore polyhedron tessellators

using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     Builds the primitive solids and shapes: <c>cube</c>, <c>sphere</c>, <c>cylinder</c>, <c>polyhedron</c>,
///     <c>square</c>, <c>circle</c> and <c>polygon</c>.
/// </summary>
/// <remarks>
///     <para>
///         Each builder binds the instance's arguments to the documented parameter names (see
///         <see cref="ScadArguments"/>), validates them, charges the triangle budget with an upper bound before
///         anything is allocated, and asks the Render3D tessellators for the mesh or region. An invalid argument
///         (wrong type, non-positive size, an index out of range) is reported as a warning at the instance's
///         location and yields nothing for that instance. A tessellator rejection that the argument checks could not
///         foresee is reported as the unsupported feature <c>scad-geometry-rejected</c>, so a build never crashes on
///         a primitive. Resource problems are <see cref="ScadEvaluationException"/>s.
///     </para>
///     <para>
///         Dialect choices pinned by tests: a diameter (<c>d</c>, <c>d1</c>, <c>d2</c>) overrides the radius
///         parameters, per-end radii override the common radius, <c>center</c> is true for any truthy value, and
///         <c>convexity</c> is accepted and ignored. The builders are stateless and thread-safe.
///     </para>
/// </remarks>
internal static partial class ScadPrimitives
{
    /// <summary>The parameter names of <c>cube</c> in positional order.</summary>
    private static readonly string[] CubeParameters = ["size", "center"];

    /// <summary>The parameter names of <c>sphere</c> in positional order.</summary>
    private static readonly string[] SphereParameters = ["r", "d"];

    /// <summary>The parameter names of <c>cylinder</c> in positional order.</summary>
    private static readonly string[] CylinderParameters = ["h", "r1", "r2", "center", "r", "d", "d1", "d2"];

    /// <summary>
    ///     The outcome of reading an optional length-like argument.
    /// </summary>
    private enum LengthState
    {
        /// <summary>The argument was not supplied (or was <c>undef</c>).</summary>
        Absent,

        /// <summary>The argument was supplied and is valid.</summary>
        Valid,

        /// <summary>The argument was supplied but is not a finite number in range.</summary>
        Invalid,
    }

    /// <summary>
    ///     A length-like argument after reading: whether it was supplied and valid, and its scaled value.
    /// </summary>
    /// <param name="State">Whether the argument was absent, valid or invalid.</param>
    /// <param name="Value">The scaled value; meaningful only when the state is <see cref="LengthState.Valid"/>.</param>
    private readonly record struct Length(LengthState State, double Value);

    /// <summary>
    ///     Builds a <c>cube(size, center)</c>.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <returns>A solid, or <see cref="ScadGeo.Empty"/> when the arguments are invalid.</returns>
    public static ScadGeo Cube(ScadInstance node, ScadGeometryContext context)
    {
        var args = Begin(node, CubeParameters, context);

        // The size is a number (a cube) or a vector of three numbers; every extent must be positive
        double[] size = [1.0, 1.0, 1.0];
        if (args.TryGet("size", out var sizeValue))
        {
            if (ScadArguments.TryNumber(sizeValue, out var edge))
            {
                size = [edge, edge, edge];
            }
            else if (!ScadArguments.TryNumbers(sizeValue, 3, out var extents))
            {
                return Invalid(node, context, "size must be a positive number or a vector of three positive numbers");
            }
            else
            {
                size = extents!;
            }
        }

        if (size.Any(s => s <= 0.0))
        {
            return Invalid(node, context, "size must be positive");
        }

        var center = args.TryGet("center", out var centerValue) && centerValue.IsTrue;
        return Solid(node, context, 12, () => PrimitiveTessellator.Box(new Vector3D(size[0], size[1], size[2]), center));
    }

    /// <summary>
    ///     Builds a <c>sphere(r, d)</c> with the fragment count of the instance's <c>$fn</c>, <c>$fa</c> and <c>$fs</c>.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <returns>A solid, or <see cref="ScadGeo.Empty"/> when the arguments are invalid.</returns>
    public static ScadGeo Sphere(ScadInstance node, ScadGeometryContext context)
    {
        var args = Begin(node, SphereParameters, context);
        var diameter = ReadLength(args, "d", 0.5, allowZero: false);
        var radiusArgument = ReadLength(args, "r", 1.0, allowZero: false);
        if (diameter.State == LengthState.Invalid || radiusArgument.State == LengthState.Invalid)
        {
            return Invalid(node, context, "the radius and the diameter must be positive numbers");
        }

        // A diameter overrides the radius
        var radius = FirstValid(1.0, diameter, radiusArgument);
        var fragments = ScadFragmentRule.Fragments(radius, node.Fragments);
        var rings = Math.Max(2, (fragments + 1) / 2);
        return Solid(node, context, 2L * fragments * rings, () => PrimitiveTessellator.Sphere(radius, fragments));
    }

    /// <summary>
    ///     Builds a <c>cylinder(h, r1, r2, center, r, d, d1, d2)</c>: a cylinder, a cone or a frustum along +Z.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <returns>A solid, or <see cref="ScadGeo.Empty"/> when the arguments are invalid.</returns>
    public static ScadGeo Cylinder(ScadInstance node, ScadGeometryContext context)
    {
        var args = Begin(node, CylinderParameters, context);
        var heightArgument = ReadLength(args, "h", 1.0, allowZero: false);
        if (heightArgument.State == LengthState.Invalid)
        {
            return Invalid(node, context, "h must be a positive number");
        }

        // Precedence from weakest to strongest: default 1, r, d, then the per-end r1/r2, then d1/d2
        var common = ReadLength(args, "r", 1.0, allowZero: true);
        var commonDiameter = ReadLength(args, "d", 0.5, allowZero: true);
        var bottomRadius = ReadLength(args, "r1", 1.0, allowZero: true);
        var topRadius = ReadLength(args, "r2", 1.0, allowZero: true);
        var bottomDiameter = ReadLength(args, "d1", 0.5, allowZero: true);
        var topDiameter = ReadLength(args, "d2", 0.5, allowZero: true);
        if (new[] { common, commonDiameter, bottomRadius, topRadius, bottomDiameter, topDiameter }.Any(l => l.State == LengthState.Invalid))
        {
            return Invalid(node, context, "the radii and diameters must be non-negative numbers");
        }

        var height = FirstValid(1.0, heightArgument);
        var baseRadius = FirstValid(1.0, commonDiameter, common);
        var r1 = FirstValid(baseRadius, bottomDiameter, bottomRadius);
        var r2 = FirstValid(baseRadius, topDiameter, topRadius);
        if (!(r1 > 0.0) && !(r2 > 0.0))
        {
            return Invalid(node, context, "at least one radius must be greater than zero");
        }

        var centered = args.TryGet("center", out var centerValue) && centerValue.IsTrue;
        var fragments = ScadFragmentRule.Fragments(Math.Max(r1, r2), node.Fragments);
        return Solid(node, context, 4L * fragments, () => PrimitiveTessellator.Cylinder(height, r1, r2, fragments, centered));
    }

    /// <summary>
    ///     Binds the arguments of an instance of a primitive and warns when it was given child objects.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="parameters">The documented parameter names.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The bound arguments.</returns>
    private static ScadArguments Begin(ScadInstance node, string[] parameters, ScadGeometryContext context)
    {
        // A primitive has no use for children; saying so beats silently dropping them
        if (node.Children.Count > 0)
        {
            context.Reporter.Warn($"{node.Name}() takes no child objects; ignoring them", node.Location);
        }

        return ScadArguments.Bind(node, parameters, context);
    }

    /// <summary>
    ///     Reports an invalid argument as a warning and returns the empty result.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <param name="problem">What is wrong, without the module name.</param>
    /// <returns><see cref="ScadGeo.Empty"/>.</returns>
    private static ScadGeo Invalid(ScadInstance node, ScadGeometryContext context, string problem)
    {
        context.Reporter.Warn($"{node.Name}: {problem}; the instance is ignored", node.Location);
        return ScadGeo.Empty;
    }

    /// <summary>
    ///     Reads an optional length-like parameter (a radius, a diameter or a height) and scales it.
    /// </summary>
    /// <param name="args">The bound arguments.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="scale">The factor applied to a valid value (0.5 turns a diameter into a radius).</param>
    /// <param name="allowZero">Whether zero is a valid value.</param>
    /// <returns>Whether the parameter was absent, valid or invalid, with its scaled value.</returns>
    private static Length ReadLength(ScadArguments args, string name, double scale, bool allowZero)
    {
        if (!args.TryGet(name, out var raw))
        {
            return new Length(LengthState.Absent, 0.0);
        }

        if (!ScadArguments.TryNumber(raw, out var number) || number < 0.0 || (!allowZero && number <= 0.0))
        {
            return new Length(LengthState.Invalid, 0.0);
        }

        return new Length(LengthState.Valid, number * scale);
    }

    /// <summary>
    ///     Picks the value of the first valid argument, or a fallback when none was supplied.
    /// </summary>
    /// <param name="fallback">The value used when no argument is valid.</param>
    /// <param name="strongestFirst">The arguments, the one that overrides the others first.</param>
    /// <returns>The chosen value.</returns>
    private static double FirstValid(double fallback, params Length[] strongestFirst)
    {
        foreach (var length in strongestFirst)
        {
            if (length.State == LengthState.Valid)
            {
                return length.Value;
            }
        }

        return fallback;
    }

    /// <summary>
    ///     Charges the budget, tessellates a solid and wraps it in a mesh node.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <param name="triangleEstimate">The upper bound of the triangles the mesh will have.</param>
    /// <param name="make">The tessellation.</param>
    /// <returns>A solid, or <see cref="ScadGeo.Empty"/> when the tessellator rejected the input.</returns>
    /// <exception cref="ScadEvaluationException">Thrown when the triangle budget would be exceeded.</exception>
    private static ScadGeo Solid(ScadInstance node, ScadGeometryContext context, long triangleEstimate, Func<Mesh> make)
    {
        // Fail before allocating, then give cancellation a chance before the (possibly large) tessellation
        context.ChargeTriangles(triangleEstimate, node.Location);
        context.Poll();
        Mesh mesh;
        try
        {
            mesh = make();
        }
        catch (ArgumentException ex)
        {
            Rejected(node, context, ex);
            return ScadGeo.Empty;
        }

        return ScadGeo.FromSolid(context.CreateMeshNode(mesh, node.Location));
    }

    /// <summary>
    ///     Reports a Render3D rejection of an input the argument checks accepted.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <param name="exception">The rejection.</param>
    private static void Rejected(ScadInstance node, ScadGeometryContext context, ArgumentException exception) =>
        context.Reporter.Unsupported(
            ScadFeatureIds.GeometryRejected,
            $"{node.Name}: the geometry was rejected: {exception.Message}",
            node.Location);
}
