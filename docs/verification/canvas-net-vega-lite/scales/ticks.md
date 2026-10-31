### Ticks Unit Verification Design

This document describes the unit-level verification strategy for the `Ticks` unit in the
`DemaConsulting.CanvasNet.VegaLite.Scales` namespace: the internal static `Ticks` class (`GetTicks`, `TickIncrement`,
`TickStep`, `Nice`, `DefaultAxisTickCount`, `DefaultAxisTickMinStep`, `ResolveTickCount`, and `AxisTicks`, the log ticks,
and the time ticks `TimeTickInterval`, `TimeTicks`, and `TimeNice` with the `TimeTickRequest` struct). It is an
own C# implementation of the d3-array tick algorithms, the d3-scale linear `nice`, the d3-time tick intervals, and the
Vega and Vega-Lite tick
count rules; the third-party notice of the system names the upstream behavior that was studied.

#### Verification Approach

The unit is verified with vector tables whose expected values are derived by hand from the algorithm and from
arithmetic, and not by running d3 or Vega and pasting the results. The algorithm is: for a span between the start and
the stop and a requested count, the step estimate is `span / count`; its power of ten is `10^floor(log10(estimate))`;
the error is the estimate divided by that power; the factor is 1 when the error is below the square root of 2, 2 below
the square root of 10, 5 below the square root of 50, and otherwise 10; the step is the factor times the power (and
the ticks are the multiples of the step inside the range, computed as exact decimals when the step is fractional). The
tests state this derivation next to each vector, for example:

- `ticks(0, 10, 5)`: 10 / 5 = 2, which is 2 x 10^0, so the ticks are 0, 2, 4, 6, 8, 10.
- `ticks(0, 1, 10)`: 1 / 10 = 0.1, so the ticks are the exact decimals 0, 0.1, ..., 1.
- `ticks(1, 10, 10)`: 9 / 10 = 0.9 has error 9, which is at least the square root of 50, so the step is 10 x 10^-1 = 1.
- `ticks(-1, 1, 4)`: 2 / 4 = 0.5 has error 5, in the range from the square root of 10 to the square root of 50, so the
  factor is 5 and the step 5 x 10^-1, giving -1, -0.5, 0, 0.5, 1.
- `ticks(0, 1e6, 4)`: 250,000 has error 2.5, in the range from the square root of 2 to the square root of 10, so the
  step is 2 x 10^5.
- `ticks(0, 1000, 7)`: 142.86 has error 1.43, which is at least the square root of 2, so the step is 2 x 10^2.
- A reversed range (`ticks(10, 0, 5)`) gives the same ticks in descending order, and a range of one value gives that
  value; a count that is not positive or not a number gives no ticks.

The `nice` vectors extend a domain to round boundaries (for example 0.5 to 9.7 has step 1 and becomes 0 to 10, 1 to 99
has step 10 and becomes 0 to 100, -3.2 to 4.1 has step 1 and becomes -4 to 5, 10 to 35 has step 2 and becomes 10 to
36), keep the direction of a reversed domain, and leave degenerate domains alone. The tick count vectors follow the
Vega-Lite rule of one tick per 40 pixels rounded up (a 300 pixel axis asks for 8 ticks and a 200 pixel axis for 5) and
the Vega reduction of the count so that the step is not below a minimum step (0 to 3 with a minimum step of 1 can hold
only 3 + 1 = 4 ticks).

The unit has no dependencies other than `VegaLiteLimits` and `VegaLiteRenderException`, so no mocking or stubbing is
required; the internal type is reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `TicksTests.cs` and `TicksTimeTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests`
project, in the `Scales` folder.

The time ticks (`TimeTickInterval`, `TimeTicks`, `TimeNice`, and the `TimeTickRequest` of an explicit interval) are
verified in `TicksTimeTests` with ticks that are listed by hand from the calendar (the instants of the expectations are
computed with the base class library, not with the calendar under test) and with the d3 anchors of the interval table.
The rule is d3's `tickInterval`: the target is `|stop - start| / count`, the table entry just below the target is used when
the target over it is below the next entry over the target, and otherwise the next entry (the ratio rule), the stop is
inclusive, and whole milliseconds below a second and whole years beyond a year use the numeric `tickStep`. The anchors
derived in the tests are:

- January 1 to January 11, 2024 with count 10 has a target of 10 days / 10 = 1 day, so the day, and the eleven midnights
  January 1 to 11 (the stop is inclusive).
- One year (January 1, 2024 to January 1, 2025, 366 days because 2024 is a leap year) with count 10 has a target of
  36.6 days, between the month (30 days) and the quarter (90 days): 36.6 / 30 = 1.22 is below 90 / 36.6 = 2.46, so the
  month, with the 13 boundaries January 1, 2024 to January 1, 2025.
- 0 to 1000 ms with count 10 has a target of 100 ms, below a second, so `tickStep` of whole milliseconds gives every 100
  ms, 11 ticks (0, 100, ..., 1000); 2000 to 2100 with count 10 has a target of 10 years, beyond the year entry, so
  whole years by `tickStep` (10), 11 ticks.
- The minute entries by the ratio rule: 30 minutes / 10 = 3 minutes is nearer to 5 (3 / 1 = 3 is not below 5 / 3 = 1.67),
  so 5 minutes and 7 ticks; 100 minutes / 10 = 10 minutes: 10 / 5 = 2 is not below 15 / 10 = 1.5, so 15 minutes and 7
  ticks; 50 minutes / 10 = 5 minutes is the entry itself and 11 ticks. 42 days / 6 = 7 days is the week, with the
  Sundays January 7, 14, 21, 28, and February 4 and 11 (6 ticks); 20 days / 10 = 2 days.
- `nice` on January 10 13:00 to March 20 05:00, 2024 (31 + 29 = 60 days to March 10 plus 9 days 16 hours is 69.667
  days) with count 10 has a target of 6.97 days, nearer the week than two days, so the domain extends to the Sundays
  January 7 and March 24 (plan section 6 gives the target as 6.97 days; the result is the week).
- The two-day interval of a UTC scale counts the days since the epoch (d3's `unixDay`): January 1, 2024 is day 19,723
  (54 x 365 = 19,710 days plus 13 leap days, 1972 to 2020), odd, so the two-day ticks of a UTC scale are the even days
  January 2, 4, ..., 20, while a local time scale counts the day of the month (January 1, 3, ..., 21). Likewise the
  every-second-week ticks count the weeks since Sunday December 28, 1969: January 7, 2024 is 2,819 weeks after it
  (19,733 days; odd) and January 14 is 2,820 weeks (19,740 days), so the even weeks are January 14, 28, February 11,
  25, March 10, and 24.
- In a daylight saving zone (standard time minus five hours, daylight time from 2024-03-10T07:00Z) month ticks are local
  midnights whose instants shift by an hour (March 1 at 05:00Z, April 1 at 04:00Z) and the day of March 10, from
  05:00Z to the next 04:00Z, is exactly 23 hours.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions of numbers; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any tick value, step, nice boundary, or count that differs from the hand-derived expectation (compared
exactly, so that fractional steps must come out as the exact decimals) constitutes a failure.

#### Unit-Level Test Scenarios

##### Tick Values Match The Hand-Derived Vectors

**Tests**: `TicksTests.Ticks_Ticks_LinearRangeMatchesHandDerivedExpectedVector`

Asserts, for twenty-one vectors, the exact tick lists for whole steps, fractional steps, reversed ranges, negative
ranges, ranges around zero, very large and very small spans, a count of 1, degenerate ranges (including a fractional
count of 0.2), a count below 2 that would leave no tick (0.1 to 0.9 with a count of 0.5 gives the single tick 0.5), and
invalid counts (zero, negative, NaN) and a NaN start, which give no ticks.

##### Tick Limit Fails Closed

**Tests**: `TicksTests.Ticks_Ticks_ResultOverTheLimitFailsClosed`

Asserts that 0 to 100 with a count of 50 (51 ticks at step 2) throws `VegaLiteRenderException` for a limit of 5 and
succeeds for a limit of 51, and that an absurd request (0 to 1e12 with a count of 1e9) throws instead of allocating.

##### Overflowing Span Has No Non-Finite Ticks

**Tests**: `TicksTests.Ticks_Ticks_OverflowingSpanHasNoNonFiniteTicks`

Asserts that `GetTicks` and `AxisTicks` return no ticks, rather than a NaN tick, for the spans -1e308 to 1e308 (the span
is infinite as a double), 1e308 to -1e308, negative infinity to 10, 0 to positive infinity, and positive infinity to
itself, and that -1e307 to 1e307 (the span is finite) still gives ticks that are all finite.

##### Tick Step And Increment

**Tests**: `TicksTests.Ticks_TickStep_UsesHandDerivedExpectedStep`

Asserts the eleven hand-derived steps (20, 2, 0.1, 0.2, 0.5 for 0 to 1 with count 3 because 3.33 is above the square
root of 10, 200, 100, 0.0002, 0.5 for 0 to 7 with count 10, and the negative steps -2 and -0.1 of reversed ranges) and
that the increment is the step itself from 1 up and the negated reciprocal below 1 (`-10` for 0.1, `-5` for 0.2),
with NaN for a NaN input.

##### Nice Domains

**Tests**: `TicksTests.Ticks_Nice_ExpandsDomainToExpectedBoundaries`

Asserts the thirteen hand-derived nice domains listed above, that a domain that is already round is unchanged, that
a reversed domain keeps its direction (9.7 to 0.5 becomes 10 to 0), that degenerate domains are unchanged, that a NaN
count leaves the domain alone, and that the default count is 10.

##### Vega-Lite Tick Count And Minimum Step

**Tests**: `TicksTests.Ticks_DefaultAxisTickCountAndMinStep_FollowVegaLite`,
`TicksTests.Ticks_ResolveTickCount_ReducesCountToRespectTheMinimumStep`

Asserts that the default tick count is the size divided by 40 rounded up (300 gives 8, 200 gives 5, 20 gives 1, 41
gives 2, 0 gives 0), that only the integer format `d` has a minimum step (of 1), that the count is kept without a
minimum step, kept when the step already meets it, reduced to 4 for the domain 0 to 3, reduced to 1 for the domain 0
to 1 (two ticks would give a step of 0.5), the same for a reversed domain, and 2 for an empty span; and that the ticks
of an integer axis never have a fractional step (0 to 5 gives 0 through 5, 0 to 1 gives 0 and 1, and a 200 pixel axis
over 0 to 100 gives 0, 20, 40, 60, 80, 100).

##### Log Ticks Match The Hand-Derived d3 Vectors

**Tests**: `TicksTests.Ticks_LogTicks_MatchHandDerivedD3Vectors`,
`TicksTests.Ticks_ResolveTickCount_LogarithmicScaleDoesNotLowerTheCountByTheStep`,
`TicksTests.Ticks_ModelTicks_DispatchesByTheTransform`

Asserts the log ticks derived by hand from `scaleLog.ticks`: three decades with a count of 10 give the 28 multiples
1 to 9,
10 to 90, 100 to 900 and 1000; 0.01 to 1 gives 19 ticks whose decimals are exact (3 / 100 is 0.03); 2 to 30 starts at 2
(the multiples below 2 are skipped); twelve decades with a count of 5 give the powers 1, 1e2, ..., 1e12 (the d3
ticks of 0 to
12, step 2); 2 to 8 with a count of 20 has 7 multiples, fewer than half of the count, so the ordinary ticks 2, 2.2,
..., 8 are
used (31); a descending domain gives the same ticks reversed; -1000 to -1 gives the 28 reflected ticks in ascending order;
base 2 gives 1, 2, 4, 8, 16; a base of 2.5 gives the powers 2.5^0 to 2.5^7 of the ticks 0 to 7; and domains the
logarithm is
not defined on (zero, both signs, an infinite end) and bases that are not usable (1, 0, negative) give no ticks. Asserts
that a minimum step of 25 on 1 to 100 lowers the count of a linear scale to 3 (step 50) while a log scale keeps the 4 that
fit, and that the ticks of a scale model follow its transform (log ticks for a log scale, the ordinary ticks of the domain
for pow and symlog).

##### Log Ticks Respect The Limits

**Tests**: `TicksTests.Ticks_LogTicks_LimitsAndCancellationAreEnforced`

Asserts that 28 ticks against a limit of 5 and a base of 1e9 whose multiples below the domain are 5e8 (the work
budget of a
hundred times the tick limit) throw `VegaLiteRenderException` whose message contains `the limit of`, that a canceled token
stops the same walk with `OperationCanceledException` (polled every 4,096 multiples), and that the ticks of the exponents
over 1e-300 to 1e300 keep the tick limit too.

##### Log Nice

**Tests**: `TicksTests.Ticks_LogNice_ExtendsToPowersOfTheBase`

Asserts the hand-derived domains (3 to 800 becomes 1 to 1000, 0.02 to 5 becomes 0.01 to 10, 800 to 3 keeps its direction,
-800 to -3 becomes -1000 to -1, 3 to 100 in base 2 becomes 2 to 128, 5 to 5 becomes 1 to 10) and that a domain with
zero, both
signs, or the base 1 is unchanged.

##### Log Labels

**Tests**: `TicksTests.Ticks_LogLabelTest_LabelsTheLeadingDigitsThatFit`

Asserts the label rule for 28 ticks and a count of 10 (k = 3.57: the digits 1, 2, and 3 of every decade are
labeled, 4 to 9
are not, a fraction is read after its exponent so 0.5 is a 5), for a count of 3 (k = 1.07: only the powers), for 5 ticks
(every digit), and that the magnitude decides for negative ticks while zero is not labeled.

##### Time Tick Intervals Follow The d3 Table

**Tests**: `TicksTimeTests.Ticks_TimeTickInterval_FollowsD3Table`

Asserts, for nine rows (1 day, 1 month, 100 ms, 10 years, 5 minutes by the ratio rule, 15 minutes by the ratio rule, 5
minutes exactly, 1 week, and 2 days), the interval kind and the number of ticks derived above (11, 13, 11, 11, 7, 7,
11, 6, and 11), and that no count, a negative count, a NaN count, or a NaN start gives no interval and a null
calendar is `ArgumentNullException`.

##### Time Ticks Are Inclusive, Reversible, And Calendar Aware

**Tests**: `TicksTimeTests.Ticks_TimeTicks_InclusiveStop_Utc`, `TicksTimeTests.Ticks_TimeTicks_Reversed`,
`TicksTimeTests.Ticks_TimeTicks_TwoDays_UtcScaleCountsFromTheEpoch`,
`TicksTimeTests.Ticks_TimeTicks_Month_Boundaries_InAZone`

Assert the eleven daily midnights of January 1 to 11 (the stop included) and the same ticks in descending order for a
reversed domain; that the two-day ticks of a UTC scale are January 2, 4, ..., 20 (even epoch days) and those of a time
scale January 1, 3, ..., 21; and, in the daylight saving zone, the month ticks February 1 05:00Z, March 1 05:00Z, April
1 04:00Z, May 1 04:00Z, and June 1 04:00Z (121 days less an hour over five ticks is about 24.2 days each, nearer the
month than the week) and the six local midnights of March 8 to March 13 (target 23.8 hours, nearer 24 than 12) with
the day of March 10 lasting 23 hours.

##### Explicit Intervals And Requests

**Tests**: `TicksTimeTests.Ticks_TimeTicks_ExplicitInterval_Week_Step2`,
`TicksTimeTests.Ticks_TimeTickRequest_TryFromInterval_ReadsNames`

Assert that every second week gives January 14, 28, February 11, 25, March 10, and 24, that the quarter gives January,
April, July, and October 1, 2024 and January 1, 2025, that `utcmonth` in a +05:30 zone gives the UTC month starts
while `month` gives local ones (18:30Z of the day before; the one of January 1 lies before the domain), and that the
request parser reads each interval name of the union (case-insensitive, with the aliases `hours` and `date`), keeps a
step, turns a quarter into three months (times the step), reads the `utc` prefix, rejects an unknown name, and
rejects a null text; the default request has the count 10.

##### Degenerate Time Requests And Domains

**Tests**: `TicksTimeTests.Ticks_TimeTicks_DegenerateRequestsAndDomains`

Asserts that a step below one or negative (d3's `every` is null) falls back to the default of about ten ticks as Vega
does (four days over ten ticks is 9.6 hours, nearer 12 hours than 6, so every 12 hours and nine ticks), that a count
that is not positive gives no ticks, that a domain of one instant gives that instant, and that a NaN domain gives none.

##### Time Ticks Fail Closed And Poll Cancellation

**Tests**: `TicksTimeTests.Ticks_TimeTicks_MaxTicksExceeded_Throws`,
`TicksTimeTests.Ticks_TimeTicks_PollCancellation`

Assert that milliseconds over a year and a count of 1e9 against a limit of 1000 throw `VegaLiteRenderException` whose
message names the limit of 1000 and `MaxTicks` (with no location, which the axis builder adds), that exactly 11 ticks
are accepted by a limit of 11 and 10 fails, that a model that is not a time scale is `ArgumentException` and a null
one `ArgumentNullException`, and that a canceled token stops the generation with `OperationCanceledException`.

##### Time Nice Boundaries

**Tests**: `TicksTimeTests.Ticks_TimeNice_WeekExample`, `TicksTimeTests.Ticks_TimeNice_ExplicitMonth`,
`TicksTimeTests.Ticks_TimeNice_InAZone`

Assert the week example derived above (January 7 to March 24, 6.97 days per tick), that a reversed domain keeps its
direction and a domain already on the boundaries is unchanged; that the month interval gives January 1 to April 1, the
quarter step gives January 1 to July 1 for February 10 to May 20 (floored to the months 0, 3, 6, 9 and ceiled to July
1), the count 2 (34.8 days) the month, that a count of zero and a boundary beyond the supported years (9999-12-31T12:00Z)
leave the domain unchanged, and that a null calendar is `ArgumentNullException`; and that in the daylight saving zone
the domain of March 15 12:00Z to April 20 12:00Z extends to March 1 00:00 EST (05:00Z) and May 1 00:00 EDT (04:00Z).

##### Tick Count For A Time Scale

**Tests**: `TicksTimeTests.Ticks_ResolveTickCount_Temporal_SkipsTickStepLoop`,
`TicksTimeTests.Ticks_ResolveTickCount_MinStep_CapsCount`

Assert that a temporal scale caps its count by the minimum step only (91 days / 31 days = 2.935, floor 2, plus one is
3) where a numeric scale would go on to 2, and that without a minimum step the count 8 is kept; and the year-month
example of Vega-Lite: the 300 pixel default count of 8 over January 1 to April 1 with a minimum step of 31 days drops to
3, which targets 30.3 days, the month, with the ticks January 1, February 1, March 1, and April 1.

##### Time Ticks Between Two Instants

**Tests**: `TicksTimeTests.Ticks_TimeTicks_InstantsOverload_MatchesTheModelOverload`

Asserts that the overload over two instants (used for the domain of a temporal color, which is not a time scale model)
gives the ten local midnights of January 2 to 11 in a +05:30 zone (18:30Z of the day before) exactly as the model
overload does, in descending order for a reversed domain, every second Sunday (January 14 and 28, 2,820 and 2,822
weeks from the epoch week) for a week step of 2, and that a millisecond interval over a limit of 5, a canceled token,
and a null calendar throw `VegaLiteRenderException`, `OperationCanceledException`, and `ArgumentNullException`.

##### Time Ticks Are Culture-Independent

**Tests**: `TicksTimeTests.Ticks_Time_Culture_Invariant`

Asserts that the ticks of January 1 to March 31 with a count of 8 and the nice boundaries of the week example are the
same under the Turkish and German cultures as under the default.
