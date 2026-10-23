### SceneGraph

The `SceneGraph` unit is the third software unit in the `SceneModel` subsystem. It comprises five
public types in the `DemaConsulting.CanvasNet.Render3D.SceneModel` namespace plus one internal
type:

- `SceneNode` (abstract class): base of the node hierarchy, carrying a transform and an optional
  appearance
- `MeshNode` (sealed class): a leaf node referencing a `Mesh`
- `GroupNode` (sealed class): a node grouping child nodes
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
  assembly: the renderer can switch exhaustively on node type, and a later phase adds a node type
  without breaking callers.
- **`MeshNode`**: a non-null `Mesh`. **`GroupNode`**: a read-only list `Children`, copied from the
  constructor argument; empty groups are allowed; a group's meaning in this phase is the implicit
  union of its children's geometry (there is no constructive solid geometry yet).
- **`NodeAppearance(Rgba32 Color)`**: a surface color; the alpha of the color is ignored in this
  phase (surfaces are opaque).
- **`Scene`**: a non-null `Root`, and a lazily computed, cached `BoundingBox3D`.
- **`SceneInstance(Mesh, World, Appearance, Order)`**: the composed world matrix, the effective
  appearance, and the zero-based pre-order index of the mesh node, which is the instance's draw
  priority.

A node may be shared: the same `SceneNode` instance may appear under several parents (a directed
acyclic graph) and is then enumerated once per path. Because nodes are immutable and constructed
bottom-up, cycles cannot be built.

#### Key Methods

- `MeshNode(Mesh)`: null mesh throws `ArgumentNullException`.
- `GroupNode(IEnumerable<SceneNode>)`: a null sequence throws `ArgumentNullException`; a null
  element throws `ArgumentException`; the sequence is copied.
- `Scene(SceneNode root)`: null root throws `ArgumentNullException`.
- `Scene.ComputeBounds()`: the **tight** bounds, computed by transforming every vertex of every
  instance by its world matrix in double precision and taking the union (not the union of
  transformed bounding boxes, which can be loose under rotation). An empty scene or a scene of
  empty groups yields `BoundingBox3D.Empty`. The result is cached.
- `Scene.EnumerateInstances()` (internal): an **iterative, explicit-stack, depth-first pre-order**
  traversal. For each node: `world = node.Transform * parentWorld` (child transform applied first);
  `appearance = node.Appearance ?? parentAppearance`; a `MeshNode` yields one `SceneInstance` with
  the next order index; a `GroupNode` pushes its children in reverse so the first child is visited
  (and numbered) first. The traversal uses no recursion, so a hierarchy nested ten thousand levels
  deep cannot overflow the call stack.

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
