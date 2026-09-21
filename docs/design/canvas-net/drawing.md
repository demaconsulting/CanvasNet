## Drawing

![Drawing Structure](DrawingView.svg)

The `Drawing` subsystem is the fourth software subsystem in CanvasNet. It provides an
antialiased scanline-coverage fill rasterizer for closed `Geometry.Path` geometry with
solid-color paint (`PathFiller`), consuming both the `Canvas` subsystem's `Surface` unit and the
`Geometry` subsystem's vector primitives.

### Purpose

The `Drawing` subsystem groups the software units responsible for turning vector geometry into
rendered pixels on a `Canvas.Surface`. It has exactly one unit this phase, `PathFiller`, which
fills a closed `Geometry.Path` with a single solid color using an antialiased
signed-area/coverage-accumulation scanline algorithm supporting both nonzero and even-odd fill
rules. `Geometry` is deliberately distinct from `Drawing`: `Geometry` describes shape geometry
(paths, bounds, curve math) with no notion of pixels, color, or rasterization; `Drawing` is where
that geometry is turned into pixels. `Drawing` depends on both `Canvas` (`Surface`,
`Surface.CompositeOverSpan`) and `Geometry` (`Path`, `BezierFlattening`, `SvgArcConverter`);
neither `Canvas` nor `Geometry` has any dependency on `Drawing`. Strokes (outlining a path rather
than filling it), gradients (multi-color paint), and fonts/text rendering are all reserved for
later phases and are not implemented by this subsystem yet — there is deliberate room for future
sibling units (for example a future `PathStroker` unit) alongside `PathFiller` within this same
subsystem.

### Units

- **PathFiller** — a public static `Fill` entry point that flattens a `Path`'s subpaths into
  polygons and rasterizes them onto a `Surface` with a solid color, together with the supporting
  `FillRule` enum (nonzero/even-odd fill-rule selection) and the internal
  `EdgeFlattener`/`ScanlineRasterizer` helpers, all documented inline within the `PathFiller`
  unit design rather than as their own units, because none of them has any independent behavior
  beyond supporting `PathFiller.Fill`; see _PathFiller Unit Design_ (`drawing/path-filler.md`)

### Dependencies

The `Drawing` subsystem introduces zero new runtime NuGet dependencies. It is implemented
entirely against `System.Numerics.Vector2`, in-box `List<T>`/array types, and the existing
`Canvas` and `Geometry` subsystem APIs — no custom point/vector wrapper type or new package
reference was introduced. Within the subsystem, `PathFiller.Fill` depends on the internal
`EdgeFlattener` (to convert a `Path`'s subpaths to closed polygons, itself depending on
`Geometry.BezierFlattening` and `Geometry.SvgArcConverter` to flatten curves and arcs) and the
internal `ScanlineRasterizer` (to rasterize those polygons into per-row antialiased coverage and
composite each row via `Canvas.Surface.CompositeOverSpan`).

### Callers

`PathFiller.Fill` is a public API entry point, invoked externally by consumers of the CanvasNet
package. It is exercised end to end by this subsystem's own unit tests, and by a
system-integration test that builds a `Path` via `PathBuilder` and fills it onto a `Surface` (see
`CanvasNetTests.cs`). The `Drawing` subsystem itself has no dependency on `Codecs`; it depends on
`Canvas` and `Geometry` as described above.
