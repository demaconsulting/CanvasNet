## Drawing

![Drawing Structure](DrawingView.svg)

The `Drawing` subsystem is the fourth software subsystem in CanvasNet. It provides solid-color
vector rendering for `Geometry.Path` geometry through two public entry points:
direct path filling (`PathFiller`) and stroke-to-fill conversion (`PathStroker`). It consumes
both the `Canvas` subsystem's `Surface` unit and the `Geometry` subsystem's vector primitives.

### Purpose

The `Drawing` subsystem groups the software units responsible for turning vector geometry into
rendered pixels on a `Canvas.Surface`. It currently contains three sibling units:
`PathFiller`, which fills a closed `Geometry.Path` with a single solid color, or with a linear or
radial gradient paint, using an antialiased signed-area/coverage-accumulation scanline algorithm
supporting both nonzero and even-odd fill rules; `PathStroker`, which converts a path stroke
(configured by width, cap, join, miter limit, and optional dashing) into closed outline geometry
that `PathFiller` can render unchanged; and `GradientPaint`, the public
`Gradient`/`LinearGradient`/`RadialGradient`/`GradientStop`/`GradientSpread` types and internal
`GradientEvaluator` helper that `PathFiller`'s gradient overload evaluates per pixel. `Geometry`
is deliberately distinct from `Drawing`: `Geometry` describes shape geometry (paths, bounds,
curve math) with no notion of pixels, color, or rasterization; `Drawing` is where that geometry
is turned into pixels. `Drawing` depends on both `Canvas`
(`Surface`, `Surface.CompositeOverSpan`) and `Geometry` (`Path`, `PathBuilder`,
`BezierFlattening`, `SvgArcConverter`); neither `Canvas` nor `Geometry` has any dependency on
`Drawing`. Fonts/text rendering remain reserved for a later phase.

### Units

- **PathFiller** — a public static `Fill` entry point that flattens a `Path`'s subpaths into
  polygons and rasterizes them onto a `Surface` with a solid color or a `GradientPaint` gradient,
  together with the supporting
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

### Dependencies

The `Drawing` subsystem introduces zero new runtime NuGet dependencies. It is implemented
entirely against `System.Numerics.Vector2`/`Matrix3x2`, in-box `List<T>`/array types, and the
existing
`Canvas` and `Geometry` subsystem APIs — no custom point/vector wrapper type or new package
reference was introduced. Within the subsystem, `PathFiller.Fill` depends on the internal
`EdgeFlattener` (to convert a `Path`'s subpaths to closed polygons, itself depending on
`Geometry.BezierFlattening` and `Geometry.SvgArcConverter` to flatten curves and arcs), the
internal `ScanlineRasterizer` (to rasterize those polygons into per-row antialiased coverage and
composite each row via `Canvas.Surface.CompositeOverSpan`), and - for its gradient overload - the
public `Gradient` type and internal `GradientEvaluator` helper. `PathStroker.Stroke` depends on the
internal `StrokePathFlattener` (to flatten each subpath while preserving `Subpath.IsClosed`),
`DashSplitter` (to apply dash-array and dash-offset semantics), and `StrokeOutliner` (to offset
segments and resolve joins/caps into closed polygons), plus `Geometry.PathBuilder` to assemble the
returned outline path.

### Callers

`PathFiller.Fill` and `PathStroker.Stroke` are public API entry points, invoked externally by
consumers of the CanvasNet package. `PathFiller` is exercised end to end by this subsystem's own
unit tests and by a system-integration test that builds a `Path` via `PathBuilder` and fills it
onto a `Surface` (see `CanvasNetTests.cs`). `PathStroker` is exercised end to end by its own unit
tests, which stroke a path, fill the returned outline path, and inspect the resulting pixels. The
`Drawing` subsystem itself has no dependency on `Codecs`; it depends on `Canvas` and `Geometry`
as described above.
<!-- cspell:ignore Outliner -->
