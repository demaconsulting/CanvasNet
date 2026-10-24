### SceneRenderer Unit Verification Design

<!-- cspell:ignore Binner binner supersampling supersamples subtrahend subtrahends minuend speckle -->
<!-- cspell:ignore tessellated -->

This document describes the unit-level verification strategy for the `SceneRenderer` and
`SceneRenderOptions` classes, the diagnostic types `RenderBudgetExceededException`,
`SceneRenderUnsupportedFeature`, and `SceneRenderUnsupportedFeatureException`, and the internal
`CsgOperandValidator`, `TileBinner`, `TileBins`, `SampleResolver`, and `TileWorkspace` types.

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

Fragment-list constructive solid geometry is verified end to end through `SceneRenderer.Render`
with three techniques. **Flat shading with an unmistakable back-face color** makes a pixel's color
name the surface that produced it. **Independent oracles** (an analytic box-CSG evaluation at every
pixel of a top-down view, including seeded random trees; a tessellated sphere's cross-section;
the equivalent baked mesh for mirrored operands; the z-buffer image for union-only scenes) judge
correctness without golden images. **Contract probes** drive the budget (bisection for the exact
boundary, sequential and parallel), cancellation precedence, and the operand-reporting delivery
rules (recording handler, strict mode, deduplication and order, scope, and ordering relative to
rendering work) through the public options. The internal `SceneRenderer.RenderWithFragmentPath`
seam is used to prove the equivalence of the two paths.

Unit tests reside in `SceneRendererTests.cs`, `SceneRendererCsgTests.cs` (fragment-list rendering,
budget, mirrored CSG operands, fast path, determinism), `CsgOperandValidatorTests.cs` (operand
reporting), `SceneRenderOptionsTests.cs`, `SceneRenderDiagnosticsTests.cs`, `TileBinnerTests.cs`,
and `SampleResolverTests.cs` within the `DemaConsulting.CanvasNet.Render3D.Tests` project, in the
`SceneRendering` folder. Shared helpers (flat-shading options, orthographic cameras, quad and box
mesh builders, an asymmetric solid, surface comparison) are in `Render3DTestHelpers.cs`.

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
`SceneRendererTests.SceneRenderer_Render_NegativeScaleInGroup_FacesAreFrontFacing`,
`SceneRendererTests.SceneRenderer_Render_MirroredAsymmetricSolid_MatchesBakedMirrorWithNoBackFaces`,
`SceneRendererTests.SceneRenderer_Render_DoubleMirroredAsymmetricSolid_FacesStayFrontFacing`

Asserts that a symmetric cube under a mirror transform renders byte-identically to the original, and
that a cube under a group with a negative scale is shown with front-face shading, not the back-face
color. Because a symmetric object cannot reveal a swapped front/back classification, the strong
scenarios render an **asymmetric** solid under negative scale on one, two, and three axes and assert
that the image equals the same mesh with the mirror baked into its vertices (`Mesh.Transform`) and
contains no back-face-colored pixel, and that a double mirror (two negative scales) is front-facing
again (acceptance scenario A7, z-buffer part).

##### CanvasNetRender3D-SceneRendering-SceneRenderer-CsgFastPath: Union-Only Scenes Keep The Z-Buffer Path

**Tests**: `SceneRendererCsgTests.SceneRenderer_Render_UnionCsgNode_IdenticalToGroupNode`,
`SceneRendererCsgTests.SceneRenderer_RenderWithFragmentPath_UnionOnlyScenes_IdenticalToZBuffer`,
`SceneRendererCsgTests.SceneRenderer_Render_UnionOnlyScene_IgnoresFragmentBudget`

Asserts that a union `CsgNode` renders byte-identically to the equivalent `GroupNode`; that a
union-only scene renders successfully, with the same image as under the default budget, with a
fragment budget of one (the budget is ignored because no fragment storage exists; operands of a
union-only scene are never validated, see the operand-reporting scenario); and, through the
internal `SceneRenderer.RenderWithFragmentPath` seam that forces the fragment-list path (single
leaf, program `L0`) onto union-only scenes of closed solids (disjoint, overlapping, and mirrored
solids), that the fragment-list image equals the z-buffer image with antialiasing, sequentially and
in parallel, up to at most two differing pixels (grazing silhouette samples whose front and back
surfaces lie within the depth tolerance; typically none). Acceptance scenario A9.

##### CanvasNetRender3D-SceneRendering-SceneRenderer-CsgSurfaces: Fragment-List Rendering Resolves The Visible Surface

**Tests**: `SceneRendererCsgTests.SceneRenderer_Render_CubeMinusSphere_ShowsCavityInSubtrahendColor`,
`SceneRendererCsgTests.SceneRenderer_Render_CubeMinusSphereOblique_CutSurfaceUsesFlippedNormal`,
`SceneRendererCsgTests.SceneRenderer_Render_CubeIntersectSphere_SilhouetteIsSquareClippedByDisc`,
`SceneRendererCsgTests.SceneRenderer_Render_NestedDifferenceOfDifferences_MatchesOracle`,
`SceneRendererCsgTests.SceneRenderer_Render_RandomBoxTrees_MatchOracle`,
`SceneRendererCsgTests.SceneRenderer_Render_CoincidentFaceDifference_HasNoSpeckle`,
`SceneRendererCsgTests.SceneRenderer_Render_NearlyCoincidentFaceDifference_HasNoSpeckle`,
`SceneRendererCsgTests.SceneRenderer_Render_NearlyCoincidentFacesAcrossPlacements_HaveNoSpeckle`,
`SceneRendererCsgTests.SceneRenderer_Render_PerspectiveCameraInsideSolid_ShowsBackFaceEverywhere`,
`SceneRendererCsgTests.SceneRenderer_Render_OrthographicCameraInsideSolid_ShowsBackFaceEverywhere`,
`SceneRendererCsgTests.SceneRenderer_Render_FittedCameraOutsideSolid_ShowsMinuendAndCavity`

Uses flat (ambient-only) options with a back-face color no surface uses, so a pixel's color names
the surface that produced it. Asserts:

- **A1, cube minus sphere**: the cavity is shown in the subtrahend's own color with a footprint
  matching the tessellated sphere's cross-section; in an oblique view the cut surface is lit from
  the flipped normal (it differs from the union's bump and from the back-face color).
- **A2, cube intersect sphere**: the silhouette is the square clipped by the disc.
- **A3, oracle**: a nested difference of differences and seeded random box trees (depth up to four,
  three to eight boxes) equal an analytic oracle evaluated independently at every pixel of a
  top-down view.
- **A4, coincident faces**: a difference whose faces coincide with the minuend's, viewed from above,
  below, and obliquely, shows only the two surface colors and the background (no speckle); the
  same holds when the coincidence is only approximate (mathematically equal but different transform
  chains with a 90-degree rotation and decimal offsets) and over many placements with oblique views
  above and below, within the documented small allowance of isolated edge-on silhouette samples.
- **A5, camera inside a solid**: perspective and orthographic cameras inside a large cube see only
  back-face shading, including through a cavity, matching the plain cube where rays miss the cavity;
  a fitted camera outside the solid still shows the minuend and the cavity.

##### CanvasNetRender3D-SceneRendering-SceneRenderer-Budget: The Fragment Budget Fails Deterministically

**Tests**: `SceneRendererCsgTests.SceneRenderer_Render_BudgetExceeded_ThrowsDeterministicException`,
`SceneRendererCsgTests.SceneRenderer_Render_ExactBudgetBoundary_RendersAndOneLessThrows`,
`SceneRendererCsgTests.SceneRenderer_Render_CancelledAndOverBudget_ThrowsOperationCanceled`

Asserts (acceptance scenario A6) that an over-budget scene throws `RenderBudgetExceededException`
with the same `MaxFragmentsPerTile` and message for sequential, two-way, and automatic parallelism
and never an `AggregateException`; that the smallest budget that renders (found by bisection; the
largest tile's fragment need) succeeds, sequentially and in parallel, with the same image as the
default budget, and one fragment less throws in both modes; and that on the parallel path a
cancelled token together with an over-budget scene yields `OperationCanceledException`
(cancellation takes precedence). That a union-only scene ignores the budget is asserted in the
fast-path scenario above.

##### CanvasNetRender3D-SceneRendering-SceneRenderer-OperandReporting: Unsuitable Operands Are Reported Before Rendering

**Tests**: `CsgOperandValidatorTests.CsgOperandValidator_Validate_DefectKind_ReportsFeatureAndRendersAnyway`,
`CsgOperandValidatorTests.CsgOperandValidator_Validate_HealthyOperands_ReportsNothing`,
`CsgOperandValidatorTests.CsgOperandValidator_Validate_StrictMode_ThrowsTypedException`,
`CsgOperandValidatorTests.CsgOperandValidator_Validate_StrictModeWithoutHandler_Throws`,
`CsgOperandValidatorTests.CsgOperandValidator_Validate_NoHandlerNoStrict_RendersTolerantly`,
`CsgOperandValidatorTests.CsgOperandValidator_Validate_UnionOnlyScene_ReportsNothing`,
`CsgOperandValidatorTests.CsgOperandValidator_Validate_AllInstances_DeduplicatedInPreOrder`,
`CsgOperandValidatorTests.CsgOperandValidator_Validate_MirroredOperands_InversionIsIndependentOfDeterminant`,
`CsgOperandValidatorTests.CsgOperandValidator_Validate_MirroredInvertedOperand_RendersLikeBakedMesh`,
`CsgOperandValidatorTests.CsgOperandValidator_ValidateNearPlane_ClippedOperands_Reported`,
`CsgOperandValidatorTests.CsgOperandValidator_ValidateNearPlane_StrictMode_Throws`,
`CsgOperandValidatorTests.CsgOperandValidator_Validate_RunsBeforeRendering`,
`CsgOperandValidatorTests.CsgOperandValidator_Validate_CancelledFromHandler_ThrowsOperationCanceled`,
`CsgOperandValidatorTests.CsgOperandValidator_NullArguments_Throw`

Renders a difference of a healthy cube and a defective operand with a recording handler (acceptance
scenario A8). Asserts that:

- each defect kind produces exactly its token (`render3d-csg-open-mesh`,
  `render3d-csg-non-manifold-edge`, `render3d-csg-inconsistent-winding`,
  `render3d-csg-degenerate-triangle`, `render3d-csg-inverted-mesh`) with the offending `MeshNode`,
  and the render still completes; healthy closed operands report nothing;
- strict mode throws `SceneRenderUnsupportedFeatureException` for the first problem with the
  handler not called, with or without a handler configured; with neither a handler nor strict mode
  the render is tolerant and silent;
- a union-only scene reports nothing even with an open mesh;
- every mesh instance of a CSG-path scene is validated (not only the direct operands), a node
  shared by several parents is reported once, and the order is the scene pre-order and repeatable;
- inversion is independent of the mirroring transform: a mirrored outward mesh reports nothing, a
  mirrored inverted mesh is still reported as inverted, and it really renders inside out (equal to
  the baked inverted-and-mirrored mesh), so the report is accurate;
- an explicit near plane in front of operand geometry yields one
  `render3d-csg-near-plane-clips-operands` report (with a null node), throws in strict mode before
  any tile work, and a near plane behind all geometry reports nothing;
- validation runs before rendering work (a strict report wins over a tiny fragment budget), observes
  cancellation (cancelling from the handler stops the render with `OperationCanceledException`), and
  the entry points reject null arguments.

##### CanvasNetRender3D-SceneRendering-SceneRenderer-MirroredCsg: Mirrored CSG Operands Behave Like Their Baked Equivalents

**Tests**: `SceneRendererCsgTests.SceneRenderer_Render_MirroredCsgOperand_MatchesBakedMirror`,
`SceneRendererCsgTests.SceneRenderer_Render_MirrorAboveCsgNode_MatchesBakedMirror`

Asserts (acceptance scenario A7) that an asymmetric operand of a difference under negative scale on
one, two, and three axes (all seven sign combinations) renders byte-identically to the same operand
with the mirror baked into its vertices and shows no back-face-colored pixel, and that the same
holds when the negative scales are split over the `CsgNode` itself, a grandparent group, and the
operand nodes (including the double-mirror combinations), on the fragment-list path. Only
asymmetric operands in several mirror configurations can detect a swapped front/back crossing
classification.

##### CanvasNetRender3D-SceneRendering-SceneRenderer-CsgDeterminism: CSG Output Is Byte-Identical

**Test**: `SceneRendererCsgTests.SceneRenderer_Render_CsgScene_ParallelAndSequentialAreByteIdentical`

Renders a scene with coincident faces, a mirrored operand, and several leaves with a maximum degree
of parallelism of 1, 2, and -1, repeated, at each supersampling factor, and asserts byte-identical
surfaces (acceptance scenario A10).

#### Acceptance Criteria

A unit-level test run passes when every scenario above passes without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong pixel or byte
value constitutes a failure. Additional tests in the same classes (for example, back-face color on
an open quad, depth-order independence, camera inside a closed box, explicit near and far planes,
and `RenderFitted` framing) provide corner-case regression coverage beyond the requirement-linked
scenarios.
