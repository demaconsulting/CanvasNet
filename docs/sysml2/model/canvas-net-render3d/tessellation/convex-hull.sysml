package CanvasNetRender3D_ConvexHull {
    private import SoftwareItemMetadata::*;

    part def ConvexHull {
        doc /* The public static ConvexHull class: Hull3D (exact incremental convex hull of a point set as a closed, welded, outward-oriented triangle mesh whose vertices are exactly the strictly extreme points, a valid constructive-solid-geometry operand that depends only on the point set) and Hull2D (strictly convex counter-clockwise polygon of a planar point set), with the internal incremental engine HullBuilder3D and the exact three-dimensional orientation predicate SolidPredicates (filtered double, 128-bit and arbitrary-precision evaluation); degenerate input is rejected or returned as a point or segment, work is bounded and cancelable. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.Render3D/Tessellation/ConvexHull.cs",
                "src/DemaConsulting.CanvasNet.Render3D/Tessellation/HullBuilder3D.cs",
                "src/DemaConsulting.CanvasNet.Render3D/Tessellation/SolidPredicates.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.Render3D.Tests/Tessellation/ConvexHullTests.cs",
                "test/DemaConsulting.CanvasNet.Render3D.Tests/Tessellation/SolidPredicatesTests.cs"
            );
            designFiles = ("docs/design/canvas-net-render3d/tessellation/convex-hull.md");
            verificationFiles = ("docs/verification/canvas-net-render3d/tessellation/convex-hull.md");
            requirementFiles = ("docs/reqstream/canvas-net-render3d/tessellation/convex-hull.yaml");
        }
    }
}
