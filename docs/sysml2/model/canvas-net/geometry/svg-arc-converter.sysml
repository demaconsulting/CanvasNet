package CanvasNet_SvgArcConverter {
    private import SoftwareItemMetadata::*;

    part def SvgArcConverter {
        doc /* Converts SVG-style endpoint-parameterized elliptical arcs into cubic Bezier curves, following the SVG 1.1 Appendix F algorithm, including its documented degenerate cases. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet/Geometry/SvgArcConverter.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Tests/Geometry/SvgArcConverterTests.cs");
            designFiles = ("docs/design/canvas-net/geometry/svg-arc-converter.md");
            verificationFiles = ("docs/verification/canvas-net/geometry/svg-arc-converter.md");
            requirementFiles = ("docs/reqstream/canvas-net/geometry/svg-arc-converter.yaml");
        }
    }
}
