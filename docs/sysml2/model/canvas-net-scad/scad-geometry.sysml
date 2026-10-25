package CanvasNet {
    part def ScadGeometry {
        doc /* The internal geometry subsystem in the DemaConsulting.CanvasNet.Scad.Geometry namespace that turns the geometry-agnostic instantiation tree of an evaluation into a Render3D Scene: the ScadGeometryBuilder walks the tree iteratively with an explicit stack (no code in the subsystem recurses, so a model nested thousands of levels deep builds identically on any caller stack), combines children as implicit unions, applies the show-only, highlight, and background modifiers, builds difference and intersection for solids and for 2D shapes (a symbolic 2D tree), enforces the triangle budget and cancellation, reports every invalid argument as a warning and every recognized-but-unsupported construct as an unsupported feature through the evaluation's single sink (and, in strict mode, as a ScadUnsupportedFeatureException), records the source location of every mesh node, and prepares the renderer options (copy with a substituted unsupported-feature handler and mapping of the renderer's report identifiers). The ScadPrimitives unit builds cube, sphere, cylinder, polyhedron, square, circle, and polygon with the documented fragment rule for the special variables that control curve resolution; the ScadTransforms unit applies translate, rotate, scale, mirror, multmatrix, resize, and color; the ScadExtrusions unit builds linear_extrude and rotate_extrude by distributing the extrusion over the 2D tree. The subsystem is internal; its entry point is called by ScadDocument.BuildScene and ScadDocument.Render. As of this release, its constituent units are ScadGeometryBuilder, ScadPrimitives, ScadTransforms, and ScadExtrusions. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Scad.Tests/Geometry/ScadGeometryTests.cs, test/DemaConsulting.CanvasNet.Scad.Tests/Geometry/ScadGeometryTestSupport.cs */
        comment designRef /* Design: docs/design/canvas-net-scad/scad-geometry.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-scad/scad-geometry.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-scad/scad-geometry.yaml */

        part scadGeometryBuilder : ScadGeometryBuilder;
        part scadPrimitives : ScadPrimitives;
        part scadTransforms : ScadTransforms;
        part scadExtrusions : ScadExtrusions;
    }
}
