### ConvexHull

<!-- cspell:ignore coplanar collinear lexicographic lexicographically canonicalized polytope polytopes facet facets -->
<!-- cspell:ignore Shewchuk cyclic simplex tetrahedron incremental monotone collinearity halfspaces coplanarity -->

The `ConvexHull` unit computes exact convex hulls of point sets. It is the public static class `ConvexHull`
(`ConvexHull.cs`: `Hull3D`, `Hull2D`) in the `DemaConsulting.CanvasNet.Render3D.Tessellation` namespace,
together with two internal helpers that belong to the same unit: `HullBuilder3D` (`HullBuilder3D.cs`, the
incremental engine) and `SolidPredicates` (`SolidPredicates.cs`, the exact 3D orientation predicate).

#### Purpose

`Hull3D` turns a set of points into a closed, welded, outward-oriented triangle `Mesh` with positive
`SignedVolume` whose vertices are exactly the strictly extreme points of the input; `Hull2D` returns the
strictly convex counter-clockwise polygon of a planar set. Every decision is an exact predicate decision (no
tolerance), the result depends only on the point **set** (any order and any repetition gives a byte-identical
result), and every `Hull3D` result is a valid constructive-solid-geometry operand. The hull is the foundation of
the OpenSCAD `hull` operation and of `MinkowskiSum`.

#### Data Model

`ConvexHull` is stateless and thread-safe. `Hull3D` copies the input into a sorted array of distinct canonical
points (finite values only; `-0.0` is canonicalized to `+0.0` by adding zero; exact duplicates are removed after
sorting by X, then Y, then Z) and hands it to a single-use `HullBuilder3D`. The builder keeps the surface of the
current hull as **face slots** in parallel arrays: three vertex indices and three neighbor indices per slot
(neighbor `k` lies across the edge `v[k] -> v[k+1]`), an alive flag, a per-insertion classification stamp and a
visibility flag, a LIFO free list of dead slots, and the most recently inserted vertex with one incident face.
The invariants between insertions are: (1) the live faces are the boundary of the convex hull of the points
inserted so far, with every edge shared by exactly two faces in opposite directions; (2) the last inserted
point is the lexicographic maximum of all points so far.

#### Key Methods

- `Hull3D(IEnumerable<Vector3D> points, CancellationToken cancellationToken = default)` returns the hull mesh.
  `Hull2D(IEnumerable<Vector2D> points, CancellationToken cancellationToken = default)` returns the polygon.
- `SolidPredicates.Orient3D(a, b, c, d)` returns the exact sign of `det[b - a; c - a; d - a]`: positive when `d`
  lies above the plane of the counter-clockwise triangle `a b c` (on the side its normal `(b - a) x (c - a)`
  points to, the mesh winding contract), negative below, zero when coplanar. It runs three stages, each used only
  when provably correct: a double-precision determinant with the rigorous static error bound `(7 + 56 eps) eps`
  times the permanent (Shewchuk's first-stage filter; eps = 2^-53), guarded against overflow and underflow by
  requiring every coordinate difference to be at most `1e100` and the permanent to be at least `1e-150`; a
  128-bit integer evaluation when all twelve coordinates are short binary fractions (at most 40 bits after
  alignment to a common exponent, so every intermediate fits in 126 bits); and otherwise an exact `BigInteger`
  evaluation (every finite double is an integer multiple of 2^-1074, so scaling by 2^1074 is exact).
  `NonDegenerateProjection` finds a coordinate plane in which a triangle has non-zero projected area (exactly) or
  reports collinearity.

**`Hull2D`.** Andrew's monotone chain on the sorted distinct points, with `PolygonPredicates.Orient2D`
deciding every turn (a point is popped while the turn is not strictly counter-clockwise). The result is strictly
convex (no collinear vertices), counter-clockwise and starts at the lexicographically smallest point. It is
O(n log n) in every case. A degenerate set returns no point, the one point, or the two extreme points (it never
throws; the caller decides what a point or a segment means).

**`Hull3D` algorithm (sorted incremental insertion).**

1. *Reduce the input* to the sorted array of distinct points. The result of the whole operation is a function of
   this array, hence of the point set.
2. *Initial simplex from the sorted prefix.* `p0 = s[0]`, `p1 = s[1]`, `p2` = the first point not collinear with
   them (exact test), `p3` = the first point off the plane of `p0 p1 p2` (`Orient3D != 0`). All points before `p3`
   in sorted order are coplanar with `p0 p1 p2`. Fewer than four points, no `p2` and no `p3` are the three
   degenerate cases and throw `ArgumentException`.
3. *Initial polyhedron*: the pyramid with apex `p3` over the exact planar hull (monotone chain in the coordinate
   plane where `p0 p1 p2` has area) of the coplanar prefix, with the base fan-triangulated and oriented so that
   the apex lies below it. The prefix points that are not corners of the base polygon lie inside the pyramid and
   never matter again.
4. *Insert the remaining points in sorted order.* A face is **visible** from `p` when `Orient3D > 0` (strictly
   above its plane). The visible faces are found by a flood fill through the neighbor links, seeded by a strictly
   visible face of the fan around the previously inserted vertex `q`. The visible region is replaced by the cone
   from `p` to the **horizon** (the edges between visible and non-visible faces): each horizon edge `(a, b)`
   becomes the face `(a, b, p)`, the outer neighbors are re-linked, and the cone faces are linked to each other
   around `p`. The new faces reuse the freed slots from the free list.
5. *Reduce to the canonical mesh.* After the last insertion the closure (every edge matched by the reverse edge
   of a live neighbor) is checked, coplanar neighboring triangles are merged into facets (union-find; two
   neighbors are in one facet when the apex of one is exactly in the plane of the other), the boundary loop of
   each facet is extracted and every vertex that continues a straight run is removed (exactly), which leaves
   the strictly convex polygon of the facet, and each polygon is fan-triangulated from its lowest-index corner.
   Facets are ordered by their corner-index sequences. The mesh vertices are the used corners in sorted order,
   and the Euler relation `F = 2V - 4` is verified. Points that lie on a face or an edge of the hull were
   vertices of the incremental surface at some moment (each insertion is a strict extreme point at that time) but
   are not corners of the final facets, so they disappear here; the result is the same polytope whatever
   non-extreme points were in the input.

**Why the insertion is correct (the proof sketch the implementation relies on).**

- *Every insertion is a strict vertex.* Because the points are inserted in lexicographic order, `p` is the
  lexicographic maximum of all points so far; the lexicographic order is compatible with the vector-space
  structure, so `p` cannot be a convex combination of the others and is a strict extreme point.
- *A seed face exists around `q`.* The tangent cone of the current hull at its vertex `q` consists only of
  directions `x - q` that are lexicographically non-positive (every hull point is at most `q`), while `p - q` is
  lexicographically positive. If no face at `q` were strictly visible, `p` would lie in all closed halfspaces of
  the faces at `q`, which intersect in `q +` the tangent cone, a contradiction. The fan of `q` is therefore
  scanned until a strictly visible face is found; its length is amortized by the number of faces created by the
  previous insertion (the fan of the last inserted vertex is exactly its cone).
- *The visible faces are connected and the horizon is one simple cycle.* The strictly visible part of the
  surface of a convex polytope is a topological disc, and coplanar triangles have the same visibility because
  the predicate is exact; around any vertex the visible faces form a contiguous arc (the normals of the faces at
  a convex vertex are the vertices of a convex spherical polygon, which a great circle splits into at most two
  arcs). The flood fill therefore reaches every visible face and the horizon neither pinches nor splits.
- *Strict visibility never creates a degenerate triangle.* A horizon edge has one strictly visible face, so `p`
  is off its plane and cannot be collinear with the edge. A non-visible face across a horizon edge may be
  coplanar with `p`; the new triangle is then coplanar with it on its outer side (the part of the plane above the
  other face's plane lies beyond the edge), so it neither overlaps nor creates a T-junction. If the face across
  the edge is strictly below `p`, the plane through the edge and `p` supports the new hull (the pencil-of-planes
  argument), so the new surface is again the boundary of the hull.

An internal inconsistency (which the argument above excludes) throws `InvalidOperationException` instead of
returning a defective mesh; the machine check is the exhaustive lattice oracle in the unit tests.

**Complexity.** Sorting is O(n log n). An insertion costs the fan length of the previous vertex (amortized free),
the visible faces and the horizon, so the construction is O(n log n) in total for typical input and **O(n h)
worst case** (h = number of hull vertices; adversarial inputs such as points on a moment curve make every
insertion see a large part of the surface, which is O(n^2)). The canonical reduction is O(F) plus one exact
coplanarity test per edge. Memory is O(n); the result has at most `2n - 4` triangles. The work is bounded by
the input size and polls the cancellation token every few thousand elementary steps (`Array.Sort` itself
cannot be interrupted). The hull is built in internal arrays and the `Mesh` is allocated at the end.

#### Error Handling

A null sequence throws `ArgumentNullException`; a non-finite coordinate throws `ArgumentException`. `Hull3D`
throws `ArgumentException`, whose message contains "degenerate" and names the case, for fewer than four
distinct points, collinear points and coplanar points; `Hull2D` does not throw for degenerate input. A canceled
token throws `OperationCanceledException` (checked on entry, while the input is copied, and during the
construction). `InvalidOperationException` is reserved for an internal-invariant violation. In exact arithmetic
no result triangle has zero area; for coordinates below about 1e-150 the double-precision cross product used by
mesh consumers (`TryGetFaceNormal`) can underflow, which is outside the supported range (documented in the XML
remarks; the Scad coordinate guard keeps inputs within 1e100).

#### Dependencies

`PolygonPredicates` (`Orient2D` and the exact integer scaling of the `PolygonTriangulator` unit of this
subsystem) and the `Mesh`, `Vector2D` and `Vector3D` types of the `SceneModel` subsystem. No dependency on
`SceneRendering`; the unit tests use `Render3DTestHelpers.AssertValidCsgOperand` (test-only), which runs the
internal `CsgOperandValidator` through `InternalsVisibleTo`.

#### Callers

The `MinkowskiSum` unit (the convexity test and the pairwise sums) and, through the public API, callers that
need the hull of a point set; the Scad geometry builder (`hull` module) uses `Hull3D` and `Hull2D`.
