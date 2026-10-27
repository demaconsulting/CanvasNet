### CsgResolver Unit Verification Design

<!-- cspell:ignore subtrahend subtrahends minuend postfix speckle rasterizer transmittance -->

This document describes the unit-level verification strategy for the internal `CsgCompiler`,
`CsgCompilation`, `CsgProgram`, `CsgInstruction`, `CsgOpCode`, `LayerCompositor`, and `CsgResolver`
types.

#### Verification Approach

All types are internal, so the tests reach them through the test assembly's `InternalsVisibleTo`
access. Three techniques are combined:

- **Compiler and program** are verified on hand-built scene trees (`MeshNode`, `GroupNode`,
  `CsgNode`) by comparing the disassembled postfix program, the instance-to-leaf map (cross-checked
  against `Scene.EnumerateInstances`), and truth tables of the evaluator, including 130 leaves and
  10 000-deep trees.
- **Resolver scenarios** use hand-built `ScreenTriangle`s that carry only a leaf, facing, priority,
  an optional depth slope and plane, and identifiable enter/exit shades, and hand-built fragment lists, so
  each rule (initial state, tie groups including the slope-widened tolerance, choice within a group,
  far filtering) is isolated with an exact expected result.
- **Analytic oracle.** Seeded random scene trees of integer-coordinate boxes (groups, unions,
  differences with several subtrahends, intersections, single-child and empty nodes, shared nodes)
  are resolved for a grid of rays. The expected visible depth and event kind come from a direct
  evaluation of the tree at depths between the integer event depths, independent of the compiler and
  the resolver. Fragments are given in sorted and shuffled order (the results must be identical);
  boxes straddling the camera model a camera inside material; integer depths produce exact ties.
  Separate scenes with 70 to 90 leaves exercise the multi-word bitset.

The tile path is verified with fragments from the real rasterizer and `FragmentBuffer`. No mocking
or stubbing is required.

Unit tests reside in `CsgProgramTests.cs` (compiler and program) and `CsgResolverTests.cs`
(resolver) within the `DemaConsulting.CanvasNet.Render3D.Tests` project, in the `SceneRendering`
folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test constructs its own scene, triangles, and resolver; random tests use fixed
  seeds

#### Unit-Level Test Scenarios

##### CanvasNetRender3D-SceneRendering-CsgResolver-Program: The Tree Compiles To The Expected Program

**Tests**: `CsgProgramTests.CsgCompiler_Compile_NaryDifference_CollapsesUnionOperandsIntoLeaves`,
`CsgProgramTests.CsgCompiler_Compile_NaryIntersection_AndsAllChildren`,
`CsgProgramTests.CsgCompiler_Compile_NestedBooleans_ProducesPostfixOfTheTree`,
`CsgProgramTests.CsgCompiler_Compile_MixedGroupAndSingleChildBooleans_OrsChildren`,
`CsgProgramTests.CsgCompiler_Compile_EmptyNodesInsideBooleans_AreFalse`,
`CsgProgramTests.CsgCompiler_Compile_SharedNodeDag_MatchesInstanceEnumerationPerPath`,
`CsgProgramTests.CsgCompiler_Compile_MixedScene_LeafOfInstanceFollowsEnumerationOrder`,
`CsgProgramTests.CsgCompiler_Compile_DeeplyNestedDifferences_AreIterative`,
`CsgProgramTests.CsgCompiler_Compile_NullRoot_ThrowsArgumentNullException`

Asserts the exact postfix text for n-ary differences (`A - B - C`), intersections, nested
differences, mixed groups, empty nodes (FALSE for every operation) and single-child booleans, the
union collapse into shared leaves, a shared-node DAG expanded once per path, the leaf of every
instance in the pre-order of `Scene.EnumerateInstances`, and that 10 000-deep left- and
right-nested differences compile and evaluate without stack overflow.

##### CanvasNetRender3D-SceneRendering-CsgResolver-Evaluation: The Program Evaluates Exactly

**Tests**: `CsgProgramTests.CsgProgram_Evaluate_EveryOpcode_FollowsBooleanTruthTable`,
`CsgProgramTests.CsgProgram_Evaluate_MoreThan64Leaves_UsesMultipleBitsetWords`,
`CsgProgramTests.CsgProgram_Evaluate_AllLeavesOutside_IsOutside`,
`CsgProgramTests.CsgProgram_Constructor_MalformedInstructions_ThrowsArgumentException`

Asserts the truth table of every operator over all leaf states, correct results at bitset word
boundaries (leaves 63, 64, 65, 127, 128, 129), that an all-outside state is always outside, and that
malformed instruction sequences are rejected.

##### CanvasNetRender3D-SceneRendering-CsgResolver-FastPath: Union-Only Scenes Need No Program

**Tests**: `CsgProgramTests.CsgCompiler_Compile_UnionOnlyScene_HasNoProgramAndOneLeaf`,
`CsgProgramTests.CsgCompiler_Compile_SingleMeshAndEmptyNodes_HaveNoProgram`,
`CsgProgramTests.CsgCompiler_Compile_DeeplyNestedUnion_CollapsesIteratively`

Asserts that scenes made only of meshes, groups, unions, and single-child booleans (also 10 000 deep)
produce no program (an overlay, which would, is covered below) and map every instance to one leaf.

##### CanvasNetRender3D-SceneRendering-CsgResolver-InitialState: The Ray Start State Is Inferred From The Whole Ray

**Tests**: `CsgResolverTests.CsgResolver_Resolve_CameraInsideMinuend_ShowsFirstExitWithExitShade`,
`CsgResolverTests.CsgResolver_Resolve_CameraInsideMinuendThenSubtrahendFront_ShowsSubtrahendFrontWithExitShade`,
`CsgResolverTests.CsgResolver_Resolve_CameraInsideBothOperands_ShowsSubtrahendCutSurface`,
`CsgResolverTests.CsgResolver_Resolve_FragmentsBeyondFar_InferStateButAreNotVisible`

Asserts the visible exit with the exit shade for a camera inside the minuend, the exit at a
subtrahend's front face, the cut surface for a camera inside both operands, and that fragments
beyond the far distance still determine the state while never being shown.

##### CanvasNetRender3D-SceneRendering-CsgResolver-VisibleSurface: The First Change Along The Ray Is Visible

**Tests**: `CsgResolverTests.CsgResolver_Resolve_RayHitsMinuendFirst_ShowsMinuendFrontFace`,
`CsgResolverTests.CsgResolver_Resolve_RayPassesThroughSubtrahend_ShowsSubtrahendBackFaceAsCutSurface`,
`CsgResolverTests.CsgResolver_Resolve_NoFragmentsOrOnlySubtrahend_ShowsNothing`,
`CsgResolverTests.CsgResolver_Resolve_OverlappingUnionLeaf_StaysInsideUntilLastMemberLeft`,
`CsgResolverTests.CsgResolver_Resolve_Intersection_ShowsEntryIntoOverlap`,
`CsgResolverTests.CsgResolver_ResolveTile_RasterizedOperands_WritesVisibleSurfaceIntoWorkspace`,
`CsgResolverTests.CsgResolver_ResolveTile_CancelledToken_ThrowsOperationCanceledException`,
`CsgResolverTests.CsgResolver_Constructor_NullProgram_ThrowsArgumentNullException`

Asserts the minuend front face, the subtrahend back face as cut surface (enter shade and
back-facing metadata), no surface for empty or subtrahend-only samples, the signed counter of an
overlapping union leaf, the entry into an intersection, the tile path with real rasterized fragments
(depth, color, metadata, far filtering), cancellation per row, and argument validation.

##### CanvasNetRender3D-SceneRendering-CsgResolver-TieGroups: Coincident Faces Resolve Atomically And Deterministically

**Tests**: `CsgResolverTests.CsgResolver_Resolve_CoincidentFrontFaces_AreOneAtomicGroupAndFlushFaceIsRemoved`,
`CsgResolverTests.CsgResolver_Resolve_FragmentsBeyondTolerance_AreSeparateGroups`,
`CsgResolverTests.CsgResolver_Resolve_ChainOfCloseFragments_GroupIsAnchoredAtFirstDepth`,
`CsgResolverTests.CsgResolver_Resolve_CoincidentBackFaces_DoNotSpeckle`,
`CsgResolverTests.CsgResolver_Resolve_CoincidentFacesWithSlopeNoise_AreOneGroupAcrossInstances`,
`CsgResolverTests.CsgResolver_Resolve_SeparateCloselySpacedObliqueFaces_AreNotMerged`,
`CsgResolverTests.CsgResolver_Resolve_NonParallelOrUnknownPlane_IsNotWidened`,
`CsgResolverTests.CsgResolver_Resolve_SlopeNoise_NeverMergesSameInstanceAndIsCapped`,
`CsgResolverTests.CsgResolver_Resolve_CoincidentSolidsAtThinFold_ResolvesAsFrontPairAndBackPair`,
`CsgResolverTests.CsgResolver_Resolve_FlushEnterAndExit_LowerPriorityOfEquallyExplainingFragmentsWins`,
`CsgResolverTests.CsgResolver_Resolve_EqualPriority_LowerTriangleIndexWins`,
`CsgResolverTests.CsgResolver_Resolve_OnlyOneLeafExplainsTheChange_ItIsPreferredOverBetterPriority`,
`CsgResolverTests.CsgResolver_Resolve_NoSingleLeafExplainsTheChange_LowestPriorityOfChangedLeavesWins`

Asserts that a flush face between a cube and a box is removed (offsets of zero and plus or minus
1e-5 inside the tolerance), that fragments beyond the tolerance are separate groups, that groups are
anchored at their first depth, that coincident back faces never flip the result between them, and the
choice rules (single explaining leaf, then lowest priority, then lowest triangle index) over every
insertion order. The slope scenarios use hand-built triangles whose `DepthSlope` is set explicitly:

- **Slope widens the tolerance across instances.** Two flush front faces 0.004 apart (far beyond the
  base tolerance of 1e-4) that lie in one plane are seen as separate groups with slope zero, so the
  minuend front is visible, but with a slope of 1 they are one group, the flush faces cancel, and the
  cut surface at the subtrahend's back face is shown.
- **Widening needs plane coincidence.** Two separate faces on parallel 45 degree planes 0.0005 apart,
  with a slope of 1 (which alone would merge them) and a base tolerance of 1e-6, remain two events,
  so the minuend entry is seen, while the same faces made coincident are one group; a face whose
  plane is tilted by about 5.7 degrees, or has no plane, is never widened.
- **Slope never merges one instance and is capped.** Two fragments of one thin minuend (gap 0.004,
  steep slope) remain separate events, so the thin sheet is seen; and a huge slope (1e6) cannot
  merge fragments farther apart than the cap of 1000 base tolerances (a gap of 0.2 against a cap of
  0.1), so the minuend front stays visible.
- **Thin fold.** Two coincident solids entering at the same depth and leaving at nearly equal depths
  (a grazing fold) resolve as a front pair and a back pair, never crosswise, so nothing is visible;
  a crosswise grouping through slope noise would flip the root at the minuend's front.

##### CanvasNetRender3D-SceneRendering-CsgResolver-Determinism: Results Are Order-Independent And Match The Oracle

**Tests**: `CsgResolverTests.CsgResolver_Resolve_UnbalancedFragments_AreBoundedDeterministicAndLeaveNoState`,
`CsgResolverTests.CsgResolver_Resolve_RandomBoxTrees_MatchAnalyticOracle`,
`CsgResolverTests.CsgResolver_Resolve_MoreThan64Leaves_MatchesAnalyticOracle`

Asserts, for 600 seeded random box trees (a grid of rays each) and for scenes with 70 to 90 leaves,
that the visible depth, event kind, chosen fragment, and metadata match the analytic evaluation, that
sorted and shuffled fragment orders give identical results, that the totals prove the test is not
vacuous (visible surfaces, exits from inside material, and exact ties all occur), and that random
unbalanced fragment lists neither throw nor leave state behind.

##### CanvasNetRender3D-SceneRendering-CsgResolver-Overlays: Overlay Bodies Compile To Programs Of Their Own

**Tests**: `CsgProgramTests.CsgCompiler_Compile_OverlayNode_IsFalseInMainAndGetsOwnProgram`,
`CsgProgramTests.CsgCompiler_Compile_OverlayInsideUnion_DoesNotCollapseMainLeaf_ButSiblingsStillCollapse`,
`CsgProgramTests.CsgCompiler_Compile_OverlayInsideDifference_LeafOfInstanceFollowsPreOrder`,
`CsgProgramTests.CsgCompiler_Compile_NestedOverlay_IsGroupInsideOverlayBody`,
`CsgProgramTests.CsgCompiler_Compile_SharedNodeInMainAndOverlay_ClassifiedPerContext`,
`CsgProgramTests.CsgCompiler_Compile_EmptyOverlay_AddsNoProgram`,
`CsgProgramTests.CsgCompiler_Compile_OnlyOverlays_MainProgramIsFalse`,
`CsgProgramTests.CsgCompiler_Compile_NoOverlay_ProgramAndLeavesUnchanged`

Asserts the exact postfix text of the main and overlay programs (an overlay is `FALSE` in the main
program and a difference with it leaves the minuend), that a subtree with an overlay is not
collapsed while sibling subtrees are, that leaf numbers and the instance-to-leaf map follow the
pre-order of `Scene.EnumerateInstances` (compared with the enumeration and its overlay markers), that a
nested overlay is a plain group in a union-only body and in a boolean body, that a node shared by the
main tree and an overlay body is classified per context, that an empty overlay adds no program, that
a scene of only an overlay has the main program `FALSE`, and that a scene without overlays compiles
as before (a regression guard).

##### CanvasNetRender3D-SceneRendering-CsgResolver-Layers: Surface Layers Accumulate Front To Back

**Tests**: `CsgResolverTests.CsgResolver_Resolve_TranslucentFront_CompositesOverOpaqueBack`,
`CsgResolverTests.CsgResolver_Resolve_OpaqueFirstLayer_StopsSweep_AndEqualsLegacyResult`,
`CsgResolverTests.CsgResolver_Resolve_TranslucentExitAfterEnter_UsesOwnShadeNotBackFaceColor`,
`CsgResolverTests.CsgResolver_Resolve_RayStartsInsideTranslucentMain_UsesExitColorWithAlpha`,
`CsgResolverTests.CsgResolver_Resolve_LayersBeyondFar_AreIgnored`

Asserts, with hand-built triangles whose shades carry explicit alpha and hand-computed closed-form
composites (for example 128 + 64 of a translucent red and the remaining 63 of an opaque blue giving
`(192, 0, 63, 255)`), that the far wall of a translucent solid uses its own shade and never an
opaque exit shade, that a ray starting inside a translucent main solid shows the exit shade with its
alpha, that layers beyond the far distance are ignored, and that an opaque first layer gives exactly
the classic result over a transparent, a translucent and an opaque background.

##### CanvasNetRender3D-SceneRendering-CsgResolver-LayerOrder: Layers Are Ordered By Depth And Overlays Are Independent

**Tests**: `CsgResolverTests.CsgResolver_Resolve_TwoTranslucentLayers_AreOrderedByDepth_NotInsertion`,
`CsgResolverTests.CsgResolver_Resolve_OverlayCoincidentWithMain_OverlayIsInFront`,
`CsgResolverTests.CsgResolver_Resolve_OverlayBehindOpaqueMain_IsHidden`,
`CsgResolverTests.CsgResolver_Resolve_OverlayLeavesDoNotAffectMainProgram`,
`CsgResolverTests.CsgResolver_Resolve_ScratchIsClearedBetweenSamples_WithOverlays`,
`CsgResolverTests.CsgResolver_Constructor_NullOrEmptyProgramList_Throws`

Asserts that all 24 insertion orders of four fragments give the depth-ordered composite (and that the
reverse color order would differ), that an overlay layer coincident with an opaque main face tints it
(overlay first, depth and metadata from the overlay), that an overlay behind an opaque main surface is
hidden, that an overlay inside a translucent difference is neither subtracted nor intersected, that a
resolver reused for samples with different fragments leaves no state behind, and the argument checks of
the program-list constructor (null list, empty list, null program, a leaf claimed by two programs).

##### CanvasNetRender3D-SceneRendering-CsgResolver-LayerCompositing: Layers Composite In Exact Integer Arithmetic

**Tests**: `CsgResolverTests.LayerCompositor_SingleOpaqueLayer_ReturnsLayerColorExactly`,
`CsgResolverTests.LayerCompositor_HalfAlphaOverOpaqueBackground_MatchesClosedForm`,
`CsgResolverTests.LayerCompositor_TransparentBackground_YieldsAccumulatedAlphaAndStraightColor`,
`CsgResolverTests.LayerCompositor_ManyLayers_TransmittanceNeverGoesNegative_AndOpaqueStops`,
`CsgResolverTests.LayerCompositor_AlphaZeroLayers_ContributeNothing`

Asserts that a single opaque layer returns its color exactly over every background, that one layer of
alpha 1, 64, 127, 128, 200 or 254 over an opaque background is within one level of the closed form
`(a*c + (255-a)*b + 127) / 255`, that over a transparent background the layer color is unchanged with
the accumulated alpha, that forty translucent layers never exceed alpha 255 and that an opaque layer
afterwards ends accumulation, and that alpha-zero layers leave the background untouched.

#### Acceptance Criteria

A unit-level test run passes when every scenario above passes without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong program text, wrong leaf mapping, wrong
visible surface or shade, order-dependent result, or deviation from the analytic oracle constitutes a
failure.
