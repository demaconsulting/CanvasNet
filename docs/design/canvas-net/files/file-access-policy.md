### FileAccessPolicy

![Files Structure](FilesView.svg)

The `FileAccessPolicy` unit is the hook through which a host decides which referenced files may be read. It comprises
four source files in the `DemaConsulting.CanvasNet.Files` namespace:

- `FileAccessPolicy.cs` - `FileAccessPolicy` (public abstract partial class): the contract, the factories, and the
  internal `RequiresRealPath` and `PreCheck` members
- `FileAccessPolicy.BuiltIns.cs` - the private built-in policies (`AllowAllPolicy`, `UnderRootsPolicy`,
  `NoSymlinksPolicy`, `CombinedPolicy`, `DelegatePolicy`) and the internal link match `MatchesWithoutLinks` that
  `NoSymlinks` shares with `SandboxedFileResolver`
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
(the full path as written, links not resolved), and `RealPath` (the final path of the open handle).

**`FileAccessDecision`**: `IsAllowed`, `Reason` and `Hint` (sentence fragments without a trailing period). `Allow` is
one shared instance; `Deny(reason, hint)` requires a non-empty reason and drops an empty hint.

**`FileAccessPolicy`**: the abstract `Name` and `Check(request)`; the internal virtual `RequiresRealPath` (`true`
except for `AllowAll`) and `PreCheck(requestedPath)` (`null` except for `UnderRoots`). External subclasses keep the
defaults, so a custom policy always gets a real path or fails closed.

**Built-in policies** (all immutable and thread-safe):

| Factory | Name | Rule |
| --- | --- | --- |
| `AllowAll` | `any` | allows every request; needs no real path |
| `UnderRoots(roots)` | `roots` | the written path and the real path must each lie inside a root |
| `NoSymlinks` | `no-symlinks` | the real path must equal the written path below the anchor |
| `Combine(policies)` | parts joined with `+` | every part must allow; the first denial wins |
| `FromDelegate(name, check)` | the given name | the function decides |

**`UnderRootsPolicy`** keeps each root as its lexical full path and its real location
(`RealPath.TryGetCanonicalDirectory`), both captured when the policy is created and never looked up again. A root that
does not exist then (or whose real location cannot be determined) has no captured location and allows no file, even
once it exists (fail closed; the denial says so and hints to create the folder first). This closes the root
rename-and-replace race: a root renamed and replaced by a link after creation leads an open elsewhere, and the real
path no longer lies under the captured location.

**Pre-open snapshot.** A built-in policy never resolves a folder by name after a file was opened. The internal
`AnchorsOf(requestedPath, baseDirectory)` names the trusted folders a policy compares against (`NoSymlinks`: its
anchor; `Combine`: the union of its parts; others: none). `FileReferenceResolver` captures their real locations (once
per resolver, before the first file below them is opened) and the long (8.3-expanded) spelling of the written path
before every open, and passes them as the internal `FileAccessRequest.Snapshot`. A request without a snapshot (built by
other code, with no open in progress) is evaluated by resolving now.

#### Key Methods

- `UnderRoots(...)` - validates the roots (null list or entry: `ArgumentNullException`; no root, an empty, white-space
  or invalid root: `ArgumentException`) and captures their real locations. `PreCheck` denies a written path outside
  every root, in the root's given or captured real spelling (`the path as written is outside the allowed roots`); a
  written path with `~` is also compared in its long spelling (Windows). `Check` repeats the written-path test (with
  the long spelling from the snapshot) and then requires the real path inside the captured real location of a root
  (`its real path is outside the allowed roots`). Containment is `RealPath.IsUnder` (platform case rule, the separator
  as the boundary).
- `NoSymlinks.Check` - the anchor is `RealPath.CommonAncestor(RequestedPath, BaseDirectory)` (the drive root when they
  share none); the anchor and everything above it are trusted. The expected real path is the anchor's real location
  from the snapshot joined with the written path below the anchor; the file is allowed when it equals the real path
  (`RealPath.PathsEqual`). When it does not and the snapshot's long spelling differs from the written path, the
  comparison is repeated with the same number of trailing components of the long spelling; this only rewrites the
  expected text, so a link still shows up as a different real path. An anchor whose real location is unknown, or was
  not captured, is a denial. The comparison itself is the internal static
  `MatchesWithoutLinks(anchor, canonicalAnchor, requestedPath, longRequestedPath, realPath)`, which resolves nothing
  and answers `true` (no link below the anchor), `false` (a link), or `null` (the anchor's real location is unknown);
  `SandboxedFileResolver` calls it with its root and the root's real location captured before the walk and the open,
  and maps `false` to `ThroughLink` and `null` to `LinkCheckFailed`. Inside a `FromDelegate` policy the resolver does
  not know the anchor, so `NoSymlinks` there denies (compose with `Combine` instead).
- `Combine(...)` - validates the list, flattens nested combinations, returns a single policy unchanged.
  `RequiresRealPath` is true when any part needs it; `PreCheck` returns the first part's denial; `Check` returns the
  first denial and treats a part that returns `null` as a failure (`InvalidOperationException`), which the resolver
  turns into a denial.
- `FromDelegate(name, check)` - validates the name and the function; the policy needs a real path.

#### Error Handling

Factories throw `ArgumentNullException` / `ArgumentException` for invalid arguments. `Check` never throws for a valid
request of the built-ins; a request that is `null` is an `ArgumentNullException`. Exceptions and `null` decisions of
custom policies are handled by the caller (`FileReferenceResolver` denies the file).

**Known limit (case-insensitive comparison)**: `RealPath.PathComparison` is case-insensitive on Windows and macOS. On
a case-sensitive APFS volume, or a Windows directory with case sensitivity enabled, a sibling folder whose name differs
from a root only in letter case counts as inside that root for `UnderRoots` (both the written-path and the real-path
test). Deciding case sensitivity per directory (Windows) or per volume (macOS) on every component is neither cheap nor
reliable (the macOS handle path is not guaranteed to carry the on-disk case), so the limit is documented: hosts should
not configure roots next to such folders. `NoSymlinks` is not meaningfully affected: a pure case difference between
the written and the real path is not a link redirection.

#### Dependencies

- **RealPath** (same subsystem) - `IsUnder`, `PathsEqual`, `CommonAncestor`, `TryGetCanonicalDirectory`, and
  `ExpandShortNames`.
- **.NET base class library** - `System.IO.Path` and LINQ.

#### Callers

`FileReferenceResolver` asks the policy for every opened file; `SandboxedFileResolver` uses the `NoSymlinks` link match
(`MatchesWithoutLinks`), anchored at the root, to verify its open file. Host applications create policies through the
factories or subclass the type.
