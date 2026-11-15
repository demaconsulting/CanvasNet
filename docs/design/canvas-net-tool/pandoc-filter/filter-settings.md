### FilterSettings

![PandocFilter Structure](PandocFilterView.svg)

#### Purpose

`FilterSettings` holds the inputs of one run of the Pandoc filter: what only the Lua shim can see (the output format,
the output file, embedding, the resource path, and the shim's location), the environment variables of the filter, and
what follows from them: the cache folder, the file-access arguments of each picture, the tool identity, the `src` of a
picture, and the number of pictures drawn at once. It ports `find_tool`, `split_command`, `find_file_access`,
`file_access_of`, and `picture_src` of the Lua filter (version 16). The unit is the internal sealed class
`FilterSettings` in `FilterSettings.cs`, which also holds the supporting record `FileAccessArguments`. The environment
and the working directory are inputs, so a test needs no process state. The class is not thread-safe: the engine reads
it only while it plans, in document order, on one thread.

#### Data Model

**FileAccessArguments** (internal sealed record): `Arguments` (`--file-access MODE` and one `--allow-root FOLDER` per
folder; empty in the default mode), `Roots` (the folders the dependency walk may open files in, or `null` for
anywhere), `Restrictive` (whether a denied picture stops the build: a mode other than `any`, or folders), and `Key`
(`file-access` and each argument, one per line, part of every picture's cache key).

**Properties**: `FilterPath` (required; `PANDOC_SCRIPT_FILE`), `WorkingDirectory` (required, absolute),
`Environment` (required; `Func<string, string?>`, `null` for an unset variable), `Format` (default empty),
`OutputFile` (`null` for standard output), `EmbedResources`, `ResourcePaths` (default `.`), and `ToolVersion` (default
the running tool's version).

**Constants**: `ToolVariable` = `CANVASNET_TOOL`, `FileAccessVariable` = `CANVASNET_FILE_ACCESS`, `RootsVariable` =
`CANVASNET_ROOTS`, `JobsVariable` = `CANVASNET_JOBS`, `MaxJobs` = 64, and `DefaultJobs` = 4.

**Accepted modes**: `any`, `roots`, `no-symlinks`, `roots,no-symlinks`, and `no-symlinks,roots`.

**HTML writers**: `html`, `revealjs`, `slidy`, `slideous`, `s5`, and `dzslides`, matched as prefixes of `Format`.

**Cached values** (private): the cache folder, the tool identity, and the shared file-access arguments, each computed
on first use and kept.

#### Key Methods

**CacheFolder**: `generated/canvasnet` in the folder above the shim's folder, absolute and collapsed, so
`docs/filters/canvasnet.lua` draws into `docs/generated/canvasnet`.

**GetResourceDirectories()**: the resource folders, absolute and collapsed, in order.

**ToolIdentity**: three lines: the version text (trailing white space removed), the label (`default`, or
`CANVASNET_TOOL=<command>` when the variable has a non-space character), and the fingerprint. The command is
`dotnet canvasnet` by default or `SplitCommand(CANVASNET_TOOL)`. For the first leading argument (after the program)
that ends in `.dll` in any ASCII case and exists as a file, the fingerprint is `name=sha1` of every readable `.dll`
entry in its folder, sorted by bytes and joined with `;`; otherwise it is empty.

**SplitCommand(text)**: white space separates, a double quote toggles grouping (spaces included) and may appear inside
a word (`a""b` is `ab`), and there are no escapes.

**FileAccessOf(source)**: reads the variables once (`ReadFileAccess`). The mode is `CANVASNET_FILE_ACCESS` trimmed,
ASCII-lowercased, and without white space around its commas; empty is unset. The folders are the non-blank entries of
`CANVASNET_ROOTS` split as the platform's search path (`;` on Windows, `:` elsewhere), each made absolute from the
working directory. Folders without a mode imply `roots`, passed explicitly. The arguments are `--file-access <mode>`
when there is a mode and `--allow-root <folder>` per folder. When the mode contains `roots` and there are no folders,
each call adds `--allow-root` for the working directory and, when the source's folder is not the same as or under it,
for that folder too, and lists them as `Roots`, so the decision of the tool depends only on its arguments, which are
part of the key.

**PictureSrc(file)**: for an HTML writer without `EmbedResources` and with an `OutputFile`, the path from the output
file's folder to the picture (WeasyPrint reads it from there), unless the two share no first segment (another drive);
otherwise the absolute path with forward slashes.

**ResolveJobs(option, environment)** (static): the already checked `--jobs` value; else `CANVASNET_JOBS` trimmed, ASCII
digits only, from 1 to 64; else `min(ProcessorCount, 4)` when the variable is unset or blank.

**Sha1Hex(bytes)** (static): the lowercase hexadecimal SHA-1, as `pandoc.sha1`; it names cached content and is not a
security control.

#### Error Handling

Every refusal is a `ToolException` with `ErrorCodes.PandocFilterError` (`pandoc-filter-error`):

- `CANVASNET_FILE_ACCESS is '<mode>'; use roots, no-symlinks, roots,no-symlinks, or any`
- `CANVASNET_ROOTS cannot be used with CANVASNET_FILE_ACCESS=<mode>; use roots or roots,no-symlinks` (mode `any` or
  `no-symlinks`)
- `CANVASNET_TOOL has an empty quoted part: <text>`, `CANVASNET_TOOL has a quote that is never closed: <text>`, and
  `CANVASNET_TOOL names no program`
- `CANVASNET_JOBS is '<text>'; write a whole number from 1 up to 64`

The file-access and tool errors are raised on first use of `FileAccessOf` and `ToolIdentity`, that is when the first
picture needs them, as in the Lua filter; a document without pictures does not fail on them. The jobs error is raised
by the command before the run.

#### Dependencies

- **FilterPaths** - absolute and relative paths, the search-path split, the Lua white-space trims, `AsciiLower`,
  `SortBytes`, and file reads.
- **ToolException** (tool) and **ErrorCodes** (Viewer library) - the error and its code.
- **Program** - the default `ToolVersion`.

#### Callers

- **PandocFilterCommand** - builds the settings from its options and the process environment (`.` as the resource
  path when none is given) and calls `ResolveJobs` with the value of `--jobs`.
- **PandocFilterEngine** - reads `CacheFolder`, `GetResourceDirectories`, `ToolIdentity`, `FileAccessOf`, and
  `PictureSrc` while it plans and rewrites pictures, and the variable names in the message of a denied picture.
- **PictureCache** - `Sha1Hex` for the name of a Mermaid block's source file.
