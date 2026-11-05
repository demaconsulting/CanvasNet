package CanvasNet {
    part def CanvasNetViewer {
        doc /* The static class that lists the built-in image sources (raster, SVG, PDF, PPTX, VSDX, OpenSCAD, and Vega-Lite, in a fixed order) and the extensions they claim, creates a new FolderImageResolver for one render (with optional extra sources placed after the built-in ones, so the later source wins), and renders any file by path or from a stream as a ViewerImage that owns the picture and carries the format, the notes, the printed text, and the embedded images. ViewerRenderOptions carries the requested size, the sandbox folder, the strictness switch, extra sources, and a cancellation token. A vector file is drawn at the requested size, a raster is fitted or scaled by the viewer. The class is static and stateless, and every call builds its own resolver. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Viewer/CanvasNetViewer.cs, src/DemaConsulting.CanvasNet.Viewer/ViewerRenderOptions.cs, src/DemaConsulting.CanvasNet.Viewer/ViewerImage.cs, src/DemaConsulting.CanvasNet.Viewer/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Viewer.Tests/CanvasNetViewerTests.cs, test/DemaConsulting.CanvasNet.Viewer.Tests/CanvasNetViewerSystemIntegrationTests.cs, test/DemaConsulting.CanvasNet.Viewer.Tests/ViewerFixtures.cs */
        comment designRef /* Design: docs/design/canvas-net-viewer/canvas-net-viewer.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-viewer/canvas-net-viewer.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-viewer/canvas-net-viewer.yaml */
    }
}
