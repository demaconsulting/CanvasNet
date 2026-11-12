### PieParser

![MermaidPie Structure](MermaidPieView.svg)

The `PieParser` unit is the first software unit in the `Pie` subsystem. It comprises two source files in the
`DemaConsulting.CanvasNet.Mermaid.Pie` namespace: `PieParser.cs` (the entry point, the grammar, the value converters,
the database, the limits, and the error machinery, with the sealed record `PieSection` and the sealed class
`PieDiagram`) and `PieParser.Lexer.cs` (the readonly record struct `PieToken`, the token kinds, and the lexer rules and
matchers). `PieParser` is an `internal sealed partial` class, so the two files are one type; `PieDiagram` implements
the system's `IMermaidDiagram`. Both files carry the provenance header comment for the parts of Mermaid,
`@mermaid-js/parser`, and Langium they port.

#### Purpose

`PieParser` reads the preprocessed text of a pie chart into a `PieDiagram`, accepting exactly the language of the
Langium grammar of `@mermaid-js/parser` (`pie.langium` with `common.langium`), converting the values as its value
converters do, and keeping the sections, the flag, and the texts as Mermaid's pie database (`pieDb.ts` with
`populateCommonDb.ts` and `commonDb.ts`) keeps them. It exists so that every text means what it means to Mermaid, so
that an invalid text fails with Mermaid's diagnosis at the right place in the original file, and so that reading
untrusted text costs time linear in its length. `PieDiagram` is the parsed chart: the state of the database and the
metadata of the document.

The unit's behavior is specified by the `CanvasNetMermaid-Pie-PieParser-*` requirements in
`docs/reqstream/canvas-net-mermaid/pie/pie-parser.yaml`.

#### Data Model

**The token kinds** are constants whose values are the names of the Langium tokens, so that messages and
expected-token lists read like Mermaid's parse errors: the keywords `showData` (`ShowData`), `pie` (`PieKeyword`), and
`:` (`Colon`); the terminals `NUMBER_PIE` (`NumberPie`, the grammar's `FLOAT_PIE` or `INT_PIE`), `ACC_DESCR`,
`ACC_TITLE`, `TITLE`, `STRING` (`StringToken`), and `NEWLINE`; and `EOF`, the end of the tokens (chevrotain's `EOF`),
which the lexer adds once at the end of the text. **`PieToken`** is a token: its kind and the start and end of its text
in the parser text.

**The lexer rules** (`Rules`) are the 13 token types in the order Langium gives them to chevrotain, each with a
hand-written matcher in place of its regular expression; the first that matches at a position wins, as in chevrotain:

- **The keywords**, longest first: `showData`, `pie`, and `:`. A keyword matches only as written (`showdata` is no
  keyword) and only when it is followed by `%%`, by white space, or by the end of the text (the suffix
  `(?:(?=%%)|(?!\S))` of the token builder of `@mermaid-js/parser`).
- **The terminals in the order of the grammar**: `NUMBER_PIE` (`FLOAT_PIE` `-?[0-9]+\.[0-9]+(?!\.)`, else `INT_PIE`
  `-?(0|[1-9][0-9]*)(?!\.)`, solved exactly with the backtracking of the expressions, so `1e3` and `.5` are not
  numbers and `007` is the number `0` followed by more numbers, which the grammar refuses); `ACC_DESCR` (a colon and
  the rest of the line before any `%%`, or the text in braces up to the first `}`); `ACC_TITLE` (a colon and the rest
  of the line before any `%%`); `TITLE` (`title` followed by a tab or a space and the rest of the line before any `%%`,
  or `title` alone); `STRING` (a double- or single-quoted text in which a backslash escapes any character but a line
  terminator); and `NEWLINE` (`\r?\n`).
- **The hidden terminals**, which the parser never sees: white space (tabs and spaces), a `---` YAML block, a `%%{...}%%`
  directive, and a `%%` comment up to the end of its line.

**A parser instance** parses one text on one thread and holds the parser text of the source (a `MappedText`, whose
characters stay mapped to the original text), the `MermaidLimits` of the open call, the list of tokens, and the index of
the lookahead token.

**`PieSection`** is a section of Mermaid's `sections` map: the label as the string converter gives it, the value (zero
or more, possibly infinite, a negative zero kept), and the location of the section statement. `LegendText(showData)`
gives the text of its legend entry: the label, followed with `showData` by the value in brackets as JavaScript's
`String` writes the number (`a [1]`).

**`PieDiagram`** is immutable and safe for concurrent renders. It holds the location of the `pie` keyword
(`KeywordLocation`), `ShowData`, the sanitized title of the `title` statement (`DiagramTitle`), the sanitized accessible
title (`AccessibleTitle`) and description (`AccessibleDescription`), each empty when not set, and the sections in the
order they were added, each label once (`Sections`, Mermaid's `getSections`).

#### Key Methods

- **`Parse(PreprocessedSource, DetectedDiagram, MermaidLimits)`** creates a parser over the parser text of the source
  and runs it with the location of the diagram keyword; it returns the filled `PieDiagram`.
- **`Tokenize`** splits the parser text into tokens, trying the rules in order at each position, keeping the tokens of
  the kinds the grammar reads and skipping the hidden ones, and ending the list with one `EOF` token. A position where
  no rule matches is a lexical error. Every matcher looks at most to the end of its line, its closing brace, or the end
  of its YAML block or directive; the last closing that can end a directive is found once, before the tokens, so an
  opening after it fails at once instead of searching the rest of the text again, and the tokens are found in time
  linear in the text.
- **`Run`** reads the grammar `NEWLINE* "pie" showData? (TitleAndAccessibilities | PieSection | NEWLINE)* EOF` with
  one token of lookahead (the grammar is LL(1)), in a loop: line breaks; a section (`STRING`, `:`, `NUMBER_PIE`, and a
  line end), counted against `MaxNodes` before it is read; or one or more title, accessible title, and accessible
  description statements, each followed by a line end. The value of each statement is converted as it is read, and the
  last of each kind wins. After the statements the lookahead must be the end of the tokens. **`EndOfLine`** reads a line
  end (`NEWLINE+` or the end of the tokens).
- **`Populate`** fills the database from the parsed values as Mermaid's `populateDb` does: the title, the accessible
  title, and the accessible description are set only when they are not empty (`populateCommonDb` tests them for truth)
  and are sanitized; then each section in order is refused when its value is negative, kept when its label is new, and
  ignored otherwise (Mermaid's `addSection`). With `showData` the legend text of each kept section is checked against
  `MaxLabelLength`.
- **`ConvertString`** is Langium's `convertString`: the quotes removed, the escapes `\b`, `\f`, `\n`, `\r`, `\t`, `\v`,
  and `\0` replaced, and any other escaped character kept without its backslash.
- **`ConvertSingleLine`** converts a title, an accessible title, or a one-line accessible description as the value
  converter of `@mermaid-js/parser` does: the text after the keyword (and its colon) trimmed as JavaScript's `trim`
  trims it, each run of two or more tabs and spaces made one space. **`ConvertAccDescr`** chooses the one-line or the
  braced form of an accessible description.
- **`ConvertMultiLine`** converts the text in the braces of an accessible description as the converter's regular
  expressions do: the white space that starts each line removed (`/^\s*/gm`, line breaks in it included), the white
  space before each line end removed (`/\s+$/gm`), runs of two or more tabs and spaces made one space, and runs of two or
  more line breaks one line feed.
- **`PieDiagram.Sanitize`**, **`SanitizeAccTitle`**, and **`SanitizeDescription`** are the setters of Mermaid's
  `commonDb.ts`: the text sanitized with `HtmlSanitizer` (an empty text kept), the accessible title without its leading
  white space, and the description without the white space after each line feed.
- **Metadata** (`IMermaidDiagram`): `Title` is the title of the `title` statement or null (the document then falls back
  to the frontmatter title), `AccTitle` the accessible title or null, `Direction` null, `NodeCount` the number of
  sections (the legend entries), and `EdgeCount` and `SubgraphCount` 0.
- **`PieDiagram.Layout(DiagramRenderContext)`** creates a `PieRenderer` for the render and returns the scene it draws
  (see _PieRenderer Unit Design_).
- **`Is`**, **`Expect`**, **`Fail`**, **`TextOf`**, **`Checked`**, **`CheckDerived`**, and **`Locate`**: `Is` tests the
  lookahead, `Expect` consumes a token of a kind or fails, `Fail` builds the located exception for the lookahead with
  the token names chevrotain lists and a hint, `TextOf` gives the text of a token that is kept and checks it,
  `Checked` and `CheckDerived` check a converted or derived text against `MaxLabelLength`, and `Locate` maps a token to
  the original text.

#### Error Handling

- A token the grammar does not accept is a `MermaidSyntaxException` with code `mermaid-syntax-error`, located at the
  token in the original text, with the message `expected 'A' or 'B', found 'KIND' ('text')` (or `found the end of the
  text`), the expected token names as `Expected` (the names chevrotain lists: `pie` before the keyword; `:` after a
  label; `NUMBER_PIE` after the colon; `NEWLINE` and `EOF` after a statement; and `EOF` where no statement can start),
  and a hint: to start with the `pie` keyword, to separate the label and the value with a colon, to give the slice a
  value, to end a statement with a line break, or to write each statement on a line of its own.
- Text that no token matches is a lexical error, raised by `Tokenize` before the grammar runs, so it comes before any
  grammar error, as Mermaid lists the errors of its lexer first: a `MermaidSyntaxException` with code
  `mermaid-syntax-error`, located at the first character that is not matched, with the message `unrecognized text ...`,
  no expected tokens, and a hint for the form of the sections and statements. Such text is, for example, a number with
  an exponent or a leading dot, a keyword in another case, an unquoted label, or an unclosed string.
- A negative value is refused when the database is filled, after the whole text has been read (so a grammar error
  anywhere in the text comes first, as in Mermaid), with a `MermaidSyntaxException` with code `mermaid-syntax-error`,
  located at the section, with Mermaid's message `"<label>" has invalid value: <value>. Negative values are not allowed
  in pie charts. All slice values must be >= 0.` and a hint; a repeated label with a negative value is refused too.
- More section statements than `MaxNodes`, repeated labels included, are refused at the section one too many, with the
  message `the diagram has more than N sections (MermaidLimits.MaxNodes)`; a text longer than `MaxLabelLength` is
  refused with the message `a text is N characters long, more than the limit of M (MermaidLimits.MaxLabelLength)`;
  both with code `mermaid-limit-exceeded` and a hint. The parser checks the text of every token it keeps (a quoted label
  with its quotes, a number, and a title or accessibility statement), every converted value (located at its token),
  every sanitized title and accessibility text (located at the diagram keyword), and, with `showData`, every legend text
  with its value (located at its section).
- `Parse` throws `ArgumentNullException` for a null argument, and so does `Layout`.

#### Dependencies

- **PieRenderer** - created by `PieDiagram.Layout` to draw a render.
- **Syntax subsystem** - `PreprocessedSource` and `MappedText` (the text mapped to the original), `DetectedDiagram`,
  `Preprocessor.TrimJs` (JavaScript's `trim`), `ScanPatterns` (the JavaScript white space and line terminator classes),
  `HtmlSanitizer` (Mermaid's `sanitizeText`), `SourceScanner.Describe` (the description of unmatched text),
  `TokenCursor.JoinAlternatives` (the list of expected tokens in a message), and `YamlSubsetParser.FormatJsNumber`
  (JavaScript's `String(number)`).
- **Scenegraph subsystem** - `Scene`, the result of a layout.
- **System** - `IMermaidDiagram`, `DiagramRenderContext`, `MermaidLimits`, `MermaidLocation`, `MermaidSyntaxException`,
  and `MermaidDiagnosticCodes`.

#### Callers

- The document pipeline of the system (`MermaidDocument.Pipeline.cs`): its `DiagramRegistry` calls `Parse` for a text
  whose detected type is `pie`, with the limits of the open call, reads the metadata of the `PieDiagram`, and calls its
  `Layout` for every measure and render.
- **PieRenderer** - reads the sections, `ShowData`, the title, and the keyword location of the `PieDiagram`, the legend
  texts of the `PieSection`s, and `PieDiagram.Sanitize` for the frontmatter title.
- The unit and subsystem tests call `Parse` and the converters directly, and the vector tests compare the state of the
  diagram with Mermaid's database.

#### Known Differences From Mermaid

- **No regular expressions.** The lexer is a set of hand-written matchers with the same matches as Langium's regular
  expressions, so lexing is linear in the text.
- **A hand-written LL(1) parser instead of chevrotain's.** The parser accepts the same language with one token of
  lookahead and lists the expected tokens that chevrotain lists; the grammar errors of the upstream vectors give the
  identical lists at the identical places.
- **The first error only, located.** Mermaid's message lists every error of its lexer and then every error of its
  parser; this unit reports the first of them, located by line and column in the original text, with a hint.
- **Limits.** The node and label limits refuse the text when the document is opened, with code
  `mermaid-limit-exceeded`; Mermaid has no such limits.
- **A negative value is a located syntax error.** Mermaid's database throws a plain error with the same message; this
  unit raises it as a located `MermaidSyntaxException` with a hint.
- **Accessibility.** `accTitle` and `accDescr` are metadata of the diagram only, since a rendered picture has no SVG
  to carry them.
