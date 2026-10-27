package CanvasNet {
    part def ImageAnalysis {
        doc /* Reads what an image contains without changing it: Analyze counts the pixels, flattened over an opaque background, in 4096 buckets of the top four bits of each channel, clusters the buckets (in order of count, ties by bucket index) greedily by the distance to the cluster leader (at most 2 bucket units on every channel), and reports the most common colors (1 to 32) as the mean color of the cluster with its pixel count and its fraction, together with the luma statistics of the same pass (mean, range, percentiles 1, 50, and 99, the Otsu threshold, and a 16-bin histogram); Sample reports the pixel at a point with its luma and, for a radius, the window around it clipped to the image with the mean of each channel, and rejects a point outside the image as a usage error. All arithmetic is exact integer arithmetic with explicit tie-breaks. The unit works on surfaces only and is deterministic. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/Imaging/ImageAnalysis.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/Imaging/ImageAnalysisTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/imaging/image-analysis.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/imaging/image-analysis.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/imaging/image-analysis.yaml */
    }
}
