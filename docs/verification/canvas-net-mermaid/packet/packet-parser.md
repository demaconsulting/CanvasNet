### PacketParser Unit Verification Design

This document describes the unit-level verification strategy for the `PacketParser` unit in the
`DemaConsulting.CanvasNet.Mermaid.Packet` namespace: the internal sealed `PacketParser` (the lexer and the grammar, in
two partial files), the token `PacketToken`, the block records `ParsedBlock` and `PacketBlock`, and the diagram
`PacketDiagram` with its row split.

#### Verification Approach

The unit is verified in two ways. `PacketVectorTests` parses the 162 cases of
`Fixtures/upstream-vectors/packet-parser.json`, recorded from the pinned Mermaid release in headless Chrome by
`tools/visual-diff/mermaid/gen-diagram-vectors.mjs --type packet` from the texts of `packet-vector-cases.mjs`: the
inputs of Mermaid's packet spec (`diagrams/packet/packet.spec.ts`, 15 cases) and of the packet tests of
`@mermaid-js/parser` (`tests/packet.test.ts`, 8 cases), original texts that cover the keywords and their suffix, the
integer terminal, every database error, the titles and accessibility texts, the strings, the line endings, the
directives, and the row split under 16 site configurations (row sizes of 1, 4, 7, 8, 16, 64, 1.5, 0, and -3, the bit
numbers turned off, and Mermaid's cap of 10,000 rows), and the 13 packet fixtures. Each text goes through Mermaid's
preprocessing and detection, as a diagram does, and its rows are split with the case's site configuration. For each of
the 102 valid texts the facts of Mermaid's packet database (the type, the title, the accessible title and description,
the effective packet configuration, the number of rows, and every row with the start, end, bit count, and label of each
part, the first and last 50 rows of a diagram of more than 100) must equal the facts of the parsed diagram and its
rows. For each of the 60 invalid texts the error must be the first one Mermaid reports: for the 22 database errors a
`mermaid-syntax-error` whose message holds Mermaid's message; for the 2 split errors a located `mermaid-render-failed`
whose message holds Mermaid's message; for the 17 lexical errors a located `unrecognized text` error at the line and
column of Mermaid's first lexer error; and for the 19 grammar errors a located error at the line and column of
Mermaid's first parser error with exactly the expected tokens chevrotain lists. `PacketParserTests` verifies the block
forms, the keywords, the integer terminal, the grammar and database errors, the titles, the labels, the limits, and the
row split on short hand-written texts, asserting the blocks, the texts, the metadata, the rows, the expected tokens, the
locations (line and column in the original text), the messages, and the hints.

No mocking or stubbing is used; the preprocessor, the detector, the configuration resolver, and the sanitizer are real
(each test preprocesses and detects its text, then calls `Parse`, as the document does); the row split is given a work
meter of its own. The internal types are reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `PacketParserTests.cs` and `PacketVectorTests.cs` (with the shared helper `UpstreamVectors.cs`)
within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the `Packet` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: `Fixtures/upstream-vectors/packet-parser.json`, generated from the pinned Mermaid by
  `tools/visual-diff/mermaid/gen-diagram-vectors.mjs --type packet` from
  `tools/visual-diff/mermaid/packet-vector-cases.mjs`
- **Isolation**: Each test parses its own text; the fixture is read only

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any database fact, row, expected token, location, message, or error that differs from the recorded or
asserted Mermaid value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Packet-PacketParser-Tokens: Tokens As Mermaid's Lexer Makes Them

**Tests**: `PacketParserTests.PacketParser_Parse_Keywords_FollowTheTokenBuilder`,
`PacketParserTests.PacketParser_Parse_IntTerminal_BacktracksAsTheRegularExpression`,
`PacketVectorTests.PacketParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that `packet%% c` and `packet-beta%% c` are both read as the keyword before a comment, and `packet 0-3: "a"`
with the block on the keyword's line; that `packets` is refused at line 1, column 1 (the keyword needs white space, a
comment, or the end after it), while `packet-betas` is read as `packet-beta` and refused at line 1, column 12, where no
token matches the `s`; that the integer terminal (a theory with five cases) gives back its digits as the regular
expression does: `05` and `00` are the number `0` followed by another number, refused at line 2, column 2 with `:`
expected, while in `0-10.5`, `0-1.0`, and `0.5` the digits before the dot give back their last digit, so the first
character no token matches is refused with `unrecognized text` at line 2, columns 5, 3, and 2; and that every upstream
vector gives Mermaid's facts or Mermaid's first error at its place.

##### CanvasNetMermaid-Packet-PacketParser-Grammar: The Statements Of Mermaid's Grammar

**Tests**: `PacketVectorTests.PacketParser_Parse_UpstreamVector_MatchesMermaid`,
`PacketVectorTests.PacketParser_Vectors_FileHasEnoughCases_AtLeast120`,
`PacketParserTests.PacketParser_Parse_BlockForms_FillStartEndAndBits`,
`PacketParserTests.PacketParser_Parse_Keywords_FollowTheTokenBuilder`

Asserts that every upstream vector (a theory with one case for each) gives Mermaid's database facts and rows or
Mermaid's error, and that the file holds at least 120 cases, at least 20 of them from the upstream specs, with between
30 and all but 80 errors; that a range, a single bit in single quotes, a bit count, and a spaced range (`13 - 13 :`) are
all read, in order, at lines 2 to 5; and that a block can follow the keyword on its line.

##### CanvasNetMermaid-Packet-PacketParser-Values: Values As Mermaid's Converters Give Them

**Tests**: `PacketParserTests.PacketParser_Parse_TitleAccessibilityAndMetadata_AreRead`,
`PacketParserTests.PacketParser_Parse_Labels_FollowConvertString`,
`PacketVectorTests.PacketParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that of two titles, the one on the keyword's line and an indented `title   A   <b>B</b>  <script>x</script>`,
the last wins, with its runs of spaces made one space and sanitized as Mermaid's common database sanitizes it to
`A <b>B</b>` and a space; that an accessible title `T` with spaces around it is `T`; that an accessible description in
braces with the indented lines `one` and `two` is `one` and `two` joined by a line feed; that the labels `"a\nb\tc\q"`,
`'it\'s <b>x</b>'`, a
double-quoted label over two lines, and an empty label convert to `a`, a line feed, `b`, a tab, `cq`, to
`it's <b>x</b>` (not sanitized), to `first`, a line feed, and `second`, and to an empty label; and that every upstream
vector gives Mermaid's converted texts and numbers.

##### CanvasNetMermaid-Packet-PacketParser-Database: Blocks As Mermaid's Database Keeps Them

**Tests**: `PacketParserTests.PacketParser_Parse_BlockForms_FillStartEndAndBits`,
`PacketParserTests.PacketParser_Parse_InvalidBlock_FailsLocatedWithMermaidsMessage`,
`PacketParserTests.PacketParser_Parse_TitleAccessibilityAndMetadata_AreRead`,
`PacketVectorTests.PacketParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that `0-3`, `4`, `+8`, and `13 - 13` give the blocks 0 to 3 of 4 bits, 4 to 4 of 1 bit, 5 to 12 of 8 bits, and
13 to 13 of 1 bit; that seven invalid blocks (a theory) are refused with `mermaid-syntax-error` at their statement,
column 1, with Mermaid's message and a hint: `9-6` (`Packet block 9 - 6 is invalid. End must be greater than start.`),
`18-20` after `0-16` (`Packet block 18 - 20 is not contiguous. It should start from 17.`), the single bit `18` after
`0-16` (`Packet block 18 - 18 ...`), a first block `1-3` (`... It should start from 0.`), a repeated `0` (`Packet block
0 - 0 is not contiguous. It should start from 1.`), `+0` after `0-7` (`Packet block 8 is invalid. Cannot have a zero
bit field.`), and a 25-digit bit written as JavaScript writes it (`Packet block 1.2345678901234568e+24 - ...`); and that
the 22 database errors among the upstream vectors fail with Mermaid's message and the sanitized titles and
accessibility texts of the valid vectors are Mermaid's.

##### CanvasNetMermaid-Packet-PacketParser-Rows: Rows As Mermaid Splits Them

**Tests**: `PacketParserTests.PacketDiagram_Rows_SplitBlocksAsMermaid`,
`PacketParserTests.PacketDiagram_Rows_OtherRowSizes_SplitAsMermaid`,
`PacketParserTests.PacketDiagram_Rows_PartStartAfterEnd_IsALocatedRenderFailure`,
`PacketParserTests.PacketDiagram_Rows_TooManyParts_IsALocatedLimit`,
`PacketParserTests.PacketDiagram_Rows_ChargesOnePerBlockAndPart`,
`PacketVectorTests.PacketParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that the blocks `0-10`, `11-90`, `91-95`, and `96` give, at 32 bits per row, the rows `0-10` and `11-31` (bit
count 20), `32-63` (31), `64-90` (26) and `91-95`, and `96`, the parts of `11-90` keeping its label and its statement's
location (Mermaid's bit counts of a cut part are one less than its bits); that at 1 bit per row each bit is a row, at 0
bits per row the split gives Mermaid's 10,000 rows of the part `0` to `-1`, at a negative size it gives no row, and the
range `0-99999999` stops at 10,000 rows, the last from 319,968 to 319,999; that at 1.5 bits per row the blocks `0`,
`1`, and `2-3` fail with `mermaid-render-failed` at line 3 and Mermaid's message `Block start 1.5 is greater than block
end 1.`; that with a `MaxNodes` of 10 the range `1-99999999` fails with `mermaid-limit-exceeded` at line 3, naming
`MaxNodes`; that the split of `0-40` and `41` charges 5 units (one for each of the two blocks and each of the three
parts) and fails with `mermaid-limit-exceeded` on a meter of 4; and that the rows of every upstream vector are Mermaid's.

##### CanvasNetMermaid-Packet-PacketParser-Metadata: Counts And Titles

**Tests**: `PacketParserTests.PacketParser_Parse_TitleAccessibilityAndMetadata_AreRead`,
`PacketParserTests.PacketParser_Parse_FrontmatterTitle_IsTheDrawnTitleFallback`,
`PacketParserTests.PacketParser_Parse_TitleSanitizedToEmpty_ReplacesTheFrontmatterTitle`

Asserts that two blocks give a node count of 2, an edge count of 0, a subgraph count of 0, and no direction, with the
title and the accessible title of their statements; that a diagram without a `title` statement has no title of its own
and draws the sanitized frontmatter title `From <i>front</i>`; that a diagram with its own title draws it instead of
the frontmatter title; and that a `title` statement that sanitizes to empty (`<script>x</script>`) replaces the
frontmatter title `FM`, so no title is drawn, while a bare `title` keeps `FM`, as in Mermaid.

##### CanvasNetMermaid-Packet-PacketParser-Errors: Errors With Chevrotain's Expected Tokens

**Tests**: `PacketParserTests.PacketParser_Parse_GrammarError_IsLocatedWithTheExpectedTokens`,
`PacketParserTests.PacketParser_Parse_IntTerminal_BacktracksAsTheRegularExpression`,
`PacketVectorTests.PacketParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that seven grammar errors (a theory) are `mermaid-syntax-error` at the refused token with exactly the expected
tokens chevrotain lists and a hint: `0 "a"` at line 2, column 3 with `:`; `0:` at line 2, column 3 with `STRING`;
`0-: "a"` at line 2, column 3 with `INT`; `+-1: "a"` at line 2, column 2 with `INT`; two blocks on one line at line 2,
column 8 with `NEWLINE` and `EOF`; `: "a"` at line 2, column 1 with `EOF`; and a second `packet` keyword at line 2,
column 1 with `EOF`; that the lexical errors of the integer terminal are located at the first character no token
matches, with the message `unrecognized text`; and that the 36 lexical and grammar errors among the upstream vectors
are located at Mermaid's first error, the grammar errors with Mermaid's expected tokens.

##### CanvasNetMermaid-Packet-PacketParser-Limits: Block And Label Limits

**Tests**: `PacketParserTests.PacketParser_Parse_TooManyBlocks_IsALocatedLimit`,
`PacketParserTests.PacketParser_Parse_LongTexts_AreLocatedLimits`

Asserts that with a `MaxNodes` of 2 a third block statement is `mermaid-limit-exceeded` at line 4, its statement, with
a message that names `MaxNodes`; and that with a `MaxLabelLength` of 8 a label of 12 characters and a title of 12
characters are `mermaid-limit-exceeded` at line 2, their statements, and a frontmatter title of 12 characters (which
the diagram would draw) is `mermaid-limit-exceeded` at a known location in the frontmatter.
