package CanvasNetTool_HelpTopics {
    private import SoftwareItemMetadata::*;
    private import CanvasNetViewer_CanvasNetHelp::*;

    part def HelpTopics {
        doc /* The command-line layer of the help cards. The cards about the drawing and file formats are the library cards of the Viewer (CanvasNetHelp of the Help subsystem of CanvasNetViewer), which describe them in library terms; this unit composes them with the tool's own source, the embedded resources DemaConsulting.CanvasNet.Tool.Help/<path> of the folder Commands/Help of the tool assembly (named canvasnet-tool by its _source.md), which imports the library source and holds tool cards (json, json/results, json/diagnostics, json/help, and pandoc, the card of the pandoc-filter command and its Lua shim) and overlays (extends: <path>) of library cards that append a Command line section (the flags, shell blocks and quoting, and the canvasnet commands that render, inspect, compare, montage, and flatten), command examples, and related tool cards; the tool's own token is schemaVersion. Configure builds the combined catalog with DemaConsulting.HelpCards: CanvasNetHelp.Configure, the tool source, the tool name canvasnet, the tool version, and the eighteen expected roots (TopicNames: the sixteen library roots, then json and pandoc), with aliases (for example openscad, vl, mmd, powerpoint, visio, png or jpg for raster, and filter for pandoc). Catalog is that catalog, built once through a thread-safe lazy value; Find finds a root by name or alias. RenderOptions gives the HelpCards render options of the command line (links written as canvasnet help commands), Envelope writes a help document in the envelope of ResultWriter (schemaVersion, ok, command help, then kind, which an error leaves out), WriteError writes the error object {code, message, hint} of a help failure (every HelpCards code is usage-error except a defective catalog, operation-failed), and RenderTopics renders the whole tree (kind topics). Its tests verify the command-line layer against the tool (the command lines and their quoting, the option terms, the error codes, the formats table, the user guide's flatten and file access lists, and the commands of the framed-figure and selectors sections, run through the tool) and validate the combined cards with the HelpCards validator; the library cards are verified by the tests of the library. It is used by the CommandRegistry unit, which owns the help command, and by the help examples self-test of the Validation unit. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.Tool/Commands/HelpTopics.cs",
                "src/DemaConsulting.CanvasNet.Tool/Commands/HelpTopics.Render.cs",
                "src/DemaConsulting.CanvasNet.Tool/Commands/Help/"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.Tool.Tests/Commands/HelpTopicsTests.cs",
                "test/DemaConsulting.CanvasNet.Tool.Tests/Commands/HelpTopicsTests.Tree.cs",
                "test/DemaConsulting.CanvasNet.Tool.Tests/Commands/HelpTopicsTests.Overlays.cs",
                "test/DemaConsulting.CanvasNet.Tool.Tests/Commands/HelpTopicsTests.CommandLine.cs",
                "test/DemaConsulting.CanvasNet.Tool.Tests/Commands/HelpCardText.cs",
                "test/DemaConsulting.CanvasNet.Tool.Tests/Commands/HelpView.cs",
                "test/DemaConsulting.CanvasNet.Tool.Tests/Commands/InputFragmentTests.cs"
            );
            designFiles = ("docs/design/canvas-net-tool/commands/help-topics.md");
            verificationFiles = ("docs/verification/canvas-net-tool/commands/help-topics.md");
            requirementFiles = ("docs/reqstream/canvas-net-tool/commands/help-topics.yaml");
        }
        comment /* src/DemaConsulting.CanvasNet.Tool/Commands/Help/: the embedded tool cards, the overlays of library cards, and _source.md */
        comment /* HelpTopics uses the OTS library DemaConsulting.HelpCards, documented in docs/design/ots/help-cards.md. */

        dependency usesCanvasNetHelp from HelpTopics to CanvasNetHelp;
    }
}
