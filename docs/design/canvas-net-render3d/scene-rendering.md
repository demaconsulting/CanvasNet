## SceneRendering

<!-- cspell:ignore rasterizer rasterization supersampling Lambertian Blinn Phong Binner -->

![SceneRendering Structure](SceneRenderingView.svg)

### Overview

The `SceneRendering` subsystem (namespace `DemaConsulting.CanvasNet.Render3D.SceneRendering`,
folder `src/DemaConsulting.CanvasNet.Render3D/SceneRendering/`) renders a `SceneModel` `Scene`
onto a new core `Surface`. It contains six units:

- **Camera** — `Camera`, `CameraProjection`, and the internal camera-relative `ViewTransform`;
  see _Camera Unit Design_ (`scene-rendering/camera.md`)
- **Lighting** — `Light`, `AmbientLight`, `DirectionalLight`, and the internal `LightingModel`;
  see _Lighting Unit Design_ (`scene-rendering/lighting.md`)
- **TriangleRasterizer** — the internal `TriangleRasterizer`, `TriangleClipper`, `ClipVertex`,
  `ClipPlane`, `SampleProjection`, `ViewTriangle`, `ScreenTriangle`, `TileRect`,
  `IFragmentVisitor`, and `DepthTestVisitor`; see _TriangleRasterizer Unit Design_
  (`scene-rendering/triangle-rasterizer.md`)
- **FragmentBuffer** — the internal per-worker `FragmentBuffer` that collects every fragment of a
  tile for the fragment-list CSG path; see _FragmentBuffer Unit Design_
  (`scene-rendering/fragment-buffer.md`)
- **CsgResolver** — the internal `CsgCompiler`, `CsgProgram`, and `CsgResolver` that compile the
  scene tree and resolve each sample's visible CSG surface; see _CsgResolver Unit Design_
  (`scene-rendering/csg-resolver.md`)
- **SceneRenderer** — the public `SceneRenderer`, `SceneRenderOptions`,
  `RenderBudgetExceededException`, `SceneRenderUnsupportedFeature`, and
  `SceneRenderUnsupportedFeatureException`, and the internal `TileBinner` (with `TileBins`),
  `SampleResolver`, `TileWorkspace`, and `CsgOperandValidator`; see _SceneRenderer Unit Design_
  (`scene-rendering/scene-renderer.md`)

The subsystem's namespace documentation is in `NamespaceDoc.cs`.

### Interfaces

**Exposed** (public): `Camera`, `CameraProjection`, `Light`, `AmbientLight`, `DirectionalLight`,
`SceneRenderOptions`, the static `SceneRenderer` (`Render`, `RenderFitted`), and the diagnostics
`RenderBudgetExceededException`, `SceneRenderUnsupportedFeature`, and
`SceneRenderUnsupportedFeatureException`. All other types are internal and visible only to the test
assembly through `InternalsVisibleTo`; among them the internal `CsgOperandValidator` and the
`SceneRenderer.RenderWithFragmentPath` test seam, which forces the fragment-list path onto a
union-only scene.

**Consumed**: the `SceneModel` subsystem (`Scene`, `Mesh`, `CsgNode`, `Vector3D`, `Matrix4x4D`,
`BoundingBox3D`), and the core `CanvasNet` system's `Surface` and `Rgba32`. The .NET base class
library supplies `System.Numerics.Vector3`, `Parallel.For`, and `CancellationToken`.

### Design

`SceneRenderer` is the orchestrator and the only unit with public entry points that render. For
one render it (1) builds a `ViewTransform` from the `Camera` (the `Camera` unit), (2) compiles the
scene tree with `CsgCompiler` to choose the path: a scene without a difference or intersection of
two or more operands has no program and uses the z-buffer path, any other scene uses the
fragment-list path, and for such a scene `CsgOperandValidator` reports unsuitable operands before
any rendering work, (3) flattens the `Scene` and transforms vertices to camera-relative view space,
(4) shades each triangle once with `LightingModel` (the `Lighting` unit; four shades on the
fragment-list path), (5) hands each shaded view-space triangle to `TriangleRasterizer.Setup` for
clipping, projection, and fixed-point conversion (the `TriangleRasterizer` unit), (6) bins the
prepared triangles into tiles with `TileBinner`, and (7) per tile either runs
`TriangleRasterizer.Rasterize` with a `DepthTestVisitor` (z-buffer path), or collects every
fragment of the tile with `FragmentBuffer` and resolves each sample with `CsgResolver`
(fragment-list path), then resolves the samples with `SampleResolver`. Steps 1 to 6 run on the
calling thread in deterministic order; step 7 runs tiles in parallel unless
`SceneRenderOptions.MaxDegreeOfParallelism` is 1.

The unit boundaries follow the data hand-offs: `Camera` and `Lighting` are independent of the
rasterizer; `TriangleRasterizer` knows nothing of scenes, cameras, or lights (it consumes a
`ViewTriangle` and a `SampleProjection`); and `SceneRenderer` owns the policy decisions (path
selection, clip range, depth tolerance, tiling, supersampling, parallelism, fragment budget,
operand reporting, cancellation).

Fixed-point integer coverage and independent tiles are the two properties that make the output
deterministic: coverage never depends on floating-point rounding, and tiles share no mutable state
beyond disjoint regions of the output `Surface`. The conventions and numeric rules are defined in
_CanvasNetRender3D System Design_ (`../canvas-net-render3d.md`), Design Constraints; the detailed
algorithms are in the unit designs.

**Fragment-list CSG.** Difference and intersection cannot be resolved by a depth test, so the
subsystem resolves them per sample from all of a sample's fragments: `CsgCompiler` turns the scene
tree into a `CsgProgram` over union-collapsed leaves (and reports no program for a union-only scene,
which keeps the z-buffer fast path and allocates no fragment storage); `FragmentBuffer` collects
every fragment of a tile through the same `IFragmentVisitor` rasterization in an exact-sized count
pass and fill pass, with a deterministic per-tile budget that `SceneRenderer` surfaces as
`RenderBudgetExceededException` (unwrapped from the `AggregateException` of a parallel render, with
cancellation taking precedence); and `CsgResolver` sorts a sample's fragments, infers the ray's
starting state, applies atomic tie groups (widened by the triangles' depth slope for independently
placed coincident faces), and writes the visible surface into the `TileWorkspace` in the same form
the z-buffer does, so the existing `SampleResolver`, tiling, and supersampling are reused
unchanged. `SceneRenderer` classifies the facing of mirrored operands with the winding correction
it already applies, and validates the meshes of a fragment-list scene (see _SceneRenderer Unit
Design_, Operand validation). `CsgCompiler`, `FragmentBuffer`, and `CsgResolver` know nothing of
scenes beyond the node tree, cameras, or lights; the `SceneRenderer` unit is their only caller.
