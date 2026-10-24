### SceneRenderer

<!-- cspell:ignore Binner binner supersampling supersamples rasterizer rasterize -->

The `SceneRenderer` unit is the fourth software unit in the `SceneRendering` subsystem. It
comprises five public types in the `DemaConsulting.CanvasNet.Render3D.SceneRendering` namespace plus
three internal types:

- `SceneRenderer` (static class): the public `Render` and `RenderFitted` entry points
- `SceneRenderOptions` (sealed class): immutable, validated render options with a `Default`
  instance
- `RenderBudgetExceededException`, `SceneRenderUnsupportedFeatureException` (sealed exception
  classes) and `SceneRenderUnsupportedFeature` (sealed record): the diagnostics of fragment-list
  constructive solid geometry rendering
- `TileBinner` (internal static class) and `TileBins` (internal sealed class): triangle-to-tile
  binning
- `SampleResolver` (internal static class): antialiasing resolve into output pixels
- `TileWorkspace` (internal sealed class): per-worker sample buffers

#### Purpose

`SceneRenderer` is the orchestrator of the rendering pipeline and the library's main entry point.
It validates the request, prepares all triangles, and drives a tiled, depth-tested, antialiased
render into a new `Surface`, in parallel when allowed, with cooperative cancellation.

#### Data Model

- **`SceneRenderOptions`** (every property init-only and validated **when initialized**, so an
  instance is always valid and `Render` need not re-check it):

  <!-- markdownlint-disable MD013 -->
  | Property | Default | Validation |
  | -------- | ------- | ---------- |
  | `BackgroundColor` | opaque white | none; alpha honored |
  | `DefaultColor` | `(180, 180, 180)` | none; alpha ignored |
  | `BackFaceColor` | `(200, 70, 70)` | none; alpha ignored |
  | `Lights` | `null` | list copied; a null element throws `ArgumentException`; `null` selects `Light.DefaultRig`; an empty list is valid (unlit black) |
  | `SpecularStrength` | 0.10 | finite, 0 to 1 |
  | `SpecularPower` | 32 | finite, greater than 0 |
  | `Supersampling` | 3 | 1 to 4 |
  | `MaxDegreeOfParallelism` | -1 | -1 (scheduler chooses) or at least 1; 1 means sequential; 0 and below -1 rejected |
  | `MaxFragmentsPerTile` | 4 Mi (4194304) | `long`, 1 to `Array.MaxLength`; the per-tile fragment budget of per-pixel fragment resolution |
  | `UnsupportedFeatureHandler` | `null` | none; receives `SceneRenderUnsupportedFeature` reports |
  | `ThrowOnUnsupportedFeature` | `false` | none; when `true` the first report is thrown instead |
  <!-- markdownlint-enable MD013 -->

  Violations of a numeric range throw `ArgumentOutOfRangeException`. `SceneRenderOptions.Default`
  is a shared instance of the defaults.
- **Diagnostics**: `RenderBudgetExceededException` (an `InvalidOperationException`) carries only the
  exceeded `long MaxFragmentsPerTile` and a message that depends on nothing else, so the failure is
  identical whichever tile or thread detects it. `SceneRenderUnsupportedFeature(Feature, Message,
  Node)` is the record delivered to the handler: `Feature` is a short, stable, machine-matchable
  token, `Node` the optional `MeshNode` concerned. `SceneRenderUnsupportedFeatureException` (an
  `InvalidOperationException`) carries the same `Feature` and `Node`; it mirrors the
  unsupported-feature exceptions of the sibling packages (tolerant by default, strict opt-in).
  Both exceptions provide the standard parameterless, message and message-plus-inner constructors.
  The options and diagnostics types are defined ahead of the renderer integration that raises them.
- **`TileBins`**: a flat `int[]` of triangle indices plus a per-tile offsets array; `GetTriangles(tile)`
  returns the tile's slice.
- **`TileWorkspace`**: three arrays sized for one tile at the chosen supersampling (`float` depth,
  `int` winner metadata, `Rgba32` color) and the tile's origin and size; `Begin` fills the depth
  with positive infinity (an untouched sample).
- **Constants**: tile size 32 x 32 output pixels; a cancellation check every 4096 triangles; depth
  tolerance factor 1e-6; automatic near floor factor 1e-4; near margin 0.99 and far margin 1.01.

#### Key Methods

##### `Render(scene, camera, width, height, options, cancellationToken)`

1. **Validate**: null scene or camera throws `ArgumentNullException`; each dimension must be 1 to
   `Surface.MaxDimension` (`ArgumentOutOfRangeException`); a null `options` becomes `Default`; the
   token is checked. Building the `ViewTransform` runs `Camera.Validate()`, which throws
   `ArgumentException` for eye equal to target, a zero up vector, an orthographic camera without a
   height, or near not below far.
2. **Flatten and transform**: for each `Scene.EnumerateInstances()` instance, compose
   `world * translate(-eye) * rotation` and transform every vertex in double precision into a
   per-instance view-space array (so shared vertices produce bit-identical positions in every
   incident triangle), tracking the component-wise view-space minimum and maximum. The token is
   checked per instance.
3. **Depth statistics**: nearest depth `-max.Z`, farthest depth `-min.Z`, and the diameter of the
   view-space bounding box.
4. **Clip range**: a `NearPlane`/`FarPlane` set on the camera wins. Otherwise, for a perspective
   camera, `near = nearestDepth * 0.99` when the nearest depth exceeds the floor
   `1e-4 * max(farthestDepth, diameter)`, else the floor (so geometry crossing the eye is clipped
   rather than dividing by zero); for an orthographic camera
   `near = nearestDepth - 0.01 * max(depth range, diameter)`. `far = farthestDepth * 1.01` (or
   `farthestDepth + 0.01 * diameter` when not positive). An empty scene uses 1 and 2.
5. **Triangle preparation** (calling thread, deterministic order; the token is checked every 4096
   triangles): for each instance triangle, swap two indices when the instance's world determinant
   is negative; compute the face normal from the double-precision view-space vertices (a zero-area
   or non-finite normal skips the triangle); shade front and back once with `LightingModel`; narrow
   the vertices to single precision; call `TriangleRasterizer.Setup`. The depth tolerance is
   `eps = 1e-6 * max(diameter, farthestDepth)`.
6. **Binning**: `TileBinner.Bin` counts per-tile triangles in a first pass, prefix-sums the counts
   into offsets, and fills a flat index array in a second pass, so each tile's list is in ascending
   triangle order. A triangle is binned to every tile its sample bounding box overlaps; bounds that
   miss the grid entirely are skipped and partly off-grid bounds are clamped.
7. **Tiles**: a `Surface` is created, then tiles run sequentially on the calling thread when
   `MaxDegreeOfParallelism` is 1 (one workspace), otherwise with `Parallel.For` over the tile index
   with `ParallelOptions` carrying the token and the degree, and one `TileWorkspace` per worker
   created by `localInit`. Per tile: compute the output pixel rectangle (partial tiles at the right
   and bottom edges are allowed) and the sample rectangle (`k` times larger), call
   `TileWorkspace.Begin`, rasterize every binned triangle with a `DepthTestVisitor`, then call
   `SampleResolver.Resolve`. The token is checked at the start of each tile.
8. **Failure handling**: any exception during tile rendering (cancellation included) disposes the
   `Surface` before it propagates. Because `Parallel.For` can surface cancellation as an
   `AggregateException`, an aggregate whose inner exceptions are all `OperationCanceledException` is
   converted to a plain `OperationCanceledException` carrying the token.

##### `SampleResolver.Resolve`

For each output pixel the `k` x `k` samples are summed in **premultiplied** integer arithmetic. An
untouched sample (infinite depth) contributes the background color. With `n = k * k`, `sumA` the sum
of alphas, and `sumC` the sum of `channel * alpha`: when `sumA` is zero the pixel is `(0, 0, 0, 0)`;
otherwise each color channel is `(sumC + sumA / 2) / sumA` and alpha is `(sumA + n / 2) / n`. For
opaque input this reduces to the rounded average of the sample colors, and a half-covered pixel over a
fully transparent background keeps the surface color at half alpha. Each pixel is written through
`Surface.GetRowSpan`; tiles write disjoint pixel ranges, so concurrent tiles never touch the same
memory.

##### `RenderFitted(scene, width, height, options, cancellationToken)`

Validates the scene and size, honors the cancellation token (before any bounds work), builds `Camera.FitToBounds(scene.ComputeBounds(),
Camera.DefaultAzimuthDegrees, Camera.DefaultElevationDegrees, width / (double)height)`, and calls
`Render`. An empty scene yields a fallback unit-cube framing and therefore the background only.

#### Error Handling

Argument errors are thrown before any work (see step 1); an invalid camera is detected before
triangle preparation. Cancellation throws `OperationCanceledException` (never wrapped in an
`AggregateException`) and disposes any partially rendered surface. Non-finite or degenerate
triangles are skipped silently (see _TriangleRasterizer Unit Design_). Exceptions are otherwise
propagated; the only local handling is the surface disposal and the aggregate-cancellation
conversion described above.

#### Dependencies

The `Camera` unit (`Camera`, `ViewTransform`), the `Lighting` unit (`LightingModel`, `Light`), the
`TriangleRasterizer` unit (`TriangleRasterizer`, `SampleProjection`, `ScreenTriangle`,
`ViewTriangle`, `DepthTestVisitor`, `TileRect`), the `SceneGraph` and `DoublePrecisionMath` units
(`Scene`, `SceneInstance`, `Vector3D`, `Matrix4x4D`), the core `CanvasNet` system's `Canvas`
subsystem (`Surface`, `Rgba32`), and the .NET base class library (`System.Threading.Tasks.Parallel`,
`System.Threading.CancellationToken`, `System.Runtime.InteropServices.CollectionsMarshal`).

#### Callers

`SceneRenderer` and `SceneRenderOptions` are public API entry points invoked directly by consumers
of the `DemaConsulting.CanvasNet.Render3D` package; a future format front end would call them. No
other unit calls `SceneRenderer`.
