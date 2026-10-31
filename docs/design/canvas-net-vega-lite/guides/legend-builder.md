### LegendBuilder

![Guides Structure](VegaLiteGuidesView.svg)

The `LegendBuilder` unit is the fifth unit in the `Guides` subsystem. It is six source files in the
`DemaConsulting.CanvasNet.VegaLite.Guides` namespace: `LegendPlan.cs` (the planning types and the static class
`LegendPlan`), `LegendBuilder.cs` (the sealed partial class `LegendBuilder` with its result records),
`LegendBuilder.Symbols.cs` (symbol legends), `LegendBuilder.Gradient.cs` (gradient and discrete gradient legends),
`LegendBuilder.Grid.cs` (the grid layout of Vega's `layout` transform), and `LegendBuilder.Time.cs` (the ticks, labels,
and `values` of temporal legends). The files carry the provenance header comment
`Portions derived from Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause)` (the time file adds the d3-time, d3-time-format,
and d3-scale notices). The label formats and tick requests of dates are shared with the axes through the `TimeLabels`
unit (see _Time Labels Design_).

#### Purpose

The unit builds the legends of a chart. `LegendPlan` decides which channels get a legend, of what kind and orient, and
which channels share one; `LegendBuilder` turns one planned legend into scene items relative to its own origin and
measures the box it needs; the `Layout` subsystem places the boxes and makes room for them. The rules are those of
Vega-Lite 6 (`legend/parse.ts`, `properties.ts`, `encode.ts`) over Vega's legend parsers (`legend.js`,
`legend-symbol-groups.js`, `legend-gradient.js`, `legend-gradient-discrete.js`, `legend-gradient-labels.js`,
`legend-title.js`), the `LegendEntries` generator, `scaleGradient`, and the grid and legend layout of
`vega-view-transforms`; the code is an own C# implementation and the third-party notice names what was studied. The
unit's behavior is specified by the `CanvasNetVegaLite-Guides-LegendBuilder-*` requirements in
`docs/reqstream/canvas-net-vega-lite/guides/legend-builder.yaml`.

#### Data Model

- `LegendMember` - one channel that a legend explains: the channel, its `ResolvedScale`, the `ScaleModel` (a legend scale
  does not depend on the size of the data rectangle), the channel definition and its `legend` object.
- `LegendMarkFacts` - what the symbols stand for: whether the mark is filled, its fixed fill and stroke (the same rules
  the marks use), the opacity (0.7 for point-like marks, or the constant opacity channel), the fixed shape, the stroke
  width, dash, fill and stroke opacity, and whether a field drives the fill or the stroke.
- `LegendSettings` - property lookup: the `legend` objects of the members (the first member that has a property wins,
  which is Vega-Lite's explicit-over-implicit and first-channel rule), then `config.legend` with the Vega-Lite and Vega
  defaults, then the `guide-label` and `guide-title` styles.
- `LegendSpec` - a planned legend: the members in channel order (the first is the canonical scale that decides the
  entries), the `LegendKind` (`Symbol`, `Gradient`, `DiscreteGradient`), the orient, whether the orient was explicit,
  the direction, the title lines and the mark facts.
- `LegendResult` - a built legend: kind, orient, the explicit offset, `legendX` and `legendY`, the box size (rounded
  up), the bounds (the box grown by half the border width), the children (the `legend-entry` group, then the
  `legend-title` text), the `LegendFrame` (fill, stroke, stroke width and dash, corner radius) and the item count;
  `CreateGroup(x, y)` creates the `legend` group at the position the layout chose.
- `GridOptions` and `GridResult` - the parameters and result of the grid layout (`GridLayout`).

Scene roles: `legend` (group) containing `legend-entry` (group) and `legend-title`; the entry group contains
`legend-symbol` and `legend-label` pairs, or `legend-gradient` and `legend-label`s, or `legend-band`s and
`legend-label`s.

#### Key Methods

- `LegendPlan.Create(view, scales, config, sourceName)` - for each channel in the order color, size, shape, fill,
  stroke, strokeDash, strokeWidth, opacity that has a scale and a field or datum (and whose `legend` and `scale` are not
  null, with `config.legend.disable` unset) creates a member; members whose domains are the same (a key from the
  derived field, the binning, discrete or continuous domain, and the fingerprints of the explicit `scale.domain` and
  `sort`) merge into one legend unless both give a different `orient`. The kind is a gradient for a sequential or
  diverging color scale, a discrete gradient for a quantize, quantile or threshold color scale, a symbol legend
  otherwise, `legend.type: symbol` forces symbols, and members of different kinds make a symbol legend. The orient
  is the explicit one or `right`; the direction is the explicit one, Vega-Lite's default for the orient (horizontal for
  top and bottom, and for the inner corners with gradients), then `config.legend.direction`, `symbolDirection` or
  `gradientDirection`. The title is the explicit `legend.title` or channel `title` (null or empty means none), else the
  distinct default titles of the members joined by a comma. `LegendPlan` itself did not change for time: a temporal
  color is a sequential scale, so it is planned as a **gradient** legend, a size or opacity over a time scale and an
  ordinal field with a time unit are planned as **symbol** legends, and the merge key already uses the derived field
  name of the plan (a different time unit of the same field is a different key).
- `LegendBuilder.Build(spec, dataWidth, dataHeight, budget, cancellationToken)` - charges the legend group, the title
  and the entry group, builds the content, then performs Vega's `legendGroupLayout`: the entries sit at the padding,
  below the title by its height plus the title padding; a middle or end anchored title is positioned against the width
  of the entries (the bar alone for a horizontal gradient); a title that reaches left of the padding pushes everything
  right; the box is `ceil(right + padding)` by `ceil(bottom + padding)`.
- Symbol legends - the values are the explicit `values`, the domain of a discrete scale, the d3 ticks of a continuous
  scale (the tick count is `tickCount` or 5, reduced by `tickMinStep`; for a log scale only the log ticks that d3's log
  rule labels, all labeled with `NumberFormat.FormatFloat`), or for a discretizing scale minus infinity and
  the thresholds (for a bin-ordinal scale the bin edges). More than `symbolLimit` values keep the first `limit - 1` and
  add an entry `…N entries` for the next value. A first value that the scale maps to zero is dropped unless the values
  are explicit. Labels are made only for the kept entries: a discrete value is its JavaScript text (a number
  with `format` uses the d3 format), a continuous value the d3 tick format of the domain, and a discretizing class a
  range label (`< hi`, `lo – hi`, `≥ lo`, with the format of the thresholds for ten ticks per class). An entry has the
  size `max(ceil(sqrt(symbolSize) + strokeWidth), labelFontSize)`; the symbol is centered at half the entry size
  (half the largest entry size with columns) plus `symbolOffset`, and its label starts after the entry plus `symbolOffset`
  and `labelOffset`. The symbol look combines the scales of the members (fill, stroke, shape, size, stroke width,
  dash, opacity) with the fixed paints of the mark and the base paints of the legend configuration, as described in
  the requirements. Entry boxes are the union of the symbol box (the shape box grown by two stroke widths for a
  stroked symbol, Vega's miter allowance) and the label box; the grid layout (`GridLayout`, a port of Vega's `layout`
  with align each, row padding 2, column padding 10, the rows centered) gives the position of each entry; a vertical
  legend with several columns fills them from top to bottom. **Temporal symbol legends** (`LegendBuilder.Time.cs`): a
  size or opacity on a **time or UTC** scale (and a temporal color asked to be shown as symbols) has as values the explicit
  `values` or the **time ticks** of the domain (the count is `tickCount` or 5, limited by `tickMinStep`, or an interval
  name or `{interval, step}`), labeled as dates; the discrete values of an ordinal or nominal field **with a time unit**
  are dates, labeled by the same function (a discrete legend over the date values of a nominal field _without_ a time
  unit is not temporal and keeps the text labels). `legend.values` of a temporal field are timestamps, date texts, or
  date-time objects (`TemporalValue`: a date by the domain-entry unit rule for a discrete scale, milliseconds for a
  continuous one; an entry that is not a date is dropped).
- **Temporal labels** (`TimeLabelFunction`, built on `TimeLabels.Create`; plan decision P8-D7): the label of a date is,
  in order, the legend `format` (a string is a d3 time format; an object is Vega's multi-format with its members as the
  overrides), the specifier of the time unit of the field (`%b %Y` for `yearmonth`), and `config.timeFormat` (`%b %d, %Y`
  by default, so `Jan 01, 2024`), in the calendar of the scale (UTC for a UTC scale or unit, or for a color whose
  explicit scale type is `utc`, whose kind is the sequential one but which carries `ResolvedScale.IsUtcTime`; otherwise
  the zone of the chart). The time ticks of the gradient and of the symbols count the whole days of two-day ticks from the
  epoch when `IsUtcTime` is set (d3's `utcTicks`). A `formatType` of `number` formats the milliseconds as a number; `time` and `utc` force time labels; any other
  name is a custom format type and is reported by the feature catalog. The length of every label is bounded by
  `MaxStringLength`, the labels of the legend are bounded together by the same limit (the running total of
  `TimeLabels.Create`), and the length of each is charged to the build's evaluation (`MaxEvaluationSteps`, one step per
  64 characters) as it is made. The build's
  `ExpressionFunctionContext` is created with the **calendar** of the canonical scale (`ResolvedScale.Calendar`, UTC when
  the legend has none), so that expressions evaluated for the legend read dates in the zone of the chart; `labelExpr` is
  not implemented for any legend.
- Gradient legends - the bar is `gradientThickness` (16) by the gradient length, which is `gradientLength`, else for a
  continuous scale the view height limited to 64 to 200 (vertical) or the view width limited to 100 to 200 for top and
  bottom (100 for other horizontal legends), else 200. Its `FillGradient` has the stops of Vega's `scaleGradient`: the ends
  of the domain and the ticks for a count of 15, each at its position **along the transformed domain**
  (`ScaleModel.Fraction`; the log ticks for a log scale) with the color of the scale (a domain
  without span is replaced by 0 to 1 with the interpolator). The labels are the ticks for a count of `tickCount` or
  `max(2, 2 * floor(length / 100))` (the two ends when there are fewer than three), formatted by the d3 tick format
(for a
  log scale only the ticks that d3's log rule labels, with the variable-precision format and the same rule applied to the
  end points), at their fraction along the transformed domain,
  placed outside the bar at `gradientLabelOffset` (2) or `labelOffset`, aligned by position, and thinned by
  `LabelOverlap` (the method of `labelOverlap`, greedy for quantile and threshold scales, else parity). A discrete
  gradient has one band per class (the colors of the scale at minus infinity and the thresholds, positioned by the linear
  fraction of the domain, a threshold domain widened by one class at each end) and a label for each boundary. The
  gradient legend of a **temporal** color (`ResolvedScale.IsTemporalField` on a sequential or diverging scale) takes
  the time ticks of its millisecond domain instead of the numeric ticks, both for the labels (the tick count of the
  legend, or the interval of `tickCount`, limited by `tickMinStep`; labels as above) and for the stops: the ramp is
  sampled at the time ticks for a count of 15 (Vega samples a time scale at its ticks): over a domain of ten days the
  target is 16 hours, which is nearer to the 12-hour entry of the table than to the 1-day entry, so the ramp has 21
  stops.

**Preconditions and postconditions**: the spec comes from `LegendPlan.Create`; the result's children are positioned
relative to the legend group and the entry group; the budget has been charged for exactly the items created.

#### Error Handling

An invalid `format` is a `VegaLiteSpecException` located at the `format` property. Too many explicit values
(`MaxScaleDomainSize`), too many ticks (`MaxTicks`), a label longer than `MaxStringLength` (measured with the shared
`ExpressionEvaluator.ToJsString` helper, which bounds arrays with a visited set) or more scene items than
`MaxSceneItems` is a `VegaLiteRenderException` located at the legend and naming the limit. **Limits and charging**: the
entry count is bounded before any label or item is created (domain and values by `MaxScaleDomainSize`, ticks by
`MaxTicks`, kept entries by `symbolLimit`); each label's length is charged to a private expression evaluation
(`MaxEvaluationSteps`) before it is measured; every scene item is charged to the budget before it is created, the
labels of a gradient after the overlap removal (hidden labels cost nothing); the loops poll the cancellation token
every 1,024 entries. **Determinism**: text is measured with the bundled fonts only; the gradient has at most about 30
stops; no dictionary enumeration order reaches the output. A time label is also bounded before it is made (the formatter
stops at `MaxStringLength`, and all the labels of a legend together are limited to it), the time ticks by `MaxTicks` (located at the legend), and nothing reads the host clock,
zone, or culture.

#### Dependencies

`TextMeasurer`, `LabelOverlap`, `TimeLabels`, `ResolvedScale`, `ScaleModel`, `Ticks` (including `TimeTicks`,
`TimeTickRequest`, and `ResolveTickCount`), `NumberFormat`, `RgbColor` and the discretizing
scale kinds of the `Scales` subsystem; `SpecNode`, `ConfigNode` and `ResolvedConfig` of the `SpecModel` subsystem;
`DataValue`, `JsNumber`, `TimeCalendar`, `TimeUnitSpec`, `DateTimeObject`, and `JsDateParser` of the `Data` subsystem;
`ExpressionEvaluator.ToJsString` and `ExpressionFunctionContext` of
the `Expressions` subsystem; the scene types and `SceneBudget` of the `Scenegraph` subsystem; and the public
`VegaLiteLimits`, `VegaLiteLocation`, `VegaLiteRenderException` and `VegaLiteSpecException`.

#### Callers

`VegaLiteDocument` and `LegendLayout` of the `Layout` subsystem (which also calls `LegendBuilder.GridLayout`). The
tests in `test/DemaConsulting.CanvasNet.VegaLite.Tests/Guides/LegendPlanTests.cs`, `LegendBuilderTests.cs`, and
`LegendBuilderTimeTests.cs` call the
unit directly through `InternalsVisibleTo`.

#### Deviations From Vega-Lite

The gradient stops follow Vega's rule (the ends of the domain and the ticks for a count of 15), with the offset of each
stop its position along the transformed domain (Vega's `scaleFraction`; the file with that function could not be
read, so the
log, pow, and symlog fractions are derived from the d3 transforms). The canonical scale of a merged continuous
legend is the first channel in Vega-Lite's order
(Vega picks by its own order of scale names). A `titleOrient` other than `top`, `clipHeight`, `encode`, `labelExpr` (not
implemented for any legend or axis)
and custom format types (a `formatType` other than `number`, `time`, and `utc`) are not drawn and are reported by the
feature catalog. A discrete legend over the dates of a nominal field without a time unit is not temporal (its values are
not date-parsed) and keeps the text labels. A temporal color scale that asks for a discretizing type (quantile,
quantize, threshold) is replaced by the sequential default (see _ScaleResolver Unit Design_), so no discretized
temporal legend exists. The symbols of the `arrow`, `wedge` and
`triangle` shapes are bounded by the box of their path. `legend.symbolType` is combined with the configuration's
`symbolType` only for marks that default to a circle. A bin-ordinal scale (the scale of a binned quantitative color) has
a symbol legend with one entry per bin (the edges but the last are the values) labeled `lo – hi` with the format of the
legend or the JavaScript text of the numbers (Vega's `formatRange`, which reads the next value of the list as the upper
edge and, for the last bin of the scale, the last edge); explicit `values` are a plain list, so each label runs to the
next listed value and the last reads `≥ lo`, as the array of Vega has no `max`. A binned size, opacity, or angle is a
linear scale over the start of the bins and has the ticks of a continuous scale; a binned nominal or ordinal field has
the texts of its bins as categories. Channels that bin the same field with the same parameters have the same `BinKind`
and domain in the merge key, so they share one legend.

A channel that has **conditions** gets a legend only from its field definition: a condition that holds a field makes the
legend of that field (its scale, title, and format), while conditions that hold only values create none, as in
Vega-Lite. The symbols of another legend (for example a size legend) look like the marks they describe through the facts
that `LegendPlan` reads from a channel that no field drives: the fill, stroke, or shape is the value of the first
condition on a test (Vega-Lite's `getFirstConditionValue`), else the value of the channel, and the opacity is the largest
of the value of the channel and the values of its conditions, so the symbols are as visible as the most visible mark.
