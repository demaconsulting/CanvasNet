## Tessellation

<!-- cspell:ignore tessellator tessellation -->

![Tessellation Structure](TessellationView.svg)

### Overview

The `Tessellation` subsystem (namespace `DemaConsulting.CanvasNet.Render3D.Tessellation`, folder
`src/DemaConsulting.CanvasNet.Render3D/Tessellation/`) turns analytic solids into triangle meshes.
It contains one unit:

- **PrimitiveTessellator** — the static `PrimitiveTessellator` factory (`Box`, `Sphere`,
  `Cylinder`, `Polyhedron`); see _PrimitiveTessellator Unit Design_
  (`tessellation/primitive-tessellator.md`)

The subsystem is a separate tier so that further generators (for example, polygon triangulation and
extrusion in a later phase) can be added behind the same architectural boundary without touching
`SceneModel` or `SceneRendering`. Its namespace documentation is in `NamespaceDoc.cs`.

### Interfaces

**Exposed**: the public static class `PrimitiveTessellator`. Every method returns a `SceneModel`
`Mesh`.

**Consumed**: the `SceneModel` subsystem's `Mesh`, `MeshBuilder`, and `Vector3D`. It does not
depend on `SceneRendering` or on the core `CanvasNet` system.

### Design

Every primitive is built through `MeshBuilder`, which welds bit-identical vertices, and each vertex
is computed once and referenced by index everywhere it is used (the sphere's azimuthal seam wraps by
index rather than duplicating vertices; cylinder caps use a dedicated center vertex; a cone has a
single apex vertex). The results are therefore watertight, closed meshes (`Mesh.IsClosed` is true)
with counter-clockwise-outward winding and positive `SignedVolume`, the contract the renderer's
lighting and back-face handling rely on. Argument validation is eager and fail-closed. See
_CanvasNetRender3D System Design_ (`../canvas-net-render3d.md`) for the coordinate conventions.
