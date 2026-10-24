### PrimitiveTessellator

<!-- cspell:ignore tessellator tessellation polyhedra -->

The `PrimitiveTessellator` unit is the only software unit in the `Tessellation` subsystem. It is a
single public static class, `PrimitiveTessellator`, in the
`DemaConsulting.CanvasNet.Render3D.Tessellation` namespace, with four factory methods.

#### Purpose

`PrimitiveTessellator` produces ready-made, topologically sound meshes for the solids a CAD-style
scene is built from, so callers need not hand-assemble vertex and index lists. Every `Box`,
`Sphere`, and `Cylinder` result is welded, closed, and counter-clockwise-outward oriented, which is
the contract the renderer's lighting and back-face handling assume. `Polyhedron` is the exception:
it builds its mesh from the caller-supplied points and faces and preserves the supplied faces and
winding as given, leaving closure and outward orientation to the caller.

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
  least three distinct indices, each addressing
  a point. Faces must be **convex** and listed **counter-clockwise as seen from outside**; neither
  property is checked, so an inside-out polyhedron yields a mesh with negative `SignedVolume`
  (the caller repairs it with `Mesh.Reverse`), and a non-convex face triangulates incorrectly until
  ear-clipping triangulation arrives in a later phase.

#### Error Handling

All validation precedes any construction. `ArgumentOutOfRangeException` for a non-positive or
non-finite size, radius, or height, for fewer than three segments, for both radii zero, and for a
face index outside the point list; `ArgumentException` for a face with fewer than three indices;
`ArgumentNullException` for a null point list, face list, or face. Exceptions from `MeshBuilder`
(for example a non-finite point) propagate unchanged.

#### Dependencies

The `Mesh` unit (`Mesh`, `MeshBuilder`) and the `DoublePrecisionMath` unit (`Vector3D`), both in the
`SceneModel` subsystem. No dependency on `SceneRendering`.

#### Callers

Callers building scenes invoke the factories directly and wrap the result in a `MeshNode`; a future
format front end will use them to realize primitive shapes.
