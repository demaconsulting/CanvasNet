### SvekSyntax

![Svek Structure](PlantUmlSvekView.svg)

The `SvekSyntax` unit is the second software unit in the `Svek` subsystem. Its source files are `Svek/SvekSyntax.cs`
(names, page texts, and the common statements), `Svek/SvekSyntax.Links.cs` (the arrow grammar and the link statements),
and `Svek/SvekSyntax.Notes.cs` (the notes).

#### Purpose

`SvekSyntax` reads the statements that every diagram PlantUML lays out with Graphviz writes the same way, so the class,
state, and description parsers read them alike and as PlantUML does. The unit's behavior is specified by the
`CanvasNetPlantUml-Svek-SvekSyntax-*` requirements in `docs/reqstream/canvas-net-plant-uml/svek/svek-syntax.yaml`.

#### Data Model

The unit holds no state. It returns `SvekName(Text, Kind)` (`Plain`, `Quoted`, `Parenthesized`, `Bracketed`, `Colon`,
`Star`, `History`, `DeepHistory`, `Interface`; `Business` is set for a name in parentheses or colons followed by a
slash), `SvekArrow` (`TailDecor`, `HeadDecor`, `Length`, `Direction`, `Dotted`, `Doubled`,
`Text`, and from the brackets `Style`, `Thickness`, `Color`, `NoRank`, the unchecked `StyleText`, and the `Middle`
decoration), `SvekLinkStatement` (the ends, the arrow, the end labels, roles, and qualifiers, the label and its mark,
the color, the stereotypes, the weight, and whether a URL is written), `SvekNoteStatement` (the text, the side, the
target or `OnLink`, the alias, the fill), and `SvekHideShow(Show, What, Location)`.

#### Key Methods

- `IdentifierEnds(text, start)` and `NameEnds(text, start)` - the identifiers (`sep? letters (sep letters)*`, the
  separator being any character that is not a letter, digit, white space, quote, or one of `_ $ # : { } < >`, or `::`)
  and names that start at an index, longest first; `() name` and `() "Long name"` are interfaces, and `(x)/` and
  `:x:/` are the business forms (offered before the forms without the slash).
- `TryReadName`, `TryReadAlias`, `ReadStereotypes`, `ReadColor` - one name, `as Alias`, `<<a>> <<b>>`, `#color`.
- `TryReadArrow(text, start, ...)` - PlantUML's arrow grammar: a decoration of PlantUML's `decors1` table (longest
  first, with word boundaries around an `o`), a body of `-`, `.`, `=`, an optional style in brackets, a direction word
  (`left`, `right`, `up`, `down`, `le`, `l`, `ri`, `r`, `u`, `do`, `d`), a middle decoration, another style, more body,
  and a decoration of `decors2`. Left and right make the length 1; up and down make it at least 2.
- `TryReadLink(statement, ...)` - `[@weight] end1 [[q]] ["label"] [/role] arrow ["label"] [/role] [[q]] end2 [#color]
  [[[url]]] [<<stereotype>>] [: label]`: the ends, the decorations, and the direction words are tried from the longest
  as PlantUML's regular expression backtracks (so `A-B` is a link of length 1; a name in brackets after the arrow is a
  qualifier only when a second end follows it, so `[A] --> [B]` links two components); once the statement matches, the
  style in brackets and the color are checked and the label is split (`SplitLabel`: quoted end labels around it when the
  arrow has none, a `>` or `<` mark at its end or start).
- `TryReadNote(statements, ref index, ...)` - `note left|right|top|bottom [of Name] [#color] [: text]`,
  `note [side] on link [#color] [: text]`, `note "text" as N [#color]`, and `note as N` with a block up to `end note`.
- `TryReadPageText`, `TryReadDirection`, `ReadScale`, `TryReadHideShow`, `TakeLayoutSettings` - the page texts (as the
  activity parser reads them), `left to right direction` and `top to bottom direction`, `scale N` and `scale N/M`,
  `hide` and `show`, and the skin parameters `nodesep`, `ranksep` (taken out of the list) and `linetype` (reported).
- `CheckLabel`, `Error`, `Unsupported`, `ReadBlock`, `SkipSpaces`, `TryKeyword`, `FirstWord` - shared helpers.

#### Error Handling

A reader that does not recognize a statement returns false and leaves the index unchanged. A recognized statement that
cannot be read throws a located `PlantUmlSyntaxException` with the code `plantuml-syntax-error` and a hint: a style in
brackets that is not a style, a color that is not a color, an unclosed block, a floating note without a name or with an
unclosed quote, `note ... of` without a name, `note on` without `link`, and a note without text. `CheckLabel` refuses a
text longer than the label limit of `PlantUmlLimits` with `plantuml-limit-exceeded`. `TakeLayoutSettings` reports
`linetype` and a `nodesep` or `ranksep` that is not a number of pixels from 0 to 10000 as ignored skin parameters
(warnings). No regular expression runs on the input; the backtracking of the link grammar is bounded by
the few candidate ends, decorations, and direction words of a statement.

#### Dependencies

The Syntax subsystem (`SourceStatement`, `DiagramDetector.IsCloser`), the Styling subsystem (`SkinParams.TryParseColor`,
`SkinParamEntry`), the Scenegraph subsystem (`PageTexts`, `PageText`, `TextAlign`), and the root namespace's
`PlantUmlLimits`, `PlantUmlLocation`, `PlantUmlDiagnosticCodes`, `PlantUmlPendingReport`, and
`PlantUmlSyntaxException`.

#### Callers

The parsers of the graph diagrams (the `ClassParser` unit of the Class subsystem, the `StateParser` unit of the State
subsystem, and the `DescriptionParser` unit of the Description subsystem), the `DiagramDetector` unit (the graph
families), and the tests.
