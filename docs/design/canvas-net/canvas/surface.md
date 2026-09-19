## Surface

![Canvas Structure](CanvasView.svg)

The `Surface` class is the second software unit in CanvasNet. It provides a mutable, in-memory
32-bit RGBA pixel buffer with allocation-free row access, independent-copy cropping, and
vectorized bulk pixel operations, forming the core pixel-storage primitive on which future
drawing and codec functionality will build.

### Purpose

`Surface` stores pixel data for a rectangular image in a single contiguous byte array. It exposes
single-pixel get/set access, row-level `Span<T>` access (both as raw bytes and as strongly typed
`Rgba32` pixels), a `Crop` operation that produces an independent copy of a rectangular
sub-region, and vectorized bulk pixel operations for alpha premultiplication and Porter-Duff
"over" alpha compositing (`PremultiplyAlpha`, `UnpremultiplyAlpha`, `CompositeOver`). It performs
no I/O, and its only runtime dependency beyond the .NET base class library is
`System.Numerics.Tensors` (used internally by the bulk pixel operations; see
[Dependencies](#dependencies)).

### Data Model

| Field/Property  | Type        | Description                                                          |
| --------------- | ----------- | -------------------------------------------------------------------- |
| `Width`         | `int`       | The width of the surface, in pixels (read-only after construction).  |
| `Height`        | `int`       | The height of the surface, in pixels (read-only after construction). |
| `_buffer`       | `byte[]`    | Contiguous, row-major pixel storage, sized `Height * _strideBytes`.  |
| `_strideBytes`  | `int`       | The physical byte size of one row, including trailing padding.       |
| `BytesPerPixel` | `const int` | The number of bytes per pixel (always 4: R, G, B, A).                |

A supporting value type, `Rgba32`, represents a single pixel:

| Field | Type   | Description              |
| ----- | ------ | ------------------------ |
| `R`   | `byte` | The red channel value.   |
| `G`   | `byte` | The green channel value. |
| `B`   | `byte` | The blue channel value.  |
| `A`   | `byte` | The alpha channel value. |

`Rgba32` is a `[StructLayout(LayoutKind.Sequential)]` struct with exactly these four `byte`
fields and no other state, so that it is blittable and can be reinterpreted directly over a row
of the `Surface` byte buffer via `MemoryMarshal.Cast` with no copying or conversion. It provides
value equality (`Equals`, `GetHashCode`, `==`, `!=`) so pixel values can be compared directly in
application and test code. It is documented here, inline within the `Surface` unit, rather than as
its own software unit, because it has no independent behavior beyond being a 4-byte data carrier
consumed exclusively by `Surface`.

### Row Storage Layout

Internally, each row is physically padded up to a multiple of 16 pixels (64 bytes), rather than
being packed at exactly `Width * 4` bytes. `_strideBytes` is computed once in the constructor as
`ceil(Width / 16) * 16 * 4`, and `_buffer` is allocated as `Height * _strideBytes` bytes rather
than `Width * Height * 4` bytes.

**Architectural decision**: 16 pixels is the smallest common multiple of the vector widths
CanvasNet's target platforms are likely to use for elementwise per-channel pixel operations:
SSE2 (4 px / 16 B), AVX2 (8 px / 32 B), and AVX-512 (16 px / 64 B) all divide evenly into 16 px.
Padding every row's physical storage up to a whole multiple of 16 pixels means a full-row SIMD
loop over the padded stride processes only whole vector-width chunks, with zero scalar-remainder
handling required regardless of which vector width the runtime selects at JIT time. This benefits
the vectorized bulk pixel operations described below (`PremultiplyAlpha`, `UnpremultiplyAlpha`,
`CompositeOver`).

This padding is **purely an internal storage-layout detail with no observable effect**: `Width`
and `Height` are unaffected, and both public row accessors (`GetRowSpanBytes`, `GetRowSpan`)
continue to return spans of exactly `Width * 4` bytes / `Width` pixels — the trailing padding
bytes of each row are never included in, or reachable through, any public member. A private
helper, `GetPaddedRowSpanBytes(int y)`, returns the full `_strideBytes`-length row (including the
padding bytes) for internal use only by the vectorized bulk pixel operations, which need to
process the complete physical row in one pass; its padding-region contents are never read back
through any public accessor. `Crop` and all four codecs (`BmpCodec`, `PngCodec`, `TiffCodec`,
`JpegCodec`) are unaffected because they already exclusively use the public, `Width`-scoped
`GetRowSpanBytes` accessor rather than assuming a flat, gap-free `Width * Height * 4` buffer.

### Key Methods

#### Surface(int width, int height)

Constructs a surface of the given size. Validates `0 < width <= 16384` and `0 < height <= 16384`,
throwing `ArgumentOutOfRangeException(nameof(width))` or `ArgumentOutOfRangeException(nameof(height))`
respectively. Allocates a `byte[]` of `Height * _strideBytes` bytes, where `_strideBytes` rounds
`width` up to the next multiple of 16 pixels then converts to bytes (see
[Row Storage Layout](#row-storage-layout)). The 16384 upper bound guarantees that this padded-
stride/buffer-size arithmetic — `paddedWidthPixels <= 16384`, `_strideBytes <= 16384 * 4 = 65536`,
and `height * _strideBytes <= 16384 * 65536 = 1,073,741,824` — stays within plain `int` range with
margin to spare below `int.MaxValue` (2,147,483,647), so no `long`/`checked` arithmetic is needed.

**Architectural decision**: a freshly constructed surface is always fully transparent black (every
channel, including alpha, is zero). This is deliberate: a newly allocated `byte[]` is already
zero-filled by the runtime at no extra cost, and "fully transparent" is the least surprising
default for a compositing/rendering surface — untouched regions contribute nothing until
something is explicitly drawn into them.

**Throws:**

- `ArgumentOutOfRangeException` — when `width` is less than or equal to zero, or exceeds 16384
- `ArgumentOutOfRangeException` — when `height` is less than or equal to zero, or exceeds 16384

#### this[int x, int y]

Indexer providing single-pixel get/set access. Validates `y` (via `GetRowSpan`) and `x`, throwing
`ArgumentOutOfRangeException` naming the offending parameter.

**Performance note**: this indexer is slower than working directly with `GetRowSpan` or
`GetRowSpanBytes`, because each call re-validates `y` and re-slices the row. Callers processing
many pixels in the same row should obtain the row span once instead of repeatedly indexing.

**Throws:**

- `ArgumentOutOfRangeException` — when `x` is outside `[0, Width)`
- `ArgumentOutOfRangeException` — when `y` is outside `[0, Height)`

#### GetRowSpanBytes(int y)

Returns a `Span<byte>` of length `Width * 4` aliasing row `y`'s raw bytes directly over
`_buffer` — no data is copied, so writes through the span are immediately visible through the
indexer and vice versa. The returned length is always exactly `Width * 4`, regardless of
`_strideBytes`'s internal padding (see [Row Storage Layout](#row-storage-layout)).

**Throws:**

- `ArgumentOutOfRangeException` — when `y` is outside `[0, Height)`

#### GetRowSpan(int y)

Returns a `Span<Rgba32>` of length `Width` by reinterpreting the result of `GetRowSpanBytes` via
`MemoryMarshal.Cast<byte, Rgba32>`. This is a zero-copy reinterpretation, not a conversion: writes
through the returned span are immediately visible through the indexer and `GetRowSpanBytes`.

**Throws:**

- `ArgumentOutOfRangeException` — when `y` is outside `[0, Height)`

#### Crop(int x, int y, int width, int height)

Returns a new, independent `Surface` of size `width` by `height` containing a copy of the
requested sub-region. Validates each argument individually:

- `x < 0` → `ArgumentOutOfRangeException(nameof(x))`
- `y < 0` → `ArgumentOutOfRangeException(nameof(y))`
- `width <= 0` → `ArgumentOutOfRangeException(nameof(width))`
- `height <= 0` → `ArgumentOutOfRangeException(nameof(height))`
- `x + width > Width` → `ArgumentOutOfRangeException(nameof(width))`
- `y + height > Height` → `ArgumentOutOfRangeException(nameof(height))`

**Algorithm**: allocates the destination surface, then copies one row at a time using
`Span<byte>.CopyTo` — each source row is a contiguous run of `width * 4` bytes starting at column
`x`, so the whole row transfers in a single bulk operation rather than a per-pixel loop. This is
both simpler and faster than iterating pixel by pixel for contiguous row data.

**Throws:**

- `ArgumentOutOfRangeException` — for any of the six invalid conditions listed above

#### PremultiplyAlpha()

Converts this surface's pixel buffer, in place, from straight (unassociated) alpha to
premultiplied alpha: for every pixel, each color channel is replaced by
`round(channel * alpha / 255)` (round-half-away-from-zero, clamped to `[0, 255]`); the alpha
channel is unchanged. Every possible input byte pattern is valid, so this method never throws.

#### UnpremultiplyAlpha()

Converts this surface's pixel buffer, in place, from premultiplied alpha back to straight alpha —
the inverse of `PremultiplyAlpha`. For every pixel with non-zero alpha, each color channel is
replaced by `round(channel * 255 / alpha)` (round-half-away-from-zero, clamped to `[0, 255]` —
the clamp is load-bearing since premultiplication is lossy and this division can genuinely
overshoot 255). For a fully transparent pixel (`alpha == 0`), no color information is
recoverable; the defined result is `R = G = B = 0`, matching common raster-graphics convention.
Every possible input byte pattern is valid, so this method never throws.

#### CompositeOver(Surface foreground)

Composites `foreground` "over" this surface using standard Porter-Duff "over" alpha compositing,
writing the result back into this surface in place. Both surfaces are assumed to hold straight
(unassociated) alpha on input and the result is also straight alpha — callers do not need to call
`PremultiplyAlpha` first.

**Formula** (per channel, normalized to `[0, 1]` by dividing by 255):

```text
outA = fgA + bgA * (1 - fgA)
outC = 0                                              if outA == 0
     = (fgC * fgA + bgC * bgA * (1 - fgA)) / outA      otherwise
```

Results are converted back to bytes with round-half-away-from-zero, clamped to `[0, 255]`.

**Throws:**

- `ArgumentNullException` — when `foreground` is `null`
- `ArgumentException` — when `foreground.Width != Width` or `foreground.Height != Height`

#### CompositeOver(Rgba32 color)

Composites the constant `color` "over" every pixel of this surface, using the same formula and
rounding rule as `CompositeOver(Surface)`, with `color` acting as the foreground at every pixel.
Every possible `Rgba32` value is valid, so this method never throws.

**Implementation note (all three bulk pixel operations)**: each row is deinterleaved from RGBA
byte order into planar `R`/`G`/`B`/`A` arrays via a simple scalar loop (this transform is layout
work, not numeric work, so clarity is prioritized over vectorizing it), then the numerically
heavy work — byte→float widening, multiply, divide, rounding, float→byte narrowing — is performed
via `System.Numerics.Tensors.TensorPrimitives` calls (`ConvertSaturating`, `Multiply`, `Divide`,
`Add`, `Subtract`, `Round` with `MidpointRounding.AwayFromZero`) operating on the planar float
spans, before reinterleaving back to RGBA byte order. The one exception that cannot be expressed
as a branch-free vector formula — the `alpha == 0` degenerate cases in `UnpremultiplyAlpha` and
`CompositeOver` — is handled by a small scalar fix-up loop afterward, since `TensorPrimitives` has
no conditional-select primitive. Since `netstandard2.0` support has been dropped, there is a
single, unconditional code path shared by `net8.0`, `net9.0`, and `net10.0` — no `#if`
target-framework gating is required.

### Error Handling

All validation is performed at the start of the constructor, indexer, `GetRowSpanBytes`, `Crop`,
and `CompositeOver(Surface)`, using `ArgumentOutOfRangeException`/`ArgumentNullException`/
`ArgumentException` naming the specific invalid parameter. `Surface` performs no local error
handling or recovery — validation failures are detected at the point of entry and the resulting
exception propagates directly to the caller uncaught. There is no internal state to roll back
because invalid arguments are rejected before any field is read or written, and before the
destination surface is mutated in `Crop`.

### Dependencies

`Surface` has one NuGet package dependency, `System.Numerics.Tensors` (pinned to a version
compatible with `net8.0`), used both at compile time (for its `TensorPrimitives` API surface) and
at runtime by the vectorized bulk pixel operations (`PremultiplyAlpha`, `UnpremultiplyAlpha`,
`CompositeOver`); no other member of `Surface` depends on it. Every other member of `Surface` is
implemented exclusively against the .NET Base Class Library
(`System.Runtime.InteropServices.MemoryMarshal` and `System.Span<T>`), which is available natively
on all of CanvasNet's target frameworks.

### Callers

`Surface` is a public API entry point: it is invoked externally by consumers of the CanvasNet
package. It is also invoked internally by the in-house `BmpCodec` unit, which depends on
`Surface` (constructing surfaces and reading/writing rows via `Surface.GetRowSpanBytes`) — see
_BmpCodec Unit Design_ (`../codecs/bmp-codec.md`) for details of that dependency. `Surface` itself
has no dependency on `BmpCodec` or on any other unit.
