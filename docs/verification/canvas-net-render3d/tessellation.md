## Tessellation Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Tessellation` subsystem,
containing one unit: `PrimitiveTessellator`.

### Verification Approach

The `Tessellation` subsystem is verified by the unit tests of `PrimitiveTessellator` (see
_PrimitiveTessellator Unit Verification Design_, `tessellation/primitive-tessellator.md`) and by one
subsystem-level test in `TessellationTests.cs`. Because the subsystem consumes the `SceneModel`
subsystem's `Mesh` and `MeshBuilder`, the subsystem-level test verifies the combination through the
public API: it creates one mesh of each primitive kind and asserts, with the shared helper
`Render3DTestHelpers.AssertClosedAndOutward`, that every mesh is closed, has positive signed volume,
and has every triangle normal pointing away from the mesh centroid. No mocking or stubbing is used.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the subsystem depends only on the in-house `SceneModel` subsystem
- **Isolation**: Each test creates its own meshes; no shared state

### Acceptance Criteria

The `Tessellation` subsystem's verification passes when the subsystem-level scenario below and every
unit-level scenario in the _PrimitiveTessellator Unit Verification Design_ pass without error or
unexpected exception.

### Test Scenarios

- **All Primitives Are Closed And Outward Oriented**:
  `TessellationTests.Tessellation_AllPrimitives_AreClosedAndOutwardOriented` — creates a centered
  box, a sphere, a cylinder, and a cone and asserts that each is closed with positive volume and
  outward-pointing triangle normals.
