### AxisBuilder

![Guides Structure](VegaLiteGuidesView.svg)

The `AxisBuilder` unit is the third software unit in the `Guides` subsystem. It is one source file, `AxisBuilder.cs`,
in the `DemaConsulting.CanvasNet.VegaLite.Guides` namespace, and defines four `internal` types: `AxisBuilder`,
`AxisRequest`, `AxisTick`, and `AxisResult` (sealed records), plus private helper types (the layout and settings of one
axis). The file carries the provenance header comment for the Vega and Vega-Lite axis rules it ports.

#### Purpose

`AxisBuilder` builds the axis guide of a position scale of a single view: the grid lines, the tick marks, the tick labels,
the domain line, and the title, as scene groups positioned at the edge of the data rectangle, and measures how far they
reach beyond it. It exists so that the axis properties, their defaults, configuration precedence, geometry, and
the label-overlap removal of Vega-Lite and Vega are defined in one place, and so that the layout can size the canvas around
the axes without knowing anything about them.

The unit's behavior is specified by the `CanvasNetVegaLite-Guides-AxisBuilder-*` requirements in
`docs/reqstream/canvas-net-vega-lite/guides/axis-builder.yaml`.

#### Data Model

- **`AxisRequest`** `(string Channel, SpecNode? ChannelDef, ResolvedScale Scale, ScaleModel Model, double DataWidth, double
  DataHeight, string? MarkType)` - what the builder needs: the channel (`x` or `y`), its definition in the encoding (whose
  `axis`, `title`, `field`, `aggregate`, and `bin` are read), its resolved scale and the model for the final data rectangle,
  the size of
  that rectangle, and the mark type (a `rect` mark with a discrete channel draws its axis in front of the mark).
- **`AxisTick`** `(DataValue Value, double Position, string Label)` - one tick: the domain value, its position along the
  axis (rounded like the tick mark), and its label text before truncation.
- **`AxisResult`** `(SceneGroup Group, SceneGroup? Grid, SceneBounds Bounds, string Orient, IReadOnlyList<AxisTick> Ticks,
  int ItemCount, bool InFront)` - the axis group (role `axis`, with ticks, labels, domain, and title as children, in drawing
  order), the separate grid group (role `axis-grid-group`, at the same position, or null), the bounds of the axis in the
  data rectangle's coordinates (the grid is not part of them), the side, the ticks (including those whose labels were
  dropped because they overlap), the number of scene items for the budget, and whether the axis is drawn in front of the
  marks (`zindex` above 0).
- **`AxisBuilder`** holds the `TextMeasurer`, the `ResolvedConfig`, the `MaxTicks` and `MaxSceneItems` limits, and the
  source name. `AxisRequest` may carry the `SceneBudget` of the chart; the builder then charges the axis group, the grid
  group, each grid line, tick rule, the remaining labels, the domain line and the title text before it creates them (an
  axis without a shared budget uses a budget of its own from `MaxSceneItems`).

**Properties and precedence.** A property is looked up in the axis of the channel, then the Vega-Lite-only configuration
(`axisX`, `axisY`, and the `Band`, `Point`, `Discrete`, and `Quantitative` variants), then the Vega configuration (`axisBand`
for band scales, `axisTop`, `axisBottom`, `axisLeft`, and `axisRight`, `axisX` and `axisY`, `axis`, including the Vega
defaults), then the `guide-label` and `guide-title` styles. Rules used only when no Vega-Lite configuration names the
property: `grid` is true for a continuous scale with a field or an aggregate (a count has no field) and no bin
(neither a
binned nor a pre-binned field); `labelAngle` is 270 for labels of a nominal or
ordinal x axis (with Vega-Lite's label alignment and baseline for the angle); `labelFlush` is true (1 pixel) for a
quantitative x axis; `labelOverlap` is parity for non-discrete scales; `zindex` is 1 for a `rect` mark with a discrete
channel and otherwise 0.

**Honored properties** are `grid`, `ticks`, `labels`, `domain`, `title` (the axis title, then the channel title, then the
default title that the encoding plan gives the scale in `ResolvedScale.Title` - `Sum of x`, `Count of Records`,
`x (binned)`, two titles of a range merged as `a, b` - then the field name; null hides it;
an array gives lines), `orient` (top or bottom for x, left or right for y), `offset`,
`minExtent`, `maxExtent`, `tickSize`, `tickWidth`, `tickColor`, `tickDash`, `tickDashOffset`, `tickCap`, `tickOpacity`,
`tickRound`, `tickOffset`, `tickCount`, `tickMinStep`, `values`, `format` (d3), `labelAngle`, `labelAlign`,
`labelBaseline`, `labelPadding`, `labelOffset`, `labelLimit`, the label font properties and `labelColor`, `labelOpacity`,
and `labelLineHeight`, `labelFlush`, `labelFlushOffset`, `labelOverlap` (true, `parity`, `greedy`, or false),
`labelSeparation`, `domainColor`, `domainWidth`, and the other domain properties, the `grid` color, width, dash, opacity,
and cap, `titleAnchor`, `titleAngle`, `titleAlign`, `titleBaseline`, `titleColor`, `titleFont*`, `titleLimit`,
`titleLineHeight`, `titleOpacity`, `titlePadding`, `zindex`, and `bandPosition` (the configuration default 0.5). The
properties `labelBound`, `tickBand`, `tickExtra`, `position`, `translate`, `bandPosition` (as an axis property), `titleX`,
`titleY`, `encode`, `labelExpr`, and `formatType` are not drawn and are reported by the feature catalog.

**Geometry** (Vega): the group sits at the edge of the data rectangle shifted half a pixel, so a one-pixel line is
crisp; tick and grid positions are the scale position plus the band offset (half a band, with the `-0.5` correction of
`axisBand`) plus `tickOffset`, rounded half up; labels use the unrounded position plus `labelOffset`; ticks stand `tickSize`
out from the data rectangle and labels `tickSize` plus `labelPadding`; the extent is the ticks and labels clamped to
`minExtent` and `maxExtent`; the title sits `titlePadding` beyond the extent, with multi-line titles pushing further for
the top, left, and right axes; bounds follow Vega (a stroked rule grows by half its width and text by its measured box).

#### Key Methods

- `Build(AxisRequest request, CancellationToken token)` - returns null when the channel's `axis` is null or false;
  otherwise resolves the settings and the orientation, builds the ticks (`BuildTicks`), creates the grid rules, tick rules,
  labels, and domain rule, computes the bounds of ticks and labels, resolves the title, and finishes the groups with the
  extent clamping and title placement. The token is polled per tick.
- Ticks (`BuildTicks`): a continuous scale uses `Ticks.AxisTicks` with Vega-Lite's count (`ceil(size / 40)`, or the number
  of `values`, or `tickCount`) and minimum step, and labels from `NumberFormat.TickFormat` (the default is `,f`; negatives
  use the minus sign U+2212); a field that the encoding bins (`BinKind.Binning`, not a pre-binned one) has the default count
  `ceil(size / 10)` instead, so the ticks follow the bins, and the labels of a normalized stack
  (`ResolvedScale.Stack` is `Normalize`) use `config.normalizedNumberFormat` (`.0%` when the configuration has none) unless
  the axis gives a `format`; explicit `values` follow Vega's `validTicks` (in range, in range order, thinned to the count);
  a band or point scale has a tick for every category (or the existing `values`), labeled with JavaScript-style text of
  the value (numbers formatted with `format` when given).
- Label overlap: Vega's parity method (every second label dropped until none overlap, taking `labelSeparation` into
  account) or its greedy method, performed by the `LabelOverlap` unit that `AxisBuilder` shares with `LegendBuilder`
  (see _LabelOverlap Unit Design_); dropped labels remain in `AxisResult.Ticks`.
- `GetExtents(axes, dataWidth, dataHeight)` (static) - the union of the axes' bounds with the data rectangle, as the
  `ViewExtents` (how far everything reaches beyond each side, never negative), for `ViewLayout`.

**Preconditions and postconditions**: the request carries a scale and a model of the final data rectangle; the result is
immutable scene data.

#### Error Handling

An `axis.orient` that is not valid for the channel and an `axis.format` that is not a valid d3 specifier are
`VegaLiteSpecException`s located at the offending value (the `FormatException` of `NumberFormat` is converted). An axis
with more than `MaxTicks` ticks, categories included, is a `VegaLiteRenderException` located at the channel (the exception
of `Ticks` is rethrown with the location). A canceled token is an `OperationCanceledException`. A null request, measurer,
or configuration is `ArgumentNullException`. A `values` array that is not made of numbers, strings, or booleans is ignored.
The unit performs no I/O.

#### Dependencies

- **`TextMeasurer`** - label and title measurement, in the `TextMeasurer` unit
- **`ScaleModel`**, **`ResolvedScale`**, **`Ticks`**, and **`NumberFormat`** - the `Scales` subsystem
- **`BinKind`** and **`StackOffset`** - the bin kind and the stack offset that a `ResolvedScale` carries (the `Transforms`
  subsystem)
- **`DataValue`** - the `Data` subsystem
- **`SceneGroup`**, **`SceneRule`**, **`SceneText`**, **`SceneItem`**, and **`SceneBounds`** - the `SceneModel` unit
- **`ResolvedConfig`** and **`SpecNode`** - the `SpecModel` subsystem
- **`ViewExtents`** - the `Layout` subsystem
- **`VegaLiteLimits`**, **`VegaLiteLocation`**, **`VegaLiteSpecException`**, and **`VegaLiteRenderException`** - the public
  types of the `VegaLiteDocument` unit
- **.NET base class library** - `System.Globalization`, `System.Collections.Generic`

#### Callers

`VegaLiteDocument` builds the axes of the x and y channels in every layout pass and passes the results to
`ViewLayout` (through `GetExtents`) and into the scene content (grids first, then the axes behind the marks, then the axes
in front). The tests in `test/DemaConsulting.CanvasNet.VegaLite.Tests/Guides/AxisBuilderTests.cs` call the builder directly
through `InternalsVisibleTo`.
