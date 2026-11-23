package CanvasNet_RenderingCanvas {
    private import SoftwareItemMetadata::*;

    part def RenderingCanvas {
        doc /* Transform-aware surface wrapper owning a Matrix3x2 current transform and a Stack<Matrix3x2>, exposing Save/Restore, Translate/RotateDegrees, and FillPath/StrokePath that bake the current transform. Produces byte-identical output to direct PathFiller/PathStroker calls at the identity transform. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet/Rendering/Canvas.cs",
                "src/DemaConsulting.CanvasNet/Rendering/NamespaceDoc.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.Tests/Rendering/CanvasTests.cs");
            designFiles = ("docs/design/canvas-net/rendering/canvas.md");
            verificationFiles = ("docs/verification/canvas-net/rendering/canvas.md");
            requirementFiles = ("docs/reqstream/canvas-net/rendering/canvas.yaml");
        }
    }
}
