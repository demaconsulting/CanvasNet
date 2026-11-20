---
title: Unsupported Vega-Lite
summary: 'What a chart cannot use: geographic charts, remote data, interaction, and more.'
tags:
  - vega-lite
  - diagnostics
see-also:
  - vega-lite/diagnostics
---

## Unsupported

- The geoshape mark; geographic data, the longitude and latitude channels,
  projection, and the geojson field type.
- Every remote url of an image mark (a network URL, a UNC path, a file: URI with
  a host): it is left out and reported (svg-image-unresolved), never fetched.
  The size of a picture beyond the pixel budget is left out too
  (svg-image-too-large).
- Interaction: events, input widgets, and bindings are ignored (a selection is
  drawn in the state its value gives). The legend that a selection is bound to
  (bind "legend") dims the entries the selection does not hold to
  config.legend.unselectedOpacity (0.35), symbols and labels, as Vega-Lite does.
  Variable params inside a layer or a composition are ignored, as Vega-Lite
  itself declares no signal for them. A lookup from a param (the data of a
  selection) is reported and not applied.
- A point selection without fields or encodings (it selects rows by identity)
  with an initial value, animation selections (a timer), and a top-level
  selection of a layered view with two or more layers (Vega rejects it): each is
  reported and applied as an empty selection.
- An xOffset or yOffset without a nominal or ordinal position channel to nest
  in, or a temporal one with a time unit to nest in (not day or weekday, binned
  or maxbins; not a rect mark), and a temporal or binned offset of a bar or
  area, or a continuous offset of a bar or area whose position has a time unit
  or a secondary channel. (An unbinned quantitative offset of a bar or area with
  a nominal or ordinal position is a ranged offset and is drawn from the zero of
  the offset scale to its value.)
- legend.encode and axis.encode, and a custom formatType.
- Expression functions outside the 113 (the random function and sampleNormal,
  sampleLogNormal and sampleUniform among them: an expression is a pure function
  of its row and params), signal, event, and item, and the bitwise and in
  operators.
- Remote data: a url with a scheme other than a local file: URI, or a network
  path, is never fetched; the chart is drawn with an empty dataset.
- values given as text, the identity scale, and scale properties such as bins.
- Vega (as opposed to Vega-Lite) specifications.
- Anything injected from the command line other than the values of params
  (--param): there is no option for datasets, configuration, a data folder, a
  time zone, or fonts.
