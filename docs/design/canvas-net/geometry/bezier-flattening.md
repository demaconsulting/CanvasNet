## BezierFlattening

![Geometry Structure](GeometryView.svg)

The `BezierFlattening` class is a software unit in the `Geometry` subsystem. It provides
adaptive, tolerance-driven flattening of quadratic and cubic Bezier curves into polylines,
writing the resulting points into a caller-supplied output collection.

### Purpose

Rendering, precise bounds computation, and other consumers of vector geometry frequently need to
convert a smooth curve into a sequence of straight-line segments within a controllable accuracy
budget. `BezierFlattening` provides this for both quadratic and cubic Bezier curves, using
recursive adaptive subdivision so flatter regions of a curve receive fewer segments than sharply
curved regions, for a given error tolerance.

### Key Methods

#### FlattenCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float tolerance, IList\<Vector2\> output)

#### FlattenQuadratic(Vector2 p0, Vector2 p1, Vector2 p2, float tolerance, IList\<Vector2\> output)

Appends the flattened polyline points for the given cubic (`p0`-`p1`-`p2`-`p3`) or quadratic
(`p0`-`p1`-`p2`) Bezier curve to `output`, via recursive adaptive subdivision (de Casteljau
midpoint split).

**Architectural decision: `IList<Vector2> output` parameter, not a return list.** Both methods
write into a caller-supplied output collection rather than allocating and returning a fresh list.
This is deliberately allocation-conscious: a caller flattening many curves in a hot rendering
loop (for example, every curve in a `Path`) can reuse one growable list across every call, paying
for at most occasional list-capacity growth rather than one fresh allocation per curve.

**Architectural decision: `p0` is never written to `output`; `pEnd` (`p3` or `p2`) is always
written last.** This convention lets repeated calls chain their outputs into one continuous
polyline: the caller's first call supplies the overall start point externally (it is already
known), and every subsequent curve segment's `p0` is the same point as the previous segment's
`pEnd` (already the last point in `output`), so writing it again would produce a duplicate point
at every segment boundary.

**Algorithm**: the flatness test measures the distance of the curve's interior control point(s)
from the finite chord *segment* between its endpoints (`p0`-`p3` for cubic, `p0`-`p2` for
quadratic) - not the infinite line through that chord. Each control point is projected onto the
chord and the projection parameter is clamped to `[0, 1]` before measuring distance to that
clamped point, so a control point whose unclamped projection falls beyond a chord endpoint is
correctly measured against that endpoint rather than being wrongly judged "flat" via its
(potentially much smaller) distance to the infinite line. If every interior control point's
distance is `<= tolerance`, the curve is accepted as flat enough and only its end point is
appended. Otherwise, the curve is split at `t = 0.5` via de Casteljau's algorithm into two
half-curves, and both halves are flattened recursively.

**Architectural decision: `MaxRecursionDepth` (20) is a resource/termination safety bound, not a
tolerance guarantee.** Once recursion reaches this depth, the current sub-curve's end point is
accepted and appended *regardless of whether it actually passes the flatness test*. This mirrors
the well-established `curve_recursion_limit` convention from Anti-Grain Geometry (AGG) - the
reference implementation most 2D vector graphics libraries derive their curve-flattening approach
from -
whose own documented default is 32, and whose own implementation likewise emits the segment
unconditionally once the limit is hit. This repository deliberately keeps 20 rather than matching
AGG's 32 verbatim: for any well-formed curve, the guard is never reached at either value, so the
only real difference between them is the worst-case bound placed on a genuinely pathological or
adversarial input that never converges under the flatness test - which is exactly the scenario
this limit exists to bound. At depth 20 that worst case is 2^20 (about one million) output points
for a single input curve; at depth 32 it is 2^32 (over four billion) - no longer a "safety valve"
for a pathological input, but a multi-gigabyte allocation and an unbounded-feeling hang. For all
realistic, well-formed curves at any sane tolerance, this limit is never reached in practice - the
tolerance guarantee described above holds unconditionally for such input. Only pathological input
(near-coincident control points at floating-point precision limits, or a tolerance far tighter
than float precision can resolve) - for which perfect convergence is not achievable at any
reasonable depth in the first place - can exercise this safety valve, and for that input only the
termination/bounded-output-size guarantee applies, not the tolerance guarantee.

**Bug found and fixed**: an earlier version measured distance to the *infinite line* through the
chord (via a cross product divided by chord length) rather than the finite chord segment. A
control point whose projection landed beyond a chord endpoint could then be wrongly judged "flat"

- its distance to the infinite line small - even though the curve it belongs to travels far
outside the flattened output's chord segment, producing a flattened polyline that silently
violated the requested tolerance. Clamping the projection parameter to `[0, 1]` before measuring
distance fixes this.

**Throws:**

- `ArgumentOutOfRangeException` - when `tolerance` is less than or equal to zero, for either
  method (a non-positive tolerance can never be satisfied by any finite subdivision)

No other input throws, including coincident or collinear control points - these are simply
degenerate curves that flatten to very few points (or terminate immediately, if already flat
enough by the flatness test).

**Recursion depth is a resource guarantee, not a tolerance guarantee.** See the architectural
decision above: `MaxRecursionDepth` bounds worst-case output size and guarantees termination for
every input, but for the rare pathological input that hits the limit, the tolerance contract does
not apply.

### Error Handling

The single `tolerance <= 0` guard above is this unit's only validation; every other combination
of finite `Vector2` control points is accepted.

### Dependencies

`BezierFlattening` depends only on `System.Numerics.Vector2` (in-box BCL type, no new NuGet
package) and `System.Collections.Generic.IList<T>` from the .NET Base Class Library.

### Callers

`BezierFlattening` is a public API entry point, invoked externally by consumers of the CanvasNet
package. It is also invoked internally by `Path.GetBounds`'s flattening mode (a positive
`flattenTolerance`) - see *Path Unit Design* (`path.md`). `BezierFlattening` has no dependency on
`Path`, `Rect`, or `SvgArcConverter`.
