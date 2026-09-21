<!-- cspell:ignore Outliner inradius -->

## PathStroker

![Drawing Structure](DrawingView.svg)

The `PathStroker` unit converts vector strokes into ordinary closed `Geometry.Path` outline
geometry that the existing `PathFiller` unit can rasterize unchanged. It also covers the public
`StrokeStyle`, `LineCap`, and `LineJoin` types, and the internal `StrokePathFlattener`,
`DashSplitter`, and `StrokeOutliner` helpers.

### Purpose

`PathStroker` exists so CanvasNet can render strokes without introducing a second rasterizer.
Rather than painting pixels directly, it transforms an input centerline path into one or more
closed outline polygons describing the exact area the stroke should occupy. The caller can then
render those polygons through `PathFiller.Fill(..., FillRule.NonZero, ...)`, obtaining the same
analytic antialiasing and compositing behavior fills already use. This design keeps stroke
rendering and fill rendering on one coverage-computation path, which reduces implementation
duplication and ensures the two public APIs agree on how pixel coverage is resolved.

### Data Model

| Type | Description |
| --- | --- |
| `LineCap` | Public enum selecting butt, round, or square end-cap geometry for open stroked segments. |
| `LineJoin` | Public enum selecting miter, round, or bevel corner-join geometry. |
| `StrokeStyle` | Public immutable style snapshot containing width, cap, join, miter limit, and dash settings. |
| `StrokePathFlattener` | Internal helper that flattens each subpath while preserving `Subpath.IsClosed`. |
| `DashSplitter` | Internal helper that applies dash-array and dash-offset semantics to a flattened polyline. |
| `StrokeOutliner` | Internal helper that converts one flattened polyline into outline polygons. |
| `PathStroker` | Public static entry point that orchestrates flattening, dashing, and outlining. |

`StrokeStyle` is intentionally immutable and thread-safe after construction. The constructor
copies a non-empty dash array into a private read-only snapshot rather than retaining a caller's
mutable collection reference, so a stroke style cannot be changed after creation by mutating the
original list elsewhere. This is important because dash phase and width are visible rendering
behavior; a caller must be able to reuse the same `StrokeStyle` instance across many paths and
receive identical outline geometry every time.

### Key Methods

#### StrokeStyle.StrokeStyle(...)

The constructor validates every public styling input at the API boundary, mirroring
`PathFiller.Fill`'s own validation conventions:

- `Width` must be finite and greater than zero.
- `Cap` and `Join` must be defined enum values.
- `MiterLimit` must be finite and greater than or equal to one, matching the SVG/PostScript
  interpretation of `stroke-miterlimit` as a ratio.
- `DashArray` may be null or empty for a solid stroke; otherwise every entry must be finite and
  greater than or equal to zero, and the array must not consist entirely of zeros.
- `DashOffset` must be finite.

This constructor deliberately performs all dash-array validation once, up front, rather than
having `PathStroker` rediscover invalid style data every time a path is stroked. The style is the
caller-controlled configuration object; rejecting bad values there keeps the conversion pipeline
itself focused only on geometry.

#### PathStroker.Stroke(Path path, StrokeStyle style, float flattenTolerance)

`Stroke` performs stroke-to-fill conversion in three geometry-only stages, then emits the result
as a brand-new `Path`:

1. **Flatten while preserving open/closed state.** `StrokePathFlattener` walks each
   `Subpath` exactly once, converting lines directly, flattening quadratic/cubic Beziers via
   `BezierFlattening`, and converting arcs via `SvgArcConverter.ToBeziers` followed by cubic
   flattening. Unlike `EdgeFlattener`, it never appends an implicit closing point for open
   subpaths; `Subpath.IsClosed` is preserved verbatim because stroking must distinguish end caps
   from shell rings.
2. **Apply dashing in path-length space.** `DashSplitter` interprets the dash array as
   alternating on/off lengths, conceptually duplicates odd-length arrays to preserve the standard
   repeating on/off cycle, phase-shifts the pattern by `DashOffset`, and returns only the visible
   "on" segments. When a closed contour's visible dash run wraps across the seam, the helper
   stitches the two halves back into one contiguous segment so the stroke has one join sequence
   rather than two artificial caps at the seam.
3. **Outline each visible segment.** `StrokeOutliner` offsets each segment by half the stroke
   width on both sides and resolves the corners and ends into plain polygon vertices:
   - **Open segments** produce one closed polygon built from the left side, an end cap, the right
     side in reverse, and the start cap.
   - **Closed contours** produce two closed rings (outer and inner) wound in opposite directions
     so `FillRule.NonZero` fills only the shell between them.
   - **Miter joins** intersect the two offset lines and then compare the distance from the source
     vertex to that intersection against `MiterLimit * (Width / 2)`. This single offset line sits
     half the stroke width from the source vertex, so this distance is exactly half of the SVG
     "miter length" (the full tip-to-tip span across both offset lines), and `Width / 2` is
     exactly half the stroke width - the ratio of the two halves equals the full
     `miterLength / strokeWidth` SVG ratio (for example, `~1.414` for a right-angle join, matching
     the standard `1 / sin(theta / 2)` formula). When the ratio exceeds the limit, the join falls
     back to a bevel.
   - **Round joins and round caps** are tessellated as circular arcs whose sagitta is bounded by
     `flattenTolerance`, reusing the same tolerance concept already established for curve
     flattening elsewhere in the library.

The returned path is assembled through `PathBuilder`; `PathStroker` never constructs `Path`
instances directly. Every emitted polygon becomes one closed subpath in the result, and
`Path.Empty` is returned when no outline polygon is produced.

#### StrokeOutliner.Outline(...) (internal)

`StrokeOutliner` carries most of the geometry-specific design decisions:

- **Open-side join handling.** For an open polyline, the side of the stroke on the outside of a
  turn receives the caller-selected join style (`Miter`, `Round`, or `Bevel`). The opposite side
  is the clipped "inside" of the turn and is connected directly between its two offset endpoints,
  because the visible outline there is the boundary of the union of two segment rectangles rather
  than the infinite-line intersection of their offsets.
- **Closed-side join handling.** For a closed contour, the two offset rings are not symmetric, and
  which ring is locally the "outside of the turn" at a given vertex is not a fixed, ring-wide
  property: for a convex closed contour every vertex agrees, but a concave (reflex) vertex flips
  which ring is locally convex there. `StrokeOutliner` therefore resolves each vertex on each ring
  independently, from the same local turn-direction sign used for open-path joins: the side that
  is locally convex at that vertex receives the caller-selected join style (`Miter`, `Round`, or
  `Bevel`), while the side that is locally concave at that vertex is forced to the geometrically
  exact intersection of the two offset edges (falling back to the un-joined offset points only
  when the edges are parallel and have no intersection), never a stylized corner - stylizing the
  concave side would carve away or add stroke area. Because the two rings use opposite offset
  signs, exactly one of them is locally convex at any given vertex, so the two treatments are
  always assigned to the correct ring even as convexity flips along a concave contour. The two
  rings are then wound in opposite directions so `FillRule.NonZero` fills only the shell between
  them, regardless of how many vertices on each ring ended up locally reflex.
- **Inner-ring collapse (half-width exceeding the local inradius).** The forced exact-edge
  intersections above are only valid while the stroke half-width stays within the contour's local
  inradius (the largest half-width for which the offset ring still nests inside the source
  contour). Once half-width exceeds it somewhere along the contour, two adjacent forced
  intersection vertices' shared offset edge runs backwards relative to its source edge's
  direction, instead of forwards - the classic "erosion has gone empty" case from polygon
  offsetting, except the naive per-vertex intersection construction does not notice on its own and
  instead produces an invalid, oversized, wrongly wound ring. `StrokeOutliner` detects this
  per-edge direction reversal while building each ring and, when found on the ring that would
  otherwise become the hole, omits that ring entirely rather than emitting it as a hole - matching
  what a true geometric erosion of the contour by that half-width would produce (an empty inner
  boundary) and leaving the whole interior filled as solid stroke.
- **Degenerate subpaths.** A single point (or a path collapsed to one effective point after
  duplicate-vertex simplification) renders as a cap-shaped mark: round creates a full circle,
  square creates an axis-aligned width-by-width square, and butt creates nothing.

The helper intentionally removes only redundant consecutive duplicate vertices. It does not try to
topologically simplify self-intersections or reorder segments; the contract is to preserve the
input path's authored shape, not to reinterpret it.

### Complexity

The total conversion cost is the sum of three bounded passes over the path data. Flattening is
the same adaptive-subdivision cost already documented for `BezierFlattening`; dashing is linear in
the number of emitted dash boundaries plus the number of polyline edges those boundaries cross;
and outlining is linear in the number of resulting polyline vertices plus the tessellated points
added for round joins and caps. A bevel or miter join adds at most two vertices per corner, while
round joins and caps add a number of vertices proportional to the swept arc angle divided by the
maximum allowed arc step derived from `flattenTolerance`.

These properties are verified by design review and code review rather than timing-based unit
tests. This mirrors the existing `PathFiller` design decision: wall-clock assertions are not a
stable proof of algorithmic behavior on heterogeneous CI hardware, while the geometric pass
structure and bounded per-vertex/per-arc work are visible directly in the implementation.

### Error Handling

`PathStroker.Stroke` performs the public API validation:

- `ArgumentNullException` when `path` or `style` is null.
- `ArgumentOutOfRangeException` when `flattenTolerance` is non-finite or less than or equal to
  zero.

The internal helpers assume they receive already-validated input from `PathStroker`. Likewise,
`StrokeStyle` performs its own constructor validation and throws `ArgumentOutOfRangeException` or
`ArgumentException` before a style instance is ever created.

### Dependencies

`PathStroker` depends on the `Geometry` subsystem's `Path`, `Subpath`, `PathCommand`,
`PathBuilder`, `BezierFlattening`, and `SvgArcConverter` types, and on `System.Numerics.Vector2`
for the underlying geometry math. It has no direct dependency on `Canvas.Surface` because it does
not rasterize; callers render its result through `PathFiller`, which in turn depends on the
`Canvas` subsystem. No new runtime NuGet package is introduced by this unit.

### Callers

`PathStroker.Stroke` is a public API entry point, invoked directly by consumers of the CanvasNet
package and indirectly by the README and User Guide examples that demonstrate stroking followed by
filling. The unit is exercised by `PathStrokerTests` end to end, and by dedicated internal-helper
tests for flattening, dash semantics, and outline geometry.
