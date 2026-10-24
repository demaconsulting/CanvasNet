# System Design

<!-- cspell:ignore tessellator tessellators tessellation rasterizer rasterized supersampling supersampled -->
<!-- cspell:ignore Sutherland Hodgman polyhedra polyhedron unlit Lambertian Phong Blinn -->
<!-- cspell:ignore extrusions unmirrored Minkowski -->

This document provides the system-level design for CanvasNetRender3D.

![CanvasNetRender3D Structure](CanvasNetRender3DView.svg)

## Architecture

CanvasNetRender3D is a pure-managed, format-agnostic software 3D rasterizer, distributed as its own
NuGet package (`DemaConsulting.CanvasNet.Render3D`, root namespace
`DemaConsulting.CanvasNet.Render3D`), independent of, but depending on, the core `CanvasNet` system
(its own separate package, `DemaConsulting.CanvasNet`) — see the Dependencies section below. It
renders an immutable `Scene` of triangle meshes, as seen by a `Camera`, onto a new core `Surface`.
It has no knowledge of any 3D file format; a format front end is expected to build a `Scene` and
call the renderer, never the other way around.

`CanvasNetRender3D` consists of three subsystems, one per sub-namespace, each in its own sub-folder
of `src/DemaConsulting.CanvasNet.Render3D/` (the root namespace itself contains no types):

- **SceneModel** (namespace `DemaConsulting.CanvasNet.Render3D.SceneModel`, folder `SceneModel/`):
  the data model, with three units. `DoublePrecisionMath` provides the `Vector2D`, `Vector3D`,
  `Matrix4x4D`, and `BoundingBox3D` value types, all in 64-bit floating point. `Mesh` provides the
  immutable, validated, indexed triangle `Mesh`, the vertex-welding `MeshBuilder`, and the internal
  `MeshTopology` helper. `SceneGraph` provides the immutable scene graph — the abstract `SceneNode`,
  `MeshNode`, `GroupNode`, `NodeAppearance`, and the `Scene` root — and the internal `SceneInstance`
  flattened view. See _SceneModel Subsystem Design_ (`canvas-net-render3d/scene-model.md`).
- **Tessellation** (namespace `DemaConsulting.CanvasNet.Render3D.Tessellation`, folder
  `Tessellation/`): one unit, `PrimitiveTessellator`, a static factory for closed, welded,
  outward-oriented box, sphere, and cylinder (including cone and frustum) meshes, and for polyhedron
  meshes built from caller-supplied faces whose winding and closure are preserved as given. See
  _Tessellation Subsystem Design_ (`canvas-net-render3d/tessellation.md`).
- **SceneRendering** (namespace `DemaConsulting.CanvasNet.Render3D.SceneRendering`, folder
  `SceneRendering/`): the rasterization pipeline, with four units. `Camera` provides the
  perspective/orthographic `Camera`, `LookAt`, `FitToBounds`, and the internal camera-relative
  `ViewTransform`. `Lighting` provides `Light`, `AmbientLight`, `DirectionalLight`, and the internal
  `LightingModel`. `TriangleRasterizer` provides the internal homogeneous clipper, the 24.8
  fixed-point setup and top-left-rule rasterizer, and the z-buffer `DepthTestVisitor`.
  `SceneRenderer` provides the public `SceneRenderer` and `SceneRenderOptions`, and the internal
  tile binner, sample resolver, and per-worker tile workspace. See _SceneRendering Subsystem Design_
  (`canvas-net-render3d/scene-rendering.md`).

Dependencies between the subsystems are strictly one-directional: `Tessellation` depends on
`SceneModel`; `SceneRendering` depends on `SceneModel`; `SceneModel` depends on neither. The
`SceneRendering` units depend on each other as follows: `SceneRenderer` calls `Camera`, `Lighting`,
and `TriangleRasterizer`; `Camera` and `Lighting` do not depend on the rasterizer.

## External Interfaces

The system exposes the following public API:

- **`SceneModel` namespace**: `Vector2D`, `Vector3D`, `Matrix4x4D`, and `BoundingBox3D` (immutable
  double-precision value types); `Mesh` (an immutable triangle mesh) and `MeshBuilder` (a mutable
  construction helper with vertex welding); the closed `SceneNode` hierarchy `MeshNode` and
  `GroupNode`; the `NodeAppearance` record; and `Scene`.
- **`Tessellation` namespace**: the static `PrimitiveTessellator` with `Box`, `Sphere`, `Cylinder`,
  and `Polyhedron`.
- **`SceneRendering` namespace**: `Camera` and `CameraProjection`; `Light`, `AmbientLight`, and
  `DirectionalLight`; `SceneRenderOptions`; and the static `SceneRenderer` with `Render` and
  `RenderFitted`.

<!-- markdownlint-disable MD013 -->
| Interface | Direction | Format | Constraints |
| ------------------------------- | ---------------- | ------------------------------------ | ---------------------------------------------------------------------- |
| `Mesh` constructor | Inbound | Vertex and triangle-index sequences | Index count a multiple of three; indices in range; vertices finite; inputs copied |
| `GroupNode` constructor | Inbound | Child node sequence | Sequence and each child non-null; sequence copied |
| `SceneNode.Transform` | Inbound | `Matrix4x4D` | Every element finite |
| `PrimitiveTessellator.*` | Inbound/Outbound | Dimensions / `Mesh` return | Positive finite dimensions, at least three segments, valid face indices |
| `Camera` properties | Inbound | `Vector3D`, `double`, enumeration | Each value validated at initialization; cross-property rules at render time |
| `SceneRenderOptions` properties | Inbound | Colors, lights, numeric options | Each value validated at initialization; instance therefore always valid |
| `SceneRenderer.Render(...)` | Inbound/Outbound | `Scene`, `Camera`, size / `Surface` | Non-null scene and camera; each dimension 1 to `Surface.MaxDimension` |
| `SceneRenderer.RenderFitted(...)` | Inbound/Outbound | `Scene`, size / `Surface` | Non-null scene; each dimension 1 to `Surface.MaxDimension` |
| `CancellationToken` argument | Inbound | Cooperative cancellation token | Cancellation yields `OperationCanceledException` |
<!-- markdownlint-enable MD013 -->

The returned `Surface` is owned by the caller, who disposes it. See the subsystem and unit design
documents (_SceneModel Subsystem Design_, _Tessellation Subsystem Design_, and _SceneRendering
Subsystem Design_, and the unit designs beneath them) for every type's complete constructor,
property, and exception detail.

## Dependencies

`CanvasNetRender3D` depends on the separate `CanvasNet` system (its own package,
`DemaConsulting.CanvasNet`, referenced via a project reference from
`src/DemaConsulting.CanvasNet.Render3D/DemaConsulting.CanvasNet.Render3D.csproj`), specifically its
`Canvas` subsystem only:

- The `Surface` unit — the pixel buffer `SceneRenderer` allocates, writes (through the allocation-free
  `GetRowSpan` row access), and returns
- The `Rgba32` unit — the color type used for node, light, background, and back-face colors

It uses no other `CanvasNet` subsystem: the renderer needs no path filling, fonts, or codecs.

**`CanvasNetRender3D` must never reference `DemaConsulting.CanvasNet.Svg`,
`DemaConsulting.CanvasNet.Pdf`, `DemaConsulting.CanvasNet.Pptx`, `DemaConsulting.CanvasNet.Vsdx`,
`DemaConsulting.CanvasNet.Charts`, or any future 3D file-format package.** This is a hard
architectural constraint: the renderer is a reusable, format-agnostic library, and any format front
end depends on it, never the reverse.

Beyond the `CanvasNet` system, `CanvasNetRender3D` uses only the .NET base class library:
`System.Numerics` (`Vector3` is used solely for the documented single-precision narrowing step),
`System.Threading.Tasks.Parallel` (tile parallelism), `System.Collections.ObjectModel`
(`ReadOnlyCollection<T>`, for defensive-copy immutable snapshots), and `System.Threading`
(`CancellationToken`). These are platform types available on every target framework, not OTS
Software Items, and require no new runtime NuGet dependency; the project references exactly the
same build-time analyzers and tooling packages as its sibling projects.

This is an ordinary, same-repository, system-to-system dependency: both `CanvasNet` and
`CanvasNetRender3D` are produced by this repository, so it is neither an OTS Software Item nor a
Shared Package (that category is scoped to a package produced by a _different_ repository within
the same program). `CanvasNetRender3D` introduces no new OTS Software Item beyond those already
used to build and verify the `CanvasNet` system — see _OTS Integration Design_
(`docs/design/ots.md`).

## Risk Control Measures

`CanvasNetRender3D` has no segregation requirement for safety. Its risk control measures are
defensive-correctness measures against invalid input, numeric failure, and resource misuse:

- **Fail-closed validation at construction.** `Mesh`, `GroupNode`, `SceneNode.Transform`, `Camera`
  properties, `Light` properties, `SceneRenderOptions` properties, and every `PrimitiveTessellator`
  factory validate their arguments eagerly and throw a specific exception
  (`ArgumentNullException`, `ArgumentException`, or `ArgumentOutOfRangeException`) rather than
  coercing or clamping. Because the model types are immutable once constructed and
  `SceneRenderOptions` validates each property when it is initialized, the renderer can trust every
  `Scene`, `Camera`, and options instance it receives. The only checks the renderer itself performs
  are those that span several properties and therefore cannot be made at initialization (eye
  versus target, orthographic height versus projection, near versus far) and the output size.
- **Overflow containment.** All coverage decisions use 64-bit integer edge functions over 24.8
  fixed-point sample coordinates. Geometry is clipped to a guard band of 2^20 samples about the
  screen center, which bounds fixed-point magnitudes below about 2^29 and edge-function products
  below about 2^59, so the 64-bit arithmetic can never overflow. Any value that still cannot be
  represented (non-finite or outside the fixed-point range) causes the triangle to be skipped, never
  to wrap.
- **No unbounded recursion.** Scene traversal uses an explicit stack, so a very deeply nested group
  hierarchy cannot overflow the call stack.
- **Bounded working memory.** The per-worker sample buffers are sized for a single tile (at most
  128 x 128 samples), independent of the output size.
- **Resource safety.** A `Surface` is allocated only after all validation and triangle preparation
  has succeeded, and is disposed before any exception (cancellation or otherwise) leaves the
  renderer, so a partially rendered surface is never leaked.
- **Cooperative cancellation.** Long renders observe a `CancellationToken` at entry, after scene
  flattening, periodically during triangle setup, and at the start of every tile.

## Data Flow

**Scene construction path:**

1. **Input**: Vertex and index sequences (or a `PrimitiveTessellator` call), `Matrix4x4D`
   transforms, optional `NodeAppearance` colors
2. **Validation**: Each constructor or initializer validates its arguments and copies any
   caller-supplied collection (see Risk Control Measures)
3. **Processing**: `MeshBuilder` welds identical vertices; `MeshNode`/`GroupNode` compose the graph
4. **Output**: An immutable `Scene`, shareable between threads and between renders

**Rendering path (`SceneRenderer.Render`):**

1. **Validate** the scene, camera, size, and options (a null `options` selects
   `SceneRenderOptions.Default`) and check the cancellation token
2. **Flatten** the scene by an iterative depth-first pre-order traversal into mesh instances, each
   with a composed world matrix, innermost-wins appearance, and a pre-order index that serves as
   its draw priority
3. **Transform** every instance vertex in double precision to camera-relative view space (see the
   Coordinate and Precision Conventions below) and gather the view-depth range and scene diameter
4. **Resolve the clip range**: the camera's `NearPlane`/`FarPlane` when set, otherwise values
   derived from the depth range
5. **Prepare triangles**: for each triangle (swapping two indices when the instance's world
   matrix has a negative determinant) compute the double-precision face normal, shade the front and
   back faces once (flat shading), narrow the vertices to single precision, clip against the near
   plane (and the guard band when needed), project to the sample grid, convert to 24.8 fixed point,
   and orient by sign of area to record front/back facing
6. **Bin** the prepared triangles into 32 x 32-pixel tiles by a two-pass counting sort, preserving
   ascending triangle order within each tile
7. **Render tiles**, in parallel or sequentially: per tile, rasterize each binned triangle with
   exact integer edge functions into a depth-tested sample buffer (`k` x `k` samples per pixel,
   `k` being the supersampling factor), then resolve the samples into the output pixels with a
   premultiplied-alpha box filter
8. **Output**: A new `Surface` of the requested size, owned by the caller

`SceneRenderer.RenderFitted` first computes the scene's tight bounds, derives an isometric camera
from `Camera.FitToBounds` (azimuth 45 degrees, elevation about 35.26 degrees, 5 % margin, aspect
ratio from the output size), and then follows the path above.

## Design Constraints

- **Simplicity**: Minimal functionality kept easy to understand and extend
- **Compliance**: All functionality must be traceable to requirements
- **Quality**: Zero warnings, full test coverage, complete documentation
- **Portability**: Compatible across supported .NET platforms
- **Dependency direction**: `CanvasNetRender3D` must never reference `CanvasNetSvg`, `CanvasNetPdf`,
  `CanvasNetPptx`, `CanvasNetVsdx`, `CanvasNetCharts`, or a future 3D format package (see
  Dependencies above)
- **Determinism**: Output is byte-identical across runs, platforms, and degrees of parallelism

### Platform Support

The library targets the following frameworks, identical to the `CanvasNet` system it depends on,
enabling compatibility across modern, currently supported .NET runtimes:

| Target Framework | Runtime / Environment |
| ---------------- | --------------------- |
| `net8.0`         | .NET 8 LTS            |
| `net9.0`         | .NET 9                |
| `net10.0`        | .NET 10               |

The library is supported on the following operating systems:

- **Windows** — primary developer and CI platform
- **Linux** — CI/CD and containerized environments
- **macOS** — developer workstations using Apple platforms

Portability is achieved by restricting the implementation exclusively to Base Class Library (BCL)
APIs and the `CanvasNet` system's own public API, available across all target frameworks. No
platform-specific native interop, hardware intrinsics, OS-specific APIs, or
framework-version-specific features are used; coverage is computed in integer arithmetic, so the
output does not depend on the platform's floating-point behavior for which samples are covered.

### Integration Patterns

- **NuGet Packaging**: Standard .NET library packaging and distribution, referencing the
  `CanvasNet` package as an ordinary NuGet dependency
- **CI/CD Integration**: Automated build, test, and quality validation
- **Requirements Traceability**: All features linked to passing tests
- **Format front ends**: A future package that reads a 3D format builds a `Scene` (using
  `MeshBuilder`, `PrimitiveTessellator`, and the node types) and calls `SceneRenderer`; the renderer
  never calls back into a front end

### Coordinate and Precision Conventions

These conventions are part of the public contract and are pinned by tests:

- **World space** is right-handed with **+Z up**.
- **View space** is right-handed; the camera sits at the origin, looks down **-Z**, with **+Y up**
  and **+X right**. View depth is `-Z`.
- **Screen and sample space** have x to the right and **y down**; the center of pixel `i` is at
  `i + 0.5`.
- **Mesh winding**: a triangle whose vertices are **counter-clockwise (CCW) as seen from outside**
  has its right-hand-rule normal pointing outward. Every primitive produced by
  `PrimitiveTessellator` follows this convention (except `Polyhedron`, which preserves the
  caller-supplied winding as given), and a closed mesh with this winding has a
  positive `SignedVolume`.
- **Matrices** use the same row-vector convention as `System.Numerics.Matrix4x4`: a point
  transforms as `v' = v * M`, translation lives in `M41`..`M43`, and `a * b` applies `a` first and
  then `b`. A node's world matrix is its own transform multiplied by its parent's world matrix
  (`node.Transform * parentWorld`). Rotation angles are in degrees and right-handed; rotations by
  multiples of 90 degrees use exact sine and cosine values, so axis-aligned rotations introduce no
  rounding error.
- **Mirroring**: a transform with a negative determinant reverses winding. `Mesh.Transform` and
  the renderer both swap two indices of every triangle of such an instance, so a mirrored mesh
  keeps outward-CCW winding and shades identically to the unmirrored equivalent.
- **Orbit convention** of `Camera.FitToBounds`: azimuth 0 and elevation 0 place the eye on the
  negative Y axis looking along +Y (a front view); positive elevation raises the eye toward +Z;
  positive azimuth moves the eye counter-clockwise as seen from +Z (from -Y toward +X).
- **Double precision, camera-relative**: all model-space math (`DoublePrecisionMath`, scene
  traversal, bounds) is 64-bit. The renderer composes `world * translate(-eye) * rotation` in
  double precision and transforms every vertex in double precision, so the eye position cancels
  before anything is narrowed to single precision; precision is therefore relative to the view
  distance, not to the distance from the origin. Only the view-space vertices are narrowed to
  single precision (`System.Numerics.Vector3`); face normals are computed from the un-narrowed
  double-precision vertices, and clipping, projection, and fixed-point conversion use double
  arithmetic.
- **Fixed-point coverage**: sample coordinates are rounded to 24.8 fixed point; sample `(i, j)` has
  its center at `(256 i + 128, 256 j + 128)` fixed units. A sample is covered when all three edge
  functions are positive, or zero on a top-left edge (with the normalized orientation, an edge
  owns its boundary samples when `dy < 0` or `dy == 0 && dx > 0`). A neighbor sharing an edge sees
  it reversed, so every boundary sample is covered exactly once: no gaps and no double hits. The
  guard band is 2^20 samples.
- **Clip-edge consistency**: a clipped-edge intersection is computed with the edge endpoints put in
  canonical lexicographic (X, Y, Z) order, so two triangles sharing an edge compute bit-identical
  intersection points and no crack opens along a clipped edge.
- **Depth tie rule**: with tolerance `eps = 1e-6 * max(sceneDiameter, farthestVertexDepth)`, a
  fragment replaces the stored one when it is nearer by more than `eps`, and is discarded when it
  is farther by more than `eps`. Within `eps` the fragments are coincident: a front-facing fragment
  beats a back-facing one, then the lower draw priority (earlier in scene pre-order), then the
  earlier rasterized triangle; the stored depth becomes the minimum of the two. This rule is
  documented as not being a strict weak ordering, but its evaluation order is fixed, so the outcome
  is deterministic.
- **Tiling**: tiles are 32 x 32 output pixels (`32k` x `32k` samples), independent, and write
  disjoint pixel ranges of the output `Surface`, which is why parallel execution is bit-identical
  to sequential execution.
- **Supersampling**: an ordered `k` x `k` grid, `k` from 1 to 4 (default 3), resolved with an
  integer box filter in premultiplied alpha.
- **Cancellation**: cooperative; surfaces as a plain `OperationCanceledException` (never wrapped in
  an `AggregateException`).

### Alpha and Transparency (Phase 1 Limitation)

Surfaces are opaque in Phase 1: the alpha of a node color, light color, default color, or back-face
color is ignored and shaded surfaces are written with alpha 255. The background color's alpha **is**
honored, so a render over a transparent background composites correctly with other content.
Transparent surfaces are deferred to a later phase.

### Closed-Mesh and Winding Contract

The renderer assumes, but does not verify, that meshes follow the outward-CCW convention.
`Mesh.IsClosed` (every directed edge occurs exactly once and its reverse occurs exactly once) and
`Mesh.SignedVolume` let a caller check this; a closed mesh with negative `SignedVolume` is
inside-out and can be repaired with `Mesh.Reverse`. `PrimitiveTessellator.Polyhedron` validates only
the structure of its input and intentionally preserves the supplied faces and winding as given,
without closing the surface or correcting its orientation; closure and outward orientation of a
polyhedron are the caller's responsibility. A
back-facing surface is not culled but painted with `SceneRenderOptions.BackFaceColor`, which makes
inside-out or open meshes visible instead of silently disappearing.

### Pure-Managed Implementation

The rasterizer is implemented entirely in managed code on the BCL, with no native library, no
hardware intrinsics, and no unsafe code, so the library runs unchanged in any environment that
runs the target .NET runtimes.

## Implementation Phase Plan

`CanvasNetRender3D` is delivered incrementally; each phase is a separate pull request that adds
requirements, design, verification, and tests for its own scope.

1. **Phase 1 — Foundation (delivered).** Everything described in this document: double-precision
   math, immutable meshes, the scene graph without constructive solid geometry, primitive
   tessellation, the camera, ambient and directional lighting, the fixed-point tiled z-buffer
   rasterizer, supersampling antialiasing, deterministic tile parallelism, and cooperative
   cancellation.
2. **Next — Constructive solid geometry.** A CSG node type added to the closed `SceneNode`
   hierarchy and a fragment-list resolver in the `SceneRendering` subsystem, relying on the
   closed-mesh, consistent-winding, and watertight-clipping guarantees established in Phase 1.
3. **Later — Polygons and extrusions**, with polygon triangulation (Phase 1 polyhedron faces must
   be convex) and linear/rotational extrusion.
4. **Later — Format front ends**, as separate packages that depend on this one.
5. **Later — Hull and Minkowski operations**, **transparency** (sorted fragment compositing; node
   alpha is ignored until then), and **performance work** (vectorized edge evaluation and per-tile
   pruning, with equivalence tests against the scalar path; changing the default degree of
   parallelism is a non-breaking, one-line option change).

Every later phase adds new init-only properties to `SceneRenderOptions` and new node types rather
than changing the signatures of `SceneRenderer.Render` and `SceneRenderer.RenderFitted`, which
already carry `options` and `cancellationToken` parameters.
