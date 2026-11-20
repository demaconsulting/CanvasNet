---
title: Render options of a chart
summary: Size, background, page, params, and strict mode for a chart.
tags:
  - vega-lite
  - hosting
see-also:
  - vega-lite/params
  - limits
---

The host chooses these when it renders a chart: the properties of
`ViewerRenderOptions` for `CanvasNetViewer.Render`, or of `SourceOptions` for
the operations of the Imaging namespace.

## Options

- `Scale`: Multiply the laid-out size by a factor.
- `Width`, `Height`: Fit the chart to a width or a height, keeping its aspect
  ratio; with both, fit it inside the box and center it.
- `Background`: Replace the background the chart declares (white when it
  declares none); a transparent color keeps alpha.
- `ThrowOnUnsupportedFeature`: Strict mode (`SourceOptions.Strict`): fail with
  unsupported-feature instead of leaving a construct out.
- `Page`: A chart has one page, so only 1 is valid; a larger number is
  page-out-of-range.
- `Parameters`: The values of a `ChartOptions` (`ViewerRenderOptions.VegaLite`
  or `SourceOptions.VegaLite`) replace the default values of params of the
  specification, by name. A value is true, false, null, a number, text, or a
  list or a dictionary of values, so a selection takes its initial state as in
  the specification ({"x": [2, 6]} for an interval). A name the specification
  does not declare is a vega-lite-error that lists the declared ones (with
  `WarnOnUnknownParameters` the warning vl-param-unknown). The chart is laid out
  again with the values, so its size can change; every other kind of file
  ignores the option. An SVG file sets the params of a chart it embeds with the
  fragment of the link: `<image href="chart.vl.json#param.cutoff=50" .../>` (see
  [[svg]]), and so does an input path
  (chart.vl.json#param.cutoff=50; the fragment wins over the option for that
  input, and a name the chart does not declare is then the warning
  vl-param-unknown instead of an error).
- The options of a model (`ScadViewOptions`) do not apply to a chart. A probe
  (`SourceLoader.Probe`) reports the laid-out size, and in `SourceInfo.Chart`
  the marks and data references, without drawing.
