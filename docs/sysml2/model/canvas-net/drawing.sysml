package CanvasNet {
    part def Drawing {
        doc /* Antialiased scanline-coverage fill rasterizer subsystem for closed Geometry.Path geometry with solid-color paint (PathFiller, covering FillRule and the internal EdgeFlattener/ScanlineRasterizer helpers inline). Consumes Canvas.Surface and Geometry.Path; neither Canvas nor Geometry depends on Drawing. Strokes/gradients/fonts reserved for later phases. */

        comment designRef /* Design: docs/design/canvas-net/drawing.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/drawing.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/drawing.yaml */

        part pathFiller : PathFiller;
    }
}
