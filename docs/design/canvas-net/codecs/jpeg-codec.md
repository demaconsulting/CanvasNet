## JpegCodec

![Codecs Structure](CodecsView.svg)

The `JpegCodec` class is the sixth software unit in CanvasNet, and depends on `Surface` exactly as
`BmpCodec`, `PngCodec`, and `TiffCodec` do. It provides hand-rolled loading and saving of a
common real-world subset of JPEG (ITU-T T.81 / ISO/IEC 10918-1) files to and from `Surface` pixel
buffers.

### Purpose

`JpegCodec` lets callers persist a `Surface` as a JPEG file (or stream) and load a JPEG file (or
stream) back into a `Surface`. It is implemented entirely against the .NET base class library, with
no third-party JPEG or imaging library dependency. `Load` supports baseline sequential DCT (SOF0)
and progressive DCT (SOF2) frames, 1-component grayscale and 3-component YCbCr images, arbitrary
1x1 or 2x2 sampling factors per component (covering 4:4:4, 4:2:2, and 4:2:0 chroma
subsampling), restart markers, and both 8-bit and 16-bit quantization tables. `Save` writes a
baseline-only, 3-component YCbCr, 4:2:0 chroma-subsampled JPEG using the standard ITU-T Annex K
example quantization and Huffman tables scaled by a caller-supplied quality value. Four-component
CMYK/YCCK images, arithmetic coding, and any SOF marker other than SOF0/SOF2 are explicitly out
of scope and are rejected with `System.IO.InvalidDataException` rather than silently producing
incorrect pixels.

`JpegCodec` is a `static` class: JPEG encoding and decoding carry no instance state, so a static
utility shape was chosen over an object with nothing to construct or configure, matching
`BmpCodec`, `PngCodec`, and `TiffCodec`.

### Data Model

#### JPEG marker/segment stream

| Marker | Name | Written by `Save` | Consumed by `Load` | Notes |
| ------ | ---- | ----------------- | ------------------ | ----- |
| `0xFFD8` | SOI | Yes | Yes | Required start-of-image marker. |
| `0xFFDB` | DQT | Yes | Yes | One or more quantization tables, 8-bit or 16-bit on load. |
| `0xFFC0` | SOF0 | Yes | Yes | Baseline sequential frame header. |
| `0xFFC2` | SOF2 | No | Yes | Progressive frame header. |
| `0xFFC4` | DHT | Yes | Yes | Huffman tables embedded in the file. |
| `0xFFDD` | DRI | No | Yes | Restart interval for restart-marked files. |
| `0xFFDA` | SOS | Yes | Yes | Scan header; progressive files may have multiple scans. |
| `0xFFD0`-`0xFFD7` | RST0-RST7 | No | Yes | Restart markers inside entropy-coded data. |
| `0xFFD9` | EOI | Yes | Yes | Required end-of-image marker. |

APPn/COM and other unknown length-prefixed segments are skipped on load. `Save` intentionally does
not emit an APP0/JFIF segment because the codec's decoder does not depend on it and the ITU-T T.81
base JPEG syntax does not require it.

#### SOF and SOS payload fields

| Segment | Field | Description |
| ------- | ----- | ----------- |
| SOF0 / SOF2 | Precision | Must be 8 on load; `Save` always writes 8-bit samples. |
| SOF0 / SOF2 | Height / Width | Pixel dimensions of the decoded image. |
| SOF0 / SOF2 | Component count | `Load` accepts only 1-component grayscale or 3-component YCbCr. |
| SOF0 / SOF2 | Sampling factors | Per-component horizontal/vertical sampling, restricted to 1 or 2. |
| SOF0 / SOF2 | Quantization selector | Index of the DQT table used by that component. |
| SOS | Component selectors | Associates each scan component with DC and AC Huffman tables. |
| SOS | `Ss`, `Se`, `Ah`, `Al` | Progressive-only scan controls; baseline files use `0, 63, 0, 0`. |

For decoding, the component sampling factors determine each component's MCU-grid block dimensions,
chroma upsampling ratio, and plane size before conversion back to RGB.

#### Huffman table construction

Each DHT segment supplies 16 code-length counts followed by a flat symbol list. `JpegCodec` builds
canonical Huffman tables from that data using the `minCode`/`maxCode`/`valPtr` structure described
in ITU-T T.81 Annex F; every scan then decodes coefficients strictly from the file's own embedded
DC/AC tables. No implicit “standard” table is assumed to exist in the input stream.

`Save` uses the ITU-T Annex K default luminance/chrominance DC and AC Huffman tables. Those tables
are serialized into the output DHT segments and then reused by the encoder's entropy writer.

#### Quantization tables and zigzag order

JPEG coefficients are stored in zigzag order rather than natural 8x8 row-major order. `JpegCodec`
maintains the standard 64-entry zigzag mapping (ITU-T T.81 Figure A.6) so that:

- `Load` dequantizes each coefficient from zigzag order back into its natural 8x8 position before
  applying the separable inverse DCT.
- `Save` performs the forward DCT in natural order, quantizes with scaled luminance or chrominance
  tables, then reorders coefficients into zigzag order before Huffman encoding.

The encoder starts from the ITU-T Annex K example luminance and chrominance quantization tables,
then applies the usual libjpeg-style quality scaling formula for caller-supplied qualities 1-100.

### Key Methods

#### Load(Stream stream)

Reads a JPEG image from an open stream by buffering the remaining bytes into memory, validating the
SOI marker, parsing DQT/DHT/DRI/SOF/SOS segments, and decoding one or more baseline or progressive
scans into coefficient blocks. Immediately after parsing the SOF segment, and before any MCU-grid
width/height arithmetic used to size the coefficient buffers, validates that the frame's width and
height are positive and do not exceed `Surface.MaxDimension` (8192). After entropy decoding, it
dequantizes, performs a separable float IDCT, upsamples chroma as required by the frame's sampling
factors, converts YCbCr back to RGB, and writes fully opaque pixels into a new `Surface`.

**Architectural decision:** `Load` supports both baseline (SOF0) and progressive (SOF2) JPEG, and
builds every Huffman and quantization table from the file's own DHT/DQT segments rather than
assuming any implicit standard tables are present.

**Throws:**

- `ArgumentNullException` — `stream` is null
- `InvalidDataException` — the stream does not begin with SOI; an unsupported SOF marker,
  arithmetic-coded variant, or unsupported component count is encountered; a frame width or height
  that is non-positive or exceeds `Surface.MaxDimension`; a referenced DHT/DQT table is missing; a
  mandatory SOF/DHT/DQT/SOS segment is missing; the marker/segment structure is malformed; or the
  stream ends before all header or entropy-coded data has been read

#### Load(string path)

Opens `path` as a read-only `FileStream` and delegates to `Load(Stream)`.

**Architectural decision:** the path overload is a thin convenience wrapper over `Load(Stream)`, so
all JPEG parsing, progressive/baseline handling, and malformed-data rejection remain in one decode
path.

**Throws:**

- `ArgumentNullException` — `path` is null
- `ArgumentException` — `path` is an empty string
- `InvalidDataException` — see `Load(Stream)`
- Underlying file-system exceptions (`FileNotFoundException`, `DirectoryNotFoundException`,
  `UnauthorizedAccessException`, `IOException`) propagate uncaught

#### Save(Surface surface, Stream stream, int quality = 90)

Writes `surface` to `stream` as a baseline (SOF0), 3-component YCbCr, 4:2:0 chroma-subsampled JPEG
with the requested quality. The encoder converts RGB to YCbCr, pads partial MCUs by edge
replication, downsamples chroma by 2x2 box filtering, performs a separable float FDCT, quantizes
with quality-scaled Annex K tables, Huffman-encodes the zigzag-ordered coefficients, and writes
the required SOI/DQT/SOF0/DHT/SOS/EOI segments.

**Architectural decision:** because JPEG is a lossy format, round-trip verification for `Save`
compares decoded pixels to the source within a tolerance rather than exact equality.

**Architectural decision:** `Save` always writes 3-component YCbCr with 4:2:0 chroma subsampling,
using the standard Annex K default Huffman tables and Annex K example quantization tables scaled
by the libjpeg-style quality formula; it never writes progressive scans or APP0/JFIF metadata.

**Throws:**

- `ArgumentNullException` — `surface` or `stream` is null
- `ArgumentOutOfRangeException` — `quality` is less than 1 or greater than 100

#### Save(Surface surface, string path, int quality = 90)

Creates or overwrites `path` as a `FileStream` and delegates to `Save(Surface, Stream, int)`.

**Architectural decision:** the path overload deliberately shares the exact same encoder as the
stream overload, so quality scaling, 4:2:0 baseline output, and all argument validation behavior
stay synchronized across both public entry points.

**Throws:**

- `ArgumentNullException` — `surface` or `path` is null
- `ArgumentException` — `path` is an empty string
- `ArgumentOutOfRangeException` — see `Save(Surface, Stream, int)`
- Underlying file-system exceptions (`UnauthorizedAccessException`, `DirectoryNotFoundException`,
  `IOException`) propagate uncaught

#### GetInfo(Stream stream)

Reports a JPEG's width, height, and component count without ever entropy-decoding scan data (and
therefore without requiring an SOS segment, restart markers, or any entropy-coded bytes to be
present at all). Reads a bounded prefix of the stream — at most `MaxProbeHeaderBytes`
(1,048,576 bytes / 1 MiB) — into an in-memory buffer via `ReadBoundedPrefix`, then scans that
buffer's markers with `ProbeDimensions`: validating the SOI marker, then repeatedly reading a
marker and, for any marker other than SOF0/SOF2, skipping over its length-prefixed segment
(reusing the same `SkipLengthPrefixedSegment` helper `Load`'s own marker loop uses) without
inspecting its payload, until an SOF0 or SOF2 marker is found and parsed (reusing `Load`'s own
`ReadSof` helper with `enforceMaxDimension: false`) or the buffer is exhausted. Because scanning
stops the instant SOF0/SOF2 is found — before ever reaching an SOS marker or any entropy-coded
byte — a stream containing only SOI/DQT/DHT/SOF and nothing else is a fully valid input to
`GetInfo`. `Channels` is the SOF frame's component count (1 for grayscale, 3 for YCbCr);
`HasAlpha` is always `false`, since JPEG has no alpha channel.

**Architectural decision:** unlike `BmpCodec`/`PngCodec` (whose headers have a small, fixed
maximum size) and unlike `TiffCodec` (which can seek directly to its IFD), a JPEG's SOF marker can
in principle be preceded by an unbounded run of APPn/COM segments (each up to 65,533 bytes), so an
unbounded sequential scan is not safe against a pathological or malicious stream. `GetInfo`
therefore reads and scans at most `MaxProbeHeaderBytes` bytes; if no SOF0/SOF2 marker is found
within that budget, it throws `InvalidDataException` with a message distinguishing "probe limit
reached with more data possibly remaining" from "stream ended before an SOF marker was found" (the
latter also covers the ordinary truncated/malformed-header case).

**Throws:**

- `ArgumentNullException` — `stream` is null
- `InvalidDataException` — the stream does not begin with SOI; an unsupported SOF marker or
  unsupported component count is encountered; an SOS marker is encountered before any SOF0/SOF2
  marker; the marker/segment structure is malformed; the stream ends before an SOF0/SOF2 marker is
  found; or the `MaxProbeHeaderBytes` probe limit is reached before an SOF0/SOF2 marker is found
  (same contract as `Load`, except the `Surface.MaxDimension` check is skipped, entropy-coded scan
  data is never required or read, and the probe-limit case is new to `GetInfo`)

#### GetInfo(string path)

Opens `path` as a read-only `FileStream` and delegates to `GetInfo(Stream)`.

**Throws:**

- `ArgumentNullException` — `path` is null
- `ArgumentException` — `path` is an empty string
- `InvalidDataException` — see `GetInfo(Stream)`
- Underlying file-system exceptions propagate uncaught

### Error Handling

All argument validation happens at the start of each public method, before any image data is read
or written. `Load` validates the marker stream incrementally, failing immediately for unsupported
SOF markers, four-component frames, missing DHT/DQT/SOF/SOS segments, malformed entropy-coded
structure, or truncated data. There is no local recovery or retry logic inside `JpegCodec`; every
validation failure results in an exception that propagates directly to the caller. `Save` validates
`surface`, `stream`/`path`, and `quality` before the encoder writes any bytes, so an invalid call
fails fast without producing a partial JPEG.

### Dependencies

`JpegCodec` depends on `Surface` (constructing surfaces in `Load` and reading rows via
`Surface.GetRowSpanBytes` in `Save`), using only `Surface`'s existing public API exactly as
`BmpCodec`, `PngCodec`, and `TiffCodec` do. No new public members were added to `Surface` or
`Rgba32` to support this codec. `JpegCodec` also depends on the `Codecs` subsystem's shared
`ImageInfo` record struct as the return type of `GetInfo` — see _Codecs Subsystem Design_
(`../codecs.md`). Beyond `Surface` and `ImageInfo`, `JpegCodec` uses only the .NET base class
library's `System.IO` namespace (`Stream`, `FileStream`, `MemoryStream`,
`InvalidDataException`) and `System.Numerics.Vector<T>` for optional vector acceleration in the
IDCT/FDCT dot-product and YCbCr-to-RGB hot paths. No new runtime NuGet package is introduced.

### Conformance Testing

In addition to hand-built malformed-stream tests and synthetic round-trip checks, `JpegCodec` is
validated against the `JpegFixtures` corpus (see `test/DemaConsulting.CanvasNet.Tests/JpegFixtures/README.md`):
five JPEG files generated with ImageMagick from the PngSuite `basn2c08.png` source image,
covering baseline 4:4:4, baseline 4:2:2, baseline 4:2:0, progressive 4:2:0, and grayscale
baseline files. Every fixture must load successfully with the expected 32x32 dimensions; color
fixtures are compared against the source PNG using the exact per-channel tolerances from
`JpegFixtureTests.cs` — 15 for `baseline_422.jpg` and `baseline_444.jpg`, and 30 for
`baseline_420.jpg` and `progressive_420.jpg`; the grayscale fixture is checked for successful
loading, correct dimensions, and exact `R == G == B` expansion. The in-memory round-trip tests in
`JpegCodecTests.cs` additionally enforce a 15-per-channel tolerance on synthetic quality-90 save
then load scenarios.

### Callers

`JpegCodec` is a public API entry point invoked directly by consumers of the CanvasNet package; it
is not called by any other unit within this system. It calls into `Surface` (see _Dependencies_
above) but nothing calls into it from within CanvasNet itself.
