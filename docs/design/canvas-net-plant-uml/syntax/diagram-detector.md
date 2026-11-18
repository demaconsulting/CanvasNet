### DiagramDetector

![Syntax Structure](PlantUmlSyntaxView.svg)

The `DiagramDetector` unit is the second software unit in the `Syntax` subsystem. Its source file is
`Syntax/DiagramDetector.cs`, which defines the internal static class `DiagramDetector` and the record
`DetectedDiagram`.

#### Purpose

`DiagramDetector` names the type of a diagram so that the document can hand a supported type to its parser and report
a type that is not drawn instead of a syntax error. It follows the order in which PlantUML's diagram factories try a
text: the sequence diagram first, then the other types, with the activity diagram of the new syntax after the
types that PlantUML lays out with Graphviz. The unit's behavior is specified by the
`CanvasNetPlantUml-Syntax-DiagramDetector-*` requirements in
`docs/reqstream/canvas-net-plant-uml/syntax/diagram-detector.yaml`.

#### Data Model

- `DetectedDiagram(DiagramType, IsSupported, Keyword, Location)` - the public type name, whether this version draws
  it (whether it is in `SupportedTypes`), the construct it was recognized by (an `@start` tag or a statement's first
  word), and its location.
- `SupportedTypes` - the drawn types; `TagTypes` - the `@start` tags other than `uml` with their types (`mindmap`,
  `gantt`, `wbs`, `json`, `yaml`, `salt`, `ditaa`, `dot`, `math`, `latex`, `regex`, `ebnf`, `chen`, `files`,
  `chronology`, `board`, `creole`, `wire`, `nwdiag`, `chart`, `def`, `git`, `project`, `flow`, `packetdiag`,
  `jcckit`).
- The keyword tables - the participant keywords and the keywords of the sequence diagram only, the keywords of the
  activity diagram only, the keywords both accept (titles, notes, styling, `else`, `end`), and the keywords of the
  other types with their type, in the order they are tried.

#### Key Methods

- `Detect(source)` - a tag other than `uml` names its type through `TagTypes`; an unknown tag is a
  `plantuml-unknown-diagram` error whose hint is the known tag at the smallest edit distance. Otherwise the non-empty
  statements are reduced to the ones outside multi-line text blocks (`OuterStatements`), and: a diagram whose
  statements are all common is an empty diagram (a syntax error); a diagram whose statements the sequence diagram all
  accepts is a sequence diagram; otherwise, when the activity diagram accepts them all and one is an activity
  statement, an activity diagram; otherwise the first statement with a marker of another type names that type;
  otherwise a syntax error at the first statement the more likely type (by the number of its specific statements)
  refuses.
- `Classify(text)` - the family of a statement: a sequence separator (`==`, `...`, `||`) is a sequence statement; a
  statement that starts with `:` or `|`, an activity arrow, or an activity keyword is an activity statement; a common
  keyword, a style block, or a directive is common; a participant keyword, a sequence keyword, or a message is a
  sequence statement.
- `IsSequenceArrow(text)` - finds a run of arrow characters (dashes, dots, heads, slashes, `x`, `o`, and a bracketed
  color) before the label that holds a head, a dash pair, or a half head, with a participant on each side (a name not
  in brackets or parentheses, or the found and lost ends `[`, `]`, and `?`).
- `OtherType(text)` - `[*]` is a state, `(*)` a legacy activity, a bracketed name followed by a gantt verb a gantt
  chart, another bracketed name a component, a parenthesized name a use case, and the keyword table names the rest; a
  time statement `@0` is a timing diagram.
- `OuterStatements`, `BlockCloser`, `IsCloser` - the statements of a multi-line `note`, `hnote`, `rnote`, `legend`,
  `title`, `header`, `footer`, `caption`, `ref`, `skinparam ... {`, and `<style>` block are text and are skipped up to
  the closing statement.
- `UnsupportedHint(detected)` and `EditDistance(a, b)` - the hint of a type that is not drawn, and the Levenshtein
  distance of two words.

#### Error Handling

Every refusal is a `PlantUmlSyntaxException`: `plantuml-unknown-diagram` for an unknown tag (at the tag, with the
nearest tag and `@startuml` expected), and `plantuml-syntax-error` for a diagram with nothing to draw (at its first
statement) and for statements no type accepts (at the refused statement, with a hint that shows the form of a
statement of the more likely type). No exception is caught.

#### Dependencies

The unit reads the `PreprocessedSource` of the `Preprocessor` unit and uses the root namespace's
`PlantUmlSyntaxException`, `PlantUmlDiagnosticCodes`, and `PlantUmlLocation`.

#### Callers

`PlantUmlDocument` detects every text it opens and reads `UnsupportedHint`; the diagram parsers use the multi-line
block rules (`BlockCloser`, `IsCloser`).
