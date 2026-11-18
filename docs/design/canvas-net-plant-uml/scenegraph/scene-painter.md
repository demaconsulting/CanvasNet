### ScenePainter

![Scenegraph Structure](PlantUmlScenegraphView.svg)

The `ScenePainter` unit is the third software unit in the `Scenegraph` subsystem. It is two source files:
`ScenePainter.cs`, which defines the record `ScenePlacement` and the class `ScenePainter` with the paths, the bounds
checks, and the budget, and `ScenePainter.Text.cs`, which draws the text lines.

#### Purpose

`ScenePainter` is the only part of the package that produces pixels: it draws a scene onto a new core `Surface`, bounded
in the coordinates it accepts and the pixels it paints, so that a defective or hostile scene can neither exhaust memory
nor run without end. The unit's behavior is specified by the `CanvasNetPlantUml-Scenegraph-ScenePainter-*` requirements
in `docs/reqstream/canvas-net-plant-uml/scenegraph/scene-painter.yaml`.

#### Data Model

- `ScenePlacement(Scale, OffsetX, OffsetY)` - device = scene × scale + offset; `Uniform(viewBox, scale)` places the view
  box at the origin; `Fit(viewBox, width, height)` scales by the largest factor that fits and centers (a view box
  without area is not scaled).
- The painter's state: the text layout of the call, the budget of painted pixels, and the pixels painted so far (one
  painting, one thread).
- The constants: `MaxMagnitude` (32,768 device pixels), the flattening tolerance (0.1), the smallest dash period (0.25
  device pixels), and the largest number of dashes on one path (50,000).

#### Key Methods

- `Paint(scene, width, height, placement, background, token)` - a new surface, cleared to the background when it is
  not transparent; then every item in order, the token checked before each.
- Paths - the device path of the segments; the fill with the nonzero rule; the stroke built by the core stroker in
  device space with the scaled width (butt caps, miter joins) and the scaled dashes, which are drawn solid when the
  period is below the smallest or when they would be too many.
- Text - the line's box is charged once; each run is drawn with the face the text layout resolves at the scaled size
  with the core's text paths, the next run starting at the measured advance; an underline bar below the baseline and a
  strike bar above it are filled for the runs that have them.
- Bounds and budget - an item whose coordinates are not finite or exceed `MaxMagnitude` is skipped; before an item is
  painted, the pixels of its device box clipped to the surface are added to the count, and passing the budget throws.

#### Error Handling

Passing `PlantUmlLimits.MaxPaintedPixels` throws a `PlantUmlRenderException` with the code `plantuml-limit-exceeded`,
located at the item, before any of its pixels is painted; a canceled token throws `OperationCanceledException`; on
any failure the new surface is disposed before the exception propagates. Null arguments throw
`ArgumentNullException`.

#### Dependencies

The `SceneModel` and `TextLayout` units, and the core `Canvas`, `Drawing`, `Geometry`, and `Rendering` subsystems.

#### Callers

`PlantUmlDocument` paints the scene of every render with the text layout of the call.
