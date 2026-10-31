### TimeCalendar Unit Verification Design

This document describes the unit-level verification strategy for the `TimeCalendar` unit in the
`DemaConsulting.CanvasNet.VegaLite.Data` namespace: the internal `TimeCalendar` class (`ToParts`, `FromParts`,
`UtcOffsetMilliseconds`, `IsValid`, `Create`, and `Interval`, with the zone half in `TimeCalendar.Zone.cs`) and the
`DateParts` record struct; `TimeIntervalKind`, `TimeInterval` (the d3-time `Floor`, `Ceil`, `Round`, `Offset`, `Count`,
`Every`, `EveryEpochDay`, and `Range`, and `TryParseName`), and `TimeRangeLimitException`; `TimeUnitSpec` (the
Vega-Lite time units: names, parts, field names, label specifiers, `Floor`, `End`, the `maxbins` resolution, and the
duration of a step); and `DateTimeObject` (the Vega-Lite `DateTime` object and the rule for a bare number or text
compared with a field that has a time unit). The unit is the one place that knows what a date is: it reads no host
zone, no clock, and no culture, so its results are the same on every machine.

#### Verification Approach

The unit is verified by calling its entry points directly, with the real `SpecParser` only to build the specification
nodes that `TimeUnitSpec` and `DateTimeObject` read (`TimeTestSupport.Node`). No dependency is mocked or stubbed; the
unit has none other than the BCL, and the internal types are reached through the test assembly's `InternalsVisibleTo`
access. Every zone is built in the test with `TimeZoneInfo.CreateCustomTimeZone` by the helper `TimeTestSupport.cs`
(`UsLike`, `Fixed`, and `SouthernLike`), never with `TimeZoneInfo.Local` or a zone looked up by name, so no test reads
the host's zone database; the expected instants are the hand-derived constants below, or are computed with
`DateTimeOffset` (`TimeTestSupport.Ms`), independently of the code under test. The expected values are derived by hand
from the Gregorian calendar and from the d3-time, Vega, and Vega-Lite rules, with the derivation in the test comments
and repeated here:

- **Day numbers.** The Unix epoch (1970-01-01) is day 0, a Thursday. The day 19,723 is 2024-01-01, because 54 years
  from 1970 have 54 x 365 = 19,710 days and 13 leap days (1972, 1976, ..., 2020). A Gregorian day number maps to a
  weekday by its remainder modulo 7 counted from Thursday. A day is 86,400,000 ms.
- **Anchor instant.** 2024-02-29T12:34:56.789Z: February 29 is 31 + 28 = 59 days after January 1, so it is day 19,782
  and 19,782 x 86,400,000 = 1,709,164,800,000; the time 12:34:56.789 is 43,200,000 + 2,040,000 + 56,000 + 789 =
  45,296,789 ms; the sum is **1,709,210,096,789**. The day number 19,782 is divisible by 7, so it is a Thursday, and
  the day of the year is 31 + 29 = 60. The same instant seen through `FromParts(2024, 1, 29, 12, 34, 56, 789)` (the
  month is zero-based) gives it back.
- **Neighbors of the epoch.** Instant 0 is Thursday 1970-01-01 (day of year 1); -1 ms is 1969-12-31T23:59:59.999, a
  Wednesday, the 365th day of 1969 (floor division keeps the time of day positive). A fraction of a millisecond
  truncates toward zero: 1.9 gives 1, -1.5 gives -1, and 0.5 gives 0.
- **Calendar dates used throughout.** 2024-03-01 is day 19,723 + 31 + 29 = 19,783, which is **1,709,251,200,000**;
  2024-04-01 is 31 days later (day 19,814), **1,711,929,600,000**; 2024-03-05 is day 19,787, **1,709,596,800,000**, a
  Tuesday (19,787 mod 7 = 5 days after Thursday) and the 65th day of the year; 2012-01-01 is day 42 x 365 + 10 leap
  days (1972 to 2008) = 15,340, a Sunday, **1,325,376,000,000**; 2012-01-03 (a Tuesday) is day 15,342,
  **1,325,548,800,000**; 2012-03-01 is day 15,340 + 31 + 29 = 15,400, **1,330,560,000,000**; and 2012-02-29 is day
  15,399, **1,330,473,600,000** (the value of the culture scenario).
- **Roll-over (ECMAScript `MakeDay` and `MakeTime`).** Month 12 of 2024 is January 2025; date 0 of January 2024 is
  December 31, 2023; date 32 of January is February 1; month -1 is December of the year before; February 30 of the leap
  year 2024 is March 1 (February has 29 days), and February 29 of 2023 is March 1; hour 24 is the next midnight; 59
  minutes 60 seconds 1,000 ms is 01:00:01; fractional months and dates (5.9 and 1.9) truncate to 5 and 1.
- **Range.** The calendar is valid from 0001-01-01T00:00:00.000Z to 9999-12-31T23:59:59.999Z, the range of
  `DataValue.FromDate`; years 0 and 10000, any non-finite part, and a millisecond beyond either end give NaN. The two
  ends are the constants -62,135,596,800,000 (719,162 days before the epoch) and 253,402,300,799,999 (2,932,897 days
  after the epoch, less one millisecond).
- **United States like zone** (standard offset -5 h, daylight +1 h from the second Sunday of March at 02:00 standard
  time to the first Sunday of November at 02:00 daylight time). 2024-03-01 is a Friday (the anchor 2024-03-05 is a
  Tuesday), so the second Sunday is March 10, and 02:00 at -5 h is **2024-03-10T07:00Z**; November 1 is the 306th day of
  the year, day 20,028, 20,028 mod 7 = 1, a Friday, so the first Sunday is November 3, and 02:00 at -4 h is
  **2024-11-03T06:00Z**. The offset at 06:59:59.999Z on March 10 is -5 h and at 07:00Z it is -4 h, the local clock
  jumping from 01:59:59.999 to 03:00; at 05:59:59.999Z on November 3 it is -4 h and at 06:00Z it is -5 h.
- **Gap and overlap (plan decision P8-D3, the ECMAScript rule).** The wall time 02:30 on 2024-03-10 does not exist; it is
  read with the offset from before the change (-5 h) and is **07:30Z** (1,710,028,800,000 + 27,000,000 =
  1,710,055,800,000), which shows as 03:30. The wall time 01:30 on 2024-11-03 occurs twice (at -4 h and at -5 h); the
  earlier instant, at -4 h, is **05:30Z**. Away from the ambiguity the wall time minus the offset in force gives the
  instant: 00:59 on November 3 is 04:59Z, 02:00 is 07:00Z, 01:59 on March 10 is 06:59Z, 03:00 is 07:00Z, 12:00 on June
  15 is 16:00Z (-4 h), and 12:00 on January 15 is 17:00Z (-5 h).
- **Days that are not 24 hours.** Local midnight of March 10 is 00:00 at -5 h = 05:00Z and the next local midnight is
  00:00 at -4 h = 04:00Z the next day, so the day of 2024-03-10 lasts 23 h; local midnight of November 3 is 00:00 at
  -4 h = 04:00Z and the next is 00:00 at -5 h = 05:00Z, so that day lasts 25 h. Noon March 9 and noon March 12 local
  (17:00Z and 16:00Z) are 71 hours of elapsed time apart but 3 calendar days (`Count` of the day interval).
- **Southern zone** (standard +10 h, daylight +1 h from the first Sunday of October at 02:00 standard time to the first
  Sunday of April at 03:00 daylight time, a rule that spans the new year): October 1, 2024 is day 19,997 (a Tuesday), so
  the first Sunday is October 6 and 02:00 at +10 h is **2024-10-05T16:00Z**; April 1 is day 19,814 (a Monday), so the
  first Sunday is April 7 and 03:00 at +11 h is **2024-04-06T16:00Z**. The offset is +11 h in January and December and
  +10 h in July. The wall time 02:30 on October 6 is skipped and is read at +10 h (**2024-10-05T16:30Z**); 02:30 on April
  7 repeats and the earlier instant, at +11 h, is **2024-04-06T15:30Z**.
- **Fixed offsets.** 2024-06-01T00:00Z shows as 05:30 at +05:30, as 21:00 (on May 31) at -03:00, and as 20:30 at -03:30,
  and the parts build the same instant back.
- **Intervals** (the d3-time definitions): the day of Wednesday 2024-03-06 floors to its midnight and the week (starting
  on Sunday) floors to Sunday March 3; March 2 at 23:59:59.999 floors to Sunday February 25; the next Sunday after
  Wednesday is March 10; the Sundays between March 3 and March 17 at 05:00 are 2 (March 10 and 17) and January 4, 1970 is
  the first Sunday after the epoch (count 1); the month offset keeps the date and rolls over (January 31 plus one month
  is March 2 in 2024), rolls the year (December 15 plus one is January 15), and counts month boundaries (from
  2023-11-20 to 2024-03-02 there are 4: December 1, January 1, February 1, and March 1); round picks the nearer
  boundary (March 10 to March 1, March 25 to April 1); the hour of 10:20Z at +05:30 (local 15:50) floors to local 15:00,
  which is 09:30Z, and ceils to 10:30Z; the year floors 2024-05-05 to 2024-01-01 and its `every(10)` floors to 2020 and
  ceils to 2030; the millisecond `every(100)` floors the anchor 1,709,210,096,789 to ...700, ceils it to ...800, offsets
  ...700 by two steps to ...900, counts 10 steps from 0 to 1,000, and floors -1 to -100.
- **Filtered intervals** (d3 `every(k)` keeps the boundaries whose field is a multiple of k): `hour.every(3)` keeps the
  hours 0, 3, 6, ...; `minute.every(15)` and `second.every(15)` keep the quarters; `day.every(2)` keeps the dates 1,
  3, 5,
  ... of each month (the field is the date minus one), so it jumps from January 31 to February 1; `month.every(3)` keeps
  January, April, July, and October; `week.every(2)` keeps every other Sunday counted from the epoch week; the step is
  limited to 64 (a larger week step has no interval, an offset of more than 64 steps is NaN), and a step beyond the
  field is clamped to it (`hour.every(1000)` keeps only the hour 0). The epoch-day filter (d3 `unixDay.every`) keeps
  the even day numbers: 2024-01-01 is day 19,723 (odd), so the days kept in January are the 2nd, 4th, 6th, ... and the
  month-based `day.every(2)` keeps the 1st, 3rd, 5th, ...; day 19,725 (January 3) floors to January 2 and ceils to
  January 4; January 30 is day 19,752, February 1 is day 19,754; day 0 is even and day -1 (December 31, 1969) is odd,
  so it floors to December 30, 1969.
- **Range** is from the first boundary at or after the start up to but excluding the stop: the days from January 1 to
  January 4 are January 1, 2, and 3; from noon January 1 they are January 2 and 3; the months from January 15, 2024 to
  January 1, 2025 are February to December (11). A day range in the United States like zone from March 9 05:00Z to March
  12 04:00Z is the three local midnights 05:00Z, 05:00Z (March 10), and 04:00Z (March 11).
- **Time units.** The 11 single and 29 multiple names of Vega-Lite (40 in all, and each with its `utc` form, giving 80),
  each with the parts in the order year, quarter, month, week, day, day of year, date, hours, minutes, seconds,
  milliseconds. The floor reads the parts of the instant, replaces the missing larger parts by the reference values
  (the leap year **2012**, which starts on a Sunday, and January), and builds the instant: for 2024-03-17T10:20:45.678
  `year` is 2024-01-01, `month` is 2012-03-01, `date` is 2012-01-17, `hours` is 2012-01-01T10:00, `minutes` is
  2012-01-01T00:20, `monthdate` is 2012-03-17, `hoursminutes` is 2012-01-01T10:20, and `yearmonthdatehours` is
  2024-03-17T10:00. `seconds` and `milliseconds` keep :45 and .678 on 2012-01-01. `day` is the weekday of January
  2012, which is a Sunday: Tuesday 2024-03-05 gives 2012-01-03 (**1,325,548,800,000**), Sunday gives 2012-01-01, and
  Saturday gives 2012-01-07. The week is the Sunday-based week number (the Sundays since January 1): 2024-03-05 is
  week 9
  (Sundays January 7, 14, 21, 28, February 4, 11, 18, 25, and March 3), and in 2012 the week 9 starts on January 57 =
  February 26, so `week` is 2012-02-26, `weekday` is Tuesday of that week, February 28, and `yearweekday` is March 5 of
  2024 (week 9 of 2024 starts on March 3, January 63). The week numbers at the start of the year: 2012-01-01 (Sunday) is
  week 1, 2012-01-07 is week 1, 2012-01-08 is week 2, 2013-01-01 (Tuesday) is week 0, and 2013-01-06 (Sunday) is week
  1; the `yearweek` floor of 2013-01-02 is **2012-12-30**, because the Sunday of week 0 is `0 - (2 + 6) % 7 = -1`, that
  is the date -1 of January 2013.   A quarter is three months, so May 20, 2024 (quarter 2) floors to April 1 (of 2012 for
    `quarter`, of 2024 for `yearquarter`), and day 60 of the year (March 1, 2023, is 31 + 28 + 1) is February 29 in 2012
  (31 + 29 = 60) and March 1, 2023 for `yeardayofyear`. A step applies to the last part only and counts from its first
  value (from 1 for the date): `{unit: month, step: 3}` floors March to January and keeps April; `yearmonth` with step 3
  floors May 2024 to April 2024; `monthdate` with step 7 floors March 20 (date - 1 = 19, floored to 14) to March 15 and
  keeps March 22 (21); `yearmonthdatehours` with step 6 floors 13:04 to 12:00; `year` with step 10 floors 2024 to 2020.
- **End of a unit** is one step of the smallest part after the start: `yearmonth` from 1,709,251,200,000 ends at
  1,711,929,600,000 (31 days later); `month` from 1,330,560,000,000 (2012-03-01) ends at 1,333,238,400,000 (+ 31 days,
  2012-04-01); `day` from 1,325,548,800,000 ends one day later; a quarter ends three months later; a week one week
  later; `{month, step: 3}` from January ends in April; `{year, step: 10}` from 2020 ends in 2030. In the United States
  like zone the unit `yearmonthdate` of March 10 starts at 05:00Z and ends at 04:00Z (23 h). A `utc` unit ignores the
  zone: at 03:00Z on March 10 the local day is March 9 (local midnight 05:00Z) and the UTC day is March 10.
- **`maxbins`** (Vega's `timeBin`): an extent of 5 days with 5 bins targets 1 day, which is the day step, so the unit is
  year, month, date with step 1; an extent of 365 days with 10 bins targets 36.5 days, nearer to the month of 30 days
  (36.5 / 30 = 1.22) than to the quarter of 90 (90 / 36.5 = 2.47), so the unit is year, month; 100 years with 10 bins
  targets 10 years, so the unit is year with the step `tickStep(0, 100, 10)` = 10; 1,000 ms with 10 bins targets 100 ms,
  below a second, so the unit is milliseconds with `tickStep(0, 1000, 10)` = 100; an extent that is not finite gives
  one-year bins.
- **Duration of a unit step** (Vega-Lite `durationExpr`, measured from 2001-01-01): a month 31 days (2,678,400,000 ms),
  a year 365 days (31,536,000,000), a quarter 31 + 28 + 31 = 90 days (7,776,000,000), a week 604,800,000, a date or
  day of
  year 86,400,000, an hour 3,600,000, a minute 60,000, a second 1,000, a millisecond 1; a step multiplies it (two months
  are 59 days, 5,097,600,000; six hours 21,600,000); a unit whose smallest part is the day of the week has none.
- **Label specifiers.** The Vega-Lite specifier of a unit (Vega's `timeUnitSpecifier` with the Vega-Lite overrides)
  is in the table below; Vega's own defaults (`TryGetUnitSpecifier` without overrides) are `year` `%Y`, year-month
  `%Y-%m`, year-month-date `%Y-%m-%d`, month-date `%b %d`, hours-minutes `%H:%M`, hours-minutes-seconds `%H:%M:%S`,
  year-isoweek `%G W%V`, year-week `%Y W%U`, year-dayofyear `%Y %j`, quarter-month `Q%q %b`, day-hours `%a %H:00`, and
  milliseconds `.%L`; an override replaces a default and a combined key wins over its parts.

| Unit                        | Specifier          |
|-----------------------------|--------------------|
| `month`                     | `%b`               |
| `date`                      | `%d`               |
| `year`                      | `%Y`               |
| `quarter`                   | `Q%q`              |
| `day`                       | `%a`               |
| `week`                      | `W%U`              |
| `dayofyear`                 | `%j`               |
| `hours`                     | `%H:00`            |
| `minutes`                   | `00:%M`            |
| `seconds`                   | `:%S`              |
| `milliseconds`              | `.%L`              |
| `yearmonth`                 | `%b %Y`            |
| `yearmonthdate`             | `%b %d, %Y`        |
| `monthdate`                 | `%b %d`            |
| `yearquarter`               | `%Y Q%q`           |
| `yearquartermonth`          | `%Y Q%q %b`        |
| `quartermonth`              | `Q%q %b`           |
| `yearweek`                  | `%Y W%U`           |
| `yeardayofyear`             | `%Y %j`            |
| `weekday`                   | `W%U %a`           |
| `dayhours`                  | `%a %H:00`         |
| `hoursminutes`              | `%H:%M`            |
| `hoursminutesseconds`       | `%H:%M:%S`         |
| `yearmonthdatehours`        | `%b %d, %Y %H:00`  |
| `yearmonthdatehoursminutes` | `%b %d, %Y %H:%M`  |
| `minutesseconds`            | `00:%M:%S`         |
| `secondsmilliseconds`       | `:%S.%L`           |

- **Field names** follow Vega-Lite's `timeUnitToString`: `yearmonth` of `date` is `yearmonth_date`, `utcmonth` of `x`
  is `utcmonth_x`, `{unit: month, step: 2}` is `month_step_2_date`, `{maxbins: 10}` is `timeunit_maxbins_10_d`,
  `{unit: month, utc: true}` is `utcmonth_f`, a step of 2.5 is `yearmonth_step_2_5_f`, and a binned unit is
  `yearmonth_binned_true_f`.
- **Datetime objects** (Vega-Lite `DateTime`): the defaults are the year 2012, January, the first, and midnight; the
  month is a number from 1 or a name (full or three letters, any case); the quarter stands for its first month (quarter
  1 January, 3 July, 4 October) and the month wins over it; a quarter above 4 is flagged and rolls over (quarter 5 is
  month index 12, January 2013); the day of the week is a date in the first week of 2012, which starts on a Sunday
  (Sunday is 0 or 7, so January 1; Monday is January 2; Wednesday January 4; Saturday January 7), and a `day` that is
  combined with any other key, the `utc` flag included, is dropped and flagged; the parts that are too large roll over
  (date 32 of January is February 1, month 13 is January of the next year, hour 25 is 01:00 the next day, month 0 is
  December 2011); the years 0 to 99 are 1900 to 1999 (year 24 is 1924, year 0 is 1900, year 100 is the year 100), and
  year 10000 or a negative year is NaN; all keys together give the anchor 1,709,210,096,789. The `utc` flag evaluates in
  UTC and otherwise the parts are read in the zone of the chart: `{hours: 5}` is 10:00Z in the United States like zone
  in January (-5 h) and 05:00Z with `utc`. A bare number compared with a field that has a single local unit is that part
  when it is below 10,000 (5 against `hours` is 05:00, 3 against `month` is March, 2024 against `year` is 2024) and
  milliseconds otherwise; a text is a date when the date parser reads it and otherwise a month or day name or a number
  for the part (`Mar` against `month`, `Tuesday` against `day`).

Unit tests reside in `TimeCalendarTests.cs` (the UTC arithmetic), `TimeCalendarZoneTests.cs` (zones, gap, and overlap),
`TimeCalendarIntervalTests.cs` and `TimeCalendarEpochDayTests.cs` (the d3-time intervals), `TimeUnitSpecTests.cs` and
`TimeUnitSpecifierTests.cs` (the time units and their label specifiers), and `DateTimeObjectTests.cs`, with the shared
helper `TimeTestSupport.cs`, all within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Data` folder. The
tick sequences that the scales and the axes build from the intervals are verified with those units (see the _Ticks_ and
_TimeLabels Unit Verification Designs_), and the d3 tick anchors that depend on the intervals are derived there: for
example [January 1, January 11, 2024] with a count of 10 targets one day, so the day interval gives 11 ticks, the stop
included.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Files**: None; the specification nodes are parsed from in-memory strings
- **Isolation**: Pure functions of numbers and text. Every zone is a custom zone created by the test; the tests never
  read `TimeZoneInfo.Local`, the clock, or the host's zone database. The culture scenarios set the current culture to
  `tr-TR` (with its dotted capital I) and `de-DE` and compare the results with those of the invariant culture; the
  cancellation scenario passes a token that is already canceled

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any instant, part, boundary, or unit floor or end that differs from the hand-derived value, any non-finite or
out-of-range input that does not give NaN (or, for a call that is documented to fail, the documented exception), any
call that throws on an extreme input, any dependence on the host's zone or the current culture, or any list that exceeds
its cap constitutes a failure.

#### Unit-Level Test Scenarios

##### Anchor Instant And Parts In UTC

**Tests**: `TimeCalendarTests.TimeCalendar_ToParts_Utc_Anchor_20240229T123456789`,
`TimeCalendarTests.TimeCalendar_ToParts_Utc_Epoch_Is1970_01_01Thursday`,
`TimeCalendarTests.TimeCalendar_ToParts_Utc_OneMillisecondBeforeTheEpoch`,
`TimeCalendarTests.TimeCalendar_ToParts_Utc_TruncatesFractions`

Asserts that the anchor 1,709,210,096,789 breaks into the year 2024, the month index 1, the date 29, the weekday 4 (a
Thursday), the day of the year 60, and 12:34:56.789 with offset 0, and that the parts build the anchor back; that the
epoch is Thursday 1970-01-01; that -1 ms is 1969-12-31T23:59:59.999, a Wednesday, day 365; and that a fraction of a
millisecond truncates toward zero.

##### Parts Roll Over Like JavaScript

**Tests**: `TimeCalendarTests.TimeCalendar_FromParts_Utc_RollsMonthsAndDaysLikeJavaScript`

Asserts the ten roll-over vectors derived above (month 12, date 0, date 32, month -1, February 30, February 29 of a
common year, hour 24, 59 minutes 60 seconds 1,000 ms, hour -1, and fractional month and date) against instants computed
with the base class library.

##### The Valid Range Is Years 1 To 9999 And Nothing Throws

**Tests**: `TimeCalendarTests.TimeCalendar_FromParts_Utc_RejectsNonFiniteAndOutOfRange`,
`TimeCalendarTests.TimeCalendar_IsValid_RangeBoundaries`, `TimeCalendarTests.TimeCalendar_Extremes_NeverThrow`,
`TimeCalendarTests.TimeCalendar_ToParts_Utc_MatchesTheBaseClassLibraryAcrossTheRange`,
`TimeCalendarTests.TimeCalendar_Create_NullAndZeroOffsetZones_GiveUtc`,
`TimeCalendarTests.TimeCalendar_ToParts_And_FromParts_AreCultureIndependent`

Asserts that non-finite parts and the years 0 and 10000 give NaN while 0001-01-01T00:00:00.000Z and
9999-12-31T23:59:59.999Z are accepted and one millisecond beyond either is not; that the first and last representable
instants are valid and NaN, infinity, and the values beyond them are not; that eleven extreme values (1e300, the
double extremes, NaN, the infinities, and the ends of the range plus or minus one) placed in every argument of every
entry point of three calendars (UTC, the United States like zone, and +05:30), and in every interval operation, never
throw and give NaN or a valid instant; that about 60,000 instants spread over the whole range (one every 61 days and 7
ms) break into the same year, month, date, weekday, day of the year, and time as `DateTimeOffset` and build back; that
`Create` returns the UTC singleton for null, the UTC zone, and a fixed zone at +00:00; and that the anchor and a
February 29 give the same text under the invariant, `tr-TR`, and `de-DE` cultures.

##### Zone Offsets And Fixed-Offset Zones

**Tests**: `TimeCalendarZoneTests.TimeCalendarZone_UsLike_OffsetsAroundTheTransitions`,
`TimeCalendarZoneTests.TimeCalendarZone_UsLike_ToPartsAroundTheSpringChange`,
`TimeCalendarZoneTests.TimeCalendarZone_FixedOffset_UsesTheFastPathAndRoundTrips`,
`TimeCalendarZoneTests.TimeCalendarZone_SouthernRuleSpanningNewYear`

Asserts the offsets -5 h, -4 h, -4 h, and -5 h one millisecond before and at the two 2024 transitions of the United
States like zone and that the zone is not UTC; that 06:59:59.999Z is local 01:59:59.999 (offset -5 h) and 07:00Z is local
03:00 (offset -4 h); that the fixed zones +05:30, -03:00, and -03:30 show 05:30, 21:00, and 20:30 for 2024-06-01T00:00Z
and round-trip with their offsets; and that the southern zone is at +11 h in January and December, +10 h in July, and
changes at 2024-04-06T16:00Z and 2024-10-05T16:00Z.

##### Gap And Overlap Follow The ECMAScript Rule

**Tests**: `TimeCalendarZoneTests.TimeCalendarZone_UsLike_Gap_0230BecomesThe0330Instant`,
`TimeCalendarZoneTests.TimeCalendarZone_UsLike_Overlap_0130IsTheEarlierInstant`,
`TimeCalendarZoneTests.TimeCalendarZone_UsLike_WallTimesAwayFromTheAmbiguity`,
`TimeCalendarZoneTests.TimeCalendarZone_Southern_GapAndOverlap`

Asserts that 02:30 on 2024-03-10 is 07:30Z and shows as 03:30; that 01:30 on 2024-11-03 is 05:30Z with the offset -4 h;
the six wall times just outside and away from the ambiguity (00:59, 02:00, 01:59, 03:00, a June noon, and a January
noon); and the southern gap (02:30 on October 6 is 16:30Z on the 5th) and overlap (02:30 on April 7 is 15:30Z on the
6th, the earlier instant).

##### Zone Lookups Are Safe At The Edges And Independent Of The Host

**Tests**: `TimeCalendarZoneTests.TimeCalendarZone_ProbeAtYear1AndYear9999NeverThrows`,
`TimeCalendarZoneTests.TimeCalendarZone_Results_DoNotDependOnTheHostZone`

Asserts that the offsets at the first and last representable instants are -5 h (standard time in January), that the
parts at the first instant are 0000-12-31 19:00, that the wall time of year 1 January 1 is the minimum instant plus
18,000,000 ms, that the wall time 23:00 on 9999-12-31 is NaN (it is past the end), and that a summary of an offset, a
gap time, and a day floor is the hand-derived text `-14400000,1710055800000,1710046800000` (offset -4 h on July 1,
07:30Z on March 10, local midnight 05:00Z) on repeated calls whatever zone the process runs in.

##### Day Intervals Follow The Local Midnights

**Tests**: `TimeCalendarIntervalTests.TimeCalendarInterval_Day_AcrossSpringForwardIs23Hours`,
`TimeCalendarIntervalTests.TimeCalendarInterval_Day_FallBackIs25Hours`,
`TimeCalendarIntervalTests.TimeCalendarInterval_Day_CountAcrossSpringForwardCountsCalendarDays`,
`TimeCalendarIntervalTests.TimeCalendarInterval_Hour_FloorIsLocalHourInAHalfHourZone`,
`TimeCalendarIntervalTests.TimeCalendarInterval_Range_DaysInAZoneFollowLocalMidnight`

Asserts that the day of noon on March 10 starts at 05:00Z and ends at 04:00Z the next day (23 hours), that the day of
noon on November 3 starts at 04:00Z and ends at 05:00Z the next day (25 hours), that three calendar days are counted
across the spring change, that the hour of 10:20Z at +05:30 floors to 09:30Z and ceils to 10:30Z, and that the day range
across the spring change is 05:00Z, 05:00Z, and 04:00Z.

##### Intervals Floor, Ceil, Offset, Round, And Count Like d3-time

**Tests**: `TimeCalendarIntervalTests.TimeCalendarInterval_TimeOfDay_FloorInUtc`,
`TimeCalendarIntervalTests.TimeCalendarInterval_Week_IsSundayBasedAndCountsSundays`,
`TimeCalendarIntervalTests.TimeCalendarInterval_Month_OffsetClampsNothingAndRollsYear`,
`TimeCalendarIntervalTests.TimeCalendarInterval_Year_EveryKFloorsToMultiples`,
`TimeCalendarIntervalTests.TimeCalendarInterval_Millisecond_EveryKFloorsToMultiples`,
`TimeCalendarIntervalTests.TimeCalendarInterval_Offset_FloorsFractionalSteps`,
`TimeCalendarIntervalTests.TimeCalendarInterval_Round_PicksTheNearerBoundary`

Asserts the floor of 10:20:30.400 for each time-of-day interval (the components below the interval are cleared); the
week vectors (Sunday March 3, February 25, March 10, the offset by one week, and the counts 2 and 1); the month vectors
(roll-over to March 2, year roll in both directions, floor, ceil, round, and the count 4); the year and the decade
form (floor 2020, ceil 2030, offsets by decades in both directions, a NaN count and no further `every`); the
millisecond form (...700, ...800, ...900, 10, -100); that a fractional step offsets by its floor (1.9 is one hour,
-0.5 is minus one hour, 0 changes nothing) and a NaN or infinite step gives NaN; and that round gives the earlier hour
at 10:29 and the later at 10:30 and 10:31.

##### Filtered And Bounded Every

**Tests**: `TimeCalendarIntervalTests.TimeCalendarInterval_Every_NonPositiveOrNaNIsNull`,
`TimeCalendarIntervalTests.TimeCalendarInterval_Every_HoursByThree`,
`TimeCalendarIntervalTests.TimeCalendarInterval_Every_MinutesAndSecondsByFifteen`,
`TimeCalendarIntervalTests.TimeCalendarInterval_Every_DaysByTwoAndMonthsByThree`,
`TimeCalendarIntervalTests.TimeCalendarInterval_Every_WeeksByTwoKeepsEveryOtherEpochWeek`,
`TimeCalendarIntervalTests.TimeCalendarInterval_Every_IsBounded`

Asserts that a step that is zero, negative, NaN, infinite, or below one gives no interval and that a step of 1 or a
fraction below 2 gives the interval itself; the floor, ceil, offset (in both directions, by three, and across the day
end), and round of `hour.every(3)`; the quarters of `minute.every(15)` and `second.every(15)`; the odd dates of
`day.every(2)` (including the jump from January 31 to February 1 and back to December 31) and the quarters of
`month.every(3)`; that the floor of `week.every(2)` over 30 consecutive weeks is a Sunday midnight less than two weeks
before the instant on an even week count; and the bounds (week steps up to 64, an hour step of 1,000 keeping only hour
0, and 64 steps accepted and 65 steps NaN in both directions).

##### Epoch-Day Filter For Two-Day Ticks

**Tests**: `TimeCalendarEpochDayTests.TimeCalendar_EveryEpochDay_KeepsEvenDaysSinceTheEpoch`,
`TimeCalendarEpochDayTests.TimeCalendar_EveryEpochDay_FloorCeilOffset`,
`TimeCalendarEpochDayTests.TimeCalendar_EveryEpochDay_RejectsWhatItCannotDo`

Asserts that every second epoch day gives January 2, 4, 6, 8, and 10 where the month-based filter gives January 1, 3,
5, 7, 9, and 11; the floor, ceil, offset across a month end, and the floor before the epoch derived above; and that
there is no interval for a step that is not a positive whole number, above 64, on a calendar that is not UTC, on an
interval that is not the day, or on a filtered interval, while a step of 1 gives the day itself.

##### Ranges Are Exclusive, Capped, And Cancelable

**Tests**: `TimeCalendarIntervalTests.TimeCalendarInterval_Range_IsCeilStartToExclusiveStop`,
`TimeCalendarIntervalTests.TimeCalendarInterval_Range_EmptyCases`,
`TimeCalendarIntervalTests.TimeCalendarInterval_Range_ThrowsAtMaxCount`,
`TimeCalendarIntervalTests.TimeCalendarInterval_Range_PollsCancellation`

Asserts the three days, two days from noon, four days by two, and eleven months of the range vectors above; that a start
not before the stop, a step below one, and NaN arguments give an empty list; that a range that fits the cap succeeds, one
item more throws `TimeRangeLimitException` with the cap, and a range of every hour of the whole calendar stops at a cap
of
1,000 instead of allocating; and that a canceled token stops the range of a million milliseconds with an
`OperationCanceledException` and not with the limit.

##### Interval Objects Agree With UTC And Are Cached

**Tests**: `TimeCalendarIntervalTests.TimeCalendarInterval_Utc_And_Zone_Intervals_Agree_ForAZoneAtOffsetZero`,
`TimeCalendarIntervalTests.TimeCalendarInterval_Interval_IsCachedPerKind`

Asserts that, for a zone with a zero base offset (in winter, so no daylight change is reached) and for every interval
kind, the floor, ceil, offset, and count of a zone calendar equal those of the UTC calendar at instants that are
spread over 20 days and are not on a boundary; and that a calendar hands out one interval object per kind, with its kind
and calendar.

##### Interval Names

**Tests**: `TimeCalendarIntervalTests.TimeCalendarInterval_TryParseName_AcceptsTheDocumentedAndTheCodedSpellings`,
`TimeCalendarIntervalTests.TimeCalendarInterval_TryParseName_RejectsUnknownNames`,
`TimeCalendarIntervalTests.TimeCalendarInterval_TryParseName_IsCultureIndependent`

Asserts that the d3 names (`millisecond` to `year`), the Vega names (`milliseconds`, `seconds`, `minutes`, `hours`,
`date`, `dayofyear` as a day, `quarter` as three months), the `utc` forms (`utcDay`, `utcQuarter`, `UTCYear`), and the
upper-case spelling are accepted with the kind, the multiplier, and the UTC flag; that the empty text, `utc`,
`fortnight`, `isoWeek`, and `days` are rejected with no UTC flag; and that `MINUTES` is accepted under `tr-TR` and
`de-DE` as under the invariant culture.

##### Time Unit Names And Parts

**Tests**: `TimeUnitSpecTests.TimeUnitSpec_Parse_AcceptsEveryVegaLiteName`,
`TimeUnitSpecTests.TimeUnitSpec_Parse_BinnedUnits`, `TimeUnitSpecTests.TimeUnitSpec_Parse_RejectsUnknownAndIncompatible`,
`TimeUnitSpecTests.TimeUnitSpec_Parse_ObjectForm`, `TimeUnitSpecTests.TimeUnitSpec_Parse_RejectsInvalidObjects`,
`TimeUnitSpecTests.TimeUnitSpec_TryRead_AcceptsNamesAndObjectsAndSkipsUnknownMembers`

Asserts that each of the 40 names (and its `utc` form) has the parts of the list above in the canonical order, the
normalized unit name, the step 1, no `maxbins`, and the UTC flag only for the `utc` form; that `binnedyearmonth` and
`binnedutcyearmonthdate` are binned with their parts and `binnedmonth` and `binnedhours` are not units; that `foo`, the
empty text, `year-week-date`, `day-month`, `Month`, `utc-utc-month`, `utc`, and `months` are rejected with a message
that quotes the name; that the object form reads the unit, the step (3), the UTC flag or prefix, the binned flag, and a
`maxbins` object with no unit (no parts, `maxbins` 10); that a zero or negative step, a text step, a text UTC flag, a
numeric binned flag, an unknown unit, a numeric unit, an unknown key, an object without a unit or `maxbins`, a zero
`maxbins`, the empty object, and non-objects are rejected with a message; and that the reader of a `timeUnit` value
accepts a name or an object, skips a member that a time unit does not define when asked to (it rejects it otherwise,
naming it), gives the title name `utcyearmonth` or `month_step_3`, and reads nothing for null, a number, an unknown
name, or an object without a unit, with a null node an `ArgumentNullException`.

##### Field Names And Label Specifiers

**Tests**: `TimeUnitSpecTests.TimeUnitSpec_FieldName_MatchesVegaLiteNaming`,
`TimeUnitSpecTests.TimeUnitSpec_Specifier_FollowsVegaTimeUnitSpecifier`,
`TimeUnitSpecifierTests.TimeUnitSpec_TryGetUnitSpecifier_ValidUnits_JoinTheDefaults`,
`TimeUnitSpecifierTests.TimeUnitSpec_TryGetUnitSpecifier_Overrides_ReplaceTheDefaults`,
`TimeUnitSpecifierTests.TimeUnitSpec_TryGetUnitSpecifier_InvalidUnits_AreRejected`,
`TimeUnitSpecifierTests.TimeUnitSpec_Specifier_KeepsTheVegaLiteOverrides`

Asserts the nine derived field names; the 27 Vega-Lite specifiers of the table above (with the corrected
`minutesseconds` specifier `00:%M:%S`, without a space); the 13 lists of Vega's default specifiers (`year,month` and
`month,year` are `%Y-%m`, `year,isoweek` is `%G W%V`, and so on); that an override replaces a default (`year-month`
`%B %Y`, `date` `%e` giving `%b %e` for month and date); that the lists none, `fortnight`, `Year`, `year,year`,
`week,dayofyear`, `day,dayofyear`, `month,dayofyear`, `quarter,dayofyear`, `week,isoweek`, and thirteen parts are
rejected with the empty specifier; and that `yearmonth` is `%b %Y` in Vega-Lite and `%Y-%m` in Vega.

##### Floors Use The Reference Year And Vega's Week Rules

**Tests**: `TimeUnitSpecTests.TimeUnitSpec_Floor_UsesReferenceYear2012_ForUnitsWithoutYear`,
`TimeUnitSpecTests.TimeUnitSpec_Floor_SecondsAndMilliseconds`, `TimeUnitSpecTests.TimeUnitSpec_Floor_Day_IsWeekdayInJanuary2012`,
`TimeUnitSpecTests.TimeUnitSpec_Floor_Week_And_WeekDay`, `TimeUnitSpecTests.TimeUnitSpec_Floor_YearWeek_Handles2013Week0`,
`TimeUnitSpecTests.TimeUnitSpec_Floor_Quarter_And_DayOfYear`, `TimeUnitSpecTests.TimeUnitSpec_Floor_StepAppliesToLastUnitOnly`

Asserts the nine floors of 2024-03-17T10:20:45.678 listed above, the seconds and milliseconds floors, the three weekday
vectors (1,325,548,800,000 for Tuesday, January 1 for Sunday, January 7 for Saturday), week 9 (February 26, February 28,
March 5, and March 3), the seven week-number vectors around the start of 2012 and 2013 with the floor 2012-12-30, the
quarter and day-of-year units (April 1, February 29, March 1, 2023), and the eight stepped floors.

##### Ends Follow The Calendar Of The Chart

**Tests**: `TimeUnitSpecTests.TimeUnitSpec_End_IsOneStepOfTheSmallestUnit`,
`TimeUnitSpecTests.TimeUnitSpec_End_DayFollowsTheCivilDayInAZone`, `TimeUnitSpecTests.TimeUnitSpec_Utc_Units_IgnoreTheZone`,
`TimeUnitSpecTests.TimeUnitSpec_Floor_And_End_AreTotal`

Asserts the ends of `yearmonth` (1,711,929,600,000), `month` (1,333,238,400,000), and `day` (one day later) and of the
quarter, week, year, hour, minute, second, and millisecond units and the stepped units; that the bin of noon on
March 10 in the United States like zone is 05:00Z to 04:00Z; that the `utc` unit ignores the zone while the local unit
follows it; and that a NaN instant, an instant beyond the range, and an end beyond the range are NaN, and a `maxbins`
unit with no
parts has no floor, no end, no specifier, and no duration.

##### Max-Bins Resolution

**Tests**: `TimeUnitSpecTests.TimeUnitSpec_MaxBins_PicksVegaTimeBinIntervals`,
`TimeUnitSpecTests.TimeUnitSpec_MaxBins_ResolvedUnitFloorsAndKeepsItsName`,
`TimeUnitSpecTests.TimeUnitSpec_MaxBins_NonFiniteExtentGivesYears`

Asserts the four derived resolutions (year, month, date step 1; year, month step 1; year step 10; the seven parts
through milliseconds with step 100), that a resolved unit floors 2024-03-05T13:00Z to the day, keeps the UTC flag, its
`maxbins`, and its field name `utctimeunit_maxbins_5_f`, has the specifier `%b %d, %Y`, and that resolving it again
returns the same object; and that an extent that is NaN resolves to the year with step 1.

##### Durations And Single Units

**Tests**: `TimeUnitSpecTests.TimeUnitSpec_Duration_IsTheSmallestPartOfTwoThousandOne`,
`TimeUnitSpecTests.TimeUnitSpec_Duration_DayOfWeekHasNone`, `TimeUnitSpecTests.TimeUnitSpec_IsSingleLocalUnit_OnlyForOneLocalPart`,
`TimeUnitSpecTests.TimeUnitSpec_Parse_Specifier_And_Floor_AreCultureIndependent`

Asserts the thirteen derived durations, that `day`, `weekday`, and `yearweekday` have none, that only a unit of one local
part is a single local unit (`month` is, `utcmonth`, `yearmonth`, and `binnedyear` are not), and that names, specifiers,
field names, and floors of five units whose names contain the letter I are the same under `tr-TR`, `de-DE`, and the
invariant culture.

##### Datetime Objects Are Read And Evaluated

**Tests**: `DateTimeObjectTests.DateTimeObject_ToMilliseconds_MonthNamesAndNumbers`,
`DateTimeObjectTests.DateTimeObject_ToMilliseconds_Quarter`, `DateTimeObjectTests.DateTimeObject_ToMilliseconds_DayOfWeek`,
`DateTimeObjectTests.DateTimeObject_TryRead_DayCombinedWithOtherKeysIsDropped`,
`DateTimeObjectTests.DateTimeObject_ToMilliseconds_UtcFlagSelectsUtc`, `DateTimeObjectTests.DateTimeObject_ToMilliseconds_Defaults`,
`DateTimeObjectTests.DateTimeObject_ToMilliseconds_AllKeysAndOverflow`,
`DateTimeObjectTests.DateTimeObject_ToMilliseconds_TwoDigitYearsAndOutOfRange`

Asserts the six spellings of March (3, `"3"`, `March`, `mar`, `MAR`, `march`) giving 2012-03-01; the quarter rules; the
ten day-of-week spellings; the dropped day with a month or the UTC flag; `{hours: 5}` as 10:00Z in the zone and 05:00Z
with `utc`; the defaults (2012-01-01, the year 2020, 5 ms); the anchor from all keys and the four overflow vectors; and
the years 24, 0, 100, 10000, and -5.

##### Datetime Objects Are Validated

**Tests**: `DateTimeObjectTests.DateTimeObject_TryRead_RejectsInvalidMembers`,
`DateTimeObjectTests.DateTimeObject_TryRead_RejectsNonObjects`, `DateTimeObjectTests.DateTimeObject_NamesAreCultureIndependent`

Asserts that an unknown month or day name, a boolean or empty month, a text quarter, year, or date, a null time part, an
unknown key (`foo`, `week`), and a text UTC flag are rejected, with a message and the node of the offending member; that
a number, a text, and an array are rejected at the node itself; and that `FRIDAY` (2012-01-06), `SEPTEMBER`
(2012-09-01), and `FRI` against `day` give the same instants under `tr-TR`, `de-DE`, and the invariant culture.

##### Bare Numbers And Texts Compared With A Time Unit

**Tests**: `DateTimeObjectTests.DateTimeObject_FromScalar_NumbersFollowTheUnit`,
`DateTimeObjectTests.DateTimeObject_FromScalar_StringsFollowTheUnit`

Asserts that 5 against `hours` is 05:00, 3 against `month` is March, 2024 against `year`, 2 against `quarter` is
April 1, 30 against `date`, 7 against `day` is January 1 (Sunday), 7 against `minutes` is 00:07, and 5 against `week`
is the default date; that 1,709,251,200,000 and the values against units that are not single or local are
milliseconds; that 1e300 is NaN; that the texts `Mar`, `Tuesday`, and `5` are the part, a text the date parser reads is
that date, and `abc`, `Mar` against `yearmonth`, and `Mar` with no unit are not dates.

##### Datetime Objects Become Expression Source

**Tests**: `DateTimeObjectTests.DateTimeObject_ToExpression_WritesTheNormalizedParts`,
`DateTimeObjectTests.DateTimeObject_ToExpression_IsTheSameInstantAsToMilliseconds`,
`DateTimeObjectTests.DateTimeObject_TryUnitPartExpression_WritesThePartWithDefaults`

Asserts the source text of nine objects (month names as the zero-based index, the quarter as three months, the weekday
as a date, `utc(...)` for a UTC object, and the invariant text of a fractional second); that the expression evaluates to
the same instant as `ToMilliseconds` (checked in UTC and at +05:30, where 08:00 local is 5 h 30 min earlier in UTC); and
the source of a single part of a unit (month 3 is index 2, `Mar` is the same, `Fri` is the date 6, quarter 2 is month
index 3, hours 5, `week` the default) and none for an invalid name or no value.
