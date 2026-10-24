package CanvasNet {
    part def Tessellation {
        doc /* Conversion of analytic 3D primitives into SceneModel Mesh instances in the DemaConsulting.CanvasNet.Render3D.Tessellation namespace: closed, welded, outward-oriented (counter-clockwise seen from outside) box, sphere, and cylinder/cone/frustum meshes, and polyhedron meshes that preserve the caller-supplied faces and winding as given. As of this release, its sole constituent unit is PrimitiveTessellator. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Render3D.Tests/Tessellation/TessellationTests.cs */
        comment designRef /* Design: docs/design/canvas-net-render3d/tessellation.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-render3d/tessellation.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-render3d/tessellation.yaml */

        part primitiveTessellator : PrimitiveTessellator;
    }
}
