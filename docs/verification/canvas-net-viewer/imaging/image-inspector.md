### ImageInspector

<!-- cspell:ignore canvasnet luma -->

#### Verification Approach

`ImageInspector` is verified with unit tests in `ImageInspectorTests.cs` in the Viewer test project. The tests write
solid-color PNG files to a temporary directory, probe them, and call `ImageInspector.Execute` with request records; the
expected colors and samples are known from the color of the file. Nothing is mocked; the analysis itself is verified with
the core `SurfaceAnalysis` unit.

#### Test Environment

N/A - standard test environment; the tests write only to temporary directories that each test deletes.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The colors of a flat image are that color alone, with the size of the image, page 1, and the white background.
- The sample of a pixel carries the window of the radius that was asked for.
- The operations of the request run before the analysis, so the coordinates and the pixels are those after them.
- A point outside the image is a usage error; a pixel request without a point is a defect.

#### Test Scenarios

**ImageInspector_Execute_Colors_ReportsTheDominantColor**: a red 10 by 6 image has one color with the fraction 1, the
size 10 by 6, page 1, and the white background, and no pixel sample.

**ImageInspector_Execute_PixelWithRadius_SamplesPixelAndWindow**: a pixel at 4,3 with a radius of 1 reports the pixel
and a
3 by 3 window around it, and no colors.

**ImageInspector_Execute_PointOutsideImage_ThrowsUsageError**: a point beyond the last column is `usage-error`, the
message says it is outside the 10x6 image, and the hint names the valid range up to 9,5.

**ImageInspector_Execute_Operations_AreAppliedBeforeTheAnalysis**: a 10 by 6 image padded by 2 with a blue border is 14
by 10
in the result, the pixel at 0,0 is the border color, and the `pad` step is reported.

**ImageInspector_Execute_TranslucentBackground_IsReportedFlattenedOverWhite**: a background of black with alpha 128 is
reported as the opaque color 127,127,127.

**ImageInspector_Execute_PixelWithoutPoint_ThrowsArgumentException**: a pixel request without a point throws
`ArgumentException`.

**ImageInspector_Execute_NullRequestMember_ThrowsArgumentNullException** and
**ImageInspector_Execute_NullOperationElement_ThrowsArgumentNullException**: a request without the source options or the
operations throws `ArgumentNullException` that names the member, and so does a null operation in the list (for
`operations`).

**ImageInspector_Execute_InvalidOperationValue_ThrowsUsageError**: a resize to 0x0 is a `usage-error` naming the
operation even though the probed file was deleted, so the check happens before the source is decoded.
