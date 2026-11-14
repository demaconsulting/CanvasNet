---
title: Channels, field types, and scales
summary: Encoding channels, field types and their defaults, scales, and color schemes.
aliases:
  - scales
  - channels
see-also:
  - vega-lite/marks
  - vega-lite/transforms
---

## Supported

- Channels: x, y, xOffset, yOffset (grouped bars and offset points within a band
  or a time unit), theta, radius, color, fill, stroke, size, opacity,
  fillOpacity, strokeOpacity, strokeWidth, strokeDash, shape, angle, text, url
  (image marks), order, detail, and the secondary channels x2, y2, theta2,
  radius2, and the xError and yError channels of errorbar and errorband.
- Field types quantitative, temporal, nominal, ordinal; aggregate, bin,
  timeUnit, stack, impute, sort, and condition.
- A field without a type gets Vega-Lite's default, by the first rule that
  applies: quantitative on latitude and longitude; nominal on row, column,
  facet, shape, and strokeDash; ordinal on order or with a sort array; temporal
  with a timeUnit; quantitative with a bin or an aggregate other than argmin and
  argmax; quantitative for a numeric or discretizing scale.type, temporal for
  time or utc; and nominal otherwise - also on x and y. A datum takes its type
  from its value (a number is quantitative, a date-time object temporal,
  anything else nominal), and x2, y2, theta2, and radius2 take the type of their
  main channel. Write the type when another is meant.
- Scales (linear, log, pow, sqrt, symlog, sequential, time, utc, band, point,
  ordinal, quantile, quantize, threshold, bin-ordinal), axes, legends, 68 named
  color schemes, titles, autosize, and container sizes. labelExpr of axes,
  legends, and facet headers (and config.axis and config.header) runs on
  datum.value and datum.label.

## Known differences

- Color schemes are interpolated piecewise in sRGB, not taken from the
  d3-scale-chromatic tables.
- A mark with invalid: "show" (or null) draws a row whose value is not a number
  at zero (or at the first value of the domain), and an entry of
  config.scale.invalid ("zero-or-min", or a number or text value) does the same
  for its channel whatever the invalid mode is (a value is the output of the
  property as it is, not scaled); a stacked row of a null stays in its slot, and
  a stacked channel ignores its config.scale.invalid entry. An invalid custom
  SVG path shape is a warning and a circle, where Vega fails.
