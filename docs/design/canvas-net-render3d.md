# System Design

<!-- cspell:ignore tessellator tessellators tessellation rasterizer rasterized supersampling supersampled -->
<!-- cspell:ignore Sutherland Hodgman polyhedra polyhedron unlit Lambertian Phong Blinn -->
<!-- cspell:ignore extrusions unmirrored Minkowski subtrahend subtrahends minuend speckle -->
<!-- cspell:ignore tessellated watertight -->

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
  `MeshNode`, `GroupNode`, `CsgNode` (with `CsgOperation`: union, difference, intersection),
  `NodeAppearance`, and the `Scene` root — and the internal `SceneInstance` flattened view. See
  _SceneModel Subsystem Design_ (`canvas-net-render3d/scene-model.md`).
- **Tessellation** (namespace `DemaConsulting.CanvasNet.Render3D.Tessellation`, folder
  `Tessellation/`): one unit, `PrimitiveTessellator`, a static factory for closed, welded,
  outward-oriented box, sphere, and cylinder (including cone and frustum) meshes, and for polyhedron
  meshes built from caller-supplied faces whose winding and closure are preserved as given. See
  _Tessellation Subsystem Design_ (`canvas-net-render3d/tessellation.md`).
- **SceneRendering** (namespace `DemaConsulting.CanvasNet.Render3D.SceneRendering`, folder
  `SceneRendering/`): the rasterization pipeline, with six units. `Camera` provides the
  perspective/orthographic `Camera`, `LookAt`, `FitToBounds`, and the internal camera-relative
  `ViewTransform`. `Lighting` provides `Light`, `AmbientLight`, `DirectionalLight`, and the internal
  `LightingModel`. `TriangleRasterizer` provides the internal homogeneous clipper, the 24.8
  fixed-point setup and top-left-rule rasterizer, the z-buffer `DepthTestVisitor`, and the
  `IFragmentVisitor` contract. `FragmentBuffer` provides the internal per-worker, budgeted store of
  every fragment of a tile. `CsgResolver` provides the internal boolean-tree compiler
  (`CsgCompiler`, `CsgProgram`) and the per-sample fragment-list `CsgResolver`. `SceneRenderer`
  provides the public `SceneRenderer`, `SceneRenderOptions`, `RenderBudgetExceededException`,
  `SceneRenderUnsupportedFeature` and `SceneRenderUnsupportedFeatureException`, and the internal
  tile binner, sample resolver, per-worker tile workspace, and `CsgOperandValidator`. See
  _SceneRendering Subsystem Design_ (`canvas-net-render3d/scene-rendering.md`).

Dependencies between the subsystems are strictly one-directional: `Tessellation` depends on
`SceneModel`; `SceneRendering` depends on `SceneModel`; `SceneModel` depends on neither. The
`SceneRendering` units depend on each other as follows: `SceneRenderer` calls `Camera`, `Lighting`,
`TriangleRasterizer`, `FragmentBuffer`, and `CsgResolver`; `FragmentBuffer` calls the rasterizer;
`CsgResolver` reads fragments from `FragmentBuffer`; `Camera` and `Lighting` do not depend on the
rasterizer.

## External Interfaces

The system exposes the following public API:

- **`SceneModel` namespace**: `Vector2D`, `Vector3D`, `Matrix4x4D`, and `BoundingBox3D` (immutable
  double-precision value types); `Mesh` (an immutable triangle mesh) and `MeshBuilder` (a mutable
  construction helper with vertex welding); the closed `SceneNode` hierarchy `MeshNode`,
  `GroupNode`, and `CsgNode` (with the `CsgOperation` enumeration); the `NodeAppearance` record; and
  `Scene`.
- **`Tessellation` namespace**: the static `PrimitiveTessellator` with `Box`, `Sphere`, `Cylinder`,
  and `Polyhedron`.
- **`SceneRendering` namespace**: `Camera` and `CameraProjection`; `Light`, `AmbientLight`, and
  `DirectionalLight`; `SceneRenderOptions` (including `MaxFragmentsPerTile`,
  `UnsupportedFeatureHandler`, and `ThrowOnUnsupportedFeature`); the diagnostics
  `RenderBudgetExceededException`, `SceneRenderUnsupportedFeature`, and
  `SceneRenderUnsupportedFeatureException`; and the static `SceneRenderer` with `Render` and
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
| `SceneRenderOptions.UnsupportedFeatureHandler` | Outbound | `SceneRenderUnsupportedFeature` report (token, message, node) | Called on the calling thread before tile work, only for CSG scenes with an unsuitable operand |
| `RenderBudgetExceededException` | Outbound | Exception carrying `MaxFragmentsPerTile` | Thrown, never wrapped, when a CSG tile needs more fragments than the budget |
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
  128 x 128 samples), independent of the output size. On the constructive-solid-geometry path each
  worker additionally holds one fragment buffer that is never larger than the per-tile fragment
  budget (`SceneRenderOptions.MaxFragmentsPerTile`); a tile that would need more fails with a
  deterministic `RenderBudgetExceededException` instead of allocating without bound (see
  _Fragment Budget and Memory Model_ below).
- **Operand diagnostics.** A scene that takes the constructive-solid-geometry path is checked, before
  any rendering work, for operands that are not closed, consistently wound, outward solids; each
  problem is reported through the caller's handler (or thrown in strict mode) with a stable feature
  token, and rendering stays bounded and deterministic regardless (see _Operand Validation
  Contract_ below).
- **Resource safety.** A `Surface` is allocated only after all validation and triangle preparation
  has succeeded, and is disposed before any exception (cancellation, budget, or otherwise) leaves the
  renderer, so a partially rendered surface is never leaked.
- **Cooperative cancellation.** Long renders observe a `CancellationToken` at entry, after scene
  flattening, periodically during triangle setup, at the start of every tile, and, on the
  constructive-solid-geometry path, periodically while collecting fragments and once per sample row
  while resolving.

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
2. **Select the path**: compile the scene tree (`CsgCompiler`); a scene without a difference or
   intersection of two or more operands (unions, groups, and union `CsgNode`s only) has no program
   and takes the z-buffer path below, unchanged and with no fragment storage; any other scene takes
   the constructive-solid-geometry path. For such a scene, and only when a handler or strict mode
   is set, validate the operand meshes and report problems before any rendering work
3. **Flatten** the scene by an iterative depth-first pre-order traversal into mesh instances, each
   with a composed world matrix, innermost-wins appearance, and a pre-order index that serves as
   its draw priority
4. **Transform** every instance vertex in double precision to camera-relative view space (see the
   Coordinate and Precision Conventions below) and gather the view-depth range and scene diameter
5. **Resolve the clip range**: the camera's `NearPlane`/`FarPlane` when set, otherwise values
   derived from the depth range of all instances
6. **Prepare triangles**: for each triangle (swapping two indices when the instance's world
   matrix has a negative determinant) compute the double-precision face normal, shade the front and
   back faces once (flat shading; on the constructive-solid-geometry path four shades, see
   _Constructive Solid Geometry Algorithm_), narrow the vertices to single precision, clip against
   the near plane (and the guard band when needed), project to the sample grid, convert to 24.8
   fixed point, and orient by sign of area to record front/back facing
7. **Bin** the prepared triangles into 32 x 32-pixel tiles by a two-pass counting sort, preserving
   ascending triangle order within each tile
8. **Render tiles**, in parallel or sequentially. On the z-buffer path, per tile: rasterize each
   binned triangle with exact integer edge functions into a depth-tested sample buffer (`k` x `k`
   samples per pixel, `k` being the supersampling factor). On the constructive-solid-geometry path,
   per tile: count and then collect every fragment of every binned triangle (failing if the tile
   exceeds the fragment budget) and resolve each sample's visible surface of the combined solid into
   the same sample buffer. Both paths then resolve the samples into the output pixels with a
   premultiplied-alpha box filter
9. **Output**: A new `Surface` of the requested size, owned by the caller

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
- **Determinism**: Output is byte-identical across runs, platforms, and degrees of parallelism, on
  both the z-buffer and the constructive-solid-geometry path
- **Bounded memory**: Per-worker memory is independent of the output size; fragment storage never
  exceeds the configured per-tile budget

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
  keeps outward-CCW winding and shades identically to the unmirrored equivalent. The facing of every
  screen triangle is classified after that swap, so on the constructive-solid-geometry path a
  mirrored operand also contributes the corrected front (+1) and back (-1) crossings.
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
- **Depth tie rule** (z-buffer path): with tolerance
  `eps = 1e-6 * max(sceneDiameter, farthestVertexDepth)`, a
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

Surfaces are opaque in Phases 1 and 2: the alpha of a node color, light color, default color, or
back-face color is ignored and shaded surfaces are written with alpha 255. The background color's
alpha **is** honored, so a render over a transparent background composites correctly with other
content. Transparent surfaces are deferred to a later phase.

### Closed-Mesh and Winding Contract

The renderer's z-buffer path needs no closed meshes, but constructive solid geometry does: every
operand must be a closed, consistently wound, outward-oriented (outward-CCW) mesh.
`Mesh.IsClosed` (every directed edge occurs exactly once and its reverse occurs exactly once) and
`Mesh.SignedVolume` let a caller check this; a closed mesh with negative `SignedVolume` is
inside-out and can be repaired with `Mesh.Reverse`. `PrimitiveTessellator.Polyhedron` validates only
the structure of its input and intentionally preserves the supplied faces and winding as given,
without closing the surface or correcting its orientation; closure and outward orientation of a
polyhedron are the caller's responsibility.

For scenes on the constructive-solid-geometry path the contract is enforced by observation rather
than by refusing to render: the operand validator (see _Operand Validation Contract_) names every
violating mesh through the handler or a strict-mode exception, and the mirror tests prove that the
winding correction of a negative-determinant transform keeps mirrored operands correct. On the
z-buffer path a back-facing surface is not culled but painted with `SceneRenderOptions.BackFaceColor`,
which makes inside-out or open meshes visible instead of silently disappearing.

### Constructive Solid Geometry Algorithm

Constructive solid geometry is rendered **without mesh boolean operations**. Every operand is
rasterized as an ordinary closed mesh and the combined solid is evaluated per sample, along the view
ray, from the depth-sorted list of the ray's surface crossings (fragments):

1. **Leaves.** The scene tree is compiled (`CsgCompiler`) into a postfix program over **leaves**. A
   leaf is a maximal union-only subtree (meshes, groups, union nodes), whose instances share one
   **signed crossing counter**: a front-facing fragment adds +1 and a back-facing one -1, and the
   leaf is inside exactly while its counter is positive. A difference is "first operand minus all
   others"; an intersection requires all operands. Scenes with no difference or intersection of two
   or more operands compile to no program.
2. **Initial state.** The counter of each leaf at the start of the ray is minus the sum of the signed
   crossings of all its fragments (including those beyond the far distance), so a camera inside a
   solid starts with that solid inside.
3. **Tie groups.** Fragments are sorted by the total order (depth, triangle index) and swept in
   atomic **tie groups**: a group starts at a fragment and contains the following fragments whose
   depth is within the tolerance of the group's first depth (see _Known Limits_ for the tolerance).
   All fragments of a group are applied to the counters before the program is re-evaluated, which is
   what makes coincident faces of different operands resolve without speckle.
4. **Visible event.** The first group that changes the program's result is the visible surface. The
   fragment chosen within the group is the one whose leaf alone explains the change, then the lowest
   draw priority, then the lowest triangle index. Its depth and shade are written into the same
   sample buffer the z-buffer fills, so tile resolve, supersampling, and parallelism are shared.
5. **Cut-surface shading.** Each triangle is shaded four ways, and the rasterizer selects the pair
   for its screen facing. With `n` the triangle's outward normal, the rule is
   color = `Shade(viewer-facing normal, X)`, with `X` the leaf color when the visible event enters
   the result and `BackFaceColor` when it leaves it (the viewer is inside material):

   <!-- markdownlint-disable MD013 -->
   | Shade | Facing | Event | Color | Meaning |
   | ----- | ------ | ----- | ----- | ------- |
   | `FrontEnter` | Front | Enters the result | `Shade(n, leaf color)` | The outer surface of the result |
   | `FrontExit` | Front | Leaves the result | `Shade(n, BackFaceColor)` | Viewer inside material, seen at a front face |
   | `BackEnter` | Back | Enters the result | `Shade(-n, leaf color)` | A cut surface exposed by a subtrahend: its own color, lit from the flipped normal |
   | `BackExit` | Back | Leaves the result | `ShadeBackFace(n)` = `Shade(-n, BackFaceColor)` | Viewer inside material, seen at a back face |
   <!-- markdownlint-enable MD013 -->

   The cut surface of a difference is therefore never painted with the back-face color.

### Correctness Prerequisites

The algorithm is exact when all of the following hold:

- **Operands are closed, consistently wound, outward-CCW meshes.** The signed counter is only
  meaningful for such meshes; open, non-manifold, inconsistently wound, degenerate, or inside-out
  meshes produce unmatched crossings (see _Operand Validation Contract_).
- **Mirrored instances are wound correctly.** The renderer swaps two indices of every triangle of a
  negative-determinant instance (before the screen-winding classification), so the facing that
  yields the +1/-1 crossing of a leaf is the corrected one; mirrored operands behave like operands
  with the mirror baked into their vertices.
- **Rasterization is watertight.** Fixed-point top-left-rule coverage and canonical clipped-edge
  intersections guarantee that a ray crossing a shared edge of a mesh is counted exactly once, so
  the counters never lose or gain a crossing at a triangle boundary.
- **Coincident faces tie.** Faces of different operands that coincide, or nearly so, are merged by
  the tie groups instead of being applied one at a time.

### Known Limits

- **Near plane.** Whole-ray initial-state inference needs the near plane to clip nothing. The
  automatic near and far planes derive from all instances' vertices, so no operand is clipped unless
  the eye is inside or behind geometry. An explicit `Camera.NearPlane` greater than the smallest
  vertex view depth clips operands and is reported as `render3d-csg-near-plane-clips-operands`.
  Fragments beyond the far plane are retained for the inference and are only filtered when chosen
  as the visible surface.
- **Scene bounds are conservative.** `Scene.ComputeBounds` includes every operand, even the
  subtracted or intersected-away parts, so `FitToBounds` frames everything and an image may show
  more margin than the result alone needs.
- **Epsilon scaling.** The base tie tolerance is `1e-6 * max(scene diameter, |largest depth|)`,
  the Phase 1 value, so it scales with the scene. Events closer than the tolerance anywhere (thin
  features smaller than it, or a grazing silhouette fold whose front and back surfaces lie within
  it) are treated as coincident. This is why forcing the fragment path onto a union-only scene can
  differ from the z-buffer in a couple of grazing silhouette samples.
- **Coincident and nearly coincident faces.** Two independently tessellated or placed meshes whose
  faces coincide mathematically rasterize from vertices rounded to 1/256 sample, so at oblique
  angles their interpolated depths differ by about the depth slope times that quantum, far above the
  base tolerance. For a fragment of a mesh instance that is not yet in a tie group the tolerance is
  therefore widened by `min((slopeAnchor + slopeCandidate) * (1/128 sample), 1000 * epsilon)`, where
  `slope` is the triangle's screen-space depth gradient (`ScreenTriangle.DepthSlope`). A fragment of
  an instance already in the group is never merged by this widening (it is a different event, such
  as the far side of a thin fold or wall) and uses the base tolerance only. Exactly coincident faces
  (identical vertex coordinates) give zero speckle, and nearly coincident faces from different
  transform chains are speckle-free in the tested views; along the edge-on silhouette of coincident
  walls a few isolated samples can remain (the stress test allows up to 12 in an 80 x 80 image over
  72 placements and views). Where exact faces can be avoided, give subtracted operands a small
  overlap, the standard practice in solid modeling.
- **Eye inside a solid.** The first exit along the ray is the visible surface and is shown with
  `BackFaceColor` shading, consistent with the z-buffer's rendering of back faces.

### Fragment Budget and Memory Model

Each tile on the constructive-solid-geometry path first counts the fragments of every binned
triangle, then fills exactly-sized storage. A tile whose total exceeds
`SceneRenderOptions.MaxFragmentsPerTile` (default 4 Mi, 4194304) throws
`RenderBudgetExceededException` as soon as the running count passes the budget; the count only
grows during the pass, so a tile exceeds the budget if and only if its complete triangle set does,
independent of triangle order. The exception's message and `MaxFragmentsPerTile` property depend
only on the budget, so the failure is identical whichever tile or thread detects it. A sequential
render throws it directly. In a parallel render `Parallel.For` wraps it in an `AggregateException`,
which the renderer unwraps: if cancellation was requested the result is an
`OperationCanceledException` (cancellation wins); otherwise one of the equal budget exceptions is
rethrown unchanged. An `AggregateException` never leaks, and the partially rendered surface is
disposed on every exception.

Each worker owns one fragment buffer, grown to exactly the need of a tile (8 bytes per fragment, a
`float` depth and an `int` triangle index, plus three `int` arrays per sample) and reused, never
above the budget. The default budget is about 32 MB per worker. Memory is independent of the image
size because a tile is at most 32 x 32 pixels times the supersampling factor squared samples. A
scene without a difference or intersection allocates no fragment storage and ignores the budget.

### Operand Validation Contract

For a scene on the natural constructive-solid-geometry path (never a union-only scene, and not the
internal forced-fragment-path test seam), when `UnsupportedFeatureHandler` is set or
`ThrowOnUnsupportedFeature` is true (otherwise no analysis is performed), the internal
`CsgOperandValidator` checks the meshes before `Render` does any transform or tile work:

- Instances are walked in scene pre-order and each `MeshNode` reference is analyzed and reported
  once, however many parents or paths share it; meshes with no triangles are skipped; the
  cancellation token is checked per instance.
- **Every** mesh instance of the scene is validated, not only the direct operands of a difference or
  intersection: all leaves use the signed counter, and an open mesh in a top-level union branch
  would vanish from the result.
- For each node the tokens are reported in the fixed order of the table below.
- In strict mode (`ThrowOnUnsupportedFeature`) the first problem throws
  `SceneRenderUnsupportedFeatureException(feature, message, node)` and the handler is not called.
  Otherwise the handler receives `SceneRenderUnsupportedFeature(feature, message, node)` for each
  report, on the calling thread, and rendering proceeds tolerantly: the output is deterministic and
  bounded, and visibly wrong where an operand is not a solid.
- Inversion is a property of the mesh data, independent of the instance's mirroring transform: the
  renderer reverses the winding of a mirrored instance, which restores the orientation the mesh data
  describes, so an outward mesh stays outward and an inward mesh stays inward under a mirror.

<!-- markdownlint-disable MD013 -->
| Feature token | Condition | Node |
| ------------- | --------- | ---- |
| `render3d-csg-open-mesh` | The mesh has a boundary edge | The mesh node |
| `render3d-csg-non-manifold-edge` | An edge is shared by three or more triangles | The mesh node |
| `render3d-csg-inconsistent-winding` | Neighboring triangles are wound in conflicting directions | The mesh node |
| `render3d-csg-degenerate-triangle` | The mesh has zero-area (repeated index) triangles | The mesh node |
| `render3d-csg-inverted-mesh` | A defect-free closed mesh has negative signed volume (`Mesh.IsInverted`) | The mesh node |
| `render3d-csg-near-plane-clips-operands` | The explicit `Camera.NearPlane` is greater than the smallest vertex view depth of the scene | None |
<!-- markdownlint-enable MD013 -->

The near-plane report is delivered once, after the vertices are transformed and still before any
tile work.

### Determinism for Constructive Solid Geometry

The visible surface of a sample is a pure function of the tile's fragment set: fragments are sorted
by a total order, tie groups and the choice within a group use no thread-dependent state, and tiles
write disjoint pixels. The output is therefore byte-identical across `MaxDegreeOfParallelism` (1,
2, and -1), repeated runs, and every supersampling factor.

### Pure-Managed Implementation

The rasterizer is implemented entirely in managed code on the BCL, with no native library, no
hardware intrinsics, and no unsafe code, so the library runs unchanged in any environment that
runs the target .NET runtimes.

## Implementation Phase Plan

`CanvasNetRender3D` is delivered incrementally; each phase is a separate pull request that adds
requirements, design, verification, and tests for its own scope.

1. **Phase 1 — Foundation (delivered).** Double-precision math, immutable meshes, the scene graph,
   primitive tessellation, the camera, ambient and directional lighting, the fixed-point tiled
   z-buffer rasterizer, supersampling antialiasing, deterministic tile parallelism, and cooperative
   cancellation.
2. **Phase 2 — Constructive solid geometry (delivered).** The `CsgNode` type in the closed
   `SceneNode` hierarchy, and a fragment-list resolver in the `SceneRendering` subsystem
   (`FragmentBuffer`, `CsgResolver`) integrated into `SceneRenderer` (union-only scenes keep the
   z-buffer path), with a per-tile fragment budget and operand reporting, relying on the
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
