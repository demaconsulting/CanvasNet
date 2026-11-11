### CoseLayout

![MermaidCose Structure](MermaidCoseView.svg)

The `CoseLayout` unit is the one software unit in the `Cose` subsystem. It comprises six source files in the
`DemaConsulting.CanvasNet.Mermaid.Cose` namespace: `CoseLayout.cs` (the records of the input and the results, `Run`,
the tree check, the root graph, `runLayout`, and the move to the origin), `CoseLayout.Graph.cs` (the graph model, the
bounds, the geometry, and JavaScript's `parseInt`), `CoseLayout.Radial.cs` (the forest, the center, and the radial
placement), `CoseLayout.Forces.cs` (the spring embedder, the repulsion grid, the cooling schedule, and the metered
logarithm and power), `CoseLayout.Edges.cs` (cytoscape's straight-edge points), and `CoseLayout.Math.cs` (the
correctly rounded sine, cosine, logarithm, and power). `CoseLayout` is an `internal sealed partial` class, so the six
files are one type. The ported files carry the provenance header comment; `CoseLayout.Math.cs` is original work.

#### Purpose

`CoseLayout` lays out one tree as Mermaid's cose-bilkent layout lays out a mindmap in the browser, and gives the points
cytoscape stores for each edge. It exists so that the positions of a mindmap's nodes and the routes of its edges are
Mermaid's, bit for bit, for the same node sizes, on every platform, with bounded and stoppable work.

The unit's behavior is specified by the `CanvasNetMermaid-Cose-CoseLayout-*` requirements in
`docs/reqstream/canvas-net-mermaid/cose/cose-layout.yaml`.

#### Data Model

**Input and results** (readonly records): `CoseSize(Width, Height)` (a node's size, the browser's `getBBox` of the
drawn node), `CoseLink(Source, Target)` (an edge as the indexes of its nodes), `CosePoint(X, Y)` (NaN where cytoscape
gives none), `CoseEdgePoints(Start, Mid, End)` (cytoscape's `rscratch` points of an edge), and `CoseResult(Centers,
Edges, Iterations)`.

**The options in effect** are those of cose-bilkent's defaults with Mermaid's `quality: 'proof'` and `animate: false`,
held as constants: the ideal edge length 50, the spring constant 0.45, the repulsion constant 4,500, gravity 0 (a
connected graph has no node gravity applies to), the iteration limit 2,500 (raised to five per node beyond 500 nodes),
the convergence check every 100 iterations, the grid rebuilt every 10 iterations, the minimum repulsion distance 5, the
largest move 300, the cooling adaptation between 1,000 and 5,000 nodes down to 0.33, the cooling adjuster 1 (proof
quality), the repulsion range 100 (twice the ideal edge length at level 0), the graph margin 15, the world boundary
1,000,000, the world center (1200, 900), the radial separation 50, and `Integer.MAX_VALUE` and `MIN_VALUE`.

**A layout instance** serves one call on one thread and holds the meter, the nodes and edges of the root graph in the
input order, the root graph's bounds (left, right, top, bottom), the iteration state (the iteration count and limit,
the cooling cycle, the maximum cooling cycle 25 and final temperature 0.04 fixed before the limit is raised, the
cooling factor and its initial value, and the total displacements of the current and the previous check with the
convergence threshold), and the repulsion grid: its column and row counts and the cells that hold nodes, kept in a
dictionary by column and row with the same iteration as JavaScript's dense arrays.

**A node** (`CoseNode`, layout-base's `LNode` with the fields of `FDLayoutNode` and `CoSENode`) has its index, the
rectangle (left, top, width, height), the incident edges in the order they were added, the spring and repulsion forces,
the grid cells it covers (NaN, as JavaScript's `undefined`, until a grid holds it), and the nodes near enough to repel
it, in the order found. **An edge** (`CoseEdge`, `LEdge`) has its source and target and the lengths between the
clipping points of its end nodes, and whether they overlap.

#### Key Methods

- **`Run(nodes, edges, meter)`** charges the input (nodes, edges, and 1), checks that the input is one tree
  (`CheckTree`), builds the root graph (`AddGraph`), runs the layout (`RunLayout`), and reads the results (`Results`).
  An `InsufficientExecutionStackException` of the radial recursion becomes the meter's `LimitExceeded()`.
- **`CheckTree(count, edges)`**: no nodes and no edges is the empty layout; otherwise there must be one edge fewer than
  nodes, every end must be a node, and a breadth-first search from the first node must reach every node (which also
  rules out self loops, parallel edges, and cycles), else `ArgumentException`.
- **`AddGraph`** ports cytoscape-cose-bilkent's `run` and `processChildrenList`: each node is a rectangle at Mermaid's
  position (0, 0), `(0 - w/2, 0 - h/2, w, h)`, with `parseFloat` of the size (only -0 changes, to 0) and a NaN corner
  set to 0; each edge is added to the graph and to the incidence lists of its ends.
- **`RunLayout`** ports layout-base's `runLayout` with cose-base's `layout` and `classicLayout`: no nodes give 0
  iterations; otherwise `FlatForest`, `PositionNodesRadially`, `InitSpringEmbedder`, `RunSpringEmbedder`, and
  `Transform(0, 0)` (`doPostLayout`).
- **`FlatForest`** ports `getFlatForest` for one tree: a breadth-first search from the first node, each node visited
  when it leaves the queue and its neighbors other than its parent queued.
- **`FindCenterOfTree(tree, meter)`** ports `findCenterOfTree` with its quirk: the original removes nodes from its list
  while it walks the list by index, so each pass keeps the nodes at odd positions until one or two remain; the first is
  the center. The degree bookkeeping of the original never affects the result and is not kept.
- **`PositionNodesRadially`** ports `positionNodesRadially` for one tree at the start point (0, 0): `RadialLayout` puts
  the rings `max(maxDiagonalInTree, 50)` apart, `BranchRadialLayout` places each node at the middle of its angle
  interval (degrees turned to radians as `angle * 2π / 360`, then `distance * cos` and `distance * sin`) and its
  children in equal parts of the interval on the next ring, starting after the edge to its parent in the order of its
  edges; the bounds are moved to the start point, and the drawing to the world center less half its size.
- **`InitSpringEmbedder`** and **`CoolingStart(nodeCount)`** port cose-base's `initParameters` and layout-base's
  `initSpringEmbedder`: the maximum cooling cycle and the final temperature come from the limit of 2,500 before it is
  raised; the cooling factor is 1 up to 1,000 nodes and `max(0.33, 1 - (n - 1000) / 4000 * 0.67)` beyond; the limit is
  `max(5n, 2500)`; the convergence threshold is `3 * 50 / 100 * n`.
- **`Tick`** ports cose-base's `tick` without its tree-growing branches: the layout ends at the iteration limit; every
  100 iterations it ends on convergence (`IsConverged`: the total displacement below the threshold, or an oscillation
  after a third of the limit) or cools by `max(initial - pow(cycle, log(100 * (initial - final)) / log(maxCycle)) / 100,
  final)`; then it charges the nodes and edges and applies `UpdateBounds`, `CalcSpringForces`, `CalcRepulsionForces`,
  and `MoveNodes`.
- **`CalcSpringForces`** ports `calcSpringForce`: for each edge whose ends do not overlap, the length between the
  clipping points (`getIntersection2`, a component below 1 in magnitude becoming its sign) gives the force
  `0.45 * (length - 50)` along the edge.
- **`CalcRepulsionForces`** ports the grid variant: every 10th iteration `UpdateGrid` rebuilds the grid
  (`calcGrid` with `parseInt(Math.ceil(...))` of the bounds over the repulsion range, then `AddNodeToGrid` for every
  node) and `FindSurrounding` finds, for each node, the nodes in the cells around its own, not yet processed, whose gap
  is at most the repulsion range in both directions; each pair then repels once (`CalcRepulsionForce`): overlapping
  nodes by twice their separation amounts (`calcSeparationAmount` with
  `decideDirectionsForOverlappingNodes`), others by `4500 / d²` along the distance between their clipping points, each
  component at least 5.
- **`MoveNodes`** ports `CoSENode.move`: each node moves by the cooled sum of its forces, each component clamped to the
  cooling factor times 300, and the displacement is added to the total.
- **`Transform(left, top)`** ports `Layout.transform(newLeftTop)`: `updateLeftTop` finds the corner less the margin
  (nothing moves when JavaScript would return null), and each node's corner, clamped to the world boundary, is moved
  through the inverse transformation (`LNode.transform`).
- **`StraightEdgePoints(source, target)`** ports cytoscape's `findStraightEdgePoints` and `findEndpoints` for the
  default style: `intersectLineEllipse` with the circle of radius 15 around each center, `shortenIntersection` by 0, and
  the middle point the mean of the ends and the arrow ends; centers 15 apart or closer give NaN points.
- **`ParseInt(value)`** is JavaScript's `parseInt` of a whole number: below 1e21 the number itself (-0 gives 0); from
  1e21 on only the first digit of its shortest representation; NaN and the infinities give NaN.
- **`Sin(x)`**, **`Cos(x)`**, **`Log(x)`**, and **`Pow(x, y)`** (each with the number of extra precision steps it
  needed) are the correctly rounded functions: the double nearest to the exact value, ties to even. Each evaluates the
  exact value in binary fixed point with `BigInteger` at a working precision of 192 bits, with an error bound of
  2^24 units, and **`RoundCorrectly`** rounds both ends of the error interval with **`RoundToDouble`** (ties to even,
  subnormal results and overflow as IEEE 754 defines them); when they differ the value is near a rounding midpoint and
  the evaluation is repeated at 384 and then 768 bits (Ziv's strategy). Sine and cosine reduce the argument by a
  multiple of π/2 and sum Taylor series; the logarithm splits off a power of two and sums the series of
  `2 * atanh((f - 1) / (f + 1))`; the power is `exp(y * ln x)` with the exponential reduced by a multiple of ln 2. The
  constants π (Machin's formula) and ln 2 (the series of `atanh(1/3)`) are computed once, to 1,024 bits. `Pow` first
  applies V8's special cases in V8's order (a NaN exponent; an infinite exponent with a base of ±1; the exponent 2 as
  `x * x`; the exponent 0.5 as `sqrt(x + 0)`) and ECMAScript's (the exponent ±0, a NaN base, the base 1), and an
  exponent of 2^64 or more over- or underflows directly.

**The work charges** (all deterministic, pinned by the unit tests): the input, nodes plus edges plus 1; the forest
search, 1 plus the node's edges per visit; the center search, the list length per pass; the radial placement, 1 plus
the node's edges per node; each iteration, nodes plus edges plus 1; each node's repulsion pairs; each grid, its
columns times rows, charged before the grid is filled; each node's cells when it is put in the grid; each node's
surrounding cells and the entries of each cell scanned; each transcendental function, 1 plus its extra precision steps;
and the edge points, edges plus 1. The meter observes cancellation at every charge. A tree of two nodes takes 903 units
and a path of ten nodes 5,598.

**Not ported, because a tree never reaches it**: `reduceTrees`, `growTree`, and `findPlaceforPrunedNode` (the only
`Math.random` calls) and the tree-growing branches of `tick`, because the list of pruned nodes is always empty;
`positionNodesRandomly`, `LNode.scatter`, and `RandomSeed`, because the non-forest branch is never taken; the tiling of
zero-degree members, because a tree has at most one zero-degree node (a lone root) and no compounds;
`calcGravitationalForces`, because a connected graph has no node gravity applies to; the compound, inter-graph, and
bend point code, because the graph is flat and `createBendsAsNeeded` is false; the animation, because `animate` is
false; and `getIntersection` with four points and `angleOfVector`, because no reachable code calls them. `Run` refuses
any input that is not one tree, so none of these is needed.

**Preconditions and postconditions**: the nodes and edges form one tree (or are both empty); the result holds one
center per node and one set of points per edge in the input order, the drawing's top-left corner at the margin of 15
from the origin, or an exception. The same input always gives the same result, bit for bit.

#### Error Handling

- An input that is not one tree is an `ArgumentException` (an internal invariant: a mindmap always is one); null
  arguments are `ArgumentNullException`s.
- A grid that JavaScript could not build (a count of columns or rows that is not a valid array length, a `RangeError`
  in Mermaid) and a node outside the grid (a `TypeError` in Mermaid) are a `MermaidRenderException` with code
  `mermaid-render-failed` from the meter's `Failure`, at the meter's location; a grid that cannot be built fails before
  its cells are charged or made.
- An argument of a transcendental function outside its domain, which no layout of a tree makes, is caught at the
  metered call and becomes the meter's `mermaid-render-failed`; a value too close to a rounding midpoint to round at 768
  bits is the same. The four functions themselves throw `ArgumentOutOfRangeException`.
- The meter throws a located `MermaidRenderException` with code `mermaid-limit-exceeded` when the work passes
  `MaxLayoutWork`, and an `OperationCanceledException` when the call is canceled. A recursion that would exhaust the
  stack is the meter's `mermaid-limit-exceeded`.
- Every exception propagates to the renderer; nothing is caught and swallowed.

#### Dependencies

- **Dagre subsystem** - `LayoutWorkMeter` (`Charge`, `Failure`, `LimitExceeded`, and cancellation).
- **System** - `MermaidRenderException` and `MermaidDiagnosticCodes`.
- **.NET base class library** - `System.Numerics.BigInteger`, collections, `System.Runtime.CompilerServices.RuntimeHelpers`
  (`EnsureSufficientExecutionStack`), and the exactly rounded operations of `System.Math` (`Abs`, `Floor`, `Ceiling`,
  `Max`, and `Sqrt`); no transcendental function of `System.Math` is used.

#### Callers

- **MindmapRenderer** (`Mindmap` subsystem) - calls `Run` once per render with the node sizes in node order, the edges
  in the order of `generateEdges`, and the meter of the render.
- The unit tests call `Run`, the math functions, `StraightEdgePoints`, `FindCenterOfTree`, `CoolingStart`, and
  `ParseInt` directly; the golden tests and the mindmap reference layout tests call `Run` with recorded inputs.

#### Known Differences From Mermaid

- **Randomness.** None: no random number is drawn, because a tree never reaches the random code of cose-base (see
  _Cose Design_). No generator is ported, and there is no tie case in which the layout could differ from Mermaid's.
- **Pow beyond 1,016 nodes.** Chrome's `Math.pow` is correctly rounded on every cooling pair the layout reaches up to
  1,016 nodes; from 1,017 nodes, for some node counts, it is not, and the cooling, and so the whole layout, may differ.
  A mindmap that large can reach the default `MaxLayoutWork` first.
- **Failures.** Where Mermaid's layout throws a JavaScript error, the port fails the render with a located
  `mermaid-render-failed`; the work budget and cancellation are additions.
