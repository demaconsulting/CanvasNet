### FontDirectoryScanner

<!-- cspell:ignore ttcf reparse ARIALN -->

`FontDirectoryScanner` is the third public software unit in the `Fonts` subsystem. It scans
directory trees for font files and reports the identity, name, and style metadata of every
well-formed face they hold, reading only each font's headers. It is the one implementation of
"directory scan to faces" that `SystemFontCatalog` builds its host-font catalog from, and that
a caller with its own font directories can use directly. The unit includes the small
`FontScanLimits` bounds record it is configured with.

#### Purpose

A caller that needs to know which fonts a directory offers - the host operating system's font
directories (`SystemFontCatalog.Fonts`), or a directory the caller supplies - must not load every
font fully to read its name and style, must get the same answer in the same order on every run,
and must be protected against a huge or hostile tree and against links that lead out of, or back
into, the scanned tree. `FontDirectoryScanner.Scan` provides exactly that and nothing about
matching or loading: choosing a face from the result is `SystemFontCatalog.FindInFamily`'s job,
and loading it is `SystemFontCatalog.Load`'s.

#### Data Model

- **`FontScanLimits`** - a `sealed record` of four `init`-validated bounds, each at least one:
  `MaxDirectories` (10,000), `MaxEntries` (200,000), `MaxFontFiles` (5,000) and `MaxFaces`
  (20,000), counted across all roots of one scan. Immutable once constructed, so safe to share.
- **`ScanBudget`** (private) - the per-scan counters of directories listed, entries examined, font
  files accepted and faces examined. Reaching a directory/entry/font-file bound stops the
  *collection* of files; reaching the face bound stops the *reading* of faces. What was found before
  a bound stays in the result.
- The result is a `List<SystemFontInfo>` returned as `IReadOnlyList<SystemFontInfo>`; `SystemFontInfo`
  is documented with `SystemFontCatalog`.

#### Key Methods

##### Scan(directories, limits, trustLinkedFiles, cancellationToken)

For each root, in the order given:

1. **Collect.** An empty, `null`, missing, or malformed root contributes nothing. Unless
   `trustLinkedFiles` is `true`, a root that is a link is skipped. The tree is walked depth first,
   one directory at a time, listing entries with `DirectoryInfo.EnumerateFileSystemInfos`: a
   subdirectory that is a link is never followed (in either mode), a font file (`.ttf`, `.otf`,
   `.ttc`, `.otc`, case-insensitive) is accepted unless it is a link and `trustLinkedFiles` is
   `false`, and subdirectories are visited in path order (below). An unreadable directory contributes
   nothing.
2. **Order.** The font files collected under the root are sorted by *path order* of their full
   paths - ordinal comparison ignoring case, with the case-sensitive ordinal comparison breaking a
   tie - and a file already reported for an earlier root is not reported again. Ignoring case
   deliberately matches the alphabetical listing of the case-insensitive file systems of Windows
   and macOS (the order the previous, unsorted catalog scan saw there, so `arial.ttf` still precedes
   `ARIALN.TTF`), while the tie-break makes the order total and identical on every platform.
3. **Read.** Each file is opened once (`FileShare.ReadWrite | FileShare.Delete`) and every face is
   read through the internal `SfntHeaderReader` (face count, then per-face names and style); a face
   or file that does not parse is skipped. The family name falls back to the file name without its
   extension when the face has no usable name.

A "link" is the file-system *reparse-point attribute*, deliberately and not `FileSystemInfo.LinkTarget`:
the attribute also covers reparse points `LinkTarget` does not report, so no redirection can reopen a
cycle. The sandboxed data-file resolvers of other packages use a different predicate for a different
purpose and are not related to this one.

##### FontScanLimits properties

Each setter validates its value and throws `ArgumentOutOfRangeException` for less than one.

#### Error Handling

`Scan` does not fail for anything about the file system: a missing, unreadable, malformed, or
oversized input is skipped or bounded (catching `IOException`, `UnauthorizedAccessException`,
`ArgumentException`, `NotSupportedException`, `SecurityException`, `InvalidDataException` and
`OverflowException` at the narrowest scope - one entry, one directory listing, one file, or one
face). It throws `ArgumentNullException` for a `null` directory list and
`OperationCanceledException` when the token is canceled (checked per directory, entry, file and
face). An empty result is a valid outcome.

#### Dependencies

`FontDirectoryScanner` depends on the internal `SfntHeaderReader` (and through it `SfntContainer`,
`NameTable`, `StyleTable`) and on `SystemFontInfo`, all within the `Fonts` subsystem, and on
`System.IO` only. It does not depend on `TrueTypeFont` and never fully loads a font.

#### Callers

- `SystemFontCatalog` builds `Fonts` from it (`BuildCatalogFromRoots`, with `trustLinkedFiles: true`
  and generous limits, since the host's own font directories commonly use links and the scan exists
  only to bound a pathological tree).
- A library user may call it directly to enumerate the fonts of a directory of their own.

#### Thread Safety

`Scan` keeps all of its state in locals and only reads the file system, so concurrent calls are safe;
`FontScanLimits` is immutable.
