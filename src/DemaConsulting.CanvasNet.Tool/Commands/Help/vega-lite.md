---
title: Vega-Lite charts (.vl.json, .vl)
summary: Vega-Lite charts (.vl.json, .vl) drawn as one picture.
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
  - composition
  - options
  - unsupported
  - diagnostics
  - warnings
  - differences
see-also:
  - svg
  - limits
---

A chart is laid out and drawn as one still picture; a defined subset of
Vega-Lite 5 and 6.

Write a normal Vega-Lite specification and render it with `canvasnet render
chart.vl.json -o chart.png --json`, then look at the PNG. Start with `canvasnet
help vega-lite marks` and `canvasnet help vega-lite encodings`; `--example bar`
prints a small chart to start from. A chart can also be placed inside an SVG
drawing with `<image href="chart.vl.json" .../>` (see `canvasnet help svg`).
