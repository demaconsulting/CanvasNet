### FragmentBuffer Unit Verification Design

<!-- cspell:ignore rasterizer rasterization -->

This document describes the unit-level verification strategy for the internal `FragmentBuffer`
type and its `Fragment` record struct and count/fill visitors.

#### Verification Approach

`FragmentBuffer` is internal, so the tests reach it through the test assembly's `InternalsVisibleTo`
access. Fragments are produced by the **real** `TriangleRasterizer.Rasterize` from hand-built
`ScreenTriangle`s (the shared `Render3DTestHelpers.MakeScreenTriangle`), and the stored content is
compared with the hits that a counting visitor (a test-only `IFragmentVisitor`) reports for each
triangle separately, so the expected data is independent of the buffer. Two triangles cover a
16 x 16 sample tile exactly once between them, which gives exact analytic totals (120, 136, and 256
fragments) for the budget scenarios. The unit has no external dependencies, so no mocking or
stubbing is required.

Unit tests reside in `FragmentBufferTests.cs` within the
`DemaConsulting.CanvasNet.Render3D.Tests` project, in the `SceneRendering` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test constructs its own buffer and triangles

#### Unit-Level Test Scenarios

##### CanvasNetRender3D-SceneRendering-FragmentBuffer-Collect: Every Fragment Is Collected Exactly

**Tests**: `FragmentBufferTests.FragmentBuffer_Collect_OverlappingTriangles_HoldsEveryRasterizerHitPerSample`,
`FragmentBufferTests.FragmentBuffer_Collect_TileAtOffset_CollectsOnlySamplesInsideTile`,
`FragmentBufferTests.FragmentBuffer_EndCounting_FirstTile_AllocatesExactlyTheNeededFragments`

Asserts that, for three overlapping triangles, every sample holds exactly the `(depth, triangle)`
pairs the rasterizer reports in triangle order, that for a tile at a non-zero origin only samples
inside the tile are collected and addressed locally, that the fragment storage of the first tile has
exactly the needed size, and that a fragment of an extremely large depth is retained.

##### CanvasNetRender3D-SceneRendering-FragmentBuffer-Budget: The Budget Is Enforced Deterministically

**Tests**: `FragmentBufferTests.FragmentBuffer_CountTriangle_ExactlyAtBudget_Succeeds_OneAboveBudget_Throws`,
`FragmentBufferTests.FragmentBuffer_CountTriangle_BudgetOutcomeAndMessage_IndependentOfOrderAndTile`,
`FragmentBufferTests.FragmentBuffer_Begin_AfterBudgetExceeded_CollectsNextTile`,
`FragmentBufferTests.FragmentBuffer_Constructor_InvalidBudget_ThrowsArgumentOutOfRangeException`

Asserts that a tile of exactly the budget succeeds and one fragment more throws
`RenderBudgetExceededException` carrying the budget, that the same triangle set in a different order
and on a different tile gives the same outcome and message, that the buffer is usable for the next
tile after a failure, and that a budget of zero, a negative budget, or one above `Array.MaxLength` is
rejected.

##### CanvasNetRender3D-SceneRendering-FragmentBuffer-Reuse: Storage Is Reused Across Tiles

**Tests**: `FragmentBufferTests.FragmentBuffer_Begin_ReusedAcrossTiles_ReusesStorageAndNeverLeaksStaleFragments`

Asserts that a smaller second tile keeps the storage size and shows none of the first tile's
fragments, and that a larger third tile grows the storage to exactly its need.

##### CanvasNetRender3D-SceneRendering-FragmentBuffer-Lifecycle: Misuse Is Detected

**Tests**: `FragmentBufferTests.FragmentBuffer_Passes_OutOfOrder_ThrowsInvalidOperationException`,
`FragmentBufferTests.FragmentBuffer_GetSample_IncompleteFill_ThrowsInvalidOperationException`

Asserts that counting or filling before `Begin`, filling before `EndCounting`, counting or ending
again after `EndCounting`, and reading a sample whose fragments were counted but not stored all throw
`InvalidOperationException`.

#### Acceptance Criteria

A unit-level test run passes when every scenario above passes without error or exception beyond
those explicitly asserted. Any unexpected exception, missing or extra fragment, wrong storage size,
or budget outcome that depends on triangle order constitutes a failure.
