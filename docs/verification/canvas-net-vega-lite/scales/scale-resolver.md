### ScaleResolver Unit Verification Design

This document describes the unit-level verification strategy for the `ScaleResolver` unit in the
`DemaConsulting.CanvasNet.VegaLite.Scales` namespace: the internal static partial `ScaleResolver` class (`Resolve`,
`InferOrient`, and `ChannelIsDrawn` in `ScaleResolver.cs`, the channels, domains, and titles that follow the encoding
plan in `ScaleResolver.Plan.cs`, the discrete sort in `ScaleResolver.Sort.cs`, the colors and the quantile domain in
`ScaleResolver.Color.cs`, and the output ranges in `ScaleResolver.Range.cs`) and the `ResolvedScales`, `ResolvedScale`,
and `RangeEnd` types of its file. The resolver reads the view of a parsed specification, the `EncodingPlan` and the
tables that the implicit stage produced (the Raw and the Main table), and the resolved configuration, and infers for
each of the channels `x`, `y`, `theta`, `radius`, `color`, `fill`, `stroke`, `size`, `opacity`, `shape`, `strokeWidth`,
`strokeDash`, `fillOpacity`, `strokeOpacity`, and `angle` (the channels that the mark draws: `ChannelIsDrawn` is the
table of the marks that draw each channel) the scale kind, the domain, the range, and the paddings according to the
Vega-Lite rules for this release (linear, time, UTC, sequential, diverging, quantize, quantile, threshold, band, point,
and ordinal scales; the types `quantitative`, `temporal`, `nominal`, and `ordinal`), including the fields that
aggregates, bins, and stacks
derive, the domains of stacked, binned, ranged, and `unaggregated` channels, the titles, and the sort of a discrete
domain by another channel's aggregate.

#### Verification Approach

The unit is verified by parsing small, original specifications, loading their inline data with the real `SpecParser`,
`ConfigResolver`, and `DataLoader`, resolving the scales, and asserting on the resolved scales and on the models they
create for a data rectangle of a chosen size. The scenarios of the aggregating features resolve through the pipeline
overload that takes the plan and the data of the implicit stage (the `ResolveFull` helper of `ChartTestHelpers`, which
runs the explicit transforms, the real `EncodingPlan`, and the real `EncodingTransforms` first), and the older
convenience overload builds that plan itself. The collaborators are real (the unit's contract is how the specification,
the data, and the configuration combine into scales), and no mocking or stubbing is used; the internal types are reached
through the test assembly's `InternalsVisibleTo` access.

The expected values are derived by hand from the Vega-Lite rules and arithmetic, with the derivation in the tests:

- A bar chart with a nominal x, a quantitative y of 3 to 12, and a nominal color: the nominal x of a bar is a band, the
  quantitative y is linear and includes zero (bars), nice with a count of 10 has step 1 so the domain is 0 to 12 (6
  maps to pixel 100 and 12 to 0 in a 200 pixel range), and the range runs from the bottom up; the band scale of three
  categories in a step of 20 takes 20 x 3 = 60 pixels (band space 3 - 0.1 + 2 x 0.05 = 3 steps), so the first band
  starts at (60 - 20 x 2.9) / 2 = 1 and is 18 wide, giving starts 1, 21, 41.
- A scatter plot includes zero by the configuration; x of 10 to 35 is nice as 0 to 35 (step 5, so 35 maps to pixel 300
  of 300) and y of 3 to 12 as 0 to 12; 5 to 15 is nice as 0 to 16 (step 2, because 15 / 10 = 1.5 is above the square
  root of 2, so 10 maps to pixel 60 of 160); and with `zero: false`, 10 to 35 has step 2 so it is nice as 10 to 36
  while 3 to 12 stays 3 to 12 (step 1).
- Pixel padding widens the domain around its middle: 0 to 100 in 200 pixels with 10 pixels of padding leaves 180 pixels
  for 100 units, so the domain grows by 10 / 1.8 = 5.56 on each side (-5.56 to 105.56), and 0 lands on pixel 10 and 100
  on pixel 190; a vertical bar on a continuous x has the configured 5 pixels of padding and no zero, and the padded
  domain 0.89 to 3.11 is extended by nice to 0.8 to 3.2 (step 0.2 at 10 ticks).
- A point mark's size range is from 4 to (0.95 x 20)^2 = 361 with zero in the domain; opacity runs from 0.3 to 0.8
  over the data extent without zero (a value in the middle of the extent gets 0.55); bars and ticks get sizes from 2
  to the step minus 1, rules from 1 to 4; a step of 10 limits the largest point size to (0.95 x 10)^2 = 90.25 and the
  bar size to 9; three categories on a point size scale are spread as 4, 182.5, and 361.
- Step-driven discrete sizes: three bars take 20 x (3 - 0.1 + 0.1) = 60 pixels, a specification step of 30 gives 90,
  four points with half a step of padding take 20 x 4 = 80, five bars on y take 20 x 5 = 100, and the configured step
  of 25 gives 25 x 3 = 75; a fixed width, an explicit range, a continuous scale, and a `container` size are not
  step-driven.
- Domains of the aggregating features: stacked layers have the extent of their starts and ends (and `[0, 1]` for
  `normalize`); a range `x` of 1 to 5 with `x2` of 3 to 9 has the domain 1 to 9; pre-binned starts 0 and 10 with ends
  10 and 20 have the domain 0 to 20 with no zero and no nice; and the means 2 (of 1 and 3) and 8 (of 4 and 12) per
  category have the domain 2 to 8, while the `unaggregated` domain reads the rows before the aggregate, 1 to 12.
- Sort of the categories `a` (weights 2 and 3), `b` (10), and `c` (1): by the sum 5, 10, 1 the order is `c, a, b`; by
  the minimum 2, 10, 1 it is `c, a, b`; by the third quartile (R-7: 2.75, 10, 1) and by the number of valid values (2,
  1, 1) the orders are `c, a, b` and `b, c, a` (ties in first appearance order); the sample variance of `a` is 0.5 and
  the categories with one value have a null variance, which sorts first in either direction.
- A pie chart (slices 1, 1, and 2 of the colors a, b, and c): the layers are stacked ascending by the color, so the
  slices are (0, 1), (1, 2), and (2, 4), the `theta` domain is 0 to 4 (the unstacked extent would be 1 to 2), zero is
  included and there is no `nice`, and the range is the whole turn, 0 to 2 pi, whatever extent the caller passes, so
  the values 0, 1, 2, and 4 map to 0, pi / 2, pi, and 2 pi (a quarter, a half, and the full turn of the total 4). A
  normalized stack has the unit domain 0 to 1, and an unaggregated field with nothing to split it is one stack of one
  slice per row (3 + 7 + 12 + 5 = 27, so 0 to 27).
- A radius over the values 3, 7, 12, and 5 on a data rectangle whose smaller half is 100: the domain includes zero (0 to
  12), the range is 0 to 100 (half of the smaller side), so 6 maps to 50 and 12 to 100, and on a half side of 60 the
  value 6 maps to 30. With the type `sqrt`, `zero: true`, and `rangeMin: 20` (the radial chart) the range is 20 to 100
  over the domain 0 to 12, and `v` maps to `20 + 80 x sqrt(v / 12)`: 0 to 20, 3 (a quarter of the maximum, whose square
  root is one half) to 20 + 80 x 0.5 = 60, and 12 to 100.
- Discrete polar scales spread their categories over the same ranges without padding: the three categories a, b, and c
  of an arc are bands of 2 pi / 3 starting at 0, 2 pi / 3, and 4 pi / 3, the same categories on a text mark are points at
  0, pi, and 2 pi, and the radius bands over 90 pixels are 30 wide so that c starts at 60.

Unit tests reside in `ScaleResolverTests.cs` (the scale kinds, domains, ranges, paddings, colors, and sizes),
`ScaleResolverPlanTests.cs` (the plan-driven fields, domains, titles, and sorts), `ScaleResolverTimeTests.cs` (the
scales of temporal fields and of fields with a time unit), `ScaleResolverPolarTests.cs` (the polar scales and the
orientation of the line, area, trail, and arc marks), and `MarkChannelGatingTests.cs` (the table of the marks that draw
each channel) within the
`DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Scales` folder; the sorts over rows made by a calculate
transform are in `RowTransformsTests.cs`, in the `Transforms` folder.

The temporal scenarios resolve through the same pipeline the render uses (the `Resolve` helper of
`ScaleResolverTimeTests` prepares the data stage in the calendar of a chosen zone, applies the explicit transforms and
the real `EncodingPlan` and `EncodingTransforms`, and resolves), so the time-unit starts and ends, the zone, and the
scale agree with the later stages. The instants are computed with the base class library (never with the calendar
under test) and the expected values are derived by hand:

- Dates January 1, 6, and 11, 2024 give the domain of ten days; over 400 pixels January 6 is five of ten days, pixel
  200, and a temporal y runs upward (January 1 at 300 and January 11 at 0 of 300). Pixel padding of 10 on 400 pixels
  leaves 380 for the span, so the domain grows by a factor of 400 / 380 about its middle: January 6 plus and minus
  5 x 400 / 380 days. A bar of a month unit over January, February, and March 2024 has the domain January 1 to April 1,
  the 31 + 29 + 31 = 91 days of the 2024 first quarter, so on 300 pixels February 1 (31 days in) is at 31 / 91 x
  300 = 102.198, derived by arithmetic and not copied from a run.
- The instants of 2024-01-10T13:00Z and 2024-03-20T05:00Z are 31 + 29 = 60 days from January 10 to March 10, plus 9 days
  16 hours, so 69.667 days apart; `nice: true` is the count 10 with a target of 6.97 days, which is nearer to the week
  (7 days) than to two days, so the domain becomes the Sundays January 7 and March 24, and the count 2 has a target of
  34.8 days, which is the month, so January 1 to April 1; the interval `month` gives the same, and the quarter step 3
  does too.
- In a +05:30 zone local midnight is 18:30Z of the day before: the month `nice` gives December 31 18:30Z to March 31
  18:30Z, the day of 2024-03-05T20:00Z (01:30 on March 6 local) starts at March 5 18:30Z, a date-time object at
  February 1 is January 31 18:30Z, and a time of day without an offset (12:00) is 06:30Z; a date alone and a `utc`
  object are UTC.
- The reference year of a single month unit is 2012, so the month number 2 and the name "Mar" are February 1 and March
  1, 2012, and 1,325,376,000,000 is January 1, 2012 (15,340 days x 86,400,000 ms, since 42 x 365 + 10 leap days is
  15,340 days from 1970).
- Opacity runs 0.3 to 0.8, so the middle date (January 6) is 0.55, and the angle 0 to 360 gives 180; January 1 and
  January 11 are the ends of any numeric range. Three dates in a limit of three are accepted and four fail.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test parses its own specification and data; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any wrong scale kind, domain, range, padding, step, discrete size, color, orientation, or wrong exception type
or location constitutes a failure.

#### Unit-Level Test Scenarios

##### Scale Kinds Are Inferred From Field Type, Channel, And Mark

**Tests**: `ScaleResolverTests.ScaleResolver_Resolve_InfersQuantitativeNominalOrdinal`

Asserts that the nominal x of a bar is a band, the quantitative y is linear, and the nominal color is ordinal; that a
discrete x is a band for `rect`, `bar`, `rule`, and `tick` and a point scale otherwise; that an ordinal field is
discrete like a nominal one while keeping its type for the color range; that size and opacity follow the field type
(linear for numbers, point for categories); that fill and stroke are separate color scales; that an explicit scale type
counts when the channel and the field type accept it and the default applies otherwise; that there is no scale for a
value definition, a datum-less definition, or `scale: null`, while a temporal x has a `Time` scale (the migrated
assertion of the former "a temporal field has no scale" scenario: the only scale of a specification with a temporal x
and three scale-less channels is `x`, of kind `Time` and data type `temporal`); that a count (an
aggregate without a field) is a field definition and so has a linear scale that reads `__count` and has the title
"Count of Records"; that a field without a type is read as quantitative and a datum takes the type of its value; and
that a view without a mark or encoding has no scales and no orientation.

##### Orientation Of Bars, Ticks, Rules, Lines, Areas, Trails, And Arcs

**Tests**: `ScaleResolverTests.ScaleResolver_InferOrient_FollowsVegaLite`,
`ScaleResolverPolarTests.ScaleResolver_InferOrient_AreaRangeAndTemporalRulesFollowVegaLite`,
`ScaleResolverPolarTests.ScaleResolver_InferOrient_LineFollowsTheMeasure`

Asserts, over a table of `(mark, specified orient, x type, y type, expected orient)` rows, that a bar extends along the
measure, a tick is the opposite, a rule with one position channel runs across the view, and marks without a direction
have none. For the marks of Phase 9 it asserts, over 27 rows, that an area with a `y2` (a field or a value) is vertical
and with an `x2` horizontal, that `y2` wins when both exist, and that both win over the orientation that the
specification gives; that a binned secondary position reverses the rule (an area on a `y` with `bin: "binned"` and a
`y2` is horizontal, on an `x` with a binned `x2` it is vertical, and the binned `y` with its `y2` is horizontal even
when the specification says vertical); that a time axis against an axis that is not temporal is the dimension of a
line, an area, and a tick (a temporal `x` with a nominal `y` is vertical, a temporal `y` with a nominal `x` is
horizontal, and a lone temporal axis decides alone), while two time axes, two categories, and no channel give no
orientation, a quantitative axis decides before the time axis does, and an explicit orientation wins over time; and
that a trail and an arc are always vertical, whatever the channels and the specified orientation. A second table of nine
rows asserts that a line extends along the dimension, so an unbinned quantitative `x` next to a `y` that is not
quantitative is horizontal and the mirror is vertical, two measures are vertical, a lone `x` measure is horizontal and a
lone `y` measure vertical, two discrete axes give none, a binned `x` with a counted `y` is vertical (a bin is not a
measure, a count is), and an explicit orientation wins; and the orientation reaches the scales (`Orient` of the resolved
scales is horizontal for a quantitative `x` over a nominal `y`, vertical for the mirror, and vertical for a time `x` of
a tick over categories, horizontal for the time `y`).

##### Polar Scales Cover The Turn And The Radius

**Tests**: `ScaleResolverPolarTests.ScaleResolver_Resolve_ThetaHasTheFullCircleRange`,
`ScaleResolverPolarTests.ScaleResolver_Resolve_RadiusRangeIsHalfTheSmallerSideWithRangeMin`,
`ScaleResolverPolarTests.ScaleResolver_Resolve_PolarDefaultsAreLinearZeroNoNice`,
`ScaleResolverPolarTests.ScaleResolver_Resolve_StackedThetaDomainSpansTheStack`,
`ScaleResolverPolarTests.ScaleResolver_Resolve_PolarChannelsAreOnlyResolvedForArcAndText`

Asserts, for the pie data above, that `theta` is a linear scale that is polar and not a position, located at
`/encoding/theta`, whose model for any extent (12,345 passed here) is the domain 0 to 4 over the range 0 to 2 pi, with
the values 0, 1, 2, and 4 at 0, pi / 2, pi, and 2 pi; that `reverse` gives the range 2 pi to 0, `rangeMin` 1 and
`rangeMax` 3
give 1 to 3, an explicit range `[0, 3]` replaces the default, and the keyword `"width"` stands for the extent that the
caller passes (0 to 40). For the radius it asserts the range 0 to the extent (0 to 100, so 6 maps to 50), the square-root
radial scale above (a linear scale with a power transform, 20 at zero, 60 at 3, and 100 at 12), the extent that follows
the data rectangle (30 for the value 6 over a half side of 60), and that a text mark has a radius scale too. It asserts
the defaults of a polar scale (linear, the domain 0 to 12 because zero is included, no `nice`, no padding, no step, no
natural extent, no stack for `stack: false`), that `zero: false` keeps 3 to 12, that an explicit domain `[2, 9.5]` keeps
its ends, that `config.scale.zero: false` turns the zero rule off for a radius, and the discrete forms (the bands and
points above). It asserts that the domain of a stacked channel spans the start and the end of the stack (0 to 4, with
different start and end fields), that a normalized stack is 0 to 1, that a stacked radius with `theta` as its dimension
stacks the layers a and b of the categories x and y to 3 and 1 (domain 0 to 3, where the unstacked extent would be 0 to
2) while the `theta` of that chart is a band without a stack, that an unaggregated field with no layers is the one stack
0 to 27, and that a text mark with `stack: true` stacks its `theta` the same way (0 to 4). Last, it asserts that `theta`
and `radius` are resolved only for the arc and text marks: ten other marks (point, bar, line, area, rect, tick, rule,
trail, circle, and square) give no scale for either, and an arc with a radius value, a quantitative `theta`, a color, and
an `x` datum has the scales `x`, `theta`, and `color` in that order.

##### Channels Are Resolved Only For The Marks That Draw Them

**Tests**: `MarkChannelGatingTests.ScaleResolver_ChannelIsDrawn_FollowsTheSupportTable`,
`MarkChannelGatingTests.ScaleResolver_Resolve_ChannelsThatTheMarkDoesNotDrawHaveNoScale`

Asserts, for each of the 13 marks (point, circle, square, rect, bar, tick, rule, line, area, trail, text, arc, and
image), the exact set of marks that draw a channel: `theta` and `radius` by text and arc, `theta2` and `radius2` by arc,
`text` by text, `size` by every mark but rect, area, arc, and image, `strokeDash` by every mark but rect, area, trail,
text, and arc, `angle` by point, circle, square, and text, `shape` by point, and the position and paint channels by
all; and, resolving a one-channel encoding of a small table, that a size on an area or an arc, a stroke dash on an area,
a trail, a text, or an arc, an angle on a line, and a shape on an arc have no scale while a size on a line, a trail, or
a text, a stroke dash on a line, and an angle on a text do.

##### Zero, Nice, Domain, Range, And Padding

**Tests**: `ScaleResolverTests.ScaleResolver_Resolve_AppliesZeroNiceDomainRangeAndPadding`

Asserts the derived domains above, that an explicit domain is used as given (no nice, and zero only when it already
spans zero) while an explicit `zero: true` overrides the rule, that `nice` may be a tick count or false, the pixel
padding example, that a horizontal bar has the measure on x with zero and no padding there, that a reversed scale or
`sort: "descending"` swaps the range ends, that an explicit range replaces the default and `"width"` stands for the
extent, that a datum has a one-value domain extended by zero to 0 to 5, that no data gives the default domain 0 to 1,
and that invalid values (null, non-numeric text) are ignored, numeric text is read, and booleans are 1 and 0.

##### Piecewise Domains And Ranges

**Tests**: `ScaleResolverTests.ScaleResolver_Resolve_PiecewiseDomainAndRangeFollowD3`

Asserts that an explicit domain 0, 50, 100 over the range 0, 200, 300 maps 25 to 100, 50 to 200, and 75 to 250 with no
nice; that the keyword `width` is an entry of the range (`[0, "width", 600]` over a width of 300 maps 50 to 300 and 75
to 450); that the same domain over the default two-entry range uses its first two entries (25 gives 150 and 100 gives
600 over 300 pixels); that `clamp` stops at the first and last entry (-10 gives 0, 500 gives 300); that `reverse` flips
the whole range (300, 200, 0: 0 gives 300, 25 gives 250); that `zero: true` changes only the first entry (5, 10, 15
becomes 0, 10, 15 and 5 gives 25 over 0, 50, 100); that `nice: 5` extends 0, 3, 97 to 0, 3, 100 (3 gives 100, 100 gives
200); that 10 pixels of padding over a range of 300 widen the end entries around their middle by 300 / 280 (to -25 / 7
and 725 / 7) and leave the middle entry at pixel 200; and that a band scale takes the first two entries of the range 0,
100, 500, giving a step of 100 / 3.

##### Band And Point Paddings

**Tests**: `ScaleResolverTests.ScaleResolver_Resolve_PaddingsFollowMarkAndScaleProperties`

Asserts that the paddings follow the mark (bar inner 0.1, tick inner 0.25, rect and rule 0, the outer padding half the
inner), that an explicit inner padding makes the outer half of it, that a point scale has half a step outside by default
(or `padding` or `paddingOuter`) and an inner padding of 1, that the configuration overrides the mark defaults, and that
alignment and rounding are passed on.

##### Discrete Domain Order

**Tests**: `ScaleResolverTests.ScaleResolver_Resolve_DiscreteDomainsFollowTheSortProperty`

Asserts, for the sample with categories b, a, c, a in data order, the ascending default, the data order, descending,
and an explicit array (values missing from the array come first, as Vega-Lite sorts by an index of -1 for them), that an
explicit scale domain is used as given including values the data lacks, that numbers sort numerically, numeric text
sorts as text, and null sorts first, and that a field the data does not have is one null category.

##### Explicit Sort Array First Index

**Tests**: `ScaleResolverTests.ScaleResolver_Resolve_ExplicitSortArrayUsesTheFirstIndexOfEachValue`

Asserts, for the same sample, that a value listed more than once counts at its first position (`["c","b","c","a"]`
gives c, b, a; `["b","c","b"]` gives a, b, c with the unlisted `a` first), that values of another kind in the array do
not position the text values, and that an array of 20,000 padding entries followed by `c`, `b`, `c` gives the same
first-index order (a, c, b). The test asserts the result only; the linear cost of the one-time index is by construction
(a dictionary of first positions) and no timing is measured.

##### Sort By An Aggregate Of A Field Of The Transformed Rows

**Tests**: `RowTransformsTests.ScaleResolver_Resolve_SortByFieldUsesTheTransformedRows`,
`RowTransformsTests.ScaleResolver_Resolve_SortByFieldIsStableForTies`,
`RowTransformsTests.ScaleResolver_Resolve_SortByFieldPutsCategoriesWithoutValuesFirstInBothDirections`

These tests are in `RowTransformsTests.cs` (folder `Transforms`) because the sorted table is the result of a
calculate transform; they use the real `SpecParser`, `DataLoader`, `RowTransforms`, `ConfigResolver`, and
`ScaleResolver`. Asserts, for the categories `a` (the weights 2 and 3 after the calculate), `b` (10), and `c` (1) in
the order of first appearance `a, b, c`, that a `sort` object whose `op` is `sum` of `w` gives `c, a, b` (the sums 1, 5,
and 10) and `b, a, c` with the order `descending`; that `count` gives `b, c, a` (the counts 1, 1, and 2, the tie kept in
the order of first appearance); that `max` and `median` give `c, a, b`, `min` with the order descending and `average`
with the order descending give `b, a, c`, `mean` gives `c, a, b`, and `valid` (the numbers of valid values 2, 1, and 1)
gives `b, c, a`; that the `encoding` form `{"encoding": "x"}` gives `a, b, c`; that categories with equal aggregates
keep their order of first appearance whichever the order (`z, m, a` for three equal sums, ascending and descending);
that a category without a valid value has a null aggregate that sorts first in both directions for `mean`, `average`,
`median`, `min`, and `max` (`m, z, a` ascending and `m, a, z` descending); and that `IsSupportedFieldSort` accepts
`{"op":"sum"}` and refuses an unknown order and the text form `"x"`.

##### Sort By Another Channel's Aggregate

**Tests**: `ScaleResolverPlanTests.ScaleResolver_Resolve_SortsByAnotherChannelsAggregate`,
`ScaleResolverPlanTests.ScaleResolver_Resolve_SortByAPlainChannelUsesMinOrSumWhenStacked`,
`ScaleResolverPlanTests.ScaleResolver_Resolve_SortsByAnOperationOfAField`,
`ScaleResolverPlanTests.ScaleResolver_Resolve_SortOfAnAggregatedViewReadsTheRawRows`,
`ScaleResolverPlanTests.ScaleResolver_Resolve_SortByAnAggregatedStackedChannelUsesTheRawSums`,
`ScaleResolverPlanTests.ScaleResolver_Resolve_UnresolvableSortsAndPlainFormsKeepTheirOrder`,
`ScaleResolverPlanTests.ScaleResolver_IsSupportedFieldSort_AcceptsOrderableOperations`

Asserts that the shorthands `y` and `-y` and the `encoding` object order the categories by the operation of the sort
channel's own aggregate over the Raw rows (the sums of `w` are `c` 1, `a` 5, `b` 10; the counts are `a` 2, `c` 1, `b` 1,
`c` before `b` by appearance); that a channel without an aggregate sorts by its `min` for a mark that does not stack
(`c` 1, `a` 2, `b` 10) and by its `sum` for the stacked channel of a bar (`c` 1, `a` 5, `b` 10), so descending gives
`b, a, c` for both; that every orderable operation of a sort object orders the categories, with the operation defaulting
to `min` and the field to the channel's own (the third quartile of `a` is 2.75, the sample variance of `a` is 0.5 and
null for the single-value categories, which sort first, the valid counts are 2, 1, 1, and the distinct counts are 2, 1,
1); that the sort reads the Raw rows when the table is aggregated (`a` has 100 + 100 and `b` has 1, and the aggregated
table has no field `w`) and, for an aggregated stacked channel, the Raw sums (`a` is 1 + 2 + 4 = 7, `b` is 9, so the
descending sort puts `b` first); that a shorthand naming a channel that is not in the encoding and an operation that
cannot order categories keep the ascending default and the plain forms (ascending, descending, `null`, and an array) are
unchanged; and that `IsSupportedFieldSort` accepts any orderable operation with a valid field and order and refuses
`encoding`, an invalid order, the text form, and a null argument.

##### Sort Array Is Bounded, Cancellable, And Scalar Only

**Tests**: `ScaleResolverPlanTests.ScaleResolver_Resolve_SortArrayIsBoundedAndPollsCancellation`,
`ScaleResolverPlanTests.ScaleResolver_Resolve_SortArrayNeverMapsNonScalarItemsToNull`

Asserts that, with `MaxScaleDomainSize` of 3, a sort array of four items is a `VegaLiteRenderException` located at
`/encoding/x/sort` whose message contains `the limit of 3` and `VegaLiteLimits.MaxScaleDomainSize`, that an array of
three items is accepted (`c, b, a`), and that a canceled token stops the read of a sort array (the data has no rows, so
only the sort can notice it); and, with a specification tree built by hand because the parser rejects such items, that
an object item and an array item of a sort array never stand for the null category: for the categories `a`, null, and
`b` and the sort `[{}, [], "a"]` the unlisted null and `b` come first in data order and `a` last (`null, b, a`; reading
the object as null would have given `b, null, a`).

##### Sort Cost Is Charged And Bounded

**Tests**: `ScaleResolverPlanTests.ScaleResolver_Resolve_SortIsChargedBoundedAndCancellable`

Asserts that the rows of a sort by an aggregate are charged to the step budget (a located failure at the channel that
names `MaxEvaluationSteps` under a budget of 40), that the categories are bounded by `VegaLiteLimits.MaxScaleDomainSize`
(a limit of 100 fails), that the same specification resolves under the defaults, and that a canceled token is polled.

##### Binned, Ranged, Stacked, And Unaggregated Domains

**Tests**: `ScaleResolverPlanTests.ScaleResolver_Resolve_OrdinalBinsSortByStartAndDescendingReverses`,
`ScaleResolverPlanTests.ScaleResolver_Resolve_OrdinalBinTextUsesTheAxisFormat`,
`ScaleResolverPlanTests.ScaleResolver_Resolve_RangeDomainIsTheUnionOfBothFields`,
`ScaleResolverPlanTests.ScaleResolver_Resolve_StackedDomainReachesTheTotalOfTheHighestStack`,
`ScaleResolverPlanTests.ScaleResolver_Resolve_StackedDomainIncludesNegativeLayers`,
`ScaleResolverPlanTests.ScaleResolver_Resolve_SignedStackDomainInBothOrientations`,
`ScaleResolverPlanTests.ScaleResolver_Resolve_NormalizedStackHasTheUnitDomain`,
`ScaleResolverPlanTests.ScaleResolver_Resolve_PreBinnedDomainIsTheUnionOfStartAndEnd`,
`ScaleResolverPlanTests.ScaleResolver_Resolve_BinnedScalesHaveNoNiceUnlessAsked`,
`ScaleResolverPlanTests.ScaleResolver_Resolve_UnaggregatedDomainReadsTheRawRows`

Asserts that the text categories of an ordinal bin (the boundaries joined with an en dash) are ordered by where the bin
starts and `descending` reverses them, and that the axis `format` (`.0%` for the bins 0 to 1 by 0.1) formats the
boundaries; the range domain 1 to 9 with the merged title; the stacked domains read straight from the resolved scale
(the layers 3, 2, 5 of north and 4, 1 of south give 0 to 10, the total of the highest stack; a layer of +3 and one of -4
stack upward and downward from zero and give -4 to 3; signed stacks in both orientations give -4 to 3 on a horizontal
measure, -5 to 0 for an all-negative stack, 0 to 1 for a normalized stack of mixed signs, and 0 to 4 for a centered one
(the absolute values 4 and 2 start at 0 and 1); a normalized stack gives 0 to 1, and a chart with one row per category
is not stacked and keeps its zero-rule domain 0 to 10); the pre-binned domain 0 to 20 with the scale marked as binned
and no computed bins; that a binned scale has no `nice` unless asked for and an explicit domain is used as given; and
that the `unaggregated` domain of a mean reads the Raw rows (1 to 12) while the normal domain reads the means (2 to 8)
and a sum has no unaggregated domain.

##### Scales Carry The Plan

**Tests**: `ScaleResolverPlanTests.ScaleResolver_Resolve_ScalesCarryThePlanFacts`

Asserts that a resolved scale carries the field it reads and its second end, the title, the bin kind, and the stack
offset, and that a datum has neither a field nor a title.

##### Size And Opacity Ranges

**Tests**: `ScaleResolverTests.ScaleResolver_Resolve_SizeAndOpacityUseTheDefaultRanges`

Asserts the derived size and opacity ranges above, including the dependence on the mark, the smaller step, a mark
without sizes (`rect`), and an explicit range.

##### Colors

**Tests**: `ScaleResolverTests.ScaleResolver_Resolve_OrdinalColorsUseTheConfiguredRanges`,
`ScaleResolverColorTests.ScaleResolverColor_Ordinal_SchemesAreWholeListsOrSamples`,
`ScaleResolverColorTests.ScaleResolverColor_Precedence_RangeThenSchemeThenTheDefault`

Asserts that a nominal field gets the `tableau10` colors in domain order; that the named scheme `tableau10`, a scheme
object, another categorical scheme, and an explicit range behave as documented; that an ordinal field uses Vega-Lite's
`ordinal` range (the blues ramp sampled at `i / (n + 1)` for the three categories); that the configured category range
(a list or a scheme) wins over the default; that a categorical scheme is used whole (its count ignored), a continuous
scheme sampled for the domain size or the `count`, and `reverse` reverses the list; and that the scale's range wins over
its scheme, which wins over the default, with a range that is not a list of colors skipped.

##### Continuous Color Scales

**Tests**: `ScaleResolverColorTests.ScaleResolverColor_Default_QuantitativeColorIsSequentialWithTheRampScheme`,
`ScaleResolverColorTests.ScaleResolverColor_Default_RectUsesTheHeatmapRangeAndDomainMidTheDivergingRange`,
`ScaleResolverColorTests.ScaleResolverColor_Domain_ThreeEntriesAreDivergingAndMoreAreRefused`,
`ScaleResolverColorTests.ScaleResolverColor_Domain_ZeroNiceMinMaxAndClamp`,
`ScaleResolverColorTests.ScaleResolverColor_UnsupportedProperties_AreIgnored`

A quantitative color of the data 3 to 12 is a sequential scale over exactly that extent (no zero, no nice) whose ends
are the ends of the blues ramp; with the domain 0 to 10, 2.5, 5, and 10 are the ramp at 0.25, 0.5, and 1, and 20
extrapolates to the ramp at 2; fill and stroke are separate scales. A `rect` uses the yellowgreenblue (heatmap) ramp.
`domainMid: 7` makes a diverging scale with the domain 3, 7, 12 whose ends are the blueorange ramp read backwards (its
extent is 1 to 0) and whose middle is the ramp at 0.5; a middle value of 20 or -5 goes after the last or before the
first entry; a size ignores `domainMid`. An explicit domain of three entries is diverging with the blues range, and at
0, 5, 10 the quarter point is 0.25; 0, 10, 100 maps 5 to 0.25; four entries (or three plus `domainMid`) fail with the
location of the domain. `zero` pulls 3 to 0; `nice` rounds 0.2 to 9.7 to 0 to 10; `domainMin` and `domainMax` replace
the ends; clamping keeps 20 on the end color. `scheme` and `interpolate` on a size, and `domainMin`, `domainMax`, and
`clamp` on a quantize scale, are ignored.

##### Schemes, Interpolation, And Configured Ranges

**Tests**: `ScaleResolverColorTests.ScaleResolverColor_Scheme_NamesObjectsAndExtents`,
`ScaleResolverColorTests.ScaleResolverColor_Reverse_FlipsTheRamp`,
`ScaleResolverColorTests.ScaleResolverColor_Range_ExplicitColorsInterpolatePiecewise`,
`ScaleResolverColorTests.ScaleResolverColor_Interpolate_ChoosesTheColorSpaceOfAList`,
`ScaleResolverColorTests.ScaleResolverColor_ConfigRange_NamedRangesAreListsOrSchemes`,
`ScaleResolverColorTests.ScaleResolverColor_Scheme_UnknownNameIsASpecificationError`

Over the domain 0 to 10 the colors at 0, 5, and 10 are the scheme at the start, the middle, and the end of its extent (a
name in any case, an object, an extent of 0.2 to 0.8, a reversed extent 1 to 0, `turbo` with a count that continuous
scales ignore); `reverse` (and `sort: "descending"`, unless `reverse` says otherwise) flips the ramp, also for a scheme
extent and a list of colors. Black and white give `#808080` in the middle (127.5 rounds half away from zero); red, lime,
blue give `#808000` at a quarter (halfway from red to lime) and `#00ff00` in the middle; one color is constant; an
invalid color fails at its entry. `interpolate` changes the middle of red and blue in each color space and the colors
equal the `ColorInterpolator` results within one per channel; a named scheme ignores `interpolate`. `config.range`
supplies lists and scheme objects for `ramp`, `heatmap`, `diverging`, and names of its own that `scale.range` refers to;
a name it lacks is an error at the range, and a removed default falls back to Vega's own. An unknown scheme name fails
at the scheme, at its `name`, or, when a configured range names it, at the channel (or the range).

##### Discretizing Scales

**Tests**: `ScaleResolverColorTests.ScaleResolverColor_Quantize_UsesFiveSchemeColorsOverEqualIntervals`,
`ScaleResolverColorTests.ScaleResolverColor_Quantile_ThresholdsComeFromTheSortedValues`,
`ScaleResolverColorTests.ScaleResolverColor_Threshold_ExplicitThresholdsOrTheExtent`,
`ScaleResolverColorTests.ScaleResolverColor_DiscretizingNumbers_SpreadTheRangeEvenly`

Quantize of 3 to 12 with five colors (blues at `i / 6`) has the thresholds 4.8, 6.6, 8.4, and 10.2; three colors cut at
6 and 9 and a value on a threshold takes the color above; `zero` applies (0 to 12) while `domainMin` does not. Quantile
of 3, 5, 7, 12 with four colors has the thresholds 4.5, 6, and 8.25; with five 4.2, 5.4, 6.6, and 9; an explicit domain
is sorted. Threshold of 5 and 10 has three colors (5 goes up), without a domain the thresholds are 3 and 12, and one
threshold needs two colors; neither has zero or nice. A quantize size over 4 to 361 has the sizes 4, 123, 242, 361;
`quantileCount` 3 gives 4, 182.5, 361, as do two thresholds; opacity keeps 0.3 and 0.8; an explicit range is kept and
reversed on request; an ordinal size alternates 4 and 361.

##### Types, Channels, And Ranges Of The New Channels

**Tests**: `ScaleResolverColorTests.ScaleResolverColor_Kind_FollowsTheVegaLiteTypeRules`,
`ScaleResolverColorTests.ScaleResolverColor_AcceptsScaleType_AppliesChannelAndFieldRules`,
`ScaleResolverColorTests.ScaleResolverColor_AcceptsScaleType_RejectsNullArguments`,
`ScaleResolverColorTests.ScaleResolverColor_Channels_AreDroppedForMarksThatDoNotUseThem`,
`ScaleResolverColorTests.ScaleResolverColor_NewChannels_UseTheirDefaultRanges`,
`ScaleResolverColorTests.ScaleResolverColor_Shape_RangeIsTheSymbolList`,
`ScaleResolverColorTests.ScaleResolverColor_StrokeDash_RangeIsFivePatterns`,
`ScaleResolverColorTests.ScaleResolverColor_RangeMinAndMax_ReplaceTheEntriesOfADefaultRange`

A table of channel, field type, and explicit scale type gives the kind that Vega-Lite's `defaultType`,
`channelSupportScaleType`, and `scaleTypeSupportDataType` give (for example a nominal color with `quantize` stays
ordinal, a quantitative shape is ordinal, a quantitative shape with `quantize` is quantize, a size with `sequential` is
linear), and the same rules answer `AcceptsScaleType`. `shape` has a scale only on a `point`, `angle` on `point`,
`circle`, and `square`, `strokeDash` and `size` not on a `rect`. Over 3 to 12 `strokeWidth` maps 7.5 to 2.5 (1 to 4),
the opacities 0.3 to 0.8 (0.55 in the middle), `angle` 0 to 360 (180 in the middle), and the configuration moves the
ends; categories are points over 1 to 4. A shape scale lists the eight symbols (the categories a, b, c are circle,
square, triangle-up), takes an explicit list (path strings too), a configured list, a reversed list, and a quantize
scale cuts the extent into eight intervals; a non-string entry fails at its location. A dash scale lists the five
patterns, takes an explicit list of lists, and a bad entry fails at its location. `rangeMin` and `rangeMax` replace the
first and second entry of a default range (size 10 to 50; x 20 to 120; y keeps the height and takes the maximum), not of
an explicit range, a step-sized scale, or a discretizing scale.

##### Color Limits, Cancellation, And Determinism

**Tests**: `ScaleResolverColorTests.ScaleResolverColor_Limits_AreEnforcedBeforeAnythingIsBuilt`,
`ScaleResolverColorTests.ScaleResolverColor_QuantileDomain_IsSortedChargedAndCancellable`,
`ScaleResolverColorTests.ScaleResolverColor_Resolve_IsDeterministicAcrossRepeatedAndConcurrentRuns`

With `MaxScaleDomainSize` 10 a scheme count of 11 fails (ordinal and quantize) at the scheme name; a range of three
colors, a domain of three numbers, and a threshold domain of three with a limit of 2 fail at the array; `quantizeCount`
1000 fails at the channel; all messages name `MaxScaleDomainSize` and `the limit of`, and a count of exactly 10 is
accepted. The quantile domain of 5,000 values given in descending order is complete and ascending, the median is 2500.5,
a step limit of 40 fails at the channel with `MaxEvaluationSteps`, and a canceled token stops the read of 9,000 values.
Resolving the same specification repeatedly and from eight tasks at once gives the same colors, thresholds, sizes, and
`lab` interpolated colors.

##### Discrete Sizes Come From The Step

**Tests**: `ScaleResolverTests.ScaleResolver_Resolve_DiscreteSizesComeFromTheStep`

Asserts the derived step-driven sizes above, that a continuous scale has no step size, and that the model of a
step-sized scale fills the extent given by the layout.

##### Invalid Inputs Fail With A Location

**Tests**: `ScaleResolverTests.ScaleResolver_Resolve_InvalidInputsFailWithLocation`

Asserts that three categories over a `MaxScaleDomainSize` of two (at the pointer `/encoding/x`), and an explicit domain
over the limit, throw `VegaLiteRenderException` naming the limit; that a range whose second value is text
(`/encoding/x/scale/range/1`), a range of one value (`/encoding/x/scale/range`), a domain whose second value is text
(`/encoding/x/scale/domain/1`), and a linear domain of one value throw `VegaLiteSpecException` naming the offending
pointer; and that null arguments throw `ArgumentNullException` and a canceled token over nine thousand categories
throws `OperationCanceledException`.

##### Log, Pow, Sqrt, And Symlog Scales

**Tests**: `ScaleResolverTests.ScaleResolver_Resolve_LogPowSqrtSymlogCarryTheirTransform`,
`ScaleResolverTests.ScaleResolver_Resolve_LogScaleDefaultsFollowVegaLite`,
`ScaleResolverTests.ScaleResolver_Resolve_LogDomainThatIncludesZeroFailsWithALocatedError`,
`ScaleResolverTests.ScaleResolver_Resolve_TransformTypesNeedAChannelAndFieldThatAcceptThem`,
`ScaleResolverColorTests.ScaleResolverColor_AcceptsScaleType_AppliesChannelAndFieldRules`

Assert, for the data 3, 7, 12, and 5, that a log x scale has the base 10, no zero (3 to 12), a nice domain of 1 to 100 (150
pixels per decade over 300), base 2 rounds out to 2 to 16, a pow scale with the exponent 2 has the domain 0 to 12 and
puts 6 at 75
of 300, a sqrt scale ignores an `exponent` and puts 3 at half of 300, a symlog scale with the constant 10 over 0 to 90
puts 9 at
ln(1.9) / ln(10), size and color scales take the transform (the geometric mean of 3 and 12 is the middle color), and the
properties of another type are ignored; that `zero: true` does not pull a log domain to zero, an explicit domain
disables nice
and clamp carries over, a bar on a log axis spans 1 to 100, a domain below zero is reflected, the pixel padding
is applied in
the logarithm (the domain becomes 6 / 2^(15/14) to 6 * 2^(15/14) for 10 pixels on 300), and data without a valid
value gets the
decade 1 to 10; that data with a zero or with both signs, an explicit domain containing zero (also a later entry), and a
`domainMin` of zero are located errors (the render error at `/encoding/y` for the data and `domainMin`, the specification
error at `/encoding/x/scale/domain` for an explicit domain) while symlog and pow take the same data; and that a
measure with a
log scale is not stacked (the stack is null and the domain is that of the values), a nominal field and a `shape`
channel keep
their default scale types, and the color, size, and position channels accept the transform types.

##### Bin-Ordinal Scales And Binned Channels

**Tests**: `ScaleResolverBinTests.ScaleResolverBin_BinnedColor_DefaultsToBinOrdinalOverTheBinEdges`,
`ScaleResolverBinTests.ScaleResolverBin_Kind_FollowsTheVegaLiteTypeRulesForBinnedFields`,
`ScaleResolverBinTests.ScaleResolverBin_ContinuousScales_SpanTheBins`,
`ScaleResolverBinTests.ScaleResolverBin_BinOrdinal_UsesTheRangeAndTheReversalOfTheSpecification`,
`ScaleResolverBinTests.ScaleResolverBin_DiscreteBinnedColor_ReadsTheTextOfTheBins`,
`ScaleResolverBinTests.ScaleResolverBin_FieldWithoutNumbers_HasAnEmptyBinOrdinalScale`,
`ScaleResolverBinTests.ScaleResolverBin_Limits_AreEnforcedOnTheBinsOfAChannel`,
`ScaleResolverBinTests.ScaleResolverBin_Work_IsChargedToTheEvaluationBudget`,
`ScaleResolverColorTests.ScaleResolverColor_AcceptsScaleType_BinOrdinalNeedsABinnedQuantitativeColor`

Assert, for the values 3 to 97 with the six-bin default (the step 20 over 0 to 100), that a binned quantitative color
is a bin-ordinal scale with the six edges 0, 20, ..., 100 as its domain, five different colors, the field
`bin_maxbins_6_v`, and the bin of a number giving its color (a number below the first edge none, and the last edge the
first color, as in Vega); that as a theory over channels, field types, and scale types the default kinds follow
`defaultType` (a binned color, fill, and stroke bin-ordinal; size, opacities, stroke width, and angle linear; shape and
stroke dash ordinal; a nominal or ordinal field the ordinal scale of the text) and an explicit type is used where
`channelSupportScaleType` and `scaleTypeSupportDataType` allow it and the default otherwise (the explicit `bin-ordinal`
of a binned quantitative color is that default, and anywhere else is replaced); that the values 132 to 176 have the
bins 130 to 180 by 10, so an opacity, a size, and a linear color have the domain 130 to 180 (a size from 0, no nice)
and a bin-ordinal one six edges; that an explicit range of three colors cycles over five bins and `reverse` turns them
around, while a scheme `count` is not used; that the text of the bins is the domain of a binned ordinal color; that a
field without a number has no edge and no color; that a limit on `MaxScaleDomainSize` passes six bins (`maxbins` 6) and
five bins of a `step` of 20 and fails a lower one with a located error at the `bin` that names the limit; that a step
budget of 40 stops the bins of 5000 rows; and that `bin-ordinal` is accepted by the question of the feature catalog
for a binned quantitative color, fill, and stroke only.

##### Conditional Fields

**Tests**: `ScaleResolverColorTests.ScaleResolverColor_Resolve_FieldInAConditionGivesTheChannelItsScale`,
`LegendPlanConditionTests.LegendPlan_Create_ValueConditionsCreateNoScaleAndNoLegend`

Assert, over the sample data (the numbers 3, 5, 7, and 12 and the categories a, b, and c), that a color whose condition
holds the quantitative field `q` is a sequential scale over 3 to 12 read from `q`, that a shape whose condition holds the
nominal field `k` has the ordinal scale of `a`, `b`, and `c`, and that a size and an opacity whose conditions hold only
values have no scale (and no legend is planned for a color, an opacity, and a size with value conditions).

##### Temporal Fields Get A Time Scale

**Tests**: `ScaleResolverTimeTests.ScaleResolver_Temporal_XDefaultsToATimeScaleWithTheDataExtent`,
`ScaleResolverTimeTests.ScaleResolver_Temporal_EmptyDataGivesADefaultDomain`

Assert that a temporal x is a `Time` scale of data type `temporal` over the data extent (January 1 to January 11), with
no zero (zero would pull the domain to 1970), no `nice`, no padding, no time unit, the UTC calendar, and the field
path `d`; that the model it creates over 400 pixels is a `Time` model sharing the calendar and puts January 6 at 200
and the ends at 0 and 400, while the quantitative y stays linear; that a temporal y runs upward; and that a field
without a valid date (no rows, an unparsable text, a null) still gets a time scale with the default domain 0 to 1,
mapping the instant 1 to the end of a 100 pixel range, as a linear scale does.

##### Time Units Choose The UTC Or Local Scale

**Tests**: `ScaleResolverTimeTests.ScaleResolver_UtcTimeUnit_GivesAUtcScale`,
`ScaleResolverTimeTests.ScaleResolver_TimeUnit_InAZone_FollowsTheLocalDay`

Assert that `utcyearmonthdate` gives a `Utc` scale with the UTC calendar in a +05:30 zone while `yearmonthdate` gives a
`Time` scale with the zone's calendar (and each model has its kind), and that the local day of 2024-03-05T20:00Z in
+05:30 starts at March 5 18:30Z, where the UTC unit starts at March 5 00:00Z.

##### Explicit Scale Types Of Temporal Fields

**Tests**: `ScaleResolverTimeTests.ScaleResolver_ExplicitTypeTimeAndUtc`,
`ScaleResolverTimeTests.ScaleResolver_AcceptsScaleType_PinsTheTemporalTable`

Assert that `time` and `utc` on a temporal x or size give a `Time` or `Utc` scale, that on a color or fill they give the
sequential scale over the instants, that the types a temporal field refuses (`linear`, `band`, `ordinal`, and the
others) are replaced by the default (the sequential scale for a color), that opacity, stroke width, fill and stroke
opacity, and angle give a time scale while `shape` and `strokeDash` stay ordinal, and that `time` on a nominal,
ordinal, or quantitative field is ignored. The acceptance question that the feature catalog asks is pinned over a
table: `time` and `utc` are accepted for a temporal field on nine channels, refused on `shape` and `strokeDash`, no
other type is accepted for a temporal field, and `time` and `utc` are refused for the other field types.

##### Time-Unit Bars Span Their Unit

**Tests**: `ScaleResolverTimeTests.ScaleResolver_TimeUnitBarDomainIncludesTheEnd`,
`ScaleResolverTimeTests.ScaleResolver_TimeUnitPointDomainIsTheStartsOnly`,
`ScaleResolverTimeTests.ScaleResolver_BarPaddingNotAppliedForTimeUnit`

Assert, for a date in each of January, February, and March 2024 under a `yearmonth` unit of a bar, that the scale reads
`yearmonth_d` with the end field `yearmonth_d_end`, the domain is January 1 to April 1 with no padding and no `nice`,
and February 1 maps to 31 / 91 x 300 = 102.198 on 300 pixels; that a point under the same unit has no end field and
the domain January 1 to March 1; and Vega-Lite's padding rule (a bar on a time axis with no unit has 5 pixels, with a
unit none, and a scale `padding` of 3 or a configured `continuousPadding` of 7 apply to both) and the 400 / 380 growth
of the padded domain derived above.

##### Explicit Domains Of Dates

**Tests**: `ScaleResolverTimeTests.ScaleResolver_DomainStringsAndDatetimeObjects`,
`ScaleResolverTimeTests.ScaleResolver_DomainOfASingleUnit_NamesParts`,
`ScaleResolverTimeTests.ScaleResolver_DomainMinMaxDatetime`,
`ScaleResolverTimeTests.ScaleResolver_Datum_Temporal`

Assert that a domain of date texts, date-time objects, and timestamps resolves to instants (UTC, and in a +05:30 zone
the object is local midnight, a date alone and a `utc` object are UTC, and a time of day without an offset is local), that
three entries make a piecewise scale that keeps its order, that an entry that is not a date, a boolean, or a domain
with one entry fails with a located `VegaLiteSpecException` at `/encoding/x/scale/domain/1`, `.../0`, and
`.../domain`; that for a
single month unit the number 2 and the text "Mar" name the months of 2012 while a timestamp and a date text are what
they say; that `domainMin` and `domainMax` (an object and a timestamp) replace the ends of the data domain
(January 3 to February 1, or January 1 to March 1 for a `domainMax` of `{year: 2024, month: 3}`); and that a datum of a
temporal channel (a date text or a timestamp) is a domain of that one instant.

##### Nice Time Domains

**Tests**: `ScaleResolverTimeTests.ScaleResolver_NiceTrueAndInterval`,
`ScaleResolverTimeTests.ScaleResolver_NiceInAZone_UsesLocalBoundaries`

Assert the derivations above: no `nice` by default (the domain starts at the data, January 10 13:00Z), `true` is the
count 10 and gives the week boundaries January 7 and March 24, the count 2 gives the month boundaries (January 1 and
April 1), the interval texts and objects (`month`, and `month` with step 3) give the same boundaries and keep the
interval and step on the scale (no count), `false`, 0, and a negative number switch it off, and in a +05:30 zone the
boundaries are local midnights (December 31 18:30Z and March 31 18:30Z).

##### Ordinal And Nominal Fields With A Time Unit

**Tests**: `ScaleResolverTimeTests.ScaleResolver_OrdinalTimeUnit_UsesABandScaleSortedByDate`,
`ScaleResolverTimeTests.ScaleResolver_OrdinalTimeUnit_SortDescending`,
`ScaleResolverTimeTests.ScaleResolver_SortArrayOfDatetimeObjects`

Assert that an ordinal field with a `month` unit on a bar is a band scale over the dates of the unit (the months of the
reference year 2012, with the two Januaries one category), sorted by date whatever the order of the data, with the
field `month_d`, the data type `ordinal`, and the temporal flag set; that a point mark or a nominal field gives a point
scale over the three dates; that a descending sort puts the latest unit first (March, February, January); and that a
sort array `["Dec", 1]` puts the unlisted February first and then December and January, and an explicit domain of
`"Dec"`, 1, and `{"month": "Feb"}` gives December, January, February of 2012.

##### Colors And Numeric Channels Of Temporal Fields

**Tests**: `ScaleResolverTimeTests.ScaleResolver_TemporalColor_IsSequentialOverMilliseconds`,
`ScaleResolverTimeTests.ScaleResolver_TemporalColor_ExplicitUtcType_UsesTheUtcCalendar`,
`ScaleResolverTimeTests.ScaleResolver_TemporalColor_ZeroIsIgnored`,
`ScaleResolverTimeTests.ScaleResolver_TemporalColorQuantile_Threshold_Quantize_FallBackToSequential`,
`ScaleResolverTimeTests.ScaleResolver_TemporalSize_And_Opacity_UseTheTimeScale`

Assert that the color of a temporal field is a sequential scale over the milliseconds (January 1 to January 11) with an
interpolator, no `nice`, and the temporal flag, whose colors at the two ends and in the middle are the samples of the
interpolator at 0, 1, and 0.5, with no color for a null; that a `domainMid` timestamp (January 6) makes a diverging
scale of three
entries; that in a chart five hours behind UTC the color and fill channels with `scale.type: "utc"` stay sequential but
have the UTC calendar and `IsUtcTime`, while `type: "time"` and no type have the zone's calendar and no flag; that
`scale.zero: true` on a temporal color (also with the type `utc`) leaves the domain at January 1 to January 11 while the
same property on a quantitative color pulls the domain of 1 to 3 to 0 to 3;
that `quantile`, `quantize`, `threshold`, `bin-ordinal`, and `ordinal` on a temporal color stay sequential;
and that size, opacity, and angle get a time scale over their own ranges (opacity 0.3 to 0.8, angle 0 to 360), the
first date of a size being its minimum and not zero, the middle date 0.55 and 180 as derived above, and `shape` an
ordinal scale of three dates.

##### Range, Reverse, Clamp, And Round Of Time Scales

**Tests**: `ScaleResolverTimeTests.ScaleResolver_TimeScale_RangeReverseClampRound`

Asserts that an explicit range of 50 to 250 and the limits `rangeMin` 20 and `rangeMax` 380 map the ends of the domain to
them, that `reverse` and a descending sort flip the range of the continuous domain (January 1 at 400), and that `clamp`
and `round` reach the model: February 1 is clamped to 100 and 3.5 days of ten over 100 pixels is 35.

##### Time Scales Respect The Limits, And Are Deterministic

**Tests**: `ScaleResolverTimeTests.ScaleResolver_ConfigAndSpecArraysRespectMaxScaleDomainSize`,
`ScaleResolverTimeTests.ScaleResolver_Deterministic_UnderCultures`

Assert that with `MaxScaleDomainSize` 3 an explicit domain of four dates fails with a located `VegaLiteRenderException`
at `/encoding/x/scale/domain` that names the limit of 3 and `MaxScaleDomainSize`, for a temporal field and for a
discrete time-unit field, that exactly three entries are accepted, and that a discrete domain of four dates drawn
from the data fails at `/encoding/x`; and that the resolution of a `yearmonth` bar with `nice` and an explicit
domain (January 1 and April 2024) is identical under the Turkish and German cultures (kind `Time`, January 1 to
April 1, count 10).
