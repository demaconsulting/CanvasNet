package CanvasNet {
    part def SurfaceAnalysis {
        doc /* The analysis of the public Surface class, in the partial class file Surface.Analysis.cs, plus the public DominantColor, LumaStatistics, SurfaceStatistics, PixelWindow, and PixelSample records. Analyze flattens each pixel over an opaque background, counts the pixels in 4096 buckets of 4 bits per channel, clusters the buckets greedily into dominant colors (a bucket joins the first cluster whose leader is within 2 bucket units on every channel), and reports the luma statistics (mean, range, percentiles 1, 50, and 99, the Otsu threshold, and a 16-bin histogram) from the same sequential pass; Sample reads the pixel at a point with its luma and, for a radius up to 8, the window around it clipped to the surface with the mean of each channel. Exact integer arithmetic with explicit tie-breaks, so the result is the same on every run; the surface is never modified. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Canvas/Surface.Analysis.cs, src/DemaConsulting.CanvasNet/Canvas/SurfaceStatistics.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Canvas/SurfaceAnalysisTests.cs */
        comment designRef /* Design: docs/design/canvas-net/canvas/surface-analysis.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/canvas/surface-analysis.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/canvas/surface-analysis.yaml */
    }
}
