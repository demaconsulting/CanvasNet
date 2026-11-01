### ScaleResolver

![Scales Structure](VegaLiteScalesView.svg)

The `ScaleResolver` unit is the second software unit in the `Scales` subsystem. It comprises six source files in the
`DemaConsulting.CanvasNet.VegaLite.Scales` namespace, all `internal`, that define four `internal` types:
`ScaleResolver` (a `static partial` class), `ResolvedScales` (sealed class), `ResolvedScale` (sealed class), and
`RangeEnd` (readonly record struct):

- `ScaleResolver.cs` - the entry points, the channel resolution (kind, continuous and discrete scales, the zero, nice, padding,
  and range rules), and the four types
- `ScaleResolver.Plan.cs` - the domains that follow from the encoding plan: binned, stacked, ranged, and unaggregated
- `ScaleResolver.Sort.cs` - the order of the categories of a discrete scale (every `sort` form, including the sort by an
  aggregate)
- `ScaleResolver.Color.cs` - the colors of the color, fill, and stroke scales (the scale's `range`, its `scheme`, the named
  ranges of `config.range`, the interpolators of continuous scales, `interpolate`) and the domain of a quantile scale
- `ScaleResolver.Range.cs` - the output values of every non-position channel (numbers, shapes, dash patterns), the named
  and
  explicit ranges, the counts of discretizing scales, and the limit check of a specification array
- `ScaleResolver.Time.cs` - the scales of temporal fields and of fields with a time unit: the time facts of a channel
  (calendar, unit, temporal or not), the reading of explicit temporal domains, domain bounds, and datums, the
  conversion of one entry to milliseconds, and the `nice` request of a time scale

The files carry the provenance header comment for the Vega-Lite and Vega rules and the d3-scale and d3-time
behavior they port.

#### Purpose

`ScaleResolver` infers, for each encoding channel of a single view, the scale that Vega-Lite would build: its kind, its
domain, its range, its padding, and whether it is reversed, rounded, clamped, or extended by nice. It exists so the rules
of Vega-Lite's scale inference (type by field type, channel, and mark; the zero, nice, and padding rules; default ranges;
step sizing of discrete views) live in one place, separate from the pixel arithmetic of `ScaleModel` and from the
guides and marks that consume the result.

The unit's behavior is specified by the `CanvasNetVegaLite-Scales-ScaleResolver-*` requirements in
`docs/reqstream/canvas-net-vega-lite/scales/scale-resolver.yaml`.

#### Data Model

**`ResolvedScales`** holds the `MarkType`, the inferred `Orient` (horizontal, vertical, or null), the `Plan` (the
`EncodingPlan` the scales were resolved with), the scales by channel
(an indexer by channel name and an enumeration in the fixed channel order x, y, theta, radius, color, fill, stroke, size,
opacity, shape,
strokeWidth, strokeDash, fillOpacity, strokeOpacity, angle), and
the `DiscreteWidth` and `DiscreteHeight` that a step-driven discrete position scale needs.

**`ResolvedScale`** (immutable) describes one channel before the size of the data rectangle is known: `Channel`, `Kind`,
`FieldPath` (the field the marks read, which the encoding plan names: the derived field of a bin or an aggregate, or the
upper end of a stack; null for a `datum`), `Field2Path` (the lower end of a stack, the end of a bin, the end of a time
unit, or the field of `x2` or `y2`), `Title` (the default title of the plan), `Bin` (a `BinKind`) and `BinSpec` (the
bins of a continuous channel that
the encoding bins), `Stack` (the `StackOffset` of the stacked channel, or null), `DataType` (`quantitative`,
`temporal`, `nominal`, or `ordinal`), `Location` (the channel
definition), the continuous `DomainStart` and
`DomainEnd` (after the zero rule, before pixel padding and nice) and `Domain` (all the entries when the domain has more
than two: a piecewise linear domain, the three entries of a diverging scale, the sorted values of a quantile scale, the
thresholds of a threshold scale), `NiceCount`, `NiceInterval`, `PaddingPixels`, `Clamp`, `Round`,
`Reverse`, the `DiscreteDomain`, `PaddingInner`, `PaddingOuter`, `Align`, `Step`, the `ExplicitRange` (a list of
`RangeEnd` values, each a number or the keyword `width` or `height`), `RangeMin` and `RangeMax`, the `ValueRange` of a
linear, band, or point scale of a numeric channel, the `NumberRange` of an ordinal or discretizing numeric scale, the
`ColorRange` of an ordinal or discretizing color scale, the `ShapeRange` and the `DashRange`, the `Interpolator` of a
sequential or diverging scale,
`IsPosition` (`x` or `y`), `IsPolar` (`theta` or `radius`), and `NaturalExtent` (the step times the band space of a
step-driven
discrete scale). The time facts of the channel are `Calendar` (the `TimeCalendar` of its instants: UTC for a `utc` scale
or a `utc` time unit or a color that asked for the scale type `utc`, otherwise the zone of the chart;
null when the field is neither temporal nor has a time unit),
`IsUtcTime` (the scale follows the UTC rules: kind `Utc`, a `utc` unit, or the `utc` type of a color channel, which the
`Sequential` kind does not show),
`TimeUnit` (the `TimeUnitSpec` of the field, the resolved one for the `maxbins` form, or null), `IsTemporalField` (the
values of the field are instants: it is temporal or has a time unit), and `HasBandEnd` (the channel spans its time
unit: `Field2Path` is the end of the unit, Vega-Lite's `hasBandEnd`, so a bar, rect, or tick covers the unit and a point
can sit anywhere between its start and its end; false for the range that an `x2` or `y2` gives). `NiceInterval` is the
`TimeTickRequest` (an interval name with an optional step) that `nice` extends a time scale to, null when `nice` uses
`NiceCount` or is off. `CreateModel(extent)` builds the `ScaleModel` for a data-rectangle width (x) or height (y), or,
for `radius`, for the
largest radius that the data rectangle allows (the caller passes half of its smaller side); the extent is ignored for
every other channel (also for `theta`, which always sweeps the full circle).

**Inference rules** (Vega-Lite 6 behavior, ported with attribution):

- **Type.** `ChooseKind` is Vega-Lite's `defaultType` with its `channelSupportScaleType` and `scaleTypeSupportDataType`
  checks.
  Quantitative position channels are linear; discrete position channels are band scales for `rect`, `bar`,
  `image`, `rule`, and `tick` marks and point scales for the others; **the polar channels `theta` and `radius`** (the
  channel group `ChannelGroup.Polar`) take the same scale types as a position channel (linear, pow, sqrt, symlog, log,
  band, point, time, and UTC) and default to linear for a quantitative field, to a **band** scale for a nominal or
  ordinal field of an `arc` (a slice or ring is a rectangle in polar coordinates) and to a **point** scale for one of a
  `text` mark; a quantitative `color`, `fill`, or `stroke` is a **sequential**
  scale (a **diverging** one with three domain entries); a nominal or ordinal one is ordinal, as are `shape` and
  `strokeDash` (also for a quantitative field, which Vega-Lite warns about); `size`, `opacity`, `strokeWidth`,
  `fillOpacity`, `strokeOpacity`, and `angle` are
  linear for a quantitative field and point scales otherwise; a quantitative `color`, `fill`, or `stroke` field that the
  encoding **bins** is a **bin-ordinal** scale instead (Vega-Lite's `defaultType`), whose domain is the edges of the bins
  (one more than the bins) and whose range is one color per bin (`ColorSchemes` with `SchemeUse.BinOrdinal`: the ramp
  sampled once per bin), while the other channels keep their scale over the start of the bin. A **temporal** field
  (also the derived start of a time unit) gets Vega-Lite's default type: a **time** scale on the position, `size`,
  `opacity`, `strokeWidth`, `fillOpacity`, `strokeOpacity`, and `angle` channels (a **utc** scale when its time unit is
  a `utc` unit), a **sequential** scale over the milliseconds on `color`, `fill`, and `stroke` (Vega-Lite's default
  there is
  a `time` scale with a color range, which is drawn as the sequential scale: the same colors, with ticks and labels of
  dates), and an ordinal scale on `shape` and `strokeDash`. A nominal or ordinal field with a time unit keeps the band,
  point, and ordinal scales, over the **date values** of the unit. An explicit `scale.type` of
  linear, log, pow, sqrt, symlog, time, utc, sequential (colors), quantile, quantize, threshold, bin-ordinal (a binned
  quantitative
  color only: Vega-Lite refuses it for every other field type, and it is what the default is there), band, point, or
  ordinal is used when
  both the channel and the field type accept it (the table below); otherwise the default applies (the feature catalog
  reports a replaced type as `vl-scale-type-ignored`).
  The log, pow, sqrt, and symlog types are the continuous-to-continuous scales with a transform
  (`ResolvedScale.Transform`, a `ScaleTransform`): a **log** scale with its `base` (default 10), a **pow** scale with its
  `exponent` (default 1), a **sqrt** scale as the power 0.5 whatever `exponent` says, and a **symlog** scale with its
  `constant` (default 1); the properties of another type are ignored. They are accepted on position, size, opacity,
  stroke width, angle, and color channels (a color scale becomes the sequential or diverging scale of the
transform) for a
  quantitative field, not on `shape` or `strokeDash`, and a nominal field falls back to the discrete default; a measure
  that carries one of them is never stacked (`StackRules` refuses the stack, as Vega-Lite does). `ChannelIsDrawn`
  gives a channel a scale
  only for the marks Vega-Lite's `supportMark` allows (the table below), `AcceptsScaleType` answers the type question
  for the feature catalog, and
  `IsKnownShape` tells it whether a shape is a symbol name that the painter draws (anything else is a custom path).

  <!-- markdownlint-disable MD013 -->
  | Channel | Drawn for |
  | ------- | --------- |
  | `theta`, `radius` | `arc`, `text` |
  | `theta2`, `radius2` | `arc` (they have no scale of their own; the feature catalog uses the table) |
  | `text` | `text` (it has no scale; the feature catalog uses the table) |
  | `shape` | `point` |
  | `angle` | `point`, `circle`, `square`, `text` |
  | `strokeDash` | every mark but `rect`, `area`, `trail`, `text`, `arc` |
  | `size` | every mark but `rect`, `area`, `arc`, `image` |
  | every other channel | every mark |
  <!-- markdownlint-enable MD013 -->

  A channel that a mark does not draw has no scale: `ResolveChannel` returns none for it, and the feature catalog
  reports it (see _FeatureCatalog_).
- **Domain.** The fields a channel reads come from the `EncodingPlan`. A linear domain is the extent of the valid values
  (numbers, booleans as 1 and 0, numeric strings; null and
  NaN are ignored; an empty extent is 0 to 1) of the **Main** table, over the channel's field and its second field: for
  a
  **stacked** channel the extent spans the lower and the upper ends (`[0, 1]` for `normalize`), for a **range** (`x` with
  `x2`) the fields of both channels, and for a field that is **pre-binned** (`bin: "binned"`) its start and its end (when
  there is no `x2`, a `bin.step` extends the last start by one step); a field that the encoding **bins** has the domain
  `[start of the first bin, stop of the last]` from the `BinSpec` that `EncodingTransforms` computed, **without zero and
  without nice** (also for pre-binned fields); a datum gives its own value. `scale.domain` of **`unaggregated`** takes
  the extent of the source field in the **Raw** table, for a channel that aggregates with `mean`, `average`, `median`,
  `q1`, `q3`, `min`, or `max` (Vega-Lite's shared-domain operations); for any other channel it is ignored and the normal
  domain applies. Then the `zero` rule pulls an end to zero (true for the measure axis of a
  bar (unless it is a range) and for quantitative sizes that are not quantized, the configured default otherwise, never
  for a binned field,
  and only
  when a custom domain already spans zero; never for quantile and threshold scales, never for a time or UTC scale or any
  temporal field (also the sequential scale of a temporal color: zero is not meaningful for dates), and never for a log
  scale, whose domain
  cannot reach zero, even when `zero: true` is given), then `domainMin` and `domainMax`
  replace the first and the last entry (sequential and linear scales), and `domainMid` (colors) inserts the middle value
  before the first entry, after the last, or between the ends, as Vega's `configureDomain` does. **A log scale refuses a
  domain that includes zero or crosses it**: Vega only warns and then draws nothing (every position is NaN), while the
  resolver throws a located error that names the domain and suggests a symlog scale or leaving the zero and negative
  values out - a `VegaLiteSpecException` at `scale.domain` for an explicit domain, a `VegaLiteRenderException` at the
  channel for the domain of the data or after `domainMin`/`domainMax` (a domain below zero is fine: the scale is reflected).
  A log scale without any valid value gets one decade, 1 to 10, instead of the default 0 to 1. A quantize scale has
  the extent of the values; a **quantile** scale all the valid values of the field in ascending order (read with the
  token polled and charged to the step budget, see _Limits_); a **threshold** scale the
  explicit thresholds or, without a domain, the extent as two thresholds. A color scale whose domain
  has three entries is diverging; one with four or more is a specification error (a piecewise color scale is not
  supported). A
  discrete domain is the distinct values of the Main table in order of first appearance, ordered by the `sort` of the
  channel (see _Sort forms_; ascending by default); an explicit `scale.domain` is used as given; a nominal or ordinal
  channel that is binned has the **text of its bins** (the boundaries formatted with the axis `format` or
  `config.numberFormat` and joined with an en dash, `null` for an invalid value) as categories, ordered by where the bins
  start. An explicit
  quantitative domain keeps **all** its numbers (`ResolvedScale.Domain` when there are more than two): with an explicit
  range of more than two entries the scale is piecewise as in d3 (see _ScaleModel_), `zero`, pixel padding and `nice`
  change only the first and the last entry as Vega's `configureDomain` does (so `nice` changes the last entry of the
  complete domain, as d3's `nice` does), and a domain of three entries over the default two-entry range uses its first
  two entries only.
- **Polar channels.** `theta` and `radius` (drawn by the `arc` and `text` marks) are resolved like a position channel,
  with these differences. They have **no pixel padding, no `nice`, no step, and no axis**, and a band or point scale on
  them has no padding unless the scale gives one (inner and outer padding are 0). The domain of a stacked polar channel
  is the extent of its stack, the lower and the upper ends (`[0, 1]` for `normalize`), exactly as for a stacked x or
  y, so the `theta` of a pie spans zero to the sum; this comes from the generic extent (`LinearExtent`) and is pinned
  by a test. The **zero rule** for them is the configuration only: `config.scale.zero` (true by default) applies unless
  the domain is explicit (an explicit domain keeps zero only when it already spans it) or the channel is binned; the
  bar and area rules of x and y do not apply, because the marks that draw a polar channel are neither bars nor areas
  and have no dimension axis. A `theta2` or `radius2` has no scale. The **range** is the subject of the _Range_ item.
- **Temporal fields** (the partial file `ScaleResolver.Time.cs`). The values of a temporal field, and of
  the derived start of a time unit, are instants (`DataValue.Date`, milliseconds since the epoch), and the calendar of the
  scale is the zone of the chart (UTC for a `utc` scale or a `utc` unit), read from the evaluation facts of the render.
  `Accepts` (`channelSupportScaleType` and `scaleTypeSupportDataType` of Vega-Lite, pinned by a test) takes an explicit
  type when the channel and the field type both accept it:

  <!-- markdownlint-disable MD013 -->
  | Field type | Accepted explicit `scale.type` |
  | ---------- | ------------------------------ |
  | `temporal` | `time`, `utc` only (any other type, including `sequential`, `quantile`, `quantize`, and `threshold`, is replaced by the default) |
  | `quantitative` | linear, log, pow, sqrt, symlog, sequential, the discretizing types, bin-ordinal (binned colors); never `time` or `utc` |
  | `nominal`, `ordinal` | band, point, ordinal; never `time` or `utc` |
  <!-- markdownlint-enable MD013 -->

  A position, `size`, `opacity`, `strokeWidth`, or `angle` channel accepts the time and UTC kinds; a color channel
  maps `time` and `utc` to the sequential scale (as `linear` is for numbers); `shape` and `strokeDash` do not accept
  them. The **data domain** is the extent of the valid dates of the main table over the channel's field and its second
  field (so the end of a time unit that a bar spans, and the `x2` and `y2` of a range, count), **without** the zero
  rule, and an empty or unparsable field gets the default domain 0 to 1, as a linear scale does. An explicit
  `scale.domain` has entries that are timestamps, date texts (read by `JsDateParser` in the calendar of the scale), or
  date-time objects (`DateTimeObject`, in the calendar unless the object says `utc`); for a field with a **single local
  time unit** a number below 10,000 or a text that is not a date names a part of the unit, Vega-Lite's `valueExpr` rule.
  `domainMin` and `domainMax` take a timestamp or a date-time object, and `domainMid` of a temporal color a timestamp
  (Vega-Lite's `domainMid` is `number | ExprRef`). A temporal `datum` (a number or a date text) is a domain of one
  instant; a `datum` that is a date-time object stays reported. **`nice`** is off by default for a time scale (Vega-Lite
  gives time scales no default): `true` asks for ten ticks, a positive number for that many, and a name or
  `{interval, step}` for the boundaries of that interval (`TimeNiceRequest` gives `ResolvedScale.NiceCount` or
  `NiceInterval`); a temporal sequential or diverging color scale ignores `nice` (milliseconds are not nice numbers).
  **Padding**: a bar on a time axis gets no 5-pixel padding when its field has a time unit (the bar spans the unit, as for
  a bin), an explicit `padding` and `continuousPadding` apply as for a linear scale; `clamp`, `round`, `reverse`,
  `range`, `rangeMin`, `rangeMax`, and `sort: "descending"` behave as for a linear scale, and a y time scale runs
  upward. A **nominal or ordinal field with a time unit** has a discrete domain of the distinct date values ordered
  ascending by value by default (see _Sort forms_) and an explicit domain or sort array whose entries are converted with
  the unit rule to the same millisecond value as the floor of the unit.
- **Padding and nice.** A band scale has inner padding by mark from the configuration (bar 0.1, tick 0.25, others 0) and
  outer padding half of it; a point scale 0.5; `scale.padding` sets both. A continuous position scale gets nice with a
  count of 10 unless it has a custom domain **or is binned** (its bins already end on round values); a bar on its
  continuous axis (not a binned one) gets 5 pixels of padding. On a **log** scale `nice` floors and ceils the ends to
  powers of the base (`Ticks.LogNice`, the count plays no part), on pow, sqrt, and symlog scales it is the ordinary
  tick-based `nice` of the domain, and the pixel padding widens the domain around its middle **in the transformed
  domain** (so a log domain keeps its geometric middle). On a **time or UTC** scale `nice` extends the domain to the
  boundaries of a calendar interval with `Ticks.TimeNice` (after the pixel padding, like the numeric `nice`), in the
  calendar of the scale, and a non-finite or out-of-range result keeps the padded domain.
- **Range.** x is 0 to the width and y is the height to 0 (continuous) or 0 to the height (discrete). **`theta` is 0 to
  2 * PI** (radians; the extent is ignored) and **`radius` is 0 to
  the extent that the caller passes** (half of the smaller side of the data rectangle, in pixels); an explicit
  `range` (with the `width` and `height` keywords), `rangeMin`, `rangeMax`, and `reverse` apply to them as to x and y.
  Size ranges follow
  the mark (point-like marks 4 to the square of 0.95 steps, bar and tick 2 to step minus 1, rule, line, and trail 1 to 4
  as stroke widths, text 8 to 40 as font sizes); `strokeWidth`
  is `minStrokeWidth` to `maxStrokeWidth` (1 to 4); the opacities are 0.3 to 0.8; `angle` is 0 to 360; a discretizing
  **size** scale (quantile, quantize, threshold) spreads the numbers evenly over the range (Vega-Lite's
  `interpolateRange`) with one value per
  interval - `quantileCount` and `quantizeCount` (4), or one more than the thresholds - and the other numeric channels keep
  their two-entry range. `shape` takes `config.range.symbol` (eight names) and `strokeDash` five fixed patterns. A
  discrete position
  scale without an explicit range
  or a fixed size takes its size from `step` (20 pixels by default), which is what `DiscreteWidth` and `DiscreteHeight`
  report so that the layout can size the view. An explicit numeric range keeps all its entries for a linear scale (`reverse`
  flips the whole array, as Vega does); a band or point scale uses the first two entries, as d3's `scaleBand` does;
  `rangeMin` and `rangeMax` replace the first and second entry of a default two-entry range of a linear, band, or point
  scale (not of a step-sized one).
- **Colors** (`ScaleResolver.Color.cs`). The colors come, in Vega-Lite's order, from the scale's `range` (an array of
  colors, or a string naming an entry of `config.range`), then its `scheme` (a name, an array of colors, or
  `{name, count, extent}`), then the named range of `config.range` that `defaultRange` picks: `category` for a nominal
  field and `ordinal` for an ordinal one; `diverging` when the scale has a `domainMid`; `heatmap` for a `rect` mark;
  `ramp` otherwise. A named range is a list of colors or a scheme object (`scheme`, `count`, `extent`); one that the
  configuration lacks is a specification error for `scale.range` (Vega's "Unrecognized scale range value"), and Vega's
  own default is used when the configuration removed a default one. A scheme name is looked up in the 68 schemes of
  `ColorSchemes` ignoring case; an unknown name is a specification error ("Unrecognized scheme name", Vega's behavior).
  A **sequential or diverging** scale gets an interpolator over 0 to 1: a scheme through `ColorSchemes.Configure` (its `extent`
  and `reverse` included; `interpolate` does not apply to a named scheme, as in Vega) or a list of colors piecewise
  through a `ColorRamp` in the space `interpolate` names (`rgb` by default; `hsl`, `hsl-long`, `lab`, `hcl`, `hcl-long`;
  the object form adds a sRGB `gamma`; `cubehelix` is accepted and interpolates in sRGB). An **ordinal, quantile,
  quantize, threshold, or bin-ordinal** scale gets a list of colors: the list given, or what `ColorSchemes.Configure`
  picks (the whole list of a categorical scheme for an ordinal scale, otherwise the first colors or samples at
  `i / (count + 1)`; the count is the explicit `count`, the domain size for an ordinal scale, 5 for quantile and
  quantize, one more than the thresholds for a threshold scale). `reverse` (also set by `sort: "descending"` on a scale
  with a
  continuous domain) reverses the colors. A list of colors (an explicit or configured `range`, a configured scheme
  list, or a default named range) that is empty, or has an entry that is not a string or not a color the renderer can
  read, is a located `VegaLiteSpecException` (at the entry, at the list when empty; a configured default at the channel):
  the list is **not** replaced by the default range, in the lenient mode or the strict one, because that would hide the
  mistake and a color the painter cannot read must never be emitted. A configured scheme object whose `count` is not a
  positive whole number or whose `extent` is not two numbers is reported the same way (the parser does the same for a
  scheme of the specification).
- **Limits and cancellation.** A range, domain, or sort array of the specification is checked by its length against
  `MaxScaleDomainSize` before anything is built; a scheme `count` and a generated numeric range are checked by their
  count (the check of `ColorSchemes.Configure` is located at the scheme) and polled for cancellation every 4,096 values.
  The values of a **quantile** scale are counted against `TableLayout.MaxCells` (twice the number of rows: the values
  and the sort buffer) before the array is allocated, read with the token polled every 4,096 rows, charged to the
  evaluation-step budget through a `WorkMeter` (one step per 64 values read and per 64 comparisons of the sort,
  `n * ceil(log2 n)`), and sorted by a bottom-up merge sort that polls the token. Strings are never built from data
  here: colors are
  `#rrggbb` texts or texts the parser has already length-checked. The temporal reading adds no new limit: the explicit
  domain, `domainMin`/`domainMax`, and sort arrays go through the same length checks (spec **and** configuration
  arrays; the time scales add no configuration array) **before** any date is read, the data pass polls the token every
  4,096 rows, every conversion of a date text is constant work (`JsDateParser` rejects text longer than 128
  characters before scanning it), and the discrete date domain of a time-unit field is bounded by `MaxScaleDomainSize`
  like any discrete domain.
- **Sort forms** (`ScaleResolver.Sort.cs`). `"ascending"` and `"descending"` order the categories by value (a stable
  sort, null first, the order of `DataValue.Compare`); `null` keeps the data order; an array lists the categories (those
  it
  does not list come first). A sort array has at most `MaxScaleDomainSize` items (otherwise a `VegaLiteRenderException`
  located at the array that carries `the limit of` and `VegaLiteLimits.MaxScaleDomainSize`, which the tool maps to
  `budget-exceeded`), is read with the cancellation token polled every 4,096 items, and skips an object or array item
  (the parser rejects them; the resolver never lets one stand for the null category), except that for a temporal field
  or a field with a time unit a number, a date text, or a date-time object is converted with the unit rule to the date
  it names and so takes its place in the order (an entry that is not a date is skipped). For such a field the categories
  are **dates**, ordered by value (the order of `DataValue.Compare`) ascending by default and descending with
  `"descending"`, and the sort by a field or an encoding keeps working over them. The other forms order the
  categories by an **aggregate of a field over the rows of the Raw
  table** (the rows before the implicit aggregate, as Vega-Lite does, so a calculated, flattened, or looked-up
  field can be
  the sort field): an object `{op, field, order}` with any aggregate operation that yields a comparable value (every
  operation except `argmin`, `argmax`, `values`, and the exponential ones; default operation `min`, default field the
  field of the channel); and `{encoding, order}` and the shorthands `"y"` and `"-y"`, which order by another channel: the
  operation is that channel's aggregate, `sum` when it is the stacked channel, and `min` otherwise, over the field the
  channel reads. A category whose result is null (no valid value) sorts first in either direction; categories with the same
  result keep the order of first appearance, so the sort is stable and independent of the culture. The aggregates are
  computed with the accumulators of `AggregateTransforms` (`MeasureCell`, one per category), with the work of every row
  charged to the evaluation-step budget and the cancellation token polled. A discrete channel that shows the bins of a
  field is ordered by the start of its bins (descending with `"descending"`). `IsSupportedFieldSort` recognizes the
  `{op, field, order}` form; the `FeatureCatalog` decides what to report with `SpecParser.IsSupportedSort`.
- **Orientation.** `InferOrient` follows Vega-Lite: no orientation for point, circle, square, and rect marks; a **trail
  and an arc are always vertical** (Vega-Lite returns `vertical` for the marks that it has no rule for, which lets the
  path builder sort a trail by its pixel x); a bar is
  horizontal when only x is a measure and vertical when only y is, a tick the other way round, and a rule is vertical
  with only x and horizontal with only y. **An area with a `y2` is vertical** (horizontal when `y` is `bin: "binned"`)
  **and an area with only an `x2` is horizontal** (vertical when `x` is `bin: "binned"`); both rules come before the
  explicit `orient`, which wins in the other cases where Vega-Lite lets it. A line, area, or tick follows the measure
  like a bar does (x measure and y not: horizontal, a tick vertical; y measure and x not: vertical, a tick horizontal;
  two measures: vertical), and, **when neither axis is a measure, the time axis is the dimension: a temporal x next to
  a y that is not temporal is vertical and a temporal y next to an x that is not is horizontal**. This temporal
  fall-through is limited to the line, area, and tick (Vega-Lite applies it to a bar, a rule, and a text as well; those
  keep their present behavior). A count definition
  (an `aggregate` of `count` without a field) is a measure. The encoding plan and the feature catalog reuse it.

**Deviations** (also listed in _CanvasNetVegaLite System Design_): values of different kinds in one discrete domain
are ordered by kind; `scale.domain` forms other than an array and `"unaggregated"` are ignored, and
`"unaggregated"` applies only to the shared-domain operations; a property that the scale type or channel does not support
(`scheme` or `interpolate` on a non-color channel, `domainMin` on a quantize scale, and so on) is ignored without Vega-Lite's
console warning; a color scale with an explicit domain of four or more entries (a piecewise color scale) is a specification
error; an array of colors given as the `scheme` of a discrete scale is used as the whole list of colors; a sort by the
`argmin`, `argmax`, `values`, or exponential
operation is ignored (the catalog reports it); a `sort` array puts values
missing from it first; a bin is planned on every channel that has a scale, but a `bin-ordinal` scale is accepted for a binned
quantitative color only (a nominal or ordinal binned color keeps the ordinal scale of the text of its bins even when it
asks for `bin-ordinal`, and a field that is not binned ignores the type). A
`sort` array is read once into a
dictionary from each listed value to its first position (a value listed twice counts at its first position), so
ordering a domain costs one lookup per value rather than a search of the list per value.

The temporal rules deviate from Vega-Lite or from the plan of this phase in these ways: (1) a temporal field accepts
only the `time` and `utc` scale types (Vega-Lite 6's `scaleTypeSupportDataType`), so a `sequential`, `quantile`,
`quantize`, or `threshold` type on it is replaced by the default and reported as `vl-scale-type-ignored` (an earlier
plan allowed the discretizing types); (2) the default type of a temporal color is a `time` scale in Vega-Lite and is
drawn as the sequential scale over the instants (the same colors, with the date ticks and labels of the legend); (3)
`domainMid` takes a timestamp, not a date-time object, as in Vega-Lite; (4) a sort array of date-time objects is valid
only for a field of type `temporal` (which gets a time scale), so a discrete scale converts numbers, texts, and objects
with the unit rule but in practice only meets numbers and texts; (5) the calendar for
the entries of an explicit domain and for a `datum` is the scale's (UTC for a `utc` scale or unit, otherwise the zone),
where Vega-Lite always writes `datetime(...)` in local time; (6) a step of `{interval, step}` that gives no interval
(below one, or negative) falls back to the default of about ten ticks, and a `nice` or `tickCount` number that is not
positive gives none; (7) `utc`-prefixed interval names use the UTC calendar on a zone scale (Vega would report an
error); (8) a degenerate domain gives its single instant as one tick, and an empty or unparsable temporal field gets
the default domain 0 to 1; (9) `datum` with a date-time object is not implemented (`vl-encoding-datum-datetime`),
while a `datum` of a number or a date text is; (10) a new warning `vl-scale-zero-ignored` reports `scale.zero` on a
temporal field with a time or UTC scale, which Vega-Lite drops, and (like Vega-Lite's drop) the resolver ignores the
`zero` of a temporal sequential color too, so the warning, which also covers the color channels, is true; (11) an
explicit `utc` type on a temporal color keeps its meaning although the scale is the sequential one (Vega-Lite's `utc`
color scale): the scale uses the UTC calendar and `ResolvedScale.IsUtcTime` is set, so the legend takes the whole-day
ticks counted from the epoch and UTC labels whatever the zone of the chart is.

**The `ScaleKind.Linear` audit.** `ScaleKind.Time` and `ScaleKind.Utc` are new enum members, so each of the 31 source
sites that tested for `ScaleKind.Linear` was reviewed and decided deliberately:

<!-- markdownlint-disable MD013 -->
| Site | Decision |
| ---- | -------- |
| `ScaleModel` `Map`, `Invert`, and `Map(DataValue)` | accept `IsLinearLike` (`Linear`, `Time`, `Utc`) |
| `CreateModel` | a new `Time`/`Utc` case: `ScaleModel.CreateTime` with the calendar |
| `ResolveRange` (a y position runs upward) | `Linear`, `Time`, `Utc` |
| `ChooseKind` and `TryParseKind` | a temporal branch; `time` and `utc` added; log, pow, symlog unchanged |
| the three `Accepts` conditions | position and numeric channels accept `Time`/`Utc`; the type check is per field type |
| `continuousToContinuous`, `supportsRangeLimits`, `Round` | include `Time`/`Utc` |
| `NiceRule` | the time kinds have their own rule (`TimeNiceRequest`); a temporal sequential or diverging ignores `nice` |
| `ScaleResolver.Range.cs` (`NumericOutputs`, `ReverseOf`) | `Time`/`Utc` added |
| `MarkBuilder` stack and zero-or-min sites | unchanged: a time axis is not stacked or the measure axis |
| `MarkBuilder` bin-end site | generalized to band ends (a bin or a time unit; see _MarkBuilder Unit Design_) |
| `MarkBuilder` continuous-domain, range, and bar-thickness sites | `Time`/`Utc` added |
| `AxisBuilder` tick path and default-grid sites | changed to route time scales to time ticks (see _AxisBuilder Unit Design_) |
| `LegendBuilder` symbol and gradient sites | changed for temporal legends (see _LegendBuilder Unit Design_) |
| 23 existing test sites | re-read: each asserts the scale of a quantitative field, so all stay; temporal parallels are in the new tests |
<!-- markdownlint-enable MD013 -->

A conditional channel resolves its scale from the **field definition** that the plan names (`ChannelPlan.FieldDef`): its
own definition when that holds a field, else the first condition on a test that does. The type, the scale options, the
domain, and the title of the scale come from that object, so a color given by `{"condition": {"test": ..., "field": "g",
"type": "nominal"}, "value": "gray"}` has the ordinal scale of `g`, while conditions that hold only values (or a
parameter) resolve no scale.

#### Key Methods

- `Resolve(view, table, config, sourceName, limits, token)` - the convenience entry for callers that have only the rows
  after the explicit transforms (the tests): it requires non-null view, table, and configuration, builds the
  `EncodingPlan`, runs `EncodingTransforms.ApplyStandalone` (a private context with an empty row budget), and calls the
  pipeline entry.
- `Resolve(view, plan, data, config, sourceName, limits, token)` - the pipeline entry: reads the mark and its orient from
  the plan, returns an empty result for a view without a mark or an encoding, builds the shared context (plan, the Raw and
  Main tables, configuration, mark, orient, encoding, limits, token, and the width and height steps), and resolves each
  present channel in the fixed order, reading the field of each from its `ChannelPlan`. The data is scanned with the token
  polled every 4,096 rows.
- `ChooseKind`, `ChannelIsDrawn`, and `AcceptsScaleType` - the scale-type rules above (`ChooseKind` also takes whether
  the field has a `utc` unit, for the default of a temporal position); `BuildContinuous` builds the linear, time, UTC,
  sequential, diverging, quantize, quantile, and threshold scales (domain, zero, `domainMin`, `domainMax`, `domainMid`,
  nice, clamp; the temporal domain forms come from `ScaleResolver.Time.cs`), `BuildDiscrete` the band, point, and
  ordinal ones, and `BuildBinOrdinal` the bin-ordinal scale (the edges
  of the bins from `EncodingData.Bins` as the domain - `sequence(start, stop + step / 2, step)`, one more than the bins,
  as
  Vega builds them from the `bins` of the scale - and one color per bin; no edge and no color for a field without a
  finite value; the bins and the work were bounded by `EncodingTransforms`); both ask `ResolveOutputs` for the output values
  of the channel, by its group
  (position, color, shape and dash, numeric).
- `InferOrient(markType, specified, encoding)` - the orientation rule above; a measure is an unbinned quantitative field
  or a count.
- `ReadTimeUnit` and `MakeTimeFacts` (`ScaleResolver.Time.cs`) - find the time unit of a channel (from the plan; the
  resolved unit of `EncodingData.TimeUnits` for the `maxbins` form) and, once the kind is known, build the private
  `TimeFacts(Calendar, Unit, IsTemporal, IsUtc)` record: UTC (and `IsUtc`) for a `Utc` scale, a `utc` unit, or an
  accepted explicit `utc` type of a color (`ChooseKind` reports it through its `utcRequested` output, since the kind of
  a color is `Sequential`), otherwise the calendar of the
  evaluation facts; null for a field that is neither temporal nor has a unit.
- `ReadTemporalDomain`, `ToMilliseconds`, `ReadDomainBound`, and `ReadTemporalDatum` - read an explicit temporal domain
  (at least two entries, each a date; otherwise a located `VegaLiteSpecException`), convert one number, text, or
  date-time object entry to milliseconds (NaN when it is not a date), read `domainMin`, `domainMax`, and `domainMid`
  (a timestamp is milliseconds as it is; an object is a date-time), and read a temporal `datum`; `ToDataValue(node,
  time)` turns the same entries into `DataValue.Date` categories for the discrete scales and sort arrays.
- `TimeNiceRequest(scaleDef)` - the `nice` request of a time scale: `true` is ten ticks, a positive number that count, a
  known interval name or `{interval, step}` an interval request through `TimeTickRequest.TryFromInterval`; `false`, an
  unknown name, and a bad number are no `nice`.
- `ResolvedScale.CreateModel(extent)` - applies what depends on the extent: the range of a position scale (including an
  explicit range with `width` and `height` keywords, all of whose entries are kept for a linear scale, and `rangeMin`
  and `rangeMax`) and of a polar scale (`theta`: 0 to 2 * PI whatever the extent; `radius`: 0 to the extent; the same
  explicit range, `rangeMin`, `rangeMax`, and `reverse`), the pixel
  padding of a continuous scale (the domain is widened so that the extreme values keep that distance from the edge),
  then `nice` (after the padding, as Vega does; `Ticks.TimeNice` with the calendar for a time scale), and builds a
  linear, time (`ScaleModel.CreateTime`, with the calendar and the UTC flag), UTC, sequential, diverging, quantize,
  quantile, threshold, bin-ordinal,
  band, point, or ordinal `ScaleModel` (the discretizing scales from the output range of the channel); a non-finite
  domain after nice keeps the padded domain.
- `ResolvedScale.NaturalExtent` - `step * BandSpace(count, inner, outer)`, the size a step-driven discrete scale needs.
- `LinearExtent` (`ScaleResolver.Plan.cs`) - finds the domain of a continuous channel before the zero rule:
  the bins of a
  binned field, `[0, 1]` for a normalized stack, the Raw extent for `unaggregated`, otherwise the extent of the plan's
  fields in Main; `ExtentOfFields` scans the columns with the token polled.
- `IsSupportedFieldSort(sort)` (static) - whether a `sort` object is a sort by the aggregate of a field that the resolver
  can compute (see _Inference rules_). `SortDomain` applies the sort forms to a discrete domain, and `OrderByAggregate`
  feeds the rows of the Raw table to one `MeasureCell` per category (bounded by `MaxScaleDomainSize`; an operation that
  keeps values is bounded by the cell limit) and orders the categories by the results with a stable sort.

**Preconditions and postconditions**: the view was validated by `SpecParser`; on return every present, supported channel
has a resolved scale, and the result is immutable.

#### Error Handling

A `scale.domain` or `scale.range` of the wrong shape (a range that is not an array of numbers or the keywords, with fewer
than two values; a quantitative domain with fewer than two numbers or with a non-number; a shape range that is not a
list of strings; a dash range that is not a list of lists of numbers; a color that cannot be read; a color domain of
four or more entries;
an unknown scheme name or an unknown named range; a temporal domain with fewer than two entries or with an entry that is
not a date: a timestamp, a date text, or a date-time object) is a `VegaLiteSpecException`
located at the offending value (a name that comes from `config.range` is located at the channel). A color range that is
empty or has an entry that is not a color is the same error, never a fallback to the default range. A log scale whose domain
includes zero or crosses it is a `VegaLiteSpecException` at `scale.domain` (an explicit domain) or a
`VegaLiteRenderException` at the channel (the domain of the data). A discrete domain with
more than `MaxScaleDomainSize` values, a specification array (domain, range) or an array of the configuration (a named
or default range of `config.range`, a `symbol` list, a configured scheme list, a dash pattern) of more entries than that,
a scheme `count`
or
a generated range (`quantileCount`, `quantizeCount`) above it, is a
`VegaLiteRenderException` located at the channel or the array whose message carries `the limit of` and
`VegaLiteLimits.MaxScaleDomainSize`; so is the work of a sort by an aggregate or of the values of a quantile scale that
exceeds the
step budget or the
value-state or cell limit (the failure of the accumulators and of the work meter is converted with the same message and
located at the channel
definition). A canceled token is an `OperationCanceledException`. A null argument is
`ArgumentNullException`, which is not converted. Constructs outside the supported subset are never errors here: they are
reported by the `FeatureCatalog`, and the resolver falls back or ignores them as listed above.

#### Dependencies

- **`ScaleModel`** (including its `ScaleTransform`), **`Ticks`** (the tick-based `nice` rule, `LogNice`, `IsLogDomain`,
  `TimeNice`, and `TimeTickRequest`),
  **`ColorSchemes`**, **`ColorInterpolator`**, and **`ColorRamp`** - the models, the
  nice rules,
  the scheme catalog and its range configuration, and the color interpolation
- **`CssColor`** (core `CanvasNet`, `Canvas` subsystem) - the reading of the colors of a list of colors
- **`EncodingPlan`**, **`EncodingData`**, **`ChannelPlan`**, **`BinSpec`**, and **`StackOffset`** - the plan and the tables
  of the implicit pipeline (the `EncodingPlan` unit), which also reuses `InferOrient` of this unit
- **`MeasureCell`**, **`MeasureEnvironment`**, **`WorkMeter`**, **`AggregateMeasures`**, and **`TableLayout`** - the
  accumulators and limits of the `AggregateTransforms` and `RowTransforms` units, for the sort by an aggregate
- **`DataTable`** and **`DataValue`** - the data and values of the `Data` subsystem
- **`TimeCalendar`**, **`TimeUnitSpec`**, **`DateTimeObject`**, and **`JsDateParser`** - the calendar, the time unit of
  a field, the date-time objects, and the date-text reading of the `Data` subsystem (see _Time Calendar Design_ and
  _Time Format Design_)
- **`SpecNode`**, **`SpecParser`** (`TryParseSortShorthand`), and **`ResolvedConfig`** - the tree and the
  configuration of
  the `SpecModel` subsystem
- **`VegaLiteLimits`**, **`VegaLiteLocation`**, **`VegaLiteSpecException`**, and **`VegaLiteRenderException`** - the public
  types of the `VegaLiteDocument` unit
- **.NET base class library** - `System`, `System.Collections.Generic`, `System.Globalization`

#### Callers

`VegaLiteDocument` calls the pipeline `Resolve` once per render, with the plan and the tables that `EncodingTransforms`
produced. `FeatureCatalog` calls `InferOrient` (to decide the stack of the view) and `ChannelIsDrawn` (to report a channel
that the mark does not draw). `AxisBuilder` and
`MarkBuilder` read the `ResolvedScale` (field path, second field, title, bin and stack facts, kind, domain, padding) and
the models built from it (the calendar, unit, temporal flag, band-end flag, and nice interval of a temporal channel
included); `MarkBuilder` builds the polar models with the radius extent `min(width, height) / 2`. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Scales/ScaleResolverTests.cs`, `ScaleResolverColorTests.cs`,
`ScaleResolverTimeTests.cs`, `ScaleResolverPolarTests.cs` (the polar scales and the orientation rules), and
`MarkChannelGatingTests.cs` (the support table of `ChannelIsDrawn`) call the
resolver directly through
`InternalsVisibleTo`.
