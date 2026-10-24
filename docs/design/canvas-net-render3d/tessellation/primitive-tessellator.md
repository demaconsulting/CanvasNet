### PrimitiveTessellator

<!-- cspell:ignore tessellator tessellation polyhedra -->

The `PrimitiveTessellator` unit is the primitive-shape unit of the `Tessellation` subsystem. It is a
single public static partial class, `PrimitiveTessellator`, in the
`DemaConsulting.CanvasNet.Render3D.Tessellation` namespace, with four 3D factory methods
(`PrimitiveTessellator.cs`) and four 2D factory methods (`PrimitiveTessellator.Shapes2D.cs`).

#### Purpose

`PrimitiveTessellator` produces ready-made, topologically sound meshes for the solids a CAD-style
scene is built from, so callers need not hand-assemble vertex and index lists. Every `Box`,
`Sphere`, and `Cylinder` result is welded, closed, and counter-clockwise-outward oriented, which is
the contract the renderer's lighting and back-face handling assume. `Polyhedron` is the exception:
it builds its mesh from the caller-supplied points and faces and preserves the supplied faces and
winding as given, leaving closure and outward orientation to the caller. The 2D factories return
validated `PolygonRegion` profiles for the `Extruder`.

#### Data Model

`PrimitiveTessellator` is stateless. Every method builds its result through a local `MeshBuilder`
and returns an immutable `Mesh`. Each vertex is computed exactly once and referenced by index
wherever it is used, so welding never depends on two separately computed floating-point values
being equal.

#### Key Methods

- `Box(Vector3D size, bool center = false)`: an axis-aligned box spanning `[0, size]` on each axis,
  or `[-size/2, +size/2]` when `center` is true. Eight vertices, twelve triangles (two per face).
  Every component of `size` must be finite and greater than zero.
- `Sphere(double radius, int segments)`: a UV sphere centered on the origin with its poles on the
  +Z and -Z axes. `segments` is the number of azimuthal divisions (at least 3); the number of
  latitude bands is `max(2, (segments + 1) / 2)`. The two pole vertices are single shared
  vertices, each latitude ring's vertices are computed once and wrap by index (no duplicated seam),
  the poles are triangle fans, and the bands between rings are split into two triangles per quad.
  `radius` must be finite and greater than zero. All vertices lie exactly on the radius sphere; the
  inscribed polyhedron's volume is below the analytic sphere volume and converges to it as
  `segments` grows.
- `Cylinder(double height, double radius1, double radius2, int segments, bool center = false)`: a
  solid along +Z from `z = 0` to `z = height` (or `+/- height/2` when `center` is true), with
  `radius1` at the bottom and `radius2` at the top, so unequal radii give a frustum. A radius of
  zero collapses that end to a single apex vertex, yielding a **cone** with no degenerate
  triangles; both radii zero throws. Caps are triangle fans around a dedicated center vertex. The
  side wall is two triangles per segment (one when an end is an apex). `height` must be finite and
  greater than zero; each radius finite and not negative; `segments` at least 3.
- `Polyhedron(IReadOnlyList<Vector3D> points, IReadOnlyList<IReadOnlyList<int>> facesCcw)`: welds the
  points, then fan-triangulates each face, preserving the supplied faces and winding exactly as
  given; it never adds faces to close the surface and never reorients faces. Each face needs at
  least three indices, each addressing a point, and all distinct after welding: a face that repeats
  an index, or lists two indices of identical points, throws `ArgumentException` (parameter
  `facesCcw`) before the face is added. Faces must be **convex** and listed **counter-clockwise as
  seen from outside**; neither property is checked, so an inside-out polyhedron yields a mesh with
  negative `SignedVolume`
  (the caller repairs it with `Mesh.Reverse`), and a non-convex face triangulates incorrectly (the
  2D `PolygonTriangulator` does not triangulate polyhedron faces).
- `Rectangle(Vector2D size, bool center = false)`: a counter-clockwise rectangle region with the
  corners `(0,0) (sx,0) (sx,sy) (0,sy)`, or symmetric about the origin when `center` is true. Both
  components of `size` must be finite and greater than zero.
- `Ellipse(double radiusX, double radiusY, int segments)`: an inscribed polygon region centered on
  the origin whose vertex `j` is `(radiusX cos(theta_j), radiusY sin(theta_j))` with
  `theta_j = 2 pi j / segments` (counter-clockwise, starting on the +X axis). Its area is
  `n / 2 * radiusX * radiusY * sin(2 pi / n)`, slightly below the ellipse. Radii finite and greater
  than zero; `segments` at least 3.
- `Circle(double radius, int segments)`: `Ellipse(radius, radius, segments)`. The ring equals the
  ring of `Cylinder`, so `Extruder.Linear` of a circle has the volume of the cylinder.
- `Polygon(IReadOnlyList<Vector2D> points, IReadOnlyList<IReadOnlyList<int>>? paths = null)`: with no
  paths, one contour of all points in order; otherwise one contour per index path (each index must
  address a point). Which contours are outer boundaries and which are holes follows from their
  nesting (the even-odd rule of `PolygonRegion`), not from path order or winding.

#### Error Handling

All validation precedes any construction. `ArgumentOutOfRangeException` for a non-positive or
non-finite size, radius, or height, for fewer than three segments, for both radii zero, for a face
index outside the point list, and for a 2D path index outside the point list; `ArgumentException` for
a face with fewer than three indices, a repeated index, or indices of identical (welded) points, and
(from `PolygonRegion`) for an invalid 2D polygon, for example a zero-area or self-intersecting one;
`ArgumentNullException` for a null point list, face list, face, or path. Exceptions from
`MeshBuilder` (for example a non-finite point) propagate unchanged.

#### Dependencies

The `Mesh` unit (`Mesh`, `MeshBuilder`) and the `DoublePrecisionMath` unit (`Vector2D`, `Vector3D`),
both in the `SceneModel` subsystem, and the `PolygonTriangulator` unit (`PolygonRegion`) for the 2D
factories. No dependency on `SceneRendering`.

#### Callers

Callers building scenes invoke the factories directly and wrap the result in a `MeshNode`; the 2D
factories feed `Extruder`; a future format front end will use them to realize primitive shapes.
