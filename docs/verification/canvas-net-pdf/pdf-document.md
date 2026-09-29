## PdfDocument Unit Verification Design

<!-- cspell:ignore xref startxref endobj endstream ObjStm MediaBox -->

This document describes the unit-level verification strategy for the `PdfDocument` class.

`PdfDocument` is distributed as the separate `DemaConsulting.CanvasNet.Pdf` NuGet package
(namespace `DemaConsulting.CanvasNet.Pdf`), which references the core `DemaConsulting.CanvasNet`
package; its unit tests live in the sibling `DemaConsulting.CanvasNet.Pdf.Tests` project.

### Verification Approach

The `PdfDocument` unit is verified through unit tests that exercise its tokenizer, object model,
and public API in isolation. Tokenizer and object-model tests (`PdfDocumentTests.cs`) construct
small, hand-written byte sequences directly (using the `internal`, `InternalsVisibleTo`-exposed
`PdfTokenizer`/`PdfObject` types) for controlled, targeted coverage of individual lexical and
structural rules without needing a full, valid PDF file for every case. Cross-reference form,
linear-scan fallback, page-tree traversal/inheritance/cycle-rejection, `/Encrypt` detection, and
public-API (`Open`/`PageCount`/`GetPageInfo`/`Render`/`Dispose`) tests exercise the same
hand-authored, byte-exact fixture files under `PdfFixtures/` (see `PdfFixtures/README.md` for
provenance) through the public API only. Because `PdfDocument`'s Phase 1 dependencies
(`Canvas.Surface`, `Codecs.UnsupportedImageFeatureException`) are all sibling in-house types, not
external services, no mocking or stubbing is required. Tests assert on parsed token/object field
values, on `PageCount`/`PdfPageInfo` field values, on `Surface` pixel/dimension values, and on
thrown exception types (and, for `UnsupportedImageFeatureException`, its `Feature` token) - never
on "no exception thrown" alone, so every test can actually fail if the implementation is wrong.

Unit tests reside in `PdfDocumentTests.cs` within the `DemaConsulting.CanvasNet.Pdf.Tests`
project.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `PdfDocument`'s Phase 1 dependencies are all in-house types
  (`Canvas.Surface`, `Codecs.UnsupportedImageFeatureException`)
- **Isolation**: Each test method builds its own byte sequence or opens its own fixture file from
  `PdfFixtures/`; no shared state between tests

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

#### CanvasNetPdf-PdfDocument-Render: Render Returns Correctly Sized Blank Surface and Validates Arguments

**Tests**: `PdfDocument_Render_ValidPageIndex_ReturnsCorrectlySizedBlankSurface`,
`PdfDocument_Render_OutOfRangePageIndex_ThrowsArgumentOutOfRangeException`,
`PdfDocument_Render_InvalidWidth_PropagatesSurfaceArgumentOutOfRangeException`

Calls `Render` with a valid page index and caller-chosen size, asserting the returned `Surface`
has exactly the requested dimensions and every pixel is the default (fully transparent) value.
Calls `Render` with an out-of-range page index and a non-positive width, asserting
`ArgumentOutOfRangeException` in both cases (the latter propagated unwrapped from `Surface`'s own
constructor).

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
