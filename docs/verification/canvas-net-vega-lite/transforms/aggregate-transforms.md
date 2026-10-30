### AggregateTransforms Unit Verification Design

This document describes the unit-level verification strategy for the `AggregateTransforms` unit in the
`DemaConsulting.CanvasNet.VegaLite.Transforms` namespace: the internal `AggregateTransforms` class (`Apply`, with its
aggregate, bin, window, stack, and impute files) that executes the grouped transforms of the drawn view — `aggregate`,
`joinaggregate`, `window`, `bin`, `stack`, and `impute` — eagerly over an immutable `DataTable` and returns a new
table; and the internal types of its files: `AggregateMeasures` and `MeasureCell` (the 24 aggregate and 13 window-only
operations and their accumulators), `BinAlgorithm` with `BinSettings` and `BinSpec` (the choice of the bin step and the
assignment of a value to a bin), `StackRules` (the Vega-Lite decision whether and how an encoding stacks), `WorkMeter`
(the step charge of the per-row work), and `StableSorter` (the stable merge sort of the window and stack engines). The
same engines serve the implicit pipeline of the encoding; the unit that plans it, `EncodingPlan`, has its own
verification design.

#### Verification Approach

The unit is verified by parsing small, original specification strings with the real `SpecParser`, loading the data with
the real `DataLoader`, and applying the transforms the way a render does (the `Run` helper of
`AggregateTestSupport`), or by calling the engine entries directly (`Aggregate`, `Window`, `Stack`, `BinAlgorithm`, and
`MeasureCell`) with a hand-built environment. The collaborators (`SpecParser`, `DataLoader`, `DataTable`,
`ExpressionEvaluator`, `RowTransforms` as the dispatcher, and `VegaLiteLimits`) are real; the unit's contract is how a
parsed transform turns one table into another, and no mocking or stubbing is used. The internal types are reached
through the test assembly's `InternalsVisibleTo` access. The expected values are derived by hand from the public
descriptions of the algorithms, with the derivation in the test comments:

- Aggregate over the sample `[2, 4, 4, 4, 5, 5, 7, 9]`: `count` 8, `valid` 8, `missing` 0, `distinct` 5, `sum` 40,
  `product` 2 x 4^3 x 5^2 x 7 x 9 = 201,600, `mean` 5, `variance` 32/7 (4.57143), `variancep` 4, `stdev` 2.13809,
  `stdevp` 2, `stderr` the square root of 32/56 (0.75593), `median` 4.5, `q1` 4, `q3` 5.5 (the R-7 interpolation
  between ranks), `min` 2, and `max` 9. Over `[null, "", NaN, 3]` the field has one valid and two missing values, so
  `sum` is 3 and `variance` is null. The values 1e9 + 4, 7, 13, and 16 have the mean 1e9 + 10 and the variances 30 and
  22.5, which proves the update of the variance is numerically stable. The exponential weights of `[1, 2, 3]` at 0.5
  sum to 3 + 2 x 0.5 + 1 x 0.25 = 4.25, divided by the weights 1.75 for `exponential` (17/7) and multiplied by 0.5 for
  `exponentialb` (2.125).
- Group keys are structural: a number and a text that spell the same are two groups, NaN is one group, zero and negative
  zero are one, arrays and objects are compared by content (the order of the members of an object does not matter), and
  groups appear in first-seen order with the group fields first and the measures after them.
- Bin steps (extent to start, stop, step): 10 bins of `[0, 100]` give 0, 100, 10; 20 bins give step 5 (the divisor 5 of
  100 gives 20 bins at most); `[3.7, 98.2]` gives 0 to 100 by 10; `[0, 1]` gives 0.1; `[5, 5]` gives 5 to 5.5 by 0.5;
  `[-3.2, 3.2]` gives -4 to 4 by 1; `steps` of `[1, 2, 5, 10]` for 10 bins over `[0, 100]` pick 5; base 2 gives 0 to
  112 by 16; `nice: false` keeps 3.7 to 98.2; an anchor of 5 shifts the bins to 5 to 105, and 0 then falls below the
  first bin. Assignment with the bins 0 to 100 by 10: 0 is `[0, 10)`, 9.999 is `[0, 10)`, 10 is `[10, 20)`, 100 is `[90,
  100]` (the last bin is closed), -1 is the bin at negative infinity, and 101 the bin at positive infinity.
- Window over `[10, 20, 20, 30]` sorted ascending: `row_number` 1, 2, 3, 4; `rank` 1, 2, 2, 4; `dense_rank` 1, 2, 2, 3;
  `percent_rank` 0, 1/3, 1/3, 1; `cume_dist` 0.25, 0.75, 0.75, 1; `ntile(2)` 1, 2, 2, 2; `ntile(3)` 1, 3, 3, 3. The
  default frame with the sort takes in the peers, so the running `sum` is 10, 50, 50, 80, and with `ignorePeers` 10,
  30, 50, 80. A frame of `[2, 5]` looks ahead (sum 50, 30, null, null with the counts 2, 1, 0, 0), and an empty frame
  has a null sum and a count of 0.
- Stack of `[3, 2, 5]`: `zero` gives (0, 3), (3, 5), (5, 10); with the values `[3, -4, 2, -1]` the positives stack up
  as (0, 3), (3, 5) and the negatives down as (0, -4), (-4, -5); `normalize` gives (0, 0.3), (0.3, 0.5), (0.5, 1); and
  `center` of two groups with the sums 10 and 4 starts the narrower group at (10 - 4) / 2 = 3, giving (3, 4), (4, 7).
- Impute of the series `a` (x = 1 and 3) and `b` (x = 2) appends a/2, b/1, and b/3 after the input rows with the
  default 0; the statistical methods over the group give 20, 5, 5 for `mean` and `median`, 30, 5, 5 for `max`, and
  10, 5, 5 for `min`; a frame of `[-1, 0]` gives 30, 5, and null.

The limits are verified at their exact boundaries: an aggregate of four groups over four rows needs `MaxDataRows` 8
(the input counts until the output is complete) and fails located at the transform with 7; an impute with six output
rows over three input rows needs 9 and fails with 8; 100 groups of one row with their own keys need 10,000 imputed rows,
which a limit of 5,000 refuses before allocation; 120 measures over 60,000 groups (601 cells each) and 600 medians over
60,000 rows are refused by the cell limit `TableLayout.MaxCells` (33,554,432) before any row is read; and a window of
3,000 rows with a frame of the whole table recomputed per row exhausts a `MaxEvaluationSteps` of 100,000 while the
cumulative frame, computed incrementally, fits the same budget and gives the sum 4,498,500.

Unit tests reside in `AggregateTransformsTests.cs` (the `aggregate` and `bin` transforms and the `WorkMeter`),
`AggregateTransformsJoinAggregateTests.cs`, `AggregateTransformsWindowTests.cs`, `AggregateTransformsStackTests.cs`,
`AggregateTransformsImputeTests.cs`, `AggregateMeasuresTests.cs` (the registry and the accumulators),
`BinAlgorithmTests.cs`, `StackRulesTests.cs`, and `StableSorterTests.cs`, with the shared helpers in
`AggregateTestSupport.cs`, all within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Transforms` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Files**: None; every specification is an in-memory string, and no file or network is read
- **Isolation**: Each test parses its own specification and builds its own loader, evaluator, and context. The
  cancellation scenarios call the unit with a token that is already canceled; the culture scenarios run the same
  pipelines under `tr-TR` and `de-DE` and compare the results with those of the invariant culture

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any wrong row, row order, field, field order, group, cell kind, or value; a result that depends on the culture
or differs between two runs; a limit that is off by one or that is checked after the allocation it bounds; a failure
without the location of the transform; a missing or repeated charge to the step budget; a cancellation that is not
polled in a loop or that is wrapped in another exception; or an exception of the wrong type constitutes a failure.

#### Unit-Level Test Scenarios

##### Operation Registry And Accumulators

**Tests**: `AggregateMeasuresTests.AggregateMeasures_TryParse_AcceptsEveryAggregateName`,
`AggregateMeasuresTests.AggregateMeasures_TryParseWindow_AcceptsEveryWindowName`,
`AggregateMeasuresTests.AggregateMeasures_TryParse_RejectsUnknownNames`,
`AggregateMeasuresTests.AggregateMeasures_Cell_MatchesTheHandDerivedStatistics`,
`AggregateMeasuresTests.AggregateMeasures_Cell_ClassifiesValidMissingAndNaNValues`,
`AggregateMeasuresTests.AggregateMeasures_Cell_PopulationSpreadOfSingleValueIsZero`,
`AggregateMeasuresTests.AggregateMeasures_Cell_VarianceIsNumericallyStable`,
`AggregateMeasuresTests.AggregateMeasures_Cell_QuartilesInterpolateBetweenRanks`,
`AggregateMeasuresTests.AggregateMeasures_Cell_ExponentialOperationsWeightRecentValues`

Asserts that the 24 aggregate operation names (`average` being an alias of `mean`) and the 13 window operation names
parse, that each canonical name is distinct and parses back, and that unknown names are refused; the statistics of the
sample above to five decimals; that null and the empty text are missing, NaN is neither missing nor valid, `count` and
`distinct` see every row, and the other operations see the valid values only; that a single valid value gives a population
variance and deviation of 0 (and none gives null) while the sample variance, deviation, and standard error need two; that
the variance is stable for a large
common offset; the R-7 quartiles of `[1, 2, 3, 4, 5]` (2, 3, 4), `[1, 2]` (1.25, 1.5, 1.75), and `[9, 1, 5, 3]` (2.5,
4, 6) whatever the row order; and the exponential vectors above.

##### Values Are Read Like JavaScript

**Tests**: `AggregateMeasuresTests.AggregateMeasures_Cell_NumericTextIsReadAsANumber`,
`AggregateMeasuresTests.AggregateMeasures_Cell_OverflowGivesInfinity`,
`AggregateMeasuresTests.AggregateMeasures_Cell_MinAndMaxCompareLikeJavaScript`,
`AggregateMeasuresTests.AggregateMeasures_Cell_ArgMinAndArgMaxKeepTheFirstRowOfATie`,
`AggregateMeasuresTests.AggregateMeasures_Cell_DistinctCountsTextsAndValuesKeepsEveryValue`,
`AggregateMeasuresTests.AggregateMeasures_Cell_WithoutValidValuesCountsZeroAndOtherwiseIsNull`

Asserts that numeric text, booleans, and dates add as numbers (`"1"`, `" 2 "`, `true`, and the date 4 sum to 8) while
text that is not a number poisons a sum with NaN and is skipped by the order statistics; that sums and products overflow
to infinity; that `min` and `max` compare numbers numerically, texts by code unit (`"10"` is below `"9"`), dates by
instant, and mixed kinds by conversion, returning the original value; that `argmin` and `argmax` return the row of the
first of a tie (rows 1 and 3 of `[5, 3, 3, 9, 9]`); that `distinct` counts texts, so `1` and `"1"` are one, and
`values` keeps every value in row order; and that over no valid value the counts are zero and every other operation is
null.

##### Bootstrap Intervals Are Deterministic

**Tests**: `AggregateMeasuresTests.AggregateMeasures_Cell_BootstrapCiIsDeterministicAndBounded`,
`AggregateMeasuresTests.AggregateMeasures_Cell_BootstrapCiIgnoresTheCulture`,
`AggregateMeasuresTests.AggregateMeasures_GroupSeed_DiffersByGroupAndIsStable`,
`AggregateTransformsTests.AggregateTransforms_Aggregate_BootstrapIntervalIsSeededByThePointer`

Asserts that `ci0` and `ci1` of 10, 20, 30, 40, 50 are repeatable for one seed, bracket the mean 30, and stay within 10
and 50; that the result is identical under `tr-TR` and `de-DE`; that the seed of a group differs by group and is stable;
and that two transforms of one specification, at different pointers, give different intervals while each repeats.

##### Aggregate Groups And Names The Rows

**Tests**: `AggregateTransformsTests.AggregateTransforms_Aggregate_GroupsByStructuralKeysInFirstSeenOrder`,
`AggregateTransformsTests.AggregateTransforms_Aggregate_KeysAreStructuralNotTextual`,
`AggregateTransformsTests.AggregateTransforms_Aggregate_NaNAndSignedZeroFormOneGroupEach`,
`AggregateTransformsTests.AggregateTransforms_Aggregate_ArraysAndObjectsAreComparedByContent`,
`AggregateTransformsTests.AggregateTransforms_Aggregate_CountWithoutFieldAndAsDefaults`,
`AggregateTransformsTests.AggregateTransforms_Aggregate_EmptyInputYieldsNoRows`,
`AggregateTransformsTests.AggregateTransforms_Aggregate_NoGroupByYieldsOneRow`,
`AggregateTransformsTests.AggregateTransforms_Aggregate_NestedGroupByPathStaysNested`,
`AggregateTransformsTests.AggregateTransforms_Aggregate_ComputesEveryOperation`,
`AggregateTransformsTests.AggregateTransforms_Aggregate_AMissingFieldIsMissingInEveryRow`,
`AggregateTransformsTests.AggregateTransforms_Aggregate_ArgOperationsReturnTheRowAndValuesTheArray`,
`AggregateTransformsTests.AggregateTransforms_Aggregate_ExponentialUsesTheParameter`

Asserts the group rules above, the output names (`count` for a count of rows, `op_field` otherwise with `average` named
`mean`, an `as` that overrides, the fields in ordinal order, a later write of a name winning), that an empty input has
no group and so no row, with or without a group-by, that without a group-by the output is one row, that a nested
group-by path stays nested, every operation through the transform over the sample, that a field no row has gives the
row count for `count` and null otherwise, that `argmax` and `argmin` return the whole row as an object and `values` the
array of field values, and that `exponential` uses the specified parameter and the default 0.5.

##### Bin Transform Assigns Boundaries

**Tests**: `AggregateTransformsTests.AggregateTransforms_Bin_AssignsBoundariesAndNullsForInvalidValues`,
`AggregateTransformsTests.AggregateTransforms_Bin_NamesTheOutputFields`,
`AggregateTransformsTests.AggregateTransforms_Bin_DefaultsToTenBinsAndHonorsMaxbinsAndStep`,
`AggregateTransformsTests.AggregateTransforms_Bin_ExplicitExtentSendsOutliersToInfinity`,
`AggregateTransformsTests.AggregateTransforms_Bin_AnchorShiftsTheBins`,
`AggregateTransformsTests.AggregateTransforms_Bin_NonFiniteValuesAreIgnored`,
`AggregateTransformsTests.AggregateTransforms_Bin_NoValidValuesGiveNulls`

Asserts the lower and upper boundary of every row for the extent 0 to 100 by 10 (a value on an edge, the closed last
bin, and null for null, text that is not a number, and the empty text); that one name in `as` gives the name and
`name_end`, two names are used as given, none gives `bin0` and `bin1`, and an existing field is replaced; that the
default is ten bins (a hundred-wide extent has step 10) and `maxbins` or `step` change it; that an explicit `extent`
sends outliers to the infinite bins; the anchor shift above; that infinite values and NaN stay out of the extent (the
finite 1 and 2 with ten bins have step 0.1) and get null; and that a field with no valid value bins to null everywhere
without failing.

##### Bin Algorithm Chooses The Step

**Tests**: `BinAlgorithmTests.BinAlgorithm_Compute_MatchesHandDerivedSteps`,
`BinAlgorithmTests.BinAlgorithm_Compute_DefaultsToTwentyBins`,
`BinAlgorithmTests.BinAlgorithm_Compute_UsesAnExactStep`,
`BinAlgorithmTests.BinAlgorithm_Compute_ChoosesTheLargestAllowedStepBelowTheTarget`,
`BinAlgorithmTests.BinAlgorithm_Compute_HonorsMinStepDivideAndBase`,
`BinAlgorithmTests.BinAlgorithm_Compute_NiceFalseKeepsTheExtent`,
`BinAlgorithmTests.BinAlgorithm_Compute_MovesTheStartDownWhenToleranceRoundsAboveTheMinimum`,
`BinAlgorithmTests.BinAlgorithm_Compute_DegenerateExtentUsesTheMagnitude`,
`BinAlgorithmTests.BinAlgorithm_Align_AnchorShiftsTheBins`,
`BinAlgorithmTests.BinAlgorithm_Align_RoundsTheStopUpToAWholeStep`,
`BinAlgorithmTests.BinAlgorithm_Resolve_AppliesTheAnchor`,
`BinAlgorithmTests.BinAlgorithm_Assign_PlacesValuesInBins`,
`BinAlgorithmTests.BinAlgorithm_Assign_ToleranceKeepsABoundaryValueInTheBinAbove`,
`BinAlgorithmTests.BinAlgorithm_Assign_AnchoredBinsLeaveSmallValuesBelowTheStart`,
`BinAlgorithmTests.BinAlgorithm_BinSettingsRead_ReadsEveryParameter`

Asserts, as a theory of 13 extents, the start, stop, and step above (also 0 to 7 gives step 1, 0 to 3 gives 0.5,
0 to 1,000,000 gives 100,000, -50 to 50 gives 10, and 0 to 0 gives 0 to 0.1); that the algorithm itself defaults to 20
bins (step 5 over 0 to 100) while a transform asks for ten; that an exact `step` of 25 gives four bins over 0 to 100 and
rounds `[3, 61]` by 20 out to 0 to 80; the `steps`, `minstep`, `divide` (a divisor of 2 halves 10 to 5, a divisor of 10
keeps it), and base vectors; that the start moves down a step when the rounding tolerance of 2.9995 / 0.1 would put the
first boundary above the minimum; that a single value uses its magnitude (50 gives 50 to 55 by 5); that the anchor
shifts by `anchor - (start + step x floor((anchor - start) / step))` (5 gives 5 to 105, 23 gives 3 to 103, 40 changes
nothing); that the tolerance keeps 0.3 in the bin that starts at 0.3 although 0.3 / 0.1 is 2.9999999999999996; and
that every bin parameter is read, with `maxbins` defaulting to the caller's value (10 for a position channel) and to 20
when a `step` is given.

##### Bins Fail Closed

**Tests**: `BinAlgorithmTests.BinAlgorithm_Compute_TinyHugeOrInvalidExtentsFail`,
`BinAlgorithmTests.BinAlgorithm_Compute_MoreBinsThanTheLimitFails`,
`AggregateTransformsTests.AggregateTransforms_Bin_UnbinnableExtentsAndTooManyBinsFail`,
`AggregateTransformsTests.AggregateTransforms_Bin_KeepsRowsAndChargesWork`,
`AggregateTransformsTests.AggregateTransforms_Bin_CancellationIsPolled`

Asserts that a subnormal, an overflowing, a non-finite, and a reversed extent fail located with "The values cannot be
binned"; that 101 bins over a limit of 100 and a tiny exact step fail naming `the limit of 100
(VegaLiteLimits.MaxScaleDomainSize)` while exactly 100 bins pass with step 0.01; that a bin transform leaves the rows
and the row budget alone, charges its work, and bounds its output names; and that the cancellation token is polled.

##### Joinaggregate Writes Group Measures To Every Row

**Tests**: `AggregateTransformsJoinAggregateTests.AggregateTransforms_JoinAggregate_WritesGroupMeasuresToEveryRow`,
`AggregateTransformsJoinAggregateTests.AggregateTransforms_JoinAggregate_WholeTableAndNaming`,
`AggregateTransformsJoinAggregateTests.AggregateTransforms_JoinAggregate_ValuesArraysAreSharedWithinAGroup`,
`AggregateTransformsJoinAggregateTests.AggregateTransforms_JoinAggregate_RowMeasuresAndQuantiles`,
`AggregateTransformsJoinAggregateTests.AggregateTransforms_JoinAggregate_EmptyInputGivesNoRows`,
`AggregateTransformsJoinAggregateTests.AggregateTransforms_JoinAggregate_DoesNotChangeTheRowBudget`

Asserts that every row keeps its place and gets the measures of its group, that without a group-by the mean of 1 to 5
(3) is written as `mean_v` to all rows and an `as` replaces an existing field, that the array of a `values` measure is
one instance shared by the rows of a group (and a different one for another group), that the row-valued measures and
quantiles are computed once per group, that an empty table gives an empty table with the new fields, and that the row
budget is unchanged.

##### Window Ranks And Frames

**Tests**: `AggregateTransformsWindowTests.AggregateTransforms_Window_RankingOperationsFollowVegaOnPeers`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_SingleRowAndUnsortedRanks`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_CumeDistCountsPeersOfTheFirstRows`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_DefaultNamesFrameAndReplacement`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_FramesIncludePeersUnlessIgnored`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_PartitionsAreSortedSeparatelyAndOutputKeepsInputOrder`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_SortIsStableNullFirstAndDescendingReverses`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_LagAndLeadReadNeighbors`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_PositionalValuesReadTheFrame`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_PreviousAndNextValueSkipNulls`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_SlidingFrameAverages`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_FramesAreClampedAndMayBeEmpty`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_WholePartitionFrame`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_ValuesFollowTheFrameAndPeersShareTheArray`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_AggregateOperationsOverTheWholePartition`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_ComposesWithFilter`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_EmptyInputGivesNoRows`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_DoesNotChangeTheRowBudget`

Asserts the ranking vectors above; that `percent_rank` of a one-row partition is NaN (0 / 0) and that without a sort no
two rows are peers, so `rank` is the row number; that peers at the start of a partition get the cumulative distribution
of their whole group; the default names (`op` or `op_field`) and the default frame `[null, 0]`; the peer-widened frame
against `ignorePeers`; that partitions sort separately and the output keeps the input order; that the sort is stable,
puts null first, and reverses for descending, with several fields in order of significance; that `lag` and `lead` default
to offset 1, give null beyond the partition, and do not overflow at the largest integer; `first_value`, `last_value`,
and `nth_value` over `[-1, 1]` clamped at the ends; that `prev_value` and `next_value` take the nearest non-null value
and do not carry across partitions; the sliding average; the clamped and empty frames above; the whole-partition frame
per partition; that `values` follows the frame and peers share one array; aggregate operations over a partition; that
the rank of a descending sort can drive a following `filter`; and that an empty table gives an empty table with the new
fields and the row budget does not change.

##### Window Cumulative Fast Path Equals Recomputation

**Tests**: `AggregateTransformsWindowTests.AggregateTransforms_Window_CumulativeFastPathEqualsRecomputation`

Asserts, over 200 rows of `(i x 37) mod 11 - 5` with a null in every seventh row (so with many repeated values and
peers), that for the 13 operations `sum`, `mean`, `count`, `valid`, `missing`, `min`, `max`, `median`, `q1`, `variance`,
`stdev`, `distinct`, and `values`, with and without a sort, the incremental computation of the frame `[null, 0]` gives
exactly the rows of the frame `[-100000, 0]`, which is recomputed from scratch for every row.

##### Stack Offsets

**Tests**: `AggregateTransformsStackTests.AggregateTransforms_Stack_ZeroOffsetStacksFromZero`,
`AggregateTransformsStackTests.AggregateTransforms_Stack_NegativeValuesStackSeparatelyDownwards`,
`AggregateTransformsStackTests.AggregateTransforms_Stack_NormalizeScalesEveryGroupToOne`,
`AggregateTransformsStackTests.AggregateTransforms_Stack_CenterOffsetCentersOnTheWidestGroup`,
`AggregateTransformsStackTests.AggregateTransforms_Stack_GroupsStackIndependently`,
`AggregateTransformsStackTests.AggregateTransforms_Stack_SortOrdersTheStackAndTiesKeepInputOrder`,
`AggregateTransformsStackTests.AggregateTransforms_Stack_NamesTheOutputFields`,
`AggregateTransformsStackTests.AggregateTransforms_Stack_ValuesAreReadLikeUnaryPlus`,
`AggregateTransformsStackTests.AggregateTransforms_Stack_ZeroSumsAndNaNFollowIeee`,
`AggregateTransformsStackTests.AggregateTransforms_Stack_EmptyInputGivesNoRows`,
`AggregateTransformsStackTests.AggregateTransforms_Stack_EngineStacksWithGivenNames`

Asserts the stack vectors above with the default names `v_start` and `v_end` and the rows unchanged; that negative values
stack by absolute value under `normalize`; that interleaved groups stack independently and a structural group key
separates `1` from `"1"`; that the first row of the `sort` is at the bottom, equal sort values stack in input order, and
the output keeps the input row order; one name in `as` gives the name and `name_end`; that null reads as 0, numeric text
as its number, other text as NaN, which poisons the positive stack from that row on, and a negative value is not
poisoned; that a group with a zero sum under `normalize` has NaN ends (0 times infinity) and a NaN in a group makes every
end of it NaN under `center` without touching other groups; that an empty table gives an empty table with the two
fields; and that the engine entry used by the encoding stacks with group-by, a descending sort, and explicit names
without counting groups in the row budget.

##### Impute Fills Missing Keys

**Tests**: `AggregateTransformsImputeTests.AggregateTransforms_Impute_AppendsMissingKeysAfterTheInputRows`,
`AggregateTransformsImputeTests.AggregateTransforms_Impute_KeyValuesExtendTheDomain`,
`AggregateTransformsImputeTests.AggregateTransforms_Impute_WithoutGroupByThereIsOneSeries`,
`AggregateTransformsImputeTests.AggregateTransforms_Impute_AddsAMissingField`,
`AggregateTransformsImputeTests.AggregateTransforms_Impute_ConstantValueForms`,
`AggregateTransformsImputeTests.AggregateTransforms_Impute_StatisticalMethodsUseTheGroup`,
`AggregateTransformsImputeTests.AggregateTransforms_Impute_FrameIsOverTheGroupInDataOrder`,
`AggregateTransformsImputeTests.AggregateTransforms_Impute_StatisticalMethodsReplaceExistingNulls`,
`AggregateTransformsImputeTests.AggregateTransforms_Impute_KeysAreStructuralAndDuplicatesCountOnce`,
`AggregateTransformsImputeTests.AggregateTransforms_Impute_EmptyInputGivesNoRows`

Asserts that the rows `a|1|10|1`, `a|3|30|1`, `b|2|5|1` are followed by `a|2|0|null`, `b|1|0|null`, `b|3|0|null` (the
keys 1, 3, 2 in first-seen order, group order then key order, the other fields of an imputed row null); that `keyvals`
come first as an array (a lacks 2 and 4, b lacks 1, 3, and 4 for `[1, 2, 3, 4]`) or as a sequence with a start, stop,
step, a negative step, and a step that points away (empty), with the observed keys after them; that without a group-by
there is one series; that a field no row has is added; the constant forms (a text, a number, and an explicit null that is
not replaced by 0); the statistical vectors above; that with a statistical method an existing null is replaced as well
while with a constant it is left alone; that `1` and `"1"` are different keys, a key twice in a group counts once, and
nothing is added when every group has every key; and that an empty table is returned as it is.

##### Limits Bound Every Allocation

**Tests**: `AggregateTransformsTests.AggregateTransforms_Aggregate_GroupLimitIsALocatedFailure`,
`AggregateTransformsTests.AggregateTransforms_Aggregate_ReleasesTheInputRowsOnlyWhenAsked`,
`AggregateTransformsTests.AggregateTransforms_Aggregate_GroupStateIsBoundedByTheCellLimit`,
`AggregateTransformsTests.AggregateTransforms_Aggregate_ValueStateIsBoundedUpFront`,
`AggregateTransformsTests.AggregateTransforms_Aggregate_FieldLimitIsALocatedFailure`,
`AggregateTransformsTests.AggregateTransforms_Aggregate_LongTextsRespectMaxStringLength`,
`AggregateTransformsJoinAggregateTests.AggregateTransforms_JoinAggregate_GroupStateIsBoundedByTheCellLimit`,
`AggregateTransformsJoinAggregateTests.AggregateTransforms_JoinAggregate_ValueAndFieldLimitsAreLocated`,
`AggregateTransformsJoinAggregateTests.AggregateTransforms_JoinAggregate_LongNamesRespectMaxStringLength`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_ValuesArraysAreBounded`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_ValueStateIsBoundedUpFront`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_FieldAndStringLimitsAreLocated`,
`AggregateTransformsStackTests.AggregateTransforms_Stack_FieldAndStringLimitsAreLocated`,
`AggregateTransformsImputeTests.AggregateTransforms_Impute_OutputRowsAreBoundedByTheRowBudget`,
`AggregateTransformsImputeTests.AggregateTransforms_Impute_GroupsTimesKeysIsBoundedBeforeAllocation`,
`AggregateTransformsImputeTests.AggregateTransforms_Impute_HugeKeySequencesAreRejectedUpFront`,
`AggregateTransformsImputeTests.AggregateTransforms_Impute_LongKeyListsAreRejected`,
`AggregateTransformsImputeTests.AggregateTransforms_Impute_FieldAndStringLimitsAreLocated`

Asserts the exact boundaries above, each failure located at `/transform/0` and naming its limit (`(MaxDataRows)`,
`the limit of 33554432 cells (TableLayout.MaxCells)`, `the limit of 33554432 values (TableLayout.MaxCells)`, or
`MaxStringLength`); that an aggregate of five rows into three groups holds 3 rows of the budget after the input is
released and 8 when the caller keeps the input; that the cumulative `values` arrays of 100 rows (5,050 values) exceed a
row limit of 1,000 while one shared array of the whole partition fits; that sequences of 1e12 and 1e300 / 1e-300 keys
are refused with `the limit of 1000000 (MaxDataRows)` before they are materialized, and 2,000 keys under a limit of
1,000 as well; and that more output fields than the field limit and a text longer than the string limit are refused
before any row is read.

##### Work Is Charged To The Step Budget

**Tests**: `AggregateTransformsTests.AggregateTransforms_Aggregate_StepBudgetExhaustionIsLocated`,
`AggregateTransformsTests.AggregateTransforms_Aggregate_RowWorkIsCharged`,
`AggregateTransformsTests.AggregateTransforms_Aggregate_LongGroupTextIsChargedPerRow`,
`AggregateTransformsTests.AggregateTransforms_WorkMeter_PaysWholeBlocksAndCarriesTheRemainder`,
`AggregateMeasuresTests.AggregateMeasures_Cell_WorkIsChargedToTheStepBudget`,
`AggregateMeasuresTests.AggregateMeasures_Cell_LongTextRespectsTheStringLimit`,
`AggregateTransformsJoinAggregateTests.AggregateTransforms_JoinAggregate_WorkIsChargedToTheStepBudget`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_WideFramesAreChargedToTheStepBudget`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_SortingIsChargedToTheStepBudget`,
`AggregateTransformsStackTests.AggregateTransforms_Stack_WorkIsChargedToTheStepBudget`,
`AggregateTransformsImputeTests.AggregateTransforms_Impute_WorkIsChargedToTheStepBudget`

Asserts that the meter pays one step for every complete block of 64 elements, carries the remainder, and fails naming
`VegaLiteLimits.MaxEvaluationSteps` when the budget is spent; that the rows, the sort of a median (n x ceil(log2 n)
elements), the bootstrap (1,000 samples per value), the distinct keys, and the conversion of text to a number are
charged, so a small budget fails located at the transform and a larger one succeeds (a rank sort of 5,000 rows costs
5,000 x 13 / 64 = 1,015 steps and fails under 500); and that a window frame recomputed for every row is charged per frame
row (one step per 16 rows), which the wide-frame vector above shows; and that hashing the text of a group value is charged
(200 rows with a 2,000-character group text cost 31 steps each: a budget of 100,000 passes and 1,000 fails).

##### Stable Sort

**Tests**: `StableSorterTests.StableSorter_Sort_OrdersAndKeepsTiesInOrder`,
`StableSorterTests.StableSorter_Sort_SortsOnlyTheGivenRange`,
`StableSorterTests.StableSorter_Sort_TinyRangesNeedNoComparison`,
`StableSorterTests.StableSorter_Sort_StopsOnCancellationAndPropagatesExceptions`

Asserts, for sizes that exercise every merge width, that items with the key `(i x 7919) mod 13` come out ordered with
ties in their original order, that only the given range changes, that ranges of zero or one item never call the
comparison, and that a canceled token and an exception of the comparison propagate unwrapped.

##### Stack Rules Of The Encoding

**Tests**: `StackRulesTests.StackRules_Decide_FindsTheStackedChannelOffsetAndStackByFields`,
`StackRulesTests.StackRules_Decide_ReturnsNullWhenNothingIsStacked`,
`StackRulesTests.StackRules_Decide_ListsTheStackByDefinitionsInChannelOrder`,
`StackRulesTests.StackRules_Decide_HandlesMissingArguments`,
`StackRulesTests.StackRules_Helpers_ClassifyDefinitionsAndSwitches`

Asserts the Vega-Lite decision: which channel stacks and with which offset (the default for `bar`, `area`, and `arc`),
that the fields of the non-position channels in channel order (array items included) split a group, and that nothing
stacks for an explicit `null` or `false`, a mark that does not stack, an aggregate without a field to split, a range
(`x2` or `y2`), or a channel that cannot stack; that a missing mark is refused and a missing or non-object encoding
does not stack; and the small predicates for a field definition, its defaulted type, and what counts as switched on.

##### Cancellation, Determinism, And Culture

**Tests**: `AggregateTransformsTests.AggregateTransforms_Aggregate_CancellationIsPolled`,
`AggregateTransformsTests.AggregateTransforms_Apply_IsDeterministicAndCultureInvariant`,
`AggregateTransformsJoinAggregateTests.AggregateTransforms_JoinAggregate_CancellationIsPolled`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_CancellationIsPolled`,
`AggregateTransformsWindowTests.AggregateTransforms_Window_IsDeterministicAndCultureInvariant`,
`AggregateTransformsStackTests.AggregateTransforms_Stack_CancellationIsPolled`,
`AggregateTransformsStackTests.AggregateTransforms_Stack_IsDeterministicAndCultureInvariant`,
`AggregateTransformsImputeTests.AggregateTransforms_Impute_CancellationIsPolled`,
`AggregateTransformsImputeTests.AggregateTransforms_GroupedTransforms_AreDeterministicAndCultureInvariant`,
`AggregateMeasuresTests.AggregateMeasures_Cell_BootstrapPollsCancellation`

Asserts that a token that is already canceled stops every transform with an `OperationCanceledException` that is not
wrapped (every loop of the aggregate is reached directly, the window also from a sink that cancels after the first
result and over a table of more than 1,024 rows, which is stopped before the end), and that every transform, and a
pipeline that feeds each into the next, gives the same table on a second run and under `tr-TR` and `de-DE`.
