package CanvasNet {
    part def PolygonTriangulator {
        doc /* The polygon unit of the Tessellation subsystem: the public immutable PolygonRegion (even-odd nesting, normalized winding, area, empty region; invalid polygons - non-finite, zero-area, self-intersecting or touching contours - are rejected with an ArgumentException), the internal exact epsilon-free PolygonPredicates (orientation with a BigInteger fallback, segment intersection, containment), and the public static PolygonTriangulator, which bridges holes into their containing contour and ear-clips the result into counter-clockwise triangles over the region's own vertices (exactly V + 2H - 2P triangles, no Steiner points, every collinear vertex kept). */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Render3D/Tessellation/PolygonRegion.cs, src/DemaConsulting.CanvasNet.Render3D/Tessellation/ContourInfo.cs, src/DemaConsulting.CanvasNet.Render3D/Tessellation/PolygonPredicates.cs, src/DemaConsulting.CanvasNet.Render3D/Tessellation/PolygonTriangulator.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Render3D.Tests/Tessellation/PolygonRegionTests.cs, test/DemaConsulting.CanvasNet.Render3D.Tests/Tessellation/PolygonPredicatesTests.cs, test/DemaConsulting.CanvasNet.Render3D.Tests/Tessellation/PolygonTriangulatorTests.cs */
        comment designRef /* Design: docs/design/canvas-net-render3d/tessellation/polygon-triangulator.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-render3d/tessellation/polygon-triangulator.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-render3d/tessellation/polygon-triangulator.yaml */
    }
}
