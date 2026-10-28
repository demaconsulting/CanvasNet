## Layout

![Layout Structure](VegaLiteLayoutView.svg)

### Overview

The `Layout` subsystem (namespace `DemaConsulting.CanvasNet.VegaLite.Layout`, folder
`src/DemaConsulting.CanvasNet.VegaLite/Layout/`) computes the boxes of a chart: the data rectangle, the padding, the
canvas that holds everything, the place of the title, and the background and border of the view. It is **internal**: its
types are visible only to the other subsystems of the system and, through `InternalsVisibleTo`, to the test assembly. It
contains one unit:

- **ViewLayout** - the internal static `ViewLayout` with its request, measure, and result records; see _ViewLayout Unit
  Design_ (`layout/view-layout.md`)

The subsystem's namespace documentation is in `Layout/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetVegaLite-Layout-*` requirements in `docs/reqstream/canvas-net-vega-lite/layout.yaml`. In the first delivery the
subsystem lays out a **single view**; the composition layout (layers, concatenation, facets, repeats) is a later phase.

### Interfaces

**Exposed** (internal only): `ViewLayout.Resolve(SpecNode?, ResolvedConfig, ViewSizeHints, string?)`, which returns a
`ViewLayoutRequest`; `ViewLayout.Measure(request, ViewExtents, TitleBlock?)`, which returns a `ViewMeasure`;
`ViewLayout.Build(request, measure, content)`, which returns a `Scene`; and `ViewLayout.Compute(request, extents, title,
content)`, which does both and returns a `ViewLayoutResult`. The records `ViewPadding`, `ViewExtents`, `ViewStyle`,
`ViewSizeHints`, `ViewLayoutRequest`, `ViewMeasure`, and `ViewLayoutResult` and the enumeration `AutosizeType` carry the
data.

**Consumed**: `SpecNode` and `ResolvedConfig` from the `SpecModel` subsystem; `TitleBlock` from the `Guides` subsystem;
the scene types from the `Scenegraph` subsystem; and the public `VegaLiteSize` of the `VegaLiteDocument` unit.

**Consumers**: `VegaLiteDocument` (the pipeline resolves, measures, and builds), and `AxisBuilder`, whose `GetExtents`
returns the `ViewExtents` record that `Measure` consumes.

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
continuous view size (300 by 300), or, for a discrete scale, the size computed from the step; `"container"` takes the
container size from the options, less the padding and the guides at the sides. With `autosize: pad` (the default) the canvas
grows to hold everything that reaches beyond the data rectangle; with `none` it is the stated size plus the padding (or
exactly the stated size when `contains` is `padding`) and anything that does not fit is cut off. The other autosize types
are reported by the feature catalog and laid out as `pad`.
