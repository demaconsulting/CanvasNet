package CanvasNet {
    part def Canvas {
        doc /* Pixel-buffer primitives subsystem: the Surface unit with its SurfaceAdjustments (tone and color adjustments), SurfaceTransforms (flip, EXIF orientation, pad, trim, fit, flatten), SurfaceGeometry (rotation by any angle and skew estimation), SurfaceComparison (metrics, grid, and difference pictures), and SurfaceAnalysis (dominant colors, luma statistics, and pixel samples) parts, its supporting Rgba32 value type, and the public CssColor parser that turns CSS color text into Rgba32 values. */

        comment designRef /* Design: docs/design/canvas-net/canvas.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/canvas.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/canvas.yaml */

        part surface : Surface;
        part surfaceAdjustments : SurfaceAdjustments;
        part surfaceTransforms : SurfaceTransforms;
        part surfaceGeometry : SurfaceGeometry;
        part surfaceComparison : SurfaceComparison;
        part surfaceAnalysis : SurfaceAnalysis;
        part rgba32 : Rgba32;
        part cssColor : CssColor;
    }
}
