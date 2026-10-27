## Validation

<!-- cspell:ignore Postconditions scad openscad rmse ssim deskew -->

![CanvasNetTool Structure](CanvasNetToolView.svg)

### Purpose

`Validation` orchestrates the self-validation test suite behind `--validate`. Its single responsibility is to
run a fixed set of internal tests that exercise the tool's own functionality, print a summary to the provided
`Context`, and optionally serialize the results to a TRX or JUnit XML file. It does not define the tool's
requirements; it produces the qualification evidence that the tool behaves correctly in the deployment
environment, and the per-platform and per-runtime evidence that the platform requirements rely on.

### Data Model

`Validation` holds no instance state. The class is `internal static`; all state is local to `Run` and the
private test methods. The names of the self-tests are a contract with the platform requirements and are never
changed: `CanvasNetTool_VersionDisplay` and `CanvasNetTool_HelpDisplay` (the platform-evidence tests) and
`CanvasNetTool_Formats`, and the imaging self-tests `CanvasNetTool_RenderPng`, `CanvasNetTool_ConvertFormats`,
`CanvasNetTool_Info`, `CanvasNetTool_RenderSvg`, `CanvasNetTool_PdfMinimal`, `CanvasNetTool_RenderScad`,
`CanvasNetTool_CropResize`, and `CanvasNetTool_RotateFlipPad`, and the comparison self-tests
`CanvasNetTool_Compare` and `CanvasNetTool_Montage`, and the photo self-test `CanvasNetTool_Adjustments` (fourteen in
total; the suite is deliberately kept small, so further coverage belongs in the unit and subsystem tests).
Each is
recorded as a `TestResult` with
`ClassName` "Validation" and `CodeBase` "CanvasNetTool".

### Key Methods

**Run**: Entry point for the self-validation suite.

- *Parameters*: `Context context` - the output channel and results configuration.
- *Preconditions*: `context` is not null.
- *Postconditions*: a summary has been printed; if `context.ResultsFile` was set, a results file has been
  written or an error has been recorded for an unsupported extension.

Prints the header (a markdown heading at `context.HeadingDepth` and a table of tool version, machine name, OS,
runtime, and time stamp), constructs a `TestResults` object named "CanvasNet Tool Self-Validation", runs the
self-tests, prints the totals (using `WriteError` for the failed count when any test failed), and calls
`WriteResultsFile` if `context.ResultsFile` is set.

**RunVersionTest** and **RunHelpTest**: each creates a `TemporaryDirectory`, builds a log path with
`PathHelpers.SafePathCombine`, runs the tool in-process (`Context.Create` and `Program.Run`) with
`--silent --log {path} --version` or `--help`, reads the log, and checks it: the version log must contain a
dotted version number; the help log must contain both "Usage:" and "Options:". Any exception is recorded as a
test failure by `HandleTestException`.

**RunFormatsTest**: the same pattern for the `formats` command: it runs the tool in-process with
`--silent --log {path} formats --json`, then `FormatsDocumentIsValid` parses the log as JSON and checks the schema
version, the success flag, the command name, and that `png` is listed as an input and as an output format. This
exercises command dispatch, `ArgumentReader`, and `ResultWriter` together in the deployment environment.

**RunRenderPngTest**, **RunConvertFormatsTest**, **RunInfoTest**, **RunRenderSvgTest**, **RunPdfMinimalTest**,
**RunRenderScadTest**, **RunCropResizeTest**, and **RunRotateFlipPadTest**:
the imaging self-tests. Each runs through `RunSelfTest`, which creates a `TemporaryDirectory`, calls the test body
with the directory (the body returns `null` for a pass or a description of the failure), records the outcome, and
records an exception as a failure. The bodies generate their own inputs - a small opaque gradient PNG written with
`PngCodec`, a small SVG text file, a hand-written one-page PDF (72x48 points with a red rectangle over its left
half, built with a correct cross-reference table by `CreateMinimalPdf`), and a three-line OpenSCAD model (a variable,
a module that makes a block, and a call) - and run the `render`, `convert`, and
`info` commands in-process through
`RunCommand` (`--silent --log {path} {command ...}`, returning the log text and throwing when the exit code is not
0). `CanvasNetTool_RenderPng` renders the PNG to a PNG and requires every pixel unchanged and a PNG output in the
JSON result; `CanvasNetTool_ConvertFormats` converts it to BMP, TIFF, JPEG, and PNG through the `convert` alias
and requires the right size from each codec and every pixel of the lossless outputs; `CanvasNetTool_Info`
requires the format and size in the JSON document; `CanvasNetTool_RenderSvg` renders an 8x8 SVG with a red square
at width 16 and requires a 16x16 image with the red square and white (flattened) background;
`CanvasNetTool_PdfMinimal` describes the PDF with `info` (one page of 96x64 pixels), renders it at the default
resolution (a 96x64 image with the red half and a white rest) and at `--dpi 192` (192x128);
`CanvasNetTool_RenderScad` describes the model with `info` (one module, no function), renders the front view at
40x40 (the block fills the middle and the top of the picture, the corner is white), then renders it again with
`-D w=40` and requires that the longer block no longer reaches the top; `CanvasNetTool_CropResize` crops the 8x6
gradient PNG to the 4x3 rectangle at 2,1 with the `crop` command and with `render --crop` (requiring the right pixels
and byte-identical files), resizes it with `resize` to 4x3, and runs `render --crop --resize` with `--json`, requiring
the operations `crop` then `resize` and an 8x6 result; `CanvasNetTool_RotateFlipPad` rotates the gradient by 90
degrees (a 6x8 image whose right column is the original top row), flips it horizontally and vertically, and pads it
with `2,1,0,3` in red, requiring the exact pixels and sizes. These exercise the
codecs, the SVG, PDF, and SCAD renderers, the source loader, the image pipeline, the image writer, and the commands
together.

**RunCompareTest** and **RunMontageTest**: the comparison self-tests, run
through `RunSelfTest` and `RunCommand` like the imaging self-tests, on the 8x6 gradient PNG that the other self-tests
generate. `CanvasNetTool_Compare` compares the PNG with an identical copy using `--json` and two thresholds
that only identical images meet (`rmse=0`, `ssim=1`), requiring success, `identical`, an MAE of 0, a similarity of 1, a
null PSNR, no differing pixel, and passing thresholds. It then compares the PNG with a copy whose
4x3 region at 2,1 has a blue channel 50 higher, with `--diff`, `--side-by-side`, and a threshold that is not met
(`ssim=1`). That run goes through `Program.Main` directly, because `RunCommand` throws on a non-zero exit code and
`Program.Run` lets the expected error escape. It requires exit code 1, a failure document with `ok` false, the
`threshold-failed` code, and the metrics (a largest channel difference of 50, 12 differing pixels, the exact MAE, a
similarity below 1), a difference picture that is black at (0,0) and not at (3,2), and a side-by-side picture that is
40x6. `CanvasNetTool_Montage`
composes two copies of the PNG into 32x32 cells in two columns with labels and requires an 88x70 sheet in the result
and in the file, white padding at the corner, the image fitted and centered in the first cell, and text pixels in its
label strip. These exercise the comparison, the
contact sheet with its bundled-font labels, the picture creators, and the commands together.

**RunAdjustmentsTest**: the photo self-test, one smoke test for every photo operation (the 14th and last self-test).
It generates a 160x100 PNG sketch with `CreateSketchSurface` (light-gray paper `#C8C8C8` with four dark two-pixel lines
tilted 3 degrees clockwise, drawn with plain arithmetic, so that the pixel at 80,19 is ink and the pixel at 0,0 is
paper) and runs the checks of four helpers in turn, stopping at the first failure. `CheckToneOperations` runs
`render --grayscale --threshold auto --json` and requires an automatic level from 1 to 254, only black and white pixels,
black ink and white paper, then `render --auto-levels` and requires the ink at most 5 and the paper at least 250.
`CheckDeskewAndRotate` runs `render --deskew --json` and requires `applied` and a reported skew within 0.7 degrees of 3,
then `rotate 30 --rotate-fill #ff0000` and requires a 189x167 image (the canvas that holds a 160x100 image turned 30
degrees) with a red corner. `CheckTrimAndGrid` pads the sketch with a white border of 10 pixels and trims it, requiring
the `trim` operation to report the 160x100 content box, then renders `--grid 20` and requires that the pixel at 20,50
differs from the sketch and that the input file still equals the generated sketch. `CheckInspect` runs `inspect colors`
and `inspect pixel 0,0` with `--json` and requires that the first dominant color and the pixel are both `#C8C8C8`.
This exercises the tone operations, the skew estimation, the arbitrary-angle rotation, the trim, the grid with its
bundled-font labels, the analysis, and the commands together.

**WriteResultsFile**: serializes the results to `context.ResultsFile`: `.trx` uses `TrxSerializer`, `.xml`
uses `JUnitSerializer`; any other extension calls `context.WriteError` and writes nothing.

**TemporaryDirectory** (nested class): creates a uniquely named directory under the system temporary
directory and deletes it on `Dispose`, ignoring cleanup errors.

### Error Handling

`Run` throws `ArgumentNullException` if `context` is null. Each self-test wraps its body in a broad
`catch (Exception)` - justified because this is a test framework in which any failure, including an unexpected
one, must be recorded as a failed test so the remaining tests still run and the evidence is complete.
`WriteResultsFile` catches file-write exceptions and reports them through `context.WriteError`. An unsupported
results extension is a user error: `WriteError` is called ("Unsupported results file format") and the exit code
becomes 1.

### Dependencies

- **Context** - output channel and results configuration.
- **Program** - `Program.Run` is called in-process to exercise the tool.
- **PathHelpers** - `SafePathCombine` builds log paths inside temporary directories.
- **DemaConsulting.TestResults** (OTS) - `TestResults`, `TestResult`, `TestOutcome`, and the
  `TrxSerializer` and `JUnitSerializer` of `TestResults.IO`. See *DemaConsulting.TestResults OTS Integration
  Design* (`ots/test-results.md`).

### Callers

- **Program** - calls `Validation.Run(context)` when the `--validate` flag is set.
