# CanvasNet PlantUML Package

## API Reference

### PlantUmlDocument

`PlantUmlDocument` (namespace `DemaConsulting.CanvasNet.PlantUml`) is distributed via the separate
`DemaConsulting.CanvasNet.PlantUml` NuGet package, whose only dependency is the core `DemaConsulting.CanvasNet` package.
It reads diagrams written in the PlantUML text language and draws **sequence diagrams** and **activity diagrams** (the
new syntax) as static pictures with PlantUML's default look and layout, using only managed code and the fonts bundled
with the libraries: no Java, no Graphviz, and no network. Every other PlantUML diagram type is recognized and reported,
never drawn wrongly. The package is derived with attribution from the MIT-licensed PlantUML sources and is **not
affiliated with or endorsed by** the PlantUML project.

The lists below are also the *Supported* sections of `canvasnet help plantuml sequence` and `canvasnet help plantuml
activity` and the codes of `canvasnet help plantuml unsupported` and `canvasnet help plantuml diagnostics`; a test keeps
them the same.

### Supported Sequence Syntax

- **Sequence diagrams**: a diagram whose statements are all sequence statements is a sequence diagram. It is laid out
  like PlantUML's teoz engine: the participants stand in a row, and every message, note, and group is placed below the
  one before it.
- **Participants**: `participant`, `actor`, `boundary`, `control`, `entity`, `database`, `collections`, and `queue`,
  with a quoted display name, `as` for an alias, a `#color`, and `order N`; a participant named only by a message is a
  participant too.
- **Messages**: the arrows `->`, `-->`, `->>`, `-->>`, `<-`, `<--`, `<->`, `-\`, `-/`, `->x`, `->o`, and `-[#color]>`
  (two dashes are dotted), self messages, and a label after a colon with `\n` line breaks.
- **Activations**: `activate` and `deactivate` (with a color), `destroy`, `return`, and the `++`, `--`, `**`, and `!!`
  shorthands of a message.
- **Notes**: `note`, `hnote`, and `rnote` `left`, `right`, or `over` one or two participants, on one line or up to `end
  note`, with a color.
- **Groups**: `alt` and `else`, `opt`, `loop`, `par`, `break`, `critical`, and `group`, nested, with a color and a
  condition, closed by `end`.
- **Separators**: `== divider ==`, `...` delays (with a text), and `|||` or `||N||` space.
- **Boxes**: `box "title" #color` ... `end box` around participants.
- **Numbers**: `autonumber` with a start, a step, and a format, `autonumber stop`, and `autonumber resume`.
- **Page texts**: `title`, `header`, `footer`, `caption`, and `legend` ... `endlegend`, and `hide footbox`.
- **Scale**: `scale N` and `scale N/M` multiply the natural size.

### Supported Activity Syntax

- **Activity diagrams**: a diagram in the new activity syntax (`start`, `:action;`, `if`, `while`, ...) is an activity
  diagram. It is laid out like PlantUML's ftile composition, with no Graphviz.
- **Start and end**: `start`, `stop`, `end`, `kill`, and `detach`.
- **Actions**: `:text;` on one or more lines, with a `#color` before the colon and creole markup in the text.
- **Decisions**: `if (condition) then (label)`, `elseif`, `else`, and `endif` (or `end if`), with the `is` and `equals`
  forms, nested.
- **Loops**: `while (condition) is (label)` ... `endwhile (label)`, and `repeat` ... `repeat while (condition) is
  (label) not (label)`, with `backward:`.
- **Parallel branches**: `fork`, `fork again`, `end fork`, and `end merge`; `split`, `split again`, and `end split`.
- **Swimlanes**: `|Lane|` and `|#color|Lane|`; the lanes are columns in the order they appear.
- **Partitions**: `partition Name { ... }` and `group` frames around instructions.
- **Notes**: `note left` or `note right`, on one line or up to `end note`, floating notes, and a color.
- **Arrows**: `-> label;` before an instruction, and the `-[#color]->`, `-[dashed]->`, `-[dotted]->`, `-[bold]->`, and
  `-[hidden]->` styles.
- **Connectors**: `(A)` circles that join two parts of a flow.
- **Page texts**: `title`, `header`, `footer`, `caption`, and `legend` ... `endlegend`, and `scale N`.

### PlantUML Unsupported Features and Strict Mode

A construct that PlantUML defines but this package does not draw is reported as a `PlantUmlUnsupportedFeature` with a
stable `Feature` code, a message, a location, and a hint, and the rest of the diagram is still drawn.
`ThrowOnUnsupportedFeature` turns the first one into a `PlantUmlUnsupportedFeatureException`. A **warning**
(`PlantUmlDiagnostic`) is a questionable construct that still renders; strict mode does not fail on a warning.

The codes, with their kind and when each is reported:

- **`plantuml-diagram-unsupported`**: Unsupported: The diagram type is not drawn by this version (every type but
  sequence and activity diagrams); the picture is empty.
- **`plantuml-multiple-diagrams`**: Unsupported: The text has more than one `@start` block; only the first one is drawn.
- **`plantuml-preprocessor-unsupported`**: Unsupported: A preprocessor directive outside the subset (`!if`, `!function`,
  `!procedure`, `!foreach`, built-in `%functions`, `!define` with parameters); it is dropped with the block it opens.
- **`plantuml-include-unsupported`**: Unsupported: An include that is never followed: a URL, the standard library
  (`<name>`), a part of a file (`file!part`), `!includeurl`, `!includesub`, `!includedef`, or `!import`.
- **`plantuml-theme-unsupported`**: Unsupported: A `!theme` line; the default look is drawn.
- **`plantuml-style-unsupported`**: Unsupported: A `<style>` block; it is skipped.
- **`plantuml-statement-unsupported`**: Unsupported: A statement PlantUML defines that this version does not draw (`ref
  over`, `newpage`, `switch`, `goto`, a specification and description language box style, ...); the rest of the diagram
  is drawn.
- **`plantuml-markup-unsupported`**: Unsupported: Creole markup outside the subset (images, sprites, icons, tables,
  lists, headings); the markup is drawn as text.
- **`plantuml-syntax-error`**: Error (plantuml-error): a statement no parser of the diagram type accepts, an empty
  diagram, or text that is not valid UTF-8; the message gives the file, the line, and the column, and the hint suggests
  the fix.
- **`plantuml-unknown-diagram`**: Error (plantuml-error): an `@start` tag PlantUML does not define; the hint names the
  nearest one.
- **`plantuml-include-failed`**: Error (plantuml-error): an `!include` file that is missing or cannot be read; a file
  the file-access mode denies is file-access-denied instead.
- **`plantuml-limit-exceeded`**: Error (budget-exceeded): a limit of the library (nodes, edges, includes, nesting, label
  length, layout work, painted pixels); see 'canvasnet help limits'.
- **`plantuml-render-failed`**: Error (plantuml-error): the diagram cannot be laid out; please report it.
- **`plantuml-skinparam-ignored`**: Warning: a `skinparam` setting outside the supported subset, or with a value that
  cannot be used; the message says why.
- **`plantuml-pragma-ignored`**: Warning: a `!pragma` line, which has no effect.
- **`plantuml-text-missing-glyph`**: Warning: a character that the font of its text does not have; it is drawn with a
  bundled fallback font, or as a box when no bundled font has it.
- **`plantuml-diagnostics-suppressed`**: Warning: more reports than the diagnostics limit; the rest are not listed.
