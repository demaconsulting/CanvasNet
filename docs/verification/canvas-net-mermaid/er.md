## Er Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Er` subsystem (internal namespace
`DemaConsulting.CanvasNet.Mermaid.Er`), containing two units: `ErParser` (the lexer and the grammar of Mermaid's ER
language, with the values of the statements, the located errors, and the nesting and label limits) and `ErModel`
(Mermaid's ER database, the diagram metadata, the node, edge, and label limits, the mapping to the layout data of the
shared renderer, the reports of what is not drawn, and the frame of the drawing).

### Verification Approach

The `Er` subsystem is verified at two tiers. The unit tests (see the unit verification designs under `er/`) verify
each unit, including the 246 upstream vectors recorded from the pinned Mermaid release. The subsystem-level scenarios
in `ErTests` verify the two units working together with the shared rendering: a whole text is preprocessed, detected,
parsed into the model, and then asked for its database facts, its layout data, its metadata, its located errors, its
reports, or its laid-out scene. The original fixtures in `Fixtures/er/` (12 diagrams written for this project, one for
each area of the language: basic relationships, the cardinalities in symbols, the cardinalities in words, identifying
and non-identifying relationships, attributes, keys and comments, aliases, the direction, styles, generic and
backticked types, subgraphs, and a configuration with a title, the accessibility texts, and SVG text) are all parsed,
mapped, and laid out. The located limits of whole documents under the default limits are verified at the system level
by the hardening tests.

The expectations are stated in the tests as values worked out from Mermaid's documented semantics and confirmed
against the pinned Mermaid release: the entities, node ids, parents, aliases, classes, styles, markers, patterns, and
labels of a known text, the metadata of a document, the location and expected tokens of a grammar error, the codes and
lines of the reports, and the nodes, clusters, and edges of the scene of every fixture. The scenarios use no mocking or
stubbing: the preprocessor, the detector, the configuration resolver, the ER style sheet, the shared rendering, and the
bundled fonts are real. The subsystem is internal, so its tests reach the model and the scene through the test
assembly's `InternalsVisibleTo` access (the helpers `ErFacts`, `SceneQuery`, and `MermaidTestHelpers`).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: The original diagrams in `test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/er/*.mmd`
- **Fonts**: The bundled fonts for the scenarios that measure or lay out
- **Isolation**: Each test opens its own document; the fixtures are only read

### Acceptance Criteria

The `Er` subsystem's verification passes when every subsystem-level scenario below and every unit-level scenario in
the two unit verification documents passes without error or unexpected exception.

### Test Scenarios

#### CanvasNetMermaid-Er-Language: ER Texts Open As Mermaid Reads Them

**Tests**: `ErTests.Er_Parse_Fixture_GivesConsistentLayoutData`, `ErTests.Er_Parse_WholeText_GivesModelAndLayoutData`

Every original fixture (a theory with one case for each of the 12 files) parses into a model with at least one entity
whose layout data is consistent: both ends of every edge are nodes of the data, the parent of every node is one of its
clusters, and every node that is not a cluster is an `erBox` node with its entity box data. A whole text with a
frontmatter title, a comment, an accessible title and description, a `classDef warm fill:#f96`, an entity `c` with the
alias `"Customer"`, the class `warm`, and an attribute `string name PK "full name"`, a subgraph `Sales["Sales area"]`
holding the relationship `c ||--o{ ORDER : places`, and the relationship in words
`ORDER one or more optionally to zero or one INVOICE : "billed by"` gives the entities `c`, `ORDER`, and `INVOICE` in
that order, the layout nodes `Sales`, `entity-c-0`, `entity-ORDER-1`, and `entity-INVOICE-2` with the parents none,
`Sales`, `Sales`, and none, the alias `Customer` with the CSS classes `default warm` and the compiled style
`fill:#f96`, the edges `only_one` to `zero_or_more` (solid) and `one_or_more` to `zero_or_one` (dashed), and the
labels `places` and `billed by`.

#### CanvasNetMermaid-Er-Metadata: Metadata Without Rendering

**Tests**: `ErTests.Er_Parse_WholeText_GivesModelAndLayoutData`, `ErTests.Er_Document_IsSupportedWithMetadata`

The whole text above gives the accessible title `Shop model` and the accessible description `Customers and orders`.
An `erDiagram` document with a `direction LR` statement, a subgraph `Sales` holding `ORDER`, and the relationships
`CUSTOMER ||--o{ ORDER : places` and `ORDER ||--|{ LINE : holds` has the type `er`, the supported flag, three nodes
(the entities `ORDER`, `CUSTOMER`, and `LINE`), two edges, one subgraph, and the direction `LR`, without being
rendered. That an entity only named after a subgraph is not counted is verified at the unit level (see the ErModel unit
verification design).

#### CanvasNetMermaid-Er-FailClosed: Invalid Text Is Refused At Its Place

**Tests**: `ErTests.Er_Parse_GrammarError_FailsClosedWithLocation`

A relationship without its colon and role (`C ||--o{ D`) after a valid relationship and a frontmatter block is refused
when the document is parsed with a `MermaidSyntaxException` of code `mermaid-syntax-error` in the named file `e.mmd`,
located at line 6 of the original text, frontmatter lines included, with exactly `COLON` and `STYLE_SEPARATOR`
expected, and no model is returned. The other grammar errors, the lexical errors, and the limits are verified at the
unit level (see the ErParser and ErModel unit verification designs) and at the system level by the hardening tests.

#### CanvasNetMermaid-Er-Layout: ER Diagrams Are Laid Out

**Tests**: `ErTests.Er_Parse_WholeText_GivesModelAndLayoutData`,
`ErTests.Er_Layout_Fixture_DrawsEntitiesAndRelationships`

The layout data of the whole text above holds the subgraph cluster first, then the entity boxes with their parents,
alias, classes, and compiled styles, and the relationships with the markers of their cardinalities, their line
patterns, and their roles, in Mermaid's order. Every original fixture (a theory with one case for each file) lays out
into a scene with a positive view box, one node for each drawn entity of the model, one cluster for each distinct
subgraph id, and one edge for each relationship. The exact layout data of clusters, entity boxes, edges, and spacing
is verified at the unit level (see the ErModel unit verification design and the upstream vectors), and the positions
against Mermaid by the system-level reference layout tests.

#### CanvasNetMermaid-Er-Drawing: ER Diagrams Are Drawn

**Tests**: `ErTests.Er_Layout_Fixture_DrawsEntitiesAndRelationships`

Every fixture draws a node for each of its drawn entities, a cluster for each of its subgraphs, and an edge for each of
its relationships into a scene with a positive view box. The frame of the drawing (the title above it and the padding
of the view box) is verified at the unit level (see the ErModel unit verification design); the entity box shape, the
ER markers, and the paint of the ER style sheet are verified by the Rendering and Configuration subsystems and against
Mermaid by the system-level reference layout and integration tests.

#### CanvasNetMermaid-Er-UnsupportedFeatures: Looks And Layouts Are Reported

**Tests**: `ErTests.Er_Render_UnsupportedFeatures_AreReported`

A text with the `handDrawn` look and the `elk` layout in its frontmatter is still measured to a size larger than 16 by
16 pixels, and reports, in this order, `mermaid-look-unsupported` (line 3) and `mermaid-layout-unsupported` (line 4),
with no warnings.
