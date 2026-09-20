## TiffCodec

![Codecs Structure](CodecsView.svg)

The `TiffCodec` class is the fifth software unit in CanvasNet, and depends on `Surface` exactly as
`BmpCodec` and `PngCodec` do. It provides hand-rolled loading and saving of a restricted subset of
TIFF 6.0 files (8-bit-per-sample RGB, RGBA, and Grayscale, Chunky planar configuration,
strip-based layout) to and from `Surface` pixel buffers.

### Purpose

`TiffCodec` lets callers persist a `Surface` as a TIFF file (or stream) and load a TIFF file (or
stream) back into a `Surface`. It is implemented entirely against the .NET base class library's
`System.IO` and `System.IO.Compression` types, with no third-party TIFF or imaging library
dependency, and supports only 8-bit-per-sample RGB (photometric interpretation 2, with or without
an alpha channel) and Grayscale/BlackIsZero (photometric interpretation 1) images, Chunky planar
configuration, and strip-based (not tiled) pixel data, compressed with None, PackBits, LZW, or
Deflate. Both byte orders ("II" little-endian and "MM" big-endian) are supported on load; Palette,
CMYK, YCbCr, and other photometric interpretations, any bit depth other than 8, Planar
configuration, tiled layouts, and any compression other than the four listed above are explicitly
out of scope and are rejected with a descriptive `System.IO.InvalidDataException` rather than
silently producing incorrect pixels.

`TiffCodec` is a `static` class: TIFF encoding/decoding has no instance state to carry, so a
static utility shape was chosen over an object with nothing to construct or configure, matching
`BmpCodec`'s and `PngCodec`'s precedent.

### Data Model

#### TiffCompression enum

| Value      | Numeric Value | Description                                                   |
| ---------- | ------------- | ------------------------------------------------------------- |
| `None`     | 1             | Uncompressed strip data.                                      |
| `Lzw`      | 5             | TIFF-flavor LZW (TIFF 6.0 Section 13).                        |
| `Deflate`  | 8             | zlib-wrapped DEFLATE, matching `PngCodec`'s own IDAT wrapper. |
| `PackBits` | 32773         | Apple/TIFF PackBits run-length encoding (TIFF 6.0 Section 9). |

#### TIFF header (8 bytes)

| Offset | Size | Field            | Value                                               |
| ------ | ---- | ---------------- | --------------------------------------------------- |
| 0      | 2    | Byte-order mark  | `"II"` (little-endian) or `"MM"` (big-endian)       |
| 2      | 2    | Magic number     | 42                                                  |
| 4      | 4    | First IFD offset | Byte offset of the (only) Image File Directory read |

#### IFD layout

| Field           | Size               | Description                                        |
| --------------- | ------------------ | -------------------------------------------------- |
| Entry count     | 2                  | Number of 12-byte entries that follow              |
| Entries         | `count * 12` bytes | See below                                          |
| Next IFD offset | 4                  | Ignored - only the first IFD is read (single-page) |

Each 12-byte IFD entry: 2-byte tag, 2-byte type (BYTE=1, SHORT=3, LONG=4 are handled), 4-byte
count, and a 4-byte value/offset field (holding the value directly when `count * typeSize <= 4`,
otherwise a file offset to the value's storage). All multi-byte fields (tag, type, count,
value/offset, and every value read from an external location) are read and written by explicit
byte composition using the byte order determined from the file's own byte-order mark, never
`BitConverter` or `BinaryPrimitives`, exactly as `BmpCodec`/`PngCodec` do internally.

#### Recognized TIFF tags

| Tag Number | Name                      | Required | Notes                                              |
| ---------- | ------------------------- | -------- | -------------------------------------------------- |
| 256        | ImageWidth                | Yes      |                                                    |
| 257        | ImageLength               | Yes      |                                                    |
| 258        | BitsPerSample             | Yes      | Every sample must be 8.                            |
| 259        | Compression               | Yes      | 1, 5, 8, or 32773 only.                            |
| 262        | PhotometricInterpretation | Yes      | 1 (Grayscale) or 2 (RGB) only.                     |
| 273        | StripOffsets              | Yes      | One entry per strip.                               |
| 277        | SamplesPerPixel           | No       | Defaults to `BitsPerSample`'s entry count.         |
| 278        | RowsPerStrip              | Yes      |                                                    |
| 279        | StripByteCounts           | Yes      | Same entry count as StripOffsets.                  |
| 284        | PlanarConfiguration       | No       | Defaults to 1 (Chunky); 2 (Planar) is rejected.    |
| 317        | Predictor                 | No       | Defaults to 1 (none); 2 (horizontal diff) allowed. |
| 322        | TileWidth                 | No       | Presence (with 323) means tiled TIFF; rejected.    |
| 323        | TileLength                | No       | See TileWidth.                                     |
| 338        | ExtraSamples              | No*      | *Value 2 when SamplesPerPixel is 4 (RGBA).         |

#### Horizontal-differencing predictor (Predictor tag = 2)

For each row, independently per sample plane: `raw[i] = decoded[i] - decoded[i - samplesPerPixel]`
for `i >= samplesPerPixel`, with the first `samplesPerPixel` bytes of the row left unchanged.
Decoding reverses this with a running sum. The predictor never carries across row boundaries and
is applied strictly after decompression (on load) or strictly before compression (on save).

#### Zlib wrapper layout (used only for `TiffCompression.Deflate`)

| Field            | Size         | Description                                                       |
| ---------------- | ------------ | ----------------------------------------------------------------- |
| CMF/FLG header   | 2 bytes      | Fixed `0x78 0x9C` on write; validated on read                     |
| DEFLATE data     | variable     | Compressed strip bytes, via `System.IO.Compression.DeflateStream` |
| Adler-32 trailer | 4 bytes (BE) | Checksum of the decompressed strip bytes, hand-computed           |

This is structurally identical to `PngCodec`'s own `IDAT` zlib wrapper, but reimplemented
privately within `TiffCodec.cs` rather than shared, since no unit in CanvasNet currently shares
internal helper code between codecs.

### Key Methods

#### Load(Stream stream)

Reads a TIFF image from an open stream (buffered entirely into memory first, since TIFF's IFD and
value-array offsets require random access, unlike `BmpCodec`'s and `PngCodec`'s purely sequential
formats). Validates the 8-byte header (byte-order mark, magic number 42), parses the first IFD
(ignoring any subsequent IFD offset - only single-page TIFFs are supported), and rejects a tiled
TIFF (identified by the presence of a `TileWidth` or `TileLength` tag) before validating any other
tag, so that tiled files receive a specific, actionable error rather than a misleading
"missing tag" error. Validates every mandatory tag is present, that `BitsPerSample` is 8 for every
sample, that `Compression` is one of the four supported values, that
`PhotometricInterpretation` is Grayscale (1) or RGB (2), that `PlanarConfiguration` is Chunky (1),
and that `Predictor` (if present) is None (1) or horizontal differencing (2). Also validates that
`ImageWidth` and `ImageLength` are positive and do not exceed `Surface.MaxDimension` (8192) —
checked immediately after parsing the mandatory tags and before the row-byte-width
(`width * samplesPerPixel`) arithmetic used to size and index strip data. For RGB images with
4 samples per pixel, requires an `ExtraSamples` tag of 2 (unassociated alpha); for 3 samples per
pixel, alpha is forced to 255. Reads each strip's raw bytes (per `StripOffsets`/
`StripByteCounts`), decompresses it according to `Compression`, reverses the horizontal predictor
if `Predictor` is 2, and unpacks each row into the destination `Surface`'s rows via
`Surface.GetRowSpanBytes`.

**Throws:**

- `ArgumentNullException` — `stream` is null
- `InvalidDataException` — bad byte-order mark or magic number; a tiled TIFF; a missing
  mandatory tag; an unsupported bit depth, compression, photometric interpretation, planar
  configuration, or predictor value; non-positive `ImageWidth`/`ImageLength`, or either exceeding
  `Surface.MaxDimension`; an RGB image with 4 samples per pixel lacking a correct
  `ExtraSamples` tag; mismatched `StripOffsets`/`StripByteCounts` entry counts; strip data that
  does not cover the declared image height; or the stream ends before all header, IFD, or strip
  data has been read

#### Load(string path)

Opens `path` as a read-only `FileStream` and delegates to `Load(Stream)`.

**Throws:**

- `ArgumentNullException` — `path` is null
- `ArgumentException` — `path` is an empty string
- `InvalidDataException` — see `Load(Stream)`
- Underlying file-system exceptions (`FileNotFoundException`, `DirectoryNotFoundException`,
  `UnauthorizedAccessException`, `IOException`) propagate uncaught

#### Save(Surface surface, Stream stream, TiffCompression compression = TiffCompression.None)

Writes `surface` to `stream` as a little-endian ("II") TIFF file containing a single IFD
describing an 8-bit-per-sample, 4-samples-per-pixel (RGBA) RGB image (photometric interpretation
2, with an `ExtraSamples` tag value of 2 marking the 4th sample as unassociated alpha), Chunky
planar configuration, and the entire image stored as a single strip. Compresses the strip data
according to `compression`.

**Architectural decision**: when `compression` is `TiffCompression.Lzw` or
`TiffCompression.Deflate`, `Save` automatically applies the horizontal-differencing predictor
(Predictor tag value 2) before compressing, and writes the Predictor tag accordingly, because it
materially improves the compression ratio for typical photographic or rendered image data; the
Predictor tag is omitted (equivalent to 1, none) for `TiffCompression.None` and
`TiffCompression.PackBits`, where it provides no benefit.

**Architectural decision**: the default `compression` is `TiffCompression.None` and the image is
always written as RGBA (never a 3-samples-per-pixel, alpha-free RGB image). There is no
`TiffColorType` parameter in this initial version, matching `BmpCodec`'s and `PngCodec`'s own
precedent of defaulting to the alpha-preserving representation to give callers full round-trip
fidelity with no extra effort.

**Throws:**

- `ArgumentNullException` — `surface` or `stream` is null
- `ArgumentOutOfRangeException` — `compression` is not a defined `TiffCompression` value

#### Save(Surface surface, string path, TiffCompression compression = TiffCompression.None)

Creates (or overwrites) `path` as a `FileStream` and delegates to
`Save(Surface, Stream, TiffCompression)`.

**Throws:**

- `ArgumentNullException` — `surface` or `path` is null
- `ArgumentException` — `path` is an empty string
- `ArgumentOutOfRangeException` — see `Save(Surface, Stream, TiffCompression)`
- Underlying file-system exceptions (`UnauthorizedAccessException`, `DirectoryNotFoundException`,
  `IOException`) propagate uncaught

#### GetInfo(Stream stream)

Reports a TIFF's width, height, samples-per-pixel-derived channel count, and alpha presence
**without buffering or decoding strip data whenever the stream supports seeking** — a
deliberately different strategy from every other `GetInfo` overload in `Codecs`, which read a
short prefix sequentially. This is required because a TIFF's Image File Directory is not
necessarily near the start of the file (its offset is given by the 8-byte header's 4th field, and
a well-formed writer may place it anywhere, including after the strip data it describes); a purely
sequential short-prefix read (as used by `BmpCodec`/`PngCodec`) cannot reliably reach it.

- **`stream.CanSeek == true` (the common case):** `GetInfo` reads only the 8-byte header, seeks
  directly to the declared IFD offset, reads the 2-byte entry count, and then reads each 12-byte
  IFD entry directly from the stream via `ProbeIfdEntriesSeekable`, decoding **only** the four
  tags relevant to `ImageInfo` — `ImageWidth` (256), `ImageLength` (257), `SamplesPerPixel` (277),
  and `ExtraSamples` (338, presence alone signals `HasAlpha`) — and **only** when each tag's value
  fits inline within the 12-byte entry itself (`count == 1` and type BYTE/SHORT/LONG); TIFF entries
  whose value must be dereferenced through a separate offset are deliberately never followed, so
  no strip-data-adjacent or arbitrary-offset reads ever occur. If `SamplesPerPixel` is absent,
  `GetInfo` defaults it to 1 — a simpler default than `Load`'s (which defaults to
  `BitsPerSample`'s entry count); this is an intentional, documented reduced-fidelity trade-off
  for the significant read-cost savings of never dereferencing `BitsPerSample`'s own value/offset
  field. As a minor optimization (not required for correctness), the entry scan may stop early
  once `ImageWidth` and `ImageLength` have both been found and the current tag number reaches or
  exceeds 338 (`ExtraSamples`) — safe because every fixture and every file this codec itself
  writes places IFD entries in ascending tag-number order (the TIFF 6.0-recommended convention),
  but **not** relied upon for correctness against an arbitrary non-conforming file: a
  non-ascending-order IFD would simply be read to its end instead, never producing an incorrect
  result.
- **`stream.CanSeek == false` (fallback):** seeking is impossible, so `GetInfo` instead buffers
  the entire stream into memory (prefixing the 8 header bytes already consumed) and reuses the
  same `ParseIfd` and `ReadTiffImageInfo(..., enforceMaxDimension: false)` helpers `Load` itself
  uses (only strip decoding is skipped) — trading away the "never read strip data" property for
  correctness when seeking is unavailable. Because this path reuses `Load`'s richer
  `SamplesPerPixel`/`Photometric`-aware logic rather than the seekable path's simpler four-tag
  scan, `HasAlpha` is derived via `SamplesPerPixel == 4 && Photometric == RGB` here, which can in
  principle disagree with the seekable path's simpler "`ExtraSamples` tag present" signal on a
  malformed file that presents 4 samples per pixel without an `ExtraSamples` tag; this discrepancy
  between the two paths is a deliberate, documented consequence of reusing two different
  tag-reading strategies, not a defect.

Neither path enforces `Surface.MaxDimension` — an oversized `ImageWidth`/`ImageLength` is returned
as-is in the resulting `ImageInfo`.

**Throws:**

- `ArgumentNullException` — `stream` is null
- `InvalidDataException` — bad byte-order mark or magic number; the stream ends before the header,
  entry count, or a declared IFD entry has been fully read (same contract as `Load`, except the
  `Surface.MaxDimension` check is skipped, and — on the seekable path only — most other
  tag-presence/value validation `Load` performs is also skipped, since only the four tags above
  are ever inspected)

#### GetInfo(string path)

Opens `path` as a read-only `FileStream` and delegates to `GetInfo(Stream)`. Note that a
`FileStream` is always seekable, so opening by path always exercises `GetInfo`'s seek-based path,
never its non-seekable fallback.

**Throws:**

- `ArgumentNullException` — `path` is null
- `ArgumentException` — `path` is an empty string
- `InvalidDataException` — see `GetInfo(Stream)`
- Underlying file-system exceptions propagate uncaught

### Error Handling

All argument validation happens at the start of each public method, before any header or pixel
data is read or written. `Load` performs incremental format validation as each tag is inspected,
failing at the first invalid or missing tag with a message naming the actual invalid value found;
the tiled-TIFF check runs before mandatory-tag validation so that tiled files get a specific error
rather than a misleading "missing StripOffsets" error. There is no local recovery or retry logic
anywhere in `TiffCodec` - every validation failure results in an exception that propagates
directly to the caller. `Save` never mutates the destination stream/file if argument validation
fails, because all argument checks precede any byte write.

### Dependencies

`TiffCodec` depends on `Surface` (constructing surfaces in `Load` and reading/writing rows via
`Surface.GetRowSpanBytes` in `Save`), using only `Surface`'s existing public API exactly as
`BmpCodec`/`PngCodec` do. No new public members were added to `Surface` or `Rgba32` to support this
codec. `TiffCodec` also depends on the `Codecs` subsystem's shared `ImageInfo` record struct as the
return type of `GetInfo` — see _Codecs Subsystem Design_ (`../codecs.md`). Beyond `Surface` and
`ImageInfo`, `TiffCodec` uses only the .NET base class library's `System.IO` namespace (`Stream`,
`FileStream`, `InvalidDataException`) and `System.IO.Compression.DeflateStream` (available on
every one of CanvasNet's target frameworks with no new runtime NuGet dependency); PackBits,
TIFF-flavor LZW, the horizontal-differencing predictor, and the zlib wrapper (2-byte header,
Adler-32 trailer) are all computed by hand-rolled algorithms rather than any third-party library.

### Conformance Testing

In addition to the hand-built positive/negative unit tests above, `TiffCodec` is validated against
the `TiffFixtures` corpus (see `test/DemaConsulting.CanvasNet.Tests/TiffFixtures/README.md`): nine
TIFF files generated with ImageMagick from two files in the PngSuite conformance corpus
(`basn2c08.png`, RGB, and `basn6a08.png`, RGBA), exercising both byte orders and all four
supported compression methods, plus grayscale conversions. Every fixture must load successfully
with the expected (programmatically confirmed) dimensions; the RGB/RGBA fixtures' decoded pixels
must match their PngSuite source exactly (with alpha forced to 255 for the RGB fixtures, since
they have no alpha channel); the grayscale fixtures (a lossy conversion relative to their color
source) are only checked for successful loading, correct dimensions, and R == G == B per pixel.
See `CanvasNet-Codecs-TiffCodec-FixtureSupported` for the corresponding requirement.

### Callers

`TiffCodec` is a public API entry point invoked directly by consumers of the CanvasNet package; it
is not called by any other unit within this system. It calls into `Surface` (see _Dependencies_
above) but nothing calls into it from within CanvasNet itself.
