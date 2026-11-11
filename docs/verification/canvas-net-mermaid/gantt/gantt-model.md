### GanttModel Unit Verification Design

This document describes the unit-level verification strategy for the `GanttModel` unit in the
`DemaConsulting.CanvasNet.Mermaid.Gantt` namespace: the internal sealed `GanttModel` (the database, the metadata, and
the compilation of the task dates, in three partial files) with `GanttStartKind`, `GanttRawTask`, `GanttTask`,
`GanttClock`, and the static `Dayjs`.

#### Verification Approach

The unit is verified in two ways. `GanttVectorTests` parses the 291 cases of
`Fixtures/upstream-vectors/gantt-parser.json` (see the GanttParser unit verification design for their sources) and,
for each of the 269 texts that Mermaid parses, compares the facts of the model with the facts of Mermaid's gantt
database and then compiles the task dates with the clock of the vectors, 2024-06-12T10:30:00Z, and compares them with
the tasks that Mermaid's `getTasks` gave in a browser with the UTC time zone and the same clock. For each of the 22
texts on which Mermaid's compilation throws, the compilation must fail with a located `mermaid-render-failed` whose
message holds Mermaid's message (such as `Invalid date:` with the text, `Failed to find a valid date`, or the
`Cannot read properties of undefined` of a task without a start or with more than three fields). For the 11 texts whose
start dates Mermaid reads with the browser's legacy date forms (a documented difference), the compilation must fail
with Mermaid's `Invalid date:` message; for the 3 texts whose tasks Mermaid leaves unresolved and would draw without
geometry (a circular reference, a task after itself, and a chain longer than the passes), it must fail with a located
`mermaid-render-failed`. For each of the other 233 texts every compiled task must equal Mermaid's: its id, name,
section, processed flag, manual end, classes, tags, row, start, end, and end of the drawn bar, as times in
milliseconds. `GanttModelTests` verifies the durations, the references, the excluded and included days, the inclusive
end dates, today with and without a clock, the date fallback, the failures, the object semantics of ids, the work
charges, the work limit, and the dayjs functions on short hand-written texts and dates, asserting exact times in
milliseconds, codes, messages, locations, reports, and work units. The statements, tasks, links, metadata, and label
limits of the database are verified through the parser in `GanttParserTests`, since only the parser fills a model.

No mocking or stubbing is used; the preprocessor, the detector, the parser, the sanitizers, and the work meter are
real, and the clock is a real `GanttClock` built from a fixed time or none, with a real report sink. The internal types
are reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `GanttModelTests.cs`, `GanttParserTests.cs`, and `GanttVectorTests.cs` (with the shared helper
`UpstreamVectors.cs`) within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the `Gantt` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: `Fixtures/upstream-vectors/gantt-parser.json`, generated from the pinned Mermaid by
  `tools/visual-diff/mermaid/gen-diagram-vectors.mjs --type gantt` from
  `tools/visual-diff/mermaid/gantt-vector-cases.mjs`
- **Clock**: 2024-06-12T10:30:00Z for the vectors and the compilations that need a clock; none where a test asserts
  the epoch fallback
- **Isolation**: Each test parses and compiles its own text with its own clock and work meter; the fixture is read only

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any database fact, task date, flag, class, row, code, message, location, report, or work unit that differs
from the recorded or asserted value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Gantt-GanttModel-Database: Statements As Mermaid's Database Keeps Them

**Tests**: `GanttParserTests.GanttParser_Parse_Statements_KeepTheActionsSubstrings`,
`GanttVectorTests.GanttParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that the statements of one chart give, as written, the date format `YYYY-MM-DD` with a space before and after
it, the axis format `%d` with a space after it, the tick interval `1week`, and the today marker
`stroke:red,opacity:0.5`; that `excludes Weekends, 2024-01-01` gives exactly the tokens `weekends` and `2024-01-01` (in
lower case, split on the comma and the space) and `includes 2024-01-06` the token `2024-01-06`; that `weekday monday`,
`weekend friday`, and `inclusiveEndDates` give the weekday `monday`, the weekend `friday`, and inclusive end dates;
that the title `A title` is kept with the space before and after it; and that every upstream vector gives Mermaid's
database facts, among them the excludes and includes merged across statements without duplicates.

##### CanvasNetMermaid-Gantt-GanttModel-Tasks: Tasks As Mermaid's Database Adds Them

**Tests**: `GanttParserTests.GanttParser_Parse_Tasks_SplitNameTagsAndFields`,
`GanttVectorTests.GanttParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that the tasks `First task :crit, done, a1, 2024-01-01, 3d`, `Second :2024-02-01, 1w`, `Third :2d`, and
`Marker :vert, 2024-01-02, 0d` in one section have the ids `a1`, `task1`, `task2`, and `task3`; that the first has the
tags `crit` and `done` but not `active`, a start given as a date, the start data `2024-01-01`, and the end data `3d`;
that the third starts at the end of the task before it, whose id is `task1`; that the rows are 0, 1, 2, and -1 (the
vertical marker); and that the tasks of every upstream vector have Mermaid's ids, names, sections, tags, classes, and
rows.

##### CanvasNetMermaid-Gantt-GanttModel-Links: Clickable Tasks And Sanitized Links

**Tests**: `GanttParserTests.GanttParser_Parse_ClickForms_AddClassesAndSanitizedLinks`,
`GanttVectorTests.GanttParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that each call and each link of a click statement adds `clickable` once to each named task that exists (twice
for `a` and five times for `b`, nothing for the id `zz`); that the link `javascript:alert(1)` is kept as `about:blank`
and a later link of `b` replaces its earlier one, so the links are exactly `a` with `about:blank` and `b` with
`https://example.com/b`; and that the links and classes of every upstream vector are Mermaid's.

##### CanvasNetMermaid-Gantt-GanttModel-ObjectKeys: Ids As A JavaScript Object Finds Them

**Tests**: `GanttModelTests.GanttModel_FindTask_JavaScriptObjectSemantics`,
`GanttVectorTests.GanttParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that of the tasks `A` and `B`, both with the id `x`, the id `x` finds `B`, the later one; that the task with
the id `__proto__` is never found, so `click __proto__ href "https://example.com"` gives it no class and no link; that
a missing id finds the task `U` with the id `undefined`; and that the upstream vectors with the ids `__proto__` and
`constructor` give Mermaid's links and dates.

##### CanvasNetMermaid-Gantt-GanttModel-Metadata: Counts And Title

**Tests**: `GanttParserTests.GanttParser_Parse_Tasks_SplitNameTagsAndFields`,
`GanttParserTests.GanttParser_Parse_Statements_KeepTheActionsSubstrings`

Asserts that the four tasks above, a vertical marker among them, in one section give a node count of 4, an edge count
of 0, and a subgraph count of 1; and that the title `A title`, written with a space before and after it, gives the
trimmed title `A title`.

##### CanvasNetMermaid-Gantt-GanttModel-Compile: Dates Compiled In Passes For Each Render

**Tests**: `GanttModelTests.GanttModel_Compile_AfterUntilAndForwardReferences_Resolve`,
`GanttVectorTests.GanttParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that the task `C :c, after a b, 1d`, written before the tasks `a` and `b` it refers to, starts at
2024-01-07 (resolved in a later pass); that the task `E :2d`, without a start, starts at the end of the task before it
(2024-01-01) and ends at 2024-01-03; and that the compiled tasks of the 233 upstream vectors that Mermaid compiles to
valid dates are Mermaid's, field by field.

##### CanvasNetMermaid-Gantt-GanttModel-Durations: Durations As Dayjs Adds Them

**Tests**: `GanttModelTests.GanttModel_Compile_Durations_AddAsDayjsDoes`,
`GanttVectorTests.GanttParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that under `dateFormat YYYY-MM-DD` the end of `2024-01-31` plus `1M` is 2024-02-29 (the last day of the
shorter month); that `2024-01-01` plus `1.5d` ends two days later (the days rounded); that `36h` ends one and a half
days later; that `2w` ends 14 days later; that `1 d`, which is no duration, ends at the start; and that the durations
of the upstream vectors (milliseconds to years) give Mermaid's ends.

##### CanvasNetMermaid-Gantt-GanttModel-References: After And Until

**Tests**: `GanttModelTests.GanttModel_Compile_AfterUntilAndForwardReferences_Resolve`,
`GanttVectorTests.GanttParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that with `A :a, 2024-01-01, 3d` and `B :b, 2024-01-02, 5d` the task `after a b` starts at 2024-01-07, the
later of their ends, and the task `D :d, 2024-01-01, until b a` ends at 2024-01-01, the earlier of their starts; and
that the references of the upstream vectors, those to later tasks, to several tasks, and to tasks that do not exist
among them, give Mermaid's dates (`AFTER a` in capitals is no reference and fails as an invalid date, as in Mermaid).

##### CanvasNetMermaid-Gantt-GanttModel-Excludes: Excluded And Included Days

**Tests**: `GanttModelTests.GanttModel_Compile_ExcludesAndIncludes_MoveEnds`,
`GanttVectorTests.GanttParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that with `excludes weekends` the task `2024-01-05, 2d` (from a Friday) ends at 2024-01-09 with its drawn bar
ending at 2024-01-09, while the task `2024-01-05, 2024-01-08` keeps its manual end 2024-01-08 with no drawn end of its
own; that with `includes 2024-01-06` the Saturday is a working day and the same task ends at 2024-01-08; that with
`weekend friday` the task `2024-01-04, 2d` (from a Thursday) ends at 2024-01-08; and that the excluded weekdays, dates,
and weekends of the upstream vectors give Mermaid's ends and drawn ends.

##### CanvasNetMermaid-Gantt-GanttModel-InclusiveEnd: Inclusive End Dates

**Tests**: `GanttModelTests.GanttModel_Compile_InclusiveEndDates_AddADayToDates`,
`GanttVectorTests.GanttParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that with `inclusiveEndDates` the task `2024-01-01, 2024-01-03` ends at 2024-01-04 with a manual end, while
the task `2024-01-01, 2d` ends at 2024-01-03; and that the inclusive end dates of the upstream vectors are Mermaid's.

##### CanvasNetMermaid-Gantt-GanttModel-Today: Today From The Clock Or The Epoch

**Tests**: `GanttModelTests.GanttModel_Compile_Today_FromClockOrEpochWithWarning`,
`GanttVectorTests.GanttParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that under `dateFormat HH:mm`, with the clock at 2024-06-12T23:30+02:00 (21:30 UTC), the task `10:30, 2h`
starts at 2024-06-12T10:30Z (the date taken from today in UTC) and the task `after nothing, 1h` at 2024-06-12T00:00Z
(today at midnight), with no report; that without a clock the same tasks start at 10.5 hours after the epoch and at the
epoch; that exactly one report is delivered for the compilation without a clock, of code
`mermaid-gantt-today-unavailable`, although two tasks need today; and that the vectors whose dates take today give
Mermaid's dates for the clock of the vectors.

##### CanvasNetMermaid-Gantt-GanttModel-DateFallback: ECMAScript Date Texts Only

**Tests**: `GanttModelTests.GanttModel_Compile_DateFallback_ReadsEcmaScriptFormsOnly`,
`GanttModelTests.Dayjs_NumberAndDateStrings_FollowJavaScript`,
`GanttVectorTests.GanttParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that without a date format the start `2024-01-05` is 2024-01-05T00:00Z, `2024-01-05T10:00:00+02:00` is
2024-01-05T08:00Z, and `2024-02-30` is 2024-03-01 (the day carried); that the legacy form `2024-1-5`, indented by two
spaces, fails with `mermaid-render-failed`, the message `Invalid date:2024-1-5`, at line 3, column 3; that
`ParseDateString` reads `2024` as 2024-01-01 and `2024-01-05T24:00` as 2024-01-06, and refuses `2024-01-05T10` and the
year `-000000`; that the 27 date texts of the upstream vectors read as `new Date` reads them give Mermaid's dates or
fail where Mermaid fails, except the legacy forms; and that the 11 vectors with legacy date forms, among them some of
those 27, fail with Mermaid's `Invalid date:` message.

##### CanvasNetMermaid-Gantt-GanttModel-Dayjs: The Dayjs That Mermaid Bundles

**Tests**: `GanttModelTests.Dayjs_Format_TokensAsTheBundledDayjs`,
`GanttModelTests.Dayjs_ParseStrict_MatchesTheCustomParseFormatPlugin`,
`GanttModelTests.Dayjs_NumberAndDateStrings_FollowJavaScript`,
`GanttModelTests.GanttModel_Compile_Durations_AddAsDayjsDoes`

Asserts that 2024-01-05T09:07:03.045Z formatted with
`YYYY-MM-DD HH:mm:ss.SSS [at] dddd ddd dd MMMM MMM Do Q W kk h a A Z ZZ X` is exactly
`2024-01-05 09:07:03.045 at Friday Fri Fr January Jan 5th 1 1 09 9 am AM +00:00 +0000 1704445623`, and with an empty
format `2024-01-05T09:07:03+00:00`; that the format `w` throws `NotSupportedException` for a valid date and gives
`Invalid Date` for an invalid one; that with today at 2024-06-12T10:30Z the strict parse reads `05-01-2024` as
`DD-MM-YYYY` to 2024-01-05, `69-01-08` as `YY-MM-DD` to 1969-01-08, and `04:30 pm` as `hh:mm a` to
2024-06-12T16:30Z (taking the date from today and noting it), and refuses `2024-1-5` as `YYYY-MM-DD` (it does not
format back) and `01/05/2024` as `L` (no long-date format in the locale); that `Number` gives 0 for spaces, 16 for
`0x10`, 1000 for ` 1e3 `, and NaN for `12px`; that the year difference from 2024-06-01 to 2023-01-01 is -1 (truncated);
and that the month and day arithmetic of the durations above is dayjs's.

##### CanvasNetMermaid-Gantt-GanttModel-RenderFailures: Located Compilation Failures

**Tests**: `GanttModelTests.GanttModel_Compile_Failures_AreLocatedRenderFailures`,
`GanttModelTests.GanttModel_Compile_DateFallback_ReadsEcmaScriptFormsOnly`,
`GanttVectorTests.GanttParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that five texts (a theory) fail with `mermaid-render-failed`, located, with a message that holds: `(reading
'endTime')` for a first task `A :3d` without a start; `(reading 'type')` for a task with four data fields;
`Failed to find a valid date` for a task under `excludes weekends monday tuesday wednesday thursday friday`;
`cannot be resolved` for two tasks `after` each other; and `Invalid date` for the date `2024-01-01 1` under the date
format `YYYY-MM-DD w`, whose week token the bundled dayjs cannot parse; that the legacy date `2024-1-5` fails at its
task; and that the 22 compile errors, the 11 legacy dates, and the 3 unresolved charts among the upstream vectors fail
as the Verification Approach describes.

##### CanvasNetMermaid-Gantt-GanttModel-WorkCharges: Pinned Work Charges

**Tests**: `GanttModelTests.GanttModel_Compile_WorkCharges_ArePinned`

Asserts that compiling the tasks `A :a, 2024-01-01, 3d` and `B :b, after a, 2d` under `dateFormat YYYY-MM-DD` charges
exactly 3 work units (one for each task in the one pass and one for the `after` id), and that with `excludes weekends`
the same tasks charge exactly 87 (the same 3 plus 7 days of the excluded-days loops at a day cost of 12: 1, the 10
characters of the date format, and the 1 excludes token).

##### CanvasNetMermaid-Gantt-GanttModel-Limits: Derived Texts And Work

**Tests**: `GanttParserTests.GanttParser_Parse_ManyClicks_ClassListExceedsMaxLabelLength`,
`GanttModelTests.GanttModel_Compile_LongExcludedSpan_ExceedsWorkLimit`

Asserts that with `MaxLabelLength` of 50 six statements `click a call f()` make the class list of the task `a` (six
times `clickable` joined with spaces, 59 characters) too long, refused with `mermaid-limit-exceeded` at line 8, the
sixth click statement, after five were accepted; and that the task `2024-01-01, 2000y` under `excludes weekends`,
compiled with a work meter of 100,000 units, fails with a `MermaidRenderException` of code `mermaid-limit-exceeded`
whose message names `MaxLayoutWork`.
