### SceneModel

![Scenegraph Structure](PlantUmlScenegraphView.svg)

The `SceneModel` unit is the first software unit in the `Scenegraph` subsystem. Its source files are
`Scenegraph/SceneModel.cs`, which defines the scene records and the static class `SceneShapes`, and
`Scenegraph/SceneModel.Page.cs`, which defines the page texts (`PageText`, `PageTexts`) and the static class
`ScenePage` that places them around the body of a diagram.

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
- `PageText(Text, Align, Location)` and `PageTexts` with `Title`, `Header`, `Footer`, `Caption`, and `Legend` (each
  null when the diagram has none; `PageTexts.None` has none).

#### Key Methods

- `Scene.BoundsOf(items)` - the union of the boxes of the points of every path (the control points of the shapes this
  package draws lie on their curves' boxes) and the boxes of the text lines; null for no items.
- `Scene.ToPath(segments, scale, offsetX, offsetY)` - the core path in device space; a line or curve before any move
  starts at the origin.
- `SceneShapes.Rect` (a radius clamped to half the smaller side; square corners for zero), `Ellipse` (four cubic arcs
  with the control distance 0.5523 of the radius), `Polygon` (closed), `Polyline`, and `Line`.
- `ScenePage.Decorate(body, page, theme, text, sink, metrics)` - places the page texts around a body whose view box
  starts at the origin, as PlantUML's annotated diagram does (values measured from PlantUML's output): the page is as
  wide as the
  widest of the body, the title plus 31, the legend box plus 46, and the other texts plus 12; the header (10-pixel
  gray text) is drawn 5 pixels from the top and, right aligned, 6 pixels from the right edge, and takes its height plus
  one; the title (bold) is centered with its top 15 pixels into a block of its height plus 21; the body follows,
  centered; the legend box (its text with a padding of 5, corners of 15) is 7 pixels below the body, centered or 17 or
  18 pixels from the left or right edge, in a block of its height plus 32; the caption is centered 4 pixels above the
  end of the legend block in a block of its height plus 3; the footer is centered (or aligned 6 pixels from an edge) 5
  pixels above the end of the caption block in a block of its height plus 1. A body without page texts is returned as
  it is. Centered texts are centered in the page width minus one pixel, as PlantUML centers them. These are the values
  of a sequence diagram (`PageMetrics.Sequence`); an optional `PageMetrics(Inset, BodyTrim, LegendWidth)` gives the
  values of another diagram type: `PageMetrics.Activity` adds 5 pixels above the header and the title, beside the
  aligned header and footer, and below the page, counts the body 5 pixels shorter (its own bottom margin), and keeps
  55 pixels beside the legend's text, as PlantUML frames an activity diagram.
- `ScenePage.Translate(item, dx, dy)` - a moved copy of a path or a text line (other items, and a zero offset, return
  the item itself).

#### Error Handling

Null arguments throw `ArgumentNullException`. Values are not validated here; the painter skips items whose
coordinates are not finite or too large.

#### Dependencies

The core `Canvas` (`Rgba32`) and `Geometry` (`Path`, `PathBuilder`) subsystems; for the page texts, the `TextLayout`
unit and the Styling subsystem's `SkinTheme` (the styles of the title, header, footer, legend, and caption).

#### Callers

The diagram layouts and renderers build scenes and place their page texts with `ScenePage`; `TextLayout` produces text
lines; `ScenePainter` draws scenes; `PlantUmlDocument` reads the view box, the background, and the scale.
