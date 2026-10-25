## ScadLanguage Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `ScadLanguage` subsystem (internal
namespace `DemaConsulting.CanvasNet.Scad.Language`), containing two units: `ScadLexer` and `ScadParser`.

### Verification Approach

The `ScadLanguage` subsystem is verified at two tiers. The unit tests (see _ScadLexer Unit Verification Design_
and _ScadParser Unit Verification Design_ under `scad-language/`) verify each unit in isolation, including its
internal types. The subsystem-level tests in `ScadLanguageTests.cs` verify the lexer and parser working together
through the internal `ScadParser.Parse(string, string?)` entry point on realistic programs, on the authored
fixtures, and on hostile input: that every valid fixture parses; that a program has an identical tree and
identical node locations whichever line-ending convention it uses; that tokenizing first and then parsing the
token list is equivalent to parsing the source directly; that directives are recorded in source order; that a
lexical error surfaces as a located `ScadSyntaxException` and takes precedence over a later-reported grammatical
error because the whole source is tokenized before it is parsed; and that hostile nesting fails closed with a
typed exception, including on a thread with a small stack.

Trees are compared through `ScadAstDumper`, a test helper that renders a syntax tree as a compact, location-free
S-expression string, so equality of two trees is a string comparison (locations are compared separately where
they matter). The syntax-error contract is the same as at the system tier: `ScadSyntaxException` derives from
`FormatException` (not `InvalidDataException`), and oversize source is rejected with a plain
`InvalidDataException`. No mocking or stubbing is used: the subsystem has no dependencies outside itself and the
core types `ScadSourceLocation` and `ScadSyntaxException`. The subsystem is internal, so its tests reach it
through the test assembly's `InternalsVisibleTo` access.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Files**: The authored fixtures in the `ScadFixtures/` folder copied to the test output directory (read only)
- **Isolation**: Each test parses its own sources; the small-stack test runs the parse on a dedicated thread
  with a 1 MiB stack and joins it before asserting

### Acceptance Criteria

The `ScadLanguage` subsystem's verification passes when every subsystem-level scenario below and every unit-level
scenario in the two unit verification documents passes without error or unexpected exception.

### Test Scenarios

- **Tokenization — Fixtures And Line Endings** (requirement `CanvasNetScad-ScadLanguage-Tokenization`):
  `ScadLanguageTests.ScadLanguage_Parse_AllAuthoredFixtures_Succeed`,
  `ScadLanguageTests.ScadLanguage_Parse_LineEndingVariants_ProduceIdenticalTrees`,
  `ScadLanguageTests.ScadLanguage_Parse_FixtureLineEndingConversion_ProducesIdenticalTrees` — parses all
  thirty-eight valid fixtures (the ten parse fixtures, the sixteen golden evaluation fixtures, and the twelve
  render fixtures) into non-empty programs;
  parses a small program joined with LF, CRLF, and CR line endings and
  asserts a single distinct tree dump and a single distinct list of statement locations (`p.scad:2:1` and
  `p.scad:10:1`); and converts every fixture to CRLF and to CR (after normalizing to LF) and asserts the dump is
  unchanged. Together they show that comment skipping, string handling, and position tracking compose through
  the parser for every line-ending style.
- **Parsing — Fixtures And Staged Equivalence** (requirement `CanvasNetScad-ScadLanguage-Parsing`):
  `ScadLanguageTests.ScadLanguage_Parse_AllAuthoredFixtures_Succeed`,
  `ScadLanguageTests.ScadLanguage_TokenizeThenParse_Fixture_MatchesParseOfSource`,
  `ScadLanguageTests.ScadLanguage_TokenizeThenParse_Program_MatchesParseOfSource` — parses every authored
  fixture (assignments, definitions, instantiations with modifiers, control flow, expressions, comprehensions,
  and function literals); and for every fixture and for a small program asserts that running
  `ScadLexer.Tokenize` and then `ScadParser.Parse(IReadOnlyList<ScadToken>)` yields the same tree dump (and, for
  the program, the same program location) as parsing the source directly.
- **Directives Are Recorded In Source Order** (requirement `CanvasNetScad-ScadLanguage-Directives`):
  `ScadLanguageTests.ScadLanguage_Parse_IncludeAndUseFixture_RecordsDirectivesInOrder` — parses the
  `include-and-use.scad` fixture and asserts that its four directive statements carry the expected kinds, exact
  paths (including `./relative/../nested/deep/path/box.scad`), and line numbers 3 to 6 in source order.
- **Lexical Error Inside A Statement** (requirement `CanvasNetScad-ScadLanguage-SyntaxErrors`):
  `ScadLanguageTests.ScadLanguage_Parse_LexicalErrorInsideStatement_SurfacesAsSyntaxExceptionWithLocation` —
  parses a program with a stray `@` at line 2, column 7 and asserts a `ScadSyntaxException` at exactly that
  location, naming the file, with the message `Unexpected character '@'`.
- **Lexical Error Is Reported Before A Grammatical Error** (requirement `CanvasNetScad-ScadLanguage-SyntaxErrors`):
  `ScadLanguageTests.ScadLanguage_Parse_LexicalErrorAfterGrammarError_IsReportedFirst` — parses a source with a
  grammatical error on line 1 and a lexical error on line 2 and asserts that the line 2 lexical error is the
  one reported, pinning the documented consequence of tokenizing the whole source before parsing it.
- **Deep Nesting Fails Closed With A Typed Exception** (requirement `CanvasNetScad-ScadLanguage-InputLimits`):
  `ScadLanguageTests.ScadLanguage_Parse_DeepNesting_FailsClosedWithTypedException` — parses sources nested 5,000
  levels deep in three shapes (parentheses, nested module instantiations with blocks, and a long chain of
  additions) and asserts that each throws a `ScadSyntaxException` whose message contains `Nesting too deep`.
- **Deep Nesting On A Small Stack Does Not Overflow** (requirement `CanvasNetScad-ScadLanguage-InputLimits`):
  `ScadLanguageTests.ScadLanguage_Parse_DeepNesting_OnSmallStackThread_DoesNotOverflow` — parses alternating
  vector and parenthesis nesting 50,000 levels deep on a thread with a 1 MiB stack and asserts that the
  outcome is a `ScadSyntaxException` and not a stack overflow or another exception type. The source-length
  half of this requirement (rejecting more than 4,194,304 characters with a plain `InvalidDataException`) is
  verified at the unit tier in the _ScadParser Unit Verification Design_ source-length scenarios.
