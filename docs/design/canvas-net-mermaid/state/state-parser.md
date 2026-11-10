### StateParser

![MermaidState Structure](MermaidStateView.svg)

The `StateParser` unit is the first software unit in the `State` subsystem. It comprises two source files in the
`DemaConsulting.CanvasNet.Mermaid.State` namespace: `StateParser.cs` (the entry point, the grammar, the lookahead sets
of jison's states, the nesting and label limits, and the error machinery) and `StateParser.Lexer.cs` (the static
`StateTokens` class with the token names and jison's terminal numbers, and the lexer rules, start conditions, matchers,
and line caches). `StateParser` is an `internal sealed partial` class, so the two files are one type; `StateTokens` is
an `internal static` class. Both files carry the provenance header comment for the parts of Mermaid they port.

#### Purpose

`StateParser` reads the preprocessed text of a state diagram into a `StateModel`, accepting exactly the language of the
generated parser of Mermaid's `stateDiagram.jison` under both headers and building the statement tree that Mermaid's
grammar actions build, which it hands to the model once, as the grammar's `setRootDoc` action does. It exists so that
every text means what it means to Mermaid, so that an invalid text fails with Mermaid's diagnosis at the right place in
the original file, and so that reading untrusted text costs time linear in its length.

The unit's behavior is specified by the `CanvasNetMermaid-State-StateParser-*` requirements in
`docs/reqstream/canvas-net-mermaid/state/state-parser.yaml`.

#### Data Model

**`StateTokens`** holds the token kinds as constants whose values are the names of the terminals of
`stateDiagram.jison` (`NL`, `SD`, `DESCR`, `-->`, `HIDE_EMPTY`, `scale`, `WIDTH`, `COMPOSIT_STATE`, `STRUCT_START`,
`STRUCT_STOP`, `STATE_DESCR`, `AS`, `ID`, `FORK`, `JOIN`, `CHOICE`, `CONCURRENT`, `note`, `NOTE_TEXT`, the accessibility
tokens `acc_title`, `acc_title_value`, `acc_descr`, `acc_descr_value`, and `acc_descr_multiline_value`, `CLICK`,
`STRING`, `HREF`, the style tokens `classDef`, `CLASSDEF_ID`, `CLASSDEF_STYLEOPTS`, `DEFAULT`, `style`, `STYLE_IDS`,
`STYLEDEF_STYLEOPTS`, `class`, `CLASSENTITY_IDS`, and `STYLECLASS`, the four direction tokens `direction_tb`,
`direction_bt`, `direction_rl`, and `direction_lr`, `EDGE_STATE`, `STYLE_SEPARATOR`, `left_of`, and `right_of`), so that
messages and expected-token lists read like Mermaid's parse errors. The lexer also produces two kinds the grammar never
accepts (`INVALID`, any other character such as a dash outside a composite state, and `DEFAULT_CLASSDEF_ID`, the
`default` class name of a `classDef`), so either is a parse error, as in Mermaid; and the grammar has two kinds that no
lexer rule produces where the grammar wants them (`SPACE`, and `DEFAULT` after `classDef`), which appear only in
expected lists. `Number` gives jison's terminal number of a kind, which orders expected-token lists as jison prints
them; kinds the grammar does not know sort last.

**A parser instance** parses one text on one thread and holds:

- the `StateModel` it fills and the `MermaidLimits` of the open call;
- the scanner built from the lexer rules and the token cursor over it (one token of lookahead);
- the current nesting depth of composite states;
- the cached results of the line searches of the current line (the last direction and the last pseudo-state marker on
  it, by search) and of the brace search of the single-word rule (the rules are created per parser for that reason).

**Lexer tables.** The 80 rules of the generated lexer are ported one to one, in jison's order, each with a hand-written
matcher in place of its regular expression and the action of the rule (a token kind, a change of start condition, a
rewrite of the token text, or a combination); the 20 start conditions (the initial one, `struct`, `STATE`,
`STATE_STRING`, `STATE_ID`, `SCALE`, `acc_title`, `acc_descr`, `acc_descr_multiline`, `CLASSDEF`, `CLASSDEFID`,
`CLASS`, `CLASS_STYLE`, `STYLE`, `STYLEDEF_STYLES`, `NOTE`, `NOTE_ID`, `NOTE_TEXT`, `FLOATING_NOTE`, and
`FLOATING_NOTE_ID`) list the indexes of their rules in the order jison tries them, the first matching rule winning. The
lexer is case-insensitive as JavaScript's `i` flag is: ASCII letters match in any case, and nothing outside ASCII folds
into ASCII; keywords get jison's automatic word boundary. Mermaid's quirks are kept:

- a line with `direction TB`, `BT`, `RL`, or `LR` anywhere in it from the current position is one direction token up to
  the line feed after the direction, so the ids before it on the line are swallowed; the last such direction on the line
  is found once and cached per line (`LastOnLine`);
- a pseudo-state after `state` (`<<fork>>`, `<<join>>`, `<<choice>>`, or `[[fork]]`, `[[join]]`, `[[choice]]`) is one
  token running to the last marker on the line, whose text is the trimmed id before the marker;
- an inline `%%` ends an id and the rest is read again (jison's `processId`, which returns the rest to the input), and
  an id that starts with `%%` is skipped; a `%%` comment (not `%%{`) and a `#` comment run to the end of the line;
- an id ends at a colon, a dash, a brace, or white space, so a dash outside a composite state is the `INVALID` token;
  `--` is a `CONCURRENT` token only in the `struct` condition of a composite state's body;
- in a composite state's body line feeds are skipped, and neither `click` nor `href` nor a string is a token, so
  `click A "u" "t"` there is four ids;
- a description or transition label is a colon and the text after it up to a line feed, a semicolon, or a colon that
  is not followed by text, so `A : first: second` keeps `first: second`;
- the position of a note (`left of`, `right of`) keeps the case it is written in; the text of a one-line note is its
  match without the first two characters (Mermaid's `substr(2)`, meant for the space and colon before the text), and
  the text of a multi-line note runs to the first line that starts with `end note`, whose eight characters are dropped;
  a floating note is `note "text" as id`;
- a `state` name of two or more words before a brace (Mermaid's `\w+\s+\w+.*?\{`, whose white space may span lines)
  throws Mermaid's single-word error; the first brace of the line is found once and cached (`FirstBraceOnLine`);
- `classDef default` is the `DEFAULT_CLASSDEF_ID` token, `hide empty description` one token, and `scale N width` the
  `scale` keyword, the number, and the `width` that ends it;
- the end of the text in the initial condition is an `NL` token.

**The parser's lookahead sets** (`StatementStarts`, `LineStarts`, `StatementFollowers`, `StateFollowers`,
`TransitionFollowers`, `IdFollowers`, and `CompositeFollowers`) are the lookahead sets of the states of jison's LALR(1)
tables, including the merged set on which several states reduce after an id statement (with `STRING` and `HREF`, which
follow the state of a click), so that a token is refused in the state where jison refuses it, with jison's list of
expected tokens.

**The statement tree** is a list of `StateStatement` objects (see _StateModel Unit Design_) with Mermaid's statement
kinds: `state` (an id, a type, descriptions, a note, `:::` classes, and the document of a composite state), `relation`
(two state statements and a label), `classDef`, `style`, `applyClass`, `click`, and `dir`. A statement the grammar keeps
as a string (`hide empty description`, `scale`, a lone `state name`, a floating note, and the accessibility statements)
is a statement without a kind that keeps its text, which the database ignores; a statement whose text is `nl` is left
out, as Mermaid's `$2 != 'nl'` check does.

#### Key Methods

- **`Parse(PreprocessedSource, DetectedDiagram, MermaidLimits)`** creates the model (with the location of the diagram
  keyword and Mermaid's id of the detected diagram, `stateDiagram` or the legacy `state`) and a parser, and runs the
  grammar over the parser text of the source, whose characters stay mapped to the original text. The model applies the
  statement tree before `Parse` returns, so the returned model is complete.
- **`Run`** reads line breaks, the `SD` header (`stateDiagram` or `stateDiagram-v2`), and the top-level document, and
  hands it to the model (`SetRootDocument`). **`ParseDocument`** reads statements and line breaks up to the end of the
  tokens or, in a composite state's body, up to its `}`, checking the followers of each one (`CheckEnd`).
  **`ParseStatement`** dispatches on the first token: `classDef`, `style`, and `class` (with the ids and the styles or
  class trimmed), an id or `[*]`, `hide empty description` and `scale` (kept as text), `state`, a quoted description,
  a fork, join, or choice (a `state` statement of that type), `--` (a `divider` state with the id the model gives out,
  `NextDividerId`, so dividers are numbered in parse order, inner ones first), a note, a direction, the accessibility
  statements (trimmed and handed to the model as they are read), and `click`.
- **`ParseStateOrTransition`** reads an id statement and then either a description (`id : text`, trimmed of its colon
  with `StateModel.TrimColon`), nothing, or a transition: `-->`, a second id statement, and an optional label.
- **`ParseIdStatement`** reads an id or `[*]` with an optional `:::` class; the class is kept on the statement (the
  database applies it only in a transition, as Mermaid does).
- **`ParseCompositeState`** reads the name after `state` and either keeps the statement as text (no body) or reads the
  body (**`ParseBody`**: `{`, a document, and `}`), which makes a composite state. **`ParseDescribedState`** reads
  `state "description" as id` with an optional body; without a body a colon in the id splits off a second description
  (`state "Quoted" as B:more`).
- **`ParseBody`** counts the nesting depth against `MaxNestingDepth`; it is the only method that recurses, through
  `ParseDocument` and `ParseStatement`, so the depth of the recursion is bounded by the limit.
- **`ParseNote`** reads `note left of id : text`, `note right of id : text`, or a multi-line note into a `state`
  statement with a `StateNote`, or a floating note `note "text" as id`, which is checked and kept as text.
- **`ParseClick`** reads `click id "url" "tooltip"` or `click id href "url"`, ended by a line break, and keeps the
  target, the URL, and the tooltip (with their quotes) for the model, which never draws them.
- **`CheckEnd`**, **`CheckLookahead`**, **`Is`**, **`IsEnd`**, **`Advance`**, **`Expect`**, **`ExpectAny`**, and
  **`Fail`**: `CheckLookahead` refuses a token outside the set a state reduces on (the end of the tokens is accepted
  where jison reduces on it but never listed), `Is` tests the lookahead, `Advance` consumes a token, `Expect` and
  `ExpectAny` consume a token of the given kinds or fail with all of them expected, and `Fail` builds the located
  exception with the expected kinds sorted by jison's terminal numbers and a hint chosen from them (`HintFor`).
- **`TextOf`** checks `MaxLabelLength` for the text of every token the parser keeps or checks: ids, `:::` class names,
  composite names, quoted descriptions and the ids after `as`, descriptions and transition labels, fork, join, and
  choice ids, note texts and note ids (of floating notes too, though they are dropped), the `classDef` name and styles,
  the ids and styles of `style`, the ids and class of `class`, the URL and tooltip of `click`, the accessibility texts,
  and the number of `scale`.
- **`Lex`** runs a step that reads a token and rewrites a lexical error of the scanner as `unrecognized text ...` with a
  hint for state diagrams; **`CreateRules`** builds the 80 rules with their actions; **`ProcessId`** splits an id at an
  inline comment; **`ThrowStateWords`** throws the single-word error; **`DirectionLine`**, **`PseudoStateLine`**, and
  **`MatchStateWords`** are the far-looking matchers, linear through **`LastOnLine`** and **`FirstBraceOnLine`**.

#### Error Handling

- A token the grammar does not accept is a `MermaidSyntaxException` with code `mermaid-syntax-error`, located at the
  token in the original text, with the message `expected 'A', 'B' or 'C', found 'KIND' ('text')` (or `found the end of
  the text`), the expected token names as `Expected`, and a hint: a text without the header, a transition without its
  target, a note without its position, and a click without its link have their own hints; an `INVALID` token gets a hint
  that a state id has no dashes and that `--` divides regions only inside a composite state; `DEFAULT_CLASSDEF_ID` a hint
  that the default class cannot be redefined; and the end of the text where `STRUCT_STOP` was expected a hint to close
  every composite state with `}`.
- Text that no lexer rule matches (such as a multi-line note without `end note`) is a located lexical error raised by
  the scanner of the `Syntax` subsystem; the parser rewrites its message as `unrecognized text ...` with a hint for state
  diagrams (close a multi-line note with `end note`, end `scale` with `width`, and end every keyword statement on its
  line).
- A composite state name of two words before a brace is Mermaid's own error, `Error: State name must be a single word.
  Found: "..."`, with code `mermaid-syntax-error`, located at the name, with no expected tokens and a hint to write one
  word or a quoted description; the hint tells it apart from a lexical error, so `Lex` keeps it.
- Composite states nested deeper than `MaxNestingDepth` are refused at the `{` of the first one too deep, and a token
  text longer than `MaxLabelLength` at the token, both with code `mermaid-limit-exceeded` and a message that names the
  limit. The texts the model joins or derives from texts the parser checked apart, the concurrent regions as nesting
  levels, and the node and edge counts are checked by the model (see _StateModel Unit Design_).
- The errors of the model while the statement tree is applied (Mermaid's database error and the model's limits)
  propagate unchanged.
- `Parse` throws `ArgumentNullException` for a null argument.

#### Dependencies

- **StateModel** - the model the statement tree is handed to, the statement types (`StateStatement`, `StateNote`), the
  divider ids (`NextDividerId`), the accessibility texts, the type constants (`DefaultType`, `DividerType`), and
  `TrimColon`.
- **Syntax subsystem** - `SourceScanner`, `ScanRule`, `ScanMatcher`, `ScanToken`, and `TokenCursor` (the scanner and the
  cursor), `ScanPatterns` (the JavaScript character classes), `MappedText` and `PreprocessedSource` (the text mapped to
  the original), `DetectedDiagram`, and `Preprocessor.TrimJs` (JavaScript's `trim`).
- **System** - `MermaidLimits`, `MermaidLocation`, `MermaidSyntaxException`, and `MermaidDiagnosticCodes`.

#### Callers

- The document pipeline of the system (`MermaidDocument.Pipeline.cs`): its `DiagramRegistry` calls `Parse` for a text
  whose detected type is a state diagram (the `stateDiagram` and `stateDiagram-v2` keywords and the legacy id).
- The unit and subsystem tests call `Parse` directly (through the test helper `StateFacts`).

#### Known Differences From Mermaid

- **No regular expressions.** The lexer is a set of hand-written matchers with the same matches as Mermaid's regular
  expressions; the rules that look far ahead in Mermaid (a direction anywhere on a line, a pseudo-state marker at the end
  of a line, and the brace after a name of two words) are searched once per line and cached, so reading a text stays
  linear in its length.
- **Recursive descent instead of LALR(1) tables.** The parser accepts the same language with one token of lookahead
  and emulates the expected-token lists of jison's tables, including the merged lookahead after an id statement; the
  grammar errors of the upstream vectors give the identical lists in jison's order.
- **Located errors.** Mermaid's parse errors name a line of the text that its parser sees, and jison reports the line
  after the lookahead was read; this unit's errors are located by line and column in the original text and carry a
  hint, so the vector tests compare the unexpected token and the expected tokens, not jison's line. The errors Mermaid
  throws without a token (the single-word error) keep Mermaid's message after the location.
- **Limits.** The nesting and label limits refuse the text when the document is opened, with code
  `mermaid-limit-exceeded`; Mermaid has no nesting or label limit.
- **Interaction and accessibility.** `click` statements are read and their data kept for the model, which never draws
  or binds them; `accTitle` and `accDescr` are metadata of the diagram only, since a rendered picture has no SVG to carry
  them.
