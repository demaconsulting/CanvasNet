package CanvasNet {
    part def SurfaceComparison {
        doc /* The comparison of the public Surface class with another surface of the same size, in the partial class files Surface.Comparison.cs and Surface.ComparisonImages.cs, plus the public ComparisonMetrics, ComparisonCell, ComparisonResult, and DiffStyle types. Compare computes, on opaque RGB with the alpha channel ignored and with exact integer sums, the mean absolute error, the root mean square error, the peak signal-to-noise ratio (absent for identical images), the largest channel difference, the count and fraction of pixels whose largest channel difference is above a tolerance, the windowed structural similarity of the integer luma (uniform windows of at most 8 by 8 pixels with a stride of 4 and edge-flush final windows), and a grid of columns by rows (cell boundaries floor(i * size / count), so the cells partition the image exactly) with the same error measures for each cell. CreateDiff draws the amplified difference through a hot ramp or per channel, CreateOverlay blends the two surfaces with an integer rule, and CreateSideBySide places the surface, a second surface, and a difference image left to right with gray gaps, scaling the panels down when the sheet would exceed MaxDimension. Deterministic; neither input is modified. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Canvas/Surface.Comparison.cs, src/DemaConsulting.CanvasNet/Canvas/Surface.ComparisonImages.cs, src/DemaConsulting.CanvasNet/Canvas/ComparisonResult.cs, src/DemaConsulting.CanvasNet/Canvas/DiffStyle.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Canvas/SurfaceComparisonTests.cs */
        comment designRef /* Design: docs/design/canvas-net/canvas/surface-comparison.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/canvas/surface-comparison.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/canvas/surface-comparison.yaml */
    }
}
