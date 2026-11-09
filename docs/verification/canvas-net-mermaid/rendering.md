## Rendering Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Rendering` subsystem (internal namespace
`DemaConsulting.CanvasNet.Mermaid.Rendering`), containing five units: `ClusterLayout` (Mermaid's dagre renderer, with
the layout data records), `ShapeLibrary` (the node shapes, their intersect functions, the subgraph rectangle, and the
shape registry `ShapeNames`), `EdgeBuilder` (edge routes, curves, markers, and edge labels), `LabelBuilder` (label text
layout and measurement), and `NodeImages` (the pictures loaded through the caller's resolver).

### Verification Approach

The `Rendering` subsystem is verified at two tiers. The unit tests (see the unit verification designs under
`rendering/`) verify each unit in isolation, most of them against vectors recorded from the pinned Mermaid. The
subsystem-level scenarios in `RenderingTests.cs` verify the units working together: a flowchart fixture is parsed,
mapped to layout data by the `Flowchart` subsystem, and laid out through the whole subsystem - `ClusterLayout` with the
real `ShapeLibrary`, `LabelBuilder` (with the bundled fonts), `EdgeBuilder`, and the dagre layout - into the scene that
the painter would draw. The test helper `SceneQuery` reads the nodes, subgraph rectangles, edge paths, and edge labels
of the scene by their Mermaid DOM ids in absolute coordinates.

The expected numbers are those of the pinned Mermaid for the same text, rendered in headless Chrome with the bundled
Liberation Sans: the view box, the node and subgraph boxes, the end points of every command of an edge path, and the
centers of the edge labels, compared to the three decimals that Mermaid's SVG reports (a tolerance of 0.002 pixel).
Because the node sizes come from the label measurement and the shape formulas, the edge ends from the intersect
functions and the route cuts, and the positions from the subgraph handling around dagre, an exact match of these
numbers verifies the collaboration of all five units. The subsystem uses no mocking or stubbing; the subsystem is
internal, so its tests reach it through the test assembly's `InternalsVisibleTo` access.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled Liberation fonts, resolved through the rendering's font resolver
- **Files**: The flowchart fixtures in `Fixtures/flowchart/` of the test project (copied to the output folder)
- **Isolation**: Each test lays out its own document; no shared state

### Acceptance Criteria

The `Rendering` subsystem's verification passes when every subsystem-level scenario below and every unit-level
scenario in the five unit verification documents passes without error or unexpected exception. A box, end point, or
label center that differs from the pinned Mermaid's by more than 0.002 pixel constitutes a failure.

### Test Scenarios

#### CanvasNetMermaid-Rendering-Layout: Subgraphs, Links, And Self-Loops Are Laid Out As Mermaid Lays Them Out

**Tests**: `RenderingTests.Rendering_Flowchart_LinkedSubgraphs_MatchMermaid`,
`RenderingTests.Rendering_Flowchart_SubgraphWithDirection_MatchesMermaid`,
`RenderingTests.Rendering_Flowchart_SelfLoop_MatchesMermaid`

Lays out two subgraphs joined by a link from a node of one to a node of the other (so both are laid out with the root
graph and the link crosses both rectangles), a subgraph laid out on its own in the direction it declares and linked by
its id (the links ending on the box that stands in for it), and a self-loop beside a bent edge, and asserts that the
view box, the node boxes, the subgraph rectangles, and the end points of the edge routes - including the compact loop -
are Mermaid's.

#### CanvasNetMermaid-Rendering-NodeShapes: Nodes Have Mermaid's Sizes And Places

**Tests**: `RenderingTests.Rendering_Flowchart_LinkedSubgraphs_MatchMermaid`,
`RenderingTests.Rendering_Flowchart_SubgraphWithDirection_MatchesMermaid`

Asserts that the boxes of the drawn nodes (rectangles sized from their measured labels and padding, for example 77.797
by 54 pixels) and the subgraph rectangles built around them are Mermaid's, in a top-to-bottom diagram and in a
left-to-right subgraph.

#### CanvasNetMermaid-Rendering-Edges: Edges And Their Labels Are Where Mermaid Draws Them

**Tests**: `RenderingTests.Rendering_Flowchart_LinkedSubgraphs_MatchMermaid`,
`RenderingTests.Rendering_Flowchart_SubgraphWithDirection_MatchesMermaid`,
`RenderingTests.Rendering_Flowchart_SelfLoop_MatchesMermaid`,
`RenderingTests.Rendering_Flowchart_EdgeLabels_MatchMermaid`

Asserts that the commands of the edge paths (the basis curve through the layout's points, ended on the node outlines or
on the box of a subgraph laid out on its own, and pulled back for the arrowheads; and the compact self-loop) end where
Mermaid's do, and that the edge labels of a chain of labeled links (normal, dotted, and thick) are centered where
Mermaid centers them, with the spacing they add between ranks giving Mermaid's view box.

#### CanvasNetMermaid-Rendering-Labels: Labels Measure As Mermaid's Labels

**Tests**: `RenderingTests.Rendering_Flowchart_LinkedSubgraphs_MatchMermaid`,
`RenderingTests.Rendering_Flowchart_EdgeLabels_MatchMermaid`

Asserts the sizes that follow from the measured labels: the node widths of the subgraph fixture, which are the HTML
label widths with the padding, and the positions of the edge labels and the width of the view box of the labeled
chain, which follow from the measured edge label boxes.
