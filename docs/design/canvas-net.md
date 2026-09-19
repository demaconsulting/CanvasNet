# System Design

This document provides the system-level design for CanvasNet.

![CanvasNet Structure](CanvasNetView.svg)

## Architecture

CanvasNet is a .NET library providing a canvas-based drawing and rendering API, following
DEMA Consulting best practices. The system consists of two implemented subsystems, plus one
subsystem reserved for future work:

- **Canvas subsystem** (namespace `CanvasNet.Canvas`, folder
  `src/DemaConsulting.CanvasNet/Canvas/`): the pixel-buffer primitives on which all other
  functionality builds — the `Surface` unit (a mutable, in-memory 32-bit RGBA pixel buffer with
  span-based row access and independent-copy cropping) and the `Rgba32` unit (a single-pixel
  value type, documented inline within `Surface`). See _Canvas Subsystem Design_ (`canvas.md`).
- **Codecs subsystem** (namespace `CanvasNet.Codecs`, folder
  `src/DemaConsulting.CanvasNet/Codecs/`, flat — no further nesting): four hand-rolled image
  format codecs, each converting to and from a `CanvasNet.Canvas.Surface` pixel buffer —
  `BmpCodec` (uncompressed 24-bit/32-bit Windows BMP), `PngCodec` (8-bit-per-channel Truecolor
  and Truecolor-with-alpha, non-interlaced PNG), `TiffCodec` (8-bit-per-sample RGB, RGBA, and
  Grayscale, strip-based TIFF 6.0 with None/PackBits/LZW/Deflate compression, either byte order),
  and `JpegCodec` (a common real-world subset of JPEG: baseline/progressive decode with
  4:4:4/4:2:2/4:2:0 support, baseline 4:2:0 encode). See _Codecs Subsystem Design_ (`codecs.md`).
- **Drawing subsystem** (reserved): not yet implemented. No folder, namespace, or documentation
  exists for it yet. It is reserved for future drawing primitives (shapes, brushes, pens,
  transforms) that will build on top of the `Canvas` subsystem's `Surface` unit.

The `Codecs` subsystem depends on the `Canvas` subsystem's `Surface` unit (constructing surfaces
and reading/writing rows via `Surface.GetRowSpanBytes`); the `Canvas` subsystem has no dependency
on `Codecs` or on any other subsystem. Within the `Codecs` subsystem, its four units are flat and
mutually independent — none of `BmpCodec`, `PngCodec`, `TiffCodec`, or `JpegCodec` depends on any
other codec. See _Surface Unit Design_ (`canvas/surface.md`), _BmpCodec Unit Design_
(`codecs/bmp-codec.md`), _PngCodec Unit Design_ (`codecs/png-codec.md`),
_TiffCodec Unit Design_ (`codecs/tiff-codec.md`), and _JpegCodec Unit Design_
(`codecs/jpeg-codec.md`) for each unit's internal collaboration.

## External Interfaces

The system exposes the following public API to external consumers:

- **Surface(int width, int height)**: Constructor; allocates a fully transparent pixel buffer of
  the specified size. Throws `ArgumentOutOfRangeException` if `width` or `height` is less than
  or equal to zero.
- **Surface.Width** / **Surface.Height**: Read-only properties exposing the surface dimensions.
- **Surface[int x, int y]**: Indexer providing single-pixel get/set access. Throws
  `ArgumentOutOfRangeException` if `x` or `y` is out of range.
- **Surface.GetRowSpanBytes(int y)**: Returns a `Span<byte>` over a row's raw bytes. Throws
  `ArgumentOutOfRangeException` if `y` is out of range.
- **Surface.GetRowSpan(int y)**: Returns a `Span<Rgba32>` over a row's pixels. Throws
  `ArgumentOutOfRangeException` if `y` is out of range.
- **Surface.Crop(int x, int y, int width, int height)**: Returns a new, independent `Surface`
  containing a copy of the specified sub-region. Throws `ArgumentOutOfRangeException` if any
  argument is invalid or the region exceeds the source bounds.
- **BmpCodec.Load(Stream stream)** / **BmpCodec.Load(string path)**: Loads a `Surface` from an
  uncompressed 24-bit or 32-bit BMP stream or file. Throws `ArgumentNullException` for a null
  `stream`/`path`, `ArgumentException` for an empty `path`, and `InvalidDataException` for
  malformed or unsupported BMP data.
- **BmpCodec.Save(Surface surface, Stream stream, BmpBitDepth bitDepth)** /
  **BmpCodec.Save(Surface surface, string path, BmpBitDepth bitDepth)**: Saves a `Surface` as an
  uncompressed 24-bit or 32-bit BMP stream or file. Throws `ArgumentNullException` for a null
  `surface`/`stream`/`path`, `ArgumentException` for an empty `path`, and
  `ArgumentOutOfRangeException` for an undefined `bitDepth`.
- **PngCodec.Load(Stream stream)** / **PngCodec.Load(string path)**: Loads a `Surface` from an
  8-bit-per-channel Truecolor (RGB) or Truecolor-with-alpha (RGBA), non-interlaced PNG stream or
  file. Throws `ArgumentNullException` for a null `stream`/`path`, `ArgumentException` for an
  empty `path`, and `InvalidDataException` for malformed or unsupported PNG data.
- **PngCodec.Save(Surface surface, Stream stream, PngColorType colorType)** /
  **PngCodec.Save(Surface surface, string path, PngColorType colorType)**: Saves a `Surface` as an
  8-bit-per-channel RGB or RGBA PNG stream or file. Throws `ArgumentNullException` for a null
  `surface`/`stream`/`path`, `ArgumentException` for an empty `path`, and
  `ArgumentOutOfRangeException` for an undefined `colorType`.
- **TiffCodec.Load(Stream stream)** / **TiffCodec.Load(string path)**: Loads a `Surface` from an
  8-bit-per-sample RGB, RGBA, or Grayscale, strip-based TIFF stream or file (either byte order).
  Throws `ArgumentNullException` for a null `stream`/`path`, `ArgumentException` for an empty
  `path`, and `InvalidDataException` for malformed or unsupported TIFF data.
- **TiffCodec.Save(Surface surface, Stream stream, TiffCompression compression)** /
  **TiffCodec.Save(Surface surface, string path, TiffCompression compression)**: Saves a `Surface`
  as an 8-bit RGBA, single-strip, little-endian TIFF stream or file, with optional
  None/PackBits/LZW/Deflate compression. Throws `ArgumentNullException` for a null
  `surface`/`stream`/`path`, `ArgumentException` for an empty `path`, and
  `ArgumentOutOfRangeException` for an undefined `compression`.
- **JpegCodec.Load(Stream stream)** / **JpegCodec.Load(string path)**: Loads a `Surface` from a
  supported JPEG stream or file (baseline SOF0 or progressive SOF2; grayscale or 3-component
  YCbCr; 4:4:4/4:2:2/4:2:0 decode support). Throws `ArgumentNullException` for a null
  `stream`/`path`, `ArgumentException` for an empty `path`, and `InvalidDataException` for
  malformed or unsupported JPEG data.
- **JpegCodec.Save(Surface surface, Stream stream, int quality)** /
  **JpegCodec.Save(Surface surface, string path, int quality)**: Saves a `Surface` as a baseline,
  3-component YCbCr, 4:2:0 chroma-subsampled JPEG stream or file, with quality in the inclusive
  range 1-100. Throws `ArgumentNullException` for a null `surface`/`stream`/`path`,
  `ArgumentException` for an empty `path`, and `ArgumentOutOfRangeException` for an out-of-range
  `quality`.

| Interface                       | Direction        | Format                         | Constraints                 |
| ------------------------------- | ---------------- | ------------------------------ | --------------------------- |
| `Surface(int, int)`             | Inbound          | Constructor call               | `width > 0`, `height > 0`   |
| `Surface[int, int]`             | Inbound/Outbound | Indexer get/set                | `x`, `y` within bounds      |
| `Surface.GetRowSpanBytes(int)`  | Outbound         | `Span<byte>` return            | `y` within bounds           |
| `Surface.GetRowSpan(int)`       | Outbound         | `Span<Rgba32>` return          | `y` within bounds           |
| `Surface.Crop(int,int,int,int)` | Inbound/Outbound | Method call / `Surface` return | Region within source bounds |
| `BmpCodec.Load(...)`            | Inbound/Outbound | Method call / `Surface` return | Valid BMP stream or path    |
| `BmpCodec.Save(...)`            | Inbound          | Method call                    | `surface` non-null          |
| `PngCodec.Load(...)`            | Inbound/Outbound | Method call / `Surface` return | Valid PNG stream or path    |
| `PngCodec.Save(...)`            | Inbound          | Method call                    | `surface` non-null          |
| `TiffCodec.Load(...)`           | Inbound/Outbound | Method call / `Surface` return | Valid TIFF stream or path   |
| `TiffCodec.Save(...)`           | Inbound          | Method call                    | `surface` non-null          |
| `JpegCodec.Load(...)`           | Inbound/Outbound | Method call / `Surface` return | Valid JPEG stream or path   |
| `JpegCodec.Save(...)`           | Inbound          | Method call                    | `surface` non-null          |

## Dependencies

CanvasNet has zero runtime NuGet dependencies — the `Surface`, `BmpCodec`, `PngCodec`,
`TiffCodec`, and `JpegCodec` units are implemented exclusively against the .NET Base Class Library
(`Surface`'s use of `Span<T>` and `MemoryMarshal` are BCL APIs available natively on every target
framework, with no runtime NuGet package required; `BmpCodec` uses only
`System.IO` types; `PngCodec` and `TiffCodec` additionally use
`System.IO.Compression.DeflateStream`; `JpegCodec` additionally uses `System.Numerics.Vector<T>`
for optional SIMD acceleration; all of these are BCL APIs available on every target framework,
with no new runtime NuGet package). The following OTS items are used for building and verifying
this system (not consumed at runtime); see
_OTS Integration Design_ (`docs/design/ots.md`) and each item's dedicated design document for
details:

- **BuildMark** — generates build-notes documentation; see _BuildMark Design_
- **FileAssert** — validates generated documents against acceptance criteria; see
  _FileAssert Design_
- **Pandoc** — converts Markdown documentation to HTML; see _Pandoc Design_
- **ReqStream** — enforces requirements-to-test traceability; see _ReqStream Design_
- **ReviewMark** — enforces file review coverage and currency; see _ReviewMark Design_
- **SarifMark** — converts CodeQL SARIF results to markdown; see _SarifMark Design_
- **SonarMark** — generates SonarCloud quality reports; see _SonarMark Design_
- **VersionMark** — captures and publishes tool-version information; see _VersionMark Design_
- **WeasyPrint** — converts HTML documentation to PDF; see _WeasyPrint Design_
- **xUnit** — executes unit and integration tests; see _xUnit Design_

## Risk Control Measures

N/A - CanvasNet currently has no safety-critical functionality requiring risk control
measures (IEC 62304 §5.3.3).

## Data Flow

**Surface construction/pixel-access path:**

1. **Input**: Constructor parameters `width`/`height`; subsequent pixel writes via the indexer,
   `GetRowSpanBytes`, or `GetRowSpan`
2. **Validation**: `Surface(int, int)` rejects non-positive dimensions with
   `ArgumentOutOfRangeException`; the indexer and both row accessors reject out-of-range
   coordinates the same way
3. **Storage**: A zero-filled `byte[]` is allocated once at construction time; pixel writes
   mutate this buffer directly, whether through the indexer or a row span
4. **Output**: Pixel reads (via the indexer or a row span) return values directly from the same
   buffer — no intermediate copies

**Surface crop path:**

1. **Input**: Method parameters `x`, `y`, `width`, `height` identifying a sub-region
2. **Validation**: `Crop` rejects negative coordinates, non-positive sizes, and regions
   exceeding the source bounds with `ArgumentOutOfRangeException`
3. **Processing**: A new `Surface` is allocated for the destination, then each row of the source
   region is copied into it in a single `Span<byte>.CopyTo` call
4. **Output**: A new, independent `Surface` containing a copy of the requested pixels

**BMP save path:**

1. **Input**: Method parameters `surface`, `stream`/`path`, and `bitDepth`
2. **Validation**: `Save` rejects a null `surface`/`stream`/`path` with `ArgumentNullException`,
   an empty `path` with `ArgumentException`, and an undefined `bitDepth` with
   `ArgumentOutOfRangeException`
3. **Processing**: Writes the BITMAPFILEHEADER and BITMAPINFOHEADER, then writes each surface row
   bottom-up into a single reused scratch buffer, swapping RGBA to BGR(A) and zero-filling any
   4-byte row-padding
4. **Output**: A complete, uncompressed BMP file written to the destination stream/file

**BMP load path:**

1. **Input**: Method parameter `stream`/`path`
2. **Validation**: `Load` rejects a null `stream`/`path` with `ArgumentNullException`, an empty
   `path` with `ArgumentException`, and malformed/unsupported BMP data (bad signature,
   unsupported header size, unsupported compression, unsupported bit depth, top-down
   orientation, truncated stream) with `InvalidDataException`
3. **Processing**: Reads each padded pixel row bottom-up into a reused scratch buffer, swapping
   BGR(A) to RGBA and forcing alpha to 255 for 24-bit source data, writing directly into the
   destination `Surface`'s rows via `Surface.GetRowSpanBytes`
4. **Output**: A new `Surface` containing the decoded pixels

**PNG save path:**

1. **Input**: Method parameters `surface`, `stream`/`path`, and `colorType`
2. **Validation**: `Save` rejects a null `surface`/`stream`/`path` with `ArgumentNullException`,
   an empty `path` with `ArgumentException`, and an undefined `colorType` with
   `ArgumentOutOfRangeException`
3. **Processing**: Writes the PNG signature and `IHDR` chunk, builds one in-memory buffer of
   every scanline (filter type 0/None, followed by packed RGB or RGBA pixel bytes), zlib-wraps
   the DEFLATE-compressed buffer with a fixed header and a computed Adler-32 trailer, and writes
   the compressed payload across as many `IDAT` chunks as needed, followed by an empty `IEND`
   chunk
4. **Output**: A complete PNG file written to the destination stream/file

**PNG load path:**

1. **Input**: Method parameter `stream`/`path`
2. **Validation**: `Load` rejects a null `stream`/`path` with `ArgumentNullException`, an empty
   `path` with `ArgumentException`, and malformed/unsupported PNG data (bad signature, unsupported
   color type, bit depth, compression method, filter method, or interlace method, a chunk CRC-32
   mismatch, a malformed or unsupported zlib header, an Adler-32 checksum mismatch, an unsupported
   scanline filter type, or a truncated stream) with `InvalidDataException`
3. **Processing**: Validates the signature and every chunk's CRC-32, parses `IHDR`, concatenates
   `IDAT` data across all chunks present, zlib-unwraps and inflates the payload, defilters each
   scanline (reconstructing all five standard filter types), and unpacks each row directly into
   the destination `Surface`'s rows via `Surface.GetRowSpanBytes`, forcing alpha to 255 for RGB
   source data
4. **Output**: A new `Surface` containing the decoded pixels

**TIFF save path:**

1. **Input**: Method parameters `surface`, `stream`/`path`, and `compression`
2. **Validation**: `Save` rejects a null `surface`/`stream`/`path` with `ArgumentNullException`,
   an empty `path` with `ArgumentException`, and an undefined `compression` with
   `ArgumentOutOfRangeException`
3. **Processing**: Packs every row's pixels as RGBA into a single in-memory buffer, applies the
   horizontal-differencing predictor to that buffer when `compression` is `Lzw` or `Deflate`,
   compresses the buffer according to `compression` (None/PackBits/LZW/Deflate), then writes a
   little-endian TIFF header and a single IFD (describing an 8-bit RGBA, Chunky, single-strip
   image, with a `Predictor` tag only when the predictor was applied) followed by the compressed
   strip data
4. **Output**: A complete TIFF file written to the destination stream/file

**TIFF load path:**

1. **Input**: Method parameter `stream`/`path`
2. **Validation**: `Load` rejects a null `stream`/`path` with `ArgumentNullException`, an empty
   `path` with `ArgumentException`, and malformed/unsupported TIFF data (bad byte-order mark,
   unsupported bit depth, photometric interpretation, compression, or planar configuration, a
   tiled layout, a missing mandatory tag, or a truncated stream) with `InvalidDataException`
3. **Processing**: Buffers the whole stream into memory (TIFF's IFD/value-array offsets require
   random access), validates the header and IFD tags, then for each strip decompresses its bytes
   according to `Compression`, reverses the horizontal-differencing predictor if `Predictor` is
   2, and unpacks each row (RGB, RGBA, or Grayscale) directly into the destination `Surface`'s
   rows via `Surface.GetRowSpanBytes`, forcing alpha to 255 where the source has no alpha channel
4. **Output**: A new `Surface` containing the decoded pixels

**JPEG save path:**

1. **Input**: Method parameters `surface`, `stream`/`path`, and `quality`
2. **Validation**: `Save` rejects a null `surface`/`stream`/`path` with `ArgumentNullException`, an
   empty `path` with `ArgumentException`, and a `quality` outside 1-100 with
   `ArgumentOutOfRangeException`
3. **Processing**: Converts surface rows from RGB to YCbCr, pads partial MCUs by edge replication,
   downsamples chroma to 4:2:0, performs a separable float FDCT, quantizes coefficients with
   quality-scaled Annex K luminance/chrominance tables, Huffman-encodes the zigzag-ordered blocks
   with Annex K default Huffman tables, and writes the required SOI/DQT/SOF0/DHT/SOS/EOI marker
   sequence
4. **Output**: A complete baseline JPEG file written to the destination stream/file

**JPEG load path:**

1. **Input**: Method parameter `stream`/`path`
2. **Validation**: `Load` rejects a null `stream`/`path` with `ArgumentNullException`, an empty
   `path` with `ArgumentException`, and malformed/unsupported JPEG data (missing SOI, unsupported
   SOF marker, unsupported component count, missing DHT/DQT/SOF/SOS segment, malformed marker
   structure, or a truncated stream) with `InvalidDataException`
3. **Processing**: Buffers the remaining stream into memory, parses DQT/DHT/DRI/SOF/SOS markers,
   decodes one or more baseline or progressive scans into coefficient blocks, dequantizes and
   applies the separable float IDCT, upsamples chroma according to the frame's sampling factors,
   converts YCbCr back to RGB (using the vectorized path where available), and writes fully opaque
   pixels into the destination `Surface`
4. **Output**: A new `Surface` containing the decoded pixels

## Design Constraints

- **Simplicity**: Minimal functionality kept easy to understand and extend
- **Compliance**: All functionality must be traceable to requirements
- **Quality**: Zero warnings, full test coverage, complete documentation
- **Portability**: Compatible across supported .NET platforms

### Platform Support

The library targets the following frameworks, enabling compatibility across modern, currently
supported .NET runtimes:

| Target Framework | Runtime / Environment                             |
| ---------------- | ------------------------------------------------- |
| `net8.0`         | .NET 8 LTS                                        |
| `net9.0`         | .NET 9                                            |
| `net10.0`        | .NET 10                                           |

The library is supported on the following operating systems:

- **Windows** — primary developer and CI platform
- **Linux** — CI/CD and containerized environments
- **macOS** — developer workstations using Apple platforms

Portability is achieved by restricting the implementation exclusively to Base Class Library (BCL)
APIs available across all target frameworks. No platform-specific native interop, OS-specific
APIs, or framework-version-specific features are used.

### Integration Patterns

- **NuGet Packaging**: Standard .NET library packaging and distribution
- **CI/CD Integration**: Automated build, test, and quality validation
- **Requirements Traceability**: All features linked to passing tests
- **Review Management**: Systematic file review using ReviewMark patterns
