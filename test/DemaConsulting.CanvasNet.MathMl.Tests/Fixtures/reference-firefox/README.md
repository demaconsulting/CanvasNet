# MathML Reference Layouts (Firefox)

The `*.mml` files in this folder are original fixtures written for this repository. They exercise the MathML 3 table
attributes that MathML Core dropped and Chrome ignores, but that CanvasNet draws as Firefox draws them (decision D2 of
issue #192): `columnalign`, `rowalign`, `columnspacing`, `rowspacing`, `framespacing`, `columnlines`, `rowlines`, and
`frame`. Each `*.layout.json` file is the layout that headless Firefox computes for its fixture, with the Noto Sans Math
font that CanvasNet bundles, at a font size of 100 CSS pixels, in the format of the Chrome goldens of `../reference/`
(the version key is `firefox`).

- **Generator**: `tools/visual-diff/update-mathml-goldens.ps1 -Browser Firefox`, which runs
  `tools/visual-diff/render-mathml.mjs --browser firefox` (see the README there). The goldens are made offline and
  committed; no browser runs at test time.
- **Browser**: firefox/160.0 (the Firefox that the puppeteer of the pinned `@mermaid-js/mermaid-cli` 11.16.0 expects,
  installed once with `npx puppeteer browsers install firefox`).
- **Font size**: 100 CSS pixels.
- **Compared**: `MathMlReferenceLayoutTests.CanvasNetMathMl_ReferenceLayout_MatchesFirefox` compares the size of every
  `mtable` and the box of every `mtr`, `mtd`, and `mspace`, relative to the box of its outermost `mtable`. The tokens,
  the operators, and the `math` element are not compared: Chrome is CanvasNet's reference for them.
- **Tolerance**: 1 CSS pixel plus Firefox's layout unit (1/60 pixel), for every value.

## Fixtures

- `table-columnalign` - `columnalign="left right"` on the table (the last value repeated over the third column), a row
  with `columnalign="center"`, and a cell with `columnalign="left"` in it.
- `table-rowalign` - `rowalign="top bottom center baseline axis"` over five rows of a tall and a short cell, and a row
  with `rowalign="top"` whose second cell has `rowalign="bottom"`.
- `table-spacing` - `columnspacing="0.5em 2em"` and `rowspacing="0.5em 1.5em"` with a solid frame and
  `framespacing="1em 0.5em"`, and a second table with zero spacing and a dashed frame.
- `table-lines-frame` - `columnlines="solid dashed"`, `rowlines="dashed"`, and a solid frame, and a one-cell table with
  a dashed frame.

## How The Fixtures Avoid Firefox's Own Differences

- **Frames** - every fixture table has a frame. Without one, Firefox drops the outer padding of the edge cells, which
  CanvasNet keeps as MathML Core and Chrome do (see *Known Differences* in `docs/design/canvas-net-math-ml.md`).
- **`em` lengths** - Firefox resolves `ex` in the spacing attributes with its default font rather than the font of the
  table, so the fixtures write the spacing in `em` and give `framespacing` where a frame and spacing meet.
- **`mspace` contents** - Firefox puts a line box with the strut of the font (ascent 1.07em, descent 0.42em at this
  size) in every cell, which MathML Core and Chrome do not; every content is an `mspace` at least 1.2em high and 0.5em
  deep, so the strut never decides a size, and token metrics play no part.

## Known Differences

- **Lines** - a column or row line widens its gap by one pixel, and CanvasNet draws it in the middle of the gap between
  the cell boxes; Firefox counts that pixel in the box of the cell after the line. The boxes of those cells and their
  contents differ by at most one pixel, within the tolerance.
