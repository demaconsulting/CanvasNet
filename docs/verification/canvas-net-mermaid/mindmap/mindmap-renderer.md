### MindmapRenderer Unit Verification Design

This document describes the unit-level verification strategy for the `MindmapRenderer` unit in the
`DemaConsulting.CanvasNet.Mermaid.Mindmap` namespace: the internal sealed `MindmapRenderer` (the drawing of Mermaid's
mindmap renderer and its cose-bilkent renderer: the node data, the changes per shape, the node sizes in single
precision, the layout call, the edges, the reports, the view box, and the limits and work of a render).

#### Verification Approach

The unit is verified in three ways. The functions that decide the node data are called directly: `DataOf` on every
node of the 246 upstream vectors, compared with the padding, width, look, shape, and DOM id that Mermaid's `getData`
recorded; `Prepared` on every shape; and `Box32` on chosen boxes. The drawing is verified by laying out short texts
through the document and inspecting the scene: the node groups and their parts by their roles and ids, the edges by
their ids, the paint against the theme variables of the configuration, the sizes, and the view box, with the values of
Mermaid's `mindmapRenderer.ts`, `styles.ts`, and shapes for the same input stated in the tests. The reports, the
limits, the pinned work, and cancellation are verified through the document and the report handlers. The positions and
sizes of whole fixtures against the goldens drawn by Mermaid are verified at the system level by the reference layout
tests.

No mocking or stubbing is used; the parser, the configuration resolver, the mindmap style sheet, the shared shapes and
edges, the `Cose` layout, the text metrics, and the bundled fonts are real (the helper `SceneQuery` opens and lays out a
text). The internal types are reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `MindmapRendererTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Mindmap` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: `Fixtures/upstream-vectors/mindmap-parser.json`, copied to the output folder
- **Fonts**: The bundled fonts for the scenarios that lay out
- **Isolation**: Each test draws its own text; the vectors are only read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any padding, width, size, color, stroke width, point, report, location, view box, or work count that differs
from the asserted Mermaid value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Mindmap-MindmapRenderer-NodeData: The Padding And Wrap Width Of Each Shape

**Tests**: `MindmapRendererTests.MindmapRenderer_DataOf_UpstreamVector_MatchesMermaidPaddingAndWidth`,
`MindmapRendererTests.MindmapRenderer_Prepared_PerShapeChanges_MatchDraw`,
`MindmapRendererTests.MindmapRenderer_Draw_Padding_SizesEachShapeAsMermaid`,
`MindmapRendererTests.MindmapRenderer_Draw_LongLabels_WrapAtTheShapesWidth`

Asserts that for every node of every valid vector case the node data has Mermaid's padding (the configured padding,
doubled for a rectangle, a rounded rectangle, and a hexagon), width (`mindmap.maxNodeWidth`), look, shape, and DOM id
under the vector's site configuration; that with a width of 200 and a padding of 20 the rounded rectangle becomes
(width 0, padding 15), the rectangle (0, 10), and the hexagon (0, 20), while `mindmapCircle`, `defaultMindmapNode`,
`cloud`, `bang`, and `circle` keep (200, 20); that raising `mindmap.padding` from 10 to 20 grows a default node by 40
across and 10 down, a hexagon by 30 and 20, and a circle by 20 and 20, and leaves the rectangle and the rounded
rectangle unchanged; and that with `maxNodeWidth` 100 a default node's long label wraps within 140 pixels and a
rectangle's within 160 to 240 (it wraps at `flowchart.wrappingWidth`), on fewer lines.

##### CanvasNetMermaid-Mindmap-MindmapRenderer-LayoutSizes: Sizes In Single Precision, Nodes At Their Centers

**Tests**: `MindmapRendererTests.MindmapRenderer_Box32_RoundsSidesToSinglePrecision`,
`MindmapRendererTests.MindmapRenderer_Draw_ViewBox_IsTheDrawingGrownByThePadding`

Asserts that the box (-50.1, -0.3, 100.2, 0.6) becomes the box whose left is `(float)-50.1` and whose width and height
are the single-precision differences `(float)50.1 - (float)-50.1` and `(float)0.3 - (float)-0.3`, and that the box
(-44.6875, -17, 89.375, 34), on the 1/64 pixel grid, is unchanged; and that the nodes placed at the layout's centers
have the leftmost and topmost box sides at 15, the layout's margin.

##### CanvasNetMermaid-Mindmap-MindmapRenderer-SectionPaint: Nodes Painted By Their Sections

**Tests**: `MindmapRendererTests.MindmapRenderer_Draw_Sections_PaintNodesBySection`

Asserts that the root's body is filled with the theme's `git0`; that the child `a`, whose own class is `section-9`,
takes `cScale10` (the later rule wins); that its child `a1` takes its section's `cScale1`, and the children `b` and `c`
`cScale2` and `cScale3`; that the line under `a1` (a default node) is stroked with `cScaleInv1` 3 pixels wide; and that
the body of `a1` has no stroke.

##### CanvasNetMermaid-Mindmap-MindmapRenderer-Edges: Edges By Depth, Through The Layout's Points

**Tests**: `MindmapRendererTests.MindmapRenderer_Draw_Edges_FollowDepthAndAreNotCut`

Asserts that a chain of seven nodes, each indented one column more than its parent, draws its edges with the widths 11,
8, 5, 2, 3, and 3 (17 less 3 per level, and the sheet's 3 where that would not be positive), without a fill, and that
the first edge starts 15 pixels from its parent's center (cytoscape's point, not the node's outline).

##### CanvasNetMermaid-Mindmap-MindmapRenderer-Reports: Reports In Order

**Tests**: `MindmapRendererTests.MindmapRenderer_Draw_UnsupportedFeatures_AreReportedInOrder`,
`MindmapRendererTests.MindmapRenderer_Draw_PlainMindmap_ReportsNothing`

Asserts that a mindmap with `look: handDrawn`, `layout: elk`, an icon on the node `a`, and the label `fa:fa-car Car`
gives `mermaid-look-unsupported`, `mermaid-layout-unsupported` ("... laid out with cose-bilkent"),
`mermaid-icon-unsupported` ("the icon 'fa fa-book' of node 'a' is not drawn; the node keeps its shape and label", at
line 9, column 10), and `mermaid-icon-unsupported` at line 10, in that order, and still draws its three nodes; and that
a mindmap of every shape with a markdown label gives no warning and no report in strict mode.

##### CanvasNetMermaid-Mindmap-MindmapRenderer-ViewBox: The View Box

**Tests**: `MindmapRendererTests.MindmapRenderer_Draw_ViewBox_IsTheDrawingGrownByThePadding`,
`MindmapRendererTests.MindmapRenderer_Draw_EmptyMindmap_GivesThePaddedEmptyBox`

Asserts that with `mindmap.padding` 7 the view box is the box of the nodes grown by 7 on every side (to 1e-4 pixel);
and that the keyword alone draws nothing, with the view box (-10, -10, 20, 20) at the default padding and
(-5, -5, 10, 10) at a padding of 5.

##### CanvasNetMermaid-Mindmap-MindmapRenderer-Limits: Label Limit, Pinned Work, And Cancellation

**Tests**: `MindmapRendererTests.MindmapRenderer_Draw_LabelOverTheRenderLimit_IsALocatedLimit`,
`MindmapRendererTests.MindmapRenderer_Draw_SmallMindmap_ChargesPinnedWork`,
`MindmapRendererTests.MindmapRenderer_Draw_Canceled_Throws`

Asserts that a document opened under the default limits and rendered with a `MaxLabelLength` of 10 fails with
`mermaid-limit-exceeded` naming `MermaidLimits.MaxLabelLength` at the node's line 3; that a mindmap of three nodes is
drawn with a `MaxLayoutWork` of exactly its work, 1,271 units, and fails with `mermaid-limit-exceeded` at the keyword's
line 1 with one unit less; and that a render whose token is canceled stops with an `OperationCanceledException`.
