package CanvasNet_Rect {
    private import SoftwareItemMetadata::*;

    part def Rect {
        doc /* Axis-aligned bounding rectangle (position plus size), with union, intersection, matrix transformation, containment testing, and a union-identity Empty sentinel. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet/Geometry/Rect.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Tests/Geometry/RectTests.cs");
            designFiles = ("docs/design/canvas-net/geometry/rect.md");
            verificationFiles = ("docs/verification/canvas-net/geometry/rect.md");
            requirementFiles = ("docs/reqstream/canvas-net/geometry/rect.yaml");
        }
    }
}
