package CanvasNet {
    part def PictureAttributes {
        doc /* Reads the attributes of a picture or a Mermaid code block for the Pandoc filter, as a literal port of the Lua filter (version 16). It classifies a picture by the end of its name (Vega-Lite .vl.json and .vl, OpenSCAD .scad, Visio .vsdx, PowerPoint .pptx, PDF .pdf, Mermaid .mmd and .mermaid, or a local .svg drawing) and leaves URLs and other names alone. It checks the drawing attributes (size, scale, background, page, dpi, view, azimuth, elevation, projection, theme, define-NAME, and param-NAME) for their grammar, range, and kind, refusing a bad one with pandoc-filter-error and the message of the Lua filter, adds the defaults (a model at 1600x1200 on white with a budget of 2000000 triangles, page 1 of a paged document), and writes the normalized options sorted by their bytes in the supporting record PictureOptions together with the attributes that stay on the picture in their original order. For an SVG drawing it reads only scale. It names Mermaid code blocks for messages, splits off their caption, and writes numbers as C's %.15g. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/PandocFilter/PictureAttributes.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/PandocFilter/PictureAttributesTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/pandoc-filter/picture-attributes.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/pandoc-filter/picture-attributes.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/pandoc-filter/picture-attributes.yaml */
    }
}
