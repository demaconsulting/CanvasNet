### ClusterLayout

![Rendering Structure](MermaidRenderingView.svg)

The `ClusterLayout` unit is the first software unit in the `Rendering` subsystem. It is the internal sealed partial
class `ClusterLayout` in four source files - `ClusterLayout.cs` (the graphs, the measuring, and the drawing),
`ClusterLayout.Extractor.cs` (the subgraph table, the anchors, and the extraction), `ClusterLayout.SelfLoops.cs` (the
edges that are drawn and the compact self-loops), and `ClusterLayout.Diagram.cs` (the framing of a whole diagram with
its title and view box) - with the graph labels `ClusterLayoutNode` and `ClusterLayoutEdge` and the result record
`RenderedGraph` in `ClusterLayout.cs`. The unit also groups the input records of the renderer in `LayoutData.cs`:
`LayoutData`, `LayoutNode`, `LayoutEdge`, `LayoutClassBox`, `LayoutClassMember`, `LayoutErBox`, and
`LayoutErAttribute`. All types are in the `DemaConsulting.CanvasNet.Mermaid.Rendering` namespace.

#### Purpose

`ClusterLayout` is Mermaid's dagre renderer (`rendering-util/layout-algorithms/dagre/index.js` with
`mermaid-graphlib.js`). Dagre lays out a compound graph but knows nothing of what Mermaid does around it: a subgraph
without links to the outside is laid out on its own, in its own direction, and stands in for its contents as one node;
a link to a subgraph is moved to a node inside it and cut at the subgraph's border; a self-loop is laid out through two
hidden nodes and, for the diagram types whose renderer merges its segments, drawn as one compact loop; and the
subgraph title margins move the contents down. The unit reproduces all of it and draws the result in Mermaid's layers,
so that the positions of the nodes, subgraphs, and edges are those of the pinned Mermaid. It also holds the shared end
of the `draw` functions of the unified diagrams (the flowchart's `flowRenderer-v3-unified.ts`, the class diagram's
`classRenderer-v3-unified.ts`, the state diagram's `stateRenderer-v3-unified.ts`, and the entity relationship
diagram's `erRenderer-unified.ts`): the drawing as the root group, the diagram title (`insertTitle`), and the view box
grown by the padding (`setupViewPortForSVG`).

`LayoutData` is the contract between a diagram model and the renderer (Mermaid's `LayoutData`, `Node`, and `Edge` of
`rendering-util/types.ts`). The unit's behavior is specified by the `CanvasNetMermaid-Rendering-ClusterLayout-*`
requirements in `docs/reqstream/canvas-net-mermaid/rendering/cluster-layout.yaml`.

#### Data Model

- `LayoutData(Nodes, Edges, Config)` - the nodes and subgraphs in Mermaid's order (subgraphs first, the last declared
  first, then the vertices, for a flowchart; the namespaces, the classes, the notes, and the lollipop interfaces for a
  class diagram; the states, clusters, and notes in the order of the state diagram's data fetcher; the subgraphs in
  reverse order and then the entities for an entity relationship diagram), the edges in declaration order, and the
  effective configuration, with `DiagramType` (`flowchart-v2`, `classDiagram`, the legacy `class`, `stateDiagram`,
  `er`, and so on; it decides whether self-loops are merged), `LayoutAlgorithm` (always
  `dagre`), `Direction`, `NodeSpacing` and `RankSpacing` (50 by default), `Markers`, and `Clusters` (the nodes that are
  groups). Immutable. `CheckNestingDepth(maxDepth, clusters)` checks the nesting that the parents make, as the layout
  sets them (for an id that several nodes share, the last parent given): a node inside more than `maxDepth` clusters
  is a `MermaidRenderException` with code `mermaid-limit-exceeded` and the message `<clusters> are nested deeper than
  N levels (MermaidLimits.MaxNestingDepth)`, located at the node that gave its parent to the first node (in node order)
  exactly one level too deep, where the limit is passed (every deeper chain goes through such a node). A
  diagram model calls it from its `GetData` with `MaxNestingDepth`, because a cluster can be nested by naming it
  inside another cluster declared elsewhere, which chains clusters that the parser never sees nested. The depth of
  each id is computed once, so the check is linear; a chain that runs into a cycle is left to the layout, which fails
  on it, located, as before. `CheckNestingDepthOf(nodes, maxDepth, clusters)` is the same check over a list of
  `NestingEntry` values (id, parent, location) that is not layout data: the flowchart model runs it on every subgraph
  and vertex before a collapsed subgraph hides its descendants from the layout data, so hidden nesting is bounded too.
- `LayoutNode` - a node or a subgraph (`IsGroup`): `Id`, `Label`, `LabelType` (`text`, `string`, or `markdown`),
  `ParentId`, `Padding`, `CssStyles`, `TrustedStyles` (the styles are the diagram's own and are applied without
  reports), `CssCompiledStyles`, `CssClasses`, `Dir`, `DomId` (`flowchart-A-0`, `classId-A-0`, `state-A-0`,
  `entity-A-0`), `Look`, `Shape` (a name of `ShapeNames`, a legacy name, `classBox`, or a state cluster's
  `roundedWithTitle`, `divider`, or `noteGroup`), the icon and picture properties of `@{ ... }` (`Icon`, `Pos`, `Img`,
  `AssetWidth`, `AssetHeight`, `Constraint`), `ClassBox` (the texts of a class box, or null), `Rx` and `Ry` (the radii
  Mermaid gives a state's rectangle, 10, with which a `rect` is drawn as a `roundedRect`; else null), `CenterLabel`
  (Mermaid's `node.centerLabel`, which a state sets: the label is centered whatever its kind), `Descriptions` (the
  descriptions a `rectWithTitle` state draws under its title, the first description being its `Label`; else null),
  `ErBox` (the alias and attributes of an entity box, or null), and the `Location` of its first mention. A property
  that Mermaid leaves undefined is null.
- `LayoutClassBox(Text, Annotations, Members, Methods, Styles)` - the texts of a class box as Mermaid's class database
  holds them (generics already written as `&lt;T&gt;`, a visibility escaped with a backslash): the drawn name, the
  annotations without their angle quotes (only the first is drawn), the attributes, the methods, and the class's own
  styles (its `style` statements and class styles). Immutable.
- `LayoutClassMember(Text, CssStyle)` - an attribute or a method: its text and the style of its classifier
  (`font-style:italic;` for an abstract member, `text-decoration:underline;` for a static one, else empty).
- `LayoutErBox(Alias, Attributes)` - the texts of an entity box as Mermaid's `erBox` shape reads them from an entity
  node: its alias, drawn in place of its name when set (empty for none), and its attributes in order. Immutable.
- `LayoutErAttribute(Type, Name, Keys, Comment)` - one row of an entity box: its type as written (a generic `~T~` is
  drawn as `<T>` by the shape), its name, its keys in order (`PK`, `FK`, `UK`; empty for none), and its comment (empty
  for none).
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
  10 by 10 `labelRect` nodes of each self-loop), adjusts the subgraphs and edges, and renders the root graph. Every
  parent is set (`SetParent`, which fails a cycle) before any edge is added, and each edge is first checked by
  `CheckEnds`: an end that is not a node of the graph (in an entity relationship diagram, an entity named like a
  subgraph declared after the relationship, which is not drawn, where graphlib would add a node without data on which
  Mermaid's render fails) fails located at the edge. Because the parents come first, a subgraph that is its own parent
  fails as a cycle before its dangling edge is checked, as in Mermaid.
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
  margin above the origin, in the font and color of the style sheet's `Title` rule, with the box of its glyphs as SVG's
  `getBBox` gives it (the advance of the centered text, on the browser's 1/64 pixel run grid, grown to the boxes of the
  glyphs at its ends by `LabelBuilder.SvgLineExtent`, so a title starting with a glyph that reaches left of its origin
  starts that far left) and the rounded ascent and descent from the label builder; and the view box of the drawing and
  the title grown by the padding on every side (`setupViewPortForSVG`). The scene carries the pictures of the render. A
  flowchart passes its `titleTopMargin` and `diagramPadding`; a class diagram the state section's `titleTopMargin` (25
  by default) and a padding of 8; a state diagram its `state.titleTopMargin` and a padding of 8; an entity relationship
  diagram its `er.titleTopMargin` and a padding of 8.
- `SortNodesByHierarchy(graph)` - `sortNodesByHierarchy`: the top level first, then each subtree.

**Preconditions and postconditions**: the layout data has unique node ids; the result is a deterministic function of the
data, the configuration, and the label measurement.

#### Error Handling

- A parent that would make a cycle of subgraphs throws a located `MermaidRenderException` (`mermaid-render-failed`),
  where graphlib's `setParent` fails Mermaid's render: for example a state inside a composite state that is itself
  inside that state (`state A { B }` with `state B { A }`), or an entity relationship subgraph that holds itself. The
  failure is located at the node whose parent is set.
- An edge with an end that is not a node of the diagram throws a `MermaidRenderException` (`mermaid-render-failed`)
  located at the edge, whose message names the edge and the end that is not drawn and whose hint asks to rename the
  entity or the later subgraph that has its name, where Mermaid's render fails ("reading 'shape'").
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

- **Rendering subsystem** - `ShapeLibrary` (node shapes, subgraph rectangles, and state clusters), `EdgeBuilder`
  (edges, edge labels, and terminal labels), `LabelBuilder` (the glyph box and vertical metrics of the diagram title,
  through `SvgLineExtent` and `VerticalMetrics`)
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
class diagram's title margin and a padding of 8. `StateModel.Layout` (the `State` subsystem) builds the `LayoutData` of
a state diagram with `StateModel.GetData` (its `DiagramType` `stateDiagram`, also for the legacy id, the marker `barb`,
the state spacing, and the states, pseudo-states, notes, composite states, concurrent regions, and note groups with
their shapes and classes) and calls `RenderDiagram` with `state.titleTopMargin` and a padding of 8. `ErModel.Layout`
(the `Er` subsystem) builds the `LayoutData` of an entity relationship diagram with `ErModel.GetData` (its
`DiagramType` `er`, the four cardinality markers, the entity relationship spacing, the entities with their
`LayoutErBox`, the subgraphs, and the relationships) and calls `RenderDiagram` with `er.titleTopMargin` and a padding
of 8. The tests in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Rendering/ClusterLayoutTests.cs` call the unit directly through
`InternalsVisibleTo`.

#### Known Differences From Mermaid

- A subgraph cycle and a link to a subgraph without an anchor, on which Mermaid's render fails with a script error,
  fail with a located `MermaidRenderException` (`mermaid-render-failed`) that names the problem and suggests a fix; so
  do a state diagram's parent cycle, an entity relationship subgraph that is its own parent, and an edge with an end
  that is not drawn (an entity relationship diagram's relationship to an entity named like a later subgraph), on which
  Mermaid's render also fails.
- Only the subgraph drawing that a flowchart, a class diagram, a state diagram, and an entity relationship diagram use
  is ported (`clusters.js`'s `rect`, which also draws the namespaces of a class diagram and the subgraphs of an entity
  relationship diagram, and the state diagram's `roundedWithTitle`, `noteGroup`, and `divider`; not `kanbanSection`).
- A `flowchart-elk` diagram merges its self-loops as a flowchart does, although `flowchart-elk` is not among the types
  of Mermaid's `shouldMergeSelfLoopSegments`, because its ELK layout is not ported and it is laid out with dagre and
  drawn as a flowchart.
