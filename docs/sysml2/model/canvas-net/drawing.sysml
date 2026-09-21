package CanvasNet {
    part def Drawing {
        doc /* Antialiased solid-color vector-rendering subsystem for Geometry.Path geometry. Provides both PathFiller (direct path filling) and PathStroker (stroke-to-fill conversion with standard cap/join/dash styling), consuming Canvas.Surface and Geometry.Path; neither Canvas nor Geometry depends on Drawing. Gradients/fonts remain reserved for later phases. */

        comment designRef /* Design: docs/design/canvas-net/drawing.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/drawing.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/drawing.yaml */

        part pathFiller : PathFiller;
        part pathStroker : PathStroker;
    }
}
