### LegendBuilder

![Guides Structure](VegaLiteGuidesView.svg)

The `LegendBuilder` unit is the fifth unit in the `Guides` subsystem. It is seven source files in the
`DemaConsulting.CanvasNet.VegaLite.Guides` namespace: `LegendPlan.cs` (the planning types and the static class
`LegendPlan`), `LegendBuilder.cs` (the sealed partial class `LegendBuilder` with its result records),
`LegendBuilder.Symbols.cs` (symbol legends), `LegendBuilder.Gradient.cs` (gradient and discrete gradient legends),
`LegendBuilder.Grid.cs` (the grid layout of Vega's `layout` transform), `LegendBuilder.Time.cs` (the ticks, labels,
and `values` of temporal legends), and `LegendDimming.cs` (the dimming of the entries of a legend that a selection is
bound to). The files carry the provenance header comment
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
- `GuideTitle` - the title that one source (a channel, or a layer) gives a guide, written or derived, and its merge
  (`Merge`): Vega-Lite's `mergeValuesWithExplicit` with `mergeTitleComponent` as the tie-breaker, shared by the legend titles
  and the axis titles of layers. A written title (`FromWritten`: a string or an array of lines; null or another value
  switches the title off, an empty one is neutral) wins over a derived one (`FromField`); two derived titles join as the
  distinct parts in order; two different written titles join with a comma (equal ones are one); `Resolve` gives the lines
  a guide shows.
- `LegendMarkFacts` - what the symbols stand for: whether the mark is filled (a **trail is never filled for its legend**,
  see the paragraph on the path, area, arc, and text marks below), its fixed fill and stroke (the same rules
  the marks use), the opacity (0.7 for point-like marks, or the constant opacity channel), the fixed shape, the stroke
  width, dash, fill and stroke opacity, and whether a field drives the fill or the stroke. For a path mark with a **point
  overlay** (`SpecNormalizer.PointOverlayDefinition`) the facts are those of the overlay mark, a filled `point` with an
  opacity of 1 (whatever the overlay object replaces): Vega-Lite normalizes the mark into the path and a filled point
  layer, and the merge of the legends prefers the circle of the points to the stroke of the path (`mergeSymbolType`), so
  the legend of a line with points shows filled symbols (the color is their fill), not the hollow symbols of the line.
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
- `LegendDimming` (phase 12) - the selections that bind a legend (`"bind": "legend"`) and what each holds for the field
  of the first channel; `LegendPlan.Create` finds it (`LegendDimming.Create`) from the optional `SelectionModel` and the
  unit view of the first layer of the legend, and `LegendSpec.Dimming` carries it (null when no selection is bound).
Scene roles: `legend` (group) containing `legend-entry` (group) and `legend-title`; the entry group contains
`legend-symbol` and `legend-label` pairs, or `legend-gradient` and `legend-label`s, or `legend-band`s and
`legend-label`s.

#### Key Methods

- `LegendPlan.Create(layers, config, sourceName)` (phase 10, over `LegendLayer(View, Scales, Scope)`; the single-view
  overload `Create(view, scales, config, sourceName)` is a call with one layer) - the same planning over the layers of
  a layered view. For each channel, the layers that have the same **scope** for it (the function `Scope(channel)` that
  `VegaLiteDocument` builds from the layers' `resolve` of the legend and their scale groups, so layers whose legend or
  scale is independent are in different scopes) contribute one member: the first layer that has a legend for the
  channel decides its scale (the merged one for a shared channel), definition and the mark facts; the titles of all the
  layers of
  the scope merge as `GuideTitle.Merge` does (Vega-Lite's `mergeValuesWithExplicit` with `mergeTitleComponent`): a written
  legend or channel title of any layer wins over the titles derived from fields, derived titles join with a comma
  (`a, b`), different written titles join too, equal ones are one. The `legend` objects of all the layers give the
  properties as `mergeLegendComponent` merges them (`LegendMember.LayerLegends`, read by `LegendSettings` in layer
  order, then
  channel order): a property that one layer gives wins over the silence of the others, and when several give it
  differently the first one wins (Vega-Lite's default tie-breaker, with a warning there); explicit `type`s that
  differ give a symbol legend (`ExplicitTexts`). Layers that give different explicit `orient`s cannot be merged
  (Vega-Lite's `mergeLegendComponent` returns undefined and the channel's legend becomes independent): each of them is
  a member of its own, with the mark facts of its layer. Members of different layers and scopes then merge
  exactly as members of one view do (equal key and scope, no two members of a channel in one legend), and the
  `LegendMarkFacts` of a legend are those of the layer that its first member comes from (Vega-Lite also merges the
  symbol encodings of the layers, `symbolType` preferring circle; not reproduced).
- `LegendPlan.Create(layers, config, sourceName, owner)` and `LegendPlan.Owners(layers)` (phase 11) - a `LegendLayer`
  may carry the `LayerNode` it comes from (`Node`). `LegendLayer.OwnerOf(channel)` then climbs from that node while the
  parent's `resolve.legend` of the channel is shared (`LayerResolve.Legend`, which already makes the legend independent
  when the scale is) and returns the highest such view: the **owner**. The children of a concatenation share the legends
  of `color`, `size`, `shape` and the other non-position channels by default, so one legend is owned by the
  concatenation and drawn beside it; a legend that a view makes independent is owned by the view below (the unit, a
  layer's chart). Two layers never share a legend when their owners differ (the owner is part of the scope key next to
  the scope text of the caller), so a leaf that has a channel owned by the concatenation and another owned by itself is
  two legends. `LegendSpec.Owner` is the owner of the first member's channel. `Owners` lists the distinct owners of the
  channels that have a legend, channel order first and layer order second, so the caller plans each owner once:
  `Create(..., owner)` considers a layer's channel only when its owner is that view and returns that view's legends.
  Without an owner argument all legends are planned, and layers without a node have no owner (the plan is exactly that
  of phase 10).
- `LegendPlan.Create(view, scales, config, sourceName)` - for each channel in the order color, size, shape, fill,
  stroke, strokeDash, strokeWidth, opacity that has a scale and a field or datum (and whose `legend` and `scale` are not
  null, with `config.legend.disable` unset) creates a member; members whose domains are the same (a key from the
  derived field, the binning, discrete or continuous domain, and the fingerprints of the explicit `scale.domain` and
  `sort`) merge into one legend unless both give a different `orient`. The kind is a gradient for a sequential or
  diverging color scale, a discrete gradient for a quantize, quantile or threshold color scale, a symbol legend
  otherwise, `legend.type: symbol` forces symbols, and members of different kinds make a symbol legend. The orient
  is the explicit one or `right`; the direction is the explicit one, Vega-Lite's default for the orient (horizontal for
  top and bottom, and for the inner corners with gradients), then `config.legend.direction`, `symbolDirection` or
  `gradientDirection`. The title is the merge of the titles of the members (`GuideTitle`, also used for the axis title of
  layers): a member's title is **written** (`legend.title` or channel `title`; null means none, an empty text is neutral)
  or **derived** from the field (its default title); a written title wins over a derived one, derived titles join by a
  comma as distinct parts, different written titles join by a comma, and a written null switches the title off.
  `LegendPlan` itself did not change for time: a temporal
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
  legend with several columns fills them from top to bottom. **Path, area, arc, and text marks** (the symbol that stands
  for each mark): the shape of the symbols of a legend that has no shape scale is the `symbolType` of the legend, else
  the fixed shape of the mark, else the shape of the mark: a `square` for a bar, rect, image, and square, the **`stroke`
  shape** (a short line) for a **line, rule, and trail**, and a circle (or the `symbolType` of the configuration) for
  every other mark, so an **area, an arc, and a text mark have filled circles**. A mark is filled by its `filled` property
  or by the default of the mark type (`ConfigResolver.IsFilledByDefault`), **except that a trail is never filled for its
  legend**: Vega-Lite's legend encoding paints a symbol as filled only when `filled && mark !== 'trail'`, so a trail, which
  is filled by default, gets a `stroke` symbol painted with the stroke color (the color of the category is the stroke,
  and the fill is `transparent`), as a line does. This was a fix of `LegendPlan` (in the creation of `LegendMarkFacts`);
  before it the trail got the stroke-shaped symbol with the category color as its fill. The stroke width of a line,
  rule, or trail symbol is the default of a legend symbol, **1.5**, because Vega-Lite's legend encoding does not read the
  width of Vega's line style; a `strokeWidth` that the mark definition gives wins. **The size legend of a trail** scales
  the
  **stroke width** of its symbols and keeps the symbol size at `symbolSize` (100 by default): `LookOf` follows
  `compile/legend/parse.ts` `getLegendDefWithScale` of vega-lite@6.5.0 (`mark === 'trail'`: `size` becomes
  `strokeWidth`, the
  color becomes the stroke), so the symbol column is `max(ceil(sqrt(size) + strokeWidth), fontSize)` wide and a thick
  trail has a thick
  symbol; the scale wins over `symbolStrokeWidth` and a stroke width of the mark. The size legend of a line, a point, or
  a text mark is
  still the symbol size. Text and arc marks have filled circles
  of the default symbol size, and the font size that a `size` channel gives a text mark is shown as the size of the
  symbol. **Temporal symbol legends** (`LegendBuilder.Time.cs`): a
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
  epoch when `IsUtcTime` is set (d3's `utcTicks`). A `formatType` of `number` formats the milliseconds as a number;
  `time` and `utc` force time labels; any other name is a custom format type and is reported by the feature catalog. The
  length of every label is bounded by
  `MaxStringLength`, the labels of the legend are bounded together by the same limit (the running total of
  `TimeLabels.Create`), and the length of each is charged to the build's evaluation (`MaxEvaluationSteps`, one step per
  64 characters) as it is made. The build's
  `ExpressionFunctionContext` is created with the **calendar** of the canonical scale (`ResolvedScale.Calendar`, UTC when
  the legend has none), so that expressions evaluated for the legend read dates in the zone of the chart.
- **Label expressions** (`LabelExpressions`, see _LabelExpressions Unit Design_; Vega-Lite's `compile/legend/assemble.ts`
  and Vega's `LegendEntries.js`): the explicit `legend.labelExpr` (the compiled expression of the node, found through
  `LegendSettings.ExplicitNode`) replaces the label of every entry **before the entries are measured and laid out**, so
  blank labels change the measured width of the legend. **`config.legend.labelExpr` is not read**, because Vega-Lite 6
  has no such property. `Labeled` builds the datum for each label: a symbol legend gives `index` (a whole number),
  `label`, and `value` (the `...N entries` row of `symbolLimit` is an extra entry whose `index` is the number of entries
  shown); a gradient legend gives `index`, `label`, `value` (a date for a temporal field), and `perc` (the position
  along the bar); a discrete gradient also gives `perc2` (the end of the class). `datum.size` and `datum.offset` of a
  symbol legend are null. The context keeps the compiled expression; an array result is a multi-line label
  (`MakeLabel` takes the lines). The expression runs on the evaluator of the call (the optional `RowExpressions`
  argument of the constructor; without it the builder uses an evaluator of its own with the same limits) and the length
  of the replaced text is charged as the length of every label is.
- **Legends bound to a selection** (phase 12; Vega-Lite's `compile/selection/legends.ts` and `selectedCondition` of
  `compile/legend/encode.ts`): `LegendPlan.Create` gives the legend a `LegendDimming` when a selection of the unit view
  of its first layer is **bound to the legend** (`"bind": "legend"`, `resolve` `global`, one projected field that is not
  the row identity, equal to the `field` of the first channel's definition or to the channel). The test of an entry is
  `!length(data(store)) || (N[field] && indexof(N[field], datum.value) >= 0)`, evaluated once for the value of the
  entry: an empty store selects everything; otherwise the values that the resolved selection holds for the field must
  contain the value of the entry by **strict equality** (the same function as the selection tests uses). The value of
  the entry is what Vega holds in `datum.value` (`LegendBuilder.DatumValue`): the number of milliseconds for a domain
  value of a discrete scale over a field of dates, a date for a tick of a time scale (or of a sequential or diverging
  scale over dates), which the numbers of a selection never equal. The test reads the field of the channel definition,
  not the projected field, so a selection over a field with a time unit (stored under `month_d`) dims every entry.
  The labels of
  every legend kind (`MakeLabel` takes the value of the entry) get the opacity 1 or `config.legend.unselectedOpacity`;
  the symbols of a **symbol** legend that does not explain the `opacity` channel get the opacity of the symbol (the
  opacity of the mark; 1 when the legend has a `symbolOpacity`, as in Vega-Lite's `opacity ?? 1`) or
  `unselectedOpacity`. A legend that explains the `opacity` channel keeps its symbols (Vega-Lite deletes the symbol
  opacity rule when the legend encodes opacity). When the channels of a legend merge, the first one decides (Vega-Lite
  keeps the encoding of the first
  component). The unit view comes from the layer of the first member; a `LegendPlan.Create` without a selection model (or
  without the layer's node) dims nothing. The comparisons are charged to the evaluation of the build.
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
stops at `MaxStringLength`, and all the labels of a legend together are limited to it), the time ticks by `MaxTicks`
(located at the legend), and nothing reads the host clock, zone, or culture. A label expression that exceeds the step
budget or the string limit is a `VegaLiteRenderException` located at the expression (the steps are those of the shared
`MaxEvaluationSteps` budget of the call).

#### Dependencies

`TextMeasurer`, `LabelOverlap`, `TimeLabels`, `LabelExpressions`, `ResolvedScale`, `ScaleModel`, `Ticks` (including
`TimeTicks`,
`TimeTickRequest`, and `ResolveTickCount`), `NumberFormat`, `RgbColor` and the discretizing
scale kinds of the `Scales` subsystem; `SpecNode`, `ConfigNode` and `ResolvedConfig` of the `SpecModel` subsystem;
`DataValue`, `JsNumber`, `TimeCalendar`, `TimeUnitSpec`, `DateTimeObject`, and `JsDateParser` of the `Data` subsystem;
`ExpressionEvaluator.ToJsString`, `ExpressionFunctionContext`, and `RowExpressions` of
the `Expressions` subsystem; the scene types and `SceneBudget` of the `Scenegraph` subsystem; and the public
`VegaLiteLimits`, `VegaLiteLocation`, `VegaLiteRenderException` and `VegaLiteSpecException`.

#### Callers

`VegaLiteDocument` and `LegendLayout` of the `Layout` subsystem (which also calls `LegendBuilder.GridLayout`). The
tests in `test/DemaConsulting.CanvasNet.VegaLite.Tests/Guides/LegendPlanTests.cs`, `LegendPlanLayerTests.cs` (the legends
of layered views), `LegendBuilderTests.cs`,
`LegendBuilderTimeTests.cs`, `LegendBuilderLabelExprTests.cs` (`labelExpr`), `LegendBuilderSelectionTests.cs` (the
dimming of a legend bound to a selection), and `LegendBuilderPathMarkTests.cs` (the
symbols of the line, rule, trail, area, arc, and text
marks) call the
unit directly through `InternalsVisibleTo`.

#### Deviations From Vega-Lite

The gradient stops follow Vega's rule (the ends of the domain and the ticks for a count of 15), with the offset of each
stop its position along the transformed domain (Vega's `scaleFraction`; the file with that function could not be
read, so the
log, pow, and symlog fractions are derived from the d3 transforms). The canonical scale of a merged continuous
legend is the first channel in Vega-Lite's order
(Vega picks by its own order of scale names). A `titleOrient` other than `top`, `clipHeight`, `encode`, and custom format
types (a `formatType` other than `number`, `time`, and `utc`) are not drawn and are reported by the
feature catalog. The `datum.size` and `datum.offset` of a symbol legend that a `labelExpr` can read are null, and
`config.legend.labelExpr` is not read (Vega-Lite 6 has no such property). A discrete legend over the dates of a nominal
field without a time unit is not temporal (its values are
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

The legend of a **layered view** (phase 10) takes the symbol facts of the first layer that has the channel, and the
properties (`legend` objects) and titles of all the layers as Vega-Lite's `mergeLegendComponent` merges them (see
`LegendPlan.Create(layers, ...)`); Vega-Lite also merges the symbol encodings of the layers, so a legend whose layers
disagree on them (for example a bar layer and a point layer that both color by one field) shows the symbols of the first
layer only.
