## SceneRendering Subsystem Verification Design

<!-- cspell:ignore Lambert rasterizer -->

This document describes the subsystem-level verification strategy for the `SceneRendering`
subsystem, containing six units: `Camera`, `Lighting`, `TriangleRasterizer`, `FragmentBuffer`,
`CsgResolver`, and `SceneRenderer`.

### Verification Approach

The `SceneRendering` subsystem is verified at two tiers. The unit tests (see _Camera Unit
Verification Design_, _Lighting Unit Verification Design_, _TriangleRasterizer Unit Verification
Design_, _FragmentBuffer Unit Verification Design_, _CsgResolver Unit Verification Design_, and
_SceneRenderer Unit Verification Design_ under `scene-rendering/`) verify each unit in
isolation, including its internal types. The subsystem-level tests in `SceneRenderingTests.cs`
verify the units working together: a fitted camera frames an object inside
the image, a custom headlight shades a face with the analytic Lambert value, two adjacent quads
leave no gap, a closed shape crossing tile boundaries has no seams, and the fragment-list
pipeline (triangle setup, rasterization, fragment collection, and CSG resolution) shows the cut
surface of a box cut into another. Rendered output is verified
with analytic oracles (exact computed colors and structural pixel properties) rather than golden
images. No mocking or stubbing is used.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the subsystem depends on the in-house `SceneModel` subsystem and the
  core `CanvasNet` system's `Surface` and `Rgba32`
- **Isolation**: Each test constructs its own scene, camera, and options and disposes its own
  surfaces; renders are small (at most a few hundred pixels per side)

### Acceptance Criteria

The `SceneRendering` subsystem's verification passes when every subsystem-level scenario below and
every unit-level scenario in the six unit verification documents passes without error or
unexpected exception.

### Test Scenarios

- **Fitted Camera Renders Object Inside Frame**:
  `SceneRenderingTests.SceneRendering_FittedCamera_RendersObjectInsideFrame` — renders a sphere with
  a `FitToBounds` camera and asserts that many pixels are drawn and none touch the image border.
- **Custom Headlight Shades Face Analytically**:
  `SceneRenderingTests.SceneRendering_CustomHeadlight_ShadesFaceAnalytically` — renders a quad
  facing the camera with an ambient light of 0.25 and a headlight of 0.5, and asserts the exact
  Lambert value (135 per channel, within one) and, tilted 60 degrees, the half-intensity value (90).
- **Two Adjacent Quads Leave No Gap**:
  `SceneRenderingTests.SceneRendering_TwoAdjacentQuads_LeaveNoGap` — renders two quads sharing a
  slanted welded edge that crosses tile boundaries and asserts that every pixel inside the union's
  bounding box is the surface color (no cracks).
- **Scene Straddling Tile Boundaries Has No Seams**:
  `SceneRenderingTests.SceneRendering_SceneStraddlingTileBoundaries_HasNoSeams` — renders a sphere
  that crosses all interior tile edges and asserts that, along every row, the drawn pixels form one
  contiguous run.
- **Fragment-List Pipeline Shows The Cut Surface**:
  `SceneRenderingTests.SceneRendering_FragmentListPipeline_BoxMinusBox_ShowsCutSurfaceInsideTheCutout` —
  feeds the triangles of two real box meshes through `TriangleRasterizer.Setup`, collects their
  fragments with a `FragmentBuffer`, resolves a 64 x 64 sample tile with a `CsgResolver` for
  `A - B`, and asserts that samples outside the cutout hold A's front face at the analytic depth,
  samples inside it hold B's back face (the cut surface, back-facing metadata) at its analytic
  depth, and samples off both are background.
