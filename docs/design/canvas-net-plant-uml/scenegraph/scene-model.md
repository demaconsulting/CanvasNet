### SceneModel

![Scenegraph Structure](PlantUmlScenegraphView.svg)

The `SceneModel` unit is the first software unit in the `Scenegraph` subsystem. Its source file is
`Scenegraph/SceneModel.cs`, which defines the scene records and the static class `SceneShapes`.

#### Purpose

`SceneModel` is the description of a picture that every diagram layout produces and the painter draws: a list of
immutable items in paint order and the view box whose size is the natural size of the diagram. Keeping the layout's
output a plain value lets tests and the reference comparison read the geometry without pixels. The unit's behavior is
specified by the `CanvasNetPlantUml-Scenegraph-SceneModel-*` requirements in
`docs/reqstream/canvas-net-plant-uml/scenegraph/scene-model.yaml`.

#### Data Model

- `SceneRect(X, Y, Width, Height)` with `Right`, `Bottom`, and `Union`.
- `SceneSegmentKind` (`MoveTo`, `LineTo`, `CubicTo`, `Close`) and `SceneSegment(Kind, X, Y, X1, Y1, X2, Y2)` with the
  factories `Move`, `Line`, `Cubic`, and `Close`.
- `SceneStroke(Color, Width)` with `Dash`; `SceneFont(Family, Size, Bold, Italic)`; `SceneTextRun(Text, Font, Color,
  Width)` with `Underline` and `Strike` (the width is the advance the text layout measured).
- `SceneItem` - the base of the items: `Role` (required: `participant`, `lifeline`, `message`, `note`, ...), `Id`, and
  `Location`; `ScenePath(Segments)` with `Fill` and `Stroke`; `SceneText(X, Baseline, Runs, Box)` with `Text`.
- `Scene(ViewBox, Items)` with `Background` (null for transparent) and `Scale` (the diagram's own `scale`, 1 by
  default).

#### Key Methods

- `Scene.BoundsOf(items)` - the union of the boxes of the points of every path (the control points of the shapes this
  package draws lie on their curves' boxes) and the boxes of the text lines; null for no items.
- `Scene.ToPath(segments, scale, offsetX, offsetY)` - the core path in device space; a line or curve before any move
  starts at the origin.
- `SceneShapes.Rect` (a radius clamped to half the smaller side; square corners for zero), `Ellipse` (four cubic arcs
  with the control distance 0.5523 of the radius), `Polygon` (closed), `Polyline`, and `Line`.

#### Error Handling

Null arguments throw `ArgumentNullException`. Values are not validated here; the painter skips items whose
coordinates are not finite or too large.

#### Dependencies

The core `Canvas` (`Rgba32`) and `Geometry` (`Path`, `PathBuilder`) subsystems.

#### Callers

The diagram layouts and renderers build scenes; `TextLayout` produces text lines; `ScenePainter` draws scenes;
`PlantUmlDocument` reads the view box, the background, and the scale.
