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

### Dependencies

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
