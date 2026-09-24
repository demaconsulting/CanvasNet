### Shapes Unit Design

The `Shapes` class is a stateless static class of extension methods on `Rendering.Canvas` that
provide convenience fills and strokes for the three most common shapes: rectangles, rounded
rectangles, and circles.

#### Entry points

- `FillRect(this Canvas, Rect, Rgba32)` — fills an axis-aligned rectangle.
- `StrokeRect(this Canvas, Rect, StrokeStyle, Rgba32)` — strokes an axis-aligned rectangle.
- `FillRoundRect(this Canvas, Rect, float radius, Rgba32)` — fills a rounded rectangle,
  clamping `radius` to `min(width, height) / 2`.
- `StrokeRoundRect(this Canvas, Rect, float radius, StrokeStyle, Rgba32)` — strokes a rounded
  rectangle with the same clamping rule.
- `FillCircle(this Canvas, Vector2 center, float radius, Rgba32)` — fills a circle.
- `StrokeCircle(this Canvas, Vector2 center, float radius, StrokeStyle, Rgba32)` — strokes a
  circle.

#### Behavior

Every helper builds its path via the corresponding `Geometry.Path` factory
(`Path.Rectangle`, `Path.RoundRectangle`, `Path.Circle`) and dispatches the fill or stroke
through the given `Canvas`, so every helper honors the Canvas current transform automatically.
Degenerate zero-size or non-positive-radius inputs produce an empty path per the underlying
factory contract, resulting in a no-op draw.

#### Validation

Every helper throws `ArgumentNullException` on a null `Canvas` argument. Radius clamping
happens inside the underlying `Path.RoundRectangle` factory; the helpers themselves do not
validate `radius` further.
