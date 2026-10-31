## Scales Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Scales` subsystem (internal namespace
`DemaConsulting.CanvasNet.VegaLite.Scales`), containing six units: `ScaleModel` (the linear, time, UTC, band, point,
ordinal, sequential, diverging, quantize, quantile, threshold, and bin-ordinal mappings), `ScaleResolver` (the
inference of scales from a specification and its data), `Ticks` (the tick, tick step, nice, log, and time tick
algorithms), `NumberFormat` (the
d3-format subset that labels axes), `ColorSchemes` (the 68 Vega color schemes and the configuration of a color range),
and `ColorInterpolator` (the color interpolation).

The time scales are verified in the same way. The resolver, the scale model, the tick algorithm, and the axis builder
run for real over original specifications in `ScaleResolverTimeTests`, `ScaleModelTimeTests`, `TicksTimeTests`, and (in
the `Guides` folder) `AxisBuilderTimeTests`, with the instants computed by the base class library and not by the
calendar under test (the calendar is the `TimeCalendar` unit of the `Data` subsystem, see the _TimeCalendar Unit
Verification Design_), and the zones built in the tests. The expected values are derived by hand from the days of 2024
and from the d3 interval table.

### Verification Approach

The `Scales` subsystem is verified at two tiers. The unit tests (see the unit verification designs under `scales/`)
verify each unit in isolation. The subsystem-level scenarios verify the units working together in the ways later stages
use them: the resolver reads a parsed specification, its loaded table (and, for the aggregating features, the encoding
plan and the Raw and Main tables of the implicit stage), and the resolved configuration, and produces
`ResolvedScale` objects whose `CreateModel` builds a `ScaleModel` for the data rectangle (`ScaleResolverTests` runs this
whole chain with the real `SpecParser`, `ConfigResolver`, `DataLoader`, and `ScaleModel`, and asserts the mapped
positions, the domains, and the ranges); and the axis stage derives its tick values and labels from a scale model's
domain with `Ticks` and `NumberFormat` (`AxisBuilderTests` runs this chain and asserts the tick values and label texts
of the whole axis against values derived by hand). The colors of a color scale come from `ColorSchemes`
through the resolver (`ScaleResolverColorTests` resolves color specifications with the real units and compares the
colors with the samples of the scheme ramps at hand-derived positions); the colors of a scheme are interpolated by
`ColorInterpolator` (`ColorSchemesTests` runs this chain with the real unit and compares the colors with hand-derived
values).

The algorithmic units (`Ticks`, `NumberFormat`, `ScaleModel`) are verified with vector tables whose expected values are
derived by hand from the published algorithms and from arithmetic, never by running d3 or Vega and pasting the output:
each test states its derivation in comments next to the vectors, and each unit verification design summarizes the
derivation. The subsystem uses no mocking or stubbing, performs no file or network input, and does not depend on the
current culture (a test runs the formats under a `de-DE` culture). The subsystem is internal, so its tests reach it
through the test assembly's `InternalsVisibleTo` access.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Files**: None; every specification is an in-memory string
- **Isolation**: Each test builds its own specification, table, and scales; the culture scenario restores the current
  culture in a `finally` block

### Acceptance Criteria

The `Scales` subsystem's verification passes when every subsystem-level scenario below and every unit-level scenario in
the five unit verification documents passes without error or unexpected exception.

### Test Scenarios

- **Specification To Scale Model: Positions Follow The Resolved Domain And Range**:
  `ScaleResolverTests.ScaleResolver_Resolve_AppliesZeroNiceDomainRangeAndPadding`,
  `ScaleResolverTests.ScaleResolver_Resolve_DiscreteSizesComeFromTheStep` — resolves scales for bar and scatter
  specifications from inline data and asserts the domains (zero and nice applied), the band steps and bandwidths of
  the created models, and the mapped positions in a data rectangle, using the model arithmetic of the _ScaleModel Unit
  Verification Design_.
- **Axis Ticks And Labels Come From The Scale Domain**:
  `AxisBuilderTests.AxisBuilder_Build_LinearAxisUsesExpectedTickValuesAndLabels`,
  `AxisBuilderTests.AxisBuilder_Build_ValuesCountFormatAndFlushAreHonored` — the tick values come from `Ticks` for the
  domain and the Vega-Lite tick count (a 300 pixel axis over 0 to 100 gives ticks 0, 10, ..., 100) and the labels
  from `NumberFormat` (the default `,f`, a `$,.2f` format, and the Unicode minus), asserted against values derived by
  hand from the tick algorithm.
- **Discrete Domains Are Ordered And Bounded**:
  `ScaleResolverTests.ScaleResolver_Resolve_DiscreteDomainsFollowTheSortProperty`,
  `ScaleResolverTests.ScaleResolver_Resolve_InvalidInputsFailWithLocation` — categories are sorted ascending by
  default or follow the data order, a descending sort, or an explicit array; a domain over `MaxScaleDomainSize` and
  invalid scale properties fail with a located specification or render exception.
- **Planned Fields, Sorts, And Domains Follow The Implicit Pipeline**:
  `ScaleResolverPlanTests.ScaleResolver_Resolve_SortsByAnotherChannelsAggregate`,
  `ScaleResolverPlanTests.ScaleResolver_Resolve_PreBinnedDomainIsTheUnionOfStartAndEnd`,
  `ScaleResolverPlanTests.ScaleResolver_Resolve_UnaggregatedDomainReadsTheRawRows`,
  `AxisBuilderTests.AxisBuilder_Build_BinnedAxisAsksForATickPerTenPixels` — the resolver reads the fields, bins, and
  stacks that the plan derived (the Raw and the Main table) and the axis stage takes its ticks from the resulting
  domain: categories are sorted by the sum, count, or other aggregate of another channel over the Raw rows, a
  pre-binned field has the union of its start and end as its domain with no zero and no nice, an `unaggregated` domain
  reads the rows before the aggregate (1 to 12 rather than the means 2 to 8), and a binned axis asks for one tick per ten
  pixels (the ticks 0, 2, ..., 34 for the bins 0 to 35).
- **Polar Channels Resolve To Angles And Radii For The Marks That Draw Them**:
  `ScaleResolverPolarTests.ScaleResolver_Resolve_ThetaHasTheFullCircleRange`,
  `ScaleResolverPolarTests.ScaleResolver_Resolve_StackedThetaDomainSpansTheStack`,
  `MarkChannelGatingTests.ScaleResolver_ChannelIsDrawn_FollowsTheSupportTable` — the resolver reads the stack that the
  plan derived for a pie chart (the slices 1, 1, and 2 stack to (0, 1), (1, 2), and (2, 4)) and gives the `theta`
  channel the domain 0 to 4 over the range 0 to 2 pi (a quarter turn for 1, a half turn for 2, and the whole turn for
  4), the `radius` channel the range from 0 to half of the smaller side of the data rectangle that the builder passes
  (100 for a chart of 200 by 200 pixels), and a channel that the mark does not draw (a size on an area or an arc, a stroke
  dash on a trail, a text, or an area, a theta on a line) no scale at all, so that the feature catalog and the builders
  agree on what is ignored.
- **Orientation Follows The Mark**:
  `ScaleResolverTests.ScaleResolver_InferOrient_FollowsVegaLite`,
  `ScaleResolverPolarTests.ScaleResolver_InferOrient_AreaRangeAndTemporalRulesFollowVegaLite` — the resolver infers
  the orientation of a bar, a tick, and a rule, of a line, an area, and a tick against a time axis, of an area with a
  secondary channel, and of a trail and an arc (always vertical), so that the builders extend a path along the axis that
  Vega-Lite extends it along.
- **Colors Come From The Configured Ranges**:
  `ScaleResolverTests.ScaleResolver_Resolve_OrdinalColorsUseTheConfiguredRanges`,
  `ColorSchemesTests.ColorSchemes_Tableau10_ReturnsDocumentedCategoryColors` — the ordinal color scale of a nominal
  field uses the ten `tableau10` colors in domain order, an ordinal field the blues ramp sampled for its categories, and
  an explicit range, a scheme, or a configured named range wins in that order; a quantitative color is a sequential or
  diverging scale, and the quantize, quantile, and threshold scales classify (`ScaleResolverColorTests`).
- **Tick Limits Fail Closed Through The Axis**:
  `TicksTests.Ticks_Ticks_ResultOverTheLimitFailsClosed`,
  `AxisBuilderTests.AxisBuilder_Build_InvalidSettingsAndLimitsFailWithLocations` — a tick result larger than the limit
  throws before it is allocated and the axis reports it with the location of the axis in the specification.
- **Formats Are Culture-Independent**:
  `NumberFormatTests.NumberFormat_Format_IgnoresTheCurrentCulture` — the label text is identical under a culture with
  a decimal comma.
- **Temporal Fields Resolve To Time Scales That Map, Tick, And Nice By The Calendar**:
  `ScaleResolverTimeTests.ScaleResolver_Temporal_XDefaultsToATimeScaleWithTheDataExtent`,
  `ScaleResolverTimeTests.ScaleResolver_TimeUnitBarDomainIncludesTheEnd`,
  `ScaleResolverTimeTests.ScaleResolver_NiceTrueAndInterval`,
  `ScaleModelTimeTests.ScaleModel_Time_MapsDatesLinearly`,
  `TicksTimeTests.Ticks_TimeTicks_InclusiveStop_Utc`,
  `TicksTimeTests.Ticks_TimeNice_WeekExample`,
  `AxisBuilderTimeTests.AxisBuilder_Time_YearMonthLabelsUseTheUnitSpecifier` — the resolver turns a temporal field into
  a `Time` or `Utc` scale over the dates of the data (the unit starts and, for a bar, the unit ends) in the calendar of
  the render's zone, the scale model maps the instants linearly, `Ticks` chooses the d3 interval nearest the target
  and the calendar places its boundaries, and the axis labels them by the unit. The hand-derived chain of the month
  bars is a domain of January 1 to April 1 (91 days, so 300 pixels give January 102.198 pixels) with the three ticks of
  the month starts at 0, 102, 198, and 300 after the minimum step of a month lowered the count from 8 to 3, and `nice`
  extends the domain of January 10 13:00Z to March 20 05:00Z to the Sundays January 7 and March 24 (6.97 days a tick,
  the week).
- **Time Scales Are Zone-Aware And UTC Scales Are Not**:
  `ScaleResolverTimeTests.ScaleResolver_UtcTimeUnit_GivesAUtcScale`,
  `ScaleResolverTimeTests.ScaleResolver_NiceInAZone_UsesLocalBoundaries`,
  `TicksTimeTests.Ticks_TimeTicks_Month_Boundaries_InAZone`,
  `AxisBuilderTimeTests.AxisBuilder_Time_Zone_LabelsAndTicksUseLocalMidnights`,
  `AxisBuilderTimeTests.AxisBuilder_Time_UtcScale_IgnoresZone` — a local time unit keeps the zone's calendar and a UTC
  one the UTC calendar, so the local `nice` and the ticks are local midnights (18:30Z of the day before in a +05:30
  zone, and a day of 23 hours across the daylight saving change), while a UTC scale gives the same ticks and labels
  whatever the zone is.
- **Time Ticks And Labels Fail Closed And Do Not Depend On The Culture**:
  `TicksTimeTests.Ticks_TimeTicks_MaxTicksExceeded_Throws`,
  `AxisBuilderTimeTests.AxisBuilder_Time_TooManyTicks_IsLocated`,
  `TicksTimeTests.Ticks_Time_Culture_Invariant`,
  `AxisBuilderTimeTests.AxisBuilder_Time_Culture_Invariant` — a tick result over `MaxTicks` throws before it is
  allocated and the axis reports it located at the channel, and the ticks and the English labels are identical under
  the Turkish and German cultures.
