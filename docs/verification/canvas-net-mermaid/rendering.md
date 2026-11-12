## Rendering Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Rendering` subsystem (internal namespace
`DemaConsulting.CanvasNet.Mermaid.Rendering`), containing five units: `ClusterLayout` (Mermaid's dagre renderer, with
the layout data records and the framing of a whole diagram), `ShapeLibrary` (the node shapes, the class box, the state
shapes and clusters, the entity box, and the mindmap shapes, their intersect functions, the subgraph rectangle, and the
shape registry
`ShapeNames`), `EdgeBuilder` (edge routes, curves, the flowchart, class, state, and entity relationship markers, edge
labels, and terminal labels), `LabelBuilder` (label text layout and
measurement, the text utilities of sequence diagrams, and the Font Awesome test), and `NodeImages` (the pictures loaded
through the caller's resolver).

### Verification Approach

The `Rendering` subsystem is verified at two tiers. The unit tests (see the unit verification designs under
`rendering/`) verify each unit in isolation, most of them against vectors recorded from the pinned Mermaid. The
subsystem-level scenarios in `RenderingTests.cs` verify the units working together: a flowchart fixture is parsed,
mapped to layout data by the `Flowchart` subsystem, and laid out through the whole subsystem - `ClusterLayout` with the
real `ShapeLibrary`, `LabelBuilder` (with the bundled fonts), `EdgeBuilder`, and the dagre layout - into the scene that
the painter would draw. A class diagram fixture is laid out the same way, mapped to layout data by the `Class`
subsystem, so that the class boxes, the class markers, the relation labels, and the terminal labels of the
cardinalities go through the same units. A state diagram fixture (mapped to layout data by the `State` subsystem) and
an entity relationship diagram fixture (mapped by the `Er` subsystem) are laid out the same way, so that the state
shapes, the composite state and concurrent region clusters, the `barb` marker, the entity tables, the cardinality
markers, and the SVG role labels go through the same units. A mindmap fixture is drawn through `ShapeLibrary` and
`LabelBuilder` with the mindmap's style sheet and its edges through `EdgeBuilder`, placed by the `Cose` subsystem, so
that the mindmap shapes, the paint by classes, and the wrapping width of a node go through the same units; and a Font
Awesome label in a flowchart and in a mindmap goes through the shared test of the `LabelBuilder`. The test helper
`SceneQuery` reads the nodes, subgraph
rectangles (and the inner areas of composite states), edge paths, edge labels, terminal labels, and markers of the
scene by their Mermaid DOM ids in absolute coordinates.

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
- **Files**: The flowchart fixtures in `Fixtures/flowchart/`, the class diagram fixtures in `Fixtures/class/`, the
  state diagram fixtures in `Fixtures/state/`, the entity relationship diagram fixtures in `Fixtures/er/`, and the
  mindmap fixtures in `Fixtures/mindmap/` of the test project (copied to the output folder)
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
`RenderingTests.Rendering_ClassDiagram_RelationsWithCardinalities_MatchMermaid`,
`RenderingTests.Rendering_StateDiagram_CompositeWithRegions_MatchesMermaid`,
`RenderingTests.Rendering_ErDiagram_EntityTablesAndMarkers_MatchMermaid`

Lays out two subgraphs joined by a link from a node of one to a node of the other (so both are laid out with the root
graph and the link crosses both rectangles), a subgraph laid out on its own in the direction it declares and linked by
its id (the links ending on the box that stands in for it), and a self-loop beside a bent edge, and asserts that the
view box, the node boxes, the subgraph rectangles, and the end points of the edge routes - including the compact loop -
are Mermaid's. Lays out the class fixture `08-cardinality-labels.mmd` (six relations with cardinalities, four of them
labeled) and asserts that the framed diagram has Mermaid's view box of 429.629 by 416 pixels at the origin, the
padding of 8 around the drawing included. Lays out the state fixture `07-concurrency.mmd` (a composite state `Active`
with three concurrent regions) and asserts Mermaid's view box of 673.648 by 637 pixels, the composite state's
rectangle `state-Active-9` at (8, 72), 657.648 by 493 pixels, holding the region `state-divider-id-1-1` at (43, 109.5),
169.25 by 418 pixels, the start state at (329.824, 8) and the end state at (329.824, 615), so that the composite state,
its regions laid out inside it, and the states around it are where Mermaid puts them. Lays out the entity relationship
fixture `06-keys-comments.mmd` (an entity with keys and comments, an entity with neither, one with a key only, and one
with a comment only, joined by three relationships `||--o{` labeled `has`) and asserts Mermaid's
view box of 879.313 by 587.25 pixels, the entities `entity-ACCOUNT-0` at (189.5, 8), `entity-PLAIN-1` at (8, 451), and
`entity-COMMENTED-3` at (604.219, 451), and three drawn relationships.

#### CanvasNetMermaid-Rendering-NodeShapes: Nodes Have Mermaid's Sizes And Places

**Tests**: `RenderingTests.Rendering_Flowchart_LinkedSubgraphs_MatchMermaid`,
`RenderingTests.Rendering_Flowchart_SubgraphWithDirection_MatchesMermaid`,
`RenderingTests.Rendering_Flowchart_ImageNode_SizedByThePictureOfTheResolver`,
`RenderingTests.Rendering_Flowchart_PicturesBeyondMaxImages_FailAtTheNode`,
`RenderingTests.Rendering_ClassDiagram_RelationsWithCardinalities_MatchMermaid`,
`RenderingTests.Rendering_StateDiagram_CompositeWithRegions_MatchesMermaid`,
`RenderingTests.Rendering_ErDiagram_EntityTablesAndMarkers_MatchMermaid`,
`RenderingTests.Rendering_Mindmap_Shapes_MatchMermaid`

Asserts that the boxes of the drawn nodes (rectangles sized from their measured labels and padding, for example 77.797
by 54 pixels) and the subgraph rectangles built around them are Mermaid's, in a top-to-bottom diagram and in a
left-to-right subgraph. Lays out an image node with a label, a height of 60, and the aspect constraint, through a
resolver whose pictures are 40 by 20, and asserts that the resolver is asked once, for `pic.png` at 256 by 256 device
pixels, and that the scene holds one node picture of `pic.png` of 120 by 60 (the natural aspect ratio of 2 at the
given height). Lays out three image nodes with `MermaidLimits.MaxImages` of 2 and asserts that the layout fails with
`mermaid-limit-exceeded` at line 4, the node of the third picture, naming the limit, after the resolver was asked for
the first two pictures only. Asserts that the class boxes of the class fixture are Mermaid's: `classId-Customer-0` at
(292.273, 8), 98.688 by 84 pixels, and `classId-Order-1` at (174.469, 166), 67.578 by 84 pixels (the name and the
empty compartment of each class, sized from the measured name). In the state fixture, asserts the state shapes and
clusters at Mermaid's sizes: the start circle of 14 by 14 pixels, the state `state-NumOff-3` at (92.211, 273.5), 70.828
by 40 pixels (a rounded rectangle with the padding of 8 around its label), the end state of 14.013 by 14 pixels (the
exact rough.js outline), the composite state's titled rectangle with its inner area `state-Active-9` at (8, 98),
657.648 by 463 pixels, and the dashed region of 169.25 by 418 pixels. In the entity relationship fixture, asserts the
entity tables at Mermaid's sizes: `entity-ACCOUNT-0` 373.938 by 342 pixels (the name row and the attribute rows with
their keys and comments), and `entity-PLAIN-1` and `entity-COMMENTED-3` 140.719 and 267.094 by 128.25 pixels. In the
mindmap fixture `03-root-shapes`, drawn through the shared shapes with the mindmap's style sheet and laid out by the
`Cose` subsystem, asserts the view box (5, 5, 656.514, 378.522) and the boxes Mermaid gives its nodes, within 0.002
pixel: the bang root at (317.406, 170.899), 165.918 by 80 pixels, the rectangle at (144.391, 158.435), 116.469 by 44,
the default node with its line at (15, 230.483), 156.531 by 34, the hexagon at (525.378, 101.554), 87.391 by 44, and
the circle at (519.168, 261.341), 56.453 by 56.453, with 6 edges.

#### CanvasNetMermaid-Rendering-Edges: Edges And Their Labels Are Where Mermaid Draws Them

**Tests**: `RenderingTests.Rendering_Flowchart_LinkedSubgraphs_MatchMermaid`,
`RenderingTests.Rendering_Flowchart_SubgraphWithDirection_MatchesMermaid`,
`RenderingTests.Rendering_Flowchart_SelfLoop_MatchesMermaid`,
`RenderingTests.Rendering_Flowchart_EdgeLabels_MatchMermaid`,
`RenderingTests.Rendering_ClassDiagram_RelationsWithCardinalities_MatchMermaid`,
`RenderingTests.Rendering_StateDiagram_CompositeWithRegions_MatchesMermaid`,
`RenderingTests.Rendering_ErDiagram_EntityTablesAndMarkers_MatchMermaid`

Asserts that the commands of the edge paths (the basis curve through the layout's points, ended on the node outlines or
on the box of a subgraph laid out on its own, and pulled back for the arrowheads; and the compact self-loop) end where
Mermaid's do, and that the edge labels of a chain of labeled links (normal, dotted, and thick) are centered where
Mermaid centers them, with the spacing they add between ranks giving Mermaid's view box. Asserts that the label
`places` of the class relation `id_Customer_Order_1` is centered at (277.715, 129), that its cardinalities are the two
terminal labels at (284.976, 96.172) beside its start and (259.867, 161.33) beside its end, and that the class
relations are drawn with markers. Asserts that the state fixture draws 10 transitions with 10 markers, one `barb` at the
end of each. Asserts that each of the 3 relationships of the entity relationship fixture has 3 marker items (the two
bars of exactly one at its start, and the ring and the crow's foot of zero or more at its end), and that the role
label of `id_entity-ACCOUNT-0_entity-KEYED-2_1` is centered at (376.469, 400.5), within 0.002 pixel, where Mermaid
centers it.

#### CanvasNetMermaid-Rendering-Labels: Labels Measure As Mermaid's Labels

**Tests**: `RenderingTests.Rendering_Flowchart_LinkedSubgraphs_MatchMermaid`,
`RenderingTests.Rendering_Flowchart_EdgeLabels_MatchMermaid`,
`RenderingTests.Rendering_Flowchart_UndrawnConstructs_AreReportedAtTheirNodes`,
`RenderingTests.Rendering_TextUtilities_WrapAtWordsAndMeasurePerLine`,
`RenderingTests.Rendering_ClassDiagram_RelationsWithCardinalities_MatchMermaid`,
`RenderingTests.Rendering_StateDiagram_CompositeWithRegions_MatchesMermaid`,
`RenderingTests.Rendering_ErDiagram_EntityTablesAndMarkers_MatchMermaid`,
`RenderingTests.Rendering_FontAwesomeLabel_IsReportedAsAnIcon`

Asserts the sizes that follow from the measured labels: the node widths of the subgraph fixture, which are the HTML
label widths with the padding, and the positions of the edge labels and the width of the view box of the labeled
chain, which follow from the measured edge label boxes. Lays out, without an image resolver, a node with an `<s>`
label, a node whose label has a character that no bundled font draws, and an image node, and asserts that the three
nodes are laid out, that no feature is reported as unsupported, and that the warnings are, in this order,
`mermaid-html-tag-ignored` at line 2, `mermaid-text-missing-glyph` at line 3, and `mermaid-image-unresolved` at line 4.
Wraps a sentence wider than 150 pixels with the text utilities that sequence diagrams use, in the default sequence
font, and asserts that it breaks at its words into several lines that rejoin to the sentence, that the first line is
narrower than the width, and that the wrapped text measures as wide as its widest line and as high as one line height
per line. In the class fixture, the widths of the class boxes (98.688 pixels for `Customer`, 67.578 for `Order`) follow
from the names measured in bold, and the place of the relation label and the routes beside which the cardinalities
stand follow from the measured label `places`, so the same assertions verify the class diagram's labels. In the state
fixture, the width of the state `NumOff` (70.828 pixels, its label plus the padding), the width of the composite
state's rectangle, and the view box follow from the measured state labels and the composite state's title. In the
entity relationship fixture, the widths of the entity tables (373.938, 140.719, and 267.094 pixels) follow from the
measured cell texts, and the center of the role label at (376.469, 400.5) follows from the measured role `has` in the
14 pixel edge label font of the entity relationship sheet, so the same assertions verify the labels of both diagrams.
For a flowchart and a mindmap (a theory with two cases), each with a label `fa:fa-car Car` on its second node at line
3, asserts that both nodes are drawn and that the label gives exactly one `mermaid-icon-unsupported` report at line 3,
found by the shared Font Awesome test of the `LabelBuilder`.
