## PptxDocument Unit Verification Design

<!-- cspell:ignore ooxml pptx srgb -->

This document describes the unit-level verification strategy for the `PptxDocument` class.

`PptxDocument` is distributed as the separate `DemaConsulting.CanvasNet.Pptx` NuGet package
(namespace `DemaConsulting.CanvasNet.Pptx`), which references the core `DemaConsulting.CanvasNet`
package; its unit tests live in the sibling `DemaConsulting.CanvasNet.Pptx.Tests` project.

### Verification Approach

The `PptxDocument` unit is verified through unit tests that exercise its OOXML package layer
(ZIP opening, `[Content_Types].xml` resolution, relationship resolution) and its Phase 1b
presentation/theme/master/layout/slide model and placeholder-inheritance resolver, in isolation,
through the public API plus `internal` members exposed to the test project via
`InternalsVisibleTo` (`ResolvePart`/`ResolveRelationship` in Phase 1a; `GetTheme`/`GetMaster`/
`GetLayout`/`GetSlide`/`ResolvePlaceholderProperties` and every new `internal` record type in
Phase 1b). Every test builds its own minimal, in-memory `.pptx`-shaped ZIP package via a private
helper (`BuildPackage`, parameterized by an arbitrary set of entry name/content pairs) using the
BCL `System.IO.Compression.ZipArchive` writer over a `MemoryStream` - this is a new fixture
pattern for this repository (no existing precedent uses `ZipArchive` to build an in-memory test
fixture), chosen as a natural, low-risk extension of the already-established "hand-authored,
in-memory, byte-exact fixture" philosophy used throughout `PdfDocumentTests.cs` for this same
class of structural/malformed-input test, without introducing an undocumented binary blob for a
trivial package-layer scenario. No file-based fixture exists for this unit (unlike
`PdfFixtures/*.pdf`); every fixture is constructed entirely in-memory, at test-method scope.
Phase 1b's placeholder-inheritance tests additionally construct `PptxPlaceholder`/`PptxTheme`
records directly in C# (bypassing XML parsing entirely), using marker-attribute XElements to
unambiguously assert which level's property fragment "won" the fallback chain.

Because `PptxDocument`'s dependencies (`System.IO.Compression.ZipArchive`, `System.Xml.Linq`,
and, as of Phase 1b, `DemaConsulting.CanvasNet.Canvas.Rgba32`) are BCL types or an already-tested
sibling-package type, not external services, no mocking or stubbing is required. Tests assert on
resolved content-type strings, resolved relationship target paths, resolved slide size/count,
resolved theme colors/fonts, parsed placeholder type/idx values, resolved effective property
fragments, and thrown exception types - never on "no exception thrown" alone, so every test can
actually fail if the implementation is wrong.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, databases, or network access required; every fixture is
  built entirely in-memory
- **Isolation**: Each test method constructs its own in-memory package; no shared state between
  tests

### Unit-Level Test Scenarios

#### CanvasNetPptx-PptxDocument-OpenStream: Open(Stream) Buffers Input and Validates Null Argument

**Tests**: `PptxDocument_Open_WellFormedMinimalPackage_Succeeds`,
`PptxDocument_Open_NullStream_ThrowsArgumentNullException`

Proves `Open(Stream)` succeeds against a minimal, well-formed, in-memory package (constructed via
`BuildMinimalValidPackage`, which declares only `[Content_Types].xml` and `_rels/.rels`), and
proves a null stream throws `ArgumentNullException` before any parsing is attempted.

#### CanvasNetPptx-PptxDocument-OpenPath: Open(string) Validates Null and Empty/Whitespace Path Arguments

**Tests**: `PptxDocument_Open_NullPath_ThrowsArgumentNullException`,
`PptxDocument_Open_WhitespacePath_ThrowsArgumentException`

Proves `Open(string)` throws `ArgumentNullException` for a null path and `ArgumentException` for
a whitespace-only path, mirroring `PdfDocument.Open(string, ...)`'s own validation order and
exception types.

#### CanvasNetPptx-PptxDocument-ZipValidation: Corrupt/Non-ZIP Stream Throws InvalidDataException

**Test**: `PptxDocument_Open_CorruptNonZipStream_ThrowsInvalidDataException`

Proves a stream of arbitrary bytes that is not a valid ZIP local-file-header signature throws
`InvalidDataException` from `Open`, rather than an unrelated `ZipArchive`-internal exception type
leaking through uncaught.

#### CanvasNetPptx-PptxDocument-ContentTypesValidation: Missing [Content_Types].xml Throws InvalidDataException

**Test**: `PptxDocument_Open_MissingContentTypes_ThrowsInvalidDataException`

Proves a package containing only `_rels/.rels` (no `[Content_Types].xml` entry at all) throws
`InvalidDataException` from `Open`.

#### CanvasNetPptx-PptxDocument-PackageRelationshipsValidation: Missing _rels/.rels Throws InvalidDataException

**Test**: `PptxDocument_Open_MissingPackageRelationships_ThrowsInvalidDataException`

Proves a package containing only `[Content_Types].xml` (no package-level `_rels/.rels` entry at
all) throws `InvalidDataException` from `Open`.

#### CanvasNetPptx-PptxDocument-ContentTypeOverride: Override Content Type Takes Precedence Over Default Extension

**Test**: `PptxDocument_ResolvePart_OverrideContentType_TakesPrecedenceOverDefaultExtension`

Declares a `[Content_Types].xml` with both a `Default Extension="xml"` mapping to
`"application/xml"` and a part-specific `Override PartName="/ppt/presentation.xml"` mapping to
the real presentation content type. Asserts `ResolvePart("ppt/presentation.xml")` returns the
override value, not the default, proving the documented precedence rule.

#### CanvasNetPptx-PptxDocument-ContentTypeDefault: Default Extension Resolves, Unknown Part Fails Closed

**Tests**: `PptxDocument_ResolvePart_NoOverride_ResolvesDefaultExtensionContentType`,
`PptxDocument_ResolvePart_UnknownPart_ThrowsInvalidDataException`

Proves a part with no `Override` entry resolves via the `Default` extension mapping, and proves
requesting a part path the package does not actually contain throws `InvalidDataException`
rather than silently falling through to a spurious default.

#### CanvasNetPptx-PptxDocument-RelationshipResolution: Known Relationship Resolves, Unknown Relationship Fails Closed

**Tests**: `PptxDocument_Open_WellFormedMinimalPackage_Succeeds` (also proves package-root
relationship resolution via `ResolveRelationship(string.Empty, "rId1")`),
`PptxDocument_ResolveRelationship_UnknownRelationshipId_ThrowsInvalidDataException`

Proves a known relationship ID in the package-level `_rels/.rels` resolves to its declared
target part path, and proves requesting an unknown relationship ID throws
`InvalidDataException`.

#### CanvasNetPptx-PptxDocument-RelativeTargetTraversal: Relative "../" Target Resolves, Root Escape Fails Closed

**Tests**: `PptxDocument_ResolveRelationship_RelativeTargetWithParentTraversal_ResolvesCorrectTarget`,
`PptxDocument_ResolveRelationship_TargetEscapesPackageRoot_ThrowsInvalidDataException`

Declares `ppt/slides/slide1.xml`'s own relationship to `"../slideLayouts/slideLayout1.xml"` and
asserts it resolves to `"ppt/slideLayouts/slideLayout1.xml"` (relative to the source part's own
directory, not the package root). Separately, declares a relationship whose target traverses more
`".."` segments than the source part has preceding directories, asserting
`InvalidDataException` is thrown rather than producing a nonsensical or out-of-package path.

#### CanvasNetPptx-PptxDocument-Dispose: Dispose Is Idempotent

**Test**: `PptxDocument_Dispose_CalledTwice_DoesNotThrow`

Opens a minimal package, calls `Dispose()` twice, and asserts (via `Record.Exception`) that the
second call throws nothing.

#### CanvasNetPptx-PptxDocument-PresentationParsing: SlideCount/SlideSize/GetSlideSizeInPixels Resolve Correctly

**Tests**: `PptxDocument_Open_MultiSlidePresentation_SlideCountAndSlideSizeResolveCorrectly`,
`PptxDocument_GetSlideSizeInPixels_ValidDpi_ComputesExpectedPixelSize`,
`PptxDocument_GetSlideSizeInPixels_NonPositiveDpi_ThrowsArgumentOutOfRangeException`,
`CanvasNetPptx_SystemIntegration_PptxOpenPresentation_SlideCountAndSizeResolveEndToEnd`

Opens a multi-slide presentation package and asserts `SlideCount` and `SlideSize` resolve to the
declared `<p:sldIdLst>`/`<p:sldSz>` values; asserts `GetSlideSizeInPixels(96f)` computes the
expected pixel dimensions for a known EMU size (9144000x6858000 EMU -> 960x720px at 96 DPI); and
asserts a non-positive/non-finite `dpi` throws `ArgumentOutOfRangeException`.

#### CanvasNetPptx-PptxDocument-PresentationValidation: Malformed Presentation Parts Fail Closed

**Tests**: `PptxDocument_Open_MissingOfficeDocumentRelationship_ThrowsInvalidDataException`,
`PptxDocument_Open_PresentationMissingSldSz_ThrowsInvalidDataException`,
`PptxDocument_Open_PresentationEmptySlideList_ThrowsInvalidDataException`,
`PptxDocument_Open_PresentationNonNumericSldSz_ThrowsInvalidDataException`,
`CanvasNetPptx_SystemIntegration_PptxOpenValidationEmptySlideList_ThrowsInvalidDataException`

Proves each of the following throws `InvalidDataException` from `Open`: a package with no
`/officeDocument` relationship; a presentation missing `<p:sldSz>`; a presentation with an empty
`<p:sldIdLst>`; and a presentation whose `<p:sldSz>` has a non-numeric `cx` attribute.

#### CanvasNetPptx-PptxDocument-SlideSizePixelConversion: EMU-to-Pixel Conversion Rounds Correctly and Validates DPI

**Tests**: `PptxDocument_GetSlideSizeInPixels_ValidDpi_ComputesExpectedPixelSize`,
`PptxDocument_GetSlideSizeInPixels_NonPositiveDpi_ThrowsArgumentOutOfRangeException`

Proves `GetSlideSizeInPixels` computes the exact expected pixel dimensions for a known EMU slide
size at a known DPI, and rejects a non-positive or non-finite `dpi` with
`ArgumentOutOfRangeException`.

#### CanvasNetPptx-PptxDocument-ThemeColorScheme: srgbClr and sysClr Color Scheme Slots Resolve Correctly

**Tests**: `PptxDocument_GetTheme_SrgbClrColorScheme_ResolvesRgba32Values`,
`PptxDocument_GetTheme_SysClrColorScheme_ResolvesLastClrValue`

Proves all 12 `<a:clrScheme>` slots resolve to the expected `Rgba32` value when each is an
`<a:srgbClr val="RRGGBB"/>`, and separately proves an `<a:sysClr val="..." lastClr="RRGGBB"/>`
slot resolves to its cached `lastClr` RGB equivalent.

#### CanvasNetPptx-PptxDocument-ThemeFontScheme: Major/Minor Font Scheme Typefaces Resolve Correctly

**Test**: `PptxDocument_GetTheme_FontScheme_ResolvesMajorMinorTypefaces`

Proves a theme's `<a:fontScheme>` resolves both the major and minor font collections' Latin,
East Asian, and complex-script typeface names correctly.

#### CanvasNetPptx-PptxDocument-MasterPlaceholderParsing: Master Placeholder Shapes and Theme Relationship Resolve

**Test**: `PptxDocument_GetMaster_PlaceholderShapes_ParsedWithTypeAndIdx`

Proves a slide master's immediate placeholder shapes parse with their explicit `type`/`idx`
attribute values, and that the master's `/theme` relationship resolves to the expected theme
part path.

#### CanvasNetPptx-PptxDocument-LayoutPlaceholderParsing: Layout Placeholders Default Correctly, Master Relationship Resolves

**Test**: `PptxDocument_GetLayout_PlaceholderShapes_ParsedWithDefaultTypeAndIdx`

Proves a slide layout's `<p:ph/>` element with no `type`/`idx` attributes defaults to `"obj"`/`0`
per the OOXML schema default, and that the layout's `/slideMaster` relationship resolves to the
expected master part path.

#### CanvasNetPptx-PptxDocument-SlidePlaceholderParsing: Slide Placeholders Parse, Non-Placeholders Excluded, Index Checked

**Tests**: `PptxDocument_GetSlide_ImmediatePlaceholderShapes_ParsedStructurally`,
`PptxDocument_GetSlide_IndexOutOfRange_ThrowsArgumentOutOfRangeException`

Proves a slide's immediate placeholder shapes parse structurally while a sibling non-placeholder
`<p:sp>` (no `<p:ph>` descendant) is excluded, and that an out-of-range slide index (negative or
`>= SlideCount`) throws `ArgumentOutOfRangeException`.

#### CanvasNetPptx-PptxDocument-PlaceholderInheritanceIdxMatch: Slide-to-Layout Match Is Idx-Only, Miss Short-Circuits

**Tests**: `PptxDocumentInheritance_ResolvePlaceholderProperties_IdxMatchAtLayout_UsesLayoutSpPr`,
`PptxDocumentInheritance_ResolvePlaceholderProperties_NoIdxMatchAtLayout_FallsThroughToMasterOnly`

Proves a slide placeholder matches a layout placeholder sharing the same `idx` (regardless of
differing `type`), resolving the layout placeholder's `<p:spPr>`/`<a:lstStyle>`. Separately,
proves that when no layout placeholder shares the slide placeholder's `idx`, the chain
short-circuits entirely - a master placeholder whose `type` matches the slide's own `type` is
deliberately *not* consulted, proving there is no type-based retry at this hop.

#### CanvasNetPptx-PptxDocument-PlaceholderInheritanceTypeMatch: Layout-to-Master Matching Is Type-Only via Remap Table

**Tests**: `PptxDocumentInheritance_ResolvePlaceholderProperties_TypeMatchAtMaster_IgnoresMismatchedIdx`,
`PptxDocumentInheritance_ResolvePlaceholderProperties_RemappedBodyLikeType_MatchesMasterBodyPlaceholder`

Proves a matched layout placeholder resolves a master placeholder by `type` alone (a deliberately
mismatched `idx` between the layout and master placeholders does not prevent the match), and
proves a layout placeholder whose type remaps to `"body"` (for example `"subTitle"`) matches a
master placeholder whose own `type` is literally `"body"`.

#### CanvasNetPptx-PptxDocument-PlaceholderInheritancePerCategory: Categories Independent, Theme Is Unchanged Context

**Tests**:
`PptxDocumentInheritance_ResolvePlaceholderProperties_PerCategoryIndependentFallthrough_SlideOverridesFillButNotText`,
`PptxDocumentInheritance_ResolvePlaceholderProperties_NoMatchAtAnyLevel_ReturnsNullForBothCategories`

Proves `<p:spPr>` and `<p:txBody>/<a:lstStyle>` resolve independently - a slide-level `<p:spPr>`
with no slide-level `<a:lstStyle>` resolves the slide's own fill while still falling through to
the layout for text style. Separately, proves that when no placeholder matches at any level, both
effective property categories resolve to `null` while the resolved theme is still returned
unchanged as context.

## Acceptance Criteria

A unit-level test run passes when all scenarios above pass without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong return value
constitutes a failure. Collectively, these scenarios cover the complete current `PptxDocument`
package layer (Phase 1a: ZIP opening, content-type resolution, relationship resolution including
relative-target traversal and its root-escape guard, and the public API's argument validation and
disposal contract) and the presentation/theme/master/layout/slide model and placeholder
property-inheritance resolver (Phase 1b: slide size/count, EMU-to-pixel conversion, theme
color/font scheme resolution, master/layout/slide structural placeholder parsing, and the
verified ECMA-376 placeholder-matching and per-category property-resolution algorithm). No shape
geometry/paint rendering, non-placeholder (freeform) shape parsing, font loading, or rendering API
is covered because none is implemented yet.
