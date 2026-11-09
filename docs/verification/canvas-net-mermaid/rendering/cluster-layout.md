### ClusterLayout Unit Verification Design

This document describes the unit-level verification strategy for the `ClusterLayout` unit in the
`DemaConsulting.CanvasNet.Mermaid.Rendering` namespace: the internal `ClusterLayout` class (Mermaid's dagre renderer,
in `ClusterLayout.cs`, `ClusterLayout.Extractor.cs`, and `ClusterLayout.SelfLoops.cs`) and the layout data records
`LayoutData`, `LayoutNode`, and `LayoutEdge` that it reads.

#### Verification Approach

The unit is verified by laying out small flowcharts with `ClusterLayout.Render` and asserting the structure and
geometry of the drawn graph. The test helper `RenderingTestContext` parses the text, maps it to layout data with the
`Flowchart` subsystem, builds the `ShapeContext` of the text (its configuration, style sheet, and the real
`LabelBuilder` with the bundled fonts), and runs the layout with a `LayoutWorkMeter` of a given budget; the drawn
graph is read with `SceneQuery` (nodes, subgraph rectangles, edges, and edge labels in absolute coordinates, by their
Mermaid ids and roles). The assertions are structural (a subgraph laid out on its own is one `subgraph` group; a
self-loop is one path) and geometric with hand-derived relations (a node moved down by the title margin, a rectangle
widened around the same center, a route ending 4 pixels short of a box for the arrowhead). The exact agreement with
Mermaid's numbers is verified at the subsystem level. The real shapes, labels, edges, and dagre layout are used; no
mocking or stubbing is required, and the internal types are reached through the test assembly's `InternalsVisibleTo`
access.

Unit tests reside in `ClusterLayoutTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Rendering` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled Liberation fonts for the label measurement
- **Isolation**: Each test lays out its own text with its own meter; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A subgraph laid out in the wrong graph or direction, a link attached or cut elsewhere, a self-loop drawn as
more than one path or inside its node, a layer or subgraph drawn out of order, or a work limit that is not enforced
constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Rendering-ClusterLayout-IsolatedSubgraphs: Isolated Subgraphs Are Laid Out On Their Own

**Tests**: `ClusterLayoutTests.ClusterLayout_Render_IsolatedSubgraph_IsLaidOutOnItsOwnInTheOtherDirection`,
`ClusterLayoutTests.ClusterLayout_Render_DeclaredDirection_Wins`,
`ClusterLayoutTests.ClusterLayout_Render_NestedIsolatedSubgraph_IsANestedGroup`

Asserts that a subgraph without links to the outside of a top-to-bottom diagram is drawn as one `subgraph` group whose
nodes lie side by side on one row (the other direction) inside its rectangle, that a `direction RL` declared in the
subgraph wins over the flipped one, and that an isolated subgraph nested in a subgraph with links to the outside is the
only `subgraph` group, its rectangle inside the rectangle of its parent.

##### CanvasNetMermaid-Rendering-ClusterLayout-SubgraphLinks: Links To Subgraphs Attach And Cut As In Mermaid

**Tests**: `ClusterLayoutTests.ClusterLayout_Render_LinksToIsolatedSubgraph_EndOnItsBox`,
`ClusterLayoutTests.ClusterLayout_Render_LinkFromSubgraphWithOutsideLinks_StartsOutsideIt`

Asserts that a link into a subgraph without links to the outside ends 4 pixels short of the box of the laid-out
subgraph (for the arrowhead) and a link out of it starts on the box, and that a labeled link from a subgraph with links
to the outside is moved to a node inside it, starts on the left side of the subgraph's rectangle within its height, and
keeps its label.

##### CanvasNetMermaid-Rendering-ClusterLayout-SelfLoops: A Self-Loop Is One Compact Loop

**Tests**: `ClusterLayoutTests.ClusterLayout_Render_SelfLoop_IsOneLoopBesideItsNode`

Asserts that a link from a node to itself is laid out through the two hidden nodes of Mermaid's workaround (drawn as
`dummy-group` items) and drawn as one edge path whose points all lie outside its node.

##### CanvasNetMermaid-Rendering-ClusterLayout-SubgraphRectangles: Subgraph Rectangles Follow Their Titles

**Tests**: `ClusterLayoutTests.ClusterLayout_Render_WideTitle_WidensTheRectangle`,
`ClusterLayoutTests.ClusterLayout_Render_SubgraphTitleMargins_MoveNodesDown`

Asserts that a subgraph title wider than the contents widens the rectangle to at least the title width plus the
padding, centered on the node, and that subgraph title margins of 10 and 6 pixels make the rectangle 16 pixels taller
and move its node down by 8 pixels.

##### CanvasNetMermaid-Rendering-ClusterLayout-DrawingOrder: Layers And Subgraphs Are Drawn In Mermaid's Order

**Tests**: `ClusterLayoutTests.ClusterLayout_SortNodesByHierarchy_TopLevelFirst`,
`ClusterLayoutTests.ClusterLayout_Render_EmptyDiagram_DrawsNothing`

Asserts the drawing order of `sortNodesByHierarchy` on a nested graph (the top-level nodes `C` and `B` first, then the
subtree `A` and `D`), and that an empty diagram draws the four layers `clusters`, `edgePaths`, `edgeLabels`, and
`nodes`, all empty, and has no bounds.

##### CanvasNetMermaid-Rendering-ClusterLayout-WorkLimit: The Layout Work Is Bounded

**Tests**: `ClusterLayoutTests.ClusterLayout_Render_TinyWorkBudget_ThrowsLimitExceeded`

Asserts that a layout with a tiny work budget stops with a `MermaidRenderException` whose code is
`mermaid-limit-exceeded`, located on the line of the construct the meter was made for.
