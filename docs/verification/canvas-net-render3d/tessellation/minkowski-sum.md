### MinkowskiSum Unit Verification Design

<!-- cspell:ignore Minkowski convexity convex coplanar polytope welded octahedron pentagram chord unwelded -->

This document describes the unit-level verification strategy for the `MinkowskiSum` unit (`ConvexSum`).

#### Verification Approach

The unit is verified with analytic sums, with a catalogue of non-convex operands that must be rejected, with
the documented tolerance boundary, and with argument, determinism and cancellation checks.

**Analytic oracles.** A box plus a box is the box of the summed edge lengths (volume 60, extents checked). A
cube plus a tessellated sphere (five segment counts) has exactly the mixed-volume value
`a^3 + a^2 (wx + wy + wz) + a (Ax + Ay + Az) + Vol(S)`, where `w` are the widths and `A` the projected areas of the
sphere polytope `S` (the projected areas are computed from the triangles), within relative `1e-9`, and lies
between the box with slabs and the analytic rounded box. Rotated and translated cubes (whose quads are not
exactly planar), a mirrored rotated sphere and a rotated cone-like cylinder are accepted. The output of
`ConvexHull` (here a lattice hull) is accepted and its sum with a box has the analytic volume.

**Convexity detection.** A theory feeds eight non-convex operands, each on both sides, and asserts a
`NotSupportedException` whose message names the operand (`'a'` or `'b'`): an L-shaped prism, a cube whose face
center is pushed 0.3 into the solid, a square prism with a hole (genus one), two disjoint cubes, two cubes
touching along an edge (an edge shared by four faces), two cubes touching at a vertex, an octahedron with one
edge flipped into a chord through the interior, and a pentagram cone whose apex fan winds twice. A further test
asserts that an open cube, an inside-out cube and a cube with one flipped triangle are rejected with messages
that name the defect. The tolerance test uses a unit cube whose face center is pushed in by `d` (dent volume
`d / 3`): `d = 1e-13` and `1e-10` are accepted (the sum is the box of the summed extents), `d = 1e-8`, `1e-6` and
`1e-3` are rejected, and the same relative dent on a cube scaled by 1000 is accepted, which shows the tolerance is
relative.

**Arguments and bound.** Null operands, an empty mesh and a flat closed double-sided triangle are rejected with
the documented exceptions; two spheres with about 1,740 vertices each (a product above 2^21) are rejected
immediately with a message naming the pairwise sums, with less than 20 MB allocated and in under five seconds,
and the constant `MaxPairwiseSums` equals 2,097,152.

**Determinism and operands.** The fingerprint of `ConvexSum(sphere, box)` equals those of the swapped call, of a
call with the sphere's vertices permuted, of a call with the unwelded (STL-style) sphere, and of a repeated call.
Five operand pairs give results that pass `AssertValidCsgOperand` and `AssertClosedAndOutward`. A pre-canceled
token throws `OperationCanceledException` for small and large operands.

The unit depends only on `ConvexHull` and the `SceneModel` types, so no mocking or stubbing is required. The only
link to another subsystem is test-only: `Render3DTestHelpers.AssertValidCsgOperand`.

Unit tests reside in `MinkowskiSumTests.cs` within the `DemaConsulting.CanvasNet.Render3D.Tests` project, in the
`Tessellation` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test creates its own meshes; random sources are seeded

#### Unit-Level Test Scenarios

##### CanvasNetRender3D-Tessellation-MinkowskiSum-ConvexSum: Convex Operands Give The Exact Sum

**Tests**: `MinkowskiSumTests.MinkowskiSum_ConvexSum_CubePlusCube_IsBoxWithAnalyticVolume`,
`MinkowskiSumTests.MinkowskiSum_ConvexSum_CubePlusSphere_HasTheRoundedBoxVolume`,
`MinkowskiSumTests.MinkowskiSum_ConvexSum_RotatedConvexOperand_IsAccepted`,
`MinkowskiSumTests.MinkowskiSum_ConvexSum_HullOutputOperand_IsAccepted`

Asserts the analytic sums and the acceptance of rotated, mirrored and hull-output operands described above.

##### CanvasNetRender3D-Tessellation-MinkowskiSum-ConvexityDetection: Non-Convex Operands Are Rejected

**Tests**: `MinkowskiSumTests.MinkowskiSum_ConvexSum_NonConvexOperand_ThrowsNotSupported`,
`MinkowskiSumTests.MinkowskiSum_ConvexSum_OpenOrInvertedOperand_ThrowsNotSupported`,
`MinkowskiSumTests.MinkowskiSum_ConvexSum_ToleranceBoundary_RejectsDentsAboveTauAndAcceptsRoundingNoise`

Asserts the rejection catalogue, the defect messages and the tolerance boundary described above.

##### CanvasNetRender3D-Tessellation-MinkowskiSum-Validation: Arguments And The Work Bound Are Checked

**Tests**: `MinkowskiSumTests.MinkowskiSum_ConvexSum_NullEmptyOrFlatOperand_Throws`,
`MinkowskiSumTests.MinkowskiSum_ConvexSum_TooManyPairwiseSums_ThrowsBeforeAllocating`

Asserts the exceptions for null, empty and flat operands and the early rejection above the pair bound.

##### CanvasNetRender3D-Tessellation-MinkowskiSum-Determinism: The Sum Does Not Depend On Operand Order Or Storage

**Tests**: `MinkowskiSumTests.MinkowskiSum_ConvexSum_OperandOrderAndRepeats_AreByteIdentical`

Asserts identical fingerprints for swapped, permuted, unwelded and repeated calls.

##### CanvasNetRender3D-Tessellation-MinkowskiSum-ValidOperand: Sums Are Valid CSG Operands

**Tests**: `MinkowskiSumTests.MinkowskiSum_ConvexSum_Results_AreClosedOutwardAndValidCsgOperands`

Applies `AssertValidCsgOperand` and `AssertClosedAndOutward` to five operand pairs.

##### CanvasNetRender3D-Tessellation-MinkowskiSum-Cancellation: Cancellation Is Observed

**Tests**: `MinkowskiSumTests.MinkowskiSum_ConvexSum_Canceled_ThrowsOperationCanceledException`

Asserts `OperationCanceledException` for a pre-canceled token.

#### Acceptance Criteria

A unit-level test run passes when every scenario above passes without error or exception beyond those
explicitly asserted. Any unexpected exception, wrong exception type (in particular a non-convex operand that is
accepted or approximated), wrong volume beyond the stated tolerance, or a result that is not a valid
constructive-solid-geometry operand constitutes a failure.
