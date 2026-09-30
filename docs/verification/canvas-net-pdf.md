# System Verification Design

<!-- cspell:ignore Zapf -->

This document describes the system-level verification strategy for CanvasNetPdf.

## Verification Approach

The CanvasNetPdf system is verified through system-level integration tests that exercise the
library as a whole from the perspective of a consumer. Tests instantiate the library using its
public API and assert on observable outputs, without relying on knowledge of internal
implementation details. No mocking or stubbing is required at the system level — the entire
integrated system is exercised as it would be used by a real caller.

System tests reside in `PdfSystemIntegrationTests.cs` within the
`DemaConsulting.CanvasNet.Pdf.Tests` project.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, databases, or network access required
- **Isolation**: Each test method constructs its own test fixtures; no shared state between tests

## External Interface Simulation

The system has no external network or service interfaces requiring simulation - no HTTP calls,
databases, or remote services are involved. It does support local file-path-based I/O through its
public API (`Open(string path)` reads a PDF document from a local file path), which system tests
exercise against the hand-authored fixture files in `PdfFixtures/`. System tests call the public
API directly with controlled inputs and verify returned values and thrown exceptions.

## System-Level Test Scenarios

### Integration: Pdf Open Returns Expected Page Count

**Test**: `CanvasNetPdf_SystemIntegration_PdfOpen_ReturnsExpectedPageCount`

Exercises end-to-end system behavior for `Open`/`PageCount`/`GetPageInfo`/`Render`: opens a
multi-page fixture once through the public API, asserts `PageCount`, then calls `GetPageInfo` for
two different pages and `Render` for two different pages against the *same* opened instance.
Confirms the "parse once, reused across every subsequent call" property — one no stateless
design could express — and serves as the shared platform-proof test referenced by every
`CanvasNetPdf-Platform-*` requirement.

### Integration: Pdf Get Page Info Reports Rotated Dimensions

**Test**: `CanvasNetPdf_SystemIntegration_PdfGetPageInfo_ReportsRotatedDimensions`

Exercises end-to-end system behavior for rotation-adjusted page sizing: opens a fixture
containing a page with a non-zero `/Rotate` value through the public API, then calls
`GetPageInfo`. Asserts the reported `Width`/`Height` are swapped relative to the page's raw
`/MediaBox` and `Rotation` reports the effective normalized value, confirming page-tree
inheritance and rotation normalization integrate correctly with the public API.

### Integration: Pdf Render Returns Blank Sized Surface

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_ReturnsBlankSizedSurface`

Exercises end-to-end system behavior for `Render`: calls the public `Render` API for a valid page
index and caller-chosen size, against a fixture with no `/Contents`. Asserts the returned
`Surface` has exactly the requested dimensions and every pixel is the default (fully transparent)
value, confirming the documented "a page with no content renders blank" behavior is honored at
the system's own public entry point.

### Integration: Pdf Render Filled Rectangle And Stroked Line Paints Expected Pixels

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_FilledRectangleAndStrokedLine_PaintsExpectedPixels`

Exercises end-to-end system behavior for the Phase 2 content-stream interpreter: calls the public
`Render` API against a hand-authored fixture containing a filled rectangle and a stroked vertical
line. Asserts specific opaque-black pixels inside the rectangle and along the stroked line, and
specific fully-transparent pixels away from both, confirming real path geometry is rasterized to
the correct device-pixel positions through the system's own public entry point (not merely that
"something non-blank" was painted).

### Integration: Pdf Render Rotated Page Maps Geometry To Correct Pixel Position

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_RotatedPage_MapsGeometryToCorrectPixelPosition`

Exercises end-to-end system behavior for the base CTM's rotation handling: calls the public
`Render` API against a hand-authored, `/Rotate 90` fixture containing a deliberately asymmetric
filled rectangle, at the fixture's own rotation-swapped display size. Asserts the rectangle's
interior is opaque black at its mathematically correct rotated position, and asserts a specific
pixel where a 270-instead-of-90 rotation-sign regression would incorrectly paint it is untouched,
confirming `/MediaBox`/`/Rotate`-to-device-pixel-space correctness through the system's own public
entry point.

### Integration: Pdf Validation Null Null Stream Throws Argument Null Exception

**Test**: `CanvasNetPdf_SystemIntegration_PdfValidationNull_NullStreamThrowsArgumentNullException`

Exercises end-to-end system behavior for null-argument validation: calls the public `Open` API
with a null stream. Asserts `ArgumentNullException` is thrown, confirming the documented
validation contract is honored at the system's own public entry point, before any parsing is
attempted.

### Integration: Pdf Dispose Object Disposed Exception After Dispose

**Test**: `CanvasNetPdf_SystemIntegration_PdfDispose_ObjectDisposedExceptionAfterDispose`

Exercises end-to-end system behavior for disposal: opens a fixture through the public API, calls
`Dispose()` twice (confirming idempotency), then calls `PageCount`, `GetPageInfo`, and `Render`.
Asserts each throws `ObjectDisposedException`, confirming the documented disposal contract is
honored across every other public member.

### Integration: Pdf Encrypt Detection Encrypted Trailer Throws Unsupported Image Feature Exception

**Test**: `CanvasNetPdf_SystemIntegration_PdfEncryptDetection_EncryptedTrailerThrowsUnsupportedImageFeatureException`

Exercises end-to-end system behavior for `/Encrypt` detection: calls the public `Open` API
against a fixture whose trailer contains an `/Encrypt` key. Asserts
`Codecs.UnsupportedImageFeatureException` is thrown with `Feature == "pdf-encrypted"`, confirming
an encrypted document is rejected rather than silently mis-parsed at the system's own public
entry point.

### Integration: Pdf Render Colored Rectangle Fill Paints Expected Rgb Pixels

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_ColoredRectangleFill_PaintsExpectedRgbPixels`

Exercises end-to-end system behavior for the Phase 3 device-color operators: calls the public
`Render` API against a hand-authored fixture using `rg` to fill a rectangle opaque red. Asserts a
specific interior pixel is opaque red and a specific exterior pixel remains transparent,
confirming real device color (not merely solid opaque black) is rasterized through the system's
own public entry point.

### Integration: Pdf Render Image X Object Placement Composites Expected Pixels

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_ImageXObjectPlacement_CompositesExpectedPixels`

Exercises end-to-end system behavior for the Phase 3 image-XObject pipeline: calls the public
`Render` API against a hand-authored fixture placing a 2x2 `DeviceRGB` `FlateDecode` image
XObject via `cm`/`Do`. Asserts the four composited device pixels match the fixture's known source
image quadrants, and a pixel outside the placed image's device-space footprint remains
transparent, confirming stream decoding, sample-to-color conversion, and unit-square-to-device
compositing all integrate correctly through the system's own public entry point.

### Integration: Pdf Render Embedded True Type Font Text Paints Glyph Strokes Not Counters

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_EmbeddedTrueTypeFontText_PaintsGlyphStrokesNotCounters`

Exercises end-to-end system behavior for the Phase 4 text-rendering pipeline: calls the public
`Render` API against a hand-authored fixture with a real, embedded (`/FontDescriptor/FontFile2`)
copy of the shared `OpenSans-Regular.ttf` production font, drawing `"HO"` at font size 60 with
the default `/WinAnsiEncoding` via `BT`/`Tf`/`Td`/`Tj`/`ET`. Rather than hardcoding font-specific
pixel numbers, the test independently loads the same real font through
`Fonts.TrueTypeFont` and re-derives the expected device-pixel positions of real glyph ink from
the font's own outline/metrics via the documented text-rendering-matrix formula. Asserts a pixel
inside `'H'`'s left stroke is opaque, a pixel at the exact center of `'O'`'s bounding box (its
hollow counter) is transparent, and the canvas's far corners remain transparent, confirming
font-dictionary resolution, `/Encoding` mapping, glyph-outline transformation, and glyph painting
all integrate correctly through the system's own public entry point with a real font file (not a
hand-rolled synthetic one).

### Integration: Pdf Render Lzw Decode Content Stream Paints Expected Pixels

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_LzwDecodeContentStream_PaintsExpectedPixels`

Exercises end-to-end system behavior for the Phase 7 `LZWDecode` content-stream filter: calls the
public `Render` API against a hand-authored fixture whose `/Contents` stream is PDF-variant
LZW-compressed. Asserts the same interior-opaque-red/exterior-transparent pixel pattern as the
uncompressed device-color fixture, confirming the decoded operator text is parsed and rendered
identically to an uncompressed content stream through the system's own public entry point.

### Integration: Pdf Render Ascii85 Decode Content Stream Paints Expected Pixels

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_Ascii85DecodeContentStream_PaintsExpectedPixels`

Exercises end-to-end system behavior for the Phase 7 `ASCII85Decode` content-stream filter: calls
the public `Render` API against a hand-authored fixture whose `/Contents` stream is base-85
armored (terminated by `~>`). Asserts the same interior-opaque-red/exterior-transparent pixel
pattern, confirming `ASCII85Decode` decoding integrates correctly through the system's own public
entry point.

### Integration: Pdf Render Ascii Hex Decode Content Stream Paints Expected Pixels

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_AsciiHexDecodeContentStream_PaintsExpectedPixels`

Exercises end-to-end system behavior for the Phase 7 `ASCIIHexDecode` content-stream filter:
calls the public `Render` API against a hand-authored fixture whose `/Contents` stream is
hex-digit-pair armored (terminated by `>`). Asserts the same interior-opaque-red/
exterior-transparent pixel pattern, confirming `ASCIIHexDecode` decoding integrates correctly
through the system's own public entry point.

### Integration: Pdf Render Run Length Decode Content Stream Paints Expected Pixels

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_RunLengthDecodeContentStream_PaintsExpectedPixels`

Exercises end-to-end system behavior for the Phase 7 `RunLengthDecode` content-stream filter:
calls the public `Render` API against a hand-authored fixture whose `/Contents` stream is a
single PackBits-style literal run. Asserts the same interior-opaque-red/exterior-transparent
pixel pattern, confirming `RunLengthDecode` decoding integrates correctly through the system's
own public entry point.

### Integration: Render Standard14 Helvetica Without Embedded Font Paints Visible Glyph Ink

**Test**: `CanvasNetPdf_SystemIntegration_RenderStandard14HelveticaWithoutEmbeddedFont_PaintsVisibleGlyphInk`

Exercises end-to-end system behavior for the Phase 6 font-fallback substitution path: calls the
public `Render` API against a synthetic, in-memory single-page document declaring a
`/BaseFont /Helvetica` font resource with no embedded `/FontFile2`. Since the actual substitute
font (a matching system font, or the bundled Liberation Sans fallback) genuinely varies across
the Windows/Linux/macOS CI matrix, asserts the strongest platform-independent property: at least
one visibly-painted (non-transparent) pixel appears somewhere on the canvas, confirming the
fallback path genuinely renders a substitute glyph rather than merely not throwing.

### Integration: Render Symbol Font Without Embedded Font Throws Unsupported Image Feature Exception

**Test**: `CanvasNetPdf_SystemIntegration_RenderSymbolFontWithoutEmbeddedFont_ThrowsUnsupportedImageFeatureException`

Exercises end-to-end system behavior for the Phase 6 font-fallback fail-closed boundary: calls
the public `Render` API against a synthetic, in-memory single-page document declaring a
`/BaseFont /Symbol` font resource with no embedded `/FontFile2`. Asserts
`Codecs.UnsupportedImageFeatureException` is thrown with `Feature == "pdf-font-symbolic-not-embedded"`,
confirming Symbol/ZapfDingbats fonts are never substituted with an unrelated system or bundled
font through the system's own public entry point.

### Integration: Pdf Validation Empty Path Empty Path Throws Argument Exception

**Test**: `CanvasNetPdf_SystemIntegration_PdfValidationEmptyPath_EmptyPathThrowsArgumentException`

Exercises end-to-end system behavior for empty-path-argument validation: calls the public
`Open(string)` API with `string.Empty`. Asserts `ArgumentException` is thrown, confirming the
documented validation contract is honored at the system's own public entry point, before any
file access is attempted.

### Integration: Pdf Unsupported Format Validation Malformed Content Stream Throws Invalid Data Exception

**Test**: `CanvasNetPdf_SystemIntegration_PdfUnsupportedFormatValidation_MalformedContentStreamThrowsInvalidDataException`

Exercises end-to-end system behavior for content-stream-level malformed-data validation: calls
the public `Render` API against a hand-authored fixture whose otherwise well-formed
document/xref/page-tree structure has a `/Contents` stream containing a lexically malformed
operator (a `re` given only 2 of its 4 required operands). Asserts `InvalidDataException` is
thrown, confirming a content-stream-level malformation (distinct from the structural
malformations already covered by `malformed-startxref.pdf`/`cyclic-page-tree.pdf`) is rejected at
the system's own public entry point.

### Integration: Pdf Get Page Info Validation Out Of Range Page Index Throws Argument Out Of Range Exception

**Test**: `CanvasNetPdf_SystemIntegration_PdfGetPageInfoValidation_OutOfRangePageIndexThrowsArgumentOutOfRangeException`

Exercises end-to-end system behavior for out-of-range-page-index validation: calls the public
`GetPageInfo` API against a single-page fixture with an out-of-range page index. Asserts
`ArgumentOutOfRangeException` is thrown, confirming the documented validation contract is honored
at the system's own public entry point.

## Acceptance Criteria

A system-level test run passes when all scenarios above pass without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong return value
constitutes a failure. Collectively, these scenarios cover the complete current CanvasNetPdf
feature set: document structure parsing, the content-stream interpreter, device color, image
XObjects, the full `FlateDecode`/`LZWDecode`/`ASCII85Decode`/`ASCIIHexDecode`/`RunLengthDecode`
stream-filter set, embedded-TrueType and automatically-substituted-fallback text rendering, and
the documented validation-contract/fail-closed exception boundaries.
