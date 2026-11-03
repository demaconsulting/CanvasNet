package CanvasNet {
    part def CommandRegistry {
        doc /* The table of commands the tool offers, and the ICommand and OptionSpec supporting types. It registers commands (rejecting duplicate names and aliases and the reserved name help), finds a command by name or alias ignoring case, runs the command selected on the command line (recording the command name on a ToolException that escapes without one and letting every other exception propagate), reports an unknown command as a usage-error ToolException suggesting the closest name within an edit distance of two, runs the built-in help command, and renders the top-level help (usage, commands with aliases and summaries, help topics, global options) and the per-command help (usage, summary, options, global options). The built-in help command (the partial class file CommandRegistry.Help.cs) takes a command or a help topic by name or alias, or topics for the index, prints one example of a topic alone with --example or lists them with --examples, writes every help output as one JSON document with --json (the kinds index, command, topic, examples, and example), and reports an unknown subject as a usage error suggesting the closest subject and listing the valid ones; a topic name typed as a command gets a hint that it is a help topic. The help content of the topics is the HelpTopics unit. The registry is an instance so that tests can inject purpose-made commands. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/Commands/CommandRegistry.cs, src/DemaConsulting.CanvasNet.Tool/Commands/CommandRegistry.Help.cs, src/DemaConsulting.CanvasNet.Tool/Commands/ICommand.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/Commands/CommandRegistryTests.cs, test/DemaConsulting.CanvasNet.Tool.Tests/TestCommand.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/commands/command-registry.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/commands/command-registry.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/commands/command-registry.yaml */
    }
}
