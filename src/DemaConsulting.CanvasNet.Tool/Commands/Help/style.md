---
title: Making pictures look good
summary: 'Make pictures look good: palette, fonts, size, background.'
see-also:
  - mermaid/style
  - vega-lite/style
  - svg/style
  - scad/style
  - composition
examples:
  - name: check
    kind: command
    language: shell
    content: 'canvasnet render chart.vl.json --scale 2 -o chart.png --strict --json'
    description: Draw at twice the natural size, fail if anything is left out, and print the result to check.
  - name: colors
    kind: command
    language: shell
    content: 'canvasnet inspect colors chart.png --count 6'
    description: List the dominant colors of a rendered picture, to check its palette and background.
---

Decide the final size first, choose a small palette, and check every change in
the rendered PNG. The style card of each format shows the syntax:
`canvasnet help mermaid style`, `vega-lite style`, `svg style`, and
`scad style`.

## Palette and contrast

- Use few colors: one accent for what the text talks about, a quiet blue-gray
  for the rest, and red only for a problem. Give a color one meaning in every
  picture of a document.
- Pair each light fill with a darker stroke and darker text of the same hue
  (text `#1b2733` on `#eef4fb` with a `#2f6db3` stroke), and keep text dark on
  light or light on dark.
- Set category colors yourself (Mermaid `classDef`, Vega-Lite
  `config.range.category`) so a diagram and a chart of one document match.

## Font sizes and output size

- Text and lines scale with the picture: `--scale 2` doubles them, and
  `--width` scales the picture to fit. Pick the final width (1200 pixels for a
  figure across a page), then size the text for it: a title about twice the
  body text, and no label below 12 pixels at the final size.
- An embedded picture is drawn at the size of its placement, and its text
  scales with it: place it near its natural size (`canvasnet info file --json`
  reports `input.natural`) so its text matches the text around it.
- Mermaid and Vega-Lite text is always drawn with the bundled fonts; in SVG use
  the generic families (`sans-serif`, `serif`, `monospace`), which are bundled
  too, because an installed family differs between machines.

## Spacing

- Leave room: margins of 24 to 64 units in an SVG figure, `padding` in a
  Vega-Lite chart, and `nodeSpacing` and `rankSpacing` in a Mermaid flowchart.
- Align pictures and captions on one grid, with one gap between each picture
  and its caption.

## Light and dark backgrounds

- A drawing, chart, diagram, or model is drawn over white unless `--background`
  says otherwise. `--background transparent` keeps it clear for a slide or a
  colored page; JPEG has no transparency, so it is flattened over the
  background.
- For a dark page use a dark `--background` together with a dark design
  (Mermaid `--theme dark`, light chart text and axes).
- Inside an SVG figure a diagram and a model stay transparent, and a chart
  keeps its own background unless it sets `"background": "transparent"`.

## Resolution

- Draw a vector source at the target size with `--width`, `--height`,
  `--size`, or `--scale` instead of resizing a small PNG; for a high-density
  screen draw at twice the display size. No side may exceed {{maxDimension}}
  pixels.

## Check the result

- Render with `--strict --json`: a construct that is left out becomes an error,
  and `diagnostics[]` lists the warnings that remain.
- Look at the PNG after every change. `canvasnet inspect colors out.png` lists
  its dominant colors, and `canvasnet compare old.png new.png` measures what an
  edit changed.
