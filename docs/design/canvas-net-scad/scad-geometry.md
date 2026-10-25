## ScadGeometry

<!-- cspell:ignore evaluator evaluators tessellator tessellators tessellation iteratively minkowski multmatrix -->
<!-- cspell:ignore polyhedron extrude extrusions mirrored slab slabs frusta frustum pointwise underflows -->

![ScadGeometry Structure](ScadGeometryView.svg)

### Overview

The `ScadGeometry` subsystem (namespace `DemaConsulting.CanvasNet.Scad.Geometry`, folder
`src/DemaConsulting.CanvasNet.Scad/Geometry/`) turns the geometry-agnostic instantiation tree produced by
evaluation into a `CanvasNetRender3D` `Scene`. It walks the tree, builds the meshes and 2D regions of the
primitives, applies transforms and colors, combines solids and shapes with unions and booleans, extrudes 2D
shapes, applies the modifiers, enforces the triangle budget, and reports everything it cannot build. The
subsystem is **internal**: its types are visible only to the `ScadDocument` unit and, through
`InternalsVisibleTo`, to the test assembly. The public entry points are `ScadDocument.BuildScene` and
`ScadDocument.Render` (see _ScadDocument Unit Design_, `scad-document.md`). The subsystem contains four units:

- **ScadGeometryBuilder** — the engine and everything common to all modules: the iterative walk, the dispatch
  tables, implicit unions, the `!`, `#`, and `%` modifiers, the difference and intersection combiners and the
  dimension rules, the `hull` and `minkowski` convex combinations (`ScadHulls`), the unsupported-module table,
  the triangle budget, cancellation, the reporting channel, the
  node-to-location map, and the copy of the renderer options; see _ScadGeometryBuilder Unit Design_
  (`scad-geometry/scad-geometry-builder.md`)
- **ScadPrimitives** — `cube`, `sphere`, `cylinder`, `polyhedron`, `square`, `circle`, and `polygon`, the
  argument binder, and the fragment rule for `$fn`, `$fa`, and `$fs`; see _ScadPrimitives Unit Design_
  (`scad-geometry/scad-primitives.md`)
- **ScadTransforms** — `translate`, `rotate`, `scale`, `mirror`, `multmatrix`, `resize`, and `color` (with the
  named color table); see _ScadTransforms Unit Design_ (`scad-geometry/scad-transforms.md`)
- **ScadExtrusions** — `linear_extrude` and `rotate_extrude`, the symbolic 2D shape tree, and the slabs of
  top-level 2D shapes; see _ScadExtrusions Unit Design_ (`scad-geometry/scad-extrusions.md`)

The subsystem's namespace documentation is in `Geometry/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetScad-ScadGeometry-*` requirements in `docs/reqstream/canvas-net-scad/scad-geometry.yaml`.

### Interfaces

**Exposed** (internal only):

- `ScadGeometryBuilder.Build(IReadOnlyList<ScadInstance> roots, ScadGeometrySettings settings,
  ScadEvaluationSink sink)` — builds the scene of an evaluated model and returns a `ScadBuiltScene`; throws
  `ScadEvaluationException` (triangle budget), `ScadUnsupportedFeatureException` (strict mode), and
  `OperationCanceledException`.
- `ScadGeometrySettings` — `MaxTriangles` (a `long`), `FlatShapeThickness`, `ThrowOnUnsupportedFeature`, and the
  `CancellationToken`.
- `ScadBuiltScene` — the `Scene` (an empty group when nothing was built) and the `ScadSceneMap` from every
  `MeshNode` to the source location of the module that produced it.
- `ScadSceneOptions.Create` and `ScadSceneOptions.MapFeature` — the copy of the renderer options with a
  substituted unsupported-feature handler, and the mapping of a Render3D report identifier to a `scad-`
  identifier.
- `ScadFeatureIds` — the stable unsupported-feature identifiers (see the table in _Design_).

**Consumed**: from the `ScadEvaluation` subsystem, the instantiation tree (`ScadInstance`, `ScadInstanceKind`,
`ScadModifiers`, `ScadFragmentSettings`), the `ScadValue` of the evaluated arguments, and the
`ScadEvaluationSink` that buffers warnings and unsupported-feature reports; from the `ScadDocument` unit, the
public `ScadLimits`, `ScadEvaluationException`, `ScadUnsupportedFeatureException`, and `ScadSourceLocation`;
from `CanvasNetRender3D`, the public scene-model types (`Scene`, `SceneNode`, `GroupNode`, `MeshNode`, `CsgNode`,
`Mesh`, `Matrix4x4D`, `Vector3D`, `BoundingBox3D`), the tessellators and extruders (`PrimitiveTessellator`,
`PolygonTriangulator`, `PolygonRegion`, `Extruder`, `ConvexHull`, `MinkowskiSum`), and `SceneRenderOptions`; and from
`CanvasNet`, `Rgba32`.
The subsystem never uses the `ScadLanguage` subsystem or the syntax tree.

**Consumers**: `ScadDocument.BuildScene` and `ScadDocument.Render` (through the `Build` entry point and the
renderer options helpers).

### Design

#### Iterative Walk (No Recursion)

A recursive user module nests as deeply as `ScadLimits.MaxRecursionDepth` allows (up to 16,384 evaluation
frames), so a recursive tree walk could overflow the stack of a caller with a small stack, and a stack overflow
cannot be caught. `ScadGeometryBuilder` therefore walks the instantiation tree in post-order with an explicit
`List<Frame>`, exactly as the renderer's CSG compiler does, and **no code in the `Geometry` namespace recurses**:
the `!` pre-pass, the 2D shape-tree walks of the extrusions, the bounds propagation, and the location map are
all iterative. The build behaves identically on any caller stack, including 64 KiB, and needs no stack-guard
thread (the evaluator keeps its own bounded large-stack restart). This is proven by tests that build and render
models nested thousands of levels deep, and hand-built trees 100,000 levels deep, from 64 KiB and 256 KiB
caller threads and compare them with a large-stack result. Reviewers of this subsystem check that no method
calls itself, directly or through another.

#### Dimensions And The Symbolic 2D Tree

The package has no 2D boolean kernel (a clipping library is out of scope). A 2D result is therefore a symbolic
tree (`ScadShape2D`: a leaf `PolygonRegion`, a planar transform, a color, or a boolean of operands), and
extrusion is **distributed over the tree**: `linear_extrude() difference() { A; B; }` is built as
`difference() { extrude(A); extrude(B); }`, with the planar matrices applied to the points of every leaf first and
the colors kept on the extruded parts. An extrusion is a pointwise map of the profile domain, so the result
equals the extrusion of the true 2D difference, and flush caps are resolved by the renderer's coincident-face
handling. A 2D shape that is not extruded and sits at the top of the model becomes a thin slab of
`ScadRenderOptions.FlatShapeThickness` so that it is visible. Mixing dimensions never fails silently: a union
keeps its 3D members and warns about the dropped 2D ones; a difference or an intersection keeps the dimension of
its first non-empty operand and warns about the others; a 3D child under an extrusion warns and yields nothing.

#### Event Classification

The classes are those of the evaluation subsystem (see _ScadEvaluation Subsystem Design_, _Event
Classification_), applied to geometry:

<!-- markdownlint-disable MD013 -->
| Class | Mechanism | Examples | Continues? |
| ----- | --------- | -------- | ---------- |
| **Warning** | `ScadDiagnostic` through the shared sink, at the instance's location | an invalid or non-finite argument (a non-positive size, a wrong type), an unknown, surplus, or repeated argument, a child object under a primitive, a singular transform, a zero `rotate` axis or `mirror` normal, an invalid `color`, a mixed 2D and 3D combination, a 3D child under an extrusion, a polyhedron wound the wrong way round | Yes; an invalid instance is empty (a zero `rotate` axis or `mirror` normal, an invalid `color` value, and an invalid `alpha` warn and keep their children untransformed or uncolored) |
| **Unsupported feature** | `ScadUnsupportedFeature` through the shared sink; in strict mode a `ScadUnsupportedFeatureException` at the first one | a recognized module that is not built, a `hull` or `minkowski` whose operands have no hull or sum, a degraded modifier, a color alpha, an extrusion the Render3D extruders cannot build, a renderer operand defect (see the identifier table) | Yes (the instance or subtree is omitted, or degraded), unless strict |
| **Error** | `ScadEvaluationException` with the location of the module | the triangle budget would be exceeded (`MaxTriangles`), including for hostile sizes whose estimate would overflow | No |
| **Cancellation** | `OperationCanceledException`, never wrapped | the token is canceled before or during the build | No |
<!-- markdownlint-enable MD013 -->

The rule is the evaluator's: invalid _arguments_ (what the author wrote is wrong) are warnings, things the
language allows but this implementation _cannot build_ are unsupported features, and things that would
_exhaust resources_ are errors. In no case is a module silently skipped. Geometry builders never throw an
`ArgumentException` out of the subsystem: arguments are validated before a Render3D tessellator or extruder is
called, and a rejection that the validation did not anticipate is reported as `scad-geometry-rejected` (the
message of the exception is kept).

#### One Reporting Channel

`ScadGeometryBuilder` receives the `ScadEvaluationSink` of the evaluation and writes its warnings and
unsupported-feature reports into it through `ScadReporter`, which also implements strict mode (the first report
of a feature at a location throws `ScadUnsupportedFeatureException`). `ScadDocument` delivers the sink once, so
the events of evaluation, geometry building, and the renderer's operand checks are ordered, de-duplicated by
(message or feature, location), capped like the evaluator's diagnostics, and delivered on the calling thread,
and strict mode behaves identically at every stage.

#### Unsupported-Feature Identifiers

Every identifier below is lower-case and stable; callers can match on `ScadUnsupportedFeature.Feature`. The
identifiers of the evaluation subsystem (`scad-function-<name>`, `scad-search-form`) are unchanged.

<!-- markdownlint-disable MD013 -->
| Identifier | Trigger | Effect |
| ---------- | ------- | ------ |
| `scad-module-text`, `scad-module-offset`, `scad-module-projection`, `scad-module-surface`, `scad-module-import`, `scad-module-roof` | The module is recognized but not implemented | The module and its children are omitted |
| `scad-hull-csg-operand` | A `hull` has a difference or an intersection among its children (3D, or 2D, at any depth below it) | The hull is omitted |
| `scad-hull-degenerate` | The points of a `hull` span no volume (3D) or no area (2D) | The hull is omitted |
| `scad-hull-mixed-dimensions` | A `hull` has both 2D and 3D children | The hull is omitted |
| `scad-minkowski-nonconvex` | A `minkowski` has a child that is a boolean or a mesh that is not convex (within the Render3D tolerance), also a lone child | The module is omitted; the message carries the reason |
| `scad-minkowski-2d` | A `minkowski` of 2D children (deferred) | The module is omitted |
| `scad-minkowski-mixed-dimensions` | A `minkowski` has both 2D and 3D children | The module is omitted |
| `scad-minkowski-too-complex` | A child has more than 4,096 convex parts, a fold step needs more than 4,096 convex sums, or two meshes have more than `MinkowskiSum.MaxPairwiseSums` pairwise vertex sums | The module is omitted |
| `scad-modifier-highlight` | The `#` modifier | The subtree is built normally, without highlighting |
| `scad-modifier-background` | The `%` modifier | The subtree is omitted |
| `scad-color-alpha` | A color with alpha below 1 (vector, hex, `alpha=`, or `transparent`) | The geometry is drawn opaque |
| `scad-resize-approximate` | `resize` over children that contain a difference or an intersection | The estimated bounds are used |
| `scad-multmatrix-projective` | `multmatrix` with a bottom row other than `[0, 0, 0, 1]` | The module and its children are omitted |
| `scad-polygon-invalid` | A `polygon`, or any 2D leaf after transformation under an extrusion or slab, that is self-crossing or zero-area | That leaf is omitted |
| `scad-polyhedron-face-invalid` | A polyhedron face that cannot be triangulated | The polyhedron is omitted |
| `scad-linear-extrude-scale` | A negative `scale` component, or exactly one zero component | The extrusion is omitted |
| `scad-linear-extrude-apex-multiple-contours` | `scale = 0` over a profile with holes or islands in one leaf | The extrusion is omitted |
| `scad-rotate-extrude-axis-crossing` | The profile lies on both sides of the axis (also a union or a boolean operand that does) | The extrusion is omitted |
| `scad-rotate-extrude-axis-contact` | The profile touches the axis at an isolated vertex | The extrusion is omitted |
| `scad-geometry-rejected` | Any other `ArgumentException` of a Render3D tessellator or extruder, or an instance whose coordinates are not finite or exceed 1e100 in magnitude (including hidden difference operands) | The affected instance is omitted; the message is kept |
| `scad-csg-open-mesh`, `scad-csg-non-manifold-edge`, `scad-csg-inconsistent-winding`, `scad-csg-degenerate-triangle`, `scad-csg-inverted-mesh`, `scad-csg-singular-transform` | The renderer's operand check of a difference or an intersection (only `Render`, only when a handler or strict mode is set), mapped from `render3d-csg-*` | Reported at the producing module; the image is still drawn |
<!-- markdownlint-enable MD013 -->

#### Dialect Decisions Of The Geometry Phase

These are original decisions of the CanvasNetScad dialect, each pinned by a test and described in the unit
designs.

1. **Singular transforms.** A transform whose matrix is singular or non-finite (zero determinant, or a
   determinant that underflows relative to the matrix) is a warning and an empty instance, never a renderer
   report. The mapped `scad-csg-singular-transform` is reached only when the product of several valid
   transforms underflows at render time.
2. **`rotate_extrude` profiles on the negative side.** A profile whose vertices all have `x <= 0` is swept where
   it lies (a point at `(-4, 0)` with `angle = 90` sweeps from 180 to 270 degrees, the quadrant `x < 0`,
   `y < 0`); a profile on both sides of the axis is
   `scad-rotate-extrude-axis-crossing`. A negative `angle` sweeps clockwise (the revolved mesh is mirrored); an
   `angle` beyond 360 degrees in magnitude is clamped to 360; zero is a warning and empty.
3. **Rejected extrusions are omitted whole.** When an extrusion is rejected (axis crossing or contact, an invalid
   scale, or a Render3D rejection), the whole extrusion is omitted, not only the failing leaf, so a boolean over
   it never silently changes meaning. Axis crossing and contact are judged over all leaves of the 2D tree (a
   union that spans both sides crosses). An invalid leaf after transformation (`scad-polygon-invalid`) drops only
   that leaf.
4. **Mixed dimensions.** A union keeps the 3D members and warns about the dropped 2D ones; a difference or an
   intersection keeps the dimension of the first non-empty operand and warns at the boolean.
5. **Invalid arguments.** A warning and an empty instance, with the documented exceptions in the event table
   above; an absent argument is the identity.
6. **Twist.** A positive `twist` of `linear_extrude` turns clockwise seen from `+Z` (the builder negates it for
   the Render3D extruder, whose positive twist is counter-clockwise); the default height is 100.
7. **`rotate_extrude` sweep step.** The number of segments is
   `ceil(max(fragments(maxAbsX) * |angle| / 360, 1))`, raised to `ceil(|angle| / 120)`, so that every angular
   step is at most 120 degrees, the contract of `Extruder.Rotate` (see _Extruder Unit Design_ in the Render3D
   _Tessellation_ subsystem). The raise never takes effect for a fragment count of at least three.
8. **Modifiers.** `!` makes the outermost marked subtrees the new root of the design, without the transforms and
   colors of their ancestors (`translate([10, 0, 0]) { !cube(1); }` renders the cube at the origin); `%{ !x }` with
   a single child is one instance that is both, so it is omitted and the model
   is empty.
9. **Mesh locations.** The meshes of an extrusion are mapped to the location of the extrusion module; the slab of
   a top-level 2D shape is mapped to the location of its 2D leaf.
10. **Colors.** The named color table is the CSS table already in this repository's SVG codec (authored
    in-repo from the public W3C list, 148 names plus `transparent`); the innermost color wins.
11. **Hull and Minkowski.** A `hull` is the convex hull of every vertex of every mesh below its children in their
    transformed positions (a union of children is one point set), or, for 2D children, of the vertices of every leaf
    shape; it is built on `ConvexHull`, whose result is a closed, outward-wound mesh of the strictly extreme vertices.
    A `minkowski` is folded from the left; each child is the list of the meshes of its union-only scene tree, the sum
    distributes over a union, and each pairwise sum is `MinkowskiSum.ConvexSum`, which verifies that both operands are
    convex (within its documented tolerance) or refuses. Nothing is approximated: a boolean below a `hull`, a
    boolean or a non-convex mesh below a `minkowski`, mixed dimensions, degenerate points and the 2D Minkowski
    sum are reported (`scad-hull-*`, `scad-minkowski-*`) and the module is omitted whole. A lone `minkowski` child is
    verified the same way (by a discarded sum with a small tetrahedron) and returned unchanged. The `convexity`
    argument is ignored, and a color inside either module is dropped with a warning (the result takes the enclosing
    `color`). The result of both modules is a new scene mesh at the module's location, so it is a valid operand of
    later booleans, transforms and extrusions.

#### Fragment Rule

The number of segments of a circle of radius `r` is public and deterministic: `r` non-finite or at most `1e-8`
gives 3; a positive `$fn` gives `max(3, truncate($fn))`; otherwise `ceil(max(min(360 / $fa, 2 pi r / $fs), 5))`
with `$fa` and `$fs` clamped to at least 0.01 (a non-finite value falls back to its default, 12 and 2); the
result is clamped to 3 through 1,048,576 as an overflow guard, and the real cost cap is the triangle limit.
With the defaults, a radius of 1 gives 5 and a radius of 10 gives 30. A sphere uses the radius of the sphere, a
cylinder the larger of its two radii, a circle its radius, `rotate_extrude` the largest profile abscissa, and a
twisted `linear_extrude` the largest profile radius. See _ScadPrimitives Unit Design_.

#### Resource Limits

`ScadLimits.MaxTriangles` (default 5,000,000) bounds the geometry of one build. Every unit computes the exact
triangle count of a mesh from its parameters before it allocates (a sphere, a cylinder, a polyhedron, a slab, and
an extrusion, whose caps have `V + 2H − 2P` triangles for a profile with `V` vertices, `H` holes and `P` solid
contours), with saturating arithmetic, and fails with a located `ScadEvaluationException` when the count would
exceed the remaining budget. The charge is reconciled with the actual mesh when it is created, so the triangles
charged always equal the triangles of the meshes built. A `hull` and a `minkowski` know the triangle count of
their result only once it is computed, so they charge it exactly right after the computation and before the node
is created (their work is bounded first: the points of a hull by `MaxTriangles`, the pairwise sums of two
meshes by `MinkowskiSum.MaxPairwiseSums`); the temporary sums of a Minkowski fold are charged while they live and
refunded when the next step has consumed them, so the operands stay charged and only the meshes of the result
remain. The budget accumulates across all instances, including
loops. The renderer's own resource
limit (`SceneRenderOptions.MaxFragmentsPerTile`) is reported by `ScadDocument.Render` as a
`ScadEvaluationException` without a location.

#### Renderer Hand-Off

`ScadDocument.Render` draws the scene with `SceneRenderer.Render` (or fits the camera to the scene bounds). The
renderer checks the operands of a difference or an intersection only when it has an unsupported-feature handler,
so `Render` installs one only when the caller supplied a handler or strict mode; it maps each report back to the
module that produced the mesh through the node-to-location map and to a `scad-csg-*` identifier through
`MapFeature`. `SceneRenderOptions` has no clone, so `ScadSceneOptions.Create` copies every public property (a
reflection test fails when a property is added to Render3D and not copied) and replaces the handler and the
strict flag, which are ignored in the caller's scene options.

#### Determinism

The same instantiation tree and settings give the same scene on every platform, thread, stack size, and culture:
the walk is ordered, the tables are ordinal, the fragment rule and the matrices are pure functions of the
arguments, and no wall-clock, random, or environment input exists. A build holds its state in per-call objects,
so independent builds can run concurrently.

#### Clean-Room Provenance

The geometry phase is an original implementation. No OpenSCAD source code, test, manual prose, or example was
read, copied, ported, or paraphrased; every decision above is the dialect's own and is pinned by in-repo tests
and fixtures (`ScadFixtures/render-*.scad`), whose assertions are analytic (areas, bounds, pixel rectangles, and
exact volumes), not golden images. The named color table is the repository's own SVG table. Exact parity with
any other implementation (pixels, tessellation, edge cases) is a non-goal. See _CanvasNetScad System Design_,
Clean-Room Provenance.
