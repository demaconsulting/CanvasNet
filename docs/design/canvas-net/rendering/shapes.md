### Shapes Unit Design

The `Shapes` class is a stateless static class of extension methods on `Rendering.Canvas` that
provide convenience fills and strokes for the three most common shapes: rectangles, rounded
rectangles, and circles.

#### Entry points

- `FillRect(this Canvas, float x, float y, float width, float height, Rgba32)` — fills an
  axis-aligned rectangle.
- `StrokeRect(this Canvas, float x, float y, float width, float height, StrokeStyle, Rgba32)` —
  strokes an axis-aligned rectangle.
- `FillRoundRect(this Canvas, float x, float y, float width, float height, float radius, Rgba32)`
  — fills a rounded rectangle, clamping `radius` to `min(width, height) / 2`.
- `StrokeRoundRect(this Canvas, float x, float y, float width, float height, float radius, StrokeStyle, Rgba32)`
  — strokes a rounded rectangle with the same clamping rule.
- `FillCircle(this Canvas, float centerX, float centerY, float radius, Rgba32)` — fills a circle.
- `StrokeCircle(this Canvas, float centerX, float centerY, float radius, StrokeStyle, Rgba32)` —
  strokes a circle.

#### Behavior

Every helper builds its path via the corresponding `Geometry.Path` factory
(`Path.Rectangle`, `Path.RoundRectangle`, `Path.Circle`) and dispatches the fill or stroke
through the given `Canvas`, so every helper honors the Canvas current transform automatically.
Degenerate inputs are handled per the underlying factory's own contract, which differs by
factory:

- `Rectangle` and `RoundRectangle` (and therefore `FillRect`/`StrokeRect`/`FillRoundRect`/
  `StrokeRoundRect`) produce an empty path — a no-op draw — only when `width` or `height` is
  non-positive. `RoundRectangle`'s `radius` parameter is independent of that check: a zero or
  negative `radius` is treated as zero and produces a normal (non-rounded) rectangle, not an
  empty path.
- `Circle` (and therefore `FillCircle`/`StrokeCircle`) produces an empty path — a no-op draw —
  whenever `radius` itself is non-positive, since a circle has no other dimension to fall back
  to.

#### Validation

Every helper throws `ArgumentNullException` on a null `Canvas` argument. Radius clamping
happens inside the underlying `Path.RoundRectangle` factory; the helpers themselves do not
validate `radius` further.
