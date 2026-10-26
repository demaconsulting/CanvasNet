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

**CheckTarget(outputPath, inputPath, noClobber)**: throws `same-input-output` when the two paths are identical after
full-path normalization and symbolic link or junction resolution: the same full path, or links in either path (to a
file, or to any folder on the way, including a link whose target itself
goes through a link, as `/var` does on macOS) lead to the same file (compared ignoring case on Windows and macOS,
whose file systems are case-insensitive by default - which can only refuse more, never less). It compares path names,
not file identity, so a hard link to the input is not detected; that is safe because the input is read and decoded
completely before any output is written, and an output is written to a new temporary file that is moved over the target
name, which replaces that directory entry instead of writing into the existing file - a hard-linked output name gets a
new file and the input keeps its content. Platform file-identity checks would need operating-system-specific APIs and
add no data safety. `CheckTarget` also throws `output-write-failed` when the output is an existing directory, is not a
valid path (empty, a null character, too
long, a format the system does not support), or has a file where one of its folders should be (a folder that does
not exist is fine: `Write` creates it), and `output-exists` when it exists and `noClobber` is set. Commands call it
before loading the input so that an output mistake is reported before any work is done; `Write` calls it again.

**CheckTargets(outputPaths, inputPaths, noClobber)**: the check of a command with several outputs and inputs, without
writing anything. For each output, in order: the path must be usable (`output-write-failed`), must not be the same
file as any input (`same-input-output`, by the same `IsSameFile` comparison as `CheckTarget`), must not be an existing
directory (`output-write-failed`) and, with `noClobber`, must not exist (`output-exists`); then it must not be the same
file as an output before it (`usage-error`: two outputs cannot share a file). Null arguments or elements throw
`ArgumentNullException`. `CheckTarget` and `CheckTargets` share the private helpers that make these checks.

**Write(surface, outputPath, inputPathForGuard, options)**: stages the output with `Stage` and commits it at once
(`Stage(...)` then `Commit()`), so a single output is written atomically.

**Stage(surface, outputPath, inputPathForGuard, options)**: validates the arguments (a quality outside 1 to 100 is
a defect of the caller and throws `ArgumentOutOfRangeException`), resolves the format, checks the target, flattens
the surface over `FlattenBackground` when the format is JPEG and any pixel is not opaque (using
`SurfaceOperations`), creates the target directory when needed, and encodes into a temporary file in the target
directory (a short fixed name with a new GUID, so a long target name cannot make the temporary name too long;
created with `CreateNew`). It returns a `StagedOutput` that holds the temporary file and the `WriteResult` (with
the size of the encoded file). A failure while staging deletes the temporary file and is reported as
`output-write-failed`.

**StagedOutput.Commit()**: moves the temporary file over the target (overwriting unless `NoClobber`) and returns
the `WriteResult`. **StagedOutput.Dispose()**: deletes the temporary file unless it was committed, so a staged
output that is abandoned (because a later output failed) leaves nothing behind. A command that writes several
outputs stages all of them first and commits only when every one was staged, so a failure while rendering or encoding
leaves no new or partial output and every existing output untouched; the commits are separate moves, so if one itself
fails the outputs committed before it remain, each complete (no rollback). The encoders are PNG (RGBA), JPEG (at the
quality), BMP (32 bits per
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

- **CompareCommand** and **MontageCommand** - check all their targets once with `CheckTargets` before loading anything.
- **RenderCommand** - checks every target, stages each page's result, and commits them all once every page has succeeded.
