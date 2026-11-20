### StateParser

![State Structure](PlantUmlStateView.svg)

The `StateParser` unit is the first software unit in the `State` subsystem. Its source files are `State/StateParser.cs`
(the statement loop, the model, the names, the pseudo-states, the limits, and the errors) and
`State/StateParser.Statements.cs` (the statements: states, descriptions, separators, closers, transitions, and notes).

#### Purpose

`StateParser` reads the statements of a state diagram into a `StateModel` as PlantUML's state diagram factory reads
them, reports what it reads but does not draw, and refuses what PlantUML refuses with located errors. The unit's
behavior is specified by the `CanvasNetPlantUml-State-StateParser-*` requirements in
`docs/reqstream/canvas-net-plant-uml/state/state-parser.yaml`.

#### Data Model

An instance holds the statements and the limits of one call and collects, in text order: the elements by id and by
name, the floating notes by alias, the notes, the transitions, the skin parameters, the page texts, the direction, the
scale, `hide empty description`, the open composite states with the locations of their openers, the number of
concurrent regions read so far (PlantUML numbers them `CONC1`, `CONC2`, ... across the diagram), the element and the
transition read last (the targets of a note without `of` and of `note on link`), and the reports. Transition ids are
`link#k`, ids of notes without an alias `note"k` (with a quote, which no name or alias can hold, so that they never
take the id of a state or a floating note).

#### Key Methods

- `Parse(source, detected, limits)` - reads every statement (`ReadStatement`), checks the transitions between regions
  (`CheckConcurrentTransitions`), and builds the model with the theme of the skin parameters applied as a graph
  diagram (`SkinParams.Apply(..., graph: true)`); an unclosed composite state is a located syntax error at its opening.
- `ReadStatement(index)` - in order: skin parameters and style blocks, the closers `}` and `end state`, the concurrent
  separators (`IsConcurrentSeparator`: two or more `-` or two or more `|`), page texts
  (`SvekSyntax.TryReadPageText`), the direction, the keyword statements (`state`, `note`, `hide` and `show`, `scale`,
  `json` and `map` blocks, and the statements that are recognized and reported), transitions
  (`SvekSyntax.TryReadLink`), and descriptions `Name : text`; anything else is a syntax error naming the state
  diagram.
- `ReadState`, `ReadStateName`, `ApplyStereotype`, `ReadStateColors` - the name (a name, a quoted display with an alias
  in either order, or a quoted name alone), the stereotypes (`<<choice>>`, `<<fork>>`, `<<join>>`, `<<start>>`,
  `<<end>>`, `<<junction>>`, `<<history>>`, and `<<history*>>` choose the kind; the border symbols `<<entryPoint>>`,
  `<<exitPoint>>`, `<<inputPin>>`, `<<outputPin>>`, `<<expansionInput>>`, `<<expansionOutput>>`, and `<<sdlreceive>>`
  are reported and drawn as states), the colors (`#back`, `##line`, `#back;line:color`; a line style `##[dashed]` is
  reported), and a block opener (`{` or `begin`) or a description after a colon.
- `Find`, `Resolve`, `Create`, `EnsureComposite` - PlantUML's name rules: a member id is the region's prefix and the
  name; a name is looked up in the current region, then in the regions around it, then by a unique name; otherwise a
  state is created in the current region; opening a block or naming a history of a state makes it composite (a
  pseudo-state cannot be).
- `Pseudo(kind, region, location)` - the start, end, and histories of a region, created once with PlantUML's ids
  (`.start.` and `.end.` at the top, `.historical.` and `.deephistory.` for the histories, `P..start.S` inside a region
  with prefix `P` whose last segment is `S`).
- `ReadConcurrentSeparator`, `Close` - a separator adds a region to the innermost composite state (the first separator
  decides stacked or side by side); a separator outside a composite state is reported; a closer without an open
  composite state is a syntax error.
- `ReadTransition`, `ResolveEnd`, `FindScopedNote`, `RawLabel` - the ends (`[*]` is the start as a source and the end
  as a target, `[H]` and `[H*]` the histories of the current composite state, `X[H]` and `X[H*]` those of `X`, a
  floating note by its alias, or inside a composite state by a short name that the prefix of the current region or of
  a region around it qualifies into a note's alias, as PlantUML links it, unless a state of that name is nearer), the
  decorations, the style, the thickness, the color, the length, the direction, and the label as written after its
  colon; arrows that are not transition arrows are syntax errors, and a cross at the start and link stereotypes are
  reported.
- `CheckConcurrentTransitions` - a transition between two different concurrent regions is a syntax error (PlantUML
  refuses it); one that leaves the first region of a composite state with concurrent regions is reported, as the Svek
  engine draws it from the border.
- `ReadNote`, `TryReadDescription`, `AddDescription` - `note left|right|top|bottom of X`, `note ... : text` for the last
  element, `note on link`, and `note "text" as N`, on one line or up to `end note`; a note on a composite state is
  reported and drawn as a floating note of the composite's region; descriptions are added line by line.
- `CountNode`, `CheckLabel` - the limits `MaxNodes` (elements and notes), `MaxEdges` (transitions), `MaxNestingDepth`
  (composite states), and `MaxLabelLength` (every text).

#### Error Handling

Every refusal is a located `PlantUmlSyntaxException`: `plantuml-syntax-error` with a hint for malformed statements,
unclosed or unopened composite states, transitions between concurrent regions, pseudo-states made composite, a state
or a pseudo-state whose id is the alias of a floating note (and the reverse), invalid colors, and statements of other
diagram types; `plantuml-limit-exceeded` naming the limit. Reports of statements that
are read but not drawn use `plantuml-statement-unsupported`, and skin parameters that are not applied
`plantuml-skinparam-ignored`.

#### Dependencies

The `StateModel` unit, the Svek subsystem (`SvekSyntax`, the Svek enumerations), the Syntax subsystem
(`PreprocessedSource`, `SourceStatement`, `DetectedDiagram`), the Styling subsystem (`SkinTheme`, `SkinParams`), and
the Scenegraph subsystem (`PageTexts`).

#### Callers

`PlantUmlDocument` through its diagram registry, and the tests.
