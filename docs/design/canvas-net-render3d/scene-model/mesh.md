### Mesh

<!-- cspell:ignore Orientable orientable -->

The `Mesh` unit is the second software unit in the `SceneModel` subsystem. It comprises two public
types in the `DemaConsulting.CanvasNet.Render3D.SceneModel` namespace plus one internal helper:

- `Mesh` (sealed class): an immutable, validated, indexed triangle mesh
- `MeshBuilder` (sealed class): a mutable construction helper that welds identical vertices
- `MeshTopology` (internal static class): closedness, topology-defect classification, signed volume,
  and surface-area algorithms
- `MeshTopologyDefects` (internal flags enum): the defect kinds the analysis reports

#### Purpose

`Mesh` is the one geometry representation the renderer understands: a list of `Vector3D` vertices
and a flat list of triangle indices. It guarantees that every mesh that exists is structurally
valid, and it provides the topological queries (`IsClosed`, `SignedVolume`, `SurfaceArea`) that
callers and tests use to check that a mesh is closed and outward-oriented. `MeshBuilder` lets
generators and callers assemble a welded mesh without managing vertex de-duplication.

#### Data Model

- **`Mesh`**: private arrays of `Vector3D` vertices and `int` triangle indices (three per
  triangle), exposed as `IReadOnlyList<>` views (`Vertices`, `TriangleIndices`) plus `VertexCount`
  and `TriangleCount`. `Bounds` is computed once at construction. Invariants: the index count is a
  multiple of three; every index addresses a vertex; every vertex component is finite. A mesh may
  have zero triangles (and then has empty bounds). Winding contract: counter-clockwise as seen from
  outside (the right-hand-rule normal points outward). Internal `VertexSpan` and `IndexSpan` give
  the renderer zero-copy read-only access.
- **`MeshBuilder`**: a `List<Vector3D>` of vertices, a `List<int>` of indices, and a
  `Dictionary<Vector3D, int>` that maps a position to its index. Vertex welding is by **exact
  value equality** of the three components, with `-0.0` and `+0.0` treated as equal; no tolerance is
  applied.
- **`MeshTopology`**: stateless.
- **`MeshTopologyDefects`**: a `[Flags]` enum: `None`, `BoundaryEdge`, `NonManifoldEdge`,
  `InconsistentWinding`, `DegenerateTriangle`.

#### Key Methods

- `Mesh(IEnumerable<Vector3D> vertices, IEnumerable<int> triangleIndices)`: copies both sequences
  into private arrays (so later mutation of the caller's collections has no effect), then validates
  the invariants above.
- `Bounds`: the axis-aligned box of the vertices (empty for a mesh with no vertices).
- `IsClosed`: computed on first request and cached (through the cached topology defects). A mesh
  is closed when it has at least one triangle and the topology analysis (below) reports no defect:
  every **directed** edge `a -> b` occurs exactly once across all triangles, its reverse `b -> a`
  also occurs, and no triangle repeats a vertex index. A mesh made of several disjoint closed shells
  is closed.
- `MeshTopology.Analyze(indices)` (internal): classifies every defect in one pass. Each triangle
  that repeats an index is flagged `DegenerateTriangle` and its edges are ignored (so one collapsed
  triangle does not also appear as edge defects). For every other triangle edge it records the
  **undirected** edge key (lower index, higher index) with a direction flag, sorts the keys (with
  the flags as a parallel array) and, per run of equal keys, counts the traversals low-to-high
  (`n_ab`) and high-to-low (`n_ba`): one traversal in total is a `BoundaryEdge`; three or more is a
  `NonManifoldEdge`; exactly two in the same direction is `InconsistentWinding`; one in each
  direction is correct. The result is the OR of the defects found; an empty list has none.
  O(E log E) with two array allocations. `IsClosedOrientable` is `length > 0 && Analyze == None`.
- `MeshTopology.Analyze(vertices, indices)` (internal): the index-only analysis plus
  `DegenerateTriangle` for every triangle with distinct indices whose vertices are coincident or
  collinear, judged by the same criterion the renderer discards by
  (`MeshTopology.TryGetFaceNormal`: the normalized cross product of the edges is zero or
  non-finite). Its edges stay in the edge table, so such a triangle does not also appear as edge
  defects of its neighbors. A triangle that is merely very thin but has a representable normal is
  rendered, not discarded, and is not reported.
- `TopologyDefects` (internal): `Analyze` over the mesh's indices, computed lazily on first use and
  cached (a benign race because the analysis is deterministic). `IsClosed` is defined through it and
  therefore stays purely topological. `OperandDefects` (internal) is the cached
  `Analyze(vertices, indices)` result, used by the renderer's operand validation so that every
  triangle the renderer would drop from a CSG operand is reported.
- `IsInverted` (internal): true when the mesh is closed (no topology defect) and its signed volume is
  below the negative of the rounding error bound of that volume
  (`MeshTopology.SignedVolume` with its `errorBound` output), i.e. a closed mesh wound inside out.
  The bound is `(n + 12) * 2^-52 * sum(|a| . (|b| x |c|)) / 6` over the `n` triangles'
  reference-relative vertices, so it scales with the mesh's own magnitude instead of a fixed
  fraction of its bounds: a thin valid solid (for example a reversed 1 x 1 x 1e-12 box) is detected,
  while a zero-volume closed surface, whose volume is only rounding noise, is not. A hollow
  mesh (an outward outer shell and an inward inner shell) keeps a positive total volume and is not
  inverted; open and empty meshes are never inverted because their volume is meaningless.
- `SignedVolume`: the sum of signed tetrahedron volumes `a . (b x c) / 6` taken relative to the
  center of the bounds (so the value keeps its precision for a mesh far from the origin); positive
  for a closed, outward-CCW mesh, negative when inside-out; 0 for an empty mesh.
- `SurfaceArea`: the sum of triangle areas.
- `Transform(Matrix4x4D)`: returns a new mesh with every vertex transformed by `TransformPoint`;
  **when the matrix determinant is negative, two indices of every triangle are swapped** so the
  result keeps outward-CCW winding. A non-finite matrix, or a transform that produces a non-finite
  vertex, throws `ArgumentException`.
- `Reverse()`: returns a new mesh with every triangle's winding flipped (the repair for an
  inside-out mesh).
- `MeshBuilder.AddVertex(Vector3D)`: returns the index of an existing identical vertex, or appends
  a new one; a non-finite position throws `ArgumentException`.
- `MeshBuilder.AddTriangle(a, b, c)`: validates every index (`ArgumentOutOfRangeException`) and that
  the three are distinct (`ArgumentException`) before modifying any state, so a failed call leaves
  the builder unchanged.
- `MeshBuilder.AddPolygon(ReadOnlySpan<int>)`: fan-triangulates a convex polygon around its first
  index, `(0,1,2), (0,2,3), ...`; fewer than three indices or any repeated index throws
  `ArgumentException`, and an out-of-range index throws `ArgumentOutOfRangeException`. All
  validation precedes any mutation, so a failed call leaves the builder unchanged. The polygon
  must be convex and counter-clockwise; no check is made.
- `MeshBuilder.Build()`: creates a new `Mesh` from the current state; repeatable, and each result is
  independent of the builder and of earlier results.

#### Error Handling

All validation is eager. `ArgumentNullException` for a null vertex or index sequence;
`ArgumentException` for a non-multiple-of-three index count, a non-finite vertex, a non-finite
transform or transform result, a degenerate or too-short polygon, or a non-finite builder vertex;
`ArgumentOutOfRangeException` for an index that does not address a vertex. Exceptions propagate to
the caller; nothing is caught locally.

#### Dependencies

The `DoublePrecisionMath` unit (`Vector3D`, `Matrix4x4D`, `BoundingBox3D`) and the .NET base class
library (`System.Collections.ObjectModel.ReadOnlyCollection<T>`).

#### Callers

`MeshNode` references a `Mesh`; `Scene` reads `Bounds`, `VertexSpan`, and `IndexSpan`;
`PrimitiveTessellator` builds every result through `MeshBuilder`; `SceneRenderer` reads the vertex
and index spans (and, for constructive solid geometry operand validation, `OperandDefects` and
`IsInverted`); callers use `IsClosed`, `SignedVolume`, `Transform`, and `Reverse` directly.
