## Packet Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Packet` subsystem (internal namespace
`DemaConsulting.CanvasNet.Mermaid.Packet`), containing two units: `PacketParser` (the lexer and the grammar of
Mermaid's packet language, the value converters, the checks of the packet database, the diagram metadata, the row
split, the located errors, and the node and label limits) and `PacketRenderer` (the drawing of Mermaid's packet
renderer: the rectangles, labels, and bit numbers of each row, the title row, the paint, and the view box).

### Verification Approach

The `Packet` subsystem is verified at two tiers. The unit tests (see the unit verification designs under `packet/`)
verify each unit, including the 162 upstream vectors recorded from the pinned Mermaid release. The subsystem-level
scenarios in `PacketTests` verify the two units working together through the document: a whole text is opened as a
`MermaidDocument` (preprocessed, detected, and parsed into the diagram), and then asked for its metadata, its located
errors, or its laid-out scene under a render configuration. The original fixtures in `Fixtures/packet/` (13 diagrams
written for this project, one for each area of the language and the settings: a basic frame, the `packet` keyword with
a title and the accessibility texts, bit counts and single bits, row splits, the bit numbers turned off, the geometry
settings, a frontmatter title, the dark and forest themes, theme variables, labels with escapes and entities, the
keyword alone, and `themeCSS`) are all opened and laid out. The reference layout of the fixtures against the goldens
drawn by Mermaid and the located limits of whole documents under the default limits are verified at the system level by
the reference layout and hardening tests.

The expectations are stated in the tests as values worked out from Mermaid's documented semantics and confirmed
against the pinned Mermaid release: the labels of every fixture, the metadata of a document, the rows, labels, and bit
numbers of a known text, the paint of each theme, and the location and code of an invalid text. The scenarios use no
mocking or stubbing: the preprocessor, the detector, the configuration resolver, the packet style sheet, the text
metrics, and the bundled fonts are real. The subsystem is internal, so its tests reach the diagram and the scene
through the test assembly's `InternalsVisibleTo` access (the helper `SceneQuery`).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: The original diagrams in `test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/packet/*.mmd`
- **Fonts**: The bundled fonts for the scenarios that lay out
- **Isolation**: Each test opens its own document; the fixtures are only read

### Acceptance Criteria

The `Packet` subsystem's verification passes when every subsystem-level scenario below and every unit-level scenario
in the two unit verification documents passes without error or unexpected exception.

### Test Scenarios

#### CanvasNetMermaid-Packet-Language: Packet Texts Open As Mermaid Reads Them

**Tests**: `PacketTests.CanvasNetMermaid_Packet_Fixture_ParsesAndLaysOutConsistently`,
`PacketTests.CanvasNetMermaid_Packet_Text_GivesTheDiagramAndTheScene`

Every original fixture (a theory with one case for each of the 13 files) opens as a document whose diagram is a packet
diagram, and the `packet-beta` text with the title `Header` and the blocks `0-23: "Address"`, `+16: "Length"`, and
`40: "F"` opens as a document of the type `packet` with three blocks and the title `Header`. The exact blocks and texts
of every kind of statement are verified at the unit level against Mermaid's database (see the PacketParser unit
verification design and the upstream vectors).

#### CanvasNetMermaid-Packet-Metadata: Metadata Without Rendering

**Tests**: `PacketTests.CanvasNetMermaid_Packet_Text_GivesTheDiagramAndTheScene`

The text above gives, without being rendered, the diagram type `packet`, a node count of 3 (its three block
statements, although the second is drawn in two parts), and the title `Header` of its `title` statement. That the edge
and subgraph counts are 0, that there is no direction, and the accessible title are verified at the unit level (see the
PacketParser unit verification design).

#### CanvasNetMermaid-Packet-FailClosed: Invalid Text Is Refused At Its Place

**Tests**: `PacketTests.CanvasNetMermaid_Packet_InvalidText_FailsClosedWithLocation`

A block without its colon (`8-15 "b"`) after a valid block and a frontmatter block is refused when the document is
opened with a `MermaidSyntaxException` of code `mermaid-syntax-error` in the named file `p.mmd`, located at line 6 of
the original text, frontmatter lines included, with expected tokens and a hint; a block that leaves a gap (`9-15` after
`0-7`) is refused with the same code at line 3, its statement, with Mermaid's message `Packet block 9 - 15 is not
contiguous. It should start from 8.` and a hint; and a third block over a `MaxNodes` of 2 is refused with
`mermaid-limit-exceeded` at line 4, its statement. No document is returned. The other grammar errors, the lexical
errors, the other database errors, and the label limits are verified at the unit level (see the PacketParser unit
verification design) and at the system level by the hardening tests.

#### CanvasNetMermaid-Packet-Rows: Rows Follow The Configured Row Size

**Tests**: `PacketTests.CanvasNetMermaid_Packet_Rows_FollowTheConfiguredRowSize`,
`PacketTests.CanvasNetMermaid_Packet_Text_GivesTheDiagramAndTheScene`

The blocks `0-15: "Wide"` and `16: "F"` lay out in one row at the default 32 bits per row, and in three rows with the
labels `Wide`, `Wide`, and `F` and a view box 258 pixels wide when the site configuration sets `packet.bitsPerRow` to
8; with a `bitsPerRow` of 1.5 the blocks `0`, `1`, and `2-3` fail the render with `mermaid-render-failed` at line 3 and
Mermaid's message `Block start 1.5 is greater than block end 1.`; and the range `1-99999999` fails the render with
`mermaid-limit-exceeded` at line 3, its statement, under the default limits. In the text of the first scenario the
block `+16` (bits 24 to 39) is drawn on both rows, its label repeated, with the bit numbers `24` and `31` on the first
and `32` and `39` on the second. The split for every row size is verified at the unit level against Mermaid's rows
(see the PacketParser unit verification design and the upstream vectors).

#### CanvasNetMermaid-Packet-Drawing: Packet Diagrams Are Drawn As Mermaid Draws Them

**Tests**: `PacketTests.CanvasNetMermaid_Packet_Fixture_ParsesAndLaysOutConsistently`,
`PacketTests.CanvasNetMermaid_Packet_Text_GivesTheDiagramAndTheScene`,
`PacketTests.CanvasNetMermaid_Packet_Themes_PaintTheBlocksAndTexts`

Every fixture lays out into a scene with a label for every block whose label shows text, its items lying vertically
within the view box, and lays out a second time into the same view box and the same number of items. The text of the
first scenario draws two rows, the labels `Address`, `Length`, `Length`, and `F`, the first bit numbers `0`, `24`,
`32`, and `40`, the last bit numbers `23`, `31`, and `39` (none for the single bit `40`), and the title `Header`. A
block is filled `#efefef` with a black label in the default theme, `#333` with a `#e0dfdf` label in the dark theme, and
`#efefef` with a red label in the dark theme when the site configuration's `themeVariables.packet` sets only
`labelColor` (the object replaces the dark theme's). The geometry, the bit numbers, the title row, the view box, the
narrow blocks, the texts, and the work charged are verified at the unit level (see the PacketRenderer unit verification
design), the packet style sheet by the Configuration subsystem, and the positions, sizes, and paint against Mermaid's
goldens by the system-level reference layout tests.
