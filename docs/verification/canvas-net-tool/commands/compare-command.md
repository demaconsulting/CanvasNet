### CompareCommand

<!-- cspell:ignore canvasnet jpeg jpg tiff tif pptx scad psnr rmse ssim -->

#### Verification Approach

`CompareCommand` is verified with unit tests in `CompareCommandTests.cs` (the metrics, the grid, the thresholds, the
result, and the errors), `CompareCommandSourceTests.cs` (the sizes and the size normalization, the per-side options, the
documents and models, the background, and strict mode), and `CompareCommandOutputTests.cs` (the pictures, the formats,
and the checks of the output paths). The command is run in-process: the tests create a `Context` (with `--json` when the
JSON document is wanted), call `Run`, and capture the console with the `ToolHost` helper; failures are asserted as the
`ToolException` the command throws, and for a threshold that is not met also what the command wrote before it failed and
the failure document that `ResultWriter.Failure` builds from the exception. Inputs are generated in a temporary
directory: flat-colored PNG images, in which the metrics are known exactly (gray against the same gray with one channel
10 higher has an MAE of 10/3, an RMSE of the square root of 100/3, a PSNR of 32.90 dB, and every pixel above the default
tolerance), a copy with one changed pixel for the pictures, the presentation of the test project, the SVG and the model
fixtures, and a raster rendering of the SVG. The pictures are decoded with the codecs and their pixels are compared with
the values of the design.

#### Test Environment

N/A - standard test environment. The tests belong to the "Sequential" collection because they redirect the console.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The command is registered under `compare` with the documented options, the shared ones being the render command's.
- Identical inputs are reported as identical in one line; different inputs have the documented metrics and grid, in
  text and in the JSON document with its fields in order, and the PSNR of identical inputs is `null`.
- Inputs of different sizes are `size-mismatch` unless `--resize-to` is given; `a`, `b`, and an explicit size bring a
  raster input to the size by `contain` or `stretch`, and a vector input is rendered at the size and equals the raster
  made from it.
- Per-side options win over shared ones and are errors for an input of the wrong kind; shared model options without a
  model, a page that does not exist, and a background that is not opaque are errors; transparency is flattened over the
  background on both sides.
- The difference, side-by-side, and overlay pictures have the documented pixels and sizes, the formats follow the
  extensions, and an output that is an input, a second use of the same path, an existing file with `--no-clobber`, and a
  path that cannot be written are errors that leave no picture.
- Each metric passes on its own side of its limit; a threshold that is not met fails with `threshold-failed` after the
  result and the pictures were written, and with JSON the failure document carries the result fields and no success
  document is written.
- Mistakes are usage errors or expected input errors; the same comparison twice is identical.

#### Test Scenarios

**CompareCommand_Metadata_DeclaresNameSummaryUsageAndOptions**: the name, no alias, the summary, the usage line with
examples, every option declared once with a description, and the declaration of `--page` that is the render command's.

**CompareCommand_Run_IdenticalImages_ReportsIdentical**, **CompareCommand_Run_DifferentImages_ReportsMetricsAndGridInText**,
and **CompareCommand_Run_JsonIdenticalImages_PsnrIsNull**: identical inputs give the one line
`compared {a} with {b} (8x6): identical`; the different pair gives MAE 3.33, RMSE 5.77, the PSNR, 100.00% of pixels
differing, and a 2x2 grid of 5.8; with JSON identical inputs have a null PSNR, a similarity of 1, no worst cell, and a
passing PSNR threshold whose value is null.

**CompareCommand_Run_Json_WritesDocumentWithMetricsGridWorstCellsAndThresholds**: the top-level fields in order, the
tool, both inputs, the size, the options, the metrics (3.3333, 5.7735, 0.022641, the PSNR rounded to four decimals, 10,
48 pixels, a ratio of 1), the grid cells with their positions and sizes, the worst cells in row-major order when all are
equal, and empty thresholds, outputs, diagnostics, and echo.

**CompareCommand_Run_Tolerance_ChangesDiffPixelRatioOnly**: a tolerance equal to the difference leaves no differing
pixel, the default counts all 48, and no other number changes.

**CompareCommand_Run_GridOption_ReportsRequestedCells** and **CompareCommand_Run_InvalidGrid_ThrowsUsageError**: `3x2`
gives six cells and the default sixteen; `0x3`, `100x100`, `4`, `65x1`, `x4`, `4x`, and grids larger than the 8x6 image
are usage errors.

**CompareCommand_Run_ThresholdMet_Succeeds**, **CompareCommand_Run_Thresholds_EachMetricDirection**,
**CompareCommand_Run_InvalidThreshold_ThrowsUsageError**, and **CompareCommand_Run_DuplicateThreshold_ThrowsUsageError**:
a met threshold writes `passed` lines; each of the seven metrics passes on the side of its measured value that the
direction allows and fails on the other; ten malformed or out-of-range thresholds, and a metric given twice (in any
case), are usage errors.

**CompareCommand_Run_ThresholdNotMet_WritesResultThenThrowsThresholdFailed**: with `--diff` and two thresholds, the
standard output holds the summary, the line of the picture, and a `failed` and a `passed` line before the command throws
`threshold-failed` with only the failed check in its message and a hint, and the picture exists.

**CompareCommand_Run_ThresholdNotMetWithJson_ThrowsWithResultFieldsAndNoSuccessDocument**: in JSON mode nothing is
written to standard output, the picture exists, and the failure document built from the exception has `ok: false`, the
error, and then the result fields in order, with the failed check and the output.

**CompareCommand_Run_WrongNumberOfInputs_ThrowsUsageError**, **CompareCommand_Run_MissingInput_ThrowsInputNotFound**,
**CompareCommand_Run_CorruptSecondInput_ThrowsCorruptInputAndWritesNothing**, and
**CompareCommand_Run_NullArguments_ThrowsArgumentNullException**: zero, one, and three inputs, a missing file, a PNG cut
after 30 bytes (and no picture), and null arguments.

**CompareCommand_Run_SameInputsTwice_WriteIdenticalJsonAndBytes**: two runs with all three pictures give the same JSON,
the same text, and the same bytes of every picture.

**CompareCommand_Run_DifferentSizesWithoutResize_ThrowsSizeMismatchWithHint**, **CompareCommand_Run_ResizeToA_FitsRasterBToA**,
**CompareCommand_Run_ResizeToB_FitsAToB**, **CompareCommand_Run_ResizeToExplicitSize_FitsBoth**,
**CompareCommand_Run_ResizeToTooLarge_ThrowsSizeLimit**, and **CompareCommand_Run_InvalidResizeToOrFit_ThrowsUsageError**:
8x6 against 4x3 is `size-mismatch` naming both sizes with a hint; `a` enlarges the second to 8x6, `b` shrinks the first
to
8x6, and `10x10` with `stretch` resamples both, each with the loaded and compared size, a `fit` or `resize` operation,
and the `resize` field in the result; `9000x10` is `size-limit`; `--resize-to c` and `--fit cover` are usage errors.

**CompareCommand_Run_FitContainVsStretch_DifferInLetterboxing**: a 10x10 image against a 20x10 reference differs in half
of the pixels with `contain` (a white margin) and is identical with `stretch`.

**CompareCommand_Run_VectorAgainstItsRaster_RendersVectorNativelyAndIsIdentical**: the SVG fixture compared with its
200x160 raster as the reference with `--resize-to a` is rendered at 200x160 with no `fit` or `resize` operation, and the
two are identical.

**CompareCommand_Run_PerSidePages_CompareDifferentPagesOfDocuments** and **CompareCommand_Run_DocumentsInText_NamePages**:
slide 1 against slide 2 of one presentation differs, with each side's page and the page count in the result; a shared
`--page 1` gives two equal slides; `--b-page` wins over `--page`; the text names `{path} page 1` and `{path} page 2`.

**CompareCommand_Run_PerSideOptionsOnWrongKind_ThrowUsageError**: `--a-page`, `--b-dpi`, `--a-view`, and `--b-define` with
PNG inputs are usage errors that name the kind.

**CompareCommand_Run_ScadSharedOptions_ApplyToModelSideOnly**, **CompareCommand_Run_ScadOptionsWithoutModel_ThrowsUsageError**,
and **CompareCommand_Run_PerSideDefines_OverrideSharedDefines**: `--view` is applied to the model against a PNG (another
view gives another image) and skipped for the PNG; `--view` with two PNGs is a usage error; a shared `-D width=10` gives
two equal models and `--b-define width=30` changes the second.

**CompareCommand_Run_PageOutOfRange_NamesTheSide**: `--b-page 5` is `page-out-of-range` with `Input b:` in the message.

**CompareCommand_Run_Background_FlattensTransparencyBothSides** and **CompareCommand_Run_TransparentBackground_ThrowsUsageError**:
a half transparent blue agrees with the opaque half blue to within one level over black and not over the default white,
and `transparent` and `#ff000080` are usage errors.

**CompareCommand_Run_Strict_UnsupportedFeature_Fails**: a slide with a picture the renderer skips is reported in the
diagnostics with the side `b`, and with `--strict` is `unsupported-feature`.

**CompareCommand_Run_DiffOutput_WritesHeatmapOfDifferences** and **CompareCommand_Run_DiffStyleAbsAndGain_ChangePixels**:
a difference of 50 at gain 4 is (255, 255, 90), identical pixels are black, and the line of the picture ends with `[diff]`;
`abs` at gain 2 gives (0, 0, 100) and the heat map at gain 1 gives (150, 0, 0).

**CompareCommand_Run_SideBySide_WritesThreePanels** and **CompareCommand_Run_Overlay_WritesBlendAndWeightChangesIt**: the
side-by-side picture is 40x6 with the first image, the second image, and the heat map, separated by gray gaps; the overlay
of grays 100 and 200 is the integer blend at the default weight and the images themselves at weights 0 and 1.

**CompareCommand_Run_AllOutputs_WrittenTogetherAndListedInJson** and **CompareCommand_Run_OutputFormatFromExtension_WritesDecodableFile**:
all three pictures in a new folder are listed as `diff`, `sideBySide`, and `overlay` in that order with their widths and
sizes on disk; `.png`, `.bmp`, `.tif`, and `.jpg` give `png`, `bmp`, `tiff`, and `jpeg` files that decode at 8x6.

**CompareCommand_Run_OutputIsInputA_ThrowsSameInputOutputAndWritesNothing**,
**CompareCommand_Run_OutputIsInputB_ThrowsSameInputOutputAndWritesNothing**,
**CompareCommand_Run_TwoOutputsSameFile_ThrowsUsageError**,
**CompareCommand_Run_NoClobberAndExistingOutput_ThrowsOutputExistsAndKeepsFile**,
**CompareCommand_Run_ExistingOutput_IsOverwritten**, and **CompareCommand_Run_SecondOutputPathInvalid_LeavesNoFirstOutput**:
a picture that is an input leaves the input and every other picture untouched; `diff` and `overlay` spelled as one file
with `.` are a usage error; with `--no-clobber` an existing picture is kept and no other picture is written; by default
it is overwritten and no temporary file remains; a path inside a file fails before the first picture is written.

**CompareCommand_Run_InvalidOptionValues_ThrowUsageError**: `--quality 0`, `--tolerance 256` and `-1`, `--gain 0` and
`300`, `--overlay-weight 2`, `--diff-style glow`, and `--filter sharp` are usage errors.
