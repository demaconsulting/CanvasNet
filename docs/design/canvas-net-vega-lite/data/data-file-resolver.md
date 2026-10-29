### DataFileResolver

![Data Structure](VegaLiteDataView.svg)

<!-- cspell:ignore sandboxed sandboxing junction junctions symlink symlinks -->

The `DataFileResolver` unit is the third software unit in the `Data` subsystem. It comprises two source files in the
`DemaConsulting.CanvasNet.VegaLite.Data` namespace, both `internal`:

- `DataFileResolver.cs` - `DataFileResolver` (sealed partial class) and the `DataFile` record: the public face of the
  unit, the search, the bounded read, the strict UTF-8 decoding, the per-call cache, and the data-file limits
- `DataFileResolver.Paths.cs` - the path text rules, candidate generation, root confinement, the link walk, and the
  roots of file-system and resolver mode

#### Purpose

`DataFileResolver` finds and reads the local data files that a chart names with a relative `data.url`, and is the only
code of the system that touches the file system or calls the caller's file callback. It confines that access to a
sandbox, because a specification and its data may come from an untrusted source, and it never reaches the network:
`DataLoader` classifies a remote URL before it asks for anything, and the resolver itself rejects the path forms that
would escape a root. It is a separate implementation of the path rules that the other file resolvers of the repository
follow, because `CanvasNetVegaLite` must not reference another document system (see _CanvasNetVegaLite System Design_,
Dependencies).

The unit's behavior is specified by the `CanvasNetVegaLite-Data-DataFileResolver-*` requirements (`Resolution`,
`Sandbox`, `Links`, `ResolverCallback`, `MissingFiles`, `Cache`, `FileLimit`, `ByteLimit`, `StrictUtf8`, and
`Cancellation`) in `docs/reqstream/canvas-net-vega-lite/data/data-file-resolver.yaml`.

#### Data Model

**`DataFile`** (sealed record) is a file that was found and read: `Key` (its identity: the normalized full path, or the
normalized logical path in resolver mode), `Display` (the path as the specification wrote it, with forward slashes, for
messages), and `Text` (the decoded text, without the byte-order mark).

**`DataFileResolver`** is created for one measure or render call with the effective `VegaLiteOpenOptions`, the
`VegaLiteLimits`, a cancellation token, and an optional link probe (`Func<string, bool>`, a seam for tests; the
default asks the file system). It holds the cache of the files found by candidate key (a `null` remembers a candidate
that does not exist), the result of each link probe by full path, the number of files read, and the roots, which are
computed on first use. It is not thread-safe and runs on the calling thread only.

**Modes.** _File-system mode_ (no callback): the roots are the full path of `BaseDirectory` and of each `DataPaths`
entry. _Resolver mode_ (`FileResolver` set): the file system is never touched; the logical root is the empty path, and
each data path that is itself a valid relative logical path is a logical directory prefix; the base directory plays no
role. `HasFileSource` says whether either mode has anything to look in.

**Path text rules.** Backslashes become forward slashes. A path that is empty or white space, contains a control
character (including NUL) or a colon (drive letters and alternate data streams), or starts with a slash (absolute and
UNC paths) is refused as a sandbox violation. Combining the text with a root normalizes `.` and `..`; a result that is
not under the root (ordinal comparison, case-insensitive on Windows and macOS) is refused, while `..` that stays inside
the root is fine.

**Invariants**: a file is read at most once per instance (hit or miss); every component below a root is checked for a
link before the file is opened; no more than `MaxDataFiles` distinct files are read and none longer than
`MaxDataBytes` bytes; the text of a returned file is valid UTF-8.

#### Key Methods

- `Load(path, location)` - the entry point. Fails if no file source is configured; validates the path text; walks the
  candidates (the base directory first, then each data path in declaration order, or in resolver mode the logical root
  and then each prefix), loading each through the cache and returning the first file that exists; fails with "not
  found" when none does.
- `LoadCandidate` - answers from the cache, otherwise reads the bytes, decodes them, and remembers the result.
- `ReadBytes` - in resolver mode calls the callback with the normalized logical path, reads the stream it returns, and
  disposes it; in file-system mode first runs the link walk and `File.Exists`, then opens the file for reading with
  sharing, and treats a file that vanished between the check and the open as missing.
- `ReadBounded` - counts the file against `MaxDataFiles` as soon as it is known to exist and before any byte is read,
  checks a size the stream reports, then reads in 8 KiB chunks and stops as soon as the running length exceeds
  `MaxDataBytes`, so a stream that never ends stops one chunk past the limit; the token is observed after every chunk.
- `Decode` - removes a UTF-8 byte-order mark and decodes strictly (invalid bytes, UTF-16 text, and encoded surrogates
  are errors, never U+FFFD).
- `WalkRejectingLinks` and `ProbeLink` - walk the candidate top-down from the root, probing each existing component once
  (the first missing component ends the walk and nothing below it is probed); a symbolic link or junction anywhere
  below the root is an error, whatever its target; the root and everything above it are trusted; a probe that throws
  rejects the file (fail closed).
- `DefaultIsLink` - a component is a link when its `LinkTarget` is not null (symbolic links and junctions, for files and
  directories); other reparse points that redirect nothing, such as the placeholders of a synchronized folder, are not
  rejected.
- `ValidatePathText`, `Candidates`, `Combine`, `CombineFileSystem`, `CombineLogical`, `IsUnder`, `EnsureRoots`, and
  `NormalizePrefix` - the path rules above.

**Preconditions and postconditions**: `path` is the text of a `url` that `DataLoader` classified as not remote; on return
the file exists, is not behind a link, is within the limits, and is decoded text, or an exception was thrown and
nothing was returned. A call never changes the files it reads.

**Residual risk.** The link check and the open are two steps; a writer that can change the tree between them could
swap a checked component for a link. The unit does not defend against this and says so: its protection is against
path-based escapes in a static tree, not against a concurrent local attacker with write access to the data folders.
Hard links and special files (named pipes, devices) are not recognized either; a caller who cannot trust a folder must
not use it as a data root.

#### Error Handling

Every failure is a `VegaLiteRenderException` carrying the location of the `url` (never an empty result): no file source
is configured, the path text is refused or leaves its root ("is not allowed" or "is outside the allowed
directories"), a component is a link or the link probe fails, the file is not found (also when a directory has the
name), it cannot be read (`IOException`, `UnauthorizedAccessException`, a stream that fails while being read), it is not
valid UTF-8, it is larger than `MaxDataBytes`, or it would be the file after `MaxDataFiles`; the limit messages
contain "the limit of ..." so that `CanvasNetTool` maps them to `budget-exceeded`. A configured directory that is not a
valid path is also a located failure. An exception thrown by the caller's callback is the caller's code and propagates
unchanged. A canceled token is an `OperationCanceledException`, never wrapped, thrown before any request or file access
and between files and chunks.

#### Dependencies

- **`VegaLiteOpenOptions`**, **`VegaLiteLimits`**, **`VegaLiteLocation`**, and **`VegaLiteRenderException`** - the
  public types of the `VegaLiteDocument` unit
- **.NET base class library** - `System.IO` (`File`, `Directory`, `FileInfo.LinkTarget`, `FileStream`, `Path`),
  `System.Text` (strict UTF-8), and `System.Collections.Generic`

#### Callers

`DataLoader` creates one resolver per call (through its constructor) and calls `Load`. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/DataFileResolverTests.cs` and `DataFileResolverLinkTests.cs` call it
directly through `InternalsVisibleTo`, with the helpers of `DataTestSupport.cs`.
