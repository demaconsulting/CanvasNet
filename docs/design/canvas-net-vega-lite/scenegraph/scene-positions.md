### ScenePositions

![Scenegraph Structure](VegaLiteScenegraphView.svg)

The `ScenePositions` unit is the sixth software unit in the `Scenegraph` subsystem. It is one source file,
`ScenePositions.cs`, in the `DemaConsulting.CanvasNet.VegaLite.Scenegraph` namespace, and defines the `internal` static
class `ScenePositions`. In the SysML2 model the unit is named `VegaLiteScenePositions`, because `ScenePositions` would
also name the unit of the `CanvasNetMermaid` system.

#### Purpose

`ScenePositions` reports the features of a laid-out chart - its titles, views (data rectangles), axes, legends, and
marks - as core `ImagePosition` values in the pixels of the surface a placement draws on. The builders only tag the
groups of the guides and views (`SceneItem.Position`) and record, when positions are asked for, the row each mark item
draws (`SceneItem.Datum`, the vertices of the path marks, and the `SceneMarkContext` of each marks group); this unit
turns those tags and rows into ids, values, boxes, and attachment pixels in one place, the same for every view of a
composition.

The unit's behavior is specified by the `CanvasNetVegaLite-Scenegraph-ScenePositions-*` requirements (`ViewPaths`,
`Ids`, `Values`, `Data`, `Geometry`, and `Filter`) in `docs/reqstream/canvas-net-vega-lite/scenegraph/scene-positions.yaml`.

#### Data Model

The class is static and stateless; a call builds its own lists. `GuideOrder` is the report order of the guide kinds:
`title`, `view`, `axis`, `legend`; marks come after them.

**Kinds and ids.** A `title` has the id `title`; a `view` (the data rectangle of a chart) has its view path; an `axis`
has its channel; a `legend` has its first channel; a `mark` has the id `<mark>-<n>`, where `<mark>` is the mark type (the
role of the item without its `mark-` prefix) and `n` counts the items of that mark type in that view from 0 in drawing
order (each vertex of a line, area, or trail counts).

**View paths.** Every view has a canonical path: `""` for the root, then one `operator[index]` segment per level below
the root, joined by `.` (`hconcat[1]`, `vconcat[0].layer[1]`, `facet[3]`, `repeat[2]`). The operator is that of the
parent: `layer` for a layered parent, else the parent's `Operator` (`concat`, `hconcat`, `vconcat`, `repeat`, or
`facet`; the index of a facet's cell is its place in the layout). The layers a composite mark (a boxplot) expands to,
and synthetic views, add no segment: they are the composite mark's view. The `name` the specification gives a view is
reported separately as `ViewName`.

**Values.** A mark reports `SceneMarkContext.Values(row)` (the normalized values of its row by channel, built by the
`MarkBuilder` unit) followed by the `FacetValues` of its cell (`row`, `column`, or `facet`); the `ImagePosition` sorts
them by name.

**Data.** A mark also reports `SceneMarkContext.Data(row)` (the data fields of its row, at most 32, sorted by name,
built by the `MarkBuilder` unit) and the shared `ChannelNames`: the Vega-Lite 6.5 encoding channels (`x`, `y`,
`x2`, `y2`, `xOffset`, `yOffset`, the error, geographic, and polar channels, `color`, `fill`, `stroke`, the
opacity and stroke channels, `size`, `angle`, `shape`, `text`, `tooltip`, `href`, `url`, `description`,
`key`, `detail`, `order`, `time`, `row`, `column`, and `facet`), one internal static `FrozenSet`. A selector
predicate with one of those names compares with the values, any other name with the data fields.

**Invariants**: the result never contains a position whose box or pixel is not finite; the same scene, placement,
measurer, and filter always give the same list.

#### Key Methods

- **`Report(scene, placement, measurer, options)`** - does nothing when `VegaLiteRenderOptions.PositionHandler` is
  null; otherwise calls the handler once per position of `Collect(scene, placement, measurer,
  options.PositionKindFilter)`, in order, on the calling thread.
- **`Collect(scene, placement, measurer, kindFilter)`** - walks the scene from the root in drawing order with an
  explicit stack (no recursion), carrying the translation of the enclosing groups and the `SceneMarkContext` of the
  nearest enclosing marks group (`SceneGroup.Marks`). An item tagged with a guide kind that the filter accepts becomes a
  guide position; otherwise, when the filter accepts `mark` and the item is inside a marks group, its mark positions are
  added. The result is the titles, views, axes, and legends, each in drawing order, then the marks in drawing order.
- **`Guide`** (private) - the box is the tag's own `Bounds` (when not empty) translated by the parent's translation,
  else the rectangle of the tagged group (a view's data rectangle, a legend's frame); the pixel is the center of the
  box; `Text` is the tag's text (null when empty or white space), with the tag's `View` and `ViewName`.
- **`AddMarks`** (private) - a `ScenePath` or `SceneTrail` with `Vertices` (a line, area, or trail) gives one position
  per vertex, at the vertex, with a box of zero size and the vertex's row; any other item gives one position when its
  `Datum` is a row (not -1) and `Geometry` gives it a shape.
- **`Geometry(item, measurer)`** (internal) - the geometry bounds (stroke not included) and the aim point, in the
  coordinates of the marks group: a rectangle (bar, rect, tick) its normalized rectangle and its center; a rule the
  normalized box of its two ends and their midpoint; a symbol the bounds of `ScenePainter.SymbolPath` of its shape and
  size, rotated by its angle and moved to its center (flattened at 0.01; a point when empty), aimed at its center; a text
  the box `TextMeasurer.GetBounds` measures, aimed at its anchor (`X + Dx`, `Y + Dy`); an arc the bounds of the
  `ArcGeometry.Build` outline moved to its center, aimed at its mid-angle (`(start + end) / 2`, measured clockwise from
  12 o'clock) at its mid-radius (`(inner + outer) / 2`); an image the box of its width and height placed by its `Align`
  and `Baseline`, aimed at the center; and nothing for any other item.
- **`ViewPath(node)`**, **`ViewName(node)`**, and **`FacetValues(node)`** (internal) - the canonical view path, the
  `name` of the specification node, and the facet values of the nearest view that is a cell of a facet (`column` and
  `row` for the fields the facet has, `facet` for a wrapped facet, each converted with `MarkBuilder.TryConvert`; empty
  outside a facet), used by `VegaLiteDocument` when it tags the guides and marks groups of a view.
- **`Map`** and **`MapPoint`** (private) - scene coordinates to surface pixels by the placement: `x * Scale + OffsetX`,
  `y * Scale + OffsetY`, sizes times `Scale` (a negative size taken as 0).

**Preconditions and postconditions**: the scene, placement, and measurer are those of the render; the positions are in
the pixels of the surface that `ScenePainter.Paint` draws with the same placement.

#### Error Handling

The unit throws nothing of its own: a guide or mark whose mapped box or pixel is not finite is skipped, an item without
a recorded row or a shape reports no position, and a kind the filter rejects is skipped before its geometry is computed.
An exception thrown by the caller's handler propagates out of `Report` unchanged (the `VegaLiteDocument` unit still
delivers the reports recorded before it).

#### Dependencies

- **SceneModel** - `Scene`, the items, `SceneItem.Position` and `ScenePositionTag`, `SceneItem.Datum`,
  `SceneGroup.Marks` and `SceneMarkContext`, `ScenePath.Vertices`, `SceneTrail.Vertices` and `SceneVertex`, and
  `SceneBounds` of the same subsystem
- **ScenePainter** - `ScenePlacement` and `ScenePainter.SymbolPath`; **ArcGeometry** - `ArcGeometry.Build`
- **Guides subsystem** - `TextMeasurer.GetBounds` (the box of a text mark)
- **Marks subsystem** - `MarkBuilder.TryConvert` (the facet values)
- **SpecModel and Data subsystems** - `LayerNode`, `ViewKind`, and the facet cell of a view; `DataValue`
- **System public API** - `VegaLiteRenderOptions.PositionHandler` and `PositionKindFilter`
- **Core `CanvasNet`** - `ImagePosition`, `PositionBox`, `PositionPoint`, and `PositionValue` (`Positions`), through
  the public API
- **.NET base class library** - `System.Numerics.Matrix3x2`, `System.Globalization`, and LINQ

#### Callers

The `VegaLiteDocument` unit calls `Report` in both `Render` overloads after the layout and the placement and before the
painting, and calls `ViewPath`, `ViewName`, and `FacetValues` when it tags the root view, the views, guides, and titles
of a composition, and the marks groups of each layer (`VegaLiteDocument.Render.cs` and `VegaLiteDocument.Composition.cs`).
The tests in `test/DemaConsulting.CanvasNet.VegaLite.Tests/Scenegraph/ScenePositionsTests.cs` call `Collect` and
`Geometry` directly through `InternalsVisibleTo`.
