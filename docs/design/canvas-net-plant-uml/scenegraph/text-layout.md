### TextLayout

![Scenegraph Structure](PlantUmlScenegraphView.svg)

The `TextLayout` unit is the second software unit in the `Scenegraph` subsystem. It is two source files:
`TextLayout.cs`, which defines the enumeration `TextAlign`, the records `TextLine` and `TextBlock`, and the class
`TextLayout`, and `TextLayout.Creole.cs`, which defines the record `CreolePiece` and the nested creole parser.

#### Purpose

`TextLayout` measures and lays out every text of a diagram so that the elements get the sizes PlantUML gives them.
PlantUML measures text with Java's logical `SansSerif` font, which on Windows is Arial with the line of a composite
font; the bundled Liberation Sans has the metrics of Arial, so measuring it the way Java does reproduces PlantUML's
sizes on every machine. The unit's behavior is specified by the `CanvasNetPlantUml-Scenegraph-TextLayout-*`
requirements in `docs/reqstream/canvas-net-plant-uml/scenegraph/text-layout.yaml`.

#### Data Model

- `TextLine(Runs, Width, Height, Descent)` and `TextBlock(Lines)` with `Width` (the widest line), `Height` (the sum of
  the line heights), `Text`, and `Empty`.
- `CreolePiece(Text, Font, Color, Underline, Strike)` - a styled piece before measurement.
- The logical-font minimums `LogicalMinAscent` (2059/2048) and `LogicalMinDescent` (450/2048).
- The cache of resolved faces by CSS family, weight, and style (one call, one thread).

#### Key Methods

- `CreateFontResolver()` - a core `FontResolver` with the common aliases mapped to the bundled fonts and no system
  fonts.
- `CssFamily(family)` - `Monospaced`, `Courier`, and `Consolas` are `monospace`; `Serif` and `Times` are
  `serif`; every other family is `sans-serif`.
- `LineMetrics(font)` - the ascent and the descent of the face, raised to the logical-font minimums, plus the face's
  line gap, times the size: 17.609 high and 3.076 deep at 14 pixels for Liberation Sans.
- `Width(text, font)` - the sum of the advances of the glyphs of the face, without kerning; a run with a character the
  face lacks is measured with the core's `TextRenderer`, which uses the bundled fallback fonts.
- `Layout(display, font, color, wrapWidth, sink, location)` - `SplitLines` breaks at `\n` (and PlantUML's `\r` and
  `\l`), at real line feeds, and turns `\\` into a backslash and `\t` into a tab; each line is parsed into pieces by the
  creole parser, wrapped greedily at spaces when a width is given, and measured (the line height and descent are the
  largest of its pieces; an empty line has the height of the base font). Markup outside the subset and a missing glyph
  are reported once per text.
- The creole parser - `~` escapes the next character; `**`, `//` (not after a colon, so URLs stay), `""`, `__`, and
  `--` (only when it opens a pair closed later on the line, or closes one) toggle bold, italic, monospace, underline,
  and strike; the tags `<b>`, `<i>`, `<u>`, `<s>`/`<strike>`/`<del>` and their closings set them; `<color:x>`,
  `<size:n>` (1 to 500), and `<font:x>` push a style that `</color>`, `</size>`, and `</font>` pop; `<U+XXXX>` is a
  character; `<img>`, `<$...>`, `<&...>`, and `<back>` are kept as text and remembered as unsupported, and so are lines
  that are tables, lists, headings, or rules. The style of open markup carries over to the next line.
- `TextBlock.Draw(x, top, align, role, location)` - one `SceneText` per non-empty line, its baseline at the line top
  plus the line height minus the descent, aligned in the block's width.

#### Error Handling

Null arguments throw `ArgumentNullException`. No diagram content throws: unsupported markup is drawn as text and
reported as `plantuml-markup-unsupported`, and a missing glyph is drawn with a fallback font and reported as
`plantuml-text-missing-glyph` (both only when a sink is given).

#### Dependencies

The core `Fonts` (`FontResolver`, `ResolvedFont`, `TrueTypeFont`) and `Rendering` (`TextRenderer`) subsystems, the
`SceneModel` unit, the Styling subsystem's `SkinParams.TryParseColor`, and the root namespace's report sink.

#### Callers

The diagram layouts measure and lay out their texts; `ScenePainter` resolves the faces of the runs it draws;
`PlantUmlDocument` creates one text layout per call.
