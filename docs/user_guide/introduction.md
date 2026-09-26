# Introduction

<!-- cspell:ignore glyf sfnt codepoint -->
<!-- cspell:ignore rasterizing unparseable SMIL -->

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

`Surface` implements `IDisposable`. In this release its pixel buffer is a plain managed array, so
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

### SvgCodec

The `SvgCodec` static class decodes and rasterizes a common real-world subset of SVG documents
into a `Surface` of caller-chosen pixel dimensions. `SvgCodec` is decode-only: there is no `Save`.
It supports basic shapes (`rect`, `circle`, `ellipse`, `line`, `polyline`, `polygon`, `path`),
grouping (`g`) with cascading presentation attributes, `transform` functions, linear/radial
gradients (including `xlink:href`/`href` template inheritance), `use` references, and best-effort
`text` rendering against a caller-supplied dictionary of `TrueTypeFont` instances. The root
`viewBox`/`width`/`height` are fit into the requested raster using a "meet, centered" policy
equivalent to CSS `object-fit: contain` (`preserveAspectRatio` itself is not read). Well-formed but
out-of-scope constructs (`style`, `filter`, `mask`, `clipPath`, `animate`/SMIL, `image`,
`foreignObject`, `pattern`, `marker`, nested `svg`, CSS selectors) are silently skipped so the rest
of the document still renders; malformed/unparseable input throws `InvalidDataException`.

#### SvgCodec Methods

##### SvgCodec.Load(Stream stream, int width, int height, IReadOnlyDictionary&lt;string, TrueTypeFont&gt;? fonts = null)

```csharp
public static Surface Load(
    Stream stream,
    int width,
    int height,
    IReadOnlyDictionary<string, TrueTypeFont>? fonts = null)
```

Decodes and rasterizes an SVG document from an open, readable stream into a new `width` x
`height` `Surface`. If `fonts` is supplied, `text` elements are rendered using the matching
`TrueTypeFont` keyed by family name; unmatched or missing fonts cause that `text` element to be
silently skipped rather than throwing.

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

### TrueTypeFont

The `TrueTypeFont` class loads glyph-based TrueType (`glyf`-based) SFNT fonts and exposes raw
font-design-unit outlines, metrics, advance widths, and basic pairwise kerning.

```csharp
public sealed class TrueTypeFont
{
    public static TrueTypeFont Load(Stream stream);
    public static TrueTypeFont Load(string path);

    public int UnitsPerEm { get; }
    public int Ascender { get; }
    public int Descender { get; }
    public int LineGap { get; }
    public int GlyphCount { get; }

    public int GetGlyphIndex(int codepoint);
    public Path GetGlyphOutline(int glyphIndex);
    public int GetAdvanceWidth(int glyphIndex);
    public int GetKerning(int leftGlyphIndex, int rightGlyphIndex);
}
```

`GetGlyphOutline` returns `Geometry.Path` in raw font-design-unit coordinates with Y increasing
upward, per the TrueType convention. Callers typically scale that path by the desired point size
and flip Y before rendering it through `PathFiller` or `PathStroker`.

```csharp
var font = TrueTypeFont.Load("font.ttf");
var glyphIndex = font.GetGlyphIndex('A');
var outline = font.GetGlyphOutline(glyphIndex);
var advanceWidth = font.GetAdvanceWidth(glyphIndex);
```

**Exceptions:**

- `ArgumentNullException`: Thrown when `Load` receives a null stream or path.
- `ArgumentException`: Thrown when `Load(string)` receives an empty path.
- `InvalidDataException`: Thrown when the font data is malformed, truncated, or unsupported.
- `ArgumentOutOfRangeException`: Thrown when `GetGlyphOutline` or `GetAdvanceWidth` receives an
  out-of-range glyph index.

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

## Example 3: BMP Load/Save

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

## Example 4: PNG Load/Save

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

## Example 5: TIFF Load/Save

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

## Example 6: JPEG Load/Save

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

using var surface = new Surface(16, 16);
surface[0, 0] = new Rgba32(255, 128, 0, 255); // opaque orange pixel

// Save at quality 90 (the default)
JpegCodec.Save(surface, "surface.jpg", 90);
using var loaded = JpegCodec.Load("surface.jpg");
Console.WriteLine(loaded[0, 0].A); // Output: 255

// JPEG is lossy, so compare color channels with a tolerance rather than exact equality
Console.WriteLine(Math.Abs(loaded[0, 0].R - surface[0, 0].R) <= 15); // Output: True
```

## Example 7: Compositing a Semi-Transparent Color Over a Background

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

## Example 9: Filling a Vector Path

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

## Example 10: Stroking a Vector Path

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

## Example 11: Filling a Vector Path with a Radial Gradient

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
Console.WriteLine(canvas[32, 32].A); // Output: 255 (at the gradient's center)
```

## Example 12: Loading a TrueType Font and Filling a Glyph Outline

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using System.Numerics;

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
                case PathCommandType.Close:
                    builder.Close();
                    break;
            }
        }
    }

    return builder.Build();
}

var font = TrueTypeFont.Load("font.ttf");
var glyphIndex = font.GetGlyphIndex('A');
var glyphOutline = font.GetGlyphOutline(glyphIndex);
var scale = 48f / font.UnitsPerEm;
var canvasOutline = TransformGlyph(glyphOutline, scale, baselineY: 56f);

using var surface = new Surface(64, 64);
PathFiller.Fill(surface, canvasOutline, new Rgba32(20, 120, 255, 255));
Console.WriteLine(font.GetAdvanceWidth(glyphIndex));
```

## Example 13: Decoding and Rasterizing an SVG Document

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
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
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Rendering;

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
var red = new Rgba32(255, 0, 0, 255);
canvas.FillRect(10, 10, 50, 50, red);
canvas.FillRoundRect(70, 10, 50, 50, radius: 12f, red);
canvas.FillCircle(150, 35, radius: 20f, red);
```

`FillRoundRect` clamps `radius` to half of the shorter side; degenerate zero-size shapes are
no-op.

### CornerRoundEffect

```csharp
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
