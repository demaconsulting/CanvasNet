### LegendBuilder

![Guides Structure](VegaLiteGuidesView.svg)

The `LegendBuilder` unit is the fifth unit in the `Guides` subsystem. It is five source files in the
`DemaConsulting.CanvasNet.VegaLite.Guides` namespace: `LegendPlan.cs` (the planning types and the static class
`LegendPlan`), `LegendBuilder.cs` (the sealed partial class `LegendBuilder` with its result records),
`LegendBuilder.Symbols.cs` (symbol legends), `LegendBuilder.Gradient.cs` (gradient and discrete gradient legends), and
`LegendBuilder.Grid.cs` (the grid layout of Vega's `layout` transform). The files carry the provenance header comment
`Portions derived from Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause)`.

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
  distinct default titles of the members joined by a comma.
- `LegendBuilder.Build(spec, dataWidth, dataHeight, budget, cancellationToken)` - charges the legend group, the title
  and the entry group, builds the content, then performs Vega's `legendGroupLayout`: the entries sit at the padding,
  below the title by its height plus the title padding; a middle or end anchored title is positioned against the width
  of the entries (the bar alone for a horizontal gradient); a title that reaches left of the padding pushes everything
  right; the box is `ceil(right + padding)` by `ceil(bottom + padding)`.
- Symbol legends - the values are the explicit `values`, the domain of a discrete scale, the d3 ticks of a continuous
  scale (the tick count is `tickCount` or 5, reduced by `tickMinStep`), or for a discretizing scale minus infinity and
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
  legend with several columns fills them from top to bottom.
- Gradient legends - the bar is `gradientThickness` (16) by the gradient length, which is `gradientLength`, else for a
  continuous scale the view height limited to 64 to 200 (vertical) or the view width limited to 100 to 200 for top and
  bottom (100 for other horizontal legends), else 200. Its `FillGradient` has the stops of Vega's `scaleGradient`: the ends
  of the domain and the ticks for a count of 15, each at its linear position with the color of the scale (a domain
  without span is replaced by 0 to 1 with the interpolator). The labels are the ticks for a count of `tickCount` or
  `max(2, 2 * floor(length / 100))` (the two ends when there are fewer than three), formatted by the d3 tick format,
  placed outside the bar at `gradientLabelOffset` (2) or `labelOffset`, aligned by position, and thinned by
  `LabelOverlap` (the method of `labelOverlap`, greedy for quantile and threshold scales, else parity). A discrete
  gradient has one band per class (the colors of the scale at minus infinity and the thresholds, positioned by the linear
  fraction of the domain, a threshold domain widened by one class at each end) and a label for each boundary.

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
stops; no dictionary enumeration order reaches the output.

#### Dependencies

`TextMeasurer`, `LabelOverlap`, `ResolvedScale`, `ScaleModel`, `Ticks`, `NumberFormat`, `RgbColor` and the discretizing
scale kinds of the `Scales` subsystem; `SpecNode`, `ConfigNode` and `ResolvedConfig` of the `SpecModel` subsystem;
`DataValue` and `JsNumber` of the `Data` subsystem; `ExpressionEvaluator.ToJsString` and `ExpressionFunctionContext` of
the `Expressions` subsystem; the scene types and `SceneBudget` of the `Scenegraph` subsystem; and the public
`VegaLiteLimits`, `VegaLiteLocation`, `VegaLiteRenderException` and `VegaLiteSpecException`.

#### Callers

`VegaLiteDocument` and `LegendLayout` of the `Layout` subsystem (which also calls `LegendBuilder.GridLayout`). The
tests in `test/DemaConsulting.CanvasNet.VegaLite.Tests/Guides/LegendPlanTests.cs` and `LegendBuilderTests.cs` call the
unit directly through `InternalsVisibleTo`.

#### Deviations From Vega-Lite

The gradient stops follow Vega's rule (the ends of the domain and the ticks for a count of 15), with the offset of each
stop its linear position. The canonical scale of a merged continuous legend is the first channel in Vega-Lite's order
(Vega picks by its own order of scale names). A `titleOrient` other than `top`, `clipHeight`, `encode`, `labelExpr`
and custom format types are not drawn and are reported by the feature catalog. The symbols of the `arrow`, `wedge` and
`triangle` shapes are bounded by the box of their path. `legend.symbolType` is combined with the configuration's
`symbolType` only for marks that default to a circle. A bin-ordinal scale has a symbol legend with range labels.
