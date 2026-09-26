## GifCodec Unit Verification Design

This document describes the unit-level verification strategy for the `GifCodec` class.

### Verification Approach

The `GifCodec` unit is verified through two complementary test files. `GifCodecTests.cs` exercises
`Load` and `GetInfo` in isolation, using hand-built `MemoryStream` byte sequences (constructed with
private helper methods, including a companion GIF-native LZW encoder that mirrors the production
decoder's bit-packing and code-table growth) to cover argument validation, malformed/unsupported
GIF data, and the interlacing/transparency/sub-region-blit code paths precisely. `GifFixtureTests.cs`
exercises the same public API against 8 real-world GIF files produced by an independent encoder
(see `GifFixtures\README.md`), reading each solid-color fixture's own Global Color Table bytes
directly rather than assuming a hardcoded RGB value, and confirming the three animated fixtures
decode their first frame without throwing. Because `GifCodec`'s only documented dependency is
`Surface` (a sibling in-house unit, not an external service), no mocking or stubbing is required.

Unit tests reside in `GifCodecTests.cs` and `GifFixtureTests.cs` within the
`DemaConsulting.CanvasNet.Tests` project.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `GifCodec`'s only dependency is the in-house `Surface` unit
- **Isolation**: Each hand-built-byte-stream test constructs its own `MemoryStream`; each fixture
  test reads a read-only file copied to the test output directory by the test project's
  `GifFixtures\**` content item; no shared state between tests

### Unit-Level Test Scenarios

#### CanvasNet-Codecs-GifCodec-Load: Load Decodes First-Frame Pixels Matching the Color Table

**Tests**: `GifCodec_Load_FirstFrame_DecodesExpectedPixels`,
`GifCodec_Load_FromFilePath_ReturnsExpectedPixels`,
`GifCodec_Load_SolidColorFixture_MatchesOwnGlobalColorTable`,
`GifCodec_Load_LocalColorTableOnly_DecodesExpectedPixels`

Builds a small hand-crafted GIF (2x2 pixels, an explicit Global Color Table, and LZW-compressed
index data) and asserts every decoded pixel's RGB and alpha (255) matches expectations; repeats
via the `Load(string)` file-path overload against a temporary file; for each of the 5
solid-color real-world fixtures, reads that file's own Global Color Table entry 0 directly from
its raw bytes and asserts multiple sampled pixels (all four corners plus the center) returned by
`Load` match it exactly; and builds a hand-crafted GIF with no Global Color Table at all, whose
sole Image Descriptor declares its own Local Color Table, asserting the decoded pixels match that
Local Color Table's entries exactly.

#### CanvasNet-Codecs-GifCodec-FirstFrameOnly: Multi-Frame GIFs Decode Only the First Frame

**Tests**: `GifCodec_Load_MultiFrame_DecodesFirstFrameOnlyWithoutThrowing`,
`GifCodec_Load_AnimatedFixture_DecodesFirstFrameWithoutThrowing`

Builds a hand-crafted GIF containing two Image Descriptors before the Trailer and asserts `Load`
succeeds without throwing, returning a surface matching the first Image Descriptor's pixels; and,
for each of the 3 animated real-world fixtures (each containing 3 frames), asserts `Load` succeeds
and returns a surface sized to the Logical Screen Descriptor's declared dimensions.

#### CanvasNet-Codecs-GifCodec-Interlacing: Interlaced Rows Are Restored to Normal Order

**Test**: `GifCodec_Load_InterlacedImage_DeinterlacesCorrectly`

Builds a hand-crafted GIF whose Image Descriptor sets the interlace flag and whose LZW-compressed
data encodes pixel rows in GIF's four-pass interlaced order (computed as the exact inverse of the
production de-interlacing algorithm), and asserts the decoded surface's rows appear in normal
top-to-bottom order matching the pre-interlacing source pixel grid.

#### CanvasNet-Codecs-GifCodec-TransparentColorIndex: Transparent Color Index Is Read From Byte Offset 3

**Test**: `GifCodec_Load_TransparentColorIndex_ProducesAlphaZero`

Builds a hand-crafted GIF with a Graphic Control Extension whose transparency flag is set, whose
delay-time bytes (offset 1-2) are deliberately set to a *different* color table index than the
actual transparent color index (offset 3), and whose Image Descriptor's pixel data includes that
transparent index. Asserts the decoded pixel at that index has alpha 0, proving the codec reads
the transparent index from byte offset 3 and not from a delay-time byte such as offset 1.

#### CanvasNet-Codecs-GifCodec-SubRegionBlit: Image Descriptor Is Blitted at Its Declared Offset

**Test**: `GifCodec_Load_SubRegionImageDescriptor_BlitsAtCorrectOffset`

Builds a hand-crafted GIF whose Logical Screen Descriptor declares a larger canvas than its single
Image Descriptor, which itself declares a non-zero left/top offset, and asserts the decoded
surface's pixels appear at that offset rather than at (0, 0).

#### CanvasNet-Codecs-GifCodec-LoadNullStream / LoadNullPath / LoadEmptyPath: Load Rejects Invalid Arguments

**Tests**: `GifCodec_Load_NullStream_ThrowsArgumentNullException`,
`GifCodec_Load_NullPath_ThrowsArgumentNullException`,
`GifCodec_Load_EmptyPath_ThrowsArgumentException`

Calls `Load(Stream)` with a null stream, `Load(string)` with a null path, and `Load(string)` with
an empty path, asserting `ArgumentNullException`, `ArgumentNullException`, and
`ArgumentException` respectively.

#### CanvasNet-Codecs-GifCodec-LoadBadSignature: Load Rejects a Missing GIF Signature

**Test**: `GifCodec_Load_BadSignature_ThrowsInvalidDataException`

Builds a stream beginning with "NOTAGIF" instead of "GIF87a"/"GIF89a" and asserts `Load` throws
`InvalidDataException`.

#### CanvasNet-Codecs-GifCodec-LoadExceedsMaxDimension: Load Rejects Dimensions Exceeding Surface.MaxDimension

**Tests**: `GifCodec_Load_WidthExceedsMaxDimension_ThrowsInvalidDataException`,
`GifCodec_Load_HeightExceedsMaxDimension_ThrowsInvalidDataException`

Builds a Logical Screen Descriptor declaring width one greater than `Surface.MaxDimension`, and
separately height one greater, and asserts `Load` throws `InvalidDataException` (not the
`ArgumentOutOfRangeException` that would otherwise escape from `Surface`'s constructor) in both
cases.

#### CanvasNet-Codecs-GifCodec-NoColorTable: Load Rejects Missing Color Table

**Tests**: `GifCodec_Load_NoColorTable_ThrowsInvalidDataException`,
`GifCodec_Load_SecondFrameMissingColorTable_ThrowsInvalidDataException`

Builds a GIF with no Global Color Table and an Image Descriptor with no Local Color Table, and
asserts `Load` throws `InvalidDataException`. Separately, builds a multi-frame GIF with no Global
Color Table whose first frame supplies its own Local Color Table (and so decodes successfully) but
whose second frame has neither a Local Color Table nor a Global Color Table to fall back on, and
asserts `Load` still throws `InvalidDataException` even though this codec never decodes the second
frame's pixel data - proving every Image Descriptor's color table is validated, not merely the
first.

#### CanvasNet-Codecs-GifCodec-MalformedGraphicControlExtension: Load Rejects a Wrong-Length GCE

**Tests**: `GifCodec_Load_MalformedGraphicControlExtension_ThrowsInvalidDataException`,
`GifCodec_Load_FragmentedGraphicControlExtension_ThrowsInvalidDataException`

Builds a Graphic Control Extension sub-block declaring a data length of 3 bytes instead of the
required 4, and asserts `Load` throws `InvalidDataException`. Separately, builds a Graphic Control
Extension whose 4 bytes of data are split across two 2-byte sub-blocks (which would reassemble to
the correct total byte count under a naive concatenating reader) and asserts `Load` still throws
`InvalidDataException`, proving the Graphic Control Extension is parsed as exactly one 4-byte
sub-block followed by the block terminator, per the GIF89a specification, rather than merely
checking the reassembled total length.

#### CanvasNet-Codecs-GifCodec-UnexpectedBlockIntroducer: Load Rejects an Unrecognized Block Byte

**Test**: `GifCodec_Load_UnexpectedBlockIntroducer_ThrowsInvalidDataException`

Builds a stream whose top-level block introducer byte is `0x99` (neither Extension Introducer,
Image Descriptor, nor Trailer), and asserts `Load` throws `InvalidDataException`.

#### CanvasNet-Codecs-GifCodec-TrailingDataAfterTrailer: Load Rejects Trailing Bytes

**Test**: `GifCodec_Load_TrailingDataAfterTrailer_ThrowsInvalidDataException`

Builds a well-formed single-frame GIF followed by one extra byte after the Trailer, and asserts
`Load` throws `InvalidDataException`.

#### CanvasNet-Codecs-GifCodec-NoImageDescriptor: Load Rejects a Stream With No Frame

**Test**: `GifCodec_Load_NoImageDescriptor_ThrowsInvalidDataException`

Builds a GIF containing only a Logical Screen Descriptor and Trailer, with no Image Descriptor in
between, and asserts `Load` throws `InvalidDataException`.

#### CanvasNet-Codecs-GifCodec-ImageDescriptorOutOfBounds: Load Rejects an Out-of-Bounds Image Descriptor

**Test**: `GifCodec_Load_ImageDescriptorOutOfBounds_ThrowsInvalidDataException`

Builds an Image Descriptor whose left offset plus width exceeds the Logical Screen Descriptor's
canvas width, and asserts `Load` throws `InvalidDataException`.

#### CanvasNet-Codecs-GifCodec-TruncatedStream: Load Rejects Truncated Streams

**Tests**: `GifCodec_Load_TruncatedAtSignature_ThrowsInvalidDataException`,
`GifCodec_Load_TruncatedAtColorTable_ThrowsInvalidDataException`,
`GifCodec_Load_TruncatedAtSubBlock_ThrowsInvalidDataException`

Truncates a well-formed GIF stream at three distinct points — mid-signature, mid-color-table, and
mid-sub-block — and asserts `Load` throws `InvalidDataException` in each case.

#### CanvasNet-Codecs-GifCodec-InvalidLzwCode: Load Rejects an Unresolvable LZW Code

**Test**: `GifCodec_Load_InvalidLzwCode_ThrowsInvalidDataException`

Builds LZW-compressed data containing a code value that is neither Clear Code, End-of-Information,
an already-defined table entry, nor the valid next KwKwK code, and asserts `Load` throws
`InvalidDataException`.

#### CanvasNet-Codecs-GifCodec-InsufficientLzwOutput: Load Rejects Short LZW Output

**Test**: `GifCodec_Load_InsufficientLzwOutput_ThrowsInvalidDataException`

Builds LZW-compressed data that decodes to fewer index values than the Image Descriptor's declared
width times height, and asserts `Load` throws `InvalidDataException`.

#### CanvasNet-Codecs-GifCodec-LzwRequiresEoi: Load Rejects Missing EOI and Output-Count Overrun

**Tests**: `GifCodec_Load_LzwStreamMissingEoi_ThrowsInvalidDataException`,
`GifCodec_Load_LzwStreamOverrunsExpectedCount_ThrowsInvalidDataException`

Builds LZW-compressed data that decodes exactly the Image Descriptor's declared width times
height index values but ends without ever emitting an End-of-Information code, and asserts `Load`
throws `InvalidDataException` rather than treating the stream as successfully decoded. Separately,
builds a hand-written LZW code sequence whose table-entry (KwKwK back-reference) expansion would
decode more index values than the declared width times height before any End-of-Information code
is read, and asserts `Load` throws `InvalidDataException` immediately rather than silently
truncating the excess output.

#### CanvasNet-Codecs-GifCodec-SubBlockBudget: Load Rejects Excessive Total Sub-Block Data

**Tests**: `GifCodec_Load_ExcessiveSubBlockData_ThrowsInvalidDataException`,
`GifCodec_Load_GraphicControlExtensionExceedsRemainingSubBlockBudget_ThrowsInvalidDataException`

Builds a hand-crafted GIF with tiny (1x1) declared canvas dimensions containing a single Comment
Extension whose sub-block chain's cumulative declared size is one byte more than
`GifCodec.MaxTotalSubBlockBytes`, and asserts `Load` throws `InvalidDataException` rather than
buffering an unbounded amount of sub-block data. Separately, builds a GIF whose Comment Extension
consumes all but 2 bytes of the shared sub-block budget, followed by an otherwise well-formed
4-byte Graphic Control Extension, and asserts `Load` still throws `InvalidDataException` -
proving the dedicated Graphic Control Extension reader also checks the shared remaining budget
before decrementing it (rather than letting the budget go negative and silently accepting the
extension), consistent with `ReadSubBlocks`' enforcement for every other sub-block chain.

#### CanvasNet-Codecs-GifCodec-GetInfo: GetInfo Reports Dimensions/Channels/CanDecode Without Decoding Pixels

**Tests**: `GifCodec_GetInfo_ReturnsExpectedDimensionsChannelsAndCanDecode`,
`GifCodec_GetInfo_NeverReadsPixelData`,
`GifCodec_GetInfo_OversizedDimensions_ReturnsRawValue_ButLoadThrows`,
`GifCodec_GetInfo_Fixture_MatchesLoadDimensionsAndReportsDecodable`

Calls `GetInfo` on a valid hand-built GIF and asserts the returned `ImageInfo` reports the correct
width/height, `Channels == 1`, and `CanDecode == true`. Proves `GetInfo` never reads pixel data by
wrapping a valid GIF's bytes in a `BoundedReadStream` capped at exactly the signature-plus-Logical-
Screen-Descriptor size (13 bytes) and asserting `GetInfo` still succeeds. Proves `GetInfo` does not
enforce `Surface.MaxDimension` by building a Logical Screen Descriptor declaring a width/height one
greater than `Surface.MaxDimension`, asserting `GetInfo` returns those raw oversized values without
throwing, and then asserting `Load` on the exact same bytes still throws `InvalidDataException`.
Also confirms, for all 8 real-world fixtures, that `GetInfo`'s reported dimensions match `Load`'s
resulting surface dimensions and that `CanDecode` is `true`.

#### CanvasNet-Codecs-GifCodec-GetInfoValidation: GetInfo Rejects Invalid Arguments and a Bad Signature

**Tests**: `GifCodec_GetInfo_NullStream_ThrowsArgumentNullException`,
`GifCodec_GetInfo_NullPath_ThrowsArgumentNullException`,
`GifCodec_GetInfo_EmptyPath_ThrowsArgumentException`,
`GifCodec_GetInfo_BadSignature_ThrowsInvalidDataException`

Calls `GetInfo(Stream)` with a null stream, `GetInfo(string)` with a null path and separately an
empty path, and `GetInfo(Stream)` with a stream carrying an incorrect signature, asserting
`ArgumentNullException`, `ArgumentNullException`, `ArgumentException`, and `InvalidDataException`
respectively — the same exception contract as the corresponding `Load` scenarios.

### Acceptance Criteria

A unit test run passes when all test methods above (37 in `GifCodecTests.cs` plus 16 fixture-based
theory cases in `GifFixtureTests.cs`) pass without error or unexpected exception; any unexpected
exception type or pixel-value mismatch constitutes a failure.
