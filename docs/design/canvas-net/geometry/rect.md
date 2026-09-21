## Rect

![Geometry Structure](GeometryView.svg)

The `Rect` struct is a software unit in the `Geometry` subsystem. It represents an
axis-aligned bounding rectangle using the position-plus-size convention (`X`, `Y`, `Width`,
`Height`, all `float`), reusing `System.Numerics.Vector2` for its point/size-valued members and
`System.Numerics.Matrix3x2` for its `Transform` operation.

### Purpose

`Rect` is the common bounding-box representation used throughout the `Geometry` subsystem - most
notably as the return type of `Path.GetBounds`. It provides computed edge/corner properties,
containment testing, union, intersection, matrix transformation, and value equality, with a
carefully chosen `Empty` sentinel that makes folding `Union` over zero or more contributing
rectangles always produce the mathematically correct result.

### Data Model

| Field/Property       | Type                   | Description                                                |
| -------------------- | ---------------------- | ---------------------------------------------------------- |
| `X`                  | `float`                | The x-coordinate of the top-left corner.                   |
| `Y`                  | `float`                | The y-coordinate of the top-left corner.                   |
| `Width`              | `float`                | The width. May be negative only for the `Empty` sentinel.  |
| `Height`             | `float`                | The height. May be negative only for the `Empty` sentinel. |
| `Left`/`Top`         | `float`                | Computed; equal to `X`/`Y`.                                |
| `Right`/`Bottom`     | `float`                | Computed from `Width`/`Height` (see NaN-guard note below). |
| `Location`/`TopLeft` | `Vector2`              | Computed; `(X, Y)`.                                        |
| `Size`               | `Vector2`              | Computed; `(Width, Height)`.                               |
| `BottomRight`        | `Vector2`              | Computed; `(Right, Bottom)`.                               |
| `Empty`              | `static readonly Rect` | The union-identity sentinel (see below).                   |

### The `Empty` Sentinel

**Architectural decision**: `Empty` is represented as `X = Y = float.PositiveInfinity`,
`Width = Height = float.NegativeInfinity` - a WPF-style "union identity" sentinel - rather than
the more common all-zero "empty" convention (as used by, for example,
`System.Drawing.RectangleF.Empty`). This is deliberate and load-bearing: it makes
`Union(Empty, r)` an identity operation (`Union(Empty, r) == r` for every rectangle `r`), which
lets `Path.GetBounds` fold `Union` over zero or more subpaths and always get the mathematically
correct answer (an empty path's bounds are `Empty`, and a one-subpath path's bounds are exactly
that subpath's bounds) with no special-casing for "no subpaths yet". An all-zero "empty"
rectangle would instead silently corrupt every union with a real rectangle by contributing the
origin point.

`IsEmpty` only tests `Width < 0` (not `Height`), because `Empty`'s construction always keeps both
negative together; this single-field test matches the WPF convention this type follows.

**Bug found and fixed during implementation**: a naive `Right => X + Width` (and equivalently for
`Bottom`) computes `float.PositiveInfinity + float.NegativeInfinity`, which IEEE 754 defines as
`NaN` - not the intended `float.NegativeInfinity`. This would silently corrupt every `Union`
involving `Empty` (propagating `NaN` through the subsequent `Math.Min`/`Math.Max` calls in
`Union`). `Right` and `Bottom` therefore special-case `float.IsNegativeInfinity(Width)` /
`float.IsNegativeInfinity(Height)` and return `float.NegativeInfinity` directly in that case,
matching the "inverted extent" behavior the rest of this type's design relies on.

### Key Methods

#### Contains(Vector2 point)

Tests whether `point` lies within the rectangle using a half-open interval on both axes: the
left/top edges are included, the right/bottom edges are excluded
(`point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom`). `Empty`'s inverted
extent (`Left` greater than `Right`) makes every comparison fail for any finite point, so `Empty`
correctly contains no points. Never throws.

#### Union(Rect) / Union(Rect, Rect)

Returns the smallest rectangle enclosing both input rectangles, computed as
`(min(Left), min(Top))` to `(max(Right), max(Bottom))`. Available as both an instance method
(`this.Union(other)`) and a static method (`Rect.Union(a, b)`). Never throws.

#### Intersect(Rect) / Intersect(Rect, Rect)

Returns the overlapping region of both input rectangles, computed as `(max(Left), max(Top))` to
`(min(Right), min(Bottom))`. If the rectangles are disjoint on either axis (`right < left` or
`bottom < top`), returns the canonical `Empty` sentinel rather than a rectangle with a
non-canonical negative size. Available as both an instance method and a static method. Never
throws.

#### Transform(Matrix3x2 matrix)

Returns the smallest axis-aligned rectangle enclosing this rectangle after applying `matrix`.

**Architectural decision**: all four corners (`TopLeft`, `(Right, Top)`, `(Left, Bottom)`,
`BottomRight`) are transformed individually via `Vector2.Transform`, then re-enclosed via the same
min/max folding as `Union`, rather than transforming only two opposite corners. Transforming just
the top-left and bottom-right corners is mathematically incorrect for any matrix containing
rotation or shear: the image of an axis-aligned rectangle under such a transform is a
parallelogram, not another axis-aligned rectangle, so its true bounding box can only be recovered
by considering all four transformed corners. Never throws.

**Bug found and fixed**: `Empty`'s corners include `+Infinity`/`-Infinity`. Transforming them
directly can produce `NaN` (for example `0 * Infinity`, when a matrix has a zero off-diagonal
element), which would then poison the min/max re-enclosure fold and yield a `NaN`-filled rectangle
instead of `Empty`. `Transform` therefore checks `IsEmpty` first and returns `Empty` immediately,
preserving it as an identity/no-op - the same guard philosophy already applied to `Right`/`Bottom`
and `Intersect`.

#### Equals(Rect) / Equals(object?) / GetHashCode / == / !=

Value equality compares the raw bit pattern of each of the four fields (`X`, `Y`, `Width`,
`Height`) via `BitConverter.SingleToInt32Bits`, rather than `==`/tolerance-based comparison. This
is exact snapshot equality of stored field values (matching the precedent set by
`DemaConsulting.CanvasNet.Canvas.Rgba32`'s exact byte equality) - not a comparison of
independently computed floating-point results, so no rounding tolerance is appropriate. Comparing
raw bits (rather than `==`) also avoids a SonarAnalyzer S1244 diagnostic (float exact-equality) at
the call site, since the intent here is documented exact snapshot equality rather than a
numerically fragile result comparison.

#### ToString()

Returns a human-readable string in the form `"{X=.., Y=.., Width=.., Height=..}"`, for diagnostic
output.

### Error Handling

`Rect` never throws from any public member; every input is either arithmetically well-defined (a
`float` value, however large, small, `NaN`, or infinite) or produces `Empty` for a
would-be-invalid result (a disjoint `Intersect`), rather than raising an exception.

### Dependencies

`Rect` depends only on `System.Numerics.Vector2` and `System.Numerics.Matrix3x2` (in-box BCL
types, no new NuGet package) and `System.BitConverter`/`System.HashCode` from the .NET Base Class
Library.

### Callers

`Rect` is a public API entry point, invoked externally by consumers of the CanvasNet package. It
is also invoked internally by `Path.GetBounds`, which returns a `Rect` computed by folding
`Union` over every subpath's own bounds (and, for `Rect.Empty`'s benefit, over zero subpaths for
an empty path) - see _Path Unit Design_ (`path.md`). `Rect` has no dependency on `Path`,
`BezierFlattening`, or `SvgArcConverter`.
