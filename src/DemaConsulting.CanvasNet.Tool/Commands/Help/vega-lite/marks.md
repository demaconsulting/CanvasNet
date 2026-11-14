---
title: Marks
summary: The marks a chart can draw, including composite marks.
see-also:
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

## Known differences

- Composite marks (boxplot, errorbar, errorband) group by the bin, time unit,
  and aggregate of their channels as Vega-Lite does; the one difference is that
  the box of a box plot over a bin spans the whole bin, where Vega-Lite 6.5
  draws it from the middle of the bin to its end.
