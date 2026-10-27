### CsgResolver

<!-- cspell:ignore subtrahend subtrahends minuend postfix rasterizer speckle transmittance -->

The `CsgResolver` unit is a software unit in the `SceneRendering` subsystem. It turns the
scene's boolean tree into a small program and resolves, per sample, which surfaces of the combined
solid (and of any overlay bodies) are visible. All of its types are internal to the assembly (visible to the test assembly
through `InternalsVisibleTo`):

- `CsgCompiler` (static class) and `CsgCompilation` (readonly record struct): the iterative tree
  compiler and its result (main program, overlay programs, instance-to-leaf map, leaf count)
- `CsgProgram` (sealed class), `CsgInstruction` (readonly record struct), and `CsgOpCode`
  (enum): the immutable postfix program evaluated over a leaf-state bitset
- `LayerCompositor` (struct): the integer front-to-back accumulator of translucent layers
- `CsgResolver` (sealed class): the per-worker, per-sample resolver

#### Purpose

Constructive solid geometry is rendered **without mesh boolean operations**: every operand is
rasterized as an ordinary closed mesh and the combined solid is evaluated per sample along the view
ray, from the sorted list of the ray's surface crossings (fragments). The unit owns the compilation
of the boolean tree, the ray-state bookkeeping, tie handling for coincident faces, the choice of
the visible fragment and its shade, and, for translucent surfaces and overlays, the front-to-back
accumulation of the surface layers of the main solid and of independent overlay bodies.

#### Data Model

- **Leaf.** A maximal union-only subtree (no difference or intersection with two or more children,
  no overlay) collapses into one leaf; all its mesh instances share the leaf's **signed crossing
  counter**: a
  front-facing fragment adds +1 and a back-facing one -1, and the leaf is *inside* exactly while the
  counter is positive. For closed meshes this is exact for overlapping unions.
- **`CsgProgram`.** Postfix instructions `PushLeaf n`, `PushFalse`, `Or`, `And`, `AndNot` over a
  `ulong[]` bitset (bit `n % 64` of word `n / 64`), with `LeafCount`, `BitsetWords`, and the
  maximum stack depth computed at construction (which also validates the structure). Instances are
  immutable and shared by all workers.
- **`CsgCompilation`.** The main program (`null` for a union-only scene without overlay),
  `LeafOfInstance` indexed by the instance's pre-order number, the leaf count, and
  `OverlayPrograms`: one program per non-empty `OverlayNode` body in pre-order. All programs use the
  global leaf count and every leaf belongs to exactly one program.
- **`LayerCompositor`.** The accumulated opacity (0 to 255, the complement of the remaining
  transmittance) and the weighted channel sums; its default value is the empty state.
- **`CsgResolver` scratch.** `int[] counters`, `ulong[] bits`, `bool[] changed`, the evaluation
  stack, the owning program of each leaf, the current result and a touched mark per program and
  the layer list of the current tie group, sized from the programs; they are all zero/false
  between samples and only touched for leaves that have fragments.
- **Tie constants and per-triangle inputs.** `SlopeQuantum` (1/128 sample), `SlopeToleranceCap`
  (1000 base tolerances) and `NormalTolerance` (1e-6) are private constants; the resolver reads each
  triangle's `Priority` (the mesh instance), `DepthSlope`, and supporting plane (`PlaneNormal`, a
  view-space unit normal, and `PlanePoint`, the centroid, both from the narrowed vertices the
  triangle was rasterized from) from the `ScreenTriangle` list to decide group membership.

#### Key Methods

##### Compile (`CsgCompiler.Compile`)

1. Compute, with an explicit stack and a reference-keyed memo (shared nodes are evaluated once),
   which nodes have a difference or intersection with two or more children, or (in the main
   context) an overlay, in their subtree. A second memo with an overlay treated as a plain group
   classifies overlay bodies on demand, so a node shared by the main tree and an overlay body is
   classified per context.
2. Walk the tree in the pre-order of `Scene.EnumerateInstances` (children in order, a shared node
   once per path) with an explicit frame stack. A union-only subtree becomes one `PushLeaf` for a new
   leaf (all its instances map to it) or `PushFalse` when it contains no mesh. A boolean node emits
   its first child, then each further child followed by `Or` (group or union), `AndNot` (difference:
   *first minus all others*), or `And` (intersection). A node without children is `PushFalse` for
   every operation; a single-child boolean is its child. An `OverlayNode` met in the main context
   emits `PushFalse` and its body is compiled **at that moment** by a second, non-nested walk into a
   program of its own (a union-only body is one leaf, a body with booleans gets normal frames, a
   nested overlay is a plain group), so leaf numbers follow the same pre-order as the instances; a
   body without meshes adds no program.
3. A union-only root without overlay yields no program: an opaque scene is rendered by the z-buffer
   with no fragment storage. A scene of only overlays has the main program `FALSE`.

No recursion is used, so deep scenes cannot overflow the call stack; the evaluator is iterative
over a caller-supplied boolean stack for the same reason.

##### Resolve (`CsgResolver.Resolve` / `ResolveTile`)

For one sample's fragments:

1. **Order.** Sort by the total order *(depth, triangle index)*; the outcome is therefore
   independent of the order in which the fragments were collected.
2. **Initial state.** The counter of a leaf at the start of the ray is minus the sum of the signed
   crossings of **all** its fragments, including those beyond the far distance. A camera inside a
   solid sees only the solid's exit, so its leaf starts inside. Set the leaf bits (`counter > 0`) and
   evaluate every program: `inside` of each program at the ray start.
3. **Sweep tie groups.** A group starts at a fragment (the **anchor**) and takes the following
   fragments that satisfy the join rule below (anchored at the first fragment, so a chain of close
   fragments does not merge). Stop when the group starts beyond the far distance. Apply all
   fragments of the group to the counters, mark leaves whose state changed, and only if some leaf
   changed update the bits and re-evaluate, in ascending program order, each program that owns a
   changed leaf.

   **Join rule (`JoinsGroup`).** With `gap` the depth difference to the anchor and `epsilon` the
   base tolerance (`1e-6 * max(scene diameter, |largest depth|)`, the Phase 1 value), a fragment
   joins when `gap <= epsilon`. Otherwise it joins only if no fragment already in the group belongs
   to its own mesh instance (`ScreenTriangle.Priority`, the instance's pre-order number), the
   candidate's triangle is **coplanar** with the anchor's triangle, and
   `gap <= epsilon + noise`, where
   `noise = min((slopeAnchor + slopeCandidate) * SlopeQuantum, SlopeToleranceCap * epsilon)`,
   `slope` is the triangle's `ScreenTriangle.DepthSlope`, `SlopeQuantum` is 1/128 sample (two
   fixed-point 24.8 quanta of 1/256), and `SlopeToleranceCap` is 1000. Coplanar (`AreCoplanar`)
   means the unit normals are parallel in either orientation (`1 - |n1 . n2| <= 1e-6`, about 0.08
   degrees) and the nearer of the two centroid-to-plane distances is within `epsilon`
   (measuring from the larger triangle's plane, so the angular noise of a tiny triangle's normal
   does not reject a truly coincident face); a triangle without a plane is never coplanar.
   Rationale: two independently tessellated or placed meshes with mathematically coincident faces
   rasterize from vertices rounded to 1/256 sample, so at oblique angles their interpolated depths
   differ by about slope times quantum, far above `epsilon`, which would otherwise leave isolated
   wrong pixels. That noise only moves the rasterized depth of faces that are the same plane in
   view space, so the widening is applied only to coplanar faces: genuinely separate, closely spaced
   surfaces (parallel planes further apart than `epsilon`, however grazing) keep their own events and
   their transition is not skipped. A
   fragment of an instance that is already in the group is a different event (for example the far
   side of a thin fold or wall) and is never merged by slope noise: it falls back to `epsilon`
   only.
4. **Visible surface (layers).** A program whose result changes in a group contributes a **layer**.
   If the result became inside, the chosen triangle's `EnterColor` is the shade; if it became
   outside, its `EnterColor` as well (the far wall of a translucent solid, a cavity wall: the
   triangle's own color with the normal flipped toward the viewer), except the **first** change of
   the main program (index 0) when it is an exit - the ray started inside the main solid - which uses
   the triangle's `ExitColor`. Overlay programs always use `EnterColor`. The renderer precomputes
   these shades per triangle, with the alpha of the owning appearance: for a front-facing triangle the
   viewer-facing normal shades with the leaf color when entering; for a back-facing triangle entering
   means a **cut surface** exposed by a subtrahend, shaded with the flipped normal and that leaf's
   color. Exit shades use the back-face color, like the z-buffer's back faces seen from inside. The
   layers of one tie group are applied overlay programs first, then by draw priority, then by triangle
   index, so an overlay in front of a coincident main face (for example a highlighted subtrahend)
   tints it instead of being hidden by it. A layer whose chosen depth lies beyond the far distance
   ends the sweep.
5. **Compositing.** Each layer is added to the `LayerCompositor` (see below); the sweep ends at an
   opaque layer (alpha 255), at the far distance or at the end of the fragments. The remainder is
   composited over the background color. With a single program whose first layer is opaque the result
   is exactly the classic first-visible-surface result.
6. **Choice within a group.** Candidates are the fragments whose leaf changed **and belongs to the
   program whose result changed**. Preferred are those
   whose leaf **alone** explains the change (reverting just that leaf's bit restores the previous
   result of that program); if none does, all candidates qualify. Among the preferred, the lowest
   `Priority` (scene pre-order) wins, then the lowest triangle index.
7. **Output.** `ResolveTile` writes the composite color, the depth and the winner metadata
   `(priority << 1) | backFacing` **of the first layer** into the `TileWorkspace`, or leaves
   positive infinity for no surface, so `SampleResolver` is reused unchanged (its premultiplied
   average already handles partial alpha). It checks the
   cancellation token once per sample row.

##### Layer compositing (`LayerCompositor`)

Front-to-back "under" compositing in integer arithmetic. With the accumulated opacity `O` (the
remaining transmittance is `T = 255 - O`), a layer of color `c` and alpha `a` takes
`w = (T * a + 127) / 255`, `O += w` and the sums gain `w * c`; an alpha of 255 takes all of `T`
(`IsOpaque`), alpha 0 takes nothing. `Finish(background)` fills the remaining transmittance with the
background (`wb = (T * ba + 127) / 255`) and returns the straight-alpha color
`(sum + wb * bg + A / 2) / A` with `A = O + wb` (fully transparent black when `A` is 0). Accumulating
from the nearest to the farthest layer is the same as the sorted back-to-front composite and needs no
per-sample layer storage; a single opaque layer returns its color exactly, one layer of alpha `a` over
an opaque background `b` gives `(a*c + (255-a)*b + 127) / 255` within one level, and over a
transparent background the result keeps the layer color with alpha `a`.

**Why coincident faces do not speckle.** For `cube(10) - box(10, 10, 5)` with the box flush on the
cube's bottom, a ray from below meets the cube's bottom (front) and the box's bottom (front) at
depths that differ only by float noise. Applied one at a time the root would turn inside after the
first and show the flush face (or, from above, turn inside between two coincident back faces for a
single fragment). Within the tolerance they form one group, so the root does not change there and
the cut surface (the box top, where the box ends) is shown instead.

**Limits.** Exactly coincident faces (identical vertex coordinates) give zero speckle. Nearly
coincident faces from different transform chains are speckle-free in the tested views, but along
the edge-on silhouette of coincident walls a few lone samples can remain (the stress test allows
up to 12 isolated pixels in an 80 x 80 image over 72 placements and views); where exact faces
cannot be avoided, use a small overlap for subtracted operands. Events closer than the tolerance
anywhere (thin features smaller than `epsilon`, or a grazing silhouette fold whose front and back
surfaces lie within `epsilon`) are treated as coincident, so forcing the fragment path onto a
union-only scene can differ from the z-buffer in a couple of grazing silhouette samples.

#### Error Handling

- `CsgCompiler.Compile` throws `ArgumentNullException` for a null root; `CsgProgram` and
  `CsgResolver` constructors throw `ArgumentException` / `ArgumentNullException` /
  `ArgumentOutOfRangeException` for malformed input (the `CsgResolver` program-list constructor rejects
  a null or empty list, a null program, and a leaf claimed by two programs).
- Open, non-manifold, inverted, or clipped operands produce unmatched crossings; the resolver never
  throws for them. Their counters can be wrong (a visible streak), but the outcome stays bounded and
  deterministic. Detection and reporting of such operands belong to the renderer (the
  `CsgOperandValidator` of the `SceneRenderer` unit).
- The unit validates nothing about triangle indices or leaf numbers beyond the .NET bounds checks:
  its inputs are produced by `SceneRenderer`.

#### Dependencies

`FragmentBuffer` (`Fragment`, `GetSample`), `ScreenTriangle` and `TileWorkspace` of the
`TriangleRasterizer` and `SceneRenderer` units, the `SceneModel` subsystem's `SceneNode`,
`GroupNode`, `CsgNode`, `OverlayNode`, and `CsgOperation`, and the core `CanvasNet` system's `Rgba32`.

#### Callers

`SceneRenderer`: `CsgCompiler.Compile` once per render, then one `CsgResolver` (built from the main
program, the overlay programs and the background color) per worker calling
`ResolveTile` for every tile on the fragment path. The unit tests also call `Resolve` directly with
hand-built fragment lists.
