### RealPath Unit Verification Design

This document describes the unit-level verification strategy for the internal `RealPath` unit (`RealPath`,
`NativeMethods`, and `FileOpenHooks`) in the `DemaConsulting.CanvasNet.Files` namespace.

#### Verification Approach

The unit is verified against the real operating system, because its purpose is to call it: real temporary files and
folders are opened with `RealPath.OpenForRead`, and the facts reported by `RealPath.Inspect` and
`RealPath.TryGetCanonicalDirectory` are compared with what the test created. The tests reach the internal unit through
the core assembly's `InternalsVisibleTo` grant. Every scenario that exists on one operating system only runs there
and skips elsewhere with the reason; the CI matrix (Windows, Linux, and macOS on Arm64, each with .NET 8, 9, and 10)
runs each of them, and the requirements link them with source filters, so only runs on the right system count:

| Scenario | Windows | Linux | macOS |
| --- | --- | --- | --- |
| Plain file, file through a directory link, linked folder | runs (junction) | runs | runs |
| Short (8.3) name expansion | runs | skips | skips |
| `/private` location of the temporary folder (pins the `proc_pidfdinfo` path offset) | skips | skips | runs |
| Deleted file has no real path | skips | runs | skips |
| Device and directory are not regular files | null device; folder refused | `/dev/null`, folder | `/dev/null`, folder |
| Named pipe opens without blocking (pins the mode offset) | skips | runs | runs |

The one wait is a hang guard (one minute) around the named-pipe open, which fails the test if the open blocks; no
duration is asserted.

Unit tests reside in `RealPathTests.cs` in the `Files` folder of the `DemaConsulting.CanvasNet.Tests` project; the
seams in `FileOpenHooks` are exercised by the resolver tests (see _FileReferenceResolver Unit Verification Design_).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline on each operating system
- **Files**: per-test temporary directories, deleted by the test; `/dev/null` on Linux and macOS
- **Tools**: `mkfifo` (Linux and macOS) for named pipes; `cmd.exe` (Windows) for junctions and short names

#### Acceptance Criteria

A unit-level test run passes on an operating system when every scenario that runs there passes. A wrong or missing
real path where the system can tell it, a real path for a deleted file, a device or pipe reported as a regular file,
or a blocked open constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNet-Files-RealPath-FinalPath: The Real Path of an Open File

**Tests**: `RealPath_Inspect_PlainFile_ReportsItsCanonicalPathAndARegularFile`,
`RealPath_Inspect_FileThroughADirectoryLink_ReportsTheTarget`, `RealPath_Inspect_ShortNameSpelling_IsExpanded`
(Windows), `RealPath_Inspect_TempFileOnMacOS_ReportsThePrivateLocation` (macOS)

A plain file is a regular file whose fully qualified final path is the real location of its folder plus its name; a
file opened through a directory link reports the target; a file opened by its short spelling reports the long one and
short names expand; on macOS a temporary file under `/var` or `/tmp` reports its `/private` location.

##### CanvasNet-Files-RealPath-Unavailable: No Path Rather Than a Wrong One

**Tests**: `RealPath_Inspect_DeletedFile_IsUnavailable` (Linux)

A file deleted after the open is still a regular file but has no real path.

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

**Tests**: `RealPath_TryGetCanonicalDirectory_LinkedDirectory_ReturnsTheTarget`

A linked folder reports the real location of its target; a missing folder reports none.

##### CanvasNet-Files-RealPath-Comparison: Comparison Rules

**Tests**: `RealPath_IsUnder_BoundaryIsTheSeparator`

A folder contains itself and what lies below its separator (also with a trailing separator) but not a sibling that
shares its name as a prefix; the system root contains everything; trailing separators do not change equality; the
deepest shared folder is found by whole components.
