### Extruder

<!-- cspell:ignore tessellator triangulator triangulated collinear frustum extruder extrusion extrusions extruded -->
<!-- cspell:ignore sinδ Pappus faceting apex -->

The `Extruder` unit turns a `PolygonRegion` into a solid `Mesh`. It is a public static partial
class, `Extruder`, in the `DemaConsulting.CanvasNet.Render3D.Tessellation` namespace, split over
`Extruder.cs` (shared helpers and the uniform-scale overload of `Linear`), `Extruder.Linear.cs`, and
`Extruder.Rotate.cs`.

#### Purpose

`Extruder` produces closed, welded, outward-oriented (counter-clockwise seen from outside) meshes
with positive `SignedVolume`, free of degenerate triangles, from 2D profiles. Every result is a
valid constructive-solid-geometry operand: the operand validator reports nothing for it, in strict
mode included. Meshes are built so that this holds by construction (shared vertex indices), not by
recomputing coordinates and hoping they compare equal.

**Limits of the guarantee.** The statements above hold in exact arithmetic for profiles and
parameters of ordinary magnitude. Two classes of input fall outside them, and both are documented in
the `Extruder` XML remarks and the user guide:

- *Finite precision.* With extreme values (a tiny non-zero scale such as `1e-17`, a height near
  `double.MaxValue`, or coordinates of about `1e200`) the rounded vertices can make a triangle
  degenerate (zero area or overflow), which the operand validator flags as a `DegenerateTriangle`
  defect. The geometry is correct in exact arithmetic; no clamping or relative threshold is applied,
  because any threshold would be arbitrary.
- *Self-overlap about the origin.* Scale and twist act about the origin. An apex, a large scale
  change, or a per-axis scale combined with twist is only guaranteed valid when the profile is
  star-shaped about the origin; otherwise the result can self-intersect (it can be reported as
  inverted) and is not a valid operand.

A third class would be inputs whose result is pinched (a non-manifold vertex although every edge
stays manifold); these are **rejected** instead of documented: an apex over a region with more than one
contour, and a rotational profile that touches the axis at one isolated vertex.

#### Data Model

`Extruder` is stateless and thread-safe. Each method builds its result through a local
`MeshBuilder` and returns an immutable `Mesh`. The vertex **rings** of the walls are created once and
referenced by index from the wall quads of both neighboring slices and from the caps. The caps come
from `PolygonTriangulator`, which adds no vertices, so the caps share the ring vertex indices with
the walls and no T-junction can appear. Because the region's contours are normalized (solid on the
left of travel, outer boundaries counter-clockwise, holes clockwise), one wall formula produces
outward normals for outer walls and for hole walls (which then face into the hole).

#### Key Methods

- `Linear(PolygonRegion region, double height, bool center = false, double twistDegrees = 0.0, int
  slices = 1, Vector2D? scale = null)`: extrudes along +Z from `z = 0` to `z = height`, or
  `+/- height/2` when `center` is true. Ring `k` (`k = 0..slices`) sits at `t = k / slices`: height
  `z0 + height * t`, per-axis scale `1 + (scale - 1) t` applied first (about the origin), then a
  rotation of `twistDegrees * t` about +Z. `Linear(region, height, center, twistDegrees, slices,
  double uniformScale)` is the overload for a uniform scale.
- `Rotate(PolygonRegion region, double angleDegrees, int segments)`: revolves the profile about the
  +Z axis, with profile X as the radius and profile Y as the height.

**Linear extrusion rules.**

- *Twist sign*: a positive `twistDegrees` rotates the top ring counter-clockwise seen from +Z
  (right-hand rule about +Z, as `Matrix4x4D.CreateRotationZ`) relative to the bottom. A language
  front end with a different convention negates the angle.
- *Scale rules*: each component must be finite and not negative. Both components positive scales the
  top ring (a frustum shape); **both zero** is the *apex* case; exactly one zero component or any
  negative component is rejected (`ArgumentOutOfRangeException`): one zero flattens the top ring to a
  line (degenerate triangles) and a negative scale passes through zero mid-extrusion, so the solid
  would self-intersect. Interpolation is linear per ring from 1 at the bottom to the requested scale
  at the top.
- *Apex*: with scale `(0, 0)` the last ring is the single apex vertex `(0, 0, z_top)` shared by every
  contour. Because every contour would then meet at that one vertex, the apex is accepted only for a region
  with a **single contour**: a hole or a second island would leave a pinched (non-manifold) vertex, so
  such a region is rejected with `ArgumentException` (the scale case with exactly one zero component is
  rejected for every region). A wall triangle with a repeated corner is skipped, leaving one wall triangle per edge in the
  last slice, and **no top cap** is emitted. The volume of a prism-to-apex extrusion is `A h / 3`.
- *Walls and caps*: for every contour edge `i -> i+1` and slice `k -> k+1`, the triangles
  `(b_i, b_i+1, t_i+1)` and `(b_i, t_i+1, t_i)` are emitted (`b` = ring `k`, `t` = ring `k+1`). The
  top cap is the triangulation mapped to the last ring, the bottom cap the same triangles reversed
  and mapped to ring 0.
- *Counts* (no collinear points, no apex): `V (slices + 1)` vertices and
  `2 V slices + 2 (V + 2H - 2P)` triangles (V vertices, H holes, P solid contours).
- *Holes*: a region with a hole yields **one connected shell** with a through-hole (outer wall,
  inward-facing hole wall, and both caps share vertices). This replaces the earlier idea of
  independent, alternately oriented outer and hole shells: the coplanar outer cap of such shells would
  be painted across the hole on the z-buffer path, which shows the nearest front-facing fragment and
  never evaluates solids, so the hole would not be visible. A single bridged shell is correct on both
  paths and passes the operand validator with no report. Only an **island** inside a hole is a
  separate shell; it lies outside the ring solid and is outward oriented, so the validator's
  nesting-parity rule accepts it.
- *Faceting tolerance*: untwisted, uniformly scaled extrusions have planar facets, and the prism
  (`A h`) and frustum (`A h (1 + (s - 1) + (s - 1)^2 / 3)`) volumes are exact. Twisted or
  non-uniformly scaled wall quads are not planar, so the facet volume only approaches the smooth solid
  as `slices` grows; the error of a twist is first order in the twist angle per slice (for example
  about 1.7% for 90 degrees in 32 slices). The slice count is never refined automatically, and a large
  twist per slice can make neighboring slices intersect, so callers choose `slices` accordingly.

**Rotational extrusion rules.**

- *Axis rule*: all profile vertices must have `x >= 0`, or all `x <= 0`. The negative side is
  supported by flipping the winding; the sweep is the literal rotation of the profile from azimuth 0
  toward `+angleDegrees`. A profile with vertices on both sides (it *crosses the axis*) and a profile
  entirely on the axis are rejected with `ArgumentException`. There is **no tolerance**: `x == 0`
  exactly means a vertex on the axis (a tolerance policy belongs to the caller).
- *Isolated axis contact*: a vertex on the axis whose two contour neighbors are both off the axis is shared by
  every angular ring, so the swept surfaces would meet only at that point (a pinched, non-manifold
  vertex); the profile is rejected with `ArgumentException`. A vertex that ends an edge lying on the axis
  (a pole) is accepted.
- *Angle and segments*: `angleDegrees` must be finite in `(0, 360]`, and its radian value must not
  round to zero (for example `double.Epsilon`), which would weld the end ring onto the start ring
  and return an open mesh. Exactly 360 is a full revolution
  (`segments >= 3`, the last ring wraps to the first by index, no duplicated seam, no end caps);
  anything less is a partial sweep (`segments >= 1` angular steps, a start cap at azimuth 0 and an end
  cap at the final azimuth, both triangulated from the profile and sharing the wall vertices).
  Every angular step (`angleDegrees / segments`) must be **at most 120 degrees**, the step of the
  coarsest full revolution (360 degrees in 3 segments), so a partial sweep follows the same rule as a
  full one. Wider steps are rejected with `ArgumentOutOfRangeException` on `segments`: at 180 degrees the
  wall chords pass through the axis (zero volume) and beyond it the facets face the wrong way (inside-out,
  negative volume). A caller that tessellates by fragment count always satisfies the bound.
- *Axis welding*: every profile vertex on the axis becomes exactly one mesh vertex, not one per ring;
  a profile edge with both ends on the axis sweeps to nothing and is skipped; an edge with one end on
  the axis yields one triangle per segment; an off-axis edge yields two triangles per segment. The
  result is closed with no degenerate triangle.
- *Holes*: a profile with a hole yields a hollow solid: the outer shell is outward and the cavity
  shell (revolved hole contour) is inward. A partial sweep has bridged end caps, so it is a single
  shell.
- *Volume*: the facets are planar (parallel chords of concentric rings at identical azimuths), so the
  volume has the exact closed form `n sin(delta) * integral(|x| dA)` with `delta = angle / n` (`n` the
  segment count, `delta = 2 pi / n` for a full revolution; the integral is evaluated over the region
  with Green's theorem). It tends to the Pappus volume `angle * x_centroid * A` as `n` grows. The
  tests use this closed form.

#### Error Handling

All validation precedes any construction. `ArgumentNullException` for a null region;
`ArgumentOutOfRangeException` for a height that is not finite and greater than zero, fewer than one
slice, an invalid scale (non-finite, negative, or zero in only one axis), an angle outside `(0, 360]`
or not finite or so small that its radian value is zero, and too few segments (including a segment count that
leaves a single angular step wider than 120 degrees); `ArgumentException`
for a non-finite twist, an apex (zero scale) over a region with more than one contour, a profile that
crosses or lies entirely on the rotation axis or touches it at an isolated vertex, a sweep step
too small to resolve at a profile vertex's radius (two rings would coincide), and a result that would
exceed the maximum mesh size
(checked in `long` before allocation, so hostile counts fail with a clear exception). An empty region
extrudes to an empty mesh without throwing. Invalid polygons never reach this unit because
`PolygonRegion` rejects them at construction. Exceptions from `Mesh` construction propagate unchanged.

#### Dependencies

The `PolygonTriangulator` unit (`PolygonRegion` and `PolygonTriangulator`) of this subsystem, and the
`Mesh` unit (`Mesh`, `MeshBuilder`) and `DoublePrecisionMath` unit (`Vector2D`, `Vector3D`) of the
`SceneModel` subsystem. No dependency on `SceneRendering`.

The unit tests have one **test-only** dependency that production code does not: the shared helper
`Render3DTestHelpers.AssertValidCsgOperand` runs the internal `CsgOperandValidator` of the
`SceneRendering` subsystem (made visible to the test assembly through `InternalsVisibleTo`) on every
extruded mesh. It is used so that "valid operand" is checked by the real validator rather than by a
re-implementation of its rules; it does not make `Extruder` depend on `SceneRendering`.

#### Callers

Callers building scenes invoke `Linear` and `Rotate` directly and wrap the result in a `MeshNode`
(including as an operand of a `CsgNode`); a future format front end will use them to realize its 2D
profile operations.
