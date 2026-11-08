### ViewLayout

![Layout Structure](VegaLiteLayoutView.svg)

The `ViewLayout` unit is the first software unit in the `Layout` subsystem. It is one source file, `ViewLayout.cs`, in
the `DemaConsulting.CanvasNet.VegaLite.Layout` namespace, and defines ten `internal` types: `ViewLayout` (static class),
`ViewPadding`, `ViewExtents`, `ViewSizeHints`, and `ViewSize` (readonly record structs), `AutosizeType` (enumeration),
and `ViewStyle`, `ViewLayoutRequest`, `ViewMeasure`, and `ViewLayoutResult` (sealed records).

#### Purpose

`ViewLayout` lays out a single view, or a layered view as one view (the layers share the data rectangle, the axes, and the
title; the unit is unchanged by layers, which differ only in the `ViewSizeHints` they are given): the size of the data
rectangle, the padding, the room the guides and the title need,
the size of the canvas that holds them, and the scene around the content that the builders produced. It exists so that the
view-layout rules of Vega and Vega-Lite (default size, step sizing, padding, `autosize`, title bands, view background and
border) are defined once and can be tested by analytic numbers without drawing anything.

The unit's behavior is specified by the `CanvasNetVegaLite-Layout-ViewLayout-*` requirements in
`docs/reqstream/canvas-net-vega-lite/layout/view-layout.yaml`.

#### Data Model

- **`ViewPadding`** `(Left, Top, Right, Bottom)` with `Horizontal` and `Vertical` sums. **`ViewExtents`** `(Left, Top,
  Right, Bottom)` - how far the guides reach beyond the data rectangle on each side (zero or more).
- **`AutosizeType`** - `Pad` (the canvas grows around the data rectangle to hold the guides and the title), `None` (the
  canvas is the given size and the guides may overflow it), or `Fit`, `FitX`, `FitY` (the width and/or height is the size
  of the **whole view**: the data rectangle shrinks to leave room for the guides, the title, and, for `contains:
  padding`, the padding; a dimension that is not fitted pads).
- **`ViewStyle`** - the fill, stroke, stroke width, opacities, dash, and corner radius of the plot area (CSS color strings
  or null).
- **`ViewSizeHints`** `(DiscreteWidth, DiscreteHeight, ContainerWidth, ContainerHeight)` - the sizes that the scale stage
  (from the step of a discrete scale; for a layered view the step times the **combined** categories of the layers, from
  the merged scale) and the host (the container size) provide.
- **`ViewLayoutRequest`** - the data rectangle size, padding, autosize, `ContainsPadding`, `ViewStyle`, the canvas
  `Background`, an optional `Clip` to the data rectangle, the container sizes (the size a fitted `"container"`
  dimension fills), `IsFit`, and `DroppedFit` (the dimension, `x`, `y`, or `both`, whose fit a step size dropped,
  for the report). It is produced by `Resolve` or built directly by tests.
- **`ViewMeasure`** - the final data rectangle, the bounds of everything in the view's coordinates, the padding, the
  origin of the data rectangle on the canvas, the canvas `Width` and `Height` (possibly fractional), the placed title
  group and its bounds, and `NaturalSize`, the canvas size in whole pixels (rounded half away from zero, clamped to the
  range of an integer by `NumericConversions`, so NaN and negative infinity are 0 on every runtime) as a
  `VegaLiteSize`. A size that is not a finite number never gets here: the document refuses it with a located
  `VegaLiteRenderException` when the view is measured (phase 14).
- **`ViewLayoutResult`** - the `ViewMeasure` and the `Scene`.

**Resolution rules.** The data rectangle is the number in `width` and `height`; a `"container"` size takes the container
size from the hints; a `{step}` size or no size takes the discrete size computed by the scale stage, else the configured
continuous size (`view.continuousWidth` and `continuousHeight`, 300 by 300 in Vega-Lite 6, overridden by the `view` of
the specification). The padding is a number for all sides or an object whose missing sides are 0, from the
specification, then the configuration, then 5 on all sides. The **autosize** is Vega-Lite's merge
(`normalizeAutoSize`): `pad` (with `contains: content`), then the fit that a `"container"` size implies (`fit` for
both sizes, `fit-x` for the width, `fit-y` for the height, with `contains: padding`), then `config.autosize` when the
configuration sets one (the built-in default `pad` is recognized as the very node the merge leaves in place and does
not count), then the `autosize` of the specification (a type name, or an object whose `type` and `contains` each
override). A fit type is then dropped for a dimension that a step sizes (an explicit `{step}`, or no size on a
discrete scale, which is when the scale stage gave a size): for both it becomes `pad`; for one it becomes the fit of
the **other** dimension, as Vega-Lite's `compile.ts` does (so `fit-x` with a step width becomes `fit-y`); only a
change is recorded in `DroppedFit`. A `"container"` size whose dimension is not fitted (the specification says `pad`
or `none`) is the container size as the data rectangle, as Vega-Lite's `width` signal is. The canvas background is
the caller's override, then the specification's `background`, then the configuration's, then white. The plot style
comes from `ResolveViewProperty`: the `cell` style, the `view` configuration, and the Vega cell style (transparent
fill, `#ddd` stroke).

**Measure rules.** With `pad` the canvas is the data rectangle plus the whole pixels the bounds reach on each side
(rounded up) plus the padding; with `none` it is the width and height plus padding, or exactly the width and height if
`contains` is `padding` (the data rectangle is then what is left after the padding). With a fit type (Vega's
`viewSizeLayout`) the fitted size of a dimension is the container size (or the number in `width` or `height`), less
the padding when `contains` is `padding`, and the data rectangle is that size less the reach of the guides **and the
title** on each side, each rounded up to whole pixels and never below 0; the canvas is that size (plus the padding
when `contains` is `content`), so it never grows for the guides, and the origin of the data rectangle is the padding
plus the rounded reach on the left and top. The title is placed against the data rectangle, which it also shapes (a
wide title takes room from the width), so the fit is iterated up to three times; a title above or below, whose reach
does not depend on the width, settles in one. The reach to the right and below is taken from the guides and the placed
title rather than by subtracting the data rectangle from the bounds, so a guide of whole pixels is not rounded up by
floating-point error. A clipped view has no reach. The title is placed against the bounds of the data rectangle and
guides and its bounds are added. The view group's border is not part of the bounds, so it does not enlarge the
canvas. For a fit the legends are counted only across their side (`LegendLayout.Combine` with the fit flag, see the
`LegendLayout` unit).

**Scene shape.** The root group holds the view group, translated to the data rectangle's origin and carrying the plot
background and border (the group paints its own fill and stroke, as a Vega group does); the content is its children in the
order given, followed by the title group.

#### Key Methods

- `Resolve(SpecNode? root, ResolvedConfig config, ViewSizeHints hints, string? backgroundOverride)` - reads the size,
  padding, autosize, background, and view style as described above and returns the request.
- `Measure(request, ViewExtents guides, TitleBlock? title)` - computes the final data rectangle (fit sizes and
  `contains: padding` applied), the bounds with the title, the canvas size for `pad`, `none`, or a fit type, and the
  origin of the data rectangle on the canvas.
- `Build(request, measure, IEnumerable<SceneItem> content)` - assembles the scene.
- `Compute(request, guides, title, content)` - `Measure` followed by `Build`.
- `Resolve(..., bool composition)` - with `composition` set, the request of the **root of a concatenation or a facet**:
  the data rectangle is 0 by 0, there is no container, no plot style, and the autosize is `pad` (Vega-Lite drops
  `width`, `height`, `view`, and `autosize` of a composition); only the padding and the background are read. `Measure`
  then pads the bounds of the content, which the caller passes as the extents of a root of size zero (for example
  `LegendLayout.Combine` of empty extents and the bounds of the content), so the canvas is the content plus the padding.
- `ResolveSize(SpecNode? chartRoot, ResolvedConfig config, ViewSizeHints hints)` - the `ViewSize` of the data rectangle
  of a chart that sizes itself (a chart inside a concatenation or a facet): the number in `width` or `height`, else the
  discrete size of the scale stage (a `{step}` size or no size), else the continuous size of the chart's own `view` over
  the configuration; `"container"` has no meaning there (Vega-Lite drops it with a warning that the feature catalog
  reports) and is read as a missing size.
- `CellBounds(width, height, ViewExtents guides, TitleBlock? title, double strokeWidth)` - the bounds of one cell in its
  own coordinates (no padding or autosize) and the title group placed against them: the data rectangle united with the
  reach of the guides (negative values count as 0) and the placed title, expanded on each side by half the stroke width
  of the cell border (Vega expands the bounds of a stroked group, with everything in it, so a cell with a one pixel
  border has bounds from -0.5 to the size plus 0.5, a cell with a title 20 above its data rectangle from -20.5, and one
  with an axis 30 wide from -30.5; the single view, whose border is not part of its bounds, is not changed).
- `CreateViewGroup(ViewStyle style, x, y, width, height, SceneClip? clip, children)` - the group of role `view` of a
  chart: the plot area with the fill and border of the style, positioned at its origin, holding the guides, the marks,
  and the title; `Build` places the one of a single view on the canvas, and every chart of a composition makes one at the
  origin of its cell (the composition layout moves it).

**Preconditions and postconditions**: arguments are non-null where documented; the results are immutable and
deterministic.

#### Error Handling

The unit throws only `ArgumentNullException` for a null configuration (also of `ResolveSize`), request, measure, or
content list, which is not converted. Specification values that it reads were validated by `SpecParser`; a negative or
missing size, padding, or dimension is clamped to zero or replaced by its default rather than thrown, and an `autosize`
type that the specification parser did not reject is laid out by the rules above (a fit that a step drops is reported by
`VegaLiteDocument` as `vl-layout-fit-step`). The canvas size is
clamped to the range of an integer; the check of the size against `Surface.MaxDimension` is made by `VegaLiteDocument`.
The unit performs no I/O.

#### Dependencies

- **`SpecNode`**, **`ResolvedConfig`**, **`ConfigNode`**, and **`ConfigNodeKind`** - the `SpecModel` subsystem
- **`TitleBlock`** - the title of the `TitleBuilder` unit, which it places
- **`Scene`**, **`SceneGroup`**, **`SceneItem`**, **`SceneBounds`**, and **`SceneClip`** - the `SceneModel` unit
- **`VegaLiteSize`** - the public size type of the `VegaLiteDocument` unit
- **.NET base class library** - `System`, `System.Collections.Generic`

#### Callers

`VegaLiteDocument` calls `Resolve`, `Measure`, and `Build` (or `Compute` when there is no drawable view). The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Layout/ViewLayoutTests.cs` call the methods directly through
`InternalsVisibleTo`, and `VegaLiteDocumentLayoutTests.cs` exercises the layout through the public document.
