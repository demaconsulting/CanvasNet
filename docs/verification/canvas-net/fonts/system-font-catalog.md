### SystemFontCatalog Unit Verification Design

<!-- cspell:ignore Dejavu Nimbus Consolas ttcf dogfoods dogfooding -->

This document describes the unit-level verification strategy for the `SystemFontCatalog` class.

#### Verification Approach

The `SystemFontCatalog` unit is verified through three distinct test files, each covering a
different concern:

- `SystemFontCatalogTests.cs` exercises the scoring algorithm (`FindBestMatchCore`) against
  small, fully-controlled synthetic candidate lists, and the directory-scan tolerance behavior
  (`BuildCatalogFromRoots`) against real temporary directories deliberately constructed to be
  missing or to contain an unparseable file - both via `InternalsVisibleTo`, so every test is
  deterministic regardless of what happens to be installed on the machine running the tests.
- `SystemFontCatalogRealDiscoveryIntegrationTests.cs` runs the real, un-mocked `Fonts` property
  and `FindBestMatch` public entry point against the actual host filesystem, proving the
  production directory-scan roots and the production `GenericSansFamilies` bucket list actually
  work together end to end on a real operating system - written as weak, always-true-or-gracefully-
  empty assertions (never skipped) so they remain deterministic across this repository's full
  Windows/Linux/macOS CI matrix, per this repository's own testing-principles guidance to
  conditionally strengthen an assertion rather than skip a test.
- `BundledLiberationFontsTests.cs` dogfoods every one of the 12 real bundled Liberation `.ttf`
  files bundled as embedded resources, loading each directly through
  `SystemFontCatalog.LoadBundledFallbackCore` (the same code path `LoadBundledFallback` itself
  uses) and asserting each one's `TrueTypeFont.GetNameInfo()`/`IsBold`/`IsItalic`/`IsFixedPitch`
  match its expected family/style, proving the bundled files are genuine, valid, parseable
  TrueType fonts (not merely present on disk).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; every dependency is either an in-house helper (`TrueTypeFont`,
  `SyntheticFontBuilder`) or a real temporary directory/file created and cleaned up by the test
  itself
- **Isolation**: Each synthetic test builds its own candidate list or its own temporary
  directory tree; the real-discovery integration tests read only the real host filesystem and
  never write to it

#### Acceptance Criteria

A unit test run passes when every scenario below passes without error or unexpected exception,
and when every named test method listed for each requirement ID passes across every target
framework and every operating system in this repository's CI matrix.

#### Test Scenarios

##### CanvasNet-Fonts-SystemFontCatalog-DirectoryScanDiscovery: Directory-Scan Discovery Across the Host OS

**Tests**: `SystemFontCatalog_RealFilesystem_ScanKnownOsDirectories_DiscoversFontsOrIsGracefullyEmpty`,
`SystemFontCatalog_RealFilesystem_KnownPlatformFontFamily_ResolvesToRealFontWhenInstalled`

Runs `Fonts` and `FindBestMatch` against the real host filesystem's well-known font directories,
asserting either that at least one real font was discovered and every discovered entry has a
non-empty family name and a `.ttf`/`.ttc`/`.otf` file path, or that the catalog is gracefully
empty (never throwing) - and that a well-known platform font family resolves to a real, loadable
font when it happens to be installed, without depending on any particular font actually being
present on the CI runner.

##### CanvasNet-Fonts-SystemFontCatalog-LazyProcessLifetimeCache: Fonts Is Built Once Per Process and Cached

**Tests**: `SystemFontCatalog_Fonts_IsLazilyBuiltOnceAndCachedForProcessLifetime`

Accesses `Fonts` twice in succession and asserts the exact same `IReadOnlyList<SystemFontInfo>`
instance (by reference) is returned both times, proving the underlying scan runs at most once
per process.

##### CanvasNet-Fonts-SystemFontCatalog-ToleratesMissingOrUnparseable: Missing/Unparseable Inputs Are Skipped

**Tests**: `SystemFontCatalog_Fonts_MissingScanDirectory_SkippedWithoutThrowing`,
`SystemFontCatalog_Fonts_UnparseableCandidateFile_SkippedWithoutThrowing`

Calls `BuildCatalogFromRoots` with a root pointing at a directory that does not exist, and
separately with a root containing a `.ttf`-named file whose bytes are not a valid font, asserting
in both cases the call returns normally (an empty or otherwise unaffected result) rather than
throwing.

##### CanvasNet-Fonts-SystemFontCatalog-FindBestMatchExactFamily: Exact Family-Name Matching Is Case-Insensitive and Style-Aware

**Tests**: `SystemFontCatalog_FindBestMatch_ExactFamilyNameCaseInsensitive_ReturnsMatch`,
`SystemFontCatalog_FindBestMatch_ExactFamilyStyleMismatch_PrefersClosestStyleVariant`

Builds a small synthetic candidate list containing an exact family-name match under a different
letter case, and separately a family with multiple bold/italic style variants, asserting
`FindBestMatchCore` returns the case-insensitive match and, respectively, the style variant
minimizing mismatched bold/italic flags.

##### CanvasNet-Fonts-SystemFontCatalog-FindBestMatchGenericBucket: Generic-Bucket Fallback Selects a Well-Known Family

**Tests**: `SystemFontCatalog_FindBestMatch_NoExactFamily_FallsBackToGenericSansBucket`,
`SystemFontCatalog_FindBestMatch_NoExactFamily_FallsBackToGenericSerifBucket`,
`SystemFontCatalog_FindBestMatch_NoExactFamily_FallsBackToGenericFixedPitchBucket`

Builds a synthetic candidate list with no exact match for the requested family, but containing
one of each generic bucket's well-known family names, asserting `FindBestMatchCore` returns the
sans/serif/fixed-pitch bucket entry respectively, selecting the fixed-pitch bucket over the serif
bucket when both `serif` and `fixedPitch` are requested together.

##### CanvasNet-Fonts-SystemFontCatalog-FindBestMatchNoMatch: No Match Returns Null Without Consulting the Bundled Fallback

**Tests**: `SystemFontCatalog_FindBestMatch_EmptyCandidateList_ReturnsNull`,
`SystemFontCatalog_FindBestMatch_NoFamilyAndNoBucketMatch_ReturnsNull`

Calls `FindBestMatchCore` with an empty candidate list, and separately with a non-empty list
containing no exact match and no generic-bucket match, asserting `null` is returned in both
cases.

##### CanvasNet-Fonts-SystemFontCatalog-LoadBundledFallback: Every Bundled Liberation Style Loads Successfully

**Tests**: `BundledFonts_AllTwelveLiberationVariants_LoadSuccessfullyAndExposeExpectedNameAndStyle`

A `[Theory]` with 12 `[MemberData]` rows, one per bundled Liberation `.ttf` file, each loading the
file via `LoadBundledFallbackCore` and asserting `TrueTypeFont.GetNameInfo()`'s family name and
the `IsBold`/`IsItalic` properties match the expected values for that file's family/style,
proving every bundled file is a genuine, valid, parseable TrueType font.

##### CanvasNet-Fonts-SystemFontCatalog-LoadBundledFallbackMissingResourceGuard: Missing Resource Throws

**Tests**: `SystemFontCatalog_LoadBundledFallback_MissingEmbeddedResource_ThrowsInvalidOperationException`

Calls `LoadBundledFallbackCore` with a deliberately wrong bundled file name (one guaranteed not to
correspond to any real embedded resource) and asserts `InvalidOperationException` is thrown.
