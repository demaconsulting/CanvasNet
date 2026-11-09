### ClusterLayout

![Rendering Structure](MermaidRenderingView.svg)

The `ClusterLayout` unit is the first software unit in the `Rendering` subsystem. It is the internal sealed partial
class `ClusterLayout` in three source files - `ClusterLayout.cs` (the graphs, the measuring, and the drawing),
`ClusterLayout.Extractor.cs` (the subgraph table, the anchors, and the extraction), and `ClusterLayout.SelfLoops.cs`
(the edges that are drawn and the compact self-loops) - with the graph labels `ClusterLayoutNode` and
`ClusterLayoutEdge` and the result record `RenderedGraph` in `ClusterLayout.cs`. The unit also groups the input records
of the renderer in `LayoutData.cs`: `LayoutData`, `LayoutNode`, and `LayoutEdge`. All types are in the
`DemaConsulting.CanvasNet.Mermaid.Rendering` namespace.

#### Purpose

`ClusterLayout` is Mermaid's dagre renderer (`rendering-util/layout-algorithms/dagre/index.js` with
`mermaid-graphlib.js`). Dagre lays out a compound graph but knows nothing of what Mermaid does around it: a subgraph
without links to the outside is laid out on its own, in its own direction, and stands in for its contents as one node;
a link to a subgraph is moved to a node inside it and cut at the subgraph's border; a self-loop is laid out through two
hidden nodes and drawn as one compact loop; and the subgraph title margins move the contents down. The unit reproduces
all of it and draws the result in Mermaid's layers, so that the positions of the nodes, subgraphs, and edges are those
of the pinned Mermaid.

`LayoutData` is the contract between a diagram model and the renderer (Mermaid's `LayoutData`, `Node`, and `Edge` of
`rendering-util/types.ts`). The unit's behavior is specified by the `CanvasNetMermaid-Rendering-ClusterLayout-*`
requirements in `docs/reqstream/canvas-net-mermaid/rendering/cluster-layout.yaml`.

#### Data Model

- `LayoutData(Nodes, Edges, Config)` - the nodes and subgraphs in Mermaid's order (subgraphs first, the last declared
  first, then the vertices), the edges in declaration order, and the effective configuration, with `DiagramType`
  (`flowchart-v2`), `LayoutAlgorithm` (always `dagre`), `Direction`, `NodeSpacing` and `RankSpacing` (50 by default),
  `Markers`, and `Clusters` (the nodes that are groups). Immutable.
- `LayoutNode` - a node or a subgraph (`IsGroup`): `Id`, `Label`, `LabelType` (`text`, `string`, or `markdown`),
  `ParentId`, `Padding`, `CssStyles`, `CssCompiledStyles`, `CssClasses`, `Dir`, `DomId` (`flowchart-A-0`), `Look`,
  `Shape` (a name of `ShapeNames` or a legacy name), the icon and picture properties of `@{ ... }` (`Icon`, `Pos`,
  `Img`, `AssetWidth`, `AssetHeight`, `Constraint`), and the `Location` of its first mention. A property that Mermaid
  leaves undefined is null.
- `LayoutEdge` - an edge: `Id` (`L_A_B_0` or the id of `e1@`), `Start`, `End`, `Type`, `Label`, `LabelType`,
  `Thickness`, `MinLen`, `Classes`, `ArrowTypeStart`, `ArrowTypeEnd`, the compiled and own styles, `Animate`,
  `Animation`, `Curve`, and `Location`.
- `ClusterLayoutNode` (a `DagreNode`) - the dagre label of a node: its `LayoutNode`, the drawn `ShapeResult` and
  intersect function, and for a laid-out subgraph (Mermaid's `clusterNode`) the inner graph, the `RenderedGraph` of it,
  and its title correction `Diff`; `Box` is its layout box.
- `ClusterLayoutEdge` (a `DagreEdge`) - the dagre label of an edge: its `LayoutEdge`, the measured label, the arrow
  types, the subgraphs it leaves or enters (`FromCluster`, `ToCluster`), and for the three segments of a self-loop the
  id of the loop and the order of the segment.
- `RenderedGraph(Items, Bounds, Diff)` - the drawn graph: the layers as scene groups, the box of everything drawn
  (null when nothing is drawn), and the `Diff` of the last subgraph.
- The subgraph table (Mermaid's `clusterDb`) and the descendants of each subgraph are instance fields of one render.

#### Key Methods

- `ClusterLayout(ShapeContext context, LayoutWorkMeter meter)` - the layout of one render, with the total subgraph
  title margin of the configuration.
- `Render(LayoutData data)` - builds the root graph (`prepareLayoutForDagre`: `nodesep` and `ranksep` from the
  configuration, a top-level `nodeSpacing` or `rankSpacing` winning, margins of 8, every node with its parent, every
  edge, and the three segments and two hidden 10 by 10 `labelRect` nodes of each self-loop), adjusts the subgraphs and
  edges, and renders the root graph.
- `AdjustClustersAndEdges` - `adjustClustersAndEdges`: records each subgraph's descendants and first non-subgraph
  descendant, marks the subgraphs an edge leaves or enters as having external connections, chooses each anchor
  (`findNonClusterChild`, `isNodeInExtractableCluster`, `findSafeAnchorNode`), moves the links to subgraphs with
  external connections onto their anchors (remembering the subgraph to cut at), and runs `Extractor`.
- `Extractor` - `extractor` and `copy`: each subgraph without external connections becomes a graph of its own (the
  direction it declares, else the other one of `TB` and `LR`; spacing 50; margins 8) and a `clusterNode` takes its
  place, recursively to a depth of 10.
- `RenderGraph` - `measureDagreGraph`, `DagreLayout.Run`, then `Paint`. Measuring lays out the inner graph of each
  `clusterNode` first (with the parent's `nodesep` and its `ranksep` plus 25) and takes its size, draws every other
  node with `ShapeLibrary.Draw` in the direction of its graph, and measures every edge label with
  `EdgeBuilder.MeasureLabel` (of a self-loop only the middle segment's).
- `Paint` - `paintDagreLayoutCore`: in `SortNodesByHierarchy` order, moves the `clusterNode`s and nodes down by the
  title margins and draws each subgraph laid out with its graph through `ShapeLibrary.DrawCluster`; draws the edges of
  `GetEdgesToRender` with `EdgeBuilder.Draw` and places their labels with `EdgeBuilder.LabelPosition`; draws the labels
  in measuring order with `EdgeBuilder.DrawLabel`; and draws the nodes and laid-out subgraphs (`positionNode`: a
  `clusterNode` at `x + diff - width / 2`, `y - height / 2 - 8`). The layers are the groups `clusters`, `edgePaths`,
  `edgeLabels`, and `nodes`, and the bounds are those of SVG's `getBBox` (path geometry and label boxes, markers and
  empty boxes excluded).
- `GetEdgesToRender` - `getEdgesToRender`: every edge in order, then one merged edge per self-loop, placed on the side
  of `getSelfLoopSide` with the points of `getSelfLoopPoints` and the label of `getSelfLoopLabelPosition`.
- `SortNodesByHierarchy(graph)` - `sortNodesByHierarchy`: the top level first, then each subtree.

**Preconditions and postconditions**: the layout data has unique node ids; the result is a deterministic function of the
data, the configuration, and the label measurement.

#### Error Handling

- A parent that would make a cycle of subgraphs throws a located `MermaidRenderException` (`mermaid-render-failed`),
  where graphlib's `setParent` fails Mermaid's render.
- A link to a subgraph whose anchor is not inside a subgraph, or to a subgraph without a node to attach to, throws a
  located `MermaidRenderException` (`mermaid-render-failed`), where Mermaid's render fails.
- A link (other than a self-edge) that still ends at a subgraph holding other nodes when its graph is about to be laid
  out (`CheckNoEdgeAtSubgraph`, run before each dagre layout) throws a `MermaidRenderException`
  (`mermaid-render-failed`) located at the link: dagre ranks a flat copy of the graph without such subgraphs, so the
  end has no label there and the pinned Mermaid fails in `longestPath` ("Cannot set properties of undefined (setting
  'rank')"). This happens, for example, when a subgraph is also used as a node of another subgraph, or for a self-loop
  on a subgraph next to links into the subgraphs around it.
- The work meter throws a located `MermaidRenderException` (`mermaid-limit-exceeded`) when the layout work limit is
  spent, and `OperationCanceledException` when the call is canceled.
- The errors of `ShapeLibrary`, `EdgeBuilder`, and the dagre layout propagate unchanged; an unexpected self-loop state
  keeps its segments rather than dropping the edge.

#### Dependencies

- **Rendering subsystem** - `ShapeLibrary` (node shapes, subgraph rectangles), `EdgeBuilder` (edges and edge labels)
- **Dagre subsystem** - `DagreLayout.Run`, `DagreGraph`, `DagreNode`, `DagreEdge`, `DagrePoint`, `LayoutWorkMeter`
- **Graphlib subsystem** - `Graph` (compound multigraphs)
- **Scenegraph subsystem** - `SceneGroup`, `SceneItem`, `SceneRect`
- **Configuration subsystem** - `MermaidConfig` (spacing and title margins)
- **Package root** - `MermaidRenderException`, `MermaidDiagnosticCodes`, `MermaidLocation`

#### Callers

`FlowchartModel.Layout` (the `Flowchart` subsystem) builds the `LayoutData` with `FlowchartModel.GetData`, creates
the layout with the render's `ShapeContext` and `LayoutWorkMeter`, and puts the drawn layers into the root group of the
scene. The tests in `test/DemaConsulting.CanvasNet.Mermaid.Tests/Rendering/ClusterLayoutTests.cs` call the unit
directly through `InternalsVisibleTo`.

#### Known Differences From Mermaid

- A subgraph cycle and a link to a subgraph without an anchor, on which Mermaid's render fails with a script error,
  fail with a located `MermaidRenderException` (`mermaid-render-failed`) that names the problem and suggests a fix.
- Only the subgraph drawing a flowchart uses is ported (`clusters.js`'s `rect`; not `roundedWithTitle`, `noteGroup`,
  `divider`, or `kanbanSection`).
