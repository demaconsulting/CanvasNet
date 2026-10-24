## SceneModel Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `SceneModel` subsystem,
containing three units: `DoublePrecisionMath`, `Mesh`, and `SceneGraph`.

### Verification Approach

The `SceneModel` subsystem is verified at two tiers. The unit tests of its three units (see
_DoublePrecisionMath Unit Verification Design_, `scene-model/double-precision-math.md`, _Mesh Unit
Verification Design_, `scene-model/mesh.md`, and _SceneGraph Unit Verification Design_,
`scene-model/scene-graph.md`) verify each unit in isolation. The subsystem-level tests in
`SceneModelTests.cs` verify cross-unit integration through the subsystem's public API only: that
transforms compose in double precision through a nested scene graph, that a mirrored mesh inside a
group keeps positive volume and stays closed, and that nested groups yield analytically correct
bounds. No mocking or stubbing is used; the units under test are in-house and fully deterministic.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `SceneModel` depends only on the .NET base class library and the core
  `CanvasNet` system's `Rgba32`
- **Isolation**: Each test constructs its own meshes and scenes; no shared state

### Acceptance Criteria

The `SceneModel` subsystem's verification passes when every subsystem-level scenario below and every
unit-level scenario described in the three unit verification documents passes without error or
unexpected exception.

### Test Scenarios

- **Transform Chain Composes In Double**:
  `SceneModelTests.SceneModel_TransformChain_ComposesInDouble` — builds a 0.001-unit cube rotated
  90 degrees about Z inside a group translated one million units along X, and asserts that the
  scene bounds match the analytic result to 1e-9, which a single-precision pipeline could not
  achieve.
- **Mirrored Mesh In Group Keeps Positive Volume And Stays Closed**:
  `SceneModelTests.SceneModel_MirroredMeshInGroup_KeepsPositiveVolumeAndClosed` — mirrors a cube
  through a group transform, applies the enumerated instance's world matrix with `Mesh.Transform`,
  and asserts that the world determinant is negative and the result is closed with a positive
  unit `SignedVolume`.
- **Nested Groups Bounds Match Analytic**:
  `SceneModelTests.SceneModel_NestedGroups_BoundsMatchAnalytic` — builds nested groups with
  different translations and asserts that `Scene.ComputeBounds` equals the analytic union of the
  transformed cube extents.
