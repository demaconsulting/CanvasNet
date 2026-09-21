## SvgArcConverter

![Geometry Structure](GeometryView.svg)

The `SvgArcConverter` class is a software unit in the `Geometry` subsystem. It converts
SVG-style endpoint-parameterized elliptical arcs into one or more cubic Bezier curves, following
the SVG 1.1 Appendix F "Elliptical arc implementation notes" algorithm.

### Purpose

`PathBuilder.ArcTo` always stores the raw SVG arc parameters supplied by the caller, without
pre-inspecting or pre-converting them (see _Path Unit Design_, `path.md`). `SvgArcConverter` is
the only place in the `Geometry` namespace responsible for SVG-spec fidelity, including its
documented degenerate cases, so a consumer that never needs Bezier segments (for example, a
hit-testing implementation with its own arc math) never pays for the conversion; conversion
happens lazily, only when a consumer actually calls `ToBeziers`.

### Key Method

#### ToBeziers(...)

Signature:

```csharp
ToBeziers(Vector2 start, Vector2 radius, float rotationDegrees, bool largeArc, bool sweep,
    Vector2 end, IList<(Vector2 Control1, Vector2 Control2, Vector2 End)> output)
```

Converts the described SVG-style elliptical arc into a sequence of cubic Bezier segments,
appended to `output` as `(Control1, Control2, End)` tuples in end-to-end order, so a caller
building a continuous polyline/curve chain never needs to re-derive the implicit start point of
each segment (it is the previous segment's `End`, or `start` for the first).

**Architectural decision: `IList<...> output` parameter, not a return list.** As with
`BezierFlattening`, `ToBeziers` writes into a caller-supplied output collection rather than
allocating and returning a fresh list, for the same allocation-conscious, hot-loop-friendly
reason.

**Algorithm** (SVG 1.1 Appendix F, endpoint-to-center parameterization):

1. If `start == end`, emit nothing (per spec, a zero-length arc is not rendered).
2. If either radius component is zero, emit exactly one synthetic straight-line-equivalent cubic
   Bezier, with control points at one-third and two-thirds along the `start`-`end` chord (see the
   degenerate-case decision below).
3. Otherwise, apply the spec's out-of-range radius correction (the Lambda scale-factor formula),
   compute the center parameterization (`cx`, `cy`, `theta1`, `deltaTheta`), split `deltaTheta`
   into sub-arcs of at most 90 degrees each, and convert each sub-arc to a cubic Bezier via the
   standard `kappa = 4/3 * tan(delta/4)` control-point-distance formula, expressed on the unit
   circle then mapped through the ellipse's radii, x-axis rotation, and center offset.

**Architectural decision: the final segment's `End` is forced to exactly the caller-supplied
`end` parameter**, rather than the value obtained by evaluating trigonometric functions at the
target angle. This avoids residual floating-point drift between the computed end angle's
position and the caller's own exact `end` value, so a caller chaining segments into a continuous
curve/polyline always lands exactly on the requested end point.

**Architectural decision: zero-radius degenerate case emits a synthetic straight-line-equivalent
cubic**, per the SVG specification's own definition of this case as "treated as a straight line".
A synthetic cubic whose controls lie exactly on the chord reproduces a straight line precisely,
while keeping this unit's output contract uniform (always cubic Bezier segments) for every
caller, rather than requiring every consumer to special-case a line-segment result type.

`ToBeziers` never throws for any SVG-valid input, including out-of-range radii (corrected per the
specification) and all four `largeArc`/`sweep` flag combinations.

### Error Handling

`ToBeziers` performs no argument validation and never throws; every documented degenerate case
(zero-length arc, zero radius) is handled by producing a geometrically sensible result rather
than raising an exception, per this unit's contract.

### Dependencies

`SvgArcConverter` depends only on `System.Numerics.Vector2` (in-box BCL type, no new NuGet
package) and `System.MathF`/`System.Collections.Generic.IList<T>` from the .NET Base Class
Library.

### Callers

`SvgArcConverter` is a public API entry point, invoked externally by consumers of the CanvasNet
package. It is also invoked internally by `Path.GetBounds`, to convert any `ArcTo` command to
cubic Bezier segments before applying either bounds mode - see _Path Unit Design_ (`path.md`).
`SvgArcConverter` has no dependency on `Path`, `Rect`, or `BezierFlattening`.
