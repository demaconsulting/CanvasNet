---
title: 'Gantt charts: settings and known differences'
summary: 'The gantt settings and the known differences of a gantt chart: dates, today, and width.'
tags:
  - mermaid
  - authoring
see-also:
  - mermaid/gantt
  - mermaid/differences
---

Set these in the YAML frontmatter (`config:`) or in an `%%{init: ...}%%`
directive, with Mermaid's precedence.

## Settings

- `gantt.barHeight` (default 20): The height of a bar.
- `gantt.barGap` (default 4): The space between two rows.
- `gantt.topPadding` (default 50): The space above the first row and below the
  last.
- `gantt.leftPadding` (default 75): The space left of the time axis, where the
  section names are.
- `gantt.rightPadding` (default 75): The space right of the time axis.
- `gantt.gridLineStartPadding` (default 35): The top of the grid lines and of a
  vertical marker.
- `gantt.titleTopMargin` (default 25): The baseline of the title.
- `gantt.fontSize` (default 11): The font size of the task texts.
- `gantt.sectionFontSize` (default 11): The font size of the section names.
- `gantt.numberSectionStyles` (default 4): The number of section colors that
  repeat.
- `gantt.axisFormat` (default `%Y-%m-%d`): The format of the axis labels of a
  chart without `axisFormat`.
- `gantt.tickInterval` (default unset): The interval of the ticks of a chart
  without `tickInterval`.
- `gantt.topAxis` (default `false`): `true` draws a second axis at the top.
- `gantt.displayMode` (default empty): `compact` puts the tasks of a section on
  as few rows as keep them apart.
- `gantt.useWidth` (default unset): The width of the chart; unset is 784, the
  width Mermaid takes in the page of `mmdc`.
- `gantt.weekday` (default `sunday`): Accepted, and without effect as in
  Mermaid; the `weekday` statement applies.
- `gantt.useMaxWidth` (default `true`): No meaning for a picture.

## Known differences

- The today of a gantt chart is the time the caller gives
  (MermaidRenderOptions.Now), never the clock; the tool, the Viewer, and the
  Pandoc filter give none, so the today line is not drawn and 1970-01-01 is
  taken as today (mermaid-gantt-today-unavailable where it matters). Write
  todayMarker off, or give the dates in full, to keep the warning away.
- The dates of a gantt chart are in UTC, where Mermaid uses the time zone of the
  browser, so a chart never changes with the machine or with daylight saving
  time.
- A gantt start date that matches neither the dateFormat nor an ISO form
  (2024-1-5, Jan 5 2024, which browsers read in their own ways) is a located
  mermaid-render-failed Invalid date.
- Mermaid fails, or draws a broken chart, for the topAxis statement, a task
  whose data holds only tags, a first task without a start, a task with more
  than three fields, after or until references that go in a circle, excluded
  days that never end, a compact section named like a JavaScript object member
  (constructor, toString), and a dateFormat with the week (w, ww, wo, gggg) or
  zone name (z, zzz) tokens; these are a located mermaid-syntax-error or
  mermaid-render-failed instead.
- A gantt chart is 784 pixels wide, the width Mermaid takes in the page of mmdc,
  unless gantt.useWidth is set (in a browser it takes the width of the page),
  and its ticks keep d3's half-pixel offset at every scale.
- The links of a gantt chart (click ... href) are always checked as at Mermaid's
  strict level and kept only as data.
