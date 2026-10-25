### ScadPrimitives

<!-- cspell:ignore evaluator undef polyhedron polyhedra tessellator tessellators tessellation Newell frustum truthy
  truthiness -->

The `ScadPrimitives` unit is the second software unit in the `ScadGeometry` subsystem. It comprises five source
files in the `DemaConsulting.CanvasNet.Scad.Geometry` namespace, all `internal`:

- `ScadArguments.cs` — `ScadArguments` (sealed class): binds the evaluated arguments of an instance to a module's
  documented parameter names and reads typed values
- `ScadFragmentRule.cs` — `ScadFragmentRule` (static class): the public `$fn`, `$fa`, `$fs` fragment-count formula
- `ScadPrimitives.cs` — `ScadPrimitives` (static partial class): `cube`, `sphere`, `cylinder`, and the helpers
  shared by every builder (argument preparation, length reading, budget charging, rejection reporting)
- `ScadPrimitives.Polyhedron.cs` — the `polyhedron` builder: argument parsing, winding reversal, and face
  triangulation
- `ScadPrimitives.Shapes2D.cs` — the 2D shapes `square`, `circle`, and `polygon`

#### Purpose

`ScadPrimitives` turns the seven primitive module instances of the evaluator's `ScadInstance` tree — `cube`,
`sphere`, `cylinder`, `polyhedron`, `square`, `circle`, and `polygon` — into Render3D geometry: a `MeshNode` (a
closed, outward-oriented `Mesh`) for a solid, or a flat `PolygonRegion` result for a 2D shape. It owns the
calling convention shared by all of them (the argument binder), the resolution rule for round shapes (the
fragment rule), the dialect of each primitive's parameters, and the translation of every kind of problem into
the right kind of report: a **warning** for an invalid argument, an **unsupported feature** for a shape that the
polygon, polyhedron, or Render3D checks cannot accept, and a **`ScadEvaluationException`** for resource
exhaustion. The dialect (parameter precedence, `center` truthiness, winding conventions, the fragment formula)
is an original, test-pinned decision of CanvasNetScad (see _CanvasNetScad System Design_, Clean-Room
Provenance); no OpenSCAD source or manual text was consulted. The fragment formula is the public, documented
formula of the language plus two safety clamps.

The unit's behavior is specified by the `CanvasNetScad-ScadGeometry-ScadPrimitives-*` requirements
(`ArgumentBinding`, `ArgumentWarnings`, `TypedReaders`, `FragmentFormula`, `FragmentClamps`, `Cube`, `Sphere`,
`Cylinder`, `CylinderRadii`, `InvalidSolidArguments`, `Shape2D`, `InvalidShapeArguments`, `Polygon`,
`PolygonInvalid`, `Polyhedron`, `PolyhedronTriangulation`, `TriangleBudget`, and `GeometryRejected`) in
`docs/reqstream/canvas-net-scad/scad-geometry/scad-primitives.yaml`.

#### Data Model

**`ScadArguments`** holds the documented parameter names in positional order and the bound `ScadValue` by
parameter index. It is created by `Bind`, is short-lived, and is used by one build on one thread. An unbound
parameter keeps the default `ScadValue`, whose kind is `Undef`.

**`ScadFragmentRule`** is stateless. Its constants are `MinimumFragments` (3), `MaximumFragments` (`1 << 20`),
`MinimumRadius` (`1e-8`), and `MinimumStep` (`0.01`); the defaults substituted for a non-finite `$fa` and `$fs`
are 12 and 2, and the floor of the automatic rule is 5.

**`ScadPrimitives`** is stateless. It keeps one `string[]` of parameter names per module in positional order:

| Module       | Parameters, in positional order                  |
|--------------|--------------------------------------------------|
| `cube`       | `size`, `center`                                 |
| `sphere`     | `r`, `d`                                         |
| `cylinder`   | `h`, `r1`, `r2`, `center`, `r`, `d`, `d1`, `d2`  |
| `polyhedron` | `points`, `faces`, `convexity`, `triangles`      |
| `square`     | `size`, `center`                                 |
| `circle`     | `r`, `d`                                         |
| `polygon`    | `points`, `paths`, `convexity`                   |

Two private value types support the length parameters: `LengthState` (`Absent`, `Valid`, `Invalid`) and the
`Length(State, Value)` record struct, whose value is already scaled (a diameter is halved). The public method
`SlabTrianglesLowerBound(vertexCount)` (`8·V / 3`) is the fewest triangles the slab of a region of `V` vertices can
have (`2V + 2(V + 2H − 2P)` with every contour at least a triangle); `Flat` uses it to refuse a hostile fragment
count early, while the `ScadExtrusions` unit charges the slab's exact count.

**Results** are `ScadGeo` values defined by the `ScadGeometryBuilder` unit: `ScadGeo.Empty` for a rejected
instance, `ScadGeo.FromMesh(node)` for a solid, and `ScadGeo.FromRegion(region, location)` for a 2D shape. The
`ScadGeometryContext` supplies the reporter, the triangle budget, cancellation polling, and the mesh-node
location map.

#### Key Methods

##### `ScadArguments.Bind(ScadInstance node, string[] names, ScadGeometryContext context)`

Walks the instance's arguments in call order. An unnamed argument fills the next parameter; an unnamed argument
beyond the parameter list is reported (`Ignoring extra argument N of <module>`) and ignored. A named argument
whose name starts with `$` is ignored silently, because the evaluator has already folded `$fn`, `$fa`, `$fs`,
and `$t` into `ScadInstance.Fragments`. Any other named argument fills the parameter of that name; an unknown
name is reported (`Ignoring unknown argument '<name>' of <module>`) and ignored. A parameter supplied twice
keeps the last value and is reported (`Argument '<name>' of <module> is given more than once; using the last
value`). Every warning is located at the instance.

##### `ScadArguments.TryGet(string name, out ScadValue value)`

Returns `true` when the parameter was supplied with a value other than `undef`, so an `undef` argument counts as
not supplied and the module's default applies. Asking for a name that is not one of the module's parameters is a
programming error and throws `ArgumentException`.

##### `TryNumber`, `TryNumbers`, `TryIndex`

Static typed readers that never throw: `TryNumber` accepts a finite number; `TryNumbers(value, count)` accepts a
vector of exactly `count` finite numbers; `TryIndex` accepts a non-negative whole number that fits in an `int`
(a list index). They return `false` for anything else, and the caller reports the warning for its module.

##### `ScadFragmentRule.Fragments(radius, fn, fa, fs)` and `Fragments(radius, settings)`

The rule is evaluated in this order (the order matters: the tiny-radius rule is applied **before** `$fn` is
considered, so `$fn = 100` on a circle of radius `1e-9` still has 3 fragments, while radius `1e-7` honors `$fn`;
pinned by `ScadFragmentRule_Fragments_TinyRadiusRuleComesBeforeFn`):

1. A radius that is non-finite or at most `MinimumRadius` gives 3, whatever the settings.
2. A positive `$fn` gives `max(3, truncate($fn))`, capped at `MaximumFragments`; NaN is neither positive nor
   negative and falls through.
3. Otherwise the count is `ceil(max(min(360 / $fa, radius * 2 * pi / $fs), 5))`, where `$fa` and `$fs` are
   clamped to at least `MinimumStep` (a non-finite value is replaced by its default), and the result is clamped
   to `[3, MaximumFragments]`.

Spot values with the defaults (`$fa = 12`, `$fs = 2`): radius 1 gives 5, 2 gives 7, 10 gives 30, and 100 gives
30; `$fn = 1` gives 3 and `$fn = 64` gives 64. The overload taking a `ScadFragmentSettings` applies the same rule
to the settings in effect at an instance. The rule is pure, deterministic, and thread-safe; the real cost cap
is `ScadLimits.MaxTriangles`, not the fragment cap.

##### `ScadPrimitives.Cube`, `Sphere`, `Cylinder`

Each builder prepares its arguments (`Begin`: warn when the instance has child objects, then `Bind`), validates
them, and passes the tessellation to the shared `Solid` helper together with the exact triangle count of the mesh.

- **`cube(size, center)`** — `size` is a number (an edge) or a vector of exactly three numbers, default 1; every
  extent must be positive. `center` is true for any truthy value. The mesh is `PrimitiveTessellator.Box`; the
  charge is 12 triangles.
- **`sphere(r, d)`** — `r` and `d` must be positive numbers; **a diameter overrides the radius** (default radius
  1). The fragment count is `Fragments(radius, node.Fragments)`, the rings are `max(2, (fragments + 1) / 2)`,
  the mesh is `PrimitiveTessellator.Sphere(radius, fragments)`, and the charge is `2 * fragments * (rings - 1)`
  (two pole fans and `rings - 2` bands of two triangles per fragment).
- **`cylinder(h, r1, r2, center, r, d, d1, d2)`** — `h` must be positive (default 1); the radii and diameters
  must be non-negative numbers. Precedence for each end, weakest to strongest: the default 1, `r`, `d`, the
  per-end `r1`/`r2`, and the per-end `d1`/`d2`. At least one end radius must be greater than zero (a cone has
  one zero end). The fragment count uses `max(r1, r2)`; the mesh is `PrimitiveTessellator.Cylinder`; the charge
  is `4 * fragments` (`2 * fragments` for a cone).

##### `ScadPrimitives.Square`, `Circle`

`square(size, center)` mirrors `cube` with two extents and `PrimitiveTessellator.Rectangle`; `circle(r, d)`
mirrors `sphere`'s radius rules with `PrimitiveTessellator.Circle` and the fragment rule. Both return a flat
result through the shared `Flat` helper.

##### `ScadPrimitives.Polygon`

`points` is required and must be a list of at least three `[x, y]` pairs of finite numbers. `paths` is optional;
when absent the polygon is one contour through all the points in order, and when given it must be a non-empty
list of lists of whole-number indices below the point count. `convexity` is accepted and ignored. The region is
built by `PrimitiveTessellator.Polygon(points, paths)`, which normalizes winding and derives holes by nesting, so
the winding of a contour or of a path does not matter.

##### `ScadPrimitives.Polyhedron`

1. `points` is required and must be a list of `[x, y, z]` triples of finite numbers. The faces come from `faces`,
   or from the legacy `triangles` **only when `faces` is absent** (`faces` wins when both are given).
2. Each face must hold at least three distinct, in-range point indices, and the list must not be empty;
   otherwise an argument warning.
3. **Every face is reversed**: the language lists faces clockwise when seen from outside, Render3D wants
   counter-clockwise.
4. The triangle budget is charged with `sum(face vertices - 2)` before any triangulation.
5. Each face of more than three vertices is triangulated. The face is projected onto its best-fit plane (the
   Newell normal, with a `(u, v)` basis that turns counter-clockwise about it). A **strictly convex** projected
   face (every turn clearly counter-clockwise, tolerance `1e-12` relative to the squared extent) is **fanned
   from its first vertex**, which also covers slightly non-planar convex faces. Any other face is **ear-clipped**
   with `PolygonRegion` and `PolygonTriangulator`, and each region vertex is mapped back to its face vertex by
   exact coordinate. A zero or non-finite normal, or a projected polygon that Render3D rejects (self-touching,
   crossing, or without area), makes the face impossible to triangulate.
6. The mesh is assembled with `PrimitiveTessellator.Polyhedron(points, triangles)`.
7. **Negative-volume reversal**: when the mesh is closed and its `SignedVolume` is negative, the faces were
   evidently listed counter-clockwise from outside, so the mesh is reversed with `Mesh.Reverse()` and a warning
   says so.
8. An **open mesh is kept as given**, without a report: the renderer shows it correctly, and Render3D reports it
   later only if it becomes a boolean operand (mapped to `scad-csg-open-mesh` by the builder unit). A point that
   no face uses is harmless.

##### Shared helpers

- **`Begin`** warns `<module>() takes no child objects; ignoring them` when the instance has children, then binds
  the arguments; the primitive is still built. The child warning is therefore reported before any argument
  warning.
- **`Invalid`** reports `<module>: <problem>; the instance is ignored` as a warning at the instance's location
  and returns `ScadGeo.Empty`.
- **`ReadLength` and `FirstValid`** read a length-like parameter (`Absent`, `Valid`, or `Invalid`, scaled by 0.5
  for a diameter) and choose the strongest valid argument or a fallback.
- **`Solid`** charges the triangle budget with the exact count (`ChargeTriangles`), polls cancellation, builds the
  mesh, settles the charge against the mesh's triangle count (`SettleTriangles`; a rejected tessellation is
  refunded), and wraps it with `CreateMeshNode`, which records the mesh node's source location in the scene map.
- **`Flat`** checks that the slab the region may become fits the budget (`EnsureTriangles(SlabTrianglesLowerBound(vertexEstimate))`)
  **without charging it**; the exact charge happens when the slab is made at the root or when the region is extruded.
  It then builds the region, returns `ScadGeo.Empty` for an empty region, and wraps the region with the
  instance's location otherwise. The vertex estimate is 4 for `square`, the fragment count for `circle`, and the
  point count for `polygon`.

#### Error Handling

- **Warning (invalid argument)**: a wrong type, a non-positive size or radius, a negative radius or diameter, no
  positive cylinder radius, a missing required parameter (`points`, `faces`), a malformed `points`, `paths`, or
  `faces` list, fewer than three points, or an index out of range. The instance yields `ScadGeo.Empty` and the
  warning names the module and the problem at the instance's location. Unknown, surplus, and duplicate arguments
  and child objects are also warnings, but the primitive is still built.
- **Warning (polyhedron winding)**: a closed polyhedron with negative volume is reversed and reported.
- **Unsupported feature** (`ScadFeatureIds`, reported through the `ScadReporter`, which throws a
  `ScadUnsupportedFeatureException` for the first report of a feature at a location in strict mode):
  - `scad-polygon-invalid` — `polygon`: the region was rejected (self-crossing or self-touching contours, zero
    area); the message begins with `polygon: the shape was rejected:` and keeps the rejection's text.
  - `scad-polyhedron-face-invalid` — `polyhedron`: a face cannot be triangulated; the whole polyhedron is
    omitted.
  - `scad-geometry-rejected` — the catch-all: any other `ArgumentException` from a Render3D tessellator for an
    input the argument checks accepted (message `<module>: the geometry was rejected: …` for solids and
    `<module>: the shape was rejected: …` for 2D shapes; `circle` and `square` use this id). A build therefore
    never crashes on a primitive. The path is shared by every builder but is hard to reach from source text,
    because the argument checks foresee almost every rejection; the polyhedron test with welded duplicate points
    accepts either this id or the face id.
- **`ScadEvaluationException`** (located at the instance): `Triangle limit exceeded (limit N)` when an estimate
  does not fit the remaining `ScadLimits.MaxTriangles` budget. The estimate is made before any allocation, so a
  hostile `$fn` or face list fails before memory is spent. For `Flat`, only the slab's _possible_ cost is checked
  and nothing is charged until the slab is made.
- **`OperationCanceledException`** — `Solid`, `Flat`, and `Polyhedron` poll cancellation before the tessellation;
  the exception is never wrapped.
- **`ArgumentException` from `ScadArguments.TryGet`** — a programming error (a parameter name that is not in the
  module's list), never caused by user input.
- **No recursion**: no method of the unit is recursive. The binder, the readers, the face loops, the fan, and the
  convexity test are loops, and the ear clipping is delegated to the Render3D triangulator. The unit is therefore
  stack-safe on any caller stack, which supports the iterative guarantee of the engine (see _ScadGeometry
  Subsystem Design_).
- **Threading**: the builders are stateless and thread-safe; a `ScadArguments` instance and the context belong to
  one build on one thread.

#### Dependencies

- **`ScadEvaluation` subsystem** — `ScadInstance` (`Name`, `Arguments`, `Children`, `Fragments`, `Location`),
  `ScadFragmentSettings`, `ScadValue` and `ScadValueKind`, `ScadEvaluationSink` (reached through the reporter),
  and `ScadEvaluationException` (see _ScadEvaluation Subsystem Design_).
- **`ScadGeometry` siblings** — `ScadGeometryContext` (reporter, `ChargeTriangles`, `EnsureTriangles`, `Poll`,
  `CreateMeshNode`), `ScadReporter`, `ScadGeo`, and `ScadFeatureIds`; `ScadLimits.MaxTriangles` through the
  context's settings.
- **`Render3D` public types** — `Mesh`, `MeshNode`, `Vector2D`, `Vector3D`, `PolygonRegion`,
  `PrimitiveTessellator` (`Box`, `Sphere`, `Cylinder`, `Rectangle`, `Circle`, `Polygon`, `Polyhedron`), and
  `PolygonTriangulator`.
- **.NET base class library** — `System`, `System.Collections.Generic`, `System.Linq`, and
  `System.Globalization`.

#### Callers

`ScadGeometryBuilder` is the only production caller: its leaf table maps the module names `cube`, `sphere`,
`cylinder`, `polyhedron`, `square`, `circle`, and `polygon` to the matching `ScadPrimitives` builder (see
_ScadGeometryBuilder Unit Design_), and the builder consumes the returned `ScadGeo` (a solid, a flat result, or
empty) according to its dimension rules. The `ScadTransforms` unit (its transform and color builders) and the
`ScadExtrusions` unit reuse `ScadArguments`; the extrusion unit also reuses `ScadFragmentRule` and the
`SlabTrianglesLowerBound` method. The tests in
`test/DemaConsulting.CanvasNet.Scad.Tests/Geometry/` reach the unit through `InternalsVisibleTo`: the
argument and fragment-rule tests call `ScadArguments.Bind` and `ScadFragmentRule.Fragments` directly, and the
primitive tests build real programs through the evaluator and `ScadGeometryBuilder` (the helpers of
`ScadGeometryTestSupport`) and assert on the resulting meshes, warnings, and unsupported-feature reports.
