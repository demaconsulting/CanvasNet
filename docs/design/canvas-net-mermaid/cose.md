## Cose

![MermaidCose Structure](MermaidCoseView.svg)

### Overview

The `Cose` subsystem (namespace `DemaConsulting.CanvasNet.Mermaid.Cose`, folder `src/DemaConsulting.CanvasNet.Mermaid/Cose/`)
is the force-directed layout that Mermaid draws its mindmaps with: a port of the cose-bilkent layout
(cytoscape-cose-bilkent over cose-base and layout-base) as Mermaid runs it, with the edge points that Mermaid reads back
from cytoscape. Given the sizes of the nodes of one tree and its edges, it gives the center of every node and the
start, middle, and end point of every edge. It knows nothing about Mermaid text, shapes, or paint. It is **internal**:
its types are visible only to the other subsystems of the system and, through `InternalsVisibleTo`, to the test
assembly. It contains one unit:

- **CoseLayout** - the internal sealed `CoseLayout` (a partial class in `CoseLayout.cs`, `CoseLayout.Graph.cs`,
  `CoseLayout.Radial.cs`, `CoseLayout.Forces.cs`, `CoseLayout.Edges.cs`, and `CoseLayout.Math.cs`) with the records
  `CoseSize`, `CoseLink`, `CosePoint`, `CoseEdgePoints`, and `CoseResult` (in `CoseLayout.cs`): the root graph, the
  radial start, the spring embedder with its repulsion grid and cooling schedule, the move to the origin, cytoscape's
  straight-edge points, the correctly rounded sine, cosine, logarithm, and power, and the work charges; see _CoseLayout
  Unit Design_ (`cose/cose-layout.md`)

The subsystem's namespace documentation is in `Cose/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetMermaid-Cose-*` requirements in `docs/reqstream/canvas-net-mermaid/cose.yaml`. The SysML2 model names the
subsystem `MermaidCose` (see _CanvasNetMermaid System Design_).

**Attribution.** The subsystem ports the parts of layout-base and cose-base (MIT, iVis@Bilkent) and of
cytoscape-cose-bilkent and cytoscape (MIT, The Cytoscape Consortium) that a mindmap reaches, as the pinned Mermaid
release bundles them, and the options of Mermaid's `layout-algorithms/cose-bilkent/cytoscape-setup.ts` and `layout.ts`
(MIT); see `third-party/README.md`, which ships with the license texts `LICENSE-cose-base.txt` and
`LICENSE-cytoscape.txt`. The correctly rounded math (`CoseLayout.Math.cs`) and the namespace documentation are original
work of this repository. Every ported file carries the provenance header comment.

### Interfaces

**Exposed** (internal only): `CoseLayout.Run(IReadOnlyList<CoseSize>, IReadOnlyList<CoseLink>, LayoutWorkMeter)`, which
lays out one tree and returns a `CoseResult` (the node centers in the order of the nodes, the edge points in the order
of the edges, and the number of iterations), or throws. For tests and for the vectors the unit also exposes the
correctly rounded `Sin`, `Cos`, `Log`, and `Pow` with `RoundCorrectly` and `RoundToDouble`, `StraightEdgePoints`,
`FindCenterOfTree`, `CoolingStart`, and `ParseInt`.

**Consumed**: from the `Dagre` subsystem the `LayoutWorkMeter` of the diagram (`Charge`, `Failure`, `LimitExceeded`,
and the cancellation token it observes); from the system the public `MermaidRenderException` and
`MermaidDiagnosticCodes`; and from the .NET base class library `System.Numerics.BigInteger`, collections, and the exact
operations of `System.Math` (`Abs`, `Floor`, `Ceiling`, `Max`, and `Sqrt`, which IEEE 754 defines exactly).

**Consumers**: the `Mindmap` subsystem: `MindmapRenderer` calls `Run` once per render with the node sizes, the edges of
the tree, and the meter of the render.

### Design

**Only the path a tree takes.** Mermaid's cose-bilkent renderer adds every node (at the origin, with its measured size)
and every edge to cytoscape and runs the layout with `quality: 'proof'` and `animate: false`. A mindmap is always one
tree, so the layout always takes the same path: `getFlatForest` finds the one tree, `positionNodesRadially` places it
around its center, the spring embedder moves the nodes under a cooling schedule until it converges or reaches its
iteration limit, and `transform` moves the drawing so that its top-left corner less the margin of 15 is at the origin.
The tree reduction, the random placement, the tiling of zero-degree nodes, the compound-node code, and the bend points
are never reached by a tree; they are not ported, and `Run` refuses any input that is not one tree.

**Randomness.** The request for this phase asked for a seeded, deterministic random number generator in place of
`Math.random`, matching Mermaid except where a tie makes the order of random draws matter. That is not needed, and no
generator is ported. The only `Math.random` calls of cose-base are in `findPlaceforPrunedNode`, which runs only for the
nodes that `reduceTrees` prunes, and `reduceTrees` runs only when the graph is not a flat forest; layout-base's seeded
`RandomSeed` serves only `positionNodesRandomly` and `LNode.scatter`, which only the non-forest branch reaches; and
cose-bilkent's `randomize: true` only means "not incremental". A mindmap is one tree, a flat forest, so none of these
is reachable: the layout of a mindmap draws no random number at all. This was confirmed against the pinned release:
an instrumented mermaid-cli render made no `Math.random` call from the layout, two renders gave byte-identical SVGs,
every one of the 36 layout cases recorded in Chrome took the radial path once without a random call, and the reference
harness fails a golden run if `findPlaceforPrunedNode` ever calls `Math.random`. The layout therefore matches Mermaid
exactly, with no tie caveat, whenever it is given bit-identical node sizes.

**Exactness in a chaotic layout.** The layout is chaotic: a node size changed by 0.01 pixel, or one unit in the last
place of a sine, can move the nodes of a mid-sized tree by hundreds of pixels. To give Mermaid's positions bit for bit,
the port follows the JavaScript operation order of every reachable function, keeps JavaScript's number semantics
(`parseInt` of a number through its string, NaN comparisons, `Integer.MAX_VALUE` bounds, and the clamp to the world
boundary), and computes the sine, cosine, logarithm, and power correctly rounded, as Chrome does, instead of with the
host's math library, which differs from Chrome in the last bit for some arguments. The results are therefore the same
on every platform.

**Edge points from cytoscape.** Mermaid's cytoscape instance keeps cytoscape's default node style (an ellipse of 30 by
30), so a lone edge between two nodes is straight and its ends are where the line between the centers crosses the
circle of radius 15 around each center, with the middle point between them. Mermaid draws the edge through these three
points without cutting it at the node outlines. When two centers are 15 pixels apart or closer, cytoscape gives no
points (NaN), and the renderer drops the edge.

**Fidelity is proven against the browser.** The vectors `cose-layouts.json` (36 layouts recorded in Chrome with the
libraries extracted from the pinned Mermaid bundle: single nodes, paths, stars, balanced and random trees up to 600
nodes, zero, identical, and huge sizes, sizes beyond the world boundary, and 3 trees replayed from mindmaps drawn by
Mermaid; 3 of them fail in Chrome as they fail in the port) and `js-math.json` (Chrome's sine and cosine of 23,480
angles, logarithm of 5,002 values, and power of the 850 cooling pairs that 1 to 1,016 nodes reach) are compared bit for
bit by the unit tests. The mindmap reference layout tests of the system also lay out every mindmap golden's own sizes
and compare the result bit for bit.

**Bounded and stoppable.** Every step charges the meter of the render, which bounds the work deterministically and
observes cancellation: the input, the forest and center searches, the radial placement, each iteration's nodes and
edges, every repulsion pair, every grid (its cells before it is filled), every grid cell a node is put in or searched,
and every transcendental function (one, plus each higher precision it needed). A grid that JavaScript could not build
or fill fails as `mermaid-render-failed` before anything is allocated for it. An instance serves one call on one
thread; the math functions are pure and safe for concurrent use.
