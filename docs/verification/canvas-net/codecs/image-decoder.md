### ImageDecoder Unit Verification Design

This document describes the unit-level verification strategy for the `ImageDecoder` unit (the
`ImageFormat` enum, the `ImageFormats` recognizer, and the `ImageDecoder` dispatcher).

#### Verification Approach

The unit is verified by unit tests in `ImageFormatsTests.cs` and `ImageDecoderTests.cs` within the
`DemaConsulting.CanvasNet.Tests` project. Recognition tests feed hand-built header byte arrays
(including implausible BMP headers) and media-type strings; decoding tests produce real images
with the sibling codecs' `Save` methods (PNG, BMP, TIFF, JPEG), a hand-built 1x1 GIF, and a
`Jpeg2000Fixtures` file, and compare the decoded result with the source pixels or the sibling
codec's own `Load` result.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the sibling codecs are the in-house units the dispatcher delegates to
- **Isolation**: Each test builds its own in-memory data; a private test stream wrapper reports
  `CanSeek == false` and returns at most five bytes per read to exercise the non-seekable path

#### Unit-Level Test Scenarios

##### CanvasNet-Codecs-ImageDecoder-DetectSignature: Detect Recognizes Signatures

**Tests**: `ImageFormats_Detect_KnownSignature_ReturnsFormat`,
`ImageFormats_Detect_BmpWithPlausibleHeader_ReturnsBmp`,
`ImageFormats_Detect_ImplausibleBmpHeader_ReturnsNull`,
`ImageFormats_Detect_EmptyShortOrUnknown_ReturnsNull`

Asserts each signature maps to its format (both GIF versions, both TIFF byte orders, the JP2 box and
the raw codestream), a BMP is recognized for every accepted bitmap-header size, and that a wrong
header size, a nonzero reserved field, a 17-byte header, text starting with `BM`, empty and
truncated input, a PDF, and an SVG document all yield `null` without throwing.

##### CanvasNet-Codecs-ImageDecoder-FromMediaType: FromMediaType Maps Media Types

**Tests**: `ImageFormats_FromMediaType_KnownType_ReturnsFormat`,
`ImageFormats_FromMediaType_CaseWhitespaceAndParameters_AreNormalized`,
`ImageFormats_FromMediaType_UnsupportedOrMissing_ReturnsNull`

Asserts every recognized type and alias, that case, surrounding whitespace, and `;` parameters are
ignored, and that `null`, empty, whitespace, `image/svg+xml`, `image/x-emf`, `image/webp`, and
non-image types yield `null`.

##### CanvasNet-Codecs-ImageDecoder-Load: Load Decodes by Detected or Given Format

**Tests**: `ImageDecoder_LoadBytes_PngBmpTiff_DecodesExactPixels`,
`ImageDecoder_LoadBytes_JpegGifJpeg2000_DispatchesToMatchingCodec`,
`ImageDecoder_LoadStream_SeekableStreamAtOffset_DecodesFromStartPosition`,
`ImageDecoder_LoadStream_NonSeekableStream_DecodesWholeImage`,
`ImageDecoder_LoadStreamWithFormat_UsesGivenFormat`

Asserts lossless formats decode to exactly the source pixels, the lossy and decode-only formats
dispatch to the right codec (by dimensions and the known GIF pixel), a seekable stream positioned
inside a larger buffer is decoded from that position, a non-seekable stream is decoded completely
(the replayed header and the remainder join seamlessly), and an explicit format is honored while
data of another format is rejected by the codec.

##### CanvasNet-Codecs-ImageDecoder-LoadValidation: Load Rejects Invalid Input

**Tests**: `ImageDecoder_Load_UnrecognizedData_ThrowsInvalidDataException`,
`ImageDecoder_Load_InvalidArguments_Throw`

Asserts unrecognized bytes are rejected with `InvalidDataException` on the span, seekable-stream, and
non-seekable-stream overloads, and that a null stream throws `ArgumentNullException` and an
undefined `ImageFormat` value throws `ArgumentOutOfRangeException`.

#### Acceptance Criteria

A unit test run passes when every test method above passes without error or unexpected exception.
