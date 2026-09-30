package CanvasNet {
    part def PdfDocument {
        doc /* PDF document parser and page-rendering codec: parses cross-references, trailer, and page tree, reports page count/size/rotation, and interprets each page's content stream (path construction/painting, graphics-state stack, device color operators, generalized stream filter pipeline, image XObjects, and (Phase 4) simple TrueType font resolution/encoding and text-object/positioning/showing operators, with (Phase 6) automatic system/bundled-Liberation font-fallback substitution for a non-embedded font) to render real path geometry, images, and glyph outlines onto the returned Surface in real device color. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Pdf/PdfDocument.cs, src/DemaConsulting.CanvasNet.Pdf/PdfDocument.Tokenizer.cs, src/DemaConsulting.CanvasNet.Pdf/PdfDocument.ObjectModel.cs, src/DemaConsulting.CanvasNet.Pdf/PdfDocument.Xref.cs, src/DemaConsulting.CanvasNet.Pdf/PdfDocument.PageTree.cs, src/DemaConsulting.CanvasNet.Pdf/PdfDocument.ContentStream.cs, src/DemaConsulting.CanvasNet.Pdf/PdfDocument.GraphicsState.cs, src/DemaConsulting.CanvasNet.Pdf/PdfDocument.PathOps.cs, src/DemaConsulting.CanvasNet.Pdf/PdfDocument.Color.cs, src/DemaConsulting.CanvasNet.Pdf/PdfDocument.Filters.cs, src/DemaConsulting.CanvasNet.Pdf/PdfDocument.Images.cs, src/DemaConsulting.CanvasNet.Pdf/PdfDocument.Fonts.cs, src/DemaConsulting.CanvasNet.Pdf/PdfDocument.FontFallback.cs, src/DemaConsulting.CanvasNet.Pdf/PdfDocument.Text.cs, src/DemaConsulting.CanvasNet.Pdf/PdfPageInfo.cs, src/DemaConsulting.CanvasNet.Pdf/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Pdf.Tests/PdfDocumentTests.cs, test/DemaConsulting.CanvasNet.Pdf.Tests/PdfSystemIntegrationTests.cs */
        comment designRef /* Design: docs/design/canvas-net-pdf/pdf-document.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-pdf/pdf-document.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-pdf/pdf-document.yaml */
    }
}
