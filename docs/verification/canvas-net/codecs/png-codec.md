## PngCodec Unit Verification Design

This document describes the unit-level verification strategy for the `PngCodec` class (and the
supporting `PngColorType` enum).

### Verification Approach

The `PngCodec` unit is verified through unit tests that exercise `Load` and `Save` in isolation,
using `MemoryStream` for all in-memory round-trip and negative-path checks, and
`Path.GetTempFileName()` for the file-path overloads. Several tests build raw PNG byte streams by
hand (correct or deliberately corrupted), using independent test-only implementations of CRC-32,
Adler-32, zlib wrapping, and scanline filtering, rather than reusing `PngCodec`'s own private
algorithms — this verifies `PngCodec`'s behavior against the PNG/zlib specifications directly,
rather than only against its own internal consistency. Because `PngCodec`'s only documented
dependency is `Surface` (a sibling in-house unit, not an external service), no mocking or stubbing
is required.

Unit tests reside in `PngCodecTests.cs` within the `DemaConsulting.CanvasNet.Tests` project.
Conformance testing against the industry-standard PngSuite corpus resides separately in
`PngSuiteTests.cs` within the same project (see `PngSuite\PngSuite.README` /
`PngSuite\PngSuite.LICENSE` for corpus provenance and licensing).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `PngCodec`'s only dependency is the in-house `Surface` unit
- **Isolation**: Each test method builds its own `Surface` and/or PNG byte array; file-path tests
  use a uniquely generated temporary file deleted in a `finally` block; no shared state between
  tests

### Unit-Level Test Scenarios

#### CanvasNet-Codecs-PngCodec-SaveLoadRgb: RGB Round-Trip Preserves RGB and Forces Opaque Alpha

**Test**: `PngCodec_SaveThenLoad_Rgb_ReturnsExpectedPixelsWithOpaqueAlpha`

Builds a surface with varied, non-opaque per-pixel values, saves it at `PngColorType.Rgb` to a
`MemoryStream`, loads it back, and asserts every pixel's R/G/B values match the source exactly
and every pixel's alpha is 255, regardless of the source alpha.

#### CanvasNet-Codecs-PngCodec-SaveLoadRgba: RGBA Round-Trip Preserves RGBA Exactly

**Test**: `PngCodec_SaveThenLoad_Rgba_ReturnsExpectedPixelsIncludingAlpha`

Builds a surface with varied pixel values, saves it using the default `PngColorType.Rgba` to a
`MemoryStream`, loads it back, and asserts every pixel (including alpha) matches the source
exactly. This also exercises a correct round-trip Adler-32 checksum (see
_CanvasNet-Codecs-PngCodec-AdlerValidation_ below).

#### CanvasNet-Codecs-PngCodec-MultiIdatChunks: IDAT Data Is Concatenated Across Multiple Chunks

**Test**: `PngCodec_Load_MultipleIdatChunks_ReturnsExpectedPixels`

Hand-builds a small, valid PNG whose compressed payload is deliberately split into five small
`IDAT` chunks (a size unrelated to the payload's natural size), and asserts that `Load` correctly
concatenates them before decompressing, reproducing every expected pixel value.

#### CanvasNet-Codecs-PngCodec-LoadCorruptCrc: Chunk CRC-32 Is Validated

**Test**: `PngCodec_Load_CorruptChunkCrc_ThrowsInvalidDataException`

Saves a surface to a valid PNG, corrupts the final byte of the file (part of the `IEND` chunk's
CRC-32), and asserts `Load` throws `InvalidDataException`.

#### CanvasNet-Codecs-PngCodec-AdlerValidation: Zlib Adler-32 Checksum Is Validated

**Test**: `PngCodec_Load_CorruptAdlerTrailer_ThrowsInvalidDataException`

Saves a surface to a valid PNG, locates the final `IDAT` chunk, flips the last byte of its data
(the last byte of the zlib Adler-32 trailer), recomputes that chunk's CRC-32 (using an
independent test-only CRC-32 implementation) so only the Adler-32 check is exercised, and asserts
`Load` throws `InvalidDataException`.

#### CanvasNet-Codecs-PngCodec-FilterReconstruction: All Five Standard Filter Types Are Reconstructed

**Tests**: `PngCodec_Load_FilterTypeSub_ReconstructsExpectedPixels`,
`PngCodec_Load_FilterTypeUp_ReconstructsExpectedPixels`,
`PngCodec_Load_FilterTypeAverage_ReconstructsExpectedPixels`,
`PngCodec_Load_FilterTypePaeth_ReconstructsExpectedPixels`

For each of filter types 1 (Sub), 2 (Up), 3 (Average), and 4 (Paeth), hand-builds a small PNG
whose filtered scanline bytes were computed independently (by applying the encode-direction
complement of the filter formula to known raw pixel values), and asserts `Load` reconstructs the
exact expected pixel values. Filter type 0 (None) is already exercised by every round-trip test,
since `Save` always writes filter type 0.

#### CanvasNet-Codecs-PngCodec-LoadFromPath / CanvasNet-Codecs-PngCodec-SaveToPath: File Path Overloads Round-Trip

**Test**: `PngCodec_Load_FromFilePath_ReturnsExpectedPixels`

Saves a surface to a temporary file via `Save(Surface, string, PngColorType)`, loads it back via
`Load(string)`, asserts every pixel matches, and deletes the temporary file in a `finally` block.

#### CanvasNet-Codecs-PngCodec-SaveNullCanvas: Save Rejects a Null Surface

**Test**: `PngCodec_Save_NullCanvas_ThrowsArgumentNullException`

Calls `Save` with a null `surface` and a valid stream, and asserts `ArgumentNullException` is
thrown.

#### CanvasNet-Codecs-PngCodec-SaveNullStream: Save Rejects a Null Stream

**Test**: `PngCodec_Save_NullStream_ThrowsArgumentNullException`

Calls `Save` with a valid surface and a null stream, and asserts `ArgumentNullException` is
thrown.

#### CanvasNet-Codecs-PngCodec-SaveNullPath: Save Rejects a Null Path

**Test**: `PngCodec_Save_NullPath_ThrowsArgumentNullException`

Calls `Save` with a valid surface and a null path, and asserts `ArgumentNullException` is thrown.

#### CanvasNet-Codecs-PngCodec-SaveEmptyPath: Save Rejects an Empty Path

**Test**: `PngCodec_Save_EmptyPath_ThrowsArgumentException`

Calls `Save` with a valid surface and an empty path, and asserts `ArgumentException` is thrown.

#### CanvasNet-Codecs-PngCodec-SaveInvalidColorType: Save Rejects an Undefined Color Type

**Test**: `PngCodec_Save_UndefinedColorType_ThrowsArgumentOutOfRangeException`

Calls `Save` with a valid surface and stream but an undefined `PngColorType` value cast from an
out-of-range integer, and asserts `ArgumentOutOfRangeException` is thrown.

#### CanvasNet-Codecs-PngCodec-LoadNullStream: Load Rejects a Null Stream

**Test**: `PngCodec_Load_NullStream_ThrowsArgumentNullException`

Calls `Load` with a null stream and asserts `ArgumentNullException` is thrown.

#### CanvasNet-Codecs-PngCodec-LoadNullPath: Load Rejects a Null Path

**Test**: `PngCodec_Load_NullPath_ThrowsArgumentNullException`

Calls `Load` with a null path and asserts `ArgumentNullException` is thrown.

#### CanvasNet-Codecs-PngCodec-LoadEmptyPath: Load Rejects an Empty Path

**Test**: `PngCodec_Load_EmptyPath_ThrowsArgumentException`

Calls `Load` with an empty path and asserts `ArgumentException` is thrown.

#### CanvasNet-Codecs-PngCodec-LoadBadSignature: Load Rejects a Missing PNG Signature

**Test**: `PngCodec_Load_BadSignature_ThrowsInvalidDataException`

Builds 8 zero bytes (not matching the PNG signature) and asserts `Load` throws
`InvalidDataException`.

#### CanvasNet-Codecs-PngCodec-LoadUnsupportedColorType: Load Rejects Unsupported Color Types

**Tests**: `PngCodec_Load_UnsupportedColorTypeGrayscale_ThrowsInvalidDataException`,
`PngCodec_Load_UnsupportedColorTypePalette_ThrowsInvalidDataException`

Builds a minimal PNG (signature + `IHDR` only) declaring color type 0 (grayscale), and separately
color type 3 (palette/indexed), and asserts `Load` throws `InvalidDataException` for each.

#### CanvasNet-Codecs-PngCodec-LoadUnsupportedBitDepth: Load Rejects Unsupported Bit Depths

**Test**: `PngCodec_Load_UnsupportedBitDepth_ThrowsInvalidDataException` (`[Theory]` over 1, 2, 4, 16)

Builds a minimal PNG declaring each unsupported bit depth in turn, and asserts `Load` throws
`InvalidDataException` for every value.

#### CanvasNet-Codecs-PngCodec-LoadUnsupportedInterlace: Load Rejects Adam7 Interlacing

**Test**: `PngCodec_Load_UnsupportedInterlaceAdam7_ThrowsInvalidDataException`

Builds a minimal PNG declaring interlace method 1 (Adam7), and asserts `Load` throws
`InvalidDataException`.

#### CanvasNet-Codecs-PngCodec-LoadTruncatedStream: Load Rejects a Truncated Stream

**Test**: `PngCodec_Load_TruncatedStream_ThrowsInvalidDataException`

Builds a valid signature and `IHDR` chunk but omits every chunk that should follow (`IDAT`,
`IEND`), and asserts `Load` throws `InvalidDataException` when the stream ends while looking for
the next chunk.

#### CanvasNet-Codecs-PngCodec-LoadChunkBeforeIhdr: Load Rejects a Chunk Preceding IHDR

**Test**: `PngCodec_Load_IendBeforeIhdr_ThrowsInvalidDataExceptionMentioningIhdr`

Builds a valid signature followed directly by a well-formed `IEND` chunk (correct CRC-32), with
no `IHDR` chunk present anywhere in the stream, and asserts `Load` throws
`InvalidDataException` with a message naming `IHDR` as the missing chunk.

#### CanvasNet-Codecs-PngCodec-PngSuiteSupported: PngSuite Files Within Scope Load Successfully

**Test**: `PngCodec_Load_PngSuiteSupportedFile_ReturnsCanvas` (`[Theory]` over 30 PngSuite files)

Loads every PngSuite conformance file whose IHDR declares an 8-bit-per-channel Truecolor (color
type 2) or Truecolor-with-alpha (color type 6), non-interlaced image — verified directly against
each file's raw IHDR bytes rather than trusted from its filename — and asserts `Load` returns a
surface with non-zero width and height, without throwing.

#### CanvasNet-Codecs-PngCodec-PngSuiteUnsupported: PngSuite Files Outside Scope Are Rejected

**Test**: `PngCodec_Load_PngSuiteUnsupportedFile_ThrowsInvalidDataException` (`[Theory]` over 131
PngSuite files)

Loads every PngSuite conformance file that is structurally valid but declares a color type, bit
depth, or interlace method outside `PngCodec`'s supported feature set (grayscale,
grayscale-with-alpha, palette/indexed color types; bit depths other than 8; or Adam7 interlacing),
and asserts `Load` throws `InvalidDataException` for every one, rather than silently producing
incorrect pixels.

#### CanvasNet-Codecs-PngCodec-PngSuiteCorrupt: Deliberately Corrupt PngSuite Files Are Rejected

**Test**: `PngCodec_Load_PngSuiteCorruptFile_ThrowsInvalidDataException` (`[Theory]` over 14
PngSuite files)

Loads every PngSuite conformance file that is deliberately corrupt (bad signature, bad IHDR
CRC-32, or an invalid color-type/bit-depth combination — verified directly against each file's raw
bytes), and asserts `Load` throws `InvalidDataException` for every one.

### Acceptance Criteria

A unit test run passes when all thirty test methods above (including each `[Theory]` case,
covering the full 175-file PngSuite conformance corpus) pass without error or unexpected
exception; any unexpected exception type or wrong return/byte value constitutes a failure.
