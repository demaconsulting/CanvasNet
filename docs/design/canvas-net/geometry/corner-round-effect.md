### CornerRoundEffect Unit Design

The `CornerRoundEffect` class is a stateless static class in the `Geometry` subsystem that
returns a new `Path` in which polyline corners are replaced by tangent-radius arcs.

#### Rationale for a path-level effect

Corner rounding is applied at the `Path` level (a pre-processing pass on the immutable path)
rather than inside the stroking pipeline. Placing it here composes cleanly with downstream
`PathFiller`, `PathStroker`, and dashing: `CornerRoundEffect` runs first, producing a new
path with rounded corners baked into the vector geometry; any subsequent stroking, dashing, or
fill sees the rounded corners as ordinary Bezier segments.

Dashing itself is a post-flatten step inside `PathStroker` (it operates on flattened edge
segments), so the only architecturally valid composition point for corner rounding is
before the fill/stroke pipeline. `CornerRoundEffect` occupies that position.

#### Algorithm

For every command in the source path, if command `i` is a `LineTo` and command `i + 1` is also
a `LineTo`, the corner is:

1. `cornerPoint` = end point of command `i`
2. `nextEnd` = end point of command `i + 1`
3. The effective radius is clamped per-corner to
   `min(radius, incomingLen / 2, outgoingLen / 2)` to prevent adjacent rounds from
   overlapping.
4. The effect calls `PathBuilder.TangentArcTo(cornerPoint, nextEnd, effectiveRadius)`, which
   emits a `LineTo` to the tangent-in point followed by a `CubicBezierTo` approximating the
   arc at kappa = 0.5522847498.
5. The next iteration's `LineTo` continues from the tangent-out point to `nextEnd`.

Corners involving a curved segment (cubic or quadratic Bezier or arc) are left unchanged.
This documented policy avoids the derivative-matching logic that would be required to round
curve tangents cleanly.

#### Validation

- `Apply` throws `ArgumentNullException` on a null source path.
- `Apply` throws `ArgumentOutOfRangeException` on a negative or non-finite radius.
- A zero radius returns the source path unchanged (no allocation).
