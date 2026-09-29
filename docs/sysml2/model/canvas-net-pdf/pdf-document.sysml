package CanvasNet {
    part def PdfDocument {
        doc /* PDF document parser and page-rendering codec: parses cross-references, trailer, and page tree, and reports page count/size/rotation. Phase 1: Render returns a correctly sized but blank Surface. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Pdf/PdfDocument.cs, src/DemaConsulting.CanvasNet.Pdf/PdfDocument.Tokenizer.cs, src/DemaConsulting.CanvasNet.Pdf/PdfDocument.ObjectModel.cs, src/DemaConsulting.CanvasNet.Pdf/PdfDocument.Xref.cs, src/DemaConsulting.CanvasNet.Pdf/PdfDocument.PageTree.cs, src/DemaConsulting.CanvasNet.Pdf/PdfPageInfo.cs, src/DemaConsulting.CanvasNet.Pdf/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Pdf.Tests/PdfDocumentTests.cs, test/DemaConsulting.CanvasNet.Pdf.Tests/PdfSystemIntegrationTests.cs */
        comment designRef /* Design: docs/design/canvas-net-pdf/pdf-document.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-pdf/pdf-document.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-pdf/pdf-document.yaml */
    }
}
