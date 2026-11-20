---
title: Positions of Vega-Lite charts
summary: The views, axes, legends, titles, and marks of a chart, with view paths and values.
tags:
  - vega-lite
  - composition
see-also:
  - positions/selectors
  - positions/geometry
  - vega-lite/composition
---

## Kinds

- title (id `title`), view (the data rectangle of a chart; its id is its view
  path), axis (id: its channel, `x` or `y`), legend (id: its first channel,
  such as `color`), and mark.
- Every item has view, the path of its view: `""` for a single chart, then one
  segment per level: `hconcat[1]`, `vconcat[0].layer[1]`, `concat[2]`,
  `facet[3]` (the cells in layout order), `repeat[1]`. name is the `name` the
  specification gives the view. The layers a boxplot, errorbar, or errorband
  expands to belong to its own view.
- A mark has mark, its type, and the id `<mark>-<n>`: n counts the items of
  that type in its view from 0, in drawing order, as in `bar-2`. A line, area,
  or trail is one item per point (`line-0`, `line-1`, ...). The ids are stable
  for the same input, not across edits: select marks by their data or values.

## Values

- `values` has every encoding channel bound to a field or a datum, with the
  value after the transforms, not the label drawn: an aggregate (the sum), the
  start of a bin on `x` and its end on `x2`, the start of a time unit, a date
  as UTC ISO text (`"2025-01-01T00:00:00.000Z"`). A stacked channel has the
  value of its row. A cell of a facet adds `row`, `column`, or `facet`.
- `mark:x=B+color=Shipped` picks one bar of a grouped chart, and
  `axis:@id=y+@view="hconcat[1]"` one axis of a concatenation.

## Data

- `data` has the fields of the row the mark draws, after the specification's
  transforms (`calculate`, `fold`, ...) and before the encoding: scalars only
  (a nested object or array is left out), sorted by name, at most
  {{positionsMaxDataFields}} fields. A date the data gave as text keeps that
  text. An aggregated mark has its group fields (a time unit's as
  `month_month`) and its aggregates (`sum_revenue`, `__count`); a line, area,
  or trail point has its own row.
- A month time unit on `{"month": "2025-11-01"}` gives `x` the date
  `"2012-11-01T00:00:00.000Z"`: Vega-Lite puts a month alone in the year 2012,
  and the axis shows Nov. `data` keeps `"month": "2025-11-01"`, so
  `mark:month=2025-11-01+region=Americas` picks that point while
  `mark:x=2025-11-01` matches nothing. Select marks by data fields where you
  can.

## Attach points

- bar, rect, tick, image, view, axis, legend, and title: the center of the box.
- point, circle, and square: the center of the symbol; the box is its shape.
- rule: the middle of the segment. text: the anchor of the text; the box is the
  measured text.
- arc: the middle angle at the middle radius.
- line, area, and trail: the point itself (the top edge of an area), with a box
  of zero size.
