package CanvasNet_SurfaceAdjustments {
    private import SoftwareItemMetadata::*;

    part def SurfaceAdjustments {
        doc /* The tone and color adjustments of the public Surface class, in the partial class file Surface.Adjustments.cs. Surface.Luma is the BT.601 fixed-point luma (77 R + 150 G + 29 B + 128) >> 8, flattened over white for a translucent pixel; Grayscale sets red, green, and blue to the luma and keeps alpha; Invert complements the color channels; Threshold makes a pixel opaque white when its luma over white is above a level and opaque black otherwise; OtsuThreshold chooses the level that maximizes the between-class variance of a 256-bin luma histogram with exact 64-bit integer sums, the middle of the plateau of tied levels, and 127 when there is no choice; Levels, Brightness, and Contrast map red, green, and blue through a 256-entry table built from a black level, white level, and gamma, an additive amount, or a contrast around mid-gray; AutoLevels stretches an image from the clipped percentile black and white points of its luma histogram and leaves a flat image alone. LumaHistogram sums rows in fixed blocks of 64 rows merged in block order and every adjustment processes rows in parallel with disjoint output, so results do not depend on the number of cores. The adjustments work in place on a surface the caller owns, know nothing of files or commands, and are deterministic. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet/Canvas/Surface.Adjustments.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Tests/Canvas/SurfaceAdjustmentsTests.cs");
            designFiles = ("docs/design/canvas-net/canvas/surface-adjustments.md");
            verificationFiles = ("docs/verification/canvas-net/canvas/surface-adjustments.md");
            requirementFiles = ("docs/reqstream/canvas-net/canvas/surface-adjustments.yaml");
        }
    }
}
