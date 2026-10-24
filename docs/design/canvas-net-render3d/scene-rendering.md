## SceneRendering

<!-- cspell:ignore rasterizer rasterization supersampling Lambertian Blinn Phong Binner -->

![SceneRendering Structure](SceneRenderingView.svg)

### Overview

The `SceneRendering` subsystem (namespace `DemaConsulting.CanvasNet.Render3D.SceneRendering`,
folder `src/DemaConsulting.CanvasNet.Render3D/SceneRendering/`) renders a `SceneModel` `Scene`
onto a new core `Surface`. It contains four units:

- **Camera** — `Camera`, `CameraProjection`, and the internal camera-relative `ViewTransform`;
  see _Camera Unit Design_ (`scene-rendering/camera.md`)
- **Lighting** — `Light`, `AmbientLight`, `DirectionalLight`, and the internal `LightingModel`;
  see _Lighting Unit Design_ (`scene-rendering/lighting.md`)
- **TriangleRasterizer** — the internal `TriangleRasterizer`, `TriangleClipper`, `ClipVertex`,
  `ClipPlane`, `SampleProjection`, `ViewTriangle`, `ScreenTriangle`, `TileRect`,
  `IFragmentVisitor`, and `DepthTestVisitor`; see _TriangleRasterizer Unit Design_
  (`scene-rendering/triangle-rasterizer.md`)
- **SceneRenderer** — the public `SceneRenderer` and `SceneRenderOptions`, and the internal
  `TileBinner` (with `TileBins`), `SampleResolver`, and `TileWorkspace`; see _SceneRenderer Unit
  Design_ (`scene-rendering/scene-renderer.md`)

The subsystem's namespace documentation is in `NamespaceDoc.cs`.

### Interfaces

**Exposed** (public): `Camera`, `CameraProjection`, `Light`, `AmbientLight`, `DirectionalLight`,
`SceneRenderOptions`, and the static `SceneRenderer` (`Render`, `RenderFitted`). All other types
are internal and visible only to the test assembly through `InternalsVisibleTo`.

**Consumed**: the `SceneModel` subsystem (`Scene`, `Mesh`, `Vector3D`, `Matrix4x4D`,
`BoundingBox3D`), and the core `CanvasNet` system's `Surface` and `Rgba32`. The .NET base class
library supplies `System.Numerics.Vector3`, `Parallel.For`, and `CancellationToken`.

### Design

`SceneRenderer` is the orchestrator and the only unit with public entry points that render. For
one render it (1) builds a `ViewTransform` from the `Camera` (the `Camera` unit), (2) flattens the
`Scene` and transforms vertices to camera-relative view space, (3) shades each triangle once with
`LightingModel` (the `Lighting` unit), (4) hands each shaded view-space triangle to
`TriangleRasterizer.Setup` for clipping, projection, and fixed-point conversion (the
`TriangleRasterizer` unit), (5) bins the prepared triangles into tiles with `TileBinner`, and
(6) per tile runs `TriangleRasterizer.Rasterize` with a `DepthTestVisitor` and resolves the samples
with `SampleResolver`. Steps 1 to 5 run on the calling thread in deterministic order; step 6 runs
tiles in parallel unless `SceneRenderOptions.MaxDegreeOfParallelism` is 1.

The unit boundaries follow the data hand-offs: `Camera` and `Lighting` are independent of the
rasterizer; `TriangleRasterizer` knows nothing of scenes, cameras, or lights (it consumes a
`ViewTriangle` and a `SampleProjection`); and `SceneRenderer` owns the policy decisions (clip
range, depth tolerance, tiling, supersampling, parallelism, cancellation).

Fixed-point integer coverage and independent tiles are the two properties that make the output
deterministic: coverage never depends on floating-point rounding, and tiles share no mutable state
beyond disjoint regions of the output `Surface`. The conventions and numeric rules are defined in
_CanvasNetRender3D System Design_ (`../canvas-net-render3d.md`), Design Constraints; the detailed
algorithms are in the unit designs.
