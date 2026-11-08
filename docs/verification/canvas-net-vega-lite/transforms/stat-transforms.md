### StatTransforms Unit Verification Design

This document describes the unit-level verification strategy for the `StatTransforms` unit in the
`DemaConsulting.CanvasNet.VegaLite.Transforms` namespace: the internal `StatTransforms` class (`Apply`, with its
regression, loess, density, quantile, and extent parts) and the numeric helpers it uses - `RegressionFit` (the
regression models), `CurveSampler` (the adaptive sampling of a curve), `LoessFit` (the locally weighted regression),
`KernelDensity` (the Gaussian kernel density, the cumulative distribution, and Scott's rule), and
`StatisticsPrimitives`
(the JavaScript number conversion, the quantiles, the deviation, the extent, and the range length). The unit takes an
immutable `DataTable`, the transform entry of the specification, and the `TransformContext` of the call, and returns
the
table that the transform generates (or, for `extent`, the same table with a parameter set).

#### Verification Approach

The numeric helpers are verified directly, with hand-built `DataValue` arrays, and the transforms are verified through
`StatTransforms.Apply` and through `RowTransforms.Apply` (the dispatcher) with small, original data and specification
strings parsed by the real `SpecParser`. The collaborators (`SpecParser`, `DataTable`, `DataLoader` for the row budget,
`ExpressionEvaluator` for the step budget, `TableLayout`, and `VegaLiteLimits`) are real and no mocking is used; the
internal types are reached through the test assembly's `InternalsVisibleTo` access, and the invariant culture is used
for every number in the tests (`StatTestSupport.cs` builds the rows).

**Expected values are derived from mathematics, not from the output of the reference.** A regression of points that lie
on a line has the slope and intercept of the line and `rSquared` 1; a quadratic, a cubic, an exponential, a power, and
a
logarithmic law are fitted back from exact samples; a density of a few support points is the mean of Gaussian bumps
(`exp(-z^2 / 2) / (sqrt(2 pi) h)`) and is computed by hand at the checked points, a symmetric support gives a symmetric
density, and a density on a grid integrates to 1 within the trapezoid error; the cumulative distribution of two
symmetric points is one half at the middle; the quantiles of `1..5` are the R-7 values (`0.5` is 3, `0.25` is 2); the
extent of a column is its minimum and maximum. A **few parity tests** (named `...MatchTheReference` or
`...HasTheReferenceNumberOfPoints`) compare with numbers that the Vega 6 reference produces for the same inputs (the
loess of a noisy sample, the density with Scott's bandwidth, and the number of points of an adaptively sampled cubic, a
quadratic, an exponential, and a sine) **at the tolerance of the test** (1e-9 or tighter for the closed forms of the
models, and the tolerance of the reference's printed digits for the others), as a guard against a drifting port; they
are never the only check of a behavior. The structure of the output (the group fields, the field names, the order, and
the number of rows) is derived from the rules in the requirements.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline, on every target framework
- **Mocking**: None required
- **Isolation**: Each test builds its own table, context, and budgets; no files are read, no clock, zone, culture, or
  random source is used, and no state is shared between tests

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A coefficient, a predicted value, a density, or a quantile that differs from the derived one beyond its
tolerance; a group that is split, merged, or ordered differently from Vega's `partition`; a missing, extra, or misnamed
field; a limit that does not fail closed at the transform or that is reported without the budget being returned; a
warning that is missing or not located; or a result that differs between two runs constitutes a failure.

#### Unit-Level Test Scenarios

##### Dispatch And Arguments

**Tests**: `StatTransformsQuantileTests.StatTransforms_Apply_RejectsNullArgumentsAndIgnoresOtherTransforms`,
`RowTransformsTests.RowTransforms_Apply_StatisticalTransformReplacesTheTableAndTheNextTransformRuns`

Asserts that a null table, entry, operation, or context is an `ArgumentNullException`, that an entry whose operation is
not one of the five returns the input table itself, and that `RowTransforms.Apply` hands each of the five to the unit
and runs the transform that follows on the table that a statistical transform generated.

##### Regression

**Tests**: `StatTransformsRegressionTests.StatTransforms_Regression_LinearGivesTheEndPointsOfTheLine`,
`StatTransforms_Regression_AsRenamesTheOutputFieldsAndNamesAreLiteral`,
`StatTransforms_Regression_ReadsNestedFieldsAndNamesTheOutputLiterally`,
`StatTransforms_Regression_ExtentSetsTheDomainOfTheLine`,
`StatTransforms_Regression_ExtentBeforeTheRegressionIsStillTheRegressionsOption`,
`StatTransforms_Regression_ParamsGiveTheCoefficientsAndRSquared`,
`StatTransforms_Regression_PolySamplesTheCurveAdaptively`,
`StatTransforms_Regression_CurveMethodsPassThroughTheirData`,
`StatTransforms_Regression_LogIgnoresANonPositiveExtent`

Asserts that points on the line `y = 2x + 1` give the two end points of the extent (x of the group or the `extent`
option) and the field names `x` and `y` by default, that `as` renames them and a name with a dot is one literal field,
that a nested `on` field is read through its path, that `params: true` gives one row with the `keys` array, `coef` `[1,
2]`, and `rSquared` 1, that a polynomial or a curve method generates the adaptively sampled curve (more than the
minimum
grid, the end points exactly the extent), that a log regression ignores an extent that starts at or below zero (with
the
warning), and that the `extent` that is the option of a regression is not read as an `extent` transform.

`RegressionFitTests.RegressionFit_Fit_LinearReproducesALineExactly`, `..._LinearOnNoisyDataMatchesTheNormalEquations`,
`..._LogReproducesALogarithmicCurve`, `..._ExpReproducesAnExponentialCurve`, `..._PowReproducesAPowerCurve`,
`..._QuadReproducesAParabola`, `..._PolyReproducesACubic`, `..._LowPolynomialOrdersUseTheDedicatedModels`,
`..._ConstantChecksTheXOnlyForNull`, `..._IgnoresPairsWithoutNumbers`, `..._LinearOnConstantXHasZeroSlope`,
`..._WithoutValidPointsGivesNaNRSquared`, `..._DegreesOfFreedom_AreTheParametersOfTheMethod`, and
`..._RejectsInvalidArguments` assert the coefficients (from exact samples, within 1e-9), `rSquared`, the delegation of
the orders 0 to 2, the validity rule of a pair, and the degrees of freedom of each method.

##### Curve Sampling

**Tests**: `CurveSamplerTests.CurveSampler_Sample_EqualStepCountsGiveAUniformGrid`,
`..._AFractionalUniformStepCountFollowsTheJavaScriptLoop`, `..._AStraightLineKeepsTheMinimumGrid`,
`..._ABendingCurveIsSubdividedWhereItBends`, `..._CubicCurveHasTheReferenceNumberOfPoints`,
`..._StepCountsAreNormalizedAsInJavaScript`, `..._ANaNExtentGivesNaNSamples`, `..._AReversedExtentKeepsTheMinimumGrid`,
`..._ReportsProgressAndStopsWhenTheCallbackThrows`, `CurveSampler_MaximumPoints_BoundsTheSample`

Asserts that equal minimum and maximum steps give the uniform grid `min + i * (max - min) / steps`, that a straight
line
keeps the minimum grid, that a bending curve is subdivided where it bends and not where it is straight, the number of
points of the reference curves, the normalization of the step counts, the NaN and reversed extents, that the progress
callback sees every new point and can stop the sampling, and that `MaximumPoints` is an upper bound of the sample.

##### Loess

**Tests**: `StatTransformsRegressionTests.StatTransforms_Loess_OfALineIsTheLine`,
`StatTransforms_Loess_GroupsAreSortedByXAndRenamed`, `StatTransforms_Loess_DefaultBandwidthAndASinglePoint`;
`LoessFitTests.LoessFit_Fit_OfALineIsTheLine`, `..._OfNoisyDataMatchesTheReference`, `..._DownWeightsAnOutlier`,
`..._OfOneTwoAndThreePointsFollowsTheReference`, `..._MergesRepeatedXAsTheReferenceDoes`,
`..._SortsTheRowsAndIgnoresInvalidPairs`, `..._ReportsProgressAndEstimatesTheCost`, `..._RejectsInvalidArguments`

Asserts that the loess of points on a line is the line (every bandwidth), that an outlier is down-weighted by the
robustness iterations, that the groups are sorted by x with the default fields `x` and `y` renamed by `as`, that the
default bandwidth is 0.3, that one point gives NaN, that repeated x are merged as in the reference, and that the sample
of a noisy series matches the reference within 1e-9.

##### Density

**Tests**: `StatTransformsDensityTests.StatTransforms_Density_SharedGridHasTheClosedFormDensities`,
`..._IntegratesToOneAndSharedDefaultsToTwoHundredSteps`, `..._OfASymmetricSupportIsSymmetric`,
`..._CumulativeMatchesTheReference`, `..._CountsMultiplyByTheGroupSize`,
`..._SharedGroupsUseOneGridAndTheirOwnSupport`,
`..._IndependentGroupsUseTheirOwnExtent`, `..._StepOptionsBoundTheSample`, `..._WithoutABandwidthUsesScottsRule`,
`..._CountsNullRowsAsZeroAndTextAsNaN`, `..._OfNoRowsGivesNaNRows`, `..._AsRenamesTheOutputFields`,
`..._IsDeterministic`; `KernelDensityTests.KernelDensity_Pdf_OfTwoPointsIsTheMeanOfTwoGaussians`,
`..._Pdf_ScalesWithTheBandwidth`, `..._Cdf_OfASymmetricPairIsOneHalfAtTheMiddle`,
`..._NormalCdf_MatchesKnownValuesAndTheReference`, `..._PdfAndCdf_WithScottBandwidthMatchTheReference`,
`..._EstimateBandwidth_FollowsScottsFormula`, `..._EstimateBandwidth_FallsBackWhenTheSpreadIsZero`,
`..._Pdf_CountsNullAndEmptyTextAsZero`, `..._Pdf_OfNonNumericOrEmptySupportIsNaN`, `..._Constructor_RejectsNull`

Asserts the densities of a small support at hand-computed points, that a shared resolve gives 201 uniform points over
the extent of the whole column (and `steps` overrides), that groups share the grid and keep their own support, that an
independent resolve samples every group adaptively over its own extent between `minsteps` and `maxsteps`, that `counts`
multiplies by the number of rows, that `cumulative` ends near 1 and is monotonic, Scott's bandwidth `1.06 min(sd, iqr /
1.34) n^(-1/5)` for a known sample and its fallbacks for a zero spread, and the reading of null (zero) and text (NaN)
values.

##### Quantile

**Tests**: `StatTransformsQuantileTests.StatTransforms_Quantile_DefaultStepGivesOneHundredProbabilities`,
`..._StepGivesTheRangeOfHalfStepsUpToOne`, `..._ProbsAreUsedAsGiven`, `..._GroupbyComputesEveryGroupSeparately`,
`..._IgnoresValuesThatAreNotNumbers`, `..._OfNothingHasNoValue`, `..._AsRenamesTheOutputFields`;
`StatisticsPrimitivesTests.StatisticsPrimitives_Quantiles_AreLinearInterpolationOfTheSortedNumbers`,
`..._Numbers_DropsNullEmptyAndNaNAndConvertsTheRest`,
`..._Deviation_IsTheSampleDeviationOfTheNonNullNumbers`, `..._Extent_FollowsJavaScriptComparison`,
`..._RangeCount_MatchesTheLengthOfD3Range`, `..._RangeCount_QuotientBeyondLong_SaturatesToLongMaxValue`,
`..._IsTruthy_ZeroAndNaNAreFalsy`,
`..._JsMinAndJsMax_PropagateNaN`, `..._NullArguments_Throw`

Asserts that the default step gives the probabilities 0.005 to 0.995 (100 rows), that `step: 0.25` gives 0.125, 0.375,
0.625, and 0.875, that `probs` are used as given, that the quantile of `[1, 2, 3, 4, 5]` at 0.25 is 2 and at 0.5 is 3,
that null, the empty text, and NaN are not numbers, that no numbers give a null value, and the group fields and `as`
names of the output rows; and, for the primitives, the interpolation, the JavaScript conversions, the sample deviation,
the extent, and the length of a range.

##### Extent

**Tests**: `StatTransformsQuantileTests.StatTransforms_Extent_StoresTheMinimumAndMaximumInTheParameter`,
`..._WithoutAFiniteExtentSetsNullsAndWarns`, `..._IsReadByTheFollowingTransforms`

Asserts that the table is returned unchanged (the same instance), that the parameter holds the array `[min, max]` of
the
numbers of the field, that null and the empty text are skipped, that a field without a finite value gives `[null,
null]`
and the warning, and that a transform that follows reads the parameter (a `calculate` with the `datum` and the
parameter).

##### Groups

**Tests**: `StatTransformsRegressionTests.StatTransforms_Regression_GroupbyFitsEveryGroupInOrderOfFirstAppearance`,
`..._GroupedParamsCarryTheKeys`, `..._GroupKeysAreTheTextOfTheValues`,
`..._GroupsWithTooFewRowsAreSkippedWithAWarning`, `..._RowsCountEvenWhenTheyAreNotValidPoints`,
`..._EmptyInputGivesAnEmptyTable`, `..._AGroupWithoutXValuesGivesPointsWithoutPosition`,
`StatTransformsDensityTests.StatTransforms_Density_SharedGroupsUseOneGridAndTheirOwnSupport`,
`StatTransformsQuantileTests.StatTransforms_Quantile_GroupbyComputesEveryGroupSeparately`

Asserts the groups of Vega's `partition`: first-seen order, the group fields before the output fields with the values
of
the first row, the key by the JavaScript text of the values (the number 1 and the text `"1"` are one group; null and a
missing field are one group), a group of too few rows skipped with a warning, the rows that are not valid points
counting toward the group size, and an empty table giving an empty result with the group fields.

##### Budgets And Cancellation

**Tests**: `StatTransformsRegressionTests.StatTransforms_Regression_CurveBeyondTheRowBudgetFailsAtTheTransform`,
`..._OutputBeyondTheFieldLimitFailsAtTheTransform`, `..._Loess_WorkBeyondTheStepBudgetFailsAtTheTransform`,
`..._RegressionAndLoess_StopWhenCanceled`, `..._IsDeterministic`;
`StatTransformsDensityTests.StatTransforms_Density_StepsBeyondTheRowBudgetFailAtTheTransform`,
`..._WorkBeyondTheStepBudgetFailsAtTheTransform`, `..._StopsWhenCanceled`;
`StatTransformsQuantileTests.StatTransforms_Quantile_TinyStepBeyondTheRowBudgetFailsAtTheTransform`,
`..._StepWithACountBeyondLong_FailsLocatedAtTheRowBudget`,
`..._StepBeyondTheCellLimitWithAnUnboundedRowBudget_FailsLocatedAtTheCellLimit`,
`..._StopsWhenCanceled`, `StatTransforms_Apply_ReleasesTheInputRowsWhenTheOutputIsComplete`;
`LoessFitTests.LoessFit_Cost_CountsEachWeightEvaluationAsThreeElements`,
`LoessFitTests.LoessFit_Cost_SaturatesForTheLargestGroups`,
`StatTransformsDensityTests.StatTransforms_Density_KernelCostDoesNotWrapForTheLargestGroups`

Asserts that a curve, a density grid, or a quantile list beyond `MaxRows` is a `VegaLiteRenderException` located at the
transform before the rows exist (also a quantile step whose count of probabilities no integer holds, on every target
framework, and one beyond the cell limit when the row limit is raised to its maximum), that work beyond
`MaxEvaluationSteps` is a located failure, that an output beyond the
field or cell limit fails, that a canceled token throws `OperationCanceledException`, that the input rows are released
when the output is complete, and that two runs give identical tables. Asserts the calibration of the work estimate
(phase 14): the cost of a loess of 1,000 points with a bandwidth of 1 is 1,000 times 1,002 times 3 passes times 3
elements plus 16,000 for the sort, a bandwidth that no integer window can hold is a window of every point, and a loess
of 20,000 points with a bandwidth of 1 costs more than the whole default budget of 3.2 billion elements (it ran eleven
seconds before the weights were set). The estimates cannot wrap into free work: the loess estimate for the largest
group saturates at the largest long, the density charge of a group of `int.MaxValue` rows is four times that in a long
(not a wrapped int), and the meter adds a pending count of 63 elements and the largest long as a saturated count that
exhausts the budget (regression of a defect that review found).

##### System Integration

**Tests**: `CanvasNetVegaLite_SystemIntegration_StatTransforms_*` in `VegaLiteStatTransformsIntegrationTests.cs`

Asserts, through `VegaLiteDocument`, that a regression line is drawn at the scaled end points, that an `extent` option
extends the line, that a grouped regression draws one line each, that the loess of a line is the line, that a density
peaks at the center of its support, that the quantiles of two identical samples lie on the diagonal (the
quantile-quantile plot), that an extent parameter feeds the following transforms and sets a scale domain, that a static
site sees no extent and the warning `vl-param-extent-static` is delivered, that a facet cell gets its own regression,
and that the render is deterministic in strict mode.
