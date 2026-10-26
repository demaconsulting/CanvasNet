## Tessellation Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Tessellation` subsystem,
containing five units: `PrimitiveTessellator`, `PolygonTriangulator`, `Extruder`, `ConvexHull`, and
`MinkowskiSum`.

### Verification Approach

The `Tessellation` subsystem is verified by the unit tests of `PrimitiveTessellator`,
`PolygonTriangulator`, `Extruder`, `ConvexHull`, and `MinkowskiSum` (see _PrimitiveTessellator Unit
Verification Design_, _PolygonTriangulator Unit Verification Design_, _Extruder Unit Verification Design_,
_ConvexHull Unit Verification Design_, and _MinkowskiSum Unit Verification Design_ under
`tessellation/`) and by six subsystem-level tests in `TessellationTests.cs`. Because the subsystem
consumes the `SceneModel` subsystem's `Mesh` and `MeshBuilder`, the subsystem-level tests verify the
combination through the public API. The primitive test creates one mesh of each primitive kind and
asserts, with the shared helper `Render3DTestHelpers.AssertClosedAndOutward`, that every mesh is
closed, has positive signed volume, and has every triangle normal pointing away from the mesh
centroid. The polygon tests build regions with the 2D factories, extrude them, and assert with
`Render3DTestHelpers.AssertValidCsgOperand` that every result is a valid constructive-solid-geometry
operand, and that invalid polygons are rejected when the region is created. The point-set tests compute the
convex hull of the vertices of the primitives and of an extruded L-shape and the Minkowski sum of primitives,
and assert that the results are valid operands, that the hull of a convex primitive has the primitive's volume,
and that the non-convex L-shape is rejected as an operand of the sum. No mocking or stubbing is
used.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the subsystem depends only on the in-house `SceneModel` subsystem
- **Isolation**: Each test creates its own meshes and regions; no shared state

### Acceptance Criteria

The `Tessellation` subsystem's verification passes when the subsystem-level scenarios below and every
unit-level scenario in the _PrimitiveTessellator Unit Verification Design_, the
_PolygonTriangulator Unit Verification Design_, the _Extruder Unit Verification Design_, the
_ConvexHull Unit Verification Design_, and the _MinkowskiSum Unit Verification Design_ pass
without error or unexpected exception.

### Test Scenarios

- **All Primitives Are Closed And Outward Oriented**:
  `TessellationTests.Tessellation_AllPrimitives_AreClosedAndOutwardOriented` — creates a centered
  box, a sphere, a cylinder, and a cone and asserts that each is closed with positive volume and
  outward-pointing triangle normals.
- **Polyhedron Preserves Supplied Faces**:
  `TessellationTests.Tessellation_Polyhedron_OpenFaceSet_IsPreservedWithoutClosureOrReorientation` —
  builds a polyhedron from three clockwise faces of a tetrahedron and asserts that the mesh is
  still open and contains exactly the supplied triangle indices in the supplied order.
- **Polygon Extrusion Results Are Closed And Valid CSG Operands**:
  `TessellationTests.Tessellation_PolygonExtrusion_AllResults_AreClosedAndValidCsgOperands` —
  builds a square plate with a 32-gon hole from the 2D factories, a ten-point star, and an off-axis
  profile, extrudes them (the plate linearly, the star with twist and a per-axis scale and with a
  twist to an apex, the profile by full and partial rotation, and an on-axis rectangle by full
  rotation) and asserts that every result passes `AssertValidCsgOperand` and that the plate volume
  equals the plate area minus the 32-gon hole area, times the height.
- **Invalid Polygons Are Rejected**:
  `TessellationTests.Tessellation_PolygonRegion_InvalidPolygon_IsRejected` — asserts that a bow tie
  (through the constructor and through `PrimitiveTessellator.Polygon`), a zero-area contour, crossing
  contours, and touching contours throw `ArgumentException` when the region is created.
- **Convex Hull Of Primitive Vertices Is Closed And A Valid CSG Operand**:
  `TessellationTests.Tessellation_ConvexHull_OfPrimitiveVertices_IsClosedAndAValidCsgOperand` —
  computes the hull of the vertices of a box, a sphere, a cylinder frustum and a cone and asserts that
  each has the primitive's volume and passes `AssertValidCsgOperand`, and that the hull of the vertices
  of an extruded L-shape (volume 3) is a valid operand with volume 3.5.
- **Minkowski Sum Of Primitives Is Closed, Valid, And Rejects Non-Convex Operands**:
  `TessellationTests.Tessellation_MinkowskiSum_OfPrimitives_IsClosedValidAndRejectsNonConvexOperands` —
  sums a box with a sphere, a cone with a box, and the hull of an L-shape with a sphere and asserts
  that each result is a valid operand with the summed extents, and that the extruded L-shape itself is
  rejected with a `NotSupportedException` as either operand.
