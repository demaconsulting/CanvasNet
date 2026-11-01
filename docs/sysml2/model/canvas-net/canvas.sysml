package CanvasNet {
    part def Canvas {
        doc /* Pixel-buffer primitives subsystem: the Surface unit with its SurfaceAdjustments (tone and color adjustments) and SurfaceTransforms (flip, pad, trim, fit, flatten) parts, its supporting Rgba32 value type, and the public CssColor parser that turns CSS color text into Rgba32 values. */

        comment designRef /* Design: docs/design/canvas-net/canvas.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/canvas.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/canvas.yaml */

        part surface : Surface;
        part surfaceAdjustments : SurfaceAdjustments;
        part surfaceTransforms : SurfaceTransforms;
        part rgba32 : Rgba32;
        part cssColor : CssColor;
    }
}
