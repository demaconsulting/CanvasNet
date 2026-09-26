## GifCodec

![Codecs Structure](CodecsView.svg)

The `GifCodec` class is the sixth software unit in CanvasNet's `Codecs` subsystem. It provides
hand-rolled, decode-only loading of a common real-world subset of GIF (GIF87a/GIF89a) files into
`Surface` pixel buffers.

### Purpose

`GifCodec` lets callers load a GIF file (or stream) into a `Surface`. It is implemented entirely
against the .NET base class library's `System.IO` types, with no third-party GIF or imaging
library dependency. Unlike `BmpCodec`, `PngCodec`, `TiffCodec`, and `JpegCodec`, `GifCodec` is
decode-only: it has no `Save` method.

A well-formed GIF file may contain multiple frames (an animation), but `GifCodec` decodes only
the *first* Image Descriptor's pixel data — every subsequent frame is parsed only far enough to
validate its structure (its color table, if any, and its compressed sub-block chain must still be
well-formed) and is then discarded. This is a deliberate, documented scope limitation, not a
malformed-input condition: a multi-frame GIF never causes `Load` to throw, and
`ImageInfo.CanDecode` is always `true` for a well-formed GIF file, unlike PNG's Adam7-interlacing
case (see *Codecs Subsystem Design*, `../codecs.md`, for the cross-codec `CanDecode` rationale).

`GifCodec` is a `static` class: GIF decoding has no instance state to carry, so a static utility
shape was chosen over an object with nothing to construct or configure.

### Data Model

#### Signature (6 bytes)

The first 6 bytes of a GIF file must be the ASCII string `"GIF87a"` or `"GIF89a"`; any other value
is rejected.

#### Logical Screen Descriptor (7 bytes, little-endian, immediately following the signature)

| Offset | Size | Field                  | Notes                    |
| ------ | ---- | ---------------------- | ------------------------ |
| 0      | 2    | Logical Screen Width   | Canvas width, in pixels  |
| 2      | 2    | Logical Screen Height  | Canvas height, in pixels |
| 4      | 1    | Packed Fields          | See below                |
| 5      | 1    | Background Color Index | Ignored                  |
| 6      | 1    | Pixel Aspect Ratio     | Ignored                  |

The Packed Fields byte's bit 7 is the global color table flag; bits 4-6 are the color resolution
(ignored); bit 3 is the sort flag (ignored); bits 0-2 are the global color table size exponent.

#### Color Table entry (3 bytes per entry — used for both the Global and any Local Color Table)

| Offset | Size | Field | Notes                          |
| ------ | ---- | ----- | ------------------------------ |
| 0      | 1    | Red   | Promoted to an opaque `Rgba32` |
| 1      | 1    | Green |                                |
| 2      | 1    | Blue  |                                |

A color table's entry count is `2 << (packed & 0x07)` (2 to 256 entries).

#### Graphic Control Extension data (4 bytes, following the `0x21 0xF9` introducer/label and a size byte)

| Offset | Size | Field                   | Notes                                 |
| ------ | ---- | ----------------------- | ------------------------------------- |
| 0      | 1    | Packed Fields           | See below                             |
| 1-2    | 2    | Delay Time              | Little-endian; ignored                |
| 3      | 1    | Transparent Color Index | Used only when bit 0 of byte 0 is set |

The Packed Fields byte's bit 0 is the transparency flag; bits 1-3 are the disposal method
(ignored); bit 4 is the user input flag (ignored).

**This is the offset most easily transcribed incorrectly**: the Transparent Color Index is byte 3
of the extension's data, not byte 1 (byte 1 is the low byte of the ignored, little-endian Delay
Time field). `GifCodec` reads it from `data[3]`.

#### Image Descriptor (9 bytes, little-endian, following the `0x2C` Image Separator)

| Offset | Size | Field         | Notes                                                  |
| ------ | ---- | ------------- | ------------------------------------------------------ |
| 0      | 2    | Left Position | Offset, in pixels, from the left of the logical screen |
| 2      | 2    | Top Position  | Offset, in pixels, from the top of the logical screen  |
| 4      | 2    | Width         | Frame width, in pixels                                 |
| 6      | 2    | Height        | Frame height, in pixels                                |
| 8      | 1    | Packed Fields | See below                                              |

The Packed Fields byte's bit 7 is the local color table flag; bit 6 is the interlace flag; bits
0-2 are the local color table size exponent.

Immediately following the Image Descriptor (and any Local Color Table) is a single
LZW-minimum-code-size byte, then the frame's compressed image data as a sub-block chain (see
below).

#### Sub-block chain

A sequence of length-prefixed data blocks: each block is preceded by a 1-byte size, and a
zero-length size byte terminates the chain. Used for both Extension block payloads and Image
Descriptor compressed data.

#### GIF-native LZW code-table state

A `List<byte[]>` of dynamically learned code-table entries (mirroring `TiffCodec`'s `DecodeLzw`
table shape), a `byte[]?` "previous entry" used for KwKwK reconstruction, and a `codeSize` that
starts at `minCodeSize + 1` and grows by one bit whenever the next code to be assigned would
require it (standard, non-early-change growth — distinct from `TiffCodec`'s early-change
convention), capped at 12 bits. Special code values are declared relative to the file's own
`minCodeSize`: Clear code = `1 << minCodeSize`; end-of-information code = Clear code + 1; the
first code eligible for table assignment is end-of-information + 1.

### Key Methods

#### Load(Stream stream)

Reads a GIF image from an open stream. Calls the shared `ReadLogicalScreenDescriptor` helper
(see below), validates the canvas width/height are positive and do not exceed
`Surface.MaxDimension`, reads the Global Color Table if present, then loops reading one block at
a time:

- **Extension (`0x21`)** — reads the label byte and the extension's sub-block chain via the
  shared `ReadSubBlocks` helper. A Graphic Control Extension (label `0xF9`) is validated to be
  exactly 4 bytes and its transparency flag/transparent-color-index are recorded for the *next*
  Image Descriptor only. Every other extension label's data is read and discarded (no special
  casing needed, since `ReadSubBlocks` already fully consumes it).
- **Image Descriptor (`0x2C`)** — reads the 9-byte descriptor, any Local Color Table, the
  LZW-minimum-code-size byte, and the compressed image sub-block chain. Validates the frame's
  width/height are positive and its region lies fully within the logical screen. On the *first*
  Image Descriptor only: decodes the compressed data via the GIF-native LZW decoder
  (`DecodeGifLzw`), de-interlaces it first if the interlace flag is set, resolves each palette
  index through the active color table (Local, else Global, else `InvalidDataException`) to an
  `Rgba32` (alpha 0 if the index matches a pending Graphic Control Extension's transparent index
  and its transparency flag is set, else alpha 255), allocates a canvas-sized `Surface`
  (defaulting to fully transparent), and blits the frame into it at its declared offset. Every
  subsequent Image Descriptor is fully parsed and validated but its pixel data is not decoded.
- **Trailer (`0x3B`)** — ends the block loop.
- Any other introducer byte — `InvalidDataException`.

After the loop, validates no bytes remain in the stream, and that at least one Image Descriptor
was seen.

**Throws:**

- `ArgumentNullException` — `stream` is null
- `InvalidDataException` — missing `"GIF87a"`/`"GIF89a"` signature; non-positive or
  oversized (`> Surface.MaxDimension`) width/height; no color table (Global or Local) available
  for the first Image Descriptor; a Graphic Control Extension whose data is not exactly 4 bytes;
  an Image Descriptor region lying outside the logical screen; an unexpected block introducer
  byte; bytes remaining after the Trailer; no Image Descriptor ever seen; an invalid/out-of-range
  LZW code; insufficient LZW output before the stream ends; the stream ending before all header,
  color-table, or block data has been read

#### Load(string path)

Opens `path` as a read-only `FileStream` and delegates to `Load(Stream)`.

**Throws:**

- `ArgumentNullException` — `path` is null
- `ArgumentException` — `path` is an empty string
- `InvalidDataException` — see `Load(Stream)`
- Underlying file-system exceptions (`FileNotFoundException`, `DirectoryNotFoundException`,
  `UnauthorizedAccessException`, `IOException`) propagate uncaught

#### GetInfo(Stream stream)

Calls the shared `ReadLogicalScreenDescriptor` helper and returns immediately:
`new ImageInfo(width, height, 1, false)`. No color table, extension, or image block is ever read;
`Surface.MaxDimension` is never enforced — the raw header-declared width/height are always
returned, even when they exceed it, matching `ImageInfo`'s documented "bomb triage" contract (see
*Codecs Subsystem Design*, `../codecs.md`). `Channels` is always 1 (the raw file's single
palette-index-per-pixel encoding); `HasAlpha` is always `false` (see `ImageInfo`'s remarks for the
full rationale). `CanDecode` defaults to `true` and is never overridden — see this unit's
*Purpose* section above for why a multi-frame GIF is not a `CanDecode == false` case.

**Throws:**

- `ArgumentNullException` — `stream` is null
- `InvalidDataException` — missing `"GIF87a"`/`"GIF89a"` signature; the stream ends before the
  13-byte signature and Logical Screen Descriptor has been fully read

#### GetInfo(string path)

Opens `path` as a read-only `FileStream` and delegates to `GetInfo(Stream)`.

**Throws:**

- `ArgumentNullException` — `path` is null
- `ArgumentException` — `path` is an empty string
- `InvalidDataException` — see `GetInfo(Stream)`
- Underlying file-system exceptions propagate uncaught

#### ReadLogicalScreenDescriptor(Stream stream) — shared, private

Reads and validates the 6-byte signature and 7-byte Logical Screen Descriptor, returning the
decoded width, height, and packed byte. Called identically, as the literal first statement, by
both `GetInfo(Stream)` and `Load(Stream)` — the same parity guarantee documented for the other
four raster codecs' shared header helpers (see *Codecs Subsystem Design*, `../codecs.md`'s
Header-Only Probing section), but simpler here: GIF has no analogous "skip past optional extra
header bytes" step at this stage, since the color table and all subsequent block parsing happens
only in `Load`, never in `GetInfo`.

### Error Handling

All argument validation happens at the start of each public method, before any header, color
table, or block data is read. `Load` performs incremental format validation as each block is
parsed, failing at the first invalid field or block with a message naming the actual invalid
value or byte found. There is no local recovery or retry logic anywhere in `GifCodec` — every
validation failure results in an exception that propagates directly to the caller.

### Dependencies

`GifCodec` depends on `Surface` (constructing a canvas-sized surface and writing rows via
`Surface.GetRowSpan` in `Load`) and the `Codecs` subsystem's shared `ImageInfo` record struct (the
return type of `GetInfo`) — see *Codecs Subsystem Design* (`../codecs.md`). Beyond `Surface` and
`ImageInfo`, `GifCodec` uses only the .NET base class library's `System.IO` namespace (`Stream`,
`FileStream`, `MemoryStream`, `InvalidDataException`), available on every one of CanvasNet's
target frameworks with no new runtime NuGet dependency.

### Callers

`GifCodec` is a public API entry point invoked directly by consumers of the CanvasNet package; it
is not called by any other unit within this system. It calls into `Surface` (see *Dependencies*
above) but nothing calls into it from within CanvasNet itself.
