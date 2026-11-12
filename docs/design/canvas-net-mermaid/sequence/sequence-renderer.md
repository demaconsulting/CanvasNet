### SequenceRenderer

![Sequence Structure](MermaidSequenceView.svg)

The `SequenceRenderer` unit is the fourth software unit in the `Sequence` subsystem. It comprises five source files in
the `DemaConsulting.CanvasNet.Mermaid.Sequence` namespace: `SequenceRenderer.cs` (the paint order, the lifelines, the
title, and the geometry helpers, with the sealed class `SceneSlot`), `SequenceRenderer.Actors.cs` (the eight
participant types), `SequenceRenderer.Messages.cs` (messages, markers, sequence numbers, central connections, notes,
and activations), `SequenceRenderer.Blocks.cs` (block frames, `rect` backgrounds, boxes, and menus), and
`SequenceRenderer.Text.cs` (the text placements, with the sealed record `SequenceTextData`). `SequenceRenderer` is an
`internal sealed partial` class, so the five files are one type. Every file carries the provenance header comment for
the parts of Mermaid it ports.

#### Purpose

`SequenceRenderer` is Mermaid's sequence drawing functions (`svgDraw.js`, the message drawing of
`sequenceRenderer.ts`, and `svgDrawCommon.ts`) for one render: it turns what the `SequenceLayout` places into scene
items with Mermaid's geometry, roles, and ids, painted from the theme's sequence style sheet over the attributes that
Mermaid writes, in Mermaid's paint order, and returns the heights that Mermaid measures with `getBBox` so that the
layout can continue from them. It exists so that the pixels of a sequence diagram are Mermaid's for the same text,
configuration, and theme.

The unit's behavior is specified by the `CanvasNetMermaid-Sequence-SequenceRenderer-*` requirements in
`docs/reqstream/canvas-net-mermaid/sequence/sequence-renderer.yaml`.

#### Data Model

**A renderer instance** draws one render on one thread and holds the `SequenceConfig`, the `SequenceStyles`, the
`LabelBuilder` (text metrics), and the `MermaidConfig` (theme variables) of the render, the number of the last
participant drawn at the top (Mermaid's `actorCnt`), the lifelines by participant (drawn to y 2000 until their ends
are fixed), and two lists of **`SceneSlot`** places: the appended places in order and the lowered places in the order
they were lowered. A place is an SVG group that Mermaid appends or lowers; it is made when Mermaid makes the group and
filled when the items are drawn, which may be later (an activation is drawn into the group made when it started).

**`SequenceTextData`** is Mermaid's text object of `getTextObj` with the fields the sequence renderer sets: the text
with `<br>` tags between its lines, the position, the width of the area it is aligned in (or none), the horizontal
and vertical alignment, the text margin, the `dy="1em"` flag, the font of the inline style, the weight of a style rule
that replaces the inline one, and the color.

**Roles and ids.** The items carry roles and ids for the tests and the reference comparison: `actor-top` and
`actor-bottom` groups (the participant's name as id) with `actor-rect`, `actor-line`, `actor-head`, `actor-cylinder`,
`actor-marker`, and `actor-text` items; `lifeline` paths; `message` groups (Mermaid's `i` and the message index as id)
with the `message-line`, its `marker-start` and `marker-end` (the marker name as id), and `message-text`;
`connection-circles` and `sequence-number` groups; `note` groups with `note-rect` and `note-text`; `activation`
rectangles (the activation class as id); `frame` groups of blocks (the id of the message that ends the block) with
`frame-line`, `frame-label-box`, `frame-label`, `frame-title`, and `frame-section-title` items; `rect` backgrounds;
`box` groups with a `box-rect` and a `box-title`; `menu` groups (Mermaid's `actor{n}_popup` id) with a `menu-panel`
and `menu-text` lines; and the `title`.

#### Key Methods

- **`DrawActor(SequenceActorLayout, bool isFooter)`** draws a participant at the top or its mirror at the bottom, by
  type, and returns its height: `participant` (a rounded box with the label centered), `collections` (a box with a
  second box 6 pixels down and left), `queue` (a horizontal cylinder with its right end drawn again), `database` (an
  upright cylinder a third of the box's width with the label below it), `actor` (a stick figure 65 pixels high with
  the label below it), `boundary` (a circle with a T bar on its left), `control` (a circle with the
  `filled-head-control` arrowhead turned by 172.5 degrees on its top), and `entity` (a circle on a line); the
  participant's lifeline is lowered with it from below its drawing, and the heights of `boundary`, `entity`,
  `database`, and `control` grow with `labelBoxHeight` as Mermaid's do. A type that Mermaid does not define draws
  nothing and gives a height that is not a number, as in Mermaid. A `properties` class replaces the `actor` class of a
  box, with the note fill `#EDF2AE` and stroke `#666` as attributes, so the box takes the paint of a style rule of
  that class, or else its attributes.
- **`DrawMessage(SequenceMessageState, double, ...)`** draws a message: its text above the line, the line of a
  message between participants or the loop of a self message (a cubic, or a rectangle path with `rightAngles`),
  solid or dashed by the line type, with its markers (`MessageLine`, `PlaceMarker`: Mermaid's `arrowhead`,
  `crosshead`, `filled-head`, and the solid and stick half heads, placed with their reference point at the end of the
  line, turned along it, scaled by the stroke width unless in user units, and filled markers clipped to their
  viewport), the circles of a central connection on the lifelines it names (`CentralConnection`), and the sequence
  number (`SequenceNumber`: the `sequencenumber` marker of a zero-length line, scaled by the 2-pixel width of the
  `line` rule, with the number in white, smaller for a longer number).
- **`DrawNote`** draws a note's text with the note margin and the box around the measured text, and returns its
  height; **`DrawActivation`** draws an activation into the group made when it started, its class cycling through
  three with the number of activations still open on the participant.
- **`DrawLoop`** draws the frame of a block: the four sides dashed, a dashed separator above each further section,
  the label box with its cut corner and the block's keyword, the title, and the section titles. **`DrawBackgroundRect`**
  draws the background of a `rect` block behind everything drawn so far, in its color and without a stroke or shadow;
  **`DrawBox`** draws a box behind everything drawn so far, in its color with the theme's node border and drop shadow,
  and its title centered at its top. A fill attribute is resolved as a browser resolves it (`FillAttribute`): a CSS
  color, a color keyword or system color, or else the root fill.
- **`DrawPopup`** draws a participant's menu of links: an empty group unless `forceMenus` is set, else a panel below
  the participant's box in its fill with the menu shadow and one line per link; it returns the size the menu needs
  either way.
- **Text** (`DrawText`, `ActorText`, `ByTspan`, `OldText`, `TextLine`): `DrawText` is Mermaid's `drawText`, one text per
  line aligned in its area with the margin; participant labels use `byTspan` (one text per line, centered on the
  central baseline, the lines one font size apart), or `byText` for the `old` text placement (one text as written, in
  the root font, filled and stroked by the `.actor` rule); the `fo` placement is drawn as `byTspan`. `DisplayText`
  turns Mermaid's entity placeholders into their characters and collapses white space as SVG text does, and every line
  gets the box that the browser's `getBBox` gives it.
- **Paint order** (`Append`, `Lower`, `AnchorElement`, `Items`): the scene items are the lowered places from the last
  lowered to the first, then the appended places in order, so the mirrored participants, then each participant's
  lifeline and box lowered to the back, then the notes and messages appear as in Mermaid's SVG.
  **`FixLifeLineHeights`** ends the lifelines at the mirrored participant, or, without mirrored participants, half a
  participant below where a destroyed participant ends (else at y 2000); **`DrawTitle`** draws the title in the root
  font.
- **Geometry helpers** (`RectSegments`, `CircleSegments`, `LineSegments`) give the segments of an SVG rectangle with
  corners clamped as SVG clamps them, a circle as four quarter arcs, and a line.

#### Error Handling

- Links on a participant type without a box (`actor`, `boundary`, `control`, `entity`), where Mermaid's `drawPopup`
  crashes, are a `MermaidRenderException` with code `mermaid-render-failed` located at the participant's declaration.
- A `properties` icon that is not text on a `participant`, `collections`, or `queue`, where Mermaid's `trim` throws, is
  a `MermaidRenderException` with code `mermaid-render-failed` located at the participant.
- The render errors of the text utilities (a font size of zero, a line of only spaces between breaks, and the layout
  work limit) propagate unchanged.

#### Dependencies

- **SequenceLayout** - the layout state types (`SequenceActorLayout`, `SequenceBoxLayout`, `SequenceLoopModel`,
  `SequenceActivation`, `SequenceMessageState`, and `SequenceRectData`) that it draws.
- **SequenceModel** - the line types, the placements, the participant types, and the messages.
- **Configuration subsystem** - `SequenceConfig`, `SequenceStyles` (shape, line, text, and shadow paint, and the
  marker colors), `DiagramStyles.Root`, `MermaidConfig` (theme variables), and `MermaidFontConfig`.
- **Rendering subsystem** - `LabelBuilder` (`CalculateTextDimensions`, `SvgLineExtent`, `VerticalMetrics`, and
  `XHeight`).
- **Scenegraph subsystem** - `SceneItem`, `SceneGroup`, `ScenePath` (with its drop shadow), `SceneText` (with its
  stroke), `SceneTextRun`, `SceneFont`, `SceneStroke`, `SceneShadow`, `SceneSegment`, and `SceneRect`.
- **Core `CanvasNet`** - `Rgba32` and `CssColor`, through the public API.
- **System** - `MermaidLocation`, `MermaidRenderException`, and `MermaidDiagnosticCodes`.

#### Callers

- **SequenceLayout** - creates the renderer of a render, draws every element through it, and takes the items in paint
  order.
- **SequenceModel** - uses `IsTruthy` for the icon report.
- The unit tests lay texts out through the document pipeline and call `DisplayText` and the geometry helpers directly.

#### Known Differences From Mermaid

- **Text boxes.** The box of a text line is the advance grown to the rasterizer's boxes of its first and last glyphs,
  from the rounded ascent to the rounded descent; Chrome's snapping of baselines to 1/64 pixel is not modeled.
- **Markers.** The open stroked markers (the crosshead and the stick heads) are not clipped to their viewport.
- **Colors.** Color keywords and system colors take the values of headless Chrome's light scheme; an invalid color,
  `inherit`, `unset`, and `revert` take the root fill, and `currentcolor` and `initial` are black.
- **Failures.** Links on a type without a box and an icon that is not text fail with a located
  `mermaid-render-failed` where Mermaid crashes.
- **Text placements.** The `fo` placement (HTML labels) is drawn as the default `byTspan` placement.
- **Menus** are not interactive; a hidden menu is an empty group.
- **Empty lines.** Mermaid draws an empty line as a zero-width space; the renderer keeps it as a line without runs,
  with the same box.
