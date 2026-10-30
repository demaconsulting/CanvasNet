### LegendBuilder Unit Verification Design

This document describes the unit-level verification strategy for the `LegendBuilder` unit in the
`DemaConsulting.CanvasNet.VegaLite.Guides` namespace: the internal static `LegendPlan` class (which channels have
legends, their kind, orient, direction, title, and merging), the sealed `LegendBuilder` class (`Build` and the static
`GridLayout`), and their records (`LegendMember`, `LegendMarkFacts`, `LegendSettings`, `LegendSpec`, `LegendResult`,
`LegendFrame`, `GridOptions`, and `GridResult`).

#### Verification Approach

The unit is verified with values derived by hand from Vega's legend rules and the measured width of the bundled font,
never by recording the output of the code.

**Symbol legends.** For a nominal color on a point mark the entry size is `max(ceil(sqrt(100) + 1.5), 10)` = 12, the
symbol is centered at (6, 6), and the label starts at 12 + 4 = 16. The symbol box reaches 5 + 2 x 1.5 = 8 around the
center (a stroked symbol is bounded with Vega's miter allowance of two stroke widths), so the box of an entry is 14
tall and starts 2 above its origin; the grid then puts the rows 2 (row padding) + 2 (the room above the origin) + 14
(the extent of the row above) = 18 pixels apart: 6, 24, 42. The bold title of 11 pixels is 11 tall, so the entries
start at 11 plus 5 (title padding) = 16, and the box is as tall as the last row (36 + 14) plus 16 = 66. With two
columns the second starts at the column padding 10 + the room 2 + the widest box of the first column (the labels end
at 16 + 5.56, rounded up to 22) = 34.

**Gradient legends.** A continuous color over 1 to 3 on a 300 pixel view has a bar of 16 by 200 pixels (the height
limited to 64 to 200) and asks for max(2, 2 x floor(200 / 100)) = 4 ticks (d3 steps of 0.5: five labels from the
bottom at 200 to the top at 0). Its gradient has the ticks for a count of 15 (steps of 0.1: 21 stops at offsets
i / 20) with the first and last colors of the blues ramp. On a view 150 wide a top legend has a 150 pixel bar, two
ticks (steps of 1) and labels at 0, 75, and 150 pixels; a domain 1 to 2.9 has only the ticks 1 and 2, so the two ends
are labeled; a domain without span becomes 0 to 1 (21 stops). A quantize scale over 0 to 90 with three classes has the
thresholds 30 and 60, bands a third of the bar tall from the top down, labels 30 and 60 at one third and two thirds,
and the symbol labels `< 30`, `30 – 60`, and `≥ 60`; a threshold scale at 10 and 30 is widened by 20 at both ends so
the thresholds are at a third and two thirds of the bar. The nine items of a legend with a title and three entries
(legend, title, entry group, three symbols and three labels) are charged exactly.

`LegendPlanTests` runs the real `SpecParser`, `ConfigResolver`, `DataLoader`, `ScaleResolver`, and `EncodingPlan`
through the shared helper `ChartTestHelpers` to plan the legends of specification texts; `LegendBuilderTests`
additionally builds them with the real `TextMeasurer`, `Ticks`, and `NumberFormat` and reads the scales of the same
specifications for the sizes, opacities, and widths that the scale maps (the scale arithmetic itself is verified with
the `ScaleModel` and `ScaleResolver` units). No mocking is used. The internal types are reached through the test
assembly's `InternalsVisibleTo` access. Unit tests reside in `LegendPlanTests.cs` and `LegendBuilderTests.cs` within
the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Guides` folder.

#### Test Environment

+ **Framework**: xUnit v3 running under the .NET SDK
+ **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
+ **Mocking**: None required
+ **Isolation**: Each test builds its own specification; the bundled Liberation fonts only (the widths in the
  expectations are measured with the same measurer, so no host font enters); no files, clock, or shared state; the
  limits are exercised with small values of `VegaLiteLimits` and a pre-canceled token, never with timing

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any position, text, color, size, count, or exception type that differs from the expectation constitutes a
failure.

#### Unit-Level Test Scenarios

##### Planning

**Tests**: `LegendPlanTests.LegendPlan_Create_ChannelsInVegaLiteOrderOneLegendPerField`,
`LegendPlanTests.LegendPlan_Create_NoLegendForNullLegendNullScaleValueOrDisabled`,
`LegendPlanTests.LegendPlan_Create_KindFollowsTheScaleAndTheTypeProperty`,
`LegendPlanTests.LegendPlan_Create_OrientAndDirectionFollowVegaLiteDefaults`,
`LegendPlanTests.LegendPlan_Create_TitleFollowsTheLegendTheChannelAndTheFieldTitle`,
`LegendPlanTests.LegendPlan_Create_MarkFactsDescribeFilledFixedPaintsAndOpacity`

Assert the order color, size, shape, opacity (with fillOpacity and angle absent), the absence of a legend for a null
legend, a null scale, a constant value, and a disabled configuration, the kind of each scale and `legend.type`, the
orient and direction defaults and overrides, the title precedence (legend, channel, field, aggregate), and the facts of
filled, unfilled, and shaped marks.

##### Merging

**Tests**: `LegendPlanTests.LegendPlan_Merge_ChannelsOfTheSameDomainShareOneLegend`,
`LegendPlanTests.LegendPlan_Merge_ExplicitOrientsThatDifferKeepLegendsApart`,
`LegendBuilderTests.LegendBuilder_Symbol_MergedChannelsCombineTheirProperties`

Assert that channels of one field share a legend in channel order with one title, that a different field, sort, or
explicit domain keeps legends apart, that explicit orients that differ do not merge, and that a merged symbol combines
the shape, stroke, and size of its channels.

##### Symbols And Entries

**Tests**: `LegendBuilderTests.LegendBuilder_Symbol_NominalColorOnAPointDrawsStrokedCirclesInOneColumn`,
`LegendBuilderTests.LegendBuilder_Symbol_QuantitativeSizeUsesTheTicksOfTheScale`,
`LegendBuilderTests.LegendBuilder_Symbol_SymbolLimitAddsAnEllipsisEntry`,
`LegendBuilderTests.LegendBuilder_Symbol_DiscretizingScaleLabelsUseRanges`,
`LegendBuilderTests.LegendBuilder_Symbol_FixedAndBasePaintsComeFromTheMarkAndTheConfiguration`,
`LegendBuilderTests.LegendBuilder_Symbol_OpacityStrokeWidthAndDashChannelsShowTheirValues`,
`LegendBuilderTests.LegendBuilder_Symbol_ValuesFormatAndSymbolPropertiesApply`

Assert the geometry and looks derived above, the seven tick entries with one decimal for a size, the limit with the
ellipsis entry, the range labels, the fixed and base paints, the scaled opacity, stroke width, and dash, and the explicit
values, format, and symbol properties.

##### Grid

**Tests**: `LegendBuilderTests.LegendBuilder_Symbol_ColumnsFillTopToBottomWithColumnPadding`,
`LegendBuilderTests.LegendBuilder_Symbol_HorizontalDirectionLaysTheEntriesOutInOneRow`

Assert the order and positions of four entries in two columns and of three entries in one row.

##### Gradient And Discrete Gradient

**Tests**: `LegendBuilderTests.LegendBuilder_Gradient_ContinuousColorDrawsAVerticalBarWithTickLabels`,
`LegendBuilderTests.LegendBuilder_Gradient_LengthFollowsTheDataRectangleAndDirection`,
`LegendBuilderTests.LegendBuilder_Gradient_FewerThanThreeTicksUseTheEndsOfTheDomain`,
`LegendBuilderTests.LegendBuilder_Gradient_ExplicitLengthThicknessTicksAndLabelProperties`,
`LegendBuilderTests.LegendBuilder_Gradient_DomainWithoutSpanUsesTheUnitRamp`,
`LegendBuilderTests.LegendBuilder_Discrete_QuantizeColorDrawsOneBandPerClass`,
`LegendBuilderTests.LegendBuilder_Discrete_ThresholdScaleIsWidenedByOneClassAtEachEnd`

Assert the bars, the stops, the labels and their alignment, the lengths, and the bands derived above.

##### Titles And Box

**Tests**: `LegendBuilderTests.LegendBuilder_Title_PaddingAnchorAndBoxPaint`,
`LegendBuilderTests.LegendBuilder_Title_NullTitleLeavesTheEntriesAtThePadding`,
`LegendBuilderTests.LegendBuilder_Build_ReportsOrientOffsetAndPositionForTheLayout`

Assert the centered title that is pushed in, the padding, the box paint and bounds, the legend without a title, and the
orient, offset, and position facts.

##### Limits, Cancellation, And Determinism

**Tests**: `LegendBuilderTests.LegendBuilder_Limits_AreEnforcedBeforeAnythingIsCreated`,
`LegendBuilderTests.LegendBuilder_Build_CancellationAndInvalidFormat`,
`LegendBuilderTests.LegendBuilder_Build_IsDeterministic`

Assert the located limit failures, the exact charge of nine scene items, the cancellation, the located specification
error of an invalid format, and that two builds give the same items.

##### Legends Of Log, Pow, And Sqrt Scales

**Tests**: `LegendBuilderTests.LegendBuilder_Gradient_LogScaleLabelsThePowersAlongTheLogarithm`,
`LegendBuilderTests.LegendBuilder_Symbol_LogSizeShowsThePowersWithEvenSizeSteps`,
`LegendBuilderTests.LegendBuilder_Gradient_SqrtScalePlacesTheLabelsAlongTheRoot`,
`LegendBuilderTests.LegendBuilder_Gradient_LogTicksAreLimitedAndCancellable`

Assert, for the readings 1, 10, 100, and 1000 on a log color scale, a gradient legend with the labels `1`, `10`,
`100`, and
`1,000` (a count of 4 over 28 log ticks keeps the digit 1: k = 1.43) at the heights 200, 133.3, 66.7, and 0 of the
200 pixel bar
(a third per decade), 28 stops at the log ticks whose offsets are the fractions along the logarithm (ln 2 / ln 1000
for the stop
of 2, a third for 10) with the colors of the scale at those values; that a log size legend has the entries 1, 10, 100, and
1,000 with sizes that grow by the same amount per decade; that a square root color legend places 2 and 3 of 1 to 4 at
`sqrt 2 - 1` and `sqrt 3 - 1` of the bar; and that 28 ticks against a limit of 5 fail with a located error naming the limit
and a canceled token stops the build.

##### Binned Legends

**Tests**: `LegendBuilderTests.LegendBuilder_Symbol_BinnedColorLabelsTheRangesOfTheBins`,
`LegendBuilderTests.LegendBuilder_Symbol_BinnedColorFormatValuesAndReverseApply`,
`LegendBuilderTests.LegendBuilder_Symbol_BinnedSizeShowsTicksAndOrdinalBinnedOpacityShowsTheBinText`,
`LegendBuilderTests.LegendBuilder_Merge_BinnedChannelsOfTheSameBinShareOneLegend`,
`LegendBuilderTests.LegendBuilder_Limits_BinnedLegendChargesItsItemsAndPollsTheToken`

Assert that a binned quantitative color (the bins 0 to 100 by 20) draws a symbol legend with five entries (one per bin,
not per edge) labeled `0 – 20` to `80 – 100`, with the strokes of the five colors of the range and the title
`q (binned)`; that a format of `.1f` gives `0.0 – 20.0` and so on, that the explicit values 20 and 60 give `20 – 60` and
`≥ 60` (the label of a value runs to the next value of the list, as in Vega) with the second and fourth colors, and that
`reverse` turns the colors and not the ranges; that a binned size shows the ticks 0, 20, ..., 100 of its linear scale
with the sizes of the scale and an ordinal binned opacity the texts of the five bins; that a binned color and a binned
fill of the same bin share one legend while a binned color and an unbinned fill of the same field are two; and that the
thirteen items of the legend are charged exactly (a budget of 12 fails naming `MaxSceneItems`) and a canceled token
stops the build.
