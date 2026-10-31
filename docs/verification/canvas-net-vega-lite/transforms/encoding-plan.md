### EncodingPlan Unit Verification Design

This document describes the unit-level verification strategy for the `EncodingPlan` unit in the
`DemaConsulting.CanvasNet.VegaLite.Transforms` namespace: the internal `EncodingPlan` class (`Create`, a pure analysis
of the view's encoding and mark that decides which fields the channels read, the implicit bin, aggregate, impute, and
stack (cartesian or polar), the fields that group the rows of a path mark and the fields of the `order` channel, the
orientation, the titles, and whether a scale needs the rows from before the aggregate) with its `ChannelPlan`, `BinPlan`,
`BinKind`, and stack, aggregate, and impute descriptions, the `TimeUnitPlan` of a field with a time unit, and the
internal static `EncodingTransforms` class (`Apply` and
`ApplyStandalone`, which run the plan over the table that the explicit transforms produced and return the Raw table, the
Main table, the bins, the time units, and the evaluation). The unit reuses the engines of `AggregateTransforms` (the bin
algorithm, the aggregate, the impute, and the stack), the rules of `StackRules`, and the time unit stage of
`TimeUnitTransform` (see _Time Unit Transform Verification Design_); the scales, marks, and axes consume what it
produces.

#### Verification Approach

The unit is verified in two parts. The plan is verified by parsing small, original specifications with the real
`SpecParser`, resolving the configuration with the real `ConfigResolver`, creating the plan, and asserting on the
channels: the derived field names, the titles, the bin and stack decisions, and the group-by and the measures. The
execution is verified by loading inline data with the real `DataLoader`, applying the plan with `EncodingTransforms`
over the result of the explicit transforms (directly, and through the `ResolveFull` helper of `ChartTestHelpers`, which
runs the explicit transforms, the plan, and the encoding stage the way a render does), and asserting on the tables, with
the group fields in ordinal order and the
rows as text. The collaborators (`SpecParser`, `ConfigResolver`, `DataLoader`, `DataTable`, `AggregateTransforms`,
`BinAlgorithm`, `StackRules`, and `VegaLiteLimits`) are real; no mocking or stubbing is used, and the internal types are
reached through the test assembly's `InternalsVisibleTo` access. The expected values are derived by hand, with the
derivation in the test comments:

- Field names and titles follow Vega-Lite: `sum` of `x` is `sum_x` with the title `Sum of x`, `average` keeps its
  spelling (`average_x`, `Average of x`), `q1` is `Q1 of x`, a count (with or without a field) is `__count` with the
  title `Count of Records`, an `argmax` of `y` over `x` is `argmax_y.x` with the title `x for max y`, and the `fieldTitle`
  modes give `Sum of x` and `x (binned)` (verbal, the default), `x` and `x` (plain), and `SUM(x)` and `BIN(x)`
  (functional), while a count keeps the configured `countTitle` in every mode.
- A bin is named `bin_`, then each parameter as `name_value` in the order written, with `maxbins_10` added last when
  neither `maxbins` nor `step` is given, then the field: `true` and `{}` give `bin_maxbins_10_x`, `{"step": 0.5}` gives
  `bin_step_0_5_x`, `{"nice": false}` gives `bin_nice_false_maxbins_10_x`, `{"extent": [0, 100]}` gives
  `bin_extent_0_100_maxbins_10_x`, and `{"anchor": -1.5, "maxbins": 8}` gives `bin_anchor__1_5_maxbins_8_x`; the end field
  adds `_end`, a discrete channel reads the range text `_range`, and the title is `x (binned)`.
- The stack follows Vega-Lite: a bar with a quantitative `y` and a nominal `x` stacks `y` with the offset `zero` even
  with one row per category, a horizontal bar stacks `x`, `stack: "normalize"` and `"center"` choose the offset,
  `stack: null` and `false` turn it off, an aggregate without a layer to split does not stack, a point mark stacks only
  when asked, and a range (`x2`) does not stack. The stacked field is `<measure>_start` and `<measure>_end`, the stacked
  channel reads the end and has the start as its second field, and a stack splits by the fields of the other position
  channel.
- A histogram of the values 1 to 10 with `maxbins` 10 has the extent `[1, 10]` and the bins `[1, 2)` to `[9, 10]` (9 and
  10 share the closed last bin), so the counts are 1 for the first eight bins and 2 for the last; the values 0 and 100
  give the bins 0 to 100 by 10 with 0 in `[0, 10)` and 100 in `[90, 100]`; a step of 25 over the values 0, 24, 25, 99,
  and 100 gives the counts 2, 1, 2 for the bins starting at 0, 25, and 75.
- A stack of the layers `x = 1` and `y = 2` of category `a` and `x = 3` of category `b` is, with the layer order
  descending by the layer field because the stacked channel is `y`, `y` (0, 2) and `x` (2, 3) for `a`, and `x` (0, 3) for
  `b`; a horizontal stack orders ascending (`x` (0, 1), `y` (1, 3)); negative values stack down separately (layers
  `z` 1, `y` -2, `x` 3 give `x` (1, 4), `y` (0, -2), `z` (0, 1)); `normalize` of `y` 3 and `x` 1 gives (0, 0.75) and
  (0.75, 1) with the domain `[0, 1]`; and `center` of totals 4 and 2 starts the narrower group at (4 - 2) / 2 = 1.
- The row budget counts the groups: the three input rows of an aggregate stay counted while its two groups are added
  (five), so `MaxDataRows` 5 passes and 4 fails located at the aggregated channel; a stack next to a kept Raw table
  counts its three rows too (3 + 3 = 6 passes, 5 fails), while a stack that replaces its input counts nothing.
- A polar stack follows the cartesian one with `theta` and `radius` in the places of `y` and `x`: a pie chart (`arc`
  with the sum of `v` on `theta` and the nominal `g` on `color`) stacks `theta` into `sum_v_start` and `sum_v_end`,
  the layers ascend by `g` (the stacked channel is not `y`, so the order is not reversed), there is no dimension to group
  by, the channel reads `sum_v_end` and has `sum_v_start` as its second field, and the aggregate groups by `g`. With a
  nominal `radius` the groups of the stack are the fields of `radius` (and the groups of a `radius` stack the fields of
  `theta`); the `order` channel sorts the layers in its own directions (`o` ascending then `p` descending); a field that
  the dimension shares with the stacked channel is not a group (the radial chart that reads `theta` and `radius` from
  `v`), so its slices stack in one group and the stack of a dimension of another field is grouped by that field; a
  `theta2` or `radius2` is linked to its main channel as `x2` and `y2` are (the channel reads the field of the secondary
  as its second end and the titles merge as `a, b`), a stacked channel keeping its own ends (`a_start`); and the
  cartesian stack of a bar keeps its precedence over a polar channel of the same mark.
- The grouping fields of a path mark follow Vega-Lite's rules: the position channels, `x2`, `y2`, the polar channels,
  `text`, `shape`, `angle`, `tooltip`, `href`, `description`, and `url` never group; a definition that aggregates, a
  value, and an `order` of a line or trail do not group; `color`, `fill`, `stroke`, the opacities, `strokeDash`,
  `strokeWidth`, `detail`, and `key` do, each field once; `size` groups a line but not a trail (the size of a trail is
  per point); and `order` groups an area, after the other fields. So a line of `color` `g`, `detail` `d1` and `d2`, and
  an `order` `o` is grouped by `g, d1, d2`, the same encoding as an area by `g, d1, d2, o`, and a trail with a size, an
  order, and a key by `k` alone; a binned or time unit field is read through its derived name (`bin_maxbins_6_c`, its
  range text `bin_maxbins_6_c_range` for a nominal channel, and `year_d`), and the nested path `a.b` and the escaped name
  `a\.b` are two fields.
- The impute of an encoding runs after the aggregate and before the stack. Series `a` has the keys 1, 2, and 3 with the
  values 1, 2, and 3, and series `b` the keys 1 and 3 with the values 4 and 5. With `impute: {"value": 5}` on `y` of
  an area the row `b` at key 2 is added with 5 after the five input rows, and the stack (descending by the color, `b`
  at the bottom) gives `b` (0, 4), (0, 5), (0, 5) and `a` (4, 5), (5, 7), (5, 8) at the keys 1, 2, and 3, where the
  stack impute then finds nothing left to add. The method `mean` over the series `a` (keys 1 and 3, values 10 and 30)
  and `b` (key 2, value 5), whose key domain is 1, 3, 2 in first-seen order, appends `a` at 2 (the mean of 10 and 30,
  20), `b` at 1, and `b` at 3 (5 each); `keyvals` `{"start": 1, "stop": 5}` with the value -1 gives the keys 1 to 4, so
  `a` is imputed at 2 and 4 and `b` at 1, 3, and 4 (five rows of -1); the method `max` over the frame `[null, 0]` gives
  30 for `a` at 2 (the imputed row follows the input rows of its group, so its frame holds both) and 5 for `b` at 1 and
  3; and the impute of `x` uses `y` as its key (the series `b` with one `h` lacks 2 and gets `w` 0).
- The implicit impute of a stacked line, area, or trail adds the missing combinations of a stack-by group and a
  dimension key with the value 0: for the same data an area stacks to the rows `a` at 1, 2, and 3 as (4, 5), (0, 2), and
  (5, 8) and `b` at 1, 3, and 2 as (0, 4), (0, 5), and (0, 0), so the layer `b` has three vertices at every key of the
  chart (the third is zero thick) and the largest top is 8, while with `impute: null` on `y` it has only its two rows;
  a stacked line (`stack: true`) is imputed the same way and a stacked bar is not (five rows). With the aggregate the
  sums are `a` 1 + 2 = 3 at 1, 2 at 2, and `b` 4 at 1, so `b` is imputed at 2 and the rows are `a` (4, 7), (0, 2) and
  `b` (0, 4), (0, 0). The plan of the impute has the series of the stack-by fields (the detail of a line is one of
  them), the dimension as the key, and the stacked measure (its derived name after an aggregate and its start field
  for a time unit) as the field, and it is skipped for `impute: null`, a stack without stack-by fields, a mark that is
  not a path mark, and a binned dimension (which the catalog reports as a warning).
- The impute is bounded before it is built, by the combinations of the series and the keys. For the series of 3 keys
  above the five input rows plus the six combinations are 11 rows, so `MaxDataRows` 11 passes and 10 fails located at the
  stacked channel; when a scale sorts by a field and the Raw table keeps the five input rows beside the six imputed rows
  and the stacked copy, 5 + 6 + 6 = 17 passes and 16 fails; a thousand `keyvals` fail under a budget of 500 located at the
  impute object; and 100 series that each have a key of their own need 100 x 100 = 10,000 rows, which the default limit
  accepts and a limit of 5,000 refuses at the stacked channel before anything is allocated.
- A time unit field is named by the unit and the field, with the end adding `_end`: the unit `yearmonth` of the field
  `date` is `yearmonth_date` and `yearmonth_date_end`, the object form with a step is `month_step_2_date`, a UTC unit
  keeps its prefix (`utcmonth_date`), and the `maxbins` form that names no unit is `timeunit_maxbins_10_date`. The
  default titles follow Vega-Lite: verbal `date (year-month)` (the parts of the unit joined with a hyphen, so
  `yearweekday` is `date (year-week-day)` and the step form `{"unit": "month", "step": 2}` is `date (month)`), functional
  `YEARMONTH(date)` (`UTCYEARMONTH(date)` for a UTC unit, `TIMEUNIT(date)` for the `maxbins` form), plain `date`, and the
  `maxbins` form is the bare field in the verbal mode because it has no parts.
- A channel spans its unit (reads the end as its second field) when it is a position channel of a temporal field with no
  secondary channel and a band position is defined: the bar, rect, and tick have 0.5 by default, a point has none unless
  the definition's `bandPosition`, the mark's `timeUnitBandPosition`, or the configuration of the mark sets one, and an
  ordinal or nominal field, a color channel, and a channel with an `x2` do not span.
- Six dates in 2024 (January 5, January 20 at 12:00, February 10, February 11, February 29 at 23:59:59.999, and March 1)
  count by month as 2, 3, and 1, in first-seen order: the last millisecond of the leap day is still February. The start
  of each group is the first of its month (2024-01-01, 2024-02-01, 2024-03-01, whose milliseconds the test computes
  independently with `TimeTestSupport.Ms`) and its end is the first of the next month. With the bin step 10 the values
  5, 15, and 15 fall in the bins starting at 0, 10, and 10; with the unit `month` the three dates give the reference
  months January, January, and February of 2012; so the three groups have one row each and the two groups of the second
  bin are stacked descending on `y`: February is the bottom layer (0, 1) and January the upper (1, 2). In the stack by
  month, the layers of the category `a` (two January rows and one February row) are February (0, 1) at the bottom and
  January (1, 3) above it, and `b` has February (0, 1).
- A zone changes the day: in a US-like zone (daylight time from 07:00Z on 2024-03-10) the instant 2024-03-10T12:00Z is on
  the local day that starts at 00:00 local = 05:00Z (at -5 h) and ends at 00:00 on March 11 = 04:00Z (at -4 h), a day of
  23 hours; in UTC the day is the one that starts at 2024-03-10T00:00Z.

Unit tests reside in `EncodingPlanTests.cs` (the plan), `EncodingTransformsTests.cs` (the execution), `EncodingPlanPolarTests.cs`
(the polar stack and the secondary polar channels), `EncodingPlanPathTests.cs` (the path grouping fields, the order fields,
and the plan of the encoding impute and of the stack impute), `EncodingTransformsImputeTests.cs` (the imputes of the
execution and their limits) and, for the time
units, `EncodingPlanTimeTests.cs` (the plan) and `EncodingTransformsTimeTests.cs` (the execution; its helpers are in
`TimeTestPipeline.cs`) within the
`DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Transforms` folder; the parser rules of the `impute` object
are in `SpecParserImputeTests.cs` in the `SpecModel` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Files**: None; every specification is an in-memory string, and no file or network is read
- **Isolation**: Each test parses its own specification and builds its own loader and context. The cancellation
  scenario runs under a token that is already canceled, and the determinism scenario runs the stage under `tr-TR` and
  `de-DE` as well as the invariant culture

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any wrong field name, title, bin name, group-by, stack decision, offset, layer order, row, row order, domain,
or label; a path group field that is missing, repeated, or wrongly included; an imputed row, key, series, or value that
differs from the hand-derived one; a table that is kept when a scale needs it or dropped when it does not; a row budget
that is off by one; a bin, step, or impute limit that is not located at the channel or the impute object; a wrong time
unit field name, title, group, band end, or day
boundary; a cancellation that is not polled; or a result that depends on the
culture constitutes a failure.

#### Unit-Level Test Scenarios

##### Field Names And Titles

**Tests**: `EncodingPlanTests.EncodingPlan_Create_NamesAndTitlesAggregatesAndPlainFields`,
`EncodingPlanTests.EncodingPlan_Create_NamesABinByItsParametersAndField`,
`EncodingPlanTests.EncodingPlan_Create_DiscreteBinReadsTheRangeText`,
`EncodingPlanTests.EncodingPlan_Create_TitlesFollowTheConfiguration`,
`EncodingPlanTests.EncodingPlan_Create_MergesTheTitlesAndFieldsOfARange`,
`EncodingPlanTests.EncodingPlan_Create_DatumAndValueChannelsReadNoField`

Asserts, as theories of twelve aggregate definitions and nine bin spellings, the names and titles above (the value
field, the field path, the Raw field, which is null for a count, and the title, with no second field); that a nominal or
ordinal binned field reads the range text and has no second field while a continuous channel that shares the bin gets
the range name in its plan; the three `fieldTitle` modes (a sum is titled `Sum of x`, `x`, or `SUM(x)`, and a bin
`x (binned)`, `x`, or `BIN(x)`) and the configured `countTitle` (`Rows` instead of `Count of Records`); that the
titles of a range merge as `a, b` without repeating equal ones and the second field is the one of the secondary
channel, while a pre-binned field keeps its field and gets the end field of `x2` and the plain title; and that a
datum or a value reads no field and has no title.

##### Implicit Bin Is Shared

**Tests**: `EncodingPlanTests.EncodingPlan_Create_SharesABinBetweenChannelsByFieldAndParameters`,
`EncodingPlanTests.EncodingPlan_Create_IdentityPlansAskForNothing`

Asserts that two channels that bin the same field with the same parameters share one bin (one pass over the field) while
different parameters make two, and that an encoding without aggregates, bins, or a stack is an identity plan while a view
without a mark or an encoding has no channels.

##### Implicit Aggregate Groups By The Other Channels

**Tests**: `EncodingPlanTests.EncodingPlan_Create_AggregateGroupsByEveryOtherFieldDefinition`,
`EncodingPlanTests.EncodingPlan_Create_AggregateGroupsByTheBoundariesOfABin`,
`EncodingPlanTests.EncodingPlan_Create_ArgMaxReadsTheFieldOfTheReturnedRow`

Asserts that the measures are those of the aggregating definitions (one per output name, even when two channels use
it), that the dimensions are every other field definition of every channel, including the detail and tooltip channels and
the elements of an array channel, and that a binned dimension contributes its start, its end, and for a discrete bin its
text, while a pre-binned field contributes its own field and that of its secondary channel; and that an `argmax`
definition becomes an `argmax` measure over the compared field with an argument field that reads the channel's field of
the returned row, while an operation without a field is not a measure.

##### Stack Decision, Names, And Layer Order

**Tests**: `EncodingPlanTests.EncodingPlan_Create_DecidesTheStackLikeVegaLite`,
`EncodingPlanTests.EncodingPlan_Create_StackNamesAndGroupsFollowTheMeasure`,
`EncodingPlanTests.EncodingPlan_Create_OrdersTheLayersLikeVegaLite`,
`EncodingPlanTests.EncodingPlan_Create_StackByExcludesTheDimensionField`,
`EncodingPlanTests.EncodingPlan_Create_BarWithTwoMeasuresStacksTheAggregatedOne`,
`EncodingPlanTests.EncodingPlan_Create_AreaMarkStacksSignedValuesLikeABar`

Asserts, as a theory of fourteen mark and encoding combinations, the stacked channel and offset above; that the stack
writes `<field>_start` and `<field>_end` of the derived measure, the stacked channel reads the upper end with the lower
end as its second field, and a stack splits by the fields of the other position channel; that the layers are ordered
bottom first by the stack-by fields, descending when the stacked channel is `y` and ascending when it is `x`, or by the
`order` channel in its own direction; that a stack-by field that is also the group-by is not a layer (the dimension's own
field is excluded) and a stack-by field that is aggregated is sorted by its derived name; that the plan carries the
inferred orientation, which decides the stack of a bar with two measures; and that an `area` with the signed values 3,
-2, and 1 gets the decision, the layer order, and the rows of a bar in both orientations (vertical: z (0, 1), y (0, -2),
x (1, 4); horizontal: x (0, 3), y (0, -2), z (3, 4)), so that the area mark, which is drawn since Phase 9, stacks as a bar
does.

##### Raw Table Is Flagged

**Tests**: `EncodingPlanTests.EncodingPlan_Create_FlagsTheEncodingsThatReadTheRawTable`

Asserts that a sort by an object or by a channel shorthand and an `unaggregated` scale domain make the scales read the
Raw table, and that the plain sorts do not.

##### Aggregate Per Category

**Tests**: `EncodingTransformsTests.EncodingTransforms_Apply_AggregatesPerCategoryInFirstSeenOrder`,
`EncodingTransformsTests.EncodingTransforms_Apply_AggregateWithoutDimensionsIsOneRowAndEmptyInputHasNone`,
`EncodingTransformsTests.EncodingTransforms_Apply_ArgMaxReadsTheFieldOfTheWinningRow`,
`EncodingTransformsTests.EncodingTransforms_Apply_KeepsTheRawRowsWhenAScaleNeedsThem`

Asserts that the sum per category gives one row per category in first-seen order with the derived field names and that
the Raw table is the Main table when no scale needs the rows before the aggregate; that an aggregate without a dimension
is one row, no rows give no row, and a count does not read a field; that an `argmax` reads the field of the row with the
largest compared value (in group `a` the row with t = 5 has v = 9, in group `b` the only row has v = 2); and that Raw
keeps the three input rows and their fields while Main has the two groups when a scale sorts by a field.

##### Stack Layers

**Tests**: `EncodingTransformsTests.EncodingTransforms_Apply_StacksTheAggregatedLayersDescendingOnY`,
`EncodingTransformsTests.EncodingTransforms_Apply_StacksNegativeValuesDownwards`,
`EncodingTransformsTests.EncodingTransforms_Apply_SignedStacksOnXAndWithAggregatedNegativeSums`,
`EncodingTransformsTests.EncodingTransforms_Apply_NormalizeAndCenterOffsets`,
`EncodingTransformsTests.EncodingTransforms_Apply_StacksAscendingOnX`,
`EncodingTransformsTests.EncodingTransforms_Apply_OrderChannelOrdersTheLayers`

Asserts the stacked ranges above for the aggregated layers, the negative values (the rows keep their order), the
signed values on a horizontal measure (x (0, 3) and z (3, 4) up, y (0, -2) down) and the aggregated negative sums
(x = -1 + -3 = -4 stacks to (0, -4), y = 2 to (0, 2)), the
normalize and center offsets with the domain `[0, 1]` for normalize, the ascending horizontal stack, and that the
`order` channel replaces the default layer order: with the order field descending the layer with the larger order is at
the bottom (`q` (0, 2), `p` (2, 3)), and with the default direction `p` is (0, 1) and `q` is (1, 3).

##### Bins, Histogram, And Labels

**Tests**: `EncodingTransformsTests.EncodingTransforms_Apply_BinsAndCountsAHistogram`,
`EncodingTransformsTests.EncodingTransforms_Apply_BinOnlyKeepsRowsAndLeavesInvalidValuesWithoutABin`,
`EncodingTransformsTests.EncodingTransforms_Apply_InvalidValuesFormOneGroupWithoutBounds`,
`EncodingTransformsTests.EncodingTransforms_Apply_BinStepAndExtentParameters`,
`EncodingTransformsTests.EncodingTransforms_Apply_OrdinalBinsAreTextCategoriesOrderedByStart`

Asserts the histogram counts above; that a bin without an aggregate keeps every row and adds the start and the end of
its bin (0 is `[0, 10)` and 100 is `[90, 100]`), with null for a value that is not a finite number (null and
`"abc"`); that an aggregate puts the rows with no valid value in one group, in first-seen order, with no bin; the exact
`step` and `extent` vectors above; and that a discrete bin has the text of its boundaries joined with an en dash as its
categories, ordered by where the bin starts (the values 0, 10, and so on to 100 have the bins 0 to 100 by 10).

##### Binned Channels Other Than The Positions

**Tests**: `EncodingPlanTests.EncodingPlan_Create_BinsTheChannelsThatHaveAScaleWithTheirDefaultMaxBins`,
`EncodingPlanTests.EncodingPlan_Create_DiscreteBinOfAnotherChannelReadsTheRangeTextWithTheLegendFormat`,
`EncodingPlanTests.EncodingPlan_Create_DiscreteBinReadsTheRangeText`,
`EncodingTransformsTests.EncodingTransforms_Apply_BinsAColorChannelAndSharesTheBinWithX`,
`EncodingTransformsTests.EncodingTransforms_Apply_NonPositionBinsUseTheirDefaultMaxBinsAndTheRangeTextOfDiscreteFields`,
`EncodingTransformsTests.EncodingTransforms_Apply_NonPositionBinsAreChargedAndCancellable`

Asserts, as a theory over the channels, that a binned `color`, `fill`, `stroke`, `size`, the opacities, `strokeWidth`, and
`shape` get the field `bin_maxbins_6_v`, a `strokeDash` `bin_maxbins_4_v`, an `angle` `bin_maxbins_10_v`, that an explicit
`maxbins`, `step`, or `nice` is named in the order written, that the second field is the end of the bin, and that the
title is `v (binned)`; that a nominal or ordinal color reads `bin_maxbins_6_v_range` with no second field and the
`legend.format` is the format of the text, while `bin: "binned"` and a bin of an aggregate plan no bin; that a bin of
`x` and a bin of `color` with the same `maxbins` of 5 are one bin whose start and end the aggregate groups by (the
values 3, 12, 27, 41, 58, 66, 83, and 97 have the step 20 over 0 to 100 and the counts 2, 1, 2, 1, and 2), that a
color alone keeps the rows and adds `0|20`, `0|20`, `20|40`, `40|60`, `40|60`, `60|80`, `80|100`, and `80|100`; that
four bins ask for the step 50, and the ordinal color has the texts `0 – 20` to `80 – 100`; and that a limit of 100
evaluation steps stops the bins of a color of 5000 rows with a located error naming `MaxEvaluationSteps` and a canceled
token stops the stage.

##### Row Budget, Limits, And Cancellation

**Tests**: `EncodingTransformsTests.EncodingTransforms_Apply_CountsTheGroupsInTheRowBudget`,
`EncodingTransformsTests.EncodingTransforms_Apply_StackNextToRawRowsCountsTheRows`,
`EncodingTransformsTests.EncodingTransforms_Apply_BoundsTheBinsAndChargesTheRows`,
`EncodingTransformsTests.EncodingTransforms_Apply_PollsTheCancellationToken`

Asserts the exact row budgets above, with the failure located at the aggregated channel; that more bins than
`VegaLiteLimits.MaxScaleDomainSize` fail located at the `bin` and that the rows are charged to the step budget (a located
failure that names `MaxEvaluationSteps`); and that the standalone entry polls a canceled token while it reads the rows.

##### Identity, Arguments, And Determinism

**Tests**: `EncodingTransformsTests.EncodingTransforms_Apply_IdentityReturnsTheInputAndChecksArguments`,
`EncodingTransformsTests.EncodingTransforms_Apply_IsDeterministicAndCultureInvariant`

Asserts that an encoding that asks for nothing returns the input table itself as both the Raw and the Main table, that
null arguments are refused, and that two runs and runs under other cultures give the same tables, domains, and labels.

##### Conditional Fields

**Tests**: `EncodingPlanTests.EncodingPlan_Create_FieldInAConditionIsTheFieldOfTheChannel`

Asserts, for a point mark, that a color whose own definition is a value with a condition on a test that holds the field
`c` reads `c` (the source field, the field path, and the title) and names the condition as its field definition while
its `Definition` stays the channel; that a size with a value condition and an opacity with a condition on a parameter
read no field; and that a shape with a field of its own keeps it and is its own field definition.

##### Polar Stack

**Tests**: `StackRulesTests.StackRules_Decide_StacksThetaWithRadiusAsTheDimension`,
`StackRulesTests.StackRules_Decide_RangedPolarMarkIsNotStacked`,
`EncodingPlanPolarTests.EncodingPlan_Create_PlansAPolarStackWithAscendingLayers`,
`EncodingPlanPolarTests.EncodingPlan_Create_PolarDimensionAndOrderDecideTheStackGroups`,
`EncodingPlanPolarTests.EncodingPlan_Create_LinksTheSecondaryPolarChannels`,
`EncodingPlanPolarTests.EncodingPlan_Create_CartesianStackKeepsPrecedenceOverPolar`,
`EncodingPlanPolarTests.EncodingPlan_Create_StackDoesNotGroupByTheStackedField`

Asserts, for the decision, that `theta` of an arc is stacked with the offset `zero` and one stack-by field for a color,
that a nominal `radius` is its dimension (and a color on the same field as the dimension is no layer: zero stack-by
fields), that a `radius` alone or with a nominal `theta` stacks the other way round, that an explicit `stack: true` on
`radius` beside a quantitative `theta` chooses `radius`, that `normalize` is the offset of an explicit one, that an
aggregated `theta` and a `text` mark with `stack: true` (two stack-by fields, the text and the color) and a `point` with
an explicit stack are stacked, that `x` is decided first, and that the `orient` of a bar does not apply to a polar
channel;
and, as a theory over sixteen mark and encoding combinations, that two quantitative polar channels without an explicit
stack, a secondary `theta2` of a `theta` or `radius2` of a `radius` that is a field (a ranged mark; also for an aggregated
one and for a datum), a
binned or nominal measure, `stack: false`, a `sqrt` scale type, a single aggregate without layers, a `rect`, and a `line`
are not stacked, while a `theta2` that is a value, a `theta` with a `radius2` field, and a `bar` with `theta` are. It
asserts for the plan that the pie chart stacks `theta` into `sum_v_start` and `sum_v_end` with the layers ascending by `g`
and no group, that `theta` reads `sum_v_end` and `sum_v_start`, that the aggregate groups by `g` and the plan is not the
identity; that the group of a `theta` stack is the field of `radius` and the sort the `order` channel (`o` ascending, `p`
descending), that the group of a `radius` stack is the field of `theta` and the layers sort by the detail `g`; that
`theta2` and `radius2` give the second fields `b` and `d` and the titles `a, b` and `c, d` when nothing is stacked, and
`a_start` when `theta` is stacked; that a bar with `x`, `y`, and `theta` keeps the stack of `y` grouped by `k` and
sorted descending by `g` with `theta` unstacked; and that the radial chart that reads `theta` and `radius` from `v` has
no group while a radius of another field is the group `r`. A small test asserts that the dimension channel pairs `x`
with `y` and `theta` with `radius`, refuses another channel, and refuses a null.

##### Path Grouping And Order Fields

**Tests**: `EncodingPlanPathTests.EncodingPlan_PathGroupFields_FollowVegaLiteGroupingRules`,
`EncodingPlanPathTests.EncodingPlan_OrderFields_ListTheOrderChannelWithDirections`

Asserts, as a theory of seventeen mark and encoding combinations, the grouping fields above (none for a line of two
positions, `g` for a color, `g,d1,d2` for a line with a detail pair and an `order`, `g,d1,d2,o` for the same area, `k` for
a trail, `s,k` for a line with a size and a key, `g` once when the color and the detail share it, `a,b,c,d,e,f,h` for the
stroke, fill, opacity, fill opacity, stroke opacity, stroke dash, and stroke width, none for the secondary, polar, text,
shape, angle, tooltip, href, description, and url channels and for the value definitions, `d` when the measure and the
color aggregate, the derived names of a bin and a time unit, and `g` for an area with an aggregated `order`); and that the
order fields are one entry for every definition that reads a field, in order and with its direction (`o` ascending; `a`
ascending, `b` descending, `c` ascending; none for an `order` that is a value; `max_o` descending for an aggregated one;
none without an `order` and for an empty encoding).

##### Encoding Impute And Stack Impute Are Planned

**Tests**: `EncodingPlanPathTests.EncodingPlan_Impute_PlansFieldKeyAndPathGroups`,
`EncodingPlanPathTests.EncodingPlan_StackImpute_SkipsABinnedDimension`

Asserts that the impute of `y` of a line with a color and an `order` imputes the field `v` over the key `t`, grouped by
the colors (the `order` does not group a line), carries the definition of the channel, and makes the plan something
other than the identity; that the impute of `x` has `t` as its field and `v` as its key, and that `x` is the one that
is planned when both channels impute; and that there is none for `impute: null`, a missing key channel, a key channel
that is a value, an impute on a channel other than `x` and `y` (the plan stays the identity), while an aggregated measure
is imputed through its derived name `sum_v` and a count through `__count`. For the stack impute it asserts that an
area stacked over an ordinal `t` has the field `v`, the key `t`, and the series `g` and carries the definition of the
stack, that a stacked line has the series `g,d` (its detail too), that there is none for `impute: null` (the stack stays),
without a color, for a bar (the stack stays), and for a binned dimension (the stack stays), that a horizontal area
stacks `x` over the key `y`, that an aggregate is imputed as `sum_v` and a time unit through the key `year_t`, and that
an impute object on the stacked channel does not switch the stack impute off while it plans the encoding impute too.

##### Encoding And Stack Impute Rows

**Tests**: `EncodingTransformsImputeTests.EncodingTransforms_Apply_ImputesBeforeTheStack`,
`EncodingTransformsImputeTests.EncodingTransforms_Apply_ImputeMethodMeanUsesTheGroupWindow`,
`EncodingTransformsImputeTests.EncodingTransforms_Apply_ImputeObjectParametersReachTheEngine`,
`EncodingTransformsImputeTests.EncodingTransforms_Apply_ImputeOnXUsesYAsTheKey`,
`EncodingTransformsImputeTests.EncodingTransforms_Apply_StackedPathMarkImputesMissingKeysWithZero`,
`EncodingTransformsImputeTests.EncodingTransforms_Apply_ImputeNullDisablesTheStackImpute`

Asserts the rows above: with the value 5 the six rows are `a|1|4|5`, `a|2|5|7`, `a|3|5|8`, `b|1|0|4`, `b|3|0|5`, and
`b|2|0|5` (group, key, start, end), the Raw table is the Main table, and the stacked scale reads `v_end`; the `mean` rows
`a|2|20`, `b|1|5`, and `b|3|5` follow the three input rows, the `keyvals` rows are `a|2`, `a|4`, `b|1`, `b|3`, and `b|4`
all with -1 and the `max` rows 30, 5, and 5; the impute of `x` adds `b|2|0`; a stacked area and a stacked line are both
`a|1|4|5`, `a|2|0|2`, `a|3|5|8`, `b|1|0|4`, `b|3|0|5`, `b|2|0|0` while the bar keeps its five rows, and the scale
domain of the area ends at the largest top, 8; with `impute: null` the area keeps five rows and the series `b` has only
`b|1|0|4` and `b|3|0|5`; and the sums of the aggregate give `a|1|4|7`, `a|2|0|2`, `b|1|0|4`, and `b|2|0|0` (compared
in sorted order).

##### Impute Limits, Budget, And Cancellation

**Tests**: `EncodingTransformsImputeTests.EncodingTransforms_Apply_ImputeRespectsTheRowAndCellLimits`,
`EncodingTransformsImputeTests.EncodingTransforms_Apply_ImputeBoundsSeriesTimesKeys`,
`EncodingTransformsImputeTests.EncodingTransforms_Apply_ImputePollsCancellation`

Asserts the exact budgets above: a limit of 11 rows passes with six stacked rows and 10 fails with a
`VegaLiteRenderException` at `/encoding/y` that names `MaxDataRows`; with a sort that keeps the Raw table (five rows) a
limit of 17 passes with six rows in Main and 16 fails at `/encoding/y`; the thousand `keyvals` under a limit of 500 fail
at `/encoding/y/impute`; the 100 series with 100 distinct keys give 10,000 rows under the default limit and fail at
`/encoding/y` under 5,000; and, for 5,000 rows of one series, a limit of 100 evaluation steps fails at
`/encoding/y/impute` naming `MaxEvaluationSteps` while the standalone entry throws an `OperationCanceledException` for a
token that is already canceled.

##### Impute Object Is Validated

**Tests**: `SpecParserImputeTests.SpecParser_Parse_RejectsABadImputeObjectWithItsLocation`,
`SpecParserImputeTests.SpecParser_Parse_AcceptsTheImputeForms`,
`SpecParserImputeTests.SpecParser_Parse_WarnsAboutUnknownImputeMembers`

Asserts, with `MaxScaleDomainSize` 2, that 24 wrong objects are `VegaLiteSpecException` errors located at the offending
value with the file, a positive line and column, and a message that starts with the file name: a text, a boolean, or an
array as the object (`/encoding/y/impute`), a method that is not one of the five (`mode`, a number, `MEAN`:
`/encoding/y/impute/method`), `keyvals` that are a number or a text (`/keyvals`) or hold an array or an object (`/keyvals/0`
and `/keyvals/1`), a sequence without a `stop` (`/keyvals`), with a `stop` or a `start` that is a text, or with a step
of 0
(`/keyvals/stop`, `/keyvals/start`, `/keyvals/step`), key lists and sequences of more than two values (an array of three,
`0` to `3`, `10` down to `0` by -3, and `1e300` by `1e-300`, which is refused by arithmetic before any key is
generated), a `value` that is an object or an array, and a `frame` that is a number, has one entry, a fraction, or a text
(`/frame`, `/frame/0`, `/frame/1`); that 16 valid forms give no warning (`null`, the empty object, each method with a
constant, frames with nulls, the scalar kinds of the value, key arrays with mixed kinds, and sequences with a positive
and a negative step); and that the members `groupby` and `key`, which Vega-Lite does not define for the encoding, are two
`vl-unknown-property` warnings located at their keys with the text "an impute definition".

##### Time Unit Field Names, Titles, And Band End

**Tests**: `EncodingPlanTimeTests.EncodingPlan_Create_NamesATimeUnitFieldAndItsEnd`,
`EncodingPlanTimeTests.EncodingPlan_Create_ObjectFormNames`,
`EncodingPlanTimeTests.EncodingPlan_Create_MarksWithABandPositionSpanTheirUnit`,
`EncodingPlanTimeTests.EncodingPlan_Create_BarWithTimeUnit_HasBandEnd_PointWithTimeUnit_HasNoBandEnd`,
`EncodingPlanTimeTests.EncodingPlan_Create_OrdinalTimeUnit_HasNoBandEnd`,
`EncodingPlanTimeTests.EncodingPlan_Create_SecondaryChannel_SuppressesBandEnd`,
`EncodingPlanTimeTests.EncodingPlan_Create_TitlesVerbalFunctionalPlain`

Asserts that the unit `yearmonth` on the temporal field `date` of a bar chart makes the channel read `yearmonth_date`
(the value field, the field path, and the Raw field), with `yearmonth_date_end` as its second field, `date` as its source
field, a band end, and exactly one unit in the plan (`StartName`, `EndName`, and `SourceField` as above, the same
object that the channel carries), and that the plan is no longer an identity; that, as a theory of six spellings, the
object forms and the `maxbins` form are named as above (`month_step_2_date`, `utcmonth_date` for `{"unit": "month",
"utc": true}`, `timeunit_maxbins_10_date`, and `yearmonthdate_step_7_date`), each with the end name adding `_end`; that,
as a theory over six marks, a `bar`, `rect`, and `tick` span their unit (the second field is the end) and a `point`,
`circle`, and `rule` do not; that a point gets the band end when the definition has a `bandPosition` of 0.5, the mark has
a `timeUnitBandPosition` of 0.25, or the configuration of `point` has 0.5; that an ordinal or nominal field with a unit
has the start only (the unit is the category); that a field with a unit and no type is temporal and spans, that a color
channel reads the start only, and that a position channel with a secondary channel (`x2` with its own unit
`month_until`) keeps that channel as its second end and does not span, with both units in the plan; and that the three
title modes give `date (year-month)`, `YEARMONTH(date)`, and `date` for `yearmonth`, `date (year-week-day)` for
`yearweekday`, `date (month)` and `MONTH(date)` for the step form, `date` and `TIMEUNIT(date)` for `maxbins`, and
`date (year-month)` and `UTCYEARMONTH(date)` for a UTC unit.

##### Time Unit Dimensions, Sharing, And Precedence

**Tests**: `EncodingPlanTimeTests.EncodingPlan_Create_AggregateGroupsByTimeUnitStart`,
`EncodingPlanTimeTests.EncodingPlan_Create_BinWinsOverTimeUnit`,
`EncodingPlanTimeTests.EncodingPlan_Create_SharedAndDistinctUnits`,
`EncodingPlanTimeTests.EncodingPlan_Create_StackUsesTheTimeUnitFields`,
`EncodingPlanTimeTests.EncodingPlan_Create_WithoutAnEncodingThereAreNoUnits`

Asserts that an implicit count grouped by the unit of a bar groups by the start and the end (`yearmonth_date` and
`yearmonth_date_end`: the end follows from the start, so it adds no groups, but it keeps the end field alive after the
aggregate), that the same encoding on a point groups by the start only, that an ordinal `month` unit groups by
`month_date` only, and that the measure of the bar is `__count`; that a bin wins over a unit on the same definition (no
unit in the plan, the
field `bin_maxbins_10_a`), that an aggregate of the field replaces the unit (`max_a`), and that a binned unit is ignored
(no unit, the field `date` as it is, the plan an identity); that two channels with the same field and unit share one
unit (which spans because the position channel does, while the color channel does not) and that another unit of the same
field is another one (`month_date` and `year_date`); that a stack whose layers are a time unit orders them by the unit
start descending on `y` (`month_date`) and that a time unit on the position channel is a group of the stack with its end
(`month_date` and `month_date_end`); and that a view without an encoding has no units and is an identity.

##### Time Unit Stage Order And Aggregation

**Tests**: `EncodingTransformsTimeTests.EncodingTransforms_TimeUnitThenAggregate_CountsByMonth`,
`EncodingTransformsTimeTests.EncodingTransforms_TimeUnitThenAggregate_PointHasNoEndField`,
`EncodingTransformsTimeTests.EncodingTransforms_BinThenTimeUnitThenAggregate_ThenStack`,
`EncodingTransformsTimeTests.EncodingTransforms_StackByTimeUnitField`,
`EncodingTransformsTimeTests.EncodingTransforms_SortByAggregate_KeepsTheUnitFieldsInTheRawTable`,
`EncodingTransformsTimeTests.EncodingTransforms_WithoutTimeUnits_HasNoUnits`

Asserts, over the six dates above, that the units are computed before the aggregate: the Main table has the fields
`__count`, `yearmonth_d`, and `yearmonth_d_end`, the counts are 2, 3, and 1 in first-seen order, the starts are the first
of January, February, and March 2024 and the ends the first of February, March, and April (an end is the start of the
next month), and the unit is handed on in `EncodingData.TimeUnits` with the parts `year` and `month`; that a point
aggregates by the start only (the Main fields are `__count` and `yearmonth_d`, the counts again 2, 3, and 1); that the
bin, the unit, the aggregate, and the stack apply in this order (the values 5, 15, 15 with the dates of January 10,
January 20, and February 10: the Main fields are the count, its stack start and end, the bin start and end, and
`month_d`, the rows are bin 0 with January (0, 1), bin 10 with January (1, 2), and bin 10 with February (0, 1)), with the
bin and the unit both handed on; that the stack by a unit orders the layers of the category `a` February (0, 1) then
January (1, 3) and puts `b` in February (0, 1); that, when a scale sorts by an aggregate, the Raw table keeps the six
rows with `month_d` and `month_d_end` while Main has the three groups and is another instance; and that an encoding
without a time unit hands on none.

##### Time Unit Resolution, Calendar, And Limits

**Tests**: `EncodingTransformsTimeTests.EncodingTransforms_MaxBins_HandsOnTheResolvedUnit`,
`EncodingTransformsTimeTests.EncodingTransforms_TimeUnit_FollowsTheCalendarOfTheChart`,
`EncodingTransformsTimeTests.EncodingTransforms_TimeUnit_FailureIsLocatedAtTheChannelUnit`

Asserts that the `maxbins` form is resolved against the data and the resolved unit is handed on under the literal name of
the form: the dates March 1, March 3 at 12:00, and March 6 with `{"maxbins": 5}` make the one-day unit (the parts `year`,
`month`, and `date`, the field `timeunit_maxbins_5_d`), so the three days are three groups starting at March 1, 3, and
6; that the units follow the calendar of the chart (the local day of 2024-03-10T12:00Z starts at 2024-03-10T00:00Z in
UTC, at 05:00Z in the US-like zone, and ends at 04:00Z on March 11 there, as derived above); and that a unit stage over
10,000 rows under a budget of 100 evaluation steps fails with a located `VegaLiteRenderException` at
`/encoding/x/timeUnit` that names `MaxEvaluationSteps`. The unit stage itself is verified in _Time Unit Transform
Verification Design_.
