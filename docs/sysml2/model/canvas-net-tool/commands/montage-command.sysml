package CanvasNet {
    part def MontageCommand {
        doc /* The montage command: composes images, drawings, and the pages of documents (the page chosen with page, or every page with all-pages) and OpenSCAD model views into one contact sheet. It declares its options (output, columns, cell, fit, spacing, padding, background, labels, page, all-pages, dpi, the model options, strict, filter, quality, no-clobber), reports every mistake about the inputs, the pages, and the output before decoding anything, refuses a sheet larger than the maximum image size with size-limit before it loads an input, and composes the sheet as a stream with the ContactSheet unit (each input is loaded into its cell, a vector input rendered at the cell size and a raster input fitted, added, and disposed before the next), with the file names as labels in the bundled font when asked. The sheet is staged and published through the ImageWriter only when complete, and the result is one text line or one JSON document with the sheet geometry and the cells. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/Commands/MontageCommand.cs, src/DemaConsulting.CanvasNet.Tool/Commands/MontageCommand.Options.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/Commands/MontageCommandTests.cs, test/DemaConsulting.CanvasNet.Tool.Tests/Commands/MontageCommandSourceTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/commands/montage-command.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/commands/montage-command.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/commands/montage-command.yaml */
    }
}
