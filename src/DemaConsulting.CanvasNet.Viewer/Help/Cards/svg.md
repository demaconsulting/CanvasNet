---
title: SVG drawings (.svg)
summary: SVG drawings; image href embeds other pictures and pages.
tags:
  - svg
  - authoring
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
picture, another SVG file, a model, a chart, a Mermaid or PlantUML diagram, a
MathML equation, or a document page, named relative to the SVG file.

Use an SVG file as the layout of a page: draw titles, captions, and frames in
SVG, and place every diagram, chart, model, or document page with
`<image href="file" x="..." y="..." width="..." height="..."/>` (see
[[svg/embedding]]). Render it at its final width and look at the
picture.

For good-looking figures read [[svg/style]] and
[[svg/composition]]
([[#report]] is a complete figure);
[[svg/framed-figure]] frames one picture with a title band and
callout arrows, [[svg/arrows]] draws arrowheads that follow their
connectors, and [[svg/bindings]] lands an arrow on a feature of a
diagram, chart, or model.
