package CanvasNetViewer_CanvasNetViewer {
    private import SoftwareItemMetadata::*;

    part def CanvasNetViewer {
        doc /* The static class that lists the built-in image sources (raster, SVG, PDF, PPTX, VSDX, OpenSCAD, Vega-Lite, Mermaid, PlantUML, and MathML, in a fixed order; adapters over the SourceLoader of the Imaging subsystem, so a linked file and a rendered file are drawn by the same code as the canvasnet tool) and the extensions they claim, creates a new FolderImageResolver for one render (with optional extra sources placed after the built-in ones, so the later source wins), and renders any file by path or from a stream as a ViewerImage that owns the picture and carries the format, the page drawn and the page count, the notes (with detail, location, and origin), the printed text, the anchors of a model, and the embedded images. A document link chooses its page, slide, or model view, a diagram link its theme, and an equation link its display mode, with the fragment of the link (page=N, slide=N, view=name, azimuth=degrees, elevation=degrees, projection=ortho|perspective, theme=name, display=block|inline; page 1 without one). ViewerRenderOptions carries the requested size, the page, the resolution, the background, the model options, the base folder of the links (a base, not a boundary), the file-access policy (FileAccess, AllowAll by default like Pandoc, null refused, passed to the resolver and so to every nested picture, model include, and chart data file), the strictness switch, extra sources, the resource limits of a chart, a diagram, and an equation (VegaLiteLimits, MermaidLimits, PlantUmlLimits, and MathMlLimits; null keeps the library defaults; applied to the probe and the render of the file and of every chart, diagram, and equation it embeds, at any level), and a cancellation token. The file a caller renders by path may lie anywhere (also outside Folder or behind a link) and is never judged by the policy; a non-regular, too large, or unreadable file is a FileReferenceException. A link the policy denies is the warning image-access-denied, and with ThrowOnUnsupportedFeature the ViewerException file-access-denied (ErrorCodes.FileAccessDenied). A vector file is drawn at the requested size, a raster is fitted or scaled by the viewer. The class is static and stateless, and every call builds its own resolver. The built-in sources declare the prefixes def. (a model) and param. (a chart) and set the variables of a model and the parameters of a chart from the fragment of a link, and ViewerRenderOptions.VegaLite (ChartOptions) sets the parameters of a chart that is rendered directly, so the command line, the library and an embedded file draw a parameterized chart the same way. ViewerRenderOptions.Positions (a PositionSelection) asks for the feature positions of the file rendered directly (a Mermaid diagram or a Vega-Lite chart; LoaderImageSource passes it through DirectRenderOptions and never to a picture that file embeds): ViewerImage.Positions returns them mapped through the fit into the requested box, ViewerImage.PositionsTruncated tells whether matches were left out, a selector that matched nothing is a positions-unmatched note, and under ThrowOnUnsupportedFeature it is the ViewerException positions-unmatched. LoaderImageSource answers ImageSourceRequest.Positions for embedded Mermaid diagrams, Vega-Lite charts, and OpenSCAD anchors (kind anchor), so SVG position bindings resolve through the facade; a strict render fails with svg-binding-failed for one that is not resolved. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.Viewer/CanvasNetViewer.cs",
                "src/DemaConsulting.CanvasNet.Viewer/LoaderImageSource.cs",
                "src/DemaConsulting.CanvasNet.Viewer/ViewerRenderOptions.cs",
                "src/DemaConsulting.CanvasNet.Viewer/ViewerImage.cs",
                "src/DemaConsulting.CanvasNet.Viewer/NamespaceDoc.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.Viewer.Tests/CanvasNetViewerTests.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/CanvasNetViewerSystemIntegrationTests.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/CanvasNetViewerFragmentTests.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/CanvasNetViewerParameterFragmentTests.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/CanvasNetViewerRenderOptionsTests.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/CanvasNetViewerPositionTests.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/ViewerFixtures.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/CanvasNetViewerBindingTests.cs"
            );
            designFiles = ("docs/design/canvas-net-viewer/canvas-net-viewer.md");
            verificationFiles = ("docs/verification/canvas-net-viewer/canvas-net-viewer.md");
            requirementFiles = ("docs/reqstream/canvas-net-viewer/canvas-net-viewer.yaml");
        }
    }
}
