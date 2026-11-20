### SequenceParser

![Sequence Structure](PlantUmlSequenceView.svg)

The `SequenceParser` unit is the first software unit in the `Sequence` subsystem. Its source files are
`Sequence/SequenceParser.cs` (the reading loop, the participants, the model, and the arrow reader) and
`Sequence/SequenceParser.Statements.cs` (the reader of each statement and the statement scanner).

#### Purpose

`SequenceParser` reads the statements of a sequence diagram as PlantUML reads them and produces the immutable
`SequenceModel`. It reads by leading keywords, arrows, and a small left-to-right scanner, without regular expressions,
so the work is linear in the text. The unit's behavior is specified by the `CanvasNetPlantUml-Sequence-SequenceParser-*`
requirements in `docs/reqstream/canvas-net-plant-uml/sequence/sequence-parser.yaml`.

#### Data Model

The parser keeps, while it reads: the participants in declaration order with a map by code; the events; the life
changes that belong to each message; the boxes being read; the event indexes of the messages that started the open
activations (for `return`); the open groups; the autonumber state (on or off, the next number, the step, the format);
the page texts; the foot box flag; the scale; the skin parameters; and the pending reports.

#### Key Methods

- `Parse(source, detected, limits)` - reads every non-empty statement, checks that every group and box is closed, and
  builds the model: the participants are sorted by their `order` values (0 when none, declaration order otherwise), the
  events and boxes are mapped to the sorted indexes, the life changes are attached to their messages, the skin
  parameters are applied to the default theme (`hide footbox` turns the foot box off), and the reports are put in text
  order.
- Statements, in the order they are tried: `skinparam` and `<style>` (Styling); dividers `== text ==`, delays `...`
  and `... text ...`, spaces `|||` and `||N||`; participant declarations `kind name [as alias] [#color] [order N]` (a
  quoted name is the display; with two plain names the second is the alias; `<<stereotype>>` and `[[link]]` are
  reported); messages; notes `note|hnote|rnote left|right [of X]`, `over X[, Y]`, `across` with a color, on one line
  after a colon or up to `end note`; page texts `title`, `[left|center|right] header|footer`, `caption` (on one line or
  as a block), and `legend [left|right|center]` ... `endlegend`; `activate X [#color]`, `deactivate X`, `destroy X`;
  `return [#color] [label]`; `autonumber [start] [step] ["format"]`, `autonumber stop`, `autonumber resume`; `box
  ["title"] [#color]` ... `end box`; groups `alt|opt|loop|par|break|critical [#color] [condition]` and `group [title]
  [[condition]]`, `else [condition]`, `end`; `hide|show footbox`; `scale N` and `scale N/M` (0 to 100).
- Messages: a name (quoted, or letters, digits, and `_ . @ $`), an arrow, a name, the shorthands `++`, `--`, `**`,
  `!!`, an activation color, and a label after a colon. The arrow reader (`ArrowParser`) reads an optional `o` or `x`,
  a left head (`<`, `<<`, `/`, `//`, `\`, `\\`), a body of dashes with optional `[#color,dashed]` options (two dashes
  are dotted), a right head (`>`, `>>`, `\`, `\\`, `/`, `//`), and an optional `o` or `x`; an `x` replaces the head on
  its side, an `o` adds a circle, a doubled head is open, and an arrow with a head on its left end only points from the
  right participant to the left one.
- Life changes: `++` activates the receiver, `--` deactivates the sender, `!!` destroys the receiver; an `activate`,
  `deactivate`, or `destroy` right after a message involving its participant belongs to that message, else it is an
  event of its own. An activation pushes its message as the activating message; a deactivation pops it.
- `return`: a reply from the receiver of the innermost activating message to its sender with the same heads and a
  dotted body, which deactivates the receiver; with no open activation, a reply to the last message.
- Autonumber: the number is drawn bold (`<b>N`) or, with a format, with the first run of `0` and `#` outside markup
  replaced by the number padded to the count of zeros.
- `note left|right` without a participant right after a message is drawn on the row of that message; later, beside
  that message's participant on that side.
- For `group Title [condition]` the tab text keeps the space before the bracket, as PlantUML measures it.

#### Error Handling

An invalid statement throws `PlantUmlSyntaxException` with `plantuml-syntax-error`, located at the statement (or at the
part of it that is wrong) with a hint: an unknown statement, a message without a sender or receiver or with text after
it, an arrow that is not an arrow, an incomplete declaration, an invalid color, a note without a position or not closed,
a note without a participant before any message, `else` or `end` outside a group, an `end` that closes nothing, a nested
or unclosed box, an unclosed group, `return` with nothing to return to, and a space that is not a number. More
participants or boxes than `MaxNodes`, more messages than `MaxEdges`, a text longer than `MaxLabelLength`, and groups
nested deeper than `MaxNestingDepth` throw `plantuml-limit-exceeded`. The statements PlantUML accepts that are not
drawn - `ref over` (with its block), `newpage`, `create`, `**`, `mainframe`, `autoactivate`, `ignore newpage`, `skin`,
`also`, `par2`, messages to or from the border (`[->`, `->]`, `?->`, `->?`, with a circle or a cross at the border end,
recognized by `DiagramDetector.IsBorderMessage` from the part before the label), parallel statements (`& A -> B`, read
and drawn on their own row), stereotypes and links of participants, `hide` and `show` other than the foot box, other
`scale` forms, and `autonumber` options other than a start, a step, and a format - are reported as
`plantuml-statement-unsupported` and skipped (or drawn without that part).

#### Dependencies

The Syntax subsystem (`SourceStatement`, `DiagramDetector` helpers), the Styling subsystem (`SkinParams`,
`SkinTheme`), the Scenegraph subsystem (`PageText`, `PageTexts`, `TextAlign`), and the model records.

#### Callers

`DiagramRegistry.Parse` for the `sequence` type.
