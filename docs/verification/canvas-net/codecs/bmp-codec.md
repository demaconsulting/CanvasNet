## BmpCodec Unit Verification Design

This document describes the unit-level verification strategy for the `BmpCodec` class (and the
supporting `BmpBitDepth` enum).

### Verification Approach

The `BmpCodec` unit is verified through unit tests that exercise `Load` and `Save` in isolation,
using `MemoryStream` for all in-memory round-trip, padding, and orientation checks, and
`Path.GetTempFileName()` for the file-path overloads. Because `BmpCodec`'s only documented
dependency is `Surface` (a sibling in-house unit, not an external service), no mocking or stubbing
is required. Tests supply controlled inputs — either a `Surface` to save, or a hand-built byte
array representing a BMP file — and assert on decoded pixel values, raw byte layout, and thrown
exception types.

Unit tests reside in `BmpCodecTests.cs` within the `DemaConsulting.CanvasNet.Tests` project.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `BmpCodec`'s only dependency is the in-house `Surface` unit
- **Isolation**: Each test method builds its own `Surface` and/or BMP byte array; file-path tests
  use a uniquely generated temporary file deleted in a `finally` block; no shared state between
  tests

### Unit-Level Test Scenarios

#### CanvasNet-Codecs-BmpCodec-SaveLoad24Bit: 24-bit Round-Trip Preserves RGB and Forces Opaque Alpha

**Test**: `BmpCodec_SaveThenLoad_24Bit_ReturnsExpectedPixelsWithOpaqueAlpha`

Builds a surface with varied, non-opaque per-pixel values, saves it at `BmpBitDepth.Bit24` to a
`MemoryStream`, loads it back, and asserts every pixel's R/G/B values match the source exactly
and every pixel's alpha is 255, regardless of the source alpha.

#### CanvasNet-Codecs-BmpCodec-SaveLoad32Bit: 32-bit Round-Trip Preserves RGBA Exactly

**Test**: `BmpCodec_SaveThenLoad_32Bit_ReturnsExpectedPixelsIncludingAlpha`

Builds a surface with varied pixel values, saves it at `BmpBitDepth.Bit32` to a `MemoryStream`,
loads it back, and asserts every pixel (including alpha) matches the source exactly.

#### CanvasNet-Codecs-BmpCodec-RowPadding: Rows Are Padded to a Multiple of Four Bytes

**Test**: `BmpCodec_Save_WidthRequiringPadding_ProducesCorrectlyPaddedRows`

Saves a 3-pixel-wide, 24-bit surface (9 data bytes per row, requiring 3 padding bytes to reach the
next multiple of four) and asserts, directly on the raw saved bytes: the pixel-data section
length equals `paddedRowBytes * height`, and the three trailing bytes of every row are zero.
Also confirms the surface still round-trips correctly through `Load` despite the padding.

#### CanvasNet-Codecs-BmpCodec-BottomUpOrientation: Rows Are Written Bottom-Up

**Test**: `BmpCodec_Save_DistinctTopAndBottomRows_WritesBottomRowFirstInFile`

Builds a 2x2 surface with distinct, known pixel values in its top and bottom rows, saves it at
`BmpBitDepth.Bit32`, and reads the raw bytes immediately following the 54-byte header directly
(bypassing `Load`) to assert the *first* row physically present in the file matches the surface's
*last* (bottom) row in BGRA order — proving bottom-up orientation independently of the `Load`
path, which could otherwise mask a symmetric orientation bug.

#### CanvasNet-Codecs-BmpCodec-LoadFromPath / CanvasNet-Codecs-BmpCodec-SaveToPath: File Path Overloads Round-Trip

**Test**: `BmpCodec_Load_FromFilePath_ReturnsExpectedPixels`

Saves a surface to a temporary file via `Save(Surface, string, BmpBitDepth)`, loads it back via
`Load(string)`, asserts every pixel matches, and deletes the temporary file in a `finally` block.

#### CanvasNet-Codecs-BmpCodec-SaveNullCanvas: Save Rejects a Null Surface

**Test**: `BmpCodec_Save_NullCanvas_ThrowsArgumentNullException`

Calls `Save` with a null `surface` and a valid stream, and asserts `ArgumentNullException` is
thrown.

#### CanvasNet-Codecs-BmpCodec-SaveNullStream: Save Rejects a Null Stream

**Test**: `BmpCodec_Save_NullStream_ThrowsArgumentNullException`

Calls `Save` with a valid surface and a null stream, and asserts `ArgumentNullException` is
thrown.

#### CanvasNet-Codecs-BmpCodec-SaveNullPath: Save Rejects a Null Path

**Test**: `BmpCodec_Save_NullPath_ThrowsArgumentNullException`

Calls `Save` with a valid surface and a null path, and asserts `ArgumentNullException` is thrown.

#### CanvasNet-Codecs-BmpCodec-SaveEmptyPath: Save Rejects an Empty Path

**Test**: `BmpCodec_Save_EmptyPath_ThrowsArgumentException`

Calls `Save` with a valid surface and an empty path, and asserts `ArgumentException` is thrown.

#### CanvasNet-Codecs-BmpCodec-SaveInvalidBitDepth: Save Rejects an Undefined Bit Depth

**Test**: `BmpCodec_Save_UndefinedBitDepth_ThrowsArgumentOutOfRangeException`

Calls `Save` with a valid surface and stream but an undefined `BmpBitDepth` value cast from an
out-of-range integer, and asserts `ArgumentOutOfRangeException` is thrown.

#### CanvasNet-Codecs-BmpCodec-LoadNullStream: Load Rejects a Null Stream

**Test**: `BmpCodec_Load_NullStream_ThrowsArgumentNullException`

Calls `Load` with a null stream and asserts `ArgumentNullException` is thrown.

#### CanvasNet-Codecs-BmpCodec-LoadNullPath: Load Rejects a Null Path

**Test**: `BmpCodec_Load_NullPath_ThrowsArgumentNullException`

Calls `Load` with a null path and asserts `ArgumentNullException` is thrown.

#### CanvasNet-Codecs-BmpCodec-LoadEmptyPath: Load Rejects an Empty Path

**Test**: `BmpCodec_Load_EmptyPath_ThrowsArgumentException`

Calls `Load` with an empty path and asserts `ArgumentException` is thrown.

#### CanvasNet-Codecs-BmpCodec-LoadBadSignature: Load Rejects a Missing "BM" Signature

**Test**: `BmpCodec_Load_BadSignature_ThrowsInvalidDataException`

Builds a 14-byte header with an incorrect signature and asserts `Load` throws
`InvalidDataException`.

#### CanvasNet-Codecs-BmpCodec-LoadUnsupportedHeaderSize: Load Rejects Non-BITMAPINFOHEADER Sizes

**Tests**: `BmpCodec_Load_Bitmapcoreheader12Bytes_ThrowsInvalidDataException`,
`BmpCodec_Load_UnsupportedHeaderSizeV4_ThrowsInvalidDataException`

Builds a header declaring `biSize = 12` (BITMAPCOREHEADER), and separately `biSize = 108`
(BITMAPV4HEADER), and asserts `Load` throws `InvalidDataException` for each.

#### CanvasNet-Codecs-BmpCodec-LoadUnsupportedCompression: Load Rejects Non-BI_RGB Compression

**Test**: `BmpCodec_Load_RleCompression_ThrowsInvalidDataException`

Builds a header declaring `biCompression = 1` (BI_RLE8) and asserts `Load` throws
`InvalidDataException`.

#### CanvasNet-Codecs-BmpCodec-LoadUnsupportedBitDepth: Load Rejects Palette-Based Bit Depths

**Test**: `BmpCodec_Load_8BitPaletteDepth_ThrowsInvalidDataException`

Builds a header declaring `biBitCount = 8` and asserts `Load` throws `InvalidDataException`.

#### CanvasNet-Codecs-BmpCodec-LoadUnsupportedTopDown: Load Rejects Negative Height

**Test**: `BmpCodec_Load_TopDownNegativeHeight_ThrowsInvalidDataException`

Builds a header declaring `biHeight = -1` and asserts `Load` throws `InvalidDataException`.

#### CanvasNet-Codecs-BmpCodec-LoadExceedsMaxDimension: Load Rejects Dimensions Exceeding Surface.MaxDimension

**Tests**: `BmpCodec_Load_WidthExceedsMaxDimension_ThrowsInvalidDataException`,
`BmpCodec_Load_HeightExceedsMaxDimension_ThrowsInvalidDataException`

Builds a header declaring `biWidth` one greater than `Surface.MaxDimension` (8192), and
separately `biHeight` one greater, and asserts `Load` throws `InvalidDataException` (not the
`ArgumentOutOfRangeException` that would otherwise escape from `Surface`'s constructor) in both
cases, confirming the dimension check happens before any row/stride arithmetic performed later in
`Load`.

#### CanvasNet-Codecs-BmpCodec-LoadTruncatedStream: Load Rejects a Truncated Stream

**Test**: `BmpCodec_Load_TruncatedStream_ThrowsInvalidDataException`

Builds a valid header for a 1x1, 24-bit image but omits the pixel data that should follow it, and
asserts `Load` throws `InvalidDataException`.

### Acceptance Criteria

A unit test run passes when all twenty-two test methods above pass without error or unexpected
exception; any unexpected exception type or wrong return/byte value constitutes a failure.
