### SequenceParser Unit Verification Design

This document describes the unit-level verification strategy for the `SequenceParser` unit in the
`DemaConsulting.CanvasNet.Mermaid.Sequence` namespace: the internal sealed `SequenceParser` (the lexer, the grammar,
and the statements, in three partial files) and the token names of `SequenceTokens`.

#### Verification Approach

The unit is verified in two ways. `SequenceVectorTests` parses the 242 cases of
`Fixtures/upstream-vectors/sequence-parser.json`, recorded from the pinned Mermaid release in headless Chrome by
`benchmarks/visual-diff/mermaid/gen-sequence-vectors.mjs` from the original texts of `sequence-vector-cases.mjs`: for
each of the 207 valid texts the facts of Mermaid's sequence database (participants in order with their types,
descriptions, wrap settings, boxes, links, and properties; messages with their line types, texts, wrap settings,
activation and central-connection flags, placements, and numbering; boxes with their fills; created and destroyed
participants; the title and accessibility texts) must equal the facts of the parsed model under the configuration
resolved as Mermaid resolves it,
and for each of the 35 invalid texts the error must be Mermaid's: the same unexpected token and the same expected
tokens in jison's order for a grammar error, an unrecognized-text error for a lexical error, Mermaid's own message for
a database error, and `mermaid-yaml-invalid` for invalid participant metadata, always located. `SequenceParserTests`
verifies the lexer quirks, the statements, the errors, and the limits on short hand-written texts, asserting the
participants, the messages, the line types, the expected tokens, the locations (line and column in the original
text), and the hints.

No mocking or stubbing is used; the preprocessor, the detector, the scanner, the YAML reader, and the model are
real (the helper `SequenceFacts` preprocesses and detects a text and calls `Parse`). The internal types are reached
through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `SequenceParserTests.cs` and `SequenceVectorTests.cs` (with the helper `SequenceFacts.cs`)
within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the `Sequence` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: `Fixtures/upstream-vectors/sequence-parser.json`, generated from the pinned Mermaid by
  `benchmarks/visual-diff/mermaid/gen-sequence-vectors.mjs`
- **Isolation**: Each test parses its own text; the fixture is read only

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any database fact, expected token, location, or error that differs from the recorded or asserted Mermaid
value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Sequence-SequenceParser-Tokens: Tokens As Mermaid's Lexer Makes Them

**Tests**: `SequenceParserTests.SequenceParser_Parse_ParticipantNames_FollowMermaidLexer`,
`SequenceParserTests.SequenceParser_Parse_Arrow_MapsToLineType`,
`SequenceParserTests.SequenceParser_Parse_KeywordAsName_FailsAtTheEndLikeMermaid`,
`SequenceVectorTests.SequenceParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that the names `Back Office as BO` and `Alpha # the first` (a spaced name keeps its alias text and its
comment), `Front-End`, and `Big Server` are the participants Mermaid's lexer makes; that each of the 26 arrows, written
with and without spaces around it, maps to its line type between `Left` and `Right` (a theory with one case for each
arrow); that `Loop->>Loop: x` starts a `loop` block and fails at the end of the text with `end` expected and a hint
that names `'end'`; and that every upstream vector gives Mermaid's facts.

##### CanvasNetMermaid-Sequence-SequenceParser-Grammar: The Statements Of Mermaid's Grammar, In Order

**Tests**: `SequenceVectorTests.SequenceParser_Parse_UpstreamVector_MatchesMermaid`,
`SequenceVectorTests.SequenceParser_Vectors_FileHasEnoughCases_AtLeast150`,
`SequenceParserTests.SequenceParser_Parse_Signal_AddsParticipantsAndMessage`,
`SequenceParserTests.SequenceParser_Parse_NotesActivationsAndCentralConnections_ListMessagesInOrder`,
`SequenceParserTests.SequenceParser_Parse_Blocks_ListMarkersInOrder`,
`SequenceParserTests.SequenceParser_Parse_Autonumber_KeepsStartStepAndVisibility`

Asserts that every upstream vector (a theory with one case for each) gives Mermaid's database facts or Mermaid's
error, and that the file holds at least 150 cases with between 20 and all but 100 errors; that two signals add the
participants `Client` and `Server` and the messages with their texts, line types, and ids `0` and `1`; that a note over
two participants, the `+` and `-` shorthands, `activate`, `deactivate`, and a central connection at both ends list the
note, the messages, the activation starts and ends, and the central connections in Mermaid's order, with the
activation flag and the dual central connection on the message; that `alt` with `else`, `par_over` with `and`,
`critical` with `option`, and `rect` list their start, section, and end markers with their titles in order; and that
`autonumber` alone, with a start, with a decimal start and step, and `off` keep the start, the step, and the
visibility as JavaScript numbers.

##### CanvasNetMermaid-Sequence-SequenceParser-LinearTime: Long Inputs Without Backtracking

**Tests**: `SequenceParserTests.SequenceParser_Parse_LongDashedNamesAndManyStatements_Parses`

Asserts that a participant name of 50,001 dash-separated parts and 5,000 more messages among 50 participants parse
into the expected name, 5,001 messages, and 52 participants; the matchers do no backtracking, so the test runs in
linear time (no wall-clock assertion).

##### CanvasNetMermaid-Sequence-SequenceParser-Errors: Errors With Jison's Expected Tokens

**Tests**: `SequenceParserTests.SequenceParser_Parse_MissingText_ThrowsLocatedErrorWithExpectedTokens`,
`SequenceParserTests.SequenceParser_Parse_TokenAfterStatement_ListsMergedFollowers`,
`SequenceParserTests.SequenceParser_Parse_MetadataAfterActivatedName_ListsNameFollowers`,
`SequenceParserTests.SequenceParser_Parse_StrayEnd_ListsLineStartsOnly`,
`SequenceParserTests.SequenceParser_Parse_MissingParticipantName_ThrowsLexicalError`,
`SequenceParserTests.SequenceParser_Parse_GrammarAndDatabaseErrors_GrammarErrorFirst`,
`SequenceVectorTests.SequenceParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that a signal without a text is `mermaid-syntax-error` at line 2, column 17 of the named file, found
`NEWLINE`, with exactly `TXT` expected and a hint; that a comma after a whole statement lists the 34 tokens of jison's
merged followers (with `break`, `option`, `and`, `else`, and `participant` at positions 22 to 26); that metadata after
the name of an `activate` statement fails at `CONFIG_START` with the merged followers of a name, from `NEWLINE`, `,`,
and `()` to `TXT`; that a stray `end` lists the 30 line starts without `end`; that `participant` without a name is a
lexical error ("unrecognized text") at line 2, column 16 with a hint that names `participant`; that a grammar error on
a later line comes before the database error of an earlier deactivation; and that the 35 invalid upstream vectors fail
as Mermaid fails.

##### CanvasNetMermaid-Sequence-SequenceParser-ParticipantMetadata: Participants, Aliases, Metadata, And Create

**Tests**: `SequenceParserTests.SequenceParser_Parse_ParticipantStatements_SetTypesAliasesAndMetadata`,
`SequenceParserTests.SequenceParser_Parse_InvalidMetadata_ThrowsLocatedYamlError`

Asserts that `actor User as wrap:The user` gives the type `actor`, the description `The user`, and the wrap setting;
that metadata with a `type` and an `alias` gives the type `database` and the description `Data`; that a `create`d
participant is a `participant` recorded as created at message 1; and that metadata that is not valid YAML is
`mermaid-yaml-invalid` located at line 2 inside the metadata.

##### CanvasNetMermaid-Sequence-SequenceParser-InteractionStatements: Links, Link, Properties, And Details

**Tests**: `SequenceVectorTests.SequenceParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that the vectors with `links`, `link`, `properties`, and `details` statements give the links and properties
that Mermaid's database records for them (see also the SequenceModel unit verification design).

##### CanvasNetMermaid-Sequence-SequenceParser-TitleAndAccessibility: Title And Accessibility Texts

**Tests**: `SequenceParserTests.SequenceParser_Parse_TitleAndAccessibility_SetTexts`

Asserts that `title Before # after` gives the title `Before` (cut at the comment, trailing space kept), that an
accessible title with spaces around it is trimmed to `Title`, and that a multi-line `accDescr { ... }` gives `One` and
`Two` on two lines.

##### CanvasNetMermaid-Sequence-SequenceParser-Limits: Nesting And Label Limits

**Tests**: `SequenceParserTests.SequenceParser_Parse_NestingDeeperThanLimit_ThrowsLimitExceeded`,
`SequenceParserTests.SequenceParser_Parse_LongText_ThrowsLimitExceeded`

Asserts that with `MaxNestingDepth` of 3 a box inside two blocks (depth 3) is accepted and a fourth nested block is
`mermaid-limit-exceeded` at line 11, column 1, the block that is too deep; and that with `MaxLabelLength` of 10 a
longer message text is `mermaid-limit-exceeded` at line 2, column 10.
