## PandocFilter

![PandocFilter Structure](PandocFilterView.svg)

### Overview

The `PandocFilter` subsystem (namespace `DemaConsulting.CanvasNet.Tool.PandocFilter`, folder
`src/DemaConsulting.CanvasNet.Tool/PandocFilter/`) draws the pictures of a Pandoc document in one process. It is the
logic of the Pandoc filter that was a Lua filter of about 1,840 lines (filter version 16), ported literally to C#, so
a document is drawn, named, and reported as before; each repository now keeps only a thin Lua shim,
`docs/filters/canvasnet.lua`, which sends the whole document to the `pandoc-filter` command once and applies what comes
back. A fix of the filter therefore ships with the tool, and a Pandoc run starts the tool once instead of once per
picture. Everything in the subsystem is `internal`.

It contains eight units:

- `PandocAst` - reads and writes Pandoc's JSON document, finds the pictures and code blocks in the order Pandoc's Lua
  filters visit them, and builds the elements that replace them.
- `PictureAttributes` - the kind of a picture from its file name, the checks of its attributes, the tool options they
  ask for, and the attributes that stay on the picture.
- `FilterPaths` - the path rules of the Lua filter and of `pandoc.path`: finding a source in the resource path, file
  URIs, remote URLs, and the labels of the files in a key.
- `DependencyHasher` - every file a picture reads (includes, data files, linked pictures, nested drawings), as the
  parts of its cache key, with the limits of 64 levels and 512 files.
- `FilterSettings` - the inputs of the run and the variables `CANVASNET_FILE_ACCESS`, `CANVASNET_ROOTS`,
  `CANVASNET_TOOL`, and `CANVASNET_JOBS`: the file-access arguments, the tool identity, the cache folder, the `src` of a
  picture, and the number of jobs.
- `PictureCache` - the key (version `17`), the names of the pictures, copies, and markers, and the atomic writes.
- `PandocFilterEngine` - plans, draws, and applies; the `IPictureDrawer` seam and its production `ToolPictureDrawer`.
- `PandocFilterShim` - the shim embedded in the tool, its protocol, and writing it.

The command that runs the subsystem, `PandocFilterCommand`, is a unit of the `Commands` subsystem (see
_PandocFilterCommand_).

### Interfaces

**PandocFilterEngine(settings, drawer, jobs).Run(input, pandocVersion)** (provided to `PandocFilterCommand`): takes
the bytes of a Pandoc JSON document and returns the bytes of the transformed document (UTF-8 without a byte order
mark), with the warnings in the metadata list `canvasnet-warnings`; a caption of a Mermaid block is a placeholder
`Span` of class `canvasnet-markdown` whose attribute `markdown` the shim parses. A failure is a `ToolException`:
`pandoc-filter-error` for a mistake in a picture, `corrupt-input` for a document that is not Pandoc 3's JSON,
`file-access-denied` for a picture a restrictive mode denies, and the code of the tool for a drawing that fails;
`WarningsBeforeFailure` then holds the warnings of the pictures before it.

**FilterSettings** (provided to `PandocFilterCommand`): the inputs of a run (the shim's path, the working directory, the
environment, the format, the output file, the embed flag, and the resource paths) and `ResolveJobs`.

**PandocFilterShim** (provided to `PandocFilterCommand`): `Bytes`, `Write(path)`, `CheckProtocol(protocol,
filterPath)`, and the protocol constants (`Protocol`, `MinProtocol`, and `MaxProtocol`, all 1).

**Lua shim protocol 1** (provided to Pandoc): the shim runs `<prog> pandoc-filter --json --protocol 1 --filter-path
<PANDOC_SCRIPT_FILE> --format <FORMAT> --pandoc-version <PANDOC_VERSION> [--resource-path <dir>]... [--pandoc-output
<file>] [--embed-resources]` with `pandoc.pipe` and the document on standard input. Exit code 0 means standard output
is the document; exit code 1 means standard output is the JSON failure document, whose message and hint the shim
raises as the error of the build. The shim starts the tool only when the document has a code block with a class or a
local picture that is not a PNG, JPEG, GIF, or WebP raster.

**RenderCommand.Execute** and **FlattenCommand.Execute** (consumed, from the `Commands` subsystem): `ToolPictureDrawer`
draws a picture with the same argument list the Lua filter passed to `canvasnet render ... --json` and `canvasnet
flatten ... --json`, in-process, so the pictures, the notes, and the errors are those of the commands.

**ErrorCodes** (consumed, from the `ViewerException` unit of the Viewer library): `pandoc-filter-error`,
`corrupt-input`, and `file-access-denied`.

### Design

A run has three steps. **Plan**, sequentially in the order Pandoc's Lua filter visited the elements (every `Image` of
the metadata and the blocks, then every `CodeBlock`): `PictureAttributes` checks the attributes, `FilterPaths` finds the
source, `FilterSettings` gives the file-access arguments, `DependencyHasher` reads every file the picture depends on,
and `PictureCache` computes the key and looks the picture or a marker up in the cache folder `generated/canvasnet`
beside the folder of the shim. A picture that misses becomes a job; a second picture with the same key follows the first
one's job. **Draw** the jobs, up to the number of jobs at once (`--jobs`, `CANVASNET_JOBS`, or the processors up to 4),
each into a partial file that is renamed into place. **Apply**, sequentially in document order: `PandocAst` rewrites the
elements and the warnings go to the metadata.

Every failure is recorded with the position of its picture. Planning stops at the first one; a job above a known failure
may be skipped and a job below it always runs, so the reported failure is always the one of the first picture in the
order of the plan, the one the Lua filter reported, and the document, the warnings, and the cache files are the same
for one job or many. The cache is content-addressed, so two builds sharing a folder never conflict: a file is written
under a random partial name and renamed without overwriting, and a lost race keeps the other build's identical copy.

The logic is a literal port of the Lua filter. The key composition, the separators, the file names, and the messages are
unchanged; only the key version (16 to 17) and three details differ: a kept attribute stays in its original order (Lua
used hash order), a repeated attribute keeps its last value at the position of its first, and the warnings of the
pictures before a failure are written to standard error by the command, since the failure document has no place for
them. The variables `CANVASNET_FILE_ACCESS` and `CANVASNET_ROOTS` are checked when the first picture needs them, as the
Lua filter did.
