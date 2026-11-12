## State Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `State` subsystem (internal namespace
`DemaConsulting.CanvasNet.Mermaid.State`), containing two units: `StateParser` (the lexer and the grammar of Mermaid's
state language, the statement tree, the located errors, and the nesting and label limits) and `StateModel` (Mermaid's
state database and data fetcher, the diagram metadata, the node, edge, nesting, and joined label limits, the mapping
to the layout data of the shared renderer, the reports of what is not drawn, and the frame of the drawing).

### Verification Approach

The `State` subsystem is verified at two tiers. The unit tests (see the unit verification designs under `state/`)
verify each unit, including the 157 upstream vectors recorded from the pinned Mermaid release. The subsystem-level
scenarios in `StateTests` verify the two units working together with the shared rendering: a whole text is
preprocessed, detected, parsed into the model, and then asked for its statement tree, its database facts, its layout
data, its metadata, its located errors, its reports, or its laid-out scene. The original fixtures in `Fixtures/state/`
(14 diagrams written for this project, one for each area of the language: basic transitions, descriptions, nested
composite states, a composite state with its own direction, fork and join, choice, concurrency, notes, styles, the
`stateDiagram` header, self and parallel transitions, start and end states in composite states, a configuration with a
title, and transitions into composite states) are all parsed, mapped, and laid out.

The expectations are stated in the tests as values worked out from Mermaid's documented semantics and confirmed
against the pinned Mermaid release: the states, node ids, and edge ids of a known text, the metadata of a document, the
location of a database error, the codes and lines of the reports, and the nodes, clusters, and edges of the scene of
every fixture. The scenarios use no mocking or stubbing: the preprocessor, the detector, the configuration resolver,
the state style sheet, the shared rendering, and the bundled fonts are real. The subsystem is internal, so its tests
reach the model and the scene through the test assembly's `InternalsVisibleTo` access (the helpers `StateFacts`,
`SceneQuery`, and `MermaidTestHelpers`).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: The original diagrams in `test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/state/*.mmd`
- **Fonts**: The bundled fonts for the scenarios that measure or lay out
- **Isolation**: Each test opens its own document; the fixtures are only read

### Acceptance Criteria

The `State` subsystem's verification passes when every subsystem-level scenario below and every unit-level scenario in
the two unit verification documents passes without error or unexpected exception.

### Test Scenarios

#### CanvasNetMermaid-State-Language: State Texts Open As Mermaid Reads Them

**Tests**: `StateTests.State_Parse_Fixture_GivesConsistentLayoutData`,
`StateTests.State_Parse_WholeText_GivesModelAndLayoutData`

Every original fixture (a theory with one case for each of the 14 files) parses into a model with states whose layout
data has an edge only between its nodes, a parent only where a cluster is, and a shape for every node. A whole text
with a frontmatter title, a comment, an accessible title and description, a `classDef warm fill:#f96`, a start
transition to `Active:::warm`, a composite state `Active` with two concurrent regions (`[*] --> NumOff`, `--`, and
`[*] --> CapsOff`), a note on `Active`, and a click gives the database states `root_start` and `Active`, the layout
nodes `root_start`, `Active`, `divider-id-1`, `divider-id-1_start`, `NumOff`, `id-random-1`, `id-random-1_start`,
`CapsOff`, `Active----parent`, and `Active----note-3` in that order, the compiled style `fill:#f96` on `Active`, the
edges `edge0`, `edge1`, `edge2`, and `Active-Active----note-3`, and one link whose URL is kept as data only as the
shared link check gives it, without its quotes (`https://example.com/`).

#### CanvasNetMermaid-State-Metadata: Metadata Without Rendering

**Tests**: `StateTests.State_Parse_WholeText_GivesModelAndLayoutData`,
`StateTests.State_Document_IsSupportedWithMetadata`

The whole text above gives the accessible title `Keys` and the accessible description `Lock states`. A
`stateDiagram-v2` document with a `direction LR` statement, a start transition to a composite state `Active` with two
concurrent regions each holding a start transition, a transition from `Active` to the end state, and a note on
`Active` has the type `state`, the supported flag, six nodes (the start states, `NumOff`, `CapsOff`, and the end state;
the composite state, the regions, the note, and the note group are not counted), four edges (the transitions; the note
link is not counted), one subgraph (the composite state; the regions are not counted), and the direction `LR`, without
being rendered.

#### CanvasNetMermaid-State-FailClosed: Invalid Text Is Refused At Its Place

**Tests**: `StateTests.State_Parse_DatabaseError_FailsClosedWithLocation`

A composite state `state "T" as C { A }` followed by `C : more` after a frontmatter block, a description beyond the
title of a composite state, is refused when the document is parsed with a `MermaidSyntaxException` of code
`mermaid-syntax-error` located at line 8, column 1 of the named file (the line of `C : more` in the original text,
frontmatter lines included), with Mermaid's message naming the node `[C]`, and no model is returned. The grammar and
lexical errors, the single-word error, and the limits are verified at the unit level (see the StateParser and
StateModel unit verification designs) and at the system level by the hardening tests.

#### CanvasNetMermaid-State-Layout: State Diagrams Are Laid Out

**Tests**: `StateTests.State_Parse_WholeText_GivesModelAndLayoutData`,
`StateTests.State_Layout_Fixture_DrawsStatesAndTransitions`

The layout data of the whole text above holds the start state, the composite state, its two regions with their start
states and states, the note group with its note, and the transitions and the note link in Mermaid's order. Every
original fixture (a theory with one case for each file) lays out into a scene with a positive view box, a node for the
DOM id of every layout node that is not a group (the states and the notes), a cluster for the DOM id of every group
other than a note group (the composite states and the regions), and edges that are all transitions or note links of
the model (two self transitions of one state are one edge, as in Mermaid's graph). The exact layout data, the note
groups, the marker, and the spacing are verified at the unit level (see the StateModel unit verification design and the
upstream vectors), and the positions against Mermaid by the system-level reference layout tests.

#### CanvasNetMermaid-State-Drawing: State Diagrams Are Drawn

**Tests**: `StateTests.State_Layout_Fixture_DrawsStatesAndTransitions`

Every fixture draws a node for each of its states and notes, a cluster for each composite state and region, and its
transitions into a scene with a positive view box. The frame of the drawing (the title above it and the padding of the
view box) and the located failure of a state without a shape are verified at the unit level (see the StateModel unit
verification design); the state shapes, the clusters, the `barb` marker, and the paint of the state style sheet are
verified by the Rendering and Configuration subsystems and against Mermaid by the system-level reference layout and
integration tests.

#### CanvasNetMermaid-State-UnsupportedFeatures: Looks And Layouts Are Reported

**Tests**: `StateTests.State_Render_UnsupportedFeatures_AreReported`

A `stateDiagram` text with the `handDrawn` look, the `elk` layout, and `state.defaultRenderer: dagre-d3` in its
frontmatter is still measured to a size larger than 16 by 16 pixels, and reports, in this order,
`mermaid-layout-unsupported` for the legacy renderer (line 5, the `state` key), `mermaid-look-unsupported` (line 3),
and `mermaid-layout-unsupported` (line 4), with no warnings.

#### CanvasNetMermaid-State-LegacyRenderer: The Legacy Renderer Is Drawn By The Unified Renderer And Reported

**Tests**: `StateTests.State_Parse_LegacyStateId_KeepsItsIdAndTheUnifiedDiagramType`,
`StateTests.State_Render_UnsupportedFeatures_AreReported`

A `stateDiagram` text detected with a `state.defaultRenderer` of `elk` keeps Mermaid's legacy id `state` (the model is
legacy), while its layout data has the unified diagram type `stateDiagram` and the nodes `root_start`, `A`, and `B`
of the translated tree. The legacy renderer is reported first, as `mermaid-layout-unsupported` at the line of the
`state` key, before the look and the layout (see the UnsupportedFeatures scenario above). The legacy order of the
database is verified at the unit level (see the StateModel unit verification design), and the identical pixels of the
legacy and the current header by the system-level integration tests.
