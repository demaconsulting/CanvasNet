### SceneGraph

The `SceneGraph` unit is the third software unit in the `SceneModel` subsystem. It comprises seven
public types in the `DemaConsulting.CanvasNet.Render3D.SceneModel` namespace plus one internal
type:

- `SceneNode` (abstract class): base of the node hierarchy, carrying a transform and an optional
  appearance
- `MeshNode` (sealed class): a leaf node referencing a `Mesh`
- `GroupNode` (sealed class): a node grouping child nodes (their union)
- `CsgNode` (sealed class): a node combining child nodes with a boolean `CsgOperation`
- `OverlayNode` (sealed class): an independent, typically translucent body (a color and a union of
  children) that takes no part in any ancestor's boolean operation
- `CsgOperation` (enum): `Union`, `Difference` (first child minus the rest), `Intersection`
- `NodeAppearance` (sealed record): a per-node surface color
- `Scene` (sealed class): the root of a scene, with bounds computation and instance enumeration
- `SceneInstance` (internal readonly record struct): one flattened mesh instance

#### Purpose

`SceneGraph` is the immutable description of "what to draw": a tree of nodes whose transforms
compose from the root down and whose appearances are inherited with the innermost node winning. It
also provides the one traversal that both bounds computation and rendering rely on, so that both
see exactly the same instances in the same order.

#### Data Model

- **`SceneNode`**: `Matrix4x4D Transform` (init-only; defaults to `Matrix4x4D.Identity`; the setter
  rejects a matrix with any non-finite element) and `NodeAppearance? Appearance` (init-only;
  `null` means "inherit"). The constructor is `private protected`, so the hierarchy is closed to the
  assembly: the renderer can switch exhaustively on node type, and a new node type (such as
  `CsgNode`) can be added without breaking callers.
- **`MeshNode`**: a non-null `Mesh`. **`GroupNode`**: a read-only list `Children`, copied from the
  constructor argument; empty groups are allowed; a group means the implicit union of its
  children's geometry. **`CsgNode`**: a defined `CsgOperation Operation` and a read-only list
  `Children`, copied from the constructor argument. `Difference` is the first child minus every
  other child and `Intersection` is the intersection of all children; a node with no children is
  empty (never inside) for **every** operation, including `Intersection`, and a node with one child
  is that child. A `CsgNode`'s transform and appearance apply to its operands exactly as a group's
  do. The operands of a difference or intersection are expected to be closed, outward-oriented
  meshes (see the `Mesh` unit's topology-defect classification).
- **`NodeAppearance(Rgba32 Color)`**: a surface color; the alpha of the color is honored (a value
  below 255 makes the visible surfaces translucent, 0 is invisible but still solid); the renderer
  composites the layers (see the `CsgResolver` unit). Members of one union merge, so only the
  boundary of a union of translucent bodies is visible.
- **`OverlayNode(Rgba32 color, IEnumerable<SceneNode> children)`**: a read-only list `Children`
  copied from the constructor argument (a null sequence throws `ArgumentNullException`, a null
  element `ArgumentException`; an empty overlay is allowed) and the `Color`. The overlay's color
  **replaces** every appearance beneath it (a forced override, unlike innermost-wins; the overlay
  node's own `Appearance` property is ignored); its alpha is honored (255 is an opaque independent
  body). In the boolean structure of every ancestor an overlay is false: it does not union,
  subtract or intersect. An overlay nested inside another overlay body is a plain group, so the
  outer color wins. The transform composes like a group's.
- **`Scene`**: a non-null `Root`, and two lazily computed, cached `BoundingBox3D` values (with and
  without overlay bodies). The bounds are the
  union of **all** operand instances, which for a difference or intersection conservatively
  encloses (and may exceed) the visible result; this keeps camera framing and the depth range
  enclosing every operand. Overlay bodies are included by default (a fitted camera keeps a ghost in
  the picture) and left out by `ComputeBounds(false)`.
- **`SceneInstance(Mesh, World, Appearance, Order, Node, InOverlay)`**: the composed world matrix, the
  effective appearance (the forced overlay color beneath an overlay), the zero-based pre-order index
  of the mesh node (which is the instance's
  draw priority), the originating `MeshNode` (used to attribute diagnostics) and whether the
  instance lies beneath an overlay. The boolean
  structure is not stored in the instance: a consumer that needs it walks `CsgNode`/`GroupNode`
  children itself in the same pre-order, which numbers the mesh nodes identically.

A node may be shared: the same `SceneNode` instance may appear under several parents (a directed
acyclic graph) and is then enumerated once per path. Because nodes are immutable and constructed
bottom-up, cycles cannot be built.

#### Key Methods

- `MeshNode(Mesh)`: null mesh throws `ArgumentNullException`.
- `GroupNode(IEnumerable<SceneNode>)`: a null sequence throws `ArgumentNullException`; a null
  element throws `ArgumentException`; the sequence is copied.
- `CsgNode(CsgOperation, IEnumerable<SceneNode>)`: an undefined operation throws
  `ArgumentOutOfRangeException`; a null sequence throws `ArgumentNullException`; a null element
  throws `ArgumentException`; the sequence is copied.
- `OverlayNode(Rgba32, IEnumerable<SceneNode>)`: a null sequence throws `ArgumentNullException`; a
  null element throws `ArgumentException`; the sequence is copied.
- `Scene(SceneNode root)`: null root throws `ArgumentNullException`.
- `Scene.ComputeBounds()`: the **tight** bounds, computed by transforming every vertex of every
  instance by its world matrix in double precision and taking the union (not the union of
  transformed bounding boxes, which can be loose under rotation). An empty scene or a scene of
  empty groups yields `BoundingBox3D.Empty`. The result is cached. It equals `ComputeBounds(true)`.
- `Scene.ComputeBounds(bool includeOverlays)`: the same computation, skipping the instances beneath an
  `OverlayNode` when the argument is `false` (a scene of only overlays then has empty bounds); each of
  the two results is cached separately.
- `Scene.EnumerateInstances()` (internal): an **iterative, explicit-stack, depth-first pre-order**
  traversal. For each node: `world = node.Transform * parentWorld`;
  `appearance = forced ?? node.Appearance ?? parentAppearance`, with `forced` the color of the
  outermost enclosing overlay (none outside overlays); a `MeshNode` yields one `SceneInstance` with
  the next order index; a `GroupNode`, a `CsgNode` or an `OverlayNode` pushes its children in
  reverse so the first
  child is visited (and numbered) first (every operand of a boolean node and every overlay child is
  enumerated; the
  traversal never drops a node kind). The traversal uses no recursion, so a hierarchy nested ten
  thousand levels deep cannot overflow the call stack.

#### Error Handling

Construction-time validation as listed above; an invalid `Transform` throws `ArgumentException`
when the node is initialized. Traversal and bounds computation cannot fail on a constructed scene.
Nothing is caught locally.

#### Dependencies

The `Mesh` and `DoublePrecisionMath` units (`Mesh`, `Matrix4x4D`, `BoundingBox3D`), the core
`CanvasNet` system's `Canvas` subsystem (`Rgba32`, for `NodeAppearance`), and the .NET base class
library (`System.Collections.ObjectModel`).

#### Callers

`SceneRenderer` calls `Scene.EnumerateInstances` and `Scene.ComputeBounds` (the latter through
`RenderFitted`); callers construct scenes directly; `PrimitiveTessellator` meshes are wrapped in
`MeshNode`s by callers.
