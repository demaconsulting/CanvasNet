### TimeLabels Unit Verification Design

This document describes the unit-level verification strategy for the `TimeLabels` unit in the
`DemaConsulting.CanvasNet.VegaLite.Guides` namespace: the internal static `TimeLabels` class (`Create`,
`IsKnownFormatType`, `IntervalRequest`, `UsesVegaDefaultCount`, and `ReadInstant`) with its `Options` record, which holds
what the axes and the legends of temporal fields share: the format that a label of a date gets (the `format` and
`formatType` of the guide, the unit specifier, the default of the guide, and Vega's multi-format), the reading of a
`tickCount` that names a calendar interval, Vega-Lite's rule for the default tick count of a time axis, and the reading
of an explicit list of dates. The unit is also verified through its two callers, the time axes of `AxisBuilder` and the
temporal legends of `LegendBuilder` (`LegendBuilder.Time.cs`), whose time tests are named below.

#### Verification Approach

The unit is verified in two parts. The static functions are called directly with the real `TimeFormat`, `TimeUnitSpec`,
`TimeCalendar`, and `JsDateParser` and with specification nodes that `TimeTestSupport.Node` parses with the real
`SpecParser`; the label function that `Create` returns is called with instants. The axes and the legends are verified by
parsing a small, original specification with the real `SpecParser`, resolving the configuration, data, scales, and ticks
with the real `ConfigResolver`, `DataLoader`, `ScaleResolver`, and `Ticks` (through the shared helper
`ChartTestHelpers`, with the zone of the chart when a test needs one), building the guide with the real `AxisBuilder` or
`LegendBuilder` and the real `TextMeasurer`, and asserting on the tick values, positions, and labels and on the scene
items. No dependency is mocked or stubbed, and the internal types are reached through the test assembly's
`InternalsVisibleTo` access. Every zone is built in the test (`TimeTestSupport.Fixed` and `UsLike`) and no test reads
the host's zone, clock, or culture. The expected values are derived by hand (the day numbers are those of the calendar
verification: the epoch is day 0 and 2024-01-01 is day 19,723 = 54 x 365 + 13 leap days, a Monday; a day is 86,400,000
ms), and the derivations are repeated here:

- **Anchors.** 2024-03-05T00:00Z is day 19,787 (31 + 29 + 4 days after January 1), which is 19,787 x 86,400,000 =
  **1,709,596,800,000**, a Tuesday. 2024-01-01T00:00Z is 19,723 x 86,400,000 = **1,704,067,200,000**, and January 11
  is ten days (864,000,000 ms) later, 1,704,931,200,000.
- **Label format resolution** (plan decision P8-D7; the first rule that applies wins). For the instant March 5:
  a `format` text is a d3 time format (`%Y/%m/%d` gives `2024/03/05`); a `format` object gives the levels of Vega's
  multi-format (`{date: "%d."}` gives `05.`, because March 5 is a day that is not the start of a week); with no format a
  field with a time unit uses the specifier of the unit (`yearmonth` gives `%b %Y`, so `Mar 2024`); otherwise the
  default of the guide (the legend's `config.timeFormat`, `%b %d, %Y`, gives `Mar 05, 2024`); and with nothing at all
  Vega's multi-format, which gives `Tue 05` for March 5; an instant that is NaN is `Invalid Date`. The default
  multi-format is the label of the coarsest boundary the instant sits on (in UTC): 2024-03-05T00:00 is a day, `Tue 05`;
  2024-03-03T00:00 is a Sunday that is not the first of a month, `Mar 03`; 2024-03-01 is `March`; 2024-04-01 is
  `April`; 2024-01-01 is `2024`; 2024-03-05T13:00 is `01 PM`; 13:45 is `01:45`; 13:45:30 is `:30`; and 13:45:30.250 is
  `.250` (the formatter itself is verified in the _TimeFormat Unit Verification Design_).
- **Format types.** `formatType: "number"` formats the milliseconds as a number: `,d` of 1,709,596,800,000 is
  `1,709,596,800,000` (thirteen digits in groups of three) and no format at all gives the JavaScript text
  `1709596800000`; the time format `%H:%M` at +05:30 gives `05:30` for March 5 at 00:00Z (the local time) and `00:00`
  with `formatType: "utc"`, which ignores the zone of the scale; an invalid number format (`%Q`) is a located
  specification error that names the axis format. The format types that are evaluated are `number`, `time`, and `utc`;
  `custom`, the empty text, and null are not.
- **Label length.** `Mar 05, 2024` has 12 characters and the number `1709596800000` has 13, so a limit of 12 returns
  the time label and a limit of 11 is a located render exception that contains `the limit of 11`, and a limit of 12
  fails the number with `the limit of 12`.
- **Labels together.** The labels of one guide are limited together to the same length: under a limit of 30 two `Mar 05,
  2024` labels (24 characters) are made and the third (36) is a located render exception that contains `are together
  longer than the limit of 30 characters` (as is every later one, since a refused label is not counted); under a limit of
  36 three are made (exactly 36) and the fourth is refused; a second label function keeps its own total; the 13-character
  number labels are 26 under a limit of 30 and the third (39) is refused. On an axis, the eleven `%c` labels of the ten-day
  axis are 9 x 21 + 2 x 22 = 233 characters (the 10th and 11th are `1/10/2024, 12:00:00 AM` and `1/11/2024, 12:00:00 AM`),
  so a limit of 240 allows them and a limit of 100 refuses the fifth (5 x 21 = 105); on a legend the six `Jan 01, 2024`
  labels are 72 characters, so a limit of 72 allows them and a limit of 40 refuses the fourth (48). With the default limit
  of 1,048,576, a format of 512 `%B` (1,024 characters, the longest allowed) on the 481 month starts of January 1980 to
  January 2020 makes labels of about 6.2 x 512 = 3,200 characters, which pass the limit at about the 330th tick, so the
  axis fails with `the limit of 1048576 characters`.
- **Specifier limits.** A time format of 1,025 characters, or 400 `%c` (5,200 tokens, over 4,096), is a located
  specification exception at the format: from `TimeLabels.Create` ("The axis time format is not valid: ...") and from
  the specification parser for a format of an axis or a legend that is read as a time (a temporal field, a time unit, or
  `formatType` of `time` or `utc`: the text, or a member of a format object; see the _SpecParser Unit Verification
  Design_); the `config.timeFormat` of a legend is found by `TimeLabels.Create`. A specifier of exactly 1,024 characters
  works.
- **Interval requests.** `tickCount: "month"` is the month interval (step 0, since none is given),
  `{interval: "week", step: 2}` is the week interval with step 2, `"quarter"` is the month interval with step 3,
  `"utcday"` is a UTC request; null, a number, an unknown name such as `decade`, and an object with no interval are not
  interval requests.
- **The default tick count rule.** Vega-Lite leaves the default count to Vega (10) when the time unit of the field is
  exactly one part and that part is a month, hours, a day, or a quarter (`month`, `hours`, `day`, `quarter`, and
  `utcmonth` are, with a step as well), and otherwise uses one tick for every 40 pixels, `ceil(extent / 40)`
  (`yearmonth`, `date`, `year`, `minutes`, `hoursminutes`, and no unit are not).
- **Tick count of a month-unit axis.** Three months of 2024 as `yearmonth` over 300 pixels: the default count is
  ceil(300 / 40) = 8; the minimum tick step is the length of one month, 31 days (measured from January 2001); the
  domain is January 1 to April 1, 31 + 29 + 31 = 91 days, so the count is limited to min(8, floor(91 / 31) + 1) =
  min(8, 3) = 3; the target is 91 / 3 = 30.33 days, which is nearest to the month of 30 days of the d3 interval table
  (30.33 / 30 = 1.01 is below 90 / 30.33 = 2.97), so the ticks are the first of January, February, March, and April
  (the stop is included) with the labels `Jan 2024`, `Feb 2024`, `Mar 2024`, and `Apr 2024`; the pixel positions are
  31 / 91 x 300 = 102.198 (rounded 102), 60 / 91 x 300 = 197.802 (198), and 300, and the title is `date (year-month)`.
  With `tickMinStep: 1` the count stays 8 and the target is 91 / 8 = 11.375 days, nearer to the week of 7 days
  (11.375 / 7 = 1.63 is below 30 / 11.375 = 2.64): the 13 Sundays January 7, 14, 21, 28, February 4, 11, 18, 25, March
  3, 10, 17, 24, and 31, each labeled with the unit specifier.
- **d3 tick anchors on a ten-day axis.** January 1 to January 11, 2024 over 400 pixels: the count is ceil(400 / 40) =
  10, the target is 10 days / 10 = 1 day, which is in the table, so the ticks are the 11 midnights, the stop included,
  40 pixels apart; the label of each is the coarsest boundary it sits on: January 1 starts a year (`2024`), January 7 is
  a Sunday that is not the first of a month (`Jan 07`), and the rest show the weekday and the date (`Tue 02`, `Wed 03`,
  `Thu 04`, `Fri 05`, `Sat 06`, `Mon 08`, `Tue 09`, `Wed 10`, `Thu 11`). A count of 5 targets 2 days, which is in the
  table, so the ticks are the dates 1, 3, 5, 7, 9, and 11 at 0, 80, 160, 240, 320, and 400 pixels; `tickCount: "week"`
  gives only Sunday January 7 (6 x 40 = 240 pixels, `Jan 07`); `{interval: "day", step: 5}` gives the dates 1, 6, and
  11 (`2024`, `Sat 06`, `Thu 11`); `"month"` gives only January 1.
- **Hours.** The instants 2024-05-05T00:00Z and 23:00Z with the unit `hours` over 800 pixels use Vega's count of 10:
  the target is 23 h / 10 = 2.3 h, between the 1 hour and the 3 hours of the table, and 2.3 / 1 = 2.3 is not below
  3 / 2.3 = 1.30, so the step is 3 hours and the ticks are 00:00, 03:00, ..., 21:00 (eight, with the unit specifier
  `%H:00`); with the unit `hoursminutes` the count is ceil(800 / 40) = 20, the target is 1.15 h (1.15 / 1 is below
  3 / 1.15 = 2.61), so the step is one hour and there are 24 ticks.
- **Zone.** January 1 and 11 at 00:00Z at +05:30 make the domain run from local 05:30 on the 1st to local 05:30 on the
  11th, so the ticks are the ten local midnights of January 2 to 11, each at 18:30Z on the day before; the first is
  18.5 h of 240 h after the start, 18.5 / 240 x 400 = 30.83 pixels (rounded 31). A UTC scale ignores the zone.
- **Explicit values.** A timestamp (1,704,844,800,000 is January 10: 1,704,067,200,000 + 9 x 86,400,000), the text
  `2024-01-05`, and the object `{year: 2024, month: 1, date: 3}` are read as the instants of January 10, 5, and 3
  (a fraction of a millisecond is truncated); a date after the domain (`2025-01-01`), a boolean, a null, a text that is
  not a date, a number beyond the years 1 to 9999, and an array are not instants. An object is read in the calendar of
  the scale (at +05:30 local midnight of March 5 is March 5 less 5.5 hours) unless it has `utc: true`, and a date-only
  text is UTC in every calendar.
- **Quantitative axis with a time format.** The numbers 0 to 10,000 with `formatType: "time"` and `%H:%M:%S` are
  milliseconds after the epoch: a tick every 1,000 (count 10 over 400 pixels) gives `00:00:00` to `00:00:10` in UTC, and
  `05:30:00` to `05:30:10` in a chart at +05:30 (the chart's calendar; the epoch is 05:30 there); `formatType: "utc"`
  gives the UTC labels in every zone.
- **Legend ticks.** The ten days of a temporal color in a gradient legend 200 pixels long: the tick count is
  max(2, 2 x floor(200 / 100)) = 4, the target is 10 / 4 = 2.5 days, nearest to the 2 days of the table (2.5 / 2 =
  1.25 is below 7 / 2.5 = 2.8), so the labels are the dates 1, 3, 5, 7, 9, and 11 as `Jan 01, 2024` and so on; the
  color ramp is sampled at the time ticks of a count of 15, whose target is 240 h / 15 = 16 h, nearest to the 12 hours
  of the table (16 / 12 = 1.33 is below 24 / 16 = 1.5), so there are 240 / 12 + 1 = 21 stops at the offsets 0, 0.05,
  ..., 1; a size on a time scale has 5 ticks by default, so a target of 2 days and the same six dates; the three
  `yearmonth` months of January 15, February 10, and March 20 (starts January 1, February 1, and March 1) over a count
  of 4 target 15 days and give the month ticks `Jan 2024`, `Feb 2024`, and `Mar 2024`; at +05:30 the first odd day is
  the 3rd (local midnight of January 1 is before the domain starts), so the labels are the dates 3, 5, 7, 9, and 11.
  Labels that are 12 characters long fit a limit of 12 for one label (one explicit value) and not 10, and a count of an
  hourly interval over ten days (241 ticks) is above a tick limit of 5.
- **Discrete dates.** The months of three dates as an ordinal field with the unit `month` are labeled `Jan`, `Feb`, and
  `Mar` (the specifier `%b`), `January`, `February`, and `March` with the format `%B`, and `Jan` and `Mar` for the values
  `["Mar", 1]` (a name or a number that names a part of the unit); the labels are not turned.
- **Limits.** More ticks than the limit are refused before any label is made: with a tick limit of 5 the 11 days, the
  864,000,000 milliseconds of an interval request of `millisecond` over ten days, and a list of 6 values are located
  render exceptions; a label longer than the string limit is refused: `%c` is `1/1/2024, 12:00:00 AM` (21 characters)
  and `%c %c` is 43, so for a single tick (an explicit value) a limit of 30 allows the first and refuses the second.
- **Culture.** The labels are the English ones under `tr-TR` and `de-DE` as under the invariant culture: `Monday January
  12 AM` for the format `%A %B %I %p` on January 1, 2024 at 00:00, and `1,704,067,200,000` for the number format.

Unit tests reside in `TimeLabelsTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Guides`
folder, with the time tests of the two callers in `AxisBuilderTimeTests.cs` and `LegendBuilderTimeTests.cs` of the same
folder (see also the _AxisBuilder_ and _LegendBuilder Unit Verification Designs_) and the zones of
`Data/TimeTestSupport.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The Liberation fonts bundled with the core package, so every measured label width is independent of
  installed fonts
- **Files**: None; every specification is an in-memory string
- **Isolation**: Each test parses its own specification and builds its own guide. Every zone is a custom zone created
  by the test; the tests never read `TimeZoneInfo.Local`, the clock, or the host's zone database. The culture scenarios
  run the same guide under `tr-TR` and `de-DE` and compare the labels with those of the invariant culture, and the
  cancellation scenarios pass a token that is already canceled

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any label, tick value, or position that differs from the hand-derived expectation, any format chosen out of
the order of Vega-Lite, any interval request or default count that differs from the rule, any more ticks or characters
than the limit that do not fail with the limit and a location, a canceled token that does not stop the build, or a
result that depends on the host's zone or the current culture constitutes a failure.

#### Unit-Level Test Scenarios

##### The Label Format Is Chosen In The Order Of Vega-Lite

**Tests**: `TimeLabelsTests.TimeLabels_Create_ChoosesTheFormatInTheOrderOfVegaLite`,
`TimeLabelsTests.TimeLabels_Create_FormatTypesNumberAndUtc`, `TimeLabelsTests.TimeLabels_IsKnownFormatType_NumberTimeAndUtc`

Asserts the five labels of March 5 for the format text, the format object, the unit specifier, the default of the guide,
and the multi-format (`2024/03/05`, `05.`, `Mar 2024`, `Mar 05, 2024`, and `Tue 05`) and `Invalid Date` for NaN; the
number format (`1,709,596,800,000` and `1709596800000`), the local and the UTC time (`05:30` and `00:00`) and the
specification error of an invalid number format; and that `number`, `time`, and `utc` are evaluated format types and
`custom`, the empty text, and null are not.

##### Interval Requests And The Default Tick Count

**Tests**: `TimeLabelsTests.TimeLabels_IntervalRequest_ReadsNamesAndObjects`,
`TimeLabelsTests.TimeLabels_UsesVegaDefaultCount_OnlyForSingleMonthHoursDayQuarter`

Asserts the four interval requests (the month interval with step 0, the week with step 2, the quarter as the month with
step 3, and a UTC day) and that null, a number, an unknown name, and an object with no interval are not requests; and the
eleven units of the default count rule (true for `month`, `hours`, `day`, `quarter`, and `utcmonth` and false for
`yearmonth`, `date`, `year`, `minutes`, `hoursminutes`, and no unit).

##### Explicit Dates Are Read As Instants

**Tests**: `TimeLabelsTests.TimeLabels_ReadInstant_NumbersTextsObjectsAndNonDates`

Asserts that a timestamp (truncated), a date text, and a date-time object are read as March 5 (1,709,596,800,000); that
the object is read in the calendar of the scale unless it has the `utc` flag (at +05:30 it is 5.5 hours earlier) and a
date-only text is UTC; and that a boolean, a null, a text that is not a date, a number beyond the range, and an array are
NaN.

##### Labels Are Bounded By The Limit

**Tests**: `TimeLabelsTests.TimeLabels_Create_LabelsAreBoundedByTheLimit`,
`TimeLabelsTests.TimeLabels_Create_LabelsOfAGuideAreBoundedTogether`,
`TimeLabelsTests.TimeLabels_Create_LongSpecifier_IsASpecificationError`

Asserts that `Mar 05, 2024` is made under a limit of 12 and is a located render exception that contains `the limit of
11` under a limit of 11, and that the 13-digit number label under a limit of 12 is one that contains `the limit of 12`;
that the labels of one function are limited together (two of three 12-character labels under 30, three under 36, the
refused label uncounted, an independent function with its own total, and 13-character number labels refused at the
third); and that a specifier of 1,025 characters, one of 400 `%c`, and a long member of a format object are located
specification exceptions ("The axis time format is not valid") while a specifier of 1,024 characters is formatted.

##### Time Axes Choose Ticks And Labels By The Calendar

**Tests**: `AxisBuilderTimeTests.AxisBuilder_Time_DefaultLabelsAreMultiFormat`,
`AxisBuilderTimeTests.AxisBuilder_Time_YearMonthLabelsUseTheUnitSpecifier`,
`AxisBuilderTimeTests.AxisBuilder_Time_FormatStringAndObject`,
`AxisBuilderTimeTests.AxisBuilder_Time_FormatTypeNumberAndTime`,
`AxisBuilderTimeTests.AxisBuilder_Time_TickCountNumberObjectAndInterval`,
`AxisBuilderTimeTests.AxisBuilder_Time_TickCountDefaultMonthUnitUsesVegaDefault10`,
`AxisBuilderTimeTests.AxisBuilder_Time_TickMinStepFromTheUnit`,
`AxisBuilderTimeTests.AxisBuilder_Time_ValuesAcceptDatetimeObjects`

Asserts the 11 daily ticks, labels, and positions of the ten-day axis (with the grid of 11 rules, the flush end labels,
the title, and the labels thinned by parity unless the overlap method is off); the month ticks, labels, positions
(0, 102, 198, and 300), and title of the `yearmonth` axis derived above; the text format and the multi-format object
(the labels `2024-01-01` to `2024-01-11` and `Y2024`, `02`, ..., `Jan 07`, ..., `11`); the number format of the
milliseconds and the time (in the zone of the chart: `05:30:00` to `05:30:10` at +05:30) and UTC formats of a quantitative axis; the count, interval, and step forms of `tickCount`; the
default of 10 for the unit `hours` (eight ticks) against 20 for `hoursminutes` (24 ticks); the 13 Sundays under
`tickMinStep: 1`; and the values given as a timestamp, a text, and an object, in the order of the scale at 80, 160, and
360 pixels, with the dropped date and text.

##### Time Axes Follow Their Defaults, Zones, And Discrete Dates

**Tests**: `AxisBuilderTimeTests.AxisBuilder_Time_LabelFlushAndOverlapDefaults`,
`AxisBuilderTimeTests.AxisBuilder_Time_NoRotationDefaultForTimeUnitOrdinal`,
`AxisBuilderTimeTests.AxisBuilder_Time_Zone_LabelsAndTicksUseLocalMidnights`,
`AxisBuilderTimeTests.AxisBuilder_Time_UtcScale_IgnoresZone`,
`AxisBuilderTimeTests.AxisBuilder_Time_DiscreteDateLabels_UseUnitSpecifier`

Asserts that dense labels are thinned by parity and all drawn without the overlap method, that a vertical time axis has
no flush labels, and that the `axisTemporal` configuration applies (no grid); that the labels of an ordinal field with a
time unit are `Jan`, `Feb`, and `Mar` and not turned; the ten local midnights at +05:30 with the first at 31 pixels
and the 11 UTC midnights of the same data; that a UTC scale in a zone has the plain axis; and the discrete labels
`Jan`, `February`, and `Jan` and `Mar` for the values.

##### Time Axes Fail Closed And Are Deterministic

**Tests**: `AxisBuilderTimeTests.AxisBuilder_Time_TooManyTicks_IsLocated`,
`AxisBuilderTimeTests.AxisBuilder_Time_LabelTooLong_IsLocated`,
`AxisBuilderTimeTests.AxisBuilder_Time_LabelsTogetherTooLong_IsLocated`,
`AxisBuilderTimeTests.AxisBuilder_Time_HostileFormatOnManyTicks_IsStoppedByTheAggregateCap`,
`AxisBuilderTimeTests.AxisBuilder_Time_FormatSpecifierLimits_AreSpecificationErrors`,
`AxisBuilderTimeTests.AxisBuilder_Time_Cancellation`,
`AxisBuilderTimeTests.AxisBuilder_Time_Culture_Invariant`, `AxisBuilderTimeTests.AxisBuilder_Time_Deterministic`

Asserts the located render exceptions of the tick limit (generated ticks, an interval of milliseconds, and six values)
and of the label limit (43 characters against 30, and the 21-character `%c` that fits), of the labels together (233
characters against 100, and the hostile 512-month-name format on 481 ticks against the default limit), and the located
specification exceptions of a format of 1,025 characters, of 400 `%c`, of a format object member, and of a quantitative
field forced to time by its format type; that a canceled token stops the
build and an uncanceled one builds the axis; that the labels are the English ones under `tr-TR`, `de-DE`, and the
invariant culture; and that two builds in a zone give the same ticks, labels, positions, and item count.

##### Temporal Legends Are Labeled With Dates

**Tests**: `LegendBuilderTimeTests.LegendBuilder_Time_TemporalGradient_LabelsUseConfigTimeFormat`,
`LegendBuilderTimeTests.LegendBuilder_Time_LegendFormatOverridesAndUnitSpecifier`,
`LegendBuilderTimeTests.LegendBuilder_Time_OrdinalTimeUnitSymbols_LabelWithTheUnitSpecifier`,
`LegendBuilderTimeTests.LegendBuilder_Time_SizeOnTimeScale_TicksAndLabels`,
`LegendBuilderTimeTests.LegendBuilder_Time_ExplicitValuesAcceptDatetimeObjects`

Asserts the six `%b %d, %Y` labels of the gradient legend and its 21 color stops at 0, 0.05, ..., 1 with the title of
the field; the `legend.format` text (`01/01` and so on) and object, the `config.timeFormat` (`2024.01.01` and so on),
and the unit specifier (`Jan 2024`, `Feb 2024`, and `Mar 2024`); one symbol and label per month for an ordinal field with
a time unit, with `%B` and with the value `Feb`; the six symbols of a size over dates; and the values given as an
object, a text, and a timestamp in the order written (`Jan 03, 2024`, `Jan 09, 2024`, and `Jan 01, 2024`).

##### Temporal Legends Follow The Zone, Fail Closed, And Are Deterministic

**Tests**: `LegendBuilderTimeTests.LegendBuilder_Time_Zone_LabelsUseLocalTime`,
`LegendBuilderTimeTests.LegendBuilder_Time_LimitsAndCancellation`,
`LegendBuilderTimeTests.LegendBuilder_Time_Culture_InvariantAndDeterministic`

Asserts the five local labels at +05:30 against the six UTC ones; that a label limit of 10 and a tick limit of 5 are
located render exceptions that name their limits while a limit of 12 gives the one label of a single explicit value, and
that a canceled token stops the build; and that the labels are the same under `tr-TR`, `de-DE`, and the invariant
culture and on repeated calls.

##### Temporal Legends Keep The UTC Type, Ignore Zero, And Limit Their Labels Together

**Tests**: `LegendBuilderTimeTests.LegendBuilder_Time_TemporalColorUtcType_UsesTheUtcCalendar`,
`LegendBuilderTimeTests.LegendBuilder_Time_TemporalColor_ZeroIsIgnored`,
`LegendBuilderTimeTests.LegendBuilder_Time_LabelsTogetherTooLong_IsLocated`,
`LegendBuilderTimeTests.LegendBuilder_Time_FormatSpecifierLimits_AreSpecificationErrors`

Asserts that a temporal color with `scale.type: "utc"` in a chart five hours behind UTC labels the whole-day ticks counted
from the epoch (2024-01-01 is day 19,723, so the even days 19,724 to 19,732: `Jan 02, 2024`, `Jan 04, 2024`, ...,
`Jan 10, 2024`), the same as without a zone, and that the `time` type in the zone gives other labels (the local odd
days); that `scale.zero: true` leaves the six labels `Jan 01, 2024` to `Jan 11, 2024` (the domain is not pulled to 1970);
that the six 12-character labels are 72 characters, refused at the fourth under a limit of 40 (`are together longer than
the limit of 40 characters`) and allowed under 72; and that a `config.timeFormat` and a legend `format` member of 1,025
characters are located specification exceptions that contain `the limit of 1024 characters`.
