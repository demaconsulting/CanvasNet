package CanvasNetTool_PictureAttributes {
    private import SoftwareItemMetadata::*;

    part def PictureAttributes {
        doc /* Reads the attributes of a picture or a Mermaid, PlantUML, or MathML code block for the Pandoc filter, as a literal port of the Lua filter (version 16). It classifies a picture by the end of its name (Vega-Lite .vl.json and .vl, OpenSCAD .scad, Visio .vsdx, PowerPoint .pptx, PDF .pdf, Mermaid .mmd and .mermaid, PlantUML .puml, .plantuml, and .pu, MathML .mml, or a local .svg drawing) and leaves URLs and other names alone, gives a code block the kind of its exact class mermaid, plantuml, or mathml, and lists those picture suffixes and code-block classes (PictureSuffixes, BlockClasses), from which the shim's fast path is written. It checks the drawing attributes (size, scale, background, page, dpi, view, azimuth, elevation, projection, theme, define-NAME, and param-NAME) for their grammar, range, and kind, refusing a bad one with pandoc-filter-error and the message of the Lua filter, adds the defaults (a model at 1600x1200 on white with a budget of 2000000 triangles, page 1 of a paged document, an equation at scale 2), tells the engine when an equation the author did not size is shown at its natural width (NaturalWidthNeeded), and writes the normalized options sorted by their bytes in the supporting record PictureOptions together with the attributes that stay on the picture in their original order. For an SVG drawing it reads only scale. It names Mermaid code blocks for messages, splits off their caption, and writes numbers as C's %.15g. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.Tool/PandocFilter/PictureAttributes.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Tool.Tests/PandocFilter/PictureAttributesTests.cs");
            designFiles = ("docs/design/canvas-net-tool/pandoc-filter/picture-attributes.md");
            verificationFiles = ("docs/verification/canvas-net-tool/pandoc-filter/picture-attributes.md");
            requirementFiles = ("docs/reqstream/canvas-net-tool/pandoc-filter/picture-attributes.yaml");
        }
    }
}
