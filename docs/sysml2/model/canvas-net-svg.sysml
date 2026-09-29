package CanvasNet {
    part def CanvasNetSvgSystem {
        doc /* CanvasNetSvg is a decode/rasterize-only SVG codec system, distributed as the separate DemaConsulting.CanvasNet.Svg NuGet package, which depends on the core CanvasNet system's Canvas, Geometry, Drawing, and Fonts subsystems. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Svg.Tests/SvgSystemIntegrationTests.cs */
        comment designRef /* Design: docs/design/canvas-net-svg.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-svg.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-svg.yaml */
        comment /* CanvasNetSvg depends on the sibling CanvasNet system's Canvas subsystem (for the Surface pixel buffer SvgCodec rasterizes into), Geometry subsystem (for Path/Rect/BezierFlattening/SvgArcConverter), Drawing subsystem (for PathFiller/PathStroker/GradientPaint rasterization), and Fonts subsystem (for TrueTypeFont glyph-outline text rendering) - modeled below as ordinary part usages of those existing subsystem part defs, since both systems live in this same repository/package rather than being third-party (OTS) or Shared Package dependencies. */

        part svgCodec : SvgCodec;

        part usesCanvas : Canvas;
        part usesGeometry : Geometry;
        part usesDrawing : Drawing;
        part usesFonts : Fonts;
    }
}
