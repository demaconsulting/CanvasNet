package CanvasNet {
    part def Commands {
        doc /* The subsystem in the DemaConsulting.CanvasNet.Tool.Commands namespace that turns the command named on the command line into running code. The ICommand contract (name, aliases, summary, options, usage, and Run, which reports expected failures as ToolException) and the OptionSpec option description define what a command is; the CommandRegistry unit holds the table of commands, resolves a typed name or alias, dispatches to the command, suggests the closest name for an unknown command, and renders the top-level and per-command help. The individual command units are added to this subsystem as the commands are implemented. As of this release, its constituent units are CommandRegistry and DiscoveryCommands (the formats command). */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/Commands/CommandsSubsystemTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/commands.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/commands.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/commands.yaml */

        part commandRegistry : CommandRegistry;
        part discoveryCommands : DiscoveryCommands;
    }
}
