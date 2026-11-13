## Files Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Files` subsystem: the
`FileReferenceResolver`, `FileAccessPolicy`, `RealPath`, and `SandboxedFileResolver` units and their supporting types.

### Verification Approach

Each unit is verified by its own unit tests (see _FileReferenceResolver Unit Verification Design_, _FileAccessPolicy
Unit Verification Design_, _RealPath Unit Verification Design_, and _SandboxedFileResolver Unit Verification Design_).
The subsystem tests in `FilesTests.cs` exercise the units together through their public types: a resolver with the
default policy and with combined built-in policies over real temporary trees, the race-free check of both resolvers
with the tree changed inside the open seams, the references that are refused under every policy, the resolver
callback of the reference resolver, the file limit of both resolvers, and the facts a denial carries.

`FilesTests.cs` also holds a **source-tree guard**: it scans the text of `src/**/*.cs` (comment lines ignored) for
direct file opens (`new FileStream(`, `File.Open*(`, `File.ReadAll*(`, `File.ReadLines(`) and fails when one appears
outside a reviewed allow list (the subsystem itself, the codecs' and documents' top-level `Load`/`Open`/`Save`, the
font scanner, the tool's self-validation, and the viewer's input and output files), each entry with the reason it never
reads a referenced file; a second test fails when an entry no longer opens a file. The guard reads source text only
and executes nothing from other systems. It is a tripwire, not linked to a requirement.

The consumers of the subsystem - `FolderImageResolver` in `ImageSources`, `DataFileResolver` in `CanvasNetVegaLite`,
and `ScadFileResolver` in `CanvasNetScad` - verify in their own projects that every failure kind they receive is
translated into their own located error; they do not repeat the subsystem's mechanics.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline on Windows, Linux, and macOS (Arm64), each
  with .NET 8, 9, and 10
- **Mocking**: None; the internal seams (the open hooks, the link probe, the stream opener) are real implementations
  supplied by the tests
- **Files**: per-test temporary directories, deleted by the test; no checked-in file is read except by the source-tree
  guard, which reads `src/`
- **Links**: directory links fall back to an NTFS junction on Windows, which needs no privilege, so link and race
  scenarios run everywhere; a file symbolic link needs a privilege on Windows, so the one scenario that needs it skips
  there with the reason
- **Operating-system scenarios**: named pipes (`mkfifo`) and devices (`/dev/null`) on Linux and macOS, the null device
  and short (8.3) names on Windows, the `/private` location of the temporary folder on macOS, and execute-only folders
  on Linux and macOS (skipped where permissions are not enforced, as root); each skips on the other
  systems and is linked to its requirement with a source filter

### Acceptance Criteria

The `Files` subsystem's verification passes when every subsystem scenario below and every unit scenario described in
the unit verification designs passes without error or unexpected exception on each operating system of the CI matrix.

### Test Scenarios

#### Referenced Files Are Read Anywhere by Default

**Test**: `Files_ReferenceResolution_RelativeDotDotAndAbsoluteReferences_AreReadUnderTheDefaultPolicy`

A relative, a dot-dot, and an absolute reference are read under the default policy; a missing file is `null`.

#### Roots and No-Symlinks Combined Confine References

**Test**: `Files_AccessPolicy_RootsAndNoSymlinksCombined_ReadsInsideAndDeniesEscapeAndLink`

With `Combine(UnderRoots(root), NoSymlinks)` a plain file inside the root is read, a dot-dot escape and a file reached
through a directory link inside the root are `AccessDenied`, and the denial names `roots+no-symlinks`.

#### The Decision Follows the Opened File

**Test**: `Files_RaceFreeCheck_SwapAfterOpen_DecisionFollowsTheOpenedFile`

For the engine, a link to an outside folder is replaced by a real inside folder after the open: the file is still
denied by `UnderRoots`. For the sandbox, a folder is replaced by a link to an outside folder after the link walk and
before the open: the file is `ThroughLink`. The scenario runs on Windows, Linux, and macOS.

#### Unsafe References Are Refused Under the Default Policy

**Test**: `Files_AlwaysRefused_NetworkDeviceAndNonRegularReferences_AreRefusedUnderAllowAll`

A UNC path and a `//host` path are `NetworkPath`, a `\\.\` device path and an alternate data stream are `InvalidPath`,
a directory is `NotARegularFile`, and on Linux and macOS `/dev/zero` is `NotARegularFile`. On Windows, `folder/NUL` (a
legacy device name) is `InvalidPath`, and a real local file whose real path is reported (through the `RealPathOf` seam)
as a network share is `NetworkPath` under the default policy.

#### The Reference Resolver Uses Only Its Callback

**Test**: `Files_ResolverCallback_ReferenceResolverUsesOnlyTheCallbackAndReportsItsFailure`

With a resolver callback, a backslash reference and a reference relative to the file it found reach the callback as the
normalized logical paths `dir/main.txt` and `dir/x.txt`, and a policy that would fail the test is never asked; a
callback that throws is `CannotRead`, and a cancellation it raises passes through.

#### The File Limit Refuses the Extra File Before It Is Opened

**Test**: `Files_BoundedReading_FileLimit_RefusesTheExtraFileBeforeItIsOpened`

With `MaxFiles = 1`, the reference resolver and the sandbox each read `a.txt` and refuse `b.txt` with `TooManyFiles`;
the open seams of both record that only `a.txt` was ever opened.

#### A Denial Carries Its Facts Without a Host Path in the Message

**Test**: `Files_ReferenceFailureReporting_DenialCarriesFactsWithoutHostPathInMessage`

A dot-dot reference outside a roots policy is denied with the reference, the policy name, a reason, a hint, and the
requested path; the message names the reference and the policy but not the temporary directory.

#### Source-Tree Guard

**Tests**: `Files_SourceTree_DirectFileOpens_AreOnlyInAllowlistedFiles`,
`Files_SourceTree_EveryAllowlistEntry_StillOpensAFile`

Every direct file open in `src/` is in an allow-listed file, and every allow-list entry still opens a file.
