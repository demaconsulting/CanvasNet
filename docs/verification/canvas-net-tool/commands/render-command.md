### RenderCommand

<!-- cspell:ignore canvasnet jpeg jpg tiff tif exif -->

#### Verification Approach

`RenderCommand` is verified with unit tests in `RenderCommandTests.cs`. The command is run in-process: the tests
create a `Context` (with `--json` when the JSON document is wanted), call `Run`, and capture the console with the
`ToolHost` helper; failures are asserted as the `ToolException` the command throws (the registry would record the
command name on it). Inputs are generated in a temporary directory (rasters with the codecs, an EXIF segment injected
into a JPEG, a PNG that declares an oversized image) plus the original SVG fixture, and the outputs are decoded with
the codecs and compared with the generated pixels.

#### Test Environment

N/A - standard test environment. The tests belong to the "Sequential" collection because they redirect the console.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The command is registered as `render` and `convert` with the documented options.
- The text line and the JSON document have the documented content.
- Sizes, background, quality, format, orientation, and overwrite options take effect; contradictory or malformed
  options are usage errors and write nothing.
- An output problem is reported before the input is decoded; a bad input is an expected error and leaves no output.

#### Test Scenarios

**RenderCommand_Metadata_DescribesCommandAndOptions** and **RenderCommand_DefaultRegistry_RegistersRenderAndConvert**:
the name, the alias, the summary, the usage line with examples, the ten options in order (the short alias `-o`), every
option with a description and a value name exactly when it takes a value; the default registry resolves both names to
the one command.

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
**RenderCommand_Run_CommandLineMistakes_ThrowUsageError**, and **RenderCommand_Run_NullArguments_ThrowsArgumentNullException**:
a missing file, a truncated PNG, a text file, and a directory are `input-not-found`, `corrupt-input`,
`unsupported-format`, and `input-unreadable` with no output; a missing output, a missing or second input, an unknown
option with a suggestion, a flag with a value, and a missing value are usage errors; null arguments throw.
