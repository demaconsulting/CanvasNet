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
the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Guides` folder; the legends of temporal fields (the time
ticks and date labels of a gradient, date symbols, a size over dates, and the zone) are in `LegendBuilderTimeTests.cs`,
in the same folder, with the expectations of the ten day data derived in the scenarios below from the d3 interval
table.

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

##### Conditional Legends

**Tests**: `LegendPlanConditionTests.LegendPlan_Create_ValueConditionsCreateNoScaleAndNoLegend`,
`LegendPlanConditionTests.LegendPlan_Create_FieldConditionCreatesTheScaleAndTheLegend`,
`LegendPlanConditionTests.LegendPlan_Create_FieldWithValueConditionKeepsItsLegend`,
`LegendPlanConditionTests.LegendBuilder_Symbol_SizeLegendShowsTheFirstConditionValueOfAFixedColor`,
`LegendPlanConditionTests.LegendBuilder_Symbol_OpacityOfConditionalChannelIsTheLargestValue`,
`LegendPlanConditionTests.LegendBuilder_Symbol_ConditionalLegendIsDeterministic`

Assert, for circles over three rows, that value conditions on a color, an opacity, and a size plan no legend; that a
color whose condition holds the field `g` plans one legend (the categories `a`, `b`, `c` in the first three colors of the
palette, titled `g`), as does a field with a value condition; that the symbols of a size legend are filled with the
value of the first condition of a color (`red`, not the later `blue` or the base `gray`) and with the base value when
there is no condition; that the symbols show the largest opacity of the base and condition values (0.9 for 0.2 and 0.9,
and 0.4 for 0.4 and 0.1); and that building the same legend twice gives equal symbols.

##### Temporal Gradient Legends

**Tests**: `LegendBuilderTimeTests.LegendBuilder_Time_TemporalGradient_LabelsUseConfigTimeFormat`,
`LegendBuilderTimeTests.LegendBuilder_Time_LegendFormatOverridesAndUnitSpecifier`

The color of three points on January 1, 6, and 11, 2024 is a gradient legend 200 pixels long, so it asks for max(2, 2 x
floor(200 / 100)) = 4 ticks; ten days / 4 = 2.5 days is nearest the two days of the d3 table (2.5 / 2 = 1.25 is below
7 / 2.5 = 2.8), so the labels are the days of the month 1, 3, 5, 7, 9, 11 in the format of `config.timeFormat` (`%b %d,
%Y`: `Jan 01, 2024`, ...). The gradient is sampled at the time ticks for a count of 15, a target of 16 hours, nearest
12 hours (16 / 12 = 1.33 is below 24 / 16 = 1.5): 00:00 and 12:00 of each day, 21 stops at offsets 0, 0.05, ..., 1 (the
tests compare to nine decimals) rather than at numeric ticks of the milliseconds; the title is `d`. A legend `format` of
`%d/%m` gives `01/01`, `03/01`, ..., a multi-format object (`date` `%d`, `year` `Y%Y`) gives `Y2024`, `03`, `05`, `Jan
07`, `09`, `11`, a configured `timeFormat` of `%Y.%m.%d` gives `2024.01.01`, ..., and a month unit over three months
(count 4 over 60 days targets 15 days, nearest the month, since 15 / 7 is not below 30 / 15) gives the unit specifier
labels `Jan 2024`, `Feb 2024`, `Mar 2024`.

##### Date Symbols, Sizes, And Explicit Values

**Tests**: `LegendBuilderTimeTests.LegendBuilder_Time_OrdinalTimeUnitSymbols_LabelWithTheUnitSpecifier`,
`LegendBuilderTimeTests.LegendBuilder_Time_SizeOnTimeScale_TicksAndLabels`,
`LegendBuilderTimeTests.LegendBuilder_Time_ExplicitValuesAcceptDatetimeObjects`

Assert that an ordinal color with a month unit has one symbol and one label per month in date order (`Jan`, `Feb`,
`Mar`), `January`, `February`, `March` under a `%B` format, and only `Feb` for `values: ["Feb"]`; that a size over the
dates has a symbol legend of the five default ticks, target two days, so six symbols labeled January 1, 3, ..., 11;
and that `values` of a date-time object (January 3), a date text (January 9), and a timestamp (1704067200000, which is
January 1) are labeled in the order given (`Jan 03, 2024`, `Jan 09, 2024`, `Jan 01, 2024`).

##### Temporal Legend Zone, Limits, Cancellation, And Determinism

**Tests**: `LegendBuilderTimeTests.LegendBuilder_Time_Zone_LabelsUseLocalTime`,
`LegendBuilderTimeTests.LegendBuilder_Time_TemporalColorUtcType_UsesTheUtcCalendar`,
`LegendBuilderTimeTests.LegendBuilder_Time_TemporalColor_ZeroIsIgnored`,
`LegendBuilderTimeTests.LegendBuilder_Time_LimitsAndCancellation`,
`LegendBuilderTimeTests.LegendBuilder_Time_LabelsTogetherTooLong_IsLocated`,
`LegendBuilderTimeTests.LegendBuilder_Time_FormatSpecifierLimits_AreSpecificationErrors`,
`LegendBuilderTimeTests.LegendBuilder_Time_Culture_InvariantAndDeterministic`

Assert that for dates at 00:00Z on January 1 and 11 in a +05:30 zone the two-day ticks are the local midnights of the odd
days, whose first (January 1, December 31 18:30Z) lies before the domain, so the labels are January 3 to 11 (five),
against the six labels from January 1 in UTC; that a color with `scale.type: "utc"` in a zone five hours behind UTC takes
the UTC calendar and the two-day ticks of the epoch days (2024-01-01 is day 19,723, odd, so the even days 19,724 to
19,732 give the labels January 2, 4, 6, 8, and 10), the same labels as without a zone, while `type: "time"` in the zone
gives other labels; that `scale.zero: true` on the temporal color is ignored (the labels stay the six dates of the data,
not the years back to 1970); that `MaxStringLength` of 10 fails a 12 character label (`Jan 01, 2024`)
with a located exception naming the limit of 10 while 12 passes with the single label of one explicit value, that the
six 12-character labels (72 in all) fail under a limit of 40 at the fourth with `together longer than the limit of 40
characters` and pass under 72, that an hourly `tickCount` over ten days
(241 ticks) against a `MaxTicks` of 5 fails with a located exception naming `MaxTicks`, and that a canceled token
throws `OperationCanceledException` while a live token builds the six labels; that a `config.timeFormat` or a legend
`format` member of 1,025 characters is a located `VegaLiteSpecException` naming `the limit of 1024 characters`; and that
the labels are the same under the Turkish, German, and invariant cultures and for two calls in a row.
