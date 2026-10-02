## Codecs Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Codecs` subsystem
(the `BmpCodec`, `PngCodec`, `TiffCodec`, `JpegCodec`, and `GifCodec` units).

Note: SVG rasterization verification was previously modeled as a sixth unit of this subsystem's
verification, but is now provided by the separate `CanvasNetSvg` system (its own package,
`DemaConsulting.CanvasNet.Svg`) — see _CanvasNetSvg System Verification Design_
(`../canvas-net-svg.md`) and _SvgCodec Unit Verification Design_
(`../canvas-net-svg/svg-codec.md`).

### Verification Approach

The `Codecs` subsystem is verified through its five constituent units' tests (see
_BmpCodec Unit Verification Design_, _PngCodec Unit Verification Design_,
_TiffCodec Unit Verification Design_, _JpegCodec Unit Verification Design_, and
_GifCodec Unit Verification Design_ under `codecs/`),
together with the system-level round-trip (or, for the decode-only `GifCodec`,
load-only) integration tests in `CanvasNetTests.cs` that exercise each codec end-to-end against a
`Surface`. No separate subsystem-level tests otherwise exist; the subsystem-level requirements
reuse the corresponding unit and system-integration tests as verification evidence.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `BmpCodec`, `PngCodec`, `TiffCodec`, `JpegCodec`, and `GifCodec`'s
  only dependency is the in-house `Canvas` subsystem's `Surface` unit

### Acceptance Criteria

The `Codecs` subsystem's verification passes when every unit test scenario described in the five
codec unit verification documents under `codecs/`, and every `CanvasNet_SystemIntegration_*`
round-trip/load test referenced by the `Codecs` subsystem requirements, pass without error or
unexpected exception.

### Test Scenarios

The `Codecs` subsystem's test scenarios are those named in each of the five constituent unit
verification documents under `codecs/` (_BmpCodec_, _PngCodec_, _TiffCodec_, _JpegCodec_, and
_GifCodec Unit Verification Design_), covering `Load`/`Save`/`GetInfo` normal-path round-trips,
malformed/truncated-input rejection, and `ImageInfo.CanDecode`/`UnsupportedImageFeatureException`
well-formed-but-unsupported handling, plus the system-level `CanvasNet_SystemIntegration_*`
round-trip (or, for `GifCodec`, load-only) tests that exercise each codec end-to-end against a
`Surface` (see the system verification design, `../canvas-net.md`).
