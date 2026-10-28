### MarkBuilder

![Marks Structure](VegaLiteMarksView.svg)

The `MarkBuilder` unit is the only software unit in the `Marks` subsystem. It is one source file, `MarkBuilder.cs`, in the
`DemaConsulting.CanvasNet.VegaLite.Marks` namespace, and defines one `internal` type, the sealed class `MarkBuilder` (with
private helper types for one channel, the sizing of a position, the paint of an item, and one build run). The file carries
the provenance header comment for the Vega-Lite mark rules it ports.

#### Purpose

`MarkBuilder` builds the scene items of the mark of a single view: `bar`, `rect`, `point`, `circle`, `square`, `rule`, and
`tick`. It exists so that the Vega-Lite mark geometry, the channel-to-property resolution, the paint defaults, and the
invalid-data rules are defined in one place, separate from the scales that provide the mappings and from the painter that
draws the result.

The unit's behavior is specified by the `CanvasNetVegaLite-Marks-MarkBuilder-*` requirements in
`docs/reqstream/canvas-net-vega-lite/marks/mark-builder.yaml`.

#### Data Model

The builder is stateless between calls; an instance holds the `ResolvedConfig`, the shared `SceneBudget`, and the source
name. A private run object holds the state of one `Build` call (the mark definition, the view, the table, the scales, the
data rectangle, and the token), and private helpers describe a channel (its definition, scale, and how to read a row's
value), a position sizing (fixed, band, or encoded), and a paint (fill, stroke, stroke width, and opacity).

**Geometry** (Vega-Lite 6):

- A discrete position of a `bar`, `rect`, or `tick` fills the band (its bandwidth, at least `minBandSize`); a point scale
  gives `step - 2`. An explicit `size`, `width`, or `height` (a number or `{band}`) is centered in the band.
- The thickness axis of a bar or tick on a continuous scale is centered on the value and `continuousBandSize` (5) thick
  unless a size says otherwise.
- The measure axis runs from the value to zero (to the first domain value when the domain does not contain zero); an
  absent channel spans the data rectangle; a bar or tick without a position on its thickness axis is centered and as
  thick as one minus the band padding (0.1 for a bar, 0.25 for a tick) of the rectangle.
- A `tick` is `thickness` (1 in the configuration) across and as long as its band; its orientation comes from
  `ScaleResolver.InferOrient` (vertical by default).
- `point`, `circle`, and `square` sit at the center of their band or value, and at the middle of the rectangle for
  an absent channel; `size` is the area (30 by style), the `size` channel maps through the size scale, and `shape` and
  `angle` are honored.
- A `rule` with only `x` is a vertical line across the height, with only `y` a horizontal line across the width (drawn from
  the far edge back to zero, as Vega-Lite does), and with both a stem from the value to zero along the measure axis; its
  stroke is black with a width from `strokeWidth`, `size`, or the size scale; a rule with neither channel draws nothing.
- `cornerRadius` (a single radius) rounds bars and rectangles.

**Paint** (Vega-Lite `init.ts` and `color()`): `filled` is on for every mark except `point` and `rule`; the mark color
(`#4c78a8`, black for a rule) is the fill of a filled mark and the stroke of an unfilled one, and unfilled bars and symbols
get a transparent fill. A `color`, `fill`, or `stroke` field is mapped by its ordinal scale (`tableau10` by default or
`scale.range`), a `value` is used as given, and the `fill` channel wins over `color`. An `opacity` field is mapped through
the opacity scale. `point`, `circle`, `square`, and `tick` marks get an opacity of 0.7 when nothing names an opacity. The
`fillOpacity`, `strokeOpacity`, `strokeDash`, `strokeDashOffset`, `strokeCap`, and `strokeJoin` properties of the mark
definition are honored. Explicit mark properties win over style, `config.<mark>`, `config.mark`, and the Vega styles.

**Ignored without a report**: `tooltip`, `href`, expression-valued mark properties, individual corner radii, and
`cornerRadiusEnd`. `x2`, `y2`, and the offset channels are reported by the feature catalog and ignored here.

#### Key Methods

- `IsSupported(string? markType)` (static) - true for the seven drawn mark types.
- `Build(view, table, scales, width, height, token)` - reads the mark type (a string or the `type` of a mark definition);
  returns an empty `marks` group for an unsupported type; otherwise builds the rows of the table in batches of
  1,024 (charging the item budget and polling the token per batch), creating bar and rect items, symbol items, rule items,
  and tick items with resolved geometry and paint, and returns the group, clipped to the data rectangle only when the
  mark definition says `"clip": true`.
- Row filtering - a row whose value for a continuous channel (`x`, `y`, `size`, or `opacity` on a linear scale) is null,
  missing, NaN, or not a number is not drawn; a null in a discrete field is the category `null` and is drawn; a value outside
  an explicit discrete domain is not drawn.

**Preconditions and postconditions**: the scales were resolved from the same view and data; on return the group holds one
item per visible mark in row order (deterministic), and the budget has been charged for them.

#### Error Handling

Exceeding `MaxSceneItems` is a `VegaLiteRenderException` located at the mark, thrown before the items of the batch are
created (the `SceneBudget` carries the failure). A canceled token is an `OperationCanceledException`. A null configuration,
view, table, or scales argument is `ArgumentNullException` (not converted). Invalid data is not an error: affected rows
are dropped as described above. A mark type that is not drawn yields an empty group, because the feature catalog has already
reported it. The unit performs no I/O.

#### Dependencies

- **`ResolvedScales`**, **`ResolvedScale`**, and **`ScaleModel`** - the scales of the `Scales` subsystem
- **`DataTable`** and **`DataValue`** - the data of the `Data` subsystem
- **`ResolvedConfig`** and **`SpecNode`** - the `SpecModel` subsystem
- **`SceneGroup`**, **`SceneRect`**, **`SceneRule`**, **`SceneSymbol`**, **`SceneClip`**, and **`SceneBudget`** - the
  `Scenegraph` subsystem
- **`VegaLiteLimits`**, **`VegaLiteLocation`**, and **`VegaLiteRenderException`** - the public types of the
  `VegaLiteDocument` unit
- **.NET base class library** - `System.Globalization`, `System.Collections.Generic`

#### Callers

`VegaLiteDocument` calls `IsSupported` to decide if a view is drawn and `Build` once for the final data rectangle. The
tests in `test/DemaConsulting.CanvasNet.VegaLite.Tests/Marks/MarkBuilderTests.cs` call the builder directly through
`InternalsVisibleTo`, and the chart integration tests exercise it through the public document.
