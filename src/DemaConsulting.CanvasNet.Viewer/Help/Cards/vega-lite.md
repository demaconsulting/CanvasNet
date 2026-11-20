---
title: Vega-Lite charts (.vl.json, .vl)
summary: Vega-Lite charts (.vl.json, .vl) drawn as one picture.
tags:
  - vega-lite
  - authoring
aliases:
  - vegalite
  - vl
children:
  - data
  - marks
  - encodings
  - transforms
  - params
  - layout
  - style
  - composition
  - options
  - unsupported
  - diagnostics
  - warnings
  - differences
see-also:
  - style
  - composition
  - svg
  - limits
---

A chart is laid out and drawn as one still picture; a defined subset of
Vega-Lite 5 and 6.

Write a normal Vega-Lite specification and render it, then look at the picture.
Start with [[vega-lite/marks]] and
[[vega-lite/encodings]];
[[#bar]] prints a small chart to start
from. A chart can also be placed inside an SVG drawing with
`<image href="chart.vl.json" .../>` (see [[svg]]).

For a good-looking chart read [[vega-lite/style]]; to combine
charts or place one in a figure, [[vega-lite/composition]].
