package CanvasNet {
    part def Canvas {
        doc /* Pixel-buffer primitives subsystem: the Surface unit and its supporting Rgba32 value type. */

        comment designRef /* Design: docs/design/canvas-net/canvas.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/canvas.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/canvas.yaml */

        part surface : Surface;
        part rgba32 : Rgba32;
    }
}
