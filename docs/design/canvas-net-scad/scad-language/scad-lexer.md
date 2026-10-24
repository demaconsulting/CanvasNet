### ScadLexer

<!-- cspell:ignore lexer lexical lexeme lexemes tokenize tokenized tokenizes tokenizer hexadecimal UHHHHHH -->
The `ScadLexer` unit is the first software unit in the `ScadLanguage` subsystem. It comprises four
source files in the `DemaConsulting.CanvasNet.Scad.Language` namespace, all `internal`:

- `ScadTokenKind.cs` — `ScadTokenKind` (enum): every kind of token
- `ScadToken.cs` — `ScadToken` (readonly record struct): kind, text, numeric value, and location
- `ScadLexer.cs` — `ScadLexer` (sealed partial class): the dispatch loop, trivia, identifiers,
  keywords, directives, and operators
- `ScadLexer.Literals.cs` — the second half of `ScadLexer`: number and string literals

#### Purpose

`ScadLexer` converts the complete source text into a list of `ScadToken` values, always ending with
exactly one end-of-file token, or throws a `ScadSyntaxException` at the first lexical error. It exists
as a separate stage so that the parser can look ahead over a plain token list, and so that every
lexical error is reported before any grammatical analysis begins (see _ScadLanguage_,
`scad-language.md`). The lexer is an original implementation written from general language-design
knowledge, with no OpenSCAD source, grammar, manual text, or test material consulted.

The unit's behavior is specified by the `CanvasNetScad-ScadLanguage-ScadLexer-*` requirements
(`Numbers`, `Strings`, `StringEscapes`, `Identifiers`, `Keywords`, `Operators`, `Comments`,
`Directives`, `Locations`, and `UnexpectedCharacters`) in
`docs/reqstream/canvas-net-scad/scad-language/scad-lexer.yaml`.

#### Data Model

**`ScadToken`** is `(ScadTokenKind Kind, string? Text, double Number, ScadSourceLocation Location)`.
`Text` holds the identifier name (including the `$` of a special variable), the decoded value of a
string literal, the path of an `include`/`use` directive (without the angle brackets), or the exact
lexeme of a keyword, operator, punctuation mark, or number; it is `null` only for the end-of-file
token. `Number` is the numeric value of a `Number` token and zero for every other kind. `Location`
is the position of the first character of the token; for a directive it is the position of the
`include`/`use` keyword, and for the end-of-file token it is the position just after the last
character of the input (after any trailing whitespace, line breaks, and comments).

**`ScadTokenKind`** has one member per kind so that the parser can dispatch on `Kind` alone:

<!-- markdownlint-disable MD013 -->

| Group | Kinds |
| ----- | ----- |
| Structural | `EndOfFile` |
| Literals and names | `Number`, `String`, `Identifier` |
| Directives | `Include`, `Use` (the whole `include <path>` / `use <path>` is one token) |
| Keywords | `Module`, `Function`, `If`, `Else`, `For`, `IntersectionFor`, `Let`, `Each`, `Assert`, `Echo`, `True`, `False`, `Undef` |
| Punctuation | `LeftParen`, `RightParen`, `LeftBrace`, `RightBrace`, `LeftBracket`, `RightBracket`, `Semicolon`, `Comma`, `Dot`, `Colon`, `Question` |
| Operators | `Assign`, `EqualEqual`, `NotEqual`, `Less`, `LessEqual`, `Greater`, `GreaterEqual`, `AndAnd`, `OrOr`, `Plus`, `Minus`, `Star`, `Slash`, `Percent`, `Caret`, `Bang`, `Hash` |

<!-- markdownlint-enable MD013 -->

The characters `*`, `!`, `#`, and `%` double as statement modifiers; the lexer always emits them as
`Star`, `Bang`, `Hash`, and `Percent` and the parser decides from context whether they are modifiers
or operators.

**Lexical specification** (the contract the scanner implements):

- **Whitespace**: space, tab, vertical tab, form feed, and the line breaks `\r\n`, `\n`, and a lone
  `\r` (each is exactly one line break). All other characters, including other Unicode whitespace,
  are not whitespace.
- **Comments**: `//` runs to the end of the line (the line break itself is left for the line counter);
  `/* … */` does not nest, and the search for `*/` starts after the opening `/*`, so `/*/` does not
  close itself. A lone `/` is the divide operator.
- **Identifiers**: an ASCII letter or `_` followed by ASCII letters, digits, and `_`. A special
  variable is `$` followed by one or more of the same characters (so `$fn` and `$1` are valid) and is
  an `Identifier` token whose text includes the `$`. Matching is case-sensitive and ordinal.
- **Keywords**: `module`, `function`, `if`, `else`, `for`, `intersection_for`, `let`, `each`,
  `assert`, `echo`, `true`, `false`, `undef`, matched on the whole identifier, so `iffy` and `for_` are
  identifiers. `include` and `use` are reserved words that introduce a directive token rather than an
  identifier, so they cannot be used as names.
- **Numbers**: decimal digits with an optional fraction and an optional exponent. The fraction may be
  leading (`.5`, recognized only when the dot is followed by a digit) or trailing (`5.`). The exponent
  `e`/`E` with an optional sign is consumed only when at least one digit follows; otherwise the letter
  is left to start the next token (`2e` is the number `2` and the identifier `e`). A number carries no
  sign (a leading `-` is the unary minus operator) and no base prefix, a letter directly after the
  digits starts a new token, and a value that overflows `double` becomes infinity instead of an error.
  The value is parsed with `double.Parse` and the invariant culture.
- **Strings**: delimited by `"`. A raw line break inside the literal is part of the value (kept
  verbatim and counted as a line). Escapes: `\n`, `\t`, `\r`, `\"`, `\\`, and the code point forms
  `\xHH` (two hexadecimal digits), `\uHHHH` (four), and `\UHHHHHH` (six). A code point above U+FFFF
  becomes a surrogate pair. Any other escape is an error.
- **Operators and punctuation**: longest match. The two-character operators are `==`, `!=`, `<=`, `>=`,
  `&&`, and `||`; the single characters are `( ) { } [ ] ; , . : ? = < > + - * / % ^ ! #`. A lone `&` or
  `|` is an error.
- **Directives**: after the keyword `include` or `use`, whitespace and comments are skipped, then `<`
  must follow; the path is every character up to the next `>` on the same line and is kept exactly
  (not trimmed, never empty).
- **Locations**: one-based; a column counts UTF-16 code units and a tab is one column. A leading
  U+FEFF is skipped without consuming a column; a U+FEFF anywhere else is an unexpected character.

#### Key Methods

- `ScadLexer.Tokenize(string source, string? file)` (public static) — checks that `source` is not null
  (`ArgumentNullException`), creates a single-use `ScadLexer`, and runs it. Returns the tokens in source
  order, always ending with one `EndOfFile` token. The `file` name is copied into every token location.
  Because each call uses a new instance and no shared mutable state, the entry point is safe for
  concurrent use; an instance is single-use and not thread-safe.
- `Run()` — the main loop: `SkipTrivia()`, stop at the end of the input, otherwise `ScanToken()`; then
  append the `EndOfFile` token at the current position. The lexer is iterative (never recursive), so
  hostile input of any shape cannot overflow the stack; the caller bounds the input size.
- `SkipTrivia()` / `SkipOneTrivia()` — consume whitespace, line breaks, and comments one unit at a
  time. `SkipBlockComment()` finds the terminator with `IndexOf("*/")` and then walks the body so that
  embedded line breaks advance the line counter.
- `TryConsumeLineBreak()` — the only place that increments the line counter and resets the column
  origin, so line and column tracking is uniform across trivia and string literals. A CR LF pair is one
  break.
- `ScanToken()` — dispatches on the first character: a digit, or a `.` followed by a digit, starts a
  number; `"` starts a string; an ASCII letter or `_` starts a word; `$` starts a special variable;
  anything else is an operator.
- `ScanWord()` — reads the identifier, routes `include`/`use` to `ScanDirective`, and otherwise looks
  the text up in the keyword table, falling back to `Identifier`.
- `ScanSpecialVariable()` — reads `$` plus identifier characters; a bare `$` is an error.
- `ScanDirective(word, location)` — implements the directive rule above and emits an `Include` or `Use`
  token whose text is the path and whose location is the keyword's.
- `ScanOperator()` — tries the two-character operators first (`TwoCharOperator`), then the
  single-character table, then reports an unexpected character.
- `ScanNumber()` / `ScanExponent()` (in `ScadLexer.Literals.cs`) — scan the integer part, an optional
  fraction, and an optional exponent that is taken only when digits follow, then convert the lexeme.
- `ScanString()`, `AppendRawCharacter()`, `AppendEscape()`, `ReadCodePoint()` (in
  `ScadLexer.Literals.cs`) — build the decoded value one character at a time. `ReadCodePoint` requires
  exactly the specified number of ASCII hexadecimal digits and rejects surrogate values
  (U+D800 to U+DFFF) and values above U+10FFFF.

**Guarantees**: the returned list is never empty, its last element is the only `EndOfFile` token,
every other token has a non-null `Text`, and token locations are strictly non-decreasing in source
order.

#### Error Handling

The lexer is fail-fast: the first lexical error throws a `ScadSyntaxException` whose message is
`{file}:{line}:{column}: {description}` (the prefix is shortened when the file name is null). No
recovery is attempted. The complete message catalogue follows; `c` stands for the offending character.

<!-- markdownlint-disable MD013 -->

| Condition | Message | Location |
| --------- | ------- | -------- |
| Block comment not closed | `Unterminated block comment` | the opening `/*` |
| String not closed (also when the input ends right after a backslash) | `Unterminated string literal` | the opening quote |
| Unknown escape letter | `Invalid escape sequence '\c'` | the backslash |
| Missing or non-hexadecimal digits | `Invalid escape sequence '\x': expected 2 hexadecimal digits` (`\u` expects 4, `\U` expects 6) | the backslash |
| Surrogate or above U+10FFFF | `Invalid escape sequence '\u': not a valid Unicode scalar value` | the backslash |
| `$` without a name | `Expected a name after '$'` | the `$` |
| Directive without `<` | `Expected '<' after 'include'` (or `'use'`) | the directive keyword |
| Directive path not closed on its line | `Unterminated include/use path` | the directive keyword |
| Directive path empty | `Empty include/use path` | the directive keyword |
| Character that cannot start a token | `Unexpected character 'c'` | the character |
| Lone `&` or `\|` | `Unexpected character '&'; did you mean '&&'?` (or `'\|'` / `'\|\|'`) | the character |

<!-- markdownlint-enable MD013 -->

A null `source` is a programming error and throws `ArgumentNullException`, which is not converted.
Source length is not checked here: `ScadParser.Parse(string, string?)` and `ScadDocument` enforce the
4,194,304-character limit before or while the text is read.

#### Dependencies

- **`ScadSourceLocation`** and **`ScadSyntaxException`** — the public location and error types of the
  `ScadDocument` unit (see _ScadDocument Unit Design_, `scad-document.md`).
- **.NET base class library** — `System.Globalization` (`double.Parse` and `int.Parse` with the
  invariant culture), `System.Text.StringBuilder` for string values, and `char.ConvertFromUtf32`.

The unit does not depend on `ScadParser`; the dependency runs the other way.

#### Callers

`ScadParser` is the only production caller: `ScadParser.Parse(string, string?)` calls
`ScadLexer.Tokenize` and hands the result to the token-list overload. The tests in
`test/DemaConsulting.CanvasNet.Scad.Tests/Language/ScadLexer*Tests.cs` call `Tokenize` directly through
`InternalsVisibleTo`.
