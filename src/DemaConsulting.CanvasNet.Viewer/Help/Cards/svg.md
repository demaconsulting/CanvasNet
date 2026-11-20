---
title: SVG drawings (.svg)
summary: SVG drawings; image href embeds other pictures and pages.
children:
  - supported
  - embedding
  - fragments
  - units
  - flatten
  - style
  - composition
  - framed-figure
  - arrows
  - bindings
  - options
  - diagnostics
see-also:
  - style
  - composition
  - files
  - limits
---

A common subset of SVG, drawn sharply at the target size. An image can embed a
picture, another SVG file, a model, a chart, a Mermaid or PlantUML diagram, or a
document page, named relative to the SVG file.

Use an SVG file as the layout of a page: draw titles, captions, and frames in
SVG, and place every diagram, chart, model, or document page with `<image
href="file" x="..." y="..." width="..." height="..."/>` (see `canvasnet help svg
embedding`). Render with `canvasnet render page.svg --width 1200 -o page.png
--json` and look at the PNG.

For good-looking figures read `canvasnet help svg style` and
`canvasnet help svg composition` (`--example report` is a complete figure);
`canvasnet help svg framed-figure` frames one picture with a title band and
callout arrows, `canvasnet help svg arrows` draws arrowheads that follow their
connectors, and `canvasnet help svg bindings` lands an arrow on a feature of a
diagram, chart, or model.
