### DataFileResolver Unit Verification Design

This document describes the unit-level verification strategy for the `DataFileResolver` unit: the internal
`DataFileResolver` (search order, the file-access policy, translation of the core engine's refusals, strict-UTF-8
decoding, per-call cache and the data-file limits) and the `DataFile` record it returns, in the
`DemaConsulting.CanvasNet.VegaLite.Data` namespace.

#### Verification Approach

The unit is verified by calling `DataFileResolver.Load` directly with a location of the test's choosing, and asserting
on the returned `DataFile` (its decoded text, its display name, and its identity), on the requests that reached a file
source, and on the `VegaLiteRenderException`s thrown (their message, their location, and their inner exception). The
file-access mechanics that the unit delegates to the core `FileReferenceResolver` (path-text rules, the real path of the
open handle, the race-free check, the policies, the open and bounded-read rules, the callback boundary, the cache) are
verified once, at the core units, with seams that this project cannot reach (they are internal to the core package);
see _FileReferenceResolver Unit Verification Design_ and _FileAccessPolicy Unit Verification Design_. What this unit
proves is its own layer:

- **The policy of the call.** Files are read under `VegaLiteOpenOptions.FileAccess`: by default `AllowAll`, so `..`,
  absolute paths, and links are read; under an explicit `UnderRoots` or `NoSymlinks` policy an escape or a link is a
  located denial.
- **Wording and location.** `Translate` is called directly with a `FileReferenceException` of every failure kind and
  asserts the Vega-Lite message, the location, the refusal kept as the inner exception, and that no host directory
  appears. Hostile paths and `..` escapes are also sent through `Load` in resolver mode and file-system mode, so the
  end-to-end path from the engine to the located error is proven.
- **Two kinds of file source** are used, and both are real implementations of the unit's contract rather than mocks.
  _Resolver mode_ uses `VirtualDataFiles` (in `DataTestSupport.cs`), an in-memory file set exposed as the `FileResolver`
  delegate that records every logical path it is asked for, the thread of each request, and the streams it handed out.
  _File-system mode_ uses `DataTempDirectory`, a per-test temporary directory tree that the test creates and deletes.
- **Real links.** Three scenarios create real links (directory links are junctions on Windows, which need no privilege;
  file links are symbolic links, created only where the host allows it) and assert that the default policy follows a
  link, that `NoSymlinks` denies it with a located error naming the policy and not the host directory, and that a
  dangling link is "not found" like a missing file under either policy.

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
- **Capabilities**: directory links fall back to a junction on Windows, so the directory-link scenarios never skip; the
  file-link parts skip (or, inside a scenario, are left out) where file symbolic links cannot be created (Windows without
  the privilege); nothing else depends on the host

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A request to the resolver callback that was not expected, a file-system access in resolver mode, an accepted
hostile path, a file read that the policy denied, an allowed file that is not read, a wrong location or display name,
or a wrong exception type or message constitutes a failure.

#### Unit-Level Test Scenarios

##### Search Order And Normalization

**Tests**: `DataFileResolverTests.DataFileResolver_Load_RelativePathUsesBaseDirectoryThenDataPaths`,
`DataFileResolverTests.DataFileResolver_Load_DotDotInsideRootNormalizesAndEscapeIsRejected`,
`DataFileResolverTests.DataFileResolver_Load_DotDotOutsideTheBase_IsReadByDefault`,
`DataFileResolverTests.DataFileResolver_Load_CallbackMode_AbsolutePathIsALocatedError`

Asserts that the same name in the base directory and in data paths is taken from the base directory, that a name only in
a later data path is found there, that the result carries the decoded text and the path as written, that `.` and `..`
segments inside the root (also with backslashes) name one file, and that, under `UnderRoots(root)`, a `..` that leaves
the root, directly or after a detour, is denied with "was denied by the file-access policy 'roots'" even though the
file exists, with the location in the message and the `FileReferenceException` as inner exception. Under the default
policy a `..` that leaves the base directory and the absolute path of the same file are read, once. In resolver-callback
mode an absolute path is the located error "absolute paths have no meaning for the file source" and never reaches the
callback.

##### Hostile Path Text Is Refused Before Any Access

**Tests**: `DataFileResolverTests.DataFileResolver_Load_HostilePathTextIsRejectedWithoutAnyAccess`,
`DataFileResolverTests.DataFileResolver_Translate_EveryFailureKind_ProducesLocatedWording`,
`DataFileResolverTests.DataFileResolver_Load_InvalidConfiguredDirectoryIsLocatedFailure`

Asserts, in resolver mode and as a theory over an empty path, white space, a Unix absolute path, a rooted backslash
path, UNC paths in both slash styles, a drive path, a drive-relative path, a colon inside a name, NUL, a line feed, DEL,
and C1 control characters, that each is refused as "not allowed" with the location, and that the callback received no
request. The translation test asserts the Vega-Lite wording, the location, the refusal as inner exception, and the
absence of any host directory for all 11 failure kinds (four path-text kinds including a network path, denied by the
policy, real path unavailable, not a regular file, cannot be read, too large, invalid configured directory, too many
files); a base directory that is not a valid path is a located failure.

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
host's directory in the message, that an existing directory where the file is expected is a located "is not a regular
file" failure (it exists, so it is not "not found") that also does not reveal the host's directory, and that a resolver
with
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
count, and the first file over the limit fails with "the limit of 2 data files (MaxDataFiles)" and the location. The
file over the limit is refused by the core engine before it is read: the callback records its requests and is never
asked for it. The translation of the engine's `TooManyFiles` kind to this wording is also asserted by the translation
scenario above (`DataFileResolver_Translate_EveryFailureKind_ProducesLocatedWording`).

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

##### Links Follow The File-Access Policy

**Tests**: `DataFileResolverTests.DataFileResolver_Translate_EveryFailureKind_ProducesLocatedWording`,
`DataFileResolverLinkTests.DataFileResolver_Load_RealLinksAreRejectedOrCapabilitySkipped`,
`DataFileResolverLinkTests.DataFileResolver_Load_RealLinksAreFollowedByDefaultOrCapabilitySkipped`,
`DataFileResolverLinkTests.DataFileResolver_Load_RealDanglingLinkIsNotFoundOrCapabilitySkipped`

Asserts that a `FileReferenceException` of the denied kind (and of the real-path-unavailable kind) becomes the located
"was denied by the file-access policy" (or "cannot be checked") message naming the file as written and not the host
directory. With real links (directory links are junctions on Windows, so these parts never skip; the file-link part runs
only where file links can be created), under `NoSymlinks` a directory link that leaves the root, a directory link inside
the root, and a file link are each denied with the located message "The data file '…' was denied by the file-access
policy 'no-symlinks'" and no host directory, a base directory reached through a link is trusted, and the real file
beside the links is read; under the default policy the same directory links and the file link are followed and read;
and a dangling file link is "was not found", like a truly missing file, under the default policy and under
`NoSymlinks` (this scenario skips where file links cannot be created). _Not verified, by design:_ a hard link has no
marker that tells it from a regular file, and its real path is the path of the link, so under `UnderRoots` a hard link
inside a root to a file outside it is read; this is a documented residual risk (see _DataFileResolver Unit Design_), and
no test claims otherwise. A component swapped for a link around the open cannot change the decision: the core engine
judges the real path of the file it opened, which is covered at the core unit
(`CanvasNet-Files-FileReferenceResolver-RaceFree`).
