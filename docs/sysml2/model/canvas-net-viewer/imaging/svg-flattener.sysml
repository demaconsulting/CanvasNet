package CanvasNetViewer_SvgFlattener {
    private import SoftwareItemMetadata::*;

    part def SvgFlattener {
        doc /* The flatten operation of the library: given the probed facts of an SVG drawing and a request, it reads the drawing as UTF-8 text, draws it once with a recording image resolver over the resolver of the render operation (links relative to the folder of the drawing, under FlattenRequest.FileAccess, AllowAll by default, null refused; a link the policy denies is not flattened and is the warning image-access-denied, or with Strict the error file-access-denied; the limits of 64 pictures, 4 levels, cycles, fragments, and the pixel budget apply), and draws every link the drawing shows that names a Mermaid or PlantUML diagram, a MathML equation, an OpenSCAD model, a Vega-Lite chart, a page of a PDF, PPTX, or VSDX document, or an SVG file that links one at any depth, at its placement size through the viewBox and every transform times the scale of the request, keeping each as a PNG picture. It finds the value of the effective link attribute of every image element (href, else xlink:href) in the raw text with the hardened XML reader, and writes a copy in which only those values are PNG data URIs (and, when the copy is in another folder, the relative links that are left are rebased), every other byte kept, staged next to the output and moved into place. It writes nothing when nothing is replaced, reports a link to such a file that the drawing never shows as the warning svg-flatten-image-not-drawn, applies the strict policy of render, and refuses a copy over the output limit with size-limit. The unit includes the FlattenRequest, FlattenedImage, RebasedLink, and FlattenResult records. It is the library form of the flatten command of the canvasnet tool. Position bindings of the drawing are baked from the captured pictures (SvgFlattener.Bindings.cs): the recorder draws each bound link once with the positions its bindings select, a binding pass of the SVG library over captured-size blank surfaces with the recorded positions resolves every binding through its own image placement without drawing any linked file again, the resolved numbers are written, the consumed cn attributes and an unused namespace declaration are removed, FlattenResult counts ResolvedBindings and UnresolvedBindings, and with Strict a binding that fails is svg-binding-failed before anything is written. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.Viewer/Imaging/SvgFlattener.cs",
                "src/DemaConsulting.CanvasNet.Viewer/Imaging/SvgFlattener.Links.cs",
                "src/DemaConsulting.CanvasNet.Viewer/Imaging/SvgFlattener.Recorder.cs",
                "src/DemaConsulting.CanvasNet.Viewer/Imaging/SvgFlattener.Bindings.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.Viewer.Tests/Imaging/SvgFlattenerTests.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/Imaging/SvgFlattenerLinkTests.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/Imaging/SvgFlattenerBindingTests.cs"
            );
            designFiles = ("docs/design/canvas-net-viewer/imaging/svg-flattener.md");
            verificationFiles = ("docs/verification/canvas-net-viewer/imaging/svg-flattener.md");
            requirementFiles = ("docs/reqstream/canvas-net-viewer/imaging/svg-flattener.yaml");
        }
    }
}
