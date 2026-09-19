## Canvas

![Canvas Structure](CanvasView.svg)

The `Canvas` subsystem is the first software subsystem in CanvasNet. It provides the pixel-buffer
primitives on which all other CanvasNet functionality builds: a mutable, in-memory 32-bit RGBA
pixel buffer (`Surface`) and the single-pixel value type it stores (`Rgba32`).

### Purpose

The `Canvas` subsystem groups the software units responsible for representing and manipulating
raw pixel data, independent of any file format or drawing operation. It has no dependency on any
other subsystem: the `Codecs` subsystem depends on `Canvas`, not the other way around.

### Units

- **Surface** — mutable, in-memory 32-bit RGBA pixel buffer with allocation-free row access and
  independent-copy cropping; see _Surface Unit Design_ (`canvas/surface.md`)
- **Rgba32** — a blittable four-byte value type representing a single RGBA pixel, documented
  inline within the `Surface` unit design rather than as its own unit, because it has no
  independent behavior beyond being a data carrier consumed exclusively by `Surface`

### Dependencies

N/A - the `Canvas` subsystem has no dependencies beyond the .NET base class library
(`System.Runtime.InteropServices.MemoryMarshal` and `System.Span<T>`), available natively on
all of CanvasNet's target frameworks.

### Callers

The `Canvas` subsystem's `Surface` unit is a public API entry point, invoked externally by
consumers of the CanvasNet package. It is also invoked internally by every unit of the `Codecs`
subsystem (`BmpCodec`, `PngCodec`, `TiffCodec`, `JpegCodec`), each of which constructs a `Surface`
when loading and reads its rows when saving — see _Codecs Subsystem Design_ (`codecs.md`) for
details. The `Canvas` subsystem itself has no dependency on the `Codecs` subsystem, or on the
`Drawing` subsystem reserved for future work.
