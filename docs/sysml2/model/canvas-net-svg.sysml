package CanvasNet {
    part def CanvasNetSvgSystem {
        doc /* CanvasNetSvg is a decode/rasterize-only SVG codec system, distributed as the separate DemaConsulting.CanvasNet.Svg NuGet package, which depends on the core CanvasNet system's Canvas, Geometry, Drawing, and Fonts subsystems. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Svg.Tests/SvgSystemIntegrationTests.cs */
        comment designRef /* Design: docs/design/canvas-net-svg.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-svg.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-svg.yaml */
        comment /* CanvasNetSvg depends on the sibling CanvasNet system's Canvas subsystem (for the Surface pixel buffer SvgCodec rasterizes into), Geometry subsystem (for Path/Rect/BezierFlattening/SvgArcConverter), Drawing subsystem (for PathFiller/PathStroker/GradientPaint rasterization), and Fonts subsystem (for TrueTypeFont glyph-outline text rendering) - modeled below as non-owning `dependency` relationships to those existing subsystem part defs (not ordinary `part` usages, which would incorrectly model them as owned children of CanvasNetSvgSystem), since both systems live in this same git repository (though each is distributed as its own separate NuGet package - CanvasNetSvg is not folded into the CanvasNet package) rather than being third-party (OTS) or Shared Package dependencies (both of which are reserved for a package produced by a different repository). */

        part svgCodec : SvgCodec;

        dependency usesCanvas from CanvasNetSvgSystem to Canvas;
        dependency usesGeometry from CanvasNetSvgSystem to Geometry;
        dependency usesDrawing from CanvasNetSvgSystem to Drawing;
        dependency usesFonts from CanvasNetSvgSystem to Fonts;
    }
}
