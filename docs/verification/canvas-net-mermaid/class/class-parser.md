### ClassParser Unit Verification Design

This document describes the unit-level verification strategy for the `ClassParser` unit in the
`DemaConsulting.CanvasNet.Mermaid.Class` namespace: the internal sealed `ClassParser` (the lexer and the grammar, in
two partial files) and the token names of `ClassTokens`.

#### Verification Approach

The unit is verified in two ways. `ClassVectorTests` parses the 179 cases of
`Fixtures/upstream-vectors/class-parser.json`, recorded from the pinned Mermaid release in headless Chrome by
`tools/visual-diff/mermaid/gen-diagram-vectors.mjs --type class` from the inputs of Mermaid's class parser specs,
the original texts of `class-vector-cases.mjs`, and the class fixtures: for each of the 168 valid texts the facts of
Mermaid's class database (the type, the direction, the accessibility texts, the classes with their labels, types, DOM
ids, CSS classes, styles, annotations, members, methods, parents, and link data, the relations, the notes, the
namespaces, the interfaces, and the style classes) and the facts of its `getData()` must equal the facts of the parsed
model under the configuration resolved as Mermaid resolves it with the case's site configuration, and for each of the
11 invalid texts the error must be Mermaid's: the same unexpected token and the same expected tokens in jison's order
for a grammar error (8 cases), an unrecognized-text error for a lexical error (2 cases), and Mermaid's own message for
a database error (1 case), always located. jison's line of an error is not compared, because jison reports the line
after the lookahead was read. `ClassParserTests` verifies the lexer quirks, the statements, the errors, and the limits
on short hand-written texts, asserting the classes, the relations, the expected tokens, the locations (line and column
in the original text), and the hints.

No mocking or stubbing is used; the preprocessor, the detector, the scanner, the configuration resolver, and the model
are real (the helper `ClassFacts` preprocesses and detects a text, calls `Parse`, and writes the facts of a model as
the vector generator records them from Mermaid). The internal types are reached through the test assembly's
`InternalsVisibleTo` access.

Unit tests reside in `ClassParserTests.cs` and `ClassVectorTests.cs` (with the helper `ClassFacts.cs`) within the
`DemaConsulting.CanvasNet.Mermaid.Tests` project, in the `Class` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: `Fixtures/upstream-vectors/class-parser.json`, generated from the pinned Mermaid by
  `tools/visual-diff/mermaid/gen-diagram-vectors.mjs --type class`
- **Isolation**: Each test parses its own text; the fixture is read only

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any database or layout fact, expected token, location, or error that differs from the recorded or asserted
Mermaid value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Class-ClassParser-Tokens: Tokens As Mermaid's Lexer Makes Them

**Tests**: `ClassVectorTests.ClassParser_Parse_UpstreamVector_MatchesMermaid`,
`ClassParserTests.ClassParser_Parse_DirectionAnywhereOnTheLine_IsDirectionStatement`,
`ClassParserTests.ClassParser_Parse_NamesStartingWithO_AreNames`,
`ClassParserTests.ClassParser_Parse_ClassNames_FollowTheGrammar`

Asserts that every upstream vector gives Mermaid's facts; that a line `whatever direction BT and more` after a
`direction LR` statement is one direction statement, so the direction is `BT` and the only class is `Alpha`; that in
`order o-- owner` only the lone `o` is an aggregation end, giving a relation from `order` to `owner` with the
aggregation at the first end; and that the names `Ca-r` (a dash between name tokens), `` `Line Item` `` (a backticked
name), `Box~T~` (a generic name, kept as `Box` with the type `T`), and `Città` (a letter outside ASCII) are the classes
`Ca-r`, `Line Item`, `Box`, and `Città`.

##### CanvasNetMermaid-Class-ClassParser-Grammar: The Statements Of Mermaid's Grammar, In Order

**Tests**: `ClassVectorTests.ClassParser_Parse_UpstreamVector_MatchesMermaid`,
`ClassVectorTests.ClassParser_Vectors_FileHasEnoughCases_AtLeast120`,
`ClassParserTests.ClassParser_Parse_ClassStatement_AddsClassWithLabelClassAndMembers`,
`ClassParserTests.ClassParser_Parse_RelationForm_MapsToRelationCodes`,
`ClassParserTests.ClassParser_Parse_RelationWithCardinalitiesAndLabel_KeepsThem`,
`ClassParserTests.ClassParser_Parse_LoneName_DoesNothing`,
`ClassParserTests.ClassParser_Parse_ClickStatements_KeepLinkData`,
`ClassParserTests.ClassParser_Parse_EncodedUnsafeLinks_KeepOnlyTheCheckedLink`

Asserts that every upstream vector (a theory with one case for each) gives Mermaid's database and layout facts or
Mermaid's error, and that the file holds at least 120 cases with between 10 and all but 100 errors; that
`class Account["Bank account"]:::warm` with a body adds the one class `Account` with the label `Bank account`, the CSS
classes `default warm`, the attribute `String owner`, and the method `deposit`; that each of 16 relation forms (eleven
one-ended solid and dashed forms, the plain `--` and `..`, and the two-way forms `<|--|>`, `*--o`, and `<..>`),
written with spaces around it and without, maps to Mermaid's ends and line between `Left` and `Right` (a
theory with one case for each form); that `Customer "1" --> "*" Order : places` keeps the cardinalities `1` and `*` and
the label `places`, while `Order --> "1" Payment` has the first cardinality `none` and no label; that a lone name and a
`title A title` line add no class; and that `click Gateway href "https://example.com/g" "Gateway docs" _self` keeps the
link, the target `_self`, and the tooltip and gives the CSS classes `default clickable`, while `callback Docs "show"`
records the callback; and that fourteen `link` and `click ... href` statements keep the links the pinned Mermaid keeps
(recorded in headless Chrome): `about:blank` for a raw tab, a percent-encoded letter, `&Tab;` with `&colon;`, an
escaped white space, spaces around a `data` link, a letter outside ASCII before the scheme, `%09`, `%2520`, a
`vbscript` link, and `&NewLine;`; a numeric reference kept in Mermaid's entity encoding (it never decodes to a scheme);
and `https://Example.com/a/../b c`, a relative link, and a `mailto:` link written as sanitize-url writes them.

##### CanvasNetMermaid-Class-ClassParser-Errors: Errors With Jison's Expected Tokens

**Tests**: `ClassParserTests.ClassParser_Parse_MissingTarget_ThrowsLocatedErrorWithExpectedTokens`,
`ClassParserTests.ClassParser_Parse_UnclosedBody_FailsAtTheEndOfTheBody`,
`ClassParserTests.ClassParser_Parse_Semicolon_IsLexicalError`,
`ClassParserTests.ClassParser_Parse_DirectionOnTheKeywordLine_FailsLikeMermaid`,
`ClassVectorTests.ClassParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that `Alpha -->` without a second class is `mermaid-syntax-error` at line 2, column 10 of the named file, found
`NEWLINE`, with exactly `STR`, `ALPHA`, `LINE`, `DOTTED_LINE`, `NUM`, `MINUS`, `UNICODE_TEXT`, and `BQUOTE_STR`
expected in jison's order; that a class body without its `}` fails at `EOF_IN_STRUCT` with `STRUCT_STOP` and `MEMBER`
expected and a hint that names `'}'`; that `Alpha --> Beta;` is a lexical error ("unrecognized text ';'") at line 2,
column 15, with no expected tokens; that `classDiagram direction LR` on the keyword line turns the line into a
direction statement, so the text has no header and its `EOF` is refused with no expected tokens, as in Mermaid; and
that the 11 invalid upstream vectors fail as Mermaid fails.

##### CanvasNetMermaid-Class-ClassParser-LinearTime: Long Names And Texts Without Recursion

**Tests**: `ClassParserTests.ClassParser_Parse_NameOfManyTokens_IsReadIteratively`,
`ClassParserTests.ClassParser_Parse_TextOfManyStatements_IsReadIteratively`

Asserts that, with `MaxLabelLength` raised to 100,000, a class name of 15,000 `a` tokens joined by dashes (29,999
tokens) is read without recursion into the one class with that whole name; and that, with `MaxTextSize`, `MaxNodes`
and `MaxEdges` raised, a text of 6,000 statements (1,500 class statements, 1,500 attribute and 1,500 method member
statements, and 1,500 inheritance relations in a chain) is read without recursion into 1,501 classes, 1,500 relations,
and 1,500 classes with one attribute and one method each. The matchers do no backtracking, so the tests run in linear
time (no wall-clock assertion).

##### CanvasNetMermaid-Class-ClassParser-Limits: Nesting And Label Limits

**Tests**: `ClassParserTests.ClassParser_Parse_NamespacesTooDeep_ThrowsLimitExceeded`,
`ClassParserTests.ClassParser_Parse_NestedDottedNamespacesTooDeep_ThrowsLimitExceeded`,
`ClassParserTests.ClassParser_Parse_LongMember_ThrowsLimitExceeded`,
`ClassParserTests.ClassParser_Parse_LongTextAnywhere_ThrowsLimitExceeded`

Asserts that with `MaxNestingDepth` of 2 a third nested namespace is `mermaid-limit-exceeded` at line 4, column 1, the
namespace that is too deep, with a message that names `MaxNestingDepth`; that with `MaxNestingDepth` of 3 the nested
keywords and the dots in their names count together, so `namespace C.D` inside `namespace A.B` (four levels in two
keywords) is `mermaid-limit-exceeded` at its name (line 3, column 11), while `namespace C` inside `namespace A.B` is
accepted as `A.B.C`; that with `MaxLabelLength` of 10 a longer
member line of a class body is `mermaid-limit-exceeded` with a message that names `MaxLabelLength`; and that with the
same limit a text of 11 characters is `mermaid-limit-exceeded`, located at its line, with a message that names
`MermaidLimits.MaxLabelLength`, wherever it stands (a theory with 28 cases): a class name, a class label, a generic
type, a `:::` class, an annotation in a body and in an annotation statement, an attribute and the parameters of a
method in member statements, a relation label, both cardinalities, the second class of a relation, a free note and a
note for a class, a namespace name and label, a `classDef` name, the joined style of a `classDef` and of a `style`
statement, a `cssClass` name, a link and its tooltip, a callback, the function of `click ... call`, the link of
`click ... href`, an accessible title, and a one-line and a multi-line accessible description.
