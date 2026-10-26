### RenderCommand

<!-- cspell:ignore canvasnet jpeg jpg tiff tif exif pptx vsdx dpi -->

#### Verification Approach

`RenderCommand` is verified with unit tests in `RenderCommandTests.cs` and `RenderCommandDocumentTests.cs` (pages,
resolution, and strict mode of the documents). The command is run in-process: the tests
create a `Context` (with `--json` when the JSON document is wanted), call `Run`, and capture the console with the
`ToolHost` helper; failures are asserted as the `ToolException` the command throws (the registry would record the
command name on it). Inputs are generated in a temporary directory (rasters with the codecs, an EXIF segment injected
into a JPEG, a PNG that declares an oversized image, and the PDF, presentation, and drawing the test project writes)
plus the original SVG fixture, and the outputs are decoded with the codecs and compared with the generated pixels.

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

#### Test Scenarios

**RenderCommand_Metadata_DescribesCommandAndOptions** and **RenderCommand_DefaultRegistry_RegistersRenderAndConvert**:
the name, the alias, the summary, the usage line with examples, the fourteen options in order (the short alias `-o`),
every option with a description and a value name exactly when it takes a value; the default registry resolves both
names to the one command.

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
