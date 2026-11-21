### FileAccessPolicy Unit Verification Design

This document describes the unit-level verification strategy for the `FileAccessPolicy` unit: the public
`FileAccessPolicy` with its built-in policies and the `FileAccessRequest` and `FileAccessDecision` types, in the
`DemaConsulting.CanvasNet.Files` namespace.

#### Verification Approach

The policies are asked directly (`Check`, and the internal `PreCheck` and `RequiresRealPath` through the core
assembly's `InternalsVisibleTo` grant) with `FileAccessRequest`s that the test builds over real temporary folders,
because roots and anchors are compared in their real location: the real path in a request is the real location of a
folder (from the `RealPath` unit, a documented dependency) joined with the file name. A link that exists only in the
request (a real path that differs from the written one) needs no link on disk; the scenarios that need a real link
above the anchor use a directory link or, on Windows, a junction. No mocking is needed.

Unit tests reside in `FileAccessPolicyTests.cs` in the `Files` folder of the `DemaConsulting.CanvasNet.Tests`
project.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline on Windows, Linux, and macOS
- **Files**: per-test temporary directories, deleted by the test
- **Operating-system scenarios**: the short-name scenario runs on Windows (it skips where the volume does not create
  short names) and is linked with a source filter; the letter-case scenario of a real path asserts the same denial on
  every system and the platform rule of the written path; scenarios that need a temporary folder that ignores letter
  case (Windows, macOS) or compares it (Linux) probe the folder and skip elsewhere, and are linked with source filters;
  no test changes a setting of the file system

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. An allowed file outside a root, a denied file whose real
path equals its written path below the anchor, a wrong name, or an accepted invalid argument constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNet-Files-FileAccessPolicy-AllowAll: The Default Allows Everything

**Tests**: `FileAccessPolicy_AllowAll_AnyRequest_IsAllowedAndNamedAny`

Any request is allowed; the name is `any` (also as `ToString`); no real path and no pre-check are needed.

##### CanvasNet-Files-FileAccessPolicy-UnderRoots: Confinement to Folders

**Tests**: `FileAccessPolicy_UnderRoots_RealPathInsideOrEqualToARoot_IsAllowed`,
`FileAccessPolicy_UnderRoots_SiblingWithSharedPrefix_IsDenied`,
`FileAccessPolicy_UnderRoots_RealPathOutsideEveryRoot_IsDeniedWithReasonAndHint`,
`FileAccessPolicy_UnderRoots_RootCreatedAfterThePolicy_AllowsNothing`,
`FileAccessPolicy_UnderRoots_WrittenPathOutsideEveryRoot_IsDeniedByThePreCheck`,
`FileAccessPolicy_UnderRoots_InvalidRoots_Throw`,
`FileAccessPolicy_UnderRoots_RealPathDifferingFromARootInLetterCase_IsDenied`,
`FileAccessPolicy_UnderRoots_RootWrittenInAnotherLetterCase_AllowsFilesInside` (Windows, macOS)

A file inside the second of two roots and the root itself are allowed; a sibling folder that shares the root's name as
a prefix is denied before and after the open; a written path inside whose real path is outside is denied with the
documented reason and a hint; a root created only after the policy passes the pre-check but is denied after the open
with the "did not exist when the policy was created" reason and a hint, because its real location is captured only at
creation; a written path outside is denied by the pre-check, a written path inside passes it, and
an outside written path whose real path is inside is still denied; a null list, a null, empty, white-space or invalid
root, and no root at all are rejected, and on Windows a `\\?\GLOBALROOT\` or `\\.\` root is rejected while an
extended-length drive root is accepted. A real path that spells the root in upper case is denied on every system (the
real path is compared exactly): with the reason "its real path is outside the allowed roots" on Windows and macOS,
where the upper-case written path passes the pre-check, and by the written-path test on Linux, where it does not. In
a folder that ignores letter case, a root given in upper case allows a file inside it, because its real location is
captured in the stored letter case.

##### CanvasNet-Files-FileAccessPolicy-NoSymlinks: No Links Below the Anchor

**Tests**: `FileAccessPolicy_NoSymlinks_RealPathMatchesTheWrittenPath_IsAllowed`,
`FileAccessPolicy_NoSymlinks_RealPathDiffersBelowTheAnchor_IsDenied`,
`FileAccessPolicy_NoSymlinks_LinkAboveTheAnchor_IsTrusted`,
`FileAccessPolicy_NoSymlinks_ShortNameSpelling_IsAllowed` (Windows),
`FileAccessPolicy_NoSymlinks_RealPathDifferingInLetterCaseBelowTheAnchor_IsDenied`,
`FileAccessPolicy_NoSymlinks_WrittenPathInAnotherLetterCase_IsAllowed` (Windows, macOS),
`FileAccessPolicy_NoSymlinks_LinkNamedLikeItsTargetInAnotherCase_IsDenied` (Linux),
`FileAccessPolicy_NoSymlinks_AnchorInAnotherLetterCaseThanTheBase_IsNotTrusted` (Linux),
`FileAccessPolicy_MatchesWithoutLinks_VolumeGuidAnchorAndRealPath_Match` (Windows)

A real path equal to the written path below the base directory is allowed and the name is `no-symlinks`; a different
real path below the anchor is denied with a reason naming a symbolic link or junction and a hint; a base directory that
is itself a link is trusted; a path written with short (8.3) names is allowed when its long spelling matches. A real
path that spells an existing folder below the anchor (`sub`) in another letter case (`SUB`) is denied; a path written as
`SUB/A.TXT` whose stored spelling is `sub/a.txt` is allowed in a case-insensitive folder (compared in its on-disk
spelling). In a case-sensitive folder a link `Docs` to `docs` is denied, and a link `ALLOWED` next to the base directory
`allowed` is not trusted as the anchor, so a file reached through it is denied. On Windows the link match accepts an
anchor location and a real path on a volume without a drive letter (both `\\?\Volume{GUID}\...`): the anchor followed by
the path as written matches, and another folder of that volume does not. That the base directory a resolver passes is
the trusted chain origin, never a referring document's folder, is verified with the resolver
(`CanvasNet-Files-FileReferenceResolver-ChainOrigin`).

##### CanvasNet-Files-FileAccessPolicy-AnchoredAt: A Nested Request Is Judged From the Outer Origin

**Tests**: `FileAccessPolicy_AnchoredAt_NoSymlinks_JudgesFromTheOuterOrigin`,
`FileAccessPolicy_AnchoredAt_AllowAllAndAnchored_ReturnTheSameInstance`,
`FileAccessPolicy_AnchoredAt_AnchorsOf_NamesTheAnchorOfTheOuterOrigin`

These tests verify the internal `AnchoredAt` that a resolver uses to give a source the policy of its chain
(`CanvasNet-ImageSources-FolderImageResolver-PolicyPropagation`). With `outer/lib` a directory link (a junction on
Windows, so the test never skips), a nested request whose base directory is `outer/lib` is allowed by plain
`NoSymlinks` and denied, with a reason naming a symbolic link or junction, once the policy is anchored at `outer`; the
anchored policy keeps the name `no-symlinks` and needs a real path. Anchoring `AllowAll`, or a policy that is already
anchored, returns the same instance (the outermost origin wins); the pre-check of an anchored `roots` policy is that of
the inner one; a null origin is rejected. An anchored `roots+no-symlinks` policy names, for a nested request whose base
directory is a folder below `outer`, the anchor of `outer` as the only folder to capture before the open, where plain
`NoSymlinks` names the nested folder, so the nested resolver captures exactly what the anchored check compares against.

##### CanvasNet-Files-FileAccessPolicy-Combine: Composition

**Tests**: `FileAccessPolicy_Combine_FirstDenialWinsAndNamesAreJoined`,
`FileAccessPolicy_Combine_InvalidArguments_Throw`

A nested combination is flattened (`third+first+second`) and returns the first denial's reason; `roots+no-symlinks`
allows a plain inside file and pre-checks an outside path; a single policy is returned as it is; the need for a real
path follows the parts; a null, empty, or null-holding list is rejected.

##### CanvasNet-Files-FileAccessPolicy-FromDelegate: A Policy From a Function

**Tests**: `FileAccessPolicy_FromDelegate_UsesNameAndCheckAndRejectsInvalidArguments`

The function receives the very request and its decision is returned under the given name; the policy needs a real path;
a null or white-space name and a null function are rejected.

##### CanvasNet-Files-FileAccessPolicy-Decision: Decisions

**Tests**: `FileAccessDecision_Deny_KeepsReasonAndHintAndRejectsAnEmptyReason`

A denial keeps its reason and hint, a white-space hint becomes none, the allow decision is one instance without a
reason, and a null or empty reason is rejected.
