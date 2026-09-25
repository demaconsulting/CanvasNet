## Codecs

![Codecs Structure](CodecsView.svg)

<!-- cspell:ignore rasterizing unparseable Linq -->

The `Codecs` subsystem is the second software subsystem in CanvasNet. It groups five flat,
hand-rolled image-format codecs — `BmpCodec`, `PngCodec`, `TiffCodec`, `JpegCodec`, and
`SvgCodec`. The four raster codecs (`BmpCodec`, `PngCodec`, `TiffCodec`, and `JpegCodec`) each
convert to and from a `DemaConsulting.CanvasNet.Canvas.Surface` pixel buffer; `SvgCodec` only
decodes/rasterizes SVG vector artwork into a `Surface` — it has no encode/save direction.

### Purpose

The `Codecs` subsystem groups the software units responsible for reading and writing pixel data
in standard image file formats, and, for `SvgCodec`, rasterizing vector artwork into pixel data.
It is flat: none of its five units depend on one another. `BmpCodec`, `PngCodec`, `TiffCodec`,
and `JpegCodec` depend only on the `Canvas` subsystem's `Surface` unit for their in-memory pixel
representation; `SvgCodec` additionally depends on the `Geometry`, `Drawing`, and `Fonts`
subsystems to build and rasterize vector paths and text — see _SvgCodec Unit Design_
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
- **SvgCodec** — decode/rasterize-only loader for a common real-world subset of SVG documents; see
  _SvgCodec Unit Design_ (`codecs/svg-codec.md`)

### Shared Types

#### ImageInfo

`ImageInfo` is a `public readonly record struct` shared by all five codecs' `GetInfo` methods:

```csharp
public readonly record struct ImageInfo(int Width, int Height, int Channels, bool HasAlpha);
```

It reports a candidate image's declared width, height, channel count, and alpha presence without
requiring the caller to decode (or even fully read) the file. It is the return type of every
`{Codec}.GetInfo(Stream)` / `{Codec}.GetInfo(string)` method across `BmpCodec`, `PngCodec`,
`TiffCodec`, `JpegCodec`, and `SvgCodec`.

### Header-Only Probing (`GetInfo`)

Each of the five codecs, in addition to its existing `Load` method (and, for the four raster
codecs, `Save`), exposes a pair of `GetInfo` overloads:

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
still enforces the limit as before, via `Surface`'s own constructor.

Each of `BmpCodec`, `PngCodec`, `TiffCodec`, and `JpegCodec` shares a single internal
header-parsing helper between `Load` and `GetInfo` (a `bool enforceMaxDimension` parameter selects
whether the `Surface.MaxDimension` check is applied), so `GetInfo` can never drift out of sync
with `Load`'s understanding of a well-formed header. `PngCodec` additionally threads a second,
orthogonal `bool validateDecodability` parameter through the same shared helper, gating only its
Adam7-interlacing rejection (every other header-validity check is unconditional, since `Load`'s
decodable color-type/bit-depth space now spans the PNG specification's entire legal space) — see
_PngCodec Unit Design_ (`codecs/png-codec.md`) for the exact rationale; the other three raster
codecs still use only the single `enforceMaxDimension` flag, since none of them has a
feature-based `Load` refusal that is independent of header well-formedness. `SvgCodec` does not
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
