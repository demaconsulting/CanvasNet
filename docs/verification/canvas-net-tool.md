# System Verification Design

<!-- cspell:ignore canvasnet dpi pptx vsdx scad openscad psnr rmse ssim deskew deskewing grayscale luma otsu gamma -->

This document describes the system-level verification strategy for CanvasNetTool.

## Verification Approach

System-level verification uses two kinds of tests in `CanvasNetToolTests.cs`, which treat the tool as a black
box and assert only on observable behavior.

- **Out-of-process tests** run the built tool as a real process through the `Runner.Run` helper
  (`dotnet DemaConsulting.CanvasNet.Tool.dll ...`), exercising the full stack - argument parsing, dispatch,
  execution, and output - and validating the exit code and the combined console output. `Runner.Run` merges
  standard output and standard error, so assertions about one stream alone cannot be made at this level.
- **In-process tests** run `Program.Execute` through the `ToolHost` helper with a command registry that holds
  a purpose-made `TestCommand` (or with the default registry). They cover behavior that needs the two streams
  examined separately, or a command that fails or misbehaves on demand: the clean-output rule, the concise error
  report, the JSON failure document, command dispatch with global options in any position, the option forms read
  through `ArgumentReader`, and the re-throw of an unexpected exception.

Platform and runtime support is proven differently: the integration-test job of the build workflow installs the
packaged tool in each operating system and .NET runtime of its matrix and runs `canvasnet --validate` with a
results file; the self-test results (`CanvasNetTool_VersionDisplay` and `CanvasNetTool_HelpDisplay`) are the
evidence for the platform requirements, selected by the source filters in the platform requirements file. The same
run records the other self-tests (`CanvasNetTool_Formats`, `CanvasNetTool_RenderPng`, `CanvasNetTool_ConvertFormats`,
`CanvasNetTool_Info`, `CanvasNetTool_RenderSvg`, `CanvasNetTool_PdfMinimal`, `CanvasNetTool_RenderScad`,
`CanvasNetTool_CropResize`, `CanvasNetTool_RotateFlipPad`, `CanvasNetTool_Compare`, `CanvasNetTool_Montage`, `CanvasNetTool_Adjustments`, and
`CanvasNetTool_RenderVegaLite`), which
generate their own inputs and are evidence for the validation requirements. The self-validation suite is deliberately
kept small (fifteen tests); the compare, montage, and adjustments self-tests are smoke tests (the last runs every photo
operation once on a generated sketch), the Vega-Lite self-test is a smoke test of one chart, and the detailed coverage of these commands and operations is in the unit,
subsystem, and system tests.
The same job guards the package contents (no NuGet dependency at all, a `DotnetTool` package, and the tool, its
start-up files, the library assemblies, and the third-party libraries bundled for every target framework, with the Vega-Lite assembly and the licenses folder
that carries the third-party notices of the Vega-Lite library) and smoke-tests the installed tool (`formats --json`, an
SVG `render`, a `crop`, and the `info` and `render` of a small Vega-Lite chart); the self-validation covers the PDF,
SCAD (OpenSCAD), and Vega-Lite paths of the bundled libraries.

## Test Environment

System tests run on .NET 8.0, .NET 9.0, and .NET 10.0 on Windows, Linux, and macOS in CI; all scenarios are
expected to produce identical results everywhere. Out-of-process tests need the tool assembly and its
libraries in the test output directory, which the test project's project references provide. Temporary files
are created in the system temporary directory and removed by each test. Tests that redirect the console
belong to the "Sequential" collection because console redirection is process-wide.

## Acceptance Criteria

- All system tests pass with zero failures on every supported runtime and platform.
- Exit code 0 is returned for every valid invocation and exit code 1 for every invalid argument, unknown
  command, and expected failure.
- Expected failures are one-line messages on standard error with a stable error code and no stack trace; an
  unexpected exception is reported and re-thrown.
- A command's standard output contains only the command's own output.
- Results files are created at the requested paths when `--results` is used with `--validate`.
- Silent mode produces no console output.
- With `--json`, a command's standard output is exactly one JSON document, and an expected failure is also
  written as a failure document (unless `--silent` is given); the formats command lists the formats as text or JSON.
- `render` and `convert` write PNG, JPEG, BMP, and TIFF files from every supported input (PNG, JPEG, BMP, TIFF, GIF,
  JPEG 2000, SVG, the pages of PDF, PPTX, and VSDX documents, OpenSCAD models, and Vega-Lite charts) with the documented defaults
  (overwrite, flatten
  over white, EXIF orientation, 96 dpi, 800x800 for a model, 8192-pixel limit), the same bytes for the same input, and
  a stable error for
  every bad input; a document page, or every page, is chosen with `--page` and `--all-pages`, and a model view with
  `--view`, `--azimuth`, `--elevation`, `--projection`, `-D`, `--time`, `--default-color`, and `--max-triangles`; a
  chart is recognized by its `.vl.json` or `.vl` file name or its Vega-Lite `$schema`, drawn at its laid-out size over
  its own background with UTC dates and the bundled fonts, and an invalid chart is a `vega-lite-error`;
  `info` describes a
  file, the pages of a document, the declarations of a model, and the marks, data, and parameters of a chart, without
  rendering it.
- `compare` reports exact metrics (MAE, RMSE, PSNR, SSIM, largest channel difference, differing-pixel ratio) and a
  grid of per-cell errors, writes the difference, side-by-side, and overlay pictures, refuses images of different sizes
  unless `--resize-to` is given (rendering a vector source at that size), and with `--threshold` exits 1 with
  `threshold-failed` after writing its result, pictures, and (with `--json`) a failure document that carries the
  metrics; `montage` writes a contact sheet of images and of every page of documents, with bundled-font labels, and
  refuses a sheet that is too large before loading any input; both write the same bytes and text for the same inputs.
- The photo operations (`--grayscale`, `--invert`, `--threshold`, `--levels`, `--brightness`, `--contrast`,
  `--auto-levels`, `--deskew`, `--trim`, `--grid`, and a rotation by any angle with `--rotate-fill`) write the pixels
  their formulas give, report what they chose (the level of an automatic threshold, the points of auto-levels, the
  angle, correction, and confidence of deskew, the box of trim) in the JSON result, apply in the order written, and
  write the same bytes for the same input; `inspect colors` and `inspect pixel` report the dominant colors, the luma
  statistics, and the value of a pixel and its window as text or as one JSON document, in the coordinates of the image
  after the pipeline, write no file, and refuse a bad subcommand or a point outside the image as a usage error.
  They are verified in `CanvasNetToolTests.PhotoTools.cs`, on generated sketches.

## Test Scenarios

**CanvasNetTool_VersionFlag_Provided_OutputsVersion**: `--version` is the sole argument; the output is a
version number with no banner, copyright, or error text, and the exit code is 0.

**CanvasNetTool_HelpFlag_Provided_OutputsUsageInformation**: `--help` is the sole argument; the output contains
"Usage:", "Options:", `--version`, and `--help`, and the exit code is 0.

**CanvasNetTool_NoArguments_Invoked_DisplaysBanner**: the tool is invoked with no arguments; the output
contains the banner, the copyright notice, and the usage hint, and the exit code is 0.

**CanvasNetTool_ValidateFlag_Provided_RunsValidation**: `--validate` is the sole argument; the output contains
the "Total Tests:" and "Passed:" summary and the exit code is 0; every self-test of the run builds its temporary file
paths under a private directory with `PathHelpers.SafePathCombine`, so a pass also shows that the path rule accepts
every path the self-tests need.

**CanvasNetTool_ValidateWithTrxResults_Requested_GeneratesTrxFile**: `--validate` with `--results <path>.trx`;
a TRX file containing a `TestRun` element is created and the exit code is 0.

**CanvasNetTool_ValidateWithXmlResults_Requested_GeneratesJUnitFile**: `--validate` with `--results <path>.xml`;
a JUnit XML file containing a `testsuites` element is created and the exit code is 0.

**CanvasNetTool_ResultAlias_LegacyFlag_WritesResultsFile**: `--validate` with the legacy `--result <path>.trx`;
the file is created and the exit code is 0.

**CanvasNetTool_ValidateWithBadExtension_ExtensionInvalid_ReturnsNonZero**: `--validate` with a results path
with an unsupported extension; the exit code is non-zero and no file is created.

**CanvasNetTool_ValidateWithDepth_DepthThree_OutputsCorrectHeadingLevel**: `--validate --depth 3`; the report
heading has three hashes and the exit code is 0.

**CanvasNetTool_SilentFlag_Provided_SuppressesOutput**: `--version --silent`; the output is empty and the exit
code is 0.

**CanvasNetTool_LogFlag_Provided_WritesOutputToFile**: `--log <path>`; the log file is created, contains the
banner, and the same text appears on the console.

**CanvasNetTool_UnknownArgument_Provided_ReturnsError**: an unknown option (`--unknown`) before any command;
the exit code is non-zero and the output contains an error naming the option.

**CanvasNetTool_UnknownCommand_Provided_ReturnsConciseUsageError**: an unknown command name; the exit code is 1
and the output contains `Error: usage-error: Unknown command 'bogus'` with no stack trace.

**CanvasNetTool_CommandOutput_CommandRuns_HasNoBanner**: a command that writes one line runs in-process;
standard output is exactly that line and standard error is empty.

**CanvasNetTool_CommandDispatch_GlobalFlagsAnywhere_RunsCommandWithItsArguments**: `--silent` precedes the
command name and the command arguments include an option the global parser does not know; the command receives
exactly its own tokens and the global flag is honored.

**CanvasNetTool_ExpectedFailure_CommandThrowsToolException_ReportsConciseError**: a command throws a
`ToolException` with a hint; the exit code is 1, standard output is empty, standard error carries the code,
message, and hint, and no stack trace.

**CanvasNetTool_UnexpectedError_CommandThrowsUnexpectedException_IsReThrown**: a command throws an exception
that is not an expected failure; the same exception escapes the entry point and `Unexpected error:` was
written to standard error first.

**CanvasNetTool_Formats_Requested_ListsFormats**: `formats` is run as a real process; the output starts with the
input formats listing, names PNG and PDF, lists the output formats and the maximum image dimension, and has no
banner; exit code 0.

**CanvasNetTool_Formats_JsonRequested_WritesJsonDocument**: `formats --json` is run as a real process; the whole
output is one line that parses as a JSON document with schema version 1, `ok` true, the command name, PDF among
the inputs, PNG among the outputs, and the maximum dimension 8192.

**CanvasNetTool_Json_ExpectedFailure_WritesErrorDocumentAndStderrMessage**: a command fails with a `ToolException`
and `--json` is given; exit code 1, standard output is the failure document (schema version, `ok` false, command,
code, message, hint, no exit-code field), and standard error still carries the text report.

**CanvasNetTool_Json_BadGlobalOption_WritesErrorDocument**: `--json --bogus` fails before any command is known;
standard output is a `usage-error` document with a null command.

**CanvasNetTool_Json_SilentFlag_SuppressesErrorDocument**: a failing `formats extra --json --silent` returns exit
code 1 with nothing on standard output.

**CanvasNetTool_CommandLineGrammar_OptionForms_ReadByCommand**: a command reads options in the separate, equals,
short, and attached forms plus a repeated option and positional arguments through `ArgumentReader`; the options
are in command-line order under their canonical names and the positionals are in order.

**CanvasNetTool_CommandLineGrammar_UnknownOption_ReportsUsageError**: `formats --bogus` is run as a real process;
exit code 1 and a concise `usage-error` naming the option and the command, with no stack trace.

**CanvasNetTool_Render_SvgToPng_WritesImageAndResultLine**: the SVG fixture is rendered as a real process at twice
its natural size; exit code 0, the one-line result names the output, `200x160 png`, its byte count, and the input;
the image has the red square where the drawing puts it and white where the drawing is empty.

**CanvasNetTool_Render_SvgWithText_DrawsTextWithDefaultFont**: an SVG whose only content is black text in the
family list `Arial, sans-serif` is rendered as a real process (the tool registers no font); exit code 0 and the
image has dark pixels on the white background, proving the text is drawn rather than silently dropped.

**CanvasNetTool_Render_JsonRequested_WritesJsonDocument**: a PNG is rendered to a JPEG with `--quality 80 --json` as
a real process; the whole output is one line that parses as a success document for `render` describing the PNG input
and the JPEG output with its byte count.

**CanvasNetTool_Convert_EachOutputFormat_WritesDecodableFile**: a PNG is converted through the `convert` alias to PNG,
BMP, TIFF, and JPEG as real processes; each file decodes with the codec of its format to the source pixels (on
average, for JPEG).

**CanvasNetTool_Info_Requested_DescribesFile**: `info` on a BMP as a real process, as text and as JSON; the line starts
with the path, format, and size, and the document has the same facts.

**CanvasNetTool_Info_DocumentPages_DescribesPages**: `info` on the two-page PDF as a real process with `--all-pages`
as text (the page count, and both pages with their size in pixels and points and the rotation of the second) and
with `--page 2 --json` (the `document` object with the page count and the one page, with its rotation).

**CanvasNetTool_InputFormats_EveryFormat_AreReadable**: a source of every kind is rendered to a PNG in-process;
each succeeds with a result line, nothing on standard error, and an image of the size the source has.

**CanvasNetTool_Documents_EveryFormat_WritesDecodableFile**: page 2 of the PDF, the presentation, and the drawing is
rendered at 72 dpi to PNG, JPEG, BMP, and TIFF in-process; each succeeds with a result line that names the page, and
the file decodes to the size of the page in points.

**CanvasNetTool_Documents_RenderPageAndDpi_WritesPageAtSize**: page 1 of the PDF is rendered at 192 dpi as a real
process; the one-line result names the page and `384x256 png`, and the image has the red and blue rectangles where
the document puts them and white elsewhere.

**CanvasNetTool_Documents_AllPages_WritesEveryPage**: every page of each document is rendered with `--all-pages` to
`{page}.png` in a directory; the result lists one output per page, and each file decodes to the size of its page.

**CanvasNetTool_Documents_UnsupportedFeature_ReportedAndStrictFails**: a presentation with an EMF picture and a
drawing with a geometry row of an unknown kind render with one diagnostic naming the source; with `--strict` the exit
code is 1 with an `unsupported-feature` document and message and no output.

**CanvasNetTool_OutputFormats_ExtensionAndFormatOption_SelectFormat**: the six extensions select their formats in the
JSON result, and `--format bmp` on an output named `.png` writes a BMP.

**CanvasNetTool_Defaults_ExistingOutputAndSameFile_FollowPolicy**: `--no-clobber` refuses an existing output with
`output-exists` and keeps it; by default it is overwritten; the input as the output is refused with
`same-input-output`.

**CanvasNetTool_Defaults_Transparency_FlattenedOverWhiteForSvgAndJpeg**: an SVG renders over white; a PNG with a
transparent edge keeps its transparency as a PNG and is white there as a JPEG.

**CanvasNetTool_Defaults_DocumentBackground_WhiteUnlessTransparent**: a page the document does not paint is white
by default, transparent in a PNG with `--background transparent`, and white again in a JPEG, for each document.

**CanvasNetTool_Defaults_JpegWithExif_IsTurnedUpright**: a JPEG with the EXIF orientation 8 is written upright (8x12)
by default, as stored (12x8) with `--no-auto-orient`, and the upright pixels are the stored pixels turned.

**CanvasNetTool_Defaults_OversizedInput_IsRefusedBeforeDecoding**: a PNG declaring 9000x9000 pixels is refused with
exit code 1, a `size-limit` document and message, and no output.

**CanvasNetTool_Pipeline_MultiOpRender_AppliesOperationsInOrder**: the gradient PNG is rendered as a real process with
`--crop 2,1,8,6 --rotate 90 --flip h --fit 3x3 --pad 1 --pad-color #ff0000 --json`; the document lists the five
operations in that order with the sizes after the rotation (6x8) and the fit (2x3), and the file is 4x5 with the red
border at its corners.

**CanvasNetTool_TransformCommands_EachVerb_MatchesRenderEquivalent**: `crop`, `resize`, `rotate`, `flip`, and `pad` are
run as real processes, each with its operand after the input; each writes one result line and the same bytes as the
equivalent `render` invocation.

**CanvasNetTool_TransformCommands_Help_ListsVerbsAndTheirUsage**: the top-level help lists the five verbs and
`crop --help` shows its usage line with the operand and the options of `render`.

**CanvasNetTool_Pipeline_UnsupportedOrImpossibleOperations_AreReportedAsExpectedErrors**: an angle that is not a number,
a grid spacing of 3, levels with equal black and white points, a crop that starts outside the image, a border of 9000
pixels, and a size of 10000x10 are run in-process; each returns exit code 1 with `usage-error` (the value and the option
are named in the message, the crop names the image size) or `size-limit` in the message and in the JSON document, without
a stack trace and with no output.

**CanvasNetTool_Determinism_SameInputTwice_WritesIdenticalBytes**: a PNG, a JPEG, an SVG, a PDF, a presentation, a
drawing, and a model are rendered twice to every output format at a given width; the two files are byte-identical.

**CanvasNetTool_InputErrors_BadInputs_AreReportedAsExpectedErrors**: a missing file, a text file, and a truncated file
and garbage of every supported format (a model is only given garbage, because a truncated model is usually still a
valid one, and its code is `scad-error`; a truncated or garbage chart has the code `vega-lite-error`) are rendered in-process; each returns exit code 1 (an unexpected exception
would escape and fail the test) with the stable error code in the message and in the JSON document, and no output
exists.

**CanvasNetTool_InputErrors_DocumentMistakes_AreReportedAsExpectedErrors**: a page that does not exist, a Word-like
archive named `.pptx`, a presentation and a drawing with a broken part, a PDF with an unsupported encryption handler,
a presentation with a shape that does not exist, and `--page` with `--all-pages` are rendered in-process; each returns
exit code 1 with its stable error code in the message and in the JSON document, and no output exists.

**CanvasNetTool_Scad_RenderAsProcess_WritesImageAndReportsOptions**: the model fixture is rendered as a real process
from the top view with `-D width=30` at 120x120 with `--json`; exit code 0, the document names the view `top`, the
definition `width=30`, and the `scad` format, and the image has the block with its hole (white in the hole, covered in
the block beside it).

**CanvasNetTool_Scad_NamedViews_WriteDistinctDeterministicImages**: a model that looks different from every side is
rendered from each of the seven named views twice in-process; the two files of a view are byte-identical, and no two
views give the same file.

**CanvasNetTool_Scad_OutputFormats_WriteDecodableFiles**: the model fixture is rendered to PNG, JPEG, BMP, and TIFF
in-process; each succeeds with a result line and nothing on standard error, and the file decodes to the size asked for
with the model in the middle.

**CanvasNetTool_Scad_Info_DescribesModel**: `info` on the model fixture as a real process, as text and as JSON; the
text line gives one module, one function, and no directives and lists them; the document has a `model` object with the
module `block`, the function `half`, and no directives.

**CanvasNetTool_Scad_Diagnostics_AreReportedInJson**: a model that echoes, calls a module that does not exist, and uses
an unsupported module renders with exit code 0; the JSON result has the echo line and, in order, the `scad-warning` and
`scad-module-surface` diagnostics.

**CanvasNetTool_Scad_Includes_AreReadFromTheModelFolderOnly**: a model that includes and uses files in a subfolder
renders without notes; a model that includes a file that does not exist and a model that includes a file outside its
folder (which exists) each return exit code 1 with a `scad-error` message and document and write no output.

**CanvasNetTool_Scad_ModelMistakes_AreReportedAsExpectedErrors**: a syntax error, a missing include, a failed `assert`,
garbage, a triangle limit of 1, runaway recursion, an unsupported feature with `--strict`, an unknown view, an elevation
of 100, a definition without `=`, and a triangle limit of 0 are rendered in-process; each returns exit code 1 with a
one-line message that starts with its stable error code (`scad-error`, `budget-exceeded`, `unsupported-feature`, or
`usage-error`), no stack trace, the code in the JSON document, and no output.

**CanvasNetTool_VegaLite_RenderAsProcess_DescribesAndWritesImage**: the two-bar chart is described with `info --json` and
rendered to a PNG with `render --json`, each as a real process (so the Vega-Lite assembly bundled next to the tool is
loaded); exit code 0, `info` names the `vega-lite` format and the `bar` mark, `render` names the format and the `render`
operation, and the image has the size `info` reported, a white corner, and the default mark color `#4c78a8` at a point of
the second bar computed from the layout (5 pixels of padding at the right, 50 pixels from the right edge of the data
rectangle, 15 pixels from the top).

**CanvasNetTool_VegaLite_DetectionDiagnosticsAndErrors_FollowTheContract**: in-process, a chart saved as `.json` is
described as `vega-lite` (found by its `$schema`); a chart with an unsupported mark renders with exit code 0 and the
`vl-mark-arc` diagnostic in the JSON result, and with `--strict` returns exit code 1 with an `unsupported-feature` message
and writes no output; a malformed chart returns exit code 1 with a `vega-lite-error` message and document and writes no
output; and a plain Vega specification saved as `.json` returns exit code 1 with an `unsupported-format` message.

**CanvasNetTool_Compare_IdenticalImages_ReportsZeroDifference**: two identical gray PNGs are compared as real
processes, as text with `--threshold rmse=0` (one `identical` line and the passed check) and as JSON (`identical`
true, RMSE 0, SSIM 1, PSNR null); exit code 0.

**CanvasNetTool_Compare_DifferentImages_ReportsMetricsAndGridAsJson**: two gray PNGs whose blue channels differ by 10
are compared with `--grid 2x2 --json` as a real process; one document with `ok` true, MAE 3.3333, RMSE 5.7735, a
largest channel difference of 10, 48 differing pixels, four grid cells, and three worst cells.

**CanvasNetTool_Compare_VectorAgainstReference_RendersVectorAtReferenceSize**: the SVG fixture is compared with its own
200x160 rendering with `--resize-to a`; the SVG side is loaded at 200x160 (not resampled) and the images are identical.

**CanvasNetTool_Compare_ImageOutputs_WriteDiffSideBySideAndOverlay**: `--diff`, `--side-by-side`, and `--overlay` (a
JPEG) are written together; each is listed in the output, decodes, and has the documented size (8x6, 40x6, and 8x6),
and a difference of 10 at the default gain is the heat color (120, 0, 0).

**CanvasNetTool_Compare_ThresholdMet_ExitsZero**: `--threshold rmse=10` and `ssim=0.99` are met; both checks are
printed as passed and the exit code is 0.

**CanvasNetTool_Compare_ThresholdNotMet_ExitsOneAfterWritingResultAndImages**: `--threshold rmse=1` is not met as a
real process; exit code 1, the result lines and the failed check are printed, the `threshold-failed` error and its hint
follow without a stack trace, and the `--diff` picture was written.

**CanvasNetTool_Compare_ThresholdNotMetWithJson_WritesFailureDocumentWithMetrics**: two unmet thresholds with `--json`
and `--overlay` in-process; exit code 1, the error on standard error, and on standard output one document with `ok`
false, the `threshold-failed` code, the metrics, both failed checks, and the overlay output, which exists.

**CanvasNetTool_Compare_Errors_AreReportedAsExpectedErrors**: images of two sizes without `--resize-to`, a threshold
without a value, an unknown threshold metric, an output that is an input, a missing input, and one file for two
outputs are run in-process; each returns exit code 1 with its stable code (`size-mismatch`, `usage-error`,
`same-input-output`, `input-not-found`) in the message and in the document, no stack trace, and no output.

**CanvasNetTool_Compare_SameInputsTwice_WriteIdenticalBytes**: the same comparison with all three pictures and `--json`
is run twice as a real process; the output text and the bytes of every picture are identical.

**CanvasNetTool_Montage_Images_WritesContactSheet**: a red and a blue PNG are composed as a real process; the one line
names the sheet (536x272), and each cell holds its image fitted and centered.

**CanvasNetTool_Montage_DocumentAllPagesWithLabels_WritesCellPerPage**: the two-slide presentation is composed with
`--all-pages --labels`; the JSON lists two cells labeled `source.pptx p1` and `source.pptx p2` with pages 1 and 2, a
label strip of 22 pixels, and a sheet of the size the file has.

**CanvasNetTool_Montage_Errors_AreReportedAsExpectedErrors**: an output that is an input, a corrupt input, a missing
input, a cell that is too small, a sheet that is too large (refused before the corrupt input is opened), and a missing
output are run in-process; each returns exit code 1 with its stable code in the message and in the document, and no
sheet is written.

**CanvasNetTool_Montage_SameInputsTwice_WriteIdenticalBytes**: the same labeled montage is run twice as a real process;
the output text and the bytes of the sheet are identical.

**CanvasNetTool_Help_ListsCompareAndMontage**: the top-level help lists both commands and `compare --help` and
`montage --help` show their usage lines.

**CanvasNetTool_Photo_GrayscaleThresholdLevels_WriteExpectedPixels**: a row of six pixels (red, green, blue, and the
grays 200, 100, and 50) is rendered as real processes with `--grayscale` (the luma 77, 149, 29, and the grays
unchanged), `--threshold 100` (white only for the luma above 100), `--levels 50,150` (the grays become 255, 128, 0), and
`--brightness 20` (220, 120, 70).

**CanvasNetTool_Photo_AutoLevels_StretchesContrastAndReportsPoints**: a sketch with paper at 200 and ink at 100 is
rendered with `--auto-levels --json`; the operation reports `applied`, black 100, and white 200, and the ink is black
and the paper white in the file.

**CanvasNetTool_Photo_RotateObliqueAngle_ExpandsCanvasAndFillsCorners**: a 160x100 sketch is rotated by 30 degrees with
`--rotate-fill #ff0000` and with the default; the operation reports 30 degrees and 189x167, the corners are red (and
white by default for an opaque image), and the center is still paper.

**CanvasNetTool_Photo_RotateVerbWithFill_MatchesRenderEquivalent**: `rotate <input> 30` and `rotate <input> -12.5` with
`--rotate-fill #00ff00` are run as real processes; each writes one result line and the same bytes as `render --rotate`.

**CanvasNetTool_Photo_Deskew_StraightensTiltedLinesAndReportsAngleInJson**: a sketch with six lines tilted 4 degrees is
rendered with `--deskew --rotate-fill #C8C8C8 --json`; the operation is applied with a skew between 3.5 and 4.5, the
opposite correction, and a confidence of at least 0.4; deskewing the result finds a remaining skew of at most 0.5.

**RenderCommand_Run_DeskewTooLarge_SucceedsWithReasonAndWarning**: an 8192x400 image with a two-degree skew is
rendered with `--deskew`; the command succeeds, the output remains 8192x400, JSON reports `applied:false` with the
size-limit reason, and normal output includes a non-fatal warning.

**CanvasNetTool_Photo_Trim_CropsUniformBorder**: a 100x80 block on a white image with a near-white ring around it is
trimmed with the default tolerance (the box 30,20 100x80, the file 100x80) and with `--trim-tolerance 2` (the ring is kept,
102x82).

**CanvasNetTool_Photo_Grid_DrawsLabeledLinesAtSpacing**: a 160x100 sketch gets `--grid 20,#FF0000`; the operation reports
8 vertical and 5 horizontal lines and labels, the pixels on a vertical and a horizontal line are red, a pixel between the
lines is unchanged, the label box at the origin changes the pixels under it, and the input file is not modified.

**CanvasNetTool_Inspect_Colors_ReportsDominantColorsAndLumaAsJson**: `inspect colors --json` on 70 red and 30 blue pixels
as a real process; one document with the colors `#FF0000` (70 pixels, 0.7) and `#0000FF` (30), the luma minimum 29,
maximum 77, mean 62.6, a histogram of 16 counts, one color with `--count 1`, and no file written.

**CanvasNetTool_Inspect_Pixel_ReportsValueAndNeighborhood**: `inspect pixel` on the gradient at `3,2 --radius 1` as a
real process; the JSON document has the point, the color and hex of the pixel, and the clipped 3x3 window at 2,1; the
text line starts `pixel 3,2 of`; after `--crop 2,1,4,4` the point `1,1` is the same pixel.

**CanvasNetTool_Inspect_Errors_AreReportedAsExpectedErrors**: a missing subcommand, a misspelled subcommand, an option of
the other subcommand, a point outside the image, a point that is not numbers, `--count` for `pixel`, and a missing input
are run in-process; each returns exit code 1 with `usage-error` (or `input-not-found`) in the message and in the document
and no stack trace.

**CanvasNetTool_Help_ListsInspectAndPhotoOptions**: the top-level help lists `inspect`, `inspect --help` shows both usage
lines, and `render --help` lists the photo options.

**CanvasNetTool_Photo_NapkinWorkflow_ChainedOperationsApplyInOrderAndReport**: the workflow of the user guide on a 400x300
tilted, low-contrast sketch: `--fit 300x300 --auto-levels --deskew --trim` reports the four operations in order (300x225
after the fit, both corrections applied, a skew between 2.4 and 3.6, a trim box that matches the file); `--grid 50` on the
result reports the final size; and `inspect colors --auto-levels` reports `#FFFFFF` as the dominant color.

**CanvasNetTool_Photo_SameInputsTwice_WriteIdenticalBytes**: three option sets that use the photo operations (including
Otsu, deskew, an oblique rotation, gamma, and the grid) are rendered twice in-process; the two files of each are
byte-identical.
