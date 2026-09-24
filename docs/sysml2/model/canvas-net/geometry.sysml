package CanvasNet {
    part def Geometry {
        doc /* Vector-geometry primitives subsystem: axis-aligned bounds (Rect), an immutable vector path and its builder (Path/PathBuilder, covering Subpath/PathCommand/PathCommandType inline), adaptive Bezier flattening (BezierFlattening), and SVG-style elliptical arc conversion (SvgArcConverter). Distinct from the Drawing subsystem, which rasterizes this geometry onto a Canvas.Surface. */

        comment designRef /* Design: docs/design/canvas-net/geometry.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/geometry.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/geometry.yaml */

        part rect : Rect;
        part path : Path;
        part bezierFlattening : BezierFlattening;
        part svgArcConverter : SvgArcConverter;
        part cornerRoundEffect : CornerRoundEffect;
    }
}
