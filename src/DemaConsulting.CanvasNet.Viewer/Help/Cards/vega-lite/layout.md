---
title: Layers, concatenation, facets, and repeats
summary: Views that combine charts, and what a composed view keeps.
aliases:
  - layer
  - concat
  - facet
see-also:
  - vega-lite/composition
  - vega-lite/style
  - vega-lite/marks
  - vega-lite/warnings
examples:
  - name: layer
    kind: source
    language: json
    file: layer.vl.json
    description: A bar layer and a rule layer at the mean.
---

Use `layer` to draw marks over each other on shared axes, `hconcat`, `vconcat`,
or `concat` to put charts side by side, and `facet` or `repeat` for small
multiples of one chart.

## Supported

- Views: a single view, layer, concat, hconcat, vconcat, facet, and repeat.

## Known differences

- A shared legend shows the symbols of the first layer.
- Compositions drop more properties than Vega-Lite does; each is reported as a
  warning.
- A line feed inside title text splits the title into lines, which Vega itself
  does not do.
