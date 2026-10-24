### TriangleRasterizer Unit Verification Design

<!-- cspell:ignore rasterizer rasterization -->

This document describes the unit-level verification strategy for the internal `TriangleRasterizer`,
`TriangleClipper`, `SampleProjection`, `ClipVertex`, `ClipPlane`, `ViewTriangle`, `ScreenTriangle`,
`TileRect`, `IFragmentVisitor`, and `DepthTestVisitor` types.

#### Verification Approach

Every type of this unit is internal, so the tests reach it through the test assembly's
`InternalsVisibleTo` access. Coverage is verified with a **counting visitor** (a test-only
`IFragmentVisitor` that records how many times each sample is visited) so that exactly-once
coverage of shared edges, analytic sample counts, and tile restriction can be asserted without any
rendering. Setup is verified by feeding `ViewTriangle`s to `TriangleRasterizer.Setup` and inspecting
the `ScreenTriangle`s it appends (facing, area, depth, fixed-point coordinates, absence of output).
The z-buffer tie rule is verified by driving a `DepthTestVisitor` against a `TileWorkspace` with
fragments at chosen depths and priorities. Randomized properties use a fixed seed so the tests are
deterministic. The unit has no external dependencies, so no mocking or stubbing is required.

Unit tests reside in `TriangleRasterizerTests.cs` (rasterization, setup, projection, and depth
test) and `TriangleClipperTests.cs` (clipping) within the `DemaConsulting.CanvasNet.Render3D.Tests`
project, in the `SceneRendering` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the counting visitor is a hand-written test double of the
  `IFragmentVisitor` interface
- **Isolation**: Each test constructs its own triangles and workspace; random tests use a fixed seed

#### Unit-Level Test Scenarios

##### CanvasNetRender3D-SceneRendering-TriangleRasterizer-FixedPointCoverage: Coverage Is Exact In Fixed Point

**Tests**: `TriangleRasterizerTests.TriangleRasterizer_Rasterize_AxisAlignedRightTriangle_CoversAnalyticSampleCount`,
`TriangleRasterizerTests.TriangleRasterizer_Rasterize_SubSampleVertexOffsets_RoundToNearestEighthOf256`

Asserts that an axis-aligned right triangle covers the analytically expected number of samples and
that sub-sample vertex offsets are rounded to the 24.8 fixed-point grid as documented.

##### CanvasNetRender3D-SceneRendering-TriangleRasterizer-TopLeftRule: Shared Edges Are Covered Exactly Once

**Tests**: `TriangleRasterizerTests.TriangleRasterizer_Rasterize_SharedEdgeOnSampleCenters_CoveredExactlyOnce`,
`TriangleRasterizerTests.TriangleRasterizer_Rasterize_TriangleFanAroundVertex_CoversEachSampleOnce`,
`TriangleRasterizerTests.TriangleRasterizer_Rasterize_RandomTriangulatedGrid_EveryInteriorSampleCoveredOnceNoneOutside`

Asserts that samples lying exactly on a shared edge are covered by exactly one of the two triangles,
that a fan of triangles around a vertex covers each sample once, and, over seeded random
triangulated grids, that every interior sample is covered exactly once and no sample outside the
grid is covered.

##### CanvasNetRender3D-SceneRendering-TriangleRasterizer-Winding: Facing Is Derived From Projected Winding

**Tests**: `TriangleRasterizerTests.TriangleRasterizer_Setup_CounterClockwiseOutwardQuad_IsFrontFacing`,
`TriangleRasterizerTests.TriangleRasterizer_Setup_ReversedWinding_IsBackFacing`

Asserts that a quad whose world winding is counter-clockwise as seen from the camera yields
front-facing screen triangles with the front color, and that the reversed winding yields back-facing
triangles with the back color.

##### CanvasNetRender3D-SceneRendering-TriangleRasterizer-NearClipping: Near-Plane Clipping Is Correct And Consistent

**Tests**: `TriangleClipperTests.TriangleClipper_NearPlane_TriangleCrossingPlane_ClipsToFrontPortion`,
`TriangleClipperTests.TriangleClipper_NearPlane_FullyBehind_ProducesNothing`,
`TriangleClipperTests.TriangleClipper_SharedEdgeIntersection_IsBitIdenticalForBothNeighbors`

Asserts that a triangle crossing the near plane is clipped to its in-front portion, that a triangle
entirely behind the plane produces nothing, and that the intersection point on an edge shared by two
triangles is bit-identical for both neighbors.

##### CanvasNetRender3D-SceneRendering-TriangleRasterizer-GuardBand: Huge Triangles Stay Within Fixed-Point Range

**Tests**: `TriangleRasterizerTests.TriangleClipper_GuardBand_HugeTriangle_ClipsAndStaysWithinFixedPointRange`,
`TriangleRasterizerTests.TriangleRasterizer_Rasterize_HugeTriangleCoveringTile_CoversAllSamples`

Asserts that a triangle projecting far outside the screen is clipped to the guard band with all
fixed-point coordinates within the documented range, and that a huge triangle covering a tile covers
every sample of the tile.

##### CanvasNetRender3D-SceneRendering-TriangleRasterizer-DepthInterpolation: Depth Is Interpolated Correctly

**Tests**: `TriangleRasterizerTests.TriangleRasterizer_Rasterize_PerspectivePlane_DepthIsPerspectiveCorrect`,
`TriangleRasterizerTests.TriangleRasterizer_Rasterize_OrthographicSlantedPlane_DepthIsLinear`

Asserts that the depth produced for a slanted plane under a perspective projection is
perspective-correct (matches the analytic plane depth) and, under an orthographic projection, is
linear in screen space.

##### CanvasNetRender3D-SceneRendering-TriangleRasterizer-Degenerate: Degenerate Input Produces No Output

**Tests**: `TriangleRasterizerTests.TriangleRasterizer_Setup_CollinearVertices_ProducesNoTriangle`,
`TriangleRasterizerTests.TriangleRasterizer_Setup_NonFiniteVertex_IsSkipped`

Asserts that collinear vertices produce no screen triangle and that a triangle with a non-finite
vertex is skipped.

##### CanvasNetRender3D-SceneRendering-TriangleRasterizer-TieResolution: Depth Ties Resolve Deterministically

**Tests**: `TriangleRasterizerTests.DepthTestVisitor_Visit_CoincidentWithinTolerance_LowerPriorityWins`,
`TriangleRasterizerTests.DepthTestVisitor_Visit_FrontBeatsBackWithinTolerance`,
`TriangleRasterizerTests.DepthTestVisitor_Visit_BeyondTolerance_NearerWins`,
`TriangleRasterizerTests.DepthTestVisitor_Visit_TieWinner_StoresMinimumDepth`

Asserts that, within the tolerance, the lower draw priority wins and a front-facing fragment beats a
back-facing one, that beyond the tolerance the nearer fragment wins, and that the stored depth after
a tie is the minimum of the two depths.

##### CanvasNetRender3D-SceneRendering-TriangleRasterizer-CsgAttributes: CSG Attributes Follow The Facing

**Tests**: `TriangleRasterizerTests.TriangleRasterizer_Setup_CsgAttributes_AreCopiedAndSelectedByFacing`

Asserts that a front-facing and a reversed (back-facing) triangle both carry the view triangle's CSG
leaf, that the front shade pair is selected for the front-facing triangle and the back shade pair
for the back-facing one, and that the z-buffer `Color`, facing, and area are unchanged.

#### Acceptance Criteria

A unit-level test run passes when every scenario above passes without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong sample count, double-covered or missed
sample, wrong facing, or wrong depth constitutes a failure.
