# Introduction

<!-- cspell:ignore canvasnet halign -->
<!-- cspell:ignore glyf sfnt codepoint -->
<!-- cspell:ignore rasterizing unparseable SMIL renderable -->
<!-- cspell:ignore unitless -->
<!-- cspell:ignore Zapf -->
<!-- cspell:ignore SASLprep -->
<!-- cspell:ignore Noto -->
<!-- cspell:ignore asvg -->
<!-- cspell:ignore Visio NURBS -->
<!-- cspell:ignore visio -->
<!-- cspell:ignore multmatrix newsize rrggbbaa minkowski tessellators textmetrics fontmetrics -->

## Purpose

This document is the user guide for CanvasNet, a .NET library providing a
canvas-based drawing and rendering API.

## Scope

This user guide covers:

- Installation of the library and of the `canvasnet` command-line tool
- Basic usage and examples
- API reference
- The `canvasnet` command-line tool: its commands, options, JSON output, error codes, and self-validation

# Continuous Compliance

CanvasNet follows the
[Continuous Compliance](https://github.com/demaconsulting/ContinuousCompliance) methodology, which ensures
compliance evidence is generated automatically on every CI run.

## Key Practices

- **Requirements Traceability**: Every requirement is linked to passing tests, and a trace matrix is
  auto-generated on each release
- **Linting Enforcement**: markdownlint, cspell, and yamllint are enforced before any build proceeds
- **Automated Audit Documentation**: Each release ships with generated requirements, justifications,
  trace matrix, and quality reports
- **CodeQL and SonarCloud**: Security and quality analysis runs on every build
- **API Documentation**: Markdown API reference for all public types and members is generated
  automatically and distributed in the `api/` folder of the NuGet package

# Installation

Install the library using the .NET CLI:

```bash
dotnet add package DemaConsulting.CanvasNet
```

To use CanvasNet from the command line instead (to inspect, render, convert, and transform images, documents,
OpenSCAD models, and Vega-Lite charts, from scripts or AI agents), install the `canvasnet` .NET tool, which bundles the
libraries. The
chapter on the command-line tool describes it in full:

```bash
dotnet tool install --global DemaConsulting.CanvasNet.Tool
```

# Usage

## Basic Usage

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

using var surface = new Surface(4, 4);
surface[0, 0] = new Rgba32(255, 0, 0, 255); // opaque red pixel
Console.WriteLine(surface[0, 0].R); // Output: 255
```

## API Reference

### Surface

The `Surface` class provides a mutable, in-memory 32-bit RGBA pixel buffer.

#### Surface Constructors

##### Surface(int width, int height)

```csharp
public Surface(int width, int height)
```

Initializes a new instance of the `Surface` class with the specified dimensions. The pixel buffer
is fully transparent (all channels zero) until pixels are explicitly set.

**Parameters:**

- `width` (int): The width of the surface, in pixels. Must be greater than zero and no more than
  8192.
- `height` (int): The height of the surface, in pixels. Must be greater than zero and no more than
  8192.

**Exceptions:**

- `ArgumentOutOfRangeException`: Thrown when `width` or `height` is less than or equal to zero, or
  greater than 8192.

#### Disposal

`Surface` implements `IDisposable`. Its pixel buffer is currently a plain managed array, so
`Dispose()` has nothing to actually release yet — it exists to establish the disposal contract
ahead of a future release that may back the buffer with a pooled array, without another breaking
API change. Call `Dispose()` (or wrap construction in a `using`/`using var` statement) once a
`Surface` is no longer needed; calling `Dispose()` more than once is safe and has no additional
effect.

```csharp
public void Dispose()
```

After `Dispose()` has been called, every other public member that touches the pixel buffer (the
indexer, `GetRowSpanBytes`, `GetRowSpan`, `Crop`, `PremultiplyAlpha`, `UnpremultiplyAlpha`,
`Clear`, `CompositeOver`, and `CompositeOverSpan`) throws `ObjectDisposedException`.

**Exceptions:**

- `ObjectDisposedException`: Thrown by any other public buffer-touching member once this
  `Surface` has been disposed.

#### Surface Properties

##### Width

```csharp
public int Width { get; }
```

Gets the width of the surface, in pixels.

##### Height

```csharp
public int Height { get; }
```

Gets the height of the surface, in pixels.

##### MaxDimension

```csharp
public const int MaxDimension = 8192;
```

The maximum permitted value for either `width` or `height` passed to the `Surface` constructor.
Callers can compare a `GetInfo` probe result's `Width`/`Height` (and their product, for a
memory-size estimate) against this constant before calling a codec's `Load` method, to reject
untrusted or maliciously oversized image files without decoding any pixel data.

#### Indexer

##### this[int x, int y]

```csharp
public Rgba32 this[int x, int y] { get; set; }
```

Gets or sets the pixel at the specified coordinates. Slower than span-based row access for
processing many pixels; prefer `GetRowSpan`/`GetRowSpanBytes` for bulk operations.

**Exceptions:**

- `ArgumentOutOfRangeException`: Thrown when `x` or `y` is out of range.

#### Surface Methods

##### GetRowSpanBytes

```csharp
public Span<byte> GetRowSpanBytes(int y)
```

Returns the raw bytes of the specified row as a mutable `Span<byte>` aliasing the surface's own
storage.

**Exceptions:**

- `ArgumentOutOfRangeException`: Thrown when `y` is out of range.

##### GetRowSpan

```csharp
public Span<Rgba32> GetRowSpan(int y)
```

Returns the specified row reinterpreted as a mutable `Span<Rgba32>`, aliasing the same storage
as `GetRowSpanBytes` with no copying.

**Exceptions:**

- `ArgumentOutOfRangeException`: Thrown when `y` is out of range.

##### Crop

```csharp
public Surface Crop(int x, int y, int width, int height)
```

Creates a new, independent `Surface` containing a copy of the specified rectangular sub-region.

**Parameters:**

- `x` (int): The zero-based column of the top-left corner. Must not be negative.
- `y` (int): The zero-based row of the top-left corner. Must not be negative.
- `width` (int): The width of the region. Must be greater than zero and fit within the source.
- `height` (int): The height of the region. Must be greater than zero and fit within the source.

**Returns:**

A new `Surface` containing an independent copy of the requested pixels.

**Exceptions:**

- `ArgumentOutOfRangeException`: Thrown when any argument is invalid or the region exceeds the
  source surface's bounds.

##### Resize

```csharp
public Surface Resize(int width, int height, ResamplingMode mode = ResamplingMode.Auto)
```

Creates a new, independent `Surface` resampled to the requested size. Resampling is done in
premultiplied alpha, so transparent pixels leave no color fringe, and with any filtering mode other
than `Nearest` thin features such as one-pixel lines survive a reduction (`Nearest` uses point
sampling and can drop them). A request for the current size returns an exact copy.

**Parameters:**

- `width` (int): The new width. Must be between 1 and `Surface.MaxDimension`.
- `height` (int): The new height. Must be between 1 and `Surface.MaxDimension`.
- `mode` (`ResamplingMode`): The reconstruction filter. `Auto` (default) uses `Area` on an axis that
  shrinks and `Bicubic` on an axis that enlarges. The other values are `Nearest`, `Bilinear`,
  `Bicubic` (Catmull-Rom), `Lanczos3` and `Area`.

**Exceptions:**

- `ArgumentOutOfRangeException`: Thrown when a dimension is out of range or `mode` is not defined.
- `ObjectDisposedException`: Thrown when the surface has been disposed.

##### ResizeToFit

```csharp
public Surface ResizeToFit(int maxWidth, int maxHeight, ResamplingMode mode = ResamplingMode.Auto)
```

Creates a new `Surface` with the aspect ratio preserved that fits inside the given box, enlarging or
shrinking as needed (each dimension is at least one pixel).

**Parameters:**

- `maxWidth` (int): The width of the bounding box. Must be between 1 and `Surface.MaxDimension`.
- `maxHeight` (int): The height of the bounding box. Must be between 1 and `Surface.MaxDimension`.
- `mode` (`ResamplingMode`): The reconstruction filter; see `ResamplingMode` below. Defaults to `Auto`.

**Returns:**

A new `Surface` whose aspect ratio matches the source and that fits inside the box.

**Exceptions:**

- `ArgumentOutOfRangeException`: Thrown when `maxWidth` or `maxHeight` is out of range or `mode` is
  not defined.
- `ObjectDisposedException`: Thrown when the surface has been disposed.

##### ResamplingMode

The `ResamplingMode` enum selects the reconstruction filter for `Resize` and `ResizeToFit`:

- `Auto`: `Area` on an axis that shrinks, `Bicubic` on an axis that enlarges, exact copy when unchanged.
- `Nearest`: point sampling; hard edges, but aliases and can drop thin features when shrinking.
- `Bilinear`: triangle filter; smooth, no overshoot.
- `Bicubic`: Catmull-Rom cubic; sharper, with clamped overshoot.
- `Lanczos3`: three-lobe windowed sinc; the sharpest smooth filter, with mild ringing.
- `Area`: exact area averaging; best for large reductions.

##### PremultiplyAlpha

```csharp
public void PremultiplyAlpha()
```

Converts this surface's pixel buffer, in place, from straight (unassociated) alpha to
premultiplied alpha: each color channel becomes `round(channel * alpha / 255)`
(round-half-away-from-zero, clamped to `[0, 255]`); alpha is unchanged. Never throws.

##### UnpremultiplyAlpha

```csharp
public void UnpremultiplyAlpha()
```

Converts this surface's pixel buffer, in place, from premultiplied alpha back to straight alpha -
the inverse of `PremultiplyAlpha`. Each color channel becomes `round(channel * 255 / alpha)`
(round-half-away-from-zero, clamped to `[0, 255]`) for non-zero alpha; fully transparent pixels
(`alpha == 0`) are defined as `R = G = B = 0`. Never throws.

##### CompositeOver(Surface foreground)

```csharp
public void CompositeOver(Surface foreground)
```

Composites `foreground` "over" this surface in place, using standard Porter-Duff "over" alpha
compositing on straight-alpha pixels (no explicit `PremultiplyAlpha` call is needed by callers).

**Parameters:**

- `foreground` (Surface): The surface to composite over this one. Must be the same size as this
  surface.

**Exceptions:**

- `ArgumentNullException`: Thrown when `foreground` is null.
- `ArgumentException`: Thrown when `foreground`'s `Width` or `Height` does not match this
  surface's.

##### CompositeOver(Rgba32 color)

```csharp
public void CompositeOver(Rgba32 color)
```

Composites the constant `color` "over" every pixel of this surface in place, using the same
formula as `CompositeOver(Surface)` with `color` acting as the foreground at every pixel. Never
throws.

##### Clear(Rgba32 color)

```csharp
public void Clear(Rgba32 color)
```

Overwrites every pixel of this surface with the constant `color`, in place, replacing rather
than blending against existing pixel data. Unlike `CompositeOver(Rgba32)`, no Porter-Duff "over"
formula is evaluated — every pixel becomes exactly `color`, regardless of `color.A` or of what
was previously stored there. This is the recommended way to establish a known background before
drawing (for example clearing a surface to transparent black before filling shapes onto it).
Never throws. `Canvas.Clear(Rgba32)` is a thin, transform-independent passthrough to this method.

### Rgba32

The `Rgba32` struct represents a single 32-bit RGBA pixel, with public `R`, `G`, `B`, and `A`
`byte` fields and value equality (`Equals`, `GetHashCode`, `==`, `!=`).

### ImageInfo

```csharp
public readonly record struct ImageInfo(int Width, int Height, int Channels, bool HasAlpha);
```

The `ImageInfo` record struct represents the result of a header-only probe of an image file via
a codec's `GetInfo` method: the image's `Width` and `Height` in pixels, its `Channels` count (3
for RGB, 4 for RGBA, 1 for grayscale where applicable), and whether it `HasAlpha`. `GetInfo`
methods read only enough of the file to populate an `ImageInfo` - never decoding pixel data for
every codec except `GifCodec`, whose `GetInfo` decodes the first frame's compressed pixel data (to
determine `CanDecode`, without ever resolving that data into a rendered `Surface`) - so they are
safe to call on untrusted or very large files before deciding whether to call `Load`.
Unlike `Load`, `GetInfo` does not enforce `Surface.MaxDimension`, so callers should compare the
returned dimensions against `Surface.MaxDimension` themselves when triaging untrusted input.

`ImageInfo` also has a `CanDecode` property (`init`-only, defaulting to `true`), set by a codec's
`GetInfo` to `false` when the probed file is well-formed but declares a feature that codec's
`Load` does not implement (PNG Adam7 interlacing), or when its pixel data is corrupt in a way only
detectable by attempting to decode it (GIF: the first frame's compressed data fails to LZW-decode).
A caller can check `CanDecode` before calling `Load` to detect either case up front, instead of
catching `UnsupportedImageFeatureException` or `InvalidDataException` from `Load` itself - see
[Handling Unsupported Features](#handling-unsupported-features).

`ImageInfo` also has a `FrameCount` property (`init`-only, defaulting to `1`), reporting the
total number of frames a codec's `Load` would find in the file if it inspected every one.
Every codec except `GifCodec` reports `FrameCount == 1` unconditionally, since none of them has a
concept of multiple frames. `GifCodec.GetInfo` is the sole exception: a GIF file may legitimately
declare more than one frame (an animation), and `GifCodec.GetInfo` reports the file's true count
by walking its block structure - without ever resolving any frame's decoded pixels into a
`Surface` - see [GifCodec](#gifcodec) below.

### BmpCodec

The `BmpCodec` static class loads and saves `Surface` pixel buffers as uncompressed Windows BMP
files (24-bit or 32-bit, BI_RGB). Only this uncompressed variant is supported; palette-based
formats, RLE compression, and other header variants are rejected with `InvalidDataException`.

#### BmpBitDepth

```csharp
public enum BmpBitDepth
{
    Bit24 = 24, // no alpha channel written; alpha is dropped
    Bit32 = 32, // alpha channel preserved
}
```

#### BmpCodec Methods

##### Load(Stream stream)

```csharp
public static Surface Load(Stream stream)
```

Loads a `Surface` from an open, readable stream containing an uncompressed 24-bit or 32-bit BMP
image. Pixels decoded from a 24-bit BMP always have alpha 255 (fully opaque).

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `InvalidDataException`: Thrown when the stream does not contain a valid, supported BMP image.

##### Load(string path)

```csharp
public static Surface Load(string path)
```

Loads a `Surface` from a BMP file at the specified path.

**Exceptions:**

- `ArgumentNullException`: Thrown when `path` is null.
- `ArgumentException`: Thrown when `path` is an empty string.
- `InvalidDataException`: Thrown for the same conditions as `Load(Stream)`.

##### GetInfo(Stream stream)

```csharp
public static ImageInfo GetInfo(Stream stream)
```

Reads only the BMP header (never pixel data) from an open, readable stream and returns an
`ImageInfo` describing the image. Does not enforce `Surface.MaxDimension`.

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `InvalidDataException`: Thrown when the stream does not contain a valid BMP header.

##### GetInfo(string path)

```csharp
public static ImageInfo GetInfo(string path)
```

Reads only the BMP header from a file at the specified path and returns an `ImageInfo`.

**Exceptions:**

- `ArgumentNullException`: Thrown when `path` is null.
- `ArgumentException`: Thrown when `path` is an empty string.
- `InvalidDataException`: Thrown for the same conditions as `GetInfo(Stream)`.

##### Save(Surface surface, Stream stream, BmpBitDepth bitDepth = BmpBitDepth.Bit32)

```csharp
public static void Save(Surface surface, Stream stream, BmpBitDepth bitDepth = BmpBitDepth.Bit32)
```

Saves a `Surface` to a stream as an uncompressed BMP image. Saving at `BmpBitDepth.Bit24` discards
the source surface's alpha channel entirely, so reloading always yields fully opaque pixels.

**Exceptions:**

- `ArgumentNullException`: Thrown when `surface` or `stream` is null.
- `ArgumentOutOfRangeException`: Thrown when `bitDepth` is not a defined `BmpBitDepth` value.

##### Save(Surface surface, string path, BmpBitDepth bitDepth = BmpBitDepth.Bit32)

```csharp
public static void Save(Surface surface, string path, BmpBitDepth bitDepth = BmpBitDepth.Bit32)
```

Saves a `Surface` to a file as an uncompressed BMP image, overwriting any existing file at `path`.

**Exceptions:**

- `ArgumentNullException`: Thrown when `surface` or `path` is null.
- `ArgumentException`: Thrown when `path` is an empty string.
- `ArgumentOutOfRangeException`: Thrown when `bitDepth` is not a defined `BmpBitDepth` value.

### PngCodec

The `PngCodec` static class loads and saves `Surface` pixel buffers as PNG files. `Load` decodes
any non-interlaced PNG whose color type and bit depth form a combination the PNG specification
defines: Grayscale, Truecolor, Palette/indexed, Grayscale-with-alpha, and Truecolor-with-alpha, at
whichever bit depths (1, 2, 4, 8, or 16) each color type permits, honoring `tRNS`-chunk
transparency for Grayscale, Truecolor, and Palette source data. `Save` writes only
8-bit-per-channel Truecolor (RGB) or Truecolor-with-alpha (RGBA), non-interlaced. Any
bit-depth/color-type combination the PNG specification itself does not define (for example
Palette at bit depth 16) is malformed and is rejected by `Load` with `InvalidDataException`.
Adam7-interlaced data is well-formed but not implemented by `Load`, which rejects it with the
distinct `UnsupportedImageFeatureException` (see [Handling Unsupported
Features](#handling-unsupported-features)) rather than `InvalidDataException` - callers that need
to detect this case before calling `Load` at all should check `GetInfo`'s `CanDecode` result
instead.

#### PngColorType

```csharp
public enum PngColorType
{
    Rgb = 2,  // no alpha channel written; alpha is dropped
    Rgba = 6, // alpha channel preserved
}
```

#### PngCodec Methods

##### PngCodec.Load(Stream stream)

```csharp
public static Surface Load(Stream stream)
```

Loads a `Surface` from an open, readable stream containing a PNG image whose color type and bit
depth combination the PNG specification defines, and which is not Adam7-interlaced. Pixels decoded
from source data without an alpha channel (Grayscale, Truecolor, or Palette without a `tRNS`
match) always have alpha 255 (fully opaque) unless a `tRNS`-chunk key-color or per-palette-entry
value makes them fully transparent (alpha 0).

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `InvalidDataException`: Thrown when the stream does not contain a valid PNG image, or uses a
  bit-depth/color-type combination the PNG specification does not define.
- `UnsupportedImageFeatureException`: Thrown when the image is well-formed but Adam7-interlaced
  (`Feature` is `"png-adam7-interlace"`). This is a distinct type from `InvalidDataException` -
  see [Handling Unsupported Features](#handling-unsupported-features).

##### PngCodec.Load(string path)

```csharp
public static Surface Load(string path)
```

Loads a `Surface` from a PNG file at the specified path.

**Exceptions:**

- `ArgumentNullException`: Thrown when `path` is null.
- `ArgumentException`: Thrown when `path` is an empty string.
- `InvalidDataException`/`UnsupportedImageFeatureException`: Thrown for the same conditions as
  `Load(Stream)`.

##### PngCodec.GetInfo(Stream stream)

```csharp
public static ImageInfo GetInfo(Stream stream)
```

Reads only the PNG signature and `IHDR` chunk (never pixel data) from an open, readable stream
and returns an `ImageInfo` describing the image. Succeeds for every well-formed `IHDR`, including
Adam7-interlaced files and every color-type/bit-depth combination the PNG specification defines,
even those `Load` refuses (Adam7) - for these, the returned `ImageInfo.CanDecode` is `false`,
signaling that `Load` will throw `UnsupportedImageFeatureException` rather than decode the file.
Does not enforce `Surface.MaxDimension`. For Palette (indexed) files, `Channels` and `HasAlpha`
describe the raw file encoding (1 channel, no alpha) rather than the 4-channel RGBA result `Load`
would produce after resolving palette indices.

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `InvalidDataException`: Thrown when the stream does not contain a valid PNG signature/`IHDR`,
  or when `IHDR` declares a bit-depth/color-type combination the PNG specification does not
  define.

##### PngCodec.GetInfo(string path)

```csharp
public static ImageInfo GetInfo(string path)
```

Reads only the PNG signature/`IHDR` from a file at the specified path and returns an `ImageInfo`.

**Exceptions:**

- `ArgumentNullException`: Thrown when `path` is null.
- `ArgumentException`: Thrown when `path` is an empty string.
- `InvalidDataException`: Thrown for the same conditions as `GetInfo(Stream)`.

##### Save(Surface surface, Stream stream, PngColorType colorType = PngColorType.Rgba)

```csharp
public static void Save(Surface surface, Stream stream, PngColorType colorType = PngColorType.Rgba)
```

Saves a `Surface` to a stream as a PNG image. Saving at `PngColorType.Rgb` discards the source
surface's alpha channel entirely, so reloading always yields fully opaque pixels.

**Exceptions:**

- `ArgumentNullException`: Thrown when `surface` or `stream` is null.
- `ArgumentOutOfRangeException`: Thrown when `colorType` is not a defined `PngColorType` value.

##### Save(Surface surface, string path, PngColorType colorType = PngColorType.Rgba)

```csharp
public static void Save(Surface surface, string path, PngColorType colorType = PngColorType.Rgba)
```

Saves a `Surface` to a file as a PNG image, overwriting any existing file at `path`.

**Exceptions:**

- `ArgumentNullException`: Thrown when `surface` or `path` is null.
- `ArgumentException`: Thrown when `path` is an empty string.
- `ArgumentOutOfRangeException`: Thrown when `colorType` is not a defined `PngColorType` value.

### TiffCodec

The `TiffCodec` static class loads and saves `Surface` pixel buffers as TIFF 6.0 files, supporting
only 8-bit-per-sample RGB and Grayscale (BlackIsZero) photometric interpretations, Chunky planar
configuration, and strip-based layouts (single- or multi-strip). Tiled TIFFs, other bit depths,
Planar configuration, and unsupported photometric interpretations (Palette, CMYK, YCbCr, etc.)
are rejected with `InvalidDataException`. `Load` supports both little-endian ("II") and
big-endian ("MM") byte order, and None/PackBits/LZW/Deflate compression, with an optional
horizontal-differencing predictor for LZW/Deflate.

#### TiffCompression

```csharp
public enum TiffCompression
{
    None = 1,     // no compression
    Lzw = 5,      // TIFF-flavor LZW (variable-width codes, MSB-first bit packing)
    PackBits = 32773, // Apple/TIFF PackBits RLE
    Deflate = 8,  // zlib-wrapped DEFLATE
}
```

#### TiffCodec Methods

##### TiffCodec.Load(Stream stream)

```csharp
public static Surface Load(Stream stream)
```

Loads a `Surface` from an open, readable stream containing a supported TIFF image. Pixels decoded
from an RGB (no `ExtraSamples`) or Grayscale image always have alpha 255 (fully opaque).

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `InvalidDataException`: Thrown when the stream does not contain a valid, supported TIFF image.

##### TiffCodec.Load(string path)

```csharp
public static Surface Load(string path)
```

Loads a `Surface` from a TIFF file at the specified path.

**Exceptions:**

- `ArgumentNullException`: Thrown when `path` is null.
- `ArgumentException`: Thrown when `path` is an empty string.
- `InvalidDataException`: Thrown for the same conditions as `Load(Stream)`.

##### TiffCodec.GetInfo(Stream stream)

```csharp
public static ImageInfo GetInfo(Stream stream)
```

Reads only the TIFF header and the relevant IFD tags (never strip data) and returns an
`ImageInfo` describing the image. Does not enforce `Surface.MaxDimension`. A TIFF's IFD can
legitimately be located anywhere in the file (unlike PNG/JPEG/BMP, whose headers are always near
the start), so the reporting strategy depends on `stream.CanSeek`. When `stream.CanSeek` is
`true`, a `StreamTiffDataSource` seeks directly to the IFD and reads only the bytes the parser
actually asks for - the header, IFD entries, and any out-of-line tag value needed (for example a
multi-value `BitsPerSample` tag) - and never reads `StripOffsets`/`RowsPerStrip`/`StripByteCounts`
or any strip/pixel data. When `stream.CanSeek` is `false` (for example a network stream), `GetInfo`
falls back to the same unconditional buffering `Load` already performs: the entire stream is
read into memory and the resulting bytes are probed through the same validating parser, so a
non-seekable source's IFD can still be located and resolved wherever it lies, and `GetInfo` never
throws merely because its input happens to be non-seekable. In both cases every tag is resolved
through the exact same validating parser `Load` itself uses (`ReadTiffImageInfo`), resolving
`ImageWidth`, `ImageLength`, `BitsPerSample`, `SamplesPerPixel`, `Compression`,
`PhotometricInterpretation`, `PlanarConfiguration`, `Predictor`, and `ExtraSamples`.

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `InvalidDataException`: Thrown when the stream does not contain a valid TIFF header/IFD.

##### TiffCodec.GetInfo(string path)

```csharp
public static ImageInfo GetInfo(string path)
```

Reads only the TIFF header/IFD from a file at the specified path and returns an `ImageInfo`.

**Exceptions:**

- `ArgumentNullException`: Thrown when `path` is null.
- `ArgumentException`: Thrown when `path` is an empty string.
- `InvalidDataException`: Thrown for the same conditions as `GetInfo(Stream)`.

##### Save(Surface surface, Stream stream, TiffCompression compression = TiffCompression.None)

```csharp
public static void Save(Surface surface, Stream stream, TiffCompression compression = TiffCompression.None)
```

Saves a `Surface` to a stream as a little-endian, 8-bit RGBA, Chunky, single-strip TIFF image,
compressed using the specified `compression` method. The horizontal-differencing predictor is
applied automatically (and the `Predictor` tag written) when `compression` is `Lzw` or `Deflate`.

**Exceptions:**

- `ArgumentNullException`: Thrown when `surface` or `stream` is null.
- `ArgumentOutOfRangeException`: Thrown when `compression` is not a defined `TiffCompression`
  value.

##### Save(Surface surface, string path, TiffCompression compression = TiffCompression.None)

```csharp
public static void Save(Surface surface, string path, TiffCompression compression = TiffCompression.None)
```

Saves a `Surface` to a file as a TIFF image, overwriting any existing file at `path`.

**Exceptions:**

- `ArgumentNullException`: Thrown when `surface` or `path` is null.
- `ArgumentException`: Thrown when `path` is an empty string.
- `ArgumentOutOfRangeException`: Thrown when `compression` is not a defined `TiffCompression`
  value.

### JpegCodec

The `JpegCodec` static class loads and saves `Surface` pixel buffers as JPEG files, supporting
baseline sequential DCT (SOF0) and progressive DCT (SOF2) decode, grayscale and 3-component YCbCr
images, and 4:4:4/4:2:2/4:2:0 chroma sampling on load. Saving always writes a baseline,
3-component YCbCr, 4:2:0 chroma-subsampled JPEG at a caller-supplied quality setting. JPEG is a
lossy format, so decoded pixels from a saved image are expected to be visually close rather than
bit-for-bit identical to the source.

#### JpegCodec Methods

##### JpegCodec.Load(Stream stream)

```csharp
public static Surface Load(Stream stream)
```

Loads a `Surface` from an open, readable stream containing a supported JPEG image. Decoded pixels
are always fully opaque (`A = 255`), and grayscale JPEG expands to `R = G = B`.

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `InvalidDataException`: Thrown when the stream does not contain a valid, supported JPEG image.

##### JpegCodec.Load(string path)

```csharp
public static Surface Load(string path)
```

Loads a `Surface` from a JPEG file at the specified path.

**Exceptions:**

- `ArgumentNullException`: Thrown when `path` is null.
- `ArgumentException`: Thrown when `path` is an empty string.
- `InvalidDataException`: Thrown for the same conditions as `Load(Stream)`.

##### JpegCodec.GetInfo(Stream stream)

```csharp
public static ImageInfo GetInfo(Stream stream)
```

Scans markers (skipping length-prefixed segments without entropy-decoding any scan data) to find
the first SOF0/SOF2 marker, and returns an `ImageInfo` describing the image. Does not enforce
`Surface.MaxDimension`. Incrementally reads up to a soft cap of `MaxProbeHeaderBytes` (1,048,576
bytes), which comfortably covers the leading marker segments of essentially all real-world JPEG
files. If that soft cap is reached without finding a SOF0/SOF2 marker, `GetInfo` keeps scanning
past the cap - one marker segment at a time, exactly as it does below the cap - until a SOF0/SOF2
marker is found, so `GetInfo` never throws merely because a file has more than
`MaxProbeHeaderBytes` of leading marker-segment data - as long as `Load` itself would successfully
parse that file up to and including the SOF marker. That post-soft-cap scanning is bounded by two
independent ceilings, either of which stops it once reached without a SOF0/SOF2 marker ever being
found: a much larger hard byte limit, `MaxProbeHeaderBytesHardLimit` (16,777,216 bytes, 16x the
soft cap), and a hard segment-count limit, `MaxProbeSegmentCount` (512), which bounds the number
of non-terminating marker segments scanned directly - since a marker segment can be as small as
4 bytes, the byte limit alone would not cheaply bound scan iterations for a malformed stream built
from many minimal-size segments. Once either ceiling is reached, `GetInfo` throws
`InvalidDataException` rather than continuing to read, buffer, or loop without bound, protecting
against a malformed, adversarial, or effectively-infinite stream that never presents a SOF0/SOF2
marker. The segment-count limit never applies to the terminating SOF0/SOF2 marker segment itself,
so a well-formed file's SOF marker always succeeds regardless of which segment number it falls on.
`Load` enforces these same two ceilings on its own pre-SOF marker-segment walk and throws the same
`InvalidDataException` for the same condition, so a pathological JPEG whose leading marker-segment
data exceeds either ceiling is rejected consistently by both `GetInfo` and `Load` - true parity,
not a documented exception - because both ceilings are sized generously enough (16 MiB, or 512
segments) that no realistic real-world JPEG is ever affected by it.

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `InvalidDataException`: Thrown when the SOI marker is missing, an SOS marker or end-of-image is
  reached before any SOF0/SOF2 marker is found, an unsupported SOF/frame marker is encountered, no
  SOF0/SOF2 marker is found before the stream genuinely ends (a genuinely truncated or non-JPEG
  input) - the same condition `Load(Stream)` itself would reject on the same bytes - the
  `MaxProbeHeaderBytesHardLimit` hard ceiling is reached without a SOF0/SOF2 marker ever being
  found, or the `MaxProbeSegmentCount` segment-count ceiling is reached without a SOF0/SOF2 marker
  ever being found - both of the latter two conditions equally rejected by `Load(Stream)` on the
  same bytes.

##### JpegCodec.GetInfo(string path)

```csharp
public static ImageInfo GetInfo(string path)
```

Scans markers in a file at the specified path and returns an `ImageInfo`.

**Exceptions:**

- `ArgumentNullException`: Thrown when `path` is null.
- `ArgumentException`: Thrown when `path` is an empty string.
- `InvalidDataException`: Thrown for the same conditions as `GetInfo(Stream)`.

##### JpegCodec.Save(Surface surface, Stream stream, int quality = 90)

```csharp
public static void Save(Surface surface, Stream stream, int quality = 90)
```

Saves a `Surface` to a stream as a baseline, 3-component YCbCr, 4:2:0 chroma-subsampled JPEG
image. `quality` must be in the inclusive range 1-100.

**Exceptions:**

- `ArgumentNullException`: Thrown when `surface` or `stream` is null.
- `ArgumentOutOfRangeException`: Thrown when `quality` is less than 1 or greater than 100.

##### JpegCodec.Save(Surface surface, string path, int quality = 90)

```csharp
public static void Save(Surface surface, string path, int quality = 90)
```

Saves a `Surface` to a file as a JPEG image, overwriting any existing file at `path`.

**Exceptions:**

- `ArgumentNullException`: Thrown when `surface` or `path` is null.
- `ArgumentException`: Thrown when `path` is an empty string.
- `ArgumentOutOfRangeException`: Thrown when `quality` is less than 1 or greater than 100.

### GifCodec

The `GifCodec` static class loads `Surface` pixel buffers from GIF87a/GIF89a files. `GifCodec` is
decode-only: there is no `Save`. A well-formed GIF file may contain multiple frames (an
animation), but this codec decodes only the *first* Image Descriptor's pixel data - every
subsequent frame is parsed only far enough to validate its structure and is then discarded. This
is a deliberate, documented scope limitation, not a malformed-input condition, so a multi-frame
GIF never causes `Load` to throw, and a well-formed multi-frame GIF is not, by itself, a
`CanDecode == false` case; `GetInfo`'s `CanDecode` is `false` only when the first frame's
compressed pixel data fails to LZW-decode (see `GifCodec.GetInfo(Stream stream)` below).
Supported features include a Global or Local Color Table, the Graphic Control Extension's
transparent color index, interlaced Image Descriptors (de-interlaced back to normal row order),
and Image Descriptors covering a sub-region of the logical screen. GIF's LZW compression uses a
distinct bit-packing and code-value scheme from `TiffCodec`'s, so `GifCodec` implements its own
private GIF-native LZW decoder rather than reusing `TiffCodec`'s.

#### GifCodec Methods

##### GifCodec.Load(Stream stream)

```csharp
public static Surface Load(Stream stream)
```

Loads a `Surface` from an open, readable stream containing a GIF87a or GIF89a image, decoding only
the first Image Descriptor. The returned `Surface` is sized to the file's Logical Screen
Descriptor dimensions. A pixel outside the first frame's region, or whose palette index matches an
active Graphic Control Extension's transparent color index, is fully transparent (alpha 0); every
other decoded pixel is fully opaque (alpha 255).

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `InvalidDataException`: Thrown when the stream does not contain a valid, supported GIF image
  (bad signature, dimensions exceeding `Surface.MaxDimension`, missing color table, a malformed
  Graphic Control Extension, an unexpected block introducer, trailing data after the Trailer, no
  Image Descriptor found, an out-of-bounds Image Descriptor, any Image Descriptor's LZW minimum
  code size byte outside the valid 2-8 range, an invalid LZW code, the compressed data decoding to
  a different pixel count than declared or omitting the required end-of-information code,
  cumulative sub-block data across the whole file exceeding `MaxTotalSubBlockBytes`, or a
  truncated stream).

##### GifCodec.Load(string path)

```csharp
public static Surface Load(string path)
```

Loads a `Surface` from a GIF file at the specified path.

**Exceptions:**

- `ArgumentNullException`: Thrown when `path` is null.
- `ArgumentException`: Thrown when `path` is an empty string.
- `InvalidDataException`: Thrown for the same conditions as `Load(Stream)`.

##### GifCodec.GetInfo(Stream stream)

```csharp
public static ImageInfo GetInfo(Stream stream)
```

Walks the GIF's Logical Screen Descriptor, color tables, and every subsequent block through and
including the Trailer, and returns an `ImageInfo` describing the image - including the file's
true total frame count in `FrameCount` - without ever resolving any frame's decoded pixels into a
`Surface`, and without enforcing `Surface.MaxDimension`. `Channels` is always 1. `GetInfo` never
invokes the LZW decoder for any frame after the first (matching `Load`'s own
decode-only-the-first-frame scope), but it does attempt the same first-frame LZW decode `Load`
performs - discarding the decoded palette-index output instead of resolving it into a `Surface` -
so `CanDecode` is `false` only when that first-frame decode attempt fails (a well-formed container
whose first frame's compressed pixel data is corrupt); `true` for every other well-formed GIF
file, including one with more than one frame.

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `InvalidDataException`: Thrown when the stream does not begin with the "GIF87a"/"GIF89a"
  signature, the declared width or height is non-positive, no color table (global or local) is
  available for some Image Descriptor, a Graphic Control Extension's data is not exactly 4 bytes,
  an Image Descriptor's region lies outside the logical screen, any Image Descriptor's LZW minimum
  code size is outside the 2-8 range, an unexpected/unrecognized block introducer byte is
  encountered, no Image Descriptor is ever encountered before the Trailer, trailing bytes remain in
  the stream after the Trailer, the cumulative sub-block data read across the whole file exceeds
  `GifCodec.MaxTotalSubBlockBytes`, or the stream ends before all header, color-table, or block
  data has been read. This is *structural* malformation only - a well-formed container whose first
  frame's compressed data merely fails to LZW-decode does **not** throw from `GetInfo`; it is
  instead reported via `CanDecode == false` (see above).

##### GifCodec.GetInfo(string path)

```csharp
public static ImageInfo GetInfo(string path)
```

Reads the signature, Logical Screen Descriptor, and every block of a GIF file at the specified
path and returns an `ImageInfo`.

**Exceptions:**

- `ArgumentNullException`: Thrown when `path` is null.
- `ArgumentException`: Thrown when `path` is an empty string.
- `InvalidDataException`: Thrown for the same conditions as `GetInfo(Stream)`.

### Jpeg2000Codec

The `Jpeg2000Codec` static class decodes JPEG 2000 Part 1 images (JP2 files and raw codestreams)
into `Surface` pixel buffers. It is decode-only: there is no `Save`. It supports reversible and
irreversible coding, tiles and tile-parts, quality layers, all five progression orders, precincts,
every code-block style, regions of interest, 1 to 16 bit samples, subsampled components, and the
JP2 color, palette and channel-definition boxes. JPEG 2000 Part 2 extensions and High Throughput
(Part 15) codestreams are rejected with `UnsupportedImageFeatureException`. An embedded ICC profile
is reported but not applied.

#### Jpeg2000Codec Methods

##### Jpeg2000Codec.Load(Stream stream) / Jpeg2000Codec.Load(string path)

```csharp
public static Surface Load(Stream stream)
public static Surface Load(string path)
```

Decodes the image to an RGBA `Surface` (gray expanded to RGB, CMYK converted to RGB, alpha
preserved).

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` or `path` is null.
- `ArgumentException`: Thrown when `path` is an empty string.
- `FileNotFoundException`: Thrown when the file at `path` does not exist.
- `InvalidDataException`: Thrown when the data is not JPEG 2000, exceeds `Surface.MaxDimension`,
  or is truncated or corrupt.
- `UnsupportedImageFeatureException`: Thrown for valid but unsupported JPEG 2000 features.

##### Jpeg2000Codec.GetInfo(Stream stream) / Jpeg2000Codec.GetInfo(string path)

```csharp
public static ImageInfo GetInfo(Stream stream)
public static ImageInfo GetInfo(string path)
```

Returns the width, height, channel count and alpha presence from the container and codestream
header alone, without decoding pixels and without enforcing `Surface.MaxDimension`. Only the header
is read: for JP2 the boxes up to the codestream box, then the SOC and SIZ segments (at most about
64 KiB of the codestream), so the position of the stream afterwards is unspecified and the
codestream body is never read. A box before the codestream is skipped by seeking when
`stream.CanSeek` is `true`; on a non-seekable stream it is read and discarded (never buffered), up
to the 256 MiB input limit. A truncated file whose codestream box is longer than its data is only
rejected by `Load`/`Decode` (or by `GetInfo` on a seekable stream). Throws the same
argument and `InvalidDataException` errors as `Load`.

##### Jpeg2000Codec.Decode(Stream stream) / Jpeg2000Codec.Decode(byte[] data)

```csharp
public static Jpeg2000Image Decode(Stream stream)
public static Jpeg2000Image Decode(byte[] data)
```

Decodes to a `Jpeg2000Image` exposing `Width`, `Height`, `ColorSpace` (`Gray`, `Srgb`, `Cmyk` or
`Unknown`), `ColorChannelCount`, 8-bit interleaved `ColorSamples`, optional `AlphaSamples` (with
`AlphaPremultiplied`), `HasAlpha` (true when `AlphaSamples` is present), the `IccProfile` bytes, the
source `BitDepth` of the first color channel before 8-bit scaling, and `HasPalette` (whether a JP2
palette was applied to `ColorSamples`).
Throws the same exceptions as `Load`.

##### Jpeg2000Codec.Decode(..., Jpeg2000DecoderLimits limits)

```csharp
public static Jpeg2000Image Decode(Stream stream, Jpeg2000DecoderLimits limits)
public static Jpeg2000Image Decode(byte[] data, Jpeg2000DecoderLimits limits)
```

Decodes with caller-supplied resource limits, for example tighter limits for untrusted input.
Throws `ArgumentNullException` for null limits and `ArgumentOutOfRangeException` for a
non-positive limit. `Jpeg2000DecoderLimits` is a record with `init` properties
(`MaxInputBytes` 256 MiB, `MaxWidth`/`MaxHeight` `Surface.MaxDimension`, `MaxTotalSamples`
2^27, `MaxTileSamples` 2^26, `MaxTiles` 65535, `MaxTilePrecincts` 2^18, `MaxTileCodeBlocks`
2^20, `MaxTilePackets` 2^22, `MaxProgressionChanges` 128, `MaxProgressionSteps` 2^30 and
`MaxTier1Work` 2^34); `Jpeg2000DecoderLimits.Default` holds the defaults. The progression and
tier-1 work ceilings additionally scale with the input length, so a small hostile stream cannot
consume the full budget. Data over a limit fails with `InvalidDataException`.

The defaults favour accepting every valid image, so a hostile stream can still cost CPU time:
roughly 50 microseconds per input byte (Release build; about twice that in Debug) up to the
`MaxTier1Work` ceiling, which at the default is on the order of a minute or more of CPU for a
stream of about 1 MB. When decoding untrusted input, pass tighter limits sized to what you expect:

```csharp
var untrustedBytes = File.ReadAllBytes("untrusted.jp2");
var limits = new Jpeg2000DecoderLimits
{
    MaxInputBytes = 8 * 1024 * 1024,
    MaxTotalSamples = 1 << 24,        // about 16 megapixel-samples
    MaxTier1Work = 1L << 30,          // hard cap on entropy-decoding work
};
var image = Jpeg2000Codec.Decode(untrustedBytes, limits);
```

### SvgCodec

`SvgCodec` is distributed via the separate `DemaConsulting.CanvasNet.Svg` NuGet package (namespace
`DemaConsulting.CanvasNet.Svg`), which references the core `DemaConsulting.CanvasNet` package -
see the Installation section of the project README.

The `SvgCodec` static class decodes and rasterizes a common real-world subset of SVG documents
into a `Surface` of caller-chosen pixel dimensions. `SvgCodec` is decode-only: there is no `Save`.
It supports basic shapes (`rect`, `circle`, `ellipse`, `line`, `polyline`, `polygon`, `path`),
grouping (`g`) with cascading presentation attributes, `transform` functions, linear/radial
gradients (including `xlink:href`/`href` template inheritance), `use` references, `marker`
elements (referenced via `marker-start`/`marker-mid`/`marker-end`, with `markerWidth`/
`markerHeight`, `refX`/`refY`, `markerUnits`, `orient`, and an optional `viewBox`), `filter`
elements (referenced via the `filter` presentation attribute on any directly renderable shape or
`text` element, or on a `g`/`symbol` reference/`use` element - applied to the whole referenced
subtree as a single unit - with `x`/`y`/`width`/`height` filter-region attributes and `feBlend`/
`feColorMatrix`/`feComponentTransfer`/`feComposite`/`feConvolveMatrix`/`feDiffuseLighting`/
`feDisplacementMap`/`feDropShadow`/`feFlood`/`feGaussianBlur`/`feImage`/`feMerge`/`feMorphology`/
`feOffset`/`feSpecularLighting`/`feTile`/`feTurbulence` primitive children), `clipPath` elements
(referenced via the `clip-path` presentation attribute, hard-clipping to the union of the
`clipPath` element's own rect/circle/ellipse/polyline/polygon/path/text children, honoring
`clipPathUnits` and each child's own `clip-rule`), `mask` elements (referenced via the `mask`
presentation attribute, attenuating alpha by the referenced content's own computed luminance,
honoring `maskUnits`/`x`/`y`/`width`/`height` and `maskContentUnits` as independent attributes) -
`clip-path`/`mask` apply to the same per-shape/per-group targets `filter` does, and, when
combined with `filter` on the same element, are applied before that element's own filter chain
runs - `pattern` elements (referenced from a shape/text element's own `fill`/`stroke` as a tiled
paint server, honoring `patternUnits`/`patternContentUnits`, `patternTransform`, an optional
`viewBox`/`preserveAspectRatio`, and `href`/`xlink:href` tile-content inheritance) - `image`
elements (a base64-encoded `data:` URI in a supported raster format, decoded through that
format's own existing codec and drawn into the element's own `x`/`y`/`width`/`height` placement
rect) - `image-rendering` on an `image` (attribute, `style` declaration or stylesheet rule, inherited
from ancestors) selects the resampling filter: `pixelated`, `crisp-edges` and `optimizeSpeed` draw with
hard nearest-pixel edges, any other recognized value (for example `auto` or `optimizeQuality`) smooths,
and an unrecognized value is ignored so the inherited value applies - and best-effort,
weight/style-aware `text` rendering - against a caller-supplied dictionary of per-family font faces
(and, for a family you do not register, installed fonts and the bundled Liberation fonts - see
*SvgCodec Default Fonts* below):
either a single `TrueTypeFont` per family (the legacy shape), or a list of `SvgFontFace` values -
each pairing a `TrueTypeFont` with the `font-weight`/`font-style` it represents - letting a caller
register distinct bold/italic variants of a family and have `SvgCodec` pick the closest-matching
face for each `text` element's own cascaded `font-weight`/`font-style`. The root
`viewBox`/`width`/`height` are fit into the requested raster via the root `svg` element's own
`preserveAspectRatio` attribute (`[defer] <align> [<meetOrSlice>]` - all 10 aligns and both
`meet`/`slice`), defaulting to "xMidYMid meet" (equivalent to CSS `object-fit: contain`) when the
attribute is absent - the centered-letterbox default every document without an
explicit `preserveAspectRatio` attribute already rendered with. This same fit algorithm also
applies to a `use` element referencing a `symbol` element with its own `viewBox`, and to a
`marker` element's own explicit `preserveAspectRatio` attribute. Every shape/text geometry
attribute (`x`/`y`/`width`/`height`/`rx`/`ry`/`cx`/`cy`/`r`/`x1`/`y1`/`x2`/`y2`/`font-size`/
`stroke-width`/`stroke-dasharray`/`stroke-dashoffset`/`use`'s `x`/`y`/`width`/`height`/`text`'s
`x`/`y`) also accepts a trailing `%`, resolved against the current viewport (or, for `font-size`,
the parent element's own `font-size`) - `stroke-miterlimit` is the sole exception, since it is a
unitless ratio rather than a length. A `style` element's text content is parsed as CSS, matching
type/class/id/universal/compound selectors (including comma-separated lists and
descendant/child combinators) against elements, with standard CSS specificity/cascade-order
resolution and a 3-tier precedence (presentation attribute, then any matching stylesheet rule,
then an inline `style="..."` attribute, which always wins). Well-formed but
out-of-scope constructs (`animate`/SMIL,
`foreignObject`, nested `svg`, and any remaining unrecognized filter primitive type)
are silently skipped/passed through so the rest of the document still
renders; malformed/unparseable input throws `InvalidDataException`.

Rendering is bounded by fixed resource budgets so that hostile input cannot consume unbounded
memory or CPU: a cap on document size (5,000,000 characters), element nesting depth (100) and
total rendered elements (100,000); cumulative geometry and CSS-matching work budgets; and, for
filters, a cap of 1,000 primitives per filter, a per-filter work budget (primitive count times
region area) and a cumulative per-`Load`-call filter work budget. A filter whose region is too
large, whose chain exceeds either budget, or that has no primitives is skipped and the element
renders unfiltered rather than throwing; exceeding the document, depth or element caps throws
`InvalidDataException`. The deterministic effect counters used to verify these budgets (work
charged, charges rejected, effects surfaces and pixels allocated) are internal to the library and
are not part of the public API.

#### SvgCodec Methods

##### SvgCodec.LoadWithFontFaces(Stream, ..., IReadOnlyDictionary&lt;string, IReadOnlyList&lt;SvgFontFace&gt;&gt;?)

```csharp
public static Surface LoadWithFontFaces(
    Stream stream,
    int width,
    int height,
    IReadOnlyDictionary<string, IReadOnlyList<SvgFontFace>>? fonts)
```

Decodes and rasterizes an SVG document from an open, readable stream into a new `width` x
`height` `Surface`, matching each `text` element's cascaded `font-family`/`font-weight`/
`font-style` against `fonts` - a dictionary mapping family names to the list of `SvgFontFace`
instances registered for that family. When a family has more than one registered face, the face
whose `Weight`/`Style` most closely matches the element's own cascaded `font-weight`/`font-style`
is selected: an exact style match always beats a style mismatch; among faces tied on style, the
smallest weight distance wins; among faces tied on both, the face on the same "boldness side"
(`>= 400` or `< 400`) as the request wins. A `null` value, a dictionary with no entry matching a
given `text` element's `font-family`, or a matching entry whose face list is empty, makes that
element use the default font instead (see *SvgCodec Default Fonts* below) - text is never silently
dropped.

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `ArgumentOutOfRangeException`: Thrown when `width` or `height` is not a valid `Surface` size (not
  pre-validated by `SvgCodec`; propagates from `new Surface(width, height)`).
- `InvalidDataException`: Thrown when the stream does not contain valid, supported SVG content.

##### SvgCodec.LoadWithFontFaces(string, ..., IReadOnlyDictionary&lt;string, IReadOnlyList&lt;SvgFontFace&gt;&gt;?)

```csharp
public static Surface LoadWithFontFaces(
    string path,
    int width,
    int height,
    IReadOnlyDictionary<string, IReadOnlyList<SvgFontFace>>? fonts)
```

Decodes and rasterizes an SVG file at the specified path, as
`LoadWithFontFaces(Stream, int, int, IReadOnlyDictionary<string, IReadOnlyList<SvgFontFace>>?)`.

**Exceptions:**

- `ArgumentNullException`: Thrown when `path` is null.
- `ArgumentException`: Thrown when `path` is an empty string.
- `ArgumentOutOfRangeException`: Thrown for the same reason as the stream overload above.
- `InvalidDataException`: Thrown for the same conditions as the stream overload above.

##### SvgCodec.Load(..., fonts, SvgLoadOptions) and SvgCodec.LoadWithFontFaces(..., fonts, SvgLoadOptions)

```csharp
public static Surface Load(Stream stream, int width, int height,
    IReadOnlyDictionary<string, TrueTypeFont>? fonts, SvgLoadOptions options)
public static Surface Load(string path, int width, int height,
    IReadOnlyDictionary<string, TrueTypeFont>? fonts, SvgLoadOptions options)
public static Surface LoadWithFontFaces(Stream stream, int width, int height,
    IReadOnlyDictionary<string, IReadOnlyList<SvgFontFace>>? fonts, SvgLoadOptions options)
public static Surface LoadWithFontFaces(string path, int width, int height,
    IReadOnlyDictionary<string, IReadOnlyList<SvgFontFace>>? fonts, SvgLoadOptions options)
```

The same as the overloads without `options`, with explicit load options. `SvgLoadOptions` has one
property, `UseSystemFonts` (default `true`); `new SvgLoadOptions()` behaves exactly like omitting the
argument. `options` must not be null (`ArgumentNullException`); the other exceptions are as for the
overloads above.

##### SvgCodec Default Fonts

A `text` element is drawn even when you register no font. For each `text` element the font is
chosen in this order:

1. **A font you registered** for a family in the element's `font-family` list (case-insensitive;
   the whole list is checked against your dictionary first, so a registered family anywhere in the
   list wins over a generic keyword).
2. **Otherwise, each family of the `font-family` list, in order:** a generic keyword is drawn with
   a bundled Liberation font - `sans-serif`, `system-ui`, `ui-sans-serif`, `cursive`, and
   `fantasy` with Liberation Sans; `serif` and `ui-serif` with Liberation Serif; `monospace` and
   `ui-monospace` with Liberation Mono - and any other name is looked up among the fonts installed
   on the machine, matching the family name exactly (ignoring case).
3. **Otherwise,** Liberation Sans.

Bold is chosen when the cascaded `font-weight` is 600 or more, and italic when `font-style` is
`italic` or `oblique`; the installed or bundled variant that matches is used. Installed fonts differ
between machines, so the same SVG can look different on different hosts. For output that is
identical everywhere, pass `new SvgLoadOptions { UseSystemFonts = false }`: installed fonts are then
never consulted and every text you did not register a font for uses the bundled Liberation fonts.

```csharp
// Reproducible: text is drawn with the bundled Liberation fonts on every machine
var surface = SvgCodec.Load("diagram.svg", 800, 600, fonts: null,
    new SvgLoadOptions { UseSystemFonts = false });
```

##### SvgCodec.Load(Stream stream, int width, int height, IReadOnlyDictionary&lt;string, TrueTypeFont&gt;? fonts = null)

```csharp
public static Surface Load(
    Stream stream,
    int width,
    int height,
    IReadOnlyDictionary<string, TrueTypeFont>? fonts = null)
```

Decodes and rasterizes an SVG document from an open, readable stream into a new `width` x
`height` `Surface`, using at most one `TrueTypeFont` per font-family. A thin wrapper over the
richer `SvgFontFace`-list overload above: each registered font is wrapped as a single
normal-weight (`400`)/normal-style face, so every `text` element always resolves to that single
registered font, regardless of its own `font-weight`/`font-style` - exactly as before the richer
overload existed. If `fonts` is supplied, `text` elements are rendered using the matching
`TrueTypeFont` keyed by family name; unmatched or missing fonts make that `text` element use the
default font (see *SvgCodec Default Fonts* above) rather than being skipped or throwing. Callers
registering more than one face per family (bold/
italic variants) should call the richer overload directly instead. Because the richer overload is
named `LoadWithFontFaces` rather than sharing the `Load` name, there is no ambiguity between the
two: `Load(stream, width, height, null)` always resolves to this legacy single-font-per-family
overload with no fonts registered, and `LoadWithFontFaces(stream, width, height, null)` always
resolves to the richer per-face overload with no fonts registered - each call is unambiguous
regardless of which overload the caller intends.

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `ArgumentOutOfRangeException`: Thrown when `width` or `height` is not a valid `Surface` size (not
  pre-validated by `SvgCodec`; propagates from `new Surface(width, height)`).
- `InvalidDataException`: Thrown when the stream does not contain valid, supported SVG content.

##### SvgCodec.Load(string path, int width, int height, IReadOnlyDictionary&lt;string, TrueTypeFont&gt;? fonts = null)

```csharp
public static Surface Load(
    string path,
    int width,
    int height,
    IReadOnlyDictionary<string, TrueTypeFont>? fonts = null)
```

Decodes and rasterizes an SVG file at the specified path, as `Load(Stream, int, int, ...)`.

**Exceptions:**

- `ArgumentNullException`: Thrown when `path` is null.
- `ArgumentException`: Thrown when `path` is an empty string.
- `ArgumentOutOfRangeException`: Thrown for the same reason as `Load(Stream, int, int, ...)`.
- `InvalidDataException`: Thrown for the same conditions as `Load(Stream, int, int, ...)`.

##### SvgCodec.GetInfo(Stream stream)

```csharp
public static ImageInfo GetInfo(Stream stream)
```

Parses only the SVG document's root `svg` start-tag and its own attributes (never reading into
the document body) and returns an `ImageInfo` describing its intrinsic size, without rasterizing
pixel data. Uses `viewBox` when present; otherwise falls back to `width`/`height` attributes;
otherwise falls back to the CSS/UA default replaced-element intrinsic size of 300x150. `Channels`
is always 4 and `HasAlpha` is always true.

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `InvalidDataException`: Thrown when the stream is not well-formed XML up to and including the
  root start-tag, its character count exceeds the document character cap, its root element is not
  named `svg`, or its `viewBox` attribute is present but malformed (not exactly four numbers, or
  non-positive width/height). A malformed or unparseable `width`/`height` attribute is **not**
  included in this list: such a value is treated as absent and falls back to the next sizing tier
  (ultimately the 300x150 default size) rather than throwing.

##### SvgCodec.GetInfo(string path)

```csharp
public static ImageInfo GetInfo(string path)
```

Parses an SVG file at the specified path and returns an `ImageInfo`.

**Exceptions:**

- `ArgumentNullException`: Thrown when `path` is null.
- `ArgumentException`: Thrown when `path` is an empty string.
- `InvalidDataException`: Thrown for the same conditions as `GetInfo(Stream)`.

When rendering a `.pptx` presentation via the separate `DemaConsulting.CanvasNet.Pptx` package's
`PptxDocument.Render`, a `<p:pic>` picture blip that declares only a Microsoft SVG extension
fallback (no raster `r:embed`/`r:link`, only `<a:extLst>/<a:ext>` wrapping an
`<asvg:svgBlip r:embed="...">`) is automatically resolved and rasterized via `SvgCodec` at the
referenced SVG's own intrinsic size - no extra caller code is required beyond calling
`PptxDocument.Render` itself.

### PdfDocument

`PdfDocument` is distributed via the separate `DemaConsulting.CanvasNet.Pdf` NuGet package
(namespace `DemaConsulting.CanvasNet.Pdf`), which references the core `DemaConsulting.CanvasNet`
package - see the Installation section of the project README.

The `PdfDocument` sealed class opens a PDF document, parses its cross-reference table/stream and
page tree, and reports each page's displayed (rotation-adjusted) size and its page count.
`Render` interprets a page's content stream, painting real path geometry, device color (`rg`/
`g`/`k`/`cs`/`sc` and related operators, including `/Pattern`-color-space shading and tiling
pattern fills/strokes - axial/radial/mesh (`/ShadingType 2`-`7`) shading patterns driven by
`/FunctionType 0` (including multi-input), `2`, `3` and `4` (PostScript calculator) functions, and
colored/uncolored (`/PaintType 1`/`2`) tiling patterns
rendering a repeating tile), placed image XObjects (`Do`), and text shown with a simple TrueType
font (`Tf`/`Td`/`Tj` and the other `BT`/`ET` text operators) onto the returned `Surface`. A font
with an embedded `/FontDescriptor/FontFile2` stream is always used directly;
a font with no embedded font data is instead automatically substituted, fully silently (no new
API, no "fallback occurred" indicator): first with the closest-matching font actually installed
on the host operating system (matched by family name and bold/italic/serif/fixed-pitch style),
and, when nothing on the host machine matches, with a bundled Liberation Sans/Serif/Mono font
that ships with the `DemaConsulting.CanvasNet` package and is therefore always available. A
recognized Standard-14 name (`Helvetica`, `Times-Roman`, `Courier`, and their bold/italic
variants, and so on) is classified by a fixed, built-in table; any other non-embedded font's
style is derived from its `/FontDescriptor` flags/weight/angle. `Symbol` and `ZapfDingbats`
(matched by exact `/BaseFont` name) instead resolve via a dedicated, bundled Noto substitute font:
`Symbol` tries `NotoSans-Regular.ttf` (Greek letters and general symbols), then
`NotoSansMath-Regular.ttf` (mathematical operators), then `NotoSansSymbols2-Regular.ttf`
(Private-Use-Area-adjacent/rare symbols), in that priority order; `ZapfDingbats` uses
`NotoSansSymbols2-Regular.ttf` alone - using the Symbol/ZapfDingbats built-in encoding (PDF
32000-1 Appendix D) rather than treating them like any other Latin-text font. This is a
documented, accepted fidelity limitation, not full glyph coverage: of Symbol's 163 distinct
mapped codepoints, 161 are covered (only U+2329/U+232A are not); of ZapfDingbats' 202 distinct
mapped codepoints, 158 are covered (the circled-digit Dingbats and a handful of others are not).
Any other font whose `/FontDescriptor` marks it as a symbolic, non-Latin-text glyph set is
never substituted this way, since a symbol/dingbat glyph set with no bundled substitute has no
meaningful generic-family
equivalent - it still fails closed with `UnsupportedImageFeatureException`, exactly as a
non-embedded font of any kind did before this fallback behavior existed. A shading pattern's
`/Extend` is honored per end (PDF 32000-1 §8.7.4.5.2): an extended end repeats its end color
beyond the defining geometry, while a non-extended end (the default) paints nothing past it;
stitching-function boundaries render as sharp transitions, and a mesh shading's `/Function` is
applied to the interpolated parametric value (through a 4096-entry color table whose entries are
each converted from the function output at their exact parameter, so nonlinear color spaces are
converted after evaluation). A function-less mesh in `/CalGray`, `/CalRGB` or `/Lab` interpolates the
vertex components in that space and converts per pixel; other mesh color spaces (including
`/DeviceCMYK`, `/Separation`, `/DeviceN`) convert each vertex first and blend the RGB result, an
approximation. Axial and radial shadings convert up to 256 samples exactly and blend only between
adjacent samples (within about 3 color levels of direct conversion for `/Lab`). A non-integral
`/N` of an `/ICCBased` space or `/Hival` of an `/Indexed` space is rejected as malformed. The
`sh` operator and generic path
clipping (`W`/`W*`) are supported. A clip made only of axis-aligned rectangles (including the union
of several) is applied geometrically, so a fill or image whose edge coincides with a clip edge keeps
its full ink; any other clip is applied as an antialiased coverage mask. Placed images are resampled
once to the device window they cover (area averaging when shrinking, bicubic when enlarging), so thin
features such as one-pixel rules are attenuated rather than dropped. Device, `/CalGray`, `/CalRGB`, `/Lab`, `/ICCBased`,
`/Indexed`, `/Separation`, `/DeviceN` and `/Pattern` color spaces are supported; `/CalGray`, `/CalRGB` and `/Lab`
color spaces are converted to
sRGB through a CIE XYZ pipeline. `/ICCBased` spaces are resolved by their `/N` component
count (or a matching `/Alternate`); the ICC profile data itself is not applied. An image in a `/Lab`
color space with an explicit `/Decode` array fails closed (feature `pdf-image-lab-decode`).
`/Separation` and `/DeviceN` colors (operators, images, `/Indexed` palettes and shadings) are
evaluated through their tint-transform function (types 0 including multi-input, 2, 3 and 4
PostScript calculator, under hard resource limits) and converted via the alternate space;
`/None` colorants paint nothing, and an alternate that is itself Pattern/Indexed/Separation/DeviceN
fails closed.
A multi-component `/DeviceN` image has a bounded per-image tint-evaluation budget; an image whose
distinct pixels would exceed it fails closed (feature `pdf-image-tint-budget`). ICC profile data is
never applied - only `/N`/`/Alternate` are used - and this is silent (no exception is thrown).
**Documented scope boundaries**: `/MMType1` fonts, function-based shadings (`/ShadingType 1`),
transparency groups,
and Type 3 glyphs shown under a clip text-rendering mode (`Tr 4`-`7`; clip modes with outline-based
fonts are supported) all fail closed with `UnsupportedImageFeatureException`
rather than being silently skipped or mis-rendered.

```csharp
using var doc = PdfDocument.Open("file.pdf");
var info = doc.GetPageInfo(0);
using var surface = doc.Render(0, info.Width, info.Height);
```

#### PdfDocument Methods

##### PdfDocument.Open(Stream stream, string? password = null)

```csharp
public static PdfDocument Open(Stream stream, string? password = null)
```

Reads the entirety of an open, readable stream into an in-memory buffer and parses it into a new
`PdfDocument`. Does not take ownership of, and does not dispose, the caller's `stream`.

The optional `password` parameter defaults to `null`, which preserves the library's original
empty-user-password-only behavior byte-for-byte. When a non-`null` password is supplied, it is
tried first as the **user password**, then - if that does not authenticate - as the **owner
password** (ISO 32000-1 Algorithm 3 for RC4/AES-128 documents; the owner-password variant of
ISO 32000-2 Algorithm 2.A for AES-256/R5/R6 documents). R2-R4 (RC4/AES-128) passwords are encoded as
Latin-1 (the ASCII range of PDFDocEncoding); R5/R6 (AES-256) passwords are encoded as UTF-8 with no
SASLprep/Unicode normalization applied - both are intentional scope boundaries. Both encodings
truncate the password's encoded bytes to a maximum of 127 bytes before any hashing.

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `InvalidDataException`: Thrown when the stream does not contain valid, supported PDF content
  (including when normal cross-reference parsing fails and the linear-scan fallback also cannot
  resolve the document catalog).
- `UnsupportedImageFeatureException`: Thrown when the document's trailer declares an `/Encrypt`
  entry that this library cannot open: a security handler other than the PDF "Standard" handler,
  an AES-256 document using an `/R` other than 5 or 6, a crypt filter other than
  the standard `/StdCF` filter (RC4, AES-128/`AESV2`, or AES-256-R5/R6/`AESV3`), a document that
  requires a password but `password` was not supplied (`null`), or a supplied `password` that
  does not authenticate as either the user or the owner password (feature
  `pdf-encrypted-incorrect-password`), or - for an R2-R4 document - contains a character outside
  ASCII 0-127 (feature `pdf-encrypted-password-non-ascii`). A document encrypted with the Standard
  security handler using RC4 (40-128 bit), AES-128, or AES-256 (R5/R6) and an empty user password -
  the vast majority of "owner password"/permission-restricted real-world PDFs - opens and renders
  normally with `password` left at its default `null`; its permission flags are not enforced (this
  library only ever reads for rendering, so copy/print restrictions do not apply).

##### PdfDocument.Open(string path, string? password = null)

```csharp
public static PdfDocument Open(string path, string? password = null)
```

Opens its own internal `FileStream` for the file at `path`, reads it fully, closes that stream
synchronously within `Open` (mirroring `PngCodec.Load(string)`'s open/consume/close pattern), then
parses the buffered content identically to the `Stream` overload above, with the same `password`
parameter and semantics.

**Exceptions:**

- `ArgumentNullException`: Thrown when `path` is null.
- `ArgumentException`: Thrown when `path` is an empty or whitespace-only string.
- `InvalidDataException`: Thrown for the same conditions as `Open(Stream, string?)`.
- `UnsupportedImageFeatureException`: Thrown for the same reasons as `Open(Stream, string?)`.

##### PdfDocument.PageCount

```csharp
public int PageCount { get; }
```

The total number of pages in the document's page tree.

**Exceptions:**

- `ObjectDisposedException`: Thrown when accessed after `Dispose()` has been called.

##### PdfDocument.GetPageInfo(int pageIndex)

```csharp
public PdfPageInfo GetPageInfo(int pageIndex)
```

Returns the specified page's displayed (rotation-adjusted) width/height, plus its normalized
clockwise rotation (0/90/180/270). Width and height are swapped relative to the page's raw
`/MediaBox` when rotation is 90 or 270. A leaf page inherits `/MediaBox`/`/Rotate` from its
page-tree ancestors when it does not declare its own.

**Exceptions:**

- `ArgumentOutOfRangeException`: Thrown when `pageIndex` is negative or `>= PageCount`.
- `ObjectDisposedException`: Thrown when called after `Dispose()` has been called.

##### PdfDocument.Render(int pageIndex, int width, int height, PdfRenderOptions? options = null)

```csharp
public Surface Render(int pageIndex, int width, int height, PdfRenderOptions? options = null)
```

Returns a new `Surface` of the caller-specified `width` x `height` for the given page, cleared to
`options.BackgroundColor` (opaque white by default, when `options` is `null`) and then painted
with the page's interpreted content-stream geometry (path construction/painting with real device
color, placed image XObjects, and text shown with a resolved TrueType font - embedded when
present, otherwise automatically substituted, see *PdfDocument* above). A page with no
`/Contents` simply returns the cleared, unpainted background surface. The `width`/`height` used
is exactly as given - it is not clamped or derived from the page's own `/MediaBox` size. To
reproduce the pre-`PdfRenderOptions` fully transparent background, pass
`new PdfRenderOptions { BackgroundColor = new(0, 0, 0, 0) }`.

**Exceptions:**

- `ArgumentOutOfRangeException`: Thrown when `pageIndex` is negative or `>= PageCount`, or when
  `width`/`height` is not a valid `Surface` size (propagates from `new Surface(width, height)`).
- `InvalidDataException`: Thrown for malformed `/Contents`, a malformed recognized operator's
  operand count/type, an unresolvable font resource name, or a text-showing operator invoked with
  no font selected.
- `UnsupportedImageFeatureException`: Thrown for a well-formed but unsupported color space,
  stream filter, font subtype (`/MMType1`; `/TrueType`, `/Type0`, `/Type1`, and `/Type3` are all
  supported), an otherwise-symbolic font (other than `Symbol`/`ZapfDingbats`, which resolve via a
  bundled Noto substitute instead) with no embedded font data, font encoding, or
  Type 3 glyph under a clip text-rendering mode (`pdf-text-render-mode-type3-clip`).
- `ObjectDisposedException`: Thrown when called after `Dispose()` has been called.

##### PdfDocument.Render(int pageIndex, float dpi, PdfRenderOptions? options = null)

```csharp
public Surface Render(int pageIndex, float dpi, PdfRenderOptions? options = null)
```

Convenience overload of `Render(int, int, int, PdfRenderOptions?)` for the common "render at a
given resolution" case: reads `GetPageInfo(pageIndex)`'s rotation-adjusted width/height (in
points, 1/72 inch), scales both by `dpi / 72`, rounds to the nearest pixel, and renders at that
size - preserving the page's own aspect ratio, unlike the four-argument overload, and forwarding
`options` unchanged. Use `Render(int, int, int, PdfRenderOptions?)` directly instead when
independent X/Y scaling (non-square pixels, or an exact pixel size regardless of aspect ratio) is
needed.

**Exceptions:**

- `ArgumentOutOfRangeException`: Thrown when `pageIndex` is negative or `>= PageCount`, when
  `dpi` is not a positive, finite number, or when the computed pixel width/height is not a valid
  `Surface` size.
- Also throws every exception `Render(int, int, int, PdfRenderOptions?)` itself can throw.

##### PdfDocument.Dispose()

```csharp
public void Dispose()
```

Releases the document's in-memory buffer and parsed state. Idempotent - safe to call more than
once. Every other public member throws `ObjectDisposedException` once called.

### VsdxDocument

`VsdxDocument` is distributed via the separate `DemaConsulting.CanvasNet.Vsdx` NuGet package
(namespace `DemaConsulting.CanvasNet.Vsdx`), which references the core `DemaConsulting.CanvasNet`
package - see the Installation section of the project README.

The `VsdxDocument` sealed class opens a Microsoft Visio (`.vsdx`) diagram document - an OPC (Open
Packaging Conventions) ZIP package - resolving `[Content_Types].xml` and its relationship graph to
locate `visio/document.xml` and `visio/pages/pages.xml`, and reports each page's declared name and
size and the document's page count. `Render` resolves the requested page's full shape tree (each
shape's effective cells, geometry, and style, merged with its referenced Master/MasterShape and
StyleSheet chain exactly as a native Visio client would) and walks it in document order (the first
declared shape paints first/bottom), painting each shape's resolved fill, stroke, text, and - for
a 1-D connector - its line and arrowheads onto the returned `Surface`. Master/MasterShape cell and
geometry-row inheritance, StyleSheet-chain-resolved line/fill/text style (including the built-in
color palette, resolved fill/line transparency, and bare `THEMEVAL()` themed color/font cells, which
resolve to the built-in "No Style" StyleSheet's values and the theme's minor Latin typeface), connector/
glue-point routing, arrowhead rendering, and arbitrarily nested shape groups are all resolved
automatically - no separate API is needed to opt into any of them. A `NonPrinting` shape skips its
own fill/stroke/text/arrowhead paint (but still recurses into its own children); a `HideText` shape
skips only its own text paint; and a resolved sub-pixel `LineWeight` is floored to a minimum
visible stroke width rather than vanishing entirely, mirroring Visio's own "always draw a
perceptible line" rendering convention. Constructs the renderer recognizes but cannot (fully)
render are never skipped silently: set `VsdxRenderOptions.UnsupportedFeatureHandler` to receive a
`VsdxUnsupportedFeature` (stable `Feature` token plus message) once per distinct feature per
`Render` call, or set `ThrowOnUnsupportedFeature` to get a `VsdxUnsupportedFeatureException`
instead (rendering is tolerant by default). **Documented scope boundaries**: embedded images/foreign
shapes, theme Quick Style variation matrices (a themed color that cannot be resolved is drawn neutral
gray and reported as `vsdx-themed-color`), non-solid fill-pattern combinations beyond solid
(including any gradient approximation), the full arrowhead style-index table, and a handful of
rarely-encountered geometry row types (`InfiniteLine`) are tolerated (the row/feature is skipped
and reported, never thrown) but not resolved into their own path segments or paint.
`PolylineTo`, `NURBSTo` and `SplineStart`/`SplineKnot` rows are drawn as flattened line segments
(bounded by hard limits on control points, degree and segments; a curve that cannot be evaluated
exactly is approximated by a polyline and reported as `vsdx-geometry-spline-approximated`). A
malformed geometry row is skipped, never thrown, and reported with the token
`vsdx-geometry-row-<Type>` (for example `vsdx-geometry-row-NURBSTo`): a `PolylineTo`, `NURBSTo` or
`SplineStart`/`SplineKnot` row with a missing, malformed, non-finite or over-limit cell or formula;
an orphan `SplineStart`/`SplineKnot`; a row that continues a subpath before any `MoveTo`; or an
unrecognized row type such as `InfiniteLine`. See `canvas-net-vsdx.md`'s Design Constraints
section for the complete deferred-feature boundary.

```csharp
using var doc = VsdxDocument.Open("diagram.vsdx");
var info = doc.GetPageSize(0);
using var surface = doc.Render(0, 150); // Render the first page at 150 DPI.

// Receive a diagnostic for every construct that was recognized but could not be fully rendered.
var options = new VsdxRenderOptions
{
    UnsupportedFeatureHandler = f => Console.WriteLine($"{f.Feature}: {f.Message}"),
};
using var reported = doc.Render(0, 150, options);
```

#### VsdxDocument Methods

##### VsdxDocument.Open(Stream stream)

```csharp
public static VsdxDocument Open(Stream stream)
```

Reads the entirety of an open, readable stream into an in-memory buffer and parses it into a new
`VsdxDocument`. Does not take ownership of, and does not dispose, the caller's `stream`.

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `InvalidDataException`: Thrown when the stream does not contain a valid, supported `.vsdx`
  package (not a readable ZIP archive, missing a required part, or a malformed part), or when
  `stream` supplies more than the package layer's maximum supported package size.

##### VsdxDocument.Open(string path)

```csharp
public static VsdxDocument Open(string path)
```

Opens its own internal `FileStream` for the file at `path`, reads it fully, closes that stream,
then parses the buffered content identically to the `Stream` overload above.

**Exceptions:**

- `ArgumentNullException`: Thrown when `path` is null.
- `ArgumentException`: Thrown when `path` is empty or consists only of white space.
- `InvalidDataException`: Thrown for the same conditions as `Open(Stream)`.

##### VsdxDocument.PageCount

```csharp
public int PageCount { get; }
```

The total number of pages declared by `visio/pages/pages.xml`'s page index.

**Exceptions:**

- `ObjectDisposedException`: Thrown when accessed after `Dispose()` has been called.

##### VsdxDocument.GetPageSize(int pageIndex)

```csharp
public VsdxPageInfo GetPageSize(int pageIndex)
```

Returns the requested page's declared name and size (converted from inches to EMU, English Metric
Units) as a `VsdxPageInfo`.

**Exceptions:**

- `ObjectDisposedException`: Thrown when this document has been disposed.
- `ArgumentOutOfRangeException`: Thrown when `pageIndex` is outside `[0, PageCount)`.

##### VsdxDocument.Render(int pageIndex, int width, int height, VsdxRenderOptions? options = null)

```csharp
public Surface Render(int pageIndex, int width, int height, VsdxRenderOptions? options = null)
```

Returns a new `Surface` of the caller-specified `width` x `height` for the given page, cleared to
`options.BackgroundColor` (opaque white by default, when `options` is `null`) and then painted
with the page's resolved shape tree (see *VsdxDocument* above). The `width`/`height` used is
exactly as given - it is not clamped or derived from the page's own declared size.

**Exceptions:**

- `ObjectDisposedException`: Thrown when this document has been disposed.
- `ArgumentOutOfRangeException`: Thrown when `pageIndex` is outside `[0, PageCount)`, or when
  `width`/`height` is not a valid `Surface` size.
- `InvalidDataException`: Thrown when the page's shape tree cannot be resolved (a malformed
  content part, or a group-nesting depth/resolved-shape-count budget exceeded).

##### VsdxDocument.Render(int pageIndex, int dpi, VsdxRenderOptions? options = null)

```csharp
public Surface Render(int pageIndex, int dpi, VsdxRenderOptions? options = null)
```

Convenience overload of `Render(int, int, int, VsdxRenderOptions?)` for the common "render at a
given resolution" case: reads `GetPageSize(pageIndex)`'s declared width/height (in inches), scales
both by `dpi`, rounds to the nearest pixel, and renders at that size - preserving the page's own
aspect ratio, unlike the four-argument overload. Deliberately accepts `dpi` as an `int`, not a
`float` - a documented deviation from `PptxDocument`'s own `float dpi` overload.

**Exceptions:**

- `ObjectDisposedException`: Thrown when this document has been disposed.
- `ArgumentOutOfRangeException`: Thrown when `dpi` is not greater than zero, when `pageIndex` is
  outside `[0, PageCount)`, or when the computed pixel width/height falls outside `Surface`'s own
  valid dimension range.
- `InvalidDataException`: Thrown for the same conditions as the pixel-dimension overload above.

##### VsdxDocument.Dispose()

```csharp
public void Dispose()
```

Releases the document's underlying ZIP archive and in-memory buffer. Idempotent - safe to call
more than once. Every other public member throws `ObjectDisposedException` once called.

### TrueTypeFont

The `TrueTypeFont` class loads glyph-based TrueType (`glyf`-based) SFNT fonts and CFF/OpenType
(`OTTO`-flavored, Type 2 charstring-based) fonts - including selecting an individual face out of
a TrueType Collection (`.ttc`) container - and exposes raw font-design-unit outlines, metrics,
advance widths, and basic pairwise kerning through one uniform API regardless of outline flavor.

```csharp
public sealed class TrueTypeFont
{
    public static TrueTypeFont Load(Stream stream);
    public static TrueTypeFont Load(string path);
    public static TrueTypeFont Load(Stream stream, int faceIndex);
    public static TrueTypeFont Load(string path, int faceIndex);
    public static int GetFaceCount(Stream stream);
    public static int GetFaceCount(string path);

    public int UnitsPerEm { get; }
    public int Ascender { get; }
    public int Descender { get; }
    public int LineGap { get; }
    public int GlyphCount { get; }
    public bool IsBold { get; }
    public bool IsItalic { get; }
    public bool IsFixedPitch { get; }

    public int GetGlyphIndex(int codepoint);
    public Path GetGlyphOutline(int glyphIndex);
    public int GetAdvanceWidth(int glyphIndex);
    public int GetKerning(int leftGlyphIndex, int rightGlyphIndex);
    public FontNameInfo GetNameInfo();
}
```

`IsBold`/`IsItalic`/`IsFixedPitch` report the font's derived bold/italic/fixed-pitch
classification, resolved from the `OS/2` table's `fsSelection`/`usWeightClass`, the `head` table's
`macStyle`, and the `post` table's `isFixedPitch` flag (whichever of these tables the font
provides). `GetNameInfo` resolves the font's `name`-table strings into a `FontNameInfo` - the
typographic (or, failing that, standard) family and subfamily names, the full name, and the
PostScript name - returning `null` for any field the font's `name` table does not provide, rather
than throwing:

```csharp
public readonly record struct FontNameInfo(
    string? FamilyName,
    string? SubfamilyName,
    string? FullName,
    string? PostScriptName);
```

`GetGlyphOutline` returns `Geometry.Path` in raw font-design-unit coordinates with Y increasing
upward, per the TrueType convention. Callers typically scale that path by the desired point size
and flip Y before rendering it through `PathFiller` or `PathStroker`. A glyf-flavored font's
outline uses `LineTo`/`QuadraticBezierTo` segments; a CFF/OpenType font's outline uses
`LineTo`/`CubicBezierTo` segments instead - callers that support both must handle both command
kinds (see Example 13 below).

```csharp
var font = TrueTypeFont.Load("font.ttf");   // also accepts a CFF/OpenType .otf file
var glyphIndex = font.GetGlyphIndex('A');
var outline = font.GetGlyphOutline(glyphIndex);
var advanceWidth = font.GetAdvanceWidth(glyphIndex);
```

`Load(Stream, int)` / `Load(string, int)` select a specific zero-based face out of a `.ttc`
(TrueType Collection) container; `GetFaceCount` reports how many faces a file contains (`1` for
an ordinary single-face font) without parsing any face's own table directory:

```csharp
var faceCount = TrueTypeFont.GetFaceCount("collection.ttc"); // e.g. 2
var secondFace = TrueTypeFont.Load("collection.ttc", faceIndex: 1);
```

`Load(Stream)`/`Load(string)` (without an explicit `faceIndex`) transparently default to face 0
when given a `.ttc` file, so existing callers that only ever loaded single-face fonts continue to
work unchanged against the first face of a collection.

**Exceptions:**

- `ArgumentNullException`: Thrown when `Load`/`GetFaceCount` receives a null stream or path.
- `ArgumentException`: Thrown when the `string`-path overload of `Load`/`GetFaceCount` receives an
  empty path.
- `InvalidDataException`: Thrown when the font data is malformed, truncated, or unsupported
  (including CFF-specific failures such as CID-keyed CFF data, and `.ttc`-specific failures such
  as a malformed collection header).
- `ArgumentOutOfRangeException`: Thrown when `GetGlyphOutline` or `GetAdvanceWidth` receives an
  out-of-range glyph index, or when `Load(Stream, int)`/`Load(string, int)` receives a `faceIndex`
  that is negative or not less than the file's own face count.

### SystemFontCatalog

The `SystemFontCatalog` static class discovers fonts installed on the host operating system,
best-effort matches a requested family-name hint and style (bold/italic/serif/fixed-pitch) against
that discovered catalog, and - when no suitable system font can be found - provides a bundled
Liberation Sans/Serif/Mono `TrueTypeFont` as a last-resort fallback.

```csharp
public static class SystemFontCatalog
{
    public static IReadOnlyList<SystemFontInfo> Fonts { get; }

    public static SystemFontInfo? FindBestMatch(
        string familyNameHint,
        bool bold,
        bool italic,
        bool serif,
        bool fixedPitch);

    public static TrueTypeFont LoadBundledFallback(bool serif, bool fixedPitch, bool bold, bool italic);
}
```

`Fonts` lazily enumerates the host operating system's installed fonts (platform-specific font
directories/registries) on first access, caching the result for the process lifetime; each entry
is a `SystemFontInfo` describing the font's family/subfamily name, derived bold/italic/fixed-pitch
style, and the file path/face index needed to load it via `TrueTypeFont.Load`:

```csharp
public readonly record struct SystemFontInfo(
    string FamilyName,
    string SubfamilyName,
    bool Bold,
    bool Italic,
    bool FixedPitch,
    string FilePath,
    int FaceIndex);
```

`FindBestMatch` follows a two-tier strategy: it first looks for an exact (case-insensitive)
family-name match against `Fonts`; if none exists, it falls back to searching a small,
hand-maintained list of well-known generic family names (sans-serif, serif, or monospace,
selected by the `serif`/`fixedPitch` hints) for the first one actually present on the host system.
It returns `null` when neither tier finds a match.

`LoadBundledFallback` selects one of the library's twelve embedded Liberation Sans/Serif/Mono
TrueType fonts (by the requested `serif`/`fixedPitch`/`bold`/`italic` combination) and loads it via
`TrueTypeFont.Load`, guaranteeing a usable font is always available even when the host system has
no suitable installed font (or no accessible font directory at all):

```csharp
// Best-effort match against installed system fonts, falling back to a bundled font
var match = SystemFontCatalog.FindBestMatch("Helvetica", bold: false, italic: false, serif: false, fixedPitch: false);
var font = match is { } info
    ? TrueTypeFont.Load(info.FilePath, info.FaceIndex)
    : SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold: false, italic: false);
```

### PathFiller

The `PathFiller` static class fills a closed `Geometry.Path` with a solid color onto a `Surface`,
using an antialiased scanline-coverage rasterizer. It supports both `FillRule.NonZero` (the
default) and `FillRule.EvenOdd` winding resolution, correctly renders holes via nested,
counter-wound subpaths, and treats every subpath as implicitly closed for fill purposes,
regardless of whether the path explicitly called `Close`.

#### PathFiller Methods

##### PathFiller.Fill(Surface surface, Path path, Rgba32 color, FillRule fillRule, float flattenTolerance)

```csharp
public static void Fill(
    Surface surface,
    Path path,
    Rgba32 color,
    FillRule fillRule = FillRule.NonZero,
    float flattenTolerance = 0.25f)
```

Fills `path` with the solid `color` onto `surface`. Curves and arcs are flattened to line
segments within `flattenTolerance` before rasterization (see `BezierFlattening`). No-ops, without
throwing, if `path` is empty or its bounds do not intersect `surface`'s pixel extent.

**Exceptions:**

- `ArgumentNullException`: Thrown when `surface` or `path` is null.
- `ArgumentOutOfRangeException`: Thrown when `fillRule` is not a defined `FillRule` value, when
  `flattenTolerance` is less than or equal to zero, or is not a finite value (NaN or infinity).

##### PathFiller.Fill(Surface surface, Path path, Gradient paint, FillRule fillRule, float flattenTolerance)

```csharp
public static void Fill(
    Surface surface,
    Path path,
    Gradient paint,
    FillRule fillRule = FillRule.NonZero,
    float flattenTolerance = 0.25f)
```

Fills `path` with `paint` (a `LinearGradient` or `RadialGradient`) onto `surface`, evaluating the
gradient once per pixel and scaling the result by that pixel's antialiased coverage. Shares
curve-flattening, clip-bounds, argument validation, `FillRule`, and empty/out-of-bounds no-op
behavior with the solid-color overload above - see `GradientPaint` below for gradient-specific
behavior.

**Exceptions:**

- `ArgumentNullException`: Thrown when `surface`, `path`, or `paint` is null.
- `ArgumentOutOfRangeException`: Thrown when `fillRule` is not a defined `FillRule` value, when
  `flattenTolerance` is less than or equal to zero, or is not a finite value (NaN or infinity).

##### PathFiller.Fill(Surface surface, Path path, TilePaint paint, FillRule fillRule, float flattenTolerance)

```csharp
public static void Fill(
    Surface surface,
    Path path,
    TilePaint paint,
    FillRule fillRule = FillRule.NonZero,
    float flattenTolerance = 0.25f)
```

Fills `path` with `paint` (a repeating tile bitmap) onto `surface`, sampling the tile once per
pixel at its wrapped-around pattern-space offset and scaling the result by that pixel's
antialiased coverage. Shares curve-flattening, clip-bounds, argument validation, `FillRule`, and
empty/out-of-bounds no-op behavior with the solid-color overload above - see `TilePaint` below for
tile-specific behavior.

**Exceptions:**

- `ArgumentNullException`: Thrown when `surface`, `path`, or `paint` is null.
- `ArgumentOutOfRangeException`: Thrown when `fillRule` is not a defined `FillRule` value, when
  `flattenTolerance` is less than or equal to zero, or is not a finite value (NaN or infinity).

### PathStroker

The `PathStroker` static class converts a `Geometry.Path` centerline into a new closed `Path`
describing the stroked area. Callers render the returned outline path through `PathFiller.Fill`,
reusing the same antialiased rasterizer as ordinary fills. Open subpaths honor end caps; closed
subpaths become shell rings suitable for `FillRule.NonZero`.

#### LineCap

```csharp
public enum LineCap
{
    Butt,
    Round,
    Square
}
```

Selects the visible end shape for an open stroked segment: no extension (`Butt`), semicircular
ends (`Round`), or half-width square extension (`Square`).

#### LineJoin

```csharp
public enum LineJoin
{
    Miter,
    Round,
    Bevel
}
```

Selects the visible corner shape where consecutive stroked segments meet: a sharp miter (with
limit-based bevel fallback), a rounded corner, or a flat bevel.

#### StrokeStyle

```csharp
public sealed class StrokeStyle
{
    public float Width { get; }
    public LineCap Cap { get; }
    public LineJoin Join { get; }
    public float MiterLimit { get; }
    public IReadOnlyList<float>? DashArray { get; }
    public float DashOffset { get; }
}
```

Immutable public stroke-style snapshot supplying width, cap, join, miter limit, optional dash
array, and optional dash offset. A null or empty dash array means a solid stroke.

##### StrokeStyle Constructor

```csharp
public StrokeStyle(
    float width,
    LineCap cap = LineCap.Butt,
    LineJoin join = LineJoin.Miter,
    float miterLimit = 4f,
    IReadOnlyList<float>? dashArray = null,
    float dashOffset = 0f)
```

**Exceptions:**

- `ArgumentOutOfRangeException`: Thrown when `width` is less than or equal to zero or non-finite,
  when `cap` or `join` is not a defined enum value, when `miterLimit` is less than 1 or
  non-finite, or when `dashOffset` is non-finite.
- `ArgumentException`: Thrown when `dashArray` contains a negative or non-finite entry, or when
  every entry is zero.

#### PathStroker Methods

##### PathStroker.Stroke(Path path, StrokeStyle style, float flattenTolerance)

```csharp
public static Path Stroke(
    Path path,
    StrokeStyle style,
    float flattenTolerance = 0.25f)
```

Converts `path` into a new closed-outline `Path` representing the requested stroke. Returns
`Path.Empty` when the stroke contributes no visible area.

**Exceptions:**

- `ArgumentNullException`: Thrown when `path` or `style` is null.
- `ArgumentOutOfRangeException`: Thrown when `flattenTolerance` is less than or equal to zero, or
  is not a finite value (NaN or infinity).

### GradientPaint

`GradientPaint` is the set of public `Gradient`/`LinearGradient`/`RadialGradient`/`GradientStop`/
`GradientSpread` types, in the `Drawing` namespace, that let a caller paint a filled path with a
smoothly (or sharply) varying color ramp via `PathFiller.Fill(Surface, Path, Gradient, FillRule,
float)` (see `PathFiller` above), instead of a single solid color.

#### GradientSpread

```csharp
public enum GradientSpread
{
    Pad,
    Reflect,
    Repeat
}
```

Selects how the ramp behaves beyond its own `[0, 1]` extent: clamping to the nearest endpoint
color (`Pad`), reflecting the ramp back and forth in a triangle wave (`Reflect`), or repeating the
ramp periodically (`Repeat`).

#### GradientStop

```csharp
public readonly struct GradientStop
{
    public float Offset { get; }
    public Rgba32 Color { get; }
}
```

Associates a `Color` with a position (`Offset`, in `[0, 1]`) along a gradient's ramp.

##### GradientStop Constructor

```csharp
public GradientStop(float offset, Rgba32 color)
```

**Exceptions:**

- `ArgumentOutOfRangeException`: Thrown when `offset` is outside `[0, 1]`, or is not a finite
  value (NaN or infinity).

#### Gradient

```csharp
public abstract class Gradient
{
    public IReadOnlyList<GradientStop> Stops { get; }
    public GradientSpread Spread { get; }
    public Matrix3x2 Transform { get; }
}
```

The abstract base type shared by `LinearGradient` and `RadialGradient` (the only two types
permitted to derive from it). `Stops` is a defensive, stable-sorted-ascending-by-offset copy of
the stops supplied to the subtype constructor - stability preserves caller-supplied relative
order among stops that share the same offset, which is what makes a "hard stop" (a sharp color
step) well-defined. `Transform` maps the gradient's own defining coordinates into the same
coordinate space as the filled `Path`; it does not have to be invertible - see `RadialGradient`
and `LinearGradient` below for how a non-invertible transform is resolved when painting a fill.

#### LinearGradient

```csharp
public sealed class LinearGradient : Gradient
{
    public Vector2 Start { get; }
    public Vector2 End { get; }
}
```

A gradient whose color varies linearly along the vector from `Start` to `End`, reaching the first
stop's color at `Start` and the last stop's color at `End`.

##### LinearGradient Constructor

```csharp
public LinearGradient(
    Vector2 start,
    Vector2 end,
    IReadOnlyList<GradientStop> stops,
    GradientSpread spread = GradientSpread.Pad,
    Matrix3x2? transform = null)
```

`start` equal to `end` (a zero-length gradient vector) is accepted - this degenerate case
flat-fills with the last stop's color when painting a fill. Omitting `transform` (or passing
`null`) is equivalent to supplying `Matrix3x2.Identity`; an explicitly-supplied `Matrix3x2` value -
including the all-zero `default(Matrix3x2)` matrix, a legitimate (if singular) transform - is
preserved exactly as given.

**Exceptions:**

- `ArgumentNullException`: Thrown when `stops` is null.
- `ArgumentException`: Thrown when `stops` is empty.
- `ArgumentOutOfRangeException`: Thrown when `start` or `end` has a non-finite component, when
  `spread` is not a defined `GradientSpread` value, or when any component of `transform` is not
  finite.

#### RadialGradient

```csharp
public sealed class RadialGradient : Gradient
{
    public Vector2 StartCenter { get; }
    public float StartRadius { get; }
    public Vector2 EndCenter { get; }
    public float EndRadius { get; }
}
```

A gradient whose color varies radially between two independently positioned and sized circles -
the general "two-circle" model used by SVG/CSS radial gradients - reaching the first stop's color
on the start circle (`StartCenter`/`StartRadius`) and the last stop's color on the end circle
(`EndCenter`/`EndRadius`). This general model subsumes both the simpler single-circle case
(`StartRadius` zero, `StartCenter` equal to `EndCenter`) and the "focal point" case (`StartRadius`
zero, `StartCenter` different from `EndCenter`), without needing a second public gradient type.

##### RadialGradient Constructor

```csharp
public RadialGradient(
    Vector2 startCenter,
    float startRadius,
    Vector2 endCenter,
    float endRadius,
    IReadOnlyList<GradientStop> stops,
    GradientSpread spread = GradientSpread.Pad,
    Matrix3x2? transform = null)
```

A point outside every circle the two-circle family sweeps through (when the two circles do not
overlap or contain one another) is left unpainted (fully transparent) rather than resolved to any
stop's color. A start and end circle sharing the same center and the same radius (whether that
shared radius is zero or a nonzero value) flat-fills with the last stop's color, the same
degenerate policy as `LinearGradient`'s zero-length vector. As with `LinearGradient`, omitting
`transform` (or passing `null`) is equivalent to supplying `Matrix3x2.Identity`, while an
explicitly-supplied value - including the all-zero matrix - is preserved exactly as given.

**Exceptions:**

- `ArgumentNullException`: Thrown when `stops` is null.
- `ArgumentException`: Thrown when `stops` is empty.
- `ArgumentOutOfRangeException`: Thrown when `startCenter`/`endCenter` has a non-finite component;
  when `startRadius`/`endRadius` is not finite or is negative; when `spread` is not a defined
  `GradientSpread` value; or when any component of `transform` is not finite.

### TilePaint

`TilePaint` is the tiling-pattern analogue of `GradientPaint`: a resolved, renderable tile paint
pairing a pre-rendered one-cell tile bitmap (a `Surface`) with a `Transform` mapping the tile's
own pattern-space coordinates into the same coordinate space a filled `Path` already uses, and an
`XStep`/`YStep` pitch describing how far apart successive tile repetitions are spaced in
pattern-space units. A caller paints a filled path with a repeating tile via
`PathFiller.Fill(Surface, Path, TilePaint, FillRule, float)` above.

```csharp
public sealed class TilePaint
{
    public TilePaint(Surface surface, Matrix3x2 transform, float xStep, float yStep);

    public Surface Surface { get; }
    public Matrix3x2 Transform { get; }
    public float XStep { get; }
    public float YStep { get; }

    public TilePaint WithTransform(Matrix3x2 transform);
}
```

`TilePaint` does not own `Surface`'s lifetime: it does not dispose it, and the caller that
constructed the `TilePaint` remains responsible for disposing the underlying `Surface` once it is
no longer needed - mirroring how `Gradient` similarly owns no disposable resource of its own.
`XStep`/`YStep` may be negative (a legitimate PDF tiling-pattern value meaning the tile repeats in
the negative pattern-space axis direction) - only zero or a non-finite value is rejected.
`WithTransform` returns a new `TilePaint` with the same `Surface`/`XStep`/`YStep` whose `Transform`
is this tile paint's own `Transform` composed with the supplied transform (this tile paint's
existing `Transform` is applied first, then the supplied transform is applied on top of that) -
the same row-vector composition convention as `Gradient.WithTransform`.

**Exceptions:**

- `ArgumentNullException`: Thrown by the constructor when `surface` is null.
- `ArgumentOutOfRangeException`: Thrown by the constructor when any component of `transform` is
  not finite, or when `xStep`/`yStep` is not finite or is zero; thrown by `WithTransform` when the
  composed transform has a non-finite component.

### Chart

`Chart` and its companion types (`ChartSeries`, `ChartAxis`, `ChartLegend`, `ChartTitle`,
`ChartType`, `ChartLegendPosition`) are distributed via the separate
`DemaConsulting.CanvasNet.Charts` NuGet package (namespace `DemaConsulting.CanvasNet.Charts`),
which references the core `DemaConsulting.CanvasNet` package - see the Installation section of
the project README.

`Chart` is an immutable, fully validated description of a chart: its `ChartType`, one or more
`ChartSeries`, and optional `CategoryAxis`/`ValueAxis` (both `ChartAxis`), `Legend`
(`ChartLegend`), `Title` (`ChartTitle`), and default `ColorPalette`. Every constructor argument is
validated eagerly and every caller-supplied collection is defensively copied, so a constructed
`Chart` can be trusted by a renderer without re-validation.

```csharp
public enum ChartType { Bar, Column, Line, Pie, Doughnut, Area }

public enum ChartLegendPosition { Top, Bottom, Left, Right, None }

public sealed class ChartSeries
{
    public ChartSeries(
        string name,
        IReadOnlyList<double> values,
        IReadOnlyList<Rgba32>? pointColors = null,
        IReadOnlyList<string>? pointLabels = null,
        Rgba32? color = null);

    public string Name { get; }
    public IReadOnlyList<double> Values { get; }
    public IReadOnlyList<Rgba32>? PointColors { get; }
    public IReadOnlyList<string>? PointLabels { get; }
    public Rgba32? Color { get; }
}

public sealed class ChartAxis
{
    public ChartAxis(
        IReadOnlyList<string>? labels = null,
        float? minimum = null,
        float? maximum = null,
        float? tickInterval = null,
        string? title = null);

    public IReadOnlyList<string>? Labels { get; }
    public float? Minimum { get; }
    public float? Maximum { get; }
    public float? TickInterval { get; }
    public string? Title { get; }
}

public sealed class ChartLegend
{
    public ChartLegend(ChartLegendPosition position = ChartLegendPosition.Right, bool isVisible = true);

    public ChartLegendPosition Position { get; }
    public bool IsVisible { get; }
}

public sealed class ChartTitle
{
    public ChartTitle(string text, float? fontSize = null);

    public string Text { get; }
    public float? FontSize { get; }
}

public sealed class Chart
{
    public Chart(
        ChartType type,
        IReadOnlyList<ChartSeries> series,
        ChartAxis? categoryAxis = null,
        ChartAxis? valueAxis = null,
        ChartLegend? legend = null,
        ChartTitle? title = null,
        IReadOnlyList<Rgba32>? colorPalette = null);

    public ChartType Type { get; }
    public IReadOnlyList<ChartSeries> Series { get; }
    public ChartAxis? CategoryAxis { get; }
    public ChartAxis? ValueAxis { get; }
    public ChartLegend? Legend { get; }
    public ChartTitle? Title { get; }
    public IReadOnlyList<Rgba32>? ColorPalette { get; }
}
```

For `Bar`, `Column`, `Line`, and `Area` (the category-based chart kinds), when `CategoryAxis` has
non-null `Labels`, every series' `Values.Count` must exactly equal that label count - `Pie` and
`Doughnut` are exempt, since their single series' values represent proportional wedge shares
rather than per-category values.

**Exceptions:**

- `ArgumentNullException`: Thrown when a required reference-type argument (`name`/`values` on
  `ChartSeries`; `text` on `ChartTitle`; `series` on `Chart`) is null.
- `ArgumentException`: Thrown for a blank name/title/text, an empty or null-containing values/
  series/labels collection, a non-finite values entry, a per-point color/label collection whose
  count does not match `Values.Count`, or a series/category-axis label count mismatch.
- `ArgumentOutOfRangeException`: Thrown for an undefined `ChartType`/`ChartLegendPosition` value,
  a non-finite axis minimum/maximum/font size, a tick interval that is not finite or not positive,
  or a minimum not strictly less than a supplied maximum.

### ChartBuilder

`ChartBuilder` is a fluent, mutable-until-`Build` API for assembling a `Chart` without
constructing every nested type by hand. It performs no independent data-shape validation of its
own beyond `Build()` itself: every other invalid argument surfaces the identical exception the
corresponding model type's constructor would throw.

```csharp
public sealed class ChartBuilder
{
    public ChartBuilder OfType(ChartType type);
    public ChartBuilder AddSeries(ChartSeries series);
    public ChartBuilder AddSeries(
        string name,
        IReadOnlyList<double> values,
        IReadOnlyList<Rgba32>? pointColors = null,
        IReadOnlyList<string>? pointLabels = null,
        Rgba32? color = null);
    public ChartBuilder WithCategoryAxis(ChartAxis axis);
    public ChartBuilder WithCategoryAxis(IReadOnlyList<string> labels, string? title = null);
    public ChartBuilder WithValueAxis(ChartAxis axis);
    public ChartBuilder WithValueAxis(
        float? minimum = null, float? maximum = null, float? tickInterval = null, string? title = null);
    public ChartBuilder WithLegend(ChartLegend legend);
    public ChartBuilder WithLegend(ChartLegendPosition position = ChartLegendPosition.Right, bool isVisible = true);
    public ChartBuilder WithTitle(string text, float? fontSize = null);
    public ChartBuilder WithColorPalette(IReadOnlyList<Rgba32> colorPalette);

    public Chart Build();
}
```

Every chainable method returns the same `ChartBuilder` instance (`this`), enabling fluent method
chaining; `AddSeries` preserves insertion order across multiple calls.

**Exceptions:**

- `InvalidOperationException`: Thrown by `Build()` when `OfType` was never called, or when no
  series was added via `AddSeries`.
- Every other exception documented above for the corresponding model type's own constructor.

### ChartRenderer

`ChartRenderer` is distributed in the same `DemaConsulting.CanvasNet.Charts` package as `Chart`.
It is a public static entry point that paints a `Chart` onto a new core `CanvasNet`
`Canvas.Surface`, dispatching per `ChartType` to paint bars/columns/lines/areas/pie or doughnut
wedges, plus axes (for the category-based chart kinds), a legend (at any `ChartLegendPosition`),
a title, and opt-in per-point data labels (`ChartSeries.PointLabels`).

```csharp
public static class ChartRenderer
{
    public static Surface Render(Chart chart, int width, int height, ChartRenderOptions? options = null);

    public static Surface Render(
        Chart chart,
        float widthInches,
        float heightInches,
        float dpi,
        ChartRenderOptions? options = null);
}
```

The first overload renders directly to a caller-specified pixel size. The second computes pixel
dimensions as `round(widthInches * dpi)`/`round(heightInches * dpi)` and delegates to the first -
a `Chart` carries no intrinsic physical page size of its own to scale from (unlike a PDF page or
PPTX slide), so this overload exists purely for callers who prefer to reason in physical
units/DPI the same way `PdfDocument.Render(int, float, PdfRenderOptions?)` does.

A caller with no explicit series/point colors, color palette, or render options still receives a
fully deterministic, legible rendering: `ChartRenderer` resolves each series'/point's color, when
unspecified, from `Chart.ColorPalette`, then `ChartRenderOptions.ColorPalette`, then
`ChartColorPalette.Default` (a documented, fixed 10-entry categorical palette), wrapping by index
via modulo; text is drawn with the bundled Liberation Sans Regular fallback font unless
`ChartRenderOptions.Font` overrides it. A render target too small to reserve a title/legend/axis
band simply skips that band rather than throwing; a legend with more entries than fit its band
omits the entries that do not fit; an over-long category/legend label is truncated to fit.

**Exceptions:**

- `ArgumentNullException`: Thrown when `chart` is null.
- `ArgumentOutOfRangeException`: Thrown when `width`/`height` is outside `Surface`'s own supported
  range (propagated from `Surface`'s own constructor); thrown by the DPI overload when
  `widthInches`/`heightInches`/`dpi` is not finite or not positive, or when the computed pixel
  width/height would exceed `Surface.MaxDimension`.

### ChartRenderOptions

`ChartRenderOptions` groups every optional rendering setting `ChartRenderer.Render` accepts, as a
single, extensible options parameter - mirroring `PptxRenderOptions`'s own init-property/
`Default`-singleton shape.

```csharp
public sealed class ChartRenderOptions
{
    public static readonly ChartRenderOptions Default;

    public Rgba32 BackgroundColor { get; init; } // default: opaque white
    public TrueTypeFont? Font { get; init; } // default: null (bundled fallback font)
    public float TitleFontSize { get; init; } // default: 18
    public float AxisFontSize { get; init; } // default: 11
    public float LegendFontSize { get; init; } // default: 11
    public float DataLabelFontSize { get; init; } // default: 10
    public IReadOnlyList<Rgba32>? ColorPalette { get; init; } // default: null
}
```

Passing `null` (or omitting the `options` parameter entirely) to either `ChartRenderer.Render`
overload uses `ChartRenderOptions.Default`.

### OpenXmlChartParser

`OpenXmlChartParser` is distributed in the same `DemaConsulting.CanvasNet.Charts` package as
`Chart`, in the `DemaConsulting.CanvasNet.Charts.OpenXml` namespace. It is a public static entry
point that parses a raw ECMA-376 DrawingML-Charts `c:chartSpace` (or bare `c:chart`)
`System.Xml.Linq` element into a validated `Chart`, directly consumable by `ChartRenderer.Render`
with no further adaptation. It is format-agnostic: it has no knowledge of OPC/ZIP packaging or
any host document format (for example, PresentationML/`.pptx`) - a caller is responsible for
first locating and opening the relevant `chart#.xml` part and handing this parser only the
resulting XML content.

```csharp
public static class OpenXmlChartParser
{
    public static Chart Parse(XDocument chartDocument);
    public static Chart Parse(XElement chartSpaceOrChartElement);
}
```

`OpenXmlChartParser` reads only cached values (`c:numCache`/`c:strCache`) - it never recomputes a
value from a sibling `c:f` formula - and supports `c:barChart` (bar/column, classified by
`c:barDir`), `c:lineChart`, `c:pieChart`, `c:doughnutChart`, and `c:areaChart`, extracting series
names/categories/values, the chart title, axis titles/range, and legend presence/position.

#### Exceptions

- `ArgumentNullException`: Thrown when `chartDocument`/`chartSpaceOrChartElement` is null.
- `ArgumentException`: Thrown for a document with no root element, an element that is neither a
  `c:chart` element nor one containing one, or a `c:chart` with no `c:plotArea` child.
- `ChartUnsupportedFeatureException`: Thrown - carrying a short, stable `Feature` token - for a
  recognized-but-unimplemented chart kind (radar, bubble, scatter, stock, surface, 3-D, or "of
  pie"), a combo chart (more than one recognized chart-type element in one plot area), a plot
  area with no recognized chart-type element at all, or a series whose value has no cached
  `c:numCache`.

```csharp
public class ChartUnsupportedFeatureException : IOException
{
    public ChartUnsupportedFeatureException();
    public ChartUnsupportedFeatureException(string message);
    public ChartUnsupportedFeatureException(string message, Exception innerException);
    public ChartUnsupportedFeatureException(string feature, string message);
    public ChartUnsupportedFeatureException(string feature, string message, Exception innerException);

    public string Feature { get; } // default: string.Empty
}
```

When rendering a `.pptx` presentation via the separate `DemaConsulting.CanvasNet.Pptx` package's
`PptxDocument.Render`, an embedded `<p:graphicFrame>` chart is automatically located, parsed via
`OpenXmlChartParser`, rendered via `ChartRenderer`, and composited onto the slide - no extra
caller code is required beyond calling `PptxDocument.Render` itself.

### PptxDocument

`PptxDocument` is distributed via the separate `DemaConsulting.CanvasNet.Pptx` NuGet package
(namespace `DemaConsulting.CanvasNet.Pptx`), which references the core `DemaConsulting.CanvasNet`
package - see the Installation section of the project README.

The `PptxDocument` sealed class opens a PowerPoint (`.pptx`) presentation - an OPC (Open Packaging
Conventions) ZIP package - and rasterizes individual slides onto a `Surface`. Opening buffers the
whole package in memory (up to 256 MiB), so the caller's stream need not stay open. A slide is
rendered by walking its shape tree in document order, including nested groups, and painting:
auto shapes (preset and custom geometry) with solid, linear-gradient and covered pattern fills and
outlines; connectors (straight, elbow and curved, with arrowheads); raster pictures (BMP, PNG, TIFF,
JPEG and GIF, with `srcRect` cropping) and SVG-only picture blips; tables; embedded charts; and text
with placeholder, layout, master and theme inheritance, word wrap, alignment, vertical anchoring,
autofit, bullets and numbering. The slide's own background fill (falling back to its layout's, then
its master's) is painted before the shapes. Footer, date and slide-number placeholders inherit from
the master like any other placeholder, and a master's non-placeholder date or slide-number text box
honors its own list style, so its size is not replaced by the 18 pt default. `PptxDocument` is not
thread-safe: open one instance per thread, or synchronize access externally.

```csharp
using DemaConsulting.CanvasNet.Pptx;

using var deck = PptxDocument.Open("slides.pptx");
Console.WriteLine($"{deck.SlideCount} slides, {deck.SlideSize.WidthEmu}x{deck.SlideSize.HeightEmu} EMU");

// Render the first slide at 96 DPI (the surface size follows the slide's aspect ratio).
using var slide = deck.Render(0, 96f);
var (width, height) = deck.GetSlideSizeInPixels(96f);
Console.WriteLine($"{width}x{height}"); // Output: the pixel size of the surface returned above
```

#### PptxDocument Members

- `PptxDocument.Open(Stream stream)` / `PptxDocument.Open(string path)`: Opens and parses a
  presentation. Throws `ArgumentNullException` for a null argument, `ArgumentException` for an empty
  path, and `InvalidDataException` when the package is not a readable ZIP archive, is missing a
  required part, is malformed, or is not a navigable presentation (a slide size and at least one slide
  are required).
- `PptxDocument.SlideCount`: The number of slides declared by the presentation.
- `PptxDocument.SlideSize`: The slide size as a `PptxSlideSize` (`WidthEmu` and `HeightEmu`, in EMU,
  English Metric Units; 914,400 EMU per inch).
- `PptxDocument.GetSlideSizeInPixels(float dpi)`: The slide size in pixels at `dpi`, each dimension
  rounded to the nearest pixel.
- `PptxDocument.Render(int slideIndex, int width, int height, PptxRenderOptions? options = null)`:
  Renders the slide into a new `width` x `height` `Surface`.
- `PptxDocument.Render(int slideIndex, float dpi, PptxRenderOptions? options = null)`: Renders the
  slide at `dpi`, preserving the slide's aspect ratio.
- `PptxDocument.Dispose()`: Releases the document; idempotent. Every other member then throws
  `ObjectDisposedException`.

`Render` throws `ArgumentOutOfRangeException` for an invalid slide index, `dpi` or surface size,
and `InvalidDataException` for malformed slide content. A shape, picture or graphic frame that
declares no position anywhere in its ancestry is skipped silently.

#### PptxRenderOptions

`PptxRenderOptions` is a sealed class with `init`-only properties (`PptxRenderOptions.Default` holds
the defaults):

- `BackgroundColor`: The color the surface is cleared to first (opaque white by default). A slide,
  layout or master `<p:bg>` fill, when declared, is painted over it.
- `StrictUnsupportedPictures`: See the EMF/WMF section below.
- `OnUnsupportedPictureSkipped`: See the EMF/WMF section below.

A well-formed but unsupported construct (for example a radial or path gradient, an unsupported
preset geometry or pattern-fill preset, a linked non-embedded image, or an unsupported raster image
format) makes `Render` throw `PptxUnsupportedFeatureException`, an `IOException` whose `Feature`
property carries a stable token such as `pptx-image-link`, `pptx-image-format`, `pptx-pattern-fill` or
`pptx-gradient-path`. A chart that cannot be rendered is reported as `pptx-chart-` followed by the
chart feature token.

### PPTX EMF/WMF Pictures

`PptxDocument.Render` cannot draw EMF/WMF vector metafile pictures (a known gap). By default, a
slide's `<p:pic>` whose media is an EMF or WMF is skipped - nothing is drawn for that picture and
the rest of the slide renders normally. To learn which pictures were skipped, set
`PptxRenderOptions.OnUnsupportedPictureSkipped` (it reports only the slide's own or group-nested
pictures; EMF/WMF pictures on a layout or master are skipped silently in both modes); to make
`Render` fail instead, set `PptxRenderOptions.StrictUnsupportedPictures = true`, which makes it throw
`PptxUnsupportedFeatureException` (`Feature` is `"pptx-image-metafile"`).

```csharp
using DemaConsulting.CanvasNet.Pptx;

using var document = PptxDocument.Open("slides.pptx");
var options = new PptxRenderOptions
{
    OnUnsupportedPictureSkipped = ex => Console.WriteLine($"Skipped picture: {ex.Message}"),
};
using var surface = document.Render(0, 96f, options);
```

### 3D Rendering Overview

The `DemaConsulting.CanvasNet.Render3D` package is a separate, optional NuGet package containing a
pure-managed software 3D rasterizer. It depends only on the core `CanvasNet` package (for `Surface`
and `Rgba32`) and has no knowledge of any 3D file format. Its types live in three namespaces:

- `DemaConsulting.CanvasNet.Render3D.SceneModel` - double-precision math, meshes, and the scene graph
- `DemaConsulting.CanvasNet.Render3D.Tessellation` - primitive mesh factories, 2D polygon regions,
  polygon triangulation, and linear and rotational extrusion
- `DemaConsulting.CanvasNet.Render3D.SceneRendering` - camera, lights, options, and the renderer

Scenes can combine solids with constructive solid geometry (union, difference, and intersection of
closed meshes) through `CsgNode`; see *Constructive Solid Geometry*.

The conventions used throughout: world space is right-handed with +Z up; the camera looks down -Z
in view space; a triangle whose vertices are counter-clockwise as seen from outside faces outward;
matrices use the same row-vector convention as `System.Numerics.Matrix4x4` (a point transforms as
`v * M`, and `a * b` applies `a` first); and angles are in degrees. All model-space math is done in
`double` precision, so scenes positioned far from the origin render without precision loss.

### Vector3D, Matrix4x4D, and BoundingBox3D

`Vector2D`, `Vector3D`, `Matrix4x4D`, and `BoundingBox3D` are immutable double-precision value types.

```csharp
public readonly record struct Vector3D(double X, double Y, double Z)
{
    public static Vector3D Zero { get; }
    public static Vector3D One { get; }
    public static Vector3D UnitX { get; }
    public static Vector3D UnitY { get; }
    public static Vector3D UnitZ { get; }
    public double Length { get; }
    public bool IsFinite { get; }
    public static double Dot(Vector3D left, Vector3D right);
    public static Vector3D Cross(Vector3D left, Vector3D right);
    public Vector3D Normalize(); // a zero-length vector returns Vector3D.Zero
    public Vector3 ToVector3(); // explicit narrowing to System.Numerics.Vector3
    // plus +, -, unary -, * and / by a scalar
}

public readonly struct Matrix4x4D : IEquatable<Matrix4x4D>
{
    public static Matrix4x4D Identity { get; } // default(Matrix4x4D) is the ZERO matrix
    public static Matrix4x4D CreateTranslation(Vector3D offset);
    public static Matrix4x4D CreateScale(Vector3D scale);
    public static Matrix4x4D CreateScale(double scale);
    public static Matrix4x4D CreateRotationX(double degrees); // also Y and Z
    public static Matrix4x4D CreateFromAxisAngle(Vector3D axis, double degrees);
    public static Matrix4x4D CreateMirror(Vector3D planeNormal);
    public static Matrix4x4D FromRowMajor(ReadOnlySpan<double> elements); // exactly 16 elements
    public static Matrix4x4D operator *(Matrix4x4D left, Matrix4x4D right); // left is applied first
    public double Determinant { get; }
    public bool IsFinite { get; }
    public bool TryInvert(out Matrix4x4D inverse);
    public Vector3D TransformPoint(Vector3D point);
    public Vector3D TransformDirection(Vector3D direction); // ignores translation
}

public readonly record struct BoundingBox3D(Vector3D Min, Vector3D Max)
{
    public static BoundingBox3D Empty { get; }
    public bool IsEmpty { get; }
    public Vector3D Size { get; }
    public Vector3D Center { get; }
    public double Diagonal { get; }
    public BoundingBox3D Union(BoundingBox3D other);
    public BoundingBox3D Union(Vector3D point);
    public BoundingBox3D Transform(Matrix4x4D matrix); // axis-aligned box of the transformed corners
}
```

Rotations by multiples of 90 degrees are exact (no rounding error). A mirror matrix has a negative
determinant; the library compensates by flipping triangle winding, so a mirrored mesh is shaded
like the original.

```csharp
using DemaConsulting.CanvasNet.Render3D.SceneModel;

// Rotate 90 degrees about Z, then move 10 units along X (the left operand is applied first)
var matrix = Matrix4x4D.CreateRotationZ(90) * Matrix4x4D.CreateTranslation(new Vector3D(10, 0, 0));
Console.WriteLine(matrix.TransformPoint(new Vector3D(1, 0, 0))); // X = 10, Y = 1, Z = 0
```

**Exceptions:**

- `ArgumentException`: Thrown by `FromRowMajor` when the span does not hold exactly 16 elements, and by
  `CreateFromAxisAngle`/`CreateMirror` when the axis or normal is zero-length or not finite.

### Mesh and MeshBuilder

`Mesh` is an immutable, validated, indexed triangle mesh: a list of `Vector3D` vertices and a flat
list of triangle indices (three per triangle). The winding contract is counter-clockwise as seen
from outside. `MeshBuilder` assembles a mesh and welds identical vertices.

```csharp
public sealed class Mesh
{
    public Mesh(IEnumerable<Vector3D> vertices, IEnumerable<int> triangleIndices);
    public IReadOnlyList<Vector3D> Vertices { get; }
    public IReadOnlyList<int> TriangleIndices { get; }
    public int VertexCount { get; }
    public int TriangleCount { get; }
    public BoundingBox3D Bounds { get; }
    public bool IsClosed { get; } // every edge shared by exactly two consistently wound triangles
    public double SignedVolume { get; } // positive for a closed, outward-oriented mesh
    public double SurfaceArea { get; }
    public Mesh Transform(Matrix4x4D matrix); // reverses winding when the determinant is negative
    public Mesh Reverse(); // flips every triangle (repairs an inside-out mesh)
}

public sealed class MeshBuilder
{
    public int VertexCount { get; }
    public int TriangleCount { get; }
    public int AddVertex(Vector3D position); // returns the index of an identical existing vertex
    public void AddTriangle(int a, int b, int c);
    public void AddPolygon(ReadOnlySpan<int> polygon); // convex, counter-clockwise; fan-triangulated
    public Mesh Build();
}
```

The `Mesh` constructor copies its inputs, so later changes to the caller's collections have no
effect. A mesh with zero triangles is allowed (it has empty bounds and is not closed).

```csharp
using DemaConsulting.CanvasNet.Render3D.SceneModel;

// A tetrahedron with outward-facing (counter-clockwise) triangles
var builder = new MeshBuilder();
var a = builder.AddVertex(new Vector3D(0, 0, 0));
var b = builder.AddVertex(new Vector3D(1, 0, 0));
var c = builder.AddVertex(new Vector3D(0, 1, 0));
var d = builder.AddVertex(new Vector3D(0, 0, 1));
builder.AddTriangle(a, c, b);
builder.AddTriangle(a, d, c);
builder.AddTriangle(a, b, d);
builder.AddTriangle(b, c, d);
Mesh tetrahedron = builder.Build();
Console.WriteLine($"{tetrahedron.IsClosed} {tetrahedron.SignedVolume}"); // Output: True 0.16666666666666666
```

**Exceptions:**

- `ArgumentNullException`: Thrown by the `Mesh` constructor for a null sequence.
- `ArgumentException`: Thrown by the `Mesh` constructor for an index count that is not a multiple of
  three or a non-finite vertex; by `Transform` for a non-finite matrix or result; by
  `AddVertex` for a non-finite position; by `AddTriangle` for repeated indices; and by `AddPolygon`
  for fewer than three indices or a repeated index.
- `ArgumentOutOfRangeException`: Thrown by the `Mesh` constructor and by `AddTriangle` and
  `AddPolygon` for an index that does not address a vertex.

### PrimitiveTessellator

`PrimitiveTessellator` (namespace `DemaConsulting.CanvasNet.Render3D.Tessellation`) creates welded
meshes for common solids: `Box`, `Sphere`, and `Cylinder` results are closed and outward-oriented,
while `Polyhedron` builds its mesh from the points and faces you supply and preserves them as given.

```csharp
public static class PrimitiveTessellator
{
    public static Mesh Box(Vector3D size, bool center = false);
    public static Mesh Sphere(double radius, int segments);
    public static Mesh Cylinder(double height, double radius1, double radius2, int segments, bool center = false);
    public static Mesh Polyhedron(IReadOnlyList<Vector3D> points, IReadOnlyList<IReadOnlyList<int>> facesCcw);

    // 2D profiles for the Extruder (see PolygonRegion)
    public static PolygonRegion Rectangle(Vector2D size, bool center = false);
    public static PolygonRegion Ellipse(double radiusX, double radiusY, int segments);
    public static PolygonRegion Circle(double radius, int segments);
    public static PolygonRegion Polygon(IReadOnlyList<Vector2D> points, IReadOnlyList<IReadOnlyList<int>>? paths = null);
}
```

- `Box` spans `[0, size]` on each axis, or is centered on the origin when `center` is true.
- `Sphere` is a UV sphere centered on the origin with its poles on the Z axis; `segments` (at
  least 3) is the number of divisions around the Z axis.
- `Cylinder` extends along +Z from 0 to `height` (or symmetrically about 0 when `center` is true).
  `radius1` is the bottom radius and `radius2` the top radius, so unequal radii make a frustum and a
  radius of 0 makes a cone (not both 0).
- `Polyhedron` welds the points and fan-triangulates each face, preserving the faces and winding you
  supply exactly as given: it neither closes the surface nor corrects its orientation, so closure and
  outward orientation are your responsibility. Faces must be **convex** and should be listed
  **counter-clockwise as seen from outside** for an outward-facing result; neither is checked, so
  inspect `Mesh.IsClosed` and `Mesh.SignedVolume`, and for an inside-out polyhedron (negative
  `SignedVolume`) call `Mesh.Reverse()`.
- `Rectangle` is a counter-clockwise rectangle from the origin to `size` (or centered on the origin
  when `center` is true). `Ellipse` and `Circle` are inscribed polygons centered on the origin whose
  vertex `j` is at the angle `2 pi j / segments` counter-clockwise from +X (the same ring as
  `Cylinder`, so extruding `Circle(r, n)` gives the same solid as `Cylinder(h, r, r, n)`); their
  area is slightly below the true ellipse. `Polygon` builds a region from a point list: with no
  `paths` the points form one contour in order, otherwise each path is a list of indices into
  `points` forming one contour (a nested contour is a hole). All four return a `PolygonRegion`.

**Exceptions:**

- `ArgumentOutOfRangeException`: Thrown for a non-positive or non-finite size, radius, or height,
  fewer than three segments, both cylinder radii zero, a face index outside the point list, or a
  `Polygon` path index outside the point list.
- `ArgumentException`: Thrown by `Polyhedron` for a face with fewer than three indices, a repeated
  index, or two indices that address identical (welded) points, and by the 2D factories for an
  invalid polygon (see *PolygonRegion*).
- `ArgumentNullException`: Thrown by `Polyhedron` for a null point list, face list, or face, and by
  `Polygon` for a null point list or path.

### PolygonRegion

`PolygonRegion` (namespace `DemaConsulting.CanvasNet.Render3D.Tessellation`) is an immutable,
validated 2D region bounded by simple polygon contours, with holes expressed by nesting. It is the
input of `Extruder` and `PolygonTriangulator`.

```csharp
public sealed class PolygonRegion
{
    public PolygonRegion(IEnumerable<IReadOnlyList<Vector2D>> contours);
    public static PolygonRegion Empty { get; }
    public IReadOnlyList<IReadOnlyList<Vector2D>> Contours { get; }
    public IReadOnlyList<Vector2D> Vertices { get; }
    public int VertexCount { get; }
    public bool IsEmpty { get; }
    public double Area { get; }
}
```

- Each contour is a list of at least three distinct points that is closed implicitly. The even-odd
  rule decides what is solid: a contour inside no other contour is an outer boundary, a contour
  inside one is a hole, and a contour inside a hole is an island, whatever winding you supply.
  `Contours` returns the normalized contours (outer boundaries counter-clockwise, holes clockwise).
- Closing duplicates (a last point equal to the first) and consecutive duplicate points are
  dropped; collinear points are kept and become vertices of the extruded mesh.
- `Area` is the area of the region (outer boundaries minus holes plus islands). `Empty` has no
  contours; extruding it gives an empty mesh. `Vertices` is all contour vertices in order, the index
  space of `PolygonTriangulator.Triangulate`.
- **Invalid polygons are rejected, never repaired**: the constructor throws `ArgumentException`
  (naming the contour) for a non-finite point, a contour with fewer than three distinct vertices or
  with zero area (all points collinear, or an area too small for a normal double, such as a triangle with
  legs of `1e-200`), a contour that crosses or touches itself (a bow tie, a
  spike, or a repeated non-consecutive point), and contours that cross or touch each other (a shared
  point, or a vertex on another contour's edge). Move touching contours apart or merge them
  yourself. `ArgumentNullException` is thrown for a null contour list or contour. Finite but
  extreme coordinates (beyond about 1e154, where the area overflows) are also rejected with
  `ArgumentException`, so `Area` is always a finite number.

```csharp
// A 10 x 10 square plate with a 4 x 4 hole (winding does not matter)
var plate = new PolygonRegion(
[
    new Vector2D[] { new(0, 0), new(10, 0), new(10, 10), new(0, 10) },
    new Vector2D[] { new(3, 3), new(3, 7), new(7, 7), new(7, 3) },
]);
Console.WriteLine(plate.Area); // Output: 84
```

### PolygonTriangulator

`PolygonTriangulator` triangulates a `PolygonRegion` into counter-clockwise triangles that use only
the region's own vertices (no points are added, and every collinear vertex is kept).

```csharp
public static class PolygonTriangulator
{
    public static IReadOnlyList<int> Triangulate(PolygonRegion region);
}
```

- The result lists three indices per triangle into `region.Vertices`. A region with `V` vertices,
  `H` holes and `P` outer boundaries gives exactly `V + 2H - 2P` triangles, which tile the region
  exactly and leave the holes uncovered. The decisions use exact arithmetic (no tolerances), and the
  result is deterministic.
- Holes are connected to their outer boundary by zero-width bridges before the ears are clipped.
  A convex polygon is linear. A concave polygon costs up to the number of vertices times the number
  of reflex (inward-pointing) vertices, so the worst case is quadratic in the number of vertices:
  fine for typical CAD-sized profiles, but a comb with 8,000 vertices and many deep teeth takes about
  two seconds, and the time keeps growing quadratically beyond that. There is no cancellation, so
  split very large concave profiles.
- `ArgumentNullException` is thrown for a null region.

```csharp
var triangles = PolygonTriangulator.Triangulate(plate);
Console.WriteLine(triangles.Count / 3); // Output: 8 (8 vertices + 2 * 1 hole - 2 * 1 boundary)
```

### Extruder

`Extruder` turns a `PolygonRegion` into a solid `Mesh` by extruding it along +Z or by revolving it
about the Z axis.

```csharp
public static class Extruder
{
    public static Mesh Linear(PolygonRegion region, double height, bool center = false,
        double twistDegrees = 0.0, int slices = 1, Vector2D? scale = null);
    public static Mesh Linear(PolygonRegion region, double height, bool center,
        double twistDegrees, int slices, double uniformScale);
    public static Mesh Rotate(PolygonRegion region, double angleDegrees, int segments);
}
```

Every result is a welded, closed, outward-oriented mesh with positive `SignedVolume` and no
degenerate triangles, so it is a valid operand for constructive solid geometry (`CsgNode`) without
any operand report. An empty region gives an empty mesh. These guarantees hold for profiles and
parameters of ordinary magnitude and are subject to two caveats:

- **Finite precision.** With extreme values (a tiny non-zero scale such as `1e-17`, a height near
  `double.MaxValue`, or coordinates of about `1e200`) rounding can make a triangle degenerate; the
  operand validator then reports `render3d-csg-degenerate-triangle`. Use scales, heights, and
  coordinates of moderate magnitude.
- **Scale and twist are about the origin.** For an apex (scale `(0, 0)`), a large scale change, or a
  per-axis scale combined with a twist, the profile should be **star-shaped about the origin** (every
  point of the profile is visible from the origin). Otherwise the solid can intersect itself (it may
  even come out inverted) and is not a valid operand.

The `Linear` method:

- The solid spans `z = 0` to `z = height`, or `+/- height/2` when `center` is true.
- `twistDegrees` rotates the top relative to the bottom about +Z: a **positive** angle turns the top
  **counter-clockwise** as seen from +Z (right-hand rule). If you are porting from a language with
  the opposite sign, negate the angle.
- `scale` is the scale of the top relative to the bottom about the origin (per axis, interpolated
  linearly; applied before the twist). Each component must be finite and not negative. A scale of
  `(0, 0)` makes the top a single **apex** point (no top cap), giving a pyramid or cone shape; the apex
  needs a region with a **single contour**, because a hole or a second island would meet the outer
  shell at one pinched (non-manifold) vertex, so such a region throws `ArgumentException`. A
  scale that is zero in only one axis, or negative in either, is rejected: it would flatten the top
  to a line or make the solid pass through itself. The `uniformScale` overload scales both axes.
- `slices` is the number of slices along the height (at least 1). A straight extrusion without
  taper or twist needs only one. Twisted or non-uniformly scaled walls are not planar, so the mesh
  approximates the smooth solid and the approximation improves as `slices` grows: use enough slices
  that the twist per slice is small (the volume error is about 1.7% for 90 degrees in 32 slices, and
  shrinks as slices increase). The slice count is never refined automatically.
- A region with a hole becomes **one connected solid with a through-hole**: the outer wall, the
  hole wall (facing into the hole), and both caps share their vertices. An island inside a hole is
  a separate solid.

The `Rotate` method:

- The profile's X is the radius and its Y is the height; it is revolved about the +Z axis, turning
  from azimuth 0 toward `+angleDegrees` (counter-clockwise as seen from +Z).
- `angleDegrees` must be in `(0, 360]`, and not so small that its radian value rounds to zero
  (`double.Epsilon` is rejected). Exactly 360 is a full revolution (`segments` at least 3, no
  end caps); a smaller angle is a partial sweep (`segments` at least 1) with end caps at both ends. In
  every case each angular step, `angleDegrees / segments`, must be at most **120 degrees** (the step of
  the coarsest full revolution); wider steps give flat or inside-out facets and are rejected. For
  example `(90, 1)`, `(180, 2)`, and `(359, 3)` are valid, while `(180, 1)` and `(359, 2)` are not.
- The whole profile must lie on **one side of the axis**: every vertex with `x >= 0`, or every
  vertex with `x <= 0`. A profile that **crosses the axis** (vertices with both signs) throws
  `ArgumentException`, as does a profile lying only on the axis. There is no tolerance: `x == 0`
  exactly means on the axis. Vertices on the axis are welded into single points, so a profile
  touching the axis along an edge gives a cone, cylinder, or lens without degenerate triangles. A
  profile that touches the axis only at an **isolated vertex** (both neighboring edges leave the axis)
  would pinch the solid at that point and throws `ArgumentException`.
- The mesh is faceted: its volume is `segments * sin(angle / segments) * integral(|x| dA)` over the
  profile, which approaches the exact `angle * x_centroid * area` as `segments` grows (for a
  full revolution, `angle` is 2 pi radians).
- A profile with a hole gives a hollow solid (its cavity is a second, inward-facing shell).

**Exceptions:**

- `ArgumentNullException`: Thrown for a null region.
- `ArgumentOutOfRangeException`: Thrown for a height that is not finite and positive, fewer than one
  slice, an invalid scale, an angle outside `(0, 360]` (or one whose radian value is zero), too few
  segments, or a sweep step (`angleDegrees / segments`) wider than 120 degrees.
- `ArgumentException`: Thrown for a non-finite twist, an apex (zero scale) over a region with a hole or
  several contours, a profile that crosses or lies on the rotation axis or touches it at an isolated
  vertex, a sweep step too small to resolve at a profile vertex's radius, or a result too
  large for a mesh.

```csharp
var plate3D = Extruder.Linear(plate, 2.0, center: true);
Console.WriteLine(plate3D.SignedVolume); // Output: 168 (84 * 2)

// A cone: revolve a right triangle that touches the axis
var cone = Extruder.Rotate(new PolygonRegion([new Vector2D[] { new(0, 0), new(2, 0), new(0, 5) }]), 360.0, 32);
Console.WriteLine(cone.IsClosed); // Output: True
```

### ConvexHull

`ConvexHull` computes the exact convex hull of a point set: a closed solid mesh for 3D points and a polygon for
2D points. It is what the OpenSCAD `hull` module is built on.

```csharp
public static class ConvexHull
{
    public static Mesh Hull3D(IEnumerable<Vector3D> points, CancellationToken cancellationToken = default);
    public static IReadOnlyList<Vector2D> Hull2D(IEnumerable<Vector2D> points, CancellationToken cancellationToken = default);
}
```

- `Hull3D` returns a welded, closed, outward-oriented mesh with positive `SignedVolume` whose vertices are
  exactly the **strictly extreme** points of the input (points inside the hull, on a face, or on an edge do
  not appear), so the result is a valid operand for constructive solid geometry (`CsgNode`). It has exactly
  `2 * V - 4` triangles for `V` vertices; coplanar triangles of one facet are merged and fan-triangulated.
- `Hull2D` returns the strictly convex polygon (no collinear points), counter-clockwise, starting at the
  lexicographically smallest point. A degenerate set does not throw: it returns no point, the single point,
  or the two end points of the segment, and the caller decides what that means.
- Every decision is exact (no tolerance), and the result depends only on the point **set**: any order and any
  repetition of the points (and `-0.0` versus `0.0`) gives a byte-identical result.
- `Hull2D` is O(n log n) time in every case (the sort dominates; the monotone chain after it is O(n)) and O(n)
  memory.
- `Hull3D` is O(n log n) for the copy, the sort and the removal of duplicates, plus the incremental insertion:
  each insertion costs the faces it replaces and the faces around the previously inserted vertex, so the
  total is O(n log n) when the hull stays small (random points in a cube or a ball, lattices) and O(n * h) in the
  worst case, which is O(n^2) when almost every point is a hull vertex (h = number of hull vertices; points
  on a moment curve are the classic adversarial input). Merging the coplanar triangles into facets is
  O(F log F) for F ≤ 2n - 4 triangles. Memory is O(n). The cancellation token is polled every few thousand steps
  (the initial sort cannot be interrupted).

**Exceptions:** `ArgumentNullException` for a null sequence; `ArgumentException` for a non-finite coordinate, and
for `Hull3D` when the points are not **affinely independent** enough to span a volume (fewer than four distinct
points, all collinear, or all coplanar; the message says which); `OperationCanceledException` when canceled.

```csharp
Vector3D[] points =
[
    new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0),
    new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1),
    new(0.5, 0.5, 0.5), // inside: not a vertex of the hull
];
var hull = ConvexHull.Hull3D(points);
Console.WriteLine($"{hull.VertexCount} {hull.TriangleCount} {hull.SignedVolume}"); // Output: 8 12 1
```

### MinkowskiSum

`MinkowskiSum.ConvexSum` computes the Minkowski sum of two **convex** solid meshes (every point `a + b`, the
shape of one solid swept over the other): rounding a block with a sphere, or offsetting a part by another. It
is what the OpenSCAD `minkowski` module is built on.

```csharp
public static class MinkowskiSum
{
    public const int MaxPairwiseSums = 2_097_152;
    public static Mesh ConvexSum(Mesh a, Mesh b, CancellationToken cancellationToken = default);
}
```

- The result is the exact `ConvexHull.Hull3D` of all pairwise sums of the operands' vertices (each a single
  `double` addition, so the sum has an error of at most one unit in the last place per coordinate): a closed,
  outward-oriented mesh and a valid CSG operand, byte-identical for `(a, b)` and `(b, a)`.
- **Convex operands only, and checked.** An operand is verified to be a closed, consistently wound, single-shell
  convex solid; it is accepted only if it differs from the convex hull of its own vertices by at most **1e-9
  of the hull's volume and surface area** (plus the rounding error of the volume computation), so the rounding
  noise of rotated cubes, cylinder caps, and sphere quads passes and no real dent, notch, hole, or second shell
  does. The sum is then computed from the operand's vertices, which is exact for the operand's hull. A
  non-convex operand is **never approximated**: it throws. A per-edge tolerance would be unsound (a gently
  concave bowl of many facets passes any per-edge height tolerance), which is why the test is global. Models
  whose offset is huge compared with their size (more than about a million times the feature size) can fail
  the test through coordinate rounding and are rejected as well.
- The work is O(`Va * Vb` * log(`Va * Vb`)) with O(`Va * Vb`) memory (the array of sums, 24 bytes per point and about
  50 MB at the bound, is sorted in place, so the peak is about 70 MB), where `Va` and
  `Vb` are the numbers of distinct vertices; the product is limited to `MaxPairwiseSums` (2^21).

**Exceptions:** `ArgumentNullException` for a null operand; `ArgumentException` for an operand without
triangles or without volume, or when `Va * Vb` exceeds `MaxPairwiseSums`; `NotSupportedException` for an
operand that is not provably a closed convex solid (open, non-manifold, inconsistently wound, inverted, several
shells, or not convex within the tolerance; the message gives the reason); `OperationCanceledException` when
canceled.

```csharp
var block = PrimitiveTessellator.Box(new Vector3D(2, 2, 2));
var other = PrimitiveTessellator.Box(new Vector3D(1, 1, 1));
var sum = MinkowskiSum.ConvexSum(block, other);
Console.WriteLine(sum.SignedVolume); // Output: 27 (a 3 x 3 x 3 box)

try
{
    var corners = new Vector2D[] { new(0, 0), new(3, 0), new(3, 1), new(1, 1), new(1, 3), new(0, 3) };
    var notch = Extruder.Linear(new PolygonRegion([corners]), 1.0);
    MinkowskiSum.ConvexSum(notch, other);
}
catch (NotSupportedException ex)
{
    Console.WriteLine("not convex: " + ex.Message);
}
```

### Scene

A `Scene` is an immutable tree of nodes. Each node has a `Transform` (a `Matrix4x4D`, default
identity) applied to the node and everything below it, and an optional `Appearance`. A node's
effective color is that of the innermost node that has an appearance; the alpha of a node color is
honored (see *Transparency and Overlays* below). A group is the union of its children; a `CsgNode`
combines its children by union, difference, or intersection (see *Constructive Solid Geometry*
below), and an `OverlayNode` is an independent translucent body (see *Transparency and Overlays*).

```csharp
public abstract class SceneNode
{
    public Matrix4x4D Transform { get; init; } // default Matrix4x4D.Identity; must be finite
    public NodeAppearance? Appearance { get; init; }
}

public sealed class MeshNode : SceneNode
{
    public MeshNode(Mesh mesh);
    public Mesh Mesh { get; }
}

public sealed class GroupNode : SceneNode
{
    public GroupNode(IEnumerable<SceneNode> children);
    public IReadOnlyList<SceneNode> Children { get; }
}

public enum CsgOperation { Union, Difference, Intersection }

public sealed class CsgNode : SceneNode
{
    public CsgNode(CsgOperation operation, IEnumerable<SceneNode> children);
    public CsgOperation Operation { get; }
    public IReadOnlyList<SceneNode> Children { get; } // for Difference the first is the minuend
}

public sealed class OverlayNode : SceneNode
{
    public OverlayNode(Rgba32 color, IEnumerable<SceneNode> children);
    public Rgba32 Color { get; } // replaces every appearance beneath the overlay; alpha is honored
    public IReadOnlyList<SceneNode> Children { get; } // their union is the overlay body
}

public sealed record NodeAppearance(Rgba32 Color); // alpha below 255 makes the surface translucent

public sealed class Scene
{
    public Scene(SceneNode root);
    public SceneNode Root { get; }
    public BoundingBox3D ComputeBounds(); // tight world-space bounds, overlay bodies included
    public BoundingBox3D ComputeBounds(bool includeOverlays); // false leaves the overlay bodies out
}
```

The node hierarchy is closed (you cannot derive your own node types). A node (including a mesh
node) may appear under several parents. Coincident faces of different meshes are resolved by scene
order: the node that comes first in a depth-first traversal wins.

**Exceptions:**

- `ArgumentNullException`: Thrown by `MeshNode` for a null mesh, by `GroupNode`, `CsgNode` and
  `OverlayNode` for a null child list, and by `Scene` for a null root.
- `ArgumentException`: Thrown by `GroupNode`, `CsgNode` and `OverlayNode` for a null child, and when a
  node `Transform` contains a non-finite element.
- `ArgumentOutOfRangeException`: Thrown by `CsgNode` for an undefined `CsgOperation` value.

### Transparency and Overlays

The alpha channel of a `NodeAppearance` color is honored. A color with alpha below 255 makes the
visible surfaces of the solid translucent: the renderer composites the surfaces along each view ray
from the nearest to the farthest, deterministically and independent of any submission order, until it
meets an opaque surface, and then composites the remainder over `SceneRenderOptions.BackgroundColor`
(whose own alpha is honored, so over a transparent background the pixel keeps its color with the
accumulated alpha). A translucent box therefore shows its far wall through its front, and a
translucent box in front of an opaque one shows a blend of the two. An alpha of 0 is invisible but the
body is still solid, so it still takes part in a difference or intersection. The alpha of
`SceneRenderOptions.DefaultColor`, `BackFaceColor` and of a light color remains ignored.

```csharp
// A translucent red cube in front of an opaque blue floor: the pixel blends red (twice: front and
// back wall of the cube) with the blue floor behind it
var cube = new MeshNode(PrimitiveTessellator.Box(new Vector3D(2, 2, 2), center: true))
{
    Transform = Matrix4x4D.CreateTranslation(new Vector3D(0, 0, 2)),
    Appearance = new NodeAppearance(new Rgba32(200, 40, 40, 128)),
};
var floor = new MeshNode(PrimitiveTessellator.Box(new Vector3D(8, 8, 1), center: true))
{
    Appearance = new NodeAppearance(new Rgba32(30, 60, 220, 255)),
};
var scene = new Scene(new GroupNode([floor, cube]));
```

Members of one union **merge**: a union of overlapping translucent bodies shows only the boundary of
the union, and geometry fully enclosed by a translucent solid of the same union is not visible (this
is the only meaning consistent with difference and intersection). The cut surface of a translucent
difference is drawn with the subtrahend's own color and alpha.

An `OverlayNode` expresses an **independent** see-through body (a ghost or a highlight). Its children
(a union) are drawn with the overlay's `Color` (alpha honored; it replaces every appearance beneath
the overlay, and the overlay node's own `Appearance` is ignored), and the overlay is **false** in every
enclosing boolean: it does not add to, subtract from, or intersect the solid. It is depth-composited
with the rest of the scene, so it tints what is behind it, is hidden behind opaque geometry, and can
sit anywhere, including inside a solid that is translucent. An overlay nested in an overlay is a plain
group (the outer color wins). To highlight a subtrahend, put the same node both in the difference and,
wrapped in an overlay, beside it:

```csharp
var cutter = new MeshNode(PrimitiveTessellator.Cylinder(6, 1.0, 1.0, 32, center: true));
var overlay = new OverlayNode(new Rgba32(255, 64, 128, 110), [cutter]);
var part = new CsgNode(CsgOperation.Difference, [cube, new GroupNode([cutter, overlay])]);
```

`Scene.ComputeBounds()` (and so `RenderFitted` and `Camera.FitToBounds`) includes the overlay bodies
so that a ghost stays in the picture; `Scene.ComputeBounds(false)` leaves them out, for example to size
only the solid.

**Requirements.** Like constructive solid geometry, translucency and overlays are resolved with signed
surface crossings, so every mesh in such a scene should be a closed, consistently wound, outward-oriented
solid (see *Operands must be solids* below); the renderer checks and reports them with the same
tokens and options. Such scenes are rendered per pixel from stored surface crossings and are subject to
`MaxFragmentsPerTile` (an overlay of the same size doubles the fragments of its tile); an opaque scene
without overlays is rendered exactly as before, with no extra memory.

### Constructive Solid Geometry

A `CsgNode` builds a solid from other solids. `Union` is inside where any child is inside,
`Difference` is the first child minus every other child, and `Intersection` is inside where every
child is inside. The operations are n-ary and nest freely (a `CsgNode` may be a child of a
`CsgNode` or a `GroupNode`), the node's `Transform` and `Appearance` apply to all of its children,
and a node without children is empty for every operation. The renderer never builds a boolean mesh:
each sample of the image is resolved from the surfaces its view ray crosses, so a result can be
rendered and re-rendered cheaply, and the operand meshes are never modified.

```csharp
// A 4 x 4 x 4 cube hollowed out by a centered sphere (a round window in every face)
var cube = new MeshNode(PrimitiveTessellator.Box(new Vector3D(4, 4, 4), center: true));
var bite = new MeshNode(PrimitiveTessellator.Sphere(2.6, 48));
var part = new CsgNode(CsgOperation.Difference, [cube, bite]);
```

**Operands must be solids.** Every mesh that takes part in a CSG scene should be a closed,
consistently wound, outward-oriented (counter-clockwise from outside) mesh: `Mesh.IsClosed` must
be `true` and `Mesh.SignedVolume` positive. Closedness is geometric: vertices at exactly the same
position count as one vertex, so a mesh that stores every triangle's corners separately (for
example one read from an STL file) is closed when its triangles form a closed surface. A hollow
solid is valid: an outward outer shell with inward-wound cavity shells (and outward solids nested
inside a cavity again); a shell whose orientation disagrees with how deeply it is nested - for
example an inside-out cube next to a valid one - is reported even when the total volume is
positive. The `PrimitiveTessellator` box, sphere, cylinder, and
cone meshes satisfy this; for a closed mesh with a negative `SignedVolume` (inside-out), use
`Mesh.Reverse`. Checking applies to every mesh in a scene that contains a difference or an
intersection (or a translucent appearance or an overlay, see *Transparency and Overlays*), not only
the direct operands, because an open mesh anywhere in the scene would vanish
from the result. By default the renderer is **tolerant**: it renders anyway, and the output is
deterministic but visibly wrong where an operand is not a solid. To find out about such operands,
set `SceneRenderOptions.UnsupportedFeatureHandler` (called once per problem, on the calling
thread, before any pixels are produced) or set `ThrowOnUnsupportedFeature` for **strict** behavior
(the first problem throws `SceneRenderUnsupportedFeatureException`). Without either option no
analysis is performed.

```csharp
public sealed record SceneRenderUnsupportedFeature(string Feature, string Message, MeshNode? Node = null);

public sealed class SceneRenderUnsupportedFeatureException : InvalidOperationException
{
    public SceneRenderUnsupportedFeatureException(string feature, string message, MeshNode? node = null);
    public string Feature { get; } // the same token as SceneRenderUnsupportedFeature.Feature
    public MeshNode? Node { get; }
}
```

The `Feature` tokens are stable and machine-matchable:

| Token | Meaning |
| ----- | ------- |
| `render3d-csg-open-mesh` | The mesh has a boundary edge (it is not closed) |
| `render3d-csg-non-manifold-edge` | An edge is shared by three or more triangles |
| `render3d-csg-inconsistent-winding` | Neighboring triangles are wound in conflicting directions |
| `render3d-csg-degenerate-triangle` | The mesh has zero-area triangles (repeated index, or collinear vertices) |
| `render3d-csg-inverted-mesh` | A closed mesh has a shell wound against its nesting |
| `render3d-csg-singular-transform` | An instance's world transform is singular (zero or non-finite determinant) |

Mesh problems are reported once per `MeshNode` however many places in the scene share it; a
singular transform (or triangles that collapse under an extreme transform, reported as
`render3d-csg-degenerate-triangle`) is reported per instance, so a node shared by a valid and a
singular instance is reported for the singular one. Reports are delivered
in scene order, and `Node` identifies the offending mesh node so a front end can map it back to its
source.

Rendering behavior worth knowing:

- **Unions are free.** An opaque scene whose `CsgNode`s are all unions (or whose difference/intersection
  nodes have a single child) is rendered exactly as a scene of groups: the same z-buffer path, no
  extra memory, no operand analysis, and byte-identical output to a render without `CsgNode`.
- **Per-tile budget.** A scene with a difference or an intersection (or a translucent appearance or an
  overlay) stores every surface crossing
  of a 32 x 32 pixel tile. `SceneRenderOptions.MaxFragmentsPerTile` (default 4 Mi, about 32 MB per
  worker) bounds that memory; a tile that needs more throws `RenderBudgetExceededException`
  (with the `MaxFragmentsPerTile` that was exceeded), identically for every thread count. Lower
  `Supersampling`, the image size, or the scene complexity - or raise the budget - to avoid it.
- **Cut surfaces.** A surface exposed by a subtrahend is painted in that subtrahend's own color,
  lit as seen by the viewer; it is never painted with `BackFaceColor`.
- **Camera inside a solid.** An eye inside a solid starts inside it: the first exit is the visible
  surface and is painted with `BackFaceColor`, as for any back face. For an orthographic camera
  (which has no eye position along its rays) set an explicit `Camera.NearPlane` to place the
  viewpoint. An explicit `NearPlane` (including one in front of some geometry, or a camera inside
  an operand such as a hollow room) is supported and not reported: the result is the CSG solid
  clipped to the near plane of the view frustum, and no cap surfaces are drawn on that plane, so
  from inside a hollow room you see the room's walls.
- **Coincident faces.** Faces of different operands that coincide (for example a subtrahend whose
  face lies exactly on a face of the minuend) are resolved without speckle when their coordinates
  are identical. Meshes that merely coincide mathematically but are placed through different
  transforms are within a small depth tolerance of each other and are also resolved, but along the
  edge-on silhouette of such walls a few isolated pixels (the tests tolerate up to 12 in an
  80 x 80 image across many views) can still be wrong. Features thinner than the depth tolerance
  (one millionth of the scene size; up to about one thousand times that between different mesh
  instances seen at grazing angles) are treated as coincident. Where you can, give a subtrahend a
  small overlap instead of an exactly flush face.
- **Mirrored transforms.** An operand under a negative-determinant (mirroring) transform renders
  as if the mirror were baked into its vertices; the inside-out check is unaffected by mirroring.
- **Determinism.** At any given `Supersampling` setting, the output is byte-identical for every
  `MaxDegreeOfParallelism` and every run, as for scenes without CSG. (Different `Supersampling`
  factors naturally produce different antialiasing.)
- **Bounds.** `Scene.ComputeBounds()` includes every operand, even the removed parts (and every overlay
  body), so `RenderFitted` and `Camera.FitToBounds` frame everything and may leave extra margin.

### Camera

`Camera` describes the viewpoint and projection. A default-constructed camera is valid (the eye on
the -Y axis looking at the origin with +Z up, perspective, 22.5 degree vertical field of view).

```csharp
public enum CameraProjection { Perspective, Orthographic }

public sealed class Camera
{
    public const double DefaultFieldOfViewDegrees = 22.5;
    public const double DefaultAzimuthDegrees = 45.0;
    public static readonly double DefaultElevationDegrees; // about 35.26 (isometric)

    public Vector3D Eye { get; init; }
    public Vector3D Target { get; init; }
    public Vector3D Up { get; init; } // default +Z
    public CameraProjection Projection { get; init; }
    public double FieldOfViewDegrees { get; init; } // vertical; between 0 and 180 (exclusive)
    public double? OrthographicHeight { get; init; } // required for Orthographic
    public double? NearPlane { get; init; } // null = automatic
    public double? FarPlane { get; init; } // null = automatic

    public static Camera LookAt(Vector3D eye, Vector3D target, Vector3D up);
    public static Camera FitToBounds(
        BoundingBox3D bounds,
        double azimuthDegrees,
        double elevationDegrees,
        double aspectRatio,
        CameraProjection projection = CameraProjection.Perspective,
        double fieldOfViewDegrees = DefaultFieldOfViewDegrees,
        double marginFraction = 0.05);
    public Matrix4x4D GetViewMatrix();
}
```

`FitToBounds` places the camera so the bounding box fills the frame (with `marginFraction` of
margin) when viewed from the given orbit angles: azimuth 0 and elevation 0 look along +Y from the
-Y axis (a front view), positive elevation raises the eye toward +Z, and positive azimuth moves the
eye counter-clockwise as seen from above. An empty or zero-size bounding box falls back to a unit
cube at the origin. When the view direction is parallel to the up vector (for example, looking
straight down), an alternate up vector is substituted. With `NearPlane`/`FarPlane` left `null`, the
renderer chooses them from the scene's depth range; geometry that crosses the near plane is clipped,
so a camera inside or beside a model works.

**Exceptions:**

- `ArgumentException`: Thrown by an initializer for a non-finite `Eye`, `Target`, or `Up`; by
  `LookAt` when the eye equals the target or `up` is zero; and by `SceneRenderer.Render` (not at
  initialization) when the eye equals the target, an orthographic camera has no `OrthographicHeight`,
  or `NearPlane` is not less than `FarPlane`.
- `ArgumentOutOfRangeException`: Thrown for a field of view outside (0, 180), a non-positive
  or non-finite height or clip distance, and by `FitToBounds` for a non-finite angle, a non-positive
  aspect ratio, or a negative margin.

### Lights

Lights are set through `SceneRenderOptions.Lights`. If it is `null`, a neutral default rig is used:
an ambient light of 0.35, a camera-relative headlight of 0.55, and a camera-relative key light of
0.25 from the upper left (also available as `Light.DefaultRig`). An explicit empty list renders
everything black; to get flat, unshaded color use a single `AmbientLight { Intensity = 1 }`.

```csharp
public abstract class Light
{
    public static IReadOnlyList<Light> DefaultRig { get; }
    public Rgba32 Color { get; init; } // default white; alpha is ignored
    public float Intensity { get; init; } // default 1; finite and not negative
}

public sealed class AmbientLight : Light
{
}

public sealed class DirectionalLight : Light
{
    public Vector3D Direction { get; init; } // direction the light TRAVELS; non-zero
    public bool IsCameraRelative { get; init; }
}
```

A `DirectionalLight.Direction` is the direction the light travels, normalized when set. When
`IsCameraRelative` is `true` the direction is expressed in view space (+X right, +Y up, -Z forward),
so the light moves with the camera; otherwise it is a world-space direction. Shading is flat
(one color per triangle) Lambertian shading plus an optional Blinn-Phong highlight controlled by
`SceneRenderOptions.SpecularStrength`/`SpecularPower`.

**Exceptions:**

- `ArgumentOutOfRangeException`: Thrown when `Intensity` is negative or not finite.
- `ArgumentException`: Thrown when `Direction` is zero-length or not finite.

### SceneRenderOptions

`SceneRenderOptions` is an immutable options bag. Every property is validated when it is
initialized, so an options instance is always valid.

```csharp
public sealed class SceneRenderOptions
{
    public static SceneRenderOptions Default { get; }

    public Rgba32 BackgroundColor { get; init; } // default opaque white; alpha is honored
    public Rgba32 DefaultColor { get; init; } // default (180, 180, 180): nodes without an appearance; alpha is ignored
    public Rgba32 BackFaceColor { get; init; } // default (200, 70, 70): surfaces seen from behind; alpha is ignored
    public IReadOnlyList<Light>? Lights { get; init; } // null = Light.DefaultRig; copied
    public float SpecularStrength { get; init; } // default 0.10; 0 to 1
    public float SpecularPower { get; init; } // default 32; greater than 0
    public int Supersampling { get; init; } // default 3; 1 to 4 samples per axis
    public int MaxDegreeOfParallelism { get; init; } // default -1; 1 = sequential
    public long MaxFragmentsPerTile { get; init; } // default 4 Mi; 1 to Array.MaxLength; per-pixel scenes only
    public Action<SceneRenderUnsupportedFeature>? UnsupportedFeatureHandler { get; init; } // default null
    public bool ThrowOnUnsupportedFeature { get; init; } // default false (tolerant)
}
```

`Supersampling` of `k` shades `k x k` samples per pixel and resolves them with a box filter, so
cost grows with the square of `k`; a transparent `BackgroundColor` is preserved, and antialiased
edges keep their color at partial opacity. `MaxDegreeOfParallelism` of `-1` lets the scheduler
choose how many tiles to render at once, `1` renders sequentially, and the output is byte-identical
for every setting. A back-facing surface is not culled but painted with `BackFaceColor`, which makes
inside-out or open meshes easy to spot.

The last three properties apply to scenes that are resolved per pixel - a `CsgNode` difference or
intersection, a translucent appearance, or an `OverlayNode` (see *Constructive Solid Geometry* and
*Transparency and Overlays*): `MaxFragmentsPerTile` bounds the per-tile fragment storage and is
ignored for opaque scenes without such nodes, `UnsupportedFeatureHandler` receives a
`SceneRenderUnsupportedFeature` for each operand problem, and `ThrowOnUnsupportedFeature` turns the
first problem into a `SceneRenderUnsupportedFeatureException` instead.

**Exceptions:**

- `ArgumentOutOfRangeException`: Thrown by an initializer for `Supersampling` outside 1 to 4,
  `SpecularStrength` outside 0 to 1, a non-positive or non-finite `SpecularPower`, a
  `MaxDegreeOfParallelism` of 0 or below -1, or a `MaxFragmentsPerTile` below 1 or above
  `Array.MaxLength`.
- `ArgumentException`: Thrown when `Lights` contains a null light.

### SceneRenderer

`SceneRenderer` is the static entry point that renders a `Scene` into a new `Surface`. The caller
owns, and must dispose, the returned surface.

```csharp
public static class SceneRenderer
{
    public static Surface Render(
        Scene scene,
        Camera camera,
        int width,
        int height,
        SceneRenderOptions? options = null,
        CancellationToken cancellationToken = default);

    public static Surface RenderFitted(
        Scene scene,
        int width,
        int height,
        SceneRenderOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

`Render` draws the scene as seen by the camera. `RenderFitted` builds an isometric camera (azimuth
45 degrees, elevation about 35.26 degrees) that frames the whole scene with a 5 % margin, which is
a convenient way to preview a scene; an empty scene renders the background only. Rendering is
deterministic, and a scene, camera, or options instance may be shared between threads and renders.
Passing a `CancellationToken` lets you abandon a long render: cancellation throws
`OperationCanceledException` (never an `AggregateException`) and releases the partial surface.

**Exceptions:**

- `ArgumentNullException`: Thrown when `scene` or `camera` is null.
- `ArgumentOutOfRangeException`: Thrown when `width` or `height` is outside 1 to
  `Surface.MaxDimension`.
- `ArgumentException`: Thrown by `Render` when the camera is invalid (eye equals target, an
  orthographic camera without `OrthographicHeight`, or `NearPlane` not less than `FarPlane`).
- `OperationCanceledException`: Thrown when `cancellationToken` is cancelled.
- `RenderBudgetExceededException`: Thrown when a scene with a CSG difference or intersection, a
  translucent appearance or an overlay needs more than `MaxFragmentsPerTile` fragments in a tile.
- `SceneRenderUnsupportedFeatureException`: Thrown when `ThrowOnUnsupportedFeature` is set and a CSG,
  translucent or overlay mesh is not a closed, outward-oriented solid.

### ScadDocument

`ScadDocument` (namespace `DemaConsulting.CanvasNet.Scad`) is distributed via the separate
`DemaConsulting.CanvasNet.Scad` NuGet package, which references the core `DemaConsulting.CanvasNet` and the
`DemaConsulting.CanvasNet.Render3D` packages - see the Installation section of the project README. It is the single
entry point for OpenSCAD-language models. A document is **immutable and thread-safe**: it is created from source
text, checked for syntax once, and can then be evaluated, built into a scene, or rendered any number of times (and
from several threads at once), each call giving the same result for the same options. The package is an
independent, clean-room implementation of the language, not affiliated with the OpenSCAD project; it follows its
own documented dialect (described below) and does not aim for pixel or tessellation parity with any other
implementation.

```csharp
public sealed partial class ScadDocument
{
    public static ScadDocument Parse(string source, ScadOpenOptions? options = null);
    public static ScadDocument Open(string path, ScadOpenOptions? options = null);
    public static ScadDocument Open(Stream stream, ScadOpenOptions? options = null);

    public string? SourceName { get; }
    public IReadOnlyList<ScadDirective> Directives { get; }
    public IReadOnlyList<string> ModuleNames { get; }
    public IReadOnlyList<string> FunctionNames { get; }

    public ScadEvaluationResult Evaluate(
        ScadEvaluationOptions? options = null,
        CancellationToken cancellationToken = default);

    public Scene BuildScene(
        ScadRenderOptions? options = null,
        CancellationToken cancellationToken = default);

    public Surface Render(
        int width,
        int height,
        ScadRenderOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

- `Parse` and the two `Open` overloads read UTF-8 source (at most 4,194,304 characters) and throw a
  `ScadSyntaxException` with the file, line, and column of the first syntax error. `Open(string)` names the source
  by the path and uses the file's folder as the base directory for `include` and `use`; a string or stream document
  has no base directory unless `ScadOpenOptions` supplies one (or a `FileResolver`).
- `Evaluate` runs the program without generating geometry and returns the `echo` lines, the warnings, the
  unsupported-feature reports, and the number of module instances.
- `BuildScene` evaluates the program and returns a Render3D `Scene` in right-handed, Z-up model space, so you can
  measure it (`scene.ComputeBounds()`), combine it with other scenes, or render it with your own camera and
  `SceneRenderer`. Every call returns a new scene; a model without geometry gives a scene that renders as the
  background.
- `Render` evaluates, builds, and draws the model onto a new `Surface` of exactly `width` by `height` pixels. The
  caller owns, and must dispose, the returned surface. Without a camera the view is **fitted to the model**: an
  isometric perspective view (azimuth 45 degrees, elevation about 35.26 degrees) on a white background, with
  neutral gray surfaces unless the model uses `color()`. The result is the same as `SceneRenderer.RenderFitted`
  of the scene `BuildScene` returns, and rendering is deterministic: two renders of one document with the same
  options are byte-identical, for every degree of parallelism.

**Exceptions** (`BuildScene` and `Render`; `Evaluate` throws the same, except the render-only ones):

- `ArgumentOutOfRangeException`: Thrown by `Render`, before anything is evaluated, when `width` or `height` is
  outside 1 to `Surface.MaxDimension`.
- `ArgumentException`: Thrown by `Render`, before anything is evaluated, when `ScadRenderOptions.Camera` is invalid
  (for example eye equal to target).
- `ScadSyntaxException`: Thrown when an included or used file contains a syntax error (its location names that
  file).
- `ScadEvaluationException`: Thrown, with the source location in the message, when an `assert` fails, a limit is
  exceeded (including the triangle limit), an `include` or `use` violates the sandbox, or, without a location,
  when the renderer's `MaxFragmentsPerTile` budget is exceeded by a model with booleans.
- `ScadUnsupportedFeatureException`: Thrown at the first unsupported feature of any stage when
  `ScadEvaluationOptions.ThrowOnUnsupportedFeature` is set (see *Unsupported Features and Strict Mode*).
- `OperationCanceledException`: Thrown, never wrapped, when `cancellationToken` is canceled before or during the
  work; with an already canceled token nothing is evaluated.
- `InvalidDataException` and `IOException`: Thrown when an included or used file is longer than 4,194,304
  characters or cannot be read.

Nothing the model asks for is skipped silently: an invalid argument is a **warning** at the module's location and
yields nothing for that instance, a construct this package recognizes but cannot build is an **unsupported
feature** with a stable identifier, and an exhausted resource is an **error**. Warnings, unsupported features, and
`echo` output of all stages are delivered to the handlers of `ScadEvaluationOptions` once, in the order they
occurred, on the calling thread, before the method returns or throws.

### ScadRenderOptions

`ScadRenderOptions` is an immutable class; every property is init-only and validated when set, so one instance can
be shared between threads and calls.

<!-- markdownlint-disable MD013 -->
| Property | Default | Meaning |
| -------- | ------- | ------- |
| `Evaluation` | `null` (default `ScadEvaluationOptions`) | The options of the evaluation: `-D`-style `Variables`, `Time` (`$t`), `Limits`, the `EchoHandler`, `WarningHandler`, and `UnsupportedFeatureHandler`, and `ThrowOnUnsupportedFeature`. It is the single place that decides how problems are reported, for evaluation, geometry building, and rendering alike |
| `Camera` | `null` | A Render3D `Camera`; `null` fits the view to the model. An invalid camera is rejected by `Render` before evaluation. `BuildScene` ignores it |
| `ViewAzimuthDegrees` | 45 | Azimuth of the fitted view (rotation about +Z); finite. Used only without a `Camera` |
| `ViewElevationDegrees` | about 35.26 | Elevation of the fitted view; finite. Used only without a `Camera` |
| `Projection` | `Perspective` | Projection of the fitted view (`CameraProjection.Perspective` or `Orthographic`). Used only without a `Camera` |
| `SceneOptions` | `null` (Render3D defaults) | The `SceneRenderOptions` of the renderer: background color (white by default), default surface color (neutral gray), lights, supersampling, parallelism, and fragment budget. Its `UnsupportedFeatureHandler` and `ThrowOnUnsupportedFeature` are **ignored**; reporting always goes through `Evaluation` |
| `FlatShapeThickness` | 0.1 | Thickness in model units of the slab that represents a 2D shape (`square`, `circle`, `polygon`) rendered without an extrusion; finite and greater than zero |
| `HighlightColor` | `(255, 64, 128, 110)`, a translucent pink | The color of the translucent copy that the `#` modifier draws over a highlighted solid (see *Transparency, Ghosts, and Highlights*). Any alpha is accepted: 255 draws an opaque copy and 0 an invisible one |
| `GhostColor` | `(128, 128, 128, 80)`, a translucent gray | The color of the translucent ghost that the `%` modifier draws instead of a solid. Any alpha is accepted |
| `FontResolution` | `null` (a default `ScadFontResolutionOptions`) | Where `text()` finds a font that the model names with `font = ...` (see *Text*): `UseSystemFonts` (default `true`) and `AdditionalDirectories` (default empty; copied and validated when set, a null, empty, or white-space directory is an `ArgumentException`). Text without a `font` argument always uses the bundled font and ignores these options |
<!-- markdownlint-enable MD013 -->

A `color()` in the model overrides `SceneOptions.DefaultColor` (and the alpha of a `color()` is rendered; the alpha of
`DefaultColor` is ignored). `ScadLimits` (in `Evaluation.Limits`) bounds every
resource of a call; besides the evaluation limits (`MaxRecursionDepth`, `MaxEvaluationSteps`,
`MaxLoopIterations`, `MaxListLength`, `MaxStringLength`, `MaxIncludeDepth`, `MaxIncludedFiles`, `MaxInstances`,
`MaxOutputLength`, `MaxDiagnostics`) it has `MaxTriangles` (a `long`, default 5,000,000, at least 1), the bound on
the number of triangles the model's geometry may need. The exact number of triangles of every sphere, cylinder,
polyhedron, slab, and extrusion (including the caps of a profile with holes) is computed **before** it is
allocated, so a hostile model such as `sphere(1, $fn = 100000)`
fails with a located `ScadEvaluationException` instead of exhausting memory.

**Variables (`-D`).** `ScadEvaluationOptions.Variables` replaces every top-level assignment of the named variable
in the document (and creates the variable when it is absent), including special variables such as `$fn`; values
may be `null`, booleans, numbers, strings, or sequences of these (a sequence becomes a vector). Names must be
ordinary identifiers (optionally starting with `$`).

### Supported OpenSCAD Subset

Everything below is built to a Render3D scene by `BuildScene` and `Render`. Arguments follow the OpenSCAD-language
forms (positional or named); an unknown, surplus, or repeated argument is a warning, and `$fn`, `$fa`, `$fs`
(see *Curve Resolution*) apply to the curved primitives. `convexity` arguments are accepted and ignored.

<!-- markdownlint-disable MD013 -->
| Module | Forms | Notes |
| ------ | ----- | ----- |
| `cube` | `cube(size, center)`; `size` is a number or `[x, y, z]` | Origin corner unless `center` |
| `sphere` | `sphere(r)` or `sphere(d = ...)` | `d` overrides `r`; the number of fragments follows `$fn`, `$fa`, and `$fs`, except that a radius of 1e-8 or less always gives 3 fragments, even with `$fn` set |
| `cylinder` | `cylinder(h, r, r1, r2, d, d1, d2, center)` | Cones and frusta with `r1`/`r2` (per-end values override `r`/`d`); `d1`/`d2` are strongest |
| `polyhedron` | `polyhedron(points, faces, convexity)`, or the legacy `triangles` when `faces` is absent | Faces list their points **clockwise seen from outside**; a solid supplied counter-clockwise is reversed with a warning. Concave planar faces are triangulated; an open shape is kept |
| `square`, `circle`, `polygon` | `square(size, center)`, `circle(r)` or `circle(d = ...)`, `polygon(points, paths)` | 2D shapes; `paths` gives holes and islands (nesting parity), in either winding. A 2D shape that is not extruded is drawn as a thin slab (`FlatShapeThickness`) |
| `text` | `text(text, size, font, halign, valign, spacing, direction, language, script)` | A 2D shape of glyph outlines; see *Text* below for the size, font, alignment, and direction rules and the limits of the dialect |
| `translate`, `scale`, `mirror` | `translate(v)`, `scale(v)` with a number or vector, `mirror(v)` (reflection through the plane with normal `v`) | Work on solids and 2D shapes |
| `rotate` | `rotate([x, y, z])` (degrees; X first, then Y, then Z), `rotate(a)` (about Z), `rotate(a, v)` (about an axis) | |
| `multmatrix` | `multmatrix(m)` with a 4 x 4 (or 3 x 4) column-vector matrix | A bottom row other than `[0, 0, 0, 1]` is `scad-multmatrix-projective` |
| `resize` | `resize(newsize, auto)` | Scales about the origin so the children reach `newsize`; `auto` keeps proportions |
| `color` | `color(c, alpha)`; `c` is a CSS color name (case-insensitive, including `"transparent"`), `"#RGB"`, `"#RGBA"`, `"#RRGGBB"`, `"#RRGGBBAA"`, or `[r, g, b]`/`[r, g, b, a]` with components 0 to 1 | The innermost color wins, alpha included. An alpha below 1 (from `alpha`, which overrides the alpha inside `c`, from the fourth component, from the hex form, or from `transparent`) makes the surface **translucent** (see *Transparency, Ghosts, and Highlights*); an `alpha` without a `c` is a warning |
| `union`, `render`, `difference`, `intersection`, `intersection_for` | Booleans of solids and of 2D shapes | `difference` subtracts every later child from the first; an empty first child gives an empty result; mixed 2D and 3D children keep the dimension of the first operand and warn about the others |
| `linear_extrude` | `linear_extrude(height, center, convexity, twist, slices, scale)` | `height` defaults to 100; a positive `twist` turns **clockwise seen from +Z**; `scale` is a number or `[sx, sy]` (both zero makes an apex) |
| `hull` | `hull() { ... }` of 3D children or of 2D children | The convex hull of every vertex of the children, in their transformed positions (a union of children, whether groups, loops, or user modules, is one point set). 3D children give one closed convex solid that is a valid operand of later booleans; 2D children give a convex shape that `linear_extrude`, `rotate_extrude`, and the top-level slab consume. Colors inside are dropped with a warning (the hull takes the enclosing `color`). A `difference` or `intersection` among the children is `scad-hull-csg-operand`, mixed 2D and 3D children are `scad-hull-mixed-dimensions`, and points that span no volume (or area) are `scad-hull-degenerate`; the hull is omitted in each case, never approximated |
| `minkowski` | `minkowski(convexity) { ... }` of convex 3D children | The Minkowski sum of the children, folded from the left: `minkowski() { cube([10, 6, 4]); sphere(1); }` is the block with rounded edges and corners. A union among the children distributes over the sum. Every child must be **convex**: a block, a sphere, a cylinder, a `hull`, a convex `polyhedron`, or a union of those; a `difference`, an `intersection`, or a hollow, notched, or otherwise non-convex mesh is `scad-minkowski-nonconvex` (also for a single child), and the module is omitted. A sum of 2D children is deferred (`scad-minkowski-2d`), mixed 2D and 3D children are `scad-minkowski-mixed-dimensions`, and more than 4,096 convex parts or sums, or more than 2,097,152 pairwise vertex sums for two parts or in total for the whole module, is `scad-minkowski-too-complex`. `convexity` is ignored; colors inside are dropped with a warning (also for a lone child, so an enclosing `color` applies) |
| `rotate_extrude` | `rotate_extrude(angle, convexity)` | Profile X is the radius; the sweep starts at +X and runs counter-clockwise; `angle` defaults to 360, larger values are clamped, negative values sweep clockwise; a profile on the negative side of the axis is swept where it lies (a point at `(-4, 0)` with `angle = 90` sweeps from 180 to 270 degrees); a profile with vertices on both sides of the axis is `scad-rotate-extrude-axis-crossing` |
| `!` (show only) | `!cube(5);` | The marked subtree becomes the new root of the design, **without** the transforms and colors of its ancestors: `translate([10, 0, 0]) { !cube(1); }` renders the cube at the origin |
| `*` (disable) | `*cube(5);` | The subtree is ignored |
| `#` (highlight) | `#cube(5);` | The subtree is rendered normally **and** as a translucent copy in `HighlightColor`; it still takes part in the booleans around it. On a 2D shape that is not extruded it is reported as `scad-modifier-highlight` (see the next section) |
| `%` (background) | `%cube(5);` | The subtree is drawn only as a translucent ghost in `GhostColor` that is not part of the model. On a 2D shape that is not extruded it is omitted and reported as `scad-modifier-background` (see the next section) |
<!-- markdownlint-enable MD013 -->

Language features (variables, functions, modules with `children()`, `for`, `if`, `let`, comprehensions, `echo`,
`assert`, `include` and `use`, and the built-in functions) are evaluated by the evaluator described in
*ScadDocument* above; the geometry builder never interprets them again.

#### Transparency, Ghosts, and Highlights

A `color()` with an alpha below 1, `%`, and `#` make surfaces translucent. The renderer composites the surfaces
along each viewing ray from front to back, deterministically (the same bytes for every degree of parallelism) and
over the background color of `SceneOptions` (a transparent background keeps a partial alpha). Because translucent
bodies and overlays are resolved like the operands of a boolean, **their meshes must be closed solids**; a
`polyhedron` that is not closed is reported through the `scad-csg-*` identifiers (when a handler or strict mode is
set), and the fragment budget `SceneOptions.MaxFragmentsPerTile` applies to them as to booleans.

- **A color with alpha.** `color("red", 0.5) cube(10);` is a half-transparent red cube: seen from above, the near and
  the far wall of the cube both show, each with the alpha of the color, over what lies behind. An alpha of 0 is
  invisible but still solid, so it still cuts and still counts for a `difference`. The innermost `color` wins,
  alpha included.
- **A union of translucent bodies shows the boundary of the union.** Overlapping members of one union merge, as in
  every constructive solid geometry result, so the faces of one member that lie inside another are not visible.
  To see through independent bodies, mark them with `%`.
- **`%` draws a ghost.** The subtree is drawn as a translucent body in `GhostColor` (its own colors are replaced)
  and is **not part of the model**: it adds nothing to a union, subtracts nothing from a `difference`, and
  intersects nothing (an `intersection` with a ghost is empty; a ghost as the **first** child of a `difference`
  leaves no ordinary geometry; the ghost is still drawn). It contributes no propagated bounds, so `resize`
  ignores it, and `hull` and `minkowski` ignore it too. It **is** part of `Scene.ComputeBounds()`, the bounds that
  fit the default camera, so a ghost cannot fall out of the frame (`ComputeBounds(includeOverlays: false)` leaves it
  out). This
  is a documented choice of this package, not a claim of parity with any other implementation.
- **`#` draws a highlight.** The subtree is rendered **normally** (its own colors, in the model) **and** as a
  translucent copy in `HighlightColor`. It keeps taking part in the booleans around it, so in
  `difference() { cube(10); #translate([2, 2, 2]) cube(6); }` the highlighted cutter still cuts, and its pink body is
  drawn inside the cut: the highlight is visible even where the subtree was subtracted. The triangles of the copy
  count a second time against `ScadLimits.MaxTriangles` (so a highlight on a very large subtree can reach the
  limit sooner). A `#` on a `hull` or `minkowski` child (at any depth below it) makes the child an ordinary part of
  the hull or sum: no copy is built or drawn, so it is not charged against the limit either; a `%` there contributes
  nothing, and a `minkowski` or `hull` of only a ghost builds nothing.
- **2D shapes.** A `#` or `%` on a 2D shape that is not extruded (a bare `square`, `circle`, or `polygon`, which is
  drawn as a slab) has no translucent form: `#` renders the slab normally and `%` omits it, and each is reported
  (`scad-modifier-highlight`, `scad-modifier-background`). The modifier on an **extrusion** of the shape, for example
  `%linear_extrude(2) circle(4);`, works fully.
- **The alpha of `SceneOptions.DefaultColor`, `BackFaceColor`, and lights is ignored.** Only the alpha of a model
  `color()` and of `HighlightColor` and `GhostColor` is honored.

Examples 25 and 27 show the reports and a complete transparent model.

#### Text

`text()` lays a string out with a TrueType font and builds a **flat 2D shape** of the glyph outlines (holes such as the
counter of an `O` are kept), so it works wherever a `square` does: extrude it with `linear_extrude` or
`rotate_extrude`, subtract it with a 2D `difference`, move it with `translate` and `rotate`, take its `hull`, or color
it. A text that is not extruded is drawn as a thin slab like any 2D shape. The arguments, in positional order:

<!-- markdownlint-disable MD013 -->
| Argument | Default | Meaning |
| -------- | ------- | ------- |
| `text` | required | The string. A number or another value is converted like `str()`. An empty string builds nothing and is not reported |
| `size` | 10 | The height in model units of the glyph `H` of the selected font, so a capital `H` is exactly `size` tall and stands on the baseline (`Y = 0`). A finite number greater than zero |
| `font` | the bundled Liberation Sans Regular | A family, `"Family"`, or a family with a style, `"Family:style=Bold Italic"` (the words `Bold` and `Italic`, or `Oblique`, in any case). See *Fonts* below |
| `halign` | `"left"` | `"left"`, `"center"`, or `"right"`: which point of the **ink** (the drawn outline, not the advance box) is at `X = 0`: the left edge, the middle, or the right edge |
| `valign` | `"baseline"` | `"baseline"` (the baseline is `Y = 0`), `"top"`, `"center"`, or `"bottom"`: the top edge, the middle, or the bottom edge of the ink at `Y = 0` |
| `spacing` | 1 | A finite number that is not negative that multiplies the advance of every glyph (not the kerning); `spacing = 0` overlaps the glyphs |
| `direction` | `"ltr"` | `"ltr"`; `"rtl"` (the characters in reverse order, laid out left to right); `"ttb"` and `"btt"` (upright characters stacked downwards or upwards by their advance, without kerning) |
| `language`, `script` | none | Accepted with any value and **ignored** |
<!-- markdownlint-enable MD013 -->

The curve resolution follows `$fn`, `$fa`, and `$fs` like a circle of radius `size / 2` does (see *Curve Resolution*):
the outlines are flattened so that they stay within the sagitta of such a circle. An invalid argument (for example a
`size` of 0, or a `halign` of `"middle"`) is a warning and the text is omitted.

```openscad
// A label cut into a plate: the text is a 2D shape, so it extrudes and subtracts like one
difference() {
    cube([60, 16, 4]);
    translate([30, 8, 2]) linear_extrude(height = 3) text("CanvasNet", size = 7, halign = "center", valign = "center");
}
```

**Fonts and determinism.** Without a `font` argument the text always uses the Liberation Sans Regular font that is
bundled with the core package, whatever fonts the machine has, so a model without a `font` renders identically
everywhere. A named font is resolved by `ScadRenderOptions.FontResolution`: first the family is searched in the
`AdditionalDirectories` (scanned recursively), then, when `UseSystemFonts` is `true` (the default), among the fonts
installed on the machine, then among the bundled `Liberation Sans`, `Liberation Serif`, and `Liberation Mono`
families (and the generic names `sans-serif`, `serif`, and `monospace`). Only an exact family name (case aside)
matches, and the closest bold and italic style of that family is used. A family that is found nowhere, or whose
font file cannot be loaded, gives one warning and the bundled sans font. **The result for a font that comes from
the installed fonts depends on the machine**, and the installed fonts are searched before the bundled families, so
even a request for a bundled family name (such as `Liberation Sans`) can be answered by a different font installed on
the machine. Only `UseSystemFonts = false` makes every named font reproducible.

```csharp
var options = new ScadRenderOptions
{
    FontResolution = new ScadFontResolutionOptions
    {
        UseSystemFonts = false,                       // do not look at the installed fonts
        AdditionalDirectories = ["./fonts"],          // but look here first
    },
};
```

**Limits of the dialect** (CanvasNetScad's own decisions; no parity with any other implementation is claimed): the
text is laid out one character at a time without shaping, ligatures, or combining marks, and `language` and
`script` change nothing, so right-to-left scripts are not shaped (`"rtl"` only reverses the characters); the kerning
is the one of the font's classic `kern` table; the size is the ink height of `H` and not a font-wide ascent;
vertical text stacks upright characters by their advance width (a tall letter can touch the next one; raise `spacing`
to separate them); a character the font does not map is drawn with the font's `.notdef`
glyph; control characters (such as line feeds) are skipped, so text does not wrap; the functions `textmetrics()` and
`fontmetrics()` are not supported (see *Unsupported Features and Strict Mode*). A glyph whose outline cannot be
represented (a damaged outline, or contours that cross each other) is reported as `scad-text-invalid-outline` or
`scad-text-overlapping-contours` and the whole text is omitted; none of the bundled fonts has such a glyph in Basic
Latin or Latin-1. Example 28 shows lettering, alignment, and the font options.

#### Curve Resolution

The number of segments of a circle of radius `r` follows a public, deterministic rule: a positive `$fn` gives
`max(3, $fn)`; otherwise it is `ceil(max(min(360 / $fa, 2 * pi * r / $fs), 5))`, never below 3. With the defaults
(`$fa = 12`, `$fs = 2`) a radius of 1 gives 5 segments and a radius of 10 gives 30. A sphere uses its own radius, a
cylinder the larger of its two radii, and `rotate_extrude` and a twisted `linear_extrude` the largest radius of
their profile. Increase `$fn` for smooth curves, for example `$fn = 64`; the triangle limit bounds the cost.

### Unsupported Features and Strict Mode

A construct the package recognizes but cannot build is reported as a `ScadUnsupportedFeature` with a stable,
lower-case `Feature` identifier, a message, and the `ScadSourceLocation` of the module (the file is named for
included and used files), and the rest of the model is still built. Reports are de-duplicated per location, so a
module in a loop is reported once. The identifiers are part of the documented behavior of the package:

<!-- markdownlint-disable MD013 -->
| Identifier | Trigger | Effect |
| ---------- | ------- | ------ |
| `scad-module-offset`, `scad-module-projection`, `scad-module-surface`, `scad-module-import`, `scad-module-roof` | The module is recognized but not implemented | The module and its children are omitted |
| `scad-text-invalid-outline` | A glyph outline of a `text()` is damaged, or a contour of a glyph crosses itself or has no area | The whole text is omitted |
| `scad-text-overlapping-contours` | The contours of one glyph of a `text()` overlap or touch each other, other than as a hole, an island, or the overlapping solids of a composite glyph | The whole text is omitted |
| `scad-hull-csg-operand` | A `hull` has a `difference` or an `intersection` among its children (3D or 2D, at any depth) | The hull is omitted |
| `scad-hull-degenerate` | The points of a `hull` span no volume (3D: fewer than four, collinear, or coplanar points) or no area (2D) | The hull is omitted |
| `scad-hull-mixed-dimensions` | A `hull` has both 2D and 3D children | The hull is omitted |
| `scad-minkowski-nonconvex` | A `minkowski` has a child that is a boolean or a mesh that is not convex (also a lone child); the message names the child by its position and source location | The module is omitted |
| `scad-minkowski-2d` | A `minkowski` of 2D children (deferred) | The module is omitted |
| `scad-minkowski-mixed-dimensions` | A `minkowski` has both 2D and 3D children | The module is omitted |
| `scad-minkowski-too-complex` | A `minkowski` has a child of more than 4,096 convex parts, a step of more than 4,096 sums, or more than 2,097,152 pairwise vertex sums for two parts or in total for the module | The module is omitted |
| `scad-modifier-highlight` | The `#` modifier on a 2D shape that is not extruded (on a solid, `#` is rendered and not reported) | The shape is rendered normally, without highlighting |
| `scad-modifier-background` | The `%` modifier on a 2D shape that is not extruded (on a solid, `%` is rendered as a ghost and not reported) | The shape is omitted |
| `scad-resize-approximate` | `resize` over children that contain a difference or an intersection | Estimated bounds are used |
| `scad-multmatrix-projective` | `multmatrix` with a bottom row other than `[0, 0, 0, 1]` | The module and its children are omitted |
| `scad-polygon-invalid` | A `polygon` (or a 2D shape after transformation under an extrusion) that crosses itself or has no area | That shape is omitted |
| `scad-polyhedron-face-invalid` | A polyhedron face that cannot be triangulated | The polyhedron is omitted |
| `scad-linear-extrude-scale` | A negative `scale` component, or exactly one zero component | The extrusion is omitted |
| `scad-linear-extrude-apex-multiple-contours` | `scale = 0` over a profile with holes or islands | The extrusion is omitted |
| `scad-rotate-extrude-axis-crossing` | The profile lies on both sides of the axis | The extrusion is omitted |
| `scad-rotate-extrude-axis-contact` | The profile touches the axis at an isolated vertex | The extrusion is omitted |
| `scad-geometry-rejected` | Any other geometry the Render3D tessellators and extruders reject, or an instance whose coordinates are not finite or exceed 1e100 in magnitude (for example `translate([1.7e308, 0, 0])`) | The affected geometry is omitted; the message says why |
| `scad-csg-open-mesh`, `scad-csg-non-manifold-edge`, `scad-csg-inconsistent-winding`, `scad-csg-degenerate-triangle`, `scad-csg-inverted-mesh`, `scad-csg-singular-transform` | `Render` found a defect in an operand of a `difference` or `intersection` (reported only when a handler or strict mode is set) | Reported at the module that produced the mesh; the image is still drawn |
| `scad-function-<name>`, `scad-search-form` | An unimplemented built-in function (`parent_module`, `textmetrics`, `fontmetrics`) or `search` argument form | The call evaluates to `undef` |
<!-- markdownlint-enable MD013 -->

Pass a handler to receive the reports (and the warnings) as they are delivered, or set
`ThrowOnUnsupportedFeature` to refuse a partial result: the first unsupported feature of any stage, after the
earlier events were delivered, is thrown as a `ScadUnsupportedFeatureException` that carries the `Feature` and the
`Location`. See Examples 23 to 25. The former identifier `scad-color-alpha` no longer exists: a color with an alpha
below 1 is rendered as a translucent surface (see *Transparency, Ghosts, and Highlights*).

### OpenSCAD Include Sandbox

`include` and `use` are resolved before evaluation and only through `ScadOpenOptions`: files under `BaseDirectory`
or a `LibraryPaths` entry, or, when a `FileResolver` callback is supplied, only that callback with no file-system
access at all. Absolute paths, drive letters, and `..` above the root are rejected with a
`ScadEvaluationException`, and a symbolic link or junction below a root is never followed.

**Security note.** The sandbox protects against path-based escapes in a *static* file tree. It is **not a
security boundary against a concurrent local attacker** who has write access to the included folders: the
link check and the file open are separate operations, so a component swapped for a link or junction between
them is not noticed, and a hard link (which cannot be detected) to a file outside the folders is read like any
other file. Only point `BaseDirectory` and `LibraryPaths` at folders that untrusted parties cannot write to, or
use a `FileResolver` callback to serve the files yourself.

Two further behaviors are worth knowing. Indexing is lenient: `v[i]` is `undef` for every negative or
non-finite index, and a fractional non-negative index is truncated (`[10, 20, 30][1.5]` is `20`). A very deeply
recursive program may need a dedicated 384 MiB-stack thread (address space, committed only as touched); where
the process cannot provide it, such as a 32-bit process, `Evaluate` throws a `ScadEvaluationException` (and
`Parse` a `ScadSyntaxException` for the corresponding 16 MiB parser thread) instead of a runtime
out-of-memory error.

### VegaLiteDocument

`VegaLiteDocument` (namespace `DemaConsulting.CanvasNet.VegaLite`) is distributed via the separate
`DemaConsulting.CanvasNet.VegaLite` NuGet package, whose only dependency is the core `DemaConsulting.CanvasNet`
package - see the Installation section of the project README. It is the single entry point for Vega-Lite
specifications and draws them as **static pictures**: a chart is a still image, so interaction (tooltips, links,
selections, bindings) has no meaning. The package implements a **defined subset** of the public Vega-Lite language
(see *Supported Vega-Lite Subset*); it does not imply general Vega-Lite support, and everything outside the subset is
reported, never skipped silently. It is derived with attribution from the Vega-Lite, Vega, and d3 projects and is not
affiliated with or endorsed by them (see *Vega-Lite Third-Party Attribution*). A document is **immutable and
thread-safe**: it is created from specification text (JSON), validated once, and can then be measured and rendered any
number of times, from several threads at once, each call giving the same result for the same options.

```csharp
public sealed partial class VegaLiteDocument
{
    public static VegaLiteDocument Parse(string json, VegaLiteOpenOptions? options = null);
    public static VegaLiteDocument Open(string path, VegaLiteOpenOptions? options = null);
    public static VegaLiteDocument Open(Stream stream, VegaLiteOpenOptions? options = null);

    public string? SourceName { get; }
    public string? SchemaUrl { get; }
    public int? SchemaMajorVersion { get; }
    public string? Title { get; }
    public IReadOnlyList<string> MarkTypes { get; }
    public IReadOnlyList<VegaLiteDataReference> DataReferences { get; }
    public IReadOnlyList<string> ParameterNames { get; }

    public VegaLiteSize Measure(
        VegaLiteRenderOptions? options = null,
        CancellationToken cancellationToken = default);

    public Surface Render(
        VegaLiteRenderOptions? options = null,
        CancellationToken cancellationToken = default);

    public Surface Render(
        int width,
        int height,
        VegaLiteRenderOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

- `Parse` and the two `Open` overloads read UTF-8 text (at most 4,194,304 UTF-16 characters by default; a leading byte
  order mark is ignored; invalid UTF-8, or a string with an unpaired surrogate, is rejected rather than replaced) and
  throw a `VegaLiteSpecException` with the location of the first problem when the text is not valid JSON, is nested too
  deeply, or has a known property of the wrong type or value. Plain **Vega** specifications (a `$schema` under
  `vega.github.io/schema/vega/`) are rejected. `Open(string)` names the source by the path; `Open(Stream)` reads from
  the current position and leaves the stream open. Opening never loads data, never opens another file, and never uses
  the network; only `Measure` and `Render` read data, and only as described in *Vega-Lite Data*.
- The descriptive properties (`SchemaUrl`, `SchemaMajorVersion`, `Title`, `MarkTypes`, `DataReferences`,
  `ParameterNames`) describe the text only: nothing is loaded or evaluated, so they are cheap and safe to read for a
  file you do not trust. A `VegaLiteDataReference` records the `Kind` of a data source as written (`Inline`, `Url`,
  `Name`, or `Sequence`), its `Url`, the `RowCount` when the text gives it, and its `Location`.
- `Measure` computes the size in pixels that `Render` produces for the same options, without drawing anything. It
  runs the same checks and delivers the same reports, so it is the cheap way to learn the size of a chart.
- `Render()` draws the chart onto a new `Surface` at its **natural size** (its `width` and `height` plus its axes,
  title, and padding) multiplied by `VegaLiteRenderOptions.Scale`. The chart is drawn sharply at the final size, not
  enlarged afterwards. The caller owns, and must dispose, the returned surface.
- `Render(width, height)` draws the chart onto a new surface of exactly `width` by `height` pixels: the natural chart
  is scaled uniformly to fit and centered, and the margins that remain take the background. `Scale` is not used,
  because the size decides the scale.

**Exceptions** (`Measure` and both `Render` overloads):

- `ArgumentOutOfRangeException`: Thrown, before any work, when the explicit size or the scaled natural size is outside
  1 to `Surface.MaxDimension` on a side.
- `ArgumentException`: Thrown when `VegaLiteRenderOptions.Parameters` names a parameter that the document does not
  declare.
- `VegaLiteSpecException`: Thrown, with the location of the construct, when an axis, scale, or format property has an
  invalid value that can only be found while laying out (for example an unknown axis orientation or a malformed d3
  format).
- `VegaLiteRenderException`: Thrown, with the location of the construct, when the data, scale, axis, or scene cannot
  be built or a limit of `VegaLiteLimits` is exceeded. A local data file that is missing, refused by the sandbox,
  behind a link, not valid UTF-8, or not valid JSON or delimited text, a `data.name` that nobody defines, and a
  document with no place to look for a local data file are all this exception, in lenient and strict mode alike (see
  *Vega-Lite Data*).
- `VegaLiteUnsupportedFeatureException`: Thrown at the first unsupported feature when
  `ThrowOnUnsupportedFeature` is set (see *Vega-Lite Unsupported Features and Strict Mode*).
- `OperationCanceledException`: Thrown, never wrapped, when the token is canceled before or during the work.
- `InvalidDataException`: Thrown by `Parse` and `Open`, without a location, when the text is longer than
  `MaxSourceLength`.

The locations carried by the exceptions and reports are `VegaLiteLocation` values: the `File` (the source name), the
RFC 6901 JSON `Pointer` of the construct, and its one-based `Line` and `Column` (counted in UTF-16 code units, with
`\n` as the only line terminator, so they are the same on every platform). `ToString()` gives
`file:line:column /pointer`, the form editors and build tools recognize.

### VegaLiteOpenOptions and VegaLiteRenderOptions

Both classes are immutable; every property is init-only and validated when set (fail-closed), collections are copied
and exposed read-only, so one instance can be shared between threads and calls.

<!-- markdownlint-disable MD013 -->
| `VegaLiteOpenOptions` property | Default | Meaning |
| ------------------------------ | ------- | ------- |
| `SourceName` | `null` (the path for `Open(string)`) | The name shown in locations and messages |
| `Limits` | `null` (`VegaLiteLimits.Default`) | The resource limits of parsing (the source length and the JSON depth) |
| `BaseDirectory` | `null` (the folder of the file for `Open(string)`) | The folder that a relative data `url` is looked up in first. A document opened from a string or a stream has none unless you set it. The current directory is never used |
| `DataPaths` | empty | More folders that a relative data `url` is looked up in, in order, after `BaseDirectory`; copied when set |
| `FileResolver` | `null` | A `Func<string, Stream?>` callback that replaces the file system: it receives a normalized logical path (forward slashes, no leading slash) and returns a readable stream of UTF-8 text, or `null` when the file does not exist. When it is set the file system is never touched. The library disposes the stream |
<!-- markdownlint-enable MD013 -->

<!-- markdownlint-disable MD013 -->
| `VegaLiteRenderOptions` property | Default | Meaning |
| -------------------------------- | ------- | ------- |
| `Scale` | 1 | The factor by which the natural size is multiplied (text included, so a larger scale gives a larger, equally sharp image); finite, greater than zero. The scaled size must lie within 1 and `Surface.MaxDimension` |
| `Background` | `null` | An `Rgba32` that replaces the background of the specification and of the configuration; `null` uses them (white when neither defines one). `new Rgba32(0, 0, 0, 0)` gives a transparent picture |
| `Config` | `null` | A Vega-Lite configuration as JSON object text, merged over the built-in defaults and the `config` of the specification (the caller wins). This is how a host applies a theme without editing the specification |
| `ContainerWidth`, `ContainerHeight` | 400, 300 | The size in pixels of a chart whose `width` or `height` is `"container"` |
| `Fonts` | `null` | A `VegaLiteFontOptions`: `UseSystemFonts` (default `false`) and `AdditionalDirectories` (default empty; copied and validated when set). See *Time Zone, Fonts, and Determinism* |
| `TimeZone` | `null` (UTC) | Accepted and reserved for the time support that is planned for a later release; there is no time scale yet |
| `Parameters` | `null` | Caller-supplied parameter values, validated and copied when set; accepted and reserved for the parameter support that is planned for a later release (they have no effect today). Values are `null`, `bool`, `string`, finite numbers, `DateTime`, or `DateTimeOffset` |
| `Datasets` | `null` | Named tables of rows for a `data.name`, copied when set; a caller dataset **replaces** a dataset of the same name in the specification's top-level `datasets`. Each row is a dictionary of field name to a value that is `null`, `bool`, `string`, a finite number, a `DateTime` (UTC; an unspecified kind is taken as UTC), or a `DateTimeOffset` (see *Vega-Lite Data*) |
| `Limits` | `VegaLiteLimits.Default` | The resource limits of the call (see below) |
| `WarningHandler` | `null` | Receives each `VegaLiteDiagnostic` (a warning); `null` drops them |
| `UnsupportedFeatureHandler` | `null` | Receives each `VegaLiteUnsupportedFeature`; `null` drops them |
| `ThrowOnUnsupportedFeature` | `false` | Turns the first unsupported feature into a `VegaLiteUnsupportedFeatureException` (strict mode) |
<!-- markdownlint-enable MD013 -->

`VegaLiteLimits` bounds every resource of a call. Its sixteen properties are init-only, each at least 1 (the depth
limits have an upper ceiling too), and the defaults are `MaxSourceLength` 4,194,304 characters, `MaxJsonDepth` 64,
`MaxDataFiles` 64, `MaxDataBytes` 64 MiB, `MaxDataRows` 1,000,000, `MaxFieldCount` 1,024, `MaxViews` 1,000,
`MaxCompositionDepth` 16, `MaxSceneItems` 2,000,000, `MaxScaleDomainSize` 100,000, `MaxTicks` 1,000,
`MaxExpressionLength` 4,096, `MaxExpressionDepth` 64, `MaxEvaluationSteps` 50,000,000, `MaxStringLength` 1,048,576, and
`MaxDiagnostics` 1,000 (the first report beyond it becomes a single `vl-diagnostics-suppressed` warning). The data,
scale, tick, and scene limits are checked **before** the memory is allocated, so a hostile specification fails with a
located `VegaLiteRenderException` instead of exhausting memory. `MaxSceneItems` counts every item of the scene, groups
included (the root and view groups, the title and its texts, each axis with its grid, ticks, labels, domain and title,
the marks group, and each mark), and an item is counted before it is created, so a limit of N allows a chart of exactly
N items and fails on the next one; tick labels that are dropped because they overlap are not items and cost nothing. The
data limits apply to each `Measure` or `Render` call: `MaxDataRows` is a **total** across every table that the call
creates (inline rows, files, datasets, and sequences), `MaxDataFiles` counts the distinct files read, `MaxDataBytes` is
the size of each file, and `MaxFieldCount` is the number of fields of a row (the columns of a file). The data is read
and checked before any scene item is counted. The expression limits belong to features that are planned for later
releases and are not reached today.

### Vega-Lite Data

A chart's rows can come from five sources. The source is chosen by precedence, not by the order of the keys: inline
`values`, then `url`, then `sequence`, then `name`. Everything is read while a chart is **measured or rendered**;
opening a document never reads a data file.

<!-- markdownlint-disable MD013 -->
| Source | Written as | Notes |
| ------ | ---------- | ----- |
| Inline rows | `"data": {"values": [{...}, ...]}` | An array of objects (one row each) or of primitives (one row `{"data": value}`); nested objects and arrays are flattened to fields named by their path (`a.b`, `p.r.0`) |
| A local file | `"data": {"url": "rows.csv"}` | A **relative** path only; see *Data files* and *The sandbox* below |
| A named dataset | `"data": {"name": "rows"}` | The caller's `VegaLiteRenderOptions.Datasets` first, then the top-level `datasets` of the specification; the caller wins. A name that nobody defines is an error |
| A sequence | `"data": {"sequence": {"start": 0, "stop": 5, "step": 1, "as": "x"}}` | The numbers from `start` up to, **not including**, `stop` in steps of `step` (1 by default; negative counts down) in the field `as` (`data` by default). `start` and `stop` must be finite numbers; a step that is present must be a finite number other than zero (an absent step is 1), and a step that points away from `stop` gives no rows. A missing, non-finite, or zero value fails the render, and so does a sequence longer than `MaxDataRows` |
<!-- markdownlint-enable MD013 -->

**Data files.** The format is `format.type` (`json`, `csv`, `tsv`, or `dsv`) when it is given; otherwise a path ending in
`.csv` or `.tsv` (in any case) is CSV or TSV, and **everything else is JSON**, as in Vega-Lite. A DSV names its
delimiter with `format.delimiter` (one character, a comma when omitted). `format.header` is ignored: the first line of
delimited text is always the header.

- *JSON*: the file must be an array of rows (primitives become `{"data": value}` rows), or an object whose rows are
  selected by `format.property`, a dotted path such as `"report.rows"` (or `"a[0].rows"`). JSON values keep their JSON
  types.
- *Delimited text (CSV, TSV, DSV)*: UTF-8 with or without a byte-order mark; lines end at LF, CRLF, or a lone CR; empty
  lines are skipped. A field is plain (a quote inside it is an ordinary character) or quoted with `"` (it may hold
  delimiters and line breaks, and `""` is one quote). Text after a closing quote, or a quote that is never closed, is
  an error. A row with **fewer** entries than the header has `null` in the missing fields, and **extra** entries are
  ignored. Repeated header names, and more columns than `MaxFieldCount`, are errors. A header such as `a.b` is
  addressed in an encoding as `a\.b`, like a JSON key that contains a dot.

**Types: `format.parse`.** Delimited text is read as text and, **as in Vega, nothing is converted unless you ask**: with
no `parse` property (or `"parse": null`) every cell of a CSV, TSV, or DSV file stays a **string**, so identifiers such as
`01234` and text such as `2024-03-05` or `true` are kept exactly as written, and there is no data-based inference of
column types. JSON files, inline rows, and datasets keep their JSON types. You ask for conversion in one of three ways:

- `"parse": "auto"` is the explicit opt-in to inference. It looks at the whole column and picks, in this order,
  **boolean** when every non-empty value is `true` or `false` (any case), **number** when every non-empty value is a
  number (`12`, `-2.5`, `+4`, `.5`, `1e3`, `007`), **date** when every non-empty value is an ISO 8601 date or date-time,
  and otherwise leaves the column as text. One value that does not fit keeps the **whole** column text. An empty value
  is `null` in a typed column. (On JSON data `"auto"` applies the same rule to columns that hold text.)
- `"parse": null` (or no `parse`) keeps every value as read (text stays text).
- `"parse": {"price": "number", "ok": "boolean", "day": "date", "id": "string"}` converts only the **named** fields
  (the keys are field paths) and leaves every other field as read: `number` reads a finite number (a boolean is 1 or 0,
  a date is its milliseconds since the
  epoch); `boolean` reads `true` or `false` (any case) or `1` or `0`, and a number is true unless it is zero; `date` reads
  an ISO 8601 text, or a number as milliseconds since the epoch; `string` is the text of the value (a number as
  JavaScript would write it, a date as `2024-03-05T00:00:00.000Z`). An empty value is `null` (except for `string`).
- A value that an explicit conversion **cannot read becomes `null`**, never a made-up number or date, and the chart is
  drawn: one warning `vl-data-parse-failed` per field and type tells the author how many values were affected and the
  first one (`3 value(s) of the field 'n' could not be read as number and are null (the first is in row 2: 'abc')`).
- ISO 8601 dates are `yyyy-MM-dd` (midnight UTC), and `yyyy-MM-ddTHH:mm`, `...:ss`, and `...:ss.fff` (up to seven digits
  of fraction), each with an optional `Z` or `+HH:mm` offset. A date-time **without** a zone is taken as UTC, so a
  chart never depends on the time zone of the machine. A date is an instant: its numeric value is the milliseconds since
  1970-01-01T00:00:00Z, and its text is `yyyy-MM-ddTHH:mm:ss.fffZ`. Dates are supported from the year 1 to the **last
  millisecond of the year 9999**; ISO text beyond that (for example `9999-12-31T23:59:59.9995`, a fraction of a
  millisecond past it) is **not a date**: under `"auto"` its column stays text, and under an explicit `date`
  conversion it becomes `null` with the usual `vl-data-parse-failed` warning.

*Numbers in unparsed text.* Because the cells stay text, a chart still works with an unparsed CSV: as in Vega, a
**quantitative** encoding coerces numeric text to a number when it draws it, so quantitative `x`, `y`, `size`, and
`opacity` fields (and a linear scale's domain) read `"12.5"` as 12.5 without any `parse`. This coercion is not a parse
mode, so the values stay text in every other use (a nominal field or a label). Quantitative `color` is not drawn
by the current subset. Parsing text as **dates** for temporal encodings, and the time scales that need it, arrive with the
time support (Phase 8): until then convert a date column with `parse` (ISO 8601), or write it in ISO 8601.

*Where this differs from Vega.* The conversion rules follow Vega's, with these deliberate
differences: a number is a finite decimal or exponent number (hexadecimal, thousands separators, `NaN`, and infinities
are not numbers, where JavaScript's conversion would accept some of them); a date is ISO 8601 only and always UTC
(Vega accepts any text that JavaScript's `Date.parse` reads, in more formats, and reads a date-time without a zone as
local time); and a custom time format (`"parse": {"d": "date:'%d/%m/%Y'"}`, also `utc:'...'`) is **recognized and not
applied**: the field is left as read and `vl-data-parse-date-format` is reported (an error in strict mode), because
time formatting arrives with the time support. Convert such a column to ISO 8601 in the file, or supply it as a date in
`Datasets`. There are no time scales yet, so where a quantitative value is needed a date reads as its number of
milliseconds, and as a
category it reads as its ISO text.

**Named datasets from code.** `VegaLiteRenderOptions.Datasets` maps a name to a list of rows, each row a dictionary
from field name to a value (`null`, `bool`, `string`, a finite number, a `DateTime`, or a `DateTimeOffset`; a
`DateTime` of unspecified kind is taken as UTC, so the result does not depend on the host; `DateTime.MaxValue` and
`DateTimeOffset.MaxValue`, which are often used to mean "no end", are represented by the last supported millisecond of
the year 9999 (`9999-12-31T23:59:59.999Z`) instead of failing). A name in `Datasets`
replaces a dataset of the same name in the specification. This is the way to chart data that you hold in memory without
writing a file. The same `format.parse` rules apply to a named dataset.

**The sandbox.** A relative `url` is looked for in the **base directory** first (the folder of a file opened with
`Open(path)`, or `VegaLiteOpenOptions.BaseDirectory`) and then in each `DataPaths` folder, in order; the first file
that exists wins. The rules are the same on every platform:

- Only **relative** paths are read. A path that is empty, starts with a slash, has a drive letter or a colon, is a UNC
  or network path, or contains a control character is refused. `.` and `..` are resolved, and a path that would leave
  its folder (for example `../secret.csv`) is refused even if the file exists. A backslash is the same as a slash.
- A **symbolic link or junction** inside a folder is never followed, whatever it points to: a file that is a link, or
  that lies below a directory that is a link, is refused before it is opened. The folder you name is trusted, including
  when you reach it through a link; to use linked content, name the real folder as a data path. A hard link cannot be
  recognized, and a file that is replaced by a link after it was checked (a race with a writer) is not noticed, so point
  `BaseDirectory` and `DataPaths` only at folders that untrusted parties cannot write to.
- With a **`FileResolver`** callback the file system is never touched: the callback is called on the calling thread
  with the normalized logical path (first as written, then once per valid `DataPaths` entry used as a prefix), at most
  once per path and call, and returns a stream (disposed by the library) or `null`. It is never called for a remote URL.
- A document parsed from a string or opened from a stream has **no folder**: a local `url` then needs
  `BaseDirectory`, `DataPaths`, or a `FileResolver`, or the render fails.
- A file is read as UTF-8 (a byte-order mark is removed); text that is not valid UTF-8, including UTF-16, is an error and
  is never repaired. Each file is read **once per call**, however often the chart names it, and again by the next call.

**Remote URLs.** A `url` with a scheme (`https:`, `file:`, `data:`, ...), a drive letter, an absolute path, or a network
path is never fetched, never opened, and never passed to a `FileResolver`: it is reported as `vl-data-remote-url`, and
the chart is drawn with an empty dataset (strict mode throws `VegaLiteUnsupportedFeatureException`).

**Errors.** A local file that cannot be used is **not** an empty chart. These fail the render with a
`VegaLiteRenderException` located at the `url` (or `name`) in the specification, in lenient and strict mode alike: no
place to look, a path the sandbox refuses, a link, a missing file, a directory where a file is expected, an existing file
that cannot be opened (for example for lack of permission, which is reported as a read failure and **never** as a
missing file), a file or a
`FileResolver` stream that cannot be read (including a callback that throws, which is reported as a read failure and
never as the callback's own exception), text that is not UTF-8, JSON that is invalid, nested too deeply, or not an array,
JSON text that is not valid Unicode (such as an escaped lone surrogate, `"\ud800"`, in a string or a property name),
a missing `format.property`, malformed delimited text, a name that nobody defines, a sequence with a missing,
non-finite, or zero value, and any data limit. A cancellation is never an error of this kind: it is always an
`OperationCanceledException`. Files are opened read-only and tolerate other programs reading, writing, renaming, or
deleting them at the same time.
`MaxDataFiles` (64 distinct files per call), `MaxDataBytes` (64 MiB per file; reading stops one chunk past the limit,
so a
stream that never ends cannot exhaust memory), `MaxDataRows` (1,000,000 rows in total per call, checked before the rows
are allocated, also for a sequence), `MaxFieldCount`, and `MaxStringLength` (a text longer than this) name themselves in
the message. Data is read before the scene is built, so a data failure never depends on `MaxSceneItems`.

**What is not read.** Geographic data (`topojson`, `sphere`, `graticule`, GeoJSON values), `values` given as text, and
remote URLs are reported (`vl-data-geo`, `vl-data-values-text`, `vl-data-remote-url`) and give an empty dataset; the
transforms that could reshape data (`calculate`, `filter`, `aggregate`, and the others) are not available yet and are
reported (`vl-transform-<name>`).

### Supported Vega-Lite Subset

The following is exactly what the package draws today. Anything else that Vega-Lite defines is **recognized and
reported** with a stable identifier (see *Vega-Lite Unsupported Features and Strict Mode*); the chart is still drawn
without it. A key that Vega-Lite does not define for a view, mark, encoding, or channel is a warning
(`vl-unknown-property`); unknown keys of the configuration are ignored.

<!-- markdownlint-disable MD013 -->
| Area | Supported |
| ---- | --------- |
| Schema | A `$schema` of Vega-Lite version 5 or 6, interpreted with the **version 6 defaults** (not the version 5 defaults, for example a view is 300 by 300 pixels by default); no `$schema` at all |
| Views | One single view. `layer`, `concat`, `hconcat`, `vconcat`, `facet`, and `repeat` are reported, and only the title, the background, and the empty view are drawn |
| Data | Inline `data.values` (an array of objects, one row per object with nested values flattened to path fields such as `a.b`, or of primitives, one row `{"data": value}`); a relative local `data.url` read as JSON, CSV, TSV, or DSV; `format.type`, `delimiter`, `property`, and `parse` (see *Vega-Lite Data*); named data (`data.name`, from the caller's `Datasets` or the top-level `datasets`); and `data.sequence`. Remote URLs, geographic data, and `values` given as text are not read |
| Marks | `bar`, `rect`, `point`, `circle`, `square`, `rule`, and `tick`, given as a string or as a definition object with `type` |
| Channels | `x`, `y`, `color`, `fill`, `stroke`, `size`, and `opacity`, each as a `field`, a literal `value`, or a literal `datum` where the channel permits it. `tooltip`, `href`, `description`, and `key` are accepted and ignored without a report, because they have no meaning in a still picture |
| Field types | `quantitative`, `nominal`, and `ordinal` |
| Encoding properties | `field`, `type`, `value`, `datum`, `title`, `scale`, `axis`, `legend` (as `null` only), `sort` (ascending, descending, `null`, or an array of values), and `bin` and `stack` (as `false` or `null` only) |
| Scales | The types linear, band, point, and ordinal, with the properties `type`, `domain`, `range`, `zero`, `nice`, `padding`, `reverse`, `paddingInner`, `paddingOuter`, `align`, `round`, `clamp`, and `scheme` (only `tableau10`, which is also the default categorical palette); a quantitative `domain` and `range` of more than two entries map piecewise exactly as in d3-scale (`min(domain length, range length)` entries are used, and `zero`, `nice` and padding change only the first and last entry), while a band or point scale uses the first two entries of its `range` |
| Axes | Grid, ticks, labels, domain, title, `orient`, `offset`, `minExtent`, `maxExtent`, `tickCount`, `tickMinStep`, `values`, a d3 `format`, the label properties (`labelAngle`, `labelAlign`, `labelBaseline`, `labelPadding`, `labelOffset`, `labelLimit`, `labelFlush`, `labelFlushOffset`, `labelOverlap`, `labelSeparation`), the font, color, opacity, dash, width, and cap properties of the parts, `titleAnchor`, `titleAngle`, `titlePadding`, and `zindex`, with the configuration precedence of Vega-Lite |
| Legends | **None**: a color, fill, stroke, size, or opacity channel driven by a field is drawn without a legend and reported (`vl-guide-legend`), unless `legend` or `scale` is `null` |
| Titles | `text` (a string or an array of lines), `subtitle`, `anchor`, `orient`, `offset`, `frame`, the font, color, and alignment properties, `limit`, `angle`, `dx`, and `dy` |
| Layout | `width` and `height` as numbers, `{"step": n}`, or `"container"`; `padding` as a number or an object; `autosize` of `pad` or `none`; `background`; the `view` configuration (size, fill, and stroke) |
| Configuration | The Vega-Lite 6 and Vega defaults, the `config` of the specification, and `VegaLiteRenderOptions.Config`, merged in that order; the `style` mechanism |
| Transforms, expressions, parameters, time | **None** (see the identifiers below) |
<!-- markdownlint-enable MD013 -->

The default colors, sizes, orientations, and layout constants follow Vega-Lite 6 and Vega; positions are derived from
the same rules, so a chart is laid out as Vega-Lite would lay it out up to the width of the text. Marks with a
missing, `null`, NaN, or non-numeric value on a continuous `x`, `y`, `size`, or `opacity` channel are dropped (the
Vega-Lite 6 behavior for marks that are not lines), and so are values outside an explicit discrete `domain`.
Discrete domains are ascending by default, with mixed kinds ordered null, boolean, number, and string. Marks are clipped
only with `"clip": true`. A line feed inside title text splits the title into lines, which Vega itself does not do.

### Time Zone, Fonts, and Determinism

Rendering is **deterministic**: two renders of one document with the same options are byte-identical, for every
degree of parallelism, on every machine and in every culture. This holds because the package consults no wall clock,
environment variable, or locale (numbers are formatted with the d3 `en-US` locale and the minus sign U+2212,
culture-invariant), the time zone defaults to UTC, and only the **bundled fonts** are used unless you say otherwise.

- **Fonts.** Text is measured and drawn with the bundled Liberation Sans, Liberation Serif, and Liberation Mono, which
  are metric-compatible with Arial, Times, and Courier and are used for the generic and Arial-like families a chart
  names. A browser draws with other fonts, so the widths of labels can differ from a
  browser's by the difference of the advance widths. `VegaLiteFontOptions.UseSystemFonts` is **`false` by default**
  (unlike `ScadFontResolutionOptions` and the core catalog) so that the output does not depend on the host; set it to
  `true`, and/or give `AdditionalDirectories`, to let a **named** family (`"font": "Verdana"`) be found in your
  directories first and then among the installed fonts. A family that cannot be found falls back to the bundled sans
  font.
- **Time.** There are no time scales yet: `temporal` fields, `timeUnit`, and `datetime` are reported
  (`vl-type-temporal`, `vl-encoding-timeunit`). The `TimeZone` option is accepted and reserved; it is UTC when `null`,
  so the output will not depend on the local zone of the machine when time support arrives.
- **No network.** The package never opens a socket. A `url` that names a scheme, a drive letter, an absolute path, or a
  network path is reported as `vl-data-remote-url` and is **never fetched, opened, or given to your `FileResolver`**;
  the chart is drawn with an empty dataset (strict mode throws). Download such a file next to the specification and
  refer to it by a relative path.

### Vega-Lite Unsupported Features and Strict Mode

A construct that Vega-Lite defines but the package cannot draw is reported as a `VegaLiteUnsupportedFeature` with a
stable, lower-case `Feature` identifier of the form `vl-<area>-<detail>`, a message, and the `VegaLiteLocation` of the
construct, and the rest of the chart is still drawn. A name taken from the specification is lower-cased without
separators (`timeUnit` gives `timeunit`). The identifiers are part of the documented behavior of the package and are
never renamed once released. A **warning** (`VegaLiteDiagnostic`) is a questionable construct that still renders: its
message starts with its `vl-*` identifier and a colon. Reports are delivered once, in document order, on the calling
thread, after the work finished and before the method returns or throws.

<!-- markdownlint-disable MD013 -->
| Identifier | Kind | Reported at, and when |
| ---------- | ---- | --------------------- |
| `vl-schema-version` | Warning | `$schema`, when the major version is not 5 or 6 or the URL names no Vega-Lite version (the chart is rendered with the version 6 defaults) |
| `vl-data-empty` | Warning | The `data` object, when it has an empty `values` array or no source key |
| `vl-data-remote-url` | Unsupported | The `data` object, when `url` has a scheme (including `file:`), a drive letter, or is an absolute path (`/etc/hosts`, a leading backslash), or a network or UNC path (never fetched; empty dataset). A relative local path is supported and is not reported |
| `vl-data-parse-date-format` | Unsupported | The `format.parse` entry that names a date with a custom format (`date:'%d/%m/%Y'`, `utc:'...'`); the field is left as read |
| `vl-data-parse-failed` | Warning | The `data` object, at render time: values that an explicit `format.parse` conversion could not read (one warning per field and type; those values are `null`) |
| `vl-data-values-text` | Unsupported | `data/values` given as a string, or a top-level dataset that `data.name` refers to given as a string |
| `vl-data-geo` | Unsupported | `sphere`, `graticule`, a topojson format, or `values` (or a referenced dataset) given as an object |
| `vl-mark-<type>` | Unsupported | `mark`, for every type except bar, rect, point, circle, square, rule, and tick; the chart is drawn without marks and without axes |
| `vl-channel-<name>` | Unsupported | A channel other than x, y, color, fill, stroke, size, and opacity (tooltip, href, description, and key are ignored) |
| `vl-channel-geo` | Unsupported | `longitude`, `latitude`, `longitude2`, `latitude2` |
| `vl-type-temporal`, `vl-type-geojson` | Unsupported | The `type` of a channel |
| `vl-scale-color-ramp` | Unsupported | A quantitative `color`, `fill`, or `stroke` field (the default mark color is used) |
| `vl-encoding-aggregate`, `-bin`, `-timeunit`, `-stack`, `-sort`, `-condition`, `-impute`, `-bandposition` | Unsupported | That property of a supported channel (`bin` and `stack` unless false or null; `sort` only for a shorthand or an object) |
| `vl-scale-<type>` | Unsupported | `scale/type`, for every type except linear, band, point, and ordinal |
| `vl-scale-property-<name>` | Unsupported | A scale property outside the supported list; `scheme` unless `tableau10` |
| `vl-guide-legend` | Unsupported | The legend, or the channel, of a field-driven color, fill, stroke, size, or opacity channel |
| `vl-guide-encode` | Unsupported | `axis.encode`, `legend.encode` |
| `vl-guide-axis-<name>` | Unsupported | `labelBound`, `tickBand`, `tickExtra`, `position`, `translate`, `bandPosition`, `titleX`, `titleY` of an axis |
| `vl-format-type-custom` | Unsupported | `axis.formatType` |
| `vl-expr-ref`, `vl-expr-calculate`, `vl-expr-filter`, `vl-expr-label` | Unsupported | An `{"expr": ...}` value in a title, size, padding, background, view, mark, or encoding; a calculate expression; a string filter; `axis.labelExpr` |
| `vl-transform-<name>` | Unsupported | Every entry of `transform` |
| `vl-composition-layer`, `-concat`, `-hconcat`, `-vconcat`, `-facet`, `-repeat` | Unsupported | That property; the nested views are examined too |
| `vl-param-variable`, `vl-param-selection` | Unsupported | Every entry of `params`; the legacy top-level `selection` |
| `vl-projection` | Unsupported | `projection` |
| `vl-layout-autosize-<type>` | Unsupported | `autosize` types other than pad and none (laid out as `pad`) |
| `vl-duplicate-key` | Warning | The later of two equal keys of one object (the last value wins) |
| `vl-unknown-property` | Warning | A key that Vega-Lite does not define for a view, mark definition, encoding, or channel definition |
| `vl-diagnostics-suppressed` | Warning | The first report beyond `MaxDiagnostics` |
<!-- markdownlint-enable MD013 -->

Pass handlers to receive the reports as they are delivered, or set `ThrowOnUnsupportedFeature` to refuse an
approximate chart: the first unsupported feature, after the earlier reports were delivered to the handlers, is thrown
as a `VegaLiteUnsupportedFeatureException` that carries the `Feature` and the `Location`. See Examples 30 and 31.
The identifiers `vl-data-file`, `vl-data-named`, `vl-data-sequence`, `vl-data-parse`, `vl-data-format-csv`,
`vl-data-format-tsv`, `vl-data-format-dsv`, and `vl-data-property` were reported by earlier releases and are no longer
produced, because local files, named data, sequences, `parse`, the delimited formats, and `format.property` are now
supported (see *Vega-Lite Data*); a build that matched one of them should stop doing so.
Planned later releases add, among other things, expressions and row transforms, aggregation and binning,
legends and further color schemes and scale types, time scales, line-like marks, layers, composition, and parameters;
until then the constructs are reported with the identifiers above. Geographic constructs (`projection`, `geoshape`,
`longitude` and `latitude`, topojson) are not planned and remain reported.

### Vega-Lite Third-Party Attribution

`DemaConsulting.CanvasNet.VegaLite` is **derived with attribution**; it is not a clean-room implementation. The
specification model, normalization, layout structure, and marks are original code written from the public
documentation, but a small number of values and algorithms were ported from the following projects, and no source
file, test, example, data set, or documentation text of the upstream projects was copied:

- **Vega-Lite** and **Vega** (BSD-3-Clause, Copyright (c) University of Washington Interactive Data Lab): default
  configuration values, scale inference rules, the title, axis, and view layout constants, and the axis geometry.
- **d3-array**, **d3-format**, **d3-scale**, and **d3-scale-chromatic** (ISC, Copyright Mike Bostock): the tick
  algorithms, the number format grammar, the arithmetic of the linear, band, and point scales, and the ten `tableau10`
  colors.

The license texts ship in the NuGet packages under `licenses/` (also for the `canvasnet` tool, which bundles the
assembly), and `src/DemaConsulting.CanvasNet.VegaLite/third-party/README.md` records what was ported, from which
version, and by which unit. The package is **not affiliated with, endorsed by, or promoted by** the Vega project, the
Vega-Lite project, the d3 project, or the University of Washington Interactive Data Lab; the names are used only to
describe the language that is implemented.

# Examples

## Example 1: Surface Pixel Access

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

using var surface = new Surface(4, 4);
surface[1, 1] = new Rgba32(255, 0, 0, 255); // opaque red pixel
var pixel = surface[1, 1];
Console.WriteLine(pixel.R); // Output: 255
```

## Example 2: Surface Crop

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

using var surface = new Surface(4, 4);
surface[1, 1] = new Rgba32(0, 255, 0, 255); // opaque green pixel

using var cropped = surface.Crop(1, 1, 2, 2);
Console.WriteLine(cropped.Width);  // Output: 2
Console.WriteLine(cropped.Height); // Output: 2
Console.WriteLine(cropped[0, 0].G); // Output: 255
```

## Example 3: Load, Crop, Resize and Save

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

using var original = PngCodec.Load("photo.png");
using var cropped = original.Crop(10, 10, 200, 100);
using var thumbnail = cropped.ResizeToFit(64, 64, ResamplingMode.Lanczos3);
PngCodec.Save(thumbnail, "thumbnail.png");
```

## Example 4: BMP Load/Save

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

using var surface = new Surface(2, 2);
surface[0, 0] = new Rgba32(255, 0, 0, 128); // semi-transparent red pixel

// Save with alpha preserved (Bit32, the default)
BmpCodec.Save(surface, "surface.bmp");
using var loaded = BmpCodec.Load("surface.bmp");
Console.WriteLine(loaded[0, 0].A); // Output: 128

// Save without alpha (Bit24) - reloading always yields opaque pixels
BmpCodec.Save(surface, "surface24.bmp", BmpBitDepth.Bit24);
using var loaded24 = BmpCodec.Load("surface24.bmp");
Console.WriteLine(loaded24[0, 0].A); // Output: 255
```

## Example 5: PNG Load/Save

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

using var surface = new Surface(2, 2);
surface[0, 0] = new Rgba32(0, 255, 0, 128); // semi-transparent green pixel

// Save with alpha preserved (Rgba, the default)
PngCodec.Save(surface, "surface.png");
using var loaded = PngCodec.Load("surface.png");
Console.WriteLine(loaded[0, 0].A); // Output: 128

// Save without alpha (Rgb) - reloading always yields opaque pixels
PngCodec.Save(surface, "surface-rgb.png", PngColorType.Rgb);
using var loadedRgb = PngCodec.Load("surface-rgb.png");
Console.WriteLine(loadedRgb[0, 0].A); // Output: 255
```

## Example 6: TIFF Load/Save

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

using var surface = new Surface(2, 2);
surface[0, 0] = new Rgba32(0, 0, 255, 128); // semi-transparent blue pixel

// Save uncompressed (the default)
TiffCodec.Save(surface, "surface.tiff");
using var loaded = TiffCodec.Load("surface.tiff");
Console.WriteLine(loaded[0, 0].A); // Output: 128

// Save with LZW compression and an automatic horizontal-differencing predictor
TiffCodec.Save(surface, "surface-lzw.tiff", TiffCompression.Lzw);
using var loadedLzw = TiffCodec.Load("surface-lzw.tiff");
Console.WriteLine(loadedLzw[0, 0].B); // Output: 255
```

## Example 7: JPEG Load/Save

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

using var surface = new Surface(16, 16);
surface.Clear(new Rgba32(255, 128, 0, 255)); // opaque orange everywhere

// Save at quality 90 (the default)
JpegCodec.Save(surface, "surface.jpg", 90);
using var loaded = JpegCodec.Load("surface.jpg");
Console.WriteLine(loaded[0, 0].A); // Output: 255

// JPEG is lossy, so compare color channels with a tolerance rather than exact equality
Console.WriteLine(Math.Abs(loaded[0, 0].R - surface[0, 0].R) <= 15); // Output: True
```

## Example 8: Compositing a Semi-Transparent Color Over a Background

```csharp
using DemaConsulting.CanvasNet.Canvas;

using var background = new Surface(1, 1);
background[0, 0] = new Rgba32(0, 255, 0, 255); // opaque green background

// Composite a semi-transparent red overlay over the background, in place
background.CompositeOver(new Rgba32(255, 0, 0, 128));
var result = background[0, 0];
Console.WriteLine($"{result.R} {result.G} {result.B} {result.A}"); // Output: 128 127 0 255

// CompositeOver(Surface) works the same way when the foreground is itself a Surface (for
// example, one loaded from a PNG file with an alpha channel), pixel by pixel across the whole
// surface rather than a single constant color.
```

## Example 9: Header-Only Probing Before Load (Decompression-Bomb Triage)

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using System.IO;

// GetInfo reads only the header - never pixel data - so it is safe to call on an untrusted or
// unexpectedly large file before deciding whether to fully decode it with Load.
var info = PngCodec.GetInfo("untrusted.png");

var pixelCount = (long)info.Width * info.Height;
var maxPixelCount = (long)Surface.MaxDimension * Surface.MaxDimension;
if (info.Width > Surface.MaxDimension || info.Height > Surface.MaxDimension || pixelCount > maxPixelCount)
{
    throw new InvalidDataException(
        $"Refusing to load a {info.Width}x{info.Height} image: exceeds Surface.MaxDimension.");
}

// Only decode pixel data once the header has been judged safe.
using var surface = PngCodec.Load("untrusted.png");
Console.WriteLine($"{surface.Width}x{surface.Height}, alpha: {info.HasAlpha}");
```

### Handling Unsupported Features

`GetInfo`'s `CanDecode` property lets a caller detect a well-formed-but-unsupported file (for
example an Adam7-interlaced PNG) before calling `Load` at all, instead of discovering it only via
an exception from `Load`:

```csharp
using DemaConsulting.CanvasNet.Codecs;

var info = PngCodec.GetInfo("untrusted.png");
if (!info.CanDecode)
{
    // Well-formed per the PNG specification, but declares a feature this library does not
    // implement (currently: Adam7 interlacing) - deny without attempting to decode.
    Console.WriteLine("This PNG uses a feature CanvasNet cannot decode.");
}
else
{
    using var surface = PngCodec.Load("untrusted.png");
}
```

A caller that commits directly to `Load` without checking `CanDecode` first can instead catch
`UnsupportedImageFeatureException`. This is a distinct type from `InvalidDataException`
(`InvalidDataException` itself being `sealed` in .NET prevents this type from deriving from it),
so an existing `catch (InvalidDataException)` block does **not** catch it. `InvalidDataException`
derives directly from `SystemException`, not `IOException`, so `IOException` is **not** a common
base that can catch both - a caller that wants to
handle both "malformed" and "well-formed but unsupported" files together must add two explicit
catch clauses, one per type:

```csharp
using DemaConsulting.CanvasNet.Codecs;

try
{
    using var surface = PngCodec.Load("untrusted.png");
}
catch (UnsupportedImageFeatureException ex)
{
    Console.WriteLine($"Unsupported feature '{ex.Feature}': {ex.Message}");
}
catch (InvalidDataException ex)
{
    Console.WriteLine($"Malformed PNG: {ex.Message}");
}
```

## Example 10: Filling a Vector Path

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using System.Numerics;

using var canvas = new Surface(64, 64);
var triangle = new PathBuilder()
    .MoveTo(new Vector2(8, 56))
    .LineTo(new Vector2(56, 56))
    .LineTo(new Vector2(32, 8))
    .Close()
    .Build();

// Antialiased solid fill using the default NonZero fill rule and 0.25f flatten tolerance
PathFiller.Fill(canvas, triangle, new Rgba32(0, 128, 255, 255));
Console.WriteLine(canvas[32, 40].A); // Output: 255 (well inside the triangle)
```

## Example 11: Stroking a Vector Path

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using System.Numerics;

using var canvas = new Surface(64, 64);
var polyline = new PathBuilder()
    .MoveTo(new Vector2(8, 48))
    .LineTo(new Vector2(32, 16))
    .LineTo(new Vector2(56, 48))
    .Build();

var style = new StrokeStyle(
    width: 6f,
    cap: LineCap.Round,
    join: LineJoin.Round,
    dashArray: [10f, 6f]);

var strokedOutline = PathStroker.Stroke(polyline, style);
PathFiller.Fill(canvas, strokedOutline, new Rgba32(255, 128, 0, 255));
Console.WriteLine(canvas[20, 32].A); // Output: 255 (on the centerline, inside one visible dash run)
```

## Example 12: Filling a Vector Path with a Radial Gradient

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using System.Numerics;

using var canvas = new Surface(64, 64);
var square = new PathBuilder()
    .MoveTo(new Vector2(4, 4))
    .LineTo(new Vector2(60, 4))
    .LineTo(new Vector2(60, 60))
    .LineTo(new Vector2(4, 60))
    .Close()
    .Build();

var gradient = new RadialGradient(
    startCenter: new Vector2(32, 32),
    startRadius: 0f,
    endCenter: new Vector2(32, 32),
    endRadius: 28f,
    stops:
    [
        new GradientStop(0f, new Rgba32(255, 255, 0, 255)),
        new GradientStop(1f, new Rgba32(255, 0, 0, 0))
    ],
    spread: GradientSpread.Pad);

PathFiller.Fill(canvas, square, gradient);
Console.WriteLine(canvas[32, 32].A); // Output: about 249 (nearly opaque; alpha fades toward the edge)
```

## Example 13: Loading a TrueType Font and Filling a Glyph Outline

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using System.Numerics;
// Disambiguate from System.IO.Path when implicit usings are enabled.
using Path = DemaConsulting.CanvasNet.Geometry.Path;

// glyf-flavored (.ttf) fonts produce QuadraticBezierTo segments; CFF/OpenType
// (.otf) fonts produce CubicBezierTo segments instead - both are handled here
// so this helper works for either outline flavor.
static Path TransformGlyph(Path glyph, float scale, float baselineY)
{
    var builder = new PathBuilder();

    Vector2 ToCanvas(Vector2 point) => new(point.X * scale, baselineY - point.Y * scale);

    foreach (var subpath in glyph.Subpaths)
    {
        builder.MoveTo(ToCanvas(subpath.Start));
        foreach (var command in subpath.Commands)
        {
            switch (command.Type)
            {
                case PathCommandType.LineTo:
                    builder.LineTo(ToCanvas(command.EndPoint));
                    break;
                case PathCommandType.QuadraticBezierTo:
                    builder.QuadraticBezierTo(
                        ToCanvas(command.Control1),
                        ToCanvas(command.EndPoint));
                    break;
                case PathCommandType.CubicBezierTo:
                    builder.CubicBezierTo(
                        ToCanvas(command.Control1),
                        ToCanvas(command.Control2),
                        ToCanvas(command.EndPoint));
                    break;
                case PathCommandType.Close:
                    builder.Close();
                    break;
            }
        }
    }

    return builder.Build();
}

var font = TrueTypeFont.Load("font.ttf"); // also accepts .otf, or a .ttc via the faceIndex overload
var glyphIndex = font.GetGlyphIndex('A');
var glyphOutline = font.GetGlyphOutline(glyphIndex);
var scale = 48f / font.UnitsPerEm;
var canvasOutline = TransformGlyph(glyphOutline, scale, baselineY: 56f);

using var surface = new Surface(64, 64);
PathFiller.Fill(surface, canvasOutline, new Rgba32(20, 120, 255, 255));
Console.WriteLine(font.GetAdvanceWidth(glyphIndex));
```

## Example 14: Decoding and Rasterizing an SVG Document

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Svg;
using System.IO;
using System.Text;

const string svg = """
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
      <rect x="10" y="10" width="80" height="80" fill="#0080ff"/>
    </svg>
    """;

// Probe the intrinsic size from the viewBox before rasterizing.
using var probeStream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
var info = SvgCodec.GetInfo(probeStream);
Console.WriteLine($"{info.Width}x{info.Height}"); // Output: 100x100

// Rasterize the document into a caller-chosen 64x64 surface (SvgCodec is decode-only).
using var loadStream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
using var surfaceSvg = SvgCodec.Load(loadStream, 64, 64);
Console.WriteLine(surfaceSvg[32, 32].A); // Output: 255 (well inside the filled rectangle)
```

## Example 15: Building and Rendering a Chart

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Charts;

// Build a validated Chart fluently: a two-category Column chart with one series.
var chart = new ChartBuilder()
    .OfType(ChartType.Column)
    .WithCategoryAxis(["Q1", "Q2"])
    .WithValueAxis(minimum: 0f, maximum: 10f)
    .AddSeries("Revenue", [4.0, 9.0], color: new Rgba32(31, 119, 180, 255))
    .WithTitle("Quarterly Revenue")
    .WithLegend(ChartLegendPosition.Bottom)
    .Build();

// Render it onto a new 400x300 surface using every rendering default.
using var chartSurface = ChartRenderer.Render(chart, 400, 300);
Console.WriteLine($"{chartSurface.Width}x{chartSurface.Height}"); // Output: 400x300
```

## Example 16: Parsing and Rendering an OOXML Chart

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Charts;
using DemaConsulting.CanvasNet.Charts.OpenXml;
using System.Xml.Linq;

// Parse a c:chartSpace/c:chart XML document (for example, a chart1.xml OPC part a host
// document-format library has already located and opened). This hand-authored fragment mirrors
// the shape a real PowerPoint chart part's bar chart would carry.
var chartDocument = XDocument.Parse("""
    <c:chartSpace xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart"
                  xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
      <c:chart>
        <c:plotArea>
          <c:barChart>
            <c:barDir val="col"/>
            <c:ser>
              <c:tx><c:v>Revenue</c:v></c:tx>
              <c:cat>
                <c:strRef>
                  <c:strCache>
                    <c:ptCount val="2"/>
                    <c:pt idx="0"><c:v>Q1</c:v></c:pt>
                    <c:pt idx="1"><c:v>Q2</c:v></c:pt>
                  </c:strCache>
                </c:strRef>
              </c:cat>
              <c:val>
                <c:numRef>
                  <c:numCache>
                    <c:ptCount val="2"/>
                    <c:pt idx="0"><c:v>4.0</c:v></c:pt>
                    <c:pt idx="1"><c:v>9.0</c:v></c:pt>
                  </c:numCache>
                </c:numRef>
              </c:val>
            </c:ser>
          </c:barChart>
        </c:plotArea>
        <c:legend><c:legendPos val="b"/></c:legend>
      </c:chart>
    </c:chartSpace>
    """);
var parsedChart = OpenXmlChartParser.Parse(chartDocument);
Console.WriteLine(parsedChart.Type); // Output: Column

// The parsed Chart is directly consumable by ChartRenderer, exactly like a Chart built via
// ChartBuilder.
using var parsedChartSurface = ChartRenderer.Render(parsedChart, 400, 300);
Console.WriteLine($"{parsedChartSurface.Width}x{parsedChartSurface.Height}"); // Output: 400x300
```

## Example 17: Opening and Rendering a Visio (.vsdx) Diagram

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Vsdx;

using var doc = VsdxDocument.Open("diagram.vsdx");
Console.WriteLine(doc.PageCount); // Output: the number of pages in the document

// Each page reports its own declared name and size (converted to EMU, English Metric Units).
var pageInfo = doc.GetPageSize(0);
Console.WriteLine(pageInfo.Name);

// Render the first page at 150 DPI - Master/StyleSheet inheritance, connectors/arrowheads, and
// nested groups are all resolved automatically.
using var surface = doc.Render(0, 150);
Console.WriteLine($"{surface.Width}x{surface.Height}");
```

## Example 18: Rendering a PowerPoint (.pptx) Slide

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Pptx;

using var deck = PptxDocument.Open("slides.pptx");

// Render the first slide at 96 DPI, reporting any EMF/WMF pictures that had to be skipped.
var options = new PptxRenderOptions
{
    OnUnsupportedPictureSkipped = ex => Console.WriteLine($"Skipped picture: {ex.Message}"),
};
using var slide = deck.Render(0, 96f, options);
PngCodec.Save(slide, "slide1.png");
```

## Example 19: Rendering a PDF Page

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Pdf;

using var pdf = PdfDocument.Open("document.pdf");
Console.WriteLine(pdf.PageCount);

// Render the first page at 150 DPI onto a transparent background.
var options = new PdfRenderOptions { BackgroundColor = new Rgba32(0, 0, 0, 0) };
using var page = pdf.Render(0, 150f, options);
PngCodec.Save(page, "page1.png");
```

## Example 20: Rendering a 3D Scene

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

// Build a scene: a gray base plate, a red sphere, and a blue cone. Nodes without an appearance
// use SceneRenderOptions.DefaultColor (neutral gray).
var plate = new MeshNode(PrimitiveTessellator.Box(new Vector3D(10, 10, 1), center: true))
{
    Transform = Matrix4x4D.CreateTranslation(new Vector3D(0, 0, -0.5)),
};
var ball = new MeshNode(PrimitiveTessellator.Sphere(2, 48))
{
    Transform = Matrix4x4D.CreateTranslation(new Vector3D(-2, 0, 2)),
    Appearance = new NodeAppearance(new Rgba32(200, 40, 40, 255)),
};
var cone = new MeshNode(PrimitiveTessellator.Cylinder(4, 1.5, 0, 48))
{
    Transform = Matrix4x4D.CreateTranslation(new Vector3D(2.5, 0, 0)),
    Appearance = new NodeAppearance(new Rgba32(40, 80, 200, 255)),
};
var scene = new Scene(new GroupNode([plate, ball, cone]));

// A quick preview: an isometric view that frames the whole scene automatically
using var preview = SceneRenderer.RenderFitted(scene, 640, 480);
PngCodec.Save(preview, "scene-preview.png");

// A full render: an explicit camera, custom lights, a transparent background, 4 x 4
// supersampling, and a 30-second cancellation timeout
var camera = Camera.FitToBounds(
    scene.ComputeBounds(),
    azimuthDegrees: 30,
    elevationDegrees: 25,
    aspectRatio: 640.0 / 480.0);
var options = new SceneRenderOptions
{
    BackgroundColor = new Rgba32(0, 0, 0, 0),
    Lights =
    [
        new AmbientLight { Intensity = 0.3f },
        new DirectionalLight { Direction = new Vector3D(-1, 1, -1), Intensity = 0.8f },
    ],
    Supersampling = 4,
    MaxDegreeOfParallelism = Environment.ProcessorCount,
};
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
using var render = SceneRenderer.Render(scene, camera, 640, 480, options, cts.Token);
PngCodec.Save(render, "scene.png");
Console.WriteLine(render[0, 0].A); // Output: 0 (the corner is untouched transparent background)
```

## Example 21: Cube Minus Sphere (Constructive Solid Geometry)

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

// A red 4 x 4 x 4 cube from which a blue sphere is subtracted. The sphere is centered in the cube and
// larger than the cube's inscribed sphere, so it hollows the cube out and cuts a round window in
// every face. The surface exposed by the subtraction takes the sphere's color. Both operands are
// closed, outward-oriented meshes, as constructive solid geometry requires.
var cube = new MeshNode(PrimitiveTessellator.Box(new Vector3D(4, 4, 4), center: true))
{
    Appearance = new NodeAppearance(new Rgba32(200, 60, 50, 255)),
};
var hollow = new MeshNode(PrimitiveTessellator.Sphere(2.6, 64))
{
    Appearance = new NodeAppearance(new Rgba32(50, 110, 210, 255)),
};
var scene = new Scene(new CsgNode(CsgOperation.Difference, [cube, hollow]));

// Collect operand problems instead of failing: a tolerant render still produces an image
var problems = new List<SceneRenderUnsupportedFeature>();
var options = new SceneRenderOptions
{
    UnsupportedFeatureHandler = problems.Add,
    MaxFragmentsPerTile = 8 * 1024 * 1024, // raise the default memory budget (4 Mi fragments per tile)
};

try
{
    using var image = SceneRenderer.RenderFitted(scene, 640, 480, options);
    PngCodec.Save(image, "cube-minus-sphere.png");
    Console.WriteLine(problems.Count); // Output: 0 (both operands are closed solids)
}
catch (RenderBudgetExceededException ex)
{
    // A tile needed more fragments than allowed: raise MaxFragmentsPerTile or lower Supersampling
    Console.WriteLine($"Scene too complex for a budget of {ex.MaxFragmentsPerTile} fragments per tile");
}

// Strict mode: fail fast when an operand is not a closed solid (here an open, single-triangle mesh)
var openMesh = new MeshBuilder();
openMesh.AddTriangle(
    openMesh.AddVertex(new Vector3D(0, 0, 0)),
    openMesh.AddVertex(new Vector3D(1, 0, 0)),
    openMesh.AddVertex(new Vector3D(0, 1, 0)));
var broken = new Scene(new CsgNode(CsgOperation.Difference, [cube, new MeshNode(openMesh.Build())]));
try
{
    using var failed = SceneRenderer.RenderFitted(
        broken, 640, 480, new SceneRenderOptions { ThrowOnUnsupportedFeature = true });
}
catch (SceneRenderUnsupportedFeatureException ex)
{
    Console.WriteLine(ex.Feature); // Output: render3d-csg-open-mesh
}
```

## Example 22: Extruding a Polygon With a Hole

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

// A 20 x 20 square plate with a round hole: the second contour lies inside the first, so it is a
// hole (the winding of the contours does not matter). Invalid polygons, such as a self-intersecting
// bow tie or contours that touch, throw ArgumentException here, when the region is created.
var plate = new PolygonRegion(
[
    PrimitiveTessellator.Rectangle(new Vector2D(20, 20), center: true).Contours[0],
    PrimitiveTessellator.Circle(5.0, 48).Contours[0],
]);

// Extrude 4 units along Z: one closed solid with a through-hole, outward-oriented
var plateMesh = Extruder.Linear(plate, 4.0, center: true);
Console.WriteLine(plateMesh.IsClosed); // Output: True

// A star twisted by 90 degrees (positive is counter-clockwise from above) and tapered to a point.
// Twisted solids are approximated by slices, so use enough of them.
var star = PrimitiveTessellator.Polygon(Enumerable.Range(0, 10)
    .Select(i => new Vector2D(
        (i % 2 == 0 ? 3.0 : 1.2) * Math.Cos(i * Math.PI / 5.0),
        (i % 2 == 0 ? 3.0 : 1.2) * Math.Sin(i * Math.PI / 5.0)))
    .ToArray());
var spire = Extruder.Linear(star, 8.0, twistDegrees: 90.0, slices: 32, scale: Vector2D.Zero);

// A vase: revolve a profile (X = radius, Y = height) that stays on one side of the Z axis
var vase = Extruder.Rotate(
    PrimitiveTessellator.Polygon([new(0, 0), new(3, 0), new(4, 3), new(2, 6), new(2.5, 8), new(0, 8)]),
    360.0,
    48);

var scene = new Scene(new GroupNode(
[
    new MeshNode(plateMesh) { Appearance = new NodeAppearance(new Rgba32(70, 130, 180, 255)) },
    new MeshNode(spire)
    {
        Transform = Matrix4x4D.CreateTranslation(new Vector3D(0, 0, 2)),
        Appearance = new NodeAppearance(new Rgba32(220, 170, 60, 255)),
    },
    new MeshNode(vase)
    {
        Transform = Matrix4x4D.CreateTranslation(new Vector3D(25, 0, -2)),
        Appearance = new NodeAppearance(new Rgba32(190, 90, 80, 255)),
    },
]));

using var image = SceneRenderer.RenderFitted(scene, 800, 400);
PngCodec.Save(image, "extrusions.png");
```

## Example 23: Rendering an OpenSCAD Model

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Scad;

// A small bracket: a plate with two through-holes and a rounded boss. The model is plain OpenSCAD-language
// source; ScadDocument.Open("bracket.scad") would read the same text from a file.
var document = ScadDocument.Parse("""
    $fn = 48;
    module boss(r, h) { cylinder(r = r, h = h); }

    difference() {
        union() {
            cube([40, 24, 4], center = true);
            translate([0, 0, 2]) boss(8, 6);
        }
        for (x = [-14, 14]) translate([x, 0, 0]) cylinder(r = 2.5, h = 10, center = true);
        cylinder(r = 4, h = 20, center = true);
    }
    """);

// Render a fitted isometric view; the caller owns the surface. Surfaces are neutral gray on white by default.
using var image = document.Render(800, 600);
PngCodec.Save(image, "bracket.png");

// Or build the Render3D scene to measure it, or to render it yourself with another camera. (The bounds of a
// scene include every operand of a boolean, so the cutters count towards the height; X and Y are the plate.)
var scene = document.BuildScene();
var bounds = scene.ComputeBounds();
Console.WriteLine($"{bounds.Max.X - bounds.Min.X} x {bounds.Max.Y - bounds.Min.Y}");
// Output: 40 x 24
```

## Example 24: Customizing the View, Colors, and Variables

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Scad;

var document = ScadDocument.Parse("""
    pegs = 3;      // can be overridden from outside, like a command-line -D pegs=5
    pitch = 10;
    $fn = 32;

    color("steelblue") cube([pegs * pitch, pitch, 4]);
    for (i = [0 : pegs - 1])
        color(i % 2 == 0 ? "tomato" : "gold")
            translate([pitch / 2 + i * pitch, pitch / 2, 4]) cylinder(r = 3, h = 6);
    """);

// 1. A fitted view from the front-right, in orthographic projection, on a dark background with smooth edges
var look = new ScadRenderOptions
{
    ViewAzimuthDegrees = 20,
    ViewElevationDegrees = 25,
    Projection = CameraProjection.Orthographic,
    SceneOptions = new SceneRenderOptions
    {
        BackgroundColor = new Rgba32(32, 34, 40, 255),
        Supersampling = 4,
    },
};
using var front = document.Render(800, 400, look);
PngCodec.Save(front, "pegs-front.png");

// 2. An explicit camera looking straight down at the model (a top view, model units)
var top = new ScadRenderOptions
{
    Camera = new Camera
    {
        Eye = new Vector3D(25, 5, 100),
        Target = new Vector3D(25, 5, 0),
        Up = Vector3D.UnitY,
        Projection = CameraProjection.Orthographic,
        OrthographicHeight = 20,
    },
};
using var plan = document.Render(800, 400, top);
PngCodec.Save(plan, "pegs-top.png");

// 3. Override variables from outside the source (the same as -D pegs=5 -D pitch=12), including special variables
var variant = new ScadRenderOptions
{
    Evaluation = new ScadEvaluationOptions
    {
        Variables = new Dictionary<string, object?> { ["pegs"] = 5, ["pitch"] = 12, ["$fn"] = 64 },
    },
};
using var longer = document.Render(800, 400, variant);
PngCodec.Save(longer, "pegs-five.png");

// 4. Bound the work for models you do not control: at most 200,000 triangles, and give up after 10 seconds
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var bounded = new ScadRenderOptions
{
    Evaluation = new ScadEvaluationOptions { Limits = new ScadLimits { MaxTriangles = 200_000 } },
};
using var safe = document.Render(400, 200, bounded, timeout.Token);
```

## Example 25: Handling Unsupported Features and Strict Mode

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Scad;

var document = ScadDocument.Parse("""
    cube(10);
    translate([20, 0, 0]) surface(file = "heights.dat");
    roof() { sphere(3); translate([8, 0, 0]) sphere(3); }
    cube(-1);
    offset(r = 1) square(2);
    """, new ScadOpenOptions { SourceName = "model.scad" });

// Lenient: everything that can be built is drawn, and every problem is reported once, in order, on this
// thread, before Render returns
var warnings = new List<ScadDiagnostic>();
var unsupported = new List<ScadUnsupportedFeature>();
var lenient = new ScadRenderOptions
{
    Evaluation = new ScadEvaluationOptions
    {
        WarningHandler = warnings.Add,
        UnsupportedFeatureHandler = unsupported.Add,
    },
};
using var image = document.Render(320, 240, lenient);

foreach (var warning in warnings)
{
    Console.WriteLine(warning); // Output: model.scad:4:1: cube: size must be positive; the instance is ignored
}

foreach (var feature in unsupported)
{
    Console.WriteLine($"{feature.Location} {feature.Feature}");
}
// Output:
// model.scad:2:23 scad-module-surface
// model.scad:3:1 scad-module-roof
// model.scad:5:1 scad-module-offset

// Strict: refuse a partial result. The first unsupported feature of any stage throws, after the events that
// preceded it were delivered to the handlers
var strict = new ScadRenderOptions
{
    Evaluation = new ScadEvaluationOptions { ThrowOnUnsupportedFeature = true },
};

try
{
    using var never = document.Render(320, 240, strict);
}
catch (ScadUnsupportedFeatureException ex)
{
    Console.WriteLine($"{ex.Location}: {ex.Feature}"); // Output: model.scad:2:23: scad-module-surface
}
```

## Example 26: Hull and Minkowski Sum

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Scad;

var document = ScadDocument.Parse("""
    $fn = 32;

    // A capsule: the convex hull of two spheres
    hull() { sphere(r = 3); translate([12, 0, 0]) sphere(r = 3); }

    // A rounded block: the Minkowski sum of a block and a ball
    translate([0, 14, 0]) minkowski() { cube([14, 8, 4]); sphere(r = 1.5); }

    // A hull as a cutter: a diagonal slot through a plate
    translate([0, 34, 0]) difference() {
        cube([24, 24, 4]);
        translate([4, 4, -1]) hull() { cube([2, 2, 6]); translate([12, 12, 0]) cube([2, 2, 6]); }
    }

    // A 2D hull, extruded
    translate([30, 0, 0]) linear_extrude(height = 3) hull() { circle(r = 3); translate([10, 6]) square(2); }
    """, new ScadOpenOptions { SourceName = "convex.scad" });

using var image = document.Render(800, 600);
PngCodec.Save(image, "convex.png");

// A hull of a difference, and a Minkowski sum with a hollow part, are reported, never approximated
var report = new List<ScadUnsupportedFeature>();
var options = new ScadRenderOptions
{
    Evaluation = new ScadEvaluationOptions { UnsupportedFeatureHandler = report.Add },
};
ScadDocument.Parse("""
    hull() difference() { cube(4); cube(3); }
    minkowski() { difference() { cube(4); translate([1, 1, 1]) cube(2); } sphere(1); }
    """, new ScadOpenOptions { SourceName = "bad.scad" }).BuildScene(options);

foreach (var feature in report)
{
    Console.WriteLine($"{feature.Location} {feature.Feature}");
}
// Output:
// bad.scad:1:1 scad-hull-csg-operand
// bad.scad:2:1 scad-minkowski-nonconvex
```

## Example 27: Transparency, Ghosts, and Highlights

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Scad;

var document = ScadDocument.Parse("""
    // A half-transparent red box: its near and far walls both show
    color("red", 0.5) cube(10);

    // A plate with a hole whose cutter is highlighted: the cutter still cuts, and its pink body is drawn in the hole
    translate([16, 0, 0]) difference() {
        cube([10, 10, 3]);
        #translate([5, 5, -1]) cylinder(h = 5, r = 3, $fn = 32);
    }

    // A ghost: drawn translucent gray, but not part of the model - it does not cut the block it passes through
    translate([32, 0, 0]) difference() {
        cube(10);
        %translate([2, 2, 2]) cube([6, 6, 12]);
    }

    // A ghost of an extruded shape (a % on a bare 2D shape is reported, not drawn)
    %translate([52, 5, 0]) linear_extrude(height = 4) circle(r = 4, $fn = 24);
    """, new ScadOpenOptions { SourceName = "see-through.scad" });

// The overlay colors are part of the render options; any alpha is accepted
var options = new ScadRenderOptions
{
    HighlightColor = new Rgba32(255, 64, 128, 110), // the default: a translucent pink
    GhostColor = new Rgba32(128, 128, 128, 80),     // the default: a translucent gray
};

using var image = document.Render(800, 300, options);
PngCodec.Save(image, "see-through.png");

// Nothing here is reported as unsupported: the alpha, the ghost, and the highlight are rendered. A scene that
// uses them is resolved like a boolean, so its meshes must be closed solids; the ghost is part of the scene bounds
// that fit the default camera but not of the model (it adds no propagated bounds, so resize ignores it)
var scene = document.BuildScene(options);
Console.WriteLine(scene.ComputeBounds().Max.X);                              // Output: 56
Console.WriteLine(scene.ComputeBounds(includeOverlays: false).Max.X);        // Output: 42
```

## Example 28: Text, Alignment, and Fonts

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Scad;

var document = ScadDocument.Parse("""
    // A nameplate with the name cut into it: text is a 2D shape, so it extrudes and subtracts like one
    difference() {
        cube([60, 16, 4]);
        translate([30, 8, 2]) linear_extrude(height = 3)
            text("CanvasNet", size = 7, halign = "center", valign = "center");
    }

    // Raised bold italic serif lettering on a second plate, spaced a little wider
    translate([0, 24, 0]) {
        cube([60, 16, 2]);
        translate([4, 4, 2]) linear_extrude(height = 1.5)
            text("Hello", size = 8, font = "Liberation Serif:style=Bold Italic", spacing = 1.1);
    }

    // Stacked letters, top to bottom, and a text whose characters are laid out right to left
    translate([70, 30, 0]) linear_extrude(height = 2) text("ABC", size = 5, direction = "ttb");
    translate([90, 0, 0]) linear_extrude(height = 2) text("12345", size = 5, direction = "rtl");
    """);

// Make the named font independent of this machine: only the bundled Liberation fonts and ./fonts are consulted
var options = new ScadRenderOptions
{
    FontResolution = new ScadFontResolutionOptions { UseSystemFonts = false, AdditionalDirectories = ["./fonts"] },
};

using var image = document.Render(800, 400, options);
PngCodec.Save(image, "nameplates.png");

// The size is the height of the capital H of the font, and the ink starts at X = 0 (halign = "left"): in the bundled
// Liberation Sans, text("HI", size = 14.09) is 16.91 wide and 14.09 high
var scene = ScadDocument.Parse("linear_extrude(height = 1) text(\"HI\", size = 14.09);").BuildScene();
var bounds = scene.ComputeBounds();
Console.WriteLine($"{bounds.Max.X:F2} x {bounds.Max.Y:F2}"); // Output: 16.91 x 14.09
```

## Example 29: Rendering a Vega-Lite Chart

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.VegaLite;

// A bar chart with inline data. VegaLiteDocument.Open("chart.vl.json") would read the same text from a file.
var chart = VegaLiteDocument.Parse("""
    {
      "$schema": "https://vega.github.io/schema/vega-lite/v5.json",
      "title": "Crates sold per stall",
      "width": 240,
      "height": 200,
      "data": { "values": [
        { "stall": "North", "crates": 28 },
        { "stall": "South", "crates": 55 },
        { "stall": "East", "crates": 43 },
        { "stall": "West", "crates": 91 }
      ] },
      "mark": "bar",
      "encoding": {
        "x": { "field": "stall", "type": "nominal", "axis": { "labelAngle": 0 } },
        "y": { "field": "crates", "type": "quantitative", "axis": { "title": "Crates" } }
      }
    }
    """);

// The document describes the text without loading anything
Console.WriteLine(chart.Title);                     // Output: Crates sold per stall
Console.WriteLine(string.Join(", ", chart.MarkTypes)); // Output: bar
Console.WriteLine(chart.DataReferences[0].Kind);    // Output: Inline

// Measure gives the size Render produces (the data rectangle plus the axes, title, and padding) without drawing
VegaLiteSize natural = chart.Measure();

// Render at the natural size, then at twice the size (everything, text included, is drawn sharply at the final size)
using var normal = chart.Render();
using var doubled = chart.Render(new VegaLiteRenderOptions { Scale = 2 });
Console.WriteLine($"{normal.Width}x{normal.Height} = {natural.Width}x{natural.Height}");
PngCodec.Save(doubled, "crates.png");

// Fit the chart into a fixed box: it is scaled uniformly and centered, and the margins take the background
var gray = new VegaLiteRenderOptions { Background = new Rgba32(244, 244, 244, 255) };
using var thumbnail = chart.Render(320, 240, gray);
PngCodec.Save(thumbnail, "crates-thumbnail.png");
```

## Example 30: Vega-Lite Unsupported Features and Strict Mode

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite;

// A line mark is recognized but not drawn yet
var chart = VegaLiteDocument.Parse("""
    {
      "data": { "values": [ { "week": 1, "visits": 120 }, { "week": 2, "visits": 150 } ] },
      "mark": "line",
      "encoding": { "x": { "field": "week", "type": "ordinal" } }
    }
    """, new VegaLiteOpenOptions { SourceName = "visits.vl.json" });

// Lenient: the chart is drawn without what is not supported, and every report is delivered once, in order, on
// this thread, before Render returns
var warnings = new List<VegaLiteDiagnostic>();
var unsupported = new List<VegaLiteUnsupportedFeature>();
var lenient = new VegaLiteRenderOptions
{
    WarningHandler = warnings.Add,
    UnsupportedFeatureHandler = unsupported.Add,
};
using var picture = chart.Render(lenient);

foreach (var feature in unsupported)
{
    // The location is file:line:column and the RFC 6901 pointer of the construct
    Console.WriteLine($"{feature.Location}: {feature.Feature}"); // Output: visits.vl.json:3:11 /mark: vl-mark-line
}

// Strict: refuse an approximate chart. The first unsupported feature throws, after it was delivered to the handler
var strict = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true };

try
{
    using var never = chart.Render(strict);
}
catch (VegaLiteUnsupportedFeatureException ex)
{
    Console.WriteLine($"{ex.Location}: {ex.Feature}"); // Output: visits.vl.json:3:11 /mark: vl-mark-line
}
```

## Example 31: Themes, Fonts, and Limits for a Vega-Lite Chart

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.VegaLite;

var chart = VegaLiteDocument.Open("chart.vl.json");

var options = new VegaLiteRenderOptions
{
    // 1. A host theme: a configuration that is merged over the specification's own (the caller wins)
    Config = """{ "bar": { "fill": "steelblue" }, "title": { "fontSize": 16 } }""",

    // 2. Reproducible text: only the bundled Liberation fonts and ./fonts are consulted for a named family
    Fonts = new VegaLiteFontOptions { UseSystemFonts = false, AdditionalDirectories = ["./fonts"] },

    // 3. Bound the work for specifications you do not control: at most 10,000 data rows and 5,000 scene items
    Limits = new VegaLiteLimits { MaxDataRows = 10_000, MaxSceneItems = 5_000 },
};

// 4. Give up after 10 seconds
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

try
{
    using var picture = chart.Render(options, timeout.Token);
    PngCodec.Save(picture, "themed.png");
}
catch (VegaLiteRenderException ex)
{
    // A limit was exceeded or the data cannot be drawn; the location names the construct
    Console.WriteLine($"{ex.Location}: {ex.Message}");
}
```

## Example 32: Vega-Lite Charts from Data Files, Named Datasets, and Sequences

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.VegaLite;

// 1. A chart whose rows are in a CSV file next to the specification. Opening it by path makes its folder the base
//    directory, so "stalls.csv" is read from there (never from the current directory) when the chart is rendered.
//    stalls.csv:   stall,crates,fresh        chart.vl.json: "data": { "url": "stalls.csv" }, "mark": "bar", ...
//                  North,28,true
//                  South,55,false
var fromFile = VegaLiteDocument.Open("charts/chart.vl.json");
using var picture = fromFile.Render();
PngCodec.Save(picture, "from-file.png");

// 2. The same text parsed from a string has no folder: say where its data lives, or serve it yourself
var text = File.ReadAllText("charts/chart.vl.json");
var inFolder = VegaLiteDocument.Parse(
    text,
    new VegaLiteOpenOptions { BaseDirectory = "charts", DataPaths = ["shared-data"] });
var inMemory = VegaLiteDocument.Parse(
    text,
    new VegaLiteOpenOptions
    {
        // The callback replaces the file system: it gets normalized logical paths and returns UTF-8 text (or null)
        FileResolver = path => path == "stalls.csv"
            ? new MemoryStream("stall,crates\nNorth,28\nSouth,55\n"u8.ToArray())
            : null,
    });

// 3. Rows that you hold in memory, for a chart that says "data": { "name": "stalls" }
var rows = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>
{
    ["stalls"] =
    [
        new Dictionary<string, object?> { ["stall"] = "North", ["crates"] = 28 },
        new Dictionary<string, object?> { ["stall"] = "South", ["crates"] = 55 },
    ],
};
var named = VegaLiteDocument.Parse("""
    {
      "data": { "name": "stalls" },
      "mark": "bar",
      "encoding": {
        "x": { "field": "stall", "type": "nominal" },
        "y": { "field": "crates", "type": "quantitative" }
      }
    }
    """);
using var fromMemory = named.Render(new VegaLiteRenderOptions { Datasets = rows });

// 4. A generated sequence needs no data at all: x = 0, 1, 2 (the stop is not included)
var sequence = VegaLiteDocument.Parse("""
    {
      "data": { "sequence": { "start": 0, "stop": 3, "as": "x" } },
      "mark": "point",
      "encoding": { "x": { "field": "x", "type": "quantitative" } }
    }
    """);
using var dots = sequence.Render();

// 5. A local file that cannot be used is an error, not an empty chart; a remote URL is reported and never fetched
try
{
    using var broken = VegaLiteDocument.Parse(text, new VegaLiteOpenOptions { BaseDirectory = "elsewhere" }).Render();
}
catch (VegaLiteRenderException ex)
{
    Console.WriteLine($"{ex.Location}: {ex.Message}"); // names the url and says that the file was not found
}
```

## Rendering (transform-aware Canvas, text, and shapes)

The `Rendering` subsystem adds a transform-aware `Canvas` wrapper over `Surface`, along with
text rendering and shape helpers.

### Canvas transform stack

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Rendering;
using System.Numerics;

using var surface = new Surface(200, 200);
var canvas = new Canvas(surface);

canvas.Save();
canvas.Translate(100, 100);
canvas.RotateDegrees(45f);
// ... drawing here is in a rotated, translated frame
canvas.Restore(); // pops back to the identity
```

`Save` pushes the current transform; `Restore` pops it. `Restore` on an empty stack throws
`InvalidOperationException`. `Translate` and `RotateDegrees` prepend to the current transform.

### DrawText and MeasureText

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Rendering;

using var surface = new Surface(200, 200);
var canvas = new Canvas(surface);

// Load a TrueType font.
using var stream = File.OpenRead("OpenSans-Regular.ttf");
var font = TrueTypeFont.Load(stream);

// Measure text before drawing.
var metrics = TextRenderer.MeasureText("Hello", font, size: 32f);
Console.WriteLine($"width={metrics.Width} ascent={metrics.Ascent}");

// Draw text at a baseline anchor with alignment.
canvas.DrawText("Hello", x: 100, y: 100, TextAlign.Center, font, size: 32f, new Rgba32(0, 0, 0, 255));
```

`DrawText` respects the Canvas current transform, so translated/rotated text works too.

### Shape helpers

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Rendering;

using var surface = new Surface(200, 200);
var canvas = new Canvas(surface);
var red = new Rgba32(255, 0, 0, 255);
canvas.FillRect(10, 10, 50, 50, red);
canvas.FillRoundRect(70, 10, 50, 50, radius: 12f, red);
canvas.FillCircle(150, 35, radius: 20f, red);
```

`FillRoundRect` clamps `radius` to half of the shorter side; degenerate zero-size shapes are
no-op.

### CornerRoundEffect

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.Rendering;
using System.Numerics;

using var surface = new Surface(200, 200);
var canvas = new Canvas(surface);
var red = new Rgba32(255, 0, 0, 255);

var polygon = new PathBuilder()
    .MoveTo(new Vector2(10, 10))
    .LineTo(new Vector2(100, 10))
    .LineTo(new Vector2(100, 100))
    .LineTo(new Vector2(10, 100))
    .Close()
    .Build();

var rounded = CornerRoundEffect.Apply(polygon, radius: 12f);
canvas.FillPath(rounded, red);
```

`CornerRoundEffect` rounds polyline corners only; curved corners are preserved as documented.
The radius is clamped per-corner to half of the shorter adjacent segment.

### Rgba32.Parse

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Rendering;

using var surface = new Surface(200, 200);
var canvas = new Canvas(surface);
var userInput = "#80FF0000";

var opaqueRed = Rgba32.Parse("#FF0000");     // A=255
var translucentRed = Rgba32.Parse("#80FF0000"); // A=128

if (Rgba32.TryParse(userInput, out var color))
{
    canvas.FillRect(10, 10, 50, 50, color);
}
```

Accepts `#RRGGBB` (alpha defaults to 255) and `#AARRGGBB`, case-insensitive. Rejects short
forms, missing `#`, wrong length, and non-hex characters.

# References

- [REF-1] Continuous Compliance Methodology (<https://github.com/demaconsulting/ContinuousCompliance>)
