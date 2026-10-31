### AxisBuilder Unit Verification Design

This document describes the unit-level verification strategy for the `AxisBuilder` unit in the
`DemaConsulting.CanvasNet.VegaLite.Guides` namespace: the internal `AxisBuilder` class (`Build` and the static
`GetExtents`) and the `AxisRequest`, `AxisTick`, and `AxisResult` types of its file. The unit builds the axis guide of a
linear, time, UTC, band, or point position scale: the grid (a group of its own that is drawn behind everything), the
tick marks,
the labels, the domain line, and the title, as scene items inside an axis group positioned at the data rectangle edge
with Vega's half pixel shift, and it reports the extent that the guide needs beyond the data rectangle so that the
layout can grow the canvas.

#### Verification Approach

The unit is verified by building axes for original specifications through the shared helper `ChartTestHelpers.Axis`,
which runs the real `SpecParser`, `ConfigResolver`, `DataLoader`, and `ScaleResolver`, creates the scale model for a
data rectangle of a chosen size, and calls the builder with a real `TextMeasurer`. The assertions are on the scene
items (roles, positions, strings, alignments, baselines, angles, strokes, font sizes), on the tick list (values,
labels, positions), and on the extents. No mocking or stubbing is used; the collaborators are real because the unit's
contract is how the scale, the tick algorithm, the number format, the configuration, and the text measure combine. The
internal types are reached through the test assembly's `InternalsVisibleTo` access.

The expected values are derived by hand from the d3 tick algorithm, the scale arithmetic, and the Vega axis layout
rules, and not recorded from the code:

- A linear x axis of 0 to 100 over 300 pixels asks for ceil(300 / 40) = 8 ticks; d3 `ticks(0, 100, 8)` has 100 / 8 = 12.5,
  power 10, error 1.25 below the square root of 2, so the step is 10 and the ticks are 0, 10, ..., 100 (eleven); a y axis
  of 0 to 50 over 200 pixels asks for 5 ticks, 50 / 5 = 10 has error 1, so the ticks are 0, 10, ..., 50 (six). Tick positions
  are 3 pixels per unit on x and 200 - 4 x value on y, labels sit at the unrounded position, tick marks are 5 pixels
  long, grid lines run the 200 pixels of the data rectangle, the x axis group is at (0.5, 200.5), and `labelFlush` aligns
  the first label left and the last label right while the others are centered.
- Three bands over 300 pixels with the bar padding (inner 0.1, outer 0.05) have step 300 / (3 - 0.1 + 0.1) = 100, band
  width 90, and the first band at (300 - 100 x 2.9) / 2 = 5; the tick of a band is at round(5 + 45 - 0.5) =
  round(49.5) = 50 (half up), so the ticks are at 50, 150, 250, while the labels use the unrounded 49.5, 149.5,
  249.5 and, for a discrete x axis, Vega-Lite's default angle of 270. A point scale has no band offset.
- The bottom axis extent: tick marks are rules from 0 to 5 that grow by half their 1 pixel width (5.5); a 10 pixel label
  hangs from y = 5 + 2 = 7 (the baseline offset round(0.79 x 10) = 8 cancels the box offset round(0.8 x 10) = 8), so its
  box is 7 to 17; the extent is 17; the 11 pixel title hangs from 17 + 4 = 21 and ends at 32, so the bottom extent is 32,
  the group's bounds end at 232, and the side extents are the half pixel shift 0.5. For a left axis, the labels end 7
  pixels left of the data rectangle, so the extent is 7 plus the measured width of the widest label ("50") and the
  rotated title is at x = -(extent + 4), for a total of extent + 4 + 11. `minExtent: 40` pushes the title to 44, and
  `offset: 10` moves the group to y = 210.5 and the bounds to 200 + 10 + 44 + 11.
- Label overlap by parity: 21 ticks 5 pixels apart (`tickCount` 20 over 100 pixels) each have a label wider than the
  spacing; parity keeps every other label until none overlaps: 21 becomes 11 (every 10), then 6 (0, 20, ..., 100,
  where "80" and the right-aligned "100" still overlap), then 3 (0, 40, 80), which no longer overlap.
- Explicit values: `values` of 25, 0, 50, 500, 100 on a 0 to 100 domain over 300 pixels keep the values in range order,
  drop 500, and give positions 0, 75, 150, 300 with the `.1f` labels `0.0`, `25.0`, `50.0`, `100.0`; `tickCount: 2` gives
  100 / 2 = 50, error 5 (from the square root of 10 up to the square root of 50), step 50, so the labels are `0`, `50`,
  `100`; a `$,.2f` format gives `$0.00` and `$100.00`.
- Binned and normalized axes: the twelve weights of the histogram binned into 0 to 35 by 5 over 200 pixels ask for one tick
  per ten pixels (20), which the d3 algorithm turns into the step 2, so the ticks are 0, 2, ..., 34 (eighteen) and the
  explicit `tickCount` of 5 gives 0, 5, ..., 35 (eight); a field that is binned already keeps one tick per 40 pixels
  (5), so the domain 0 to 30 has 0, 5, ..., 30 (seven); a normalized stack has the domain 0 to 1, so a 100 pixel axis
  has the ticks 0, 0.5, and 1, labeled `0%`, `50%`, and `100%` by the default `.0%`, `0.0%`, `50.0%`, and `100.0%` by a
  configured `.1%`, and `0.00`, `0.50`, and `1.00` by an explicit `.2f`.

Unit tests reside in `AxisBuilderTests.cs` and `AxisBuilderTimeTests.cs` within the
`DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Guides` folder; the shared helpers `ChartTestHelpers`
(`Parts`, `Axis`, `Marks`, and `Of`) are in `ChartTestHelpers.cs`.

The time axes are verified with two original specifications whose numbers are small enough to derive by hand: two
points ten days apart (January 1 and 11, 2024) on an x axis of 400 pixels, and three monthly bars (the unit `yearmonth`)
whose domain is January 1 to April 1, 91 days. The instants are computed with the base class library and the
expectations are derived from the d3 interval table, the multi-format rule, and the scale arithmetic:

- Ten days over 400 pixels ask for ceil(400 / 40) = 10 ticks, so the target is 10 days / 10 = 1 day, which is in the d3
  table, and the ticks are the eleven midnights January 1 to 11 (the stop is included), 40 pixels apart (day 5 at 200).
  The default label is the coarsest boundary a tick sits on (Vega's multi-format): January 1 starts a year, so it reads
  `2024`; January 7 is a Sunday (a week start that is not a month start), so `Jan 07`; the other days are weekdays,
  `Tue 02`, `Wed 03`, ..., `Thu 11`.
- The month bars over 300 pixels ask for 8 ticks, which the minimum step of the unit (one month, 31 days) lowers to
  floor(91 / 31) + 1 = 3 (a time scale does not run the numeric tick step loop); three ticks over 91 days target
  30.33 days, nearest the month entry (30 days), so the ticks are the month starts January 1 to April 1 (the end
  included) with the labels of the unit specifier (`Jan 2024`, ..., `Apr 2024`) at 0, 31 / 91 x 300 = 102.198
  (rounded 102), 60 / 91 x 300 = 197.802 (198), and 300. The title is `date (year-month)`.
- A tick count of 5 over ten days targets 2 days, so the ticks are the odd days of the month 1, 3, ..., 11 (80 pixels
  apart); the interval `week` has the Sunday January 7 only (at 6 x 40 = 240); `{interval: day, step: 5}` has the days
  1, 6, and 11; the interval `month` has only January 1. One hour per 40 pixels over 800 pixels of 23 hours would be
  20 ticks, but Vega-Lite leaves a field whose unit is exactly `hours` the Vega default of ten: 2.3 hours is nearer 3
  than 1 (2.3 / 1 is not below 3 / 2.3), so every 3 hours and 8 ticks, where `hoursminutes` keeps 20 and the hour (24
  ticks).
- In a +05:30 zone the domain of January 1 and 11 at 00:00Z runs from 05:30 local on January 1 to 05:30 local on January
  11, so the ticks are the local midnights of January 2 to 11 (18:30Z of the day before), and the first is 18.5 hours
  after the start, 18.5 / 240 x 400 = 30.83 pixels (rounded 31); a UTC scale ignores the zone.
- January 1, 2024 is day 19,723 (54 x 365 + 13 leap days), 19,723 x 86,400,000 = 1,704,067,200,000 ms, which the number
  format prints with grouped digits (the culture scenario pins the same text under de-DE).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled Liberation fonts through a real `TextMeasurer`; label widths are measured by the test with the
  same measurer where they enter an extent, so no width is a recorded constant
- **Isolation**: Each test builds its own specification text; no files are read; the cancellation scenario uses an
  already canceled token

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any tick value, label, position, alignment, baseline, angle, stroke, font size, extent, or z-order that differs
from the hand-derived expectation, or any exception of the wrong type or without the documented location, constitutes a
failure.

#### Unit-Level Test Scenarios

##### Linear Axis Ticks, Labels, Grid, And Domain

**Tests**: `AxisBuilderTests.AxisBuilder_Build_LinearAxisUsesExpectedTickValuesAndLabels`

Asserts the derived x and y ticks, labels, positions, tick marks, grid lines, domain, group offsets, and flush
alignment; that the roles in the axis group are `axis-tick`, `axis-label`, `axis-domain`, and `axis-title` while the
grid is its own group (`axis-grid-group`) at the same offset; that the axis is not in front; and that the y axis is on
the left with its labels right aligned, middle based, 7 pixels left of the data rectangle.

##### Band And Point Axis Categories

**Tests**: `AxisBuilderTests.AxisBuilder_Build_BandAxisUsesExpectedCategoriesAndGrid`

Asserts the derived band ticks, the unrounded label positions and the 270 degree default angle, no grid by default,
`labelAngle: 0` giving centered labels hanging below the tick with `grid: true` drawing a line at each tick, `values`
keeping the listed categories that exist in range order (`a` before `c`, `zzz` dropped), and point scale positions.

##### Title And Extent Match The Analytic Layout

**Tests**: `AxisBuilderTests.AxisBuilder_Build_TitleAndExtentMatchAnalyticLayout`

Asserts the derived title position (150, 21, centered, top baseline, 11 pixel bold black), the bottom bounds and
extents, the left axis title and extent, and the effect of `minExtent` and `offset`.

##### Orientation And Part Switches

**Tests**: `AxisBuilderTests.AxisBuilder_Build_OrientAndPartSwitchesChangeTheItems`

Asserts that a top axis sits at the top edge with ticks pointing up (-5), labels above them (y = -7, bottom baseline),
and its title above the labels (y = -21); that a right axis is at x = 300.5 with left-aligned labels and a title rotated
by 90 degrees; that switching off the domain, ticks, labels, grid, and title leaves an empty group with no extent and
no items; and that `axis: null` produces no axis.

##### Label Overlap By Parity

**Tests**: `AxisBuilderTests.AxisBuilder_Build_OverlappingLabelsAreRemovedByParity`

Asserts the derived result (21 ticks kept, labels `0`, `40`, `80`), that `labelOverlap: false` keeps all 21 labels,
and that the tick marks are not removed.

##### Values, Counts, Formats, Flush, And Rounding

**Tests**: `AxisBuilderTests.AxisBuilder_Build_ValuesCountFormatAndFlushAreHonored`

Asserts the derived explicit values, count, and format results, that without flush the end labels are centered, and that
`tickRound: false` keeps the fractional band positions 49.5, 149.5, 249.5 instead of rounding.

##### Configuration Precedence

**Tests**: `AxisBuilderTests.AxisBuilder_Build_ConfigurationPrecedenceFollowsVegaLite`

Asserts, with `config.axis`, `config.axisX`, `config.axisBottom`, and `config.axisQuantitative` all defining properties,
that the most specific configuration wins (a tick size of 7 from `axisBottom` over 8 and 9, a red domain, a blue
grid, 12 pixel labels, a 14 pixel title), that the axis object of the channel wins over all of them (tick size 4, a
domain width of 3 alongside), that properties nobody names keep Vega's defaults, and that `config.axisBand` applies to
a band scale (tick size 6).

##### Z-Order

**Tests**: `AxisBuilderTests.AxisBuilder_Build_AxesOfRectMarksWithDiscreteChannelsAreInFront`

Asserts that axes are behind the marks by default (a bar chart), in front of a `rect` mark with discrete channels, in
front with an explicit `zindex: 1`, and behind with an explicit `zindex: 0` on a `rect` chart.

##### Titles And Grid Follow The Plan

**Tests**: `AxisBuilderTests.AxisBuilder_Build_TitlesFollowThePlanAndACountHasAGrid`

Asserts that the axis title of an aggregated, binned, or counted channel is the default title of the plan (`Sum of v`,
`v (binned)`, and `Count of Records`), that an explicit title (`Total`) still wins, and that a count (a definition
without a field) and an aggregated measure have a grid while a binned axis has none.

##### Binned Axis Asks For A Tick Per Ten Pixels

**Tests**: `AxisBuilderTests.AxisBuilder_Build_BinnedAxisAsksForATickPerTenPixels`

Asserts the derived tick values above: 0, 2, ..., 34 for a binned field over 200 pixels, 0, 5, ..., 35 for an explicit
`tickCount` of 5, and 0, 5, ..., 30 for a field that is binned already.

##### Normalized Stack Uses The Normalized Number Format

**Tests**: `AxisBuilderTests.AxisBuilder_Build_NormalizedStackUsesTheNormalizedNumberFormat`

Asserts the derived labels above for the default, the configured, and the explicit format, that an ordinary stack has no
percent label, and that its axis title is `n`.

##### Invalid Settings, Limits, And Cancellation

**Tests**: `AxisBuilderTests.AxisBuilder_Build_InvalidSettingsAndLimitsFailWithLocations`

Asserts that an invalid orient for the channel (`left` on x) and an invalid d3 format (`!!`) throw
`VegaLiteSpecException` at `/encoding/x/axis/orient` and `/encoding/x/axis/format` (with the file and a line); that
more ticks than `MaxTicks` (a continuous axis with a count of 100 over 3,000 pixels and a limit of 5, and a band axis
with three categories and a limit of 2) throw `VegaLiteRenderException` at `/encoding/x` naming `MaxTicks`; that a
canceled token throws `OperationCanceledException`; and that a null request throws `ArgumentNullException`.

##### Log Axis

**Tests**: `AxisBuilderTests.AxisBuilder_Build_LogAxisHasDecadeTicksAndFilteredLabels`,
`AxisBuilderTests.AxisBuilder_Build_LogAxisHonorsValuesTickCountFormatAndNegativeDomains`,
`AxisBuilderTests.AxisBuilder_Build_LogAxisThinsOverlappingLabelsGreedilyByDefault`,
`AxisBuilderTests.AxisBuilder_Build_PowSqrtAndSymlogAxesUseOrdinaryTicksAtTransformedPositions`,
`AxisBuilderTests.AxisBuilder_Build_LogAxisEnforcesTheTickLimitAndCancellation`

Assert, for 1 to 1000 over 300 pixels, 28 ticks (1 to 9, 10 to 90, 100 to 900, 1000) at the positions 300, 200, 100,
and 0
for the powers, and that the labels of the digits above 3 are empty (k = 10 * 10 / 28 = 3.57) while the others use the grouped
variable-precision format (`1`, `2`, `3`, `10`, ..., `1,000`); that explicit values are all labeled, a tick count of 2 gives
the powers 1 and 100, a precision format keeps its zeros (`1.0`) and a type without one is trimmed (`10k`, `1M`),
and -1000 to
-1 gives 28 ascending ticks with the labels `−1,000`, `−300`, ..., `−1`; that on 40 pixels the ten labels overlap and are
thinned by default but all drawn with `labelOverlap: false`; that a pow axis (the square of 0 to 100 over 200
pixels) has the
ordinary ticks 0, 20, ..., 100 at the positions 200, 192, 168, 128, 72, and 0, a sqrt axis puts 20 at the square
root, and a
symlog axis of -100 to 100 has the ticks -100, -50, 0, 50, 100 with zero in the middle; and that a limit of 5 gives a
`VegaLiteRenderException` at `/encoding/y` containing `the limit of 5` and a canceled token an `OperationCanceledException`.

##### Time Axis Defaults: Multi-Format Labels, Grid, Flush, And Overlap

**Tests**: `AxisBuilderTimeTests.AxisBuilder_Time_DefaultLabelsAreMultiFormat`,
`AxisBuilderTimeTests.AxisBuilder_Time_LabelFlushAndOverlapDefaults`

Assert the eleven derived ticks of the ten day axis (instants, positions 0, 40, ..., 400, and the labels `2024`, `Tue
02`, ..., `Jan 07`, ..., `Thu 11`, every tick a date value), a grid with 11 rules, the title `d`, the first label left
aligned and the last right aligned, that parity thins the labels that do not fit (only the order and the text of the
labels are pinned, since which labels fit depends on the measured widths) while `labelOverlap: false` draws all eleven;
that eleven ticks 10 pixels apart on 100 pixels are thinned to between 2 and 10 labels and all 11 with the overlap off;
that a vertical time axis has centered, middle-based labels (no flush); and that `config.axisTemporal` with
`grid: false` removes the grid.

##### Time Unit Labels And Tick Count

**Tests**: `AxisBuilderTimeTests.AxisBuilder_Time_YearMonthLabelsUseTheUnitSpecifier`,
`AxisBuilderTimeTests.AxisBuilder_Time_TickCountDefaultMonthUnitUsesVegaDefault10`,
`AxisBuilderTimeTests.AxisBuilder_Time_TickMinStepFromTheUnit`

Assert the month bar axis derived above (the labels `Jan 2024` to `Apr 2024`, the instants January 1 to April 1, the
positions 0, 102, 198, 300, and the title `date (year-month)`); that a field whose unit is exactly `hours` takes
Vega's default of ten ticks (the labels `00:00`, `03:00`, ..., `21:00`, the unit specifier `%H:00`) while
`hoursminutes` has one tick per 40 pixels and gives 24 ticks; and that `tickMinStep: 1` replaces the minimum step of the
unit so that 8 ticks over 91 days target 11.4 days (nearest the 7 day week) and give the 13 Sundays January 7 to March
31, still labeled with the unit specifier.

##### Time Axis Format, Format Type, Ticks, And Values

**Tests**: `AxisBuilderTimeTests.AxisBuilder_Time_FormatStringAndObject`,
`AxisBuilderTimeTests.AxisBuilder_Time_FormatTypeNumberAndTime`,
`AxisBuilderTimeTests.AxisBuilder_Time_TickCountNumberObjectAndInterval`,
`AxisBuilderTimeTests.AxisBuilder_Time_ValuesAcceptDatetimeObjects`

Assert that a text `format` is a d3 time format (`%Y-%m-%d` labels every tick with its date) and an object replaces
the day and year levels of the multi-format while a missing level keeps the Vega default (`Y2024`, `02`, ..., `Jan 07`,
...); that `formatType: number` formats the milliseconds (`1,704,067,200,000` and `1,704,931,200,000`, ten days
later, which is 10 x 86,400,000 = 864,000,000 more) and that `formatType: time` on a quantitative axis of 0 to
10,000 reads the numbers as times in the zone of the chart (`00:00:00`, `00:00:01`, ..., `00:00:10` in UTC, and
`05:30:00`, ..., `05:30:10` at +05:30, whose wall time at the epoch is 05:30) while `utc` reads them in UTC in
every zone; that `tickCount` as a number, an interval, and `{interval, step}` gives the ticks derived above; and that
`values` take a timestamp, a date
text, and a date-time object (the 10th, 5th, and 3rd of January), drop a date outside the scale and a text that is not
a date, and keep the scale order (the 3rd, 5th, and 10th at 80, 160, and 360 pixels, labeled `Wed 03`, `Fri 05`,
`Wed 10`).

##### Time Axis Zones And Discrete Dates

**Tests**: `AxisBuilderTimeTests.AxisBuilder_Time_Zone_LabelsAndTicksUseLocalMidnights`,
`AxisBuilderTimeTests.AxisBuilder_Time_UtcScale_IgnoresZone`,
`AxisBuilderTimeTests.AxisBuilder_Time_NoRotationDefaultForTimeUnitOrdinal`,
`AxisBuilderTimeTests.AxisBuilder_Time_DiscreteDateLabels_UseUnitSpecifier`

Assert the ten local midnights of the +05:30 zone with their labels and the first at 31 while the same data on the UTC
calendar has eleven ticks from January 1; that a UTC scale has the same ticks and labels in a US-like zone as with
none; that an ordinal field with a month unit has three horizontal labels (angle 0, no 270 degree default) `Jan`,
`Feb`, `Mar`; and that the discrete dates are labeled by the unit specifier, by an explicit `%B` as `January`,
`February`, `March`, and that `values` that name a part of the unit (`"Mar"` and 1) keep `Jan` and `Mar`, every tick
being a date.

##### Time Axis Limits, Cancellation, Cultures, And Determinism

**Tests**: `AxisBuilderTimeTests.AxisBuilder_Time_TooManyTicks_IsLocated`,
`AxisBuilderTimeTests.AxisBuilder_Time_LabelTooLong_IsLocated`,
`AxisBuilderTimeTests.AxisBuilder_Time_LabelsTogetherTooLong_IsLocated`,
`AxisBuilderTimeTests.AxisBuilder_Time_HostileFormatOnManyTicks_IsStoppedByTheAggregateCap`,
`AxisBuilderTimeTests.AxisBuilder_Time_FormatSpecifierLimits_AreSpecificationErrors`,
`AxisBuilderTimeTests.AxisBuilder_Time_Cancellation`,
`AxisBuilderTimeTests.AxisBuilder_Time_Culture_Invariant`,
`AxisBuilderTimeTests.AxisBuilder_Time_Deterministic`

Assert that with `MaxTicks` of 5 a daily interval request, a millisecond interval request, and six listed values throw
a located `VegaLiteRenderException` that names the limit (`the limit of 5`, `MaxTicks`, and `MaxTicks (5)`); that with
`MaxStringLength` of 30 the format `%c %c` (`1/1/2024, 12:00:00 AM` twice with a space, 43 characters) fails for one
listed tick with a located exception naming the limit of 30 while `%c` (21 characters) fits; that the eleven `%c`
labels of the ten-day axis (9 x 21 + 2 x 22 = 233 characters, the 10th and 11th being one digit longer) fit a limit of
240 and are refused at the fifth label under 100 (5 x 21 = 105) with `together longer than the limit of 100 characters`;
that the hostile format of 512 `%B` (1,024 characters) on the 481 month starts between January 1980 and January 2020
(about 3,200 characters a label, 1.5 million in all) is refused with `the limit of 1048576 characters` under the default
limit; that a format of 1,025 characters, one of 400 `%c`, a member of a format object, and the format of a quantitative
field with `formatType: utc` are located `VegaLiteSpecException`s naming `the limit of 1024 characters` or `the limit
of 4096 pieces`; that a canceled token throws `OperationCanceledException` and the same request
builds with a live token; that the multi-format labels, `%A %B %I %p` (`Monday January 12 AM`), and the number labels
are identical under the Turkish and German cultures and the invariant culture; and that building the same axis twice
in a US-like zone gives the same instants, positions, labels, and item count.
