package CanvasNet {
    part def CanvasNetPdfSystem {
        doc /* CanvasNetPdf is a PDF page-rendering system, distributed as the separate DemaConsulting.CanvasNet.Pdf NuGet package, which depends on the core CanvasNet system's Canvas, Codecs, Geometry, and Drawing subsystems. Phase 1 parsed document structure (cross-references, trailer, page tree) and reported page count/size/rotation; Phase 2 adds a content-stream interpreter so Render tokenizes and executes path-construction/painting and graphics-state operators, painting real path geometry in solid opaque black only (no color/text/image/clipping support yet). */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Pdf.Tests/PdfSystemIntegrationTests.cs */
        comment designRef /* Design: docs/design/canvas-net-pdf.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-pdf.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-pdf.yaml */
        comment /* CanvasNetPdf depends on the sibling CanvasNet system's Canvas subsystem (for the Surface pixel buffer Render returns and paints onto), Codecs subsystem (for the shared Codecs.UnsupportedImageFeatureException type used for /Encrypt detection), Geometry subsystem (for PathBuilder/Path, accumulating each content stream's current path), and Drawing subsystem (for PathFiller/PathStroker/StrokeStyle, rasterizing each finished path onto the destination Surface) - modeled below as non-owning `dependency` relationships to those existing subsystem part defs (not ordinary `part` usages, which would incorrectly model them as owned children of CanvasNetPdfSystem), since both systems live in this same git repository (though each is distributed as its own separate NuGet package - CanvasNetPdf is not folded into the CanvasNet package) rather than being third-party (OTS) or Shared Package dependencies (both of which are reserved for a package produced by a different repository). The Geometry/Drawing dependencies are new as of Phase 2, added at the point they are actually first used by the content-stream interpreter; no Fonts dependency exists yet, since no text/font operator is implemented. */

        part pdfDocument : PdfDocument;

        dependency usesCanvas from CanvasNetPdfSystem to Canvas;
        dependency usesCodecs from CanvasNetPdfSystem to Codecs;
        dependency usesGeometry from CanvasNetPdfSystem to Geometry;
        dependency usesDrawing from CanvasNetPdfSystem to Drawing;
    }
}
