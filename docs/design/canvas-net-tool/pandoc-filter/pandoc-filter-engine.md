### PandocFilterEngine

![PandocFilter Structure](PandocFilterView.svg)

#### Purpose

`PandocFilterEngine` draws the pictures of a Pandoc document in one process: it is the logic of the former Lua filter
(version 16), run once for the whole document instead of once per picture. It plans every picture, draws the pictures
that miss the cache (up to a number of jobs at once), rewrites the elements, and returns the warnings in the document
metadata. The unit is the internal sealed partial class `PandocFilterEngine` in `PandocFilterEngine.cs` (plan, apply)
and `PandocFilterEngine.Drawing.cs` (draw). The latter also holds the supporting types of the unit:

- `IPictureDrawer` (internal interface) - the seam between the engine and the tool's commands: `Render(args)` and
  `Flatten(args)`, each returning a `PictureDrawing` or throwing a `ViewerException` with a stable code. An
  implementation must be safe to call from several threads at once.
- `ToolPictureDrawer` (internal sealed class) - the production drawer: it calls `RenderCommand.Execute` and
  `FlattenCommand.Execute` in-process with the arguments the Lua filter passed to `canvasnet render ... --json` and
  `canvasnet flatten ... --json`, and maps their result to a `PictureDrawing` (a render is always `Flattened`). It is
  stateless.
- `PictureDiagnostic(Code, Message, Embedded)` (internal sealed record) - a note of the tool; `Embedded` is true when
  the note has an origin, that is, it is about a file the picture embeds.
- `PictureDrawing(Flattened, Diagnostics, Echo)` (internal sealed record) - what the tool reported for a picture.

#### Data Model

**Fields**: `_settings` (`FilterSettings`, the inputs of the run), `_drawer` (`IPictureDrawer`), and `_jobs` (the most
pictures drawn at once, at least 1). **WarningsBeforeFailure** (`IReadOnlyList<string>`): the warnings of the pictures
before the failure of the last run, in document order; reset to empty at the start of every run. The constant
`LeftAlone` is the end of a warning about a diagram left as written: `; it is left as written and not drawn`.

Private nested types:

- **PlannedPicture** - an element the plan handles: its position (`Index`), `Name` (its `src`, or the name of a code
  block from `PictureAttributes.BlockName`, for example `mermaid block (journey)`), `Node`, the `Block` and `Caption`
  of a code block, the `Keep` attributes, the `Warnings` found while planning, and either a cached `File`, or a `Job`
  with `Leads` true for the picture that created the job and false for a follower with the same key.
- **DrawJob** - a picture to draw: position and name of the leading picture, `Svg` (flatten rather than render), kind,
  source, options, file-access arguments, cache folder, key, the `File` it writes, the `Marker` it writes when the
  picture is left alone, and its `Outcome`.
- **JobOutcome** - `Drawn`, the `Warnings` of the leading picture, `UnsupportedMessage` of a diagram left as written,
  and `PlainLines` of a drawing with nothing to flatten; `FollowerWarnings(name)` gives a follower the warnings a later
  build would read from the marker (none for a drawn picture).
- **Failure** - a position and the captured exception (`ExceptionDispatchInfo`).

#### Key Methods

**Run(input, pandocVersion)**: parses the document with `PandocAst.Parse`, calls `Transform`, sets the warnings with
`PandocAst.SetWarnings` (metadata key `canvasnet-warnings`, removed when there are none), and returns
`PandocAst.Write(document)`.

**Transform(document)**: the three steps.

1. **Plan**, sequentially in the order Pandoc's Lua filter visited the elements: every `Image` (those in the metadata
   first, then those in the blocks), then every `CodeBlock`, as `PandocAst.FindAll` returns them. Planning stops at the
   first failure (an `ArgumentException` or `InvalidOperationException`, which includes `ToolException`), recorded with
   its position.
2. **Draw** the jobs sorted by position with `Draw`, passing the position of a planning failure as the limit, then
   **commit** what they left with `CommitInOrder`.
3. If there is any failure, the one of the lowest position is rethrown with its stack trace, after
   `WarningsBeforeFailure` is set to the warnings of the planned pictures before it. Otherwise **Apply** every planned
   picture in planning order and return the warnings.

**PlanImage**: an `.svg` source goes to `PlanSvg`; a source with a URL scheme or a kind `PictureAttributes.KindOf` does
not know is left alone; otherwise the source is located with `FilterPaths.LocateSource` in
`FilterSettings.GetResourceDirectories()` and planned with `PlanDraw`.

**PlanBlock**: a code block for which `PictureAttributes.BlockKindOf` finds no kind (it has no class `mermaid` or
`plantuml`) is left alone. Otherwise the caption attribute is split off (`PictureAttributes.SplitCaption`), the block's
text and a line feed are written atomically to the cache folder (`PictureCache.BlockSourceFile`, a `.mmd` or `.puml`
named by the SHA-1 of the text), and the file is planned with `PlanDraw` as that kind (`mermaid` or `plantuml`) with
the kind as the stem. A `plantuml` block is read from the cache folder, so a relative `!include` in it is looked up
there. Every picture and
code block that `PlanImage` and `PlanBlock` leave alone passes through the run untouched, which is what the shim's
fast path relies on.

**PlanDraw**: builds the options and kept attributes (`PictureAttributes.Build`), the file access
(`FilterSettings.FileAccessOf`), the dependency hashes (`DependencyHasher.ForPicture`, reading through
`FilterSettings.PolicyOf`), and the key
(`PictureCache.ComputeKey`). A picture `<stem>-<key>.png` in the cache is used; otherwise an existing
`<key>.unsupported` marker gives the warning `<name>: <message>; it is left as written and not drawn`; otherwise the
picture is attached to a job.

**PlanSvg**: a drawing `FilterPaths.LocateSource` cannot find is left to Pandoc (no error); a drawing that links no
file only the tool draws (`DependencyHasher.ForSvg` returns not composite) is left alone. Otherwise the options come
from `PictureAttributes.BuildSvg`, and the key also includes the drawing's folder relative to the cache folder. An
existing `<key>.plain` marker gives one warning `<src>: <line>` per non-empty line; otherwise the copy
`<stem>-<key>.svg` in the cache is used; otherwise the drawing is attached to a job.

**Attach**: a picture whose key already has a job follows it (the picture is drawn once); otherwise it creates and
leads a new job.

**Draw(jobs, limit)**: runs the jobs in order of position, one after the other when `_jobs` is 1 or there are fewer
than two jobs, else with `Parallel.ForEach` over a non-buffering partitioner with `MaxDegreeOfParallelism = _jobs`, so
the jobs start in order of position. A job whose position is above the lowest failure known so far is skipped; a job
below it always runs. Every exception of a job, a defect too, is recorded with its position, and the lowest known
position is lowered with a compare-and-exchange loop. A job writes nothing into the cache under its own name: it sets
`Outcome` and `Pending` (a `PendingWrite`: the partial file to rename with the message of a failed rename, or the text
of a marker), so which jobs happened to run before a failure was known cannot change the cache.

**CommitInOrder(jobs, failures)**: in order of position, every job below the first failure renames its partial file
into the cache (`PictureCache.Commit`) or writes its marker (`PictureCache.WriteTextAtomically`); a commit that fails
is added to the failures and becomes the first. The partial file of every job at or after the first failure is
deleted. The jobs committed are exactly those a run of one job commits, so the cache files after a failed run are the
same for one job or many.

**Render(job)**: calls `IPictureDrawer.Render` with the source, the options, the file-access arguments, and
`--output` set to a partial file (`<key>.<9 digits>.partial.png`). After the denial check, a Mermaid picture whose
diagnostics hold `mermaid-diagram-unsupported` (a PlantUML picture `plantuml-diagram-unsupported`) not `Embedded`
deletes the partial file, leaves the message to write to
the `.unsupported` marker, and is not drawn, with the warning ending in `LeftAlone`. Otherwise every diagnostic and echo
line becomes the warning `<name>: <text>`, and the partial file is left to commit to the picture file.

**Flatten(job)**: calls `IPictureDrawer.Flatten` likewise (partial `.svg`). After the denial check, each diagnostic
message is made one line (`OneLine`: every run of carriage returns and line feeds becomes one space). When the tool did
not flatten, the partial file is deleted, the messages are left to write to the `.plain` marker, one per line, and
the drawing is left as written; otherwise the partial file is left to commit to the copy.

**Apply(item, warnings)**: adds the picture's warnings (`CollectWarnings`: the planning warnings, then the job's
warnings for the leader or `FollowerWarnings` for a follower). When a file was found or drawn, an image gets its `src`
set to `FilterSettings.PictureSrc(file)` and its attributes to the kept ones (`PandocAst.RewriteImage`). A code block
is replaced: with a caption, by a `Figure` with the block's identifier, a caption of one `Plain` holding the
placeholder `Span` of class `canvasnet-markdown` (`PandocAst.MarkdownPlaceholder`), and one `Plain` with an image
whose description is the same placeholder; without a caption, by a `Para` holding an image with the block's
identifier and an empty description. The image keeps the block's classes other than the one that made it a picture
(`mermaid` or `plantuml`) and the kept
attributes.

#### Error Handling

All errors are `ToolException` (a `ViewerException`), with the Lua filter's messages:

- `cannot find '<src>' in the document's resource path` (`pandoc-filter-error`) for a missing non-SVG source.
- `could not create <folder>: <reason>` (`pandoc-filter-error`) when the cache folder cannot be created.
- `drawing '<name>' failed: <message>` or `flattening '<name>' failed: <message>`, followed by the hint in parentheses
  when the tool gave one, for an `ArgumentException` or `InvalidOperationException` of the drawer; the code is the
  `ViewerException`'s code, `usage-error` for an `ArgumentException`, or `operation-failed` otherwise. The partial
  file is deleted. A `size-limit` error that carries the scale that fits (`Exception.Data` under the key
  `DemaConsulting.CanvasNet.Viewer.FitScale`, set by `SourceLoader.ComputeTargetSize` for a size that follows the
  natural size) gets a hint that names the attribute instead of the command-line options: `Set the block's scale
  attribute to <fit> or less, for example {.mermaid scale=<fit>}.` (`{.plantuml scale=<fit>}` for a PlantUML block) for
  a
  code block (the `Block` flag of the job), and
  `Set the picture's scale attribute to <fit> or less, for example {scale=<fit>}.` for an image.
- In a restrictive file-access mode, a diagnostic `image-access-denied` or `file-access-denied` stops the run with
  `drawing '<name>' failed: <message> (denied by CANVASNET_FILE_ACCESS/CANVASNET_ROOTS)` (`file-access-denied`), for a
  render and a flatten alike, before a marker or a picture is written.
- `could not write the picture of '<name>' to <file>` and `could not write the flattened copy of '<name>' to <file>`
  from `PictureCache.Commit`, and the errors of the units it calls (attributes, paths, environment, cache).

Planning stops at its first failure; the jobs below it still run; only the jobs below the first failure are committed
into the cache; the failure of the lowest position is rethrown (planning, drawing, or commit alike), so the result and
the cache files are the ones the Lua filter left whatever the number of jobs. An
unexpected exception of a job is also recorded and rethrown by position. After a failure the document is left partly
unchanged and nothing is returned.

#### Dependencies

- **PandocAst** - reading, finding, building, and writing the elements and the warnings.
- **FilterSettings** - resource directories, tool identity, file access, cache folder, and the picture `src`.
- **FilterPaths** - locating sources, file existence, folders, and relative paths.
- **PictureAttributes** - picture kinds, options and kept attributes, captions, and block names.
- **DependencyHasher** - the hashes of the files a picture depends on, and whether a drawing is composite.
- **PictureCache** - keys, file and marker names, atomic writes, commits, and deletes.
- **RenderCommand** and **FlattenCommand** - called in-process by `ToolPictureDrawer`.
- **MermaidDiagnosticCodes**, **PlantUmlDiagnosticCodes**, **FolderImageResolver**, **ErrorCodes**, and
  **ViewerException** - the codes it reads
  and reports.

#### Callers

- **PandocFilterCommand** - creates the engine with `ToolPictureDrawer` and the resolved number of jobs, calls `Run`,
  and, when it fails, writes each of `WarningsBeforeFailure` to standard error as
  `[WARNING] canvasnet.lua: <warning>` before reporting the error.
