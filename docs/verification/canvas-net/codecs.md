## Codecs Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Codecs` subsystem
(the `BmpCodec`, `PngCodec`, `TiffCodec`, `JpegCodec`, `GifCodec`, `Jpeg2000Codec`, and `ImageDecoder` units).

Note: SVG rasterization verification is not part of this subsystem's verification; it is
provided by the separate `CanvasNetSvg` system (its own package,
`DemaConsulting.CanvasNet.Svg`) — see *CanvasNetSvg System Verification Design*
(`../canvas-net-svg.md`) and *SvgCodec Unit Verification Design*
(`../canvas-net-svg/svg-codec.md`).

### Verification Approach

The `Codecs` subsystem is verified through its six constituent units' tests (see
*BmpCodec Unit Verification Design*, *PngCodec Unit Verification Design*,
*TiffCodec Unit Verification Design*, *JpegCodec Unit Verification Design*,
*GifCodec Unit Verification Design*, and *Jpeg2000Codec Unit Verification Design* under `codecs/`),
together with the system-level round-trip (or, for the decode-only `GifCodec`,
load-only) integration tests in `CanvasNetTests.cs` that exercise each codec end-to-end against a
`Surface`. No separate subsystem-level tests otherwise exist; the subsystem-level requirements
reuse the corresponding unit and system-integration tests as verification evidence.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `BmpCodec`, `PngCodec`, `TiffCodec`, `JpegCodec`, `GifCodec`, and
  `Jpeg2000Codec`'s only dependency is the in-house `Canvas` subsystem's `Surface` unit

### Acceptance Criteria

The `Codecs` subsystem's verification passes when every unit test scenario described in the six
codec unit verification documents under `codecs/`, and every `CanvasNet_SystemIntegration_*`
round-trip/load test referenced by the `Codecs` subsystem requirements, pass without error or
unexpected exception.

### Test Scenarios

The `Codecs` subsystem's test scenarios are those named in each of the six constituent unit
verification documents under `codecs/` (*BmpCodec*, *PngCodec*, *TiffCodec*, *JpegCodec*, *GifCodec*, and
*Jpeg2000Codec Unit Verification Design*), covering `Load`/`Save`/`GetInfo` normal-path round-trips,
malformed/truncated-input rejection, and `ImageInfo.CanDecode`/`UnsupportedImageFeatureException`
well-formed-but-unsupported handling, plus the system-level `CanvasNet_SystemIntegration_*`
round-trip (or, for `GifCodec`, load-only) tests that exercise each codec end-to-end against a
`Surface` (see the system verification design, `../canvas-net.md`).

The internal `ZlibHelper` shared by `PngCodec` and `TiffCodec` is not a separate software unit and
has no tests of its own; it is verified through the decompression-bound scenarios in the
`PngCodec` and `TiffCodec` unit verification documents
(`CanvasNet-Codecs-PngCodec-LoadDecompressionBound` and
`CanvasNet-Codecs-TiffCodec-LoadDecompressionBound`), which exercise its hard output limit, and
through every valid-fixture decode that exercises its checksum validation.

The `ImageDecoder` unit is verified by `ImageFormatsTests` and `ImageDecoderTests` (see *ImageDecoder
Unit Verification Design*, `codecs/image-decoder.md`). Likewise, the internal `PngFilter` and
`LzwDecoder` are not software units; they are verified by direct tests (`PngFilterTests`,
`LzwDecoderTests`) cited from the `PngCodec` filter-reconstruction and `TiffCodec` LZW and
decompression-bound scenarios, and by the PDF package's own predictor and `LZWDecode` tests, which
exercise the same shared code through the PDF filter pipeline.
