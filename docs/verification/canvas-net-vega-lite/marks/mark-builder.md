### MarkBuilder Unit Verification Design

This document describes the unit-level verification strategy for the `MarkBuilder` unit in the
`DemaConsulting.CanvasNet.VegaLite.Marks` namespace: the internal `MarkBuilder` class (`Build` and the static
`IsSupported`). The unit reads the rows of a `DataTable` and the resolved scales of a view and creates, in a `marks`
group, the scene items of a `bar`, `rect`, `point`, `circle`, `square`, `rule`, or `tick` mark (roles `mark-<type>`) with
the geometry and the paint that Vega-Lite assigns: positions from the scale models, band sizes, default sizes, the
filled or outlined appearance, the colors, the sizes, the opacities, the invalid-data filter, and the scene item budget.

#### Verification Approach

The unit is verified through the shared helper `ChartTestHelpers.Marks`, which runs the real `SpecParser`,
`ConfigResolver`, `DataLoader`, and `ScaleResolver` for an original specification and calls the builder for a data
rectangle of a chosen size. The assertions are on the scene items (positions, sizes, fills, strokes, stroke widths,
opacities, shapes, angles, corner radii). No mocking or stubbing is used; the collaborators are real because the unit's
contract is how the table, the scales, and the configuration combine into items. The internal types are reached through
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
  y(0) = 150 to y(-10) = 200 (y 150, height 50) and the bar of 30 spans 0 to 150; a measure axis whose domain does not
  contain zero runs the bar to the first domain value (y(20) = 100 for a domain of 20 to 100 over 100 pixels, so y 50,
  height 50).
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

Unit tests reside in `MarkBuilderTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Marks`
folder; the shared helpers are in `ChartTestHelpers.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test builds its own specification text; no files are read; the cancellation scenario uses an
  already canceled token

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any position, size, shape, paint, opacity, dropped or kept row, or scene structure that differs from the
hand-derived expectation (to nine decimals), or an exception of the wrong type or without the documented location,
constitutes a failure.

#### Unit-Level Test Scenarios

##### Bar And Rect Positions

**Tests**: `MarkBuilderTests.MarkBuilder_Build_BarAndRectPositionsMatchExpectedSceneItems`

Asserts the derived vertical, horizontal, negative, and non-zero-domain bars, the continuous bars, the absent channels,
the explicit size and corner radius (`size: 20`, `cornerRadius: 3`), the `rect` cells, and the clip rule.

##### Point, Circle, Square, Rule, And Tick Geometry

**Tests**: `MarkBuilderTests.MarkBuilder_Build_PointCircleSquareRuleAndTickMatchExpectedSceneItems`

Asserts the derived symbol positions, sizes, outline or fill, the dropped row, the middle position for a missing
channel with an explicit opacity (1), size (80), and angle (45), the point scale positions, the rule orientations and
stems, and the vertical, horizontal, and two-measure ticks.

##### Color, Size, And Opacity

**Tests**: `MarkBuilderTests.MarkBuilder_Build_ColorSizeAndOpacityUseExpectedProperties`

Asserts the derived tableau10 colors, sizes, and opacities for a filled and an unfilled point; that value definitions are
used as given and the `fill` channel wins over `color` (`#102030`, opacity 0.25, size 99); that the mark definition wins
over `config.point`, which wins over `config.mark` (stroke `teal`, stroke width 3, size 50, fill opacity 0.5); that
bars are opaque by default and a configured opacity (0.9) replaces the reduced default of points; and that a row whose
size value is not a number is dropped.

##### Limits, Cancellation, And Unsupported Marks

**Tests**: `MarkBuilderTests.MarkBuilder_Build_LimitsCancellationAndUnsupportedMarks`

Asserts that more items than a `MaxSceneItems` of 3 throw `VegaLiteRenderException` at `/mark` naming the limit; that a
canceled token throws `OperationCanceledException`; that a `line` mark gives an empty group and `IsSupported` is true for
`tick` and false for `line` and null; that a rule without x and y draws nothing; and that null arguments are rejected.

##### Budget Counts Only The Items Created

**Tests**: `MarkBuilderTests.MarkBuilder_Build_BudgetCountsOnlyTheItemsThatAreCreated`

Asserts that a point mark over five rows, three of which have no valid x, creates two items and charges a shared budget
of three exactly three times (the marks group and the two symbols), without failing, and that a budget of two fails at the
second symbol, at `/mark`, after the marks group was charged first. Every mark item is charged before it is created; the
exact boundary of a whole chart is asserted by the system tests of the scene item limit.
