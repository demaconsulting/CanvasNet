### DiscoveryCommands

<!-- cspell:ignore canvasnet jpeg tiff pptx vsdx scad -->

![Commands Structure](CommandsView.svg)

#### Purpose

`DiscoveryCommands` holds the read-only commands that tell a caller what the tool can do. Its single
responsibility is to answer "what do you support?" without reading or writing any image. As of this release it
provides one command, `formats`, implemented as the nested class `DiscoveryCommands.Formats`; commands that
describe a given file are added to this unit as they are implemented. The table of formats is data owned by this
unit.

#### Data Model

**InputFormats** (`IReadOnlyList<InputFormat>`): one row per input format, in listing order - `png`, `jpeg`, `bmp`,
`tiff`, `gif`, `jpeg2000`, `svg`, `pdf`, `pptx`, `vsdx`, `scad`. A row (`InputFormat`) has `Name`, `Kind`
(`raster`: decoded at its own size; `vector`: a drawing rendered at any requested size; `document`: pages or slides
rendered at any requested size; `model`: a 3D model rendered from a viewpoint), `Extensions` (lower case, with the
dot), `MultiPage` (one file holds pages or slides that can be chosen), and `Vector` (rendered at the requested size
rather than decoded at its own size).

**OutputFormats** (`IReadOnlyList<OutputFormatInfo>`): `png`, `jpeg`, `bmp`, `tiff`. A row has `Name` (also the value
of the `--format` option), `Extensions`, `Alpha` (keeps transparency), and `Lossy` (takes a quality setting).

**Formats**: `ICommand` named `formats`, with no aliases and no options of its own. It holds the tool version and the
CanvasNet library version it reports. The public constructor reads them from the assemblies'
informational versions (falling back to the assembly version, then `0.0.0`); an `internal` constructor takes them as
arguments so that tests can produce output that does not depend on the build.

**Constants**: the JPEG quality range (`1` to `100`) and default (`90`), and the maximum image dimension, which is
`Surface.MaxDimension` (8192).

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

#### Error Handling

An argument or option on the `formats` command line throws a `ToolException` with the code `usage-error` (from
`ArgumentReader`). Null arguments throw `ArgumentNullException`. The command does not touch the file system and has
no other failure mode.

#### Dependencies

- **ArgumentReader** - argument validation.
- **ResultWriter** - the JSON document.
- **Context** - the `Json` flag and `WriteLine`.
- **Program** - the tool version.
- **CanvasNet core** - `Surface.MaxDimension` and the library version.

#### Callers

- **CommandRegistry** - `CreateDefault` registers `Formats`; the registry dispatches to it.
- **Validation** - the `CanvasNetTool_Formats` self-test runs it in-process.
