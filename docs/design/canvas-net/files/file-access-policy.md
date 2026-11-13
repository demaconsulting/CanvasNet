### FileAccessPolicy

![Files Structure](FilesView.svg)

The `FileAccessPolicy` unit is the hook through which a host decides which referenced files may be read. It comprises
four source files in the `DemaConsulting.CanvasNet.Files` namespace:

- `FileAccessPolicy.cs` - `FileAccessPolicy` (public abstract partial class): the contract, the factories, and the
  internal `RequiresRealPath` and `PreCheck` members
- `FileAccessPolicy.BuiltIns.cs` - the private built-in policies (`AllowAllPolicy`, `UnderRootsPolicy`,
  `NoSymlinksPolicy`, `CombinedPolicy`, `DelegatePolicy`)
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
directory the reference was resolved against), `RequestedPath` (the full path as written, links not resolved), and
`RealPath` (the final path of the open handle).

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

**`UnderRootsPolicy`** keeps each root as its lexical full path (fixed at creation) and its real location (from
`RealPath.TryGetCanonicalDirectory`, determined on first use and again while it cannot be determined, so a root
created later is found).

#### Key Methods

- `UnderRoots(...)` - validates the roots (null list or entry: `ArgumentNullException`; no root, an empty, white-space
  or invalid root: `ArgumentException`). `PreCheck` denies a written path outside every root (`the path as written is
  outside the allowed roots`); a written path with `~` is also compared in its long spelling
  (`RealPath.ExpandShortNames`, Windows). `Check` repeats the written-path test and then requires the real path inside
  a root (`its real path is outside the allowed roots`). Containment is `RealPath.IsUnder` (platform case rule, the
  separator as the boundary). A real path contains no link, so comparing it with the root's written spelling as well
  as its real one is safe.
- `NoSymlinks.Check` - the anchor is `RealPath.CommonAncestor(RequestedPath, BaseDirectory)` (the drive root when they
  share none); the anchor and everything above it are trusted. The expected real path is the anchor's real location
  joined with the written path below the anchor; the file is allowed when it equals the real path
  (`RealPath.PathsEqual`). When it does not and the written path contains `~`, the comparison is repeated with the
  long spelling of the anchor and the written path; this only rewrites the expected text, so a link still shows up as
  a different real path. An anchor whose real location cannot be determined is a denial.
- `Combine(...)` - validates the list, flattens nested combinations, returns a single policy unchanged.
  `RequiresRealPath` is true when any part needs it; `PreCheck` returns the first part's denial; `Check` returns the
  first denial and treats a part that returns `null` as a failure (`InvalidOperationException`), which the resolver
  turns into a denial.
- `FromDelegate(name, check)` - validates the name and the function; the policy needs a real path.

#### Error Handling

Factories throw `ArgumentNullException` / `ArgumentException` for invalid arguments. `Check` never throws for a valid
request of the built-ins; a request that is `null` is an `ArgumentNullException`. Exceptions and `null` decisions of
custom policies are handled by the caller (`FileReferenceResolver` denies the file).

#### Dependencies

- **RealPath** (same subsystem) - `IsUnder`, `PathsEqual`, `CommonAncestor`, `TryGetCanonicalDirectory`, and
  `ExpandShortNames`.
- **.NET base class library** - `System.IO.Path` and LINQ.

#### Callers

`FileReferenceResolver` asks the policy for every opened file; `SandboxedFileResolver` uses `NoSymlinks`, anchored at
the root, to verify its open file. Host applications create policies through the factories or subclass the type.
