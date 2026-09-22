<!-- cspell:ignore Rgba lerp -->

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
`LinearGradient` and `RadialGradient` may derive from it, via a `protected` constructor that
performs every validation and normalization step shared by both subtypes. This keeps the type
closed to caller-authored derivation (a third gradient kind would need library-level support
throughout the evaluation pipeline anyway) while still allowing exactly the two subtypes this unit
defines.

`Gradient`'s constructor takes a defensive copy of the supplied stops, **stable-sorted** ascending
by `GradientStop.Offset` (via `IReadOnlyList<GradientStop>.OrderBy`, which is a documented-stable
sort, unlike `Array.Sort`/`List<T>.Sort`). Stability is what makes a "hard stop" well-defined: of
two or more stops sharing the same offset, the caller-supplied relative order is preserved, so the
earlier-supplied stop's color is used for gradient parameter values approaching from below, and
the later-supplied stop's color for values at or beyond it (see `GradientEvaluator.ResolveColor`
below).

### Key Methods

#### Gradient(stops, spread, transform) (protected)

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
"both radii zero and both centers equal" configuration.

Both `LinearGradient` and `RadialGradient` accept a `Matrix3x2 transform = default` default-
parameter value rather than `Matrix3x2.Identity` directly, because `Matrix3x2.Identity` is not a
compile-time constant usable as a C# default parameter value; the constructor substitutes
`Matrix3x2.Identity` whenever the caller passes (or omits) the all-zero `default(Matrix3x2)`
value, before running validation.

#### GradientEvaluator.EvaluatePoint(gradient, point) / EvaluateRow(gradient, y, x, count, destination) (internal)

Resolves `gradient`'s color at a single point, or once per pixel center across a horizontal run of
pixels (used by `ScanlineRasterizer`'s gradient-aware sweep). The evaluation pipeline, per point:

1. **Invert `gradient.Transform`.** If it is singular (non-invertible) or produces a non-finite
   inverse, the whole gradient flat-fills with the last (post-sort) stop's color - the "Degenerate
   Transform" case (see the unifying degenerate-case policy below).
2. **Map the point into gradient-defining coordinates** via the inverted transform.
3. **Compute a raw (unbounded) gradient parameter `t`**:
   - For `LinearGradient`: project the gradient-space point onto the `End - Start` vector,
     `t = dot(point - Start, End - Start) / |End - Start|^2`. A vector with squared length below
     a small threshold is the "Zero-Length Linear Vector" degenerate case (flat-fill with the last
     stop's color).
   - For `RadialGradient`: solve the standard two-circle ("conical") gradient quadratic for the
     largest `t` (subject to the interpolated radius `radius(t) = lerp(StartRadius, EndRadius, t)`
     being non-negative) at which the interpolated circle `(center(t), radius(t))` - with
     `center(t) = lerp(StartCenter, EndCenter, t)` - passes through the point:
     `a = d.d - dr^2`, `b = -2(pd.d + StartRadius * dr)`, `c = pd.pd - StartRadius^2`, where
     `d = EndCenter - StartCenter`, `dr = EndRadius - StartRadius`, `pd = point - StartCenter`.
     If both radii are zero and both centers are equal, the family of circles never varies with
     `t` at all - a further degenerate case, flat-filling with the last stop's color. If no real
     root satisfies `radius(t) >= 0`, the point lies outside every circle the gradient's family
     ever sweeps through - it is left **unpainted** (returned with alpha zero), not flat-filled;
     this is distinct from every other degenerate case above (see the policy statement below).
4. **Fold the raw `t` into `[0, 1]` per `Spread`**: `Pad` clamps; `Repeat` floor-mods (`t -
   floor(t)`, never a naive `%`, which is negative for a negative dividend in C#); `Reflect` folds
   into a period-2 triangle wave.
5. **Resolve a color from the sorted stop list** at the folded `t`: a single-stop gradient always
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

`Gradient`'s protected constructor (invoked by both `LinearGradient` and `RadialGradient`) throws:

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
`PathFiller.Fill(Surface, Path, Gradient, FillRule, float)`. The internal `GradientEvaluator`
helper is invoked exclusively by `ScanlineRasterizer`'s gradient-aware `Fill` overload (see
_PathFiller Unit Design_, `path-filler.md`). The unit is exercised by `GradientStopTests`,
`LinearGradientTests`, `RadialGradientTests`, and `GradientEvaluatorTests`, and indirectly by
`PathFillerTests`'/`ScanlineRasterizerTests`' gradient-specific scenarios.
