package CanvasNet {
    /* cspell:ignore multmatrix */
    part def ScadTransforms {
        doc /* The internal static ScadTransforms class and its color helpers: they build the transform modules (translate, rotate in vector Euler order X then Y then Z, scalar about Z or axis-angle, scale, mirror, multmatrix with a column-vector matrix transposed into the renderer's row-vector convention, and resize to a requested bounding-box size with auto proportions and exact bounds of rotated children) and the color module (CSS names from the 149-entry ScadColorTable, hexadecimal and vector forms, innermost color wins) from the results of a module's children. A transform applies to the implicit union of its children as a wrapper node carrying the matrix (an identity adds none; 2D shapes use the in-plane part and a warning for a Z coupling). An invalid argument or a singular or non-finite transform is a warning and an empty instance, except for a zero rotate axis, a zero mirror normal, and an invalid color or alpha, which keep the children; a projective multmatrix, an alpha below 1, and a resize over a difference or intersection are reported as the unsupported features scad-multmatrix-projective, scad-color-alpha, and scad-resize-approximate. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Scad/Geometry/ScadTransforms.cs, src/DemaConsulting.CanvasNet.Scad/Geometry/ScadTransforms.Resize.cs, src/DemaConsulting.CanvasNet.Scad/Geometry/ScadColors.cs, src/DemaConsulting.CanvasNet.Scad/Geometry/ScadColorTable.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Scad.Tests/Geometry/ScadTransformsTests.cs, test/DemaConsulting.CanvasNet.Scad.Tests/Geometry/ScadResizeTests.cs, test/DemaConsulting.CanvasNet.Scad.Tests/Geometry/ScadColorsTests.cs, test/DemaConsulting.CanvasNet.Scad.Tests/Geometry/ScadGeometryTestSupport.cs */
        comment designRef /* Design: docs/design/canvas-net-scad/scad-geometry/scad-transforms.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-scad/scad-geometry/scad-transforms.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-scad/scad-geometry/scad-transforms.yaml */
    }
}
