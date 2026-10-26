### DiscoveryCommands

<!-- cspell:ignore canvasnet jpeg tiff pptx vsdx scad Visio -->

![Commands Structure](CommandsView.svg)

#### Purpose

`DiscoveryCommands` holds the read-only commands that tell a caller what the tool can do or what a file is. Its
single responsibility is to answer "what do you support?" and "what is this file?" without writing any image. It
provides two commands, `formats` and `info`, implemented as the nested classes `DiscoveryCommands.Formats` and
`DiscoveryCommands.Info`. The table of formats is data owned by this unit.

#### Data Model

**InputFormats** (`IReadOnlyList<InputFormat>`): one row per input format, in listing order - `png`, `jpeg`, `bmp`,
`tiff`, `gif`, `jpeg2000`, `svg`, `pdf`, `pptx`, `vsdx`, `scad`. A row (`InputFormat`) has `Name`, `Kind`
(`raster`: decoded at its own size; `vector`: a drawing rendered at any requested size; `document`: pages or slides
rendered at any requested size; `model`: a 3D model rendered from a viewpoint), `Extensions` (lower case, with the
dot), `MultiPage` (one file holds pages or slides that can be chosen), and `Vector` (rendered at the requested size
rather than decoded at its own size). The rows are the `SourceKind` members in order, and their extensions are those
that `SourceLoader.ExtensionKinds` maps to the kind; a test keeps the table consistent with the enumeration and the
loader, so a listed format is one the code knows. (Formats that are recognized but not yet readable - the PDF,
PowerPoint, Visio, and OpenSCAD kinds - are listed from the start and are reported as unsupported by `info` and
`render` until they are implemented.)

**OutputFormats** (`IReadOnlyList<OutputFormatInfo>`): `png`, `jpeg`, `bmp`, `tiff`. A row has `Name` (also the value
of the `--format` option), `Extensions`, `Alpha` (keeps transparency), and `Lossy` (takes a quality setting). The
rows are the `OutputFormat` members in order, with the extensions that `ImageWriter.ExtensionFormats` accepts.

**Formats**: `ICommand` named `formats`, with no aliases and no options of its own. It holds the tool version and the
CanvasNet library version it reports. The public constructor reads them from the assemblies'
informational versions (falling back to the assembly version, then `0.0.0`); an `internal` constructor takes them as
arguments so that tests can produce output that does not depend on the build.

**Info**: `ICommand` named `info`, with no aliases and no options of its own (`--json` is global). It holds no state.

**Constants**: the JPEG quality range (`1` to `100`) and default (`90`), which are those of `ImageWriter`, and the
maximum image dimension, which is `Surface.MaxDimension` (8192).

#### Key Methods

**Formats.Run(context, args)**: reads the arguments with `ArgumentReader` (against an empty option table) and
requires no positional arguments, so a stray argument or an unknown option is a `usage-error`. Then writes either
the JSON document (when `context.Json`) or the text listing, through `context.WriteLine`.

**Text listing**: the sections _Input formats_ (a table of format, kind, extensions, pages, and scalable),
_Output formats_ (format, extensions, alpha, quality), _Limits_ (maximum image dimension, JPEG quality range and
default), and _Versions_ (tool and library). Columns are aligned and the last column is not padded.

**JSON document**: written through `ResultWriter.Success("formats", ...)` with these fields after the envelope, in
this order:

<!-- markdownlint-disable MD013 -->
| Key | Value |
| --- | ----- |
| `tool` | object: `name` (`canvasnet`), `version` |
| `library` | object: `name` (`DemaConsulting.CanvasNet`), `version` |
| `inputs` | array of objects: `format`, `kind`, `extensions` (array), `multiPage` (bool), `vector` (bool) |
| `outputs` | array of objects: `format`, `extensions` (array), `alpha` (bool), `lossy` (bool) |
| `quality` | object: `min`, `max`, `default` |
| `maxDimension` | number |
<!-- markdownlint-enable MD013 -->

**Info.Run(context, args)**: reads the arguments with `ArgumentReader` (against an empty option table) and requires
exactly one positional argument, the input. Calls `SourceLoader.Probe` and writes either the JSON document (when
`context.Json`) or one text line through `context.WriteLine`. Nothing is decoded and no file is written.

**Info text line**: `{path}: {format}, {width}x{height} px[ (intrinsic size)], {channels} channel[s][ with alpha],
{bytes} bytes`, followed when they apply by `, {n} frames (the first is used)` (an animated GIF),
`, EXIF orientation {n}` (a JPEG that has one), and `, cannot be decoded`. The size is the stored size, before any
EXIF orientation.

**Info JSON document**: `ResultWriter.Success("info", ...)` with these fields after the envelope, in this order:

<!-- markdownlint-disable MD013 -->
| Key | Value |
| --- | ----- |
| `input` | object: `path`, `format`, `bytes` |
| `image` | object: `width`, `height`, `channels`, `hasAlpha`, `canDecode`, `frameCount`, and `exifOrientation` only when the file has one |
<!-- markdownlint-enable MD013 -->

#### Error Handling

An argument or option on the `formats` command line throws a `ToolException` with the code `usage-error` (from
`ArgumentReader`); so does a missing input, an extra input, or an unknown option on the `info` command line. The
`info` command's failures to read the file come from `SourceLoader.Probe` (`input-not-found`, `input-unreadable`,
`unsupported-format`, `corrupt-input`, `unsupported-feature`). Null arguments throw `ArgumentNullException`. The
`formats` command does not touch the file system and has no other failure mode.

#### Dependencies

- **ArgumentReader** - argument validation.
- **ResultWriter** - the JSON documents.
- **Context** - the `Json` flag and `WriteLine`.
- **Program** - the tool version.
- **SourceLoader** and **ImageWriter** (Imaging) - the `info` command's probe, and the format and quality
  definitions that the tables and constants must agree with.
- **CanvasNet core** - `Surface.MaxDimension` and the library version.

#### Callers

- **CommandRegistry** - `CreateDefault` registers `Formats` and `Info`; the registry dispatches to them.
- **Validation** - the `CanvasNetTool_Formats` and `CanvasNetTool_Info` self-tests run them in-process.
