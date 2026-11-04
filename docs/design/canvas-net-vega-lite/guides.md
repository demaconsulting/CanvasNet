## Guides

![Guides Structure](VegaLiteGuidesView.svg)

### Overview

The `Guides` subsystem (namespace `DemaConsulting.CanvasNet.VegaLite.Guides`, folder
`src/DemaConsulting.CanvasNet.VegaLite/Guides/`) builds the parts of a chart that are not marks: it resolves fonts and
measures text, builds the title and subtitle, builds the axes (grid, ticks, labels, domain, title) together with the
room they need, and builds the legends (symbol, gradient, and discrete gradient) together with the box each needs; the
axes and legends of temporal fields (time ticks, date labels) share one label-format unit, and the `labelExpr` of axes,
legends, and facet headers is evaluated by one shared unit. It is **internal**: its
types are visible only to the other subsystems of the system and, through `InternalsVisibleTo`, to the test assembly. It
contains seven units:

- **TextMeasurer** - the internal `TextMeasurer` with `TextFont`, `ResolvedFont`, `FontSource`, `TextExtent`, and
  `FontMetrics`: font resolution (bundled Liberation fonts by default), text measurement, line splitting, truncation, and
  Vega's text bounds; see _TextMeasurer Unit Design_ (`guides/text-measurer.md`)
- **TitleBuilder** - the internal `TitleBuilder` and `TitleBlock`: the title and subtitle scene items and their placement;
  see _TitleBuilder Unit Design_ (`guides/title-builder.md`)
- **AxisBuilder** - the internal `AxisBuilder` with `AxisRequest`, `AxisTick`, and `AxisResult`: one axis of a position
  scale (including a time or UTC scale and the axis of a time unit) and its extents; see _AxisBuilder Unit Design_
  (`guides/axis-builder.md`)
- **LabelOverlap** - the internal static `LabelOverlap`: Vega's parity and greedy removal of overlapping labels, shared
  by the axes and the gradient legends; see _LabelOverlap Unit Design_ (`guides/label-overlap.md`)
- **LegendBuilder** - the internal `LegendPlan`, `LegendBuilder`, and their records: which channels have legends, how
  they merge, and the symbol, gradient, and discrete gradient legend as scene items with the box each needs (the legends
  of temporal fields are in `LegendBuilder.Time.cs`; the symbol of each mark type, including the stroke symbol of a line,
  rule, and trail and the filled circle of an area, arc, and text mark, is decided here); see _LegendBuilder Unit Design_
  (`guides/legend-builder.md`)
- **TimeLabels** - the internal static `TimeLabels` (with its `Options` record): the label format of a date (the
  `format`, `formatType`, time unit, and `config.timeFormat` rules), the interval `tickCount` request, the default
  tick count of a time axis, and the reader of an explicit list of dates, shared by `AxisBuilder` and `LegendBuilder`
  (and, through the bounded form `CreateBounded` that leaves the budget to its caller, by the text mark of `Marks`);
  see _TimeLabels Unit Design_ (`guides/time-labels.md`). The date formatting itself (the d3 time formats and Vega's
  multi-format) and the calendar are in the `Data` subsystem (plan decision P8-D1), not here
- **LabelExpressions** - the internal sealed `LabelExpressions` and the `LabelText` record struct: the evaluation of
  the `labelExpr` of axes, legends, and facet headers on the shared evaluator of the call (the datum of each guide,
  the coercion of the result like Vega's text mark, and the compile of configured text), shared by `AxisBuilder`,
  `LegendBuilder`, and the facet headers of `VegaLiteDocument`; see _LabelExpressions Unit Design_
  (`guides/label-expressions.md`)

The subsystem's namespace documentation is in `Guides/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetVegaLite-Guides-*` requirements in `docs/reqstream/canvas-net-vega-lite/guides.yaml`.

### Interfaces

**Exposed** (internal only): `new TextMeasurer(VegaLiteFontOptions?)` with `Resolve`, `MeasureWidth`, `Measure`,
`Truncate`, `TextValue`, `GetBounds`, `GetMetrics`, and the static helpers `IsBold`, `IsItalic`,
`SplitLines`, `LineHeight`, `BaselineOffset`, and `RoundHalfUp`; `new TitleBuilder(TextMeasurer).Build(SpecNode?,
ResolvedConfig, bool)`, which returns a `TitleBlock` whose `Place` produces the positioned title group;
`new AxisBuilder(TextMeasurer, ResolvedConfig, VegaLiteLimits?, string?, TimeCalendar?, RowExpressions?).Build(AxisRequest,`
`CancellationToken)`, which returns an `AxisResult`, and the static `AxisBuilder.GetExtents(axes, dataWidth, dataHeight)`;
`LegendPlan.Create(SpecNode, ResolvedScales, ResolvedConfig, string?)`, which returns the planned `LegendSpec`s, and
`new LegendBuilder(TextMeasurer, VegaLiteLimits?, string?, RowExpressions?).Build(LegendSpec, double, double, SceneBudget,`
`CancellationToken)`, which returns a `LegendResult`, with the static `LegendBuilder.GridLayout(boxes, options)` (the
grid layout that the
`Layout` subsystem also uses to place the legends); the static `LabelOverlap.Hidden(...)`; the static `TimeLabels`
(`Create(Options)`, `CreateBounded(Options)`, `IntervalRequest`, `UsesVegaDefaultCount`, and `ReadInstant`); and
`new LabelExpressions(RowExpressions?, VegaLiteLimits?, TimeCalendar?, CancellationToken)` with `Datum`, `Resolve`, and
`Evaluate`.

**Consumed**: `SpecNode` and `ResolvedConfig` from the `SpecModel` subsystem; `ScaleModel`, `ResolvedScale`, `Ticks`
(including the time ticks), and `NumberFormat` from the `Scales` subsystem; `DataValue` and the time model
(`TimeCalendar`, `TimeUnitSpec`, `DateTimeObject`, `TimeFormat`, and `JsDateParser`) from the `Data` subsystem;
`RowExpressions`, `CompiledExpression`, and the shared evaluator from the `Expressions`
subsystem; the scene
types (`SceneGroup`,
`SceneRule`, `SceneText`, `SceneBounds`, and the others) from the `Scenegraph` subsystem; `ViewExtents` from the `Layout`
subsystem; the public `VegaLiteFontOptions`, `VegaLiteLimits`, `VegaLiteLocation`, `VegaLiteSpecException`, and
`VegaLiteRenderException` of the `VegaLiteDocument` unit; and the core `Fonts` (`TrueTypeFont`, `SystemFontCatalog`) and
`Rendering` (`TextRenderer`) subsystems through their public API.

**Consumers**: `VegaLiteDocument` (the pipeline creates the measurer, the title, and the axes), `ViewLayout` (reads the
`TitleBlock`), `ScenePainter` (draws text with the same measurer, so that what was measured is what is drawn), and
`MarkBuilder` (the text mark formats the time labels of its rows with `TimeLabels.CreateBounded`).

### Design

**One measurer per render.** A `TextMeasurer` is created for the font options of the render and shared by every unit that
measures or draws text, so that measurement and painting can never disagree and the fonts are resolved once. It resolves
CSS font requests to the bundled Liberation fonts for the generic and Arial-like families, so charts render identically
on every machine regardless of the installed fonts; only a family that the specification names explicitly is looked
up in the additional directories and, when allowed, in the installed fonts.

**Vega's text layout, not the font's metrics.** Layout follows the rules Vega uses for text: a line is as tall as the font
size, successive lines are a line height (font size plus 2) apart, and the baseline of a text item is shifted from its
anchor by fixed fractions of the font size. This keeps the guide geometry independent of the metrics of one font; only the
advance widths come from the font.

**Builders produce scene items and extents, not pixels.** `TitleBuilder`, `AxisBuilder`, and `LegendBuilder` return
immutable scene groups
positioned in the coordinates of the data rectangle together with the bounds they occupy; `ViewLayout` uses the bounds to
size and place everything, and `ScenePainter` draws the items. The title is built at the origin and placed in a second
step, because with the `bounds` frame it is centered over everything the view contains, which is only known after the axes
are measured. The axes are built against a data rectangle that can itself depend on the axes (a `"container"` size),
so the pipeline iterates the two until the rectangle is stable.

**Layers share guides by planning, not by the builders.** A layered view has one data rectangle; which axes and legends
it
has is decided before the builders run (`VegaLiteDocument.Layers.cs`; see _VegaLiteDocument Unit Design_): the layers that
share a position scale and an axis give one `AxisRequest` (the second axis of an independent channel carries a
`DefaultOrient` of the opposite side and `DefaultGrid` false), and `LegendPlan` plans the legends over all the layers. The
builders themselves are unchanged apart from those two optional request properties and the multi-layer `Create` of
`LegendPlan`.

**Composed views add header guides, not new builders.** The cells of a facet share their axes: Vega-Lite moves the main
part of a shared axis into the header or footer of the facet and leaves the grid in the cells. `AxisRequest.InHeader`
builds that axis in the coordinates of its header (the grid group stays where the cell draws it), `HeaderSettings`
resolves the `header` properties of a facet channel over `config.headerRow`/`headerColumn`/`headerFacet` and
`config.header`, and the header part of `TitleBuilder` (`BuildHeaderBlock`, `BuildHeader`) builds the label of a cell
and the title of a channel in the `guide-label` and `guide-title` styles and the header group that holds them with the
shared axes, sized for `CompositionLayout`. The legend that composed views share is planned once for the view that owns
it (`LegendLayer.Node`, `LegendLayer.OwnerOf`, `LegendSpec.Owner`, `LegendPlan.Owners`, and the owner argument of
`LegendPlan.Create`). Nothing here depends on the pipeline; the facet and concatenation work packages compose these
pieces.

**Precedence mirrors Vega-Lite.** A guide property is looked up in the guide's own definition, then the Vega-Lite-only
configuration, then the Vega configuration (by orientation, band scale, and axis), then the guide styles, with a few
Vega-Lite rules that apply only when no configuration names the property (for example the default grid, the label angle
of a discrete x axis, and the label overlap strategy).

**Limits and cancellation.** An axis with more than `MaxTicks` ticks (categories included) fails closed with a located
`VegaLiteRenderException`; a legend is bounded by `MaxScaleDomainSize` (values and domains), `MaxTicks` (ticks), `symbolLimit`
(entries), `MaxStringLength` (labels), and the scene budget, all before anything is created; the font directory scan is
bounded (at most 4,096 files, depth 4, ordinal order, unusable files skipped); the cancellation token is polled per tick
and every 1,024 legend entries. The ticks of a time axis or legend are bounded by `MaxTicks` before any label is made,
and a date label longer than `MaxStringLength` is a located `VegaLiteRenderException` that names the limit.

**Time guides are the same builders with one shared rule set.** A time or UTC scale takes the d3 time ticks of its
calendar (the interval nearest to the span over the tick count, or the interval a `tickCount` names), a temporal color
gets a gradient legend over time ticks, and the dates of a time-unit field are labeled by the unit's specifier; the label
format follows Vega-Lite (`format`, then the unit specifier, then `config.timeFormat` for a legend and Vega's
multi-format for an axis) and lives in `TimeLabels`, so an axis and a legend format the same date the same way. All
names are the English d3 locale with invariant digits, and the calendar is that of the chart (the `TimeZone` render
option, UTC by default). `labelBound` is not evaluated.

**Label expressions run before the labels are laid out.** The `labelExpr` of an axis, a legend, and a facet header is evaluated
by `LabelExpressions` on the evaluator of the call, so it shares the step budget, the string limit, the cancellation
token, and the parameters of every other expression. The replacement happens after the ticks and entries (and the
labels they would have) are known and before the labels are measured, laid out, and thinned, so the extent of the axis,
`labelLimit`, the overlap removal, and the legend box all see the replaced text. A result that is an array is a multi-line
label. The datum has `value` and `label` and, by guide, `index`, `tickIndex`, `perc`, and `perc2` (see _LabelExpressions
Unit Design_). A syntax error is found when the document is opened; the feature catalog reports only what an expression
cannot do (`vl-expr-function-<name>`, `vl-expr-view-state`, `vl-expr-construct-<name>`).
