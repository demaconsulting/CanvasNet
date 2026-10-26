### ImagePipeline

<!-- cspell:ignore canvasnet jpeg lanczos zabcz zdefz ccdd eeff -->

#### Verification Approach

`ImagePipeline` is verified with unit tests in `ImagePipelineTests.cs`. The operations are applied to tiny surfaces
built by the test, whose pixels are numbered in reading order (the letters `a`, `b`, `c`, and so on), so that the
expected result of every transformation is a short literal such as `da/eb/fc` for a rotation, written by hand from the
definition of the operation and not computed by the code under test. Parsing is tested with an `ArgumentReader` built
against the option table of `RenderCommand`, so the options the unit reads are the ones the command declares. The
resampling filters are checked on flat colors (which every filter must keep) and on a hard edge (where they must
differ); exact filter arithmetic belongs to the core library.

#### Test Environment

N/A - standard test environment; the tests use only in-memory surfaces.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Rotations, flips, pads, and crops give exactly the hand-written pixels, for a non-square image.
- Operations are applied in the order written: crop then resize differs from resize then crop; a verb's operation comes
  first.
- A crop past an edge is cut off and reported, a crop outside the image or smaller than a pixel is a usage error.
- Fit shrinks, never enlarges unless allowed; every filter gives the exact size.
- Any angle that is not a right angle is a usage error that says it is not supported yet.
- A size, a box, or a border above 8192 pixels is `size-limit`, and exactly 8192 works.
- The input is never modified or disposed, and the same operations give the same pixels.

#### Test Scenarios

**ImagePipeline_Apply_Rotate90_TurnsClockwiseAndSwapsSize**, **ImagePipeline_Apply_Rotate180_ReversesBothAxes**,
**ImagePipeline_Apply_Rotate270_TurnsCounterClockwiseAndSwapsSize**, and
**ImagePipeline_Apply_NegativeAngleAndFullTurn_AreEquivalentRotations**: the 3x2 image `abc/def` becomes `da/eb/fc`
(2x3), `fed/cba`, and `cf/be/ad` (2x3); `-90` equals 270, and four quarter turns restore the image.

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
angles of 45, 0, 360, and 12.5 degrees say the rotation is not supported yet; a word, a bad axis, a three-value
rectangle or border, a zero width, a negative border, a bad color, and an unknown filter are usage errors that name
the option; an upscale switch without a fit and a border color without a pad are usage errors.

**ImagePipeline_Parse_BeyondMaxDimension_ThrowsSizeLimit**, **ImagePipeline_Apply_PadBeyondMaxDimension_ThrowsSizeLimit**,
and **ImagePipeline_Apply_PadToMaxDimension_Succeeds**: sizes, boxes, and borders above 8192 are `size-limit` at parse
time; a border that makes 8200 pixels is `size-limit` naming `8200x201`; one that makes exactly 8192 works.

**ImagePipeline_Apply_Input_IsNeverModifiedOrDisposed**, **ImagePipeline_Apply_SameInputTwice_GivesIdenticalPixels**,
and **ImagePipeline_Parse_UnknownVerb_ThrowsArgumentException**: an empty list gives an independent copy, a failing
chain leaves the input usable, the same chain twice gives equal pixels, and a verb that does not exist or has no
operand is a defect of the caller.
