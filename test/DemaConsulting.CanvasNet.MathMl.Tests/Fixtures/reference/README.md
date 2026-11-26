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
  operator, a prime, the ForceDefault vertical line, and two-character operators), and `operators-lone-in-msqrt-mtd` (a
  lone operator in the anonymous rows of `msqrt` and `mtd`, which MathML Core and Chrome space as a grouping element:
  without operator spacing).
- **Stretchy and large operators**: `stretchy-parens` (the base glyph, variants, and an assembly),
  `stretchy-brackets-braces`, `stretchy-symmetric` (symmetric and non-symmetric targets, `stretchy="false"`, and an
  operator without a construction), `largeop-display`, `largeop-inline`, and `minsize-maxsize` (lengths and percentages,
  a row of only stretchy operators, and operators wrapped in `mrow` and `mstyle`).
- **Fractions**: `mfrac-basic` (inline and display style), `mfrac-linethickness` (`0`, `0px`, `200%`, `3px`, `0.2em`,
  and `-1px`), `mfrac-nested` (continued fractions, whose levels follow `math-depth: auto-add`), `mfrac-operators`
  (fractions between operators and an embellished fraction), `mfrac-fenced` (stretchy fences around fractions), and
  `mfrac-raised-denominator` (a ring above as the denominator, with a bar and as a stack: the descent of the fraction is
  at least zero).
- **Radicals**: `msqrt-basic` (one and several children, with the forms of their operators), `msqrt-tall` (size
  variants and the glyph assembly of the radical symbol), `mroot-index` (a narrow index, a wide index, and nested
  roots), and `mroot-deep-index` (indexes with a depth, which the ascent of the root counts).
- **Scripts**: `msub-msup` (letter, italic *f*, digit, and row bases), `msubsup-gap` (the gap of both scripts with
  `mspace` scripts, the cramped superscript under a radical and in a denominator), `scripts-largeop` (integrals inline
  and in display style), `scripts-largeop-wide-limit` (an integral in display math whose subscript is wider than its
  superscript), `msup-italic-letter` (digit superscripts of ζ and b, inline and cramped under a radical),
  `mmultiscripts-tensor` (postscript pairs with `none`), `mmultiscripts-prescripts` (the nuclide notation and prescripts
  with `none`), and `scripts-embellished-fence` (scripts on a fenced row and embellished scripted operators spaced by
  their operators).
- **Under- and overscripts**: `munderover-limits` (limits of a summation, a union, and a product in display style),
  `movablelimits-inline` (limits moved to the script positions inline, `movablelimits="false"`, a text operator, and a
  letter base), `mover-accents` (combining circumflexes stretched over one and three letters, a tilde, an accent
  `munderover` written `TRUE`, an accent underline, and an overscript that is not an accent), `overbrace-underbrace`
  (braces stretched over and under rows, a labeled brace, and a brace base with a label), and `arrow-over` (arrows as
  bases with labels at the stretch stack gaps, a non-stretchy arrow over a wide label, and an arrow overscript).
- **Tables**: `table-matrix` (a 3 by 3 matrix of letters and digits), `table-spans` (`columnspan`, `rowspan`, and
  `rowspan="0"`, with `mspace` contents), `table-fenced-matrix` (a matrix in stretched parentheses after `A =` and
  before a fraction, in a display equation), `table-cell-baselines` (fractions, scripts, a radical, and a deep `mspace`
  in the cells of an inline table, followed by an operator and a letter), and `table-span-values` (the `columnspan`
  values `2px`, `+2`, and ` 2`, which browsers read with the HTML rules for parsing non-negative integers).

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
- A negative `linethickness` draws no bar (`mfrac-linethickness`).
- A radical is as wide as its base plus the widest construction of the radical symbol, while its base follows the
  symbol drawn; an assembled symbol is as wide as its ink (`msqrt-tall`).
- The bottom of the index of a root is raised by RadicalDegreeBottomRaisePercent of the height of the radical symbol,
  the percentage taken at Chrome's 1/64 precision (`mroot-index`).
- Only the italic correction of an operator drawn with a size variant or an assembly moves a script (`scripts-largeop`;
  the superscript of an italic *f* starts at its advance, `msub-msup`), and the width of a scripted element is its base
  and its wider script with SpaceAfterScript, without the italic corrections. Two visible consequences: a large
  operator whose subscript is wider than its superscript ends LargeOpItalicCorrection plus SpaceAfterScript after its
  subscript (`scripts-largeop-wide-limit`), and the superscript of an italic letter starts at the advance of the letter
  with its bottom below the top of a tall letter, so a letter leaning right at its top (ζ, b) may touch it
  (`msup-italic-letter`).
- The ascent and the descent of a root count the depth of its index (`mroot-deep-index`; MathML Core leaves it out of
  both). Chrome draws an index taller than the raise of its bottom lower than that raise, which CanvasNet does not (a
  known difference), so the fixtures use indexes no taller than the raise.
- An overscript is centered on its base rather than placed by its top accent attachment, and the gaps of an accent and
  of a script that is neither a limit nor on a stretchy base are measured between the boxes (`mover-accents`).
- An operator stretched along the inline axis keeps the inline size of its unstretched glyph, its stretched glyph
  centered on it (`mover-accents`, `overbrace-underbrace`).
- The ascent and the descent of a row, a scripted element, and an under- and overscript element are at least zero (the
  brace base of `overbrace-underbrace` reaches down to the baseline).
- A cell spanning several columns or rows spreads the room it lacks over them in proportion to their sizes, and a
  row-spanning cell is aligned on the baseline of its first row (`table-spans`); the boxes of `mtr` and `mtd` are the
  whole row and cell rectangles, padding and spans included.

## Fixture Notes

- `stretchy-symmetric` writes `depth="0em"` rather than `depth="0"`: Chrome turns the `height` and `depth` of `mspace`
  into the CSS `calc(height + depth)`, which is invalid with a unitless zero, so the browser would give the `mspace` an
  empty box while keeping its ascent; the fixture avoids that browser quirk, which is not what it tests.
- Chrome rounds the overlap of a glyph assembly to its layout unit, so its assemblies may be a fraction of a pixel
  taller than CanvasNet's; the difference is within the tolerance.
- Chrome's glyph bounds follow the hinting of the font: at 50 px (two script levels) the digits are a pixel shorter
  than their outlines rounded out, and the plus sign is a pixel higher at 100 px. Each alone is within the tolerance,
  but a pixel carried through a nested fraction or a root adds up beyond it, so `mfrac-nested` and `mroot-index` use
  letters (or a minus sign) where a digit at 50 px or a plus sign would carry such a pixel into the values the fixtures
  test, and `mfrac-fenced` and `mfrac-operators` avoid the letters whose automatic italic Chrome measures on a taller or
  shorter upright letter (see *Known Differences*). The ink width of an assembled radical symbol rounds a pixel narrower
  than Chrome's for the same reason (`msqrt-tall`, within the tolerance).
- `scripts-largeop-wide-limit` uses letters as limits: a minus sign and an infinity at 60 px each carry a hinted pixel
  that adds up beyond the tolerance in the height of the scripted element.
- The script fixtures avoid the same pixel: digits and letters with descenders as scripts at 60 px (`j`, `y`, and the
  `=` sign of a limit), and the `lim` operator in display style (a pixel taller in Chrome), would carry it beyond the
  tolerance together with Chrome's 1/64 pixel flooring of the MATH constants; `msubsup-gap` uses `mspace` scripts to
  test the gap.
- Chrome computes the width of an `mmultiscripts` with both prescripts and postscripts from its postscript pairs only
  (its box is then wider than its content), so `mmultiscripts-prescripts` uses script pairs of one width.
- The U+02D9 dot above has no glyph in Noto Sans Math; Chrome draws it with a system font and CanvasNet with the bundled
  Noto Sans, so `mover-accents` uses the ring above (U+02DA) and the combining low line (U+0332) instead.
- A table centered on the math axis puts its rows, and the equation after it, on a baseline at a fraction of a pixel,
  where Chrome's pixel-snapped glyph bounds of the letters with ascenders (`b`, `d`, `t`) and of the plus sign are a
  pixel taller than their outlines rounded out; carried through two rows or onto the baseline of the equation, that
  pixel goes just beyond the tolerance, so `table-fenced-matrix` and `table-cell-baselines` use x-height letters and the
  minus sign.

## Fixtures Not Compared With Chrome

- **Every `mathvariant` value but `normal`**: Chrome implements MathML Core, whose `mathvariant` only cancels the
  automatic italic, so the browser draws `mathvariant="bold"` upright and unstyled. `mathvariant-normal` checks the
  value `normal` and the Mathematical Alphanumeric Symbols written directly; the other values are verified by the unit
  tests of `TokenLayout`.
- **The stretch fallbacks**: Chrome stretches only what the font constructs, so the Unicode-based glyph assemblies of
  MathML Core B.4 and the rule of a line character are verified by the unit tests of `StretchyGlyphs`.
- **The MathML 3 table attributes**: Chrome ignores `columnalign`, `rowalign`, `columnspacing`, `rowspacing`,
  `framespacing`, `columnlines`, `rowlines`, and `frame`; their fixtures and Firefox reference layouts are in
  `../reference-firefox/` (see the README there).
- **Stretched arrows**: Chrome does not stretch the right and left arrows of Noto Sans Math (their assembly connectors
  are shorter than MinConnectorOverlap), which CanvasNet assembles; `arrow-over` compares their boxes (the box keeps the
  inline size of the unstretched arrow in both), and the stretching is verified by the unit tests of `TokenLayout`.
- **Stretchy operators at the base of a script**: MathML Core makes a scripted element whose base is a stretchy operator
  an embellished operator that its row stretches; Chrome does not stretch it (it draws the operator at its size,
  centered on the baseline). `scripts-embellished-fence` uses `stretchy="false"` there, and the stretching is verified
  by the unit tests of `LayoutEngine`.
- **The form of an operator at the base of a script**: MathML Core gives it the form of the scripted element in its row;
  Chrome infers it from the position of the operator inside the scripted element (prefix), so the fixtures use operators
  without a prefix form there (`=`).

## Known Differences (Exceptions of the Test)

- **`tokens-mi-italic`, value `1 <mi> height`**, and **`operators-spacing`, value `10 <mi> height`**: Chrome measures
  the ink of an automatic italic `mi` on the untransformed character (the upright `f` has no descender) but draws and
  advances the italic glyph, which descends below the baseline; CanvasNet measures the glyph it draws.
- **`arrow-over`, values `math height`, `1 <munderover> height`, and `1/1 <mi> y`**, and **`overbrace-underbrace`,
  value `math height`**: Chrome's hinted bounds of the arrow and of the under brace are a pixel shorter than their
  outlines rounded out, and Chrome floors the MATH constants to its layout unit (1/64 pixel); together the values
  differ by just over the tolerance (1.02 pixels).
