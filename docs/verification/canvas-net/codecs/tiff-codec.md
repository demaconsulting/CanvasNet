## TiffCodec Unit Verification Design

This document describes the unit-level verification strategy for the `TiffCodec` class (and the
supporting `TiffCompression` enum).

### Verification Approach

The `TiffCodec` unit is verified through unit tests that exercise `Load` and `Save` in isolation,
using `MemoryStream` for all in-memory round-trip and negative-path checks, and
`Path.GetTempFileName()` for the file-path overloads. Several tests build raw TIFF byte streams
by hand (correct or deliberately malformed) via a small independent test-only TIFF file builder,
using independent test-only implementations of PackBits, TIFF-flavor LZW, the
horizontal-differencing predictor, and zlib wrapping, rather than reusing `TiffCodec`'s own
private algorithms — this verifies `TiffCodec`'s behavior against the TIFF 6.0 and zlib
specifications directly, rather than only against its own internal consistency. Because
`TiffCodec`'s only documented dependency is `Surface` (a sibling in-house unit, not an external
service), no mocking or stubbing is required.

Unit tests reside in `TiffCodecTests.cs` within the `DemaConsulting.CanvasNet.Tests` project.
Conformance testing against the `TiffFixtures` corpus (nine real-world files generated with
ImageMagick from the PngSuite corpus) resides separately in `TiffFixtureTests.cs` within the same
project (see `TiffFixtures\README.md` for corpus provenance).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `TiffCodec`'s only dependency is the in-house `Surface` unit
- **Isolation**: Each test method builds its own `Surface` and/or TIFF byte array; file-path tests
  use a uniquely generated temporary file deleted in a `finally` block; no shared state between
  tests

### Unit-Level Test Scenarios

#### CanvasNet-Codecs-TiffCodec-SaveLoadRgba: RGBA Round-Trip Preserves RGBA Exactly

**Test**: `TiffCodec_SaveThenLoad_AllCompressions_RoundTripsExactly` (`[Theory]` over all four
`TiffCompression` values)

Builds a surface with varied, non-opaque per-pixel values, saves it with the default RGBA
representation to a `MemoryStream` for each supported compression method, loads it back, and
asserts every pixel (including alpha) matches the source exactly.

#### CanvasNet-Codecs-TiffCodec-LoadRgbForcesOpaqueAlpha: RGB (No Alpha) Load Forces Alpha to 255

**Tests**: `TiffCodec_Load_LittleEndianRgbSingleStrip_ReturnsExpectedPixels`,
`TiffCodec_Load_RgbFixture_MatchesSourcePngWithOpaqueAlpha`

Hand-builds (and separately, loads from the `TiffFixtures` corpus) a 3-samples-per-pixel RGB TIFF
with no `ExtraSamples` tag, and asserts every loaded pixel's R/G/B matches the source exactly and
alpha is forced to 255.

#### CanvasNet-Codecs-TiffCodec-LoadGrayscale: Grayscale Load Produces R=G=B With Full Alpha

**Tests**: `TiffCodec_Load_Grayscale_ReturnsGrayPixelsWithFullAlpha`,
`TiffCodec_Load_GrayscaleFixture_HasEqualRgbChannels`

Hand-builds (and separately, loads from the `TiffFixtures` corpus) a BlackIsZero Grayscale TIFF,
and asserts every loaded pixel has R == G == B equal to the gray sample value, with alpha 255.

#### CanvasNet-Codecs-TiffCodec-LoadLittleEndian: Load Supports the "II" Byte-Order Mark

**Tests**: `TiffCodec_Load_LittleEndianRgbSingleStrip_ReturnsExpectedPixels`,
`TiffCodec_Load_Fixture_ReturnsCanvasWithExpectedDimensions`

Hand-builds (and separately, loads from the `TiffFixtures` corpus) a little-endian TIFF, and
asserts it loads with the expected pixel values/dimensions.

#### CanvasNet-Codecs-TiffCodec-LoadBigEndian: Load Supports the "MM" Byte-Order Mark

**Tests**: `TiffCodec_Load_BigEndianRgbSingleStrip_ReturnsExpectedPixels`,
`TiffCodec_Load_Fixture_ReturnsCanvasWithExpectedDimensions`

Hand-builds (and separately, loads from the `TiffFixtures` corpus) a big-endian TIFF, and asserts
it loads with the expected pixel values/dimensions.

#### CanvasNet-Codecs-TiffCodec-LoadMultiStrip: Multi-Strip Images Are Reassembled Correctly

**Tests**: `TiffCodec_Load_MultiStrip_ReturnsExpectedPixels`,
`TiffCodec_Load_MultiStripWithTwoRowsPerStrip_ReturnsExpectedPixels`

Hand-builds TIFF images whose pixel data is split across several one-row and multi-row strips,
and asserts every pixel is correctly reassembled in row order.

#### CanvasNet-Codecs-TiffCodec-CompressionNone: None Compression Round-Trips Exactly

**Tests**: `TiffCodec_SaveThenLoad_AllCompressions_RoundTripsExactly`,
`TiffCodec_Save_WithoutPredictorCompression_OmitsPredictorTag`

Saves and loads a surface using `TiffCompression.None`, asserting exact pixel round-trip and that
no `Predictor` tag is written.

#### CanvasNet-Codecs-TiffCodec-CompressionPackBits: PackBits Compression Round-Trips Exactly

**Tests**: `TiffCodec_SaveThenLoad_AllCompressions_RoundTripsExactly`,
`TiffCodec_Load_PackBitsCompressedStrip_DecodesUsingIndependentEncoder`,
`TiffCodec_Load_RgbFixture_MatchesSourcePngWithOpaqueAlpha`

Saves and loads a surface using `TiffCompression.PackBits`; separately, decodes a strip built with
an independent test-only PackBits encoder, and loads the real-world `rgb_packbits.tiff` fixture.

#### CanvasNet-Codecs-TiffCodec-CompressionLzw: LZW Compression Round-Trips Exactly

**Tests**: `TiffCodec_SaveThenLoad_AllCompressions_RoundTripsExactly`,
`TiffCodec_Load_LzwCompressedStrip_DecodesUsingIndependentEncoder`,
`TiffCodec_Load_LzwCompressedRepeatingData_DecodesUsingIndependentEncoder`,
`TiffCodec_Load_RgbFixture_MatchesSourcePngWithOpaqueAlpha`,
`TiffCodec_Load_RgbaFixture_MatchesSourcePngExactly`,
`TiffCodec_Load_GrayscaleFixture_HasEqualRgbChannels`

Saves and loads a surface using `TiffCompression.Lzw`; separately, decodes strips built with an
independent test-only TIFF-flavor LZW encoder (including a repeating-pattern case that exercises
dictionary growth and code-width transitions), and loads the real-world `rgb_lzw.tiff`/
`rgba_lzw.tiff`/`gray_lzw.tiff` fixtures (genuine third-party encoder output).

#### CanvasNet-Codecs-TiffCodec-CompressionDeflate: Deflate Compression Round-Trips Exactly

**Tests**: `TiffCodec_SaveThenLoad_AllCompressions_RoundTripsExactly`,
`TiffCodec_Load_DeflateCompressedStrip_DecodesUsingIndependentEncoder`,
`TiffCodec_Load_RgbFixture_MatchesSourcePngWithOpaqueAlpha`

Saves and loads a surface using `TiffCompression.Deflate`; separately, decodes a strip built with
an independent test-only zlib/Deflate encoder, and loads the real-world `rgb_deflate.tiff`
fixture.

#### CanvasNet-Codecs-TiffCodec-PredictorRoundTrip: Horizontal Predictor Is Applied and Reversed Correctly

**Tests**: `TiffCodec_Save_WithPredictorCompression_WritesPredictorTagAndIndependentlyDecodesToSameBytes`
(`[Theory]` over Lzw/Deflate), `TiffCodec_Load_PredictorWithLzw_DecodesUsingIndependentEncoder`,
`TiffCodec_Load_PredictorWithDeflate_DecodesUsingIndependentEncoder`

Asserts `Save` writes the `Predictor` tag (value 2) for Lzw/Deflate compression and that an
independent test-only decoder reproduces the exact original raw pixel bytes; separately, hand-
builds strips with predictor differencing applied via an independent test-only implementation and
asserts `Load` reverses it correctly.

#### CanvasNet-Codecs-TiffCodec-LoadFromPath / CanvasNet-Codecs-TiffCodec-SaveToPath: File Path Overloads Round-Trip

**Test**: `TiffCodec_SaveThenLoad_PathRoundTrip_ReturnsExpectedPixel`

Saves a surface to a temporary file via `Save(Surface, string, TiffCompression)`, loads it back via
`Load(string)`, asserts the expected pixel matches, and deletes the temporary file in a `finally`
block.

#### CanvasNet-Codecs-TiffCodec-SaveNullCanvas: Save Rejects a Null Surface

**Tests**: `TiffCodec_SaveStream_NullCanvas_ThrowsArgumentNullException`,
`TiffCodec_SavePath_NullCanvas_ThrowsArgumentNullException`

Calls each `Save` overload with a null `surface`, and asserts `ArgumentNullException` is thrown.

#### CanvasNet-Codecs-TiffCodec-SaveNullStream: Save Rejects a Null Stream

**Test**: `TiffCodec_SaveStream_NullStream_ThrowsArgumentNullException`

Calls `Save` with a valid surface and a null stream, and asserts `ArgumentNullException` is
thrown.

#### CanvasNet-Codecs-TiffCodec-SaveNullPath: Save Rejects a Null Path

**Test**: `TiffCodec_SavePath_NullPath_ThrowsArgumentNullException`

Calls `Save` with a valid surface and a null path, and asserts `ArgumentNullException` is thrown.

#### CanvasNet-Codecs-TiffCodec-SaveEmptyPath: Save Rejects an Empty Path

**Test**: `TiffCodec_SavePath_EmptyPath_ThrowsArgumentException`

Calls `Save` with a valid surface and an empty path, and asserts `ArgumentException` is thrown.

#### CanvasNet-Codecs-TiffCodec-SaveInvalidCompression: Save Rejects an Undefined Compression Value

**Tests**: `TiffCodec_SaveStream_UndefinedCompression_ThrowsArgumentOutOfRangeException`,
`TiffCodec_SavePath_UndefinedCompression_ThrowsArgumentOutOfRangeException`

Calls each `Save` overload with an undefined `TiffCompression` value cast from an out-of-range
integer, and asserts `ArgumentOutOfRangeException` is thrown.

#### CanvasNet-Codecs-TiffCodec-LoadNullStream: Load Rejects a Null Stream

**Test**: `TiffCodec_LoadStream_NullStream_ThrowsArgumentNullException`

Calls `Load` with a null stream and asserts `ArgumentNullException` is thrown.

#### CanvasNet-Codecs-TiffCodec-LoadNullPath: Load Rejects a Null Path

**Test**: `TiffCodec_LoadPath_NullPath_ThrowsArgumentNullException`

Calls `Load` with a null path and asserts `ArgumentNullException` is thrown.

#### CanvasNet-Codecs-TiffCodec-LoadEmptyPath: Load Rejects an Empty Path

**Test**: `TiffCodec_LoadPath_EmptyPath_ThrowsArgumentException`

Calls `Load` with an empty path and asserts `ArgumentException` is thrown.

#### CanvasNet-Codecs-TiffCodec-LoadBadByteOrder: Load Rejects an Invalid Byte-Order Mark

**Test**: `TiffCodec_Load_BadByteOrderMark_ThrowsInvalidDataException`

Hand-builds a TIFF file and corrupts its byte-order mark to neither `"II"` nor `"MM"`, and
asserts `Load` throws `InvalidDataException`.

#### CanvasNet-Codecs-TiffCodec-LoadUnsupportedBitDepth: Load Rejects Unsupported Bit Depths

**Test**: `TiffCodec_Load_UnsupportedBitsPerSample_ThrowsInvalidDataException`

Hand-builds a TIFF declaring a `BitsPerSample` value of 16, and asserts `Load` throws
`InvalidDataException`.

#### CanvasNet-Codecs-TiffCodec-LoadExceedsMaxDimension: Load Rejects Dimensions Exceeding Surface.MaxDimension

**Tests**: `TiffCodec_Load_WidthExceedsMaxDimension_ThrowsInvalidDataException`,
`TiffCodec_Load_HeightExceedsMaxDimension_ThrowsInvalidDataException`

Hand-builds a TIFF declaring an `ImageWidth` value one greater than `Surface.MaxDimension`
(8192), and separately an `ImageLength` value one greater, and asserts `Load` throws
`InvalidDataException` (not the `ArgumentOutOfRangeException` that would otherwise escape from
`Surface`'s constructor) in both cases, confirming the dimension check happens before the
row-byte-width (`width * samplesPerPixel`) arithmetic performed later in `Load`.

#### CanvasNet-Codecs-TiffCodec-LoadUnsupportedPhotometric: Load Rejects Unsupported Photometric Interpretations

**Test**: `TiffCodec_Load_UnsupportedPhotometricPalette_ThrowsInvalidDataException`

Hand-builds a TIFF declaring photometric interpretation 3 (Palette), and asserts `Load` throws
`InvalidDataException`.

#### CanvasNet-Codecs-TiffCodec-LoadUnsupportedCompression: Load Rejects Unsupported Compression Values

**Test**: `TiffCodec_Load_UnsupportedCompression_ThrowsInvalidDataException`

Hand-builds a TIFF declaring compression value 6 (JPEG, not supported), and asserts `Load` throws
`InvalidDataException`.

#### CanvasNet-Codecs-TiffCodec-LoadPlanarConfiguration: Load Rejects Planar Configuration

**Test**: `TiffCodec_Load_PlanarConfigurationPlanar_ThrowsInvalidDataException`

Hand-builds a TIFF declaring `PlanarConfiguration = 2` (Planar), and asserts `Load` throws
`InvalidDataException`.

#### CanvasNet-Codecs-TiffCodec-LoadTiled: Load Rejects Tiled TIFF Images

**Test**: `TiffCodec_Load_TiledTiff_ThrowsInvalidDataException`

Hand-builds a TIFF declaring `TileWidth`/`TileLength` tags and no strip tags, and asserts `Load`
throws `InvalidDataException`.

#### CanvasNet-Codecs-TiffCodec-GetInfoTiledRejection: GetInfo Rejects Tiled TIFF Images Identically to Load

**Tests**: `TiffCodec_GetInfo_Seekable_TiledTiff_ThrowsInvalidDataException`

Hand-builds the same tiled-TIFF byte layout used by `TiffCodec_Load_TiledTiff_ThrowsInvalidDataException`
and calls `GetInfo` on a seekable `MemoryStream`, asserting `InvalidDataException`. Proves the
tiled-rejection check lives in the shared `ReadTiffImageInfo` helper used by both `Load` and
`GetInfo`, rather than only in `Load`, closing the gap where `GetInfo` previously reported
dimensions for an image `Load` could never actually decode.

#### CanvasNet-Codecs-TiffCodec-LoadMissingMandatoryTag: Load Rejects Files Missing a Mandatory Tag

**Test**: `TiffCodec_Load_MissingMandatoryTag_ThrowsInvalidDataException` (`[Theory]` over each
of the 8 mandatory tags)

Hand-builds a TIFF omitting each mandatory tag (`ImageWidth`, `ImageLength`, `BitsPerSample`,
`Compression`, `PhotometricInterpretation`, `StripOffsets`, `RowsPerStrip`, `StripByteCounts`) in
turn, and asserts `Load` throws `InvalidDataException` for every case.

#### CanvasNet-Codecs-TiffCodec-LoadTruncatedStream: Load Rejects a Truncated Stream

**Tests**: `TiffCodec_Load_TruncatedHeader_ThrowsInvalidDataException`,
`TiffCodec_Load_TruncatedStripData_ThrowsInvalidDataException`

Builds a stream that ends before the 8-byte header is complete, and separately a file whose strip
data is shorter than the declared image dimensions require, and asserts `Load` throws
`InvalidDataException` for both.

#### CanvasNet-Codecs-TiffCodec-TagValueBoundsValidation: Load and GetInfo Reject Zero-Count and Overflowing Tag Arithmetic

**Tests**: `TiffCodec_Load_ImageWidthZeroCount_ThrowsInvalidDataExceptionNotIndexOutOfRange`,
`TiffCodec_GetInfo_Seekable_ImageWidthZeroCount_ThrowsInvalidDataExceptionNotIndexOutOfRange`,
`TiffCodec_Load_IfdOffsetAboveInt32Range_ThrowsInvalidDataExceptionNotOverflowException`,
`TiffCodec_GetInfo_Seekable_IfdOffsetAboveInt32Range_ThrowsInvalidDataExceptionNotOverflowException`,
`TiffCodec_Load_OutOfLineTagOffsetAboveInt32Range_ThrowsInvalidDataExceptionNotOverflowException`,
`TiffCodec_GetInfo_Seekable_OutOfLineTagOffsetAboveInt32Range_ThrowsInvalidDataExceptionNotOverflowException`

Hand-builds a TIFF whose `ImageWidth` tag declares `Count = 0`, and asserts `Load` and `GetInfo`
throw `InvalidDataException` rather than the `IndexOutOfRangeException` that would otherwise
escape from indexing an empty resolved-values array. Separately hand-builds a TIFF whose IFD
offset (in the 8-byte header), and separately whose out-of-line tag value offset (for a
multi-value tag too large to fit inline), is set to a value above `int.MaxValue` (representable
in the TIFF file format's unsigned 32-bit offset field but not as a .NET array index/count), and
asserts `Load` and `GetInfo` throw `InvalidDataException` rather than the `OverflowException`
that would otherwise escape from an unguarded `checked((int)...)` cast.

#### CanvasNet-Codecs-TiffCodec-FixtureSupported: TiffFixtures Corpus Loads Successfully

**Tests**: `TiffFixtures_SourcePngDimensions_Are32x32`,
`TiffCodec_Load_Fixture_ReturnsCanvasWithExpectedDimensions` (`[Theory]` over 9 fixture files),
`TiffCodec_Load_RgbFixture_MatchesSourcePngWithOpaqueAlpha` (`[Theory]` over 5 RGB fixture files),
`TiffCodec_Load_RgbaFixture_MatchesSourcePngExactly` (`[Theory]` over 2 RGBA fixture files),
`TiffCodec_Load_GrayscaleFixture_HasEqualRgbChannels` (`[Theory]` over 2 grayscale fixture files)

Confirms the PngSuite source images' actual dimensions programmatically (32x32), then loads every
file in the `TiffFixtures` corpus and asserts each loads successfully with the expected
dimensions; for RGB/RGBA fixtures, asserts every decoded pixel matches the corresponding PngSuite
source exactly (alpha forced to 255 for RGB fixtures); for grayscale fixtures, asserts only
successful load, correct dimensions, and R == G == B per pixel.

#### CanvasNet-Codecs-TiffCodec-GetInfo: GetInfo Reports Dimensions/Channels/Alpha Without Decoding Strips

**Tests**: `TiffCodec_GetInfo_Seekable_Rgb_ReturnsExpectedInfoWithoutAlpha`,
`TiffCodec_GetInfo_Seekable_Rgba_ReturnsExpectedInfoWithAlpha`,
`TiffCodec_GetInfo_Seekable_PositionStaysWellBelowFullLength`,
`TiffCodec_GetInfo_OversizedDimensions_ReturnsRawValue_ButLoadThrows`,
`TiffCodec_GetInfo_SucceedsWithTruncatedStripData_ButLoadThrows`,
`TiffCodec_GetInfo_Seekable_SamplesPerPixelTagOmitted_DefaultsToBitsPerSampleCount`,
`TiffCodec_GetInfo_Seekable_UnsupportedBitsPerSample_ThrowsInvalidDataException`,
`TiffCodec_GetInfo_Seekable_StreamNotAtPositionZero_ReturnsCorrectImageInfo`,
`TiffCodec_GetInfo_NonSeekableStream_SucceedsAndMatchesLoadResult`,
`CanvasNet_SystemIntegration_TiffGetInfoOnNonSeekableStream_ReturnsExpectedInfo`

Builds a seekable RGB TIFF and separately an RGBA TIFF (with an `ExtraSamples` tag), calls
`GetInfo` on a `MemoryStream` for each, and asserts the returned `ImageInfo` reports the correct
width/height, `Channels` (3 or 4), and `HasAlpha` (false or true). Proves the seekable path never
reads strip data by building a 100x100 image with substantial strip data and asserting
`stream.Position` after `GetInfo` returns is well below `stream.Length / 2`. Proves `GetInfo` does
not enforce `Surface.MaxDimension` by building an RGB image one pixel wider than
`Surface.MaxDimension`, asserting `GetInfo` returns that raw oversized width without throwing, and
then asserting `Load` on the exact same bytes still throws `InvalidDataException`. Proves the
seekable path never needs strip data at all by truncating a valid file's trailing strip bytes
entirely and asserting `GetInfo` still succeeds while `Load` on the same truncated bytes throws
`InvalidDataException`. Proves `GetInfo`'s `SamplesPerPixel`-absent default (the regression
scenario for the historical divergence described in the design documentation): builds a TIFF with
the `SamplesPerPixel` tag omitted and asserts `GetInfo` returns `Channels == 3` (derived from
`BitsPerSample`'s entry count, not defaulted to 1); separately builds a TIFF declaring
`BitsPerSample = 16,16,16` (otherwise a valid RGB image) and asserts `GetInfo` throws
`InvalidDataException`, proving the full format-support validation applies to the seekable path.
Proves the stream-position fix (`GetInfo` resolving `StreamTiffDataSource` reads relative to the
stream's starting position rather than absolute byte 0): writes a non-empty byte prefix to a
`MemoryStream`, then a valid TIFF, sets `stream.Position` past the prefix, and asserts `GetInfo`
returns the correct `ImageInfo` for the TIFF that follows the prefix rather than misinterpreting
bytes at absolute offset 0. Proves `GetInfo` succeeds on a non-seekable stream and that its
reported dimensions match what `TiffCodec.Load` actually decodes from the identical bytes - the
GetInfo/Load parity invariant: the unit test wraps valid TIFF bytes in the unit's private
`FunctionallySeekableButCanSeekFalseStream` helper (reports `CanSeek == false` while forwarding
`Position`/`Seek`/`Length`/`Read` to a fully functional inner `MemoryStream` - none of those
members throw), asserts `GetInfo` on that stream succeeds via the buffer-and-probe fallback, and
asserts `GetInfo`'s reported `Width`/`Height` match the `Surface.Width`/`Surface.Height` that
`TiffCodec.Load` decodes from a separate, seekable `MemoryStream` over the same bytes - proving
the non-seekable fallback resolves dimensions that genuinely agree with what `Load` would produce,
not merely dimensions that agree with another `GetInfo` call. The system-level test separately uses
`TestSupport.NonSeekableStream` (a test-only stream
reporting `CanSeek == false` while forwarding `Read`/`Flush` to a fully functional inner buffer,
but genuinely throwing `NotSupportedException` from `Position`/`Length`/`Seek` themselves) to
exercise the fallback end-to-end through the public `TiffCodec.GetInfo` entry point with a
saved-then-reloaded TIFF file, per `docs/verification/canvas-net.md`'s system-level evidence
contract.

#### CanvasNet-Codecs-TiffCodec-TagValueCountUpperBound: GetInfo Rejects an Implausibly Large Tag Count

**Tests**: `TiffCodec_GetInfo_Seekable_BitsPerSampleCountImplausiblyLarge_ThrowsWithoutLargeAllocation`

Hand-builds a TIFF file large enough that a `BitsPerSample` tag declaring a `Count` of 2,000,000
still satisfies the pre-existing stream-bounds check (the file genuinely contains that many bytes
at the declared offset), so only the new upper-bound cardinality cap (`MaxImageLevelTagCount`) can
reject it. Measures `GC.GetAllocatedBytesForCurrentThread()` immediately before and after calling
`GetInfo` on the seekable stream, and asserts both that `InvalidDataException` is thrown and that
the allocated-bytes delta stays under a small bound (proving the multi-megabyte `uint[]` the count
would otherwise require is never allocated).

#### CanvasNet-Codecs-TiffCodec-GetInfoValidation: GetInfo Rejects Invalid Arguments and Malformed Headers

**Tests**: `TiffCodec_GetInfo_NullStream_ThrowsArgumentNullException`,
`TiffCodec_GetInfo_NullPath_ThrowsArgumentNullException`,
`TiffCodec_GetInfo_EmptyPath_ThrowsArgumentException`,
`TiffCodec_GetInfo_BadByteOrderMark_ThrowsInvalidDataException`

Calls `GetInfo(Stream)` with a null stream, `GetInfo(string)` with a null path and separately an
empty path, and `GetInfo(Stream)` with an 8-byte header carrying an invalid byte-order mark,
asserting `ArgumentNullException`, `ArgumentNullException`, `ArgumentException`, and
`InvalidDataException` respectively — the same exception contract as the corresponding `Load`
scenarios.

### Acceptance Criteria

A unit test run passes when all test methods above (including each `[Theory]` case) pass without
error or unexpected exception; any unexpected exception type or wrong return/byte value
constitutes a failure. Across `TiffCodecTests.cs` and `TiffFixtureTests.cs`, this totals 70 test
methods (65 in `TiffCodecTests.cs` and 5 in `TiffFixtureTests.cs`; several of these are `[Theory]`
methods that additionally expand to multiple executed xUnit test cases, one per fixture file or
data row), plus the system-level integration scenarios documented in
`docs/verification/canvas-net.md`.
