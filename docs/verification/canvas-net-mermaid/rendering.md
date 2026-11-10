## Rendering Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Rendering` subsystem (internal namespace
`DemaConsulting.CanvasNet.Mermaid.Rendering`), containing five units: `ClusterLayout` (Mermaid's dagre renderer, with
the layout data records and the framing of a whole diagram), `ShapeLibrary` (the node shapes and the class box, their
intersect functions, the subgraph rectangle, and the shape registry `ShapeNames`), `EdgeBuilder` (edge routes, curves,
the flowchart and class markers, edge labels, and terminal labels), `LabelBuilder` (label text layout and
measurement, and the text utilities of sequence diagrams), and `NodeImages` (the pictures loaded through the caller's
resolver).

### Verification Approach

The `Rendering` subsystem is verified at two tiers. The unit tests (see the unit verification designs under
`rendering/`) verify each unit in isolation, most of them against vectors recorded from the pinned Mermaid. The
subsystem-level scenarios in `RenderingTests.cs` verify the units working together: a flowchart fixture is parsed,
mapped to layout data by the `Flowchart` subsystem, and laid out through the whole subsystem - `ClusterLayout` with the
real `ShapeLibrary`, `LabelBuilder` (with the bundled fonts), `EdgeBuilder`, and the dagre layout - into the scene that
the painter would draw. A class diagram fixture is laid out the same way, mapped to layout data by the `Class`
subsystem, so that the class boxes, the class markers, the relation labels, and the terminal labels of the
cardinalities go through the same units. The test helper `SceneQuery` reads the nodes, subgraph rectangles, edge
paths, edge labels, and terminal labels of the scene by their Mermaid DOM ids in absolute coordinates.

The expected numbers are those of the pinned Mermaid for the same text, rendered in headless Chrome with the bundled
Liberation Sans: the view box, the node and subgraph boxes, the end points of every command of an edge path, the
centers of the edge labels, and the positions of the terminal labels, compared to the three decimals that Mermaid's
SVG reports (a tolerance of 0.002 pixel).
Because the node sizes come from the label measurement and the shape formulas, the edge ends from the intersect
functions and the route cuts, and the positions from the subgraph handling around dagre, an exact match of these
numbers verifies the collaboration of all five units. Three further scenarios lay out small texts written for the
purpose and assert analytic expectations: the box of a picture worked out from the natural size that a resolver gives
and Mermaid's sizing rule of an image node, the located failure of the picture limit, and the codes and lines of the
reports of the label measurement and the pictures. These use a test resolver that gives a picture of a fixed size and
records the links it is asked for, which stands in for the caller's resolver, the only way the subsystem reaches a
picture; otherwise the subsystem uses no mocking or stubbing. The subsystem is internal, so its tests reach it
through the test assembly's `InternalsVisibleTo` access.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None; a test image resolver that gives fixed-size pictures and records its requests stands in for the
  caller's resolver in the picture scenarios
- **Fonts**: The bundled Liberation fonts, resolved through the rendering's font resolver
- **Files**: The flowchart fixtures in `Fixtures/flowchart/` and the class diagram fixtures in `Fixtures/class/` of the
  test project (copied to the output folder)
- **Isolation**: Each test lays out its own document; no shared state

### Acceptance Criteria

The `Rendering` subsystem's verification passes when every subsystem-level scenario below and every unit-level
scenario in the five unit verification documents passes without error or unexpected exception. A box, end point, label
center, or terminal label position that differs from the pinned Mermaid's by more than 0.002 pixel constitutes a
failure.

### Test Scenarios

#### CanvasNetMermaid-Rendering-Layout: Subgraphs, Links, And Self-Loops Are Laid Out As Mermaid Lays Them Out

**Tests**: `RenderingTests.Rendering_Flowchart_LinkedSubgraphs_MatchMermaid`,
`RenderingTests.Rendering_Flowchart_SubgraphWithDirection_MatchesMermaid`,
`RenderingTests.Rendering_Flowchart_SelfLoop_MatchesMermaid`,
`RenderingTests.Rendering_ClassDiagram_RelationsWithCardinalities_MatchMermaid`

Lays out two subgraphs joined by a link from a node of one to a node of the other (so both are laid out with the root
graph and the link crosses both rectangles), a subgraph laid out on its own in the direction it declares and linked by
its id (the links ending on the box that stands in for it), and a self-loop beside a bent edge, and asserts that the
view box, the node boxes, the subgraph rectangles, and the end points of the edge routes - including the compact loop -
are Mermaid's. Lays out the class fixture `08-cardinality-labels.mmd` (six relations with cardinalities, four of them
labeled) and asserts that the framed diagram has Mermaid's view box of 429.629 by 416 pixels at the origin, the
padding of 8 around the drawing included.

#### CanvasNetMermaid-Rendering-NodeShapes: Nodes Have Mermaid's Sizes And Places

**Tests**: `RenderingTests.Rendering_Flowchart_LinkedSubgraphs_MatchMermaid`,
`RenderingTests.Rendering_Flowchart_SubgraphWithDirection_MatchesMermaid`,
`RenderingTests.Rendering_Flowchart_ImageNode_SizedByThePictureOfTheResolver`,
`RenderingTests.Rendering_Flowchart_PicturesBeyondMaxImages_FailAtTheNode`,
`RenderingTests.Rendering_ClassDiagram_RelationsWithCardinalities_MatchMermaid`

Asserts that the boxes of the drawn nodes (rectangles sized from their measured labels and padding, for example 77.797
by 54 pixels) and the subgraph rectangles built around them are Mermaid's, in a top-to-bottom diagram and in a
left-to-right subgraph. Lays out an image node with a label, a height of 60, and the aspect constraint, through a
resolver whose pictures are 40 by 20, and asserts that the resolver is asked once, for `pic.png` at 256 by 256 device
pixels, and that the scene holds one node picture of `pic.png` of 120 by 60 (the natural aspect ratio of 2 at the
given height). Lays out three image nodes with `MermaidLimits.MaxImages` of 2 and asserts that the layout fails with
`mermaid-limit-exceeded` at line 4, the node of the third picture, naming the limit, after the resolver was asked for
the first two pictures only. Asserts that the class boxes of the class fixture are Mermaid's: `classId-Customer-0` at
(292.273, 8), 98.688 by 84 pixels, and `classId-Order-1` at (174.469, 166), 67.578 by 84 pixels (the name and the
empty compartment of each class, sized from the measured name).

#### CanvasNetMermaid-Rendering-Edges: Edges And Their Labels Are Where Mermaid Draws Them

**Tests**: `RenderingTests.Rendering_Flowchart_LinkedSubgraphs_MatchMermaid`,
`RenderingTests.Rendering_Flowchart_SubgraphWithDirection_MatchesMermaid`,
`RenderingTests.Rendering_Flowchart_SelfLoop_MatchesMermaid`,
`RenderingTests.Rendering_Flowchart_EdgeLabels_MatchMermaid`,
`RenderingTests.Rendering_ClassDiagram_RelationsWithCardinalities_MatchMermaid`

Asserts that the commands of the edge paths (the basis curve through the layout's points, ended on the node outlines or
on the box of a subgraph laid out on its own, and pulled back for the arrowheads; and the compact self-loop) end where
Mermaid's do, and that the edge labels of a chain of labeled links (normal, dotted, and thick) are centered where
Mermaid centers them, with the spacing they add between ranks giving Mermaid's view box. Asserts that the label
`places` of the class relation `id_Customer_Order_1` is centered at (277.715, 129), that its cardinalities are the two
terminal labels at (284.976, 96.172) beside its start and (259.867, 161.33) beside its end, and that the class
relations are drawn with markers.

#### CanvasNetMermaid-Rendering-Labels: Labels Measure As Mermaid's Labels

**Tests**: `RenderingTests.Rendering_Flowchart_LinkedSubgraphs_MatchMermaid`,
`RenderingTests.Rendering_Flowchart_EdgeLabels_MatchMermaid`,
`RenderingTests.Rendering_Flowchart_UndrawnConstructs_AreReportedAtTheirNodes`,
`RenderingTests.Rendering_TextUtilities_WrapAtWordsAndMeasurePerLine`,
`RenderingTests.Rendering_ClassDiagram_RelationsWithCardinalities_MatchMermaid`

Asserts the sizes that follow from the measured labels: the node widths of the subgraph fixture, which are the HTML
label widths with the padding, and the positions of the edge labels and the width of the view box of the labeled
chain, which follow from the measured edge label boxes. Lays out, without an image resolver, a node with an `<u>`
label, a node whose label has a character that no bundled font draws, and an image node, and asserts that the three
nodes are laid out, that no feature is reported as unsupported, and that the warnings are, in this order,
`mermaid-html-tag-ignored` at line 2, `mermaid-text-missing-glyph` at line 3, and `mermaid-image-unresolved` at line 4.
Wraps a sentence wider than 150 pixels with the text utilities that sequence diagrams use, in the default sequence
font, and asserts that it breaks at its words into several lines that rejoin to the sentence, that the first line is
narrower than the width, and that the wrapped text measures as wide as its widest line and as high as one line height
per line. In the class fixture, the widths of the class boxes (98.688 pixels for `Customer`, 67.578 for `Order`) follow
from the names measured in bold, and the place of the relation label and the routes beside which the cardinalities
stand follow from the measured label `places`, so the same assertions verify the class diagram's labels.
