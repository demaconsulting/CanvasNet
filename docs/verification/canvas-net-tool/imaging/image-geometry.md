### ImageGeometry

<!-- cspell:ignore canvasnet bilinear premultiplied deskew downscales -->

#### Verification Approach

`ImageGeometry` is verified with unit tests in `ImageGeometryTests.cs`. The canvas size is checked as arithmetic on
hand-computed examples (100 by 50 at 90, 45, and 30 degrees). The rotation mapping is checked with a single marker
pixel in a small image, rotated with nearest sampling so that its exact destination is known; the interpolation is checked
on solid images (the interior keeps the source color), on a linear gradient (bilinear sampling reproduces a linear
function exactly, so rotating by 3 degrees and back differs only by rounding), on a hard edge (intermediate grays appear),
and on fills of every kind (opaque, transparent, semi-transparent), where the expected edge pixels are recomputed from
their own alpha with the premultiplied formula. The fast paths are checked by rendering the same random, partly
translucent image at eight angles and three fills with and without them and requiring identical pixels. The trim bounds
are checked on small surfaces whose content box is known. The skew estimation is checked on generated pictures of ruled
lines at known tilts (drawn with plain arithmetic, not with the rotation code under test), with a bright and dark ground,
on a large picture that is analyzed on a reduced plane, and on blank and noise pictures that must not give a confident
answer; the tie-break is checked directly on its helper. The unit needs no files and no mocking.

#### Test Environment

N/A - standard test environment; the tests use only in-memory surfaces.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The rotated canvas follows the documented formula and a right angle does not gain a pixel.
- Rotation is clockwise about the centers, the interior keeps the source pixels, and a round trip by a small angle stays
  within rounding of the original.
- The corners are the fill color, a transparent fill fades the edge in alpha, and a semi-transparent fill blends in
  premultiplied space.
- Nearest copies source pixels, bilinear blends, and the fast paths give the pixels of the generic sampler.
- A result larger than 8192 pixels on a side is refused with `size-limit` before any pixel is allocated.
- The trim box follows the corner-majority background, the tolerance, and the transparent-pixel rule; an image without
  content has no box and is copied unchanged.
- Invalid arguments are rejected.
- The skew of ruled lines is estimated to within 0.15 degrees for tilts from -12 to 14.5 degrees, with the correct sign,
  on a large image, and under uneven lighting; a blank image is not estimated; random noise has a confidence below the
  threshold the pipeline applies; the same pixels give the same estimate.

#### Test Scenarios

**ImageGeometry_RotatedSize_RightAndObliqueAngles_ComputesCeilingOfBoundingBox** and
**ImageGeometry_RotatedSize_NearExactFit_DoesNotGainAPixel**: 100 by 50 is 100 by 50 at 0, 50 by 100 at 90 (and at -90,
270, and 450), 107 by 107 at 45, and 112 by 94 at 30; 160 by 100 at 30 is 189 by 167; 1 by 1 at 45 is 2 by 2; an angle and
the same angle plus or minus half a turn give the same size; an 8192 by 8192 image at 90 degrees stays 8192 by 8192.

**ImageGeometry_RotateArbitrary_Clockwise_MovesRightOfCenterBelowCenter**: in an 11 by 11 white image a marker at (8, 5),
right of the center, is at (5, 8) after a nearest-sampled quarter turn and not at (8, 5); a marker above the center at
(5, 2) moves to (8, 5).

**ImageGeometry_RotateArbitrary_SolidImage_FillsInteriorWithSourceColor**: the 7 by 7 block at the center of a solid image
rotated by 20 degrees has exactly the source color.

**ImageGeometry_RotateArbitrary_RoundTripSmallAngle_StaysCloseToOriginal**: a 100 by 60 gradient (level = x + y) rotated
by 3 degrees (to 104 by 66) and back (to 108 by 72) matches the original, offset by 4 columns and 6 rows, to within 2
levels over its central area.

**ImageGeometry_RotateArbitrary_Corners_AreFillColor**: the four corners of a 100 by 60 image rotated by 30 degrees (to
117 by 102) are exactly the red fill, with nearest and with bilinear sampling.

**ImageGeometry_RotateArbitrary_TransparentFill_EdgePixelsFadeInAlpha**: a white 40 by 40 image rotated by 45 degrees with
a transparent fill has a transparent black corner, a white center, more than 40 pixels with a partial alpha that are all
white (the transparent fill does not darken them), and every fully transparent pixel is (0, 0, 0, 0).

**ImageGeometry_RotateArbitrary_SemiTransparentFill_BlendsInPremultipliedSpace**: a red 40 by 40 image rotated by 45
degrees with the fill (0, 0, 255, 128) has the fill exactly at the corner, and every edge pixel with an alpha between
128 and 255 has the red and blue the premultiplied formula gives from its alpha (the straight blend would be much darker),
with no green.

**ImageGeometry_RotateArbitrary_Nearest_CopiesPixelsWithoutBlending**: every pixel of a nearest-sampled rotation of a
two-color image is one of the two colors or the fill, and the fill appears.

**ImageGeometry_RotateArbitrary_Bilinear_BlendsAHardEdge**: rotating a black and white image by 10 degrees with bilinear
sampling gives at least 10 opaque gray pixels along the edge.

**ImageGeometry_RotateArbitrary_FastPathAndGenericPath_GiveSamePixels**: a 37 by 29 random image with translucent pixels,
rotated by 7, 30, 45, 90, 133, 200, 275, and -12.5 degrees with a transparent, a translucent, and an opaque fill, is
identical with and without the fast paths.

**ImageGeometry_RotateArbitrary_ResultTooLarge_ThrowsSizeLimitBeforeAllocating**: an 8192 by 8192 image at 45 degrees
would be 11586 by 11586 and is `size-limit` with a hint about `--fit`; a quarter turn of it is accepted; rotating an
8192 by 3400 image by 45 degrees throws `size-limit` after allocating less than 1 MB.

**ImageGeometry_FindTrimBounds_UniformBorder_ReturnsContentBox**: a 5 by 3 black block at (4, 2) in a white 20 by 10 image
is the box (4, 2, 5, 3), and `Trim` returns exactly those pixels.

**ImageGeometry_FindTrimBounds_Tolerance_IgnoresNoiseBelowTolerance**: noise at distance 5 and 7 from white is ignored at
tolerances 8 and 7 and is content at 4 and 0.

**ImageGeometry_FindTrimBounds_TransparentBorder_TreatsAllTransparentPixelsAsEqual**: transparent pixels of different
colors at the four corners are all background, and an opaque and a translucent pixel are content.

**ImageGeometry_FindTrimBounds_CornerMajority_ChoosesMostFrequentCorner**: with three white corners and one red the
background is white and the red corner pixel is content; with two white and two black corners the top left wins.

**ImageGeometry_FindTrimBounds_AllBackground_ReturnsNull**: a uniform image, an image whose only difference is within the
tolerance, and a 1 by 1 image have no box, and `Trim` returns an equal copy that is a different object.

**ImageGeometry_Trim_ContentTouchingEdges_ReturnsFullImage**: content pixels on all four edges give the whole image.

**ImageGeometry_NullOrInvalidArguments_Throw**: null surfaces (for rotation, trim, and skew estimation), a NaN or infinite
angle, a width or height of 0, and tolerances of -1 and 256 throw.

**ImageGeometry_EstimateSkew_TiltedLines_FindsAngleWithinTolerance**: 400 by 300 pictures of eight ruled lines
(`FixtureFactory.SketchPhoto`, drawn with plain arithmetic independent of the rotation code) tilted by -12, -3, 0, 2.5,
7, and 14.5 degrees are estimated within 0.15 degrees, with a confidence of at least twice `MinConfidence` (0.4).

**ImageGeometry_EstimateSkew_SignConvention_DescendingToRightIsPositive**: after checking that the generated picture
really has its line higher at the left edge than at the right, lines tilted by 5 degrees have an estimate above 4.5 and
lines tilted by -5 degrees one below -4.5.

**ImageGeometry_EstimateSkew_LargeImage_DownscalesAndStillFindsAngle**: 3000 by 2000 thick lines tilted by 4 degrees
(analyzed on a 1000 by 667 plane) are estimated within 0.15 degrees with a high confidence.

**ImageGeometry_EstimateSkew_LightGroundUnevenLighting_StillFindsAngle**: lines tilted by -6 degrees on paper that
brightens by 80 levels from left to right, the ink 70 levels darker than the paper at every place, are estimated within
0.15 degrees.

**ImageGeometry_EstimateSkew_BlankImage_IsNotEstimated**: uniform paper, paper with 30 dark pixels (fewer than the 100
needed), and a transparent surface give 0 degrees, confidence 0, and not estimated.

**ImageGeometry_EstimateSkew_RandomNoise_HasLowConfidence**: noise of 400 by 300, 800 by 800, 1000 by 250, and 300 by
900 pixels is estimated but with a confidence below `MinConfidence`; this pins the constant together with the line tests
above.

**ImageGeometry_EstimateSkew_SameInputTwice_ReturnsIdenticalResult**: a picture, and an exact copy of it, give identical
estimates every time.

**ImageGeometry_EstimateSkew_EqualScores_PrefersSmallerAbsoluteAngle**: the tie-break rule is checked on its own: a
higher score wins, equal scores go to the angle closer to 0, equal scores at the same distance go to the more negative
angle, and an angle is not better than itself.
