package CanvasNetSvg {
    private import SoftwareItemMetadata::*;
    public import CanvasNetSvg_SvgCodec::*;

    part def CanvasNetSvgSystem {
        doc /* CanvasNetSvg is a decode/rasterize-only SVG codec system, distributed as the separate DemaConsulting.CanvasNet.Svg NuGet package, which depends on the core CanvasNet system's Canvas, Geometry, Drawing, Fonts, and Codecs subsystems. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasNet.Svg.Tests/SvgSystemIntegrationTests.cs");
            designFiles = ("docs/design/canvas-net-svg.md");
            verificationFiles = ("docs/verification/canvas-net-svg.md");
            requirementFiles = ("docs/reqstream/canvas-net-svg.yaml");
        }

        comment /* CanvasNetSvg depends on the sibling CanvasNet system's Canvas subsystem (for the Surface pixel buffer SvgCodec rasterizes into), Geometry subsystem (for Path/Rect/BezierFlattening/SvgArcConverter), Drawing subsystem (for PathFiller/PathStroker/GradientPaint rasterization), Fonts subsystem (for TrueTypeFont glyph-outline text rendering, and the SystemFontCatalog installed-font lookup and bundled Liberation fallback fonts that resolve a text element's font when the caller registers none), and Codecs subsystem (for the shared Codecs.ImageInfo record struct returned by SvgCodec.GetInfo) - modeled below as non-owning `dependency` relationships to those existing subsystem part defs (not ordinary `part` usages, which would incorrectly model them as owned children of CanvasNetSvgSystem), since both systems live in this same git repository (though each is distributed as its own separate NuGet package - CanvasNetSvg is not folded into the CanvasNet package) rather than being third-party (OTS) or Shared Package dependencies (both of which are reserved for a package produced by a different repository). */

        part svgCodec : SvgCodec;

        dependency usesCanvas from CanvasNetSvgSystem to CanvasNet_CanvasSubsystem::Canvas;
        dependency usesGeometry from CanvasNetSvgSystem to CanvasNet_GeometrySubsystem::Geometry;
        dependency usesDrawing from CanvasNetSvgSystem to CanvasNet_DrawingSubsystem::Drawing;
        dependency usesFonts from CanvasNetSvgSystem to CanvasNet_FontsSubsystem::Fonts;
        dependency usesCodecs from CanvasNetSvgSystem to CanvasNet_CodecsSubsystem::Codecs;
        dependency usesPackaging from CanvasNetSvgSystem to CanvasNet_PackagingSubsystem::Packaging;
        dependency usesImageSources from CanvasNetSvgSystem to CanvasNet_ImageSourcesSubsystem::ImageSources;
    }
}
