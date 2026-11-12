### GanttRenderer

![MermaidGantt Structure](MermaidGanttView.svg)

The `GanttRenderer` unit is the third software unit in the `Gantt` subsystem. It comprises three source files in the
`DemaConsulting.CanvasNet.Mermaid.Gantt` namespace: `GanttRenderer.cs` (the size of the chart, the rows and the compact
rows, the time scale, the order of the tasks, the title, and the texts as the browser measures and shows them),
`GanttRenderer.Tasks.cs` (the excluded days, the background rows, the bars and their classes, the milestones, the
vertical markers, the task texts, the section titles, and the today line), and `GanttRenderer.Axis.cs` (Mermaid's
`makeGrid` with the custom tick interval and its guard, and the bottom and top axes of d3-axis). `GanttRenderer` is an
`internal sealed partial` class, so the three files are one type. Each file carries the provenance header comment for
the parts of Mermaid and d3 it ports.

#### Purpose

`GanttRenderer` draws one render of a `GanttModel` into a scene as the `draw` of Mermaid's `ganttRenderer.js` draws the
chart, with the d3 axes it uses and the paint of the gantt style sheet of the render's theme. It compiles the task dates
of the render with the clock of the render options, so that the dates and the today line follow the `Now` that the host
gives and never the system clock. It exists so that the geometry, the texts, the colors, and the size of a gantt chart
are Mermaid's for the same text, configuration, theme, and clock, and so that the parts of the drawing can be compared
with Mermaid's one by one.

The unit's behavior is specified by the `CanvasNetMermaid-Gantt-GanttRenderer-*` requirements in
`docs/reqstream/canvas-net-mermaid/gantt/gantt-renderer.yaml`.

#### Data Model

**The constants**: the width of mermaid-cli's page container (`PageWidth`, 784: the 800-pixel page within its body
margins of 8 pixels, which is the `offsetWidth` that Mermaid reads), the most ticks a custom tick interval may make
(Mermaid's `MAX_TICK_COUNT`, 10,000), d3-axis's offset of the ticks at a device pixel ratio of 1 (0.5) and its padding
between a tick and its label (3), the units of a custom tick interval with their lengths in dayjs's `duration` (a month
being 365/12 days), the CSS transform of a milestone in Mermaid's style sheet (`rotate(45deg) scale(0.8,0.8)`), and the
names that every JavaScript object inherits (`__proto__`, `constructor`, `toString`, `valueOf`, and the others), which
a section name hits in the compact mode.

**A renderer instance** draws one render on one thread and holds the model, the `DiagramRenderContext` of the render,
the `GanttConfig` section of its configuration (the paddings, the bar height and gap, the font sizes, the number of
section styles, the axis format, the tick interval, `topAxis`, `displayMode`, and `useWidth`), the `GanttStyles` built
from its configuration (reporting the problems of `themeCSS` to its sink), a `LayoutWorkMeter` of `MaxLayoutWork`
located at the diagram keyword and bound to the cancellation token of the render, a `LabelBuilder` over the fonts of the
render that charges that meter, and the `GanttClock` of the render, made from `MermaidRenderOptions.Now`. While it draws
it keeps the compiled tasks (`GanttTask`), the sections of the tasks that are not vertical markers, each once
(Mermaid's `categories`), the rows of each section in the order of a JavaScript object's keys (Mermaid's
`categoryHeights`, an `ObjectKeyMap`), the time scale (`TimeScale`), the width and the height of the chart, and the
items of the scene in paint order.

**The scene** has the view box (0, 0, width, height) and holds, in paint order, the shaded excluded days
(`gantt-exclude`, with the id `exclude-YYYY-MM-DD`), the axis groups (`gantt-axis`, with the id `bottom` or `top`)
with their grid lines (`gantt-tick`) and labels (`gantt-tick-label`), the background rows (`gantt-section`, with the
section as the id), the bars (`gantt-task`, with the task id), the task texts (`gantt-task-text`, with the task id and
`-text`), the section titles (`gantt-section-title`), the today line (`gantt-today`), and the title (`title`). The
items carry the location of their task, of the `todayMarker` statement, of the title, or of the diagram keyword.

#### Key Methods

- **`Draw()`** follows Mermaid's `draw`:
  - It compiles the task dates of the render with the clock and the meter (`GanttModel.Compile`, see
    _GanttModel Unit Design_), takes the width as `gantt.useWidth` when it is set and else 784, and keeps the sections
    of the tasks that are not vertical markers, each once in order (`checkUnique`, whose object never holds the key
    `__proto__`, so that name is kept each time).
  - The height is two top paddings plus a row of the bar height and the bar gap for each task that is not a vertical
    marker, with the number of tasks of each section as its rows; in the compact mode `CompactRows` gives the rows.
  - The time scale maps the earliest start to the latest end (`TimeAxis.Min` and `TimeAxis.Max`, which skip invalid
    dates) onto 0 to the width less the left and right paddings, rounded to whole pixels (`rangeRound` without `nice`).
  - It sorts the tasks stably by their start, charging the meter, and draws, in Mermaid's order, the excluded days, the
    axes, the rows with the bars and the task texts, the section titles, the today line, and the title.
- **`CompactRows`** groups the tasks that are not vertical markers by section in the order of a JavaScript object's keys
  (integer-like names first) and gives each section, one after the other, the rows of `MaxIntersections` (Mermaid's
  `getMaxIntersections`): the tasks in order of their start and then their row, each in the first row whose last task
  ends at or before its start; the chart grows by the rows of each section.
- **`DrawExcludeDays`** (Mermaid's `drawExcludeDays`) runs only when the chart has `excludes` or `includes`. When the
  tasks span more than five years (dayjs's `diff` in years, `Dayjs.DiffYears`) it reports
  `mermaid-gantt-excludes-skipped` at the diagram keyword and shades nothing, as Mermaid only warns. Otherwise it walks
  the days from the earliest start to the latest end, charging the meter the day cost of the model for each, asks the
  model whether the day is excluded (`IsInvalidDate`, which charges the meter for formatting the day), and draws a
  rectangle for each run of excluded days that a day that is not excluded ends (a run still open at the end is not
  drawn, as in Mermaid): from the start of its first day to the end of its last day, from the grid line start padding
  to the top padding above the bottom, with the paint of `.exclude-range`.
- **`DrawRects`** (Mermaid's `drawRects`) puts the vertical markers after the other tasks and draws, for each row, a
  background row with the first task of the row at x 0, the row's y less 2, the width less half the right padding, and
  one row high, with the classes `section section<n>`, where `n` is the index of the section among the sections modulo
  `numberSectionStyles`; then all bars (`DrawBar`); then all task texts (`DrawTaskText`).
- **`DrawBar`** draws a rectangle with corners of radius 3 (at most half its size), on its row at
  `order * (barHeight + barGap) + topPadding`, from the scaled start plus the left padding to the scaled drawn end
  (the render end that the model sets when excluded days move the end, else the end). A milestone is a square of the
  bar height centered between its start and end, turned 45 degrees and scaled by 0.8 around its center, with its
  stroke width scaled, when its computed transform is Mermaid's. A vertical marker is 0.08 bar heights wide at its
  start, from the grid line start padding over all rows and two bar heights more. The class is built as Mermaid builds
  it (`BarClass`): `task`, the state (`active`, `activeCrit`, `done`, `doneCrit`, `crit`, or `task`), `milestone` and
  `vert` before it, the section's style number (the last index of the section among the sections, modulo
  `numberSectionStyles`, as Mermaid's loops without a break find it), and the task's own classes, which include
  `clickable` for a task with a link or a callback.
- **`DrawTaskText`** measures the text as the browser's `getBBox` does before its class is set (`MeasureBeforeClass`:
  the root's font family at `fontSize`, Mermaid's entity placeholders not yet decoded). The text is at the middle of the
  bar when it fits, else 5 pixels right of the bar, or 5 pixels left of its start when the end of the bar, the text, and
  one and a half left paddings would pass the width; a milestone's text is placed from its square, and a vertical
  marker's text is at its start, 60 pixels below the rows. The y is the row's top plus half the bar height plus half the
  font size less 2. The class (`TextClass`) is `taskText`, `taskTextOutsideRight`, or `taskTextOutsideLeft` with the
  section's number and the state classes, chosen from the end and the unshifted start of a milestone as Mermaid chooses
  it, so it may differ from the side the x was chosen for; the anchor and the font follow from the paint of that class.
- **`VertLabels`** (Mermaid's `vertLabels`) draws a title for each section in the order of the rows' keys, at x 10 and
  the middle of the section's rows after the rows of the sections before it (Mermaid's `prevGap`), its lines split at
  `<br>` tags (`LabelBuilder.SplitBreaks`) and centered on the central baseline, with the classes `sectionTitle` and
  `sectionTitle<n>` at the `sectionFontSize`.
- **`DrawToday`** (Mermaid's `drawToday`) draws nothing for `todayMarker off`. Without a clock it draws nothing and
  reports a `todayMarker` style as `mermaid-gantt-today-unavailable` at its statement. Otherwise it draws a line at the
  scaled `Now` plus the left padding from the title top margin to the height less the title top margin, with the paint
  of the `.today` rule under the chart's `todayMarker` style, its commas made semicolons, as an inline style.
- **`MakeGrid`** (Mermaid's `makeGrid`) takes the axis format of the chart, else `%d` when the date format is `D`, else
  the configured axis format or `%Y-%m-%d`, and the tick interval of the chart, else the configured one. It draws the
  bottom axis at the left padding and the height less 50 and, with `gantt.topAxis`, the top axis at the left padding
  and the top padding, with an inner and outer tick size of the height less the top padding and the grid line start
  padding, upward.
- **`Ticks`** gives the ticks of an axis. A custom interval (`ParseTickInterval`) whose estimated count, the length of
  the domain over the interval rounded up, is at most 10,000 gives the ticks of `TimeAxis` at that interval, the weeks
  starting on the chart's `weekday`; a larger one gives d3's ten default ticks and, for the bottom axis only, a
  `mermaid-gantt-ticks-skipped` report, as Mermaid warns once. An interval that `every` cannot make, and no interval,
  give the ten default ticks. The meter is charged for each step of the tick generation.
- **`ParseTickInterval(text)`** parses Mermaid's form `^([1-9]\d*)(millisecond|second|minute|hour|day|week|month)$`
  into the step, the unit, and the unit's length in milliseconds, or gives null.
- **`DrawAxis`** is d3-axis's `axisBottom` or `axisTop`: a group at the axis position holding the domain path, which is
  drawn only when the style sheet gives it a stroke width (Mermaid's sheet gives it none), and for each tick a grid
  line from the axis to the tick size at the scaled tick plus 0.5, and a label formatted by `TimeAxis.Format`, checked
  against `MaxLabelLength`, 3 pixels past the axis, the bottom labels one em lower, with the paint of the `.grid`
  rules of the style sheet over the presentation attributes that d3-axis writes.
- **`DrawTitle`** draws the title of the model, or the sanitized frontmatter title when no title statement set the
  title (`IsTitleSet`), at half the width and the title top margin, with the paint of `.titleText`.
- **`TextLine`** makes one text: it shows it as `DisplayText` gives it, at an x and a baseline that are 0 when they are
  not numbers, with the box of the browser's `getBBox` from the `LabelBuilder`, anchored as its computed `text-anchor`
  says, and charges the meter for its length; an empty text or a font size that cannot be drawn gives no text.
- **`DisplayText`** gives the text the browser shows: each of Mermaid's entity placeholders decoded into its character,
  everything else shown as written, and the white space collapsed as in all SVG text.
- **`Rect`** makes a rectangle with rounded corners and its computed paint (the opacity folded into the colors), and
  none for a size that is not positive or a position that is not a number, as SVG does not render it.

#### Error Handling

- The failures of the task dates propagate from `GanttModel.Compile` as located `MermaidRenderException`s (see
  _GanttModel Unit Design_).
- In the compact mode, a section named like a name that every JavaScript object inherits fails as Mermaid's render
  fails on it (`categoryElements[section].push is not a function`): a `MermaidRenderException` with code
  `mermaid-render-failed`, located at the task, with a hint.
- When the excluded days are checked against a `dateFormat` with a token that Mermaid's dayjs cannot format (`w`, `ww`,
  `wo`, `gggg`, `z`, `zzz`), the model's `NotSupportedException` becomes a `MermaidRenderException` with code
  `mermaid-render-failed`, located at the diagram keyword, with a hint.
- A tick label longer than `MaxLabelLength` throws a `MermaidRenderException` with code `mermaid-limit-exceeded`
  (`a tick label is N characters long, more than the limit of M (MermaidLimits.MaxLabelLength)`), located at the
  diagram keyword, before it is measured.
- The layout work meter of the render throws a `MermaidRenderException` with code `mermaid-limit-exceeded` when the
  work of the dates, the days of the excluded days, the tick steps and labels, the compact rows, the sorts, and the
  measured texts exceeds `MaxLayoutWork`, and an `OperationCanceledException` when the render is canceled; both
  propagate.
- `GanttStyles.Create` throws `MermaidUnsupportedFeatureException` through the sink at the first unsupported `themeCSS`
  rule in strict mode, and the `todayMarker` style is read as a restricted inline style that reports its problems
  through the sink; both propagate.
- The warnings `mermaid-gantt-excludes-skipped`, `mermaid-gantt-ticks-skipped`, and `mermaid-gantt-today-unavailable`
  are reported through the sink of the render.
- No other error can occur: dates that are not numbers, an empty chart, and sizes that are not positive are drawn as
  the browser draws Mermaid's picture (a coordinate that is not a number taken as 0, a rectangle of no size not drawn).

#### Dependencies

- **GanttModel** - `GanttModel` (`Compile`, the date and axis formats, the tick interval, the weekday, the excludes and
  includes with `DayCost` and `IsInvalidDate`, the `todayMarker` and its location, the title and its location, the
  keyword location, and `Sanitize`), `GanttTask` and `GanttRawTask` (the compiled dates, the row, the section, the tags,
  and the classes), `GanttClock` (today and whether it is known), and `Dayjs` (`DiffYears`, `Add`, `StartOfDay`,
  `EndOfDay`, and `Format`).
- **TimeAxis** - `JsDate`, `TimeScale` (the map and the ticks), `TimeInterval` (`Every`), the intervals, `Min`, `Max`,
  and `Format`.
- **Configuration subsystem** - `MermaidConfig` (the `Gantt` section `GanttConfig`), `GanttStyles`, `GanttElement`, and
  `GanttComputedStyle` (the gantt style sheet and its cascade), and `StyleDeclarations`, `StyleDeclarationSet`, and
  `StyleDeclaration` (the inline styles and the drawable font sizes).
- **Rendering subsystem** - `LabelBuilder` (the browser's text extents and vertical metrics, `SplitBreaks`,
  `CollapseSvgWhiteSpace`, and `DecodeEntities`).
- **Syntax subsystem** - `HtmlSanitizer.DecodeReferences` (the character references of a placeholder) and
  `YamlSubsetParser.FormatJsNumber` (JavaScript's `String(number)`).
- **Graphlib subsystem** - `ObjectKeyMap`, the order of a JavaScript object's keys.
- **Dagre subsystem** - `LayoutWorkMeter`, the work budget and cancellation of a render.
- **Scenegraph subsystem** - `Scene`, `SceneItem`, `SceneGroup`, `ScenePath`, `SceneSegment`, `SceneText`,
  `SceneTextRun`, `SceneFont`, `SceneRect`, and `SceneStroke`, the drawing.
- **Core `CanvasNet`** - `Rgba32` (the `Canvas` subsystem), through the public API.
- **System** - `DiagramRenderContext`, `MermaidRenderOptions` (`Now` and the limits), `MermaidLimits`,
  `MermaidLocation`, `MermaidRenderException`, and `MermaidDiagnosticCodes`.

#### Callers

- **GanttModel** - `GanttModel.Layout` creates a renderer for each render and returns the scene of `Draw`.
- The unit tests call `ParseTickInterval` directly, and the unit and subsystem tests lay out whole texts.

#### Known Differences From Mermaid

- **The width.** Mermaid takes the width of the chart from its container in the page; the port takes the 784 pixels of
  mermaid-cli's default page, or `gantt.useWidth`, which takes precedence as in Mermaid.
- **The clock and the time zone.** Today is the `Now` of the render options, never the system clock; without it the
  today line is not drawn and a `todayMarker` style is reported. The dates are those of a browser whose time zone is UTC
  (see _TimeAxis Unit Design_).
- **The axis offset.** The ticks are half a pixel past their whole pixels at every render scale, as d3-axis places them
  at a device pixel ratio of 1.
- **Reports in place of console warnings.** The excluded days skipped for a span of more than five years and a tick
  interval whose span is more than 10,000 intervals (Mermaid's `getEstimatedTickCount`, the span over the interval
  rounded up, so exactly 10,000 intervals draw 10,001 ticks) are reported as `mermaid-gantt-excludes-skipped` and
  `mermaid-gantt-ticks-skipped`, where Mermaid writes a warning to the console.
- **Limits.** The work of the drawing is bounded by `MaxLayoutWork` and each tick label by `MaxLabelLength`; Mermaid has
  no such limits.
- **Render failures are located.** A section named like an inherited member in the compact mode fails as a located
  `MermaidRenderException` with code `mermaid-render-failed` and a hint, in place of Mermaid's JavaScript error.
- **Paint.** The element opacities (`.section`, `.grid .tick`) are folded into the colors, so overlapping parts of one
  element are not composited as a group; `shape-rendering: crispEdges` is not modeled; the milestone transform is
  applied to the geometry and the stroke width; and a rectangle without area is not drawn, as in the browser.
- **Links.** A task with a link or a callback gets the `clickable` class and its paint, but no link is drawn and no
  callback is bound.
- **Text metrics.** The widths and boxes of the texts are those of the `LabelBuilder`, which emulates the browser's SVG
  text measurement with the fonts of the render; the reference layout tests hold them to Mermaid's goldens.
