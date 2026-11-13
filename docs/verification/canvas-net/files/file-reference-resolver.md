### FileReferenceResolver Unit Verification Design

This document describes the unit-level verification strategy for the `FileReferenceResolver` unit: the public
`FileReferenceResolver` and its `FileReferenceOptions`, `FileReference`, `FileReferenceException`, and
`FileReferenceFailureKind` types, in the `DemaConsulting.CanvasNet.Files` namespace.

#### Verification Approach

The unit is verified by calling `FileReferenceResolver.Resolve` (and the internal `ResolveTopLevel`) directly and
asserting on the returned `FileReference` (key, real path, directory, bytes), on the requests that reached the policy
and the callback, on the paths that were opened, and on the `FileReferenceException`s thrown (kind, reference, reason,
hint, policy name, paths, inner exception, message). Real temporary files and folders (`SandboxTempDirectory`) are used
for the file system, the in-memory `SandboxVirtualFiles` for callback mode, `RecordingPolicy` and
`FileAccessPolicy.FromDelegate` for policies whose answers the test controls, and the built-in policies where their
real behavior matters.

The internal `FileOpenHooks` seams, supplied through an internal constructor that the test project reaches through the
core assembly's `InternalsVisibleTo` grant, make the open and the handle deterministic without any privilege, lock or
timing: `BeforeOpen` records the paths that are opened, `Open` replaces the opener (to raise an open failure, report
"not found", or hand in a device handle), `AfterOpen` changes the directory tree between the open and the check, and
`RealPathOf` replaces the real path reported for the handle (an outside path, or none).

The race-free check is proven without timing in `FileReferenceResolverRaceTests`: inside `AfterOpen` the opened file is
moved away and its folder replaced by a junction or symbolic link to an outside folder (Windows does not allow renaming
a folder above an open file, but it allows moving the file itself and deleting the emptied folder because the file is
opened with delete sharing), or a link to an outside folder is removed and replaced by a real inside folder. Real links
are created in `FileReferenceResolverLinkTests` with a directory symbolic link or, on Windows without the privilege, an
NTFS junction (`mklink /J`), so these scenarios run on every system; only the file-link scenario skips on Windows when
the host cannot create one, and its decision is also covered on every system by the injected real-path scenarios.

Limits are verified at their exact boundary, the endless stream with a stream that never ends, and cancellation with a
token that is already canceled. The only wait is a hang guard (one minute) around the named-pipe scenario, which fails
the test if the open blocks; no duration is asserted.

Unit tests reside in `FileReferenceResolverTests.cs`, `FileReferenceResolverLinkTests.cs`,
`FileReferenceResolverRaceTests.cs`, and `FileReferenceExceptionTests.cs` in the `Files` folder of the
`DemaConsulting.CanvasNet.Tests` project, with helpers in `FileTestSupport.cs` and `SandboxTestSupport.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline on Windows, Linux, and macOS
- **Mocking**: None; the in-memory file set, the temporary directories, the recording policy, and the open hooks are
  real implementations of the unit's seams
- **Files**: per-test temporary directories, deleted by the test
- **Operating-system scenarios**: the null device (Windows), `/dev/null` and named pipes created with `mkfifo` (Linux
  and macOS); each skips on the other systems and is linked with a source filter

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A byte read before the policy allowed it, a denial that does not stop the search, a refusal reported as
missing, a host directory in an exception message, a wrong kind, or a blocked open constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNet-Files-FileReferenceResolver-Candidates: Search Order, Dot-Dot, and Absolute References

**Tests**: `FileReferenceResolver_Resolve_RelativePathUsesReferrerThenBaseThenSearchPaths`,
`FileReferenceResolver_Resolve_DotDotLeavingTheBase_IsReadUnderAllowAll`,
`FileReferenceResolver_Resolve_AbsolutePath_IsASingleCandidateAndIsRead`,
`FileReferenceResolver_Resolve_BackslashesAndDotSegments_NameOneFile`,
`FileReferenceResolver_Resolve_DrivePath_IsAbsoluteOnWindowsAndInvalidElsewhere`

The base directory wins over the search paths, a later search path is used when earlier ones miss, a referring file's
directory comes first, and the result carries the key and directory; `..` leaving the base directory is read; an
absolute reference is read and, when missing, opened once and not looked up on the search paths; backslashes and dot
segments give the same instance; a drive path is absolute on Windows and invalid on Linux and macOS.

##### CanvasNet-Files-FileReferenceResolver-PathText: Text That Is Never Allowed

**Tests**: `FileReferenceResolver_Resolve_HostilePathText_IsRejectedWithoutAnyAccess`,
`FileReferenceResolver_Resolve_NullPath_ThrowsArgumentNullException`,
`FileReferenceResolver_Resolve_LegacyDeviceName_IsInvalidPathAndNoDeviceIsOpened` (Windows),
`FileReferenceResolver_Resolve_LegacyDeviceNameWithExtendedLengthBaseDirectory_IsInvalidPathAndNoDeviceIsOpened`
(Windows)

A theory over empty and white-space text, NUL, a line feed, DEL, a C1 control, an alternate data stream, a
drive-relative path, UNC paths in both slash styles, and `\\?\` and `\\.\` device paths asserts the matching kind in
file-system and callback mode, a printable reference, a message that says the path is not allowed, and that nothing was
opened or requested. A null reference is an `ArgumentNullException`. On Windows a theory over `x/NUL`, `NUL`,
`x/NUL` (trailing space), `x/CON`, `x/COM1` and `x/LPT1` asserts that a name the running Windows version maps to a
device (`Path.GetFullPath` gives `\\.\`) is `InvalidPath` with the reason "the path names a device" and nothing
opened, that a name it does not map is an ordinary missing file, that `x/NUL` is `InvalidPath` on every version, and
that no device path is ever opened. With an extended-length base directory (`\\?\` in front of the temporary
directory), a theory over the absolute references `C:/x/NUL` and `C:/x/nul` (trailing space), which the system maps
to `\\.\NUL`, asserts `InvalidPath` with the reason "the path names a device", and that a file under the base directory
is still read and is the only thing opened.

##### CanvasNet-Files-FileReferenceResolver-MissingFiles: Not Found Is Not a Failure

**Tests**: `FileReferenceResolver_Resolve_MissingFileAndDanglingLink_ReturnNull`,
`FileReferenceResolver_Resolve_FileNotFoundFromOpen_TriesTheNextCandidate`,
`FileReferenceResolver_Resolve_NoSource_ReturnsNullAndHasSourceIsFalse`

A missing file and a dangling directory link are `null`, and with a search path that has the file the search falls
through to it; "not found" from the open tries the next candidate; a resolver without a source finds nothing.

##### CanvasNet-Files-FileReferenceResolver-PolicyEvaluation: What the Policy Is Asked and How Its Answer Is Used

**Tests**: `FileReferenceResolver_Resolve_PolicyDenial_IsAccessDeniedWithFactsAndNothingRead`,
`FileReferenceResolver_Resolve_PolicyDenial_StopsTheSearch`,
`FileReferenceResolver_Resolve_ThrowingOrNullPolicy_IsAccessDeniedAndCancellationPassesThrough`,
`FileReferenceResolver_Resolve_CachedFile_IsReCheckedOnEveryResolveButReadOnce`,
`FileReferenceResolver_Resolve_UnderRootsPreCheck_DeniesBeforeTheOpen`

The request carries the reference, the configured referrer (top level) or the referring file's key, the base directory
(the chain origin, also for the nested reference), the requested path, and the real path (the real location of the
folder plus the file name); a denial carries the reason, hint, policy name and both paths, and its message names the
policy but neither the temporary directory nor the reason; a denial ends the search; a throwing or `null`-returning
policy denies and a cancellation passes through; a cached file is opened once while the policy is asked on every resolve
and a later denial wins; a roots policy refuses a path written outside its roots before the open, which does not count
against the file limit.

##### CanvasNet-Files-FileReferenceResolver-ChainOrigin: The Policy's Base Directory Is the Chain Origin

**Tests**: `FileReferenceResolver_Resolve_ReferrerFolderSwappedForLinkAfterItWasRead_IsAccessDeniedUnderNoSymlinks`
(Windows, Linux and macOS), `FileReferenceResolver_Resolve_NestedReference_RequestBaseDirectoryIsTheChainOrigin`,
`FileReferenceResolver_Resolve_RelativeToFromAnotherResolver_ThrowsArgumentException`

A document is read from `base/sub` under `NoSymlinks`; just before the referenced open (a `BeforeOpen` seam that fires
only for the referenced file), `base/sub` is replaced by a directory link to an outside folder holding the referenced
name: the nested reference is `AccessDenied` by `no-symlinks` and nothing is returned (had the anchor been taken from
the referrer's folder, which is the defect this test guards against, the reference would have been allowed). With a
base directory and a search
path, a file found under the search path and a reference relative to it both give the policy the search path as the
base directory, and the second request names the first file as the referrer. A file returned by one resolver is an
`ArgumentException` (parameter `relativeTo`) as the starting point of another resolver over the same tree, even one
that has read the same file, while that resolver's own file is accepted.

##### CanvasNet-Files-FileReferenceResolver-TopLevelFile: The Host's Top-Level File Is Not Judged

**Tests**: `FileReferenceResolver_ResolveTopLevel_DenyingPolicy_ReadsTheFileAndChecksItsReferences`,
`FileReferenceResolver_ResolveTopLevel_Directory_IsNotARegularFile`,
`FileReferenceResolver_ResolveTopLevel_TakesAFileSlot`

The internal `ResolveTopLevel` (reached through `InternalsVisibleTo`) reads a top-level file outside the base directory
under a policy that denies everything, returns the same instance for its absolute path, and returns `null` for a
missing file; a reference inside it and a reference back to it are `AccessDenied`, and the policy is asked only for
those two, with the top-level file's folder as the base directory. A directory is `NotARegularFile`. The top-level file
takes a slot of the file limit: with `MaxFiles = 2` it and one reference are read and a second reference is
`TooManyFiles` without being opened; callback mode has no top-level file (`InvalidOperationException`).

##### CanvasNet-Files-FileReferenceResolver-NetworkTarget: A Link to a Share Is Refused

**Tests**: `FileReferenceResolver_Resolve_LocalPathWhoseRealPathIsANetworkShare_IsNetworkPathUnderEveryPolicy`
(Windows)

A real local file whose real path is reported (through the `RealPathOf` seam) as `\\server\share\a.txt` is
`NetworkPath` under `AllowAll`, `NoSymlinks` and `UnderRoots`, with that real path, the reason "it leads to a network
location", and no temporary directory in the message; a second resolve fails the same way without opening the file
again. A real link to a share and a mapped network drive cannot be created on CI, so the exemption of a written path
that is itself a network location is covered by `RealPath-NetworkLocation` and by review.

##### CanvasNet-Files-FileReferenceResolver-FileUri: Classifying File URIs

**Tests**: `FileReferenceResolver_ParseFileUri_ClassifiesFileUris`,
`FileReferenceResolver_ParseFileUri_ShortAndLongFormGiveTheSamePath`

`a.png` and `http://h/a.png` are not `file:` URIs; `file:///tmp/a.png`, `file:/tmp/a.png`, `FILE:/tmp/a.png` and
`file:/C:/a.png` are local with a local path; `file://localhost/a.png`, `file://server/share/a.png` and
`file:////server/share/a.png` have a host and no local path; `file://[/a.png` and `file:` are malformed with no local
path. The short form `file:/tmp/a%20b.png` and the long form `file:///tmp/a%20b.png` give the same, percent-decoded
local path on every platform.

##### CanvasNet-Files-FileReferenceResolver-RaceFree: The Decision Follows the Open Handle

**Tests**: `FileReferenceResolver_Resolve_FileMovedAndFolderSwappedForOutsideLinkAfterOpen_IsAllowedWithOriginalBytes`,
`FileReferenceResolver_Resolve_OutsideLinkReplacedByInsideFolderAfterOpen_IsAccessDeniedUnderUnderRoots`,
`FileReferenceResolver_Resolve_OutsideLinkReplacedByInsideFolderAfterOpen_IsAccessDeniedUnderNoSymlinks`,
`FileReferenceResolver_Resolve_InjectedOutsideRealPath_IsAccessDenied`,
`FileReferenceResolver_Resolve_InjectedRealPath_IsTheOneInTheRequestAndTheResult`

After the open, the inside file is moved and its folder replaced by a link to the outside: the file is allowed, its
original bytes are returned, its real path follows the moved file, and by name the path now reads the outside file.
After the open, an outside link is replaced by a benign inside folder: the file is denied under `UnderRoots` (with the
outside real path) and under `NoSymlinks`. On a plain tree, an injected outside real path is denied by both policies,
and an injected real path is the one in the request and in the result.

##### CanvasNet-Files-FileReferenceResolver-RealPathUnavailable: Fail Closed Without a Real Path

**Tests**: `FileReferenceResolver_Resolve_RestrictivePolicyWithoutRealPath_IsRealPathUnavailable`

With no real path reported, `NoSymlinks` refuses the file as `RealPathUnavailable` naming the policy, while the default
policy reads it and the result has no real path.

##### CanvasNet-Files-FileReferenceResolver-RegularFile: Only Regular Files Are Read

**Tests**: `FileReferenceResolver_Resolve_DirectoryAtFilePath_IsNotARegularFile`,
`FileReferenceResolver_Resolve_UnknownFileType_IsNotARegularFileUnderEveryPolicy`,
`FileReferenceResolver_Resolve_InjectedNulHandle_IsNotARegularFile` (Windows),
`FileReferenceResolver_Resolve_DevNull_IsNotARegularFile` (Linux, macOS),
`FileReferenceResolver_Resolve_Fifo_IsNotARegularFileWithoutBlocking` (Linux, macOS)

A directory, the Windows null device handed in through the open seam, `/dev/null`, and a named pipe without a writer
are `NotARegularFile`; the pipe's resolve returns within the hang guard. A handle whose type the platform cannot tell
(reported through the `IsRegularFileOf` seam) is `NotARegularFile` under `AllowAll` and under a restrictive policy, and
nothing is read.

##### CanvasNet-Files-FileReferenceResolver-Reading: Open Failures and Shared Access

**Tests**: `FileReferenceResolver_Resolve_OpenFailure_IsCannotReadAndStopsTheSearch`,
`FileReferenceResolver_Resolve_FileHeldOpenByWriter_IsStillRead`

An access or I/O failure of the open is `CannotRead` with the cause and only the first candidate opened; a file held
open by a writer with permissive sharing is read.

##### CanvasNet-Files-FileReferenceResolver-Limits: Bytes and Files

**Tests**: `FileReferenceResolver_Resolve_MaxFileBytesAtLimitAndOneOver`,
`FileReferenceResolver_Resolve_EndlessCallbackStream_StopsAtTheBound`,
`FileReferenceResolver_Resolve_MaxFiles_CountsOpenedFilesAndRefusesBeforeOpen`

Ten bytes are accepted and eleven are `TooLarge` for real and callback files; an endless stream stops within one chunk
past 20,000 bytes; with a limit of three, a denied, an oversized and a non-regular file count, a miss does not, the
fourth file is `TooManyFiles` and never opened, and the denied file asked again is denied again rather than refused by
the limit.

##### CanvasNet-Files-FileReferenceResolver-Cache: One Open per Path

**Tests**: `FileReferenceResolver_Resolve_SameFileTwice_IsOpenedOnceAndReturnsTheSameInstance`,
`FileReferenceResolver_KeyComparer_FollowsModeAndPlatform`

A file and a miss are opened once, the same instance is returned, and a change to the file after its first read is
not seen; the key comparer is ordinal for logical paths and case-insensitive for full paths exactly on Windows and
macOS.

##### CanvasNet-Files-FileReferenceResolver-Callback: Callback Mode

**Tests**: `FileReferenceResolver_Resolve_CallbackMode_UsesLogicalPathsAndNeverConsultsThePolicy`,
`FileReferenceResolver_Resolve_CallbackMode_DotDotAboveRootAndAbsolutePath_AreInvalidPath`,
`FileReferenceResolver_Resolve_CallbackFailure_IsCannotReadAndCancellationPassesThrough`,
`FileReferenceResolver_Resolve_RelativeToFromTheOtherMode_ThrowsArgumentException`

The callback receives only normalized logical paths in order (referrer directory, then the valid search-path prefix),
a same-named file on disk is never read, a denying policy is never asked, and results have no real path; `..` above
the logical root and absolute references are `InvalidPath` without a request; callback failures (including a forged
`FileReferenceException`) are `CannotRead` with the cause and a cancellation passes through; a file of the other mode is
rejected as a starting point.

##### CanvasNet-Files-FileReferenceResolver-Cancellation: Cancellation

**Tests**: `FileReferenceResolver_Resolve_CanceledToken_StopsBeforeAnyAccess`

A canceled token throws `OperationCanceledException` before any request or open.

##### CanvasNet-Files-FileReferenceResolver-Failures: Exception Contract and Argument Validation

**Tests**: `FileReferenceException_Constructor_EveryKind_KeepsFactsAndHidesHostPath`,
`FileReferenceException_Constructor_NullReference_ThrowsArgumentNullException`,
`FileReferenceResolver_Resolve_InvalidBaseDirectory_IsInvalidBaseDirectory`,
`FileReferenceResolver_Constructor_InvalidArguments_Throw`

For every kind the exception keeps its facts, its message is a sentence without the host folder, and only a path-text
failure states its rule; a null reference is rejected; an invalid base directory or search path is
`InvalidBaseDirectory` naming the configured text; null options, a null search-path list, a null policy, a non-positive
byte limit, and a negative file limit are rejected at construction.
