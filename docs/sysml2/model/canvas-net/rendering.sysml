package CanvasNet_RenderingSubsystem {
    private import SoftwareItemMetadata::*;
    public import CanvasNet_RenderingCanvas::*;
    public import CanvasNet_TextRenderer::*;
    public import CanvasNet_Shapes::*;
    public import CanvasNet_WordWrap::*;

    part def Rendering {
        doc /* Higher-level rendering subsystem that composes Canvas, Geometry, Drawing, and Fonts into a transform-aware surface API (Canvas), text rendering (TextRenderer), shape helpers (Shapes), and a font- and unit-independent word-wrap line packer (WordWrap). */

        metadata info : SoftwareInfo {
            testFiles = (
                "test/DemaConsulting.CanvasNet.Tests/Rendering/CanvasTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Rendering/TextRendererTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Rendering/ShapesTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Rendering/WordWrapTests.cs"
            );
            designFiles = ("docs/design/canvas-net/rendering.md");
            verificationFiles = ("docs/verification/canvas-net/rendering.md");
            requirementFiles = ("docs/reqstream/canvas-net/rendering.yaml");
        }

        part canvas : RenderingCanvas;
        part textRenderer : TextRenderer;
        part shapes : Shapes;
        part wordWrap : WordWrap;
    }
}
