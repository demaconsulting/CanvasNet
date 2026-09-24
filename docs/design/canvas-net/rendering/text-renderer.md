### TextRenderer Unit Design

The `TextRenderer` class is a stateless static class that measures and renders TrueType text
against a `Rendering.Canvas`. Its two entry points are the static `MeasureText` method and the
`DrawText` extension method on `Rendering.Canvas`.

#### Types covered inline

The subsystem-scoped types `TextAlign` (enum with `Left`, `Center`, `Right`) and `TextMetrics`
(a readonly struct with `Width`, `Ascent`, `Descent` fields) are simple data carriers with no
independently testable behavior and are covered inline in this unit.

#### MeasureText

Given a string, a `TrueTypeFont`, and a pixel `size`, `MeasureText` returns a `TextMetrics`
where:

- `Width` is the sum of scaled advance widths across every glyph in the string, plus scaled
  kerning between adjacent glyphs.
- `Ascent` is the font's `Ascender` field scaled to pixels.
- `Descent` is the absolute value of the font's `Descender` field scaled to pixels.

`size` scales font design units to pixels by the ratio `size / font.UnitsPerEm`.

#### DrawText

Given a `Rendering.Canvas`, a string, an anchor `(x, y)` at the text baseline, a `TextAlign`,
a `TrueTypeFont`, a pixel `size`, and a fill `Rgba32`, `DrawText` walks the string as Unicode
runes and for each glyph:

1. Retrieves the outline `Path` from the font.
2. Computes the per-glyph transform
   `Matrix3x2.CreateScale(scale, -scale) * Matrix3x2.CreateTranslation(penX + alignOffset, y)`
   which converts y-up font design units to y-down pixel space and positions the pen.
3. Composes that with `canvas.CurrentTransform` so scene transforms apply to text as they do
   to filled paths.
4. Bakes the composed transform into the glyph path and dispatches to `canvas.FillPath` with
   the fill color.
5. Advances the pen by the scaled advance width plus scaled kerning with the next glyph.

`alignOffset` is `0` for `Left`, `-width / 2` for `Center`, and `-width` for `Right`, where
`width` is the pre-measured run width.

#### Validation

- `MeasureText` and `DrawText` throw `ArgumentNullException` on a null `text` or null `font`.
- `MeasureText` throws `ArgumentOutOfRangeException` on a non-finite `size`.
- `DrawText` throws `ArgumentOutOfRangeException` on an undefined `TextAlign`.
