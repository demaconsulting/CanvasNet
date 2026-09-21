package CanvasNet {
    part def SvgArcConverter {
        doc /* Converts SVG-style endpoint-parameterized elliptical arcs into cubic Bezier curves, following the SVG 1.1 Appendix F algorithm, including its documented degenerate cases. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Geometry/SvgArcConverter.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Geometry/SvgArcConverterTests.cs */
        comment designRef /* Design: docs/design/canvas-net/geometry/svg-arc-converter.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/geometry/svg-arc-converter.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/geometry/svg-arc-converter.yaml */
    }
}
