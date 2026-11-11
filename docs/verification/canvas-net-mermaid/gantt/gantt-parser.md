### GanttParser Unit Verification Design

This document describes the unit-level verification strategy for the `GanttParser` unit in the
`DemaConsulting.CanvasNet.Mermaid.Gantt` namespace: the internal sealed `GanttParser` (the grammar and the lexer, in
two partial files) and the token kinds `GanttTokens`.

#### Verification Approach

The unit is verified in two ways. `GanttVectorTests` parses the 291 cases of
`Fixtures/upstream-vectors/gantt-parser.json`, recorded from the pinned Mermaid release in headless Chrome, with the
UTC time zone and the clock fixed at 2024-06-12T10:30:00Z, by `tools/visual-diff/mermaid/gen-diagram-vectors.mjs
--type gantt` from the texts of `gantt-vector-cases.mjs`: the inputs of Mermaid's gantt parser spec
(`diagrams/gantt/parser/gantt.spec.js`, 34 cases) and of its gantt database spec (`diagrams/gantt/ganttDb.spec.ts`, 33
cases), 210 original texts that cover the lexer's rule order and quirks, the statements, the tasks and their tags,
the click forms, the date formats, the date texts, the references, the excluded days, and the errors, and the 14 gantt
fixtures. Each text goes through Mermaid's preprocessing and detection, as a diagram does. For each of the 269 texts
that Mermaid parses the facts of Mermaid's gantt database (the title, the accessibility texts, the date and axis
formats, the tick interval, the today marker, the includes and excludes, the weekday, the inclusive end dates, the
sections, and the links) must equal the facts of the parsed model, and the compiled tasks are compared as the GanttModel
unit verification design describes. For each of the 22 texts that Mermaid does not parse the error must be the one
Mermaid reports: for the 18 grammar errors a located `mermaid-syntax-error` that finds the same token and lists exactly
the tokens jison expects, in jison's order; for the 3 failures of Mermaid's own parser (the `topAxis` statement and two
tasks whose data holds only tags) a located `mermaid-syntax-error` with no expected tokens and a message that starts
`Mermaid fails`; and for the 1 text Mermaid does not detect (the keyword `GANTT` in capitals) the detector's
`mermaid-unknown-diagram`. `GanttParserTests` verifies the statement values, the keywords, the comments, the tasks, the
click forms, the errors, Mermaid's own failures, and the limits on short hand-written texts, asserting the values, the
tasks, the classes and links, the metadata, the expected tokens, the locations (line and column in the original text),
the messages, and the codes.

No mocking or stubbing is used; the preprocessor, the detector, and the sanitizers are real (each test preprocesses
and detects its text, then calls `Parse`, as the document does). The internal types are reached through the test
assembly's `InternalsVisibleTo` access.

Unit tests reside in `GanttParserTests.cs` and `GanttVectorTests.cs` (with the shared helper `UpstreamVectors.cs`)
within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the `Gantt` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: `Fixtures/upstream-vectors/gantt-parser.json`, generated from the pinned Mermaid by
  `tools/visual-diff/mermaid/gen-diagram-vectors.mjs --type gantt` from
  `tools/visual-diff/mermaid/gantt-vector-cases.mjs`
- **Isolation**: Each test parses its own text; the fixture is read only

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any database fact, statement value, expected token, location, message, or error that differs from the
recorded or asserted Mermaid value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Gantt-GanttParser-Tokens: Tokens As Mermaid's Jison Lexer Makes Them

**Tests**: `GanttVectorTests.GanttParser_Parse_UpstreamVector_MatchesMermaid`,
`GanttParserTests.GanttParser_Parse_KeywordsAnyCaseAndLineFeedSeparator_AreRead`,
`GanttParserTests.GanttParser_Parse_CommentQuirks_FollowJisonRuleOrder`,
`GanttParserTests.GanttParser_Parse_Tasks_SplitNameTagsAndFields`

Asserts that every upstream vector gives Mermaid's facts or Mermaid's error at its place; that `DATEFORMAT YYYY-MM-DD`
gives the date format `YYYY-MM-DD`, that `Title` followed by a line feed and `Next line` gives the title `Next line`
(the keyword's one white-space character is the line feed), and that `SECTION S` gives the section `S`; that in a
chart with the lines `%% one`, `% two`, `A :a, 2024-01-01, 1d`, `x% skipped`, and `B :b, after a, 1d` the comments
and the line `x% skipped` (taken with the line feed before it) are skipped, so the tasks are exactly `a` and `b`, while
the line `}% kept` is not a comment and is refused as a task name with `taskData` expected; and that the task names
`First task`, `Second`, `Third`, and `Marker` run to their colons, each with the space before its colon.

##### CanvasNetMermaid-Gantt-GanttParser-Grammar: The Statements Of Mermaid's Grammar

**Tests**: `GanttVectorTests.GanttParser_Parse_UpstreamVector_MatchesMermaid`,
`GanttVectorTests.GanttParser_Vectors_FileHasEnoughCases_AtLeast200`,
`GanttParserTests.GanttParser_Parse_Statements_KeepTheActionsSubstrings`,
`GanttParserTests.GanttParser_Parse_Tasks_SplitNameTagsAndFields`

Asserts that every upstream vector (a theory with one case for each) gives Mermaid's database facts or Mermaid's error,
and that the file holds at least 200 cases, at least 50 of them from the upstream specs, with between 15 and all but
150 parse errors and between 10 and all but 150 compile errors; that a chart of the ten setting statements
`dateFormat`, `axisFormat`, `tickInterval`, `todayMarker`, `excludes`, `includes`, `weekday`, `weekend`,
`inclusiveEndDates`, and `title` is accepted; and that a section with four tasks of one, two, and three fields and a
vertical marker is accepted, the tasks kept in the order they are written.

##### CanvasNetMermaid-Gantt-GanttParser-Statements: Values As The Grammar's Actions Pass Them

**Tests**: `GanttParserTests.GanttParser_Parse_Statements_KeepTheActionsSubstrings`,
`GanttParserTests.GanttParser_Parse_KeywordsAnyCaseAndLineFeedSeparator_AreRead`,
`GanttParserTests.GanttParser_Parse_Tasks_SplitNameTagsAndFields`,
`GanttVectorTests.GanttParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that `dateFormat` followed by two spaces, `YYYY-MM-DD`, and a space gives the date format `YYYY-MM-DD` with a
space before and after it (the text after the keyword's one white-space character, trailing space kept); that
`axisFormat %d` followed by a space gives `%d` with its trailing space; that `tickInterval 1week` gives `1week`; that
`todayMarker stroke:red,opacity:0.5` gives the whole style; that `title` followed by two spaces, `A title`, and a space
gives the title `A title` with a space before and after it; that the title after a line feed is `Next line`; that the
task names are kept as written up to their colons with their data after them; and that every upstream vector gives
Mermaid's values.

##### CanvasNetMermaid-Gantt-GanttParser-Clicks: The Seven Click Forms

**Tests**: `GanttParserTests.GanttParser_Parse_ClickForms_AddClassesAndSanitizedLinks`,
`GanttVectorTests.GanttParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that, for the tasks `a` and `b` and the statements `click a href "javascript:alert(1)"`,
`click b call cb(1, "x") href "https://example.com"`, `click a,b,zz call cb()`, and
`click b href "https://example.com/b" call cb()`, the task `a` gets the class `clickable` twice and the task `b` five
times (once for each call and once for each link, the id `zz`, which names no task, ignored); and that the links are
exactly `a` with `about:blank` (the `javascript:` URL sanitized) and `b` with `https://example.com/b` (the later link
replacing the earlier one); and that the click statements of every upstream vector give Mermaid's links and classes.

##### CanvasNetMermaid-Gantt-GanttParser-Errors: Errors With Jison's Expected Tokens

**Tests**: `GanttParserTests.GanttParser_Parse_GrammarErrors_ListTheExpectedTokensOfJison`,
`GanttParserTests.GanttParser_Parse_CommentQuirks_FollowJisonRuleOrder`,
`GanttVectorTests.GanttParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that a task name without data (`A task`) is refused with exactly `taskData` expected and a message that names
the line feed found (`found 'NL'`); that a date at the start of a statement (`2024-01-01`, indented by two spaces) is
refused at line 2, column 3 with the 27 tokens that can start a line expected, from `EOF` to `click`; that
`click a` followed by a task line is refused with exactly `callbackname` and `href` expected; that
`click a call cb() call x` is refused at the second call, with a message that names `callbackname` as found and `href`
as the last expected token; that the line `}% kept` is refused with exactly `taskData` expected; and that the 18
grammar errors among the upstream vectors find Mermaid's unexpected token with exactly the tokens jison expects.

##### CanvasNetMermaid-Gantt-GanttParser-MermaidFailures: Mermaid's Own Parser Failures

**Tests**: `GanttParserTests.GanttParser_Parse_MermaidsOwnFailures_FailLocated`,
`GanttVectorTests.GanttParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that the `topAxis` statement, indented by two spaces, is refused at line 2, column 3 with a message that names
`topAxis` and no expected tokens; that the task `A :done, crit`, whose data holds only tags, is refused at line 2 with
a message that names the data of only tags; and that the 3 failures of Mermaid's own parser among the upstream vectors
are located `mermaid-syntax-error`s with no expected tokens and a message that starts `Mermaid fails`.

##### CanvasNetMermaid-Gantt-GanttParser-Limits: Node And Label Limits

**Tests**: `GanttParserTests.GanttParser_Parse_TooManyTasksAndSections_ExceedsMaxNodes`,
`GanttParserTests.GanttParser_Parse_LongText_ExceedsMaxLabelLength`

Asserts that with `MaxNodes` of 3 a section with two tasks is accepted, while two sections with one task each (four
statements) are `mermaid-limit-exceeded` at line 5, the fourth statement, with a message that names `MaxNodes`,
because the task and section statements count together; and that with `MaxLabelLength` of 50 a text of 60 characters
(a theory with 17 cases) is `mermaid-limit-exceeded`, located, with a message that names `MaxLabelLength`, as a title,
a section, a task name, a task's data, a date format, an axis format, a tick interval, an excludes list, an includes
list, a today marker, a one-line accessible title or description, a braced accessible description, the ids of a click
statement, its URL, its function name, or its arguments.
