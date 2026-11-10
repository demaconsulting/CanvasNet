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
character classes are compared with the matches of the corresponding JavaScript regular expression parts, and the
shared `UNICODE_TEXT` letters with letters and non-letters inside and outside ASCII. No
mocking or stubbing is required; the internal types are reached through the test assembly's `InternalsVisibleTo`
access. The full lexers of the flowchart and class grammars on top of this unit are verified by the `Flowchart` and
`Class` subsystems.

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

##### CanvasNetMermaid-Syntax-SourceScanner-UnicodeLetters: The Grammar Letters Outside ASCII Are Shared

**Tests**: `SourceScannerTests.ScanPatterns_IsUnicodeLetter_FollowsTheGrammarLetters`

Asserts that `ScanPatterns.IsUnicodeLetter`, the one table of the `UNICODE_TEXT` letters that the flowchart and class
lexers share, is true for letters of several scripts - the feminine ordinal indicator U+00AA, the Latin letters U+00C0
and U+00E9, the Cyrillic U+0416, and the CJK ideograph U+4E2D - and false for the ASCII letter `a`, the digit `1`, the
underscore, the space, the no-break space U+00A0, and the multiplication and division signs U+00D7 and U+00F7 that sit
inside the Latin-1 letters; ASCII is matched by the grammars' own rules, not by this table.

##### CanvasNetMermaid-Syntax-SourceScanner-LinkCheck: Links Are Checked Exactly As sanitize-url Checks Them

**Tests**: `SourceScannerTests.UrlSanitizer_Sanitize_UnsafeScheme_IsBlank`,
`SourceScannerTests.UrlSanitizer_Sanitize_Url_GivesSanitizeUrlResult`,
`SourceScannerTests.UrlSanitizer_FormatUrl_EmptyAndUnsafeLinks_FollowMermaid`,
`SourceScannerTests.UrlSanitizer_Sanitize_DeeplyNestedReference_IsDecodedAndRefused`

Asserts that every attack vector of sanitize-url's own test file is `about:blank` - numeric character references
with and without semicolons, decimal with leading zeros and hexadecimal, a tab, a line feed, a carriage return, a
form feed, a vertical tab, U+0000, and U+0001 before or inside the scheme, `&Tab;` and nested `&NewLine;`, escaped
white space and nested percent-encoding of it (`%5C`, `%255C`, `%25%35%43`), invisible characters inside the scheme,
leading spaces and `%20`, `&colon;` and `&COLON;`, mixed case, and leading non-word characters, for `javascript`,
`data`, and `vbscript` - and so are further encoded forms: `0x`, `0X`, `0o`, and `0b` references, references beyond
65535 that wrap to `j`, a reference hidden in `&#38;` and in percent escapes, percent-encoded letters, tabs, line
feeds, U+0000, and invisible characters, escaped white space, the line separator before the scheme or a line, white
space outside ASCII, and a letter outside ASCII before the scheme (not a word character in JavaScript). Asserts, with
sanitize-url's results in Node as the expected values, that safe links keep sanitize-url's result (the safe cases of
its test file, relative and protocol-less links, `mailto:`, deep links, backslashes as slashes, control characters
removed), that encoded links decode as JavaScript decodes them (a reference word that is not a number gives U+0000,
an exponent, `Infinity`, numbers beyond 2^53, malformed percent escapes, overlong and surrogate UTF-8, `toLowerCase`
of U+0130 and the Kelvin sign), and that `http` and `https` links are written back as the WHATWG URL parser writes
them: scheme and host case, IDNA, IPv4 in decimal, octal, and hexadecimal forms, IPv6 compression and embedded IPv4,
user information with `%40`, default ports, dot segments with `%2e`, the percent-encode sets of the path, query, and
fragment, the forbidden host code points, a lone surrogate, and `about:blank` for a link that does not parse (an
empty host, a port beyond 65535, an invalid IPv4 or IPv6 address, an invalid `xn--` label); the dot segments of
`http://a/y/.x/.` are resolved as the Standard and Chromium resolve them. Asserts that no link (null or empty) is
`about:blank`, that `FormatUrl` gives no link for a blank one and trims and checks any other, and that a reference
nested 1,000 levels deep is decoded level by level and refused.
