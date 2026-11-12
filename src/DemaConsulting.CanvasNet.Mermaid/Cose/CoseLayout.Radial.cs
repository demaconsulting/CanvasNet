// Portions derived from layout-base and cose-base (MIT); see third-party/README.md
using System.Runtime.CompilerServices;
using DemaConsulting.CanvasNet.Mermaid.Dagre;

namespace DemaConsulting.CanvasNet.Mermaid.Cose;

/// <summary>
///     The initial placement of the cose layout: layout-base's <c>getFlatForest</c> and <c>findCenterOfTree</c>, and
///     cose-base's <c>positionNodesRadially</c>, <c>radialLayout</c>, <c>branchRadialLayout</c> and
///     <c>maxDiagonalInTree</c>, for one tree.
/// </summary>
internal sealed partial class CoseLayout
{
    /// <summary>The coordinates of the world's center (<c>WORLD_CENTER_X</c>, <c>WORLD_CENTER_Y</c>).</summary>
    private const double WorldCenterX = 1200;

    /// <summary>The y coordinate of the world's center.</summary>
    private const double WorldCenterY = 900;

    /// <summary>The smallest distance between the rings of the radial layout (<c>DEFAULT_RADIAL_SEPARATION</c>).</summary>
    private const double DefaultRadialSeparation = IdealEdgeLength;

    /// <summary>
    ///     Finds the tree of the graph in breadth-first order from the first node (<c>getFlatForest</c>; the graph is one
    ///     tree, so the forest has one tree and its check for a cycle never fires).
    /// </summary>
    /// <returns>The nodes in the order the search visits them.</returns>
    private List<CoseNode> FlatForest()
    {
        // A node is visited when it leaves the queue; its neighbors other than its parent join the queue
        var visited = new List<CoseNode>(_nodes.Count);
        var parents = new Dictionary<CoseNode, CoseNode>();
        var toBeVisited = new Queue<CoseNode>();
        toBeVisited.Enqueue(_nodes[0]);
        while (toBeVisited.Count > 0)
        {
            var current = toBeVisited.Dequeue();
            visited.Add(current);
            _meter.Charge(1 + current.Edges.Count);
            foreach (var edge in current.Edges)
            {
                var neighbor = current.OtherEnd(edge);
                if (!parents.TryGetValue(current, out var parent) || parent != neighbor)
                {
                    toBeVisited.Enqueue(neighbor);
                    parents[neighbor] = current;
                }
            }
        }

        return visited;
    }

    /// <summary>
    ///     Finds the node the radial layout starts from (<c>Layout.findCenterOfTree</c>).
    /// </summary>
    /// <remarks>
    ///     The original removes nodes from its list while it walks the list with an index, so each pass removes every
    ///     other remaining node (the first, third, fifth, ...) rather than the leaves, until one or two nodes remain;
    ///     the first of them is the center. The remaining degrees it also keeps never influence that list, so they are
    ///     not computed.
    /// </remarks>
    /// <typeparam name="T">The node type.</typeparam>
    /// <param name="tree">The nodes of the tree, in the forest's order; not empty.</param>
    /// <param name="meter">The work meter.</param>
    /// <returns>The center node.</returns>
    internal static T FindCenterOfTree<T>(IReadOnlyList<T> tree, LayoutWorkMeter meter)
    {
        var list = new List<T>(tree);
        while (list.Count > 2)
        {
            // Removing the element at the walking index and then stepping the index skips every other element: the
            // pass keeps the elements at odd positions
            meter.Charge(list.Count);
            var kept = new List<T>((list.Count / 2) + 1);
            for (var i = 1; i < list.Count; i += 2)
            {
                kept.Add(list[i]);
            }

            list = kept;
        }

        return list[0];
    }

    /// <summary>
    ///     Places the tree radially around its center and moves it so the drawing is centered on the world's center
    ///     (<c>positionNodesRadially</c> with one tree, at the starting point (0, 0)).
    /// </summary>
    /// <param name="tree">The nodes of the tree, in the forest's order.</param>
    private void PositionNodesRadially(List<CoseNode> tree)
    {
        var center = FindCenterOfTree(tree, _meter);
        var (pointX, pointY) = RadialLayout(tree, center);
        Transform(WorldCenterX - (pointX / 2), WorldCenterY - (pointY / 2));
    }

    /// <summary>
    ///     Places a tree on rings around its center and moves its top-left corner to the origin (<c>radialLayout</c> with
    ///     the starting point (0, 0)).
    /// </summary>
    /// <param name="tree">The nodes of the tree.</param>
    /// <param name="center">The center node.</param>
    /// <returns>The bottom-right corner of the tree after the move.</returns>
    private (double X, double Y) RadialLayout(List<CoseNode> tree, CoseNode center)
    {
        // The rings are as far apart as the largest node diagonal, at least the default separation
        var radialSeparation = Math.Max(MaxDiagonalInTree(tree), DefaultRadialSeparation);
        BranchRadialLayout(center, null, 0, 359, 0, radialSeparation);

        // Move the bounds' top-left corner to the starting point
        var bounds = CalculateBounds(tree);
        var transform = new LayoutTransform(0, 0, bounds.X, bounds.Y);
        foreach (var node in tree)
        {
            node.Transform(transform);
        }

        return (transform.InverseX(bounds.X + bounds.Width), transform.InverseY(bounds.Y + bounds.Height));
    }

    /// <summary>
    ///     Places a node on its ring at the middle of its angle interval and its children in equal parts of the interval,
    ///     on the next ring (<c>branchRadialLayout</c>).
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="parentOfNode">The node it was reached from, or null for the center.</param>
    /// <param name="startAngle">The start of the node's interval, in degrees.</param>
    /// <param name="endAngle">The end of the node's interval, in degrees.</param>
    /// <param name="distance">The radius of the node's ring.</param>
    /// <param name="radialSeparation">The distance between rings.</param>
    private void BranchRadialLayout(
        CoseNode node,
        CoseNode? parentOfNode,
        double startAngle,
        double endAngle,
        double distance,
        double radialSeparation)
    {
        // The recursion is as deep as the tree is high around its center
        RuntimeHelpers.EnsureSufficientExecutionStack();
        _meter.Charge(1 + node.Edges.Count);

        // The node's angle is the middle of its interval
        var halfInterval = (endAngle - startAngle + 1) / 2;
        if (halfInterval < 0)
        {
            halfInterval += 180;
        }

        var nodeAngle = (halfInterval + startAngle) % 360;
        var theta = nodeAngle * (2.0 * Math.PI) / 360;

        // Polar to Cartesian coordinates
        var x = distance * MeteredCos(theta);
        var y = distance * MeteredSin(theta);
        node.SetCenter(x, y);

        // The children follow the edge to the parent in the node's edge order, wrapping around (a tree has one edge
        // to the parent, so the original's pruning of parallel edges never runs)
        var neighborEdges = node.Edges;
        var incEdgesCount = neighborEdges.Count;
        var childCount = parentOfNode == null ? incEdgesCount : incEdgesCount - 1;
        var startIndex = 0;
        if (parentOfNode != null)
        {
            var parentEdge = neighborEdges.Find(edge => edge.Source == parentOfNode || edge.Target == parentOfNode)!;
            startIndex = (neighborEdges.IndexOf(parentEdge) + 1) % incEdgesCount;
        }

        var stepAngle = Math.Abs(endAngle - startAngle) / childCount;
        var branchCount = 0;
        var i = startIndex;
        while (branchCount != childCount)
        {
            var currentNeighbor = node.OtherEnd(neighborEdges[i]);
            if (currentNeighbor != parentOfNode)
            {
                var childStartAngle = (startAngle + (branchCount * stepAngle)) % 360;
                var childEndAngle = (childStartAngle + stepAngle) % 360;
                BranchRadialLayout(currentNeighbor, node, childStartAngle, childEndAngle, distance + radialSeparation, radialSeparation);
                branchCount++;
            }

            i = (i + 1) % incEdgesCount;
        }
    }

    /// <summary>
    ///     Gets the largest node diagonal of a tree (<c>maxDiagonalInTree</c>), starting from <c>Integer.MIN_VALUE</c>.
    /// </summary>
    /// <param name="tree">The nodes of the tree.</param>
    /// <returns>The largest diagonal.</returns>
    private static double MaxDiagonalInTree(List<CoseNode> tree)
    {
        var maxDiagonal = IntegerMinValue;
        foreach (var node in tree)
        {
            var diagonal = Math.Sqrt((node.Width * node.Width) + (node.Height * node.Height));
            if (diagonal > maxDiagonal)
            {
                maxDiagonal = diagonal;
            }
        }

        return maxDiagonal;
    }

    /// <summary>
    ///     Computes a correctly rounded cosine and charges it.
    /// </summary>
    /// <param name="x">The angle in radians.</param>
    /// <returns>The cosine.</returns>
    /// <exception cref="MermaidRenderException">Thrown when the argument is outside the function's domain.</exception>
    private double MeteredCos(double x)
    {
        // An argument outside the function's domain (which no layout of a tree makes) fails the render
        double result;
        int steps;
        try
        {
            result = Cos(x, out steps);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw _meter.Failure(ex.Message);
        }

        _meter.Charge(1 + steps);
        return result;
    }

    /// <summary>
    ///     Computes a correctly rounded sine and charges it.
    /// </summary>
    /// <param name="x">The angle in radians.</param>
    /// <returns>The sine.</returns>
    /// <exception cref="MermaidRenderException">Thrown when the argument is outside the function's domain.</exception>
    private double MeteredSin(double x)
    {
        // An argument outside the function's domain (which no layout of a tree makes) fails the render
        double result;
        int steps;
        try
        {
            result = Sin(x, out steps);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw _meter.Failure(ex.Message);
        }

        _meter.Charge(1 + steps);
        return result;
    }
}
