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

#### CanvasNet-Codecs-PngCodec-LoadGrayscale / -LoadGrayscaleAlpha: Load Decodes Grayscale and Grayscale-with-Alpha PNGs

**Tests**: `PngCodec_Load_Grayscale8Bit_ReturnsExpectedGrayPixels`,
`PngCodec_Load_GrayscaleAlpha8Bit_ReturnsExpectedPixels`

Hand-builds an 8-bit Grayscale (color type 0) PNG and separately an 8-bit Grayscale-with-alpha
(color type 4) PNG with independently pre-computed expected pixel values, and asserts `Load`
produces those exact R=G=B(=gray) and alpha values.

#### CanvasNet-Codecs-PngCodec-LoadPalette: Load Decodes Palette PNGs and Requires a PLTE Chunk

**Tests**: `PngCodec_Load_Palette8Bit_ResolvesIndicesThroughPlte`,
`PngCodec_Load_PaletteWithoutPlte_ThrowsInvalidDataExceptionMentioningPlte`,
`PngCodec_Load_PaletteIndexOutOfRange_ThrowsInvalidDataException`

Hand-builds an 8-bit Palette (color type 3) PNG with a `PLTE` chunk and asserts `Load` resolves
each pixel's index to the correct RGB triple. Separately asserts `Load` throws
`InvalidDataException` naming `PLTE` when a Palette-color-type file has no `PLTE` chunk, and
throws `InvalidDataException` when a pixel's palette index is out of range for the supplied
`PLTE` chunk.

#### CanvasNet-Codecs-PngCodec-LoadTrnsPalette / -LoadTrnsGrayscale / -LoadTrnsTruecolor: Load Honors tRNS Transparency

**Tests**: `PngCodec_Load_PaletteWithTrns_AppliesPerIndexAlpha`,
`PngCodec_Load_GrayscaleWithTrns_MarksExactMatchTransparent`,
`PngCodec_Load_TruecolorWithTrns_MarksExactMatchTransparent`,
`PngCodec_Load_Grayscale16BitTrns_ComparesRawSampleBeforeDownshift`

Hand-builds a Palette PNG with a `tRNS` chunk assigning a distinct alpha byte to specific palette
entries, and asserts `Load` applies each entry's alpha to every pixel using that index. Hand-builds
an 8-bit Grayscale PNG and separately an 8-bit Truecolor PNG, each with a `tRNS` chunk naming one
exact gray value (respectively one exact RGB triple) as transparent, and asserts `Load` makes only
exact-match pixels fully transparent (alpha 0), leaving every other pixel opaque. Hand-builds a
16-bit Grayscale PNG whose `tRNS` chunk's 16-bit key value and a pixel's raw 16-bit sample differ
only in their low byte (so both share the same post-downshift 8-bit value), and asserts `Load`
does **not** treat that pixel as transparent — proving the 16-bit `tRNS` comparison happens before
downshifting, not after.

#### CanvasNet-Codecs-PngCodec-LoadSubByteDepths: Load Decodes Sub-Byte (1/2/4-bit) Grayscale and Palette PNGs

**Tests**: `PngCodec_Load_Grayscale1BitDepth_ScalesSamplesAndUnpacksMsbFirst`,
`PngCodec_Load_Grayscale2BitDepth_ScalesSamples`, `PngCodec_Load_Grayscale4BitDepth_ScalesSamples`,
`PngCodec_Load_Palette2BitDepth_UnpacksIndicesWithoutScaling`

Hand-builds 1-bit, 2-bit, and 4-bit Grayscale PNGs, each with independently pre-computed
MSB-first-packed row bytes at a width not evenly divisible into whole bytes (exercising row-padding
edges), and asserts `Load` unpacks each sample and scales it to the full 0-255 range. Hand-builds a
2-bit Palette PNG and asserts `Load` unpacks each palette index without any scaling (an index
selects a palette entry; it is never a sample magnitude).

#### CanvasNet-Codecs-PngCodec-Load16BitDepth: Load Decodes 16-Bit-Per-Sample PNGs by Discarding the Low Byte

**Tests**: `PngCodec_Load_Grayscale16BitDepth_DiscardsLowByte`,
`PngCodec_Load_TruecolorAlpha16BitDepth_DiscardsLowByteOfEveryChannel`

Hand-builds a 16-bit Grayscale PNG and separately a 16-bit Truecolor-with-alpha PNG, each with
distinct high and low sample bytes, and asserts `Load` produces the expected 8-bit pixel values by
discarding each sample's low byte (`value >> 8`).

#### CanvasNet-Codecs-PngCodec-LoadInvalidCombination: Load Rejects Invalid Bit-Depth/Color-Type Combinations

**Test**: `PngCodec_Load_InvalidBitDepthColorTypeCombination_ThrowsInvalidDataException` (`[Theory]`
over multiple invalid combinations: Truecolor at 1/2/4 bits, Palette at 16 bits, Grayscale-with-alpha
at 1/2/4 bits, Truecolor-with-alpha at 1/2/4 bits)

Hand-builds a minimal PNG declaring each invalid bit-depth/color-type pairing in turn, and asserts
`Load` throws `InvalidDataException` for every one — these combinations are invalid per the PNG
specification itself, independent of any feature this codec chooses to support.

#### CanvasNet-Codecs-PngCodec-LoadUnsupportedInterlace: Load Rejects Adam7 Interlacing, but GetInfo Still Succeeds

**Test**: `PngCodec_Load_UnsupportedInterlaceAdam7_ThrowsInvalidDataException`

Builds a minimal PNG declaring interlace method 1 (Adam7), and asserts `Load` throws
`InvalidDataException`, while `GetInfo` on the exact same bytes succeeds and reports the correct
declared width and height — proving Adam7 interlacing is a decode-capability limitation, not a
well-formedness defect.

#### CanvasNet-Codecs-PngCodec-LoadExceedsMaxDimension: Load Rejects Dimensions Exceeding Surface.MaxDimension

**Tests**: `PngCodec_Load_WidthExceedsMaxDimension_ThrowsInvalidDataException`,
`PngCodec_Load_HeightExceedsMaxDimension_ThrowsInvalidDataException`

Builds a minimal PNG declaring an IHDR width one greater than `Surface.MaxDimension` (8192), and
separately a height one greater, and asserts `Load` throws `InvalidDataException` (not the
`ArgumentOutOfRangeException` that would otherwise escape from `Surface`'s constructor) in both
cases, confirming the dimension check happens before the width-times-channels row-byte-width
arithmetic performed elsewhere in `Load`.

#### CanvasNet-Codecs-PngCodec-LoadTruncatedStream: Load Rejects a Truncated Stream

**Test**: `PngCodec_Load_TruncatedStream_ThrowsInvalidDataException`

Builds a valid signature and `IHDR` chunk but omits every chunk that should follow (`IDAT`,
`IEND`), and asserts `Load` throws `InvalidDataException` when the stream ends while looking for
the next chunk.

#### CanvasNet-Codecs-PngCodec-LoadChunkBeforeIhdr: Load Rejects Any Chunk Preceding IHDR

**Tests**: `PngCodec_Load_IendBeforeIhdr_ThrowsInvalidDataExceptionMentioningIhdr`,
`PngCodec_Load_AncillaryChunkBeforeIhdr_ThrowsInvalidDataException`

Builds a valid signature followed directly by a well-formed `IEND` chunk (correct CRC-32), with
no `IHDR` chunk present anywhere in the stream, and asserts `Load` throws
`InvalidDataException` with a message naming `IHDR` as the missing chunk. Separately builds a
valid signature followed directly by an otherwise-safe-to-skip ancillary chunk (`tEXt`), again
with no `IHDR` chunk present, and asserts `Load` throws the same `InvalidDataException` naming
`IHDR`, proving even a chunk type this codec would otherwise silently skip is still rejected when
it appears before the mandatory, always-first `IHDR` chunk.

#### CanvasNet-Codecs-PngCodec-LoadUnrecognizedCriticalChunk: Load Rejects Unrecognized Critical Chunks

**Tests**: `PngCodec_Load_UnrecognizedCriticalChunk_ThrowsInvalidDataException`,
`PngCodec_Load_UnrecognizedAncillaryChunk_StillLoadsSuccessfully`

Builds a valid `IHDR` followed by a hypothetical unrecognized chunk type `ABCD` (uppercase first
type byte, marking it critical per the PNG specification), and asserts `Load` throws
`InvalidDataException` naming `ABCD`. Separately builds a valid, otherwise-complete single-pixel
PNG with a hypothetical unrecognized ancillary chunk type `abCd` (lowercase first type byte,
uppercase third byte per the reserved-bit rule) inserted between `IHDR` and `IDAT`, and asserts
`Load` still decodes the expected pixel successfully, proving the new critical-chunk rejection does
not affect the existing ancillary-chunk-skip behavior.

#### CanvasNet-Codecs-PngCodec-LoadMalformedChunkType: Load Rejects a Malformed Chunk Type Code

**Tests**: `PngCodec_Load_ChunkTypeWithNonLetterByte_ThrowsInvalidDataException`,
`PngCodec_Load_ChunkTypeWithLowercaseReservedByte_ThrowsInvalidDataException`

Builds a valid `IHDR` followed by a chunk whose type code is `a!cd` (a non-ASCII-letter second
byte), and asserts `Load` throws `InvalidDataException` mentioning "ASCII letter". Separately
builds a valid `IHDR` followed by a chunk whose type code is `abcd` (all four bytes lowercase, so
its third byte violates the PNG specification's reserved-bit rule), and asserts `Load` throws
`InvalidDataException` mentioning "reserved-bit rule" - proving both malformed cases are rejected
before the codec's first-byte-only critical/ancillary classification ever sees them.

#### CanvasNet-Codecs-PngCodec-LoadPlteForbiddenForGrayscale: Load Rejects PLTE on Grayscale Color Types

**Tests**: `PngCodec_Load_PlteOnGrayscale_ThrowsInvalidDataException`,
`PngCodec_Load_PlteOnGrayscaleAlpha_ThrowsInvalidDataException`

Builds a Grayscale (color type 0) `IHDR` followed by a `PLTE` chunk, and separately a
Grayscale-with-alpha (color type 4) `IHDR` followed by a `PLTE` chunk, and asserts `Load` throws
`InvalidDataException` in both cases, since grayscale samples are never resolved through a
palette.

#### CanvasNet-Codecs-PngCodec-LoadTrnsOrderRequiresPlteForPalette: Load Rejects tRNS Before PLTE

**Test**: `PngCodec_Load_TrnsBeforePlteForIndexedColor_ThrowsInvalidDataException`

Builds a Palette (color type 3) `IHDR` followed by a `tRNS` chunk and then a `PLTE` chunk (the
reverse of the order the PNG specification requires), and asserts `Load` throws
`InvalidDataException` with a message naming `PLTE` as the cause.

#### CanvasNet-Codecs-PngCodec-LoadPlteAfterTrns: Load Rejects PLTE Appearing After tRNS

**Test**: `PngCodec_Load_PlteAfterTrns_ThrowsInvalidDataException`

Builds a Truecolor (color type 2, where PLTE is an optional suggested palette rather than
mandatory) `IHDR` followed by a `tRNS` chunk and then a `PLTE` chunk, and asserts `Load` throws
`InvalidDataException` with a message naming `tRNS` as the cause, proving the PLTE-before-tRNS
ordering requirement is enforced in both directions and for every color type that can legally
carry both chunks, not only the already-covered indexed-color (Palette) tRNS-before-PLTE case.

#### CanvasNet-Codecs-PngCodec-LoadTrnsForbiddenForAlphaColorTypes: Load Rejects tRNS on Alpha Color Types

**Tests**: `PngCodec_Load_TrnsOnGrayscaleAlpha_ThrowsInvalidDataException`,
`PngCodec_Load_TrnsOnTruecolorAlpha_ThrowsInvalidDataException`

Builds a Grayscale-with-alpha (color type 4) `IHDR` followed by a `tRNS` chunk, and separately a
Truecolor-with-alpha (color type 6) `IHDR` followed by a `tRNS` chunk, and asserts `Load` throws
`InvalidDataException` in both cases, since both color types already carry a full per-pixel alpha
channel that leaves nothing for a single-key-color transparency chunk to add.

#### CanvasNet-Codecs-PngCodec-LoadTrnsKeyExceedsBitDepthRange: Load Rejects a tRNS Key Outside the Bit-Depth Range

**Tests**: `PngCodec_Load_TrnsGrayscaleKeyExceedsBitDepthRange_ThrowsInvalidDataException`,
`PngCodec_Load_TrnsTruecolorKeyExceedsBitDepthRange_ThrowsInvalidDataException`

Builds a 1-bit Grayscale `IHDR` (whose only representable sample values are 0 and 1) followed by
a `tRNS` chunk declaring an out-of-range gray key of 200, and separately an 8-bit Truecolor
`IHDR` followed by a `tRNS` chunk declaring an out-of-range red key of 256 (stored as the 2-byte
big-endian value `{0x01, 0x00}`, since tRNS keys are always 2 bytes regardless of bit depth), and
asserts `Load` throws `InvalidDataException` in both cases, since a key that can never match any
real pixel at the file's declared bit depth is non-conforming.

#### CanvasNet-Codecs-PngCodec-LoadNonConsecutiveIdat: Load Rejects Non-Consecutive IDAT Chunks

**Tests**: `PngCodec_Load_NonConsecutiveIdatChunks_ThrowsInvalidDataException`,
`PngCodec_Load_MultipleIdatChunks_ReturnsExpectedPixels`

Builds a stream with two `IDAT` chunks separated by an unrelated ancillary (`tEXt`) chunk, and
asserts `Load` throws `InvalidDataException`, since the PNG specification requires every `IDAT`
chunk to be consecutive. Separately, `PngCodec_Load_MultipleIdatChunks_ReturnsExpectedPixels`
proves the no-regression case: a compressed payload deliberately split across five directly
consecutive `IDAT` chunks (with no other chunk type interleaved) still loads and decodes
successfully, since encoders commonly split one image's payload across several small IDAT chunks
for streaming purposes.

#### CanvasNet-Codecs-PngCodec-LoadIendNonEmptyPayload: Load Rejects a Non-Empty IEND Payload

**Test**: `PngCodec_Load_IendWithNonEmptyPayload_ThrowsInvalidDataException`

Builds a valid one-pixel Truecolor PNG whose terminating `IEND` chunk declares a 3-byte payload
(with a correct CRC-32 computed over that payload, so the rejection is due to the length check,
not an incidental CRC mismatch), and asserts `Load` throws `InvalidDataException` naming `IEND`
as the cause, since the PNG specification defines `IEND` as always carrying zero bytes of data.

#### CanvasNet-Codecs-PngCodec-LoadTrailingDataAfterIend: Load Rejects Data After the IEND Chunk

**Test**: `PngCodec_Load_TrailingDataAfterIend_ThrowsInvalidDataException`

Builds a complete, valid one-pixel Truecolor PNG (`IHDR` + `IDAT` + a CRC-valid, empty-payload
`IEND` chunk), then appends one extra, arbitrary byte after the `IEND` chunk, and asserts `Load`
throws `InvalidDataException` naming `IEND` as the cause, since the PNG specification requires
`IEND` to be the final chunk in the datastream.

#### CanvasNet-Codecs-PngCodec-PngSuiteSupported: PngSuite Files Within Scope Load Successfully

**Test**: `PngCodec_Load_PngSuiteSupportedFile_ReturnsCanvas` (`[Theory]` over 126 PngSuite files)

Loads every well-formed, non-interlaced PngSuite conformance file — verified directly against
each file's raw IHDR bytes rather than trusted from its filename — covering every color type
(Grayscale, Truecolor, Palette, Grayscale-with-alpha, Truecolor-with-alpha) and every bit depth
each color type permits (1, 2, 4, 8, or 16 as applicable), and asserts `Load` returns a surface
with non-zero width and height, without throwing.

#### CanvasNet-Codecs-PngCodec-PngSuiteUnsupported: Adam7-Interlaced PngSuite Files Rejected by Load; GetInfo Still Succeeds

**Tests**: `PngCodec_Load_PngSuiteUnsupportedFile_ThrowsInvalidDataException`,
`PngSuiteUnsupportedFile_GetInfoReturnsCorrectDimensions` (`[Theory]` over 35 PngSuite files)

Loads every PngSuite conformance file that is structurally well-formed but Adam7-interlaced —
verified directly against each file's raw IHDR bytes — and asserts `Load` throws
`InvalidDataException` for every one, rather than silently producing incorrect pixels. Separately
calls `GetInfo` on the same 35 files and asserts it succeeds, reporting the file's exact declared
IHDR width and height (read directly from each file's raw bytes, not merely asserted positive,
which would pass even if the reported dimensions were wrong, for example swapped), since Adam7
interlacing does not affect the declared dimensions and is not itself a well-formedness defect.

#### CanvasNet-Codecs-PngCodec-PngSuiteCorrupt: PngSuite Files Corrupt At/Before IHDR Are Rejected by Both Load and GetInfo

**Tests**: `PngCodec_Load_PngSuiteCorruptFile_ThrowsInvalidDataException`,
`PngCodec_GetInfo_PngSuiteCorruptFile_ThrowsInvalidDataException` (`[Theory]` over 12 PngSuite
files each)

Loads every PngSuite conformance file that is deliberately corrupt at or before its IHDR chunk
(bad signature, bad IHDR CRC-32, or an invalid color-type/bit-depth combination — verified
directly against each file's raw bytes), and asserts `Load` throws `InvalidDataException` for
every one. Separately calls `GetInfo` on the same 12 files and asserts it also throws
`InvalidDataException`, since these files are well-formedness defects rather than merely a
decode-capability limitation `GetInfo` is designed to tolerate (unlike the Adam7-interlaced files
above, which `GetInfo` still accepts). This deliberately excludes the two files covered by
`CanvasNet-Codecs-PngCodec-PngSuiteCorruptAfterIhdr` below, whose corruption lies entirely after a
well-formed IHDR chunk and is therefore never encountered by `GetInfo`.

#### CanvasNet-Codecs-PngCodec-PngSuiteCorruptAfterIhdr: Corrupt-After-IHDR Files: Load Rejects, GetInfo Succeeds

**Test**: `PngCodec_PngSuiteCorruptAfterIhdrFile_GetInfoReturnsCorrectDimensions_ButLoadThrows`
(`[Theory]` over 2 PngSuite files)

Exercises the two PngSuite files whose IHDR chunk is well-formed but whose corruption lies
entirely in a later chunk — `xcsn0g01.png` (its IDAT chunk's CRC-32 bytes literally spell the
ASCII text "CSUM" rather than a computed checksum) and `xdtn0g01.png` (its IDAT chunk is missing
entirely, verified directly against each file's raw bytes) — and asserts, for each, that `GetInfo`
succeeds, reporting the file's exact declared IHDR width and height (read directly from the raw
bytes), while `Load` on the same file throws `InvalidDataException`. `GetInfo` never encounters
either corruption since it reads only the signature and IHDR chunk, exactly like the
Adam7-interlaced files in `CanvasNet-Codecs-PngCodec-PngSuiteUnsupported` above — but unlike those
files, `Load` rejects both because they are genuinely malformed, not merely because of an
unimplemented decode feature.

#### CanvasNet-Codecs-PngCodec-GetInfo: GetInfo Reports Dimensions/Channels/Alpha Without Decoding Pixels

**Tests**: `PngCodec_GetInfo_Rgb_ReturnsExpectedInfoWithoutAlpha`,
`PngCodec_GetInfo_Rgba_ReturnsExpectedInfoWithAlpha`, `PngCodec_GetInfo_NeverReadsPastIhdr`,
`PngCodec_GetInfo_SucceedsWithCorruptIdatRegion_ButLoadThrows`,
`PngCodec_GetInfo_OversizedDimensions_ReturnsRawValue_ButLoadThrows`,
`PngCodec_GetInfo_NonIhdrFirstChunkWithHugeDeclaredLength_ThrowsWithoutLargeAllocation`,
`PngCodec_GetInfo_IhdrChunkWithWrongDeclaredLength_ThrowsWithoutLargeAllocation`

Saves a surface at `Rgb` and separately at `Rgba`, calls `GetInfo` on each, and asserts the
returned `ImageInfo` reports the correct width/height, `Channels` (3 or 4), and `HasAlpha` (false
or true). Proves `GetInfo` never reads past the `IHDR` chunk by wrapping a valid RGBA PNG's bytes
in a `BoundedReadStream` capped at exactly 33 bytes (the signature plus the first chunk frame) and
asserting `GetInfo` still succeeds. Proves `GetInfo` never needs to decompress `IDAT` by
corrupting a saved file's final Adler-32 byte and asserting `GetInfo` still returns the correct
info while `Load` on the same bytes throws `InvalidDataException`. Proves `GetInfo` does not
enforce `Surface.MaxDimension` by building a minimal `IHDR` declaring a width one greater than
`Surface.MaxDimension`, asserting `GetInfo` returns that raw oversized width without throwing, and
then asserting `Load` on the exact same bytes still throws `InvalidDataException`. Proves, by
measuring `GC.GetAllocatedBytesForCurrentThread()` before/after the call (never wall-clock time),
that a crafted first chunk declaring a huge (100 MB) length is rejected by `ReadIhdrChunkFrame`
before any length-dependent allocation is attempted — once for a non-`IHDR` first chunk type, and
once for an `IHDR` chunk whose declared length is not the mandatory 13 — asserting both a bounded
(well under 1 MB) allocation delta and `InvalidDataException` in each case.

#### CanvasNet-Codecs-PngCodec-GetInfoAnyDecodability: GetInfo Succeeds Regardless of Whether Load Can Decode the File

**Tests**: `PngCodec_GetInfo_Grayscale_ReturnsExpectedInfoWithoutAlpha`,
`PngCodec_GetInfo_GrayscaleAlpha_ReturnsExpectedInfoWithAlpha`,
`PngCodec_GetInfo_Palette_ReturnsRawFileEncodingNotDecodedRgba`,
`PngCodec_GetInfo_SubByteGrayscaleBitDepth_ReturnsCorrectDimensions` (`[Theory]` over bit depths
1, 2, 4), `PngCodec_GetInfo_BitDepth16_ReturnsCorrectDimensions`,
`PngCodec_GetInfoAndLoad_InvalidBitDepth_BothThrowInvalidDataException` (`[Theory]`),
`PngCodec_GetInfoAndLoad_InvalidColorType_BothThrowInvalidDataException` (`[Theory]`)

Calls `GetInfo` on hand-built Grayscale and Grayscale-with-alpha PNGs and asserts the correct
`Channels`/`HasAlpha` mapping (1/false and 2/true respectively). Calls `GetInfo` on a hand-built
Palette PNG and asserts it reports `Channels=1, HasAlpha=false` — the raw file encoding (one
palette-index sample per pixel, packed at sub-byte bit depths) — deliberately not the four-channel
RGBA result `Load` would produce after
resolving indices through `PLTE`/`tRNS`. Calls `GetInfo` on hand-built sub-byte-depth (1, 2, 4)
and 16-bit Grayscale PNGs and asserts the correct declared width/height are reported. Calls both
`GetInfo` and `Load` on a hand-built PNG declaring an invalid bit depth, and separately an invalid
color type, and asserts both methods throw `InvalidDataException` symmetrically — proving
well-formedness (as opposed to decodability) is enforced identically by both entry points.

#### CanvasNet-Codecs-PngCodec-GetInfoValidation: GetInfo Rejects Invalid Arguments and Malformed Headers

**Tests**: `PngCodec_GetInfo_NullStream_ThrowsArgumentNullException`,
`PngCodec_GetInfo_NullPath_ThrowsArgumentNullException`,
`PngCodec_GetInfo_EmptyPath_ThrowsArgumentException`,
`PngCodec_GetInfo_BadSignature_ThrowsInvalidDataException`,
`PngCodec_GetInfo_PngSuiteCorruptFile_ThrowsInvalidDataException`

Calls `GetInfo(Stream)` with a null stream, `GetInfo(string)` with a null path and separately an
empty path, and `GetInfo(Stream)` with an 8-byte all-zero buffer (an incorrect signature),
asserting `ArgumentNullException`, `ArgumentNullException`, `ArgumentException`, and
`InvalidDataException` respectively — the same exception contract as the corresponding `Load`
scenarios. Also calls `GetInfo(string)` on every deliberately corrupt PngSuite conformance file
(see `CanvasNet-Codecs-PngCodec-PngSuiteCorrupt` above) and asserts `InvalidDataException` for
every one.

### Acceptance Criteria

A unit test run passes when all test methods above pass without error or unexpected exception; any
unexpected exception type or wrong return/byte value constitutes a failure. Across
`PngCodecTests.cs` and `PngSuiteTests.cs`, this totals 78 test methods (74 in `PngCodecTests.cs`
and 4 in `PngSuiteTests.cs`), which expand to a much larger number of executed xUnit test cases
when every `[Theory]` data row is included, covering the full 175-file PngSuite conformance
corpus.
