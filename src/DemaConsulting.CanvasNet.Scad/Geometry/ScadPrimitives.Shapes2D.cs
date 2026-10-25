using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <content>
///     The 2D shapes: <c>square</c>, <c>circle</c> and <c>polygon</c>.
/// </content>
internal static partial class ScadPrimitives
{
    /// <summary>The parameter names of <c>square</c> in positional order.</summary>
    private static readonly string[] SquareParameters = ["size", "center"];

    /// <summary>The parameter names of <c>circle</c> in positional order.</summary>
    private static readonly string[] CircleParameters = ["r", "d"];

    /// <summary>The parameter names of <c>polygon</c> in positional order.</summary>
    private static readonly string[] PolygonParameters = ["points", "paths", "convexity"];

    /// <summary>The number of triangles of a slab made of a region, per region vertex (walls plus both caps).</summary>
    public const long SlabTrianglesPerVertex = 4;

    /// <summary>
    ///     Builds a <c>square(size, center)</c>.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <returns>A flat shape, or <see cref="ScadGeo.Empty"/> when the arguments are invalid.</returns>
    public static ScadGeo Square(ScadInstance node, ScadGeometryContext context)
    {
        var args = Begin(node, SquareParameters, context);

        // The size is a number (a square) or a vector of two numbers; every extent must be positive
        double[] size = [1.0, 1.0];
        if (args.TryGet("size", out var sizeValue))
        {
            if (ScadArguments.TryNumber(sizeValue, out var edge))
            {
                size = [edge, edge];
            }
            else if (!ScadArguments.TryNumbers(sizeValue, 2, out var extents))
            {
                return Invalid(node, context, "size must be a positive number or a vector of two positive numbers");
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
        return Flat(node, context, 4, () => PrimitiveTessellator.Rectangle(new Vector2D(size[0], size[1]), center));
    }

    /// <summary>
    ///     Builds a <c>circle(r, d)</c> with the fragment count of the instance's <c>$fn</c>, <c>$fa</c> and <c>$fs</c>.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <returns>A flat shape, or <see cref="ScadGeo.Empty"/> when the arguments are invalid.</returns>
    public static ScadGeo Circle(ScadInstance node, ScadGeometryContext context)
    {
        var args = Begin(node, CircleParameters, context);
        var diameter = ReadLength(args, "d", 0.5, allowZero: false);
        var radiusArgument = ReadLength(args, "r", 1.0, allowZero: false);
        if (diameter.State == LengthState.Invalid || radiusArgument.State == LengthState.Invalid)
        {
            return Invalid(node, context, "the radius and the diameter must be positive numbers");
        }

        // A diameter overrides the radius
        var radius = FirstValid(1.0, diameter, radiusArgument);
        var fragments = ScadFragmentRule.Fragments(radius, node.Fragments);
        return Flat(node, context, fragments, () => PrimitiveTessellator.Circle(radius, fragments));
    }

    /// <summary>
    ///     Builds a <c>polygon(points, paths, convexity)</c>; the paths may describe holes and islands.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <returns>A flat shape, or <see cref="ScadGeo.Empty"/> when the arguments or the polygon are invalid.</returns>
    public static ScadGeo Polygon(ScadInstance node, ScadGeometryContext context)
    {
        var args = Begin(node, PolygonParameters, context);
        if (!args.TryGet("points", out var pointsValue))
        {
            return Invalid(node, context, "points is required");
        }

        // Points: a list of [x, y] pairs of finite numbers, at least three
        var points = ReadPoints2D(pointsValue);
        if (points is null || points.Length < 3)
        {
            return Invalid(node, context, "points must be a list of at least three [x, y] pairs of finite numbers");
        }

        // Paths: lists of point indices; absent means one contour through all the points in order
        IReadOnlyList<IReadOnlyList<int>>? paths = null;
        if (args.TryGet("paths", out var pathsValue))
        {
            paths = ReadPaths(pathsValue, points.Length);
            if (paths is null || paths.Count == 0)
            {
                return Invalid(node, context, "paths must be a non-empty list of lists of valid point indices");
            }
        }

        return Flat(node, context, points.Length, () => PrimitiveTessellator.Polygon(points, paths), ScadFeatureIds.PolygonInvalid);
    }

    /// <summary>
    ///     Reads a list of 2D points.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The points, or <see langword="null"/> when the value is not a list of [x, y] pairs of finite numbers.</returns>
    private static Vector2D[]? ReadPoints2D(ScadValue value)
    {
        if (value.Kind != ScadValueKind.Vector)
        {
            return null;
        }

        var list = value.AsVector;
        var points = new Vector2D[list.Count];
        for (var i = 0; i < points.Length; i++)
        {
            if (!ScadArguments.TryNumbers(list[i], 2, out var pair))
            {
                return null;
            }

            points[i] = new Vector2D(pair![0], pair[1]);
        }

        return points;
    }

    /// <summary>
    ///     Reads a list of index lists.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="pointCount">The number of points the indices refer to.</param>
    /// <returns>The paths, or <see langword="null"/> when the value is not a list of lists of in-range whole numbers.</returns>
    private static IReadOnlyList<IReadOnlyList<int>>? ReadPaths(ScadValue value, int pointCount)
    {
        if (value.Kind != ScadValueKind.Vector)
        {
            return null;
        }

        var list = value.AsVector;
        var paths = new int[list.Count][];
        for (var i = 0; i < paths.Length; i++)
        {
            var path = ReadIndices(list[i], pointCount);
            if (path is null)
            {
                return null;
            }

            paths[i] = path;
        }

        return paths;
    }

    /// <summary>
    ///     Reads a list of point indices.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="count">The exclusive upper bound of an index.</param>
    /// <returns>The indices, or <see langword="null"/> when the value is not a list of whole numbers below <paramref name="count"/>.</returns>
    private static int[]? ReadIndices(ScadValue value, int count)
    {
        if (value.Kind != ScadValueKind.Vector)
        {
            return null;
        }

        var list = value.AsVector;
        var indices = new int[list.Count];
        for (var i = 0; i < indices.Length; i++)
        {
            if (!ScadArguments.TryIndex(list[i], out indices[i]) || indices[i] >= count)
            {
                return null;
            }
        }

        return indices;
    }

    /// <summary>
    ///     Builds a 2D region and wraps it as a flat result.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <param name="vertexEstimate">The upper bound of the region's vertices, used to refuse a hostile fragment count before building.</param>
    /// <param name="make">The region construction.</param>
    /// <param name="rejectionFeature">The feature reported when the region is rejected, or <see langword="null"/> for the generic rejection.</param>
    /// <returns>A flat shape, or <see cref="ScadGeo.Empty"/> when the region was rejected.</returns>
    private static ScadGeo Flat(
        ScadInstance node,
        ScadGeometryContext context,
        int vertexEstimate,
        Func<PolygonRegion> make,
        string? rejectionFeature = null)
    {
        // The slab this region may become must fit the budget; the charge itself happens when the slab is made
        context.EnsureTriangles(SlabTrianglesPerVertex * vertexEstimate, node.Location);
        context.Poll();
        PolygonRegion region;
        try
        {
            region = make();
        }
        catch (ArgumentException ex)
        {
            context.Reporter.Unsupported(
                rejectionFeature ?? ScadFeatureIds.GeometryRejected,
                $"{node.Name}: the shape was rejected: {ex.Message}",
                node.Location);
            return ScadGeo.Empty;
        }

        return region.IsEmpty ? ScadGeo.Empty : ScadGeo.FromRegion(region, node.Location);
    }
}
