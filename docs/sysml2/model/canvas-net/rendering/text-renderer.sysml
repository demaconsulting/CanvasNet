package CanvasNet {
    part def TextRenderer {
        doc /* Static text measurement and rendering unit exposing MeasureText and the DrawText extension on Canvas. Sums per-glyph advances and kerning; composes the Canvas current transform with per-glyph pen and unit-flip transforms; supports Left/Center/Right alignment. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Rendering/TextRenderer.cs, src/DemaConsulting.CanvasNet/Rendering/TextAlign.cs, src/DemaConsulting.CanvasNet/Rendering/TextMetrics.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Rendering/TextRendererTests.cs */
        comment designRef /* Design: docs/design/canvas-net/rendering/text-renderer.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/rendering/text-renderer.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/rendering/text-renderer.yaml */
    }
}
