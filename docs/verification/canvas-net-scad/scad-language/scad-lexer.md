### ScadLexer Unit Verification Design

<!-- cspell:ignore xmodule -->

This document describes the unit-level verification strategy for the `ScadLexer` unit: the internal static
`ScadLexer` class (`Tokenize`), the `ScadToken` record struct, and the `ScadTokenKind` enumeration in the
`DemaConsulting.CanvasNet.Scad.Language` namespace.

#### Verification Approach

The unit is verified by tokenizing small source strings and asserting on the returned token list: the kinds, the
decoded text or numeric value, and the one-based line and column of each token. Every lexical error is asserted
as a `ScadSyntaxException` at an exact line and column with an expected message fragment. The unit has no
dependencies other than `ScadSourceLocation` and `ScadSyntaxException`, so no mocking or stubbing is required;
the internal types are reached through the test assembly's `InternalsVisibleTo` access. The lexer's interaction
with the parser is verified at the subsystem tier (see _ScadLanguage Subsystem Verification Design_) and under
the _ScadParser Unit Verification Design_. As elsewhere in the system, `ScadSyntaxException` is a
`FormatException` (not an `InvalidDataException`); the lexer applies no source-length limit of its own, which is
enforced by the parser entry point.

The shared helper `ScadLexerTestSupport` (in `ScadLexerTestSupport.cs`) provides `Lex` (tokenize with an optional
file name), `Single` (tokenize a source that must yield exactly one token plus the end-of-file token and return
that token), `Kinds` (render the token kinds, without the end-of-file token, as a space-separated string such as
`Identifier Assign Number Semicolon` so that token streams are asserted as strings), and `AssertLexError` (assert
a syntax error at an exact location with a message fragment, built on `ScadTestHelpers.AssertSyntaxError`).
Many scenarios are xUnit theories whose data rows cover each form; this document lists the test method once.

Unit tests reside in `ScadLexerTests.cs` (identifiers, keywords, operators, and unexpected characters),
`ScadLexerNumberTests.cs`, `ScadLexerStringTests.cs` (strings and escapes), `ScadLexerCommentTests.cs`,
`ScadLexerDirectiveTests.cs`, and `ScadLexerLocationTests.cs` within the `DemaConsulting.CanvasNet.Scad.Tests`
project, in the `Language` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test tokenizes its own source strings; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those
explicitly asserted. Any unexpected exception, wrong exception type, or wrong token kind, text, value, line, or
column constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetScad-ScadLanguage-ScadLexer-Numbers: Numeric Literal Forms And Boundaries

**Tests**: `ScadLexerNumberTests.ScadLexer_Tokenize_DecimalNumber_ProducesNumberTokenWithValue`,
`ScadLexerNumberTests.ScadLexer_Tokenize_LeadingDotNumber_ProducesNumberToken`,
`ScadLexerNumberTests.ScadLexer_Tokenize_TrailingDotNumber_ProducesNumberToken`,
`ScadLexerNumberTests.ScadLexer_Tokenize_ExponentNumber_ProducesNumberTokenWithValue`,
`ScadLexerNumberTests.ScadLexer_Tokenize_ExponentWithoutDigits_SplitsNumberAndIdentifier`,
`ScadLexerNumberTests.ScadLexer_Tokenize_ExponentOverflow_ProducesInfinity`,
`ScadLexerNumberTests.ScadLexer_Tokenize_NumberFollowedByLetters_SplitsIntoNumberAndIdentifier`,
`ScadLexerNumberTests.ScadLexer_Tokenize_DotBeforeDigitOrLetter_DistinguishesNumberFromMember`

Asserts that integer and decimal literals (including leading zeros), a leading-dot literal (`.5`), a
trailing-dot literal (`5.`), and exponent forms with signs and both exponent cases produce one number token with
the expected double value and lexeme text; that an exponent marker without digits (`2e`, `2e+`, `2e-x`) ends the
number and leaves the remainder to be lexed separately; that `1e999` yields positive infinity and `1e-999`
yields zero rather than an error; that letters after a number (`2cm`) start an identifier at the next column;
and that a dot after an identifier is a member access while a dot before a digit starts a number (`1.2.3` is two
numbers).

##### CanvasNetScad-ScadLanguage-ScadLexer-Strings: String Literal Boundaries

**Tests**: `ScadLexerStringTests.ScadLexer_Tokenize_String_ProducesStringTokenWithValue`,
`ScadLexerStringTests.ScadLexer_Tokenize_EmptyString_ProducesEmptyValue`,
`ScadLexerStringTests.ScadLexer_Tokenize_StringContainingNewline_TracksLineAndColumn`,
`ScadLexerStringTests.ScadLexer_Tokenize_UnterminatedString_ThrowsAtOpeningQuote`,
`ScadLexerStringTests.ScadLexer_Tokenize_EscapedQuote_DoesNotTerminateString`

Asserts that a double-quoted string yields a string token carrying its decoded value, that an empty string is
accepted, that a raw line break (LF or CRLF) inside a string is kept in the value and advances the line count so
that the next token is reported at line 2 after the closing quote, that an escaped quote does not end the
string, and that an unterminated string (including input that ends right after a backslash, a lone quote, and a
string that runs to the end of the input) throws `Unterminated string literal` located at the opening
quote.

##### CanvasNetScad-ScadLanguage-ScadLexer-StringEscapes: String Escape Decoding

**Tests**: `ScadLexerStringTests.ScadLexer_Tokenize_SimpleEscapes_ProducesControlCharacters`,
`ScadLexerStringTests.ScadLexer_Tokenize_HexEscape_ProducesLatin1Character`,
`ScadLexerStringTests.ScadLexer_Tokenize_ShortUnicodeEscape_ProducesBmpCharacter`,
`ScadLexerStringTests.ScadLexer_Tokenize_LongUnicodeEscape_ProducesSurrogatePair`,
`ScadLexerStringTests.ScadLexer_Tokenize_UnknownEscape_ThrowsAtBackslash`,
`ScadLexerStringTests.ScadLexer_Tokenize_TruncatedHexEscape_Throws`,
`ScadLexerStringTests.ScadLexer_Tokenize_SurrogateOrOutOfRangeCodePoint_Throws`

Asserts that the single-character escapes for newline, tab, carriage return, quote, and backslash decode to the
expected characters; that the two-digit hexadecimal escape decodes ASCII, the top of the Latin-1 range, and NUL;
that the four-digit escape decodes Basic Multilingual Plane characters including the code points adjacent to the
surrogate range; that the six-digit escape decodes `U+1F600` to a surrogate pair and accepts the maximum scalar
`U+10FFFF`; that an unknown escape throws `Invalid escape sequence '\q'` at the backslash; that truncated or
non-hexadecimal digits for each of the three hexadecimal forms (and input ending inside the escape) throw a
message naming the required hexadecimal digits at the backslash; and that surrogate code points and values
above `U+10FFFF` throw `not a valid Unicode scalar value` at the backslash.

##### CanvasNetScad-ScadLanguage-ScadLexer-Identifiers: Identifiers And Special Variables

**Tests**: `ScadLexerTests.ScadLexer_Tokenize_Identifier_ProducesIdentifierToken`,
`ScadLexerTests.ScadLexer_Tokenize_SpecialVariable_ProducesIdentifierWithDollar`,
`ScadLexerTests.ScadLexer_Tokenize_LoneDollar_Throws`,
`ScadLexerTests.ScadLexer_Tokenize_IdentifierStartingWithDigit_SplitsNumberAndIdentifier`

Asserts that ASCII identifiers that start with a letter or underscore produce identifier tokens carrying their
name; that `$`-prefixed names (including `$1` and `$_a`) are identifiers that keep the dollar sign in their
text; that a lone dollar sign, at the end of input, before a semicolon, or before a space, throws
`Expected a name after '$'` at the dollar sign; and that `9lives` is a number followed by an identifier
at column 2.

##### CanvasNetScad-ScadLanguage-ScadLexer-Keywords: Reserved Words

**Tests**: `ScadLexerTests.ScadLexer_Tokenize_Keyword_ProducesKeywordKind`,
`ScadLexerTests.ScadLexer_Tokenize_KeywordPrefixOrSuffix_IsIdentifier`

Asserts that each of the thirteen reserved words (`module`, `function`, `if`, `else`, `for`,
`intersection_for`, `let`, `each`, `assert`, `echo`, `true`, `false`, `undef`) produces its own keyword kind
with the lexeme as text, and that identifiers that merely contain, extend, or differ in case from a keyword
(`modules`, `xmodule`, `If`, `for_`, `_for`, `true1`, `Undef`, `included`, `user`, `PI`, `children`) remain
identifiers.

##### CanvasNetScad-ScadLanguage-ScadLexer-Operators: Operators And Punctuation

**Tests**: `ScadLexerTests.ScadLexer_Tokenize_Operator_ProducesOperatorKind`,
`ScadLexerTests.ScadLexer_Tokenize_AdjacentOperators_UsesLongestMatch`,
`ScadLexerTests.ScadLexer_Tokenize_MinusAndNumber_ProducesSeparateTokens`,
`ScadLexerTests.ScadLexer_Tokenize_SlashAlone_IsDivide`,
`ScadLexerTests.ScadLexer_Tokenize_SingleAmpersandOrPipe_Throws`,
`ScadLexerTests.ScadLexer_Tokenize_Statement_ProducesTokensEndingWithEndOfFile`

Asserts that every one of the 28 operator and punctuation lexemes produces the matching kind with its lexeme as
text; that adjacent operator characters split greedily from the left (`<==` is less-or-equal then assign,
`!==` is not-equal then assign); that a minus sign is always a separate token from a following number; that a
lone slash is division; that a single `&` or `|` throws `Unexpected character` at the character with a hint that
names the doubled operator (`'&&'` or `'||'`); and that a full call statement produces the expected kind
sequence followed by a single end-of-file token with no text.

##### CanvasNetScad-ScadLanguage-ScadLexer-Comments: Comment Skipping

**Tests**: `ScadLexerCommentTests.ScadLexer_Tokenize_LineComment_IsSkippedToEndOfLine`,
`ScadLexerCommentTests.ScadLexer_Tokenize_LineCommentAtEndOfFileWithoutNewline_IsSkipped`,
`ScadLexerCommentTests.ScadLexer_Tokenize_LineCommentEndedByCarriageReturn_ContinuesOnNextLine`,
`ScadLexerCommentTests.ScadLexer_Tokenize_BlockComment_IsSkippedAndCountsLines`,
`ScadLexerCommentTests.ScadLexer_Tokenize_BlockCommentsDoNotNest_FirstTerminatorCloses`,
`ScadLexerCommentTests.ScadLexer_Tokenize_CommentMarkersInsideString_AreNotComments`,
`ScadLexerCommentTests.ScadLexer_Tokenize_UnterminatedBlockComment_ThrowsAtCommentStart`,
`ScadLexerCommentTests.ScadLexer_Tokenize_EmptyBlockComment_IsSkipped`

Asserts that a line comment is skipped to the end of its line (the next token is on line 2), that a line comment
at the end of the file needs no line break, that a carriage return ends a line comment, that a block comment is
skipped and its line breaks counted (the next token follows at line 2, column 6), that block comments do not nest
(the text after the first terminator is lexed as ordinary tokens), that comment markers inside strings are inert,
that an unterminated block comment (including `/*/` and a bare `/*`) throws `Unterminated block comment` at the
opening marker, and that `/**/` and `/***/` are valid empty block comments.

##### CanvasNetScad-ScadLanguage-ScadLexer-Directives: Include And Use Directive Tokens

**Tests**: `ScadLexerDirectiveTests.ScadLexer_Tokenize_IncludeDirective_ProducesPathToken`,
`ScadLexerDirectiveTests.ScadLexer_Tokenize_UseDirective_ProducesPathToken`,
`ScadLexerDirectiveTests.ScadLexer_Tokenize_DirectiveWithWhitespaceBeforeAngle_ProducesPathToken`,
`ScadLexerDirectiveTests.ScadLexer_Tokenize_DirectiveAfterOtherTokens_CarriesKeywordLocation`,
`ScadLexerDirectiveTests.ScadLexer_Tokenize_DirectiveWithoutAngleBracket_Throws`,
`ScadLexerDirectiveTests.ScadLexer_Tokenize_DirectiveUnterminated_Throws`,
`ScadLexerDirectiveTests.ScadLexer_Tokenize_DirectiveEmptyPath_Throws`

Asserts that `include <a/b.scad>` and `use <lib.scad>` each produce one directive token whose text is the exact
path and whose location is the keyword, and that the next token follows the closing bracket (column 16); that
white space, a newline, or a comment may separate the keyword from the opening angle bracket and that the path
is not trimmed; that a directive on a later line carries the keyword's line and column; that a directive without
an opening angle bracket throws `Expected '<' after 'include'` (or `'use'`) at the keyword; that a path that is
not closed on its line, or at the end of the file, throws `Unterminated include/use path` at the keyword; and
that an empty path throws `Empty include/use path` at the keyword.

##### CanvasNetScad-ScadLanguage-ScadLexer-Locations: Line And Column Tracking

**Tests**: `ScadLexerLocationTests.ScadLexer_Tokenize_Tokens_CarryOneBasedLineAndColumn`,
`ScadLexerLocationTests.ScadLexer_Tokenize_LineEndings_CountOneLineEach`,
`ScadLexerLocationTests.ScadLexer_Tokenize_MixedLineEndings_CountEachBreak`,
`ScadLexerLocationTests.ScadLexer_Tokenize_Tab_CountsAsOneColumn`,
`ScadLexerLocationTests.ScadLexer_Tokenize_VerticalTabAndFormFeed_AreWhitespace`,
`ScadLexerLocationTests.ScadLexer_Tokenize_FileName_IsPropagatedToTokenLocations`,
`ScadLexerLocationTests.ScadLexer_Tokenize_EndOfFileToken_IsAppendedAtEndLocation`,
`ScadLexerLocationTests.ScadLexer_Tokenize_LeadingByteOrderMark_IsSkipped`,
`ScadLexerLocationTests.ScadLexer_Tokenize_ByteOrderMarkInsideSource_Throws`

Asserts one-based lines and columns across lines; that LF, CRLF, and a lone CR each count as exactly one line
break; that mixed and consecutive terminators are counted individually (`\r\n\r` is two breaks, `\n\n\r` is
three); that a tab is one column and that vertical tab and form feed are white space; that the supplied file
name appears in every token location, including end-of-file, and in an error location; that exactly one
end-of-file token is appended at the position just after the last character (`1:1` for empty input, `1:4` after
`abc`, `2:1` after `a` and a newline, `2:3` after `a`, CRLF, and two spaces) with no text; that a leading
byte-order mark is skipped without consuming a column and a BOM-only source yields only the end-of-file token;
and that a byte-order mark anywhere else throws `Unexpected character`.

##### CanvasNetScad-ScadLanguage-ScadLexer-UnexpectedCharacters: Unexpected Characters And Null Source

**Tests**: `ScadLexerTests.ScadLexer_Tokenize_UnexpectedCharacter_ThrowsWithLocation`,
`ScadLexerTests.ScadLexer_Tokenize_NullSource_ThrowsArgumentNullException`

Asserts that characters that cannot start a token (`@`, a backtick, a backslash, `~`, a single quote, and the
non-ASCII `é` outside a string or comment) throw `Unexpected character` naming the character at its exact
location (line 2, column 3 in the test source), and that a null source throws `ArgumentNullException`.
