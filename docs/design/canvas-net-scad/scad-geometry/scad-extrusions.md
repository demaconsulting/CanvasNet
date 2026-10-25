### ScadExtrusions

<!-- cspell:ignore extrude extruder extruders extrusion extrusions apex pinched Pappus lathe bijection bijectively -->
<!-- cspell:ignore subtrahend minuend frustum faceting faceted facets -->

The `ScadExtrusions` unit is the fourth software unit in the `ScadGeometry` subsystem. It comprises four source
files in the `DemaConsulting.CanvasNet.Scad.Geometry` namespace, all `internal`:

- `ScadShape2D.cs` — `ScadShape2D` (sealed class) and `ScadShape2DKind`: the symbolic 2D tree (leaf, planar
  transform, color, boolean) that the geometry builder keeps instead of one evaluated 2D region
- `ScadExtrusions.cs` — `ScadExtrusions` (static partial class): the shared machinery — child collection,
  applying the planar transforms to the leaves, the distribution over the tree, the leaf extrusion with the
  rejection mapping, the budget arithmetic, and `MakeSlabs` for top-level 2D shapes
- `ScadExtrusions.Linear.cs` — `LinearExtrude`: argument validation, the slice rule, and the call to
  `Extruder.Linear`
- `ScadExtrusions.Rotate.cs` — `RotateExtrude`: the angle and segment rules, the axis checks, and the call to
  `Extruder.Rotate`

#### Purpose

`ScadExtrusions` turns the 2D results of a model into 3D solids: it builds `linear_extrude` and `rotate_extrude`
and the thin slabs of 2D shapes that stand alone at the top of a model. The package has no 2D boolean kernel
(see _ScadGeometry Subsystem Design_), so a 2D result is a `ScadShape2D` tree and the extrusion is
**distributed over the tree**: the extrusion of `difference() { A; B; }` is the 3D difference of the extrusion
of `A` and the extrusion of `B`. That is exact because an extrusion maps a point of the profile domain to a
point of the solid bijectively, so set operations commute with it, and the renderer's constructive solid
geometry then resolves the result per pixel. The dialect decisions of the unit (the extrusion defaults, the twist
sign, the sweep direction, and the segment rules) are original decisions of the CanvasNetScad dialect (see
_CanvasNetScad System Design_, Clean-Room Provenance); no OpenSCAD source or manual text was consulted.

The unit's behavior is specified by the `CanvasNetScad-ScadGeometry-ScadExtrusions-*` requirements
(`Distribution`, `ProfileTransforms`, `InvalidProfile`, `RejectedExtrusion`, `ChildDimensions`, `LinearHeight`,
`LinearTwist`, `LinearSlices`, `LinearScale`, `LinearUnsupportedScale`, `LinearArguments`, `TriangleBudget`,
`RotateSweep`, `RotateArguments`, `RotateSegments`, `RotateAxis`, `ErrorMapping`, and `Robustness`) in
`docs/reqstream/canvas-net-scad/scad-geometry/scad-extrusions.yaml`.

#### Data Model

**`ScadShape2D`** is an immutable node of kind `Leaf`, `Transform`, `Color`, or `Csg` (`ScadShape2DKind`) with a
read-only child list: none for a leaf, one for a transform or a color, any number for a boolean.

- A **leaf** holds the `PolygonRegion` of a 2D primitive and the `ScadSourceLocation` of the instance that
  produced it.
- A **transform** holds the planar part of a 4×4 `Matrix4x4D` in its row-vector convention (rows and columns 0
  and 1 plus the translation, with the Z row and column of the identity).
- A **color** holds the `NodeAppearance` of the enclosing `color` instance.
- A **boolean** holds a `CsgOperation` (`Union`, `Difference`, or `Intersection`); for a difference the first
  child is the minuend.

Nodes are created only through the `CreateLeaf`, `CreateTransform`, `CreateColor`, and `CreateCsg` factories,
are consumed once by their parent (the instantiation tree has no shared nodes), and are never mutated.
`EnumerateLeaves` returns the leaves in document order with the accumulated planar matrix of each (a transform
applies to its child first, then to everything above it), and `ComputeVertexBounds` returns the exact bounds of
every vertex of every leaf, including operands a boolean would remove, as a box in the XY plane at Z = 0.

**Extrusion job** (`ScadExtrusions.Job`, private): the shape, the transformed `PolygonRegion` of each valid leaf
keyed by leaf reference, a `Failed` flag that is set when a leaf is rejected, and the running bounds of the
meshes created so far. One job lives for one extrusion call.

#### Key Methods

##### `CollectShape(node, results, context)`

Builds the 2D input of an extrusion from the results of its children. When any child is a solid it warns
`<module>: only 2D children can be extruded; the 3D children are ignored` at the instance, then unions the flat
children with `ScadBooleans.Union`. An extrusion without flat children yields an empty result, silently.

##### `Prepare(shape, context)`

Walks `EnumerateLeaves` and, for each leaf, applies the accumulated planar matrix to the points of its region
(`TransformRegion`: `x' = x·M11 + y·M21 + M41`, `y' = x·M12 + y·M22 + M42`) before any extrusion, so the extruders
only ever see regions and never a matrix; an identity matrix reuses the leaf's own region. `PolygonRegion`
re-normalizes the winding, so a mirror costs nothing. A leaf whose transformed points the polygon validator
rejects (for example a self-crossing contour; a transform that leaves the coordinate range is rejected
earlier by the builder as `scad-geometry-rejected`) is reported as the unsupported feature **`scad-polygon-invalid`**
at the **leaf's** location and left out; the other leaves are kept. Each leaf also ticks the cancellation counter.
When no leaf is valid, the extrusion is empty.

##### `Distribute(shape, leaf)` and `CombineOperands(node, results)`

An iterative post-order walk (an explicit list of frames, like the builder's engine) that extrudes every leaf with
the supplied function and rebuilds the shape tree as a scene tree. A transform node forwards its single child (the
transform was already applied to the points), a color node wraps its child in a `GroupNode` that carries the
appearance, and a boolean node combines its operands: a union is a `GroupNode` (or the single operand), a
difference is a `CsgNode` with `CsgOperation.Difference`, and an intersection a `CsgNode` with
`CsgOperation.Intersection`. A leaf that yields no node is an **empty operand**: it vanishes from a union, empties
an intersection, and empties a difference when it is the first operand and vanishes otherwise (position matters:
nothing minus anything is nothing).

##### `Extrude(job, context, node, approximate, predict, extrude)` and `ExtrudeLeaf(...)`

`Extrude` first charges the exact triangle count of every leaf (`predict`, see the budget rules below), then
distributes one leaf function over the job and wraps the root as a solid whose bounds are the union of
the leaf mesh bounds; the result carries the approximate flag of the input shape (set when it contains a
difference or intersection). `ExtrudeLeaf` polls cancellation, calls the extruder, settles the charge against the
mesh's actual triangle count, and creates the mesh node with
`ScadGeometryContext.CreateMeshNode`, which records the **extrusion instance's location** for the node (this is
how a later Render3D report finds its way back to the source). A mesh without triangles is an empty operand. An
`ArgumentException` from the extruder is the **catch-all mapping** (see _Error Handling_): it sets `Failed`,
reports `scad-geometry-rejected`, and the **whole extrusion** is then omitted, because a partial extrusion would
silently change the meaning of a boolean.

##### `LinearExtrude(node, results, context)`

Builds `linear_extrude(height, center, convexity, twist, slices, scale)`:

1. Binds the arguments with `ScadArguments` (an unknown or surplus argument warns and the extrusion is still
   built), collects the shape, and validates the parameters; an invalid instance warns and is omitted, and an
   empty shape is then silently empty.
2. **Defaults.** `height` defaults to **100**; `center` is the truthiness of its value; `convexity` is accepted and
   ignored (a pure hint); `twist` defaults to 0, `slices` to the default rule below, and `scale` to 1.
3. **Validation.** `height` must be a finite number above zero; `twist` must be a finite number; `slices` must
   be a finite number of at least 1 and is rounded down; `scale` must be a finite number or a vector of two finite
   numbers (a non-finite value is a malformed argument). A violation is a
   **warning** `linear_extrude: <problem>; the instance is ignored` at the instance, and the instance is empty.
4. **Scale.** A scalar `s` means `[s, s]`. Both components must be positive, or both exactly zero (the top
   collapses to an **apex**, a point). A negative component, or exactly one zero component, is the unsupported
   feature **`scad-linear-extrude-scale`**. A scale of zero in both components over a leaf with more than one
   contour (a hole or an island) is the unsupported feature **`scad-linear-extrude-apex-multiple-contours`**,
   because the extruder cannot collapse several contours onto one point. Both omit the whole extrusion.
   A scale equal to `[1, 1]` is passed as no scale.
5. **Twist.** A positive `twist` turns the top **clockwise seen from +Z**, which is the opposite of the renderer's
   extruder convention, so the angle is negated (`-twist`) when it is handed to `Extruder.Linear`.
6. **Slices.** An explicit `slices` wins. Otherwise 1 without a twist and, with a twist,
   `max(1, ceil(Fragments(radius) · |twist| / 360))` where `radius` is the largest distance from the origin of
   any vertex of any leaf and `Fragments` is `ScadFragmentRule.Fragments` with the instance's `$fn`, `$fa`, and
   `$fs`. The twist turns about the origin, so the farthest vertex sweeps the longest arc.
7. **Budget.** The **exact** triangle count of every valid leaf is computed from its contours before anything is
   tessellated and charged with `ScadGeometryContext.ChargeTriangles`: `2·V·slices` wall triangles (`2·V·(slices − 1)
   - V` with an apex, whose last slice has one triangle per edge) plus `V + 2H − 2P` triangles per cap (an apex
   has only the bottom cap), where `V` is the vertex count of the leaf, `H` its number of holes (clockwise
   contours) and `P` its number of solid contours (counter-clockwise outer boundaries and islands); the cap count
   is the documented triangle count of `PolygonTriangulator`, which adds no Steiner points. The arithmetic is done
   in`double` and saturated at 1e18 with `Saturate`; the slice count handed to the extruder is clamped to
   `int.MaxValue` (the budget check has already failed by then for any hostile value). A profile of one outer
   boundary and two holes with 12 vertices is therefore charged `24 + 2·14 = 52` (a rule of four triangles per
   vertex would have charged 48). When a leaf's mesh is created, `SettleTriangles` reconciles the charge with the
   mesh's actual triangle count (it differs only if the tessellator drops a triangle that rounds to degenerate),
   so the charge always equals the triangles created; the charge of a leaf that is rejected or not attempted is
   returned with `RefundTriangles`.
8. Each leaf is extruded with `Extruder.Linear(region, height, center, -twist, slices, scale)`; all leaves share
   the same slice count, so the faceting of the operands of a boolean agrees.

##### `RotateExtrude(node, results, context)`

Builds `rotate_extrude(angle, convexity)`:

1. **Angle.** The default is **360**. A non-number angle (including a non-finite one) warns
   `rotate_extrude: angle must be a number; the instance is ignored`; an angle of **0** warns
   `rotate_extrude: angle must not be zero; the instance is ignored`; both omit the instance. A magnitude above 360
   is clamped to a full turn. A negative angle sweeps clockwise: the unit revolves counter-clockwise and mirrors
   the resulting mesh in the XZ plane with `Mesh.Transform(Matrix4x4D.CreateMirror(Y))`. `convexity` is accepted
   and ignored.
2. **Axis rule** (`CheckAxis`, over all valid leaves together). The profile X is the radius and profile Y the
   height. Vertices strictly on both sides of the axis (`x > 0` somewhere and `x < 0` somewhere, in any leaf —
   which also covers a difference operand that crosses the axis even where the difference would not, because every
   operand is revolved separately) are the unsupported feature **`scad-rotate-extrude-axis-crossing`**. A vertex
   at exactly `x == 0` whose two contour neighbors are both off the axis (`FindAxisContact`) is an isolated
   contact, the unsupported feature **`scad-rotate-extrude-axis-contact`**, because the swept surfaces would meet
   at a pinched point; the message names the point. There is no tolerance. A vertex on the axis that ends an edge
   along the axis (a **pole**) is fine. Both reports omit the whole extrusion.
3. **Negative side.** A profile with every vertex at `x <= 0` (and some `x < 0`) is accepted and **swept where it
   lies**: it is handed to `Extruder.Rotate` unchanged, whose rotation is literal (azimuth 0 toward `+angle`, with
   the winding flipped for the negative side, so the mesh is outward and closed). A point at `(-4, 0)` with
   `angle = 90` therefore sweeps from azimuth 180 to 270 degrees and the wedge lies in the quadrant `x < 0`,
   `y < 0`; with `angle = -90` it sweeps from 180 down to 90 degrees (`x < 0`, `y > 0`). A full turn equals the
   full turn of the mirrored profile. Mixed signs are the axis-crossing report of rule 2. Pinned by
   `ScadExtrusionsRotate_NegativeSideProfile_PartialSweepLiesInTheNegativeQuadrant`,
   `ScadExtrusionsRotate_NegativeSideProfile_FullTurnEqualsTheMirroredProfile`, and
   `ScadExtrusionsRotate_MixedSignProfile_IsReported`.
4. **Segments.** `radius` is the largest `|x|` over all leaves, `fragments = ScadFragmentRule.Fragments(radius, $fn,
   $fa, $fs)`, and `segments = ceil(max(fragments · sweep / 360, 1))` where `sweep = min(|angle|, 360)`. The
   count is then raised to at least `ceil(sweep / 120)`. This honors the **`Extruder.Rotate` step contract**: every
   angular step (`sweep / segments`) must be at most 120 degrees, the step of the coarsest full revolution, because
   wider steps make the wall chords pass through the axis or face the wrong way (see _Extruder Unit Design_ in
   `docs/design/canvas-net-render3d/tessellation/extruder.md`, Rotational extrusion rules). A caller that
   tessellates by fragment count (at least 3 for a full turn) satisfies the bound for a full turn; the extra rule
   covers partial sweeps (for example 121 degrees in one segment) and small `$fn` values. A partial sweep gets flat
   end caps from the extruder; a full turn has no seam.
5. **Budget.** The exact count is charged before the extruder allocates, as for `linear_extrude`: per segment two
   wall triangles for each profile edge with both ends off the axis and one for an edge with exactly one end on the
   axis (none when both are on it), plus, for a partial sweep only, two caps of `V + 2H − 2P` triangles each (a full
   turn has no caps). The charge is settled against the actual mesh and refunded for a rejected leaf, as there.
6. Each leaf is revolved with `Extruder.Rotate(region, sweep, segments)`, with the same segment count for all
   leaves.

##### `MakeSlabs(geo, context)`

Turns the 2D result at the **top of the model** (called by `ScadGeometryBuilder` for a flat root) into thin solids
of the configured `FlatShapeThickness` (default 0.1, the `ScadRenderOptions` setting). It applies `Prepare`, charges
the budget for **every** leaf first with the slab's exact count (`2·V` wall triangles plus two caps of
`V + 2H − 2P` triangles; the primitives also check a lower bound of it, `SlabTrianglesLowerBound(V) = 8V / 3`,
without charging, when a 2D leaf is created, to refuse a hostile `$fn` early), then distributes
`Extruder.Linear(region, thickness)` over
the tree, so a top-level `difference()` of 2D shapes becomes a difference of slabs of one thickness. The charge of
each slab is settled against its mesh (and refunded when it is rejected). The mesh of a
slab is mapped to the location of the **2D leaf** that produced it. Unlike an extrusion, a slab whose extruder
rejects it is reported as `scad-geometry-rejected` ("The 2D shape could not be made into a slab") and only that
leaf is left out. The result is an empty group when nothing could be built.

#### Error Handling

The unit follows the subsystem rule (see _ScadGeometry Subsystem Design_): argument problems are warnings, CanvasNet
capability limits are unsupported features, and only a resource overrun throws.

- **Warning** (`ScadReporter.Warn`, at the instance, instance omitted): invalid `linear_extrude` height, twist,
  slices, or scale; a zero or non-numeric `rotate_extrude` angle; 3D children of an extrusion (the children are
  ignored and the 2D ones are still extruded); unknown or surplus arguments (reported by `ScadArguments`, the
  extrusion is still built).
- **Unsupported feature** (`ScadReporter.Unsupported`; in strict mode the first new report throws
  `ScadUnsupportedFeatureException`): the whole extrusion is omitted.

  | Cause | Feature id | Location |
  | ----- | ----- | ----- |
  | Scale with a negative component or exactly one zero component | `scad-linear-extrude-scale` | extrusion |
  | Scale 0 over a leaf with holes or islands | `scad-linear-extrude-apex-multiple-contours` | extrusion |
  | Profile on both sides of the axis | `scad-rotate-extrude-axis-crossing` | extrusion |
  | Profile touches the axis at an isolated vertex | `scad-rotate-extrude-axis-contact` | extrusion |
  | Leaf invalid after its transformations (only that leaf is left out) | `scad-polygon-invalid` | 2D leaf |
  | Any other `ArgumentException` from `Extruder.Linear` or `Extruder.Rotate` | `scad-geometry-rejected` | extrusion |

- **Render3D `ArgumentException` mapping.** The checks above are made **before** the Render3D extruders are called,
  and each corresponds to a contract row of the extruders: a zero or one-sided scale, an apex over several
  contours, an axis crossing, an isolated axis contact, and a region that is not valid. The single catch in
  `ExtrudeLeaf` therefore only sees what the checks did not foresee and reports it as `scad-geometry-rejected`
  with the extruder's message preserved; the build never crashes on a tessellator rejection. In the tests this path
  is reached only through `rotate_extrude` with an angle whose radian value rounds to zero (`5e-324`); no input
  that makes `Extruder.Linear` reject after the checks is known, and the path is shared. The eight contract rows
  (each tested in report mode and in strict mode, with the expected feature and location) are: apex over several
  contours, negative scale, half-zero scale, axis crossing, axis contact, an invalid leaf under `linear_extrude`,
  an invalid leaf under `rotate_extrude`, and the extruder catch-all.
- **`ScadEvaluationException`** (located at the extrusion, "Triangle limit exceeded (limit N)"): the exact
  triangle count exceeds `MaxTriangles` (extrusions and slabs). The counts saturate, so a hostile slice count, twist,
  or `$fn` fails cleanly without overflow and before any mesh is allocated.
- **`OperationCanceledException`** — polled for each leaf and before each extruder call; never wrapped.
- **No recursion.** `EnumerateLeaves`, `ComputeVertexBounds`, `Distribute`, and `CombineOperands` are iterative
  (explicit stacks), and the builder never recurses either, so a deeply nested 2D tree (thousands of transforms or
  booleans) is extruded on any caller stack. The methods are stateless and thread-safe.

#### Dependencies

- **`Evaluation` namespace** — `ScadInstance` (name, location, fragment settings, and arguments),
  `ScadSourceLocation`, `ScadEvaluationException`, `ScadValue` (through `ScadArguments`), and `ScadNumbers`;
  warnings and unsupported features go to the `ScadEvaluationSink` through `ScadReporter`.
- **Sibling units of the subsystem** — `ScadGeo` (the result union), `ScadGeometryContext` (reporter, budget,
  cancellation, mesh-node creation, and `FlatShapeThickness`), `ScadArguments` and `ScadFragmentRule`
  (`ScadPrimitives` unit), `ScadBooleans.Union`, `ScadFeatureIds`, and `ScadPrimitives.SlabTrianglesLowerBound`.
- **Render3D public types** — `PolygonRegion`, `Extruder.Linear` and `Extruder.Rotate`, `Mesh`, `MeshNode`,
  `GroupNode`, `CsgNode`, `CsgOperation`, `NodeAppearance`, `SceneNode`, `BoundingBox3D`, `Matrix4x4D`, `Vector2D`,
  and `Vector3D`.
- **.NET base class library** — `System.Collections.Generic` and `System.Linq`.

#### Callers

`ScadGeometryBuilder` registers `LinearExtrude` and `RotateExtrude` in its module table (so they run as the
combiner of the `linear_extrude` and `rotate_extrude` instances, after the children are built) and calls
`MakeSlabs` for a flat model result. `ScadGeo.FromRegion` (primitives), `ScadTransforms` (planar transform),
`ScadColors` (color), and `ScadBooleans` (boolean) create the `ScadShape2D` nodes, and `ScadTransforms.Resize` uses
`ComputeVertexBounds`. The tests in `test/DemaConsulting.CanvasNet.Scad.Tests/Geometry/ScadExtrusions*Tests.cs`
call the unit through `ScadGeometryBuilder.Build` using `ScadGeometryTestSupport`, with exact analytic volumes
as the oracle. The pixel equivalence of an extruded 2D difference with a hand-built difference of extrusions is
checked in `ScadDocumentRenderExtrusionTests.cs`, and the builder-level stack and slab behavior in the builder,
primitives, and colors tests; see _ScadExtrusions Unit Verification Design_.
