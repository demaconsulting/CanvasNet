package CanvasNet {
    part def CanvasNetPdfSystem {
        doc /* CanvasNetPdf is a PDF page-rendering system, distributed as the separate DemaConsulting.CanvasNet.Pdf NuGet package, which depends on the core CanvasNet system's Canvas, Codecs, Geometry, Drawing, and (as of Phase 4) Fonts subsystems. Phase 1 parsed document structure (cross-references, trailer, page tree) and reported page count/size/rotation; Phase 2 added a content-stream interpreter so Render tokenizes and executes path-construction/painting and graphics-state operators (solid opaque black only); Phase 3 added real device color, a generalized stream-filter pipeline, and image XObjects; Phase 4 adds real text/font rendering, resolving a simple embedded TrueType font from a page's /Resources/Font dictionary and painting shown glyph outlines through the composed text-rendering matrix. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Pdf.Tests/PdfSystemIntegrationTests.cs */
        comment designRef /* Design: docs/design/canvas-net-pdf.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-pdf.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-pdf.yaml */
        comment /* CanvasNetPdf depends on the sibling CanvasNet system's Canvas subsystem (for the Surface pixel buffer Render returns and paints onto), Codecs subsystem (for the shared Codecs.UnsupportedImageFeatureException type used for /Encrypt detection and every other fail-closed case), Geometry subsystem (for PathBuilder/Path, accumulating each content stream's current path, including every painted glyph outline), Drawing subsystem (for PathFiller/PathStroker/StrokeStyle, rasterizing each finished path onto the destination Surface), and, as of Phase 4, Fonts subsystem (for TrueTypeFont, loading each embedded /FontFile2 byte stream and resolving each shown codepoint to a glyph index/outline/advance width) - modeled below as non-owning `dependency` relationships to those existing subsystem part defs (not ordinary `part` usages, which would incorrectly model them as owned children of CanvasNetPdfSystem), since both systems live in this same git repository (though each is distributed as its own separate NuGet package - CanvasNetPdf is not folded into the CanvasNet package) rather than being third-party (OTS) or Shared Package dependencies (both of which are reserved for a package produced by a different repository). The Geometry/Drawing dependencies were added as of Phase 2, at the point they were actually first used by the content-stream interpreter; the Fonts dependency is new as of Phase 4, added at the point it is actually first used by the text-rendering operators. Encrypted documents using a supported security handler/scheme - the standard security handler's RC4 (/V 1/2), AES-128 (/V 4/AESV2), or AES-256 with the R5 or R6 hardened-hash key derivation (/V 5/R 5 or R 6/AESV3) - are decrypted and rendered successfully once authenticated with the correct (or, where permitted, empty/missing) user or owner password; fail-closed Codecs.UnsupportedImageFeatureException behavior applies only to unsupported encryption shapes (a non-/Standard filter, a crypt filter other than /StdCF, an /R other than 5 or 6, or any other unsupported /V/R combination) or to an incorrect or missing required password. */

        part pdfDocument : PdfDocument;

        dependency usesCanvas from CanvasNetPdfSystem to Canvas;
        dependency usesCodecs from CanvasNetPdfSystem to Codecs;
        dependency usesGeometry from CanvasNetPdfSystem to Geometry;
        dependency usesDrawing from CanvasNetPdfSystem to Drawing;
        dependency usesFonts from CanvasNetPdfSystem to Fonts;
    }
}
