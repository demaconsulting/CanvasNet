package CanvasNetTool_Validation {
    private import SoftwareItemMetadata::*;

    part def Validation {
        doc /* The self-validation framework behind --validate. It runs the contractual platform-evidence self-tests (CanvasNetTool_VersionDisplay and CanvasNetTool_HelpDisplay, each running the tool in-process into a log file), the formats self-test (CanvasNetTool_Formats, which runs the formats command with JSON output in-process and checks the document), the imaging self-tests (render, convert, info, the transformation commands) and the comparison self-tests (CanvasNetTool_Compare and CanvasNetTool_Montage) and the photo self-test (CanvasNetTool_Adjustments, which runs the tone operations, deskew, oblique rotation, trim, grid, and the inspect commands on a generated sketch) and the chart self-test (CanvasNetTool_RenderVegaLite, which writes an inline-data bar chart and runs info and render on it, checking the chart object, the size, and the pixels of the bars) and, right after it, the diagram self-test (CanvasNetTool_RenderMermaid, which writes a three-node flowchart and runs info and render on it, checking the diagram object, the size, the white background, and the node fill of the default theme), then the PlantUML self-test (CanvasNetTool_RenderPlantUml, which writes a two-participant sequence diagram and runs info and render on it, checking the diagram object, the size, the white background, and the participant fill of PlantUML's default look), then the Pandoc filter self-test (CanvasNetTool_PandocFilter, which runs the pandoc-filter command on a Pandoc JSON document with a captioned Mermaid code block and checks that it became a figure showing the one PNG drawn into generated/canvasnet), and last the help examples self-test (CanvasNetTool_HelpExamples); nineteen self-tests in all, in a documented order, prints a header table and a summary, and optionally writes the results as a TRX file (.trx) or a JUnit XML file (.xml) using DemaConsulting.TestResults; an unsupported results extension is reported as an error. Further self-tests are added with the commands they exercise. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.Tool/Validation.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Tool.Tests/ValidationTests.cs");
            designFiles = ("docs/design/canvas-net-tool/validation.md");
            verificationFiles = ("docs/verification/canvas-net-tool/validation.md");
            requirementFiles = ("docs/reqstream/canvas-net-tool/validation.yaml");
        }
    }
}
