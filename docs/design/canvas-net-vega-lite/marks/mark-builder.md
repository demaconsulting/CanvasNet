### MarkBuilder

![Marks Structure](VegaLiteMarksView.svg)

The `MarkBuilder` unit is the only software unit in the `Marks` subsystem. It is one source file, `MarkBuilder.cs`, in
the `DemaConsulting.CanvasNet.VegaLite.Marks` namespace, and defines one `internal` type, the sealed class `MarkBuilder`
(with private helper types for one channel, the sizing of a position, the paint of an item, and one build run). The file
carries the provenance header comment for the Vega-Lite mark rules it ports.

#### Purpose

`MarkBuilder` builds the scene items of the mark of a single view: `bar`, `rect`, `point`, `circle`, `square`, `rule`,
and `tick`. It exists so that the Vega-Lite mark geometry, the channel-to-property resolution, the paint defaults, and
the invalid-data rules are defined in one place, separate from the scales that provide the mappings and from the painter
that draws the result.

The unit's behavior is specified by the `CanvasNetVegaLite-Marks-MarkBuilder-*` requirements in
`docs/reqstream/canvas-net-vega-lite/marks/mark-builder.yaml`.

#### Data Model

The builder is stateless between calls; an instance holds the `ResolvedConfig`, the shared `SceneBudget`, the source
name, and the optional `RowExpressions` (the compiled expressions of the specification and the evaluator of the call;
null leaves every `{"expr": ...}` unevaluated). A private run object holds the state of one `Build` call (the mark
definition, the view, the table, the scales, the
data rectangle, and the token), and private helpers describe a channel (its definition, scale, and how to read a row's
value), a position sizing (fixed, band, or encoded), and a paint (fill, stroke, stroke width, opacity, fill opacity,
stroke opacity, and dash pattern).

**Geometry** (Vega-Lite 6):

- A discrete position of a `bar`, `rect`, or `tick` fills the band (its bandwidth, at least `minBandSize`); a point
  scale gives `step - 2`. An explicit `size`, `width`, or `height` (a number or `{band}`) is centered in the band.
- The thickness axis of a bar or tick on a continuous scale is centered on the value and `continuousBandSize` (5) thick
  unless a size says otherwise.
- The measure axis runs from the value to zero (to the first domain value when the domain does not contain zero); an
  absent channel spans the data rectangle; a bar or tick without a position on its thickness axis is centered and as
  thick as one minus the band padding (0.1 for a bar, 0.25 for a tick) of the rectangle.
- **Stacked ranges.** A stacked channel (`ResolvedScale.Stack` is set, its field is the upper end and its second field
  the lower end of the stack) is drawn by a `bar`, `rect`, or `rule` from the lower to the upper end instead of to zero,
  in a
  vertical or a horizontal stack, for `zero`, `center`, and `normalize`; `point`, `circle`, `square`, and `tick` sit at
  the
  upper end (the field the channel reads). A row whose end is not a number is not drawn.
- **Secondary channels.** `x2` and `y2` (a field, a datum, or a pixel `value`) make a position channel a range for
  `bar`, `rect`, and `rule`: the mark spans from the first to the second end, and a bar with a secondary channel is a
  range, not a
  thickness centered on the value. A rule with a range on one channel runs along that channel; with a range on both it
  is the segment between the two points. An explicit pixel `value` of the secondary channel wins over the lower end of a
  stack; a datum is mapped through the primary scale and is not part of the domain. Other marks report the secondary
  channels (`vl-channel-x2`, `vl-channel-y2`) and ignore them.
- **Binned geometry** (Vega-Lite's `rectBinPosition`). A `bar` or `rect` on a continuous channel that is binned (by the
  encoding, or `bin: "binned"` with `x2` or a bin `step`) spans from the position of the start of its bin to that of its
  end, each edge moved inward by half the `binSpacing` (1 for a bar, 0 for a rect and a tick; from the mark or the
  configuration), and the whole span shifted by the **half pixel** by which Vega translates the axis group (the marks
  themselves are not translated, so the half pixel is applied in the bin geometry only: the first bar of a spaced
  histogram starts at x = 1 and a rect at 0.5). A bin thinner than `minBandSize` (0.25 in the configuration) is widened
  to that size around its middle, without spacing. An explicit size (`size`, `width`, `height`) centers the mark on the
  middle of the bin instead. `point`, `circle`, `square`, `tick`, and `rule` sit at the **middle of the bin**
  (Vega-Lite's `bandPosition` 0.5).
- A `tick` is `thickness` (1 in the configuration) across and as long as its band; its orientation comes from
  `ScaleResolver.InferOrient` (vertical by default).
- `point`, `circle`, and `square` sit at the center of their band or value, and at the middle of the rectangle for
  an absent channel; `size` is the area (30 by style), the `size` channel maps through the size scale (a continuous,
  quantize, quantile, threshold, or ordinal one), and `shape` and `angle` are honored per row.
- A `rule` with only `x` is a vertical line across the height, with only `y` a horizontal line across the width (drawn
  from the far edge back to zero, as Vega-Lite does), and with both a stem from the value to zero along the measure
  axis; its stroke is black with a width from `strokeWidth`, `size`, or the size scale; a rule with neither channel
draws nothing. - `cornerRadius` (a single radius) rounds bars and rectangles.

**Paint** (Vega-Lite `init.ts` and `color()`): `filled` is on for every mark except `point` and `rule`; the mark color
(`#4c78a8`, black for a rule) is the fill of a filled mark and the stroke of an unfilled one, and unfilled bars and
symbols get a transparent fill. A `color`, `fill`, or `stroke` field is mapped through its scale whatever its kind
(`ScaleModel.MapText`): the category color of an ordinal scale (`tableau10` by default or `scale.range`), the ramp color
of a sequential or diverging scale (`#rrggbb`), or the color of the class of a quantize, quantile, or threshold scale; a
`value` is used as given, and the `fill` channel wins over `color`. An `opacity` field is mapped through the opacity
scale. `point`, `circle`, `square`, and `tick` marks get an opacity of 0.7 when nothing names an opacity. The
`strokeDashOffset`, `strokeCap`, and `strokeJoin` properties of the mark definition are honored, and so are
`fillOpacity`, `strokeOpacity`, and `strokeDash` of the mark definition; the **`strokeWidth`**, **`fillOpacity`**,
**`strokeOpacity`**, and **`strokeDash`** channels replace them for a row (a field through its numeric or dash scale, a
`value` as given, a `strokeDash` value being an array), at most 64 entries long (`MaxDashLength`: a longer pattern, from
the mark, a channel value, or a scale range, is a `VegaLiteRenderException` located at the mark, because every item
carries its own copy). A point's **`shape`** comes from the `shape` channel (a symbol name from the scale or a `value`;
a custom path is kept as written and the painter draws a circle for it), else from the mark's `shape`; its **`angle`**
from the `angle` channel (degrees), else the mark's. The `shape` channel is read only for a `point`, the `angle` channel
only for a point, circle, or square and the `strokeDash` channel for every mark but a rect
(`ScaleResolver.ChannelIsDrawn`); the catalog reports and the builder ignores the others. Explicit mark properties win
over style, `config.<mark>`, `config.mark`, and the Vega styles.

**Row-bound expressions.** A value written as `{"expr": ...}` is evaluated once per row, with
`datum` bound to the row of the
**transformed** table, by the evaluator of the call (so the steps count against the shared `MaxEvaluationSteps`). The
parser compiled the expression when the document was opened, so no text is parsed here. The sites are exactly:

- the paint properties of the mark definition `color`, `fill`, `stroke`, `opacity`, and `strokeWidth`, and `size` for
  the `point`, `circle`, and `square` marks (the area of the symbol), following the same precedence as the plain
  properties (`fill` or `stroke` wins over `color`; `color` is the fill of a filled mark and the stroke of an unfilled
one); and - the constant `value` of the `color`, `fill`, `stroke`, and `opacity` channels, and of the `size` channel of
  a symbol mark (a `value` of a channel overrides the corresponding mark-definition property, as before).

A result of the wrong kind leaves the default in place: a color expression that does not give text, a number expression
(`opacity`, `strokeWidth`, `size`) that does not give a finite number. Everything else that is an `{"expr": ...}` (other
mark properties, other encoding values, and conditions that cannot be evaluated) is not evaluated and is reported by the
feature catalog (`vl-expr-ref`, `vl-encoding-condition`, `vl-encoding-condition-param`).

**Ignored without a report**: `tooltip`, `href`, and the individual corner radii and
`cornerRadiusEnd`. `x2` and `y2` of the marks other than `bar`, `rect`, and `rule`, and the
offset channels are reported by the feature catalog and ignored here.

#### Key Methods

- `IsSupported(string? markType)` (static) - true for the seven drawn mark types.
- `Build(view, table, scales, width, height, token)` - reads the mark type (a string or the `type` of a mark
  definition); returns an empty `marks` group for an unsupported type; otherwise builds the rows of the table (polling
  the token every 1,024 rows and charging the item budget one item at a time as each item is created, so a row that
  draws nothing costs nothing), creating bar and rect items, symbol items, rule items,
  and tick items with resolved geometry and paint, and returns the group, clipped to the data rectangle only when the
  mark definition says `"clip": true` (an explicit `scale.domain` does not clip: Vega-Lite's `scaleClip` is for the
  extent of an interval selection, so a bar that starts at zero, as a default stack does, can overflow such a domain, as
  in Vega-Lite).
- Row filtering (Vega-Lite's invalid filter) - a row whose value for a channel with a continuous domain (`x`, `y`,
  `size`, or `opacity` on a linear scale, and a color, `strokeWidth`, `fillOpacity`, `strokeOpacity`, or `angle` channel
  on a linear, sequential, diverging, quantize, quantile, or threshold scale) is null, missing, NaN, or not a number is
  not drawn; a number that a log, pow, sqrt, or symlog scale cannot take (zero or a negative number on a log scale)
has no
  position (`ScaleModel.Map` gives null), so the mark is not drawn instead of being placed at a position that is not a
  number, and the bars of a log axis start at the start of the domain (`ZeroOrMin`) and are not stacked; a null in a
  discrete field is the category `null` and is drawn; a value outside an explicit discrete domain
  is not drawn by a position scale and gets the default (no color) from an ordinal color scale. A binned field reads
  the **start of the bin** from the table (the field the plan names), so the marks of one bin share the value of the
  scale: a bin-ordinal scale gives the color of the bin (a start below the first edge, or a null start, gets none), a
  linear scale the size or opacity of the start; the bars of a histogram whose `color` bins the field as `x` does take
  the color of their own bin.
- Conditions (Vega-Lite's conditional encodings) - on the color, fill, stroke, opacity, fillOpacity, strokeOpacity,
  strokeWidth, strokeDash, size, shape, and angle channels, the constructor's `RowExpressions` evaluate the `test` of
  every condition that the parser compiled, for every row, against the transformed row (so the steps are charged to
  `MaxEvaluationSteps` and the token is polled by the same loops as the other expressions, and a test that evaluates to
  null, a missing field, or any falsy value does not hold). The **first** condition that holds gives the row its value:
  the constant `value` (a `value` that is an `expr` is evaluated per row; a value of the wrong kind leaves the default,
  as an expression of the wrong kind does), or, for a condition that holds a field, the value of the scale of that field.
  When no condition holds the row gets what the channel gives without conditions: its scaled field, else its own
  `value`, else the default of the mark. A channel with a field of its own takes value conditions only; a channel
  that is only a value may take a field condition. A field condition leaves the continuous-domain row filter alone
  (the field is read only where its test holds). Conditions on a parameter, and tests that no expression can say, are
  left out by the parser, so they never hold. Without the `RowExpressions` (a caller that has none) the conditions
  are ignored and the channels draw as they do without them.
**Preconditions and postconditions**: the scales were resolved from the same view and data; `table` is the
**Main** table of
the implicit pipeline (the one the plan's field names refer to); on return the group holds one
item per visible mark in row order (deterministic), and the budget has been charged for them.

#### Error Handling

Exceeding `MaxSceneItems` is a `VegaLiteRenderException` located at the mark, thrown before the item that passes
the limit is created (the `SceneBudget` carries the failure; the marks group and every mark are charged first, then
created). A canceled token is an
`OperationCanceledException`. The evaluation of a row-bound expression can fail with a
`VegaLiteRenderException` located at
the expression (the step budget or the string limit is exhausted), which is propagated unchanged. A null configuration,
view, table, or scales argument is `ArgumentNullException` (not converted). Invalid data is not an error: affected rows
are dropped as described above. A mark type that is not drawn yields an empty group, because the feature catalog has
already reported it. The unit performs no I/O.

#### Dependencies

- **`ResolvedScales`**, **`ResolvedScale`** (with `Field2Path`, `Bin`, `BinSpec`, and `Stack`), and **`ScaleModel`** -
  the scales of the `Scales` subsystem
- **`DataTable`** and **`DataValue`** - the data of the `Data` subsystem
- **`RowExpressions`** - the compiled expressions and the evaluator of the call (the `Transforms` subsystem), and
  **`BinKind`** of the same subsystem (the bin kind of a channel), and
  **`CompiledExpression`** and **`ExpressionEvaluator`** of the `Expressions` subsystem, for the row-bound paint values
- **`ResolvedConfig`** and **`SpecNode`** - the `SpecModel` subsystem
- **`SceneGroup`**, **`SceneRect`**, **`SceneRule`**, **`SceneSymbol`**, **`SceneClip`**, and **`SceneBudget`** - the
  `Scenegraph` subsystem
- **`VegaLiteLimits`**, **`VegaLiteLocation`**, and **`VegaLiteRenderException`** - the public types of the
  `VegaLiteDocument` unit
- **.NET base class library** - `System.Globalization`, `System.Collections.Generic`

#### Callers

`VegaLiteDocument` calls `IsSupported` to decide if a view is drawn and `Build` once for the final data rectangle,
passing the Main table that the implicit pipeline produced and the `RowExpressions` of the call. The
tests in `test/DemaConsulting.CanvasNet.VegaLite.Tests/Marks/MarkBuilderTests.cs` call the builder directly through
`InternalsVisibleTo`, and the chart integration tests exercise it through the public document.
