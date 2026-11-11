### GanttParser

![MermaidGantt Structure](MermaidGanttView.svg)

The `GanttParser` unit is the first software unit in the `Gantt` subsystem. It comprises two source files in the
`DemaConsulting.CanvasNet.Mermaid.Gantt` namespace: `GanttParser.cs` (the entry point, the grammar with its database
calls, the click statements, the node limit, and the error machinery) and `GanttParser.Lexer.cs` (the static class
`GanttTokens` with the token kinds and their jison terminal numbers, and the lexer rules, start conditions, and
matchers). `GanttParser` is an `internal sealed partial` class, so the two files are one type. Both files carry the
provenance header comment for the parts of Mermaid they port.

#### Purpose

`GanttParser` reads the preprocessed text of a gantt chart into a `GanttModel`, accepting exactly the language of
Mermaid's jison grammar `gantt.jison` and making the calls on the gantt database that the grammar's actions make, with
the values those actions pass. It exists so that every text means what it means to Mermaid, quirks included, so that
an invalid text fails with Mermaid's diagnosis at the right place in the original file, and so that reading untrusted
text costs time linear in its length. The meaning of the statements - the database, the tasks, and their dates - is
the `GanttModel` unit's (see _GanttModel Unit Design_).

The unit's behavior is specified by the `CanvasNetMermaid-Gantt-GanttParser-*` requirements in
`docs/reqstream/canvas-net-mermaid/gantt/gantt-parser.yaml`.

#### Data Model

**The token kinds** (`GanttTokens`) are constants whose values are the names of the jison terminals, so that messages
and expected-token lists read like Mermaid's parse errors: the keyword `gantt`; the grammar's end of the text `EOF`
(the kind `<<EOF>>`, shown as `EOF`); `NL`, one or more line feeds; the statements `dateFormat`, `inclusiveEndDates`,
`topAxis`, `axisFormat`, `tickInterval`, `includes`, `excludes`, `todayMarker`, `weekday_monday` to `weekday_sunday`,
`weekend_friday`, `weekend_saturday`, `title`, and `section`, each with its value in its text where it has one; the
accessibility tokens `acc_title`, `acc_title_value`, `acc_descr`, `acc_descr_value`, and `acc_descr_multiline_value`;
the task tokens `taskTxt` (the name) and `taskData` (a colon and the data); and the click tokens `click` (the ids),
`callbackname`, `callbackargs`, and `href`. Five kinds come from lexer rules whose tokens the grammar does not know
(`open_directive`, `date`, `accDescription`, `:`, and `INVALID`), so they are always parse errors, as in Mermaid; and
the grammar knows one kind that no rule produces (`SPACE`), which only appears in expected lists. `GanttTokens.Number`
gives the jison terminal number of a kind, which orders an expected list as jison prints it, and `JisonName` its name.

**The lexer rules** (`Rules`) are the 53 rules of the lexer that jison generates from `gantt.jison`, in jison's order,
each with a hand-written matcher in place of its regular expression and its action (a token, a skip, or a change of
start condition); **the start conditions** (`Conditions`) list the rules each condition tries: the initial condition,
`acc_title`, `acc_descr`, `acc_descr_multiline`, `click`, `href`, `callbackname`, `callbackargs`, and
`open_directive` (which has no rules). The lexer is case-insensitive. The rules of the initial condition, in order:

- **The accessibility keywords** `accTitle:`, `accDescr:`, and `accDescr {`, which start the conditions that read the
  rest of the line or the text up to `}`.
- **The comments and white space**: `%%` and the rest of the line; the quirky rule `[^\}]\%\%*[^\n]*`, which skips
  any character but `}` (a line feed included) followed by one or more `%` and the rest of the line; `%` and the rest
  of the line with the line feeds after it; the line feeds (`NL`); and other white space, skipped.
- **The click parts**, before the task rules, so that a line that starts with them is never a task: `href` with white
  space and a quote (the URL up to the next quote), `call` with white space (the function name, then its arguments in
  parentheses), and `click` with white space (the ids up to the next white space).
- **The statements**: `gantt`; `dateFormat`, `axisFormat`, `tickInterval`, `includes`, `excludes`, and
  `accDescription`, each the keyword, one white-space character (a line feed included), and a value up to a line feed,
  `;`, or `#`; `todayMarker`, whose value runs up to a line feed or `;` only; the keywords `inclusiveEndDates` and
  `topAxis`; `weekday` or `weekend` with white space and a day; a date `dddd-dd-dd` (which the grammar does not use);
  and `title` and `section`, each the keyword, one white-space character, and the rest of the line.
- **The tasks**: `taskTxt`, one or more characters up to a colon or a line feed, and `taskData`, a colon and one or
  more characters up to `#`, `;`, or a line feed; then a colon alone, the end of the text, and any other character
  (`INVALID`).

**A parser instance** parses one text on one thread and holds the `GanttModel` it fills, the `MermaidLimits` of the
open call, the `SourceScanner` over the parser text of the source (a `MappedText`, whose characters stay mapped to the
original text), the `TokenCursor` with the lookahead token, and the number of task and section statements so far.
**`LineStarts`** lists the 27 tokens that can start a line (jison's state after `gantt document`), which is the
expected list of an error where a statement must start.

#### Key Methods

- **`Parse(PreprocessedSource, DetectedDiagram, MermaidLimits)`** creates a `GanttModel` with the limits and the
  location of the diagram keyword, runs a parser over the parser text of the source, and returns the filled model.
- **`Run`** reads the grammar in a loop with one token of lookahead: the `gantt` keyword, then line feeds and
  statements up to the grammar's `EOF` followed by the end of the tokens. The lookahead must be one of `LineStarts`
  where a statement can start.
- **`ParseStatement`** reads one statement and makes its database call with the value of the grammar's action: the
  text after the keyword and its one white-space character, as the action's `substr` takes it, trailing white space
  kept (for example `SetDateFormat(text[11..])` for `dateFormat`); `inclusiveEndDates`, `weekday`, and `weekend` with
  no value or the day; the accessible title and description trimmed as JavaScript's `trim` trims them; a `section`,
  counted against `MaxNodes`, with its name; and a task, `taskTxt` followed by `taskData`, counted against `MaxNodes`,
  with its name as written and its data with the colon (`AddTask`). The `topAxis` statement fails (see Error
  Handling).
- **`ParseClick`** reads a click statement in the seven forms of the grammar's `clickStatement` - the ids followed by
  `href`; by `call` with or without arguments; by `call`, with or without arguments, and then `href`; or by `href`
  and then `call`, with or without arguments - and makes the database calls of each form in the grammar's order:
  `SetClickEvent` for the call, then `SetLink` for the link. Before each call it checks the lookahead against the
  actions of the LALR state the parser is in (`CheckLookahead`), so that a token after a complete click statement is
  refused where jison refuses it.
- **`CountNode`** counts a task or section statement against `MaxNodes`.
- **`TextOf`** gives the text of a token that is kept and checks its length against `MaxLabelLength` (with
  `GanttModel.CheckLength`); **`Locate`** maps a token to the original text.
- **`Is`**, **`IsEnd`**, **`Advance`**, and **`Expect`**: `Is` tests the lookahead, `IsEnd` tests for the end of the
  tokens (jison's `$end`), `Advance` consumes the lookahead, and `Expect` consumes a token of a kind or fails.
- **`Fail`** builds the located exception for the lookahead: the expected kinds, each once, ordered by their jison
  terminal numbers, with their jison names, the token found, and a hint (`HintFor`, from the expected tokens and the
  token found). **`Lex`** runs a step that lexes a token and gives a lexical error a gantt hint.

#### Error Handling

- A token the grammar does not accept is a `MermaidSyntaxException` with code `mermaid-syntax-error`, located at the
  token in the original text, with the message `expected 'A' or 'B', found 'kind' ('text')` (or `found the end of the
  text`) and, as `Expected`, the jison names of the tokens of the LALR state where jison reports the error, in jison's
  order: `gantt` before the keyword; the 27 `LineStarts` (from `EOF` to `click`) where a statement must start;
  `taskData` after a task name; `callbackname` and `href` after the ids of a click statement; and the line starts with
  the click tokens that may still follow after a part of a click statement. A date, `accDescription`, a colon alone, or
  a `%%{` that the preprocessing left at the start of a statement is such a token. The hint is to start with the
  `gantt` keyword, to give a task its data after a colon, to complete the click statement, to write the task name before
  its colon or a date after it, to use `accDescr`, or to check the statement against the gantt syntax.
- Text that no lexer rule matches can follow only a `%%{` that the preprocessing left (its start condition has no
  rules); it is a `MermaidSyntaxException` with code `mermaid-syntax-error` and the message `unrecognized text ...`,
  located at the text, with no expected tokens and a hint to remove the `%%{`.
- **Mermaid's own failures.** The action of the `topAxis` statement calls a database function that Mermaid's gantt
  database does not have, and a task whose data holds only tags makes the database read past the end of its fields;
  Mermaid fails at both with a JavaScript error. The parser fails at the `topAxis` statement, and the model (see
  _GanttModel Unit Design_) at the task, with a `MermaidSyntaxException` with code `mermaid-syntax-error`, a message
  that starts `Mermaid fails`, no expected tokens, and a hint (to set `gantt.topAxis` in the configuration, or to give
  the task a start or a duration after its tags).
- More task and section statements together than `MaxNodes` are refused at the statement one too many, with the
  message `the diagram has more than N sections and tasks (MermaidLimits.MaxNodes)`; a kept token text longer than
  `MaxLabelLength` is refused at its token with the message `a text is N characters long, more than the limit of M
  (MermaidLimits.MaxLabelLength)`; both with code `mermaid-limit-exceeded` and a hint. The parser checks the text of
  every token it keeps: a title, a section, a task name, a task's data, a date or axis format, a tick interval, an
  excludes or includes list, a today marker, an accessibility text, and the ids, function name, arguments, and URL of a
  click statement. The texts the model derives from them are checked by the model.
- `Parse` throws `ArgumentNullException` for a null argument.

#### Dependencies

- **GanttModel** - the database the parser fills, and its `CheckLength` for the label limit.
- **Syntax subsystem** - `PreprocessedSource` and `MappedText` (the text mapped to the original), `DetectedDiagram`,
  the rule-driven lexer `SourceScanner` with `ScanRule`, `ScanMatcher`, and `ScanToken`, the token cursor
  `TokenCursor` (with `JoinAlternatives` for the list of expected tokens in a message), `ScanPatterns` (the JavaScript
  white space, line terminator, and word character classes), `Preprocessor.TrimJs` (JavaScript's `trim`), and
  `SourceScanner.Describe` (the description of unmatched text).
- **System** - `MermaidLimits`, `MermaidLocation`, `MermaidSyntaxException`, and `MermaidDiagnosticCodes`.

#### Callers

- The document pipeline of the system (`MermaidDocument.Pipeline.cs`): its `DiagramRegistry` calls `Parse` for a text
  whose detected type is `gantt`, with the limits of the open call, and keeps the returned `GanttModel` as the
  document's diagram.
- The unit and subsystem tests call `Parse` directly, and the vector tests compare the state of the model and its
  compiled tasks with Mermaid's database.

#### Known Differences From Mermaid

- **No regular expressions.** The lexer is a set of hand-written matchers with the same matches as jison's regular
  expressions, so lexing is linear in the text.
- **A hand-written parser instead of jison's tables.** The parser accepts the same language with one token of
  lookahead and checks the lookahead against the actions of the LALR state jison is in, so the grammar errors of the
  upstream vectors come at the identical tokens with the identical expected lists.
- **The first error only, located.** Jison reports the first error with its line; this unit reports it located by
  line and column in the original text, with a hint.
- **Mermaid's own failures are located.** The `topAxis` statement and a task whose data holds only tags fail as in
  Mermaid, but as a located `MermaidSyntaxException` with a hint in place of Mermaid's JavaScript error.
- **Limits.** The node and label limits refuse the text when the document is opened, with code
  `mermaid-limit-exceeded`; Mermaid has no such limits.
- **Accessibility.** `accTitle` and `accDescr` are metadata of the diagram only, since a rendered picture has no SVG
  to carry them.
