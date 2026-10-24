### Extruder Unit Verification Design

<!-- cspell:ignore triangulator frustum extruder extrusion extrusions extruded collinear faceting Pappus apex -->

This document describes the unit-level verification strategy for the `Extruder` class (linear and
rotational extrusion of a `PolygonRegion` into a `Mesh`).

#### Verification Approach

The unit is verified by extruding regions with analytic geometry and asserting properties of the
returned `Mesh` that are known in closed form, plus a single shared check that every result is a
valid constructive-solid-geometry operand.

**Analytic oracles.**

- *Prism*: volume `A h` (exact, tolerance `1e-12` to `1e-9`), also for a concave polygon, for a
  region with holes, and for `Linear(Circle(r, n), h)`, which must equal the `Cylinder(h, r, r, n)`
  volume and surface area.
- *Frustum*: with a uniform top scale `s` the volume is `A h (1 + (s - 1) + (s - 1)^2 / 3)`, exact
  (relative `1e-12`) for one and four slices.
- *Pyramid*: with scale `(0, 0)` the volume is `A h / 3`, with exactly one vertex at the top, no top cap
  and no degenerate triangles; a region with a hole or several contours is rejected.
- *Per-axis scale and twist*: wall quads are not planar, so the facet volume only approaches the smooth
  solid. The per-axis scale test uses 64 slices and relative tolerance `1e-3`; the twist test uses a
  90 degree twist with 64 slices and relative tolerance `1e-2` and also asserts convergence (the
  error with 64 slices is smaller than the error with 32 slices divided by 1.5), because the faceting
  error is first order in the twist per slice.
- *Counts*: `V (slices + 1)` vertices and `2 V slices + 2 (V + 2H - 2P)` triangles.
- *Rotation*: the facets are planar, so the volume has the exact closed form
  `n sin(delta) * integral(|x| dA)` with `delta = angle / n` (`2 pi / n` for a full revolution), evaluated
  by the test helper from the region with Green's theorem and compared at relative `1e-9`. It tends to
  the Pappus volume `angle * x_centroid * A` as `n` grows. A rectangle touching the axis equals the
  `Cylinder` volume.
- *Twist sign*: with a positive twist the top ring is the bottom ring rotated counter-clockwise about +Z.

**Structure.** Tests assert the number of connected shells (a ring with a hole is one shell; an island
inside a hole is two disjoint outward shells; a revolved profile with a hole is two shells, outer
outward and cavity inward), that hole-wall normals point toward the hole axis, that rotation welds each
axis vertex exactly once and skips an edge lying on the axis, and that a full revolution has no
duplicated seam vertices.

**Valid operands.** The shared helper `Render3DTestHelpers.AssertValidCsgOperand` asserts that the mesh
is closed, that `OperandDefects` is `None`, that it is not inverted, that the signed volume is
positive, that no two vertices share a position and every vertex is referenced, and that the internal
`CsgOperandValidator` (run on a scene that subtracts a far-away box) delivers no report, also with
`ThrowOnUnsupportedFeature`. Two theories apply it to every parameter set under test (seventeen linear
variants; fifteen rotational variants covering full and partial sweeps, the positive and negative side
of the axis, holes, and profiles touching the axis). A negative test asserts that the helper fails for
a reversed (inside-out) result, which proves the check can fail.

Each argument-validation rule has a test for its exception. The unit depends only on
`PolygonRegion`, `PolygonTriangulator`, `Mesh`, and `MeshBuilder`, so no mocking or stubbing is
required. The only link to another subsystem is test-only: `Render3DTestHelpers.AssertValidCsgOperand`
runs the internal `SceneRendering.CsgOperandValidator` (visible to the test project through
`InternalsVisibleTo`), as stated in the unit design.

Unit tests reside in `ExtruderLinearTests.cs` and `ExtruderRotateTests.cs` within the
`DemaConsulting.CanvasNet.Render3D.Tests` project, in the `Tessellation` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test creates its own regions and meshes

#### Unit-Level Test Scenarios

##### CanvasNetRender3D-Tessellation-Extruder-LinearPrism: Linear Extrusion Is An Exact Prism

**Tests**: `ExtruderLinearTests.Extruder_Linear_Square_IsClosedPrismWithAnalyticVolume`,
`ExtruderLinearTests.Extruder_Linear_Centered_SpansPlusMinusHalfHeight`,
`ExtruderLinearTests.Extruder_Linear_ConcavePolygon_VolumeIsAreaTimesHeight`,
`ExtruderLinearTests.Extruder_Linear_CircleRegion_MatchesCylinderVolume`,
`ExtruderLinearTests.Extruder_Linear_Slices_TriangleCountMatchesFormula`,
`ExtruderLinearTests.Extruder_Linear_CollinearContourPoints_AreClosedWithoutTJunctions`

Asserts that a square extrusion has eight vertices, twelve triangles, and volume `A h`; that a
centered extrusion spans plus and minus half the height; that a concave extrusion has volume
`A h`; that a circle region matches the cylinder; that the triangle count follows the formula for
one, two, and five slices; and that collinear contour points leave the mesh closed.

##### CanvasNetRender3D-Tessellation-Extruder-LinearHoles: Holes Are One Connected Shell

**Tests**: `ExtruderLinearTests.Extruder_Linear_RingWithHole_IsSingleClosedShellWithThroughHole`,
`ExtruderLinearTests.Extruder_Linear_MultipleHoles_VolumeIsAnalytic`,
`ExtruderLinearTests.Extruder_Linear_IslandInsideHole_IsTwoOutwardShellsAndValidOperand`

Asserts that a ring with a hole is exactly one closed shell with volume `(A_outer - A_hole) h`, with
hole-wall normals pointing toward the hole axis and a result that is not inverted; that several
holes give the analytic volume; and that an island inside a hole is two outward shells that is still a
valid operand with the analytic volume.

##### CanvasNetRender3D-Tessellation-Extruder-LinearScale: Scale Gives Exact Frustum Volumes

**Tests**: `ExtruderLinearTests.Extruder_Linear_UniformScale_FrustumVolumeIsExact`,
`ExtruderLinearTests.Extruder_Linear_PerAxisScale_VolumeConvergesToAnalytic`

Asserts the exact frustum volume for a uniform scale with one and four slices, and the analytic
volume within `1e-3` for a per-axis scale with 64 slices.

##### CanvasNetRender3D-Tessellation-Extruder-LinearApex: Zero Scale Gives A Single Apex

**Tests**: `ExtruderLinearTests.Extruder_Linear_ScaleZero_IsPyramidWithSingleApex`,
`ExtruderLinearTests.Extruder_Linear_ScaleZeroWithHoleOrIslands_Throws`,
`ExtruderLinearTests.Extruder_Linear_TwistAndScaleToApex_IsValidClosedMesh`

Asserts the pyramid volume `A h / 3`, a single top vertex, no top cap triangle and no degenerate
triangle; that an apex over a region with a hole or with two separate islands is rejected with an
`ArgumentException` naming `scale` (every contour would collapse onto one pinched vertex) while the same
region with a positive scale and a single-contour apex are valid operands; and that a twist combined with an
apex is a valid closed mesh.

##### CanvasNetRender3D-Tessellation-Extruder-LinearTwist: Twist Is Counter-Clockwise And Watertight

**Tests**: `ExtruderLinearTests.Extruder_Linear_Twist_SlicesShareRingVertices`,
`ExtruderLinearTests.Extruder_Linear_Twist_PositiveRotatesTopCounterClockwise`,
`ExtruderLinearTests.Extruder_Linear_Twist_VolumeWithinToleranceOfAnalytic`,
`ExtruderLinearTests.Extruder_Linear_Twist360_IsClosedOutwardValidOperand`

Asserts the vertex count `V (slices + 1)` and a closed mesh for a twisted extrusion, that a positive
twist rotates the top ring counter-clockwise, that the volume is within the documented tolerance and
converges as the slice count grows, and that a 360 degree twist is closed, outward, and a valid
operand.

##### CanvasNetRender3D-Tessellation-Extruder-LinearValidation: Invalid Linear Arguments Are Rejected

**Tests**: `ExtruderLinearTests.Extruder_Linear_InvalidArguments_Throw`,
`ExtruderLinearTests.Extruder_Linear_ScaleWithOneZeroComponent_Throws`,
`ExtruderLinearTests.Extruder_Linear_NegativeOrNonFiniteScale_Throws`,
`ExtruderLinearTests.Extruder_Linear_ScaleWithOneZeroComponentAndHole_Throws`

Asserts the documented exception for a null region, a non-positive or non-finite height, fewer than one
slice, a non-finite twist, a scale with exactly one zero component, and a negative or non-finite
scale.

##### CanvasNetRender3D-Tessellation-Extruder-RotateFull: Full Revolution Has The Exact Facet Volume

**Tests**: `ExtruderRotateTests.Extruder_Rotate_RectangleOffAxis_FullRevolution_ExactFacetVolume`,
`ExtruderRotateTests.Extruder_Rotate_CircleProfileOffAxis_IsTorusWithExactFacetVolume`,
`ExtruderRotateTests.Extruder_Rotate_ProfileWithHole_FullRevolution_IsClosed`,
`ExtruderRotateTests.Extruder_Rotate_Angle360_SeamHasNoDuplicateVertices`

Asserts the exact facet volume for an off-axis rectangle and a torus profile, that a profile with a
hole gives two shells (outer outward, cavity inward) with the closed-form volume, and that a full
revolution has no duplicated seam vertices.

##### CanvasNetRender3D-Tessellation-Extruder-RotatePartial: Partial Sweep Has End Caps

**Tests**: `ExtruderRotateTests.Extruder_Rotate_PartialAngle_HasEndCapsClosedAndExactVolume`,
`ExtruderRotateTests.Extruder_Rotate_PartialAngleTouchingAxis_IsClosed`

Asserts that a 90 degree sweep in eight segments is closed with two end caps and the exact facet volume,
and that a partial sweep of a profile touching the axis is closed.

##### CanvasNetRender3D-Tessellation-Extruder-RotateAxis: Axis Vertices Are Welded

**Tests**: `ExtruderRotateTests.Extruder_Rotate_RectangleTouchingAxis_MatchesCylinderAndWeldsAxisVertices`,
`ExtruderRotateTests.Extruder_Rotate_TriangleOnAxis_IsConeWithSingleApex`,
`ExtruderRotateTests.Extruder_Rotate_ProfileEdgeAlongAxis_SkipsDegenerateWall`,
`ExtruderRotateTests.Extruder_Rotate_NegativeSideProfile_IsOutwardWithSameVolume`

Asserts that a rectangle touching the axis has two axis vertices plus `2 n` ring vertices and the
`Cylinder` volume, that a triangle with a vertex on the axis is a cone with a single apex, that an
edge lying on the axis produces no degenerate triangle, and that a profile on the negative side is
outward oriented with the same volume as its mirror image.

##### CanvasNetRender3D-Tessellation-Extruder-RotateValidation: Invalid Rotation Arguments Are Rejected

**Tests**: `ExtruderRotateTests.Extruder_Rotate_ProfileCrossingAxis_Throws`,
`ExtruderRotateTests.Extruder_Rotate_ProfileOnlyOnAxis_Throws`,
`ExtruderRotateTests.Extruder_Rotate_IsolatedAxisVertex_Throws`,
`ExtruderRotateTests.Extruder_Rotate_PoleVerticesWithAxisEdge_AreAccepted`,
`ExtruderRotateTests.Extruder_Rotate_InvalidArguments_Throw`,
`ExtruderRotateTests.Extruder_Rotate_TinyAngleRoundingToZeroRadians_Throws`,
`ExtruderRotateTests.Extruder_Rotate_StepUnresolvableAtRadius_Throws`

Asserts `ArgumentException` for a profile that crosses the axis and for a profile lying only on it
(which the region constructor rejects as zero area, so the test asserts the exception from that
path, while the defensive check in `Rotate` remains), for a profile touching the axis at an isolated
vertex (on either side of the axis, and next to an edge lying on the axis) while a vertex ending an edge on
the axis (a pole) is accepted, and the documented exception for a null
region, an angle of zero, above 360, or not a number, and too few segments for a full revolution and
for a partial sweep. The smallest positive double as an angle (its radian value rounds to zero)
is rejected with an `ArgumentOutOfRangeException` naming `angleDegrees`, and a `1e-300` degree
sweep of a profile at radius `1e-300` (the ring offset rounds to zero) is rejected with an
`ArgumentException` instead of returning an open mesh.

##### CanvasNetRender3D-Tessellation-Extruder-ValidOperand: Every Result Is A Valid CSG Operand

**Tests**: `ExtruderLinearTests.Extruder_Linear_AllVariants_AreValidCsgOperands`,
`ExtruderRotateTests.Extruder_Rotate_AllVariants_AreValidCsgOperands`,
`ExtruderLinearTests.Extruder_Linear_ReversedResult_FailsOperandAssertion`

Applies `AssertValidCsgOperand` to every linear and rotational parameter set under test and asserts
that the same assertion fails for a reversed result.

##### CanvasNetRender3D-Tessellation-Extruder-EmptyRegion: Empty Region Gives An Empty Mesh

**Tests**: `ExtruderLinearTests.Extruder_Linear_EmptyRegion_ReturnsEmptyMesh`,
`ExtruderRotateTests.Extruder_Rotate_InvalidArguments_Throw`

Asserts that an empty region extrudes linearly, with twist and slices, to a mesh without triangles,
and that rotating the empty region yields no triangles (asserted within the invalid-arguments test).

#### Acceptance Criteria

A unit-level test run passes when every scenario above passes without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, wrong count, wrong
volume beyond the stated tolerance, or a result that is not a valid constructive-solid-geometry operand
constitutes a failure.
