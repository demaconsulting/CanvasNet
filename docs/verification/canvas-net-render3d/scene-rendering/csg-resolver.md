### CsgResolver Unit Verification Design

<!-- cspell:ignore subtrahend subtrahends minuend postfix speckle rasterizer -->

This document describes the unit-level verification strategy for the internal `CsgCompiler`,
`CsgCompilation`, `CsgProgram`, `CsgInstruction`, `CsgOpCode`, and `CsgResolver` types.

#### Verification Approach

All types are internal, so the tests reach them through the test assembly's `InternalsVisibleTo`
access. Three techniques are combined:

- **Compiler and program** are verified on hand-built scene trees (`MeshNode`, `GroupNode`,
  `CsgNode`) by comparing the disassembled postfix program, the instance-to-leaf map (cross-checked
  against `Scene.EnumerateInstances`), and truth tables of the evaluator, including 130 leaves and
  10 000-deep trees.
- **Resolver scenarios** use hand-built `ScreenTriangle`s that carry only a leaf, facing, priority,
  and identifiable enter/exit shades, and hand-built fragment lists, so each rule (initial state,
  tie groups, choice within a group, far filtering) is isolated with an exact expected result.
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
produce no program and map every instance to one leaf.

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
`CsgResolverTests.CsgResolver_Resolve_FlushEnterAndExit_LowerPriorityOfEquallyExplainingFragmentsWins`,
`CsgResolverTests.CsgResolver_Resolve_EqualPriority_LowerTriangleIndexWins`,
`CsgResolverTests.CsgResolver_Resolve_OnlyOneLeafExplainsTheChange_ItIsPreferredOverBetterPriority`,
`CsgResolverTests.CsgResolver_Resolve_NoSingleLeafExplainsTheChange_LowestPriorityOfChangedLeavesWins`

Asserts that a flush face between a cube and a box is removed (offsets of zero and plus or minus
1e-5 inside the tolerance), that fragments beyond the tolerance are separate groups, that groups are
anchored at their first depth, that coincident back faces never flip the result between them, and the
choice rules (single explaining leaf, then lowest priority, then lowest triangle index) over every
insertion order.

##### CanvasNetRender3D-SceneRendering-CsgResolver-Determinism: Results Are Order-Independent And Match The Oracle

**Tests**: `CsgResolverTests.CsgResolver_Resolve_UnbalancedFragments_AreBoundedDeterministicAndLeaveNoState`,
`CsgResolverTests.CsgResolver_Resolve_RandomBoxTrees_MatchAnalyticOracle`,
`CsgResolverTests.CsgResolver_Resolve_MoreThan64Leaves_MatchesAnalyticOracle`

Asserts, for 600 seeded random box trees (a grid of rays each) and for scenes with 70 to 90 leaves,
that the visible depth, event kind, chosen fragment, and metadata match the analytic evaluation, that
sorted and shuffled fragment orders give identical results, that the totals prove the test is not
vacuous (visible surfaces, exits from inside material, and exact ties all occur), and that random
unbalanced fragment lists neither throw nor leave state behind.

#### Acceptance Criteria

A unit-level test run passes when every scenario above passes without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong program text, wrong leaf mapping, wrong
visible surface or shade, order-dependent result, or deviation from the analytic oracle constitutes a
failure.
