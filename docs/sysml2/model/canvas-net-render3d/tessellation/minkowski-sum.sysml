package CanvasNet {
    part def MinkowskiSum {
        doc /* The public static MinkowskiSum class: ConvexSum computes the Minkowski sum of two convex solid meshes as the exact convex hull of all pairwise vertex sums (a valid constructive-solid-geometry operand, byte-identical for swapped or reordered operands), verifies that each operand is a closed convex solid (exact structural preconditions and a global comparison with its own convex hull within a relative tolerance of 1e-9) and rejects any other operand with a NotSupportedException instead of approximating it; the work is bounded by MaxPairwiseSums and cancellable. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Render3D/Tessellation/MinkowskiSum.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Render3D.Tests/Tessellation/MinkowskiSumTests.cs */
        comment designRef /* Design: docs/design/canvas-net-render3d/tessellation/minkowski-sum.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-render3d/tessellation/minkowski-sum.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-render3d/tessellation/minkowski-sum.yaml */
    }
}
