### ClassParser

![Class Structure](PlantUmlClassView.svg)

The `ClassParser` unit is the first software unit in the `Class` subsystem. Its source files are
`Class/ClassParser.cs` (the statement dispatch, the blocks, the names, the limits, and the errors) and
`Class/ClassParser.Members.cs` (the declarations, bodies, objects, maps, packages, notes, links, and hide and show).

#### Purpose

`ClassParser` reads the statements of a class or object diagram into a `ClassModel` as PlantUML's class diagram
factory reads them, reports what it reads but does not draw, and refuses what PlantUML refuses with located errors.
The unit's behavior is specified by the `CanvasNetPlantUml-Class-ClassParser-*` requirements in
`docs/reqstream/canvas-net-plant-uml/class/class-parser.yaml`.

#### Data Model

An instance holds the statements and the limits of one call and collects, in text order: the entities by id and by
short name, the packages by id, the notes (floating notes by alias), the links, the hide and show rules, the skin
parameters, the page texts, the direction, the scale, the name separator (`.` by default, `set namespaceSeparator`),
the open blocks (packages, namespaces, and `together` groups), the entity declared last and the link read last (the
targets of a note without `of` and of `note on link`), and the reports. Link ids are `link#k`, ids of notes without an
alias `note"k` (with a quote, which no name or alias can hold, so that they never take the id of an entity or a
floating note).

#### Key Methods

- `Parse(source, detected, limits)` - reads every statement (`ReadStatement`) and builds the model with the theme of
  the skin parameters applied as a graph diagram (`SkinParams.Apply(..., graph: true)`); an unclosed block is a
  located syntax error at its opening.
- `ReadStatement(index)` - in order: skin parameters and style blocks, closing braces, page texts
  (`SvekSyntax.TryReadPageText`), the direction, the keyword statements (declarations of classes and their kin,
  objects, and maps, packages and namespaces, notes, hide and show, the scale, `set`, and the statements that are
  recognized and reported), links (`SvekSyntax.TryReadLink`), and member lines `Name : text`; an association class is
  reported; an element keyword of another diagram type is a syntax error with the `allowmixing` hint; anything else is
  a syntax error naming the class diagram.
- `ReadDeclaration`, `ReadObject`, `ReadMap` - the kind from the keyword (`abstract class` and `abstract` are abstract
  classes; `circle` and `diamond` are reported and drawn as classes), the name (quoted, with a display before or after
  `as`), the generic parameters in angle brackets, the stereotypes (a spot `(X,color)` in a stereotype included), the
  colors (`#back`, `##line`, `#back;line:color;text:color`; a line style `##[dashed]` is reported), `extends` and
  `implements` (each a link), and a body in
  braces kept line by line until the closing brace; the entries `key => value` of a map.
- `Declare`, `ResolveEnd`, `Lookup`, `Create`, `EnsurePackage`, `ContextOf` - PlantUML's name rules: a qualified name
  creates its packages; inside a package a name is relative unless its first segment is a package at the top; a link
  end, a note target, or a member line looks the name up in the current package, then its ancestors, then by a unique
  short name, and creates a class otherwise; a declaration looks only in the current package; a name already used by a
  package or a note is a syntax error.
- `ReadPackage` - `package` and `namespace` with a name or a quoted title and an alias, stereotypes (`<<Folder>>`,
  `<<Frame>>`, `<<Node>>`, `<<Rectangle>>`, `<<Cloud>>`, `<<Database>>`, `<<Card>>` choose the container symbol), a
  color, and an optional body in braces closed by `}`.
- `ReadNote` - `note left|right|top|bottom of X`, `note ... : text` for the last entity, `note on link`, and
  `note "text" as N`, on one line or up to `end note`; links to a floating note connect it (the Svek layout draws a
  floating note with one link to an entity as a note with a pointer, as PlantUML does); a note on a package is reported
  and drawn as a floating note.
- `ReadLink` - the ends, the decorations, the style, the length, the direction, the labels, and the color that the
  shared link reader found; the parts that are not drawn (crow's feet, `redefines`, `defined by`, roles, qualifiers, a
  middle decoration, `norank`, link stereotypes, a link to a member `A::x`, association classes) are reported and
  dropped; links are bounded by `MaxEdges`.
- `ReadHideShow` - `hide|show [kind|<<stereotype>>|name] [empty] members|fields|attributes|methods|circle|stereotype`
  are rules; the other forms (hiding an element itself, hiding by visibility, ...) are reported.
- `CountNode`, `Open`, `CheckLabel` - the limits `MaxNodes` (entities and notes), `MaxNestingDepth` (blocks and the
  implicit packages of qualified names), and `MaxLabelLength` (every text).

#### Error Handling

Every refusal is a located `PlantUmlSyntaxException`: `plantuml-syntax-error` with a hint for malformed statements,
unclosed or unopened blocks, names used for another kind of element, invalid colors, and elements of other diagram
types; `plantuml-limit-exceeded` naming the limit. Reports of statements that are read but not drawn use
`plantuml-statement-unsupported`, and skin parameters that are not applied `plantuml-skinparam-ignored`.

#### Dependencies

The `ClassModel` unit, the Svek subsystem (`SvekSyntax`, the Svek enumerations), the Syntax subsystem
(`PreprocessedSource`, `SourceStatement`, `DetectedDiagram`, `DiagramDetector`), the Styling subsystem (`SkinTheme`,
`SkinParams`), and the Scenegraph subsystem (`PageTexts`).

#### Callers

`PlantUmlDocument` through its diagram registry, and the tests.
