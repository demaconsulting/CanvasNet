# System Verification Design

This document describes the system-level verification strategy for CanvasNetMermaid.

## Verification Approach

`CanvasNetMermaid` is verified at three tiers with xUnit v3 in the test project
`test/DemaConsulting.CanvasNet.Mermaid.Tests/`, which references only the Mermaid package and, through it, the core
`CanvasNet` package (no other format package, so the tests stay independent of the Viewer):

- **System tests** (described here) use only the public API, as a consumer would: they open original-work diagrams
  from in-memory text, streams, temporary files, the 42 flowchart fixtures in `Fixtures/flowchart/`, the 24
  sequence fixtures in `Fixtures/sequence/`, the 16 class fixtures in `Fixtures/class/`, the 14 state fixtures in
  `Fixtures/state/`, the 12 ER fixtures in `Fixtures/er/`, the 8 pie fixtures in `Fixtures/pie/`, the 18 gantt
  fixtures in `Fixtures/gantt/`, the 14 mindmap fixtures in `Fixtures/mindmap/`, the 13 packet fixtures in
  `Fixtures/packet/`, and the 18 git graph fixtures in `Fixtures/gitgraph/` with `MermaidDocument.Parse` and
  `MermaidDocument.Open`, supply `MermaidOpenOptions`, `MermaidRenderOptions`, and
  `MermaidLimits`, and assert on observable outputs: the metadata properties, the sizes, sampled pixels of the
  rendered `Surface`, the reports delivered to the handlers, and the exceptions with their codes, locations, and
  hints. The system test classes are `MermaidSystemIntegrationTests` (entry points, metadata, errors, configuration,
  pictures, reports, image source, notices, cancellation, concurrency), `MermaidFlowchartIntegrationTests` (every
  fixture in strict mode, theme and style pixels, scale, background, exact size, determinism, cancellation),
  `MermaidSequenceIntegrationTests` (every sequence fixture in strict mode, theme pixels, look, math, and icon reports,
  scale, work limit, determinism, cancellation), `MermaidClassIntegrationTests` (every class fixture in strict mode,
  metadata, theme, classifier, and style pixels, look, layout, and style reports, the legacy renderer, scale, work
  limit, determinism, cancellation), `MermaidStateIntegrationTests` (every state fixture in strict mode, metadata,
  theme, region, and style pixels, sanitized notes, the legacy renderer, look and layout reports, located render
  failures, scale, work limit, determinism, cancellation), `MermaidErIntegrationTests` (every ER fixture in strict
  mode, metadata, theme, row, outline, marker, and style pixels, the markers of the cardinalities, generic types,
  spacing, look and layout reports, located render failures, scale, work limit, determinism, cancellation),
  `MermaidPieIntegrationTests` (every pie fixture in strict mode, metadata, theme pixels, transparency, scale, the
  negative value, work limit, determinism, cancellation), `MermaidGanttIntegrationTests` (every gantt fixture in strict
  mode with the reference clock, metadata, theme pixels of the task states, the today line with and without the clock,
  the gantt reports, the located invalid date, determinism, cancellation), `MermaidMindmapIntegrationTests` (metadata,
  theme pixels of the root and the sections, transparency, scale, the strict-mode reports of an icon, a layout, a look,
  and a Font Awesome label, the empty mindmap, the second root, work limit, determinism, cancellation),
  `MermaidMindmapReferenceLayoutTests` (the mindmap layout goldens in three tiers), `MermaidPacketIntegrationTests`
  (every packet fixture in strict mode, metadata, theme pixels, the two keywords, the located gap between blocks, the
  huge range, determinism, cancellation), `MermaidGitGraphIntegrationTests` (every git graph fixture in strict mode,
  metadata, theme pixels, the generated ids, the located rejected merge, the site's main branch, limits, determinism,
  cancellation), `MermaidGitGraphReferenceLayoutTests` (the git graph layout goldens),
  `MermaidChartReferenceLayoutTests` (the pie, gantt, and packet layout goldens), `MermaidReferenceLayoutTests` (the
  flowchart, class, state, and ER layout goldens) and
  `MermaidSequenceReferenceLayoutTests` (the sequence layout goldens), `MermaidHardeningTests` (limits and seeded
  mutation fuzzers), `MermaidUnsupportedDiagramTests` (the diagram types this version does not draw), and
  `MermaidGalleryTests` (the diagrams of the documentation gallery). They are named
  `CanvasNetMermaid_<Area>_<Scenario>_<Result>`.
- **Subsystem tests** (`{Subsystem}Tests.cs` in each folder) and **unit tests** (`{Unit}Tests.cs`) are described in
  the subsystem and unit verification designs that follow this document; the direct unit `MermaidDocument` is
  described in _MermaidDocument Unit Verification Design_.

Two kinds of upstream evidence make the fidelity objective:

- **Upstream vectors** (`Fixtures/upstream-vectors/`) are the expected results of the pinned Mermaid, khroma,
  dagre-d3-es, d3 packages (d3-shape, and the d3 time modules of `time-axis.json`), and the cytoscape, layout-base,
  cose-base, and cytoscape-cose-bilkent libraries bundled by Mermaid (`cose-layouts.json` and `js-math.json`) for
  original inputs, generated by
  the developer scripts in `tools/visual-diff/mermaid/` (headless Chrome through the pinned puppeteer for Mermaid and
  for the cose layouts and their math, which differ in Node; Node for dagre and d3). They are checked by the unit and
  subsystem tests.
- **Reference layout goldens** (`Fixtures/reference/*.layout.json`, provenance in `Fixtures/reference/README.md`) are
  extracted from mermaid-cli renders of every flowchart fixture with the bundled Liberation Sans
  (`tools/visual-diff/render-mermaid.mjs` and `liberation.css`; regenerated by
  `tools/visual-diff/update-mermaid-goldens.ps1`). The system test compares the view box, node centers and
  sizes, subgraph rectangles, 21 arc-length samples of every edge, and the edge label centers. The sequence goldens
  (`Fixtures/reference/sequence/*.layout.json`) are extracted the same way from every sequence fixture; the system
  test compares the view box, the title, every participant drawing with its parts, paint, and labels, the lifelines,
  every message line or sampled self loop with its markers, texts, sequence number, and circles, the notes,
  activations, block frames, `rect` backgrounds, boxes, menus, and the paint order. The class goldens
  (`Fixtures/reference/class/*.layout.json`) are extracted the same way from every class fixture; the system test
  compares the view box, every node's position and box and each drawn part of each class box, the namespace
  rectangles and titles, every edge (its length and 21 sampled points), the edge label centers, the terminal label
  positions, and the title box. The state goldens (`Fixtures/reference/state/*.layout.json`) are extracted the same
  way from every state fixture; the system test compares the view box, each node's position, box, and drawn parts,
  each cluster's rectangle, inner rectangle, and title (the composite states, the concurrent regions, and the groups
  of a state and its note), every edge (its length and 21 sampled points), the edge label centers, and the title box.
  The ER goldens (`Fixtures/reference/er/*.layout.json`) are extracted the same way from every ER fixture; the system
  test compares the view box, each node's position and box and the parts of each entity table (the outline, the rows,
  each text cell by its own box, and the dividers), each subgraph's rectangle and title, every edge (its length and 21
  sampled points), the edge label centers, and the title box. The pie goldens (`Fixtures/reference/pie/*.layout.json`)
  are extracted the same way from every pie fixture; the system test compares the view box, the outer circle, every
  slice's path, box, and paint, the percentage labels, the title, and every legend entry with its square and text.
  The gantt goldens (`Fixtures/reference/gantt/*.layout.json`) are extracted from mermaid-cli renders whose page has
  the UTC time zone and a clock fixed at 2024-06-12T10:30:00Z, the `Now` the system test passes; it compares the view
  box, the excluded days, both axes with every tick line and label, the section rows, every task bar (its corners after
  a milestone's transform) and text with its paint, the section titles, the today line, and the title.
  The mindmap goldens (`Fixtures/reference/mindmap/*.layout.json`) are extracted from mermaid-cli renders of every
  mindmap fixture, each drawn twice by the harness, which fails when the two differ or when the layout draws a random
  number; they hold each node's size as Chrome's `getBBox` gives it, its center, box, and computed paint, and each
  edge's cytoscape points, 21 samples, and paint. The system test compares them in three tiers because the
  cose-bilkent layout is chaotic: the node sizes the library gives the layout, the layout alone given the golden's own
  sizes (bit for bit), and the whole drawing. The packet goldens (`Fixtures/reference/packet/*.layout.json`) are
  extracted the same way from every packet fixture; the system test compares the view box, every block rectangle with
  its paint, and every label, bit number, and title with its content, anchor, font size, color, and box. The git graph
  goldens (`Fixtures/reference/gitgraph/*.layout.json`) are extracted the same way from every git graph fixture, with
  Mermaid's id function given the fixed pseudo-random sequence of `tools/visual-diff/mermaid/seeded-random.mjs`, so a
  commit without an id has the label the library gives it; the system test compares the view box, every branch line,
  label rectangle, and label row, the path data of every arrow, every bullet, every commit label and tag through their
  transforms, and the title, each with its computed paint.

The goldens and vectors are committed; CI only compares. `tools/visual-diff/compare-with-mermaid.ps1` produces
side-by-side pictures for a manual visual review and never gates.

## Test Environment

- **Framework**: xUnit v3 under the .NET SDK, for `net8.0`, `net9.0`, and `net10.0`.
- **Platforms**: Windows, Linux, and macOS in CI; the platform requirements are proven by source-filtered results of
  `CanvasNetMermaid_Flowchart_DefaultTheme_PaintsMermaidsColors`.
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Files**: the fixtures and goldens are copied to the output folder; the third-party notices are copied to
  `Notices/` so that the notice test reads what the package ships. Temporary files are written to the system
  temporary folder and deleted.
- **Fonts**: only the fonts bundled with the core package are used; no system font is installed or required.
- **Isolation**: no network, no browser, no Node at test time. A picture resolver, where needed, is a stub in the
  test that returns a solid surface.

## Acceptance Criteria

A system-level test run passes when every scenario below passes on every target framework and platform without an
exception beyond those explicitly asserted. A layout that differs from a golden beyond its documented tolerance
(0.01 pixel, 0.1 pixel for the two fixtures with rough.js circles; for the sequence goldens 0.01 pixel, 0.02 pixel for
text boxes and cylinder arcs; for the class goldens 0.01 pixel, 0.23 pixel for `16-config-title-links`, whose class
texts are SVG text; for the state and ER goldens 0.01 pixel with no exception; for the pie and gantt goldens 0.01 pixel,
0.02 pixel for text boxes and slice arc boxes; for the mindmap goldens 0.02 pixel for the node sizes, bit equality for
the layout given the golden's sizes, and 0.01 pixel for the whole drawing of every fixture but `02-shapes`, whose cloud
node Chrome measures 7.6e-6 pixel taller, which the chaotic layout turns into a different picture; for the packet goldens
0.01 pixel, 0.02 pixel for text boxes; for the git graph goldens 0.01 pixel, 0.02 pixel for text boxes, with no
exception), a fixture that gives
a report it should
not give in strict mode, a pixel of a documented theme color that differs, an undocumented exception type from the
mutation fuzzer, or two renders of the same document that differ constitutes a failure.

## System-Level Test Scenarios

### CanvasNetMermaid-Open: String, Stream, And Path Agree

**Tests**: `MermaidSystemIntegrationTests.CanvasNetMermaid_Open_StringStreamAndPath_Agree`,
`MermaidFlowchartIntegrationTests.CanvasNetMermaid_Flowchart_Fixture_RendersStrictly`

Opens one flowchart from a string, from a stream with a byte-order mark, and from a temporary file, and asserts that
the file is named by its path and that the metadata and the rendered pixels are identical; every fixture is opened
by its path.

### CanvasNetMermaid-DiagramMetadata: A Diagram Describes Itself

**Tests**: `MermaidSystemIntegrationTests.CanvasNetMermaid_Metadata_Flowchart_DescribesTheDiagram`,
`MermaidUnsupportedDiagramTests.CanvasNetMermaid_Render_OtherKnownTypes_AreUnsupportedWithHint`,
`MermaidClassIntegrationTests.CanvasNetMermaid_ClassDiagram_Metadata_CountsClassesRelationsAndNamespaces`,
`MermaidClassIntegrationTests.CanvasNetMermaid_ClassDiagram_Metadata_CountsTheDrawnNamespaces`,
`MermaidStateIntegrationTests.CanvasNetMermaid_StateDiagram_Metadata_CountsStatesTransitionsAndComposites`,
`MermaidErIntegrationTests.CanvasNetMermaid_ErDiagram_Metadata_CountsEntitiesRelationshipsAndSubgraphs`,
`MermaidPieIntegrationTests.CanvasNetMermaid_PieChart_Metadata_CountsTheSections`,
`MermaidGanttIntegrationTests.CanvasNetMermaid_GanttChart_Metadata_CountsTasksAndSections`,
`MermaidMindmapIntegrationTests.CanvasNetMermaid_Mindmap_Metadata_CountsNodesAndEdges`,
`MermaidPacketIntegrationTests.CanvasNetMermaid_PacketDiagram_Metadata_CountsTheBlocks`,
`MermaidGitGraphIntegrationTests.CanvasNetMermaid_GitGraph_Metadata_CountsCommitsAndParentLinks`

Asserts the type, the supported flag, the frontmatter title, the accessible title, the direction, and the node, edge,
and subgraph counts of a flowchart without rendering it, and the type and the unsupported flag of other known
diagram types. For a class diagram with a namespace, two classes, two relations (one to a lollipop interface), and a
note, asserts the type `class`, the supported flag, the counts 2, 2, and 1 (notes, interfaces, and note links are not
counted), the direction `LR`, and no title; and that `namespace Geo.Plane` counts 2 namespaces by default and 1 when
the frontmatter sets `class.hierarchicalNamespaces: false`, each equal to the number of clusters drawn. For a state
diagram with a start, a state `Off`, a `<<fork>>` state, a composite state of two concurrent regions with one state
each, a note on `Off`, an end, `accTitle: Lamp`, and `direction BT` before a final `direction LR`, asserts the type
`state`, the supported flag, the counts 6, 4, and 1 (the start, `Off`, the fork, the two inner states, and the end;
the note, its link, the regions, and the note group are not counted), the direction `LR` (the last top-level
direction statement), the accessible title `Lamp`, and no title. For an ER diagram with the frontmatter title `Shop`,
`accTitle: Orders`, `direction RL` before a final `direction LR`, a subgraph `S` that holds `A ||--o{ B`, and a
relationship from `C` to the subgraph `S`, asserts the type `er`, the supported flag, the counts 3, 2, and 1 (the
entity `S` that the relationship adds is not drawn and not counted), the direction `LR`, the accessible title
`Orders`, and the title `Shop`. For a pie chart titled `Pets` with `accTitle: Animals` and the sections `Dogs`, `Cats`,
and `Dogs` again, asserts the type `pie`, the supported flag, the counts 2, 0, and 0 (a repeated label once), no
direction, the title `Pets`, and the accessible title `Animals`, and that a pie chart without a title of its own takes
the frontmatter title. For a gantt chart titled `Plan` with `accTitle: Schedule`, the sections `A` (two tasks), `B`
(only a vertical marker), and `C` (one task), asserts the type `gantt`, the counts 4, 0, and 2 (the marker is a node,
but its section is not drawn), no direction, the title `Plan`, and the accessible title `Schedule`. For a mindmap of a
root with the children `Origins` (holding `History`), `Research`, and `Tools`, asserts the type `mindmap`, the
supported flag, the counts 5, 4, and 0, no direction, no title, and no accessible title, and that a mindmap under a
frontmatter title takes that title. For a `packet-beta` diagram titled `Frame` with `accTitle: A frame`, the block
`0-99` (drawn over four rows), and the block `+4`, asserts the type `packet`, the supported flag, the counts 2, 0, and 0
(the blocks as written), no direction, the title `Frame`, and the accessible title `A frame`, and that a packet diagram
without a title of its own takes the frontmatter title. For a `gitGraph BT:` git graph titled `History` with
`accTitle: A history`, a commit, the branch `dev` with a commit, and a merge of `dev` into `main`, asserts the type
`gitGraph`, the supported flag, the counts 3, 3, and 0 (the commits, and the links to their parents, two of them from
the merge), the direction `BT`, the title `History`, and the accessible title `A history`, and that a git graph without
a title of its own takes the frontmatter title and the direction `LR`.

### CanvasNetMermaid-SyntaxErrors: Invalid Text Fails Closed With A Location And A Hint

**Tests**: `MermaidSystemIntegrationTests.CanvasNetMermaid_SyntaxErrors_AreLocatedCodedAndHinted`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_MutatedFixtures_ThrowOnlyDocumentedExceptions`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_MutatedClassFixtures_ThrowOnlyDocumentedExceptions`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_MutatedStateFixtures_ThrowOnlyDocumentedExceptions`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_MutatedErFixtures_ThrowOnlyDocumentedExceptions`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_MutatedPieFixtures_ThrowOnlyDocumentedExceptions`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_MutatedGanttFixtures_ThrowOnlyDocumentedExceptions`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_MutatedMindmapFixtures_ThrowOnlyDocumentedExceptions`,
`MermaidPieIntegrationTests.CanvasNetMermaid_PieChart_NegativeValue_FailsLocated`,
`MermaidMindmapIntegrationTests.CanvasNetMermaid_Mindmap_SecondRoot_FailsLocated`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_MutatedPacketFixtures_ThrowOnlyDocumentedExceptions`,
`MermaidPacketIntegrationTests.CanvasNetMermaid_PacketDiagram_GapBetweenBlocks_FailsLocated`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_MutatedGitGraphFixtures_ThrowOnlyDocumentedExceptions`,
`MermaidGitGraphIntegrationTests.CanvasNetMermaid_GitGraph_RejectedMerge_FailsLocated`

Asserts the `mermaid-syntax-error` code, the source name, the line, the expected tokens, and the message prefix of an
unclosed label, and the `mermaid-unknown-diagram` code with a hint naming `flowchart` for a misspelled keyword; the
fuzzers assert that mutated flowchart, class, state, and ER fixtures only ever raise the documented exception types.
For a state diagram, the seeded fuzzer (seed 20261110) mutates each of the 14 state fixtures eight times with the
tokens of the state language (transitions, `[*]`, composite states, `<<fork>>`, `<<join>>`, `<<choice>>`, notes,
directions, `classDef`, `class`, `style`, `click`, and accessibility texts) and asserts that opening and measuring
raise only `MermaidSyntaxException`, `MermaidRenderException`, `InvalidDataException`, or
`ArgumentOutOfRangeException`. For an ER diagram, the seeded fuzzer (seed 20261111) does the same over the 12 ER
fixtures with the tokens of the ER language (the cardinalities in symbols and words, roles, attribute blocks with
keys, comments, generic types and backticks, aliases, subgraphs, directions, `classDef`, `class`, `style`, and
accessibility texts). Seeded fuzzers do the same over the 8 pie and the 18 gantt fixtures with the tokens of the pie
and gantt languages, rendering the gantt texts with the reference clock, and assert the same documented exception
types. A pie section with the value `-2` in `chart.mmd` fails at open with `mermaid-syntax-error` at line 3, column 3,
with Mermaid's message that negative values are not allowed in pie charts. A seeded fuzzer (seed 20261111) mutates each
of the 14 mindmap fixtures twelve times with the tokens of the mindmap language (indentation, tabs, every bracket,
quotes and backticks, icons, classes, comments, line breaks, markdown, Font Awesome names, and a frontmatter fence) and
asserts that opening and measuring raise only the documented exception types; and a second root `c` in `map.mmd`
fails at open with `mermaid-syntax-error` at line 4, column 3, with Mermaid's message `There can be only one root. No
parent could be found for ("c")`. A seeded fuzzer (seed 20261112) mutates each of the 13 packet fixtures twelve times
with the tokens of the packet language (both keywords, titles and accessibility texts, quoted labels, colons, ranges,
bit counts, `+0`, numbers with leading zeros or fractions, huge numbers, comments, directives, frontmatter fences, line
breaks, escapes, and entities) and asserts that opening and measuring raise only the documented exception types; and a
block `9-15` after `0-7` in `frame.mmd` fails at open with `mermaid-syntax-error` at line 3, column 3, with Mermaid's
message `Packet block 9 - 15 is not contiguous. It should start from 8.` A seeded fuzzer (seed 20261112) mutates each of
the 18 git graph fixtures twelve times with the tokens of the git graph language (the header forms and directions,
every statement and option, commit types, quotes, colons, comments, directives, frontmatter fences, line breaks,
escapes, entities, `<br>` tags, decimals, and a letter no token matches) and asserts that opening and measuring raise
only the documented exception types; and a merge of the branch `feature`, which does not exist, in `graph.mmd` fails
at open with `mermaid-syntax-error` at line 3, column 3, with Mermaid's message `Incorrect usage of "merge". Branch to
be merged (feature) does not exist`.

### CanvasNetMermaid-Configuration: The Text Overrides The Caller

**Tests**: `MermaidSystemIntegrationTests.CanvasNetMermaid_Configuration_TextOverridesTheCaller`,
`MermaidErIntegrationTests.CanvasNetMermaid_ErDiagram_TopLevelNodeSpacing_WinsOverTheErSpacing`

Renders the same flowchart with the caller's `dark` theme alone, under a frontmatter `forest` theme, and under a
frontmatter `forest` theme plus a `neutral` init directive, and asserts by the node fill pixels that the frontmatter
replaces the caller's theme and the directive replaces the frontmatter. For an ER diagram with two entities under one
parent, asserts that the gap between them is 140 pixels with the default ER spacing and 30 pixels when the frontmatter
sets a top-level `nodeSpacing: 30` beside `er.nodeSpacing: 200`, as Mermaid's dagre layout takes it.

### CanvasNetMermaid-Themes: Mermaid's Theme Colors

**Tests**: `MermaidFlowchartIntegrationTests.CanvasNetMermaid_Flowchart_DefaultTheme_PaintsMermaidsColors`,
`MermaidFlowchartIntegrationTests.CanvasNetMermaid_Flowchart_Theme_ChangesTheNodeFill`,
`MermaidSequenceIntegrationTests.CanvasNetMermaid_Sequence_DefaultTheme_PaintsMermaidsColors`,
`MermaidSequenceIntegrationTests.CanvasNetMermaid_Sequence_Theme_ChangesTheParticipantFill`,
`MermaidClassIntegrationTests.CanvasNetMermaid_ClassDiagram_DefaultTheme_PaintsMermaidsColors`,
`MermaidClassIntegrationTests.CanvasNetMermaid_ClassDiagram_Theme_ChangesTheBoxFill`,
`MermaidStateIntegrationTests.CanvasNetMermaid_StateDiagram_DefaultTheme_PaintsMermaidsColors`,
`MermaidStateIntegrationTests.CanvasNetMermaid_StateDiagram_ConcurrentRegion_IsFilledWithTheAlternateBackground`,
`MermaidStateIntegrationTests.CanvasNetMermaid_StateDiagram_Theme_ChangesTheStateFill`,
`MermaidErIntegrationTests.CanvasNetMermaid_ErDiagram_DefaultTheme_PaintsTheRowsInTurn`,
`MermaidErIntegrationTests.CanvasNetMermaid_ErDiagram_DefaultTheme_StrokesTheTableInTheNodeBorder`,
`MermaidErIntegrationTests.CanvasNetMermaid_ErDiagram_ZeroMarker_IsAWhiteCircle`,
`MermaidErIntegrationTests.CanvasNetMermaid_ErDiagram_Theme_ChangesTheEntityFill`,
`MermaidPieIntegrationTests.CanvasNetMermaid_PieChart_DefaultTheme_PaintsSlicesAndLegend`,
`MermaidPieIntegrationTests.CanvasNetMermaid_PieChart_Theme_ChangesTheSectionColors`,
`MermaidGanttIntegrationTests.CanvasNetMermaid_GanttChart_DefaultTheme_PaintsTheTaskStates`,
`MermaidGanttIntegrationTests.CanvasNetMermaid_GanttChart_Theme_ChangesTheBarColors`,
`MermaidMindmapIntegrationTests.CanvasNetMermaid_Mindmap_DefaultTheme_PaintsRootAndSections`,
`MermaidMindmapIntegrationTests.CanvasNetMermaid_Mindmap_Theme_ChangesTheColors`,
`MermaidPacketIntegrationTests.CanvasNetMermaid_PacketDiagram_Pixels_CarryTheThemeColors`,
`MermaidGitGraphIntegrationTests.CanvasNetMermaid_GitGraph_Pixels_CarryTheThemeColors`

Asserts the default theme's node fill `#ECECFF`, node border `#9370DB`, arrowhead `#333333`, and subgraph fill `#ffffde`
at sampled pixels, and the node fill of the `dark`, `forest`, and `neutral` themes; for a sequence diagram rendered at
scale 4, the participant fill `#ECECFF` and border `#9370DB`, the note fill `#fff5ad`, the activation fill `#f4f4f4`,
and the arrowhead `#333333`, and the participant fill of the `default`, `dark`, `forest`, and `neutral` themes
(`#ECECFF`, `#1F2020`, `#CDE498`, and `#EEEEEE`); for a class diagram rendered at scale 4, the class box fill `#ECECFF`,
the divider in the node border `#9370DB`, the composition marker filled with the line color `#333333`, and the extension
marker left hollow, and the class box fill of the same four themes at scale 2. For a state diagram rendered at scale 4,
asserts the state fill `#ECECFF`, the start state in `#333333`, the end state's inner circle in the node border
`#9370DB` inside a ring in the line color `#333333`, a composite state's title band in `#ECECFF` over its white
(`#FFFFFF`) inner area, and the inner area of a composite state nested in it in the alternate background `#F0F0F0`; that
each of the two concurrent regions of a composite state is a cluster and a region is filled with `#F0F0F0`; and the
state fill of the same four themes at scale 2. For an ER diagram rendered at scale 2, asserts that a table of three
attributes has its name row in `#ECECFF` and its rows in turn in `#FFFFFF`, `#F1F1FF`, and `#FFFFFF`, and that an entity
without attributes is filled `#ECECFF`; at scale 4, that the table's left edge is drawn in the node border `#9370DB` and
that the circle of a zero-or-one end marker, 21 pixels before the end of the line, is filled white over the line while
the marker is not filled beside it (the transparent background shows); and the entity fill and the name row fill of the
same four themes at scale 2. For a pie chart, asserts the legend squares in `pie1` (`#ECECFF`) and `pie2` (`#FFFFDE`)
and the first slice in `pie1` at the slice opacity of 0.7 over a white background (red and green between 240 and 244,
blue 255), and that each theme changes the first section's color. For a gantt chart, asserts a plain bar in
`taskBkgColor` (`#8a90dd`), a critical bar in `red`, a done bar in `lightgrey`, and an active bar in `#bfc7ff`, and that
each theme changes the bar color. For a mindmap rendered at scale 2 over white, asserts the root filled with `git0`
(`#0000EC`) and its first child and that child's child filled with the first section color (`#FFFF78`), and that the
`forest` theme changes the colors of the root and of the first section. For a packet diagram of a title and three blocks
rendered over white at its natural size of 1026 by 141 pixels, asserts a block filled `#efefef` inside and stroked dark
on its top edge in the default theme, and filled `#333` in the `dark` theme. For a git graph of the one commit `A`
rendered over white at its natural size, asserts the pixel at the commit's center (10, -2), which is (105, 18) on the
surface, filled with the default theme's `git0` (`#0000EC`), and another color there in the `dark` theme.

### CanvasNetMermaid-Flowcharts: Every Fixture Draws Strictly

**Tests**: `MermaidFlowchartIntegrationTests.CanvasNetMermaid_Flowchart_Fixture_RendersStrictly`,
`MermaidFlowchartIntegrationTests.CanvasNetMermaid_Flowchart_ClassDefAndStyle_PaintTheNodes`,
`MermaidGalleryTests.CanvasNetMermaid_SystemIntegration_Gallery_HoldsEveryDrawnDiagramType`,
`MermaidGalleryTests.CanvasNetMermaid_SystemIntegration_Gallery_RendersStrictWithoutWarnings` (a theory with one case
for each file)

Renders each of the 42 fixtures (directions, all shapes, link forms, subgraphs, styles, classes, curves, markdown and
HTML labels, unicode, long labels, many nodes) in strict mode and asserts that it draws something, measures as it
renders, and gives no report other than the documented missing-glyph warning of `38-unicode`; asserts the pixels of
`classDef`, `class`, and `style` declarations. The diagrams of the documentation gallery
(`docs/user_guide/examples/mermaid-*.mmd`, copied next to the test assembly as `Gallery`) are checked the same way: the
first test asserts that there are at least sixteen, that each opens as a supported diagram of a type this version
draws (a flowchart, a sequence diagram, a class diagram, a state diagram, or an entity relationship diagram), and that
every drawn type is shown; the theory renders each with `ThrowOnUnsupportedFeature = true`, a warning handler, and a
resolver that gives a solid picture for every link, and asserts that no warning is raised, that the surface has the
measured size, that more than one pixel in fifty differs from the corner pixel, and that the pictures diagram asks the
resolver for its `.scad`, `.svg`, and `.vl.json` links.

### CanvasNetMermaid-SequenceDiagrams: Every Sequence Fixture Draws Strictly

**Tests**: `MermaidSequenceIntegrationTests.CanvasNetMermaid_Sequence_Fixture_RendersStrictly` (a theory with one case
for each file), `MermaidSequenceIntegrationTests.CanvasNetMermaid_Sequence_DefaultTheme_PaintsMermaidsColors`,
`MermaidGalleryTests.CanvasNetMermaid_SystemIntegration_Gallery_HoldsEveryDrawnDiagramType`,
`MermaidGalleryTests.CanvasNetMermaid_SystemIntegration_Gallery_RendersStrictWithoutWarnings`

Opens each of the 24 sequence fixtures by its path (the arrows, participant types, aliases and metadata, activations,
notes, self messages, every block, `rect` backgrounds, boxes, numbering, created and destroyed participants, the
sequence configuration keys, wrapping and breaks, titles, menus, and properties) and renders it in strict mode, and
asserts that the surface has the measured size, is not fully transparent, and that no report or warning is given; the
default theme's pixels of a sequence diagram are asserted as above; and the gallery holds sequence diagrams that render
strictly without warnings.

### CanvasNetMermaid-ClassDiagrams: Every Class Fixture Draws Strictly

**Tests**: `MermaidClassIntegrationTests.CanvasNetMermaid_ClassDiagram_Fixture_RendersStrictly` (a theory with one
case for each file), `MermaidClassIntegrationTests.CanvasNetMermaid_ClassDiagram_ClassifiersAndStyles_StyleTheBox`,
`MermaidClassIntegrationTests.CanvasNetMermaid_ClassDiagram_LegacyRenderer_IsDrawnTheSame`,
`MermaidGalleryTests.CanvasNetMermaid_SystemIntegration_Gallery_HoldsEveryDrawnDiagramType`,
`MermaidGalleryTests.CanvasNetMermaid_SystemIntegration_Gallery_RendersStrictWithoutWarnings`

Opens each of the 16 class fixtures by its path (classes, members and methods, visibility and classifiers, generics,
annotations, every relation form, two-way relations, cardinalities and labels, lollipop interfaces, namespaces,
notes, directions, `classDef`, `cssClass`, and `style`, labels and backticked names, `hideEmptyMembersBox`, and a
frontmatter title with `htmlLabels: false`, `click`, `link`, and `callback`) and renders it in strict mode, and
asserts the type `class`, that the surface has the measured size, is not fully transparent, and that no report or
warning is given; asserts that a static member is drawn underlined and not italic, an abstract member italic and not
underlined, the class name bold, and that `style Dog fill:#ff0000` fills that class box red at its pixels; asserts
that a text whose `class.defaultRenderer` selects the legacy `class` id has that type and the same pixels as the
current renderer; and the gallery holds class diagrams that render strictly without warnings.

### CanvasNetMermaid-StateDiagrams: Every State Fixture Draws Strictly

**Tests**: `MermaidStateIntegrationTests.CanvasNetMermaid_StateDiagram_Fixture_RendersStrictly` (a theory with one
case for each file), `MermaidStateIntegrationTests.CanvasNetMermaid_StateDiagram_StylesAndClasses_FillTheState`,
`MermaidStateIntegrationTests.CanvasNetMermaid_StateDiagram_NestedNoteMarkup_IsSanitized`,
`MermaidStateIntegrationTests.CanvasNetMermaid_StateDiagram_LegacyRenderer_IsReportedAndDrawnTheSame`,
`MermaidStateIntegrationTests.CanvasNetMermaid_StateDiagram_MermaidRenderFailures_AreLocated`,
`MermaidGalleryTests.CanvasNetMermaid_SystemIntegration_Gallery_HoldsEveryDrawnDiagramType`,
`MermaidGalleryTests.CanvasNetMermaid_SystemIntegration_Gallery_RendersStrictWithoutWarnings`

Opens each of the 14 state fixtures by its path (`[*]` start and end states and labeled transitions, `state "d" as s`
and several descriptions, three levels of composite states, `direction LR` at the root with `TB`, `LR`, and `BT` in
composite states, `<<fork>>`, `<<join>>`, `[[fork]]`, and `[[join]]`, `<<choice>>` and `[[choice]]`, a composite
state of three concurrent regions, notes on one and several lines and in a composite state, `classDef`, `class`,
`:::`, and `style`, the `stateDiagram` header, self and parallel transitions, start and end states in nested
composite states, transitions into composite states, and a frontmatter title with `htmlLabels: false`, the `state`
spacing, `hide empty description`, `scale`, and `click`) and renders it in strict mode, and asserts the type `state`,
that the surface has the measured size, is not fully transparent, and that no report or warning is given. Asserts that
`classDef hot fill:#ff0000` applied with `:::` fills that state red and `style B fill:#00ff00` fills that state green
at their pixels; that a note in a composite state written `<b>kept</b><script>dropped</script>` draws `kept` bold and
draws neither the script nor its text; and that a `stateDiagram` whose frontmatter sets `state.defaultRenderer:
dagre-d3` has the type `state`, is supported, is reported once at the `state` key (line 3), and draws the same pixels
as the `stateDiagram-v2` text. Asserts the located `mermaid-render-failed` errors where Mermaid's render fails too: a
note on a state that appears nowhere before it fails at the note (line 3) with a message that names `No such shape`
(Mermaid's `No such shape: undefined`), and two composite states inside each other fail at the second (line 5) with a
message that names the `cycle` of parents. The gallery holds state diagrams that render strictly without warnings.

### CanvasNetMermaid-ErDiagrams: Every ER Fixture Draws Strictly

**Tests**: `MermaidErIntegrationTests.CanvasNetMermaid_ErDiagram_Fixture_RendersStrictly` (a theory with one case
for each file), `MermaidErIntegrationTests.CanvasNetMermaid_ErDiagram_Markers_FollowTheCardinalities`,
`MermaidErIntegrationTests.CanvasNetMermaid_ErDiagram_StylesAndClasses_FillTheNameAndEvenRows`,
`MermaidErIntegrationTests.CanvasNetMermaid_ErDiagram_GenericTypes_AreDrawnWithAngleBrackets`,
`MermaidErIntegrationTests.CanvasNetMermaid_ErDiagram_MermaidRenderFailures_AreLocated`,
`MermaidGalleryTests.CanvasNetMermaid_SystemIntegration_Gallery_HoldsEveryDrawnDiagramType`,
`MermaidGalleryTests.CanvasNetMermaid_SystemIntegration_Gallery_RendersStrictWithoutWarnings`

Opens each of the 12 ER fixtures by its path (relationships with crow's foot markers and quoted roles, every
cardinality in symbols and in words with `to` and `--`, identifying and non-identifying lines (`--`, `..`, `.-`,
`-.`, `to`, and `optionally to`), attribute types and names, `PK`, `FK`, and `UK` keys, key lists, and comments,
aliases, `direction LR`, `classDef`, `:::`, `class`, and `style`, generic types and backtick names, nested subgraphs
with a title and their own direction, and a frontmatter title with `htmlLabels: false` and the `er` spacing and box
settings) and renders it in strict mode, and asserts the type `er`, that the surface has the measured size, is not
fully transparent, and that no report or warning is given. Asserts that `A ||--o{ B` draws three marker parts (one
path for the only-one start; a circle and a path for the zero-or-more end) and that `A u--o{ B` draws two, since
Mermaid has no marker for `md_parent`; that a class with `fill:#ff0000` on a table of two attributes fills the name
row and the even row red while the odd row keeps `#FFFFFF`, and fills an entity without attributes red; and that the
attribute type `List~int~` is drawn as `List<int>` and that the empty keys and comment columns are dropped (six
divider paths: the line under the name twice and the line after the type column, each a fill and a stroke). Asserts
the located `mermaid-render-failed` errors where Mermaid's render fails too: a relationship whose entity is named like
a subgraph declared after it fails at the relationship (line 2) with a message that its end `is not drawn`, and a
subgraph that holds a relationship to itself fails at the subgraph (line 2) with a message that names the `cycle` of
parents. The gallery holds ER diagrams that render strictly without warnings.

### CanvasNetMermaid-PieCharts: Every Pie Fixture Draws Strictly

**Tests**: `MermaidPieIntegrationTests.CanvasNetMermaid_PieChart_Fixture_RendersStrictly` (a theory with one case for
each file), `MermaidPieIntegrationTests.CanvasNetMermaid_PieChart_DefaultTheme_PaintsSlicesAndLegend`,
`MermaidPieIntegrationTests.CanvasNetMermaid_PieChart_NegativeValue_FailsLocated`,
`MermaidGalleryTests.CanvasNetMermaid_SystemIntegration_Gallery_HoldsEveryDrawnDiagramType`,
`MermaidGalleryTests.CanvasNetMermaid_SystemIntegration_Gallery_RendersStrictWithoutWarnings`

Opens each of the 8 pie fixtures by its path (a basic chart, `showData` with a title wider than the chart, the
accessibility statements and comments, sections under one percent, a zero, a repeated label and decimals, more than
twelve sections, a left legend with a donut hole and a text position, a top legend in the `forest` theme, and a
bottom legend with a highlighted slice in the `dark` theme with theme variables) and renders it in strict mode, and
asserts the type `pie`, that the surface has the measured size, is not fully transparent, and that no report or
warning is given. The paint of the slices and the legend and the located negative value are asserted as above. The
gallery holds pie charts that render strictly without warnings.

### CanvasNetMermaid-GanttCharts: Every Gantt Fixture Draws Strictly, With The Caller's Clock

**Tests**: `MermaidGanttIntegrationTests.CanvasNetMermaid_GanttChart_Fixture_RendersStrictly` (a theory with one case
for each file), `MermaidGanttIntegrationTests.CanvasNetMermaid_GanttChart_DefaultTheme_PaintsTheTaskStates`,
`MermaidGanttIntegrationTests.CanvasNetMermaid_GanttChart_Now_DrawsTheTodayLineOrReports`,
`MermaidGanttIntegrationTests.CanvasNetMermaid_GanttChart_SkippedTicksAndExcludes_AreReported`,
`MermaidGanttIntegrationTests.CanvasNetMermaid_GanttChart_InvalidDate_FailsLocated`,
`MermaidGalleryTests.CanvasNetMermaid_SystemIntegration_Gallery_HoldsEveryDrawnDiagramType`,
`MermaidGalleryTests.CanvasNetMermaid_SystemIntegration_Gallery_RendersStrictWithoutWarnings`

Opens each of the 18 gantt fixtures by its path (the date fallback without a `dateFormat`, durations and inclusive end
dates, `after` and `until` references, task states, milestones and vertical markers, excludes and includes, a Friday
weekend, an axis format and a weekly tick interval, the top axis with the compact mode, times of day, click links, a
today marker style, configuration with a theme and line breaks in sections, texts beside their bars, excluded days over
a second pass, end dates read strictly, `dateFormat X`, and `numberSectionStyles: 0`) and renders it
in strict mode with the clock `Now` at 2024-06-12T10:30:00Z, and asserts the type `gantt`, that the surface has the
measured size, is not fully transparent, and that no report or warning is given. For a chart with
`todayMarker
stroke-width:6px` and a task `after nothing`, asserts that the today line is drawn red at the clock's time when `Now` is
set and not drawn without it, and that without it two `mermaid-gantt-today-unavailable` warnings are given, the first at
the task's line 5. For a chart with `excludes weekends`, `tickInterval 1second`, and a task of seven years, asserts the
`mermaid-gantt-ticks-skipped` and `mermaid-gantt-excludes-skipped` warnings. For a task dated `next week` under
`dateFormat YYYY-MM-DD` in `plan.mmd`, asserts that measuring fails with `mermaid-render-failed` at line 4, column 3,
with Mermaid's `Invalid date:next week` message. The gallery holds gantt charts that render strictly without warnings.

### CanvasNetMermaid-Mindmaps: Mindmaps Are Drawn, Laid Out By Cose-Bilkent

**Tests**: `MermaidMindmapReferenceLayoutTests.CanvasNetMermaid_MindmapReferenceLayout_MatchesMermaid`,
`MermaidMindmapIntegrationTests.CanvasNetMermaid_Mindmap_DefaultTheme_PaintsRootAndSections`,
`MermaidMindmapIntegrationTests.CanvasNetMermaid_Mindmap_Empty_IsATransparentPaddedPicture`,
`MermaidMindmapIntegrationTests.CanvasNetMermaid_Mindmap_SecondRoot_FailsLocated`,
`MermaidGalleryTests.CanvasNetMermaid_SystemIntegration_Gallery_HoldsEveryDrawnDiagramType`,
`MermaidGalleryTests.CanvasNetMermaid_SystemIntegration_Gallery_RendersStrictWithoutWarnings`

Compares the whole drawing of each mindmap fixture but the documented `02-shapes` with its mermaid-cli golden (see
_CanvasNetMermaid-LayoutFidelity_), which covers the tree of the indentation, every node shape, plain, quoted, and
markdown texts and their wrapping, classes and icons, the sections and the theme colors, the edges by section and
depth, and the mindmap settings, laid out by cose-bilkent; asserts the root and section colors of a mindmap in pixels;
asserts that the keyword alone renders strictly, without any report, as a transparent picture of 20 by 20 pixels with
no nodes and no edges; and asserts that a second root fails at open, located. The gallery holds mindmaps that render
strictly without warnings, and holds every drawn diagram type, mindmaps included.

### CanvasNetMermaid-PacketDiagrams: Packet Diagrams Are Drawn In Rows

**Tests**: `MermaidPacketIntegrationTests.CanvasNetMermaid_PacketDiagram_Fixture_RendersStrictly`,
`MermaidPacketIntegrationTests.CanvasNetMermaid_PacketDiagram_PacketBeta_DrawsAsPacket`,
`MermaidPacketIntegrationTests.CanvasNetMermaid_PacketDiagram_Pixels_CarryTheThemeColors`,
`MermaidPacketIntegrationTests.CanvasNetMermaid_PacketDiagram_GapBetweenBlocks_FailsLocated`,
`MermaidChartReferenceLayoutTests.CanvasNetMermaid_PacketReferenceLayout_MatchesMermaid`,
`MermaidGalleryTests.CanvasNetMermaid_SystemIntegration_Gallery_HoldsEveryDrawnDiagramType`,
`MermaidGalleryTests.CanvasNetMermaid_SystemIntegration_Gallery_RendersStrictWithoutWarnings`

Renders each of the 13 packet fixtures (ranges, single bits, bit counts, row splits, the bit numbers turned off, the
geometry settings, titles and accessibility texts, the themes, theme variables, labels with escapes and entities, the
keyword alone, and `themeCSS`) in strict mode and asserts the type, that it measures as it renders, that it draws
something unless it has no block, and that it gives no report; compares the drawing of each fixture with its mermaid-cli
golden (see _CanvasNetMermaid-LayoutFidelity_); asserts that `packet` and `packet-beta` draw the same pixels; asserts
the block colors of the default and dark themes in pixels; and asserts that a gap between two blocks fails at open,
located, with Mermaid's message. The gallery holds a packet diagram that renders strictly without warnings, and holds
every drawn diagram type, packet diagrams included.

### CanvasNetMermaid-GitGraphs: Git Graphs Are Drawn As Mermaid Draws Them

**Tests**: `MermaidGitGraphIntegrationTests.CanvasNetMermaid_GitGraph_Fixture_RendersStrictly`,
`MermaidGitGraphIntegrationTests.CanvasNetMermaid_GitGraph_Pixels_CarryTheThemeColors`,
`MermaidGitGraphIntegrationTests.CanvasNetMermaid_GitGraph_RejectedMerge_FailsLocated`,
`MermaidGitGraphIntegrationTests.CanvasNetMermaid_GitGraph_SiteMainBranch_IsUsedWhenDrawn`,
`MermaidGitGraphIntegrationTests.CanvasNetMermaid_GitGraph_GeneratedIds_AreDeterministic`,
`MermaidGitGraphReferenceLayoutTests.CanvasNetMermaid_GitGraphReferenceLayout_MatchesMermaid`,
`MermaidGalleryTests.CanvasNetMermaid_SystemIntegration_Gallery_HoldsEveryDrawnDiagramType`,
`MermaidGalleryTests.CanvasNetMermaid_SystemIntegration_Gallery_RendersStrictWithoutWarnings`

Renders each of the 18 git graph fixtures (generated ids, branches and merges, the normal, reverse, and highlight
commits, cherry-picks, rerouted arrows, the three directions, parallel commits in each direction, labels that are not
rotated, hidden branches and commit labels, many branches with orders and a renamed main branch, titles and
accessibility texts, labels with markup and entities, the dark, forest, and neutral themes with the `neo` look, and
theme variables with `themeCSS`) in strict mode and asserts the type, the supported flag, that it measures as it
renders, that it draws something, and that it gives no report; compares the drawing of each fixture with its
mermaid-cli golden (see _CanvasNetMermaid-LayoutFidelity_); asserts the main branch's commit color of the default and
dark themes in pixels; asserts that a merge of a branch that does not exist fails at open, located, with Mermaid's
message; asserts that the ids generated for commits without one are the same in two documents of the same text, which
draw the same pixels, and are `0-eb28011`, `1-cc99105`, and `3-4bc15df` for the labeled commits of a graph of two
branches, a tag, a merge (drawn without a label), and a last commit; and asserts that a site configuration naming the
main branch `trunk` runs the statements
with that name, so the same graph's `checkout main` fails when measured with `mermaid-render-failed` at line 5 and
Mermaid's message, while a graph of one commit draws the branch `trunk`. The gallery holds a git graph that renders
strictly without warnings, and holds every drawn diagram type, git graphs included.

### CanvasNetMermaid-LayoutFidelity: The Layout Matches Mermaid

**Tests**: `MermaidReferenceLayoutTests.CanvasNetMermaid_Flowchart_ReferenceLayout_MatchesMermaid`,
`MermaidReferenceLayoutTests.CanvasNetMermaid_Flowchart_ReferenceGoldens_CoverEveryFixture`,
`MermaidSequenceReferenceLayoutTests.CanvasNetMermaid_SequenceReferenceLayout_MatchesMermaid`,
`MermaidSequenceReferenceLayoutTests.CanvasNetMermaid_Sequence_ReferenceGoldens_CoverEveryFixture`,
`MermaidReferenceLayoutTests.CanvasNetMermaid_ClassDiagram_ReferenceLayout_MatchesMermaid`,
`MermaidReferenceLayoutTests.CanvasNetMermaid_ClassDiagram_ReferenceGoldens_CoverEveryFixture`,
`MermaidReferenceLayoutTests.CanvasNetMermaid_StateDiagram_ReferenceLayout_MatchesMermaid`,
`MermaidReferenceLayoutTests.CanvasNetMermaid_StateDiagram_ReferenceGoldens_CoverEveryFixture`,
`MermaidReferenceLayoutTests.CanvasNetMermaid_ErDiagram_ReferenceLayout_MatchesMermaid`,
`MermaidReferenceLayoutTests.CanvasNetMermaid_ErDiagram_ReferenceGoldens_CoverEveryFixture`,
`MermaidChartReferenceLayoutTests.CanvasNetMermaid_PieReferenceLayout_MatchesMermaid`,
`MermaidChartReferenceLayoutTests.CanvasNetMermaid_Pie_ReferenceGoldens_CoverEveryFixture`,
`MermaidChartReferenceLayoutTests.CanvasNetMermaid_GanttReferenceLayout_MatchesMermaid`,
`MermaidChartReferenceLayoutTests.CanvasNetMermaid_Gantt_ReferenceGoldens_CoverEveryFixture`,
`MermaidMindmapReferenceLayoutTests.CanvasNetMermaid_MindmapReferenceSizes_MatchMermaid`,
`MermaidMindmapReferenceLayoutTests.CanvasNetMermaid_MindmapReferenceSeededLayout_MatchesMermaid`,
`MermaidMindmapReferenceLayoutTests.CanvasNetMermaid_MindmapReferenceLayout_MatchesMermaid`,
`MermaidMindmapReferenceLayoutTests.CanvasNetMermaid_Mindmap_ReferenceGoldens_CoverEveryFixture`,
`MermaidMindmapReferenceLayoutTests.CanvasNetMermaid_Mindmap_ReferenceExclusions_NameGoldens`,
`MermaidChartReferenceLayoutTests.CanvasNetMermaid_PacketReferenceLayout_MatchesMermaid`,
`MermaidChartReferenceLayoutTests.CanvasNetMermaid_Packet_ReferenceGoldens_CoverEveryFixture`,
`MermaidGitGraphReferenceLayoutTests.CanvasNetMermaid_GitGraphReferenceLayout_MatchesMermaid`,
`MermaidGitGraphReferenceLayoutTests.CanvasNetMermaid_GitGraph_ReferenceGoldens_CoverEveryFixture`

Compares the scene of each fixture with its mermaid-cli golden within the documented tolerance, and asserts that
every fixture has a golden except the documented `38-unicode` exclusion; compares the scene of each sequence fixture
with its golden (the view box, the title, the participants and their parts, paint, and labels, the lifelines, the
messages with their markers, texts, numbers, and circles, the notes, activations, frames, backgrounds, boxes, menus,
and the paint order) within the documented tolerances and allowances, and asserts that every sequence fixture has a
golden; compares the scene of each class fixture with its golden (the view box, the nodes and the parts of each class
box, the namespaces with their titles, the edges, the edge labels, the terminal labels, and the title box) at 0.01
pixel, 0.23 pixel for `16-config-title-links`, and asserts that every class fixture has a golden. Compares the scene
of each state fixture with its golden (the view box, each node's position, box, and drawn parts, each cluster's
rectangle, inner rectangle, and title, each edge's length and 21 sampled points, the edge label centers, and the
title box) at 0.01 pixel with no exception, and asserts that the 14 state fixtures and the state goldens are the same
set. Compares the scene of each ER fixture with its golden (the view box, each node's position and box, the parts of
each table: the outline, the rows, each text cell, and the dividers, each subgraph's rectangle and title, each edge's
length and 21 sampled points, the edge label centers, and the title box) at 0.01 pixel with no exception, and asserts
that the 12 ER fixtures and the ER goldens are the same set. Compares the scene of each pie fixture with its golden
(the view box, the outer circle, each slice's path, box, and paint, the percentage labels, the title, and each legend
entry with its square and text, with the computed paint compared exactly) at 0.01 pixel, 0.02 pixel for text boxes and
slice arc boxes, and asserts that the 8 pie fixtures and the pie goldens are the same set. Compares the scene of each
gantt fixture, laid out with `Now` at the harness's fixed clock of 2024-06-12T10:30:00Z, with its golden (the view box,
the excluded days, each axis's ticks and labels, the section rows, the bars (their corners after a milestone's
transform) and texts, the section titles, the today line, and the title, with the computed paint compared exactly) at
0.01 pixel, 0.02 pixel for text boxes, and asserts that the 18 gantt fixtures and the gantt goldens are the same set.
For each of the 14 mindmap fixtures (a theory per tier), asserts that the node sizes the library gives the layout (each
node's box in single precision) equal the sizes Chrome's `getBBox` gave Mermaid's layout within 0.02 pixel; that
`CoseLayout.Run`, given the golden's own sizes and edges, gives every node center and every cytoscape edge point of the
golden bit for bit; and, for every fixture but `02-shapes`, that the whole drawing (the view box, each node's center,
box, and paint: the body's fill and stroke, the line's stroke and width, and every label run's color; each edge's 21
samples, length, and paint) matches the golden at 0.01 pixel with the colors exact. Asserts that the 14 mindmap fixtures
and the mindmap goldens are the same set, and that the one exclusion from the whole-drawing tier, `02-shapes` (Chrome's
box of its cloud node is about 8e-6 pixel taller, as it bounds the arcs in single precision, and the chaotic layout
moves every node), names a golden. Compares the scene of each packet fixture with its golden (the view box, every block
rectangle with its fill, stroke, and stroke width, and every label, bit number, and title with its content, anchor, x,
font size, color, and box) at 0.01 pixel, 0.02 pixel for text boxes, and asserts that the 13 packet fixtures and the
packet goldens are the same set. Compares the scene of each git graph fixture with its golden (the view box, every
branch line, label rectangle, label transform, and label row with its font size, the numbers of every arrow's path
data, every bullet's circle, rectangle, line, or cross, every commit label and tag with its rectangle or polygon, hole,
and text mapped through their transforms, and the title, each with its fill, stroke, stroke width, dashes, and shadow
compared exactly) at 0.01 pixel, 0.02 pixel for text boxes, with no fixture excluded, and asserts that the 18 git graph
fixtures and the git graph goldens are the same set.

### CanvasNetMermaid-MeasureRender: Natural Size, Scale, Exact Size, And Background

**Tests**: `MermaidFlowchartIntegrationTests.CanvasNetMermaid_Flowchart_Scale_KeepsTheDrawing`,
`MermaidSequenceIntegrationTests.CanvasNetMermaid_Sequence_Scale_MultipliesTheNaturalSize`,
`MermaidClassIntegrationTests.CanvasNetMermaid_ClassDiagram_Scale_MultipliesTheNaturalSize`,
`MermaidStateIntegrationTests.CanvasNetMermaid_StateDiagram_Scale_MultipliesTheNaturalSize`,
`MermaidErIntegrationTests.CanvasNetMermaid_ErDiagram_Scale_MultipliesTheNaturalSize`,
`MermaidPieIntegrationTests.CanvasNetMermaid_PieChart_Scale_MultipliesTheNaturalSize`,
`MermaidMindmapIntegrationTests.CanvasNetMermaid_Mindmap_Scale_MultipliesTheNaturalSize`,
`MermaidFlowchartIntegrationTests.CanvasNetMermaid_Flowchart_Background_IsTransparentByDefault`,
`MermaidPieIntegrationTests.CanvasNetMermaid_PieChart_NoBackground_LeavesTheCornersTransparent`,
`MermaidMindmapIntegrationTests.CanvasNetMermaid_Mindmap_NoBackground_LeavesTheCornersTransparent`,
`MermaidFlowchartIntegrationTests.CanvasNetMermaid_Flowchart_FixedSize_FitsTheDiagram`,
`MermaidUnsupportedDiagramTests.CanvasNetMermaid_RenderSized_InvalidSize_Throws`

Asserts that a scale multiplies the natural size and keeps the drawing, that the natural size of a sequence diagram, a
class diagram, a state diagram, an ER diagram, a pie chart (450 pixels high), and a mindmap is its view box rounded up
and doubles at scale 2, that the corners of a flowchart, a pie chart, and a mindmap are transparent without a background
(the mindmap's drawing itself not transparent) and take the background
color when one is given, that an exact size gives a picture of exactly that size with the drawing in it, and that an
invalid size is rejected.

### CanvasNetMermaid-ImageEmbedding: Pictures Only Through The Resolver

**Tests**: `MermaidSystemIntegrationTests.CanvasNetMermaid_ImageEmbedding_PictureComesFromTheResolver`

Renders a node with an `img` shape datum with a stub resolver that returns a solid red picture and asserts that the
resolver is asked for that link and the red pixels are drawn; without a resolver, asserts one located
`mermaid-image-unresolved` warning and no red pixel.

### CanvasNetMermaid-UnsupportedFeatures: Reported Once, In Order, And Strict Mode

**Tests**: `MermaidSystemIntegrationTests.CanvasNetMermaid_Reports_AreCodedAndLocated`,
`MermaidUnsupportedDiagramTests.CanvasNetMermaid_Render_JourneyDiagram_ReportsOnceAndDrawsTransparentPlaceholder`,
`MermaidUnsupportedDiagramTests.CanvasNetMermaid_Render_JourneyDiagramStrict_Throws`,
`MermaidUnsupportedDiagramTests.CanvasNetMermaid_Measure_JourneyDiagram_ReportsAndGivesScaledPlaceholderSize`,
`MermaidUnsupportedDiagramTests.CanvasNetMermaid_RenderSized_JourneyDiagramWithBackground_FillsBackground`,
`MermaidUnsupportedDiagramTests.CanvasNetMermaid_Render_OtherKnownTypes_AreUnsupportedWithHint`,
`MermaidUnsupportedDiagramTests.CanvasNetMermaid_Render_FailingHandler_Propagates`,
`MermaidSequenceIntegrationTests.CanvasNetMermaid_Sequence_NeoLook_IsReportedAndDrawnClassic`,
`MermaidSequenceIntegrationTests.CanvasNetMermaid_Sequence_Math_IsReportedAndDrawnAsText`,
`MermaidSequenceIntegrationTests.CanvasNetMermaid_Sequence_PropertiesIcon_IsReportedOrFails`,
`MermaidClassIntegrationTests.CanvasNetMermaid_ClassDiagram_HandDrawnLook_IsReportedAndDrawnClassic`,
`MermaidClassIntegrationTests.CanvasNetMermaid_ClassDiagram_ElkLayout_IsReportedAndLaidOutWithDagre`,
`MermaidClassIntegrationTests.CanvasNetMermaid_ClassDiagram_UserStyleIgnored_IsReportedButDiagramStylesAreNot`,
`MermaidStateIntegrationTests.CanvasNetMermaid_StateDiagram_HandDrawnLook_IsReportedAndDrawnClassic`,
`MermaidStateIntegrationTests.CanvasNetMermaid_StateDiagram_ElkLayout_IsReportedAndLaidOutWithDagre`,
`MermaidStateIntegrationTests.CanvasNetMermaid_StateDiagram_LegacyRenderer_IsReportedAndDrawnTheSame`,
`MermaidErIntegrationTests.CanvasNetMermaid_ErDiagram_HandDrawnLook_IsReportedAndDrawnClassic`,
`MermaidErIntegrationTests.CanvasNetMermaid_ErDiagram_ElkLayout_IsReportedAndLaidOutWithDagre`,
`MermaidMindmapIntegrationTests.CanvasNetMermaid_Mindmap_UnsupportedFeatureStrict_Throws`

Asserts the `mermaid-look-unsupported` report at its frontmatter line and the `mermaid-style-ignored` warning at the
styled node while the diagram is drawn; that a user journey gives one located report whose hint says that journey
diagrams are not supported and names the supported types (mindmap, packet, and gitGraph included), and a
transparent 16 by 16 picture (scaled by the scale, filled by a background), throws in strict mode, and that every
other known type that is not drawn (quadrantChart, timeline, and kanban among them) does the same with its own hint; and
that a failing handler's exception reaches the caller. For a
sequence diagram, asserts that the `neo` look is reported once and drawn with the same pixels as the classic look and
throws in strict mode; that `$$` math is reported once as `mermaid-math-unsupported` at its message (line 3) and
throws in strict mode; and that a `properties` icon that names an image is reported as `mermaid-icon-unsupported`, a
symbol reference (`@server`, which Mermaid cannot find either) is not reported, and an icon that is not text fails
with `mermaid-render-failed` at the participant (line 2), as Mermaid fails. For a class diagram, asserts that the
`handDrawn` look is reported once as `mermaid-look-unsupported`, drawn with the same pixels as the classic look, and
refused in strict mode; that `layout: elk` is reported once as `mermaid-layout-unsupported` and drawn with the same
pixels as dagre; and that `style A cursor:pointer` is one `mermaid-style-ignored` warning at line 2 (the class's first
mention) while the styles the diagram writes itself (the note colors, the opacity of a lollipop interface) give no
report. For a state diagram, asserts that the `handDrawn` look is reported once as `mermaid-look-unsupported` and
drawn with the same pixels as the classic look; that `layout: elk` is reported once as `mermaid-layout-unsupported`
and drawn with the same pixels as dagre; and that a `stateDiagram` whose frontmatter sets `state.defaultRenderer:
dagre-d3` gives one `mermaid-layout-unsupported` report at the `state` key (line 3) with the hint
`state.defaultRenderer selects Mermaid's legacy state renderer; drawn with dagre-wrapper`, draws the same pixels as
the `stateDiagram-v2` text, and is refused in strict mode. For an ER diagram, asserts that the `handDrawn` look is
reported once as `mermaid-look-unsupported` and drawn with the same pixels as the classic look, and that
`layout: elk` is reported once as `mermaid-layout-unsupported` and drawn with the same pixels as dagre. For a mindmap
(a theory with four cases), asserts that an `::icon(fa fa-book)`, a frontmatter `layout: elk`, a frontmatter
`look: handDrawn`, and a `fa:fa-book` label are each refused in strict mode with a `MermaidUnsupportedFeatureException`
of the feature `mermaid-icon-unsupported`, `mermaid-layout-unsupported`, `mermaid-look-unsupported`, or
`mermaid-icon-unsupported`, after exactly one report.

### CanvasNetMermaid-Limits: Hostile Input Is Bounded

**Tests**: `MermaidHardeningTests.CanvasNetMermaid_Hardening_TooManyEdges_IsALocatedLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_TooLongText_IsRefused`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_DeepNesting_IsALocatedLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_NestingAtTheLimit_Renders`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_FlowchartNestedThroughIds_IsALocatedNestingLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_FlowchartNestedUnderCollapsedSubgraph_IsALocatedNestingLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_YamlAliasBomb_IsRefused`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_LongLabel_IsALimitOrWraps`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_CompleteGraph_RendersOrHitsTheWorkLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_LongChainAndManySubgraphs_Render`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_MutatedFixtures_ThrowOnlyDocumentedExceptions`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_HostileCorpus_RendersOrFailsLocated`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_SequenceDeepBlocks_AreALocatedLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_SequenceTooManyParticipants_IsALocatedLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_SequenceTooManyBoxes_IsALocatedLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_SequenceMetadataAliasList_IsALocatedLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_SequenceLongMessage_IsALocatedLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_SequenceWrapBomb_RendersOrHitsTheWorkLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_SequenceActivationStorm_RendersHitsTheLimitOrCancels`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_MutatedSequenceFixtures_ThrowOnlyDocumentedExceptions`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_SequenceTinyFitScale_Renders`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_SequenceHugeStrokeWidth_ShadowIsTheFillsShadow`,
`MermaidSequenceIntegrationTests.CanvasNetMermaid_Sequence_WorkLimit_FailsWithLimitExceeded`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_HostileClassDiagram_RendersOrFailsLocated`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_ClassWithManyMembers_RendersOrHitsTheWorkLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_MutatedClassFixtures_ThrowOnlyDocumentedExceptions`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_ClassTooManyNamespaces_IsALocatedLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_ClassNestedNamespaceIds_AreALocatedLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_ClassDottedNamespacesTooDeep_AreALocatedLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_ClassDottedNamespacesAtTheLimit_Render`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_ClassNonStringNoteThemeVariables_Render`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_ClassLabelWithGenericType_IsALocatedLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_ClassRepeatedClassesAndStyles_AreALocatedLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_ClassNamespacesChainedByClassNames_AreALocatedLimit`,
`MermaidClassIntegrationTests.CanvasNetMermaid_ClassDiagram_WorkLimit_FailsWithLimitExceeded`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_HostileStateDiagram_RendersOrFailsLocated`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_StateManyNotes_IsALocatedNodeLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_StateNestedRegions_IsALocatedNestingLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_StateManyDescriptions_IsALocatedLabelLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_StateNestedThroughNames_IsALocatedNestingLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_StateAndErRepeatedClassesAndStyles_AreALocatedLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_StateLongChain_RendersOrHitsTheWorkLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_MutatedStateFixtures_ThrowOnlyDocumentedExceptions`,
`MermaidStateIntegrationTests.CanvasNetMermaid_StateDiagram_WorkLimit_FailsWithLimitExceeded`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_HostileErDiagram_RendersOrFailsLocated`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_ErManyEntities_IsALocatedNodeLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_ErNestedSubgraphs_IsALocatedNestingLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_ErNestedThroughNames_IsALocatedNestingLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_ErLongComment_IsALocatedLabelLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_ErManyAttributes_Render`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_MutatedErFixtures_ThrowOnlyDocumentedExceptions`,
`MermaidErIntegrationTests.CanvasNetMermaid_ErDiagram_WorkLimit_FailsWithLimitExceeded`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_HostileMindmap_RendersOrFailsLocated`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_MindmapManyNodes_IsALocatedNodeLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_MindmapDeepNesting_IsALocatedNestingLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_MindmapLongTexts_AreALocatedLabelLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_MindmapLargeTree_RendersOrHitsTheWorkLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_MutatedMindmapFixtures_ThrowOnlyDocumentedExceptions`,
`MermaidMindmapIntegrationTests.CanvasNetMermaid_Mindmap_WorkLimit_FailsWithLimitExceeded`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_HostilePacketDiagram_RendersOrFailsLocated`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_PacketHugeBlock_IsALocatedNodeLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_PacketManyBlocks_IsALocatedNodeLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_PacketLongTexts_AreALocatedLabelLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_MutatedPacketFixtures_ThrowOnlyDocumentedExceptions`,
`MermaidPacketIntegrationTests.CanvasNetMermaid_PacketDiagram_HugeRange_FailsWithLimitExceeded`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_HostileGitGraph_RendersOrFailsLocated`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_GitGraphManyCommits_IsALocatedNodeLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_GitGraphLongTexts_AreALocatedLabelLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_GitGraphCherryPickChain_EndsAtTheLabelLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_GitGraphManyReroutes_StaysWithinTheWorkLimit`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_MutatedGitGraphFixtures_ThrowOnlyDocumentedExceptions`,
`MermaidGitGraphIntegrationTests.CanvasNetMermaid_GitGraph_Limits_FailWithLimitExceeded`

Asserts located `mermaid-limit-exceeded` errors for too many edges and too deep nesting (and a render at the limit),
`InvalidDataException` for too long text, refusal of a YAML alias bomb, a label limit that can be raised, a complete
graph of 30 nodes that renders with the default work budget and fails with a small one, long chains and many subgraphs
that render, and only documented exceptions from the seeded mutation fuzzer; and, for a hostile corpus of inputs found
by review, that the two diagrams on which Mermaid 11.17.2 fails (a subgraph used as a node of another subgraph, a
self-loop on a subgraph) fail with a located `mermaid-render-failed` at line 9, and that font sizes that are zero or
infinite in single precision (in `style`, `classDef`, `linkStyle`, an HTML label, and the theme's `fontSize`) and dash
patterns beyond single precision render a picture; and that 32 flowchart subgraphs chained by id (`subgraph Si` listing
`S(i+1)`, declared in order or in reverse, so a subgraph is re-used inside one declared after it) render while 40 fail
when measured with `mermaid-limit-exceeded` and the message `subgraphs are nested deeper than 32 levels`, located at
`subgraph S33` (line 101 in order, line 20 in reverse), also when the root `S0` is collapsed and hides the whole chain
from the layout (line 101, while 32 render). For sequence diagrams, asserts that 33 nested blocks fail at open
with a located `mermaid-limit-exceeded` while 32 render, that 2,001 participants fail at the extra participant (line
2,002), that 1,000 boxes of one participant each fail at the next box (line 3,002), that a participant metadata alias of
9,000 `1e20` numbers (about 45,000 characters, 197,999 once converted) fails with `MaxLabelLength` at its line, and that
a message longer than the label limit fails at its line; that wrapped messages and notes of one 4,000-character word
(Mermaid's hyphenation, quadratic in the word) render within the default layout work and fail with
`mermaid-limit-exceeded` under a small one; that 300 nested activations render, fail with the limit at the diagram
keyword under a small budget, and stop on a canceled token; that 200 messages under a work limit of 2,000 fail with
`mermaid-limit-exceeded` naming `MaxLayoutWork`; and that a seeded mutation fuzzer over the sequence fixtures (eight
mutations of each with the tokens of the sequence language) only ever raises the documented exception types; and that a
sequence diagram whose stroke widths round to zero in single precision when it is fitted to 400 by 300 pixels (a width
of 1e300, or an actor margin of 1e60) renders; and that a box around participants and a forced menu panel, both with a
drop shadow, given a stroke width of 1e300, 1e30, or 40,000 pixels by `themeCSS` (`g rect.rect` or
`.actorPopupMenuPanel`), render without an exception and with the same pixels as with a stroke width of zero (the stroke
is not drawn and the shadow is that of the fill). For class diagrams, asserts that hostile settings (paddings, spacings,
and font sizes of 1e300 or 1e-308, a dash of 1e300, cardinalities on a self-loop, markup in a note) render fitted to 400
by 300 pixels or fail with a located render error; that a class of 1,500 members renders with the default work budget
and fails with `mermaid-limit-exceeded` under a small one; that a seeded mutation fuzzer over the class fixtures only
ever raises the documented exception types; that 1,000 namespaces of one class each fail at the next namespace (line
3,002) with `MaxNodes`; that two nested namespaces of 3,000-character names (a 6,001-character qualified id) and a
3,000-character label on a class with a 3,000-character generic type fail with `MaxLabelLength` at their line; that a
namespace name of 33 dotted parts and one of 1,000 dotted parts (which once overflowed the stack) fail at their line
(line 2) with `MaxNestingDepth`, as do 17 nested namespaces of two-part names at the 17th (line 18), while a name of 32
parts and 16 nested namespaces of two-part names render with 32 subgraphs; that note theme variables set to a number or
a boolean by a directive or the frontmatter render without an exception; that CSS classes and styles repeated on one
class (a class listed 1,000 times in three `cssClass` statements, a style class listed 2,000 times in one `classDef`,
two style classes or two `style` statements of 2,099 characters each) fail at open with `MaxLabelLength` at the
statement that makes them too long; that 32 namespaces chained by class names (namespace `Ni` holds a class named
`N(i+1)`) render while 40 fail when measured with `mermaid-limit-exceeded` and the message `namespaces are nested deeper
than 32 levels`, located at `class N33` (line 99); and that a class diagram under a small work limit fails with
`mermaid-limit-exceeded`. For state diagrams, asserts that hostile diagrams (a `state` node and rank spacing of 1e300,
theme font sizes of 1e300 and 1e-308, a dash of 1e300, self-loops on a state and on a composite state, a note on a
composite state, markup and a script in a nested note, composite states inside each other, a note on a state that
appears nowhere else, and a cycle through a fork, a join, and a choice) render fitted to 400 by 300 pixels or fail with
a located render error; that 700 states with a note each (2,100 nodes once each note and its group are counted) fail at
open with `MaxNodes` at the note that makes the 2,001st (line 1,335); that 16 composite states each nested in a
concurrent region of the one before (32 nesting levels) render with 16 subgraphs while 17 fail at open with
`MaxNestingDepth` at the 17th (line 18); that 500 descriptions `S : abcd` of one state fail at open at the 457th (line
458), where the joined descriptions reach 4,099 characters; that 32 composite states chained by name (`state Si { S(i+1)
}` at the top) render while chains of 40 and 1,700 fail when measured with `mermaid-limit-exceeded` naming
`MaxNestingDepth`, located at `state S33` (line 101); that CSS classes and styles repeated on one state fail at open
with `MaxLabelLength` at the statement that makes them too long, with the length they reach there (a theory shared with
the ER cases): the reviewed shape of four `classDef c` statements of 1,000 styles, two `class` statements listing the
state 2,000 times, and 300 mentions at the second definition (line 3, 4,099 characters), the two `class` statements
alone at the second (line 3, 4,098 characters with `statediagram-state`), and two style classes of 2,099 characters on
one state at the statement that gave the second (line 5, 4,099 characters compiled); that a chain of 300 labeled
transitions renders with the default work budget and fails with `mermaid-limit-exceeded` under a work limit of 5,000;
that the seeded mutation fuzzer over the state fixtures only ever raises the documented exception types; and that a
chain of 60 transitions under a work limit of 2,000 fails with `mermaid-limit-exceeded` naming `MaxLayoutWork`. For ER
diagrams, asserts that hostile diagrams (an `er` node and rank spacing of 1e300, a diagram padding, entity padding, and
minimum entity width of 1e300, theme font sizes of 1e300 and 1e-308, a stroke width and dash of 1e300,
self-relationships, the `u` (`md_parent`) cardinality, generic types, comments over two lines, markup and a script in a
comment, duplicate subgraph ids, relationships to subgraphs, an entity named like a later subgraph, and a subgraph that
holds a relationship to itself) render fitted to 400 by 300 pixels or fail with a located render error; that 2,001
entities fail at open with `MaxNodes` at the extra entity (line 2,002); that 32 nested subgraphs render with 32
subgraphs while 33 fail at open with `MaxNestingDepth` at the 33rd (line 34); that 32 subgraphs chained by name
(`subgraph Si` holding `S(i+1)`) render while 40 fail when measured with `mermaid-limit-exceeded` and the message
`subgraphs are nested deeper than 32 levels`, located at `subgraph S33` (line 101); that a `style` statement listing an
entity 2,000 times with 450 styles (line 3, 4,103 characters) and a `classDef` statement listing a class 2,000 times
with 1,000 styles (line 3, 4,099 characters) fail at open with `MaxLabelLength`; that an attribute comment of 4,097
characters fails at open with `MaxLabelLength` at its line (line 4); that an entity of 1,000 attributes with keys and
comments renders; that the seeded mutation fuzzer over the ER fixtures only ever raises the documented exception types;
and that a chain of 60 relationships under a work limit of 2,000 fails with `mermaid-limit-exceeded` naming
`MaxLayoutWork`. For pie charts, asserts that hostile charts (text sizes and stroke widths of 1e300, a stroke width that
is not a number, an unknown legend position, a text position of 1e300 and a negative donut hole, an empty chart, all
values zero, a value beyond the double range, values far apart, and markup in the texts) render fitted to 400 by 300
pixels or fail with a located render error; that more section statements than `MaxNodes` (a repeated label counting too)
fail at open at the first one over the limit; that a title, an accessible title, both forms of the accessible
description, a label, and a legend text that `showData` makes too long fail at open with `MaxLabelLength` at their
statement; that a seeded mutation fuzzer over the pie fixtures only ever raises the documented exception types; and that
a pie chart under a small work limit fails with `mermaid-limit-exceeded` naming `MaxLayoutWork`. For gantt charts,
asserts that hostile charts (sizes and paddings of 1e300, zero, and negative values, a tick interval of one millisecond
and one of 100,000,000 seconds over 100 years, an axis format of ten `%c`, durations of 100,000 years and 1,000,000,000
days, a compact section named `constructor`, sections named `__proto__` and `7`, a chart with only a title, markup in
the texts, circular references, a date format with a token of a plugin Mermaid does not load, and a today marker style
with extreme values) render fitted to 400 by 300 pixels or fail with a located render failure or limit; that more task
and section statements than `MaxNodes` fail at open at the first one over the limit; that every text (the title, a
section, a task name, task data, the date and axis formats, the tick interval, excludes, the today marker, the
accessibility texts, and the `click` link and call arguments) longer than `MaxLabelLength` fails at open at its
statement; that excluded days over a long span, tiny tick steps over a long domain, a long date format with many
excluded days, and a tick label that a format makes longer than `MaxLabelLength` fail at render with
`mermaid-limit-exceeded` (under a smaller work limit) while cancellation is honored; and that a seeded mutation fuzzer
over the gantt fixtures only ever raises the documented exception types. For mindmaps, asserts that hostile mindmaps
(a padding of 1e300, a `maxNodeWidth` and padding of 1e300 with cloud, bang, and hexagon nodes, negative and zero
paddings and widths, theme font sizes of 1e300 and 1e-300, a root of 16 children, long cloud and bang labels, markup,
references, and empty decorations, a lone root, and the keyword alone) render fitted to 400 by 300 pixels or fail with
a located `MermaidRenderException`; that a root with 2,000 children fails at open with `MaxNodes` at the extra node
(line 2,002); that a chain of 33 levels under the root fails at open with `MaxNestingDepth` at the 33rd (line 35) while
a chain of 32 renders; that an id, a description, a quoted and a markdown string, an icon, and a class of 4,097
characters fail at open with `MaxLabelLength` at their line, and that a class of 4,080 characters, which only the class
list Mermaid derives for the root makes too long, fails at the root's line (a theory with seven cases); that under the
default `MaxLayoutWork` a tree of 301 nodes (a root, 20 children, and 14 grandchildren of each) renders while one of
1,961 nodes (40 children of 48 grandchildren each) stops with `mermaid-limit-exceeded` naming `MaxLayoutWork` at the
keyword (line 1); that the seeded mutation fuzzer over the mindmap fixtures only ever raises the documented exception
types; and that a mindmap of five nodes under a work limit of 500 fails with `mermaid-limit-exceeded` at line 1 naming
`MaxLayoutWork`. For packet diagrams, asserts that hostile diagrams (row sizes of 0, -3, 1.5, and 1,000,000,000, a negative
row height and padding, a horizontal padding, a bit width, and a row height of 1e300, the bit numbers turned off, theme
font sizes of 1e300 pixels and a color that is not one, markup, entities, and escapes in the texts, and a bit count of
301 digits) render fitted to 400 by 300 pixels or fail with a located `MermaidRenderException`; that the range
`0-99999999` and the bit count `+99999999999999999999`, which Mermaid splits into 10,000 rows, fail when measured with
`mermaid-limit-exceeded` naming `MermaidLimits.MaxNodes` at their statement (line 2), as the range does through the
packet integration test; that 2,001 block statements fail at open with `MaxNodes` at the extra block (line 2,002); that
a title, an accessible title, both forms of the accessible description, and a label of 4,097 characters fail at open
with `MaxLabelLength` at their statement (line 3); and that a seeded mutation fuzzer over the packet fixtures only ever
raises the documented exception types. For git graphs, asserts that hostile graphs (a diagram padding of 1e300 and
-1e300, a title top margin of 1e300 and -1e300, a main branch order of 1e300 with parallel commits, a branch order of
413 digits, theme font sizes of 1e300 pixels, markup, a script, and an image in the ids, tags, branch names, and
messages, and the bottom-to-top placement on which Mermaid fails) render fitted to 400 by 300 pixels or fail with a
located `MermaidRenderException`; that 2,001 commits fail at open with `MaxNodes` at the commit that makes the 2,001st
node, the main branch counted (line 2,001); that a title, an accessible title, both forms of the accessible
description, an id, a message, a tag, and a branch name of 4,097 characters fail at open with `MaxLabelLength` at their
statement (line 3); that a chain of cherry-picks of cherry-picks, whose messages Mermaid composes from the picked
commit's message, ends with `MaxLabelLength` instead of growing without bound; that a graph of 300 rounds of commits on
two branches merged back each time, whose arrows are rerouted many times, lays out or fails with
`mermaid-limit-exceeded` under the default work limit and fails with it under a work limit of 10,000; that a seeded
mutation fuzzer over the git graph fixtures only ever raises the documented exception types; and that a graph of two
branches, a tag, and a merge fails at open with `mermaid-limit-exceeded` at line 4 under a `MaxNodes` of 3 and fails
the render with it under a `MaxLayoutWork` of 50.

### CanvasNetMermaid-UnsafeLinks: Encoded Unsafe Links Are Checked And Never Exposed

**Tests**: `MermaidHardeningTests.CanvasNetMermaid_Hardening_EncodedUnsafeLinks_AppearInNoOutput`

Opens and renders, through the public API, a class diagram (`link` with a tab inside the scheme, `click ... href` with a
percent-encoded letter, `link` with `&Tab;` and `&colon;` in mixed case), a flowchart (`click ... href` with a tab and
`click` with a percent-encoded letter), a sequence diagram (`link` with a percent-encoded letter, `links` with an
escaped white space and a mixed-case scheme), and a state diagram (`click` with a tab inside the scheme, `click ...
href` with a percent-encoded letter, `click ... href` with `&Tab;` and `&colon;` in mixed case, and `click` with the
character reference `&#106;`), and a gantt chart (`click ... href` with a tab inside the scheme, with a percent-encoded
letter, and with `&Tab;` and `&colon;` in mixed case), each with a tooltip or target where the statement allows one (a
theory with one case for each). Asserts that each renders a picture, that no report, hint, title, or accessible title
carries the link, and that the only links the parsed diagram keeps are `about:blank` (for the state diagram, in its
links and in its click statements alike, and for the gantt chart in the links of its tasks; the flowchart keeps none).

### CanvasNetMermaid-Determinism: Identical Renders

**Tests**: `MermaidFlowchartIntegrationTests.CanvasNetMermaid_Flowchart_RepeatedAndConcurrentRenders_AreIdentical`,
`MermaidSystemIntegrationTests.CanvasNetMermaid_Render_Concurrent_IsDeterministic`,
`MermaidSequenceIntegrationTests.CanvasNetMermaid_Sequence_RepeatedAndConcurrentRenders_AreIdentical`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_SequenceFixtures_RenderDeterministically`,
`MermaidClassIntegrationTests.CanvasNetMermaid_ClassDiagram_RepeatedAndConcurrentRenders_AreIdentical`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_ClassFixtures_RenderDeterministically`,
`MermaidStateIntegrationTests.CanvasNetMermaid_StateDiagram_RepeatedAndConcurrentRenders_AreIdentical`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_StateFixtures_RenderDeterministically`,
`MermaidErIntegrationTests.CanvasNetMermaid_ErDiagram_RepeatedAndConcurrentRenders_AreIdentical`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_ErFixtures_RenderDeterministically`,
`MermaidPieIntegrationTests.CanvasNetMermaid_PieChart_RepeatedAndConcurrentRenders_AreIdentical`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_PieFixtures_RenderDeterministically`,
`MermaidGanttIntegrationTests.CanvasNetMermaid_GanttChart_RepeatedAndConcurrentRenders_AreIdentical`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_GanttFixtures_RenderDeterministically`,
`MermaidGanttIntegrationTests.CanvasNetMermaid_GanttChart_Now_DrawsTheTodayLineOrReports`,
`MermaidMindmapIntegrationTests.CanvasNetMermaid_Mindmap_RepeatedAndConcurrentRenders_AreIdentical`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_MindmapFixtures_RenderDeterministically`,
`MermaidPacketIntegrationTests.CanvasNetMermaid_PacketDiagram_RepeatedAndConcurrentRenders_AreIdentical`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_PacketFixtures_RenderDeterministically`,
`MermaidGitGraphIntegrationTests.CanvasNetMermaid_GitGraph_RepeatedAndConcurrentRenders_AreIdentical`,
`MermaidHardeningTests.CanvasNetMermaid_Hardening_GitGraphFixtures_RenderDeterministically`,
`MermaidGitGraphIntegrationTests.CanvasNetMermaid_GitGraph_GeneratedIds_AreDeterministic`

Renders the same documents repeatedly and from parallel threads and asserts identical pixels, sizes, and reports;
renders a numbered sequence fixture twice and six times in parallel, and every sequence fixture twice, and asserts
identical pixels; renders the class fixture `10-namespaces` twice and six times in parallel, and every class fixture
twice, and asserts identical pixels; renders the state fixture `07-concurrency` and the ER fixture `11-subgraphs`
twice and six times in parallel, and every state and ER fixture twice, and asserts identical pixels; renders a pie
chart and a gantt chart twice and in parallel, and every pie and gantt fixture twice (the gantt fixtures also on
parallel threads), and asserts identical pixels; renders a mindmap of every shape twice and six times in parallel, and
every mindmap fixture twice on parallel threads, and asserts identical pixels (the cose layout makes no random choice
and does not use the host's math library, which the Cose subsystem verifies bit for bit); renders the packet fixture
`04-row-splits` twice and six times in parallel, and every packet fixture twice, and asserts identical pixels; renders
the git graph fixture `04-rerouting` twice and six times in parallel, and every git graph fixture twice, and asserts
identical pixels, and asserts that two documents of the same git graph generate the same commit ids and draw the same
pixels (the ids come from a fixed pseudo-random sequence restarted for each document, never from the host's random
generator). That the
library never
reads the system clock is shown by the gantt
chart without `Now`, which draws no today line and reports what needs today, while the same chart with `Now` draws the
line at that time.

### CanvasNetMermaid-Cancellation: A Canceled Token Stops The Work

**Tests**: `MermaidSystemIntegrationTests.CanvasNetMermaid_Render_CanceledToken_Throws`,
`MermaidFlowchartIntegrationTests.CanvasNetMermaid_Flowchart_CanceledToken_Throws`,
`MermaidSequenceIntegrationTests.CanvasNetMermaid_Sequence_CanceledToken_Throws`,
`MermaidClassIntegrationTests.CanvasNetMermaid_ClassDiagram_CanceledToken_Throws`,
`MermaidStateIntegrationTests.CanvasNetMermaid_StateDiagram_CanceledToken_Throws`,
`MermaidErIntegrationTests.CanvasNetMermaid_ErDiagram_CanceledToken_Throws`,
`MermaidPieIntegrationTests.CanvasNetMermaid_PieChart_CanceledToken_Throws`,
`MermaidGanttIntegrationTests.CanvasNetMermaid_GanttChart_CanceledToken_Throws`,
`MermaidMindmapIntegrationTests.CanvasNetMermaid_Mindmap_CanceledToken_Throws`,
`MermaidPacketIntegrationTests.CanvasNetMermaid_PacketDiagram_CanceledToken_Throws`,
`MermaidGitGraphIntegrationTests.CanvasNetMermaid_GitGraph_CanceledToken_Throws`

Asserts `OperationCanceledException` from `Measure`, `Render`, and `Render(width, height)` with a canceled token, and
from the render of a sequence diagram, a class diagram, a state diagram, an ER diagram, a pie chart, a gantt chart, a
mindmap, a packet diagram, and a git graph.

### CanvasNetMermaid-ImageSource: Diagram Bytes Through The Image-Source Contract

**Tests**: `MermaidSystemIntegrationTests.CanvasNetMermaid_ImageSource_DrawsDiagramBytes`

Renders the bytes of a flowchart through `MermaidImageSource` and asserts the natural size of the document, the
default theme's node fill, and no diagnostics.

### CanvasNetMermaid-ThirdPartyNotices: The Notices Ship With The Package

**Tests**: `MermaidSystemIntegrationTests.CanvasNetMermaid_ThirdPartyNotices_ShipWithPackage`

Reads the license texts and the provenance record that the package ships and asserts the copyright notice of each ported
project (sanitize-url's `Copyright (c) 2017 Braintree`, the Mermaid parser package's `Copyright (c) 2023 Yokozuna59`,
Langium's `Copyright 2021 TypeFox GmbH`, dayjs's `Copyright (c) 2018-present, iamkun`, the d3 notice's `Copyright
2010-2023 Mike Bostock`, layout-base's `Copyright (c) 2019 iVis@Bilkent` and cose-base's `Copyright (c) 2019 - present,
iVis@Bilkent.`, and cytoscape's `Copyright (c) 2016-2026, The Cytoscape Consortium.` and cytoscape-cose-bilkent's
`Copyright (c) 2016-2018, The Cytoscape Consortium.` included), the not-affiliated statement, and that the record names
every license file (`LICENSE-sanitize-url.txt`, `LICENSE-mermaid-parser.txt`, `LICENSE-langium.txt`,
`LICENSE-dayjs.txt`, `LICENSE-cose-base.txt`, and `LICENSE-cytoscape.txt` included).

### Platform Requirements

**Tests**: `CanvasNetMermaid_Flowchart_DefaultTheme_PaintsMermaidsColors` with the source filters `windows@`,
`ubuntu@`, `macos@`, `net8.0@`, `net9.0@`, and `net10.0@`

The representative test parses a flowchart with a subgraph, resolves the default theme, measures text with the
bundled fonts, lays it out with dagre, draws shapes, edges, and markers, and samples the pixels, so it exercises every
subsystem on each platform and framework.
