package CanvasNet {
    part def ImageComparison {
        doc /* The command-line side of comparing two images. The measures, the per-cell grid, and the difference, overlay, and side-by-side pictures are computed by the core Surface (the SurfaceComparison unit); this unit checks a comparison against thresholds (mae, rmse, max-diff, diff-ratio, and cell-rmse must be at most the limit; psnr and ssim at least the limit; the PSNR of identical images is infinite), names the metrics as they are written on the command line and in results with their accepted ranges, and holds the defaults of the comparison options. It includes the ThresholdMetric, ComparisonThreshold, and ThresholdCheck supporting types. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/Imaging/ImageComparison.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/Imaging/ImageComparisonTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/imaging/image-comparison.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/imaging/image-comparison.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/imaging/image-comparison.yaml */
    }
}
