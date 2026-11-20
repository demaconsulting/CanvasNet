---
title: Pictures in charts and charts in drawings
summary: Charts in SVG figures, several charts in one specification, and pictures shown by the image mark.
tags:
  - vega-lite
  - composition
see-also:
  - svg/bindings
  - composition
  - vega-lite/style
  - positions/vega-lite
  - vega-lite/layout
  - svg/composition
  - svg/embedding
  - svg/fragments
---

Choose where the charts meet: in one Vega-Lite specification when they share
data or scales, or in an SVG figure when they sit beside diagrams, models,
pages, and text.

## Charts together

- Several views of one data set belong in one specification: `layer` to share
  the axes, `hconcat` or `vconcat` to align charts, and `facet` or `repeat` for
  small multiples (see [[vega-lite/layout]]). One `config` then
  styles them all.

## A chart in a figure

- Place the chart in an SVG layout with
  `<image href="chart.vl.json" x="150" y="476" width="700" height="432"/>`. It
  is drawn at the size of the placement on the output, so it stays sharp, and
  its text scales with it: give the placement the aspect ratio of its natural
  size (the size it is drawn at when no size is asked for), and a size near its
  natural size so its text matches the page.
- The chart keeps its own background in the figure; write
  `"background": "transparent"` in the chart when the page has a color.
- Set a param for one placement with `#param.<name>=<value>` after the file
  name, the way the `Parameters` option does for a render.

## Supported

- Pictures: the image mark shows the files named by its url channel (a field, a
  datum, or a value) or its url property: a raster (PNG, JPEG, and the other
  formats), an SVG file, a .scad model, a page of a document
  ("report.pdf#page=2"), a Mermaid diagram, or another chart. Files are named
  relative to the folder of the chart, and nothing is fetched from the network.
  The position, width, height, aspect (true by default), align, baseline,
  smooth, and opacity of the mark follow Vega-Lite; see [[svg]] for
  the links the pictures accept.

## Point at a feature

- Render the chart alone at the size of its placement with position selectors
  (`ViewerRenderOptions.Positions`): `mark:x=B` gives the box and center of that
  bar, `axis:@id=y` the y axis, each with the path of its view. Add the image
  `x` and `y` for an arrow in a figure (see
  [[positions/vega-lite]]).

## Known differences

- A chart can be drawn inside an SVG diagram with
  `<image href="chart.vl.json" .../>` (see [[svg]]); it keeps its
  own background there, and #param.<name>=<value> in the href sets a param of
  the chart the way the `Parameters` option does.
- Pictures of an image mark are loaded when the chart is drawn, relative to the
  chart's folder, and the chart is laid out without them: the size of a picture
  never changes the size of the chart. A raster keeps its pixels as scene units
  (at a scale of 2 it covers twice the pixels); a model, an SVG file, a PDF
  page, or a chart without a width and height of the mark is drawn 256 units
  wide and high; a picture that a width and a height fit is fitted inside the
  box and centered when its aspect ratio differs (aspect false stretches it).
  One picture that many rows show is loaded once. At most 256 different pictures
  are loaded; beyond that the chart fails with budget-exceeded.
