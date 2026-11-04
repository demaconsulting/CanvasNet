package CanvasNet {
    part def CanvasNetViewerSystem {
        doc /* CanvasNetViewer is the aggregating system of the CanvasNet libraries, distributed as the separate DemaConsulting.CanvasNet.Viewer NuGet package. It lists the image sources of every format (the raster source of the core ImageSources subsystem and the sources of the Svg, Pdf, Pptx, Vsdx, Scad, and VegaLite packages) in one fixed list, creates a folder image resolver over them for one render so that an SVG document can embed every format with image href, and renders any file as a picture (by path or from a stream, at its natural or a requested size) with its notes, printed text, and embedded images. A library user adds image sources of their own (a new extension, an override of a built-in one, a live source claimed by a URI scheme) for one render; the later source wins, nothing is cached, and no source claims http or https unless the user supplies one. The command-line tool wires the SVG case of this system in; the tool has no plug-in mechanism. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Viewer.Tests/CanvasNetViewerSystemIntegrationTests.cs */
        comment designRef /* Design: docs/design/canvas-net-viewer.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-viewer.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-viewer.yaml */
        comment /* CanvasNetViewer references the core CanvasNet package (the ImageSources subsystem: IImageSource, FolderImageResolver, ImageSourceSet, and RasterImageSource) and the Svg, Pdf, Pptx, Vsdx, Scad, and VegaLite packages for their image sources; Render3D and Charts arrive transitively. It is a leaf of the dependency graph: no other package references it, and only the command-line tool consumes it. */

        part canvasNetViewer : CanvasNetViewer;

        dependency usesImageSources from CanvasNetViewerSystem to ImageSources;
        dependency usesSvg from CanvasNetViewerSystem to CanvasNetSvgSystem;
        dependency usesPdf from CanvasNetViewerSystem to CanvasNetPdfSystem;
        dependency usesPptx from CanvasNetViewerSystem to CanvasNetPptxSystem;
        dependency usesVsdx from CanvasNetViewerSystem to CanvasNetVsdxSystem;
        dependency usesScad from CanvasNetViewerSystem to CanvasNetScadSystem;
        dependency usesVegaLite from CanvasNetViewerSystem to CanvasNetVegaLiteSystem;
    }
}
