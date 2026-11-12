### StateParser Unit Verification Design

This document describes the unit-level verification strategy for the `StateParser` unit in the
`DemaConsulting.CanvasNet.Mermaid.State` namespace: the internal sealed `StateParser` (the lexer and the grammar, in
two partial files) and the token names of `StateTokens`.

#### Verification Approach

The unit is verified in two ways. `StateVectorTests` parses the 157 cases of
`Fixtures/upstream-vectors/state-parser.json`, recorded from the pinned Mermaid release in headless Chrome by
`tools/visual-diff/mermaid/gen-diagram-vectors.mjs --type state` from the inputs of Mermaid's state parser specs
(97 cases), the original texts of `state-vector-cases.mjs` (46 cases), and the 14 state fixtures: for each of the 145
valid texts the facts of Mermaid's state diagram (the type, the direction of `getDirection`, the accessibility texts,
the translated statement tree `rootDoc`, the states, the transitions, the style classes, the links, and the direction,
nodes, and edges of its `getData()`) must equal the facts of the parsed model under the configuration resolved as
Mermaid resolves it with the case's site configuration (the URL of each link and click statement compared as the
model's link check gives it for Mermaid's recorded URL, since Mermaid keeps the URL as written, quotes included, and
the library keeps only the checked URL), and for each of the 12 invalid texts the error must be
Mermaid's: the same unexpected token and the same expected tokens in jison's order for a grammar error (7 cases), an
unrecognized-text error for a lexical error (1 case), and Mermaid's own message for an error Mermaid throws without a
token (4 cases: three single-word errors of the lexer and the group error of the database), always located. jison's
line of an error is not compared, because jison reports the line after the lookahead was read. `StateParserTests`
verifies the lexer quirks, the statements, the errors, and the limits on short hand-written texts, asserting the
statement tree, the states, the transitions, the expected tokens, the locations (line and column in the original
text), and the hints.

No mocking or stubbing is used; the preprocessor, the detector, the scanner, the configuration resolver, and the model
are real (the helper `StateFacts` preprocesses and detects a text, calls `Parse`, and writes the facts of a model as
the vector generator records them from Mermaid). The internal types are reached through the test assembly's
`InternalsVisibleTo` access.

Unit tests reside in `StateParserTests.cs` and `StateVectorTests.cs` (with the helper `StateFacts.cs`) within the
`DemaConsulting.CanvasNet.Mermaid.Tests` project, in the `State` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: `Fixtures/upstream-vectors/state-parser.json`, generated from the pinned Mermaid by
  `tools/visual-diff/mermaid/gen-diagram-vectors.mjs --type state`
- **Isolation**: Each test parses its own text; the fixture is read only

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any statement, database or layout fact, expected token, location, or error that differs from the recorded or
asserted Mermaid value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-State-StateParser-Tokens: Tokens As Mermaid's Lexer Makes Them

**Tests**: `StateVectorTests.StateParser_Parse_UpstreamVector_MatchesMermaid`,
`StateParserTests.StateParser_Parse_KeywordsInAnyCase_AreRecognized`,
`StateParserTests.StateParser_Parse_CommentsAndDirectionLines_FollowTheLexer`

Asserts that every upstream vector gives Mermaid's facts; that `STATE Comp {`, `Note Right Of Comp : remark`,
`ClassDef hot fill:#f00`, and `CLASS Comp hot` are read as keywords, so the state `Comp` has the note `remark` at the
position `Right Of`, kept in the case it is written in, and the CSS class `hot`, and the one style class is `hot`; and
that in `A --> B%%note` the inline comment ends the id `B`, while the line `C --> D %% direction LR is swallowed` is
one direction statement, so the states are `A`, `B`, and `E` and the direction is `LR`.

##### CanvasNetMermaid-State-StateParser-Grammar: The Statements Of Mermaid's Grammar, In Order

**Tests**: `StateVectorTests.StateParser_Parse_UpstreamVector_MatchesMermaid`,
`StateVectorTests.StateParser_Vectors_FileHasEnoughCases_AtLeast100`,
`StateParserTests.StateParser_Parse_Header_ReadsTheSameStatements`,
`StateParserTests.StateParser_Parse_IgnoredStatements_AreKeptAsText`,
`StateParserTests.StateParser_Parse_StateForms_GiveMermaidsStatements`,
`StateParserTests.StateParser_Parse_DividersAndDirections_AreReadPerComposite`,
`StateParserTests.StateParser_Parse_ClickInsideComposite_IsReadAsStates`

Asserts that every upstream vector (a theory with one case for each) gives Mermaid's statement tree, database, and
layout facts or Mermaid's error, and that the file holds at least 100 cases with between 10 and all but 90 errors;
that the headers `stateDiagram-v2` and `stateDiagram` (a theory) both give the Mermaid id `stateDiagram`, the states
`root_start`, `A`, and `B`, and the transitions `root_start` to `A` without a label and `A` to `B` labeled `go`; that
`hide empty description`, `scale 200 width`, `state Lone`, and the floating note `note "floating" as N1` are the first
four top-level statements, without a kind and with the texts `hide empty description`, `scale` (with the space after
it), `Lone`, and `note` (with the space after it), in that order, while the only state is `A`; that
`A : first: second` keeps the description `first: second`,
`state "Quoted" as B:more` gives the id `B` with the two descriptions `Quoted` and `more`, `state f <<fork>>`,
`state j [[join]]`, and `state c <<CHOICE>>` give the states `f`, `j`, and `c` of the types `fork`, `join`, and
`choice`, and `C:::warm` keeps the class `warm`; that in a composite state `Outer` holding a composite state `Inner`
with a divider and a divider of its own, the dividers are numbered in parse order (the inner one `divider-id-1`, the
outer one `divider-id-2`), so `Outer` holds the regions `divider-id-2` and `id-random-1` and `Inner` the regions
`divider-id-1` and `id-random-2`, and the direction statements go with their regions, leaving `Outer` the direction
`TB` and giving the region `id-random-1` the direction `BT` of its last `direction` statement; and that
`click A "u" "t"` inside a composite state `C` records no link and gives the nodes `C`, `A`, `click`, `"u"`, and
`"t"`, all but `C` inside `C`, as Mermaid's lexer reads it.

##### CanvasNetMermaid-State-StateParser-Errors: Errors With Jison's Expected Tokens

**Tests**: `StateParserTests.StateParser_Parse_MissingTarget_ThrowsLocatedErrorWithExpectedTokens`,
`StateParserTests.StateParser_Parse_DashOutsideComposite_IsAnInvalidToken`,
`StateParserTests.StateParser_Parse_UnclosedNote_IsALexicalError`,
`StateParserTests.StateParser_Parse_TwoWordCompositeName_ThrowsMermaidsMessage`,
`StateParserTests.StateParser_Parse_UnclosedComposite_FailsAtTheEnd`,
`StateVectorTests.StateParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that `A -->` without a target is `mermaid-syntax-error` at line 2 of the named file, found `NL`, with exactly
`ID` and `EDGE_STATE` expected in jison's order; that a `--` divider at the top level (line 3) and the id `first-state`
(line 2) are both `found 'INVALID' ('-')` at their lines, with a hint that names `'--'` (a theory with two cases);
that a multi-line note without `end note` is a lexical error ("unrecognized text") at line 3 with a hint that names
`end note`; that `state Two Words {` is `mermaid-syntax-error` with Mermaid's message
`Error: State name must be a single word. Found: "Two Words {"`, located at the name (line 3, column 7), with no
expected tokens; that a composite state without its `}` fails at the end of the text with `STRUCT_STOP` among the
expected tokens and a hint that names `'}'`; and that the 12 invalid upstream vectors fail as Mermaid fails.

##### CanvasNetMermaid-State-StateParser-LinearTime: Long Lines And Texts Without Recursion

**Tests**: `StateParserTests.StateParser_Parse_LongLinesAndManyStatements_AreReadIteratively`

Asserts that a `state` line of 7,000 words, followed by the state `A`, is read without recursion and without scanning
the line again for every token into 7,001 top-level statements with `A` the only state; and that, with `MaxEdges`
raised to 2,000, a text of 1,500 labeled transitions in a chain is read without recursion into 1,500 transitions and
1,501 nodes. The matchers do no backtracking and the line searches are cached per line, so the test runs in linear
time (no wall-clock assertion).

##### CanvasNetMermaid-State-StateParser-Limits: Nesting And Label Limits

**Tests**: `StateParserTests.StateParser_Parse_CompositesTooDeep_ThrowsLimitExceeded`,
`StateParserTests.StateParser_Parse_LongTextAnywhere_ThrowsLimitExceeded`

Asserts that with `MaxNestingDepth` of 2 two nested composite states are accepted with three nodes, while a third
nested composite state is `mermaid-limit-exceeded` at its brace (line 4, column 13 of the named file) with a message
that names `MermaidLimits.MaxNestingDepth`; and that with `MaxLabelLength` of 10 a text of 11 characters is
`mermaid-limit-exceeded`, located at its line, with a message that names `MermaidLimits.MaxLabelLength`, wherever it
stands (a theory with 28 cases): a state id, a description, a transition label, the second state of a transition, a
`:::` class, the quoted description and the id after `as`, a composite state name, a fork id and a choice id, a note
text, the state id of a note, the text of a multi-line note, the text and the id of a floating note, a `classDef` name
and its styles, the styles and the ids of a `style` statement, the class and the ids of a `class` statement, the URL
and the tooltip of a click and the URL of `click ... href`, an accessible title, a one-line and a multi-line accessible
description, and the number of a `scale` statement.
