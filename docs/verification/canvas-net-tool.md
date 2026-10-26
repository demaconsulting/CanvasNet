# System Verification Design

<!-- cspell:ignore canvasnet dpi pptx vsdx scad openscad -->

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
`CanvasNetTool_Info`, `CanvasNetTool_RenderSvg`, `CanvasNetTool_PdfMinimal`, and `CanvasNetTool_RenderScad`), which
generate their own inputs and
are evidence for the
validation requirements.
The same job guards the package contents (no dependency on sibling CanvasNet packages, library assemblies
bundled for every target framework).

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
  JPEG 2000, SVG, and the pages of PDF, PPTX, and VSDX documents, and OpenSCAD models) with the documented defaults
  (overwrite, flatten
  over white, EXIF orientation, 96 dpi, 800x800 for a model, 8192-pixel limit), the same bytes for the same input, and
  a stable error for
  every bad input; a document page, or every page, is chosen with `--page` and `--all-pages`, and a model view with
  `--view`, `--azimuth`, `--elevation`, `--projection`, `-D`, `--time`, `--default-color`, and `--max-triangles`;
  `info` describes a
  file, the pages of a document, and the declarations of a model, without rendering it.

## Test Scenarios

**CanvasNetTool_VersionFlag_Provided_OutputsVersion**: `--version` is the sole argument; the output is a
version number with no banner, copyright, or error text, and the exit code is 0.

**CanvasNetTool_HelpFlag_Provided_OutputsUsageInformation**: `--help` is the sole argument; the output contains
"Usage:", "Options:", `--version`, and `--help`, and the exit code is 0.

**CanvasNetTool_NoArguments_Invoked_DisplaysBanner**: the tool is invoked with no arguments; the output
contains the banner, the copyright notice, and the usage hint, and the exit code is 0.

**CanvasNetTool_ValidateFlag_Provided_RunsValidation**: `--validate` is the sole argument; the output contains
the "Total Tests:" and "Passed:" summary and the exit code is 0.

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

**CanvasNetTool_Determinism_SameInputTwice_WritesIdenticalBytes**: a PNG, a JPEG, an SVG, a PDF, a presentation, a
drawing, and a model are rendered twice to every output format at a given width; the two files are byte-identical.

**CanvasNetTool_InputErrors_BadInputs_AreReportedAsExpectedErrors**: a missing file, a text file, and a truncated file
and garbage of every supported format (a model is only given garbage, because a truncated model is usually still a
valid one, and its code is `scad-error`) are rendered in-process; each returns exit code 1 (an unexpected exception
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
`scad-module-text` diagnostics.

**CanvasNetTool_Scad_Includes_AreReadFromTheModelFolderOnly**: a model that includes and uses files in a subfolder
renders without notes; a model that includes a file that does not exist and a model that includes a file outside its
folder (which exists) each return exit code 1 with a `scad-error` message and document and write no output.

**CanvasNetTool_Scad_ModelMistakes_AreReportedAsExpectedErrors**: a syntax error, a missing include, a failed `assert`,
garbage, a triangle limit of 1, runaway recursion, an unsupported feature with `--strict`, an unknown view, an elevation
of 100, a definition without `=`, and a triangle limit of 0 are rendered in-process; each returns exit code 1 with a
one-line message that starts with its stable error code (`scad-error`, `budget-exceeded`, `unsupported-feature`, or
`usage-error`), no stack trace, the code in the JSON document, and no output.
