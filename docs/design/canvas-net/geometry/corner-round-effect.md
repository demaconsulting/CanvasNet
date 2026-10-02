### CornerRoundEffect Unit Design

The `CornerRoundEffect` class is a stateless static class in the `Geometry` subsystem that
returns a new `Path` in which polyline corners are replaced by tangent-radius arcs.

#### Purpose

Corner rounding is applied at the `Path` level (a pre-processing pass on the immutable path)
rather than inside the stroking pipeline. Placing it here composes cleanly with downstream
`PathFiller`, `PathStroker`, and dashing: `CornerRoundEffect` runs first, producing a new
path with rounded corners baked into the vector geometry; any subsequent stroking, dashing, or
fill sees the rounded corners as ordinary Bezier segments.

Dashing itself is a post-flatten step inside `PathStroker` (it operates on flattened edge
segments), so the only architecturally valid composition point for corner rounding is
before the fill/stroke pipeline. `CornerRoundEffect` occupies that position.

#### Data Model

`CornerRoundEffect` is a stateless static class with no instance fields and no supporting types
of its own. `Apply`'s only parameters are a `Path source` and a `float radius`; its private
helpers (`ComputeWrapAroundCorner`, `ClampRadius`) operate purely on `Vector2` points and
`Subpath`/`PathCommand` values already defined by the `Path` unit. There is no other data model
to document.

#### Key Methods

##### Apply(Path source, float radius)

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

For a closed subpath (one ending in a trailing `Close`), the same corner-detection and
`TangentArcTo` mechanics also apply to two additional vertices that have no literal adjacent
`LineTo`-then-`LineTo` pair in the command list:

- The vertex at the last real command's endpoint, immediately before the trailing `Close`: its
  `nextEnd` is the subpath's start point (`Subpath.Start`), since the implicit closing edge
  drawn by `Close` runs from that vertex back to the start.
- The wrap-around vertex at the subpath's start point itself: its "incoming" edge is that same
  implicit closing edge, and its "outgoing" edge is the subpath's first `LineTo`. Because the
  builder must begin the new subpath with a `MoveTo` before either corner has been rounded, the
  effect first computes this wrap-around corner's tangent-out point with a scratch
  `PathBuilder`, then issues the real `MoveTo` directly to that point instead of to the
  un-rounded start; the closing `Close` command is then replaced with a final
  `TangentArcTo` back to the start followed by `Close`.

Corners involving a curved segment (cubic or quadratic Bezier or arc) are left unchanged.
This documented policy avoids the derivative-matching logic that would be required to round
curve tangents cleanly.

#### Error Handling

- `Apply` throws `ArgumentNullException` on a null source path.
- `Apply` throws `ArgumentOutOfRangeException` on a negative or non-finite radius.
- A zero radius returns the source path unchanged (no allocation).

#### Dependencies

`CornerRoundEffect` depends only on the `Geometry` subsystem's own `Path`, `PathBuilder`,
`Subpath`, and `PathCommand`/`PathCommandType` types, and on `System.Numerics.Vector2` from the
.NET Base Class Library. It has no dependency on `Drawing`, `Canvas`, or any other subsystem.

#### Callers

`CornerRoundEffect` is a public API entry point, invoked externally by consumers of the
CanvasNet package that want rounded-corner polylines (for example before passing the result to
`Drawing.PathFiller`/`Drawing.PathStroker`). No unit within `Geometry` itself calls
`CornerRoundEffect`.
