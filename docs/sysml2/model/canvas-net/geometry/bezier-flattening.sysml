package CanvasNet_BezierFlattening {
    private import SoftwareItemMetadata::*;

    part def BezierFlattening {
        doc /* Adaptive, tolerance-driven flattening of quadratic and cubic Bezier curves into polylines, writing into a caller-supplied output collection. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet/Geometry/BezierFlattening.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Tests/Geometry/BezierFlatteningTests.cs");
            designFiles = ("docs/design/canvas-net/geometry/bezier-flattening.md");
            verificationFiles = ("docs/verification/canvas-net/geometry/bezier-flattening.md");
            requirementFiles = ("docs/reqstream/canvas-net/geometry/bezier-flattening.yaml");
        }
    }
}
