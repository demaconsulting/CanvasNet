---
title: Known differences from browsers
summary: Glyph shaping, weights, stretched operators, accents, table attributes, and other differences.
tags:
  - mathml
  - diagnostics
see-also:
  - mathml/unsupported
  - mathml/style
  - mathml/pitfalls
---

The equation is laid out by the rules of MathML Core and compared with Chrome
(and with Firefox for the table attributes); these differences remain.

## Known differences

- Glyphs are placed by their advances, without the script variants (ssty), the
  dotless forms (dtls), or the kerning of the font, so a prime written as a
  superscript, or a script on a letter with an overhang, can sit a little apart
  from a browser's.
- One weight: Noto Sans Math has one regular weight and `font-weight` is not
  supported; letters and digits are bold with `mathvariant="bold"` (the bold
  mathematical letters), but punctuation and other text stay regular.
- A stretched operator is centered in the width of its widest size, as Chrome
  draws it, which leaves gaps beside a short argument; write `stretchy="false"`
  there (see [[mathml/pitfalls]]).
- An accent or an overscript is centered over its base, as Chrome centers it,
  instead of placed at the top-accent point of a slanted letter.
- Every mathvariant value is drawn and the table attributes (columnalign,
  rowspacing, columnlines, frame, ...) are drawn as Firefox draws them; Chrome
  draws only mathvariant="normal" and ignores the table attributes.
- `ms` is drawn without quotes, as MathML Core and Chrome draw it.
- Percentages of `mspace` and `mpadded` are ignored, as MathML Core says.
- There is no line breaking, no right-to-left layout, and no style sheet.
- A character that Noto Sans Math lacks is drawn with the bundled Noto Sans and
  the other fallback fonts; the picture never depends on installed fonts.
- A slanted letter gets its italic correction as clearance before a fence or a
  superscript where its ink would otherwise collide with it (the 2 of ζ², the k
  of y to the k, a parenthesis after f); Chrome lets them touch.
- A scripted element ends after its scripts as they are placed, so the
  integrand follows an integral with limits about a quarter em after the
  limits, close to TeX; Chrome leaves nearly 1em there.
- The subscript of a single character stays at the normal subscript shift of
  the font whatever its descender, as TeX sets it (the n of γ as low as the n
  of x); MathML Core and Chrome lower it below the descender.
- The picture is the content box united with the ink and a one-pixel margin
  around the ink, so an overhanging glyph, a stretched fence, or the tail of a
  radical is never clipped; a browser's box may be a pixel or two smaller.
