package CanvasNet {
    part def RenderCommand {
        doc /* The render command and its alias convert: loads an image or drawing and writes it as PNG, JPEG, BMP, or TIFF in one pass. It declares its options (scale, width, height, size, background, no-auto-orient, output, format, quality, no-clobber) in one table that also drives the help, reads and validates them (rejecting contradictory size options, malformed values, and sizes beyond the maximum), reports every mistake about the input and the output before decoding anything, loads the source through the SourceLoader, writes it through the ImageWriter, and reports the result as one text line or, when the JSON option is given, as one JSON document with the tool, the input, the operations applied, the outputs, the diagnostics, and the echoed text. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/Commands/RenderCommand.cs, src/DemaConsulting.CanvasNet.Tool/Commands/RenderCommand.Options.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/Commands/RenderCommandTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/commands/render-command.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/commands/render-command.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/commands/render-command.yaml */
    }
}
