### TimeUnitTransform Unit Verification Design

This document describes the unit-level verification strategy for the `TimeUnitTransform` unit in the
`DemaConsulting.CanvasNet.VegaLite.Transforms` namespace: the internal static `TimeUnitTransform` class (`Apply`) with
the `TimeUnitRequest` record, which adds to every row of a `DataTable` the start and the end of the time unit that
contains a date (the `timeUnit` transform with `as` naming the two fields, and the implicit time unit of an encoding),
resolving the `maxbins` form against the data; and the internal static `TemporalFields` class (`Collect`), which finds
the fields of a view that are read as dates (the implicit date parse of the data loader). The calendar arithmetic that
both rely on (the unit floors and ends, the time zone, and the end-of-range rules) belongs to `TimeCalendar` and is
verified there (see the _TimeCalendar Unit Verification Design_); this document verifies that the transform applies it
to the rows correctly, charges and bounds its work, and fails closed.

#### Verification Approach

The unit is verified by parsing small, original specification strings with the real `SpecParser`, loading the data with
the real `DataLoader` (which applies the implicit date parse of `TemporalFields.Collect` with the calendar of a zone),
and applying the transform the way a render does (the `Run`, `Prepare`, and `Encode` helpers of `TimeTestPipeline.cs`),
or by calling `TimeUnitTransform.Apply` directly with a hand-built request. The collaborators (`SpecParser`,
`DataLoader`, `DataTable`, `ExpressionEvaluator`, `RowTransforms` as the dispatcher, `TimeCalendar`, `TimeUnitSpec`, and
`VegaLiteLimits`) are real and no mocking or stubbing is used; the internal types are reached through the test
assembly's `InternalsVisibleTo` access. Every zone is built in the test (`TimeTestSupport.UsLike` and `Fixed`) and no test
reads the host's zone, clock, or culture. The expected values are derived by hand, with the derivation in the test
comments and repeated here (the day numbers and the anchors are those of the calendar verification: the epoch is day 0
and 2024-01-01 is day 19,723 = 54 x 365 + 13 leap days; a day is 86,400,000 ms):

- **Anchors.** 2024-03-01 is day 19,783 (31 + 29 days after January 1), **1,709,251,200,000**; 2024-04-01 is 31 days
  later, **1,711,929,600,000**; 2012-01-01 is day 42 x 365 + 10 leap days = 15,340, a Sunday, **1,325,376,000,000**;
  2012-03-01 is 31 + 29 = 60 days later, **1,330,560,000,000**, and 2012-04-01 is 31 days after that,
  **1,333,238,400,000**; 2012-01-03 is 2 days after 2012-01-01, **1,325,548,800,000**.
- **`yearmonth` of 2024-03-17T10:20Z.** The start is the first of March, 1,709,251,200,000, and the end is the first of
  April, 1,711,929,600,000, which is 31 x 86,400,000 = 2,678,400,000 ms later; the table keeps its fields and rows, in
  the order `d`, `k`, `u`, `u_end`, and both new fields are dates.
- **`month` of the same date** has no year, so it uses the reference year 2012 (a leap year that starts on a Sunday):
  the start is 2012-03-01, 1,330,560,000,000, and the end is 2012-04-01, 1,333,238,400,000.
- **`day`** is the weekday of January 2012: Tuesday 2024-03-05 is 2012-01-03 (1,325,548,800,000) and Sunday 2024-03-10
  is 2012-01-01 (1,325,376,000,000); each end is one day (86,400,000 ms) later.
- **Time of day** for 2024-03-05T13:04:09.007Z on the reference date 2012-01-01: `hours` starts 13 x 3,600,000 =
  46,800,000 ms after it and ends one hour (3,600,000) later; `minutes` starts 4 x 60,000 = 240,000 after it and ends
  60,000 later; `seconds` starts 9,000 after it and ends 1,000 later; `milliseconds` starts 7 after it and ends 1
  later; `hoursminutes` starts 46,800,000 + 240,000 = 47,040,000 after it and ends 60,000 later.
- **Quarters.** `yearquarter` of 2024-05-17 starts on 2024-04-01 (1,711,929,600,000) and ends on 2024-07-01, which is
  30 + 31 + 30 = 91 days later; `quarter` alone starts on 2012-04-01 (1,333,238,400,000) and ends on 2012-07-01, 91
  days later.
- **Step 3.** `{unit: month, step: 3}` floors March (month index 2) to the multiple of 3, January 2012 (1,325,376,000,000)
  and keeps April (index 3) as April 2012, which is 31 + 29 + 31 = 91 days after January 1; the ends are three months
  after the starts: April 2012 (the start plus 91 days) and July 2012.
- **`maxbins` over five days.** The rows 2024-03-01, 2024-03-03T12:00, and 2024-03-06 have an extent of 5 days; with 5
  bins the target is 1 day, which is the day step of Vega's table, so the unit is year, month, date with step 1: the
  starts are 1,709,251,200,000 and that plus 2 and 5 days, and the ends are the starts plus one day (86,400,000 ms).
- **`maxbins` over a year.** The rows 2024-01-01, 2024-06-15, and 2025-01-01 have an extent of 366 days (2024 is a leap
  year); with 10 bins the target is 36.6 days, which is nearer to the month of 30 days (36.6 / 30 = 1.22) than to the
  quarter of 90 days (90 / 36.6 = 2.46), so the unit is year, month: the starts are 2024-01-01, 2024-06-01, and
  2025-01-01, and the ends are 2024-02-01, 2024-07-01, and 2025-02-01. A table with no valid date, or an empty table,
  has nothing to resolve: the fields are null and the unit stays as written.
- **Values that are not dates.** With the parse switched off (`format: {parse: null}`) a null, a boolean, a text, the
  number 1e300 (beyond the years 1 to 9999), the number -1e15 (about 31,688 years before the epoch), and a missing field
  give null in both fields, while the number 0 is a date (milliseconds since the epoch): the start of January 1970 is
  0 and the end is 31 x 86,400,000 = 2,678,400,000. With the implicit parse of a temporal field the text `2024-03-17`
  and the legacy form `Mar 17, 2024` are the same date, March 17, and `not a date` is null (with the loader's parse
  warning).
- **Binned unit.** `binnedyearmonth` does not floor: the value 2024-03-01T00:00Z is the start, 1,709,251,200,000, and
  the end follows one month later, 1,711,929,600,000.
- **Zones.** At +05:30 the instant 2024-03-05T20:00Z is 2024-03-06 01:30 local, so `date` starts at the local midnight of
  January 6 of the reference year, which is 2012-01-05T18:30Z, and ends 24 hours later at 2012-01-06T18:30Z, while
  `utcdate` (or `{unit: date, utc: true}`) ignores the zone: the UTC date is the 5th, so it starts at 2012-01-05T00:00Z
  and ends at 2012-01-06T00:00Z. In the United States like zone (standard time -5 h, daylight time -4 h from
  2024-03-10T07:00Z) the instant 2024-03-10T12:00Z is 08:00 local, so `yearmonthdate` starts at local midnight,
  00:00 at -5 h = 05:00Z, and ends at the next local midnight, 00:00 at -4 h = 2024-03-11T04:00Z, which is 23 hours
  (82,800,000 ms) later. The instant 2024-12-31T23:59:59.999Z is 18:59:59.999 on December 31 at -5 h, so `yearmonth`
  starts at local midnight of December 1, 05:00Z.
- **Charging.** A row costs 2 elements per request in UTC (or for a `utc` unit) and 8 in a zone, and the work meter pays
  one step for each complete block of 64 elements. Ten thousand rows of one request cost 20,000 elements = 312 steps
  (312.5, whole blocks only) in UTC, so a budget of 100 steps fails and the default budget does not; in a zone the same
  rows cost 80,000 elements = 1,250 steps, so a budget of 600 steps passes in UTC (312) and fails in the zone (1,250).
  The extent pass of a `maxbins` unit adds 1 element per row in UTC and 2 in a zone.
- **Limits.** The fields `d`, `u`, and `u_end` are three, so a field limit of 2 fails before any cell is allocated; the
  output names go through the string limit (`u` has one character, so a limit of 1 fails on `u_end`); a `maxbins` of 50
  with a domain limit of 10 fails; every failure is a located render exception that names its limit and carries the
  pointer of the transform (`/transform/0`) or of the channel (`/encoding/x/timeUnit`).
- **Cancellation.** The token is checked at the first row of every batch of 1,024 rows (and in the extent pass); a
  table of 3,000 rows with a canceled token stops with an `OperationCanceledException`.

Unit tests reside in `TimeUnitTransformTests.cs` and `TemporalFieldsTests.cs`, with the shared helper
`TimeTestPipeline.cs` (and `TimeTestSupport.cs` of the `Data` folder for the zones), all within the
`DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Transforms` folder. The time units in the explicit transform
dispatch and in the encoding are verified in `RowTransformsTimeTests.cs`, `EncodingPlanTimeTests.cs`, and
`EncodingTransformsTimeTests.cs` of the same folder, which are referenced below (see also the _RowTransforms_ and
_EncodingPlan Unit Verification Designs_).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Files**: None; every specification is an in-memory string, and no file or network is read
- **Isolation**: Each test parses its own specification and builds its own loader, evaluator, and context. Every zone is
  a custom zone created by the test; the tests never read `TimeZoneInfo.Local`, the clock, or the host's zone database.
  The cancellation scenario passes a token that is canceled before the call, and the determinism scenario runs the same
  pipeline under `tr-TR` and `de-DE` and compares the results with those of the invariant culture

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any start or end of a unit that differs from the hand-derived instant, a value that is not a date and does not
give null, a field or a row that is added, dropped, or reordered differently from the expectation, a limit or a budget
that does not fail closed at the location of the transform or the channel, a canceled token that does not stop the
loop, or a result that depends on the host's zone or the current culture constitutes a failure.

#### Unit-Level Test Scenarios

##### Year-Month, Month, And Day Units Have Hand-Derived Starts And Ends

**Tests**: `TimeUnitTransformTests.TimeUnitTransform_YearMonth_StartAndEnd`,
`TimeUnitTransformTests.TimeUnitTransform_Month_UsesReferenceYear2012`,
`TimeUnitTransformTests.TimeUnitTransform_Day_IsTheWeekdayOfJanuary2012`

Asserts the fields `d`, `k`, `u`, `u_end` of `yearmonth` with the start 1,709,251,200,000 and the end 1,711,929,600,000
(31 days) and the untouched field `k`; the start 1,330,560,000,000 and the end 1,333,238,400,000 of `month`; and the
starts 1,325,548,800,000 (Tuesday) and 1,325,376,000,000 (Sunday) of `day`, each ending a day later.

##### Time-Of-Day And Quarter Units

**Tests**: `TimeUnitTransformTests.TimeUnitTransform_Hours_Minutes_Seconds_Milliseconds`,
`TimeUnitTransformTests.TimeUnitTransform_Quarter_YearQuarter_Ends`

Asserts the five time-of-day vectors (`hours`, `minutes`, `seconds`, `milliseconds`, and `hoursminutes`, each at its
offset from 2012-01-01T00:00Z with its own step), and the quarters: `yearquarter` from 1,711,929,600,000 for 91 days
and `quarter` from 1,333,238,400,000 for 91 days.

##### Steps Apply To The Last Part

**Tests**: `TimeUnitTransformTests.TimeUnitTransform_Step_ThreeMonths`

Asserts that `{unit: month, step: 3}` gives the starts January 2012 and April 2012 (January plus 91 days) and the ends
April 2012 and July 2012 for March 17 and April 2.

##### Max-Bins Resolves Against The Data

**Tests**: `TimeUnitTransformTests.TimeUnitTransform_MaxBins_Day`, `TimeUnitTransformTests.TimeUnitTransform_MaxBins_Month`,
`TimeUnitTransformTests.TimeUnitTransform_MaxBins_ResolvedUnitsAndEmptyTables`

Asserts the day bins of the five-day extent and the month bins of the 366-day extent derived above; that the unit chosen
for the `maxbins` form is reported to the caller (year, month, date with step 1 for five days and five bins); and that
an empty table keeps its fields `u` and `u_end` and a table of nulls gets null fields.

##### Values That Are Not Dates Give Null

**Tests**: `TimeUnitTransformTests.TimeUnitTransform_NullAndInvalidAndStringDates_YieldNull`,
`TimeUnitTransformTests.TimeUnitTransform_ImplicitParseReadsTextDates`,
`TimeUnitTransformTests.TimeUnitTransform_BinnedUnit_KeepsTheValueAsTheStart`

Asserts the seven rows of the not-a-date vector (null, boolean, text with the parse off, 1e300, a missing field, 0, and
-1e15) with the dates and the nulls described above; that the implicit parse reads `2024-03-17` and `Mar 17, 2024` as
the same day and `not a date` as null; and that the binned unit keeps the value as the start and ends one month later.

##### Zones And Utc Units

**Tests**: `TimeUnitTransformTests.TimeUnitTransform_Utc_ZoneIndependent`,
`TimeUnitTransformTests.TimeUnitTransform_Zone_LocalDayAcrossDst`

Asserts the local and the UTC date vectors at +05:30 (starts 2012-01-05T18:30Z and 2012-01-05T00:00Z, with the `utc`
prefix and the `utc` flag equal) and the 23-hour day of the United States like zone (05:00Z to 04:00Z the next day).

##### Several Requests And Replaced Fields

**Tests**: `TimeUnitTransformTests.TimeUnitTransform_SeveralRequestsAndReplacedFields`

Asserts that two requests are written in one pass, that an output name replaces an existing field of that name (`u` was
the text `old` and is the date 1,709,251,200,000), and that the fields are `d`, `u`, `u_end`, `y`, `y_end` with the
year 2024-01-01 to 2025-01-01.

##### Limits, Charging, And Cancellation Fail Closed

**Tests**: `TimeUnitTransformTests.TimeUnitTransform_MaxBins_AboveTheLimitFails`,
`TimeUnitTransformTests.TimeUnitTransform_FieldLimit_IsCheckedBeforeAllocation`,
`TimeUnitTransformTests.TimeUnitTransform_OutputNameIsCheckedAgainstTheStringLimit`,
`TimeUnitTransformTests.TimeUnitTransform_Cancellation_StopsTheRowLoop`,
`TimeUnitTransformTests.TimeUnitTransform_StepsAreCharged`,
`TimeUnitTransformTests.TimeUnitTransform_ZonedRowsCostMoreThanUtcRows`

Asserts that a `maxbins` of 50 above a domain limit of 10, a field limit of 2, and a string limit of 1 are located
render exceptions at `/transform/0` that name `MaxScaleDomainSize`, `MaxFieldCount`, and `MaxStringLength`; that a
canceled token stops the loop of 3,000 rows; that 10,000 rows fail a budget of 100 steps with `MaxEvaluationSteps` at
`/transform/0` while the default budget produces the rows (all on 1970-01-01, so the last start is 0); and that a
budget of 600 steps passes in UTC (312 steps) and fails in a zone (1,250 steps).

##### Determinism And Argument Checks

**Tests**: `TimeUnitTransformTests.TimeUnitTransform_Deterministic_AcrossRunsAndCultures`,
`TimeUnitTransformTests.TimeUnitTransform_Apply_RejectsNullArguments`

Asserts that two runs and runs under `tr-TR` and `de-DE` give the same starts, the second being 2024-12-01T05:00Z in the
United States like zone, and that a null table, request list, or context throws `ArgumentNullException`.

##### Implicit Date Fields Are Collected From The Encoding

**Tests**: `TemporalFieldsTests.TemporalFields_Collect_NothingWithoutTemporalFields`,
`TemporalFieldsTests.TemporalFields_Collect_TypeTimeUnitAndFormatType`,
`TemporalFieldsTests.TemporalFields_Collect_SecondaryChannelsConditionsArraysAndNames`,
`TemporalFieldsTests.TemporalFields_Collect_IgnoresDefinitionsWithoutAFieldName`,
`TemporalFieldsTests.TemporalFields_Collect_RejectsNull`

Asserts that a view without an encoding or without a temporal field has none; that a field is read as dates by its type
`temporal`, by a time unit (also on an ordinal field, and not for a `null` time unit), or by the format type `time` on
the definition, its axis, or its legend (and not by `number`); that a secondary channel (`x2`, `y2`) follows the type
of its main channel (`end` follows the temporal `x`, `hi` does not follow the quantitative `y`); that the field of a
condition and the elements of an array of definitions are judged on their own; that names are canonical (`a.b` and
`no\.dot`); that a value, a datum, and a count add nothing; and that a null view throws `ArgumentNullException`.

##### Implicit Date Fields Are Collected From The Transforms

**Tests**: `TemporalFieldsTests.TemporalFields_Collect_TimeUnitTransformFields`,
`TemporalFieldsTests.TemporalFields_Collect_FilterPredicatesWithTimeUnitsAndDatetimeObjects`,
`TemporalFieldsTests.TemporalFields_Collect_ConditionTests`

Asserts that the field of a `timeUnit` transform is read as dates (`d` and `a.b`) and the field it derives is not; that
a filter predicate with a time unit or a datetime object among its values makes its field a date field, also inside a
composition (`and`, `or`, `not`), while a plain comparison, a `valid` test, an expression text, and an expression
reference do not; and that the test of a condition is read like a filter predicate.

##### Time Units In The Encoding Are Named, Titled, And Planned

**Tests**: `EncodingPlanTimeTests.EncodingPlan_Create_NamesATimeUnitFieldAndItsEnd`,
`EncodingPlanTimeTests.EncodingPlan_Create_ObjectFormNames`,
`EncodingPlanTimeTests.EncodingPlan_Create_MarksWithABandPositionSpanTheirUnit`,
`EncodingPlanTimeTests.EncodingPlan_Create_BarWithTimeUnit_HasBandEnd_PointWithTimeUnit_HasNoBandEnd`,
`EncodingPlanTimeTests.EncodingPlan_Create_OrdinalTimeUnit_HasNoBandEnd`,
`EncodingPlanTimeTests.EncodingPlan_Create_SecondaryChannel_SuppressesBandEnd`,
`EncodingPlanTimeTests.EncodingPlan_Create_TitlesVerbalFunctionalPlain`,
`EncodingPlanTimeTests.EncodingPlan_Create_AggregateGroupsByTimeUnitStart`,
`EncodingPlanTimeTests.EncodingPlan_Create_BinWinsOverTimeUnit`,
`EncodingPlanTimeTests.EncodingPlan_Create_SharedAndDistinctUnits`,
`EncodingPlanTimeTests.EncodingPlan_Create_StackUsesTheTimeUnitFields`,
`EncodingPlanTimeTests.EncodingPlan_Create_WithoutAnEncodingThereAreNoUnits`

Cross-reference to the plan of the unit in the encoding (the unit verification design of `EncodingPlan` owns the
scenario): the field `yearmonth_date` with its end `yearmonth_date_end`, the object-form names (`month_step_2_date`,
`utcmonth_date`, `timeunit_maxbins_10_date`), the band end of a bar, a rect, and a tick and not of a point, a circle, a
rule, an ordinal field, a color channel, or a position with a secondary channel, the three title modes (`date
(year-month)`, `YEARMONTH(date)`, `date`), the group-by of an implicit aggregate (the start, and the end when the
channel spans the unit), a bin winning over a unit, shared and distinct units, the stack by the unit fields, and no
units without an encoding.

##### The Pipeline Runs The Units Before The Aggregate And Hands Them On

**Tests**: `EncodingTransformsTimeTests.EncodingTransforms_TimeUnitThenAggregate_CountsByMonth`,
`EncodingTransformsTimeTests.EncodingTransforms_TimeUnitThenAggregate_PointHasNoEndField`,
`EncodingTransformsTimeTests.EncodingTransforms_BinThenTimeUnitThenAggregate_ThenStack`,
`EncodingTransformsTimeTests.EncodingTransforms_StackByTimeUnitField`,
`EncodingTransformsTimeTests.EncodingTransforms_SortByAggregate_KeepsTheUnitFieldsInTheRawTable`,
`EncodingTransformsTimeTests.EncodingTransforms_MaxBins_HandsOnTheResolvedUnit`,
`EncodingTransformsTimeTests.EncodingTransforms_TimeUnit_FollowsTheCalendarOfTheChart`,
`EncodingTransformsTimeTests.EncodingTransforms_WithoutTimeUnits_HasNoUnits`,
`EncodingTransformsTimeTests.EncodingTransforms_TimeUnit_FailureIsLocatedAtTheChannelUnit`,
`RowTransformsTimeTests.RowTransforms_TimeUnitTransform_ThenAggregate`,
`RowTransformsTimeTests.RowTransforms_TimeUnitTransform_UsesTheChartZone`

Cross-reference to the encoding and the explicit transform (the unit verification designs of `EncodingPlan` and
`RowTransforms` own the scenarios): the six dates of January (two), February (three, the 29th being a leap day), and
March (one) are counted per month in first-seen order, 2, 3, 1, with the starts 2024-01-01, 2024-02-01, 2024-03-01 and
the ends one month later and no end field for a point; the values 5, 15, 15 in the bins [0, 10) and [10, 20) with the
months January, January, and February of 2012 stack descending on y (February (0, 1) at the bottom and January (1, 2)
on top in the second bin); the Raw table keeps the unit fields of the six rows while the Main table has three groups;
the five-day `maxbins` unit is handed on with the day parts and the starts March 1, 3, and 6; the local day of
2024-03-10T12:00Z starts at 2024-03-10T05:00Z and ends 23 hours later in the United States like zone and at the UTC
midnight in UTC; the step budget of 100 fails at `/encoding/x/timeUnit` naming `MaxEvaluationSteps`; and the explicit
`timeUnit` transform dispatched by `RowTransforms` feeds an aggregate and follows the zone of the chart (2024-03-01T02:00Z
is February 29 at 21:00 at -5 h, so its month starts at 2024-02-01T05:00Z and ends at 2024-03-01T05:00Z).
