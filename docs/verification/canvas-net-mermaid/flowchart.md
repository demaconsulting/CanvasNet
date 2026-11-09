## Flowchart Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Flowchart` subsystem (internal namespace
`DemaConsulting.CanvasNet.Mermaid.Flowchart`), containing two units: `FlowchartParser` (the lexer and the grammar of
Mermaid's flowchart language, with the located errors and the nesting limit) and `FlowchartModel` (Mermaid's flowchart
database, the diagram metadata, the layout input of a render, the reports of what is not drawn, and the drawing).

### Verification Approach

The `Flowchart` subsystem is verified at two tiers. The unit tests (see the unit verification designs under
`flowchart/`) verify each unit in isolation, including the 207 upstream vectors recorded from the pinned Mermaid
release. The subsystem-level scenarios in `FlowchartTests` verify the two units working together behind the public
`MermaidDocument`: a whole text is preprocessed, detected, parsed into the model, and then asked for its metadata, its
layout input under a configuration resolved as Mermaid resolves it, its reports, or its pixels. The original fixtures in
`Fixtures/flowchart/` (42 diagrams written for this project, one for each area of the language) are all opened and
mapped without a report.

The expectations are stated in the tests as values worked out from Mermaid's documented semantics and confirmed against
the pinned Mermaid release: the counts and direction of a known text, the order and parents of the layout nodes, the
compiled styles and markers of the layout input, the codes and lines of the reports, and the line and column of a
syntax error in the original text after a frontmatter and a comment. The scenarios use no mocking or stubbing: the
preprocessor, the detector, the configuration resolver, the shared rendering, and the bundled fonts are real. The
subsystem is internal, so its tests reach the model through the test assembly's `InternalsVisibleTo` access.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: The original diagrams in `test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/flowchart/*.mmd`
- **Fonts**: The bundled fonts for the scenarios that measure or render
- **Isolation**: Each test opens its own document; the fixtures are only read

### Acceptance Criteria

The `Flowchart` subsystem's verification passes when every subsystem-level scenario below and every unit-level
scenario in the two unit verification documents passes without error or unexpected exception.

### Test Scenarios

#### CanvasNetMermaid-Flowchart-Language: Flowchart Texts Open As Mermaid Reads Them

**Tests**: `FlowchartTests.Flowchart_Open_Fixture_ParsesWithoutReports`,
`FlowchartTests.Flowchart_Fixtures_AreAboutForty`, `FlowchartTests.Flowchart_GetData_WholeText_MapsLikeMermaid`

Every original fixture (between 40 and 60 diagrams, one for each area of the language: shapes, links, subgraphs,
styles, labels, and directions) opens as a supported flowchart with at least two nodes, its layout input has one node
for each vertex and subgraph and one edge for each edge, and nothing is reported; a whole text with a directive, a
subgraph, a class, a style, and two link forms gives the vertices, parents, classes, styles, and link patterns that
Mermaid's database gives.

#### CanvasNetMermaid-Flowchart-Metadata: Metadata Without Rendering

**Tests**: `FlowchartTests.Flowchart_Open_Document_GivesMetadata`

A text with a frontmatter title, an accessible title, a subgraph, and a node chain gives the type `flowchart`, the
supported flag, the direction `LR`, the frontmatter title, the accessible title, and four nodes, three edges, and one
subgraph, without being rendered.

#### CanvasNetMermaid-Flowchart-FailClosed: Invalid Text Is Refused At Its Place

**Tests**: `FlowchartTests.Flowchart_Open_SyntaxErrorAfterFrontmatter_IsLocatedInOriginalText`,
`FlowchartTests.Flowchart_Open_OverLimits_IsRefusedAtTheElement`,
`FlowchartTests.Flowchart_Open_DatabaseErrors_AreLocatedWithHints`

A link to a bracket without an id, after a frontmatter and a comment, is refused when the document is opened with a
`MermaidSyntaxException` located at line 7, column 9 of the original file, whose message starts with the location and
the code `mermaid-syntax-error` and whose expected tokens include `NODE_STRING`. A chain of three links after a
frontmatter, whose last node has a label longer than ten characters, is refused when it is opened with
`MermaidLimits.MaxEdges` of 2, `MaxNodes` of 3, or `MaxLabelLength` of 10, each time with `mermaid-limit-exceeded`
at line 7 of the named file (the third link, the fourth node, and the long label) and a message that names the limit.
After a comment line, a subgraph without an id or a title is refused at its keyword (line 3, column 3) with a message
that asks for an id or a title and a hint, and an unknown shape name is refused with `mermaid-shape-unknown` at its
value (line 3, column 14) with a hint that names the nearest shape, `rectangle`. The nesting limit and the other
refusals are verified at the unit level (see the FlowchartParser and FlowchartModel unit verification designs).

#### CanvasNetMermaid-Flowchart-LayoutData: The Layout Input Is Mermaid's

**Tests**: `FlowchartTests.Flowchart_GetData_WholeText_MapsLikeMermaid`,
`FlowchartTests.Flowchart_Open_Fixture_ParsesWithoutReports`

Under the configuration of a directive (curve `linear`, padding 9), the layout input lists the cluster first and then
the vertices with their parents, the compiled class styles and classes of a vertex with `:::`, the raw style of a
`style` statement, the padding of the configuration, the markers of a point arrow and of a dotted cross arrow, the
curve of the configuration on every edge, and the dotted pattern; every fixture maps to as many layout nodes and edges
as the document counts.

#### CanvasNetMermaid-Flowchart-UnsupportedFeatures: Looks, Layouts, And Icons Are Reported

**Tests**: `FlowchartTests.Flowchart_Render_UnsupportedFeatures_AreReported`,
`FlowchartTests.Flowchart_Render_StrictMode_ThrowsAtFirstUnsupportedFeature`,
`FlowchartTests.Flowchart_Render_ElkKeyword_ReportsLayout`

A text with the `handDrawn` look, the `elk` layout, a node icon, and a Font Awesome label is still measured, and
reports, in this order, `mermaid-look-unsupported` (line 3), `mermaid-layout-unsupported` (line 4), and two
`mermaid-icon-unsupported` (line 7), all in the named source file, with no warning; in strict mode the render stops
with a `MermaidUnsupportedFeatureException` for the look at line 3; and the `flowchart-elk` keyword opens as a
flowchart with direction `TB` that is rendered with dagre and reports the layout once.

#### CanvasNetMermaid-Flowchart-Drawing: Flowcharts Are Measured And Drawn

**Tests**: `FlowchartTests.Flowchart_Render_ElkKeyword_ReportsLayout`,
`FlowchartTests.Flowchart_Render_UnsupportedFeatures_AreReported`

A flowchart renders to a surface that is not fully transparent, and a flowchart with a look, a layout, and icons that
are not supported measures to a size larger than its padding. The exact geometry of the drawing, the title, and the
view box is verified against Mermaid by the system-level reference layout tests and the rendering subsystem tests.
