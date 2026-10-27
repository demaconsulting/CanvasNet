### ScadGeometryBuilder Unit Verification Design

<!-- cspell:ignore minuend subtrahend subtrahends minkowski multmatrix -->

This document describes the unit-level verification strategy for the `ScadGeometryBuilder` unit: the internal
iterative engine and its supporting types (`ScadGeometryBuilder`, `ScadGeo`, `ScadGeometryContext`, `ScadSceneMap`,
`ScadReporter`, `ScadSceneOptions`, `ScadFeatureIds`, `ScadBooleans`, and `ScadHulls`) in the
`DemaConsulting.CanvasNet.Scad.Geometry` namespace.

#### Verification Approach

The unit is verified by calling `ScadGeometryBuilder.Build` directly, through the helpers of
`ScadGeometryTestSupport`, and asserting on the built scene (its node structure, the meshes and their world
transforms, and the bounds), on the warnings and unsupported-feature reports recorded in the shared
`ScadEvaluationSink`, and on the exceptions thrown. Three kinds of input are used, all real implementations of the
unit's contract rather than mocks:

- **Source programs** are evaluated by the real evaluator (`ScadEvaluatorTestSupport.Run`) into a sink, and the
  resulting roots are built, so the tests exercise the real instantiation tree. `ScadVirtualFiles`, an in-memory
  file set, supplies `use` libraries so that locations in a used file can be asserted.
- **Hand-built `ScadInstance` trees** reach depths the evaluator cannot (100,000 nested groups, transforms, and
  colors; 20,000 nested booleans), to prove the engine has no recursion; the same builds are repeated on a
  caller thread with a 256 MiB stack, and the results on the 64 KiB and 256 KiB threads must be identical to it.
  Deep chains written in source (the deepest the evaluator allows with `MaxRecursionDepth` raised) cover the whole
  front end on the small stacks.
- **Direct calls** of `ScadSceneOptions.Create` and `MapFeature`, on options with every property set to a
  non-default value, including a reflection test over the public properties of `SceneRenderOptions`.

Booleans are verified structurally: each test asserts the produced scene node by node (`CsgNode` operation and
operand count, `GroupNode` children, mesh volumes) so that the position-preserving rules, the operand counting of
loops and modules, and the mixed-dimension rules are pinned by the shape of the output rather than by a rendering.
2D results are checked through the slabs they become (exact bounds and volumes at the default thickness of 0.1).
The triangle budget is verified with small budgets and exact boundary values (the estimate that fits and the one
that does not), and cancellation with an already canceled token and a token canceled around the 256-instance
polling interval.

Where the tests that exercise a behavior live in a sibling unit's test file, the scenario names them explicitly:
the scene location map and the flat-shape thickness setting are asserted by single tests in
`ScadPrimitivesSolidTests.cs` and `ScadPrimitivesShape2DTests.cs`, because they are observed through a primitive.
The render-level behavior of the unit's outputs (the mapping of renderer reports back to a module's location, the
renderer fragment budget, and the installation of the renderer handler) is built by `ScadDocument` and is verified
in the `ScadDocument` unit and the `ScadGeometry` subsystem and system tests, not here.

Hull and minkowski are verified the same way: the built meshes are checked against closed-form results (the
volume and bounds of a hull of boxes, the box that is the sum of two boxes, the rounded box between the analytic
bounds), the reports for the operands that have no hull or sum by feature and location, the budget by the
exact charge (the charge equals the triangles of the operands plus the meshes of the result, with the temporary
sums of a fold refunded, and the boundary limit accepts while one below rejects), cancellation by a canceled
token driven both through `Build` and directly through `ScadHulls` with already built results (so that the
builders' own polls are reached), determinism by comparing two builds vertex for vertex, and the stack by
hand-built chains of nested instances on a 64 KiB caller thread.

Unit tests reside in `ScadGeometryBuilderWalkTests.cs`, `ScadGeometryBuilderStackTests.cs`,
`ScadGeometryBuilderCancellationTests.cs`, `ScadGeometryBuilderLimitTests.cs`, `ScadGeometryBuilderBooleanTests.cs`,
`ScadGeometryBuilderHullTests.cs`, `ScadGeometryBuilderMinkowskiTests.cs`,
`ScadGeometryBuilderTransformDepthTests.cs`, `ScadSceneOptionsTests.cs`, and `ScadUnsupportedModuleTests.cs` within
the `DemaConsulting.CanvasNet.Scad.Tests` project, in the `Geometry` folder, with the shared helpers
(`ScadGeometryTestSupport`, `ScadBuildResult`, and `SceneLeaf`) in `ScadGeometryTestSupport.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None; the evaluator, the in-memory file set, and the Render3D types are real
- **Files**: None; `use` libraries come from the in-memory file set, and no checked-in file is read by this
  unit's tests
- **Threads**: The stack scenarios run `Build` on a caller thread with a 64 KiB or a 256 KiB stack and on a
  256 MiB thread, and join each thread before asserting
- **Isolation**: Each test evaluates its own program or builds its own instance tree and sink; no state is shared

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A recursion-bound failure or a different scene on a small stack, an unexpected report or a missing one,
a report at a wrong location, a wrong operand structure of a boolean, a budget error without a location or with a
wrong limit, an exception that is wrapped, or a scene option that is lost by the copy constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-NoRecursion: Iterative Engine

**Tests**: `ScadGeometryBuilderStackTests.ScadGeometryBuilder_Build_HandMadeDeepTree_OnSmallStack_MatchesBigStack`,
`ScadGeometryBuilderStackTests.ScadGeometryBuilder_Build_DeepModuleChainFromSource_OnSmallStack_MatchesBigStack`,
`ScadGeometryBuilderStackTests.ScadGeometryBuilder_Build_DeepShowOnlySearch_OnSmallStack_FindsTheMark`,
`ScadGeometryBuilderStackTests.ScadGeometryBuilder_Build_DeepBooleanChain_OnSmallStack_MatchesBigStack`,
`ScadGeometryBuilderStackTests.ScadGeometryBuilder_Build_DeepExtrudedBooleanChain_OnSmallStack_MatchesBigStack`,
`ScadGeometryBuilderStackTests.ScadGeometryBuilder_Build_DeepFlatTransformChainFromSource_OnSmallStack_Extrudes`,
`ScadGeometryBuilderTransformDepthTests.ScadGeometryBuilder_Build_DeepTranslateChain_OnSmallStack_ComposesExactly`,
`ScadGeometryBuilderTransformDepthTests.ScadGeometryBuilder_Build_DeepScaleChain_OnSmallStack_ComposesToTheProduct`,
`ScadGeometryBuilderTransformDepthTests.ScadGeometryBuilder_Build_DeepColorChain_OnSmallStack_Builds`,
`ScadGeometryBuilderTransformDepthTests.ScadGeometryBuilder_Build_DeepFlatTranslateChain_OnSmallStack_ComposesExactly`,
`ScadGeometryBuilderTransformDepthTests.ScadGeometryBuilder_Build_DeepTranslateChainFromSource_OnSmallStack_ComposesExactly`,
`ScadGeometryBuilderHullTests.ScadGeometryBuilder_HullAndMinkowski_DeeplyNestedInstances_BuildOnASmallStack`

Asserts that a chain of 100,000 groups (a cube and the next group per level) yields 100,001 meshes at depth
100,001 with the same description on 64 KiB, 256 KiB, and big stacks; that 5,000 levels of a user module written
in source build the same scene on small and big stacks; that a show-only mark at the bottom of a 100,000-level
tree is found on a 64 KiB stack; that 20,000-level nests of differences, of intersections, and of extruded 2D
differences give one node and one operand per level; that a 3,000-level chain of 2D translations written in
source extrudes linearly and by rotation; that 100,000 translations compose to the exact offset, 10,000
nested scales to the closed-form product, 100,000 colors build, 100,000 2D translations place the slab exactly, and
3,000 translations from source compose exactly; and that hand-built chains of 3,000 nested hulls and 800 nested
Minkowski sums around a unit cube build to the same unit solid on 64 KiB and big stacks.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-ImplicitUnion: Groups And Unions

**Tests**: `ScadGeometryBuilderWalkTests.ScadGeometryBuilder_Build_EmptyDocument_GivesEmptyScene`,
`ScadGeometryBuilderWalkTests.ScadGeometryBuilder_Build_SingleSolid_IsTheRootNode`,
`ScadGeometryBuilderWalkTests.ScadGeometryBuilder_Build_GroupsAndUnions_FlattenInDocumentOrder`,
`ScadGeometryBuilderWalkTests.ScadGeometryBuilder_Build_SingleChildChain_AddsNoWrappers`,
`ScadGeometryBuilderWalkTests.ScadGeometryBuilder_Build_Disable_IsAbsentAndSilent`,
`ScadGeometryBuilderBooleanTests.ScadBooleans_UnionAndRender_AreGroups`

Asserts that an empty document gives an empty group with empty bounds and no reports, that one solid is the root
without a wrapper, that user modules, `for`, `union`, and `render` flatten into the meshes in document order, that
a chain of single-child modules adds no wrapper nodes (depth 1), that a disabled instance is absent without a
report, and that `union` and `render` give one group while a lone child is itself.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-ShowOnly: The `!` Modifier

**Tests**: `ScadGeometryBuilderWalkTests.ScadGeometryBuilder_Build_ShowOnly_ReplacesModelWithoutAncestors`,
`ScadGeometryBuilderWalkTests.ScadGeometryBuilder_Build_ShowOnly_IsTheNewRootWithoutAncestorTransformsOrColors`,
`ScadGeometryBuilderWalkTests.ScadGeometryBuilder_Build_SeveralShowOnly_UnionInOrderAndNestedMarkIgnored`,
`ScadGeometryBuilderWalkTests.ScadGeometryBuilder_Build_ShowOnlyInsideBackground_IsIgnored`,
`ScadGeometryBuilderWalkTests.ScadGeometryBuilder_Build_BackgroundBlockAroundSingleShowOnly_BuildsOnlyTheGhost`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_ShowOnly_StillIgnoresBackgroundSubtree`,
`ScadUnsupportedModuleTests.ScadUnsupportedModule_Modifiers_BehaveAsForAnyModule`

Asserts that a marked subtree is the new root of the design: it replaces the model without the translation around it
and without reports for its ancestors, so `translate([10, 0, 0]) { !cube(1); }` is a cube at the origin and a mark
under `color("red") rotate(...) translate(...)` is neither colored nor moved, that several marks are combined in
document order and a nested mark has no effect, that a mark inside a
`%` subtree is ignored (the model stays the ordinary instances, and the ghost holds both cubes) and a subtree marked
both `!` and `%` is only a ghost, that `%{ !x }` with a single child builds no ordinary geometry and reports
nothing, and
that `!` on an unsupported module makes it the model (it is reported and the rest is dropped).

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-ModifierBackground: The `%` Ghost

**Tests**: `ScadGeometryBuilderWalkTests.ScadGeometryBuilder_Build_Background_DrawsGhostInsteadOfTheSubtreeAndNoReport`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_Background3D_BuildsOnlyAnOverlay_NoMainGeometry`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_CustomColors_AreUsedForBothOverlays`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_Background_ContributesNoBounds_ButIsInReach`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_BackgroundInsideDifference_DoesNotSubtract`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_BackgroundFirstOperandOfDifference_LeavesMainEmpty`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_BothHighlightAndBackground_BackgroundWins`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_UnderTransformAndColor_AppliesTransformAndKeepsOverlayColor`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_Background3DOfExtrudedShape_Works`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_ChildrenOfBackground_AreVisited_UnsupportedInsideIsReported`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_OutOfRangeBackground_IsRejectedByCoordinateGuard`,
`ScadUnsupportedModuleTests.ScadUnsupportedModule_Modifiers_BehaveAsForAnyModule`

Asserts that `%cube(1)` is a scene whose root is an overlay in the default ghost color `(128, 128, 128, 80)` that
holds the cube, with no ordinary mesh, 12 triangles charged (a ghost is not drawn twice), and no report or
warning; that the colors of the settings replace the defaults for both overlays; that a `%` union beside an
ordinary cube leaves the cube the only ordinary mesh; that a rotated cube beside a ghost 50 units away is
resized to exactly 4 by 4 by 4 (the ghost has no bounds) while the ghost is still in the scene; that a ghost in a
`difference` is an overlay operand beside the ordinary minuend (it does not subtract) and as the first operand
stays the first operand of the node (no ordinary geometry); that `#%` is the ghost; that a translation around a
colored ghost moves it (scene bounds 5..6) while the ordinary bounds are empty and the overlay keeps the ghost
color; that `%` on a
`linear_extrude` is a full ghost; that an unsupported module and an invalid cube inside a ghost are still
reported (at their lines); that a ghost 2e100 away, and one that only an enclosing translation pushes beyond the
limit (its reach), is rejected as `scad-geometry-rejected` at the instance and leaves no overlay; and that on an
unsupported module `#` and `%` add no modifier report to the module report.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-ModifierHighlight: The `#` Highlight

**Tests**: `ScadGeometryBuilderWalkTests.ScadGeometryBuilder_Build_Highlight_RendersNormallyWithOverlayAndNoReport`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_Highlight3D_BuildsNormalGeometryAndOverlayWithoutReport`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_Highlight_SharesMeshNodes_ChargesTheOverlayCopyAgainstTheBudget`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_HighlightCopyOverTheBudget_ThrowsLocatedEvaluationException`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_NestedHighlights_AreBoundedByTheTriangleBudget`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_HighlightInsideDifference_StillSubtractsAndHasOverlay`,
`ScadGeometryTests.ScadGeometry_Build_Modifiers_ShowOnlyReplacesTheModelAndHighlightIsOverlaid`

Asserts that `#cube(1)` is a group of the ordinary cube mesh and an overlay in the default highlight color
`(255, 64, 128, 110)` whose only child **is** that mesh node (no second mesh), with no report or warning; that
the charge is 24 triangles against 12 for the plain cube (the copy the renderer draws), so a budget of 20 fails at
the instance's line with a `ScadEvaluationException` and forty nested highlights fail the default budget instead
of building 2^40 paths; that a highlighted subtrahend stays an operand of the `difference` node and has its
overlay; and that under `!` a highlight is built with the marked subtree.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-ModifierFlatEdge: The Modifiers On A 2D Shape

**Tests**: `ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_Highlight2D_IsRenderedNormallyAndReported`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_Background2D_IsOmittedAndReported`,
`ScadGeometryBuilderWalkTests.ScadGeometryBuilder_Build_StrictMode_ThrowsForHighlightOnAFlatShape`

Asserts that `#square(1)` builds the slab without an overlay and reports `scad-modifier-highlight` once at 2:4,
that `%circle(2)` builds nothing and reports `scad-modifier-background` once at 2:4, and that strict mode throws
`scad-modifier-highlight` for `#square(1)`.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-ModifierRobustness: Modifier Guarantees

**Tests**: `ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_DeeplyNested_IsIterative`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_Cancellation_IsPolledWhileBuildingOverlays`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_InsideHullChild_IsNotPartOfTheHullAndDoesNotThrow`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_InsideMinkowskiChild_IsOrdinarySummandOrNothing`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_HullOfOnlyAGhost_BuildsNothing`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_StrictMode_DoesNotThrowFor3DModifiersOrAlpha`

Asserts that a hand-made tree 20,000 groups deep with a ghost on every group builds to the same 20,000 nested
overlays on a 64 KiB stack as on a 256 MiB stack, that a canceled token stops a model of 1,000 highlighted and 1,000
ghosted instances, that a `%` child of a `hull` does not stretch the hull (also when a colored sibling makes the
unit strip appearances from a copy, which turns the overlay into an empty group) while a `#` child is an ordinary
member of the hull and leaves no overlay, that the same holds for `minkowski` (a `#` child is an ordinary summand, a
`%` child contributes nothing), that a hull of only a ghost builds nothing without throwing, and that
strict mode does not throw for `#` and `%` on solids or a color alpha.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-UnsupportedModules: Modules That Are Not Built

**Tests**: `ScadUnsupportedModuleTests.ScadUnsupportedModule_Module_IsReportedAndOmittedWithItsChildren`,
`ScadUnsupportedModuleTests.ScadUnsupportedModule_RealisticArguments_AreReportedOnce`,
`ScadUnsupportedModuleTests.ScadUnsupportedModule_Siblings_StillBuild`,
`ScadUnsupportedModuleTests.ScadUnsupportedModule_Table_CoversExactlyTheDocumentedModules`,
`ScadUnsupportedModuleTests.ScadUnsupportedModule_UnderTransformsAndBooleans_YieldsNothing`,
`ScadUnsupportedModuleTests.ScadUnsupportedModule_UserModuleOfTheSameName_ShadowsTheBuiltin`,
`ScadGeometryBuilderWalkTests.ScadGeometryBuilder_Build_UnsupportedModule_ReportsFeatureWithLocation`,
`ScadGeometryBuilderWalkTests.ScadGeometryBuilder_Build_UserModuleShadowingBuiltin_IsBuiltNotReported`

Asserts, for each of the six modules `text`, `offset`, `projection`, `surface`, `import`, and `roof`, that the
report is `scad-module-{name}` at the module's location with a message naming it, that no
mesh is built, and that an invalid child (a cube that would warn) is never visited; that realistic arguments make
no difference and three modules in one program are reported in order at their own lines; that supported siblings
still build; that, of every module the evaluator recognizes, exactly the six are reported (`hull` and `minkowski`
are built, and without children they yield nothing and report nothing, so a module added to
the evaluator cannot be silently dropped); that an unsupported module under transforms yields nothing, as a
minuend it empties the difference, and as a subtrahend it vanishes after being reported; and that a user module of
the same name builds its own geometry without a report.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-Hull3D: Hull Of Solids

**Tests**: `ScadGeometryBuilderHullTests.ScadGeometryBuilder_Hull_TwoSeparatedCubes_IsTheConvexSolidSpanningBoth`,
`ScadGeometryBuilderHullTests.ScadGeometryBuilder_Hull_ChildrenAreTransformedBeforeTheHull`,
`ScadGeometryBuilderHullTests.ScadGeometryBuilder_Hull_MirroredAndRotatedChildren_GiveAPositiveVolumeMesh`,
`ScadGeometryBuilderHullTests.ScadGeometryBuilder_Hull_UnionOfChildrenViaGroupsAndUserModules_IsOneHull`,
`ScadGeometryBuilderHullTests.ScadGeometryBuilder_Hull_LoneColoredChild_WarnsAndDropsAppearance`,
`ScadGeometryBuilderHullTests.ScadGeometryBuilder_Hull_EnclosingColorAroundLoneColoredChild_UsesEnclosingAppearance`

Asserts that the hull of two unit cubes ten apart is one closed mesh at the scene root with volume 11, bounds
11 x 1 x 1, twelve triangles and no report; that translated, quarter-turned, and scaled children are hulled in
their transformed positions (bounds x -1..6, y 0..2, z 0..3); that mirrored and rotated children give a closed
mesh of positive volume; that the children of a user module, a loop, and a union are one point set, the hull being
byte-identical to the one of the same cubes written as direct children; and that a lone colored child warns, is
uncolored in the result, keeps its transform, and takes an enclosing color.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-Hull2D: Hull Of Shapes

**Tests**:
`ScadGeometryBuilderHullTests.ScadGeometryBuilder_Hull_TwoDimensionalChildren_ProduceAConvexRegionThatExtrudes`,
`ScadGeometryBuilderHullTests.ScadGeometryBuilder_Hull_TwoDimensionalTransformsAndColors_AreApplied`

Asserts that the extrusion of the hull of two squares is the 8 x 2 box (volume 48 at height 3), that a triangle
with a square inside it stays the triangle (volume 16 at height 2), that planar transforms of the children move
the points (bounds x -1..12, y 0..11), and that a color inside a 2D or a 3D hull is dropped with one warning
at the hull's location and leaves the result without an appearance.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-HullDimensions: Hull Of Mixed Dimensions

**Tests**: `ScadGeometryBuilderHullTests.ScadGeometryBuilder_Hull_MixedDimensions_ReportsAndOmits`

Asserts that a hull of a square and a cube is one `scad-hull-mixed-dimensions` report at the hull's location,
that nothing of it is built, and that its sibling cube is.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-HullCsgOperand: Hull Of A Boolean

**Tests**: `ScadGeometryBuilderHullTests.ScadGeometryBuilder_Hull_DifferenceOrIntersectionChild_ReportsAndOmits`

Asserts that a difference or an intersection below a 3D or a 2D hull, directly or under a union or a translate,
is one `scad-hull-csg-operand` report with nothing built, and that a union of the same operands is hulled.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-HullDegenerate: Flat And Empty Hulls

**Tests**:
`ScadGeometryBuilderHullTests.ScadGeometryBuilder_Hull_FlatCollinearOrSinglePointChildren_ReportsDegenerate`,
`ScadGeometryBuilderHullTests.ScadGeometryBuilder_Hull_NoChildrenOrEmptyChildren_YieldsNothingWithoutAReport`

Asserts that a single triangle and two coplanar triangles are one `scad-hull-degenerate` report at the hull's
location whose message describes the cause in plain words (fewer than four distinct points, or all on one line or
in one plane) without the text of the Render3D exception or doubled punctuation, and that a hull without
children, with an empty block, or with an invalid cube (which warns by itself)
builds nothing and reports nothing of its own.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-Minkowski: Minkowski Sum Of Convex Solids

**Tests**: `ScadGeometryBuilderMinkowskiTests.ScadGeometryBuilder_Minkowski_CubeAndSphere_IsTheRoundedBox`,
`ScadGeometryBuilderMinkowskiTests.ScadGeometryBuilder_Minkowski_TransformedConvexChildren_AreSummed`,
`ScadGeometryBuilderMinkowskiTests.ScadGeometryBuilder_Minkowski_UnionOfConvexChildren_Distributes`,
`ScadGeometryBuilderMinkowskiTests.ScadGeometryBuilder_Minkowski_ConvexityArgumentIsIgnored`,
`ScadGeometryBuilderMinkowskiTests.ScadGeometryBuilder_Minkowski_HullChild_IsConvex`,
`ScadGeometryBuilderMinkowskiTests.ScadGeometryBuilder_Minkowski_LoneColoredChild_WarnsAndStripsAppearance`,
`ScadGeometryBuilderMinkowskiTests.ScadGeometryBuilder_Minkowski_EnclosingColorAroundLoneColoredChild_UsesEnclosingAppearance`,
`ScadGeometryBuilderMinkowskiTests.ScadGeometryBuilder_Minkowski_StripAppearances_SharedSubtree_IsCopiedOnceAndShared`,
`ScadGeometryBuilderMinkowskiTests.ScadGeometryBuilder_Minkowski_StripAppearances_DeepSharedDag_CopiesEachNodeOnce`,
`ScadGeometryBuilderMinkowskiTests.ScadGeometryBuilder_Minkowski_StripAppearances_CopiesKeepTheOriginalSourceLocation`

Asserts that a 10 x 6 x 4 block summed with a ball of 16 fragments is a closed mesh with bounds -1..11, -1..7,
-1..5 and a volume between the block with its faces added and the exact rounded block; that a translated box and a
quarter-turned box sum to the 4 x 3 x 5 box (volume 60); that a union on either side or both gives two or four
boxes by distribution; that the `convexity` argument, named or positional, changes nothing; that the output
of a hull is an accepted operand and a lone convex child keeps its geometry and charge; that a colored lone
child warns and is uncolored (its colors are stripped, so that an enclosing color applies) while preserving its
geometry and transform, with an enclosing color applying afterward; that the appearance-stripping copy of a scene
in which a node is shared (a root holding `[X, S]` where `X` holds `P` and `P` holds `S`, in three child orders,
the earlier implementation threw `KeyNotFoundException` for it) makes one copy of the shared node, with the same
transform and mesh, referenced by both parents, with no appearance anywhere and the original untouched; and that a
chain of a hundred thousand nodes that each hold the next one twice (2^100000 paths) is copied with one copy per
node, iteratively; and that every stripped mesh copy is recorded in the location map under the location of its original
node (the cube instance, not the `minkowski`), so a render-time report about the copy is locatable.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-MinkowskiNonConvex: Non-Convex Operands

**Tests**: `ScadGeometryBuilderMinkowskiTests.ScadGeometryBuilder_Minkowski_NonConvexChild_ReportsAndOmits`,
`ScadGeometryBuilderMinkowskiTests.ScadGeometryBuilder_Minkowski_NonConvexChild_MessageNamesTheChildInUserTerms`,
`ScadGeometryBuilderMinkowskiTests.ScadGeometryBuilder_Minkowski_ChildWithoutVolume_IsRejectedNamingTheChild`

Asserts that an L-shaped prism, a hollowed block, a ring, a difference, and an intersection, as one of several
children or alone, are each one `scad-minkowski-nonconvex` report at the module's location with a message that
names convexity, that nothing is built, and that no warning is added; that the exact messages name the offending
child by its position and source location (`child 2 (at 3:3) is not a closed convex solid`, or contains a
difference or an intersection) and contain no Render3D parameter names (`'a'`, `'b'`), no internal wording and no
doubled punctuation; and that a closed double-sided flat triangle is one `scad-geometry-rejected` report that says
the child has no volume.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-MinkowskiDimensions: 2D And Mixed Operands

**Tests**:
`ScadGeometryBuilderMinkowskiTests.ScadGeometryBuilder_Minkowski_TwoDimensionalChildren_ReportsDeferred`,
`ScadGeometryBuilderMinkowskiTests.ScadGeometryBuilder_Minkowski_MixedDimensions_ReportsAndOmits`

Asserts that a sum of two squares, or of one, is one `scad-minkowski-2d` report and that a sum of a cube and a
square is one `scad-minkowski-mixed-dimensions` report, each at the module's location, with the sibling cube
still built.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-MinkowskiLimits: Work Bounds

**Tests**:
`ScadGeometryBuilderMinkowskiTests.ScadGeometryBuilder_Minkowski_TooManyPairsOrCombinations_ReportsTooComplex`,
`ScadGeometryBuilderMinkowskiTests.ScadGeometryBuilder_Minkowski_SumBeyondTheCoordinateRange_IsRejected`,
`ScadGeometryBuilderMinkowskiTests.ScadGeometryBuilder_Minkowski_CumulativeWorkAboveTheTotalCap_ReportsTooComplex`

Asserts that a child of 4,097 parts, two children of 65 parts (4,225 combinations), and two spheres of 4,902
vertices each are one `scad-minkowski-too-complex` report with nothing built, that a sum that leaves the
1e100 range of two parts that are each inside it is `scad-geometry-rejected`, and that two cylinders of 1,026 and
2,042 vertices (a product of 2,095,092, inside the bound of one pair) are reported with the exact cumulative
message (the verification of the members counts toward the 2,097,152 total) before the sum is computed, while three
small cylinders stay under the cap and build one closed mesh.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-HullMinkowskiBudget: Bounds Charged Before Computing, Exact Totals

**Tests**:
`ScadGeometryBuilderHullTests.ScadGeometryBuilder_HullAndMinkowski_ChargeExactlyTheTrianglesCreated`,
`ScadGeometryBuilderHullTests.ScadGeometryBuilder_HullAndMinkowski_OverTheBudget_ThrowsLocatedEvaluationExceptionWithoutLeakingCharge`,
`ScadGeometryBuilderHullTests.ScadGeometryBuilder_HullAndMinkowski_BoundChargedBeforeComputing_RejectsWithoutStartingAndSettlesToActual`,
`ScadGeometryBuilderHullTests.ScadGeometryBuilder_Minkowski_TransformedCopies_AreChargedWhileTheyLiveAndRefunded`,
`ScadGeometryBuilderHullTests.ScadGeometryBuilder_Minkowski_DistinctVertexCounts_AreScannedOncePerMemberAndReused`,
`ScadGeometryBuilderMinkowskiTests.ScadGeometryBuilder_Minkowski_MirroredAndScaledChildren_AreVerifiedAsBuiltAndSummedInFrame`,
`ScadGeometryBuilderHullTests.ScadGeometryBuilder_Hull_DuplicatedVertexSlots_AreBudgetedByTheirDistinctPoints`

Asserts, for hulls of boxes and spheres, an extruded 2D hull, sums of one, two, and three children, a union
operand, and the reported cases (a boolean, a flat hull, and a fold whose second step is too complex after its
first step produced sums), that the charge equals the triangles of the operands built alone plus the triangles of
the meshes in the scene, that a colored lone sum adds no charge while stripping its appearance, so the temporary
sums are refunded and nothing leaks on a report; and that the boundary is the upper bound that is charged before the
computation: for two unit cubes (24 triangles) a hull needs 24 + 28 and a sum 24 + 124, so limits of 52 and 148 build
(settling to a box of 12 triangles, 36 charged) and one below throws a `ScadEvaluationException` at the module's
location with the limit in its message. The direct test on a context asserts that a rejected hull or sum is never
begun (the context's `HullsStarted` and `SumsStarted` counters stay 0) and leaves exactly the operands charged, that a
bound that fits is settled to the actual twelve triangles, that a degenerate flat hull refunds its pre-charge, and, for
a fold of three cubes, that the second step needs the operands (36) plus the live first sum (12) plus its own bound
(124): 172 builds (36 + 12 charged) and 171 fails in the second step with the first sum started and only the operands
(36) left charged. A hull gathers its points into one exactly sized array that the hull sorts in place (covered by the hull
tests above, whose meshes and charges are unchanged). No guard is made on the raw number of vertex slots: ten
children that are the same unwelded cube (360 slots, 8 distinct points) are hulled at a limit of 132 (the ten cubes
cost 120 triangles and the bound of the 8 distinct points is 12), and a limit of 131 is rejected by that bound before
the hull is computed. The copy test asserts that the transformed copy of a translated
child (12 triangles) is charged
before it is allocated and refunded afterwards: for a fold of two children the operands (24) plus the copy (12) plus
the bound of the sum (124) need a limit of 160 (159 fails and 35 fails when the copy does not fit, each leaving only the
operands charged), the final charge is the operands plus the result, and that a lone child (translated, rotated, scaled
non-uniformly or mirrored) is verified as built, so a limit equal to its own 12 triangles passes with no copy and it
stays charged once, while a non-convex lone child is reported and leaves nothing charged. The count test asserts that
two children of three parts give nine sums
with six distinct-vertex scans (not one per pair), and that an unwelded cube (36 vertices, 8 distinct) costs like a
welded one: its sum is accepted at 24 + 124 and settles to 12 triangles.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-HullMinkowskiCancellation: Cancellation

**Tests**:
`ScadGeometryBuilderHullTests.ScadGeometryBuilder_HullAndMinkowski_Canceled_ThrowsOperationCanceledException`

Asserts that a canceled token stops a hull and a sum through `Build`, and that `ScadHulls.Hull` and
`ScadHulls.Minkowski` called directly with already built operands throw `OperationCanceledException` unwrapped.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-HullMinkowskiDeterminism: Repeatable Meshes

**Tests**:
`ScadGeometryBuilderHullTests.ScadGeometryBuilder_HullAndMinkowski_SameModelTwice_BuildsIdenticalMeshes`

Asserts that a model with a hull of a sphere and a rotated cube, a distributed sum with a union, and an extruded
2D hull builds twice to the same four meshes vertex for vertex and index for index with the same charge.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-Reporting: One Reporting Channel

**Tests**: `ScadGeometryBuilderWalkTests.ScadGeometryBuilder_Build_SharedSink_KeepsEvaluationBeforeGeometryEvents`,
`ScadGeometryBuilderWalkTests.ScadGeometryBuilder_Build_RepeatedUnsupportedInstance_ReportedOnce`,
`ScadGeometryBuilderWalkTests.ScadGeometryBuilder_Build_UnsupportedInUsedFile_CarriesFileLocation`,
`ScadUnsupportedModuleTests.ScadUnsupportedModule_InUsedFile_CarriesTheFile`

Asserts that an echo of the evaluation precedes the geometry's unsupported report and warning in the shared sink's
delivery order, that a module instantiated three times by a loop is reported once, and that a report for a module
inside a used library carries the file name `lib.scad` with the line and column of the module.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-StrictMode: Strict Mode

**Tests**: `ScadGeometryBuilderWalkTests.ScadGeometryBuilder_Build_StrictMode_ThrowsAtFirstUnsupported`,
`ScadGeometryBuilderWalkTests.ScadGeometryBuilder_Build_StrictMode_ThrowsForHighlightOnAFlatShape`,
`ScadUnsupportedModuleTests.ScadUnsupportedModule_StrictMode_ThrowsAtTheFirst`,
`ScadUnsupportedModuleTests.ScadUnsupportedModule_PublicHandlerAndStrictMode_SeeEveryModule`

Asserts that strict mode throws at the first unsupported module with its feature and location while the sink keeps
exactly that one report, that `#` on a 2D shape throws `scad-modifier-highlight`, and that through the public options the
handler sees `text`, `surface`, `import`, and `projection` at lines 1 to 4 in order and strict mode throws the
first.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-Difference: Position-Preserving Difference

**Tests**: `ScadGeometryBuilderBooleanTests.ScadBooleans_Difference_TwoSolids_IsADifferenceNode`,
`ScadGeometryBuilderBooleanTests.ScadBooleans_Difference_FirstChildIsTheMinuend`,
`ScadGeometryBuilderBooleanTests.ScadBooleans_Difference_SeveralSubtrahends_AreSeparateOperands`,
`ScadGeometryBuilderBooleanTests.ScadBooleans_Difference_Nested_NestsNodes`,
`ScadGeometryBuilderBooleanTests.ScadBooleans_Difference_EmptyFirstOperand_IsEmpty`,
`ScadGeometryBuilderBooleanTests.ScadBooleans_Difference_EmptyLaterOperands_Vanish`,
`ScadGeometryBuilderBooleanTests.ScadBooleans_Difference_SingleOrNoChild_IsTheChildOrNothing`

Asserts that a difference of a cube and a cylinder is a `Difference` node with the cube mesh first (volume 1000)
and no reports, that the first child is the minuend whatever the volumes, that three subtrahends give four
operands, that nested differences nest, that an empty first operand (an invalid cube, an empty translate, an empty
color) empties the result, that empty later operands vanish and a difference left with one operand is a bare mesh,
and that a single child is itself while no child is nothing without a warning.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-Intersection: Intersection

**Tests**:
`ScadGeometryBuilderBooleanTests.ScadBooleans_Intersection_IsAnIntersectionNodeAndAnyEmptyOperandEmptiesIt`,
`ScadGeometryBuilderBooleanTests.ScadBooleans_IntersectionFor_IsAnIntersectionOfTheIterations`

Asserts that an intersection of two solids is an `Intersection` node, that an empty operand anywhere empties the
result, that a lone child is itself, and that `intersection_for` over three iterations is an intersection node of
three operands with the closed-form bounds `[0,0,0]` to `[7,5,5]`.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-BooleanOperands: Operand Counting

**Tests**: `ScadGeometryBuilderBooleanTests.ScadBooleans_Difference_LoopChild_IsOneUnionOperand`,
`ScadGeometryBuilderBooleanTests.ScadBooleans_Difference_ModuleAndUnionChildren_AreOneOperandEach`,
`ScadGeometryBuilderBooleanTests.ScadBooleans_TransformsAndColors_StayWhereTheyWereWritten`

Asserts that a three-iteration loop under a difference is one group operand of three children, that a user module
call and an explicit `union` are one operand each, and that a translate and color around a difference and a
translate and color on an operand stay on the right leaves (exact world bounds and colors).

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-MixedDimensions: 2D And 3D Together

**Tests**: `ScadGeometryBuilderBooleanTests.ScadBooleans_MixedDimensions_KeepTheDimensionOfTheFirstOperand`,
`ScadGeometryBuilderBooleanTests.ScadBooleans_UnionOfMixedDimensions_KeepsTheSolids`

Asserts that a difference with a solid first operand drops the square and warns at the difference's location with
a message that contains `mixes 2D and 3D`, that an intersection with a 2D first operand keeps the square (as a
slab of thickness 0.1) and warns, and that a union of a cube and a square inside a difference keeps the cube and
produces exactly one warning.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-FlatBooleans: 2D Boolean Trees

**Tests**: `ScadGeometryBuilderBooleanTests.ScadBooleans_Flat_Difference_BecomesADifferenceOfSlabs`,
`ScadGeometryBuilderBooleanTests.ScadBooleans_Flat_IntersectionAndNesting_FollowTheTree`,
`ScadGeometryBuilderBooleanTests.ScadBooleans_Flat_TransformsApplyToTheirOperandOnly`,
`ScadGeometryBuilderBooleanTests.ScadBooleans_Flat_HoleAndMirroredOperands_AreValid`,
`ScadGeometryBuilderBooleanTests.ScadBooleans_Flat_ColorAroundDifference_ColorsAllOperands`,
`ScadGeometryBuilderBooleanTests.ScadBooleans_Flat_EmptyOperands_FollowTheSameRules`

Asserts that a 2D difference becomes a difference of two slabs with the exact footprints and volumes, that a 2D
intersection and a nested union and intersection under a difference follow the tree, that a 45-degree rotation and
translation apply to their operand only (bounds from the closed form with the square root of two), that a holed
polygon and a mirrored operand are valid (volume `(100 - 36) * 0.1` and the mirrored bounds), that a color around
the difference colors both slabs, and that an empty first operand empties the result while an empty later one
vanishes.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-ResizeBounds: Bounds Of Booleans

**Tests**:
`ScadGeometryBuilderBooleanTests.ScadBooleans_Resize_OverBoolean_ReportsApproximateBoundsAndStillResizes`,
`ScadGeometryBuilderBooleanTests.ScadBooleans_Resize_OverDifference_UsesTheFirstOperandExtent`,
`ScadGeometryBuilderBooleanTests.ScadBooleans_Resize_OverIntersection_UsesTheOverlap`,
`ScadGeometryBuilderBooleanTests.ScadBooleans_Resize_OverUnionOfPlainSolids_IsNotReported`,
`ScadGeometryBuilderBooleanTests.ScadBooleans_Resize_OverSingleOperandBoolean_IsNotReported`

Asserts that `resize` over a difference or an intersection of solids, and over a 2D difference, reports
`scad-resize-approximate` at the resize and still produces geometry; that a difference sizes from its first
operand (factor 5 for every operand), an intersection from the overlap of the boxes (factor 4), that a union of
plain solids reports nothing, and that a boolean left with one operand stays exact (no report, exact bounds).

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-TriangleBudget: Triangle Budget

**Tests**: `ScadGeometryBuilderLimitTests.ScadGeometryBuilder_Build_HostileFragmentCount_ThrowsLocatedLimitError`,
`ScadGeometryBuilderLimitTests.ScadGeometryBuilder_Build_HugeSphereUnderDefaultBudget_ThrowsLocatedLimitError`,
`ScadGeometryBuilderLimitTests.ScadGeometryBuilder_Build_BudgetAccumulatesAcrossLoopIterations`,
`ScadGeometryBuilderLimitTests.ScadGeometryBuilder_Build_CylinderEstimate_IsFourTrianglesPerFragment`,
`ScadGeometryBuilderLimitTests.ScadGeometryBuilder_Build_PublicMaxTriangles_FailsBuildScene`,
`ScadGeometryBuilderTriangleChargeTests.ScadGeometryBuilder_Build_ChargesExactlyTheTrianglesCreated`,
`ScadGeometryBuilderTriangleChargeTests.ScadGeometryBuilder_Build_LimitAtTheExactCount_AcceptsAndOneBelowRejects`,
`ScadGeometryBuilderTriangleChargeTests.ScadGeometryBuilder_Build_LinearExtrudeOfOuterWithTwoHoles_ChargesFiftyTwo`,
`ScadGeometryBuilderTriangleChargeTests.ScadGeometryBuilder_Build_RotateExtrudeAndSlabWithHoles_ChargeTheExactCaps`,
`ScadGeometryBuilderTriangleChargeTests.ScadGeometryBuilder_Build_ExtrusionOverBudget_FailsWithTheLocation`,
`ScadGeometryBuilderTriangleChargeTests.ScadGeometryBuilder_Build_HugeSlicesWithTwoLeaves_IsRefusedWhateverTheBudget`,
`ScadGeometryBuilderTriangleChargeTests.ScadGeometryBuilder_Build_ManySaturatedLeaves_AreRefusedNotWrapped`,
`ScadGeometryBuilderTriangleChargeTests.ScadGeometryBuilder_Build_RejectedExtrusion_RefundsItsWholeCharge`

Also asserts that `linear_extrude(height = 1, slices = 1e308)` over two profile leaves is refused with the triangle
limit error at the extrusion for a budget of 5,000,000 and for `long.MaxValue` (a saturated estimate is never
charged, so it cannot be refunded leaf by leaf into a negative counter), that twelve saturated leaves are refused
instead of overflowing their sum, and that a `rotate_extrude` the extruder rejects refunds both of its leaves, so
that two cubes of exactly the remaining 24 triangles still fit and the charge equals the triangles of the scene.
Asserts that a hostile `$fn` on a sphere, cylinder, circle, and a polygon plus circle fails with
`Triangle limit exceeded (limit 1000)` at the right line before allocating, that the default budget alone stops a
sphere with a million fragments (`2:1: Triangle limit exceeded (limit 5000000)`), that eight cubes of twelve
triangles fit a budget of 100 and nine fail at 1:17, that a cylinder of 100 fragments fits 400 and fails at 399,
and that the public `ScadLimits.MaxTriangles` fails `BuildScene` with the located error. It also asserts, over a
matrix of 43 programs (cubes, spheres, cylinders, cones, polyhedra, flat slabs, linear extrusions with holes, nested
islands, twist, slices, scale and apex, differences, and rotate extrusions full, partial, with holes, poles, negative
profiles, and negative angles), that the number of triangles charged equals the triangles of the meshes created; that
for eight representative programs the limit equal to that number builds and one below fails; that a linear extrusion
of one outer boundary and two holes (12 vertices) is charged 52 (24 wall triangles and two caps of 14), so a limit
of 51, or 48 (four per vertex), fails and 52 builds; that a partial `rotate_extrude` of a profile with a hole is
charged 80 and the slab of a flat shape with two holes 52, each failing one below; and that an extrusion over budget
fails located at the extrusion.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-CoordinateRange: Coordinate Range

**Tests**:
`ScadDocumentCoordinateRangeTests.ScadGeometryBuilder_ExtremeCoordinates_AreRejectedAtTheInstance`,
`ScadDocumentCoordinateRangeTests.ScadGeometryBuilder_ExtremeCoordinates_OmitOnlyTheOffendingInstance`,
`ScadDocumentCoordinateRangeTests.ScadGeometryBuilder_LargeButSafeCoordinates_AreKept`,
`ScadDocumentCoordinateRangeTests.ScadDocument_ExtremeTranslate_BuildSceneAndRenderAreConsistent`,
`ScadDocumentCoordinateRangeTests.ScadDocument_Render_ExtremeTransformNextToVisibleSolid_RendersTheVisibleSolid`,
`ScadDocumentCoordinateRangeTests.ScadDocument_Render_CoordinatesAtTheLimit_DoesNotThrow`,
`ScadDocumentCoordinateRangeTests.ScadDocument_FitCamera_UnusableBounds_FallsBackToTheUnitCube`,
`ScadDocumentCoordinateRangeTests.ScadDocument_FitCamera_UsableBounds_FitsTheScene`

Asserts that a finite but extreme translation (`1.7e308`, `2e100`), `multmatrix` entry, extrusion height,
primitive size, 2D shape, or a scale that pushes a hidden difference operand out of range is reported once as
`scad-geometry-rejected` at the instance and omitted, leaving the siblings, that an overflowing scale is
a located warning, that coordinates up to `9e99` are kept and render without throwing in both projections, that
`BuildScene` and `Render` give the same report outside strict mode and the same `ScadUnsupportedFeatureException`
in strict mode, that the rejected instances do not change the image of the visible solid, and that the fitted
camera falls back to the unit-cube framing for bounds beyond the limit while it fits usable bounds exactly.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-FlatSlabs: Top-Level 2D Shapes

**Tests**: `ScadGeometryBuilderBooleanTests.ScadBooleans_Flat_Difference_BecomesADifferenceOfSlabs`,
`ScadGeometryBuilderLimitTests.ScadGeometryBuilder_Build_FlatShape_IsChargedForItsSlab`,
`ScadPrimitivesShape2DTests.ScadGeometryBuilder_Build_FlatShapeThickness_SetsSlabHeight`

Asserts that the slabs of a 2D difference have the exact footprints (bounds from 0 to 0.1 in Z at the default
thickness) and volumes, that a circle of 1,000 vertices is charged its exact slab (3,996 triangles: 2,000 wall
and two caps of 998), so it fits a budget of 3,996 and fails at 3,995, that two shapes of 4 and 10 vertices fail at 19,
and that a thickness setting of 2.5 gives a slab
of height 2.5 and volume `4 * 2.5` (this last scenario resides in the `ScadPrimitives` unit's test file).

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-Cancellation: Cancellation

**Tests**: `ScadGeometryBuilderCancellationTests.ScadGeometryBuilder_Build_AlreadyCanceled_ThrowsBeforeTessellating`,
`ScadGeometryBuilderCancellationTests.ScadGeometryBuilder_Build_AlreadyCanceled_StopsWalkOverManyUnsupportedInstances`,
`ScadGeometryBuilderCancellationTests.ScadGeometryBuilder_Build_NothingToDo_IgnoresCanceledToken`,
`ScadGeometryBuilderCancellationTests.ScadGeometryBuilder_Build_Canceled_KeepsEarlierEventsInSink`,
`ScadGeometryBuilderCancellationTests.ScadGeometryBuilder_Build_AlreadyCanceled_StopsTheShowOnlyPrePass`,
`ScadGeometryBuilderCancellationTests.ScadGeometryBuilder_Build_NotCanceled_ShowOnlyPrePassCompletes`

Asserts that a canceled token throws `OperationCanceledException` before the first tessellation, that a walk over
1,000 instances is stopped at the polling schedule, that an empty model completes even with a canceled token and an
uncanceled token never interferes, that the warning reported by the first instance is still in the sink after
the build is canceled (the show-only pre-pass of about 140 instances and the build share one 256-instance schedule), and
that a canceled token stops the `!` pre-pass over 1,000 unmarked instances before the marked (unsupported) module is
built or reported, while an uncanceled token lets the same model complete.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-SceneMap: Location Map

**Tests**: `ScadPrimitivesSolidTests.ScadPrimitives_Cube_TwoInstances_MapRecordsEachLocation`

Asserts that the map records 1:1 and 2:3 for two cubes and returns an unknown location for a `null` node (this
scenario resides in the `ScadPrimitives` unit's test file, because the map is filled through a primitive).

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-SceneOptionsCopy: Renderer Options Copy

**Tests**: `ScadSceneOptionsTests.ScadSceneOptions_Create_EveryPublicProperty_IsCopiedExceptReplacedOnes`,
`ScadSceneOptionsTests.ScadSceneOptions_Create_ReplacesHandlerAndForcesStrictOff`,
`ScadSceneOptionsTests.ScadSceneOptions_Create_NullSource_UsesRenderDefaults`

Asserts, by reflection over the public properties of `SceneRenderOptions`, that the test knows every property and
that each one except the handler and strict mode is copied with a non-default value; that the supplied handler is
installed (or none) and strict mode is off in the copy while the source keeps its own; and that a `null` source
gives a new instance with the Render3D defaults.

##### CanvasNetScad-ScadGeometry-ScadGeometryBuilder-SceneOptionsMapping: Identifier Mapping

**Tests**: `ScadSceneOptionsTests.ScadSceneOptions_MapFeature_RewritesPrefix`

Asserts that `render3d-csg-open-mesh`, `render3d-csg-non-manifold-edge`, and `render3d-csg-singular-transform`
become the `scad-csg-` identifiers and that an identifier without the prefix gets `scad-` prepended. The remaining
`render3d-csg-` identifiers use the same rule and are exercised end to end in the `ScadDocument` render-report
tests.
