### ScaleResolver

![Scales Structure](VegaLiteScalesView.svg)

The `ScaleResolver` unit is the second software unit in the `Scales` subsystem. It comprises five source files in the
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

The files carry the provenance header comment for the Vega-Lite and Vega rules and the d3-scale
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
(an indexer by channel name and an enumeration in the fixed channel order x, y, color, fill, stroke, size, opacity, shape,
strokeWidth, strokeDash, fillOpacity, strokeOpacity, angle), and
the `DiscreteWidth` and `DiscreteHeight` that a step-driven discrete position scale needs.

**`ResolvedScale`** (immutable) describes one channel before the size of the data rectangle is known: `Channel`, `Kind`,
`FieldPath` (the field the marks read, which the encoding plan names: the derived field of a bin or an aggregate, or the
upper end of a stack; null for a `datum`), `Field2Path` (the lower end of a stack, the end of a bin, or the field of `x2`
or `y2`), `Title` (the default title of the plan), `Bin` (a `BinKind`) and `BinSpec` (the bins of a continuous channel that
the encoding bins), `Stack` (the `StackOffset` of the stacked channel, or null), `DataType`, `Location` (the channel
definition), the continuous `DomainStart` and
`DomainEnd` (after the zero rule, before pixel padding and nice) and `Domain` (all the entries when the domain has more
than two: a piecewise linear domain, the three entries of a diverging scale, the sorted values of a quantile scale, the
thresholds of a threshold scale), `NiceCount`, `PaddingPixels`, `Clamp`, `Round`,
`Reverse`, the `DiscreteDomain`, `PaddingInner`, `PaddingOuter`, `Align`, `Step`, the `ExplicitRange` (a list of
`RangeEnd` values, each a number or the keyword `width` or `height`), `RangeMin` and `RangeMax`, the `ValueRange` of a
linear, band, or point scale of a numeric channel, the `NumberRange` of an ordinal or discretizing numeric scale, the
`ColorRange` of an ordinal or discretizing color scale, the `ShapeRange` and the `DashRange`, the `Interpolator` of a
sequential or diverging scale,
`IsPosition`, and `NaturalExtent` (the step times the band space of a step-driven
discrete scale). `CreateModel(extent)` builds the `ScaleModel` for a data-rectangle width (x) or height (y); the extent
is ignored for every channel but x and y.

**Inference rules** (Vega-Lite 6 behavior, ported with attribution):

- **Type.** `ChooseKind` is Vega-Lite's `defaultType` with its `channelSupportScaleType` and `scaleTypeSupportDataType`
  checks.
  Quantitative position channels are linear; discrete position channels are band scales for `rect`, `bar`,
  `image`, `rule`, and `tick` marks and point scales for the others; a quantitative `color`, `fill`, or `stroke` is a **sequential**
  scale (a **diverging** one with three domain entries); a nominal or ordinal one is ordinal, as are `shape` and
  `strokeDash` (also for a quantitative field, which Vega-Lite warns about); `size`, `opacity`, `strokeWidth`,
  `fillOpacity`, `strokeOpacity`, and `angle` are
  linear for a quantitative field and point scales otherwise. A temporal field has no scale. An explicit `scale.type` of
  linear, sequential (colors), quantile, quantize, threshold, band, point, or ordinal is used when both the channel and
  the field type accept it; otherwise (and for the types that are not drawn yet) the default applies. `ChannelIsDrawn`
  gives a channel a scale
  only for the marks Vega-Lite's `supportMark` allows (`shape` on `point`; `angle` on `point`, `circle`, and `square`;
  `strokeDash` and `size` not on `rect`), and `AcceptsScaleType` answers the type question for the feature catalog.
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
  when a custom domain already spans zero; never for quantile and threshold scales), then `domainMin` and `domainMax`
  replace the first and the last entry (sequential and linear scales), and `domainMid` (colors) inserts the middle value
  before the first entry, after the last, or between the ends, as Vega's `configureDomain` does. A quantize scale has
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
- **Padding and nice.** A band scale has inner padding by mark from the configuration (bar 0.1, tick 0.25, others 0) and
  outer padding half of it; a point scale 0.5; `scale.padding` sets both. A continuous position scale gets nice with a
  count of 10 unless it has a custom domain **or is binned** (its bins already end on round values); a bar on its
  continuous axis (not a binned one) gets 5 pixels of padding.
- **Range.** x is 0 to the width and y is the height to 0 (continuous) or 0 to the height (discrete). Size ranges follow
  the mark (point-like marks 4 to the square of 0.95 steps, bar and tick 2 to step minus 1, rule 1 to 4); `strokeWidth`
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
  continuous domain) reverses the colors.
- **Limits and cancellation.** A range, domain, or sort array of the specification is checked by its length against
  `MaxScaleDomainSize` before anything is built; a scheme `count` and a generated numeric range are checked by their
  count (the check of `ColorSchemes.Configure` is located at the scheme) and polled for cancellation every 4,096 values.
  The values of a **quantile** scale are counted against `TableLayout.MaxCells` (twice the number of rows: the values
  and the sort buffer) before the array is allocated, read with the token polled every 4,096 rows, charged to the
  evaluation-step budget through a `WorkMeter` (one step per 64 values read and per 64 comparisons of the sort,
  `n * ceil(log2 n)`), and sorted by a bottom-up merge sort that polls the token. Strings are never built from data
  here: colors are
  `#rrggbb` texts or texts the parser has already length-checked.
- **Sort forms** (`ScaleResolver.Sort.cs`). `"ascending"` and `"descending"` order the categories by value (a stable
  sort, null first, the order of `DataValue.Compare`); `null` keeps the data order; an array lists the categories (those
  it
  does not list come first). A sort array has at most `MaxScaleDomainSize` items (otherwise a `VegaLiteRenderException`
  located at the array that carries `the limit of` and `VegaLiteLimits.MaxScaleDomainSize`, which the tool maps to
  `budget-exceeded`), is read with the cancellation token polled every 4,096 items, and skips an object or array item
  (the parser rejects them; the resolver never lets one stand for the null category). The other forms order the
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
- **Orientation.** `InferOrient` follows Vega-Lite: no orientation for point, circle, square, and rect marks; a bar is
  horizontal when only x is a measure and vertical when only y is, a tick the other way round, and a rule is vertical
  with only x and horizontal with only y; an explicit `orient` of the mark wins where Vega-Lite lets it. A count definition
  (an `aggregate` of `count` without a field) is a measure. The encoding plan and the feature catalog reuse it.

**Deviations** (also listed in _CanvasNetVegaLite System Design_): values of different kinds in one discrete domain
are ordered by kind; `scale.domain` forms other than an array and `"unaggregated"` are ignored, and
`"unaggregated"` applies only to the shared-domain operations; a property that the scale type or channel does not support
(`scheme` or `interpolate` on a non-color channel, `domainMin` on a quantize scale, and so on) is ignored without Vega-Lite's
console warning; a color scale with an explicit domain of four or more entries (a piecewise color scale) is a specification
error; an array of colors given as the `scheme` of a discrete scale is used as the whole list of colors; a sort by the
`argmin`, `argmax`, `values`, or exponential
operation is ignored (the catalog reports it); a `sort` array puts values
missing from it first; a bin is planned on x and y only. A
`sort` array is read once into a
dictionary from each listed value to its first position (a value listed twice counts at its first position), so
ordering a domain costs one lookup per value rather than a search of the list per value.

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
- `ChooseKind`, `ChannelIsDrawn`, and `AcceptsScaleType` - the scale-type rules above; `BuildContinuous` builds the linear,
  sequential, diverging, quantize, quantile, and threshold scales (domain, zero, `domainMin`, `domainMax`, `domainMid`,
  nice, clamp), and `BuildDiscrete` the band, point, and ordinal ones; both ask `ResolveOutputs` for the output values
  of the channel, by its group
  (position, color, shape and dash, numeric).
- `InferOrient(markType, specified, encoding)` - the orientation rule above; a measure is an unbinned quantitative field
  or a count.
- `ResolvedScale.CreateModel(extent)` - applies what depends on the extent: the range of a position scale (including an
  explicit range with `width` and `height` keywords, all of whose entries are kept for a linear scale, and `rangeMin`
  and `rangeMax`), the pixel
  padding of a continuous scale (the domain is widened so that the extreme values keep that distance from the edge),
  then `nice` (after the padding, as Vega does), and builds a linear, sequential, diverging, quantize, quantile,
  threshold, bin-ordinal,
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
an unknown scheme name or an unknown named range) is a `VegaLiteSpecException`
located at the offending value (a name that comes from `config.range` is located at the channel). A discrete domain with
more than `MaxScaleDomainSize` values, a specification array (domain, range) of more entries than that, a scheme `count`
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

- **`ScaleModel`**, **`Ticks`**, **`ColorSchemes`**, **`ColorInterpolator`**, and **`ColorRamp`** - the models, the
  tick-based `nice` rule,
  the scheme catalog and its range configuration, and the color interpolation
- **`CssColor`** - the reading of the colors of a list of colors
- **`EncodingPlan`**, **`EncodingData`**, **`ChannelPlan`**, **`BinSpec`**, and **`StackOffset`** - the plan and the tables
  of the implicit pipeline (the `EncodingPlan` unit), which also reuses `InferOrient` of this unit
- **`MeasureCell`**, **`MeasureEnvironment`**, **`WorkMeter`**, **`AggregateMeasures`**, and **`TableLayout`** - the
  accumulators and limits of the `AggregateTransforms` and `RowTransforms` units, for the sort by an aggregate
- **`DataTable`** and **`DataValue`** - the data and values of the `Data` subsystem
- **`SpecNode`**, **`SpecParser`** (`TryParseSortShorthand`), and **`ResolvedConfig`** - the tree and the
  configuration of
  the `SpecModel` subsystem
- **`VegaLiteLimits`**, **`VegaLiteLocation`**, **`VegaLiteSpecException`**, and **`VegaLiteRenderException`** - the public
  types of the `VegaLiteDocument` unit
- **.NET base class library** - `System`, `System.Collections.Generic`, `System.Globalization`

#### Callers

`VegaLiteDocument` calls the pipeline `Resolve` once per render, with the plan and the tables that `EncodingTransforms`
produced. `FeatureCatalog` calls `InferOrient` (to decide whether the `order` channel orders a stack). `AxisBuilder` and
`MarkBuilder` read the `ResolvedScale` (field path, second field, title, bin and stack facts, kind, domain, padding) and
the models built from it. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Scales/ScaleResolverTests.cs` and `ScaleResolverColorTests.cs` call the
resolver directly through
`InternalsVisibleTo`.
