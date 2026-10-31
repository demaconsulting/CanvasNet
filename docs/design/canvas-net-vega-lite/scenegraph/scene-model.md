### SceneModel

![Scenegraph Structure](VegaLiteScenegraphView.svg)

The `SceneModel` unit is the first software unit in the `Scenegraph` subsystem. It comprises three source files in the
`DemaConsulting.CanvasNet.VegaLite.Scenegraph` namespace, all `internal`:

- `SceneModel.cs` - `Scene`, `SceneItem` and its subtypes `SceneGroup`, `SceneRect`, `SceneRule`, `ScenePath`,
  `SceneSymbol`, `SceneText`, `SceneArc`, and `SceneTrail`, the gradient types `SceneGradient` and `SceneGradientStop`,
  the path types `ScenePathSegmentKind` and `ScenePathSegment`, the value types `ScenePoint`, `SceneTrailPoint`,
  `SceneClip`, and `SceneEntry`, and the `SceneLists` copy helper
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
(copied, read-only), `StrokeDashOffset`, the `StrokeCap` and `StrokeJoin`, and the `StrokeMiterLimit` (null means the
default of 4 and the painter clamps a value that is not finite or is below 1 to 1 and one above 1,000 to 1,000; see
_ScenePainter_). Items are immutable; a changed copy is made
with a `with` expression, and list-valued members are copied when set. An item can also carry a `FillGradient`, which
replaces the solid `Fill` when it is set and can be drawn.

- **`SceneGradient`** `(X1, Y1, X2, Y2, stops)` - an immutable linear gradient for a fill, as in Vega's gradient object:
  the start `(X1, Y1)` and the end `(X2, Y2)` are fractions of the bounding box of the item (`(0, 0)` is the top-left
  corner and `(1, 1)` the bottom-right one), the defaults `(0, 0)` to `(1, 0)` run from left to right (`Horizontal`), and
  the vertical gradient of a legend runs from `(0, 1)` to `(0, 0)`, so that the last stop is at the top (`Vertical`).
  `Stops` is a read-only copy of the `SceneGradientStop` list (an `Offset` from 0 to 1 and a CSS `Color`); the stops
  need not be sorted and may share an offset (a hard step). The number of stops is bounded by `MaxStops` (256; a larger
  list is an `ArgumentException`), so a gradient costs a constant and is not charged against the scene-item budget.
  Vega's `scaleGradient` builds its stops from the domain end points and the ticks of the scale (15 by default); that
  choice belongs to the scene builders, not to the model.

- **`SceneGroup`** - a coordinate system at `(X, Y)` with a `Width`, `Height`, and `CornerRadius` for its own
  background and border, an optional `SceneClip`, a `StrokeOffset` (null means Vega's rule: half a pixel for a one-pixel
  stroke), and the `Children` in drawing order (the last is on top).
- **`SceneRect`** `(X, Y, Width, Height)` with a `CornerRadius`; **`SceneRule`** `(X, Y, X2, Y2)`, visible only with a
  stroke; **`ScenePath`** with `Points` and `Closed`, or with `Segments` (below); **`SceneSymbol`** `(X, Y)` with `Shape`
  (circle by default), `Size`
  (the area of the bounding box in square pixels, 64 by default) and `Angle`; **`SceneText`** `(X, Y, Lines)` with `Text`
  (lines joined by a line feed), `Dx`, `Dy`, `FontFamily`, `FontSize` (11), `FontWeight`, `FontStyle`, `Align` (left,
  center, right), `Baseline` (alphabetic, top, middle, bottom, line-top, line-bottom), `Angle`, `Limit`, `LineHeight`, and
  `Ellipsis` (the horizontal ellipsis); text has no gradient fill.
- **`ScenePathSegmentKind`** (`MoveTo`, `LineTo`, `CubicTo`, `Close`) and **`ScenePathSegment`** - one drawing command with
  absolute coordinates: `Kind`, the point `(X, Y)` (the end point of a cubic), and the control points `(X1, Y1)` and
  `(X2, Y2)` of a cubic (zero when unused). The factories `Move(x, y)`, `Line(x, y)`, `Cubic(x1, y1, x2, y2, x, y)` (the
  first control point, the second control point, then the end point), and `Close()` create the commands, and
  `IsFinite()` says whether every coordinate that the kind of the command uses is finite (a close uses none).
- **`ScenePath.Segments`** - the drawing commands of a path, copied into a read-only list; the path has two
  constructors, one
  of points and one of segments. A path made of segments has an empty `Points` list, **the segments replace the points
  when they are set**, and `Closed` then has no effect, because a `Close` command closes a subpath (a path can hold
  several subpaths, which is how a broken line and a closed area are one item). The line, area, and trail marks use the
  segments for the interpolated paths; an axis domain and a straight polygon keep the points.
- **`SceneArc`** `(X, Y)` - a circular or annular sector centered at `(X, Y)`, with `InnerRadius` (0, a pie slice),
  `OuterRadius` (0, a point), `StartAngle` and `EndAngle` (radians, clockwise from 12 o'clock, 0 by default), `PadAngle`,
  and `CornerRadius` (0 by default). The geometry is that of the `ArcGeometry` unit.
- **`SceneTrailPoint`** `(X, Y, Size)` and **`SceneTrail`** - a trail is a line whose width follows the `Size` at every
  point. `Runs` is a list of runs of points, each drawn on its own with a gap between two runs; the constructor takes the
  runs, requires a non-null sequence and non-null runs (`ArgumentNullException`), and **deep-copies** the lists (the outer
  list and every run), so a later change of the caller's lists cannot reach the scene. A point whose coordinates or size
  are not finite, or whose size is not positive, ends its run when the scene is painted.
- **Roles of the path-like marks.** The items of the line, area, trail, arc, and text marks have the roles `mark-line` and
  `mark-area` (the `ScenePath` items), `mark-trail` (the `SceneTrail`), `mark-arc` (the `SceneArc` items), and `mark-text`
  (the `SceneText` items), next to the existing `mark-bar`, `mark-rule`, `mark-tick`, and the symbol roles.
- **`Scene`** `(Width, Height, Background, Root)` - the canvas at a scale of one, y down; the constructor requires finite,
  non-negative sizes and a non-null root. `Enumerate()`, `Find(role)`, `FindAll(role)`, `OfType<T>()`, and `CountItems()`
  visit the items in drawing order, parents before children, reporting for each `SceneEntry` the item, the accumulated
  translation of its enclosing groups, and its depth.
- **`SceneBounds`** `(X1, Y1, X2, Y2)` - a bounding box, with `Empty` (the identity of `Union`), `IsEmpty`, `Width`, `Height`,
  `Union`, `Translate`, and `Rotate(degrees)` (the enclosing box of the rotated corners, with exact quarter turns).
- **`SceneBudget`** `(max)` - `Count`, `Add(items, location)`, `Fork()` and `Adopt(fork)`.

**Invariants**: items and scenes never change once built; every item has a non-blank role; the enumeration order is
the drawing order; the empty bounds contain nothing.

#### Key Methods

- Item and scene constructors - validate the role (`ArgumentException`), the scene size (`ArgumentOutOfRangeException`),
  the root (`ArgumentNullException`), the stops of a gradient (`ArgumentNullException`, and `ArgumentException` for
  more than `MaxStops`), and the runs of a trail (`ArgumentNullException`), and copy list-valued members (the points,
  the segments, the runs and their points) into read-only lists.
- `ScenePathSegment.Move`, `Line`, `Cubic`, and `Close` - the factories of the drawing commands, and `IsFinite()`, which
  the painter uses to skip a path that has a coordinate that is not finite.
- `Scene.Enumerate()` - an iterative walk with an explicit stack, so a deep scene cannot overflow the stack; the same scene
  always yields the same sequence.
- `SceneBounds.Union`, `Translate`, and `Rotate` - the arithmetic that layout uses to grow the bounds of a view; the empty
  box stays empty; sine and cosine are snapped near 0 and 1 so that quarter turns are exact.
- `SceneBudget.Add(items, location)` - adds a non-negative count (the total saturates at the largest `long` instead of
  overflowing) and throws a `VegaLiteRenderException` carrying the location when the total passes the maximum
  (`MaxSceneItems`). Every `SceneItem` of the finished scene counts once, groups included (the root and view groups, the
  title group and its texts, each axis group with its grid group, grid lines, ticks, labels, domain and title, the marks
  group and each mark; a clip is a property of its group, not an item), and a builder calls `Add` _before_ it creates
  the item, so a limit of N allows a chart of exactly N items and fails on the (N+1)th without allocating it. The axes
  are rebuilt until the data rectangle is stable, so each pass is charged to a `Fork()` of the budget and only the pass
  that is kept is taken over with `Adopt`. A tick label that is removed because it overlaps another is not in the scene
  and costs nothing: the label candidates are bounded by `MaxTicks`, which is checked before any label is made, and the
  labels that remain are charged before they are added to the axis.

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

`TitleBuilder`, `AxisBuilder`, `ViewLayout`, and `MarkBuilder` create items and bounds (`MarkBuilder` with the
`ScenePathSegment` commands of the `CurveBuilder` for the line and area marks, and the `SceneTrail` and `SceneArc` items);
`TextMeasurer` reads `SceneText`
and `SceneBounds`; `ScenePainter` draws a `Scene`; `VegaLiteDocument` creates the budget. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Scenegraph/SceneModelTests.cs` call the types directly through
`InternalsVisibleTo`.
