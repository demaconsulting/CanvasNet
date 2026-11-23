package CanvasNetCharts_ChartRenderer {
    private import SoftwareItemMetadata::*;

    part def ChartRenderer {
        doc /* The ChartRenderer, ChartRenderOptions, and ChartColorPalette public types: a pixel-rendering engine that paints a validated Chart onto a new core CanvasNet Canvas.Surface, dispatching per ChartType to paint bars/columns/lines/areas/pie or doughnut wedges, plus axes, a legend, a title, and opt-in data labels. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.Charts/ChartRenderer.cs",
                "src/DemaConsulting.CanvasNet.Charts/ChartRenderer.Layout.cs",
                "src/DemaConsulting.CanvasNet.Charts/ChartRenderer.CategoryChart.cs",
                "src/DemaConsulting.CanvasNet.Charts/ChartRenderer.PieChart.cs",
                "src/DemaConsulting.CanvasNet.Charts/ChartRenderer.Legend.cs",
                "src/DemaConsulting.CanvasNet.Charts/ChartRenderer.Title.cs",
                "src/DemaConsulting.CanvasNet.Charts/ChartRenderOptions.cs",
                "src/DemaConsulting.CanvasNet.Charts/ChartColorPalette.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.Charts.Tests/ChartRendererTests.cs",
                "test/DemaConsulting.CanvasNet.Charts.Tests/ChartRendererAxisLegendTitleTests.cs",
                "test/DemaConsulting.CanvasNet.Charts.Tests/ChartRendererEdgeCaseTests.cs"
            );
            designFiles = ("docs/design/canvas-net-charts/chart-model/chart-renderer.md");
            verificationFiles = ("docs/verification/canvas-net-charts/chart-model/chart-renderer.md");
            requirementFiles = ("docs/reqstream/canvas-net-charts/chart-model/chart-renderer.yaml");
        }
    }
}
