### ScadExtrusions Unit Verification Design

<!-- cspell:ignore extrude extruder extruders extrusion extrusions apex Pappus frustum faceted subtrahend minuend -->

This document describes the unit-level verification strategy for the `ScadExtrusions` unit: the internal
`ScadShape2D` tree and the `ScadExtrusions` static class (`linear_extrude`, `rotate_extrude`, and the slabs of
top-level 2D shapes) in the `DemaConsulting.CanvasNet.Scad.Geometry` namespace.

#### Verification Approach

The unit is verified by building small `CanvasNetScad` programs through `ScadGeometryTestSupport.Build`, which
evaluates the source, runs `ScadGeometryBuilder.Build` on the instantiation tree, and returns a `ScadBuildResult`
holding the scene, the warnings, and the unsupported-feature reports. The tests then assert on the real Render3D
output rather than on mocks:

- **Analytic volumes.** A mesh is checked for closedness and an exact signed volume (`AssertSolidVolume`): area
  times height for a linear extrusion, the frustum and pyramid formulas for a scaled one, and for a revolution the
  exact faceted closed form `n · sin(2π / n) · (first moment of the profile)`, which approaches the Pappus volume
  (a torus is also compared with the Pappus value within one percent). Bounds are checked with `AssertBounds`.
- **Structure.** The scene root of a 2D difference, intersection, or nested boolean is asserted to be a `CsgNode`
  with the expected operation and operand count, and every operand to share its slice or segment (vertex) count.
- **Reports.** Warnings and unsupported-feature reports are asserted by feature id, message text, and exact
  `ScadSourceLocation` (line and column of the instance or of the 2D leaf); strict mode is asserted to throw
  `ScadUnsupportedFeatureException` with the same feature and location, and the public `ScadDocument.BuildScene`
  path to deliver the report to the `UnsupportedFeatureHandler`.
- **Error mapping.** `ScadExtrusionsMappingTests` drives a table of eight contract rows (program, expected feature,
  and the token whose location is expected), each in report mode and in strict mode, so the whole mapping from
  causes to reports is covered by one data set, plus a list of extreme inputs asserted never to throw anything but a
  resource-limit `ScadEvaluationException`.
- **Budgets.** The triangle limit is exercised with a small `maxTriangles` setting (exact boundary) and with hostile
  slice, twist, and fragment counts that must fail before anything is allocated.

Unit tests reside in `ScadExtrusionsLinearTests.cs`, `ScadExtrusionsRotateTests.cs`, and
`ScadExtrusionsMappingTests.cs` within the `DemaConsulting.CanvasNet.Scad.Tests` project, in the `Geometry` folder,
with the shared helpers (`Build`, `Settings`, `AssertSolidVolume`, `AssertBounds`, `Leaves`, and
`RegularPolygonArea`) in `ScadGeometryTestSupport.cs`.

Evidence that belongs to other units' test files and is **not cited** by a requirement of this unit:

- The pixel equivalence of an extruded 2D difference with the hand-built Render3D difference of extrusions (linear
  and rotate), an extruded ring annulus, and a revolved vase silhouette is checked end to end in
  `ScadDocumentRenderExtrusionTests` (`ScadDocument_Render_ExtrudedDifference_EqualsTheHandBuiltDifferenceOfExtrusions`
  and its siblings).
- The slabs of top-level 2D shapes (`MakeSlabs`: thickness, difference of slabs, color of slabs, and the budget) are
  checked by `ScadPrimitivesShape2DTests`, `ScadGeometryBuilderBooleanTests`, `ScadColorsTests`, and
  `ScadGeometryBuilderLimitTests`.
- The no-recursion guarantee (deep chains of 2D transforms and extruded booleans on a 256 KiB stack) is checked by
  `ScadGeometryBuilderStackTests` and `ScadGeometryBuilderTransformDepthTests`.
- The `Extruder.Rotate` step contract itself is verified by the Render3D extruder tests (see _Extruder Unit
  Verification Design_); this unit verifies that every angle and fragment count it produces honors it.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None; the evaluator, the builder, and the Render3D extruders are the real implementations
- **Files**: None; every program is an in-memory string
- **Isolation**: Each test builds its own program and scene; no state is shared

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A wrong volume or bounds, an open or inside-out mesh, a report with the wrong feature id, message, or
location, a warning where an unsupported report is expected (or the reverse), a thrown exception where a report is
expected, or a build that throws anything other than the asserted `ScadEvaluationException` constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetScad-ScadGeometry-ScadExtrusions-Distribution: Extrusion Over The 2D Tree

**Tests**: `ScadExtrusionsLinearTests.ScadExtrusionsLinear_SeveralChildren_AreExtrudedTogether`,
`ScadExtrusionsLinearTests.ScadExtrusionsLinear_OverFlatDifference_IsADifferenceOfExtrusions`,
`ScadExtrusionsLinearTests.ScadExtrusionsLinear_OverNestedBooleans_ShareParameters`,
`ScadExtrusionsRotateTests.ScadExtrusionsRotate_OverFlatDifference_IsADifferenceOfSolidsWithSharedSegments`

Asserts that several 2D children are extruded together with the same height range, that a 2D difference becomes a
`CsgNode` difference of the extrusions with the exact volumes and bounds of each operand, that nested intersection,
union, and difference share the slice count (every operand has the same number of vertices) with a twist and a
scale applied, and that a revolved difference has equal segment counts for both operands and exact faceted volumes.

##### CanvasNetScad-ScadGeometry-ScadExtrusions-ProfileTransforms: Transforms And Colors

**Tests**: `ScadExtrusionsLinearTests.ScadExtrusionsLinear_ProfileTransformsAndColors_ApplyToTheProfile`,
`ScadExtrusionsLinearTests.ScadExtrusionsLinear_ThenTransformedAndColoredIn3D_Composes`,
`ScadExtrusionsRotateTests.ScadExtrusionsRotate_TransformsAndColors_Compose`

Asserts that a 2D translation, rotation, mirror, and scale inside an extrusion give the expected bounds and volumes
(the profile is changed, not the solid), that a color inside the extrusion is carried to the mesh, that a 3D
rotation, translation, and color around a linear extrusion compose, and that the same holds for a revolution of a
transformed profile.

##### CanvasNetScad-ScadGeometry-ScadExtrusions-InvalidProfile: Leaf Invalid After Transformation

**Tests**: `ScadExtrusionsLinearTests.ScadExtrusionsLinear_InvalidLeafAfterTransform_IsReportedAsInvalidPolygon`,
`ScadExtrusionsMappingTests.ScadExtrusionsMapping_InvalidPolygonInsideBoolean_IsReportedAndTheRestExtrudes`

Asserts that two stretches that multiply to infinity when composed make the only leaf invalid, which is reported as
`scad-polygon-invalid` at the leaf's line with no mesh, and that an invalid polygon inside a 2D difference is
reported while the valid operand is extruded alone with the expected bounds.

##### CanvasNetScad-ScadGeometry-ScadExtrusions-RejectedExtrusion: Omission Of The Whole Extrusion

**Tests**: `ScadExtrusionsMappingTests.ScadExtrusionsMapping_RejectedOperand_IsAnEmptyOperand`,
`ScadExtrusionsRotateTests.ScadExtrusionsRotate_AxisCrossing_IsReportedAndOmitted`

Asserts that a rejected `rotate_extrude` as the subtrahend of a solid difference vanishes (the solid remains) and as
the minuend empties the difference, each with exactly one report, and that a profile whose crossing operand is only
one of several shapes (separate shapes on both sides, or a crossing difference operand) omits the entire extrusion.

##### CanvasNetScad-ScadGeometry-ScadExtrusions-ChildDimensions: 2D And 3D Children

**Tests**: `ScadExtrusionsLinearTests.ScadExtrusionsLinear_ChildDimensions_AreHandled`,
`ScadExtrusionsRotateTests.ScadExtrusionsRotate_ChildDimensions_AreHandled`

Asserts that an extrusion of only a solid is empty with an "only 2D children" warning, that a solid next to a 2D
shape warns once and still extrudes the 2D shape, and that an extrusion without children is empty and silent, for
both extrusions.

##### CanvasNetScad-ScadGeometry-ScadExtrusions-LinearHeight: Height, Centering, And Volume

**Tests**: `ScadExtrusionsLinearTests.ScadExtrusionsLinear_Square_GivesAreaTimesHeight`,
`ScadExtrusionsLinearTests.ScadExtrusionsLinear_Arguments_GiveTheExpectedHeightRange`,
`ScadExtrusionsLinearTests.ScadExtrusionsLinear_Circle_GivesThePolygonPrism`,
`ScadExtrusionsLinearTests.ScadExtrusionsLinear_PolygonWithHole_HasAThroughHole`

Asserts the exact volume, closedness, and bounds of a plain extrusion, the Z range for the default height of 100,
positional and named height, `center` (named and positional), and an ignored `convexity`, that a circle extrudes to
the volume of its inscribed prism, and that a profile with a hole has the outline-minus-hole volume.

##### CanvasNetScad-ScadGeometry-ScadExtrusions-LinearTwist: Twist Direction

**Tests**: `ScadExtrusionsLinearTests.ScadExtrusionsLinear_Twist_PositiveIsClockwiseFromAbove`,
`ScadExtrusionsLinearTests.ScadExtrusionsLinear_Twist_KeepsVolumeApproximately`

Asserts that the top vertices of a 90-degree twist are the profile turned clockwise for a positive twist (and
counter-clockwise for a negative one) and that a twist of 180 degrees keeps a closed mesh whose volume is within
three percent of the untwisted volume.

##### CanvasNetScad-ScadGeometry-ScadExtrusions-LinearSlices: Slice Rule

**Tests**: `ScadExtrusionsLinearTests.ScadExtrusionsLinear_Slices_FollowTheDefaultRule`

Asserts the vertex count (and therefore the slice count) for no twist, a zero twist, twists of 90, -90, 360, and 1
degrees with `$fn` values, an explicit `slices`, and a fractional `slices` rounded down.

##### CanvasNetScad-ScadGeometry-ScadExtrusions-LinearScale: Frustum And Apex

**Tests**: `ScadExtrusionsLinearTests.ScadExtrusionsLinear_Scale_GivesTheFrustumVolume`,
`ScadExtrusionsLinearTests.ScadExtrusionsLinear_ScaleZero_GivesAPyramid`

Asserts the analytic frustum volumes for uniform and non-uniform scale (including a scale of 1), with no reports,
and that a scale of 0 or `[0, 0]` gives a pyramid with a single apex vertex and a volume of one third of the
prism.

##### CanvasNetScad-ScadGeometry-ScadExtrusions-LinearUnsupportedScale: Unsupported Scales

**Tests**: `ScadExtrusionsLinearTests.ScadExtrusionsLinear_UnsupportedScale_IsReportedAndOmitted`,
`ScadExtrusionsLinearTests.ScadExtrusionsLinear_ApexOverSeveralContours_IsReportedAndOmitted`,
`ScadExtrusionsLinearTests.ScadExtrusionsLinear_StrictMode_ThrowsForUnsupportedScale`

Asserts that a negative or half-zero scale is exactly one `scad-linear-extrude-scale` report at the instance with
no mesh and no warning, that an apex over a profile with a hole is `scad-linear-extrude-apex-multiple-contours`
while an apex over two separate single-contour leaves builds both, and that strict mode throws the same feature
with the same location.

##### CanvasNetScad-ScadGeometry-ScadExtrusions-LinearArguments: Invalid Arguments

**Tests**: `ScadExtrusionsLinearTests.ScadExtrusionsLinear_InvalidArguments_WarnAndRemoveInstance`,
`ScadExtrusionsLinearTests.ScadExtrusionsLinear_UnknownArgument_WarnsAndStillBuilds`

Asserts that a zero, negative, non-numeric, vector, and non-finite height, a non-numeric twist, `slices` of 0, -2,
or a string, and a string, three-component, partly non-numeric, or non-finite scale each give exactly one
`linear_extrude:` warning at the instance and no mesh, and that an unknown argument warns while the extrusion is
still built.

##### CanvasNetScad-ScadGeometry-ScadExtrusions-TriangleBudget: Resource Limit

**Tests**: `ScadExtrusionsLinearTests.ScadExtrusionsLinear_HostileSlicesOrTwist_ExceedTheTriangleLimit`,
`ScadExtrusionsLinearTests.ScadExtrusionsLinear_SmallBudget_IsEnforced`,
`ScadExtrusionsRotateTests.ScadExtrusionsRotate_HostileFragmentCount_ExceedsTheTriangleLimit`

Asserts that slices of 1e9 and 1e300, a twist of 1e15 (also with `$fn` of 1e6), and a `$fn` of 1e9 for a revolution
throw `ScadEvaluationException` ("Triangle limit") located at the extrusion, that the estimate is exact at its
boundary (808 triangles for four vertices and 100 slices), and that the budget accounts for all leaves.

##### CanvasNetScad-ScadGeometry-ScadExtrusions-RotateSweep: Angle, Direction, And Sweep Start

**Tests**: `ScadExtrusionsRotateTests.ScadExtrusionsRotate_FullRevolution_GivesTheFacetedPappusVolume`,
`ScadExtrusionsRotateTests.ScadExtrusionsRotate_PartialSweep_IsAClosedWedge`,
`ScadExtrusionsRotateTests.ScadExtrusionsRotate_NegativeAndOversizedAngles_ClockwiseAndClamped`,
`ScadExtrusionsRotateTests.ScadExtrusionsRotate_NegativeSideProfile_IsMirroredToTheSweepStart`,
`ScadExtrusionsRotateTests.ScadExtrusionsRotate_Torus_MatchesPappus`

Asserts the exact faceted volume and bounds of a full revolution and of a 90-degree wedge (closed, with end caps),
that a negative angle sweeps into negative Y with the same volume, that an angle of 720 is the same solid as 360,
that a profile at `x <= 0` gives the same bounds and volume as its mirror image for a partial sweep, and that a
torus matches its faceted closed form and the Pappus volume within one percent.

##### CanvasNetScad-ScadGeometry-ScadExtrusions-RotateArguments: Invalid Angle

**Tests**: `ScadExtrusionsRotateTests.ScadExtrusionsRotate_InvalidAngle_WarnsAndRemovesInstance`,
`ScadExtrusionsRotateTests.ScadExtrusionsRotate_ConvexityAndUnknownArguments_AreHandled`

Asserts that an angle of 0, a string, a non-finite value, and a vector give exactly one `rotate_extrude: angle`
warning at the instance and no mesh, that `convexity` produces no warning, and that an unknown argument warns while
the extrusion is still built.

##### CanvasNetScad-ScadGeometry-ScadExtrusions-RotateSegments: Segment Count And Step Limit

**Tests**: `ScadExtrusionsRotateTests.ScadExtrusionsRotate_DefaultSegments_FollowTheFragmentRule`,
`ScadExtrusionsRotateTests.ScadExtrusionsRotate_SeveralLeaves_ShareTheSegmentCountOfTheLargestRadius`,
`ScadExtrusionsRotateTests.ScadExtrusionsRotate_AnyAngleAndFragmentCount_BuildsAClosedSolid`

Asserts the default segment count (ten rings for a radius of 3), that leaves of different radii share the count of
the largest radius, and that for angles from 1 to 361 degrees (including 119, 120, 121, 240, 241, 359, and -200)
and fragment counts of 3, 4, 5, 7, and 24 the result is a closed solid with positive volume and no warning or
report, which proves that no angular step ever exceeds the 120-degree limit of `Extruder.Rotate`.

##### CanvasNetScad-ScadGeometry-ScadExtrusions-RotateAxis: Axis Crossing, Contact, Poles, And Holes

**Tests**: `ScadExtrusionsRotateTests.ScadExtrusionsRotate_AxisCrossing_IsReportedAndOmitted`,
`ScadExtrusionsRotateTests.ScadExtrusionsRotate_AxisContact_IsReportedAndPolesAreFine`,
`ScadExtrusionsRotateTests.ScadExtrusionsRotate_ProfileWithHole_IsHollow`,
`ScadExtrusionsRotateTests.ScadExtrusionsRotate_StrictMode_ThrowsForAxisCrossing`

Asserts that a crossing square, separate shapes on both sides, a crossing difference operand, and a circle on the
axis each give exactly one `scad-rotate-extrude-axis-crossing` report at the instance with no mesh and no warning,
that a triangle touching the axis at one vertex gives `scad-rotate-extrude-axis-contact` while a cone and a cylinder
(poles) revolve with exact volumes, that a profile with a hole gives the hollow solid of outline minus cavity, and
that strict mode throws the crossing report with its location.

##### CanvasNetScad-ScadGeometry-ScadExtrusions-ErrorMapping: Rejection Mapping

**Tests**: `ScadExtrusionsMappingTests.ScadExtrusionsMapping_Row_IsReportedWithItsFeatureAndLocation`,
`ScadExtrusionsMappingTests.ScadExtrusionsMapping_Row_StrictModeThrowsTheSameReport`,
`ScadExtrusionsMappingTests.ScadExtrusionsMapping_PublicApi_DeliversAndThrows`

Asserts, for each of the eight rows (apex over several contours, negative scale, half-zero scale, axis crossing,
axis contact, invalid leaf under `linear_extrude`, invalid leaf under `rotate_extrude`, and the
`scad-geometry-rejected` catch-all reached by an angle of `5e-324`), that exactly one report with the expected
feature, a non-empty message, and the location of the expected token is made, that nothing is built and there is no
warning, and, in strict mode, that `ScadUnsupportedFeatureException` carries the same feature and location; and that
`ScadDocument.BuildScene` delivers the report to the public handler (the rest of the scene still builds) and throws
it in strict mode.

##### CanvasNetScad-ScadGeometry-ScadExtrusions-Robustness: Extreme Inputs

**Tests**: `ScadExtrusionsMappingTests.ScadExtrusionsMapping_ExtremeInputs_NeverCrashTheBuild`

Asserts for 30 unusual inputs (extreme heights, scales, twists, angles, and coordinates; degenerate, collinear, and
self-crossing profiles; overlapping and touching shapes) that the build either completes or throws
`ScadEvaluationException`, and never any other exception.
