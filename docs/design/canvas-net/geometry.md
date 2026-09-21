## Geometry

![Geometry Structure](GeometryView.svg)

The `Geometry` subsystem is the third software subsystem in CanvasNet. It provides
vector-geometry primitives that are independent of pixels, color, and rasterization: an
axis-aligned bounding rectangle (`Rect`), an immutable vector path and its fluent builder
(`Path`/`PathBuilder`), adaptive Bezier curve flattening (`BezierFlattening`), and SVG-style
elliptical arc to cubic Bezier conversion (`SvgArcConverter`).

### Purpose

The `Geometry` subsystem groups the software units responsible for describing shape geometry -
paths made of lines, curves, and arcs, and the bounding boxes and flattened polylines derived
from them - with no notion of pixels, color, or rasterization. It has no dependency on any other
subsystem: the `Canvas` and `Codecs` subsystems have no dependency on `Geometry`, and `Geometry`
has no dependency on either of them. `Geometry` is deliberately distinct from the reserved
`Drawing` subsystem: `Geometry` is where shape geometry is described and measured; the future
`Drawing` subsystem will be where that geometry is turned into pixels on a `Canvas.Surface`
(rasterization, brushes, pens, and stroke/fill styling).

### Units

- **Rect** — an axis-aligned bounding rectangle (position plus size), with union, intersection,
  matrix transformation, containment testing, and a union-identity `Empty` sentinel; see
  _Rect Unit Design_ (`geometry/rect.md`)
- **Path** — an immutable vector path (an ordered collection of independent subpaths) and its
  mutable, fluent `PathBuilder`, together with the supporting `Subpath`, `PathCommand`, and
  `PathCommandType` data types, documented inline within the `Path` unit design rather than as
  their own units, because none of them has any independent behavior beyond being a data carrier
  consumed exclusively by `Path`/`PathBuilder`; see _Path Unit Design_ (`geometry/path.md`)
- **BezierFlattening** — adaptive, tolerance-driven flattening of quadratic and cubic Bezier
  curves into polylines, writing into a caller-supplied output collection; see
  _BezierFlattening Unit Design_ (`geometry/bezier-flattening.md`)
- **SvgArcConverter** — conversion of SVG-style endpoint-parameterized elliptical arcs into cubic
  Bezier curves, following the SVG 1.1 Appendix F algorithm; see
  _SvgArcConverter Unit Design_ (`geometry/svg-arc-converter.md`)

### Dependencies

The `Geometry` subsystem introduces zero new runtime NuGet dependencies. It is implemented
entirely against `System.Numerics.Vector2` and `System.Numerics.Matrix3x2`, in-box BCL types
available natively on all of CanvasNet's target frameworks (net8.0, net9.0, net10.0); no custom
point or vector wrapper type was introduced. Within the subsystem, `Path.GetBounds` depends on
both `BezierFlattening` (to flatten curves when a tighter, tolerance-based bound is requested)
and `SvgArcConverter` (to convert any `ArcTo` command to cubic Bezier segments first, since arcs
carry no control points of their own); `Rect` has no dependency on any other unit in this
subsystem, but is the return type of `Path.GetBounds` and the common bounding-box representation
used throughout.

### Callers

`Rect`, `PathBuilder`/`Path`, `BezierFlattening`, and `SvgArcConverter` are all public API entry
points, invoked externally by consumers of the CanvasNet package. `Geometry` has no runnable
end-to-end example yet within this library, since no rasterizer (the reserved `Drawing`
subsystem) exists yet to consume a `Path` and turn it into pixels on a `Canvas.Surface`; it is
exercised end to end only by this subsystem's own unit and system-integration tests. The
`Geometry` subsystem itself has no dependency on `Canvas`, `Codecs`, or the reserved `Drawing`
subsystem.
