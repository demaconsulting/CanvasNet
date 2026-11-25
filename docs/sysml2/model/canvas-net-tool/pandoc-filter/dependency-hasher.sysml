package CanvasNetTool_DependencyHasher {
    private import SoftwareItemMetadata::*;

    part def DependencyHasher {
        doc /* Collects every file a picture of the Pandoc filter is drawn from as the parts of its cache key, so that a change to any of them draws the picture again; a literal port of the dependency walks of the Lua filter (version 16). Each part is the file's path from the picture's folder, a line feed, and the file's bytes; a missing file an SVG drawing links is the part missing and its path. It follows the include and use statements of an OpenSCAD model, the data URLs, image marks, url channels, inline rows, and nested charts of a Vega-Lite chart, the img links of a Mermaid diagram, the !include files of a PlantUML diagram (a MathML equation is hashed as its own file only, since it references no other file), and the image links of an SVG drawing found by a scanner that skips comments, CDATA, processing instructions, and a DOCTYPE and honors the XLink namespace, and it reports whether a drawing is composite (links a model, chart, diagram, or document page, directly or through a nested drawing, or in a restrictive mode any file the mode denies, so the flatten reports the denial). A chart's picture links are taken as written and percent-decoded, as the tool's image resolver opens them. A document page is its one file. Each path is taken once; in a restrictive file-access mode every referenced file is read through the policy the tool draws the picture under (a core FileReferenceResolver that judges the real path of the opened file), so a file outside the folders is never opened and a file the mode denies, such as a link leading outside the folders or any link under no-symlinks, is never read or hashed, while the default mode reads as before; and more than 64 levels or 512 files stops the build with pandoc-filter-error. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.Tool/PandocFilter/DependencyHasher.cs",
                "src/DemaConsulting.CanvasNet.Tool/PandocFilter/DependencyHasher.Chart.cs",
                "src/DemaConsulting.CanvasNet.Tool/PandocFilter/DependencyHasher.Mermaid.cs",
                "src/DemaConsulting.CanvasNet.Tool/PandocFilter/DependencyHasher.PlantUml.cs",
                "src/DemaConsulting.CanvasNet.Tool/PandocFilter/DependencyHasher.Svg.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.Tool.Tests/PandocFilter/DependencyHasherTests.cs");
            designFiles = ("docs/design/canvas-net-tool/pandoc-filter/dependency-hasher.md");
            verificationFiles = ("docs/verification/canvas-net-tool/pandoc-filter/dependency-hasher.md");
            requirementFiles = ("docs/reqstream/canvas-net-tool/pandoc-filter/dependency-hasher.yaml");
        }
    }
}
