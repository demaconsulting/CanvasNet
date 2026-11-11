## Gantt Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Gantt` subsystem (internal namespace
`DemaConsulting.CanvasNet.Mermaid.Gantt`), containing four units: `GanttParser` (the jison lexer and grammar of
Mermaid's gantt language, the statement values, the click statements, the located errors, Mermaid's own parser
failures, and the node and label limits), `GanttModel` (Mermaid's gantt database, the diagram metadata, and the
compilation of the task dates of each render with the bundled dayjs functions and the clock of the render),
`GanttRenderer` (the drawing of Mermaid's gantt renderer), and `TimeAxis` (the JavaScript date arithmetic in UTC and
the d3 time intervals, ticks, scale, and format).

### Verification Approach

The `Gantt` subsystem is verified at two tiers. The unit tests (see the unit verification designs under `gantt/`)
verify each unit, including the 295 upstream vectors recorded from the pinned Mermaid release, which compare both the
parser and the model (the database facts and the compiled task dates) with Mermaid. The subsystem-level scenarios in
`GanttTests` verify the four units working together through the document: a whole text is opened as a
`MermaidDocument` (preprocessed, detected, and parsed into the model), and then asked for its metadata, its located
errors, or its laid-out scene, which compiles the task dates and draws them. The original fixtures in
`Fixtures/gantt/` (14 charts written for this project, one for each area of the language and the options: a basic
chart; a date format with durations and inclusive end dates; `after` and `until` references; the task states;
milestones and a vertical marker; excludes and includes; Friday weekends; an axis format with a weekly tick interval
and a start weekday; a top axis in the compact display mode; times of day; click links; a today marker style; the dark
theme with gantt settings and section titles with line breaks; and task texts outside their bars with a title wider
than the chart) are all opened and laid out with the clock of the reference renders, 2024-06-12T10:30:00Z. The
reference layout of the fixtures against the goldens drawn by Mermaid with the same clock, the strict renders, the
paint, the reports, and the cancellation of whole documents, and the located limits of whole documents under the
default limits are verified at the system level by the reference layout tests (`MermaidChartReferenceLayoutTests`),
the gantt integration tests (`MermaidGanttIntegrationTests`), and the hardening tests (`MermaidHardeningTests`).

The expectations are stated in the tests as values worked out from Mermaid's documented semantics and confirmed
against the pinned Mermaid release: the numbers of bars, texts, and rows of every fixture, the metadata and the drawn
texts of a known text, the location and code of an invalid text and of a date Mermaid cannot read, and the today line
and the warnings with and without a clock. The scenarios use no mocking or stubbing: the preprocessor, the detector,
the configuration resolver, the gantt style sheet, the text metrics, and the bundled fonts are real. The subsystem is
internal, so its tests reach the model and the scene through the test assembly's `InternalsVisibleTo` access (the
helper `SceneQuery`).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: The original charts in `test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/gantt/*.mmd`
- **Clock**: `MermaidRenderOptions.Now` set to 2024-06-12T10:30:00Z where a scenario draws with a clock
- **Fonts**: The bundled fonts for the scenarios that lay out
- **Isolation**: Each test opens its own document; the fixtures are only read

### Acceptance Criteria

The `Gantt` subsystem's verification passes when every subsystem-level scenario below and every unit-level scenario in
the four unit verification documents passes without error or unexpected exception.

### Test Scenarios

#### CanvasNetMermaid-Gantt-Language: Gantt Texts Open As Mermaid Reads Them

**Tests**: `GanttTests.CanvasNetMermaid_Gantt_Fixture_ParsesAndLaysOutConsistently`,
`GanttTests.CanvasNetMermaid_Gantt_Text_GivesTheModelAndTheScene`

Every original fixture (a theory with one case for each of the 14 files) opens as a document whose diagram is a
`GanttModel`, and the text with `title Plan`, `dateFormat YYYY-MM-DD`, the section `Build` with the tasks
`Code :c, 2024-03-04, 5d` and `Test :t, after c, 2d`, and the section `Ship` with the task
`Release :milestone, r, after t, 0d` opens as a document of the type `gantt`. The exact statements, sections, tasks,
and links of every kind of statement are verified at the unit level against Mermaid's database (see the GanttParser
and GanttModel unit verification designs and the upstream vectors).

#### CanvasNetMermaid-Gantt-Metadata: Metadata Without Rendering

**Tests**: `GanttTests.CanvasNetMermaid_Gantt_Text_GivesTheModelAndTheScene`

The text above gives, without being rendered, the diagram type `gantt`, a node count of 3 (its three tasks), a
subgraph count of 2 (its two sections), and the title `Plan` of its `title` statement. That vertical markers count as
nodes but not their sections, that the edge count is 0, and that there is no direction are verified at the unit level
(see the GanttModel unit verification design).

#### CanvasNetMermaid-Gantt-FailClosed: Invalid Text And Dates Are Refused At Their Place

**Tests**: `GanttTests.CanvasNetMermaid_Gantt_InvalidText_FailsClosedWithLocation`

A task name without its data (`Code` in the section `A`) after a frontmatter block is refused when the document is
opened with a `MermaidSyntaxException` of code `mermaid-syntax-error` in the named file `g.mmd`, located at line 6 of
the original text, frontmatter lines included, with exactly `taskData` expected and a hint; no document is returned.
A task with the date `2024-13-45` under `dateFormat YYYY-MM-DD` opens, and its render fails with a
`MermaidRenderException` of code `mermaid-render-failed` located at line 4, the task, with a hint. The other grammar
errors, Mermaid's own parser failures, the compilation failures, the limits, and the work charges are verified at the
unit level (see the GanttParser and GanttModel unit verification designs) and at the system level by the hardening
tests.

#### CanvasNetMermaid-Gantt-Dates: Task Dates Are Compiled As Mermaid Compiles Them

**Tests**: `GanttTests.CanvasNetMermaid_Gantt_Fixture_ParsesAndLaysOutConsistently`,
`GanttTests.CanvasNetMermaid_Gantt_Text_GivesTheModelAndTheScene`

Every fixture, among them those with durations, inclusive end dates, `after` and `until` references, excluded and
included days, Friday weekends, and times of day, compiles its task dates and lays out with the clock of the reference
renders, and the text above compiles `Test` after `Code` and `Release` after `Test` and draws their bars `c`, `t`, and
`r` in that order of their starts. The exact dates of durations, references, excluded days, inclusive end dates, the
date fallback, and the dayjs functions are verified at the unit level against the dates Mermaid compiles (see the
GanttModel unit verification design and the upstream vectors), and the positions of the bars against Mermaid's
goldens by the system-level reference layout tests.

#### CanvasNetMermaid-Gantt-Clock: Today Comes Only From The Render Options

**Tests**: `GanttTests.CanvasNetMermaid_Gantt_Clock_DrawsTheTodayLineOrReports`

The text with `todayMarker stroke-width:3px` at line 3, the task `Code :c, 2024-06-10, 5d`, and the task
`Late :l, after nobody, 1d` at line 6 draws exactly one today line with `Now` set to 2024-06-12T10:30:00Z, and no today
line without `Now`; without `Now` exactly two warnings are reported, in this order: `mermaid-gantt-today-unavailable`
at line 6 (the reference that names no task, when the dates are compiled) and `mermaid-gantt-today-unavailable` at
line 3 (the `todayMarker` style, when the line would be drawn); no unsupported feature is reported, and the chart is
still drawn with a view box of positive width and height. The dates that take today and the one warning of a
compilation are verified at the unit level (see the GanttModel unit verification design).

#### CanvasNetMermaid-Gantt-Drawing: Gantt Charts Are Drawn As Mermaid Draws Them

**Tests**: `GanttTests.CanvasNetMermaid_Gantt_Fixture_ParsesAndLaysOutConsistently`,
`GanttTests.CanvasNetMermaid_Gantt_Text_GivesTheModelAndTheScene`,
`GanttTests.CanvasNetMermaid_Gantt_SkippedTicksAndExcludes_AreReported`

Every fixture lays out into a scene with between one and as many task bars as its model has tasks (a bar without width
is not drawn, as in the browser), exactly one task text for each task, between one and as many section rows as it has
tasks that are not vertical markers, and at least one tick, and lays out a second time into the same view box and the
same number of items. The text above draws the task texts `Code`, `Test`, and `Release`, the section titles `Build` and
`Ship`, and the task bars with the ids `c`, `t`, and `r`. A chart with `excludes weekends`, `tickInterval 1second`,
and one task of seven years, drawn with the clock set, reports exactly one `mermaid-gantt-excludes-skipped` and one
`mermaid-gantt-ticks-skipped` warning, still draws ticks (the default ones), and shades no excluded day. The size,
rows, bars, milestones, vertical markers, task texts, compact mode, section titles, excluded days, axes, tick
intervals, today line, and title are verified at the unit level (see the GanttRenderer and TimeAxis unit verification designs),
the paint of the gantt style sheet by the Configuration subsystem, and the positions and sizes against Mermaid's goldens
by the system-level reference layout tests.
