---
title: Making pictures look good
summary: 'Make pictures look good: palette, fonts, size, background.'
tags:
  - design
see-also:
  - mermaid/style
  - plantuml/style
  - vega-lite/style
  - svg/style
  - scad/style
  - composition
---

Decide the final size first, choose a small palette, and check every change in
the rendered picture. The style card of each format shows the syntax:
[[mermaid/style]], [[plantuml/style]],
[[vega-lite/style]], [[svg/style]], and
[[scad/style]].

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

- Text and lines scale with the picture: a scale of 2 doubles them, and a width
  scales the picture to fit. Pick the final width (1200 pixels for a figure
  across a page), then size the text for it: a title about twice the body text,
  and no label below 12 pixels at the final size.
- An embedded picture is drawn at the size of its placement, and its text scales
  with it: place it near its natural size (the size it is drawn at when no size
  is asked for) so its text matches the text around it.
- Mermaid, PlantUML, and Vega-Lite text is always drawn with the bundled fonts;
  in SVG use the generic families (`sans-serif`, `serif`, `monospace`), which
  are bundled too, because an installed family differs between machines.

## Spacing

- Leave room: margins of 24 to 64 units in an SVG figure, `padding` in a
  Vega-Lite chart, and `nodeSpacing` and `rankSpacing` in a Mermaid flowchart.
- Align pictures and captions on one grid, with one gap between each picture
  and its caption.

## Light and dark backgrounds

- A drawing, chart, diagram, or model is drawn over the background color of the
  render (white unless the host says otherwise). A transparent background keeps
  it clear for a slide or a colored page; JPEG has no transparency, so it is
  flattened over the background.
- For a dark page use a dark background together with a dark design (the Mermaid
  theme dark, light chart text and axes).
- Inside an SVG figure a diagram and a model stay transparent, and a chart
  keeps its own background unless it sets `"background": "transparent"`.

## Resolution

- Draw a vector source at the target size (a width, a height, a box, or a scale
  factor) instead of resizing a small picture; for a high-density screen draw at
  twice the display size. No side may exceed {{maxDimension}} pixels.

## Check the result

- Render in strict mode: a construct that is left out becomes an error, and the
  diagnostics of the picture list the warnings that remain.
- Look at the picture after every change. The dominant colors of the picture
  (`ImageInspector` of the Imaging namespace) show its palette, and a comparison
  with the previous picture (`ImageComparer`) measures what an edit changed.
