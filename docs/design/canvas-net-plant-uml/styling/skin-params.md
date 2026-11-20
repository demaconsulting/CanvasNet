### SkinParams

![Styling Structure](PlantUmlStylingView.svg)

The `SkinParams` unit is the second software unit in the `Styling` subsystem. It is two source files: `SkinParams.cs`,
which defines the record `SkinParamEntry` and the internal static class `SkinParams`, and `SkinParams.Colors.cs`, which
adds PlantUML's colors to the class.

#### Purpose

`SkinParams` applies the `skinparam` statements of a diagram to the default look: the settings that diagrams commonly
use to set their colors, fonts, and line widths. Everything outside the supported subset is reported rather than
silently ignored, so an author learns which settings were not applied. The unit's behavior is specified by the
`CanvasNetPlantUml-Styling-SkinParams-*` requirements in `docs/reqstream/canvas-net-plant-uml/styling/skin-params.yaml`.

#### Data Model

- `SkinParamEntry(Name, Value, Location)` - the full parameter name (a block's name and the inner name joined), the
  value text, and the location of the statement.
- The element prefixes and the elements each one styles: the eight participant kinds; `note`; `sequenceGroupHeader` (the
  header tab), `sequenceGroup` (the frame and the tab); `sequenceBox`; `sequenceDivider`; `sequenceDelay`;
  `sequenceLifeLine` and `lifeLine`; `arrow` (sequence and activity arrows); `activityDiamond`; `activity`;
  `swimlaneTitle` and `swimlane`; `partition`; `title`; `header`; `footer`; `legend`; and `caption`. The graph prefixes,
  read only in the diagrams PlantUML lays out with Graphviz (class, object, and state diagrams): `class`,
  `classStereotype` and `objectStereotype` (the stereotypes), `classAttribute` (the members), `object` and `map`,
  `package`, `state` (the states and composite states), and `stateAttribute` (their descriptions); there `arrow` also
  styles the links (`SvekArrow`). The description prefixes, read only in description diagrams: `component`, `interface`,
  `node`, `rectangle`, `folder`, `frame`, `cloud`, `card`, and `usecase` (with `database` and `actor`, shared with
  sequence diagrams, styling the database and the actors of the description theme).
- The property suffixes: `BackgroundColor`, `BorderColor` (also `Color` and `LineColor`), `FontColor`, `FontSize`,
  `FontName`, `FontStyle`, and `BorderThickness` (also `Thickness`).
- The accepted ranges: font sizes from 1 to 500, line widths from 0 to 100, widths from 1 to 100,000.

#### Key Methods

- `TryRead(statements, ref index, entries)` - a statement that starts with the keyword `skinparam` is one entry, or,
  when it ends with `{`, a block whose inner statements up to `}` are entries named with the block's name first.
- `TrySkipStyle(statements, ref index, reports)` - a statement that starts with `<style` is reported once as
  `plantuml-style-unsupported` and skipped up to the statement that holds `</style>`.
- `Apply(theme, entries, reports, graph, description)` - for each entry in order: a stereotyped name is ignored; in a
  description diagram (`description` true) the description prefixes come first; in a graph diagram
  (`graph` true) `classAttributeIconSize` (0 to 100) and `circledCharacterRadius` (1 to 100) set the class settings,
  `stateStartColor` and `stateEndColor` set the fill and the line of the start and end circles, and the graph prefixes
  join the element prefixes; a document or sequence
  setting (`backgroundColor`, `defaultFontName`, `defaultFontSize` for the participant kinds, the legend and the
  caption, `defaultFontColor`, `roundCorner` for the participant kinds, `shadowing`, `sequenceArrowThickness`,
  `sequenceMessageAlign`, `responseMessageBelowArrow`, `maxMessageSize`, `footbox`) is applied; otherwise the longest
  element prefix whose rest is a property is applied to its elements (the group's background is its header's, its font
  its condition's, and the life line's background is the activation box's); an entry with no effect becomes a
  `plantuml-skinparam-ignored` warning that says why.
- `TryParseColor(text, out color)` - an HTML color name with or without a hash (through the core `CssColor` table),
  `#RRGGBB`, `#RRGGBBAA`, `#RGB`, one hexadecimal digit for a gray (`#8` is `#888888`), `transparent` (fully
  transparent), and for a gradient its first color.

#### Error Handling

No exception is thrown for diagram content: every value that cannot be read and every unsupported name becomes a
pending warning located at its statement. Null arguments throw `ArgumentNullException`.

#### Dependencies

The `SkinTheme` unit, the Syntax subsystem's `SourceStatement`, the core `Canvas` subsystem's `Rgba32` and `CssColor`,
and the root namespace's codes, locations, and pending reports.

#### Callers

The diagram parsers read and apply the parameters of their diagrams and skip style blocks; the `TextLayout` unit reads
the colors of the `<color>` tag.
