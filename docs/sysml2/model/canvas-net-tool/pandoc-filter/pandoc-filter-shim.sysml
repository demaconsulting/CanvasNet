package CanvasNetTool_PandocFilterShim {
    private import SoftwareItemMetadata::*;

    part def PandocFilterShim {
        doc /* The Lua shim of the Pandoc filter (canvasnet.lua), embedded in the tool and kept in the repository as docs/filters/canvasnet.lua, and the protocol it speaks with the pandoc-filter command. The shim starts the tool only when the document has a code block of a class the engine draws (mermaid, plantuml, mathml, vega-lite, vegalite), a code block of a class the engine draws only with the class draw (openscad) that has draw, or a local picture whose name ends in a suffix the engine may change (.svg, .vl.json, .vl, .scad, .vsdx, .pptx, .pdf, .mmd, .mermaid, .puml, .plantuml, .pu, .mml, in any letter case), lists that the class generates from the PictureAttributes tables when it writes the shim from the embedded template, so the shim is written again after the tool is updated; it runs CANVASNET_TOOL (split on white space, double quotes grouping) or dotnet canvasnet once with pandoc-filter --json --protocol 1, the filter path, the format, the Pandoc version, the resource path, the output file, and embed-resources, piping the whole document as JSON; it maps a start failure (any error of pandoc.pipe that is not a table with a numeric error_code, read in protected mode), the JSON error message and hint, or a bare exit code to a canvasnet.lua error, shows the warnings of the metadata key canvasnet-warnings through pandoc.log.warn, and replaces each canvasnet-markdown placeholder Span with its caption parsed as Markdown. The class generates the shim from the embedded template, accepts protocol 1 (MinProtocol to MaxProtocol) and otherwise raises usage-error naming the --write-shim command or the tool update, and writes the shim byte for byte through a partial file moved into place. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.Tool/PandocFilter/PandocFilterShim.cs",
                "src/DemaConsulting.CanvasNet.Tool/PandocFilter/canvasnet.lua",
                "docs/filters/canvasnet.lua"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.Tool.Tests/PandocFilter/PandocFilterShimTests.cs");
            designFiles = ("docs/design/canvas-net-tool/pandoc-filter/pandoc-filter-shim.md");
            verificationFiles = ("docs/verification/canvas-net-tool/pandoc-filter/pandoc-filter-shim.md");
            requirementFiles = ("docs/reqstream/canvas-net-tool/pandoc-filter/pandoc-filter-shim.yaml");
        }
    }
}
