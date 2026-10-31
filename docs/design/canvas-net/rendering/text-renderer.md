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
  kerning between adjacent glyphs.
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
2. The private glyph-placement walker yields, for every glyph that has an outline, the outline and
   its pen x-position.
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

##### Glyph-placement walker (private)

An iterator over the run that mirrors `MeasureText`'s arithmetic: for each rune it looks up the
glyph index, adds the scaled kerning against the previous glyph, yields `(outline, pen)` when the
outline has at least one subpath, then adds the scaled advance. Glyphs without outlines still
advance the pen and take part in kerning.

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
`UnitsPerEm` metrics), the `Geometry` subsystem's `Path` and `PathBuilder` units
(`PathBuilder.AppendPath` places each outline in `CreateTextPath`), the `Drawing` subsystem's
`PathFiller` unit, and `System.Numerics.Matrix3x2`/`System.Text.Rune` from the .NET Base Class
Library.

#### Callers

`TextRenderer` is a public API entry point, invoked directly by consumers of the CanvasNet
package. Within this repository the SVG codec (`DemaConsulting.CanvasNet.Svg`) calls
`CreateTextPath` to lay out text runs (see _SvgCodec Unit Design_), the chart renderer
(`DemaConsulting.CanvasNet.Charts`) calls `TruncateToWidth` to fit category and legend labels, and the
command-line tool's contact sheet calls `TruncateToWidth` to fit file-name labels.
