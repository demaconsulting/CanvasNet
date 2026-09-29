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
Cross-reference form, linear-scan fallback, page-tree traversal/inheritance/cycle-rejection,
`/Encrypt` detection, and public-API (`Open`/`PageCount`/`GetPageInfo`/`Render`/`Dispose`) tests
exercise the same hand-authored, byte-exact fixture files under `PdfFixtures/` (see
`PdfFixtures/README.md` for provenance) through the public API only. Because `PdfDocument`'s
dependencies (`Canvas.Surface`, `Codecs.UnsupportedImageFeatureException`, `Geometry.PathBuilder`,
`Drawing.PathFiller`/`PathStroker`) are all sibling in-house types, not external services, no
mocking or stubbing is required. Tests assert on parsed token/object field values, on
`PageCount`/`PdfPageInfo` field values, on `Surface` pixel/dimension values, and on thrown
exception types (and, for `UnsupportedImageFeatureException`, its `Feature` token) - never on "no
exception thrown" alone, so every test can actually fail if the implementation is wrong.

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

Renders a content stream containing an unrecognized color operator (`RG`) immediately followed by
a recognized stroke operator, asserting the stroke still painted its expected pixel (proving the
unknown operator was silently skipped rather than aborting the whole stream). The end-to-end
system-integration test independently proves the same dispatch loop against a real, hand-authored
fixture containing both a filled rectangle and a stroked line, asserting specific opaque-black and
transparent pixels at specific coordinates.

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

## Acceptance Criteria

A unit-level test run passes when all scenarios above pass without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong return/field
value constitutes a failure.
