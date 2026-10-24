### SceneRenderer

<!-- cspell:ignore Binner binner supersampling supersamples rasterizer rasterize subtrahend -->
<!-- cspell:ignore minuend -->

The `SceneRenderer` unit is the fourth software unit in the `SceneRendering` subsystem (after
`Camera`, `Lighting`, and `TriangleRasterizer`, and together with `FragmentBuffer` and
`CsgResolver`, which it drives). It comprises five public types in the
`DemaConsulting.CanvasNet.Render3D.SceneRendering` namespace plus the internal types:

- `SceneRenderer` (static class): the public `Render` and `RenderFitted` entry points, and the
  internal `RenderWithFragmentPath` test seam
- `SceneRenderOptions` (sealed class): immutable, validated render options with a `Default`
  instance
- `RenderBudgetExceededException`, `SceneRenderUnsupportedFeatureException` (sealed exception
  classes) and `SceneRenderUnsupportedFeature` (sealed record): the diagnostics of fragment-list
  constructive solid geometry rendering
- `CsgOperandValidator` (internal static class): the closed-operand checks and their reports
- `TileBinner` (internal static class) and `TileBins` (internal sealed class): triangle-to-tile
  binning
- `SampleResolver` (internal static class): antialiasing resolve into output pixels
- `TileWorkspace` (internal sealed class): per-worker sample buffers
- `TileWorker` (private sealed class of `SceneRenderer`): the per-worker bundle of a
  `TileWorkspace` and, on the fragment-list path, a `FragmentBuffer` and a `CsgResolver`

#### Purpose

`SceneRenderer` is the orchestrator of the rendering pipeline and the library's main entry point.
It validates the request, selects the z-buffer path or the fragment-list constructive solid
geometry path, reports unsuitable CSG operands, prepares all triangles, and drives a tiled,
antialiased render into a new `Surface`, in parallel when allowed, with cooperative cancellation
and a deterministic per-tile fragment budget.

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
- **`TileBins`**: a flat `int[]` of triangle indices plus a per-tile offsets array; `GetTriangles(tile)`
  returns the tile's slice.
- **`TileWorkspace`**: three arrays sized for one tile at the chosen supersampling (`float` depth,
  `int` winner metadata, `Rgba32` color) and the tile's origin and size; `Begin` fills the depth
  with positive infinity (an untouched sample).
- **`TileWorker`**: one per worker (one in sequential mode, one per `Parallel.For` worker
  otherwise, created by `localInit`): the `TileWorkspace`, and when the scene has a CSG program a
  `FragmentBuffer` constructed with `MaxFragmentsPerTile` and a `CsgResolver` over the shared
  immutable `CsgProgram`. Workers share no mutable state.
- **`CsgOperandValidator` feature tokens**: `render3d-csg-open-mesh`,
  `render3d-csg-non-manifold-edge`, `render3d-csg-inconsistent-winding`,
  `render3d-csg-degenerate-triangle`, `render3d-csg-inverted-mesh`,
  `render3d-csg-singular-transform`; the report
  table is in _CanvasNetRender3D System Design_, Operand Validation Contract.
- **Constants**: tile size 32 x 32 output pixels; a cancellation check every 4096 triangles (also
  while collecting fragments); depth tolerance factor 1e-6; automatic near floor factor 1e-4; near
  margin 0.99 and far margin 1.01.

#### Key Methods

##### `Render(scene, camera, width, height, options, cancellationToken)`

1. **Validate**: null scene or camera throws `ArgumentNullException`; each dimension must be 1 to
   `Surface.MaxDimension` (`ArgumentOutOfRangeException`); a null `options` becomes `Default`; the
   token is checked. Building the `ViewTransform` runs `Camera.Validate()`, which throws
   `ArgumentException` for eye equal to target, a zero up vector, an orthographic camera without a
   height, or near not below far.
2. **Select the path.** `CsgCompiler.Compile(scene.Root)` yields the CSG program and the leaf of
   each instance. No program (a union-only scene: meshes, groups, union `CsgNode`s, and
   single-operand booleans) means the z-buffer path: output is unchanged, no `FragmentBuffer` or
   `CsgResolver` is allocated, `MaxFragmentsPerTile` is ignored, and operands are never validated.
   Any other scene takes the CSG path. The internal `RenderWithFragmentPath` seam shares the
   implementation but forces a union-only scene onto the fragment-list path with the one-leaf
   program `L0` (so tests can prove equivalence with the z-buffer); the seam never validates
   operands.
3. **Validate operands** (natural CSG path only): `CsgOperandValidator.Validate` runs before any
   transform or rendering work, as described under _Operand validation_ below.
4. **Flatten and transform**: for each `Scene.EnumerateInstances()` instance, compose
   `world * translate(-eye) * rotation` and transform every vertex in double precision into a
   per-instance view-space array (so shared vertices produce bit-identical positions in every
   incident triangle), tracking the component-wise view-space minimum and maximum. The token is
   checked per instance. On the CSG path, `CsgOperandValidator.ValidateTransformed` then checks
   every instance's world transform and view-space triangles (only when validation is enabled; see
   _Operand validation_). The camera's near plane is not validated.
5. **Depth statistics**: nearest depth `-max.Z`, farthest depth `-min.Z`, and the diameter of the
   view-space bounding box, over **all** instances (operands included).
6. **Clip range**: a `NearPlane`/`FarPlane` set on the camera wins. Otherwise, for a perspective
   camera, `near = nearestDepth * 0.99` when the nearest depth exceeds the floor
   `1e-4 * max(farthestDepth, diameter)`, else the floor (so geometry crossing the eye is clipped
   rather than dividing by zero); for an orthographic camera
   `near = nearestDepth - 0.01 * max(depth range, diameter)`. `far = farthestDepth * 1.01` (or
   `farthestDepth + 0.01 * diameter` when not positive). An empty scene uses 1 and 2. Because the
   statistics cover every instance, no operand is clipped by the automatic planes unless the eye is
   inside or behind geometry.
7. **Triangle preparation** (calling thread, deterministic order; the token is checked every 4096
   triangles): for each instance triangle, swap two indices when the instance's world determinant
   is negative (before the screen-winding classification, so `ScreenTriangle.BackFacing`, which
   gives a leaf's +1 or -1 crossing sign, is the corrected facing for CSG as well); compute the face
   normal from the double-precision view-space vertices (a zero-area or non-finite normal skips the
   triangle, the same `MeshTopology.TryGetFaceNormal` criterion operand validation reports as a
   degenerate triangle); shade with `LightingModel`; narrow the vertices to single precision (for a
   CSG leaf, also compute the supporting plane of the narrowed triangle: unit normal and centroid,
   used by `CsgResolver` to tell coincident faces from separate ones); call
   `TriangleRasterizer.Setup`. The z-buffer path shades the front and back once. When the instance
   has a CSG leaf, `PrepareTriangle` computes four shades with `n` the face normal:
   `FrontEnter = Shade(n, leaf color)`, `FrontExit = Shade(n, BackFaceColor)`,
   `BackEnter = Shade(-n, leaf color)` (the cut surface of a subtrahend: its own color lit from the
   flipped normal), and `BackExit = ShadeBackFace(n) = Shade(-n, BackFaceColor)`; the rasterizer
   picks the pair by screen facing. The depth tolerance is
   `eps = 1e-6 * max(diameter, farthestDepth)`.
8. **Binning**: `TileBinner.Bin` counts per-tile triangles in a first pass, prefix-sums the counts
   into offsets, and fills a flat index array in a second pass, so each tile's list is in ascending
   triangle order. A triangle is binned to every tile its sample bounding box overlaps; bounds that
   miss the grid entirely are skipped and partly off-grid bounds are clamped.
9. **Tiles**: a `Surface` is created, then tiles run sequentially on the calling thread when
   `MaxDegreeOfParallelism` is 1 (one `TileWorker`), otherwise with `Parallel.For` over the tile
   index with `ParallelOptions` carrying the token and the degree, and one `TileWorker` per worker
   created by `localInit`. See `RenderTile` below. The token is checked at the start of each tile.
10. **Failure handling**: any exception during tile rendering disposes the `Surface` before it
    propagates. `RenderTiles` normalizes the exceptions of a parallel render, see _Budget and
    cancellation unwrap_ below.

##### `RenderTile`

Computes the tile's output pixel rectangle (partial tiles at the right and bottom edges are
allowed) and sample rectangle (`k` times larger) and calls `TileWorkspace.Begin`.

- **Z-buffer path** (no program): rasterize every binned triangle, in ascending order, with a
  `DepthTestVisitor`, then `SampleResolver.Resolve`.
- **CSG path**: `ResolveTileWithFragments`, then the unchanged `SampleResolver.Resolve`:
  1. `FragmentBuffer.Begin` for the tile's sample rectangle.
  2. **Count pass**: `CountTriangle` for every binned triangle in ascending order; the budget is
     checked after every triangle, and the token every 4096 triangles.
  3. `EndCounting`, then the **fill pass**: `AddTriangle` for the same triangles in the same order,
     with the same cancellation cadence.
  4. `CsgResolver.ResolveTile`: per sample, sort the fragments by (depth, triangle), infer each
     leaf's initial state from the whole ray, sweep the tie groups, and take the first group that
     flips the root as the visible surface; it writes depth, color, and metadata into the
     `TileWorkspace` exactly as the z-buffer does and checks the token once per sample row.

The token is therefore observed at the start of every tile, every 4096 triangles of both passes,
and once per sample row of the resolve.

##### Budget and cancellation unwrap (`RenderTiles`)

A tile whose fragment total exceeds `MaxFragmentsPerTile` makes `FragmentBuffer` throw
`RenderBudgetExceededException`, whose message and property depend only on the budget. In
sequential mode it propagates directly. In parallel mode `Parallel.For` wraps it (and cancellations)
in an `AggregateException`, which `RenderTiles` unwraps so that an `AggregateException` never
leaks:

1. If every inner exception is an `OperationCanceledException`, throw
   `OperationCanceledException(token)`.
2. Otherwise, if any inner exception (after `Flatten`) is a `RenderBudgetExceededException`: if
   cancellation was requested, throw `OperationCanceledException` (cancellation wins); else rethrow
   one of them unchanged with `ExceptionDispatchInfo` (all are equal in content, so which one is
   irrelevant).
3. Any other aggregate propagates as is.

##### Operand validation (`CsgOperandValidator`)

`Validate(scene, options, token)` runs only for natural CSG-path scenes and only when
`UnsupportedFeatureHandler` is set or `ThrowOnUnsupportedFeature` is true (otherwise it returns
immediately: no analysis cost). It walks `scene.EnumerateInstances()` in pre-order, skips an
already analyzed `MeshNode` (deduplication by reference, so a node shared by several parents or
paths is analyzed and reported once), checks the token per instance, skips meshes with no triangles,
and for each remaining node reports, in this fixed order, `render3d-csg-open-mesh` (boundary edge),
`render3d-csg-non-manifold-edge`, `render3d-csg-inconsistent-winding`,
`render3d-csg-degenerate-triangle` (a repeated index or coincident or collinear vertices, as
`Mesh.OperandDefects` reports, evaluated on position-welded indices), and
`render3d-csg-inverted-mesh` (a defect-free closed mesh in which a connected shell's signed volume
disagrees with its nesting depth parity, `Mesh.IsInverted`). Every mesh instance
of the scene is validated, not
only the direct operands of a difference or intersection: all leaves use the signed counter, so an
open mesh in a top-level union branch would also vanish. Inversion is a property of the mesh and
independent of the instance's mirroring transform, because the renderer reverses the winding of a
mirrored instance, which restores the orientation the mesh data describes.

`ValidateTransformed(options, instance, viewVertices)` is the transform-dependent counterpart,
called by `Render` for every instance after `TransformInstances` (same gating, cancellation checked
per instance, reports after all mesh-level reports). A world transform with a zero or non-finite
determinant reports `render3d-csg-singular-transform` for the instance's node and nothing more for
it. Otherwise the instance's triangles are tested on the same double-precision view-space vertices
with `MeshTopology.TryGetFaceNormal`, exactly as `PrepareTriangle` discards them, and the first
failing triangle yields one `render3d-csg-degenerate-triangle` report for the instance (skipped when
the mesh itself already carries that defect and was reported per node). The cost is one face-normal
evaluation per operand triangle, paid only when a handler or strict mode is set.

Delivery: in strict mode the first problem throws
`SceneRenderUnsupportedFeatureException(feature, message, node)` and the handler is not called;
otherwise the handler is invoked with `SceneRenderUnsupportedFeature(feature, message, node)` per
report on the calling thread and rendering proceeds tolerantly (deterministic, bounded, and visibly
wrong where an operand is not a solid).

The camera's near plane is deliberately not validated: the resolver infers the ray's initial state
from the fragments that remain after near clipping, so an explicit `NearPlane` (including a camera
inside an operand) yields the CSG solid clipped to the near plane of the view frustum, with no cap
surfaces drawn on that plane.

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
triangle preparation. The exception contract of `Render` beyond argument errors:

- `OperationCanceledException` (never wrapped in an `AggregateException`) on cancellation, with any
  partially rendered surface disposed. Cancellation takes precedence over a budget failure.
- `RenderBudgetExceededException` when a CSG tile needs more fragments than `MaxFragmentsPerTile`;
  identical content for sequential and parallel rendering, never wrapped, surface disposed.
- `SceneRenderUnsupportedFeatureException` in strict mode for the first unsuitable operand, thrown
  before any rendering work so no surface exists yet.

Without strict mode, operand problems are delivered to the handler and are not errors. A handler
that throws propagates unchanged, and one that cancels the token stops the validation with
`OperationCanceledException` at the next instance. Non-finite or degenerate triangles are skipped
silently (see _TriangleRasterizer Unit Design_). Exceptions are otherwise propagated; the only
local handling is the surface disposal and the aggregate unwrapping described above.

#### Dependencies

The `Camera` unit (`Camera`, `ViewTransform`), the `Lighting` unit (`LightingModel`, `Light`), the
`TriangleRasterizer` unit (`TriangleRasterizer`, `SampleProjection`, `ScreenTriangle`,
`ViewTriangle`, `DepthTestVisitor`, `TileRect`), the `FragmentBuffer` unit (`FragmentBuffer`), the
`CsgResolver` unit (`CsgCompiler`, `CsgProgram`, `CsgResolver`), the `SceneGraph`, `Mesh`, and
`DoublePrecisionMath` units (`Scene`, `SceneInstance`, `MeshNode`, `Mesh` topology defects and
`IsInverted`, `Vector3D`, `Matrix4x4D`), the core `CanvasNet` system's `Canvas` subsystem
(`Surface`, `Rgba32`), and the .NET base class library (`System.Threading.Tasks.Parallel`,
`System.Threading.CancellationToken`, `System.Runtime.ExceptionServices.ExceptionDispatchInfo`,
`System.Runtime.InteropServices.CollectionsMarshal`).

#### Callers

`SceneRenderer` and `SceneRenderOptions` are public API entry points invoked directly by consumers
of the `DemaConsulting.CanvasNet.Render3D` package; a future format front end would call them. No
other unit calls `SceneRenderer`. `CsgOperandValidator` is called only by `SceneRenderer` (and by
the unit tests), and `RenderWithFragmentPath` only by the unit tests.
