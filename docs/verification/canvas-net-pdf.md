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
index and caller-chosen size. Asserts the returned `Surface` has exactly the requested
dimensions and every pixel is the default (fully transparent) value, confirming Phase 1's
documented "blank page" behavior is honored at the system's own public entry point.

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

## Acceptance Criteria

A system-level test run passes when all scenarios above pass without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong return value
constitutes a failure.
