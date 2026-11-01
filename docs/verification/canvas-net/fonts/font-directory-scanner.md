### FontDirectoryScanner Unit Verification Design

<!-- cspell:ignore ttcf reparse mklink -->

This document describes the unit-level verification strategy for the `FontDirectoryScanner` class and
its `FontScanLimits` bounds record.

#### Verification Approach

`FontDirectoryScannerTests.cs` scans controlled temporary directories populated with copies of the
real bundled Liberation fonts (extracted from the assembly's embedded resources) and with
deliberately unparseable files, so every test is deterministic regardless of what is installed on
the machine running the tests. The ordering tests create files out of order (including a mixed-case
name) and compare against the traversal order (a directory's font files in case-insensitive alphabetical order,
then its subdirectories). The limit
tests use directories of several identical fonts so the expected survivors are the first ones in
traversal order; the entry-bound test uses a directory whose entries exceed the budget, so its result cannot
depend on the order the file system lists entries in. The link tests create directory links
without administrator privileges where the platform allows it - a symbolic link on Unix, a junction
(`mklink /J`) on Windows - and a file symbolic link where permitted; where the environment cannot
create a link the test returns early rather than failing (link creation is privilege dependent),
and the remaining tests still cover the scan without links.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None; real temporary directories created and removed (links first) by each test
- **Isolation**: Each test creates its own uniquely named temporary directory

#### Acceptance Criteria

A unit test run passes when every scenario below passes, and when every named test method listed for each
requirement ID passes across every target framework and every operating system in this repository's CI
matrix.

#### Test Scenarios

##### CanvasNet-Fonts-FontDirectoryScanner-HeaderOnlyDiscovery: Faces Are Reported From Headers

**Tests**: `FontDirectoryScanner_Scan_BundledFonts_ReportsFamilyStyleAndAbsolutePath`,
`FontDirectoryScanner_Scan_ExtensionsAndUnparseableFiles_OnlyWellFormedFontsReported`

Scans copies of bundled Liberation Sans Bold and Liberation Serif Italic and asserts each face's family,
bold/italic/fixed-pitch flags, absolute path and face index; scans a directory of `.ttf`, `.OTF`, `.ttc`,
`.OtC`, `.woff`, `.txt` and extensionless copies and asserts only the four font extensions are reported.

##### CanvasNet-Fonts-FontDirectoryScanner-DeterministicOrder: Same Tree, Same Order

**Tests**: `FontDirectoryScanner_Scan_NestedDirectories_ResultIsDeterministicallyOrdered`,
`FontDirectoryScanner_Scan_SeveralRoots_RootOrderKeptAndDuplicatesReportedOnce`,
`FontDirectoryScanner_Scan_RepeatedRoot_DuplicatesDoNotConsumeTheFontFileBound`,
`FontDirectoryScanner_Scan_RootFileAndChildFile_RootFileComesFirstAndCountsFirst`

Creates fonts in a scrambled order across nested directories (including mixed-case names) and asserts two scans
return equal results in traversal order: each directory's own font files in case-insensitive alphabetical order,
then its subdirectories; scans several roots (one repeated, one spelled with `.`) and asserts the
root order is kept and no file is reported twice; scans roots `[A, A, B]` with a font-file bound of two and
asserts both A's and B's files are returned (the repeat does not consume the bound); places the same family and
style in a root file and in a
subdirectory file and asserts the root file comes first and is the one `FindInFamily` selects.

##### CanvasNet-Fonts-FontDirectoryScanner-BoundedScan: Bounds Stop the Scan Deterministically

**Tests**: `FontDirectoryScanner_Scan_FontFileLimit_KeepsTheFirstFilesInPathOrder`,
`FontDirectoryScanner_Scan_RootFileAndChildFile_RootFileComesFirstAndCountsFirst`,
`FontDirectoryScanner_Scan_FaceLimit_KeepsTheFirstFaces`,
`FontDirectoryScanner_Scan_DirectoryLimit_StopsTheDescent`,
`FontDirectoryScanner_Scan_EntryLimit_StopsTheExamination`,
`FontDirectoryScanner_Scan_BoundReached_RemainingRootsAreNotEnumerated`,
`FontDirectoryScanner_Scan_CraftedCollectionHeaders_AreBoundedByTheFileAndTheFaceLimit`,
`FontScanLimits_Properties_RejectLessThanOneAndHaveDocumentedDefaults`

Applies each of the four bounds to a tree that exceeds it and asserts the faces kept are the first ones in
traversal order (a directory bound keeps the root's fonts and not the subdirectory's; a font-file bound of one keeps
the root's file rather than a subdirectory's; an entry bound that a directory's entries exceed uses none of that
directory while keeping the complete directories before it); passes a lazily enumerated list of roots that throws
if asked for a second root, with the font-file or face bound reached in the first, proving the scan stops without
enumerating further roots; scans a directory holding a collection file that declares `uint.MaxValue` faces
(rejected, costing no face) and one that declares 10,000 faces (bounded by the face limit, so the font after
it is not read); asserts the documented defaults and that each
property throws `ArgumentOutOfRangeException` for a value below one.

##### CanvasNet-Fonts-FontDirectoryScanner-LinkPolicy: Links Are Never Followed Below the Root

**Tests**: `FontDirectoryScanner_Scan_LinkedSubdirectory_IsNeverFollowed`,
`FontDirectoryScanner_Scan_LinkBackToParent_TerminatesAndFindsTheFont`,
`FontDirectoryScanner_Scan_LinkedRoot_IsRefusedUnlessTrusted`,
`FontDirectoryScanner_Scan_LinkedFile_IsSkippedUnlessTrusted`

Creates a linked subdirectory pointing outside the tree (with and without trust), a subdirectory linking back at
its own parent, a linked root, and a linked font file, and asserts the linked content is reported only where the
policy allows it: never through a linked subdirectory, only with trust for a linked root or file.

##### CanvasNet-Fonts-FontDirectoryScanner-ToleratesUnusableInput: Unusable Inputs Are Skipped

**Tests**: `FontDirectoryScanner_Scan_MissingEmptyAndNullRoots_AreSkipped`,
`FontDirectoryScanner_Scan_ExtensionsAndUnparseableFiles_OnlyWellFormedFontsReported`

Scans a list containing a missing, empty, `null` and invalid-character root beside a valid one, and a
directory holding garbage and empty `.ttf` files beside valid fonts, asserting no exception and only the valid
fonts reported.

##### CanvasNet-Fonts-FontDirectoryScanner-Cancellation: Cancellation and Argument Checks

**Tests**: `FontDirectoryScanner_Scan_CanceledTokenAndNullDirectories_Throw`

Scans with an already canceled token and asserts `OperationCanceledException`; passes `null` directories and
asserts `ArgumentNullException`.
