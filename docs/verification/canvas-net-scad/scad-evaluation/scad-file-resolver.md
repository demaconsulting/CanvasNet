### ScadFileResolver Unit Verification Design

<!-- cspell:ignore undef symlink symlinks sandboxed junction mklink -->

This document describes the unit-level verification strategy for the `ScadFileResolver` unit: the internal
`ScadFileResolver` (expansion, library loading, path normalization, and the sandbox) and the `ScadSourceUnit` it
produces, in the `DemaConsulting.CanvasNet.Scad.Evaluation` namespace.

#### Verification Approach

The unit is verified by calling `ScadFileResolver.Load` directly, through the helpers of
`ScadResolverTestSupport`, on small programs and file sets, and asserting on the resulting `ScadSourceUnit` (its
statements, their locations, and its `Uses`), on the warnings recorded in the sink, and on the exceptions thrown.
Two kinds of file source are used, and both are real implementations of the unit's contract rather than mocks:

- **Resolver mode** uses `ScadVirtualFiles`, an in-memory file set exposed as the `FileResolver` delegate. It records
  every path it is asked for and the thread each request came from, so that tests can assert exactly which logical
  paths reached the callback, that hostile paths never did, that a missing file was asked for once, and that no
  file-system access happened (the scenario runs with a base directory that does not exist).
- **File-system mode** uses `ScadTempDirectory`, a per-test temporary directory tree that the test creates and
  deletes, for real files, folders, library paths, byte-order marks, and (where the host permits creating them)
  directory links.

The link rule (a symbolic link or junction below a root is rejected whatever its target) is verified in two layers.
The **cited** scenarios (`ScadFileResolverLinkTests`) inject a fake link probe through the resolver's internal
constructor seam (`Func<string, bool>`): the fake reports chosen paths of real temporary files and directories as
links, so the walk logic (top-down order, existing components only, caching, trusted root, fail-closed probe,
resolver mode never probing, dot-dot normalization) is proven on every platform without creating a link, and these
tests never skip. The **not cited** scenarios create real links (a junction through `mklink /J` or a symbolic link
through the framework) and so additionally prove the default probe against the real file system; they skip
themselves where the host cannot create links and are listed here as additional evidence only.

Limits are verified in memory with generated chains: include depth at the limit and one over (and the default limit
and the maximum of 64 on a thread with a small stack, to show that expansion is not stack-bound), cycles and
self-includes, a diamond that is not a cycle, a diamond blow-up stopped by the inclusion and statement limits, an
oversize file, and cancellation between files (the resolver cancels the token). The Windows, Linux, and macOS file
name case rules are asserted per platform.

Unit tests reside in `ScadFileResolverIncludeTests.cs`, `ScadFileResolverUseTests.cs`,
`ScadFileResolverSandboxTests.cs`, `ScadFileResolverLinkTests.cs`, `ScadFileResolverLimitTests.cs`, and
`ScadFileResolverFileSystemTests.cs` within
the `DemaConsulting.CanvasNet.Scad.Tests` project, in the `Evaluation` folder, with the shared helpers
(`ScadVirtualFiles`, `ScadResolverTestSupport`, and `ScadTempDirectory`) in `ScadResolverTestSupport.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None; the in-memory file set and the temporary directories are real implementations
- **Files**: Per-test temporary directories under the system temporary folder, deleted by the test; no checked-in
  file is read by this unit's tests
- **Threads**: The stack scenarios run `Load` from a caller thread with a small stack and join it before asserting
- **Isolation**: Each test builds its own file set or directory

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A request to the resolver callback that is not expected, a file-system access in resolver mode, an
accepted hostile path, a wrong location or display name, or a wrong exception type or message constitutes a
failure.

#### Unit-Level Test Scenarios

##### CanvasNetScad-ScadEvaluation-ScadFileResolver-Include: Textual Inclusion

**Tests**: `ScadFileResolverIncludeTests.ScadFileResolver_Load_NoDirectives_ReturnsStatementsUnchanged`,
`ScadFileResolverIncludeTests.ScadFileResolver_Load_Include_ReplacesDirectiveInPlaceWithIncludedLocations`,
`ScadFileResolverIncludeTests.ScadFileResolver_Load_NestedIncludes_ResolveRelativeToIncludingFile`,
`ScadFileResolverIncludeTests.ScadFileResolver_Load_SameFileTwice_ExpandsTwiceReadsOnce`,
`ScadFileResolverIncludeTests.ScadFileResolver_Load_Include_BringsInEveryKindOfStatement`

Asserts that a program without directives is unchanged, that an include replaces the directive in place and the
statements keep locations that name the included file, that nested includes resolve relative to the including
file, that a file included twice is expanded twice but requested once, and that every statement kind is brought
in.

##### CanvasNetScad-ScadEvaluation-ScadFileResolver-Use: Library Units

**Tests**: `ScadFileResolverUseTests.ScadFileResolver_Load_Use_CreatesLibraryUnitAndRemovesDirective`,
`ScadFileResolverUseTests.ScadFileResolver_Load_UseSameFileTwice_LoadsOnce`,
`ScadFileResolverUseTests.ScadFileResolver_Load_SeveralUses_KeepsOrderOfLastUse`,
`ScadFileResolverUseTests.ScadFileResolver_Load_LibraryUse_StaysPrivateToLibrary`,
`ScadFileResolverUseTests.ScadFileResolver_Load_MutuallyUsingLibraries_Terminate`,
`ScadFileResolverUseTests.ScadFileResolver_Load_SelfUsingLibrary_Terminates`,
`ScadFileResolverUseTests.ScadFileResolver_Load_UseInsideInclude_IsCollectedByIncludingFile`,
`ScadFileResolverUseTests.ScadFileResolver_Load_LibraryInclude_IsExpandedInsideLibrary`,
`ScadFileResolverUseTests.ScadFileResolver_Load_LongLibraryChain_OnSmallStack_Succeeds`

Asserts that a use creates a library unit and removes the directive, that a library used twice is loaded once,
that the libraries are ordered by last use, that a library's own uses stay private to it, that libraries using each
other or themselves terminate, that a use inside an included file is collected by the including file, that a
library's includes are expanded inside the library, and that a long chain of libraries loads on a small stack.

##### CanvasNetScad-ScadEvaluation-ScadFileResolver-Resolution: Normalization And Search Order

**Tests**: `ScadFileResolverIncludeTests.ScadFileResolver_Load_BackslashPath_IsNormalizedToForwardSlashes`,
`ScadFileResolverIncludeTests.ScadFileResolver_Load_DotSegments_AreCollapsedInRequestedPath`,
`ScadFileResolverIncludeTests.ScadFileResolver_Load_Include_FallsBackToLibraryPath`,
`ScadFileResolverFileSystemTests.ScadFileResolver_Load_LibraryPath_IsSearchedAfterIncludingDirectory`,
`ScadFileResolverFileSystemTests.ScadFileResolver_Load_ExplicitBaseDirectory_OverridesFileFolder`,
`ScadFileResolverFileSystemTests.ScadFileResolver_Load_StringDocument_UsesBaseDirectoryOption`,
`ScadFileResolverSandboxTests.ScadFileResolver_Load_FileSystem_CandidateOrderIsIncludingDirectoryThenLibraries`,
`ScadFileResolverFileSystemTests.ScadFileResolver_Load_TwoSpellingsOfOnePath_AreOneFile`,
`ScadFileResolverFileSystemTests.ScadFileResolver_Load_FileNameCase_FollowsPlatform`

Asserts that backslashes become forward slashes and dot segments collapse in the path handed to the callback, that
the including directory is searched before the library paths, that an explicit base directory overrides an opened
file's folder and a string document uses the base directory option, that two spellings of a path are one file, and
that file-name case follows the platform.

##### CanvasNetScad-ScadEvaluation-ScadFileResolver-Sandbox: Confinement

**Tests**: `ScadFileResolverSandboxTests.ScadFileResolver_Load_HostilePath_InResolverMode_ThrowsWithoutAskingResolver`,
`ScadFileResolverSandboxTests.ScadFileResolver_Load_HostilePath_InFileSystemMode_Throws`,
`ScadFileResolverSandboxTests.ScadFileResolver_Load_DotDotInsideRoot_IsAllowed`,
`ScadFileResolverSandboxTests.ScadFileResolver_Load_NestedFileEscapingRoot_Throws`,
`ScadFileResolverSandboxTests.ScadFileResolver_Load_FileSystem_LibraryRootConfinesCandidates`,
`ScadFileResolverSandboxTests.ScadFileResolver_Load_FileSystem_ExistingFileOutsideRoot_IsRefused`,
`ScadFileResolverSandboxTests.ScadFileResolver_Load_FileSystem_SiblingWithSharedPrefix_IsOutsideRoot`,
`ScadFileResolverLinkTests.ScadFileResolverLink_Load_LinkedDirectoryBetweenRootAndFile_IsRejected`,
`ScadFileResolverLinkTests.ScadFileResolverLink_Load_LinkedDirectoryAtAnyDepth_IsRejected`,
`ScadFileResolverLinkTests.ScadFileResolverLink_Load_LinkedFile_IsRejectedWhateverItsTarget`,
`ScadFileResolverLinkTests.ScadFileResolverLink_Load_LinkedLibraryRootComponent_IsRejected`,
`ScadFileResolverLinkTests.ScadFileResolverLink_Load_LinkedRootAndAncestors_AreTrusted`,
`ScadFileResolverLinkTests.ScadFileResolverLink_Load_LinkAfterMissingComponent_IsNotProbed_AndFileIsMissing`,
`ScadFileResolverLinkTests.ScadFileResolverLink_Load_EveryComponentProbedOnce_AndCached`,
`ScadFileResolverLinkTests.ScadFileResolverLink_Load_ProbeThrows_FailsClosed`,
`ScadFileResolverLinkTests.ScadFileResolverLink_Load_ResolverMode_NeverProbes`,
`ScadFileResolverLinkTests.ScadFileResolverLink_Load_DotDotRouteThroughLink_IsJudgedOnNormalizedPath`

Asserts that absolute paths, drive letters, colons, empty paths, control characters, and paths climbing above the
root throw an evaluation error (in resolver mode without asking the callback), that dot-dot segments inside the
root are allowed, that a nested file whose own include escapes the root is rejected, that a library root confines
its own candidates, that an existing file outside every root is refused, and that a sibling folder sharing a name
prefix with a root is outside it. The link scenarios (fake probe, never skipped) assert that a linked directory at
depth one, two, or three, a linked file, and a linked component below a library root are each rejected with a
message that names the component below the root and contains `link` (the file is never read), that a linked root
and its ancestors are trusted, that the walk ends at the first missing component without probing below it, that
each component is probed once, that a failing probe rejects the file, that resolver mode never asks the probe, and
that a route through dot-dot is judged on the normalized path. Additional evidence, not cited by the requirement
and skipped where links cannot be created: `ScadFileResolverLinkTests.ScadFileResolverLink_Load_RealDirectoryLink_EscapingRoot_IsRejected`,
`…_RealDirectoryLink_InsideRoot_IsRejected`, `…_RealFileLink_IsRejected` (a file link is rejected even when its
target is inside the root), `…_RealLinkedBaseDirectoryItself_IsAllowed`, and the end-to-end
`ScadDocumentEvaluateSafetyTests.ScadDocument_Evaluate_FileSystemSandbox_DirectoryLinkEscape_IsRejected`.
_Not verified, by design:_ the requirement covers symbolic links and junctions only. A hard link has no marker a
link probe can see, so a hard link inside a root to a file outside it is read like a regular file, and a component
swapped between the link walk and the open (a check/use race) is not noticed; both are documented residual risks of
the file-system mode (see the design, _File-system mode_), and no test claims otherwise. In short, the sandbox
protects against path-based escapes in static file trees and is not a security boundary against a concurrent local
attacker with write access to the included folders.

##### CanvasNetScad-ScadEvaluation-ScadFileResolver-ResolverCallback: Resolver Mode

**Tests**: `ScadFileResolverSandboxTests.ScadFileResolver_Load_ResolverMode_RequestsOnlyNormalizedLogicalPaths`,
`ScadFileResolverSandboxTests.ScadFileResolver_Load_ResolverMode_DoesNotTouchFileSystem`,
`ScadFileResolverSandboxTests.ScadFileResolver_Load_ResolverMode_UsesCallbackStream`,
`ScadFileResolverSandboxTests.ScadFileResolver_Load_ResolverException_Propagates`,
`ScadFileResolverIncludeTests.ScadFileResolver_Load_InvalidLogicalLibraryPaths_AreIgnored`,
`ScadFileResolverIncludeTests.ScadFileResolver_Load_MissingFileTwice_AsksResolverOnce`

Asserts that only normalized logical paths reach the callback, that a base directory that does not exist is never
touched, that the stream returned by the callback is the source used, that an exception from the callback
propagates unchanged, that library paths that are not valid logical paths are ignored, and that a missing path is
requested once.

##### CanvasNetScad-ScadEvaluation-ScadFileResolver-FileSystem: Files On Disk

**Tests**: `ScadFileResolverFileSystemTests.ScadFileResolver_Load_OpenedFile_ResolvesRelativeIncludes`,
`ScadFileResolverFileSystemTests.ScadFileResolver_Load_Utf8BomFile_IsAccepted`,
`ScadFileResolverFileSystemTests.ScadFileResolver_Load_DirectoryInsteadOfFile_IsMissing`

Asserts that a document opened from a file resolves relative includes against its folder, that a UTF-8 file with a
byte-order mark is accepted, and that a directory in place of a file counts as missing.

##### CanvasNetScad-ScadEvaluation-ScadFileResolver-Cycles: Include Cycles

**Tests**: `ScadFileResolverLimitTests.ScadFileResolver_Load_IncludeCycle_ThrowsWithChain`,
`ScadFileResolverLimitTests.ScadFileResolver_Load_SelfInclude_ThrowsCycle`,
`ScadFileResolverLimitTests.ScadFileResolver_Load_DiamondInclude_IsNotACycle`,
`ScadFileResolverFileSystemTests.ScadFileResolver_Load_MainFileIncludingItself_IsACycle`,
`ScadFileResolverFileSystemTests.ScadFileResolver_Load_IncludeBackToMain_IsACycle`

Asserts that a two-file cycle and a self-include throw an evaluation error whose message names the chain, that a
diamond is accepted, and that a file including the main file is a cycle.

##### CanvasNetScad-ScadEvaluation-ScadFileResolver-DepthLimit: Include Depth

**Tests**: `ScadFileResolverLimitTests.ScadFileResolver_Load_IncludeDepth_AtLimit_Succeeds_OneOver_Throws`,
`ScadFileResolverLimitTests.ScadFileResolver_Load_DefaultDepthLimit_StopsLongChain_OnSmallStack`,
`ScadFileResolverLimitTests.ScadFileResolver_Load_MaximumDepth64_OnSmallStack_Succeeds`

Asserts that nesting at the limit succeeds and one deeper throws, that the default limit stops a long generated
chain on a small stack, and that the maximum permitted depth of 64 resolves on a small stack.

##### CanvasNetScad-ScadEvaluation-ScadFileResolver-FileCountLimit: Inclusion And Statement Counts

**Tests**: `ScadFileResolverLimitTests.ScadFileResolver_Load_IncludedFiles_AtLimit_Succeeds_OneOver_Throws`,
`ScadFileResolverLimitTests.ScadFileResolver_Load_Libraries_CountOncePerDistinctFile`,
`ScadFileResolverLimitTests.ScadFileResolver_Load_DiamondBlowUp_IsStoppedByInclusionLimit`,
`ScadFileResolverLimitTests.ScadFileResolver_Load_ExpandedStatements_AtLimit_Succeeds_OneOver_Throws`

Asserts that the included-file limit is exact, that a library counts once however often it is used, that a
diamond blow-up is stopped by the limit, and that the expanded-statement limit is exact.

##### CanvasNetScad-ScadEvaluation-ScadFileResolver-MissingFiles: Missing Files

**Tests**: `ScadFileResolverIncludeTests.ScadFileResolver_Load_MissingInclude_WarnsAndContinues`,
`ScadFileResolverUseTests.ScadFileResolver_Load_MissingUse_WarnsAndCreatesNoUnit`,
`ScadFileResolverFileSystemTests.ScadFileResolver_Load_MissingFile_WarnsOnDisk`

Asserts the located warning for a missing include and use (in memory and on disk), that the rest of the program is
retained, and that a missing use creates no library unit.

##### CanvasNetScad-ScadEvaluation-ScadFileResolver-NoBase: No Place To Look

**Tests**: `ScadFileResolverLimitTests.ScadFileResolver_Load_NoBaseNoLibraryNoResolver_DirectiveIsError`

Asserts that a directive in a document with no base directory, library path, or resolver throws an evaluation error
located at the directive.

##### CanvasNetScad-ScadEvaluation-ScadFileResolver-SyntaxErrors: Errors In Referenced Files

**Tests**: `ScadFileResolverLimitTests.ScadFileResolver_Load_SyntaxErrorInInclude_ThrowsSyntaxExceptionWithFileName`,
`ScadFileResolverUseTests.ScadFileResolver_Load_SyntaxErrorInLibrary_ThrowsWithLibraryName`

Asserts that a syntax error in an included file and in a library is a `ScadSyntaxException` whose location names
that file.

##### CanvasNetScad-ScadEvaluation-ScadFileResolver-SourceLimit: Oversize Files

**Tests**: `ScadFileResolverLimitTests.ScadFileResolver_Load_OversizeInclude_ThrowsInvalidData`

Asserts that an included file longer than 4,194,304 characters throws `InvalidDataException`.

##### CanvasNetScad-ScadEvaluation-ScadFileResolver-Cancellation: Cancellation

**Tests**: `ScadFileResolverLimitTests.ScadFileResolver_Load_CanceledToken_ThrowsBeforeAnyRequest`,
`ScadFileResolverLimitTests.ScadFileResolver_Load_CancelDuringLoading_StopsBetweenFiles`

Asserts that an already canceled token throws before any request and that cancellation by the resolver while a
file is loading stops before the next file is requested.
