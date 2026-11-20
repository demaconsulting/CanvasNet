package CanvasNet {
    part def CanvasNetViewerSystem {
        doc /* CanvasNetViewer is the aggregating system of the CanvasNet libraries, distributed as the separate DemaConsulting.CanvasNet.Viewer NuGet package. It lists the image sources of every format (the raster source of the core ImageSources subsystem and the sources of the Svg, Pdf, Pptx, Vsdx, Scad, VegaLite, Mermaid, and PlantUml packages) in one fixed list, creates a folder image resolver over them for one render so that an SVG document can embed every format with image href, and renders any file as a picture (by path or from a stream, at its natural or a requested size) with its notes, printed text, and embedded images. A library user adds image sources of their own (a new extension, an override of a built-in one, a live source claimed by a URI scheme) for one render; the later source wins, nothing is cached, and no source claims http or https unless the user supplies one. The system is also the library form of the canvasnet command-line tool: its Imaging subsystem probes a file of any supported format, renders it with an ordered pipeline of transformations into image files, inspects its colors and pixels, compares two images with thresholds, composes a contact sheet, and writes a copy of an SVG drawing whose links to models, charts, diagrams, and document pages are PNG pictures, and reports every expected failure as a ViewerException with a stable error code. Its Help subsystem carries the help cards about the formats (what to write in a file, style, composition, diagnostics, limits, what the host controls, and file access) as tag-filterable data that a host, often an agent toolkit, reads programmatically and extends with its own cards; the command-line tool prints the same cards with its command-line sections layered on them. A render of a Mermaid diagram or a Vega-Lite chart can also return the feature positions that a selection of the core Positions subsystem asks for (collected while the picture is drawn, at most 1,000, and mapped through the image pipeline or the fit into the picture that is returned or written), with the warning or, when strict, the error positions-unmatched for a selector that matched nothing. The command-line tool is a thin shell over these types; it has no plug-in mechanism. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Viewer.Tests/CanvasNetViewerSystemIntegrationTests.cs */
        comment designRef /* Design: docs/design/canvas-net-viewer.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-viewer.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-viewer.yaml */
        comment /* CanvasNetViewer references the core CanvasNet package (the ImageSources subsystem: IImageSource, FolderImageResolver, ImageSourceSet, and RasterImageSource; the Positions subsystem: ImagePosition, PositionSelection, and PositionCollector) and the Svg, Pdf, Pptx, Vsdx, Scad, VegaLite, Mermaid, and PlantUml packages for their image sources; Render3D is referenced explicitly (the OpenSCAD views of the Imaging subsystem) and Charts arrives transitively. It is a leaf of the dependency graph: no other package references it, and only the command-line tool consumes it. */

        part canvasNetViewer : CanvasNetViewer;
        part viewerException : ViewerException;
        part imaging : Imaging;
        part help : Help;

        dependency usesImageSources from CanvasNetViewerSystem to ImageSources;
        dependency usesPositions from CanvasNetViewerSystem to Positions;
        dependency usesSvg from CanvasNetViewerSystem to CanvasNetSvgSystem;
        dependency usesPdf from CanvasNetViewerSystem to CanvasNetPdfSystem;
        dependency usesPptx from CanvasNetViewerSystem to CanvasNetPptxSystem;
        dependency usesVsdx from CanvasNetViewerSystem to CanvasNetVsdxSystem;
        dependency usesScad from CanvasNetViewerSystem to CanvasNetScadSystem;
        dependency usesVegaLite from CanvasNetViewerSystem to CanvasNetVegaLiteSystem;
        dependency usesMermaid from CanvasNetViewerSystem to CanvasNetMermaidSystem;
        dependency usesPlantUml from CanvasNetViewerSystem to CanvasNetPlantUmlSystem;
        dependency usesRender3D from CanvasNetViewerSystem to CanvasNetRender3DSystem;
    }
}
