### ScaleModel Unit Verification Design

This document describes the unit-level verification strategy for the `ScaleModel` unit in the
`DemaConsulting.CanvasNet.VegaLite.Scales` namespace: the internal immutable `ScaleModel` class (the factories
`CreateLinear`, `CreateBand`, `CreatePoint`, `CreateOrdinal`, `CreateSequential`, `CreateQuantize`, `CreateQuantile`,
`CreateThreshold`, `CreateBinOrdinal`, and `CreateTime`, the mapping methods `Map`, `MapCenter`, `MapOrdinal`,
`MapText`, `MapColor`, `MapIndex`, and `Invert`, `IndexOf`, and the static `BandSpace`), the `ScaleRange` struct, and
the `ScaleKind` enumeration (with its `Time` and `Utc` kinds). The
arithmetic is an own C# implementation of the d3-scale linear, time, band, point, ordinal, sequential, diverging,
quantize, quantile, and
threshold scales, of Vega's bin-ordinal scale, and of the Vega `bandspace` rule.

#### Verification Approach

The unit is verified by building scales from explicit domains and ranges and asserting the mapped positions against
values computed by hand from the d3-scale formulas. The expected numbers are small enough to check by eye, and the
tests state the arithmetic next to them:

- Linear 0 to 10 onto 0 to 100 is a factor of ten (5 maps to 50, 12 extrapolates to 120, and 75 inverts to 7.5); a
  clamped scale stops at the ends of the domain; a y scale from 200 down to 0 maps 2.5 to 150; a reversed domain maps
  10 to the start of the range; a domain of zero width maps to the middle of the range; rounding turns 3.33 into 3 and
  6.67 into 7.
- Three bands without padding split 300 into thirds: step 100, bandwidth 100, starts 0, 100, 200, centers 50, 150,
  250. With Vega-Lite's bar padding (inner 0.1, outer 0.05) the space is 3 - 0.1 + 2 x 0.05 = 3 steps, so the step is
  100, the bandwidth is 90, and the first band starts at (300 - 100 x 2.9) / 2 = 5, giving 5, 105, 205. A reversed range
  lays the bands out from the other end (205, 105, 5). Rounding floors the step (100 / 3 = 33.3 becomes 33) and rounds
  the start ((100 - 99) / 2 = 0.5 becomes 1, half up), giving 1, 34, 67; alignment 0 keeps the leftover pixel at the
  end and alignment 1 at the start.
- Three points with half a step of padding sit at (i + 0.5) x 100 / 3: 16.67, 50, 83.33; points without padding reach
  both ends (0, 50, 100); a single point is centered.
- The Vega band space is the count minus the inner padding plus twice the outer padding, at least 1, and zero without
  categories: `BandSpace(3, 0.1, 0.05)` is 3, `BandSpace(4, 1, 0.5)` is 4, `BandSpace(1, 1, 0)` is 1, and
  `BandSpace(0, ...)` is 0.
- The ordinal scale cycles through its range: five categories over a range of two colors give red, blue, red, blue,
  red.
- A time scale is the same linear arithmetic over milliseconds since the epoch (`ScaleModelTimeTests`). The domain of
  the tests is January 1 to January 11, 2024 (ten days) over 0 to 400 pixels, so a day is 40 pixels, a quarter of a day
  (six hours, 21,600,000 ms) is 10 pixels, and January 6 (five days) is 200. The instants are computed with the base
  class library, never with the calendar under test, and the expected positions are worked out by hand: December 25 is
  seven days before the start, 7 x 40 = 280 pixels before it (-280), and January 12 is 440. The inverse of 200 is
  January 6 and of 40 is one day after the start; the inverse returns the number of milliseconds, not a date.

The calendar of the ticks of a time scale (the interval arithmetic and the zone offsets) is verified with the calendar
unit (see _TimeCalendar Unit Verification Design_); the scale model only stores it.

The unit has no dependencies other than the data value type of the `Data` namespace (and the color type of the
interpolator unit, which a
test passes as a probe function), so no mocking or stubbing is
required; the internal type is reached through the test assembly's `InternalsVisibleTo` access. The unit's use by the
resolver and the axis and mark builders is verified at the subsystem tier (see _Scales Subsystem Verification
Design_).

Unit tests reside in `ScaleModelTests.cs` and `ScaleModelTimeTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests`
project, in the `Scales` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions of numbers and categories; positions that are fractions are compared to nine decimals

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any position, step, bandwidth, inverse, or color that differs from the hand-derived value (beyond nine
decimals for fractions), or a wrong exception type for an operation of the wrong scale kind, constitutes a failure.

#### Unit-Level Test Scenarios

##### Linear, Band, And Point Positions Match The Derived Values

**Tests**: `ScaleModelTests.ScaleModel_Map_LinearBandAndPointMatchAnalyticPositions`

Asserts all the derived positions above, and in addition that a numeric text maps like its number (`"2.5"` maps to 25)
and a non-numeric text maps to nothing, that NaN stays NaN, that unknown categories map to nothing and have index -1,
that duplicate categories are ignored and a number is distinct from its text (`1` and `"1"` are two categories), that
an empty domain maps nothing, and that the center of a band is its start plus half its bandwidth while the center of a
point is the point.

##### Piecewise Linear Scales Follow d3

**Tests**: `ScaleModelTests.ScaleModel_Map_PiecewiseLinearFollowsD3Polymap`,
`ScaleModelTests.ScaleModel_Map_PiecewiseTruncatesToTheShorterOfDomainAndRange`,
`ScaleModelTests.ScaleModel_CreateLinear_PiecewiseEdgeCases`

The expectations are derived by hand from d3-scale's `continuous.js` (`polymap`, `bimap`, `bisectRight`, `normalize`,
`clamper`). Domain 0, 10, 100 onto range 0, 50, 100 maps 5 to 25 (halfway through the first segment), 10 to 50
(`bisectRight` sends an equal value into the next segment), 55 to 75, and extrapolates -10 to -50 and 200 to 1400 / 9
(the second segment continues, t = 190 / 90); the inverse maps 25 to 5, 75 to 55, and 50 to 10; clamping limits to the
first and last domain entry (-10 gives 0, 200 gives 100). A descending domain 100, 10, 0 reverses both arrays (5 gives
75, 55 gives 25), and a descending range gives the same segments. A domain of three entries over a range of two uses
only the first two domain entries (bimap: 5 gives 100, 100 gives 2000, clamp stops at the second entry, 10) while the
complete domain still reports its end (100); two domain entries over three range entries use the first two range
entries; four domain entries over three range entries use polymap over the first three of each (100 gives 2, 1000 gives
12). The case that d3 handles in a surprising way is kept: a descending domain 10, 5, 0, -1 over the range 0, 1, 2
reverses the complete arrays (-1, 0, 5, 10 and 2, 1, 0) before the first three are used, so -1 gives 2, 0 gives 1, 5
gives 0, and 10 gives -1. A segment of zero width maps to the middle of its output segment (-1 gives 25, 0 gives 50, 5
gives 75 for domain 0, 0, 10 onto 0, 50, 100), rounding applies to the result, and a domain or range with fewer than two
entries, or a null one, is rejected.

##### Ordinal Mapping Cycles Through The Range

**Tests**: `ScaleModelTests.ScaleModel_MapOrdinal_CyclesThroughTheRange`,
`ScaleModelTests.ScaleModel_Range_HoldsTextsNumbersOrACount`

Asserts the cycling derived above, that an unknown category maps to nothing, that an empty range maps to nothing, and
that the kind and the range are reported; and that a `ScaleRange` copies its texts, numbers, or count and answers by
index,
and that an ordinal scale with numbers cycles through them and one with only a count gives the index.

##### Sequential Scales Follow d3

**Tests**: `ScaleModelTests.ScaleModel_Sequential_MapsThroughTheInterpolator`

With a probe interpolator that returns 100 times the position as the red channel, the domain 0 to 10 maps 5 to 50, 10
to
100, and extrapolates 20 to 200 and -10 to -100 unless clamped (then 100 and 0); the domain 10 to 20 maps 15 to 50; a
reversed
domain 10 to 0 maps 10 to 0 and 2.5 to 75; a domain of zero width gives 50 for every value, clamped or not; NaN and
text that is not a number have no
color; a black-to-white ramp gives `#808080` in the middle (127.5 rounds half away from zero to 128).

##### Diverging Scales Use Two Slopes

**Tests**: `ScaleModelTests.ScaleModel_Diverging_UsesTheTwoSlopes`

The domain 0, 50, 100 maps 25 to 25 and 75 to 75; the asymmetric 0, 10, 100 gives 0.05 per unit below the middle (5 maps
to 25, -5 to
-25) and 0.5 / 90 above it (55 maps to 75); the descending 100, 50, 0 maps 100 to 0, 0 to 100, and 75 to 25; equal first
entries (5, 5, 15) give 50 below the
middle and 75 at 10; clamping limits 200 (which maps to 200) to 100.

##### Quantize, Quantile, And Threshold Scales Classify By Bisect

**Tests**: `ScaleModelTests.ScaleModel_Quantize_ThresholdsAreEqualWidth`,
`ScaleModelTests.ScaleModel_Quantile_UsesR7Thresholds`,
`ScaleModelTests.ScaleModel_Threshold_Bisects`

Quantize over 0 to 10 with four values has the thresholds 2.5, 5, and 7.5 (a value on a threshold takes the value
above it; -5 and 15 take the
first and last); over 100 to 200 with two values the threshold 150; one value has none and an empty range maps nothing.
Quantile of 1 to 8 with four
groups has the R-7 quartiles 1 + 7 x 0.25 = 2.75, 4.5, and 6.25; of 0, 10, 10, 10, 100 with five groups the quintiles
8, 10, 10, and 28. Threshold
at 10 and 20 with three values maps 5 to the first, 10 and 15 to the second, 20 and 25 to the third; a range of two
values uses only the first of three
thresholds.

##### Bin-Ordinal Scales Follow Vega

**Tests**: `ScaleModelTests.ScaleModel_BinOrdinal_MapsByBinIndex`

With the edges 0, 10, 20, 30 and three values, 0 and 5 map to the first, 10 and 19.5 to the second, 20 and 29 to the
third; -1 has no value; 30 and 35 map
to index 3 modulo 3, the first value (Vega's behavior for a value at or above the last edge); a range of two values cycles.

##### Band Space

**Tests**: `ScaleModelTests.ScaleModel_BandSpace_MatchesVega`

Asserts the five derived band spaces above, including the five bands without padding that need exactly 5 steps.

##### Wrong Kind Of Scale

**Tests**: `ScaleModelTests.ScaleModel_WrongKind_Throws`

Asserts that numeric mapping and inversion of a band scale, an ordinal mapping and a color mapping of a linear scale throw
`InvalidOperationException`, that a sequential scale needs two or three domain entries and an interpolator, that the
center of an unknown
category on a linear scale is nothing, and that the kinds of a band and a point scale are reported.

##### Log, Pow, Sqrt, And Symlog Transforms

**Tests**: `ScaleModelTests.ScaleModel_Transform_LogMapsDecadesEvenly`,
`ScaleModelTests.ScaleModel_Transform_PowAndSqrtFollowTheSignedPower`,
`ScaleModelTests.ScaleModel_Transform_SymlogIsSymmetricAndAccurateNearZero`,
`ScaleModelTests.ScaleModel_Transform_SequentialAndDivergingUseTheTransformedPosition`,
`ScaleModelTests.ScaleModel_Fraction_FollowsTheTransformedDomain`

Assert the positions derived by hand from the d3 formulas: 1 to 1000 over 300 pixels puts the powers of ten at 0,
100, 200, and
300 (the geometric mean at 150, 0.1 at -100), the inverse gives the exponential, clamping happens in the domain,
a number that
is not above zero is NaN (null for a data value), -1000 to -1 is reflected (-10 at 200), a descending domain runs backwards;
the square of 0 to 10 puts 5 at 25 and -2 at -4, the square root of 0 to 100 puts 25 at 5 of 10 and the signed root
of -100 to
100 puts -25 at 50 of 200, the cube puts 1 at 9 and -1 at 7 of 0 to 16, and the exponent 1 is no transform; symlog over
+-(e-1) puts 1 at 50 + 50 ln 2, the constant 10 puts 9 at ln(1.9) / ln(10), and a tiny number (1e-12) keeps its
digits to a part
in a billion (the accurate `log1p`); a sequential log scale over 1 to 100 puts 10 at the middle color and 1000 at 1.5, a
diverging log scale over 1, 10, 1000 puts 100 at 0.75, no color is given to zero or a negative number (and clamping
holds the
ends); and the fraction of a value runs along the transformed domain (a decade is a third of three decades, 25 is
half of the
square root domain 0 to 100, a domain without span is 0.5).

##### Time Scales Map Instants Linearly

**Tests**: `ScaleModelTimeTests.ScaleModel_Time_MapsDatesLinearly`,
`ScaleModelTimeTests.ScaleModel_Time_Invert_RoundTrips`

The ten day domain over 400 pixels maps January 6 to 200 whether it is given as a number of milliseconds, as a `Date`
value, or as a number value; the ends map to 0 and 400, six hours to 10, and a date outside the domain continues the
line (December 25 gives -280, January 12 gives 440). The kind is `Time` and the scale is linear-like. The inverse of 200
is January 6 and of 40 is January 2, the round trip `Invert(Map(ms))` returns each of four instants (the ends, an odd
12,345,678 ms after the start, and 17:00 on January 9), and the inverse of NaN is NaN.

##### Time Scales Clamp, Reverse, And Are Piecewise

**Tests**: `ScaleModelTimeTests.ScaleModel_Time_Clamp`, `ScaleModelTimeTests.ScaleModel_Time_Reverse`,
`ScaleModelTimeTests.ScaleModel_Time_Piecewise3Entries`

A clamped scale maps December 25 to 0 and February 1 to 400 and leaves January 6 at 200; the inverse of -50 and 450
are the two ends of the domain. A descending domain (January 11 to January 1) maps January 11 to 0, January 1 to 400,
and the middle to 200, and reports January 11 as its `DomainStart`; a descending range maps January 1 to 400 and
January 11 to 0, with the inverse of 200 at January 6. A three entry domain (January 1, 2, and 11) over the three
entry range 0, 100, 300 puts half a day at 50 (half of the first segment), January 2 at 100, and 4.5 days past it at
200 (half of the second segment, which spans nine days over 200 pixels), January 11 at 300; the domain start and end
are the first and last entries.

##### Time Scales Reject Invalid Values And Other Kinds' Operations

**Tests**: `ScaleModelTimeTests.ScaleModel_Time_InvalidDateHasNoPosition`

A null, a text that is not a number, and a NaN number have no position (null), NaN maps to NaN, and the ordinal and the
color mappings of a time scale throw `InvalidOperationException`.

##### UTC And Time Scales Do The Same Arithmetic

**Tests**: `ScaleModelTimeTests.ScaleModel_Time_Utc_And_Time_MapIdentically`

The time scale, the UTC scale, and a time scale with a fixed +05:30 zone calendar give exactly the same position for
the start, 07:30 on January 3, and the day after the end, because the calendar only matters to the ticks; the UTC
scale reports the `Utc` kind and is linear-like. With `round` the date January 2 of the domain January 1 to 4 over 0
to 10 maps to 3, and without it to 10 / 3 = 3.33.

##### Time Scales Store Their Calendar

**Tests**: `ScaleModelTimeTests.ScaleModel_Time_Calendar_IsStored`

The model returns the very calendar it was given (a US-like zone) and the UTC calendar for a UTC scale; a linear or a
band scale has no calendar, the linear scale is linear-like and the band scale is not; a null calendar or domain is
`ArgumentNullException`, and a domain or a range with fewer than two entries is `ArgumentException`.

##### Time Scales Report The Fraction Along The Domain

**Tests**: `ScaleModelTimeTests.ScaleModel_Time_Fraction_IsThePositionInTheDomain`

January 3 is two tenths of the way along the domain (0.2) and the two ends are 0 and 1; legends use the fraction for
their gradient stops.
