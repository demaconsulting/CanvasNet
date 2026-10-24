## Tessellation

<!-- cspell:ignore tessellator tessellation -->

![Tessellation Structure](TessellationView.svg)

### Overview

The `Tessellation` subsystem (namespace `DemaConsulting.CanvasNet.Render3D.Tessellation`, folder
`src/DemaConsulting.CanvasNet.Render3D/Tessellation/`) turns analytic solids into triangle meshes.
It contains three units:

- **PrimitiveTessellator** — the static `PrimitiveTessellator` factory (`Box`, `Sphere`,
  `Cylinder`, `Polyhedron`, and the 2D `Rectangle`, `Ellipse`, `Circle`, `Polygon`); see
  _PrimitiveTessellator Unit Design_ (`tessellation/primitive-tessellator.md`)
- **PolygonTriangulator** — the immutable, validated `PolygonRegion`, the exact predicates, and the
  ear-clipping `PolygonTriangulator` with hole bridging; see _PolygonTriangulator Unit Design_
  (`tessellation/polygon-triangulator.md`)
- **Extruder** — the static `Extruder` (`Linear` with twist, scale, and apex; `Rotate`); see
  _Extruder Unit Design_ (`tessellation/extruder.md`)

The subsystem is a separate tier so that further generators can be added behind the same
architectural boundary without touching `SceneModel` or `SceneRendering`. Its namespace
documentation is in `NamespaceDoc.cs`.

### Interfaces

**Exposed**: the public static classes `PrimitiveTessellator`, `PolygonTriangulator`, and
`Extruder`, and the public sealed class `PolygonRegion`. The 3D factories and the `Extruder` return a
`SceneModel` `Mesh`; the 2D factories return a `PolygonRegion`.

**Consumed**: the `SceneModel` subsystem's `Mesh`, `MeshBuilder`, `Vector2D`, and `Vector3D`. It does
not depend on `SceneRendering` or on the core `CanvasNet` system.

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
defective mesh later. `Extruder` builds its vertex rings once and shares them by index between walls
and caps, and the caps are triangulated by `PolygonTriangulator` without added vertices, so every
extruded mesh is welded, closed, outward, free of degenerate triangles, and a valid
constructive-solid-geometry operand. A region with a hole is extruded as one connected shell with a
through-hole (the hole is bridged into the cap triangulation), because independent outer and hole
shells would paint a coplanar outer cap across the hole on the z-buffer path; only an island inside a
hole is a separate, outward shell. See the unit designs for the algorithms and conventions.
