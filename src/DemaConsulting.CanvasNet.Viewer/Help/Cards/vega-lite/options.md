---
title: Render options of a chart
summary: Size, background, page, params, and --strict for a chart.
tags:
  - vega-lite
  - hosting
see-also:
  - vega-lite/params
  - limits
---

## Options

- `--scale <factor>`: Multiply the laid-out size by a factor.
- `--width <pixels>`: Fit the chart to a width, keeping its aspect ratio;
  --height does the same for the height.
- `--size <WxH>`: Fit the chart inside a box and center it.
- `--background <color>`: Replace the background the chart declares (white when
  it declares none); transparent keeps alpha.
- `--strict`: Fail with unsupported-feature instead of leaving a construct out.
- `--page <N>`: A chart has one page, so only 1 is valid; a larger number is
  page-out-of-range.
- `--param <name=value>`: Replace the default value of a param of the
  specification (repeatable; the later one of a name wins). The value is true,
  false, null, a number, text (region=EU), or a JSON array or object, so a
  selection takes its initial state as in the specification: --param cutoff=50,
  --param brush={"x":[2,6]} (quote it for your shell). A name the specification
  does not declare is a vega-lite-error that lists the declared ones. The chart
  is laid out again with the values, so its size can change; only a chart takes
  it (a usage-error for any other input). An SVG file sets the params of a chart
  it embeds with the fragment of the link: `<image
  href="chart.vl.json#param.cutoff=50" .../>` (see [[svg]]), and so
  does an input path of the command line: canvasnet render
  "chart.vl.json#param.cutoff=50" -o c.png (the fragment wins over --param for
  that input; a name the chart does not declare is then the warning
  vl-param-unknown instead of an error).
- The model options (-D, --view, ...) are a usage-error for a chart: -D is for
  the variables of a model and --param for the params of a chart. 'canvasnet
  info' reports the laid-out size, marks, and data references without drawing.
