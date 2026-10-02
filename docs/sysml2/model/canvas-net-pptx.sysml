package CanvasNet {
    part def CanvasNetPptxSystem {
        doc /* CanvasNetPptx is a PowerPoint (.pptx) presentation-rendering system, distributed as the separate DemaConsulting.CanvasNet.Pptx NuGet package. This is a multi-phase, in-progress feature. Phase 1a implemented the underlying OOXML (Office Open XML) package layer: opening a .pptx file as a read-only ZipArchive, resolving [Content_Types].xml (default extension mappings and part-specific overrides), and resolving package-level (_rels/.rels) and per-part relationships, including relative-target resolution (for example "../slideLayouts/slideLayout1.xml"). Phase 1b (this release) adds the presentation/theme/master/layout/slide model and the placeholder property-inheritance resolver: ppt/presentation.xml's declared slide size and ordered slide list, each slide master's theme (color/font scheme, resolved into DemaConsulting.CanvasNet.Canvas.Rgba32 values), structural placeholder-shape parsing for masters/layouts/slides, and the verified ECMA-376 placeholder matching algorithm. No shape geometry/paint rendering, non-placeholder shape parsing, font loading, or rendering is implemented yet. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Pptx.Tests/PptxSystemIntegrationTests.cs */
        comment designRef /* Design: docs/design/canvas-net-pptx.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-pptx.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-pptx.yaml */
        comment /* CanvasNetPptx depends on the sibling CanvasNet system (referenced via a ProjectReference from its own csproj, matching the architecture convention every package-bounded system in this repository follows). As of Phase 1b, CanvasNetPptx uses the Canvas subsystem's Rgba32 type to represent a theme's resolved color scheme - modeled below as a non-owning `dependency` relationship to the existing Canvas subsystem part def (not an ordinary `part` usage, which would incorrectly model it as an owned child of CanvasNetPptxSystem), mirroring CanvasNetPdf's and CanvasNetSvg's own precedent of adding a `dependency` edge only at the phase that actually first uses the referenced subsystem (for example CanvasNetPdf's Fonts dependency was added only at its own Phase 4, not at Phase 1). No other CanvasNet subsystem (Codecs, Geometry, Drawing, Fonts) is used yet; a future phase that actually renders a slide onto a Canvas.Surface may add further dependency edges at that time. */

        part pptxDocument : PptxDocument;

        dependency usesCanvas from CanvasNetPptxSystem to Canvas;
    }
}
