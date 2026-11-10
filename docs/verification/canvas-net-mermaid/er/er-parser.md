### ErParser Unit Verification Design

This document describes the unit-level verification strategy for the `ErParser` unit in the
`DemaConsulting.CanvasNet.Mermaid.Er` namespace: the internal sealed `ErParser` (the lexer and the grammar, in two
partial files) and the token names of `ErTokens`.

#### Verification Approach

The unit is verified in two ways. `ErVectorTests` parses the 246 cases of `Fixtures/upstream-vectors/er-parser.json`,
recorded from the pinned Mermaid release in headless Chrome by
`tools/visual-diff/mermaid/gen-diagram-vectors.mjs --type er` from the inputs of Mermaid's ER parser specs
(`parser/erDiagram.spec.js` and `parser/subgraph.spec.js`), the original texts of `er-vector-cases.mjs` (some of them
modeled on Mermaid's `erDb.spec.js`), and the 12 ER fixtures: for each of the 219 valid texts the facts of Mermaid's ER
database (the type, the direction, the accessibility texts, the entities with their ids, aliases, CSS classes, styles,
and attributes, the relationships with their ends, roles, and specifications, the style classes, and the subgraphs
with their ids, titles, nodes, classes, styles, and directions) and the facts of its `getData()` (the clusters and
entity nodes with their parents, directions, paddings, classes, styles, compiled styles, color indexes, looks, and
attributes, and the edges with their ids, ends, labels, markers, and patterns) must equal the facts of the parsed model
under the configuration resolved as Mermaid resolves it, and for each of the 27 invalid texts, all of them grammar
errors, the error must be Mermaid's: the same unexpected token and the same expected tokens in jison's order, always
located. jison prints the end of the tokens as `'1'`, which is also the name of the character `1` that an attribute
block returns as itself, so either reading of that token is accepted, while the expected tokens are still compared
exactly; jison's line of an error is not compared, because jison reports the line after the lookahead was read.
`ErParserTests` verifies the lexer order and quirks, the statements, the errors, and the limits on short hand-written
texts, asserting the entities, the relationships, the attributes, the subgraphs, the expected tokens, the locations
(line and column in the original text), and the hints.

No mocking or stubbing is used; the preprocessor, the detector, the scanner, the configuration resolver, and the model
are real (the helper `ErFacts` preprocesses and detects a text, calls `Parse`, and writes the facts of a model as the
vector generator records them from Mermaid). The internal types are reached through the test assembly's
`InternalsVisibleTo` access.

Unit tests reside in `ErParserTests.cs` and `ErVectorTests.cs` (with the helper `ErFacts.cs`) within the
`DemaConsulting.CanvasNet.Mermaid.Tests` project, in the `Er` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: `Fixtures/upstream-vectors/er-parser.json`, generated from the pinned Mermaid by
  `tools/visual-diff/mermaid/gen-diagram-vectors.mjs --type er`
- **Isolation**: Each test parses its own text; the fixture is read only

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any database or layout fact, expected token, location, or error that differs from the recorded or asserted
Mermaid value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Er-ErParser-Tokens: Tokens As Mermaid's Lexer Makes Them

**Tests**: `ErVectorTests.ErParser_Parse_UpstreamVector_MatchesMermaid`,
`ErParserTests.ErParser_Parse_LeftCardinality_FollowsMermaidsLexerOrder`,
`ErParserTests.ErParser_Parse_KeywordLikeNamesAndKeywordsInAnyCase_FollowTheLexer`,
`ErParserTests.ErParser_Parse_DirectionLines_FollowTheLexer`,
`ErParserTests.ErParser_Parse_GenericType_TakesTheLineUpToItsLastTilde`

Asserts that every upstream vector gives Mermaid's facts; that each of 22 left cardinalities in `A <left>--o{ B : r`
(a theory with one case for each) is read as Mermaid's lexer reads it and becomes the second cardinality (`cardB`),
while the right `o{` is the first (`cardA`, `ZERO_OR_MORE`): `|o`, `o|`, `one or zero`, and `zero or one` as
`ZERO_OR_ONE`; `}o`, `o{`, `zero or more`, `zero or many`, `0+`, `many(0)`, and `many` as `ZERO_OR_MORE`; `}|`, `|{`,
`one or more`, `one or many`, `1+`, and `many(1)` as `ONE_OR_MORE`; `||`, `one`, `only one`, and `1` as `ONLY_ONE`;
and `u` as `MD_PARENT`; that the names `onerous`, `toaster`, `manyfold`, `user`, `only`, and `zero`, which start like
keywords or cardinalities, are the six entities in that order, while the keywords in upper case (`CLASSDEF`, `CLASS`,
`STYLE`, `ONE OR MANY`, `TO`, and `MANY`) give the entity `A` the CSS classes `default c` and the one style
`stroke:blue` and a relationship with `cardA` `ZERO_OR_MORE`, the type `IDENTIFYING`, and `cardB` `ONE_OR_MORE`; that
a relationship line ending with `%% direction LR` is one direction statement, so the direction is `LR` and its
entities are not added, and that in a subgraph with `direction BT`, the entity `C`, and `direction RL`, the only entity
is `C` and the subgraph's direction is `RL`, the last one; and that the attribute line `List~int~ a, Map~x~ b` is one
attribute with the type `List~int~ a, Map~x~` (up to the last tilde of the line) and the name `b`.

##### CanvasNetMermaid-Er-ErParser-Grammar: The Statements Of Mermaid's Grammar, In Order

**Tests**: `ErVectorTests.ErParser_Parse_UpstreamVector_MatchesMermaid`,
`ErVectorTests.ErParser_Vectors_FileHasEnoughCases_AtLeast110`,
`ErParserTests.ErParser_Parse_RelationshipType_GivesTheIdentification`,
`ErParserTests.ErParser_Parse_Roles_AreQuotedOrOneWord`,
`ErParserTests.ErParser_Parse_Attributes_KeepTypesKeysAndComments`,
`ErParserTests.ErParser_Parse_Aliases_KeepTheFirst`,
`ErParserTests.ErParser_Parse_StatementValues_BecomeSubgraphNodes`

Asserts that every upstream vector (a theory with one case for each) gives Mermaid's database and layout facts or
Mermaid's error, and that the file holds at least 110 cases with between 10 and all but 100 errors; that in
`A ||<type>|| B : r` the types `--` and `to` are `IDENTIFYING` and `..`, `.-`, `-.`, and `optionally to` are
`NON_IDENTIFYING` (a theory with six cases); that the roles `""`, `plain words here`, and `"quoted: with colon"` are
the empty role, `plain`, and `quoted: with colon`, while the words `words` and `here` after the one-word role become
entities, so the entities are `A`, `B`, `C`, `words`, `here`, and `D`; that an attribute block keeps six attributes in
order: `List~int~ ids`, `string[] tags` with the keys `PK` and `FK` and the comment `key`, `decimal? amount`, the
backticked `geo.point` and `display.name` with the key `uk` in lower case, `int code` with the keys `FK`, `UK`, and
`PK` and the comment `all`, and `int g` with only the comment `comment only`; that the aliases of `a["Quoted alias"]`,
`b[Bare]`, `c["First"]` followed by `c["Second"]`, `d` followed by `d["Late"]`, and `e["E"]:::hot` with a block are
`Quoted alias`, `Bare`, `First`, `Late`, and `E` (the first alias that is not empty), with `e` having the CSS classes
`default hot` and one attribute; and that a subgraph `S["Sales" area]` has the id `S`, the title `Sales area`, and the
nodes `hello`, `classDef`, `class`, `style`, `A`, `B`, and `C`, the values of its accessibility, style, entity, and
relationship statements in order.

##### CanvasNetMermaid-Er-ErParser-Errors: Errors With Jison's Expected Tokens

**Tests**: `ErParserTests.ErParser_Parse_GrammarError_ThrowsLocatedErrorWithExpectedTokens`,
`ErParserTests.ErParser_Parse_UnclosedSubgraph_FailsAtTheEnd`,
`ErParserTests.ErParser_Parse_DotInAStyle_IsALexicalError`,
`ErVectorTests.ErParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that ten grammar errors (a theory) are `mermaid-syntax-error` at the line of the refused token, with jison's
expected tokens (the first one and their number asserted) and a hint: `A ||--o{ B` without its role at line 2 with
`COLON` and `STYLE_SEPARATOR` (2); `end` at the top level at line 2 with the 19 tokens of a top-level line, `EOF`
first; the key list `PK,` and the type `string?` without a name, each closed by `}` at line 4, with only
`ATTRIBUTE_KEY` and only `ATTRIBUTE_WORD`; the roles `one` and `to`, which are keywords, at line 2 with the 3 role
tokens, `UNICODE_TEXT` first; `subgraph one["Title"]`, whose id is a cardinality, at line 2 with the 5 name tokens,
`UNICODE_TEXT` first; and a relationship followed by `;` or by a `%%` comment on its line, and a `1` before a line
break and a name (a cardinality, not an entity), each at line 2 with the 20 tokens that may follow a line, `EOF` first.
Asserts that a subgraph without its `end` fails at the end of the text (`found the end of the text`) with `END`
expected and a hint that names `'end'`; that `style A stroke-width:1.5px` is a lexical error, `mermaid-syntax-error`
with the message `unrecognized text '.'`, located at line 3, column 23; and that the 27 invalid upstream vectors fail
as Mermaid fails.

##### CanvasNetMermaid-Er-ErParser-LinearTime: Long Lines And Texts Without Recursion

**Tests**: `ErParserTests.ErParser_Parse_LongLinesAndManyStatements_AreReadIteratively`

Asserts that, with `MaxNodes` raised to 6,000 and `MaxEdges` to 2,000, a line of 5,000 names is read into 5,000
entities; that an attribute with 5,000 keys `PK` joined by commas and a comment holding a tilde (so that the lexer
searches the long line for its last tilde) is read, under the default limits, into one attribute with 5,000 keys; and
that a text of 1,500 relationship statements in a chain is read into 1,500 relationships and 1,501 entities. The
statements, ids, and keys are read in loops and the line searches are cached per line, so the tests run without
recursion and in linear time (no wall-clock assertion).

##### CanvasNetMermaid-Er-ErParser-Limits: Nesting And Label Limits

**Tests**: `ErParserTests.ErParser_Parse_SubgraphsTooDeep_ThrowsLimitExceeded`,
`ErParserTests.ErParser_Parse_LongTextAnywhere_ThrowsLimitExceeded`

Asserts that with `MaxNestingDepth` of 2 two nested subgraphs are accepted as two subgraphs, while a third nested
subgraph is `mermaid-limit-exceeded` at line 4, the `subgraph` keyword that is too deep, with a message that names
`MermaidLimits.MaxNestingDepth`; and that with `MaxLabelLength` of 10 a text of 11 or more characters is
`mermaid-limit-exceeded`, located at its line, with a message that names `MermaidLimits.MaxLabelLength`, wherever it
stands (a theory with 23 cases): an entity name, a quoted name (measured with its quotes), an alias, the second entity
of a relationship, a one-word and a quoted role, a `:::` class, an attribute type, an attribute name, a type joined
with its `?`, a backticked attribute word, a comment, a class name of a `class` statement, a `classDef` name, a
one-word `classDef` style, a style joined from its components in a `classDef` and in a `style` statement, a subgraph
id, a one-word and a joined three-word subgraph title, an accessible title, and a one-line and a multi-line accessible
description.
