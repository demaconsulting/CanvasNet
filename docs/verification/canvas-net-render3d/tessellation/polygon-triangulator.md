### PolygonTriangulator Unit Verification Design

<!-- cspell:ignore triangulator triangulation triangulated collinear reflex Ngon -->

This document describes the unit-level verification strategy for the `PolygonTriangulator` unit:
the `PolygonRegion` type, the internal `PolygonPredicates` class, and the `PolygonTriangulator`
class.

#### Verification Approach

The unit is verified by building regions with analytic geometry and asserting properties that are
known in closed form, using a shared private helper, `AssertExactTriangulation(region, indices)`,
that checks the invariants of an exact triangulation for every region it is given:

- the triangle count is `V + 2H - 2P` (V vertices, H holes, P solid contours);
- every triangle is strictly counter-clockwise (exact `Orient2D` greater than zero);
- the triangle areas sum to `region.Area` (relative tolerance `1e-12`);
- every vertex is used by at least one triangle;
- every contour edge occurs exactly once in the direction of its contour and never reversed, and every
  other edge occurs exactly once in each direction. This proves that holes stay uncovered, that no
  triangles overlap, and that the triangulation is a manifold whose only boundary is the contours.

Named polygons (square, L, arrow, comb, C, sawtooth, star, a regular n-gon, rectangles with many
collinear points, rings, a grid of holes, an island inside a hole with a second outer contour, a hole
whose bridge ray passes a reflex vertex, a hole vertex collinear with outer vertices) cover the
deterministic cases. Two fuzz tests with fixed-seed `System.Random` cover randomized star polygons
(seeds 0 to 199) and randomized hole layouts (seeds 0 to 99); the seeds are fixed, so the tests are
deterministic and any failure is reproducible. Region validation is verified with one test per
rejection rule, each asserting `ArgumentException` (or `ArgumentNullException`). The exact predicates
are verified against an independent integer reference evaluation and against classical
near-collinear inputs on which a naive double-precision determinant has the wrong sign. The unit
depends only on `Vector2D`, so no mocking or stubbing is required.

Unit tests reside in `PolygonRegionTests.cs`, `PolygonTriangulatorTests.cs`, and
`PolygonPredicatesTests.cs` within the `DemaConsulting.CanvasNet.Render3D.Tests` project, in the
`Tessellation` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test creates its own regions; random generators use fixed seeds

#### Unit-Level Test Scenarios

##### CanvasNetRender3D-Tessellation-PolygonTriangulator-RegionModel: Regions Are Normalized By Nesting Parity

**Tests**: `PolygonRegionTests.PolygonRegion_Constructor_ClockwiseOuter_IsNormalizedToCounterClockwise`,
`PolygonRegionTests.PolygonRegion_Constructor_CounterClockwiseHole_IsNormalizedToClockwise`,
`PolygonRegionTests.PolygonRegion_Constructor_IslandInHole_HasEvenDepthAndCounterClockwise`,
`PolygonRegionTests.PolygonRegion_Constructor_ClosingAndRepeatedPoints_AreRemoved`,
`PolygonRegionTests.PolygonRegion_Constructor_CollinearPoints_AreKept`,
`PolygonRegionTests.PolygonRegion_Constructor_NoContours_IsEmptyWithZeroArea`,
`PolygonRegionTests.PolygonRegion_Area_RingWithHoleAndIsland_IsAnalytic`,
`PolygonRegionTests.PolygonRegion_Contours_AreSnapshotsOfInput`

Asserts that a clockwise outer contour is reversed to counter-clockwise and a counter-clockwise hole
to clockwise, that an island inside a hole has even depth and counter-clockwise winding, that closing
and consecutive duplicate points are removed while collinear points are kept, that a region without
contours is empty with zero area, that the area of a ring with a hole and an island is the analytic
value, and that later changes to the caller's lists do not affect the region.

##### CanvasNetRender3D-Tessellation-PolygonTriangulator-RegionValidation: Invalid Polygons Are Rejected

**Tests**: `PolygonRegionTests.PolygonRegion_Constructor_FewerThanThreeDistinctPoints_Throws`,
`PolygonRegionTests.PolygonRegion_Constructor_ZeroAreaContour_Throws`,
`PolygonRegionTests.PolygonRegion_Constructor_NonFinitePoint_Throws`,
`PolygonRegionTests.PolygonRegion_Constructor_AreaNotFinite_Throws`,
`PolygonRegionTests.PolygonRegion_Constructor_NullArguments_Throw`,
`PolygonRegionTests.PolygonRegion_Constructor_SelfIntersectingBowTie_Throws`,
`PolygonRegionTests.PolygonRegion_Constructor_SpikeOverlappingEdges_Throws`,
`PolygonRegionTests.PolygonRegion_Constructor_RepeatedNonConsecutivePoint_Throws`,
`PolygonRegionTests.PolygonRegion_Constructor_CrossingContours_Throws`,
`PolygonRegionTests.PolygonRegion_Constructor_TouchingContours_Throws`

Asserts the documented exception for each rejection rule: fewer than three distinct points, a
zero-area (collinear) contour, a non-finite coordinate, finite coordinates whose area overflows (a
1e308 square and a 1e200 square are rejected while a 1e100 square is accepted with a finite area),
null arguments, a bow tie, a spike of
overlapping edges, a repeated non-consecutive point, crossing contours, and touching contours (a
shared vertex and a vertex on another contour's edge).

##### CanvasNetRender3D-Tessellation-PolygonTriangulator-ExactCoverage: Triangulation Covers The Region Exactly

**Tests**: `PolygonTriangulatorTests.PolygonTriangulator_Triangulate_Square_ReturnsTwoCounterClockwiseTriangles`,
`PolygonTriangulatorTests.PolygonTriangulator_Triangulate_ConcavePolygons_CoverRegionExactly`,
`PolygonTriangulatorTests.PolygonTriangulator_Triangulate_RegularNgon_HasNMinusTwoTriangles`,
`PolygonTriangulatorTests.PolygonTriangulator_Triangulate_RandomStarPolygons_Fuzz_CoverRegionExactly`,
`PolygonTriangulatorTests.PolygonTriangulator_Triangulate_EmptyRegion_ReturnsNoTriangles`,
`PolygonTriangulatorTests.PolygonTriangulator_Triangulate_NullRegion_Throws`

Asserts the invariants of `AssertExactTriangulation` for a square, six concave named polygons (as a
theory), a regular n-gon (n minus two triangles), and 200 random star polygons; that an empty region
yields no triangles; and that a null region throws `ArgumentNullException`.

##### CanvasNetRender3D-Tessellation-PolygonTriangulator-Holes: Holes Are Bridged And Stay Uncovered

**Tests**: `PolygonTriangulatorTests.PolygonTriangulator_Triangulate_RingWithOneHole_CoversRegionAndLeavesHoleEmpty`,
`PolygonTriangulatorTests.PolygonTriangulator_Triangulate_RectangleWithGridOfHoles_IsExact`,
`PolygonTriangulatorTests.PolygonTriangulator_Triangulate_HoleWithIslandAndSecondOuterContour_IsExact`,
`PolygonTriangulatorTests.PolygonTriangulator_Triangulate_HoleWhoseBridgeRayHitsReflexVertex_IsExact`,
`PolygonTriangulatorTests.PolygonTriangulator_Triangulate_HoleVertexCollinearWithOuterVertices_BridgesToNearestVisible`,
`PolygonTriangulatorTests.PolygonTriangulator_Triangulate_RandomHoleLayouts_Fuzz_CoverRegionExactly`

Asserts the exact-triangulation invariants for a ring with one hole, a rectangle with a grid of
holes, a hole containing an island next to a second outer contour, a hole whose bridge ray hits a
reflex vertex, a hole vertex collinear with outer vertices, and 100 random hole layouts.

##### CanvasNetRender3D-Tessellation-PolygonTriangulator-CollinearAndDuplicatePoints: Collinear Vertices Are Kept

**Tests**: `PolygonTriangulatorTests.PolygonTriangulator_Triangulate_CollinearVerticesOnEdges_UseEveryVertexWithoutDegenerateTriangles`,
`PolygonTriangulatorTests.PolygonTriangulator_Triangulate_RectangleWithManyCollinearPointsOnAllSides_IsExact`,
`PolygonTriangulatorTests.PolygonTriangulator_Triangulate_NearlyCollinearVertices_StillTriangulates`

Asserts that every collinear vertex is used, that no triangle has zero area, and that the exact
invariants (which include the once-per-direction edge rule that excludes T-junctions) hold for a
rectangle with many collinear points on every side and for nearly collinear vertices.

##### CanvasNetRender3D-Tessellation-PolygonTriangulator-ExactPredicates: Predicates Are Exact

**Tests**: `PolygonPredicatesTests.PolygonPredicates_Orient2D_NearCollinearPoints_ReturnsExactSign`,
`PolygonPredicatesTests.PolygonPredicates_Orient2D_ExactCollinearAndExtremeMagnitudes_ReturnExactSign`,
`PolygonPredicatesTests.PolygonPredicates_Orient2D_RandomPoints_MatchIntegerReference`,
`PolygonPredicatesTests.PolygonPredicates_SegmentsIntersect_TouchingCollinearAndCrossing_AreClassified`,
`PolygonPredicatesTests.PolygonPredicates_PointInPolygon_InsideAndOutside_AreClassified`,
`PolygonPredicatesTests.PolygonPredicates_InsideCorner_ConvexReflexAndStraightCorners_AreClassified`,
`PolygonPredicatesTests.PolygonPredicates_PointInClosedTriangle_InteriorBoundaryAndExterior_AreClassified`

Asserts the exact sign of the orientation test on classical near-collinear inputs, on exactly
collinear points, and at extreme magnitudes, that random points agree with the independent integer
reference, and that segment intersection (touching, collinear overlap, crossing), point in polygon,
corner classification, and point in closed triangle classify their cases correctly.

##### CanvasNetRender3D-Tessellation-PolygonTriangulator-Determinism: Output Is Deterministic

**Tests**: `PolygonTriangulatorTests.PolygonTriangulator_Triangulate_SameInput_IsDeterministic`,
`PolygonTriangulatorTests.PolygonTriangulator_Triangulate_ClockwiseAndCounterClockwiseInput_GiveIdenticalTriangulation`

Asserts that two calls with the same region return identical indices, and that the same polygon
supplied clockwise and counter-clockwise yields identical triangulations.

#### Acceptance Criteria

A unit-level test run passes when every scenario above passes without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, violated triangulation
invariant, or wrong predicate result constitutes a failure.
