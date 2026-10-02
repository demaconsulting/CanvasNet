package CanvasNet {
    part def CanvasNetPptxSystem {
        doc /* CanvasNetPptx is a PowerPoint (.pptx) presentation-rendering system, distributed as the separate DemaConsulting.CanvasNet.Pptx NuGet package. This is a multi-phase, in-progress feature. Phase 1a (this release) implements only the underlying OOXML (Office Open XML) package layer: opening a .pptx file as a read-only ZipArchive, resolving [Content_Types].xml (default extension mappings and part-specific overrides), and resolving package-level (_rels/.rels) and per-part relationships, including relative-target resolution (for example "../slideLayouts/slideLayout1.xml"). No presentation-specific content (ppt/presentation.xml, slides, slide layouts/masters, or any rendering) is implemented yet. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Pptx.Tests/PptxSystemIntegrationTests.cs */
        comment designRef /* Design: docs/design/canvas-net-pptx.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-pptx.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-pptx.yaml */
        comment /* CanvasNetPptx depends on the sibling CanvasNet system (referenced via a ProjectReference from its own csproj, matching the architecture convention every package-bounded system in this repository follows), but as of Phase 1a no symbol from CanvasNet is actually used yet - the package layer relies solely on the BCL System.IO.Compression.ZipArchive and System.Xml.Linq types. No `dependency` relationship to any CanvasNet subsystem (Canvas, Codecs, Geometry, Drawing, Fonts) is therefore modeled yet, mirroring CanvasNetPdf's and CanvasNetSvg's own precedent of adding a `dependency` edge only at the phase that actually first uses the referenced subsystem (for example CanvasNetPdf's Fonts dependency was added only at its own Phase 4, not at Phase 1). A future phase that returns a Canvas.Surface from a rendering method will add the corresponding usesCanvas dependency edge at that time. */

        part pptxDocument : PptxDocument;
    }
}
