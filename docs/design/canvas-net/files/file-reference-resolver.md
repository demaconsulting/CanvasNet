### FileReferenceResolver

![Files Structure](FilesView.svg)

The `FileReferenceResolver` unit is the engine that reads the files documents reference. It comprises six source files
in the `DemaConsulting.CanvasNet.Files` namespace, all `public`:

- `FileReferenceResolver.cs` - `FileReferenceResolver` (sealed partial class): the entry point, the per-candidate
  pipeline (pre-check, cache, file limit, open, inspect, policy, read), the callback boundary, and the shared bounded
  read
- `FileReferenceResolver.Paths.cs` - the text rules that hold under every policy, candidate generation, logical paths,
  and the configured directories
- `FileReferenceOptions.cs` - `FileReferenceOptions` (sealed record): base directory, referrer path, search paths,
  resolver callback, file-access policy, and limits
- `FileReference.cs` - `FileReference` (sealed class): the result
- `FileReferenceException.cs` - `FileReferenceException` (sealed class): the failure
- `FileReferenceFailureKind.cs` - `FileReferenceFailureKind` (enum): the reasons for a failure

#### Purpose

`FileReferenceResolver` turns a path that a document references into the bytes of a regular file after a
host-chosen `FileAccessPolicy` approved the file that was actually opened. It follows references the way Pandoc does -
relative paths, dot-dot segments, absolute paths, and symbolic links - and leaves every restriction to the policy,
except for the references that are never safe (network, device and alternate-data-stream paths, control characters,
and files that are not regular files; on Windows also reserved device names in any path component and a link from a
local path to a network share) and the resource limits, which hold under every policy. These refusals apply to the
files a document references; the top-level file a caller names (`ResolveTopLevel`) is opened as given, because the
threat they address is a document making the host reach a device or an attacker's share, not the operator's own
choice. The decision is made
about the open handle, so it cannot be raced by another process that changes the directory tree between a check and
the open.

The unit's behavior is specified by the `CanvasNet-Files-FileReferenceResolver-*` requirements (`Candidates`,
`PathText`, `MissingFiles`, `PolicyEvaluation`, `ChainOrigin`, `NetworkTarget`, `RaceFree`, `RealPathUnavailable`,
`RegularFile`, `Reading`, `Limits`, `Cache`, `Callback`, `Cancellation`, `Failures`, and `TopLevelFile`) in
`docs/reqstream/canvas-net/files/file-reference-resolver.yaml`.

#### Data Model

**`FileReferenceOptions`** (immutable record): `BaseDirectory` (where a relative reference of the top-level document
starts; a starting point, not a boundary), `ReferrerPath` (the top-level document, passed to the policy as the
referrer), `SearchPaths` (search locations, not roots; copied when the resolver is created), `FileResolver` (a
`Func<string, Stream?>` that replaces the file system), `FileAccess` (the policy; default `FileAccessPolicy.AllowAll`),
`MaxFileBytes` (default 16 MiB, positive), and `MaxFiles` (optional, not negative).

**`FileReference`** (immutable): `Key` (the full path as written, `.` and `..` collapsed, links not resolved; or the
logical path in callback mode), `RealPath` (the real path of the open handle, or `null` in callback mode and where the
platform cannot tell; the identity for cycle checks), `Directory` (the directory as written, where nested references
start), `Content` (`ReadOnlyMemory<byte>`), the internal `IsLogical` flag, and the internal `Origin` (the full path of
the host-chosen base directory or search path where the chain of references that reached the file started; `null` in
callback mode). A file reached again through another chain is the same instance and keeps the origin of the chain that
first read it; every origin is host-chosen, so either is trusted.

**`FileReferenceException`** (immutable): `Kind`, `Reference` (as written, control characters replaced by `?`),
`Reason` (the broken text rule or the policy's reason), `RequestedPath`, `RealPath`, `PolicyName`, `Hint`, and the
cause as the inner exception. `Message` is computed from the kind, the reference and the policy name only; it never
contains a host directory nor a policy reason, which may name host paths.

**`FileReferenceFailureKind`**: `EmptyPath`, `ControlCharacter`, `InvalidPath`, `NetworkPath` (the reference itself;
`NetworkPath` also for a Windows link from a local path to a share),
`AccessDenied`, `RealPathUnavailable`, `NotARegularFile`, `CannotRead`, `TooLarge`, `InvalidBaseDirectory`, and
`TooManyFiles`.

**`FileReferenceResolver`** is created for one operation with the options, a cancellation token and (internally) the
`FileOpenHooks` test seams. It holds the outcome of every key that was looked up (a file, a remembered failure, or a
miss; a file read by `ResolveTopLevel` is flagged `Exempt`) and the set of keys counted against `MaxFiles`. **It is
not thread-safe**: one instance per operation. A
candidate (private record `Candidate`) carries its `Key` and its `Origin`: the base directory or search path of the
candidate, or the origin of the referring file (in callback mode the logical directory, which no policy sees).

**Invariants**: no byte of a referenced file is read before the policy (when it needs the real path) allowed the open
handle (the top-level file of `ResolveTopLevel` is not judged; a reference that reaches it later is);
the bytes come from the handle the policy was asked about; a key is opened at most once per instance except after a
policy denial (a denial is not cached, so the policy is asked again); no more than `MaxFileBytes` bytes are read of any
file; with `MaxFiles` set, no more than that many distinct keys are opened or requested.

#### Key Methods

- `Resolve(reference, relativeTo)` - validates the text (`ValidateText`), rejects with `ArgumentException` a
  `relativeTo` of the other mode or one this instance did not return (the cached outcome of its key must hold that very
  instance), returns `null` when no source is configured, and walks the candidates, returning the first file that
  exists.
- `ResolveTopLevel(path)` (internal) - reads the top-level file a host named itself (the document being rendered,
  for `FolderImageResolver.Render(path)`). The host chose it, so the text rules, the pre-check, the network-target
  check and the policy are skipped; the open, the regular-file check, the size limit, a `MaxFiles` slot and the cache
  apply. The path is made full against the base directory (or the current directory); an invalid path is
  `InvalidPath`, and callback mode is an `InvalidOperationException`. The candidate's origin is the file's own folder,
  so its references are judged from there. The outcome is flagged `Exempt`; a later `Resolve` that reaches the same
  key applies the skipped checks (`CheckExemptFile`: the network target, `RealPathUnavailable` for a restrictive
  policy without a real path, and the policy).
- `DescribeInvalidText` - backslashes become forward slashes; refuses an empty text, a control character, a device
  path (`//?/`, `//./`), a network path (a leading `//`), and a colon anywhere except the drive of an absolute Windows
  path (`C:/`); in callback mode also an absolute or drive path.
- `Candidates` - in file-system mode an absolute reference (`Path.IsPathRooted`) is one candidate, resolved with
  `Path.GetFullPath(text, start)` so a rooted path without a drive takes the drive of the starting directory; a relative
  reference yields the referring file's directory (or the base directory) and then each search path. Each candidate
  carries its origin: the base directory or the search path itself, or, relative to a referring file, that file's origin
  (never its directory); an absolute reference keeps the referring file's origin or starts at the first configured
  directory. `FullCandidate` refuses on Windows a full path in a device namespace (`RealPath.IsWindowsDeviceNamespace`:
  `\\.\`, `\\?\`, `\??\`, for example `\\.\NUL` mapped from a legacy device name) or with a reserved device name in any
  component, with `InvalidPath` before the open. The only exemption is an ordinary extended-length path
  (`RealPath.IsOrdinaryExtendedPath`: `\\?\X:\` or `\\?\Volume{GUID}\`, where no names are mapped) under an ordinary
  extended-length configured directory with the same root (`Path.GetPathRoot`, compared under
  `RealPath.PathComparison`). Any other root (`\\?\GLOBALROOT\`, `\\?\UNC\`, `\\?\pipe\`, `\\.\`) gets no exemption, so
  every reference under such a base directory is refused, and an absolute reference such as `C:/x/NUL` cannot reach a
  device through an extended-length directory. In callback mode the logical candidates
  collapse `.` and `..` and refuse a `..` above the logical root.
- `LoadFile` - the per-candidate pipeline: (1) `policy.PreCheck(key)` may deny the path as written before anything is
  opened (not counted; skipped for the exempt top-level file); (2) a cached outcome answers, after the policy is asked
  again for a cached file (or `CheckExemptFile` runs for a file first read as the top-level file); (3) the file
  limit refuses a new key before the open; (4) `OpenCheckAndRead`, which skips the network-target check and the policy
  for the exempt top-level file.
- `OpenCheckAndRead` - opens the key through the hooks or `RealPath.OpenForRead`. "Not found" is a miss; an access
  failure on a directory is `NotARegularFile`; any other open failure is `CannotRead`. From the open on, the key counts.
  The handle's facts come from `RealPath.Inspect` (or the `RealPathOf` and `IsRegularFileOf` hooks). On Windows, a real
  path that is a network location (`RealPath.IsNetworkLocation`) while the key is not is `NetworkPath` under every
  policy, before the regular-file check and the policy; a handle that is not known to be a regular file (including one
  whose type the platform cannot tell) is `NotARegularFile` under every policy (fail closed); when the policy needs the
  real path and it is unknown, `RealPathUnavailable` (fail closed); otherwise `CheckPolicy` runs and the file is read
  from the same handle (`Read`).
- `CheckPolicy` - builds the `FileAccessRequest` (reference, referrer = `relativeTo.Key` or `ReferrerPath`, base
  directory = the candidate's origin, requested path, real path) and asks the policy. An exception other than
  `OperationCanceledException`, or a `null` decision, is a denial with the reason `the file-access policy failed`; a
  denial throws `AccessDenied` with the policy's name, reason and hint and both paths.
- `ReadBounded` (internal static, shared with `SandboxedFileResolver`) - checks a reported length first, then reads in
  8 KiB chunks, stops as soon as the running length exceeds the limit, and observes the token after each chunk.
- `ParseFileUri(text, out localPath)` (internal static) with the nested `FileUriKind` - classifies a text as a `file:`
  URI for the consumers that accept one: `NotFileUri` (no `file:` prefix, case-insensitive), `Local` (no host; the
  local path is `Uri.LocalPath`, percent-decoded), `HasHost` (any host, `localhost` included, and the UNC forms that
  `System.Uri` maps to a host; never read), or `Malformed` (the text does not parse as a file URI). The short form
  `file:/x` has no authority, which `System.Uri` rejects on Windows, so it is written out as `file:///x` first and
  parses alike on every platform. The text is not trimmed. Its consumers are `FolderImageResolver` (image links) and
  `FeatureCatalog.LocalPathOfFileUri` (`CanvasNetVegaLite`, through the core assembly's `InternalsVisibleTo` grant),
  so the `file:` rules of image links and data URLs are one implementation and cannot drift apart.
- `LoadLogical` and `ReadFromCallback` - callback mode: the callback, the bounded read and the disposal form one
  boundary whose every failure except a cancellation is `CannotRead`; the policy is never asked.

**Preconditions and postconditions**: `reference` is untrusted text; on return the file is a regular file that the
policy allowed (or the policy needs no real path), its bytes are within the limit, or `null` was returned because no
candidate exists, or an exception was thrown and nothing was read.

#### Error Handling

Every refusal and read failure is a `FileReferenceException`; a missing file is `null`. Text failures are thrown before
any access. A denial (`AccessDenied`), `RealPathUnavailable`, `NotARegularFile`, `CannotRead`, and `TooLarge` end the
search (no fall-through to the next candidate). `NotARegularFile`, `RealPathUnavailable`, `CannotRead`, `TooLarge`, and
a `NetworkPath` found after the open are remembered per key; a denial is not, so the policy decides again on the next
resolve. A canceled token is an `OperationCanceledException` (never wrapped, also when the policy or the callback raises
it). Invalid options are `ArgumentNullException` / `ArgumentOutOfRangeException`; an invalid configured directory is
`InvalidBaseDirectory` on first use.

#### Design Decisions

- **The chain origin, not the referrer's folder, is the policy's base directory.** A referring document's folder can be
  swapped for a link to another folder after the document was read; anchoring `NoSymlinks` there would trust that link
  (its real location would become the expected one), so a nested reference could reach any file. The origin is always
  a folder the host configured, and its anchor (the deepest folder shared with the path as written) is an
  ancestor-or-self of it, which the threat model trusts. The ownership check in `Resolve` keeps a file from another
  resolver, which carries another resolver's origin, from becoming a starting point.
- **A link to a share is refused under every policy for a referenced file.** A document reaching the network through a
  local path is never
  expected; the open itself follows the link and may already contact the share before the check refuses the file
  (see the subsystem design for the `SymlinkEvaluation` advice). A key that is itself a network location (a UNC base
  directory or a mapped network drive) is exempt, so a host working on a share keeps working.

**Known limits**: where Windows cannot tell the real path, the default policy reads the file without the network check
(a restrictive policy fails closed with `RealPathUnavailable`); a SUBST or DFS path whose drive is local but whose
target is remote is refused (fail closed); paths are compared case-insensitively on Windows and macOS (see
_FileAccessPolicy Unit Design_).

#### Dependencies

- **FileAccessPolicy** (same subsystem) - the policy that decides, including its lexical pre-check.
- **RealPath** (same subsystem) - the open, the handle facts, and the platform path comparer.
- **.NET base class library** - `System.IO` (`Path`, `FileStream`, `Directory`) and `System.Collections.Generic`.

#### Callers

`FileReferenceResolver` is a public API entry point for consumers of the CanvasNet package. Inside the library it is
used by `FolderImageResolver` (`ImageSources`; every file link, and `ResolveTopLevel` for the file of a render by
path), by `DataFileResolver` (`CanvasNetVegaLite`) and by `ScadFileResolver` (`CanvasNetScad`), each with the policy
its caller chose; the obsolete `SandboxedFileResolver` reuses its `ReadBounded`, and `FolderImageResolver` and the
Vega-Lite `FeatureCatalog` share its `ParseFileUri`. The tests in
`test/DemaConsulting.CanvasNet.Tests/Files/` call it
directly and reach the internal hooks constructor through the core assembly's `InternalsVisibleTo` grant to the test
project.
