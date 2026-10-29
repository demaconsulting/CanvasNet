package CanvasNet {
    part def VegaLiteMarks {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.VegaLite.Marks namespace that turns data rows, resolved scales, and mark properties into scene items: its MarkBuilder unit draws the marks bar, rect, point, circle, square, rule, and tick with the Vega-Lite 6 geometry (bands, continuous thickness, measure axis from the value to zero, symbols at the band center or value, rules and ticks) and paint (filled by default for every mark but point and rule, reduced default opacity for point-like marks and ticks, the property precedence of the mark definition, style, mark configuration, and Vega styles), drops rows with invalid values on continuous channels (the filter behavior of Vega-Lite 6), charges the scene-item budget for each item as it is created (a located VegaLiteRenderException beyond MaxSceneItems; a row that draws nothing costs nothing), polls cancellation every 1,024 rows, and clips to the data rectangle only for clip true. Every other mark type is reported by the FeatureCatalog and yields an empty marks group. The subsystem is internal; its entry point is called by VegaLiteDocument once for the final data rectangle. As of this release, its only unit is MarkBuilder. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Marks/ (the unit test class MarkBuilderTests) */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/marks.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/marks.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/marks.yaml */

        part markBuilder : MarkBuilder;
    }
}
