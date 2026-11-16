### ScenePositions

![Scenegraph Structure](MermaidScenegraphView.svg)

The `ScenePositions` unit is the third software unit in the `Scenegraph` subsystem. It is one source file,
`ScenePositions.cs`, in the `DemaConsulting.CanvasNet.Mermaid.Scenegraph` namespace, and defines the internal static
class `ScenePositions` (with its private `Feature` record). The SysML2 model names the unit `MermaidScenePositions`.

#### Purpose

`ScenePositions` turns the position tags that the renderers set on the items of a laid-out scene (see *SceneModel*,
`ScenePositionTag`) into the core `ImagePosition` values of the features of the diagram, in the pixels of the surface a
placement draws on. Its single responsibility is the geometry and the order of the positions, the same for every
diagram type: the renderers only say which item draws which feature.

The unit's behavior is specified by the `CanvasNetMermaid-Scenegraph-ScenePositions-*` requirements (`Merge`, `Aim`,
`Order`, and `Mapping`) in `docs/reqstream/canvas-net-mermaid/scenegraph/scene-positions.yaml`.

#### Data Model

The class is static and stateless; a call builds its own lists.

- **`KindOrder`** - the kinds in report order: `title`, `group`, `node`, `participant`, `task`, `milestone`,
  `section`, `slice`, `block`, `commit`, `branch`, `tag`, `edge`, `message`, `note`; a kind not listed comes after
  them.
- **`FlattenTolerance`** = 0.01 scene unit - the tolerance of the curve flattening of the arc-length midpoint.
- **`Feature(Tag)`** (private record) - one feature: the tag of its first item and `Parts`, the items that draw it in
  drawing order, each with the translation of the groups around it.

**Invariants**: the result never contains a position whose box or pixel is not finite; the same scene, placement, and
filter always give the same list.

#### Key Methods

- **`Report(scene, placement, options)`** - does nothing when `MermaidRenderOptions.PositionHandler` is null; otherwise
  calls the handler once per position of `Collect(scene, placement, options.PositionKindFilter)`, in order, on the
  calling thread.
- **`Collect(scene, placement, kindFilter)`** - the algorithm:
  1. **Group.** Walk the scene with `Scene.Walk` (depth first, in paint order, with the translation of the groups).
     Skip an item without a tag and an item whose kind the filter rejects, before any geometry is computed. The items
     whose tags have the same kind and id are one feature (a self-loop drawn in three pieces, a gantt section drawn as
     one band per row, a commit bullet drawn as several shapes); the first item decides the tag and the drawing order
     of the feature.
  2. **Order.** Sort the features by the rank of their kind in `KindOrder`, then by source line (a feature without a
     line last), then by drawing order (a stable sort).
  3. **Box.** The union of the geometry bounds of the parts, each taken with the internal
     `Scene.BoundsOf(items, dx, dy)` with its own translation (curves exactly, strokes and drop shadows not included,
     the box of a text line or picture as given); a feature whose parts draw nothing is skipped.
  4. **Aim.** The explicit `Aim` of the tag translated by the groups around the first part (a pie slice's mid-angle at
     mid-radius, a commit's point), else, for a tag with `AlongPath` (edges and messages), the arc-length midpoint of
     the `ScenePath` parts joined in drawing order (markers and labels are not parts of the path), else the center of
     the box.
  5. **Map.** Box and pixel are mapped by the placement: `x * Scale + OffsetX`, `y * Scale + OffsetY`, sizes times
     `Scale`. A feature whose mapped box or pixel is not finite is skipped.
  6. **Facts.** `Text` is the tag's text trimmed (null when it is empty or white space), `Line` the line of the tag's
     location (null when unknown), `From` and `To` the tag's ends, `Values` the single `value` of a finite
     `ScenePositionTag.Value` (a pie slice), and `Index`, for a tag with an end, the 0-based count of the features
     before it in report order with the same kind, `From`, and `To`.
- **`PathMidpoint(pieces)`** (internal) - flattens the segments of every piece, in order, into straight lines (moves
  start a new subpath, lines are kept, quadratic and cubic curves are flattened with the core `BezierFlattening`, arcs
  are converted with the core `SvgArcConverter` and flattened, and a close returns to the start of the subpath), then
  walks half of the total length: the point at that length, the first point when the lines have no length, or null when
  there are no lines.

**Preconditions and postconditions**: the scene and placement are those of the render; the positions are in the
pixels of the surface that `ScenePainter.Paint` draws with the same placement.

#### Error Handling

The unit throws nothing of its own: geometry that is not finite is skipped, and a feature that draws nothing reports
no position. The `ArgumentException` of an `ImagePosition` setter cannot occur for the values it builds (a lowercase
kind, a positive line, a non-negative index, finite geometry). An exception thrown by the caller's handler propagates
out of `Report` unchanged (the `MermaidDocument` unit still delivers the reports recorded before it).

#### Dependencies

- **SceneModel** - `Scene`, `Scene.Walk`, the internal `Scene.BoundsOf(items, dx, dy)`, `SceneItem.Position`,
  `ScenePositionTag`, `ScenePath`, `SceneSegment`, and `SceneRect` of the same subsystem
- **ScenePainter** - `ScenePlacement` (the scale and offset of the render)
- **System public API** - `MermaidRenderOptions.PositionHandler` and `PositionKindFilter`
- **Core `CanvasNet`** - `ImagePosition`, `PositionBox`, `PositionPoint`, and `PositionValue` (`Positions`), and
  `BezierFlattening` and `SvgArcConverter` (`Geometry`), through the public API
- **.NET base class library** - `System.Numerics.Vector2` and LINQ

#### Callers

The `MermaidDocument` unit calls `Report` in both `Render` overloads, after the layout and the placement
(`ScenePlacement.Uniform` or `ScenePlacement.Fit`) and before the painting. The tests in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Scenegraph/ScenePositionsTests.cs` call `Collect`, `Report`, and
`PathMidpoint` directly through `InternalsVisibleTo`.
