### SourceScanner Unit Verification Design

This document describes the unit-level verification strategy for the `SourceScanner` unit in the
`DemaConsulting.CanvasNet.Mermaid.Syntax` namespace: the internal `SourceScanner` class with `ScanRule`, `ScanToken`,
and `ScanMatcher`, the grouped `ScanPatterns` matcher building blocks, and the grouped `TokenCursor` lookahead.

#### Verification Approach

The unit is verified with a small grammar built in the tests from `ScanPatterns` matchers: white space that is
skipped, a `-` rule placed before a `-->` rule (so that the first-match rule is observable), a quote that enters a
string condition and a closing quote that pops it, words, an `=>` rule whose action keeps one character, and an
end-of-input rule. The token kinds, texts, and positions it produces are compared with what a jison-generated lexer
produces for the same rules, and the errors are checked for their code, message, expected tokens, and location in
the original text. Separate rule sets check the defensive refusals. The matcher building blocks and the JavaScript
character classes are compared with the matches of the corresponding JavaScript regular expression parts. No
mocking or stubbing is required; the internal types are reached through the test assembly's `InternalsVisibleTo`
access. The full lexer of the flowchart grammar on top of this unit is verified by the `Flowchart` subsystem.

Unit tests reside in `SourceScannerTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Syntax` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test builds its own scanner over in-memory text; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any token sequence, position, or error that differs from the expected literal constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Syntax-SourceScanner-FirstMatch: The First Matching Rule Wins

**Tests**: `SourceScannerTests.SourceScanner_Next_FirstMatchingRuleWins`

Asserts that `a -- b` lexes as a word, two single minus tokens, and a word (the earlier `-` rule wins over the
longer `-->` rule), with skipped white space, followed by the end-of-input rule and the end.

##### CanvasNetMermaid-Syntax-SourceScanner-StartConditions: Start Conditions Switch The Rules

**Tests**: `SourceScannerTests.SourceScanner_Next_StartConditions_SwitchRules`,
`SourceScannerTests.SourceScanner_StateStack_PopAtInitialAndTopStateBeyondDepth`,
`SourceScannerTests.SourceScanner_Less_ReturnsRestOfMatchToInput`

Asserts that a quote pushes the string condition (stack depth 2), whose rules lex `x y` as one string token at
positions 3 to 6, and that the closing quote returns to the initial condition; that the top state, the state below
it, and a state beyond the depth of the stack are the string, initial, and initial conditions, that popping the
initial condition keeps it, and that beginning an unknown condition is an `ArgumentException`; and that an action
that keeps one character of `=>` produces `=` at positions 0 to 1 and leaves the position at 1.

##### CanvasNetMermaid-Syntax-SourceScanner-EndOfInput: The End-Of-Input Rule Runs Once

**Tests**: `SourceScannerTests.SourceScanner_Next_EndOfInputRule_RunsOnce`

Asserts that after the last word the end-of-input rule produces its token once and every further call returns the
end of input.

##### CanvasNetMermaid-Syntax-SourceScanner-LexicalErrors: Unrecognized Text Is A Located Error

**Tests**: `SourceScannerTests.SourceScanner_Next_UnrecognizedText_ThrowsLocatedError`

Asserts that a line break that no rule matches is `mermaid-syntax-error` at line 1, column 2 of the named source,
and that the message describes it as "(a line break)".

##### CanvasNetMermaid-Syntax-SourceScanner-ExpectedTokens: Parse Errors List The Expected Tokens

**Tests**: `SourceScannerTests.TokenCursor_Expect_WrongToken_ThrowsWithExpected`,
`SourceScannerTests.TokenCursor_JoinAlternatives_JoinsWithCommasAndOr`

Asserts that the cursor consumes an expected word and keeps the next token as lookahead, that expecting a link or a
word on a minus fails at line 1, column 3 with the expected tokens listed once each in the grammar's words and the
message "expected '-->' or a node id, found 'MINUS' ('-')", and that alternatives join as "nothing", "a", and "a, b
or c".

##### CanvasNetMermaid-Syntax-SourceScanner-GrammarDefects: Defects Of A Grammar Port Fail Visibly

**Tests**: `SourceScannerTests.SourceScanner_Constructor_InvalidConditions_Throw`,
`SourceScannerTests.SourceScanner_Next_NoProgress_ThrowsInvalidOperation`

Asserts that a condition table without the initial condition and one that names a rule that does not exist are
`ArgumentException`s, and that a rule that matches nothing and produces no token ends in an
`InvalidOperationException` instead of an endless loop.

##### CanvasNetMermaid-Syntax-SourceScanner-JavaScriptClasses: Matchers Behave As Regular Expression Parts

**Tests**: `SourceScannerTests.ScanPatterns_Matchers_BehaveAsRegexParts`

Asserts a case-insensitive literal followed by optional white space, an ordered choice, a negative lookahead, and a
run with a minimum length; that the no-break space, the byte-order mark, and a four-per-em space are JavaScript white
space while a zero-width space is not; that the line separator is a line terminator; and that an accented letter is
not a word character.
