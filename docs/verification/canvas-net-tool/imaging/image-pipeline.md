### ImagePipeline

<!-- cspell:ignore canvasnet jpeg lanczos zabcz zdefz ccdd eeff grayscale deskew luma otsu deskewing -->

#### Verification Approach

`ImagePipeline` is verified with unit tests in `ImagePipelineTests.cs`. The operations are applied to tiny surfaces
built by the test, whose pixels are numbered in reading order (the letters `a`, `b`, `c`, and so on), so that the
expected result of every transformation is a short literal such as `da/eb/fc` for a rotation, written by hand from the
definition of the operation and not computed by the code under test. Parsing is tested with an `ArgumentReader` built
against the option table of `RenderCommand`, so the options the unit reads are the ones the command declares. The
resampling filters are checked on flat colors (which every filter must keep) and on a hard edge (where they must
differ); exact filter arithmetic belongs to the core library. The photo operations are checked on one-row surfaces with
hand-computed pixels (grayscale, invert, threshold, and the tables), on the generated picture of a tilted sketch
(`FixtureFactory.SketchPhoto`, drawn with plain arithmetic independent of the rotation code) for deskew, and on small
solid or two-tone images for rotation and trim; the arithmetic of each unit it calls is verified with that unit.

#### Test Environment

N/A - standard test environment; the tests use only in-memory surfaces.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Rotations, flips, pads, and crops give exactly the hand-written pixels, for a non-square image.
- Operations are applied in the order written: crop then resize differs from resize then crop (and crop then deskew from
  deskew then crop); a verb's operation comes first.
- A crop past an edge is cut off and reported, a crop outside the image or smaller than a pixel is a usage error.
- Fit shrinks, never enlarges unless allowed; every filter gives the exact size.
- Any angle is accepted: a multiple of 90 is an exact move (0 and full turns are copies), another angle expands the
  canvas, fills the corners by the documented rule, and uses the nearest or the bilinear kernel; a canvas above 8192
  pixels is `size-limit`; a value that is not an angle is a usage error.
- The tone adjustments, deskew, trim, and grid give the documented pixels and report their parameters, the level and
  points they chose, the angle and the confidence.
- Settings without their operation, and malformed or out-of-range values, are usage errors that name the option.
- A size, a box, or a border above 8192 pixels is `size-limit`, and exactly 8192 works.
- The input is never modified or disposed (also for in-place operations and failures), and the same operations give the
  same pixels; a chain of adjustments equals the steps run separately.

#### Test Scenarios

**ImagePipeline_Apply_Rotate90_TurnsClockwiseAndSwapsSize**, **ImagePipeline_Apply_Rotate180_ReversesBothAxes**,
**ImagePipeline_Apply_Rotate270_TurnsCounterClockwiseAndSwapsSize**, and
**ImagePipeline_Apply_NegativeAngleAndFullTurn_AreEquivalentRotations**: the 3x2 image `abc/def` becomes `da/eb/fc`
(2x3), `fed/cba`, and `cf/be/ad` (2x3); `-90` equals 270, and four quarter turns restore the image.

**ImagePipeline_Apply_ZeroAndFullTurn_AreCopies**: rotating by 0, 360, -360, 720, and 0.0 gives a different surface with
the same pixels `abc/def` and reports a rotation by 0.

**ImagePipeline_Apply_ArbitraryAngle_ExpandsCanvasAndReports**: a solid red 100x50 image rotated by 30 degrees is
112x94 with white corners and a red middle, and the step reports the angle 30, the fill `#FFFFFF`, the filter
`bilinear`, and the size.

**ImagePipeline_Apply_NegativeObliqueAngle_EqualsComplement**: -30 equals 330, 390 equals 30, and `30deg` equals 30,
pixel for pixel.

**ImagePipeline_Apply_RotateFill_DefaultRuleOpaqueWhiteTransparentOrBackground**: the corner of a rotated opaque image is
white, that of an image with one transparent pixel is transparent, the user's background wins over both, `--rotate-fill`
wins over the background, and `transparent` gives a transparent corner.

**ImagePipeline_Apply_RotateFilterNearest_UsesNearest**: a black and white image rotated by 20 degrees has only black and
white pixels with the nearest filter and more than 20 blended pixels with another filter, and the reported filter is
`nearest` or `bilinear`.

**ImagePipeline_Apply_RotateTooLarge_ThrowsSizeLimit**: an 8192x100 image rotated by 1 degree would be 8193 pixels
wide and
is a `size-limit` error that names the size and hints at `--fit`.

**ImagePipeline_Parse_AdjustmentOptions_KeepCommandLineOrder**: the photo options parse into their operations in the
order written with the settings of the whole line (clip, trim tolerance, fill, filter) and, when a setting is absent,
the defaults: clip 1, tolerance 8, the default grid color, and the normalized angle 270 for -90.

**ImagePipeline_Apply_GrayscaleInvertThreshold_ProduceExpectedPixels**: a red, a translucent green, and a dark pixel
become the grays 77, 149, and 18 with their alphas, the complements, and, with the threshold 100 (luma over white 77,
202, and 18), black, white, and black; the steps report their size and the level.

**ImagePipeline_Apply_ThresholdAuto_ReportsLevel**: five pixels at level 20 and five at 200 give the automatic level 109,
reported with `auto` true, and split into black and white.

**ImagePipeline_Apply_LevelsBrightnessContrast_ApplyTablesInOrder**: the level 100 through levels 50..150 (128), brightness
+10 (138), and contrast 0 (138) keeps its alpha, while brightness before levels gives 153.

**ImagePipeline_Apply_AutoLevels_ReportsPointsAndFlatImageIsUnchanged**: levels 100 to 145 become 0 to 255 and are reported
(clip 1, black 100, white 145, applied); a flat image is unchanged and reported as not applied, without points.

**ImagePipeline_Apply_Deskew_RotatesByNegativeSkewAndReportsAngle**: ruled lines tilted by 5 degrees are found within 0.2
degrees, the correction is exactly the negative of the skew, the confidence is at least 0.4, the canvas grows, and the
estimate of the result is within 0.3 degrees of level.

**ImagePipeline_Apply_DeskewNoStructure_LeavesImageUnchanged**: a blank image and lines that are already level are copied
unchanged, `applied` is false, the correction is 0, and no fill is reported.

**ImagePipeline_Apply_Trim_CropsToContentAndReportsBox**: a 6x4 black block in a white 20x20 image with a speck 10 levels
off white is cropped to the speck and block (10x10) at the default tolerance of 8 and to the block (6x4 at 5,7) with a
tolerance of 12, and the steps report the tolerance and the box.

**ImagePipeline_Apply_TrimAllBackground_IsUnchangedAndReportsEmpty**: an all-white image is copied unchanged and
reported as
empty and not trimmed.

**ImagePipeline_Apply_Grid_DrawsOverCopyAndNeverModifiesInput**: a grid of spacing 50 draws red lines on the result and
none on the input, the default color is `#FF00FFA0`, and the step reports the spacing, color, 2 and 2 lines, and the labels.

**ImagePipeline_Apply_InPlaceOperations_NeverModifyTheInputAndDisposeIntermediates** and
**ImagePipeline_Apply_ChainedAdjustments_MatchSeparateRuns**: a single in-place operation, a chain of five, an operation
after a crop, and a chain that ends in a failure all leave the input as it was and give other surfaces; a chain of
grayscale, levels, contrast, and an automatic threshold equals the four steps run one after the other.

**ImagePipeline_Apply_OperationOrder_CropThenDeskewDiffersFromDeskewThenCrop**: cropping half of a tilted sketch and then
deskewing grows the canvas beyond the crop, while deskewing and then cropping gives exactly the crop.

**ImagePipeline_Apply_Flip_MirrorsAboutTheAxis**: `h` and `horizontal` give `cba/fed`, `v` and `vertical` give `def/abc`,
and `both` gives `fed/cba`, all 3x2.

**ImagePipeline_Apply_PadOneValue_AddsBorderOnEverySide**, **ImagePipeline_Apply_PadFourValues_UsesLeftTopRightBottom**,
**ImagePipeline_Apply_PadColor_DefaultComesFromCallerAndOptionOverridesIt**,
**ImagePipeline_Apply_PadTransparent_LeavesBorderTransparent**, and
**ImagePipeline_Apply_PadSemiTransparentColor_KeepsStraightAlpha**: a border of 1 in red surrounds the image
(`zzzzz/zabcz/zdefz/zzzzz`); `1,0,2,3` puts 1 pixel on the left, none on top, 2 on the right, and 3 below; without
`--pad-color` the border has the default the caller passes (and the result reports it), with it the option wins; a
transparent border is `(0,0,0,0)`; `#11223380` is stored as given.

**ImagePipeline_Apply_CropPixels_CutsOutTheRectangle**, **ImagePipeline_Apply_CropPercent_IsRelativeToTheImage**,
**ImagePipeline_Apply_CropPercentHalfPixel_RoundsEdgesAwayFromZero**, and
**ImagePipeline_Apply_CropPastEdge_IsClampedAndReported**: both geometry forms and the `px` unit cut out the expected
pixels; percentages are of the image at that point (the second crop of two is of the first result); the edge at 1.5
pixels rounds to 2 so two halves have 2 and 1 pixels; a region past the edge is cut off and the operation reports
the real rectangle and `clamped`.

**ImagePipeline_Apply_CropOutside_ThrowsUsageError** and **ImagePipeline_Apply_CropRoundsToNothing_ThrowsUsageError**:
regions that start at or beyond an edge are usage errors naming the 3x2 image and leaving the input intact; 0.1 percent
of 3 pixels is smaller than a pixel.

**ImagePipeline_Apply_ResizeNearest_ReplicatesPixels**, **ImagePipeline_Apply_ResizeArea_AveragesPixels**,
**ImagePipeline_Apply_ResizeEveryFilter_GivesExactSizeAndKeepsFlatColor**, and
**ImagePipeline_Apply_ResizeFilters_DifferOnAHardEdge**: doubling `ab/cd` with the nearest filter gives
`aabb/aabb/ccdd/ccdd`; shrinking to one pixel averages (and the default is the area filter when shrinking); each of
the six filter names (and one in capitals) gives the exact size when shrinking and enlarging and keeps a flat color;
nearest keeps a hard edge hard while bilinear blends it and lanczos3 differs from bilinear.

**ImagePipeline_Apply_FitLargerImage_ShrinksIntoBox**,
**ImagePipeline_Apply_FitSmallerImage_IsUnchangedUnlessUpscaleAllowed**, and
**ImagePipeline_ParseFilter_NamesAnyCase_RoundTripThroughFilterName**: images of 100x50, 40x10, and 10x80 fit a 20x20
box as 20x10, 20x5, and 3x20; a small image is a copy with `scaled` false, and 20x8 with upscaling; the filter names
parse in any case and round-trip.

**ImagePipeline_Apply_OperationOrder_CropThenResizeDiffersFromResizeThenCrop**,
**ImagePipeline_Parse_KeepsCommandLineOrderAndPutsVerbFirst**, **ImagePipeline_Parse_EachVerb_ParsesItsOperand**, and
**ImagePipeline_Apply_Chain_ReportsEachStepInOrderWithSizes**: the same two operations in the two orders give
4x4 `aabb/aabb/eeff/eeff` and 2x2 `aa/aa`; the parse order equals the command-line order with the verb first (the
same option twice stays twice); each verb parses its operand into its operation, with the shared filter and border
color; a chain of five operations is reported in order with the size after each and its parameters.

**ImagePipeline_Parse_BadValue_ThrowsUsageError** and **ImagePipeline_Parse_ModifierWithoutItsOperation_ThrowsUsageError**:
a word, `NaN`, or infinity as an angle, a bad axis, a three-value rectangle or border, a zero width, a negative border,
a bad color, an unknown filter, a threshold of 256, -1, or a word, levels with the black level not below the white
level or with one, four, or out-of-range values, a brightness or contrast out of range, a clip of 21 or -1, a trim
tolerance of 256, a grid spacing of 4 or 5000, and a bad grid color are usage errors that name the option; each setting
(upscale, border color, rotation fill, auto-levels clip, trim tolerance) without its operation is a usage error, and is
accepted with it (the rotation fill also with deskew).

**ImagePipeline_Parse_BeyondMaxDimension_ThrowsSizeLimit**, **ImagePipeline_Apply_PadBeyondMaxDimension_ThrowsSizeLimit**,
and **ImagePipeline_Apply_PadToMaxDimension_Succeeds**: sizes, boxes, and borders above 8192 are `size-limit` at parse
time; a border that makes 8200 pixels is `size-limit` naming `8200x201`; one that makes exactly 8192 works.

**ImagePipeline_Apply_Input_IsNeverModifiedOrDisposed**, **ImagePipeline_Apply_SameInputTwice_GivesIdenticalPixels**,
and **ImagePipeline_Parse_UnknownVerb_ThrowsArgumentException**: an empty list gives an independent copy, a failing
chain leaves the input usable, the same chain twice gives equal pixels, and a verb that does not exist or has no
operand is a defect of the caller.
