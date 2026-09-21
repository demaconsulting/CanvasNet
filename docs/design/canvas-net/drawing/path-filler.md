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

1. Validate `surface` and `path` are non-null, `fillRule` is a defined `FillRule` value
   (`Enum.IsDefined<FillRule>(fillRule)`, matching `BmpCodec.Save`/`TiffCodec.Save`'s own
   enum-validation convention) - rejected here rather than being silently treated as `EvenOdd` by
   `ScanlineRasterizer.ResolveCoverage`'s winding-resolution `else` branch, which has an explicit
   case only for `NonZero` - and `flattenTolerance` is finite and strictly
   positive (`float.IsFinite(flattenTolerance) && flattenTolerance > 0`) - a non-finite tolerance
   (`NaN`/`+-Infinity`) is rejected here rather than being allowed to silently reach
   `EdgeFlattener`/`BezierFlattening`, where flatness comparisons involving a non-finite tolerance
   never succeed and curve subdivision would instead recurse to its maximum depth, wastefully
   allocating on the order of a million points before ultimately producing a meaningless result.
2. Call `EdgeFlattener.Flatten(path, flattenTolerance)` **first** to obtain one closed polygon per
   subpath. Curve/arc flattening is the expensive part of this method, so it is performed exactly
   once; there is deliberately no separate, earlier call to `Path.GetBounds(flattenTolerance)`
   (which would flatten every curve a second time purely to compute a bounding box).
3. Compute the path's bounds directly from the already-flattened polygons' own vertices (folding
   `Rect.Union` over each vertex, starting from `Rect.Empty`) and intersect that bounds with
   `surface`'s `[0, Width) x [0, Height)` pixel extent via `Rect.Intersect`. If the flattened
   bounds or the intersection is empty (`Rect.IsEmpty`), return immediately - a well-defined
   no-op. Because flattening has already occurred by this point, this no-op check no longer
   avoids the flattening cost itself, only the cost of the rasterize-and-composite step -
   flattening an out-of-bounds or degenerate path is cheap relative to full rasterization, so this
   remains an acceptable trade-off for avoiding the double-flatten.
4. Call `ScanlineRasterizer.Fill(surface, polygons, color, fillRule, clipBounds)` to rasterize and
   composite the already-flattened polygons, where `clipBounds` is the intersected `Rect` from
   step 3.

**Throws:**

- `ArgumentNullException` - when `surface` or `path` is `null`
- `ArgumentOutOfRangeException` - when `fillRule` is not a defined `FillRule` value, or when
  `flattenTolerance` is non-finite, or less than or equal to
  zero (matching `BezierFlattening`'s own tolerance-validation convention, extended to also reject
  `NaN`/`Infinity` explicitly rather than relying on comparison operators alone)

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
its last emitted point does not already coincide with `Start` (compared with **exact
floating-point equality**, not an epsilon tolerance), an explicit closing point equal to `Start`
is appended. If the last point already coincides with `Start` (for example, an explicitly closed
subpath whose final command's end point already equals `Start`), no duplicate closing point is
appended. Exact equality is correct (not merely convenient) here because both
`BezierFlattening.FlattenCubic`/`FlattenQuadratic` and `SvgArcConverter.ToBeziers` are
deliberately designed so the very last point written for any command is always the original,
caller-supplied `PathCommand.EndPoint` verbatim - `SvgArcConverter` explicitly forces its final
Bezier segment's end point to the caller-supplied `end` rather than a value obtained from
trigonometric evaluation, precisely so no residual floating-point drift can occur. Since the last
emitted point is therefore never a computed approximation, the only way it can differ from
`Start` is a genuine, intentional gap in the path data - not floating-point rounding - so an
epsilon comparison would risk silently dropping a real closing edge rather than guarding against
any actual rounding error.

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
3. **Per-row cell accumulation, not per-interval edge sorting**: every active edge's extent within
   the row is captured as a `RowEdge` (its `x` at the row's top and bottom, its vertical span
   within the row, and its winding `Direction`). Each `RowEdge` is then accumulated
   **independently** into two shared, dense per-column arrays for the row - `cover[x]` (the net
   signed vertical extent contributed by every edge slice passing through column `x`) and `area[x]`
   (the exact signed sub-cell area of column `x` lying to the right of every edge slice passing
   through it) - via `AccumulateRowEdge` (reusing the same `AccumulateSingleColumn`/
   `AccumulateSlantedSpan` per-edge geometry as a single-edge design would use). This is a single
   `O(edges)` pass with **no sorting of edges by `x` and no pairing of edges into "inside gaps"
   whatsoever**.
4. **Single left-to-right sweep and fill-rule resolution**: a running `accumulatedCover` total
   starts at zero. At each column `x`, `accumulatedCover` is first advanced by this column's own
   `cover[x]` (folding this column's own vertical edge crossings into the running winding total),
   and only then is the row's raw signed value resolved as `accumulatedCover + area[x]` (the
   updated running total, including this column, plus this column's own partial-edge geometry),
   converted to a `[0, 1]` coverage fraction by `ResolveCoverage` per `FillRule` (`NonZero`:
   `min(1, abs(total))`; `EvenOdd`: fold `total` into `[0, 2)` and reflect,
   `folded > 1 ? 2 - folded : folded`). The resulting
   per-pixel `float[]` coverage row for the row's clipped `[minX, maxX)` sub-range is composited
   directly via `Surface.CompositeOverSpan(y, minX, coverage, color)` - no full-row or
   full-surface coverage buffer is ever allocated; only the current row's `cover`/`area`/coverage
   arrays exist at any time.
   - **Why this handles crossing/self-intersecting edges correctly, not merely assumed absent**:
     a prior revision of this algorithm split each row into sub-intervals at every edge's own
     start/end `y`, sorted the edges spanning each sub-interval by `x` once, and relied on that
     order staying fixed for the sub-interval's whole vertical extent. That assumption fails
     whenever two edges actually cross each other's `x`-order strictly inside a sub-interval (not
     at a shared vertex or row boundary) - which happens for ordinary self-intersecting polygons
     such as a bowtie or star, which `EdgeFlattener` does not detect or split - producing gross
     over-filling (reproduced: ~100% fill where a dense-supersampling ground truth reference is
     ~67%, under both fill rules, for a simple two-edge bowtie crossing mid-row). Cell-based
     accumulation sidesteps this entirely: each edge only ever contributes to the specific
     column(s) it geometrically passes through, independently of every other edge, and the
     sweep's running total reconstructs the correct winding number at every `x` purely via
     summation - which is
     associative/commutative regardless of the order in which edges cross one another. No
     comparison between edges' `x`-positions is ever needed, so crossing and self-intersecting
     edges are handled correctly by construction, not merely assumed absent.
   - **Accepted trade-off: exactly coincident/duplicate edges within the same cell.** Because each
     edge's contribution is accumulated independently rather than resolved against a per-cell
     winding decision first, two edges that occupy the same, or overlapping, sub-pixel position
     within one cell (for example, an identical polygon submitted twice, or two distinct
     overlapping shapes whose boundaries both land in the same pixel column) have their raw
     signed cover/area contributions sum linearly, which can exceed the single-shape value before
     `ResolveCoverage` folds the total back into `[0, 1]` - this is the same documented, accepted
     behavior of AGG/FreeType/`stb_truetype` for coincident/overlapping contours within one cell,
     and is a materially rarer case in practice than ordinary self-intersecting geometry, which is
     why this trade-off is accepted in exchange for fixing the crossing-edge bug above.

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
`ScanlineRasterizer`; see *Surface Unit Design*, `../canvas/surface.md`, for that method's own
documentation). No new runtime NuGet package is introduced.

### Callers

`PathFiller.Fill` is a public API entry point, invoked externally by consumers of the CanvasNet
package. It is exercised end to end by this unit's own tests (`PathFillerTests`,
`EdgeFlattenerTests`, `ScanlineRasterizerTests`) and by a system-integration test that builds a
`Path` via `PathBuilder` and fills it onto a `Surface` (see `CanvasNetTests.cs`). `PathFiller` has
no dependency on any consumer, and no other unit in this library depends on `PathFiller`.
