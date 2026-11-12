### GanttModel

![MermaidGantt Structure](MermaidGanttView.svg)

The `GanttModel` unit is the second software unit in the `Gantt` subsystem. It comprises three source files in the
`DemaConsulting.CanvasNet.Mermaid.Gantt` namespace: `GanttModel.cs` (the database operations, the JavaScript object of
tasks, the links, the metadata, and the label limit, with the enumeration `GanttStartKind` and the sealed class
`GanttRawTask`), `GanttModel.Tasks.cs` (the compilation of the task dates, with the sealed classes `GanttTask` and
`GanttClock` and the private compiler of one render), and `GanttModel.Dayjs.cs` (the static class `Dayjs`, the parts
of dayjs that Mermaid's gantt database uses, with JavaScript's `Number` and the date texts of JavaScript's `Date`).
`GanttModel` is an `internal sealed partial` class, so the parts in `GanttModel.cs` and `GanttModel.Tasks.cs` are one
type, and it implements the system's `IMermaidDiagram`. All three files carry the provenance header comment for the
parts of Mermaid and dayjs they port.

#### Purpose

`GanttModel` is Mermaid's gantt database (`ganttDb.js`, with the title and accessibility setters of `commonDb.ts`) for
one parsed text: it applies the operations that the parser calls with Mermaid's semantics, gives the document the
metadata of the chart, and compiles the task dates of each render as Mermaid's `getTasks` compiles them when it draws,
with the dayjs functions Mermaid bundles and the clock of the render options; its `Layout` has the `GanttRenderer` draw
the chart. It exists so that the meaning of a gantt chart - its settings, its tasks, and their dates - is decided in
one place that can be compared with Mermaid's database and compiled tasks fact by fact, independently of the grammar and
of the drawing.

The unit's behavior is specified by the `CanvasNetMermaid-Gantt-GanttModel-*` requirements in
`docs/reqstream/canvas-net-mermaid/gantt/gantt-model.yaml`.

#### Data Model

**The database** (filled by one parser on one thread and never changed after parsing, so safe for concurrent renders):

- the **settings** as the grammar passes them, trailing white space kept (their uses trim them): the date format and
  the axis format (empty by default), the tick interval (none by default), the today marker (empty, `off`, or a style)
  with the location of its last statement, whether end dates are inclusive, the weekday the week ticks start on
  (`sunday` by default), and the first day of the weekend (`friday` or `saturday`, the default);
- the **excludes** and **includes** tokens, in lower case, split on white space and commas, and merged across
  statements, each once (Mermaid's `mergeTokens`);
- the sanitized **title** of the `title` statement with the location of its last statement, and the sanitized
  accessible title and description, each empty when not set; the **sections** of the `section` statements in order;
- the **tasks** in the order of the text (`GanttRawTask`): the section (empty before the first `section`), the name as
  written (trailing white space included), the id (null when the data has more than three fields, Mermaid's
  `undefined`), the id of the task before it (null for none), how the start is given (`GanttStartKind`: `None` for more
  than three fields, `PreviousTaskEnd` for one field, and `StartDate` for two or three), the start and end data, the
  tags `active`, `done`, `crit`, `milestone`, and `vert`, the row (`Order`: the next row, or -1 for a vertical marker),
  the classes (`clickable` once for each click call on the task), and the location of the task statement;
- the **object of tasks** (Mermaid's `taskDb`, a plain JavaScript object): the index of the last task of each id, a
  missing id stored under the key `undefined` and the id `__proto__` never stored, with the count of tasks without an id
  (`taskCnt`), the id of the last task (`lastTaskID`), the next row (`lastOrder`), and the current section;
- the **links** by task id, sanitized, in the order they were first set; and the limits of the open call and the
  location of the diagram keyword.

**`GanttTask`** is a task of one render, as `getTasks` gives it to the renderer: its raw task and its row, and the
start, the end, and the end of the drawn bar when excluded days moved the end (`RenderEndTime`), each a `JsDate` (see
_TimeAxis Unit Design_) or null while undefined, whether the dates are resolved (`Processed`), and whether the end is a
`YYYY-MM-DD` date that excluded days do not move (`ManualEndTime`). The renderer may set the row again for the compact
mode.

**`GanttClock`** is the clock of one render: today (`Today`) is the time of `MermaidRenderOptions.Now` in UTC, or
1970-01-01T00:00Z when it is not set (`IsKnown` false), with the report sink of the render and whether the warning was
reported. **`Dayjs`** is stateless: the English locale (weekday and month names, the ordinal function, short names of
three letters, the meridiem `AM`/`PM`, and no long-date formats) and the token lists of the core format, the
`advancedFormat` plugin, and the `customParseFormat` plugin.

#### Key Methods

- **Settings.** `SetDateFormat`, `SetAxisFormat`, `SetTickInterval`, `SetTodayMarker`, `EnableInclusiveEndDates`,
  `SetWeekday`, and `SetWeekend` keep their values as written, the last statement winning; `SetExcludes` and
  `SetIncludes` merge their tokens (`MergeTokens`); `SetDiagramTitle` sanitizes the title and marks it set
  (`IsTitleSet`), and `SetAccessibleTitle` and `SetAccessibleDescription` sanitize their texts as Mermaid's
  `commonDb.ts` does (the title without leading white space, the description without the white space after each line
  feed); `AddSection` starts a section.
- **`AddTask(name, data, location)`** is Mermaid's `addTask` with `parseData`: the data after its colon is split on
  commas; the leading tags (fields that are a tag when trimmed) are taken off in Mermaid's order until none matches, and
  data that runs out of fields while tags are taken off fails as Mermaid fails; the remaining fields are trimmed and
  read as one field (an end after the task before it, with the next generated id `task<N>`), two (a start and an end,
  with the next generated id), or three (an id, a start, and an end); more fields leave the id and the start undefined.
  The task gets the current section, the id of the last task, its tags, and its row, and the object of tasks records it
  under its id.
- **`SetClickEvent`** adds the class `clickable` to each task of the ids that exists (`SetClass`); the function name
  and its arguments are not kept, since callbacks are never bound. **`SetLink`** sanitizes the URL with `UrlSanitizer`
  (Mermaid's `sanitizeUrl` of the `strict` security level), keeps it for each id that names a task (a later link
  replacing an earlier one), and then adds the class as `SetClass` does.
- **`FindTask(id)`** is Mermaid's `findTaskById`: the last task with the id; a null id is the key `undefined`, and
  `__proto__` is never found. **`IndexOf`** gives its index for the compilation.
- **Metadata** (`IMermaidDiagram`): `Title` is the title of the `title` statement trimmed, or null when it is empty (the
  document then falls back to the frontmatter title unless `IsTitleSet`, which the diagram exposes through
  `IMermaidDiagram`), `AccTitle` the accessible title or null, `Direction` null,
  `NodeCount` the number of tasks, vertical markers included, `EdgeCount` 0, and `SubgraphCount` the number of
  sections of the tasks that are not vertical markers, each once, without a section named `__proto__` (whose title
  Mermaid does not draw).
- **`Layout(DiagramRenderContext)`** creates a `GanttRenderer` for the render and returns the scene it draws (see
  _GanttRenderer Unit Design_).
- **`Compile(GanttClock, LayoutWorkMeter)`** is Mermaid's `getTasks` for one render: it copies the raw tasks into
  `GanttTask`s of the render and runs one pass over them, then up to ten more while a task is unresolved
  (`compileTasks`), charging one unit of work for each task in each pass. A task (`compileTask`) takes its start from
  the end of the task before it (found by its id) or from `GetStartDate`; when it has a start, its end from
  `GetEndDate`; and when it has an end, it is processed, its end is manual when the end data is a strict `YYYY-MM-DD`
  date, and `CheckTaskDates` moves its end past the excluded days. After the passes every task must be processed with
  a valid start and end.
- **`GetStartDate`**: for the date format `x` or `X` and data of digits only, the timestamp, one unit of work for each
  digit; for `after` and ids (`ReferenceIds`, the lower-case word, white space, and ids of letters, digits, `_`, `-`,
  and spaces, split on single spaces, as Mermaid's regular expression reads them), the latest end of the tasks the ids
  name, one unit of work for each id, or today at midnight when no id names a task (`GanttClock.TodayAtMidnight`); else
  the date the date format reads strictly (`Dayjs.ParseStrict`, which charges its work to the meter of the render);
  else the date text as JavaScript's `Date` reads it in the ECMAScript format (`Dayjs.ParseDateString`), which must be
  valid with a year from -10,000 to 10,000.
- **`GetEndDate`**: for `until` and ids, the earliest start of the tasks the ids name, or today at midnight; else the
  date the date format reads strictly (charged as for the start), a day later with inclusive end dates; else the start
  plus the duration that `ParseDuration` reads (Mermaid's `parseDuration`: `/^(\d+(?:\.\d+)?)([Mdhmswy]|ms)$/` on the
  trimmed text), added with `Dayjs.Add`; else the start.
- **`CheckTaskDates`** is Mermaid's `checkTaskDates` with `fixTaskDates`: when excludes are set and the end is not
  manual, it walks the days from the day after the start to the end, each day charged `DayCost` (1 plus the length of
  the date format plus, for each excludes and includes token, 1 and its length, which bounds the searches of the
  lists) and the formatting of the day, which `IsInvalidDate` charges; each excluded day moves the end a day later, the
  end of the drawn bar is the last end before an excluded day, and an end more than 10,000 days past its first value
  fails as Mermaid fails.
- **`IsInvalidDate`** is Mermaid's `isInvalidDate`: a date is excluded unless its text in the date format or as
  `YYYY-MM-DD` is included, when weekends are excluded and its ISO weekday is the first or second day of the weekend
  (`WEEKEND_START_DAY`: Friday 5, Saturday 6), or when its weekday name (`dddd`, in lower case), its text in the date
  format, or its `YYYY-MM-DD` text is excluded. It formats the date with the work meter of the render, so each
  formatting is charged.
- **`GanttClock.TodayAtMidnight`** gives today at midnight and **`NoteTodayUsed`** notes a strict date that took a
  field from today; both report, when today is not known, one `mermaid-gantt-today-unavailable` warning for the clock
  (`Warn`), so one for each compilation, located at the first task that needs today.
- **`Dayjs`**: `Add` adds a duration as dayjs does (`ms`, `s`, `m`, and `h` as milliseconds; `d` and `w` through
  `setDate` with the number of days rounded as JavaScript's `Math.round` rounds it; `M` and `y` through the month or the
  year, the day kept or the last day of a shorter month taken); `StartOfDay`, `EndOfDay`, `IsoWeekday`, and `DiffYears`
  (dayjs's month difference divided by 12, truncated); `Format` (the `advancedFormat` tokens `Q`, `Do`, `k`, `kk`, `X`,
  `x`, `W`, and `WW`, then the core tokens, text in brackets kept, the default format `YYYY-MM-DDTHH:mm:ssZ` for an
  empty one, and `Invalid Date` for an invalid date); `ParseStrict` (the `customParseFormat` plugin's strict parse: the
  format split into its tokens, each value taken with its token's expression, the date built with today for the fields
  the format does not give, and the date made invalid unless formatting it gives the text back; `x` and `X` read as
  timestamps); `ToNumber` (JavaScript's `Number`: white space ignored, an empty text 0, and the decimal, hexadecimal,
  octal, binary, and infinite literals, a radix literal rounded to the nearest double as JavaScript rounds it and taken
  as infinite without building a number past 1,024 bits); and `ParseDateString` (the ECMAScript date time string
  format: a four-digit or signed six-digit year, an optional month and day, an optional time with seconds and a
  fraction, and with a time an optional `Z` or offset; a date alone in UTC, a time without an offset local, which is
  UTC; a day up to 31 carried into the next month and `24:00` as the next midnight, as V8 reads them).
- **Cost of `Dayjs`.** `Format` is linear in its format: the search for the end of a text in brackets resumes at the
  closing bracket it found last, so a format of many opening brackets is searched once. `ParseStrict` splits the
  format in one pass (a text in brackets is searched only up to the next opening bracket), searches the rest of the
  text once for each token with a value (the word expression of `A`, `a`, `Do`, `MMM`, and `MMMM` passes a run of
  digits whole instead of trying each start in it), and removes the value from the text with a linear search (Knuth,
  Morris, and Pratt), so its work is at most the product of the lengths of the text and the format. Given a work
  meter, `Format` charges 1 plus the length of the format and of the text its advanced tokens give, and `ParseStrict`
  charges 1 plus the length of the text and twice that of the format, 1 plus twice the length of the rest of the text
  for each token with a value before it is searched, and the formatting of the strict check.
- **`CheckLength`** checks a text's length against `MaxLabelLength`; the parser uses it for the tokens it keeps, and the
  model for the texts it derives (`Checked`).

#### Error Handling

- **While the text is parsed**, the model raises `MermaidSyntaxException`s, located in the original text, with a hint:
  - a task whose data holds only tags, with code `mermaid-syntax-error`, the message `Mermaid fails on a task whose data
    holds only tags (...)`, no expected tokens, and the hint to give the task a start or a duration after its tags;
  - a derived text longer than `MaxLabelLength`, with code `mermaid-limit-exceeded` and the message `a text is N
    characters long, more than the limit of M (MermaidLimits.MaxLabelLength)`: the sanitized title or accessibility
    text (the sanitizer can make a text longer), located at its statement; the sanitized link, located at the click
    statement; and the class list of a task joined with spaces, which grows by `clickable` with each click call,
    located at the click statement that makes it too long.
- **When a render compiles the dates**, `Compile` raises `MermaidRenderException`s with code `mermaid-render-failed`,
  located at the task statement, with a hint, where Mermaid's compilation fails:
  - a start date that no form reads, with Mermaid's message `Invalid date:` followed by the text, and the hint to write
    the date as the `dateFormat` says, or as `YYYY-MM-DD` without one;
  - a task without a start whose task before it does not exist (the first task), with the message of Mermaid's
    JavaScript error (`reading 'endTime'`);
  - a task with more than three data fields (`reading 'type'`);
  - excluded days that would move a task's end by more than 10,000 days, with Mermaid's message ``Failed to find a
    valid date that was not excluded by `excludes` after 10,000 iterations.`` and the hint to leave a day of the week
    out of `excludes` or to shorten the task;
  - a date format whose formatting of a valid date needs a plugin that Mermaid does not load (the week tokens `w`,
    `ww`, `wo`, and `gggg`, and the zone names `z` and `zzz`), with the hint to remove those tokens;
  - and, where Mermaid would draw a task without valid dates, a task still unresolved after the passes (a circular
    `after` or `until` reference: `the start or end of the task ... cannot be resolved`) or with an invalid date (`the
    task ... has an invalid date`).
- `Compile` propagates the errors of the work meter: a `MermaidRenderException` with code `mermaid-limit-exceeded` that
  names `MaxLayoutWork` when the compilation needs more work than the limit, and `OperationCanceledException` for a
  canceled token.
- `Dayjs.Format` and `Dayjs.ParseStrict` throw `NotSupportedException` for a token whose plugin Mermaid does not load,
  which the compilation turns into the located failure above. `Compile` and `Layout` throw `ArgumentNullException` for
  null arguments.

#### Dependencies

- **GanttRenderer** - created by `Layout` to draw a render.
- **TimeAxis** - `JsDate` (the JavaScript date whose local time zone is UTC) and `TimeAxis.JsRound` (JavaScript's
  `Math.round`).
- **Syntax subsystem** - `HtmlSanitizer` (Mermaid's `sanitizeText`), `UrlSanitizer` (Mermaid's `sanitizeUrl`),
  `Preprocessor.TrimJs` (JavaScript's `trim`), `ScanPatterns` (the JavaScript white space class), and
  `YamlSubsetParser.FormatJsNumber` (JavaScript's `String(number)`).
- **Dagre subsystem** - `LayoutWorkMeter`, the work budget and cancellation of a render.
- **Scenegraph subsystem** - `Scene`, the result of a layout.
- **System** - `IMermaidDiagram`, `DiagramRenderContext`, `MermaidReportSink`, `MermaidLimits`, `MermaidLocation`,
  `MermaidSyntaxException`, `MermaidRenderException`, and `MermaidDiagnosticCodes`.

#### Callers

- **GanttParser** - creates the model in `Parse`, calls its database operations as the grammar's actions run, and
  checks the texts of its tokens with `CheckLength`.
- The document pipeline of the system - reads the metadata and calls `Layout` for every measure and render.
- **GanttRenderer** - creates the `GanttClock` of a render from `MermaidRenderOptions.Now`, calls `Compile` with it and
  the work meter of the render, and reads the settings of the model, the title and the today marker with their
  locations, `IsInvalidDate`, `DayCost`, and the `Dayjs` functions for the excluded days and the axis.
- The unit and subsystem tests call `Compile`, `FindTask`, and the `Dayjs` functions directly, and the vector tests
  compare the state of the model and the compiled tasks with Mermaid's.

#### Known Differences From Mermaid

- **The clock.** Today is the time of `MermaidRenderOptions.Now`, never the system clock. Without it, 1970-01-01 is
  taken as today for a reference that names no task and for the fields a date format does not give, and each
  compilation that needs today reports one `mermaid-gantt-today-unavailable` warning; Mermaid reads the viewer's
  clock.
- **UTC.** All dates are in UTC, as in a browser whose time zone is UTC; Mermaid uses the viewer's time zone.
- **The date fallback.** Without a date format that reads it, a start date is read in the ECMAScript date time string
  format only; the legacy forms that browsers also read (such as `2024-1-5` or `Jan 5 2024`) fail as an invalid date.
- **Failures are located.** Where Mermaid's compilation fails with a JavaScript error, and where Mermaid would draw a
  task without valid dates (`NaN` geometry), the render fails with a located `MermaidRenderException` with a hint.
- **The bundled dayjs.** The dayjs functions follow the dayjs that Mermaid bundles, with the plugins it loads, not a
  later dayjs; the tokens whose plugins it does not load fail on a valid date as they do in Mermaid.
- **Links and callbacks.** Links are sanitized as in the `strict` security level only and kept as data, never drawn as
  links; callbacks are never bound, in any security level, while the `clickable` class is kept as in Mermaid.
- **Limits.** The label limit refuses a derived text when the document is opened, and the layout work limit bounds the
  compilation of a render; Mermaid has no such limits.
- **Accessibility.** `accTitle` and `accDescr` are metadata only; a rendered picture has no SVG title or description.
