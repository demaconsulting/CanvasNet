### ImageComparer

<!-- cspell:ignore canvasnet psnr rmse ssim -->

#### Verification Approach

`ImageComparer` is verified with unit tests in `ImageComparerTests.cs` in the Viewer test project. The tests write
solid-color PNG files of known sizes and colors to a temporary directory, probe them, and call `ImageComparer.Execute`
with a request that the test adjusts with `with`; the expected measures are known from the colors (identical images, black
against white). Nothing is mocked; the measures themselves are verified with the core `SurfaceComparison` unit and the
thresholds with `ImageComparison`.

#### Test Environment

N/A - standard test environment; the tests write only to temporary directories that each test deletes.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Identical images are reported as identical, with no checks and no pictures.
- A threshold that is not met is a failed check in the result, not an exception.
- Inputs of different sizes are `size-mismatch` unless a resize is requested, and a resize to a side fits the other input
  and reports the loaded size.
- A grid larger than the images is a usage error; a picture that is one of the inputs is `same-input-output`.
- The pictures that were asked for are written, in the order difference, side by side, overlay.

#### Test Scenarios

**ImageComparer_Execute_IdenticalImages_ReportsIdentical**: two red 20 by 10 images are identical, with no checks, no
pictures, the sizes 20 by 10, and the labels `a` and `b`.

**ImageComparer_Execute_ThresholdNotMet_IsReturnedAsFailedCheck**: black against white with a mean-error limit of 1 is not
identical, and the one check failed with its metric and measured value.

**ImageComparer_Execute_DifferentSizesWithoutResize_ThrowsSizeMismatch**: a 20 by 10 image against a 10 by 10 image without
a resize is `size-mismatch`, and the message names both sizes.

**ImageComparer_Execute_ResizeToA_FitsTheSecondInputAndReportsIt**: a 20 by 10 image against a 10 by 10 image with a resize
to side `a` gives both sides 20 by 10, the second side reports that it was loaded at 10 by 10 and a `fit` step, and the
first side has no operations.

**ImageComparer_Execute_GridLargerThanImage_ThrowsUsageError**: a grid with more columns than the image has pixels is
`usage-error`.

**ImageComparer_Execute_Pictures_AreWrittenInOrder**: the three pictures are written and listed as diff, sideBySide, and
overlay, the difference picture has the size of the images, and the side-by-side picture is at least three images wide.

**ImageComparer_Execute_PictureIsAnInput_ThrowsSameInputOutput**: a difference picture whose path is an input is
`same-input-output` and the input is unchanged.
