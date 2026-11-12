## Pie Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Pie` subsystem (internal namespace
`DemaConsulting.CanvasNet.Mermaid.Pie`), containing two units: `PieParser` (the lexer and the grammar of Mermaid's pie
language, the value converters, the pie database with the diagram metadata, the located errors, and the node and label
limits) and `PieRenderer` (the drawing of Mermaid's pie renderer: the slices, the percentage labels, the colors, the
title, the legend, the donut hole, the highlighted slice, and the view box).

### Verification Approach

The `Pie` subsystem is verified at two tiers. The unit tests (see the unit verification designs under `pie/`) verify
each unit, including the 133 upstream vectors recorded from the pinned Mermaid release. The subsystem-level scenarios
in `PieTests` verify the two units working together through the document: a whole text is opened as a
`MermaidDocument` (preprocessed, detected, and parsed into the diagram), and then asked for its metadata, its located
errors, or its laid-out scene. The original fixtures in `Fixtures/pie/` (8 charts written for this project, one for
each area of the language and the options: a basic chart, `showData` with a title wider than the chart, the
accessibility texts with comments and quoted and escaped labels, small, zero, and repeated sections, fourteen slices
whose colors wrap, a left legend with a donut hole and a label position, a top legend with the forest theme and a
frontmatter title, and a bottom legend with a highlighted slice, the dark theme, and theme variables) are all opened
and laid out. The reference layout of the fixtures against the goldens drawn by Mermaid and the located limits of whole
documents under the default limits are verified at the system level by the reference layout and hardening tests.

The expectations are stated in the tests as values worked out from Mermaid's documented semantics and confirmed
against the pinned Mermaid release: the counts of slices, labels, and legend entries of every fixture, the metadata of a
document, the percentages and legend texts of a known text, and the location and code of an invalid text. The scenarios
use no mocking or stubbing: the preprocessor, the detector, the configuration resolver, the pie style sheet, the text
metrics, and the bundled fonts are real. The subsystem is internal, so its tests reach the diagram and the scene
through the test assembly's `InternalsVisibleTo` access (the helper `SceneQuery`).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: The original charts in `test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/pie/*.mmd`
- **Fonts**: The bundled fonts for the scenarios that lay out
- **Isolation**: Each test opens its own document; the fixtures are only read

### Acceptance Criteria

The `Pie` subsystem's verification passes when every subsystem-level scenario below and every unit-level scenario in
the two unit verification documents passes without error or unexpected exception.

### Test Scenarios

#### CanvasNetMermaid-Pie-Language: Pie Texts Open As Mermaid Reads Them

**Tests**: `PieTests.CanvasNetMermaid_Pie_Fixture_ParsesAndLaysOutConsistently`,
`PieTests.CanvasNetMermaid_Pie_Text_GivesTheDiagramAndTheScene`

Every original fixture (a theory with one case for each of the 8 files) opens as a document whose diagram is a pie
diagram, and the text `pie showData title Pets` with the sections `"Dogs" : 3` and `"Cats" : 1` opens as a document of
the type `pie` with two sections and the title `Pets`. The exact sections, flags, and texts of every kind of statement
are verified at the unit level against Mermaid's database (see the PieParser unit verification design and the upstream
vectors).

#### CanvasNetMermaid-Pie-Metadata: Metadata Without Rendering

**Tests**: `PieTests.CanvasNetMermaid_Pie_Text_GivesTheDiagramAndTheScene`

The text above gives, without being rendered, the diagram type `pie`, a node count of 2 (its two sections), and the
title `Pets` of its `title` statement. That a repeated label is counted once, that the edge and subgraph counts are 0,
and that there is no direction are verified at the unit level (see the PieParser unit verification design).

#### CanvasNetMermaid-Pie-FailClosed: Invalid Text Is Refused At Its Place

**Tests**: `PieTests.CanvasNetMermaid_Pie_InvalidText_FailsClosedWithLocation`

A section without its colon (`"Cats" 1`) after a valid section and a frontmatter block is refused when the document is
opened with a `MermaidSyntaxException` of code `mermaid-syntax-error` in the named file `p.mmd`, located at line 6 of
the original text, frontmatter lines included, with expected tokens and a hint; and a section with a negative value
(`"Cats" : -1`) is refused with the same code at line 3, column 5 (the section), with a hint. No document is returned.
The other grammar errors, the lexical errors, and the limits are verified at the unit level (see the PieParser unit
verification design) and at the system level by the hardening tests.

#### CanvasNetMermaid-Pie-Drawing: Pie Charts Are Drawn As Mermaid Draws Them

**Tests**: `PieTests.CanvasNetMermaid_Pie_Fixture_ParsesAndLaysOutConsistently`,
`PieTests.CanvasNetMermaid_Pie_Text_GivesTheDiagramAndTheScene`

Every fixture lays out into a scene with one legend entry for each section of its diagram and one slice and one
percentage label for each section of at least 1% of the sum, its items lying horizontally within the view box (to 3
pixels), and lays out a second time into the same view box and the same number of items. The text above
draws the slice labels `75%` and `25%`, the legend texts `Dogs [3]` and `Cats [1]` (with `showData`), and the title
`Pets`. The angles and outlines of the slices, the label positions, the colors, the legend positions, the donut hole,
the highlighted slice, the title, and the view box are verified at the unit level (see the PieRenderer unit
verification design), the paint of the pie style sheet by the Configuration subsystem, and the positions and sizes
against Mermaid's goldens by the system-level reference layout tests.
