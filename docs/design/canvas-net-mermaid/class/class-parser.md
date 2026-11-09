### ClassParser

![Class Structure](MermaidClassView.svg)

The `ClassParser` unit is the first software unit in the `Class` subsystem. It comprises two source files in the
`DemaConsulting.CanvasNet.Mermaid.Class` namespace: `ClassParser.cs` (the entry point, the grammar, the merged
lookahead sets of jison's states, the limits, and the error machinery) and `ClassParser.Lexer.cs` (the static
`ClassTokens` class with the token names and jison's terminal numbers, and the lexer rules, start conditions, and
matchers). `ClassParser` is an `internal sealed partial` class, so the two files are one type; `ClassTokens` is an
`internal static` class. Both files carry the provenance header comment for the parts of Mermaid they port.

#### Purpose

`ClassParser` reads the preprocessed text of a class diagram into a `ClassModel`, accepting exactly the language of the
generated parser of Mermaid's `classDiagram.jison` and calling the operations of the class database at the points
where Mermaid's grammar runs its actions, in the same order. It exists so that every text means what it means to
Mermaid, so that an invalid text fails with Mermaid's diagnosis at the right place in the original file, and so that
reading untrusted text costs time linear in its length.

The unit's behavior is specified by the `CanvasNetMermaid-Class-ClassParser-*` requirements in
`docs/reqstream/canvas-net-mermaid/class/class-parser.yaml`.

#### Data Model

**`ClassTokens`** holds the token kinds as constants whose values are the names of the terminals of
`classDiagram.jison` (`CLASS_DIAGRAM`, `NEWLINE`, `SQS`, `STR`, `SQE`, `DOT`, `GENERICTYPE`, `LABEL`, `STRUCT_START`,
`STRUCT_STOP`, `NAMESPACE`, `STYLE_SEPARATOR`, `ANNOTATION_START`, `ANNOTATION_END`, `CLASS`, `MEMBER`, `NOTE_FOR`,
`NOTE`, `CLASSDEF`, `ALPHA`, the four direction tokens, the relation ends `AGGREGATION`, `EXTENSION`, `COMPOSITION`,
`DEPENDENCY`, and `LOLLIPOP`, the lines `LINE` and `DOTTED_LINE`, the interaction tokens `CALLBACK`, `LINK`,
`LINK_TARGET`, `CLICK`, `CALLBACK_NAME`, `CALLBACK_ARGS`, and `HREF`, the style tokens, `BQUOTE_STR`, and so on), so
that messages and expected-token lists read like Mermaid's parse errors. The one exception is `Eof`, the `EOF` terminal
that the lexer produces once at the end of the text, which must be told apart from the end of the token stream
(jison's `$end`). The lexer also produces kinds the grammar never accepts (`EDGE_STATE`, `EOF_IN_STRUCT`,
`OPEN_IN_STRUCT`, `PLUS`, `EQUALS`, and `PUNCTUATION`), so any of them is a parse error, as in Mermaid. `JisonName`
gives the name a kind is printed with, and `Number` gives jison's terminal number, which orders expected-token lists
as jison prints them.

**A parser instance** parses one text on one thread and holds:

- the `ClassModel` it fills and the `MermaidLimits` of the open call;
- the scanner built from the lexer rules and the token cursor over it (one token of lookahead);
- the current nesting depth of namespaces;
- the cached match of the direction rules on the current line (the rules are created per parser for that reason).

**Lexer tables.** The 99 rules of the generated lexer are ported one to one, in jison's order, each with a hand-written
matcher in place of its regular expression and the action of the rule (a token kind, a change of start condition, or
both); the 15 start conditions (`namespace-body`, `namespace`, `class-body`, `class`, `acc_descr_multiline`,
`acc_descr`, `acc_title`, `callback_args`, `callback_name`, `href`, `struct`, `generic`, `bqstring`, `string`, and the
initial one) list the indexes of their rules in the order jison tries them, the first matching rule winning, and the
grammar is case-sensitive. Keywords get jison's automatic word boundary. Mermaid's quirks are kept: a line with
`direction TB`, `BT`, `RL`, or `LR` anywhere in it is one direction token (the last such direction on the line is found
once and cached per line, so the far-looking rule stays linear); keywords such as `note`, `link`, or `click` are
keywords inside generics and backticks too; only an `o` that does not start a longer word is an aggregation end (so
`owner` is a name); a colon starts a label that runs to the next colon, semicolon, or line break; a line of a class body
is one `MEMBER` token; the end of the text inside an open body is `EOF_IN_STRUCT`; and a `}` in the `namespace` state
leaves the state and is read again. Names take the letters of the `UNICODE_TEXT` rule, the table of letters of every
script that the `Syntax` subsystem shares with the flowchart lexer (`ScanPatterns.IsUnicodeLetter`).

**The parser's lookahead sets** (`StatementStarts`, `StatementEnds`, `StructEnds`, `NameFollowers`,
`NameTokenFollowers`, `NameContinuations`, `LiteralFollowers`, `NameStatementFollowers`, `RelationTypeFollowers`,
`LineFollowers`, `RelationFollowers`, `RelationEnds`, `ClassNameFollowers`, `LabelFollowers`, `BodyOrEnd`,
`StyleComponents`, `StyleFollowers`, `ClassListFollowers`, `NamespaceItems`, `NamespaceNameContinuations`, and
`NamespaceNameFollowers`) are the lookahead sets of the states of jison's LALR(1) tables, including the merged sets on
which several states reduce (after a name token, a class name, a relation end, or a part of a style), so that a token
is refused in the state where jison refuses it, with jison's list of expected tokens.

#### Key Methods

- **`Parse(PreprocessedSource, DetectedDiagram, MermaidLimits, bool hierarchicalNamespaces = true)`** creates the model
  (with the location of the diagram keyword, Mermaid's id of the detected diagram, `classDiagram` or the legacy
  `class`, and the text's own `class.hierarchicalNamespaces` setting for its counts) and a parser, and runs the grammar
  over the parser text of the source, whose characters stay mapped to the original text. The database operations run
  while the text is parsed, so the returned model is complete.
- **`Run`** reads the `classDiagram` keyword, a line break, the statements, and `EOF`; a text whose first line holds a
  direction (the keyword line then lexes as one direction token) has no header and reads the statements alone, and its
  remaining `EOF` is refused with an empty expected list, as in Mermaid. **`ParseStatements`** reads statements
  separated by line breaks, checking the followers of each one (`CheckLookahead`); **`ParseStatement`** dispatches on
  the first token: `class`, `namespace`, an annotation, a note, `classDef`, `style`, `cssClass`, `callback`, `link`,
  `click`, a direction (the last one wins), the accessibility statements (trimmed and handed to the model), a member or
  separator outside a body (accepted and ignored), and otherwise a statement that starts with a class name.
- **`ParseNameStatement`** reads a class name and then either a label (a member statement, `AddMember` with the label
  cleaned by `ClassModel.CleanupLabel`), nothing (a lone name, which does nothing, so a `title` line is ignored as in
  Mermaid), or a relation: an optional first cardinality, the relation (**`ParseRelation`**: an optional end, the line,
  and an optional end, as Mermaid's relation and line codes), an optional second cardinality, the second class, and an
  optional label (`AddRelation`, with `none` for a missing cardinality).
- **`ParseClassName`** reads name tokens, concatenated or joined by dots, ending with a name token or a backticked name,
  with an optional generic type at the end, and builds the name as the grammar does (`a.b`, `ab`, `Name~T~`); it reads
  in a loop, so a name of many tokens cannot exhaust the stack. **`ParseNamespaceName`** does the same for a namespace.
- **`ParseClassStatement`** reads `class`, the name, an optional `["label"]` (**`ParseLabel`**), and then an optional
  `:::` class, annotation, or body (**`ParseBody`**: `{`, the member lines, and `}`; after a `:::` class the body may
  not be empty). It calls `AddClass` and `SetClassLabel` when the name and label are reduced, then `SetCssClass`,
  `AddAnnotation`, and `AddMembers` in jison's order, and returns the name for an enclosing namespace.
- **`ParseNamespaceStatement`** reads `namespace`, the name, an optional label, and `{`, enters the namespace
  (`AddNamespace`) when it is reduced, reads its items (classes, notes, and nested namespaces, separated by line
  breaks) up to `}`, and then puts the classes and notes declared in it into it (`AddClassesToNamespace`) and leaves it
  (`PopNamespace`). Only this method recurses, bounded by `MaxNestingDepth`.
- **`ParseAnnotationStatement`**, **`ParseNoteStatement`**, **`ParseClassDefStatement`**, **`ParseStyleStatement`**,
  and **`ParseCssClassStatement`** read `<<annotation>> Name`, `note for Name "text"` and `note "text"`,
  `classDef a,b styles`, `style Name styles`, and `cssClass "A,B" name`; **`ParseStyles`** reads styles separated by
  commas, each the concatenated text of its tokens, in a loop.
- **`ParseClickStatement`** and **`ParseClickAction`** read `callback Name "function" ["tooltip"]`,
  `link Name "url" ["tooltip"] [target]`, `click Name call function(args) ["tooltip"]`, and
  `click Name href "url" ["tooltip"] [target]`, and hand the link, target, tooltip, and callback to the model as data
  (`SetLink`, `SetClickEvent`, `SetTooltip`); **`Optional`** reads an optional last token after checking jison's
  accepted set at that point.
- **`CheckLookahead`**, **`Is`**, **`Advance`**, **`Expect`**, and **`Fail`**: `CheckLookahead` refuses a token outside
  the set a state reduces on, `Is` tests the lookahead, `Advance` consumes a token, `Expect` consumes a token of a kind
  or fails with all the kinds jison lists there, and `Fail` builds the located exception with the expected kinds
  sorted by jison's terminal numbers and a hint chosen from them (`HintFor`).
- **`TextOf`**, **`CheckLength`**, and **`Enter`** check `MaxLabelLength` for the text of every token the parser keeps
  and for every name and style it joins from tokens, and `MaxNestingDepth` for every namespace.

#### Error Handling

- A token the grammar does not accept is a `MermaidSyntaxException` with code `mermaid-syntax-error`, located at the
  token in the original text, with the message `expected 'A', 'B' or 'C', found 'KIND' ('text')` (or `found the end
  of the text`), the expected token names as `Expected`, and a hint: close a class body or namespace with `}` at
  `EOF_IN_STRUCT`, the content of a body or namespace where `}` was expected, a quoted text for a note, the relation
  forms where a line was expected, and the form of a class name where a name was expected.
- Text that no lexer rule matches (such as a statement semicolon) is a located lexical error raised by the scanner of
  the `Syntax` subsystem; the parser rewrites its message as `unrecognized text ...` with a hint for class diagrams
  (statements end at the end of their line, and braces only surround a class body or a namespace).
- A namespace nested deeper than `MaxNestingDepth` is refused at its keyword, and a text longer than
  `MaxLabelLength` at the token it starts with, both with code `mermaid-limit-exceeded`. The parser checks every token
  text it keeps (a name token, a backticked name, a generic type, a label, a member line, a cardinality, a note, a
  `:::` class, an annotation, a style class name, a link, a target, a tooltip, a callback name and its arguments, and
  the accessibility texts) and every text it joins from tokens (a class or namespace name and a style). The texts that
  the model joins from texts the parser checked apart are checked by the model (see _ClassModel Unit Design_).
- The errors of the model while a statement is applied (Mermaid's database errors, the `MaxEdges` and `MaxNodes`
  limits, and the `MaxLabelLength` check of the texts it joins) propagate unchanged. Because each operation runs after
  the lookahead that decides its reduction has been checked, grammar and database errors come in Mermaid's order.
- `Parse` throws `ArgumentNullException` for a null argument.

#### Dependencies

- **ClassModel** - the database the operations are applied to, the relation and line codes, the relation records
  (`ClassRelationInput`, `ClassRelationType`), and `CleanupLabel`.
- **Syntax subsystem** - `SourceScanner`, `ScanRule`, `ScanMatcher`, `ScanToken`, and `TokenCursor` (the scanner and the
  cursor), `ScanPatterns` (the JavaScript character classes and the shared `UNICODE_TEXT` letters), `MappedText` and
  `PreprocessedSource` (the text mapped to the original), `DetectedDiagram`, and `Preprocessor.TrimJs` (JavaScript's
  `trim`).
- **System** - `MermaidLimits`, `MermaidLocation`, `MermaidSyntaxException`, and `MermaidDiagnosticCodes`.

#### Callers

- The document pipeline of the system (`MermaidDocument.Pipeline.cs`): its `DiagramRegistry` calls `Parse` for a text
  whose detected type is a class diagram, with the `class.hierarchicalNamespaces` setting of the text's own
  configuration.
- The unit and subsystem tests call `Parse` directly (through the test helper `ClassFacts`).

#### Known Differences From Mermaid

- **No regular expressions.** The lexer is a set of hand-written matchers with the same matches as Mermaid's regular
  expressions; the rules that look far ahead in Mermaid (the direction anywhere on a line) are matched once per line
  and cached, so reading a text stays linear in its length.
- **Recursive descent instead of LALR(1) tables.** The parser accepts the same language with one token of lookahead
  and emulates the expected-token lists of jison's tables, including the merged lookahead of its states; the grammar
  errors of the upstream vectors give the identical lists in jison's order.
- **Located errors.** Mermaid's parse errors name a line of the text that its parser sees, and jison reports the line
  after the lookahead was read; this unit's errors are located by line and column in the original text and carry a
  hint, so the vector tests compare the unexpected token and the expected tokens, not jison's line.
- **Limits.** The nesting and label limits refuse the text when the document is opened, with code
  `mermaid-limit-exceeded`; Mermaid has no nesting or label limit.
- **Interaction and accessibility.** `callback`, `link`, and `click` are read and their data kept for the model, which
  never draws or binds them; `accTitle` and `accDescr` are metadata of the diagram only, since a rendered picture has
  no SVG to carry them.
