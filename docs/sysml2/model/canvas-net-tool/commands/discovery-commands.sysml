package CanvasNetTool_DiscoveryCommands {
    private import SoftwareItemMetadata::*;

    part def DiscoveryCommands {
        doc /* The read-only commands that tell a caller what the tool can do or what a file is, and the table of formats they report. It provides the formats command (the nested class DiscoveryCommands.Formats), which lists the input formats (name, kind, extensions, whether a file holds several pages, whether the content is rendered at the requested size), the output formats (name, extensions, alpha, lossy), the maximum image dimension, the JPEG quality range and default, and the tool and library versions, as an aligned text listing or, when the JSON option is given, as one JSON success document;         and the info command (the nested class DiscoveryCommands.Info), which describes one file from its header (format, pixel size, channels, transparency, size in bytes, and, when they apply, frame count and EXIF orientation; for a PDF, PPTX, or VSDX document the number of pages and the size of the first page, the page chosen with --page, or every page with --all-pages; for an OpenSCAD model the names of its modules and functions and its include and use directives; for a Vega-Lite chart the laid-out size and, in a chart object, its marks, data references, parameters, title, and schema version; for a Mermaid or PlantUML diagram the laid-out size and, in a diagram object, its type, whether this version draws it (supported), its direction, title, and accTitle when it has them, and its numbers of nodes, edges, and subgraphs; for a MathML equation the natural size and, in an equation object, its display, number of elements, baseline, altText when it has one, and annotation encodings) as text or a JSON document. The formats listing includes the vega-lite format (kind chart, extensions .vl.json and .vl) and the mermaid format (kind diagram, extensions .mmd and .mermaid). The format tables are kept consistent with the source kinds and output formats the imaging code implements. Both commands report command-line mistakes as a usage-error. The info command reads the fragment of its input path as the render command does and uses only its page (the other parameters are checked and ignored); the warnings about the fragment are written to standard error and the JSON input object has a fragment member. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.Tool/Commands/DiscoveryCommands.cs");
            testFiles = (
                "test/DemaConsulting.CanvasNet.Tool.Tests/Commands/DiscoveryCommandsTests.cs",
                "test/DemaConsulting.CanvasNet.Tool.Tests/Commands/DiscoveryCommandsDocumentTests.cs",
                "test/DemaConsulting.CanvasNet.Tool.Tests/Commands/DiscoveryCommandsModelTests.cs",
                "test/DemaConsulting.CanvasNet.Tool.Tests/Commands/DiscoveryCommandsChartTests.cs",
                "test/DemaConsulting.CanvasNet.Tool.Tests/Commands/DiscoveryCommandsDiagramTests.cs",
                "test/DemaConsulting.CanvasNet.Tool.Tests/Commands/DiscoveryCommandsPlantUmlTests.cs",
                "test/DemaConsulting.CanvasNet.Tool.Tests/Commands/DiscoveryCommandsMathMlTests.cs",
                "test/DemaConsulting.CanvasNet.Tool.Tests/Commands/InputFragmentTests.cs"
            );
            designFiles = ("docs/design/canvas-net-tool/commands/discovery-commands.md");
            verificationFiles = ("docs/verification/canvas-net-tool/commands/discovery-commands.md");
            requirementFiles = ("docs/reqstream/canvas-net-tool/commands/discovery-commands.yaml");
        }
    }
}
