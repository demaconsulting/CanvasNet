### ScadGeometryBuilder

<!-- cspell:ignore evaluator minuend subtrahend subtrahends minkowski multmatrix tessellation tessellator -->

The `ScadGeometryBuilder` unit is the first software unit in the `ScadGeometry` subsystem. It comprises eleven
source files in the `DemaConsulting.CanvasNet.Scad.Geometry` namespace, all `internal`:

- `ScadGeometryBuilder.cs` — `ScadGeometryBuilder` (sealed partial class): the entry point `Build`, the dimension
  of the model root, and the class-level documentation of the engine
- `ScadGeometryBuilder.Walk.cs` — the iterative post-order engine (`BuildForest`, `Frame`, `Enter`)
- `ScadGeometryBuilder.Modifiers.cs` — the `!` (show only) pre-pass `FindShowOnlyRoots`
- `ScadGeometryBuilder.Unsupported.cs` — the leaf, container, and unsupported module tables and
  `ReportUnsupportedModule`
- `ScadGeo.cs` — `ScadGeo` and `ScadGeoKind`: the result of building one subtree (nothing, a 3D node, or a 2D
  shape tree) with its bounds
- `ScadGeometryContext.cs` — `ScadGeometryContext` (the services of one build), and the records
  `ScadGeometrySettings` and `ScadBuiltScene`
- `ScadSceneMap.cs` — `ScadSceneMap`: the map from mesh nodes to source locations
- `ScadReporter.cs` — `ScadReporter`: the single reporting channel
- `ScadSceneOptions.cs` — `ScadSceneOptions`: the copy of the renderer options and the feature-id mapping
- `ScadFeatureIds.cs` — `ScadFeatureIds`: the stable unsupported-feature identifiers
- `ScadBooleans.cs` — `ScadBooleans`: the implicit union and the `difference` and `intersection` combiners

#### Purpose

`ScadGeometryBuilder` converts the geometry-agnostic instantiation tree produced by the evaluator (a list of
`ScadInstance` roots) into a Render3D `Scene`, and returns with it the map from the scene's mesh nodes back to
source locations. It owns what is common to every module: the walk, the implicit union, the modifiers, the CSG
booleans and the dimension rules, the table of modules that are recognized but not built, the triangle budget,
cancellation, the single reporting channel, and the copy of the renderer options. What a particular primitive,
transform, color, or extrusion builds is delegated to the `ScadPrimitives`, `ScadTransforms`, and
`ScadExtrusions` units through the dispatch tables (see _ScadGeometry Subsystem Design_).

The central decision is that the engine is **iterative**: a recursive user module nests as deeply as
`ScadLimits.MaxRecursionDepth` allows (thousands of levels), so the engine walks the tree with an explicit stack
in post-order, like the renderer's CSG compiler, and **no code in the `Geometry` namespace recurses** (the
pre-pass, the 2D-tree walks, and the bounds propagation included). The build therefore behaves identically on any
caller stack, including one of 64 KiB, and needs no stack-guard thread. The semantics are original decisions of
the CanvasNetScad dialect (see _CanvasNetScad System Design_, Clean-Room Provenance); no OpenSCAD source or manual
text was consulted.

The unit's behavior is specified by the `CanvasNetScad-ScadGeometry-ScadGeometryBuilder-*` requirements
(`NoRecursion`, `ImplicitUnion`, `ShowOnly`, `DegradedModifiers`, `UnsupportedModules`, `Reporting`,
`StrictMode`, `Difference`, `Intersection`, `BooleanOperands`, `MixedDimensions`, `FlatBooleans`,
`ResizeBounds`, `TriangleBudget`, `CoordinateRange`, `FlatSlabs`, `Cancellation`, `SceneMap`,
`SceneOptionsCopy`, and `SceneOptionsMapping`) in `docs/reqstream/canvas-net-scad/scad-geometry/scad-geometry-builder.yaml`.

#### Data Model

**`ScadGeometrySettings`** (record) holds the validated inputs of one build: `MaxTriangles` (a `long`, from
`ScadLimits.MaxTriangles`), `FlatShapeThickness` (the slab thickness of a top-level 2D shape),
`ThrowOnUnsupportedFeature` (strict mode), and the `CancellationToken`. **`ScadBuiltScene`** (record) is the
outcome: the `Scene` (an empty group when nothing was built) and the `ScadSceneMap`.

**`ScadGeometryContext`** holds the services of one build on one thread: the `ScadReporter`, the
`ScadSceneMap`, the settings, the cancellation schedule, and the triangle counter. `Tick()` counts one visited
instance and polls cancellation once every 256 instances; `Poll()` polls immediately (before a heavy
tessellation); `EnsureTriangles(estimate, location)` checks that an estimate fits in the remaining budget without
charging it; `ChargeTriangles(estimate, location)` checks and then charges; `CreateMeshNode(mesh, location)`
creates a `MeshNode` and records its location in the map. The context is single-use and not thread-safe;
independent builds, each with its own context, can run concurrently.

**`ScadGeo`** is the result of building one subtree, consumed exactly once by its parent (the instantiation tree
has no shared nodes), so nodes and shapes are handed on without copying. `Kind` is `Empty` (an unsupported
module, an invalid instance, or an empty group), `Solid` (a Render3D `SceneNode`), or `Flat` (a `ScadShape2D`
tree that has not been extruded; see _ScadExtrusions Unit Design_). Every result carries `Bounds`, an **upper
bound** of the extent of the geometry (a 2D result has a box at Z = 0) computed bottom-up in O(1) per node for
`resize`, and two qualifiers: `IsApproximate` is set when a difference or an intersection of several operands is
inside (the box is then the first operand's, respectively the overlap of the operand boxes, and is not
necessarily the true extent), and `IsTight` is set when the box is exactly the extent of the vertices (lost by a
rotation that is not a multiple of 90 degrees and by an approximate result; `resize` recomputes exact vertex
bounds on demand). `Reach` is the box of every vertex below the node, **including the operands that a difference or an
intersection hides** (the union of the operands' reach, transformed with the node's matrix); it is what the
renderer sees and what the coordinate guard checks. `FlatLocation` is the location of the first 2D leaf, used to
place a mixed-dimension warning.
Factories: `Empty`, `FromMesh`, `FromSolid`, `FromRegion`, and `FromShape`. The union of several solids becomes a
`GroupNode` only when there is more than one, so a deep chain of single-child modules does not grow wrapper
nodes.

**`ScadSceneMap`** is a dictionary from `MeshNode` to `ScadSourceLocation` compared by **reference** equality. It
is filled while the scene is built (only through `CreateMeshNode`) and only read afterwards. `Find(node)` returns
the recorded location, or `default` (an unknown location) for a `null` node or one that was not recorded.

**`ScadReporter`** wraps the `ScadEvaluationSink` and the strict flag; see Error Handling.

**`ScadFeatureIds`** holds the stable, lower-case identifiers reported as `ScadUnsupportedFeature.Feature`:

| Identifier                                                          | Meaning                                      |
|---------------------------------------------------------------------|----------------------------------------------|
| `scad-module-text`, `-offset`, `-projection`, `-surface`, `-import` | recognized module not built; omitted         |
| `scad-module-roof`, `scad-module-hull`, `scad-module-minkowski`     | recognized module not built; omitted         |
| `scad-modifier-highlight`                                           | `#` renders normally, not highlighted        |
| `scad-modifier-background`                                          | `%` subtree omitted                          |
| `scad-color-alpha`                                                  | alpha below 1 drawn opaque (`ScadColors`)    |
| `scad-resize-approximate`                                           | `resize` used estimated bounds               |
| `scad-multmatrix-projective`                                        | projective `multmatrix` omitted              |
| `scad-polygon-invalid`, `scad-polyhedron-face-invalid`              | invalid 2D leaf or polyhedron omitted        |
| `scad-linear-extrude-scale`                                         | invalid extrusion scale; extrusion omitted   |
| `scad-linear-extrude-apex-multiple-contours`                        | apex over several contours; omitted          |
| `scad-rotate-extrude-axis-crossing`, `-axis-contact`                | profile crosses or touches the axis; omitted |
| `scad-geometry-rejected`                                            | Render3D rejection; coordinates out of range |
| `scad-csg-open-mesh`, `-non-manifold-edge`, `-inconsistent-winding` | renderer operand reports mapped back to      |
| `scad-csg-degenerate-triangle`, `-inverted-mesh`                    | the producing module (see `ScadDocument`)    |
| `scad-csg-singular-transform`                                       | collapsed world transform of an operand      |

The `Module(name)` helper builds `scad-module-{name}` with underscores kept. This unit itself emits only the
`scad-module-*` identifiers of the eight unsupported modules and the two `scad-modifier-*` identifiers; the
other identifiers are emitted by the sibling units and by `ScadDocument`, and are defined here so that they live
in one place.

#### Key Methods

##### `Build(IReadOnlyList<ScadInstance> roots, ScadGeometrySettings settings, ScadEvaluationSink sink)`

The only entry point. It rejects `null` arguments, creates the context and reporter, then finds the show-only
roots. A model that contains `!` is just the marked subtrees; otherwise it is the union of the top-level
instances. It builds that forest, converts the result to the scene root, and returns the
`ScadBuiltScene`. The root is the solid itself, a group of **slabs** for a flat model (made by
`ScadExtrusions.MakeSlabs` with the configured `FlatShapeThickness`, each slab charged against the triangle
budget and mapped to the location of its 2D leaf), or an empty group. The builder is created per call, holds
only the per-build state, and is not thread-safe; independent builds can run concurrently.

##### `BuildForest(roots)` — the iterative engine

The engine is a loop over an explicit `List<Frame>`. A `Frame` holds the instance (or `null` for the top-level
forest), its children, the index of the next child, the results gathered so far (one per child, empty ones
included), and the **combiner** that turns the results into the result of the node. For each child the engine
ticks the context (the cancellation schedule), calls `Enter`, and either records the immediate result (a leaf,
an unsupported module, an omitted background subtree) or pushes a frame for the container's children. When a
frame has visited all its children it is popped, combined, and its result handed to the parent frame; the
result of the last frame is the result of the forest. Children of primitives, unsupported modules, and `%`
subtrees are never pushed.

##### `Guard(node, geo)` — the coordinate range

Every result is passed through `Guard` as soon as it is built (a leaf on entry, a container after its combiner;
the top-level forest needs none, because its children were checked). A non-empty result whose `Bounds` or `Reach`
is not finite (NaN or infinite, as when a finite matrix overflows) or has a coordinate above
`ScadGeo.MaxCoordinate` (1e100) in magnitude is replaced by `ScadGeo.Empty` and reported as
`scad-geometry-rejected` at the instance; only that instance is omitted. The limit lies far below the 1e154 at
which the squares of the camera fitting overflow and far above any real model. Because `Reach` includes the hidden
operands and every instance is checked, no mesh of the scene can lie beyond the limit, so `Scene.ComputeBounds`
is always usable. A scale whose determinant overflows is already rejected by `ScadTransforms` as a singular or
non-finite transform (a warning); the guard covers everything that stays finite there: translations, extreme
`multmatrix` entries, rotations and extrusions of large geometry, and 2D shapes. `ScadDocument.Render` additionally
falls back to the unit-cube framing (see `FitCamera`) for bounds that are unusable, so the fit cannot throw for any
scene the build returns.

##### `Enter(node, out immediate)` — dispatch

In this order: (1) a `#` instance reports `scad-modifier-highlight` and continues; (2) a `%` instance reports
`scad-modifier-background` and yields `ScadGeo.Empty` without visiting its children (an instance with both
marks reports both); (3) a **group** (a user module, `for`, `group`, or a modifier wrapper — the instance kind,
whatever its name) is the implicit union of its children, so a user module named like a built-in is a group and
**shadows** it; (4) a built-in name in the **container table** returns its combiner; (5) a name in the **leaf
table** builds in place; (6) anything else is an unsupported module. The `*` modifier needs no handling: the
evaluator removes a disabled statement before the instances are created.

The tables are `FrozenDictionary` instances keyed by ordinal module name:

| Table       | Modules                                                                                 |
|-------------|-----------------------------------------------------------------------------------------|
| Leaf        | `cube`, `sphere`, `cylinder`, `polyhedron`, `square`, `circle`, `polygon`               |
| Container   | `union`, `render` (the implicit union), `difference`, `intersection`, `translate`,      |
|             | `rotate`, `scale`, `mirror`, `multmatrix`, `resize`, `color`, `linear_extrude`,         |
|             | `rotate_extrude`                                                                        |
| Unsupported | every other recognized built-in: `text`, `offset`, `projection`, `surface`, `import`,   |
|             | `roof`, `hull`, `minkowski`                                                             |

`intersection_for` reaches the builder as the built-in `intersection` whose children are one group per iteration
(the evaluator's decision), so it needs no entry of its own.

##### `FindShowOnlyRoots(roots)` — the `!` pre-pass

An explicit-stack depth-first walk that returns the **outermost** instances marked `!` in document order. It
never descends into a marked subtree (a `!` nested inside has no further effect) nor into a `%` subtree (that
subtree is omitted, so a mark inside it does not take part in the model), and it visits every other instance
regardless of its module, including modules that are not built. The marked subtrees become the whole model
**without the transforms and colors of their ancestors**, because the engine builds them instead of the roots;
reports for ancestors and for the unmarked rest of the model are therefore not produced. Because the evaluator
ORs the modifiers of a one-statement block onto that statement, `%{ ! x }` is a single instance that is both the
`!` root and omitted: the pre-pass selects it, `Enter` then omits and reports it, and the model is empty (the
unmarked instances are not used as a fallback). Pinned by `ScadGeometryBuilder_Build_BackgroundBlockAroundSingleShowOnly_BuildsNothing`.

##### Modifiers

`!` is the pre-pass above. `#` builds the subtree normally and reports `scad-modifier-highlight` at the
instance. `%` omits the subtree and reports `scad-modifier-background` at the instance. All three degradations
are CanvasNetScad capability limits (reports), not errors; they can be removed by later work without changing
the engine.

##### `ScadBooleans` combiners

Every child instance is **one operand**: a group, a `for` loop, or a user module under a boolean is that
child's own implicit union, counted once. A solid operand is a scene node and the boolean becomes a `CsgNode`; a
2D operand is a `ScadShape2D` and the boolean becomes a `Csg` node of the shape tree that an extrusion later
distributes over. Transforms and colors stay on the operand or the boolean where they were written.

- **`Union`** (also the implicit union): empty results vanish; several solids become one `GroupNode` (a single
  solid is itself); 2D shapes become one union node of the shape tree. If both dimensions are present the 2D
  shapes are dropped with the warning `A union mixes 2D and 3D objects; the 2D objects are ignored` at the
  location of the first dropped shape.
- **`Difference`**: removes the later operands from the **first**, so an empty first operand (or no operand) makes
  the result empty, silently; empty later operands vanish; a single remaining operand is returned unchanged (no
  boolean node, exactness preserved); otherwise a `CsgNode` of operation `Difference` with the operands in order.
  Nested differences nest as nodes.
- **`Intersection`**: any empty operand, or no operand, makes the result empty (the intersection of anything with
  nothing is nothing); a single operand is returned unchanged.
- **Mixed dimensions** in a difference or an intersection: the first non-empty operand decides the dimension;
  the operands of the other dimension are dropped with the warning
  `{name} mixes 2D and 3D objects; the objects that differ in dimension from the first one are ignored` at the
  location of the boolean module. So a union keeps the 3D members, and difference and intersection keep the
  dimension of the first operand.
- **Bounds**: a union has the union of the operand boxes; a difference the first operand's box; an intersection
  the overlap of the boxes (an empty box when they do not overlap). A result is approximate when the operation
  is not a union or any operand is approximate, and tight only when all operands are tight and it is not
  approximate.

The combiners are stateless and thread-safe; the 2D shape tree is symbolic (see _ScadExtrusions Unit Design_ for
why 2D booleans are never evaluated into one region).

##### Triangle budget

The estimates are upper bounds charged **before** a mesh is allocated, by the unit that builds the mesh through
`ChargeTriangles` (for example four triangles per fragment for a cylinder, and four per vertex —
`ScadPrimitives.SlabTrianglesPerVertex` — for a 2D leaf and the slab it becomes). An estimate fits when it does
not exceed the remaining budget, so a model that uses exactly `MaxTriangles` builds. The budget accumulates
across all instances, including those of loops, and the check uses subtraction, so it cannot overflow. A hostile
`$fn` or loop therefore fails with a located error instead of exhausting memory.

##### Cancellation

`Tick()` is called for every instance entered and polls the token once every 256 instances; `Poll()` is called
explicitly before each heavy tessellation (a primitive, an extrusion, a slab). A model of fewer than 256
instances that needs no tessellation (for example an empty document) never polls and completes even with a
canceled token. The `OperationCanceledException` is never wrapped; the events reported before it stay in the
sink.

##### `ScadSceneOptions.Create(source, handler)` and `MapFeature(feature)`

`SceneRenderOptions` has no clone or `with` support, so `Create` copies every public property explicitly
(`BackgroundColor`, `DefaultColor`, `BackFaceColor`, `Lights`, `SpecularStrength`, `SpecularPower`,
`Supersampling`, `MaxDegreeOfParallelism`, `MaxFragmentsPerTile`) and **replaces** two: the
`UnsupportedFeatureHandler` becomes the supplied handler (or `null`) and `ThrowOnUnsupportedFeature` is always
`false`, because reporting and strict mode are decided by `ScadEvaluationOptions`, never by the nested scene
options. A `null` source yields a new instance with the Render3D defaults. A reflection test enumerates the
public properties of `SceneRenderOptions` and fails when one is not known to the test, so a property added to
Render3D later cannot be forgotten by the copy. `MapFeature` rewrites the prefix: `render3d-x` becomes `scad-x`
(so the six `render3d-csg-*` identifiers become the six `scad-csg-*` identifiers), and an identifier without the
prefix gets `scad-` prepended.

#### Error Handling

- **Warning** (through the reporter, into the shared sink): the mixed-dimension warnings above. The primitives,
  transforms, and extrusions report their invalid arguments the same way (see their unit designs).
- **Unsupported feature** (`ScadReporter.Unsupported`): `scad-module-{name}` with the message
  `The module '{name}' is not supported; it and its children are omitted`, `scad-modifier-highlight`
  (`The '#' modifier is not highlighted; the subtree is rendered normally`) and `scad-modifier-background`
  (`The '%' modifier is not supported; the subtree is omitted`), each at the instance's location (which names an
  included or used file when the instance came from one). The sink de-duplicates by feature and location, so a
  loop reports one module once. In **strict mode** the first report of a feature at a location throws a
  `ScadUnsupportedFeatureException` carrying the feature, message, and location (the report stays in the sink);
  a repeated report is silent.
- **One channel**: the reporter writes into the same `ScadEvaluationSink` the evaluation used, so events are
  ordered (evaluation, geometry, renderer), de-duplicated, capped like the evaluator's, and delivered once on
  the calling thread by the owner (`ScadDocument`).
- **`ScadEvaluationException`**: `Triangle limit exceeded (limit N)`, located at the instance that would exceed
  the budget.
- **`OperationCanceledException`**: never wrapped.
- **`ArgumentNullException`**: `Build` with a `null` argument.
- **Not handled here**: the renderer's `RenderBudgetExceededException` (the fragment budget of a tile) is mapped
  to a `ScadEvaluationException` by `ScadDocument.Render`, which also installs the renderer handler that uses
  `MapFeature` and the scene map, and only when a handler or strict mode exists, because the renderer validates
  operands only when a handler is present.

#### Dependencies

- **`ScadEvaluation` subsystem** — `ScadInstance`, `ScadInstanceKind`, `ScadModifiers`, and
  `ScadSourceLocation` (the instantiation tree); `ScadEvaluationSink` (the shared event buffer);
  `ScadLimits`; `ScadEvaluationException`; and `ScadUnsupportedFeatureException`.
- **`ScadPrimitives`, `ScadTransforms`, and `ScadExtrusions` units** — the leaf and container builders in the
  dispatch tables (`ScadPrimitives.*`, `ScadTransforms.*`, `ScadColors.Color`, `ScadExtrusions.*`), and
  `ScadExtrusions.MakeSlabs` for a flat model; `ScadShape2D` for the 2D shape tree.
- **`Render3D` public types** — `Scene`, `SceneNode`, `GroupNode`, `MeshNode`, `CsgNode`, `CsgOperation`,
  `BoundingBox3D`, `PolygonRegion`, and `Mesh` (scene model and tessellation), and `SceneRenderOptions`,
  `SceneRenderUnsupportedFeature`, and `Light` (scene rendering).
- **`CanvasNet` core types** — `Rgba32` (through the options copy).
- **.NET base class library** — `System.Collections.Frozen`, `System.Collections.Generic`, and
  `System.Threading` (`CancellationToken`).

The unit never depends on the `ScadLanguage` subsystem beyond the `ScadModifiers` flags carried by the
instances.

#### Callers

`ScadDocument` is the only production caller: `BuildScene` and `Render` evaluate the document into a fresh
sink, build `ScadGeometrySettings` from the effective options (`MaxTriangles`, `FlatShapeThickness`, strict
mode, and the token), and call `ScadGeometryBuilder.Build` with the evaluation's roots and the **same** sink;
`Render` then creates the renderer options with `ScadSceneOptions.Create` and uses `MapFeature` and
`ScadSceneMap.Find` in the renderer's handler. The tests in
`test/DemaConsulting.CanvasNet.Scad.Tests/Geometry/ScadGeometryBuilder*Tests.cs`, `ScadSceneOptionsTests.cs`, and
`ScadUnsupportedModuleTests.cs` call `Build` and `Create` directly through `InternalsVisibleTo`, through the
helpers of `ScadGeometryTestSupport` (which evaluates a program into a sink and builds it), and hand-built
`ScadInstance` trees for the depths the evaluator cannot reach.
