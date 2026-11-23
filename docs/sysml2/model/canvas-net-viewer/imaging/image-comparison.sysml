package CanvasNetViewer_ImageComparison {
    private import SoftwareItemMetadata::*;

    part def ImageComparison {
        doc /* The command-line side of comparing two images. The measures, the per-cell grid, and the difference, overlay, and side-by-side pictures are computed by the core Surface (the SurfaceComparison unit); this unit checks a comparison against thresholds (mae, rmse, max-diff, diff-ratio, and cell-rmse must be at most the limit; psnr and ssim at least the limit; the PSNR of identical images is infinite), names the metrics as they are written on the command line and in results with their accepted ranges, and holds the defaults of the comparison options. It includes the ThresholdMetric, ComparisonThreshold, and ThresholdCheck supporting types. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.Viewer/Imaging/ImageComparison.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Viewer.Tests/Imaging/ImageComparisonTests.cs");
            designFiles = ("docs/design/canvas-net-viewer/imaging/image-comparison.md");
            verificationFiles = ("docs/verification/canvas-net-viewer/imaging/image-comparison.md");
            requirementFiles = ("docs/reqstream/canvas-net-viewer/imaging/image-comparison.yaml");
        }
    }
}
