package CanvasNet {
    part def ImageComparison {
        doc /* Compares two images of the same size and creates pictures of how they differ. Compare computes, on opaque RGB with the alpha channel ignored and with exact integer sums, the mean absolute error, the root mean square error, the peak signal-to-noise ratio (absent for identical images), the largest channel difference, the count and fraction of pixels whose largest channel difference is above a tolerance, and a grid of columns by rows (cell boundaries floor(i * size / count), so the cells partition the image exactly) with the same measures for each cell; Ssim computes the mean structural similarity of the integer luma over uniform windows of at most 8 by 8 pixels with a stride of 4 and edge-flush final windows; Evaluate checks the measures against thresholds (mae, rmse, max-diff, diff-ratio, and cell-rmse must be at most the limit; psnr and ssim at least the limit). CreateDiff draws the amplified difference through a hot ramp or per channel, CreateOverlay blends the two images with an integer rule, and CreateSideBySide places the first image, the second image, and the difference left to right with gray gaps, scaling the panels down when the sheet would exceed the maximum image size. The unit works on surfaces only and is deterministic. It includes the ComparisonMetrics, GridCell, ComparisonResult, DiffStyle, ThresholdMetric, ComparisonThreshold, and ThresholdCheck supporting types. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/Imaging/ImageComparison.cs, src/DemaConsulting.CanvasNet.Tool/Imaging/ImageComparison.Images.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/Imaging/ImageComparisonTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/imaging/image-comparison.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/imaging/image-comparison.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/imaging/image-comparison.yaml */
    }
}
