package CanvasNet {
    part def CanvasNetPdfSystem {
        doc /* CanvasNetPdf is a PDF page-rendering system, distributed as the separate DemaConsulting.CanvasNet.Pdf NuGet package, which depends on the core CanvasNet system's Canvas and Codecs subsystems. Phase 1 scope: parses document structure (cross-references, trailer, page tree) and reports page count/size/rotation; Render returns a correctly sized but blank Surface until a later phase adds content-stream interpretation. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Pdf.Tests/PdfSystemIntegrationTests.cs */
        comment designRef /* Design: docs/design/canvas-net-pdf.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-pdf.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-pdf.yaml */
        comment /* CanvasNetPdf depends on the sibling CanvasNet system's Canvas subsystem (for the Surface pixel buffer Render returns) and Codecs subsystem (for the shared Codecs.UnsupportedImageFeatureException type used for /Encrypt detection) - modeled below as non-owning `dependency` relationships to those existing subsystem part defs (not ordinary `part` usages, which would incorrectly model them as owned children of CanvasNetPdfSystem), since both systems live in this same git repository (though each is distributed as its own separate NuGet package - CanvasNetPdf is not folded into the CanvasNet package) rather than being third-party (OTS) or Shared Package dependencies (both of which are reserved for a package produced by a different repository). Phase 1 omits usesGeometry/usesDrawing/usesFonts dependencies since no Phase 1 file yet calls into those subsystems - they will be added when a later phase actually begins interpreting page content streams. */

        part pdfDocument : PdfDocument;

        dependency usesCanvas from CanvasNetPdfSystem to Canvas;
        dependency usesCodecs from CanvasNetPdfSystem to Codecs;
    }
}
