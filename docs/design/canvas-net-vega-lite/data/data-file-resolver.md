### DataFileResolver

![Data Structure](VegaLiteDataView.svg)

The `DataFileResolver` unit is the third software unit in the `Data` subsystem. It is one source file in the
`DemaConsulting.CanvasNet.VegaLite.Data` namespace, `DataFileResolver.cs`, holding the `internal` `DataFileResolver`
class and the `DataFile` record. It is a thin layer over the core `FileReferenceResolver` (see _FileReferenceResolver
Unit Design_, `canvas-net/files/file-reference-resolver.md`).

#### Purpose

`DataFileResolver` finds and reads the local data files that a chart names with a local `data.url` (relative, absolute,
a drive path, or the local path of a `file:` URI without a host), and is the only code of the system that touches the file
system or calls the caller's file callback - always through the core `FileReferenceResolver`, under the file-access
policy of `VegaLiteOpenOptions.FileAccess` (`FileAccessPolicy.AllowAll` by default, like Pandoc). It never reaches the
network: `DataLoader` classifies a remote URL before it asks for anything, and the core engine itself rejects network,
device and alternate-data-stream paths under every policy.

The file-access mechanics - validating the path text, asking the policy about the real path of the open file, the
regular-file check, opening and reading with a byte and a file bound, the resolver-callback mode and the per-call
cache - are implemented once, in the core `Files` subsystem, and are verified there. This unit keeps only what is
specific to Vega-Lite: the search order, strict UTF-8 decoding, and the translation of every core refusal into a
located `VegaLiteRenderException` with Vega-Lite wording.

The unit's behavior is specified by the `CanvasNetVegaLite-Data-DataFileResolver-*` requirements (`Resolution`,
`FileAccess`, `Links`, `ResolverCallback`, `MissingFiles`, `Cache`, `FileLimit`, `ByteLimit`, `StrictUtf8`, and
`Cancellation`) in `docs/reqstream/canvas-net-vega-lite/data/data-file-resolver.yaml`.

#### Data Model

**`DataFile`** (sealed record) is a file that was found and read: `Key` (its identity: the normalized full path, or the
normalized logical path in resolver mode), `Display` (the `url` as the specification wrote it, with forward slashes, for
messages), and `Text` (the decoded text, without the byte-order mark).

**`DataFileResolver`** is created for one measure or render call with the effective `VegaLiteOpenOptions`, the
`VegaLiteLimits` and a cancellation token. It creates one `FileReferenceResolver` from them (`BaseDirectory` as the base
directory, `DataPaths` as the search paths, `FileResolver` as the callback, `FileAccess` as the policy, `MaxDataBytes`
as the byte limit, `MaxDataFiles` as the file limit) and holds
the decoded files by key. It is not thread-safe and runs on the calling thread only.
`HasFileSource` says whether the engine has anything to look in.

**Invariants**: a file is requested from the engine at most once per instance (hit or miss - the engine caches; a
denial is asked again) and decoded at most once; no more than `MaxDataFiles` distinct files are read (the engine
refuses the next one before it is opened or the callback is asked for it, whatever the policy) and none longer than
`MaxDataBytes` bytes; no byte of a file is read before the policy allowed it; the text of a returned file is valid
UTF-8.

#### Key Methods

- `Load(path, location, written)` - the entry point; `written` is the `url` as the specification wrote it when it
  differs from `path` (a `file:` URI whose local path `DataLoader` passes), used in messages. Fails if no file source is
  configured; asks the engine to resolve the path (an absolute path names one file; a relative one is looked up in the
  base directory first, then in each data path in declaration order, or in resolver mode under the logical root and
  then each prefix, where an absolute path is "absolute paths have no meaning for the file source"); fails with "not
  found" when the engine returns nothing (a dangling link included); answers a file reached before (under any spelling)
  from its cache; otherwise decodes the file and remembers it. The file limit is enforced by the engine (`MaxFiles`),
  before the file beyond it is read.
- `Translate` - turns a `FileReferenceException` into the located `VegaLiteRenderException`, with the exception as the
  inner exception, by its kind (all eleven kinds): "is not allowed" (the empty, control-character, invalid and network
  path kinds, with the reason; on Linux and macOS a drive path is an invalid path), "was denied by the file-access
  policy '<name>': <reason>" (`AccessDenied`), "cannot be checked: its real path cannot be determined on this system, so
  the file-access policy '<name>' cannot be applied" (`RealPathUnavailable`), "is not a regular file", "is larger than the
  limit of ... bytes (MaxDataBytes)", "reads more than the limit of ... data files (MaxDataFiles)", "is not a valid path"
  (a configured directory), and "cannot be read" for everything else. The messages name the `url` as the specification
  wrote it, never a host directory, and the callback's own message is not copied.
- `Decode` - removes a UTF-8 byte-order mark and decodes strictly (invalid bytes, UTF-16 text, and encoded surrogates
  are errors, never U+FFFD).
- `Printable` - replaces control characters so the path in the "no file source" message can be shown.

**Preconditions and postconditions**: `path` is the local path of a `url` that `DataLoader` classified as not remote;
on return the file exists, is a regular file the policy allowed, is within the limits, and is decoded text, or an
exception was thrown and nothing was returned. A call never changes the files it reads.

**Residual risk.** The policy is asked about the real path of the open handle and the bytes are read from that same
handle, so a directory swapped for a link between a check and the open cannot redirect the read. Devices and named
pipes are refused as not regular files. Hard links remain undetectable: a hard link is the file itself and has no
marker; a host that confines data files must keep hard links to outside files out of its roots.

#### Error Handling

Every failure is a `VegaLiteRenderException` carrying the location of the `url` (never an empty result): no file source
is configured, the path is never allowed ("is not allowed"), the file-access policy denies the file or cannot be
applied, the file is not found, it is not a regular file, it cannot be read (an existing
directory where a file is expected, an existing file whose open fails, which is never reported as missing, a stream
that fails while being read, a callback that throws, or a callback stream whose disposal throws), it is not
valid UTF-8, it is larger than `MaxDataBytes`, or it would be the file after `MaxDataFiles`; the limit messages
contain "the limit of ..." so that `CanvasNetTool` maps them to `budget-exceeded`. A configured directory that is not a
valid path is also a located failure. The callback is untrusted caller code at a render boundary, so its failures are
normalized by the engine to a `CannotRead` failure and reported as the located "cannot be read" failure. A canceled
token is an `OperationCanceledException`, never wrapped (also when the callback raises it), thrown before any request or
file access and between files and chunks.

**Behavior note.** Moving from the core `SandboxedFileResolver` to `FileReferenceResolver` changed the default: data
files are no longer confined to the base directory and data paths, `..`, absolute paths, local `file:` URIs and links
are followed under `AllowAll`, and a host restores the confinement with
`FileAccessPolicy.Combine(FileAccessPolicy.UnderRoots(...), FileAccessPolicy.NoSymlinks)`. A dangling link is now a
missing file; a control character is still `char.IsControl` (C0, DEL and C1).

#### Dependencies

- **`FileReferenceResolver`**, **`FileReferenceOptions`**, **`FileReference`**, **`FileReferenceException`**,
  **`FileReferenceFailureKind`**, and **`FileAccessPolicy`** - the public types of the core `Files` subsystem
- **`VegaLiteOpenOptions`**, **`VegaLiteLimits`**, **`VegaLiteLocation`**, and **`VegaLiteRenderException`** - the
  public types of the `VegaLiteDocument` unit
- **.NET base class library** - `System.Text` (strict UTF-8) and `System.Collections.Generic`

#### Callers

`DataLoader` creates one resolver per call (through its constructor) and calls `Load`. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/DataFileResolverTests.cs` and `DataFileResolverLinkTests.cs` call it
directly through `InternalsVisibleTo`, with the helpers of `DataTestSupport.cs`.
