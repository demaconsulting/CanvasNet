### SceneGraph Unit Verification Design

This document describes the unit-level verification strategy for the `SceneNode`, `MeshNode`,
`GroupNode`, `CsgNode`, `CsgOperation`, `NodeAppearance`, and `Scene` types and the internal
`SceneInstance`.

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
`SceneGraphTests.Scene_EnumerateInstances_NoAppearance_ReportsNull`

Asserts that a mesh node's own appearance overrides its ancestors', that an ancestor's appearance is
inherited when the node has none, and that an instance with no appearance anywhere reports `null`.

##### CanvasNetRender3D-SceneModel-SceneGraph-SceneBounds: Scene Bounds Are Tight

**Tests**: `SceneGraphTests.Scene_ComputeBounds_TransformedGroup_MatchesAnalytic`,
`SceneGraphTests.Scene_ComputeBounds_EmptyGroup_IsEmpty`,
`SceneGraphTests.Scene_ComputeBounds_CalledTwice_ReturnsSame`

Asserts the analytic bounds of a transformed group, that a scene of an empty group has empty bounds,
and that repeated calls return the same value.

##### CanvasNetRender3D-SceneModel-SceneGraph-DeepNesting: Deep Nesting Does Not Overflow The Stack

**Test**: `SceneGraphTests.Scene_EnumerateInstances_TenThousandNestedGroups_DoesNotOverflowStack`

Builds a ten-thousand-level chain of nested groups, each translating by one unit along X, and
asserts that enumeration returns the single leaf instance with an accumulated translation of
10 000 and that the scene bounds agree.

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
`SceneGraphTests.Scene_ComputeBounds_CsgNode_IncludesAllOperands`,
`SceneGraphTests.Scene_EnumerateInstances_TenThousandNestedCsgNodes_DoesNotOverflowStack`

Asserts, for a mixed tree of groups and difference/intersection nodes, that every operand appears
as an instance in depth-first pre-order with its source `MeshNode` (a regression guard against a
silently dropped node kind); that a node shared by two CSG parents is enumerated per path; that a
`CsgNode` composes its transform child-then-parent and passes its appearance to operands that have
none; that a single-child node of every operation enumerates exactly that child; that the scene
bounds of a difference and an intersection include the far operand; and that ten thousand nested
CSG nodes are enumerated without stack overflow.

#### Acceptance Criteria

A unit-level test run passes when every scenario above passes without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong matrix,
appearance, order, or bounds value constitutes a failure.
