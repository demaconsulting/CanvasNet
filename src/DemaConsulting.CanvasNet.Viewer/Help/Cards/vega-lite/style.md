---
title: Styling Vega-Lite charts
summary: Config for titles, axes, legends, colors, and the frame, plus size and background of a chart.
tags:
  - vega-lite
  - design
see-also:
  - style
  - vega-lite/composition
  - vega-lite/encodings
  - vega-lite/options
examples:
  - name: styled
    kind: source
    language: json
    file: styled-chart.vl.json
    description: A finished grouped bar chart - a title with a subtitle, quiet axes, a legend on top, no frame, and its own two colors, all set in config.
---

Put the look in `config`, not in each encoding, so every chart of a document
matches: copy the `config` of [[vega-lite/style#styled]]
into each chart, render with `--scale 2`, and look at the PNG.

## Config that changes the picture

- `title`: `fontSize`, `subtitleFontSize`, `subtitleColor`, `anchor: "start"`
  (left-aligned, as in a report), and `offset`. Give the chart a title object:
  `"title": {"text": "Orders per month", "subtitle": "First half of 2024"}`.
- `axis`: `labelFontSize`, `titleFontSize`, `labelColor`, `titleColor`,
  `domainColor`, `tickColor`, and `gridColor`. `axisX` and `axisY` apply to
  one direction (`labelAngle: 0`, `domain: false`, `ticks: false`), and
  `axisBand`, `axisQuantitative`, and `axisTemporal` to one kind of scale.
- `legend`: `orient` (`top`, `bottom`, `left`, `right`), `labelFontSize`,
  `symbolType`, and `symbolSize`.
- `view`: `"stroke": null` removes the gray frame around the plot; `fill`
  colors the plot area; `continuousWidth` and `continuousHeight` size a chart
  without `width` and `height`.
- `range.category`: your category colors, in order (the default is
  `tableau10`). Per encoding, `"scale": {"scheme": "blues"}` picks one of the
  68 named schemes, and `"scale": {"range": [...]}` lists colors.
- `font` sets the family of every text, and `mark`, `bar`, `line`, `point`, and
  the other mark names set the defaults of the marks.
- A config key Vega-Lite does not define is ignored without a report, so check
  the PNG after each change.

## Size and spacing

- `width` and `height` size the plot area, not the whole picture; `padding`
  adds space around everything (16 suits a figure).
- Sort categories on purpose (`"sort": null` keeps the data order) and put the
  legend where the eye starts (`orient: "top"`).
- Draw with `--scale 2`, or fit with `--width`; vectors are drawn at the
  target size, so text stays sharp.

## Background

- A chart is drawn over white. `"background": "transparent"` in the chart, or
  `--background transparent`, keeps it clear.
- Inside an SVG drawing a chart keeps its own background: write
  `"background": "transparent"` in the chart when the page has a color.
- Text is drawn with the bundled Liberation fonts, so a chart looks the same on
  every machine.
