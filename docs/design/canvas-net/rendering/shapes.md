### Shapes Unit Design

The `Shapes` class is a stateless static class of extension methods on `Rendering.Canvas` that
provide convenience fills and strokes for the three most common shapes: rectangles, rounded
rectangles, and circles.

#### Purpose

`Shapes` saves callers from hand-building a `Geometry.Path` for the most common primitive shapes.
Every helper builds its path via the corresponding `Geometry.Path` factory and dispatches the
fill or stroke through a given `Rendering.Canvas`, so callers get the same transform-aware
behavior as building the path manually.

#### Data Model

`Shapes` is a stateless static class with no instance fields and no supporting types of its own;
every helper takes plain `float` geometry parameters, an `Rgba32` fill color, and (for stroke
helpers) a `Drawing.StrokeStyle`.

#### Key Methods

##### Entry points

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

##### Behavior

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

#### Error Handling

Every helper throws `ArgumentNullException` on a null `Canvas` argument. Radius clamping
happens inside the underlying `Path.RoundRectangle` factory; the helpers themselves do not
validate `radius` further.

#### Dependencies

`Shapes` depends on this subsystem's own `Canvas` unit, the `Geometry` subsystem's `Path` unit
(`Rectangle`/`RoundRectangle`/`Circle` factories), and the `Canvas` (pixel-buffer) subsystem's
`Rgba32` unit, plus the `Drawing` subsystem's `StrokeStyle` unit for its stroke overloads.

#### Callers

`Shapes` is a public API entry point, invoked directly by consumers of the CanvasNet package as
extension methods on `Rendering.Canvas`. No unit within CanvasNet calls `Shapes` internally.
