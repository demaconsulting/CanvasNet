package CanvasNetRender3D_PrimitiveTessellator {
    private import SoftwareItemMetadata::*;

    part def PrimitiveTessellator {
        doc /* The public static PrimitiveTessellator class: Box (optionally centered), Sphere (UV sphere with true pole vertices), Cylinder (a cone when one radius is zero, a frustum when the radii differ, optionally centered), and Polyhedron (fan-triangulated convex faces, validated structure only) factories, each producing a welded Mesh, plus the 2D Rectangle, Ellipse, Circle, and Polygon (points and index paths) factories, each producing a validated PolygonRegion; Box, Sphere, and Cylinder results are closed and outward-oriented, while Polyhedron preserves the caller-supplied faces and winding as given (closure and orientation are the caller's responsibility); invalid dimensions, segment counts, and indices are rejected. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.Render3D/Tessellation/PrimitiveTessellator.cs",
                "src/DemaConsulting.CanvasNet.Render3D/Tessellation/PrimitiveTessellator.Shapes2D.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.Render3D.Tests/Tessellation/PrimitiveTessellatorTests.cs",
                "test/DemaConsulting.CanvasNet.Render3D.Tests/Tessellation/PrimitiveTessellator2DTests.cs"
            );
            designFiles = ("docs/design/canvas-net-render3d/tessellation/primitive-tessellator.md");
            verificationFiles = ("docs/verification/canvas-net-render3d/tessellation/primitive-tessellator.md");
            requirementFiles = ("docs/reqstream/canvas-net-render3d/tessellation/primitive-tessellator.yaml");
        }
    }
}
