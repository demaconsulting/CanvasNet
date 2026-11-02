### FontResolver Unit Verification Design

<!-- cspell:ignore Liberation -->

This document describes the unit-level verification strategy for the `FontResolver` class and its
`FontResolverOptions`, `ResolvedFont` and `FontSource` types.

#### Verification Approach

`FontResolverTests.cs` resolves requests against controlled temporary directories populated with
copies of the real bundled Liberation fonts (extracted from the assembly's embedded resources), so
every test is deterministic regardless of what is installed on the machine running the tests. The
installed-font step is exercised through the resolver's internal constructor, which takes the list
of installed faces: the tests scan a temporary "system" directory and inject the result, so the
order of the sources (additional directories, installed fonts, bundled names) can be asserted
without the host's fonts. The family names of the bundled fonts (`Liberation Sans`, `Liberation
Serif`, `Liberation Mono`) are used for the named lookups because they are known, and are also the
three bundled family names, which lets one test prove both that a directory beats the bundled name
and that a common alias beats the directory. A vanished-file test removes a scanned font file after
the first lookup (the scan is cached, the load is not) to prove the failure path.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None; real temporary directories created and removed by each test, and the
  injected installed-face list described above
- **Isolation**: Each test creates its own uniquely named temporary directories and its own
  resolver; the process-wide font load cache is shared but keyed by the unique file paths

#### Acceptance Criteria

A unit test run passes when every scenario below passes, and when every named test method listed
for each requirement ID passes across every target framework and every operating system in this
repository's CI matrix.

#### Test Scenarios

##### CanvasNet-Fonts-FontResolver-DefaultAndGeneric: Bundled Defaults, Keywords And Aliases

**Tests**: `FontResolver_Resolve_DefaultAndGenericKeywords_UseTheBundledFonts`,
`FontResolver_Resolve_BoldAndItalic_SelectTheBundledStyleVariant`,
`FontResolver_Resolve_CommonAliases_AreMappedBeforeDirectoriesOnlyWhenRequested`

Asserts that `null`, empty, blank and commas-only lists, and the keywords `sans-serif`,
`system-ui`, `cursive`, `fantasy` (in any case, quoted, or ahead of another keyword), resolve to the very same bundled
Liberation Sans object with no failure; that `ui-serif` and a list whose first family is missing
and second is `'monospace'` resolve to Liberation Serif and Mono; that bold and italic select the
bundled style variant, are reported on the result, and that a repeated request returns the same
`ResolvedFont`; and that with `MapCommonAliasesToBundled` the names `Liberation Serif` and
`Times New Roman` resolve to the bundled serif even though an additional directory holds a
`Liberation Serif` face, which the same directory answers when the option is off.

##### CanvasNet-Fonts-FontResolver-LookupOrder: Directories, Then Installed Fonts, Then Bundled Names

**Tests**: `FontResolver_Resolve_NamedFamily_SearchesDirectoriesThenSystemThenBundledNames`,
`FontResolver_Resolve_SystemFontsDisabled_NeverAsksForTheInstalledFaces`

With a bold serif in one directory and an italic serif in another and in the injected installed
faces, asserts that the first directory wins a tie of style, that the order of the directories
decides it, that an exactly matching style wins over the order, that the installed font answers
when the directories do not have the family, that the bundled name answers when neither has it
(the same bundled object, in the requested style, with no failure), and that a family none has
falls through. Asserts that a resolver with installed fonts disabled never invokes the supplier
of installed faces (it throws if asked) and that a keyword does not invoke it either.

##### CanvasNet-Fonts-FontResolver-FailureReporting: Why The Fallback Was Used

**Tests**: `FontResolver_Resolve_UnknownFamily_FallsBackToSansAndReportsWhyOnlyWhenNothingWasMet`,
`FontResolver_Resolve_FoundFaceThatCannotBeLoaded_FailsTheFamilyAndReportsTheFile`

Asserts that an unknown family (and `Arial` without the alias option) gives the bundled Sans in the
requested style with the failure `no such font family was found`; that a later family that is met
clears the failure; and that a font file removed after the scan gives the bundled Sans (not the
bundled Mono of the same name: the found family is spent) with the failure naming the file, and
that a later family of the same list is still tried.

##### CanvasNet-Fonts-FontResolver-AdditionalDirectoryScan: Bounded, Tolerant, Cancellable

**Tests**: `FontResolver_Resolve_AdditionalDirectories_AreScannedWithTheLimitsAndSkipUnusableInput`,
`FontResolver_Resolve_CanceledToken_StopsTheScanOnlyWhenThereIsOneToDo`

Scans a missing directory and a directory with two fonts and a text file named `.ttf`, with and
without a one-font-file limit, and asserts the unbounded resolver finds the italic face and the
limited one only the first (bold) face; asserts that a resolver with a canceled token throws
`OperationCanceledException` on every lookup that needs the directories (the canceled scan is not
kept), and does not throw for a keyword or when there is no additional directory.

##### CanvasNet-Fonts-FontResolver-Options: Copying, Defaults And Concurrency

**Tests**: `FontResolver_Constructor_CopiesDirectoriesAndRejectsNullMembers`,
`FontResolver_Resolve_ConcurrentRequests_ShareOneResultFont`

Asserts that a directory added to the caller's list after construction is not searched, that
options with a `null` directory list or `null` scan limits are rejected with
`ArgumentNullException`, the documented defaults of `FontResolverOptions`, and that 64 parallel
requests for one family list all return the same font object.
