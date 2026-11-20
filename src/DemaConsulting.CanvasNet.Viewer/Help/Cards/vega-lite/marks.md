---
title: Marks
summary: The marks a chart can draw, including composite marks.
tags:
  - vega-lite
  - authoring
see-also:
  - vega-lite/style
  - vega-lite/encodings
  - vega-lite/layout
examples:
  - name: bar
    kind: source
    language: json
    file: bar.vl.json
    description: A bar chart of inline data with a title.
  - name: line
    kind: source
    language: json
    file: line.vl.json
    description: A line chart over a temporal axis, with points.
  - name: scatter
    kind: source
    language: json
    file: scatter.vl.json
    description: A scatter plot colored by a nominal field.
---

## Supported

- Marks: bar, rect, point, circle, square, rule, tick, line, area, trail, text,
  arc, image, boxplot, errorbar, errorband.
- Corner radii: a bar or rect rounds its corners with cornerRadius and with
  cornerRadiusTopLeft, cornerRadiusTopRight, cornerRadiusBottomLeft, and
  cornerRadiusBottomRight; a bar rounds the corners at its value end with
  cornerRadiusEnd (the top of a bar above zero, the bottom of one below, the
  right or left end of a horizontal bar, and every corner of a ranged bar).

## Known differences

- A stacked bar with cornerRadiusEnd or another corner radius rounds each
  segment by itself (cornerRadiusEnd at the segment's own end); Vega-Lite
  rounds only the outer ends of the stack.
- Composite marks (boxplot, errorbar, errorband) group by the bin, time unit,
  and aggregate of their channels as Vega-Lite does; the one difference is that
  the box of a box plot over a bin spans the whole bin, where Vega-Lite 6.5
  draws it from the middle of the bin to its end.
