## Layout

![Layout Structure](VegaLiteLayoutView.svg)

### Overview

The `Layout` subsystem (namespace `DemaConsulting.CanvasNet.VegaLite.Layout`, folder
`src/DemaConsulting.CanvasNet.VegaLite/Layout/`) computes the boxes of a chart: the data rectangle, the padding, the
canvas that holds everything, the place of the title, and the background and border of the view. It is **internal**: its
types are visible only to the other subsystems of the system and, through `InternalsVisibleTo`, to the test assembly. It
contains three units:

- **ViewLayout** - the internal static `ViewLayout` with its request, measure, and result records; see _ViewLayout Unit
  Design_ (`layout/view-layout.md`)
- **LegendLayout** - the internal static `LegendLayout` with the `LegendPlacement` record, which places the built legends
  around the data rectangle and the axes and joins their reach with that of the axes; see _LegendLayout Unit Design_
  (`layout/legend-layout.md`)
- **CompositionLayout** - the internal static partial `CompositionLayout` with the `CompositionCell`, `HeaderGroup`,
  `CompositionHeaders`, and `CompositionResult` records, a port of Vega's grid layout and trellis layout that places the
  cells of a concatenation or a facet in a grid with the headers, footers, and titles of a facet; see _CompositionLayout
  Unit Design_ (`layout/composition-layout.md`)

The subsystem's namespace documentation is in `Layout/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetVegaLite-Layout-*` requirements in `docs/reqstream/canvas-net-vega-lite/layout.yaml`. The subsystem lays out
**one view** with `ViewLayout`: a single view or a layered view, which is laid out as one view whose data rectangle is
shared by all its layers (the size comes from the top-level view, and the discrete step hints from the **combined**
discrete scale of the layers). For composed charts (concatenation, facets, repeats) `ViewLayout` also provides the
helpers of one chart in a composition (`ResolveSize`, `CellBounds`, and the composition root of `Resolve`) and
`CompositionLayout` places the cells with the headers, footers, and titles of a facet; both are pure and are called by the
`Composer` of the rendering pipeline for every concatenation and facet.

### Interfaces

**Exposed** (internal only): `ViewLayout.Resolve(SpecNode?, ResolvedConfig, ViewSizeHints, string?)`, which returns a
`ViewLayoutRequest`; `ViewLayout.Measure(request, ViewExtents, TitleBlock?)`, which returns a `ViewMeasure`;
`ViewLayout.Build(request, measure, content)`, which returns a `Scene`; `ViewLayout.Compute(request, extents, title,
content)`, which does both and returns a `ViewLayoutResult`; and `LegendLayout.Place`, `AxisBounds`, and `Combine`, and
for compositions `ViewLayout.ResolveSize`, `ViewLayout.CellBounds`, `ViewLayout.Resolve(..., composition: true)`, and
`CompositionLayout.Place(cells, headers, settings)`. The records `ViewPadding`, `ViewExtents`, `ViewStyle`,
`ViewSizeHints`, `ViewSize`, `ViewLayoutRequest`, `ViewMeasure`, and `ViewLayoutResult`, the enumeration `AutosizeType`,
and the records of `CompositionLayout` carry the data.

**Consumed**: `SpecNode`, `ResolvedConfig`, and `CompositionSettings` from the `SpecModel` subsystem; `TitleBlock`,
`AxisResult`, and `LegendResult` (with the grid layout of `LegendBuilder`, which `CompositionLayout` reuses) from the
`Guides` subsystem; the scene types from the `Scenegraph` subsystem; and the public `VegaLiteSize` of the
`VegaLiteDocument` unit.

**Consumers**: `VegaLiteDocument` (the pipeline resolves, measures, and builds the scene, and places the legends), and
`AxisBuilder`, whose `GetExtents` returns the `ViewExtents` record that `Measure` consumes and that
`LegendLayout.Combine` widens.

### Design

**Two phases, as in Vega.** Layout runs in two steps so that content can be measured before it is placed. `Resolve` reads
the settings that do not depend on the content (the data rectangle from `width`, `height`, and the view configuration, the
padding, `autosize`, the plot background, and the canvas background). The guide builders then measure themselves against
the data rectangle and report how far they reach (`ViewExtents`) and build the title; `Measure` places the title, computes
the bounds of everything and from them the canvas size; and `Build` assembles the scene around caller-supplied content.
`Compute` runs both. Because the natural size of a chart depends on the data and the axes, the pipeline calls `Measure`
repeatedly (at most three passes) until the data rectangle is stable.

**The scene is the only product.** The subsystem returns an immutable `Scene` (root group, view group with the plot
background and border, the content in the order given, then the title) and the natural size; it draws nothing, so its
numbers can be asserted analytically without pixels.

**Sizes follow Vega-Lite 6.** The data rectangle is the number in `width` and `height`; without one it is the configured
continuous view size (300 by 300), or, for a discrete scale, the size computed from the step; `"container"` is Vega-Lite's
fit to the container size from the options (`autosize: fit` with `contains: padding`). With `autosize: pad` (the default)
the canvas grows to hold everything that reaches beyond the data rectangle; with `none` it is the stated size plus the
padding (or exactly the stated size when `contains` is `padding`) and anything that does not fit is cut off; with `fit`,
`fit-x`, or `fit-y` the stated size (or the container) is the size of the whole view and the data rectangle is what the
guides, the title, and the padding leave of it, so the canvas has exactly that size (Phase 11).

**Legends.** After the axes are built, the legends are built for the same data rectangle and `LegendLayout` places them
against the data rectangle united with the axes on their side; the union of their bounds joins the extents of the axes
(`LegendLayout.Combine`) before the title is laid out, so a legend outside the data rectangle grows the canvas with
`autosize: pad` and sticks out with `none`; for a fit only the extent of a legend across its side counts, as in Vega. Each
pass of the iteration builds the axes and the legends again against the
new data rectangle.

**Compositions.** A concatenation or a facet has no data rectangle of its own: every chart in it sizes itself
(`ResolveSize`) and reports the bounds of its cell (`CellBounds`: data rectangle, guides, title, and the half stroke of
its border), `CompositionLayout.Place` positions the cells (spacing, columns, alignment, centering, full or flush
bounds) and the headers, footers, and titles of a facet, and the root is resolved with `composition: true` (a 0 by 0
root, padded) so that `Measure` pads the bounds of the whole. The grid is the `LegendBuilder.GridLayout` port that the
legends share; the trellis part is new. Nothing here knows about data, text, or fonts: the inputs are boxes, so the
numbers are derived by hand.
