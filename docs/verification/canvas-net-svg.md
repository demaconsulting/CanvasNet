# System Verification Design

<!-- cspell:ignore rasterizing -->

This document describes the system-level verification strategy for CanvasNetSvg.

## Verification Approach

The CanvasNetSvg system is verified through system-level integration tests that
exercise the library as a whole from the perspective of a consumer. Tests instantiate the library
using its public API and assert on observable outputs, without relying on knowledge of internal
implementation details. No mocking or stubbing is required at the system level — the entire
integrated system is exercised as it would be used by a real caller.

System tests reside in `SvgSystemIntegrationTests.cs` within the
`DemaConsulting.CanvasNet.Svg.Tests` project.

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

### Integration: Svg Load Returns Expected Pixel

**Test**: `CanvasNet_SystemIntegration_SvgLoad_ReturnsExpectedPixel`

Exercises end-to-end system behavior for the `SvgCodec` unit: rasterizes a minimal SVG document
(a `viewBox` matching the requested raster exactly, containing one rectangle filled with a
distinct, fully opaque color via a plain `fill` presentation attribute) into a new `Surface`
through the public API. Asserts the rasterized pixel at the rectangle's center exactly matches
the source color, confirming the system's public SVG load API integrates correctly with the
`CanvasNet` system's `Surface`/`Geometry`/`Drawing` units it depends on. Unlike the BMP/PNG/TIFF/
JPEG system-integration scenarios in _CanvasNet System Verification_ (`canvas-net.md`), there is
no "Save" half to this round-trip: `SvgCodec` is decode/rasterize-only.

### Integration: Svg Load With Css Style Element Returns Expected Pixel

**Test**: `CanvasNet_SystemIntegration_SvgLoadWithCssStyleElement_ReturnsExpectedPixel`

Exercises end-to-end system behavior for the `SvgCodec` unit's CSS engine: rasterizes an SVG
document whose only source of color is a `<style>` element's class selector rule (`.solid {
fill: rgb(11,22,33); }`) applied to a `rect` that itself carries no `fill` attribute at all,
integrating style-element parsing, selector matching, and cascade resolution with the rest of the
rendering pipeline, through the public API. Asserts the rasterized pixel at the rectangle's
center exactly matches the stylesheet-declared color, confirming the CSS engine is correctly
wired into the same rasterization pipeline the plain-attribute scenario above exercises.

## Acceptance Criteria

A system-level test run passes when both scenarios above pass without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong return value
constitutes a failure.
