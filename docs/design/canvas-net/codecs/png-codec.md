## PngCodec

![Codecs Structure](CodecsView.svg)

The `PngCodec` class is the fourth software unit in CanvasNet, and depends on `Surface` exactly as
`BmpCodec` does. It provides hand-rolled saving of a restricted subset of PNG files (8-bit-per-channel
Truecolor or Truecolor-with-alpha, non-interlaced) and hand-rolled loading of every
non-interlaced, spec-valid PNG color type/bit depth combination, to and from `Surface` pixel
buffers.

### Purpose

`PngCodec` lets callers persist a `Surface` as a PNG file (or stream) and load a PNG file (or
stream) back into a `Surface`. It is implemented entirely against the .NET base class library's
`System.IO` and `System.IO.Compression` types, with no third-party PNG or imaging library
dependency.

`Save` supports only 8-bit-per-channel color type 2 (Truecolor/RGB) and color type 6 (Truecolor
with alpha/RGBA), with standard (non-interlaced) scanline order — this is unchanged from earlier
versions of `PngCodec` and remains its only encode capability.

`Load` decodes every non-interlaced color type/bit depth combination the PNG specification
defines: Grayscale (0) at bit depths 1/2/4/8/16, Truecolor (2) and Truecolor-with-alpha (6) at bit
depths 8/16, Palette/indexed (3) at bit depths 1/2/4/8, and Grayscale-with-alpha (4) at bit depths
8/16, including `tRNS`-chunk key-color/per-palette-entry transparency where the specification
defines it (Grayscale, Truecolor, and Palette). Only two things remain outside `Load`'s decode
capability, and both are rejected with a descriptive `System.IO.InvalidDataException` rather than
silently producing incorrect pixels: Adam7-interlaced scanline order (`Load` has no interlacing
reconstruction logic), and any color-type/bit-depth combination the PNG specification itself does
not define (for example color type 3 with bit depth 16). `GetInfo`, by contrast, succeeds and
reports width/height for **every** PNG whose `IHDR` chunk is well-formed, including
Adam7-interlaced files and every bit-depth/color-type combination `Load` accepts — because
probing a file's declared size is a strictly weaker, always-safe operation than decoding its
pixels; see _GetInfo(Stream stream)_ below for the exact design rationale.

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

| Offset | Size | Field                | Value Written (Save)                     |
| ------ | ---- | -------------------- | ---------------------------------------- |
| 0      | 4    | Width                | `surface.Width` (greater than zero)      |
| 4      | 4    | Height               | `surface.Height` (greater than zero)     |
| 8      | 1    | Bit depth            | 8 (only value written)                   |
| 9      | 1    | Color type           | 2 (RGB) or 6 (RGBA)                      |
| 10     | 1    | Compression method   | 0 (zlib/DEFLATE, only value written)     |
| 11     | 1    | Filter method        | 0 (adaptive, only value written)         |
| 12     | 1    | Interlace method     | 0 (none, only value written)             |

| Offset | Field                | Value Accepted (Load)                                                |
| ------ | -------------------- | -------------------------------------------------------------------- |
| 0      | Width                | greater than zero (also &le; `Surface.MaxDimension` when decoding)   |
| 4      | Height               | greater than zero (same rule as Width)                               |
| 8      | Bit depth            | 1, 2, 4, 8, or 16 — only combinations the color type permits (below) |
| 9      | Color type           | 0, 2, 3, 4, or 6 — see _Data Model_ above                            |
| 10     | Compression method   | 0 (only value accepted)                                              |
| 11     | Filter method        | 0 (only value accepted)                                              |
| 12     | Interlace method     | 0 (none) or 1 (Adam7 — well-formed, but `Load` rejects it)           |

**Bit-depth/color-type combination validity** (per the PNG specification; any other pairing is
rejected as malformed by both `Load` and `GetInfo`, since it is invalid regardless of decode
capability):

| Color type           | Valid bit depths        |
| -------------------- | ----------------------- |
| 0 (Grayscale)        | 1, 2, 4, 8, 16          |
| 2 (Truecolor)        | 8, 16                   |
| 3 (Palette)          | 1, 2, 4, 8 (never 16)   |
| 4 (Grayscale+alpha)  | 8, 16                   |
| 6 (Truecolor+alpha)  | 8, 16                   |

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

`bpp` is the number of whole or partial bytes per pixel, rounded up: `max(1, ceil(samplesPerPixel * bitDepth / 8))`,
where `samplesPerPixel` is 1 for Grayscale/Palette, 2 for Grayscale+alpha, 3 for Truecolor, and 4 for Truecolor+alpha.
For every color type/bit-depth combination `Save` writes (RGB/RGBA at bit depth 8), this is 3 or 4 exactly as before;
`Load` computes the same formula generically so the unchanged
`DefilterRow`/`DefilterSub`/`DefilterUp`/`DefilterAverage`/`DefilterPaeth` reconstruction logic works identically for
every bit depth and color type it now decodes, including sub-byte depths where `bpp` is 1 (multiple pixels, or
fractional pixels for depth 1/2/4, share a single filter-reference byte, per the PNG specification). All five filter
types are reconstructed on load. On save, every scanline uses filter type 0 (None) - see _Key
Methods_ below for the rationale.

All multi-byte PNG fields (chunk length, CRC-32, IHDR width/height, Adler-32 trailer) are read
and written by explicit byte composition (bit shifting), never `BitConverter` or
`BinaryPrimitives`, so behavior is identical regardless of host CPU endianness.

#### PLTE and tRNS chunks (Load only; never written by Save)

| Chunk  | Required when                       | Payload shape                                   |
| ------ | ----------------------------------- | ----------------------------------------------- |
| `PLTE` | color type is Palette (3)           | one RGB triple (3 bytes) per palette entry      |
| `tRNS` | optional, color types 0, 2, and 3   | see below                                       |

A `PLTE` chunk is permitted only for color types 2 (Truecolor), 3 (Palette, where it is
mandatory), and 6 (Truecolor-with-alpha, as an optional suggested-palette hint this codec accepts
but ignores for pixel decoding). A `PLTE` chunk on either grayscale color type (0 or 4) is forbidden
by the PNG specification — grayscale samples are sample magnitudes, never palette indices, so
there is nothing for a palette to resolve — and `Load` rejects such a file with
`InvalidDataException` rather than silently storing an unusable chunk.

`tRNS`'s payload shape depends on the color type it appears with:

| Color type       | `tRNS` payload shape                                             |
| ---------------- | ---------------------------------------------------------------- |
| 0 (Grayscale)    | one 2-byte big-endian gray sample (the transparent key value)    |
| 2 (Truecolor)    | three 2-byte big-endian samples (the transparent RGB key)        |
| 3 (Palette)      | up to one alpha byte per palette entry, in index order           |

A `tRNS` chunk on either alpha-carrying color type (4 or 6) is not defined by the PNG
specification — since those color types already carry an explicit per-pixel alpha sample, there is
nothing for a single-key-color transparency chunk to add — and `Load` rejects such a file with
`InvalidDataException` (this codec no longer tolerates it, unlike some permissive decoders that
ignore a defensively-emitted tRNS chunk in this position). A `tRNS` chunk on a Palette
(color-type-3) file must also appear _after_ the `PLTE` chunk, not merely after `IHDR` and before
the first `IDAT` (the ordering `Load` already enforced for every color type): a tRNS chunk's
per-palette-entry alpha values are meaningless before the palette they index into has been read,
so `Load` rejects a Palette file whose `tRNS` chunk precedes its `PLTE` chunk with
`InvalidDataException` naming `PLTE` as the cause. A `PLTE` chunk missing on a Palette-color-type
file is a hard rejection (`InvalidDataException` naming "PLTE"), since there is no way to resolve
a palette index to a color without it. Conversely, `Load` also rejects a `PLTE` chunk that appears
_after_ a `tRNS` chunk has already been accepted, with `InvalidDataException` naming `tRNS` as the
cause — this direction of the ordering requirement applies regardless of color type, not only to
Palette files, since Truecolor and Truecolor-with-alpha files may also legally carry both chunks
(with PLTE as an optional suggested palette) and PLTE must still come first whenever both are
present.

**Design decision — chunk-type codes are validated before classification**: the PNG
specification defines a 4-byte chunk type as four ASCII letters, with each byte's case
independently signaling a property (byte 1: ancillary/critical; byte 2: private/public; byte 3:
reserved, currently always required to be uppercase; byte 4: safe-to-copy). `ReadChunkFrame`
validates a chunk's type bytes - each must be an ASCII letter (`A`-`Z` or `a`-`z`), and the third
byte specifically must be uppercase - before that type is used for anything, including the
critical/ancillary classification described below. Without this check, a malformed type such as
`a!cd` (a non-letter byte) or `aBcd`/`abcd` (a lowercase third byte, violating the reserved-bit
rule) would have reached the classification below and been silently accepted as an ordinary
ancillary chunk purely because its first byte happened to be lowercase; `Load` now rejects any
such malformed chunk type with `InvalidDataException` instead.

**Design decision — unrecognized critical chunks are rejected, not skipped**: the PNG
specification uses a (now type-code-validated, see above) chunk type's first byte's case to mark
it critical (uppercase) or ancillary (lowercase). `Load` recognizes exactly five chunk types
(`IHDR`, `PLTE`, `tRNS`, `IDAT`, `IEND`); any other chunk whose first type byte is uppercase is an
unrecognized _critical_ chunk — one that may change how pixel data must be interpreted — and
`Load` rejects it with `InvalidDataException` naming the chunk type, rather than risk silently
producing incorrect pixels from a chunk it does not understand. An unrecognized _ancillary_ chunk
(lowercase first type byte, for example `tEXt`, `pHYs`, or `gAMA`) remains safe to skip, exactly as
before: its CRC-32 is still validated, but its data is not accumulated anywhere — except that, like
every other chunk type, it must still not appear before the mandatory `IHDR` chunk (see the next
two design decisions).

**Design decision — every chunk, including an otherwise-safe-to-skip ancillary chunk, is rejected
before `IHDR`**: the PNG specification requires `IHDR` to always be the first chunk in the file,
since it supplies the width, height, and color type every later chunk depends on. `Load` already
enforced this for `PLTE`, `tRNS`, `IDAT`, and `IEND` individually; the generic ancillary-chunk-skip
fallback path was the one place this check was missing, so an ancillary chunk (for example `tEXt`)
appearing before `IHDR` was previously skipped unconditionally instead of being rejected. `Load`
now rejects any chunk of any type encountered before `IHDR` with `InvalidDataException` naming
`IHDR` as the cause.

**Design decision — `IDAT` chunks must be consecutive**: the PNG specification requires every
`IDAT` chunk in a file to be consecutive — no other chunk type may appear between the first and
last `IDAT` chunk. `Load` tracks the moment a non-`IDAT` chunk is processed after at least one
`IDAT` chunk has already been seen (the IDAT run has ended); if a further `IDAT` chunk is then
encountered, `Load` rejects it with `InvalidDataException`, since a non-conforming chunk ordering
means the file's chunk boundaries no longer match a conforming encoder's output and silently
concatenating the later `IDAT` chunk's bytes anyway would risk assembling a corrupt decompressed
stream. A payload split across any number of directly consecutive `IDAT` chunks (the common case
for streaming encoders) remains fully supported and unaffected by this check.

### Key Methods

#### Load(Stream stream)

Reads a PNG image from an open stream. Validates the 8-byte PNG signature, then reads chunks
until `IEND` is found: each chunk's CRC-32 is validated regardless of type; `IHDR` is parsed and
validated (bit depth is 1, 2, 4, 8, or 16, and forms one of the combinations the color type
permits; color type is 0, 2, 3, 4, or 6; compression method 0; filter method 0; interlace method
0 or 1 — Adam7 (1) is well-formed but is separately rejected as a decode-capability limitation,
below; width and height are positive and do not exceed `Surface.MaxDimension` (8192) — checked
before any width/height arithmetic, including the row-byte-width computation performed both while
decoding scanlines and by `Load` itself, now generalized to `ceil(width * samplesPerPixel *
bitDepth / 8)` rather than the earlier `width * channels`); `PLTE` and `tRNS` chunks are parsed
when present (see above), including the color-type and chunk-ordering rejections described above;
`IDAT` chunk data is concatenated across as many consecutive chunks as are present (see the
`IDAT`-consecutiveness design decision above); any other recognized ancillary chunk type (for
example `tEXt`, `pHYs`, `gAMA`) is CRC-validated but otherwise skipped, while an unrecognized
critical chunk type is rejected (see above). `IEND`'s declared length must be exactly zero — the
PNG specification defines `IEND` as always carrying an empty payload — checked both before
allocation (a huge declared `IEND` length is rejected by the same pre-allocation callback used for
`PLTE`/`tRNS`/the first chunk) and, redundantly, after the chunk is read.
Once `IEND` is reached, the concatenated `IDAT` payload is unwrapped as a zlib stream (2-byte
header validated, `DeflateStream` inflates the DEFLATE data, the 4-byte Adler-32 trailer is
validated against the decompressed bytes), then each scanline is defiltered (reconstructing all
five standard filter types) and its samples extracted (per-bit-depth unpacking — direct byte copy
at 8-bit, big-endian 16-bit-sample combination, or MSB-first sub-byte unpacking at 1/2/4-bit) and
mapped to an RGBA pixel per the color type's channel layout and any `tRNS` transparency, written
directly into the destination `Surface`'s rows via `Surface.GetRowSpanBytes`.

**Design decision — 16-bit `tRNS` comparison ordering**: for 16-bit Grayscale/Truecolor data, the
raw 16-bit sample is compared against the `tRNS` chunk's 16-bit key value **before** the sample is
downshifted to 8 bits (see _Design Decisions_ below) — comparing after downshifting would produce
false-positive transparency matches for any two distinct 16-bit values that happen to share the
same high byte.

**Design decision — Adam7 remains a decode-capability limitation, not a well-formedness defect**:
an Adam7-interlaced PNG is a completely valid PNG file; `Load` rejects it only because this codec
has no interlaced-scanline reconstruction logic (deinterlacing 7 separate reduced images per the
Adam7 pass pattern), not because the file itself is malformed. This is why `GetInfo` — which never
attempts to decode any pixel data — succeeds and reports correct dimensions for these files even
though `Load` refuses them; see _GetInfo(Stream stream)_ below.

**Throws:**

* `ArgumentNullException` — `stream` is null
* `InvalidDataException` — missing PNG signature; a first chunk whose type is not `IHDR`, or an
  `IHDR` first chunk whose declared length is not exactly 13 (both checked before any
  length-dependent allocation, mirroring `GetInfo`'s equivalent guard — see the pre-allocation
  validation design decision above); missing (past the first chunk), duplicate, or malformed
  `IHDR`; any
  chunk (including an otherwise-safe-to-skip ancillary chunk) encountered before `IHDR`; a
  malformed chunk type code (a byte that is not an ASCII letter, or a lowercase third byte
  violating the reserved-bit rule); a bit
  depth other than 1, 2, 4, 8, or 16; a
  color type other than 0, 2, 3, 4, or 6; a bit-depth/color-type combination the PNG
  specification does not define (for example color type 3 with bit depth 16); an unsupported
  compression method, filter method, or interlace method value; Adam7 interlacing (well-formed,
  but not a combination `Load` can decode); an unrecognized critical chunk (uppercase first type
  byte); a `PLTE` or `tRNS` chunk whose declared length exceeds that type's maximum before its
  payload is even allocated (see the pre-allocation validation design decision above); a `PLTE`
  chunk on a grayscale or grayscale-with-alpha file; a `PLTE` chunk appearing
  after a `tRNS` chunk has already been accepted; a `tRNS` chunk on a
  grayscale-with-alpha or Truecolor-with-alpha file, or one that precedes the `PLTE` chunk on a
  Palette file; a color-type-3 (Palette) file missing its `PLTE`
  chunk, or containing a pixel whose palette index is out of range; a malformed `tRNS` chunk
  length for its color type; a non-empty `IEND` payload (checked both before allocation and after
  the chunk is read); non-positive width or height, or width/height exceeding
  `Surface.MaxDimension`; non-consecutive `IDAT` chunks; any chunk's CRC-32 mismatch; a malformed
  or unsupported zlib header; an
  Adler-32 checksum mismatch; an unexpected decompressed data length; an unsupported scanline
  filter type; or the stream ends before all header, chunk, or pixel data has been read

#### Load(string path)

Opens `path` as a read-only `FileStream` and delegates to `Load(Stream)`.

**Throws:**

* `ArgumentNullException` — `path` is null
* `ArgumentException` — `path` is an empty string
* `InvalidDataException` — see `Load(Stream)`
* Underlying file-system exceptions (`FileNotFoundException`, `DirectoryNotFoundException`,
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

* `ArgumentNullException` — `surface` or `stream` is null
* `ArgumentOutOfRangeException` — `colorType` is not a defined `PngColorType` value

#### Save(Surface surface, string path, PngColorType colorType = PngColorType.Rgba)

Creates (or overwrites) `path` as a `FileStream` and delegates to `Save(Surface, Stream, PngColorType)`.

**Throws:**

* `ArgumentNullException` — `surface` or `path` is null
* `ArgumentException` — `path` is an empty string
* `ArgumentOutOfRangeException` — see `Save(Surface, Stream, PngColorType)`
* Underlying file-system exceptions (`UnauthorizedAccessException`, `DirectoryNotFoundException`,
  `IOException`) propagate uncaught

#### GetInfo(Stream stream)

Reads only the 8-byte PNG signature and the first (`IHDR`) chunk — never any subsequent chunk,
and in particular never any `IDAT` chunk — and returns an `ImageInfo` describing the file. `Load`
and `GetInfo` share a `ReadIhdrOnly(Stream, bool enforceMaxDimension, bool validateDecodability)`
helper, but the two use distinct chunk-frame readers: `Load` reuses the general `ReadChunkFrame`
helper, while `ReadIhdrOnly` calls a dedicated `ReadIhdrChunkFrame` helper that validates the chunk
type is `IHDR` **and** that the declared length is exactly 13 _before_ allocating or reading any
data payload at all. This ordering matters specifically for `GetInfo`'s "cheap probe of untrusted
input" purpose: without it, a crafted non-`IHDR` (or wrong-length `IHDR`) first chunk declaring an
attacker-controlled multi-gigabyte length could force a huge allocation on `GetInfo`'s fast path
before the type/length mismatch was ever discovered.

**Design decision — pre-allocation validation in the general chunk-frame reader**:
`ReadChunkFrame` validates every chunk's 4-byte type code (see the chunk-type-code validation
design decision below) and, before any length-dependent data payload is allocated or read: for
the very first chunk in the file, that its type is `IHDR` and its declared length is exactly 13
(mirroring `ReadIhdrChunkFrame`'s identical guard on `GetInfo`'s path, since `Load`'s `ReadChunks`
reads its first chunk through this same general-purpose reader rather than through
`ReadIhdrChunkFrame`); for `PLTE` and `tRNS` specifically, the declared length against that
type's largest legitimate size; for `IEND`, that the declared length is exactly zero; for `IDAT`,
that the run of consecutive `IDAT` chunks has not already ended; and for an unrecognized critical
chunk type (uppercase first type byte, per the PNG naming convention, and not one of the five
chunks this codec explicitly recognizes) encountered once `IHDR` has been parsed, that it is
rejected outright. A crafted first chunk, `PLTE`, `tRNS`, `IEND`, non-consecutive `IDAT`, or
unrecognized critical chunk can therefore never force a large allocation by declaring a huge (but
still sub-`int.MaxValue`) length: a non-`IHDR` first chunk, or an `IHDR`
first chunk whose declared length is not exactly 13, is rejected before
any allocation; `PLTE`'s declared length is rejected once it exceeds 768 bytes (256 three-byte
entries, the largest a spec-valid `PLTE` chunk can ever be, regardless of color type or bit depth);
`tRNS`'s declared length is rejected once it exceeds the color type's exact size (2 bytes for
Grayscale, 6 for Truecolor) once `IHDR` has been parsed, or the 256-byte palette-entry ceiling
otherwise; `IEND`'s declared length is rejected the moment it is non-zero, since the PNG
specification defines `IEND` as always carrying an empty payload; a further `IDAT` chunk is
rejected the moment the `IDAT` run has already ended, regardless of its declared length, since the
PNG specification requires every `IDAT` chunk to be consecutive; and an unrecognized critical
chunk type is rejected regardless of its declared length, since such a chunk is always refused
outright once `IHDR` has been parsed. This pre-allocation check is
deliberately loose - it exists only to close the
memory-exhaustion vector, not to duplicate the exact per-color-type/per-bit-depth correctness
checks that still run afterward on the (now safely small) allocated payload, in `ProcessChunk`,
`ParseIhdr`, and `ValidateAndNormalizeTrns`; the post-read `IDAT`-consecutiveness and
unrecognized-critical-chunk checks in `ProcessChunk` remain in place as defense-in-depth, exactly
like the other checks this pre-allocation guard duplicates, even though they become unreachable on
the success path once this guard is in place. Every other chunk type past the first (a
still-in-progress `IDAT` run legitimately carries large payloads; any other recognized or
unrecognized-ancillary chunk type has no small type-specific maximum to check) is unaffected and
is still fully allocated and read before its type is otherwise interpreted, since `Load` always
intends to read every chunk's data anyway.

**Design decision — two independent validation flags**: `enforceMaxDimension` and
`validateDecodability` gate two orthogonal concerns, and `GetInfo` passes `false` for both while
`Load` passes `true` for both:

* `enforceMaxDimension` gates only the `Surface.MaxDimension` check. `GetInfo` deliberately skips
  it so an oversized declared width or height is returned as-is rather than throwing, letting a
  caller triage a suspiciously large (or decompression-bomb-suspect) file by its declared size
  before deciding whether to call `Load` at all.
* `validateDecodability` gates only the Adam7-interlacing rejection. Every other `IHDR` field
  check (bit depth range, color type range, bit-depth/color-type combination legality,
  compression/filter method, interlace method being 0 or 1) is a well-formedness check enforced
  unconditionally by `ReadIhdrOnly`/`ParseIhdr`, regardless of either flag's value — because
  `Load`'s decodable color-type/bit-depth space is now the PNG specification's _entire_ legal
  space, the only decode-capability gap left for `GetInfo` to bypass is Adam7 interlacing.

This means `GetInfo` succeeds — reporting the file's true declared width and height — for every
PNG whose `IHDR` chunk is well-formed: any of the five color types, any bit depth that color type
permits, interlaced or not. A file with a malformed `IHDR` (bad signature, wrong chunk length,
non-positive dimensions, a bad CRC-32, or a bit-depth/color-type combination the PNG
specification itself does not define) is still rejected by `GetInfo`, since well-formedness — not
decodability — is the boundary `GetInfo` enforces.

**Design decision — `Channels`/`HasAlpha` reflect the raw file encoding, not `Load`'s decoded
output**: `GetInfo` maps `IHDR`'s color type directly to `(Channels, HasAlpha)` without any
knowledge of `PLTE`/`tRNS` (which it never reads): Grayscale (0) → `(1, false)`; Truecolor (2) →
`(3, false)`; **Palette (3) → `(1, false)`** — one palette-index sample per pixel in the file
(packed at sub-byte bit depths, not always one byte per pixel), deliberately
_not_ the four-channel RGBA result `Load` would produce after resolving each index through
`PLTE`/`tRNS`; Grayscale+alpha (4) → `(2, true)`; Truecolor+alpha (6) → `(4, true)`. This
asymmetry between `GetInfo`'s and `Load`'s notion of "channels" for Palette files is intentional:
`GetInfo` describes what is present in the file's bytes, while `Load` always produces a
fully-resolved 4-channel `Surface` regardless of source color type.

**Throws:**

* `ArgumentNullException` — `stream` is null
* `InvalidDataException` — missing PNG signature; a first chunk whose type is not `IHDR`
  (checked before any length-dependent allocation); an `IHDR` chunk whose declared length is not
  exactly 13 (also checked before any length-dependent allocation); a bad `IHDR` CRC-32; a bit
  depth other than 1, 2, 4, 8, or 16; a color type other than 0, 2, 3, 4, or 6; a
  bit-depth/color-type combination the PNG specification does not define; an unsupported
  compression method, filter method, or interlace-method value outside {0, 1}; non-positive width
  or height; the stream ends before the signature and `IHDR` chunk have been fully read (same
  contract as `Load`, except the `Surface.MaxDimension` check is skipped and Adam7 interlacing is
  not rejected)

#### GetInfo(string path)

Opens `path` as a read-only `FileStream` and delegates to `GetInfo(Stream)`.

**Throws:**

* `ArgumentNullException` — `path` is null
* `ArgumentException` — `path` is an empty string
* `InvalidDataException` — see `GetInfo(Stream)`
* Underlying file-system exceptions propagate uncaught

### Design Decisions

**Sub-byte sample scaling**: for Grayscale bit depths 1, 2, and 4, each sample is scaled to the
full 0-255 range as `sample * 255 / ((1 << bitDepth) - 1)` (for example a 4-bit sample of 15
becomes `15 * 255 / 15 = 255`, and a 4-bit sample of 8 becomes `8 * 255 / 15 = 136`). This is
numerically identical to bit-replication (repeating the sample's bit pattern to fill 8 bits, the
more commonly described technique) for every value at these bit depths; the multiply/divide
formula was chosen for implementation uniformity with the bit-depth-16 downshift case, rather than
writing a separate bit-replication code path. Palette (color type 3) indices are never scaled —
an index selects a palette entry, it does not represent a sample magnitude.

**MSB-first sub-byte bit unpacking**: for bit depths 1, 2, and 4 (only reachable for Grayscale and
Palette, the only color types the PNG specification allows at sub-byte depths), each pixel's
sample occupies `bitDepth` bits within its row, packed most-significant-bit-first starting from
each byte's high bit, with the final byte of a row zero-padded if `width * bitDepth` is not a
multiple of 8. This padding is discarded, never written to any pixel.

### Error Handling

All argument validation happens at the start of each public method, before any header or pixel
data is read or written. `Load` performs incremental format validation as each chunk is read,
failing at the first invalid chunk, header field, checksum, or filter type with a message naming
the actual invalid value found. There is no local recovery or retry logic anywhere in `PngCodec`

* every validation failure results in an exception that propagates directly to the caller. `Save`
never mutates the destination stream/file if an argument validation fails, because all argument
checks precede any byte write.

### Dependencies

`PngCodec` depends on `Surface` (constructing surfaces in `Load` and reading/writing rows via
`Surface.GetRowSpanBytes` in `Save`), using only `Surface`'s existing public API exactly as
`BmpCodec` does. No new public members were added to `Surface` or `Rgba32` to support this codec.
`PngCodec` also depends on the `Codecs` subsystem's shared `ImageInfo` record struct as the
return type of `GetInfo` — see _Codecs Subsystem Design_ (`../codecs.md`). Beyond `Surface` and
`ImageInfo`, `PngCodec` uses only the .NET base class library's `System.IO` namespace (`Stream`,
`FileStream`, `InvalidDataException`) and `System.IO.Compression.DeflateStream` (available on
every one of CanvasNet's target frameworks with no new runtime NuGet dependency); the zlib
wrapper (2-byte header, Adler-32 trailer) and every PNG chunk's CRC-32 are computed by hand-rolled
algorithms rather than any third-party library.

### Conformance Testing

In addition to the hand-built positive/negative unit tests above, `PngCodec` is validated against
the industry-standard [PngSuite](http://www.schaik.com/pngsuite/) conformance corpus (Willem van
Schaik, 1996-2011; freeware, redistributed under `PngSuite.LICENSE`). Each of the corpus's 175
test files' actual IHDR fields were verified directly against its raw bytes (not trusted from its
filename) and classified into exactly one of four groups: 126 well-formed, non-interlaced files
covering every color type and bit depth `Load` supports (must load successfully), 35
Adam7-interlaced files (must be rejected by `Load` with `InvalidDataException`, but must still
succeed and report correct dimensions via `GetInfo`), 12 files deliberately corrupt at or before
their IHDR chunk (must be rejected by both `Load` and `GetInfo` with `InvalidDataException`), and
2 files deliberately corrupt only after a well-formed IHDR chunk — a corrupt IDAT CRC-32, and a
missing IDAT chunk (must be rejected by `Load` with `InvalidDataException`, but must still succeed
and report correct dimensions via `GetInfo`, exactly like the Adam7-interlaced files, since
`GetInfo` never reads past `IHDR`). See `CanvasNet-Codecs-PngCodec-PngSuiteSupported`,
`CanvasNet-Codecs-PngCodec-PngSuiteUnsupported`, `CanvasNet-Codecs-PngCodec-PngSuiteCorrupt`, and
`CanvasNet-Codecs-PngCodec-PngSuiteCorruptAfterIhdr` for the corresponding requirements.

### Callers

`PngCodec` is a public API entry point invoked directly by consumers of the CanvasNet package; it
is not called by any other unit within this system. It calls into `Surface` (see _Dependencies_
above) but nothing calls into it from within CanvasNet itself.
