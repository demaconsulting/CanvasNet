# System Design

This document provides the system-level design for CanvasNet.

![CanvasNet Structure](CanvasNetView.svg)

## Architecture

CanvasNet is a .NET library providing a canvas-based drawing and rendering API, following
DEMA Consulting best practices. The system consists of four implemented subsystems:

- **Canvas subsystem** (namespace `DemaConsulting.CanvasNet.Canvas`, folder
  `src/DemaConsulting.CanvasNet/Canvas/`): the pixel-buffer primitives on which all other
  functionality builds — the `Surface` unit (a mutable, in-memory 32-bit RGBA pixel buffer with
  span-based row access and independent-copy cropping) and the `Rgba32` unit (a single-pixel
  value type, documented inline within `Surface`). See _Canvas Subsystem Design_ (`canvas.md`).
- **Codecs subsystem** (namespace `DemaConsulting.CanvasNet.Codecs`, folder
  `src/DemaConsulting.CanvasNet/Codecs/`, flat — no further nesting): four hand-rolled image
  format codecs, each converting to and from a `DemaConsulting.CanvasNet.Canvas.Surface` pixel buffer —
  `BmpCodec` (uncompressed 24-bit/32-bit Windows BMP), `PngCodec` (8-bit-per-channel Truecolor
  and Truecolor-with-alpha, non-interlaced PNG), `TiffCodec` (8-bit-per-sample RGB, RGBA, and
  Grayscale, strip-based TIFF 6.0 with None/PackBits/LZW/Deflate compression, either byte order),
  and `JpegCodec` (a common real-world subset of JPEG: baseline/progressive decode with
  4:4:4/4:2:2/4:2:0 support, baseline 4:2:0 encode). See _Codecs Subsystem Design_ (`codecs.md`).
- **Geometry subsystem** (namespace `DemaConsulting.CanvasNet.Geometry`, folder
  `src/DemaConsulting.CanvasNet/Geometry/`, flat — no further nesting): vector-geometry
  primitives, built directly on `System.Numerics.Vector2`/`Matrix3x2` — the `Rect` unit (an
  axis-aligned bounding rectangle with a union-identity `Empty` sentinel), the `Path` unit (an
  immutable vector path and its fluent `PathBuilder`, together with the supporting `Subpath`,
  `PathCommand`, and `PathCommandType` types documented inline), the `BezierFlattening` unit
  (adaptive quadratic/cubic Bezier curve flattening), and the `SvgArcConverter` unit
  (SVG-style elliptical arc to cubic Bezier conversion). `Geometry` has no dependency on `Canvas`
  or `Codecs`, and neither of those subsystems depends on `Geometry`. See
  _Geometry Subsystem Design_ (`geometry.md`).
- **Drawing subsystem** (namespace `DemaConsulting.CanvasNet.Drawing`, folder
  `src/DemaConsulting.CanvasNet/Drawing/`, flat — no further nesting): an antialiased
  scanline-coverage fill rasterizer for closed `Geometry.Path` geometry with solid-color paint —
  the `PathFiller` unit (a public static `Fill` entry point, with the supporting `FillRule` enum
  and the internal `EdgeFlattener`/`ScanlineRasterizer` helpers documented inline). `Drawing`
  consumes both the `Canvas` subsystem's `Surface` unit (via `Surface.CompositeOverSpan`) and the
  `Geometry` subsystem's `Path`/`BezierFlattening`/`SvgArcConverter` units; neither `Canvas` nor
  `Geometry` depends on `Drawing`. `Geometry` is deliberately distinct from `Drawing`: `Geometry`
  describes shape geometry (paths, bounds, curve math) with no notion of pixels, color, or
  rasterization, while `Drawing` turns that geometry into pixels. Strokes, gradients, and fonts
  are reserved for later phases. See _Drawing Subsystem Design_ (`drawing.md`).

The `Codecs` subsystem depends on the `Canvas` subsystem's `Surface` unit (constructing surfaces
and reading/writing rows via `Surface.GetRowSpanBytes`); the `Canvas` subsystem has no dependency
on `Codecs` or on any other subsystem. Within the `Codecs` subsystem, its four units are flat and
mutually independent — none of `BmpCodec`, `PngCodec`, `TiffCodec`, or `JpegCodec` depends on any
other codec. Each codec also exposes a pair of `GetInfo(Stream)`/`GetInfo(string)` overloads
returning the shared `ImageInfo` record struct (width, height, channel count, and alpha presence)
without fully decoding pixel data and without enforcing `Surface.MaxDimension` (now a public
constant, so callers can perform this comparison themselves before ever calling `Load`) — see
_Codecs Subsystem Design_ (`codecs.md`) for the shared `ImageInfo` type and header-only-probing
pattern, and _Surface Unit Design_ (`canvas/surface.md`) for `MaxDimension`. See
_Surface Unit Design_ (`canvas/surface.md`), _BmpCodec Unit Design_
(`codecs/bmp-codec.md`), _PngCodec Unit Design_ (`codecs/png-codec.md`),
_TiffCodec Unit Design_ (`codecs/tiff-codec.md`), and _JpegCodec Unit Design_
(`codecs/jpeg-codec.md`) for each unit's internal collaboration.

The `Geometry` subsystem's four units collaborate as follows: `PathBuilder` records raw drawing
commands (including raw, unconverted SVG arc parameters) and produces immutable `Path` snapshots;
`Path.GetBounds` is the primary internal consumer of both `BezierFlattening` (to flatten curves
when a tighter, tolerance-based bound is requested) and `SvgArcConverter` (to convert any `ArcTo`
command to cubic Bezier segments before applying either bounds mode, since arcs carry no control
points of their own). `Rect` has no dependency on the other three units, but is the return type of
`Path.GetBounds` and is used throughout as the common bounding-box representation. See
_Geometry Subsystem Design_ (`geometry.md`), _Rect Unit Design_ (`geometry/rect.md`),
_Path Unit Design_ (`geometry/path.md`), _BezierFlattening Unit Design_
(`geometry/bezier-flattening.md`), and _SvgArcConverter Unit Design_
(`geometry/svg-arc-converter.md`) for each unit's internal collaboration.

The `Drawing` subsystem's single unit, `PathFiller`, collaborates as follows: `PathFiller.Fill`
first delegates to the internal `EdgeFlattener` (which converts the target `Geometry.Path`'s
subpaths to closed polygons, flattening curves via `Geometry.BezierFlattening` and arcs via
`Geometry.SvgArcConverter`), then computes the flattened polygons' bounds and intersects them with
the `Canvas.Surface`'s pixel extent (a no-op if the path is empty or the intersection is empty),
then delegates to the internal `ScanlineRasterizer` (which rasterizes those polygons into per-row
antialiased coverage and composites each row directly via `Canvas.Surface.CompositeOverSpan`). See
_Drawing Subsystem Design_ (`drawing.md`) and _PathFiller Unit Design_
(`drawing/path-filler.md`) for full detail.

## External Interfaces

The system exposes the following public API to external consumers:

- **Surface(int width, int height)**: Constructor; allocates a fully transparent pixel buffer of
  the specified size. Throws `ArgumentOutOfRangeException` if `width` or `height` is less than
  or equal to zero.
- **Surface.Width** / **Surface.Height**: Read-only properties exposing the surface dimensions.
- **Surface[int x, int y]**: Indexer providing single-pixel get/set access. Throws
  `ArgumentOutOfRangeException` if `x` or `y` is out of range.
- **Surface.GetRowSpanBytes(int y)**: Returns a `Span<byte>` over a row's raw bytes. Throws
  `ArgumentOutOfRangeException` if `y` is out of range.
- **Surface.GetRowSpan(int y)**: Returns a `Span<Rgba32>` over a row's pixels. Throws
  `ArgumentOutOfRangeException` if `y` is out of range.
- **Surface.Crop(int x, int y, int width, int height)**: Returns a new, independent `Surface`
  containing a copy of the specified sub-region. Throws `ArgumentOutOfRangeException` if any
  argument is invalid or the region exceeds the source bounds.
- **Surface.PremultiplyAlpha()**: Converts the surface's pixels from straight (unassociated) alpha
  to premultiplied alpha in place.
- **Surface.UnpremultiplyAlpha()**: Converts the surface's pixels from premultiplied alpha back to
  straight (unassociated) alpha in place; a pixel with zero alpha is left as fully transparent
  black.
- **Surface.CompositeOver(Surface foreground)**: Composites `foreground` over this surface in
  place using the Porter-Duff "over" operator. Throws `ArgumentNullException` for a null
  `foreground`, and `ArgumentException` if `foreground`'s dimensions differ from this surface's.
- **Surface.CompositeOver(Rgba32 color)**: Composites a single solid `color` over every pixel of
  this surface in place using the Porter-Duff "over" operator.
- **Surface.CompositeOverSpan(int y, int x, ReadOnlySpan\<float\> coverage, Rgba32 color)**:
  Composites `color` over a horizontal run of `coverage.Length` pixels in row `y` starting at
  column `x`, scaling `color`'s effective alpha at each pixel by the corresponding `coverage`
  value (clamped to `[0, 1]`), using the same Porter-Duff "over" formula. Throws
  `ArgumentOutOfRangeException` if `y` is outside `[0, Height)` or the `[x, x + coverage.Length)`
  column range is outside `[0, Width]`.
- **BmpCodec.Load(Stream stream)** / **BmpCodec.Load(string path)**: Loads a `Surface` from an
  uncompressed 24-bit or 32-bit BMP stream or file. Throws `ArgumentNullException` for a null
  `stream`/`path`, `ArgumentException` for an empty `path`, and `InvalidDataException` for
  malformed or unsupported BMP data.
- **BmpCodec.Save(Surface surface, Stream stream, BmpBitDepth bitDepth)** /
  **BmpCodec.Save(Surface surface, string path, BmpBitDepth bitDepth)**: Saves a `Surface` as an
  uncompressed 24-bit or 32-bit BMP stream or file. Throws `ArgumentNullException` for a null
  `surface`/`stream`/`path`, `ArgumentException` for an empty `path`, and
  `ArgumentOutOfRangeException` for an undefined `bitDepth`.
- **PngCodec.Load(Stream stream)** / **PngCodec.Load(string path)**: Loads a `Surface` from an
  8-bit-per-channel Truecolor (RGB) or Truecolor-with-alpha (RGBA), non-interlaced PNG stream or
  file. Throws `ArgumentNullException` for a null `stream`/`path`, `ArgumentException` for an
  empty `path`, and `InvalidDataException` for malformed or unsupported PNG data.
- **PngCodec.Save(Surface surface, Stream stream, PngColorType colorType)** /
  **PngCodec.Save(Surface surface, string path, PngColorType colorType)**: Saves a `Surface` as an
  8-bit-per-channel RGB or RGBA PNG stream or file. Throws `ArgumentNullException` for a null
  `surface`/`stream`/`path`, `ArgumentException` for an empty `path`, and
  `ArgumentOutOfRangeException` for an undefined `colorType`.
- **TiffCodec.Load(Stream stream)** / **TiffCodec.Load(string path)**: Loads a `Surface` from an
  8-bit-per-sample RGB, RGBA, or Grayscale, strip-based TIFF stream or file (either byte order).
  Throws `ArgumentNullException` for a null `stream`/`path`, `ArgumentException` for an empty
  `path`, and `InvalidDataException` for malformed or unsupported TIFF data.
- **TiffCodec.Save(Surface surface, Stream stream, TiffCompression compression)** /
  **TiffCodec.Save(Surface surface, string path, TiffCompression compression)**: Saves a `Surface`
  as an 8-bit RGBA, single-strip, little-endian TIFF stream or file, with optional
  None/PackBits/LZW/Deflate compression. Throws `ArgumentNullException` for a null
  `surface`/`stream`/`path`, `ArgumentException` for an empty `path`, and
  `ArgumentOutOfRangeException` for an undefined `compression`.
- **JpegCodec.Load(Stream stream)** / **JpegCodec.Load(string path)**: Loads a `Surface` from a
  supported JPEG stream or file (baseline SOF0 or progressive SOF2; grayscale or 3-component
  YCbCr; 4:4:4/4:2:2/4:2:0 decode support). Throws `ArgumentNullException` for a null
  `stream`/`path`, `ArgumentException` for an empty `path`, and `InvalidDataException` for
  malformed or unsupported JPEG data.
- **JpegCodec.Save(Surface surface, Stream stream, int quality)** /
  **JpegCodec.Save(Surface surface, string path, int quality)**: Saves a `Surface` as a baseline,
  3-component YCbCr, 4:2:0 chroma-subsampled JPEG stream or file, with quality in the inclusive
  range 1-100. Throws `ArgumentNullException` for a null `surface`/`stream`/`path`,
  `ArgumentException` for an empty `path`, and `ArgumentOutOfRangeException` for an out-of-range
  `quality`.
- **Rect(float x, float y, float width, float height)**: Constructor; an axis-aligned rectangle in
  position-plus-size form. `Rect.Empty` is a static, publicly readable union-identity sentinel.
- **Rect.Union(Rect)** / **Rect.Union(Rect, Rect)**: Returns the smallest rectangle enclosing both
  rectangles (instance and static forms).
- **Rect.Intersect(Rect)** / **Rect.Intersect(Rect, Rect)**: Returns the overlapping region, or
  `Rect.Empty` if the rectangles are disjoint (instance and static forms).
- **Rect.Transform(Matrix3x2)**: Returns the smallest axis-aligned rectangle enclosing this
  rectangle after applying the given transform to all four corners.
- **Rect.Contains(Vector2)**: Returns whether a point lies within the rectangle (half-open on both
  axes).
- **PathBuilder()**: Constructor; a reusable, mutable, fluent path builder.
- **PathBuilder.MoveTo/LineTo/QuadraticBezierTo/CubicBezierTo/ArcTo(...)**: Fluent, `this`-returning
  methods appending a drawing command to the current subpath (or starting a new one, for
  `MoveTo`). `LineTo`/`QuadraticBezierTo`/`CubicBezierTo`/`ArcTo`/`Close` throw
  `InvalidOperationException` if called before the first `MoveTo`, or after `Close` without an
  intervening `MoveTo`.
- **PathBuilder.Close()**: Fluent method marking the current subpath as closed.
- **PathBuilder.Build()**: Returns an immutable `Path` snapshot of every command issued so far;
  the builder remains usable afterward.
- **PathBuilder.Clear()**: Resets the builder to its initial empty state for reuse.
- **Path.Subpaths**: Read-only property exposing the path's ordered, independent subpaths.
- **Path.GetBounds(float flattenTolerance = 0)**: Returns a conservative (default) or, given a
  positive `flattenTolerance`, a tighter flattening-based axis-aligned bounding `Rect`.
- **Path.Empty**: Static singleton with zero subpaths.
- **BezierFlattening.FlattenCubic(...)** / **BezierFlattening.FlattenQuadratic(...)**: Appends the
  adaptively flattened polyline points (start point never written, end point always written
  last) for a cubic or quadratic Bezier curve to a caller-supplied `IList<Vector2>`. Throws
  `ArgumentOutOfRangeException` if `tolerance` is less than or equal to zero.
- **SvgArcConverter.ToBeziers(...)**: Appends the cubic Bezier segments equivalent to an SVG-style
  elliptical arc to a caller-supplied output list, in end-to-end order. Never throws for any
  SVG-valid input.
- **PathFiller.Fill(Surface, Path, Rgba32, FillRule, float)**: Fills a closed `Path` with a solid
  color onto a `Surface` using an antialiased scanline-coverage rasterizer, with a fill rule
  (`FillRule.NonZero` by default, or `FillRule.EvenOdd`) and a curve-flattening tolerance
  (`0.25f` by default). No-ops if the path is empty or its bounds do not intersect the surface.
  Throws `ArgumentNullException` for a null `surface`/`path`, and
  `ArgumentOutOfRangeException` for a non-finite or non-positive `flattenTolerance`.

| Interface                        | Direction        | Format                         | Constraints                   |
| -------------------------------- | ---------------- | ------------------------------ | ----------------------------- |
| `Surface(int, int)`              | Inbound          | Constructor call               | `width`, `height` in 1-8192   |
| `Surface[int, int]`              | Inbound/Outbound | Indexer get/set                | `x`, `y` within bounds        |
| `Surface.GetRowSpanBytes(int)`   | Outbound         | `Span<byte>` return            | `y` within bounds             |
| `Surface.GetRowSpan(int)`        | Outbound         | `Span<Rgba32>` return          | `y` within bounds             |
| `Surface.Crop(int,int,int,int)`  | Inbound/Outbound | Method call / `Surface` return | Region within source bounds   |
| `Surface.PremultiplyAlpha()`     | Inbound          | Method call                    | None                          |
| `Surface.UnpremultiplyAlpha()`   | Inbound          | Method call                    | None                          |
| `Surface.CompositeOver(Surface)` | Inbound          | Method call                    | Equal dimensions, non-null    |
| `Surface.CompositeOver(Rgba32)`  | Inbound          | Method call                    | None                          |
| `Surface.CompositeOverSpan(...)` | Inbound          | Method call                    | `y`, `x`+run within bounds    |
| `BmpCodec.Load(...)`             | Inbound/Outbound | Method call / `Surface` return | Valid BMP stream or path      |
| `BmpCodec.Save(...)`             | Inbound          | Method call                    | `surface` non-null            |
| `PngCodec.Load(...)`             | Inbound/Outbound | Method call / `Surface` return | Valid PNG stream or path      |
| `PngCodec.Save(...)`             | Inbound          | Method call                    | `surface` non-null            |
| `TiffCodec.Load(...)`            | Inbound/Outbound | Method call / `Surface` return | Valid TIFF stream or path     |
| `TiffCodec.Save(...)`            | Inbound          | Method call                    | `surface` non-null            |
| `JpegCodec.Load(...)`            | Inbound/Outbound | Method call / `Surface` return | Valid JPEG stream or path     |
| `JpegCodec.Save(...)`            | Inbound          | Method call                    | `surface` non-null            |
| `Rect.Union(...)`                | Inbound/Outbound | Method call / `Rect` return    | None                          |
| `Rect.Intersect(...)`            | Inbound/Outbound | Method call / `Rect` return    | None                          |
| `Rect.Transform(Matrix3x2)`      | Inbound/Outbound | Method call / `Rect` return    | None                          |
| `PathBuilder.*To(...)`           | Inbound/Outbound | Method call / `this` return    | Called after `MoveTo`         |
| `PathBuilder.Build()`            | Outbound         | Method call / `Path` return    | None                          |
| `Path.GetBounds(float)`          | Outbound         | Method call / `Rect` return    | None                          |
| `BezierFlattening.Flatten*(...)` | Inbound/Outbound | Method call / list append      | `tolerance` greater than zero |
| `SvgArcConverter.ToBeziers(...)` | Inbound/Outbound | Method call / list append      | None                          |
| `PathFiller.Fill(...)`           | Inbound          | Method call                    | Non-null; tolerance > 0       |

## Dependencies

CanvasNet has one runtime NuGet dependency: `System.Numerics.Tensors`, used by the `Surface`
unit's vectorized bulk pixel operations (`PremultiplyAlpha`, `UnpremultiplyAlpha`,
`CompositeOver`) for their `TensorPrimitives`-based numeric work — see _Surface Unit Design_
(`canvas/surface.md`) for details. Every other member of `Surface`, and all of `BmpCodec`,
`PngCodec`, `TiffCodec`, and `JpegCodec`, are implemented exclusively against the .NET Base Class
Library (`Surface`'s remaining use of `Span<T>` and `MemoryMarshal` are BCL APIs available
natively on every target framework; `BmpCodec` uses only `System.IO` types; `PngCodec` and
`TiffCodec` additionally use `System.IO.Compression.DeflateStream`; `JpegCodec` additionally uses
`System.Numerics.Vector<T>` for optional SIMD acceleration; all of these are BCL APIs available on
every target framework, with no additional runtime NuGet package required). The `Geometry`
subsystem introduces zero new runtime NuGet dependencies: it is implemented entirely against
`System.Numerics.Vector2` and `System.Numerics.Matrix3x2`, which are in-box BCL types available
natively on every target framework this library supports (net8.0, net9.0, net10.0) — no custom
point/vector wrapper types were introduced. The `Drawing` subsystem likewise introduces zero new
runtime NuGet dependencies: `PathFiller`, `FillRule`, `EdgeFlattener`, and `ScanlineRasterizer`
are implemented entirely against `System.Numerics.Vector2`, in-box `List<T>`/array types, and the
existing `Geometry` and `Canvas` subsystem APIs (`Path`, `BezierFlattening`, `SvgArcConverter`,
`Surface.CompositeOverSpan`) — no new package reference was added to the project file. The
following OTS
items are used for building and verifying this system (not consumed at runtime); see
_OTS Integration Design_ (`docs/design/ots.md`) and each item's dedicated design document for
details:

- **BuildMark** — generates build-notes documentation; see _BuildMark Design_
- **FileAssert** — validates generated documents against acceptance criteria; see
  _FileAssert Design_
- **Pandoc** — converts Markdown documentation to HTML; see _Pandoc Design_
- **ReqStream** — enforces requirements-to-test traceability; see _ReqStream Design_
- **ReviewMark** — enforces file review coverage and currency; see _ReviewMark Design_
- **SarifMark** — converts CodeQL SARIF results to markdown; see _SarifMark Design_
- **SonarMark** — generates SonarCloud quality reports; see _SonarMark Design_
- **VersionMark** — captures and publishes tool-version information; see _VersionMark Design_
- **WeasyPrint** — converts HTML documentation to PDF; see _WeasyPrint Design_
- **xUnit** — executes unit and integration tests; see _xUnit Design_

## Risk Control Measures

N/A - CanvasNet currently has no safety-critical functionality requiring risk control
measures (IEC 62304 §5.3.3).

## Data Flow

**Surface construction/pixel-access path:**

1. **Input**: Constructor parameters `width`/`height`; subsequent pixel writes via the indexer,
   `GetRowSpanBytes`, or `GetRowSpan`
2. **Validation**: `Surface(int, int)` rejects non-positive dimensions with
   `ArgumentOutOfRangeException`; the indexer and both row accessors reject out-of-range
   coordinates the same way
3. **Storage**: A zero-filled `byte[]` is allocated once at construction time; pixel writes
   mutate this buffer directly, whether through the indexer or a row span
4. **Output**: Pixel reads (via the indexer or a row span) return values directly from the same
   buffer — no intermediate copies

**Surface crop path:**

1. **Input**: Method parameters `x`, `y`, `width`, `height` identifying a sub-region
2. **Validation**: `Crop` rejects negative coordinates, non-positive sizes, and regions
   exceeding the source bounds with `ArgumentOutOfRangeException`
3. **Processing**: A new `Surface` is allocated for the destination, then each row of the source
   region is copied into it in a single `Span<byte>.CopyTo` call
4. **Output**: A new, independent `Surface` containing a copy of the requested pixels

**BMP save path:**

1. **Input**: Method parameters `surface`, `stream`/`path`, and `bitDepth`
2. **Validation**: `Save` rejects a null `surface`/`stream`/`path` with `ArgumentNullException`,
   an empty `path` with `ArgumentException`, and an undefined `bitDepth` with
   `ArgumentOutOfRangeException`
3. **Processing**: Writes the BITMAPFILEHEADER and BITMAPINFOHEADER, then writes each surface row
   bottom-up into a single reused scratch buffer, swapping RGBA to BGR(A) and zero-filling any
   4-byte row-padding
4. **Output**: A complete, uncompressed BMP file written to the destination stream/file

**BMP load path:**

1. **Input**: Method parameter `stream`/`path`
2. **Validation**: `Load` rejects a null `stream`/`path` with `ArgumentNullException`, an empty
   `path` with `ArgumentException`, and malformed/unsupported BMP data (bad signature,
   unsupported header size, unsupported compression, unsupported bit depth, top-down
   orientation, truncated stream) with `InvalidDataException`
3. **Processing**: Reads each padded pixel row bottom-up into a reused scratch buffer, swapping
   BGR(A) to RGBA and forcing alpha to 255 for 24-bit source data, writing directly into the
   destination `Surface`'s rows via `Surface.GetRowSpanBytes`
4. **Output**: A new `Surface` containing the decoded pixels

**PNG save path:**

1. **Input**: Method parameters `surface`, `stream`/`path`, and `colorType`
2. **Validation**: `Save` rejects a null `surface`/`stream`/`path` with `ArgumentNullException`,
   an empty `path` with `ArgumentException`, and an undefined `colorType` with
   `ArgumentOutOfRangeException`
3. **Processing**: Writes the PNG signature and `IHDR` chunk, builds one in-memory buffer of
   every scanline (filter type 0/None, followed by packed RGB or RGBA pixel bytes), zlib-wraps
   the DEFLATE-compressed buffer with a fixed header and a computed Adler-32 trailer, and writes
   the compressed payload across as many `IDAT` chunks as needed, followed by an empty `IEND`
   chunk
4. **Output**: A complete PNG file written to the destination stream/file

**PNG load path:**

1. **Input**: Method parameter `stream`/`path`
2. **Validation**: `Load` rejects a null `stream`/`path` with `ArgumentNullException`, an empty
   `path` with `ArgumentException`, and malformed/unsupported PNG data (bad signature, unsupported
   color type, bit depth, compression method, filter method, or interlace method, a chunk CRC-32
   mismatch, a malformed or unsupported zlib header, an Adler-32 checksum mismatch, an unsupported
   scanline filter type, or a truncated stream) with `InvalidDataException`
3. **Processing**: Validates the signature and every chunk's CRC-32, parses `IHDR`, concatenates
   `IDAT` data across all chunks present, zlib-unwraps and inflates the payload, defilters each
   scanline (reconstructing all five standard filter types), and unpacks each row directly into
   the destination `Surface`'s rows via `Surface.GetRowSpanBytes`, forcing alpha to 255 for RGB
   source data
4. **Output**: A new `Surface` containing the decoded pixels

**TIFF save path:**

1. **Input**: Method parameters `surface`, `stream`/`path`, and `compression`
2. **Validation**: `Save` rejects a null `surface`/`stream`/`path` with `ArgumentNullException`,
   an empty `path` with `ArgumentException`, and an undefined `compression` with
   `ArgumentOutOfRangeException`
3. **Processing**: Packs every row's pixels as RGBA into a single in-memory buffer, applies the
   horizontal-differencing predictor to that buffer when `compression` is `Lzw` or `Deflate`,
   compresses the buffer according to `compression` (None/PackBits/LZW/Deflate), then writes a
   little-endian TIFF header and a single IFD (describing an 8-bit RGBA, Chunky, single-strip
   image, with a `Predictor` tag only when the predictor was applied) followed by the compressed
   strip data
4. **Output**: A complete TIFF file written to the destination stream/file

**TIFF load path:**

1. **Input**: Method parameter `stream`/`path`
2. **Validation**: `Load` rejects a null `stream`/`path` with `ArgumentNullException`, an empty
   `path` with `ArgumentException`, and malformed/unsupported TIFF data (bad byte-order mark,
   unsupported bit depth, photometric interpretation, compression, or planar configuration, a
   tiled layout, a missing mandatory tag, or a truncated stream) with `InvalidDataException`
3. **Processing**: Buffers the whole stream into memory (TIFF's IFD/value-array offsets require
   random access), validates the header and IFD tags, then for each strip decompresses its bytes
   according to `Compression`, reverses the horizontal-differencing predictor if `Predictor` is
   2, and unpacks each row (RGB, RGBA, or Grayscale) directly into the destination `Surface`'s
   rows via `Surface.GetRowSpanBytes`, forcing alpha to 255 where the source has no alpha channel
4. **Output**: A new `Surface` containing the decoded pixels

**JPEG save path:**

1. **Input**: Method parameters `surface`, `stream`/`path`, and `quality`
2. **Validation**: `Save` rejects a null `surface`/`stream`/`path` with `ArgumentNullException`, an
   empty `path` with `ArgumentException`, and a `quality` outside 1-100 with
   `ArgumentOutOfRangeException`
3. **Processing**: Converts surface rows from RGB to YCbCr, pads partial MCUs by edge replication,
   downsamples chroma to 4:2:0, performs a separable float FDCT, quantizes coefficients with
   quality-scaled Annex K luminance/chrominance tables, Huffman-encodes the zigzag-ordered blocks
   with Annex K default Huffman tables, and writes the required SOI/DQT/SOF0/DHT/SOS/EOI marker
   sequence
4. **Output**: A complete baseline JPEG file written to the destination stream/file

**JPEG load path:**

1. **Input**: Method parameter `stream`/`path`
2. **Validation**: `Load` rejects a null `stream`/`path` with `ArgumentNullException`, an empty
   `path` with `ArgumentException`, and malformed/unsupported JPEG data (missing SOI, unsupported
   SOF marker, unsupported component count, missing DHT/DQT/SOF/SOS segment, malformed marker
   structure, or a truncated stream) with `InvalidDataException`
3. **Processing**: Buffers the remaining stream into memory, parses DQT/DHT/DRI/SOF/SOS markers,
   decodes one or more baseline or progressive scans into coefficient blocks, dequantizes and
   applies the separable float IDCT, upsamples chroma according to the frame's sampling factors,
   converts YCbCr back to RGB (using the vectorized path where available), and writes fully opaque
   pixels into the destination `Surface`
4. **Output**: A new `Surface` containing the decoded pixels

## Design Constraints

- **Simplicity**: Minimal functionality kept easy to understand and extend
- **Compliance**: All functionality must be traceable to requirements
- **Quality**: Zero warnings, full test coverage, complete documentation
- **Portability**: Compatible across supported .NET platforms

### Platform Support

The library targets the following frameworks, enabling compatibility across modern, currently
supported .NET runtimes:

| Target Framework | Runtime / Environment                             |
| ---------------- | ------------------------------------------------- |
| `net8.0`         | .NET 8 LTS                                        |
| `net9.0`         | .NET 9                                            |
| `net10.0`        | .NET 10                                           |

The library is supported on the following operating systems:

- **Windows** — primary developer and CI platform
- **Linux** — CI/CD and containerized environments
- **macOS** — developer workstations using Apple platforms

Portability is achieved by restricting the implementation exclusively to Base Class Library (BCL)
APIs available across all target frameworks. No platform-specific native interop, OS-specific
APIs, or framework-version-specific features are used.

### Integration Patterns

- **NuGet Packaging**: Standard .NET library packaging and distribution
- **CI/CD Integration**: Automated build, test, and quality validation
- **Requirements Traceability**: All features linked to passing tests
- **Review Management**: Systematic file review using ReviewMark patterns
