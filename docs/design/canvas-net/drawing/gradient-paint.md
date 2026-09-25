<!-- cspell:ignore Rgba lerp precomputation -->

## GradientPaint

![Drawing Structure](DrawingView.svg)

The `GradientPaint` unit is the public `Gradient` abstract base type and its `LinearGradient`/
`RadialGradient` subtypes, the supporting `GradientStop`/`GradientSpread` types, and the internal
`GradientEvaluator` helper that resolves a gradient's color at a point or across a pixel row. It
has no independent externally visible behavior of its own: it exists to be consumed by
`PathFiller`'s gradient `Fill(Surface, Path, Gradient, FillRule, float)` overload (see _PathFiller
Unit Design_, `path-filler.md`).

### Purpose

`GradientPaint` lets a caller paint a filled path with a smoothly (or sharply) varying color ramp
instead of a single solid color - the standard "gradient" capability of every established 2D
vector graphics API (SVG, CSS, Direct2D, Skia). It supports both the linear ramp (`LinearGradient`,
varying along a straight line between two points) and radial ramp (`RadialGradient`, varying
between two independently positioned and sized circles - the general "two-circle"/"focal" model)
gradient kinds, an arbitrary ordered set of color stops, three spread behaviors describing what
happens beyond the gradient's own defining extent, and an optional transform mapping the
gradient's own coordinates into the caller's path coordinate space. Rendering a gradient reuses
`PathFiller`/`ScanlineRasterizer`'s existing antialiased scanline-coverage pipeline unchanged;
`GradientPaint` only supplies a per-pixel color source in place of a single constant color.

### Data Model

| Type | Description |
| --- | --- |
| `GradientSpread` | Public enum: `Pad`, `Reflect`, `Repeat` - selects out-of-range ramp behavior. |
| `GradientStop` | Public readonly struct: an `Offset` (`[0, 1]`) and a `Color` (`Rgba32`). |
| `Gradient` | Public abstract base class: `Stops`, `Spread`, `Transform` (`Matrix3x2`). |
| `LinearGradient` | Public sealed subtype: `Start`/`End` points (gradient-space `Vector2`). |
| `RadialGradient` | Public sealed subtype: `StartCenter`/`StartRadius`/`EndCenter`/`EndRadius`. |
| `GradientEvaluator` | Internal static helper: resolves a `Gradient`'s color per point/row. |

`Gradient` is deliberately not sealed but has no public constructor of its own - only
`LinearGradient` and `RadialGradient` may derive from it, via a `private protected` constructor
that performs every validation and normalization step shared by both subtypes. `private protected`
(rather than `protected`) makes the hierarchy closed in the strict sense: only types declared in
this same assembly may derive from `Gradient` at all, not merely any externally-authored type that
happens to call the constructor via a `protected`-visible in-assembly proxy. This matches
`GradientEvaluator`'s exhaustive pattern match over `LinearGradient`/`RadialGradient` only (a third
gradient kind would need library-level support throughout the evaluation pipeline anyway, so
external derivation was never actually supported - `private protected` makes that explicit instead
of merely implied).

`Gradient`'s constructor takes a defensive copy of the supplied stops, **stable-sorted** ascending
by `GradientStop.Offset` (via `IReadOnlyList<GradientStop>.OrderBy`, which is a documented-stable
sort, unlike `Array.Sort`/`List<T>.Sort`). Stability is what makes a "hard stop" well-defined: of
two or more stops sharing the same offset, the caller-supplied relative order is preserved, so the
earlier-supplied stop's color is used for gradient parameter values approaching from below, and
the later-supplied stop's color for values at or beyond it (see `GradientEvaluator.ResolveColor`
below).

### Key Methods

#### Gradient(stops, spread, transform) (private protected)

Validates and stores the state shared by every gradient subtype:

- `stops` must not be `null` and must contain at least one entry - an empty gradient has no color
  to paint with.
- `spread` must be a defined `GradientSpread` value.
- `transform` must have every one of its six components finite. `Transform` does not itself have
  to be invertible - a non-invertible (singular) transform is a defined evaluation-time
  degenerate case (see below), not a constructor error, because a caller-composed transform could
  legitimately become momentarily singular (for example, mid-animation) without that necessarily
  being a programming error.

#### LinearGradient(start, end, stops, spread, transform)

Defines a ramp varying linearly along the vector from `start` to `end` (in gradient-defining
coordinates). `start` and `end` must each have finite components; `end` equal to `start` (a
zero-length gradient vector) is accepted at construction - it is a defined degenerate evaluation
case (flat-fill with the last stop's color), not a constructor error, since a caller could compose
this state programmatically (for example, an animated gradient briefly collapsing to a point).

#### RadialGradient(startCenter, startRadius, endCenter, endRadius, stops, spread, transform)

Defines a ramp varying between two independently positioned and sized circles - the general
"two-circle" (also called "focal" or "conical") gradient model used by SVG/CSS radial gradients,
Direct2D's radial brush with a separate origin, and Skia's `TwoPointConical` shader. This general
model subsumes both the simpler single-circle case (`StartRadius` zero, `StartCenter` equal to
`EndCenter`) and the "focal point" case (`StartRadius` zero, `StartCenter` different from
`EndCenter`) without needing a second public gradient type for either. Both centers must have
finite components; both radii must be finite and non-negative. Every combination of equal/
different centers and equal/zero/different radii is accepted at construction - each is a defined,
evaluation-time case (see `GradientEvaluator` below), including the fully degenerate
"both centers equal and both radii equal" configuration (which subsumes the "both radii zero"
sub-case, since a single point-radius circle sharing both centers is likewise a swept family that
never actually varies with `t`).

Both `LinearGradient` and `RadialGradient` accept a `Matrix3x2? transform = null` optional
parameter rather than defaulting to `Matrix3x2.Identity` directly, because `Matrix3x2.Identity` is
not a compile-time constant usable as a C# default parameter value. The constructor substitutes
`Matrix3x2.Identity` only when the caller omits the argument (or explicitly passes `null`);
an explicitly-supplied `Matrix3x2` value - including the all-zero `default(Matrix3x2)` value,
which is a legitimate (if singular) transform a caller might deliberately want to test/use - is
preserved exactly as given and validated/stored unchanged. A plain `Matrix3x2 transform = default`
parameter cannot distinguish "the caller omitted the argument" from "the caller explicitly passed
the all-zero matrix"; the nullable parameter removes that ambiguity without any special-casing.

#### Gradient.WithTransform(transform) (abstract; overridden by LinearGradient/RadialGradient)

Returns a new gradient of the same runtime type and with the same `Stops`/`Spread`/geometry
(`Start`/`End` for `LinearGradient`; `StartCenter`/`StartRadius`/`EndCenter`/`EndRadius` for
`RadialGradient`), whose `Transform` is this gradient's own existing `Transform` composed with the
supplied `transform` (row-vector convention matching `Geometry.Path.Transform(Matrix3x2)`: this
gradient's existing `Transform` is applied first - mapping gradient-defining coordinates into the
caller's original path-local coordinate space - then the supplied `transform` is applied on top of
that, i.e. `Transform * transform`). This lets a caller that composes a gradient-painted fill with
an outer coordinate-space transform (for example `Rendering.Canvas`'s current transform - see
`Canvas.FillPath(Path, Gradient, FillRule)` in `canvas.md`) fold that outer transform into the
gradient's own coordinate mapping so the gradient continues to track the transformed shape, rather
than remaining fixed in the shape's old, untransformed local frame. `WithTransform` never mutates
the original instance - it returns a new gradient, preserving `Gradient`'s existing immutability
guarantees.

#### GradientEvaluator.CreatePlan / EvaluatePoint / EvaluateRow (internal)

`CreatePlan` computes every quantity that depends only on `gradient` itself and not on the point
being evaluated - the inverted `Transform`, the linear gradient's direction vector and its squared
length, and the radial gradient's two-circle quadratic coefficients (`d`, `dr`, `a`, and the
fully-degenerate check) - exactly **once**, returning the reusable `GradientPlan`. `EvaluatePoint`
calls `CreatePlan` once per point evaluated (there is nothing to share across a single-point
evaluation). `ScanlineRasterizer`'s gradient-aware sweep instead calls `CreatePlan` exactly
**once per fill operation**, before its row-iteration loop begins, and passes the same plan into
every `EvaluateRow` call for every row of that fill - not once per row. This matters because a
fill's row-iteration loop can call `EvaluateRow` many times over (once per scanline), and each
`EvaluateRow` call internally evaluates once per covered pixel: rebuilding the plan (a matrix
inverse or the radial coefficients) on every row - let alone every pixel - would repeat the same
fill-invariant work a whole fill's row count (and pixel width) of times over. When the plan
determines the transform is singular, the whole row is flat-filled directly without even entering
the per-pixel loop. The evaluation pipeline, per point:

1. **Map the point into gradient-defining coordinates** via the (already-inverted, precomputed)
   inverse transform. If the transform was found to be singular (non-invertible) or to produce a
   non-finite inverse during precomputation, the whole gradient flat-fills with the last
   (post-sort) stop's color instead - the "Degenerate Transform" case (see the unifying
   degenerate-case policy below).
2. **Compute a raw (unbounded) gradient parameter `t`**:
   - For `LinearGradient`: project the gradient-space point onto the (precomputed) `End - Start`
     vector, `t = dot(point - Start, End - Start) / |End - Start|^2`. A vector with squared length
     below a small threshold is the "Zero-Length Linear Vector" degenerate case (flat-fill with
     the last stop's color).
   - For `RadialGradient`: solve the standard two-circle ("conical") gradient quadratic for `t`
     (subject to the interpolated radius `radius(t) = lerp(StartRadius, EndRadius, t)` being
     non-negative) at which the interpolated circle `(center(t), radius(t))` - with
     `center(t) = lerp(StartCenter, EndCenter, t)` - passes through the point:
     `a = d.d - dr^2`, `b = -2(pd.d + StartRadius * dr)`, `c = pd.pd - StartRadius^2`, where
     `d = EndCenter - StartCenter`, `dr = EndRadius - StartRadius`, `pd = point - StartCenter`
     (`d`, `dr`, and `a` are all precomputed per-fill, not per-point). If `StartCenter` equals
     `EndCenter` and `StartRadius` equals `EndRadius`, the family of circles never varies with `t`
     at all - regardless of whether that shared radius is zero or nonzero - a further degenerate
     case, flat-filling with the last stop's color. If no real root satisfies `radius(t) >= 0`,
     the point lies outside every circle the gradient's family ever sweeps through - it is left
     **unpainted** (returned with alpha zero), not flat-filled; this is distinct from every other
     degenerate case above (see the policy statement below).

   **Root selection when both quadratic roots are valid.** When the start and end circles
   intersect (neither is nested entirely inside the other, i.e. `a > 0`) a point can lie on the
   swept boundary of both circles' family in two different ways, giving two real roots that both
   satisfy `radius(t) >= 0`. Naively always choosing the larger of the two roots (the simplified
   rule commonly quoted for two-circle/conical radial gradients) can pick the "wrong" root at the
   start/end circle boundaries themselves, contradicting this unit's own documented contract that
   a point on or before the start circle resolves to the first stop's color and a point on or
   after the end circle resolves to the last stop's color (and, by extension, breaking `Repeat`/
   `Reflect` spread semantics at those boundaries, since spread folds the raw `t` before resolving
   a color). The correct, boundary-preserving choice depends on whether the swept radius is
   growing or shrinking from start to end: when both roots are valid, this unit selects the
   **smaller** root if `dr > 0` (radius growing, so the start circle is the "inner" boundary of
   the swept family and the smaller `t` is the one that actually lies on it) and the **larger**
   root if `dr < 0` (radius shrinking, mirroring the same reasoning with start/end reversed); when
   `dr == 0` the two roots are only ever a single repeated root in practice (a `dr == 0` two-circle
   gradient with `a > 0` is otherwise degenerate per the check above), so the larger-root choice is
   retained unchanged. This matches Skia's actual (not simplified-prose) two-point-conical
   algorithm, which resolves the sign ambiguity deterministically from the same `dr`-sign
   reasoning, and preserves the boundary contract described above.

   **Tangent-family fully-degenerate case (`a ≈ 0`, `b ≈ 0`, `c ≈ 0`).** When `a` is near-zero the
   quadratic degrades to the linear equation `b*t + c = 0`; if `b` is also near-zero, that linear
   equation itself degenerates. Ordinarily (`c` not near-zero) this means no root exists at all -
   correctly left unpainted. But when `c` is _also_ near-zero, the equation is `0 = 0`: every `t`
   is technically a valid root, because the evaluated point lies exactly on the swept family's
   boundary circle at every `t` in this tangent-degenerate configuration (for example, a point
   diametrically opposite the tangency point of two internally-tangent start/end circles). Rather
   than leaving such a point unpainted (there being no single numerically-selected root to prefer),
   this unit extends the same `dr`-sign boundary-conforming convention used for the
   both-roots-valid case above: it resolves to the start-side endpoint (`t = 0`) when `dr > 0`
   (growing) and the end-side endpoint (`t = 1`) when `dr <= 0` (shrinking or unchanging).
3. **Fold the raw `t` into `[0, 1]` per `Spread`**: `Pad` clamps; `Repeat` floor-mods (`t -
   floor(t)`, never a naive `%`, which is negative for a negative dividend in C#); `Reflect` folds
   into a period-2 triangle wave.
4. **Resolve a color from the sorted stop list** at the folded `t`: a single-stop gradient always
   returns that stop's color (a single-stop gradient is a solid color); `t` at or before the first
   stop resolves to the first stop's color, at or after the last stop to the last stop's color;
   otherwise the bracketing consecutive stop pair is found and interpolated **in premultiplied
   alpha space**, then unpremultiplied back to straight alpha using the same "alpha == 0 implies
   R = G = B = 0" convention documented by `Surface.UnpremultiplyAlpha`. A pair of stops sharing
   the same offset (a "hard stop") resolves to the later stop's color outright, avoiding a `0/0`
   division.

**Why premultiplied-alpha interpolation.** Interpolating `R`/`G`/`B`/`A` independently
("straight-alpha" interpolation) visibly passes through a washed-out, desaturated band partway
through a transition between, for example, opaque red and fully transparent white - because the
straight-alpha midpoint blends the _fully opaque_ red and white color channels evenly regardless
of how transparent the result should be. Interpolating in premultiplied space (where each color
channel is pre-multiplied by its own alpha before interpolating, then divided back out afterward)
avoids this: a channel that is fading toward zero alpha also fades its premultiplied color
contribution toward zero, so the interpolated hue stays visually consistent with the more opaque
endpoint. This is the same fix used by every established compositing/2D-graphics system.

**Degenerate-case policy (single unifying rule).** Every geometric degenerate case that cannot
define a meaningful gradient direction - a singular `Transform`, a zero-length `LinearGradient`
vector, and a `RadialGradient` whose two circles never change with `t` at all - resolves to
flat-filling with the last stop's color. This is distinct from, and does not apply to, the
two-circle radial gradient's "no valid root" region (a legitimate, spatially varying part of an
otherwise well-defined two-circle gradient), which is instead left **unpainted** (alpha zero) -
matching the convention established by other 2D graphics systems for this case.

**Double-precision internal computation.** Both the linear projection and the radial quadratic
solve are performed entirely in `double` precision, narrowed back to `float`/`byte` only at the
very end, even though the public API is `float`-based (`System.Numerics.Vector2`). This mirrors
the same numerical safeguard `PathStroker`'s `StrokeOutliner` already applies to its own geometry:
`float` arithmetic on individually-representable values can still overflow an intermediate squared
distance or quadratic coefficient even when the true mathematical result would not, silently
producing a degenerate result rather than a visible error.

### Error Handling

`Gradient`'s `private protected` constructor (invoked by both `LinearGradient` and
`RadialGradient`) throws:

- `ArgumentNullException` - when `stops` is `null`.
- `ArgumentException` - when `stops` is empty.
- `ArgumentOutOfRangeException` - when `spread` is not a defined `GradientSpread` value, or when
  any component of `transform` is not finite.

`LinearGradient`'s constructor additionally throws `ArgumentOutOfRangeException` when `start` or
`end` has a non-finite component. `RadialGradient`'s constructor additionally throws
`ArgumentOutOfRangeException` when `startCenter`/`endCenter` has a non-finite component, or when
`startRadius`/`endRadius` is not finite or is negative. `GradientStop`'s constructor throws
`ArgumentOutOfRangeException` when its offset is outside `[0, 1]` or is not finite.

`GradientEvaluator` assumes it only ever receives an already-validated `Gradient` instance; it
performs no further argument validation of its own beyond null-checking the `gradient` reference
itself (`ArgumentNullException`).

### Dependencies

`GradientPaint` depends on `System.Numerics.Vector2`/`Matrix3x2` (in-box BCL types) and the
`Canvas` subsystem's `Rgba32` (for `GradientStop.Color` and `GradientEvaluator`'s output) and
`Surface.UnpremultiplyAlpha` convention (matched, not called directly, by
`GradientEvaluator.LerpPremultiplied`). It introduces no new runtime NuGet package.

### Callers

`GradientPaint`'s public types (`Gradient`, `LinearGradient`, `RadialGradient`, `GradientStop`,
`GradientSpread`) are constructed directly by consumers of the CanvasNet package and passed to
`PathFiller.Fill(Surface, Path, Gradient, FillRule, float)` or `Rendering.Canvas`'s
gradient-paint `FillPath(Path, Gradient, FillRule)` overload (see `canvas.md`), which calls
`Gradient.WithTransform` to fold its current transform into the gradient before filling. The
internal `GradientEvaluator` helper is invoked exclusively by `ScanlineRasterizer`'s
gradient-aware `Fill` overload (see _PathFiller Unit Design_, `path-filler.md`). The unit is
exercised by `GradientStopTests`, `LinearGradientTests`, `RadialGradientTests`, and
`GradientEvaluatorTests`, and indirectly by `PathFillerTests`'/`ScanlineRasterizerTests`'
gradient-specific scenarios.
