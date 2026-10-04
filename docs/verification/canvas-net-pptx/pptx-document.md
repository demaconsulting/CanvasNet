## PptxDocument Unit Verification Design

<!-- cspell:ignore ooxml pptx srgb xfrm prst cust patt bodyPr normAutofit noAutofit spAutoFit pPr -->
<!-- cspell:ignore rPr txBody txStyles lstStyle defRPr lnSpc spcBef spcAft justLow fontScale -->
<!-- cspell:ignore spcPct spcPts Ordinally -->

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

#### CanvasNetPptx-PptxDocument-PlaceholderInheritancePerCategory: Categories Independent, Theme Is Unchanged Context

**Tests**:
`PptxDocumentInheritance_ResolvePlaceholderProperties_PerCategoryIndependentFallthrough_SlideOverridesFillButNotText`,
`PptxDocumentInheritance_ResolvePlaceholderProperties_NoMatchAtAnyLevel_ReturnsNullForBothCategories`

Proves `<p:spPr>` and `<p:txBody>/<a:lstStyle>` resolve independently - a slide-level `<p:spPr>`
with no slide-level `<a:lstStyle>` resolves the slide's own fill while still falling through to
the layout for text style. Separately, proves that when no placeholder matches at any level, both
effective property categories resolve to `null` while the resolved theme is still returned
unchanged as context.

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
covering all 24 supported preset names), `PptxPresetGeometry_Build_UpArrow_PointsUpward`,
`PptxPresetGeometry_Build_DownArrow_PointsDownward`,
`PptxPresetGeometry_Build_UnsupportedPreset_ThrowsWithFeatureToken`,
`PptxPresetGeometry_Build_NonPositiveSize_ReturnsEmptyPath`

Proves `ResolveShapeGeometry` dispatches `<a:prstGeom prst="rect">` to a rectangle path sized to
the shape's declared extent; proves a `<p:spPr>` with neither `<a:prstGeom>` nor `<a:custGeom>`,
and a `<a:prstGeom>` missing its required `prst` attribute, each throw `InvalidDataException`; and
proves an unrecognized preset name throws `PptxUnsupportedFeatureException`. Proves every one of
the 24 supported preset names (via a `[Theory]` enumerating all of them) builds a path whose
points all lie within `(0,0)`-`(w,h)` bounds; proves `upArrow`/`downArrow` each orient their arrow
point toward the expected direction (a directional sanity check distinguishing a correctly
oriented preset from a trivially-passing bounds check alone); proves an unsupported preset throws
`PptxUnsupportedFeatureException` carrying the expected feature token; and proves a non-positive
width or height resolves to `Path.Empty` rather than throwing or producing a degenerate path.

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

#### CanvasNetPptx-PptxDocument-FillResolution: noFill/solidFill/gradFill Resolve, pattFill/blipFill Fail Closed

**Tests**: `ResolveFill_NullParent_ReturnsNoFill`, `ResolveFill_ExplicitNoFill_ReturnsNoFill`,
`ResolveFill_NoRecognizedFillChild_ReturnsNoFill`,
`ResolveFill_SolidFillSrgbClr_ReturnsResolvedSolidFill`,
`ResolveFill_SolidFillSchemeClr_ResolvesThroughTheme`,
`ResolveFill_GradFillLinear_ReturnsResolvedGradientFill`,
`ResolveFill_PatternFill_ThrowsPptxUnsupportedFeatureException`,
`ResolveFill_PictureFill_ThrowsPptxUnsupportedFeatureException`,
`CanvasNetPptx_SystemIntegration_GeometryAndPaint_FreeformShapeResolvesEndToEnd`

Proves a `null` fill parent, an explicit `<a:noFill/>`, and a `<p:spPr>` with no recognized fill
child, all three resolve to `PptxNoFill.Instance`; proves `<a:solidFill><a:srgbClr .../>` and
`<a:solidFill><a:schemeClr .../>` both resolve to the expected `PptxSolidFill` (the latter
resolving its color through the supplied `PptxTheme`, not merely parsing the slot name); proves
`<a:gradFill>` with a linear direction resolves to a `PptxGradientFill` wrapping the expected
gradient stops; and proves `<a:pattFill>`/`<a:blipFill>` each throw
`PptxUnsupportedFeatureException` rather than silently resolving to an incorrect fill. The
end-to-end system test additionally proves a real shape's fill resolves correctly when reached
through the full slide/layout/master/theme package-load chain, not merely from a hand-built
`XElement` fragment.

#### CanvasNetPptx-PptxDocument-ColorResolution: srgbClr/sysClr/schemeClr Resolve to the Expected Rgba32 Value

**Tests**: `ResolveColor_SrgbClr_ParsesHexDirectly`, `ResolveColor_SysClr_UsesLastClrAttribute`,
`ResolveColor_SchemeClrOrdinarySlots_ResolveToMatchingThemeSlot` (a `[Theory]` covering all 12
ordinary scheme slot names), `ResolveColor_SchemeClrBackgroundTextAliases_MapToExpectedSlot` (a
`[Theory]` covering `bg1`/`tx1`/`bg2`/`tx2`), `ResolveColor_SchemeClrUnrecognizedSlot_ThrowsInvalidDataException`,
`ResolveColor_UnsupportedColorKind_ThrowsPptxUnsupportedFeatureException`,
`ResolveColor_SrgbClrEightDigitValue_ThrowsInvalidDataException`,
`ResolveColor_SysClrEightDigitLastClrValue_ThrowsInvalidDataException`

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
unintended-alpha color instead of failing closed.

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
`ResolveStrokeOutline_RectangleWithSolidLine_ProducesNonEmptyOutline`

Proves a `null` `<a:ln>`, a zero/absent width, and an explicit or implicit no-fill line, each
resolve to `null` (no stroke drawn); proves a positive width with a `<a:solidFill>` resolves a
`PptxLineStyle` carrying the expected width and color; proves each of the seven supported
`<a:prstDash val="...">` preset names resolves a non-null, width-proportional dash array; proves
the common `solid` preset (and, by extension, any unrecognized name) resolves `null` (a solid
line); and proves `ResolveStrokeOutline` produces a non-empty outline path for a rectangle with a
solid line, via the core `PathStroker`/`StrokeStyle` machinery reused from the PDF renderer's own
call pattern.

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
`ResolveTextLayout_AlignCenter_CentersLineWithinAvailableWidth`,
`ResolveTextLayout_AlignRight_RightAlignsLineAgainstAvailableWidth`,
`ResolveTextLayout_RunBreakRun_ForcesSecondLine`,
`ResolveTextLayout_TallestFontComparison_ScalesByUnitsPerEmAndSizeEmu_NotRawFontUnits`

Proves a long run wraps onto multiple lines at the expected token boundary for a narrow
`widthEmu`; proves a single token that alone exceeds the available width is placed alone on its
own (overflowing) line rather than looping indefinitely; proves center/right alignment
position a line's glyphs at the expected hand-computed X offset within the available width;
proves a paragraph containing a run, a preserved `<a:br/>` line-break item, and a second run lays
out as two separate lines (the break forces a line boundary independent of word-wrap's own
token-packing decisions); and proves the per-line "tallest run" selection compares two runs using
different fonts/sizes by each run's own font metrics scaled by that font's `UnitsPerEm` and the
run's own resolved `SizeEmu`, so a visually smaller run on a font reporting disproportionately
large raw font-design-unit metrics is not incorrectly selected over a visually larger run on a
differently-scaled font.

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
`ResolveTextLayout_NormAutofitLnSpcReductionOnly_AppliesStoredReductionWithNeutralFontScale`

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
default.

#### CanvasNetPptx-PptxDocument-TextRendering: Glyph Painting and Font Resolution

**Tests**: `PaintTextLayout_IdentityTransform_PaintsGlyphAtExpectedLocationWithResolvedColor`,
`PaintTextLayout_TranslatedShapeTransform_ShiftsPaintedLocation`,
`PaintTextLayout_EmptyOutlineGlyph_PaintsNothing`,
`PaintTextLayout_AsymmetricGlyph_PaintsInkAboveBaselineNotBelow`,
`ResolveTextFont_AnyFamilyHint_ResolvesNonNullFont`,
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
Scenarios* section above). Not yet covered: non-placeholder (freeform) shape *enumeration* from a
slide's full `<p:spTree>` (Phase 1c's resolvers operate on hand-fetched shape fragments, not a
higher-level "enumerate every shape on a slide" API), pattern/picture fill, radial/path gradients,
`<a:avLst>` preset adjustment-value parsing, full group-shape rendering semantics beyond transform
composition, bullets/numbering, full text justification, `<a:spAutoFit>` shape-resize autofit,
kerning, text clipping on overflow, and a full per-slide public `Render` API - none of these is
implemented yet.
