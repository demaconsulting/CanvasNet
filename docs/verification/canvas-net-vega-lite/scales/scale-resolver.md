### ScaleResolver Unit Verification Design

This document describes the unit-level verification strategy for the `ScaleResolver` unit in the
`DemaConsulting.CanvasNet.VegaLite.Scales` namespace: the internal static partial `ScaleResolver` class (`Resolve` and
`InferOrient` in `ScaleResolver.cs`, the channels, domains, and titles that follow the encoding plan in
`ScaleResolver.Plan.cs`, and the discrete sort in `ScaleResolver.Sort.cs`) and the `ResolvedScales`, `ResolvedScale`, and
`RangeEnd` types of its file. The resolver reads the view of a parsed specification, the `EncodingPlan` and the tables
that the implicit stage produced (the Raw and the Main table), and the resolved configuration, and infers for each of
the channels `x`, `y`, `color`, `fill`, `stroke`, `size`, and `opacity` the scale kind, the domain, the range, and the
paddings according to the Vega-Lite rules for this release (linear, band, point, and ordinal scales; the types
`quantitative`, `nominal`, and `ordinal`), including the fields that aggregates, bins, and stacks derive, the domains of
stacked, binned, ranged, and `unaggregated` channels, the titles, and the sort of a discrete domain by another channel's
aggregate.

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
- Step-driven discrete sizes: three bars take 20 x (3 - 0.1 + 0.1) = 60 pixels, a specification step of 30 gives 90, four
  points with half a step of padding take 20 x 4 = 80, five bars on y take 20 x 5 = 100, and the configured step of 25
  gives 25 x 3 = 75; a fixed width, an explicit range, a continuous scale, and a `container` size are not step-driven.
- Domains of the aggregating features: stacked layers have the extent of their starts and ends (and `[0, 1]` for
  `normalize`); a range `x` of 1 to 5 with `x2` of 3 to 9 has the domain 1 to 9; pre-binned starts 0 and 10 with ends 10
  and 20 have the domain 0 to 20 with no zero and no nice; and the means 2 (of 1 and 3) and 8 (of 4 and 12) per category
  have the domain 2 to 8, while the `unaggregated` domain reads the rows before the aggregate, 1 to 12.
- Sort of the categories `a` (weights 2 and 3), `b` (10), and `c` (1): by the sum 5, 10, 1 the order is `c, a, b`; by the
  minimum 2, 10, 1 it is `c, a, b`; by the third quartile (R-7: 2.75, 10, 1) and by the number of valid values (2, 1, 1)
  the orders are `c, a, b` and `b, c, a` (ties in first appearance order); the sample variance of `a` is 0.5 and the
  categories with one value have a null variance, which sorts first in either direction.

Unit tests reside in `ScaleResolverTests.cs` (the scale kinds, domains, ranges, paddings, colors, and sizes) and
`ScaleResolverPlanTests.cs` (the plan-driven fields, domains, titles, and sorts) within the
`DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Scales` folder; the sorts over rows made by a calculate
transform are in `RowTransformsTests.cs`, in the `Transforms` folder.

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
quantitative color, a temporal field, a value definition, a datum-less definition, or `scale: null`; that a count (an
aggregate without a field) is a field definition and so has a linear scale that reads `__count` and has the title
"Count of Records"; that a field without a type is read as quantitative and a datum takes the type of its value; and
that a view without a mark or encoding has no scales and no orientation.

##### Orientation Of Bars, Ticks, And Rules

**Tests**: `ScaleResolverTests.ScaleResolver_InferOrient_FollowsVegaLite`

Asserts, over a table of `(mark, specified orient, x type, y type, expected orient)` rows, that a bar extends along the
measure, a tick is the opposite, a rule with one position channel runs across the view, and marks without a direction
have none.

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
gives c, b, a; `["b","c","b"]` gives a, b, c with the unlisted `a` first), that values of another kind in the array do not
position the text values, and that an array of 20,000 padding entries followed by `c`, `b`, `c` gives the same
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
to `min` and the field to the channel's own (the third quartile of `a` is 2.75, the sample variance of `a` is 0.5 and null
for the single-value categories, which sort first, the valid counts are 2, 1, 1, and the distinct counts are 2, 1, 1);
that the sort reads the Raw rows when the table is aggregated (`a` has 100 + 100 and `b` has 1, and the aggregated table
has no field `w`) and, for an aggregated stacked channel, the Raw sums (`a` is 1 + 2 + 4 = 7, `b` is 9, so the descending
sort puts `b` first); that a shorthand naming a channel that is not in the encoding and an operation that cannot order
categories keep the ascending default and the plain forms (ascending, descending, `null`, and an array) are unchanged; and
that `IsSupportedFieldSort` accepts any orderable operation with a valid field and order and refuses `encoding`, an invalid
order, the text form, and a null argument.

##### Sort Cost Is Charged And Bounded

**Tests**: `ScaleResolverPlanTests.ScaleResolver_Resolve_SortIsChargedBoundedAndCancellable`

Asserts that the rows of a sort by an aggregate are charged to the step budget (a located failure at the channel that
names `MaxEvaluationSteps` under a budget of 40), that the categories are bounded by `VegaLiteLimits.MaxScaleDomainSize`
(a limit of 100 fails), that the same specification resolves under the defaults, and that a canceled token is polled.

##### Binned, Ranged, Stacked, And Unaggregated Domains

**Tests**: `ScaleResolverPlanTests.ScaleResolver_Resolve_OrdinalBinsSortByStartAndDescendingReverses`,
`ScaleResolverPlanTests.ScaleResolver_Resolve_OrdinalBinTextUsesTheAxisFormat`,
`ScaleResolverPlanTests.ScaleResolver_Resolve_RangeDomainIsTheUnionOfBothFields`,
`ScaleResolverPlanTests.ScaleResolver_Resolve_PreBinnedDomainIsTheUnionOfStartAndEnd`,
`ScaleResolverPlanTests.ScaleResolver_Resolve_BinnedScalesHaveNoNiceUnlessAsked`,
`ScaleResolverPlanTests.ScaleResolver_Resolve_UnaggregatedDomainReadsTheRawRows`

Asserts that the text categories of an ordinal bin (the boundaries joined with an en dash) are ordered by where the bin
starts and `descending` reverses them, and that the axis `format` (`.0%` for the bins 0 to 1 by 0.1) formats the
boundaries; the range domain 1 to 9 with the merged title; the pre-binned domain 0 to 20 with the scale marked as binned
and no computed bins; that a binned scale has no `nice` unless asked for and an explicit domain is used as given; and
that the `unaggregated` domain of a mean reads the Raw rows (1 to 12) while the normal domain reads the means (2 to 8) and
a sum has no unaggregated domain.

##### Scales Carry The Plan

**Tests**: `ScaleResolverPlanTests.ScaleResolver_Resolve_ScalesCarryThePlanFacts`

Asserts that a resolved scale carries the field it reads and its second end, the title, the bin kind, and the stack
offset, and that a datum has neither a field nor a title.

##### Size And Opacity Ranges

**Tests**: `ScaleResolverTests.ScaleResolver_Resolve_SizeAndOpacityUseTheDefaultRanges`

Asserts the derived size and opacity ranges above, including the dependence on the mark, the smaller step, a mark
without sizes (`rect`), and an explicit range.

##### Colors

**Tests**: `ScaleResolverTests.ScaleResolver_Resolve_ColorsUseTableau10AndFallBackForOtherSchemes`

Asserts that a nominal field gets the `tableau10` colors in domain order with no fallback; that the named scheme
`tableau10`, a scheme object, an unknown scheme (with the fallback flag set), and an explicit range behave as
documented; that an ordinal field falls back because Vega-Lite's blue ramp is not available; and that the configured
category range (a list or a scheme) wins over the default.

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
