package CanvasNet {
    part def SurfaceTransforms {
        doc /* The whole-surface transformations of the public Surface class, in the partial class file Surface.Transforms.cs, plus the public FitMode enum. Each is a pure function that returns a new surface and never modifies the surface it is called on. FlipHorizontal, FlipVertical, and Rotate180 move every pixel exactly; ApplyExifOrientation moves every pixel exactly to where an EXIF orientation from 1 to 8 puts it (swapping the size for 5 to 8); Pad surrounds a surface with a border of a given color (refusing a negative width or a result beyond MaxDimension before allocating) and copies the source opaquely; FindTrimBounds finds the box of the content inside a uniform border from the most frequent corner color, a tolerance, and the rule that all transparent pixels are equal, and Trim crops to it (a copy when nothing is content); FitInto fits a surface into a box of an exact size either contained (scaled by the smaller box ratio, found by the public FitInside size calculation, centered on a fill color) or stretched; FlattenOver composites a surface over a solid color; HasTransparency tells whether any pixel is not opaque. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Canvas/Surface.Transforms.cs, src/DemaConsulting.CanvasNet/Canvas/FitMode.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Canvas/SurfaceTransformsTests.cs, test/DemaConsulting.CanvasNet.Tests/Canvas/SurfaceExifOrientationTests.cs */
        comment designRef /* Design: docs/design/canvas-net/canvas/surface-transforms.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/canvas/surface-transforms.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/canvas/surface-transforms.yaml */
    }
}
