### FileAccessPolicy

![Files Structure](FilesView.svg)

The `FileAccessPolicy` unit is the hook through which a host decides which referenced files may be read. It comprises
four source files in the `DemaConsulting.CanvasNet.Files` namespace:

- `FileAccessPolicy.cs` - `FileAccessPolicy` (public abstract partial class): the contract, the factories, and the
  internal `RequiresRealPath`, `PreCheck`, and `AnchoredAt` members
- `FileAccessPolicy.BuiltIns.cs` - the private built-in policies (`AllowAllPolicy`, `UnderRootsPolicy`,
  `NoSymlinksPolicy`, `CombinedPolicy`, `DelegatePolicy`, `AnchoredPolicy`) and the internal link match
  `MatchesWithoutLinks` that `NoSymlinks` shares with `SandboxedFileResolver`
- `FileAccessRequest.cs` - `FileAccessRequest` (public sealed record): the facts a policy decides about
- `FileAccessDecision.cs` - `FileAccessDecision` (public sealed record): the answer

#### Purpose

Which files a document may read depends on the host: a desktop converter reads what its user can, a build server
confines documents to the repository, a service may refuse links. `FileAccessPolicy` expresses that rule once, as an
immutable, thread-safe object that `FileReferenceResolver` asks after it opened a file and before it reads it. Built-in
policies cover the common rules and compose; a host with another rule derives a class or wraps a function.

The unit's behavior is specified by the `CanvasNet-Files-FileAccessPolicy-*` requirements (`AllowAll`, `UnderRoots`,
`NoSymlinks`, `Combine`, `FromDelegate`, and `Decision`) in `docs/reqstream/canvas-net/files/file-access-policy.yaml`.

#### Data Model

**`FileAccessRequest`**: `Reference` (as written), `Referrer` (the referring document or `null`), `BaseDirectory` (the
host-chosen directory the chain of references started from: the configured base directory, or the search path under
which the first file of the chain was found; never a referring document's folder, so it is trusted), `RequestedPath`
(the full path as written, links not resolved), and `RealPath` (the final path of the open handle; when the resolver
builds the request, in the letter case stored on disk and confirmed to name the open file).

**`FileAccessDecision`**: `IsAllowed`, `Reason` and `Hint` (sentence fragments without a trailing period). `Allow` is
one shared instance; `Deny(reason, hint)` requires a non-empty reason and drops an empty hint.

**`FileAccessPolicy`**: the abstract `Name` and `Check(request)`; the internal virtual `RequiresRealPath` (`true`
except for `AllowAll`), `PreCheck(requestedPath)` (`null` except for `UnderRoots`), and `AnchoredAt(origin)`. External
subclasses keep the defaults, so a custom policy always gets a real path or fails closed.

**Built-in policies** (all immutable and thread-safe):

| Factory | Name | Rule |
| --- | --- | --- |
| `AllowAll` | `any` | allows every request; needs no real path |
| `UnderRoots(roots)` | `roots` | the written path and the real path must each lie inside a root |
| `NoSymlinks` | `no-symlinks` | the real path must equal the written path below the anchor |
| `Combine(policies)` | parts joined with `+` | every part must allow; the first denial wins |
| `FromDelegate(name, check)` | the given name | the function decides |

**`UnderRootsPolicy`** keeps each root as its lexical full path and its real location in its on-disk letter case
(`RealPath.TryGetExactCanonicalDirectory`), both captured when the policy is created and never looked up again. A root
that does not exist then (or whose real location cannot be determined) has no captured location and allows no file, even
once it exists (fail closed; the denial says so and hints to create the folder first). This closes the root
rename-and-replace race: a root renamed and replaced by a link after creation leads an open elsewhere, and the real path
no longer lies under the captured location.

**Pre-open snapshot.** A built-in policy never resolves a folder by name after a file was opened. The internal
`AnchorsOf(requestedPath, baseDirectory)` names the trusted folders a policy compares against (`NoSymlinks`: its anchor;
`Combine`: the union of its parts; others: none). `FileReferenceResolver` captures their real locations in their on-disk
letter case (once per resolver, keyed exactly, before the first file below them is opened) and the on-disk spelling of
the written path (`ExactSpelling`: short names expanded and every existing component in its stored letter case) before
every open, and passes them as the internal `FileAccessRequest.Snapshot`. A request without a snapshot (built by other
code, with no open in progress) is evaluated by resolving now.

#### Key Methods

- `UnderRoots(...)` - validates the roots (null list or entry: `ArgumentNullException`; no root, an empty, white-space
  or invalid root: `ArgumentException`) and captures their real locations. `PreCheck` denies a written path outside
  every root, in the root's given or captured real spelling (`the path as written is outside the allowed roots`); a
  written path with `~` is also compared in its long spelling (Windows). `Check` repeats the written-path test (with
  the on-disk spelling from the snapshot) and then requires the real path inside the captured real location of a root
  (`its real path is outside the allowed roots`). The written-path tests use `RealPath.IsUnderAsWritten` (platform case
  rule, the separator as the boundary): they are a lexical pre-filter that never allows a file on its own. The real-path
  test uses `RealPath.IsUnder`, which compares exactly (`Ordinal`, letter case included; Unicode composed form on
  macOS) against the root's real location in its on-disk letter case, so a folder whose name differs from a root only in
  letter case is outside it.
- `NoSymlinks.Check` - the anchor is `RealPath.CommonAncestor(RequestedPath, BaseDirectory)` (the drive root when they
  share none); the anchor and everything above it are trusted. The expected real path is the anchor's real location from
  the snapshot joined with the written path below the anchor; the file is allowed when it equals the real path
  (`RealPath.PathsEqual`, exact). When it does not and the snapshot's on-disk spelling differs from the written path
  (short names, or another letter case than the stored one), the comparison is repeated with the same number of trailing
  components of the on-disk spelling; this only rewrites the expected text, so a link still shows up as a different real
  path, and a link named like its target in another letter case (on a case-sensitive folder) is kept as written by the
  on-disk spelling (the exact entry wins), so it is denied. `CommonAncestor` compares components exactly, so a folder
  that differs from the base directory's only in letter case is never the trusted anchor. `Path.GetRelativePath` only
  computes the text expected below the anchor (the anchor is a component prefix of the written path); it never decides.
  An anchor whose real location is unknown, or was not captured, is a denial. The comparison itself is the internal
  static `MatchesWithoutLinks(anchor, canonicalAnchor, requestedPath, exactRequestedPath, realPath)`, which resolves
  nothing and answers `true` (no link below the anchor), `false` (a link), or `null` (the anchor's real location is
  unknown); `SandboxedFileResolver` calls it with its root and the root's real location captured before the walk and the
  open, and maps `false` to `ThroughLink` and `null` to `LinkCheckFailed`. Inside a `FromDelegate` policy the resolver
  does not know the anchor, so `NoSymlinks` there denies (compose with `Combine` instead).
- `Combine(...)` - validates the list, flattens nested combinations, returns a single policy unchanged.
  `RequiresRealPath` is true when any part needs it; `PreCheck` returns the first part's denial; `Check` returns the
  first denial and treats a part that returns `null` as a failure (`InvalidOperationException`), which the resolver
  turns into a denial.
- `FromDelegate(name, check)` - validates the name and the function; the policy needs a real path.
- `AnchoredAt(origin)` (internal) - the same policy judged from a fixed chain origin, for the files a nested document
  reads itself through its own resolver (a linked model's includes, a linked chart's data). That resolver's base
  directory is the folder of the nested file, which may have been swapped for a link after the outer check; without
  an anchor, `NoSymlinks` would trust it. The private `AnchoredPolicy` keeps the inner policy's `Name`,
  `RequiresRealPath` and `PreCheck`, hands every request to it with `BaseDirectory` replaced by the origin, and answers
  `AnchorsOf` with the inner policy's folders for that same origin, so the nested resolver captures, before its open,
  exactly the folder the anchored check compares against (otherwise `NoSymlinks` would deny every nested file). A
  policy that needs no real path (`AllowAll`) is returned as it is, and an anchored policy returns itself, so the
  outermost origin wins. `FolderImageResolver` sets `ImageSourceRequest.FileAccess` to its policy anchored at the
  origin of the chain.

#### Error Handling

Factories throw `ArgumentNullException` / `ArgumentException` for invalid arguments. `Check` never throws for a valid
request of the built-ins; a request that is `null` is an `ArgumentNullException`. Exceptions and `null` decisions of
custom policies are handled by the caller (`FileReferenceResolver` denies the file).

**Exact letter case.** A folder may be case-sensitive on every platform (a Windows directory with case sensitivity
enabled, a case-sensitive APFS volume), where `ALLOWED` next to `allowed` is another folder. The built-in policies
therefore decide on real paths compared exactly, letter case included, and compare them in the letter case stored on
disk: the roots and anchors are captured in it (`TryGetExactCanonicalDirectory`), and the resolver hands over the real
path of the open file in it, confirmed by file identity to name that file. On a case-insensitive volume a root, a base
directory or a reference written in another letter case still works, because every side is compared in the stored
spelling. Fail-closed cases: a root missing when the policy is created allows nothing; a real path whose stored
spelling cannot be confirmed to name the open file (for example a file system that reports the spelling used to open a
file and holds several names that differ only in letter case) is denied by the resolver before the policy is asked; a
component whose folder cannot be listed, or that matches several entries ignoring case, is kept as written and can then
only fail to match. A host that calls `Check` directly must pass `RealPath` in its on-disk letter case.

#### Dependencies

- **RealPath** (same subsystem) - `IsUnder`, `IsUnderAsWritten`, `PathsEqual`, `CommonAncestor`,
  `TryGetExactCanonicalDirectory`, `GetExactPathName`, and `ExpandShortNames`.
- **.NET base class library** - `System.IO.Path` and LINQ.

#### Callers

`FileReferenceResolver` asks the policy for every opened file; `SandboxedFileResolver` uses the `NoSymlinks` link match
(`MatchesWithoutLinks`), anchored at the root, to verify its open file. `FolderImageResolver` (`ImageSources`) anchors
its policy with `AnchoredAt` for each request it hands to a source. Host applications create policies through the
factories or subclass the type and pass them as the `FileAccess` option of `FolderImageResolverOptions`,
`ScadOpenOptions`, `VegaLiteOpenOptions` and the Viewer options.
