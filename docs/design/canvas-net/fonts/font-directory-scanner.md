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

1. **Collect, in traversal order.** An empty, `null`, missing, or malformed root contributes nothing.
   Unless `trustLinkedFiles` is `true`, a root that is a link is skipped. The tree is walked depth
   first with an explicit stack. For each directory: all of its entries are listed with
   `DirectoryInfo.EnumerateFileSystemInfos` (at most as many as the entry budget still allows) and
   classified - a subdirectory that is a link is never followed (in either mode), a font file
   (`.ttf`, `.otf`, `.ttc`, `.otc`, case-insensitive) is accepted unless it is a link and
   `trustLinkedFiles` is `false`; then the font files are sorted and added to the result **first**,
   and the subdirectories, also sorted, are pushed so that the first is descended into next. A
   directory's own font files therefore precede those of its subdirectories (as in the earlier
   catalog scan), and no re-sorting of the whole result follows. An unreadable directory
   contributes nothing.
2. **Order and bounds.** *Path order* is ordinal comparison ignoring case, with the case-sensitive
   ordinal comparison breaking a tie. Ignoring case deliberately matches the alphabetical listing
   of the case-insensitive file systems of Windows and macOS (the order the previous catalog scan
   saw there, so `arial.ttf` still precedes `ARIALN.TTF`), while the tie-break makes the order
   total and identical on every platform. The bounds are applied in this traversal order and each
   stops the collection at its first violation: the first directory past `MaxDirectories`, the
   first font file past `MaxFontFiles`. A directory's entries are counted against `MaxEntries` as
   a whole: when the remaining budget cannot cover them, none of that directory's entries is used
   and the collection stops, so what was kept is always made of complete directories and never
   depends on the order the file system lists entries in. A file already accepted for an earlier
   (or repeated) root is skipped *during* collection, so only unique files count against
   `MaxFontFiles`. Once a bound has stopped the collection or the face reading, the scan ends after
   the current root's collected files are read, and the remaining roots are not even enumerated.
3. **Read.** The collected files are read in traversal order. Each is opened once
   (`FileShare.ReadWrite | FileShare.Delete`) and every face is read through the internal
   `SfntHeaderReader`: the face count first, then one face at a time (for a collection only the
   one entry of the face offset table is read for each face, never the whole table). A declared face
   count that cannot fit in the file (`12 + 4 * count` bytes) makes the file unparseable, so a crafted
   header cannot make the scan size or read anything from a huge count; the reading stops at the first
   face past `MaxFaces`. A face or file that does not parse is skipped. The family name falls back
   to the file name without its extension when the face has no usable name.

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
