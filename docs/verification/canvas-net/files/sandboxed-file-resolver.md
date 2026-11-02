### SandboxedFileResolver Unit Verification Design

This document describes the unit-level verification strategy for the `SandboxedFileResolver` unit: the public
`SandboxedFileResolver` (search, path rules, confinement, link walk, callback mode, bounded reads, per-instance cache)
and its supporting `SandboxOptions`, `SandboxedFile`, `SandboxException`, and `SandboxFailureKind` types, in the
`DemaConsulting.CanvasNet.Files` namespace.

#### Verification Approach

The unit is verified by calling `SandboxedFileResolver.Resolve` directly and asserting on the returned
`SandboxedFile` (its key, directory, root, and bytes), on the requests that reached a file source, and on the
`SandboxException`s thrown (their kind, path, detail, inner exception, and message). Two kinds of file source are used,
and both are real implementations of the unit's contract rather than mocks:

- **Callback mode** uses `SandboxVirtualFiles` (in `SandboxTestSupport.cs`), an in-memory file set exposed as the
  `FileResolver` delegate. It records every logical path it is asked for, the thread of each request, and the streams it
  handed out (so that disposal can be asserted), so the tests can assert exactly which paths reached the callback, that
  hostile paths never did, that a miss is asked once, and that no file-system access happens (the scenarios run with a base
  directory whose same-named file must never be read).
- **File-system mode** uses `SandboxTempDirectory`, a per-test temporary directory tree that the test creates and deletes,
  for real files and folders, so that search order, normalization, byte limits, and the cache are proven against the real
  file system.

The unit has two internal test seams, supplied through an internal constructor that the test project reaches through the
core assembly's `InternalsVisibleTo` grant: a **link probe** (`Func<string, bool>`) and a **file opener**
(`Func<string, Stream>`). They exist so that no scenario needs a symbolic-link privilege, a file permission, an ACL, or
a lock, and every scenario behaves identically on Windows, Linux, and macOS:

- The **cited** link scenarios (`SandboxedFileResolverLinkTests`) inject a fake probe that reports chosen paths of real
  temporary files and directories as links, so the walk logic (top-down order, existing components only, the final component
  always probed, caching, trusted root, fail-closed probe, resolver mode never probing,   dot-dot normalization) is
    proven on every platform without creating a link, and these tests never skip.
- The **open** scenarios make the open of an existing file fail with an access or I/O error, or report "not found", through
  the injected opener, and delegate to the file system where the real open is wanted.
- Two scenarios create **real links** (a symbolic link through the framework and, on Windows, a junction through
  `mklink /J`) and so additionally prove the default probe against the real file system, including a dangling file link;
  they skip themselves where the host cannot create links and are listed as additional evidence of the same requirement
  rather than as its only evidence.

Limits are verified at their exact boundary (the limit accepted and one over), the endless stream with a stream that
never ends and counts the bytes it was asked for, and cancellation with a token that is already canceled. No timing is
asserted.

Unit tests reside in `SandboxedFileResolverTests.cs`, `SandboxedFileResolverLinkTests.cs`, and
`SandboxExceptionTests.cs` within the `DemaConsulting.CanvasNet.Tests` project, in the `Files` folder, with the shared
helpers (`SandboxTempDirectory`, `SandboxVirtualFiles`, `SandboxEndlessStream`, `SandboxTrackedStream`, and
`SandboxLinkSupport`) in `SandboxTestSupport.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None; the in-memory file set, the temporary directories, the injected link probe, and the injected file
  opener are real implementations of the unit's seams
- **Files**: Per-test temporary directories under the system temporary folder, deleted by the test; no checked-in file is
  read by these tests
- **Threads**: The callback-mode scenario asserts that every request came from the calling thread
- **Isolation**: Each test builds its own file set or directory and its own resolver, as one operation does
- **Capabilities**: the real-link scenarios need permission to create a symbolic link or junction and skip otherwise;
  nothing else depends on the host

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A request to the callback that was not expected, a file-system access in callback mode, an accepted hostile
path, a file read through a link, a wrong kind or detail, a host directory in an exception message, or a wrong exception
type constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNet-Files-SandboxedFileResolver-Resolution: Search Order And Relative Resolution

**Tests**: `SandboxedFileResolver_Resolve_RelativePathUsesBaseDirectoryThenSearchPaths`,
`SandboxedFileResolver_Resolve_RelativeToFileStartsInItsDirectoryThenSearchPaths`,
`SandboxedFileResolver_Resolve_ResolverModeRelativeToFileUsesItsLogicalDirectory`,
`SandboxedFileResolver_Resolve_RelativeToFileOfAnotherResolverIsRejected`

Asserts that the same name in the base directory and in search paths is taken from the base directory, that a name only
in a later search path is found there, that a backslash path is found, that the result carries the full-path key, the
file's directory, the confining root, and the bytes, that a path relative to a resolved file starts in that file's
directory (and may climb to, but not above, its root) before falling back to the search paths, and that the same holds
for logical directories in callback mode. A file returned by another resolver whose roots are wider (or different) is
rejected with `ArgumentException` in file-system and in callback mode, while the resolver's own file still works.

##### CanvasNet-Files-SandboxedFileResolver-MissingFiles: Not Found Is Not A Failure

**Tests**: `SandboxedFileResolver_Resolve_MissingFileReturnsNullAndNoSourceHasNone`

Asserts that a file that does not exist yields `null` in file-system mode and in callback mode, that a resolver
with no base directory, search path, or callback finds nothing and reports `HasSource` false, and that a search path
alone makes
`HasSource` true.

##### CanvasNet-Files-SandboxedFileResolver-PathText: Hostile Path Text Is Refused Before Any Access

**Tests**: `SandboxedFileResolver_Resolve_HostilePathTextIsRejectedWithoutAnyAccess`,
`SandboxedFileResolver_Resolve_NullPath_ThrowsArgumentNullException`

Asserts, as a theory over an empty path, white space, a Unix absolute path, a rooted backslash path, UNC paths in both slash
styles, a drive path, a drive-relative path, a colon inside a name, NUL, a line feed, DEL, and two C1 control characters,
that each is refused with the matching kind, a reason in `Detail`, a path with no control character left in it, and a
message that says the path is not allowed, and that the callback received no request. A null path is an
`ArgumentNullException`.

##### CanvasNet-Files-SandboxedFileResolver-Confinement: Candidates Stay Inside Their Root

**Tests**: `SandboxedFileResolver_Resolve_DotDotInsideRootNormalizesAndEscapeIsRejected`,
`SandboxedFileResolver_Resolve_SiblingWithSharedPrefixAndOutsideFileAreRefused`,
`SandboxedFileResolver_Resolve_SearchPathConfinesCandidates`,
`SandboxedFileResolver_Resolve_InvalidConfiguredDirectoryIsInvalidRoot`,
`SandboxedFileResolver_Constructor_SearchPathsAreSnapshotted`

Asserts that `.` and `..` segments inside the root (also with backslashes) name one file (the very same
instance), that a `..` that leaves the root, directly or after a detour, is refused with `OutsideRoot` and the
normalized path even though
the file exists, that a sibling directory that merely shares a name prefix with the root is outside it, that a search path
confines its own candidates, and that a configured base directory or search path that is not a valid path is `InvalidRoot`
naming the configured directory. Changing the caller's search-path list after construction neither adds nor removes a root.

##### CanvasNet-Files-SandboxedFileResolver-Links: Links Are Refused Before The File Is Opened

**Tests**: `SandboxedFileResolverLinkTests.SandboxedFileResolver_Resolve_LinkedDirectoryIsRejectedBeforeOpen`,
`SandboxedFileResolverLinkTests.SandboxedFileResolver_Resolve_LinkedFileIsRejectedBeforeOpen`,
`SandboxedFileResolverLinkTests.SandboxedFileResolver_Resolve_LinkBelowSearchPathIsRejected`,
`SandboxedFileResolverLinkTests.SandboxedFileResolver_Resolve_LinkedRootAndAncestorsAreTrusted`,
`SandboxedFileResolverLinkTests.SandboxedFileResolver_Resolve_LinkAfterMissingComponentIsNotProbedAndFileIsMissing`,
`SandboxedFileResolverLinkTests.SandboxedFileResolver_Resolve_MissingFinalComponentIsProbedAndDanglingLinkIsRejected`,
`SandboxedFileResolverLinkTests.SandboxedFileResolver_Resolve_EveryComponentProbedOnceAndCached`,
`SandboxedFileResolverLinkTests.SandboxedFileResolver_Resolve_DotDotRouteThroughLinkIsJudgedOnNormalizedPath`,
`SandboxedFileResolverLinkTests.SandboxedFileResolver_Resolve_ProbeFailureFailsClosed`

Asserts (with the fake probe, on every platform) that a linked directory at depth one, two, or three between the root and
the file is rejected as `ThroughLink` before the file is opened (the file's bytes are unreadable, so reading it would give
a different outcome), that `Detail` names the link relative to the root and the message names no host
directory, that the walk stops at the first link, that a file that is itself a link is rejected whatever its target
while its neighbor is read, that
a link below a search path root is rejected too, that the root and everything above it are trusted even when the probe
reports them as links, that the first missing component ends the walk (the file is missing, nothing below is probed, no
link is reported), that a missing final component is still probed (a dangling link is refused as a link, a plain missing
file is missing, and both were probed), that each component is probed once however many files go through it, that a route
through dot-dot is judged on the normalized path, that a probe that throws (access or I/O) rejects the file as
`LinkCheckFailed` keeping the cause, and that callback mode never asks the probe.

Additional evidence, not the only evidence and skipped only where the host cannot create links:
`SandboxedFileResolver_Resolve_RealDanglingLinkIsRejectedOrCapabilitySkipped` and
`SandboxedFileResolver_Resolve_RealLinksAreRejectedOrCapabilitySkipped` create real links (a directory link, a directory
link inside the root, a file link, a dangling file link, and on Windows a junction) and assert that each is rejected, that
a base directory reached through a link is trusted, and that the real file beside them is read, which proves the default
probe against the real file system. _Not verified, by design:_ the requirement covers symbolic links and junctions only;
a hard link has no marker a link probe can see, other reparse points that redirect nothing (for example cloud placeholders)
are not rejected, and a component swapped between the link walk and the open (a check/use race) is not noticed. These are
documented residual risks (see _SandboxedFileResolver Unit Design_), and no test claims otherwise.

##### CanvasNet-Files-SandboxedFileResolver-ResolverCallback: Callback Mode

**Tests**: `SandboxedFileResolver_Resolve_ResolverModeUsesNormalizedLogicalPathsWithoutFileSystem`,
`SandboxedFileResolver_Resolve_CallbackStreamReadFailureIsCannotRead`,
`SandboxedFileResolver_Resolve_CallbackFailureIsCannotReadAndCancellationPassesThrough`,
`SandboxedFileResolver_Resolve_CallbackThrowingSandboxExceptionIsCannotRead`,
`SandboxedFileResolver_Resolve_StreamDisposeFailureIsCannotRead`

Asserts that the callback receives only normalized logical paths (the root first, then each valid search path as a prefix
in declaration order, invalid search paths ignored) while a same-named file in the base directory is never read, that every
stream it handed out is disposed, that every request came from the calling thread, and that an escape above the logical
root is refused without a request. It also asserts the `CannotRead` contract of the callback boundary: a stream that fails
while it is read, a callback that throws, and a stream whose disposal throws after a complete read are each a failure that
keeps the cause as the inner exception and does not copy the callback's own text into the message, a callback or stream
that throws a `SandboxException` of its own (here a forged link failure) is still `CannotRead` with the forged exception
as the cause, the failing stream is
disposed exactly once, and a cancellation raised by the callback passes through unchanged as `OperationCanceledException`.

##### CanvasNet-Files-SandboxedFileResolver-Reading: Directories, Open Failures, And Shared Access

**Tests**: `SandboxedFileResolver_Resolve_ExistingDirectoryAtFilePathIsCannotRead`,
`SandboxedFileResolver_Resolve_ExistingFileOpenFailureIsCannotRead`,
`SandboxedFileResolver_Resolve_FileNotFoundFromOpenIsMissingAndTriesNextRoot`,
`SandboxedFileResolver_Resolve_MissingFinalComponentIsDecidedByTheOpen`,
`SandboxedFileResolver_Resolve_FileHeldOpenByWriterIsStillRead`

Asserts that an existing directory where the file is expected is `CannotRead` (it exists, so it is not missing) without
a host directory in the message. The open of an existing file is made to fail through the injected opener, so no file
permission, ACL, or lock is involved. As a theory over `UnauthorizedAccessException` and `IOException`, with a real
temporary file in the base directory and another in a search path, the open fails and the resolve asserts `CannotRead` with
the file's key and the cause, and that only the first root was opened (the search ends). With the opener reporting
`FileNotFoundException` (and `DirectoryNotFoundException`) for the first root, the candidate is missing: the base directory
is tried first, then the search path in declaration order, whose file is returned; with no other root the result is `null`.
With an opener that records and delegates to the file system, a missing file whose directory exists reaches the open (the
open decides it is missing) while a file below a missing directory never does (the walk stops that candidate). A file held
open by another handle for reading and writing with the most permissive sharing is still read, which proves the default
opener's sharing mode. No test asserts an operating-system-specific lock.

##### CanvasNet-Files-SandboxedFileResolver-SizeLimit: Bounded Reads

**Tests**: `SandboxedFileResolver_Resolve_MaxFileBytesAtLimitAndOneOver`,
`SandboxedFileResolver_Resolve_EndlessStreamStopsAtBound`

Asserts, for virtual and real files alike, that a file of exactly the limit (10 bytes) is accepted and one of 11 bytes is
`TooLarge`, and that a stream that never ends, with a limit of 20,000 bytes, is refused after at most the limit plus one
chunk of 8 KiB was read.

##### CanvasNet-Files-SandboxedFileResolver-FileLimit: Distinct File Limit

**Tests**: `SandboxedFileResolver_Resolve_MaxFilesAtLimitAndOneOver`,
`SandboxedFileResolver_Resolve_MaxFilesOnFileSystemNeverOpensTheExtraFile`,
`SandboxedFileResolver_Resolve_OversizedFileCountsAndIsRememberedWithMaxFiles`

Asserts with a limit of two that two distinct files are read, that a file resolved again (also under another spelling)
and a file that does not exist do not count, and that the first new file over the limit is `TooManyFiles` naming that
file while the callback was never asked for it (the recorded requests stop at the last file before the limit). On the file
system, with a limit of one and an injected opener, the second file is refused and never opened, while the first is still
served from the cache. With a limit of one and files longer than the size limit, in callback and file-system mode, the
oversized file is `TooLarge` and counts, so a second file is `TooManyFiles` and never requested or opened, and asking for
the oversized file again is `TooLarge` again with no second request or open.

##### CanvasNet-Files-SandboxedFileResolver-Cache: Per-Instance Cache

**Tests**: `SandboxedFileResolver_Resolve_CacheRequestsEachCandidateOncePerResolver`,
`SandboxedFileResolver_Resolve_FileSystemFilesAreCachedPerResolver`,
`SandboxedFileResolver_Resolve_FileCachedUnderLinkedSearchRootIsCheckedAgainForAnotherRoot`,
`SandboxedFileResolver_Resolve_CallbackSeesALogicalPathOnceAcrossRoots`,
`SandboxedFileResolver_KeyComparer_FollowsModeAndPlatform`

Asserts that a file resolved twice is requested once and is the same object, that a miss is remembered (requested once per
candidate), that a new resolver starts with an empty cache, that in file-system mode a file changed after its first
read is not read again, also when it is asked for under another spelling, that a file first resolved through a trusted
search path that is itself a link (injected probe) is **not** served from the cache when the same file is requested
again relative to a file under the base directory - the link check of that root runs and refuses it as `ThroughLink` -
that in callback mode `lib/a.txt` and then `a.txt` with search path `lib` ask the callback for `lib/a.txt` once while
the two results carry their own roots (empty and `lib`), and that the key comparer is ordinal for
logical paths and
case-insensitive for full paths exactly on Windows and macOS.

##### CanvasNet-Files-SandboxedFileResolver-Cancellation: Cancellation

**Tests**: `SandboxedFileResolver_Resolve_CanceledTokenStopsBeforeAnyRequest`

Asserts that an already canceled token throws `OperationCanceledException` before any request reaches the callback.

##### CanvasNet-Files-SandboxedFileResolver-Failures: Exception Contract And Argument Validation

**Tests**: `SandboxException_Constructor_EveryKind_KeepsFactsAndHidesHostPath`,
`SandboxException_Constructor_NullPath_ThrowsArgumentNullException`,
`SandboxedFileResolver_Constructor_InvalidArguments_Throw`

Asserts, for every failure kind, that the exception keeps its kind, path, detail, and inner exception, that its message
is never empty, contains the path only for the path-text kinds and `OutsideRoot`, and never contains a host
directory, that a
null path is an `ArgumentNullException`, and that a null options object, a null search path list, a zero or negative
size limit and a negative file limit are rejected at construction.
