## Sequence Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Sequence` subsystem (internal namespace
`DemaConsulting.CanvasNet.Mermaid.Sequence`), containing four units: `SequenceParser` (the lexer and the grammar of
Mermaid's sequence language, with the located errors and the limits), `SequenceModel` (Mermaid's sequence database,
the diagram metadata, and the reports of what is not drawn), `SequenceLayout` (Mermaid's sequence renderer: the
placement of every element and the view box), and `SequenceRenderer` (Mermaid's sequence drawing functions as scene
items).

### Verification Approach

The `Sequence` subsystem is verified at two tiers. The unit tests (see the unit verification designs under
`sequence/`) verify each unit, including the 242 upstream vectors recorded from the pinned Mermaid release. The
subsystem-level scenarios in `SequenceTests` verify the four units working together: a whole text is preprocessed,
detected, parsed into the model, and then asked for its database facts, its metadata, its located errors, its reports,
or its laid-out scene. The original fixtures in `Fixtures/sequence/` (24 diagrams written for this project, one for
each area of the language: arrows, participants and their types, activations, notes, self messages, blocks,
backgrounds, boxes, numbering, created and destroyed participants, configuration keys, wrapping, menus, and titles)
are all parsed and laid out.

The expectations are stated in the tests as values worked out from Mermaid's documented semantics and confirmed
against the pinned Mermaid release: the counts and participant types of a known text, the location of a database
error, the codes and lines of the reports, and the participants of the scene of every fixture. The scenarios use no
mocking or stubbing: the preprocessor, the detector, the configuration resolver, the style sheet, the text utilities,
and the bundled fonts are real. The subsystem is internal, so its tests reach the model and the scene through the
test assembly's `InternalsVisibleTo` access (the helpers `SequenceFacts` and `SceneQuery`).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: The original diagrams in `test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/sequence/*.mmd`
- **Fonts**: The bundled fonts for the scenarios that measure or lay out
- **Isolation**: Each test opens its own document; the fixtures are only read

### Acceptance Criteria

The `Sequence` subsystem's verification passes when every subsystem-level scenario below and every unit-level
scenario in the four unit verification documents passes without error or unexpected exception.

### Test Scenarios

#### CanvasNetMermaid-Sequence-Language: Sequence Texts Open As Mermaid Reads Them

**Tests**: `SequenceTests.Sequence_Parse_Fixture_GivesParticipantsAndMessages`,
`SequenceTests.Sequence_Parse_WholeText_GivesMetadata`

Every original fixture (a theory with one case for each file) parses into a model with participants and messages; a
whole text with a frontmatter configuration, an init directive, a comment, a title, an accessible title, a box, an
actor, activations, a note, and an implicit participant gives the participants of types `actor`, `participant`, and
`participant` in that order.

#### CanvasNetMermaid-Sequence-Metadata: Metadata Without Rendering

**Tests**: `SequenceTests.Sequence_Parse_WholeText_GivesMetadata`, `SequenceTests.Sequence_Document_IsSupportedWithMetadata`

The whole text above gives the title `Checkout`, the accessible title `Paying for an order`, and three participants,
three messages, and one box; a document with a box, a note, and two messages has the type `sequence`, the supported
flag, two nodes, two edges, one subgraph (the note is not counted), and no direction, without being rendered.

#### CanvasNetMermaid-Sequence-FailClosed: Invalid Text Is Refused At Its Place

**Tests**: `SequenceTests.Sequence_Parse_DatabaseError_FailsClosedWithLocation`

A deactivation of a participant that is not active (an arrow with `-` before any activation) is refused when the
document is parsed with a `MermaidSyntaxException` of code `mermaid-syntax-error` located at line 3, column 5 of the
named file, with a hint, and no model is returned. The grammar errors, the other database errors, and the limits are
verified at the unit level (see the SequenceParser and SequenceModel unit verification designs) and at the system
level by the hardening tests.

#### CanvasNetMermaid-Sequence-Layout: Sequence Diagrams Are Laid Out

**Tests**: `SequenceTests.Sequence_Layout_Fixture_DrawsParticipants`

Every original fixture (a theory with one case for each file) lays out into a scene with a positive view box and at
least one participant drawn at the top. The exact geometry of the participants, messages, notes, activations, block
frames and rect backgrounds, boxes, menus, created and destroyed participants, sequence numbers, and the view box is
verified at the unit level (see the SequenceLayout unit verification design) and against Mermaid by the system-level
reference layout tests.

#### CanvasNetMermaid-Sequence-Drawing: Sequence Diagrams Are Drawn

**Tests**: `SequenceTests.Sequence_Layout_Fixture_DrawsParticipants`

Every fixture draws its participants into the scene. The participant types, arrows and markers, sequence numbers,
frames, backgrounds, boxes, paint, paint order, text placements, and menus are verified at the unit level (see the
SequenceRenderer unit verification design) and against Mermaid by the system-level reference layout tests, whose
goldens include the paint and the paint order.

#### CanvasNetMermaid-Sequence-UnsupportedFeatures: Looks, Icons, Math, And Unknown Types Are Reported

**Tests**: `SequenceTests.Sequence_Render_UnsupportedFeatures_AreReported`

A text with the `handDrawn` look in its frontmatter, a participant whose `properties` icon names an image, a
participant of the unknown type `queues`, and a message with `$$` math is still measured to a size larger than 16 by
16 pixels, and reports, in this order, `mermaid-look-unsupported` (line 3), `mermaid-icon-unsupported` (line 6, the
participant's declaration), and `mermaid-math-unsupported` (line 9), all in the named source file, and the one
warning `mermaid-participant-type-unknown` at line 8.
