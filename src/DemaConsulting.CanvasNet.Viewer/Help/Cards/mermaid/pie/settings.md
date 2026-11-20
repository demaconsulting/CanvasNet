---
title: 'Pie charts: settings and known differences'
summary: The pie settings and the known differences of a pie chart.
tags:
  - mermaid
  - authoring
see-also:
  - mermaid/pie
  - mermaid/differences
---

Set these in the YAML frontmatter (`config:`) or in an `%%{init: ...}%%`
directive, with Mermaid's precedence.

## Settings

- `pie.textPosition` (default 0.75): Where the percentage labels are, as a part
  of the radius (0 is the center, 1 the edge).
- `pie.donutHole` (default 0): The size of a hole in the middle, as a part of
  the radius; a value above 0 and at most 0.9 draws a donut.
- `pie.legendPosition` (default `right`): Where the legend is: `right`, `left`,
  `top`, `bottom`, or `center` (over the pie); any other value is `right`.
- `pie.highlightSlice` (default empty): The label of a slice that is drawn
  enlarged and opaque; `hover`, which enlarges the slice under the pointer in
  Mermaid, enlarges none in a picture.
- `pie.useMaxWidth` (default `true`): No meaning for a picture.

## Known differences

- The opacity of a pie slice, a gantt section row, and the gantt grid is folded
  into its colors, so where two of them overlap the colors can differ slightly;
  the turn of a milestone and the enlargement of a highlighted slice are applied
  to the outline, and the crisp edges of the gantt grid are drawn antialiased.
