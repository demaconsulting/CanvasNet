package CanvasNet {
    part def MarkBuilder {
        doc /* The internal MarkBuilder (a sealed class) with IsSupported (true for bar, rect, point, circle, square, rule, and tick) and Build, which reads the mark type (a string or the type of a mark definition), loops over the rows of the DataTable (cancellation polled every 1,024 rows, the scene budget charged for each item as it is created), maps the channels x, y, color, fill, stroke, size, and opacity through the scale models (field, value, and datum forms), resolves the mark properties through the ResolvedConfig precedence chain, and creates the SceneRect, SceneSymbol, and SceneRule items of a marks group in the coordinates of the data rectangle: bars, rectangles, and ticks fill the band on a discrete position (at least minBandSize; step minus 2 on a point scale) and are centered with continuousBandSize or thickness on a continuous one, the measure axis runs from the value to zero, symbols sit at the band center or the value with the size as the area of the bounding box, and rules are full-width or full-height lines or stems to zero; paint follows Vega-Lite (the mark color is the fill of a filled mark and the stroke of an unfilled one, ordinal colors from tableau10, default opacity 0.7 for point, circle, square, and tick). A row with a null, NaN, or non-numeric value on a continuous channel is not drawn. Exceeding MaxSceneItems is a VegaLiteRenderException with the location of the mark, and cancellation is polled every 1,024 rows. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/Marks/MarkBuilder.cs, src/DemaConsulting.CanvasNet.VegaLite/Marks/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Marks/MarkBuilderTests.cs */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/marks/mark-builder.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/marks/mark-builder.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/marks/mark-builder.yaml */
    }
}
