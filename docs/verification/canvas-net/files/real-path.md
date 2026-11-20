### RealPath Unit Verification Design

This document describes the unit-level verification strategy for the internal `RealPath` unit (`RealPath`,
`NativeMethods`, and `FileOpenHooks`) in the `DemaConsulting.CanvasNet.Files` namespace.

#### Verification Approach

The unit is verified against the real operating system, because its purpose is to call it: real temporary files and
folders are opened with `RealPath.OpenForRead`, and the facts reported by `RealPath.Inspect` and
`RealPath.TryGetCanonicalDirectory` are compared with what the test created. The tests reach the internal unit through
the core assembly's `InternalsVisibleTo` grant. Every scenario that exists on one operating system only runs there
and skips elsewhere with the reason; the CI matrix (Windows, Linux, and macOS on Arm64, each with .NET 8, 9, 10, and 11)
runs each of them, and the requirements link them with source filters, so only runs on the right system count:

| Scenario | Windows | Linux | macOS |
| --- | --- | --- | --- |
| Plain file, file through a directory link, linked folder | runs (junction) | runs | runs |
| Short (8.3) name expansion | runs | skips | skips |
| `/private` location of the temporary folder (pins the `proc_pidfdinfo` path offset) | skips | skips | runs |
| Deleted file has no real path | skips | runs | skips |
| Device and directory are not regular files | null device; folder refused | `/dev/null`, folder | `/dev/null`, folder |
| Named pipe opens without blocking (pins the mode offset) | skips | runs | runs |
| Identity of an open file and of another spelling (pins the identity offsets) | runs | runs | runs |
| Windows file identity structures decoded (pure bytes) | runs | runs | runs |
| A file symbolic link to the opened file | runs (or skips without the link privilege) | skips | skips |
| Case-insensitive temporary folder: stored letter case, same file in another case | runs | skips | runs |
| Case-sensitive temporary folder: both case variants exist | skips | runs | skips |
| Names with `*` and `?`; a symbolic link to the opened file | skips | runs | runs |
| Drive letter upper-cased | runs | skips | skips |

Whether the temporary folder ignores letter case is probed by creating a file and looking for it in upper case; no
test changes a setting of the file system (no per-directory case sensitivity, no disk image), so the case-insensitive
scenarios run where the default temporary folder is case-insensitive (Windows, macOS) and the case-sensitive ones where
it is case-sensitive (Linux).

The named-pipe open runs on the test thread with no time limit and no duration is asserted: an open that blocked would
never return, which the time limit of the CI job reports.

Unit tests reside in `RealPathTests.cs` in the `Files` folder of the `DemaConsulting.CanvasNet.Tests` project; the
seams in `FileOpenHooks` are exercised by the resolver tests (see *FileReferenceResolver Unit Verification Design*).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline on each operating system
- **Files**: per-test temporary directories, deleted by the test; `/dev/null` on Linux and macOS
- **Tools**: `mkfifo` (Linux and macOS) for named pipes; `cmd.exe` (Windows) for junctions and short names

#### Acceptance Criteria

A unit-level test run passes on an operating system when every scenario that runs there passes. A wrong or missing
real path where the system can tell it, a real path for a deleted file, a device or pipe reported as a regular file,
a blocked open, a real path compared ignoring letter case, a wrong on-disk spelling, or a spelling confirmed for another
file or a link constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNet-Files-RealPath-FinalPath: The Real Path of an Open File

**Tests**: `RealPath_Inspect_PlainFile_ReportsItsCanonicalPathAndARegularFile`,
`RealPath_Inspect_FileThroughADirectoryLink_ReportsTheTarget`, `RealPath_Inspect_ShortNameSpelling_IsExpanded`
(Windows), `RealPath_Inspect_TempFileOnMacOS_ReportsThePrivateLocation` (macOS)

A plain file is a regular file whose fully qualified final path is the real location of its folder plus its name; a
file opened through a directory link reports the target; a file opened by its short spelling reports the long one and
short names expand; on macOS a temporary file under `/var` or `/tmp` reports its `/private` location.

##### CanvasNet-Files-RealPath-Unavailable: No Path Rather Than a Wrong One

**Tests**: `RealPath_Inspect_DeletedFile_IsUnavailable` (Linux),
`RealPath_Inspect_LiveFileNamedLikeADeletedOne_ReportsItsRealPath` (Windows, Linux, macOS)

A file deleted after the open is still a regular file but has no real path; on Linux deletion is told from the link
count of the open file (zero). A live file named `x (deleted)`, the text Linux appends to a deleted file's link, reports
its real path with that name.

##### CanvasNet-Files-RealPath-RegularFile: The Type of an Open Handle

**Tests**: `RealPath_Inspect_DirectoryAndDevice_AreNotRegularFiles`

On Linux and macOS `/dev/null` and a folder open and are not regular files; on Windows the null device is not a regular
file and a folder cannot be opened as a file (`UnauthorizedAccessException`).

##### CanvasNet-Files-RealPath-NonBlockingOpen: Named Pipes and Missing Files

**Tests**: `RealPath_OpenForRead_Fifo_DoesNotBlockAndIsNotRegular` (Linux, macOS),
`RealPath_OpenForRead_MissingFileAndMissingFolder_MapToNotFoundExceptions`

A named pipe without a writer opens at once and is not a regular file; a missing file is `FileNotFoundException`, and a
missing folder or a file used as a folder is a "not found" exception.

##### CanvasNet-Files-RealPath-CanonicalDirectory: The Real Location of a Folder

**Tests**: `RealPath_TryGetCanonicalDirectory_LinkedDirectory_ReturnsTheTarget`,
`RealPath_TryGetCanonicalDirectory_ExecuteOnlyDirectory_ReturnsItsLocation` (Linux, macOS)

A linked folder reports the real location of its target; a missing folder reports none. A folder made execute-only
(mode 0100: it may be searched but not listed) reports the same real location it had while readable, which proves
that Linux (`O_PATH`) and macOS (`realpath`) need no read permission; the test restores the mode afterwards and skips
when permissions are not enforced (a process running as root).

##### CanvasNet-Files-RealPath-NetworkLocation: Network Locations

**Tests**: `RealPath_IsNetworkLocation_UncAndLocalPaths_AreClassified` (Windows)

A UNC path and an extended-length UNC path (`\\?\UNC\server\share`) are network locations; a local temporary file, an
extended-length local path (`\\?\C:\x`), a `\\?\GLOBALROOT\` path and a device path (`\\.\NUL`) are not. A mapped
network drive cannot be created on CI, so that branch is covered by review.

##### CanvasNet-Files-RealPath-Comparison: Comparison Rules

**Tests**: `RealPath_IsUnder_BoundaryIsTheSeparator`

A folder contains itself and what lies below its separator (also with a trailing separator) but not a sibling that
shares its name as a prefix; the system root contains everything; trailing separators do not change equality; the
deepest shared folder is found by whole components.

**Tests**: `RealPath_IsUnder_ComparesLetterCaseExactly`, `RealPath_CommonAncestor_ComparesComponentsExactly`

Pure text, on every system, with `C:\work\allowed` (Windows) or `/work/allowed` (elsewhere): `...\ALLOWED\x` is outside
the root and `PathsEqual` of the two case variants is false, while `...\allowed\x`, the root itself, a root with a
trailing separator and a path with a trailing separator are inside, and the sibling `allowed2` is outside. The
written-path test (`IsUnderAsWritten`) of the case variant is inside on Windows and macOS and outside on Linux. The
deepest shared folder of `...\ALLOWED\x` and `...\allowed` is the parent; on Windows a drive letter in either case is
the same root.

##### CanvasNet-Files-RealPath-ExactCase: The Letter Case Stored on Disk

**Tests**: `RealPath_GetExactPathName_FolderAndFileInAnotherCase_GiveTheStoredCase`,
`RealPath_GetExactPathName_MissingTail_IsKeptAsWritten`,
`RealPath_GetExactPathName_BothCaseVariantsExist_PrefersTheExactNameAndKeepsAnAmbiguousOne` (Linux),
`RealPath_GetExactPathName_WildcardCharactersInAName_MatchOnlyThatName` (Linux, macOS),
`RealPath_GetExactPathName_DriveLetter_IsUpperCase` (Windows),
`RealPath_ExactRoot_DriveRootsUpperCasedAndOtherRootsKept`,
`RealPath_TryGetExactCanonicalDirectory_FolderWrittenInAnotherCase_GivesTheStoredCase` (Windows, macOS)

A temporary `Docs/Readme.txt` asked as `docs/README.txt` is given as `Docs/Readme.txt` (the single entry that matches
ignoring case, on every system); `docs/Missing/Sub.txt` keeps the missing tail as written after `Docs`. In a
case-sensitive folder holding `Docs` and `docs`, `docs` is kept (the exact entry wins) and `DOCS` is kept as written
(several matches). `A*B` next to `a*b` and `aXb` gives `a*b`, and `X?` next to `x?` and `xy` gives `x?` (wildcards match
only themselves). A lower-case drive letter is upper-cased. The root rule (pure text) upper-cases `c:\` and `\\?\c:\`
and keeps `\\server\share`, `\\?\Volume{GUID}\` and `/`. The real location of a folder `Stored` asked as `STORED` ends
with `Stored`.

##### CanvasNet-Files-RealPath-Identity: Confirming a Spelling Against the Open File

**Tests**: `RealPath_TryConfirmExactPath_SpellingOfAnotherFile_IsUnconfirmed` (Windows, Linux, macOS),
`RealPath_TryConfirmExactPath_StoredSpellingOfARealPathInAnotherCase_IsConfirmed` (Windows, macOS),
`RealPath_TryConfirmExactPath_SpellingThroughALinkedFolder_IsUnconfirmed` (Windows, Linux, macOS),
`RealPath_TryConfirmExactPath_LinkToTheOpenedFile_IsUnconfirmed` (Linux, macOS),
`RealPath_TryConfirmExactPath_WindowsFileSymbolicLink_IsUnconfirmedOrCapabilitySkipped` (Windows),
`RealPath_WindowsFileId_IdsDifferingOnlyInTheHigh64Bits_AreDifferent`,
`RealPath_WindowsFileId_ByHandleInformation_DecodesSerialAndIndex`

With `a.txt` open, the path of `b.txt`, spelled through the real folder of `a.txt` (so the spelling has no link in it;
on macOS the temporary folder is otherwise reached through the `/var` link), is not confirmed and `IsSameFile` is
false, not unknown; the reported real path needs no confirmation and is returned as is. In a case-insensitive folder,
for a real path reported in upper case, its on-disk spelling (`GetExactPathName`) differs from it and is confirmed,
which pins the identity fields and the open flags on each system; when it is not, the test message carries the reason
`TryConfirmExactPath` reports (the step that failed, with its `errno`).
A spelling that reaches the open `target/a.txt` through a directory link (a junction on Windows) `link/a.txt` is not
confirmed, although it is the same file, because a spelling must reach the file through no linked folder; a symbolic
link to the open file is not confirmed either, because the spelling is opened without following a link at its last
component. On Windows a file symbolic link to the open file is not confirmed and `IsSameFile` is false with a reason
that names another file, which exercises the `FileIdInfo` identity on a real link; file links need Developer Mode or
`SeCreateSymbolicLinkPrivilege` there, so the test skips with that reason when the host cannot create one. The Windows
identity structures are decoded as pure bytes on every system: two `FILE_ID_INFO` structures with the same 64-bit
volume serial number and the same low 64 bits of the identifier, differing only in its high 64 bits (as on ReFS), are
different identities; a `BY_HANDLE_FILE_INFORMATION` gives the 32-bit serial number at offset 28 and the 64-bit file
index from its high (44) and low (48) halves.
