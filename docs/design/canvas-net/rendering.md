## Rendering

The `Rendering` subsystem is the sixth software subsystem in CanvasNet. It groups higher-level
rendering primitives that compose the `Canvas`, `Geometry`, `Drawing`, and `Fonts` subsystems
into a transform-aware surface API and standard shape and text helpers.

### Purpose

The `Rendering` subsystem provides an ergonomic layer over the low-level drawing primitives. It
wraps a `Canvas.Surface` with an affine transform stack (`Save` / `Restore` / `Translate` /
`RotateDegrees`) so callers can compose scenes without manually rewriting coordinates, exposes
convenience helpers to fill or stroke rectangles, rounded rectangles, and circles under the
current transform, and renders TrueType text with alignment and kerning against a `TrueTypeFont`.
At the identity transform, filled and stroked paths dispatched through the `Rendering.Canvas`
produce output byte-identical to direct calls into `Drawing.PathFiller` / `Drawing.PathStroker`.

### Units

- **Canvas** — the sole stateful unit of the subsystem. Wraps a `Canvas.Surface`, owns the
  transform stack, and bakes the current transform into every fill or stroke; see _Canvas Unit
  Design_ (`rendering/canvas.md`).
- **TextRenderer** — a stateless static class exposing `MeasureText` and the `DrawText`
  extension on `Rendering.Canvas`; see _TextRenderer Unit Design_ (`rendering/text-renderer.md`).
- **Shapes** — a stateless static class of `FillRect` / `StrokeRect` / `FillRoundRect` /
  `StrokeRoundRect` / `FillCircle` / `StrokeCircle` extension methods on `Rendering.Canvas`;
  see _Shapes Unit Design_ (`rendering/shapes.md`).

The subsystem-scoped enum `TextAlign` and value type `TextMetrics` are covered inline in the
`TextRenderer` unit design.

### Dependencies

The `Rendering` subsystem depends on:

- `Canvas` — for the underlying `Surface` and `Rgba32` types.
- `Geometry` — for `Path` (including the new `Transform` operation and `Rectangle`,
  `RoundRectangle`, `Circle` factories) and `PathBuilder`.
- `Drawing` — for `PathFiller`, `PathStroker`, and `StrokeStyle`.
- `Fonts` — for `TrueTypeFont` and glyph outlines.

### Callers

The subsystem is a public API entry point, called directly by consumers of the CanvasNet
package. It is exercised by dedicated `Rendering` unit tests and forms the recommended surface
for building composed scenes on top of the low-level drawing pipeline.
