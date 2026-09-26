## Codecs

![Codecs Structure](CodecsView.svg)

<!-- cspell:ignore rasterizing unparseable Linq -->

The `Codecs` subsystem is the second software subsystem in CanvasNet. It groups six flat,
hand-rolled image-format codecs — `BmpCodec`, `PngCodec`, `TiffCodec`, `JpegCodec`, `GifCodec`,
and `SvgCodec`. Four raster codecs (`BmpCodec`, `PngCodec`, `TiffCodec`, and `JpegCodec`) each
convert to and from a `DemaConsulting.CanvasNet.Canvas.Surface` pixel buffer; `GifCodec` is
decode-only — it loads a `Surface` from the first frame of a GIF file but has no `Save` method;
`SvgCodec` only decodes/rasterizes SVG vector artwork into a `Surface` — it has no encode/save
direction either.

### Purpose

The `Codecs` subsystem groups the software units responsible for reading and writing pixel data
in standard image file formats, and, for `SvgCodec`, rasterizing vector artwork into pixel data.
It is flat: none of its six units depend on one another. `BmpCodec`, `PngCodec`, `TiffCodec`,
`JpegCodec`, and `GifCodec` depend only on the `Canvas` subsystem's `Surface` unit for their
in-memory pixel representation; `SvgCodec` additionally depends on the `Geometry`, `Drawing`, and
`Fonts` subsystems to build and rasterize vector paths and text — see _SvgCodec Unit Design_
(`codecs/svg-codec.md`).

### Units

- **BmpCodec** — hand-rolled loader/saver for uncompressed 24-bit and 32-bit Windows BMP files;
  see _BmpCodec Unit Design_ (`codecs/bmp-codec.md`)
- **PngCodec** — hand-rolled saver for 8-bit-per-channel Truecolor and Truecolor-with-alpha,
  non-interlaced PNG files, and hand-rolled loader for every non-interlaced, spec-valid PNG
  color type/bit depth combination; see _PngCodec Unit Design_ (`codecs/png-codec.md`)
- **TiffCodec** — hand-rolled loader/saver for 8-bit-per-sample RGB, RGBA, and Grayscale,
  strip-based TIFF 6.0 files; see _TiffCodec Unit Design_ (`codecs/tiff-codec.md`)
- **JpegCodec** — hand-rolled loader/saver for a common real-world subset of JPEG files; see
  _JpegCodec Unit Design_ (`codecs/jpeg-codec.md`)
- **GifCodec** — hand-rolled, decode-only loader for a common real-world subset of GIF files
  (first frame only); `GetInfo` additionally reports the file's true total frame count; see
  _GifCodec Unit Design_ (`codecs/gif-codec.md`)
- **SvgCodec** — decode/rasterize-only loader for a common real-world subset of SVG documents; see
  _SvgCodec Unit Design_ (`codecs/svg-codec.md`)

### Shared Types

#### ImageInfo

`ImageInfo` is a `public readonly record struct` shared by all six codecs' `GetInfo` methods:

```csharp
public readonly record struct ImageInfo(int Width, int Height, int Channels, bool HasAlpha)
{
    public bool CanDecode { get; init; } = true;
    public int FrameCount { get; init; } = 1;
}
```

It reports a candidate image's declared width, height, channel count, and alpha presence without
requiring the caller to decode (or even fully read) the file. It is the return type of every
`{Codec}.GetInfo(Stream)` / `{Codec}.GetInfo(string)` method across `BmpCodec`, `PngCodec`,
`TiffCodec`, `JpegCodec`, `GifCodec`, and `SvgCodec`. `CanDecode` and `FrameCount` are both
declared as `init`-only properties outside the primary constructor (rather than positional
parameters) specifically to avoid changing the compiler-emitted constructor/`Deconstruct`
signature — a binary-compatibility concern, since an additional positional parameter would break
any pre-compiled caller's IL even though source would still compile unchanged. `CanDecode`
defaults to `true` and is set to `false` by `PngCodec.GetInfo` when the probed file is well-formed
per the PNG specification but declares Adam7 interlacing (the one well-formed-but-unsupported
case), and by `GifCodec.GetInfo` when the first Image Descriptor's compressed data fails the same
LZW decode `Load` itself performs for that frame (a well-formed container with an undecodable
payload) — see below and _GifCodec Unit Design_ (`codecs/gif-codec.md`) for the exact rationale.
`FrameCount` defaults to `1` and is overridden only by `GifCodec.GetInfo`, the only codec whose
file format can legitimately declare more than one frame (an animation); it reports the file's
true total Image Descriptor count by walking the file's block structure, never resolving any
frame's decoded pixels into a `Surface` — see _GifCodec Unit Design_ (`codecs/gif-codec.md`) for
the exact walk. Because `ImageInfo` is a record struct, `CanDecode` and `FrameCount` both
participate in its generated value equality like every other member.

### Header-Only Probing (`GetInfo`)

Each of the six codecs, in addition to its existing `Load` method (and, for four of the five
raster codecs — all but the decode-only `GifCodec` — `Save`), exposes a pair of `GetInfo`
overloads:

```csharp
public static ImageInfo GetInfo(Stream stream);
public static ImageInfo GetInfo(string path);
```

`GetInfo` exists so callers can triage an untrusted or unknown-source image (for example, before
allocating a `Surface`) by inspecting only its declared dimensions and channel layout, comparing
them against `Surface.MaxDimension` (now public — see _Surface Unit Design_,
`canvas/surface.md`) and rejecting suspiciously large images, without paying the cost of decoding
pixel data that will only be thrown away. Deliberately, **`GetInfo` never enforces
`Surface.MaxDimension` itself** — it always reports the raw header-declared (or, for `SvgCodec`,
document-resolved) dimensions, even when they exceed the maximum a `Surface` can hold, so that
callers can make exactly this before-you-allocate decision themselves; `Load` on the same bytes
still enforces the limit as before, via `Surface`'s own constructor. `GifCodec` is the one
exception to the "without paying the cost of decoding pixel data" claim above: its `GetInfo`
scans the entire file's block structure and, bounded conditions permitting, also LZW-decodes the
first frame's compressed pixel data solely to validate `CanDecode` — see this section's `GifCodec`
paragraph below for the exact scope and the bound that keeps this attempt from ever costing more
than a well-formed, `Surface.MaxDimension`-sized frame would.

Each of `BmpCodec`, `PngCodec`, `TiffCodec`, and `JpegCodec` shares a single internal
header-parsing helper between `Load` and `GetInfo` (a `bool enforceMaxDimension` parameter selects
whether the `Surface.MaxDimension` check is applied), so `GetInfo` can never drift out of sync
with `Load`'s understanding of a well-formed header. `PngCodec` additionally threads a second,
orthogonal `bool validateDecodability` parameter through the same shared helper, gating only its
Adam7-interlacing rejection (every other header-validity check is unconditional, since `Load`'s
decodable color-type/bit-depth space now spans the PNG specification's entire legal space) — see
_PngCodec Unit Design_ (`codecs/png-codec.md`) for the exact rationale; the other three raster
codecs still use only the single `enforceMaxDimension` flag, since none of them has a
feature-based `Load` refusal that is independent of header well-formedness. `GifCodec` shares its
per-frame structural-validation helpers (region bounds, minimum code size range, color table
resolution) between `Load` and `GetInfo`'s frame-counting walk, but with no boolean flag at all:
unlike the other four raster codecs, `GifCodec.GetInfo` never enforces `Surface.MaxDimension`
under any circumstance; every frame's own per-frame bounds-check arithmetic is bounded, and its
budget-capped sub-block buffer (see `MaxTotalSubBlockBytes`) is its only allocation proportional
to file size for every frame after the first — see _GifCodec Unit Design_ (`codecs/gif-codec.md`)
for the exact rationale, including how `GetInfo` never invokes the LZW decoder for any frame after
the first, but does attempt an LZW decode of the first frame's compressed data (reusing `Load`'s
own decoder, discarding its decoded output) purely to determine `CanDecode`, so a first-frame
compressed-data corruption that makes `Load` throw is reflected as `CanDecode == false` rather
than making `GetInfo` throw. Because the first frame's declared width/height is not itself bounded
by `Surface.MaxDimension` here, this first-frame decode attempt _does_ allocate an index buffer
proportional to that declared size — but only when their product (computed with widened
arithmetic to avoid overflow) does not exceed `Surface.MaxDimension` squared; a pathologically
large declared first frame instead skips this validation attempt entirely, leaving `CanDecode` at
its default of `true` rather than risking an overflow or an unbounded allocation. `SvgCodec`
does not
use this pattern, because
its `Load` overloads take the requested output raster's width/height as ordinary caller-supplied
parameters (not values decoded from the file) and delegate them directly to `Surface`'s own
constructor — see _SvgCodec Unit Design_ (`codecs/svg-codec.md`) for its `GetInfo` fallback
policy. Every `GetInfo` overload uses the same exception contract as the corresponding `Load`
overload (`ArgumentNullException` for a null stream/path, `ArgumentException` for an empty path,
`InvalidDataException` for malformed/unparseable source data) — see each codec's own unit design
document for the exact header-parsing strategy and any format-specific nuance (in particular
`TiffCodec.GetInfo(Stream)`'s seek-based fast path with a buffering fallback for a non-seekable
stream, and `JpegCodec`'s incremental marker scan continuing past its soft cap segment-by-segment
rather than giving up) - see the
_ImageInfo_ section above for the cross-codec invariant these fallbacks exist to uphold: GetInfo
never throws for an input Load would successfully decode.

**Well-formed but unsupported/undecodable: `UnsupportedImageFeatureException` and
`ImageInfo.CanDecode == false`.** Investigation across all five raster codecs found exactly one
case where a codec's `Load` refuses a file that is well-formed per its own format specification —
PNG's Adam7 interlacing (the other codecs conflate "unsupported" and "malformed" at `GetInfo`-time
already, so this exception type is not currently thrown by them). `GifCodec` is a related, but
distinct, second case: a multi-frame GIF is well-formed and `Load` never refuses it — it decodes
only the first frame, by design, so a well-formed multi-frame GIF is not, by itself, a
`CanDecode == false` case. However, `GifCodec.GetInfo` does report `CanDecode == false` when the
first frame's compressed data is corrupt enough that `Load`'s LZW decoder would reject it — a
well-formed container with an undecodable payload, detected by `GetInfo` attempting (and
discarding the result of) that same first-frame LZW decode — see _GifCodec Unit Design_,
`codecs/gif-codec.md`, for the full rationale.
`PngCodec.Load` signals its specific case with `UnsupportedImageFeatureException`
rather than `InvalidDataException`, so a caller can distinguish "well-formed but unsupported" from
"malformed" without string-matching `Exception.Message`. This type derives from `IOException`
rather than `InvalidDataException`, because `System.IO.InvalidDataException` is `sealed` in .NET.
Note that `InvalidDataException` itself derives directly from `SystemException`, not
`IOException` — `IOException` is **not** a common base a caller can catch to handle both
"malformed" (`InvalidDataException`) and "well-formed but unsupported"
(`UnsupportedImageFeatureException`) cases together. This is a deliberate, narrow, documented
behavior change (an existing `catch (InvalidDataException)`
around `PngCodec.Load` no longer catches the Adam7 case); a caller that wants to handle both
cases must add two explicit catch clauses, one per type. `ImageInfo.CanDecode` (see
above) lets a caller detect this case from `GetInfo` before ever calling `Load` at all — see
_PngCodec Unit Design_ (`codecs/png-codec.md`) for the exact throw site and `CanDecode`
computation.

The `Codecs` subsystem depends on the `Canvas` subsystem's `Surface` unit (constructing surfaces
when loading and reading/writing rows via `Surface.GetRowSpanBytes` when saving) — see _Canvas
Subsystem Design_ (`canvas.md`). `SvgCodec` additionally depends on the `Geometry` subsystem (path
construction and arc-to-Bezier conversion), the `Drawing` subsystem (path filling/stroking and
gradient paint resolution), and the `Fonts` subsystem (glyph outline/metrics lookup for text
rendering) — see _Geometry Subsystem Design_ (`../geometry.md`), _Drawing Subsystem Design_
(`../drawing.md`), and _Fonts Subsystem Design_ (`../fonts.md`). Beyond these, the `Codecs`
subsystem's units use only the .NET base class library (`System.IO`,
`System.IO.Compression.DeflateStream`, `System.Xml.Linq` for `SvgCodec`, and
`System.Numerics.Vector<T>` for optional SIMD acceleration in `JpegCodec`), available on every one
of CanvasNet's target frameworks with no new runtime NuGet dependency.

### Callers

Each unit of the `Codecs` subsystem is a public API entry point, invoked directly by consumers of
the CanvasNet package. No unit within the `Codecs` subsystem is called by any other subsystem, and
no unit within the `Codecs` subsystem calls into any other unit of the `Codecs` subsystem.
