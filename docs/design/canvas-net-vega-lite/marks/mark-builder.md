### MarkBuilder

![Marks Structure](VegaLiteMarksView.svg)

The `MarkBuilder` unit is the first software unit in the `Marks` subsystem (the second is _CurveBuilder_). It comprises
six
source files in the `DemaConsulting.CanvasNet.VegaLite.Marks` namespace; together they define one `internal` type, the
`sealed partial class MarkBuilder` (with private helper types for one channel, the sizing of a position, the paint of an
item, the settings of an overlay, and one build run, the nested `sealed partial class Run`):

- `MarkBuilder.cs` - the public surface (`IsSupported`, `Build`), the channels, the paint, the conditions and
  row-bound expressions, the bar, rect, tick, rule, and symbol marks, and the `Style` methods that apply the stroke
  properties to every kind of item
- `MarkBuilder.Order.cs` - the step meter (`Charge`), the cost of a sort, and the `order` channel as the drawing order
  of the marks (`MarkRows`)
- `MarkBuilder.Paths.cs` - the path marks `line`, `area`, and `trail`: reading, grouping, sorting, and generating the
  paths, and charging them
- `MarkBuilder.Overlays.cs` - the point and line overlays of the path marks
- `MarkBuilder.Text.cs` - the `text` mark: positions, polar offset, font properties, the text of a row, and its limits
- `MarkBuilder.Arcs.cs` - the `arc` mark, the polar span of a channel, and the polar position of a stacked `text`

Every file carries the provenance header comment for the d3-shape, d3-path, Vega, and Vega-Lite behavior it ports (see
_CanvasNetVegaLite System Design_, Third-Party Provenance and Attribution).

#### Purpose

`MarkBuilder` builds the scene items of the mark of a single view: `bar`, `rect`, `point`, `circle`, `square`, `rule`,
`tick`, `line`, `area`, `trail`, `text`, and `arc`, and the point and line overlays of the path marks. It exists so that
the Vega-Lite mark geometry, the channel-to-property resolution, the paint defaults, and the invalid-data rules are
defined in one place, separate from the scales that provide the mappings, from `CurveBuilder` that generates the curves,
and from the painter that draws the result.

The unit's behavior is specified by the `CanvasNetVegaLite-Marks-MarkBuilder-*` requirements in
`docs/reqstream/canvas-net-vega-lite/marks/mark-builder.yaml`.

#### Data Model

The builder is stateless between calls; an instance holds the `ResolvedConfig`, the shared `SceneBudget`, the source
name, the `MaxStringLength` of the limits (the bound of a label of a text mark), and the optional `RowExpressions` (the
compiled expressions of the specification and the evaluator of the call; null leaves every `{"expr": ...}` unevaluated).
A private run object holds the state of one `Build` call (the mark
definition, the view, the table, the scales and their `EncodingPlan`, the data rectangle, the token, and the overlay
definitions of the mark), and private helpers describe a channel (its definition, scale, and how to read a row's
value), a position sizing (fixed, band, or encoded), and a paint (fill, stroke, stroke width, opacity, fill opacity,
stroke opacity, and dash pattern). The `theta` and `radius` channels are read like `x` and `y` (the radius scale gets the
extent `min(width, height) / 2`), and the secondary channels `theta2` and `radius2` like `x2` and `y2`.

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
  stack; a datum is mapped through the primary scale and is not part of the domain. An `area` takes its baseline from
  the secondary channel the same way (see the area rules below). Marks other than these report the secondary
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
- **Time-unit bands** (the same `rectBinPosition` and `bandPosition` machinery). A channel on a **time or UTC** scale
  whose field has a time unit and whose end of the unit is known (`ResolvedScale.HasBandEnd` with a second field in the
  table: Vega-Lite's `hasBandEnd`) is a **time band** (`Channel.IsTimeBand`); `Channel.HasBandEnds`, which replaced
  `HasBinEnds`, is true for a bin or a time band, and `BinEdges` reads the start and the end of the unit through
  `ScaleModel.Map`. A `bar` or `rect` on a time band spans the unit exactly as it spans a bin: from the start to the end,
  with `binSpacing`, the half-pixel translate, and `minBandSize` (compared with the **whole** unit, so a unit thinner
  than `minBandSize` is widened around its middle to that size, without spacing). The unit may cover only a share of its
  width, centered: the **band** is the relative `{band}` of the `width` or `height` of the mark, else
  `timeUnitBandSize` (1 by the configuration), and the edges are at `(1 - band) / 2` and `(1 + band) / 2` of the way from
  the start to the end of the unit (Vega-Lite's `interpolatedSignalRef`, which on a linear scale equals the pixel
  interpolation, so a band of 1 is the full bin span and a band of 0.5 the middle half). An explicit size centers the
  mark on the **position inside the unit** instead. Where a mark that does not span sits (`Center`): `lerp(start, end,
  p)` with the **band position** `p` = 0.5 for a `bar` or `rect` (a different band position is reported as
  `vl-encoding-bandposition-rect`, and 0.5 is drawn), otherwise the `bandPosition` of the field definition, else the
  `timeUnitBandPosition` of the mark (0.5 by the configuration for a `tick`), else **0, the start of the unit**: a
  `point`, `circle`, `square`, or `rule` on a month axis sits on the first instant of its month, as in Vega-Lite, unless
  a band position is given. A time scale **without** a time unit has no band: a point sits at its date, and a `bar` on
  it is `continuousBandSize` thick, centered on the date, as on a linear scale. An ordinal or nominal field with a time
  unit uses the ordinary band scale over its date values.
- A `tick` is `thickness` (1 in the configuration) across and as long as its band; its orientation comes from
  `ScaleResolver.InferOrient` (vertical by default).
- `point`, `circle`, and `square` sit at the center of their band or value, and at the middle of the rectangle for
  an absent channel; `size` is the area (30 by style), the `size` channel maps through the size scale (a continuous,
  quantize, quantile, threshold, or ordinal one), and `shape` and `angle` are honored per row.
- A `rule` with only `x` is a vertical line across the height, with only `y` a horizontal line across the width (drawn
  from the far edge back to zero, as Vega-Lite does), and with both a stem from the value to zero along the measure
  axis; its stroke is black with a width from `strokeWidth`, `size`, or the size scale; a rule with neither channel
draws nothing. - `cornerRadius` (a single radius) rounds bars and rectangles.

**Paint** (Vega-Lite `init.ts` and `color()`): `filled` is on for every mark except `point`, `line`, and `rule`; the
mark color (`#4c78a8`, black for a rule and for a text) is the fill of a filled mark and the stroke of an unfilled one,
and unfilled bars and symbols get a transparent fill (a line has no fill). A `color`, `fill`, or `stroke` field is
mapped through its scale whatever its kind
(`ScaleModel.MapText`): the category color of an ordinal scale (`tableau10` by default or `scale.range`), the ramp color
of a sequential or diverging scale (`#rrggbb`), or the color of the class of a quantize, quantile, or threshold scale; a
`value` is used as given, and the `fill` channel wins over `color`. An `opacity` field is mapped through the opacity
scale. `point`, `circle`, `square`, and `tick` marks get an opacity of 0.7 when nothing names an opacity. The
`strokeDashOffset`, `strokeCap`, `strokeJoin`, and `strokeMiterLimit` properties of the mark definition are honored on
every item type, and so are
`fillOpacity`, `strokeOpacity`, and `strokeDash` of the mark definition; the **`strokeWidth`**, **`fillOpacity`**,
**`strokeOpacity`**, and **`strokeDash`** channels replace them for a row (a field through its numeric or dash scale, a
`value` as given, a `strokeDash` value being an array), at most 64 entries long (`MaxDashLength`: a longer pattern, from
the mark, a channel value, or a scale range, is a `VegaLiteRenderException` located at the mark, because every item
carries its own copy). A point's **`shape`** comes from the `shape` channel (a symbol name from the scale or a `value`;
a custom path is kept as written and the painter draws a circle for it), else from the mark's `shape`; its **`angle`**
from the `angle` channel (degrees), else the mark's. The `shape` channel is read only for a `point`, the `angle` channel
only for a point, circle, square, or text, and the `strokeDash` channel for every mark but a rect, area, trail, text, or
arc (`ScaleResolver.ChannelIsDrawn`); the catalog reports and the builder ignores the others. Explicit mark properties
win over style, `config.<mark>`, `config.mark`, and the Vega styles.

**Row-bound expressions.** A value written as `{"expr": ...}` is evaluated once per row, with
`datum` bound to the row of the
**transformed** table, by the evaluator of the call (so the steps count against the shared `MaxEvaluationSteps`). The
parser compiled the expression when the document was opened, so no text is parsed here. The sites are exactly:

- the paint properties of the mark definition `color`, `fill`, `stroke`, `opacity`, and `strokeWidth`, and `size` for
  the `point`, `circle`, and `square` marks (the area of the symbol), the `line` mark (its stroke width), the `trail`
  mark (the diameter of its points), and the `text` mark (the font size), following the same precedence as the plain
  properties (`fill` or `stroke` wins over `color`; `color` is the fill of a filled mark and the stroke of an unfilled
  one); and
- the constant `value` of the `color`, `fill`, `stroke`, and `opacity` channels, and of the `size` channel of a symbol,
  line, trail, or text mark (a `value` of a channel overrides the corresponding mark-definition property, as before).

A result of the wrong kind leaves the default in place: a color expression that does not give text, a number expression
(`opacity`, `strokeWidth`, `size`) that does not give a finite number. Everything else that is an `{"expr": ...}` (other
mark properties, other encoding values, and conditions that cannot be evaluated) is not evaluated and is reported by the
feature catalog (`vl-expr-ref`, `vl-encoding-condition`, `vl-encoding-condition-param`).

**Ignored without a report**: `tooltip`, `href`, and the individual corner radii and
`cornerRadiusEnd`. `x2` and `y2` of the marks other than `bar`, `rect`, `rule`, and `area`, and the
offset channels are reported by the feature catalog and ignored here. A `datum` of a temporal channel is read as one
instant (a number is milliseconds, a text is a date text in the calendar of the scale; a text that is not a date gives
the datum no position); a `datum` that is a date-time object is reported (`vl-encoding-datum-datetime`) and the
channel is ignored.

**Path marks** (`MarkBuilder.Paths.cs`). A `line`, `area`, or `trail` is built group by group, in two passes over the rows
and then one step per group:

1. **Pass 1, validity and positions.** Every row is read once. Its pixel positions are kept whenever the channels that
   give them have a number, even when another channel of the row is invalid, because the sort uses the position of the
   row. A row is a **defined** vertex when its positions are finite, its paint is valid (`TryPaint`: no channel with a
   continuous domain, no size, and no opacity that lacks a number) and, for a trail, its size is finite. Any other row is
   **undefined**, or **dropped** in the `filter` mode (see Invalid data modes below).
2. **Pass 2, grouping.** The rows are grouped by the values of the fields that `EncodingPlan` names
   (`PathGroupFields`: the canonical value fields of the non-position channels that have a field, such as `color`,
   `detail`, `key`, and, for a line, `size`; none gives one group). The groups are kept in a list in the order of their
   first row (a counting sort into one slice of a row array per group, so the work is linear and independent of the
   iteration order of a dictionary). A group value that is an array or an object compares by identity, so each such row
   is its own group.
3. **Per group.** A group with fewer than two defined points draws nothing and is charged nothing. Otherwise its rows are
   sorted **stably**: by the fields of the `order` channel when the encoding is not stacked, else by the pixel position
   along the dimension (x, or y for a horizontal orientation) when that channel is a field (an undefined position sorts
   first, as Vega's comparison does), and not at all when the sort is switched off (an `order` channel that is a value of
   null or false or, without an `order` channel, an `order` mark property of null or false) or the dimension is not a
   field. The path takes its **paint, stroke width, and dash from its first defined row** after the sort (Vega draws all
   the items of a path with the first one); the number of defined points and of runs of consecutive defined rows is
   counted; the scene budget is charged `1 + CurveBuilder.MaxSegments(...)`, the step budget `defined + bound`, and only
   then are the points listed and `CurveBuilder.Line` or `CurveBuilder.Area` generate the segments.

The rules of the three marks:

- **Line.** One `ScenePath` (role `mark-line`) per group, with the segments of the curve through the points
  `(x, y)` (the middle of the data rectangle for an absent channel), stroked and not filled. The `size` of a line is its
  stroke width: the `size` channel through the size scale per row (the first row decides), the constant `value` or the
  `{"expr": ...}` of it, the `strokeWidth` or `size` of the mark definition, else the `strokeWidth` of the configuration
  or of the Vega style (2).
- **Area.** One closed `ScenePath` (role `mark-area`) per group, filled with the paint of the first row (the stroke only
  when one is given). The orientation is `ScaleResolver.InferOrient` of the mark (an area without a decision is
  vertical); the dimension channel (x of a vertical area) gives the position along the axis and the **measure** channel
  (y of a vertical area) gives the **range** of each row: a stacked channel runs from the upper end to the lower end of
  its stack, a channel with a secondary channel (`y2` or `x2`) from its value to the second end, and any other value
  from the value to zero (or to the first value of the domain, when the domain has no zero), as for a bar. An area
  **without a measure channel draws nothing** (a deviation from Vega-Lite, which draws a zero-height area at 0). A row is
  defined when its position along the axis and both ends of its range are numbers. The outline is the top edge forward
  and the baseline backward along the same curve (see _CurveBuilder_).
- **Trail.** One `SceneTrail` (role `mark-trail`) per group whose runs are lists of `SceneTrailPoint(x, y, size)`: the
  size of a point is the `size` channel through the size scale, else the constant `value` or `{"expr": ...}`, else the
  `size` of the mark, else the Vega style (2); a row without a valid size is undefined. A trail is split into **runs** of
  consecutive defined rows, and a run of one point is dropped; a group without a run of two points draws nothing. The
  scene budget is charged `1 + kept` (the points of the runs that are kept) and the step budget `kept` before the runs
  are copied. A trail has no curve and no `interpolate`.

**Interpolation.** The curve of a line or an area is the `interpolate` of the mark definition, the style, `config.<mark>`,
or `config.mark` (linear by default), resolved by `CurveBuilder.TryParse` (a horizontal area makes `monotone` the
monotone-y curve); the `tension` property is the bundle `beta`, the cardinal tension, or the Catmull-Rom `alpha`. A name
that Vega-Lite does not know (which can come from the configuration, which the parser does not check) is the linear
curve, and so is `bundle` for an area, which d3-shape cannot draw (the feature catalog reports
`vl-mark-interpolate-bundle-area`). `interpolate` and `tension` are properties of the mark, constant for all its rows.

**Invalid data modes.** The `invalid` property of the mark definition, else of the style, `config.<mark>`, or
`config.mark`, selects what a path does at an undefined row: every mode that starts with `break-paths` (the default,
`break-paths-show-path-domains`) **breaks the path** (the undefined row ends the run, and the next defined row starts a
new subpath), and `filter` **drops** the row (the path connects its neighbors). `show`, null, and a text that is not a
known mode (from the configuration) give the default; the feature catalog reports `show` and null
(`vl-mark-invalid-show`). The scale domains are not filtered by the mode, so the invalid rows stay in the domains in
every mode; `filter` and `break-paths-filter-domains` are reported with a warning (`vl-mark-invalid-domains`). A mark
other than a path keeps its behavior: a row with an invalid continuous channel is not drawn.

**Overlays** (`MarkBuilder.Overlays.cs`; Vega-Lite's path overlay normalizer, built here and not by a layer expansion).
After the items of the mark, the builder draws a **line overlay** (an area whose `line` property is true or an object,
or which `config.area.line` enables) and then a **point overlay** (a `line`, `area`, `trail`, or `rule` whose `point`
property is true, an object, or `"transparent"`, or which `config.<mark>.point` enables; `false` and null switch it off
even when the configuration enables it). Each overlay is a second run of the builder on the same view, table, and
scales, whose mark definition is the overlay definition: the point overlay is a filled `point` mark with an opacity of
1 and the `clip` of the mark, whatever the overlay object adds to or replaces (`"transparent"` is the overlay with an
opacity of 0); the line overlay is a `line` mark with the `clip`, `interpolate`, and `tension` of the area. The overlay
run has no `x2` and `y2` (the line is drawn at the edge of the area), reads the stacked end of a stacked channel, skips
a channel that has a field but no scale (a channel that the overlaid mark does not draw), and groups its rows as a line
does. An `area` that has an overlay and names no `opacity`, `fillOpacity`, or configured opacity has the opacity 0.7.
Every overlay item is charged like the items of any run (a line overlay as a path, each point as an item).

**Draw order** (`MarkBuilder.Order.cs`). The `order` channel decides the drawing order of `bar`, `rect`, `tick`, `rule`,
`point`, `circle`, `square`, `text`, and `arc` (Vega-Lite sorts the marks by it) when the encoding is **not stacked**:
the rows are sorted stably by its fields, ascending or descending (`DataValue.Compare`), and the item drawn last is on
top. When the encoding is stacked the `order` channel orders the layers of the stack instead; an `order` channel of
null or false, or one without fields, keeps the order of the data. The step budget is charged
`m * ceil(log2(max(m, 2)))` before the sort of `m` rows.

**Text mark** (`MarkBuilder.Text.cs`; Vega-Lite's `text.ts` and `encode/text.ts` and Vega's text mark). One `SceneText`
(role `mark-text`) per row.

- _Position._ The position is that of the `x` and `y` channels (the middle of the data rectangle for an absent channel)
  plus the **polar offset** when the mark has a `radius` or a `theta` (a channel, or the mark property of the same name
  as the constant): with the radius `r` in pixels and the angle in radians clockwise from twelve o'clock, the anchor
  moves by `r` along that angle (x grows by `r` times the sine and y falls by `r` times the cosine), only when `r` is not
  zero. A constant `value` of the channel is used as given. A `radius` scale has the extent `min(width, height) / 2`.
  A row whose `theta` or `radius` is invalid is not drawn. `dx` and `dy` stay on the item (the painter applies them) and
  are not added to the position. A **stacked** `theta` or `radius` is placed inside its slice, at the scale of
  `(1 - p) * start + p * end` with `p` the `bandPosition` of the channel (0.5, the middle of the slice, by default), so
  that the labels of a pie sit in their slices.
- _Properties._ `align` (default `center`; a name that Vega does not know gives `left`), `baseline` (default `middle`;
  an unknown name gives `alphabetic`), `dx`, `dy`, `angle` (the property or the `angle` channel, in degrees), `font`,
  `fontStyle`, `fontWeight` (a text or a number), `limit`, `ellipsis` (an empty text gives the default ellipsis
  character), `lineHeight`, and `lineBreak`, plus the paint of a filled mark (black by default). The **font size** is,
  in order of precedence: the `size` channel through the size scale (or its constant `value`), the `fontSize` of the
  mark definition, a row-bound `size` expression, the `size` of the mark definition, the configuration's `fontSize` or
  `size`, the Vega style, and 11.
- _The text of a row._ The `text` channel wins over the `text` property of the mark (a text or an array of texts). A
  **field** is formatted as Vega-Lite's `formatSignalRef` does: a `formatType` that is not `number`, `time`, or `utc` is
  a custom format that is not evaluated (the `format` is ignored, the default format of the type applies, and the catalog
  reports `vl-format-type-custom`); the formats `time` and `utc`, a temporal type, and a time unit give a **time format**
  (`TimeLabels.CreateBounded`: the `format`, else the specifier of the time unit, else `config.timeFormat`; the calendar
  is UTC for a `utc` unit, scale type, or format type, else the calendar of the chart); a **binned** field (the encoding
  plans a `bin` on `text`) shows `start – end` (an en dash between spaces) through the number format, or `null` when the
  start is not a number; a quantitative field and any field with a `format` give a **d3 number format** (the `format`,
  else `config.numberFormat`, else the empty specifier) of the JavaScript number of the value, so that a null is `0`;
  every other value is its JavaScript text (a null is `null`, and an array is one line for each element, a null element
  being an empty line). A time format applied to a value that is neither a date nor a finite number shows its
  JavaScript text. A `datum` is formatted the same way, and a `value` (a text, a number, a boolean, or an array of them;
  null and objects give no text) is used as given. An invalid `format` is a located `VegaLiteSpecException`.
- _Lines._ An array is one line for each element. The `lineBreak` of the mark splits a text on that literal text (never
  an array). An empty text or an empty array draws nothing.
- _Limits._ A row is charged in this order: its position and paint are read (a row that is skipped costs one step and
  no item); then its label is made under the **allowance** `min(MaxStringLength, 16 * MaxStringLength - characters so
  far)`; then `characters + lines` are charged to the step budget; then the lines are charged to the scene budget; and
  only then is the `SceneText` created (see the charging table: rows T1 to T4). A label above `MaxStringLength`, or the
  labels of the mark together above sixteen times that (the internal constant `TextTotalFactor`), are a located
  `VegaLiteRenderException`; the time formatter and the JavaScript text conversion stop as soon as they would exceed the
  allowance, and the delimiters of a `lineBreak` are counted before the lines are made.

**Arc mark** (`MarkBuilder.Arcs.cs`; Vega-Lite's `arc.ts` and the polar case of `rectPosition`). One `SceneArc` (role
`mark-arc`) per row with a valid center, angular span, radial span, and paint.

- _Center._ The position of `x` and `y` (the middle of the data rectangle for an absent channel).
- _Span_ (`PolarSpan`, once for the angles and once for the radii). A band or point scale gives its band; a stacked
  channel, or a channel with `theta2` or `radius2` (a field, a datum, or a value), gives the range between the two ends;
  any other field gives the range from zero (or the first value of the domain) to the scaled value; a constant `value`
  channel is the main end. The second end of a channel without a secondary channel is the mark property (`theta2`,
  `radius2`, or `innerRadius`) before it is zero. Without the channel the main end is the mark property (`theta`;
  `radius` or `outerRadius`) else a whole turn (`2 * PI`) or half of the smaller side of the data rectangle, and the second
  end is its mark property, else zero. The two ends are **ordered** (the smaller first), so the larger radius is the outer
  one and a reversed scale still makes a sector. A span with an end that is not finite is invalid.
- _Properties._ `padAngle` and `cornerRadius` come from the mark definition, then the configuration (constants, not
  channels); the paint is that of the other filled marks (the mark color is the fill, a stroke only when one is given).
  A `theta2` or `radius2` without its main channel is ignored (the mark properties are the way to give a constant end).
- _Limits._ Each row is charged one evaluation step, its position and paint are read, and then one scene item is charged
  before the `SceneArc` is created; a row that draws nothing costs no item. The painter builds the outline from the
  parameters of the arc (see _ArcGeometry_).

**Stroke properties.** The `Style` methods give `strokeDash`, `strokeDashOffset`, `strokeCap`, `strokeJoin`, and
`strokeMiterLimit` to every kind of item: rectangles, symbols, rules (which now also carry the join and the miter limit),
paths, trails, texts, and arcs.

**Charging as implemented** (the evaluation steps are charged through a `WorkMeter` on the evaluator of the call, one
step for every 64 elements, and nothing is charged when the call has no `RowExpressions`; the scene items are charged to
the `SceneBudget`; every charge is made before the work or the object it pays for):

| Id | Work | Evaluation steps | Scene items |
| --- | --- | --- | --- |
| P1 | Row visit of a path (pass 1) | `n` once | - |
| P1 | Row visit of a text or arc row | 1 per row | - |
| P2 | Grouping of a path (pass 2) | `n * (1 + K)`, `K` grouping fields | - |
| P3 | Sort of `m` rows (path group, draw order) | `m * ceil(log2(max(m, 2)))` | - |
| P4 | Marks group | - | 1 |
| P5 | Line or area path | `defined + bound` | `1 + bound` |
| P6 | Trail | `kept` | `1 + kept` |
| P7 | Group with fewer than two defined points | nothing | nothing |
| P8 | Overlay | as the run of its mark | as the run |
| T1 | Text item and its lines | - | `lines`, before the item |
| T2 | Text characters | `characters + lines` | - |
| T3 | Text array, `lineBreak` | within T2 | within T1 |
| T4 | Text measurement | none (the painter measures) | - |
| A1 | Arc | 1 per row | 1, before the item |

`n` is the number of rows, `defined` the defined points of a path, `bound` the `CurveBuilder.MaxSegments` of it, and
`kept` the points of the runs of a trail that are kept. The rows of a bar, rect, tick, rule, or symbol charge one scene
item each and no steps. The token is polled every 1,024 rows by every row loop and in pass 1 and pass 2 of a path, once
for every path group, during the sort (`StableSorter`), by the curve generators, and as the step counter advances. A
failure is a located `VegaLiteRenderException`: the scene budget names `VegaLiteLimits.MaxSceneItems`, the step budget
reports the exhausted evaluation steps, and the text limits name `VegaLiteLimits.MaxStringLength`.

**Documented deviations from Vega-Lite and the plan** (each pinned by a test or reported by the feature catalog):

1. **No `shape` overlay trigger**: Vega-Lite enables the point overlay for an encoding with a `shape` channel; here the
   `shape` channel of a line is not drawn and the overlay is not enabled by it. A `size` channel of the overlaid mark is
   applied through the scale of that mark to the overlay points as well.
2. **Invalid-mode domains**: the scale domains are never filtered by the `invalid` mode (reported by the warning
   `vl-mark-invalid-domains`); `show` is not supported (`vl-mark-invalid-show`).
3. **An area without a measure channel** draws nothing.
4. **A bar or area without stack-by fields is still stacked** (as Vega-Lite's `stack` is only null for an aggregate
   without stack-by): the `order` channel of such a bar is a stack order and does not sort the drawing, and the null `y`
   of an area is a vertex with the value 0. `"stack": null` turns the stack off.
5. **Stack impute of a binned dimension** is not done (the warning `vl-encoding-stack-impute-bin`); see _EncodingPlan_.
6. **Polar bins** on `theta` or `radius` are not drawn (reported as `vl-encoding-bin`), and a time unit on `theta` is read
   at the start of the unit. A stacked arc row whose value is null is a zero-width slice (the stack takes null as 0); an
   unstacked null is not drawn.
7. **`startAngle`, `endAngle`, and `padRadius`** are not Vega-Lite mark properties here (`theta` and `theta2` are) and are
   reported as unknown properties.
8. **The text mark's rules**: a null number is formatted as `0`; `lineBreak` is a literal text, not a pattern; `dx` and
   `dy` are not added to the position; a label is bounded by `MaxStringLength` and all the labels of a mark by sixteen
   times that; a `condition` on `text` is reported and not applied.
9. **Sort position of an invalid row**: a path is sorted by the position of a row even when another channel of the row
   is invalid, and the invalid row breaks the path at its sorted place.
10. **Bundle for an area** is drawn with the linear curve (see _CurveBuilder_).
11. **`discreteBandSize`** still applies to a band `theta` through the band span (Vega-Lite reads it only for x and y).
12. **Orientation**: `ScaleResolver.InferOrient` gives a trail and an arc `vertical`, and its temporal fall-through (a
    temporal x next to a y that is not temporal is vertical, and the reverse horizontal) applies to a line, an area,
    and a tick only (see _ScaleResolver_).
13. **Stack grouping**: the stack of the encoding plan does not group by the stacked field when a dimension channel reads
    it, as in a radial chart whose `theta` and `radius` share a field (see _EncodingPlan_).
14. **Totality of the painter**: the outline of an arc whose parameters have a magnitude above 1e9 is empty, and a
    trail point that has a magnitude above 1e9 or a size that is not positive ends its run (see _ArcGeometry_ and
    _ScenePainter_); the builder itself drops only the rows whose values are not finite and passes the large values on.

#### Key Methods

- `IsSupported(string? markType)` (static) - true for the twelve drawn mark types (`bar`, `rect`, `point`, `circle`,
  `square`, `rule`, `tick`, `line`, `area`, `trail`, `text`, and `arc`).
- `Build(view, table, scales, width, height, token)` - reads the mark type (a string or the `type` of a mark
  definition); returns an empty `marks` group for an unsupported type; otherwise builds the rows of the table (polling
  the token every 1,024 rows and charging the item budget one item at a time as each item is created, so a row that
  draws nothing costs nothing), creating bar and rect items, symbol items, rule items, tick items, path items (lines
  and areas, one item per group), trail items, text items, and arc items with resolved geometry and paint, followed by the
  items of the overlays of a path mark, and returns the group, clipped to the data rectangle only when the
  mark definition says `"clip": true` (an explicit `scale.domain` does not clip: Vega-Lite's `scaleClip` is for the
  extent of an interval selection, so a bar that starts at zero, as a default stack does, can overflow such a domain, as
  in Vega-Lite).
- Row filtering (Vega-Lite's invalid filter; the path marks use the invalid modes described above instead) - a row whose
  value for a channel with a continuous domain (`x`, `y`,
  `size`, or `opacity` on a linear, time, or UTC scale, and a color, `strokeWidth`, `fillOpacity`, `strokeOpacity`, or
  `angle` channel
  on a linear, sequential, diverging, quantize, quantile, or threshold scale; a date that is not valid has no number)
  is null, missing, NaN, or not a number is
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
  are ignored and the channels draw as they do without them. (A `condition` on the `text` channel is reported by the
  catalog and not applied.)

**Preconditions and postconditions**: the scales were resolved from the same view and data; `table` is the
**Main** table of
the implicit pipeline (the one the plan's field names refer to); on return the group holds the items in a deterministic
order (the order of the rows, the order of the `order` channel, or the order of the groups of a path, then the items of
the overlays), and the budget has been charged for them.

#### Error Handling

Exceeding `MaxSceneItems` is a `VegaLiteRenderException` located at the mark, thrown before the item that passes
the limit is created (the `SceneBudget` carries the failure; the marks group and every mark are charged first, then
created; the segments of a path are charged with their upper bound before the segment list exists). A canceled token is
an `OperationCanceledException`. The evaluation of a row-bound expression can fail with a
`VegaLiteRenderException` located at
the expression (the step budget or the string limit is exhausted), which is propagated unchanged; the step charges of
the paths, the sorts, the text labels, and the arcs fail the same way, located at the mark. A label of a text mark that
is
longer than `MaxStringLength`, or the labels of a mark that are together longer than sixteen times that, are a located
`VegaLiteRenderException`. A number or time format of a text channel that is not valid is a `VegaLiteSpecException`
located at the `format`. A null configuration,
view, table, or scales argument is `ArgumentNullException` (not converted). Invalid data is not an error: affected rows
are dropped (or break their path) as described above. A mark type that is not drawn yields an empty group, because the
feature catalog has already reported it. The unit performs no I/O.

#### Dependencies

- **`ResolvedScales`**, **`ResolvedScale`** (with `Field2Path`, `Bin`, `BinSpec`, `Stack`, `TimeUnit`, `HasBandEnd`,
  `Calendar`, and `IsTemporalField`), and **`ScaleModel`** -
  the scales of the `Scales` subsystem, and **`NumberFormat`** of the same subsystem (the d3 number format of the text
  of a text mark; see _NumberFormat_)
- **`DataTable`** and **`DataValue`** - the data of the `Data` subsystem, and **`TimeCalendar`** and **`JsDateParser`**
  of the same subsystem, which read the text of a temporal `datum` as a date in the calendar of its scale (see _Time
  Calendar Design_ and _Time Format Design_)
- **`RowExpressions`** - the compiled expressions and the evaluator of the call (the `Transforms` subsystem), and
  **`BinKind`** of the same subsystem (the bin kind of a channel), and
  **`CompiledExpression`** and **`ExpressionEvaluator`** of the `Expressions` subsystem, for the row-bound paint values
  and the JavaScript text of a label
- **`EncodingPlan`** (its `PathGroupFields`, `OrderFields`, `Stack`, and `PlanPathGroupFields`), **`WorkMeter`** (the
  step charges), and **`StableSorter`** (the stable sort of the rows) - the `Transforms` subsystem (see
  _EncodingPlan_ and _AggregateTransforms_)
- **`CurveBuilder`**, **`CurveKind`**, **`CurvePoint`**, and **`AreaPoint`** - the `CurveBuilder` unit of this
  subsystem, which generates the segments of a line or an area (see _CurveBuilder_)
- **`TimeLabels`** (`CreateBounded`) - the `Guides` subsystem, the time formatter of the text of a text mark with a
  per-label limit (see _TimeLabels_)
- **`ResolvedConfig`**, **`ConfigNode`**, **`SpecNode`**, and **`FeatureCatalog`** (`IsEvaluatedFormatType`) - the
  `SpecModel` subsystem
- **`SceneGroup`**, **`SceneRect`**, **`SceneRule`**, **`SceneSymbol`**, **`ScenePath`** with **`ScenePathSegment`**,
  **`SceneTrail`** with **`SceneTrailPoint`**, **`SceneText`**, **`SceneArc`**, **`SceneClip`**, and **`SceneBudget`** -
  the `Scenegraph` subsystem (the painter builds the outline of an arc and the capsules of a trail from their parameters;
  see _SceneModel_, _ScenePainter_, and _ArcGeometry_)
- **`VegaLiteLimits`**, **`VegaLiteLocation`**, **`VegaLiteRenderException`**, and **`VegaLiteSpecException`** - the
  public types of the `VegaLiteDocument` unit
- **.NET base class library** - `System.Globalization`, `System.Collections.Generic`

#### Callers

`VegaLiteDocument` calls `IsSupported` to decide if a view is drawn and `Build` once for the final data rectangle,
passing the Main table that the implicit pipeline produced and the `RowExpressions` of the call. The
tests in `test/DemaConsulting.CanvasNet.VegaLite.Tests/Marks/` (`MarkBuilderTests.cs`, `MarkBuilderChannelTests.cs`,
`MarkBuilderConditionTests.cs`, `MarkBuilderTimeTests.cs`, `MarkBuilderPathTests.cs`, `MarkBuilderOrderTests.cs`,
`MarkBuilderOverlayTests.cs`, `MarkBuilderTextTests.cs`, `MarkBuilderArcTests.cs`, and `MarkBuilderPolarTextTests.cs`)
call the builder directly through
`InternalsVisibleTo`, and the chart integration tests exercise it through the public document.
