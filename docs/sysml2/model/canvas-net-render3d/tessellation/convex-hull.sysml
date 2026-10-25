package CanvasNet {
    part def ConvexHull {
        doc /* The public static ConvexHull class: Hull3D (exact incremental convex hull of a point set as a closed, welded, outward-oriented triangle mesh whose vertices are exactly the strictly extreme points, a valid constructive-solid-geometry operand that depends only on the point set) and Hull2D (strictly convex counter-clockwise polygon of a planar point set), with the internal incremental engine HullBuilder3D and the exact three-dimensional orientation predicate SolidPredicates (filtered double, 128-bit and arbitrary-precision evaluation); degenerate input is rejected or returned as a point or segment, work is bounded and cancellable. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Render3D/Tessellation/ConvexHull.cs, src/DemaConsulting.CanvasNet.Render3D/Tessellation/HullBuilder3D.cs, src/DemaConsulting.CanvasNet.Render3D/Tessellation/SolidPredicates.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Render3D.Tests/Tessellation/ConvexHullTests.cs, test/DemaConsulting.CanvasNet.Render3D.Tests/Tessellation/SolidPredicatesTests.cs */
        comment designRef /* Design: docs/design/canvas-net-render3d/tessellation/convex-hull.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-render3d/tessellation/convex-hull.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-render3d/tessellation/convex-hull.yaml */
    }
}
