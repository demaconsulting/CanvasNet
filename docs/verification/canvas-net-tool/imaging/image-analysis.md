### ImageAnalysis

<!-- cspell:ignore canvasnet luma otsu percentile percentiles -->

#### Verification Approach

`ImageAnalysis` is verified with unit tests in `ImageAnalysisTests.cs`. The analysis is checked on tiny surfaces whose
pixels are known exactly (a row of gray levels, a few colors with known counts) so that every expected color, count,
fraction, and statistic is a hand-computed number; the clustering rules (order, leader distance, tie-breaks) have a case
each; and the sample is checked on a surface whose channels are functions of the position. The unit needs no files and no
mocking.

#### Test Environment

N/A - standard test environment; the tests use only in-memory surfaces.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Shades in one 4-bit bucket are one color whose value is the mean of the pixels.
- Colors are ordered by pixel count with the documented tie-breaks, limited to the requested number, and clustered by the
  distance to the leader; fractions are rounded to 6 decimals.
- Translucent pixels are flattened over the background.
- The luma statistics (mean, range, percentiles, Otsu, 16-bin histogram) follow their definitions.
- A sample reports the pixel, its luma, and a clipped window with its mean; a point outside the image is a usage error.
- The same input gives an identical result.
- Invalid arguments are rejected.

#### Test Scenarios

**ImageAnalysis_Analyze_FourBitBuckets_GroupNearbyShadesIntoOneColor**: three pixels `(16, 32, 48)` and one `(31, 47, 63)`
fall in one bucket and give one color `(20, 36, 52)` with 4 pixels and the fraction 1.

**ImageAnalysis_Analyze_TwoColorImage_ReportsBothInPixelOrderWithFractions**: 75 white and 25 black pixels give white with
0.75 and then black with 0.25; one black and two white pixels give the fraction 0.666667.

**ImageAnalysis_Analyze_EqualCounts_OrderedByBucketIndex**: red, green, and blue with two pixels each are reported blue,
green, red (the buckets 0x00F, 0x0F0, 0xF00).

**ImageAnalysis_Analyze_CountLimit_ReportsOnlyTheMostCommon**: five grays with 5, 4, 3, 2, and 1 pixels give the two most
common for a count of 2 and all five for a count of 32.

**ImageAnalysis_Analyze_Gradient_MergesWithinClusterRadiusAndAveragesMembers**: the grays 128 (4 pixels), 144 (2), 160 (1)
make one color 137 with 7 pixels and 0.875, and 208 is a second color with 0.125.

**ImageAnalysis_Analyze_TransparentPixels_AreFlattenedOverBackground**: three transparent red pixels and one black pixel
give 3 white pixels over a white background and 3 blue pixels over a blue one; black at alpha 128 over white is the gray
127.

**ImageAnalysis_Analyze_Luma_ReportsMeanMinMaxPercentilesOtsuAndHistogram**: an image with each gray level once has the
mean 127.5, the range 0 to 255, the percentiles 2, 127, and 253, the Otsu level 127, and 16 pixels in each bin; two equal
pixels of level 200 have the mean, minimum, maximum, and percentile 200, the Otsu fallback 127, and 2 pixels in bin 12.

**ImageAnalysis_Sample_Point_ReturnsColorAndLuma**: a red pixel has the luma 77 and no window; a black pixel at alpha
128 has the luma 127.

**ImageAnalysis_Sample_Radius_ReturnsClippedWindowAndMean**: at the corner with radius 2 the window is the 3 by 3 rectangle
at 0,0 with the mean `(10, 10, 0, 254)`; at 3,3 with radius 1 it is the rectangle at 2,2 with the mean `(30, 30, 0, 252)`.

**ImageAnalysis_Sample_OutsideImage_Throws**: the points 6,0, 0,4, -1,0, 0,-1, and 5000,10 outside a 6 by 4 image throw
a `usage-error` whose message names the image size.

**ImageAnalysis_Analyze_SameInputTwice_ReturnsIdenticalResult**: a pseudo-random image with translucent pixels gives the
same colors, statistics, and histogram twice, and the histogram sums to the pixel count.

**ImageAnalysis_NullOrOutOfRangeArguments_Throw**: a null surface, a color count of 0 and 33, a background with alpha 254,
and a radius of -1 and 9 throw.
