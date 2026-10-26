package CanvasNet {
    part def CanvasNetToolSystem {
        doc /* CanvasNetTool is the canvasnet command-line tool, shipped as the separate DemaConsulting.CanvasNet.Tool dotnet tool package (command canvasnet). It lets people, scripts, and AI agents inspect, render, transform, compare, and compose images and documents using the CanvasNet libraries, with a stable machine-readable error contract. It follows the DEMA Consulting tool template: a Program entry point with the template's error policy (expected failures become a concise message and exit code 1, unexpected exceptions are reported and re-thrown), a Context that owns the command line and the output channels, a self-validation mode, and safe path helpers; it adds a Commands subsystem that dispatches the first non-option argument to a registered command, an Imaging subsystem that turns files into pixels and pixels into files, an ArgumentReader that is the command-line grammar shared by the commands, and a ResultWriter that writes the deterministic, versioned JSON results. This release delivers the foundation (the template core and the command registry), the command-line grammar, the JSON result contract, and the commands formats, info, render (with its alias convert), the verbs crop, resize, rotate, flip, and pad, compare (exact error and similarity metrics, a grid of per-cell errors, difference pictures, and threshold gating that fails with exit code 1), and montage (a labeled contact sheet of images and document pages) (plus the built-in help). They read PNG, JPEG, BMP, TIFF, GIF, JPEG 2000, SVG, PDF (pages), PowerPoint PPTX (slides), Visio VSDX (pages), and OpenSCAD SCAD (rendered model views) input, apply an ordered pipeline of crop, resize, fit, rotate, flip, and pad operations, and write PNG, JPEG, BMP, or TIFF output. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/CanvasNetToolTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool.yaml */
        comment /* CanvasNetTool uses the CanvasNet libraries (the core Canvas, Codecs, Fonts, and Rendering subsystems and the sibling Svg, Pdf, Pptx, Vsdx, Render3D, and Scad systems) through ProjectReferences that are private assets of the tool package: the library assemblies are bundled into the tool package rather than declared as NuGet dependencies, so installing the tool never requires the sibling packages. They are modeled below as non-owning dependency relationships, since all systems live in this same git repository rather than being third-party (OTS) or Shared Package dependencies. Charts is used transitively through Pptx. The tool's only OTS runtime dependency is DemaConsulting.TestResults, documented in docs/design/ots/test-results.md. */

        part program : Program;
        part context : Context;
        part argumentReader : ArgumentReader;
        part resultWriter : ResultWriter;
        part validation : Validation;
        part pathHelpers : PathHelpers;
        part commands : Commands;
        part imaging : Imaging;

        dependency usesCanvas from CanvasNetToolSystem to Canvas;
        dependency usesCodecs from CanvasNetToolSystem to Codecs;
        dependency usesFonts from CanvasNetToolSystem to Fonts;
        dependency usesRendering from CanvasNetToolSystem to Rendering;
        dependency usesSvg from CanvasNetToolSystem to CanvasNetSvgSystem;
        dependency usesPdf from CanvasNetToolSystem to CanvasNetPdfSystem;
        dependency usesPptx from CanvasNetToolSystem to CanvasNetPptxSystem;
        dependency usesVsdx from CanvasNetToolSystem to CanvasNetVsdxSystem;
        dependency usesRender3D from CanvasNetToolSystem to CanvasNetRender3DSystem;
        dependency usesScad from CanvasNetToolSystem to CanvasNetScadSystem;
    }
}
