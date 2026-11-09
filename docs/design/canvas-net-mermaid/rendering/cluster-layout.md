### ClusterLayout

![Rendering Structure](MermaidRenderingView.svg)

The `ClusterLayout` unit is the first software unit in the `Rendering` subsystem. It is the internal sealed partial
class `ClusterLayout` in four source files - `ClusterLayout.cs` (the graphs, the measuring, and the drawing),
`ClusterLayout.Extractor.cs` (the subgraph table, the anchors, and the extraction), `ClusterLayout.SelfLoops.cs` (the
edges that are drawn and the compact self-loops), and `ClusterLayout.Diagram.cs` (the framing of a whole diagram with
its title and view box) - with the graph labels `ClusterLayoutNode` and `ClusterLayoutEdge` and the result record
`RenderedGraph` in `ClusterLayout.cs`. The unit also groups the input records of the renderer in `LayoutData.cs`:
`LayoutData`, `LayoutNode`, `LayoutEdge`, `LayoutClassBox`, and `LayoutClassMember`. All types are in the
`DemaConsulting.CanvasNet.Mermaid.Rendering` namespace.

#### Purpose

`ClusterLayout` is Mermaid's dagre renderer (`rendering-util/layout-algorithms/dagre/index.js` with
`mermaid-graphlib.js`). Dagre lays out a compound graph but knows nothing of what Mermaid does around it: a subgraph
without links to the outside is laid out on its own, in its own direction, and stands in for its contents as one node;
a link to a subgraph is moved to a node inside it and cut at the subgraph's border; a self-loop is laid out through two
hidden nodes and, for the diagram types whose renderer merges its segments, drawn as one compact loop; and the
subgraph title margins move the contents down. The unit reproduces all of it and draws the result in Mermaid's layers,
so that the positions of the nodes, subgraphs, and edges are those of the pinned Mermaid. It also holds the shared end
of the `draw` functions of the unified diagrams (the flowchart's `flowRenderer-v3-unified.ts` and the class diagram's
`classRenderer-v3-unified.ts`): the drawing as the root group, the diagram title (`insertTitle`), and the view box grown
by the padding (`setupViewPortForSVG`).

`LayoutData` is the contract between a diagram model and the renderer (Mermaid's `LayoutData`, `Node`, and `Edge` of
`rendering-util/types.ts`). The unit's behavior is specified by the `CanvasNetMermaid-Rendering-ClusterLayout-*`
requirements in `docs/reqstream/canvas-net-mermaid/rendering/cluster-layout.yaml`.

#### Data Model

- `LayoutData(Nodes, Edges, Config)` - the nodes and subgraphs in Mermaid's order (subgraphs first, the last declared
  first, then the vertices, for a flowchart; the namespaces, the classes, the notes, and the lollipop interfaces for a
  class diagram), the edges in declaration order, and the effective configuration, with `DiagramType` (`flowchart-v2`,
  `classDiagram`, the legacy `class`, and so on; it decides whether self-loops are merged), `LayoutAlgorithm` (always
  `dagre`), `Direction`, `NodeSpacing` and `RankSpacing` (50 by default), `Markers`, and `Clusters` (the nodes that are
  groups). Immutable.
- `LayoutNode` - a node or a subgraph (`IsGroup`): `Id`, `Label`, `LabelType` (`text`, `string`, or `markdown`),
  `ParentId`, `Padding`, `CssStyles`, `TrustedStyles` (the styles are the diagram's own and are applied without
  reports), `CssCompiledStyles`, `CssClasses`, `Dir`, `DomId` (`flowchart-A-0`, `classId-A-0`), `Look`, `Shape` (a name
  of `ShapeNames`, a legacy name, or `classBox`), the icon and picture properties of `@{ ... }` (`Icon`, `Pos`, `Img`,
  `AssetWidth`, `AssetHeight`, `Constraint`), `ClassBox` (the texts of a class box, or null), and the `Location` of its
  first mention. A property that Mermaid leaves undefined is null.
- `LayoutClassBox(Text, Annotations, Members, Methods, Styles)` - the texts of a class box as Mermaid's class database
  holds them (generics already written as `&lt;T&gt;`, a visibility escaped with a backslash): the drawn name, the
  annotations without their angle quotes (only the first is drawn), the attributes, the methods, and the class's own
  styles (its `style` statements and class styles). Immutable.
- `LayoutClassMember(Text, CssStyle)` - an attribute or a method: its text and the style of its classifier
  (`font-style:italic;` for an abstract member, `text-decoration:underline;` for a static one, else empty).
- `LayoutEdge` - an edge: `Id` (`L_A_B_0`, the id of `e1@`, or a class relation's `id_A_B_1`), `Start`, `End`, `Type`,
  `Label`, `LabelType`, `Thickness`, `MinLen`, `Classes`, `ArrowTypeStart`, `ArrowTypeEnd`, the compiled and own styles,
  `Animate`, `Animation`, `Curve`, `StartLabelRight` and `EndLabelLeft` (the terminal labels beside the start and the
  end, a class relation's cardinalities; empty for none), and `Location`.
- `ClusterLayoutNode` (a `DagreNode`) - the dagre label of a node: its `LayoutNode`, the drawn `ShapeResult` and
  intersect function, and for a laid-out subgraph (Mermaid's `clusterNode`) the inner graph, the `RenderedGraph` of it,
  and its title correction `Diff`; `Box` is its layout box.
- `ClusterLayoutEdge` (a `DagreEdge`) - the dagre label of an edge: its `LayoutEdge`, the measured label, the arrow
  types, the terminal labels (`StartLabelRight` and `EndLabelLeft`; of the three segments of a self-loop the first keeps
  only the start label and the last only the end label), the subgraphs it leaves or enters (`FromCluster`,
  `ToCluster`), and for the three segments of a self-loop the id of the loop and the order of the segment.
- `RenderedGraph(Items, Bounds, Diff)` - the drawn graph: the layers as scene groups, the box of everything drawn
  (null when nothing is drawn), and the `Diff` of the last subgraph.
- The subgraph table (Mermaid's `clusterDb`) and the descendants of each subgraph are instance fields of one render, as
  is whether the self-loops of the diagram being drawn are merged. The diagram types that merge them are those of
  Mermaid's `shouldMergeSelfLoopSegments` (`flowchart`, `flowchart-v2`, `stateDiagram`, `er`, and `classDiagram`) and
  `flowchart-elk`: Mermaid lays an ELK flowchart out with ELK, but here its layout falls back to dagre and it is drawn
  as a flowchart, so it keeps the flowchart's compact loop.

#### Key Methods

- `ClusterLayout(ShapeContext context, LayoutWorkMeter meter)` - the layout of one render, with the total subgraph
  title margin of the configuration.
- `Render(LayoutData data)` - decides from `DiagramType` whether the diagram's self-loops are merged, builds the root
  graph (`prepareLayoutForDagre`: `nodesep` and `ranksep` from the configuration, a top-level `nodeSpacing` or
  `rankSpacing` winning, margins of 8, every node with its parent, every edge, and the three segments and two hidden
  10 by 10 `labelRect` nodes of each self-loop), adjusts the subgraphs and edges, and renders the root graph.
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
  `EdgeBuilder.MeasureLabel` (of a merged self-loop only the middle segment's; of an unmerged one every segment's).
- `Paint` - `paintDagreLayoutCore`: in `SortNodesByHierarchy` order, moves the `clusterNode`s and nodes down by the
  title margins and draws each subgraph laid out with its graph through `ShapeLibrary.DrawCluster`; draws the edges of
  `GetEdgesToRender` with `EdgeBuilder.Draw`, places their labels with `EdgeBuilder.LabelPosition`, and draws the
  terminal labels of each edge that has them with `EdgeBuilder.DrawTerminals` on its drawn route; draws the labels in
  measuring order with `EdgeBuilder.DrawLabel`, each followed by the terminal labels of its edge, as Mermaid's
  `insertEdgeLabel` appends them in the layer of edge labels; and draws the nodes and laid-out subgraphs
  (`positionNode`: a `clusterNode` at `x + diff - width / 2`, `y - height / 2 - 8`). The layers are the groups
  `clusters`, `edgePaths`, `edgeLabels`, and `nodes`, and the bounds are those of SVG's `getBBox` (path geometry and
  label boxes, markers and empty boxes excluded).
- `GetEdgesToRender` - `getEdgesToRender`: every edge in order, then, when self-loops are merged, one merged edge per
  self-loop, placed on the side of `getSelfLoopSide` with the points of `getSelfLoopPoints` and the label of
  `getSelfLoopLabelPosition`; otherwise the three segments of a self-loop are drawn as they are, in graph order.
- `RenderDiagram(LayoutData data, string? title, double titleTopMargin, double padding, LabelBuilder labels)`
  (`ClusterLayout.Diagram.cs`) - renders the data and returns the whole `Scene`: the drawing as the `root` group; when
  there is a title, the title sanitized with `HtmlSanitizer` and drawn by `DrawTitle` (Mermaid's `insertTitle`) as one
  `title` line centered over the bounds of the drawing (over the origin when nothing is drawn), its baseline the title
  margin above the origin, in the font and color of the style sheet's `Title` rule, with the box of its advance and the
  rounded ascent and descent from the label builder; and the view box of the drawing and the title grown by the
  padding on every side (`setupViewPortForSVG`). The scene carries the pictures of the render. A flowchart passes its
  `titleTopMargin` and `diagramPadding`; a class diagram the state section's `titleTopMargin` (25 by default) and a
  padding of 8.
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
- The errors of `ShapeLibrary`, `EdgeBuilder` (among them the located `mermaid-render-failed` of a terminal label on a
  route too short to place it), and the dagre layout propagate unchanged; an unexpected self-loop state keeps its
  segments rather than dropping the edge.
- `RenderDiagram` throws `ArgumentNullException` for null data or a null label builder.

#### Dependencies

- **Rendering subsystem** - `ShapeLibrary` (node shapes, subgraph rectangles), `EdgeBuilder` (edges, edge labels, and
  terminal labels), `LabelBuilder` (the advance and vertical metrics of the diagram title)
- **Dagre subsystem** - `DagreLayout.Run`, `DagreGraph`, `DagreNode`, `DagreEdge`, `DagrePoint`, `LayoutWorkMeter`
- **Graphlib subsystem** - `Graph` (compound multigraphs)
- **Scenegraph subsystem** - `Scene`, `SceneGroup`, `SceneText`, `SceneTextRun`, `SceneFont`, `SceneTextAnchor`,
  `SceneItem`, `SceneRect`
- **Configuration subsystem** - `MermaidConfig` (spacing and title margins), `UnifiedStyles` (the title rule)
- **Syntax subsystem** - `HtmlSanitizer` (the diagram title)
- **Package root** - `MermaidRenderException`, `MermaidDiagnosticCodes`, `MermaidLocation`

#### Callers

`FlowchartModel.Layout` (the `Flowchart` subsystem) builds the `LayoutData` with `FlowchartModel.GetData` (its
`DiagramType` the diagram's Mermaid id), creates the layout with the render's `ShapeContext` and `LayoutWorkMeter`, and
calls `RenderDiagram` with the frontmatter title, `flowchart.titleTopMargin`, and `flowchart.diagramPadding`.
`ClassModel.Layout` (the `Class` subsystem) builds the `LayoutData` of a class diagram with `ClassModel.GetData` (its
`DiagramType` `classDiagram`, or `class` for the legacy id, with the class boxes, the relations' cardinalities as
terminal labels, and the trusted styles of its notes and interfaces) and calls `RenderDiagram` the same way with the
class diagram's title margin and a padding of 8. The tests in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Rendering/ClusterLayoutTests.cs` call the unit directly through
`InternalsVisibleTo`.

#### Known Differences From Mermaid

- A subgraph cycle and a link to a subgraph without an anchor, on which Mermaid's render fails with a script error,
  fail with a located `MermaidRenderException` (`mermaid-render-failed`) that names the problem and suggests a fix.
- Only the subgraph drawing that a flowchart and a class diagram use is ported (`clusters.js`'s `rect`, which also
  draws the namespaces of a class diagram; not `roundedWithTitle`, `noteGroup`, `divider`, or `kanbanSection`).
- A `flowchart-elk` diagram merges its self-loops as a flowchart does, although `flowchart-elk` is not among the types
  of Mermaid's `shouldMergeSelfLoopSegments`, because its ELK layout is not ported and it is laid out with dagre and
  drawn as a flowchart.
