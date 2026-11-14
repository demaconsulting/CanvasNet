---
title: Gantt charts
summary: Tasks, dates, sections, excluded days, the axis, compact mode, and tags of a gantt chart.
children:
  - settings
see-also:
  - mermaid/options
  - mermaid/diagnostics
examples:
  - name: gantt
    kind: source
    language: mermaid
    file: gantt.mmd
    description: A small gantt chart with two sections, done, active, and critical tasks, after references, a milestone, and excluded weekends.
---

Gantt charts: a gantt chart starts with gantt. It is drawn by a port of
Mermaid's gantt renderer, with the time scale and the axis of d3, so the bars,
the texts, the section rows, and the ticks land where Mermaid 11 puts them, with
the dates in UTC; its syntax, tags, and settings are listed below and on the
settings card (`canvasnet help mermaid gantt settings`).

## Syntax

- Header: `gantt`; the keywords of the statements are case-insensitive.
- Tasks: `Name : data`, one per line; after the tags, the data is up to three
  fields separated by commas: an id, a start, and an end (`Design :des,
  2024-06-03, 5d`); with two fields, a start and an end; with one field, an end,
  the task starting where the task before it ends.
- Tags: `done`, `active`, `crit`, `milestone`, and `vert` before the fields, in
  any combination; they are listed below.
- Starts: a date, or `after id1 id2` (the latest end of the named tasks, which
  may come later in the text).
- Ends: a date, a duration (a number, with a fraction if needed, and one of the
  units `ms`, `s`, `m`, `h`, `d`, `w`, `M`, and `y`, such as `1.5d`), or `until
  id1 id2` (the earliest start of the named tasks).
- Dates: `dateFormat` with the dayjs tokens Mermaid loads (`YYYY`, `MM`, `DD`,
  `HH`, `mm`, `X`, `x`, and so on), read strictly; a start that does not match
  it, or any start without a `dateFormat` (Mermaid's default is empty), is read
  in the ISO forms (`2024-06-03`, `2024-06-03T09:30`), and with `dateFormat x`
  or `X` a start of digits only is milliseconds; an end that does not match the
  `dateFormat` and is no duration ends the task at its start, as in Mermaid
  (ends have no ISO forms), and an end in `X` is seconds; `inclusiveEndDates`
  makes an end date part of its task.
- Sections: `section Name` groups the tasks that follow it; the sections take
  the theme's section colors in turn, and `<br>` breaks a section name into
  lines.
- Excluded days: `excludes` with `weekends`, day names (`monday`), and dates;
  `includes` with dates that are worked anyway; `weekend friday` or `weekend
  saturday` (the default) for the first day of the weekend; a task whose end is
  a duration grows over the excluded days, which are shaded.
- Axis: `axisFormat` with the d3 time format directives (`%b %e`, `%H:%M`, and
  so on), `tickInterval` (a number and one of `millisecond`, `second`, `minute`,
  `hour`, `day`, `week`, and `month`, such as `1week`), and `weekday` (the first
  day of the week for weekly ticks); `gantt.topAxis` draws a second axis at the
  top (the `topAxis` statement fails in Mermaid, and is a `mermaid-syntax-error`
  here).
- Compact mode: `displayMode: compact` in the frontmatter, or
  `gantt.displayMode`, puts the tasks of a section on as few rows as keep them
  apart.
- Today: `todayMarker off`, or `todayMarker` with a style
  (`stroke-width:5px,stroke:#0f0`); the today line is drawn only when the caller
  gives the time (`MermaidRenderOptions.Now`).
- Interaction: `click id href "url"` and `click id call fn(args)` are read and
  never drawn as links, because a picture is static; the text of a task with a
  click is drawn bold, as in Mermaid.
- Titles: `title`, else the frontmatter `title`, is drawn above the chart;
  `accTitle` and `accDescr` are read as metadata.
- Colors: the theme variables `taskBkgColor`, `taskBorderColor`,
  `taskTextColor`, `taskTextDarkColor`, `taskTextOutsideColor`,
  `taskTextClickableColor`, `activeTaskBkgColor`, `activeTaskBorderColor`,
  `doneTaskBkgColor`, `doneTaskBorderColor`, `critBkgColor`, `critBorderColor`,
  `sectionBkgColor`, `sectionBkgColor2`, `altSectionBkgColor`,
  `excludeBkgColor`, `gridColor`, `todayLineColor`, `vertLineColor`, and
  `titleColor`.
- Configuration: YAML frontmatter, `%%{init: ...}%%` directives, the five
  themes, `themeVariables`, and the gantt settings below, with Mermaid's
  precedence.

## Tags

- `done`: A finished task: a bar in the theme's `doneTaskBkgColor`.
- `active`: A task in progress: a bar in `activeTaskBkgColor`.
- `crit`: A critical task: a bar in `critBkgColor` with a `critBorderColor`
  border, or with `done` or `active`, in their color with that border.
- `milestone`: A diamond the height of a bar at the middle of the task, so a
  milestone of `0d` is at its start.
- `vert`: A vertical line over every row at the start of the task, with its name
  below the rows.
