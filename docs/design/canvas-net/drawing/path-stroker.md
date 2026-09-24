<!-- cspell:ignore Outliner inradius Collinearity -->

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
   - **Outer-outline winding normalization.** `Stroke` appends every stroked subpath's outline(s)
     as independent closed subpaths in one output `Path`, and the whole result is documented to be
     filled with `FillRule.NonZero`. Because NonZero only reinforces (unions) two overlapping
     subpaths when they carry the *same* signed winding, every independently-emitted **outer**
     outline - an open-line outline, a point-cap circle or square, or a closed contour's outer
     shell ring - is normalized to one fixed winding direction regardless of the source subpath's
     authored orientation. Without this, two overlapping outer outlines produced by the same
     `Stroke` call could end up with opposite signed winding purely by chance of which outline kind
     produced each one (or by the caller's authored point order for a closed contour), and their
     overlap would cancel to an incorrect unfilled hole instead of solid fill. A closed contour's
     **inner** ring is deliberately *not* normalized to this same fixed direction: it is fixed up
     relative to its own (already-normalized) outer ring instead, so it always remains the
     opposite winding of that ring - the unrelated mechanism that makes `FillRule.NonZero` render
     the shell between the two rings rather than the solid disc of one ring.
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
- **Outer-outline winding normalization.** Every independently-emitted OUTER outline - an
  open-line outline (`CreateOpenStrokePolygons`), a point-cap circle or square
  (`CreatePointStrokePolygons`), or a closed contour's outer shell ring
  (`CreateClosedStrokePolygons`) - is normalized to one fixed signed-winding direction via a shared
  `NormalizeOuterWinding` helper before it is returned. Without this, an open-line outline was
  always wound one way, a point-cap circle/square the other way, and a closed contour's outer ring
  simply followed whichever direction its source contour happened to be authored in - so two
  overlapping outer outlines from the same `PathStroker.Stroke` call could easily end up with
  opposite signed winding purely by coincidence, which `FillRule.NonZero` would then cancel to an
  incorrect unfilled hole in their overlap instead of reinforcing (unioning) them. Reversing a
  polygon's vertex order only flips its signed winding, never its silhouette, so this
  normalization changes no filled shape on its own - it only removes the accidental sign mismatch
  between independently-produced outer outlines. A closed contour's inner (hole) ring is
  deliberately excluded from this normalization: it is fixed up separately, immediately after, to
  remain the *opposite* winding of its own (already-normalized) outer ring, which is the unrelated,
  pre-existing mechanism that makes the shell between the two rings render instead of the solid
  disc of one ring.
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
  square creates an axis-aligned width-by-width square, and butt creates nothing. This same
  single-point handling extends to a closed contour that has fewer than three distinct vertices
  after simplification (for example, a closed two-point subpath) - a contour that small cannot
  enclose any area, so `StrokeOutliner` treats it exactly as it would treat the equivalent open
  multi-segment stroke, with caps at both ends per `style.Cap`, rather than attempting to build a
  zero-area shell.
- **Degenerate closed contours: collinear vertices.** A closed contour is equally degenerate when
  it has three or more distinct vertices that all lie on a single line - for example, a contour
  that traces back and forth along one line before closing. Such a contour also encloses no area,
  so building the usual outer/inner offset rings for it would produce two rings with zero signed
  area that then get forced into opposite winding, which `FillRule.NonZero` cancels to nothing.
  `StrokeOutliner` detects this case up front by checking whether every vertex's perpendicular
  distance from the line through the first two distinct vertices is within a small tolerance, and
  when so, routes the whole contour through the same open-stroke path used for the
  fewer-than-three-vertex case above - the only well-defined non-empty rendering of a closed path
  that encloses no area.

The helper intentionally removes only redundant consecutive duplicate vertices. It does not try to
topologically simplify self-intersections or reorder segments; the contract is to preserve the
input path's authored shape, not to reinterpret it.

### Double-Precision Internal Computations

Several of `StrokeOutliner`'s and `DashSplitter`'s internal computations are deliberately performed
in `double` precision and only narrowed back to `float` (or kept as `double` when only a sign
comparison is needed) at the end, even though the public API is entirely `float`-based
(`System.Numerics.Vector2`). This is a precision safeguard, not a change to the supported
coordinate range: `float` (32-bit) arithmetic on values that are each individually well within
`float`'s representable range can still overflow to `Infinity` or `NaN` partway through a
computation - most commonly when a squared distance, a cross-product term, or an accumulated sum
of several such terms exceeds `float.MaxValue` even though the true, final mathematical result
would not. An overflowed or `NaN` intermediate value silently produces a degenerate result (a
zeroed tangent/normal, a misclassified degenerate contour, or an unreliable winding sign) rather
than a visible error, so the safest place to avoid it is inside the computation itself, not at its
call sites. Concretely, this applies to:

- **Per-segment tangent/normal computation** (edge delta and length), so an edge spanning
  near-extreme coordinates still offsets correctly instead of collapsing to a zero-length
  tangent/normal.
- **Collinearity testing for a closed contour** (vertex differences, squared length, direction
  normalization, and perpendicular-distance cross products), so a legitimately non-collinear large
  contour is not misclassified as degenerate merely because an intermediate squared length
  overflowed.
- **Signed-area computation** (the shoelace cross-product terms and their running sum), used both
  to pick which side of a closed contour is the outer ring and to confirm the outer and inner rings
  end up with opposite winding, so that decision remains reliable even for contours near the edges
  of the representable coordinate range.
- **Dash-length accumulation and dash-offset phase location** in `DashSplitter`, so a very long
  polyline, a very large dash pattern, or a dash offset far larger in magnitude than the dash
  pattern all resolve to the correct visible phase instead of an overflowed or oscillating result.

Every one of these call sites only ever needs a sign, a ratio, or a normalized direction from the
result - never the literal double-precision magnitude exposed back through the public API - so
computing in `double` internally is a pure precision safeguard with no observable effect on
ordinary, non-extreme stroke geometry.

#### Bounding Dash-Interval Traversal Iteration Count

Computing `DashSplitter`'s dash intervals in `double` (rather than `float`) keeps its ULP (unit in
the last place) negligible relative to any dash span at the path lengths this library is intended
to support, and a defensive `Math.BitIncrement`-based advance-guard ensures the traversal loop can
never fail to terminate even if a step does not advance `position`. However, neither of those
measures bounds the loop's worst-case *iteration count*: a path whose total length is huge but
still finite (for example, a segment spanning coordinates on the order of `1e20`) combined with a
fine dash span (for example `[5, 5]`) simply has an ordinary-shaped but astronomically large
`totalLength / dashSpan` ratio - the advance-guard itself would only actually be forced to run at
magnitudes many orders larger still (`position` reaching roughly `dashSpan × 2^52`) and is not what
makes this case slow. That ratio alone is a reproducible, unconditional near-hang, not merely a
slow computation, even though every value involved (`position`, `span`, `totalLength`) stays finite
throughout - so no `IsFinite`-style guard can detect it.

`BuildOnIntervals` counts every pass through its traversal loop (including iterations that only
advance the dash-pattern phase without emitting an interval) against a fixed cap,
`MaxOnIntervalIterations = 100_000_000`. Each dash-pattern-entry transition costs **two** loop
iterations, not one: one iteration consumes the entry's remaining span (advancing `position`), and
a separate iteration advances to the next pattern entry. Direct instrumentation of the existing
`DashSplitter_Split_FineDashPatternOnVeryLongPath_CompletesWithCorrectSegments` regression test (a
17,000,000-unit path with a `[1, 1]` dash pattern) confirms this legitimately requires
**~34,000,000** iterations (measured: 33,999,999) - correcting an earlier, since-fixed doc/comment
figure of "~17,000,000" that was too low by exactly the missing per-transition advance iteration.
`100,000,000` gives a ~2.94x margin over that corrected 34,000,000 baseline, while a bare
100,000,000-iteration loop of this shape was separately measured to complete in roughly 200-400ms
(Release, JIT-warmed) - far below any threshold a caller could perceive as hanging.

Before entering the loop at all, `BuildOnIntervals` runs a cheap, `O(pattern.Count)` pre-flight
estimate using the same two-iterations-per-transition cost model:
`estimatedIterations = 2 * positiveEntryCount * (totalLength / patternLength)`, where
`positiveEntryCount` is the count of strictly-positive entries in the (already-normalized) dash
pattern (zero-length entries cost nothing in the real loop, so they are excluded) and
`patternLength` is the sum of *all* pattern entries (the full cycle length), not just the smallest
one. When that estimate already exceeds `MaxOnIntervalIterations`, the loop is skipped entirely -
the method reports the cap-exceeded outcome immediately, without ever running the traversal loop -
turning a hopeless input's cost from `O(MaxOnIntervalIterations)` into a handful of arithmetic
operations. This is a heuristic estimate that closely tracks the loop's real cost model, not an
exact prediction or a strict mathematical upper bound: for paths short relative to a single pattern
cycle it can under-count by at most roughly one cycle's worth of iterations (bounded by
`2 * positiveEntryCount`, an array-length-order constant, negligible relative to the
100,000,000-iteration cap decision boundary and only relevant to inputs that are already fast to
resolve). It has been verified safe (does not falsely short-circuit) against a symmetric long-path
legitimate case, an asymmetric small+large pattern case, a zero-heavy pattern case (many zero
entries mixed with one large positive entry, a legal `StrokeStyle` input), and the adversarial
huge-ULP case (which it still correctly short-circuits); the loop's own running iteration count
remains the authoritative backstop for any input the pre-flight estimate does not catch. This is
what makes raising the cap
from 50,000,000 to 100,000,000 safe for CI runtime specifically: the cap's magnitude no longer
determines how long a hopeless input takes to resolve, so it can be sized purely for a comfortable
margin over legitimate use rather than traded off against pathological-input runtime. When the cap
is reached (via either the pre-flight short-circuit or the loop's own running count),
`BuildOnIntervals` signals this back to `Split` via an `out bool` parameter rather than continuing
to loop; `Split` then abandons dashing entirely for the whole path and falls back to a solid stroke
(returning the original polyline as a single, unsplit segment), mirroring this same method's
existing "cannot resolve this dash pattern" fallbacks for a non-finite or non-positive total
pattern length. This fallback shape matches `SvgCodec.RenderStroke`'s own established "cannot use
this dash pattern -> render as solid stroke" convention for other dash-pattern-specific numeric
problems, rather than throwing or silently omitting the stroke.

#### Bounding Retained On-Interval Count

`MaxOnIntervalIterations` (above) bounds the traversal loop's worst-case *iteration count*, but a
cloud-PR-review finding identified that iteration count is a distinct quantity from the loop's
*retained output*: `BuildOnIntervals` only appends an interval to its returned list for the
strictly-positive, even-indexed ("on") pattern entries - odd ("off") entries and zero-length
entries cost iterations but retain nothing. A pattern shaped so most transitions are "on"
transitions (for example `[1, 1]`, where every other entry is on) can stay at or under
`MaxOnIntervalIterations`'s estimate while still materializing tens of millions of retained
tuples. `Split` does not stop at the tuple list either: it turns each retained interval into its
own extracted polyline segment, which `PathStroker.Stroke` then feeds through `StrokeOutliner`
per segment, accumulating every resulting outline polygon into one combined path - so the real
retained cost is proportional to on-interval count times per-segment outline cost, not merely
`sizeof(interval) * count`.

A concrete worst case makes the gap exact: a two-point path with `totalLength = 50,000,000` and
dash pattern `[1, 1]` yields `estimatedIterations = 2 * 2 * (50,000,000 / 2) = 100,000,000` - not
*greater than* the 100,000,000-iteration cap, so `MaxOnIntervalIterations` alone does not trigger

- yet the same pattern retains `estimatedOnIntervalCount = 1 * (50,000,000 / 2) = 25,000,000`
on-intervals, each destined to become its own segment and outline polygon.

`BuildOnIntervals` now runs a second, independent `O(pattern.Count)` pre-flight estimate:
`estimatedOnIntervalCount = onEntryCount * (totalLength / patternLength)`, where `onEntryCount` is
the count of strictly-positive, even-indexed pattern entries (mirroring the same
`totalLength / patternLength` cycle count used by the iteration estimate, but counting only the
entries that actually retain output). When this estimate exceeds a new, separate
`MaxOnIntervalCount = 10,000,000` budget, the loop is skipped entirely, exactly like the
iteration-count short-circuit above. `MaxOnIntervalCount`'s value must stay above `8,500,000` (the
on-interval count legitimately required by the existing
`DashSplitter_Split_FineDashPatternOnVeryLongPath_CompletesWithCorrectSegments` regression test,
which must keep passing) and below `25,000,000` (the pathological `[1, 1]`-on-~50,000,000-unit-path
scenario above), giving `10,000,000` a comfortable margin on both sides. As a backstop for either
pre-flight estimate under-counting, the loop itself also increments a running count of retained
intervals immediately after appending each one and stops as soon as it would exceed
`MaxOnIntervalCount`, mirroring `MaxOnIntervalIterations`'s own in-loop running-count backstop.
Either budget being exceeded (via pre-flight estimate or in-loop backstop, for either the
iteration-count or the on-interval-count budget) is reported back to `Split` through the same
`out bool` parameter and triggers the same solid-stroke fallback described above.

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
