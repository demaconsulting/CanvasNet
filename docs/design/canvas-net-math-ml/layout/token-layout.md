### TokenLayout

![Layout Structure](MathMlLayoutView.svg)

The `TokenLayout` unit is a software unit of the `Layout` subsystem. Its source files are `TokenLayout.cs`, which
defines the internal sealed class `TokenLayout`, `MathVariants.cs`, which defines the enum `MathVariant` and the static
partial class `MathVariants`, and the generated `MathVariants.Data.cs`, which holds the italic and `mathvariant`
mapping pairs.

#### Purpose

`TokenLayout` lays out the token elements (`mi`, `mn`, `mo`, `mtext`, `ms`, `mspace`) and text written directly inside a
layout element: the transformed characters, their glyphs, the box of the token, and the spacing of an operator. The
unit's behavior is specified by the `CanvasNetMathMl-Layout-TokenLayout-*` requirements in
`docs/reqstream/canvas-net-math-ml/layout/token-layout.yaml`.

#### Data Model

- `MathVariant` - the 18 `mathvariant` values of MathML 3: `Normal`, `Bold`, `Italic`, `BoldItalic`, `DoubleStruck`,
  `BoldFraktur`, `Script`, `BoldScript`, `Fraktur`, `SansSerif`, `BoldSansSerif`, `SansSerifItalic`,
  `SansSerifBoldItalic`, `Monospace`, `Initial`, `Tailed`, `Looped`, and `Stretched`.
- The tables (built once, frozen) - the value names (ASCII case-insensitive); the automatic italic table of MathML Core
  Appendix C.1 (Latin letters, the dotless i and j, the Greek letters, nabla, and the partial differential); and the
  table of the other values by value and base code point, generated from the `<font>` decompositions of UnicodeData.txt
  (a Mathematical Alphanumeric Symbol wins over a Letterlike Symbol of the same style and base, so the letterlike
  symbols fill the holes, such as U+2102 for the double-struck C and U+210E for the italic h).
- `DefaultOperatorSpace` - 5/18 em, the spacing of the `Default` category of the operator dictionary on each side.
- Per call: the math font, the style resolver, and the report sink.

#### Key Methods

- `LayoutToken(element, style)` - `mspace` is a blank box; `ms` draws its text as written, without quotes; every other
  token lays out its text content (`LayoutText`) with the automatic italic for an `mi` of a single character; an `mo`
  becomes an embellished operator with `lspace` and `rspace` resolved against the style (a percentage of the default
  spacing) or the default spacing; `mtext` and `mspace` are space-like.
- `LayoutText(text, style, autoItalic, location)` - transforms the characters (`Transform`), resolves each with the math
  font, and places the glyphs by their advances (no kerning, no glyph substitution): an invisible format character (the
  invisible operators U+2061 to U+2064) takes no space and draws nothing; a white-space character advances by its width;
  a character that no bundled font has is drawn as the missing-glyph box of the math font and reported. The box is the
  sum of the advances wide, and its ascent and descent are those of the ink of the glyphs rounded out to whole CSS
  pixels (with a tolerance of 1e-6), also for a text with a space, as Chrome lays tokens out. A single glyph gives the
  box its italic correction and top accent attachment from the `MATH` table.
- `Transform(text, variant, autoItalic)` - maps each code point by the `mathvariant` in effect, or to its mathematical
  italic when the automatic italic applies and no `mathvariant` is set; `mathvariant="normal"` cancels it.
- `MathVariants.TryParse`, `Map`, and `AutoItalic` - the lookups; a character without a styled form is unchanged.
- `LayoutSpace` - the `width`, `height`, and `depth` of `mspace` resolved by the style resolver; a negative depth or
  height is clamped to zero, a negative width is ignored with a `mathml-attribute-invalid` warning, and a percentage is
  ignored and reported.

#### Error Handling

A character that no bundled font has is a `mathml-text-missing-glyph` warning located at the text, naming the code point
(`U+E000`), and the missing-glyph box is drawn. Invalid and unsupported attribute values are reported through the style
resolver and ignored. Nothing throws for the content of a token.

#### Dependencies

The `MathFont`, `MathBox`, and `StyleResolver` units, the `MathTree` unit of the Syntax subsystem, the root namespace's
`MathMlReportSink` and `MathMlDiagnosticCodes`, and the core `Fonts` subsystem (`TrueTypeFont`, `GlyphFallback`).

#### Callers

`LayoutEngine` lays out every token and every run of text directly inside a layout element with it; `StyleResolver`
parses `mathvariant` with `MathVariants`.
