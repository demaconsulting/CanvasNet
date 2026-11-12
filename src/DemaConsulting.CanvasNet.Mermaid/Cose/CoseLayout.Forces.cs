// Portions derived from layout-base and cose-base (MIT); see third-party/README.md
namespace DemaConsulting.CanvasNet.Mermaid.Cose;

/// <summary>
///     The spring embedder of the cose layout: layout-base's <c>FDLayout</c> (springs, the grid variant of the
///     repulsion, moves and convergence) with cose-base's <c>CoSELayout</c> iteration, cooling schedule and
///     <c>CoSENode.move</c>, for a flat tree with cose-bilkent's options (<c>quality: 'proof'</c>, not incremental).
/// </summary>
internal sealed partial class CoseLayout
{
    /// <summary>The ideal edge length (<c>idealEdgeLength</c>, cose-bilkent's default 50).</summary>
    private const double IdealEdgeLength = 50;

    /// <summary>The spring constant (<c>edgeElasticity</c>, 0.45).</summary>
    private const double SpringConstant = 0.45;

    /// <summary>The repulsion constant (<c>nodeRepulsion</c>, 4500).</summary>
    private const double RepulsionConstant = 4500.0;

    /// <summary>
    ///     The weight of every node (<c>noOfChildren</c>): 1 for a node without a child graph, which is every node of a
    ///     flat graph.
    /// </summary>
    private const double NoOfChildren = 1;

    /// <summary>
    ///     The gravitation force on every node: a connected graph has no node that gravity applies to
    ///     (<c>calculateNodesToApplyGravitationTo</c>), so it stays 0.
    /// </summary>
    private const double GravitationForce = 0;

    /// <summary>The iteration limit before it is raised for large graphs (<c>numIter</c>, 2500).</summary>
    private const double DefaultMaxIterations = 2500;

    /// <summary>The period of the convergence check and cooling (<c>CONVERGENCE_CHECK_PERIOD</c>).</summary>
    private const int ConvergenceCheckPeriod = 100;

    /// <summary>The period of the grid rebuild (<c>GRID_CALCULATION_CHECK_PERIOD</c>).</summary>
    private const int GridCalculationCheckPeriod = 10;

    /// <summary>The smallest repulsion distance in each direction (<c>MIN_REPULSION_DIST</c>, the edge length / 10).</summary>
    private const double MinRepulsionDistance = 50 / 10.0;

    /// <summary>The largest move of a node (<c>MAX_NODE_DISPLACEMENT</c>, three times the incremental 100).</summary>
    private const double MaxNodeDisplacement = 100.0 * 3;

    /// <summary>The node count above which the cooling starts lower (<c>ADAPTATION_LOWER_NODE_LIMIT</c>).</summary>
    private const double AdaptationLowerNodeLimit = 1000;

    /// <summary>The node count at which the adapted cooling reaches its floor (<c>ADAPTATION_UPPER_NODE_LIMIT</c>).</summary>
    private const double AdaptationUpperNodeLimit = 5000;

    /// <summary>The floor of the adapted initial cooling factor (<c>COOLING_ADAPTATION_FACTOR</c>).</summary>
    private const double CoolingAdaptationFactor = 0.33;

    /// <summary>
    ///     The repulsion range and grid cell size (<c>calcRepulsionRange</c>: 2·(level + 1)·idealEdgeLength with level
    ///     0).
    /// </summary>
    private const double RepulsionRange = 2 * (0 + 1) * IdealEdgeLength;

    /// <summary>The cooling adjuster of the proof quality (<c>coolingAdjuster</c>, never changed at quality 2).</summary>
    private const double CoolingAdjuster = 1;

    /// <summary>The largest length a JavaScript array can have (2^32 - 1).</summary>
    private const double MaxArrayLength = 4294967295;

    /// <summary>The cells of the repulsion grid that hold nodes, by column and row (JavaScript's dense arrays).</summary>
    private readonly Dictionary<(long Column, long Row), List<CoseNode>> _grid = [];

    /// <summary>The iterations so far (<c>totalIterations</c>).</summary>
    private int _totalIterations;

    /// <summary>The iteration limit (<c>maxIterations</c>).</summary>
    private double _maxIterations = DefaultMaxIterations;

    /// <summary>The number of cooling cycles (<c>maxCoolingCycle</c>, fixed before the limit is raised: 25).</summary>
    private double _maxCoolingCycle;

    /// <summary>The final temperature (<c>finalTemperature</c>, fixed before the limit is raised: 0.04).</summary>
    private double _finalTemperature;

    /// <summary>The cooling cycles so far (<c>coolingCycle</c>).</summary>
    private double _coolingCycle;

    /// <summary>The cooling factor (<c>coolingFactor</c>).</summary>
    private double _coolingFactor;

    /// <summary>The initial cooling factor (<c>initialCoolingFactor</c>).</summary>
    private double _initialCoolingFactor;

    /// <summary>The total displacement of the current iteration (<c>totalDisplacement</c>).</summary>
    private double _totalDisplacement;

    /// <summary>The total displacement of the previous convergence check (<c>oldTotalDisplacement</c>).</summary>
    private double _oldTotalDisplacement;

    /// <summary>The displacement below which the layout has converged (<c>totalDisplacementThreshold</c>).</summary>
    private double _totalDisplacementThreshold;

    /// <summary>The number of grid columns (<c>grid.length</c>).</summary>
    private double _gridColumns;

    /// <summary>The number of grid rows (<c>grid[0].length</c>).</summary>
    private double _gridRows;

    /// <summary>
    ///     Initializes the spring embedder (cose-base's <c>initParameters</c> and layout-base's
    ///     <c>initSpringEmbedder</c>).
    /// </summary>
    private void InitSpringEmbedder()
    {
        // initParameters runs before the limit is raised, so the cooling cycles and final temperature follow 2500
        _maxCoolingCycle = _maxIterations / ConvergenceCheckPeriod;
        _finalTemperature = ConvergenceCheckPeriod / _maxIterations;
        (_coolingFactor, _maxIterations) = CoolingStart(_nodes.Count);
        _initialCoolingFactor = _coolingFactor;
        _totalDisplacementThreshold = 3.0 * IdealEdgeLength / 100 * _nodes.Count;
    }

    /// <summary>
    ///     Gives the initial cooling factor and the iteration limit of a layout that is not incremental
    ///     (<c>initSpringEmbedder</c>): a graph of more than 1000 nodes starts cooler, down to 0.33 at 5000 nodes, and the
    ///     limit grows to five iterations per node beyond 500 nodes.
    /// </summary>
    /// <param name="nodeCount">The number of nodes.</param>
    /// <returns>The initial cooling factor and the iteration limit.</returns>
    internal static (double CoolingFactor, double MaxIterations) CoolingStart(int nodeCount)
    {
        var s = (double)nodeCount;
        var coolingFactor = s > AdaptationLowerNodeLimit
            ? Math.Max(
                CoolingAdaptationFactor,
                1.0 - ((s - AdaptationLowerNodeLimit) / (AdaptationUpperNodeLimit - AdaptationLowerNodeLimit) * (1 - CoolingAdaptationFactor)))
            : 1.0;
        return (coolingFactor, Math.Max(s * 5, DefaultMaxIterations));
    }

    /// <summary>
    ///     Runs the iterations until the layout ends (<c>runSpringEmbedder</c>), then updates the bounds.
    /// </summary>
    /// <returns>The number of iterations.</returns>
    private int RunSpringEmbedder()
    {
        var layoutEnded = false;
        while (!layoutEnded)
        {
            layoutEnded = Tick();
        }

        UpdateBounds();
        return _totalIterations;
    }

    /// <summary>
    ///     Runs one iteration (cose-base's <c>tick</c> without the tree-growing branches, which a layout without pruned
    ///     nodes never reaches).
    /// </summary>
    /// <returns>Whether the layout has ended.</returns>
    private bool Tick()
    {
        // The layout ends at the iteration limit
        _totalIterations++;
        if (JsEquals(_totalIterations, _maxIterations))
        {
            return true;
        }

        // Every 100 iterations: end on convergence, else cool by Luke's schedule 3
        if (_totalIterations % ConvergenceCheckPeriod == 0)
        {
            if (IsConverged())
            {
                return true;
            }

            _coolingCycle++;
            var exponent = MeteredLog(100 * (_initialCoolingFactor - _finalTemperature)) / MeteredLog(_maxCoolingCycle);
            _coolingFactor = Math.Max(
                _initialCoolingFactor - (MeteredPow(_coolingCycle, exponent) / 100 * CoolingAdjuster),
                _finalTemperature);
        }

        // The forces and moves of the iteration
        _meter.Charge(_nodes.Count + _edges.Count + 1);
        _totalDisplacement = 0;
        UpdateBounds();
        CalcSpringForces();
        CalcRepulsionForces();
        MoveNodes();
        return false;
    }

    /// <summary>
    ///     Decides whether the layout has converged or oscillates (<c>isConverged</c>).
    /// </summary>
    /// <returns>Whether it has.</returns>
    private bool IsConverged()
    {
        var oscillating = false;
        if (_totalIterations > _maxIterations / 3)
        {
            oscillating = Math.Abs(_totalDisplacement - _oldTotalDisplacement) < 2;
        }

        var converged = _totalDisplacement < _totalDisplacementThreshold;
        _oldTotalDisplacement = _totalDisplacement;
        return converged || oscillating;
    }

    /// <summary>
    ///     Applies the spring force of every edge (<c>calcSpringForces</c> and <c>calcSpringForce</c>): proportional to
    ///     the difference between the length between the clipping points and the ideal length, nothing when the end
    ///     nodes overlap.
    /// </summary>
    private void CalcSpringForces()
    {
        foreach (var edge in _edges)
        {
            edge.UpdateLength();
            if (edge.IsOverlapping)
            {
                continue;
            }

            var length = edge.Length;
            if (JsEquals(length, 0))
            {
                continue;
            }

            var springForce = SpringConstant * (length - IdealEdgeLength);
            var springForceX = springForce * (edge.LengthX / length);
            var springForceY = springForce * (edge.LengthY / length);
            edge.Source.SpringForceX += springForceX;
            edge.Source.SpringForceY += springForceY;
            edge.Target.SpringForceX -= springForceX;
            edge.Target.SpringForceY -= springForceY;
        }
    }

    /// <summary>
    ///     Applies the repulsion between nearby nodes with the grid variant (<c>calcRepulsionForces</c>): every tenth
    ///     iteration the grid and each node's surrounding nodes are rebuilt; each pair repels once.
    /// </summary>
    private void CalcRepulsionForces()
    {
        var rebuild = _totalIterations % GridCalculationCheckPeriod == 1;
        if (rebuild)
        {
            UpdateGrid();
        }

        var processed = new HashSet<CoseNode>();
        foreach (var node in _nodes)
        {
            if (rebuild)
            {
                FindSurrounding(node, processed);
            }

            _meter.Charge(node.Surrounding.Count);
            foreach (var other in node.Surrounding)
            {
                CalcRepulsionForce(node, other);
            }

            processed.Add(node);
        }
    }

    /// <summary>
    ///     Rebuilds the repulsion grid over the root graph's bounds and puts every node in the cells it covers
    ///     (<c>updateGrid</c>, <c>calcGrid</c> and <c>addNodeToGrid</c>).
    /// </summary>
    /// <exception cref="MermaidRenderException">
    ///     Thrown when JavaScript could not build the grid (an invalid array length, a <c>RangeError</c>) or a node lies
    ///     outside it (a <c>TypeError</c>), or when the cells pass the work limit.
    /// </exception>
    private void UpdateGrid()
    {
        // calcGrid: new Array(sizeX), each column new Array(sizeY); the cells are charged before any is made
        var columns = ParseInt(Math.Ceiling((_right - _left) / RepulsionRange));
        var rows = ParseInt(Math.Ceiling((_bottom - _top) / RepulsionRange));
        if (!IsArrayLength(columns) || (columns > 0 && !IsArrayLength(rows)))
        {
            throw _meter.Failure("the repulsion grid of the cose layout cannot be built (JavaScript's 'Invalid array length')");
        }

        ChargeCount(columns * rows);
        _gridColumns = columns;
        _gridRows = rows;
        _grid.Clear();

        // addNodeToGrid for every node, from the root graph's left and top
        foreach (var node in _nodes)
        {
            AddNodeToGrid(node, _left, _top);
        }
    }

    /// <summary>
    ///     Puts a node in every grid cell its rectangle covers and records them (<c>addNodeToGrid</c>).
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="left">The left of the grid.</param>
    /// <param name="top">The top of the grid.</param>
    private void AddNodeToGrid(CoseNode node, double left, double top)
    {
        var startX = ParseInt(Math.Floor((node.X - left) / RepulsionRange));
        var finishX = ParseInt(Math.Floor((node.Width + node.X - left) / RepulsionRange));
        var startY = ParseInt(Math.Floor((node.Y - top) / RepulsionRange));
        var finishY = ParseInt(Math.Floor((node.Height + node.Y - top) / RepulsionRange));

        // JavaScript's two loops: no column (or a NaN bound) does nothing; columns without rows only count
        if (!(startX <= finishX))
        {
            return;
        }

        if (!(startY <= finishY))
        {
            ChargeCount(finishX - startX + 1);
            return;
        }

        // A cell outside the grid is undefined in JavaScript, and pushing to it throws
        if (startX < 0 || finishX >= _gridColumns || startY < 0 || finishY >= _gridRows)
        {
            throw _meter.Failure("a node lies outside the repulsion grid of the cose layout (JavaScript's 'TypeError')");
        }

        ChargeCount((finishX - startX + 1) * (finishY - startY + 1));
        for (var i = (long)startX; i <= finishX; i++)
        {
            for (var j = (long)startY; j <= finishY; j++)
            {
                if (!_grid.TryGetValue((i, j), out var cell))
                {
                    cell = [];
                    _grid[(i, j)] = cell;
                }

                cell.Add(node);
            }
        }

        node.StartX = startX;
        node.FinishX = finishX;
        node.StartY = startY;
        node.FinishY = finishY;
    }

    /// <summary>
    ///     Finds the nodes near enough to repel a node: those in the cells around its own, not yet processed, whose gap
    ///     to it is at most the repulsion range in both directions (the rebuild of
    ///     <c>calculateRepulsionForceOfANode</c>).
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="processed">The nodes processed before it in this iteration.</param>
    private void FindSurrounding(CoseNode node, HashSet<CoseNode> processed)
    {
        // The cells from one before the node's first to one after its last, in each direction, clipped to the grid
        ChargeCount((node.FinishX + 2 - (node.StartX - 1)) * (node.FinishY + 2 - (node.StartY - 1)));
        var surrounding = new List<CoseNode>();
        var found = new HashSet<CoseNode>();
        for (var i = node.StartX - 1; i < node.FinishX + 2; i++)
        {
            for (var j = node.StartY - 1; j < node.FinishY + 2; j++)
            {
                if (i < 0 || j < 0 || i >= _gridColumns || j >= _gridRows ||
                    !_grid.TryGetValue(((long)i, (long)j), out var cell))
                {
                    continue;
                }

                _meter.Charge(cell.Count);
                foreach (var other in cell)
                {
                    if (other == node || processed.Contains(other) || found.Contains(other))
                    {
                        continue;
                    }

                    var distanceX = Math.Abs(node.CenterX - other.CenterX) - ((node.Width / 2) + (other.Width / 2));
                    var distanceY = Math.Abs(node.CenterY - other.CenterY) - ((node.Height / 2) + (other.Height / 2));
                    if (distanceX <= RepulsionRange && distanceY <= RepulsionRange)
                    {
                        found.Add(other);
                        surrounding.Add(other);
                    }
                }
            }
        }

        node.Surrounding = surrounding;
    }

    /// <summary>
    ///     Applies the repulsion between two nodes (<c>calcRepulsionForce</c>): overlapping nodes are pushed apart by
    ///     their separation amounts; others by the repulsion constant over the squared distance between their clipping
    ///     points (each component at least the minimum repulsion distance).
    /// </summary>
    /// <param name="a">The first node.</param>
    /// <param name="b">The second node.</param>
    private static void CalcRepulsionForce(CoseNode a, CoseNode b)
    {
        double repulsionForceX;
        double repulsionForceY;
        if (Intersects(a, b))
        {
            // Two nodes overlap: separate them, each by half
            var (overlapX, overlapY) = SeparationAmount(a, b, IdealEdgeLength / 2.0);
            repulsionForceX = 2 * overlapX;
            repulsionForceY = 2 * overlapY;
            var childrenConstant = NoOfChildren * NoOfChildren / (NoOfChildren + NoOfChildren);
            a.RepulsionForceX -= childrenConstant * repulsionForceX;
            a.RepulsionForceY -= childrenConstant * repulsionForceY;
            b.RepulsionForceX += childrenConstant * repulsionForceX;
            b.RepulsionForceY += childrenConstant * repulsionForceY;
            return;
        }

        // The distance between the clipping points, each component at least the minimum
        var clip = Intersection(a, b);
        var distanceX = clip.Bx - clip.Ax;
        var distanceY = clip.By - clip.Ay;
        if (Math.Abs(distanceX) < MinRepulsionDistance)
        {
            distanceX = Sign(distanceX) * MinRepulsionDistance;
        }

        if (Math.Abs(distanceY) < MinRepulsionDistance)
        {
            distanceY = Sign(distanceY) * MinRepulsionDistance;
        }

        var distanceSquared = (distanceX * distanceX) + (distanceY * distanceY);
        var distance = Math.Sqrt(distanceSquared);
        var repulsionForce = RepulsionConstant * NoOfChildren * NoOfChildren / distanceSquared;
        repulsionForceX = repulsionForce * distanceX / distance;
        repulsionForceY = repulsionForce * distanceY / distance;
        a.RepulsionForceX -= repulsionForceX;
        a.RepulsionForceY -= repulsionForceY;
        b.RepulsionForceX += repulsionForceX;
        b.RepulsionForceY += repulsionForceY;
    }

    /// <summary>
    ///     Moves every node by its forces (<c>moveNodes</c> with <c>CoSENode.move</c>): the cooled sum of the forces,
    ///     clamped to the cooled largest move, then the forces are reset.
    /// </summary>
    private void MoveNodes()
    {
        var limit = _coolingFactor * MaxNodeDisplacement;
        foreach (var node in _nodes)
        {
            var displacementX = _coolingFactor * (node.SpringForceX + node.RepulsionForceX + GravitationForce) / NoOfChildren;
            var displacementY = _coolingFactor * (node.SpringForceY + node.RepulsionForceY + GravitationForce) / NoOfChildren;
            if (Math.Abs(displacementX) > limit)
            {
                displacementX = limit * Sign(displacementX);
            }

            if (Math.Abs(displacementY) > limit)
            {
                displacementY = limit * Sign(displacementY);
            }

            node.MoveBy(displacementX, displacementY);
            _totalDisplacement += Math.Abs(displacementX) + Math.Abs(displacementY);
            node.SpringForceX = 0;
            node.SpringForceY = 0;
            node.RepulsionForceX = 0;
            node.RepulsionForceY = 0;
        }
    }

    /// <summary>
    ///     Computes a correctly rounded logarithm and charges it.
    /// </summary>
    /// <param name="x">The argument.</param>
    /// <returns>The logarithm.</returns>
    /// <exception cref="MermaidRenderException">Thrown when the argument is outside the function's domain.</exception>
    private double MeteredLog(double x)
    {
        // An argument outside the function's domain (which no layout of a tree makes) fails the render
        double result;
        int steps;
        try
        {
            result = Log(x, out steps);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw _meter.Failure(ex.Message);
        }

        _meter.Charge(1 + steps);
        return result;
    }

    /// <summary>
    ///     Computes a correctly rounded power and charges it.
    /// </summary>
    /// <param name="x">The base.</param>
    /// <param name="y">The exponent.</param>
    /// <returns>The power.</returns>
    /// <exception cref="MermaidRenderException">Thrown when the argument is outside the function's domain.</exception>
    private double MeteredPow(double x, double y)
    {
        // An argument outside the function's domain (which no layout of a tree makes) fails the render
        double result;
        int steps;
        try
        {
            result = Pow(x, y, out steps);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw _meter.Failure(ex.Message);
        }

        _meter.Charge(1 + steps);
        return result;
    }

    /// <summary>
    ///     Charges a count of steps given as a JavaScript number (which may be huge or not a number).
    /// </summary>
    /// <param name="count">The count; NaN and negative counts charge nothing.</param>
    private void ChargeCount(double count)
    {
        if (count > 0)
        {
            _meter.Charge(count >= long.MaxValue ? long.MaxValue : (long)count);
        }
    }

    /// <summary>
    ///     Whether a number is a valid JavaScript array length: a whole number from 0 to 2^32 - 1.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsArrayLength(double value) =>
        value >= 0 && value <= MaxArrayLength && JsEquals(Math.Floor(value), value);
}
