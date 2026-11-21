### SvekGeometry

![Svek Structure](PlantUmlSvekView.svg)

The `SvekGeometry` unit is the fourth software unit in the `Svek` subsystem. Its source files are
`Svek/SvekGeometry.cs` (curves, clipping, arcs) and `Svek/SvekGeometry.Extremities.cs` (the link decorations).

#### Purpose

`SvekGeometry` turns routes into the curves PlantUML's links are drawn with, clips them to the outlines of entities and
the borders of clusters, and builds the decorations of the link ends with PlantUML's geometry. The unit's behavior is
specified by the `CanvasNetPlantUml-Svek-SvekGeometry-*` requirements in
`docs/reqstream/canvas-net-plant-uml/svek/svek-geometry.yaml`.

#### Data Model

The unit holds no state. It returns scene segments and `SvekExtremity(Decor, Parts, ContactLength)` with
`ExtremityPart(Segments, Fill, StrokeWidth)`, whose `ExtremityFill` is `None`, `Line` (the link's color), or
`Background` (the document's).

#### Key Methods

- `Spline(points)` - a line for two points; for three, the quadratic Bezier curve of the points raised to a cubic
  (control points two thirds of the way to the middle point); for more, the uniform cubic B-spline clamped at both ends
  whose control points are the route: the knot vector is `0, 0, 0, 0, 1, ..., m - 1, m, m, m, m` for `m = n - 3`
  segments, and Boehm's knot insertion raises every interior knot to multiplicity 3, which gives the Bezier control
  points of the `m` cubic pieces (the curve is unchanged, starts and ends at the ends of the route, leaves them along
  the first and last legs, and is C2 where the pieces meet). Written from the mathematics: Graphviz draws the edges of
  PlantUML's diagrams as such splines, and no curve code of PlantUML, Graphviz, or d3 is ported. It lives here, not in
  the CanvasNetLayout system, because only this engine uses it and it emits PlantUML's scene segments.
- `Flatten(route, steps)` - points along the curve of `Spline`: the ends of its lines, and each cubic piece measured at
  `steps` even steps of its parameter (the layout keeps the labels clear of the drawn curve with them).
- `Clip(shape, box, toward)` - where the direction from the center of the box toward a point leaves a rectangle
  (dagre's `intersectRect`), an ellipse (`t = 1 / sqrt(dx²/rx² + dy²/ry²)`), or a diamond (`t = 1 / (|dx|/rx +
  |dy|/ry)`); `Contains(shape, box, point)`.
- `ClipToFacingSide(shape, box, outside, leftToRight)` - the end of a route on the side of a rectangle that faces a
  point beyond it across the ranks (the bottom or top side top to bottom, the right or left side left to right), in
  line with the point and kept `EndInset` (10 pixels, at most a quarter of the side) from the corners; null for an
  ellipse or a diamond, which have no corner a route should avoid, and for a point beside the box along the ranks.
- `CutAtBox(route, box, atEnd)` - drops the part of a route inside a box, from the first segment that enters it.
- `Shorten(route, atStart, atEnd)` - moves the ends back along their legs (never past the next point).
- `ArrivalAngle(route, atEnd)` - the direction of the last (first) leg with a length, arriving at the end.
- `Arc(cx, cy, radius, start, sweep)` and `Circle(cx, cy, radius)` - cubic arcs of at most a quarter circle.
- `Extremity(decor, tip, angle)` - the decoration with its tip at the end of the link, turned to the arrival direction
  (`along` into the entity, `across` perpendicular), with PlantUML's `svek/extremity` sizes: the arrow `(0,0)
  (-9,-4) (-5,0) (-9,4)` filled, contact 5; the extension triangle `(0,0) (-18,-6) (-18,6)` hollow, contact 18
  (`LinkDecor.getExtremityFactoryComplete`); the small triangle 8 by 6, contact 8; the diamonds `(0,0) (-6,-4) (-12,0)
  (-6,4)`, filled for a composition, contact 12; the cross `(-5,-4)-(-13,4)` and `(-5,4)-(-13,-4)`, contact 8; the
  circle of radius 6 centered 6 before the tip, stroke 1.5, filled with the background or the line, contact 12; the
  circle of radius 6 on the tip with the socket arc of radius 10 over 90 degrees, contact 10; the socket arc of radius 9
  over 140 degrees, stroke 1.5, contact 10; the square of 10 on the tip, stroke 1.5, contact 5; the circled plus of
  radius 8 with its two diameters, contact 16; the half arrows 9 long with a stem of 8, contact 8.
- `IsDrawn(decor)` - false for the crow's foot family and the redefinition triangles, which are read but not drawn;
  `Margin(decor)` - PlantUML's `LinkDecor` margins (30 for the extensions, 15 for the diamonds, ...), used by the
  spacing of the layout.

#### Error Handling

`Spline` throws `ArgumentException` for a route of fewer than two points and the methods throw `ArgumentNullException`
for missing routes. A degenerate input (a zero-sized box, a direction of no length) gives the center instead of a
division by zero.

#### Dependencies

The `SvekModel` unit (`SvekPoint`, `ClipShape`, `LinkDecor`) and the Scenegraph subsystem (`SceneSegment`,
`SceneShapes`, `SceneRect`).

#### Callers

`SvekLayout`, `SvekRenderer`, the entity images of the diagram subsystems, and the tests.
