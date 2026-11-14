### ScadGeometryBuilder

<!-- cspell:ignore evaluator minuend subtrahend subtrahends minkowski multmatrix tessellation tessellator memoized -->
<!-- cspell:ignore coplanar -->

The `ScadGeometryBuilder` unit is the first software unit in the `ScadGeometry` subsystem. It comprises seventeen
source files in the `DemaConsulting.CanvasNet.Scad.Geometry` namespace, all `internal`:

- `ScadGeometryBuilder.cs` — `ScadGeometryBuilder` (sealed partial class): the entry point `Build`, the dimension
  of the model root, and the class-level documentation of the engine
- `ScadGeometryBuilder.Walk.cs` — the iterative post-order engine (`BuildForest`, `Frame`, `Enter`)
- `ScadGeometryBuilder.Modifiers.cs` — the `!` (show only) pre-pass `FindShowOnlyRoots` and the `#` and `%`
  overlays (`ApplyModifiers`, `EffectiveTriangles`)
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
- `ScadHulls.cs` — `ScadHulls`: the `hull` and `minkowski` combiners (also `ScadShape2D.IsUnionOnly` and
  `HasColor`, the iterative 2D tree checks it uses, live in `ScadShape2D.cs`)
- `ScadProjections.cs` — `ScadProjections` (static partial class): the `projection` combiner (`Projection`), the
  argument and budget checks, and the class-level documentation; part of this unit like `ScadHulls`, not a unit of
  its own
- `ScadProjections.Tree.cs` — the flattened solid tree (`FrameMesh`, `TreeNode`, `SolidTree`), the shared `State`
  (work meter, section cache, first failure; the tolerances belong to each `FrameMesh`), and `Collect`
- `ScadProjections.Faces.cs` — the face groups of a mesh (`GroupFaces`) and their XY footprints (`Footprint`)
- `ScadProjections.Slice.cs` — the cross-section of a mesh by a plane, with the infinitesimal shift as a tie rule
  (`SectionRegion`, `SliceMesh`, `Section`)
- `ScadProjections.Classify.cs` — the silhouette (`Silhouette`), the exact classification of boolean subtrees
  (`Classify`, `ClassifyGroup`, `SectionTree`), and the cut (`Cut`)

#### Purpose

`ScadGeometryBuilder` converts the geometry-agnostic instantiation tree produced by the evaluator (a list of
`ScadInstance` roots) into a Render3D `Scene`, and returns with it the map from the scene's mesh nodes back to
source locations. It owns what is common to every module: the walk, the implicit union, the modifiers, the CSG
booleans and the dimension rules, the `hull` and `minkowski` convex combinations, the `projection` shadow and
cross-section of solids, the table of modules that are
recognized but not built, the triangle budget,
cancellation, the single reporting channel, and the copy of the renderer options. What a particular primitive,
transform, color, text, or extrusion builds is delegated to the `ScadPrimitives`, `ScadTransforms`,
`ScadExtrusions`, and `ScadTextGeometry` units through the dispatch tables (see _ScadGeometry Subsystem Design_).

The central decision is that the engine is **iterative**: a recursive user module nests as deeply as
`ScadLimits.MaxRecursionDepth` allows (thousands of levels), so the engine walks the tree with an explicit stack
in post-order, like the renderer's CSG compiler, and **no code in the `Geometry` namespace recurses** (the
pre-pass, the 2D-tree walks, and the bounds propagation included), so the stack a build needs does not depend on
the depth of the tree. One level still runs a fixed chain of calls (down to the exact predicates of the convex
hull, whose arbitrary-precision arithmetic is large), and the runtime may compile code on the calling thread while
it runs; on .NET 11 with the framework compiled just in time, the two together exceeded a 64 KiB stack. `Build`
therefore runs inline only when `RuntimeHelpers.TryEnsureSufficientExecutionStack` reports the runtime's minimum
execution stack and otherwise on a dedicated thread with a 16 MiB stack and the caller's cultures, rethrowing any
exception unchanged (a thread that cannot be started is reported as a `ScadEvaluationException`), so the build
behaves identically on any caller stack, including one of 64 KiB. The semantics are original decisions of
the CanvasNetScad dialect (see _CanvasNetScad System Design_, Clean-Room Provenance); no OpenSCAD source or manual
text was consulted.

The unit's behavior is specified by the `CanvasNetScad-ScadGeometry-ScadGeometryBuilder-*` requirements
(`NoRecursion`, `ImplicitUnion`, `ShowOnly`, `ModifierBackground`, `ModifierHighlight`, `ModifierFlatEdge`,
`ModifierRobustness`, `UnsupportedModules`, `Reporting`,
`StrictMode`, `Difference`, `Intersection`, `BooleanOperands`, `MixedDimensions`, `FlatBooleans`,
`ResizeBounds`, `TriangleBudget`, `CoordinateRange`, `FlatSlabs`, `Cancellation`, `SceneMap`,
`SceneOptionsCopy`, `SceneOptionsMapping`, and the Phase 7 `Hull3D`, `Hull2D`, `HullDimensions`, `HullCsgOperand`,
`HullDegenerate`, `Minkowski`, `MinkowskiNonConvex`, `MinkowskiDimensions`, `MinkowskiLimits`,
`HullMinkowskiBudget`, `HullMinkowskiCancellation`, and `HullMinkowskiDeterminism`) in `docs/reqstream/canvas-net-scad/scad-geometry/scad-geometry-builder.yaml`.

#### Data Model

**`ScadGeometrySettings`** (record) holds the validated inputs of one build: `MaxTriangles` (a `long`, from
`ScadLimits.MaxTriangles`), `FlatShapeThickness` (the slab thickness of a top-level 2D shape),
`ThrowOnUnsupportedFeature` (strict mode), the `CancellationToken`, the optional `HighlightColor` and
`GhostColor` (`Rgba32?`; null means the defaults `(255, 64, 128, 110)` and `(128, 128, 128, 80)`, which the
context exposes as `HighlightColor` and `GhostColor`), and the optional `FontResolution`
(`ScadFontResolutionOptions?`; null means the defaults; the context creates the `ScadFontResolver` of the build
from it on first use), and the internal `MaxProjectionWork` (the work ceiling of one `projection`, in triangle visits;
the default is the documented 2^23, other values exist for tests). **`ScadBuiltScene`** (record) is the
outcome: the `Scene` (an empty group when nothing was built), the `ScadSceneMap`, the triangles charged (the
meshes created plus the copies of `#` overlays), and `IsFlatModel` (true when the result of the whole forest is a
2D shape, `ScadGeoKind.Flat`, so that a union that mixes 2D and 3D, which keeps its 3D members, is a 3D model; the
renderer entry point uses it to choose the flat top view).

**`ScadGeometryContext`** holds the services of one build on one thread: the `ScadReporter`, the
`ScadSceneMap`, the settings, the cancellation schedule, and the triangle counter. `Tick()` counts one visited
instance and polls cancellation once every 256 instances; `Poll()` polls immediately (before a heavy
tessellation); `EnsureTriangles(estimate, location)` checks that a count fits in the remaining budget without
charging it; `ChargeTriangles(estimate, location)` checks and then charges; `SettleTriangles(charged, actual,
location)` reconciles a charge with the actual mesh and `RefundTriangles(charged)` returns the charge of a mesh that
was not built; `CreateMeshNode(mesh, location)`
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
| `scad-module-offset`, `-surface`, `-import`                         | recognized module not built; omitted         |
| `scad-module-roof`                                                  | recognized module not built; omitted         |
| `scad-text-invalid-outline`, `-overlapping-contours`                | `text` glyph outline unusable; text omitted  |
| `scad-hull-csg-operand`, `-degenerate`, `-mixed-dimensions`         | `hull` with a boolean, flat points, 2D + 3D  |
| `scad-minkowski-nonconvex`, `-2d`, `-mixed-dimensions`              | non-convex operand, deferred 2D sum, 2D + 3D |
| `scad-minkowski-too-complex`                                        | work above the documented bounds             |
| `scad-projection-too-complex`, `scad-projection-open-mesh`          | work above the ceiling; open mesh; omitted   |
| `scad-modifier-highlight`                                           | `#` on a 2D shape: rendered normally         |
| `scad-modifier-background`                                          | `%` on a 2D shape: omitted                   |
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

The `Module(name)` helper builds `scad-module-{name}` with underscores kept. `scad-color-alpha` no longer exists
(Phase 8): the alpha of a color is rendered. The identifier `scad-module-projection` is retired: `projection` is
built, and reports `scad-projection-*` instead. This unit itself emits the
`scad-module-*` identifiers of the four unsupported modules, the two `scad-modifier-*` identifiers (for 2D shapes
only), the seven
`scad-hull-*` and `scad-minkowski-*` identifiers of `ScadHulls`, and the two `scad-projection-*` identifiers (and
`scad-geometry-rejected` for a section of a projection that the polygon validator rejects) of `ScadProjections`; the
other identifiers are emitted by the sibling units and by `ScadDocument`, and are defined here so that they live
in one place.

#### Key Methods

##### `Build(IReadOnlyList<ScadInstance> roots, ScadGeometrySettings settings, ScadEvaluationSink sink)`

The only entry point. It rejects `null` arguments, creates the context and reporter, then finds the show-only
roots. A model that contains `!` is just the marked subtrees; otherwise it is the union of the top-level
instances. It builds that forest, converts the result to the scene root, resolves the anchors of the **same**
forest (see _Anchors_; only when the evaluation set `ScadEvaluationSink.AnchorsDeclared`, so a model without
anchors does not pay for the extra walk), and returns the
`ScadBuiltScene`. The root is the solid itself, a group of **slabs** for a flat model (made by
`ScadExtrusions.MakeSlabs` with the configured `FlatShapeThickness`, each slab charged against the triangle
budget and mapped to the location of its 2D leaf), or an empty group. The builder is created per call, holds
only the per-build state, and is not thread-safe; independent builds can run concurrently.

##### `BuildForest(roots)` — the iterative engine

The engine is a loop over an explicit `List<Frame>`. A `Frame` holds the instance (or `null` for the top-level
forest), its children, the index of the next child, the results gathered so far (one per child, empty ones
included), and the **combiner** that turns the results into the result of the node. For each child the engine
ticks the context (the cancellation schedule), **skips an `Anchor` instance** (it is never a result, so it
cannot displace the first operand of a `difference`, and an anchor named like a module is not looked up in the
tables), calls `Enter`, and either records the immediate result (a leaf,
an unsupported module, an omitted background subtree) or pushes a frame for the container's children. When a
frame has visited all its children it is popped, combined, and its result handed to the parent frame; the
result of the last frame is the result of the forest. Children of primitives, unsupported modules, and `%`
subtrees are never pushed.

##### Anchors — `CollectAnchors(forest, settings)`

`ScadGeometryBuilder.Anchors.cs`. The evaluator leaves `Anchor` leaves in the tree but cannot place them (it has no
transform context), and the builder bakes each transform into the geometry results rather than keeping a matrix
per node, so the world point is computed by a separate **iterative pre-order pass** over the same forest, with an
explicit stack of `(instance, accumulated matrix of the parent, blocker, dropped)`:

- A matrix transform (`translate`, `rotate`, `scale`, `mirror`, `multmatrix`) contributes the matrix of
  `ScadTransforms.TryGetMatrix` (the single definition the builders use too; see _ScadTransforms_), multiplied
  **in front of** the parent's (row-vector convention: the instance's own matrix applies first, so the
  innermost transform is applied first). The matrix helpers report to a throw-away silent context, so a bad
  transform is not warned about a second time; the builder's own warning stays the only one.
- A group (a user module, `for`, `group`, an `intersection_for` iteration) and the containers `union`, `render`,
  `difference`, `intersection`, `color`, `hull`, and `minkowski` pass the matrix through unchanged. The modifiers
  `#` and `%` change nothing.
- A matrix transform whose `TryGetMatrix` fails, or whose matrix collapses **both** solid geometry
  (`ScadTransforms.Collapses`) and 2D shapes (`ScadTransforms.CollapsesPlanar`, the in-plane rule of `ApplyFlat`),
  drops its whole subtree **silently**: its geometry is dropped and already reported. An anchor cannot know whether
  its siblings are 2D or solid, so a matrix that collapses only one of them (`scale([1, 1, 0])`,
  `rotate([90, 0, 0])` for a 2D shape) keeps the anchor, projected by the matrix.
- Any other module (`resize`, `linear_extrude`, `rotate_extrude`, `projection`, `offset`, a primitive with children,
  and so on) marks the subtree with a **blocker**: each anchor below it is skipped with one
  `scad-anchor-invalid` warning that names the module, because its coordinates are changed in a way the pass cannot
  follow (for `resize` the factor depends on the bounds of the children; an extrusion and `projection` change the
  dimension).
- An anchor resolves to `matrix.TransformPoint(vector)`; a result that is not finite or exceeds `ScadGeo.MaxCoordinate`
  is skipped with the same warning. The anchors are returned in document order, which is the order of the echo
  lines, duplicates included, as `ScadWorldAnchor(Name, Point, Location)` in `ScadBuiltScene.Anchors`.

Because the pass runs over the forest the build used, a model with `!` reports only the anchors of the marked
subtrees, and without the transforms of their ancestors, exactly like the geometry. The pass ticks the context
for cancellation like every walk of the builder.

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

In this order: (1) a **group** (a user module, `for`, `group`, or a modifier wrapper — the instance kind,
whatever its name) is the implicit union of its children, so a user module named like a built-in is a group and
**shadows** it; (2) a built-in name in the **container table** returns its combiner; (3) a name in the **leaf
table** builds in place; (4) anything else is an unsupported module. The modifiers `#` and `%` do **not** change how
an instance is entered (its children are visited and everything inside, unsupported modules included, is
reported): `ApplyModifiers` turns the finished result into an overlay afterwards (see _Modifiers_). The `*`
modifier needs no handling: the
evaluator removes a disabled statement before the instances are created.

The tables are `FrozenDictionary` instances keyed by ordinal module name:

| Table       | Modules                                                                                 |
|-------------|-----------------------------------------------------------------------------------------|
| Leaf        | `cube`, `sphere`, `cylinder`, `polyhedron`, `square`, `circle`, `polygon`, `text`       |
| Container   | `union`, `render` (the implicit union), `difference`, `intersection`, `translate`,      |
|             | `rotate`, `scale`, `mirror`, `multmatrix`, `resize`, `color`, `linear_extrude`,         |
|             | `rotate_extrude`, `hull`, `minkowski`, `projection`                                     |
| Unsupported | every other recognized built-in: `offset`, `surface`, `import`, `roof`                  |

`intersection_for` reaches the builder as the built-in `intersection` whose children are one group per iteration
(the evaluator's decision), so it needs no entry of its own.

##### `FindShowOnlyRoots(roots, context)` — the `!` pre-pass

An explicit-stack depth-first walk that returns the **outermost** instances marked `!` in document order. It
calls `context.Tick()` for every instance it visits, so cancellation is polled on the same fixed schedule as the
rest of the build and a canceled build stops during the pre-pass of a large model. It
never descends into a marked subtree (a `!` nested inside has no further effect) nor into a `%` subtree (that
subtree is a ghost, not part of the model, so a mark inside it does not take part in the model), and it visits
every other instance
regardless of its module, including modules that are not built. The marked subtrees become the whole model
**without the transforms and colors of their ancestors**: the marked subtree is the **new root of the design**, as if
the model consisted of it alone. So `translate([10, 0, 0]) { !cube(1); }` renders the cube at the origin, and a
mark under a `color` or a `rotate` is neither colored nor rotated. This holds because the engine builds the marked
subtrees instead of the roots;
reports for ancestors and for the unmarked rest of the model are therefore not produced. Because the evaluator
ORs the modifiers of a one-statement block onto that statement, `%{ ! x }` is a single instance that is both the
`!` root and a ghost: the pre-pass selects it, `ApplyModifiers` makes it a ghost, and the model has no ordinary
geometry (the unmarked instances are not used as a fallback). Pinned by `ScadGeometryBuilder_Build_BackgroundBlockAroundSingleShowOnly_BuildsOnlyTheGhost`
and, for the new-root rule, by `ScadGeometryBuilder_Build_ShowOnly_IsTheNewRootWithoutAncestorTransformsOrColors`.

##### Modifiers

`!` is the pre-pass above. `#` and `%` are applied by `ApplyModifiers(node, geo)` to the finished and guarded
result of an instance (a leaf in place, a container after its combiner), in both walk paths; a result that is
`Empty` is left alone. The mechanism is the Render3D `OverlayNode` (an independent translucent body that is
**false** in every enclosing boolean and is composited by depth with the rest of the scene; see the Render3D
_SceneGraph_ design):

- **`%` (ghost).** A solid becomes `OverlayNode(GhostColor, [geo.Node])`. It contributes **no propagated bounds**
  (`Bounds` is the empty box, tight) but keeps its `Reach`, so `resize`, `hull`, `minkowski`, and the bounds of
  enclosing geometry ignore it while the coordinate guard still sees its vertices. It does not add to a
  union, does not subtract from a difference, makes an intersection with it empty, and as the **first**
  operand of a `difference` it leaves the difference without ordinary geometry (the ghost is still drawn).
  It is, however, part of the scene bounds that fit the default camera (a documented choice:
  `Scene.ComputeBounds()` includes overlays, so the ghost cannot fall out of the frame); the propagated
  bounds are separate from the scene bounds, and `resize` measures a non-tight solid with
  `ComputeBounds(includeOverlays: false)`. `#` and `%` on the same instance: the background wins.
- **`#` (highlight).** A solid becomes `Group[geo.Node, OverlayNode(HighlightColor, [geo.Node])]`: the **same node**
  is the normal geometry and the body of the overlay, so no mesh is duplicated, the bounds and reach are
  unchanged, and the subtree takes part in the booleans around it exactly as if the modifier were absent (a
  highlighted subtrahend still subtracts, and its translucent body is drawn through the cut). The renderer draws
  every path to a mesh, so the **triangles of the subtree are charged to the budget a second time**
  (`EffectiveTriangles`, an iterative, memoized count of the triangles per path); this also bounds nested
  highlights, whose paths would double with every level, by `ScadLimits.MaxTriangles` (a failure is the usual
  located `ScadEvaluationException`).
- **2D shapes.** A result that is still flat has no overlay form (the overlay is a body of solids): a `#` shape is
  rendered normally and reported as `scad-modifier-highlight`, a `%` shape is omitted and reported as
  `scad-modifier-background`, each at the instance. The modifier on an extrusion of the shape (a solid) works
  completely.
- **Hull and Minkowski.** The builder counts the `hull` and `minkowski` frames it is inside (`_convexDepth`,
  incremented when such a frame is pushed and decremented when it is popped, so the modifiers of the instance itself
  are applied outside it). While the count is positive, `ApplyModifiers` creates no overlay and charges nothing: `#`
  returns the solid unchanged (an ordinary member of the hull or sum) and `%` returns `Empty` (a ghost is false in
  every boolean and is never drawn there), at any depth below the container, for a lone child as well as for several.
  A hull or sum of only ghosts therefore builds nothing and reports nothing. A `#` outside any hull or minkowski
  keeps its charge, so nested highlights stay bounded. The point collectors of `ScadHulls` and the
  appearance-stripping copy still ignore an overlay (an empty group) as a safety net.
- **Closed solids.** Overlays are resolved with signed crossing counters, so the meshes beneath them must be
  closed solids; an open mesh under `#` or `%` is reported by the renderer's operand check through the
  `scad-csg-*` identifiers like any boolean operand.

All of this is pinned by `ScadGeometryBuilderModifierTests`. The modifiers are applied without recursion.

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

##### `ScadHulls` combiners (`hull` and `minkowski`)

Both modules take their children's results with the empty ones dropped; no child left gives an empty result and
no report. Children of both dimensions are reported (`scad-hull-mixed-dimensions`,
`scad-minkowski-mixed-dimensions`) and the module yields nothing. The `convexity` argument and any other
argument are ignored and not validated. A color inside either module is dropped with one warning (the result
takes the enclosing `color`).

**Collecting the points.** A solid result is a scene tree: `MeshNode`, `GroupNode` (a union, or a transform
wrapper with the matrix in `Transform`), and `CsgNode`. The walk is an explicit stack that accumulates the world
matrix of each node (`node.Transform * parent`, the row-vector convention), polls cancellation per node
(`context.Tick()`), and stops at the first `CsgNode` that is not a union (the operation of every non-union boolean
makes the hull of its vertices wrong, so it is reported, never approximated). Only the vertices that a triangle
references are used.

**`hull` of solids.** `ConvexHull.Hull3DInPlace` of the transformed points after `context.Poll()`. `GatherPoints` flags
and
counts the referenced vertices of every mesh first and only then allocates **one** exactly sized `Vector3D[]` and fills
it, so there is no growing list and no second copy (there is no guard on the raw number of vertex slots: children can
share positions and unshared meshes reference more slots than triangles, so the budget decision is made only on the
distinct points, by `ChargeHullBound`):
the array is handed to `Hull3DInPlace`, which sorts and de-duplicates it in place; an `ArgumentException` (fewer than four
affinely independent points) is `scad-hull-degenerate`. The charge **precedes** the computation: the mesh has at most
`2d − 4` triangles for `d` distinct points, so `ChargeHullBound` charges that bound (the raw point count `n` when it
fits the remaining budget, the distinct count only when it does not, because children can share positions) before
`Hull3DInPlace` allocates anything, and a bound that does not fit the **remaining** budget fails with the normal
located `ScadEvaluationException` without the hull being begun (the bound can reject a hull that would fit exactly).
After the mesh is built `SettleTriangles` refunds the difference to its actual `2V − 4` triangles (`V ≤ d`), and a
degenerate hull, a cancellation or any other failure refunds the whole pre-charge, so the total charged equals the
triangles of the meshes created. The node is then created by `CreateMeshNode` at the hull's location. The result is
a closed, outward-wound mesh of the strictly extreme vertices, a valid operand for later booleans. The 2D hull
charges nothing here (see below).

**`hull` of flat shapes.** The 2D trees must be union-only (`ScadShape2D.IsUnionOnly`, iterative), else
`scad-hull-csg-operand`. The points are the vertices of every leaf region taken through the planar matrix of
`EnumerateLeaves`; `ConvexHull.Hull2D` gives the strictly convex counter-clockwise polygon, fewer than three
points are `scad-hull-degenerate`, and the polygon becomes a `PolygonRegion` flat result at the hull's location
(an `ArgumentException` of the region is `scad-geometry-rejected`). No triangles are charged: the region is
extruded, or made into a slab, by the unit that charges its exact triangles.

**`minkowski`.** All flat children are `scad-minkowski-2d` (deferred). Each solid child reduces to its convex
members: the meshes of its union-only scene tree, each brought into the module's frame with `Mesh.Transform`
(which reverses the winding of a mirroring transform); a boolean is `scad-minkowski-nonconvex`, and a child with
more than `MaxMinkowskiCombinations` (4,096) members is `scad-minkowski-too-complex`. The number of sums of each
fold step is the running product of the members counts, so a step of more than 4,096 sums is
`scad-minkowski-too-complex` and is decided before any member is verified. **Every member is then verified once**
(`MinkowskiSum.RequireConvexSolid`, one hull of the member's vertices; see _MinkowskiSum Unit Design_ in the
Render3D _Tessellation_ subsystem): a member that is not provably a closed convex solid is
`scad-minkowski-nonconvex`, and a member without volume is `scad-geometry-rejected`. The sum is folded from the
left. Because `(A ∪ B) ⊕ C = (A ⊕ C) ∪ (B ⊕ C)`, the members of the accumulated result are summed pairwise with the
members of the next child with `MinkowskiSum.ConvexSumOfVerified`, which skips the repeated verification (the
operands are verified members or earlier sums, which are hulls and convex by construction). A pair of meshes
above `MinkowskiSum.MaxPairwiseSums` pairwise vertex sums (decided on the distinct referenced vertices, only when
the raw counts are over the bound) is `scad-minkowski-too-complex`.

The messages are written for the SCAD author and never embed the text of a Render3D exception: a failing child is
named by its position among the children and its source location (`minkowski: child 2 (at 5:7) is not a closed
convex solid; only convex operands are supported and the minkowski is omitted`), and a degenerate `hull` says that
the points are fewer than four, collinear, or coplanar. The module is all-or-nothing: the first report makes the
whole `minkowski` empty, and one report is made.

**Cumulative work cap.** The bounds per step and per pair do not bound the total, so one `WorkMeter` per module
adds up the work in pairwise vertex sums: each member verification costs its vertex count and each sum costs the
product of its operands' vertex counts (distinct referenced vertices when the raw product is over the per-pair
bound). A total above `MaxMinkowskiWork` (2^21 = 2,097,152, the bound of one maximal pair, so a module never does
more pair work than one maximal sum) is reported as
`scad-minkowski-too-complex` ("the children need more than 2097152 pairwise vertex sums in total") before the
work that would exceed it is done.

**Lone child.** A lone convex child is verified as a closed convex solid in its own frame and returned with its
geometry and transforms unchanged, with **the colors stripped** (a warning is reported), and with no Minkowski sum
computed. No transformed copy is made, and nothing is created or charged for the verification, so a valid child
never fails on the budget because of the test. When the child scene carries an appearance (a `color` inside the
module), the child scene is copied without its appearances so that an enclosing `color` applies to the result. The copy
(`StripAppearances`) is an iterative post-order depth-first walk: a node is copied only after all its children,
and a node reached through several parents is copied once, so shared subtrees stay shared and the walk is linear
in nodes plus edges; a child without any appearance is returned as it is. The result is one `MeshNode` or a
`GroupNode` of mesh nodes (one per final combination), each created with `CreateMeshNode` after its exact charge.

**Verification as built, transformed copies and distinct counts.** A member is a mesh as built with the matrix that takes
it into the frame of the module. Convexity (a closed convex solid) is invariant under a non-singular affine map, and
every transform of the scene is one (`ScadTransforms` reports a singular or non-finite matrix and builds nothing),
while a mirror only reverses the winding, which `Mesh.Transform` restores; so each member is verified **as built**,
with no copy, and the test needs no budget. The relative tolerance of the test is then measured in the member's own
frame (the volume deviation is affine invariant, the area deviation changes only under extreme non-uniform scales).
A member whose matrix is not the identity is copied by `Mesh.Transform` only when a sum uses it: `MemberStore.InFrame`
makes the copies of a child when the fold reaches it (children 0 and 1 for the first step, each later child for its
step), charges their triangles (the same as the originals) **before** allocating each copy (a `ScadEvaluationException`
when they do not fit the remaining budget) and records the charge per child. The copies of a child are refunded when
the fold has consumed them (children 0 and 1 after the first step, each later child after its step), and whatever
remains when the module ends in any way (the `finally` of `MinkowskiSolids`), so the charge always equals the
triangles of the meshes that are alive; a lone child makes none. The same store holds the number of distinct referenced
vertices of each member, scanned **once**
when the member is verified (its cost is covered by the verification charge) and reused by every pair, so the cost of
a pair is `Va * Vb` from cached counts (a sum, which is a welded hull, uses its vertex count) instead of a rescan of
both meshes per pair.

**Budget.** Each sum is pre-charged with its upper bound, `2 * (Va * Vb) − 4` triangles (the hull of the `Va * Vb`
pairwise-sum points, `Va * Vb` being the cached distinct counts that the work meter uses), **before** `ConvexSumOfVerified`
allocates its sums array; a bound above the remaining budget fails with the located triangle-limit error without
the sum being begun, and after the sum is built the charge is settled to its actual triangle count (the node is
created afterwards) while a failure or cancellation refunds the whole pre-charge. The sums of every fold step but the
last are scene-less temporaries: they are charged (at their actual count) while they live, so the memory of a fold stays
inside the budget, and refunded when the next step has consumed them; the peak is therefore the operands, the live
sums of the previous step and the bound of the sum in progress. On any failure everything not turned into a node is
refunded. The operand meshes keep the charge they received when they
were created, so the total charged equals the triangles of every mesh created (pinned by
`ScadGeometryBuilder_HullAndMinkowski_ChargeExactlyTheTrianglesCreated`). The convexity-verification hulls for
Minkowski members and the transient distinct-point set used by the hull budget check are temporary working storage
bounded by the size of operand meshes that are already charged, and are not separately charged against the triangle
budget; precise accounting of transient storage is a planned hardening follow-up.

**Cancellation, determinism, stack.** `context.Poll()` runs before each hull, each sum, and between the meshes
gathered, and `context.Cancellation` is passed into the Render3D loops. The point and member order is document
order, the Render3D results depend on point sets only, and nothing is enumerated from a hash order, so the same
model gives the same meshes. Every walk is iterative.

##### `ScadProjections.Projection` (`projection`)

`projection(cut = false | true)` turns the 3D children into a 2D result: the shadow of the solid on the XY plane, or
its cross-section at `z = 0`. It is a container builder like `hull`, part of this unit (the class
`ScadProjections` is not a software unit of its own), and everything it builds is a symbolic `ScadShape2D`, so the
package keeps having no 2D or mesh boolean kernel and every consumer of a 2D shape (2D transforms, `color`,
`linear_extrude`, `rotate_extrude`, `hull`, 2D booleans, and the slabs of a top-level 2D shape) accepts the result.
The result has kind `Flat`, the location of the module, and the bounds of its vertices (tight when the shape is
union-only and nothing was classified; an upper bound otherwise).

**Arguments and children.** The arguments are bound with the names `cut` and `convexity`. `cut` is honored when it is
a boolean; any other value warns (`projection: 'cut' must be true or false; false is used`) and counts as `false`.
`convexity` is accepted and ignored. 2D children are ignored with a warning (`projection: only 3D children can be
projected; the 2D children are ignored`); with no 3D child left the result is empty and silent. The children are
the union of the solid results. `%` overlays (`OverlayNode`) are skipped, and a `#` highlight is projected as the real
subtree only, never twice (the overlay copy is skipped). Colors inside the projection are not kept: one warning says
that the result takes the enclosing `color`. The identifier `scad-module-projection` is retired.

**Flattening.** The scene trees of the children are walked iteratively (no recursion; `context.Tick()` per node)
into a flat list of nodes: a mesh node, or a union, difference, or intersection of child nodes, each with the world
matrix accumulated as `node.Transform * parent`. A mesh is put into the frame of the projection (`FrameMesh`): the
vertices that triangles reference are transformed and **welded by exact position**, a triangle that collapsed to
fewer than three distinct vertices is dropped, and the winding is restored after a mirroring matrix. A mesh is a
_closed outward solid_ when it is closed and has a positive signed volume. Every node knows whether it is subtracted
an odd number of times (a cutter of the solid). The tolerances do **not** depend on the distance of the model from the
origin, and no tolerance is shared between meshes: each mesh knows its _extent_ (the largest side of its bounds, which
a translation does not change) and the spacing of the doubles at its largest absolute coordinate
(`Math.BitIncrement(m) - m`, the unavoidable representation error). The tolerance of a mesh for "on a plane" is `1e-9`
times **its own** extent plus 16 spacings, so the distance of two things must exceed the size-relative part and the
rounding of the coordinates to count as different; the doubled XY area below which a triangle counts as vertical is
`1e-12` times the square of the extent of its own mesh plus 16 spacings times that extent. There is no absolute floor,
so a tiny part is not treated as degenerate and a vertex a small distance above the plane is not on it, whether or not
a solid of size 1e6 is next to it; an empty mesh (no extent) uses a nominal `1e-9`. A mesh is always judged by its own
tolerance: its vertices against a plane (the plane may be a face of another mesh), its triangles against each other,
and its cross-section cache key. The only value that involves two meshes is the padding of the XY window of a face
when a section of another mesh is compared with it, which is the **sum** of the two tolerances (it only keeps more
sections, never fewer). A consequence is that two meshes of very different size may disagree about whether a vertex is
on a plane that lies between their tolerances; each is right at its own resolution, and exactly coincident faces
(within rounding) are on the plane for both. The projection builds a symbolic tree of exact leaf regions, so no common
snapping grid is needed.

**Silhouette (`cut = false`).** Projection distributes over a union, so the node tree is walked from its root and
a mesh or a union contributes directly, and each maximal subtree rooted at a difference or an intersection is
classified exactly (below).

- _Faces._ For a closed outward solid only the upward faces (triangles with a positive Z component of the normal)
  can be on the shadow boundary; every non-vertical triangle of any other mesh is used, because the shadow of the
  back faces of an open mesh counts too. Vertical triangles have no footprint and are skipped.
- _Merging._ The wanted triangles of one mesh are joined into groups by union-find over shared edges when their
  normals agree (cosine at least `1 - 1e-12`) and one lies in the plane of the other within the tolerance. The
  footprint of a group is **one region**: the directed edges that have no reverse edge in the group are the boundary,
  chained into closed contours (the even-odd nesting of `PolygonRegion` makes holes), so the top of a cube is one
  rectangle and the cap of a `$fn = 64` cylinder is one polygon. When the boundary is ambiguous (a vertex where
  two contours touch), does not close, or the region is rejected, or the group is a single triangle, the footprint
  falls back to **one region per triangle**, which covers the same area.
- _Result._ A union-only tree is the union of the footprints as leaf regions; one leaf is the shape itself.

**Classification of a difference or an intersection.** The shadow of a boolean is not the shadow of its operands, so
for every face group of every mesh below the subtree root (the upward faces of the added operands, the downward faces
of the subtracted ones, every non-vertical face of a mesh that is not a closed outward solid) the part of its
footprint that bounds the solid from above is `face AND below AND NOT above`, where _below_ is the section of the
whole subtree by the plane of the face shifted infinitesimally down (the solid is just below the face) and _above_ the
section shifted infinitesimally up (the solid is just above it). The silhouette of the subtree is the union of these
parts. Slicing commutes with set operations, so a section of the subtree is the same boolean tree over the sections
of its meshes.

- _The infinitesimal shift is a tie rule, not a number._ A vertex within the tolerance of the plane counts as **above**
  the plane for the down shift and as **below** it for the up shift; every other vertex is classified by the sign of
  its distance. A triangle with vertices on both sides contributes one directed segment (from the cut edge that leaves
  the upper side to the one that enters it), which orients every contour consistently. The points
  of the contours are keyed by the mesh edge that was cut, or by the vertex when the edge ends on the plane (the
  point is then that vertex), **never by coordinates**, so the contours close exactly whatever the rounding. The
  contours of a mesh become one `PolygonRegion`. Sections are cached by node, plane, and direction (planes are
  identified by rounded normal and offset in units of the tolerance of the mesh that is sliced, so coplanar faces share
  their sections). A mesh whose bounds do not reach the plane is tested **before** the key is made, because the offset
  of a plane of a far or huge mesh in units of the tolerance of a tiny one would overflow the key; a plane that reaches
  a mesh has an offset within about its own coordinates, so the key stays small.
- _Window pruning and full sections._ A section matters only inside the bounding box (the window) of the footprint of
  the face. A mesh whose bounds do not reach the plane, or whose section lies outside the window, is empty. A section
  of the mesh the face belongs to covers the whole footprint on the side of its solid (below an upward face, above a
  downward one): it is **full**, and costs nothing. Another section that is one convex contour is clipped to the
  (slightly padded) window, and one that covers the window is full too (the padding is the sum of the tolerances of the
  two meshes). A face that nothing else touches therefore
  yields just its footprint and no boolean at all. The booleans combine empty, full, and shape operands without
  creating shapes where the answer is already known (an empty operand vanishes from a union and empties an
  intersection; a full operand makes a union full, vanishes from an intersection, and empties a difference when it is
  subtracted).
- _Worked checks._ A tube (a cylinder minus a taller or equal one) is an annulus, a plate with a through hole has the
  hole, and a blind pocket has no hole in its shadow.

**Cut (`cut = true`).** The children are sliced at `z = 0` and the boolean structure is kept symbolically: the section
of a difference is the difference of the sections. A face that lies in the plane belongs to the section (closed-set
semantics): when any vertex of any mesh is within the tolerance of the plane, the section is the **union of the
section just below and the section just above** the plane; otherwise one section suffices. The union of the two is
the documented behavior of this package, and is a **known divergence** from backends that treat vertices on the
plane asymmetrically; whether another implementation (for example the Manifold backend of OpenSCAD) does so was not
verified, so the behavior is documented, not asserted to match. A plane that misses every solid gives an empty,
silent result.

**Budget and cost.** Everything is bounded before it is allocated:

1. The triangles **and the vertices** of the children are counted first (an iterative walk that skips overlays). A
   total above the work ceiling (a settings member, `ScadGeometryContext.MaxProjectionWork`; the default is
   `ScadProjections.MaxProjectionWork`, 2^23, and other values exist only for tests) is `scad-projection-too-complex`,
   the module is omitted, and nothing was allocated. The vertices count because the per-vertex storage of the
   transformation and the lazy closedness test of the Render3D mesh (`Mesh.IsClosed`, which welds every vertex of
   the mesh) are sized by the vertices of the mesh, not by the triangles that use them; a polyhedron with a huge
   point list and one face is bounded by that charge. The vertex remap of `FrameMesh.Create` is itself a dictionary
   over the **referenced** vertices, so its storage is at most three times the triangles.
2. `context.EnsureTriangles(8 * candidates)` then checks that the slabs the footprints could need (8 triangles per
   candidate triangle, an upper bound) fit the triangle budget, with the located triangle-limit
   `ScadEvaluationException` otherwise, before any region is built. The check is repeated for the leaves created so
   far after the footprints of each mesh, after each classified face group, and after the cut (8 triangles per leaf).
3. Every pass charges a **work meter** (`State.Charge`) _before_ it runs, in triangle visits: the children
   (triangles plus vertices, charged before they are transformed), the scan of `GroupFaces` (the triangles of the
   mesh), its merge and collection (twice the wanted triangles), the footprint of a group (twice its triangles for the
   merged region, plus its triangles for the per-triangle fallback), every slice (the triangles and vertices of the
   mesh), and every walk of a section tree (the number of nodes of the subtree). The constants are one visit per
   triangle per pass: an upper-bound proxy, not a profile. A total above the ceiling is `scad-projection-too-complex`,
   the pass does not run, no leaf is built from a partial pass, and the projection is omitted, **never approximated**.
   Under the default limits an ordinary projection stays below the ceiling (the slab check of step 2 limits the
   candidates to an eighth of the triangle budget, and a plain silhouette costs about eight visits per candidate);
   the ceiling fires when `MaxTriangles` is raised. A slice and a section walk also poll cancellation as they charge,
   so a canceled build stops per slice. Between the charges the long loops poll with
   `context.Tick()` (the same 256-step schedule): the transformation of the triangles and vertices of each mesh
   (`FrameMesh.Create`), the grouping, merging, and collecting of the faces, the footprint of a group (the edge
   passes, the chaining of the contour, and the triangle fallback), the walk of the nodes of a subtree (`Descendants`)
   and the node loop of a section (`SectionTree`), and the slicing of a mesh (its vertices and
   triangles, the segments of the section, and the walk of its contours), so a mesh with one huge flat face can still be
   abandoned.
4. The transformed vertex copies and the section caches are transient storage bounded by the size of meshes that
   are already charged (the welded vertices of a `FrameMesh` are the referenced ones only, at most three times its
   triangles); like the transient storage of `ScadHulls` they are not charged separately.

**Failure modes.** A section that does not close (the segments do not balance at a point) means that the mesh is not a
closed solid: `scad-projection-open-mesh`. A section that the polygon validator rejects (contours that touch or
cross) is `scad-geometry-rejected`. Both, and the too-complex failure, omit the **whole** projection, because
dropping an operand of a boolean would silently change its meaning; the first failure is the one reported. An
open mesh in a union-only tree is not an error: its triangles are projected one by one. The methods are stateless
and thread-safe, everything iterates over explicit stacks (so the 64 KiB-stack guarantee of the engine holds), and
the result depends only on the geometry, in document order.

##### Triangle budget

The charges are the **exact** triangle counts of the meshes, computed from their parameters and charged **before**
a mesh is allocated, by the unit that builds the mesh through `ChargeTriangles` (for example four triangles per
fragment for a cylinder, two for a cone, and `2V + 2(V + 2H − 2P)` for a slab or `V + 2H − 2P` per cap of an
extrusion of a profile with `V` vertices, `H` holes and `P` solid contours). When the mesh exists,
`SettleTriangles` reconciles the charge with its actual triangle count and `RefundTriangles` returns the charge of a
mesh that was rejected, so the total charged equals the triangles created (pinned by
`ScadGeometryBuilder_Build_ChargesExactlyTheTrianglesCreated`). A 2D leaf only checks a lower bound of its slab
(`ScadPrimitives.SlabTrianglesLowerBound`) when it is created. A count fits when it does
not exceed the remaining budget, so a model that uses exactly `MaxTriangles` builds. The budget accumulates
across all instances, including those of loops, and the check uses subtraction, so it cannot overflow. A hostile
`$fn` or loop therefore fails with a located error instead of exhausting memory.

##### Cancellation

`Tick()` is called for every instance entered and polls the token once every 256 instances; `Poll()` is called
explicitly before each heavy tessellation (a primitive, an extrusion, a slab) and before each hull and convex sum,
and the work meter of a projection polls it before every slice; the long loops of a projection (see _Budget and cost_)
call `Tick()` per triangle, vertex, or contour point.
A model of fewer than 256
instances that needs no tessellation (for example an empty document) never polls and completes even with a
canceled token. The `OperationCanceledException` is never wrapped; the events reported before it stay in the
sink.

##### `ScadSceneOptions.Create(source, handler, flat)` and `MapFeature(feature)`

`SceneRenderOptions` has no clone or `with` support, so `Create` copies every public property explicitly
(`BackgroundColor`, `DefaultColor`, `BackFaceColor`, `Lights`, `SpecularStrength`, `SpecularPower`,
`Supersampling`, `MaxDegreeOfParallelism`, `MaxFragmentsPerTile`) and **replaces** two: the
`UnsupportedFeatureHandler` becomes the supplied handler (or `null`) and `ThrowOnUnsupportedFeature` is always
`false`, because reporting and strict mode are decided by `ScadEvaluationOptions`, never by the nested scene
options. When `flat` is `true` (a model that is entirely 2D drawn with the flat default view; the default is
`false`) and the caller set no `Lights`, the copy is lit by a single ambient light of intensity 1, so that a face
shows exactly its own color; lights the caller set are always kept. A `null` source yields a new instance with the
Render3D defaults. A reflection test enumerates the
public properties of `SceneRenderOptions` and fails when one is not known to the test, so a property added to
Render3D later cannot be forgotten by the copy. `MapFeature` rewrites the prefix: `render3d-x` becomes `scad-x`
(so the six `render3d-csg-*` identifiers become the six `scad-csg-*` identifiers), and an identifier without the
prefix gets `scad-` prepended.

#### Error Handling

- **Warning** (through the reporter, into the shared sink): the mixed-dimension warnings above and the warning
  that colors inside a `hull`, a `minkowski`, or a `projection` are not kept, and the warnings of `projection` (a
  `cut` that is not a boolean, 2D children that are ignored). The primitives,
  transforms, and extrusions report their invalid arguments the same way (see their unit designs).
- **Unsupported feature** (`ScadReporter.Unsupported`): `scad-module-{name}` with the message
  `The module '{name}' is not supported; it and its children are omitted`, `scad-modifier-highlight`
  (`The '#' modifier on a 2D shape is not highlighted; the shape is rendered normally (extrude it to draw a
  highlight)`), `scad-modifier-background`
  (`The '%' modifier on a 2D shape is not supported; the shape is omitted (extrude it to draw a ghost)`), the
  `scad-hull-*` and `scad-minkowski-*`
  identifiers of `ScadHulls` (each message names the module, the cause, and that the module is omitted), and the
  `scad-projection-too-complex` and `scad-projection-open-mesh` identifiers of `ScadProjections` (and
  `scad-geometry-rejected` for a section the polygon validator rejects; each message says that the projection is
  omitted), each at
  the instance's location (which names an
  included or used file when the instance came from one). The sink de-duplicates by feature and location, so a
  loop reports one module once. In **strict mode** the first report of a feature at a location throws a
  `ScadUnsupportedFeatureException` carrying the feature, message, and location (the report stays in the sink);
  a repeated report is silent.
- **One channel**: the reporter writes into the same `ScadEvaluationSink` the evaluation used, so events are
  ordered (evaluation, geometry, renderer), de-duplicated, capped like the evaluator's, and delivered once on
  the calling thread by the owner (`ScadDocument`).
- **`ScadEvaluationException`**: `Triangle limit exceeded (limit N)`, located at the instance that would exceed
  the budget, also for the slabs a `projection` could need (see _Projection_).
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
- **`ScadPrimitives`, `ScadTransforms`, `ScadExtrusions`, and `ScadTextGeometry` units** — the leaf and container
  builders in the dispatch tables (`ScadPrimitives.*`, `ScadTextGeometry.Build`, `ScadTransforms.*`,
  `ScadColors.Color`, `ScadExtrusions.*`), and `ScadExtrusions.MakeSlabs` for a flat model; `ScadShape2D` for the
  2D shape tree. The context also creates the `ScadFontResolver` that `ScadTextGeometry` uses (one per build).
- **`Render3D` public types** — `Scene`, `SceneNode`, `GroupNode`, `MeshNode`, `CsgNode`, `OverlayNode`, `CsgOperation`,
  `BoundingBox3D`, `PolygonRegion`, and `Mesh` (scene model and tessellation), `Matrix4x4D` and `Vector3D` (the frame
  of a `projection`), `ConvexHull` and `MinkowskiSum`
  (the exact hull and the convex Minkowski sum used by `ScadHulls`), and `SceneRenderOptions`,
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
`Render` then creates the renderer options with `ScadSceneOptions.Create` (with `flat` set for the flat default view)
and uses `MapFeature` and
`ScadSceneMap.Find` in the renderer's handler, and reads `ScadBuiltScene.IsFlatModel` to choose the camera. The tests in
`test/DemaConsulting.CanvasNet.Scad.Tests/Geometry/ScadGeometryBuilder*Tests.cs`, `ScadSceneOptionsTests.cs`, and
`ScadUnsupportedModuleTests.cs` call `Build` and `Create` directly through `InternalsVisibleTo`, through the
helpers of `ScadGeometryTestSupport` (which evaluates a program into a sink and builds it), and hand-built
`ScadInstance` trees for the depths the evaluator cannot reach.
