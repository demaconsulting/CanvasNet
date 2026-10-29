### TitleBuilder

![Guides Structure](VegaLiteGuidesView.svg)

The `TitleBuilder` unit is the second unit in the `Guides` subsystem. It is one source file, `TitleBuilder.cs`, in
the `DemaConsulting.CanvasNet.VegaLite.Guides` namespace, and defines two `internal` types: `TitleBuilder` (sealed class)
and `TitleBlock` (sealed class). The file carries the provenance header comment for the Vega title rules it ports.

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
(`start`, `middle`, or `end`), `Frame` (`group`, the data rectangle, or `bounds`, everything the view contains), `Offset`
(the distance between the title and the content), the `Children` (the title text item with role `title-text` and an
optional subtitle text item with role `title-subtitle`, in drawing order, in the coordinates of the title group), and the
`Extent` (the bounding box of the text in the group's coordinates, which is the room the title needs).

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

#### Error Handling

A missing, null, or empty title is not an error: `Build` returns null and no title is drawn. Invalid orient, anchor, or
frame values from the configuration fall back to defaults (the specification's own values were already validated by
`SpecParser`). A null configuration or measurer is `ArgumentNullException`, which is not converted. An expression-valued
title (`{"expr": ...}`) is not interpreted: the feature catalog reports `vl-expr-ref` and no title text is built for it.
The unit performs no I/O.

#### Dependencies

- **`TextMeasurer`** - font resolution and measurement, in the `TextMeasurer` unit
- **`SpecNode`** and **`ResolvedConfig`** - the tree and the configuration of the `SpecModel` subsystem
- **`SceneGroup`**, **`SceneText`**, **`SceneItem`**, and **`SceneBounds`** - the scene types of the `SceneModel` unit
- **.NET base class library** - `System.Collections.Generic`

#### Callers

`VegaLiteDocument` builds the title once per render and passes the `TitleBlock` to `ViewLayout`, which measures with its
extent and calls `Place`. The tests in `test/DemaConsulting.CanvasNet.VegaLite.Tests/Guides/TitleBuilderTests.cs` call
`Build` and `Place` directly through `InternalsVisibleTo`.
