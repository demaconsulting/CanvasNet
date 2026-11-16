package CanvasNet {
    part def Commands {
        doc /* The subsystem in the DemaConsulting.CanvasNet.Tool.Commands namespace that turns the command named on the command line into running code. The ICommand contract (name, aliases, summary, options, usage, and Run, which reports expected failures as ToolException) and the OptionSpec option description define what a command is; the CommandRegistry unit holds the table of commands, resolves a typed name or alias, dispatches to the command, suggests the closest name for an unknown command, and renders the top-level and per-command help and the help cards (through the HelpTopics unit).         The individual command units are added to this subsystem as the commands are implemented. As of this release, its constituent units are CommandRegistry, HelpTopics (the tree of short help cards for the input formats, style and composition, the result documents, errors, limits, and file access, with verified examples, shipped inside the tool), DiscoveryCommands (the formats and info commands), RenderCommand (the render command and its alias convert, and the verb commands crop, resize, rotate, flip, and pad), CompareCommand (the compare command), MontageCommand (the montage command), InspectCommand (the inspect command with the subcommands colors and pixel), FlattenCommand (the flatten command), and PandocFilterCommand (the pandoc-filter command that the Lua shim of the Pandoc filter runs; it runs the PandocFilterEngine of the PandocFilter subsystem, whose ToolPictureDrawer in turn calls RenderCommand.Execute and FlattenCommand.Execute). Render (and its verbs), inspect, compare, montage, and flatten share the options --file-access and --allow-root from the render option table and build one FileAccessPolicy per invocation with RenderCommand.ReadFileAccess, which every probe and load of the command receives; info has no such option and probes under FileAccessPolicy.AllowAll. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/Commands/CommandsSubsystemTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/commands.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/commands.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/commands.yaml */

        part commandRegistry : CommandRegistry;
        part helpTopics : HelpTopics;
        part discoveryCommands : DiscoveryCommands;
        part renderCommand : RenderCommand;
        part compareCommand : CompareCommand;
        part montageCommand : MontageCommand;
        part inspectCommand : InspectCommand;
        part flattenCommand : FlattenCommand;
        part pandocFilterCommand : PandocFilterCommand;

        // The pandoc-filter command is the front end of the PandocFilter subsystem
        dependency pandocFilterCommandUsesPandocFilter from pandocFilterCommand to PandocFilter;
    }
}
