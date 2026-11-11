### PieParser Unit Verification Design

This document describes the unit-level verification strategy for the `PieParser` unit in the
`DemaConsulting.CanvasNet.Mermaid.Pie` namespace: the internal sealed `PieParser` (the lexer and the grammar, in two
partial files), the token `PieToken`, the section `PieSection`, and the diagram `PieDiagram`.

#### Verification Approach

The unit is verified in two ways. `PieVectorTests` parses the 133 cases of `Fixtures/upstream-vectors/pie-parser.json`,
recorded from the pinned Mermaid release in headless Chrome by
`tools/visual-diff/mermaid/gen-diagram-vectors.mjs --type pie` from the texts of `pie-vector-cases.mjs`: the inputs of
Mermaid's pie spec (`diagrams/pie/pie.spec.ts`, 13 cases) and of the pie tests of `@mermaid-js/parser`
(`tests/pie.test.ts`, 28 cases), original texts that cover the lexer's first-match order, the value converters, the
numbers, the escapes, the comments, the repeated labels, and the errors, and the 8 pie fixtures. Each text goes through
Mermaid's preprocessing and detection, as a diagram does. For each of the 94 valid texts the facts of Mermaid's pie
database (the type, the title, the accessible title and description, the `showData` flag, and the sections with their
labels and their values as JavaScript's `String` writes them) must equal the facts of the parsed diagram. For each of
the 39 invalid texts the error must be the first one Mermaid reports: for the 5 database errors (negative values) a
`mermaid-syntax-error` whose message holds Mermaid's message; for the 21 lexical errors a located `unrecognized text`
error at the line and column of Mermaid's first lexer error; and for the 13 grammar errors a located error at the line
and column of Mermaid's first parser error with exactly the expected tokens chevrotain lists. `PieParserTests`
verifies the sections, the database, the header, the value converters, the errors, the numbers, and the limits on short
hand-written texts, asserting the sections, the texts, the metadata, the expected tokens, the locations (line and
column in the original text), the messages, and the hints.

No mocking or stubbing is used; the preprocessor, the detector, and the sanitizer are real (each test preprocesses and
detects its text, then calls `Parse`, as the document does). The internal types are reached through the test
assembly's `InternalsVisibleTo` access.

Unit tests reside in `PieParserTests.cs` and `PieVectorTests.cs` (with the shared helper `UpstreamVectors.cs`) within
the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the `Pie` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: `Fixtures/upstream-vectors/pie-parser.json`, generated from the pinned Mermaid by
  `tools/visual-diff/mermaid/gen-diagram-vectors.mjs --type pie` from `tools/visual-diff/mermaid/pie-vector-cases.mjs`
- **Isolation**: Each test parses its own text; the fixture is read only

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any database fact, expected token, location, message, or error that differs from the recorded or asserted
Mermaid value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Pie-PieParser-Tokens: Tokens As Mermaid's Lexer Makes Them

**Tests**: `PieVectorTests.PieParser_Parse_UpstreamVector_MatchesMermaid`,
`PieParserTests.PieParser_Parse_ShowDataAndTitle_AreRead`,
`PieParserTests.PieParser_Parse_Numbers_FollowTheTerminals`

Asserts that every upstream vector gives Mermaid's facts or Mermaid's first error at its place; that in
`pie showData title   A   pie  %% not this` the keyword `showData` is read and the title stops before the `%%`
comment; and that the numbers `10.500`, `0`, and `-0` are the values 10.5, 0, and a negative zero, while `007` is read
as the number `0` followed by another number, so the line end is refused with `NEWLINE` and `EOF` expected.

##### CanvasNetMermaid-Pie-PieParser-Grammar: The Statements Of Mermaid's Grammar

**Tests**: `PieVectorTests.PieParser_Parse_UpstreamVector_MatchesMermaid`,
`PieVectorTests.PieParser_Vectors_FileHasEnoughCases_AtLeast120`,
`PieParserTests.PieParser_Parse_Sections_KeepsOrderLabelsAndValues`,
`PieParserTests.PieParser_Parse_ShowDataAndTitle_AreRead`

Asserts that every upstream vector (a theory with one case for each) gives Mermaid's database facts or Mermaid's
error, and that the file holds at least 120 cases, at least 30 of them from the upstream specs, with between 30 and
all but 80 errors; that the indented sections `"Dogs" : 386`, `'Cats' : 85.25`, and `"Rats" : 0` are kept in that order
with the labels `Dogs`, `Cats`, and `Rats` and the values 386, 85.25, and 0; and that `showData` and a `title` statement
on the `pie` line itself are accepted.

##### CanvasNetMermaid-Pie-PieParser-Values: Values As Mermaid's Converters Give Them

**Tests**: `PieVectorTests.PieParser_Parse_UpstreamVector_MatchesMermaid`,
`PieParserTests.PieParser_Parse_ShowDataAndTitle_AreRead`,
`PieParserTests.PieParser_Parse_AccessibilityTexts_AreConverted`,
`PieParserTests.PieParser_ConvertString_Escapes_FollowLangium`,
`PieParserTests.PieParser_ConvertMultiLine_Lines_AreTrimmedAndJoined`,
`PieParserTests.PieParser_Parse_Numbers_FollowTheTerminals`

Asserts that the title `A   pie`, with spaces around it and before a comment, is `A pie`; that the accessible title
`The   title`, with spaces around it, is `The title` and `accDescr: one   two` is `one two`; that an accessible
description in braces with the indented lines `first   line` (with trailing spaces) and `second line` between two
blank lines is `first line` and `second line` joined by a line feed, with no accessible title; that the string
`"a\nb\tc\q\\d\"e\0"` converts to `a`, a line feed, `b`, a tab, `cq\d"e`, and a null character (the known escapes
replaced, any other escaped character kept without its backslash); that the braced texts (a theory with four cases)
convert as Mermaid's expressions do: a line feed, `x` indented by two spaces, a line feed, and two spaces to `x`; `a`
with two spaces around it, a blank line, `b` indented by two spaces, two tabs, `c` with two trailing spaces, and a line
feed to `a` and `b c` joined by a line feed; `one line` unchanged; and `a`, two carriage return and line feed pairs,
and `b` to `a`, a carriage return, and `b`; that the numbers are read as JavaScript reads them, `-0` as a negative
zero; and that every upstream vector gives Mermaid's converted texts and values.

##### CanvasNetMermaid-Pie-PieParser-Database: Sections As Mermaid's Database Keeps Them

**Tests**: `PieVectorTests.PieParser_Parse_UpstreamVector_MatchesMermaid`,
`PieParserTests.PieParser_Parse_Sections_KeepsOrderLabelsAndValues`,
`PieParserTests.PieParser_Parse_RepeatedLabel_FirstValueWins`,
`PieParserTests.PieParser_Parse_NegativeValue_FailsLocatedWithMermaidsMessage`

Asserts that the sections are kept in the order they are written; that the sections `"x" : 5`, `"y" : 1`, and
`"x" : 7` give only `x` with 5 and `y` with 1 (the first value of a repeated label wins); that a negative value on a
repeated label (`"x" : -7.5`, indented by two spaces, after `"x" : 5`) is refused with `mermaid-syntax-error` at line
3, column 3 (the section), with Mermaid's message `"x" has invalid value: -7.5. Negative values are not allowed in pie
charts. All slice values must be >= 0.`; and that the 5 negative values among the upstream vectors fail with Mermaid's
message and the sanitized titles and accessibility texts of the valid vectors are Mermaid's.

##### CanvasNetMermaid-Pie-PieParser-Metadata: Counts, Title, And Flag

**Tests**: `PieParserTests.PieParser_Parse_Sections_KeepsOrderLabelsAndValues`,
`PieParserTests.PieParser_Parse_ShowDataAndTitle_AreRead`,
`PieParserTests.PieParser_Parse_AccessibilityTexts_AreConverted`

Asserts that three sections give a node count of 3, an edge count of 0, a subgraph count of 0, no direction, and no
`showData`; that `pie showData title   A   pie` gives the flag `showData`, the title `A pie`, and the legend text
`a [1]` for the section `"a" : 1`; and that `accTitle:   The   title` gives the accessible title `The title`, while a
chart without an `accTitle` statement has none.

##### CanvasNetMermaid-Pie-PieParser-Errors: Errors With Chevrotain's Expected Tokens

**Tests**: `PieParserTests.PieParser_Parse_GrammarError_IsLocatedWithTheExpectedTokens`,
`PieParserTests.PieParser_Parse_LexicalError_IsLocatedAtTheFirstUnmatchedCharacter`,
`PieParserTests.PieParser_Parse_Numbers_FollowTheTerminals`,
`PieParserTests.PieParser_Parse_Directives_EndAtTheFirstClosingOrAreComments`,
`PieVectorTests.PieParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that five grammar errors (a theory) are `mermaid-syntax-error` at the refused token with exactly the expected
tokens chevrotain lists and a hint: `"a" 1` at line 2, column 5 with `:`; `"a" :` at line 2, column 6 with
`NUMBER_PIE`; `"a" : 1 "b" : 2` at line 2, column 9 with `NEWLINE` and `EOF`; `: 5` at line 2, column 1 with `EOF`; and
`pie showData showData` at line 1, column 14 with `EOF`. Asserts that four lexical errors (a theory) are located at the
first character no token matches, with the message `unrecognized text` and no expected tokens: the exponent of
`1e3` at line 2, column 8; the leading dot of `.5` at line 2, column 7; `showdata`, which is no keyword, at line 1,
column 5; and the unquoted label `label` at line 2, column 1. Asserts that `007` fails with `NEWLINE` and `EOF`
expected; that a directive `%%{` ends at the first closing `}%%` followed by a line end or white space, past a
closing followed by another character on the next line, so the section after it is read; that 6,000 lines of `%%{`
that no closing ends are comments, and the 6,000 closings `}%%x` after them fail with `unrecognized text` at line
6,002, column 1; and that the 34 lexical and grammar errors among the upstream vectors are located at Mermaid's first
error, the grammar errors with Mermaid's expected tokens.

##### CanvasNetMermaid-Pie-PieParser-Limits: Section And Label Limits

**Tests**: `PieParserTests.PieParser_Parse_TooManySections_IsALocatedLimit`,
`PieParserTests.PieParser_Parse_SanitizedTitleTooLong_IsALocatedLimit`

Asserts that with `MaxNodes` of 3 the three section statements `a`, `a`, and `b` are accepted (a node count of 2, the
repeated label once), while a fourth section statement `c` is `mermaid-limit-exceeded` at line 5, its statement,
because every section statement counts, repeated labels included; and that with `MaxLabelLength` of 60 a title of 30
`<` characters, which the sanitizer writes as 120 characters, is `mermaid-limit-exceeded` with a message that names
`MermaidLimits.MaxLabelLength`, because the sanitized text is checked.
