<!-- cspell:ignore Rgba lerp precomputation -->

## TilePaint

![Drawing Structure](DrawingView.svg)

The `TilePaint` unit is the public `TilePaint` type - a pre-rendered one-cell tile `Surface` plus a
device-space `Transform` and a pattern-space `XStep`/`YStep` repeat pitch - and the internal
`TilePaintEvaluator` helper that resolves a tiled pattern's color per pixel by nearest-neighbor
sampling the wrapped-around tile bitmap. It has no independent externally visible behavior of its
own: it exists to be consumed by `PathFiller`'s tile-paint `Fill(Surface, Path, TilePaint,
FillRule, float)` overload (see _PathFiller Unit Design_, `path-filler.md`), mirroring
`GradientPaint`'s own public-type-plus-internal-evaluator shape (see `gradient-paint.md`).

### Purpose

`TilePaint` lets a caller paint a filled path by repeating a small pre-rendered bitmap ("tile")
across the filled region, instead of a single solid color or a smoothly varying gradient ramp -
the standard "tiling pattern" capability used by PDF `PatternType 1` tiling patterns (see
_PdfDocument Unit Design_, `pdf-document.md`, `/Pattern` Color Space section) and equivalent
tiled-brush concepts in other 2D graphics APIs. Unlike `GradientPaint`, which evaluates a
continuous mathematical function per pixel, `TilePaint` samples a caller-supplied, already-rendered
`Surface` - the caller (in practice, `PdfDocument`'s tiling-pattern cell renderer) is responsible
for producing that one-cell bitmap by whatever means its own pattern content requires; `TilePaint`
itself only repeats and samples it.

### Data Model

| Type | Description |
| --- | --- |
| `TilePaint` | Public sealed class: `Surface`, `Transform` (`Matrix3x2`), `XStep`, `YStep`. |
| `TilePaintEvaluator` | Internal static helper: resolves a `TilePaint`'s color per pixel row. |

`TilePaint` does not own its `Surface`'s lifetime: it does not dispose it, and the caller that
constructed the `TilePaint` remains responsible for disposing the underlying `Surface` once it is
no longer needed - mirroring how `Gradient` similarly owns no disposable resource of its own.
`XStep`/`YStep` may be negative (a legitimate PDF tiling-pattern value, per PDF 32000-1 §8.7.3.1,
meaning the tile repeats in the negative pattern-space axis direction) - only zero or a non-finite
value is rejected by the constructor; `TilePaintEvaluator` wraps around using the absolute value of
each step.

### Key Methods

#### TilePaint(surface, transform, xStep, yStep)

Validates and stores the tile paint's state:

- `surface` must not be `null`.
- `transform` must have every one of its six components finite.
- `xStep`/`yStep` must each be finite and non-zero; a negative value is accepted (see the Data
  Model section above).

#### TilePaint.WithTransform(transform)

Returns a new `TilePaint` with the same `Surface`/`XStep`/`YStep`, whose `Transform` is this tile
paint's own existing `Transform` composed with the supplied `transform` (row-vector convention
matching `Gradient.WithTransform`: this tile paint's existing `Transform` is applied first, then
the supplied `transform` is applied on top of that). Never mutates the original instance.

#### TilePaintEvaluator.CreatePlan / EvaluateRow (internal)

Mirrors `GradientEvaluator`'s own "per-fill precomputation, per-row evaluation" shape exactly:
`CreatePlan` computes the only per-fill-invariant quantity - the inverse of `TilePaint.Transform` -
exactly once, returning the reusable `TilePaintPlan`; `ScanlineRasterizer`'s tile-aware sweep calls
`CreatePlan` once per fill operation and passes the same plan into every `EvaluateRow` call for
every row of that fill. The per-pixel evaluation pipeline:

1. **Map the device-space pixel center into pattern space** via the precomputed inverse transform.
   If `TilePaint.Transform` was found to be singular (non-invertible) or to produce a non-finite
   inverse, the whole affected fill/row is left fully transparent instead - the "Degenerate
   Transform" case (see the policy-divergence note below).
2. **Wrap the pattern-space X/Y coordinates** modulo the absolute value of `XStep`/`YStep`, using
   floor-mod (never the CLR `%` operator, which is negative for a negative dividend - matching
   `GradientEvaluator`'s own documented `Repeat` floor-mod precedent), into the half-open range
   `[0, |XStep|) x [0, |YStep|)`.
3. **Map the wrapped pattern-space point into the tile `Surface`'s own pixel coordinate space** by
   the ratio of the tile surface's pixel dimensions to `XStep`/`YStep`, clamped into
   `[0, Width - 1] x [0, Height - 1]` (guarding a floating-point edge case landing exactly on the
   width/height boundary).
4. **Sample the tile `Surface` by nearest-neighbor** at the resolved integer pixel coordinate.
   Bilinear interpolation is explicitly out of scope.

**Degenerate Transform policy - deliberate divergence from `Gradient`.** When
`TilePaint.Transform` is not invertible (or its inverse has a non-finite component), every pixel in
the affected fill is left fully transparent (alpha zero), **not** flat-filled with any color. This
is a deliberate divergence from `Gradient`'s own "Degenerate Transform" policy (which flat-fills
with the last color stop - see `gradient-paint.md`): `TilePaint` has no stop/last-color concept at
all, so there is no equivalent fallback color to flat-fill with. Instead, this mirrors
`GradientEvaluator`'s own separate "no valid root" radial-gradient case, which likewise leaves
affected pixels fully transparent/unpainted. This is an intentional, documented design decision,
not an oversight or an accidental inconsistency with `GradientPaint`.

### Error Handling

`TilePaint`'s constructor throws:

- `ArgumentNullException` - when `surface` is `null`.
- `ArgumentOutOfRangeException` - when any component of `transform` is not finite, or when
  `xStep`/`yStep` is not finite or is zero.

`TilePaintEvaluator.CreatePlan` throws `ArgumentNullException` when `tile` is `null`; it performs
no further argument validation of its own.

### Dependencies

`TilePaint` depends on `System.Numerics.Vector2`/`Matrix3x2` (in-box BCL types) and the `Canvas`
subsystem's `Surface`/`Rgba32` (for the tile bitmap and its sampled pixel values). It introduces no
new runtime NuGet package.

### Callers

`TilePaint` is constructed directly by consumers of the CanvasNet package - in practice,
`DemaConsulting.CanvasNet.Pdf`'s `PdfDocument` tiling-pattern rendering, which renders a PDF
`PatternType 1` tiling pattern's content stream into a one-cell offscreen `Surface` and wraps it in
a `TilePaint` (see `pdf-document.md`, `/Pattern` Color Space section) - and passed to
`PathFiller.Fill(Surface, Path, TilePaint, FillRule, float)`. The internal `TilePaintEvaluator`
helper is invoked exclusively by `ScanlineRasterizer`'s tile-aware `Fill` overload (see
_PathFiller Unit Design_, `path-filler.md`). The unit is exercised by `TilePaintTests` and
`TilePaintEvaluatorTests`, and indirectly by `PathFillerTests`'/`ScanlineRasterizerTests`'
tile-paint-specific scenarios.
