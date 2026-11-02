package CanvasNet {
    part def SurfaceGeometry {
        doc /* The rotation and skew estimation of the public Surface class, in the partial class files Surface.Geometry.cs and Surface.Skew.cs, plus the public SkewEstimate record. RotatedSize sizes the canvas of an image rotated by any finite angle as the ceiling of the bounding box of the rotated rectangle; RotateArbitrary rotates clockwise about the centers onto that canvas with a fill color, computing every destination pixel from its own coordinates with nearest or bilinear (premultiplied) sampling, in parallel rows with disjoint output, and refuses a canvas larger than MaxDimension before allocating; EstimateSkew finds the tilt of the dominant near-horizontal lines within 15 degrees by scoring dark-mark profile angles on a gray analysis plane of at most 1024 pixels with exact integer arithmetic and returns a SkewEstimate with its confidence, the thresholds that make it correctable, and the correction angle. Pure functions that never modify the surface they are called on. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Canvas/Surface.Geometry.cs, src/DemaConsulting.CanvasNet/Canvas/Surface.Skew.cs, src/DemaConsulting.CanvasNet/Canvas/SkewEstimate.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Canvas/SurfaceGeometryTests.cs, test/DemaConsulting.CanvasNet.Tests/Canvas/SurfaceSkewTests.cs */
        comment designRef /* Design: docs/design/canvas-net/canvas/surface-geometry.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/canvas/surface-geometry.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/canvas/surface-geometry.yaml */
    }
}
