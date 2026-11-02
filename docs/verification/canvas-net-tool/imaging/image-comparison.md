### ImageComparison

<!-- cspell:ignore canvasnet psnr rmse ssim -->

#### Verification Approach

`ImageComparison` is verified with unit tests in `ImageComparisonTests.cs`. The thresholds are checked on a result whose
measures are constructed by hand (a MAE of 5, an RMSE of 7, a PSNR of 30, an SSIM of 0.8, a differing ratio of 0.25, a
maximum difference of 40, and a worst cell RMSE of 9), so the value of every metric and the boundary of every limit is
known exactly, and on two comparisons made by the core `Surface.Compare` (identical images, and a quadrant that differs
by the maximum). The measures, the grid, and the pictures themselves are verified in the core library (see
_SurfaceComparison Unit Verification Design_, `canvas/surface-comparison.md`). The unit needs no files and no mocking.

#### Test Environment

N/A - standard test environment; the tests use only in-memory surfaces.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Thresholds pass or fail in the documented direction, at the boundary, using unrounded values; an error measure passes
  at most the limit, `psnr` and `ssim` at least the limit, and the PSNR of identical images passes every minimum.
- The metric names are unique, parse in any case, and round-trip; the ranges and directions are as documented.
- The defaults of the options are accepted by the core library.

#### Test Scenarios

**ImageComparison_Evaluate_ErrorMetrics_PassWhenAtMostLimit**, **ImageComparison_Evaluate_SimilarityMetrics_PassWhenAtLeastLimit**,
**ImageComparison_Evaluate_IdenticalImagesPsnr_PassesAnyMinimum**, **ImageComparison_Evaluate_CellRmse_UsesTheWorstCell**,
**ImageComparison_Evaluate_NullArguments_ThrowArgumentNullException**,
**ImageComparison_ThresholdMetrics_NamesRangesAndDirections_AreConsistent**, and
**ImageComparison_TryParseMetric_KnownName_RoundTrips**: for each metric the value passes at its own limit and on the
lenient side, and fails just on the strict side; identical images have an infinite PSNR that passes a limit of 1000;
the worst-cell threshold sees a cell of 255 although the whole image's RMSE is below 128; names are unique, parse in any
case, and round-trip; the ranges and directions are as documented and unknown names and undefined values are rejected;
null arguments throw.

**ImageComparison_Defaults_AreAcceptedByTheCoreLibrary**: a tolerance of 8 and a 4 by 4 grid are accepted by `Compare`
(giving 16 cells), a gain of 4 by `CreateDiff`, and a weight of 0.5 by `CreateOverlay`.
