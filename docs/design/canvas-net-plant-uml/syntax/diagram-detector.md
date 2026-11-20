### DiagramDetector

![Syntax Structure](PlantUmlSyntaxView.svg)

The `DiagramDetector` unit is the second software unit in the `Syntax` subsystem. Its source files are
`Syntax/DiagramDetector.cs`, which defines the internal static class `DiagramDetector` and the record
`DetectedDiagram`, and `Syntax/DiagramDetector.Graphs.cs`, the part of the class that detects the families PlantUML
lays out with Graphviz.

#### Purpose

`DiagramDetector` names the type of a diagram so that the document can hand a supported type to its parser and report
a type that is not drawn instead of a syntax error. It follows the order in which PlantUML's diagram factories try a
text: the sequence diagram first, then the activity diagram of the new syntax, then the class family (class and object
diagrams), the legacy activity diagram, the description family (use case, component, and deployment diagrams), and the
state diagram. The unit's behavior is specified by the
`CanvasNetPlantUml-Syntax-DiagramDetector-*` requirements in
`docs/reqstream/canvas-net-plant-uml/syntax/diagram-detector.yaml`.

#### Data Model

- `DetectedDiagram(DiagramType, IsSupported, Keyword, Location)` - the public type name, whether this version draws
  it (whether it is in `SupportedTypes`), the construct it was recognized by (an `@start` tag or a statement's first
  word), and its location.
- `SupportedTypes` - the drawn types (`sequence`, `activity`, `class`, `object`, `state`, `usecase`, `component`);
  `TagTypes` - the `@start` tags other than `uml` with their types (`mindmap`, `gantt`, `wbs`, `json`, `yaml`, `salt`,
  `ditaa`, `dot`, `math`, `latex`, `regex`, `ebnf`, `chen`, `files`, `chronology`, `board`, `creole`, `wire`, `nwdiag`,
  `chart`, `def`, `git`, `project`, `flow`, `packetdiag`, `jcckit`).
- The keyword tables - the participant keywords and the keywords of the sequence diagram only, the keywords of the
  activity diagram only, the keywords both accept (titles, notes, styling, `else`, `end`, and an aligned header or
  footer such as `left header`), and the keywords of the
  other types with their type, in the order they are tried; the class family keywords (`class`, `abstract`,
  `interface`, `enum`, `annotation`, `entity`, `object`, `map`, `package`, `namespace`, and the other class kinds and
  package symbols), and the keywords of the description family with the use case and component keywords that name its
  type.

#### Key Methods

- `Detect(source)` - a tag other than `uml` names its type through `TagTypes`; an unknown tag is a
  `plantuml-unknown-diagram` error whose hint is the known tag at the smallest edit distance. Otherwise the non-empty
  statements are reduced to the ones outside multi-line text blocks (`OuterStatements`), and: a diagram whose
  statements are all common is an empty diagram (a syntax error); a diagram whose statements the sequence diagram all
  accepts is a sequence diagram; otherwise, when the activity diagram accepts them all and one is an activity
  statement, an activity diagram (unless an action is never closed and a graph family reads the text: PlantUML's
  multi-line action needs its ending `;`, so `:Guest:` is an actor); otherwise the first graph family that accepts
  every statement (`DetectGraph`, tried at most once)
  names the type; otherwise the first statement with a marker of another type names that type;
  otherwise a syntax error at the first statement the more likely type (by the number of its specific statements)
  refuses.
- `Classify(text)` - the family of a statement: a sequence separator (`==`, `...`, `||`) is a sequence statement; a
  statement that starts with `:` or `|`, an activity arrow, or an activity keyword is an activity statement; a common
  keyword, a style block, or a directive is common; a participant keyword, a sequence keyword, or a message is a
  sequence statement.
- `IsSequenceArrow(text)` - finds a run of arrow characters (dashes, dots, heads, slashes, `x`, `o`, and a bracketed
  color) before the label that holds a head, a dash pair, or a half head, with a participant on each side (a name not
  in brackets or parentheses) or an end on the diagram border, read as PlantUML's `CommandExoArrowLeft` and
  `CommandExoArrowRight` read it: before the arrow `[`, `]`, or `?` with an optional circle `o` or cross `x`; after it
  `?` (or a lone bracket), or an arrow that ends with `]` after its head or body (`->]`, `->o]`, `->x]`, `-[#red]>]`),
  followed only by the activation shorthands.
- `IsBorderMessage(text)` - a message with one participant and one end on the diagram border (`[->`, `[o->`, `?o->`,
  `->]`, `->x]`, `->?`, `->o?`, ...), read from the part before the label only, so a label may hold brackets and
  arrows; the `SequenceParser` reports these messages.
- `DetectGraph(statements)` - finds the statements outside text blocks once, then tries the families in PlantUML's
  order: the class family accepts declarations, bodies
  (skipped as text by `ClassOuterStatements`), member lines `A : x`, packages, notes, hide and show, and links read by
  the `SvekSyntax` link reader whose ends are class names; the type is `object` when an object or a map is declared
  before any class, otherwise `class`. A text with `(*)` is then a legacy activity diagram; the description family
  accepts its keywords, bracketed components, parenthesized use cases and actors written `:name:` (with what follows
  them: an alias, stereotypes, a color), interfaces written `() name`, and their links,
  and is a use case diagram when any statement uses a use case construct (`actor`, `usecase`, `(name)`, `:name:`, or a
  link to one), else a component diagram when any statement uses a component construct (`component`, `interface`,
  ports, `[name]`, `() name`, or a link to one), else a deployment diagram; the state family accepts states, `[*]`, and
  their links.
- `IsClassOnlyLink(statement)` - a link that the sequence diagram reads but PlantUML's sequence diagram refuses (a
  dotted or doubled body, a direction word, a decoration the sequence diagram does not draw, or an end name with
  characters outside letters, digits, `_`, `.`, and `@`), so that the text falls through to the class family; a
  message to or from the border never is (`?o-> A` reads as a link with the end `?o`).
- `JoinTypes(types)` - joins type names as `a, b, and c` for the hint.
- `OtherType(text)` - `[*]` is a state, `(*)` a legacy activity, a bracketed name followed by a gantt verb a gantt
  chart, another bracketed name a component, a parenthesized name a use case, and the keyword table names the rest; a
  time statement `@0` is a timing diagram.
- `OuterStatements`, `BlockCloser`, `IsCloser` - the statements of a multi-line `note`, `hnote`, `rnote`, `legend`,
  `title`, `header`, `footer`, `caption` (also aligned, `left footer`), `ref`, `skinparam ... {`, and `<style>` block
  are text and are skipped up to the closing statement; a closer that starts with `end` is also accepted with a space
  after `end` (`end legend` closes `legend` as `endlegend` does). An activity action that opens with `:` (after an
  optional color) and does not end with `;` (or one of the box-style endings) on its line is text up to the line that
  ends it, which is skipped with it; a `floating note` opens a note block like `note`. The graph families read the
  statements with `openActionIsText` false: an action that is never closed is one statement and the lines after it
  are statements; no line after it ends an action, so every later action is one statement too and each statement is
  visited at most twice (`EndsInOpenAction` tells whether the statements end inside an action).
- `UnsupportedHint(detected)` and `EditDistance(a, b)` - the hint of a type that is not drawn, and the Levenshtein
  distance of two words.

#### Error Handling

Every refusal is a `PlantUmlSyntaxException`: `plantuml-unknown-diagram` for an unknown tag (at the tag, with the
nearest tag and `@startuml` expected), and `plantuml-syntax-error` for a diagram with nothing to draw (at its first
statement) and for statements no type accepts (at the refused statement, with a hint that shows the form of a
statement of the more likely type). No exception is caught.

#### Dependencies

The unit reads the `PreprocessedSource` of the `Preprocessor` unit, reads links and names with the `SvekSyntax` unit of
the `Svek` subsystem, and uses the root namespace's `PlantUmlSyntaxException`, `PlantUmlDiagnosticCodes`, and
`PlantUmlLocation`.

#### Callers

`PlantUmlDocument` detects every text it opens and reads `UnsupportedHint`; the diagram parsers use the multi-line
block rules (`BlockCloser`, `IsCloser`, `OpensAction`, `EndsAction`).
