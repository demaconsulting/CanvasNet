package CanvasNet {
    part def Drawing {
        doc /* Antialiased vector-rendering subsystem for Geometry.Path geometry, supporting solid-color and gradient paint. Provides PathFiller (direct path filling, solid color or GradientPaint), PathStroker (stroke-to-fill conversion with standard cap/join/dash styling), and GradientPaint (linear/radial gradient definition and evaluation), consuming Canvas.Surface and Geometry.Path; neither Canvas nor Geometry depends on Drawing, although callers may combine Drawing with Fonts-produced glyph paths. */

        comment designRef /* Design: docs/design/canvas-net/drawing.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/drawing.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/drawing.yaml */

        part pathFiller : PathFiller;
        part pathStroker : PathStroker;
        part gradientPaint : GradientPaint;
    }
}
