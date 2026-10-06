package CanvasNet {
    part def ChartRenderer {
        doc /* The ChartRenderer, ChartRenderOptions, and ChartColorPalette public types: a pixel-rendering engine that paints a validated Chart onto a new core CanvasNet Canvas.Surface, dispatching per ChartType to paint bars/columns/lines/areas/pie or doughnut wedges, plus axes, a legend, a title, and opt-in data labels. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Charts/ChartRenderer.cs, src/DemaConsulting.CanvasNet.Charts/ChartRenderer.Layout.cs, src/DemaConsulting.CanvasNet.Charts/ChartRenderer.CategoryChart.cs, src/DemaConsulting.CanvasNet.Charts/ChartRenderer.PieChart.cs, src/DemaConsulting.CanvasNet.Charts/ChartRenderer.Legend.cs, src/DemaConsulting.CanvasNet.Charts/ChartRenderer.Title.cs, src/DemaConsulting.CanvasNet.Charts/ChartRenderOptions.cs, src/DemaConsulting.CanvasNet.Charts/ChartColorPalette.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Charts.Tests/ChartRendererTests.cs, test/DemaConsulting.CanvasNet.Charts.Tests/ChartRendererAxisLegendTitleTests.cs, test/DemaConsulting.CanvasNet.Charts.Tests/ChartRendererEdgeCaseTests.cs */
        comment designRef /* Design: docs/design/canvas-net-charts/chart-model/chart-renderer.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-charts/chart-model/chart-renderer.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-charts/chart-model/chart-renderer.yaml */
    }
}
