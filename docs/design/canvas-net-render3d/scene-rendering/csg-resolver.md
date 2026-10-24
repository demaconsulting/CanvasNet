### CsgResolver

<!-- cspell:ignore subtrahend subtrahends minuend postfix rasterizer speckle -->

The `CsgResolver` unit is a software unit in the `SceneRendering` subsystem. It turns the
scene's boolean tree into a small program and resolves, per sample, which surface of the combined
solid is visible. All of its types are internal to the assembly (visible to the test assembly
through `InternalsVisibleTo`):

- `CsgCompiler` (static class) and `CsgCompilation` (readonly record struct): the iterative tree
  compiler and its result (program, instance-to-leaf map, leaf count)
- `CsgProgram` (sealed class), `CsgInstruction` (readonly record struct), and `CsgOpCode`
  (enum): the immutable postfix program evaluated over a leaf-state bitset
- `CsgResolver` (sealed class): the per-worker, per-sample resolver

#### Purpose

Constructive solid geometry is rendered **without mesh boolean operations**: every operand is
rasterized as an ordinary closed mesh and the combined solid is evaluated per sample along the view
ray, from the sorted list of the ray's surface crossings (fragments). The unit owns the compilation
of the boolean tree, the ray-state bookkeeping, tie handling for coincident faces, and the choice of
the visible fragment and its shade.

#### Data Model

- **Leaf.** A maximal union-only subtree (no difference or intersection with two or more children)
  collapses into one leaf; all its mesh instances share the leaf's **signed crossing counter**: a
  front-facing fragment adds +1 and a back-facing one -1, and the leaf is *inside* exactly while the
  counter is positive. For closed meshes this is exact for overlapping unions.
- **`CsgProgram`.** Postfix instructions `PushLeaf n`, `PushFalse`, `Or`, `And`, `AndNot` over a
  `ulong[]` bitset (bit `n % 64` of word `n / 64`), with `LeafCount`, `BitsetWords`, and the
  maximum stack depth computed at construction (which also validates the structure). Instances are
  immutable and shared by all workers.
- **`CsgCompilation`.** The program (`null` for a union-only scene), `LeafOfInstance` indexed by the
  instance's pre-order number, and the leaf count.
- **`CsgResolver` scratch.** `int[] counters`, `ulong[] bits`, `bool[] changed`, and the evaluation
  stack, sized from the program; they are all zero/false between samples and only touched for leaves
  that have fragments.
- **Tie constants and per-triangle inputs.** `SlopeQuantum` (1/128 sample) and `SlopeToleranceCap`
  (1000 base tolerances) are private constants; the resolver reads each triangle's `Priority`
  (the mesh instance) and `DepthSlope` from the `ScreenTriangle` list to decide group membership.

#### Key Methods

##### Compile (`CsgCompiler.Compile`)

1. Compute, with an explicit stack and a reference-keyed memo (shared nodes are evaluated once),
   which nodes have a difference or intersection with two or more children in their subtree.
2. Walk the tree in the pre-order of `Scene.EnumerateInstances` (children in order, a shared node
   once per path) with an explicit frame stack. A union-only subtree becomes one `PushLeaf` for a new
   leaf (all its instances map to it) or `PushFalse` when it contains no mesh. A boolean node emits
   its first child, then each further child followed by `Or` (group or union), `AndNot` (difference:
   *first minus all others*), or `And` (intersection). A node without children is `PushFalse` for
   every operation; a single-child boolean is its child.
3. A union-only root yields no program: the scene is rendered by the z-buffer with no fragment
   storage.

No recursion is used, so deep scenes cannot overflow the call stack; the evaluator is iterative
over a caller-supplied boolean stack for the same reason.

##### Resolve (`CsgResolver.Resolve` / `ResolveTile`)

For one sample's fragments:

1. **Order.** Sort by the total order *(depth, triangle index)*; the outcome is therefore
   independent of the order in which the fragments were collected.
2. **Initial state.** The counter of a leaf at the start of the ray is minus the sum of the signed
   crossings of **all** its fragments, including those beyond the far distance. A camera inside a
   solid sees only the solid's exit, so its leaf starts inside. Set the leaf bits (`counter > 0`) and
   evaluate the program: `inside` at the ray start.
3. **Sweep tie groups.** A group starts at a fragment (the **anchor**) and takes the following
   fragments that satisfy the join rule below (anchored at the first fragment, so a chain of close
   fragments does not merge). Stop when the group starts beyond the far distance. Apply all
   fragments of the group to the counters, mark leaves whose state changed, and only if some leaf
   changed update the bits and re-evaluate the program.

   **Join rule (`JoinsGroup`).** With `gap` the depth difference to the anchor and `epsilon` the
   base tolerance (`1e-6 * max(scene diameter, |largest depth|)`, the Phase 1 value), a fragment
   joins when `gap <= epsilon`. Otherwise it joins only if no fragment already in the group belongs
   to its own mesh instance (`ScreenTriangle.Priority`, the instance's pre-order number) and
   `gap <= epsilon + noise`, where
   `noise = min((slopeAnchor + slopeCandidate) * SlopeQuantum, SlopeToleranceCap * epsilon)`,
   `slope` is the triangle's `ScreenTriangle.DepthSlope`, `SlopeQuantum` is 1/128 sample (two
   fixed-point 24.8 quanta of 1/256), and `SlopeToleranceCap` is 1000. Rationale: two independently
   tessellated or placed meshes with mathematically coincident faces rasterize from vertices
   rounded to 1/256 sample, so at oblique angles their interpolated depths differ by about slope
   times quantum, far above `epsilon`, which would otherwise leave isolated wrong pixels. A
   fragment of an instance that is already in the group is a different event (for example the far
   side of a thin fold or wall) and is never merged by slope noise: it falls back to `epsilon`
   only.
4. **Visible surface.** The first group that changes the root result is visible. If the result
   became inside, the chosen triangle's `EnterColor` is the shade; if it became outside (only
   possible when the ray started inside) its `ExitColor`. The renderer precomputes these shades per
   triangle: for a front-facing triangle the viewer-facing normal shades with the leaf color when
   entering; for a back-facing triangle entering means a **cut surface** exposed by a subtrahend,
   shaded with the flipped normal and that leaf's color. Exit shades use the back-face color, like
   the z-buffer's back faces seen from inside.
5. **Choice within a group.** Candidates are the fragments whose leaf changed. Preferred are those
   whose leaf **alone** explains the change (reverting just that leaf's bit restores the previous
   root result); if none does, all candidates qualify. Among the preferred, the lowest `Priority`
   (scene pre-order) wins, then the lowest triangle index. A chosen depth beyond the far distance
   means nothing is visible.
6. **Output.** `ResolveTile` writes depth, color, and the winner metadata
   `(priority << 1) | backFacing` into the `TileWorkspace` exactly as the z-buffer does, or leaves
   positive infinity for no surface, so `SampleResolver` is reused unchanged. It checks the
   cancellation token once per sample row.

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
  `ArgumentOutOfRangeException` for malformed input.
- Open, non-manifold, inverted, or clipped operands produce unmatched crossings; the resolver never
  throws for them. Their counters can be wrong (a visible streak), but the outcome stays bounded and
  deterministic. Detection and reporting of such operands belong to the renderer (the
  `CsgOperandValidator` of the `SceneRenderer` unit).
- The unit validates nothing about triangle indices or leaf numbers beyond the .NET bounds checks:
  its inputs are produced by `SceneRenderer`.

#### Dependencies

`FragmentBuffer` (`Fragment`, `GetSample`), `ScreenTriangle` and `TileWorkspace` of the
`TriangleRasterizer` and `SceneRenderer` units, the `SceneModel` subsystem's `SceneNode`,
`GroupNode`, `CsgNode`, and `CsgOperation`, and the core `CanvasNet` system's `Rgba32`.

#### Callers

`SceneRenderer`: `CsgCompiler.Compile` once per render, then one `CsgResolver` per worker calling
`ResolveTile` for every tile on the CSG path. The unit tests also call `Resolve` directly with
hand-built fragment lists.
