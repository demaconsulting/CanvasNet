### DataFileResolver Unit Verification Design

This document describes the unit-level verification strategy for the `DataFileResolver` unit: the internal
`DataFileResolver` (search, sandbox, link walk, bounded strict-UTF-8 reads, per-call cache, and the data-file limits)
and the `DataFile` record it returns, in the `DemaConsulting.CanvasNet.VegaLite.Data` namespace.

#### Verification Approach

The unit is verified by calling `DataFileResolver.Load` directly with a location of the test's choosing, and asserting
on the returned `DataFile` (its decoded text, its display name, and its identity), on the requests that reached a file
source, and on the `VegaLiteRenderException`s thrown (their message and their location). Two kinds of file source are
used, and both are real implementations of the unit's contract rather than mocks:

- **Resolver mode** uses `VirtualDataFiles` (in `DataTestSupport.cs`), an in-memory file set exposed as the
  `FileResolver` delegate. It records every logical path it is asked for, the thread of each request, and the streams
  it handed out (so that disposal can be asserted), so the tests can assert exactly which paths reached the callback,
  that hostile paths never did, that a miss is asked once, and that no file-system access happens (the scenarios run
  with a base directory whose same-named file must never be read).
- **File-system mode** uses `DataTempDirectory`, a per-test temporary directory tree that the test creates and deletes,
  for real files and folders, so that base-directory-then-data-path order, normalization, byte limits, and the cache
  are proven against the real file system.

The link rule (a symbolic link or junction below a root is rejected, whatever its target) is verified in two layers.
The **cited** scenarios (`DataFileResolverLinkTests`) inject a fake link probe through the resolver's internal
constructor seam (`Func<string, bool>`): the fake reports chosen paths of real temporary files and directories as
links, so the walk logic (top-down order, existing components only, caching, trusted root, fail-closed probe, resolver
mode never probing, dot-dot normalization) is proven on every platform without creating a link, and these tests never
skip. The **not cited** scenario creates real links (a symbolic link through the framework and, on Windows, a junction
through `mklink /J`) and so additionally proves the default probe against the real file system; it skips itself only
where the host cannot create links and is listed as additional evidence only.

Limits are verified at their exact boundary (the limit accepted and one over), the endless stream with a stream that
never ends and counts the bytes it was asked for, and cancellation with a token that is already canceled. No timing is
asserted.

Unit tests reside in `DataFileResolverTests.cs` and `DataFileResolverLinkTests.cs` within the
`DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Data` folder, with the shared helpers (`DataTempDirectory`,
`VirtualDataFiles`, `EndlessDataStream`, and `TrackedStream`) in `DataTestSupport.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None; the in-memory file set, the temporary directories, the injected link probe, and the injected file
  opener (which raises a chosen open failure or hands out a real stream) are real implementations of the unit's seams
- **Files**: Per-test temporary directories under the system temporary folder, deleted by the test; no checked-in file
  is read by these tests
- **Threads**: The resolver-mode scenario asserts that every request came from the calling thread
- **Isolation**: Each test builds its own file set or directory and its own resolver, as one call does
- **Capabilities**: the real-link scenario needs permission to create a symbolic link or junction and skips otherwise;
  nothing else depends on the host

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A request to the resolver callback that was not expected, a file-system access in resolver mode, an accepted
hostile path, a file read through a link, a wrong location or display name, or a wrong exception type or message
constitutes a failure.

#### Unit-Level Test Scenarios

##### Search Order And Normalization

**Tests**: `DataFileResolverTests.DataFileResolver_Load_RelativePathUsesBaseDirectoryThenDataPaths`,
`DataFileResolverTests.DataFileResolver_Load_DotDotInsideRootNormalizesAndEscapeIsRejected`

Asserts that the same name in the base directory and in data paths is taken from the base directory, that a name only in
a later data path is found there, that the result carries the decoded text and the path as written, that `.` and `..`
segments inside the root (also with backslashes) name one file, and that a `..` that leaves the root, directly or
after a
detour, is refused with "outside the allowed directories" even though the file exists, with the location in the
message.

##### Hostile Path Text Is Refused Before Any Access

**Tests**: `DataFileResolverTests.DataFileResolver_Load_HostilePathTextIsRejectedWithoutAnyAccess`

Asserts, as a theory over an empty path, white space, a Unix absolute path, a rooted backslash path, UNC paths in both
slash styles, a drive path, a drive-relative path, a colon inside a name, NUL, a line feed, and DEL, that each is refused
as "not allowed" with the location, and that the callback received no request.

##### Resolver Mode

**Tests**: `DataFileResolverTests.DataFileResolver_Load_ResolverModeUsesNormalizedLogicalPathsWithoutFileSystem`,
`DataFileResolverTests.DataFileResolver_Load_CallbackStreamReadFailureIsLocated`,
`DataFileResolverTests.DataFileResolver_Load_CallbackFailureIsLocatedReadFailure`,
`DataFileResolverTests.DataFileResolver_Load_StreamDisposeFailureIsLocatedReadFailure`

Asserts that the callback receives only normalized logical paths (the root first, then each valid data path as a prefix
in declaration order, invalid data paths ignored) while a same-named file in the base directory is never read, that every
stream it handed out is disposed, that every request came from the calling thread, and that an escape above the logical
root is refused without a request. It also asserts the located read-failure contract of the callback boundary: a stream
that fails while it is read, a callback that throws, and a stream whose disposal throws after a complete read are each a
located "cannot be read" failure (the callback's own text is not leaked), the failing stream is disposed exactly once,
and a cancellation raised by the callback passes through unchanged as `OperationCanceledException`.

##### Missing Files, Directories, Open Failures And No File Source

**Tests**: `DataFileResolverTests.DataFileResolver_Load_MissingFileIsHardFailure`,
`DataFileResolverTests.DataFileResolver_Load_ExistingDirectoryAtFilePathIsLocatedReadFailure`,
`DataFileResolverTests.DataFileResolver_Load_ExistingFileOpenFailureIsLocatedReadFailure`,
`DataFileResolverTests.DataFileResolver_Load_FileNotFoundFromOpenIsMissingAndTriesNextRoot`,
`DataFileResolverTests.DataFileResolver_Load_MissingFinalComponentIsDecidedByTheOpen`,
`DataFileResolverTests.DataFileResolver_Load_StreamWithoutFileSourceThrows`

Asserts that a file that does not exist is a "was not found" failure with the location in both modes and without the
host's directory in the message, that an existing directory where the file is expected is a located "cannot be read"
failure (it exists, so it is not "not found") that also does not reveal the host's directory, and that a resolver with
no base directory, data path, or callback (as a document opened from a string or stream has) says so and reports
`HasFileSource` false.

The open of an existing file is made to fail through the injected opener (`Func<string, Stream>`, the second test seam
of the unit), so no file permission, ACL, or lock is involved and the scenarios behave identically on Windows, Linux,
and macOS. As a theory over `UnauthorizedAccessException` and `IOException`, with a real temporary file in the base
directory and another in a data path, the open fails and the load asserts a located "cannot be read" failure that is not
"not found", carries the location, does not reveal the host's directory, and that only the first root was opened (the
search ends). With the opener reporting `FileNotFoundException` (and `DirectoryNotFoundException`) for the first root,
the candidate is missing: the base directory is tried first, then the data path in declaration order, whose file is
returned; with no other root the failure is "not found". With an opener that records and delegates to the file system,
a missing file whose directory exists reaches the open (the open decides it is missing) while a file below a missing
directory never does (the walk stops that candidate). No test asserts an operating-system-specific lock.

##### Per-Call Cache

**Tests**: `DataFileResolverTests.DataFileResolver_Load_CacheRequestsEachCandidateOncePerCall`,
`DataFileResolverTests.DataFileResolver_Load_FileSystemFilesAreCachedPerCall`

Asserts that a file loaded twice is requested once and is the same object, that a miss is remembered (requested once per
candidate), that a new resolver starts with an empty cache, and that in file-system mode a file changed after its first
read is not read again and no component is probed twice.

##### Data File Limit

**Tests**: `DataFileResolverTests.DataFileResolver_Load_MaxDataFilesAtLimitAndOneOver`

Asserts with a limit of two that two distinct files are read, a file loaded again and a file that does not exist do not
count, and the first file over the limit fails with "the limit of 2 data files (MaxDataFiles)" and the location.

##### Byte Limit

**Tests**: `DataFileResolverTests.DataFileResolver_Load_MaxDataBytesAtLimitAndOneOver`,
`DataFileResolverTests.DataFileResolver_Load_EndlessStreamStopsAtBound`

Asserts, for virtual and real files alike, that a file of exactly the limit (10 bytes) is accepted and one of 11 bytes
fails with "the limit of 10 bytes (MaxDataBytes)", and that a stream that never ends, with a limit of 20,000 bytes, is
refused after at most the limit plus one chunk of 8 KiB was read.

##### Strict UTF-8

**Tests**: `DataFileResolverTests.DataFileResolver_Load_InvalidUtf8IsRejectedStrictly`

Asserts that a stray byte, a truncated multi-byte sequence, and UTF-16 text with its byte-order mark each fail with
"is not
valid UTF-8" and the location, and that valid UTF-8 keeps its characters and loses only the leading byte-order mark.

##### Cancellation

**Tests**: `DataFileResolverTests.DataFileResolver_Load_CanceledTokenStopsBeforeAnyRequest`

Asserts that an already canceled token throws `OperationCanceledException` before any request reaches the callback.

##### Links Are Refused Before The File Is Opened

**Tests**: `DataFileResolverLinkTests.DataFileResolver_Load_LinkedDirectoryIsRejectedBeforeOpen`,
`DataFileResolverLinkTests.DataFileResolver_Load_LinkedFileIsRejectedBeforeOpen`,
`DataFileResolverLinkTests.DataFileResolver_Load_LinkBelowDataPathIsRejected`,
`DataFileResolverLinkTests.DataFileResolver_Load_LinkedRootAndAncestorsAreTrusted`,
`DataFileResolverLinkTests.DataFileResolver_Load_LinkAfterMissingComponentIsNotProbedAndFileIsMissing`,
`DataFileResolverLinkTests.DataFileResolver_Load_DotDotRouteThroughLinkIsJudgedOnNormalizedPath`,
`DataFileResolverLinkTests.DataFileResolver_Load_ReparseProbeFailureFailsClosed`

Asserts (with the fake probe, on every platform) that a linked directory at depth one, two, or three between the root
and the file is rejected before the file is opened and the walk stops at the first link, that a file that is itself a
link is rejected whatever its target while its neighbor is read, that a link below a data path root is rejected too,
that the root and everything above it are trusted even when the probe reports them as links, that the first missing
component ends the walk (the file is missing, nothing below is probed, no link is reported), that a route through
dot-dot is judged on the normalized path (the directory stepped over is not examined, the one entered is), that a
probe that throws rejects the file instead of reading it, and that resolver mode never asks the probe.

Additional evidence, not cited by a requirement and skipped only where the host cannot create links:
`DataFileResolverLinkTests.DataFileResolver_Load_RealLinksAreRejectedOrCapabilitySkipped` creates real links (a
directory link, a directory link inside the root, a file link, and on Windows a junction) and asserts that each is
rejected, that a base directory reached through a link is trusted, and that the real file beside them is read, which
proves the default probe against the real file system. _Not verified, by design:_ the requirement covers symbolic links
and junctions only; a hard link has no marker a link probe can see, other reparse points that redirect nothing (for
example cloud placeholders) are not rejected, and a component swapped between the link walk and the open (a check/use
race) is not noticed. These are documented residual risks (see _DataFileResolver Unit Design_), and no test claims
otherwise: the sandbox protects against path-based escapes in static file trees and is not a security boundary against a
concurrent local attacker with write access to the data folders.
