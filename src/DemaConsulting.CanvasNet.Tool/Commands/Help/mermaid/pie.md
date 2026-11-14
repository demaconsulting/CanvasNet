---
title: Pie charts
summary: Slices, the legend, titles, and colors of a pie chart.
children:
  - settings
see-also:
  - mermaid/options
  - mermaid/diagnostics
examples:
  - name: pie
    kind: source
    language: mermaid
    file: pie.mmd
    description: A small pie chart with a title and the values in the legend (showData).
---

Pie charts: a pie chart starts with pie. It is drawn by a port of Mermaid's pie
renderer, so the slices, the percentage labels, the legend, and the title land
where Mermaid 11 puts them; its syntax and settings are listed below and on the
settings card (`canvasnet help mermaid pie settings`).

## Syntax

- Header: `pie`, optionally followed by `showData` (the legend shows each value
  after its label) and a `title` on the same line.
- Slices: `"label" : value`, one per line: a label in double or single quotes,
  with backslash escapes, and a value that is a whole or a decimal number (`42`,
  `42.96`); a label seen before is ignored, and a negative value is a
  `mermaid-syntax-error`, as in Mermaid.
- Drawing: the slices in the order of the text, clockwise from the top, each
  labeled with its percentage rounded to a whole number; a slice under 1% of the
  total is left out of the pie and kept in the legend; the theme colors `pie1`
  to `pie12` repeat after twelve slices.
- Legend: a colored square and the label of each slice, at the right of the pie
  unless `pie.legendPosition` says otherwise.
- Titles: `title` (on its own line or after `pie`), else the frontmatter
  `title`, is drawn above the pie; `accTitle` and `accDescr` (on one line, or in
  braces over several lines) are read as metadata.
- Comments: `%%` comments, on their own line or after a slice.
- Colors: the theme variables `pie1` to `pie12`, `pieTitleTextSize`,
  `pieTitleTextColor`, `pieSectionTextSize`, `pieSectionTextColor`,
  `pieLegendTextSize`, `pieLegendTextColor`, `pieStrokeColor`, `pieStrokeWidth`,
  `pieOuterStrokeColor`, `pieOuterStrokeWidth`, and `pieOpacity`.
- Configuration: YAML frontmatter, `%%{init: ...}%%` directives, the five
  themes, `themeVariables`, and the pie settings below, with Mermaid's
  precedence.
