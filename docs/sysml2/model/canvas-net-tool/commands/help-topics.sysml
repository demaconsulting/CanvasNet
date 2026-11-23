package CanvasNetTool_HelpTopics {
    private import SoftwareItemMetadata::*;
    private import CanvasNetViewer_HelpCatalog::*;

    part def HelpTopics {
        doc /* The command-line layer of the help cards. The cards about the drawing and file formats are the help catalog of the Viewer library (HelpCatalog.Default of the Help subsystem of CanvasNetViewer), which describes them in library terms; this unit extends that catalog with HelpCatalog.Extend from the embedded resources DemaConsulting.CanvasNet.Tool.Help/<path>.md of the tool assembly, which are either tool cards (json, json/results, json/diagnostics, json/help, and pandoc, the card of the pandoc-filter command and its Lua shim) or overlays (extends: <path>) of library cards that append a Command line section (the flags, shell blocks and quoting, and the canvasnet commands that render, inspect, compare, montage, and flatten), command examples, and related tool cards, and may replace the title and summary; the tool's own token is schemaVersion. The seventeen root cards are the help topics in a fixed order (scad, vega-lite, mermaid, plantuml, svg, pdf, pptx, vsdx, raster, style, composition, positions, json, errors, limits, files, and pandoc), with aliases (for example openscad, vl, mmd, powerpoint, visio, png or jpg for raster, and filter for pandoc), checked against the roots of the combined catalog. Each card of the combined catalog becomes a command-line HelpCard (Id, Aliases, Tags, Title, Summary, Body, ChildIds, SeeAlso, Examples) whose body is the library body followed by the overlay body, with every neutral link written by FormatLink as the command that prints its target (canvasnet help a b, canvasnet help a b --example name, or --example name). The unit resolves a card by a path of names or aliases ignoring case (an unknown card below a card is a usage error that suggests the closest name and lists the cards at that level), and renders a card as plain text (its title, summary, and body, then the cards below it, its examples, and its See also links as exact commands with the summaries of their targets; deterministic, without color codes) or as a JSON document of schema version 2 (kind card, with the tags of the card), the whole tree (kind topics, with tags), and the examples (kinds examples and example). Its tests verify the command-line layer against the tool (the command lines, the option terms, the error codes, the formats table, the user guide's flatten and file access lists, and the commands of the framed-figure and selectors sections, run through the tool); the library cards are verified by the tests of the library. It is used by the CommandRegistry unit, which owns the help command, and by the help examples self-test of the Validation unit. */

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
                "test/DemaConsulting.CanvasNet.Tool.Tests/Commands/InputFragmentTests.cs"
            );
            designFiles = ("docs/design/canvas-net-tool/commands/help-topics.md");
            verificationFiles = ("docs/verification/canvas-net-tool/commands/help-topics.md");
            requirementFiles = ("docs/reqstream/canvas-net-tool/commands/help-topics.yaml");
        }
        comment /* src/DemaConsulting.CanvasNet.Tool/Commands/Help/: the embedded tool cards and the overlays of library cards */

        dependency usesHelpCatalog from HelpTopics to HelpCatalog;
    }
}
