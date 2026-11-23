package CanvasNet_Shapes {
    private import SoftwareItemMetadata::*;

    part def Shapes {
        doc /* Static convenience shape helpers exposing FillRect/StrokeRect/FillRoundRect/StrokeRoundRect/FillCircle/StrokeCircle extension methods on Canvas. Each helper builds its path via the corresponding Geometry.Path factory and honors the Canvas current transform. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet/Rendering/Shapes.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Tests/Rendering/ShapesTests.cs");
            designFiles = ("docs/design/canvas-net/rendering/shapes.md");
            verificationFiles = ("docs/verification/canvas-net/rendering/shapes.md");
            requirementFiles = ("docs/reqstream/canvas-net/rendering/shapes.yaml");
        }
    }
}
