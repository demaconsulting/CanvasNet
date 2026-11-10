### ErParser

![MermaidEr Structure](MermaidErView.svg)

The `ErParser` unit is the first software unit in the `Er` subsystem. It comprises two source files in the
`DemaConsulting.CanvasNet.Mermaid.Er` namespace: `ErParser.cs` (the entry point, the grammar, the lookahead sets of
jison's states, the values of the statements, the limits, and the error machinery) and `ErParser.Lexer.cs` (the static
`ErTokens` class with the token names and jison's terminal numbers, and the lexer rules, start conditions, and
matchers). `ErParser` is an `internal sealed partial` class, so the two files are one type; `ErTokens` is an
`internal static` class. Both files carry the provenance header comment for the parts of Mermaid they port.

#### Purpose

`ErParser` reads the preprocessed text of an entity relationship diagram into an `ErModel`, accepting exactly the
language of the generated parser of Mermaid's `erDiagram.jison` and calling the operations of the ER database at the
points where Mermaid's grammar runs its actions, in the same order. It exists so that every text means what it means to
Mermaid, so that an invalid text fails with Mermaid's diagnosis at the right place in the original file, and so that
reading untrusted text costs time linear in its length.

The unit's behavior is specified by the `CanvasNetMermaid-Er-ErParser-*` requirements in
`docs/reqstream/canvas-net-mermaid/er/er-parser.yaml`.

#### Data Model

**`ErTokens`** holds the token kinds as constants whose values are the names of the terminals of `erDiagram.jison`
(`ER_DIAGRAM`, `NEWLINE`, `COLON`, `STYLE_SEPARATOR`, `BLOCK_START`, `BLOCK_STOP`, `SQS`, `SQE`, the accessibility
tokens `acc_title`, `acc_title_value`, `acc_descr`, `acc_descr_value`, and `acc_descr_multiline_value`, `SUBGRAPH`,
`END`, the direction tokens `direction_tb`, `direction_bt`, `direction_rl`, and `direction_lr`, `CLASSDEF`, `CLASS`,
`STYLE`, `STYLE_TEXT`, `COMMA`, `SEMI`, `BRKT`, the names `UNICODE_TEXT`, `NUM`, `ENTITY_NAME`, `DECIMAL_NUM`, and
`ENTITY_ONE`, the attribute tokens `ATTRIBUTE_WORD`, `ATTRIBUTE_KEY`, and `COMMENT`, the cardinalities `ZERO_OR_ONE`,
`ZERO_OR_MORE`, `ONE_OR_MORE`, `ONLY_ONE`, and `MD_PARENT`, the relationship types `NON_IDENTIFYING` and
`IDENTIFYING`, and the quoted `WORD`), so that messages and expected-token lists read like Mermaid's parse errors.
Three kinds are special:

- `Eof` is the grammar's `EOF` terminal, which the end-of-input rule of the lexer produces once at the end of the
  text; it is named `EOF` in messages but must be told apart from the end of the token stream (jison's `$end`).
- Two lexer rules return the character they match as the kind (jison's `yytext[0]`), so `?` and `,` are the terminals
  of an optional type and of a key list inside an attribute block, and any other single character, in a block or in
  the initial condition, is a token that the grammar never accepts, so a parse error, as in Mermaid.
- `title` and `title_value` are terminals of the grammar that no lexer rule produces: they appear only in expected
  lists, and the grammar's `title` statement cannot be reached, as in Mermaid (a `title x` line is two entities).

`JisonName` gives the name a kind is printed with, and `Number` gives jison's terminal number, which orders
expected-token lists as jison prints them.

**A parser instance** parses one text on one thread and holds:

- the `ErModel` it fills and the `MermaidLimits` of the open call;
- the scanner built from the lexer rules and the token cursor over it (one token of lookahead);
- the current nesting depth of subgraphs (Mermaid's `subgraphDepth`);
- the cached line searches of the lexer: the last match of each direction rule and the last tilde of the current line,
  and the current run of characters that are not white space with its last tilde before that one (the rules are
  created per parser for that reason).

**Lexer tables.** The 83 rules of the generated lexer are ported one to one, in jison's order, each with a hand-written
matcher in place of its regular expression and the action of the rule (a token kind, a change of start condition, both,
or nothing for skipped white space); the 7 start conditions (`style`, `acc_descr_multiline`, `acc_descr`, `acc_title`,
`block_bq`, `block`, and the initial one) list the indexes of their rules in the order jison tries them, the first
matching rule winning. The grammar is case-insensitive: ASCII letters match in any case, and no character outside ASCII
folds to one inside it, as in JavaScript. Every rule that ends with a word character gets jison's implicit word
boundary (`\b`), so the keywords, `one`, `only one`, `many`, `to`, and `optionally to` are tokens only as whole words
(`onerous`, `manyfold`, and `toaster` are names). Mermaid's quirks are kept:

- **The cardinality order.** The cardinalities and relationship lines are tried in jison's order, before names:
  `one or zero`, `one or more`, `one or many`, `1+`, `|o`, `zero or one`, `zero or more`, `zero or many`, `0+`, `}o`,
  `many(0)`, `many(1)`, `many`, `}|`, `one`, and `only one`; then a decimal number; a `1` before white space and a
  letter, an underscore, or a quote, a `1` before white space and a digit, and a `1` directly before a relationship
  line (each `ONLY_ONE`, only the `1` consumed); a lone `1` as a name (`ENTITY_ONE`); a number; `||`, `o|`, `o{`,
  and `|{`; a `u` directly before `.`, `-`, or `|` (`MD_PARENT`, so `user` is a name); and the lines `..`, `--`, `to`,
  `optionally to`, `.-`, and `-.`. The grammar reads the left cardinality of a relationship as its second one
  (`cardB`) and the right one as its first (`cardA`), as Mermaid's grammar swaps them.
- **Direction lines.** A line that has `direction TB`, `BT`, `RL`, or `LR` anywhere in it from the current position on
  is one direction token, up to the line feed after the direction, because jison's rule starts with `.*`; a
  relationship followed by `%% direction LR` on its line is therefore a direction statement. The last occurrence on the
  line is found once and cached per line, so the far-looking rule stays linear.
- **The generic type rule** `([^\s]*)[~].*[~]([^\s]*)` is solved exactly: in an attribute block, a word whose run of
  characters that are not white space holds a tilde with another tilde after it on the line is one attribute word up
  to the last tilde of the line and the characters after it that are not white space, so `List~int~ a, Map~x~ b` is
  the type `List~int~ a, Map~x~` and the name `b`. The last tilde of the line and the current run are cached, so a long
  line stays linear.
- **The other rules** are kept as they match: the keys `PK`, `FK`, and `UK` in any case between word boundaries (so
  `aPK` is no key); `end` with the white space after it, line breaks included; a quoted name without `%`, a backslash,
  or a line break, while a quoted role or comment may span lines; a backticked attribute word of any characters but a
  backtick; and, in a `style` or `classDef` statement, only words, colons, commas, `#`, `;`, and the line feed that
  ends the statement.

**The parser's lookahead sets** (`StatementStarts`, `TopLineStarts`, `LineFollowers`, `NameFollowers`,
`EntityFollowers`, `IdFollowers`, `CardinalityFollowers`, `StyleComponents`, `StyleIdFollowers`, `ComponentFollowers`,
`StylesFollowers`, `Separators`, `SubgraphIdFollowers`, `TitleFollowers`, `AliasFollowers`, `AliasClassFollowers`,
`EntityClassFollowers`, `RoleClassFollowers`, `SecondEntityFollowers`, `AttributeStarts`, `TypeFollowers`,
`AttributeNameFollowers`, and `KeyFollowers`, built from the token groups `Names`, `Cardinalities`,
`RelationshipTypes`, `Roles`, and `Ids`) are the lookahead sets of the states of jison's LALR(1) tables, including the
merged sets on which several states reduce (after an entity name, an id, a cardinality, a style component, or a line),
so that a token is refused in the state where jison refuses it, with jison's list of expected tokens. A top-level line
that starts with `END` is refused with the 19 tokens of `TopLineStarts`, while the lookahead after any line, where
jison merges the states that reduce a line, accepts `END` too (the 20 tokens of `LineFollowers`).

**The value of a document** (`ErDocument`) is the list of the values of its statements (`Items`) and its last
direction statement (`Direction`), which a subgraph hands to the model as its nodes and its direction. The values are
Mermaid's: the entity names of an entity statement and of both sides of a relationship, the id of a nested subgraph,
the trimmed text of an accessibility statement, and the keyword of a `classDef`, `class`, or `style` statement as
written. A direction statement inside a subgraph sets the subgraph's direction, the last one winning, and gives no
value; at the top level it sets the diagram's direction.

#### Key Methods

- **`Parse(PreprocessedSource, DetectedDiagram, MermaidLimits)`** creates the model (with the limits and the location
  of the diagram keyword) and a parser, runs the grammar over the parser text of the source, whose characters stay
  mapped to the original text, and completes the model (`ErModel.Complete`). The database operations run while the
  text is parsed, so the returned model is complete.
- **`Run`** reads the `erDiagram` keyword and the top-level document; a keyword line that also holds a direction lexes
  as one direction token and is refused with `ER_DIAGRAM` expected, as in Mermaid. **`ParseDocument`** reads line
  breaks and statements up to, at the top level, the grammar's `EOF` followed by the end of the tokens, or, in a
  subgraph, its `END`; inside a subgraph the grammar's `EOF` is only an empty line, so an unclosed subgraph fails at
  the end of the tokens. **`ParseStatement`** dispatches on the first token: the accessibility statements (trimmed and
  handed to the model), a direction, `classDef` (`AddClass`), `class` (`SetClass`), `style` (`AddCssStyles`),
  `subgraph`, and otherwise a statement that starts with an entity name; it adds the statement's value to the
  document.
- **`ParseEntityStatement`** reads an entity name, adds it to the document, and then reads an alias
  (**`ParseAliasedEntity`**: `[alias]`, then optional `:::` classes and an optional attribute block), or optional
  `:::` classes followed by a relationship or by an optional attribute block, and calls `AddEntity`, `AddAttributes`,
  and `SetClass` in jison's order.
- **`ParseRelationship`** reads the left cardinality, the relationship type, the right cardinality, the second entity
  with optional `:::` classes, a colon, and the role (one `UNICODE_TEXT` word as written, or a quoted `ENTITY_NAME` or
  `WORD` without its quotes); the colon and the role are required. It then calls `AddEntity` for both entities,
  `AddRelationship` with `ErRelationshipSpec(right, type, left)`, and `SetClass` for the first and then the second
  entity. Only the first word of an unquoted role is the role; the words after it start new entity statements, as in
  Mermaid.
- **`ParseSubgraph`** reads `subgraph`, the id (an entity name), an optional title in brackets (one or more entity
  names, quoted ones without their quotes, joined with spaces), and the separator (a line break, `;`, or the grammar's
  `EOF`); with the header reduced, it enters one more level (checked against `MaxNestingDepth`), reads the body as a
  document up to `end`, leaves the level, and calls `AddSubgraph` with the values and the direction of the body,
  returning the trimmed id as the subgraph's own value. Only this method recurses.
- **`ParseEntityName`** reads one of the five name tokens (a quoted name without its quotes); **`ParseIdList`** reads
  ids separated by commas, in a loop; **`ParseStyles`** reads the styles of a `classDef` or `style` statement
  separated by commas, each the concatenation of its components without the white space between them (so
  `stroke-dasharray: 5 5` is `stroke-dasharray:55`, as in Mermaid), and the separator after them.
- **`ParseBlock`** reads an attribute block from `{` to `}`, and **`ParseAttribute`** one attribute: a type (with an
  optional `?` joined to it), a name, keys separated by commas, and a comment without its quotes, as an
  `ErAttribute`.
- **`CheckLookahead`**, **`Is`**, **`IsEnd`**, **`Advance`**, **`Expect`**, **`ExpectAny`**, and **`Fail`**:
  `CheckLookahead` refuses a token outside the set a state reduces on, `Is` tests the lookahead, `IsEnd` tests for the
  end of the tokens, `Advance` consumes a token, `Expect` and `ExpectAny` consume a token of a kind or fail with all
  the kinds jison lists there, and `Fail` builds the located exception with the expected kinds sorted by jison's
  terminal numbers and a hint chosen from them (`HintFor`). **`Lex`** runs a step that reads a token and gives a
  lexical error of the scanner its ER message and hint.
- **`TextOf`**, **`CheckDerived`**, and **`CheckLength`** check `MaxLabelLength` for the text of every token the parser
  keeps and for every text it joins from tokens.

#### Error Handling

- A token the grammar does not accept is a `MermaidSyntaxException` with code `mermaid-syntax-error`, located at the
  token in the original text, with the message `expected 'A', 'B' or 'C', found 'KIND' ('text')` (or `found the end
  of the text`), the expected token names as `Expected` (jison's names, `EOF` for the grammar's end-of-input token, in
  jison's order), and a hint: the colon and role that end a relationship, a role of one word or in double quotes
  (`to`, `one`, and `many` being keywords), the form of an attribute, `end` for an unclosed subgraph, the quoting of
  names with other characters and the end of a statement at its line break (at an unexpected quoted word or
  character), and otherwise a check of the statement; a text that does not start with `erDiagram` is refused with a
  hint to start with the keyword.
- Text that no lexer rule matches is a located lexical error raised by the scanner of the `Syntax` subsystem. It can
  occur only in a `style` or `classDef` statement, whose start condition has no rule for any character (such as a `.`
  or a `%` in a style); the parser rewrites its message as `unrecognized text ...` with a hint for the form of the
  styles.
- Subgraphs nested deeper than `MaxNestingDepth` are refused at the `subgraph` keyword of the one too deep, with the
  message `subgraphs are nested deeper than N levels (MermaidLimits.MaxNestingDepth)`, and a text longer than
  `MaxLabelLength` at its first token, with the message `a text is N characters long, more than the limit of M
  (MermaidLimits.MaxLabelLength)`, both with code `mermaid-limit-exceeded`. The parser checks every token text it keeps
  (an entity name, a quoted name as written, an alias, a role, an id or class name of a `:::`, `class`, `classDef`, or
  `style` statement, an attribute type, name, backticked word, key, and comment, a style component, a subgraph id and
  the words of its title, and the accessibility texts) and every text it joins from tokens (a style, counted while it
  grows, a bracketed subgraph title, and an attribute type with its `?`). The texts that the model joins or derives
  from these are checked by the model (see _ErModel Unit Design_).
- The errors of the model while a statement is applied (the `MaxNodes` and `MaxEdges` limits and the `MaxLabelLength`
  checks of the texts it joins) propagate unchanged. Because each operation runs after the lookahead that decides its
  reduction has been checked, grammar and limit errors come in Mermaid's order.
- `Parse` throws `ArgumentNullException` for a null argument.

#### Dependencies

- **ErModel** - the database the operations are applied to, the records `ErAttribute` and `ErRelationshipSpec`, and
  the completion of the model (`Complete`).
- **Syntax subsystem** - `SourceScanner`, `ScanRule`, `ScanMatcher`, `ScanToken`, and `TokenCursor` (the scanner and the
  cursor), `ScanPatterns` (the JavaScript white space, word, and line terminator classes, the run matcher, and the end
  of the input), `MappedText` and `PreprocessedSource` (the text mapped to the original), `DetectedDiagram`, and
  `Preprocessor.TrimJs` (JavaScript's `trim`).
- **System** - `MermaidLimits`, `MermaidLocation`, `MermaidSyntaxException`, and `MermaidDiagnosticCodes`.

#### Callers

- The document pipeline of the system (`MermaidDocument.Pipeline.cs`): its `DiagramRegistry` calls `Parse` for a text
  whose detected type is `er`, with the limits of the open call.
- The unit and subsystem tests call `Parse` directly (through the test helper `ErFacts`).

#### Known Differences From Mermaid

- **No regular expressions.** The lexer is a set of hand-written matchers with the same matches as Mermaid's regular
  expressions; the rules that look far ahead in Mermaid (a direction anywhere on a line and the last tilde of a line)
  are matched once per line and cached, so reading a text stays linear in its length.
- **Recursive descent instead of LALR(1) tables.** The parser accepts the same language with one token of lookahead
  and emulates the expected-token lists of jison's tables, including the merged lookahead of its states; the grammar
  errors of the upstream vectors give the identical lists in jison's order.
- **Located errors.** Mermaid's parse errors name a line of the text that its parser sees, and jison reports the line
  after the lookahead was read; this unit's errors are located by line and column in the original text and carry a
  hint, so the vector tests compare the unexpected token and the expected tokens, not jison's line.
- **Limits.** The nesting and label limits refuse the text when the document is opened, with code
  `mermaid-limit-exceeded`; Mermaid has no nesting or label limit.
- **Accessibility.** `accTitle` and `accDescr` are metadata of the diagram only, since a rendered picture has no SVG
  to carry them.
- **No title statement (as in Mermaid).** The grammar's `title` statement cannot be reached in Mermaid either, so the
  diagram has no title of its own and the frontmatter title is drawn.
