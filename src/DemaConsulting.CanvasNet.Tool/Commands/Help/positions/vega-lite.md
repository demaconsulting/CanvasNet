---
title: Positions of Vega-Lite charts
summary: The views, axes, legends, titles, and marks of a chart, with view paths and values.
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
  for the same input, not across edits: select marks by their values.

## Values

- `values` has every encoding channel bound to a field or a datum, with the
  value after the transforms, not the label drawn: an aggregate (the sum), the
  start of a bin on `x` and its end on `x2`, the start of a time unit, a date
  as UTC ISO text (`"2025-01-01T00:00:00.000Z"`). A stacked channel has the
  value of its row. A cell of a facet adds `row`, `column`, or `facet`.
- `mark:x=B+color=Shipped` picks one bar of a grouped chart, and
  `axis:@id=y+@view="hconcat[1]"` one axis of a concatenation.

## Attach points

- bar, rect, tick, image, view, axis, legend, and title: the center of the box.
- point, circle, and square: the center of the symbol; the box is its shape.
- rule: the middle of the segment. text: the anchor of the text; the box is the
  measured text.
- arc: the middle angle at the middle radius.
- line, area, and trail: the point itself (the top edge of an area), with a box
  of zero size.
