package CanvasNet {
    part def RenderingCanvas {
        doc /* Transform-aware surface wrapper owning a Matrix3x2 current transform and a Stack<Matrix3x2>, exposing Save/Restore, Translate/RotateDegrees, and FillPath/StrokePath that bake the current transform. Produces byte-identical output to direct PathFiller/PathStroker calls at the identity transform. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Rendering/Canvas.cs, src/DemaConsulting.CanvasNet/Rendering/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Rendering/CanvasTests.cs */
        comment designRef /* Design: docs/design/canvas-net/rendering/canvas.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/rendering/canvas.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/rendering/canvas.yaml */
    }
}
