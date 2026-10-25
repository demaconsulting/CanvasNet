### ScadPrimitives Unit Verification Design

<!-- cspell:ignore undef polyhedron polyhedra tessellators Newell frustum truthy unsupplied -->

This document describes the unit-level verification strategy for the `ScadPrimitives` unit: the internal
`ScadArguments` binder, the `ScadFragmentRule` formula, and the `ScadPrimitives` builders of `cube`, `sphere`,
`cylinder`, `polyhedron`, `square`, `circle`, and `polygon` in the `DemaConsulting.CanvasNet.Scad.Geometry`
namespace.

#### Verification Approach

The unit is verified at two levels, both with real implementations and no mocks:

- **Direct calls** for the two pure parts. `ScadArgumentsTests` creates a `ScadInstance` with chosen arguments and
  calls `ScadArguments.Bind` with a real `ScadGeometryContext` over a real `ScadEvaluationSink`, then asserts on
  the bound values and on the exact warning texts and locations recorded in the sink. `ScadFragmentRuleTests`
  calls `ScadFragmentRule.Fragments` with spot values, clamps, and invalid numbers, and asserts on the integer
  result.
- **Programs through the evaluator and the builder** for the primitives. The tests of `ScadPrimitivesSolidTests`,
  `ScadPrimitivesShape2DTests`, and `ScadPolyhedronTests` call `ScadGeometryTestSupport.Build(source)`, which
  evaluates a small program with the real evaluator and builds the instances into a scene with
  `ScadGeometryBuilder.Build` (the instances therefore carry the same arguments, fragment settings, and locations
  as in production). The result exposes the meshes of the scene, the location map, the warnings, and the
  unsupported-feature reports. Assertions are analytic rather than reference-image based: `Mesh.IsClosed`, the
  exact `SignedVolume` against the faceted closed form (the area of the regular polygon times the height, the
  prismatoid formula for a frustum, the L-prism's area times its height, one sixth of a unit cube for a
  tetrahedron), the bounds, the vertex and triangle counts, and the exact text and location of every warning.
  A top-level 2D shape is a thin slab, so its area is checked through the slab's volume.

Invalid inputs are tabulated in `[Theory]` rows so that each wrong type, non-positive size, malformed list, and
out-of-range index is asserted with its own exact warning text. Strict mode is exercised by passing settings with
`strict: true` and asserting on the `ScadUnsupportedFeatureException`. The triangle budget is exercised by
passing a tiny `maxTriangles` setting. The unit has no recursion to prove; the iterative-engine and large-budget
behaviors belong to the `ScadGeometryBuilder` unit's tests, whose limit tests (`ScadGeometryBuilderLimitTests`)
also exercise the sphere, cylinder, and circle estimates and are cited by that unit, not this one.

Unit tests reside in `ScadArgumentsTests.cs`, `ScadFragmentRuleTests.cs`, `ScadPrimitivesSolidTests.cs`,
`ScadPrimitivesShape2DTests.cs`, and `ScadPolyhedronTests.cs` within the `DemaConsulting.CanvasNet.Scad.Tests`
project, in the `Geometry` folder, with the shared helpers (`ScadGeometryTestSupport`: `Build`, `Settings`, the
mesh and leaf walkers, and `RegularPolygonArea`) in `ScadGeometryTestSupport.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None; the evaluator, the geometry builder, and the Render3D tessellators are the real
  implementations
- **Files**: None; every program is an in-memory string
- **Threads**: The tests run on the xUnit thread; the cancellation token comes from the test context
- **Isolation**: Each test builds its own program, sink, and context

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A wrong fragment count, a mesh that is open or inward-oriented, a volume or bound outside the stated
tolerance, a missing, extra, or reordered warning, a wrong warning text or location, a wrong feature identifier,
or a wrong exception type or message constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetScad-ScadGeometry-ScadPrimitives-ArgumentBinding: Positional, Named, And Undef

**Tests**: `ScadArgumentsTests.ScadArguments_Bind_Positional_FillsInOrder`,
`ScadArgumentsTests.ScadArguments_Bind_NamedAndMixed_BindByName`,
`ScadArgumentsTests.ScadArguments_TryGet_UndefValue_IsNotSupplied`,
`ScadPrimitivesSolidTests.ScadPrimitives_Cube_ArgumentForms_GiveExpectedBounds`

Asserts that unnamed arguments fill the parameters in order and leave the rest unsupplied, that named arguments
bind in any order and mix with unnamed ones, that an `undef` argument counts as not supplied, and, through the
cube's argument forms (default, vector, named, positional, mixed order), that the bound values reach the
primitive.

##### CanvasNetScad-ScadGeometry-ScadPrimitives-ArgumentWarnings: Unknown, Extra, And Duplicate Arguments

**Tests**: `ScadArgumentsTests.ScadArguments_Bind_UnknownExtraDuplicateAndSpecial_WarnsExceptSpecial`,
`ScadPrimitivesSolidTests.ScadPrimitives_Cube_UnknownArgumentAndChildren_WarnButStillBuild`

Asserts the exact three warnings, in order and at the instance's location, for an unknown name, the fourth
positional value of a three-parameter module, and a parameter supplied twice (the last value is kept), with the
`$fn` argument producing none; and that a cube with an unknown argument and a child object warns for both, child
object first, and is still built with volume 1.

##### CanvasNetScad-ScadGeometry-ScadPrimitives-TypedReaders: Finite Numbers, Vectors, And Indices

**Tests**: `ScadArgumentsTests.ScadArguments_TypedReaders_AcceptOnlyWellFormedValues`,
`ScadArgumentsTests.ScadArguments_TryGet_UnknownParameterName_Throws`

Asserts that `TryNumber` rejects NaN, infinity, booleans, and strings; that `TryNumbers` requires the exact length
and all-finite numeric elements; that `TryIndex` rejects fractions, negatives, values beyond `int.MaxValue`, and
strings; and that asking for a name that is not a parameter throws `ArgumentException`.

##### CanvasNetScad-ScadGeometry-ScadPrimitives-FragmentFormula: The Public Formula

**Tests**: `ScadFragmentRuleTests.ScadFragmentRule_Fragments_DefaultAngleAndSize_GivesAutomaticCount`,
`ScadFragmentRuleTests.ScadFragmentRule_Fragments_AngleSizeAndInvalidSteps_AreHandled`,
`ScadFragmentRuleTests.ScadFragmentRule_Fragments_InstanceSettings_MatchesScalarOverload`

Asserts the spot values of the defaults (radius 0.5 and 1 give 5, 2 gives 7, 10 and 100 give 30), that the angle
limit dominates for a large radius and the size limit for a small one, and that the overload taking the settings
of an instance returns the same value as the scalar overload.

##### CanvasNetScad-ScadGeometry-ScadPrimitives-FragmentClamps: `$fn`, Radius, And Step Clamps

**Tests**: `ScadFragmentRuleTests.ScadFragmentRule_Fragments_PositiveFn_WinsAndIsClamped`,
`ScadFragmentRuleTests.ScadFragmentRule_Fragments_HugeFn_IsCappedAtMaximum`,
`ScadFragmentRuleTests.ScadFragmentRule_Fragments_TinyOrInvalidRadius_GivesThree`,
`ScadFragmentRuleTests.ScadFragmentRule_Fragments_TinyRadiusRuleComesBeforeFn`,
`ScadFragmentRuleTests.ScadFragmentRule_Fragments_AngleSizeAndInvalidSteps_AreHandled`

Asserts that a positive `$fn` wins, is truncated, and is raised to 3; that `1e18` and infinity are capped at
`MaximumFragments`; that a zero, tiny (including exactly `1e-8`), negative, NaN, or infinite radius gives 3
whatever the other settings (the tiny-radius rule is applied before `$fn` is considered: `$fn = 100` on a radius of
`1e-9` gives 3 fragments, a cylinder of 12 triangles and a circle prism of 8, while radius `1e-7` gives 100 fragments and
400 triangles); that a zero or negative `$fa` or `$fs` is clamped to 0.01 (629 fragments for radius
1 with both zero); that a non-finite `$fa` or `$fs` falls back to its default; and that a NaN `$fn` falls through
to the automatic rule.

##### CanvasNetScad-ScadGeometry-ScadPrimitives-Cube: Box Extent, Placement, And Location

**Tests**: `ScadPrimitivesSolidTests.ScadPrimitives_Cube_ScalarSize_IsClosedCubeFromOrigin`,
`ScadPrimitivesSolidTests.ScadPrimitives_Cube_ArgumentForms_GiveExpectedBounds`,
`ScadPrimitivesSolidTests.ScadPrimitives_Cube_TwoInstances_MapRecordsEachLocation`,
`ScadPrimitivesSolidTests.ScadPrimitives_Cube_InLoopAndModule_BuildsEveryInstance`

Asserts that `cube(10)` is closed with volume 1000 and bounds from the origin to 10 on every axis without reports;
that the default, vector, centered (including truthy `1` and falsy `0`), positional, and named forms give the
expected bounds and volume; that the location map records each instance's location; and that a cube in a module
called from a loop builds every instance with the expected volumes 1, 8, and 27.

##### CanvasNetScad-ScadGeometry-ScadPrimitives-Sphere: Radius, Diameter, And Resolution

**Tests**: `ScadPrimitivesSolidTests.ScadPrimitives_Sphere_RadiusAndDiameter_ClosedWithFacetedVolume`,
`ScadPrimitivesSolidTests.ScadPrimitives_Sphere_DiameterOverridesRadius_PositionalIsRadius`,
`ScadPrimitivesSolidTests.ScadPrimitives_Sphere_FragmentSettings_GiveExpectedVertexCount`

Asserts that a sphere of radius 10 is closed with bounds of plus and minus 10 and a faceted volume between 98 and
100 percent of the analytic one, that `d = 20` builds the identical sphere, that `d` overrides `r` and the
positional argument is the radius, and that the vertex count follows the default rule, `$fn` as an argument, `$fn`
inherited from the scope (an argument wins), and `$fa`/`$fs`.

##### CanvasNetScad-ScadGeometry-ScadPrimitives-Cylinder: Cylinder, Cone, And Frustum

**Tests**: `ScadPrimitivesSolidTests.ScadPrimitives_Cylinder_Forms_HaveExactFacetedVolume`,
`ScadPrimitivesSolidTests.ScadPrimitives_Cylinder_ConeAndFrustum_HaveExactFacetedVolume`,
`ScadPrimitivesSolidTests.ScadPrimitives_Cylinder_FragmentsFollowLargerRadius`

Asserts that named, positional, centered, and diameter forms give the exact faceted volume (the regular polygon's
area times the height), the expected lowest and highest Z, and the expected radius; that frustums and cones with a
zero end have exactly the prismatoid volume and the expected radius at each end; and that
`cylinder(h = 1, r1 = 1, r2 = 10)` has two rings of 30 vertices plus two cap centers (62) because the larger
radius selects the fragment count.

##### CanvasNetScad-ScadGeometry-ScadPrimitives-CylinderRadii: Radius Precedence

**Tests**: `ScadPrimitivesSolidTests.ScadPrimitives_Cylinder_RadiusPrecedence_FollowsDocumentedOrder`

Asserts, row by row, the radius at the bottom and the top for the default, `r`, `r` with `d`, `r` with `r1`, `d`
with `r2`, `r1` with `d1`, and `r2` with `d2`, which pins the order default, `r`, `d`, `r1`/`r2`, `d1`/`d2`.

##### CanvasNetScad-ScadGeometry-ScadPrimitives-InvalidSolidArguments: Warnings For Solids

**Tests**: `ScadPrimitivesSolidTests.ScadPrimitives_Cube_InvalidSize_WarnsAndBuildsNothing`,
`ScadPrimitivesSolidTests.ScadPrimitives_Sphere_InvalidSize_WarnsAndBuildsNothing`,
`ScadPrimitivesSolidTests.ScadPrimitives_Cylinder_InvalidArguments_WarnAndBuildNothing`,
`ScadPolyhedronTests.ScadPrimitives_Polyhedron_InvalidArguments_WarnAndBuildNothing`

Asserts that a zero, negative, string, wrong-length, or non-numeric size, radius, diameter, or height; a negative
or non-numeric cylinder radius; a cylinder with no positive radius; and every malformed or missing `points` and
`faces` of a polyhedron (not a list, wrong dimension, non-numeric, empty, an index out of range, fewer than three
indices, a repeated index, a negative index) produce exactly one warning of the form
`<line>:<column>: <module>: <problem>; the instance is ignored` at the instance's location and an empty scene.

##### CanvasNetScad-ScadGeometry-ScadPrimitives-Shape2D: Square, Circle, And Slabs

**Tests**: `ScadPrimitivesShape2DTests.ScadPrimitives_Square_Forms_GiveSlabOfExpectedArea`,
`ScadPrimitivesShape2DTests.ScadPrimitives_Circle_Forms_GiveInscribedPolygonSlab`,
`ScadPrimitivesShape2DTests.ScadGeometryBuilder_Build_FlatShapeThickness_SetsSlabHeight`,
`ScadPrimitivesShape2DTests.ScadGeometryBuilder_Build_SeveralFlatShapes_AreAllSlabs`,
`ScadPrimitivesShape2DTests.ScadGeometryBuilder_Build_FlatShape_ReportsNothing`

Asserts that every square form (default, number, vector, centered, positional) gives a closed slab of the default
thickness 0.1 with the expected bounds and area; that circles from `r`, `d`, `d` overriding `r`, positional radius,
`$fn`, the default count, and an inherited `$fn` give the slab of the inscribed regular polygon the fragment rule
selects; that the slab thickness follows the setting (2.5); that several flat shapes, through a module and a loop,
are all slabs with the expected volumes; and that a plain circle produces no warning and no unsupported feature.

##### CanvasNetScad-ScadGeometry-ScadPrimitives-InvalidShapeArguments: Warnings For 2D Shapes

**Tests**: `ScadPrimitivesShape2DTests.ScadPrimitives_Square_InvalidSize_WarnsAndBuildsNothing`,
`ScadPrimitivesShape2DTests.ScadPrimitives_Circle_InvalidSize_WarnsAndBuildsNothing`,
`ScadPrimitivesShape2DTests.ScadPrimitives_Polygon_InvalidArguments_WarnAndBuildNothing`

Asserts the exact located warning and an empty scene for a non-positive or wrongly typed square or circle size,
and for a polygon without `points`, with fewer than three points, with points that are not pairs of numbers, and
with `paths` that are empty, hold an index out of range or negative, are not lists of lists, or are not a list.

##### CanvasNetScad-ScadGeometry-ScadPrimitives-Polygon: Contours, Holes, And Islands

**Tests**: `ScadPrimitivesShape2DTests.ScadPrimitives_Polygon_SingleContour_WindingIndependent`,
`ScadPrimitivesShape2DTests.ScadPrimitives_Polygon_Paths_DescribeHolesAndIslands`

Asserts that a triangle of area 6 gives the same slab counter-clockwise and clockwise, and that paths give the
expected area for a hole with either winding (84), two separate islands (8), and a hole containing an island
(100 - 36 + 4).

##### CanvasNetScad-ScadGeometry-ScadPrimitives-PolygonInvalid: Rejected Polygons

**Tests**: `ScadPrimitivesShape2DTests.ScadPrimitives_Polygon_SelfCrossing_ReportsPolygonInvalid`,
`ScadPrimitivesShape2DTests.ScadPrimitives_Polygon_SelfCrossing_StrictThrows`

Asserts that a bow-tie polygon builds nothing and reports exactly one unsupported feature,
`scad-polygon-invalid`, at the instance's location with a message that begins with
`polygon: the shape was rejected:`, and that strict mode throws a `ScadUnsupportedFeatureException` carrying the
same feature and location.

##### CanvasNetScad-ScadGeometry-ScadPrimitives-Polyhedron: Faces, Winding, And Open Shapes

**Tests**: `ScadPolyhedronTests.ScadPrimitives_Polyhedron_ClockwiseCube_IsClosedWithPositiveVolume`,
`ScadPolyhedronTests.ScadPrimitives_Polyhedron_Tetrahedron_HasExpectedVolume`,
`ScadPolyhedronTests.ScadPrimitives_Polyhedron_LegacyTriangles_UsedOnlyWithoutFaces`,
`ScadPolyhedronTests.ScadPrimitives_Polyhedron_OpenShapeAndUnusedPoint_AreKept`,
`ScadPolyhedronTests.ScadPrimitives_Polyhedron_CounterClockwiseCube_IsReversedWithWarning`

Asserts that a cube listed clockwise from outside is closed with volume 1, 8 vertices, 12 triangles, and no
report; that a tetrahedron has volume one sixth by named and positional arguments; that `triangles` is used only
without `faces` (and `faces` wins when both are given); that a single triangle plus an unused point is kept as an
open mesh without a warning; and that the same cube listed counter-clockwise is reversed to volume 1 with exactly
one warning at the instance's location.

##### CanvasNetScad-ScadGeometry-ScadPrimitives-PolyhedronTriangulation: Concave And Invalid Faces

**Tests**: `ScadPolyhedronTests.ScadPrimitives_Polyhedron_ConcaveFaces_AreTriangulated`,
`ScadPolyhedronTests.ScadPrimitives_Polyhedron_NonPlanarQuad_IsFannedAndClosed`,
`ScadPolyhedronTests.ScadPrimitives_Polyhedron_FaceThatCannotBeTriangulated_ReportsFaceInvalid`,
`ScadPolyhedronTests.ScadPrimitives_Polyhedron_FaceThatCannotBeTriangulated_StrictThrows`

Asserts that an L-shaped prism with two concave hexagonal faces is closed with the exact volume 3 and 16
triangles (4 per concave face, 2 per quad); that a cube with a raised corner (non-planar top quad) is fanned into
a closed mesh without warning; that a face of collinear points and a self-crossing face report
`scad-polyhedron-face-invalid` at the instance's location and omit the polyhedron; and that strict mode throws
with that feature.

##### CanvasNetScad-ScadGeometry-ScadPrimitives-TriangleBudget: Budget Before Allocation

**Tests**: `ScadPolyhedronTests.ScadPrimitives_Polyhedron_ExceedsTriangleBudget_ThrowsWithLocation`,
`ScadPrimitivesShape2DTests.ScadPrimitives_Polygon_Paths_IgnoreUnusedPointsForTriangleBudget`,
`ScadPrimitivesShape2DTests.ScadPrimitives_Polygon_Paths_LargeReferencedPath_ExceedsTriangleBudget`,
`ScadPrimitivesShape2DTests.ScadPrimitives_Polygon_Paths_ChargeEqualsCreatedTriangles`

Asserts that a cube polyhedron that needs 12 triangles under a limit of 11 throws a `ScadEvaluationException`
located at the instance with the text `Triangle limit exceeded (limit 11)`, before the polyhedron is triangulated.
It also asserts that 100 unused polygon points do not cause a one-triangle path to exceed a limit of 100, that a
path referencing all 100 points is rejected by that limit, and that the sparse polygon's charge equals its eight
created slab triangles. The charges of the sphere, cylinder, and circle are verified at the builder level by
`ScadGeometryBuilderLimitTests` (cited by the `ScadGeometryBuilder` unit).

##### CanvasNetScad-ScadGeometry-ScadPrimitives-GeometryRejected: Catch-All Rejection

**Tests**: `ScadPolyhedronTests.ScadPrimitives_Polyhedron_WeldedDuplicatePointsInOneFace_ReportsGeometryRejected`

Asserts that a quad whose points 0 and 3 coincide never crashes the build: either the build reports
`scad-geometry-rejected` or `scad-polyhedron-face-invalid` and builds nothing, or it produces a single open
triangle. The catch-all path is shared by every builder; its reachability from source text is limited because the
argument checks foresee almost every rejection (see _ScadPrimitives Unit Design_), so the test accepts the
outcomes that the welded input can legitimately take.
