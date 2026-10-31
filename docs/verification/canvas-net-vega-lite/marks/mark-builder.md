### MarkBuilder Unit Verification Design

This document describes the unit-level verification strategy for the `MarkBuilder` unit in the
`DemaConsulting.CanvasNet.VegaLite.Marks` namespace: the internal `MarkBuilder` class (`Build` and the static
`IsSupported`), whose partial files `MarkBuilder.Order.cs`, `MarkBuilder.Paths.cs`, `MarkBuilder.Overlays.cs`,
`MarkBuilder.Text.cs`, and `MarkBuilder.Arcs.cs` hold the draw order, the path marks, the overlays, the text mark, and the
arc mark. The unit reads the rows of a `DataTable` and the resolved scales of a view and creates, in a `marks` group,
the scene items of a `bar`, `rect`, `point`, `circle`, `square`, `rule`, `tick`, `line`, `area`, `trail`, `text`, or
`arc` mark (roles `mark-<type>`) with the geometry and the paint that Vega-Lite assigns: positions from the scale models,
band sizes, default sizes, the filled or outlined appearance, the colors, the sizes, the opacities, the invalid-data
rules, the grouping, sort, and curves of the paths (through the `CurveBuilder` unit, verified in the _CurveBuilder Unit
Verification Design_), the overlays, the formatted labels, the polar angles and radii, and the scene item budget.

#### Verification Approach

The unit is verified through the shared helper `ChartTestHelpers.Marks`, which runs the real `SpecParser`,
`ConfigResolver`, `DataLoader`, and `ScaleResolver` for an original specification and calls the builder for a data
rectangle of a chosen size. The assertions are on the scene items (positions, sizes, fills, strokes, stroke widths,
opacities, shapes, angles, corner radii, the segments of paths, the runs of trails, the angles and radii of arcs, and the
text and lines of labels). No mocking or stubbing is used; the collaborators are real because the unit's
contract is how the table, the scales, and the configuration combine into items. The expression-reference scenario
builds the `RowExpressions` (the compiled expressions of the tree and a real `ExpressionEvaluator` with its own
parameter dictionary) by hand and passes them to the builder, because `ChartTestHelpers.Marks` builds the builder
without them. The internal types are reached through
the test assembly's `InternalsVisibleTo` access.

The expected values are derived by hand from the band, point, and linear scale formulas and the Vega-Lite mark rules,
with the arithmetic stated in the tests (positions are rounded to nine decimals before comparison):

- Vertical bars: four categories over 240 pixels with the bar padding (inner 0.1, outer 0.05) have step
  240 / (4 - 0.1 + 0.1) = 60, band width 54, and the first band at (240 - 60 x 3.9) / 2 = 3, so the bars are at
  x = 3, 63, 123, and 183 with width 54; the y scale of 0 to 91 is nice to 0 to 100 over 200 pixels, so
  y(v) = 200 - 2v and the bars for 43, 28, 55, and 91 start at 114, 144, 90, and 18 with heights 86, 56, 110, and 182
  (down to y(0) = 200); the fill is the mark color `#4c78a8` with no stroke, opacity 1, and corner radius 0.
- Horizontal bars: four bands over 200 pixels have step 50, band width 45, and the first band at 2.5, giving
  y = 2.5, 52.5, 102.5, 152.5; x(v) = 2.4 v over 240 pixels, so the widths are 103.2, 67.2, 132.0, and 218.4 from
  x(0) = 0.
- Negative values: a y domain of -10 to 30 over 200 pixels has y(v) = 5 (30 - v), so the bar of -10 hangs from
  y(0) = 150 to y(-10) = 200 (y 150, height 50) and the bar of 30 spans 0 to 150; a bar on a measure axis whose explicit
  domain does not contain zero starts at the stack start 0, as Vega-Lite draws it (y(60) = 50 down to y(0) = 125 for a
  domain of 20 to 100 over 100 pixels, so y 50, height 75, overflowing the data rectangle and not clipped, because only
  `clip: true` clips), while with `stack: null` it runs to the first domain value (y(20) = 100, so y 50, height 50).
- Continuous and absent channels: two quantitative channels give bars `continuousBandSize` (5) wide centered on x(v);
  a bar without a y channel spans the whole height; without an x channel it is centered and 0.9 of the width (x = 5,
  width 90 in 100 pixels); an explicit `size` of 20 is centered on the band (the East band is 3 to 57, its center 30,
  so x = 20); a `rect` with two discrete channels fills bands of 50 with no padding at (0, 0) and (50, 50); marks are
  clipped to the data rectangle only with `clip: true`.
- Symbols: x of 0 to 100 over 300 pixels (3 per unit) and y of 0 to 50 over 200 pixels (200 - 4 v) put the points at
  (0, 200) and (300, 0); the row with no y value is dropped (Vega-Lite's default invalid mode for these marks is
  `filter`); a `point` is an outline (a transparent fill, the mark color as a 2 pixel stroke, size 30, opacity 0.7),
  while `circle` and `square` (and a filled point) are filled with no stroke and the same reduced opacity; a missing
  channel puts the symbol in the middle (y = 100 of 200); a point scale of three categories over 300 pixels has step
  300 / (3 - 1 + 2 x 0.5) = 100 and the first point at (300 - 200) / 2, so the points are at 50, 150, 250.
- Rules: x of 0 to 60 over 100 pixels gives x(v) = 100 v / 60, so the rules for 20 and 60 are vertical at 33.33 and 100
  across the whole height; with only y, they are horizontal across the whole width, drawn (as Vega-Lite does) from
  x = width back to x2 = 0, at y(v) = 100 - 100 v / 60; with x as the measure and y as bands (rule bands have no
  padding: step 50), the stems are at the band centers 25 and 75 from x(v) back to x(0), 3 pixels thick when
  `strokeWidth` is 3; rules are opaque black, not made translucent.
- Ticks: the tick bands have inner padding 0.25 and outer 0.125, so two bands over 100 pixels have step
  100 / (2 - 0.25 + 0.25) = 50, band width 37.5, and the first band at 6.25; a vertical tick is 1 pixel thick
  centered on x(v) (x = 33.33 - 0.5, width 1, y 6.25, height 37.5; the second at 99.5, 56.25), a horizontal tick with
  `thickness` 3 spans its band and is 3 pixels thick around y(v) (y = 100 - 33.33 - 1.5), and two measures give a
  vertical tick that is 5 pixels (`continuousBandSize`) long and 1 pixel thick; ticks have the mark color at opacity
  0.7.
- Paint: with a nominal color of categories a and b and quantitative size and opacity fields, the tableau10 colors in
  sorted category order are `#4c78a8` and `#f58518`; the size is 4 + 357 q / 30 for q = 10, 20, 30 (123, 242, 361)
  because the size domain starts at zero (0 to 30) and the range is 4 to (0.95 x 20)^2 = 361; the opacity is
  0.3 + 0.5 (o - 2) / 2 for o = 2, 3, 4 (0.3, 0.55, 0.8) because opacity has no zero rule.
- Stacked layers (two bands of 45 pixels at 2.5 and 52.5, the layers `p` 3 and `q` 5 in region `a` and `p` 4 in `b`):
  the layers are ordered by the color, descending on a vertical chart, so `q` is the lower layer (0 to 5) and `p` the
  upper (5 to 8) over the domain 0 to 8, y(v) = 100 - 12.5 v, giving the bars (y 37.5, height 62.5) and (y 0, height
  37.5) and for `b` (y 50, height 50); `center` puts the stack of `b` at 2 to 6 (y 25, height 50); `normalize` divides
  by the totals 8 and 4 so `b` fills 0 to 1 (y 0, height 100); `stack: null` overlaps the bars from zero on the domain 0
  to 5; a horizontal chart stacks ascending, `p` 0 to 3 and `q` 3 to 8 with x(v) = 12.5 v; and a circle sits at the
  upper end of its layer (y 0, 37.5, and 50).
- Binned bars (bins 0 to 30 by 10 holding 2, 2, and 1 rows over 90 pixels, x(v) = 3 v, the count domain 0 to 2): a bar
  spans its bin less the bin spacing of 1 (half a pixel off each edge) and plus the half pixel of the axis translate, so
  the bars are at x = 1, 31, and 61 with the width 29 (heights 100, 100, and 50); `binSpacing` 0 gives x = 0.5, 30.5,
  and 60.5 with the width 30, and 4 gives x = 2.5, 32.5, and 62.5 with the width 26; a reversed scale mirrors the bins
  (61, 31, 1); a `size` of 10 centers the bar on the middle of its bin (x = 10, 40, 70) without spacing; a `rect` has no
  spacing (x = 0.5 and 30.5 and 60.5, width 30); bins of 3 pixels under a `minBandSize` of 5 are widened to 5 around
  their middle; a circle sits at the middle of its bin (15, 45, 75); and a pre-binned field with a `step` and no `x2`
  ends one step after each start (x = 1, 31, 61).
- Pre-binned ranges (0 to 10 and 10 to 25 over 100 pixels, no zero and no nice, so x(v) = 4 v): the bars are at x = 1
  and 41 with the widths 39 and 59, the rects at 0.5 and 40.5 with the widths 40 and 60, and the circles at 20 and 70.
- Secondary channels (x(v) = 10 v on the domain 0 to 10): a bar from `lo` 2 to `hi` 6 and from 4 to 10 is at x = 20 and
  40 with the widths 40 and 60; a second end that is the pixel value 50 or the datum 1 on the domain 0 to 2 (50 pixels)
  draws from 50 to 100; a row with a null end is not drawn; a rule with `y` and `y2` at the band centers 25 and 75 runs
  from y(lo) to y(hi), one with `x` and `x2` from x(lo) to x(hi), and a rule with both ranges is the segment from (1, 2)
  to (5, 8), drawn from (20, 75) to (100, 0).
- Line chart (the 300 by 200 view with the rows x = 0, 1, 2 and y = 0, 10, 5, both quantitative): the x domain is 0 to 2
  (zero is included), so x(v) = 150 v, and the y domain is 0 to 10, so y(v) = 200 - 20 v; the points are (0, 200),
  (150, 0), and (300, 100), the path is `M0,200 L150,0 L300,100` in the default color `#4c78a8` with the stroke width 2
  of Vega's line style, no fill, no dash, opacities 1, no overlay symbols, and no clip. Two series that alternate
  (a: y = 1,
  2 and b: y = 5, 6 at x = 0, 1, domain 0 to 6, so y(v) = 200 - 100 v / 3) are the two paths
  `M0,166.666667 L300,133.333333` (blue) and `M0,33.333333 L300,0` (orange) in the order of the first row of each
  series; a detail field splits one series into `M0,150 L300,100` and `M0,50 L300,0` (y(v) = 200 - 50 v on the domain 0
  to 4) with the same color, and without a grouping field the four rows are one path. The rows are sorted by the pixel
  x, so the shuffled rows x = 2, 0, 1 draw the same path; an `order` field o = 1, 3, 2 for x = 0, 1, 2 visits
  x = 0, 2, 1
  (`M0,200 L300,100 L150,0`) ascending and x = 1, 2, 0 (`M150,0 L300,100 L0,200`) descending; a horizontal line (the
  measure on x with the domain 0 to 10, so x(v) = 30 v, and t = 0, 2, 1 on y with y(t) = 200 - 100 t) is sorted by the
  pixel y, `M300,0 L150,100 L0,200`. A size field over the domain 0 to 4 and the range 2 to 10 gives the widths 2 + 8 x
  1 / 4 = 4 and 2 + 8 x 3 / 4 = 8 and splits the paths by size; the `size` property 5, the `strokeWidth` property 3, a
  size value 7, and the expression `2 * 3` give the widths 5, 3, 7, and 6.
- Area polygon (the same view): the baseline is y(0) = 200 and the polygon is
  `M0,200 L150,0 L300,100 L300,200 L150,200 L0,200 Z` filled with `#4c78a8`, no stroke, opacity 1. An unstacked area
  on the
  domain 4 to 10 for the rows y = 6, 10, 8 has the baseline y(4) = 200 (the first domain value) and the top at
  y(6) = 200 - 200 x 2 / 6 = 133.333333 and y(8) = 66.666667,
  `M0,133.333333 L150,0 L300,66.666667 L300,200 L150,200 L0,200 Z`;
  a nominal x of three points over 300 pixels (step 100, padding 0.5) puts the vertices at 50, 150, and 250. A
  horizontal area (x the measure on 0 to 10, so x(v) = 30 v, and y(t) = 200 - 100 t for t = 0, 1, 2) is sorted by the
  pixel y, so the top line runs along x = 150, 300, 0 for t = 2, 1, 0 and the baseline x(0) = 0 runs back,
  `M150,0 L300,100 L0,200 L0,200 L0,100 L0,0 Z`; a range area with `y2` = 1, 2, 3 for y = 4, 10, 6 (y(v) = 200 - 20 v)
  is the band `M0,120 L150,0 L300,80 L300,140 L150,160 L0,180 Z`, and an area without its measure channel draws nothing.
- Stack and impute (a: y = 2 at x = 0, 1, 2 and b: y = 1 at x = 0 and 2, with x(v) = 150 v and the stack domain 0 to 3,
  so y(v) = 200 - 200 v / 3): the layers are stacked in descending order of the color, so b is at the bottom and a on top
  of it, and the missing key x = 1 of b is imputed with 0. The layer b has the top 133.333333, 200, 133.333333 and the
  baseline 200, `M0,133.333333 L150,200 L300,133.333333 L300,200 L150,200 L0,200 Z`; the layer a runs from the top of
  b to
  that plus 2: its top is y(3) = 0, y(2) = 66.666667, y(3) = 0 and its baseline is the top of b,
  `M0,0 L150,66.666667 L300,0 L300,133.333333 L150,200 L0,133.333333 Z`, so both polygons have seven segments and the
  layers meet vertex for vertex. With `impute: null` b has the two vertices of its own rows,
  `M0,133.333333 L300,133.333333 L300,200 L0,200 Z`.
- Invalid rows (x = 0 to 4, so x(v) = 75 v, y = 1, 2, null, 3, 4 on the domain 0 to 4, so y(v) = 200 - 50 v): the default
  mode breaks the line, `M0,150 L75,100 M225,50 L300,0`, as do the three other modes that start with `break-paths` and
  a text that is not a number (`"n/a"`); the filter mode connects the neighbors, `M0,150 L75,100 L225,50 L300,0`, from the
  mark, or from `config.line` when the mark says nothing (the mark wins over the configuration); an unstacked area
  gives the two polygons `M0,150 L75,100 L75,200 L0,200 Z M225,50 L300,0 L300,200 L225,200 Z` or, in the filter mode,
  `M0,150 L75,100 L225,50 L300,0 L300,200 L225,200 L75,200 L0,200 Z`; the `show` mode and null behave as the default mode
  and the catalog reports them at `/mark/invalid`; a mode that is not known in the configuration is ignored.
- Trail (the same view): the points are the pixels (0, 200), (150, 0), and (300, 100) with the sizes 4, 8, and 12 of the
  identity size scale (domain 4 to 12, range 4 to 12), in one run, filled with `#4c78a8` and not stroked; without a size
  channel every point has the size 2 of Vega's trail style, and the `size` property (9) and the expression `1 + 4`
  replace it. With the rows x = 0 to 4 and y = 1, 2, null, 3, 4 the trail has the two runs (0, 150), (75, 100) and
  (225, 50),
  (300, 0); a run of one point is dropped, and a trail of single points is not drawn.
- Curves chosen by the mark (the 100 by 100 view with the identity scales, so the rows y = 100, 50, 75 at x = 0, 50,
  100 are
  the pixels (0, 0), (50, 50), (100, 25)): linear `M0,0 L50,50 L100,25`; step (the change at the middle between two
  points, with the final line) `M0,0 L25,0 L25,50 L75,50 L75,25 L100,25`; step-after `M0,0 L50,0 L50,50 L100,50 L100,25`;
  step-before `M0,0 L0,50 L50,50 L50,25 L100,25`; cardinal with the tension 1 (k = 0) `M0,0 C0,0 50,50 50,50 C50,50 100,25
  100,25`; the cardinal curve of the default tension 0 differs from it, starts with `M0,0 C`, and ends at `100,25`; the
  monotone curve equals the monotone-x path that `CurveBuilder` writes for the same points; a trail ignores the
  interpolation. An area with the bundle interpolation is drawn with the linear polygon of the line chart, while the bundle
  line has cubic segments.
- Overlays (the line chart): a line with `point: true` and the color green is the stroked path (green) followed by three
  filled `#4c78a8` circles of the size 30 and the opacity 1 at (0, 200), (150, 0), and (300, 100), because the color
  of the
  mark is not inherited; the budget is the marks group 1, the path 1 + 3 + 6 = 10 (below), and the three symbols,
  1 + 10 + 3 =
  14. A `point` object replaces the properties (red, size 100, square, opacity 0.5), `false` and null give no
  overlay, a row
  without a y has no point (two of three), a rule of x = 1, 2, 3 with `point: true` on the domain 0 to 3 has its
  points at
  (100, 100), (200, 100), and (300, 100), and a trail has its points after it. An area with `line: true` is the polygon
  with the opacity 0.7 followed by the line `M0,200 L150,0 L300,100` (blue, width 2, opacity 1); the line takes the
  interpolation of the area (the step line `M0,200 L75,200 L75,0 L225,0 L225,100 L300,100`); an opacity of 0.5 gives the
  area 0.5, a fill opacity of 0.4 leaves its opacity 1 and gives the fill opacity 0.4, a point overlay also gives 0.7, and
  neither overlay gives 1;
  a clip of the mark clips the group. For two stacked areas of one at x = 0, 1 (the stack domain 0 to 2, y(v) = 200 -
  100 v) the lines are at the stack end of their layer, a at y(2) = 0 and b at y(1) = 100, `M0,0 L300,0` and
  `M0,100 L300,100`. The configuration `config.area` with `line: true` and `point: { color: black }` draws the area, the
  line, and three black points, `config.line.point: true` the points of a line, and a mark with `false` switches them
  off; `"transparent"` draws three symbols with the opacity 0; a color field colors the points by group.
- Draw order: for the points x = 0, 1, 2 (x(v) = 150 v) with the order values 3, 1, 2 the symbols are drawn at
  x = 150, 300,
  0 ascending and 0, 300, 150 descending (the data order without the channel); with equal first values the second
  field breaks the ties (x = 150, 0, 300). Three bars of a nominal x (step 100, band 90, first band at 5) with the order
  values 2, 3, 1 and `stack: null` are drawn at x = 205, 5, 105; with the default stack they keep the data order, and the
  order channel of a stack orders its layers and not the drawing. With `order: null` (or `false`, as a channel value
  or a
  mark property) the symbols keep the data order x = 300, 0, 150 for the rows x = 2, 0, 1; the line keeps it too,
  `M300,100 L0,200 L150,0` (it is sorted by x otherwise), and the area runs through the points in the data order,
  `M300,100 L0,200 L150,0 L150,200 L0,200 L300,200 Z`.
- Charging of paths: a line of three points in one run has the bound 3 + 6 = 9 segments, charged with its item before
  the segments exist, so the marks group (1), the path (1), and the 9 segments make a budget of 11 that passes exactly and
  a budget of 10 that fails at `/mark` naming `MaxSceneItems` (the count 11 is reached before the failure); an area has
  the bound 2 x 3 + 6 = 12, so 1 + 1 + 12 = 14; a trail costs the group, the item, and its three points, 5; a group
  without two defined points (the series b of one row and c of two rows without y) draws nothing and costs nothing, so the
  line chart of three series costs 1 + 1 + 3 + 6 = 11. A line of 2,000 points fails a limit of 500 items. Reading 640 rows
  costs 10 evaluation steps (one for 64 elements) and the grouping and the sort add more, so a limit of 5 steps fails
  at `/mark` naming `MaxEvaluationSteps`.
- Text (the 300 by 200 view, x = 0, 2 over 0 to 2 and y = 0, 10): the labels sit at x(v) = 150 v and y(v) = 200 - 20 v,
  that is
  (0, 200) and (300, 0), centered with the middle baseline, 11 pixels, sans-serif, black, opacity 1, no offsets,
  angle 0, no
  limit, the ellipsis U+2026, and the default line height; without x and y the label sits at the middle (150, 100). A
  size channel over the domain 0 to 10 and the range 8 to 40 gives the font sizes 8 + 32 x 5 / 10 = 24 and 40; a size
  value 15, `fontSize` 20 (which the channel wins over), `size` 9, the expression `datum.s + 1` (6 and 11), and the
  default 11 follow the documented order. The d3 number format `,.1f` gives `1,234.5` for 1234.5, the default format
  `1234.5` (12 significant digits, trimmed), `.0%` gives `26%` for 0.256, `numberFormat: .1f` of the configuration gives
  `3.0` for 3 (the channel format `.2f` wins, `3.00`), a null number is the text `0`, any other null is `null`; a date
  `2020-03-05` is `2020/03/05` by `%Y/%m/%d`, `Mar 05, 2020` by the default time format, `05.03.2020` by
  `config.timeFormat` `%d.%m.%Y`, and `Mar 2020` for the unit `yearmonth`; the instant 1583366400000
  (2020-03-05T00:00Z) is
  `2020-03-05` under the format type `time` or `utc`; the bins of width 10 give `0 – 10` and `10 – 20` (with an en
  dash and
  `0.0 – 10.0` under `.1f`), and a count of three rows gives `3`. A custom format type gives the default format. An
  array value is one line per element (`a`, `b`, text `a\nb`, the line height left unset), a null element is an empty
  line, the `lineBreak` `|` splits `a|b|c` into three lines and `|a` into an empty line and `a`, and an array is never
  split; an empty label draws nothing.
- Text limits: with `MaxStringLength` 16 the number format `0>16d` makes every label exactly 16 characters, so 16 rows fit
  the total of 16 x 16 = 256 characters and a 17th row fails at `/mark` (the message says `together`), a label of 17
  characters fails on its own, and a time label of 22 characters fails a limit of 10; a label of 6,400 characters costs
  6,400 / 64 = 100 evaluation steps and fails a limit of 50 (and is drawn by default). Two labels of the three lines
  a, b, c cost the marks group and 2 x (1 + 2) items, 1 + 6 = 7, so a budget of 7 passes and a budget of 6 fails on the
  second label (the count 7), and a budget of 3 fails on the first (the count 4).
- Pie (the 200 by 200 view, the values 1, 1, 2 stacked on theta with the color k = a, b, c): the theta domain is 0 to 4
  on the range 0 to 2 PI, so the slices span 0 to PI / 2, PI / 2 to PI, and PI to 2 PI (the stack [0, 1], [1, 2], [2, 4]),
  with the outer radius 100 (half of the smaller side) and the inner radius 0 around the middle (100, 100), filled with
  the three tableau10 colors, not stroked, with no pad angle and no corner radius; on a 300 by 120 view the center is
  (150, 60) and the outer radius 60; one value without a color is the whole circle, an unstacked value 1 or 2 on the
  domain 0 to 2 is half and the whole circle, and an arc without a channel is a disc. A donut (`innerRadius` 50) has the
  inner radius 50; `outerRadius` 80 and `innerRadius` 30 are aliases of `radius` and `radius2`, which win when both are
  given (90 and 10), and the larger radius is the outer one. A radial chart on the 300 by 200 view (the outer radius 100)
  with the values 1, 4, 9 on a square root radius scale with zero and `rangeMin` 20 has the domain 0 to 9, so the outer
  radii are 20 + 80 sqrt(v / 9) = 20 + 80 / 3, 20 + 160 / 3, and 100 and the inner radius is the scaled zero, 20, and theta
  stacks the values over 0 to 14, so the slices are [0, 1], [1, 5], [5, 14] of 2 PI. A radius stacked by the color
  makes rings on the domain 0 to 4 over 0 to 100: [0, 25], [25, 50], [50, 100] with the full circle; a band scale of
  three categories on theta gives each a third of the circle (2 PI / 3 each); a ranged arc theta 1 to 3 on 0 to 3 and
  radius 20 to 60 on 0 to 60 is the sector 2 PI / 3 to 2 PI with the ring 100 / 3 to 100; constant channels and mark
  properties give the angles (0.5 to 2) and the radii (10 to 70). The pad angle 0.05, the corner radius 4, the stroke
  `white` with the width 2, the opacities 0.8, 0.5, and 0.25 and the dash 4, 2 reach every slice (and the color channel
  wins over the mark color for the fill), the configuration gives the pad angle and the corner radius (the mark wins),
  and a fill channel and a stroke value drive the paint. The center is the pixel pair of the `x` and `y` values (60, 80),
  the far edges for the keywords `width` and `height` (300, 200), the points of two categories (a point scale of 300
  pixels has the step 150 and the first point at 75, so the centers 75 and 225, y = 100), or the scaled quantitative
  values ((0, 200) and (300, 0)); the radius still follows the smaller side. A pie of three slices costs the marks
  group and three items (4), a budget of 3 fails on the third slice (the count 4), a row without a number costs nothing,
  and 200 rows cost 200 elements = 3 whole evaluation steps (200 / 64), which a limit of 2 refuses.
- Pie labels and polar offsets (the 200 by 200 view, the center (100, 100), the radius 60, the stack as for the pie): the
  anchor is `x = cx + r sin(theta)` and `y = cy - r cos(theta)`, and the label sits at the middle of its slice, the angles
  PI / 4, 3 PI / 4, and 3 PI / 2: label a at (100 + 60 sin(PI / 4), 100 - 60 cos(PI / 4)) = (142.4264, 57.5736),
  label b at
  (100 + 60 sin(3 PI / 4), 100 - 60 cos(3 PI / 4)) = (142.4264, 142.4264) (the diagonal 60 sqrt(0.5) = 42.4264 right and
  down), and label c at (100 + 60 x -1, 100 - 60 x 0) = (40, 100). A `bandPosition` of 0 puts the labels at the starts of
  the slices (0, PI / 2, PI), (100, 40), (160, 100), (100, 160), and a `bandPosition` of 1 at their ends
  (PI / 2, PI, 2 PI),
  (160, 100), (100, 160), (100, 40); a `bandPosition` of 0.25 is a quarter of the way through each slice in the data
  (0.25 of [0, 1], 1.25 of [1, 2], 2.5 of [2, 4]), the angles PI / 8, 5 PI / 8, and 5 PI / 4; `dx` and `dy` stay on the
  item and are not added to the anchor. An unstacked theta on the domain 0 to 4 with a radius value 50 puts the value 1
  (PI / 2) at (150, 100) and the value 4 (a full turn) at (100, 50); a stacked radius is the middle of its ring (the domain
  0 to 4 over 0 to 100: 0.5, 1.5, 3 give 12.5, 37.5, 75) with theta PI / 2, so the labels are at (112.5, 100), (137.5, 100),
  and (175, 100); the mark properties theta PI and radius 30 give (100, 130), a radius 0 leaves the anchor at the center,
  and a row without a number is not drawn. On the 300 by 200 text view a quarter turn with the radius 50 from the center
  (150, 100) is (200, 100), a half turn is (150, 150), no turn is (150, 50), a theta without a radius moves nothing, a
  theta field on 0 to 4 with the radius value 40 puts a = 1 at (190, 100) and a = 4 at (150, 60), and a radius field on
  0 to 10 over 0 to 100 at a quarter turn puts 5 and 10 at (200, 100) and (250, 100).

Unit tests reside in `MarkBuilderTests.cs`, `MarkBuilderChannelTests.cs`, `MarkBuilderConditionTests.cs`,
`MarkBuilderTimeTests.cs`, `MarkBuilderPathTests.cs`, `MarkBuilderOrderTests.cs`, `MarkBuilderOverlayTests.cs`,
`MarkBuilderTextTests.cs`, `MarkBuilderArcTests.cs`, and `MarkBuilderPolarTextTests.cs` within the
`DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Marks` folder; the shared helpers are in
`ChartTestHelpers.cs` and, for the text of a path, `PathText.cs`, which spells the segments of a `ScenePath` as
`M0,200 L150,0 Z` with the numbers rounded to six decimals so that a whole outline is compared as one string. The path,
overlay, order, text, arc, and polar text tests build the real chain (`SpecParser`, `ConfigResolver`, `DataLoader`, and
`ScaleResolver`) like the others, with the 300 by 200 or 200 by 200 views named in the derivations above; they use no
font, no measurement of text, and no pixel, so the expectations depend on no platform (the comparisons of angles and
trigonometric positions use a tolerance of 1e-9).

The marks over time units (`MarkBuilderTimeTests`) are derived by hand from the days of the first quarter of 2024: three
bars over January, February, and March (the unit `yearmonth`, one row each) have the domain January 1 to April 1, which
is 31 + 29 + 31 = 91 days over 300 pixels, so a day is 300 / 91 = 3.2967 pixels and the scale maps January 1, February 1,
March 1, and April 1 (days 0, 31, 60, and 91) to 0, 102.198, 197.802, and 300. A bar spans its unit with Vega-Lite's
`rectBinPosition` rules: the bin spacing of 1 pixel takes half a pixel off each edge, and the half pixel of the axis
translate shifts the whole bar, so the left edge is the start plus 0.5 plus 0.5 and the width is the days x 300 / 91
minus 1: January starts at 1 and is 31 x 300 / 91 - 1 = 101.198 wide, February starts at 102.198 + 1 = 103.198 and is
29 x 300 / 91 - 1 = 94.604 wide, and March starts at 197.802 + 1 = 198.802 and is 101.198 wide (its right edge is
198.802 + 101.198 = 300, the end of the data rectangle). A rect has no spacing, so only the half pixel remains: the
edges are 0.5, 102.698, and 198.302 with the widths 102.198, 95.604, and 102.198 (the days x 300 / 91). The test
constants are the expressions `31 * PerDay` and so on, with `PerDay` = 300 / 91, which agree with this arithmetic (the
figure 102.2527 in the plan is wrong; 31 / 91 x 300 = 102.198, and the tests are right).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test builds its own specification text; no files are read; the cancellation scenario uses an
  already canceled token

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any position, size, shape, paint, opacity, dropped or kept row, path outline, angle, radius, label text, or
scene structure that differs from the
hand-derived expectation (to nine decimals, or as the text of a path to six), a count of scene items or evaluation steps
that differs from the derived charge, or an exception of the wrong type or without the documented location,
constitutes a failure.

#### Unit-Level Test Scenarios

##### Bar And Rect Positions

**Tests**: `MarkBuilderTests.MarkBuilder_Build_BarAndRectPositionsMatchExpectedSceneItems`

Asserts the derived vertical, horizontal, negative, and non-zero-domain bars (starting at the stack start 0 and not
clipped for an explicit domain, and running to the first domain value for `stack: null`), the continuous
bars, the absent channels, the explicit size and corner radius (`size: 20`, `cornerRadius: 3`), the `rect` cells, and
the clip rule.

##### Point, Circle, Square, Rule, And Tick Geometry

**Tests**: `MarkBuilderTests.MarkBuilder_Build_PointCircleSquareRuleAndTickMatchExpectedSceneItems`

Asserts the derived symbol positions, sizes, outline or fill, the dropped row, the middle position for a missing
channel with an explicit opacity (1), size (80), and angle (45), the point scale positions, the rule orientations and
stems, and the vertical, horizontal, and two-measure ticks.

##### Color, Size, And Opacity

**Tests**: `MarkBuilderTests.MarkBuilder_Build_ColorSizeAndOpacityUseExpectedProperties`,
`MarkBuilderChannelTests.MarkBuilder_Build_InvalidValuesOfContinuousChannelsDropTheRow`

Asserts the derived tableau10 colors, sizes, and opacities for a filled and an unfilled point; that value definitions
are used as given and the `fill` channel wins over `color` (`#102030`, opacity 0.25, size 99); that the mark definition
wins over `config.point`, which wins over `config.mark` (stroke `teal`, stroke width 3, size 50, fill opacity 0.5); that
bars are opaque by default and a configured opacity (0.9) replaces the reduced default of points; and that a row whose
size value is not a number is dropped; and that, with a color field and a stroke width field of which one value each is
not a number (`"x"` and `"y"`), the two rows with an invalid value are dropped and the other two are drawn.

##### Ramp, Classified, And Categorical Colors

**Tests**: `MarkBuilderChannelTests.MarkBuilder_Build_SequentialColorInterpolatesTheRange`,
`MarkBuilderChannelTests.MarkBuilder_Build_DefaultRampReverseAndDivergingColors`,
`MarkBuilderChannelTests.MarkBuilder_Build_DiscretizedColorsGiveTheColorOfTheClass`,
`MarkBuilderChannelTests.MarkBuilder_Build_RampColorsPaintBarsRectsRulesAndTicks`

Asserts, for four rows with the field v = 20, 60, 100, 100, that a sequential scale over the extent 20 to 100 with the
range black to white gives `#000000`, `#808080` (127.5 rounds to 128), `#ffffff`, and `#ffffff`, as the fill of a filled
point, as the stroke (with the fill `transparent`) of an unfilled point, as the fill of a bar, and as the stroke of a
rule; that the default ramp of the configuration gives the ends of Vega's blues scheme (`#cfe1f2` for the lowest value
and `#0a4a90` for the highest) and `reverse` swaps them; that `domainMid` 60 with the range red, white, blue makes the
scale diverging and gives `#ff0000`, `#ffffff` (the middle of the three-color range), and `#0000ff`; that the rows whose
value is `"n/a"` or `null` are not drawn; that a quantize scale over 0 to 90 and a threshold scale at 30 and 60, both
with three colors, give red to 0 and 29, green to 31 and 59, and blue to 61 and 90, and that a value on a threshold
takes the color of the class above (30 is green, 60 is blue); and that a quantile scale over 1 to 8 with two colors cuts
at the median 4.5 (the first four rows take the first color, the last four the second).

##### Shape, Stroke Width, Stroke Dash, Opacities, And Angle

**Tests**: `MarkBuilderChannelTests.MarkBuilder_Build_ShapeChannelChoosesTheSymbolOfEachRow`,
`MarkBuilderChannelTests.MarkBuilder_Build_EveryShapeNameReachesTheSymbol`,
`MarkBuilderChannelTests.MarkBuilder_Build_StrokeWidthChannelScalesAndOverridesTheMark`,
`MarkBuilderChannelTests.MarkBuilder_Build_StrokeDashChannelChoosesThePatternOfEachRow`,
`MarkBuilderChannelTests.MarkBuilder_Build_FillAndStrokeOpacityChannelsScaleIndependently`,
`MarkBuilderChannelTests.MarkBuilder_Build_AngleChannelRotatesTheSymbols`,
`MarkBuilderChannelTests.MarkBuilder_Build_SizeOnDiscretizingScalesUsesTheClassOfTheValue`,
`MarkBuilderChannelTests.MarkBuilder_Build_NewChannelsAreDeterministic`

Asserts that the shape channel takes the symbols circle, square, and triangle-up of the configuration for the categories
a, b, and c (the repeated category repeating its shape), a custom range (`diamond`, `cross`, and a custom path that is
kept as written), a constant value, and the mark's own shape, that a circle mark ignores the channel, and that each of
the twelve symbol names reaches the symbol unchanged; that a stroke width scale over 10 to 40 (only a size scale starts
at zero) maps to 1, 2, 3, and 4, a constant value and the mark's property replace it as documented, and a quantize scale
on the channel has two classes; that the stroke dash scale gives the first three of the five patterns (`1,0`, `4,2`,
`2,1`) to the categories on a point, a rule, and a bar, a custom range its own patterns, a constant value and the mark's
pattern theirs, and that a rect keeps the solid line; that the fill opacity and the stroke opacity scales over 20 to 100
map to 0.3, 0.55, 0.8, and 0.8 independently of each other and of the opacity, with constants and the mark's properties
as the fallback, and that a rule carries the stroke opacity; that the angle scale over 0 to 100 maps 20, 60, and 100 to
72, 216, and 360 degrees, a constant and the mark's angle are used, a tick ignores the channel, and a row without a
number is dropped; that a quantize size scale with four classes over 0 to 100 gives 4, 123, 242, and 361 for 10, 30, 60,
and 90, a threshold scale with two thresholds gives 4, 182.5, and 361, and an ordinal scale gives the same size to equal
categories; and that building the same chart twice with every new channel at once gives the same items.

##### Limits, Cancellation, And Unsupported Marks

**Tests**: `MarkBuilderTests.MarkBuilder_Build_LimitsCancellationAndUnsupportedMarks`,
`MarkBuilderChannelTests.MarkBuilder_Build_LongDashPatternsExceedTheLimit`,
`MarkBuilderChannelTests.MarkBuilder_Build_NewChannelsHonorCancellationAndTheSceneBudget`

Asserts that more items than a `MaxSceneItems` of 3 throw `VegaLiteRenderException` at `/mark` naming the limit; that a
canceled token throws `OperationCanceledException`; that a `geoshape` mark (the sentinel of a mark that the renderer does
not draw) gives an empty group and `IsSupported` is true for `tick`, `line`, `area`, and `trail` and false for `geoshape`
and null; that a rule without x and y draws nothing; and that null arguments are
rejected; that a dash pattern of 64 entries is drawn while one of 65 entries, given by the mark, by a channel value, or
by a scale range, throws a `VegaLiteRenderException` at `/mark` that names "the limit of 64"; and that a point mark with
a shape and a color channel stops on a canceled token and charges a budget of two at the marks group and the first
symbol before it fails on the second symbol.

##### Budget Counts Only The Items Created

**Tests**: `MarkBuilderTests.MarkBuilder_Build_BudgetCountsOnlyTheItemsThatAreCreated`

Asserts that a point mark over five rows, three of which have no valid x, creates two items and charges a shared budget
of three exactly three times (the marks group and the two symbols), without failing, and that a budget of two fails at
the second symbol, at `/mark`, after the marks group was charged first. Every mark item is charged before it is created;
the exact boundary of a whole chart is asserted by the system tests of the scene item limit.

##### Row-Bound Expression References Are Evaluated Per Row

**Tests**: `MarkBuilderTests.MarkBuilder_Build_RowBoundExpressionReferencesAreEvaluatedPerRow`

Asserts, for a point mark over the rows `q = 2` and `q = 5` whose definition has `fill` of
`datum.q > 3 ? 'red' : 7`, `opacity` of `datum.q > 3 ? 0.25 : 'x'`, and `strokeWidth` of `datum.q`, and whose `size`
channel value is `datum.q * 10`, built by a builder that was given the compiled expressions of the tree and an
evaluator, that the first item keeps the default fill (`#4c78a8`, because the expression gave a number and not a color
text) and the default opacity 0.7 (because the expression gave text and not a number) while the second has the fill
`red` and the opacity 0.25, that the stroke widths are 2 and 5, and that the sizes are 20 and 50; and that the builder
without the expressions leaves every reference unevaluated (the opacity stays 0.7 and no fill is red), so a result of
the wrong kind never changes the mark and a missing evaluator never changes the previous behavior.

##### Stacked Layers Span From The Stack Start To Its End

**Tests**: `MarkBuilderTests.MarkBuilder_Build_StackedLayersSpanFromTheStackStartToItsEnd`

Asserts the derived stacked bars above for a vertical chart with the zero, center, and normalize offsets, that
`stack: null` brings back the overlapping bars from zero, that a horizontal chart stacks ascending, that a circle sits
at the upper end of its layer, and that the result is the same on a second build.

##### Signed Stacks Hang Below Zero In Both Orientations

**Tests**: `MarkBuilderTests.MarkBuilder_Build_SignedStacksHangBelowZeroInBothOrientations`

Asserts, for one category with the layers p = 3, q = 2, r = -4, and s = -1 over the domain -5 to 5 (y(v) = 50 - 10 v),
that the vertical chart draws q (0, 2) and p (2, 5) above the baseline and s (0, -1) and r (-1, -5) below it (the
rectangles at y = 0, 30, 60, and 50 and as high as 30, 20, 40, and 10, in data order); that the horizontal chart draws
p (0, 3), q (3, 5), r (-4, 0), and s (-5, -4) (x = 50, 80, 10, and 0 and as wide as 30, 20, 40, and 10); that an
all-negative stack (p = -3, q = -2) hangs entirely below zero over the domain -5 to 0; that center and normalize use the
absolute values (a with 3 and -1, b with -2: the centered rectangles at y = 0, 75, and 25, the normalized ones at
y = 0, 75, and 0); and that the result is the same on a second build.

##### Binned Bars And Rects Span Their Bins

**Tests**: `MarkBuilderTests.MarkBuilder_Build_BinnedBarsSpanTheirBinsLessTheSpacing`,
`MarkBuilderTests.MarkBuilder_Build_PreBinnedRangesSpanFromTheStartToTheEndField`

Asserts the derived binned geometry above (the bin spacing of the mark, the half pixel of the axis translate, an
explicit `binSpacing`, a reversed scale, a `size` centered on the bin, a `rect` without spacing, the `minBandSize`
widening, a pre-binned `step`, and a circle at the middle of its bin) and the pre-binned ranges drawn as bars with a
pixel of spacing, as rects without, and as circles in the middle of the range.

##### Secondary Channels Span A Range

**Tests**: `MarkBuilderTests.MarkBuilder_Build_SecondaryChannelsSpanARange`

Asserts the derived range bars (between two fields, between a field and a pixel value, and between a field and a datum),
that a row with an invalid end is not drawn, and the rules along the range of one channel and between two points.

##### Stacked And Binned Marks Respect The Budget And Cancellation

**Tests**: `MarkBuilderTests.MarkBuilder_Build_StackedAndBinnedMarksRespectTheBudgetAndCancellation`

Asserts that three stacked bars and the marks group need exactly four scene items (a `MaxSceneItems` of 4 passes and 3
fails naming the limit), that a canceled token throws `OperationCanceledException`, and that a chart with one row per
category is drawn as before, the bars running from zero (y 114, 144, 90, and 18 with the heights 86, 56, 110, and 182).

##### Log Scale Bars Start At The Baseline And Skip Values A Scale Cannot Map

**Tests**: `MarkBuilderTests.MarkBuilder_Build_LogScaleBarsStartAtTheBaselineAndSkipValuesTheLogCannotMap`,
`VegaLiteScaleIntegrationTests.CanvasNetVegaLite_SystemIntegration_LogScale_BarsRiseByTheLogarithmOfTheirValue`,
`VegaLiteScaleIntegrationTests.CanvasNetVegaLite_SystemIntegration_SqrtAndSymlogScales_PointsSitAtTheTransformedPositions`

Assert that bars of 43, 28, 55, and 91 on a log axis rounded out to 10 to 100 (one decade over 200 pixels) are
not stacked, reach
`200 - 200 * (log10(v) - 1)`, and all end at 200; that a fifth row of zero on an explicit domain 10 to 100 has no bar
and the
other four stay where they were; that the fixture of four daily counts (12, 95, 1200, and 8000) on a 300 pixel log
axis rounded out
to 10 to 10000 has its bars at `300 - 100 * (log10(v) - 1)` ending at the bottom, ten labels (`10`, `20`, `30`, `100`, `200`,
`300`, `1,000`, `2,000`, `3,000`, `10,000`) of the 28 ticks, 28 grid rules, the bar color at the bar centers, and the background
between the grid lines above the bars; and that points on a square root x scale (0 to 100 over 200 pixels) and a
symlog y scale
(nice domain -10 to 10 over 100 pixels) sit at the positions the d3 formulas give.

##### Binned Channels

**Tests**: `MarkBuilderChannelTests.MarkBuilder_Build_BinnedColorGivesEachMarkTheColorOfItsBin`,
`MarkBuilderChannelTests.MarkBuilder_Build_BinnedSizeUsesTheStartOfTheBin`,
`MarkBuilderChannelTests.MarkBuilder_Build_HistogramBarsTakeTheColorOfTheirBin`,
`MarkBuilderChannelTests.MarkBuilder_Build_BinnedColorMarksAreChargedAndCancellable`

Assert, for the readings 3, 12, 27, 41, 58, 66, 83, and 97 with the bins 0 to 100 by 20 and five colors, that the
circles take the color of their bin (the first two the first color, 41 and 58 the third, the last two the fifth) as a
fill and an outlined point as a stroke; that binned point sizes are `4 + 357 * start / 100` for the starts 0, 0, 20, 40,
40, 60, 80, and 80; that the five bars of a histogram whose color bins the field as its x channel does are filled with
the five colors in bin order, are 19 pixels wide starting at 1, 21, ..., 81 (the bin of 20 pixels less the bin spacing
of 1) of 100 pixels, and are 100, 50, 100, 50, and 100 pixels high for the counts 2, 1, 2, 1, and 2; and that a scene
budget of four fails naming `MaxSceneItems` and a canceled token stops the build.

##### Conditional Encodings

**Tests**: `MarkBuilderConditionTests.MarkBuilder_Build_ValueConditionOverridesTheBaseValueWhereTheTestHolds`,
`MarkBuilderConditionTests.MarkBuilder_Build_ConditionWithoutBaseValueLeavesTheMarkDefault`,
`MarkBuilderConditionTests.MarkBuilder_Build_FirstConditionThatHoldsWins`,
`MarkBuilderConditionTests.MarkBuilder_Build_FieldConditionUsesTheScaleOfItsFieldWhereTheTestHolds`,
`MarkBuilderConditionTests.MarkBuilder_Build_FieldWithValueConditionMapsTheFieldElsewhere`,
`MarkBuilderConditionTests.MarkBuilder_Build_NumericChannelConditionsGiveValuesAndKeepDefaults`,
`MarkBuilderConditionTests.MarkBuilder_Build_FillAndStrokeOpacityConditionsApplyPerRow`,
`MarkBuilderConditionTests.MarkBuilder_Build_ShapeAndDashConditionsApplyPerRow`,
`MarkBuilderConditionTests.MarkBuilder_Build_PredicateObjectTestsAreEvaluated`,
`MarkBuilderConditionTests.MarkBuilder_Build_TestReadsVariableParametersAndMissingFieldsDoNotHold`,
`MarkBuilderConditionTests.MarkBuilder_Build_ParameterConditionIsNotApplied`,
`MarkBuilderConditionTests.MarkBuilder_Build_ConditionsApplyToBarsTicksAndRules`,
`MarkBuilderConditionTests.MarkBuilder_Build_SizeConditionGivesTheThicknessOfABar`,
`MarkBuilderConditionTests.MarkBuilder_Build_TestEvaluationIsChargedToTheStepBudget`,
`MarkBuilderConditionTests.MarkBuilder_Build_CanceledTokenStopsTheEvaluation`,
`MarkBuilderConditionTests.MarkBuilder_Build_ConditionalMarksAreDeterministic`

Assert, for circles over the rows with `q` 1, 3, and 5 and the categories a, b, and a, that a color condition on
`q > 3` with the base `gray` gives `gray`, `gray`, `red`, and without a base the default of the palette (`#4c78a8`) for
the first two; that of the two conditions `q > 1` and `q > 3` the first that holds wins (`gray`, `red`, `red`); that a
condition that holds the field `c` colors the rows where `q > 1` holds with the palette colors of `b` and `a`
(`#f58518`, `#4c78a8`) and leaves the first row `gray`, and that a field with a value condition maps the field
elsewhere; that the opacity (0.7 by default, then 0.2 or 1), the size (30 by default, then 50 or 200), the stroke
width, the angle, the fill and stroke opacity, the shape, and the dash pattern take the value of the condition where
it holds and the base value or the default elsewhere; that each of the thirteen predicate forms (comparisons, a range
given in either order, a set, validity of a field that exists and one that does not, a negation, and the compositions)
holds for the rows that a hand evaluation says; that a test reads a variable parameter and a missing field does not hold;
that a condition on a parameter changes nothing; that the color and opacity conditions apply to bars, ticks, and rules and
the size condition gives the thickness of a bar (4 or 9 pixels); that a limit of 4 steps fails naming
`MaxEvaluationSteps` while a limit of 10,000 draws the three circles; that a canceled token stops the build; and that
two builds give equal items.

##### Bars And Rects Span Their Time Units

**Tests**: `MarkBuilderTimeTests.MarkBuilder_Time_YearMonthBars_SpanTheirMonths`,
`MarkBuilderTimeTests.MarkBuilder_Time_RectSpansItsUnitWithoutSpacing`,
`MarkBuilderTimeTests.MarkBuilder_Time_TimeUnitBandSize_Half`,
`MarkBuilderTimeTests.MarkBuilder_Time_WidthBand_IsTheRelativeSize`,
`MarkBuilderTimeTests.MarkBuilder_Time_MinBandSize_WidensAThinUnit`,
`MarkBuilderTimeTests.MarkBuilder_Time_RectBandPositionNotHalf_IsReported`

Assert the derived bar geometry above (three bars at 1, 103.198, and 198.802 with the widths 101.198, 94.604, and
101.198) and the rect geometry (0.5, 102.698, and 198.302 with the widths 102.198, 95.604, and 102.198). A
`timeUnitBandSize` of 0.5 (on the mark, in `config.bar`, or in `config.mark` of the specification) covers the middle
half of the unit, between the quarter and three quarters of the way, with the spacing taken off each edge, so January
starts at 0.25 x 102.198 + 1 and is 0.5 x 102.198 - 1 wide, and a `width` of `{"band": 0.5}` is the same. A unit
thinner than `minBandSize` is widened around its middle: the months of January 2000 and 2100 span 100 years and a month,
which is 36,525 days (100 x 365 plus 25 leap days, 2000 to 2096) plus the 31 days of January 2100 = 36,556 days, so
January is 31 / 36,556 x 300 = 0.254 pixels wide, below the `minBandSize` of 0.3, and the bar is exactly 0.3 pixels
wide, centered on the middle of its month (15.5 days in) plus the half pixel of the translate. A
`timeUnitBandPosition` other than 0.5 on a bar is reported with the feature id `vl-encoding-bandposition-rect` and
changes no geometry.

##### Points, Ticks, And Band Positions Of Time Units

**Tests**: `MarkBuilderTimeTests.MarkBuilder_Time_PointsAtTheStartOfTheUnit`,
`MarkBuilderTimeTests.MarkBuilder_Time_TicksAtTheMiddle`,
`MarkBuilderTimeTests.MarkBuilder_Time_BandPositionOnAPoint`,
`MarkBuilderTimeTests.MarkBuilder_Time_TimeScalePoints_AtTheirDates_AndBarsUseTheContinuousBandSize`

Assert that points sit at the start of their unit with the domain of the starts only (January 1, February 1, and March
1 are 0, 31, and 60 days apart, so the points are at 0, 31 / 60 x 300 = 155, and 300); that ticks sit in the middle of
their unit over the domain that includes the ends (the middle of January is 15.5 days, of February 31 + 14.5 = 45.5
days, and of March 60 + 15.5 = 75.5 days, times 300 / 91, each tick 1 pixel wide); that a `bandPosition` of 0.5, 0.25,
and 0 on a point puts it 15.5, 45.5, and 75.5 days along, 7.75, 38.25, and 67.75 days along, or at the start (0, 31,
and 60 days); and that on a time scale without a unit the three points of January 1, 6, and 11 are at 0, 150, and 300
of 300 pixels (30 pixels a day) while a bar is 5 pixels wide (the continuous band size) on a scale padded by 5 pixels,
so the bars are centered at 5, 150, and 295.

##### Ordinal Time Units And Zones

**Tests**: `MarkBuilderTimeTests.MarkBuilder_Time_OrdinalTimeUnit_BandsOfABandScale`,
`MarkBuilderTimeTests.MarkBuilder_Time_Zone_BarsSpanLocalMonths`

Assert that an ordinal field with a month unit has the bands of a band scale (step 300 / (3 - 0.1 + 0.1) = 100, bandwidth
90, first band at (300 - 100 x 2.9) / 2 = 5, so 5, 105, and 205 with the width 90); and that in a +05:30 zone the
instants 2024-02-29T20:00Z and 2024-03-31T20:00Z (01:30 on March 1 and April 1 locally) are in March and April, which
span the 31 + 30 = 61 days of the domain (the widths 31 x 300 / 61 - 1 and 30 x 300 / 61 - 1), while in UTC they are
February and March, 29 + 31 = 60 days (the widths 29 x 300 / 60 - 1 and 31 x 300 / 60 - 1).

##### Lines Are Grouped, Sorted, And Painted

**Tests**: `MarkBuilderPathTests.MarkBuilder_Build_LineHasHandDerivedSegmentsAndPaint`,
`MarkBuilderPathTests.MarkBuilder_Build_LineGroupsByColorAndDetailInFirstSeenOrder`,
`MarkBuilderPathTests.MarkBuilder_Build_LineSortsByPixelXAndByTheOrderChannel`,
`MarkBuilderPathTests.MarkBuilder_Build_LineSizeIsTheStrokeWidthOfTheFirstRow`

Assert the derived line chart above: the path `M0,200 L150,0 L300,100` with the stroke `#4c78a8`, the width 2, no fill,
no dash, opacities 1, no overlay points, and no clip; one path per color group (blue then orange) and per detail group
(one color), and one path without a grouping field; the sort by the pixel x (also for shuffled rows), by an ascending and
a descending `order` field, and by the pixel y of a horizontal line; and the stroke widths 4 and 8 of a size field (two
paths), 5, 3, 7, and 6 of the `size` property, the `strokeWidth` property, a size value, and a size expression.

##### Areas Run Between The Value And The Baseline

**Tests**: `MarkBuilderPathTests.MarkBuilder_Build_AreaPolygonRunsBetweenTheValueAndTheBaseline`,
`MarkBuilderPathTests.MarkBuilder_Build_StackedAreaLayersMeetAndImputeMissingKeys`,
`MarkBuilderPathTests.MarkBuilder_Build_HorizontalAreaAndRangeAreaFollowOrient`

Assert the polygon of the line chart with the baseline y(0) = 200 and the fill `#4c78a8`, the baseline at the first value
of a domain that does not hold zero for an unstacked area (y(4) = 200), the vertices at the centers of a discrete
dimension (50, 150, 250); the two stacked layers with the imputed key (b on the bottom with its vertex at 200 in the
middle, a on top, both of seven segments, and the five-segment b without the impute); and the horizontal area sorted by
the pixel y, the range band between `y` and `y2`, and the empty group of an area without its measure channel.

##### Trails Carry The Size Of Their Rows

**Tests**: `MarkBuilderPathTests.MarkBuilder_Build_TrailPointsCarryTheSizeChannel`,
`MarkBuilderPathTests.MarkBuilder_Build_TrailBreaksAtInvalidRows`

Assert the one run of the points (0, 200, 4), (150, 0, 8), (300, 100, 12) with the fill `#4c78a8` and no stroke, the
default point size 2 and the sizes 9 and 5 of the `size` property and its expression, the two runs of two points
around an
invalid row, the single run of two points that remains when a run of one point is dropped, and the empty group of a trail
of single points.

##### Interpolations Select The Curve

**Tests**: `MarkBuilderPathTests.MarkBuilder_Build_InterpolateAndTensionSelectTheCurve`,
`MarkBuilderPathTests.MarkBuilder_Build_AreaBundleFallsBackToLinear`

Assert, for the pixels (0, 0), (50, 50), (100, 25), the linear, step, step-after, step-before, and tension 1 cardinal paths
derived above, that the default tension gives another cardinal path with the same ends, that the monotone line equals the
`CurveBuilder` path of the monotone-x curve, that a trail ignores the interpolation, that the bundle area is the linear
polygon of the line chart (the bundle line having a cubic segment).

##### Invalid Rows Break Or Are Filtered

**Tests**: `MarkBuilderPathTests.MarkBuilder_Build_DefaultInvalidModeBreaksThePath`,
`MarkBuilderPathTests.MarkBuilder_Build_InvalidFilterConnectsTheNeighbors`,
`MarkBuilderPathTests.MarkBuilder_Build_InvalidShowIsTheDefaultModeAndReported`

Assert the broken line and the two polygons of the unstacked area in the default mode, the three other `break-paths`
modes, and a text value; the connected line and polygon of the `filter` mode (from the mark, from `config.line`, and the
mark winning over the configuration) and that a point mark draws only its four valid rows whatever the mode; and that
`show` and null give the broken line and one report `vl-mark-invalid-show` at `/mark/invalid` of the kind unsupported
feature, and an unknown mode of the configuration is ignored.

##### Stroke Properties Reach Every Item

**Tests**: `MarkBuilderPathTests.MarkBuilder_Build_StrokeDashCapJoinMiterAndOffsetReachEveryItemType`

Asserts, for each of the ten mark types `line`, `area`, `trail`, `bar`, `rect`, `point`, `circle`, `square`, `rule`, and
`tick`, that the first item has the dash 4, 2, the offset 3, the cap `round`, the join `bevel`, and the miter limit 2, and
that a line without the properties has the offset 0 and no cap, join, or miter limit.

##### Overlays Draw Points And Lines After The Base Items

**Tests**: `MarkBuilderOverlayTests.MarkBuilder_Build_PointOverlayDrawsSymbolsWithTheOverlayDefinition`,
`MarkBuilderOverlayTests.MarkBuilder_Build_AreaLineOverlayAndDefaultOpacity`,
`MarkBuilderOverlayTests.MarkBuilder_Build_OverlayFromConfigAndTransparentPoints`

Assert the overlays derived above: the roles `mark-line`, `mark-point` x 3 with the budget 14, the filled blue circles of
the size 30 at the three pixels (the green line not inherited), the replaced properties, no overlay for `false` or null,
two points for a row gap, the rule and trail overlays; the area with the opacity 0.7 followed by the line along its top
edge (width 2, opacity 1), the replaced line properties, the step line, the opacities 0.5, 1 (with a fill opacity of
0.4), 0.7 (with a point overlay), and 1 (with both switched off), the clip of the group, and the lines at the stack end
of two stacked layers; and the configured overlays, their switch-off by the mark, the transparent points (opacity 0), and
the colors of the overlay points of a color field.

##### The Order Channel Sorts The Drawing

**Tests**: `MarkBuilderOrderTests.MarkBuilder_Build_OrderChannelSortsTheDrawOrderOfAnyMark`,
`MarkBuilderOrderTests.MarkBuilder_Build_OrderNullKeepsTheDataOrder`

Assert the symbol orders x = 150, 300, 0 (ascending), 0, 300, 150 (descending), and 0, 150, 300 (without the channel), the
tie break by the second field (150, 0, 300), the bars at 205, 5, 105 with the stack switched off and at 5, 105, 205
with the
default stack, the stacked bars that keep the data order (blue, orange); and, with the order switched off, the symbols at
300, 0, 150, the lines `M300,100 L0,200 L150,0` (by the channel value null or `false` and by the mark property `false` or
null), the sorted line `M0,200 L150,0 L300,100` without it, and the area polygon in the data order.

##### Path Segments Are Charged Before They Exist

**Tests**: `MarkBuilderPathTests.MarkBuilder_Build_PathSegmentsAreChargedBeforeCreation`,
`MarkBuilderPathTests.MarkBuilder_Build_PathBuildPollsCancellationAndChargesSteps`,
`MarkBuilderPathTests.MarkBuilder_Build_GroupsWithoutTwoPointsCostNothing`

Assert that a budget of 11 (the group, the path, and the bound of 9 segments) passes with the count 11 and the real
segment count within the bound, that a budget of 10 fails at `/mark` naming `MaxSceneItems` with the count 11, that the
area costs exactly 14, that a line of 2,000 points fails a limit of 500 items, that a canceled token stops the build, that
640 rows exceed 5 evaluation steps at `/mark` naming `MaxEvaluationSteps` (and are drawn by default), and that the groups
of one point and of no defined point draw nothing and cost nothing (11 for the line and 5 for the trail of the same data).

##### Text Has Its Defaults, Properties, And Size

**Tests**: `MarkBuilderTextTests.MarkBuilder_Build_TextHasPositionDefaultsAndPaint`,
`MarkBuilderTextTests.MarkBuilder_Build_TextOffsetsAnglesAndFontPropertiesReachTheItem`,
`MarkBuilderTextTests.MarkBuilder_Build_TextSizeChannelMapsToTheFontSize`

Assert the labels at (0, 200) and (300, 0) with the Vega defaults, the middle (150, 100) without a position, the colors
from a color field (blue, orange) and from a value (`red`, opacity 0.5); that `dx`, `dy`, `angle`, `font`, `fontSize`,
`fontStyle`, `fontWeight`, `limit`, `ellipsis`, `lineHeight`, `align`, and `baseline` of the mark reach the item (3, -4,
30, `Georgia`, 20, `italic`, `bold`, 50, `...`, 14, `left`, `top`) without moving the position, that a numeric weight is
its text (`700`), that an unknown alignment is `left` and an unknown baseline `alphabetic`, the precedence of the mark over
`config.text` and an angle channel (45) over the mark angle; and the font sizes 24 and 40 of the size channel and the
documented order of the size value, `fontSize`, `size`, the expression, and the default 11.

##### Labels Are Formatted And Split Into Lines

**Tests**: `MarkBuilderTextTests.MarkBuilder_Build_TextFormatsNumbersTimesAndNulls`,
`MarkBuilderTextTests.MarkBuilder_Build_TextArraysAndLineBreakMakeLines`,
`MarkBuilderTextTests.MarkBuilder_Build_TextMarkPropertyAndValueChannelAreUsed`,
`MarkBuilderTextTests.MarkBuilder_Build_TextCustomFormatTypeFallsBackToTheDefault`

Assert the numbers, times, bins, and nulls derived above (`1,234.5`, `1234.5`, `26%`, `3.0`, `3.00`, `0`, `null`,
`2020/03/05`, `Mar 05, 2020`, `05.03.2020`, `Mar 2020`, `2020-03-05`, `0 – 10`, `0.0 – 10.0`, and the count `3`); the lines
of an array value, of a mixed array (`1`, `true`, an empty line, `x`), of a field made by a calculate transform, of the
`lineBreak` `|` (with the empty parts at the ends) and of a newline `lineBreak`, the text `a\nb` kept as one line without
it, the unsplit array and the single line when the break does not occur, the `text` property as a list, and the empty
labels that draw nothing (a text of length zero and an empty list, but not a list of one empty text); the `text` property
(`hello`) as the label of every row and the channel winning over it, a text, number, boolean, and null value, a datum with
its format (`5.00`, `5`, `abc`), and no label without a text source or for a missing field; and that a custom format type
gives the default number and date formats and the catalog reports `vl-format-type-custom` at `/encoding/text/formatType`.

##### Text Is Charged By Its Characters And Lines

**Tests**: `MarkBuilderTextTests.MarkBuilder_Build_TextCharactersAreChargedBeforeFormatting`,
`MarkBuilderTextTests.MarkBuilder_Build_TextLinesAreChargedAsItems`

Assert the limits derived above: 16 labels of 16 characters fit a `MaxStringLength` of 16 (the first is
`0000000000000001`) and the 17th is refused as a total at `/mark`, a label of 17 characters and a time label of 22
characters over a limit of 10 are refused on their own, a label of 6,400 characters exceeds 50 evaluation steps and is drawn
by default, a canceled token stops the build, and two labels of three lines cost 7 items exactly (6 fails with the
count 7,
and 3 fails with the count 4).

##### Arcs Have Hand-Derived Angles And Radii

**Tests**: `MarkBuilderArcTests.MarkBuilder_Build_PieSlicesHaveHandDerivedAnglesAndRadii`,
`MarkBuilderArcTests.MarkBuilder_Build_DonutRadialAndConstantRadiiUseTheMarkProperties`,
`MarkBuilderArcTests.MarkBuilder_Build_ArcPadAngleCornerRadiusAndPaintReachTheItem`,
`MarkBuilderArcTests.MarkBuilder_Build_ArcCenterFollowsXAndY`

Assert the derived pie (three slices at (100, 100), the angles 0, PI / 2, PI, 2 PI, the radii 0 and 100, the blue, orange,
and red fills, the slices meeting, no stroke, no padding), the 300 by 120 view, the whole circle of one value, the
unstacked values and the disc; the donut, the aliases and the explicit radii (30 to 80, 10 to 90, 20 to 70), the constant
angles (1 to 2.5), the configured radii (20 to 60), the radial chart with the radii 20 + 80 / 3, 20 + 160 / 3, and 100,
the rings, the band slices, the ranged arc, the unstacked and secondary values, and the constants (0.5 to 2, 10 to
70); the
pad angle, corner radius, stroke, opacities, dash, and fill that reach every slice and the configuration with the mark
winning; and the centers (60, 80), (300, 200), (75, 100), (225, 100), (0, 200), and (300, 0).

##### Arcs Are Charged Per Slice

**Tests**: `MarkBuilderArcTests.MarkBuilder_Build_ArcsAreChargedPerSliceAndPollCancellation`

Asserts that the pie costs 4 items (a budget of 4 passes, a budget of 3 fails at `/mark` with the count 4), that the
row that is not drawn in an unstacked chart costs nothing (a count of 3 for two slices and the group), that 200 rows
exceed 2
evaluation steps (200 / 64 = 3) at `/mark` naming `MaxEvaluationSteps` and are drawn by default, and that a canceled token
stops the build.

##### Labels Of A Pie Sit At The Middles Of Their Slices

**Tests**: `MarkBuilderPolarTextTests.MarkBuilder_Build_PieLabelsSitAtTheMiddleOfTheirSlice`,
`MarkBuilderPolarTextTests.MarkBuilder_Build_PolarTextOffsetsFollowThetaAndRadius`,
`MarkBuilderTextTests.MarkBuilder_Build_TextConstantAndFieldRadiusAndThetaOffsetTheAnchor`

Assert the anchors derived above: a at (142.4264, 57.5736), b at (142.4264, 142.4264), and c at (40, 100); the starts
(100, 40), (160, 100), (100, 160), the ends (160, 100), (100, 160), (100, 40), and the quarter-way positions of the three
slices; the unchanged anchor with `dx` 5 and `dy` -3 on the item; the unstacked values (150, 100) and (100, 50), the ring
middles (112.5, 100), (137.5, 100), (175, 100), the constants (100, 130), the zero radius at the center, and the
dropped row;
and, on the 300 by 200 view, the quarter, half, and zero turns (200, 100), (150, 150), (150, 50), the angle without a radius
(150, 100), the theta field (190, 100) and (150, 60), the radius field (200, 100) and (250, 100), and the dropped row
of an
angle that is null.
