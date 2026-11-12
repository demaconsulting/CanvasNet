### PacketRenderer

![MermaidPacket Structure](MermaidPacketView.svg)

The `PacketRenderer` unit is the second software unit in the `Packet` subsystem. It comprises one source file in the
`DemaConsulting.CanvasNet.Mermaid.Packet` namespace: `PacketRenderer.cs` (the drawing of Mermaid's packet renderer: the
rows of block rectangles with their labels and bit numbers, the title row, and the view box, with the texts as the
browser measures and shows them). `PacketRenderer` is an `internal sealed` class. The file carries the provenance header
comment for the parts of Mermaid it ports.

#### Purpose

`PacketRenderer` draws one render of a `PacketDiagram` into a scene as the `draw` and `drawWord` of Mermaid's packet
`renderer.ts` draw the diagram, with the paint of the packet style sheet of the render's theme. It exists so that the
geometry, the texts, the colors, and the size of a packet diagram are Mermaid's for the same text, configuration, and
theme.

The unit's behavior is specified by the `CanvasNetMermaid-Packet-PacketRenderer-*` requirements in
`docs/reqstream/canvas-net-mermaid/packet/packet-renderer.yaml`.

#### Data Model

**The constant of Mermaid's renderer**: the gap of 2 pixels between the top of a block and the baseline of its bit
numbers (`bitNumberY = wordY - 2`).

**A renderer instance** draws one render on one thread and holds the diagram, the `DiagramRenderContext` of the render,
the `PacketConfig` section of its configuration (`rowHeight`, `bitWidth`, `bitsPerRow`, `showBits`, `paddingX`, and the
vertical padding `EffectivePaddingY`, grown by 10 when the bit numbers are shown, as Mermaid's `getConfig` grows it),
the `PacketStyles` built from its configuration (reporting the problems of `themeCSS` to its sink), a
`LayoutWorkMeter` of `MaxLayoutWork` located at the diagram keyword and bound to the cancellation token of the render,
and a `LabelBuilder` over the fonts of the render that charges that meter.

**The scene** has one group with the role `packet`, located at the diagram keyword, holding, in order, a group for each
row (`packet-row`, its index as the id, located at its first part) with, for each part, its rectangle (`packet-block`)
and its texts (`packet-label`, `packet-bit-start`, and `packet-bit-end`), each with the block label as the id and the
location of the block statement; and the title text (`title`). The view box starts at (0, 0).

#### Key Methods

- **`Draw()`** follows Mermaid's `draw`:
  - It splits the blocks into rows of the configured `bitsPerRow` with `PacketDiagram.Rows`, with the limits and the
    meter of the render.
  - It computes the size of the picture as Mermaid does: the total row height is the row height plus the vertical
    padding; the height is the total row height times the rows plus one, less the row height when there is no title;
    the width is the bit width times `bitsPerRow` plus 2.
  - It draws each row with `DrawWord`, then the title to draw (`DrawnTitle`, the diagram's title once a title statement
    with a value sets it, else the frontmatter title) centered at half the width and half a total row height above the
    bottom, with a `middle` baseline, in the font and color of `.packetTitle`; an empty title draws nothing.
- **`DrawWord`** follows Mermaid's `drawWord` for the row of index `r`: the row's top is `r` times the total row height
  plus the vertical padding; for each part, the rectangle's left is the part's start modulo `bitsPerRow` (with the sign
  of the start, as JavaScript's `%` and C#'s keep it) times the bit width plus 1, its width is the part's bits times
  the bit width less `paddingX`, and its height the row height, painted with `.packetBlock`; a rectangle whose width or
  height is not more than zero, or whose numbers are not finite, is left out, as SVG draws no such rectangle. The label
  is centered on the rectangle with a `middle` baseline, in the font and color of `.packetLabel`. With `showBits`, the
  bit numbers are drawn on the baseline 2 pixels above the rectangle with an `auto` baseline: for a part of one bit
  (JavaScript's `===` of its start and end) its start centered over it, else its start at its left edge (anchored at the
  start) and its end at its right edge (anchored at the end), in the fonts and colors of `.packetByte` with
  `.packetByte.start` and `.packetByte.end`; the numbers are written as JavaScript writes them.
- **`TextLine`** makes one SVG text: it charges the meter `1` plus the length of the shown text, shows the text as
  `DisplayText` gives it, draws nothing for an empty text or a position that is not a number, places the baseline at
  the `y` for an `auto` baseline or half the font's x-height below it, rounded to 1/64 pixel as the browser lays it
  out, for a `middle` baseline, and gives the text the box of the browser's `getBBox` (the advance grown to the glyph
  outlines from `LabelBuilder.SvgLineExtent`, between the rounded ascent and descent of `LabelBuilder.VerticalMetrics`)
  for its anchor.
- **`DisplayText`** gives the text the browser shows: each of Mermaid's entity placeholders decoded into its character,
  everything else, a reference or a tag the author wrote included, shown as written, and the white space collapsed as
  in all SVG text.

#### Error Handling

- `PacketDiagram.Rows` throws a `MermaidRenderException` located at a block statement with code
  `mermaid-render-failed` where Mermaid's split fails, and with code `mermaid-limit-exceeded` when the parts exceed
  `MaxNodes`; both propagate.
- The layout work meter of the render throws a `MermaidRenderException` with code `mermaid-limit-exceeded`, located at
  the diagram keyword, when the work of the split and of the measured texts exceeds `MaxLayoutWork`, and an
  `OperationCanceledException` when the render is canceled; both propagate.
- `PacketStyles.Create` throws `MermaidUnsupportedFeatureException` through the sink at the first unsupported `themeCSS`
  rule in strict mode; it propagates.
- No other error can occur: a rectangle without area and a text at a position that is not a number are left out, as
  the browser draws nothing for them, and a configuration value of the wrong type was already left out by the
  configuration with a warning.

#### Dependencies

- **PacketParser** - `PacketDiagram` (the row split `Rows`, `DrawnTitle`, and `KeywordLocation`) and `PacketBlock` (the
  parts of the rows: start, end, label, and location).
- **Configuration subsystem** - `MermaidConfig` (the `Packet` section `PacketConfig`) and `PacketStyles` with
  `ShapePaint` and `TextPaint` (the packet style sheet).
- **Rendering subsystem** - `LabelBuilder` (the browser's text extents `SvgLineExtent`, the vertical metrics
  `VerticalMetrics`, the x-height `XHeight`, `CollapseSvgWhiteSpace`, and `DecodeEntities`).
- **Syntax subsystem** - `HtmlSanitizer.DecodeReferences` (the character references of a placeholder) and
  `YamlSubsetParser.FormatJsNumber` (JavaScript's `String(number)`).
- **Dagre subsystem** - `LayoutWorkMeter`, the work budget and cancellation of a render.
- **Scenegraph subsystem** - `Scene`, `SceneItem`, `SceneGroup`, `ScenePath`, `SceneSegment`, `SceneText`,
  `SceneTextAnchor`, `SceneTextRun`, `SceneFont`, `SceneRect`, and `SceneStroke`, the drawing.
- **System** - `DiagramRenderContext`, `MermaidLimits`, `MermaidLocation`, and `MermaidRenderException`.

#### Callers

- **PacketParser** - `PacketDiagram.Layout` creates a renderer for each render and returns the scene of `Draw`.
- The unit tests call `DisplayText` directly, and the unit and subsystem tests lay out whole texts.

#### Known Differences From Mermaid

- **No missing-glyph reports.** The texts of a packet diagram are drawn with the fonts of the render but give no
  `mermaid-text-missing-glyph` report, as the texts of a pie chart give none.
- **Text metrics.** The widths and boxes of the texts are those of the `LabelBuilder`, which emulates the browser's SVG
  text measurement with the fonts of the render; the reference layout tests hold them to Mermaid's goldens (a box can
  differ by Chrome's 1/64-pixel baseline snapping, within the tolerance of the text boxes).
- **Accessibility.** The picture carries no title or description element; the accessibility texts are metadata of the
  diagram.
