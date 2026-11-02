### DataFileResolver

![Data Structure](VegaLiteDataView.svg)

The `DataFileResolver` unit is the third software unit in the `Data` subsystem. It is one source file in the
`DemaConsulting.CanvasNet.VegaLite.Data` namespace, `DataFileResolver.cs`, holding the `internal` `DataFileResolver`
class and the `DataFile` record. It is a thin layer over the core `SandboxedFileResolver` (see _SandboxedFileResolver
Unit Design_, `canvas-net/files/sandboxed-file-resolver.md`).

#### Purpose

`DataFileResolver` finds and reads the local data files that a chart names with a relative `data.url`, and is the only
code of the system that touches the file system or calls the caller's file callback - always through the core sandbox.
It never reaches the network: `DataLoader` classifies a remote URL before it asks for anything, and the sandbox itself
rejects the path forms that would escape a root.

The sandbox mechanics - validating the path text, confining each candidate to its root, refusing symbolic links and
junctions, opening and reading with a byte bound, the resolver-callback mode and the per-call cache - are implemented
once, in the core `Files` subsystem, and are verified there. This unit keeps only what is specific to Vega-Lite: the
search order, the distinct-file limit, strict UTF-8 decoding, and the translation of every sandbox refusal into a
located `VegaLiteRenderException` with Vega-Lite wording.

The unit's behavior is specified by the `CanvasNetVegaLite-Data-DataFileResolver-*` requirements (`Resolution`,
`Sandbox`, `Links`, `ResolverCallback`, `MissingFiles`, `Cache`, `FileLimit`, `ByteLimit`, `StrictUtf8`, and
`Cancellation`) in `docs/reqstream/canvas-net-vega-lite/data/data-file-resolver.yaml`.

#### Data Model

**`DataFile`** (sealed record) is a file that was found and read: `Key` (its identity: the normalized full path, or the
normalized logical path in resolver mode), `Display` (the path as the specification wrote it, with forward slashes, for
messages), and `Text` (the decoded text, without the byte-order mark).

**`DataFileResolver`** is created for one measure or render call with the effective `VegaLiteOpenOptions`, the
`VegaLiteLimits` and a cancellation token. It creates one `SandboxedFileResolver` from them (`BaseDirectory` as the base
directory, `DataPaths` as the search paths, `FileResolver` as the callback, `MaxDataBytes` as the byte limit,
`MaxDataFiles` as the file limit) and holds
the decoded files by key. It is not thread-safe and runs on the calling thread only.
`HasFileSource` says whether the sandbox has anything to look in.

**Invariants**: a file is requested from the sandbox at most once per instance (hit or miss - the sandbox caches) and
decoded at most once; no more than `MaxDataFiles` distinct files are read (the sandbox refuses the next one before it is
opened or the callback is asked for it) and none longer than `MaxDataBytes` bytes; the
text of a returned file is valid UTF-8.

#### Key Methods

- `Load(path, location)` - the entry point. Fails if no file source is configured; asks the sandbox to resolve the path
  (the base directory first, then each data path in declaration order, or in resolver mode the logical root and then
  each prefix); fails with "not found" when the sandbox returns nothing; answers a file reached before (under any
  spelling) from its cache; otherwise decodes the file and remembers it. The file limit is enforced by the sandbox
  (`MaxFiles`), before the file beyond it is read.
- `Translate` - turns a `SandboxException` into the located `VegaLiteRenderException` by its kind: "is not allowed" (the
  four path-text kinds, with the reason), "is outside the allowed directories", "passes through a link", "cannot be
  checked for links", "is larger than the limit of ... bytes (MaxDataBytes)", "reads more than the limit of ... data
  files (MaxDataFiles)", "is not a valid path" (a configured
  directory), and "cannot be read" for everything else. The messages name the path as the specification wrote it,
  never a
  host directory, and the callback's own message is not copied.
- `Decode` - removes a UTF-8 byte-order mark and decodes strictly (invalid bytes, UTF-16 text, and encoded surrogates
  are errors, never U+FFFD).
- `Printable` - replaces control characters so the path in the "no file source" message can be shown.

**Preconditions and postconditions**: `path` is the text of a `url` that `DataLoader` classified as not remote; on return
the file exists, is not behind a link, is within the limits, and is decoded text, or an exception was thrown and
nothing was returned. A call never changes the files it reads.

**Residual risk.** The sandbox's link check and its open are two steps; a writer that can change the tree between them
could swap a checked component for a link. The sandbox does not defend against this and says so: its protection is
against path-based escapes in a static tree, not against a concurrent local attacker with write access to the data
folders. Hard links and special files (named pipes, devices) are not recognized either; a caller who cannot trust a
folder must not use it as a data root.

#### Error Handling

Every failure is a `VegaLiteRenderException` carrying the location of the `url` (never an empty result): no file source
is configured, the path text is refused or leaves its root ("is not allowed" or "is outside the allowed
directories"), a component is a link or the link check fails, the file is not found, it cannot be read (an existing
directory where a file is expected, an existing file whose open fails, which is never reported as missing, a stream
that fails while being read, a callback that throws, or a callback stream whose disposal throws), it is not
valid UTF-8, it is larger than `MaxDataBytes`, or it would be the file after `MaxDataFiles`; the limit messages
contain "the limit of ..." so that `CanvasNetTool` maps them to `budget-exceeded`. A configured directory that is not a
valid path is also a located failure. The callback is untrusted caller code at a render boundary, so its failures are
normalized by the sandbox to a `CannotRead` failure and reported as the located "cannot be read" failure. A canceled
token is an `OperationCanceledException`, never wrapped (also when the callback raises it), thrown before any request or
file access and between files and chunks.

**Behavior note.** A dangling final link is a link refusal and a control character is `char.IsControl` (C0, DEL and C1);
these are the hardened behaviors of the shared sandbox, which this unit had before it moved there.

#### Dependencies

- **`SandboxedFileResolver`**, **`SandboxOptions`**, **`SandboxedFile`**, **`SandboxException`**, and
  **`SandboxFailureKind`** - the public types of the core `Files` subsystem
- **`VegaLiteOpenOptions`**, **`VegaLiteLimits`**, **`VegaLiteLocation`**, and **`VegaLiteRenderException`** - the
  public types of the `VegaLiteDocument` unit
- **.NET base class library** - `System.Text` (strict UTF-8) and `System.Collections.Generic`

#### Callers

`DataLoader` creates one resolver per call (through its constructor) and calls `Load`. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/DataFileResolverTests.cs` and `DataFileResolverLinkTests.cs` call it
directly through `InternalsVisibleTo`, with the helpers of `DataTestSupport.cs`.
