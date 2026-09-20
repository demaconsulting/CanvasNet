## Codecs

![Codecs Structure](CodecsView.svg)

The `Codecs` subsystem is the second software subsystem in CanvasNet. It groups the four flat,
hand-rolled image-format codecs — `BmpCodec`, `PngCodec`, `TiffCodec`, and `JpegCodec` — each of
which converts to and from a `CanvasNet.Canvas.Surface` pixel buffer.

### Purpose

The `Codecs` subsystem groups the software units responsible for reading and writing pixel data
in standard image file formats. It is flat: none of its four units depend on one another, and
each depends only on the `Canvas` subsystem's `Surface` unit for its in-memory pixel
representation.

### Units

- **BmpCodec** — hand-rolled loader/saver for uncompressed 24-bit and 32-bit Windows BMP files;
  see _BmpCodec Unit Design_ (`codecs/bmp-codec.md`)
- **PngCodec** — hand-rolled loader/saver for 8-bit-per-channel Truecolor and
  Truecolor-with-alpha, non-interlaced PNG files; see _PngCodec Unit Design_
  (`codecs/png-codec.md`)
- **TiffCodec** — hand-rolled loader/saver for 8-bit-per-sample RGB, RGBA, and Grayscale,
  strip-based TIFF 6.0 files; see _TiffCodec Unit Design_ (`codecs/tiff-codec.md`)
- **JpegCodec** — hand-rolled loader/saver for a common real-world subset of JPEG files; see
  _JpegCodec Unit Design_ (`codecs/jpeg-codec.md`)

### Shared Types

#### ImageInfo

`ImageInfo` is a `public readonly record struct` shared by all four codecs' `GetInfo` methods:

```csharp
public readonly record struct ImageInfo(int Width, int Height, int Channels, bool HasAlpha);
```

It reports a candidate image's declared width, height, channel count, and alpha presence without
requiring the caller to decode (or even fully read) the file. It is the return type of every
`{Codec}.GetInfo(Stream)` / `{Codec}.GetInfo(string)` method across `BmpCodec`, `PngCodec`,
`TiffCodec`, and `JpegCodec`.

### Header-Only Probing (`GetInfo`)

Each of the four codecs, in addition to its existing `Load`/`Save` methods, exposes a pair of
`GetInfo` overloads:

```csharp
public static ImageInfo GetInfo(Stream stream);
public static ImageInfo GetInfo(string path);
```

`GetInfo` exists so callers can triage an untrusted or unknown-source image (for example, before
allocating a `Surface`) by inspecting only its declared dimensions and channel layout, comparing
them against `Surface.MaxDimension` (now public — see _Surface Unit Design_,
`canvas/surface.md`) and rejecting suspiciously large images, without paying the cost of decoding
pixel data that will only be thrown away. Deliberately, **`GetInfo` never enforces
`Surface.MaxDimension` itself** — it always reports the raw header-declared dimensions, even when
they exceed the maximum a `Surface` can hold, so that callers can make exactly this
before-you-allocate decision themselves; `Load` on the same bytes still enforces the limit as
before.

Each codec shares a single internal header-parsing helper between `Load` and `GetInfo` (a
`bool enforceMaxDimension` parameter selects whether the `Surface.MaxDimension` check is applied),
so `GetInfo` can never drift out of sync with `Load`'s understanding of a well-formed header. Every
`GetInfo` overload uses the same exception contract as the corresponding `Load` overload
(`ArgumentNullException` for a null stream/path, `ArgumentException` for an empty path,
`InvalidDataException` for a malformed or truncated header) — see each codec's own unit design
document for the exact header-parsing strategy and any format-specific nuance (in particular
`TiffCodec.GetInfo(Stream)`'s seek-only strategy, which additionally throws
`NotSupportedException` for a non-seekable stream rather than attempting to buffer it, and
`JpegCodec`'s bounded marker scan).

The `Codecs` subsystem depends on the `Canvas` subsystem's `Surface` unit (constructing surfaces
when loading and reading/writing rows via `Surface.GetRowSpanBytes` when saving) — see _Canvas
Subsystem Design_ (`canvas.md`). Beyond `Canvas`, the `Codecs` subsystem's units use only the .NET
base class library (`System.IO`, `System.IO.Compression.DeflateStream`, and
`System.Numerics.Vector<T>` for optional SIMD acceleration in `JpegCodec`), available on every one
of CanvasNet's target frameworks with no new runtime NuGet dependency.

### Callers

Each unit of the `Codecs` subsystem is a public API entry point, invoked directly by consumers of
the CanvasNet package. No unit within the `Codecs` subsystem is called by any other subsystem, and
no unit within the `Codecs` subsystem calls into any other unit of the `Codecs` subsystem.
