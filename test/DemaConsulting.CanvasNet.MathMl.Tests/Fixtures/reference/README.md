# MathML Reference Layouts

The `*.mml` files in this folder are original fixtures written for this repository. Each `*.layout.json` file is the
layout that MathML Core in headless Chrome computes for its fixture, with the Noto Sans Math font that CanvasNet
bundles, at a font size of 100 CSS pixels: the width, height, and baseline of the `math` element and the box of every
element inside it (`getBoundingClientRect`, relative to the top left corner of the `math` element).

- **Generator**: `tools/visual-diff/update-mathml-goldens.ps1`, which runs `tools/visual-diff/render-mathml.mjs` (see
  the README there). The goldens are made offline and committed; no browser runs at test time.
- **Browser**: Chrome/157.0.8103.78 (the headless Chrome of the puppeteer that the pinned
  `@mermaid-js/mermaid-cli` 11.16.0 brings).
- **Font size**: 100 CSS pixels, to dilute the rounding of the browser.
- **Tolerance**: 1 CSS pixel plus Chrome's layout unit (1/64 pixel), for every value
  (`MathMlReferenceLayoutTests`).

## Fixtures

- **Tokens and rows**: `tokens-mi-italic`, `tokens-mn-digits`, `tokens-mtext-space`, `tokens-ms`, `mspace`,
  `row-nested`, `mathvariant-normal`, `mpadded`, `mphantom`, `mstyle-scriptlevel`, and `merror`.
- **Operators**: `operators-spacing` (the categories A, B, C, K, M, and Default) and `operators-forms` (prefix, infix,
  and postfix forms, an explicit form, `lspace` and `rspace` with a percentage, a large operator inline, a text
  operator, a prime, the ForceDefault vertical line, and two-character operators).
- **Stretchy and large operators**: `stretchy-parens` (the base glyph, variants, and an assembly),
  `stretchy-brackets-braces`, `stretchy-symmetric` (symmetric and non-symmetric targets, `stretchy="false"`, and an
  operator without a construction), `largeop-display`, `largeop-inline`, and `minsize-maxsize` (lengths and percentages,
  a row of only stretchy operators, and operators wrapped in `mrow` and `mstyle`).

## Behaviors Matched

These choices of CanvasNet follow Chrome where its behavior differs from a literal reading of MathML Core; the goldens
pin them:

- The italic correction of a slanted child is not added between the children of a row (MathML Core 3.3.1.2 adds it
  before the next upright child).
- A token with a space (`tokens-mtext-space`) has the ink ascent and descent of its glyphs, like every other token
  (MathML Core lays such a text out as a line of text).
- The ink ascent and descent of a token are rounded out to whole CSS pixels, as Chrome rounds the bounds of its glyphs.
- `ms` draws its text without quotes (`tokens-ms`), as MathML Core and Chrome do.
- A stretched operator is centered in a box as wide as its widest construction (`stretchy-parens`), a stretchy operator
  without a construction keeps its glyph centered on its target (`stretchy-symmetric`), and a large operator in display
  style is centered on the math axis (`largeop-display`).

## Fixture Notes

- `stretchy-symmetric` writes `depth="0em"` rather than `depth="0"`: Chrome turns the `height` and `depth` of `mspace`
  into the CSS `calc(height + depth)`, which is invalid with a unitless zero, so the browser would give the `mspace` an
  empty box while keeping its ascent; the fixture avoids that browser quirk, which is not what it tests.
- Chrome rounds the overlap of a glyph assembly to its layout unit, so its assemblies may be a fraction of a pixel
  taller than CanvasNet's; the difference is within the tolerance.

## Fixtures Not Compared With Chrome

- **Every `mathvariant` value but `normal`**: Chrome implements MathML Core, whose `mathvariant` only cancels the
  automatic italic, so the browser draws `mathvariant="bold"` upright and unstyled. `mathvariant-normal` checks the
  value `normal` and the Mathematical Alphanumeric Symbols written directly; the other values are verified by the unit
  tests of `TokenLayout`.
- **The stretch fallbacks**: Chrome stretches only what the font constructs, so the Unicode-based glyph assemblies of
  MathML Core B.4 and the rule of a line character are verified by the unit tests of `StretchyGlyphs`.

## Known Differences (Exceptions of the Test)

- **`tokens-mi-italic`, value `1 <mi> height`**, and **`operators-spacing`, value `10 <mi> height`**: Chrome measures
  the ink of an automatic italic `mi` on the untransformed character (the upright `f` has no descender) but draws and
  advances the italic glyph, which descends below the baseline; CanvasNet measures the glyph it draws.
