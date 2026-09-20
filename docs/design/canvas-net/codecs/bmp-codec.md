## BmpCodec

![Codecs Structure](CodecsView.svg)

The `BmpCodec` class is the third software unit in CanvasNet, and the first unit with a
dependency on another in-house unit. It provides hand-rolled loading and saving of uncompressed
Windows BMP files (BITMAPFILEHEADER + BITMAPINFOHEADER, BI_RGB, 24-bit or 32-bit) to and from
`Surface` pixel buffers.

### Purpose

`BmpCodec` lets callers persist a `Surface` as a BMP file (or stream) and load a BMP file (or
stream) back into a `Surface`. It is implemented entirely against the .NET base class library's
`System.IO` types, with no third-party BMP or imaging library dependency, and supports only the
common uncompressed 24-bit and 32-bit BMP variant. Palette-based formats, RLE compression, other
info-header variants (`BITMAPCOREHEADER`, `BITMAPV4HEADER`/`BITMAPV5HEADER`), and top-down
(negative-height) row order are explicitly out of scope and are rejected with a descriptive
`System.IO.InvalidDataException` rather than silently producing incorrect pixels.

`BmpCodec` is a `static` class: BMP encoding/decoding has no instance state to carry, so a static
utility shape was chosen over an object with nothing to construct or configure.

### Data Model

#### BmpBitDepth enum

| Value   | Numeric Value | Description                                                          |
| ------- | ------------- | -------------------------------------------------------------------- |
| `Bit24` | 24            | 8 bits each for blue, green, red. Alpha is dropped when saving.      |
| `Bit32` | 32            | 8 bits each for blue, green, red, alpha. Alpha is preserved exactly. |

#### BITMAPFILEHEADER (14 bytes, little-endian)

| Offset | Size | Field         | Value Written                      |
| ------ | ---- | ------------- | ---------------------------------- |
| 0      | 2    | `bfType`      | `0x42, 0x4D` ("BM")                |
| 2      | 4    | `bfSize`      | Total file size, in bytes          |
| 6      | 2    | `bfReserved1` | 0                                  |
| 8      | 2    | `bfReserved2` | 0                                  |
| 10     | 4    | `bfOffBits`   | 54 (14 + 40; offset to pixel data) |

#### BITMAPINFOHEADER (40 bytes, little-endian) — the only info-header variant supported

| Offset | Size | Field             | Value Written                                |
| ------ | ---- | ----------------- | -------------------------------------------- |
| 14     | 4    | `biSize`          | 40                                           |
| 18     | 4    | `biWidth`         | `surface.Width`                              |
| 22     | 4    | `biHeight`        | `surface.Height` (always positive/bottom-up) |
| 26     | 2    | `biPlanes`        | 1                                            |
| 28     | 2    | `biBitCount`      | 24 or 32                                     |
| 30     | 4    | `biCompression`   | 0 (BI_RGB)                                   |
| 34     | 4    | `biSizeImage`     | Padded-row-bytes x height                    |
| 38     | 4    | `biXPelsPerMeter` | 0                                            |
| 42     | 4    | `biYPelsPerMeter` | 0                                            |
| 46     | 4    | `biClrUsed`       | 0                                            |
| 50     | 4    | `biClrImportant`  | 0                                            |

All multi-byte header fields are read and written by explicit byte composition (bit shifting),
never `BitConverter` or `BinaryPrimitives`, so behavior is identical regardless of host CPU
endianness.

### Key Methods

#### Load(Stream stream)

Reads a BMP image from an open stream. Validates the `"BM"` signature, that `biSize == 40`
(rejecting both `BITMAPCOREHEADER` and V4/V5 headers in one check), that `biCompression == 0`
(BI_RGB), that `biBitCount` is 24 or 32, and that `biHeight` is not negative. Also validates that
neither `biWidth` nor `biHeight` exceeds `Surface.MaxDimension` (8192) — checked immediately
after the existing non-positive/negative-height checks and before any padded-row-size arithmetic,
so an oversized declared dimension is rejected with `InvalidDataException` rather than reaching
`Surface`'s constructor as an unhandled `ArgumentOutOfRangeException`. Skips forward to
`bfOffBits` (tolerating any nonstandard gap between the header and pixel data), then reads each
padded row bottom-up into a reused scratch buffer and unpacks it into the appropriate surface row
via `Surface.GetRowSpanBytes`, swapping BGR(A) to RGBA and forcing alpha to 255 for 24-bit source
data.

**Throws:**

- `ArgumentNullException` — `stream` is null
- `InvalidDataException` — missing `"BM"` signature; `biSize != 40`; `biCompression != 0`;
  `biBitCount` not 24 or 32; negative `biHeight`; non-positive width or zero height; `biWidth` or
  `biHeight` exceeding `Surface.MaxDimension`; the stream ends before all header or pixel data has
  been read

#### Load(string path)

Opens `path` as a read-only `FileStream` and delegates to `Load(Stream)`.

**Throws:**

- `ArgumentNullException` — `path` is null
- `ArgumentException` — `path` is an empty string
- `InvalidDataException` — see `Load(Stream)`
- Underlying file-system exceptions (`FileNotFoundException`, `DirectoryNotFoundException`,
  `UnauthorizedAccessException`, `IOException`) propagate uncaught

#### Save(Surface surface, Stream stream, BmpBitDepth bitDepth = BmpBitDepth.Bit32)

Writes `surface` to `stream` as an uncompressed BMP. Computes the padded row size
(`(width * bytesPerPixel + 3) & ~3`) and total pixel-data size, writes the file and info headers,
then writes each row bottom-up (surface's last row first) into a single reused scratch buffer per
row, packing RGBA into BGR (24-bit, alpha dropped) or BGRA (32-bit, alpha preserved) and
zero-filling any trailing padding bytes.

**Architectural decision**: the default `bitDepth` is `Bit32` because it preserves full
round-trip fidelity (including alpha) with no extra caller effort — the least-surprising default,
matching `Surface`'s own precedent of choosing the least-surprising no-effort default. Callers who
want a smaller, alpha-free file pass `BmpBitDepth.Bit24` explicitly.

**Throws:**

- `ArgumentNullException` — `surface` or `stream` is null
- `ArgumentOutOfRangeException` — `bitDepth` is not a defined `BmpBitDepth` value

#### Save(Surface surface, string path, BmpBitDepth bitDepth = BmpBitDepth.Bit32)

Creates (or overwrites) `path` as a `FileStream` and delegates to `Save(Surface, Stream, BmpBitDepth)`.

**Throws:**

- `ArgumentNullException` — `surface` or `path` is null
- `ArgumentException` — `path` is an empty string
- `ArgumentOutOfRangeException` — see `Save(Surface, Stream, BmpBitDepth)`
- Underlying file-system exceptions (`UnauthorizedAccessException`, `DirectoryNotFoundException`,
  `IOException`) propagate uncaught

#### GetInfo(Stream stream)

Reads only the 54-byte BMP header (`BITMAPFILEHEADER` + `BITMAPINFOHEADER`) and returns an
`ImageInfo` describing the file, without reading any pixel data. Internally, `Load` and `GetInfo`
share a single private `ParseHeader(Stream, bool enforceMaxDimension)` helper that performs every
header validation `Load` performs (signature, `biSize`, `biCompression`, `biBitCount`, non-negative
`biHeight`); `GetInfo` calls it with `enforceMaxDimension: false`, so an oversized declared width or
height is returned as-is in the resulting `ImageInfo` rather than throwing — only `Load` (which
calls the same helper with `enforceMaxDimension: true`) rejects it. `Channels` is 3 for a 24-bit
header and 4 for a 32-bit header; `HasAlpha` is `false` for 24-bit and `true` for 32-bit.

**Throws:**

- `ArgumentNullException` — `stream` is null
- `InvalidDataException` — missing `"BM"` signature; `biSize != 40`; `biCompression != 0`;
  `biBitCount` not 24 or 32; negative `biHeight`; non-positive width or zero height; the stream
  ends before the 54-byte header has been fully read (same contract as `Load`, except the
  `Surface.MaxDimension` check is skipped)

#### GetInfo(string path)

Opens `path` as a read-only `FileStream` and delegates to `GetInfo(Stream)`.

**Throws:**

- `ArgumentNullException` — `path` is null
- `ArgumentException` — `path` is an empty string
- `InvalidDataException` — see `GetInfo(Stream)`
- Underlying file-system exceptions propagate uncaught

### Error Handling

All argument validation happens at the start of each public method, before any header or pixel
data is read or written. `Load` performs incremental format validation as each header field is
read, failing at the first invalid field with a message naming the actual invalid value found
(for example the actual `biSize` or `biBitCount` read). There is no local recovery or retry logic
anywhere in `BmpCodec` — every validation failure results in an exception that propagates
directly to the caller. `Save` never mutates the destination stream/file if an argument
validation fails, because all argument checks precede any header write.

### Dependencies

`BmpCodec` depends on `Surface` (constructing surfaces in `Load` and reading rows via
`Surface.GetRowSpanBytes` in `Save`) — this is the first documented inter-unit dependency in
CanvasNet. No new public members were added to `Surface` to support this: its existing
constructor and `GetRowSpanBytes` accessor were already sufficient. `BmpCodec` also depends on
the `Codecs` subsystem's shared `ImageInfo` record struct as the return type of `GetInfo` — see
_Codecs Subsystem Design_ (`../codecs.md`). Beyond `Surface` and `ImageInfo`, `BmpCodec` uses only
the .NET base class library's `System.IO` namespace (`Stream`, `FileStream`,
`InvalidDataException`), available on every one of CanvasNet's target frameworks with no new
runtime NuGet dependency.

### Callers

`BmpCodec` is a public API entry point invoked directly by consumers of the CanvasNet package; it
is not called by any other unit within this system. It calls into `Surface` (see _Dependencies_
above) but nothing calls into it from within CanvasNet itself.
