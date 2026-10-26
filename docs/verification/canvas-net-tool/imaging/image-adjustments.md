### ImageAdjustments

<!-- cspell:ignore canvasnet luma lumas otsu grayscale gamma percentile percentiles midtone midtones -->

#### Verification Approach

`ImageAdjustments` is verified with unit tests in `ImageAdjustmentsTests.cs`. The pixel operations are checked on tiny
surfaces whose pixels are known exactly (a row of 256 gray levels, a few translucent pixels) so that every expected value
is a hand-computed number; the Otsu threshold and the auto-levels range are checked on small hand-built histograms
(`{20: 100, 200: 100}` gives 109); the three tables are checked on their endpoints, pivots, and a few computed entries;
and the parallel code is checked against a sequential per-pixel reference on a pseudo-random image taller than three
histogram blocks, with and without translucent pixels. The unit needs no files and no mocking.

#### Test Environment

N/A - standard test environment; the tests use only in-memory surfaces.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The luma follows the fixed-point formula and is flattened over white for a translucent pixel.
- Grayscale, invert, and threshold give the documented pixels and keep or replace alpha as documented.
- Otsu's method puts the threshold in the middle of a gap between modes, returns 127 for a single level, and separates
  classes of different sizes.
- The levels, brightness, and contrast tables follow their formulas, and a table is applied to red, green, and blue only.
- The auto-levels points are the clipped percentiles of the histogram, a flat image has none, and a low-contrast image
  is stretched to the full range.
- The same input gives identical pixels, and the parallel histogram and table application equal a sequential reference.
- Invalid arguments are rejected.

#### Test Scenarios

**ImageAdjustments_Luma_OpaqueAndTranslucentPixels_UsesFixedPointWeightsOverWhite**: black, white, pure red, green, and
blue have the lumas 0, 255, 77, 149, and 29; black at alpha 128 has 127, red at alpha 128 has 166, and a fully
transparent pixel has 255 whatever its color.

**ImageAdjustments_Grayscale_ColorPixels_SetsEqualChannelsAndKeepsAlpha** and
**ImageAdjustments_Grayscale_AlreadyGray_IsUnchanged**: red, green, and blue pixels with alpha 100, 255, and 0 become
gray 77, 149, and 29 with the same alpha; the 256 gray levels are unchanged.

**ImageAdjustments_Invert_Pixels_ComplementsColorAndKeepsAlpha** and **ImageAdjustments_Invert_Twice_RestoresOriginal**:
(0, 100, 255, 77) becomes (255, 155, 0, 77); inverting a pseudo-random image twice restores it exactly.

**ImageAdjustments_Threshold_FixedLevel_WhiteOnlyAboveLevel** and
**ImageAdjustments_Threshold_TranslucentPixel_UsesLumaOverWhiteAndIsOpaque**: with the level 100 the ramp is white for
levels above 100 and black otherwise, level 255 gives all black and level 0 makes only level 0 black; black at alpha 128
(luma 127) is white at level 100 and black at level 127, and a transparent pixel is white; results are opaque.

**ImageAdjustments_OtsuThreshold_TwoLevels_ReturnsMiddleOfGap**,
**ImageAdjustments_OtsuThreshold_SingleLevel_ReturnsFallback127**, and
**ImageAdjustments_OtsuThreshold_UnevenClasses_SeparatesModes**: `{20: 100, 200: 100}` gives 109; a single level, an
empty histogram, and a single pixel level 255 give 127; `{30: 900, 40: 100, 220: 50}` gives 129.

**ImageAdjustments_BuildLevelsTable_BlackWhiteGamma_MapsEndpointsAndMidtone** and
**ImageAdjustments_BuildLevelsTable_InvalidRange_Throws**: for black 50 and white 200 the entries 0, 50, 125, 200, and
255 are 0, 0, 128, 255, and 255; gamma 2 maps 125 to 180 and gamma 0.5 maps it to 64; a black level of 255, a white level
of 0 or 256, a gamma of 0.05 or 10.5 or NaN, a negative black level, and a black level that is not below white throw.

**ImageAdjustments_BuildBrightnessTable_ShiftsAndClamps**: +50 and -50 shift and clamp at both ends.

**ImageAdjustments_BuildContrastTable_PivotsAtMidGray**: contrast 0 is the identity; 50 and -50 keep 128, spread or
squeeze the levels around it (138 maps to 158 at 50); at 100 the levels 127 and 129 saturate to 0 and 255.

**ImageAdjustments_ApplyTable_Pixels_MapsRgbAndKeepsAlpha**: a brightness table of +10 changes the three color channels
and clamps at 255 and leaves alpha 77 and 0 unchanged.

**ImageAdjustments_AutoLevelsRange_ClipPercent_PicksPercentilePoints**,
**ImageAdjustments_AutoLevelsRange_FlatImage_ReturnsNull**, and
**ImageAdjustments_AutoLevels_LowContrastImage_StretchesToFullRange**: for 5 black, 495 at 50, 495 at 150, and 5 white
pixels, a clip of 1 percent gives (50, 150) while 0.5 percent and 0 give (0, 255); a single level and an empty histogram
have no range and a flat surface is unchanged; a 16 by 16 gradient from 100 to 145 reports (100, 145) and its ends become
0 and 255.

**ImageAdjustments_Operations_SameInputTwice_ProduceIdenticalPixels**,
**ImageAdjustments_LumaHistogram_LargeImage_MatchesSequentialReference**, and
**ImageAdjustments_ApplyTable_LargeImage_MatchesPerPixelReference**: a chain of auto-levels, contrast, grayscale, and an
automatic threshold gives identical pixels twice; the histogram of a 300 by 200 image (three full blocks and a partial
one) equals the sequential count and sums to the pixel count; a table applied in parallel equals the per-pixel mapping.

**ImageAdjustments_NullOrOutOfRangeArguments_Throw**: null surfaces, a threshold of -1 and 256, a brightness of 256 and
-256, a contrast of 101, -100.5, and NaN, a clip of -0.1, 20.5, and NaN, a table of 255 entries, and histograms of 255
and 257 entries or with a negative count all throw.
