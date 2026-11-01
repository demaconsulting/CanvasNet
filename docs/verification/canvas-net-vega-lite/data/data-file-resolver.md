### DataFileResolver Unit Verification Design

This document describes the unit-level verification strategy for the `DataFileResolver` unit: the internal
`DataFileResolver` (search order, translation of the core sandbox's refusals, strict-UTF-8 decoding, per-call cache and
the data-file limits) and the `DataFile` record it returns, in the `DemaConsulting.CanvasNet.VegaLite.Data` namespace.

#### Verification Approach

The unit is verified by calling `DataFileResolver.Load` directly with a location of the test's choosing, and asserting
on the returned `DataFile` (its decoded text, its display name, and its identity), on the requests that reached a file
source, and on the `VegaLiteRenderException`s thrown (their message and their location). The sandbox mechanics that the
unit delegates to the core `SandboxedFileResolver` (escape and path-text rules, the link walk and its fail-closed probe,
the dangling-link rule, the open and bounded-read rules, the callback boundary, the cache) are verified once, at the
core unit, with an injected link probe and open seam that this project cannot reach (they are internal to the core
package); see _SandboxedFileResolver Unit Verification Design_ (`canvas-net/files/sandboxed-file-resolver.md`). What
this unit proves is its own layer:

- **Wording and location.** `Translate` is called directly with a `SandboxException` of every failure kind and asserts
  the Vega-Lite message, the location, and that no host directory appears. Hostile paths and `..` escapes are also sent
  through `Load` in resolver mode and file-system mode, so the end-to-end path from the sandbox to the located error is
  proven.
- **Two kinds of file source** are used, and both are real implementations of the unit's contract rather than mocks.
  _Resolver mode_ uses `VirtualDataFiles` (in `DataTestSupport.cs`), an in-memory file set exposed as the `FileResolver`
  delegate that records every logical path it is asked for, the thread of each request, and the streams it handed out.
  _File-system mode_ uses `DataTempDirectory`, a per-test temporary directory tree that the test creates and deletes.
- **Real links.** Two scenarios create real links (a symbolic link through the framework and, on Windows, a junction
  through `mklink /J`) and assert that a real link surfaces as the located "passes through a link" error without the host
  directory, and that a dangling link is a link error while a missing file is "not found". They skip themselves only
  where the host cannot create links, so a requirement cannot cite them (a skipped test is not evidence); they are
  additional evidence.

Limits are verified at their exact boundary (the limit accepted and one over), the endless stream with a stream that
never ends and counts the bytes it was asked for, and cancellation with a token that is already canceled. No timing is
asserted.

Unit tests reside in `DataFileResolverTests.cs` and `DataFileResolverLinkTests.cs` within the
`DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Data` folder, with the shared helpers (`DataTempDirectory`,
`VirtualDataFiles`, `EndlessDataStream`, and `TrackedStream`) in `DataTestSupport.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None; the in-memory file set and the temporary directories are real implementations of the unit's file
  sources
- **Files**: Per-test temporary directories under the system temporary folder, deleted by the test; no checked-in file
  is read by these tests
- **Threads**: The resolver-mode scenario asserts that every request came from the calling thread
- **Isolation**: Each test builds its own file set or directory and its own resolver, as one call does
- **Capabilities**: the real-link scenarios need permission to create a symbolic link or junction and skip otherwise;
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
after a detour, is refused with "outside the allowed directories" even though the file exists, with the location in the
message.

##### Hostile Path Text Is Refused Before Any Access

**Tests**: `DataFileResolverTests.DataFileResolver_Load_HostilePathTextIsRejectedWithoutAnyAccess`,
`DataFileResolverTests.DataFileResolver_Translate_EverySandboxKind_ProducesLocatedWording`,
`DataFileResolverTests.DataFileResolver_Load_InvalidConfiguredDirectoryIsLocatedFailure`

Asserts, as a theory over an empty path, white space, a Unix absolute path, a rooted backslash path, UNC paths in both
slash styles, a drive path, a drive-relative path, a colon inside a name, NUL, a line feed, DEL, and C1 control
characters, that each is refused as "not allowed" with the location, and that the callback received no request. The
translation test asserts the Vega-Lite wording, the location, and the absence of any host directory for every sandbox
failure kind (path text, outside the root, through a link, link check failed, cannot be read, too large, invalid
configured directory); a base directory that is not a valid path is a located failure.

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

##### Missing Files, Directories And No File Source

**Tests**: `DataFileResolverTests.DataFileResolver_Load_MissingFileIsHardFailure`,
`DataFileResolverTests.DataFileResolver_Load_ExistingDirectoryAtFilePathIsLocatedReadFailure`,
`DataFileResolverTests.DataFileResolver_Load_StreamWithoutFileSourceThrows`

Asserts that a file that does not exist is a "was not found" failure with the location in both modes and without the
host's directory in the message, that an existing directory where the file is expected is a located "cannot be read"
failure (it exists, so it is not "not found") that also does not reveal the host's directory, and that a resolver with
no base directory, data path, or callback (as a document opened from a string or stream has) says so and reports
`HasFileSource` false. That an existing file whose open fails is "cannot be read" and never "not found", that a file
reported missing by the open lets the search continue, and that files are opened with shared access are verified at the
core unit, where the open can be injected.

##### Per-Call Cache

**Tests**: `DataFileResolverTests.DataFileResolver_Load_CacheRequestsEachCandidateOncePerCall`,
`DataFileResolverTests.DataFileResolver_Load_FileSystemFilesAreCachedPerCall`

Asserts that a file loaded twice is requested once and is the same object, that a miss is remembered (requested once per
candidate), that a new resolver starts with an empty cache, and that in file-system mode a file changed after its first
read is not read again, also when it is asked for under another spelling.

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

##### Links Are Refused With A Located Error

**Tests**: `DataFileResolverTests.DataFileResolver_Translate_EverySandboxKind_ProducesLocatedWording`

Asserts that a `SandboxException` of the through-a-link kind (and of the link-check-failed kind) becomes the located
"passes through a link" (or "cannot be checked for links") message naming the link relative to the root and not the host
directory. Additional evidence, not cited by a requirement because the tests skip themselves where the host cannot
create links (`DataFileResolverLinkTests.DataFileResolver_Load_RealDanglingLinkIsRejectedOrCapabilitySkipped` and
`DataFileResolverLinkTests.DataFileResolver_Load_RealLinksAreRejectedOrCapabilitySkipped`): with real links, a directory
link that leaves the root, a directory link inside the root, a file link and (on Windows) a junction are each refused with
the located "passes through a link" message naming the link relative to the root and not the host directory, a dangling
file link is a link error while a truly missing file is "was not found", a base directory reached through a link is
trusted, and the real file beside the links is read. The walk itself - top-down order, existing components only,
caching, trusted root, fail-closed probe, resolver mode never probing, dot-dot normalization - is proven on every
platform at the core unit with an injected probe. _Not verified, by design:_ the requirement covers symbolic links and
junctions only; a hard link has no marker a link probe can see, other reparse points that redirect nothing (for example
cloud placeholders) are not rejected, and a component swapped between the link walk and the open (a check/use race) is
not noticed. These are documented residual risks (see _DataFileResolver Unit Design_), and no test claims otherwise: the
sandbox protects against path-based escapes in static file trees and is not a security boundary against a concurrent
local attacker with write access to the data folders.
