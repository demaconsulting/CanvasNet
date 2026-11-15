### PictureCache

![PandocFilter Structure](PandocFilterView.svg)

#### Purpose

`PictureCache` is the picture cache of the Pandoc filter: it computes the cache key of a picture, names the pictures,
flattened copies, markers, code block sources, and temporary files of the cache folder, and writes them atomically so
that two builds can share the folder. Every name is content-addressed: the key is the SHA-1 of everything that decides
the picture, so an unchanged source is never drawn again and a changed one always is. The composition, the separators,
and the names are those of the Lua filter (version 16); only the key version differs. The unit is the internal static
class `PictureCache` in `src/DemaConsulting.CanvasNet.Tool/PandocFilter/PictureCache.cs`.

#### Data Model

The class is stateless and thread-safe.

**KeyVersion** (`"17"`): part of every key, so a change to what the filter draws or how it treats a diagram draws the
pictures again. Its comment records the history: 12, a diagram type the tool does not draw is left as written with a
warning; 13, packet diagrams are drawn; 14, git graphs are drawn; 15, `CANVASNET_FILE_ACCESS` and `CANVASNET_ROOTS` pass
`--file-access` and `--allow-root` and are part of every name, and an absolute path or a hostless `file:` URI is
hashed; 16, `CANVASNET_ROOTS` alone passes `--file-access roots`, a restrictive build stops on `file-access-denied`,
and a chart linked back from a picture it draws is hashed once; 17, the filter runs in the tool (the `pandoc-filter`
command) and the Lua file is a shim, with the key composed as in 16 and the new version keeping the two implementations
from trusting each other's markers.

**FieldSeparator** (`"\n\0\n"`): separates the fields of the key and the parts of the dependencies.

#### Key Methods

**ComputeKey(kind, toolIdentity, fileAccessKey, options, folder, parts)**: hashes with an incremental SHA-1 the fields
`canvasnet.lua 17`, the kind (`svg` for a flattened drawing), the tool identity (`FilterSettings.ToolIdentity`), the
file-access key (`FileAccessArguments.Key`), the sorted options joined with `;`, and, for a drawing only, `folder
<folder>` (its folder as seen from the cache folder), each followed by the separator; then the dependency parts of
`DependencyHasher`, streamed with the separator between them (not after the last). It returns the lowercase hexadecimal
digest, the same as the Lua filter's `pandoc.sha1` of the fields and parts joined with `\n\0\n`.

**StemOf(source)**: the source's file name without its last extension (`FilterPaths.Stem`), without a further `.vl`, with
every UTF-8 byte that is not an ASCII letter, digit, `_`, or `-` made a `-` (a character of several bytes becomes
several dashes).

**Names**: `PictureFile` is `<stem>-<key>.png`, `FlattenedFile` is `<stem>-<key>.svg`, `UnsupportedMarker` is
`<key>.unsupported` (holding the tool's message for a diagram it does not draw), and `PlainMarker` is `<key>.plain`
(holding the tool's warnings, one per line, for a drawing with nothing to flatten). `BlockSourceFile(folder, text)` is
the SHA-1 of `canvasnet.lua mermaid block\n` and the text (the block's text and a line feed, as written to the file)
with `.mmd`. All are joined to the cache folder with `FilterPaths.Join`.

**PartialFile(folder, key, extension)**: `<key>.<9 random digits>.partial.<extension>` in the cache folder (`png` or
`svg`), so that the links a flattened copy rebases for the cache folder are right after the rename. The digits come from
`RandomNumberGenerator.GetInt32` (100000000 to 999999999).

**WriteTextAtomically(path, content)**: returns when the file already exists (it holds the same text, as its name is a
hash of it); otherwise writes the text as UTF-8 without a byte order mark to `<path>.<9 random digits>.partial` and
commits it. When the write fails, the temporary file (which may have been created before the failure) is deleted
before the error is raised. An internal overload takes the writer (`File.WriteAllBytes` in production), so a test can
make the write fail after the file was created.

**Commit(partial, target, failure)**: renames the temporary file to the cache name with `File.Move` without
overwriting. When the rename fails (another build wrote the same file first, or the rename itself failed), the
temporary file is deleted, and the file is kept when it is there (the lost-race rule: the other copy is identical); when
it is not there, the commit fails.

**ReadText(path)**: the UTF-8 text of a marker or another cache file, or `null` when it cannot be read.
**Delete(path)**: deletes a file, ignoring a failure (`os.remove`); a temporary file left behind is harmless, as no
cache name ends in `.partial.*`.

#### Error Handling

`WriteTextAtomically` throws `ToolException` with `ErrorCodes.PandocFilterError` (`pandoc-filter-error`) and the
message `could not write <partial>` when the temporary file cannot be written (`IOException` or
`UnauthorizedAccessException`; the temporary file is deleted first), and `Commit` throws it with the caller's message
when the rename failed and the target is not there (for a text file, `could not write <path>`). `ReadText` returns
`null` and `Delete` ignores `IOException` and `UnauthorizedAccessException`.

#### Dependencies

- **FilterPaths** - `Join`, `FileName`, `Stem`, `FileExists`, and `ReadFile`.
- **FilterSettings** - `Sha1Hex` for the name of a code block's source.
- **ToolException** (tool) and **ErrorCodes** (Viewer library) - the error and its code.
- **.NET base class library** - `IncrementalHash`, `RandomNumberGenerator`, and `File`.

#### Callers

- **PandocFilterEngine** - computes the keys, names the pictures, copies, markers, and code block sources, reads the
  markers, and writes the code block sources and markers; its drawing jobs draw into partial files and commit them.
- **PandocFilterShim** - deletes a temporary file with `Delete`.
