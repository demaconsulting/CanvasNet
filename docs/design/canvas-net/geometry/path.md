## Path / PathBuilder

![Geometry Structure](GeometryView.svg)

The `Path` and `PathBuilder` classes are a software unit in the `Geometry` subsystem. `Path` is
an immutable vector path: an ordered collection of independent `Subpath` contours, each an
ordered sequence of `PathCommand` drawing commands. `PathBuilder` is the mutable, fluent builder
that is the only way to construct a `Path`.

### Purpose

`Path` represents an arbitrary vector shape - straight lines, quadratic and cubic Bezier curves,
SVG-style elliptical arcs, and closed contours, potentially several disjoint subpaths at once -
as an immutable snapshot that can be safely shared or cached once built. `PathBuilder` records
drawing commands issued via a fluent (`this`-returning) API and produces `Path` snapshots via
`Build()`, while remaining reusable afterward (including via an explicit `Clear()`) so a single
builder instance can be reused across many paths without per-path allocation overhead in a hot
rendering loop.

### Data Model

| Type              | Description                                                                   |
| ----------------- | ----------------------------------------------------------------------------- |
| `PathCommandType` | Enum: `LineTo`, `QuadraticBezierTo`, `CubicBezierTo`, `ArcTo`, `Close`.       |
| `PathCommand`     | Readonly struct tagged union carrying `Type` plus the fields relevant to it.  |
| `Subpath`         | Readonly struct: `Start` (`Vector2`), `Commands` (list), `IsClosed` (`bool`). |
| `Path`            | Sealed class: `Subpaths` (`IReadOnlyList<Subpath>`); no public constructor.   |
| `PathBuilder`     | Sealed class: mutable, fluent; produces `Path` via `Build()`.                 |

**Architectural decision: `Subpaths`/`Commands` are wrapped in `ReadOnlyCollection<T>`, not just
typed as `IReadOnlyList<T>`.** `IReadOnlyList<T>` only hides mutating members from the
compile-time API; the concrete `List<T>` instance backing it still implements `IList<T>`, so a
caller could downcast `Path.Subpaths` or `Subpath.Commands` back to `IList<T>` and mutate a
supposedly-immutable `Path`/`Subpath` in place. Wrapping the backing list in
`System.Collections.ObjectModel.ReadOnlyCollection<T>` before exposing it closes that hole: its
own mutating members throw `NotSupportedException` regardless of how the exposed reference is
cast, so immutability is enforced at runtime, not merely documented at compile time.

**Architectural decision: `MoveTo` is not a `PathCommandType`.** A path always begins a subpath
with a "move", but representing it as an ordinary command (as SVG's own `M`/`m` path-data command
does) would allow a `Subpath` with no move at all, or with a move anywhere other than first -
states that should be unrepresentable. Instead, each `Subpath`'s move destination is captured
directly as its own `Start` field, and `PathCommandType` only enumerates the commands that can
legally follow a subpath's start. This makes "a subpath's first point" a structural guarantee
rather than a convention every consumer must separately validate.

`PathCommand` is a flat readonly struct (not a class hierarchy) with a private constructor and
internal static factory methods (`LineTo`, `QuadraticBezierTo`, `CubicBezierTo`, `ArcTo`,
`Close`) used only by `PathBuilder`, carrying every field any command kind might need
(`EndPoint`, `Control1`, `Control2`, `Radius`, `RotationDegrees`, `LargeArc`, `Sweep`); which
fields are meaningful is determined by `Type`. This keeps `Path`'s internal storage a simple,
allocation-friendly array of value-type structs rather than an array of polymorphic references.

**Architectural decision: `PathBuilder.ArcTo` stores raw SVG parameters only.** `ArcTo` never
pre-inspects or pre-converts its `radius`/`rotationDegrees`/`largeArc`/`sweep`/`end` parameters;
it stores them verbatim in a `PathCommand.ArcTo` value. Conversion to cubic Bezier segments is
performed lazily, only when a consumer actually needs it (for example, `Path.GetBounds`), via
`SvgArcConverter` - see _SvgArcConverter Unit Design_ (`svg-arc-converter.md`). A consumer that
never needs Bezier segments (for example, a hit-testing implementation with its own arc math)
never pays for the conversion.

### Key Methods

#### PathBuilder.MoveTo(Vector2 point)

Starts a new subpath at `point`. If a previous subpath is still open (no `Close` issued), it is
committed as-is (its `IsClosed` remains `false`) before the new subpath begins. Always succeeds.

#### PathBuilder.LineTo/QuadraticBezierTo/CubicBezierTo/ArcTo(...)

Appends the corresponding `PathCommand` to the current subpath.

**Throws:**

- `InvalidOperationException` - when called before the first `MoveTo`, or after `Close` without
  an intervening `MoveTo` (there is no current point to continue from in either case)

#### PathBuilder.Close()

Appends a `Close` command to the current subpath and marks it closed (`IsClosed = true` on the
resulting `Subpath`). A `Close` command carries no `EndPoint` of its own - a closed subpath
always returns to its own `Start`.

**Throws:**

- `InvalidOperationException` - under the same conditions as the drawing commands above

#### PathBuilder.Build()

Returns an immutable `Path` snapshot of every subpath recorded so far (including the
currently-open subpath, if any, uncommitted state included). The builder remains usable
afterward: further commands (and further `Build()` calls) do not affect a previously returned
`Path`. If no commands have ever been issued, returns a `Path` with zero `Subpaths`.

#### PathBuilder.Clear()

Resets the builder to the same empty state as a freshly constructed instance, so it can be reused
to build further, unrelated paths without allocating a new builder instance. The internal
current-commands buffer is reset via its own `Clear()` method rather than being replaced with a
new list, preserving its already-grown capacity across builds - so repeatedly building
similarly-sized paths in a loop does not repeatedly reallocate that buffer.

#### Path.Empty

A static singleton `Path` with zero `Subpaths`. `Path.Empty.GetBounds()` returns `Rect.Empty`,
matching `Rect`'s own union-identity convention.

#### Path.GetBounds(float flattenTolerance = 0)

Computes an axis-aligned bounding `Rect` for the whole path, as the `Rect.Union` of every
subpath's own contribution.

- **Conservative mode** (`flattenTolerance <= 0`, the default): for each command, folds in the
  raw endpoint and any control points directly (no flattening), relying on the convex-hull
  property of Bezier curves - every point on a quadratic or cubic Bezier curve lies within the
  convex hull of its control points and endpoints, so enclosing every control point and endpoint
  always encloses the whole curve, though possibly more loosely than the curve's true bounds.
  `ArcTo` commands are converted to cubic Bezier segments via `SvgArcConverter` first (arcs carry
  no control points of their own), then treated the same way.
- **Flattening mode** (`flattenTolerance > 0`): every curve (including each `ArcTo` command's
  converted Bezier segments) is first flattened to a polyline within the given tolerance via
  `BezierFlattening`, and the bounds fold in the flattened points instead - more expensive, but
  tighter, since a curve's flattened polyline hugs the true curve far more closely than its
  control-point convex hull does.

Never throws.

### Error Handling

`PathBuilder`'s drawing-command guard (`InvalidOperationException` before the first `MoveTo`, or
after `Close` without an intervening `MoveTo`) is the only validation this unit performs; every
other input (any finite `Vector2`, any `float` rotation, any `bool` flag combination) is accepted
without further checking. `Path` itself performs no validation - it can only ever be constructed,
internally, from a well-formed `PathBuilder` snapshot (or as the `Empty` singleton).

### Dependencies

`Path`/`PathBuilder` depend only on `System.Numerics.Vector2` (in-box BCL type, no new NuGet
package) and, within this subsystem, on `Rect` (the return type of `GetBounds`),
`BezierFlattening`, and `SvgArcConverter` (both used internally by `GetBounds`'s flattening mode
and by its `ArcTo` handling, respectively).

### Callers

`PathBuilder` and `Path` are public API entry points, invoked externally by consumers of the
CanvasNet package. `Path` has no dependency on any consumer; `Geometry` has no runnable
end-to-end example yet within this library, since no rasterizer (the reserved `Drawing`
subsystem) exists yet to consume a `Path`.
