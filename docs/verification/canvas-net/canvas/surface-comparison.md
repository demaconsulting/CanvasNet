### SurfaceComparison Unit Verification Design

<!-- cspell:ignore psnr rmse ssim luma -->

The comparison of the `Surface` class with another surface (the `SurfaceComparison` unit of the `Canvas` subsystem) is
verified by `test/DemaConsulting.CanvasNet.Tests/Canvas/SurfaceComparisonTests.cs`.

#### Verification Approach

The tests build tiny surfaces in memory whose differences are known exactly (a row of two pixels that differ by 10, four
pixels that differ by 0, 5, 8, and 9, a quadrant that is white against black), so the expected MAE, RMSE, PSNR, ratios,
and cell errors are written down as arithmetic in the test and not taken from the unit. The structural similarity is
checked against a closed form (flat images, where only the luminance term remains) and against an independent
floating-point implementation of the textbook formula in the test project, over a single window and over the four
edge-flush windows of a 10 by 10 image (the internal `Ssim` is reached by `InternalsVisibleTo`). The pictures are checked
pixel by pixel against values computed by hand from the documented formulas. The unit needs no files and no mocking.
The thresholds that turn a comparison into pass or fail are not part of this unit; they are command-line behavior of
the tool's `ImageComparison` unit.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the tests use only in-memory surfaces
- **Isolation**: Each test method builds its own surfaces; no shared state between tests

#### Acceptance Criteria

- All unit tests pass with zero failures on every target framework.
- MAE, RMSE, PSNR, the maximum channel difference, and the identical flag follow the documented formulas, ignoring alpha;
  identical images have no PSNR and an SSIM of exactly 1.
- A pixel counts as differing only when its largest channel difference is above the tolerance.
- The SSIM windows cover every pixel, including the last column and row, and match the independent reference.
- The grid cells partition the image exactly and locate a difference in the right cell; the worst cells are ordered by
  error and then position.
- The difference, overlay, and side-by-side pictures have exactly the documented pixels, and a side-by-side image that is
  too wide is scaled to fit.
- Invalid arguments and disposed surfaces are rejected; the same inputs give identical results and pictures.

#### Test Scenarios

##### CanvasNet-Canvas-SurfaceComparison-ErrorMetrics: Error Metrics

**Tests**: `Surface_Compare_IdenticalImages_ReportsZeroErrorIdenticalAndInfinitePsnr`,
`Surface_Compare_KnownDifference_ComputesMaeRmseAndPsnr`,
`Surface_Compare_SingleChannelDifference_AveragesOverThreeChannels`,
`Surface_Compare_MaxChannelDifference_IsLargestChannelDelta`, `Surface_Compare_AlphaChannel_IsIgnored`

Two copies of a gradient have zero MAE and RMSE, no PSNR, an SSIM of 1, no differing pixels, 16 cells with zero error,
and no worst cells. Two pixels differing by 0 and 10 in every channel give an MAE of 5, an RMSE of the square root of 50,
and the matching PSNR; a difference of 30 in one channel of one of four pixels is averaged over twelve channel values;
the largest of 3, 40, and 7 is 40; the same color with another alpha is identical.

##### CanvasNet-Canvas-SurfaceComparison-Ssim: Structural Similarity

**Tests**: `Surface_Ssim_IdenticalImages_IsOne`, `Surface_Ssim_ConstantBrightnessShift_MatchesClosedForm`,
`Surface_Ssim_InvertedGradient_IsLow`, `Surface_Ssim_ImageSmallerThanWindow_UsesOneWindow`,
`Surface_Ssim_DifferenceOnlyInLastColumnAndRow_IsDetectedByEdgeWindows`

Identical textured images of an odd size give exactly 1; flat grays 100 and 110 give
`(2 * 100 * 110 + C1) / (100^2 + 110^2 + C1)`; a ramp and its inverse are below zero; a 3 by 3 image is one window of its
own size and equals the textbook formula; a single changed pixel in the bottom-right corner of a 10 by 10 image (which a
stride-only layout would miss) lowers the result to the mean of the four windows at origins 0 and 2.

##### CanvasNet-Canvas-SurfaceComparison-DiffPixels: Tolerance

**Tests**: `Surface_Compare_Tolerance_CountsOnlyPixelsAboveTolerance`,
`Surface_Compare_KnownDifference_ComputesMaeRmseAndPsnr`

Differences of 0, 5, 8, and 9 give one differing pixel at a tolerance of 8 (a difference equal to the tolerance is not
counted), three at tolerances 0 and 4, and none at 255, while the error measures do not change.

##### CanvasNet-Canvas-SurfaceComparison-Grid: Grid

**Tests**: `Surface_Compare_Grid_Boundaries_PartitionTheImageExactly`,
`Surface_Compare_Grid_DifferenceInOneQuadrant_IsLocatedInThatCell`,
`Surface_Compare_Grid_WorstCells_AreOrderedByRmseThenPosition`

A 10 by 7 image in 3 columns and 2 rows has columns at x 0, 3, and 6 (widths 3, 3, 4), rows at y 0 and 3 (heights 3 and
4), and cells whose areas add up to 70. A white bottom-right quadrant gives errors of 255 and a ratio of 1 in that cell
only, and an overall MAE of 63.75; with differences of 10, 30, and 30 in three cells the worst cells are the 30 in row 0,
the 30 in row 1, then the 10, the unchanged cells are left out, and a negative count throws.

##### CanvasNet-Canvas-SurfaceComparison-DiffImage: Difference Image

**Tests**: `Surface_CreateDiff_Heat_MapsDeltaThroughHotRamp`, `Surface_CreateDiff_HeatSmallDelta_IsDarkRed`,
`Surface_CreateDiff_Abs_AmplifiesEachChannelAndClamps`

With a gain of 1, deltas of 0, 85, 170, and 255 are black, red, yellow, and white and the largest channel decides; at a
gain of 4 deltas of 64 and more are white; a delta of 21 at gain 4 is `(252, 0, 0)` and a delta of 1 at gain 0.5 rounds
up to `(3, 0, 0)`; the absolute style gives `(40, 80, 120)` for differences of 10, 20, and 30 and clamps 400 to 255;
gains outside 0.1 to 255 and an undefined style throw.

##### CanvasNet-Canvas-SurfaceComparison-Overlay: Overlay

**Test**: `Surface_CreateOverlay_Weight_BlendsWithIntegerRule`

Weight 0 and 1 give the first and the second pixel exactly, and 0.5 gives `(128, 150, 127)` by the integer rule; weights
outside 0 to 1 and not-a-number throw.

##### CanvasNet-Canvas-SurfaceComparison-SideBySide: Side by Side

**Tests**: `Surface_CreateSideBySide_PlacesPanelsInOrderSeparatedByGaps`,
`Surface_CreateSideBySide_TooWide_ScalesPanelsToFitMaximum`

Three flat 4 by 3 panels give a 28 by 3 sheet with the panels at x 0, 12, and 24 and gray gaps between them; three 3000
by 2 panels are scaled to 2725 by 1 and the sheet is 8191 wide with the panels still in place.

##### CanvasNet-Canvas-SurfaceComparison-Arguments: Invalid Arguments

**Tests**: `Surface_Compare_SizeMismatch_ThrowsArgumentException`,
`Surface_Compare_InvalidToleranceOrGrid_ThrowsArgumentOutOfRange`,
`Surface_ComparisonMethods_NullArguments_ThrowArgumentNullException`,
`Surface_ComparisonMethods_AfterDispose_ThrowObjectDisposedException`, `Surface_CreateImages_SizeMismatch_ThrowsArgumentException`

A size mismatch names both sizes and the argument `other`; a tolerance of -1 or 256, a grid side of 0 or 65, and a grid
larger than a 4 by 4 image throw; every null argument throws; a disposed surface, as the first or as the other operand
of any comparison method, throws `ObjectDisposedException`; the picture creators and the SSIM reject differently sized
inputs.

##### CanvasNet-Canvas-SurfaceComparison-Deterministic: Determinism

**Tests**: `Surface_Compare_SameInputTwice_ReturnsEqualResults`, `Surface_CreateImages_SameInputTwice_ProduceIdenticalPixels`

Two comparisons of the same images have equal metrics and cells bit for bit, and the diff, overlay, and side-by-side
pictures of the same inputs are pixel-identical.
