## SceneModel

<!-- cspell:ignore tessellator -->

![SceneModel Structure](SceneModelView.svg)

### Overview

The `SceneModel` subsystem (namespace `DemaConsulting.CanvasNet.Render3D.SceneModel`, folder
`src/DemaConsulting.CanvasNet.Render3D/SceneModel/`) is the data model of `CanvasNetRender3D`: the
double-precision math types, the immutable triangle mesh, and the immutable scene graph. It has no
dependency on the other subsystems of `CanvasNetRender3D`; `Tessellation` and `SceneRendering` both
depend on it. Its only dependency outside the .NET base class library is the core `CanvasNet`
system's `Canvas` subsystem (`Rgba32`, used by `NodeAppearance`).

It contains three units:

- **DoublePrecisionMath** — `Vector2D`, `Vector3D`, `Matrix4x4D`, and `BoundingBox3D`; see
  _DoublePrecisionMath Unit Design_ (`scene-model/double-precision-math.md`)
- **Mesh** — `Mesh`, `MeshBuilder`, and the internal `MeshTopology` and `MeshTopologyDefects`; see
  _Mesh Unit Design_ (`scene-model/mesh.md`)
- **SceneGraph** — `SceneNode`, `MeshNode`, `GroupNode`, `CsgNode`, `CsgOperation`,
  `NodeAppearance`, `Scene`, and the internal `SceneInstance`; see _SceneGraph Unit Design_
  (`scene-model/scene-graph.md`)

The subsystem's namespace documentation is in `NamespaceDoc.cs`, which declares no behavior.

### Interfaces

**Exposed** (all public, in the `SceneModel` namespace): the value types `Vector2D`, `Vector3D`,
`Matrix4x4D`, and `BoundingBox3D`; `Mesh` and `MeshBuilder`; `SceneNode`, `MeshNode`, `GroupNode`,
`CsgNode`, `CsgOperation`, `NodeAppearance`; and `Scene`. Every type is immutable once constructed except `MeshBuilder`,
which is a construction helper whose `Build` method returns independent immutable meshes.

**Internal, exposed to the renderer within the assembly**: `Mesh.VertexSpan` and `Mesh.IndexSpan`
(zero-copy read-only views of the mesh arrays), `Mesh.TopologyDefects` and `Mesh.IsInverted`
(cached defect classification), `Scene.EnumerateInstances()` and `SceneInstance` (the flattened
mesh instances with world matrix, appearance, draw order, and source `MeshNode`), `MeshTopology`,
and `MeshTopologyDefects`.

**Consumed**: the .NET base class library (`System.Collections.ObjectModel`, and `System.Numerics`'s
`Vector3` for the single-precision narrowing conversion `Vector3D.ToVector3()`), and the core
`CanvasNet` system's `Rgba32`.

### Design

The units form a short stack: `DoublePrecisionMath` is the foundation; `Mesh` stores
`Vector3D` vertices and computes bounds and volume with the math types; `SceneGraph` references
`Mesh` and composes `Matrix4x4D` transforms. All validation is at construction: a `Mesh`, `GroupNode`,
`SceneNode.Transform`, or `Scene` that exists is valid, so downstream code never re-validates.

Immutability is enforced by defensive copying: `Mesh` copies its vertex and index sequences into
private arrays and exposes them as read-only collections; `GroupNode` and `CsgNode` copy their child
sequences.
A `Scene` therefore cannot change after construction, which makes rendering deterministic and lets
one `Scene` be rendered concurrently from several threads. `Scene` caches its computed bounds, and
`Mesh` caches its bounds (at construction) and its topology defects (on first request; closedness
and inversion derive from them); these caches
are benign under concurrent access because each is a deterministic function of immutable data.

Winding and mirroring are handled in this subsystem for the data model and in `SceneRendering` for
rendering: `Mesh.Transform` swaps two indices per triangle when the matrix has a negative
determinant, and `Scene.EnumerateInstances` reports the composed world matrix so the renderer can
apply the same correction to instances without materializing a transformed mesh. The coordinate and
matrix conventions are defined in _CanvasNetRender3D System Design_ (`../canvas-net-render3d.md`),
Design Constraints.
