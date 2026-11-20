### DescriptionParser

![Description Structure](PlantUmlDescriptionView.svg)

The `DescriptionParser` unit is the first software unit in the `Description` subsystem. Its source files are
`Description/DescriptionParser.cs` (the statement loop, the model, the names, the limits, and the errors) and
`Description/DescriptionParser.Statements.cs` (the statements: declarations, containers, ports, links, and notes).

#### Purpose

`DescriptionParser` reads the statements of a description diagram (use case, component, or deployment) into a
`DescriptionModel` as PlantUML's description diagram factory reads them, reports what it reads but does not draw, and
refuses what PlantUML refuses with located errors. The unit's behavior is specified by the
`CanvasNetPlantUml-Description-DescriptionParser-*` requirements in
`docs/reqstream/canvas-net-plant-uml/description/description-parser.yaml`.

#### Data Model

An instance holds the statements, the limits, and the detected type (for the messages) of one call and collects, in
text order: the elements by id and by the name they were created with, the floating notes by alias, the notes, the
links, the ports (by id and by name; read, not drawn), the skin parameters, the page texts, the direction, the scale,
the open blocks (containers and `together` groups) with the locations of their openers, the element and the link read
last (the targets of a note without `of` and of `note on link`), and the reports. Link ids are `link#k`; the ids of
notes without an alias are `note`, a line feed, and a number, and the names of containers without a name `group`, a
line feed, and a number (no name or alias holds a line feed, while a name in brackets, parentheses, or colons may hold
a quote, so these ids never take the id of an element or a floating note).

#### Key Methods

- `Parse(source, detected, limits)` - reads every statement (`ReadStatement`) and builds the model (`Build`): a name
  only written in links becomes an actor when the diagram has a use case or an actor (not their business forms), else
  an interface, as PlantUML's `makeDiagramReady` decides; every element whose symbol has no drawing of its own is
  reported as drawn as a rectangle; the theme is the description theme (`SkinTheme.Description`) with the skin
  parameters applied as a graph and description diagram (`SkinParams.Apply(..., graph: true, description: true)`); an
  unclosed block is a located syntax error at its opening.
- `ReadStatement(index)` - in order: skin parameters and style blocks, the closer `}`, page texts, the direction, links
  (`SvekSyntax.TryReadLink`, first, as PlantUML tries its link command before its declarations and notes), the keyword
  statements (the symbol keywords of `SymbolKeywords` followed by a name or a block opener, `port`, `portin`,
  `portout`, `archimate`, `note`, `scale`, `together`, `json` and `map` blocks, `hide`, `show`, `remove`, `restore`,
  `allowmixing`, `set`, `skin`, `url`, `newpage`, and `mainframe`), the declarations without a keyword (`IsShorthand`:
  a statement starting with `[`, `(`, or `:`, or a name with `as`), the statements of class and state diagrams (a
  syntax error with the allowmixing hint), and anything else (a syntax error naming the detected type).
- `ReadDeclaration`, `ReadNames`, `TryReadElementName`, `Normalize`, `ShapeSymbol` - the names of a declaration: a code
  and a display in the forms `"Display" as Code`, `Display as Code`, and `Code as "Display"` (with stereotypes before
  `as` kept), where a name in brackets is a component, `() x` an interface, `(x)` a use case and `(x)/` a business use
  case, and `:x:` an actor and `:x:/` a business actor, overriding the keyword; a name and `as` alone is an actor (the
  actor style); `[H]` and `[H*]` are components named `H` and `H*`, as PlantUML's description names exclude only
  `[*]`. Then the stereotypes, a URL (reported), the colors (`ReadColors`: `#back`, `#back;line:color;text:color`,
  `##line`; a line style is reported), and the end: nothing, a block opener (a container of a symbol of
  `ContainerSymbols`; a keyword with a block opener alone is an anonymous container), the opener of a multi-line
  description (`[` after a keyword and a plain code, or an unclosed quote after `as`; `ReadMultiline` reads up to the
  line that ends with `]` or `"`, and the description replaces the name), or else a syntax error (a description after
  a colon and a one-line description in brackets are refused, as PlantUML refuses them).
- `Declare`, `Create`, `Join` - a declaration names an element of the current container (whose id is the container's
  id and the name joined with a dot); a declaration that finds an element with another symbol, or a container where an
  element is (and the reverse), is refused as PlantUML refuses it ("This element is already defined"); a container of
  the same symbol is opened again.
- `ReadArchimate`, `ReadPort` - an archimate element is read as a declaration (its color skipped) and drawn as a
  rectangle; a port is read inside a container only (PlantUML refuses it at the top), reported, and its links left out.
- `ResolveEnd`, `Find`, `IsPort` - the end of a link or the target of a note: a floating note by its alias, an element
  of the current container, of the containers around it, or by the name only one element was created with (a name of
  elements in several containers is refused), a port (null: the link is reported and left out), or else a new element
  in the current container with the symbol of its shape (`Unknown` for a plain or quoted name); `[*]` and `X[H]` are
  syntax errors.
- `ReadLink` - the decorations (those the engine does not draw reported and drawn plain), the style
  (`ResolveStyle(doubledIsBold: true)`: a body of `.` is dashed and of `=` bold), the thickness, the color, the length,
  the direction, the labels with the direction mark and the end labels, and the weight; middle decorations, roles,
  qualifiers, `norank`, and stereotypes of links are reported.
- `ReadNote` - `note left|right|top|bottom of X`, `note ... : text` for the last element, `note on link`, and
  `note "text" as N`, on one line or up to `end note`; a note on a container is reported and drawn as a floating note
  in it; a note on a port is reported and left out.
- `CountNode`, `CheckLabel`, `Open` - the limits `MaxNodes` (elements, notes, and ports), `MaxEdges` (links),
  `MaxNestingDepth` (open blocks), and `MaxLabelLength` (every text).

#### Error Handling

Every refusal is a located `PlantUmlSyntaxException`: `plantuml-syntax-error` with a hint for malformed declarations,
a redeclaration with another symbol, unclosed or unopened containers, an unclosed multi-line description, a port
outside a container, a container of a symbol that cannot hold elements, an ambiguous short name, a note alias that
names an element, invalid colors, pseudo-states of a state diagram, and statements of other diagram types;
`plantuml-limit-exceeded` naming the limit. Reports of statements that are read but not drawn use
`plantuml-statement-unsupported`, and skin parameters that are not applied `plantuml-skinparam-ignored`.

#### Dependencies

The `DescriptionModel` unit, the `DescriptionRenderer` unit (`IsDrawn`, for the reports of the symbols drawn as
rectangles), the Svek subsystem (`SvekSyntax`, `SvekGeometry.IsDrawn`, the Svek enumerations), the Syntax subsystem
(`PreprocessedSource`, `SourceStatement`, `DetectedDiagram`), the Styling subsystem (`SkinTheme`, `SkinParams`), and
the Scenegraph subsystem (`PageTexts`).

#### Callers

`PlantUmlDocument` through its diagram registry, and the tests.
