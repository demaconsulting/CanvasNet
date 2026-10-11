package CanvasNet {
    part def TilePaint {
        doc /* Public TilePaint type (a pre-rendered one-cell tile Surface plus a device-space Transform and pattern-space XStep/YStep pitch) and the internal TilePaintEvaluator helper that resolves a tiled pattern's color per pixel from the wrapped-around tile (exact pixel lookup at 1:1 scale, wrapped bilinear sampling when the tile is magnified, and an area-averaged prefiltered copy when it is minified, via SurfaceResampler); consumed by PathFiller's tile-paint Fill overload. Mirrors GradientPaint's own public-type-plus-internal-evaluator shape; added alongside CanvasNetPdf's /Pattern color-space tiling-pattern support. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Drawing/TilePaint.cs, src/DemaConsulting.CanvasNet/Drawing/TilePaintEvaluator.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Drawing/TilePaintTests.cs, test/DemaConsulting.CanvasNet.Tests/Drawing/TilePaintEvaluatorTests.cs */
        comment designRef /* Design: docs/design/canvas-net/drawing/tile-paint.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/drawing/tile-paint.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/drawing/tile-paint.yaml */
    }
}
