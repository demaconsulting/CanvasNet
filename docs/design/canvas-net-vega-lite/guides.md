## Guides

![Guides Structure](VegaLiteGuidesView.svg)

### Overview

The `Guides` subsystem (namespace `DemaConsulting.CanvasNet.VegaLite.Guides`, folder
`src/DemaConsulting.CanvasNet.VegaLite/Guides/`) builds the parts of a chart that are not marks: it resolves fonts and
measures text, builds the title and subtitle, builds the axes (grid, ticks, labels, domain, title) together with the
room they need, and builds the legends (symbol, gradient, and discrete gradient) together with the box each needs. It
is **internal**: its
types are visible only to the other subsystems of the system and, through `InternalsVisibleTo`, to the test assembly. It
contains five units:

- **TextMeasurer** - the internal `TextMeasurer` with `TextFont`, `ResolvedFont`, `FontSource`, `TextExtent`, and
  `FontMetrics`: font resolution (bundled Liberation fonts by default), text measurement, line splitting, truncation, and
  Vega's text bounds; see _TextMeasurer Unit Design_ (`guides/text-measurer.md`)
- **TitleBuilder** - the internal `TitleBuilder` and `TitleBlock`: the title and subtitle scene items and their placement;
  see _TitleBuilder Unit Design_ (`guides/title-builder.md`)
- **AxisBuilder** - the internal `AxisBuilder` with `AxisRequest`, `AxisTick`, and `AxisResult`: one axis of a position
  scale and its extents; see _AxisBuilder Unit Design_ (`guides/axis-builder.md`)
- **LabelOverlap** - the internal static `LabelOverlap`: Vega's parity and greedy removal of overlapping labels, shared
  by the axes and the gradient legends; see _LabelOverlap Unit Design_ (`guides/label-overlap.md`)
- **LegendBuilder** - the internal `LegendPlan`, `LegendBuilder`, and their records: which channels have legends, how
  they merge, and the symbol, gradient, and discrete gradient legend as scene items with the box each needs; see
  _LegendBuilder Unit Design_ (`guides/legend-builder.md`)

The subsystem's namespace documentation is in `Guides/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetVegaLite-Guides-*` requirements in `docs/reqstream/canvas-net-vega-lite/guides.yaml`.

### Interfaces

**Exposed** (internal only): `new TextMeasurer(VegaLiteFontOptions?)` with `Resolve`, `MeasureWidth`, `Measure`,
`Truncate`, `TextValue`, `GetBounds`, `GetMetrics`, and the static helpers `IsBold`, `IsItalic`, `SplitFamilies`,
`SplitLines`, `LineHeight`, `BaselineOffset`, and `RoundHalfUp`; `new TitleBuilder(TextMeasurer).Build(SpecNode?,
ResolvedConfig, bool)`, which returns a `TitleBlock` whose `Place` produces the positioned title group;
`new AxisBuilder(TextMeasurer, ResolvedConfig, VegaLiteLimits?, string?).Build(AxisRequest, CancellationToken)`, which
returns an `AxisResult`, and the static `AxisBuilder.GetExtents(axes, dataWidth, dataHeight)`;
`LegendPlan.Create(SpecNode, ResolvedScales, ResolvedConfig, string?)`, which returns the planned `LegendSpec`s, and
`new LegendBuilder(TextMeasurer, VegaLiteLimits?, string?).Build(LegendSpec, double, double, SceneBudget, CancellationToken)`,
which returns a `LegendResult`, with the static `LegendBuilder.GridLayout(boxes, options)` (the grid layout that the
`Layout` subsystem also uses to place the legends); the static `LabelOverlap.Hidden(...)`.

**Consumed**: `SpecNode` and `ResolvedConfig` from the `SpecModel` subsystem; `ScaleModel`, `ResolvedScale`, `Ticks`, and
`NumberFormat` from the `Scales` subsystem; `DataValue` from the `Data` subsystem; the scene types (`SceneGroup`,
`SceneRule`, `SceneText`, `SceneBounds`, and the others) from the `Scenegraph` subsystem; `ViewExtents` from the `Layout`
subsystem; the public `VegaLiteFontOptions`, `VegaLiteLimits`, `VegaLiteLocation`, `VegaLiteSpecException`, and
`VegaLiteRenderException` of the `VegaLiteDocument` unit; and the core `Fonts` (`TrueTypeFont`, `SystemFontCatalog`) and
`Rendering` (`TextRenderer`) subsystems through their public API.

**Consumers**: `VegaLiteDocument` (the pipeline creates the measurer, the title, and the axes), `ViewLayout` (reads the
`TitleBlock`), and `ScenePainter` (draws text with the same measurer, so that what was measured is what is drawn).

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

**Precedence mirrors Vega-Lite.** A guide property is looked up in the guide's own definition, then the Vega-Lite-only
configuration, then the Vega configuration (by orientation, band scale, and axis), then the guide styles, with a few
Vega-Lite rules that apply only when no configuration names the property (for example the default grid, the label angle
of a discrete x axis, and the label overlap strategy).

**Limits and cancellation.** An axis with more than `MaxTicks` ticks (categories included) fails closed with a located
`VegaLiteRenderException`; a legend is bounded by `MaxScaleDomainSize` (values and domains), `MaxTicks` (ticks), `symbolLimit`
(entries), `MaxStringLength` (labels), and the scene budget, all before anything is created; the font directory scan is
bounded (at most 4,096 files, depth 4, ordinal order, unusable files skipped); the cancellation token is polled per tick
and every 1,024 legend entries.
