package CanvasNet {
    part def Tessellation {
        doc /* Conversion of analytic 3D primitives into SceneModel Mesh instances in the DemaConsulting.CanvasNet.Render3D.Tessellation namespace: closed, welded, outward-oriented (counter-clockwise seen from outside) box, sphere, and cylinder/cone/frustum meshes, and polyhedron meshes that preserve the caller-supplied faces and winding as given, plus validated 2D polygon regions, an exact ear-clipping polygon triangulator with hole bridging, and linear and rotational extrusion of those regions into closed, outward-oriented meshes that are valid constructive-solid-geometry operands, plus the exact convex hull of point sets (as a mesh or a planar polygon) and the Minkowski sum of convex solids, which rejects non-convex operands instead of approximating them. Its constituent units are PrimitiveTessellator, PolygonTriangulator, Extruder, ConvexHull, and MinkowskiSum. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Render3D.Tests/Tessellation/TessellationTests.cs */
        comment designRef /* Design: docs/design/canvas-net-render3d/tessellation.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-render3d/tessellation.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-render3d/tessellation.yaml */

        part primitiveTessellator : PrimitiveTessellator;
        part polygonTriangulator : PolygonTriangulator;
        part extruder : Extruder;
        part convexHull : ConvexHull;
        part minkowskiSum : MinkowskiSum;
    }
}
