## PptxDocument Unit Verification Design

<!-- cspell:ignore ooxml pptx srgb xfrm prst cust patt bodyPr normAutofit noAutofit spAutoFit pPr -->
<!-- cspell:ignore rPr txBody txStyles lstStyle defRPr lnSpc spcBef spcAft justLow fontScale -->
<!-- cspell:ignore spcPct spcPts Ordinally -->
<!-- cspell:ignore srcRect blipFill tblGrid gridCol tcPr hMerge vMerge gridSpan rowSpan grpSp -->
<!-- cspell:ignore grpSpPr cxnSp graphicFrame tableStyleId spTree contentPart -->
<!-- cspell:ignore pythonpptx Autoshape groupshape aiden0z Aiden aiden reparents FAFAF -->
<!-- cspell:ignore bgRef bgFillStyleLst phClr fmtScheme asvg -->
<!-- cspell:ignore tblStyle wholeTbl tcStyle tcBdr insideH insideV bandRow firstRow band1H band2H -->
This document describes the unit-level verification strategy for the `PptxDocument` class.

`PptxDocument` is distributed as the separate `DemaConsulting.CanvasNet.Pptx` NuGet package
(namespace `DemaConsulting.CanvasNet.Pptx`), which references the core `DemaConsulting.CanvasNet`
package; its unit tests live in the sibling `DemaConsulting.CanvasNet.Pptx.Tests` project.

### Verification Approach

The `PptxDocument` unit is verified through unit tests that exercise its OOXML package layer
(ZIP opening, `[Content_Types].xml` resolution, relationship resolution) and its current
presentation/theme/master/layout/slide model, placeholder-inheritance resolver, DrawingML shape
geometry/paint resolution, and text layout/rendering, in isolation, through the public API plus
`internal` members exposed to the test project via `InternalsVisibleTo` (`ResolvePart`/
`ResolveRelationship` from Phase 1a; `GetTheme`/`GetMaster`/`GetLayout`/`GetSlide`/
`ResolvePlaceholderProperties` and every other `internal` record type and resolver added through
Phase 1d). Every test builds its own minimal, in-memory `.pptx`-shaped ZIP package via a private
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
unambiguously assert which level's property fragment "won" the fallback chain. Phase 1d's
text-layout tests additionally link the sibling `DemaConsulting.CanvasNet.Tests` project's
`TestSupport` folder (via an MSBuild `<Compile Include>` glob, mirroring
`DemaConsulting.CanvasNet.Pdf.Tests`'s own established precedent) to reuse the shared
`SyntheticFontBuilder` helper, constructing a hand-built `TrueTypeFont` with deterministic
metrics so every word-wrap/alignment/anchor/autofit expectation can be hand-computed exactly in
EMU, and Phase 1d's rendering tests use a synthetic filled-square glyph font for deterministic,
installed-font-independent painted-pixel assertions, mirroring `PdfDocumentTests.cs`'s own
painted-pixel assertion style.

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
`PptxDocument_Open_NullStream_ThrowsArgumentNullException`,
`PptxDocument_Open_NonTerminatingOversizedStream_ThrowsInvalidDataException`

Proves `Open(Stream)` succeeds against a minimal, well-formed, in-memory package (constructed via
`BuildMinimalValidPackage`, which declares only `[Content_Types].xml` and `_rels/.rels`), proves a
null stream throws `ArgumentNullException` before any parsing is attempted, and proves a
non-terminating, oversized stream (a synthetic `InfiniteZeroStream` that never reaches end-of-stream
and would otherwise be copied without bound) throws `InvalidDataException` once more than the
documented `MaxPackageBytes` (256 MiB) bound has been read, rather than exhausting memory or
blocking indefinitely.

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

**Tests**: `PptxDocument_Open_MissingContentTypes_ThrowsInvalidDataException`,
`PptxDocument_Open_OversizedContentTypesPart_ThrowsInvalidDataException`,
`PptxDocument_Open_ContentTypesWrongRoot_ThrowsInvalidDataException`,
`PptxDocument_Open_ContentTypesDefaultMissingRequiredAttribute_ThrowsInvalidDataException`,
`PptxDocument_Open_ContentTypesOverrideMissingRequiredAttribute_ThrowsInvalidDataException`

Proves a package containing only `_rels/.rels` (no `[Content_Types].xml` entry at all) throws
`InvalidDataException` from `Open`. Also proves `Open` fails closed for an attacker-controlled
`[Content_Types].xml` part: one padded past the bounded character budget (an "XML bomb" pattern)
throws `InvalidDataException` rather than risking unbounded memory use; a part whose root element
is not the OPC `Types` element throws `InvalidDataException` rather than being silently accepted
with no `Default`/`Override` entries found; and a `Default`/`Override` element missing one of its
required attributes (`Extension`/`ContentType` or `PartName`/`ContentType` respectively) throws
`InvalidDataException` rather than being silently discarded.

#### CanvasNetPptx-PptxDocument-MediaPartSizeValidation: Oversized Decompressed Part Throws InvalidDataException

**Tests**: `PptxDocument_GetPartBytes_OversizedDecompressedEntry_ThrowsInvalidDataException`,
`PptxDocument_GetPartBytes_WellFormedPart_ReturnsExactBytes`

Proves `GetPartBytes` rejects a part whose actual decompressed content exceeds the documented
256 MiB bound with `InvalidDataException`, even when the part's compressed ZIP entry is tiny (a
"zip bomb" - a highly-compressible, all-zero payload), proving the bound is enforced from bytes
actually read during the copy rather than from any declared or compressed size. Also proves a
normal-sized binary part is still returned byte-for-byte unchanged, confirming the new bound does
not disturb ordinary media-part reads.

#### CanvasNetPptx-PptxDocument-PackageRelationshipsValidation: Missing _rels/.rels Throws InvalidDataException

**Tests**: `PptxDocument_Open_MissingPackageRelationships_ThrowsInvalidDataException`,
`PptxDocument_Open_PackageRelsWrongRoot_ThrowsInvalidDataException`,
`PptxDocument_Open_RelationshipMissingRequiredAttribute_ThrowsInvalidDataException`

Proves a package containing only `[Content_Types].xml` (no package-level `_rels/.rels` entry at
all) throws `InvalidDataException` from `Open`. Also proves a relationships part whose root
element is not the OPC `Relationships` element throws `InvalidDataException` rather than being
silently accepted with no relationships found, and a `<Relationship>` element missing one of its
required attributes (`Id`, `Type`, or `Target`) throws `InvalidDataException` rather than being
silently skipped.

#### CanvasNetPptx-PptxDocument-CaseSensitivePartNames: OPC Part Names Compare Ordinally (Case-Sensitively)

**Tests**: `PptxDocument_ResolvePart_PartNamesDifferingOnlyByCase_ResolveAsDistinctParts`,
`PptxDocument_Open_WrongCaseContentTypesPartName_ThrowsInvalidDataException`,
`PptxDocument_Open_DuplicateEntryNames_ThrowsInvalidDataException`

Declares a package with both `ppt/slides/Slide1.xml` and `ppt/slides/slide1.xml`, each given its
own distinct `Override` content type, and asserts `ResolvePart` resolves each to its own distinct
content type rather than the two names colliding under a case-insensitive comparison. Also proves
a package whose reserved content-types part is incorrectly cased (for example
`[content_types].xml` instead of `[Content_Types].xml`) is treated as if the part were entirely
absent, throwing `InvalidDataException`; and proves a package containing two ZIP entries with
exactly identical names throws `InvalidDataException` from `Open` rather than one silently
shadowing the other.

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
`PptxDocument_ResolveRelationship_UnknownRelationshipId_ThrowsInvalidDataException`,
`PptxDocument_ResolveRelationship_ExternalTargetMode_ThrowsInvalidDataException`

Proves a known relationship ID in the package-level `_rels/.rels` resolves to its declared
target part path, and proves requesting an unknown relationship ID throws
`InvalidDataException`. Also proves a relationship declaring `TargetMode="External"` throws
`InvalidDataException` rather than being resolved as if it were an in-package target.

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
`PptxDocument_Open_SlideIdRelationshipWrongType_ThrowsInvalidDataException`,
`PptxDocument_GetSlideSizeInPixels_OversizedDimension_ThrowsInvalidDataException`,
`CanvasNetPptx_SystemIntegration_PptxOpenValidationEmptySlideList_ThrowsInvalidDataException`

Proves each of the following throws `InvalidDataException` from `Open`: a package with no
`/officeDocument` relationship; a presentation missing `<p:sldSz>`; a presentation with an empty
`<p:sldIdLst>`; a presentation whose `<p:sldSz>` has a non-numeric `cx` attribute; and a
`<p:sldId>` whose resolved relationship `Type` does not identify a slide part (for example one
pointed at a theme part instead), so a malformed package cannot silently report a non-slide part
as a valid slide. Also proves `GetSlideSizeInPixels` throws `InvalidDataException` (rather than
overflowing or returning a negative size) for a slide dimension whose pixel conversion would
exceed the representable `int` range.

#### CanvasNetPptx-PptxDocument-SlideSizePixelConversion: EMU-to-Pixel Conversion Rounds Correctly and Validates DPI

**Tests**: `PptxDocument_GetSlideSizeInPixels_ValidDpi_ComputesExpectedPixelSize`,
`PptxDocument_GetSlideSizeInPixels_NonPositiveDpi_ThrowsArgumentOutOfRangeException`

Proves `GetSlideSizeInPixels` computes the exact expected pixel dimensions for a known EMU slide
size at a known DPI, and rejects a non-positive or non-finite `dpi` with
`ArgumentOutOfRangeException`.

#### CanvasNetPptx-PptxDocument-ThemeColorScheme: srgbClr and sysClr Color Scheme Slots Resolve Correctly

**Tests**: `PptxDocument_GetTheme_SrgbClrColorScheme_ResolvesRgba32Values`,
`PptxDocument_GetTheme_SysClrColorScheme_ResolvesLastClrValue`,
`PptxDocument_GetTheme_SrgbClrEightDigitValue_ThrowsInvalidDataException`

Proves all 12 `<a:clrScheme>` slots resolve to the expected `Rgba32` value when each is an
`<a:srgbClr val="RRGGBB"/>`, and separately proves an `<a:sysClr val="..." lastClr="RRGGBB"/>`
slot resolves to its cached `lastClr` RGB equivalent. Also proves an eight-digit `val`
(`AARRGGBB`) throws `InvalidDataException` rather than being silently accepted with its leading
byte misinterpreted as alpha, since OOXML color values are always exactly six hex digits.

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

#### CanvasNetPptx-PptxDocument-EffectivePlaceholderTypeForTextStyle: Omitted-Type Resolves to Idx-Matched Layout Type

**Tests**: `ResolveEffectiveRunProperties_SectionHeaderTitlePlaceholderWithOmittedType_ResolvesMasterTitleStyleNotBodyStyle`,
`Render_SectionHeaderTitleWithOmittedType_PaintsIdenticallyToExplicitTitleType`

Proves a "Section Header" layout's title placeholder whose slide-level `<p:ph idx="0"/>` omits
`type` resolves `EffectivePlaceholderType` to the idx-matched layout placeholder's own
`type="title"`, and that `ResolveEffectiveRunProperties` consequently resolves the run's font
size from the master's `<p:titleStyle>` (90pt in the test's fixture), not `<p:bodyStyle>` (28pt) -
the fix for a confirmed, real-world regression (a Section Header title rendering at roughly 1/3
PowerPoint's own ground-truth size). The fix was verified to be both necessary and sufficient by
temporarily reverting it and confirming the exact same test failed, resolving to `bodyStyle`'s
28pt instead of `titleStyle`'s 90pt, before re-applying it. The second, end-to-end test proves the
same omitted-type placeholder paints pixel-for-pixel identical glyph ink to an otherwise-identical
placeholder that declares `type="title"` explicitly, through the public `Render` API rather than
only the internal resolver.

#### CanvasNetPptx-PptxDocument-PlaceholderInheritancePerCategory: Categories Independent, Theme Is Unchanged Context

**Tests**:
`PptxDocumentInheritance_ResolvePlaceholderProperties_PerCategoryIndependentFallthrough_SlideOverridesFillButNotText`,
`PptxDocumentInheritance_ResolvePlaceholderProperties_NoMatchAtAnyLevel_ReturnsNullForBothCategories`

Proves `<p:spPr>` and `<p:txBody>/<a:lstStyle>` resolve independently - a slide-level `<p:spPr>`
with no slide-level `<a:lstStyle>` resolves the slide's own fill while still falling through to
the layout for text style. Separately, proves that when no placeholder matches at any level, both
effective property categories resolve to `null` while the resolved theme is still returned
unchanged as context. For `<p:spPr>` (fill/line resolution), an empty element still counts as
present, stopping the fallback for that category; `<a:xfrm>`/geometry
(`PlaceholderXfrmGeometryInheritance`, below) and `<a:lstStyle>` level overrides
(`TxBodyListStyleLevelInheritance`, below) are the two named exceptions, each verified separately
via its own independent, non-whole-element walk.

#### CanvasNetPptx-PptxDocument-PlaceholderXfrmGeometryInheritance: `<a:xfrm>`/Geometry Inherit Past an Empty `<p:spPr/>`

**Tests**: `Render_PlaceholderShapeWithEmptySpPr_InheritsXfrmAndGeometryFromLayout`,
`PptxDocument_Render_SamplelibSamplePresentationFixture_Slide4ThrowsUnsupportedFeatureOthersPaintContent`

Proves a placeholder shape whose own `<p:spPr/>` is present but empty (declaring neither
`<a:xfrm>` nor `<a:prstGeom>`/`<a:custGeom>`, deliberately relying on its matched layout
placeholder for both) still renders and paints visible content, rather than being silently
skipped (for a missing `<a:xfrm>`) or crashing with `InvalidDataException` (for missing geometry) -
`EffectiveXfrmElement`/`EffectiveGeometrySpPr` each independently walk slide -> matched layout ->
matched master looking specifically for an `<a:xfrm>` child, or a `<p:spPr>` that itself declares
a geometry child, continuing past a tier's own empty-but-present `<p:spPr>` that declares
neither - unlike `PlaceholderInheritancePerCategory`'s own whole-element fallback, which stops at
the first present `<p:spPr>` regardless of its own contents. The second test additionally proves
this against a real-world fixture (`samplelib-sample-presentation.pptx` slides 0-2, each a
placeholder with exactly this empty-`<p:spPr/>` shape), confirming the synthetic regression test's
finding generalizes to genuine third-party-generated files, not merely a hand-authored edge case.

#### CanvasNetPptx-PptxDocument-TxBodyListStyleLevelInheritance: `<a:lstStyle>` Level Overrides Inherit Past an Empty `<a:lstStyle/>`

**Tests**:
`PptxDocumentInheritance_ResolvePlaceholderProperties_SlideLstStyleEmptyWithNoLevelOverride_FallsThroughToLayoutLevelOverride`,
`PptxDocumentInheritance_ResolvePlaceholderProperties_SlideLstStyleHasLevelOverride_SlideWinsOverLayout`,
`ResolveEffectiveRunProperties_CtrTitlePlaceholderWithEmptySlideLstStyle_ResolvesLayoutDefRPrSizeNotMasterTitleStyleFallback`,
`Render_CtrTitlePlaceholderWithEmptySlideLstStyle_PaintsIdenticallyToAbsentLstStyle`

The first resolver-level test proves a slide's own empty, self-closing `<a:lstStyle/>` (no
`<a:lvl1pPr>`..`<a:lvl9pPr>` level-override child at all - the exact real-world shape reported
from "ERF IWF Breadboard Peer Review.pptx") does not "win" `PlaceholderInheritancePerCategory`'s
own whole-element fallback: the idx-matched layout placeholder's own, level-bearing
`<a:lstStyle>` is selected instead. The second resolver-level test is a regression guard proving
the pre-existing, correct behavior is preserved - when the slide-level `<a:lstStyle>` DOES declare
a real level override, it still wins over the layout's. The third test proves the resolved
`EffectiveTxBodyListStyle` is actually consumed correctly downstream: a `ctrTitle` placeholder
with this exact empty-`<a:lstStyle/>` shape resolves its run's effective font size from the
layout's `<a:lvl1pPr>`/`<a:defRPr sz="6000">` (60pt), not the master's `<p:titleStyle>` fallback
(28pt) - the exact pre-fix symptom magnitude described in the bug report. The fourth, end-to-end
test proves (through the public `Render` API, not just the internal resolver) that the same
placeholder with an empty-but-present `<a:lstStyle/>` paints pixel-for-pixel identical glyph ink
to the same placeholder with `<a:lstStyle>` omitted entirely, confirming the layout's 60pt
override is actually painted, not merely resolved, in both cases.

#### CanvasNetPptx-PptxDocument-ShapeFrameTransform: Position/Rotation/Flip Compose Into the Correct Local-to-Parent Transform

**Tests**: `ResolveShapeFrame_PlainOffsetAndSize_MapsLocalOriginToOffset`,
`ResolveShapeFrame_MissingOff_ThrowsInvalidDataException`,
`ResolveShapeFrame_MissingExt_ThrowsInvalidDataException`,
`ResolveShapeFrame_Rotation90Degrees_RotatesAboutOwnCenterClockwise`,
`ResolveShapeFrame_MissingRot_DefaultsToZeroRotation`,
`ResolveShapeFrame_FlipHorizontal_MirrorsAboutOwnCenterX`,
`ResolveShapeFrame_FlipVertical_MirrorsAboutOwnCenterY`,
`ResolveShapeFrame_InvalidRotAttribute_ThrowsInvalidDataException`,
`ResolveShapeFrame_NonNumericOffAttribute_ThrowsInvalidDataException`,
`ResolveShapeFrame_NonNumericExtAttribute_ThrowsInvalidDataException`,
`CanvasNetPptx_SystemIntegration_GeometryAndPaint_FreeformShapeResolvesEndToEnd`

Proves a plain `<a:off>`/`<a:ext>` (no rotation/flip) maps a shape's local origin to the declared
offset; proves a `90`-degree `rot` rotates a known local point about the shape's own center to
the expected clockwise-on-screen location; proves an absent `rot` defaults to no rotation; proves
`flipH`/`flipV` each mirror a known local point about the shape's own center on the expected
axis; and proves a missing `<a:off>`, missing `<a:ext>`, non-numeric `rot` attribute, or
non-numeric `<a:off>`/`<a:ext>` `x`/`y`/`cx`/`cy` attribute each throw `InvalidDataException`
rather than an unrelated raw `FormatException`.

#### CanvasNetPptx-PptxDocument-GroupChildTransform: Group chOff/chExt Child Coordinate Space Composes Correctly

**Tests**: `ResolveGroupChildTransform_IdentityChildSpace_MatchesGroupFrameDirectly`,
`ResolveGroupChildTransform_ScaledChildSpace_ScalesChildCoordinatesIntoGroupBox`,
`ResolveGroupChildTransform_OffsetChildSpace_TranslatesChildOriginBeforeScaling`,
`ResolveGroupChildTransform_ChildOfChild_ComposesThroughBothTransforms`,
`ResolveGroupChildTransform_MissingChOffChExt_DefaultsToIdentityChildSpace`,
`ResolveGroupChildTransform_ChOffPresentWithMissingAttribute_ThrowsInvalidDataException`,
`ResolveGroupChildTransform_ChExtPresentWithMissingAttribute_ThrowsInvalidDataException`,
`CanvasNetPptx_SystemIntegration_GeometryAndPaint_GroupShapeChildTransformComposesEndToEnd`

Proves a group whose `chOff`/`chExt` exactly matches its own `off`/`ext` produces the same
transform as the group's own resolved frame; proves a `chExt` smaller than the group's own `ext`
scales a child coordinate up proportionally into the group's box; proves a non-zero `chOff`
translates a child coordinate's origin before that scaling is applied; proves a group nested
inside another group composes both levels' child transforms together with a descendant shape's
own frame correctly; proves an absent `chOff`/`chExt` defaults to an identity child coordinate
space; proves a `chOff`/`chExt` that IS present but omits one of its required attributes throws
`InvalidDataException` rather than silently substituting a default (a present-but-incomplete
element is malformed input, not the "absent element" case the default is meant to cover); and the
end-to-end system test proves this composition holds through the real package-load path (a shape
nested in a group with non-trivial `chOff`/`chExt` resolves to the expected slide-space point).

#### CanvasNetPptx-PptxDocument-PresetGeometry: Supported Presets Build Correctly, Unsupported Presets Fail Closed

**Tests**: `ResolveShapeGeometry_PrstGeomRect_ReturnsRectanglePath`,
`ResolveShapeGeometry_NeitherPrstGeomNorCustGeom_ThrowsInvalidDataException`,
`ResolveShapeGeometry_UnsupportedPreset_ThrowsPptxUnsupportedFeatureException`,
`ResolveShapeGeometry_PrstGeomMissingPrstAttribute_ThrowsInvalidDataException`,
`PptxPresetGeometry_Build_EachSupportedPreset_ProducesPathWithinDeclaredBounds` (a `[Theory]`
covering all 28 supported preset names), `PptxPresetGeometry_Build_UpArrow_PointsUpward`,
`PptxPresetGeometry_Build_DownArrow_PointsDownward`,
`PptxPresetGeometry_Build_UnsupportedPreset_ThrowsWithFeatureToken`,
`PptxPresetGeometry_Build_NonPositiveSize_ReturnsEmptyPath`,
`PptxPresetGeometry_Build_CurvedUpArrow_PointsUpwardWithoutThrowing`,
`PptxPresetGeometry_Build_CurvedArrowSiblings_EachPointsTowardItsOwnDeclaredEdge`,
`Render_CurvedUpArrowShape_RendersWithoutThrowingAndPaintsInk`

Proves `ResolveShapeGeometry` dispatches `<a:prstGeom prst="rect">` to a rectangle path sized to
the shape's declared extent; proves a `<p:spPr>` with neither `<a:prstGeom>` nor `<a:custGeom>`,
and a `<a:prstGeom>` missing its required `prst` attribute, each throw `InvalidDataException`; and
proves an unrecognized preset name throws `PptxUnsupportedFeatureException`. Proves every one of
the 28 supported preset names (via a `[Theory]` enumerating all of them) builds a path whose
points all lie within `(0,0)`-`(w,h)` bounds; proves `upArrow`/`downArrow` each orient their arrow
point toward the expected direction (a directional sanity check distinguishing a correctly
oriented preset from a trivially-passing bounds check alone); proves an unsupported preset throws
`PptxUnsupportedFeatureException` carrying the expected feature token; and proves a non-positive
width or height resolves to `Path.Empty` rather than throwing or producing a degenerate path.
`PptxPresetGeometry_Build_CurvedUpArrow_PointsUpwardWithoutThrowing` proves the exact reported
crash scenario (`Build("curvedUpArrow", ...)`) no longer throws and that the resulting path's
apex lies near the shape's top edge; `PptxPresetGeometry_Build_CurvedArrowSiblings_EachPointsTowardItsOwnDeclaredEdge`
proves each of `curvedDownArrow`/`curvedLeftArrow`/`curvedRightArrow` similarly points toward its
own named edge; `Render_CurvedUpArrowShape_RendersWithoutThrowingAndPaintsInk` proves the full
`curvedUpArrow` scenario renders end-to-end through the public `Render` API and paints visible
ink, not merely that the geometry builder itself does not throw.

#### CanvasNetPptx-PptxDocument-CustomGeometry: pathLst Commands Parse Into the Expected Path, Coordinate Space Scales Correctly

**Tests**: `ResolveCustomGeometry_MoveLineClose_ProducesClosedTriangle`,
`ResolveCustomGeometry_CubicBezTo_ProducesCubicBezierCommand`,
`ResolveCustomGeometry_QuadBezTo_ProducesQuadraticBezierCommand`,
`ResolveCustomGeometry_PathCoordinateSpaceScaledToShapeExtent`,
`ResolveCustomGeometry_NoPathLst_ReturnsEmptyPath`

Proves a `<a:path>` of `moveTo`/`lnTo`/`lnTo`/`close` commands produces a closed triangular
subpath with the expected start point and command count; proves `cubicBezTo` produces a path
command whose type and control/end points match the two control points plus endpoint declared in
XML; proves `quadBezTo` produces a path command whose single control point and endpoint match;
proves a `<a:path w= h=>` declaring a coordinate space different from the shape's own
`widthEmu`/`heightEmu` scales every point correctly (a point at the path's own declared far corner
resolves to exactly `(widthEmu, heightEmu)`, not the path's own raw `w`/`h` value); and proves a
`<a:custGeom>` with no `<a:pathLst>` resolves to `Path.Empty` rather than throwing.

#### CanvasNetPptx-PptxDocument-FillResolution: noFill/solidFill/gradFill/pattFill/blipFill Resolve or Fail Closed

**Tests**: `ResolveFill_NullParent_ReturnsNoFill`, `ResolveFill_ExplicitNoFill_ReturnsNoFill`,
`ResolveFill_NoRecognizedFillChild_ReturnsNoFill`,
`ResolveFill_SolidFillSrgbClr_ReturnsResolvedSolidFill`,
`ResolveFill_SolidFillSchemeClr_ResolvesThroughTheme`,
`ResolveFill_GradFillLinear_ReturnsResolvedGradientFill`,
`ResolveFill_PatternFillUnsupportedPreset_ThrowsPptxUnsupportedFeatureException`,
`ResolveFill_PictureFill_ThrowsPptxUnsupportedFeatureException`,
`ResolveFill_BarePatternFillWithNoPrstAttribute_ReturnsNoFill`,
`CanvasNetPptx_SystemIntegration_GeometryAndPaint_FreeformShapeResolvesEndToEnd`

Proves a `null` fill parent, an explicit `<a:noFill/>`, and a `<p:spPr>` with no recognized fill
child, all three resolve to `PptxNoFill.Instance`; proves `<a:solidFill><a:srgbClr .../>` and
`<a:solidFill><a:schemeClr .../>` both resolve to the expected `PptxSolidFill` (the latter
resolving its color through the supplied `PptxTheme`, not merely parsing the slot name); proves
`<a:gradFill>` with a linear direction resolves to a `PptxGradientFill` wrapping the expected
gradient stops; proves `<a:pattFill prst="...">` naming an uncovered preset throws
`PptxUnsupportedFeatureException` rather than silently resolving to an incorrect fill (a covered
preset's own resolution is proven by `CanvasNetPptx-PptxDocument-PatternFillResolution` below);
proves a non-conformant, attribute-less `<a:pattFill/>` with no `prst` at all resolves to
`PptxNoFill` instead of either resolving a preset or throwing; and proves `<a:blipFill>` still
throws `PptxUnsupportedFeatureException` (feature token `"pptx-picture-fill"`) when no blip-image
resolver is supplied, its pre-existing fallback behavior, now narrowed to document that specific
"no resolver supplied" case rather than picture fill being unsupported outright - see
`CanvasNetPptx-PptxDocument-PictureFillResolution` below for the resolver-supplied, fully
supported case. The end-to-end system test additionally proves a real shape's fill resolves
correctly when reached through the full slide/layout/master/theme package-load chain, not merely
from a hand-built `XElement` fragment.

#### CanvasNetPptx-PptxDocument-PictureFillResolution: blipFill Resolves to a Tiled/Stretched PptxImageFill

**Tests**: `ResolveFill_BlipFillWithResolver_ReturnsPptxImageFill`,
`ResolveImageFillTransform_TileWithNonTrivialOffsetAndScale_AppliesBothToEachAxis`,
`ResolveImageFillTransform_StretchWithFillRectInsets_CropsBeforeStretching`,
`ResolveImageFillTransform_DegenerateFillRect_FallsBackToFullStretch`,
`PptxDocument_Render_DmlFillFixture_Slide0RendersPictureAndPatternFillShapesWithoutThrowing`,
`CanvasNetPptx_SystemIntegration_Images_TileBlipFillShapeDecodesAndRendersEndToEnd`

Proves a minimal `<a:blipFill><a:blip r:embed="..."/><a:stretch/></a:blipFill>`, given a fake
`resolveBlipImage` delegate, resolves to a `PptxImageFill` wrapping that delegate's own returned
`Surface` (rather than throwing, the regression this feature fixes); pins down
`ResolveImageFillTransform`'s own `<a:tile>` derivation with a non-trivial, non-default `tx`/
`ty`/`sx`/`sy` combination (independent of the real fixture's own trivial `100%`/`0`-offset
values); pins down `<a:stretch>`'s own `<a:fillRect>` inset-cropping math, and its degenerate-crop
(edges summing to `100%` or more) fallback-to-full-stretch behavior, guarding against a
non-finite or divide-by-zero transform. The real-fixture test proves `pythonpptx-dml-fill.pptx`'s
slide 0 - which places a real, embedded `<a:blipFill>`/`<a:tile>` picture-filled shape alongside a
`<a:pattFill prst="divot"/>` pattern-filled shape, now also covered (see
`CanvasNetPptx-PptxDocument-PatternFillResolution` below) - renders completely end-to-end, with
neither shape throwing; the system-integration test proves a genuine, full end-to-end render of a
minimal, purpose-built single-shape package (an ordinary `<p:sp>`, not a `<p:pic>`, whose own
`<a:blipFill>`/`<a:tile>` shape fill is the slide's only content) completes without exception,
with a pixel-level sanity check that the filled shape's own region painted the embedded image's
content rather than being left as untouched background.

#### CanvasNetPptx-PptxDocument-PatternFillResolution: pattFill Resolves a Covered Preset to a Synthesized Tile

**Tests**: `ResolveFill_PatternFillCoveredPreset_ReturnsPptxPatternFill`,
`ResolveFill_PatternFillMissingFgBgClr_DefaultsToBlackOnWhite`,
`ResolveFill_PatternFillUnsupportedPreset_ThrowsPptxUnsupportedFeatureException`,
`ResolveFill_BarePatternFillWithNoPrstAttribute_ReturnsNoFill`,
`ResolveLineStyle_PatternFillCoveredPreset_ReturnsLineStyleWithPptxPatternFill`,
`RenderTile_Horz_AlternatesFullRows`, `RenderTile_Vert_AlternatesFullColumns`,
`RenderTile_LtHorz_SparseThinRows`, `RenderTile_DkHorz_DominantRows`,
`RenderTile_LtVert_SparseThinColumns`, `RenderTile_DkVert_DominantColumns`,
`RenderTile_DnDiag_FollowsXPlusYModuloRule`, `RenderTile_UpDiag_FollowsXMinusYModuloRule`,
`RenderTile_WdDnDiag_ProducesWideBands`, `RenderTile_Cross_FgAtHorizontalOrVerticalLines`,
`RenderTile_DiagCross_FgAtEitherDiagonal`, `RenderTile_PercentageFamily_BothColorsPresent` (a
`[Theory]` covering the `pct5`...`pct90` family), `RenderTile_PercentageFamily_FgPixelCountIncreasesWithPercent`,
`RenderTile_Divot_BothColorsPresent`, `RenderTile_Wave_BothColorsPresent`,
`RenderTile_EveryCoveredPreset_ProducesTileSizeSquareSurface` (a `[Theory]` covering all 30
covered presets), `PptxDocument_Render_DmlFillFixture_Slide0RendersPictureAndPatternFillShapesWithoutThrowing`,
`PptxDocument_Render_DmlFillFixture_Slide1PatternFillShapesRenderAndBareFillIsTransparent`,
`Render_ShapeWithPatternFillStroke_PaintsCorrectlyTransformedStripesNotFlatColor`,
`Render_ConnectorWithPatternFillStroke_PaintsCorrectlyTransformedStripesNotFlatColor`,
`Render_SlideLevelPatternFillBackground_PaintsCorrectlyTransformedStripesNotFlatColor`

Proves `<a:pattFill prst="horz">` (a representative covered preset), with explicit `<a:fgClr>`/
`<a:bgClr>` children, resolves to a `PptxPatternFill` wrapping the expected preset/foreground/
background; proves an absent `<a:fgClr>`/`<a:bgClr>` defaults to black-on-white; proves an
uncovered preset name (for example `zigZag`) still throws `PptxUnsupportedFeatureException`
(feature token `"pptx-pattern-fill"`); proves a non-conformant, attribute-less `<a:pattFill/>`
resolves to `PptxNoFill` rather than a named preset; proves `ResolveLineStyle` resolves an
`<a:ln><a:pattFill>...</a:pattFill></a:ln>` (a line/stroke fill) identically to a shape fill,
since both share the same `ResolveFill` resolver. Each `RenderTile_*` test directly exercises
`PptxPatternTileRenderer.RenderTile` (bypassing `ResolveFill` entirely) and asserts its
synthesized tile's own per-pixel rule: horizontal/vertical-stripe presets alternate whole
rows/columns at the documented density; diagonal presets follow an `(x +/- y) mod period` rule;
the percentage family's foreground pixel count strictly increases as the named percentage
increases (proving genuine density scaling, not a fixed ratio); `divot`/`wave` each paint both
colors; and every covered preset produces a tile exactly `PptxPatternTileRenderer`'s own declared
tile size, square. The two real-fixture tests prove `pythonpptx-dml-fill.pptx` renders completely,
end-to-end, without throwing on either slide: slide 0's `prst="divot"` shape (alongside the
picture-fill shape covered above) and slide 1's three pattern-fill shapes (a non-conformant,
attribute-less `<a:pattFill/>` - proven to remain an unfilled, un-overridden white rather than
falling back to its own `<p:style>/<a:fillRef>` - plus a `prst="divot"` and a `prst="wave"` shape,
each proven to paint a genuine mix of its own resolved foreground and background colors by
sampling pixels inside its own bounds, not a flat single color). Proving real pixel variation
(rather than merely "did not throw") required a companion fix to a real, pre-existing
transform-composition gap - see `PptxDocument.Tables.cs`'s own `FillPaintComposingLocalTransform`
XmlDoc remarks, and the design doc's own *Pattern Fill (`<a:pattFill>`) (Phase 2 Follow-Up)*
section, for the full detail: without it, every pattern-filled shape/connector fill, stroke
outline, or arrowhead not positioned at the surface's own origin would have rendered as a single
flat color instead of a repeating tile. The two `Render_*PatternFillStroke_*` tests prove this
same fix for `<a:ln>`-based line/stroke pattern fills specifically: an off-origin shape's and
connector's own pattern-filled stroke each paint a genuine alternating foreground/background
stripe band, not a single flat color - each test is proven to fail against the pre-fix code
(only the foreground color would ever appear in the sampled band).
`Render_SlideLevelPatternFillBackground_PaintsCorrectlyTransformedStripesNotFlatColor` proves the
identical fix for the slide/layout/master background-fill call site itself: a pattern-filled
`<p:bg><p:bgPr><a:pattFill>` slide background now composes the slide's own EMU-to-pixel
`baseTransform` into the synthesized tile's transform via `FillPaintComposingLocalTransform`,
so the background genuinely tiles (both resolved colors appear across the sampled region) rather
than collapsing to a single flat color - this test is likewise proven to fail against the pre-fix
code, where the background-fill call site still passed the identity transform.

#### CanvasNetPptx-PptxDocument-ColorResolution: srgbClr/sysClr/schemeClr/prstClr Resolve to the Expected Rgba32 Value

**Tests**: `ResolveColor_SrgbClr_ParsesHexDirectly`, `ResolveColor_SysClr_UsesLastClrAttribute`,
`ResolveColor_SchemeClrOrdinarySlots_ResolveToMatchingThemeSlot` (a `[Theory]` covering all 12
ordinary scheme slot names), `ResolveColor_SchemeClrBackgroundTextAliases_MapToExpectedSlot` (a
`[Theory]` covering `bg1`/`tx1`/`bg2`/`tx2`), `ResolveColor_SchemeClrUnrecognizedSlot_ThrowsInvalidDataException`,
`ResolveColor_UnsupportedColorKind_ThrowsPptxUnsupportedFeatureException`,
`ResolveColor_SrgbClrEightDigitValue_ThrowsInvalidDataException`,
`ResolveColor_SysClrEightDigitLastClrValue_ThrowsInvalidDataException`,
`ResolveColor_PrstClrWhite_ReturnsWhite`, `ResolveColor_PrstClrBlack_ReturnsBlack`,
`ResolveColor_PrstClrUnsupportedName_ThrowsPptxUnsupportedFeatureException`,
`ResolveColor_PrstClrMissingValAttribute_ThrowsInvalidDataException`

Proves `<a:srgbClr val="RRGGBB"/>` parses its hex value directly; proves `<a:sysClr .../>`
resolves via its `lastClr` attribute; proves, for every one of the 12 ordinary
`<a:schemeClr val="..."/>` slot names, the resolved color matches that exact slot on a
distinctly-colored `PptxColorScheme` (not merely "some" color); proves each of the four
background/text aliases (`bg1`/`tx1`/`bg2`/`tx2`) resolves to its documented aliased slot
(`lt1`/`dk1`/`lt2`/`dk2` respectively); proves an unrecognized `<a:schemeClr val="...">` slot name
throws `InvalidDataException`; proves an unsupported color-definition element kind (for
example `<a:hslClr>`) throws `PptxUnsupportedFeatureException`; and proves an 8-digit
`#AARRGGBB`-style value on either `<a:srgbClr val="...">` or `<a:sysClr lastClr="...">` throws
`InvalidDataException` rather than being passed to the underlying `Rgba32` hex parser, which would
otherwise accept the 8-digit form and silently treat its leading byte as alpha, producing a wrong,
unintended-alpha color instead of failing closed. Proves `<a:prstClr val="white"/>` and
`<a:prstClr val="black"/>` - the two ECMA-376 `ST_PresetColorVal` names this package resolves, a
narrowly-scoped companion fix needed by pattern fill's own `bgClr` (see
`CanvasNetPptx-PptxDocument-PatternFillResolution` above) - resolve to their standard RGB values;
proves any other preset-color name (for example `red`) still throws
`PptxUnsupportedFeatureException` (feature token `"pptx-color-kind"`), narrowed rather than
removed; and proves a `<a:prstClr>` missing its own required `val` attribute throws
`InvalidDataException`.

#### CanvasNetPptx-PptxDocument-ColorTransforms: lumMod/lumOff/shade/tint/alpha Apply in the Documented Fixed Order

**Tests**: `ResolveColor_Alpha_SetsAlphaChannel`, `ResolveColor_Shade_DarkensProportionally`,
`ResolveColor_Tint_LightensProportionally`, `ResolveColor_LumMod_DecreasesLuminance`,
`ResolveColor_LumOff_IncreasesLuminance`,
`ResolveColor_CombinedLumModLumOffShadeTintAlpha_AppliesFixedPipelineOrder`

Proves each of `alpha`/`shade`/`tint`/`lumMod`/`lumOff` individually transforms a known base
color to the exact expected result (per this phase's documented truncating-cast arithmetic, not
rounding - for example a `50%` `alpha` resolves to alpha `127`, not `128`); and proves all five
applied together on the same color-definition element produce the result of the documented fixed
pipeline order (`lumMod`/`lumOff`, then `shade`, then `tint`, then `alpha`) rather than the
source-XML element order, which the test deliberately orders differently from the pipeline order
to prove the implementation does not depend on it.

#### CanvasNetPptx-PptxDocument-GradientFill: Linear Gradient Direction/Stops Resolve, Path/Missing Stops Fail Closed

**Tests**: `ResolveGradientFill_LinearZeroDegrees_PointsAlongPositiveX`,
`ResolveGradientFill_MissingLin_ThrowsPptxUnsupportedFeatureException`,
`ResolveGradientFill_MissingGsLst_ThrowsInvalidDataException`,
`ResolveGradientFill_NonNumericGsPos_ThrowsInvalidDataException`

Proves a `<a:lin ang="0"/>` (zero-degree angle) resolves a `LinearGradient` whose start/end points
lie along the positive X axis, centered on the shape's own bounding box, with its `<a:gsLst>`
stops resolved to the expected offsets and colors; proves a `<a:gradFill>` with no `<a:lin>` child
(a path/radial gradient) throws `PptxUnsupportedFeatureException` rather than silently
approximating a linear direction; proves a `<a:gradFill>` missing its required `<a:gsLst>`
throws `InvalidDataException`; and proves a `<a:gs>` element with a non-numeric `pos` attribute
throws `InvalidDataException` rather than an unrelated raw `FormatException`.

#### CanvasNetPptx-PptxDocument-LineStyleResolution: Stroke Width/Fill/Dash Resolve, Outline Realizes via Core PathStroker

**Tests**: `ResolveLineStyle_NullElement_ReturnsNull`, `ResolveLineStyle_ZeroWidth_ReturnsNull`,
`ResolveLineStyle_NoFillLine_ReturnsNull`, `ResolveLineStyle_WidthAndSolidFill_ResolvesWidthAndColor`,
`ResolveLineStyle_DashPresets_ProduceNonNullDashArray` (a `[Theory]` covering all seven supported
dash preset names), `ResolveLineStyle_SolidPreset_ProducesNullDashArray`,
`ResolveLineStyle_ExplicitZeroWidth_ReturnsNull`, `ResolveLineStyle_ExplicitNegativeWidth_ReturnsNull`,
`ResolveLineStyle_MissingWidthAttribute_ResolvesDefaultWidth`,
`Render_EllipseWithNoFillAndLnMissingWidthButRecognizedColor_PaintsDefaultWidthStroke`,
`ResolveStrokeOutline_RectangleWithSolidLine_ProducesNonEmptyOutline`,
`Render_FullSlideScaleEllipseWithNoFillAndLnMissingWidth_RendersThinRingNotSolidDisc`,
`Render_FullSlideScaleRoundRectWithNoFillAndLnMissingWidth_RendersThinOutlineNotSolidFill`

Proves a `null` `<a:ln>`, an explicitly-declared zero/non-positive width, and an explicit or
implicit no-fill line, each resolve to `null` (no stroke drawn); proves a positive width with a
`<a:solidFill>` resolves a `PptxLineStyle` carrying the expected width and color; proves each of
the seven supported `<a:prstDash val="...">` preset names resolves a non-null, width-proportional
dash array; proves the common `solid` preset (and, by extension, any unrecognized name) resolves
`null` (a solid line); and proves `ResolveStrokeOutline` produces a non-empty outline path for a
rectangle with a solid line, via the core `PathStroker`/`StrokeStyle` machinery reused from the
PDF renderer's own call pattern.

**Default-width regression scenarios** (fixing the bug where a genuinely-absent `w` attribute
collapsed to the same "no stroke" outcome as an explicit `w="0"`):

- `ResolveLineStyle_ExplicitZeroWidth_ReturnsNull` and
  `ResolveLineStyle_ExplicitNegativeWidth_ReturnsNull` prove an explicitly-present `w="0"` or
  negative `w` (e.g. `w="-100"`) still resolve to `null` (no stroke) - a regression guard proving
  OOXML's own "explicit zero/negative width means no stroke" idiom is unaffected by this fix.
- `ResolveLineStyle_MissingWidthAttribute_ResolvesDefaultWidth` proves an `<a:ln>` with a
  recognized `<a:solidFill>` but no `w` attribute at all resolves a non-`null` `PptxLineStyle`
  whose `WidthEmu` is the documented default (`9525` EMU / `0.75`pt) and whose color matches the
  declared fill.
- `Render_EllipseWithNoFillAndLnMissingWidthButRecognizedColor_PaintsDefaultWidthStroke` is a
  render-level regression test reproducing the exact reported real-world pattern (an ellipse shape
  with `<a:noFill/>` and an `<a:ln><a:solidFill>...</a:solidFill></a:ln>` declaring no `w`
  attribute): it renders the shape against a small 1"x1" slide (so the sub-pixel-at-normal-scale
  9525 EMU default width survives as several solid, easily-sampled pixels) and asserts a red stroke
  pixel appears directly on the ellipse's own boundary, while both its interior and the area well
  outside its bounding box remain the default opaque-white clear - proving the stroke is now
  visible, proving `<a:noFill/>` still means no interior fill, and proving the stroke does not
  spill unexpectedly far beyond its own narrow band.

**Full-slide-scale "solid disc" regression scenarios** (fixing a second, deeper bug: the same
small-slide test above does not catch a regression that only manifests at a realistic, full-size
slide's own native EMU coordinate magnitude, where a curved shape's stroke outline falsely
collapsed to a solid fill - see `../../design/canvas-net/drawing/path-stroker.md`'s "Inner-ring
collapse" section and `../../design/canvas-net-pptx/pptx-document.md`'s "Realizing a stroke
outline" section for the full root-cause/fix explanation):

- `Render_FullSlideScaleEllipseWithNoFillAndLnMissingWidth_RendersThinRingNotSolidDisc` renders an
  `ellipse`, `<a:noFill/>`, color-only (default-width) `<a:ln>` shape against a standard full-size
  slide (`9144000x6858000` EMU rendered at `960x720` - exactly `9525` EMU/pixel, matching
  PowerPoint's own default stroke width) and asserts the ellipse's own exact center pixel remains
  the background color (proving the interior is **not** filled solid), a pixel on the ellipse's
  own top boundary is red-dominant (proving the ring itself still paints), and a pixel outside the
  ellipse's bounding box remains the background color. This test was confirmed, by direct
  before/after comparison against the unfixed code, to fail (center rendered the stroke color) in
  the pre-fix state and pass post-fix.
- `Render_FullSlideScaleRoundRectWithNoFillAndLnMissingWidth_RendersThinOutlineNotSolidFill` is the
  equivalent test for a `roundRect` preset geometry at the same full-slide scale, proving the fix
  is general to curved/Bezier-flattened closed geometry and not an ellipse-only special case (a
  plain `rect`, with no curved segments, was confirmed unaffected and does not need an equivalent
  test).

### Text Layout and Rendering (Phase 1d) Test Scenarios

#### CanvasNetPptx-PptxDocument-TextBodyParsing: Text Body/Paragraph/Run Structure Parses Correctly

**Tests**: `ParseBodyProperties_Absent_ResolvesSchemaDefaults`,
`ParseBodyProperties_AnchorCtr_ResolvesMiddle`, `ParseBodyProperties_AnchorB_ResolvesBottom`,
`ParseBodyProperties_AnchorT_ResolvesTop`, `ParseBodyProperties_WrapNone_ResolvesWrapNone`,
`ParseBodyProperties_CustomInsets_ResolvesDeclaredValues`,
`ParseBodyProperties_AutofitElement_IsRetainedUnparsed` (a `[Theory]` covering
`noAutofit`/`normAutofit`/`spAutoFit`), `ParseTextBody_NoParagraphs_ResolvesEmptyParagraphList`,
`ParseTextBody_SingleParagraphSingleRun_ParsesBodyAndParagraph`,
`ParseParagraph_NoChildren_ResolvesEmptyRunList`

Proves `ParseBodyProperties` resolves the OOXML schema's documented defaults when `<a:bodyPr>` or
an individual attribute is absent, resolves each of the three vertical anchors and the `none` wrap
mode explicitly, resolves custom inset values when declared, and retains the raw autofit child
element unparsed; proves `ParseTextBody` resolves an empty paragraph list for a text body with no
`<a:p>` children and resolves a single paragraph/run structure correctly; and proves an empty
`<a:p>` with no recognized child resolves an empty run list rather than throwing.

#### CanvasNetPptx-PptxDocument-ParagraphRunParsing: Paragraph/Run Raw Property Extraction and Level Clamping

**Tests**: `ParseParagraphProperties_Absent_ResolvesLevelZeroAndNullOptionals`,
`ParseParagraphProperties_LevelAttribute_ClampsToZeroToEight` (a `[Theory]` covering below-range,
in-range, and above-range `lvl` values), `ParseParagraphProperties_FullyPopulated_ResolvesEveryField`,
`ParseRun_NoText_ResolvesEmptyString`, `ParseRun_WithTextAndRPr_ResolvesBoth`,
`ParseParagraph_RunBreakRun_PreservesBreakInDocumentOrder`

Proves `ParseParagraphProperties` resolves level `0` and every optional field `null` when
`<a:pPr>` is absent, clamps an out-of-range `lvl` attribute into the documented `[0,8]` range,
resolves every field when fully populated; proves `ParseRun` resolves an empty string when
`<a:t>` is absent and resolves both the raw `<a:rPr>` element and text when both are present; and
proves `ParseParagraph`, given a `<a:p>` containing a run, then a `<a:br/>`, then a second run,
preserves all three as an ordered item list in document order (run, break, run) rather than
selecting only the `<a:r>` children and silently discarding the `<a:br/>`.

#### CanvasNetPptx-PptxDocument-TextPropertyInheritance: Attribute-Level Run/Paragraph Property Resolution

**Tests**: `ResolveEffectiveRunProperties_RunOverride_WinsOverEveryOtherTier`,
`ResolveEffectiveRunProperties_ParagraphDefRPrOnly_WinsWhenRunDeclaresNothing`,
`ResolveEffectiveRunProperties_PlaceholderLevelStyleOnly_WinsWhenRunAndParagraphDeclareNothing`,
`ResolveEffectiveRunProperties_NoneDeclared_ResolvesHardCodedDefaults`,
`ResolveEffectiveRunProperties_UnderlineAttribute_ResolvesExpectedBoolean` (a `[Theory]` covering
`"1"`/`"true"`/`"0"`/`"false"` underline values), `ResolveEffectiveRunProperties_RunSolidFillSrgbClr_ResolvesExplicitColor`,
`ResolveEffectiveParagraphProperties_NoneDeclared_ResolvesHardCodedDefaults`,
`ResolveEffectiveParagraphProperties_Alignment_NormalizesJustificationToLeft` (a `[Theory]`
covering `just`/`justLow`), `ResolveEffectiveParagraphProperties_FixedLineSpacing_ResolvesPointsToEmu`,
`ResolveEffectiveParagraphProperties_PlaceholderLevelStyleOnly_WinsWhenParagraphDeclaresNothing`,
`ResolveEffectiveRunProperties_ThemeFontToken_ResolvesThroughFontScheme` (a `[Theory]` covering
all six reserved theme-font tokens: `+mj-lt`/`+mn-lt`, `+mj-ea`/`+mn-ea`, `+mj-cs`/`+mn-cs`),
`ResolveEffectiveRunProperties_LiteralTypeface_PassesThroughUnchanged`

Proves each inheritance tier (run override, paragraph `defRPr`, placeholder level-indexed
`lstStyle`, hard-coded default) wins in isolation when every higher-priority tier declares
nothing, for both run properties (typeface/size/bold/italic/underline/color) and paragraph
properties (alignment/margin/indent/line-spacing); proves `algn="just"`/`"justLow"` normalizes to
`"l"`; proves fixed-point line spacing (`<a:spcPts>`) resolves to the expected EMU value; proves
each of the six reserved DrawingML theme-font tokens in `<a:latin typeface="..."/>` resolves
through the supplied `PptxTheme`'s `FontScheme` (major/minor Latin/EastAsian/ComplexScript font)
to that scheme's own declared family name rather than the literal token string; and proves an
ordinary, non-token literal typeface name passes through unresolved/unchanged.

#### CanvasNetPptx-PptxDocument-TextStylesInheritance: Master `<p:txStyles>` Parsing and Bucket Selection

**Tests**: `GetMaster_TxStylesSiblingOfCSld_ParsesAllThreeStyles`,
`ResolveEffectiveRunProperties_MasterTxStylesOnly_WinsWhenEverythingAboveIsAbsent`,
`ResolveEffectiveRunProperties_MasterTitleStyle_SelectedForTitlePlaceholderType`,
`ResolveEffectiveRunProperties_MasterOtherStyle_SelectedForNonPlaceholderShape`,
`ResolveEffectiveParagraphProperties_MasterTxStylesOnly_WinsWhenEverythingAboveIsAbsent`,
`CanvasNetPptx_SystemIntegration_TextLayoutAndRender_TitlePlaceholderResolvesMasterTitleStyleEndToEnd`,
`CanvasNetPptx_SystemIntegration_TextLayoutAndRender_NonPlaceholderShapeResolvesMasterOtherStyleEndToEnd`

Proves `GetMaster` parses a slide master's own `<p:txStyles>` element (a direct sibling of
`<p:cSld>`) into its three schema-optional style buckets; proves the master's own `txStyles`
bucket wins when every higher-priority tier is absent; proves `title`/`ctrTitle` placeholder types
select `TitleStyle` and a non-placeholder shape (the empty-string sentinel) selects `OtherStyle`;
and the two system-integration tests prove, end-to-end through a full in-memory package opened
via `PptxDocument.Open`, that a `title` placeholder resolves its effective run size from the
master's own `<p:titleStyle>` and that an ordinary (non-placeholder) text box resolves its
effective run size from the master's own `<p:otherStyle>` rather than `<p:bodyStyle>`.

#### CanvasNetPptx-PptxDocument-TextLayoutWordWrap: Word-Wrap and Horizontal Alignment

**Tests**: `ResolveTextLayout_WordWrap_NarrowWidth_WrapsAtTokenBoundary`,
`ResolveTextLayout_WordWrap_SingleTokenWiderThanAvailableWidth_PlacedAloneOnOwnLine`,
`ResolveTextLayout_WordWrap_WordSplitAcrossRuns_DoesNotWrapAtRunBoundary`,
`ResolveTextLayout_AlignCenter_CentersLineWithinAvailableWidth`,
`ResolveTextLayout_AlignRight_RightAlignsLineAgainstAvailableWidth`,
`ResolveTextLayout_RunBreakRun_ForcesSecondLine`,
`ResolveTextLayout_TallestFontComparison_ScalesByUnitsPerEmAndSizeEmu_NotRawFontUnits`

Proves a long run wraps onto multiple lines at the expected token boundary for a narrow
`widthEmu`; proves a single token that alone exceeds the available width is placed alone on its
own (overflowing) line rather than looping indefinitely; proves a word spelled across two
adjacent formatting runs with no whitespace between them (for example a bold "Hel" run
immediately followed by a plain "lo" run) is never wrapped at that internal run boundary -
`PackTokensIntoLines` packs consecutive non-whitespace tokens as a single wrap-atomic word group
regardless of which run each token's text came from, so an available width that fits either run's
own text alone but not their combined word still keeps both runs' glyphs on the same (overflowing)
line rather than incorrectly wrapping where no whitespace exists; proves center/right alignment
position a line's glyphs at the expected hand-computed X offset within the available width;
proves a paragraph containing a run, a preserved `<a:br/>` line-break item, and a second run lays
out as two separate lines (the break forces a line boundary independent of word-wrap's own
token-packing decisions); and proves the per-line "tallest run" selection compares two runs using
different fonts/sizes by each run's own font metrics scaled by that font's `UnitsPerEm` and the
run's own resolved `SizeEmu`, so a visually smaller run on a font reporting disproportionately
large raw font-design-unit metrics is not incorrectly selected over a visually larger run on a
differently-scaled font.

#### CanvasNetPptx-PptxDocument-TextAlignmentWidthUnderWrapNone: Alignment Width Independent of Line-Breaking Width

**Tests**: `ResolveTextLayout_AlignCenter_WrapNone_StillCentersAgainstShapesDeclaredWidth`,
`PptxDocument_Render_TxtFontPropsFixture_RendersEverySlideWithVisibleContent`

Proves a `wrap="none"` text body's centered paragraph is positioned against the same finite,
real width (`alignmentWidth`, the shape's own declared width less its horizontal insets) a
`wrap="square"` body's own alignment uses - identical X offsets for the same `"AA"` line in both
wrap modes - rather than against the effectively-infinite width `ResolveTextLayout` uses only to
suppress `wrap="none"`'s own word-wrap line-breaking decision. The second test additionally
proves this against a real-world fixture (`pythonpptx-txt-font-props.pptx` slide index 3, a
centered, `wrap="none"` underline-demo slide), confirming the synthetic regression test's finding
generalizes to a genuine third-party-generated file, not merely a hand-authored edge case.

#### CanvasNetPptx-PptxDocument-TextVerticalAnchor: Vertical Anchor Positioning

**Tests**: `ResolveTextLayout_AnchorTop_PositionsFirstBaselineAtAscentFromTop`,
`ResolveTextLayout_AnchorMiddle_CentersBlockVerticallyWithinAvailableHeight`,
`ResolveTextLayout_AnchorBottom_PositionsBlockAtBottomOfAvailableHeight`

Proves each of the three vertical anchors (`t`/`ctr`/`b`) positions the resolved text block's
first/last glyph `OriginYEmu` at the expected hand-computed offset within the available height.

#### CanvasNetPptx-PptxDocument-TextAutofit: Three-Tier Autofit Policy

**Tests**: `ResolveTextLayout_NoAutofit_AppliesNoScalingEvenWhenOverflowing`,
`ResolveTextLayout_SpAutoFit_AppliesNoScaling`,
`ResolveTextLayout_NormAutofitWithExplicitAttributes_AppliesStoredFactorsVerbatim`,
`ResolveTextLayout_NormAutofitAttributeLess_ShrinkLoopConvergesOnFirstFittingScale`,
`ResolveTextLayout_NormAutofitFontScaleOnly_AppliesStoredFontScaleVerbatim`,
`ResolveTextLayout_NormAutofitLnSpcReductionOnly_AppliesStoredReductionWithNeutralFontScale`,
`ResolveTextLayout_SubtitlePlaceholderWithAttributeLessNormAutofit_DoesNotOverShrinkContentThatFits`

Proves `<a:noAutofit>`/absent-autofit/`<a:spAutoFit>` all apply no scaling even when the resolved
text overflows the available height; proves an explicit `<a:normAutofit fontScale="..."
lnSpcReduction="..."/>` applies both stored factors verbatim; proves an attribute-less
`<a:normAutofit/>` on an intentionally overflowing fixture converges, via the bounded 10%-step
shrink loop, to the first hand-computed scale whose naturally laid-out height fits; proves a
`<a:normAutofit fontScale="..."/>` with no `lnSpcReduction` attribute applies the stored
`fontScale` verbatim while leaving line spacing at its neutral (no-reduction) default rather than
falling through to the attribute-less shrink loop; and proves a `<a:normAutofit
lnSpcReduction="..."/>` with no `fontScale` attribute applies the stored line-spacing reduction
(verified via the resulting glyph Y positions) while leaving `fontScale` at its neutral 100%
default. The final test was added during a real-world-corpus investigation of a reported subtitle
"invisible sliver" symptom: it proves a subtitle placeholder's attribute-less `<a:normAutofit/>`
does not over-shrink content that genuinely fits a realistically-sized box (`AppliedFontScale`
stays at `1.0`), refuting the hypothesis that the bounded shrink loop itself over-shrinks given
correct inputs - the reported symptom's precise real-world trigger could not be isolated further
from a minimal synthetic fixture in this pass (see the companion design document's "Genuine bug 3"
entry for the full investigation).

#### CanvasNetPptx-PptxDocument-TextRendering: Glyph Painting and Font Resolution

**Tests**: `PaintTextLayout_IdentityTransform_PaintsGlyphAtExpectedLocationWithResolvedColor`,
`PaintTextLayout_TranslatedShapeTransform_ShiftsPaintedLocation`,
`PaintTextLayout_EmptyOutlineGlyph_PaintsNothing`,
`PaintTextLayout_AsymmetricGlyph_PaintsInkAboveBaselineNotBelow`,
`ResolveTextFont_AnyFamilyHint_ResolvesNonNullFont`,
`ResolveTextLayout_PlusMinusFollowedByDegree_BothNeitherInPrimary_BothIndependentlyResolveFallback`,
`ResolveTextLayout_PlusMinusAlone_ResolvesFallbackGlyph`,
`ResolveTextLayout_PlusMinusFollowedByDegree_PrimaryCoversOnlyDegree_DegreeStaysOnPrimaryFont`,
`CanvasNetPptx_SystemIntegration_TextLayoutAndRender_TitlePlaceholderResolvesMasterTitleStyleEndToEnd`,
`CanvasNetPptx_SystemIntegration_TextLayoutAndRender_NonPlaceholderShapeResolvesMasterOtherStyleEndToEnd`

Proves `PaintTextLayout` paints a synthetic filled-square glyph's known ink pixel(s) in the
resolved color at the expected location under an identity shape-to-surface transform, proves a
translated shape transform shifts the painted location by the expected offset, proves a glyph
with an empty outline (no subpaths) paints nothing, proves a synthetic, deliberately asymmetric
glyph outline (ink confined to font-space y toward the ascender only, with none at all toward the
descender - a shape a symmetric square glyph cannot substitute for, since flipping a symmetric
shape about its own center is visually/structurally identical either way) is painted above the
baseline and not below it, catching a regression where a positive (rather than negated) Y scale on
the glyph-outline transform would vertically invert every glyph, and proves `ResolveTextFont`
resolves a non-null `TrueTypeFont` for an arbitrary family-name hint (falling back to the bundled
font); the two system-integration tests additionally prove `PaintTextLayout` paints at least one
non-background pixel end-to-end, from a full in-memory package opened via `PptxDocument.Open`
through `ResolveTextLayout` to a test `Surface`.

The three `ResolveTextLayout_PlusMinus...` tests reproduce the real-world "tofu box" (missing-
glyph) defect report - a degree sign (U+00B0) immediately following a plus-minus sign (U+00B1)
rendering as a missing-glyph box - and prove the fix is genuine per-character glyph-coverage
fallback, not any form of cross-character state leakage. Each injects two synthetic fonts via
`ResolveTextLayout`'s `fallbackFontResolver` test seam: a **primary** font built with a known,
deliberately incomplete cmap, and a **fallback** font built to cover whichever codepoint(s) the
primary font deliberately omits, each codepoint given its own distinct, real (non-empty) glyph
outline so a resolved glyph index can be asserted exactly, not merely "non-zero by accident."

- `ResolveTextLayout_PlusMinusFollowedByDegree_BothNeitherInPrimary_BothIndependentlyResolveFallback`:
  primary font covers neither U+00B1 nor U+00B0; both characters independently resolve to the
  fallback font with their own distinct, correct (non-`.notdef`) glyph indices - proving the
  second character's resolution is its own independent decision, not aided or corrupted by the
  first character's own fallback substitution.
- `ResolveTextLayout_PlusMinusAlone_ResolvesFallbackGlyph`: a baseline/control case isolating
  single-character fallback behavior (a run containing only the fallback-triggering character,
  with no following character to interact with).
- `ResolveTextLayout_PlusMinusFollowedByDegree_PrimaryCoversOnlyDegree_DegreeStaysOnPrimaryFont`:
  the literal reported-symptom reproduction - the primary font covers U+00B0 but deliberately
  **not** U+00B1; asserts U+00B1 resolves via the fallback font while the immediately following
  U+00B0 stays on the **primary** font (its own correct, non-`.notdef` glyph index), proving the
  preceding character's fallback substitution does not "stick" onto, or otherwise corrupt, the
  next character's own independent resolution - directly refuting the "sticky fallback"/state-
  corruption hypothesis the original bug report raised, in favor of the actual, confirmed root
  cause (no per-character coverage fallback existed at all prior to this change). This test also
  includes a baseline check: with no `fallbackFontResolver` configured at all (the historical,
  pre-feature default), U+00B0 alone still resolves via the primary font exactly the same way,
  confirming the primary font's own coverage - not an accidental fallback-font side effect - is
  what resolves it.

A one-off, non-permanent visual repro generator,
`GeneratePlusMinusDegreeTofuReproPng`, additionally renders this same "±°" two-character fixture
to an inspectable PNG file (`.agent-logs/pptx-degree-tofu-glyph-fallback-repro.png`) for manual
visual confirmation: its top half paints the pre-fix "before" behavior (a fallback resolver that
returns the primary font itself, i.e. no wider-coverage font is ever consulted - both characters
paint as invisible `.notdef` tofu, since the synthetic `.notdef` glyph used throughout this file
has zero contours), and its bottom half paints the fixed "after" behavior (a real fallback font
covering both characters - both paint their distinct, non-empty glyph outlines). This generator is
not part of the permanent regression-test guarantees (the three `[Fact]` tests above already
provide those); it exists solely to produce an artifact a human reviewer can open and inspect.

#### CanvasNetPptx-PptxDocument-TabStopExpansion: Default Tab-Stop Expansion

**Tests**: `ResolveTextLayout_TabCharacter_ExpandsToNextDefaultTabStop`,
`ResolveTextLayout_TabCharacter_WrapDecisionUsesExpandedWidthNotGlyphWidth`,
`ResolveTextLayout_MultipleTabCharacters_EachAdvancesToItsOwnNextTabStop`

Proves a literal U+0009 TAB run character expands the rendered cursor to the exact next
914400-EMU (1 inch) default tab stop - not the near-zero position a `.notdef`-glyph font advance
would otherwise produce for a character most fonts' own `cmap` does not cover - using a run
containing `"A\tB"` and asserting the glyph following the tab lands at the exact hand-computed
`914400` EMU X coordinate. Proves `PackTokensIntoLines`'s wrap/fit decision uses this same
dynamically-computed, tab-stop-expanded width (not the tab token's static, near-zero
glyph-measured width) when deciding where word-wrap breaks occur: a run containing two words
separated by an early tab, sized so the glyph-measured near-zero tab width would (incorrectly)
keep both words on one line (combined width 50800 EMU, comfortably under the chosen 920000 EMU
available width) while the true, tab-stop-expanded width (bringing the running width to 939400
EMU) correctly wraps the second word onto its own line - asserting the correct (wrapped) 2-line
outcome with hand-computed glyph X/Y coordinates for both lines. Proves two adjacent tab
characters each independently advance to their own next tab stop, computed from the cursor
position after the previous tab's own expansion (not both from the pre-first-tab position) - a
run containing `"A\t\tB"` where the second tab starts exactly on an already-aligned tab-stop
boundary (`914400`) still advances by a full, non-zero interval (landing at `1828800`, never
collapsing to a zero-width advance), confirming the "always advance by at least a minimal amount"
tab semantics `GetNextTabStopEmu` implements.

#### CanvasNetPptx-PptxDocument-PictureDecodeAndCrop: Picture Decoding, Linking, and Crop-Rectangle Resolution

**Tests**: `ResolveSrcRect_NoSrcRectElement_ReturnsNull`,
`ResolveSrcRect_AllEdgesPresent_ParsesScaledFractions`,
`ResolveSrcRect_SomeEdgesAbsent_DefaultsToZero`,
`ResolveSrcRect_NonNumericEdgeAttribute_ThrowsInvalidDataException`,
`ResolveSrcRect_NullBlipFillElement_ThrowsArgumentNullException`,
`ResolvePictureSurface_EmbeddedPng_DecodesSurfaceWithExpectedPixel`,
`ResolvePictureSurface_NoBlipElement_ThrowsInvalidDataException`,
`ResolvePictureSurface_BlipMissingEmbedAndLink_ThrowsInvalidDataException`,
`ResolvePictureSurface_LinkedBlipOnly_ThrowsPptxUnsupportedFeatureExceptionWithImageLinkToken`,
`ResolvePictureSurface_SvgOnlyBlipExtension_ReturnsDecodedSurfaceFromAsvgSvgBlipEmbed`,
`ResolvePictureSurface_SvgBlipExtensionMissingEmbed_ThrowsInvalidDataException`,
`ResolvePictureSurface_UnsupportedContentType_ThrowsPptxUnsupportedFeatureExceptionWithImageFormatToken`,
`ResolvePictureSurface_NullArguments_ThrowsArgumentNullException`

Proves `ResolveSrcRect` returns `null` when `<a:srcRect>` is absent, proves it parses all four
`l`/`t`/`r`/`b` scaled-fraction edges into a normalized `[0,1]` `PptxSrcRect` when present, proves
each omitted edge defaults to `0`, proves a non-numeric edge attribute throws
`InvalidDataException`, and proves a `null` `<p:blipFill>` element throws
`ArgumentNullException`. Proves `ResolvePictureSurface` decodes a synthetic 1x1 embedded PNG media
part (constructed via an in-memory `.pptx`-shaped ZIP package with slide/layout/master/theme/media
parts, `BuildMinimalImagePackage`) into a `Surface` whose single pixel matches the source PNG's
known color, proves a `<p:pic>` with no `<a:blip>` element at all throws
`InvalidDataException`, proves a `<a:blip>` with neither `r:embed` nor `r:link` nor any recognized
extension fallback throws `InvalidDataException` (a genuinely malformed blip - this is an explicit
non-regression check: the fix below only decodes the one recognized SVG-extension-fallback case,
not every embed/link-less blip), proves a `<a:blip>` with only `r:link` (no embedded bytes)
throws `PptxUnsupportedFeatureException` carrying the `"pptx-image-link"` feature token, proves a
`<a:blip>` with neither `r:embed` nor `r:link` but carrying a recognized Microsoft SVG extension
fallback (`<a:extLst>/<a:ext uri="{96DAC541-7B7A-43D3-8B79-37D633B846F1}">` wrapping an
`<asvg:svgBlip r:embed="...">`, the "Insert Icon" SVG-with-no-raster-fallback pattern real
PowerPoint produces) now resolves to a non-null, correctly-sized `Surface` rasterized via the
sibling `DemaConsulting.CanvasNet.Svg` package's `SvgCodec` - a Phase 2 Follow-Up fix, since this
well-formed, valid OOXML construct is now decoded rather than rejected - proves that same SVG
extension present but with no `r:embed` of its own on `<asvg:svgBlip>` (genuinely malformed -
nothing left to fall back to) still throws `InvalidDataException`, proves a media part whose
resolved content type is not a recognized raster format throws `PptxUnsupportedFeatureException`
carrying the `"pptx-image-format"` feature token, and proves null argument validation for both
`picElement`/`document`.

#### CanvasNetPptx-PptxDocument-PicturePainting: Picture Compositing Without PDF's Y-Flip

**Tests**: `PaintPicture_IdentityTransformNoCrop_PaintsImagePixelsUnflipped`,
`PaintPicture_TopBottomAsymmetricImage_PaintsWithoutVerticalFlip`,
`PaintPicture_WithSrcRectCroppingLeftHalf_SamplesOnlyRightHalf`,
`PaintPicture_TranslatedTransform_ShiftsPaintedFootprint`,
`PaintPicture_DegenerateTransform_PaintsNothing`,
`PaintPicture_NullSurfaceOrImage_ThrowsArgumentNullException`,
`PaintPicture_SourceImageWithAlphaChannel_AlphaBlendsOntoExistingBackground`,
`PaintPicture_EllipseClipPath_CornerUnpaintedCenterPainted`,
`PaintPicture_NullClipPath_PaintsFullRectangleUnclipped`

Proves `PaintPicture` paints a decoded image's pixels onto a destination `Surface` unchanged
(pixel-for-pixel) under an identity shape transform with no crop, proves a deliberately
top/bottom-asymmetric synthetic source image (a shape a symmetric image cannot substitute for,
mirroring Phase 1d's own asymmetric-glyph regression-detection rationale) paints with its rows in
the same top-down order as the source - **not** vertically flipped, the deliberate divergence from
`DemaConsulting.CanvasNet.Pdf`'s own y-up image-painting convention - proves an `<a:srcRect>`
cropping away the left half samples only the image's own right half, proves a translated shape
transform shifts the painted footprint by the expected offset, proves a singular (non-invertible,
for example zero-scale) shape transform paints no pixels at all rather than throwing or dividing
by zero, proves `null` `destination`/`image` arguments throw `ArgumentNullException`, and proves
(regression guard for a confirmed real-world raw-overwrite alpha-compositing bug) each sampled
source pixel is alpha-blended "over" the existing destination pixel - a fully transparent source
pixel with a non-matching stored RGB leaves the background completely unchanged, a fully opaque
source pixel exactly replaces it, and a partially transparent source pixel blends to the exact
expected bytes per the documented Porter-Duff "over" formula. Proves `PaintPicture`'s own new
optional `clipPath` parameter: an elliptical clip path leaves a bounding-box corner unpainted
while the shape's own center still paints the image's color, and a `null` clip path (the
default) continues painting the full rectangle unclipped - a direct regression guard for every
pre-existing call site above. See also
"CanvasNetPptx-PptxDocument-PictureGeometryClipping" below for the end-to-end render-level
clip-resolution coverage.

#### CanvasNetPptx-PptxDocument-TableParsing: Table Structure, Cell Attributes, and Verbatim Fill/Border/Text Reuse

**Tests**: `ParseTable_WellFormedTable_ParsesColumnsAndRows`,
`ParseTable_MissingGraphicData_ThrowsInvalidDataException`,
`ParseTable_NonTableGraphicFrameKind_ThrowsPptxUnsupportedFeatureExceptionWithGraphicFrameKindToken`,
`ParseTable_MissingTblGrid_ThrowsInvalidDataException`,
`ParseTable_GridColNonNumericWidth_ThrowsInvalidDataException`,
`ParseTable_TrMissingHeight_ThrowsInvalidDataException`,
`ParseTableCell_NoAttributes_DefaultsToSpanOneNoMerge`,
`ParseTableCell_AttributesPresent_ParsesSpanAndMergeFlags`,
`ParseTableCell_NonNumericGridSpan_ThrowsInvalidDataException`,
`ParseTableCell_TcPrWithFillAndBorders_ResolvesFillAndBorders`,
`ParseTableCell_NoTxBody_TextBodyIsNull`,
`ParseTableCell_WithTxBody_ParsesTextBody`,
`ParseTable_GridSpanCellWithGradientFill_UsesMergedWidth`,
`ParseTable_RowSpanCellWithGradientFill_UsesMergedHeight`

Proves `ParseTable` parses a well-formed `<a:tbl>`'s column widths, row heights, and cell
structure into a `PptxTable`; proves a missing `<a:graphicData>` throws
`InvalidDataException`; proves a `<a:graphicData>` whose `uri` does not end in `"/table"` throws
`PptxUnsupportedFeatureException` carrying the `"pptx-graphic-frame-kind"` feature token; proves a
missing `<a:tblGrid>`, a non-numeric `<a:gridCol>` `w` attribute, and a missing `<a:tr>` `h`
attribute each throw `InvalidDataException`. Proves `ParseTableCell` defaults `gridSpan`/`rowSpan`
to `1` and `hMerge`/`vMerge` to `false` when their attributes are absent, proves it parses each
attribute correctly when present, proves a non-numeric `gridSpan` throws `InvalidDataException`,
proves a cell's `<a:tcPr>` fill/border elements resolve via the already-verified Phase 1c
`ResolveFill`/`ResolveLineStyle` resolvers, and proves a cell's `<a:txBody>` presence/absence
resolves its `PptxTableCell.TextBody` field correctly (`null` when absent, a parsed
`PptxTextBody` via the already-verified Phase 1d `ParseTextBody` when present). Proves (Review
Follow-Up) that `ParseTable` sizes a `gridSpan="2"` cell's gradient fill against its true summed
two-column width - not only its first column's own width, as an un-merge-aware sizing would
produce - by comparing the merged cell's own resolved `LinearGradient`'s `End.X - Start.X` extent
against an equivalent `gridSpan="1"` cell's own extent; proves the same for a `rowSpan="2"`
cell's gradient height, comparing against the row-heights list `ParseTable` now precomputes up
front (`rowHeightsEmu`) via the new `SumConsecutive` helper.

#### CanvasNetPptx-PptxDocument-TableCellRectResolution: Merge-Aware Cell-Rect Computation

**Tests**: `ResolveCellRects_SimpleGrid_ComputesCumulativeOffsets`,
`ResolveCellRects_MergeContinuationCells_AreSkipped`,
`ResolveCellRects_HorizontalMerge_ComputesSpannedWidth`,
`ResolveCellRects_VerticalMerge_ComputesSpannedHeight`,
`ResolveCellRects_GridSpanMissingHMergePlaceholder_CompensatesColumnAdvance`

Proves `ResolveCellRects` computes each cell's own cumulative `X`/`Y` offset correctly across a
simple, unmerged grid; proves a merge-continuation cell (`hMerge`/`vMerge` set) contributes no
`PptxResolvedTableCell` of its own (it is skipped entirely); proves a horizontally-merged cell's
resolved rectangle width equals the sum of its spanned columns' own widths, while the column
offset for cells *after* the merge still advances correctly (this test originally failed during
implementation - the running column offset was incorrectly advancing by the merged cell's own
full spanned width instead of by each physical `<a:tc>` entry's own single-column width, a defect
diagnosed and fixed in `ResolveCellRects` before this test was declared passing - see the design
doc's own *Cell-Rect Resolution* subsection for the full root-cause explanation); and proves a
vertically-merged cell's resolved rectangle height equals the sum of its spanned rows' own
heights. Proves (Review Follow-Up) that a malformed row - a `gridSpan="2"` governing cell
immediately followed by a different, independent cell with no `hMerge` continuation placeholder
at all (a structure the schema's own `minOccurs="0"` on `<a:tc>` permits) - no longer corrupts
the following cell's computed column offset: `ResolveCellRects` now counts the actually-present
`hMerge` continuations following a governing cell and defensively advances both `xEmu` and
`columnIndex` past any shortfall, so the governing cell's own merged rectangle is unaffected and
the following, unrelated cell lands at its correct, non-overlapping column offset.

#### CanvasNetPptx-PptxDocument-TableRowHeightGrowth: Row-Height Growth to Fit Wrapped Text

**Tests**:
`ResolveCellRects_CellTextRequiresMoreHeightThanStored_GrowsRowAndShiftsNextRowYOffset`,
`ResolveCellRects_CellTextFitsWithinStoredHeight_RowHeightUnaffected`,
`ResolveCellRects_RowSpanCellTextRequiresMoreHeightThanSpanTotal_GrowsLastSpannedRow`,
`ResolveCellRects_NoThemeOrFontResolverSupplied_PreservesStoredHeightsUnconditionally`,
`PaintTable_TwoRowTableWithWrappedTextOverflowingStoredHeight_PaintsSecondRowBelowGrownFirstRow`

Proves `ResolveCellRects` grows a row's own effective height past its stored `<a:tr h="...">`
value when a single-row cell's own wrapped text requires more vertical space than that stored
height provides, and that the following row's own computed `Y` offset reflects the grown, not
stale stored, height - directly proving the reported row-overlap symptom no longer reproduces.
Proves a stored height already comfortably exceeding a cell's own required text height is left
entirely unchanged (the regression-safety guarantee: growth only ever enlarges a row, never
shrinks or otherwise perturbs one that already fits). Proves a row-spanning cell (`RowSpan > 1`)
whose required text height exceeds its spanned rows' stored height sum has its own shortfall
added entirely onto the *last* spanned row, leaving an earlier spanned row - which may itself
anchor an unrelated single-row cell in the same row - unaffected. Proves omitting the new optional
`theme`/`fontResolver` parameters preserves today's stored-height-only behavior unconditionally,
even for a cell that would otherwise require growth - confirming the feature is strictly opt-in
and every pre-existing direct `ResolveCellRects` call site remains unaffected. Proves, end-to-end
through `PaintTable`, that a two-row table's second row paints its own fill starting below the
first row's grown (not stored) bottom edge, when the first row's own cell text wraps to more lines
than its stored height can fit.

#### CanvasNetPptx-PptxDocument-TablePainting: Cell Fill, Border, and Text Painting

**Tests**: `PaintTable_SolidFilledCell_PaintsFillColorAcrossCellRectangle`,
`PaintTable_CellWithTopBorder_PaintsStrokedLineAtTopEdge`,
`PaintTable_CellWithText_PaintsGlyphInkInsideCellRectangle`,
`PaintTable_EmptyCell_PaintsNothing`,
`PaintTable_GradientFilledCellUnderNonIdentityTransform_PositionsGradientInSurfaceSpace`

Proves `PaintTable` paints a solid-filled cell's resolved color across its own rectangle, proves a
cell with a resolved top border paints a stroked line at the cell's own top edge, proves a cell's
text content paints glyph ink somewhere inside the cell's own rectangle (via a cell sized/scaled
and inset-zeroed so a default-size glyph reliably lands within the test surface - mirroring Phase
1d's own `ResolveTextLayout`/`PaintTextLayout` test conventions), and proves an empty cell (no
fill, no border, no text) paints no non-background pixels at all. Proves (Review Follow-Up) that
`PaintTable`/`PaintCellBorder` compose a non-identity `shapeToSurfaceTransform` into a
gradient-filled cell's own `Gradient` before filling (via `FillPaint`'s new four-parameter
overload): a gradient-filled cell painted through a 2x-scale `shapeToSurfaceTransform` samples the
first stop's color near the surface-space-mapped start of the ramp and the last stop's color near
its mapped end, proving the ramp follows the shape into surface space rather than staying fixed in
untransformed local-EMU coordinates.

#### CanvasNetPptx-PptxDocument-TableStyleResolution: `<a:tableStyleId>` Table-Style/Banding Resolution

**Tests**: `ParseTable_FirstRowTblPrWithMatchingTableStyle_HeaderRowCellUsesFirstRowStyleFill`,
`ParseTable_BandRowTblPrWithMatchingTableStyle_AlternatesBand1HAndBand2HFillStartingAfterHeaderRow`,
`ParseTable_TblPrWithNoTableStyleId_FallsBackToPlainCellOnlyBorderAndNoFill`,
`ParseTable_TblPrWithUnresolvableTableStyleId_FallsBackToPlainCellOnlyBorderAndNoFillWithoutThrowing`,
`ParseTableCell_TcPrExplicitFillAndBorder_OverridesTableStyleFillAndBorder`,
`ParseTableCell_InteriorColumnBorder_UsesInsideVNotLeftRightTcBdrEdge`

Proves a table whose `<a:tblPr>` declares a `<a:tableStyleId>` that resolves to a matched
`<a:tblStyle>`, with `firstRow="1"`, renders its header row (row index `0`) cell using that
style's own `<a:firstRow>` tier fill, while a non-header row falls back to the style's
`<a:wholeTbl>` base-tier fill. Proves a five-row table with both `firstRow="1"` and `bandRow="1"`
renders its header row via `<a:firstRow>` and alternates `<a:band1H>`/`<a:band2H>` fills across
the remaining rows starting immediately after the header row (confirming the row-parity
convention - a styled header row excluded from band-row counting entirely), and proves a band
tier declaring no `<a:fill>` at all (here, `<a:band2H>`) falls through to the `<a:wholeTbl>` base
fill rather than rendering "no fill". Proves a table whose `<a:tblPr>` declares no
`<a:tableStyleId>` at all never even invokes the supplied table-style resolver delegate (asserted
via a resolver that throws if invoked), and proves a table whose `<a:tableStyleId>` does not
resolve to any known `<a:tblStyle>` (the resolver returns `null`) likewise falls back to today's
pre-existing plain, style-less cell-only fill/border resolution, in both cases without throwing.
Proves a cell's own explicit `<a:tcPr>` fill and a single explicit border edge (`<a:lnT>`) take
final precedence over a matched table style's own fill/borders (mirroring
`ResolveShapeLineStyle`'s established "explicit always wins" precedence pattern), while the
cell's remaining, non-overridden border edges still fall back to the matched style's own
`<a:wholeTbl>` tier. Proves a three-column table's interior column boundaries resolve against the
matched style's own `<a:tcBdr>/<a:insideV>` edge rather than its `left`/`right` edges, while the
table's two true outer-boundary edges (the first cell's own left edge, the last cell's own right
edge) still resolve against `left`/`right` - confirming the structural outer-vs-interior edge-name
mapping independently of the `firstRow`/`bandRow` *styling* flags exercised by the preceding tests.

#### CanvasNetPptx-PptxDocument-ChartGraphicFrames: Chart Graphic Frame Dispatch, Resolution, Exception Wrapping, and Rendering

**Tests**: `ParseShapeTree_GraphicFrameWithSupportedChart_DispatchesToChartNode`,
`ParseShapeTree_GraphicFrameWithTable_StillDispatchesToTableNode`,
`ParseShapeTree_GraphicFrameWithRadarChart_ThrowsPptxUnsupportedFeatureExceptionWithWrappedFeatureToken`,
`ParseChart_ChartElementWithNoRelationshipId_ThrowsInvalidDataException`,
`ParseChart_GraphicDataWithNoChartChild_ThrowsInvalidDataException`,
`Render_ChartOnlySlide_PaintsVisibleContent`,
`PptxDocument_Render_Aiden0zChartAndComplexFixture_Slide0AndSlide1PaintContent`,
`PptxDocument_Render_SamplelibSamplePresentationFixture_EverySlidePaintsContent`,
`PptxDocument_Render_PythonPptxChartLineFixture_RendersSuccessfully`,
`PptxDocument_Render_PythonPptxChartUnsupportedRadarFixture_ThrowsPptxUnsupportedFeatureException`,
`PptxDocument_Render_ShpShapesFixture_Chart2NodeHasNonNullChart`

Proves `ParseShapeTree` dispatches a `<p:graphicFrame>` declaring a supported chart kind (bar,
line, pie, and area, each via its own `[InlineData]` case) to a `PptxGraphicFrameShapeNode` whose
`Chart` is non-null and whose `Table` is `null`, via the new `ParseChart`/`OpenXmlChartParser`
integration; proves a `<p:graphicFrame>` declaring a table still dispatches to `ParseTable`
exactly as before (`Table` non-null, `Chart` null) - a sanity check that chart dispatch does not
regress the pre-existing table path. Proves a chart declaring a recognized-but-unsupported kind
(radar) throws `PptxUnsupportedFeatureException` whose `Feature` is the wrapped
`ChartUnsupportedFeatureException`'s own `Feature` value prefixed with `"pptx-chart-"` (here,
`"pptx-chart-charts-openxml-radar-chart"`). Proves `ParseChart` itself throws
`InvalidDataException` for an `<a:graphicData>` with no `<c:chart>` child, and separately for a
`<c:chart>` element with no `r:id` attribute - each a malformed, not merely unsupported, chart
graphic frame. Proves a synthetic, fully in-memory chart-only slide package renders through the
public `Render` API and paints at least one non-transparent pixel, proving the chart-compositing
branch (`ChartRenderer.Render` + the existing `PaintPicture` primitive) works end-to-end, not just
at the dispatch/parse level. Proves three real-world corpus fixtures now render their own
chart-bearing slides successfully where they previously threw
(`aiden0z-1-chart-and-complex.pptx` slide index 1, `samplelib-sample-presentation.pptx` slide
index 4 - now folded into every slide of that fixture rendering - and the new, locally-generated
`pythonpptx-chart-line.pptx`), and that the new, locally-generated
`pythonpptx-chart-unsupported-radar.pptx` throws `PptxUnsupportedFeatureException` with the
expected wrapped radar feature token. Proves, via a dedicated isolation test that parses
`pythonpptx-shp-shapes.pptx` slide index 0's own shape tree directly with
`containUnsupportedGraphicFrames: true` (isolating "Chart 2" from its unsupported sibling
"Diagram 7" SmartArt graphic frame, and supplying this slide's own real layout/master/theme chain
as its `themeResolver` since the same shape tree also contains a real "Table 1" table graphic
frame whose own `ParseTable` call needs a real theme), that "Chart 2" alone resolves to a
non-null `Chart`/null `Table` node - confirming directly, not assuming, that this slide's
whole-`Render` call still throws `PptxUnsupportedFeatureException` solely because of "Diagram 7",
not because of "Chart 2" (see the corpus-level scenario below for the containing, uncontained
slide-level assertion).

**Note on the originally-anticipated "master/layout-owned chart gracefully skipped" scenario**:
this phase's planning anticipated documenting a `containUnsupportedGraphicFrames: true` scenario
specifically for a master/layout-owned *chart* with an unsupported feature. Since charts are now
a supported graphic-frame kind in general (not a deferred one), that scenario no longer applies as
originally envisioned; the pre-existing `Render_MasterGraphicFrameWithUnsupportedKind_
SkipsThatShapeAndStillRendersSlideContent` test (in `PptxRenderTests.cs`), which proves a master-
owned graphic frame of *any* unsupported kind is gracefully skipped (not propagated) while the
rest of the slide still renders, was adapted this phase to use a diagram-kind (SmartArt) stand-in
graphic frame in place of its original chart-kind one, since a `.../chart`-suffixed URI now
dispatches to the real `ParseChart` rather than remaining a generic "any unsupported kind"
stand-in - preserving this test's original intent (a master-owned, genuinely-unsupported-kind
graphic frame is gracefully skipped under `containUnsupportedGraphicFrames: true`) without
colliding with the new chart dispatch.

#### CanvasNetPptx-PptxDocument-ShapeTree: Recursive Shape-Tree Parsing and Deferred Theme Resolution

**Tests**: `ParseShapeTree_FreeformShape_ProducesSpShapeNodeWithNullPlaceholder`,
`ParseShapeTree_PlaceholderShape_ProducesSpShapeNodeWithResolvedPlaceholder`,
`ParseShapeTree_Picture_ProducesPictureShapeNode`,
`ParseShapeTree_GraphicFrameTable_ProducesGraphicFrameShapeNodeWithParsedTable`,
`ParseShapeTree_NoGraphicFrame_NeverInvokesThemeResolver`,
`ParseShapeTree_UnrecognizedElements_SilentlySkipped`,
`ParseShapeTree_Connector_ProducesConnectorShapeNode`,
`ParseShapeTree_Group_ProducesGroupShapeNodeWithDirectSiblingChildren`,
`ParseShapeTree_NestedGroups_EachResolveOwnChildTransformAndChildren`,
`ParseShapeTree_GroupMissingXfrm_ThrowsInvalidDataException`,
`ParseShapeTree_GraphicFrameInsideGroup_ParsesTableUsingPropagatedThemeResolver`,
`TryParsePlaceholder_FreeformShape_ReturnsNull`,
`TryParsePlaceholder_NoTypeOrIdxAttributes_DefaultsToObjAndZero`,
`TryParsePlaceholder_NonNumericIdx_ThrowsInvalidDataException`

Proves `ParseShapeTree` dispatches a freeform `<p:sp>` (no `<p:ph>`) to a `PptxSpShapeNode` with a
`null` `Placeholder`, proves a placeholder `<p:sp>` resolves a non-null `Placeholder` with the
expected type/idx, proves a `<p:pic>` dispatches to a `PptxPictureShapeNode` wrapping the raw
element, proves a `<p:graphicFrame>` declaring a table dispatches to a `PptxGraphicFrameShapeNode`
with its table eagerly parsed, proves a shape tree with no `<p:graphicFrame>` at all never invokes
the supplied `Func<PptxTheme>` theme resolver (confirming the deferred, lazy theme-resolution
design - see the design doc's own *Shape Tree* subsection), proves unrecognized element kinds
(`<p:contentPart>`) are silently skipped rather than rejected, proves a `<p:cxnSp>` dispatches to
a `PptxConnectorShapeNode` wrapping the raw element (see the *Connector Shape Rendering*
subsection below), proves a `<p:grpSp>`
dispatches to a `PptxGroupShapeNode` whose children are its own direct siblings (not wrapped in a
nested `<p:spTree>`) with its child transform resolved from its own `<a:xfrm>`, proves nested
`<p:grpSp>` elements each resolve their own child transform and recurse into their own direct
children independently, proves a `<p:grpSp>` with no `<p:grpSpPr>/<a:xfrm>` element throws
`InvalidDataException`, and proves a `<p:graphicFrame>` nested inside a group still resolves its
table correctly via the propagated theme resolver. Proves `PptxPlaceholderParser.TryParsePlaceholder`
(the Phase 1b placeholder-detection logic, extracted this phase into a standalone, independently
callable helper with no behavior change - confirmed by re-running the full pre-existing Phase
1a-1d test suite immediately after the extraction) returns `null` for a freeform shape, applies
the `"obj"`/`0` schema defaults when `type`/`idx` attributes are omitted, and rejects a
non-numeric `idx` attribute with `InvalidDataException`.

### Full Slide Rendering (Phase 1f) Test Scenarios

#### CanvasNetPptx-PptxDocument-SlideRendering: Shape-Tree Walk and Per-Node-Kind Dispatch

**Tests**: `Render_SingleFullSlideShape_PaintsFillColorAcrossSurface`,
`Render_EmptyShapeTree_RendersOnlyBackground`,
`Render_SmallPixelDimensionsAgainstLargeEmuSlideSize_DoesNotTruncateTransformToZero`,
`Render_TwoOverlappingShapes_LaterShapePaintsOnTopInDocumentOrder`,
`Render_ShapeWithNoXfrm_SkippedSilently`,
`Render_NestedGroup_ComposesChildTransformIntoExpectedSurfaceLocation`,
`Render_DoublyNestedGroups_EachComposeOwnChildTransform`,
`Render_Picture_PaintsEmbeddedImageAtExpectedLocation`,
`Render_PictureMissingBlipFill_SkippedSilently`,
`Render_Table_PaintsCellFillAcrossCellRectangle`,
`Render_GraphicFrameMissingXfrm_SkippedSilently`,
`Render_NonPlaceholderShapeWithText_PaintsGlyphInkInsideShapeRectangle`,
`Render_PlaceholderShape_ReadsTextFromSlideLevelShapeNotLayout`,
`Render_PlaceholderShapeWithEmptySpPr_InheritsXfrmAndGeometryFromLayout`,
`PptxDocument_Render_BlankSlideFixtures_RendersWithoutError`,
`PptxDocument_Render_ShpAutoshapePropsFixture_PaintsVisibleContent`,
`PptxDocument_Render_ShpGroupShapeFixture_RendersWithoutError`,
`PptxDocument_Render_ShpPictureFixture_RendersEverySlideWithVisibleContent`,
`PptxDocument_Render_TblCellFixture_RendersEverySlideWithVisibleContent`,
`PptxDocument_Render_TxtFontPropsFixture_RendersEverySlideWithVisibleContent`,
`PptxDocument_Render_TxtTextFrameFixture_RendersEverySlideWithVisibleContent`,
`PptxDocument_Render_ShpShapesFixture_Slide0ThrowsUnsupportedFeatureSlide1PaintsContent`,
`PptxDocument_Render_SamplelibSamplePresentationFixture_Slide4ThrowsUnsupportedFeatureOthersPaintContent`,
`PptxDocument_Render_SldBackgroundFixture_RendersWithoutErrorBackgroundNotPainted`,
`PptxDocument_Render_PhInheritPropsFixture_Slide0PaintsSlide1RendersWithoutError`,
`PptxDocument_Render_PhUnpopulatedPlaceholdersFixture_RendersEverySlideWithoutError`,
`PptxDocument_Render_TxtFitTextFixture_PaintsVisibleContent`,
`PptxDocument_Render_ShpConnectorPropsFixture_ConnectorsPaintVisibleLines`,
`PptxDocument_Render_DmlFillFixture_Slide0PictureFillNoLongerBlocksRenderingOnlyPatternFillThrows`,
`PptxDocument_Render_DmlFillFixture_Slide1PatternFillThrowsUnsupportedFillFeature`,
`PptxDocument_Render_DmlLineFixture_RendersEverySlideWithVisibleContent`,
`PptxDocument_Render_Aiden0zChartAndComplexFixture_Slide0PaintsSlide1ThrowsUnsupportedFeature`,
`PptxDocument_Render_Aiden0zImageCropCssResetFixture_PaintsVisibleContentBackgroundNotPainted`,
`PptxDocument_Render_Aiden0zTableStaleFrameFixture_PaintsVisibleContentDespiteFrameSizeMismatch`

Proves a single full-slide shape's resolved fill paints across the destination surface, proves a
slide with no shapes at all renders only the cleared background, proves rendering at a pixel size
far smaller than the slide's own EMU magnitude does not truncate the EMU-to-pixel transform to a
zero scale factor via integer division, proves two overlapping shapes composite in document order
(the later shape paints on top of the earlier one), proves a shape with no resolvable `<a:xfrm>`
is skipped silently rather than throwing, proves a shape nested inside a `<p:grpSp>` is painted at
the expected surface location after composing the group's own child transform with the base
transform, proves doubly nested groups each compose their own child transform independently
through two full levels of
recursion, proves a `<p:pic>` paints its embedded image at the expected surface location, proves
a `<p:pic>` missing its own `<p:blipFill>` is skipped silently, proves a `<p:graphicFrame>`'s
table paints a cell's own resolved fill across its cell rectangle, proves a `<p:graphicFrame>`
missing its own direct `<p:xfrm>` child is skipped silently, proves a non-placeholder shape's own
text body paints glyph ink somewhere inside the shape's own rectangle, proves a placeholder
shape's text is read from the slide-level shape element's own `<p:txBody>` rather than from its
matched layout placeholder (which supplies styling only), and proves a placeholder shape whose
own `<p:spPr/>` is empty still inherits its `<a:xfrm>`/geometry from its matched layout placeholder
rather than being silently skipped.

The remaining twenty `PptxDocument_Render_*Fixture_*` tests are the real-world corpus-conformance
tier added in the Phase 2 hardening pass (see `pptx-document.md`'s own Phase 2 design section and
`PptxFixtures/README.md` for full provenance): each opens one of twenty genuine, independently-
sourced `.pptx` files across three independent sources and renders every one of its slides,
proving real-file conformance - not merely synthetic-package conformance - for the shape-tree walk
and per-node-kind dispatch this requirement specifies. These tests additionally prove, against
real files rather than merely hand-authored packages, that a chart/SmartArt-bearing
`<p:graphicFrame>` slide throws `PptxUnsupportedFeatureException` cleanly
(`pythonpptx-shp-shapes.pptx` slide 0, `samplelib-sample-presentation.pptx` slide 4,
`aiden0z-1-chart-and-complex.pptx` slide 1), that a group shape whose every child shape
relies solely on an unresolved `<p:style>` shape-style-matrix reference for its fill (an
out-of-scope construct, see `pptx-document.md`'s Phase 1c deferred-items list) renders without
error despite painting no visible ink (`pythonpptx-shp-groupshape.pptx`), and that
`pythonpptx-dml-fill.pptx`'s slide 0 and slide 1 - which between them place a real, embedded
`<a:blipFill>`/`<a:tile>` picture-filled shape, a non-conformant attribute-less `<a:pattFill/>`,
and three named-preset `<a:pattFill prst="...">` pattern-filled shapes (`divot` x2, `wave`) -
all now render completely, end-to-end, without throwing at all: picture fill is fully supported
(see `CanvasNetPptx-PptxDocument-PictureFillResolution` above) and pattern fill is supported for
a documented covered-preset subset, including every preset this fixture itself declares (see
`CanvasNetPptx-PptxDocument-PatternFillResolution` above) - this fixture no longer exercises the
`"pptx-pattern-fill"` exception token at all; that token is still proven directly, against an
uncovered preset, by `ResolveFill_PatternFillUnsupportedPreset_ThrowsPptxUnsupportedFeatureException`
above instead. A slide's own
`<p:bg>` background fill is covered by
`CanvasNetPptx-PptxDocument-SlideBackgroundFill` below instead, including both of these same real
fixtures' own now-painted backgrounds.

#### CanvasNetPptx-PptxDocument-RenderPublicApi: Public API Argument Validation and DPI Convenience Overload

**Tests**: `Render_DefaultOptions_ClearsUnpaintedAreaToOpaqueWhite`,
`Render_CustomBackgroundColor_ClearsToThatColor`,
`Render_FullyTransparentBackgroundColor_ClearsToTransparent`,
`Render_NegativeSlideIndex_ThrowsArgumentOutOfRangeException`,
`Render_SlideIndexAtSlideCount_ThrowsArgumentOutOfRangeException`,
`Render_NonPositiveWidth_ThrowsArgumentOutOfRangeException`,
`Render_NonPositiveHeight_ThrowsArgumentOutOfRangeException`,
`Render_DisposedDocument_ThrowsObjectDisposedException`,
`Render_Dpi_NonPositiveDpi_ThrowsArgumentOutOfRangeException`,
`Render_Dpi_NonFiniteDpi_ThrowsArgumentOutOfRangeException`,
`Render_Dpi_NegativeSlideIndex_ThrowsArgumentOutOfRangeException`,
`Render_Dpi_SlideIndexAtSlideCount_ThrowsArgumentOutOfRangeException`,
`Render_DpiOverload_ComputesExpectedPixelDimensionsFromSlideSize`

Proves `Render` clears its destination surface to `PptxRenderOptions.Default`'s opaque white when
no options are supplied, proves a caller-supplied `BackgroundColor` (including a fully transparent
one) clears the surface to that exact color instead, proves both overloads reject a negative or
out-of-range `slideIndex`, a non-positive `width`/`height`, a disposed document, and - for the DPI
overload only - a non-positive or non-finite `dpi`, each with `ArgumentOutOfRangeException` or
`ObjectDisposedException` as appropriate, and proves the DPI convenience overload computes the
expected pixel width/height from the slide's own EMU `SlideSize` (a 10in by 7.5in slide at 96 DPI
renders a 960 by 720 pixel surface).

#### CanvasNetPptx-PptxDocument-SlideBackgroundFill: Slide/Layout/Master Background Fill (`<p:bg>`)

**Tests**: `Render_SlideLevelSolidBackground_PaintsAcrossFullSlide`,
`Render_SlideLevelBackgroundWithShape_ShapePaintsOnTopOfBackground`,
`Render_LayoutLevelSolidBackground_PaintsWhenSlideDeclaresNone`,
`Render_MasterLevelSolidBackground_PaintsWhenNeitherSlideNorLayoutDeclaresOne`,
`Render_SlideLevelBackground_TakesPriorityOverLayoutAndMasterBackgrounds`,
`Render_LayoutLevelBackground_TakesPriorityOverMasterBackgroundWhenSlideDeclaresNone`,
`Render_NoBackgroundAnywhere_FallsBackToOptionsBackgroundColor`,
`Render_SlideLevelThemeIndexedBackgroundReference_ResolvesBgFillStyleListEntryWithPhClrSubstitution`,
`Render_SlideLevelThemeIndexedBackgroundReferenceIdxZero_PaintsNoBackground`,
`ResolveSlideBackgroundFill_AllThreeTiersNull_ReturnsNull`,
`ResolveSlideBackgroundFill_SlideDeclaresOne_ResolvesSlideOwnBackground`,
`ResolveSlideBackgroundFill_SlideDeclaresNoneLayoutDoes_ResolvesLayoutOwnBackground`,
`ResolveSlideBackgroundFill_SlideAndLayoutDeclareNoneMasterDoes_ResolvesMasterOwnBackground`,
`ResolveSlideBackgroundFill_EmptySlideBackgroundStillWinsOverLayout_MatchesPlaceholderInheritancePrecedent`,
`ResolveSlideBackgroundFill_BgElementWithNeitherBgPrNorBgRef_ThrowsInvalidDataException`,
`ResolveSlideBackgroundFill_BgRefIdxZero_ReturnsNoFill`,
`ResolveSlideBackgroundFill_BgRefIdxOneThousand_ReturnsNoFill`,
`ResolveSlideBackgroundFill_BgRefIdxInFillStyleListRange_ThrowsPptxUnsupportedFeatureException`,
`ResolveSlideBackgroundFill_BgRefIdxResolvesBgFillStyleListEntryWithPhClrSubstitution`,
`ResolveSlideBackgroundFill_BgRefIdxSecondEntry_ResolvesSecondBgFillStyleListEntry`,
`ResolveSlideBackgroundFill_BgRefResolution_DoesNotMutateThemeBgFillStyleList`,
`ResolveSlideBackgroundFill_BgRefIdxPastEndOfBgFillStyleList_ThrowsInvalidDataException`,
`ResolveSlideBackgroundFill_BgRefWithNoIdxAttribute_ThrowsInvalidDataException`,
`ResolveSlideBackgroundFill_BgRefWithNonNumericIdxAttribute_ThrowsInvalidDataException`,
`ResolveSlideBackgroundFill_BgRefWithNoColorChild_ResolvesEntryWithDefaultPhClr`,
`GetTheme_NoFmtScheme_BgFillStyleListIsEmpty`,
`GetTheme_FmtSchemeWithNoBgFillStyleLst_BgFillStyleListIsEmpty`,
`GetTheme_FmtSchemeWithBgFillStyleLst_ParsesEntriesInDocumentOrder`,
`PptxDocument_Render_SldBackgroundFixture_RendersAndPaintsSlideBackgroundFill`,
`PptxDocument_Render_Aiden0zImageCropCssResetFixture_PaintsVisibleContentAndBackgroundFill`

Proves the slide -> layout -> master `<p:bg>` precedence chain resolves exactly like the
established placeholder-property "first element present at all wins" inheritance rule, at both
the resolver level (`ResolveSlideBackgroundFill` called directly against hand-built `<p:bg>`
fragments) and the full, pixel-level `Render` integration level (a slide-level background paints
across the full surface; a layout-level background paints only when the slide itself declares
none; a master-level background paints only when neither the slide nor its layout declares one; a
higher tier's own background takes priority over a lower tier's even when both are present; an
empty-but-present `<p:bg/>` at a higher tier still wins, and - having neither `<p:bgPr>` nor
`<p:bgRef>` - throws `InvalidDataException` rather than silently falling through to the next
tier). Proves the resolved background paints *before* the shape-tree walk, so a shape's own fill
still paints on top of it unchanged, and proves that when none of slide/layout/master declare a
`<p:bg>` at all, rendering falls back unchanged to `PptxRenderOptions.BackgroundColor` exactly as
before this feature existed. Proves `<p:bgRef idx="…">` theme-indexed resolution across its full
ECMA-376 `CT_StyleMatrixReference` boundary: `idx` 0 and 1000 both resolve to no background
(`PptxNoFill`); `idx` 1-999 (the theme's `<a:fillStyleLst>` half of the style matrix) throws
`PptxUnsupportedFeatureException` with feature token `pptx-bg-fill-style-ref`; `idx` 1001 and
above resolves the theme's own `<a:fmtScheme>/<a:bgFillStyleLst>`, 0-based from that offset
(including its second, not just first, entry), substituting `<p:bgRef>`'s own color child for the
matched entry's `phClr` token (or, when `<p:bgRef>` itself declares no color child, falling back
to `ResolveColor`'s own pre-existing default); a missing/non-numeric `idx` and an out-of-range
`idx` (including against an empty `<a:bgFillStyleLst>`) both throw `InvalidDataException`. Proves
resolving a `<p:bgRef>` never mutates the theme's own cached, shared `BgFillStyleList` tree (a
regression test for the "wrap the matched entry in a synthetic parent, which clones rather than
reparents it" approach this resolver relies on - the theme is parsed once and reused across every
slide in a document). Proves `GetTheme` parses `<a:fmtScheme>/<a:bgFillStyleLst>` into
`PptxTheme.BgFillStyleList` in document order, tolerating an absent `<a:fmtScheme>` or an absent
`<a:bgFillStyleLst>` as an empty list rather than an error (the overwhelming majority of real
themes declare neither). Proves, against two independent real-world fixture files
(`pythonpptx-sld-background.pptx`, added specifically for this feature, and
`aiden0z-image-crop-css-reset.pptx`), that each file's own real, solid-color `<p:bg>` fill now
paints the exact declared color at the pixel level (`FF0000` and `FAFAF9` respectively) - a
pixel-level, not merely XML-resolution-level, regression guard against this feature's own
single highest-visual-impact motivation. A pattern or picture background fill is not covered by
this requirement - it is rejected with `PptxUnsupportedFeatureException`, inherited unchanged
from `ResolveFill`'s own pre-existing Phase 1c boundary (see
`CanvasNetPptx-PptxDocument-FillResolution`'s own verification above) - and a linear gradient
background fill is covered only indirectly, by the same `ResolveGradientFill` tests
`CanvasNetPptx-PptxDocument-GradientFill` already verifies, since `<p:bg>` resolution reuses that
pipeline verbatim rather than introducing a background-specific gradient path.

#### CanvasNetPptx-PptxDocument-MasterLayoutShapeRendering: Master/Layout Decorative Shape Rendering

**Tests**: `Render_MasterNonPlaceholderPicture_PaintsOnEverySlideUsingThatMasterResolvingOwnRels`,
`Render_LayoutNonPlaceholderAutoshape_PaintsOnlyOnSlidesUsingThatLayout`,
`Render_MasterAndSlideShapesOverlap_SlideShapePaintsOnTopOfMasterShape`,
`Render_MasterAndLayoutShapesOverlap_LayoutShapePaintsOnTopOfMasterShape`,
`Render_MasterPlaceholderShape_DoesNotRenderItsOwnPromptContent`,
`Render_LayoutPlaceholderShape_DoesNotRenderItsOwnPromptContent`,
`Render_MasterNonPlaceholderPictureWithUnsupportedFormat_SkipsThatShapeAndStillRendersSlideContent`,
`Render_SlideOwnPictureWithUnsupportedFormat_StillThrowsPptxUnsupportedFeatureException`,
`Render_MasterSvgOnlyLogoPicture_PaintsVisiblePixelsInsteadOfBeingSkipped`,
`PptxDocument_Render_Aiden0zChartAndComplexFixture_Slide0PaintsSlide1ThrowsUnsupportedFeature`

Proves, at the pixel level with deliberately contrasting colors so a wrong z-order or a missed
shape fails visibly rather than silently, that a slide master's own non-placeholder picture paints
on every slide that uses that master regardless of which layout the slide itself uses, and that
the picture resolves its own image relationship against its own owning part's own `.rels` file -
not the rendering slide's - by giving the slide's own `.rels` a deliberately conflicting,
wrong-colored `rId2` and confirming the master's own correct color still paints (a regression
guard for the `ownerPartPath`-threading fix this feature required). Proves a slide layout's own
non-placeholder autoshape paints only on a slide that uses that specific layout, not on a sibling
slide using a different layout backed by the same master. Proves the documented back-to-front
z-order at both boundaries: a slide's own shape paints on top of an overlapping master shape, and
a layout's own shape paints on top of an overlapping master shape. Proves a master's and a
layout's own placeholder shape is never rendered directly - its own "Click to edit…" prompt
content stays invisible - while its own non-placeholder sibling shape in the same shape tree still
renders, confirming the `skipPlaceholderShapes` flag discriminates correctly between a
`PptxSpShapeNode` with and without a non-null `Placeholder`. Proves, as a regression guard for a
graceful-skip containment fix, that a master's own non-placeholder picture shape referencing an
unsupported raster format (an EMF vector picture) is caught and skipped - rendering continues and
the slide's own content still paints normally over the full slide footprint - rather than
aborting the whole render; and, as the matching counter-proof that this containment is scoped
only to master/layout-owned shapes, that a **slide's own** picture shape referencing the same
unsupported raster format still hard-fails `Render` with `PptxUnsupportedFeatureException`,
exactly as before this containment was added. Proves, as a regression guard for the Phase 2
Follow-Up "SVG-Only Picture Blip Rendering" fix (mirroring the real-world "ERF IWF Breadboard Peer
Review.pptx" master-logo scenario), that a master-owned "SVG-only" `<p:pic>` (no raster
`r:embed`/`r:link` at all, only a Microsoft `<asvg:svgBlip r:embed="...">` extension referencing an
`.svg` media part) now actually paints its own rasterized pixels on every slide using that master,
instead of being silently caught and skipped by the same graceful-skip containment described
above - a dedicated before/after regression, since this exact shape previously fell into the
"caught and skipped" path for a different reason (an unsupported feature, not a malformed one).
Proves, against a real-world fixture
(`aiden0z-1-chart-and-complex.pptx`, whose own `slideLayout1.xml` declares a real, non-placeholder,
`userDrawn="1"` `<a:custGeom>` freeform shape named "Freeform 5"), that the custGeom-freeform-on-
layout dispatch path executes without throwing and resolves its own documented `bg1`-scheme fill
to a near-white pixel within its own bounding box (a tolerance, not an exact pixel match, because
the real custGeom path is a thin decorative flourish rather than a solid-filled rectangle, so most
of its own bounding box is unfilled background with anti-aliased edges) - this fixture cannot show
a dramatic visual contrast on its own, since its own `bg1` theme color and its own slide master
background both already resolve to the same pure white this freeform shape itself is filled, so
the synthetic tests above carry the primary, deliberately-contrasting-color proof instead.

#### CanvasNetPptx-PptxDocument-BulletProperties: Bullet/Numbering Property Parsing and Choice-Group Inheritance

**Tests**: `ParseParagraphProperties_Absent_ResolvesEmptyBulletProperties`,
`ParseBulletProperties_BuNone_CapturesTypeElement`,
`ParseBulletProperties_BuChar_CapturesTypeElement`,
`ParseBulletProperties_BuAutoNum_CapturesTypeElement`,
`ParseBulletProperties_ColorFontSizeModifiers_CapturesEachIndependently`,
`ParseBulletProperties_FollowTextModifiers_CapturesEachIndependently`,
`ResolveEffectiveParagraphProperties_NoBulletMarkupAnywhere_ResolvesNone`,
`ResolveEffectiveParagraphProperties_OwnBuChar_WinsOverEveryOtherTier`,
`ResolveEffectiveParagraphProperties_OwnBuNone_SuppressesInheritedBullet`,
`ResolveEffectiveParagraphProperties_PlaceholderLevelBuAutoNum_WinsWhenParagraphDeclaresNothing`,
`ResolveEffectiveParagraphProperties_MasterBodyStyleBuChar_WinsWhenEverythingAboveIsAbsent`,
`ResolveEffectiveParagraphProperties_BuAutoNumNoAttributes_ResolvesSchemaDefaults`,
`ResolveEffectiveParagraphProperties_OwnTypeOnly_InheritsColorFontSizeIndependently`,
`ResolveEffectiveParagraphProperties_BuClrTxOrAbsent_FollowsFirstRunColor`,
`ResolveEffectiveParagraphProperties_BuClrTxRunLessParagraph_FallsBackToDark1`,
`ResolveEffectiveParagraphProperties_BuFontTxOrAbsent_FollowsFirstRunFont`,
`ResolveEffectiveParagraphProperties_BuSzPct_ResolvesFractionOfFirstRunSize`,
`ResolveEffectiveParagraphProperties_BuSzPts_ResolvesAbsoluteSizeIndependentOfRunSize`,
`ResolveEffectiveParagraphProperties_SldNumDtFtrPlaceholderType_SuppressesMasterBodyStyleBullet`,
`ResolveEffectiveParagraphProperties_SldNumDtFtrPlaceholderTypeWithOwnBuChar_StillResolvesOwnBullet`

Proves raw parsing correctly captures each of the four OOXML bullet choice-groups (type: `buNone`/
`buAutoNum`/`buChar`; color: `buClrTx`/`buClr`; font: `buFontTx`/`buFont`; size: `buSzTx`/
`buSzPct`/`buSzPts`) as its own independent raw element, and that an absent `<a:pPr>` resolves the
shared `PptxRawBulletProperties.Empty` singleton. Proves the resolver's own four independent
inheritance chains: the paragraph's own type/color/font/size markup each wins over a placeholder-
or master-level equivalent; an explicit `<a:buNone>` at the paragraph's own tier suppresses an
inherited bullet entirely rather than merely failing to add one; a placeholder-level-only and a
master-`bodyStyle`-level-only bullet each resolve correctly when nothing above declares one; the
complete absence of bullet markup anywhere resolves the conservative `None` default; and, the
central inheritance-fidelity proof, that a paragraph may declare only its own bullet *type* while
independently inheriting color/font/size from the placeholder level style, proving the four
choice-groups are genuinely resolved independently rather than as one tier-wins-everything object.
Proves `<a:buAutoNum>`'s own schema defaults (`type="arabicPeriod"`, `startAt="1"`) when both
attributes are omitted. Proves the three "follow text" sentinels (`buClrTx`/`buFontTx`/`buSzTx`,
and their own absent-markup equivalent) resolve to the paragraph's own first run's effective
color/typeface/size (with a run-less paragraph's color falling back to the theme's `Dark1`), and
that `buSzPct`/`buSzPts` each correctly compute a relative-fraction-of-run-size versus an
absolute, run-size-independent size respectively. Proves `"sldNum"`/`"dt"`/`"ftr"` field
placeholder types resolve no bullet when the same master `bodyStyle` fixture that wins a bullet
for a `"body"`-typed placeholder is reused unchanged *and the paragraph itself declares no bullet
markup* - the regression guard for a stray-bullet defect found during visual QA
(`SelectMasterTextStyle` routes these three field placeholder types to `bodyStyle`, which
commonly declares a bullet, rather than the bullet-free `otherStyle` these types should consult in
genuine PowerPoint output; `ResolveEffectiveBulletProperties` now excludes only the master tier's
contribution to the TYPE choice-group for these three types). Proves, as the companion regression
guard for a narrower defect introduced and caught across two quality-retry cycles, that the same
three field placeholder types still resolve their own, explicitly-declared `<a:buChar>` override
correctly even when that same master `bodyStyle` bullet is present for the same level - an
own-paragraph explicit bullet choice always wins over any style-bucket default, regardless of
placeholder type, confirming the master-tier exclusion above is scoped to only the master tier
and does not disturb the paragraph's own markup.

#### CanvasNetPptx-PptxDocument-BulletRendering: Auto-Number Formatting, Gutter Positioning, and Minimum Gap

**Tests**: `FormatAutoNumber_SupportedTypes_FormatsExpectedString`,
`FormatAutoNumber_UnsupportedType_ReturnsNull`,
`ResolveTextLayout_NonBulletedParagraph_FirstLineIndentStillAppliesToTextX`,
`ResolveTextLayout_BulletedParagraph_TextStartsAtMarLGutterHoldsBullet`,
`ResolveTextLayout_BuNone_PaintsNoGlyphBeyondRunText`,
`ResolveTextLayout_ConsecutiveAutoNumParagraphs_SequencesCounterAcrossParagraphs`,
`ResolveTextLayout_NestedThenReturnToShallowerLevel_ResumesShallowerCounterRestartsDeeperLevel`,
`ResolveTextLayout_UnsupportedAutoNumType_SkipsOnlyThatBulletGracefully`,
`PaintTextLayout_BulletedParagraph_PaintsBulletGlyphToSurface`,
`BuildLines_ParagraphWithBulletPropertiesAndNoRuns_PaintsNoBulletGlyph`,
`BuildLines_OtherwiseIdenticalParagraphWithOneRun_StillPaintsBulletGlyph`,
`BuildLines_RunlessAutoNumberParagraph_CounterStillAdvancesForSubsequentParagraph`,
`PptxDocument_Render_Aiden0zChartAndComplexFixture_Slide0PaintsSlide1ThrowsUnsupportedFeature`,
`ResolveTextLayout_BulletedParagraphWithZeroIndent_TextClearsBulletWidth`,
`PaintTextLayout_BulletedParagraphWithZeroIndent_BulletAndTextInkDoNotOverlap`,
`ResolveTextLayout_MasterBuCharDefault_OwnBuAutoNumOverride_PaintsOnlyAutoNumberMarker`,
`ResolveTextLayout_BulletedParagraphWithZeroIndent_AutoNumTwoDigitMarker_TextClearsBulletWidthPlusGap`,
`PaintTextLayout_BulletedParagraphWithZeroIndent_AutoNumMarker_BulletAndTextHaveVisibleGap`

Proves `FormatAutoNumber` formats all eleven supported `ST_TextAutonumberScheme` values at
representative values (including alphabetic base-26 rollover at value 27 and several Roman-numeral
edge cases: 4, 9, and 1994), and gracefully returns `null` (not an exception) for an unrecognized
scheme. Proves, by contrast with its own regression case, the hanging-indent gutter fix: a
non-bulleted paragraph's first-line text still renders at the pre-existing `marL+indent` position
exactly as before this feature, while a bulleted paragraph's own first-line text instead renders
flush at `marL`, with its bullet glyph painted separately at the `marL+indent` gutter - two
glyphs, at two distinct, independently-verified X positions. Proves an explicit `<a:buNone>`
paragraph paints no bullet glyph at all (only its own run text), and that the bullet-less
hanging-indent fix does not apply to it, unlike a resolved bullet. Proves consecutive same-level
`<a:buAutoNum>` paragraphs sequence their own rendered digit glyph in order (by asserting each
paragraph's own bullet glyph index, not merely its count), and that returning to a shallower
indent level after a nested, deeper auto-numbered sub-list resumes the shallower level's own
counter undisturbed while the (now-closed) deeper level restarts from its own `startAt` the next
time it is used - the full per-level reset/resume state machine. Proves an unrecognized
`<a:buAutoNum>` scheme skips painting only that one paragraph's own bullet glyph, with its own run
text, and the render as a whole, otherwise unaffected - the graceful per-bullet degradation policy.
Proves a resolved bullet glyph is actually painted to a pixel `Surface`, not merely resolved and
positioned, via `PaintTextLayout`. Finally, against the real-world `aiden0z-1-chart-and-complex.pptx`
fixture's own "Rectangle 5" shape (two consecutive `<a:buChar char="•">`-bulleted paragraphs whose
own `marL+indent` geometry collapses exactly to the shape's own left inset), proves visible ink
paints in that shape's own bullet-gutter pixel column, strictly left of where any paragraph text
itself can start, and meaningfully darker than a column sampled just inside that same inset - the
real-file, end-to-end visual-fidelity proof this feature's acceptance bar requires. A further
assertion against this same fixture proves no stray bullet ink appears anywhere within this
fixture's own slide-number placeholder's resolved geometry (inherited by type-match from the
slide master's own "Slide Number Placeholder 5": `off x="11669529" y="6400800"`,
`ext cx="438912" cy="155448"`), comparing the darkest pixel found there against a same-column
background baseline row just above it rather than an absolute threshold - the exact real-world
location and failure mode the stray-bullet defect above was found and closed at. Finally, proves
the run-less/spacer-paragraph bullet-suppression fix: a paragraph with resolved `<a:buChar>`
bullet properties but no `<a:r>` run children (only a synthetic `<a:endParaRPr>`) paints no glyphs
at all - neither run text nor a bullet glyph; the otherwise-identical paragraph with one run still
paints both its own run glyph and its bullet glyph (the differential counterpart); and a run-less
auto-numbered paragraph followed by a same-level paragraph with a run proves the counter still
advanced past the suppressed paragraph (the second paragraph's bullet renders "2", not "1"),
confirming only the glyph is suppressed, never the counter state. Proves the bullet-gutter/
text-start-X collision fix: `ResolveTextLayout_BulletedParagraphWithZeroIndent_TextClearsBulletWidth`
reproduces a real-world slide's exact attribute set (`marL="320040"`, `lvl="1"`, no `indent`
attribute, `<a:buAutoNum type="arabicPeriod"/>`, no placeholder/master tier contributing an
indent) and asserts, with exact numeric values, that the bullet glyph's own X is unchanged (still
the unclamped gutter) while the paragraph's own first run-text glyph's X is now clamped to clear
the bullet string's own measured width - directly disproving the pre-fix collision
(`bulletGlyphX == textGlyphX`) this defect was confirmed to produce.
`PaintTextLayout_BulletedParagraphWithZeroIndent_BulletAndTextInkDoNotOverlap` proves this at the
pixel level: rendered to a `Surface`, the bullet's own ink and the paragraph's own (now correctly
offset) text ink occupy disjoint X-pixel columns, with no shared range. Finally,
`ResolveTextLayout_MasterBuCharDefault_OwnBuAutoNumOverride_PaintsOnlyAutoNumberMarker` is the
permanent regression guard for the duplicate-paint/type-precedence-merge hypothesis this defect's
investigation refuted: with a master `<p:bodyStyle>` level declaring a default `<a:buChar>` and
the slide paragraph's own `<a:pPr>` declaring `<a:buAutoNum>`, the resolved bullet is exactly
`AutoNum` with a `null` `Character` (the master's `buChar` fully superseded, not merged), and the
full `ResolveTextLayout` pipeline emits exactly one run-glyph set plus exactly one bullet-glyph
set - never two markers. The pre-existing
`ResolveTextLayout_BulletedParagraph_TextStartsAtMarLGutterHoldsBullet` and
`ResolveTextLayout_BuNone_PaintsNoGlyphBeyondRunText` tests continue to pass unchanged, confirming
the fix's clamp is a no-op for the already-correct, sufficiently-negative-indent case.

A follow-up real-world report found that same clamp, while correct for `buChar`, still
insufficient for `buAutoNum`: CanvasNet rendered an auto-numbered marker immediately touching the
paragraph's own first word (e.g. `"1.3 custom tip geometries"`), whereas real PowerPoint shows a
clear, word-space-sized gap after the marker. The updated
`ResolveTextLayout_BulletedParagraphWithZeroIndent_TextClearsBulletWidth` now asserts the clamped
text-start-X includes both the bullet string's own measured width and a `buAutoNum`-only minimum
trailing gap (one space character's own advance width). The new
`ResolveTextLayout_BulletedParagraphWithZeroIndent_AutoNumTwoDigitMarker_TextClearsBulletWidthPlusGap`
proves this gap is additive to a genuinely multi-character marker's own full measured width (using
`startAt="12"` so the marker renders as `"12."`, three glyphs wide) rather than a fixed constant
that would understate the clamp for longer markers, and uses a digit-first run text (`"3"`)
reproducing the exact real-world ambiguous "marker digit immediately followed by the paragraph's
own digit" pattern. The new
`PaintTextLayout_BulletedParagraphWithZeroIndent_AutoNumMarker_BulletAndTextHaveVisibleGap` proves
this at the pixel level: rendered to a `Surface`, there is no ink anywhere at or before the
pre-fix "touching" boundary, no ink anywhere within the new gap region itself, and real ink only
once comfortably past the clamped text-start-X - a genuinely blank gap, not merely "no overlap".
This gap is gated to `PptxBulletKind.AutoNum` only, so the pre-existing
`ResolveTextLayout_BulletedParagraph_TextStartsAtMarLGutterHoldsBullet`,
`ResolveTextLayout_BuNone_PaintsNoGlyphBeyondRunText`, and
`PaintTextLayout_BulletedParagraphWithZeroIndent_BulletAndTextInkDoNotOverlap` (a `buChar` test)
all continue to pass unmodified, confirming the fix introduces zero regression to the
already-correct, already-verified `buChar` "touching, zero extra gap" behavior.

#### CanvasNetPptx-PptxDocument-ColorMapResolution: `<p:clrMap>`/`<p:clrMapOvr>` Indirection

**Tests**: `ResolveSchemeColor_DefaultColorMap_Tx1ResolvesToDark1`,
`ResolveSchemeColor_NonIdentityColorMap_Tx1ResolvesToOverriddenSlot`,
`ResolveColor_SchemeClrWithNonIdentityColorMap_AppliesIndirectionBeforeTransforms`,
`GetMaster_ParsesRequiredClrMap`, `GetMaster_MissingClrMap_ThrowsInvalidDataException`,
`GetLayout_ParsesOptionalClrMapOvr_DefaultsToNullWhenAbsent`,
`GetLayout_ParsesOptionalClrMapOvr_RetainsElementWhenPresent`,
`GetSlide_ParsesOptionalClrMapOvr_DefaultsToNullWhenAbsent`,
`ResolveEffectiveColorMap_NoOverridesAnywhere_ReturnsMasterColorMap`,
`ResolveEffectiveColorMap_LayoutOverrideClrMapping_ReturnsLayoutOverride`,
`ResolveEffectiveColorMap_SlideOverrideClrMapping_ReturnsSlideOverrideEvenWhenLayoutAlsoOverrides`,
`ResolveEffectiveColorMap_SlideMasterClrMapping_FallsThroughToLayoutOverride`,
`ResolveEffectiveColorMap_SlideAndLayoutMasterClrMapping_FallsThroughToMasterColorMap`,
`ResolveEffectiveColorMap_OverrideClrMappingMissingRequiredAttribute_ThrowsInvalidDataException`,
`Render_SlideClrMapOvrInvertsBg1_SchemeClrFillResolvesToOverriddenSlotNotHardcodedAlias`,
`Render_NoClrMapOvr_SchemeClrFillResolvesToMasterColorMapDefault`,
`GetSlide_TableCellSchemeColorFillWithSlideClrMapOvr_UsesEffectiveColorMapNotDefault`

Proves `bg1`/`tx1`/`bg2`/`tx2` scheme-color resolution consults the slide master's own
`<p:clrMap>` (and any layout/slide `<p:clrMapOvr>`) rather than a hard-coded `Light1`/`Dark1`/
`Light2`/`Dark2` mapping: the default, identity color map still resolves `tx1` to `Dark1`
(preserving this unit's pre-existing baseline behavior), while a non-identity color map
redirects `tx1` to whatever slot it names instead, with the indirection applied before any
`<a:lumMod>`/`<a:lumOff>`-style color transform is layered on top. Proves `GetMaster` parses a
slide master's own required `<p:clrMap>` into `PptxMaster.ColorMap`, throwing
`InvalidDataException` when the element (or any of its four required attributes) is missing - a
genuine ECMA-376 schema violation, not a tolerable omission. Proves `GetLayout`/`GetSlide` each
parse an optional `<p:clrMapOvr>` into `PptxLayout.ClrMapOvr`/`PptxSlide.ClrMapOvr` as a raw,
unparsed element when present, and `null` when absent. Proves `ResolveEffectiveColorMap`'s full
slide -> layout -> master fallback chain: with no override anywhere, the master's own color map
applies; a layout-level `<a:overrideClrMapping>` wins over the master; a slide-level
`<a:overrideClrMapping>` wins over both the layout and the master even when the layout also
declares an override; an `<a:masterClrMapping/>` marker at the slide tier falls through to the
layout's own override; and the same marker at both the slide and layout tiers falls all the way
through to the master's own color map. Proves a malformed `<a:overrideClrMapping>` missing a
required attribute throws `InvalidDataException` rather than silently substituting a default.
Finally, proves the fix end-to-end at the render/pixel level: a slide-level `<p:clrMapOvr>/
<a:overrideClrMapping bg1="dk1" .../>` makes a shape filled with `<a:schemeClr val="bg1"/>`
actually paint the theme's `Dark1` pixel color, not the previously hard-coded `Light1`, while a
companion test confirms the pre-existing, no-override baseline still renders `Light1` unchanged -
the real-world, visual-fidelity proof this fix's acceptance bar requires. Proves (Review
Follow-Up), at the `GetSlide` integration level, that a table cell's own fill color is no longer
always resolved against the default color map for a slide-owned table: a slide declaring a
`<p:clrMapOvr>/<a:overrideClrMapping bg1="dk1" .../>` and a table cell filled with
`<a:schemeClr val="bg1"/>` resolves that cell's fill to the theme's `Dark1` color via `GetSlide`
itself (not a direct `ParseTable` call bypassing the real inheritance chain), proving `GetSlide`'s
new lazy `colorMapResolver` is correctly threaded through `ParseShapeTree` into `ParseTable`. A
narrower, documented, accepted residual limitation remains for a **master- or layout-owned**
table only (not separately tested as a defect, since `GetMaster`/`GetLayout`'s own part caches are
shared across every consuming slide and cannot soundly bake in any one slide's effective color
map): such a table cell's own fill/border color is still resolved once, eagerly, at parse time,
and therefore still always effectively uses the default color map regardless of any real override
in effect for a particular consuming slide, while a table cell's own text color is resolved at
paint time and correctly reflects the real, per-render effective color map like any other text
run, for tables owned by any tier.

#### CanvasNetPptx-PptxDocument-ShapeStyleReference: `<p:style>` `fillRef`/`lnRef` Shape-Style Resolution

**Tests**: `ResolveShapeStyleFill_NullStyleElement_ReturnsNoFill`,
`ResolveShapeStyleFill_NoFillRef_ReturnsNoFill`, `ResolveShapeStyleFill_IdxZero_ReturnsNoFill`,
`ResolveShapeStyleFill_FillRefIdxOne_ResolvesFillStyleListEntryZeroNoOffset`,
`ResolveShapeStyleFill_FillRefWithSchemeClrPhClr_SubstitutesFillRefOwnColor`,
`ResolveShapeStyleFill_IdxOutOfRange_ThrowsInvalidDataException`,
`ResolveShapeStyleFill_IdxPastEndOfEmptyFillStyleList_ThrowsInvalidDataException`,
`ResolveShapeStyleFill_FillRefWithNoIdxAttribute_ThrowsInvalidDataException`,
`ResolveShapeStyleLineStyle_NullStyleElement_ReturnsNull`,
`ResolveShapeStyleLineStyle_NoLnRef_ReturnsNull`, `ResolveShapeStyleLineStyle_IdxZero_ReturnsNull`,
`ResolveShapeStyleLineStyle_LnRefIdxOne_ResolvesLnStyleListEntryZeroNoOffset`,
`ResolveShapeStyleLineStyle_LnRefWithSchemeClrPhClr_SubstitutesLnRefOwnColor`,
`ResolveShapeStyleLineStyle_IdxOutOfRange_ThrowsInvalidDataException`,
`ResolveShapeStyleLineStyle_LnRefWithNoIdxAttribute_ThrowsInvalidDataException`,
`GetTheme_NoFmtScheme_FillStyleListAndLnStyleListDefaultToEmpty`,
`GetTheme_FmtSchemeWithNoFillStyleLstOrLnStyleLst_BothDefaultToEmpty`,
`GetTheme_FmtSchemeWithFillStyleLstAndLnStyleLst_ParsesEntriesInDocumentOrder`,
`Render_ShapeWithOnlyStyleFillRefNoExplicitSpPrFill_RendersResolvedTintedAccentColor`,
`Render_ShapeWithBothExplicitSpPrFillAndStyleFillRef_ExplicitFillWins`,
`Render_ShapeWithOnlyStyleLnRefNoExplicitLn_RendersResolvedStrokeColor`,
`Render_ShapeWithExplicitLnNoFillAndStyleLnRef_ExplicitLnWins`,
`ResolveShapeLineStyle_LnElementNull_DefersToStyleLineStyle`,
`ResolveShapeLineStyle_LnHasExplicitSolidFill_OwnFillWinsOverStyle`,
`ResolveShapeLineStyle_LnHasExplicitNoFill_ResolvesNullRegardlessOfStyle`,
`ResolveShapeLineStyle_LnHasWidthButNoFillChild_KeepsOwnWidthUsesStyleColor`,
`ResolveShapeLineStyle_LnHasWidthAndOwnDashButNoFillChild_KeepsOwnDashArray`,
`ResolveShapeLineStyle_LnHasWidthButNoFillChildAndNoStyleElement_ResolvesNullDefault`,
`ResolveShapeLineStyle_LnHasWidthButNoFillChildAndStyleLnRefIdxZero_ResolvesNullDefault`,
`ResolveShapeLineStyle_LnHasNoWidthAndNoFillChild_ResolvesNullRegardlessOfStyle`,
`Render_ShapeWithLnWidthOnlyNoFillChildAndStyleLnRef_UsesOwnWidthAndStyleColor`

Proves a shape built from PowerPoint's "Shape Styles" gallery - which declares its fill/line
purely via `<p:spPr>`'s sibling `<p:style>/<a:fillRef idx="N">`/`<a:lnRef idx="N">`, indexing the
theme's own `<a:fmtScheme>/<a:fillStyleLst>`/`<a:lnStyleLst>` - resolves its fill/line correctly,
where it previously rendered with neither at all. Proves `GetTheme` parses
`<a:fmtScheme>/<a:fillStyleLst>`/`<a:lnStyleLst>` into `PptxTheme.FillStyleList`/`LnStyleList`,
defaulting to an empty list when either (or the whole `<a:fmtScheme>`) is absent, and preserving
document order when present. Proves `ResolveShapeStyleFill`/`ResolveShapeStyleLineStyle` return
"no fill"/`null` (respectively) for a `null` style element, an absent `fillRef`/`lnRef`, or an
explicit `idx="0"` (the schema-defined "none" sentinel). Proves the no-offset indexing directly,
pinning `idx="1"` to `FillStyleList`/`LnStyleList`'s entry `0` - not entry `1`, and not a
`<p:bgRef>`-style `+1000`-offset entry. Proves the ref's own color-definition child substitutes
for the matched style-list entry's own `<a:schemeClr val="phClr"/>` token. Proves an `idx` outside
`[1,3]`, an `idx` indexing past the end of an empty list, or a missing/non-numeric `idx`
attribute, each throw `InvalidDataException` rather than being silently clamped or defaulted.
Proves the fill side end-to-end at the render/pixel level: a shape with only a
`<p:style>/<a:fillRef>` (no explicit `<p:spPr>` fill at all) paints the resolved,
phClr-substituted style-list fill color; an explicit `<p:spPr>` fill on the same shape wins over
a simultaneously-present `<a:fillRef>`.

For the line side specifically, proves the corrected 4-case precedence implemented by
`ResolveShapeLineStyle`: (1) an explicit fill-definition child on the shape's own `<a:ln>` (for
example `<a:solidFill>`) wins outright over a visible style `<a:lnRef>`, resolving to the
`<a:ln>`'s own color/width; (2) an explicit `<a:ln><a:noFill/></a:ln>` resolves to "no stroke"
regardless of a visible style `<a:lnRef>`, remaining distinguishable from case 3; (3) a present
`<a:ln>` declaring a width but no recognized fill-definition child of its own (the exact reported
construct, `<a:ln w="76200"/>`) keeps its own width and dash verbatim while adopting the style
`<a:lnRef>`'s own resolved color - and resolves to "no stroke" (not an invented default color)
when no `<p:style>` element is present at all, and separately when its own `<a:lnRef idx="0"/>`
resolves to "no line"; and (4) a fully absent `<a:ln>` defers entirely to the style `<a:lnRef>`
for both width and color (`ResolveShapeLineStyle_LnElementNull_DefersToStyleLineStyle` proves this
resolves identically to calling `ResolveShapeStyleLineStyle` directly). A further edge-case test
proves a fill-less, width-less `<a:ln/>` (no `w` attribute at all) still resolves to "no stroke"
regardless of a visible style `<a:lnRef>` - width is never pulled from style for an ordinary
shape's own line, unlike `ResolveConnectorLineStyle`'s broader per-attribute merge for connectors.
Finally, `Render_ShapeWithLnWidthOnlyNoFillChildAndStyleLnRef_UsesOwnWidthAndStyleColor` proves
case 3 end-to-end at the render/pixel level, reproducing the exact reported construct alongside a
style `<a:lnRef>` resolving to a deliberately distinct, much wider line-style-list entry: the
painted stroke is the style's own color, sampled directly on the shape's own edge, and a second
sample point - outside the shape's own (narrow) stroke band but well inside where the style's own
(much wider) stroke band would have reached - remains unpainted, proving width is the shape's own
and never merged in from the style. Together these are the real-world, visual-fidelity proof this
fix's acceptance bar requires. Documented, accepted limitations (not separately tested as
defects): `<a:fontRef>` and `<a:effectRef>` are out of scope (different resolution
mechanisms/unimplemented features entirely, not an extension of this fix's pattern), and a
placeholder shape's own `<p:style>` is never inherited from its matched layout/master placeholder -
only a shape's own, directly-declared `<p:style>` is consulted.

#### CanvasNetPptx-PptxDocument-ConnectorRendering: `<p:cxnSp>` Connector Shape Rendering

**Tests**: `ParseShapeTree_Connector_ProducesConnectorShapeNode`,
`PptxPresetGeometry_Build_StraightConnectorPresets_ProducesOpenDiagonal`,
`PptxPresetGeometry_Build_StraightConnectorWithZeroWidthOrHeight_StillProducesLine`,
`PptxPresetGeometry_Build_OrdinaryPresetWithZeroHeight_StillDegradesToEmpty`,
`PptxPresetGeometry_Build_BentConnectorPresets_ReachesEndpointAsOpenPolyline`,
`PptxPresetGeometry_Build_CurvedConnectorPresets_ReachesEndpoint`,
`ResolveShapeFrame_StraightConnectorWithFlipCombination_MapsEndpointsCorrectly`,
`ResolveConnectorLineStyle_OwnLnExplicitNoFill_AlwaysWinsOverStyle`,
`ResolveConnectorLineStyle_OwnLnHasNoWidth_FallsBackToStyleWidth`,
`ResolveConnectorLineStyle_OwnLnHasWidthButNoFill_FallsBackToStylePaint`,
`ResolveConnectorLineStyle_OwnLnHasExplicitWidthAndFill_OwnValuesWin`,
`ResolveConnectorLineStyle_NoOwnWidthAndNoStyle_ResolvesToNull`,
`ResolveConnectorLineStyle_OwnLnHasExplicitDash_UsesOwnDashArray`,
`ResolveArrowhead_RecognizedTypeValue_ResolvesExpectedKind`,
`ResolveArrowhead_NoLnElement_ReturnsNull`, `ResolveArrowhead_NoMatchingEndElement_ReturnsNull`,
`ResolveArrowhead_NoneOrUnrecognizedType_ReturnsNull`,
`ResolveArrowhead_NoWOrLenAttributes_DefaultsToMed`,
`PptxArrowheadGeometry_Build_FilledKinds_TipAtOriginBodyExtendsBackward`,
`PptxArrowheadGeometry_Build_Oval_IsClosedAndCenteredNearOrigin`,
`PptxArrowheadGeometry_Build_Arrow_IsOpenChevron`,
`PptxArrowheadGeometry_Build_NonPositiveLineWidth_ReturnsEmpty`,
`PptxArrowheadGeometry_Build_SizeKeys_LargeIsBiggerThanSmall`,
`ComputeEndpointsAndTangents_HorizontalStraightConnector_TangentsPointAlongPositiveX`,
`ComputeEndpointsAndTangents_VerticalStraightConnector_TangentsPointAlongPositiveY`,
`ComputeEndpointsAndTangents_DiagonalStraightConnector_TangentsPointAtFortyFiveDegrees`,
`ComputeEndpointsAndTangents_BentConnector_FinalSegmentTangentPointsAlongLastDirection`,
`Render_StraightConnector_PaintsDiagonalLineAtExpectedPixels`,
`Render_ZeroHeightHorizontalConnector_PaintsLineWithoutDegradingToEmpty`,
`Render_ConnectorWithTailArrowhead_PaintsArrowheadNearEndpoint`,
`PptxDocument_Render_ShpConnectorPropsFixture_ConnectorsPaintVisibleLines`

Proves `ParseShapeTree` dispatches a `<p:cxnSp>` to its own `PptxConnectorShapeNode` in the same
document-order tree walk as every other shape kind (so z-order among siblings, and group-nested
transform composition, apply identically) - closing the gap where a connector was previously
never represented in the parsed shape tree at all. Proves `PptxPresetGeometry.Build` resolves
`line`/`straightConnector1` as a plain open two-point diagonal path across `flipH`/`flipV`/both/
neither transform combinations, proves a connector preset with a zero width and/or zero height
still builds its line (the real-world bug fix, confirmed necessary by the
`pythonpptx-shp-connector-props.pptx` fixture's own zero-height connector) while an ordinary,
non-connector preset given the same zero-size input still correctly degrades to `Path.Empty`
(proving the bypass is scoped to connector presets only, not a general relaxation), and proves
`bentConnectorN`/`curvedConnectorN` elbow geometry reaches both of its own declared endpoints.
Proves `ResolveConnectorLineStyle`'s per-attribute merge: a connector's own fully-specified
`<a:ln>` wins outright; an `<a:ln>` declaring neither width nor fill falls back to its sibling
`<p:style>/<a:lnRef>` for both; an explicit `<a:noFill/>` on the connector's own `<a:ln>` always
wins over the style reference regardless; a connector's own dash style wins over any style
fallback; and a connector with neither its own `<a:ln>` nor a `<p:style>/<a:lnRef>` resolves to no
line style at all. Proves `ResolveArrowhead` resolves each recognized `type` value
(`triangle`/`stealth`/`diamond`/`oval`/`arrow`) to its matching `PptxArrowheadKind` carrying
through its `w`/`len` size keys, returns no arrowhead for an absent `<a:ln>`, an absent matching
`<a:headEnd>`/`<a:tailEnd>` child, or an explicit `none`/unrecognized `type` value, and defaults
absent `w`/`len` attributes to `"med"`. Proves `PptxArrowheadGeometry.Build` places every filled
kind's tip at the local origin with its body extending backward, builds `oval` as a closed
ellipse and `arrow` as an open (not filled) chevron, returns `Path.Empty` for a non-positive line
width, and scales strictly larger for the `"lg"` size key than for `"sm"`. Proves
`ComputeEndpointsAndTangents` resolves the correct start/end points and tangent directions for
horizontal, vertical, diagonal, and bent connector geometries. Finally, proves the fix end-to-end
at the render/pixel level: a straight connector paints its diagonal line at the expected pixel
positions; a zero-height connector still paints its horizontal line (the bug-fix proof, now at
the render level); a connector with a `tailEnd type="triangle"` arrowhead paints ink well outside
the plain line's own stroke width, flared out just above its tip at the connector's own endpoint;
and the real `pythonpptx-shp-connector-props.pptx` fixture - whose one connector relies entirely
on its `<p:style>/<a:lnRef idx="2">` for its own visible color, with no fill/width declared on
its own bare `<a:ln>` - now paints visible content on both of its slides, where previously only
slide 0's dimensions were asserted and no pixel content was checked at all. Documented, accepted
limitations (not separately tested as defects): only `line`/`straightConnector1` and the four
`bentConnectorN`/four `curvedConnectorN` presets are implemented (any other connector preset name
degrades to "this one connector paints nothing", not a crash); the arrowhead size-keyword-to-EMU
mapping is a documented visual approximation, not derived from a published PowerPoint-internal
constant table; and connection-site (`<a:stCxn>`/`<a:endCxn>`) auto-routing is out of scope - a
connector always renders at its own last-saved, static `<a:xfrm>` position.

#### CanvasNetPptx-PptxDocument-UnderlineRendering: `<a:rPr u="…">` Underline Style/Color Resolution and Rendering

**Tests**: `ResolveEffectiveRunProperties_NoneDeclared_ResolvesHardCodedDefaults`,
`ResolveEffectiveRunProperties_UnderlineAttribute_ResolvesExpectedStyle`,
`ResolveEffectiveRunProperties_ExplicitUFill_OverridesTextColor`,
`ResolveEffectiveRunProperties_NoUnderlineFillMarkup_DefaultsUnderlineColorToTextColor`,
`ResolveEffectiveRunProperties_ExplicitUFillTx_DefaultsUnderlineColorToTextColor`,
`ResolveTextLayout_UnderlinedRun_EmitsOneSegmentSpanningRunWidth`,
`ResolveTextLayout_NonUnderlinedRun_EmitsNoSegment`,
`ResolveTextLayout_TwoRunsOnlyFirstUnderlined_EmitsExactlyOneSegment`,
`ResolveTextLayout_UnderlinedRunWithInteriorSpace_SpanCoversSpace`,
`PaintTextLayout_SingleUnderlineSegment_PaintsStrokeBelowBaselineAtExpectedPositionAndColor`,
`PaintTextLayout_NoUnderlineSegments_PaintsNoExtraInk`,
`PaintTextLayout_DoubleUnderlineSegment_DoesNotThrowAndPaintsInk`,
`PaintTextLayout_OtherUnderlineSegment_DoesNotThrowAndPaintsInk`

Proves `ResolveEffectiveRunProperties` resolves a run with no underline markup declared anywhere
in its own inheritance chain to the hard-coded default `PptxUnderlineStyle.None` (closing the
regression where an underlined run previously rendered with no underline at all), and proves its
own `u` attribute resolution: `"sng"` resolves to `Single`, `"none"` resolves to `None`, `"dbl"`
resolves to `Double`, and both a recognized-but-less-common variant (`"wavy"`) and an entirely
unrecognized value (`"heavy"`) both resolve to `Other` - proving every ECMA-376 variant this phase
does not render distinctly still resolves to a defined, intentional single-solid-line rendering
rather than crashing or silently dropping the underline again. Proves the underline's own color
resolves independently of the run's own text-fill color: an explicit `<a:uFill>/<a:solidFill>`
overrides the text color; no `<a:uFill>` child at all defaults the underline color to the run's
own already-resolved text color; and an explicit `<a:uFillTx/>` (the "inherit text color" marker)
also defaults to the text color, proving it is not misinterpreted as "no color". Proves
`ResolveTextLayout` emits exactly one `PptxUnderlineSegment` spanning an underlined run's own
measured glyph width, with `SizeEmu`/`Color` matching that run's own resolved properties; proves
a non-underlined run emits no segment at all; proves that when only the first of two runs on one
line is underlined, exactly one segment is emitted, spanning only that first run's own width (the
span correctly flushes at the run boundary rather than bridging into the second, non-underlined
run); and proves a single underlined run containing an interior space emits one segment spanning
the full run width including that interior space (the span does not stop at the first word).
Finally, proves `PaintTextLayout` paints a visible, solid-filled stroke below the baseline at the
segment's own expected position and color for a `Single`-style segment (with no ink painted above
the baseline or outside the segment's own horizontal range), proves a layout with no underline
segments at all paints no extra ink anywhere (a regression guard against a null/default
`Underlines` list throwing or painting unexpectedly), and proves both `Double`- and `Other`-style
segments paint without throwing. Documented, accepted limitation (not separately tested as a
defect): underline thickness/offset are computed as a pragmatic, `SizeEmu`-proportional
approximation, not derived from the target font's own OpenType `post` table
`underlinePosition`/`underlineThickness` fields - true font-metric-derived values are deferred to
a later phase.

#### CanvasNetPptx-PptxDocument-PictureGeometryClipping: `<p:pic>` Preset/Custom-Geometry Clipping

**Tests**: `ResolvePictureClipPath_RectPreset_ReturnsNull`,
`ResolvePictureClipPath_NoGeometryElement_ReturnsNull`,
`ResolvePictureClipPath_EllipsePreset_ReturnsNonNullPath`,
`ResolvePictureClipPath_RoundRectPreset_ReturnsNonNullPath`,
`ResolvePictureClipPath_CustGeom_ReturnsNonNullPath`,
`ResolvePictureClipPath_PrstGeomMissingPrstAttribute_ThrowsInvalidDataException`,
`ResolvePictureClipPath_UnsupportedPreset_ThrowsPptxUnsupportedFeatureException`,
`ResolvePictureClipPath_NullSpPrElement_ThrowsArgumentNullException`,
`Render_PictureEllipseGeometry_ClipsImageToEllipticalRegion`,
`Render_PictureRectGeometry_PaintsFullBoundingBoxUnclipped`,
`Render_PictureNoPrstGeom_PaintsFullBoundingBoxUnclipped`,
`Render_PictureRoundRectGeometry_ClipsImageCorners`,
`Render_PictureEllipseGeometry_NearSquareRealWorldAspect_ClipsAllFourBoundingBoxCorners`

Proves `ResolvePictureClipPath` returns `null` (meaning "no clip - paint the original unclipped
full bounding-box rectangle") for a `<p:spPr>` that explicitly declares `<a:prstGeom prst="rect">`
and, separately, for a `<p:spPr>` that declares no `<a:prstGeom>`/`<a:custGeom>` at all -
deliberately more tolerant than `ResolveShapeGeometry`'s own all-or-nothing auto-shape contract,
since a geometry-less `<p:pic>` is schema-valid and historically unclipped, not malformed.
Proves an `ellipse` preset, a `roundRect` preset, and an explicit `<a:custGeom>` each resolve to a
non-null `Path`, dispatching to the exact same `PptxPresetGeometry.Build`/`ResolveCustomGeometry`
resolvers an auto-shape's own `ResolveShapeGeometry` already uses - not a second, divergent
geometry resolver. Proves a `<a:prstGeom>` missing its own `prst` attribute throws
`InvalidDataException`, an unsupported preset name propagates `PptxUnsupportedFeatureException`
unchanged, and a `null` `spPrElement` argument throws `ArgumentNullException` - the identical
fail-closed contract `ResolveShapeGeometry` already carries, applied consistently to pictures.

End-to-end, render-level, proves a `<p:pic>` whose `<p:spPr>` declares `<a:prstGeom prst="ellipse">`
clips the painted image to the elliptical region described by its own bounding box (a bounding-box
corner remains unpainted/background while the shape's own center paints the image's content) -
the confirmed real-world defect this fix closes. Proves, as explicit no-regression guards, that a
`<p:pic>` with an explicit `<a:prstGeom prst="rect">` and a `<p:pic>` with no `<a:prstGeom>`/
`<a:custGeom>` at all both continue painting the full bounding-box rectangle unclipped, including
its own corners - the identical behavior this package always produced before this fix. Proves a
`<p:pic>` with `<a:prstGeom prst="roundRect">` clips its own bounding-box corners (background
visible at the rounded-corner arc) while its own center remains fully painted, confirming the fix
generalizes correctly beyond the single `ellipse` preset to any non-`rect` preset. A
non-permanent visual-verification generator (`GeneratePictureEllipseClipReproPng`) renders the
same ellipse-clipped fixture end-to-end through the public `PptxDocument.Render` API and saves it
to `.agent-logs/pptx-picture-ellipse-clip-repro.png`, allowing a human reviewer to directly confirm
the circular photo-crop effect by eye. Documented, accepted limitation (not separately tested as a
defect): the clip mask's own anti-aliased edge inherits `PathFiller.Fill`'s default flatten
tolerance and `PptxPresetGeometry`'s own fixed-segment-count curve approximation - the same
characteristic every other preset-geometry shape fill in this package already carries.
`Render_PictureEllipseGeometry_NearSquareRealWorldAspect_ClipsAllFourBoundingBoxCorners` is a
hardening regression guard, added in response to a bug report claiming a near-square
(`4678591 x 5053859` EMU), real-world-corpus ellipse rendered with an unclipped rectangular top
while its bottom clipped correctly: it reproduces the report's own exact shape geometry and 16:9
slide size, and asserts all four bounding-box corners (not just one diagonal pair) remain
background, including a point in the bounding box's upper quarter that lies outside the ellipse's
own curve. Direct investigation (a mask-boundary-trace overlay against the real rendered output)
found the clip mask is already pixel-correct and fully symmetric on all four sides - the
image-content clip concern itself has no production code defect; this test closes the
test-coverage gap for that concern. **A prior investigation pass incorrectly concluded from this
same evidence that the bug report's entire underlying complaint was "a visual illusion" with no
code defect anywhere.** That broader conclusion was wrong: the report's own fixture also declared
a red `<a:ln>` on the same `<p:pic>`, and a from-scratch, independently re-verified investigation
(see `.agent-logs/planning-picture-ln-stroke-fix-7f2a4d.md`) proved `RenderPicture` never read or
painted a `<p:pic>`'s own `<a:ln>` stroke at all - a real, 100%-of-perimeter-absent defect, fixed
and verified below under `CanvasNetPptx-PptxDocument-PictureStrokeOutline`. Only the narrower
image-content-clip conclusion above (no defect in `ResolvePictureClipPath`/`PaintPicture`) remains
correct.

#### CanvasNetPptx-PptxDocument-PictureStrokeOutline: `<p:pic>` Own `<a:ln>` Stroke Outline Rendering

**Tests**: `ResolvePictureGeometryPath_RectPreset_ReturnsNonNullRectanglePath`,
`ResolvePictureGeometryPath_NoGeometryElement_ReturnsImplicitRectanglePath`,
`ResolvePictureGeometryPath_EllipsePreset_ReturnsNonNullPath`,
`ResolvePictureGeometryPath_CustGeom_ReturnsNonNullPath`,
`ResolvePictureGeometryPath_PrstGeomMissingPrstAttribute_ThrowsInvalidDataException`,
`ResolvePictureGeometryPath_UnsupportedPreset_ThrowsPptxUnsupportedFeatureException`,
`ResolvePictureGeometryPath_NullSpPrElement_ThrowsArgumentNullException`,
`Render_PictureEllipseGeometry_WithRedLnStroke_RendersCompleteClosedEllipseOutline`,
`Render_PictureNoPrstGeom_WithLnStroke_StrokesRectangleOutline`

Proves `ResolvePictureGeometryPath` always resolves a concrete, non-null `Path` - unlike
`ResolvePictureClipPath`'s own "rect"/"no geometry" collapse to `null` - for an explicit
`<a:prstGeom prst="rect">`, for a `<p:spPr>` with neither `<a:prstGeom>` nor `<a:custGeom>` at all
(the implicit full-rectangle fallback), for a non-`rect` preset (`ellipse`), and for an explicit
`<a:custGeom>`, dispatching to the exact same `PptxPresetGeometry.Build`/`ResolveCustomGeometry`
resolvers `ResolvePictureClipPath`/an auto-shape's own `ResolveShapeGeometry` already use. Proves
a `<a:prstGeom>` missing its own `prst` attribute throws `InvalidDataException`, an unsupported
preset name propagates `PptxUnsupportedFeatureException` unchanged, and a `null` `spPrElement`
argument throws `ArgumentNullException` - the identical fail-closed contract
`ResolvePictureClipPath`/`ResolveShapeGeometry` already carry.

End-to-end, render-level, proves `RenderPicture` now strokes a `<p:pic>`'s own `<a:ln>` as a
COMPLETE, correctly-closed outline: `Render_PictureEllipseGeometry_WithRedLnStroke_RendersCompleteClosedEllipseOutline`
reproduces the original bug report's own exact `<a:xfrm>`/`<a:prstGeom prst="ellipse">`/red
`<a:ln>` geometry and 16:9 slide size, and samples all four of the ellipse's own perimeter
midpoints (top/bottom/left/right-center, not just the corners the sibling clip test samples) for
the stroke's own red-dominant color - a defect covering 0% of the perimeter (the confirmed, now-
fixed pre-fix state) would fail at all four simultaneously.
`Render_PictureNoPrstGeom_WithLnStroke_StrokesRectangleOutline` proves the new
`ResolvePictureGeometryPath` "neither `<a:prstGeom>` nor `<a:custGeom>`" fallback is actually used
for stroking: a geometry-less picture with a present `<a:ln>` now strokes a complete rectangle
around its own bounding box, sampling both its top and left edges, rather than silently dropping
the stroke as it did before this fix (when `RenderPicture` never read `<a:ln>` at all, regardless
of geometry). A before/after visual repro (the bug report's own exact geometry, rendered end-to-
end and visually inspected) confirmed the fix directly: before, a plain pale ellipse with
absolutely no red anywhere on its perimeter; after, a complete, correctly-closed red ellipse
outline fully framing the same pale ellipse - matching real-world PowerPoint's own expected visual
stacking (image content painted first, stroke framing it on top).

## Acceptance Criteria

A unit-level test run passes when all scenarios above pass without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong return value
constitutes a failure. Collectively, these scenarios cover the complete current `PptxDocument`
package layer (Phase 1a: ZIP opening, content-type resolution, relationship resolution including
relative-target traversal and its root-escape guard, and the public API's argument validation and
disposal contract); the presentation/theme/master/layout/slide model and placeholder
property-inheritance resolver (Phase 1b: slide size/count, EMU-to-pixel conversion, theme
color/font scheme resolution, master/layout/slide structural placeholder parsing, and the
verified ECMA-376 placeholder-matching and per-category property-resolution algorithm); DrawingML
shape geometry and paint resolution (Phase 1c: `<a:xfrm>` position/rotation/flip transform
resolution, `<p:grpSp>` child-coordinate-space transform composition, 24 supported `<a:prstGeom>`
preset shapes plus `<a:custGeom>` custom path-command parsing, `<a:solidFill>`/`<a:gradFill>`/
`<a:noFill>` resolution including scheme-color lookup through the resolved theme and the
`lumMod`/`lumOff`/`shade`/`tint`/`alpha` color-transform pipeline, and `<a:ln>` stroke
width/dash/fill resolution realized as a stroked outline path via the core `PathStroker`); and
DrawingML text body parsing, attribute-level property inheritance (including a slide master's own
`<p:txStyles>` buckets), word-wrap/alignment/vertical-anchor/autofit text layout, and glyph-ink
rendering onto a core `Surface` (Phase 1d: see the *Text Layout and Rendering (Phase 1d) Test
Scenarios* section above); and dedicated `<p:pic>` picture-shape decoding/cropping/painting,
`<a:tbl>` table structure parsing (reusing the Phase 1c/1d fill/border/text-body resolvers
verbatim), merge-aware table cell-rect resolution, table cell fill/border/text painting, and
recursive, full shape-tree parsing/dispatch across `<p:sp>`/`<p:pic>`/`<p:graphicFrame>`/
`<p:grpSp>` with lazy, invoke-on-demand theme resolution (Phase 1e: see the *Picture Decoding,
Linking, and Crop-Rectangle Resolution*, *Picture Compositing Without PDF's Y-Flip*, *Table
Structure, Cell Attributes, and Verbatim Fill/Border/Text Reuse*, *Merge-Aware Cell-Rect
Computation*, *Cell Fill, Border, and Text Painting*, and *Recursive Shape-Tree Parsing and
Deferred Theme Resolution* Test Scenarios sections above); and the public, slide-level rendering
API that walks a slide's full shape tree in document order and paints every supported node kind
onto a destination `Surface`, with full public-API argument validation and a DPI convenience
overload (Phase 1f: see the *Full Slide Rendering (Phase 1f) Test Scenarios* section above). With
Phase 1f, the planned PPTX 1.0 feature set is complete. A slide's own `<p:bg>` background fill is
covered by *CanvasNetPptx-PptxDocument-SlideBackgroundFill* above instead, and master/layout
decorative shape rendering is covered by *CanvasNetPptx-PptxDocument-MasterLayoutShapeRendering*
above instead. Not yet covered: picture effects/shadows, nested tables, table auto-sizing to
fit overflowing cell content (each row's resolved height is taken verbatim from its declared
`<a:tr h="...">` value, with no growth to accommodate overflowing cell content), table
style/banding (`<a:tableStyleId>`), group-level style cascading
beyond transform composition, radial/path gradients, `<a:avLst>` preset adjustment-value parsing,
full text justification, `<a:spAutoFit>` shape-resize autofit, kerning, and text
clipping on overflow. (Bullets/numbering are now covered by *CanvasNetPptx-PptxDocument-
BulletProperties*/*CanvasNetPptx-PptxDocument-BulletRendering* above instead, and `<p:cxnSp>`
connector shapes are now covered by *CanvasNetPptx-PptxDocument-ConnectorRendering* below
instead.) None of these is a currently planned phase; any of them remaining important is a
candidate for a future, corpus-driven hardening pass (`pptx-phase-2`), not a scheduled increment.
