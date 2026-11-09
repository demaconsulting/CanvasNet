### SceneModel

![Scenegraph Structure](MermaidScenegraphView.svg)

The `SceneModel` unit is the first software unit in the `Scenegraph` subsystem. It is one source file,
`SceneModel.cs`, in the `DemaConsulting.CanvasNet.Mermaid.Scenegraph` namespace, and defines the internal scene
types: the record structs `SceneRect` and `SceneSegment`, the enumerations `SceneSegmentKind` and `SceneTextAnchor`,
and the records `SceneStroke`, `SceneFont`, `SceneTextRun`, `SceneItem` (abstract), `SceneGroup`, `ScenePath`,
`SceneText`, `SceneImage`, and `Scene`. The SysML2 model names the unit `MermaidSceneModel`.

#### Purpose

`SceneModel` is the immutable description of what a laid-out diagram draws, in Mermaid's SVG user units (CSS pixels at
scale 1): groups translated together, paths with fill and stroke, lines of styled text runs, and pictures, with the
view box of the diagram. Its single responsibility is to carry the resolved geometry, paint, and fonts of a diagram
from the layout to the painter, and to be the surface on which the tests and the reference comparison assert the
layout with numbers. It also gives the bounds of items (as SVG's `getBBox`) and converts path geometry to a core
`Path`.

The unit's behavior is specified by the `CanvasNetMermaid-Scenegraph-SceneModel-*` requirements in
`docs/reqstream/canvas-net-mermaid/scenegraph/scene-model.yaml`.

#### Data Model

- **`SceneRect(X, Y, Width, Height)`** - an axis-aligned rectangle with `Right` and `Bottom`; the width and height
  are not negative.
- **`SceneSegment(Kind, X, Y, X1, Y1, X2, Y2, LargeArc, Sweep)`** - one SVG path command (`SceneSegmentKind`:
  `MoveTo`, `LineTo`, `QuadraticTo`, `CubicTo`, `ArcTo`, `Close`) in absolute scene units. `X` and `Y` are the end
  point; a quadratic curve has its control point in `X1`, `Y1`; a cubic curve its two control points in `X1` to `Y2`;
  an arc its radii in `X1`, `Y1`, its rotation in degrees in `X2`, and its flags in `LargeArc` and `Sweep`.
- **`SceneStroke(Color, Width)`** - the stroke color (opacity folded in) and width, with `Dash` (the dash lengths, or
  null for solid), `DashOffset`, `Cap` (butt by default), and `Join` (miter by default), the SVG defaults.
- **`SceneFont(Family, Size, Bold, Italic)`** - a CSS `font-family` list, a size in scene units, and a style;
  **`SceneTextRun(Text, Font, Color)`** - a run of text with one font and color.
- **`SceneItem`** - the base of every item: `Role` (required; a stable name such as `node`, `edge`, `edge-label`,
  `cluster`, `marker`, or `label`), `Id` (Mermaid's element id, or null), and `Location` (the construct in the
  original text; unknown by default).
- **`SceneGroup(OffsetX, OffsetY, Children)`** - items translated together, as SVG's
  `<g transform="translate(x, y)">`.
- **`ScenePath(Segments)`** - with `Fill` (or null), `FillRule` (nonzero by default), and `Stroke` (or null).
- **`SceneText(X, Baseline, Anchor, Runs, Box)`** - one line of runs on a baseline, anchored at its start, middle, or
  end (`SceneTextAnchor`), with the box that the label layout measured.
- **`SceneImage(Box, Href)`** - a picture that fills its box, with `Opacity` (1 by default).
- **`Scene(ViewBox, Items)`** - the view box (Mermaid's SVG `viewBox`, whose size is the natural size of the diagram),
  the items in paint order, and `Images`, the picture loader of the scene's pictures (or null).

**Invariants**: every type is an immutable record; the lists are read-only views supplied by the builders, which do
not change them after the scene is built. The scene is safe for concurrent reading.

#### Key Methods

- **`SceneRect.Union(other)` / `Offset(dx, dy)`** - the smallest rectangle containing both, and the rectangle moved.
- **`SceneSegment.Move`, `Line`, `Quadratic`, `Cubic`, `Arc`, `Close`** - the factories that fill the fields of each
  kind as documented above; callers use them instead of the constructor.
- **`Scene.Walk()`** - every item depth first in paint order with the accumulated translation of the groups around it
  (an item's absolute position is its own coordinates plus the offset).
- **`Scene.BoundsOf(items)`** - the union of the bounds of the items with the translations of their groups applied:
  the geometry of each path (the path is converted at scale 1 and its bounds taken with curves flattened to 0.01
  unit, so a curve counts, not its control points, and the stroke does not count), the box of each text line, and the
  box of each picture; null when the items draw nothing (no items, or only empty paths).
- **`Scene.ToPath(segments, scale, offsetX, offsetY)`** - a core `Path` built with `PathBuilder`, every point mapped to
  `point x scale + offset`: moves, lines, quadratic and cubic curves, elliptical arcs (radii scaled, rotation kept),
  and closes; a drawing command before the first move starts at the origin, as in an SVG path that does not begin
  with `M`.

**Preconditions and postconditions**: `BoundsOf` and `ToPath` throw `ArgumentNullException` for a null list; both are
pure functions of their arguments and deterministic.

#### Error Handling

The records validate nothing: values that are not finite are carried as given and are dealt with by the painter,
which skips them. `BoundsOf` and `ToPath` reject only null arguments. The unit performs no I/O.

#### Dependencies

- **Core `CanvasNet`** - `Rgba32` (`Canvas`), `FillRule`, `LineCap`, and `LineJoin` (`Drawing`), and `Path` and
  `PathBuilder` (`Geometry`), through the public API
- **`MermaidLocation`** - the public location type of the system, for `SceneItem.Location`
- **`ISceneImageLoader`** - of the `ScenePainter` unit, for `Scene.Images`
- **.NET base class library** - `System.Numerics.Vector2` and `System.Math`

#### Callers

The units of the `Rendering` subsystem (`ShapeLibrary`, `EdgeBuilder`, `LabelBuilder`, and `ClusterLayout`) build the
items, and `EdgeBuilder` takes the bounds of an edge with `Scene.BoundsOf`; the `FlowchartModel` unit of the
`Flowchart` subsystem assembles the scene with its view box and picture loader, and the `MermaidDocument` unit builds
the empty scene of a diagram type that is not drawn and returns the laid-out scene to the tests; the `ScenePainter`
unit converts the paths with `Scene.ToPath` and paints the scene. The tests in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Scenegraph/SceneModelTests.cs` call the unit directly, and the system
tests read scenes through the test helper `SceneQuery` with `Scene.Walk`, through `InternalsVisibleTo`.

#### Known Differences From Mermaid

- **Bounds of curves are approximated.** SVG's `getBBox` gives the exact extremes of a curve; the bounds here are those
  of the curve flattened to 0.01 unit, which differ from the exact extremes by less than that tolerance.
