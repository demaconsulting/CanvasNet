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
