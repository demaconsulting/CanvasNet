### MarkBuilder Unit Verification Design

This document describes the unit-level verification strategy for the `MarkBuilder` unit in the
`DemaConsulting.CanvasNet.VegaLite.Marks` namespace: the internal `MarkBuilder` class (`Build` and the static
`IsSupported`). The unit reads the rows of a `DataTable` and the resolved scales of a view and creates, in a `marks`
group, the scene items of a `bar`, `rect`, `point`, `circle`, `square`, `rule`, or `tick` mark (roles `mark-<type>`)
with the geometry and the paint that Vega-Lite assigns: positions from the scale models, band sizes, default sizes, the
filled or outlined appearance, the colors, the sizes, the opacities, the invalid-data filter, and the scene item budget.

#### Verification Approach

The unit is verified through the shared helper `ChartTestHelpers.Marks`, which runs the real `SpecParser`,
`ConfigResolver`, `DataLoader`, and `ScaleResolver` for an original specification and calls the builder for a data
rectangle of a chosen size. The assertions are on the scene items (positions, sizes, fills, strokes, stroke widths,
opacities, shapes, angles, corner radii). No mocking or stubbing is used; the collaborators are real because the unit's
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
canceled token throws `OperationCanceledException`; that a `line` mark gives an empty group and `IsSupported` is true
for `tick` and false for `line` and null; that a rule without x and y draws nothing; and that null arguments are
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
