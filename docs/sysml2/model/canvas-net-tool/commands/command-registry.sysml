package CanvasNetTool_CommandRegistry {
    private import SoftwareItemMetadata::*;

    part def CommandRegistry {
        doc /* The table of commands the tool offers, and the ICommand and OptionSpec supporting types. It registers commands (rejecting duplicate names and aliases and the reserved name help), finds a command by name or alias ignoring case, runs the command selected on the command line (recording the command name on a ToolException that escapes without one and letting every other exception propagate), reports an unknown command as a usage-error ToolException suggesting the closest name within an edit distance of two, runs the built-in help command, and renders the top-level help (usage, commands with aliases and summaries, help topics, global options) and the per-command help (usage, summary, options, global options). The built-in help command (the partial class file CommandRegistry.Help.cs) takes a command by name or alias, a help card path (a topic by name or alias, then the names or aliases of the cards below it, such as mermaid gantt), or topics for the tree of every card, prints one example of a card or a card below it alone with --example or prints the card's own examples in full and lists the cards below it that have examples with --examples, writes every help output as one JSON document with --json (the kinds index, command, topics, card, examples, and example), and reports an unknown subject or card as a usage error suggesting the closest name and listing the valid ones; a topic name typed as a command gets a hint that it is a help topic. The top-level help lists the help topics and points to help topics, help style, and help composition. The help content of the cards is the HelpTopics unit. The registry is an instance so that tests can inject purpose-made commands. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.Tool/Commands/CommandRegistry.cs",
                "src/DemaConsulting.CanvasNet.Tool/Commands/CommandRegistry.Help.cs",
                "src/DemaConsulting.CanvasNet.Tool/Commands/ICommand.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.Tool.Tests/Commands/CommandRegistryTests.cs",
                "test/DemaConsulting.CanvasNet.Tool.Tests/TestCommand.cs"
            );
            designFiles = ("docs/design/canvas-net-tool/commands/command-registry.md");
            verificationFiles = ("docs/verification/canvas-net-tool/commands/command-registry.md");
            requirementFiles = ("docs/reqstream/canvas-net-tool/commands/command-registry.yaml");
        }
    }
}
