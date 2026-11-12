// Portions derived from cytoscape (MIT); see third-party/README.md
namespace DemaConsulting.CanvasNet.Mermaid.Cose;

/// <summary>
///     The points cytoscape computes for an edge after the layout, which Mermaid reads from the edge's <c>rscratch</c>
///     and draws as the edge's route.
/// </summary>
/// <remarks>
///     Mermaid's cytoscape instance keeps cytoscape's default style for the nodes (an ellipse of 30 by 30, no padding or
///     border) and gives the edges <c>curve-style: bezier</c>. A lone edge between two nodes is then straight
///     (<c>findStraightEdgePoints</c>), and its ends (<c>findEndpoints</c>, with the default <c>outside-to-node</c>
///     endpoints, no arrows and no distance from the node) are where the line between the centers crosses the circle of
///     radius 15 around each center (<c>intersectLineEllipse</c>), shortened by nothing (<c>shortenIntersection</c>).
///     The middle point is the mean of the ends and the arrow ends (<c>storeAllpts</c>), which are the same points. When
///     the centers are closer than 15, the circle has no crossing and the points are NaN (cytoscape's bad line).
/// </remarks>
internal sealed partial class CoseLayout
{
    /// <summary>The radius of cytoscape's default node ellipse (half its 30 by 30 outer size, padding 0).</summary>
    private const double DefaultNodeRadius = 30.0 / 2 + 0;

    /// <summary>
    ///     Computes cytoscape's points of a straight edge between two node centers.
    /// </summary>
    /// <param name="source">The center of the source node.</param>
    /// <param name="target">The center of the target node.</param>
    /// <returns>The start, middle and end points.</returns>
    internal static CoseEdgePoints StraightEdgePoints(CosePoint source, CosePoint target)
    {
        // The target end: the crossing of the line from the source center (plus the endpoint offset 0) with the
        // target's circle; the edge end and the arrow end are the crossing shortened by the gap and the spacing (0)
        var fromSource = new CosePoint(source.X + 0, source.Y + 0);
        var targetCrossing = IntersectLineEllipse(fromSource, target, DefaultNodeRadius, DefaultNodeRadius);
        var end = ShortenIntersection(targetCrossing, fromSource, 0 + 0);
        var arrowEnd = ShortenIntersection(targetCrossing, fromSource, 0 + 0);

        // The source end, the same way from the target center
        var fromTarget = new CosePoint(target.X + 0, target.Y + 0);
        var sourceCrossing = IntersectLineEllipse(fromTarget, source, DefaultNodeRadius, DefaultNodeRadius);
        var start = ShortenIntersection(sourceCrossing, fromTarget, 0 + 0);
        var arrowStart = ShortenIntersection(sourceCrossing, fromTarget, 0 + 0);

        // The default middle point of a straight edge
        var mid = new CosePoint(
            (start.X + end.X + arrowStart.X + arrowEnd.X) / 4,
            (start.Y + end.Y + arrowStart.Y + arrowEnd.Y) / 4);
        return new CoseEdgePoints(start, mid, end);
    }

    /// <summary>
    ///     Computes where the line from a point to an ellipse's center crosses the ellipse (cytoscape's
    ///     <c>intersectLineEllipse</c>).
    /// </summary>
    /// <param name="point">The point outside the ellipse.</param>
    /// <param name="center">The ellipse's center.</param>
    /// <param name="radiusX">The ellipse's radius in x.</param>
    /// <param name="radiusY">The ellipse's radius in y.</param>
    /// <returns>The crossing, or NaN coordinates when the point lies inside (JavaScript's empty array).</returns>
    private static CosePoint IntersectLineEllipse(CosePoint point, CosePoint center, double radiusX, double radiusY)
    {
        var dispX = (center.X - point.X) / radiusX;
        var dispY = (center.Y - point.Y) / radiusY;
        var length = Math.Sqrt((dispX * dispX) + (dispY * dispY));
        var newLength = length - 1;
        if (newLength < 0)
        {
            return new CosePoint(double.NaN, double.NaN);
        }

        var proportion = newLength / length;
        return new CosePoint(
            ((center.X - point.X) * proportion) + point.X,
            ((center.Y - point.Y) * proportion) + point.Y);
    }

    /// <summary>
    ///     Moves a crossing toward a point by an amount (cytoscape's <c>shortenIntersection</c>).
    /// </summary>
    /// <param name="intersection">The crossing.</param>
    /// <param name="offset">The point the crossing moves toward.</param>
    /// <param name="amount">The amount.</param>
    /// <returns>The shortened point (NaN when the crossing is the point itself, or NaN).</returns>
    private static CosePoint ShortenIntersection(CosePoint intersection, CosePoint offset, double amount)
    {
        var dispX = intersection.X - offset.X;
        var dispY = intersection.Y - offset.Y;
        var length = Math.Sqrt((dispX * dispX) + (dispY * dispY));
        var ratio = (length - amount) / length;
        if (ratio < 0)
        {
            ratio = 0.00001;
        }

        return new CosePoint(offset.X + (ratio * dispX), offset.Y + (ratio * dispY));
    }
}
