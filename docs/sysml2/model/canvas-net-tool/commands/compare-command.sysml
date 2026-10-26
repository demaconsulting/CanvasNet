package CanvasNet {
    part def CompareCommand {
        doc /* The compare command: loads two inputs (images, drawings, document pages, or OpenSCAD model views) through the SourceLoader like the input of the render command, applying the options shared by both inputs only to an input they concern and the per-side options (a-page, b-page, a-dpi, b-dpi, a-view, b-view, a-define, b-define) over them, flattens both over an opaque background, brings them to one size with resize-to (a, b, or WxH; a vector input is rendered at the target size and a raster input is fitted with contain or stretch) or fails with size-mismatch, measures them with the ImageComparison unit (MAE, RMSE, PSNR, SSIM, the differing-pixel fraction with a tolerance, and the error of each cell of a grid), optionally writes a difference image, a side-by-side image, and an overlay through the ImageWriter (all staged first and published together), checks the measures against thresholds, and reports the result as text or as one JSON document. A threshold that is not met is the expected error threshold-failed (exit code 1) raised after the result and the pictures were written; with the JSON option the failure document carries the result fields. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/Commands/CompareCommand.cs, src/DemaConsulting.CanvasNet.Tool/Commands/CompareCommand.Options.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/Commands/CompareCommandTests.cs, test/DemaConsulting.CanvasNet.Tool.Tests/Commands/CompareCommandSourceTests.cs, test/DemaConsulting.CanvasNet.Tool.Tests/Commands/CompareCommandOutputTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/commands/compare-command.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/commands/compare-command.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/commands/compare-command.yaml */
    }
}
