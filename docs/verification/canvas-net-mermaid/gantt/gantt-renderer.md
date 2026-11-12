### GanttRenderer Unit Verification Design

This document describes the unit-level verification strategy for the `GanttRenderer` unit in the
`DemaConsulting.CanvasNet.Mermaid.Gantt` namespace: the internal sealed `GanttRenderer` (the drawing of Mermaid's gantt
renderer with the d3 axes it uses, in three partial files).

#### Verification Approach

The unit is verified by laying out short texts through the document, with the clock of the tests
(`MermaidRenderOptions.Now` at 2024-01-03T12:00Z) unless a test sets other options, and inspecting the scene: the bars,
texts, rows, excluded days, axes, today line, and titles by their roles, ids, and texts; their bounds, coordinates,
anchors, fonts, and colors; and the view box, compared with the values of Mermaid's `ganttRenderer.js` for the same
input with the default gantt configuration (a bar height of 20, a bar gap of 4, a top padding of 50, left and right
paddings of 75, a grid line start padding of 35, and a title top margin of 25). The parse of a custom tick interval is
verified by calling `ParseTickInterval` directly. The warnings are collected through the warning handler of the render
options, and the failures are caught as exceptions and inspected for their code, location, and message. The positions
and sizes of whole fixtures against the goldens drawn by Mermaid (`Fixtures/reference/gantt/*.layout.json`, recorded
by `tools/visual-diff/render-mermaid.mjs` with the browser's clock and time zone fixed) are verified at the system level
by the reference layout tests.

No mocking or stubbing is used; the parser, the model and its date compilation, the configuration resolver, the gantt
style sheet, the text metrics, and the bundled fonts are real (the helper `SceneQuery` opens and lays out a text). The
internal types are reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `GanttRendererTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Gantt` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled fonts for the scenarios that lay out
- **Clock**: A fixed `MermaidRenderOptions.Now`; the system clock is never read
- **Isolation**: Each test draws its own text; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any coordinate, size, text, anchor, font, color, count, id, report code, or exception code that differs from
the asserted Mermaid value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Gantt-GanttRenderer-Size: The Size Of The Chart

**Tests**: `GanttRendererTests.GanttRenderer_Draw_Size_IsPageWidthAndRows`,
`GanttRendererTests.GanttRenderer_Draw_Compact_SharesRows`

Asserts that a chart of two tasks and a vertical marker has the view box (0, 0, 784, 148), two top paddings of 50 and
two rows of 24 (the marker takes no row); that with a `useWidth` of 1000 and a `barHeight` of 30 a chart of one task has
the view box (0, 0, 1000, 134); and that in the compact mode three tasks that need two rows make the chart 148 high.

##### CanvasNetMermaid-Gantt-GanttRenderer-Rows: The Rows And The Background Rows

**Tests**: `GanttRendererTests.GanttRenderer_Draw_Bars_FollowTheTimeScale`,
`GanttRendererTests.GanttRenderer_Draw_VerticalMarker_SpansTheRows`

Asserts that the bars of two tasks, in the order of the text, are at y 50 and y 74, one row of 24 apart from the top
padding down, and that a chart of two tasks and a vertical marker has two background rows (`gantt-section`).

##### CanvasNetMermaid-Gantt-GanttRenderer-Bars: The Time Scale And The Bars

**Tests**: `GanttRendererTests.GanttRenderer_Draw_Bars_FollowTheTimeScale`

Asserts that of the tasks `A` (2024-01-01 for 2 days) and `B` (after `A` for 2 days), whose dates span 4 days mapped
onto the 634 pixels between the paddings, the bar of `A` has the bounds (75, 50, 317, 20) and the bar of `B`, whose
start is compiled from the end of `A`, the bounds (392, 74, 317, 20).

##### CanvasNetMermaid-Gantt-GanttRenderer-Paint: The Classes And The Paint Of The Tasks

**Tests**: `GanttRendererTests.GanttRenderer_Draw_TaskStates_PaintFromTheStyleSheet`,
`GanttRendererTests.GanttRenderer_Draw_NumberSectionStylesZero_PaintsNaNClassesAsMermaid`

Asserts, with the default theme, that a plain task is filled `#8a90dd` and stroked `#534fbc` at a width of 2, a `done`
task is filled `lightgrey` and stroked `grey`, and an `active` and `crit` task is filled `#bfc7ff` and stroked
`#ff8888`, and that the text of a task with a `click` link is bold and colored `#003163`. Asserts that with
`gantt.numberSectionStyles: 0` the chart renders without a warning or failure and its plain and `done` bars are both
filled `#333`: the section number is JavaScript's `i % 0`, NaN, so the classes (`taskNaN`, `doneNaN`, `sectionNaN`) match
no rule of the style sheet, as mermaid-cli draws them (the fixture `18-number-section-styles-zero` pins the whole
picture in its golden).

##### CanvasNetMermaid-Gantt-GanttRenderer-Milestones: The Milestone Diamond

**Tests**: `GanttRendererTests.GanttRenderer_Draw_Milestone_IsATurnedSquare`

Asserts that the milestone `m` on 2024-01-03 in a chart from 2024-01-01 to 2024-01-05 is centered at (392, 84), the
middle of its date plus the left padding and the middle of its row, that its bounds are wider than 16 (0.8 times the
bar height of 20) and narrower than 16 times √2, as a square turned 45 degrees, that its stroke width is 1.6 (2 scaled
by 0.8), and that its text is italic and anchored at its start.

##### CanvasNetMermaid-Gantt-GanttRenderer-VerticalMarkers: The Vertical Markers

**Tests**: `GanttRendererTests.GanttRenderer_Draw_VerticalMarker_SpansTheRows`

Asserts that the vertical marker `v` in a chart of two tasks starts at y 35, the grid line start padding, is 1.6 wide
(0.08 times the bar height of 20) and 88 high (two rows of 24 and two bar heights), that its text has the baseline 143
(35, the two rows, and 60) and the font size 15, and that the chart has two background rows only.

##### CanvasNetMermaid-Gantt-GanttRenderer-TaskTexts: The Side Of The Task Texts

**Tests**: `GanttRendererTests.GanttRenderer_Draw_TaskTexts_InsideRightOrLeft`,
`GanttRendererTests.GanttRenderer_Draw_Milestone_IsATurnedSquare`

Asserts that the text of a task of 20 days is anchored in the middle, inside its bar; that the long text of a task of
one day at the start of the chart is anchored at its start, right of the bar; that the long text of a task of one day at
the end of the chart is anchored at its end, left of the bar; and that the text of a milestone is anchored at its start,
right of the diamond.

##### CanvasNetMermaid-Gantt-GanttRenderer-Compact: The Compact Rows

**Tests**: `GanttRendererTests.GanttRenderer_Draw_Compact_SharesRows`

Asserts that in the compact mode, of the tasks `A` (2024-01-01 for 2 days), `B` (2024-01-02 for 2 days), and `C` (after
`A` for 1 day) of one section, `A` and `C` share the first row and `B` takes the second, the bars being at y 50, 74,
and 50, and that the chart is 148 high, two top paddings of 50 and two rows of 24.

##### CanvasNetMermaid-Gantt-GanttRenderer-SectionTitles: The Section Titles

**Tests**: `GanttRendererTests.GanttRenderer_Draw_SectionTitles_KeyOrderAndBreaks`

Asserts that of the sections `b<br>c` and `5`, written in that order, the section titles are drawn as the lines `5`,
`b`, and `c`: the integer name first, as a JavaScript object orders its keys, and the other title split at its `<br>`
tag.

##### CanvasNetMermaid-Gantt-GanttRenderer-Excludes: The Excluded Days

**Tests**: `GanttRendererTests.GanttRenderer_Draw_ExcludedDays_ShadedOrSkipped`

Asserts that with `excludes weekends` a task of 10 days from 2024-01-01 has the shaded runs `exclude-2024-01-06` and
`exclude-2024-01-13`, the two weekends that its days span, and that a task of 7 years has no shaded run and gives a
`mermaid-gantt-excludes-skipped` warning.

##### CanvasNetMermaid-Gantt-GanttRenderer-Today: The Today Line

**Tests**: `GanttRendererTests.GanttRenderer_Draw_TodayLine_FollowsTheClock`

Asserts that with the clock at 2024-01-03T12:00Z and the style `stroke:#00f,stroke-width:4px` the today line of a task
of 4 days from 2024-01-01 is at x 471 (396 plus the left padding of 75) from y 25 to y 99 (the height of 124 less the
title top margin of 25), stroked `#00f` at a width of 4; that `todayMarker off` draws no today line; and that without a
clock no today line is drawn and the `todayMarker` style gives a `mermaid-gantt-today-unavailable` warning.

##### CanvasNetMermaid-Gantt-GanttRenderer-Axes: The Axes, Their Grid Lines, And Labels

**Tests**: `GanttRendererTests.GanttRenderer_Draw_Axes_TicksAndLabels`

Asserts that with `topAxis` set, `axisFormat %d`, and `tickInterval 2day`, a task of 8 days from 2024-01-01 has the
axes `bottom` and `top`, the bottom one at (75, 74), 50 above the height of 124; that its labels are `01`, `03`, `05`,
`07`, and `09`; and that its first grid line runs at x 0.5 from y 0 to y -39, up to the grid line start padding.

##### CanvasNetMermaid-Gantt-GanttRenderer-TickInterval: The Tick Interval

**Tests**: `GanttRendererTests.GanttRenderer_ParseTickInterval_AcceptsMermaidsForm`,
`GanttRendererTests.GanttRenderer_Draw_Axes_TicksAndLabels`,
`GanttRendererTests.GanttRenderer_Draw_TickCapBoundary_EstimatesAsMermaid`

Asserts that `3week` parses as the step 3 of weeks of 604,800,000 milliseconds and `1month` as the step 1 of months of
2,628,000,000 milliseconds (365/12 days), and that `0day`, `1 day`, `1year`, and no text are no interval; that the
interval `2day` places the ticks two days apart; that `tickInterval 1millisecond` over 8 days gives a
`mermaid-gantt-ticks-skipped` warning and still draws the default ticks; and, at the cap, that `tickInterval 1day` over
exactly 10,000 days (Mermaid's estimate 10,000) draws 10,001 ticks without a warning, both ends included, as
mermaid-cli does, while over 10,001 days it warns and draws d3's 14 default ticks.

##### CanvasNetMermaid-Gantt-GanttRenderer-Title: The Title

**Tests**: `GanttRendererTests.GanttRenderer_Draw_Title_FromTheTextOrTheFrontmatter`,
`GanttRendererTests.GanttRenderer_Draw_TitleSanitizedToEmpty_ReplacesTheFrontmatterTitle`

Asserts that the title of `title Own title` is one text `Own title` anchored in the middle at x 392 (half the width of
784) and the baseline 25, that a chart without a `title` statement draws the frontmatter title `Front title`, and that
a `title` statement that sanitizes to empty (`<script>x</script>`) replaces the frontmatter title, so no title is
drawn, as in Mermaid.

##### CanvasNetMermaid-Gantt-GanttRenderer-Failures: The Located Failures

**Tests**: `GanttRendererTests.GanttRenderer_Draw_Failures_AreLocated`

Asserts that in the compact mode a section named `toString` fails with a `MermaidRenderException` of code
`mermaid-render-failed` located at line 6, its task, and that `axisFormat %c%c%c%c` with a `MaxLabelLength` of 40 fails
with a `MermaidRenderException` of code `mermaid-limit-exceeded` whose message names the tick label.
