### ScaleResolver

![Scales Structure](VegaLiteScalesView.svg)

The `ScaleResolver` unit is the second software unit in the `Scales` subsystem. It comprises three source files in the
`DemaConsulting.CanvasNet.VegaLite.Scales` namespace, all `internal`, that define four `internal` types:
`ScaleResolver` (a `static partial` class), `ResolvedScales` (sealed class), `ResolvedScale` (sealed class), and
`RangeEnd` (readonly record struct):

- `ScaleResolver.cs` - the entry points, the channel resolution (kind, linear and discrete scales, the zero, nice, padding,
  and range rules), and the four types
- `ScaleResolver.Plan.cs` - the domains that follow from the encoding plan: binned, stacked, ranged, and unaggregated
- `ScaleResolver.Sort.cs` - the order of the categories of a discrete scale (every `sort` form, including the sort by an
  aggregate)

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
(an indexer by channel name and an enumeration in the fixed channel order x, y, color, fill, stroke, size, opacity), and
the `DiscreteWidth` and `DiscreteHeight` that a step-driven discrete position scale needs.

**`ResolvedScale`** (immutable) describes one channel before the size of the data rectangle is known: `Channel`, `Kind`,
`FieldPath` (the field the marks read, which the encoding plan names: the derived field of a bin or an aggregate, or the
upper end of a stack; null for a `datum`), `Field2Path` (the lower end of a stack, the end of a bin, or the field of `x2`
or `y2`), `Title` (the default title of the plan), `Bin` (a `BinKind`) and `BinSpec` (the bins of a continuous channel that
the encoding bins), `Stack` (the `StackOffset` of the stacked channel, or null), `DataType`, `Location` (the channel
definition), the linear `DomainStart` and
`DomainEnd` (after the zero rule, before pixel padding and nice), `NiceCount`, `PaddingPixels`, `Clamp`, `Round`,
`Reverse`, the `DiscreteDomain`, `PaddingInner`, `PaddingOuter`, `Align`, `Step`, the `ExplicitRange` (a list of
`RangeEnd` values, each a number or the keyword `width` or `height`), the `ValueRange` of a size or opacity scale, the
`ColorRange` and `ColorFallback`, `IsPosition`, and `NaturalExtent` (the step times the band space of a step-driven
discrete scale). `CreateModel(extent)` builds the `ScaleModel` for a data-rectangle width (x) or height (y); the extent
is ignored for color, size, and opacity.

**Inference rules** (Vega-Lite 6 behavior, ported with attribution):

- **Type.** Quantitative position channels are linear; discrete position channels are band scales for `rect`, `bar`,
  `image`, `rule`, and `tick` marks and point scales for the others; `color`, `fill`, and `stroke` fields are ordinal;
  `size` and `opacity` are point-style value scales. A quantitative color has no scale, and a temporal field has none. An
  explicit `scale.type` of linear, band, or point is used when the channel and field type accept it.
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
  bar (unless it is a range) and for quantitative sizes, the configured default otherwise, never for a binned field,
  and only
  when a custom domain already spans zero). A
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
  the mark (point-like marks 4 to the square of 0.95 steps, bar and tick 2 to step minus 1, rule 1 to 4); opacity is 0.3
  to 0.8; colors are the configured category scheme (`tableau10`). A discrete position scale without an explicit range
  or a fixed size takes its size from `step` (20 pixels by default), which is what `DiscreteWidth` and `DiscreteHeight`
  report so that the layout can size the view. An explicit numeric range keeps all its entries for a linear scale (`reverse`
  flips the whole array, as Vega does); a band or point scale uses the first two entries, as d3's `scaleBand` does.
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
are ordered by kind; a scheme other than `tableau10`, and the ordinal range of an ordinal color field, fall back to
`tableau10` with `ColorFallback` set; `scale.domain` forms other than an array and `"unaggregated"` are ignored, and
`"unaggregated"` applies only to the shared-domain operations; a sort by the `argmin`, `argmax`, `values`, or exponential
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
- `InferOrient(markType, specified, encoding)` - the orientation rule above; a measure is an unbinned quantitative field
  or a count.
- `ResolvedScale.CreateModel(extent)` - applies what depends on the extent: the range of a position scale (including an
  explicit range with `width` and `height` keywords, all of whose entries are kept for a linear scale), the pixel
  padding of a continuous scale (the domain is widened so that the extreme values keep that distance from the edge),
  then `nice` (after the padding, as Vega does), and builds a linear, band, point, or ordinal `ScaleModel`; a non-finite
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
than two values; a quantitative domain with fewer than two numbers or with a non-number) is a `VegaLiteSpecException`
located at the offending value. A discrete domain with more than `MaxScaleDomainSize` values is a
`VegaLiteRenderException` located at the channel; so is the work of a sort by an aggregate that exceeds the
step budget or the
value-state limit (the failure of the accumulators is converted with the same message and located at the channel
definition). A canceled token is an `OperationCanceledException`. A null argument is
`ArgumentNullException`, which is not converted. Constructs outside the supported subset are never errors here: they are
reported by the `FeatureCatalog`, and the resolver falls back or ignores them as listed above.

#### Dependencies

- **`ScaleModel`**, **`Ticks`**, and **`ColorSchemes`** - the models, the tick-based `nice` rule, and the category palette
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
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Scales/ScaleResolverTests.cs` call the resolver directly through
`InternalsVisibleTo`.
