## PngCodec

![Codecs Structure](CodecsView.svg)

The `PngCodec` class is the fourth software unit in CanvasNet, and depends on `Surface` exactly as
`BmpCodec` does. It provides hand-rolled loading and saving of a restricted subset of PNG files
(8-bit-per-channel Truecolor or Truecolor-with-alpha, non-interlaced) to and from `Surface` pixel
buffers.

### Purpose

`PngCodec` lets callers persist a `Surface` as a PNG file (or stream) and load a PNG file (or
stream) back into a `Surface`. It is implemented entirely against the .NET base class library's
`System.IO` and `System.IO.Compression` types, with no third-party PNG or imaging library
dependency, and supports only 8-bit-per-channel color type 2 (Truecolor/RGB) and color type 6
(Truecolor with alpha/RGBA), with standard (non-interlaced) scanline order. Grayscale (0),
grayscale-with-alpha (4), and palette/indexed (3) color types, any bit depth other than 8, and
Adam7 interlacing are explicitly out of scope and are rejected with a descriptive
`System.IO.InvalidDataException` rather than silently producing incorrect pixels.

`PngCodec` is a `static` class: PNG encoding/decoding has no instance state to carry, so a static
utility shape was chosen over an object with nothing to construct or configure, matching
`BmpCodec`'s precedent.

### Data Model

#### PngColorType enum

| Value  | Numeric Value | Description                                                          |
| ------ | ------------- | -------------------------------------------------------------------- |
| `Rgb`  | 2             | 8 bits each for red, green, blue. Alpha is dropped when saving.      |
| `Rgba` | 6             | 8 bits each for red, green, blue, alpha. Alpha is preserved exactly. |

#### PNG signature (8 bytes)

| Byte Offset | Value                      |
| ----------- | -------------------------- |
| 0-7         | `137 80 78 71 13 10 26 10` |

#### Chunk layout (all multi-byte fields big-endian)

| Field  | Size           | Description                                               |
| ------ | -------------- | --------------------------------------------------------- |
| Length | 4              | Number of bytes in the `Data` field                       |
| Type   | 4              | 4-character ASCII chunk type (for example `IHDR`, `IDAT`) |
| Data   | `Length` bytes | The chunk's payload                                       |
| CRC-32 | 4              | CRC-32 of `Type` + `Data` (not `Length`)                  |

#### IHDR chunk (13 bytes)

| Offset | Size | Field              | Value Written/Accepted                       |
| ------ | ---- | ------------------ | -------------------------------------------- |
| 0      | 4    | Width              | `surface.Width` (must be greater than zero)  |
| 4      | 4    | Height             | `surface.Height` (must be greater than zero) |
| 8      | 1    | Bit depth          | 8 (only value accepted)                      |
| 9      | 1    | Color type         | 2 (RGB) or 6 (RGBA)                          |
| 10     | 1    | Compression method | 0 (zlib/DEFLATE, only value accepted)        |
| 11     | 1    | Filter method      | 0 (adaptive filtering, only value accepted)  |
| 12     | 1    | Interlace method   | 0 (none, only value accepted)                |

#### Zlib wrapper layout (the `IDAT` payload, concatenated across chunks)

| Field            | Size         | Description                                                                        |
| ---------------- | ------------ | ---------------------------------------------------------------------------------- |
| CMF/FLG header   | 2 bytes      | Fixed `0x78 0x9C` on write; validated (method/FCHECK/no preset dictionary) on read |
| DEFLATE data     | variable     | Compressed scanline bytes, via `System.IO.Compression.DeflateStream`               |
| Adler-32 trailer | 4 bytes (BE) | Checksum of the decompressed scanline bytes, hand-computed                         |

#### Scanline filter types

| Type | Name    | Reconstruction (raw byte = filtered byte + ...)                 |
| ---- | ------- | --------------------------------------------------------------- |
| 0    | None    | 0 (no change)                                                   |
| 1    | Sub     | the byte `bpp` positions to the left in the same (raw) row      |
| 2    | Up      | the byte at the same position in the previous (raw) row         |
| 3    | Average | floor((left + up) / 2), using the raw left/up bytes (0 if none) |
| 4    | Paeth   | the Paeth predictor of left, up, and upper-left raw bytes       |

`bpp` is the number of bytes per pixel (3 for RGB, 4 for RGBA, since bit depth is always 8). All
five filter types are reconstructed on load. On save, every scanline uses filter type 0 (None) -
see _Key Methods_ below for the rationale.

All multi-byte PNG fields (chunk length, CRC-32, IHDR width/height, Adler-32 trailer) are read
and written by explicit byte composition (bit shifting), never `BitConverter` or
`BinaryPrimitives`, so behavior is identical regardless of host CPU endianness.

### Key Methods

#### Load(Stream stream)

Reads a PNG image from an open stream. Validates the 8-byte PNG signature, then reads chunks
until `IEND` is found: each chunk's CRC-32 is validated regardless of type; `IHDR` is parsed and
validated (bit depth 8; color type 2 or 6; compression method 0; filter method 0; interlace
method 0; width and height are positive and do not exceed `Surface.MaxDimension` (16384) — checked
before any width/height arithmetic, including the row-byte-width (`width * channels`) computation
performed both while decoding scanlines and by `Load` itself); `IDAT` chunk data is concatenated
across as many chunks as are present; any other chunk type (for example `tEXt`, `pHYs`, `gAMA`) is
CRC-validated but otherwise skipped. Once `IEND` is reached, the concatenated `IDAT` payload is
unwrapped as a zlib stream (2-byte header validated, `DeflateStream` inflates the DEFLATE data,
the 4-byte Adler-32 trailer is validated against the decompressed bytes), then each scanline is
defiltered (reconstructing all five standard filter types) and unpacked into the destination
`Surface`'s rows via `Surface.GetRowSpanBytes`, forcing alpha to 255 for RGB source data.

**Throws:**

- `ArgumentNullException` — `stream` is null
- `InvalidDataException` — missing PNG signature; missing, duplicate, or malformed `IHDR`; an
  `IDAT` or `IEND` chunk encountered before `IHDR`; unsupported bit depth, color type, compression
  method, filter method, or interlace method; non-positive width or height, or width/height
  exceeding `Surface.MaxDimension`; any chunk's CRC-32 mismatch; a malformed or unsupported zlib
  header; an Adler-32 checksum mismatch; an unexpected decompressed data length; an unsupported
  scanline filter type; or the stream ends before all header, chunk, or pixel data has been read

#### Load(string path)

Opens `path` as a read-only `FileStream` and delegates to `Load(Stream)`.

**Throws:**

- `ArgumentNullException` — `path` is null
- `ArgumentException` — `path` is an empty string
- `InvalidDataException` — see `Load(Stream)`
- Underlying file-system exceptions (`FileNotFoundException`, `DirectoryNotFoundException`,
  `UnauthorizedAccessException`, `IOException`) propagate uncaught

#### Save(Surface surface, Stream stream, PngColorType colorType = PngColorType.Rgba)

Writes `surface` to `stream` as a PNG image. Writes the signature and `IHDR` chunk, then builds a
single in-memory buffer containing every scanline (a filter-type byte of 0, followed by the
row's packed pixel bytes, packing RGBA into RGB when `colorType` is `Rgb`), zlib-compresses that
entire buffer (`DeflateStream` with `CompressionLevel.Optimal`, wrapped with a fixed `0x78 0x9C`
header and a computed Adler-32 trailer), and writes the compressed payload across as many `IDAT`
chunks as needed (bounded to 8192 bytes of data each), followed by an empty `IEND` chunk.

**Architectural decision**: every scanline is filtered using type 0 (None) rather than a
per-row heuristic filter selection. None is always a correct choice (it never depends on
neighboring pixel values) and is by far the simplest to implement correctly, at the cost of a
somewhat larger compressed size than an adaptively chosen filter would produce for some images.
Since `Load` fully reconstructs all five standard filter types, this asymmetry (write only type
0, but read any of the five) does not compromise interoperability with other PNG tools' output.

**Architectural decision**: the default `colorType` is `Rgba` because it preserves full
round-trip fidelity (including alpha) with no extra caller effort - the least-surprising default,
matching `BmpCodec`'s own precedent of defaulting to the alpha-preserving option. Callers who want
a smaller, alpha-free file pass `PngColorType.Rgb` explicitly.

**Throws:**

- `ArgumentNullException` — `surface` or `stream` is null
- `ArgumentOutOfRangeException` — `colorType` is not a defined `PngColorType` value

#### Save(Surface surface, string path, PngColorType colorType = PngColorType.Rgba)

Creates (or overwrites) `path` as a `FileStream` and delegates to `Save(Surface, Stream, PngColorType)`.

**Throws:**

- `ArgumentNullException` — `surface` or `path` is null
- `ArgumentException` — `path` is an empty string
- `ArgumentOutOfRangeException` — see `Save(Surface, Stream, PngColorType)`
- Underlying file-system exceptions (`UnauthorizedAccessException`, `DirectoryNotFoundException`,
  `IOException`) propagate uncaught

### Error Handling

All argument validation happens at the start of each public method, before any header or pixel
data is read or written. `Load` performs incremental format validation as each chunk is read,
failing at the first invalid chunk, header field, checksum, or filter type with a message naming
the actual invalid value found. There is no local recovery or retry logic anywhere in `PngCodec`

- every validation failure results in an exception that propagates directly to the caller. `Save`
never mutates the destination stream/file if an argument validation fails, because all argument
checks precede any byte write.

### Dependencies

`PngCodec` depends on `Surface` (constructing surfaces in `Load` and reading/writing rows via
`Surface.GetRowSpanBytes` in `Save`), using only `Surface`'s existing public API exactly as
`BmpCodec` does. No new public members were added to `Surface` or `Rgba32` to support this codec.
Beyond `Surface`, `PngCodec` uses only the .NET base class library's `System.IO` namespace
(`Stream`, `FileStream`, `InvalidDataException`) and `System.IO.Compression.DeflateStream`
(available on every one of CanvasNet's target frameworks with no new runtime NuGet dependency);
the zlib wrapper (2-byte header, Adler-32 trailer) and every PNG chunk's CRC-32 are computed by
hand-rolled algorithms rather than any third-party library.

### Conformance Testing

In addition to the hand-built positive/negative unit tests above, `PngCodec` is validated against
the industry-standard [PngSuite](http://www.schaik.com/pngsuite/) conformance corpus (Willem van
Schaik, 1996-2011; freeware, redistributed under `PngSuite.LICENSE`). Each of the corpus's 175
test files' actual IHDR fields were verified directly against its raw bytes (not trusted from its
filename) and classified into exactly one of three groups: files within `PngCodec`'s supported
feature set (must load successfully), structurally valid files using an out-of-scope feature such
as grayscale, palette, non-8-bit, or interlaced encoding (must be rejected with
`InvalidDataException`), and deliberately corrupt files (must also be rejected with
`InvalidDataException`). See `CanvasNet-Codecs-PngCodec-PngSuiteSupported`,
`CanvasNet-Codecs-PngCodec-PngSuiteUnsupported`, and `CanvasNet-Codecs-PngCodec-PngSuiteCorrupt` for the
corresponding requirements.

### Callers

`PngCodec` is a public API entry point invoked directly by consumers of the CanvasNet package; it
is not called by any other unit within this system. It calls into `Surface` (see _Dependencies_
above) but nothing calls into it from within CanvasNet itself.
