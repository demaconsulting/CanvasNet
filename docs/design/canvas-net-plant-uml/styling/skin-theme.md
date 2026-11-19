### SkinTheme

![Styling Structure](PlantUmlStylingView.svg)

The `SkinTheme` unit is the first software unit in the `Styling` subsystem. Its source file is
`Styling/SkinTheme.cs`, which defines the internal enumeration `SkinElement`, the record `ElementStyle`, and the record
`SkinTheme`.

#### Purpose

`SkinTheme` holds what a diagram looks like: for every element, the font, colors, line width, corners, and padding,
and for the document, the background and the sequence settings. Its default values are those of PlantUML's default
skin (`plantuml.skin`, the modern look), so that a diagram without skin parameters looks as PlantUML draws it. The
unit's behavior is specified by the `CanvasNetPlantUml-Styling-SkinTheme-*` requirements in
`docs/reqstream/canvas-net-plant-uml/styling/skin-theme.yaml`.

#### Data Model

- `SkinElement` - `Document`, the eight participant kinds, `LifeLine`, `ActivationBox`, `Arrow`, `Note`, `Group`,
  `GroupHeader`, `Separator`, `Delay`, `Box`, `Title`, `Header`, `Footer`, `Legend`, `Caption`, and the activity
  elements `Activity`, `ActivityDiamond`, `ActivityArrow`, `ActivityCircle`, `ActivityBar`, `Swimlane`, and `Partition`,
  and the elements of the diagrams that PlantUML lays out with Graphviz, drawn by the Svek subsystem: `SvekArrow` (a
  link and its labels), `Package` (a package cluster), and `Container` (the other container clusters).
- `ElementStyle(FontFamily, FontSize, FontColor, Bold, Fill, Line, LineWidth, RoundCorner, Padding)` with `Italic` and
  `Dash` - a null fill or line draws none; `RoundCorner` is PlantUML's diameter (an SVG radius of half of it).
- `SkinTheme` - the styles by element, `Background` (white; null for transparent), `Shadowing` (false),
  `MaxMessageSize` (zero, no wrapping), `MessageAlign` (`left`), `ResponseBelowArrow` (false), `Footbox` (true), and
  `ArrowThickness` (1).
- The default values: the root is `SansSerif` 14 in black with lines `#181818` one pixel wide; the document is white;
  participants and their kin are filled `#E2E2F0`, outlined half a pixel wide, padded 7, and a participant has corners
  of 5; life lines are dashed 5 and 5, half a pixel wide; activation boxes are white; arrows have 13-pixel labels;
  notes are 13-pixel text on `#FEFFDD`, outlined half a pixel wide; groups are black, 1.5 pixels wide, with bold
  11-pixel conditions and a bold 13-pixel header tab on `#EEEEEE`; dividers are bold 13-pixel text on `#EEEEEE` with a
  black line two pixels wide; delays are 11-pixel text with dotted life lines; boxes are `#DDDDDD`; the header and
  footer are 10-pixel `#888888` text; the title is bold; the legend is on `#DDDDDD` with a black outline and corners of
  15; activities are 12-pixel text on `#F1F1F1` with corners of 25 and a padding of 10; diamonds and activity arrows
  have 11-pixel text; circles are `#222222`; bars are `#555555`; swimlane titles are 18 pixels with black lines 1.5
  wide; graph links have 13-pixel labels; package clusters are black, 1.5 pixels wide, and container clusters
  `#181818`, one pixel wide, both unfilled with corners of 5 and bold titles.

#### Key Methods

- `Default` - built once from the root style and the per-element differences.
- `this[element]` - the style of an element.
- `With(element, change)` - a new theme whose style of one element is `change` applied to the current style; the
  theme it is called on is unchanged.

#### Error Handling

N/A - the unit holds values; `With` throws `ArgumentNullException` for a null change, and every element always has a
style.

#### Dependencies

The core `Canvas` subsystem (`Rgba32`).

#### Callers

The `SkinParams` unit changes a theme; the diagram layouts and renderers read the styles; the root namespace's
documents read the background through the scene.
