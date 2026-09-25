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
`System.Numerics.Tensors` (used internally by the bulk pixel operations; see the Dependencies
section below).

### Data Model

| Field/Property  | Type               | Description                                                          |
| --------------- | ------------------ | -------------------------------------------------------------------- |
| `Width`         | `int`              | The width of the surface, in pixels (read-only after construction).  |
| `Height`        | `int`              | The height of the surface, in pixels (read-only after construction). |
| `MaxDimension`  | `public const int` | The maximum permitted `width`/`height` value (8192; see below).      |
| `_buffer`       | `byte[]`           | Contiguous, row-major pixel storage, sized `Height * _strideBytes`.  |
| `_strideBytes`  | `int`              | The physical byte size of one row, including trailing padding.       |
| `BytesPerPixel` | `const int`        | The number of bytes per pixel (always 4: R, G, B, A).                |
| `_disposed`     | `bool`             | Set once by `Dispose()`; buffer-touching members throw `ObjectDisposedException` if set.       |

`MaxDimension` is `public` (not merely internal) so that callers can compare a probed image's
declared dimensions — for example, a codec's `GetInfo(Stream)`/`GetInfo(string)` result — against
the same bound `Surface`'s constructor enforces, before ever constructing a `Surface` or calling
a codec's `Load` method.

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

Constructs a surface of the given size. Validates `0 < width <= MaxDimension` and
`0 < height <= MaxDimension` (`MaxDimension` is a public constant equal to 8192 — see the Data
Model table above), throwing `ArgumentOutOfRangeException(nameof(width))` or
`ArgumentOutOfRangeException(nameof(height))` respectively. Allocates a `byte[]` of
`Height * _strideBytes` bytes, where `_strideBytes` rounds `width` up to the next multiple of 16
pixels then converts to bytes (see the Row Storage Layout section below). The `MaxDimension`
upper bound guarantees that this padded-
stride/buffer-size arithmetic — `paddedWidthPixels <= 8192`, `_strideBytes <= 8192 * 4 = 32768`,
and `height * _strideBytes <= 8192 * 32768 = 268,435,456` — stays within plain `int` range with
margin to spare below `int.MaxValue` (2,147,483,647), so no `long`/`checked` arithmetic is needed.

**Architectural decision**: `MaxDimension` is `public` (rather than `internal`, as in an earlier
version of this codec suite) specifically so that each codec's `GetInfo(Stream)`/`GetInfo(string)`
method — which reports a candidate image's declared dimensions without decoding its pixel data —
lets callers perform exactly this `width <= Surface.MaxDimension && height <= Surface.MaxDimension`
comparison themselves before ever calling `Load` and allocating a `Surface`, instead of hard-coding
or guessing the bound. See _Codecs Subsystem Design_ (`../codecs.md`) for the `GetInfo`/`ImageInfo`
pattern that motivated this change.

**Architectural decision**: a freshly constructed surface is always fully transparent black (every
channel, including alpha, is zero). This is deliberate: a newly allocated `byte[]` is already
zero-filled by the runtime at no extra cost, and "fully transparent" is the least surprising
default for a compositing/rendering surface — untouched regions contribute nothing until
something is explicitly drawn into them.

**Throws:**

- `ArgumentOutOfRangeException` — when `width` is less than or equal to zero, or exceeds
  `MaxDimension` (8192)
- `ArgumentOutOfRangeException` — when `height` is less than or equal to zero, or exceeds
  `MaxDimension` (8192)

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
`_strideBytes`'s internal padding (see the Row Storage Layout section below).

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

#### Clear(Rgba32 color)

Overwrites every pixel of this surface with the constant `color`, replacing rather than
blending against existing pixel data. Every possible `Rgba32` value is valid, so this method
never throws.

**Architectural note (overwrite vs. blend)**: `Clear` is distinct from `CompositeOver(Rgba32)`
in kind, not merely in speed — `CompositeOver(Rgba32)` runs `color` through the Porter-Duff
"over" formula against the surface's existing pixels, so a semi-transparent `color` blends with
whatever was already there, and even a fully-opaque `color` requires evaluating the full blend
formula per pixel. `Clear` performs no blend math at all: the exact same `Rgba32` byte pattern is
written into every pixel regardless of what was previously stored there or of `color.A`. This
matters for a genuine "establish a known background" use case (for example clearing a surface to
transparent black before drawing), where the existing pixel data is irrelevant garbage (freshly
rented/reused memory, or leftovers from a previous frame) that must not influence the result —
blending against it via `CompositeOver(Rgba32)` would be both wrong (a non-opaque `color` would
partially preserve that garbage) and needlessly expensive (full blend math for what is
conceptually a pure overwrite).

**Algorithm** (broadcast-fill, distinct from `CompositeOver`'s per-row blend pipeline): rents a
single `RowChannelBuffers` (the same planar-channel scratch buffer type the vectorized
compositing operations use) sized for one row, fills each of its four `byte[]` planar channel
arrays with `color`'s corresponding component via `Array.Fill`, reinterleaves that single row of
constant planar data into one `byte[_strideBytes]` pattern buffer via the existing
`ReinterleaveRow` helper, then copies that one pattern buffer into every row's padded byte span
(`GetPaddedRowSpanBytes`) via `Span<byte>.CopyTo`. Only one row's worth of channel buffers and
one pattern buffer are ever allocated/rented regardless of surface height — every subsequent row
reuses the same already-built pattern via a fixed-size `CopyTo`, rather than repeating the
fill/reinterleave work per row.

#### CompositeOverSpan(int y, int x, ReadOnlySpan\<float\> coverage, Rgba32 color)

Composites the constant `color` "over" a horizontal run of `coverage.Length` pixels within row
`y`, starting at column `x`, using the same Porter-Duff "over" formula and rounding rule as
`CompositeOver(Rgba32)`, except that `color`'s effective alpha at pixel `i` is additionally
scaled by `coverage[i]` before compositing: a `coverage[i] <= 0` leaves that pixel unchanged, a
`coverage[i] >= 1` is equivalent to `CompositeOver(Rgba32)` at that pixel, and any value in
between blends proportionally. Values are clamped to `[0, 1]` implicitly (a `coverage[i]` outside
that range behaves as if clamped). A no-op if `coverage` is empty.

**Architectural note (first partial-row compositing primitive)**: every other `CompositeOver`
overload always processes either a full row (`CompositeOver(Surface)`) or the whole surface
(`CompositeOver(Rgba32)`). `CompositeOverSpan` is the first member of `Surface` that composites an
arbitrary sub-range of a single row, driven by a per-pixel coverage value rather than a uniform
alpha — this is exactly what an antialiased rasterizer (the `Drawing` subsystem's `PathFiller`
unit; see `../drawing/path-filler.md`) needs: it can composite each rasterized row's fractional
pixel coverage directly, without ever allocating a full-row or full-surface buffer merely to hold
mostly-untouched pixels.

**Algorithm**: builds a small, `coverage.Length`-sized foreground channel buffer with constant
`R`/`G`/`B` equal to `color.R`/`color.G`/`color.B` and per-pixel `A` equal to
`round(color.A * clamp(coverage[i], 0, 1))` (round-half-away-from-zero, clamped to `[0, 255]`),
then reuses the exact same private per-row compositing pipeline as `CompositeOver(Rgba32)`
(deinterleave, widen to float, blend via `TensorPrimitives`, narrow/round/clamp, zero color where
alpha is zero, reinterleave) applied to the `[x, x + coverage.Length)` sub-range of row `y`'s
byte span — no blend-math is duplicated between the two overloads.

After the shared blend pipeline runs, every column whose `coverage[i]` was zero or negative has
its blended bytes discarded and replaced with the original, untouched background bytes read from
the surface before compositing began. This restoration step exists because a zero/negative
coverage column still flows through the shared pipeline (with its foreground alpha forced to
zero by the rounding above), and that pipeline's `alpha == 0` degenerate-case handling
unconditionally zeroes a pixel's color channels whenever its resulting alpha byte is zero. A
fully transparent background pixel that legitimately holds nonzero RGB (for example, a
premultiplied-adjacent transparent fringe pixel) would otherwise be corrupted to `(0, 0, 0, 0)`
by that shared zero-alpha handling, even though the documented contract requires a zero/negative
coverage column to be left completely untouched. Restoring the original bytes for exactly those
columns preserves the documented "leave unchanged" contract without special-casing the shared
blend pipeline itself.

**Throws:**

- `ArgumentOutOfRangeException` — when `y` is outside `[0, Height)`
- `ArgumentOutOfRangeException` — when `x` is negative, or `x + coverage.Length` exceeds `Width`

#### CompositeOverSpan(int y, int x, ReadOnlySpan\<float\> coverage, ReadOnlySpan\<Rgba32\> colors)

Composites an independent foreground color, supplied per pixel via `colors`, "over" a horizontal
run of `coverage.Length` pixels within row `y`, starting at column `x`, using the same Porter-Duff
"over" formula, rounding rule, and coverage-scaling semantics as the constant-color
`CompositeOverSpan(int, int, ReadOnlySpan<float>, Rgba32)` overload above - the only difference is
that pixel `i`'s foreground color comes from `colors[i]` rather than a single shared `color`
value. Producing byte-for-byte identical output to the constant-color overload whenever every
entry of `colors` happens to be equal is a direct consequence of both overloads sharing the same
private `CompositeOverSpanCore`/`CompositeOverSpanCoreShared` blend body (see the shared-blend
refactor note below) - only the foreground buffer's _population_ differs between the two
overloads, never the blend math itself.

**Architectural motivation**: this overload exists for per-pixel-varying-color callers - most
notably the `Drawing` subsystem's gradient-aware `ScanlineRasterizer.Fill` overload (see
`../drawing/gradient-paint.md`) - which need a different color at every pixel of a rasterized
row (one gradient-evaluated color per pixel), not a single shared color, while still reusing
every bit of the existing antialiased-coverage compositing pipeline.

**Throws:**

- `ArgumentOutOfRangeException` — when `y` is outside `[0, Height)`
- `ArgumentOutOfRangeException` — when `x` is negative, or `x + coverage.Length` exceeds `Width`
- `ArgumentException` — when `colors.Length` does not equal `coverage.Length`

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

#### Dispose()

Releases the resources held by this `Surface` and implements `IDisposable`. This method is
idempotent: a second (or subsequent) call has no additional effect, matching the existing
`RowChannelBuffers`/`CompositeWorkBuffers` internal helper types elsewhere in this same file,
which follow the same "flag check, set, return" idempotent-disposal shape (though those types
also have rented `ArrayPool<T>` arrays to actually return, whereas `Surface` currently does not).

In this release, `_buffer` is a plain managed `byte[]`, not rented from an `ArrayPool<T>`, so
there is nothing for `Dispose()` to actually release yet — it exists purely to establish the
disposal contract _before_ the pixel-storage strategy changes, so that a future release can back
`_buffer` with a pooled array (returning it to the pool inside `Dispose()`) without another
breaking API change. Because the only backing storage today is managed memory the garbage
collector already reclaims safely on its own, `Surface` deliberately declares no finalizer:
forgetting to call `Dispose()` only forgoes a (currently nonexistent) prompt release — it can
never leak an unmanaged or pooled resource.

After `Dispose()` has been called, every other public member that touches the pixel buffer
(the indexer, `GetRowSpanBytes`, `GetRowSpan`, `Crop`, `PremultiplyAlpha`, `UnpremultiplyAlpha`,
both `CompositeOver` overloads, and both public `CompositeOverSpan` overloads) throws
`ObjectDisposedException` via an `ObjectDisposedException.ThrowIf(_disposed, this)` guard as the
first statement in the member.

### Error Handling

All validation is performed at the start of the constructor, indexer, `GetRowSpanBytes`, `Crop`,
`CompositeOver(Surface)`, and `CompositeOverSpan`, using `ArgumentOutOfRangeException`/
`ArgumentNullException`/`ArgumentException` naming the specific invalid parameter. `Surface`
performs no local error handling or recovery — validation failures are detected at the point of
entry and the resulting exception propagates directly to the caller uncaught. There is no
internal state to roll back because invalid arguments are rejected before any field is read or
written, and before the destination surface is mutated in `Crop`.

Additionally, once `Dispose()` has been called, every public member that touches the pixel
buffer — the indexer (get and set), `GetRowSpanBytes`, `GetRowSpan`, `Crop`, `PremultiplyAlpha`,
`UnpremultiplyAlpha`, `CompositeOver(Surface)`, `CompositeOver(Rgba32)`, and both public
`CompositeOverSpan` overloads — throws `ObjectDisposedException` via a guard at entry, before any
of the member's own argument validation runs.

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
package. It is also invoked internally by all four in-house codec units (`BmpCodec`, `PngCodec`,
`TiffCodec`, `JpegCodec`), each of which depends on `Surface` (constructing surfaces and
reading/writing rows via `Surface.GetRowSpanBytes`, and comparing declared image dimensions
against the now-public `MaxDimension` constant in their own `Load` methods before ever
constructing a `Surface`) — see each codec's own unit design document under `../codecs/` for
details of that dependency. Only each codec's `Load` method enforces `MaxDimension`; `GetInfo`
deliberately does not — it reports the raw header-declared dimensions even when they exceed the
bound (see _Codecs Subsystem Design_, `../codecs.md`, and `ImageInfo`'s own documentation for why).
Callers of `GetInfo` who want to reject an oversized file before ever calling `Load` must perform
their own `width <= Surface.MaxDimension && height <= Surface.MaxDimension` comparison against the
now-public `Surface.MaxDimension`. `Surface` is also invoked internally by the `Drawing`
subsystem's `PathFiller` unit, which calls `CompositeOverSpan` to composite each rasterized row's
antialiased coverage directly — see _PathFiller Unit Design_ (`../drawing/path-filler.md`) for
details of that dependency, and by the `Drawing` subsystem's `GradientPaint` unit's
`ScanlineRasterizer.Fill` gradient overload, which calls the per-pixel-color
`CompositeOverSpan(int, int, ReadOnlySpan<float>, ReadOnlySpan<Rgba32>)` overload to composite one
gradient-evaluated color per pixel — see _GradientPaint Unit Design_
(`../drawing/gradient-paint.md`) for details of that dependency. `Surface` itself has no
dependency on any codec, on `Drawing`, or on any other unit.

### Internal workspace-reuse overload (allocation-reduction refactor)

`ScanlineRasterizer.Fill` calls `CompositeOverSpan` once per rasterized row, and each call would
otherwise rent (and, on return, clear and release) three fresh `RowChannelBuffers`/
`CompositeWorkBuffers`-style scratch buffer sets from `ArrayPool<T>.Shared` - per-row churn that
is unnecessary because every row of a single `ScanlineRasterizer.Fill` call composites a span of
the same fixed width. To eliminate this, `Surface` exposes an `internal` nested
`CompositeSpanWorkspace` type bundling one reusable set of these scratch buffers, sized once to a
caller-chosen capacity, plus an `internal` `CompositeOverSpan(int, int, ReadOnlySpan<float>,
Rgba32, CompositeSpanWorkspace)` overload that reuses the supplied workspace's buffers instead of
renting its own. `ScanlineRasterizer.Fill` constructs one `CompositeSpanWorkspace` sized to its
row width before its row loop, reuses it for every row's `CompositeOverSpan` call, and disposes it
once after the loop - so the whole fill rents each scratch buffer exactly once, not once per row.
Both overloads share the same private blend body (`CompositeOverSpanCore`); the only difference
between them is where their `RowChannelBuffers`/`CompositeWorkBuffers` come from, so the blend
math itself is never duplicated. This overload is `internal`, not `public`: it exposes an
allocation-strategy implementation detail, not new externally observable behavior - for identical
inputs it produces byte-for-byte identical output to the public overload.

The same pattern is repeated for the per-pixel-color overload: an `internal`
`CompositeOverSpan(int, int, ReadOnlySpan<float>, ReadOnlySpan<Rgba32>, CompositeSpanWorkspace)`
overload lets the `GradientPaint` unit's gradient-aware `ScanlineRasterizer.Fill` reuse one
workspace across every rasterized row of a fill, exactly as the constant-color path does. Both the
constant-color and per-pixel-color code paths - public and workspace-reusing alike - route through
one shared `CompositeOverSpanCore`/`CompositeOverSpanCoreShared` private blend body; only the
foreground-color population step (a single broadcast value versus one read per pixel) differs
between the constant-color and per-pixel-color forms, and only the scratch-buffer source (rented
versus supplied workspace) differs between the public and internal forms. A dedicated unit test
(`Surface_CompositeOverSpan_PerPixelColors_MatchesConstantColorOverload_WhenAllColorsEqual`)
proves the pre-existing constant-color overload's output is unchanged by this refactor.
