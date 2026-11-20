---
title: Warnings of a chart
summary: 'The vl-* warnings: a chart that is drawn, but maybe not as meant.'
tags:
  - vega-lite
  - diagnostics
see-also:
  - vega-lite/diagnostics
  - errors
---

## Diagnostics

- `svg-image-unresolved`: Warning: the url of an image mark cannot be drawn (the
  file is missing, is remote, is of an unknown type, or embeds itself). Reported
  once, with the reason; nothing is drawn. In strict mode it fails with
  unsupported-feature, naming the chart and the url. image-fragment-invalid and
  image-page-out-of-range (a #page= that no page matches) are reported and fail
  in strict mode the same way; svg-image-too-large is a picture beyond the pixel
  budget, and a url the file-access mode denies is image-access-denied.
- `vl-param-selection-projection`: Warning: a selection cannot be projected over
  a channel (the view has no field on it, or its field is aggregated), an
  interval selection is initialized with a key that is not a channel, a
  selection used as a scale domain or a bin extent names no usable field and
  projects several (the first is used), or a selection bound to the scales
  projects a channel without a continuous scale.
- `vl-param-unknown`: Warning: the fragment of a link that embeds the chart
  (chart.vl.json#param.nope=1) sets a param the specification does not declare;
  the chart is drawn without it. The `Parameters` option is a vega-lite-error
  for the same mistake (unless `WarnOnUnknownParameters` is set). Strict mode
  does not fail on it. A value that is not usable (a JSON array that is cut
  short) is image-fragment-invalid and the chart is not drawn.
- `vl-param-extent-static`: Warning: an {"expr": ...} at a place that is fixed
  before the data is read (a title, a configuration value, an axis or legend
  property) reads the param that an extent transform computes; the param is null
  there. The transforms that follow the extent, bin.extent, and scale.domain see
  its value.
- `vl-shape-path-invalid`: Warning: a shape that is neither a symbol name nor
  SVG path data that parses (for example M0,0L): a circle is drawn; a path with
  no command draws nothing. A valid SVG path is drawn, scaled by the symbol size
  as Vega does (a path of 1 unit is half the symbol side).
- `vl-unknown-property`: Warning: a property Vega-Lite does not define for that
  object.
- `vl-schema-version`: Warning: a $schema that is not version 5 or 6; version 6
  defaults are used.
- `vl-layout-fit-step`: Warning: autosize fit together with a step size; the fit
  is dropped for that dimension.
- `vl-scale-domain-raw`: Warning: the value of a param used as a scale domain
  (or a domainRaw) is not an array of at least two numbers or dates (one value
  for a band, point, or ordinal scale); the scale keeps its own domain.
- `vl-composition-property-ignored`: Warning: a width, height, padding, config,
  or similar property of a composition that only the top-level view decides.
- `vl-text-missing-glyph`: Warning: text with characters that neither the font
  nor the bundled fallback fonts have a glyph for (for example a private-use or
  CJK character); the font's missing-glyph box is drawn. Reported at the text
  mark, or with no location for axis labels, legends, and titles.
- Warnings have the code vega-lite-warning and carry the vl-* identifier at the
  start of the message text, after the location. Errors: vega-lite-error
  (invalid specification, a data file that is missing or refused, a chart that
  cannot be drawn), budget-exceeded (a library limit), corrupt-input (a
  specification longer than {{vlMaxSourceLength}} characters), and size-limit (a
  chart laid out beyond {{maxDimension}} pixels on a side).
