package CanvasNet {
    part def PptxDocument {
        doc /* OOXML (Office Open XML) package parser and, in later phases, presentation-rendering codec. Phase 1a (this release) implements only the package layer: opens a .pptx file as a read-only ZipArchive, resolves [Content_Types].xml's default extension mappings and part-specific overrides, and resolves package-level (_rels/.rels) and per-part relationships (including relative-target resolution with ".." traversal). No presentation-specific content (ppt/presentation.xml, slides, slide layouts/masters) is parsed yet. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Pptx/PptxDocument.cs, src/DemaConsulting.CanvasNet.Pptx/PptxDocument.Package.cs, src/DemaConsulting.CanvasNet.Pptx/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Pptx.Tests/PptxDocumentTests.cs, test/DemaConsulting.CanvasNet.Pptx.Tests/PptxSystemIntegrationTests.cs */
        comment designRef /* Design: docs/design/canvas-net-pptx/pptx-document.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-pptx/pptx-document.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-pptx/pptx-document.yaml */
    }
}
