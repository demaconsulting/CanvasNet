---
title: SVG drawings (.svg)
summary: SVG drawings; image href embeds other pictures and pages.
children:
  - supported
  - embedding
  - fragments
  - units
  - flatten
  - options
  - diagnostics
see-also:
  - files
  - limits
---

A common subset of SVG, drawn sharply at the target size. An image can embed a
picture, another SVG file, a model, a chart, a Mermaid diagram, or a document
page, named relative to the SVG file.

Use an SVG file as the layout of a page: draw titles, captions, and frames in
SVG, and place every diagram, chart, model, or document page with `<image
href="file" x="..." y="..." width="..." height="..."/>` (see `canvasnet help svg
embedding`). Render with `canvasnet render page.svg --width 1200 -o page.png
--json` and look at the PNG.
