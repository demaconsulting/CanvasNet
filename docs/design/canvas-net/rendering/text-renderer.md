### TextRenderer Unit Design

The `TextRenderer` class is a stateless static class that measures and renders TrueType text
against a `Rendering.Canvas`, and converts text into outline geometry. Its entry points are the
static `MeasureText`, `CreateTextPath`, and `TruncateToWidth` methods and the `DrawText` extension
method on `Rendering.Canvas`.

#### Purpose

`TextRenderer` composes `Fonts.TrueTypeFont` glyph/metric queries with `Rendering.Canvas` fills
to measure and draw strings of text, so callers do not need to hand-write glyph iteration,
advance-width/kerning accumulation, or alignment-offset math themselves. `CreateTextPath` exposes
the same layout as a single `Geometry.Path`, for renderers that need text as geometry (to apply
their own accumulated transform later, or to build clips and masks) and previously each kept
their own copy of the glyph-placement loop. `TruncateToWidth` shortens a run to a width with an
ellipsis, a rule that the chart renderer and the command-line contact sheet each used to keep a
private copy of.

#### Data Model

`TextRenderer` is a stateless static class with no instance fields. The subsystem-scoped types
`TextAlign` (enum with `Left`, `Center`, `Right`) and `TextMetrics` (a readonly struct with
`Width`, `Ascent`, `Descent` fields) are simple data carriers with no independently testable
behavior and are covered inline in this unit.

#### Key Methods

##### MeasureText

Given a string, a `TrueTypeFont`, and a pixel `size`, `MeasureText` returns a `TextMetrics`
where:

- `Width` is the sum of scaled advance widths across every glyph in the string, plus scaled
  kerning between adjacent glyphs of the same font. A character the font lacks is measured with
  the advance of its bundled fallback glyph (see the run walker below).
- `Ascent` is the font's `Ascender` field scaled to pixels.
- `Descent` is the absolute value of the font's `Descender` field scaled to pixels.

`size` scales font design units to pixels by the ratio `size / font.UnitsPerEm`.

##### DrawText

Given a `Rendering.Canvas`, a string, an anchor `(x, y)` at the text baseline, a `TextAlign`,
a `TrueTypeFont`, a pixel `size`, and a fill `Rgba32`, `DrawText` walks the string as Unicode
runes (through the private glyph-placement walker described below) and for each glyph with an
outline:

1. Retrieves the outline `Path` from the font.
2. Computes the per-glyph transform
   `Matrix3x2.CreateScale(scale, -scale) * Matrix3x2.CreateTranslation(penX + alignOffset, y)`
   (the scale being that of the font that supplies the glyph)
   which converts y-up font design units to y-down pixel space and positions the pen.
3. Composes that with `canvas.CurrentTransform` so scene transforms apply to text as they do
   to filled paths.
4. Bakes the composed transform into the glyph path and dispatches directly to
   `Drawing.PathFiller.Fill` with the fill color, rather than `canvas.FillPath` - the glyph
   path is already in transformed/surface space at this point, so routing it back through
   `canvas.FillPath` would apply the canvas's current transform a second time.
5. Advances the pen by the scaled advance width plus scaled kerning with the next glyph.

`alignOffset` is `0` for `Left`, `-width / 2` for `Center`, and `-width` for `Right`, where
`width` is the pre-measured run width.

##### CreateTextPath

Given a string, a `TrueTypeFont`, a pixel `size`, an anchor `(x, y)` at the text baseline, and a
`TextAlign` (default `Left`), `CreateTextPath` returns one combined `Geometry.Path`:

1. The run width is measured with `MeasureText` and the alignment offset is computed exactly as
   for `DrawText`.
2. The private run walker yields, for every glyph that has an outline, the outline, its pen
   x-position, and the scale of the font that supplies it.
3. Each outline is appended to one `PathBuilder` with
   `AppendPath(outline, Matrix3x2.CreateScale(scale, -scale) * Matrix3x2.CreateTranslation(pen, y))`.
   No canvas transform is involved: the caller applies its own transform to the returned path.
4. The builder's path is returned; text with no contour data (empty or whitespace) yields
   `Path.Empty`.

`CreateTextPath` is deliberately **not** the implementation of `DrawText`: `DrawText` fills every
glyph separately (so overlapping glyphs composite one after another) and composes the canvas
transform into each glyph matrix, which is bit-for-bit what the existing tests pin, whereas
`CreateTextPath` produces one merged shape for a single fill-rule evaluation. Both share the
private walker, so the rune iteration, kerning-before-advance order, and float accumulation order
cannot drift apart.

##### TruncateToWidth

Given a string, a `TrueTypeFont`, a pixel `size`, a width budget `maxWidth`, and an `ellipsis`
(default U+2026), `TruncateToWidth` returns the text, shortened when needed:

1. If `MeasureText(text).Width <= maxWidth`, the text is returned unchanged (the same instance).
2. Otherwise the text is normalized to whole runes (a lone surrogate becomes U+FFFD) and the
   end offset of every rune is recorded, so a candidate beginning is always a slice that ends on a rune
   boundary and a surrogate pair is never split.
3. Beginnings are tried from `runeCount - 1` runes down to zero runes (the ellipsis alone); the first
   `beginning + ellipsis` whose measured width is at most `maxWidth` is returned.
4. If none fits, the empty string is returned. A `NaN` or negative budget therefore yields the empty string
   for non-empty text.

The scan is deliberately linear, not a binary search: kerning makes the measured width of a beginning
non-monotonic in its length, so only a scan from the longest beginning returns the longest one that fits.
The cost is quadratic in the text length (each candidate is measured in full), acceptable for labels.
The size is validated up front, independent of the budget, so a bad size is reported even for text that
would fit. The consumers (`ChartRenderer`, `ContactSheet`) pass the ASCII ellipsis `...` explicitly; the
shared default is the single character U+2026, a pure API-design choice.

##### Run walker (private, `EnumerateRun` and `EnumerateGlyphPlacements`)

`EnumerateRun` is the single home of the rune iteration, the glyph fallback, the
kerning-before-advance order, and the float accumulation order. For each rune it asks
`GlyphFallback.Resolve(font, rune)` for the font and glyph that draw it. For a character the primary
font maps this is the primary font itself with its own glyph, and nothing else is loaded; for a
character it lacks it is the first bundled Noto font that has a glyph (or the primary font's glyph 0,
its missing glyph, when none has). The scale is `size / glyphFont.UnitsPerEm` - the Noto fonts have
1000 units per em and Liberation 2048, so the scale is that of the font that supplies the glyph.
Kerning with the previous glyph is applied only when the previous glyph came from the same
`TrueTypeFont` instance, since glyph indices of different fonts are unrelated. It yields
`(glyphFont, glyphIndex, scale, pen)` with the pen after kerning, then adds the scaled advance.

`MeasureText` takes the pen after the last glyph plus its advance (a run starting at zero), `DrawText`
and `CreateTextPath` use `EnumerateGlyphPlacements`, which filters `EnumerateRun` to the glyphs that have an
outline and carries each glyph's scale. All of them therefore agree, and a run that the primary font
covers completely performs exactly the arithmetic of the single-font walker that existed before (frozen
by a digest test over the measured widths and the drawn pixels). Ascent and descent are always those of
the primary font. Glyphs without outlines still advance the pen and take part in kerning.

##### Glyph fallback behavior

The fallback is deterministic and machine independent: no installed font is consulted. It is regular
style only (a bold or italic request is not synthesized for a fallback glyph), there is no shaping or
combining-mark attachment (as before), and a character that no bundled font covers draws the primary
font's missing-glyph outline. `TextRenderer` has no diagnostics channel; a caller that must report such
characters uses `GlyphFallback.FindMissing`.

#### Error Handling

- `MeasureText`, `DrawText`, `CreateTextPath`, and `TruncateToWidth` throw `ArgumentNullException` on a null
  `text` or null `font`; `TruncateToWidth` also on a null `ellipsis`.
- `MeasureText`, `DrawText`, `CreateTextPath`, and `TruncateToWidth` throw `ArgumentOutOfRangeException` on a
  size that is not finite and positive.
- `DrawText` and `CreateTextPath` throw `ArgumentOutOfRangeException` on an undefined `TextAlign`.
- `TruncateToWidth` never throws for the width budget: a `NaN` or negative budget is a budget nothing fits.

#### Dependencies

`TextRenderer` depends on this subsystem's own `Canvas` unit, the `Fonts` subsystem's
`TrueTypeFont` unit (glyph outlines, advance widths, kerning, and `Ascender`/`Descender`/
`UnitsPerEm` metrics) and `GlyphFallback` unit (the bundled fallback chain), the `Geometry` subsystem's `Path` and
`PathBuilder` units
(`PathBuilder.AppendPath` places each outline in `CreateTextPath`), the `Drawing` subsystem's
`PathFiller` unit, and `System.Numerics.Matrix3x2`/`System.Text.Rune` from the .NET Base Class
Library.

#### Callers

`TextRenderer` is a public API entry point, invoked directly by consumers of the CanvasNet
package. Within this repository the SVG codec (`DemaConsulting.CanvasNet.Svg`) calls
`CreateTextPath` to lay out text runs (see _SvgCodec Unit Design_), the chart renderer
(`DemaConsulting.CanvasNet.Charts`) calls `TruncateToWidth` to fit category and legend labels, and the
command-line tool's contact sheet calls `TruncateToWidth` to fit file-name labels.
