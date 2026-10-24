### PolygonTriangulator

<!-- cspell:ignore tessellator triangulator triangulators triangulation triangulated polyhedra collinear -->
<!-- cspell:ignore dyadic subnormal reflex -->

The `PolygonTriangulator` unit is the 2D polygon facility of the `Tessellation` subsystem. It
consists of the public immutable `PolygonRegion` type (`PolygonRegion.cs`, with the internal
`ContourInfo` record in `ContourInfo.cs`), the public static `PolygonTriangulator` class
(`PolygonTriangulator.cs`, which also holds the internal `PolygonBridger` and `EarClipper` classes),
and the internal static `PolygonPredicates` class (`PolygonPredicates.cs`), all in the
`DemaConsulting.CanvasNet.Render3D.Tessellation` namespace.

#### Purpose

Extruded caps and swept end caps must cover a 2D region exactly, reuse the vertex indices of the side
walls (so the solid is watertight without T-junctions), and keep every collinear vertex. A fan or a
convex-only algorithm cannot do this for concave profiles or profiles with holes. This unit
therefore provides a validated region type, which is the single place where polygon validity is
decided, and a triangulator that adds no Steiner points and never moves or drops a vertex. All
decisions use exact arithmetic, so the result never depends on an epsilon.

#### Data Model

`PolygonRegion` is immutable and thread-safe. Its constructor takes contours (each an implicitly
closed list of `Vector2D` points in any winding) and stores private copies, so changing the caller's
lists afterward has no effect. It exposes:

- `Contours`: the normalized contours in input order. Each starts at its original first point and
  has no closing or consecutive duplicate; collinear points are kept.
- `Vertices` / `VertexCount`: all contour vertices concatenated in contour order; this is the index
  space of the triangulator output.
- `Area`: solid minus holes plus islands (the sum of the signed areas of the normalized contours,
  computed with compensated summation), finite and never negative.
- `IsEmpty` and the shared `Empty` instance: a region without contours has no area and no triangles.
- Internally, a `ContourInfo` table (start index, vertex count, nesting depth, innermost parent) used by
  the triangulator and the `Extruder`.

**Even-odd rule.** The depth of a contour is the number of other contours that contain it. An even
depth is a solid boundary and is normalized counter-clockwise; an odd depth is a hole and is
normalized clockwise, whatever winding was supplied. An island inside a hole is solid again. In every
normalized contour the solid lies on the left of the direction of travel, which is what lets the
`Extruder` use one wall and cap formula for outer walls and hole walls alike. Reversal keeps the first
vertex first (`v0, vn-1, ..., v1`).

#### Key Methods

- `PolygonRegion(IEnumerable<IReadOnlyList<Vector2D>> contours)`: cleans, validates, nests, and
  normalizes (see _Error Handling_ for the rejection contract). Steps: snapshot each contour and drop
  consecutive and closing duplicates; reject contours with fewer than three distinct vertices or zero
  area; check simplicity of all edges of all contours with an exact sweep along X; classify nesting by
  an exact point-in-polygon test of each contour's first vertex (valid because the simplicity check
  guarantees no boundary contact); normalize the winding by parity.
- `PolygonTriangulator.Triangulate(PolygonRegion region)`: returns triangle corner indices into
  `region.Vertices`, three per triangle, strictly counter-clockwise, using only the region's own
  vertices. An internal `TriangulateToArray` returns the private array without a read-only wrapper
  for the `Extruder`.
- `PolygonPredicates` (internal): `Orient2D` (exact sign of the cross product), `SegmentsIntersect`
  (closed segments, including touching and collinear overlap), `PointInPolygon`, `InsideCorner`,
  `PointInClosedTriangle`, `PointOnSegment`, `InBoundingBox`, and `Orient2DExact`.

**Exact predicates.** `Orient2D` evaluates the determinant in `double` together with a rigorous
forward error bound and trusts the sign only when the magnitude exceeds the bound; otherwise (nearly
collinear, exactly collinear, or extreme magnitudes) it re-evaluates exactly with
`System.Numerics.BigInteger`, because every finite `double` is an integer multiple of 2^-1074. The
exact path is rare and its cost is irrelevant for CAD-sized profiles. `BigInteger` is part of the
base class library, so no package dependency is added. No epsilon appears anywhere in this unit.

**Triangulation algorithm.** For each solid contour (outer boundary or island), the holes it
directly contains are merged into its ring by zero-width bridges, rightmost hole vertex first. For
each hole, candidate partner vertices of the merged ring are tried in order of distance to the hole's
rightmost vertex, and the first candidate whose connecting segment leaves both corners through their
interior wedges and touches no other edge is the bridge (a visible vertex always exists by the +X ray
argument). The ring `... P, M, hole(M ...), M, P ...` is weakly simple and lists original vertex
indices, so a bridge endpoint appears twice but maps to the same index. The ring is then ear-clipped
on a doubly linked list. A vertex is an ear only when it is a strictly convex corner and the closed
ear triangle contains no other reflex or collinear ring vertex (ignoring duplicates of the three
corners created by bridges). Consequently no zero-area triangle is emitted, every collinear vertex
remains a triangle vertex, and a collinear vertex on a would-be diagonal blocks that diagonal, so no
T-junction can arise. The first ear found scanning from a rotating pointer is clipped, which makes
the result deterministic. A convex ring costs one check per vertex. For every other ring, each
candidate ear is tested against the list of reflex and collinear entries `r`, so the worst case is
`O(n r)`, up to quadratic in the number of vertices. Two measures keep the constant small without
changing any result: a bounding-box test rejects reflex vertices that cannot lie in the closed ear
triangle before the exact predicates run, and entries that have turned convex are dropped lazily while
scanning instead of by a separate pass per clip. Measured in a Release build, a comb of 2,000 teeth
(8,000 vertices, many reflex vertices inside tall ear bounding boxes) triangulates in about 2 seconds
(15 seconds before these measures); larger concave rings keep growing roughly quadratically. There is no
cancellation. This is adequate for CAD-sized profiles; callers with very large concave profiles
should split them.

_Invariant of the reflex list._ Clipping an ear shrinks the interior angle at both neighbors, because
the ear triangle lies inside the polygon and the new edge runs inside the neighbors' old wedges. A
convex neighbor therefore stays convex, so the set of reflex or collinear entries only ever shrinks and
needs no insertions. This also holds for the duplicated vertices of bridges, whose wedges lie on either
side of the zero-width bridge. The fuzz tests, plus an exploratory run of about 60,000 lattice polygons
with many collinear points and aligned multi-hole layouts that asserted no neighbor ever became newly
reflex, found no counterexample.

**Invariants** (asserted by the tests for every valid region): the triangle count is exactly
`V + 2H - 2P` (V vertices, H holes, P solid contours); every triangle is strictly counter-clockwise;
the triangle areas sum to `Area`; every vertex is used; every contour edge appears once in the
direction of its contour, and every other edge appears once in each direction (so the triangles tile
the region exactly and leave holes uncovered).

#### Error Handling

The `PolygonRegion` constructor never leaves malformed input as undefined behavior. It throws
`ArgumentNullException` for a null contour list or a null contour, and `ArgumentException` (the
message names the contour and the point or edge) for:

- a point that is not finite;
- finite coordinates so extreme (beyond about 1e154) that the region's area overflows to infinity or
  NaN: the region could not report a usable area or volume;
- a contour with fewer than three distinct vertices after cleaning, or with zero area (all
  collinear);
- a contour that crosses itself (a bow tie) or touches itself (a repeated non-consecutive point, or
  edges that fold back onto each other as a spike);
- two contours that cross or **touch** each other (a shared point, or a vertex lying on another
  contour's edge).

Touching contours are rejected rather than repaired because their extrusion would have non-manifold
vertices. Consecutive duplicates, the closing duplicate, and collinear points are accepted and
normalized, not rejected. Callers that must accept such input (for example a language front end)
catch the `ArgumentException` and report it through their own channel.

`PolygonTriangulator.Triangulate` throws `ArgumentNullException` for a null region. It throws
`InvalidOperationException` only if an internal invariant is violated (no ear or no visible bridge
vertex exists), which cannot happen for a region constructed through `PolygonRegion` because the
exact predicates make validation and triangulation consistent; fuzz tests guard this.

#### Dependencies

The `DoublePrecisionMath` unit (`Vector2D`) of the `SceneModel` subsystem, and the platform's
`System.Numerics.BigInteger`. No dependency on `Mesh` or on `SceneRendering`.

#### Callers

The `Extruder` unit (caps and end caps, and the `PolygonRegion` input of both of its methods), the
2D factories of `PrimitiveTessellator`, and callers that need a triangulation or a validated region
directly.
