## Mindmap Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Mindmap` subsystem (internal namespace
`DemaConsulting.CanvasNet.Mermaid.Mindmap`), containing two units: `MindmapParser` (the lexer and the grammar of
Mermaid's mindmap language, the mindmap database with the sections, classes, and edges of its `getData`, the metadata,
the located errors, and the node, depth, and label limits) and `MindmapRenderer` (the drawing of Mermaid's mindmap
renderer: the node data and the changes per shape, the node sizes given to the layout, the section paint, the edges,
the reports, and the view box).

### Verification Approach

The `Mindmap` subsystem is verified at two tiers. The unit tests (see the unit verification designs under `mindmap/`)
verify each unit, including the 246 upstream vectors recorded from the pinned Mermaid release. The subsystem-level
scenarios in `MindmapTests` verify the two units working together through the document: a whole text is opened as a
`MermaidDocument` (preprocessed, detected, and parsed into the diagram), and then asked for its metadata, its located
errors, its reports, or its laid-out scene. The 14 original fixtures in `Fixtures/mindmap/` are opened, rendered, and
compared with the goldens drawn by Mermaid at the system level by the hardening and reference layout tests; the
subsystem tests use short inline texts: Mermaid's documentation example with a circle root and a line break, every
shape, a mindmap with SVG labels, a padding of 7, and a `maxNodeWidth` of 90, and a single node.

The expectations are stated in the tests as values worked out from Mermaid's documented semantics and confirmed
against the pinned Mermaid release: the node and edge ids of the scene, the counts and texts of a document, the
reports and their lines, and the codes and locations of invalid texts. The scenarios use no mocking or stubbing: the
preprocessor, the detector, the configuration resolver, the mindmap style sheet, the shared shapes and edges, the
`Cose` layout, the text metrics, and the bundled fonts are real. The subsystem is internal, so its tests reach the
diagram and the scene through the test assembly's `InternalsVisibleTo` access (the helper `SceneQuery`).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled fonts for the scenarios that lay out
- **Isolation**: Each test opens its own document from inline text; no files are read

### Acceptance Criteria

The `Mindmap` subsystem's verification passes when every subsystem-level scenario below and every unit-level scenario
in the two unit verification documents passes without error or unexpected exception.

### Test Scenarios

#### CanvasNetMermaid-Mindmap-Language: Mindmap Texts Open As Mermaid Reads Them

**Tests**: `MindmapTests.CanvasNetMermaid_Mindmap_Text_ParsesAndLaysOutConsistently`,
`MindmapTests.CanvasNetMermaid_Mindmap_Text_GivesTheDiagramAndTheScene`

Every inline text (a theory with four cases: Mermaid's documentation example, every shape, SVG labels with a padding of
7 and a `maxNodeWidth` of 90, and a single node) opens as a document whose diagram is a `MindmapDiagram` and lays out
into one node group per node of the diagram (`node_<id>`) and one edge per edge of the diagram (`edge_<parent>_<child>`),
every node box inside the view box, and lays out a second time into the same view box and the same nodes. The exact
tree, shapes, texts, icons, classes, and sections of every kind of statement are verified at the unit level against
Mermaid's database (see the MindmapParser unit verification design and the upstream vectors).

#### CanvasNetMermaid-Mindmap-Metadata: Metadata Without Rendering

**Tests**: `MindmapTests.CanvasNetMermaid_Mindmap_Text_GivesTheDiagramAndTheScene`

The text `mindmap` with the nodes `Root`, `**Bold** child`, `other[Square]`, and `leaf` (under `other`) gives, without
being rendered, the type `mindmap`, supported, with 4 nodes, 3 edges, and no subgraphs. That an empty mindmap has no
nodes and no edges is verified at the unit level (see the MindmapParser unit verification design).

#### CanvasNetMermaid-Mindmap-FailClosed: Invalid Text Is Refused At Its Place

**Tests**: `MindmapTests.CanvasNetMermaid_Mindmap_InvalidText_FailsClosedWithLocation`

A second root after a frontmatter block is refused when the document is opened with a `MermaidSyntaxException` of
code `mermaid-syntax-error` in the named file `m.mmd`, located at line 6 of the original text, frontmatter lines
included, with Mermaid's "There can be only one root" message and a hint; and an unclosed `b[unclosed` is refused with
the same code, expected tokens, and a hint. No document is returned. The other grammar and lexical errors, the
decorations before any node, and the open-time limits are verified at the unit level (see the MindmapParser unit
verification design), the render-time label limit by the MindmapRenderer unit verification design, and the limits of
whole documents at the system level by the hardening tests.

#### CanvasNetMermaid-Mindmap-Drawing: Mindmaps Are Drawn As Mermaid Draws Them

**Tests**: `MindmapTests.CanvasNetMermaid_Mindmap_Text_ParsesAndLaysOutConsistently`,
`MindmapTests.CanvasNetMermaid_Mindmap_Text_GivesTheDiagramAndTheScene`

The inline texts lay out with every node and edge drawn inside the view box, the same way twice; the text above draws
the labels `Root`, `Bold child` (its first run bold, from the markdown), `Square`, and `leaf` in node order and the
edges `edge_0_1`, `edge_0_2`, and `edge_2_3`. The node data, the sizes in single precision, the section paint, the edge
widths and routes, and the view box are verified at the unit level (see the MindmapRenderer unit verification design),
the paint of the mindmap style sheet by the Configuration subsystem, the shapes by the Rendering subsystem, the layout
by the Cose subsystem, and the sizes, positions, and paint against Mermaid's goldens by the system-level reference
layout tests.

#### CanvasNetMermaid-Mindmap-UnsupportedFeatures: Unsupported Features Are Reported And The Mindmap Drawn

**Tests**: `MindmapTests.CanvasNetMermaid_Mindmap_UnsupportedFeatures_AreReportedAndDrawn`

A mindmap whose frontmatter sets `layout: elk` and whose second node has `::icon(fa fa-book)` is laid out with its two
nodes and one edge, with no warning and the two unsupported features `mermaid-layout-unsupported` at line 3 (the
`layout` key) and `mermaid-icon-unsupported` at line 8 (the icon), in that order. The look and the Font Awesome label
reports and their order are verified at the unit level (see the MindmapRenderer unit verification design).
