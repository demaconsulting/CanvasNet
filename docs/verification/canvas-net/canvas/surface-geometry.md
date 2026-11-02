### SurfaceGeometry Unit Verification Design

<!-- cspell:ignore bilinear premultiplied deskew downscales -->

The rotation and skew estimation of the `Surface` class (the `SurfaceGeometry` unit of the `Canvas` subsystem) are
verified by `test/DemaConsulting.CanvasNet.Tests/Canvas/SurfaceGeometryTests.cs` (rotation) and
`test/DemaConsulting.CanvasNet.Tests/Canvas/SurfaceSkewTests.cs` (skew estimation and the `SkewEstimate` record).

#### Verification Approach

The canvas size is checked as arithmetic on hand-computed examples (100 by 50 at 90, 45, and 30 degrees). The rotation
mapping is checked with a single marker pixel in a small image, rotated with nearest sampling so that its exact
destination is known; the interpolation is checked on solid images (the interior keeps the source color), on a linear
gradient (bilinear sampling reproduces a linear function exactly, so rotating by 3 degrees and back differs only by
rounding), on a hard edge (intermediate grays appear), and on fills of every kind (opaque, transparent,
semi-transparent), where the expected edge pixels are recomputed from their own alpha with the premultiplied formula. The
fast paths are checked by rendering the same random, partly translucent image at eight angles and three fills with and
without them (through the internal `RotateArbitraryCore`, reached by `InternalsVisibleTo`) and requiring identical
pixels. The skew estimation is checked on generated pictures of ruled lines at known tilts (drawn with plain arithmetic,
not with the rotation code under test, by the `ImageFixtures.SketchPhoto` helper of the test project), with a bright
and dark ground, on a large picture that is analyzed on a reduced plane, and on blank and noise pictures that must not
give a confident answer; the tie-break is checked directly on its internal helper, and the thresholds and the sign of
`SkewEstimate` on constructed values and on a picture that is levelled with its own correction. The unit needs no files
and no mocking.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the tests use only in-memory surfaces
- **Isolation**: Each test method builds its own surfaces; no shared state between tests

#### Acceptance Criteria

- All unit tests pass with zero failures on every target framework.
- The rotated canvas follows the documented formula and a right angle does not gain a pixel.
- Rotation is clockwise about the centers, the interior keeps the source pixels, and a round trip by a small angle stays
  within rounding of the original.
- The corners are the fill color, a transparent fill fades the edge in alpha, and a semi-transparent fill blends in
  premultiplied space.
- Nearest copies source pixels, bilinear blends, and the fast paths give the pixels of the generic sampler.
- A result larger than 8192 pixels on a side is refused with `ArgumentException` before any pixel is allocated.
- Invalid arguments and a disposed surface are rejected.
- The skew of ruled lines is estimated to within 0.15 degrees for tilts from -12 to 14.5 degrees, with the correct sign,
  on a large image, and under uneven lighting; a blank image is not estimated; random noise has a confidence below
  `SkewEstimate.MinConfidence`; the same pixels give the same estimate; rotating by `CorrectionDegrees` levels a picture.

#### Test Scenarios

##### CanvasNet-Canvas-SurfaceGeometry-RotatedSize: Canvas Size

**Tests**: `Surface_RotatedSize_RightAndObliqueAngles_ComputesCeilingOfBoundingBox`,
`Surface_RotatedSize_NearExactFit_DoesNotGainAPixel`

100 by 50 is 100 by 50 at 0, 50 by 100 at 90 (and at -90, 270, and 450), 107 by 107 at 45, and 112 by 94 at 30; 160 by
100 at 30 is 189 by 167; 1 by 1 at 45 is 2 by 2; an angle and the same angle plus or minus half a turn give the same
size; an 8192 by 8192 image at 90 degrees stays 8192 by 8192.

##### CanvasNet-Canvas-SurfaceGeometry-RotateMapping: Direction and Center

**Tests**: `Surface_RotateArbitrary_Clockwise_MovesRightOfCenterBelowCenter`,
`Surface_RotateArbitrary_SolidImage_FillsInteriorWithSourceColor`,
`Surface_RotateArbitrary_RoundTripSmallAngle_StaysCloseToOriginal`

In an 11 by 11 white image a marker at (8, 5), right of the center, is at (5, 8) after a nearest-sampled quarter turn
and not at (8, 5); a marker above the center at (5, 2) moves to (8, 5). The 7 by 7 block at the center of a solid image
rotated by 20 degrees has exactly the source color. A 100 by 60 gradient (level = x + y) rotated by 3 degrees (to 104 by
66) and back (to 108 by 72) matches the original, offset by 4 columns and 6 rows, to within 2 levels over its central
area.

##### CanvasNet-Canvas-SurfaceGeometry-RotateFill: Fill and Edge

**Tests**: `Surface_RotateArbitrary_Corners_AreFillColor`,
`Surface_RotateArbitrary_TransparentFill_EdgePixelsFadeInAlpha`,
`Surface_RotateArbitrary_SemiTransparentFill_BlendsInPremultipliedSpace`

The four corners of a 100 by 60 image rotated by 30 degrees (to 117 by 102) are exactly the red fill, with nearest and
with bilinear sampling. A white 40 by 40 image rotated by 45 degrees with a transparent fill has a transparent black
corner, a white center, more than 40 pixels with a partial alpha that are all white (the transparent fill does not
darken them), and every fully transparent pixel is (0, 0, 0, 0). A red 40 by 40 image rotated by 45 degrees with the
fill (0, 0, 255, 128) has the fill exactly at the corner, and every edge pixel with an alpha between 128 and 255 has the
red and blue the premultiplied formula gives from its alpha, with no green.

##### CanvasNet-Canvas-SurfaceGeometry-RotateFilter: Sampling

**Tests**: `Surface_RotateArbitrary_Nearest_CopiesPixelsWithoutBlending`,
`Surface_RotateArbitrary_Bilinear_BlendsAHardEdge`, `Surface_RotateArbitrary_FastPathAndGenericPath_GiveSamePixels`

Every pixel of a nearest-sampled rotation of a two-color image is one of the two colors or the fill, and the fill
appears. Rotating a black and white image by 10 degrees with bilinear sampling gives at least 10 opaque gray pixels
along the edge. A 37 by 29 random image with translucent pixels, rotated by 7, 30, 45, 90, 133, 200, 275, and -12.5
degrees with a transparent, a translucent, and an opaque fill, is identical with and without the fast paths.

##### CanvasNet-Canvas-SurfaceGeometry-RotateLimit: Size Limit

**Test**: `Surface_RotateArbitrary_ResultTooLarge_ThrowsBeforeAllocating`

An 8192 by 8192 image at 45 degrees would be 11586 by 11586; a quarter turn of it is 8192 by 8192; rotating an 8192 by
3400 image by 45 degrees throws `ArgumentException` naming 8197x8197 after allocating less than 1 MB.

##### CanvasNet-Canvas-SurfaceGeometry-SkewEstimate: Skew of Ruled Lines

**Tests**: `Surface_EstimateSkew_TiltedLines_FindsAngleWithinTolerance`,
`Surface_EstimateSkew_SignConvention_DescendingToRightIsPositive`,
`Surface_EstimateSkew_LargeImage_DownscalesAndStillFindsAngle`,
`Surface_EstimateSkew_LightGroundUnevenLighting_StillFindsAngle`,
`Surface_EstimateSkew_MarkContrastAtThreshold_IsMarkOnlyWhenAtLeastTenLevelsDarker`

400 by 300 pictures of eight ruled lines tilted by -12, -3, 0, 2.5, 7, and 14.5 degrees are estimated within 0.15
degrees, with a confidence of at least twice `SkewEstimate.MinConfidence` (0.4). After checking that the generated
picture really has its line higher at the left edge than at the right, lines tilted by 5 degrees have an estimate above
4.5 and lines tilted by -5 degrees one below -4.5. 3000 by 2000 thick lines tilted by 4 degrees (analyzed on a 1000 by
667 plane) are estimated within 0.15 degrees. Lines tilted by -6 degrees on paper that brightens by 80 levels from left
to right, the ink 70 levels darker than the paper at every place, are estimated within 0.15 degrees. A 340 by 20 picture
of identical rows with a period of 17 columns (one pixel of 100, ten of 111, and six of 110, so that every window mean is
exactly 110) has 380 marks that are exactly 10 levels below their window mean and is estimated; the same picture with a
mark of 101 (9 levels below) has none and is not estimated.

##### CanvasNet-Canvas-SurfaceGeometry-SkewNoStructure: No Structure and Correction

**Tests**: `Surface_EstimateSkew_BlankImage_IsNotEstimated`, `Surface_EstimateSkew_RandomNoise_HasLowConfidence`,
`SkewEstimate_IsCorrectableAndCorrectionDegrees_FollowThresholdsAndSign`,
`SkewEstimate_CorrectionDegrees_RotatedSurface_IsLevelled`

Uniform paper, paper with 30 dark pixels (fewer than the 100 needed), and a transparent surface give 0 degrees,
confidence 0, and not estimated. Noise of 400 by 300, 800 by 800, 1000 by 250, and 300 by 900 pixels is estimated but
with a confidence below `MinConfidence` and is not correctable; this pins the constant together with the line tests
above. `IsCorrectable` needs an estimate, at least 0.05 degrees, and a confidence of at least 0.4; `CorrectionDegrees` is
`360 - skew` for a positive skew and `-skew` otherwise; a picture tilted by 5 degrees rotated by its own
`CorrectionDegrees` has a skew within 0.3 degrees of 0.

##### CanvasNet-Canvas-SurfaceGeometry-SkewDeterministic: Determinism

**Tests**: `Surface_EstimateSkew_SameInputTwice_ReturnsIdenticalResult`,
`Surface_EstimateSkew_EqualScores_PrefersSmallerAbsoluteAngle`

A picture, and an exact copy of it, give identical estimates every time. The tie-break rule is checked on its own: a
higher score wins, equal scores go to the angle closer to 0, equal scores at the same distance go to the more negative
angle, and an angle is not better than itself.

##### CanvasNet-Canvas-SurfaceGeometry-Arguments: Invalid Arguments

**Tests**: `Surface_RotationInvalidArguments_Throw`, `Surface_RotationAndSkew_AfterDispose_ThrowObjectDisposedException`

A NaN or infinite angle throws `ArgumentOutOfRangeException` from `RotateArbitrary` and `RotatedSize`; a width or height
of 0 or above 8192 given to `RotatedSize` throws; the rotation and the skew estimation of a disposed surface throw
`ObjectDisposedException`.
