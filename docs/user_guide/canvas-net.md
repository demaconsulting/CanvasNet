# CanvasNet Core Library

<!-- cspell:ignore otsu midtone midtones -->

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

##### Clone

```csharp
public Surface Clone()
```

Creates a new, independent `Surface` with the same size and pixels; use it before an in-place
operation (such as the adjustments below) when the original must be kept.

##### CopyFrom

```csharp
public void CopyFrom(Surface source, int destX, int destY)
```

Copies the pixels of `source` onto this surface with its top-left pixel at `(destX, destY)`. The
copy is opaque (every source pixel, alpha included, replaces the pixel underneath; use
`CompositeOver` to blend) and clipped to this surface (the part of `source` outside it is ignored,
and a source entirely outside changes nothing).

**Exceptions:**

- `ArgumentNullException`: Thrown when `source` is null.
- `ArgumentException`: Thrown when `source` is this surface.
- `ObjectDisposedException`: Thrown when either surface has been disposed.

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

#### Surface Transformations

These methods are pure: each returns a new `Surface` (or a value) and never changes the surface it
is called on. Each throws `ObjectDisposedException` when the surface has been disposed.

```csharp
public Surface FlipHorizontal()   // mirror left and right
public Surface FlipVertical()     // mirror top and bottom
public Surface Rotate180()        // mirror about both axes
public Surface Pad(int left, int top, int right, int bottom, Rgba32 color)
public (int X, int Y, int Width, int Height)? FindTrimBounds(int tolerance)
public Surface Trim(int tolerance)
public Surface FitInto(int boxWidth, int boxHeight, FitMode mode, Rgba32 fill,
    ResamplingMode filter = ResamplingMode.Auto)
public Surface FlattenOver(Rgba32 background)
public bool HasTransparency()
```

- `Pad` surrounds the surface with a border of the given widths and `color` and copies the pixels
  in without blending. It throws `ArgumentOutOfRangeException` for a negative width and
  `ArgumentException` when the result would be larger than `Surface.MaxDimension` on a side.
- `FindTrimBounds` finds the box of the content inside a uniform border: the background is the most
  frequent of the four corner colors, all fully transparent pixels are equal, and a pixel is
  background when its largest channel difference from the background is at most `tolerance`
  (0 to `Surface.MaxTrimTolerance`, 255). It returns `null` when every pixel is background. `Trim`
  crops to that box, or returns a copy when there is no content.
- `FitInto` returns a surface of exactly the box. With `FitMode.Contain` the image keeps its aspect
  ratio, is scaled to touch the box on its limiting side (a small image is enlarged), and is centered
  on `fill`; with `FitMode.Stretch` it is resampled to the box.
- `FlattenOver` composites the surface over a solid color into a new surface, which is opaque when
  the color is opaque; `HasTransparency` tells whether any pixel has an alpha below 255 (for example
  before saving to a format without an alpha channel).

```csharp
using var photo = PngCodec.Load("scan.png");
using var content = photo.Trim(tolerance: 8);                       // remove the paper margin
using var framed = content.Pad(16, 16, 16, 16, new Rgba32(255, 255, 255, 255));
using var thumbnail = framed.FitInto(256, 256, FitMode.Contain, new Rgba32(0, 0, 0, 0));
```

#### Surface Adjustments

These methods adjust the tone and color of the pixels **in place** (like `Clear`); call `Clone()`
first to keep the original. Red, green, and blue change and alpha is unchanged. Rows are processed
in parallel, and the results do not depend on the number of cores. Each throws
`ObjectDisposedException` when the surface has been disposed.

```csharp
public static int Luma(Rgba32 pixel)
public void Grayscale()
public void Invert()
public void Threshold(int level)
public long[] LumaHistogram()
public static int OtsuThreshold(ReadOnlySpan<long> histogram)
public void Levels(int black, int white, double gamma = 1.0)
public void Brightness(int amount)
public void Contrast(double contrast)
public (int Black, int White)? AutoLevels(double clipPercent = Surface.DefaultAutoLevelsClipPercent)
```

- `Luma` is the BT.601 fixed-point luma `(77 R + 150 G + 29 B + 128) >> 8` of an opaque pixel; a
  translucent pixel is flattened over white first, so a fully transparent pixel has the luma 255.
- `Grayscale` sets red, green, and blue to the luma of the pixel's own color; `Invert` replaces each
  color channel `c` with `255 - c`.
- `Threshold(level)` (0 to 255) makes a pixel opaque white when its luma over white is greater than
  `level`, and opaque black otherwise. `OtsuThreshold` chooses a level from a 256-bin histogram
  (for example from `LumaHistogram()`) by maximizing the between-class variance, so
  `surface.Threshold(Surface.OtsuThreshold(surface.LumaHistogram()))` converts to black and white with an
  automatic level.
- `Levels` maps `black` (0 to 254) to 0 and `white` (1 to 255, above `black`) to 255 with a midtone
  `gamma` from `Surface.MinGamma` (0.1) to `Surface.MaxGamma` (10); a gamma above 1 brightens the
  midtones. `Brightness` adds an amount from -255 to 255 and clamps. `Contrast` takes a value from
  -100 to 100 and pivots around mid-gray (0 changes nothing).
- `AutoLevels` stretches the image so that the black and white points of its luma histogram, ignoring
  `clipPercent` (0 to `Surface.MaxAutoLevelsClipPercent`, 20) of the pixels at each end, become 0 and
  255. It returns the points it applied, or `null` when the image is flat and was left unchanged.

**Exceptions:** an argument outside its range throws `ArgumentOutOfRangeException`; `black` not
below `white`, or a histogram that does not have 256 entries or has a negative count, throws
`ArgumentException`.

```csharp
using var scan = PngCodec.Load("page.png");
scan.AutoLevels();      // stretch a dull scan to the full range
scan.Contrast(20);
using var blackAndWhite = scan.Clone();
blackAndWhite.Grayscale();
blackAndWhite.Threshold(Surface.OtsuThreshold(blackAndWhite.LumaHistogram()));
```

### Rgba32

The `Rgba32` struct represents a single 32-bit RGBA pixel, with public `R`, `G`, `B`, and `A`
`byte` fields and value equality (`Equals`, `GetHashCode`, `==`, `!=`).

#### Rgba32.CompositeOver

```csharp
using DemaConsulting.CanvasNet.Canvas;

var background = new Rgba32(0, 255, 0, 255);   // opaque green
var foreground = new Rgba32(255, 0, 0, 128);   // half-transparent red

var blended = Rgba32.CompositeOver(background, foreground);
Console.WriteLine(blended); // Output: Rgba32(128, 127, 0, 255)
```

`Rgba32.CompositeOver(background, foreground)` blends one `foreground` pixel over one `background`
pixel with the standard Porter-Duff "over" operator. Both pixels are straight (not premultiplied)
alpha, and so is the result; each result channel is rounded half away from zero. When the
result alpha is zero (both pixels fully transparent) the result is `(0, 0, 0, 0)`. Every byte
combination is valid, the method never throws, and it uses the same formula and rounding as
`Surface.CompositeOver(Rgba32)`. For many pixels use the `Surface` operations, which are vectorized.

### CssColor

`CssColor` is a public static class that turns CSS color text into `Rgba32` values and back. It is the one color
grammar shared by every CanvasNet package, so the same text gives the same pixel in an SVG document, a Vega-Lite
chart specification, and a tool option.

```csharp
using DemaConsulting.CanvasNet.Canvas;

Rgba32 blue = CssColor.Parse("#4c78a8");                        // (76, 120, 168, 255)
Rgba32 half = CssColor.Parse("rgba(255, 0, 0, 0.5)");           // (255, 0, 0, 128)
Rgba32 same = CssColor.Parse("hsl(0 100% 50% / 50%)");          // (255, 0, 0, 128)

if (CssColor.TryParse("rebeccapurple", out var purple))         // (102, 51, 153, 255)
{
    Console.WriteLine(CssColor.ToCss(purple));                  // Output: #663399
}
```

`TryParse(string?, out Rgba32)` accepts surrounding white space and:

- hexadecimal `#rgb`, `#rgba`, `#rrggbb`, and `#rrggbbaa` (the alpha pair comes **last**, as in CSS; this differs
  from `Rgba32.Parse`, which reads `#AARRGGBB`);
- the 148 CSS named colors (`steelblue`, `rebeccapurple`, both `gray` and `grey` spellings, and so on) and
  `transparent`, in any letter case;
- `rgb()` and `rgba()` with numbers (0 to 255) or percentages and an optional alpha as a number (0 to 1) or a
  percentage, in either CSS syntax: commas (`rgba(255, 0, 0, 0.5)`; three or four components) or spaces with an
  optional slash before the alpha (`rgb(10 20 30 / 50%)`). The two syntaxes are not mixed (`rgb(1 2 3 0.5)` and
  `rgb(1,2,3 / 0.5)` are not colors);
- `hsl()` and `hsla()` with a hue in degrees (or with the unit `deg`, `rad`, `grad`, or `turn`), saturation and
  lightness as percentages (the space syntax also accepts plain numbers: `hsl(0 100 50)`), and the same alpha forms.

Out-of-range values are clamped and fractions are rounded half away from zero (`rgb(30%, 0, 0)` has the red channel
77). `TryParse` returns `false` (and `default`) for anything else, including `null`, empty text, and `none`, and never
throws. `Parse(string)` is the throwing counterpart: `ArgumentNullException` for `null`, `FormatException` for text
that is not a color.

The building blocks are public for callers that accept a narrower grammar than CSS:
`TryParseHex(ReadOnlySpan<char>, out Rgba32)` parses just the digits after the `#` (3, 4, 6, or 8 hexadecimal digits),
and `TryGetNamedColor(string?, out Rgba32)` looks up an exact color name or `transparent`.
`ToCss(Rgba32)` formats a color as `#rrggbb`, or `#rrggbbaa` when it is not opaque, and `NamedColorCount` is 148.

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

### ImageDecoder

```csharp
public enum ImageFormat { Png, Jpeg, Gif, Bmp, Tiff, Jpeg2000 }

public static class ImageFormats
{
    public static ImageFormat? Detect(ReadOnlySpan<byte> header);
    public static ImageFormat? FromMediaType(string? mediaType);
}

public static class ImageDecoder
{
    public static Surface Load(Stream stream);
    public static Surface Load(ReadOnlySpan<byte> data);
    public static Surface Load(Stream stream, ImageFormat format);
}
```

`ImageDecoder` loads an image of any supported format into a `Surface` without a per-format
`switch`. It only recognizes the format and delegates to the matching codec's own `Load(Stream)`,
so every codec's limits, tolerances, and exceptions apply unchanged.

`ImageFormats.Detect` recognizes the format from the first bytes of a file (18 bytes are enough
for every format) and returns `null` when none matches; it never throws. A bitmap is recognized
only when its file header is plausible, because a bare `BM` also begins ordinary text.
`ImageFormats.FromMediaType` maps a media (MIME) type to a format, ignoring case, surrounding
whitespace, and `;` parameters, and accepting the common aliases (`image/jpg`, `image/x-bmp`,
`image/x-ms-bmp`, `image/x-tiff`); it returns `null` for `null`, empty, and unsupported types such
as `image/svg+xml`.

`ImageDecoder.Load(Stream)` detects the format from the stream's first bytes. A seekable stream is
rewound to where it started, also when the format is not recognized. A non-seekable stream is read
through with its first bytes replayed to the codec; codecs that need the whole file (JPEG, TIFF, and
JPEG 2000) still read it into memory. The
stream is read, not closed. `Load(ReadOnlySpan<byte>)` does the same for bytes already in memory,
and `Load(Stream, ImageFormat)` skips the detection and decodes as the format you name. Data with
no recognized signature throws `InvalidDataException`; a null stream throws
`ArgumentNullException`, and an undefined `ImageFormat` throws `ArgumentOutOfRangeException`.

```csharp
using DemaConsulting.CanvasNet.Codecs;

// Decode whatever a user uploaded, as long as it is one of the supported formats
using var upload = File.OpenRead("upload.bin");
using var surface = ImageDecoder.Load(upload);

// Choose the decoder from an HTTP Content-Type header
var format = ImageFormats.FromMediaType("Image/JPG; q=0.8");   // ImageFormat.Jpeg
```

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

    public static SystemFontInfo? FindInFamily(
        IReadOnlyList<SystemFontInfo> faces,
        string family,
        bool bold,
        bool italic);

    public static TrueTypeFont? Load(SystemFontInfo info);

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

`FindInFamily` is the exact-family building block of `FindBestMatch`: from any list of faces (the
`Fonts` catalog, or the result of `FontDirectoryScanner.Scan`) it returns the face whose family
name equals `family` (case-insensitively, with no generic-family substitution) and whose
bold/italic flags differ least from the request - the first of equally good faces wins - or `null`
when no face has the family.

`Load` loads the font a `SystemFontInfo` describes through `TrueTypeFont.Load`, remembering the
outcome for the lifetime of the process: the same instance is returned for every later request for
that file and face (from any thread), and `null` - also remembered - for a file that has vanished or
cannot be parsed, instead of an exception. Every loaded font stays in memory for the process
lifetime, so use it for the faces you actually select rather than to examine a whole catalog.

`LoadBundledFallback` selects one of the library's twelve embedded Liberation Sans/Serif/Mono
TrueType fonts (by the requested `serif`/`fixedPitch`/`bold`/`italic` combination) and loads it via
`TrueTypeFont.Load`, guaranteeing a usable font is always available even when the host system has
no suitable installed font (or no accessible font directory at all):

```csharp
// Best-effort match against installed system fonts, falling back to a bundled font
var match = SystemFontCatalog.FindBestMatch("Helvetica", bold: false, italic: false, serif: false, fixedPitch: false);
var font = (match is { } info ? SystemFontCatalog.Load(info) : null)
    ?? SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold: false, italic: false);
```

`SystemFontCatalog` is thread-safe: every member may be called concurrently.

### FontDirectoryScanner

The `FontDirectoryScanner` static class scans directory trees for font files and reports a
`SystemFontInfo` for every well-formed face (`.ttf`, `.otf`, `.ttc`, `.otc`, any case), reading
only each font's headers. It is what `SystemFontCatalog.Fonts` is built with, and is available for
fonts in directories of your own:

```csharp
public static class FontDirectoryScanner
{
    public static IReadOnlyList<SystemFontInfo> Scan(
        IEnumerable<string> directories,
        FontScanLimits? limits = null,
        bool trustLinkedFiles = false,
        CancellationToken cancellationToken = default);
}

public sealed record FontScanLimits
{
    public int MaxDirectories { get; init; }  // default 10,000
    public int MaxEntries { get; init; }      // default 200,000
    public int MaxFontFiles { get; init; }    // default 5,000
    public int MaxFaces { get; init; }        // default 20,000
}
```

The scan is deterministic - roots in the order given, and within a root depth first, each directory's
own font files (alphabetical, ignoring case) before its subdirectories - so the same tree always
yields the same faces in the same order. It stops at the first `FontScanLimits` bound it reaches in
that order (each at least 1, counted across all roots) and keeps what it found; a directory's entries
count against `MaxEntries` as a whole, so a directory the remaining budget cannot cover is not used
at all. A bound is not an error. A missing or unreadable directory, a font file that does not parse,
or one corrupt face of a collection is skipped. A subdirectory that is a link (symbolic link,
junction, or other reparse point) is never followed; a root that is a link and a font file that is
a link are skipped unless you pass `trustLinkedFiles: true`. `Scan` throws only
`ArgumentNullException` (for a `null` directory list) and `OperationCanceledException`, and is
thread-safe; `FontScanLimits` is immutable and may be shared.

```csharp
// Use a font from a directory of your own, falling back to the bundled font
var faces = FontDirectoryScanner.Scan([@"C:\MyFonts"]);
var wanted = SystemFontCatalog.FindInFamily(faces, "My Brand Sans", bold: true, italic: false);
var font = (wanted is { } face ? SystemFontCatalog.Load(face) : null)
    ?? SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold: true, italic: false);
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

### SandboxedFileResolver

```csharp
namespace DemaConsulting.CanvasNet.Files;

public sealed record SandboxOptions
{
    public string? BaseDirectory { get; init; }
    public IReadOnlyList<string> SearchPaths { get; init; }      // default: empty
    public Func<string, Stream?>? FileResolver { get; init; }
    public long MaxFileBytes { get; init; }                      // default: 16 MiB
    public int? MaxFiles { get; init; }                          // default: no limit
}

public sealed class SandboxedFileResolver
{
    public SandboxedFileResolver(SandboxOptions options, CancellationToken cancellationToken = default);
    public bool HasSource { get; }
    public StringComparer KeyComparer { get; }
    public SandboxedFile? Resolve(string path, SandboxedFile? relativeTo = null);
}

public sealed class SandboxedFile
{
    public string Key { get; }
    public string Directory { get; }
    public string Root { get; }
    public ReadOnlyMemory<byte> Content { get; }
}

public sealed class SandboxException : Exception
{
    public SandboxFailureKind Kind { get; }
    public string Path { get; }
    public string? Detail { get; }
}

public enum SandboxFailureKind
{
    EmptyPath, ControlCharacter, ColonInPath, AbsolutePath, OutsideRoot,
    ThroughLink, LinkCheckFailed, CannotRead, TooLarge, InvalidRoot, TooManyFiles,
}
```

`SandboxedFileResolver` turns an **untrusted relative path** (the text of an include directive, a data URL, a
manifest entry) into the bytes of a file that is guaranteed to lie inside directories you chose, and refuses
everything else. It is the resolver that the Vega-Lite and OpenSCAD packages use for their local files.

`Resolve` looks for the path in `relativeTo`'s directory when you pass the file a reference came from (it must have been
returned by this resolver or one with the same roots, or `Resolve` throws `ArgumentException`; so a
nested reference starts where the referring file lives), otherwise in `BaseDirectory`, and then in each entry of
`SearchPaths`, in order; the list is copied when the resolver is created. The first file that exists wins. It returns
`null` when no candidate exists (a missing
file is not an error). A backslash is the same as a slash. Each root is trusted, including a root you reach
through a link; the files below it are not:

- An empty or white-space path, an absolute path (a leading slash or backslash, UNC), a drive letter or any colon,
  and any control character (C0, DEL, or C1) are refused before anything is touched. A `..` that would leave the
  root the path is derived from is refused (`OutsideRoot`), even when the target exists; `.` and `..` that stay
  inside the root are normalized.
- A file that is, or lies below, a **symbolic link or junction** inside a root is refused (`ThroughLink`),
  whatever the link points to, and a link that points nowhere is a link, not a missing file. If the link check
  itself fails, the file is refused (`LinkCheckFailed`). To use linked content, name the real directory as a search
  path.
- A directory where a file is expected, a file that cannot be opened (for lack of permission, for example), and a
  stream that fails are `CannotRead` - never "missing". Files are opened read-only and tolerate other programs
  reading, writing, renaming, or deleting them at the same time.
- At most `MaxFileBytes` bytes are read; a longer file (or a stream that never ends) is `TooLarge`. The bytes
  come back in `SandboxedFile.Content`; decoding them is up to you.
- Set `MaxFiles` to cap the number of **distinct files** one resolver reads. A file already read (also through
  another spelling) never counts again and a file that does not exist does not count, while a file that is too large
  does count (it was opened); once the cap is reached, a new
  file is refused with `TooManyFiles` **before it is opened or the callback is asked for it**.
- With a `FileResolver` callback the file system is never touched: the callback receives the normalized logical
  path (first under the logical root, then under each valid `SearchPaths` entry used as a prefix), on the calling
  thread, at most once per path (also when the path is reached through different search-path prefixes), and returns
  a readable stream (disposed by the resolver) or `null`. A callback
  that throws, or whose stream fails, is `CannotRead` - even when what it throws is itself a `SandboxException`,
  which can never pass through with another kind; a cancellation passes through unchanged.

Every refusal is a `SandboxException`. Branch on `Kind` (never on the message): `Detail` is the reason phrase of
a path-text failure (`the path is empty`, ...) or, for `ThroughLink`, the link's path relative to its root, and
`Path` is the path as written (control characters replaced) for the path-text kinds and `OutsideRoot`, and
otherwise the file's full path. **The message never contains a host directory** and is safe to show to the author
of the path; `Path` of a file failure is the full path, so show it only to trusted callers, and the cause (when
there is one) is the inner exception. An already canceled `CancellationToken` throws
`OperationCanceledException` before anything is read.

Each distinct file is read **once per resolver** (per root it is reached through, so the link checks of every root
always run), and a miss or a too-large file is remembered too (it is not opened again), so a resolver serves one logical
operation (one render, one document) and is then dropped. **An instance is not thread-safe**: use one per
operation, or serialize access. `SandboxOptions` and the returned `SandboxedFile` are immutable and may be
shared. Use `resolver.KeyComparer` to decide whether two `Key` values name the same file (it is ordinal for
callback paths and case-insensitive for full paths on Windows and macOS).

**Security note.** The sandbox protects against path-based escapes in a *static* directory tree. It is **not a
security boundary against a concurrent local attacker** with write access to your directories: the link check and
the open are separate operations, so a component swapped for a link between them is not noticed, and a hard link
to a file outside the directories is read like any other file. Point it only at directories that untrusted
parties cannot write to, or serve the files yourself with `FileResolver`.

```csharp
using System.Text;
using DemaConsulting.CanvasNet.Files;

var resolver = new SandboxedFileResolver(new SandboxOptions
{
    BaseDirectory = "/srv/site/content",
    SearchPaths = ["/srv/site/shared"],
    MaxFileBytes = 1_000_000,
});

try
{
    // "path" came from an untrusted document
    var file = resolver.Resolve(path);
    if (file is null)
    {
        Console.WriteLine("Not found.");
    }
    else
    {
        var text = Encoding.UTF8.GetString(file.Content.Span);

        // A reference inside that file starts in the file's own directory
        var included = resolver.Resolve("parts/header.txt", relativeTo: file);
    }
}
catch (SandboxException ex) when (ex.Kind == SandboxFailureKind.ThroughLink)
{
    Console.WriteLine($"Links are not followed: {ex.Detail}");
}
catch (SandboxException ex)
{
    Console.WriteLine(ex.Message); // safe to show: names no host directory
}
```

**Exceptions:**

- `ArgumentNullException`: Thrown by the constructor when `options` or its `SearchPaths` is null, and by `Resolve`
  when `path` is null.
- `ArgumentOutOfRangeException`: Thrown by the constructor when `MaxFileBytes` is not positive or `MaxFiles` is negative.
- `ArgumentException`: Thrown by `Resolve` when `relativeTo` was not resolved through one of this resolver's own roots.
- `SandboxException`: Thrown by `Resolve` for every refusal and read failure described above.
- `OperationCanceledException`: Thrown by `Resolve` when the token is canceled.

## Examples

### Example 1: Surface Pixel Access

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

using var surface = new Surface(4, 4);
surface[1, 1] = new Rgba32(255, 0, 0, 255); // opaque red pixel
var pixel = surface[1, 1];
Console.WriteLine(pixel.R); // Output: 255
```

### Example 2: Surface Crop

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

### Example 3: Load, Crop, Resize and Save

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

using var original = PngCodec.Load("photo.png");
using var cropped = original.Crop(10, 10, 200, 100);
using var thumbnail = cropped.ResizeToFit(64, 64, ResamplingMode.Lanczos3);
PngCodec.Save(thumbnail, "thumbnail.png");
```

### Example 4: BMP Load/Save

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

### Example 5: PNG Load/Save

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

### Example 6: TIFF Load/Save

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

### Example 7: JPEG Load/Save

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

### Example 8: Compositing a Semi-Transparent Color Over a Background

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

### Example 9: Header-Only Probing Before Load (Decompression-Bomb Triage)

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

#### Handling Unsupported Features

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

### Example 10: Filling a Vector Path

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

### Example 11: Stroking a Vector Path

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

### Example 12: Filling a Vector Path with a Radial Gradient

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

### Example 13: Loading a TrueType Font and Filling a Glyph Outline

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

### Example 14: Combining Paths with PathBuilder.AppendPath

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using System.Numerics;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

var square = Path.Rectangle(0, 0, 10, 10);

// Append the same outline several times, each moved by its own transform, into one combined path.
var builder = new PathBuilder();
for (var i = 0; i < 3; i++)
{
    builder.AppendPath(square, Matrix3x2.CreateTranslation(i * 20, 0));
}

// The overload without a matrix copies the subpaths exactly as stored (elliptical arcs stay arcs).
builder.AppendPath(Path.Circle(15, 30, 8));

var combined = builder.Build();
Console.WriteLine(combined.Subpaths.Count); // Output: 4

using var surface = new Surface(64, 64);
PathFiller.Fill(surface, combined, new Rgba32(0, 128, 255, 255));
```

`AppendPath(path, transform)` maps every point through the matrix (row-vector convention, like
`Path.Transform`, which is implemented with it); an elliptical-arc command is converted to cubic
Bezier segments first because an arc cannot be re-expressed under an arbitrary matrix.
A subpath the builder still had open is kept as open when the next one is appended, and a `null`
path throws `ArgumentNullException`. The builder is not thread-safe.

### Rendering (transform-aware Canvas, text, and shapes)

The `Rendering` subsystem adds a transform-aware `Canvas` wrapper over `Surface`, along with
text rendering and shape helpers.

#### Canvas transform stack

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

#### DrawText and MeasureText

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

#### CreateTextPath

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Rendering;
using System.Numerics;

using var stream = File.OpenRead("OpenSans-Regular.ttf");
var font = TrueTypeFont.Load(stream);

// The text as one outline path: baseline at y = 100, right edge at x = 180, no canvas transform applied.
var outline = TextRenderer.CreateTextPath("Hello", font, size: 32f, x: 180, y: 100, TextAlign.Right);

// Move it, then fill it (or stroke it, or use it as a clip) like any other path.
using var surface = new Surface(200, 200);
PathFiller.Fill(surface, outline.Transform(Matrix3x2.CreateTranslation(0, 20)), new Rgba32(0, 0, 0, 255));
```

`CreateTextPath` places glyphs with the same pen advance, kerning, and alignment as `DrawText` and
`MeasureText`, in y-down coordinates, and returns `Path.Empty` for empty or whitespace-only text. All
glyphs are merged into one path, so overlapping glyphs are filled as a single shape under the
fill rule (`DrawText` composites them one after another). `size` must be finite and positive; a
`null` text or font throws `ArgumentNullException`, and an undefined `TextAlign` throws
`ArgumentOutOfRangeException`. The method is pure and safe for concurrent use.

#### TruncateToWidth

```csharp
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Rendering;

using var stream = File.OpenRead("OpenSans-Regular.ttf");
var font = TrueTypeFont.Load(stream);

// Fit a label into 120 pixels at 14 px; the removed end is replaced by the ellipsis.
var label = TextRenderer.TruncateToWidth("Quarterly revenue by region and product line", font, size: 14f, maxWidth: 120f);
Console.WriteLine(label); // Output: a beginning of the text followed by "…" that measures at most 120 pixels

// Choose the ellipsis explicitly (here plain ASCII).
var ascii = TextRenderer.TruncateToWidth("Quarterly revenue by region", font, 14f, 120f, ellipsis: "...");
```

`TruncateToWidth` returns the text itself when it fits, otherwise the longest beginning that, followed
by the ellipsis (default U+2026), fits `maxWidth`, and the empty string when not even the ellipsis fits
(including a `NaN` or negative `maxWidth` for non-empty text). The cut is made between whole characters,
so a surrogate pair is never split, and an unpaired surrogate in a shortened result becomes U+FFFD.
Trailing white space of the kept beginning is not trimmed. An empty `ellipsis` gives a plain cut. A
`null` text, font, or ellipsis throws `ArgumentNullException`; a `size` that is not finite and
positive throws `ArgumentOutOfRangeException`, even for text that would fit. The method is pure and
safe for concurrent use.

#### Shape helpers

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

#### CornerRoundEffect

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

#### Rgba32.Parse

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
