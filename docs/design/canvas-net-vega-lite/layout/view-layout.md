### ViewLayout

![Layout Structure](VegaLiteLayoutView.svg)

The `ViewLayout` unit is the only software unit in the `Layout` subsystem. It is one source file, `ViewLayout.cs`, in the
`DemaConsulting.CanvasNet.VegaLite.Layout` namespace, and defines nine `internal` types: `ViewLayout` (static class),
`ViewPadding`, `ViewExtents`, and `ViewSizeHints` (readonly record structs), `AutosizeType` (enumeration), and `ViewStyle`,
`ViewLayoutRequest`, `ViewMeasure`, and `ViewLayoutResult` (sealed records).

#### Purpose

`ViewLayout` lays out a single view: the size of the data rectangle, the padding, the room the guides and the title need,
the size of the canvas that holds them, and the scene around the content that the builders produced. It exists so that the
view-layout rules of Vega and Vega-Lite (default size, step sizing, padding, `autosize`, title bands, view background and
border) are defined once and can be tested by analytic numbers without drawing anything.

The unit's behavior is specified by the `CanvasNetVegaLite-Layout-ViewLayout-*` requirements in
`docs/reqstream/canvas-net-vega-lite/layout/view-layout.yaml`.

#### Data Model

- **`ViewPadding`** `(Left, Top, Right, Bottom)` with `Horizontal` and `Vertical` sums. **`ViewExtents`** `(Left, Top,
  Right, Bottom)` - how far the guides reach beyond the data rectangle on each side (zero or more).
- **`AutosizeType`** - `Pad` (the canvas grows around the data rectangle to hold the guides and the title) or `None` (the
  canvas is the given size and the guides may overflow it).
- **`ViewStyle`** - the fill, stroke, stroke width, opacities, dash, and corner radius of the plot area (CSS color strings
  or null).
- **`ViewSizeHints`** `(DiscreteWidth, DiscreteHeight, ContainerWidth, ContainerHeight)` - the sizes that the scale stage
  (from the step of a discrete scale) and the host (the container size) provide.
- **`ViewLayoutRequest`** - the data rectangle size, padding, autosize, `ContainsPadding`, `ViewStyle`, the canvas
  `Background`, an optional `Clip` to the data rectangle, and the container sizes. It is produced by `Resolve` or built
  directly by tests.
- **`ViewMeasure`** - the final data rectangle, the bounds of everything in the view's coordinates, the padding, the
  origin of the data rectangle on the canvas, the canvas `Width` and `Height` (possibly fractional), the placed title
  group and its bounds, and `NaturalSize`, the canvas size in whole pixels (rounded half away from zero, clamped to the
  range of an integer) as a `VegaLiteSize`.
- **`ViewLayoutResult`** - the `ViewMeasure` and the `Scene`.

**Resolution rules.** The data rectangle is the number in `width` and `height`; a `"container"` size takes the container
size from the hints; a `{step}` size or no size takes the discrete size computed by the scale stage, else the configured
continuous size (`view.continuousWidth` and `continuousHeight`, 300 by 300 in Vega-Lite 6, overridden by the `view` of the
specification). The padding is a number for all sides or an object whose missing sides are 0, from the specification, then
the configuration, then 5 on all sides. `autosize` is `pad` or `none` (with `contains` of `padding` or `content`) from the
specification, then the configuration. The canvas background is the caller's override, then the specification's
`background`, then the configuration's, then white. The plot style comes from `ResolveViewProperty`: the `cell` style,
the `view` configuration, and the Vega cell style (transparent fill, `#ddd` stroke).

**Measure rules.** With `pad` the canvas is the data rectangle plus the whole pixels the bounds reach on each side
(rounded up) plus the padding; with `none` it is the width and height plus padding, or exactly the width and height if
`contains` is `padding` (the data rectangle is then what is left after the padding); a container size leaves the room the
guides and padding do not need. The title is placed against the bounds of the data rectangle and guides and its bounds are
added. The view group's border is not part of the bounds, so it does not enlarge the canvas.

**Scene shape.** The root group holds the view group, translated to the data rectangle's origin and carrying the plot
background and border (the group paints its own fill and stroke, as a Vega group does); the content is its children in the
order given, followed by the title group.

#### Key Methods

- `Resolve(SpecNode? root, ResolvedConfig config, ViewSizeHints hints, string? backgroundOverride)` - reads the size,
  padding, autosize, background, and view style as described above and returns the request.
- `Measure(request, ViewExtents guides, TitleBlock? title)` - computes the final data rectangle (container sizes and
  `contains: padding` applied), the bounds with the title, the canvas size for `pad` or `none`, and the origin of the
  data rectangle on the canvas.
- `Build(request, measure, IEnumerable<SceneItem> content)` - assembles the scene.
- `Compute(request, guides, title, content)` - `Measure` followed by `Build`.

**Preconditions and postconditions**: arguments are non-null where documented; the results are immutable and
deterministic.

#### Error Handling

The unit throws only `ArgumentNullException` for a null configuration, request, measure, or content list, which is not
converted. Specification values that it reads were validated by `SpecParser`; a negative or missing size, padding, or
dimension is clamped to zero or replaced by its default rather than thrown, and an `autosize` type other than `pad` or
`none` is laid out as `pad` (the feature catalog has already reported it). The canvas size is clamped to the range of an
integer; the check of the size against `Surface.MaxDimension` is made by `VegaLiteDocument`. The unit performs no I/O.

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
