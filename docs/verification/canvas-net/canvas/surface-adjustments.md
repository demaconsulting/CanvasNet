### SurfaceAdjustments Unit Verification Design

<!-- cspell:ignore luma lumas otsu grayscale gamma percentile percentiles midtone midtones -->

The tone and color adjustments of the `Surface` class (the `SurfaceAdjustments` unit of the `Canvas` subsystem) are
verified by `test/DemaConsulting.CanvasNet.Tests/Canvas/SurfaceAdjustmentsTests.cs`.

#### Verification Approach

`SurfaceAdjustments` is verified with unit tests. The pixel operations are checked on tiny surfaces whose pixels are
known exactly (a row of 256 gray levels, a few translucent pixels) so that every expected value is a hand-computed
number; the Otsu threshold and the auto-levels range are checked on small hand-built histograms (`{20: 100, 200: 100}`
gives 109); the three tables are checked on their endpoints, pivots, and a few computed entries; and the parallel code
is checked against a sequential per-pixel reference on a pseudo-random image taller than three histogram blocks, with
and without translucent pixels. The internal table builders and `AutoLevelsRange` are reached from the test project
through `InternalsVisibleTo`. The unit needs no files and no mocking. The tests moved here, with their expected values
unchanged, from the unit tests of the command-line tool's former `ImageAdjustments` class.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the tests use only in-memory surfaces
- **Isolation**: Each test method builds its own surfaces; no shared state between tests

#### Acceptance Criteria

- All unit tests pass with zero failures on every target framework.
- The luma follows the fixed-point formula and is flattened over white for a translucent pixel.
- Grayscale, invert, and threshold give the documented pixels and keep or replace alpha as documented.
- Otsu's method puts the threshold in the middle of a gap between modes, returns 127 for a single level, and separates
  classes of different sizes.
- The levels, brightness, and contrast tables follow their formulas, and a table is applied to red, green, and blue only.
- The auto-levels points are the clipped percentiles of the histogram, a flat image has none, and a low-contrast image
  is stretched to the full range.
- The same input gives identical pixels, and the parallel histogram and table application equal a sequential reference.
- Invalid arguments and disposed surfaces are rejected.

#### Test Scenarios

##### CanvasNet-Canvas-SurfaceAdjustments-Luma: Fixed-Point Luma Over White

**Test**: `Surface_Luma_OpaqueAndTranslucentPixels_UsesFixedPointWeightsOverWhite`

Black, white, pure red, green, and blue have the lumas 0, 255, 77, 149, and 29; black at alpha 128 has 127, red at
alpha 128 has 166, and a fully transparent pixel has 255 whatever its color.

##### CanvasNet-Canvas-SurfaceAdjustments-Grayscale: Grayscale

**Tests**: `Surface_Grayscale_ColorPixels_SetsEqualChannelsAndKeepsAlpha`, `Surface_Grayscale_AlreadyGray_IsUnchanged`

Red, green, and blue pixels with alpha 100, 255, and 0 become gray 77, 149, and 29 with the same alpha; the 256 gray
levels are unchanged.

##### CanvasNet-Canvas-SurfaceAdjustments-Invert: Invert

**Tests**: `Surface_Invert_Pixels_ComplementsColorAndKeepsAlpha`, `Surface_Invert_Twice_RestoresOriginal`

(0, 100, 255, 77) becomes (255, 155, 0, 77); inverting a pseudo-random image twice restores it exactly.

##### CanvasNet-Canvas-SurfaceAdjustments-Threshold: Fixed Threshold

**Tests**: `Surface_Threshold_FixedLevel_WhiteOnlyAboveLevel`,
`Surface_Threshold_TranslucentPixel_UsesLumaOverWhiteAndIsOpaque`

With the level 100 the ramp is white for levels above 100 and black otherwise, level 255 gives all black and level 0
makes only level 0 black; black at alpha 128 (luma 127) is white at level 100 and black at level 127, and a transparent
pixel is white; results are opaque.

##### CanvasNet-Canvas-SurfaceAdjustments-Otsu: Otsu Threshold

**Tests**: `Surface_OtsuThreshold_TwoLevels_ReturnsMiddleOfGap`, `Surface_OtsuThreshold_SingleLevel_ReturnsFallback127`,
`Surface_OtsuThreshold_UnevenClasses_SeparatesModes`

`{20: 100, 200: 100}` gives 109; a single level, an empty histogram, and a single pixel level 255 give 127;
`{30: 900, 40: 100, 220: 50}` gives 129.

##### CanvasNet-Canvas-SurfaceAdjustments-Levels: Levels

**Tests**: `Surface_BuildLevelsTable_BlackWhiteGamma_MapsEndpointsAndMidtone`,
`Surface_BuildLevelsTable_InvalidRange_Throws`, `Surface_LevelsBrightnessContrast_Pixels_ApplyTheirTablesAndKeepAlpha`

For black 50 and white 200 the entries 0, 50, 125, 200, and 255 are 0, 0, 128, 255, and 255; gamma 2 maps 125 to 180
and gamma 0.5 maps it to 64; a black level of 255, a white level of 0 or 256, a gamma of 0.05 or 10.5 or NaN, a negative
black level, and a black level that is not below white throw. The public `Levels` method applies the table to red,
green, and blue and keeps alpha.

##### CanvasNet-Canvas-SurfaceAdjustments-BrightnessContrast: Brightness and Contrast

**Tests**: `Surface_BuildBrightnessTable_ShiftsAndClamps`, `Surface_BuildContrastTable_PivotsAtMidGray`,
`Surface_ApplyTable_Pixels_MapsRgbAndKeepsAlpha`, `Surface_LevelsBrightnessContrast_Pixels_ApplyTheirTablesAndKeepAlpha`

+50 and -50 shift and clamp at both ends. Contrast 0 is the identity; 50 and -50 keep 128, spread or squeeze the levels
around it (138 maps to 158 at 50); at 100 the levels 127 and 129 saturate to 0 and 255. A brightness table of +10
changes the three color channels, clamps at 255, and leaves alpha 77 and 0 unchanged; the public `Brightness` and
`Contrast` methods give the same pixels.

##### CanvasNet-Canvas-SurfaceAdjustments-AutoLevels: Auto-Levels

**Tests**: `Surface_AutoLevelsRange_ClipPercent_PicksPercentilePoints`, `Surface_AutoLevelsRange_FlatImage_ReturnsNull`,
`Surface_AutoLevels_LowContrastImage_StretchesToFullRange`

For 5 black, 495 at 50, 495 at 150, and 5 white pixels, a clip of 1 percent gives (50, 150) while 0.5 percent and 0 give
(0, 255); a single level and an empty histogram have no range and a flat surface is unchanged; a 16 by 16 gradient from
100 to 145 reports (100, 145) and its ends become 0 and 255.

##### CanvasNet-Canvas-SurfaceAdjustments-Deterministic: Deterministic Parallel Results

**Tests**: `Surface_Adjustments_SameInputTwice_ProduceIdenticalPixels`,
`Surface_LumaHistogram_LargeImage_MatchesSequentialReference`, `Surface_ApplyTable_LargeImage_MatchesPerPixelReference`

A chain of auto-levels, contrast, grayscale, and an automatic threshold gives identical pixels twice; the histogram of
a 300 by 200 image (three full blocks and a partial one) equals the sequential count and sums to the pixel count; a
levels adjustment applied in parallel equals the per-pixel mapping.

##### CanvasNet-Canvas-SurfaceAdjustments-Arguments: Invalid Arguments

**Tests**: `Surface_Adjustments_OutOfRangeArguments_Throw`, `Surface_Adjustments_AfterDispose_ThrowObjectDisposedException`

A threshold of -1 and 256, a brightness of 256 and -256, a contrast of 101, -100.5, and NaN, a clip of -0.1, 20.5, and
NaN, a gamma of 10.5, a black level not below white, a table of 255 entries, and histograms of 255 and 257 entries or
with a negative count all throw; every adjustment of a disposed surface throws `ObjectDisposedException`.
