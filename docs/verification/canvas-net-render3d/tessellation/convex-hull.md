### ConvexHull Unit Verification Design

<!-- cspell:ignore coplanar collinear lexicographic lexicographically canonicalized polytope facet facets -->
<!-- cspell:ignore simplex simplices tetrahedra tetrahedron Caratheodory ulp ulps subnormal subnormals -->

This document describes the unit-level verification strategy for the `ConvexHull` unit: `Hull3D` and
`Hull2D`, the internal incremental engine `HullBuilder3D`, and the exact predicate `SolidPredicates`.

#### Verification Approach

The unit is verified by computing hulls with analytic geometry, against an independent exhaustive oracle, on
degenerate and extreme input, and by asserting properties of the result that are known exactly.

**Analytic oracles.** A cube with interior points is the twelve-triangle box with volume `s^3` and area
`6 s^2`; the unit-corner tetrahedron has four faces and volume one sixth; the vertices of a tessellated sphere
have the volume and surface area of that sphere polytope (relative `1e-12`, for five segment counts); the hull of
a cube with its center, face centers and edge midpoints is again the plain cube (eight vertices, twelve
triangles); a 5 x 5 coplanar grid with an apex is a square pyramid with volume `16 / 3`, whether the apex sorts
before or after the grid.

**Independent exhaustive oracle.** `ConvexHull_Hull3D_RandomLatticeInputs_MatchTheBruteForceOracle` draws 420
seeded random subsets of 3 x 3 x 3 and 4 x 4 x 4 integer lattices (4 to 30 distinct points, shuffled and with
repeated points, and every seventh case restricted to a plane or a line), which produces the collinear, coplanar
and duplicate configurations that break incremental hull algorithms. The reference uses only exact `long`
arithmetic and shares no code with the production predicates: a set spans a volume when some tetrahedron has a
non-zero determinant (otherwise `Hull3D` must throw `ArgumentException`), and a point is an *extreme point* when it
is in no segment, triangle or tetrahedron of affinely independent other points (Caratheodory, found by
exhaustive search). For every full-dimensional set the test asserts the complete characterization of a convex
hull: the mesh is closed with positive volume, `F = 2V - 4`, every vertex is an input point, every input point
lies on or below the plane of every triangle (exact `Orient3DExact`, so every triangle is a supporting plane),
and every extreme point is a vertex. These properties together determine the hull, so no volume reference is
needed.

**Predicates.** `SolidPredicates.Orient3D` is compared with the arbitrary-precision `Orient3DExact` on 6000
random inputs at scales `1e-30` to `1e30`, and with an independent 128-bit evaluation on random lattice points of
which a third lie exactly on the plane; probes one to sixteen units in the last place off the plane of three
non-axis-aligned points (where the naive double determinant misjudges at least one probe); subnormal offsets
above and below the plane of a `1e300` triangle; coordinates at `double.MaxValue` whose differences overflow;
tiny `1e-200` coordinates whose products underflow; and 2000 random inputs with exponents from -320 to 308. The
sign convention is checked against the unit triangle and against the faces of the outward-wound tetrahedron
mesh.

**Degenerate and invalid input.** A collinear prefix, a coplanar prefix with the apex last, the apex first,
repeated points and a coplanar prefix with the apex in the middle of the sort order are handled; fewer than four
distinct points, collinear points, coplanar points (a square and a 400-point tilted grid) throw
`ArgumentException` with a message that contains "degenerate"; a null sequence and NaN or infinite coordinates
in each of the three components throw. `Hull2D` is checked on a square with interior and collinear boundary
points (strict counter-clockwise starting at the smallest corner), degenerate sets (nothing, one point, two
points, collinear points in both slopes) and a point one unit in the last place above, below and on a line.

**Exactness at extremes.** Cubes of size `1e200`, `1e-100` and `3e-5` with an interior point keep eight vertices
and twelve triangles; a point one unit in the last place above the center of a face is extreme (nine vertices,
fourteen triangles) and one below is interior; a point `1e-150` outside or inside a cube of side `1e150` is
classified correctly.

**Determinism.** Twelve shuffled copies with repeated points of a degenerate lattice and of a random cloud give
byte-identical output (comparing the bit patterns of every vertex component and the index list); a cube written
with positive and negative zeros and duplicates gives the same bits as the clean cube and no negative zero; the
planar hull of shuffled lattice points with repeats is identical.

**Valid operands, bounds and cancellation.** Random clouds, sphere vertices, a lattice, a cube and a pyramid pass
`Render3DTestHelpers.AssertValidCsgOperand` and `AssertClosedAndOutward`. A 200,000-point input (random points in
a ball, a lattice and points on a sphere) completes within a generous time limit with a closed surface,
`F = 2V - 4`, and a sample of the input on or below a sample of the triangles; a 141 x 141 coplanar grid with an
apex is a five-vertex pyramid. A pre-canceled token, and a token canceled while a lazy 100,000-point sequence is
consumed, throw `OperationCanceledException` for both methods.

The unit depends only on `PolygonPredicates` and the `SceneModel` types, so no mocking or stubbing is required.
The only link to another subsystem is test-only: `Render3DTestHelpers.AssertValidCsgOperand` runs the internal
`SceneRendering.CsgOperandValidator` through `InternalsVisibleTo`.

Unit tests reside in `ConvexHullTests.cs` and `SolidPredicatesTests.cs` within the
`DemaConsulting.CanvasNet.Render3D.Tests` project, in the `Tessellation` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test creates its own points and meshes; random sources are seeded

#### Unit-Level Test Scenarios

##### CanvasNetRender3D-Tessellation-ConvexHull-Hull3DSolid: The Hull Is A Closed Solid With Analytic Volume

**Tests**: `ConvexHullTests.ConvexHull_Hull3D_CubeWithInteriorPoints_IsClosedBoxWithAnalyticVolume`,
`ConvexHullTests.ConvexHull_Hull3D_Tetrahedron_HasFourFacesAndAnalyticVolume`,
`ConvexHullTests.ConvexHull_Hull3D_SphereVertices_VolumeMatchesTheSpherePolytope`

Asserts the vertex, triangle, volume and area values of the cube, the tetrahedron and five sphere
tessellations, and that each result is closed and outward.

##### CanvasNetRender3D-Tessellation-ConvexHull-ExtremePoints: Vertices Are Exactly The Extreme Points

**Tests**: `ConvexHullTests.ConvexHull_Hull3D_InteriorAndFacetPoints_AreNotVertices`,
`ConvexHullTests.ConvexHull_Hull3D_RandomLatticeInputs_MatchTheBruteForceOracle`

Asserts that the interior point, face centers and edge midpoints of a cube are not vertices, and the
exhaustive-oracle characterization described above for hundreds of degenerate lattice sets.

##### CanvasNetRender3D-Tessellation-ConvexHull-DegenerateInput: Degenerate Input Is Handled Or Rejected

**Tests**: `ConvexHullTests.ConvexHull_Hull3D_CollinearCoplanarAndDuplicatePrefix_IsHandled`,
`ConvexHullTests.ConvexHull_Hull3D_FewerThanFourOrFlatInput_ThrowsArgumentException`

Asserts the collinear, coplanar, apex-first and repeated-point cases, and the nine flat inputs that must throw an
`ArgumentException` naming the degenerate case.

##### CanvasNetRender3D-Tessellation-ConvexHull-InvalidInput: Null And Non-Finite Input Is Rejected

**Tests**: `ConvexHullTests.ConvexHull_Hull3D_NullOrNonFiniteInput_Throws`,
`ConvexHullTests.ConvexHull_Hull2D_NullOrNonFiniteInput_Throws`

Asserts `ArgumentNullException` for a null sequence and `ArgumentException` for NaN and both infinities in
every component.

##### CanvasNetRender3D-Tessellation-ConvexHull-ExactPredicates: Orientation Signs Are Exact

**Tests**: `SolidPredicatesTests.SolidPredicates_Orient3D_RandomInputs_MatchesTheExactSign`,
`SolidPredicatesTests.SolidPredicates_Orient3D_NearlyCoplanar_ReturnsTheExactSign`,
`SolidPredicatesTests.SolidPredicates_Orient3D_ExtremeMagnitudes_FallBackToExact`,
`SolidPredicatesTests.SolidPredicates_Orient3D_PositiveAboveTheCounterClockwisePlane`,
`ConvexHullTests.ConvexHull_Hull3D_ExtremeMagnitudes_AreExact`

Asserts the predicate against the exact references described above, the sign convention, and the hull
classification of extreme and ulp-distance points.

##### CanvasNetRender3D-Tessellation-ConvexHull-Determinism: The Result Depends Only On The Point Set

**Tests**: `ConvexHullTests.ConvexHull_Hull3D_SamePointSetInAnyOrder_IsByteIdentical`,
`ConvexHullTests.ConvexHull_Hull3D_NegativeZeroAndDuplicates_AreCanonicalized`,
`ConvexHullTests.ConvexHull_Hull2D_SamePointSetInAnyOrder_IsIdentical`

Asserts byte-identical output for shuffled and repeated input, canonical zeros, and identical planar hulls.

##### CanvasNetRender3D-Tessellation-ConvexHull-ValidOperand: Results Are Valid CSG Operands

**Tests**: `ConvexHullTests.ConvexHull_Hull3D_Results_AreClosedOutwardAndValidCsgOperands`

Applies `AssertValidCsgOperand` and `AssertClosedAndOutward` to the hull of five assorted point sets.

##### CanvasNetRender3D-Tessellation-ConvexHull-Hull2D: The Planar Hull Is Strict And Exact

**Tests**: `ConvexHullTests.ConvexHull_Hull2D_SquareWithInteriorAndCollinearPoints_IsStrictCounterClockwise`,
`ConvexHullTests.ConvexHull_Hull2D_DegenerateInputs_ReturnThePointOrTheSegment`,
`ConvexHullTests.ConvexHull_Hull2D_NearlyCollinearPoints_UseTheExactPredicate`

Asserts the square, the degenerate results and the one-ulp classification described above.

##### CanvasNetRender3D-Tessellation-ConvexHull-Bounds: Work Is Bounded And Cancellable

**Tests**: `ConvexHullTests.ConvexHull_Hull3D_LargeInput_CompletesWithEulerCounts`,
`ConvexHullTests.ConvexHull_Hull3D_Canceled_ThrowsOperationCanceledException`,
`ConvexHullTests.ConvexHull_Hull2D_Canceled_ThrowsOperationCanceledException`

Asserts the large-input and coplanar-prefix results within a generous time limit, and cancellation before and
during the computation.

#### Acceptance Criteria

A unit-level test run passes when every scenario above passes without error or exception beyond those
explicitly asserted. Any unexpected exception, wrong exception type, wrong count, a predicate sign that differs
from the exact reference, a wrong volume beyond the stated tolerance, or a result that is not a valid
constructive-solid-geometry operand constitutes a failure.
