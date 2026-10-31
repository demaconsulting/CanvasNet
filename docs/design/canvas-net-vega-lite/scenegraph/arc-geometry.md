### ArcGeometry

![Scenegraph Structure](VegaLiteScenegraphView.svg)

The `ArcGeometry` unit is the fourth software unit in the `Scenegraph` subsystem. It is one source file, `ArcGeometry.cs`,
in the `DemaConsulting.CanvasNet.VegaLite.Scenegraph` namespace, and defines two `internal` types: `ArcGeometry` (static
class: `Build`) and `ArcPathWriter` (sealed class: the helper that records the canvas path calls of d3-path on a core
`PathBuilder`). The file carries the attribution header comment for the d3-shape, d3-path, Vega, and Vega-Lite behavior
it follows.

#### Purpose

`ArcGeometry` builds the **outline of a circular or annular sector**: the geometry of the `arc` mark. Given an inner and
an outer radius, a start and an end angle, a pad angle, and a corner radius, `Build` returns a closed core `Path`
centered at the origin; the painter translates it to the center of the item (see _ScenePainter_). It exists so that the
slices of a pie, a donut, and a radial chart have one definition that follows the d3-shape arc generator that Vega's arc
mark uses, and so that a slice can be asserted analytically (the commands, the area) without pixels. `ArcPathWriter`
exists because the core `PathBuilder` has no canvas `arc` call: it emulates the four path calls of d3-path that the
generator needs, and the painter reuses it for the capsules of a trail.

The unit's behavior is specified by the `CanvasNetVegaLite-Scenegraph-ArcGeometry-*` requirements in
`docs/reqstream/canvas-net-vega-lite/scenegraph/arc-geometry.yaml`. The geometry follows d3-shape and d3-path, as Vega
uses them, re-implemented from their public description (see _CanvasNetVegaLite System Design_, Third-Party Provenance
and Attribution).

#### Data Model

The class is static and has no state; a call builds one path on one thread.

**Angles and radii.** Angles are in radians, measured **clockwise from 12 o'clock** (the angle of the arc mark in Vega):
an angle of 0 points up and a quarter turn points right. `Build` converts them to the canvas convention (an angle of 0
points right, angles grow clockwise on screen) by subtracting a quarter turn. The sector runs from the start angle to the
end angle; when the end angle is smaller the sector runs the other way around, which is accepted. The radii are in pixels.
When the outer radius is smaller than the inner one, the two are **swapped**, so a reversed radius pair draws the same
slice.

**The shapes.** After the swap the outer radius is the larger one and the shape is one of four cases:

- **A point** - an outer radius of at most 1e-12: the path is a move to the origin and a close, a closed path with no
  area.
- **A circle or an annulus** - an angle span of a whole turn or more (more than a turn less 1e-12): the outer circle is
  one subpath (a move to its start and an arc around the center), and when the inner radius is above 1e-12 the inner
  circle is a second subpath that is wound **the opposite way**, so that the nonzero fill leaves the hole. A circle that
  is drawn by the writer is two half circles (see below).
- **A sector** - the general case, described next: the outer arc, the line (or the inner corner) to the inner radius, the
  inner arc back, and the close; with no inner radius the inner "arc" is the single point at the center, so the slice
  is a wedge.

**The sector.** `Sector` ports the general case of the d3-shape generator, in this order:

1. **Pad angle.** Half of the pad angle (the angle of each edge; a pad angle of at most 2e-12 counts as none) is used
   with the **pad radius**, the square root of the sum of the squared radii, so that the gap between two neighbors has a
   constant width in pixels instead of a constant angle. Each edge is moved inward by an angle that is derived separately
   for the outer and the inner radius: the arcsine of the pad radius divided by the radius and multiplied by the sine of
   the half pad angle (`p1` for the outer radius and `p0` for the inner one; the inner angle is the larger of the two).
   The angle span of the outer arc and of the inner arc shrink by twice their angle; a span that is no longer above 1e-12
   (or an angle that is not a number, which happens for an inner radius of zero or an arcsine argument above 1)
   **collapses** that arc to its middle angle, so a pad angle that leaves no sector draws a line or a point at the middle
   of the slice.
2. **The four corner points** - the outer arc's start `(x01, y01)` and end `(x11, y11)` and the inner arc's end
   `(x10, y10)` and start `(x00, y00)` (the names of the generator) - are computed from the padded angles.
3. **Corner radius.** The requested corner radius is first limited to **half the width of the ring** (half the difference
   of the radii). When it is above 1e-12 and the slice spans less than a half turn, it is limited further to what fits
   into the angle: the lines through the two edges of the slice are intersected (`Intersect`; lines that are nearly
   parallel, whose cross product squared is below 1e-12, do not fit a corner, so both corner radii become 0), and the
   angle between the outer corner points as seen from the intersection gives a factor that bounds the inner corner
   radius to `(r0 - l) / (k - 1)` and the outer one to `(r1 - l) / (k + 1)` (`l` is the distance of the intersection from
   the center). A slice of a half turn or more keeps the radius that the ring allows.
4. **The outer ring.** A collapsed outer arc is a move to its point. With a corner radius above 1e-12 the outer ring is
   a move to the first tangent point, a corner arc, the outer arc between the two corners, and a second corner arc; the
   corner circles come from `CornerTangents` (the center of the circle of the corner radius that touches the edge and the
   ring, and the offsets of its two tangent points, found as the intersection of the offset edge with the circle of
   radius `r1 - rc`, taking the intersection that is closer to the middle of the offset edge). When the clamp made the
   corner radius smaller than the requested one, the two corners **merge**: one corner arc joins the two edges and there
   is no outer arc between them. Without a corner radius the ring is a move to the start and one outer arc.
5. **The inner ring.** With no inner radius, or an inner arc that collapsed, the inner ring is the line to the inner end
   point; with a corner radius it is the line to the first tangent point and the corner arcs (merged in the same way,
   with the corner circles on the other side of the edge); otherwise it is one arc back along the inner radius.

The path is closed after the case (a close after a collapsed slice is harmless).

**`ArcPathWriter`** keeps the state of the d3-path object that the generator needs: the start point of the current subpath
(for a close), the current point, whether a subpath was started, and whether the core builder has an open subpath. It works
in double precision and converts to the single precision of the core path when it records a command.

- `MoveTo` starts a subpath; `LineTo` draws a line (and is a move when no subpath was started); `Close` closes the subpath
  and does nothing before the first move; a call after a `Close` **continues at the start of the closed subpath**
  (the writer reopens it with a move), as in d3-path.
- `Arc(x, y, r, startAngle, endAngle, ccw)` emulates the canvas `arc` call with an **SVG elliptical arc command**
  (`PathBuilder.ArcTo` with equal radii and no rotation). First it connects to the start of the arc: a move when no
  subpath was started, a line when the current point is not coincident with the start (a difference of more than 1e-6
  in either coordinate), and nothing otherwise. A radius of 0 stops there. The sweep is taken in the direction of the
  call (a negative sweep is moved into the range of a turn, the way d3-path does); a sweep of **more than a turn less
  1e-6** is
  a complete circle and is recorded as **two half circles** (an arc to the opposite point and an arc back to the start,
  each with the large-arc flag, because one SVG arc cannot have the same start and end point); a sweep of at most 1e-6
  adds nothing; any other sweep is **one arc** with the large-arc flag set for a sweep of at least a half turn, and the
  sweep flag set for the clockwise direction.
- The writer is **total**: a coordinate that does not fit a single, a radius that is not finite or is negative, or an
  angle that is not finite makes the writer invalid, the remaining calls are ignored, and `Build` returns `Path.Empty`.

**Limits** (every loop or allocation of the unit): none that depends on the input. A call has no loop, builds at most
about 20 commands (the corner arcs and the two rings of a rounded annular sector are the largest case), and allocates one
`PathBuilder`; the work is constant.

**Determinism.** The unit uses only its arguments; no clock, culture, or random source. The same arguments give the same
path on every call, thread, and machine of one runtime. The trigonometric functions of `System.Math` (`Sin`, `Cos`,
`Atan2`, `Asin`, and `Acos`) can differ in the last bit between runtimes and operating systems; the writer rounds every
coordinate to the single precision of the core path, so such a difference can at most move a coordinate by the last bit
of a `float`, far below what a painted pixel shows. The tests compare coordinates with a tolerance of 1e-4 because the
path stores single-precision numbers.

#### Key Methods

- `ArcGeometry.Build(double innerRadius, double outerRadius, double startAngle, double endAngle, double padAngle,
  double cornerRadius)` - the closed outline of the sector centered at the origin, or the empty path.
- `ArcPathWriter.MoveTo`, `LineTo`, `Arc`, `Close`, and `Build` - the d3-path calls and the recorded core path.

**Total function rules**: an input (a radius, an angle, a pad angle, or a corner radius) that is **not finite or has a
magnitude above 1e9** gives `Path.Empty`; so does an outline that would contain a coordinate that does not fit a single
(the writer becomes invalid). A **negative radius counts as 0** (so a slice whose two radii are both below zero is a
point), and a negative pad angle or corner radius counts as 0. An outer radius of at most 1e-12 is a single point.

**Preconditions and postconditions**: none; on return the path is empty, a point, or a closed path centered at the origin
whose coordinates are finite.

#### Error Handling

The unit never throws and never reports: every input has a result, and an input that cannot be drawn gives the empty
path, which the painter draws as nothing. There are no exceptions to convert, nothing is allocated in proportion to the
input, and the unit performs no I/O.

**Documented differences from the d3 generators** (the first two are pinned by tests):

1. **Totality**: a value that is not finite or is beyond one billion gives the empty path, and so does any outline
   with a
   coordinate that does not fit a single; a negative radius, pad angle, or corner radius counts as 0
   (`ArcGeometry_Build_DegenerateAndNonFiniteInputsGiveEmptyOrPointPaths`). This bound is the totality of the geometry
   alone; the painter draws nothing for a center, radius, or corner radius beyond its much smaller bound of 32,768 surface
   pixels (see _ScenePainter_), which is what keeps the cost of painting an arc bounded.
2. **Arc commands**: the canvas `arc` call is recorded as SVG arc commands of the core path, and a full circle as two half
   circles (`ArcGeometry_Build_FullCircleAndAnnulusUseOppositeWindingSubpaths`).
3. **Precision**: the writer works in double precision and rounds to the single precision of the core path when it
   records a command.

#### Dependencies

- **`Path`** and **`PathBuilder`** - the core CanvasNet path types (`Geometry`): the result and its builder (`MoveTo`,
  `LineTo`, `ArcTo`, `Close`, `Build`), and `Path.Empty`
- **.NET base class library** - `System.Math` and `System.Numerics` (`Vector2`)

The unit has no dependency on another unit of the system; it needs nothing of the scene model.

#### Callers

`ScenePainter` draws a `SceneArc` by building its outline with `ArcGeometry.Build` and translating it to the center of the
item (`ScenePainter.Paths.cs`); the trail painter of the same file records the capsules of a `SceneTrail` with
`ArcPathWriter` directly. `MarkBuilder` only supplies the radii and angles of the `SceneArc` items (see _SceneModel_) and
does not call the unit. The tests in `test/DemaConsulting.CanvasNet.VegaLite.Tests/Scenegraph/`
(`ArcGeometryTests.cs`, with
`ScenePainterPathTests.cs` for the painted slices) call the types directly through `InternalsVisibleTo`.
