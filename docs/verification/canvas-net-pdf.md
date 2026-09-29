# System Verification Design

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

## Acceptance Criteria

A system-level test run passes when all scenarios above pass without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong return value
constitutes a failure.
