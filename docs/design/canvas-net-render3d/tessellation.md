## Tessellation

<!-- cspell:ignore tessellator tessellation -->

![Tessellation Structure](TessellationView.svg)

### Overview

The `Tessellation` subsystem (namespace `DemaConsulting.CanvasNet.Render3D.Tessellation`, folder
`src/DemaConsulting.CanvasNet.Render3D/Tessellation/`) turns analytic solids into triangle meshes.
It contains five units:

- **PrimitiveTessellator** — the static `PrimitiveTessellator` factory (`Box`, `Sphere`,
  `Cylinder`, `Polyhedron`, and the 2D `Rectangle`, `Ellipse`, `Circle`, `Polygon`); see
  _PrimitiveTessellator Unit Design_ (`tessellation/primitive-tessellator.md`)
- **PolygonTriangulator** — the immutable, validated `PolygonRegion`, the exact predicates, and the
  ear-clipping `PolygonTriangulator` with hole bridging; see _PolygonTriangulator Unit Design_
  (`tessellation/polygon-triangulator.md`)
- **Extruder** — the static `Extruder` (`Linear` with twist, scale, and apex; `Rotate`); see
  _Extruder Unit Design_ (`tessellation/extruder.md`)
- **ConvexHull** — the static `ConvexHull` (`Hull3D`, `Hull2D`) with the internal incremental
  `HullBuilder3D` engine and the exact 3D orientation predicate `SolidPredicates`; see
  _ConvexHull Unit Design_ (`tessellation/convex-hull.md`)
- **MinkowskiSum** — the static `MinkowskiSum` (`ConvexSum`, `MaxPairwiseSums`), the sum of two convex
  solids with a sound convexity test; see _MinkowskiSum Unit Design_ (`tessellation/minkowski-sum.md`)

The subsystem is a separate tier so that further generators can be added behind the same
architectural boundary without touching `SceneModel` or `SceneRendering`. Its namespace
documentation is in `NamespaceDoc.cs`.

### Interfaces

**Exposed**: the public static classes `PrimitiveTessellator`, `PolygonTriangulator`, `Extruder`,
`ConvexHull`, and `MinkowskiSum`, and the public sealed class `PolygonRegion`. The 3D factories, the
`Extruder`, `ConvexHull.Hull3D`, and `MinkowskiSum.ConvexSum` return a `SceneModel` `Mesh`; the 2D
factories return a `PolygonRegion`; `ConvexHull.Hull2D` returns a list of `Vector2D`.

**Consumed**: the `SceneModel` subsystem's `Mesh`, `MeshBuilder`, `Vector2D`, and `Vector3D` (and, inside
`MinkowskiSum`, the internal `MeshTopology` helpers). It does
not depend on `SceneRendering` or on the core `CanvasNet` system. The only link to `SceneRendering` is
test-only: the Tessellation unit tests reach the internal `CsgOperandValidator` (via
`InternalsVisibleTo`) through `Render3DTestHelpers.AssertValidCsgOperand` to check that extruded meshes
are valid operands with the real validator; production code has no such dependency.

### Design

Every primitive is built through `MeshBuilder`, which welds bit-identical vertices, and each vertex
is computed once and referenced by index everywhere it is used (the sphere's azimuthal seam wraps by
index rather than duplicating vertices; cylinder caps use a dedicated center vertex; a cone has a
single apex vertex). The `Box`, `Sphere`, and `Cylinder` results are therefore watertight, closed
meshes (`Mesh.IsClosed` is true) with counter-clockwise-outward winding and positive `SignedVolume`,
the contract the renderer's lighting and back-face handling rely on. `Polyhedron` is the deliberate
exception: it reproduces the caller-supplied faces and winding as given, with no closure or
orientation correction, so those properties are the caller's responsibility. Argument validation is
eager and fail-closed. See
_CanvasNetRender3D System Design_ (`../canvas-net-render3d.md`) for the coordinate conventions.

The polygon path follows the same principles with stronger guarantees. `PolygonRegion` is the single
place where polygon validity is decided, exactly and without tolerances: invalid polygons (non-finite,
zero-area, self-intersecting, or touching contours) throw `ArgumentException` rather than producing a
defective mesh later (as are finite coordinates so extreme that the area overflows). `Extruder` builds
its vertex rings once and shares them by index between walls
and caps, and the caps are triangulated by `PolygonTriangulator` without added vertices, so every
extruded mesh is welded, closed, outward, free of degenerate triangles (for ordinary magnitudes; the
`Extruder` unit design lists the finite-precision and self-overlap caveats), and a valid
constructive-solid-geometry operand. A region with a hole is extruded as one connected shell with a
through-hole (the hole is bridged into the cap triangulation), because independent outer and hole
shells would paint a coplanar outer cap across the hole on the z-buffer path; only an island inside a
hole is a separate, outward shell. See the unit designs for the algorithms and conventions.

The point-set path (`ConvexHull`, `MinkowskiSum`) differs in kind: its input is an unordered set of
points, so every decision is made with exact predicates (`SolidPredicates.Orient3D`,
`PolygonPredicates.Orient2D`) and the input is first reduced to its sorted set of distinct canonical
points, which makes the result a function of the point set only. `Hull3D` inserts the sorted points one by
one, each a strict extreme point at its time, and finally reduces the surface to its canonical form
(merged coplanar facets, only the corners as vertices, fan triangulation), so the mesh is welded, closed,
outward and a valid constructive-solid-geometry operand. `MinkowskiSum.ConvexSum` is the hull of the pairwise
vertex sums and is only correct for convex operands, so it verifies each operand with a sound global test
(the operand against the exact hull of its own vertices, with a documented relative tolerance of `1e-9`)
and rejects every other operand with a `NotSupportedException` instead of approximating it. Both
operations have documented, bounded work and observe the cancellation token. See the
_ConvexHull Unit Design_ and _MinkowskiSum Unit Design_ for the proofs, complexity and tolerance statement.
