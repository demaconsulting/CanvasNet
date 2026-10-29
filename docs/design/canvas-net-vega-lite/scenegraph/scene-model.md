### SceneModel

![Scenegraph Structure](VegaLiteScenegraphView.svg)

The `SceneModel` unit is the first software unit in the `Scenegraph` subsystem. It comprises three source files in the
`DemaConsulting.CanvasNet.VegaLite.Scenegraph` namespace, all `internal`:

- `SceneModel.cs` - `Scene`, `SceneItem` and its subtypes `SceneGroup`, `SceneRect`, `SceneRule`, `ScenePath`,
  `SceneSymbol`, and `SceneText`, the value types `ScenePoint`, `SceneClip`, and `SceneEntry`, and the `SceneLists` copy
  helper
- `SceneBounds.cs` - `SceneBounds` (readonly record struct): axis-aligned bounding boxes
- `SceneBudget.cs` - `SceneBudget` (sealed class): the item counter behind `MaxSceneItems`

In the SysML2 model the unit is named `VegaLiteSceneModel`, because `SceneModel` already names a subsystem of
`CanvasNetRender3D`. The files carry the provenance header comment for the Vega scenegraph behavior they port.

#### Purpose

`SceneModel` is the immutable scene between the builders and the painter: the canvas size, the background, and a
tree of groups and items with resolved geometry and paint. It exists so that everything a chart draws can be asserted
analytically (exact positions, sizes, and CSS color strings, found by role) without rendering pixels, and so that the
painter has a single, simple input.

The unit's behavior is specified by the `CanvasNetVegaLite-Scenegraph-SceneModel-*` requirements in
`docs/reqstream/canvas-net-vega-lite/scenegraph/scene-model.yaml`.

#### Data Model

**`SceneItem`** (abstract record) is the common part: the `Role` (a stable, lower-case, non-blank name such as `view`,
`title-text`, `axis-label`, or `mark-bar`), the paints `Fill` and `Stroke` as CSS color text (null or `none` means no
paint), `StrokeWidth` (null means 1), `Opacity`, `FillOpacity`, and `StrokeOpacity` (1 by default and multiplying: the
fill is drawn with fill opacity times opacity, the stroke with stroke opacity times opacity), the `StrokeDash` pattern
(copied, read-only), `StrokeDashOffset`, and the `StrokeCap` and `StrokeJoin`. Items are immutable; a changed copy is made
with a `with` expression, and list-valued members are copied when set.

- **`SceneGroup`** - a coordinate system at `(X, Y)` with a `Width`, `Height`, and `CornerRadius` for its own
  background and border, an optional `SceneClip`, a `StrokeOffset` (null means Vega's rule: half a pixel for a one-pixel
  stroke), and the `Children` in drawing order (the last is on top).
- **`SceneRect`** `(X, Y, Width, Height)` with a `CornerRadius`; **`SceneRule`** `(X, Y, X2, Y2)`, visible only with a
  stroke; **`ScenePath`** with `Points` and `Closed`; **`SceneSymbol`** `(X, Y)` with `Shape` (circle by default), `Size`
  (the area of the bounding box in square pixels, 64 by default) and `Angle`; **`SceneText`** `(X, Y, Lines)` with `Text`
  (lines joined by a line feed), `Dx`, `Dy`, `FontFamily`, `FontSize` (11), `FontWeight`, `FontStyle`, `Align` (left,
  center, right), `Baseline` (alphabetic, top, middle, bottom, line-top, line-bottom), `Angle`, `Limit`, `LineHeight`, and
  `Ellipsis` (the horizontal ellipsis).
- **`Scene`** `(Width, Height, Background, Root)` - the canvas at a scale of one, y down; the constructor requires finite,
  non-negative sizes and a non-null root. `Enumerate()`, `Find(role)`, `FindAll(role)`, `OfType<T>()`, and `CountItems()`
  visit the items in drawing order, parents before children, reporting for each `SceneEntry` the item, the accumulated
  translation of its enclosing groups, and its depth.
- **`SceneBounds`** `(X1, Y1, X2, Y2)` - a bounding box, with `Empty` (the identity of `Union`), `IsEmpty`, `Width`, `Height`,
  `Union`, `Translate`, and `Rotate(degrees)` (the enclosing box of the rotated corners, with exact quarter turns).
- **`SceneBudget`** `(max)` - `Count` and `Add(items, location)`.

**Invariants**: items and scenes never change once built; every item has a non-blank role; the enumeration order is
the drawing order; the empty bounds contain nothing.

#### Key Methods

- Item and scene constructors - validate the role (`ArgumentException`), the scene size (`ArgumentOutOfRangeException`),
  and the root (`ArgumentNullException`), and copy list-valued members into read-only lists.
- `Scene.Enumerate()` - an iterative walk with an explicit stack, so a deep scene cannot overflow the stack; the same scene
  always yields the same sequence.
- `SceneBounds.Union`, `Translate`, and `Rotate` - the arithmetic that layout uses to grow the bounds of a view; the empty
  box stays empty; sine and cosine are snapped near 0 and 1 so that quarter turns are exact.
- `SceneBudget.Add(items, location)` - adds a non-negative count (the total saturates at the largest `long` instead of
  overflowing) and throws a `VegaLiteRenderException` carrying the location when the total passes the maximum
  (`MaxSceneItems`). The chart counts the axis groups and their items, the marks group, and each mark item as it is created.

**Preconditions and postconditions**: scene sizes are finite and not negative; the budget maximum is at least 1.

#### Error Handling

Invalid construction is a programming error and throws `ArgumentException`, `ArgumentOutOfRangeException`, or
`ArgumentNullException`, which are not converted. The only data-dependent failure is `SceneBudget.Add`, which throws a
`VegaLiteRenderException` with the location of the construct that crossed `MaxSceneItems`, before the items' memory is
spent. The unit performs no I/O.

#### Dependencies

- **`VegaLiteLocation`** and **`VegaLiteRenderException`** - the public types of the `VegaLiteDocument` unit (the budget
  only)
- **.NET base class library** - `System.Collections.ObjectModel`, `System.Collections.Generic`, `System.Linq`

The unit has no dependency on another unit of the system.

#### Callers

`TitleBuilder`, `AxisBuilder`, `ViewLayout`, and `MarkBuilder` create items and bounds; `TextMeasurer` reads `SceneText`
and `SceneBounds`; `ScenePainter` draws a `Scene`; `VegaLiteDocument` creates the budget. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Scenegraph/SceneModelTests.cs` call the types directly through
`InternalsVisibleTo`.
