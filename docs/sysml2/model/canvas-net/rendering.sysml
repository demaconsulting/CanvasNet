package CanvasNet {
    part def Rendering {
        doc /* Higher-level rendering subsystem that composes Canvas, Geometry, Drawing, and Fonts into a transform-aware surface API (Canvas), text rendering (TextRenderer), and shape helpers (Shapes). */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Rendering/CanvasTests.cs, test/DemaConsulting.CanvasNet.Tests/Rendering/TextRendererTests.cs, test/DemaConsulting.CanvasNet.Tests/Rendering/ShapesTests.cs */
        comment designRef /* Design: docs/design/canvas-net/rendering.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/rendering.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/rendering.yaml */

        part canvas : RenderingCanvas;
        part textRenderer : TextRenderer;
        part shapes : Shapes;
    }
}
