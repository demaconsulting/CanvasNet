// Portions derived from layout-base and cose-base (MIT); see third-party/README.md
using System.Globalization;

namespace DemaConsulting.CanvasNet.Mermaid.Cose;

/// <summary>
///     The graph model of the cose layout: the nodes, edges and bounds of layout-base's <c>LGraphManager</c>,
///     <c>LGraph</c>, <c>LNode</c> (with the fields of <c>FDLayoutNode</c> and <c>CoSENode</c>), <c>LEdge</c>,
///     <c>RectangleD</c> and <c>Transform</c>, and the geometry of <c>IGeometry</c>, as far as a flat tree uses them.
/// </summary>
internal sealed partial class CoseLayout
{
    /// <summary>layout-base's <c>Integer.MAX_VALUE</c>, the start of every minimum and maximum search.</summary>
    private const double IntegerMaxValue = 2147483647;

    /// <summary>layout-base's <c>Integer.MIN_VALUE</c>.</summary>
    private const double IntegerMinValue = -2147483648;

    /// <summary>
    ///     The margin of the root graph (<c>DEFAULT_GRAPH_MARGIN</c>): the root graph's parent node has no padding, so
    ///     <c>updateBounds</c> and <c>updateLeftTop</c> use the graph's margin.
    /// </summary>
    private const double GraphMargin = 15;

    /// <summary>The bound of the world (<c>WORLD_BOUNDARY</c>) that <c>LNode.transform</c> clamps a corner to.</summary>
    private const double WorldBoundary = 1000000;

    /// <summary>The left of the root graph (<c>LGraph.left</c>).</summary>
    private double _left;

    /// <summary>The right of the root graph (<c>LGraph.right</c>).</summary>
    private double _right;

    /// <summary>The top of the root graph (<c>LGraph.top</c>).</summary>
    private double _top;

    /// <summary>The bottom of the root graph (<c>LGraph.bottom</c>).</summary>
    private double _bottom;

    /// <summary>
    ///     Updates the bounds of the root graph from its nodes, with the margin (<c>LGraph.updateBounds</c>; the nodes
    ///     of a flat graph have no child graph to update first).
    /// </summary>
    private void UpdateBounds()
    {
        var bounds = CalculateBounds(_nodes);
        _left = bounds.X - GraphMargin;
        _right = bounds.X + bounds.Width + GraphMargin;
        _top = bounds.Y - GraphMargin;
        _bottom = bounds.Y + bounds.Height + GraphMargin;
    }

    /// <summary>
    ///     Updates the left and top of the root graph from its nodes, with the margin (<c>LGraph.updateLeftTop</c>).
    /// </summary>
    /// <returns>
    ///     Whether a corner was found; JavaScript returns null (and changes nothing) when no node's top is below
    ///     <c>Integer.MAX_VALUE</c>.
    /// </returns>
    private bool UpdateLeftTop()
    {
        var top = IntegerMaxValue;
        var left = IntegerMaxValue;
        foreach (var node in _nodes)
        {
            if (top > node.Y)
            {
                top = node.Y;
            }

            if (left > node.X)
            {
                left = node.X;
            }
        }

        if (JsEquals(top, IntegerMaxValue))
        {
            return false;
        }

        _left = left - GraphMargin;
        _top = top - GraphMargin;
        return true;
    }

    /// <summary>
    ///     Computes the bounding rectangle of nodes (<c>LGraph.calculateBounds</c>), starting from
    ///     <c>±Integer.MAX_VALUE</c> as JavaScript does.
    /// </summary>
    /// <param name="nodes">The nodes.</param>
    /// <returns>The rectangle (left, top, right - left, bottom - top).</returns>
    private static CoseRect CalculateBounds(IReadOnlyList<CoseNode> nodes)
    {
        var left = IntegerMaxValue;
        var right = -IntegerMaxValue;
        var top = IntegerMaxValue;
        var bottom = -IntegerMaxValue;
        foreach (var node in nodes)
        {
            var nodeLeft = node.X;
            var nodeRight = node.Right;
            var nodeTop = node.Y;
            var nodeBottom = node.Bottom;
            if (left > nodeLeft)
            {
                left = nodeLeft;
            }

            if (right < nodeRight)
            {
                right = nodeRight;
            }

            if (top > nodeTop)
            {
                top = nodeTop;
            }

            if (bottom < nodeBottom)
            {
                bottom = nodeBottom;
            }
        }

        return new CoseRect(left, top, right - left, bottom - top);
    }

    /// <summary>
    ///     Computes the points where the line between two rectangles' centers leaves each rectangle
    ///     (<c>IGeometry.getIntersection2</c>).
    /// </summary>
    /// <param name="a">The first rectangle.</param>
    /// <param name="b">The second rectangle.</param>
    /// <returns>
    ///     Whether the rectangles overlap (then the points are the centers), and the clipping points of the first and
    ///     second rectangle; a point JavaScript leaves unset (both centers equal without an overlap) is NaN.
    /// </returns>
    private static (bool Overlap, double Ax, double Ay, double Bx, double By) Intersection(CoseNode a, CoseNode b)
    {
        var p1x = a.CenterX;
        var p1y = a.CenterY;
        var p2x = b.CenterX;
        var p2y = b.CenterY;

        // Overlapping rectangles clip at their centers
        if (Intersects(a, b))
        {
            return (true, p1x, p1y, p2x, p2y);
        }

        // The corners and half sizes of both rectangles
        var topLeftAx = a.X;
        var topLeftAy = a.Y;
        var topRightAx = a.Right;
        var bottomLeftAx = a.X;
        var bottomLeftAy = a.Bottom;
        var bottomRightAx = a.Right;
        var halfWidthA = a.Width / 2;
        var halfHeightA = a.Height / 2;
        var topLeftBx = b.X;
        var topLeftBy = b.Y;
        var topRightBx = b.Right;
        var bottomLeftBx = b.X;
        var bottomLeftBy = b.Bottom;
        var bottomRightBx = b.Right;
        var halfWidthB = b.Width / 2;
        var halfHeightB = b.Height / 2;

        double r0 = double.NaN, r1 = double.NaN, r2 = double.NaN, r3 = double.NaN;
        var clipPointAFound = false;
        var clipPointBFound = false;

        if (JsEquals(p1x, p2x))
        {
            // A vertical line clips at the facing horizontal sides (equal centers leave the points unset)
            if (p1y > p2y)
            {
                return (false, p1x, topLeftAy, p2x, bottomLeftBy);
            }

            if (p1y < p2y)
            {
                return (false, p1x, bottomLeftAy, p2x, topLeftBy);
            }
        }
        else if (JsEquals(p1y, p2y))
        {
            // A horizontal line clips at the facing vertical sides
            if (p1x > p2x)
            {
                return (false, topLeftAx, p1y, topRightBx, p2y);
            }

            if (p1x < p2x)
            {
                return (false, topRightAx, p1y, topLeftBx, p2y);
            }
        }
        else
        {
            // The slopes of the rectangles' diagonals and of the line between the centers
            var slopeA = a.Height / a.Width;
            var slopeB = b.Height / b.Width;
            var slopePrime = (p2y - p1y) / (p2x - p1x);

            // A line along a diagonal clips at a corner of the first rectangle
            if (JsEquals(-slopeA, slopePrime))
            {
                (r0, r1) = p1x > p2x ? (bottomLeftAx, bottomLeftAy) : (topRightAx, topLeftAy);
                clipPointAFound = true;
            }
            else if (JsEquals(slopeA, slopePrime))
            {
                (r0, r1) = p1x > p2x ? (topLeftAx, topLeftAy) : (bottomRightAx, bottomLeftAy);
                clipPointAFound = true;
            }

            // ... or of the second one
            if (JsEquals(-slopeB, slopePrime))
            {
                (r2, r3) = p2x > p1x ? (bottomLeftBx, bottomLeftBy) : (topRightBx, topLeftBy);
                clipPointBFound = true;
            }
            else if (JsEquals(slopeB, slopePrime))
            {
                (r2, r3) = p2x > p1x ? (topLeftBx, topLeftBy) : (bottomRightBx, bottomLeftBy);
                clipPointBFound = true;
            }

            if (clipPointAFound && clipPointBFound)
            {
                return (false, r0, r1, r2, r3);
            }

            // The side (1 north, 2 east, 3 south, 4 west) each rectangle is left through
            int cardinalDirectionA;
            int cardinalDirectionB;
            if (p1x > p2x)
            {
                if (p1y > p2y)
                {
                    cardinalDirectionA = CardinalDirection(slopeA, slopePrime, 4);
                    cardinalDirectionB = CardinalDirection(slopeB, slopePrime, 2);
                }
                else
                {
                    cardinalDirectionA = CardinalDirection(-slopeA, slopePrime, 3);
                    cardinalDirectionB = CardinalDirection(-slopeB, slopePrime, 1);
                }
            }
            else if (p1y > p2y)
            {
                cardinalDirectionA = CardinalDirection(-slopeA, slopePrime, 1);
                cardinalDirectionB = CardinalDirection(-slopeB, slopePrime, 3);
            }
            else
            {
                cardinalDirectionA = CardinalDirection(slopeA, slopePrime, 2);
                cardinalDirectionB = CardinalDirection(slopeB, slopePrime, 4);
            }

            // The clipping points on those sides
            if (!clipPointAFound)
            {
                (r0, r1) = cardinalDirectionA switch
                {
                    1 => (p1x + (-halfHeightA / slopePrime), topLeftAy),
                    2 => (bottomRightAx, p1y + (halfWidthA * slopePrime)),
                    3 => (p1x + (halfHeightA / slopePrime), bottomLeftAy),
                    _ => (bottomLeftAx, p1y + (-halfWidthA * slopePrime)),
                };
            }

            if (!clipPointBFound)
            {
                (r2, r3) = cardinalDirectionB switch
                {
                    1 => (p2x + (-halfHeightB / slopePrime), topLeftBy),
                    2 => (bottomRightBx, p2y + (halfWidthB * slopePrime)),
                    3 => (p2x + (halfHeightB / slopePrime), bottomLeftBy),
                    _ => (bottomLeftBx, p2y + (-halfWidthB * slopePrime)),
                };
            }
        }

        return (false, r0, r1, r2, r3);
    }

    /// <summary>
    ///     Gives the side a line leaves a rectangle through (<c>IGeometry.getCardinalDirection</c>).
    /// </summary>
    /// <param name="slope">The signed slope of the rectangle's diagonal.</param>
    /// <param name="slopePrime">The slope of the line.</param>
    /// <param name="line">The side when the diagonal is steeper.</param>
    /// <returns>The side, 1 to 4.</returns>
    private static int CardinalDirection(double slope, double slopePrime, int line) =>
        slope > slopePrime ? line : 1 + (line % 4);

    /// <summary>
    ///     Whether two rectangles intersect, touching included (<c>RectangleD.intersects</c>; a NaN coordinate counts as
    ///     an intersection, as in JavaScript).
    /// </summary>
    /// <param name="a">The first rectangle.</param>
    /// <param name="b">The second rectangle.</param>
    /// <returns>Whether they intersect.</returns>
    private static bool Intersects(CoseNode a, CoseNode b) =>
        !(a.Right < b.X) && !(a.Bottom < b.Y) && !(b.Right < a.X) && !(b.Bottom < a.Y);

    /// <summary>
    ///     Computes half the amounts that separate two overlapping rectangles, plus a buffer, signed by the direction each
    ///     should move (<c>IGeometry.calcSeparationAmount</c> with <c>decideDirectionsForOverlappingNodes</c>).
    /// </summary>
    /// <param name="a">The first rectangle.</param>
    /// <param name="b">The second rectangle, which intersects the first.</param>
    /// <param name="separationBuffer">The buffer added to each half amount.</param>
    /// <returns>The amounts in x and y.</returns>
    private static (double X, double Y) SeparationAmount(CoseNode a, CoseNode b, double separationBuffer)
    {
        // The first rectangle moves left (-1) or right (1), up (-1) or down (1)
        double directionX = a.CenterX < b.CenterX ? -1 : 1;
        double directionY = a.CenterY < b.CenterY ? -1 : 1;

        // The overlap, widened when one rectangle spans the other in a direction
        var overlapX = Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X);
        var overlapY = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y);
        if (a.X <= b.X && a.Right >= b.Right)
        {
            overlapX += Math.Min(b.X - a.X, a.Right - b.Right);
        }
        else if (b.X <= a.X && b.Right >= a.Right)
        {
            overlapX += Math.Min(a.X - b.X, b.Right - a.Right);
        }

        if (a.Y <= b.Y && a.Bottom >= b.Bottom)
        {
            overlapY += Math.Min(b.Y - a.Y, a.Bottom - b.Bottom);
        }
        else if (b.Y <= a.Y && b.Bottom >= a.Bottom)
        {
            overlapY += Math.Min(a.Y - b.Y, b.Bottom - a.Bottom);
        }

        // Move along the line between the centers (at 45 degrees when the centers coincide), by the smaller amount
        var slope = Math.Abs((b.CenterY - a.CenterY) / (b.CenterX - a.CenterX));
        if (JsEquals(b.CenterY, a.CenterY) && JsEquals(b.CenterX, a.CenterX))
        {
            slope = 1.0;
        }

        var moveByY = slope * overlapX;
        var moveByX = overlapY / slope;
        if (overlapX < moveByX)
        {
            moveByX = overlapX;
        }
        else
        {
            moveByY = overlapY;
        }

        return ((-1 * directionX) * ((moveByX / 2) + separationBuffer), (-1 * directionY) * ((moveByY / 2) + separationBuffer));
    }

    /// <summary>
    ///     JavaScript's <c>parseInt</c> of a whole number (a result of <c>Math.ceil</c> or <c>Math.floor</c>): the
    ///     number goes through its string, so a magnitude of 1e21 or more (written with an exponent) keeps only its first
    ///     digit, and NaN and the infinities give NaN.
    /// </summary>
    /// <param name="value">The whole number.</param>
    /// <returns>The parsed number.</returns>
    internal static double ParseInt(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return double.NaN;
        }

        // Below 1e21 the string is the plain integer (-0 is "0")
        if (Math.Abs(value) < 1e21)
        {
            return JsEquals(value, 0) ? 0 : value;
        }

        // The string is a digit, then a point and digits or not, then "e+" and the exponent: parseInt stops after the
        // first digit of the shortest representation
        var digit = Math.Abs(value).ToString("R", CultureInfo.InvariantCulture)[0] - '0';
        return value < 0 ? -digit : digit;
    }

    /// <summary>
    ///     A rectangle (layout-base's <c>RectangleD</c>).
    /// </summary>
    /// <param name="X">The left.</param>
    /// <param name="Y">The top.</param>
    /// <param name="Width">The width.</param>
    /// <param name="Height">The height.</param>
    private readonly record struct CoseRect(double X, double Y, double Width, double Height);

    /// <summary>
    ///     A transformation between the layout's world and a device (layout-base's <c>Transform</c>, whose extents are
    ///     always 1, so the inverse transformation is a translation).
    /// </summary>
    /// <param name="WorldX">The world origin's x.</param>
    /// <param name="WorldY">The world origin's y.</param>
    /// <param name="DeviceX">The device origin's x.</param>
    /// <param name="DeviceY">The device origin's y.</param>
    private readonly record struct LayoutTransform(double WorldX, double WorldY, double DeviceX, double DeviceY)
    {
        /// <summary>
        ///     Maps a device x to the world (<c>inverseTransformX</c>; multiplying and dividing by the extents of 1 is
        ///     exact).
        /// </summary>
        /// <param name="x">The device x.</param>
        /// <returns>The world x.</returns>
        public double InverseX(double x) => WorldX + (x - DeviceX);

        /// <summary>
        ///     Maps a device y to the world (<c>inverseTransformY</c>).
        /// </summary>
        /// <param name="y">The device y.</param>
        /// <returns>The world y.</returns>
        public double InverseY(double y) => WorldY + (y - DeviceY);
    }

    /// <summary>
    ///     A node of the layout: layout-base's <c>LNode</c> rectangle and incidence list, <c>FDLayoutNode</c>'s forces and
    ///     grid cells, and <c>CoSENode</c>'s move.
    /// </summary>
    private sealed class CoseNode
    {
        /// <summary>
        ///     Initializes a node.
        /// </summary>
        /// <param name="index">The index of the node in the input.</param>
        public CoseNode(int index) => Index = index;

        /// <summary>Gets the index of the node in the input.</summary>
        public int Index { get; }

        /// <summary>Gets or sets the left of the rectangle (<c>rect.x</c>).</summary>
        public double X { get; set; }

        /// <summary>Gets or sets the top of the rectangle (<c>rect.y</c>).</summary>
        public double Y { get; set; }

        /// <summary>Gets or sets the width of the rectangle.</summary>
        public double Width { get; set; }

        /// <summary>Gets or sets the height of the rectangle.</summary>
        public double Height { get; set; }

        /// <summary>Gets the incident edges, in the order they were added (<c>LNode.edges</c>).</summary>
        public List<CoseEdge> Edges { get; } = [];

        /// <summary>Gets or sets the spring force in x.</summary>
        public double SpringForceX { get; set; }

        /// <summary>Gets or sets the spring force in y.</summary>
        public double SpringForceY { get; set; }

        /// <summary>Gets or sets the repulsion force in x.</summary>
        public double RepulsionForceX { get; set; }

        /// <summary>Gets or sets the repulsion force in y.</summary>
        public double RepulsionForceY { get; set; }

        /// <summary>
        ///     Gets or sets the first grid column the node lies in (<c>startX</c>, a JavaScript number; NaN, as JavaScript's
        ///     <c>undefined</c> counts, until a grid holds the node).
        /// </summary>
        public double StartX { get; set; } = double.NaN;

        /// <summary>Gets or sets the last grid column the node lies in (<c>finishX</c>).</summary>
        public double FinishX { get; set; } = double.NaN;

        /// <summary>Gets or sets the first grid row the node lies in (<c>startY</c>).</summary>
        public double StartY { get; set; } = double.NaN;

        /// <summary>Gets or sets the last grid row the node lies in (<c>finishY</c>).</summary>
        public double FinishY { get; set; } = double.NaN;

        /// <summary>Gets or sets the nodes near enough to repel this one, in the order found (<c>surrounding</c>).</summary>
        public List<CoseNode> Surrounding { get; set; } = [];

        /// <summary>Gets the right of the rectangle.</summary>
        public double Right => X + Width;

        /// <summary>Gets the bottom of the rectangle.</summary>
        public double Bottom => Y + Height;

        /// <summary>Gets the center's x (<c>getCenterX</c>).</summary>
        public double CenterX => X + (Width / 2);

        /// <summary>Gets the center's y (<c>getCenterY</c>).</summary>
        public double CenterY => Y + (Height / 2);

        /// <summary>
        ///     Puts the center at a point (<c>setCenter</c>).
        /// </summary>
        /// <param name="cx">The center's x.</param>
        /// <param name="cy">The center's y.</param>
        public void SetCenter(double cx, double cy)
        {
            X = cx - (Width / 2);
            Y = cy - (Height / 2);
        }

        /// <summary>
        ///     Moves the rectangle by an amount (<c>moveBy</c>).
        /// </summary>
        /// <param name="dx">The amount in x.</param>
        /// <param name="dy">The amount in y.</param>
        public void MoveBy(double dx, double dy)
        {
            X += dx;
            Y += dy;
        }

        /// <summary>
        ///     Moves the rectangle's corner through the inverse of a transformation, after clamping it to the world
        ///     (<c>LNode.transform</c>).
        /// </summary>
        /// <param name="transform">The transformation.</param>
        public void Transform(LayoutTransform transform)
        {
            var left = X;
            if (left > WorldBoundary)
            {
                left = WorldBoundary;
            }
            else if (left < -WorldBoundary)
            {
                left = -WorldBoundary;
            }

            var top = Y;
            if (top > WorldBoundary)
            {
                top = WorldBoundary;
            }
            else if (top < -WorldBoundary)
            {
                top = -WorldBoundary;
            }

            X = transform.InverseX(left);
            Y = transform.InverseY(top);
        }

        /// <summary>
        ///     Gets the node at the other end of an incident edge (<c>LEdge.getOtherEnd</c>).
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <returns>The other end.</returns>
        public CoseNode OtherEnd(CoseEdge edge) => edge.Source == this ? edge.Target : edge.Source;
    }

    /// <summary>
    ///     An edge of the layout (layout-base's <c>LEdge</c> with <c>FDLayoutEdge</c>'s ideal length, which is the ideal
    ///     edge length of 50 for a flat graph).
    /// </summary>
    /// <param name="source">The source node.</param>
    /// <param name="target">The target node.</param>
    private sealed class CoseEdge(CoseNode source, CoseNode target)
    {
        /// <summary>Gets the source node.</summary>
        public CoseNode Source { get; } = source;

        /// <summary>Gets the target node.</summary>
        public CoseNode Target { get; } = target;

        /// <summary>Gets or sets the length in x between the clipping points (<c>lengthX</c>).</summary>
        public double LengthX { get; set; }

        /// <summary>Gets or sets the length in y between the clipping points (<c>lengthY</c>).</summary>
        public double LengthY { get; set; }

        /// <summary>Gets or sets the length between the clipping points (<c>length</c>).</summary>
        public double Length { get; set; }

        /// <summary>Gets or sets whether the end nodes overlap (the flag <c>updateLength</c> sets).</summary>
        public bool IsOverlapping { get; set; }

        /// <summary>
        ///     Updates the lengths between the clipping points of the end nodes (<c>LEdge.updateLength</c>); a length
        ///     below 1 in magnitude becomes its sign.
        /// </summary>
        public void UpdateLength()
        {
            var clip = Intersection(Target, Source);
            IsOverlapping = clip.Overlap;
            if (!IsOverlapping)
            {
                LengthX = clip.Ax - clip.Bx;
                LengthY = clip.Ay - clip.By;
                if (Math.Abs(LengthX) < 1.0)
                {
                    LengthX = Sign(LengthX);
                }

                if (Math.Abs(LengthY) < 1.0)
                {
                    LengthY = Sign(LengthY);
                }

                Length = Math.Sqrt((LengthX * LengthX) + (LengthY * LengthY));
            }
        }
    }

    /// <summary>
    ///     JavaScript's strict equality of two numbers (<c>===</c>): false when either is NaN, and -0 equals 0.
    /// </summary>
    /// <param name="a">The first number.</param>
    /// <param name="b">The second number.</param>
    /// <returns>Whether they are equal.</returns>
    internal static bool JsEquals(double a, double b) => !double.IsNaN(a) && !double.IsNaN(b) && a.CompareTo(b) == 0;

    /// <summary>
    ///     The sign of a value, 0 for zero and NaN (<c>IMath.sign</c>).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>1, -1 or 0.</returns>
    private static double Sign(double value)
    {
        if (value > 0)
        {
            return 1;
        }

        return value < 0 ? -1 : 0;
    }
}
