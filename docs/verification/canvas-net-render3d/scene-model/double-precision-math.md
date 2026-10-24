### DoublePrecisionMath Unit Verification Design

This document describes the unit-level verification strategy for the `Vector2D`, `Vector3D`,
`Matrix4x4D`, and `BoundingBox3D` types.

#### Verification Approach

The unit is verified by direct calls to the public API with small inputs whose results are known
analytically (unit vectors, a 3-4-12 right-angle triple, exact 90-degree rotations, scale and
mirror matrices), asserting component values with exact equality where the contract is exact (for
example, a 90-degree rotation maps +X to +Y with no residual error) and with a documented tolerance
otherwise. The unit has no dependencies beyond the .NET base class library, so no mocking or
stubbing is required.

Unit tests reside in `Vector2DTests.cs`, `Vector3DTests.cs`, `Matrix4x4DTests.cs`, and
`BoundingBox3DTests.cs` within the `DemaConsulting.CanvasNet.Render3D.Tests` project, in the
`SceneModel` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test constructs its own values; the types are immutable

#### Unit-Level Test Scenarios

##### CanvasNetRender3D-SceneModel-DoublePrecisionMath-VectorArithmetic: Vector Arithmetic Matches Analytic Results

**Tests**: `Vector3DTests.Vector3D_Operators_AddSubtractScale_ComponentWise`,
`Vector3DTests.Vector3D_Dot_OrthogonalVectors_ReturnsZero`,
`Vector3DTests.Vector3D_Cross_UnitXUnitY_ReturnsUnitZ`,
`Vector3DTests.Vector3D_Length_ThreeFourTwelve_ReturnsThirteen`,
`Vector3DTests.Vector3D_Normalize_ZeroVector_ReturnsZero`,
`Vector3DTests.Vector3D_Normalize_LargeVector_ReturnsUnitVector`,
`Vector3DTests.Vector3D_Normalize_TinyVector_ReturnsUnitVector`,
`Vector3DTests.Vector3D_Normalize_NaNComponent_ReturnsZero`,
`Vector3DTests.Vector3D_Length_LargeVector_ReturnsFiniteLength`,
`Vector3DTests.Vector3D_Length_TinyVector_ReturnsNonZeroLength`,
`Vector3DTests.Vector3D_Length_ZeroVector_ReturnsZero`,
`Vector3DTests.Vector3D_ToVector3_NarrowsToFloat`,
`Vector2DTests.Vector2D_Dot_Cross_Normalize_MatchAnalytic`,
`Vector2DTests.Vector2D_Length_TinyVector_ReturnsNonZeroLength`,
`Vector2DTests.Vector2D_Length_LargeVector_ReturnsFiniteLength`,
`Vector2DTests.Vector2D_Length_ZeroVector_ReturnsZero`,
`Vector2DTests.Vector2D_Normalize_TinyVector_ReturnsUnitVector`,
`Vector2DTests.Vector2D_Normalize_LargeVector_ReturnsUnitVector`,
`Vector2DTests.Vector2D_Normalize_ZeroOrNaNVector_ReturnsZero`

Exercises the operators, dot and cross products, length, normalization (including the documented
zero-vector result), the single-precision narrowing conversion, and the 2D equivalents. The
large- and tiny-vector scenarios (for both `Vector3D` and `Vector2D`) assert that `Length` and
`Normalize` scale by the largest
component, so components of magnitude 1e200 or 1e-200 neither overflow nor underflow, and that a
vector with a NaN component normalizes to `Zero`.

##### CanvasNetRender3D-SceneModel-DoublePrecisionMath-MatrixComposition: Matrix Composition Applies The Left Operand First

**Tests**: `Matrix4x4DTests.Matrix4x4D_Identity_TransformPoint_ReturnsSamePoint`,
`Matrix4x4DTests.Matrix4x4D_CreateTranslation_TransformPoint_Translates`,
`Matrix4x4DTests.Matrix4x4D_Multiply_TranslateThenScale_AppliesLeftOperandFirst`,
`Matrix4x4DTests.Matrix4x4D_CreateRotationZ_Ninety_MapsUnitXToUnitYExactly`,
`Matrix4x4DTests.Matrix4x4D_CreateRotation_MultiplesOfNinety_AreExact`,
`Matrix4x4DTests.Matrix4x4D_CreateFromAxisAngle_ZAxis_MatchesRotationZ`,
`Matrix4x4DTests.Matrix4x4D_CreateFromAxisAngle_LargeAxis_MatchesRotationZ`,
`Matrix4x4DTests.Matrix4x4D_CreateFromAxisAngle_TinyAxis_MatchesRotationZ`,
`Matrix4x4DTests.Matrix4x4D_CreateScale_Vector_ScalesComponents`

Verifies the identity, translation, and scale factories, the row-vector product order, exact
rotations for multiples of 90 degrees, and that the axis-angle rotation about Z equals the Z
rotation (including for an axis whose squared length overflows or flushes to zero).

##### CanvasNetRender3D-SceneModel-DoublePrecisionMath-MatrixInversion: Determinant And Inverse

**Tests**: `Matrix4x4DTests.Matrix4x4D_Determinant_Scale_ReturnsProduct`,
`Matrix4x4DTests.Matrix4x4D_TryInvert_Invertible_MultipliesToIdentity`,
`Matrix4x4DTests.Matrix4x4D_TryInvert_Singular_ReturnsFalse`

Verifies the determinant of a scale matrix, that an invertible matrix times its inverse is the
identity within tolerance, and that a singular matrix reports failure.

##### CanvasNetRender3D-SceneModel-DoublePrecisionMath-MirrorTransform: Mirror Matrix Flips Orientation

**Tests**: `Matrix4x4DTests.Matrix4x4D_CreateMirror_PlaneNormalX_NegatesXAndHasNegativeDeterminant`,
`Matrix4x4DTests.Matrix4x4D_CreateMirror_ZeroNormal_ThrowsArgumentException`,
`Matrix4x4DTests.Matrix4x4D_CreateMirror_TinyNormal_MatchesUnitNormalMirror`,
`Matrix4x4DTests.Matrix4x4D_CreateMirror_LargeNormal_MatchesUnitNormalMirror`

Verifies that mirroring through a plane with normal X negates the X coordinate and has determinant
negative one, that a zero normal is rejected, and that tiny and huge non-zero normals are accepted.

##### CanvasNetRender3D-SceneModel-DoublePrecisionMath-PointDirectionTransform: Points Translate, Directions Do Not

**Tests**: `Matrix4x4DTests.Matrix4x4D_TransformDirection_Translation_IgnoresTranslation`,
`Matrix4x4DTests.Matrix4x4D_TransformPoint_LargeOffset_RetainsDoublePrecision`

Verifies that `TransformDirection` ignores translation and that a point at a coordinate of one
billion keeps a millimeter-scale offset, which single precision could not represent.

##### CanvasNetRender3D-SceneModel-DoublePrecisionMath-BoundingBoxes: Bounding Box Operations

**Tests**: `BoundingBox3DTests.BoundingBox3D_Empty_IsEmptyAndUnionIdentity`,
`BoundingBox3DTests.BoundingBox3D_Union_TwoBoxes_CoversBoth`,
`BoundingBox3DTests.BoundingBox3D_Transform_Rotation90_ReturnsAxisAlignedBounds`,
`BoundingBox3DTests.BoundingBox3D_SizeCenterDiagonal_MatchAnalytic`

Verifies the empty box and its union identity behavior, the union of two boxes, the axis-aligned
bounds of a box rotated by 90 degrees, and the size, center, and diagonal.

##### CanvasNetRender3D-SceneModel-DoublePrecisionMath-InputValidation: Invalid Input Is Rejected Or Reported

**Tests**: `Matrix4x4DTests.Matrix4x4D_FromRowMajor_WrongLength_ThrowsArgumentException`,
`Matrix4x4DTests.Matrix4x4D_CreateFromAxisAngle_ZeroAxis_ThrowsArgumentException`,
`Matrix4x4DTests.Matrix4x4D_IsFinite_NaNElement_ReturnsFalse`,
`Vector3DTests.Vector3D_IsFinite_Infinity_ReturnsFalse`

Verifies the exceptions for a wrong-length row-major input and a zero rotation axis, and that
`IsFinite` reports a NaN matrix element and an infinite vector component.

#### Acceptance Criteria

A unit-level test run passes when every scenario above passes without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong numeric value
constitutes a failure.
