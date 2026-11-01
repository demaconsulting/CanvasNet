## Canvas

![Canvas Structure](CanvasView.svg)

The `Canvas` subsystem is the first software subsystem in CanvasNet. It provides the pixel-buffer
primitives on which all other CanvasNet functionality builds: a mutable, in-memory 32-bit RGBA
pixel buffer (`Surface`), the single-pixel value type it stores (`Rgba32`), and the shared CSS color
parser (`CssColor`) that turns color text into `Rgba32` values.

### Purpose

The `Canvas` subsystem groups the software units responsible for representing and manipulating
raw pixel data, independent of any file format or drawing operation. It has no dependency on any
other subsystem: the `Codecs` subsystem depends on `Canvas`, not the other way around.

### Units

- **Surface** — mutable, in-memory 32-bit RGBA pixel buffer with allocation-free row access,
  independent-copy cropping and resampling (`Resize`/`ResizeToFit` with the `ResamplingMode`
  filters, backed by the internal separable `SurfaceResampler` engine); see _Surface Unit Design_ (`canvas/surface.md`)
- **Rgba32** — a blittable four-byte value type representing a single RGBA pixel; its channel
  layout, hex-literal `Parse`/`TryParse` methods, and other behavior are documented in its own
  unit design, _Rgba32 Unit Design_ (`canvas/rgba32.md`)
- **CssColor** — the public static CSS color parser and formatter, the one color grammar shared by every
  CanvasNet package: hexadecimal forms, the 148 named colors, and the `rgb()` and `hsl()` functions, plus the
  building blocks (`TryParseHex`, `TryGetNamedColor`) that consumers with a narrower grammar reuse; see
  _CssColor Unit Design_ (`canvas/css-color.md`)

### Dependencies

The `Canvas` subsystem's `Surface` unit has one runtime NuGet dependency, `System.Numerics.Tensors`,
used exclusively by its vectorized bulk pixel operations (`PremultiplyAlpha`, `UnpremultiplyAlpha`,
`CompositeOver`) — see _Surface Unit Design_ (`canvas/surface.md`) for details. Beyond that, the
subsystem has no dependencies other than the .NET base class library
(`System.Runtime.InteropServices.MemoryMarshal` and `System.Span<T>`), available natively on all
of CanvasNet's target frameworks.

### Callers

The `Canvas` subsystem's `Surface` unit is a public API entry point, invoked externally by
consumers of the CanvasNet package. It is also invoked internally by every unit of the `Codecs`
subsystem (`BmpCodec`, `PngCodec`, `TiffCodec`, `JpegCodec`, `GifCodec`, `Jpeg2000Codec`), each of which constructs
a `Surface` when loading and reads its rows when saving — see _Codecs Subsystem Design_
(`codecs.md`) for details. It is also invoked internally by the `Drawing` subsystem's `PathFiller`
unit, which composites each rasterized row directly via `Surface.CompositeOverSpan` — see
_Drawing Subsystem Design_ (`drawing.md`) and _PathFiller Unit Design_
(`drawing/path-filler.md`) for details. The `Canvas` subsystem itself has no dependency on the
`Codecs` subsystem or on the `Drawing` subsystem. The `CssColor` unit is a public API entry point as well, called
by the SVG codec, the Vega-Lite renderer, the OpenSCAD renderer, and the command-line tool (see _CssColor Unit
Design_, `canvas/css-color.md`), and has no dependency other than `Rgba32` and the .NET base class library
(`System.Collections.Frozen`, `System.Globalization`).
