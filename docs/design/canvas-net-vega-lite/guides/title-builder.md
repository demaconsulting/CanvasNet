### TitleBuilder

![Guides Structure](VegaLiteGuidesView.svg)

The `TitleBuilder` unit is the second unit in the `Guides` subsystem. It is three source files in the
`DemaConsulting.CanvasNet.VegaLite.Guides` namespace: `TitleBuilder.cs` (the title of a chart), `TitleBuilder.Header.cs`
(the same partial class, for the labels and titles of the headers of a facet) and `HeaderSettings.cs` (the header
properties of a facet channel). They define the `internal` types `TitleBuilder` (sealed partial class), `TitleBlock`
(sealed class), `HeaderSettings` (sealed class), `HeaderLabelFormat` (record) and `HeaderPart` (enum). The files carry
the provenance header comment for the Vega and Vega-Lite rules they port.

#### Purpose

`TitleBuilder` builds the title and subtitle of a chart from the `title` property of the specification and the
configuration: the text items, their alignment, rotation, font, and limit, and their extent. `TitleBlock.Place` then
positions the title next to the view as Vega's title layout does. It exists so that the title rules (orientation, anchor,
frame, offset, subtitle padding, and the style defaults) are defined once, and so that the title can be built before the
axes are measured and placed after.

The unit's behavior is specified by the `CanvasNetVegaLite-Guides-TitleBuilder-*` requirements in
`docs/reqstream/canvas-net-vega-lite/guides/title-builder.yaml`.

#### Data Model

**`TitleBlock`** (sealed class, immutable) holds the resolved `Orient` (`top`, `bottom`, `left`, or `right`), `Anchor`
(`start`, `middle`, or `end`), `Frame` (`group`, the data rectangle, or `bounds`, everything the view contains),
`Offset` (the distance between the title and the content), the `Children` (the title text item with role `title-text`
and an optional subtitle text item with role `title-subtitle`, in drawing order, in the coordinates of the title group),
and the `Extent` (the bounding box of the text in the group's coordinates, which is the room the title needs).
`PlacesByExtent` (set for the blocks of facet headers) makes `Place` put the block on its side by the edges of its
extent, as Vega does by the bounds of the title, instead of by the size of the extent, which suits a text whose box
starts at the group's origin.

**`HeaderSettings`** (sealed class, immutable) is Vega-Lite's `header` of a facet channel resolved over the header
configuration: `Get(property)` is the first of the header of the field definition, `config.headerRow`/`headerColumn`/
`headerFacet` (by the channel) and `config.header` that defines it (`getHeaderProperty`; a null counts as defined), with
`Text` and `Number` readers. It adds `Hidden` (`header: null`), `ShowLabels` (the `labels` of the header, else of
`config.header`, else on), `IncludeDefaultTitle` (false when the configuration of the channel says `title: null` or
`false`), the `LabelOrient`/`TitleOrient`, the kind of header they select (`LabelHeaderChannel`, `TitleHeaderChannel`,
`HeaderChannelOf` = `getHeaderChannel`), `LabelsInFooter`, `TitleAtEnd` and `TitleBand` (the `LAYOUT_TITLE_BAND` of
`titleAnchor`: column start 0 and end 1, row start 1 and end 0). `CreateLabelFormatter(HeaderLabelFormat)` returns the
function from the value of a cell to its label: `formatType` `number` or a quantitative field with a text `format` is a
d3 number format; `time`/`utc` or a temporal field (type or time unit) is the d3 time `format`, else the specifier of
the time unit, else `config.timeFormat` (through `TimeLabels`); anything else is the text of the value (null is empty).
A custom `formatType` is treated as absent, and the `orient` shortcut is not read (Vega-Lite's compiler does not read it
either). `ExplicitNode(property)` returns the node of a property that the `header` of the field definition itself has (not
the configuration), which is how the compiled `labelExpr` of the header is found; the facet code of `VegaLiteDocument`
wraps the label formatter in the `labelExpr` of the header, else of the channel configuration, else of `config.header`
(see _LabelExpressions Unit Design_; the datum has `value`, the raw value of the cell, and `label`, the formatted text,
only, and an array result is joined with line feeds). `Read(config, header, channel, honoredOrientsOnly)` reads a side
that `IsHonoredOrient` does not honor (a row's
`top` or `bottom`, a column's `left` or `right`, a wrapped facet's title on the `left` or `right`) as not given when the
flag is set, which is how the facet pipeline draws the default side for what the feature catalog reports as
`vl-header-orient`. **`HeaderPart`** says whether a block is a `Label` or the `Title` of the channel.

**`TitleBuilder`** (sealed class, stateless, thread-safe) holds the `TextMeasurer`.

**Input and defaults.** The title is a string, an array of strings (one per line), or an object with `text` and the
properties `subtitle`, `anchor`, `orient`, `offset`, `frame`, `limit`, `dx`, `dy`, `align`, `baseline`, `angle`, `font`,
`fontSize`, `fontStyle`, `fontWeight`, `color`, `lineHeight`, and the `subtitle` counterparts of the font, color, line
height, and padding properties. A property that the title does not give comes from `config.title` and then from the
`group-title` or `group-subtitle` style (13 pixel bold black sans-serif for the title, 12 pixel for the subtitle), so with
no configuration the title is centered above the plot, 4 pixels from it, with subtitle padding of 3. Text in a string is
also split at line feeds. An invalid orient, anchor, or frame from the configuration falls back to `top`, `middle`, or
`bounds`.

**Layout rules.** A title line is as tall as the font size and further lines are a line height (font size plus 2) apart;
the subtitle starts below the title (or beside it for the left and right orients) by the subtitle padding. A left title
is rotated by -90 degrees and a right title by 90 degrees, and its text is aligned by the anchor (`start` left, `middle`
center, `end` right). The frame is `group` for single and layered views whose anchor is `middle` or not set (as Vega-Lite
assigns it) and `bounds` otherwise; the title of a composition anchors at `start`.

#### Key Methods

- `Build(SpecNode? title, ResolvedConfig config, bool unitOrLayer, SceneBudget? budget, string? sourceName)` - returns
  null for no title, a null title, or a title without text; otherwise reads the lines, resolves the layout properties
  (the title's own, then the configuration, then the Vega defaults), creates the title text item and, when there is a
  subtitle, the subtitle item below or beside it, charges the scene budget (when given) for the title group and the
  title text before creating the text and for the subtitle before creating it (a `VegaLiteRenderException` located at
  the title when the limit would be passed), measures both with the `TextMeasurer`, and returns the `TitleBlock` with
  the union of their bounds as its extent.
- `TitleBlock.Place(dataWidth, dataHeight, viewBounds)` - places the group: with the `group` frame the anchor refers to
  the data rectangle (for a left title the bottom edge to the top edge, so rotated text reads upwards from its start), with
  the `bounds` frame to the bounding box of the view's content; the title then sits `Offset` pixels beyond the content on
  its side. Returns the positioned `SceneGroup` (role `title`) and its bounds in the view's coordinates.

**Preconditions and postconditions**: the configuration is non-null; the result is immutable.

**Header blocks** (`TitleBuilder.Header.cs`):

- `BuildHeaderBlock(lines, HeaderSettings, HeaderPart, budget, location)` - builds the label of a cell (`guide-label`
  style, 10 pixel, `frame: group`, `labelPadding` offset) or the title of the channel (`guide-title`, 11 pixel bold,
  frame `bounds`, `titlePadding` offset) as a `TitleBlock` with `PlacesByExtent`. The side is the header's
  `labelOrient`/`titleOrient`, else `left` for the `row` channel and `top` for the others (the rotation then follows the
  side: -90 for left, 90 for right); the anchor, angle, alignment, baseline, limit, font, color and line height are the
  `label*`/`title*` properties of the header, then `config.title` (a Vega title applies it to these titles too), then
  the style. When the header gives an angle the alignment and baseline are those that fit it (Vega-Lite's
  `defaultHeaderGuideAlign` and `defaultHeaderGuideBaseline`: for a row, `labelAngle: 0` is right aligned and vertically
  centered; the angle of a title is normalized into [0, 360) for that choice only), and `start`/`end` anchors align
  left/right; an explicit `labelAlign` or `labelBaseline` wins. It returns null for no text, charges the scene budget
  for the title group and its text first, and measures with the `TextMeasurer`.
- `BuildHeader(role, lines, HeaderSettings, HeaderPart, width, height, axes, budget, location)` - builds the group of
  one header, footer or title (a `HeaderGroup` of the `Layout` subsystem) at the origin: the size of the cell along the
  header and none across (`width`, `height`), the axes that Vega-Lite moved into it (built `InHeader`, already in its
  coordinates) and the block placed by `TitleBlock.Place` against that rectangle united with the axes' bounds, so a
  label sits `labelPadding` beyond the axis. The bounds are the union of the axes and the text (the group's rectangle is
  not part of them: it has no fill or stroke). It returns null when there is neither text nor an axis; the group is
  charged as one more item.

#### Error Handling

A missing, null, or empty title is not an error: `Build` returns null and no title is drawn; likewise a header block
without text. Invalid orient, anchor, or frame values from the configuration fall back to defaults (the specification's
own values were already validated by `SpecParser`). A null configuration or measurer is `ArgumentNullException`, which
is not converted. An expression-valued title (`{"expr": ...}`) is not interpreted: the feature catalog reports
`vl-expr-ref` and no title text is built for it. A `format` that is not a d3 specifier is a `VegaLiteSpecException`
located at the header's `format`; a label longer than `VegaLiteLimits.MaxStringLength` is a `VegaLiteRenderException`
located at the facet field. The unit performs no I/O.

#### Dependencies

- **`TextMeasurer`** - font resolution and measurement, in the `TextMeasurer` unit
- **`SpecNode`** and **`ResolvedConfig`** - the tree and the configuration of the `SpecModel` subsystem
- **`SceneGroup`**, **`SceneText`**, **`SceneItem`**, **`SceneBudget`**, and **`SceneBounds`** - the scene types of the
  `SceneModel` unit
- **`TimeLabels`**, **`NumberFormat`**, **`DataValue`** and **`TimeUnitSpec`** - the label formats of the cells of a
  facet
- **`AxisResult`** (the `AxisBuilder` unit) and **`HeaderGroup`** (the `CompositionLayout` unit) - what a header group
  holds and returns
- **.NET base class library** - `System.Collections.Generic`

#### Callers

`VegaLiteDocument` builds the title once per render and passes the `TitleBlock` to `ViewLayout`, which measures with its
extent and calls `Place`. The facet pipeline (`Composer.ComposeFacet` of `VegaLiteDocument.Composition.cs`) reads
`HeaderSettings` of each facet channel, builds the labels and titles with `BuildHeader`/`BuildHeaderBlock` and gives the
groups to `CompositionLayout`. The tests in `test/DemaConsulting.CanvasNet.VegaLite.Tests/Guides/TitleBuilderTests.cs`,
`TitleBuilderHeaderTests.cs` and `HeaderSettingsTests.cs` call the unit directly through `InternalsVisibleTo`.
