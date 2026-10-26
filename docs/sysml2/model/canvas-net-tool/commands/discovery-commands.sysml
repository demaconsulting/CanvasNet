package CanvasNet {
    part def DiscoveryCommands {
        doc /* The read-only commands that tell a caller what the tool can do or what a file is, and the table of formats they report. It provides the formats command (the nested class DiscoveryCommands.Formats), which lists the input formats (name, kind, extensions, whether a file holds several pages, whether the content is rendered at the requested size), the output formats (name, extensions, alpha, lossy), the maximum image dimension, the JPEG quality range and default, and the tool and library versions, as an aligned text listing or, when the JSON option is given, as one JSON success document;         and the info command (the nested class DiscoveryCommands.Info), which describes one file from its header (format, pixel size, channels, transparency, size in bytes, and, when they apply, frame count and EXIF orientation; for a PDF, PPTX, or VSDX document the number of pages and the size of the first page, the page chosen with --page, or every page with --all-pages; for an OpenSCAD model the names of its modules and functions and its include and use directives) as text or a JSON document. The format tables are kept consistent with the source kinds and output formats the imaging code implements. Both commands report command-line mistakes as a usage-error. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/Commands/DiscoveryCommands.cs */
                comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/Commands/DiscoveryCommandsTests.cs, test/DemaConsulting.CanvasNet.Tool.Tests/Commands/DiscoveryCommandsDocumentTests.cs, test/DemaConsulting.CanvasNet.Tool.Tests/Commands/DiscoveryCommandsModelTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/commands/discovery-commands.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/commands/discovery-commands.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/commands/discovery-commands.yaml */
    }
}
