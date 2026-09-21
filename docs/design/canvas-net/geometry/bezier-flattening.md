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
half-curves, and both halves are flattened recursively. A maximum recursion depth (20) guards
against runaway recursion for pathological input; at the depth limit, the current sub-curve is
accepted regardless of its flatness, guaranteeing the algorithm always terminates.

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
