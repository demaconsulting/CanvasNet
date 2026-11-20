---
title: Styling SVG drawings
summary: Typography, cards, gradients, spacing, viewBox sizing, and contrast for good-looking SVG figures.
see-also:
  - style
  - svg/composition
  - svg/framed-figure
  - svg/arrows
  - svg/supported
  - svg/units
examples:
  - name: gradient
    kind: source
    language: xml
    file: gradient.svg
    description: A rounded rectangle filled with a linear gradient.
  - name: render-page
    kind: command
    language: shell
    content: 'canvasnet render page.svg --width 1200 -o page.png --strict --json'
    description: Draw a drawing 1200 pixels wide, sharply, and fail if a linked picture is not drawn.
---

Treat the SVG file as the page: lay it out in a `viewBox` of round units, keep
the type in one `<style>` element, and render it at its final width. The
`canvasnet help svg composition --example report` drawing uses every rule
below.

## Typography

- Use the generic families `sans-serif`, `serif`, and `monospace`: they are
  drawn with the bundled Liberation fonts, so the text looks the same on every
  machine. An installed family is used when the machine has it, so text in
  such a family can differ between machines.
- Keep the type in classes:
  `<style>.title { font: bold 30px sans-serif; fill: #0f172a; }</style>` and
  `<text class="title" x="64" y="84">...</text>`; one change then updates every
  title.
- On a page 1000 units wide, a title of about 30, body text of 16 to 18, and
  captions of 14 to 15 read well. Give a caption a muted color (`#475569`) and
  `font-style: italic`.
- SVG text does not wrap: break a long line into several `<text>` elements,
  about 1.4 times the font size apart.

## Cards, lines, and gradients

- A card: `<rect x="24" y="24" width="952" height="932" rx="14" fill="#ffffff"
  stroke="#e2e8f0" stroke-width="2"/>` on a light page (`#f1f5f9`); a thin
  `<line>` of the same gray separates a header from the content.
- Keep a gradient subtle: two close colors of one hue
  (`canvasnet help svg style --example gradient`).

## Title bands, accents, and arrows

- A title band: a `<path>` with rounded top corners across the top of the
  card, filled with a horizontal gradient of one dark hue (`#1e3a8a` to
  `#2563eb`), with white title text and a light lead line (`#bfdbfe`).
- Accents: one warm color (`#f59e0b`) for a thin stripe under the band and a
  short bar beside each callout label; chevrons are white `<polyline>` strokes
  with `stroke-linecap: round` at rising opacity.
- Arrows: a `<marker>` with `orient="auto"` on `marker-end`, in one strong
  color (`#ea580c`) with a `stroke-width` of 2 to 3; `canvasnet help svg
  arrows` places the tip and curves the connector.
- `canvasnet help svg framed-figure --example framed` uses every rule here.

## Spacing and size

- Make the `viewBox` the layout units and give `width` and `height` the same
  numbers; keep margins of 24 to 64 units and one gap between every picture and
  its caption.
- Render at the final size with `--width 1200` (or `--size`, `--scale`): the
  drawing is drawn sharply at that size, not enlarged from a small picture.

## Contrast and background

- Use dark text on a light fill (`#0f172a` on `#ffffff`), and a darker stroke
  of the same hue around a colored shape.
- Where nothing is drawn the picture shows the `--background`, white by
  default: draw a full-size `<rect>` for a page color, or keep the drawing
  clear with `--background transparent`.
