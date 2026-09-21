# Introduction

## Purpose

This document is the user guide for CanvasNet, a .NET library providing a
canvas-based drawing and rendering API.

## Scope

This user guide covers:

- Installation of the library
- Basic usage and examples
- API reference

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

# Usage

## Basic Usage

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

var surface = new Surface(4, 4);
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
methods read only enough of the file to populate an `ImageInfo` - never decoding pixel data - so
they are safe to call on untrusted or very large files before deciding whether to call `Load`.
Unlike `Load`, `GetInfo` does not enforce `Surface.MaxDimension`, so callers should compare the
returned dimensions against `Surface.MaxDimension` themselves when triaging untrusted input.

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

The `PngCodec` static class loads and saves `Surface` pixel buffers as PNG files, supporting only
8-bit-per-channel Truecolor (RGB) and Truecolor-with-alpha (RGBA) color types, non-interlaced.
Grayscale, palette-based color types, other bit depths, and Adam7 interlacing are rejected with
`InvalidDataException`.

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

Loads a `Surface` from an open, readable stream containing a supported PNG image. Pixels decoded
from an RGB (color type 2) image always have alpha 255 (fully opaque).

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `InvalidDataException`: Thrown when the stream does not contain a valid, supported PNG image.

##### PngCodec.Load(string path)

```csharp
public static Surface Load(string path)
```

Loads a `Surface` from a PNG file at the specified path.

**Exceptions:**

- `ArgumentNullException`: Thrown when `path` is null.
- `ArgumentException`: Thrown when `path` is an empty string.
- `InvalidDataException`: Thrown for the same conditions as `Load(Stream)`.

##### PngCodec.GetInfo(Stream stream)

```csharp
public static ImageInfo GetInfo(Stream stream)
```

Reads only the PNG signature and `IHDR` chunk (never pixel data) from an open, readable stream
and returns an `ImageInfo` describing the image. Does not enforce `Surface.MaxDimension`.

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `InvalidDataException`: Thrown when the stream does not contain a valid PNG signature/`IHDR`.

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
`ImageInfo` describing the image. Does not enforce `Surface.MaxDimension`. Requires a seekable
`stream`: a TIFF's IFD can legitimately be located anywhere in the file (unlike PNG/JPEG/BMP,
whose headers are always near the start), so no bounded, purely sequential scan can reliably
resolve every well-formed TIFF from a non-seekable source. If `stream.CanSeek` is `false`,
`GetInfo` throws `NotSupportedException` immediately, before reading any bytes from `stream` at
all; a caller with a genuinely non-seekable source (for example a network stream) can trivially
wrap it in a seekable buffer such as `MemoryStream` first. Once past that check, `stream` resolves
every tag through the exact same validating parser `Load` itself uses (`ReadTiffImageInfo`). A
`StreamTiffDataSource` seeks directly to the IFD and reads only the bytes the parser actually asks
for - the header, IFD entries, and any out-of-line tag value needed (for example a multi-value
`BitsPerSample` tag) - resolving `ImageWidth`, `ImageLength`, `BitsPerSample`, `SamplesPerPixel`,
`Compression`, `PhotometricInterpretation`, `PlanarConfiguration`, `Predictor`, and `ExtraSamples`,
and never reading `StripOffsets`/`RowsPerStrip`/`StripByteCounts` or any strip/pixel data.

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `NotSupportedException`: Thrown when `stream` does not support seeking; wrap a non-seekable
  source (for example a network stream) in a seekable buffer such as a `MemoryStream` first.
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
`Surface.MaxDimension`. Reads at most `MaxProbeHeaderBytes` (1,048,576 bytes) before giving up;
throws `InvalidDataException` if no SOF0/SOF2 marker is found within that limit.

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `InvalidDataException`: Thrown when the stream does not contain a valid JPEG header, or no
  SOF0/SOF2 marker is found within `MaxProbeHeaderBytes`.

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

# Examples

## Example 1: Surface Pixel Access

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

var surface = new Surface(4, 4);
surface[1, 1] = new Rgba32(255, 0, 0, 255); // opaque red pixel
var pixel = surface[1, 1];
Console.WriteLine(pixel.R); // Output: 255
```

## Example 2: Surface Crop

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

var surface = new Surface(4, 4);
surface[1, 1] = new Rgba32(0, 255, 0, 255); // opaque green pixel

var cropped = surface.Crop(1, 1, 2, 2);
Console.WriteLine(cropped.Width);  // Output: 2
Console.WriteLine(cropped.Height); // Output: 2
Console.WriteLine(cropped[0, 0].G); // Output: 255
```

## Example 3: BMP Load/Save

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

var surface = new Surface(2, 2);
surface[0, 0] = new Rgba32(255, 0, 0, 128); // semi-transparent red pixel

// Save with alpha preserved (Bit32, the default)
BmpCodec.Save(surface, "surface.bmp");
var loaded = BmpCodec.Load("surface.bmp");
Console.WriteLine(loaded[0, 0].A); // Output: 128

// Save without alpha (Bit24) - reloading always yields opaque pixels
BmpCodec.Save(surface, "surface24.bmp", BmpBitDepth.Bit24);
var loaded24 = BmpCodec.Load("surface24.bmp");
Console.WriteLine(loaded24[0, 0].A); // Output: 255
```

## Example 4: PNG Load/Save

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

var surface = new Surface(2, 2);
surface[0, 0] = new Rgba32(0, 255, 0, 128); // semi-transparent green pixel

// Save with alpha preserved (Rgba, the default)
PngCodec.Save(surface, "surface.png");
var loaded = PngCodec.Load("surface.png");
Console.WriteLine(loaded[0, 0].A); // Output: 128

// Save without alpha (Rgb) - reloading always yields opaque pixels
PngCodec.Save(surface, "surface-rgb.png", PngColorType.Rgb);
var loadedRgb = PngCodec.Load("surface-rgb.png");
Console.WriteLine(loadedRgb[0, 0].A); // Output: 255
```

## Example 5: TIFF Load/Save

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

var surface = new Surface(2, 2);
surface[0, 0] = new Rgba32(0, 0, 255, 128); // semi-transparent blue pixel

// Save uncompressed (the default)
TiffCodec.Save(surface, "surface.tiff");
var loaded = TiffCodec.Load("surface.tiff");
Console.WriteLine(loaded[0, 0].A); // Output: 128

// Save with LZW compression and an automatic horizontal-differencing predictor
TiffCodec.Save(surface, "surface-lzw.tiff", TiffCompression.Lzw);
var loadedLzw = TiffCodec.Load("surface-lzw.tiff");
Console.WriteLine(loadedLzw[0, 0].B); // Output: 255
```

## Example 6: JPEG Load/Save

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

var surface = new Surface(16, 16);
surface[0, 0] = new Rgba32(255, 128, 0, 255); // opaque orange pixel

// Save at quality 90 (the default)
JpegCodec.Save(surface, "surface.jpg", 90);
var loaded = JpegCodec.Load("surface.jpg");
Console.WriteLine(loaded[0, 0].A); // Output: 255

// JPEG is lossy, so compare color channels with a tolerance rather than exact equality
Console.WriteLine(Math.Abs(loaded[0, 0].R - surface[0, 0].R) <= 15); // Output: True
```

## Example 7: Compositing a Semi-Transparent Color Over a Background

```csharp
using DemaConsulting.CanvasNet.Canvas;

var background = new Surface(1, 1);
background[0, 0] = new Rgba32(0, 255, 0, 255); // opaque green background

// Composite a semi-transparent red overlay over the background, in place
background.CompositeOver(new Rgba32(255, 0, 0, 128));
var result = background[0, 0];
Console.WriteLine($"{result.R} {result.G} {result.B} {result.A}"); // Output: 128 127 0 255

// CompositeOver(Surface) works the same way when the foreground is itself a Surface (for
// example, one loaded from a PNG file with an alpha channel), pixel by pixel across the whole
// surface rather than a single constant color.
```

## Example 8: Header-Only Probing Before Load (Decompression-Bomb Triage)

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
var surface = PngCodec.Load("untrusted.png");
Console.WriteLine($"{surface.Width}x{surface.Height}, alpha: {info.HasAlpha}");
```

## Example 9: Filling a Vector Path

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using System.Numerics;

var canvas = new Surface(64, 64);
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

## Example 10: Stroking a Vector Path

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using System.Numerics;

var canvas = new Surface(64, 64);
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

# References

- [REF-1] Continuous Compliance Methodology (<https://github.com/demaconsulting/ContinuousCompliance>)
