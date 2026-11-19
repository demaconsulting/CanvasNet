### ActivityParser

![Activity Structure](PlantUmlActivityView.svg)

The `ActivityParser` unit is the first software unit in the `Activity` subsystem. Its source files are
`Activity/ActivityParser.cs` (the entry point, the state of a read, the reports, the limits, and the shared readers of
colors, labels, and texts) and `Activity/ActivityParser.Statements.cs` (one reader per statement).

#### Purpose

`ActivityParser` reads the statements of an activity diagram of the new syntax as PlantUML's activity commands read
them and builds the tree of instructions of an `ActivityModel`. The unit's behavior is specified by the
`CanvasNetPlantUml-Activity-ActivityParser-*` requirements in
`docs/reqstream/canvas-net-plant-uml/activity/activity-parser.yaml`.

#### Data Model

The state of a read: the statements, the limits, the reports, the skin parameters in order, the swimlanes in order of
declaration, the root list, the stack of open blocks (if, while, repeat, fork, split, group), the arrow that leads into
the next instruction (`ActivityLink`), the current swimlane, whether swimlanes may still be declared, the number of
nodes, the page texts, and the scale.

#### Key Methods

- `Parse(source, detected, limits)` - reads every statement and builds the model: the skin parameters are applied, the
  reports are sorted into text order, and the arrows are counted against `MaxEdges`.
- **Statements** (hand-written scanners, no regular expressions), as `ActivityDiagram3` gives them meaning:
  - `start`, `stop`, `end`; `kill` and `detach` end the flow after the last instruction; a stop or end right after a
    while loop is drawn as the loop's special exit; stop and end keep a pending arrow.
  - `:text;` of one or several lines, after an optional `#color`; the box-style endings `|`, `<`, `>`, `/`, `]`, `}`
    and the stereotypes are reported and drawn as a plain box.
  - `if (test) then (label)` and `if (test) is (label) then` / `equals`; `elseif` (also `else if` and a labeled
    `(label) elseif`), `else (label)`, `endif` / `end if`.
  - `while (test) is (label)`, `endwhile (label)` / `end while` / `while end`; `repeat` and `repeat :label;`,
    `repeat while (test) is (label) not (label)` with an optional arrow back, `backward :text;` with optional arrows.
  - `fork`, `fork again`, `end fork {label}` / `fork end`, `end merge`; `split`, `split again`, `end split`.
  - `->` arrows: `-> label;`, `-[#color,dashed|dotted|bold|hidden]->`; the arrow leads into the next instruction (or
    ends the current branch).
  - `|Lane|`, `|#color|Lane|`, `|Lane| display`, and `swimlane name as display` (before the first instruction); the
    current swimlane of every instruction.
  - `note left|right[: text]` and its multi-line form up to `end note`, `floating note`, with a color; a note before
    any instruction stands alone in the flow.
  - `(A)` connectors; `partition Name {` and `group Name` (framed), `package`, `rectangle`, and `card` (reported and
    drawn without a frame), closed by `}` or `end group`.
  - Page texts (`title`, `header`, `footer`, `caption`, `legend` and their multi-line forms), `scale`, and
    `skinparam` (line and block forms) through the shared readers.
  - `switch` blocks (skipped up to `endswitch`), `goto`, `label`, `break`, `hide`, `show`, `skin`, and `mainframe` are
    reported as `plantuml-statement-unsupported` and not drawn.
- `CountNode` - every instruction and swimlane counts against `MaxNodes`; every block opened counts against
  `MaxNestingDepth`; every label against `MaxLabelLength`.

#### Error Handling

A malformed or misplaced statement (an `else` outside an if, an `endwhile` without a loop, a swimlane after the first
instruction, an action without its `;`, a block that is not closed) throws `PlantUmlSyntaxException` with
`plantuml-syntax-error` at the statement and a hint that shows the expected form. A limit throws
`PlantUmlSyntaxException` with `plantuml-limit-exceeded`, at the statement that crosses it (the arrows at the
construct the type was detected by).

#### Dependencies

The Syntax subsystem (`SourceStatement`, `DiagramDetector` helpers), the Styling subsystem (`SkinParams`, `SkinTheme`),
the Scenegraph subsystem (`PageTexts`), and the model classes.

#### Callers

`DiagramRegistry.Parse` for the `activity` type; the tests.
