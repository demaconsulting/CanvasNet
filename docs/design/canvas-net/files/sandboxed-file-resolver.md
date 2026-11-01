### SandboxedFileResolver

![Files Structure](FilesView.svg)

The `SandboxedFileResolver` unit is the only software unit of the `Files` subsystem. It comprises six source files in
the `DemaConsulting.CanvasNet.Files` namespace, all `public`:

- `SandboxedFileResolver.cs` - `SandboxedFileResolver` (sealed partial class): the public entry point, the search, the
  open and bounded read, the resolver-callback boundary, and the per-instance cache
- `SandboxedFileResolver.Paths.cs` - the path-text rules, candidate generation, root confinement, the link walk, and the
  roots of file-system and resolver mode
- `SandboxOptions.cs` - `SandboxOptions` (sealed record): base directory, search paths, resolver callback, and byte limit
- `SandboxedFile.cs` - `SandboxedFile` (sealed class): the result
- `SandboxException.cs` - `SandboxException` (sealed class): the failure
- `SandboxFailureKind.cs` - `SandboxFailureKind` (enum): the reasons for a failure

#### Purpose

`SandboxedFileResolver` turns an untrusted relative path into the bytes of a file that is guaranteed to lie inside
caller-chosen directories (or a caller-supplied virtual file source), and refuses everything else. A program that reads
files named by untrusted text (an include directive, a data URL) needs this once, correctly: path validation,
confinement to a root, refusal of symbolic links and junctions, an open that does not race against a separate existence
check, a bounded read, and a failure model a caller can map to its own error type. The unit is public so that library
users get exactly the rules the CanvasNet packages use; the `CanvasNetVegaLite` and `CanvasNetScad` packages
use it for every local file.

The unit's behavior is specified by the `CanvasNet-Files-SandboxedFileResolver-*` requirements (`Resolution`,
`MissingFiles`, `PathText`, `Confinement`, `Links`, `ResolverCallback`, `Reading`, `SizeLimit`, `FileLimit`, `Cache`,
`Cancellation`, and `Failures`) in `docs/reqstream/canvas-net/files/sandboxed-file-resolver.yaml`.

#### Data Model

**`SandboxOptions`** (immutable record): `BaseDirectory` (trusted directory searched first; ignored in callback mode),
`SearchPaths` (additional trusted roots, searched in order; copied when the resolver is created, so a later change to the
caller's list has no effect), `FileResolver` (`Func<string, Stream?>`, a callback that
replaces the file system), `MaxFileBytes` (default 16 MiB, must be positive), and `MaxFiles` (optional, default none: the
number of distinct files one resolver may read; must not be negative).

**`SandboxedFile`** (immutable): `Key` (the identity of the file: its normalized full path, or its normalized logical
path in callback mode; compared under the resolver's `KeyComparer`), `Directory` (the file's directory), `Root` (the
root the file is confined to), and `Content` (a `ReadOnlyMemory<byte>`; decoding is the caller's job, because the
caller knows the expected encoding). Passing a `SandboxedFile` back to `Resolve` as `relativeTo` (of the resolver that
returned it, or one with the same roots) resolves a path relative
to that file's directory, confined to its root.

**`SandboxException`** (immutable): `Kind` (`SandboxFailureKind`), `Path` (the path text for the path-text kinds and
`OutsideRoot`, the configured directory for `InvalidRoot`, otherwise the file's key), `Detail` (the reason phrase of a
path-text failure, the root-relative link path of a link failure, otherwise `null`), and the underlying cause as the
inner exception. The message is an English sentence that never contains a host directory.

**`SandboxFailureKind`**: `EmptyPath`, `ControlCharacter`, `ColonInPath`, `AbsolutePath` (the path text itself),
`OutsideRoot`, `ThroughLink`, `LinkCheckFailed`, `CannotRead`, `TooLarge`, `InvalidRoot`, and `TooManyFiles`.

**`SandboxedFileResolver`** is created for one logical operation with the options and a cancellation token. It holds the
cache of the files found by root and candidate key (a `null` remembers a candidate that does not exist; the root is part
of the identity because the link checks run on the components below a candidate's own root, so a file reached through one
root - for example a trusted search path that is itself a link - must never answer for another root), the keys of the
distinct files read (for `MaxFiles`), the result of each link
probe by full path, and the roots, which are computed on first use. **It is not thread-safe**: its caches are
unsynchronized and the callback is invoked on the calling thread; use one instance per operation or serialize access.
The options and results are immutable and may be shared. An internal constructor accepts a link probe
(`Func<string, bool>`) and a file opener (`Func<string, Stream>`) as test seams, so the link and open rules are proven
on every platform without any link privilege or file permission; they are not public options.

**Modes.** _File-system mode_ (no callback): the roots are the full path of `BaseDirectory` and of each `SearchPaths`
entry. _Callback mode_ (`FileResolver` set): the file system is never touched; the logical root is the empty path, and
each search path that is itself a valid relative logical path is a logical directory prefix; the base directory plays no
role. `HasSource` says whether either mode has anything to look in.

**Path text rules.** Backslashes become forward slashes. A path that is empty or white space, contains a control
character (`char.IsControl`: C0, DEL, and C1, including NUL) or a colon (drive letters and alternate data streams), or
starts with a slash (absolute and UNC paths) is refused before anything is touched. Combining the text with a directory
normalizes `.` and `..`; a result that is not under the root (ordinal comparison, case-insensitive on Windows and macOS)
is refused with `OutsideRoot`, while `..` that stays inside the root is fine.

**Invariants**: a candidate (root plus key) is looked up at most once per instance (hit or miss); every component
below a
root is checked for a link before the file is opened, for every root the file is reached through; no more than
`MaxFileBytes` bytes are read of any file; with `MaxFiles` set, no more than that many distinct files are read and
a new file beyond it is never opened or requested; every failure that is not
a cancellation or an argument error is a `SandboxException`.

#### Key Methods

- `Resolve(path, relativeTo)` - the entry point. Validates the path text; walks the candidates (the directory of
  `relativeTo` with its root, or else the base directory, or in callback mode the logical root, and then each search
  path in declaration order), loading each through the cache and returning the first file that exists, or `null` when
  none does (and when no source is configured). A null `path` is an `ArgumentNullException`. A `relativeTo` file is first
  checked by `ValidateRelativeTo`: its root must equal one of this resolver's own roots and its directory must lie under
  that root, otherwise it is an `ArgumentException` (a `SandboxedFile` cannot be created outside the assembly, so a file
  from another resolver with wider roots is the only way to violate this, and it is refused).
- `LoadCandidate` - answers from the cache (keyed by root and candidate key); otherwise, when `MaxFiles` is set and that
  many distinct files were already read, refuses a candidate whose key was not read before with `TooManyFiles` **before
  anything is opened or requested** (so a file beyond the limit is never touched, and a file already read through another
  root does not count again); otherwise reads the bytes, remembers the result, and counts the file when it exists.
- `ReadBytes` - in callback mode delegates to `ReadFromCallback`; in file-system mode first runs the link walk (which
  stops a candidate whose directory is missing but leaves the final component to the open), treats an existing
  directory where the file is expected as `CannotRead` (not a missing file), then attempts to open the file read-only
  with `FileShare.ReadWrite | FileShare.Delete`. **The open attempt, not an existence probe, decides missing versus
  unreadable**: `File.Exists` answers `false` for an existing file the process cannot access, so gating on it would report
  an inaccessible file as "not found". Only a `FileNotFoundException` or `DirectoryNotFoundException` from the open (a file
  that is missing, or that vanished since the walk) makes the candidate missing and lets the search continue; any other
  `IOException` or `UnauthorizedAccessException` of the open is `CannotRead` and ends the search.
- `ReadFromCallback` - calls the callback with the normalized logical path, reads the stream it returns, and disposes it
  exactly once, all inside one boundary: a failure of the callback, of the bounded read, or of the disposal - **any**
  exception other than a cancellation, including a `SandboxException` that the callback or its stream throws itself - is
  `CannotRead` with the cause kept as the inner exception, so untrusted code can neither leak a raw exception nor forge
  another failure kind. The size failure is raised by the resolver outside that boundary.
- `ReadBounded` - checks a size the stream reports, then reads in 8 KiB chunks and stops as soon as the running length
  exceeds `MaxFileBytes` (returning no bytes, which the caller reports as `TooLarge`), so a stream that never ends stops
  one chunk past the limit; the token is observed after every chunk. Failures of the stream are left to the caller.
- `WalkRejectingLinks` and `ProbeLink` - walk the candidate top-down from the root, probing each existing component once
  (the first missing component above the file ends the walk and nothing below it is probed; the final component is always
  probed, so a dangling link is refused as a link, while a file that is not a link is left to the open attempt); a
  symbolic link or junction anywhere below the root is `ThroughLink`, whatever its target; the root and everything above
  it are trusted; a probe that throws rejects the file (`LinkCheckFailed`, fail closed).
- `DefaultIsLink` - a component is a link when its `LinkTarget` is not null (symbolic links and junctions, for files and
  directories, existing or dangling); other reparse points that redirect nothing, such as the placeholders of a
  synchronized folder, are not rejected.
- `ValidatePathText`, `TryDescribeInvalidPath`, `Candidates`, `Combine`, `CombineFileSystem`, `CombineLogical`, `IsUnder`,
  `EnsureRoots`, `FullRoot`, and `NormalizePrefix` - the path rules above.
- `KeyComparer` - the comparer under which two keys name the same file: ordinal for logical paths, the platform's path
  comparison for full paths. It is exposed so that callers can detect that two spellings reached one file (for example an
  include cycle) without re-deriving the platform rule.

**Preconditions and postconditions**: `path` is untrusted text; on return the file exists, is not behind a link, is within
the size limit, and its bytes are in `Content`, or `null` was returned because no candidate exists, or an exception was
thrown and nothing was read through a link. A call never changes the files it reads.

**Residual risk.** The link check and the open are two steps; a writer that can change the tree between them could
swap a
checked component for a link. The unit does not defend against this and says so: its protection is against path-based
escapes in a static tree, not against a concurrent local attacker with write access to the directories. Hard links and
special files (named pipes, devices) are not recognized either; a caller who cannot trust a directory must not use it
as a
root. Strict refusal of links was chosen over resolving real paths because resolution is ambiguous (relative links,
chains,
junction targets) and racy; a caller who wants to use linked content adds the real directory as a search path.

#### Error Handling

Every failure is a `SandboxException` (never an empty result for a refusal): the path text is refused (`EmptyPath`,
`ControlCharacter`, `ColonInPath`, `AbsolutePath`) or leaves its root (`OutsideRoot`), a configured directory is not a
valid path (`InvalidRoot`), a component is a link (`ThroughLink`) or the link check fails (`LinkCheckFailed`), the file
exists but cannot be read - a directory where a file is expected, an open that fails with an `IOException` or
`UnauthorizedAccessException`, a stream that fails while being read, a callback that throws, or a callback stream whose
disposal throws (`CannotRead`) - or the file is longer than `MaxFileBytes` (`TooLarge`), or reading a new file would
exceed `MaxFiles` (`TooManyFiles`). A file that does not exist is not
a failure: `Resolve` returns `null`. The callback is untrusted caller code, so its failures are normalized to
`CannotRead` and its own message is not copied into the exception message (the cause is kept as the inner exception for
trusted diagnostics). A canceled token is an `OperationCanceledException`, never wrapped (also when the callback raises
it), thrown before any request or file access and between files and chunks. Invalid construction arguments are
`ArgumentNullException` / `ArgumentOutOfRangeException`.

**Design decision: file sharing.** Files are opened read-only with `FileShare.ReadWrite | FileShare.Delete`. The unit
reads a bounded snapshot and never writes, so it is the least restrictive reader: another process that is writing,
replacing, renaming, or deleting the file (an editor saving a file while it is read) does not turn the read into a sharing
violation. The trade-off is that the bytes read may be a mix of two versions of a file that is being rewritten, which is
the same exposure as the documented check/use race and is not defended against.

#### Dependencies

- **.NET base class library** - `System.IO` (`File`, `Directory`, `FileInfo.LinkTarget`, `DirectoryInfo.LinkTarget`,
  `FileStream`, `Path`) and `System.Collections.Generic`. The unit depends on no other CanvasNet software item.

#### Callers

`SandboxedFileResolver` is a public API entry point for consumers of the CanvasNet package. It is called by the
`DataFileResolver` unit of `CanvasNetVegaLite` (one resolver per measure or render call) and by the `ScadFileResolver`
unit of `CanvasNetScad` (one resolver per `Load`), both through the public API. The tests in
`test/DemaConsulting.CanvasNet.Tests/Files/` call it directly, and reach the internal link-probe and open seams through
the core assembly's `InternalsVisibleTo` grant to the test project.
