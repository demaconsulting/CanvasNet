package CanvasNet {
    part def DiscoveryCommands {
        doc /* The read-only commands that tell a caller what the tool can do, and the table of formats they report. As of this release it provides the formats command (the nested class DiscoveryCommands.Formats), which lists the input formats (name, kind, extensions, whether a file holds several pages, whether the content is rendered at the requested size), the output formats (name, extensions, alpha, lossy), the maximum image dimension, the JPEG quality range and default, and the tool and library versions, as an aligned text listing or, when the JSON option is given, as one JSON success document. It takes no arguments of its own: a stray argument or option is a usage-error. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/Commands/DiscoveryCommands.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/Commands/DiscoveryCommandsTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/commands/discovery-commands.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/commands/discovery-commands.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/commands/discovery-commands.yaml */
    }
}
