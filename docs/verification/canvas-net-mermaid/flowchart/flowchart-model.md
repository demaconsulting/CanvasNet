### FlowchartModel Unit Verification Design

This document describes the unit-level verification strategy for the `FlowchartModel` unit in the
`DemaConsulting.CanvasNet.Mermaid.Flowchart` namespace: the internal sealed `FlowchartModel` (the database operations,
the metadata, `GetData`, `ReportUnsupported`, and `Layout`, in three partial files) and its element types
(`FlowVertex`, `FlowEdge`, `FlowSubgraph`, `FlowClass`, `FlowText`, `FlowLink`, `ShapeDataText`, and `DocumentItem`).

#### Verification Approach

The unit is verified by calling the database operations directly on an empty model, or by opening short texts and
inspecting the model, and comparing the result with the values that Mermaid's `flowDb.ts` gives for the same calls,
stated in the tests as literals: the arrow type, stroke, and length of 15 link tokens; the DOM ids `flowchart-A-0` and
`flowchart-B-1` and the edge ids `L_a_b_0`, `L_a_b_2`, `e1`, and `L_a_b_4`; the split and text styles of a `classDef`;
the generated id `subGraph0`; the shape names of the layout input; and the codes and lines of the reports. The layout
input is asked for under configurations resolved as Mermaid resolves them (the configuration resolver of the
`Configuration` subsystem, without a site configuration). The model's agreement with Mermaid on whole texts is
verified as well by the upstream vectors of the FlowchartParser unit, whose facts include the model's layout input.

No mocking or stubbing is used; the YAML reader, the sanitizer, the configuration resolver, and the shared rendering
are real. The internal types are reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `FlowchartModelTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Flowchart` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled fonts for the scenario that renders
- **Isolation**: Each test builds its own model; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any id, type, style, shape, order, or report that differs from the asserted Mermaid value constitutes a
failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Flowchart-FlowchartModel-Links: Link Tokens And Edge Ids

**Tests**: `FlowchartModelTests.FlowchartModel_DestructLink_Token_GivesTypeStrokeAndLength`,
`FlowchartModelTests.FlowchartModel_AddLink_RepeatedPairsAndIds_GetMermaidIds`

Asserts the arrow type, stroke, and length of open, point, double, cross, and circle links in the normal, dotted,
thick, and invisible strokes, with and without the start of a link with text, and that a start and end that do not
match are invalid without a length; and that four links between the same nodes get `L_a_b_0`, `L_a_b_2`, `e1` (marked
as given by the author), and `L_a_b_4` when the id `e1@` is given twice.

##### CanvasNetMermaid-Flowchart-FlowchartModel-Vertices: A Vertex Declared Again

**Tests**: `FlowchartModelTests.FlowchartModel_AddVertex_Redeclared_MergesIntoFirst`

Asserts that a vertex declared with a label and shape, then again with a quoted label, another shape, and a style, then
mentioned without a label keeps its DOM id `flowchart-A-0`, has the unquoted second label of type `string`, the second
shape, and the style, and that the vertex declared between them has the DOM id `flowchart-B-1`.

##### CanvasNetMermaid-Flowchart-FlowchartModel-Styles: Class And Link Styles

**Tests**: `FlowchartModelTests.FlowchartModel_AddClass_Styles_SplitAndTextStyles`,
`FlowchartModelTests.FlowchartModel_UpdateLink_SeveralEdges_ShareOneList`,
`FlowchartModelTests.FlowchartModel_UpdateLink_IndexOutOfRange_ThrowsLocated`

Asserts that a `classDef` of two classes splits its styles at commas while an escaped comma stays inside a font family
and that only the color style is also a text style; that two edges styled by one statement share one list that ends
with `fill:none`; and that the indexes `1` (beyond the only edge) and `00` (not a plain index) are refused with a
`MermaidSyntaxException` at the location of the statement whose hint gives the valid range.

##### CanvasNetMermaid-Flowchart-FlowchartModel-Subgraphs: Subgraph Ids And Members

**Tests**: `FlowchartModelTests.FlowchartModel_AddSubgraph_IdsAndMembers_FollowMermaid`

Asserts that a subgraph whose title with white space is its id gets the id `subGraph0`, that a later subgraph does not
list a member of the earlier one again but keeps the texts of its separators that are not blank, and that the later
subgraph has the direction `LR` of its direction statement and the label type `markdown` of its title.

##### CanvasNetMermaid-Flowchart-FlowchartModel-Metadata: Counts And Titles

**Tests**: `FlowchartModelTests.FlowchartModel_Counts_ExcludeSubgraphVertices`

Asserts that a subgraph whose id is used as the start of a link gives two nodes, one edge, and one subgraph, and that
a flowchart without accessibility statements has no accessible title and no title of its own.

##### CanvasNetMermaid-Flowchart-FlowchartModel-LayoutData: The Layout Input Of A Render

**Tests**: `FlowchartModelTests.FlowchartModel_GetData_Shapes_FollowGetTypeFromVertex`,
`FlowchartModelTests.FlowchartModel_GetData_CollapsedSubgraph_RedirectsEdges`,
`FlowchartModelTests.FlowchartModel_GetData_InheritDirection_GivesDiagramDirection`,
`FlowchartModelTests.FlowchartModel_GetData_SubgraphsNestedThroughIds_ThrowsLimitExceeded`

Asserts the shapes `squareRect`, `roundedRect`, `ellipse`, `imageSquare`, `iconCircle`, and `diam` for a square
bracket, a round bracket, the ellipse bracket, an image, an icon with the circle form, and a shape alias, the markers
`point`, `circle`, and `cross`, the direction `LR`, the spacings 50, and the layout `dagre`; that a collapsed subgraph
is one node of shape `collapsedGroup` that is not a group, that its members are hidden, that the edges into and out of
it end at it, and that no cluster remains; that a subgraph without a direction has none by default and the diagram
direction `LR` when `flowchart.inheritDir` is set; and that, with `MaxNestingDepth` of 3, four subgraphs chained by id
(`subgraph Si` listing `S(i+1)`, the last listing `X`) fail with `mermaid-limit-exceeded` located at `X` (line 12) and
a message naming `MermaidLimits.MaxNestingDepth`, also when the root `S0` is collapsed and hides the whole chain from
the layout data, while a chain of three passes with `X` in `S2`, `S2` in `S1`, and
`S1` in `S0`, and a subgraph re-used inside one declared after it is nested in it.

##### CanvasNetMermaid-Flowchart-FlowchartModel-Unsupported: Reports In Order

**Tests**: `FlowchartModelTests.FlowchartModel_ReportUnsupported_LookLayoutAndIcons_AreReportedInOrder`

Asserts that the `handDrawn` look, the `elk` layout, a node icon, and a Font Awesome label are reported as
`mermaid-look-unsupported`, `mermaid-layout-unsupported`, and two `mermaid-icon-unsupported`, at the lines 3, 4, 7,
and 7, each with a hint. The detection of Font Awesome icons in a label is the shared
`LabelBuilder.ContainsFontAwesome`, verified with the LabelBuilder unit.

##### CanvasNetMermaid-Flowchart-FlowchartModel-Drawing: Reports, Then The Drawing

**Tests**: `FlowchartModelTests.FlowchartModel_Layout_IconLabel_ReportsThenRenders`

Asserts that rendering a flowchart with a Font Awesome label delivers exactly one `mermaid-icon-unsupported` report and
a surface that is not fully transparent. The placement of the title and the view box are verified against Mermaid by
the rendering subsystem tests and the system-level reference layout tests.
