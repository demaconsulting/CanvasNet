---
title: Pictures in charts and charts in drawings
summary: The image mark, and a chart placed inside an SVG drawing.
see-also:
  - svg/embedding
  - svg/fragments
---

## Supported

- Pictures: the image mark shows the files named by its url channel (a field, a
  datum, or a value) or its url property: a raster (PNG, JPEG, and the other
  formats), an SVG file, a .scad model, a page of a document
  ("report.pdf#page=2"), a Mermaid diagram, or another chart. Files are named
  relative to the folder of the chart, and nothing is fetched from the network.
  The position, width, height, aspect (true by default), align, baseline,
  smooth, and opacity of the mark follow Vega-Lite; see 'canvasnet help svg' for
  the links the pictures accept.

## Known differences

- A chart can be drawn inside an SVG diagram with `<image href="chart.vl.json"
  .../>` (see 'canvasnet help svg'); it keeps its own background there,
  and #param.<name>=<value> in the href sets a param of the chart the way
  --param does.
- Pictures of an image mark are loaded when the chart is drawn, relative to the
  chart's folder, and the chart is laid out without them: the size of a picture
  never changes the size of the chart. A raster keeps its pixels as scene units
  (at --scale 2 it covers twice the pixels); a model, an SVG file, a PDF page,
  or a chart without a width and height of the mark is drawn 256 units wide and
  high; a picture that a width and a height fit is fitted inside the box and
  centered when its aspect ratio differs (aspect false stretches it). One
  picture that many rows show is loaded once. At most 256 different pictures are
  loaded; beyond that the chart fails with budget-exceeded.
