---
title: Diagnostics of a chart
summary: The vl-* identifiers of what is not drawn.
tags:
  - vega-lite
  - diagnostics
see-also:
  - vega-lite/warnings
  - errors
---

## Diagnostics

- Identifiers have the form vl-<area>-<detail>, in lower case. An unsupported
  construct is listed in diagnostics[] as {source: vega-lite, severity:
  unsupported, code: <id>} (without --json, also as an
  'Unsupported: <id>: <message>' line on standard error); the message gives
  file, line, column, and JSON pointer. With --strict the first one fails with
  unsupported-feature.
- `vl-mark-<type>`: A mark that is not drawn, for example vl-mark-geoshape; the
  view is drawn without marks.
- `vl-channel-geo`: A longitude or latitude channel.
- `vl-channel-url`: A url channel on a mark other than image: Vega-Lite ignores
  it there, and so does the chart.
- `vl-data-remote-url`: A url with a scheme other than a local file: URI (http,
  https, data, a file: URI with a host), or a network path: never fetched, empty
  dataset.
- `vl-data-geo`: Geographic data (sphere, graticule, topojson).
- `vl-projection`: A projection.
- `vl-param-selection-layer`: A top-level selection param of a layered view that
  has two or more layers: Vega-Lite compiles duplicate signals that Vega
  rejects; the selection is applied as empty (declare it in one layer, or name
  the layer in views).
- `vl-param-selection-id`: A point selection with no fields or encodings and an
  initial value: it selects rows by identity, which is not modeled; the
  selection is applied as empty.
- `vl-param-selection-animation`: An animation selection (on: timer): drawn in
  its initial state, without the animation.
- `vl-param-variable`: A variable param of a nested view (a layer or a
  composition): Vega-Lite declares no signal for it, so it is ignored and an
  expression that reads it sees null (Vega fails).
- `vl-transform-lookup-param`: A lookup transform whose from is a param (the
  data of a selection): it needs the final table of another view filtered by the
  selection, which is not supported yet; the transform is not applied.
- `vl-expr-ref`: An {"expr": ...} where Vega-Lite's schema allows none (a width
  or height, or anywhere in a selection, for example), or in a place with no row
  that reads datum.
- `vl-expr-function-<name>`: An expression function outside the supported 113,
  for example vl-expr-function-now (also the sampling functions random,
  sampleNormal, sampleLogNormal and sampleUniform): the call evaluates to null.
- `vl-guide-encode`: axis.encode or legend.encode.
- `vl-offset-position`: An xOffset or yOffset that has no nominal or ordinal x
  or y channel (or a temporal one with a time unit that can hold it) to nest in:
  the offset is ignored.
- `vl-offset-ranged`: A temporal or binned xOffset or yOffset of a bar or area,
  or a continuous one whose position has a time unit or an x2 or y2 channel (an
  unbinned quantitative offset of a bar or area with a nominal or ordinal
  position is drawn as a ranged offset): the offset is ignored.
- `vl-scale-invalid-value`: An entry of config.scale.invalid that is neither
  "zero-or-min" nor an object with a number or text value (null, a boolean, a
  list; an expression reference is vl-expr-ref instead): the channel is treated
  as without an entry, so the invalid mode of the mark handles its invalid rows.
- `vl-scale-<type>`: A scale type that is not supported, for example
  vl-scale-identity.
- `vl-type-geojson`: The geojson field type.
- See the Vega-Lite section of the Vega-Lite user guide for the complete list of
  identifiers.
