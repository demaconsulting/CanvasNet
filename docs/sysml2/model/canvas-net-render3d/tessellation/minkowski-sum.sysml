package CanvasNetRender3D_MinkowskiSum {
    private import SoftwareItemMetadata::*;

    part def MinkowskiSum {
        doc /* The public static MinkowskiSum class: ConvexSum computes the Minkowski sum of two convex solid meshes as the exact convex hull of all pairwise vertex sums (a valid constructive-solid-geometry operand, byte-identical for swapped or reordered operands), verifies that each operand is a closed convex solid (exact structural preconditions and a global comparison with its own convex hull within a relative tolerance of 1e-9) and rejects any other operand with a NotSupportedException instead of approximating it; the work is bounded by MaxPairwiseSums and cancelable. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.Render3D/Tessellation/MinkowskiSum.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Render3D.Tests/Tessellation/MinkowskiSumTests.cs");
            designFiles = ("docs/design/canvas-net-render3d/tessellation/minkowski-sum.md");
            verificationFiles = ("docs/verification/canvas-net-render3d/tessellation/minkowski-sum.md");
            requirementFiles = ("docs/reqstream/canvas-net-render3d/tessellation/minkowski-sum.yaml");
        }
    }
}
