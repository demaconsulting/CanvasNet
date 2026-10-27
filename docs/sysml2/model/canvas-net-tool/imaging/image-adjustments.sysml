package CanvasNet {
    part def ImageAdjustments {
        doc /* Adjusts the tone and color of the pixels of a surface in place: Luma is the BT.601 fixed-point luma (77 R + 150 G + 29 B + 128) >> 8, flattened over white for a translucent pixel; Grayscale sets red, green, and blue to the luma and keeps alpha; Invert complements the color channels; Threshold makes a pixel opaque white when its luma over white is above a level and opaque black otherwise; OtsuThreshold chooses the level that maximizes the between-class variance of a 256-bin luma histogram with exact 64-bit integer sums, the middle of the plateau of tied levels, and 127 when there is no choice; BuildLevelsTable, BuildBrightnessTable, and BuildContrastTable build 256-entry tables from a black level, white level, and gamma, an additive amount, and a contrast around mid-gray; ApplyTable maps red, green, and blue through a table; AutoLevelsRange and AutoLevels stretch an image from the clipped percentile black and white points of its luma histogram and leave a flat image alone. LumaHistogram sums rows in fixed blocks of 64 rows merged in block order and every operation processes rows in parallel with disjoint output, so results do not depend on the number of cores. The unit works on surfaces only and is deterministic. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/Imaging/ImageAdjustments.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/Imaging/ImageAdjustmentsTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/imaging/image-adjustments.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/imaging/image-adjustments.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/imaging/image-adjustments.yaml */
    }
}
