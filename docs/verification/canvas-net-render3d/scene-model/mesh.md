### Mesh Unit Verification Design

<!-- cspell:ignore Orientable -->

This document describes the unit-level verification strategy for the `Mesh`, `MeshBuilder`, and
`MeshTopology` types.

#### Verification Approach

The unit is verified through direct calls to the public API and, for the internal `MeshTopology`,
through the test assembly's `InternalsVisibleTo` access. Test meshes are small and analytic (a unit
cube, a tetrahedron, two disjoint cubes) so volumes, areas, and topology verdicts are known exactly.
The unit depends only on `DoublePrecisionMath` types and the base class library, so no mocking or
stubbing is required.

Unit tests reside in `MeshTests.cs`, `MeshBuilderTests.cs`, and `MeshTopologyTests.cs` within the
`DemaConsulting.CanvasNet.Render3D.Tests` project, in the `SceneModel` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test constructs its own meshes and builders

#### Unit-Level Test Scenarios

##### CanvasNetRender3D-SceneModel-Mesh-Construction: Mesh Validates Its Input

**Tests**: `MeshTests.Mesh_Constructor_ValidTriangle_SetsCounts`,
`MeshTests.Mesh_Constructor_IndexCountNotMultipleOfThree_Throws`,
`MeshTests.Mesh_Constructor_IndexOutOfRange_Throws`,
`MeshTests.Mesh_Constructor_NonFiniteVertex_Throws`,
`MeshTests.Mesh_Constructor_NullArguments_Throw`,
`MeshTests.Mesh_Constructor_ZeroTriangles_AllowedWithEmptyBounds`

Constructs a valid single-triangle mesh and asserts its counts, and asserts the documented
exception for each invalid shape (index count, out-of-range index, non-finite vertex, null
sequences); a mesh with zero triangles is accepted and has empty bounds.

##### CanvasNetRender3D-SceneModel-Mesh-DefensiveCopy: Mesh Copies Its Inputs

**Test**: `MeshTests.Mesh_Constructor_MutatingSourceListsAfterwards_DoesNotAffectMesh`

Mutates the source vertex and index lists after construction and asserts that the mesh is unchanged.

##### CanvasNetRender3D-SceneModel-Mesh-Bounds: Bounds Match The Vertex Extents

**Test**: `MeshTests.Mesh_Bounds_Cube_MatchesVertexExtents`

Asserts that a cube's `Bounds` equals the minimum and maximum of its vertices.

##### CanvasNetRender3D-SceneModel-Mesh-Topology: Closedness Is Detected Correctly

**Tests**: `MeshTopologyTests.MeshTopology_IsClosed_Cube_True`,
`MeshTopologyTests.MeshTopology_IsClosed_MissingTriangle_False`,
`MeshTopologyTests.MeshTopology_IsClosed_InconsistentWinding_False`,
`MeshTopologyTests.MeshTopology_IsClosed_EdgeSharedByThreeTriangles_False`,
`MeshTopologyTests.MeshTopology_IsClosed_TwoDisjointCubes_True`,
`MeshTests.Mesh_IsClosed_Tetrahedron_True`

Asserts that a closed cube and a tetrahedron are closed, that a cube missing a triangle, a mesh with
inconsistent winding, and a mesh with an edge shared by three triangles are not, and that two
disjoint closed cubes in one mesh are closed.

##### CanvasNetRender3D-SceneModel-Mesh-VolumeArea: Signed Volume And Surface Area Are Analytic

**Tests**: `MeshTests.Mesh_SignedVolume_UnitCube_ReturnsOne`,
`MeshTests.Mesh_SignedVolume_Tetrahedron_ReturnsAnalytic`,
`MeshTests.Mesh_SurfaceArea_UnitCube_ReturnsSix`

Asserts the signed volume of a unit cube (1) and of a tetrahedron, and the surface area of a unit
cube (6).

##### CanvasNetRender3D-SceneModel-Mesh-Transform: Transform Moves Vertices And Preserves Orientation

**Tests**: `MeshTests.Mesh_Transform_Translation_MovesBounds`,
`MeshTests.Mesh_Transform_MirrorScale_ReversesWindingAndKeepsPositiveVolume`,
`MeshTests.Mesh_Transform_NonFiniteResult_Throws`

Asserts that a translation moves the bounds, that a mirroring scale reverses the winding so the
volume stays positive, and that a transform producing a non-finite vertex is rejected.

##### CanvasNetRender3D-SceneModel-Mesh-Reverse: Reverse Flips Orientation

**Test**: `MeshTests.Mesh_Reverse_ClosedMesh_NegatesSignedVolume`

Asserts that reversing a closed mesh negates its signed volume.

##### CanvasNetRender3D-SceneModel-Mesh-Builder: MeshBuilder Welds And Validates

**Tests**: `MeshBuilderTests.MeshBuilder_AddVertex_IdenticalPoints_AreWelded`,
`MeshBuilderTests.MeshBuilder_AddVertex_NegativeZero_WeldsWithPositiveZero`,
`MeshBuilderTests.MeshBuilder_AddTriangle_RepeatedIndex_Throws`,
`MeshBuilderTests.MeshBuilder_AddTriangle_IndexOutOfRange_Throws`,
`MeshBuilderTests.MeshBuilder_AddPolygon_Quad_FansToTwoTriangles`,
`MeshBuilderTests.MeshBuilder_AddPolygon_TwoIndices_Throws`,
`MeshBuilderTests.MeshBuilder_AddPolygon_RepeatedIndex_ThrowsAndLeavesBuilderUnchanged`,
`MeshBuilderTests.MeshBuilder_Build_CalledTwice_ReturnsIndependentMeshes`

Asserts exact-value welding (including negative zero), the exceptions for a repeated or
out-of-range triangle index and for a polygon with fewer than three indices, a repeated index, or
an out-of-range index (leaving the builder unchanged), fan triangulation of a
quad into two triangles, and that two `Build` calls return independent meshes.

#### Acceptance Criteria

A unit-level test run passes when every scenario above passes without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong count, volume,
or topology verdict constitutes a failure.
