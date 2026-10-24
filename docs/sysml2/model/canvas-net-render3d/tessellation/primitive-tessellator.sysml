package CanvasNet {
    part def PrimitiveTessellator {
        doc /* The public static PrimitiveTessellator class: Box (optionally centered), Sphere (UV sphere with true pole vertices), Cylinder (a cone when one radius is zero, a frustum when the radii differ, optionally centered), and Polyhedron (fan-triangulated convex faces, validated structure only) factories, each producing a welded Mesh, plus the 2D Rectangle, Ellipse, Circle, and Polygon (points and index paths) factories, each producing a validated PolygonRegion; Box, Sphere, and Cylinder results are closed and outward-oriented, while Polyhedron preserves the caller-supplied faces and winding as given (closure and orientation are the caller's responsibility); invalid dimensions, segment counts, and indices are rejected. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Render3D/Tessellation/PrimitiveTessellator.cs, src/DemaConsulting.CanvasNet.Render3D/Tessellation/PrimitiveTessellator.Shapes2D.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Render3D.Tests/Tessellation/PrimitiveTessellatorTests.cs, test/DemaConsulting.CanvasNet.Render3D.Tests/Tessellation/PrimitiveTessellator2DTests.cs */
        comment designRef /* Design: docs/design/canvas-net-render3d/tessellation/primitive-tessellator.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-render3d/tessellation/primitive-tessellator.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-render3d/tessellation/primitive-tessellator.yaml */
    }
}
