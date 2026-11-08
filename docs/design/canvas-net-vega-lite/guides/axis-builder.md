### AxisBuilder

![Guides Structure](VegaLiteGuidesView.svg)

The `AxisBuilder` unit is the third software unit in the `Guides` subsystem. It is one source file, `AxisBuilder.cs`,
in the `DemaConsulting.CanvasNet.VegaLite.Guides` namespace, and defines four `internal` types: `AxisBuilder`,
`AxisRequest`, `AxisTick`, and `AxisResult` (sealed records), plus private helper types (the layout and settings of one
axis). The file carries the provenance header comment for the Vega and Vega-Lite axis rules it ports and for the d3-time
behavior of its time axes. The label formats and the tick requests of the date axes are shared with the legends through
the `TimeLabels` unit (see _Time Labels Design_), and the `labelExpr` of the axis is evaluated by the
`LabelExpressions` unit (see _LabelExpressions Unit Design_).

#### Purpose

`AxisBuilder` builds the axis guide of a position scale of a view (the single view, or one axis of a layered view): the
grid lines, the tick marks, the tick labels,
the domain line, and the title, as scene groups positioned at the edge of the data rectangle, and measures how far they
reach beyond it. It exists so that the axis properties, their defaults, configuration precedence, geometry, and
the label-overlap removal of Vega-Lite and Vega are defined in one place, and so that the layout can size the canvas around
the axes without knowing anything about them.

The unit's behavior is specified by the `CanvasNetVegaLite-Guides-AxisBuilder-*` requirements in
`docs/reqstream/canvas-net-vega-lite/guides/axis-builder.yaml`.

#### Data Model

- **`AxisRequest`** `(string Channel, SpecNode? ChannelDef, ResolvedScale Scale, ScaleModel Model, double DataWidth,
  double DataHeight, string? MarkType, SceneBudget? Budget, string? DefaultOrient, bool? DefaultGrid, bool InHeader)` -
  what the builder needs: the channel (`x` or `y`), its definition in the encoding (whose `axis`, `title`, `field`,
  `aggregate`, and `bin` are read), its resolved scale and the model for the final data rectangle, the size of that
  rectangle, and the mark type (a `rect` mark with a discrete channel draws its axis in front of the mark). Since phase
  10, `DefaultOrient` is the side that a layered view gives the axis when the `axis` object names none (the second
  independent axis of a channel is on the opposite side) and `DefaultGrid` whether the axis has a grid when the `axis`
  object says nothing (only the first independent axis has one); both are null for a single view, which keeps the usual
  rules. Since phase 11, `InHeader` (false by default) builds the axis that the cells of a facet share for the header or
  footer that holds it (see _Header axes_ below).
- **`AxisTick`** `(DataValue Value, double Position, string Label, IReadOnlyList<string>? Lines)` - one tick: the domain
  value (a date for a time tick, or a plain number for an instant outside the years a date can hold), its position along
  the axis (rounded like the tick mark), its label text before truncation, and the lines of a multi-line label (set when
  a label expression gave an array; null for the single line of `Label`).
- **`AxisResult`** `(SceneGroup Group, SceneGroup? Grid, SceneBounds Bounds, string Orient, IReadOnlyList<AxisTick>
  Ticks, int ItemCount, bool InFront, bool InHeader)` - the axis group (role `axis`, with ticks, labels, domain, and
  title as children, in drawing order), the separate grid group (role `axis-grid-group`, at the same position, or null),
  the bounds of the axis in the data rectangle's coordinates (the grid is not part of them), the side, the ticks
  (including those whose labels were dropped because they overlap), the number of scene items for the budget, whether
  the axis is drawn in front of the marks (`zindex` above 0), and whether it was built for a header (`InHeader`).
- **`AxisBuilder`** holds the `TextMeasurer`, the `ResolvedConfig`, the `MaxTicks` and `MaxSceneItems` limits, the
  source name, and the `LabelExpressions` of the call (the constructor takes the optional `RowExpressions` of the call;
  without them the builder evaluates on an evaluator of its own with the same limits). `AxisRequest` may carry the
  `SceneBudget` of the chart; the builder then charges the axis group, the grid group, each grid line, tick rule, the
  remaining labels, the domain line and the title text before it creates them (an
  axis without a shared budget uses a budget of its own from `MaxSceneItems`).

**Properties and precedence.** A property is looked up in the axis of the channel, then the Vega-Lite-only configuration
(`axisX`, `axisY`, and the `Band`, `Point`, `Discrete`, `Quantitative`, and `Temporal` variants: `axisTemporal` and
`axisXTemporal` for a time or UTC scale), then the Vega configuration (`axisBand`
for band scales, `axisTop`, `axisBottom`, `axisLeft`, and `axisRight`, `axisX` and `axisY`, `axis`, including the Vega
defaults), then the `guide-label` and `guide-title` styles. Rules used only when no Vega-Lite configuration names the
property: `grid` is true for a continuous scale (linear, time, or UTC) with a field or an aggregate (a count has no
field) and no bin
(neither a
binned nor a pre-binned field); `labelAngle` is 270 for labels of a nominal or
ordinal x axis (with Vega-Lite's label alignment and baseline for the angle), **except** for a field with a time unit,
whose labels are not turned by default; `labelFlush` is true (1 pixel) for a
quantitative or temporal x axis; `labelOverlap` is parity for non-discrete scales and for a field with a time unit
whose `sort` is not an object (a month axis hides every other label rather than all but some); `zindex` is 1 for a
`rect` mark with a discrete
channel and otherwise 0.

**Honored properties** are `grid`, `ticks`, `labels`, `domain`, `title` (the axis title, then the channel title, then the
default title that the encoding plan gives the scale in `ResolvedScale.Title` - `Sum of x`, `Count of Records`,
`x (binned)`, two titles of a range merged as `a, b` - then the field name; null hides it;
an array gives lines), `orient` (top or bottom for x, left or right for y), `offset`,
`minExtent`, `maxExtent`, `tickSize`, `tickWidth`, `tickColor`, `tickDash`, `tickDashOffset`, `tickCap`, `tickOpacity`,
`tickRound`, `tickOffset`, `tickCount` (a number, or for a time axis an interval name or `{interval, step}`),
`tickMinStep`, `values` (numbers, or for a time axis or a field with a time unit timestamps, date texts, and date-time
objects), `format` (d3 number format, d3 time format for a date axis, or an object of multi-format levels),
`formatType` (`number`, `time`, or `utc`), `labelAngle`, `labelAlign`,
`labelBaseline`, `labelPadding`, `labelOffset`, `labelLimit`, the label font properties and `labelColor`, `labelOpacity`,
and `labelLineHeight`, `labelFlush`, `labelFlushOffset`, `labelOverlap` (true, `parity`, `greedy`, or false),
`labelSeparation`, `domainColor`, `domainWidth`, and the other domain properties, the `grid` color, width, dash, opacity,
and cap, `titleAnchor`, `titleAngle`, `titleAlign`, `titleBaseline`, `titleColor`, `titleFont*`, `titleLimit`,
`titleLineHeight`, `titleOpacity`, `titlePadding`, `zindex`, and `bandPosition` (the configuration default 0.5), and, since
phase 13, the static properties `labelBound`, `tickBand`, `tickExtra`, `position`, `translate`, `bandPosition` (as an
axis property), `titleX`, and `titleY` (see _Static axis properties_ below). The
properties `encode` and a `formatType` other than `number`, `time`, and `utc` (a custom format type) are not
drawn and are reported by the feature catalog. The `labelExpr` is honored (see _Label expressions_ below); the axis of a
position that holds a nested offset (`xOffset` or `yOffset`) is the ordinary band axis of the position (the offset
channel has no axis of its own).

**Geometry** (Vega): the group sits at the edge of the data rectangle shifted half a pixel, so a one-pixel line is
crisp; tick and grid positions are the scale position plus the band offset (half a band, with the `-0.5` correction of
`axisBand`) plus `tickOffset`, rounded half up; labels use the unrounded position plus `labelOffset`; ticks stand `tickSize`
out from the data rectangle and labels `tickSize` plus `labelPadding`; the extent is the ticks and labels clamped to
`minExtent` and `maxExtent`; the title sits `titlePadding` beyond the extent, with multi-line titles pushing further for
the top, left, and right axes; bounds follow Vega (a stroked rule grows by half its width and text by its measured box).

**Static axis properties** (Phase 13; ports of `vega-parser` `guides/guide-util.js`, `axis-title.js`, and
`axis-labels.js`, and of the `Overlap` transform of `vega-view-transforms`).

- **Band and extra tick.** `TickBand.Resolve` gives the fraction of the bandwidth that is added to the position of a
  tick (`Band`), whether there is an extra tick (`Extra`), and the offset in pixels. Without a `tickBand` the band is
  `bandPosition` (0.5 by default), the extra tick is `tickExtra` (false by default), and the offset is `tickOffset`; a
  `tickBand` of `"extent"` is the end of the band (1) with an extra tick and no offset, and any other value is the
  middle (0.5) without an extra tick and with the `tickOffset`. The ticks and the grid take the band
  shift `bandwidth x Band + offset`; **the labels keep a band of 0.5** (`axis-labels.js`) and only the offset, except on
  a position that holds a ranged offset (below). The extra
  tick is pegged to the first domain value (the start of its band, plus the offset) and is appended after the label
  expression has replaced the labels, when the band is not 0; it has a tick mark and a grid line but no label
  (`AxisTick.IsExtra`), so the labels skip it.
- **A position that holds a ranged offset** (Phase 14; Vega-Lite's `defaultBandPosition` of `compile/axis/properties.ts`
  and `labelsPositionSpec`; see _Continuous offsets_ in the `ScaleResolver` design). When the position is a band scale
  of an `x` or `y` channel whose `xOffset` or `yOffset` is a ranged offset (`ScaleResolver.HasRangedOffset`: a bar or
  area whose offset is an unbinned quantitative field), `RangedDefaultBand` gives the default `bandPosition`, 0 for `x`
  and 1 for `y`, so the ticks and the grid sit at the baseline of the offset scale (`TickBand.Resolve` takes it as its
  fallback; an explicit `axis.bandPosition` wins over it, as it wins over the configuration). Vega puts the labels of a
  band scale in the middle whatever the band position, so Vega-Lite gives them their own position, and
  `RangedLabelBandOf` does the same: the labels sit at the `bandPosition` of the axis (the default above), with the
  `labelOffset` and no tick offset; a band position of 0.5 leaves them in the middle. The other axes stay in the middle
  of the band.
- **`translate` and `position`.** The group of the axis sits at the edge of the data rectangle plus the offset, shifted
  by `translate` (0.5 by default: the half pixel that keeps a one-pixel line crisp, replaced by the property; the shift
  of the bins of a bar in `MarkBuilder` does not read it) and, along the axis, by `position`.
- **`titleX` and `titleY`** replace the position of the title in the pixels of the axis group: the value replaces the
  position along the anchor or the distance from the axis, whichever coordinate it names.
- **`labelBound`.** `true` is a tolerance of 1 pixel, a number is the tolerance, and 0 or false (or no value) is none.
  The test runs only when the labels have an overlap method (without one the `Overlap` transform returns before it),
  skips a label whose box is not larger than 1 pixel in both directions, and hides a label whose box along the axis is
  not enclosed by the range of the scale grown by the tolerance (along x for a horizontal axis and along y for a
  vertical one; a negative tolerance tests nothing).

#### Key Methods

- `Build(AxisRequest request, CancellationToken token)` - returns null when the channel's `axis` is null or false;
  otherwise resolves the settings and the orientation (an explicit `axis.orient`, else the request's `DefaultOrient`, else
  the configuration's orient, else bottom for `x` and left for `y`; the grid likewise: an explicit `axis.grid`, else
  `DefaultGrid`, else the configuration, else the Vega-Lite rule), builds the ticks (`BuildTicks`), creates the grid
  rules, tick rules,
  labels, and domain rule, computes the bounds of ticks and labels, resolves the title, and finishes the groups with the
  extent clamping and title placement. The token is polled per tick.
- **Header axes** (`InHeader`): Vega-Lite's `mergeChildAxis` moves the main part of an axis that the cells of a facet
  share (ticks, labels, domain, title) into the header or footer of the facet and leaves the grid in the cells (`bottom`
  and `right` axes go to footers, `top` and `left` to headers). The header group has the size of a cell along the header
  and none across it, so with `InHeader` the axis group's origin is `(0, 0)` shifted by the axis `offset` on its own
  side (`top`: y = -offset, `left`: x = -offset, `bottom`: y = offset, `right`: x = offset) instead of at the far edge
  of the data rectangle (`DataWidth` and `DataHeight` still give the extent along the axis), plus the half pixel of
  Vega's translate; the ticks, labels, domain, title, item count and extent are those of the axis next to the data
  rectangle, and `AxisResult.Bounds` are in the coordinates of the header group (the same bounds moved by the data
  height or width). The grid group is built where the cell draws it, at the origin that the axis would have next to the
  data rectangle. Nothing changes for a request with `InHeader` false.
- Ticks (`BuildTicks`): a time or UTC scale takes the time ticks described below; a linear scale (and, with a
  `formatType` of `time`, `utc`, or `number`, the labels follow that type) uses `Ticks.ModelTicks` (the d3 ticks of the
  domain, or the log ticks for a log
  scale) with Vega-Lite's count (`ceil(size / 40)`, or the number
  of `values`, or `tickCount`; **ten** for a log scale, for which Vega-Lite sets no count of its own) and minimum step
  (`Ticks.ResolveTickCount`, which does not lower the count by the tick step for a log scale), and labels from
  `NumberFormat.TickFormat` (the default is `,f`; negatives
  use the minus sign U+2212); a **log** axis labels with `NumberFormat.FormatFloat` (Vega's variable precision: up to 12
  digits, the zeros trimmed, `format` honored) and, for generated ticks, gives an empty label to every tick that d3's log
  rule hides (`Ticks.LogLabelTest`; the labels of explicit `values` are all shown), and its labels are thinned by the
  greedy overlap method by default (Vega-Lite's `labelOverlap` default for log scales, parity for the other continuous
  scales); a pow, sqrt, or symlog axis uses the ordinary ticks and `TickFormat` of its domain at the transformed positions;
  a field that the encoding bins (`BinKind.Binning`, not a pre-binned one) has the default count
  `ceil(size / 10)` instead, so the ticks follow the bins, and the labels of a normalized stack
  (`ResolvedScale.Stack` is `Normalize`) use `config.normalizedNumberFormat` (`.0%` when the configuration has none) unless
  the axis gives a `format`; explicit `values` follow Vega's `validTicks` (in range, in range order, thinned to the count);
  a band or point scale has a tick for every category (or the existing `values`), labeled with JavaScript-style text of
  the value (numbers formatted with `format` when given). A band or point scale over the **dates of a time unit** (an
  ordinal or nominal field with a `timeUnit`) labels each date with the time labels described below, and its `values`
  entries follow the same unit rule as the domain entries (a number or a text names the date of that unit part).
- **Time ticks** (`TimeTicks`): a time or UTC scale has the d3 time ticks (`Ticks.TimeTicks`: the calendar interval
  whose length is nearest to the span divided by the count, at the local midnights and month starts of the zone of the
  chart, or of UTC for a UTC scale). The **count** is the `tickCount` number when there is one; otherwise the number of
  `values` when there are some; otherwise `ceil(size / 40)`, except that Vega-Lite leaves the default to Vega, which uses
  **ten**, for a field whose time unit is exactly one part that is a month, an hour, a day, or a quarter
  (`TimeLabels.UsesVegaDefaultCount`). The count is then limited by `tickMinStep` through `Ticks.ResolveTickCount` with
  `temporal` set (no lowering by the numeric tick step); the minimum step is the axis's `tickMinStep` or, by default, the
  duration of the smallest part of the time unit (`TimeUnitSpec.DurationMilliseconds`, null for a unit whose smallest
  part is the day), so a `yearmonth` axis never has more than one tick per 31 days. A `tickCount` that is an interval
  name or `{interval, step}` (`TimeLabels.IntervalRequest`) puts a tick at every boundary of that interval (every
  `step`-th); explicit `values` are read with `TimeLabels.ReadInstant` (a number is milliseconds, a text is read like
  `Date.parse` in the calendar of the scale, an object is a date-time object; entries that are not dates are dropped),
  validated by the same `ValidTicks` rule as numbers (in range, in range order, thinned to the count unless an interval
  is given). The number of ticks is checked against `MaxTicks` before any label is made (the interval range stops at the
  first tick beyond the limit; the failure is located at the channel, as for numbers), and each tick polls the
  cancellation token. The domain value of a tick is a date.
- **Time labels** (Vega-Lite's `format` rules, plan decision P8-D7, made by `TimeLabels.Create`): the labels are times on
  a time or UTC scale, on a discrete scale over the dates of a time unit, and on any axis whose `formatType` is `time`
  or `utc` (also a quantitative one: its numbers are read as milliseconds, in the calendar of the chart, the time zone of
  the render options, for `time` and in UTC for `utc`, because a quantitative scale has no calendar of its own;
  `AxisBuilder` receives the calendar of the chart as its `chartCalendar` argument, from the evaluation of the call). In
  order: (1) an `axis.format` **string** is a d3 time format, in the calendar of the scale (UTC for a
  `formatType` of `utc`); (2) an `axis.format` **object** is Vega's multi-format with its members as the overrides of
  the levels (`milliseconds`, `seconds`, `minutes`, `hours`, `date` or `day`, `week`, `month`, `quarter`, `year`); (3)
  without a format and with a time unit, the specifier of the unit (`%b %Y` for `yearmonth`, so `Jan 2024`); (4)
  otherwise Vega's default multi-format, which labels an instant by the coarsest boundary it lies on (`2024`, `March`,
  `Mar 03`, `Tue 05`, `01 PM`, `01:45`, `:30`, `.250`). **Axes do not use `config.timeFormat`** (that is the default of
  legends). A `formatType` of `number` formats the millisecond value as a number with the d3 number format (an invalid
  specifier is a located `VegaLiteSpecException`; a time format fails only when it is longer than 1,024 characters or
  expands to more than 4,096 pieces, a located `VegaLiteSpecException` too, as d3 copies the directives it does not
  know). All names are the English d3 locale and all digits are ASCII, so a label never depends on the culture of the
  host, and an instant that is not a valid date is labeled `Invalid Date`.
- **Label expressions** (`ApplyLabelExpression`, after `BuildTicks` and before the layout): the `labelExpr` of the
  axis object, else of the axis configuration chain (`axisX` or `axisY`, `axisBottom`, `axisTop`, `axisLeft`,
  `axisRight`, `axisBand`, `axisPoint`, `axisQuantitative`, `axisTemporal`, then `axis`: the precedence of every
  other property) replaces the label of each tick, so the extent of the axis, `labelLimit`, and the overlap removal
  see the replaced text. The datum of a tick is `LabelExpressions.Datum(tick.Value, tick.Label, i / max(n - 1, 1),
  i)`: `datum.value` (a date for a time scale), `datum.label` (the text the label would have), `datum.index` (a
  fraction from 0 to 1) and `datum.tickIndex` (a whole number). An array result sets `AxisTick.Lines` and the label
  is drawn on several lines. An axis with `labels: false` does not evaluate (or compile) its expression. The three
  layout passes of the pipeline build the axis again, so the labels are evaluated again each time; the cost is bounded
  by `MaxTicks` times the steps of the expression, charged to the shared `MaxEvaluationSteps` budget.
- Label overlap: Vega's parity method (every second label dropped until none overlap, taking `labelSeparation` into
  account) or its greedy method, performed by the `LabelOverlap` unit that `AxisBuilder` shares with `LegendBuilder`
  (see _LabelOverlap Unit Design_); dropped labels remain in `AxisResult.Ticks`.
- `GetExtents(axes, dataWidth, dataHeight)` (static) - the union of the axes' bounds with the data rectangle, as the
  `ViewExtents` (how far everything reaches beyond each side, never negative), for `ViewLayout`.

**Preconditions and postconditions**: the request carries a scale and a model of the final data rectangle; the result is
immutable scene data.

#### Error Handling

An `axis.orient` that is not valid for the channel and an `axis.format` that is not a valid d3 specifier (a number format,
including `formatType: "number"` on a time axis) are
`VegaLiteSpecException`s located at the offending value (the `FormatException` of `NumberFormat` is converted); a time
format string fails only when it is longer than 1,024 characters or expands to more than 4,096 pieces (also a
`VegaLiteSpecException`, found by `SpecParser` when the document is opened and, for a configured format, by
`TimeLabels.Create`). An axis
with more than `MaxTicks` ticks, categories and dates included, is a `VegaLiteRenderException` located at the channel
(the exception
of `Ticks` is rethrown with the location; the count is checked before any label is made). A time label longer than
`MaxStringLength` (the formatter stops as soon as it would write more, so a huge specifier cannot allocate more), or
time labels that are together longer than `MaxStringLength` (the label function of the axis keeps a running total and
gives the formatter what is left, so the labels of one axis are charged before they are made), is a
`VegaLiteRenderException` located at the channel whose message names `VegaLiteLimits.MaxStringLength`. A label
expression that exceeds the step budget or the string limit is a `VegaLiteRenderException` located at the expression;
a text of the axis configuration that is not an expression is a `VegaLiteSpecException` located at the channel (an
expression in the specification was already compiled by `SpecParser` when the document was opened). A canceled token
is an `OperationCanceledException`. A null request, measurer,
or configuration is `ArgumentNullException`. A `values` array that is not made of numbers, strings, or booleans is
ignored (a time axis reads dates; an entry that is not a date is dropped). The unit performs no I/O and never reads the
host clock, zone, or culture.

#### Dependencies

- **`TextMeasurer`** - label and title measurement, in the `TextMeasurer` unit
- **`TimeLabels`** - the time label function, the interval `tickCount`, the default-count rule, and the reading of `values`
  of dates (see _Time Labels Design_)
- **`LabelExpressions`** and **`LabelText`** - the evaluation of the `labelExpr` of the axis (see _LabelExpressions Unit
  Design_); `RowExpressions` of the `Expressions` subsystem carries the evaluator of the call
- **`ScaleModel`**, **`ResolvedScale`**, **`Ticks`**, and **`NumberFormat`** - the `Scales` subsystem
- **`BinKind`** and **`StackOffset`** - the bin kind and the stack offset that a `ResolvedScale` carries (the `Transforms`
  subsystem)
- **`DataValue`**, **`TimeCalendar`**, **`TimeUnitSpec`**, **`DateTimeObject`**, and **`JsDateParser`** - the `Data`
  subsystem (the calendar of the scale and the reading of date `values`; see _Time Calendar Design_ and _Time Format
  Design_)
- **`SceneGroup`**, **`SceneRule`**, **`SceneText`**, **`SceneItem`**, and **`SceneBounds`** - the `SceneModel` unit
- **`ResolvedConfig`** and **`SpecNode`** - the `SpecModel` subsystem
- **`ViewExtents`** - the `Layout` subsystem
- **`VegaLiteLimits`**, **`VegaLiteLocation`**, **`VegaLiteSpecException`**, and **`VegaLiteRenderException`** - the public
  types of the `VegaLiteDocument` unit
- **.NET base class library** - `System.Globalization`, `System.Collections.Generic`

#### Callers

`VegaLiteDocument` builds the axes of the x and y channels (passing the expressions of the call) in every layout pass
(one axis for each `AxisPlan` of a layered view, see _VegaLiteDocument Unit Design_) and passes the results to
`ViewLayout` (through `GetExtents`) and into
the scene content (grids first, then the axes behind the marks, then the axes in front). The facet pipeline (a later
work package) builds the axes that the cells share `InHeader` and gives them to `TitleBuilder.BuildHeader`. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Guides/AxisBuilderTests.cs`, `AxisBuilderTimeTests.cs` and
`AxisBuilderHeaderTests.cs` (and `AxisBuilderLabelExprTests.cs` for `labelExpr`) call the builder directly through `InternalsVisibleTo`.
