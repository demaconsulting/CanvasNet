### RenderCommand

<!-- cspell:ignore canvasnet jpeg jpg tiff tif exif pptx vsdx dpi scad openscad Dlabel grayscale deskew -->

#### Verification Approach

`RenderCommand` is verified with unit tests in `RenderCommandTests.cs`, `RenderCommandDocumentTests.cs` (pages,
resolution, and strict mode of the documents), `RenderCommandScadTests.cs` (the views and options of an OpenSCAD
model), `RenderCommandVegaLiteTests.cs` (the Vega-Lite charts), and `RenderCommandPipelineTests.cs` (the image
pipeline options and the verb commands). The command is run
 in-process: the tests
create a `Context` (with `--json` when the JSON document is wanted), call `Run`, and capture the console with the
`ToolHost` helper; failures are asserted as the `ToolException` the command throws (the registry would record the
command name on it). Inputs are generated in a temporary directory (rasters with the codecs, an EXIF segment injected
into a JPEG, a PNG that declares an oversized image, the PDF, presentation, and drawing the test project writes, and
the models and charts the tests write as text) plus the original SVG, model, and chart fixtures, and the outputs are
decoded with the
codecs and compared with the generated pixels.

#### Test Environment

N/A - standard test environment. The tests belong to the "Sequential" collection because they redirect the console.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The command is registered as `render` and `convert` with the documented options.
- The text line and the JSON document have the documented content.
- Sizes, background, quality, format, orientation, and overwrite options take effect; contradictory or malformed
  options are usage errors and write nothing.
- The chosen page, or every page, of a document is rendered at the resolution given; every output is checked before
  any page is written; unsupported features are reported or, with strict mode, refused.
- An output problem is reported before the input is decoded; a bad input is an expected error and leaves no output.
- A model is rendered from the view chosen, with its variables, time, default color, and triangle limit; the options
  and the operation, diagnostics, and echo are in the result; a model option for another kind of input, an unknown
  view, and a malformed value are usage errors; an exceeded limit, a syntax error, and a strict-mode unsupported
  feature are expected errors and leave no output.
- A chart is rendered at its laid-out size or the size asked for, over its own background or the one given, with no option
  of its own; the operation, the natural size, and the diagnostics are in the result; an unsupported feature is a note or,
  in strict mode, an error; an invalid chart is a `vega-lite-error`; a page other than 1 and a model option are
  errors; no
  failure leaves an output.
- The pipeline operations are applied in the order written, after the source sizing and before the output is written,
  and each is reported with its parameters and result size; a failing operation writes nothing and leaves an
  existing output untouched; a vector source is not scaled twice; the border color default follows the background.
- Each verb command writes exactly the file of the equivalent render invocation, accepts every render option, applies
  options after its own operation, reports itself as the command, and reports a missing, extra, or malformed operand
  as a usage error.

#### Test Scenarios

**RenderCommand_Metadata_DescribesCommandAndOptions** and **RenderCommand_DefaultRegistry_RegistersRenderAndConvert**:
the name, the alias, the summary, the usage line with examples, the forty-four options in order (the short alias `-o`),
every option with a description and a value name exactly when it takes a value; the default registry resolves both
names to the one command.

**RenderCommand_ReadSourceOptions_SameAsBefore**: the source options read from an empty command line are the defaults
(page 1, no resolution, scale, size, or background, automatic orientation, not strict, the automatic filter, no model
options); a command line with a page, resolution, scale, background with alpha, `--no-auto-orient`, `--strict`, a filter,
and a view gives exactly those settings; `--size` gives the width and height; `--size` with `--scale` and a width of 0
are usage errors, as they always were.

**RenderCommand_Run_PngToPng_WritesFileAndTextLine**: a PNG rendered to a PNG writes the same pixels and the one-line
text result.

**RenderCommand_Run_Json_HasDocumentedShape** and **RenderCommand_Run_SvgJson_ListsRenderAndFlattenOperations**: the
JSON document has the envelope, the tool, the input with its natural size, the operations (none for a PNG to BMP; the
render at 50x40 and the flatten over `#FFFFFF` for an SVG at width 50, as exact text), the output with its size and
byte count, and empty diagnostics and echo.

**RenderCommand_Run_TransparentToJpeg_FlattensOverBackground** and
**RenderCommand_Run_BackgroundTransparent_KeepsAlpha**: a JPEG of a transparent image is flattened over white or a
given color and reports the flatten; an SVG is white by default and keeps its alpha with a transparent background.

**RenderCommand_Run_SizeOption_ResizesRaster**, **RenderCommand_Run_WidthAndHeightWithEquals_AreExact**,
**RenderCommand_Run_InvalidSizeOrBackground_ThrowsUsageError**, and **RenderCommand_Run_SizeBeyondMaximum_ThrowsSizeLimit**:
scale, width, height, and size resize; both sides and the `--name=value` form are exact; every contradictory
combination, zero, negative, and non-numeric values, a malformed size, and a bad color are usage errors that write no
output; sizes beyond 8192 are `size-limit`.

**RenderCommand_Run_Quality_ChangesJpegSizeAndIsValidated** and **RenderCommand_Run_FormatOption_OverridesExtension**: a
higher quality gives a larger JPEG; 0, 101, and a word are usage errors; the format option overrides the extension and
an unknown format, an unknown extension, and no extension are usage errors reported before the input is read.

**RenderCommand_Run_ExistingOutput_OverwrittenUnlessNoClobber**,
**RenderCommand_Run_OutputProblemWithHugeInput_ReportsOutputProblemFirst**, **RenderCommand_Run_OutputIsInput_ThrowsSameInputOutput**,
and **RenderCommand_Run_OutputInNewDirectory_CreatesDirectory**: no-clobber refuses and keeps the file, the default
overwrites; with an oversized input and an existing output, the output problem is the one reported; the input as the
output is refused and untouched; a missing directory is created.

**RenderCommand_Run_JpegWithExif_AutoOrientUnlessDisabled**: a JPEG with the orientation 6 is written 8x12 with the
`auto-orient` operation reported, and 12x8 with `--no-auto-orient`.

**RenderCommand_Run_BadInput_ThrowsToolExceptionAndWritesNothing**,
**RenderCommand_Run_TruncatedDocument_ThrowsCorruptInputAndWritesNothing**,
**RenderCommand_Run_CommandLineMistakes_ThrowUsageError**, and **RenderCommand_Run_NullArguments_ThrowsArgumentNullException**:
a missing file, a truncated PNG, a text file, and a directory are `input-not-found`, `corrupt-input`,
`unsupported-format`, and `input-unreadable` with no output; a PDF, a presentation, and a drawing cut after 300 bytes
are `corrupt-input` with no output; a missing output, a missing or second input, an unknown
option with a suggestion, a flag with a value, and a missing value are usage errors; null arguments throw.

**RenderCommand_Run_DocumentDefaults_WritesFirstPage** and **RenderCommand_Run_DocumentJson_DescribesPageAndOperations**:
each document renders page 1 at its size at 96 dpi with a text line that ends with `page 1`; the JSON for page 2 of the
PDF at 120 dpi has the page, the page count, the natural size, the `render` operation (page, dpi, width, background),
the page in the output, and no diagnostics. **RenderCommand_Run_RasterJson_HasNoPageFields**: a PNG's result has no
`page` or `pageCount`.

**RenderCommand_Run_AllPagesWithPlaceholder_WritesOneFilePerPage**,
**RenderCommand_Run_AllPagesToDirectory_NamesFilesAfterInputAndPage**,
**RenderCommand_Run_AllPagesWithoutPlaceholder_ThrowsUsageError**,
**RenderCommand_Run_AllPagesOfSingleImage_WritesTheOutputAsGiven**,
**RenderCommand_Run_PlaceholderWithPage_IsReplacedByThatPage**,
**RenderCommand_Run_AllPagesWithOneExistingOutputAndNoClobber_WritesNothing**, and
**RenderCommand_Run_BadPageOptions_ThrowExpectedErrors**: every page is written to `page-{page}.png` in a new
directory with its own size and colors and the JSON lists both outputs and a page on every operation; an existing
directory and a new one named with a trailing separator receive `source-1` and `source-2` with the format's
extension; an output without `{page}` for two pages is a usage error with a hint and no file; a PNG accepts
`--all-pages` and is written as given; `{page}` is replaced for `--page 2` too; an existing second output with
`--no-clobber` is `output-exists` before page 1 is written and is left unchanged; page 3 is `page-out-of-range`,
`--page` with `--all-pages`, page 0, and `first` are usage errors.

**RenderCommand_Run_AllPagesWithLaterPageFailure_LeavesOutputsUntouched**,
**RenderCommand_Run_AllPagesWithLaterPageFailureToNewFolder_WritesNoFile**, and
**RenderCommand_Run_AllPagesSucceeding_PublishesAllAndReplacesExisting**: a crop that is inside page 1 but outside
page 2 fails the run with a `usage-error` after page 1 was encoded (a render failure, so no target is touched); the
existing page 1 output keeps its content, no
other file (and no temporary file) is in the folder, and a new folder holds no file; when every page succeeds both
outputs are published (an existing one replaced) as complete images and no temporary file remains. A failure of the final
per-target move is covered by **ImageWriter_PublishAll_FailureOnStep_RestoresEverything** (see the ImageWriter
verification).

**RenderCommand_Run_DpiAndSizeOptions_SizeThePage** and
**RenderCommand_Run_TransparentBackground_KeepsAlphaInPngAndFlattensJpeg**: `--dpi 192`, `--width 96`, `--scale 0.5`,
and `--size 200x200` (fitted and centered over a green background) give the documented sizes and pixels; a
fractional resolution for a drawing and a resolution of 0 are usage errors, one above 2400 is a usage error, and a
page that would exceed 8192 pixels is `size-limit`; a transparent background keeps the alpha of the PNG and is
flattened for the JPEG.

**RenderCommand_Run_UnsupportedFeature_IsReportedOrFailsWhenStrict**: a presentation with an EMF picture and a
drawing with an unknown geometry row produce one diagnostic each (source, `unsupported`, a code that starts with the
format, page 1, a message) and, with `--strict`, an `unsupported-feature` error and no file.

**RenderCommand_Run_AllPagesTwice_WritesIdenticalBytes**: every page of each document rendered twice to PNG and JPEG
gives identical bytes.

**RenderCommand_Run_ScadDefaults_Writes800SquareImage**, **RenderCommand_Run_EachView_WritesItsOwnImage**, and
**RenderCommand_Run_ViewAzimuthElevationProjection_AreReportedInTheOperation**: the model fixture renders to an
800x800 PNG with the one-line text result (no page), white in the corner and covered in the middle; a model that looks
different from every side renders from each of the seven views to seven different files; `--view=TOP` (the equals form,
in capitals) with `--azimuth 30`, `--elevation=-20` (a negative value after an equals sign), and `--projection
perspective` is reported as the `render` operation with the view `top`, the angles 30 and -20, the projection, the
size, and `#FFFFFF`, and without `defines`.

**RenderCommand_Run_Defines_ChangeTheImageAndAreReported** and
**RenderCommand_Run_TimeDefaultColorAndMaxTriangles_AreReported**: `-D width=5`, `--define width=40`, and the attached
form `-Dlabel=a b` give a different picture from the default, and the operation lists `label="a b"` and `width=40` (the
later definition of a name wins, one entry per name, in name order); `--time 0.5`, `--default-color #336699`, and
`--max-triangles 100000` are reported with their values.

**RenderCommand_Run_ScadDiagnosticsAndEcho_AreInTheJsonResult**,
**RenderCommand_Run_StrictWithUnsupportedFeature_FailsWithoutOutput**, and
**RenderCommand_Run_MaxTrianglesExceeded_FailsWithBudgetExceeded**: the JSON result of a model that echoes, warns, and
uses `surface` has the echo line and two diagnostics (source `scad`; severity `warning` with code `scad-warning`, then
`unsupported` with `scad-module-surface`; no page); with `--strict` the same model is an `unsupported-feature` error and
no file; `--max-triangles 1` is a `budget-exceeded` error of one line with the limit and a hint that names the option,
and no file.

**RenderCommand_Run_ScadSizeOptions_FollowTheNaturalSize**, **RenderCommand_Run_ScadBackground_IsAppliedAndFlattenedForJpeg**,
**RenderCommand_Run_ScadToEachOutputFormat_WritesDecodableFile**, and **RenderCommand_Run_ScadTwice_WritesIdenticalBytes**:
a scale of 0.25, a width of 100, a height of 60, and `--size 90x50` give 200x200, 100x100, 60x60, and 90x50; a blue
background fills the corner, a transparent one leaves a PNG clear there and gives a white corner in a JPEG that
reports the flatten after the render; the model is written as PNG, JPEG, BMP, and TIFF with the model in the middle;
two runs with a view, a perspective projection, and a definition give identical bytes.

**RenderCommand_Run_BadModelOption_ThrowsUsageError**, **RenderCommand_Run_UnknownView_ListsTheViews**,
**RenderCommand_Run_ModelOptionOnAnotherKind_ThrowsUsageError**, **RenderCommand_Run_PageTwoOfAModel_ThrowsPageOutOfRange**,
and **RenderCommand_Run_ModelWithSyntaxError_ThrowsScadError**: an unknown view or projection, an azimuth or time that
is not a number, an elevation of 95, -91, or `up`, a time of 1e12, a bad color, a triangle limit of 0, `many`, or three
billion, a definition without `=`, with a name that starts with a digit, or with a vector value are usage errors that
mention the option and write nothing; the unknown view names the views in its hint; `-D x=1` with a PNG, an SVG, and a
PDF is a usage error that names the kind; page 2 of a model is `page-out-of-range`; a model with a syntax error is a
`scad-error` that names the line and leaves no output.

**RenderCommand_Run_VegaLite_WritesPngAndReportsFormat**: the two-bar chart renders to a PNG whose size is the natural
size in the JSON result, which names the `vega-lite` format and has one `render` operation with the `UTC` time zone
and no
diagnostics; the corner is white and a point of the second bar, computed from the layout, has the default mark color.

**RenderCommand_Run_VegaLiteSizeOptions_FollowTheNaturalSize**,
**RenderCommand_Run_VegaLiteToEachOutputFormat_WritesDecodableFile**, and
**RenderCommand_Run_VegaLiteTwice_WritesIdenticalBytes**: a scale of 2, a width of 100, and `--size 90x50` give twice the
natural size, a height that keeps the aspect ratio, and 90x50; the chart is written as PNG, JPEG, BMP, and TIFF (a transparent
background leaves the corner clear in the formats that keep it and white in the JPEG); two runs with a scale of 1.5 give
identical bytes.

**RenderCommand_Run_VegaLiteDiagnosticsAndStrict_AreReportedOrFail** and
**RenderCommand_Run_VegaLiteError_UsesStableErrorCode**: a chart with an unknown property and an `arc` mark has three
diagnostics in the JSON result (source `vega-lite`; the `vega-lite-warning` warning, then the `vl-mark-arc` and
`vl-channel-theta` unsupported notes, with no page); with `--strict` it is an `unsupported-feature` error and no file; a
malformed chart is a `vega-lite-error` of one line that names the file, and plain Vega named like a chart is a
`vega-lite-error` with exit code 1 and the code in the failure document, through the entry point, with no output.

**RenderCommand_Run_VegaLiteWithPageTwoOrModelOption_Fails** and **RenderCommand_Usage_NamesVegaLite**: page 2 of a
chart is
`page-out-of-range`; `-D x=1` with a chart is a usage error that names the kind; the usage text names Vega-Lite
charts and has
a chart example, and says that the data files of a chart are read from the folder of the specification and below it
only, that a missing file is an error, that there is no data-folder option, and that nothing is read from the network.

**RenderCommand_Run_VegaLiteLocalDataResolvesRelativeToSpecFolder**: a chart whose rows are in a CSV file in a sub folder
beside its specification renders, whatever the current directory is, to the same PNG bytes as the chart with the rows
inline; a chart that names a missing file is a one-line `vega-lite-error` ("was not found") and writes no output; a chart
with a remote URL renders with the `vl-data-remote-url` diagnostic in the JSON result and nothing fetched, and with
`--strict` is an `unsupported-feature` error with no output; and `--data-dir` is a `usage-error`, because the command has
no option for another data folder.

**RenderCommand_Usage_DescribesModelOptionsAndIncludeRule**: the help of the command lists the nine model options (and
`-D`), the 800x800 default size, and the rule that include and use files are read from the folder of the model and
below only.

**RenderCommand_Help_ListsPipelineOptionsAndVerbs**: the help of `render` lists the pipeline options (the nine of the
geometry pipeline and the thirteen photo options and settings), and the
registry lists each verb in the command list, has a usage line for it, and resolves its name in any case.

**RenderCommand_Run_PhotoOptions_AppearInHelpAndRunInOrder**: every photo option is declared exactly once in the table
and in the options of a verb, the usage of `render` and of `rotate` mention the photo tools, and a chain of grayscale,
invert, brightness, contrast, automatic threshold, and grid on the 12x8 gradient reports its six operations in the order
written, writes a black and white picture, and tints the pixels on a grid line.

**RenderCommand_Run_RotateVerbAnyAngle_MatchesRenderEquivalent**: `rotate` with 30, -3.5, `45deg`, 0, and -90 and a fill
writes the bytes `render --rotate` writes.

**RenderCommand_Run_DeskewJson_ReportsAngleAndConfidence**: ruled lines tilted by 5 degrees give a `deskew` operation
in the
JSON with `skewDegrees` between 4.8 and 5.2, `rotateDegrees` equal to its negative, a confidence of at least 0.4,
`applied` true, and the size of the grown canvas that equals the size of the written file.

**RenderCommand_Run_RotateFillAndBackground_ChooseFill**: the corner exposed by a 45-degree rotation is white for an opaque
image, the color of `--rotate-fill`, the `--background` color when only that is given, the fill when both are given,
and transparent for an image with a transparent pixel.

**RenderCommand_Run_MultiOpJson_ListsOperationsInOrder**: a translucent PNG enlarged to 24 wide by `--width`, then
cropped, flipped, rotated, and padded, and written as JPEG, reports as exact text the loader's `resize`, the four
pipeline operations with their parameters and sizes (the border is `#00000000`, transparent, because a raster has no
background), and the JPEG's `flatten` last.

**RenderCommand_Run_OperationOrder_CropThenResizeDiffersFromResizeThenCrop**: the same two options in the two orders
are reported in those orders and give 12x8 and 6x4 images.

**RenderCommand_Run_RasterWithWidthAndCrop_LoaderResizesFirst** and
**RenderCommand_Run_FilterWithRasterWidth_IsReportedWhenChosen**: `--width 24 --crop 0,0,12,8` crops the enlarged
image (the same bytes as `--resize 24x16 --crop 0,0,12,8` with the same filter, and the pixels are the enlarged corner);
the `filter` of the loader's resize is reported only when `--filter` was given, and changes the file.

**RenderCommand_Run_VectorWithWidthAndCrop_SizesOnceThenCrops**: an SVG of 100x80 at `--width 50` is rendered at 50x40
once and then cropped to 10x10; the operations are `render`, `flatten`, `crop`.

**RenderCommand_Run_PadOnRaster_DefaultsToTransparentThenBackground** and
**RenderCommand_Run_PadOnVector_DefaultsToTheBackgroundOfThePage**: the border of a raster is transparent, takes the
background when one is given, and `--pad-color` wins over it; the border of an SVG is white, or transparent with
`--background transparent`.

**RenderCommand_Run_Fit_ShrinksOnlyUnlessUpscaleAllowed**, **RenderCommand_Run_IdentityOperations_KeepThePixels**, and
**RenderCommand_Run_ExifJpegThenRotate_RotatesTheUprightImage**: `--fit 6x6` of 12x8 gives 6x4, a box larger than the
image leaves the pixels unchanged, `--allow-upscale` enlarges to 24x16; a full-size crop, a border of 0, and two flips
about both axes keep every pixel; a JPEG stored with the EXIF orientation 6 is turned upright (8x12) and then rotated
by 90 degrees, which equals rotating the stored image by 180 degrees, with `auto-orient` then `rotate` reported.

**RenderCommand_Run_MultiOpChain_EveryInputKind_WritesExpectedSize**,
**RenderCommand_Run_AllPagesWithPipeline_AppliesToEveryPage**, and **RenderCommand_Run_MultiOp_IsDeterministic**: a chain
of crop (left half, as percent), flip, resize, and pad gives a 12x12 PNG for every one of the eleven input kinds; the
top-left quarter of both pages of the PDF is 96x64 and 64x96 with a `crop` operation naming each page; the same chain
written twice is byte-identical as PNG, JPEG, BMP, and TIFF.

**RenderCommand_Run_BadPipeline_IsExpectedErrorAndWritesNothing**, **RenderCommand_Run_FailingStep_LeavesExistingOutputUntouched**,
and **RenderCommand_Run_PadBeyondMaxDimension_IsRefusedWithSizeLimit**: malformed operands (an angle that is not a number)
and options, a crop outside the image (also after an earlier crop), a size or border beyond the maximum, a border color
without a pad, a rotation fill without a rotation, a clip without auto-levels, a trim tolerance without trim, an upscale
switch without a fit, a grid spacing or levels out of range, and an unknown option are usage or size errors with the
documented messages and no output; a crop that fails after a pad leaves an existing output file as it was; a border that
makes 8193 pixels is `size-limit` naming `8193x8188`.

**RenderCommand_Verb_Metadata_DescribesVerbAndSharesRenderOptions** and **RenderCommand_Verb_UnknownVerb_ThrowsArgumentException**:
each verb has its own name, no alias, a summary, the usage line with its operand and examples, and the options of
`render`; a name that is not a verb cannot be constructed.

**RenderCommand_Verb_EqualsRenderEquivalent_WritesIdenticalFiles**: twelve verb invocations (both geometry forms and
percentages, resize with and without a filter, the three angles, the three flips, and one and four border widths with a
color) write the same bytes as PNG and as JPEG, and print the same result line, as `render` with the equivalent option.

**RenderCommand_Verb_ExtraPipelineOptions_AreAppliedAfterTheVerbOperation**,
**RenderCommand_Verb_SameOptionAlsoGiven_VerbOperationIsFirst**, and **RenderCommand_Verb_Json_NamesTheVerbAndListsItsOperation**:
`crop` with `--resize` and `--pad` options applies them after the crop whether they stand before or after the operand,
and writes the file of the equivalent `render`; a `--crop` option after the verb's crop is relative to the first
result; the JSON of `rotate` names the command `rotate` and lists the one `rotate` operation with its size.

**RenderCommand_Verb_EveryInputKind_RotatesNaturalSize** and **RenderCommand_Verb_DocumentPageAndDpi_AreHonored**:
`rotate 90` of each of the eleven input kinds writes the natural size with its sides swapped; `crop` with `--page 2
--dpi 192` crops the 256x384 page to its left half.

**RenderCommand_Verb_MissingOrExtraArguments_AreUsageErrors**: a verb without its operand, with two operands, or
without an input, and without `-o`, are usage errors that name the verb and the operand and show an example, and write
nothing.
