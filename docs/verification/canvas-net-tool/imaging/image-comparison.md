### ImageComparison

<!-- cspell:ignore canvasnet psnr rmse ssim luma -->

#### Verification Approach

`ImageComparison` is verified with unit tests in `ImageComparisonTests.cs`. The tests build tiny surfaces in memory
whose differences are known exactly (a row of two pixels that differ by 10, four pixels that differ by 0, 5, 8, and 9,
a quadrant that is white against black), so the expected MAE, RMSE, PSNR, ratios, and cell errors are written down as
arithmetic in the test and not taken from the unit. The structural similarity is checked against a closed form (flat
images, where only the luminance term remains) and against an independent floating-point implementation of the textbook
formula in the test project, over a single window and over the four edge-flush windows of a 10 by 10 image. The
pictures are checked pixel by pixel against values computed by hand from the documented formulas. The unit needs no
files and no mocking.

#### Test Environment

N/A - standard test environment; the tests use only in-memory surfaces.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- MAE, RMSE, PSNR, the maximum channel difference, and the identical flag follow the documented formulas, ignoring alpha;
  identical images have no PSNR and an SSIM of exactly 1.
- A pixel counts as differing only when its largest channel difference is above the tolerance.
- The SSIM windows cover every pixel, including the last column and row, and match the independent reference.
- The grid cells partition the image exactly and locate a difference in the right cell; the worst cells are ordered by
  error and then position.
- Thresholds pass or fail in the documented direction, at the boundary, using unrounded values.
- The difference, overlay, and side-by-side pictures have exactly the documented pixels, and a side-by-side image that is
  too wide is scaled to fit.
- Invalid arguments are rejected; the same inputs give identical results and pictures.

#### Test Scenarios

**ImageComparison_Compare_IdenticalImages_ReportsZeroErrorIdenticalAndInfinitePsnr**: two copies of a gradient have zero
MAE and RMSE, no PSNR, an SSIM of 1, no differing pixels, 16 cells with zero error, and no worst cells.

**ImageComparison_Compare_KnownDifference_ComputesMaeRmseAndPsnr**,
**ImageComparison_Compare_SingleChannelDifference_AveragesOverThreeChannels**,
**ImageComparison_Compare_MaxChannelDifference_IsLargestChannelDelta**, and
**ImageComparison_Compare_AlphaChannel_IsIgnored**: two pixels differing by 0 and 10 in every channel give an MAE of 5,
an RMSE of the square root of 50, and the matching PSNR; a difference of 30 in one channel of one of four pixels is
averaged over twelve channel values; the largest of 3, 40, and 7 is 40; the same color with another alpha is identical.

**ImageComparison_Compare_Tolerance_CountsOnlyPixelsAboveTolerance**: differences of 0, 5, 8, and 9 give one differing
pixel at the default tolerance of 8 (a difference equal to the tolerance is not counted), three at tolerances 0 and 4,
and none at 255, while the error measures do not change.

**ImageComparison_Ssim_IdenticalImages_IsOne**, **ImageComparison_Ssim_ConstantBrightnessShift_MatchesClosedForm**,
**ImageComparison_Ssim_InvertedGradient_IsLow**, **ImageComparison_Ssim_ImageSmallerThanWindow_UsesOneWindow**, and
**ImageComparison_Ssim_DifferenceOnlyInLastColumnAndRow_IsDetectedByEdgeWindows**: identical textured images of an odd
size give exactly 1; flat grays 100 and 110 give `(2 * 100 * 110 + C1) / (100^2 + 110^2 + C1)`; a ramp and its inverse
are below zero; a 3 by 3 image is one window of its own size and equals the textbook formula; a single changed pixel in
the bottom-right corner of a 10 by 10 image (which a stride-only layout would miss) lowers the result to the mean of the
four windows at origins 0 and 2.

**ImageComparison_Grid_Boundaries_PartitionTheImageExactly**: a 10 by 7 image in 3 columns and 2 rows has columns at x
0, 3, and 6 (widths 3, 3, 4), rows at y 0 and 3 (heights 3 and 4), and cells whose areas add up to 70.

**ImageComparison_Grid_DifferenceInOneQuadrant_IsLocatedInThatCell** and
**ImageComparison_Grid_WorstCells_AreOrderedByRmseThenPosition**: a white bottom-right quadrant gives errors of 255 and
a ratio of 1 in that cell only, and an overall MAE of 63.75; with differences of 10, 30, and 30 in three cells the worst
cells are the 30 in row 0, the 30 in row 1, then the 10, the unchanged cells are left out, and a negative count throws.

**ImageComparison_Evaluate_ErrorMetrics_PassWhenAtMostLimit**, **ImageComparison_Evaluate_SimilarityMetrics_PassWhenAtLeastLimit**,
**ImageComparison_Evaluate_IdenticalImagesPsnr_PassesAnyMinimum**, **ImageComparison_Evaluate_CellRmse_UsesTheWorstCell**,
**ImageComparison_ThresholdMetrics_NamesRangesAndDirections_AreConsistent**, and
**ImageComparison_TryParseMetric_KnownName_RoundTrips**: for each metric the value passes at its own limit and on the
lenient side, and fails just on the strict side; identical images have an infinite PSNR that passes a limit of 1000;
the worst-cell threshold sees a cell of 255 although the whole image's RMSE is below 128; names are unique, parse in any
case, and round-trip; the ranges and directions are as documented and unknown names and undefined values are rejected.

**ImageComparison_CreateDiff_Heat_MapsDeltaThroughHotRamp**, **ImageComparison_CreateDiff_HeatSmallDelta_IsDarkRed**,
and **ImageComparison_CreateDiff_Abs_AmplifiesEachChannelAndClamps**: with a gain of 1, deltas of 0, 85, 170, and 255 are
black, red, yellow, and white and the largest channel decides; at the default gain deltas of 64 and more are white; a
delta of 21 at gain 4 is `(252, 0, 0)` and a delta of 1 at gain 0.5 rounds up to `(3, 0, 0)`; the absolute style gives
`(40, 80, 120)` for differences of 10, 20, and 30 and clamps 400 to 255; gains outside 0.1 to 255 and an undefined style
throw.

**ImageComparison_CreateOverlay_Weight_BlendsWithIntegerRule**: weight 0 and 1 give the first and the second pixel
exactly, and 0.5 gives `(128, 150, 127)` by the integer rule; weights outside 0 to 1 and not-a-number throw.

**ImageComparison_CreateSideBySide_PlacesPanelsInOrderSeparatedByGaps** and
**ImageComparison_CreateSideBySide_TooWide_ScalesPanelsToFitMaximum**: three flat 4 by 3 panels give a 28 by 3 sheet with
the panels at x 0, 12, and 24 and gray gaps between them; three 3000 by 2 panels are scaled to 2725 by 1 and the sheet is
8191 wide with the panels still in place.

**ImageComparison_Compare_SizeMismatch_ThrowsArgumentException**,
**ImageComparison_Compare_InvalidToleranceOrGrid_ThrowsArgumentOutOfRange**,
**ImageComparison_Methods_NullArguments_ThrowArgumentNullException**, and
**ImageComparison_CreateImages_SizeMismatch_ThrowsArgumentException**: a size mismatch names both sizes; a tolerance of
-1 or 256, a grid side of 0 or 65, and a grid larger than a 4 by 4 image throw; every null argument throws; the picture
creators and the SSIM reject differently sized inputs.

**ImageComparison_Compare_SameInputTwice_ReturnsEqualResults** and
**ImageComparison_CreateImages_SameInputTwice_ProduceIdenticalPixels**: two comparisons of the same images have equal
metrics and cells bit for bit, and the diff, overlay, and side-by-side pictures of the same inputs are pixel-identical.
