### SceneGraph Unit Verification Design

This document describes the unit-level verification strategy for the `SceneNode`, `MeshNode`,
`GroupNode`, `CsgNode`, `OverlayNode`, `CsgOperation`, `NodeAppearance`, and `Scene` types and the
internal `SceneInstance`.

#### Verification Approach

The unit is verified through direct calls to the public API and, for the internal
`Scene.EnumerateInstances`, through the test assembly's `InternalsVisibleTo` access. Scenes are
small hand-built trees with known transforms, so composed world matrices, pre-order indices,
effective appearances, and bounds can be asserted exactly. The unit depends only on `Mesh`,
`DoublePrecisionMath`, and `Rgba32`, so no mocking or stubbing is required.

Unit tests reside in `SceneGraphTests.cs` within the `DemaConsulting.CanvasNet.Render3D.Tests`
project, in the `SceneModel` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test builds its own scene

#### Unit-Level Test Scenarios

##### CanvasNetRender3D-SceneModel-SceneGraph-NodeConstruction: Nodes And Scene Validate Their Arguments

**Tests**: `SceneGraphTests.MeshNode_Constructor_NullMesh_Throws`,
`SceneGraphTests.GroupNode_Constructor_NullChildren_Throws`,
`SceneGraphTests.GroupNode_Constructor_NullChildElement_Throws`,
`SceneGraphTests.GroupNode_Constructor_CopiesChildren`,
`SceneGraphTests.GroupNode_Constructor_Empty_Allowed`,
`SceneGraphTests.Scene_Constructor_NullRoot_Throws`

Asserts the exceptions for a null mesh, a null child list, a null child, and a null root; that a
group copies its child list; and that an empty group is allowed.

##### CanvasNetRender3D-SceneModel-SceneGraph-TransformValidation: Node Transforms Are Validated

**Tests**: `SceneGraphTests.SceneNode_Transform_NonFiniteMatrix_ThrowsArgumentException`,
`SceneGraphTests.SceneNode_Transform_DefaultIsIdentity`

Asserts that a node transform with a non-finite element is rejected when initialized and that the
default transform is the identity.

##### CanvasNetRender3D-SceneModel-SceneGraph-HierarchyComposition: Transforms Compose Child Then Parent

**Tests**: `SceneGraphTests.Scene_EnumerateInstances_NestedTransforms_ComposeChildThenParent`,
`SceneGraphTests.Scene_EnumerateInstances_PreOrderIndexIsStable`,
`SceneGraphTests.Scene_EnumerateInstances_SharedChild_EnumeratedPerPath`

Asserts the composed world matrix of a nested instance, that pre-order indices are assigned in
depth-first order and are stable across enumerations, and that a node shared by two parents is
enumerated once per path.

##### CanvasNetRender3D-SceneModel-SceneGraph-AppearanceInheritance: Innermost Appearance Wins

**Tests**: `SceneGraphTests.Scene_EnumerateInstances_InnermostAppearanceWins`,
`SceneGraphTests.Scene_EnumerateInstances_NoAppearance_ReportsNull`,
`SceneGraphTests.OverlayNode_EnumerateInstances_ForcesOverlayColorOverInnerAppearances_AndMarksInOverlay`

Asserts that a mesh node's own appearance overrides its ancestors', that an ancestor's appearance is
inherited when the node has none, that an instance with no appearance anywhere reports `null`, and
that beneath an overlay the overlay's color replaces every appearance.

##### CanvasNetRender3D-SceneModel-SceneGraph-SceneBounds: Scene Bounds Are Those Of The Result

**Tests**: `SceneGraphTests.Scene_ComputeBounds_TransformedGroup_MatchesAnalytic`,
`SceneGraphTests.Scene_ComputeBounds_EmptyGroup_IsEmpty`,
`SceneGraphTests.Scene_ComputeBounds_CalledTwice_ReturnsSame`,
`SceneGraphTests.Scene_ComputeBounds_Difference_IsTheMinuendBounds`,
`SceneGraphTests.Scene_ComputeBounds_Intersection_IsTheOverlapOfTheOperands`,
`SceneGraphTests.Scene_ComputeBounds_DisjointIntersection_IsEmpty`,
`SceneGraphTests.Scene_ComputeBounds_UnionCsg_IncludesEveryOperand`,
`SceneGraphTests.Scene_ComputeBounds_NestedCsgUnderTransforms_MatchesAnalytic`

Asserts the analytic bounds of a transformed group, that a scene of an empty group has empty bounds,
and that repeated calls return the same value. For booleans it asserts that a 10-unit cube minus a
100-unit cutter is bounded by the cube alone (with and without overlays); that an intersection of
three boxes is bounded by the largest minimum and the smallest maximum on every axis; that an
intersection of disjoint cubes is exactly `BoundingBox3D.Empty`; that a CSG union is bounded by all of
its operands; and that a union of a difference and an intersection, under transforms on the operand,
the intersection, and the enclosing group, gives the analytic world box.

##### CanvasNetRender3D-SceneModel-SceneGraph-DeepNesting: Deep Nesting Does Not Overflow The Stack

**Tests**: `SceneGraphTests.Scene_EnumerateInstances_TenThousandNestedGroups_DoesNotOverflowStack`,
`SceneGraphTests.Scene_ComputeBounds_TenThousandNestedCsgNodes_DoesNotOverflowStack`

Builds a ten-thousand-level chain of nested groups, each translating by one unit along X, and
asserts that enumeration returns the single leaf instance with an accumulated translation of
10 000 and that the scene bounds agree. Builds a ten-thousand-level chain of differences, each
subtracting a far cutter from the level below and translating by one unit along X, and asserts
that the bounds are the innermost cube moved by 10 000 (no cutter counts).

##### CanvasNetRender3D-SceneModel-SceneGraph-CsgNode: CsgNode Validates Its Arguments

**Tests**: `SceneGraphTests.CsgNode_Constructor_NullChildren_Throws`,
`SceneGraphTests.CsgNode_Constructor_NullChildElement_Throws`,
`SceneGraphTests.CsgNode_Constructor_UndefinedOperation_Throws`,
`SceneGraphTests.CsgNode_Constructor_KeepsOperationAndCopiesChildren`,
`SceneGraphTests.CsgNode_Constructor_Empty_AllowedAndEmptyForEveryOperation`,
`SceneGraphTests.CsgNode_Transform_NonFiniteMatrix_ThrowsArgumentException`

Asserts the exceptions for a null child list, a null child, and an undefined operation (values
outside `Union`, `Difference`, `Intersection`); that each defined operation and the children are
kept in order and that the child list is copied; that a childless node is allowed for every
operation and enumerates no instances and has empty bounds; and that a non-finite transform is
rejected with the identity as default.

##### CanvasNetRender3D-SceneModel-SceneGraph-CsgEnumeration: Operands Are Enumerated Like Group Children

**Tests**: `SceneGraphTests.Scene_EnumerateInstances_CsgNode_DescendsIntoOperandsInPreOrder`,
`SceneGraphTests.Scene_EnumerateInstances_CsgNode_SharedChild_EnumeratedPerPath`,
`SceneGraphTests.Scene_EnumerateInstances_CsgNode_ComposesTransformAndInheritsAppearance`,
`SceneGraphTests.CsgNode_SingleChild_EnumeratesThatChild`,
`SceneGraphTests.Scene_EnumerateInstances_TenThousandNestedCsgNodes_DoesNotOverflowStack`

Asserts, for a mixed tree of groups and difference/intersection nodes, that every operand appears
as an instance in depth-first pre-order with its source `MeshNode` (a regression guard against a
silently dropped node kind); that a node shared by two CSG parents is enumerated per path; that a
`CsgNode` composes its transform child-then-parent and passes its appearance to operands that have
none; that a single-child node of every operation enumerates exactly that child; and that ten
thousand nested CSG nodes are enumerated without stack overflow.

##### CanvasNetRender3D-SceneModel-SceneGraph-OverlayNode: OverlayNode Validates Its Arguments

**Test**: `SceneGraphTests.OverlayNode_Constructor_CopiesChildren_RejectsNullChild_AndNullEnumerable`

Asserts that the color is kept, that later changes of the caller's child list do not affect the node,
that an empty overlay is allowed, and that a null child sequence throws `ArgumentNullException` and a
null child element `ArgumentException`.

##### CanvasNetRender3D-SceneModel-SceneGraph-OverlayEnumeration: Overlay Bodies Are Enumerated With A Forced Color

**Tests**: `SceneGraphTests.OverlayNode_EnumerateInstances_ForcesOverlayColorOverInnerAppearances_AndMarksInOverlay`,
`SceneGraphTests.OverlayNode_NestedOverlay_ActsAsGroupWithOuterColor`,
`SceneGraphTests.OverlayNode_Transform_ComposesLikeGroup`,
`SceneGraphTests.Scene_EnumerateInstances_DeepOverlayNesting_IsIterative`

Asserts that the instances beneath an overlay are enumerated in pre-order with the overlay's color
replacing their own and their ancestors' appearances (including the overlay node's own
`Appearance`) and marked as being in an overlay, while an instance outside keeps its appearance; that
an overlay nested in an overlay is a plain group with the outer color; that the overlay's transform
composes child-then-parent like a group's; and that ten thousand nested overlays are enumerated
without stack overflow with the single instance marked and the scene bounds correct.

##### CanvasNetRender3D-SceneModel-SceneGraph-OverlayBounds: Overlay Bodies Can Be Left Out Of The Bounds

**Tests**: `SceneGraphTests.Scene_ComputeBounds_WithoutOverlays_SkipsOverlayInstances`,
`SceneGraphTests.Scene_ComputeBounds_Default_IncludesOverlays`,
`SceneGraphTests.Scene_ComputeBounds_OverlayInsideASubtrahend_IsIncludedOnlyWithOverlays`

Asserts, for a unit cube beside a far overlay cube, that `ComputeBounds(false)` covers only the cube,
`ComputeBounds(true)` and `ComputeBounds()` cover both, a scene of only an overlay has empty bounds
without overlays, and repeated calls return the cached values. Asserts, for a unit cube minus a far
highlighted cutter (the cutter beside an overlay of itself), that the bounds with overlays reach the
cutter and the bounds without overlays are the unit cube alone.

#### Acceptance Criteria

A unit-level test run passes when every scenario above passes without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong matrix,
appearance, order, or bounds value constitutes a failure.
