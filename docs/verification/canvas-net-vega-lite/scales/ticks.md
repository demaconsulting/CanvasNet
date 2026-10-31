### Ticks Unit Verification Design

This document describes the unit-level verification strategy for the `Ticks` unit in the
`DemaConsulting.CanvasNet.VegaLite.Scales` namespace: the internal static `Ticks` class (`GetTicks`, `TickIncrement`,
`TickStep`, `Nice`, `DefaultAxisTickCount`, `DefaultAxisTickMinStep`, `ResolveTickCount`, and `AxisTicks`). It is an
own C# implementation of the d3-array tick algorithms, the d3-scale linear `nice`, and the Vega and Vega-Lite tick
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

Unit tests reside in `TicksTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Scales`
folder.

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
