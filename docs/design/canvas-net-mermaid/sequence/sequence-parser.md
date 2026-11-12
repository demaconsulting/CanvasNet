### SequenceParser

![Sequence Structure](MermaidSequenceView.svg)

The `SequenceParser` unit is the first software unit in the `Sequence` subsystem. It comprises three source files in
the `DemaConsulting.CanvasNet.Mermaid.Sequence` namespace: `SequenceParser.cs` (the entry point, the document and line
rules, the merged lookahead sets of jison's states, the limits, and the error machinery), `SequenceParser.Lexer.cs`
(the lexer rules and start conditions, and the static `SequenceTokens` class with the token names, the arrows, and
jison's terminal numbers), and `SequenceParser.Statements.cs` (the statements: participants, boxes, `autonumber`,
activations, notes, the interaction statements, blocks, and signals). `SequenceParser` is an `internal sealed partial`
class, so the three files are one type; `SequenceTokens` is an `internal static` class. Every file carries the
provenance header comment for the parts of Mermaid it ports.

#### Purpose

`SequenceParser` reads the preprocessed text of a sequence diagram into a `SequenceModel`, accepting exactly the
language of the generated parser of Mermaid's `sequenceDiagram.jison` and collecting the statements that Mermaid's
grammar hands to its sequence database, in the same order. It exists so that every text means what it means to
Mermaid, so that an invalid text fails with Mermaid's diagnosis at the right place in the original file, and so that
reading untrusted text costs time linear in its length.

The unit's behavior is specified by the `CanvasNetMermaid-Sequence-SequenceParser-*` requirements in
`docs/reqstream/canvas-net-mermaid/sequence/sequence-parser.yaml`.

#### Data Model

**`SequenceTokens`** holds the token kinds as constants whose values are the names of the terminals of
`sequenceDiagram.jison` (`SD`, `NEWLINE`, `INVALID`, `participant`, `participant_actor`, `AS`, `ACTOR`,
`CONFIG_START`, `CONFIG_CONTENT`, `CONFIG_END`, `TXT`, `restOfLine`, the block keywords `loop`, `rect`, `opt`, `alt`,
`par`, `par_over`, `critical`, `break`, `else`, `and`, `option`, and `end`, and so on), so that messages and
expected-token lists read like Mermaid's parse errors. `ArrowRules` lists the 26 arrows in lexer order with their
terminal names and the line types that the grammar maps them to (`ArrowLineTypes`); `Number` gives jison's terminal
number, which orders expected-token lists as jison prints them.

**A parser instance** parses one text on one thread and holds:

- the `SequenceModel` it fills and the `MermaidLimits` of the open call;
- the token cursor over a `SourceScanner` built from the lexer rules (one token of lookahead);
- the list of `SequenceStatement` records collected for the database, in Mermaid's order;
- the current nesting depth of blocks and boxes.

**Lexer tables.** The 94 rules of the generated lexer are ported one to one, in jison's order, each with a
hand-written matcher in place of its regular expression and the action of the rule (a token kind, a change of start
condition, or both); the 8 start conditions (`ID`, `ALIAS`, `LINE`, `CONFIG`, `acc_title`, `acc_descr`,
`acc_descr_multiline`, and the initial one) list the indexes of their rules in the order jison tries them, the first
matching rule winning. Keywords get jison's automatic word boundary, and the grammar's `case-insensitive` option folds
ASCII letters only. The `ACTOR` rule, whose expression looks ahead for an arrow after every dash, is matched with the
list of texts after which a dash run may not continue a name (`ArrowStarts`).

**The parser's lookahead sets** (`LineStarts`, `LineFollowers`, `ActorFollowers`, `SignalStarts`, `ArrowFollowers`,
`DeclaredNameFollowers`, `BoxLineTokens`, and the table of `Blocks` with the line types of their start, sections, and
end) are the merged lookahead sets of the states of jison's LALR(1) tables, so that a token is refused in the state
where jison refuses it, with jison's list of expected tokens.

#### Key Methods

- **`Parse(PreprocessedSource, DetectedDiagram, MermaidLimits)`** creates the model (with the location of the diagram
  keyword) and a parser, runs the grammar over the parser text of the source, whose characters stay mapped to the
  original text, and then applies the collected statements to the model (`SequenceModel.Apply`), as Mermaid's
  `yy.apply` does after the whole text has parsed.
- **`Run`** reads leading line breaks, the `sequenceDiagram` keyword, and the document; **`ParseDocument`** reads
  lines up to the end of the tokens or up to a keyword that ends or continues the block around them, checking the
  merged followers of a line after each one (`CheckLookahead`); **`ParseLine`** ignores a line break or a line of
  invalid text, as the grammar does, and dispatches anything else to **`ParseStatement`**.
- **`ParseParticipantStatement`** reads `participant` or `actor`, a name, an optional `@{ ... }` metadata block
  (whose trimmed text is kept with the map of its positions to the original text, `SequenceMetadataText`), and an
  optional alias with its `wrap:` prefix, or `destroy` and a name; a `create` before it marks the participant as
  created.
- **`ParseBox`** reads `box`, its color and title (`SequenceModel.ParseBoxData`), the participant statements, and
  `end`; **`ParseBlock`** reads a block keyword and its title, its lines, its sections (`else`, `and`, or `option`,
  each with a title and lines), and `end`, collecting a start, section, and end marker with their texts.
- **`ParseSignal`** reads a participant, an arrow with its modifiers (`+` or `-` after it, `()` after it, before it,
  or both), a participant, and the text, and collects the mentions of the two participants, the message, and the
  activation or central-connection signals in the order of the grammar's actions (`AddMessage`, `Mention`).
- **`ParseNote`**, **`ParseActivation`**, **`ParseAutonumber`**, and **`ParseActorData`** read `note left of`,
  `note right of`, and `note over` one or two participants; `activate` and `deactivate`; `autonumber` alone, with a
  start, with a start and a step (read as JavaScript numbers), or `off`; and `links`, `link`, `properties`, and
  `details` with their texts.
- **`ExpectActor`**, **`ExpectArrow`**, and **`ExpectText`** consume a name, an arrow, or a text (without its colon,
  read by `SequenceText.Parse`) and check the merged lookahead that follows them.
- The title and the accessibility statements (`title`, `title:`, `accTitle:`, `accDescr:`, and `accDescr { ... }`)
  set the texts of the model while parsing, as Mermaid's grammar does.
- **`Is`**, **`Advance`**, **`Expect`**, and **`Fail`**: `Is` tests the lookahead, `Advance` consumes a token, and
  `Fail` builds the located exception with the expected kinds sorted by jison's terminal numbers and a hint chosen from
  them (`HintFor`: a block without `end`, a section keyword out of place, a missing arrow, text, or name).
- **`TextOf`** and **`Enter`** check `MaxLabelLength` for every name and text and `MaxNestingDepth` for every block
  and box.

#### Error Handling

- A token the grammar does not accept is a `MermaidSyntaxException` with code `mermaid-syntax-error`, located at the
  token in the original text, with the message `expected 'A', 'B' or 'C', found 'KIND' ('text')` (or `found the end
  of the text`), the expected token names as `Expected`, and a hint.
- Text that no lexer rule matches is a located lexical error raised by the scanner of the `Syntax` subsystem, with a
  hint for sequence diagrams.
- A block or box nested deeper than `MaxNestingDepth`, and a name or text longer than `MaxLabelLength`, are refused at
  the element with code `mermaid-limit-exceeded`.
- The errors of the model while the statements are applied (Mermaid's database errors, invalid metadata, the
  `MaxNodes` limit on participants and boxes together, and the `MaxLabelLength` check of a metadata `type` or `alias`)
  propagate unchanged; because the statements are applied after the whole text has parsed, a grammar error anywhere in
  the text comes first. The parser does not check the `@{ ... }` metadata text itself: the model checks it once it is
  converted.
- `Parse` throws `ArgumentNullException` for a null argument.

#### Dependencies

- **SequenceModel** - the database the statements are applied to, the statement records, `SequenceText.Parse`,
  `ParseBoxData`, the line types, and the title and accessibility setters.
- **Syntax subsystem** - `SourceScanner`, `ScanRule`, `ScanMatcher`, `ScanToken`, and `TokenCursor` (the scanner and
  the cursor), `MappedText` and `PreprocessedSource` (the text mapped to the original), `DetectedDiagram`, and
  `Preprocessor.TrimJs` (JavaScript's `trim`).
- **System** - `MermaidLimits`, `MermaidLocation`, `MermaidSyntaxException`, and `MermaidDiagnosticCodes`.

#### Callers

- The document pipeline of the system (`MermaidDocument.Pipeline.cs`) calls `Parse` for a text whose detected type is
  a sequence diagram.
- The unit and subsystem tests call `Parse` directly.

#### Known Differences From Mermaid

- **No regular expressions.** The lexer is a set of hand-written matchers with the same matches as Mermaid's regular
  expressions; the rules that look ahead or backtrack in Mermaid (participant names with dashes, the numbers of
  `autonumber`) are matched as the expressions match them in linear time.
- **Recursive descent instead of LALR(1) tables.** The parser accepts the same language with one token of lookahead
  and emulates the expected-token lists of jison's tables, including the merged lookahead of its states; the grammar
  errors of the upstream vectors give the identical lists in jison's order.
- **Located errors.** Mermaid's parse errors name a line of the text that its parser sees; this unit's errors are
  located by line and column in the original text and carry a hint.
- **Limits.** The nesting and label limits refuse the text when the document is opened, with code
  `mermaid-limit-exceeded`; Mermaid has no nesting or label limit.
- **Interaction and accessibility.** `links`, `link`, `properties`, and `details` are read and kept as data;
  `accTitle` and `accDescr` are metadata of the diagram only, since a rendered picture has no SVG to carry them.
