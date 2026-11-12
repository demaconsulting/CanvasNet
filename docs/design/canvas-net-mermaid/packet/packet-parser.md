### PacketParser

![MermaidPacket Structure](MermaidPacketView.svg)

The `PacketParser` unit is the first software unit in the `Packet` subsystem. It comprises two source files in the
`DemaConsulting.CanvasNet.Mermaid.Packet` namespace: `PacketParser.cs` (the entry point, the grammar, the value
converters, the database, the limits, and the error machinery, with the sealed records `ParsedBlock` and `PacketBlock`
and the sealed class `PacketDiagram` with its row split) and `PacketParser.Lexer.cs` (the readonly record struct
`PacketToken`, the token kinds, and the lexer rules and matchers). `PacketParser` is an `internal sealed partial`
class, so the two files are one type; `PacketDiagram` implements the system's `IMermaidDiagram`. Both files carry the
provenance header comment for the parts of Mermaid, `@mermaid-js/parser`, and Langium they port.

#### Purpose

`PacketParser` reads the preprocessed text of a packet diagram into a `PacketDiagram`, accepting exactly the language
of the Langium grammar of `@mermaid-js/parser` (`packet.langium` with `common.langium`), converting the values as its
value converters do, and filling in and checking the blocks as Mermaid's packet database does (the `populate` of
`parser.ts` with `db.ts`, `populateCommonDb.ts`, and `commonDb.ts`). `PacketDiagram` is the parsed diagram: the state of
the database, the metadata of the document, and the split of the blocks into rows (`Rows`, the rest of `populate` with
`getNextFittingBlock`), which depends on the row size of a render. The unit exists so that every text means what it
means to Mermaid, so that an invalid text fails with Mermaid's diagnosis at the right place in the original file, so
that the rows are Mermaid's for every row size, and so that reading untrusted text costs time linear in its length.

The unit's behavior is specified by the `CanvasNetMermaid-Packet-PacketParser-*` requirements in
`docs/reqstream/canvas-net-mermaid/packet/packet-parser.yaml`.

#### Data Model

**The token kinds** are constants whose values are the names of the Langium tokens, so that messages and
expected-token lists read like Mermaid's parse errors: the keywords `packet-beta` (`PacketBetaKeyword`), `packet`
(`PacketKeyword`), `-` (`Dash`), `+` (`Plus`), and `:` (`Colon`); the terminals `ACC_DESCR`, `ACC_TITLE`, `TITLE`,
`INT` (`Int`), `STRING` (`StringToken`), and `NEWLINE`; and `EOF`, the end of the tokens (chevrotain's `EOF`), which
the lexer adds once at the end of the text. **`PacketToken`** is a token: its kind and the start and end of its text in
the parser text.

**The lexer rules** (`Rules`) are the token types in the order Langium gives them to chevrotain, each with a
hand-written matcher in place of its regular expression; the first that matches at a position wins, as in chevrotain:

- **The keywords**, longest first: `packet-beta`, `packet`, `-`, `+`, and `:`. The token builder of
  `@mermaid-js/parser` gives only `packet` the suffix `(?:(?=%%)|(?!\S))`, so `packet` matches only when it is followed
  by `%%`, by white space, or by the end of the text, while `packet-beta` matches before any character.
- **The terminals in the order of the grammar**: `ACC_DESCR` (a colon and the rest of the line before any `%%`, or the
  text in braces up to the first `}`); `ACC_TITLE` (a colon and the rest of the line before any `%%`); `TITLE` (`title`
  followed by a tab or a space and the rest of the line before any `%%`, or `title` alone); `INT`
  (`0|[1-9][0-9]*(?!\.)`, solved exactly with the backtracking of the expression, so `05` is the number `0` followed by
  the number `5`, and the digits before a dot give back their last digit, which leaves the dot as the first character
  no token matches); `STRING` (a double- or single-quoted text, which may span lines, in which a backslash escapes any
  character but a line terminator); and `NEWLINE` (`\r?\n`). The terminals `BOOLEAN`, `FLOAT`, `NUMBER`, and `ID` of
  `common.langium` are not reached by the grammar and are not in the lexer.
- **The hidden terminals**, which the parser never sees: white space (tabs and spaces), a `---` YAML block, a `%%{...}%%`
  directive, and a `%%` comment up to the end of its line.

**A parser instance** parses one text on one thread and holds the parser text of the source (a `MappedText`, whose
characters stay mapped to the original text), the `MermaidLimits` of the open call, the list of tokens, and the index of
the lookahead token.

**`ParsedBlock`** is a block statement as written: its start bit, its end bit, and its bit count where they are given
(null where they are not), its label as the string converter gives it, and the location of the statement.
**`PacketBlock`** is a block of the database, or a part of one in a row: its first and last bit, its bit count as
Mermaid keeps it (a part that the split makes has Mermaid's count, one less than its bits), its label, and the location
of its statement. The numbers are doubles, as in JavaScript.

**`PacketDiagram`** is immutable and safe for concurrent renders. It holds the location of the diagram keyword
(`KeywordLocation`), the sanitized title of the `title` statement (`DiagramTitle`), whether a `title` statement with a
value set it (`IsTitleSet`), the sanitized accessible title (`AccessibleTitle`) and description
(`AccessibleDescription`), the sanitized frontmatter title (`FrontmatterTitle`), each empty when not set, and the blocks
in order with their starts, ends, and bit counts filled in (`Blocks`). `DrawnTitle` is the title Mermaid draws: the
diagram's title when `IsTitleSet`, even one that sanitizes to empty, else the frontmatter title. `MaxPacketSize` is
Mermaid's cap of 10,000 rows.

#### Key Methods

- **`Parse(PreprocessedSource, DetectedDiagram, MermaidLimits)`** creates a parser over the parser text of the source
  and runs it with the location of the diagram keyword and the frontmatter; it returns the filled `PacketDiagram`.
- **`Tokenize`** splits the parser text into tokens, trying the rules in order at each position, keeping the tokens of
  the kinds the grammar reads and skipping the hidden ones, and ending the list with one `EOF` token. A position where
  no rule matches is a lexical error. Every matcher looks at most to the end of its line, its closing quote or brace, or
  the end of its YAML block or directive; the last closing that can end a directive is found once, before the tokens,
  so the tokens are found in time linear in the text.
- **`Run`** reads the grammar
  `NEWLINE* ("packet" | "packet-beta") (TitleAndAccessibilities | PacketBlock | NEWLINE)* EOF` with one token of
  lookahead (the grammar is LL(1)), in a loop: line breaks; a block (`Block`); or one or more title, accessible title,
  and accessible description statements, each followed by a line end. The value of each statement is converted as it is
  read, and the last of each kind wins. After the statements the lookahead must be the end of the tokens.
  **`EndOfLine`** reads a line end (`NEWLINE+` or the end of the tokens).
- **`Block`** reads a block statement, `(INT ("-" INT)? | "+" INT) ":" STRING EOL`, counted against `MaxNodes` before
  it is read, into a `ParsedBlock`; **`IntValue`** converts an `INT` as Langium's `convertInt` does (JavaScript's
  `parseInt`: the nearest double of the digits).
- **`Populate`** fills the database as Mermaid's `populateCommonDb` and the checks of `populate` do: the title, the
  accessible title, and the accessible description are set only when they are not empty and are sanitized, and the
  frontmatter title is sanitized; then each block in order is refused when it has a start and an end and the end is
  before the start, when its start (the bit after the last block's end when it has none) is not the bit after the last
  block's end, and when its bit count is zero, else filled in (a missing end is the start plus the bit count, 1 by
  default, less one; a missing bit count is the end less the start plus one) and kept.
- **`ConvertString`** is Langium's `convertString`: the quotes removed, the escapes `\b`, `\f`, `\n`, `\r`, `\t`, `\v`,
  and `\0` replaced, and any other escaped character kept without its backslash. The label is kept as converted, not
  sanitized (Mermaid sets it as SVG text).
- **`ConvertSingleLine`** and **`ConvertMultiLine`** convert a title, an accessible title, or an accessible description
  as the common value converter of `@mermaid-js/parser` does (the one-line form trimmed as JavaScript's `trim` trims it,
  each run of two or more tabs and spaces made one space; the lines of the braced form trimmed and its blank lines
  removed). **`ConvertAccDescr`** chooses the one-line or the braced form.
- **`PacketDiagram.Sanitize`**, **`SanitizeAccTitle`**, and **`SanitizeDescription`** are the setters of Mermaid's
  `commonDb.ts`: the text sanitized with `HtmlSanitizer` (an empty text kept), the accessible title without its leading
  white space, and the description without the white space after each line feed.
- **`PacketDiagram.Rows(double, MermaidLimits, LayoutWorkMeter)`** is the split of Mermaid's `populate`: for each block
  (charging the meter 1), while the row being filled has at most `bitsPerRow + 1` parts and there are fewer than 10,000
  rows, it takes the part of the block that fits the row with **`NextFittingBlock`** (charging the meter 1 and counting
  the part against `MaxNodes`), adds it to the row, completes the row when the part ends at the row's last bit
  (JavaScript's `===`, `JsEquals`), and goes on with the rest of the block; the last row, when not empty, is added at
  the end. `NextFittingBlock` is `getNextFittingBlock`: a block that ends within the row fits whole; otherwise it is cut
  at the row's last bit (`row * bitsPerRow - 1`) and goes on at the next row's first bit, the bit counts of both parts
  one less than their bits, as in Mermaid. All arithmetic is in doubles, as in JavaScript.
- **Metadata** (`IMermaidDiagram`): `Title` is the title of the `title` statement or null (the document then falls back
  to the frontmatter title), `AccTitle` the accessible title or null, `Direction` null, `NodeCount` the number of block
  statements, and `EdgeCount` and `SubgraphCount` 0.
- **`PacketDiagram.Layout(DiagramRenderContext)`** creates a `PacketRenderer` for the render and returns the scene it
  draws (see _PacketRenderer Unit Design_).
- **`Is`**, **`Expect`**, **`Fail`**, **`TextOf`**, **`Checked`**, **`CheckDerived`**, and **`Locate`**: `Is` tests the
  lookahead, `Expect` consumes a token of a kind or fails, `Fail` builds the located exception for the lookahead with
  the token names chevrotain lists and a hint, `TextOf` gives the text of a token that is kept and checks it,
  `Checked` and `CheckDerived` check a converted or derived text against `MaxLabelLength`, and `Locate` maps a token to
  the original text.

#### Error Handling

- A token the grammar does not accept is a `MermaidSyntaxException` with code `mermaid-syntax-error`, located at the
  token in the original text, with the message `expected 'A' or 'B', found 'KIND' ('text')` (or `found the end of the
  text`), the expected token names as `Expected` (the names chevrotain lists: `packet` and `packet-beta` before the
  keyword; `INT` after a `+` or a `-`; `:` after the bits; `STRING` after the colon; `NEWLINE` and `EOF` after a
  statement; and `EOF` where no statement can start), and a hint.
- Text that no token matches is a lexical error, raised by `Tokenize` before the grammar runs, so it comes before any
  grammar error, as Mermaid lists the errors of its lexer first: a `MermaidSyntaxException` with code
  `mermaid-syntax-error`, located at the first character that is not matched, with the message `unrecognized text ...`,
  no expected tokens, and a hint. Such text is, for example, a fraction (`10.5`), an unquoted label, or an unclosed
  string.
- A block the database refuses is refused when the database is filled, after the whole text has been read (so a
  grammar error anywhere in the text comes first, as in Mermaid), with a `MermaidSyntaxException` with code
  `mermaid-syntax-error`, located at the block statement, with Mermaid's message and a hint: `Packet block S - E is
  invalid. End must be greater than start.`, `Packet block S - E is not contiguous. It should start from N.` (a single
  bit written as `S - S`), or `Packet block S is invalid. Cannot have a zero bit field.`, the numbers written as
  JavaScript writes them.
- More block statements than `MaxNodes` are refused at the statement one too many, with the message `the diagram has
  more than N blocks (MermaidLimits.MaxNodes)`; a text longer than `MaxLabelLength` is refused with the message `a text
  is N characters long, more than the limit of M (MermaidLimits.MaxLabelLength)`; both with code
  `mermaid-limit-exceeded` and a hint. The parser checks the text of every token it keeps (a number, a quoted label with
  its quotes, and a title or accessibility statement), every converted value (located at its token), every sanitized
  title and accessibility text (located at the diagram keyword), and the sanitized frontmatter title (located at the
  frontmatter).
- `Rows` throws a `MermaidRenderException` located at the block statement: with code `mermaid-render-failed` and
  Mermaid's message `Block start S is greater than block end E.` when a part would start after its end (as
  `getNextFittingBlock` throws for some fractional row sizes), and with code `mermaid-limit-exceeded` when the parts
  exceed `MaxNodes`; the meter throws `mermaid-limit-exceeded` at the work limit and `OperationCanceledException` when
  the render is canceled. All propagate.
- `Parse` throws `ArgumentNullException` for a null argument, and so do `Rows` and `Layout`.

#### Dependencies

- **PacketRenderer** - created by `PacketDiagram.Layout` to draw a render.
- **Syntax subsystem** - `PreprocessedSource`, `MappedText`, and `FrontmatterData` (the text mapped to the original and
  the frontmatter title), `DetectedDiagram`, `Preprocessor.TrimJs` (JavaScript's `trim`), `ScanPatterns` (the
  JavaScript white space and line terminator classes), `HtmlSanitizer` (Mermaid's `sanitizeText`),
  `SourceScanner.Describe` (the description of unmatched text), `TokenCursor.JoinAlternatives` (the list of expected
  tokens in a message), and `YamlSubsetParser.FormatJsNumber` (JavaScript's `String(number)`).
- **Dagre subsystem** - `LayoutWorkMeter`, charged by the row split.
- **Scenegraph subsystem** - `Scene`, the result of a layout.
- **System** - `IMermaidDiagram`, `DiagramRenderContext`, `MermaidLimits`, `MermaidLocation`, `MermaidSyntaxException`,
  `MermaidRenderException`, and `MermaidDiagnosticCodes`.

#### Callers

- The document pipeline of the system (`MermaidDocument.Pipeline.cs`): its `DiagramRegistry` calls `Parse` for a text
  whose detected type is `packet`, with the limits of the open call, reads the metadata of the `PacketDiagram`, and
  calls its `Layout` for every measure and render.
- **PacketRenderer** - calls `Rows` with the configuration and the limits of the render, and reads `DrawnTitle`,
  `KeywordLocation`, and the parts of the rows.
- The unit and subsystem tests call `Parse`, `Rows`, and the converters directly, and the vector tests compare the state
  of the diagram and its rows with Mermaid's database.

#### Known Differences From Mermaid

- **No regular expressions.** The lexer is a set of hand-written matchers with the same matches as Langium's regular
  expressions, so lexing is linear in the text.
- **A hand-written LL(1) parser instead of chevrotain's.** The parser accepts the same language with one token of
  lookahead and lists the expected tokens that chevrotain lists; the grammar errors of the upstream vectors give the
  identical lists at the identical places.
- **The first error only, located.** Mermaid's message lists every error of its lexer and then every error of its
  parser; this unit reports the first of them, located by line and column in the original text, with a hint.
- **Every block is checked before the split.** Mermaid checks each block just before it splits it, so with a row size
  that makes its split fail (a fractional `bitsPerRow`) it can fail at an earlier split before it reaches an invalid
  block; this unit checks every block when the text is read, so the invalid block is always the
  `mermaid-syntax-error`.
- **Limits.** The node and label limits refuse the text when the document is opened, and `MaxNodes` also bounds the
  parts of the rows when the diagram is drawn, with code `mermaid-limit-exceeded`; Mermaid has no such limits (only its
  own cap of 10,000 rows, which is kept).
- **Database errors are located syntax errors.** Mermaid's database throws plain errors with the same messages; this
  unit raises them as located `MermaidSyntaxException`s with hints, and the failure of the split as a located
  `MermaidRenderException`.
- **Accessibility.** `accTitle` and `accDescr` are metadata of the diagram only, since a rendered picture has no SVG
  to carry them.
