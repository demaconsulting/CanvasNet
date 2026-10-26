package CanvasNet {
    part def CommandRegistry {
        doc /* The table of commands the tool offers, and the ICommand and OptionSpec supporting types. It registers commands (rejecting duplicate names and aliases and the reserved name help), finds a command by name or alias ignoring case, runs the command selected on the command line (recording the command name on a ToolException that escapes without one and letting every other exception propagate), reports an unknown command as a usage-error ToolException suggesting the closest name within an edit distance of two, runs the built-in help command, and renders the top-level help (usage, commands with aliases and summaries, global options) and the per-command help (usage, summary, options, global options). The registry is an instance so that tests can inject purpose-made commands. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/Commands/CommandRegistry.cs, src/DemaConsulting.CanvasNet.Tool/Commands/ICommand.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/Commands/CommandRegistryTests.cs, test/DemaConsulting.CanvasNet.Tool.Tests/TestCommand.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/commands/command-registry.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/commands/command-registry.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/commands/command-registry.yaml */
    }
}
