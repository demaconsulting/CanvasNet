### SceneRenderer Unit Verification Design

<!-- cspell:ignore Binner binner supersampling supersamples -->

This document describes the unit-level verification strategy for the `SceneRenderer` and
`SceneRenderOptions` classes and the internal `TileBinner`, `TileBins`, `SampleResolver`, and
`TileWorkspace` types.

#### Verification Approach

`SceneRenderer` is verified by rendering small scenes into real `Surface` instances and asserting on
pixels with analytic oracles. Scenes and cameras are chosen so that the expected result is known
exactly; for example, an orthographic camera with a height of 16 over a 64 x 64 image has
quarter-unit pixels, so a box whose edges lie exactly on sample centers must cover exactly
`32 x 32 = 1024` pixels under the top-left rule (columns 16 to 47 and rows 15 to 46, because the top
edge is owned on a y-down screen). Determinism, mirrored equivalence, numeric precision, and tile
independence are verified by comparing whole surfaces byte for byte. The internal `TileBinner` and
`SampleResolver` are verified through the test assembly's `InternalsVisibleTo` access with
hand-built triangles and a hand-filled `TileWorkspace`. Cancellation is verified with pre-cancelled
tokens (sequential and parallel) and with a token cancelled during rendering. No mocking or stubbing
is required.

Unit tests reside in `SceneRendererTests.cs`, `SceneRenderOptionsTests.cs`,
`SceneRenderDiagnosticsTests.cs`, `TileBinnerTests.cs`,
and `SampleResolverTests.cs` within the `DemaConsulting.CanvasNet.Render3D.Tests` project, in the
`SceneRendering` folder. Shared helpers (flat-shading options, orthographic cameras, quad and box
mesh builders, surface comparison) are in `Render3DTestHelpers.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test builds its own scene, camera, and options, and disposes its own surfaces
- **Cancellation tokens**: Tests that render pass the xUnit test context's cancellation token so a
  hung run is abandoned cleanly

#### Unit-Level Test Scenarios

##### CanvasNetRender3D-SceneRendering-SceneRenderer-Validation: Render Rejects Invalid Arguments

**Tests**: `SceneRendererTests.SceneRenderer_Render_NullSceneOrCamera_Throws`,
`SceneRendererTests.SceneRenderer_Render_InvalidDimensions_ThrowsArgumentOutOfRange`,
`SceneRendererTests.SceneRenderer_Render_OrthographicWithoutHeight_ThrowsArgumentException`,
`SceneRendererTests.SceneRenderer_Render_InvalidSupersampling_Throws`

Asserts `ArgumentNullException` for a null scene or camera, `ArgumentOutOfRangeException` for a
dimension outside 1 to `Surface.MaxDimension`, `ArgumentException` for an orthographic camera
without a height (the cross-property rule enforced at render time), and
`ArgumentOutOfRangeException` for an invalid supersampling factor (rejected when the options are
initialized).

##### CanvasNetRender3D-SceneRendering-SceneRenderer-OutputSize: Output Has The Requested Size

**Tests**: `SceneRendererTests.SceneRenderer_Render_ReturnsSurfaceOfRequestedSize`,
`SceneRendererTests.SceneRenderer_Render_NonTileMultipleSize_RendersPartialTiles`

Asserts that the returned surface has the requested width and height, and that sizes that are not
multiples of the 32-pixel tile render the partial edge tiles correctly.

##### CanvasNetRender3D-SceneRendering-SceneRenderer-Background: Uncovered Pixels Are The Background

**Tests**: `SceneRendererTests.SceneRenderer_Render_EmptyScene_FillsBackgroundColor`,
`SceneRendererTests.SceneRenderer_Render_PixelsOutsideSilhouette_AreBackground`

Asserts that an empty scene fills the surface with the background color and that pixels outside an
object's silhouette are exactly the background color.

##### CanvasNetRender3D-SceneRendering-SceneRenderer-Tiling: Tiles Are Binned And Seamless

**Tests**: `TileBinnerTests.TileBinner_Bin_TriangleSpanningTiles_AppearsInEachOverlappedTileInAscendingOrder`,
`SceneRendererTests.SceneRenderer_Render_QuadStraddlingTileBoundaries_HasNoSeams`,
`SceneRendererTests.SceneRenderer_Render_OrthoCubeOnDyadicGrid_CoversExactly32By32Pixels`

Asserts that a triangle spanning several tiles is listed in every overlapped tile in ascending
triangle order, that a quad straddling tile boundaries renders without seams, and that the dyadic
orthographic cube covers exactly 1024 pixels. The related `TileBinnerTests` scenarios
`TileBinner_Bin_OffGridTriangles_AreSkipped` and `TileBinner_Bin_PartiallyOffGridTriangle_IsClampedToGrid`
additionally verify that off-grid triangles are skipped and partly off-grid ones are clamped.

##### CanvasNetRender3D-SceneRendering-SceneRenderer-Supersampling: Supersampling Smooths Edges

**Tests**: `SampleResolverTests.SampleResolver_Resolve_HalfCoveredPixelTwoByTwo_AveragesColors`,
`SceneRendererTests.SceneRenderer_Render_Supersampling1_HasNoIntermediateEdgeColors`,
`SceneRendererTests.SceneRenderer_Render_Supersampling3_ProducesIntermediateEdgeColors`

Asserts that a half-covered pixel with 2 x 2 samples resolves to the average of the two colors, that
one sample per pixel yields only the surface and background colors, and that three samples per pixel
produce intermediate edge colors. `SampleResolverTests.SampleResolver_Resolve_OpaqueThreeByThree_EqualsRoundedAverage`
and `SampleResolverTests.SampleResolver_Resolve_FullyTransparentAndFullyCovered_AreExact` verify the
rounding and the exact fully-covered and fully-transparent cases.

##### CanvasNetRender3D-SceneRendering-SceneRenderer-AlphaResolve: Resolve Blends In Premultiplied Alpha

**Test**: `SampleResolverTests.SampleResolver_Resolve_TransparentBackgroundHalfCovered_PremultipliedAverageKeepsColor`

Asserts that a half-covered pixel over a fully transparent background resolves to half alpha with
the color channels equal to the shaded surface color (no darkening).

##### CanvasNetRender3D-SceneRendering-SceneRenderer-Parallelism: Output Is Identical For Every Degree Of Parallelism

**Tests**: `SceneRendererTests.SceneRenderer_Render_ParallelismOneVersusMany_ByteIdentical`,
`SceneRendererTests.SceneRenderer_Render_RepeatedRender_ByteIdentical`,
`SceneRenderOptionsTests.SceneRenderOptions_MaxDegreeOfParallelism_ZeroOrBelowMinusOne_Throws`

Asserts byte-identical surfaces for a sequential and a parallel render and for a repeated render,
and that a maximum degree of parallelism of zero or below negative one is rejected.

##### CanvasNetRender3D-SceneRendering-SceneRenderer-Cancellation: Cancellation Throws OperationCanceledException

**Tests**: `SceneRendererTests.SceneRenderer_Render_PreCancelledToken_ThrowsOperationCanceledException`,
`SceneRendererTests.SceneRenderer_Render_PreCancelledToken_Parallel_ThrowsOperationCanceledNotAggregate`,
`SceneRendererTests.SceneRenderer_Render_PreCancelledToken_ThrowsBeforeProcessingScene`,
`SceneRendererTests.SceneRenderer_RenderFitted_PreCancelledToken_ThrowsBeforeComputingBounds`

Asserts that a pre-cancelled token throws `OperationCanceledException` carrying the token, in both
the sequential and the parallel configuration, and that the exception is never an
`AggregateException`. The last two tests use a scene whose node transform overflows to infinity, so
any scene processing would raise a different exception; they prove that `Render` and `RenderFitted`
observe the token before computing bounds or flattening the scene.
`SceneRendererTests.SceneRenderer_Render_CancelledDuringRender_ThrowsOperationCanceledException`
additionally verifies cancellation while a render is in progress.

##### CanvasNetRender3D-SceneRendering-SceneRenderer-Options: Options Defaults And Light Handling

**Tests**: `SceneRenderOptionsTests.SceneRenderOptions_Defaults_AreNeutralGrayWhiteBackgroundThreeSamples`,
`SceneRenderOptionsTests.SceneRenderOptions_Lights_Null_UsesDefaultRig`,
`SceneRenderOptionsTests.SceneRenderOptions_Lights_CopiedDefensively`

Asserts the documented defaults (neutral gray surfaces, white background, three samples per axis),
that a null light list selects the default rig, and that the supplied light list is copied so later
mutation has no effect. `SceneRenderOptionsTests.SceneRenderOptions_NumericRanges_AreValidated`
verifies the range checks of the numeric options.

##### CanvasNetRender3D-SceneRendering-SceneRenderer-CsgOptions: Fragment Budget And Unsupported-Feature Options

**Tests**: `SceneRenderOptionsTests.SceneRenderOptions_CsgDefaults_BudgetFourMiAndTolerant`,
`SceneRenderOptionsTests.SceneRenderOptions_MaxFragmentsPerTile_OutOfRange_Throws`,
`SceneRenderOptionsTests.SceneRenderOptions_UnsupportedFeatureSettings_StoredAsGiven`

Asserts that the default budget is 4 Mi (4194304) fragments with no handler and non-strict
behavior, that a budget below one or above `Array.MaxLength` is rejected with
`ArgumentOutOfRangeException` while the inclusive bounds 1 and `Array.MaxLength` are accepted, and
that a supplied handler and strict flag are stored unchanged.

##### CanvasNetRender3D-SceneRendering-SceneRenderer-Diagnostics: Diagnostic Types Carry Their Data

**Tests**: `SceneRenderDiagnosticsTests.RenderBudgetExceededException_Budget_CarriesBudgetAndStandardMessage`,
`SceneRenderDiagnosticsTests.RenderBudgetExceededException_StandardConstructors_UseDefaultBudget`,
`SceneRenderDiagnosticsTests.SceneRenderUnsupportedFeature_Record_ExposesValues`,
`SceneRenderDiagnosticsTests.SceneRenderUnsupportedFeatureException_FeatureConstructor_CarriesFeatureAndNode`,
`SceneRenderDiagnosticsTests.SceneRenderUnsupportedFeatureException_StandardConstructors_HaveEmptyFeature`

Asserts that the budget exception carries the exceeded budget and an identical message for the same
budget, that the standard constructors report the default budget, that the report record exposes
its feature, message and optional node with value equality, and that the unsupported-feature
exception carries its feature token and node (empty feature and null node for the standard
constructors).

##### CanvasNetRender3D-SceneRendering-SceneRenderer-Precision: Far-Away Geometry Renders Identically

**Test**: `SceneRendererTests.SceneRenderer_Render_CubeOneMillionUnitsAway_IdenticalToCubeAtOrigin`

Asserts, for both an orthographic and a perspective camera, that a cube and its camera translated by
one million units render byte-identically to the same cube and camera at the origin.

##### CanvasNetRender3D-SceneRendering-SceneRenderer-Mirrored: Mirrored Instances Are Front-Facing

**Tests**: `SceneRendererTests.SceneRenderer_Render_MirroredSymmetricCube_IdenticalToOriginal`,
`SceneRendererTests.SceneRenderer_Render_NegativeScaleInGroup_FacesAreFrontFacing`

Asserts that a symmetric cube under a mirror transform renders byte-identically to the original, and
that a cube under a group with a negative scale is shown with front-face shading, not the back-face
color.

#### Acceptance Criteria

A unit-level test run passes when every scenario above passes without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong pixel or byte
value constitutes a failure. Additional tests in the same classes (for example, back-face color on
an open quad, depth-order independence, camera inside a closed box, explicit near and far planes,
and `RenderFitted` framing) provide corner-case regression coverage beyond the requirement-linked
scenarios.
