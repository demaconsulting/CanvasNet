package CanvasNet {
    part def ImageGeometry {
        doc /* The geometry of a surface beyond right-angle moves. RotatedSize calculates the canvas of an image rotated by any finite angle as the ceiling of the bounding box of the rotated rectangle (a tolerance of 1e-9 so an exact fit gains no pixel) and CheckRotatedSize refuses a canvas larger than 8192 pixels on a side with the size-limit error and a hint to shrink first, before anything is allocated. RotateArbitrary rotates clockwise about the centers of the image and the expanded canvas, calculating every destination pixel from its own coordinates, with nearest sampling or bilinear sampling in premultiplied form in which a tap outside the image is the fill color so that the rotated edge fades into it, storing a fully transparent pixel as transparent black, with fast paths for all-inside and all-outside taps that give the pixels of the generic sampler. FindTrimBounds finds the inclusive bounding box of the content inside a uniform border: the background is the most frequent corner color, the distance between two transparent pixels is 0 and otherwise the largest absolute difference of red, green, blue, and alpha, and a pixel is content when its distance exceeds a tolerance; Trim crops to it and copies an all-background image. Rows are processed in parallel with disjoint output, so results do not depend on the number of cores. The unit works on surfaces only and is deterministic. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/Imaging/ImageGeometry.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/Imaging/ImageGeometryTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/imaging/image-geometry.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/imaging/image-geometry.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/imaging/image-geometry.yaml */
    }
}
