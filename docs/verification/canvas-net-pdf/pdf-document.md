## PdfDocument Unit Verification Design

<!-- cspell:ignore xref startxref endobj endstream ObjStm MediaBox -->

This document describes the unit-level verification strategy for the `PdfDocument` class.

`PdfDocument` is distributed as the separate `DemaConsulting.CanvasNet.Pdf` NuGet package
(namespace `DemaConsulting.CanvasNet.Pdf`), which references the core `DemaConsulting.CanvasNet`
package; its unit tests live in the sibling `DemaConsulting.CanvasNet.Pdf.Tests` project.

### Verification Approach

The `PdfDocument` unit is verified through unit tests that exercise its tokenizer, object model,
content-stream interpreter, and public API in isolation. Tokenizer and object-model tests
(`PdfDocumentTests.cs`) construct small, hand-written byte sequences directly (using the
`internal`, `InternalsVisibleTo`-exposed `PdfTokenizer`/`PdfObject` types) for controlled,
targeted coverage of individual lexical and structural rules without needing a full, valid PDF
file for every case. Content-stream interpreter tests (graphics-state stack, path-construction,
path-painting) build small, in-memory, single-page PDFs via a private test helper
(`BuildSinglePagePdf`) parameterized by `/MediaBox` size, optional `/Rotate`, and an arbitrary
content-stream string, then call `Render` through the public API and assert specific pixel
colors at specific `Surface` coordinates - every expected coordinate was independently confirmed
against the real rasterizer (not merely hand-derived) before being fixed into an assertion, since
stroke/curve antialiasing and the PDF-to-device y-axis flip make hand derivation error-prone.
Device-color, stream-filter-pipeline, and image-XObject tests (Phase 3) additionally use a
second private test helper, `BuildSinglePagePdfWithResources`, extending the same in-memory
construction with a page `/Resources` dictionary and 0 or more extra indirect objects (color-space
arrays, image-XObject streams) the resources reference by number - used whenever a test needs a
named `/Resources/ColorSpace` or `/Resources/XObject` entry that a bare content-stream string
cannot express.
Cross-reference form, linear-scan fallback, page-tree traversal/inheritance/cycle-rejection,
`/Encrypt` detection, and public-API (`Open`/`PageCount`/`GetPageInfo`/`Render`/`Dispose`) tests
exercise the same hand-authored, byte-exact fixture files under `PdfFixtures/` (see
`PdfFixtures/README.md` for provenance) through the public API only. Font-resolution/encoding/
text-operator tests (Phase 4) build a minimal synthetic embedded TrueType font in-memory (via the
shared `SyntheticFontBuilder` test helper, linked from `DemaConsulting.CanvasNet.Tests`) whenever
a test only needs controlled glyph outlines/`cmap` mappings; the one required end-to-end,
real-font pixel-level test instead reuses the shared, unmodified `OpenSans-Regular.ttf` fixture
(the same file `DemaConsulting.CanvasNet.Svg.Tests` links in), re-deriving its expected
device-pixel positions from the font's own outline/metrics rather than hardcoded numbers. Because
`PdfDocument`'s dependencies (`Canvas.Surface`, `Codecs.UnsupportedImageFeatureException`,
`Geometry.PathBuilder`, `Drawing.PathFiller`/`PathStroker`, and, as of Phase 4, `Fonts.TrueTypeFont`)
are all sibling in-house types, not external services, no mocking or stubbing is required. Tests
assert on parsed token/object field values, on `PageCount`/`PdfPageInfo` field values, on
`Surface` pixel/dimension values, and on thrown exception types (and, for
`UnsupportedImageFeatureException`, its `Feature` token) - never on "no exception thrown" alone,
so every test can actually fail if the implementation is wrong.

Unit tests reside in `PdfDocumentTests.cs` within the `DemaConsulting.CanvasNet.Pdf.Tests`
project.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `PdfDocument`'s dependencies are all in-house types
  (`Canvas.Surface`, `Codecs.UnsupportedImageFeatureException`, `Geometry.PathBuilder`,
  `Drawing.PathFiller`/`PathStroker`)
- **Isolation**: Each test method builds its own byte sequence, its own in-memory single-page PDF
  (content-stream interpreter tests), or opens its own fixture file from `PdfFixtures/`; no shared
  state between tests

### Unit-Level Test Scenarios

#### CanvasNetPdf-PdfDocument-Tokenizer: Tokenizer Recognizes Every Lexical Construct

**Tests**: `PdfDocument_Tokenizer_Numbers_ParsesIntegerAndRealForms`,
`PdfDocument_Tokenizer_LiteralString_HandlesNestedParensAndEscapes`,
`PdfDocument_Tokenizer_HexString_DecodesFullBytes`,
`PdfDocument_Tokenizer_HexString_PadsOddDigitCountWithTrailingZero`,
`PdfDocument_Tokenizer_Name_DecodesHashEscapes`,
`PdfDocument_Tokenizer_Delimiters_RecognizesArrayAndDictionaryBrackets`,
`PdfDocument_Tokenizer_Comments_AreSkipped`, `PdfDocument_Tokenizer_Keywords_AreRecognizedVerbatim`,
`PdfDocument_Tokenizer_EndOfInput_ReturnsEndOfFileToken`

Feeds small, hand-written byte sequences directly to `PdfTokenizer`, asserting the returned
token's `Kind` and decoded value (number, string bytes, name text, or keyword text) for every
lexical construct the tokenizer must recognize, including edge cases (odd hex-digit count
padding, `#xx` name escapes, nested-parenthesis literal strings, comment skipping).

#### CanvasNetPdf-PdfDocument-ObjectModel: Object Model Parses Dictionaries, Arrays, and References

**Tests**: `PdfDocument_ObjectModel_Dictionary_ParsesNestedEntries`,
`PdfDocument_ObjectModel_Array_ParsesMixedElementTypes`,
`PdfDocument_ObjectModel_IndirectReference_ParsesObjectAndGenerationNumbers`,
`PdfDocument_ObjectModel_BareNumber_IsNotMisreadAsReference`

Feeds small, hand-written token sequences to `PdfDocument.ParseValue`, asserting the resulting
`PdfObject` tree's `Kind`/field values for nested dictionaries, mixed-kind arrays, indirect
references, and the bare-number-vs-reference disambiguation.

#### CanvasNetPdf-PdfDocument-ClassicXref: Classic Xref Table Resolves Root and Page

**Test**: `PdfDocument_Open_ClassicXref_ResolvesRootAndPage`

Opens `PdfFixtures/classic-xref-single-page.pdf` (a classic `xref` table + `trailer`) through the
public API. Asserts `PageCount` and the single page's `GetPageInfo` size/rotation match the
fixture's known content.

#### CanvasNetPdf-PdfDocument-XrefStream: Xref Stream Resolves Root and Page

**Test**: `PdfDocument_Open_XrefStream_ResolvesRootAndPage`

Opens `PdfFixtures/xref-stream-single-page.pdf` (a `/Type /XRef` cross-reference stream, no
classic table) through the public API. Asserts `PageCount` and the single page's `GetPageInfo`
size match the fixture's known content.

#### CanvasNetPdf-PdfDocument-ObjectStream: Object Stream Decompresses and Resolves Compressed Page

**Test**: `PdfDocument_Open_ObjectStream_DecompressesAndResolvesCompressedPage`

Opens `PdfFixtures/object-stream.pdf` (a `/Type /ObjStm` compressed-object stream holding the page
dictionary) through the public API. Asserts `PageCount` and the compressed page's `GetPageInfo`
size match the fixture's known content, confirming FlateDecode decompression and per-object
resolution within the stream both work correctly.

#### CanvasNetPdf-PdfDocument-HybridXref: Hybrid Xref Resolves Compressed Entry via XRefStm

**Test**: `PdfDocument_Open_HybridXref_ResolvesCompressedEntryViaXRefStm`

Opens `PdfFixtures/hybrid-xref.pdf` (a classic trailer whose own table marks one object free, plus
an `/XRefStm` link to a supplementary cross-reference stream that alone describes that object)
through the public API. Asserts `PageCount` and the page's `GetPageInfo` size match the fixture's
known content, confirming a hybrid entry correctly overrides a classic "free" marker for the same
object number.

#### CanvasNetPdf-PdfDocument-LinearScanFallback: Malformed Startxref Falls Back to Linear Scan

**Test**: `PdfDocument_Open_MalformedStartxref_FallsBackToLinearScan`

Opens `PdfFixtures/malformed-startxref.pdf` (no `startxref`/`xref`/`trailer` at all) through the
public API. Asserts `PageCount` and the page's `GetPageInfo` size match the fixture's known
content, confirming the linear-scan fallback successfully reconstructs an object-offset table and
locates the catalog without any cross-reference section present.

#### CanvasNetPdf-PdfDocument-PageTreeTraversal: Page Tree Traversal Reports All Pages

**Test**: `PdfDocument_PageTree_Traversal_ReportsAllPagesInDocumentOrder`

Opens `PdfFixtures/multi-page-mixed-mediabox-rotate.pdf` (three pages) through the public API.
Asserts `PageCount` equals 3, confirming every `/Type /Page` leaf in the page tree is reported.

#### CanvasNetPdf-PdfDocument-PageTreeInheritance: MediaBox/Rotate Inheritance and Rotation Swap

**Tests**: `PdfDocument_PageTree_Inheritance_ExplicitMediaBoxAndNoRotationIsUnchanged`,
`PdfDocument_PageTree_Inheritance_Rotation90SwapsDisplayedWidthAndHeight`,
`PdfDocument_PageTree_Inheritance_MediaBoxInheritedFromPagesNodeWhenPageOmitsIt`

Opens `PdfFixtures/multi-page-mixed-mediabox-rotate.pdf` through the public API and calls
`GetPageInfo` for each of its three pages. Asserts: a page with its own explicit `/MediaBox` and
no `/Rotate` reports that box unchanged; a `/Rotate 90` page reports swapped width/height relative
to its raw `/MediaBox`; a page declaring no `/MediaBox` of its own inherits its ancestor `/Pages`
node's box.

#### CanvasNetPdf-PdfDocument-PageTreeCycleRejection: Page Tree Cycle Throws InvalidDataException

**Test**: `PdfDocument_PageTree_CycleRejection_ThrowsInvalidDataException`

Opens `PdfFixtures/cyclic-page-tree.pdf` (a `/Kids` entry referencing an ancestor) through the
public API. Asserts `InvalidDataException` is thrown, confirming the unbounded-cycle guard stops
traversal rather than looping forever.

#### CanvasNetPdf-PdfDocument-EncryptDetection: Encrypted Trailer Throws UnsupportedImageFeatureException

**Test**: `PdfDocument_Open_EncryptedTrailer_ThrowsUnsupportedImageFeatureException`

Opens `PdfFixtures/encrypted-trailer.pdf` (a trailer containing an `/Encrypt` key) through the
public API. Asserts `UnsupportedImageFeatureException` is thrown with `Feature == "pdf-encrypted"`.

#### CanvasNetPdf-PdfDocument-Open: Open Validates Null and Empty/Whitespace Arguments

**Tests**: `PdfDocument_Open_NullStream_ThrowsArgumentNullException`,
`PdfDocument_Open_NullPath_ThrowsArgumentNullException`,
`PdfDocument_Open_EmptyOrWhitespacePath_ThrowsArgumentException`

Calls `Open(Stream)`/`Open(string)` with a null stream, a null path, and an empty/whitespace-only
path. Asserts `ArgumentNullException`/`ArgumentException` respectively, confirming argument
validation precedes any parsing attempt.

#### CanvasNetPdf-PdfDocument-GetPageInfo: GetPageInfo Validates Page Index Range

**Test**: `PdfDocument_GetPageInfo_OutOfRangeIndex_ThrowsArgumentOutOfRangeException`
(`[Theory]`: a negative index and an index equal to `PageCount`)

Opens a single-page fixture and calls `GetPageInfo` with an out-of-range index. Asserts
`ArgumentOutOfRangeException` for both a negative index and one at/beyond `PageCount`.

#### CanvasNetPdf-PdfDocument-Render: Render Paints Content-Stream Geometry and Validates Arguments

**Tests**: `PdfDocument_Render_ValidPageIndex_ReturnsCorrectlySizedBlankSurface`,
`PdfDocument_Render_OutOfRangePageIndex_ThrowsArgumentOutOfRangeException`,
`PdfDocument_Render_InvalidWidth_PropagatesSurfaceArgumentOutOfRangeException`,
`PdfDocument_ContentStream_NoContents_RendersBlankSurface`

Calls `Render` with a valid page index and caller-chosen size against a fixture with no
`/Contents`, asserting the returned `Surface` has exactly the requested dimensions and every
pixel is the default (fully transparent) value. Calls `Render` with an out-of-range page index
and a non-positive width, asserting `ArgumentOutOfRangeException` in both cases (the latter
propagated unwrapped from `Surface`'s own constructor).

#### CanvasNetPdf-PdfDocument-ContentStreamDispatch: Unknown Operators Are Skipped, Malformed Recognized Operators Throw

**Tests**: `PdfDocument_ContentStream_UnknownOperator_IsSkippedWithoutThrowing`,
`CanvasNetPdf_SystemIntegration_PdfRender_FilledRectangleAndStrokedLine_PaintsExpectedPixels`

Renders a content stream containing an unrecognized operator (`gs`, ExtGState - still out of
scope in Phase 3) immediately followed by a recognized stroke operator, asserting the stroke
still painted its expected pixel (proving the unknown operator was silently skipped rather than
aborting the whole stream). The end-to-end system-integration test independently proves the same
dispatch loop against a real, hand-authored fixture containing both a filled rectangle and a
stroked line, asserting specific opaque-black and transparent pixels at specific coordinates.

#### CanvasNetPdf-PdfDocument-ContentsResolution: Contents Array Concatenates With a Space Separator

**Test**: `PdfDocument_ContentStream_ContentsArray_ConcatenatesStreamsWithSpaceSeparator`

Opens `PdfFixtures/contents-array-two-streams.pdf`, whose `/Contents` is a two-entry array (one
stream holding `"10 10 40"`, the other holding `"40 re f"`) deliberately chosen so an
omitted-separator regression would merge the two `"40"` tokens into `"4040"`, reducing `re`'s
operand count and (in this specific fixture) throwing `InvalidDataException` instead of rendering
the expected filled square. Asserts the expected filled-square pixel is opaque black and a pixel
outside it remains transparent, confirming the required space-separator concatenation.

#### CanvasNetPdf-PdfDocument-GraphicsStateStack: q/Q/cm Compose and Restore the Current Transform

**Tests**: `PdfDocument_GraphicsState_QPushCmThenQPop_RestoresPriorTransform`,
`PdfDocument_GraphicsState_NestedQQ_ComposesTransformsInOrder`,
`PdfDocument_GraphicsState_UnbalancedQWithNoMatchingPush_DoesNotThrow`,
`PdfDocument_GraphicsState_MalformedCmOperandCount_ThrowsInvalidDataException`

Renders a content stream that draws an identical line segment both inside a `q`/`cm`/`Q` block
and after the matching `Q`, asserting the post-`Q` segment lands at its unscaled device position
(proving the CTM was restored, not left scaled). Renders a nested `q`/`cm`/`q`/`cm`/`Q`/`Q` block
whose two `cm`s each scale a different single axis, asserting the drawn segment lands only at the
position both axes' composed scale produces, and explicitly asserting the two single-axis-only
positions a partial-composition bug would instead produce are *not* painted. Renders a stray `Q`
with no matching prior `q`, asserting no exception is thrown (a documented no-op leniency).
Renders a malformed `cm` (wrong operand count), asserting `InvalidDataException`.

#### CanvasNetPdf-PdfDocument-CtmDerivation: Base CTM Correctly Incorporates MediaBox/Rotate/Requested Size

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_RotatedPage_MapsGeometryToCorrectPixelPosition`

Opens `PdfFixtures/path-construction-rotated-page.pdf` (`/MediaBox [0 0 200 100]`,
`/Rotate 90`, a deliberately asymmetric filled rectangle near the raw MediaBox's bottom-left
corner) and renders it at its rotation-swapped display size (100x200). Asserts the rectangle's
interior is opaque black at its mathematically correct rotated device position, and asserts a
specific pixel where a 270-instead-of-90 rotation-sign regression would incorrectly paint it
instead remains transparent - a fixture and assertion pair specifically designed so a
rotation-sign or origin-offset defect produces a detectably wrong result rather than a merely
shifted-but-still-plausible one.

#### CanvasNetPdf-PdfDocument-PathConstruction: Path-Construction Operators Build Documented Geometry

**Tests**: `PdfDocument_PathOps_MoveLineRectCurve_BuildExpectedGeometry`,
`PdfDocument_PathOps_MalformedOperandCount_ThrowsInvalidDataException` (`[Theory]`, one case per
`m`/`l`/`c`/`v`/`y`/`re`), `PdfDocument_PathOps_DrawBeforeMoveTo_ThrowsInvalidDataException`

Renders, in turn: an explicit `m`/`l`/`h`-built square (filled), a `re`-built rectangle (filled),
a full `c` cubic Bezier (stroked), a `v` shorthand curve (whose first control point defaults to
the current point, stroked), and a `y` shorthand curve (whose second control point defaults to
the endpoint, stroked) - asserting the mathematically exact Bezier-midpoint (or interior/exterior)
pixel each construction is expected to paint. A `[Theory]` exercises a too-few/too-many operand
count for each path-construction operator, asserting `InvalidDataException` in every case.
Renders a bare `l` with no preceding `m`/`re`, asserting `InvalidDataException`.

#### CanvasNetPdf-PdfDocument-PathPainting: Path-Painting Operators Fill/Stroke/Clear as Documented

**Tests**: `PdfDocument_PathOps_FillNonZero_PaintsExpectedPixels`,
`PdfDocument_PathOps_FillEvenOdd_PaintsExpectedPixels`, `PdfDocument_PathOps_Stroke_PaintsExpectedPixels`,
`PdfDocument_PathOps_CloseAndFillAndStroke_PaintsExpectedPixels`,
`PdfDocument_PathOps_NoOp_DiscardsPathWithoutPainting`,
`PdfDocument_PathOps_PaintOperator_ClearsPathButPreservesGraphicsState`

Renders a filled rectangle (`f`), asserting an interior pixel is opaque black and an exterior
pixel remains transparent. Renders two nested, same-winding rectangles (`f*`), asserting the outer
ring is filled but the doubly-covered inner region is left as an even-odd "hole". Renders a
stroked vertical line (`S`), asserting a pixel on the line is opaque black and a pixel beside it
is not. Renders an *open* triangle (no `h`) via `b`, asserting both the filled interior and the
implicit closing edge are painted - directly contrasted, at the same coordinate, against the same
triangle painted with a plain `S` (no closing), which leaves that coordinate untouched. Renders a
rectangle followed by `n`, asserting the entire surface remains fully transparent. Renders two
rectangles filled under the same scaled `cm`, asserting the second path's fill lands only in its
own expected region - proving the first path was cleared after its own `f` rather than
accumulating into the second, while the surrounding CTM was preserved across the clear.

#### CanvasNetPdf-PdfDocument-Dispose: Dispose Is Idempotent

**Test**: `PdfDocument_Dispose_CalledTwice_DoesNotThrow`

Calls `Dispose()` twice on the same instance. Asserts neither call throws.

#### CanvasNetPdf-PdfDocument-ObjectDisposedException: Every Other Public Member Throws After Dispose

**Tests**: `PdfDocument_PageCount_AfterDispose_ThrowsObjectDisposedException`,
`PdfDocument_GetPageInfo_AfterDispose_ThrowsObjectDisposedException`,
`PdfDocument_Render_AfterDispose_ThrowsObjectDisposedException`

Disposes an opened document, then calls `PageCount`, `GetPageInfo`, and `Render`. Asserts each
throws `ObjectDisposedException`.

#### CanvasNetPdf-PdfDocument-DeviceColorOperators: Device Color Operators Set the Expected Fill/Stroke Color

**Tests**: `PdfDocument_Color_SetGrayFill_SetsExpectedRgbaColor`,
`PdfDocument_Color_SetGrayStroke_SetsExpectedRgbaColor`,
`PdfDocument_Color_SetRgbFill_SetsExpectedRgbaColor`,
`PdfDocument_Color_SetRgbStroke_SetsExpectedRgbaColor`,
`PdfDocument_Color_SetCmykFill_ConvertsToExpectedRgbaColor`,
`PdfDocument_Color_SetCmykStroke_ConvertsToExpectedRgbaColor`,
`PdfDocument_Color_ComponentValuesOutsideZeroToOne_AreClamped` (`[Theory]`),
`PdfDocument_Color_MalformedOperandCount_ThrowsInvalidDataException` (`[Theory]`, one case per
`g`/`G`/`rg`/`RG`/`k`/`K`/`cs`/`CS`/`sc`/`SC`/`scn`/`SCN`)

Renders a filled rectangle/stroked line after each of `g`/`G`/`rg`/`RG`/`k`/`K`, asserting the
painted pixel matches the exact expected RGBA conversion (including the CMYK
`R = 255 * (1 - C) * (1 - K)` formula, hand-verified). A `[Theory]` renders `rg` with several
component values outside `[0, 1]`, asserting the painted color matches the clamped (not
rejected) result. A second `[Theory]` renders each operator with a malformed operand count,
asserting `InvalidDataException` in every case.

#### CanvasNetPdf-PdfDocument-ColorSpaceOperators: cs/CS/sc/scn Track and Apply the Current Color Space

**Tests**: `PdfDocument_Color_SetColorSpaceFill_DeviceNames_ResetsColorToBlack` (`[Theory]`),
`PdfDocument_Color_SetColorSpaceStroke_DeviceNames_ResetsColorToBlack` (`[Theory]`),
`PdfDocument_Color_SetColorFillUsingCurrentColorSpace_Sc_PaintsExpectedColor`,
`PdfDocument_Color_SetColorStrokeUsingCurrentColorSpace_SC_PaintsExpectedColor`,
`PdfDocument_Color_ScnWithPatternName_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Color_UnsupportedNamedColorSpace_ThrowsUnsupportedImageFeatureException` (`[Theory]`:
Indexed/Separation/DeviceN/ICCBased/CalRGB/CalGray/Lab),
`CanvasNetPdf_SystemIntegration_PdfRender_ColoredRectangleFill_PaintsExpectedRgbPixels`

Renders a red-filled rectangle, then a `cs`/`CS` device-name switch (asserted, for all three
device names, to reset color to opaque black), then a second rectangle - proving the reset. Sets
color via `sc`/`SC` against the current (`DeviceRGB`) color space, asserting the expected painted
color. Renders `scn` with a trailing pattern name, asserting
`Codecs.UnsupportedImageFeatureException`. A `[Theory]` selects each unsupported named color
space (declared inline in a `/Resources/ColorSpace` dictionary built via
`BuildSinglePagePdfWithResources`) via `cs`, asserting
`Codecs.UnsupportedImageFeatureException` in every case. The end-to-end system-integration test
independently proves `rg` painting a real, hand-authored fixture, asserting a specific interior
pixel is opaque red and an exterior pixel remains transparent.

#### CanvasNetPdf-PdfDocument-FilterPipeline: Filter Pipeline Reverses PNG/TIFF Predictors, Fails Closed Otherwise

**Tests**: `PdfDocument_Images_FlateDecodePngPredictor_DecodesExpectedPixels`,
`PdfDocument_Images_FlateDecodeTiffPredictor_DecodesExpectedPixels`,
`PdfDocument_Images_UnsupportedFilter_ThrowsUnsupportedImageFeatureException`

Builds a small (2x2 pixel), hand-computed `FlateDecode` stream with a PNG predictor (mixed
`None`/`Up` filter-type rows) and, separately, a TIFF predictor (per-row horizontal-difference
encoding) as an image XObject, rendering it via `Do` and asserting every decoded pixel matches
the value independently hand-derived from the PNG specification's own defilter formulas (not
merely re-deriving the implementation's own output). Renders an image XObject declaring an
unsupported filter (`/LZWDecode`), asserting `Codecs.UnsupportedImageFeatureException`.

#### CanvasNetPdf-PdfDocument-ImageXObjects: Do Composites Images, Fails Closed on Form XObjects/Unsupported Features

**Tests**: `PdfDocument_Images_DoOperator_DeviceGrayFlateDecode_PlacesExpectedPixels`,
`PdfDocument_Images_DoOperator_DeviceRgbFlateDecode_PlacesExpectedPixels`,
`PdfDocument_Images_DoOperator_DeviceCmykFlateDecode_PlacesExpectedPixels`,
`PdfDocument_Images_DoOperator_DctDecodeJpeg_PlacesExpectedPixels`,
`PdfDocument_Images_UnsupportedBitsPerComponent_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Images_UnsupportedColorSpace_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Images_DoOperator_UndefinedXObjectName_ThrowsInvalidDataException`,
`PdfDocument_Images_DoOperator_FormXObject_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Images_DoOperator_MalformedOperandCount_ThrowsInvalidDataException`,
`CanvasNetPdf_SystemIntegration_PdfRender_ImageXObjectPlacement_CompositesExpectedPixels`

Places a small, raw 8-bit `DeviceGray`/`DeviceRGB`/`DeviceCMYK` `FlateDecode` image XObject via
`cm`/`Do`, asserting the four composited device pixels match the image's four known source
pixels (proving both the sample-to-color conversion and the unit-square-to-device-space mapping,
including the PDF image-space row-0-is-top convention). Places a bare `DCTDecode` (JPEG) image
XObject (an 8x8 solid-color surface encoded via `Codecs.JpegCodec.Save` at test-run time, quality
100 - a flat color block's DCT has only a DC coefficient, so the round-trip reproduces it within
a small per-channel tolerance), asserting the decoded/composited pixel matches within that
tolerance. Renders an image XObject with an unsupported `/BitsPerComponent` (`1`) and, separately,
an unsupported `/ColorSpace` (`Indexed`), each asserting
`Codecs.UnsupportedImageFeatureException`. Renders `Do` with a name undeclared in
`/Resources/XObject`, asserting `InvalidDataException`. Renders `Do` on a `/Subtype /Form`
XObject, asserting `Codecs.UnsupportedImageFeatureException` (not silently skipped). A `[Theory]`
renders `Do` with a malformed operand count/type, asserting `InvalidDataException` in every case.
The end-to-end system-integration test independently proves the same `Do` compositing against a
real, hand-authored fixture, asserting specific composited pixel colors at specific coordinates
matching the fixture's known 2x2 source image, and a pixel outside the placed image's
device-space footprint remains transparent.

#### CanvasNetPdf-PdfDocument-FontResolution: Font Resolution Loads Embedded FontFile2, Fails Closed Otherwise

**Tests**: `PdfDocument_Fonts_UnsupportedSubtype_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Fonts_MissingFontFile2_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Fonts_UndefinedFontName_ThrowsInvalidDataException`

A `[Theory]` builds a `/Resources/Font` dictionary declaring each excluded subtype
(`/Type0`/`/Type1`/`/MMType1`/`/Type3`) in turn and selects it via `Tf`, asserting
`Codecs.UnsupportedImageFeatureException` in every case. Builds a `/Subtype /TrueType` font
dictionary whose `/FontDescriptor` omits `/FontFile2` entirely, asserting the same exception -
proving no standard-14/system-font substitution is ever silently attempted. Selects a font name
absent from `/Resources/Font` via `Tf`, asserting `InvalidDataException`.

#### CanvasNetPdf-PdfDocument-FontEncoding: WinAnsi/MacRoman Base Encodings and Differences Overrides Resolve Correctly

**Tests**: `PdfDocument_Fonts_UnrecognizedEncoding_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Fonts_DefaultEncoding_IsWinAnsiEncoding`,
`PdfDocument_Fonts_MacRomanEncoding_DiffersFromWinAnsiEncoding`,
`PdfDocument_Fonts_Differences_OverridesBaseEncodingCode`,
`PdfDocument_Fonts_Differences_Absent_LeavesBaseEncodingCodeUnmapped`,
`PdfDocument_Fonts_Differences_UnrecognizedGlyphName_ThrowsInvalidDataException`,
`PdfDocument_Fonts_Differences_NameBeforeStartingCode_ThrowsInvalidDataException`

Selects a font declaring an unrecognized `/Encoding` base-encoding name, asserting
`Codecs.UnsupportedImageFeatureException`. Selects a font with no `/Encoding` key at all and,
separately, a font explicitly declaring `/MacRomanEncoding`, showing the byte code `0xE0` (which
diverges between the two base encodings - `WinAnsiEncoding` maps it to U+00E0, `MacRomanEncoding`
to U+2021) through a synthetic font whose `cmap` maps only U+00E0 to a real glyph, asserting the
default-encoding case paints ink and the MacRoman case does not (resolving to `.notdef`/no
paint) - proving the correct table is actually consulted, not merely that some table exists.
Declares an `/Encoding/Differences` array remapping a code to glyph name `agrave` (U+00E0) and
shows that code through a font whose `cmap` maps only U+00E0, asserting ink is painted where the
base encoding alone would not have resolved to that codepoint; separately shows a code with no
`/Differences` override present, asserting it keeps its base-encoding mapping unchanged. Declares
a `/Differences` array containing an unrecognized glyph name, asserting `InvalidDataException`
(a fail-closed policy, not a silent mis-mapping to `.notdef`); separately declares a
`/Differences` array beginning with a glyph name before any starting code number, asserting
`InvalidDataException` for that malformed array shape too.

#### CanvasNetPdf-PdfDocument-FontWidths: Advance-Width Resolution Follows the Documented Priority

**Tests**: `PdfDocument_Fonts_Widths_ExplicitEntry_DeterminesAdvance`,
`PdfDocument_Fonts_Widths_MissingWidthFallback_DeterminesAdvance`,
`PdfDocument_Fonts_Widths_FontOwnAdvanceFallback_DeterminesAdvance`

Shows two glyphs from a font declaring an explicit `/Widths` entry for the first code, asserting
the second glyph's painted device-x position matches the explicit width, not the font's own
metric. Declares a font whose `/Widths` array omits a shown code but whose `/FontDescriptor`
declares `/MissingWidth`, asserting the second glyph's position matches the `/MissingWidth`
value. Declares a font with neither `/Widths` nor `/MissingWidth` for a shown code, asserting the
second glyph's position matches the embedded font's own `GetAdvanceWidth`/`UnitsPerEm` metric.

#### CanvasNetPdf-PdfDocument-TextObjectState: BT/ET Reset Only Tm/Tlm; q/Q Save/Restore Text State

**Tests**: `PdfDocument_Text_BeginText_ResetsTextMatrixButPreservesFontAndTextState`,
`PdfDocument_Text_PushPopGraphicsState_RestoresFontSize`

Selects a font and size, moves the text position, ends the text object (`ET`), begins a new one
(`BT`) without re-selecting the font, and shows a glyph at a freshly-set position - asserting the
previously selected font/size is still in effect (painted ink appears, proving `Tf` was not lost)
while the text position reset to identity is honored (the glyph appears at the new position, not
offset by the first text object's position). Selects a font size, pushes the graphics state
(`q`), changes the font size, pops it (`Q`), and shows a glyph - asserting the glyph's painted
size matches the original (pre-`q`) font size, proving `q`/`Q` saves/restores text state exactly
like every other graphics-state parameter.

#### CanvasNetPdf-PdfDocument-TextStateOperators: Tc/Tw/Tz Apply Their Documented Spacing/Scaling Formulas

**Tests**: `PdfDocument_Text_Tc_AddsToGlyphAdvance`, `PdfDocument_Text_Tw_AppliesOnlyToCode32`,
`PdfDocument_Text_Tz_ScalesHorizontalShapeAndAdvance`

Sets a nonzero `Tc` and shows two glyphs, asserting the second glyph's device-x position is
offset by the additional character spacing beyond its own advance width. Sets a nonzero `Tw` and
shows a string containing a code-32 (space) byte followed by a non-space byte, asserting only the
byte immediately after the space is offset by the word spacing (a non-space code is never
affected). Sets `Tz` to a value other than the default `100` and shows two glyphs, asserting both
each glyph's own painted horizontal shape (narrower/wider) and its advance are scaled
accordingly, while the vertical shape (unaffected by `Th`) is unchanged.

#### CanvasNetPdf-PdfDocument-Tf: Tf Resolves the Named Font Resource and Selects Its Size

**Tests**: `PdfDocument_Text_ShowText_PaintsGlyphAtComposedTextRenderingMatrix`

Selects a font and size via `Tf`, then shows a glyph and asserts its painted device-pixel
position/size matches the composed text-rendering matrix formula independently re-derived from
the same font's own metrics (see this class's own pixel-math derivation, confirmed empirically
against the real rasterizer before being fixed into every position-dependent test's assertions).

#### CanvasNetPdf-PdfDocument-Tr: Tr Supports Fill/Invisible Modes, Fails Closed for Stroke/Clip Modes

**Tests**: `PdfDocument_Text_RenderMode3_Invisible_DoesNotPaintGlyph`,
`PdfDocument_Text_RenderMode3_Invisible_StillAdvancesTextPosition`,
`PdfDocument_Text_RenderMode_UnsupportedDefinedMode_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Text_RenderMode_OutOfDefinedRange_ThrowsInvalidDataException`

Sets `Tr 3` (invisible) and shows a glyph, asserting no ink is painted at its expected position.
Sets `Tr 3`, shows a first glyph, then shows a second glyph with `Tr 0` (fill), asserting the
second glyph's position reflects the first (invisible) glyph's own advance - proving invisible
text still moves the text position. A `[Theory]` sets `Tr` to each of the defined stroke/clip
modes (`1`/`2`/`4`/`5`/`6`/`7`) in turn, asserting `Codecs.UnsupportedImageFeatureException` in
every case; a separate `[Theory]` sets `Tr` to a value outside the specification's defined
`0`-`7` range, asserting `InvalidDataException`.

#### CanvasNetPdf-PdfDocument-TextPositioning: Td/TD/Tm/T* Compose the Text and Line Matrices Correctly

**Tests**: `PdfDocument_Text_Td_OffsetsLineMatrixNotLastTextMatrix`,
`PdfDocument_Text_TD_SetsLeadingToNegativeTy`, `PdfDocument_Text_Tm_ReplacesTextAndLineMatrix`,
`PdfDocument_Text_TStar_UsesCurrentLeading`

Issues two `Td` calls in sequence and shows a glyph after each, asserting the second glyph's
position reflects both displacements measured from the (unmoved) line matrix, not accumulated
relative to the first `Td`'s resulting text matrix. Issues `TD` and shows a glyph, then issues
`T*` (with no intervening `TL`) and shows a second glyph, asserting the second glyph's vertical
position reflects the leading `TD` implicitly set (`Leading = -ty`). Issues `Tm` with an explicit
matrix and shows a glyph, asserting its position matches that matrix directly (a replacement, not
a composition with any prior text/line matrix). Sets `TL` explicitly, issues `T*`, and shows a
glyph, asserting its position matches exactly `0 -TL Td`'s own effect.

#### CanvasNetPdf-PdfDocument-TextShowing: Tj/'/"/TJ Paint the Correct Glyphs at the Correct Positions

**Tests**: `PdfDocument_Text_ShowText_PaintsGlyphAtComposedTextRenderingMatrix`,
`PdfDocument_Text_TJ_AppliesPositionAdjustments`,
`PdfDocument_Text_TJ_ArrayEntryNotStringOrNumber_ThrowsInvalidDataException`,
`PdfDocument_Text_QuoteOperator_MovesToNextLineThenShows`,
`PdfDocument_Text_DoubleQuoteOperator_SetsSpacingThenMovesAndShows`,
`CanvasNetPdf_SystemIntegration_PdfRender_EmbeddedTrueTypeFontText_PaintsGlyphStrokesNotCounters`

Shows a string via `Tj`, asserting the painted glyph's device-pixel position/size matches the
composed text-rendering matrix. Shows a `TJ` array containing a numeric position adjustment
between two strings, asserting the second string's glyphs are shifted by exactly that
adjustment's thousandths-of-text-space amount (scaled by `Tfs`/`Th`), beyond the preceding
glyph's own advance. A `TJ` array containing an element that is neither a string nor a number
asserts `InvalidDataException`. Issues `Td` then `'`, asserting the shown text is positioned
exactly as `T*` followed by `Tj` would place it. Issues `"` with explicit word/character spacing
operands, asserting both spacing parameters are applied to the subsequently shown text exactly as
setting `Tw`/`Tc` then issuing `'` would. The end-to-end system-integration test independently
proves the same glyph-painting pipeline against a real, hand-authored fixture embedding the real
`OpenSans-Regular.ttf` production font, re-deriving expected ink/counter/background pixel
positions from the font's own outline metrics rather than hardcoded numbers.

#### CanvasNetPdf-PdfDocument-TextErrorHandling: No-Font-Selected and Malformed-Operand Text Operators Fail Closed

**Tests**: `PdfDocument_Text_ShowText_NoFontSelected_ThrowsInvalidDataException`,
`PdfDocument_Text_MalformedOperandCount_ThrowsInvalidDataException`

A `[Theory]` shows text via each of `Tj`/`'`/`"` with no preceding `Tf` call, asserting
`InvalidDataException` in every case. A large `[Theory]` supplies a malformed operand count/type
to each text operator in turn (including a trailing garbage-operand `ET`, a wrong-arity `Tf`, and
out-of-range operand counts for `Td`/`Tm`), asserting `InvalidDataException` in every case,
matching every other operator family's own established convention.

## Acceptance Criteria

A unit-level test run passes when all scenarios above pass without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong return/field
value constitutes a failure.
