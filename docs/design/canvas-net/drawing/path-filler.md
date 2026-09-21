## PathFiller

![Drawing Structure](DrawingView.svg)

The `PathFiller` class is the sole software unit in the `Drawing` subsystem. It provides a
single public entry point, `Fill(Surface, Path, Rgba32, FillRule, float)`, that rasterizes a
closed `Geometry.Path` onto a `Canvas.Surface` with a solid color, using an antialiased
signed-area/coverage-accumulation scanline algorithm. The supporting `FillRule` enum and the
internal `EdgeFlattener`/`ScanlineRasterizer` helpers are documented inline here, because none of
them has any independent behavior beyond supporting `PathFiller.Fill`.

### Purpose

`PathFiller` turns vector path geometry into rendered pixels: it flattens every subpath's lines,
curves, and arcs into a closed polygon, then rasterizes those polygons with analytically computed
antialiased pixel coverage, compositing the result directly onto a `Surface` using its existing
Porter-Duff "over" blend pipeline. It supports both `FillRule.NonZero` and `FillRule.EvenOdd`
winding resolution (matching SVG/CSS `fill-rule` semantics), correctly renders holes via nested,
counter-wound subpaths, and treats every subpath as implicitly closed regardless of whether the
path explicitly called `Close`. Strokes, gradients, and fonts are out of scope for this unit and
reserved for later phases.

### Coordinate Convention

**This is the first unit in CanvasNet that produces actual rendered pixel output from path-space
coordinates, so its coordinate convention is stated explicitly and applies throughout this
document and the `PathFiller`/`EdgeFlattener`/`ScanlineRasterizer` source:** pixel `(x, y)` covers
the path-space unit cell `[x, x + 1) x [y, y + 1)`, with `y` increasing downward (matching
`Surface`'s row-major, top-to-bottom row order). A path coordinate of, for example, `(2.5, 1.5)`
falls exactly at the center of pixel `(2, 1)`. This convention is what makes the analytic coverage
math well-defined: "how much of pixel `(x, y)`'s unit cell lies inside the filled region" is a
precise geometric question once cell boundaries are pinned to integer path coordinates this way.

### Data Model

| Type                 | Description                                                           |
| -------------------- | --------------------------------------------------------------------- |
| `FillRule`           | Public enum: `NonZero`, `EvenOdd` - selects winding-count resolution. |
| `EdgeFlattener`      | Internal static class: converts a `Path`'s subpaths into polygons.    |
| `ScanlineRasterizer` | Internal static class: rasterizes polygons into row coverage.         |
| `PathFiller`         | Public static class: the single `Fill` entry point.                   |

`FillRule.NonZero` (the default) treats a pixel as filled whenever the accumulated signed winding
count at that pixel is non-zero, regardless of magnitude - this is the conventional default for
most vector-graphics fill operations, correctly handling nested same-wound shapes as solid and
nested counter-wound shapes as holes. `FillRule.EvenOdd` instead treats a pixel as filled whenever
the winding count is odd, ignoring winding direction entirely - crossing any edge toggles the
fill state, which is what makes a self-intersecting shape (for example a five-pointed star drawn
without lifting the pen, or two overlapping same-wound rectangles) render with alternating
filled/unfilled regions rather than a single solid union.

### Key Methods

#### PathFiller.Fill(Surface surface, Path path, Rgba32 color, FillRule fillRule, float flattenTolerance)

Fills `path` with the solid `color` onto `surface`, using `fillRule` to resolve overlapping
windings and `flattenTolerance` to bound curve-flattening error (see `BezierFlattening`'s own
tolerance semantics; the default `0.25f` matches a typical single-pixel-scale flattening budget).
There is deliberately no transform parameter this phase - `path`'s coordinates are interpreted
directly in `surface`'s pixel space; a future phase may add transform support once the `Drawing`
subsystem grows a dedicated transform concept.

**Algorithm**:

1. Validate `surface` and `path` are non-null, and `flattenTolerance > 0`.
2. Compute `path.GetBounds(flattenTolerance)` and intersect it with `surface`'s
   `[0, Width) x [0, Height)` pixel extent via `Rect.Intersect`. If `path`'s bounds or the
   intersection is empty (`Rect.IsEmpty`), return immediately - a cheap, well-defined no-op with
   no allocation beyond computing the bounds.
3. Otherwise, call `EdgeFlattener.Flatten(path, flattenTolerance)` to obtain one closed polygon
   per subpath, then `ScanlineRasterizer.Fill(surface, polygons, color, fillRule, clipBounds)` to
   rasterize and composite them, where `clipBounds` is the intersected `Rect` from step 2.

**Throws:**

- `ArgumentNullException` - when `surface` or `path` is `null`
- `ArgumentOutOfRangeException` - when `flattenTolerance` is less than or equal to zero (matching
  `BezierFlattening`'s own tolerance-validation convention)

#### EdgeFlattener.Flatten(Path path, float tolerance) (internal)

Converts every one of `path`'s subpaths into a closed polygon (a `List<Vector2>`), returning one
polygon per subpath in the same order. For each subpath, walks its `Commands` starting from
`Start` (mirroring the current-point bookkeeping `Path.GetBounds` itself uses): `LineTo` appends
its end point directly; `QuadraticBezierTo`/`CubicBezierTo` flattens via
`BezierFlattening.FlattenQuadratic`/`FlattenCubic`; `ArcTo` first converts to cubic Bezier
segments via `SvgArcConverter.ToBeziers`, then flattens each resulting segment via
`BezierFlattening.FlattenCubic`; `Close` contributes no point of its own (closure is handled
below).

**Architectural decision: every subpath is always treated as implicitly closed for fill
purposes, regardless of `Subpath.IsClosed`.** A fill operation has no meaningful notion of an
"open" boundary - unlike a future stroke operation, which would need to distinguish an open path
(rendering flat/round/square end caps) from a closed one. After walking a subpath's commands, if
its last emitted point does not already coincide with `Start` (compared with the same
`NearZeroDisplacement` epsilon tolerance used throughout this unit, not exact floating-point
equality), an explicit closing point equal to `Start` is appended. If the last point already
coincides with `Start` (for example, an explicitly closed subpath whose final command's end point
already equals `Start`), no duplicate closing point is appended.

**Degenerate subpaths** (a subpath whose walked points, after the implicit-close step, number
fewer than 3) are passed through as-is with no special-casing here - `ScanlineRasterizer`
independently treats any polygon with fewer than 2 usable edges as contributing zero coverage
(see below), so no upstream filtering is required.

#### ScanlineRasterizer.Fill(Surface surface, IReadOnlyList\<List\<Vector2\>\> polygons, ...) (internal)

Rasterizes `polygons` (already-flattened, already-closed polygons from `EdgeFlattener`) onto
`surface`, restricted to `clipBounds`, using the AGG/FreeType-style **analytic**
signed-area/coverage-accumulation scanline algorithm - not supersampling. This produces exact,
continuous fractional pixel coverage from the sub-pixel geometry of each edge, rather than an
approximation converging only as the sample count grows.

**Algorithm**:

1. **Edge table construction**: every directed edge (`a` -> `b`) of every polygon is examined.
   Edges classified as horizontal (`|a.Y - b.Y| <= NearZeroDisplacement`) contribute no vertical
   coverage and are skipped entirely. Every other edge is normalized into
   `Edge { TopY < BottomY, TopX (x at TopY), Slope (dx/dy), Direction (+1 if original a.Y < b.Y,
   else -1) }` and the edge table is sorted by `TopY`, giving each edge's own winding direction
   independent of which endpoint was listed first in the polygon.
2. **Sweep**: rows are swept top-to-bottom from `clipBounds.Top` to `clipBounds.Bottom`. An
   active-edge list is maintained: edges are added from the sorted edge table once the sweep
   reaches their `TopY`, and removed once the sweep passes their `BottomY` - bounding the total
   maintenance work to `O(edges + total edge-row crossings)` rather than `O(edges x rows)`.
3. **Per-row accumulation**: two `float` arrays, `cover` and `area`, sized to the clipped row
   width plus one column and cleared at the start of every row, accumulate each active edge's
   contribution for that row:
   - The edge's vertical extent actually within this row (`[max(TopY, rowTop), min(BottomY,
     rowBottom))`) is computed, along with its x-range across that sub-interval.
   - If the edge's x-range within the row is (within epsilon) a single column, its full signed
     `Direction * deltaY` extent is banked as `area` in that column (the analytic sub-pixel
     trapezoidal term for the specific cell the edge passes through) plus `cover` one column to
     the right (so the prefix sum below propagates the edge's full vertical contribution to every
     pixel further right, matching "this edge is entirely to my left" for those columns).
   - Otherwise (a slanted edge spanning multiple columns within the row), the edge's row-local
     x-range is first clipped to `[clipMinX, clipMaxX]` - any portion to the left of the clip is
     banked in bulk at column 0 (its exact `deltaY` share is recovered via linear interpolation
     of the y-split at `clipMinX`), any portion to the right of the clip is dropped entirely
     (contributes nothing observable) - then each pixel column the (now-bounded) visible x-range
     crosses is walked individually, computing that column's exact `deltaY` share and its
     average sub-pixel x-fraction, banking the resulting trapezoidal `area` in that column and
     the remaining `cover` one column to the right.
   - Edges are classified using `NearZeroDisplacement` (`1e-6f`) rather than exact `==`/`!=`
     comparisons, both to satisfy this repository's floating-point-equality analyzer rule and,
     more importantly, because it avoids near-zero-denominator division when computing slope for
     a near-vertical edge or when detecting a near-horizontal one.
4. **Prefix sum and fill-rule resolution**: left to right across the row, `acc += cover[x]` is a
   running signed winding total, and each pixel's raw coverage is `acc + area[x]`. This raw value
   is resolved to an alpha in `[0, 1]` according to `fillRule`:
   - `FillRule.NonZero`: `alpha = min(1, abs(rawCoverage))`.
   - `FillRule.EvenOdd`: `rawCoverage` is folded into `[0, 2)` via modulo (handling negative
     values), then a triangle wave (`t <= 1 ? t : 2 - t`) maps the folded value to `[0, 1]`,
     matching the "every edge crossing toggles fill state" semantics of even-odd winding.
5. **Compositing**: the resulting per-pixel `float[]` coverage row for the row's clipped
   `[minX, maxX)` sub-range is composited directly via
   `Surface.CompositeOverSpan(y, minX, coverage, color)` - no full-row or full-surface coverage
   buffer is ever allocated; only the current row's `cover`/`area`/coverage arrays exist at any
   time.

**Degenerate input**: an empty `polygons` list, or a polygon reduced (after edge-table
construction skips horizontal edges) to fewer than 2 usable non-horizontal edges, contributes no
coverage at any pixel and is a well-defined no-op - it is never treated as an error.

**Architectural decision: dense per-row `cover`/`area` buffers, not sparse AGG-style cells.** The
canonical AGG rasterizer accumulates coverage into a sparse per-scanline cell list, coalescing
adjacent cells later. This unit instead allocates one dense `float[]` pair per swept row, sized to
the clipped row width. For the typical, modest surface sizes and path complexity this library
targets, the simpler dense representation avoids the bookkeeping and allocation overhead of cell
coalescing, at the cost of `O(clippedWidth)` per-row work regardless of how few edges actually
cross that row - an acceptable trade-off given `clipBounds` already restricts the swept region to
the path's own bounds intersected with the surface, not the surface's full width.

**Complexity**: edge-table construction is `O(edges)`; the sweep is `O(edges + total
edge-row crossings)` for active-edge-list maintenance, plus `O(rows * clippedWidth)` for the dense
per-row buffers - i.e. bounded by the clipped bounding box of the path, not the full surface, and
never revisiting an edge for rows outside its own vertical extent.

### Error Handling

All argument validation is performed by `PathFiller.Fill` itself, at the very start of the
method, before any bounds computation or rasterization begins (see above). `EdgeFlattener` and
`ScanlineRasterizer` are internal helpers that assume valid, already-validated input from
`PathFiller.Fill` and perform no further validation of their own; they are only ever reached after
`PathFiller.Fill`'s guards and the empty/out-of-bounds no-op check have already passed.

### Dependencies

`PathFiller` (and its internal `EdgeFlattener`/`ScanlineRasterizer` helpers) depend on
`System.Numerics.Vector2` (in-box BCL type), the `Geometry` subsystem's `Path`, `Subpath`,
`PathCommand`, `Rect`, `BezierFlattening`, and `SvgArcConverter` (via `EdgeFlattener`), and the
`Canvas` subsystem's `Surface`, `Rgba32`, and `Surface.CompositeOverSpan` (via
`ScanlineRasterizer`; see _Surface Unit Design_, `../canvas/surface.md`, for that method's own
documentation). No new runtime NuGet package is introduced.

### Callers

`PathFiller.Fill` is a public API entry point, invoked externally by consumers of the CanvasNet
package. It is exercised end to end by this unit's own tests (`PathFillerTests`,
`EdgeFlattenerTests`, `ScanlineRasterizerTests`) and by a system-integration test that builds a
`Path` via `PathBuilder` and fills it onto a `Surface` (see `CanvasNetTests.cs`). `PathFiller` has
no dependency on any consumer, and no other unit in this library depends on `PathFiller`.
