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
- `unsupported.vl.json`: a line mark, a calculate transform and an aggregate, which are recognized but not drawn yet
  and must be reported with their locations.

Rules for adding fixtures:

- Write the specification yourself; do not paste from the Vega-Lite documentation, gallery or any example repository.
- Do not use data from `vega-datasets` or any other published data set; invent small, obviously fictional values.
- Keep each fixture small enough that the expected positions can be derived by hand in the test that uses it.
