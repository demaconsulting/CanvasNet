### TriangleRasterizer

<!-- cspell:ignore rasterizer rasterization rasterized Sutherland Hodgman binner -->

The `TriangleRasterizer` unit is the third software unit in the `SceneRendering` subsystem. All of
its types are internal to the assembly (visible to the test assembly through `InternalsVisibleTo`):

- `TriangleRasterizer` (static class): triangle setup and fixed-point rasterization
- `TriangleClipper` (static class): homogeneous Sutherland-Hodgman polygon clipping
- `ClipVertex` (readonly record struct) and `ClipPlane` (readonly record struct): clipping
  primitives
- `SampleProjection` (readonly struct): view-space to sample-grid projection and the guard band
- `ViewTriangle` (readonly struct): a shaded view-space triangle awaiting setup
- `ScreenTriangle` (readonly struct): a prepared fixed-point triangle
- `TileRect` (readonly record struct): a half-open sample rectangle
- `IFragmentVisitor` (interface) and `DepthTestVisitor` (readonly struct): the per-sample callback
  contract and the z-buffer implementation

#### Purpose

`TriangleRasterizer` turns view-space triangles into covered samples with **exact, deterministic
coverage**: a shared edge between two triangles is covered exactly once, so a tiled or parallel
render has no gaps, no double hits, and no dependence on floating-point rounding. It knows nothing
of scenes, cameras, lights, or tiles beyond a sample rectangle to restrict to.

#### Data Model

- **`ViewTriangle`**: three `ClipVertex` positions in counter-clockwise-outward order, a draw
  `Priority` (the scene pre-order index), and the `FrontColor` and `BackColor` shades.
- **`ClipVertex(X, Y, Z)`**: a double-precision view-space vertex (promoted exactly from the
  single-precision narrowed position); `Depth` is `-Z`. `IsBefore` is the canonical lexicographic
  (X, Y, Z) order used to make clipped-edge intersections direction-independent.
- **`ClipPlane(A, B, C, D)`**: the half-space `A*X + B*Y + C*Z + D >= 0`. The near plane and the
  four guard-band planes are all linear in view-space coordinates (the homogeneous weight is `-Z`
  for perspective and 1 for orthographic), so a single representation serves every clip step.
- **`SampleProjection`**: orthographic flag, `ScaleX`/`ScaleY` (view to normalized device
  coordinates), half the sample-grid size, and the near distance. A view-space point maps to
  `sx = W/2 + (X * ScaleX / w) * W/2` and `sy = H/2 - (Y * ScaleY / w) * H/2` (y down), with `w`
  equal to the depth for perspective and 1 for orthographic. The **guard band** is 2^20 samples from
  the grid center.
- **`ScreenTriangle`**: six `int` 24.8 fixed-point coordinates in global sample space, `long Area2`
  (twice the signed area; always positive after normalization), three `float` view depths, a flag
  for perspective-correct depth, the `Priority`, a `BackFacing` flag, and the single `Color` of the
  visible side.
- **`TileRect`**: `[X0, X1) x [Y0, Y1)` in samples.

Fixed-point format: a value `v` samples is stored as `round(v * 256)`. Sample `(i, j)` has its center
at `(256 i + 128, 256 j + 128)` fixed units, which is exactly representable.

#### Key Methods

##### Setup (`TriangleRasterizer.Setup`)

For one `ViewTriangle` and a `SampleProjection`, appends zero or more `ScreenTriangle`s to an
output list:

1. A triangle with any non-finite vertex component is skipped.
2. **Near-plane clip.** The polygon is clipped against the near plane (`TriangleClipper.NearPlane`)
   always, which guarantees positive depth for the projection that follows. Fewer than three
   remaining vertices means nothing is output.
3. **Guard-band clip.** Only when some clipped vertex projects outside the guard band, the polygon is
   clipped successively against the four guard-band side planes (+X, -X, +Y, -Y). This bounds all
   fixed-point magnitudes below about 2^29 so edge-function products (below about 2^59) always fit
   in a signed 64-bit integer.
4. **Fan triangulation** of the convex clipped polygon, and for each resulting triangle
   (`Emit`): project each vertex with `SampleProjection.ToSample` in double precision, round to
   fixed point (a non-finite value or a magnitude above 2^30 rejects the triangle), and compute
   `area2 = (x1 - x0)(y2 - y0) - (y1 - y0)(x2 - x0)` in 64 bits. A zero area discards the triangle.
5. **Winding.** On a y-down screen a counter-clockwise-outward triangle seen from the front has a
   **negative** raw `area2` and is front-facing; a positive raw area means the viewer sees the back.
   `Emit` reorders two vertices (and their depths) of a front-facing triangle so that `Area2` is
   positive in every stored triangle, records `BackFacing`, and selects `FrontColor` or `BackColor`.

##### Clipping (`TriangleClipper.Clip`)

Sutherland-Hodgman against one plane: keeps inside vertices (distance at least zero) and adds the
crossing point wherever an edge changes sides; fewer than three output vertices yields zero. A
convex polygon stays convex and gains at most one vertex per plane. The crossing point is computed
with the edge's endpoints **first put in canonical lexicographic (X, Y, Z) order**, so clipping
edge `(a, b)` and edge `(b, a)` give the bit-identical point. Two triangles that share an edge
therefore share the identical clipped end point and no crack opens along a clipped edge.

##### Rasterization (`TriangleRasterizer.Rasterize<TVisitor>`)

Generic over a struct `IFragmentVisitor` so the per-sample call is inlined:

1. `GetSampleBounds` gives the inclusive range of sample centers inside the triangle's fixed-point
   bounding box (using floor and ceiling integer division); the range is intersected with the tile
   rectangle. The tile binner uses the same function so both agree exactly on which samples a
   triangle can touch.
2. Edge `k` runs from vertex `k` to vertex `k+1`; its edge function
   `E_k(p) = (bx - ax)(py - ay) - (by - ay)(px - ax)` is positive inside the normalized (positive
   area) triangle. The edge functions are evaluated once at the first sample center of each row
   and then advanced by constant integer steps per sample and per row; no re-evaluation and no
   floating point is used for coverage.
3. **Top-left rule.** An edge `(dx, dy)` owns its boundary samples when `dy < 0`, or `dy == 0`
   and `dx > 0`. Adding 1 to the edge function of an owning edge turns "positive, or zero on an
   owned edge" into "strictly positive". A neighbor sees the shared edge reversed, so exactly one
   of the two triangles covers each boundary sample.
4. For each covered sample the depth is interpolated in double precision from the barycentric
   weights `E / Area2` (bias removed): perspective-correct as `1 / sum(lambda_k / d_k)`, or linear
   `sum(lambda_k d_k)` for an orthographic projection, narrowed to `float`, and passed to
   `visitor.Visit(x, y, depth)`.

##### Depth test (`DepthTestVisitor.Visit`)

Holds the worker's `TileWorkspace` and one triangle's attributes; discards fragments deeper than
the far distance, then applies the tie rule with tolerance `eps`:

- depth less than `stored - eps`: the fragment wins (an untouched sample has stored depth positive
  infinity);
- depth greater than `stored + eps`: discarded;
- otherwise (**coincident**): a front-facing fragment beats a back-facing one; between fragments of
  the same facing, the lower `Priority` (earlier in scene pre-order) wins; otherwise the earlier
  rasterized fragment stays. The stored depth becomes the minimum of the two even when the new
  fragment does not win.

The winner's metadata (`(priority << 1) | backFacing`), depth, and color are stored per sample. The
rule is not a strict weak order, but triangles are binned and rasterized in ascending index order
and tiles are independent, so the outcome is deterministic.

#### Error Handling

The unit performs no argument validation (all inputs are produced by `SceneRenderer`) and throws no
exceptions. Numeric failure is handled by skipping: non-finite or out-of-fixed-point-range geometry
and zero-area triangles are dropped during `Setup`; all coverage arithmetic uses 64-bit integers
that the guard band keeps from overflowing.

#### Dependencies

The `SceneRenderer` unit's internal `TileWorkspace` (the depth-test visitor writes its per-sample
depth, metadata, and color buffers), and the core `CanvasNet` system's `Canvas` subsystem (`Rgba32`,
carried as the precomputed front and back shades of a `ViewTriangle`). Shading itself is not
performed here: the `Lighting` unit's output arrives as ready-made colors. The unit uses no
`SceneModel` type and otherwise only the .NET base class library.

#### Callers

`SceneRenderer` calls `Setup` once per triangle while preparing the scene and `Rasterize`, with a
`DepthTestVisitor`, once per binned triangle per tile; `TileBinner` calls `GetSampleBounds`. The
unit tests also call `Rasterize` with a coverage-counting visitor.
