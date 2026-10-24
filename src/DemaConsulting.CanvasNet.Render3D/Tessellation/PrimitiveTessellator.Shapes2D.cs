using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.Tessellation;

/// <summary>
///     Two-dimensional shapes of <see cref="PrimitiveTessellator"/>.
/// </summary>
public static partial class PrimitiveTessellator
{
    /// <summary>
    ///     Creates an axis-aligned rectangle as a region.
    /// </summary>
    /// <param name="size">The edge lengths along X and Y; every component must be finite and greater than zero.</param>
    /// <param name="center">
    ///     <see langword="true"/> to center the rectangle on the origin; <see langword="false"/> (default) to span from the
    ///     origin to <paramref name="size"/>.
    /// </param>
    /// <returns>The region with the corners <c>(0,0) (sx,0) (sx,sy) (0,sy)</c> (counter-clockwise), shifted when centered.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a size component is not finite or not greater than zero.</exception>
    public static PolygonRegion Rectangle(Vector2D size, bool center = false)
    {
        RequirePositiveFinite(size.X, nameof(size));
        RequirePositiveFinite(size.Y, nameof(size));

        var min = center ? size * -0.5 : Vector2D.Zero;
        Vector2D[] corners =
        [
            min,
            new(min.X + size.X, min.Y),
            new(min.X + size.X, min.Y + size.Y),
            new(min.X, min.Y + size.Y),
        ];
        return new PolygonRegion([corners]);
    }

    /// <summary>
    ///     Creates an ellipse centered on the origin as a regular-angle polygon.
    /// </summary>
    /// <remarks>
    ///     Vertex <c>j</c> is <c>(radiusX cos(theta_j), radiusY sin(theta_j))</c> with
    ///     <c>theta_j = 2 pi j / segments</c> (counter-clockwise, starting on the +X axis), the same
    ///     ring as <see cref="Cylinder"/>, so a linear extrusion of a circle equals a cylinder. The polygon is
    ///     inscribed, so its area is <c>n/2 radiusX radiusY sin(2 pi / n)</c>, slightly below the ellipse.
    /// </remarks>
    /// <param name="radiusX">The radius along X; finite and greater than zero.</param>
    /// <param name="radiusY">The radius along Y; finite and greater than zero.</param>
    /// <param name="segments">The number of vertices (at least 3).</param>
    /// <returns>The region.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a radius is not finite and positive or <paramref name="segments"/> is below 3.</exception>
    public static PolygonRegion Ellipse(double radiusX, double radiusY, int segments)
    {
        RequirePositiveFinite(radiusX, nameof(radiusX));
        RequirePositiveFinite(radiusY, nameof(radiusY));
        RequireSegments(segments);

        var points = new Vector2D[segments];
        for (var j = 0; j < segments; j++)
        {
            var (sin, cos) = Math.SinCos(2.0 * Math.PI * j / segments);
            points[j] = new Vector2D(radiusX * cos, radiusY * sin);
        }

        return new PolygonRegion([points]);
    }

    /// <summary>
    ///     Creates a circle centered on the origin as a regular polygon; equal to
    ///     <see cref="Ellipse"/> with two equal radii.
    /// </summary>
    /// <param name="radius">The radius; finite and greater than zero.</param>
    /// <param name="segments">The number of vertices (at least 3).</param>
    /// <returns>The region.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="radius"/> is not finite and positive or <paramref name="segments"/> is below 3.</exception>
    public static PolygonRegion Circle(double radius, int segments) => Ellipse(radius, radius, segments);

    /// <summary>
    ///     Creates a region from a point list and optional paths (index lists), like a polygon with holes.
    /// </summary>
    /// <remarks>
    ///     Which contours are outer boundaries and which are holes follows from their nesting (see
    ///     <see cref="PolygonRegion"/>), not from the order of the paths or their winding. Invalid polygons
    ///     (self-intersecting, touching, zero area, non-finite) are rejected by <see cref="PolygonRegion"/>.
    /// </remarks>
    /// <param name="points">The point list; every point must be finite.</param>
    /// <param name="paths">
    ///     The contours as lists of indices into <paramref name="points"/>; <see langword="null"/> (default) means one
    ///     contour made of all points in order.
    /// </param>
    /// <returns>The region.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> or a path is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a path index does not address a point.</exception>
    /// <exception cref="ArgumentException">Thrown when the resulting polygon is invalid (see <see cref="PolygonRegion"/>).</exception>
    public static PolygonRegion Polygon(IReadOnlyList<Vector2D> points, IReadOnlyList<IReadOnlyList<int>>? paths = null)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (paths is null)
        {
            return new PolygonRegion([points]);
        }

        // Resolve each path's indices to points, range-checking before any polygon validation
        var contours = new List<IReadOnlyList<Vector2D>>(paths.Count);
        foreach (var path in paths)
        {
            if (path is null)
            {
                throw new ArgumentNullException(nameof(paths), "A path must not be null.");
            }

            var contour = new Vector2D[path.Count];
            for (var i = 0; i < contour.Length; i++)
            {
                if ((uint)path[i] >= (uint)points.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(paths), path[i], "A path index does not address a point.");
                }

                contour[i] = points[path[i]];
            }

            contours.Add(contour);
        }

        return new PolygonRegion(contours);
    }
}
