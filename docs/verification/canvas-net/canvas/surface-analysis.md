### SurfaceAnalysis Unit Verification Design

<!-- cspell:ignore luma otsu percentile percentiles -->

The analysis of the `Surface` class (the `SurfaceAnalysis` unit of the `Canvas` subsystem) is verified by
`test/DemaConsulting.CanvasNet.Tests/Canvas/SurfaceAnalysisTests.cs`.

#### Verification Approach

The analysis is checked on tiny surfaces whose pixels are known exactly (a row of gray levels, a few colors with known
counts) so that every expected color, count, fraction, and statistic is a hand-computed number; the clustering rules
(order, leader distance, tie-breaks) have a case each; and the sample is checked on a surface whose channels are
functions of the position. The unit needs no files and no mocking.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the tests use only in-memory surfaces
- **Isolation**: Each test method builds its own surfaces; no shared state between tests

#### Acceptance Criteria

- All unit tests pass with zero failures on every target framework.
- Shades in one 4-bit bucket are one color whose value is the mean of the pixels.
- Colors are ordered by pixel count with the documented tie-breaks, limited to the requested number, and clustered by the
  distance to the leader; fractions are rounded to 6 decimals.
- Translucent pixels are flattened over the background.
- The luma statistics (mean, range, percentiles, Otsu, 16-bin histogram) follow their definitions.
- A sample reports the pixel, its luma, and a clipped window with its mean; a point outside the image is an
  out-of-range argument that names the coordinate.
- The same input gives an identical result.
- Invalid arguments and a disposed surface are rejected.

#### Test Scenarios

##### CanvasNet-Canvas-SurfaceAnalysis-Quantize: Buckets

**Test**: `Surface_Analyze_FourBitBuckets_GroupNearbyShadesIntoOneColor`

Three pixels `(16, 32, 48)` and one `(31, 47, 63)` fall in one bucket and give one color `(20, 36, 52)` with 4 pixels and
the fraction 1.

##### CanvasNet-Canvas-SurfaceAnalysis-Dominant: Dominant Colors

**Tests**: `Surface_Analyze_TwoColorImage_ReportsBothInPixelOrderWithFractions`,
`Surface_Analyze_EqualCounts_OrderedByBucketIndex`, `Surface_Analyze_CountLimit_ReportsOnlyTheMostCommon`,
`Surface_Analyze_Gradient_MergesWithinClusterRadiusAndAveragesMembers`

75 white and 25 black pixels give white with 0.75 and then black with 0.25; one black and two white pixels give the
fraction 0.666667. Red, green, and blue with two pixels each are reported blue, green, red (the buckets 0x00F, 0x0F0,
0xF00). Five grays with 5, 4, 3, 2, and 1 pixels give the two most common for a count of 2 and all five for a count of 32.
The grays 128 (4 pixels), 144 (2), 160 (1) make one color 137 with 7 pixels and 0.875, and 208 is a second color with
0.125; the cluster radius is 2.

##### CanvasNet-Canvas-SurfaceAnalysis-Alpha: Translucent Pixels

**Test**: `Surface_Analyze_TransparentPixels_AreFlattenedOverBackground`

Three transparent red pixels and one black pixel give 3 white pixels over a white background and 3 blue pixels over a
blue one; black at alpha 128 over white is the gray 127.

##### CanvasNet-Canvas-SurfaceAnalysis-LumaStats: Luma Statistics

**Test**: `Surface_Analyze_Luma_ReportsMeanMinMaxPercentilesOtsuAndHistogram`

An image with each gray level once has the mean 127.5, the range 0 to 255, the percentiles 2, 127, and 253, the Otsu
level 127, and 16 pixels in each bin; two equal pixels of level 200 have the mean, minimum, maximum, and percentile 200,
the Otsu fallback 127, and 2 pixels in bin 12.

##### CanvasNet-Canvas-SurfaceAnalysis-Sample: Sample

**Tests**: `Surface_Sample_Point_ReturnsColorAndLuma`, `Surface_Sample_Radius_ReturnsClippedWindowAndMean`,
`Surface_Sample_OutsideImage_ThrowsArgumentOutOfRange`

A red pixel has the luma 77 and no window; a black pixel at alpha 128 has the luma 127. At the corner with radius 2 the
window is the 3 by 3 rectangle at 0,0 with the mean `(10, 10, 0, 254)`; at 3,3 with radius 1 it is the rectangle at 2,2
with the mean `(30, 30, 0, 252)`. The points 6,0, 0,4, -1,0, 0,-1, and 5000,10 outside a 6 by 4 image throw
`ArgumentOutOfRangeException` naming `x` or `y`.

##### CanvasNet-Canvas-SurfaceAnalysis-Deterministic: Determinism

**Test**: `Surface_Analyze_SameInputTwice_ReturnsIdenticalResult`

A pseudo-random image with translucent pixels gives the same colors, statistics, and histogram twice, and the histogram
sums to the pixel count.

##### CanvasNet-Canvas-SurfaceAnalysis-Arguments: Invalid Arguments

**Test**: `Surface_AnalysisInvalidArguments_Throw`

A color count of 0 and 33, a background with alpha 254, and a radius of -1 and 9 throw, naming `colorCount`,
`background`, and `radius`; the analysis and the sample of a disposed surface throw `ObjectDisposedException`.
