## Gantt

![MermaidGantt Structure](MermaidGanttView.svg)

### Overview

The `Gantt` subsystem (namespace `DemaConsulting.CanvasNet.Mermaid.Gantt`, folder
`src/DemaConsulting.CanvasNet.Mermaid/Gantt/`) reads the text of a gantt chart (the `gantt` keyword, Mermaid's diagram
id `gantt`) into the state of Mermaid's gantt database, compiles the dates of its tasks for each render as that
database compiles them, and draws the chart as Mermaid's gantt renderer draws it. Like a pie chart, and unlike the
flowchart, class, state, and entity relationship diagrams, a gantt chart is not a graph: it is not laid out by the
shared layered layout or drawn by the shared unified renderer, but placed on a time scale by the geometry of Mermaid's
gantt renderer, so this subsystem draws its scene itself, with the text metrics of the shared `Rendering` subsystem and
the gantt style sheet of the `Configuration` subsystem. It is **internal**: its types are visible only to the other
subsystems of the system and, through `InternalsVisibleTo`, to the test assembly. It contains four units:

- **GanttParser** - the internal sealed `GanttParser` (a partial class in `GanttParser.cs` and `GanttParser.Lexer.cs`)
  and the static token kinds `GanttTokens` (in `GanttParser.Lexer.cs`): the jison lexer and the grammar of Mermaid's
  gantt language, the values the grammar's actions pass to the database, the click statements, the located errors with
  jison's expected tokens, the failures of Mermaid's own parser, and the node and label limits; see _GanttParser Unit
  Design_ (`gantt/gantt-parser.md`)
- **GanttModel** - the internal sealed `GanttModel` (a partial class in `GanttModel.cs`, `GanttModel.Tasks.cs`, and
  `GanttModel.Dayjs.cs`) with the raw task `GanttRawTask` and the start kind `GanttStartKind` (in `GanttModel.cs`), the
  compiled task `GanttTask` and the clock `GanttClock` (in `GanttModel.Tasks.cs`), and the static `Dayjs` (in
  `GanttModel.Dayjs.cs`): Mermaid's gantt database, the metadata of the diagram, and the compilation of the task dates
  of each render with the dayjs functions Mermaid bundles and the clock of the render options; see _GanttModel Unit
  Design_ (`gantt/gantt-model.md`)
- **GanttRenderer** - the internal sealed `GanttRenderer` (a partial class in `GanttRenderer.cs`,
  `GanttRenderer.Tasks.cs`, and `GanttRenderer.Axis.cs`): the drawing of Mermaid's gantt renderer (the size of the
  chart, the section rows, the bars, milestones, and vertical markers, the task texts, the section titles, the excluded
  days, the axes, the today line, and the title) with d3-axis; see _GanttRenderer Unit Design_
  (`gantt/gantt-renderer.md`)
- **TimeAxis** - the JavaScript date `JsDate`, the d3-time interval `TimeInterval`, the static `TimeAxis`, and the time
  scale `TimeScale` (in `TimeAxis.cs`, `TimeAxis.Scale.cs`, and `TimeAxis.Format.cs`): the JavaScript date arithmetic
  in UTC and the d3 time intervals, tick generator, time scale, and time format that the chart is built on; see
  _TimeAxis Unit Design_ (`gantt/time-axis.md`)

The subsystem's namespace documentation is in `Gantt/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetMermaid-Gantt-*` requirements in `docs/reqstream/canvas-net-mermaid/gantt.yaml`. The SysML2 model names the
subsystem `MermaidGantt` (see _CanvasNetMermaid System Design_).

**Attribution.** The subsystem is a port of the gantt chart of the pinned Mermaid release (MIT; see
`third-party/README.md`): the parser ports the jison grammar `diagrams/gantt/parser/gantt.jison` (its lexer rules and
start conditions, and the actions of its grammar); the model ports Mermaid's gantt database
(`diagrams/gantt/ganttDb.js`, with the title and accessibility setters of `commonDb.ts`) and the parts of the dayjs
that Mermaid bundles that the database uses (MIT; the core with its English locale and the `customParseFormat`,
`advancedFormat`, and `isoWeek` plugins); the renderer ports the `draw` of `diagrams/gantt/ganttRenderer.js` with
d3-axis; and the time axis ports, from d3, the local time intervals and tick generator of d3-time, `tickStep` of
d3-array, `scaleTime` of d3-scale with `interpolateRound` of d3-interpolate, and `timeFormat` of d3-time-format
(ISC). JavaScript's `Date` and `Number` are reproduced from the ECMAScript specification, not ported. The gantt style
sheet of `diagrams/gantt/styles.js` and the gantt defaults of Mermaid's configuration schema are ported by the
Configuration subsystem. Every ported file carries the provenance header comment.

### Interfaces

**Exposed** (internal only): `GanttParser.Parse(PreprocessedSource, DetectedDiagram, MermaidLimits)`, which returns
the filled `GanttModel` or throws a located `MermaidSyntaxException`; and the `GanttModel` as the `IMermaidDiagram` of
the document: the metadata (`Title`, the trimmed title of the `title` statement or null; `AccTitle`; `Direction`,
always null; `NodeCount` for the tasks, vertical markers included; `EdgeCount`, always 0; and `SubgraphCount` for the
drawn sections) and `Layout(DiagramRenderContext)`, which creates a `GanttRenderer` and returns the drawn `Scene`. For
tests and for the vectors the model also exposes the state of the database (`DateFormat`, `AxisFormat`,
`TickInterval`, `TodayMarker`, `Excludes`, `Includes`, `Weekday`, `Weekend`, `InclusiveEndDates`, `DiagramTitle`,
`AccessibleTitle`, `AccessibleDescription`, `Sections`, `Tasks`, and `Links`), `FindTask`, and
`Compile(GanttClock, LayoutWorkMeter)`, which gives the tasks of one render with their dates; `Dayjs` exposes its
dayjs and JavaScript functions; and the renderer and the time axis expose the functions their unit designs list.

**Consumed**: from the `Syntax` subsystem the preprocessed text and its map to the original text (`PreprocessedSource`,
`MappedText`), the detected diagram (`DetectedDiagram`), the rule-driven lexer with start conditions and its token
cursor (`SourceScanner`, `ScanRule`, `ScanMatcher`, `ScanToken`, and `TokenCursor`), the JavaScript character classes
of `ScanPatterns`, JavaScript's `trim` (`Preprocessor.TrimJs`), the text sanitizer (`HtmlSanitizer`), the URL
sanitizer of the `strict` security level (`UrlSanitizer`), and JavaScript's `String(number)`
(`YamlSubsetParser.FormatJsNumber`); from `Configuration` the effective `MermaidConfig` with its `GanttConfig` section
and the gantt style sheet `GanttStyles` with `StyleDeclarations`; from `Rendering` the text metrics and text utilities
of `LabelBuilder`; from `Dagre` the `LayoutWorkMeter`; from `Graphlib` the key order of a JavaScript object
(`ObjectKeyMap`), which the renderer's section maps keep; from `Scenegraph` the `Scene` and its items; from the core
`CanvasNet` the color `Rgba32`; and from the system the `IMermaidDiagram` contract, `DiagramRenderContext`,
`MermaidReportSink`, the public `MermaidRenderOptions` (its `Now`), `MermaidLimits`, `MermaidLocation`,
`MermaidSyntaxException`, `MermaidRenderException`, and `MermaidDiagnosticCodes`.

**Consumers**: the document pipeline of the system (`MermaidDocument.Pipeline.cs`): its `DiagramRegistry` calls the
parser for a detected `gantt` diagram with the limits of the open call, keeps the model as the document's diagram,
reads its metadata, and calls `Layout` for every measure and render.

### Design

**Parse once, compile and draw per render, as in Mermaid.** Mermaid's jison parser fills its gantt database when the
text is parsed, and its renderer calls `getTasks`, which compiles the task dates, when it draws, with the clock of
that moment. The subsystem keeps that split: `GanttParser` reads the whole text into a `GanttModel` when the document is
opened, and every render creates a new `GanttRenderer`, which has the model compile the task dates of that render
(`Compile`, with the render's `GanttClock` and work meter) into tasks of its own, and draws them under the
configuration, the theme, the fonts, and the clock of that render. The model is never changed after parsing, so the
vectors compare it with Mermaid's database fact by fact and the compiled tasks with Mermaid's task by task.

**The language of the jison grammar.** The lexer is the jison lexer of `gantt.jison` rule for rule: 53 rules with
their start conditions, case-insensitive, tried in order with the first that matches winning, each with a hand-written
matcher that matches what its regular expression matches, quirks included (the value of a setting runs to the end of
the line or to `;`, a keyword's one white-space character may be a line feed, any character but `}` followed by `%`
starts a comment, and a line that starts with `click`, `call`, or `href "` is a part of a click statement). The grammar
is read by a recursive-descent parser with one token of lookahead that accepts the language of the LALR(1) grammar and,
after each token, checks the lookahead against the actions of the LALR state jison is in, so that an error comes where
jison reports it, with jison's expected tokens. Each statement passes its value to the database with the `substr`
offsets of its action.

**Mermaid's database, JavaScript semantics included.** The model keeps the statements, the sections, and the tasks as
`ganttDb.js` keeps them: the leading tags taken off a task's data, its fields read as an end, a start and an end, or an
id, a start, and an end, generated ids `task1`, `task2`, and so on, and the rows of the tasks. Mermaid finds tasks in a
plain JavaScript object, so a repeated id finds the last task, `__proto__` is never found, and a task without an id is
found under `undefined`; the model reproduces that lookup. A click statement makes its tasks clickable, and an `href`
is sanitized and kept as data: links are never drawn as links and callbacks are never bound.

**The dates of Mermaid's database, in UTC.** The compilation follows `getTasks` step by step: up to eleven passes over
the tasks, so that references to later tasks resolve; `getStartDate` (a timestamp, the latest end of the `after`
tasks, a date the date format reads strictly, or a date text of the ECMAScript format) and `getEndDate` (the earliest
start of the `until` tasks, a date with a day added for inclusive end dates, or a duration added as dayjs adds it); and
`checkTaskDates` with `fixTaskDates`, which move an end past the excluded days that are not included. The dayjs
functions are those of the dayjs that Mermaid bundles, with the plugins Mermaid loads. All dates are JavaScript dates
whose local time zone is UTC (`JsDate`, from the TimeAxis unit), the equivalent of Mermaid in a browser whose time zone
is UTC, so a chart is drawn the same on every machine.

**One clock, from the render options.** The library never reads the system clock. Today is the time of
`MermaidRenderOptions.Now`, in UTC: the today line is drawn there, an `after` or `until` reference that names no task
resolves to its midnight, and a date format without a day takes its date, as in Mermaid with the browser's clock. When
`Now` is not set, no today line is drawn, 1970-01-01 is taken as today, and what needs today is reported as
`mermaid-gantt-today-unavailable`: once by the compilation, at the first task that needs today, and by the renderer for
a `todayMarker` style.

**Drawing Mermaid's geometry.** `GanttRenderer.Draw` follows Mermaid's `draw`: a chart of the page's width with a row
for each task that is not a vertical marker (or the rows of the compact mode); the time scale from the earliest start
to the latest end, rounded to whole pixels; the excluded days, the axes with their grid and tick labels, the section
rows, the bars with their texts, the section titles, the today line, and the title, in Mermaid's order, with the paint
of the gantt style sheet (`GanttStyles`). Mermaid leaves out the ticks of a `tickInterval` whose span is more than
10,000 intervals and the shading of the excluded days of a chart that spans more than five years, writing a warning to
its console; the renderer leaves them out too and reports them as `mermaid-gantt-ticks-skipped` and
`mermaid-gantt-excludes-skipped`. See _GanttRenderer Unit Design_ and _TimeAxis Unit Design_.

**Fidelity is proven against Mermaid itself.** The upstream vectors (`gantt-parser.json`, 295 cases recorded from the
pinned Mermaid release in headless Chrome with the UTC time zone and the clock fixed at 2024-06-12T10:30:00Z) give,
for each valid text, the facts of Mermaid's gantt database and the dates of every compiled task, and for each invalid
text the error Mermaid reports, with its unexpected token and, for a grammar error, the tokens jison expects; the unit
tests compare the parser and the model with all of them. The 14 original fixtures in `Fixtures/gantt/` cover the
language and the options as authors use them; the subsystem tests open and draw all of them, and the system-level
reference layout tests compare their drawings with the goldens drawn by Mermaid with the same clock.

**Fail closed, located.** What can be wrong with the text alone is found when the document is opened, as a located
`MermaidSyntaxException`: a grammar error (with jison's expected tokens), the `topAxis` statement and a task whose data
holds only tags (on which Mermaid's own parser fails), and the limits of the open call (`MaxNodes` for the task and
section statements together and `MaxLabelLength` for every kept and derived text). What Mermaid finds only when it
compiles the dates is found when a render compiles them, as a located `MermaidRenderException` with code
`mermaid-render-failed` and a hint at the task: an invalid date, the first task without a start, a task with more than
three data fields, excluded days that never end, and the tasks that Mermaid would draw without dates (a circular
reference). A render also fails on its own limits (the layout work limit and the label length of each tick label among
them) and on cancellation.

**Deterministic and bounded.** The parser and the model hold no shared state; a parsed model is never changed again,
and each compilation and each render keep their state to themselves, so the model is safe for concurrent renders. The
lexer's matchers cannot backtrack and the grammar is read in a loop, so reading a text is linear in its length. Every
loop of a render whose length the text decides - the passes of the compilation, the ids of the references, each day of
the excluded-days loops, each tick and tick label, the rows of the compact mode, the sorts, and each measured text - is
charged to the layout work meter of the render (`MaxLayoutWork`), which checks cancellation. The dates are bounded by
the range of JavaScript dates and by Mermaid's guard of 10,000 years, so no arithmetic can overflow.

**Known differences from Mermaid** are listed in each unit design and in _CanvasNetMermaid System Design_: today comes
only from `MermaidRenderOptions.Now`, and without it the today line is not drawn and 1970-01-01 is taken as today; all
dates are in UTC rather than the viewer's time zone; the date fallback reads only the ECMAScript date time string
format, not the legacy forms of browsers; the failures of Mermaid's parser and compilation, and the tasks Mermaid would
draw without dates, are located CanvasNet exceptions with hints; links are never drawn as links and callbacks never
bound; the skipped ticks and excluded days are reported; element opacity is folded into the colors and the milestone
transform into the geometry; and the accessibility texts are metadata only.
