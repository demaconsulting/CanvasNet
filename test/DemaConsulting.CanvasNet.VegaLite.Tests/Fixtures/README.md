# Test fixtures of the Vega-Lite renderer

These specifications are **original works** written for this repository, with made-up data. They are not copies,
adaptations or reductions of any Vega-Lite example, gallery chart or `vega-datasets` data set, and the values in them
describe nothing real. They exist to give the tests small, readable charts whose geometry can be derived by hand.

- `bar-inline.vl.json`: a bar chart with a nominal x axis (horizontal labels), a quantitative y axis with a custom
  title, a chart title and a fixed size.
- `point-inline.vl.json`: a scatter plot of filled points with a quantitative x axis that does not start at zero, a
  color field, a size field and a row whose y value is null.
- `rule-tick-inline.vl.json`: a strip chart of tick marks on an ordinal y axis; the tests also replace the mark with
  `rule` (and change the tick thickness) to draw stems and thicker ticks.
- `titles-config.vl.json`: a title with a subtitle, a background, and the configuration of axes, bars and the title
  (`config`), plus an explicit scale domain and tick count.
- `unsupported.vl.json`: a line mark, a regression transform and a band position, which are recognized but not drawn yet
  and must be reported with their locations.
- `data-files.vl.json` and `data-files.csv`: the same bar chart as `bar-inline.vl.json`, but its rows come from the
  relative file `data-files.csv` (the specification is opened from its path, so the file is found beside it). The CSV
  has a quoted field with a comma, a quoted field with doubled quotes, an empty cell, a boolean column and an ISO date
  column, so the tests can check parsing and quoting (the cells stay text unless a test asks for `parse`); the chart must
  be pixel-identical to the inline one.
- `data-values.json`: a JSON document with the rows nested under `report.rows`, which the tests read with
  `format.property`. TSV, DSV, hostile and oversized data files are never fixtures: the tests write them to a
  temporary folder or an in-memory file set, so that no unusual file sits in the repository.
- `aggregate-bars.vl.json`: a bar chart of the sum of `units` per `region` (five made-up rows, three regions), whose
  aggregate comes from the encoding (`aggregate: "sum"`), so the bars are three sums in first-seen order.
- `histogram.vl.json`: twelve made-up parcel weights binned by the encoding (`bin: true`) and counted
  (`aggregate: "count"`), with the bins 0 to 35 by 5 and the counts 3, 2, 3, 1, 1, 1, 1.
- `stacked-bars.vl.json`: the sum of `units` per `region` split by `product` (a color), so that each region's
  bar is a stack of layers whose totals are 10 and 5.
- `binned-ranges.vl.json`: three made-up age ranges that are binned already (`bin: "binned"` with the end of each range
  in `x2`: 0 to 10, 10 to 25 and 25 to 30) and a ticket count for each, so that the bars are as wide as their ranges
  (49, 74 and 24 pixels, a pixel of bin spacing taken off every pair of edges).
- `window-cumulative.vl.json`: four made-up quarterly order counts, turned into a running total by a `window` transform
  (`sum`, frame up to the current row: 4, 7, 12 and 14) and drawn as bars on an ordinal axis.
- `signed-stacks.vl.json`: made-up takings (positive) and outgoings (negative) per quarter, split by `kind` (a color), so
  that each quarter's bar is a stack that hangs both above and below the zero line (the positive layers stack up from
  zero and the negative layers stack down from zero, separately; the domain is -20 to 20).
- `legend-symbols.vl.json`: made-up seed packet readings as filled points with a color field (`crop`, a legend with an
  explicit title) and a size field (`trays`), so that the chart has two symbol legends stacked at its right side.
- `legend-gradient.vl.json`: made-up rain gauge readings as points colored by a quantitative field (`rain`, 2.5 to 15 in
  steps of 2.5), so that the chart has one gradient legend with the default ramp.
- `legend-merged.vl.json`: made-up garden bed readings with a color, a shape and a size field, where the color and the
  shape read the same field (`bed`), so that they share one legend (symbols with both a shape and a color) and the
  size (`weeds`) has a legend of its own.
- `log-scales.vl.json`: four made-up daily colony counts (12, 95, 1200 and 8000) as bars on a logarithmic y axis, so that
  the domain is rounded out to 10 to 10000 (three decades of 100 pixels in a height of 300) and the bars start at the bottom.
- `binned-channels.vl.json`: eight made-up daily soil readings (`moisture` from 3 to 97) as circles whose color field is
  binned, so that the six-bin default gives the bins 0-20 to 80-100 (two, one, two, one and two readings), a bin-ordinal
  color scale with five explicit colors, and a legend with one entry per bin.

Phase 5 (expressions, static parameters and row transforms) adds no fixture file. Its tests build small specifications
inline, with made-up values that are written for each test (a handful of rows such as `{"x": 3}` or
`{"k": "a", "w": 2}`), and temporary files that the tests write to a temporary folder (a lookup source, an
out-of-sandbox path). Every one of them is an **original work** of this repository: no Vega-Lite or Vega example,
gallery specification, documentation snippet, `vega-datasets` data set, compiled Vega output or reference image was
copied, adapted or used as a source of expected values. The expected values (the positions of dots in the 100 by 100
charts, the reservoir of the sample transform, the order of sorted categories, the pixels of the colors) are derived by
hand from the scale formulas and from the algorithm that the tests describe, and the only change to an existing
fixture is that `unsupported.vl.json` now uses a window transform (a later phase) so that it still has an unsupported
transform to report. In Phase 6 the window transform and the aggregate became supported, so the same fixture now uses a
regression transform and a band position (both still unsupported) for the same purpose.

Line endings: the repository normalizes text files to LF (`.gitattributes` and `.editorconfig`), but a working tree may
hold CRLF files, depending on how it was checked out. Tests that use a fixture must therefore assert the parsed or
rendered meaning (rows, values, pixels) and never the raw bytes or the line endings of the file; do not rewrite a
fixture only to change its line endings.

Rules for adding fixtures:

- Write the specification yourself; do not paste from the Vega-Lite documentation, gallery or any example repository.
- Do not use data from `vega-datasets` or any other published data set; invent small, obviously fictional values.
- Keep each fixture small enough that the expected positions can be derived by hand in the test that uses it.
