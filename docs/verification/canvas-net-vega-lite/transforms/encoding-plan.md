### EncodingPlan Unit Verification Design

This document describes the unit-level verification strategy for the `EncodingPlan` unit in the
`DemaConsulting.CanvasNet.VegaLite.Transforms` namespace: the internal `EncodingPlan` class (`Create`, a pure analysis
of the view's encoding and mark that decides which fields the channels read, the implicit bin, aggregate, and stack, the
orientation, the titles, and whether a scale needs the rows from before the aggregate) with its `ChannelPlan`, `BinPlan`,
`BinKind`, and stack and aggregate descriptions, and the internal static `EncodingTransforms` class (`Apply` and
`ApplyStandalone`, which run the plan over the table that the explicit transforms produced and return the Raw table, the
Main table, the bins, and the evaluation). The unit reuses the engines of `AggregateTransforms` (the bin algorithm, the
aggregate, and the stack) and the rules of `StackRules`; the scales, marks, and axes consume what it produces.

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

Unit tests reside in `EncodingPlanTests.cs` (the plan) and `EncodingTransformsTests.cs` (the execution) within the
`DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Transforms` folder.

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
or label; a table that is kept when a scale needs it or dropped when it does not; a row budget that is off by one; a
bin or step limit that is not located at the channel; a cancellation that is not polled; or a result that depends on the
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
x (1, 4); horizontal: x (0, 3), y (0, -2), z (3, 4)), so that the area mark, which is drawn in a later phase, inherits
verified stacking.

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
