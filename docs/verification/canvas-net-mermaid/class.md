## Class Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Class` subsystem (internal namespace
`DemaConsulting.CanvasNet.Mermaid.Class`), containing two units: `ClassParser` (the lexer and the grammar of Mermaid's
class language, with the located errors and the nesting and label limits) and `ClassModel` (Mermaid's class database,
the diagram metadata, the node, edge, and label limits, the mapping to the layout data of the shared renderer, the
reports of what is not drawn, and the frame of the drawing).

### Verification Approach

The `Class` subsystem is verified at two tiers. The unit tests (see the unit verification designs under `class/`)
verify each unit, including the 179 upstream vectors recorded from the pinned Mermaid release. The subsystem-level
scenarios in `ClassTests` verify the two units working together with the shared rendering: a whole text is
preprocessed, detected, parsed into the model, and then asked for its database facts, its layout data, its metadata,
its located errors, its reports, or its laid-out scene. The original fixtures in `Fixtures/class/` (16 diagrams written
for this project, one for each area of the language: basic classes, members and methods, visibility and classifiers,
generics, annotations, every relation, two-way relations, cardinalities and labels, lollipop interfaces, namespaces,
notes, the direction, styles, labels and backticked names, hidden empty member boxes, and a configuration with a title
and links) are all parsed, mapped, and laid out.

The expectations are stated in the tests as values worked out from Mermaid's documented semantics and confirmed
against the pinned Mermaid release: the classes, parents, node and edge ids, and terminal labels of a known text, the
metadata of a document, the location of a database or grammar error, the codes and lines of the reports, and the class
boxes of the scene of every fixture. The scenarios use no mocking or stubbing: the preprocessor, the detector, the
configuration resolver, the class style sheet, the shared rendering, and the bundled fonts are real. The subsystem is
internal, so its tests reach the model and the scene through the test assembly's `InternalsVisibleTo` access (the
helpers `ClassFacts`, `SceneQuery`, and `MermaidTestHelpers`).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: The original diagrams in `test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/class/*.mmd`
- **Fonts**: The bundled fonts for the scenarios that measure or lay out
- **Isolation**: Each test opens its own document; the fixtures are only read

### Acceptance Criteria

The `Class` subsystem's verification passes when every subsystem-level scenario below and every unit-level scenario in
the two unit verification documents passes without error or unexpected exception.

### Test Scenarios

#### CanvasNetMermaid-Class-Language: Class Texts Open As Mermaid Reads Them

**Tests**: `ClassTests.Class_Parse_Fixture_GivesClassesAndLayoutData`,
`ClassTests.Class_Parse_WholeText_GivesModelAndLayoutData`, `ClassTests.Class_Parse_LegacyClassId_KeepsItsDiagramType`

Every original fixture (a theory with one case for each file) parses into a model with classes whose layout data has
one class box node for each class, with a minimum length of 1 on every edge. A whole text with a frontmatter title, a
comment, an accessible title and description, a direction, a namespace `Sales` holding a class `Order` with an
attribute and a method, a generic class `Line~T~`, a relation with the cardinalities `1` and `many` and the label
`holds`, and a note gives the classes `Order` and `Line` in that order, `Order` in the namespace `Sales`, the layout
nodes `Sales`, `Order`, `Line`, and `note0`, the edges `edgeNote0` and `id_Order_Line_1`, the terminal labels `1` and
`many` with the label `holds`, and the drawn name `Line&lt;T&gt;`. A text detected as the legacy `class` id (a
`class.defaultRenderer` of `dagre-d3`) parses the same way, and both the model and its layout data keep the type
`class`.

#### CanvasNetMermaid-Class-Metadata: Metadata Without Rendering

**Tests**: `ClassTests.Class_Parse_WholeText_GivesModelAndLayoutData`,
`ClassTests.Class_Document_IsSupportedWithMetadata`

The whole text above gives the direction `LR`, the accessible title `The shop`, and the accessible description
`Orders and lines`. A `classDiagram-v2` document with a direction statement `RL`, a namespace holding one class, a
relation to an implicit class, a lollipop interface, and a note has the type `class`, the supported flag, two nodes
(the classes; the note and the interface are not counted), two edges (the relations; the note link is not counted),
one subgraph, and the direction `RL`, without being rendered.

#### CanvasNetMermaid-Class-FailClosed: Invalid Text Is Refused At Its Place

**Tests**: `ClassTests.Class_Parse_DatabaseError_FailsClosedWithLocation`,
`ClassTests.Class_Parse_GrammarErrorAfterFrontmatter_IsLocatedInTheOriginalText`

An annotation statement `<<interface>> Missing` for a class that does not exist is refused when the document is parsed
with a `MermaidSyntaxException` of code `mermaid-syntax-error` located at line 2, column 19 of the named file (the class
name), carrying Mermaid's message "Cannot read properties of undefined (reading 'annotations')", and no model is
returned. A grammar error after a frontmatter block (`note direction`, a note without its quoted text) is located at
line 5, column 10 of the original text, frontmatter lines included, with exactly `STR` expected. The other grammar
errors, the other database error, and the limits are verified at the unit level (see the ClassParser and ClassModel
unit verification designs) and at the system level by the hardening tests.

#### CanvasNetMermaid-Class-Layout: Class Diagrams Are Laid Out

**Tests**: `ClassTests.Class_Parse_WholeText_GivesModelAndLayoutData`,
`ClassTests.Class_Layout_Fixture_DrawsClassBoxesAndRelations`

The layout data of the whole text above holds the namespace cluster, the class boxes, the note and its link, and the
relation with its terminal labels in Mermaid's order. Every original fixture (a theory with one case for each file)
lays out into a scene with a positive view box, a node for the DOM id of each class, and at least as many edges as
relations. The exact layout data of both namespace modes, the interface nodes, the markers, and the spacing are
verified at the unit level (see the ClassModel unit verification design and the upstream vectors), and the positions
against Mermaid by the system-level reference layout tests.

#### CanvasNetMermaid-Class-Drawing: Class Diagrams Are Drawn

**Tests**: `ClassTests.Class_Layout_Fixture_DrawsClassBoxesAndRelations`

Every fixture draws a class box for each of its classes and its relations into a scene with a positive view box. The
frame of the drawing (the title above it and the padding of the view box) is verified at the unit level (see the
ClassModel unit verification design); the class box shape, the markers, the terminal labels, and the paint of the
class style sheet are verified by the Rendering and Configuration subsystems and against Mermaid by the system-level
reference layout and integration tests.

#### CanvasNetMermaid-Class-UnsupportedFeatures: Looks And Layouts Are Reported

**Tests**: `ClassTests.Class_Render_UnsupportedFeatures_AreReported`

A text with the `handDrawn` look and the `elk` layout in its frontmatter is still measured to a size larger than 16 by
16 pixels, and reports, in this order, `mermaid-look-unsupported` (line 3) and `mermaid-layout-unsupported` (line 4),
with no warnings.

#### CanvasNetMermaid-Class-InteractionIgnored: Click, Link, And Callback Draw And Report Nothing

**Tests**: `ClassTests.Class_Render_InteractionStatements_AreIgnoredSilently`

A text with a `link` statement, a `click ... href` statement, and a `callback` statement, laid out in strict mode,
reports no warning and no unsupported feature, and its scene has the same view box and the same class nodes as the
same text without the three statements.
