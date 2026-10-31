## Guides Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Guides` subsystem (internal namespace
`DemaConsulting.CanvasNet.VegaLite.Guides`), containing six units: `TextMeasurer` (font resolution, text
measurement, truncation, line splitting, and Vega text bounds), `TitleBuilder` (the title and subtitle scene items and
their extent), `AxisBuilder` (the axis grid, ticks, labels, domain, and title, and the extent they need), `LabelOverlap`
(Vega's parity and greedy removal of overlapping labels), `LegendBuilder` (which channels have legends and how they
merge, and the symbol, gradient, and discrete gradient legends with the box each needs), and `TimeLabels` (the label
format of a date, the interval `tickCount`, the default tick count rule of a time axis, and the dates of an explicit
`values` list, shared by the axes and the legends of temporal fields).

### Verification Approach

The `Guides` subsystem is verified at two tiers. The unit tests (see the unit verification designs under `guides/`)
verify each unit in isolation. The subsystem-level scenarios verify the units working together and with the units they
are fed by: both `TitleBuilder` and `AxisBuilder` measure all their text through one real `TextMeasurer` (the bundled
Liberation fonts), so that every extent a test derives by hand from a measured text width, a font size, and the Vega
text constants is checked through the whole chain. `AxisBuilderTests` additionally runs the real `SpecParser`,
`ConfigResolver`, `DataLoader`, `ScaleResolver`, `Ticks`, and `NumberFormat` (through the shared helper
`ChartTestHelpers`) to produce the scale and the axis request, and asserts the scene items and the extent of the whole
axis. `VegaLiteDocumentLayoutTests` and the chart integration tests verify the guides as the layout and the painter see
them: the title is painted inside the band that the layout reserves for it.

The guides of temporal fields are verified through the whole calendar chain. The axis and the legend of a time scale
get their ticks from the real `Ticks` over the real `TimeCalendar` (the d3 interval nearest to the span over the tick
count, in the zone of the chart or in UTC), their labels from the one shared `TimeLabels` (the `format` text or object,
the unit specifier, `config.timeFormat`, or Vega's multi-format of the real `TimeFormat`), and their `values` from the
real `JsDateParser` and `DateTimeObject`; the axis and the legend of the same date therefore read the same label. The
scenarios assert on the tick values and the labels derived by hand from the d3 interval table (for example ten days over
400 pixels target one day and give eleven daily ticks, and three months over 300 pixels are limited to three ticks by
the minimum step of a month and give the four month starts), and on the positions of the ticks on the scale. Every zone
is a custom zone that the test creates and no test reads the host's zone, clock, or culture.

No mocking or stubbing is used: font measurement is real, which is what makes the layout independent of installed
fonts (the unit tests assert that generic and Arial-like family names resolve to the very same bundled font object
whether or not system fonts are allowed). The subsystem performs no network or data-file input; font files are read
only from the bundled resources and, in the one scenario that exercises `AdditionalDirectories`, from a temporary
directory created and deleted by the test. The subsystem is internal, so its tests reach it through the test
assembly's `InternalsVisibleTo` access.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The Liberation fonts bundled with the core package; the single test that needs an installed non-generic
  font skips itself (`Assert.Skip`) on a machine without one, and every other test is independent of installed fonts
- **Files**: A per-test temporary folder holding a copy of a bundled font file, deleted by the test
- **Isolation**: Each test builds its own specification, measurer, and builders. Every time zone is a custom zone that
  the test creates; the time scenarios repeat under `tr-TR` and `de-DE` and never read the host's zone or clock

### Acceptance Criteria

The `Guides` subsystem's verification passes when every subsystem-level scenario below and every unit-level scenario in
the six unit verification documents passes without error or unexpected exception. A skipped scenario that is
documented above as machine-dependent is not a failure.

### Test Scenarios

- **Titles And Axes Measure Through The Same Font Resolution**:
  `TitleBuilderTests.TitleBuilder_Build_OrientAnchorAndSubtitleProduceExpectedExtent`,
  `AxisBuilderTests.AxisBuilder_Build_TitleAndExtentMatchAnalyticLayout` — the title extent equals the measured width
  of the title text at 13 pixels bold and its height equals the font size; the axis extent equals the tick length, the
  label padding, the label height, the title padding, and the 11 pixel title, with every text box measured by the same
  real measurer.
- **Axis Of A Parsed Specification**:
  `AxisBuilderTests.AxisBuilder_Build_LinearAxisUsesExpectedTickValuesAndLabels`,
  `AxisBuilderTests.AxisBuilder_Build_BandAxisUsesExpectedCategoriesAndGrid` — a parsed specification's scale produces
  an axis whose tick values, label texts, and positions equal the values derived by hand from the tick algorithm and
  the scale arithmetic.
- **Axis Titles And Ticks Follow The Aggregating Encoding**:
  `AxisBuilderTests.AxisBuilder_Build_TitlesFollowThePlanAndACountHasAGrid`,
  `AxisBuilderTests.AxisBuilder_Build_BinnedAxisAsksForATickPerTenPixels`,
  `AxisBuilderTests.AxisBuilder_Build_NormalizedStackUsesTheNormalizedNumberFormat`,
  `VegaLiteAggregationIntegrationTests.CanvasNetVegaLite_SystemIntegration_PreBinned_RangesAreBarsOverTheirRanges` — the
  axis title of an aggregated, binned, or counted channel is the plan's title (`Sum of v`, `v (binned)`, `Count of
  Records`) unless the specification gives one, a binned axis has no grid and asks for a tick per ten pixels, a
  normalized stack is labeled in percent, and the merged x title of pre-binned ranges names both fields.
- **Guide Configuration Precedence**:
  `AxisBuilderTests.AxisBuilder_Build_ConfigurationPrecedenceFollowsVegaLite`,
  `TitleBuilderTests.TitleBuilder_Build_PropertiesLinesConfigurationAndLimit` — the configuration, styles, and the
  explicit properties of the specification combine in Vega-Lite's order for both guide kinds.
- **Guides Fail Closed With A Location**:
  `AxisBuilderTests.AxisBuilder_Build_InvalidSettingsAndLimitsFailWithLocations` — an invalid orient or format is a
  specification error and too many ticks a render error, each pointing into the specification, and cancellation is
  honored.
- **Guides Are Painted Where The Layout Reserves Room**:
  `VegaLiteDocumentLayoutTests.VegaLiteDocument_Render_TitleIsPaintedAboveThePlotAndCenteredOnIt`,
  `CanvasNetVegaLite_SystemIntegration_Render_InlineBarHasExpectedSamplePixels` — the title and the axis texts leave ink
  in the bands that the title and axis extents reserve, and the padding is untouched.
- **Overlapping Labels Of Axes And Legends Are Thinned Alike**:
  `LabelOverlapTests.LabelOverlap_Hidden_ParityKeepsEveryOtherLabelUntilNothingOverlaps`,
  `LabelOverlapTests.LabelOverlap_Hidden_GreedyHidesLabelsThatOverlapTheLastOneKept`,
  `AxisBuilderTests.AxisBuilder_Build_OverlappingLabelsAreRemovedByParity` — the extracted unit hides the labels derived
  by hand, and the axis, which now calls it, still keeps 0, 40, and 80 of 21 ticks.
- **Legends Of A Parsed Specification**:
  `LegendPlanTests.LegendPlan_Merge_ChannelsOfTheSameDomainShareOneLegend`,
  `LegendBuilderTests.LegendBuilder_Symbol_NominalColorOnAPointDrawsStrokedCirclesInOneColumn`,
  `LegendBuilderTests.LegendBuilder_Gradient_ContinuousColorDrawsAVerticalBarWithTickLabels`,
  `LegendBuilderTests.LegendBuilder_Discrete_QuantizeColorDrawsOneBandPerClass` — a parsed specification's scales produce
  merged legends whose entries, labels, gradient stops, and box sizes equal the values derived by hand from Vega's legend
  rules and the measured text widths, through the whole chain of parser, scales, ticks, number format, and measurer.
- **Legends Fail Closed With A Location**:
  `LegendBuilderTests.LegendBuilder_Limits_AreEnforcedBeforeAnythingIsCreated`,
  `LegendBuilderTests.LegendBuilder_Build_CancellationAndInvalidFormat` — too many values, ticks, characters, or scene
  items is a render error that names the limit and points into the specification, an invalid format a specification
  error, and cancellation is honored.
- **Fonts Do Not Depend On The Machine**:
  `TextMeasurerTests.TextMeasurer_Resolve_DefaultUsesBundledFonts`,
  `CanvasNetVegaLite_SystemIntegration_Fonts_DefaultUsesBundledLiberation` — layout and pixels of text are identical
  with system fonts allowed or not, for every generic and Arial-like family name.
- **A Time Axis Has The Ticks And Labels Of The Calendar**:
  `AxisBuilderTimeTests.AxisBuilder_Time_DefaultLabelsAreMultiFormat`,
  `AxisBuilderTimeTests.AxisBuilder_Time_YearMonthLabelsUseTheUnitSpecifier`,
  `AxisBuilderTimeTests.AxisBuilder_Time_TickCountNumberObjectAndInterval`,
  `AxisBuilderTimeTests.AxisBuilder_Time_TickCountDefaultMonthUnitUsesVegaDefault10`,
  `AxisBuilderTimeTests.AxisBuilder_Time_TickMinStepFromTheUnit`,
  `TimeLabelsTests.TimeLabels_UsesVegaDefaultCount_OnlyForSingleMonthHoursDayQuarter`,
  `VegaLiteTimeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Time_PointsSitAtTheirDatesOnAMultiFormatAxis`,
  `VegaLiteTimeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Time_YearMonthBarsSpanTheirMonthsAndLabelTheMonths`
  — ten days over 400 pixels (the count is ceil(400 / 40) = 10, so the target is one day) have eleven daily ticks 40
  pixels apart labeled by the coarsest boundary they sit on (`2024` for January 1, `Jan 07` for the Sunday, and the
  weekday and date for the rest), a count of 5 targets two days, and `week`, `{day, 5}`, and `month` give one, three, and
  one tick; three months as `yearmonth` over 300 pixels have the default count ceil(300 / 40) = 8 lowered to
  floor(91 / 31) + 1 = 3 by the minimum step of a month and give the four month starts at 0, 102, 198, and 300 pixels
  (31 / 91 x 300 = 102.198 and 60 / 91 x 300 = 197.802) with the labels `Jan 2024` to `Apr 2024`, and the bars of the
  three months span their months with a pixel of spacing off every pair of edges.
- **Time Axes Follow The Zone, The Format, And The Dates Of A Unit**:
  `AxisBuilderTimeTests.AxisBuilder_Time_Zone_LabelsAndTicksUseLocalMidnights`,
  `AxisBuilderTimeTests.AxisBuilder_Time_UtcScale_IgnoresZone`,
  `AxisBuilderTimeTests.AxisBuilder_Time_FormatStringAndObject`,
  `AxisBuilderTimeTests.AxisBuilder_Time_FormatTypeNumberAndTime`,
  `AxisBuilderTimeTests.AxisBuilder_Time_ValuesAcceptDatetimeObjects`,
  `AxisBuilderTimeTests.AxisBuilder_Time_DiscreteDateLabels_UseUnitSpecifier`,
  `AxisBuilderTimeTests.AxisBuilder_Time_LabelFlushAndOverlapDefaults`,
  `AxisBuilderTimeTests.AxisBuilder_Time_NoRotationDefaultForTimeUnitOrdinal`,
  `TimeLabelsTests.TimeLabels_Create_ChoosesTheFormatInTheOrderOfVegaLite`,
  `TimeLabelsTests.TimeLabels_Create_FormatTypesNumberAndUtc`,
  `TimeLabelsTests.TimeLabels_ReadInstant_NumbersTextsObjectsAndNonDates` — at +05:30 the domain of two dates at 00:00Z
  runs from local 05:30, so the ticks are the ten local midnights, the first at 18.5 / 240 x 400 = 30.83 (31) pixels, a
  `utc` scale has the UTC midnights whatever the zone is, a `format` text and object and the format types `number`,
  `time`, and `utc` choose the labels in the order of Vega-Lite (`2024-01-01`, `1,704,067,200,000`, `00:00:00`), the
  `values` take a timestamp, a date text, and a datetime object and drop what is not a date or is off the scale, the
  dates of an ordinal time unit are labeled `Jan`, `Feb`, and `Mar` (or by the format) and not turned, and the labels of
  a dense axis are thinned by parity unless the overlap method is off.
- **Temporal Legends Are Labeled With Dates In The Zone Of The Chart**:
  `LegendBuilderTimeTests.LegendBuilder_Time_TemporalGradient_LabelsUseConfigTimeFormat`,
  `LegendBuilderTimeTests.LegendBuilder_Time_LegendFormatOverridesAndUnitSpecifier`,
  `LegendBuilderTimeTests.LegendBuilder_Time_OrdinalTimeUnitSymbols_LabelWithTheUnitSpecifier`,
  `LegendBuilderTimeTests.LegendBuilder_Time_SizeOnTimeScale_TicksAndLabels`,
  `LegendBuilderTimeTests.LegendBuilder_Time_ExplicitValuesAcceptDatetimeObjects`,
  `LegendBuilderTimeTests.LegendBuilder_Time_Zone_LabelsUseLocalTime`,
  `VegaLiteTimeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Time_TemporalColorLegendShowsDates` — a gradient
  legend of a temporal color 200 pixels long has the tick count 4, so the target is 2.5 days and the labels are the
  two-day ticks `Jan 01, 2024`, `Jan 03, 2024`, and so on in the format of `config.timeFormat`, its ramp is sampled at
  the time ticks of a count of 15 (every 12 hours, 21 stops from the first date to the last), the legend format, the
  configuration, and the unit specifier choose other labels, a size over dates has six symbols, an ordinal time unit has
  one symbol and label per month, and at +05:30 the first tick is the 3rd because local midnight of the 1st is before
  the domain.
- **Time Guides Fail Closed, Are Deterministic, And Ignore The Culture**:
  `AxisBuilderTimeTests.AxisBuilder_Time_TooManyTicks_IsLocated`,
  `AxisBuilderTimeTests.AxisBuilder_Time_LabelTooLong_IsLocated`,
  `AxisBuilderTimeTests.AxisBuilder_Time_Cancellation`,
  `AxisBuilderTimeTests.AxisBuilder_Time_Culture_Invariant`,
  `AxisBuilderTimeTests.AxisBuilder_Time_Deterministic`,
  `LegendBuilderTimeTests.LegendBuilder_Time_LimitsAndCancellation`,
  `LegendBuilderTimeTests.LegendBuilder_Time_Culture_InvariantAndDeterministic`,
  `TimeLabelsTests.TimeLabels_Create_LabelsAreBoundedByTheLimit`,
  `VegaLiteTimeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Time_Limits_TickCountAndStringLimitsAreEnforced` —
  more ticks than `MaxTicks` and a label longer than `MaxStringLength` are located render errors that name the limit
  (11 days, a millisecond interval over ten days, and six listed values against a limit of 5; a 43-character label
  against 30), a canceled token stops the build, and the labels are the English ones with ASCII digits under `tr-TR` and
  `de-DE` and the same on every call.
