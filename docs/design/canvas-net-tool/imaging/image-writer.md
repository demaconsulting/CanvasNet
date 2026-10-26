### ImageWriter

<!-- cspell:ignore canvasnet jpeg tiff tif jpg -->

![Imaging Structure](ImagingView.svg)

#### Purpose

`ImageWriter` writes pixels to a file. Its single responsibility is the output side of the tool: choose the format,
encode, and put the file in place safely. The unit consists of the static class `ImageWriter` and the supporting
records `WriteOptions` and `WriteResult`.

#### Data Model

**WriteOptions** (record): `Format` (an `OutputFormat?`; `null` means from the extension), `Quality` (JPEG, 1 to
100), `NoClobber`, and `FlattenBackground` (the color a JPEG is flattened over). `WriteOptions.Default` is format
from the extension, quality 90, overwrite, and white.

**WriteResult** (record): `Path` (as given), `Format`, `Width`, `Height`, `Bytes` (the size of the file written),
and `Flattened` (transparency was flattened for JPEG).

**ExtensionFormats**: the output format of each extension (`.png`, `.jpg`, `.jpeg`, `.bmp`, `.tif`, `.tiff`,
case-insensitive); the `formats` command lists the same extensions and a test keeps the two consistent. The
`--format` option accepts `png`, `jpeg`, `jpg`, `bmp`, `tiff`, and `tif`. The constants `MinQuality` (1),
`MaxQuality` (100), and `DefaultQuality` (90) are the quality range.

#### Key Methods

**ResolveFormat(outputPath, formatOption)**: the option wins when given (an unknown name is a `usage-error`);
otherwise the extension decides (a missing or unknown extension is a `usage-error` that says how to choose).

**CheckTarget(outputPath, inputPath, noClobber)**: throws `same-input-output` when the two paths resolve to the same
full path (compared ignoring case on Windows and macOS, whose file systems are case-insensitive by default - which
can only refuse more, never less), `output-write-failed` when the output is an existing directory, and
`output-exists` when it exists and `noClobber` is set. Commands call it before loading the input so that an output
mistake is reported before any work is done; `Write` calls it again.

**Write(surface, outputPath, inputPathForGuard, options)**: validates the arguments (a quality outside 1 to 100 is
a defect of the caller and throws `ArgumentOutOfRangeException`), resolves the format, checks the target, flattens
the surface over `FlattenBackground` when the format is JPEG and any pixel is not opaque (using
`SurfaceOperations`), and writes through `WriteAtomically`: it creates the target directory when needed, encodes
into a temporary file in the target directory (named from the target and a new GUID, created with `CreateNew`),
and moves it over the target (overwriting unless `NoClobber`). The temporary file is deleted in a `finally`
block, so a failure leaves nothing behind. The encoders are PNG (RGBA), JPEG (at the quality), BMP (32 bits per
pixel, with alpha), and TIFF (uncompressed, with alpha); all are deterministic, so the same pixels and options
give the same bytes. The surface passed in is never modified or disposed.

#### Error Handling

`ToolException` with `usage-error` (format), `same-input-output`, `output-exists`, and `output-write-failed` (an
`IOException`, `UnauthorizedAccessException`, or `NotSupportedException` while creating the directory, writing the
file, or moving it; the cause is attached and the message names the output). An `IOException` after a successful
check while `NoClobber` is set and the target now exists (another process created it) is reported as
`output-exists`. Null arguments throw `ArgumentNullException`. Any other exception is a defect and propagates.

#### Dependencies

- **ToolException** and **ErrorCodes** - the expected-error contract.
- **SourceLoader** (the `SurfaceOperations` helper and the format names) - flattening and `ToFormatName`.
- **CanvasNet core library** - `Surface`, `Rgba32`, and the PNG, JPEG, BMP, and TIFF codecs.

#### Callers

- **RenderCommand** - checks the target, then writes the result.
