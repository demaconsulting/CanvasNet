# System Verification Design

This document describes the system-level verification strategy for CanvasNet.

## Verification Approach

The CanvasNet system is verified through system-level integration tests that
exercise the library as a whole from the perspective of a consumer. Tests instantiate the library
using its public API and assert on observable outputs, without relying on knowledge of internal
implementation details. No mocking or stubbing is required at the system level — the entire
integrated system is exercised as it would be used by a real caller.

System tests reside in `CanvasNetTests.cs` within the
`DemaConsulting.CanvasNet.Tests` project.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, databases, or network access required
- **Isolation**: Each test method constructs its own test fixtures; no shared state between tests

## External Interface Simulation

The system has no external interfaces requiring simulation. It is a pure in-process .NET library
with no I/O, network calls, or platform services. System tests call the public API directly
with controlled inputs and verify returned values and thrown exceptions.

## System-Level Test Scenarios

### Integration: Canvas Construct and Set Pixel Returns Expected Pixel

**Test**: `CanvasNet_SystemIntegration_CanvasConstructAndSetPixel_ReturnsExpectedPixel`

Exercises end-to-end system behavior for the `Surface` unit: constructs a `Surface` and sets a
pixel through the public indexer, then reads it back. Asserts the read value exactly matches the
stored value, confirming the system's public pixel-access API integrates correctly.

### Integration: Canvas Crop Returns an Independent Sub-Region

**Test**: `CanvasNet_SystemIntegration_CanvasCrop_ReturnsIndependentSubRegion`

Constructs a `Surface`, sets a distinct pixel within a region, crops that region, then mutates the
source surface. Asserts the cropped result retains the pixel value captured at crop time,
confirming that `Crop` produces an independent copy at the system level.

### Integration: BMP Save Then Load Returns Expected Pixel

**Test**: `CanvasNet_SystemIntegration_BmpSaveThenLoad_ReturnsExpectedPixel`

Exercises end-to-end system behavior across the `Surface` and `BmpCodec` units: constructs a
`Surface`, sets a distinct pixel, saves it to an in-memory BMP stream via `BmpCodec.Save`, and
loads it back via `BmpCodec.Load`. Asserts the reloaded pixel matches the original, confirming
that the system's public BMP save/load API integrates correctly with `Surface`.

### Integration: PNG Save Then Load Returns Expected Pixel

**Test**: `CanvasNet_SystemIntegration_PngSaveThenLoad_ReturnsExpectedPixel`

Exercises end-to-end system behavior across the `Surface` and `PngCodec` units: constructs a
`Surface`, sets a distinct pixel, saves it to an in-memory PNG stream via `PngCodec.Save`, and
loads it back via `PngCodec.Load`. Asserts the reloaded pixel matches the original, confirming
that the system's public PNG save/load API integrates correctly with `Surface`.

### Integration: TIFF Save Then Load Returns Expected Pixel

**Test**: `CanvasNet_SystemIntegration_TiffSaveThenLoad_ReturnsExpectedPixel`

Exercises end-to-end system behavior across the `Surface` and `TiffCodec` units: constructs a
`Surface`, sets a distinct pixel, saves it to an in-memory TIFF stream via `TiffCodec.Save`, and
loads it back via `TiffCodec.Load`. Asserts the reloaded pixel matches the original, confirming
that the system's public TIFF save/load API integrates correctly with `Surface`.

### Integration: JPEG Save Then Load Returns Expected Pixel

**Test**: `CanvasNet_SystemIntegration_JpegSaveThenLoad_ReturnsExpectedPixel`

Exercises end-to-end system behavior across the `Surface` and `JpegCodec` units: constructs a
`Surface`, sets a distinct pixel, saves it to an in-memory JPEG stream via `JpegCodec.Save`, and
loads it back via `JpegCodec.Load`. Unlike the BMP/PNG/TIFF scenarios above, this scenario uses a
per-channel tolerance rather than exact equality because JPEG is lossy; passing requires every RGB
channel of the reloaded pixel to remain within the documented tolerance bound while alpha remains
opaque.

## Acceptance Criteria

A system-level test run passes when all six scenarios above pass without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong return value
constitutes a failure.
