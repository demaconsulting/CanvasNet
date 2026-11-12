### FlowchartParser Unit Verification Design

This document describes the unit-level verification strategy for the `FlowchartParser` unit in the
`DemaConsulting.CanvasNet.Mermaid.Flowchart` namespace: the internal sealed `FlowchartParser` (`Parse` and
`TrimSpaceAfterBraces`, in four partial files) and the token names of `FlowchartTokens`. The unit reads the
preprocessed text of a flowchart into a `FlowchartModel` as Mermaid's generated flowchart parser reads it.

#### Verification Approach

The unit is verified in two ways. **Upstream vectors**: `FlowchartVectorTests` runs every case of
`Fixtures/upstream-vectors/flowchart-parser.json` (207 cases recorded from the pinned Mermaid release in a browser by
the vector generator of the tools folder): for the 171 valid texts the facts of the parsed model (vertices, edges,
subgraphs, classes, defaults, direction, accessibility texts) and its layout input, written by the test helper
`FlowchartFacts` with the keys and omissions of `JSON.stringify`, must equal Mermaid's database facts and layout data
exactly; for the 36 invalid texts the open must fail where Mermaid fails, with the same unexpected token and the
identical expected-token list in jison's order for the grammar errors. The cases cover every header and direction
form, every bracket shape, every shape name and alias, every link form, node chains with shape data, subgraphs,
styling statements, every `click` form, shape data with YAML, accessibility, labels, line endings, and the quirks of
the lexer. **Focused unit tests**: `FlowchartParserTests` parses hand-written texts and asserts the model, or the code,
location, message, expected tokens, and hint of the exception, at exact values.

The tests call `Parse` after the real preprocessor and detector of the `Syntax` subsystem, so locations are checked in
the original text; no mocking or stubbing is used. The internal types are reached through the test assembly's
`InternalsVisibleTo` access.

Unit tests reside in `FlowchartParserTests.cs` and `FlowchartVectorTests.cs` (with the helper `FlowchartFacts.cs`)
within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the `Flowchart` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: `test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/upstream-vectors/flowchart-parser.json`
- **Isolation**: Each test parses its own text; the vector file is only read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any vector whose facts differ from Mermaid's in any field, any invalid vector that opens or fails with other
expected tokens, and any exception with another code, location, or expected-token list than asserted constitutes a
failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Flowchart-FlowchartParser-Grammar: The Grammar Of Mermaid's Flowchart Parser

**Tests**: `FlowchartVectorTests.FlowchartParser_Parse_UpstreamVector_MatchesMermaid`,
`FlowchartVectorTests.FlowchartParser_Vectors_FileHasEnoughCases_AtLeast150`,
`FlowchartParserTests.FlowchartParser_Parse_LinkChain_AddsVerticesAndEdges`

Asserts that every valid vector gives exactly Mermaid's database facts and layout data and every invalid vector fails
where Mermaid fails, that the vector file holds at least 150 cases with both valid and invalid texts, and that a
statement with a chain of links declares the vertices in order with their bracket shapes and an edge between each pair
of consecutive nodes with its text.

##### CanvasNetMermaid-Flowchart-FlowchartParser-Tokens: The Tokens Of Mermaid's Flowchart Lexer

**Tests**: `FlowchartParserTests.FlowchartParser_Parse_KeywordPrefixes_AreIds`,
`FlowchartParserTests.FlowchartParser_Parse_DirectionAnywhereOnLine_IsDirectionStatement`,
`FlowchartParserTests.FlowchartParser_TrimSpaceAfterBraces_TrailingSpaces_AreRemovedWithLocations`,
`FlowchartVectorTests.FlowchartParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that `styled`, `subgraphs`, `endpoint`, `classy`, and `default` are vertex ids, that a line with
`direction LR` after a link inside a subgraph is the subgraph's direction statement (and its nodes are not declared),
that the white space after a closing brace is removed up to the last line feed while the next line keeps its location,
and that the lexer quirks of the vectors give Mermaid's facts.

##### CanvasNetMermaid-Flowchart-FlowchartParser-LinearTime: Long Lines Are Read

**Tests**: `FlowchartParserTests.FlowchartParser_Parse_LongLines_Parse`

Asserts that a chain of 400 nodes gives 399 edges and, with a line of 400 `direction` words joined by `&`, 401
vertices, and that a line of 400 words with an at sign fails with a message that names `LINK_ID`, as Mermaid's lexer
reads such a line; the caches of the lexer keep such lines linear.

##### CanvasNetMermaid-Flowchart-FlowchartParser-SyntaxErrors: Located Errors With Mermaid's Expected Tokens

**Tests**: `FlowchartParserTests.FlowchartParser_Parse_WrongCloser_ThrowsLocatedErrorWithExpectedTokens`,
`FlowchartParserTests.FlowchartParser_Parse_UnclosedLabel_ListsEveryCloser`,
`FlowchartParserTests.FlowchartParser_Parse_UnknownDirection_ThrowsLexicalError`,
`FlowchartParserTests.FlowchartParser_Parse_TwoNodesOnALine_ListsSeparators`,
`FlowchartParserTests.FlowchartParser_Parse_UnclosedSubgraph_AsksForEnd`,
`FlowchartVectorTests.FlowchartParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that a `]` closing a `[[` label fails with `mermaid-syntax-error` at its line and column in the named file,
names the token found, lists exactly `SUBROUTINEEND`, `TAGEND`, `UNICODE_TEXT`, `TEXT`, and `TAGSTART`, and has a
hint; that a label that never closes lists the closers of every shape in jison's order and finds the end of the text;
that an unknown direction is an unrecognized-text error at its column; that two nodes on a line list the separators
and the continuations of a statement; that an unclosed subgraph lists `end` and its hint asks for `end`; and that the
26 grammar errors of the vectors give jison's lists.

##### CanvasNetMermaid-Flowchart-FlowchartParser-DatabaseErrors: What Mermaid's Database Rejects

**Tests**: `FlowchartParserTests.FlowchartParser_Parse_AnonymousSubgraph_ThrowsAtKeyword`,
`FlowchartParserTests.FlowchartParser_Parse_UnknownShape_HintsNearestName`,
`FlowchartParserTests.FlowchartParser_Parse_UpperCaseShape_ExplainsLowerCaseRule`,
`FlowchartVectorTests.FlowchartParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that a subgraph without id or title fails at its keyword, that the shape `rectangles` fails with
`mermaid-shape-unknown` at its value with `rectangle` as the hint, that the shape `Circle` fails with the lower-case
rule in the message and `circle` as the hint, and that the database errors of the vectors (shape names, invalid YAML,
`linkStyle` indexes, a nameless subgraph) fail where Mermaid fails.

##### CanvasNetMermaid-Flowchart-FlowchartParser-Limits: The Limits Of The Open Call

**Tests**: `FlowchartParserTests.FlowchartParser_Parse_NestingDeeperThanLimit_ThrowsLimitExceeded`,
`FlowchartParserTests.FlowchartParser_Parse_MoreEdgesThanLimit_ThrowsLimitExceeded`,
`FlowchartParserTests.FlowchartParser_Parse_MoreNodesThanLimit_ThrowsLimitExceeded`,
`FlowchartParserTests.FlowchartParser_Parse_LabelLongerThanLimit_ThrowsLimitExceeded`

Asserts `mermaid-limit-exceeded` with the name of the limit in the message: at the third nested subgraph's keyword
with `MaxNestingDepth` 2 (while 3 accepts the text), at the third edge's line with `MaxEdges` 2, at the fourth vertex or
subgraph with `MaxNodes` 3 (the subgraph counted), and, with `MaxLabelLength` 10, at the line of an 11-character
square label, link text before an arrow, link text between pipes, subgraph title, shape-data label, accessible title,
and multi-line accessible description.

##### CanvasNetMermaid-Flowchart-FlowchartParser-ClickAndAccessibility: Click And Accessibility Statements

**Tests**: `FlowchartParserTests.FlowchartParser_Parse_ClickStatements_OnlyAddClickableClass`,
`FlowchartParserTests.FlowchartParser_Parse_AccessibilityStatements_SetTitleAndDescription`

Asserts that a callback with a tooltip, a `call` with arguments, an `href` with a tooltip and a target, and a link with
a target are all accepted, that each node then has exactly the class `clickable`, and that the three edges of the
chain are the only edges; and that
`accTitle` and a multi-line `accDescr` give the title `Orders` and the description `How orders` and `flow` on two lines
with the indentation of the second removed.
