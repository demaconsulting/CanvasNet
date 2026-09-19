## JpegCodec Unit Verification Design

This document describes the unit-level verification strategy for the `JpegCodec` class.

### Verification Approach

The `JpegCodec` unit is verified through unit tests that exercise `Load` and `Save` in isolation,
using `MemoryStream` for all in-memory round-trip and malformed-stream checks. Several tests build
raw JPEG-like byte streams by hand, using independent test-only marker, segment, and entropy-data
helpers rather than `JpegCodec`'s own encoder, so malformed-stream rejection paths are exercised
against JPEG marker structure itself rather than only by round-tripping production output. Because
`JpegCodec`'s only documented dependency is `Surface` (a sibling in-house unit, not an external
service), no mocking or stubbing is required.

Unit tests reside in `JpegCodecTests.cs` within the `DemaConsulting.CanvasNet.Tests` project.
Conformance testing against the `JpegFixtures` corpus (five real-world files generated with
ImageMagick from the PngSuite corpus) resides separately in `JpegFixtureTests.cs` within the same
project (see `JpegFixtures\README.md` for corpus provenance).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `JpegCodec`'s only dependency is the in-house `Surface` unit
- **Isolation**: Each test method builds its own `Surface` and/or JPEG byte array; no shared state
  between tests

### Unit-Level Test Scenarios

#### CanvasNet-Codecs-JpegCodec-SaveLoadBaseline420: Baseline 4:2:0 Save Then Load Stays Within Tolerance

**Tests**: `JpegCodec_SaveThenLoad_AlignedCanvas_RoundTripsWithinTolerance`,
`JpegCodec_SaveThenLoad_OddSizedCanvas_RoundTripsWithinTolerance`,
`JpegCodec_SaveThenLoad_QualityEndpoints_Succeed`

Saves synthetic surfaces through the only encoder path (baseline, 3-component YCbCr, 4:2:0),
loads them back, and asserts the decoded dimensions remain correct and the RGB channels stay
within the documented per-channel tolerance, including odd image sizes and the documented quality
endpoints 1 and 100.

#### CanvasNet-Codecs-JpegCodec-LoadBaseline444: Baseline 4:4:4 Fixtures Decode Within Tolerance

**Tests**: `JpegCodec_Load_Fixture_ReturnsCanvasWithExpectedDimensions`,
`JpegCodec_Load_ColorFixture_MatchesSourcePngWithinTolerance`

Loads the baseline 4:4:4 fixture from `JpegFixtures`, asserting the expected dimensions and that
its decoded RGB pixels remain within the documented tolerance of the PngSuite source image.

#### CanvasNet-Codecs-JpegCodec-LoadBaseline422: Baseline 4:2:2 Fixtures Decode Within Tolerance

**Tests**: `JpegCodec_Load_Fixture_ReturnsCanvasWithExpectedDimensions`,
`JpegCodec_Load_ColorFixture_MatchesSourcePngWithinTolerance`

Loads the baseline 4:2:2 fixture from `JpegFixtures`, asserting the expected dimensions and that
its decoded RGB pixels remain within the documented tolerance of the PngSuite source image.

#### CanvasNet-Codecs-JpegCodec-LoadProgressive: Progressive JPEG Fixtures Decode Within Tolerance

**Tests**: `JpegCodec_Load_Fixture_ReturnsCanvasWithExpectedDimensions`,
`JpegCodec_Load_ColorFixture_MatchesSourcePngWithinTolerance`

Loads the progressive 4:2:0 fixture from `JpegFixtures`, asserting the expected dimensions and
that the completed multi-scan decode remains within the documented tolerance of the PngSuite
source image.

#### CanvasNet-Codecs-JpegCodec-LoadGrayscale: Grayscale JPEG Expands to Equal RGB Channels

**Tests**: `JpegCodec_Load_Fixture_ReturnsCanvasWithExpectedDimensions`,
`JpegCodec_Load_GrayscaleFixture_HasEqualRgbChannels`

Loads the grayscale baseline fixture, asserts the expected dimensions, and verifies every decoded
pixel satisfies `R == G == B` with alpha forced to 255.

#### CanvasNet-Codecs-JpegCodec-SaveNullCanvas: Save Rejects a Null Surface

**Test**: `JpegCodec_SaveStream_NullCanvas_ThrowsArgumentNullException`

Calls `Save` with a null `surface` and asserts `ArgumentNullException` is thrown.

#### CanvasNet-Codecs-JpegCodec-SaveNullStream: Save Rejects a Null Stream

**Test**: `JpegCodec_SaveStream_NullStream_ThrowsArgumentNullException`

Calls `Save` with a valid surface and a null stream, and asserts `ArgumentNullException` is thrown.

#### CanvasNet-Codecs-JpegCodec-SaveNullPath: Save Rejects a Null Path

**Test**: `JpegCodec_SavePath_NullPath_ThrowsArgumentNullException`

Calls `Save` with a valid surface and a null path, and asserts `ArgumentNullException` is thrown.

#### CanvasNet-Codecs-JpegCodec-SaveEmptyPath: Save Rejects an Empty Path

**Test**: `JpegCodec_SavePath_EmptyPath_ThrowsArgumentException`

Calls `Save` with a valid surface and an empty path, and asserts `ArgumentException` is thrown.

#### CanvasNet-Codecs-JpegCodec-SaveInvalidQuality: Save Rejects Out-of-Range Quality Values

**Tests**: `JpegCodec_SaveStream_QualityBelowRange_ThrowsArgumentOutOfRangeException`,
`JpegCodec_SaveStream_QualityAboveRange_ThrowsArgumentOutOfRangeException`

Calls `Save` with quality values below 1 and above 100, and asserts
`ArgumentOutOfRangeException` is thrown in both cases.

#### CanvasNet-Codecs-JpegCodec-LoadNullStream: Load Rejects a Null Stream

**Test**: `JpegCodec_LoadStream_NullStream_ThrowsArgumentNullException`

Calls `Load` with a null stream and asserts `ArgumentNullException` is thrown.

#### CanvasNet-Codecs-JpegCodec-LoadNullPath: Load Rejects a Null Path

**Test**: `JpegCodec_LoadPath_NullPath_ThrowsArgumentNullException`

Calls `Load` with a null path and asserts `ArgumentNullException` is thrown.

#### CanvasNet-Codecs-JpegCodec-LoadEmptyPath: Load Rejects an Empty Path

**Test**: `JpegCodec_LoadPath_EmptyPath_ThrowsArgumentException`

Calls `Load` with an empty path and asserts `ArgumentException` is thrown.

#### CanvasNet-Codecs-JpegCodec-RejectMalformedMarker: Load Rejects a Missing SOI Marker

**Test**: `JpegCodec_Load_MissingSoiMarker_ThrowsInvalidDataException`

Builds a byte stream that does not begin with SOI and asserts `Load` throws
`InvalidDataException`.

#### CanvasNet-Codecs-JpegCodec-RejectUnsupportedSof: Load Rejects Unsupported SOF Markers

**Test**: `JpegCodec_Load_UnsupportedSofMarker_ThrowsInvalidDataException` (`[Theory]` over
three unsupported markers)

Builds JPEG streams whose frame marker is not SOF0 or SOF2, and asserts `Load` throws
`InvalidDataException` for each unsupported marker.

#### CanvasNet-Codecs-JpegCodec-RejectFourComponent: Load Rejects Four-Component Frames

**Test**: `JpegCodec_Load_FourComponentSof0_ThrowsInvalidDataException`

Builds a baseline SOF0 stream declaring four components and asserts `Load` throws
`InvalidDataException`.

#### CanvasNet-Codecs-JpegCodec-RejectExceedsMaxDimension: Load Rejects Dimensions Exceeding Surface.MaxDimension

**Tests**: `JpegCodec_Load_WidthExceedsMaxDimension_ThrowsInvalidDataException`,
`JpegCodec_Load_HeightExceedsMaxDimension_ThrowsInvalidDataException`

Builds a SOF0 stream declaring a frame width one greater than `Surface.MaxDimension` (8192), and
separately a frame height one greater, and asserts `Load` throws `InvalidDataException` (not the
`ArgumentOutOfRangeException` that would otherwise escape from `Surface`'s constructor) in both
cases, confirming the dimension check happens immediately after parsing the SOF segment, before
any MCU-grid width/height arithmetic performed while decoding the scan.

#### CanvasNet-Codecs-JpegCodec-RejectMissingSegments: Load Rejects Streams Missing Mandatory Segments

**Tests**: `JpegCodec_Load_MissingSofSegment_ThrowsInvalidDataException`,
`JpegCodec_Load_MissingDhtSegment_ThrowsInvalidDataException`,
`JpegCodec_Load_MissingDqtSegment_ThrowsInvalidDataException`,
`JpegCodec_Load_MissingSosSegment_ThrowsInvalidDataException`

Builds streams missing SOF, DHT, DQT, or SOS in turn, and asserts `Load` throws
`InvalidDataException` for every missing mandatory segment case.

#### CanvasNet-Codecs-JpegCodec-RejectTruncatedStream: Load Rejects Truncated Header or Entropy Data

**Tests**: `JpegCodec_Load_TruncatedHeader_ThrowsInvalidDataException`,
`JpegCodec_Load_TruncatedEntropyData_ThrowsInvalidDataException`,
`JpegCodec_Load_TruncatedSegmentPayload_ThrowsInvalidDataException` (`[Theory]` over 4 truncated
DQT/DHT/SOF0/SOS segment payload cases)

Builds a stream truncated during marker/segment header parsing and separately a stream that ends
before entropy-coded scan data is complete, and asserts `Load` throws `InvalidDataException` for
both cases. Also builds streams whose DQT, DHT, SOF0, or SOS segment declares a length claiming
more payload bytes than are actually present in the stream, and asserts `Load` throws
`InvalidDataException` (rather than an unhandled `IndexOutOfRangeException`) for each truncated
segment payload case.

#### CanvasNet-Codecs-JpegCodec-RestartMarkers: Restart Markers Decode Identically to No Restart Markers

**Test**: `JpegCodec_Load_WithRestartMarkers_DecodesIdenticallyToWithoutRestartMarkers`

Builds two hand-constructed single-component JPEG streams encoding the same two-block image: one
with no DRI segment or restart markers, and one with a DRI segment declaring a restart interval of
one MCU and an RST0 marker inserted between the two entropy-coded blocks. Loads both streams and
asserts they decode to identical dimensions and pixel bytes, proving the DRI/RSTn restart-handling
code path (DC-predictor and end-of-block run reset) produces the same result as the equivalent
stream without restart markers.

#### CanvasNet-Codecs-JpegCodec-FixtureSupported: JpegFixtures Corpus Loads Successfully

**Tests**: `JpegFixtures_SourcePngDimensions_Are32x32`,
`JpegCodec_Load_Fixture_ReturnsCanvasWithExpectedDimensions` (`[Theory]` over 5 fixture files),
`JpegCodec_Load_ColorFixture_MatchesSourcePngWithinTolerance` (`[Theory]` over 4 color fixture
files), `JpegCodec_Load_GrayscaleFixture_HasEqualRgbChannels` (`[Theory]` over 1 grayscale
fixture file)

Confirms the PngSuite source image's actual dimensions programmatically (32x32), then loads every
file in the `JpegFixtures` corpus and asserts each loads successfully with the expected
dimensions; for color fixtures, asserts every decoded pixel matches the PngSuite source within the
fixture's documented tolerance (15 for 4:4:4 and 4:2:2, 30 for baseline/progressive 4:2:0); for
the grayscale fixture, asserts successful load, correct dimensions, and `R == G == B` per pixel.

#### CanvasNet-Codecs-JpegCodec-SimdMatchesScalar: Vectorized YCbCr Conversion Matches Scalar Reference

**Test**: `JpegCodec_ConvertYCbCrRowToRgb_VectorAndScalarRemainder_MatchesScalarReference`

Builds synthetic Y/Cb/Cr rows with both exact-vector and vector-plus-remainder lengths, runs both
the scalar and vectorized conversion paths, and asserts the resulting R/G/B byte rows are
identical.

### Acceptance Criteria

A unit test run passes when all test methods above pass without error or unexpected exception; any
unexpected exception type or wrong return/value relationship constitutes a failure. Across
`JpegCodecTests.cs` and `JpegFixtureTests.cs`, this totals 30 test methods (26 in
`JpegCodecTests.cs` and 4 in `JpegFixtureTests.cs`), which expand to 42 executed xUnit test cases
when every `[Theory]` data row is included, plus the single system-level integration scenario
documented in `docs/verification/canvas-net.md`.
