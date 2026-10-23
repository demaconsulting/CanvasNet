package CanvasNet {
    part def PrimitiveTessellator {
        doc /* The public static PrimitiveTessellator class: Box (optionally centered), Sphere (UV sphere with true pole vertices), Cylinder (a cone when one radius is zero, a frustum when the radii differ, optionally centered), and Polyhedron (fan-triangulated convex faces, validated structure only) factories, each producing a welded Mesh that is closed (except an open Polyhedron) and outward-oriented, rejecting invalid dimensions, segment counts, and indices. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Render3D/Tessellation/PrimitiveTessellator.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Render3D.Tests/Tessellation/PrimitiveTessellatorTests.cs */
        comment designRef /* Design: docs/design/canvas-net-render3d/tessellation/primitive-tessellator.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-render3d/tessellation/primitive-tessellator.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-render3d/tessellation/primitive-tessellator.yaml */
    }
}
