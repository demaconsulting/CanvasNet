### PrimitiveTessellator Unit Verification Design

<!-- cspell:ignore tessellator uncentered -->

This document describes the unit-level verification strategy for the `PrimitiveTessellator` class.

#### Verification Approach

The unit is verified by calling each factory with analytic parameters and asserting properties of
the returned `Mesh` that are known in closed form: vertex and triangle counts, bounds, signed volume
(the exact volume for a box, the polygon-corrected volume for a cylinder, and a documented
convergence bound for a sphere), closedness, and, through the shared test helper, that every
triangle normal points away from the mesh centroid. The 2D factories are verified the same way
against the area (exact for a rectangle, polygon-corrected for an ellipse), the vertex positions,
and the winding of the returned `PolygonRegion`. Each argument-validation rule has a test for
its exception. The unit depends only on `Mesh`, `MeshBuilder`, and `Vector3D`, so no mocking or
stubbing is required.

Unit tests reside in `PrimitiveTessellatorTests.cs` (3D factories) and `PrimitiveTessellator2DTests.cs`
(2D factories) within the `DemaConsulting.CanvasNet.Render3D.Tests` project, in the `Tessellation`
folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test creates its own meshes

#### Unit-Level Test Scenarios

##### CanvasNetRender3D-Tessellation-PrimitiveTessellator-Box: Box Has Twelve Triangles And Analytic Volume

**Tests**: `PrimitiveTessellatorTests.PrimitiveTessellator_Box_Size_HasTwelveTrianglesAndAnalyticVolume`,
`PrimitiveTessellatorTests.PrimitiveTessellator_Box_Centered_IsSymmetricAboutOrigin`,
`PrimitiveTessellatorTests.PrimitiveTessellator_Box_Uncentered_SpansZeroToSize`

Asserts eight vertices, twelve triangles, and the exact volume, and that the box spans `[0, size]`
when uncentered and is symmetric about the origin when centered.

##### CanvasNetRender3D-Tessellation-PrimitiveTessellator-Sphere: Sphere Lies On The Radius And Converges

**Tests**: `PrimitiveTessellatorTests.PrimitiveTessellator_Sphere_AllVerticesOnRadius`,
`PrimitiveTessellatorTests.PrimitiveTessellator_Sphere_VolumeConvergesBelowAnalytic`,
`PrimitiveTessellatorTests.PrimitiveTessellator_Sphere_MinimumSegments_IsClosed`,
`PrimitiveTessellatorTests.PrimitiveTessellator_Sphere_PolesAtPlusMinusRadius`

Asserts that every vertex is at the radius, that the volume increases monotonically with the
segment count, stays below the analytic sphere volume, and is within one percent of it at 48
segments, that the minimum segment count yields a closed mesh, and that the poles are at plus and
minus the radius on the Z axis.

##### CanvasNetRender3D-Tessellation-PrimitiveTessellator-Cylinder: Cylinder Volume Matches The Analytic Value

**Tests**: `PrimitiveTessellatorTests.PrimitiveTessellator_Cylinder_VolumeMatchesPolygonCorrectedAnalytic`,
`PrimitiveTessellatorTests.PrimitiveTessellator_Cylinder_Centered_SpansPlusMinusHalfHeight`,
`PrimitiveTessellatorTests.PrimitiveTessellator_Cylinder_FrustumVolume_MatchesAnalytic`

Asserts the cylinder volume against `n sin(2 pi / n) / (2 pi) * pi r^2 h`, that a centered cylinder
spans plus and minus half the height, and the frustum volume for unequal radii.

##### CanvasNetRender3D-Tessellation-PrimitiveTessellator-Cone: Zero Radius Gives A Cone With One Apex

**Test**: `PrimitiveTessellatorTests.PrimitiveTessellator_Cylinder_ZeroRadius_IsConeWithSingleApexAndNoDegenerateTriangles`

Asserts that a zero top radius produces a single apex vertex, no degenerate triangles, and a closed
mesh.

##### CanvasNetRender3D-Tessellation-PrimitiveTessellator-Polyhedron: Polyhedron Preserves Supplied Faces And Winding

**Tests**: `PrimitiveTessellatorTests.PrimitiveTessellator_Polyhedron_Tetrahedron_IsClosedWithAnalyticVolume`,
`PrimitiveTessellatorTests.PrimitiveTessellator_Polyhedron_QuadFaces_FanTriangulates`,
`PrimitiveTessellatorTests.PrimitiveTessellator_Polyhedron_InvertedWinding_HasNegativeVolumeAndReverseFixes`,
`PrimitiveTessellatorTests.PrimitiveTessellator_Polyhedron_OpenFaceSet_PreservesSuppliedFacesAndWinding`

Asserts that a tetrahedron built from outward faces is closed with the analytic volume, that quad
faces fan into two triangles each, that an inverted polyhedron has a negative volume that
`Mesh.Reverse` repairs, and
that an open face set (and a single face listed in the opposite sense) is reproduced exactly as
supplied, with the same triangle indices and still not closed, proving that no closure or
orientation correction is applied.

##### CanvasNetRender3D-Tessellation-PrimitiveTessellator-Validation: Invalid Arguments Are Rejected

**Tests**: `PrimitiveTessellatorTests.PrimitiveTessellator_Box_NonPositiveSize_Throws`,
`PrimitiveTessellatorTests.PrimitiveTessellator_Sphere_TooFewSegments_Throws`,
`PrimitiveTessellatorTests.PrimitiveTessellator_Cylinder_BothRadiiZero_Throws`,
`PrimitiveTessellatorTests.PrimitiveTessellator_Polyhedron_BadIndexOrShortFace_Throws`,
`PrimitiveTessellatorTests.PrimitiveTessellator_Polyhedron_RepeatedIndex_Throws`,
`PrimitiveTessellatorTests.PrimitiveTessellator_Polyhedron_DistinctIndicesOfIdenticalPoints_Throws`,
`PrimitiveTessellatorTests.PrimitiveTessellator_Polyhedron_DuplicatePointsOnSeparateFaces_WeldsAndSucceeds`

Asserts the documented exception for a non-positive box size, fewer than three segments, both
cylinder radii zero, and a polyhedron with a null argument, a face with an out-of-range or repeated
index (adjacent, non-adjacent, and first-equals-last repeats), a face whose distinct indices address
identical points that weld together, or fewer than three indices; and that identical points
appearing on separate faces still weld and build a valid mesh.

##### CanvasNetRender3D-Tessellation-PrimitiveTessellator-ClosedOutward: Every Primitive Is Closed And Outward Oriented

**Test**: `PrimitiveTessellatorTests.PrimitiveTessellator_AllPrimitives_AreClosedAndOutwardOriented`

Asserts, for each convex primitive, that the mesh is closed, has positive signed volume, and has
every triangle normal pointing away from the mesh centroid.

##### CanvasNetRender3D-Tessellation-PrimitiveTessellator-Rectangle: Rectangle Has Analytic Area And Extent

**Test**: `PrimitiveTessellator2DTests.PrimitiveTessellator_Rectangle_SizeAndCenter_HaveAnalyticAreaAndExtent`

Asserts that the rectangle region is a single counter-clockwise contour with four vertices, has the
exact area, and spans `[0, size]` when uncentered and is symmetric about the origin when centered.

##### CanvasNetRender3D-Tessellation-PrimitiveTessellator-Ellipse: Ellipse And Circle Are Inscribed Polygons

**Tests**: `PrimitiveTessellator2DTests.PrimitiveTessellator_Ellipse_VerticesLieOnEllipseAndAreCounterClockwise`,
`PrimitiveTessellator2DTests.PrimitiveTessellator_Ellipse_AreaIsPolygonCorrectedAnalytic`,
`PrimitiveTessellator2DTests.PrimitiveTessellator_Circle_EqualsEllipseWithEqualRadii`

Asserts that every vertex lies on the ellipse at the angle `2 pi j / n`, that the winding is
counter-clockwise, that the area equals `n / 2 * rx * ry * sin(2 pi / n)`, and that a circle equals
an ellipse with equal radii.

##### CanvasNetRender3D-Tessellation-PrimitiveTessellator-Polygon2D: Polygon Builds Contours From Points And Paths

**Tests**: `PrimitiveTessellator2DTests.PrimitiveTessellator_Polygon_PointsOnly_IsSingleContour`,
`PrimitiveTessellator2DTests.PrimitiveTessellator_Polygon_WithPaths_BuildsHole`

Asserts that a point list without paths is one contour of all points in order with the exact area,
and that two index paths (an outer square and an inner square) build a region whose second contour is
a hole at depth one with the area of the outer square minus the hole.

##### CanvasNetRender3D-Tessellation-PrimitiveTessellator-Shapes2DValidation: Invalid 2D Arguments Are Rejected

**Test**: `PrimitiveTessellator2DTests.PrimitiveTessellator_Shapes2D_InvalidArguments_Throw`

Asserts `ArgumentOutOfRangeException` for a zero, negative, or non-finite rectangle size, ellipse
radius, or circle radius, for fewer than three ellipse or circle segments, and for an out-of-range or
negative path index; `ArgumentNullException` for a null points list or path; and `ArgumentException`
for a zero-area polygon.

#### Acceptance Criteria

A unit-level test run passes when every scenario above passes without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong count, bound,
or volume constitutes a failure.
