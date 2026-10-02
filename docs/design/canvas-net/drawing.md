## Drawing

![Drawing Structure](DrawingView.svg)

The `Drawing` subsystem is the fourth software subsystem in CanvasNet. It provides solid-color,
gradient, and tile-pattern vector rendering for `Geometry.Path` geometry through three public
entry points: direct path filling (`PathFiller`) and stroke-to-fill conversion (`PathStroker`),
backed by two paint-description sibling units (`GradientPaint` and `TilePaint`). It consumes
both the `Canvas` subsystem's `Surface` unit and the `Geometry` subsystem's vector primitives.

### Purpose

The `Drawing` subsystem groups the software units responsible for turning vector geometry into
rendered pixels on a `Canvas.Surface`. It currently contains four sibling units:
`PathFiller`, which fills a closed `Geometry.Path` with a single solid color, a linear or
radial gradient paint, or a repeating tile paint, using an antialiased signed-area/coverage-
accumulation scanline algorithm supporting both nonzero and even-odd fill rules; `PathStroker`,
which converts a path stroke (configured by width, cap, join, miter limit, and optional dashing)
into closed outline geometry that `PathFiller` can render unchanged; `GradientPaint`, the public
`Gradient`/`LinearGradient`/`RadialGradient`/`GradientStop`/`GradientSpread` types and internal
`GradientEvaluator` helper that `PathFiller`'s gradient overload evaluates per pixel; and
`TilePaint`, a public type pairing a pre-rendered one-cell tile `Surface` with a pattern-space
transform and `XStep`/`YStep` pitch, together with the internal `TilePaintEvaluator` helper that
`PathFiller`'s tile overload samples per pixel (wrapping around the tile's pitch), analogous to
how `GradientEvaluator` backs the gradient overload. `Geometry`
is deliberately distinct from `Drawing`: `Geometry` describes shape geometry (paths, bounds,
curve math) with no notion of pixels, color, or rasterization; `Drawing` is where that geometry
is turned into pixels. `Drawing` depends on both `Canvas`
(`Surface`, `Surface.CompositeOverSpan`) and `Geometry` (`Path`, `PathBuilder`,
`BezierFlattening`, `SvgArcConverter`); neither `Canvas` nor `Geometry` has any dependency on
`Drawing`. Callers may combine `Drawing` with the `Fonts` subsystem's `TrueTypeFont` output when
they want text rendering, but `Drawing` itself remains a general-purpose path rasterizer with no
font-specific logic.

### Units

- **PathFiller** — a public static `Fill` entry point that flattens a `Path`'s subpaths into
  polygons and rasterizes them onto a `Surface` with a solid color, a `GradientPaint` gradient, or
  a `TilePaint` tile, together with the supporting
  `FillRule` enum (nonzero/even-odd fill-rule selection) and the internal
  `EdgeFlattener`/`ScanlineRasterizer` helpers, all documented inline within the `PathFiller`
  unit design rather than as their own units, because none of them has any independent behavior
  beyond supporting `PathFiller.Fill`; see _PathFiller Unit Design_ (`drawing/path-filler.md`)
- **PathStroker** — a public static `Stroke` entry point that converts a `Path` centerline plus a
  `StrokeStyle` into one or more closed outline polygons expressed as a new `Path`, together with
  the supporting public `LineCap`, `LineJoin`, and `StrokeStyle` types and the internal
  `StrokePathFlattener`/`DashSplitter`/`StrokeOutliner` helpers, all documented inline within the
  `PathStroker` unit design; see _PathStroker Unit Design_ (`drawing/path-stroker.md`)
- **GradientPaint** — the public `Gradient` abstract base type and its `LinearGradient`/
  `RadialGradient` subtypes, the supporting `GradientStop`/`GradientSpread` types, and the
  internal `GradientEvaluator` helper that resolves a gradient's color at a point or across a
  pixel row, consumed by `PathFiller`'s gradient overload; see _GradientPaint Unit Design_
  (`drawing/gradient-paint.md`)
- **TilePaint** — a public sealed type pairing a pre-rendered one-cell tile `Surface`, a
  `Transform` mapping the tile's own pattern-space coordinates into the caller's path coordinate
  space, and a non-zero, finite `XStep`/`YStep` pattern-space pitch (either of which may be
  negative, per PDF 32000-1 §8.7.3.1's tiling-pattern semantics), together with the internal
  `TilePaintEvaluator` helper that resolves a wrapped-around tile sample at a point or across a
  pixel row, consumed by `PathFiller`'s tile overload; `TilePaint` does not own the tile
  `Surface`'s lifetime - the caller that constructed it remains responsible for disposal; see
  _TilePaint Unit Design_ (`drawing/tile-paint.md`)

### Dependencies

The `Drawing` subsystem introduces zero new runtime NuGet dependencies. It is implemented
entirely against `System.Numerics.Vector2`/`Matrix3x2`, in-box `List<T>`/array types, and the
existing
`Canvas` and `Geometry` subsystem APIs — no custom point/vector wrapper type or new package
reference was introduced. Within the subsystem, `PathFiller.Fill` depends on the internal
`EdgeFlattener` (to convert a `Path`'s subpaths to closed polygons, itself depending on
`Geometry.BezierFlattening` and `Geometry.SvgArcConverter` to flatten curves and arcs), the
internal `ScanlineRasterizer` (to rasterize those polygons into per-row antialiased coverage and
composite each row via `Canvas.Surface.CompositeOverSpan`), and - for its gradient/tile overloads

- the public `Gradient`/`TilePaint` types and their internal `GradientEvaluator`/
`TilePaintEvaluator` helpers. `PathStroker.Stroke` depends on the
internal `StrokePathFlattener` (to flatten each subpath while preserving `Subpath.IsClosed`),
`DashSplitter` (to apply dash-array and dash-offset semantics), and `StrokeOutliner` (to offset
segments and resolve joins/caps into closed polygons), plus `Geometry.PathBuilder` to assemble the
returned outline path. `TilePaint` itself depends only on `Canvas.Surface` (the tile bitmap it
wraps) and `System.Numerics.Matrix3x2`; it has no dependency on `PathFiller`, `PathStroker`, or
`GradientPaint`.

### Callers

`PathFiller.Fill`, `PathStroker.Stroke`, and `TilePaint`'s constructor are public API entry
points, invoked externally by consumers of the CanvasNet package. `PathFiller` is exercised end to
end by this subsystem's own unit tests and by a system-integration test that builds a `Path` via
`PathBuilder` and fills it onto a `Surface` (see `CanvasNetTests.cs`). `PathStroker` is exercised
end to end by its own unit tests, which stroke a path, fill the returned outline path, and inspect
the resulting pixels. `TilePaint` is constructed and consumed by `PathFiller`'s tile overload, and
is exercised end to end by `PathFiller`'s own unit tests. The
`Drawing` subsystem itself has no dependency on `Codecs`; it depends on `Canvas` and `Geometry`
as described above.
<!-- cspell:ignore Outliner -->
